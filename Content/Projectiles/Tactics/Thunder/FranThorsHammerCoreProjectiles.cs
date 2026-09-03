using System;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


public sealed class ThorsHammerMarker : ModProjectile
{
    private const int PackedTargetStride = 256;
    private int TargetIndex => (int)Projectile.ai[0] - 1;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 120; Projectile.height = 120; Projectile.timeLeft = 75;
        Projectile.tileCollide = false; Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        NPC trackedTarget = GetTrackedTarget();
        if (trackedTarget is not null)
            Projectile.Center = trackedTarget.Center;

        float age = 75f - Projectile.timeLeft;
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
        if (Projectile.timeLeft == 25)
        {
            if (Main.myPlayer == Projectile.owner)
            {
                Vector2 spawn = Projectile.Center + new Vector2(Main.rand.NextFloat(-130f, 130f), -720f);
                Vector2 velocity = (Projectile.Center - spawn).SafeNormalize(Vector2.UnitY) * 24f;
                float packedTarget = Projectile.ai[0] + PackedTargetStride;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawn, velocity,
                    ModContent.ProjectileType<FranThorsHammerDrop>(), Projectile.damage, Projectile.knockBack,
                    Projectile.owner, Projectile.Center.X, Projectile.Center.Y, packedTarget);
            }
            Projectile.Kill();
            return;
        }
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
            Main.GlobalTimeWrappedHourly * (0.45f + focus * 3.2f), "Cal_FadedStarRing");
        FranVfx.DrawRing(Projectile.Center, Color.White,
            MathHelper.Lerp(72f, 31f, focus) * visualIntensity,
            opacity * (0.10f + focus * 0.40f) * visualIntensity,
            -Main.GlobalTimeWrappedHourly * (0.8f + focus * 3.8f), "Cal_BloomRing");
        FranVfx.DrawShared("Cal_PulseStar", Projectile.Center,
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
        return false;
    }

    private NPC GetTrackedTarget()
    {
        if (TargetIndex < 0 || !Main.npc.IndexInRange(TargetIndex))
            return null;
        NPC target = Main.npc[TargetIndex];
        return target.active && !target.friendly && target.life > 0 ? target : null;
    }
}

public sealed class ThorsHammerBolt : ModProjectile
{
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
            const float visualIntensity = 1f;
            Vector2 strikeStart = Projectile.Center - Vector2.UnitY * 900f;
            Vector2 strikeEnd = Projectile.Center + Vector2.UnitY * 38f;
            FranFlatLightningEffects.SpawnSegment(Projectile.GetSource_FromThis(), strikeStart,
                strikeEnd, Projectile.owner, 1.72f,
                FranFlatLightningPalette.ThorsHammer, 2.8f);
            FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.owner,
                FranBurstStyle.ThorsHammer, 1.8f * visualIntensity);
            FranVfx.SparkBurst(Projectile.Center, new Color(120, 165, 255),
                26, 12f * visualIntensity, 1.25f * visualIntensity);
            FranLightingBoltsSystem.SpawnThunderImpact(Projectile.Center,
                new Color(105, 175, 255), 1.35f * visualIntensity);
            FranLightingBoltsSystem.SpawnLightningFragments(Projectile.Center,
                new Color(145, 235, 255), 28, 1.15f * visualIntensity);
            FranParticleEffects.ThorsHammerDetonation(Projectile.Center, 1.18f * visualIntensity);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f, Volume = 1f }, Projectile.Center);
            FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_FromThis(), Projectile.Center,
                -Vector2.UnitY, Projectile.owner, FranSpectatorPalette.Lightning,
                7, 1.35f * visualIntensity);
            float shakePower = FranBalance.GetFloat(nameof(FranBalance.ThorsHammerBaseScreenShakePower),
                FranBalance.ThorsHammerBaseScreenShakePower);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, shakePower);
        }
        for (int i = 0; i < 2; i++)
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
    public override bool PreDraw(ref Color lightColor)
    {
        float seed = Projectile.identity * 0.83f;
        float age = 20f - Projectile.timeLeft;
        float coreFlash = 1f - FranMotion.Smooth(FranMotion.Segment(age, 0f, 5f));
        float expansion = FranMotion.EaseOutCubic(FranMotion.Segment(age, 0f, 10f));
        float blastOpacity = 1f - FranMotion.Smooth(FranMotion.Segment(age, 4f, 17f));
        Color deep = new(38, 55, 145);
        Color azure = Color.Lerp(new Color(70, 115, 255), new Color(170, 245, 255), coreFlash * 0.72f);

        FranVfx.BeginAdditive();
        FranVfx.DrawShared("Cal_CircularSmearSmokey", Projectile.Center,
            deep * (0.38f * blastOpacity), seed * 0.07f,
            MathHelper.Lerp(0.54f, 1.34f, expansion));
        FranVfx.DrawShared("CL_BloomCircle", Projectile.Center,
            new Color(52, 88, 255, 0) * (0.46f * blastOpacity), seed * 0.09f,
            MathHelper.Lerp(0.42f, 1.84f, expansion));
        FranVfx.DrawShared("CL_BloomCircle", Projectile.Center,
            new Color(175, 235, 255, 0) * (0.64f * blastOpacity), -seed * 0.06f,
            MathHelper.Lerp(0.24f, 1.16f, expansion));
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            azure * (0.82f * blastOpacity + 0.64f * coreFlash), seed,
            Vector2.Lerp(new Vector2(94f, 72f), new Vector2(430f, 244f), expansion));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * (0.98f * coreFlash + 0.32f * blastOpacity), seed,
            Vector2.Lerp(new Vector2(150f, 92f), new Vector2(338f, 188f), expansion));

        int radialCount = 10;
        for (int i = 0; i < radialCount; i++)
        {
            float angle = seed * 0.11f + MathHelper.TwoPi * i / radialCount;
            Vector2 direction = angle.ToRotationVector2();
            float rayLength = MathHelper.Lerp(24f, 132f + (i % 2) * 68f, expansion);
            Vector2 rayStart = Projectile.Center + direction * 18f;
            Vector2 rayEnd = Projectile.Center + direction * rayLength;
            Color rayColor = i % 3 == 0 ? Color.White : i % 2 == 0 ? azure : new Color(110, 135, 255);
            FranVfx.DrawTexturedSpark(rayStart, direction, rayColor,
                Math.Max(8f, rayLength - 18f), 4.2f + coreFlash * 2.2f, blastOpacity * 0.74f);
        }

        for (int i = 0; i < 3; i++)
        {
            float ringRadius = MathHelper.Lerp(32f + i * 12f, 122f + i * 68f, expansion);
            FranVfx.DrawRing(Projectile.Center, i == 1 ? Color.White : azure, ringRadius,
                blastOpacity * (0.74f - i * 0.16f), seed * (i % 2 == 0 ? 0.05f : -0.04f),
                i == 2 ? "Cal_HighResHollowCircle" : "Cal_BloomRing");
        }
        FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center,
            Color.White * (0.98f * coreFlash + 0.42f * blastOpacity), seed,
            new Vector2(0.58f, 0.105f));
        FranVfx.DrawBloom(Projectile.Center, Color.White,
            MathHelper.Lerp(118f, 196f, expansion), 0.74f * blastOpacity + 0.72f * coreFlash, true);
        FranVfx.EndAdditive();
        return false;
    }
    public override void OnKill(int timeLeft)
    {
        FranVfx.SparkBurst(Projectile.Center, new Color(105, 145, 255), 7, 3.5f, 0.8f);
    }
}
