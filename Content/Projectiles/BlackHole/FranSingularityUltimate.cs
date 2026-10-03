using System;
using System.IO;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles.BlackHole;

public sealed class FranSingularityUltimate : ModProjectile
{
    internal enum HolePhase : byte { Charging, Flight, Collapse, Aftermath }
    internal HolePhase Phase { get; private set; }
    internal int PhaseAge { get; private set; }
    private Vector2 aim;
    private Vector2 swordDirection = -Vector2.UnitY;
    private bool initialized;
    private bool detonateRequested;
    private bool previousRight;
    private bool burstShown;
    private bool launchedSound;
    private int inputTimer;
    private int panelDamage;
    private int armorPenetration;
    private bool Authority => Main.netMode != NetmodeID.MultiplayerClient;
    private Player Owner => Main.player[Projectile.owner];
    private int Slot => (int)Projectile.ai[2];
    public bool WeaponLocked => Phase == HolePhase.Charging || Phase == HolePhase.Flight &&
        !FranSingularityRules.CanDetonate(PhaseAge);
    internal bool BodyVisible => Phase != HolePhase.Aftermath;
    internal float VisualStrength => Phase switch
    {
        HolePhase.Charging => MathHelper.SmoothStep(0f, 0.7f, PhaseAge / (float)FranSingularityRules.ChargeFrames),
        HolePhase.Flight => MathHelper.SmoothStep(0.7f, 1f, Math.Min(1f, PhaseAge / 120f)),
        HolePhase.Collapse => 1f,
        _ => MathHelper.Clamp(1f - PhaseAge / 24f, 0f, 1f)
    };

    public override string Texture => "Fran/Assets/Textures/BlackHole/TransparentPixel";
    public static FranSingularityUltimate Find(int owner)
    {
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.owner == owner && projectile.ModProjectile is FranSingularityUltimate hole)
                return hole;
        return null;
    }
    public static FranSingularityUltimate FindAny()
    {
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.ModProjectile is FranSingularityUltimate hole)
                return hole;
        return null;
    }

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 8;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.timeLeft = 2;
        Projectile.scale = 0f;
    }
    // Hits are resolved once per real tick on the authority, including after the owner dies.
    // This avoids client/server double hits, shared NPC immunity, and extraUpdates changing DPS.
    public override bool? CanDamage() => false;
    public override bool? CanCutTiles() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((byte)Phase);
        writer.Write(PhaseAge);
        writer.Write(aim.X); writer.Write(aim.Y);
        writer.Write(swordDirection.X); writer.Write(swordDirection.Y);
        writer.Write(panelDamage);
        writer.Write(armorPenetration);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        HolePhase phase = (HolePhase)reader.ReadByte();
        int age = reader.ReadInt32();
        Vector2 target = new(reader.ReadSingle(), reader.ReadSingle());
        Vector2 direction = new(reader.ReadSingle(), reader.ReadSingle());
        int panel = reader.ReadInt32();
        int penetration = reader.ReadInt32();
        // Only the server advances the clock/phase or sets the damage snapshot.
        if (Authority)
            return;
        Phase = phase; PhaseAge = age; aim = target; swordDirection = direction;
        panelDamage = panel; armorPenetration = penetration;
        if (!initialized && !Main.dedServ)
            previousRight = Mouse.GetState().RightButton == ButtonState.Pressed;
        initialized = true;
    }

    internal void ReceiveInput(Vector2 target, bool detonate)
    {
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y) || Owner.dead)
            return;
        aim = SustainedBlackHole.ClampDestination(Owner, target);
        if (Phase == HolePhase.Flight && FranSingularityRules.CanDetonate(PhaseAge) && detonate)
            detonateRequested = true;
    }

    public override void AI()
    {
        Projectile.timeLeft = 2;
        if (!Main.player.IndexInRange(Projectile.owner) || !Owner.active)
        {
            if (Authority) Projectile.Kill();
            return;
        }
        if (!initialized)
        {
            if (!Authority) return;
            initialized = true;
            panelDamage = Math.Max(1, Projectile.damage);
            armorPenetration = Owner.GetWeaponArmorPenetration(Owner.HeldItem);
            aim = SustainedBlackHole.ClampDestination(Owner, new Vector2(Projectile.ai[0], Projectile.ai[1]));
            swordDirection = new Vector2(Owner.direction * 0.12f, -Owner.gravDir).SafeNormalize(-Vector2.UnitY);
            if (!Main.dedServ)
                previousRight = Mouse.GetState().RightButton == ButtonState.Pressed;
        }

        ReadLocalInput();
        if (Authority)
            TickAuthoritative();
        else
            TickClientPresentation();
        if (!Projectile.active) return;

        UpdateScaleAndPose();
        UpdatePresentation();
        if (Authority && PhaseAge % 6 == 0)
            Projectile.netUpdate = true;
    }

    private void ReadLocalInput()
    {
        if (Main.dedServ || Projectile.owner != Main.myPlayer) return;
        bool right = Mouse.GetState().RightButton == ButtonState.Pressed;
        bool request = right && !previousRight && Main.hasFocus && !Main.playerInventory &&
            !Main.ingameOptionsWindow && Phase == HolePhase.Flight && FranSingularityRules.CanDetonate(PhaseAge);
        previousRight = right;
        if (Owner.dead || !Main.hasFocus) return;
        if (Authority)
            ReceiveInput(Main.MouseWorld, request);
        else if (++inputTimer >= 3 || request)
        {
            inputTimer = 0;
            FranModMain.SendSingularityInput(Projectile.identity, Main.MouseWorld, request);
        }
    }

    private void TickClientPresentation()
    {
        // Predict only motion and appearance, never phase transitions or hits.
        PhaseAge++;
        if (Phase == HolePhase.Charging && !Owner.dead)
        {
            Vector2 desired = (aim - Owner.MountedCenter).SafeNormalize(Vector2.UnitX * Owner.direction);
            swordDirection = Vector2.Lerp(swordDirection, desired, 0.12f).SafeNormalize(desired);
            Projectile.Center = SwordTip();
        }
        else if (Phase == HolePhase.Flight)
            MoveInFlight();
    }

    private void MoveInFlight()
    {
        if (Owner.dead) { Projectile.velocity = Vector2.Zero; return; }
        Vector2 offset = aim - Projectile.Center;
        Vector2 desiredVelocity = offset.SafeNormalize(Vector2.Zero) * Math.Min(9f, offset.Length() * 0.045f);
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.055f);
        Projectile.Center += Projectile.velocity;
    }

    private void TickAuthoritative()
    {
        switch (Phase)
        {
            case HolePhase.Charging:
                if (Owner.dead)
                {
                    Projectile.Kill(); // No launched hole exists yet.
                    return;
                }
                PhaseAge++;
                Vector2 desired = (aim - Owner.MountedCenter).SafeNormalize(Vector2.UnitX * Owner.direction);
                swordDirection = Vector2.Lerp(swordDirection, desired, 0.12f).SafeNormalize(desired);
                Projectile.Center = SwordTip();
                if (PhaseAge >= FranSingularityRules.ChargeFrames)
                {
                    Projectile.velocity = swordDirection * 18f;
                    EnterPhase(HolePhase.Flight);
                }
                break;
            case HolePhase.Flight:
                // Check before incrementing: 300 complete damage ticks are compulsory,
                // and the automatic collapse follows exactly 900 damaging flight ticks.
                bool changed = Owner.selectedItem != Slot || Owner.HeldItem.type != ModContent.ItemType<ShiFu>();
                if (FranSingularityRules.ShouldDetonate(PhaseAge, detonateRequested, Owner.dead, changed))
                {
                    EnterPhase(HolePhase.Collapse);
                    break;
                }
                MoveInFlight();
                PullEnemies();
                DamageEnemies(FranSingularityRules.DamageRadius, FranSingularityRules.TickDamage(panelDamage, PhaseAge));
                PhaseAge++;
                break;
            case HolePhase.Collapse:
                Projectile.velocity *= 0.65f;
                if (++PhaseAge >= FranSingularityRules.CollapseFrames)
                {
                    DamageEnemies(FranSingularityRules.ExplosionRadius, FranSingularityRules.ExplosionDamage(panelDamage));
                    EnterPhase(HolePhase.Aftermath);
                }
                break;
            case HolePhase.Aftermath:
                if (++PhaseAge >= FranSingularityRules.AftermathFrames)
                    Projectile.Kill();
                break;
        }
    }

    private void EnterPhase(HolePhase phase)
    {
        Phase = phase;
        PhaseAge = 0;
        Projectile.netUpdate = true;
        if (phase == HolePhase.Collapse)
            ReleasePose();
    }

    internal void PullEnemies()
    {
        if (!Authority) return;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy() || npc.boss || npc.knockBackResist <= 0f ||
                npc.realLife >= 0 && Main.npc.IndexInRange(npc.realLife) && Main.npc[npc.realLife].boss)
                continue;
            Vector2 offset = Projectile.Center - npc.Center;
            float distance = offset.Length();
            if (distance > FranSingularityRules.PullRadius || distance < 2f) continue;
            float resistance = MathHelper.Clamp(npc.knockBackResist, 0f, 1f);
            Vector2 desired = offset / distance * Math.Min(12f, distance * 0.14f);
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.16f * resistance);
            if (PhaseAge % 6 == 0) npc.netUpdate = true;
        }
    }

    private void DamageEnemies(float radius, int damage)
    {
        if (!Authority || damage <= 0) return;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            Vector2 nearest = Vector2.Clamp(Projectile.Center, npc.Hitbox.TopLeft(), npc.Hitbox.BottomRight());
            if (!npc.CanBeChasedBy() || Vector2.DistanceSquared(nearest, Projectile.Center) > radius * radius ||
                CombinedHooks.CanHitNPCWithProj(Projectile, npc) == false)
                continue;
            int direction = npc.Center.X >= Projectile.Center.X ? 1 : -1;
            NPC.HitModifiers modifiers = npc.GetIncomingStrikeModifiers(DamageClass.Melee, direction);
            modifiers.ArmorPenetration += armorPenetration;
            CombinedHooks.ModifyHitNPCWithProj(Projectile, npc, ref modifiers);
            NPC.HitInfo hit = modifiers.ToHitInfo(damage, false, 0f, false);
            npc.lastInteraction = Projectile.owner;
            npc.playerInteraction[Projectile.owner] = true;
            if (npc.realLife >= 0 && Main.npc.IndexInRange(npc.realLife))
            {
                Main.npc[npc.realLife].lastInteraction = Projectile.owner;
                Main.npc[npc.realLife].playerInteraction[Projectile.owner] = true;
            }
            int dealt = npc.StrikeNPC(hit, noPlayerInteraction: true);
            if (Main.netMode == NetmodeID.Server) NetMessage.SendStrikeNPC(npc, hit);
            CombinedHooks.OnHitNPCWithProj(Projectile, npc, hit, dealt);
            Owner.addDPS(dealt);
        }
    }

    internal void LockHeldWeapon(Player owner)
    {
        if (!WeaponLocked || owner.dead || Slot < 0 || Slot >= owner.inventory.Length ||
            owner.inventory[Slot].type != ModContent.ItemType<ShiFu>()) return;
        owner.selectedItem = Slot;
        owner.changeItem = -1;
        owner.itemAnimation = Math.Max(owner.itemAnimation, 2);
        owner.itemTime = Math.Max(owner.itemTime, 2);
    }

    private Vector2 SwordGrip() => Owner.MountedCenter + new Vector2(0f, -10f * Owner.gravDir) + swordDirection * 12f;
    internal Vector2 SwordTip() => SwordGrip() + swordDirection *
        (FranShiFuVisuals.BladeLengthFromGripPixels * FranShiFuVisuals.TextureScale * 1.05f);

    private void UpdateScaleAndPose()
    {
        Projectile.scale = Phase switch
        {
            HolePhase.Charging => MathHelper.Lerp(0.025f, FranSingularityRules.MaximumScale,
                MathHelper.SmoothStep(0f, 1f, PhaseAge / (float)FranSingularityRules.ChargeFrames)),
            HolePhase.Flight => FranSingularityRules.MaximumScale,
            HolePhase.Collapse => FranSingularityRules.MaximumScale * MathF.Pow(
                MathHelper.Clamp(1f - PhaseAge / (float)FranSingularityRules.CollapseFrames, 0f, 1f), 1.8f),
            _ => 0f
        };
        Projectile.rotation = MathHelper.Lerp(Projectile.rotation, Projectile.velocity.X * 0.008f, 0.08f);
        if (Owner.dead || Phase >= HolePhase.Collapse) return;
        LockHeldWeapon(Owner);
        if (Phase == HolePhase.Flight && PhaseAge > 0)
            swordDirection = (Projectile.Center - Owner.MountedCenter).SafeNormalize(swordDirection);
        Owner.ChangeDir(swordDirection.X < 0f ? -1 : 1);
        Owner.heldProj = Projectile.whoAmI;
        Owner.itemRotation = swordDirection.ToRotation() + (Owner.direction < 0 ? MathHelper.Pi : 0f);
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, swordDirection.ToRotation() - MathHelper.PiOver2);
        // Let itemAnimation reach zero after the mandatory period, otherwise vanilla rejects
        // hotbar switches even though our own lock has been released.
    }

    private void UpdatePresentation()
    {
        if (Main.dedServ) return;
        if (Phase >= HolePhase.Flight && !launchedSound)
        {
            launchedSound = true;
            SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.65f, Pitch = -0.55f }, Projectile.Center);
        }
        if (Phase == HolePhase.Aftermath && !burstShown)
        {
            burstShown = true;
            FranSingularityBurstVisual.Spawn(Projectile);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.95f, Pitch = -0.75f }, Projectile.Center);
            Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 13f, 2200f);
        }
        if (Phase < HolePhase.Aftermath && PhaseAge % 5 == 0)
            Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center,
                VisualStrength * (Phase == HolePhase.Collapse ? 9f : 4.5f), 1600f);
        if (Phase >= HolePhase.Collapse) return;
        float strength = VisualStrength;
        if (Main.rand.NextFloat() < strength)
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = Projectile.scale * 420f;
            Vector2 direction = angle.ToRotationVector2();
            Dust dust = Dust.NewDustPerfect(Projectile.Center + direction * radius, DustID.Shadowflame,
                -direction * (3f + strength * 7f), 160, new Color(24, 5, 35), 0.8f + strength);
            dust.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, new Vector3(0.28f, 0.075f, 0.035f) * strength);
    }

    private void ReleasePose()
    {
        if (Owner.heldProj == Projectile.whoAmI) Owner.heldProj = -1;
        if (Owner.selectedItem == Slot)
        {
            Owner.itemTime = 0;
            Owner.itemAnimation = 0;
        }
    }
    public override void OnKill(int timeLeft) { if (Main.player.IndexInRange(Projectile.owner)) ReleasePose(); }

    public override bool PreDraw(ref Color lightColor)
    {
        if (!initialized || Owner.dead || Phase >= HolePhase.Collapse) return false;
        Texture2D sword = FranShiFuVisuals.Texture;
        Texture2D glow = FranShiFuVisuals.GlowTexture;
        bool flipped = swordDirection.X < 0f;
        SpriteEffects effects = flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        float rotation = swordDirection.ToRotation() + MathHelper.PiOver4 + (flipped ? MathHelper.PiOver2 : 0f);
        Vector2 position = SwordGrip() - swordDirection * FranShiFuVisuals.GripInset - Main.screenPosition;
        Main.EntitySpriteDraw(sword, position, null, Color.Lerp(lightColor, Color.White, 0.25f), rotation,
            new Vector2(flipped ? sword.Width : 0, sword.Height), 1.05f * FranShiFuVisuals.TextureScale, effects);
        Main.EntitySpriteDraw(glow, position, null, Color.White * (0.3f + VisualStrength * 0.6f), rotation,
            new Vector2(flipped ? glow.Width : 0, glow.Height), 1.05f * FranShiFuVisuals.TextureScaleFor(glow), effects);
        return false;
    }
}
