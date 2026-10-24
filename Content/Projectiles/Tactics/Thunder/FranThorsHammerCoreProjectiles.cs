using System;
using System.IO;
using FranMod.Content.Companions;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using ReLogic.Utilities;

namespace FranMod.Content.Projectiles;


public sealed class ThorsHammerMarker : ModProjectile
{
    private const int PackedTargetStride = 256;
    internal const int WindupTicks = 50;
    internal static float HammerRotation(float age) => age * 0.035f + age * age * 0.0035f;
    private SlotId chargeSoundSlot;
    private bool chargeSoundStarted;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 120; Projectile.height = 120; Projectile.timeLeft = 75;
        Projectile.tileCollide = false; Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void SendExtraAI(BinaryWriter writer) => writer.Write((byte)Projectile.timeLeft);
    public override void ReceiveExtraAI(BinaryReader reader) => Projectile.timeLeft = Math.Clamp((int)reader.ReadByte(), 1, 75);
    public override void AI()
    {
        // The synced spawn point is the seal's fixed position above the caster, not on an NPC.
        float age = 75f - Projectile.timeLeft;
        UpdateChargeSound(age);
        float focus = FranMotion.EaseInCubic(FranMotion.Segment(age, 10f, 49f));
        const float visualIntensity = 1f;
        Lighting.AddLight(Projectile.Center, 0.12f * visualIntensity, 0.22f * visualIntensity,
            0.65f * visualIntensity);
        if (!Main.dedServ && Projectile.timeLeft % (focus > 0.72f ? 1 : 2) == 0)
        {
            const int sparkPasses = 1;
            for (int i = 0; i < sparkPasses; i++)
                FranParticleEffects.InwardSpark(Projectile.Center, new Color(115, 185, 255),
                    MathHelper.Lerp(62f, 30f, focus) * visualIntensity,
                    MathHelper.Lerp(108f, 52f, focus) * visualIntensity,
                    MathHelper.Lerp(2.5f, 6f, focus) * visualIntensity,
                    (0.8f + focus * 0.35f) * visualIntensity);
        }
        if (Projectile.timeLeft % (focus > 0.72f ? 2 : 4) == 0 && !Main.dedServ)
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = MathHelper.Lerp(92f, 42f, focus);
            Vector2 spawn = Projectile.Center + angle.ToRotationVector2() * radius;
            Dust dust = Dust.NewDustPerfect(spawn, DustID.Electric,
                (Projectile.Center - spawn).SafeNormalize(Vector2.Zero) * MathHelper.Lerp(1.5f, 4.2f, focus),
                35, default, 0.9f + focus * 0.4f);
            dust.noGravity = true;
        }
        if (!Main.dedServ && age >= 26f && (int)age % 5 == 0)
        {
            Vector2 direction = HammerRotation(age).ToRotationVector2();
            FranFlatLightningEffects.Fracture(Projectile.Center + direction * 55f,
                Projectile.Center - direction * 32f, 0.45f + focus * 0.45f,
                FranFlatLightningPalette.ThorsHammer, Projectile.identity * 71 + (int)age, 1f, 8);
        }
        if (Projectile.timeLeft == 25)
        {
            StopChargeSound();
            if (Main.myPlayer == Projectile.owner)
            {
                Player owner = Main.player[Projectile.owner];
                NPC target = FranHammerTargeting.Select(owner);
                Vector2 destination = target?.Center ?? FranCompanionContext.Aim(owner);
                Vector2 direction = (destination - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Vector2 velocity = direction * (24f / FranThorsHammerDrop.BaseUpdatesPerTick);
                float packedTarget = (target is null ? 0 : target.whoAmI + 1) + PackedTargetStride;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity,
                    ModContent.ProjectileType<FranThorsHammerDrop>(), Projectile.damage, Projectile.knockBack,
                    Projectile.owner, destination.X, destination.Y, packedTarget);
            }
            if (!Main.dedServ)
            {
                FranParticleSystem.Spawn(new FranRingPulseParticle(Projectile.Center, Vector2.Zero,
                    new Color(130, 220, 255), new Vector2(35f), new Vector2(155f), 12, opacity: 0.65f));
            }
            Projectile.Kill();
            return;
        }
    }
    public override void OnKill(int timeLeft) => StopChargeSound();

    private void UpdateChargeSound(float age)
    {
        if (Main.dedServ)
            return;

        if (!chargeSoundStarted)
        {
            chargeSoundStarted = true;
            chargeSoundSlot = SoundEngine.PlaySound(SoundID.Item92 with
                { IsLooped = true, Volume = 0.12f, Pitch = -0.70f }, Projectile.Center);
        }

        if (!SoundEngine.TryGetActiveSound(chargeSoundSlot, out ActiveSound sound))
            return;

        float charge = MathHelper.Clamp(age / WindupTicks, 0f, 1f);
        sound.Volume = MathHelper.Lerp(0.12f, 1f, charge);
        sound.Pitch = MathHelper.Lerp(-0.70f, 0.42f, charge);
        sound.Position = Projectile.Center;
    }

    private void StopChargeSound()
    {
        if (chargeSoundStarted && SoundEngine.TryGetActiveSound(chargeSoundSlot, out ActiveSound sound))
            sound.Stop();
        chargeSoundStarted = false;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float age = 75f - Projectile.timeLeft;
        float introduction = FranMotion.EaseOutBack(FranMotion.Segment(age, 0f, 12f));
        float focus = FranMotion.EaseInCubic(FranMotion.Segment(age, 10f, 50f));
        float preflash = FranMotion.Smooth(FranMotion.Segment(age, 44f, 50f));
        float opacity = introduction * (1f - FranMotion.Smooth(FranMotion.Segment(age, 44f, 50f)));
        const float visualIntensity = 1f;
        float outerRadius = MathHelper.Lerp(108f, 55f, focus) * visualIntensity;
        Color telegraphColor = Color.Lerp(new Color(65, 95, 225), Color.White, preflash * 0.72f);
        FranVfx.BeginAdditive();
        FranVfx.DrawRing(Projectile.Center, telegraphColor, outerRadius,
            opacity * (0.24f + focus * 0.42f) * visualIntensity,
            Main.GlobalTimeWrappedHourly * (0.45f + focus * 3.2f), "FranVfx_FadedStarRing");
        FranVfx.DrawRing(Projectile.Center, Color.White,
            MathHelper.Lerp(72f, 31f, focus) * visualIntensity,
            opacity * (0.10f + focus * 0.40f) * visualIntensity,
            -Main.GlobalTimeWrappedHourly * (0.8f + focus * 3.8f), "FranVfx_BloomRing");
        FranVfx.DrawShared("FranVfx_PulseStar", Projectile.Center,
            new Color(120, 190, 255) * (opacity * (0.18f + focus * 0.48f) * visualIntensity),
            Main.GlobalTimeWrappedHourly * 1.7f, (0.22f + focus * 0.10f) * visualIntensity);
        FranVfx.DrawBloom(Projectile.Center, telegraphColor,
            (24f + preflash * 34f) * visualIntensity,
            opacity * (0.12f + preflash * 0.42f) * visualIntensity, preflash > 0.7f);
        const int sparkCount = 12;
        for (int i = 0; i < sparkCount; i++)
        {
            float angle = MathHelper.TwoPi * i / sparkCount + Main.GlobalTimeWrappedHourly * 0.24f;
            Vector2 point = Projectile.Center + angle.ToRotationVector2() * outerRadius;
            FranVfx.DrawTexturedSpark(point, (Projectile.Center - point).SafeNormalize(Vector2.UnitY),
                i % 2 == 0 ? telegraphColor : new Color(95, 210, 255),
                (18f + focus * 30f) * visualIntensity,
                (2.4f + focus * 2.6f) * visualIntensity,
                opacity * (0.34f + focus * 0.54f) * visualIntensity);
        }
        FranVfx.EndAdditive();
        // The hammer exists visually with the seal from age zero, initially fully transparent.
        float hammerOpacity = FranMotion.Smooth(FranMotion.Segment(age, 0f, 40f));
        FranThorsHammerDrop.DrawHammer(Projectile.Center, HammerRotation(age), hammerOpacity);
        return false;
    }
}

public sealed class ThorsHammerBolt : ModProjectile
{
    private static int ImpactPasses => Math.Max(1, FranBalance.GetInt(nameof(FranBalance.HammerImpactParticleMultiplier), FranBalance.HammerImpactParticleMultiplier));
    // The falling hammer transmits its incoming velocity for visuals only. Beacon strikes default down.
    private Vector2 ImpactDirection => new Vector2(Projectile.ai[0], Projectile.ai[1]).SafeNormalize(Vector2.UnitY);

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 260; Projectile.height = 260; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 20;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.timeLeft is <= 19 and >= 12 ? null : false;
    public override void AI()
    {
        if (Projectile.localAI[0]++ == 0f)
        {
            // The same thick Lightning Strike trees used by the ultimate, now discharged outward.
            // These and the interleaved fine fractures remain visual-only.
            if (Main.myPlayer == Projectile.owner)
            {
                float phase = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 7; i++)
                {
                    Vector2 direction = (phase + MathHelper.TwoPi * i / 7f +
                        Main.rand.NextFloat(-0.12f, 0.12f)).ToRotationVector2();
                    Vector2 start = Projectile.Center + direction * 22f;
                    Vector2 end = Projectile.Center + direction * Main.rand.NextFloat(360f, 560f);
                    FranLightningStrikeVisual.Spawn(Projectile.GetSource_FromThis(), start, end,
                        Projectile.owner, new Color(65, 160, 255), i % 3 == 0 ? 14f : 10f);
                }
            }
            FranFlatLightningEffects.Burst(Projectile.Center, 0.72f,
                FranFlatLightningPalette.ThorsHammer, 9, 325f, innerRadius: 24f);
            SpawnImpactEjecta();
            SoundEngine.PlaySound(FranCombatSounds.雷神之锤命中电弧, Projectile.Center);
            SoundEngine.PlaySound(FranCombatSounds.雷神之锤命中轨道炮, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Thunder, Projectile.Center);
            if (!Main.dedServ)
            {
                // Retain the explosion sound and spectator shake formerly supplied by FranBurst.
                Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 13f *
                    FranBalance.GetFloat(nameof(FranBalance.HammerScreenShakeMultiplier), FranBalance.HammerScreenShakeMultiplier));
            }
            float shakePower = FranBalance.GetFloat(nameof(FranBalance.ThorsHammerBaseScreenShakePower),
                FranBalance.ThorsHammerBaseScreenShakePower);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, shakePower *
                FranBalance.GetFloat(nameof(FranBalance.HammerScreenShakeMultiplier), FranBalance.HammerScreenShakeMultiplier));
        }
        if (Projectile.localAI[0] is 5f or 8f or 11f && !Main.dedServ)
        {
            float cooling = 1f - Projectile.localAI[0] / 18f;
            float wave = (Projectile.localAI[0] - 5f) / 3f;
            for (int i = 0; i < 9 * ImpactPasses; i++)
            {
                // Separate angular lanes and advancing spawn bands leave the impact core readable.
                float angle = MathHelper.TwoPi * (i + wave * 0.37f) / (9f * ImpactPasses) + Projectile.identity * 0.83f +
                    Main.rand.NextFloat(-0.12f, 0.12f);
                Vector2 radial = angle.ToRotationVector2();
                Vector2 offset = radial * Main.rand.NextFloat(68f + wave * 18f, 130f + wave * 18f);
                offset.Y *= 0.58f;
                Vector2 direction = offset.SafeNormalize(radial);
                FranParticleSystem.Spawn(new FranArcParticle(Projectile.Center + offset,
                    direction * Main.rand.NextFloat(3.8f, 6.4f), direction,
                    Color.Lerp(new Color(92, 196, 255), Color.White, Main.rand.NextFloat(0.2f, 0.6f)) * cooling,
                    Main.rand.NextFloat(45f, 90f), Main.rand.NextFloat(30f, 48f), 10));
            }
        }
        for (int i = 0; i < 2 * ImpactPasses; i++)
        {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(35f, 35f), DustID.Electric,
                Main.rand.NextVector2Circular(2f, 2f), 40, default, 1.5f);
            d.noGravity = true;
        }
        float sceneFlash = 1f - FranMotion.Smooth(FranMotion.Segment(Projectile.localAI[0], 1f, 9f));
        const float sustainedIntensity = 1f;
        Lighting.AddLight(Projectile.Center,
            (0.30f + sceneFlash * 0.72f) * sustainedIntensity,
            (0.42f + sceneFlash * 0.92f) * sustainedIntensity,
            (0.88f + sceneFlash * 1.22f) * sustainedIntensity);
    }

    private void SpawnImpactEjecta()
    {
        if (Main.dedServ)
            return;

        Color azure = new(85, 185, 255);
        Color pale = new(185, 238, 255);
        Vector2 incoming = ImpactDirection;
        float phase = Projectile.identity * 0.83f;

        // StellarContemptEcho's alternating sparkle/dust crown: broad speed and lifetime variation.
        // Use Fran particles and the blue hammer palette rather than adding damaging lunar flares.
        for (int i = 0; i < 22 * ImpactPasses; i++)
        {
            float angle = phase + MathHelper.TwoPi * i / (22f * ImpactPasses);
            Vector2 direction = angle.ToRotationVector2();
            float intensity = Main.rand.NextFloat(0.2f, 1f);
            Vector2 position = Projectile.Center + direction * Main.rand.NextFloat(20f, 32f);
            Vector2 velocity = direction * (41f * intensity);
            Color color = i % 3 == 0 ? pale : azure;
            if (i % 2 == 0)
                FranParticleSystem.Spawn(new FranSparkParticle(position, velocity * 0.7f, color,
                    intensity * 0.48f, new Vector2(0.45f, 1.4f),
                    Math.Max(8, (int)(40f * intensity)), 0.94f));
            else
            {
                Dust dust = Dust.NewDustPerfect(position, DustID.FireworksRGB, velocity,
                    0, color, Main.rand.NextFloat(1.4f, 2.1f) * intensity);
                dust.noGravity = true;
            }
        }

        // Concentrated forward spray carries the hammer's momentum through the impact.
        for (int i = 0; i < 6 * ImpactPasses; i++)
        {
            float intensity = Main.rand.NextFloat(0.5f, 1.5f);
            Vector2 direction = incoming.RotatedBy(Main.rand.NextFloat(-1f, 1f) * (1.5f - intensity) * 0.4f);
            FranParticleSystem.Spawn(new FranLineParticle(Projectile.Center + incoming * 16f,
                direction * (45f * intensity), i % 3 == 0 ? pale : azure,
                Main.rand.NextFloat(24f, 46f), 1.4f, Main.rand.Next(12, 23), 0.92f));
        }
        FranParticleSystem.Spawn(new FranBloomCirclePulseParticle(Projectile.Center, Vector2.Zero,
            pale, 12f, 88f, 6, 0.72f, 0.7f));
    }
    private void DrawPressureArcs(float age)
    {
        Vector2 incoming = ImpactDirection;
        Vector2 tangent = incoming.RotatedBy(MathHelper.PiOver2);
        for (int ring = 0; ring < 2; ring++)
        {
            float progress = FranMotion.EaseOutCubic(FranMotion.Segment(age, ring, 17f + ring));
            float fade = 1f - FranMotion.Smooth(FranMotion.Segment(age, 3f + ring, 20f));
            float radius = MathHelper.Lerp(22f, ring == 0 ? 235f : 305f, progress);
            float squash = ring == 0 ? 0.7f : 0.4f;
            Color color = ring == 0 ? new Color(80, 180, 255) : new Color(184, 237, 255);
            for (int i = 0; i < 64; i++)
            {
                float a = MathHelper.TwoPi * i / 64f;
                float b = MathHelper.TwoPi * (i + 1) / 64f;
                Vector2 p = Projectile.Center + (incoming * MathF.Cos(a) * squash + tangent * MathF.Sin(a)) * radius;
                Vector2 q = Projectile.Center + (incoming * MathF.Cos(b) * squash + tangent * MathF.Sin(b)) * radius;
                FranFissionArc.DrawLine(Main.spriteBatch, p, q, color * (fade * 0.22f), 7f);
                FranFissionArc.DrawLine(Main.spriteBatch, p, q, color * (fade * 0.72f), 2f);
            }
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float age = 20f - Projectile.timeLeft;
        float coreFlash = 1f - FranMotion.Smooth(FranMotion.Segment(age, 0f, 4f));
        Color azure = new(108, 210, 255);

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            azure * (0.58f * coreFlash), ImpactDirection.ToRotation(),
            new Vector2(178f, 122f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * coreFlash, 0f, new Vector2(202f, 78f));
        DrawPressureArcs(age);

        FranVfx.EndAdditive();
        return false;
    }
    public override void OnKill(int timeLeft)
    {
        FranVfx.SparkBurst(Projectile.Center, new Color(105, 145, 255), 7 * ImpactPasses, 3.5f, 0.8f);
    }
}
