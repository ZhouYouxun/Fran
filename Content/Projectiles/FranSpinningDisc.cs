using System;
using System.Collections.Generic;
using System.IO;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Content.Systems;
using FranMod.Content.UI;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.ID;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// Cursor-controlled right-click template. Each beat shares the left chain's timing,
// damage weight and skill budget; visual rotation never grants extra hit events.
public sealed class FranSpinningDisc : ModProjectile, IShiFuAttack
{
    private Player Owner => Main.player[Projectile.owner];
    private readonly FranSkill[] tactics = new FranSkill[4];
    private readonly Vector2[] tipHistory = new Vector2[128];
    private readonly Vector2[] tipTrailHistory = new Vector2[128];
    private readonly float[] tipAngles = new float[128];
    private readonly FranDiscSkillFlow skillFlow = new();
    private readonly HashSet<int> hitTargets = new();
    private readonly HashSet<int> skillTakerHitTargets = new();
    private readonly HashSet<int> blueResonanceHitTargets = new();
    private int baseDamage;
    private int skillBaseDamage;
    private int beatAge;
    private int beatDuration;
    private int beat;
    private int wisps;
    private int age;
    private int trailCount;
    private int fullAirburstTimer;
    private bool started;
    private bool releasedSkill;
    private bool registeredEvolution;
    private bool thunderHitTriggered;
    private bool orbitingBladesSpawned;
    private float previousAngle;
    private float damageRatio;
    private Vector2 previousCenter;
    private Vector2 cursor;
    private int returnAge;
    private int beatSerial;
    private int hammerDropsEligible;
    private float rotationSoundTravel;
    private int resumeInputGrace;
    private int lastImpactAge = -4;
    private float previousRadius;
    internal void NotifyPaused() => resumeInputGrace = 2;
    private bool Returning => Projectile.ai[0] > 0f;
    internal bool IsReturning => Returning;
    private float Opacity => Math.Min(1f, age / 10f) * (Returning ? Math.Max(0f, 1f - returnAge / 24f) : 1f);
    private float BladeRadius => FranShiFuVisuals.BladeLengthFromGripPixels * 1.05f * FranShiFuVisuals.TextureScale;
    internal float Radius => BladeRadius;
    internal Color DiscAccent => Accent;
    internal float DiscOpacity => Opacity;
    internal int SkillDamageForBeat(int combo) => Math.Max(1,
        (int)(baseDamage * FranBalance.GetComboDamageMultiplier(combo)));
    internal int SkillCycle => Math.Max(0, (beatSerial - 1) / 4);
    internal int OrbitingBladeCount => Math.Clamp(Layers(FranSkill.MultipleBodyCreation),
        0, FranPlayer.MaximumTacticSlots);
    internal float OrbitingBladeDamageMultiplier =>
        0.3f + 0.1f * Math.Clamp(Layers(FranSkill.MultipleBodyCreation), 1, FranPlayer.MaximumTacticSlots);
    internal bool AllowHammerDrop() => ++hammerDropsEligible % 5 != 0;
    private FranSkill AccentSkill
    {
        get { foreach (FranSkill skill in tactics) if (FranSkillInfo.IsRealSkill(skill)) return skill; return FranSkill.None; }
    }
    private Color Accent => AccentSkill == FranSkill.None ? Color.White : FranSkillInfo.Color(AccentSkill);
    public override string Texture => FranShiFuVisuals.TexturePath;
    int IShiFuAttack.SkillBaseDamage => skillBaseDamage;

    public void InitializeAttack(int damage, ReadOnlySpan<FranSkill> snapshot)
    {
        baseDamage = damage;
        Array.Fill(tactics, FranSkill.None);
        for (int i = 0; i < Math.Min(snapshot.Length, tactics.Length); i++) tactics[i] = snapshot[i];
    }

    int IShiFuAttack.ReserveSkillTakerWisps(int requested)
    {
        int count = Math.Clamp(requested, 0, Math.Max(0, FranBalance.SkillTakerMaximumWispsPerSwing - wisps));
        wisps += count;
        return count;
    }

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 32;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
        Projectile.netImportant = true;
        Projectile.timeLeft = 2;
    }
    public override bool ShouldUpdatePosition() => false;

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(baseDamage);
        writer.Write(cursor.X); writer.Write(cursor.Y);
        writer.Write(beat); writer.Write(beatAge); writer.Write(beatDuration);
        writer.Write(beatSerial);
        foreach (FranSkill skill in tactics) writer.Write((byte)skill);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        baseDamage = Math.Max(1, reader.ReadInt32());
        Vector2 target = new(reader.ReadSingle(), reader.ReadSingle());
        if (float.IsFinite(target.X) && float.IsFinite(target.Y)) cursor = target;
        beat = Math.Clamp(reader.ReadInt32(), 0, 3);
        beatAge = Math.Max(0, reader.ReadInt32());
        beatDuration = Math.Max(12, reader.ReadInt32());
        int serial = reader.ReadInt32();
        if (serial != beatSerial)
        {
            beatSerial = serial;
            ResetBeatBudget();
        }
        for (int i = 0; i < tactics.Length; i++)
        {
            FranSkill skill = (FranSkill)reader.ReadByte();
            tactics[i] = FranSkillInfo.IsRealSkill(skill) ? skill : FranSkill.None;
        }
    }

    public override void AI()
    {
        // UI input can be cleared while autopause is active. Do not turn that into
        // an irreversible return, and allow input to refresh on resuming the world.
        if (Main.myPlayer == Projectile.owner && Main.netMode == NetmodeID.SinglePlayer &&
            (Main.gamePaused || Main.autoPause && Main.playerInventory))
        {
            Projectile.timeLeft = 2;
            NotifyPaused();
            return;
        }
        if (!Owner.active || Owner.dead || Owner.HeldItem.type != ModContent.ItemType<ShiFu>())
        { Projectile.Kill(); return; }
        Projectile.timeLeft = 2;
        FranPlayer fran = Owner.GetModPlayer<FranPlayer>();
        if (!started)
        {
            started = true;
            if (baseDamage <= 0) baseDamage = Projectile.damage;
            if (Main.myPlayer == Projectile.owner)
            {
                cursor = Main.MouseWorld;
                BeginBeat();
            }
            else if (beatDuration <= 0) beatDuration = 21;
            Projectile.rotation = Projectile.velocity.ToRotation();
            previousAngle = Projectile.rotation;
            previousCenter = Projectile.Center;
            previousRadius = BladeRadius;
            if (!Main.dedServ) SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.25f, Volume = 0.65f }, Projectile.Center);
        }
        age++;
        if (!orbitingBladesSpawned && Main.myPlayer == Projectile.owner && !Returning)
        {
            orbitingBladesSpawned = true;
            int bladeCount = OrbitingBladeCount;
            for (int slot = 0; slot < bladeCount; slot++)
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<FranOrbitingDiscBlade>(),
                    Math.Max(1, (int)MathF.Round(Projectile.damage * OrbitingBladeDamageMultiplier)),
                    Projectile.knockBack * 0.4f, Projectile.owner, Projectile.whoAmI, slot, Projectile.identity);
        }
        // Mouse state is read only by the owning client. ai[0] synchronizes release to peers.
        bool inputReady = resumeInputGrace <= 0;
        if (resumeInputGrace > 0) resumeInputGrace--;
        if (!Returning && (Owner.CCed || fran.UltimateActive || inputReady && Owner.noItems ||
            (Main.myPlayer == Projectile.owner && inputReady &&
                (!PlayerInput.Triggers.Current.MouseRight || FranUISystem.IsWheelOpen))))
        {
            Projectile.ai[0] = 1f;
            Projectile.netUpdate = true;
        }
        previousCenter = Projectile.Center;
        previousAngle = Projectile.rotation;
        if (Returning)
        {
            returnAge++;
            Projectile.Center = Projectile.Center.MoveTowards(Owner.MountedCenter, Math.Max(28f + returnAge * 2f, Vector2.Distance(Projectile.Center, Owner.MountedCenter) * 0.22f));
            if (returnAge >= 24 || Vector2.DistanceSquared(Projectile.Center, Owner.MountedCenter) < 24f * 24f)
            { Projectile.Kill(); return; }
        }
        else
        {
            if (Main.myPlayer == Projectile.owner)
            {
                Vector2 delta = Main.MouseWorld - Owner.MountedCenter;
                float reach = FranBalance.GetDiscDistanceLimit();
                Vector2 target = Owner.MountedCenter + delta.SafeNormalize(Vector2.UnitX * Owner.direction) * Math.Min(delta.Length(), reach);
                if (Vector2.DistanceSquared(target, cursor) > 16f * 16f || age % 8 == 0)
                { cursor = target; Projectile.netUpdate = true; }
            }
            Projectile.Center = Vector2.Lerp(Projectile.Center, cursor, 0.12f).MoveTowards(cursor, 10f);
            float leash = FranBalance.GetDiscDistanceLimit();
            Vector2 tether = Projectile.Center - Owner.MountedCenter;
            if (tether.Length() > leash)
                Projectile.Center = Owner.MountedCenter + tether.SafeNormalize(Vector2.UnitX) * leash;
            if (beatAge >= beatDuration && Main.myPlayer == Projectile.owner) BeginBeat();
            beatAge++;
            float releaseTime = beat switch { 0 => 0.42f, 1 => 0.49f, 2 => 0.45f, _ => 0.46f };
            if (!releasedSkill && beatAge >= beatDuration * releaseTime)
            {
                releasedSkill = true;
                int hammerLayers = Layers(FranSkill.ThorsHammer);
                if (hammerLayers > 0 && beat == 3)
                    ShiFuImprintCombat.TriggerSwing(Owner, Projectile, beat, FranSkill.ThorsHammer,
                        hammerLayers, Projectile.rotation.ToRotationVector2(), BladeRadius);
            }
        }
        float speed = Math.Max(0.25f, Owner.GetWeaponAttackSpeed(Owner.HeldItem));
        float angularStep = Math.Min(0.7f, 0.34f * speed) * 1.75f;
        Projectile.rotation += angularStep;
        rotationSoundTravel += angularStep;
        if (rotationSoundTravel >= MathHelper.TwoPi)
        {
            rotationSoundTravel %= MathHelper.TwoPi;
            if (!Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
        }
        Vector2 blade = Projectile.rotation.ToRotationVector2();
        // Sample the actual circular motion, not straight chords between frame endpoints.
        // At the fastest spin this still keeps each segment below 2.3 degrees.
        int added = Math.Clamp((int)MathF.Ceiling(angularStep / 0.04f), 1, tipHistory.Length - 1);
        Array.Copy(tipHistory, 0, tipHistory, added, tipHistory.Length - added);
        Array.Copy(tipTrailHistory, 0, tipTrailHistory, added, tipTrailHistory.Length - added);
        Array.Copy(tipAngles, 0, tipAngles, added, tipAngles.Length - added);
        for (int i = 0; i < added; i++)
        {
            float t = 1f - i / (float)added;
            float angle = MathHelper.Lerp(previousAngle, Projectile.rotation, t);
            Vector2 center = Vector2.Lerp(previousCenter, Projectile.Center, t);
            Vector2 radial = angle.ToRotationVector2();
            float radius = MathHelper.Lerp(previousRadius, BladeRadius, t);
            tipAngles[i] = angle;
            tipHistory[i] = center + radial * radius;
            // The shader's arc sits inside the physical blade tip. Collision and
            // blade-edge particles continue to use the actual tip history above.
            tipTrailHistory[i] = center + radial * Math.Max(0f, radius - 30f);
        }
        previousRadius = BladeRadius;
        trailCount = Math.Min(tipHistory.Length, trailCount + added);
        if (!Returning && Main.myPlayer == Projectile.owner)
        {
            skillFlow.Update(Owner, this, beat + Math.Min(1f, beatAge / (float)beatDuration));
            EmitAirbursts();
        }
        Vector2 handAim = (Projectile.Center - Owner.MountedCenter).SafeNormalize(Vector2.UnitX * Owner.direction);
        Owner.ChangeDir(handAim.X < 0 ? -1 : 1);
        Owner.heldProj = Projectile.whoAmI;
        Owner.itemTime = Owner.itemAnimation = 2;
        Owner.itemRotation = handAim.ToRotation() + (Owner.direction < 0 ? MathHelper.Pi : 0);
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, handAim.ToRotation() - MathHelper.PiOver2);
        if (!Main.dedServ)
        {
            Lighting.AddLight(Projectile.Center, Accent.ToVector3() * 0.45f * Opacity);
            if (!Returning && age % 2 == 0)
                FranShiFuSwingEffects.Swing(Projectile.Center, tipHistory[0], blade.RotatedBy(MathHelper.PiOver2),
                    Accent, 1, age / 2, Opacity, 10, AccentSkill == FranSkill.BlueResonance);
        }
    }

    private int Layers(FranSkill skill)
    { int count = 0; foreach (FranSkill value in tactics) if (value == skill) count++; return count; }

    private void BeginBeat()
    {
        FranPlayer fran = Owner.GetModPlayer<FranPlayer>();
        beat = Main.myPlayer == Projectile.owner ? fran.ConsumeNextCombo() : (beat + 1) % 4;
        beatDuration = FranSwordTiming.ConsumeBeatDuration(Owner, beat);
        Owner.GetModPlayer<FranDiscPlayer>().AttackRecoveryUntil = Main.GameUpdateCount + (ulong)beatDuration;
        beatAge = 0;
        beatSerial++;
        ResetBeatBudget();
        skillFlow.BeginBeat(Owner, tactics, beat, SkillCycle);
        if (Main.myPlayer == Projectile.owner) Projectile.netUpdate = true;
    }

    private void ResetBeatBudget()
    {
        damageRatio = FranBalance.GetDiscDamageMultiplier();
        skillBaseDamage = Math.Max(1, baseDamage);
        Projectile.damage = Math.Max(1, (int)(baseDamage * FranBalance.GetComboDamageMultiplier(beat) * damageRatio));
        Projectile.CritChance = Owner.GetWeaponCrit(Owner.HeldItem);
        wisps = 0;
        hitTargets.Clear();
        releasedSkill = registeredEvolution = false;
        thunderHitTriggered = false;
        skillTakerHitTargets.Clear();
        blueResonanceHitTargets.Clear();
        // Keep existing per-NPC timers across beat boundaries; resetting them here
        // would allow two hits on adjacent frames when a new beat starts.
        Projectile.localNPCHitCooldown = Math.Max(1, (beatDuration + 1) / 2);
    }

    // A continuous cutter only winds up once, rather than becoming harmless every virtual stroke.
    public override bool? CanDamage() => !Returning && damageRatio > 0f && age >= 3 ? null : false;

    public override bool? CanCutTiles() => !Returning && damageRatio > 0f && age >= 3;

    public override void CutTiles()
    {
        // Pots and other cuttable tiles do not use Colliding's NPC sweep. Give them
        // the same physical blade path without changing the enemy hit detection.
        DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
        float width = 24f * FranBalance.GetCoreSizeMultiplier();
        float travel = MathF.Abs(Projectile.rotation - previousAngle) * BladeRadius +
            Vector2.Distance(previousCenter, Projectile.Center);
        int samples = Math.Clamp((int)MathF.Ceiling(travel / Math.Max(8f, width * 0.6f)), 4, 24);
        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector2 center = Vector2.Lerp(previousCenter, Projectile.Center, t);
            Vector2 blade = MathHelper.Lerp(previousAngle, Projectile.rotation, t).ToRotationVector2();
            Utils.PlotTileLine(center, center + blade * BladeRadius, width, DelegateMethods.CutTiles);
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        // Sweep the physical blade between frames; the decorative chain has no hitbox.
        float width = 24f * FranBalance.GetCoreSizeMultiplier();
        float travel = MathF.Abs(Projectile.rotation - previousAngle) * BladeRadius +
            Vector2.Distance(previousCenter, Projectile.Center);
        int samples = Math.Clamp((int)MathF.Ceiling(travel / Math.Max(8f, width * 0.6f)), 4, 24);
        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector2 center = Vector2.Lerp(previousCenter, Projectile.Center, t);
            Vector2 blade = MathHelper.Lerp(previousAngle, Projectile.rotation, t).ToRotationVector2();
            float collision = 0f;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), center,
                center + blade * BladeRadius, width, ref collision)) return true;
        }
        return false;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        // The mark boosts the sword itself, so keep only 80% of that bonus on
        // the disc while leaving the unmarked cutter's contact damage intact.
        float mark = target.GetGlobalNPC<FranImprintTargetGlobalNPC>().SkillTakerDamageMultiplier;
        if (mark > 1f)
            modifiers.FinalDamage *= (1f + 0.8f * (mark - 1f)) / mark;
        if (FranBalance.IsWormBossSegment(target))
            modifiers.FinalDamage *= FranBalance.GetFloat(nameof(FranBalance.SwingWormBossDamageMultiplier), FranBalance.SwingWormBossDamageMultiplier);
        bool bossFight = false;
        if (!FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh))
        {
            foreach (NPC npc in Main.ActiveNPCs)
                if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type]) { bossFight = true; break; }
            int targetCount = hitTargets.Count + (hitTargets.Contains(target.whoAmI) ? 0 : 1);
            modifiers.FinalDamage *= FranBalance.GetSwingCrowdDamageMultiplier(targetCount, bossFight, false);
        }
        modifiers.FinalDamage *= Owner.GetModPlayer<FranEvolutionPlayer>().DirectSwordDamageMultiplier(beat);
        modifiers.FlatBonusDamage += Owner.GetModPlayer<FranPlayer>().GetStolenContactDamage(
            Owner.GetWeaponDamage(Owner.HeldItem)) * damageRatio * 0.8f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        hitTargets.Add(target.whoAmI);
        if (FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh) &&
            Layers(FranSkill.TelekineticCatapult) > 0 &&
            Main.myPlayer == Projectile.owner)
            SpawnTelekineticHitLaser(target);
        if (!registeredEvolution)
        { registeredEvolution = true; Owner.GetModPlayer<FranEvolutionPlayer>().RegisterDirectSwordHit(beat); }
        FranEvolutionBossSystem.RegisterAnalysisHit(target, Owner, beat, Projectile.identity);
        Owner.GetModPlayer<FranEvolutionPlayer>().ApplyResonantPull(target, tipHistory[0]);
        Vector2 incoming = Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
        Vector2 contact = ContactPoint(target);
        if (age - lastImpactAge >= 4)
        {
            lastImpactAge = age;
            FranVanillaDustEffects.Impact(contact, incoming, AccentSkill, Accent, 3, 0.62f);
        }
        int thunderLayers = Layers(FranSkill.ThunderChain);
        if (thunderLayers > 0 && !thunderHitTriggered && Main.myPlayer == Projectile.owner)
        {
            // One actual cutter hit unlocks this beat's full left-click circuit stage.
            // Further contacts during the beat may not duplicate the lightning budget.
            thunderHitTriggered = true;
            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(
                Projectile.rotation.ToRotationVector2());
            ShiFuImprintCombat.TriggerSwing(Owner, Projectile, beat, FranSkill.ThunderChain,
                thunderLayers, direction, BladeRadius, target);
        }
        for (int i = 0; i < FranSkillInfo.Count; i++)
        {
            FranSkill skill = (FranSkill)i;
            int layers = Layers(skill);
            if (layers <= 0) continue;
            bool firstSkillHit = skill switch
            {
                FranSkill.SkillTaker => skillTakerHitTargets.Add(target.whoAmI),
                FranSkill.BlueResonance => blueResonanceHitTargets.Add(target.whoAmI),
                _ => true
            };
            ShiFuImprintCombat.OnSwordHit(Owner, Projectile, target, beat, skill, layers,
                incoming, damageDone, firstSkillHit);
        }
    }

    private void SpawnTelekineticHitLaser(NPC target)
    {
        // 先继承本次切割的时期倍率和四段倍率，再乘激光自身倍率。
        float laserMultiplier = Math.Max(0f, FranBalance.GetFloat(
            nameof(FranBalance.TelekineticDiscHitLaserDamageMultiplier),
            FranBalance.TelekineticDiscHitLaserDamageMultiplier));
        if (laserMultiplier <= 0f)
            return;
        int totalDamage = Math.Max(1, (int)(Projectile.damage * laserMultiplier));
        // Elysian Arrow's paired rain: split the former single laser budget between both strikes.
        Telekinetic_Flash.SpawnElysianRain(Projectile.GetSource_OnHit(target), Projectile,
            target, Math.Max(1, (int)MathF.Round(totalDamage * 0.5f)));
    }

    internal Vector2 ContactPoint(NPC target)
    {
        // Large enemies may have their center well outside the rotating blade.
        Vector2 radial = (target.Center - Projectile.Center).SafeNormalize(Projectile.rotation.ToRotationVector2());
        Vector2 edge = Projectile.Center + radial * Math.Min(BladeRadius, Vector2.Distance(target.Center, Projectile.Center));
        return Vector2.Clamp(edge, target.Hitbox.TopLeft(), target.Hitbox.BottomRight());
    }

    private void EmitAirbursts()
    {
        int layers = Layers(FranSkill.PressurizedQuickdraw);
        if (layers <= 0 || Main.myPlayer != Projectile.owner) return;
        bool full = Owner.GetModPlayer<FranPlayer>().IsFullTacticLoadout(FranSkill.PressurizedQuickdraw);
        if (!full) return; // Partial loadouts use the continuous cycle scheduler.
        // The full left-click loadout is itself a continuous whirlwind, with one burst
        // per configured interval. Match that cadence, not the partial-loadout four-beat counts.
        int leftInterval = Math.Max(1, FranBalance.GetInt(nameof(FranBalance.QuickdrawFullDiscAirburstInterval),
            FranBalance.QuickdrawFullDiscAirburstInterval));
        int interval = Math.Max(1, (int)MathF.Round(leftInterval / 0.8f));
        if (++fullAirburstTimer < interval) return;
        fullAirburstTimer = 0;
        int totalDamage = Math.Max(1, (int)(baseDamage * FranTacticOutputBudget.DiscAirburstMultiplier(Owner)));
        EmitOrbitingAirbursts(totalDamage, 0.7f, fullLoadout: true);
    }

    internal void EmitOrbitingAirbursts(int totalDamage, float scale, bool fullLoadout)
    {
        int damage = Math.Max(1, totalDamage / 2);
        for (int side = 0; side < 2; side++)
        {
            float angle = Projectile.rotation * 2f + side * MathHelper.Pi;
            Vector2 direction = angle.ToRotationVector2();
            float radius = BladeRadius * (0.58f + 0.15f *
                MathF.Sin((float)Main.GameUpdateCount * 0.11f + side * MathHelper.Pi));
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + direction * radius,
                direction, ModContent.ProjectileType<FranCompressionAirburst>(), damage, Projectile.knockBack * 0.35f,
                Owner.whoAmI, FranCompressionAirburst.EncodeDelay(fullLoadout ? 4f : 3f,
                    fullLoadout, fullDiscVisual: fullLoadout),
                FranCompressionAirburst.EncodeOrbitScale(scale),
                FranCompressionAirburst.EncodeOrbit(Projectile.whoAmI, side));
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        FranDiscVisuals.DrawConnection(Owner.MountedCenter, Projectile.Center, Accent, Opacity, Projectile.identity);
        FranDiscVisuals.DrawDisc(Projectile.Center, BladeRadius, Projectile.rotation, Accent, Opacity);
        Texture2D sword = FranShiFuVisuals.Texture;
        Texture2D ghost = FranShiFuVisuals.GhostTexture;
        Texture2D glow = FranShiFuVisuals.GlowTexture;
        Vector2 position = Projectile.Center - Projectile.rotation.ToRotationVector2() * FranShiFuVisuals.GripInset;
        Vector2 drawPosition = position - Main.screenPosition;
        float rotation = Projectile.rotation + MathHelper.PiOver4;
        float swordScale = 1.05f * FranShiFuVisuals.TextureScale;
        float ghostScale = 1.05f * FranShiFuVisuals.TextureScaleFor(ghost);
        float glowScale = 1.05f * FranShiFuVisuals.TextureScaleFor(glow);
        // One complete breath per 60 world ticks. Use the same phase for the body
        // silhouette and the real glow map, so the light belongs to the sword.
        float pulse = 0.5f - 0.5f * MathF.Cos(age * MathHelper.TwoPi / 60f);
        Color outline = AccentSkill == FranSkill.None ? new Color(145, 220, 255) : Accent;
        // Match the left swing's 20-copy ghost aura at 80% of its 0.15 strength.
        Color ghostTint = new(outline.R, outline.G, outline.B, 0);
        float ghostRadius = 5.5f * Opacity;
        for (int i = 0; i < 20; i++)
        {
            Vector2 offset = (MathHelper.TwoPi * i / 20f).ToRotationVector2() * ghostRadius;
            Main.EntitySpriteDraw(ghost, drawPosition + offset, null, ghostTint * (0.12f * Opacity),
                rotation, new Vector2(0, ghost.Height), ghostScale, SpriteEffects.None);
        }
        float outlineRadius = (1.25f + 1.3f * pulse) * FranBalance.GetCoreSizeMultiplier();
        float outlineOpacity = (0.12f + 0.08f * pulse) * Opacity;
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * outlineRadius;
            Main.EntitySpriteDraw(sword, drawPosition + offset, null, outline * outlineOpacity,
                rotation, new Vector2(0, sword.Height), swordScale, SpriteEffects.None);
        }
        Main.EntitySpriteDraw(sword, position - Main.screenPosition, null, lightColor * Opacity,
            rotation, new Vector2(0, sword.Height), swordScale, SpriteEffects.None);
        FranVfx.BeginAdditive();
        try
        {
            float haloOpacity = (0.08f + 0.09f * pulse) * Opacity;
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = (MathHelper.PiOver2 * i).ToRotationVector2() * outlineRadius;
                Main.EntitySpriteDraw(glow, drawPosition + offset, null, outline * haloOpacity,
                    rotation, new Vector2(0, glow.Height), glowScale, SpriteEffects.None);
            }
            Main.EntitySpriteDraw(glow, drawPosition, null, outline * ((0.25f + 0.55f * pulse) * Opacity),
                rotation, new Vector2(0, glow.Height), glowScale, SpriteEffects.None);
        }
        finally { FranVfx.EndAdditive(); }
        FranDiscVisuals.DrawPhantomSpin(Projectile.Center, Projectile.rotation, Accent, Opacity, 1f);
        int arcSamples = 0;
        while (arcSamples < trailCount && Projectile.rotation - tipAngles[arcSamples] <= 4.5f) arcSamples++;
        FranDiscVisuals.DrawTipTrail(tipTrailHistory, Math.Min(trailCount, arcSamples), Accent, Opacity, FranBalance.GetCoreSizeMultiplier());
        return false;
    }
}
