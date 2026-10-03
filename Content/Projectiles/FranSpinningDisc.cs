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
    private readonly Vector2[] tipHistory = new Vector2[24];
    private readonly HashSet<int> hitTargets = new();
    private int baseDamage;
    private int skillBaseDamage;
    private int beatAge;
    private int beatDuration;
    private int beat;
    private int wisps;
    private int age;
    private int trailCount;
    private int airburstStep;
    private int fullAirburstTimer;
    private bool started;
    private bool releasedSkill;
    private bool registeredEvolution;
    private float previousAngle;
    private float damageRatio;
    private Vector2 previousCenter;
    private Vector2 cursor;
    private int returnAge;
    private int beatSerial;
    private float rotationSoundTravel;
    private int resumeInputGrace;
    internal void NotifyPaused() => resumeInputGrace = 2;
    private bool Returning => Projectile.ai[0] > 0f;
    private float Opacity => Math.Min(1f, age / 10f) * (Returning ? Math.Max(0f, 1f - returnAge / 24f) : 1f);
    private float BladeRadius => FranShiFuVisuals.BladeLengthFromGripPixels * 1.05f * FranShiFuVisuals.TextureScale;
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
            if (!Main.dedServ) SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.25f, Volume = 0.65f }, Projectile.Center);
        }
        age++;
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
            beatAge++;
            if (beatAge >= beatDuration && Main.myPlayer == Projectile.owner) BeginBeat();
            float releaseTime = beat switch { 0 => 0.42f, 1 => 0.49f, 2 => 0.45f, _ => 0.46f };
            if (!releasedSkill && beatAge >= beatDuration * releaseTime)
            {
                releasedSkill = true;
                for (int i = 0; i < FranSkillInfo.Count; i++)
                {
                    FranSkill skill = (FranSkill)i;
                    int layers = Layers(skill);
                    if (layers > 0) ShiFuImprintCombat.TriggerSwing(Owner, Projectile, beat, skill, layers,
                        (cursor - Owner.MountedCenter).SafeNormalize(Vector2.UnitX * Owner.direction), BladeRadius);
                }
            }
            EmitAirbursts();
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
        for (int i = tipHistory.Length - 1; i > 0; i--) tipHistory[i] = tipHistory[i - 1];
        tipHistory[0] = Projectile.Center + blade * BladeRadius;
        trailCount = Math.Min(tipHistory.Length, trailCount + 1);
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
        if (Main.myPlayer == Projectile.owner) Projectile.netUpdate = true;
    }

    private void ResetBeatBudget()
    {
        damageRatio = FranBalance.GetDiscDamageMultiplier();
        skillBaseDamage = Math.Max(1, (int)(baseDamage * damageRatio));
        Projectile.damage = Math.Max(1, (int)(baseDamage * FranBalance.GetComboDamageMultiplier(beat) * damageRatio));
        wisps = 0;
        airburstStep = 0;
        hitTargets.Clear();
        releasedSkill = registeredEvolution = false;
        // Keep existing per-NPC timers across beat boundaries; resetting them here
        // would allow two hits on adjacent frames when a new beat starts.
        Projectile.localNPCHitCooldown = Math.Max(1, (beatDuration + 1) / 2);
    }

    public override bool? CanDamage() => !Returning && damageRatio > 0f && beatAge < beatDuration &&
        beatAge >= beatDuration * 0.16f ? null : false;

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
        if (FranBalance.IsWormBossSegment(target))
            modifiers.FinalDamage *= FranBalance.GetFloat(nameof(FranBalance.SwingWormBossDamageMultiplier), FranBalance.SwingWormBossDamageMultiplier);
        bool bossFight = false;
        if (!Main.hardMode)
        {
            foreach (NPC npc in Main.ActiveNPCs)
                if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type]) { bossFight = true; break; }
            int targetCount = hitTargets.Count + (hitTargets.Contains(target.whoAmI) ? 0 : 1);
            modifiers.FinalDamage *= FranBalance.GetSwingCrowdDamageMultiplier(targetCount, bossFight, false);
        }
        modifiers.FinalDamage *= Owner.GetModPlayer<FranEvolutionPlayer>().DirectSwordDamageMultiplier(beat);
        modifiers.FlatBonusDamage += Owner.GetModPlayer<FranPlayer>().GetStolenContactDamage(
            Owner.GetWeaponDamage(Owner.HeldItem)) * damageRatio;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        hitTargets.Add(target.whoAmI);
        if (!registeredEvolution)
        { registeredEvolution = true; Owner.GetModPlayer<FranEvolutionPlayer>().RegisterDirectSwordHit(beat); }
        FranEvolutionBossSystem.RegisterAnalysisHit(target, Owner, beat, Projectile.identity);
        Owner.GetModPlayer<FranEvolutionPlayer>().ApplyResonantPull(target, tipHistory[0]);
        Vector2 incoming = Projectile.rotation.ToRotationVector2();
        FranParticleEffects.MeleeImpact(target.Center, incoming, Accent, beat);
        FranVanillaDustEffects.Impact(target.Center, incoming, AccentSkill, Accent, beat == 2 ? 6 : 5, beat == 2 ? 1.12f : 0.94f);
        if (beat is 2 or 3)
            FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_OnHit(target), target.Center, incoming,
                Owner.whoAmI, FranSpectatorVfx.ForSkill(AccentSkill), beat == 2 ? 5 : 3, beat == 2 ? 1.08f : 0.88f);
        if (Layers(FranSkill.ThunderChain) > 0 || Layers(FranSkill.ThorsHammer) > 0)
            FranLightingBoltsSystem.SpawnThunderImpact(target.Center, new Color(155, 125, 255), 0.65f);
        for (int i = 0; i < FranSkillInfo.Count; i++)
        {
            FranSkill skill = (FranSkill)i;
            if (Layers(skill) > 0) ShiFuImprintCombat.OnSwordHit(Owner, Projectile, target, beat, skill, Layers(skill), incoming, damageDone);
        }
    }

    private void EmitAirbursts()
    {
        int layers = Layers(FranSkill.PressurizedQuickdraw);
        if (layers <= 0 || Main.myPlayer != Projectile.owner) return;
        bool full = Owner.GetModPlayer<FranPlayer>().IsFullTacticLoadout(FranSkill.PressurizedQuickdraw);
        if (full)
        {
            // The full left-click loadout is itself a continuous whirlwind, with one burst
            // per configured interval. Match that cadence, not the partial-loadout four-beat counts.
            int interval = Math.Max(1, FranBalance.GetInt(nameof(FranBalance.QuickdrawFullDiscAirburstInterval),
                FranBalance.QuickdrawFullDiscAirburstInterval));
            if (++fullAirburstTimer < interval) return;
            fullAirburstTimer = 0;
            Vector2 direction = Projectile.rotation.ToRotationVector2();
            int damage = Math.Max(1, (int)(baseDamage * damageRatio * FranTacticOutputBudget.DiscAirburstMultiplier(Owner)));
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + direction * BladeRadius * 0.72f,
                direction, ModContent.ProjectileType<FranCompressionAirburst>(), damage, Projectile.knockBack * 0.35f,
                Owner.whoAmI, FranCompressionAirburst.EncodeDelay(4f, fullLoadout: true, fullDiscVisual: true),
                0.7f, Projectile.rotation);
            return;
        }
        int count = Math.Max(1, FranBalance.GetQuickdrawAirburstCount(beat));
        if (!full) count = Math.Max(1, (int)MathF.Round(count * FranBalance.GetFloat(
            nameof(FranBalance.QuickdrawNonFullEmissionMultiplier), FranBalance.QuickdrawNonFullEmissionMultiplier)));
        while (airburstStep < count && beatAge >= beatDuration * (0.18f + 0.6f * airburstStep / count))
        {
            Vector2 blade = Projectile.rotation.ToRotationVector2();
            float scale = FranBalance.GetQuickdrawAirburstDamageMultiplier(beat);
            if (!full) scale *= FranBalance.GetFloat(nameof(FranBalance.QuickdrawNonFullDamageMultiplier), FranBalance.QuickdrawNonFullDamageMultiplier);
            scale *= (1f + FranBalance.GetFloat(nameof(FranBalance.QuickdrawExtraLayerDamageMultiplier),
                FranBalance.QuickdrawExtraLayerDamageMultiplier) * (layers - 1)) * FranTacticOutputBudget.Scale(FranSkill.PressurizedQuickdraw);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + blade * BladeRadius * 0.72f,
                blade, ModContent.ProjectileType<FranCompressionAirburst>(), Math.Max(1, (int)(Projectile.damage * scale)),
                Projectile.knockBack * 0.35f, Owner.whoAmI, FranCompressionAirburst.EncodeDelay(3f, full), 0.8f, Projectile.rotation);
            airburstStep++;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        FranDiscVisuals.DrawConnection(Owner.MountedCenter, Projectile.Center, Accent, Opacity, Projectile.identity);
        FranDiscVisuals.DrawDisc(Projectile.Center, BladeRadius, Projectile.rotation, Accent, Opacity);
        Texture2D sword = FranShiFuVisuals.Texture;
        Vector2 position = Projectile.Center - Projectile.rotation.ToRotationVector2() * FranShiFuVisuals.GripInset;
        Main.EntitySpriteDraw(sword, position - Main.screenPosition, null, lightColor * Opacity,
            Projectile.rotation + MathHelper.PiOver4, new Vector2(0, sword.Height),
            1.05f * FranShiFuVisuals.TextureScale, SpriteEffects.None);
        Texture2D glow = FranShiFuVisuals.GlowTexture;
        Main.EntitySpriteDraw(glow, position - Main.screenPosition, null, Accent * (0.65f * Opacity),
            Projectile.rotation + MathHelper.PiOver4, new Vector2(0, glow.Height),
            1.05f * FranShiFuVisuals.TextureScaleFor(glow), SpriteEffects.None);
        int arcSamples = Math.Clamp((int)(4.5f / Math.Max(0.08f, Projectile.rotation - previousAngle)), 4, tipHistory.Length);
        FranDiscVisuals.DrawTipTrail(tipHistory, Math.Min(trailCount, arcSamples), Accent, Opacity, FranBalance.GetCoreSizeMultiplier());
        return false;
    }
}
