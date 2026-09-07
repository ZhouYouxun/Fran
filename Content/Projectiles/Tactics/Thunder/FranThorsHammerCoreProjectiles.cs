using System;
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
        UpdateTargetSelection();
        NPC trackedTarget = GetTrackedTarget();
        if (trackedTarget is null)
        {
            // With no enemy there is no arbitrary cursor/ground strike. Allow the remaining windup
            // to acquire a target, but never release an untargeted falling hammer.
            if (Projectile.timeLeft <= 25)
                Projectile.Kill();
            return;
        }
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
                Vector2 velocity = (Projectile.Center - spawn).SafeNormalize(Vector2.UnitY) *
                    (24f / FranThorsHammerDrop.UpdatesPerTick);
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
        if (GetTrackedTarget() is null)
            return false;
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

    private void UpdateTargetSelection()
    {
        if (Main.myPlayer != Projectile.owner)
            return;

        TeacherSwingRebuild source = null;
        if (Projectile.ai[2] > 0f)
        {
            int identity = (int)Projectile.ai[2] - 1;
            foreach (Projectile candidate in Main.ActiveProjectiles)
            {
                if (candidate.owner == Projectile.owner && candidate.identity == identity &&
                    candidate.ModProjectile is TeacherSwingRebuild swing)
                {
                    source = swing;
                    break;
                }
            }
        }

        Player owner = Main.player[Projectile.owner];
        NPC target = source is not null
            ? FranHammerTargeting.Select(owner, source.HammerHitTargets)
            : FranHammerTargeting.Resolve(owner, TargetIndex);
        // Resolve once more after the final damage window so its very last hit is included.
        if (Projectile.ai[2] > 0f && (source is null || source.HammerHitWindowEnded))
        {
            Projectile.ai[2] = 0f;
            Projectile.netUpdate = true;
        }
        int encoded = target is null ? 0 : target.whoAmI + 1;
        if (Projectile.ai[0] != encoded)
        {
            Projectile.ai[0] = encoded;
            Projectile.netUpdate = true;
        }
    }

    private NPC GetTrackedTarget() => FranHammerTargeting.GetLockedTarget(TargetIndex);
}

public sealed class ThorsHammerBolt : ModProjectile
{
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
            const float visualIntensity = 1f;
            Vector2 strikeStart = Projectile.Center - Vector2.UnitY * 900f;
            Vector2 strikeEnd = Projectile.Center + Vector2.UnitY * 38f;
            FranFlatLightningEffects.SpawnSegment(Projectile.GetSource_FromThis(), strikeStart,
                strikeEnd, Projectile.owner, 1.72f,
                FranFlatLightningPalette.ThorsHammer, 2.8f);
            FranVfx.SparkBurst(Projectile.Center, new Color(120, 165, 255),
                18, 12f * visualIntensity, 1.05f * visualIntensity);
            FranLightingBoltsSystem.SpawnThunderImpact(Projectile.Center,
                new Color(105, 175, 255), 0.70f * visualIntensity);
            SpawnImpactEjecta();
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f, Volume = 1f }, Projectile.Center);
            if (!Main.dedServ)
            {
                // Retain the explosion sound and spectator shake formerly supplied by FranBurst.
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.48f, Pitch = -0.35f }, Projectile.Center);
                Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 13f);
            }
            FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_FromThis(), Projectile.Center,
                -Vector2.UnitY, Projectile.owner, FranSpectatorPalette.Lightning,
                7, 1.35f * visualIntensity);
            float shakePower = FranBalance.GetFloat(nameof(FranBalance.ThorsHammerBaseScreenShakePower),
                FranBalance.ThorsHammerBaseScreenShakePower);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, shakePower);
        }
        // Main sky fracture first, ground branches two ticks later, then brief independent after-arcs.
        if (Projectile.localAI[0] == 3f)
            FranFlatLightningEffects.Burst(Projectile.Center, 1.12f,
                FranFlatLightningPalette.ThorsHammer, 8, 260f, ground: true, innerRadius: 28f);
        if (Projectile.localAI[0] is 5f or 8f or 11f && !Main.dedServ)
        {
            float cooling = 1f - Projectile.localAI[0] / 18f;
            float wave = (Projectile.localAI[0] - 5f) / 3f;
            for (int i = 0; i < 9; i++)
            {
                // Separate angular lanes and advancing spawn bands leave the impact core readable.
                float angle = MathHelper.TwoPi * (i + wave * 0.37f) / 9f + Projectile.identity * 0.83f +
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

    private void SpawnImpactEjecta()
    {
        if (Main.dedServ)
            return;

        Color azure = new(118, 218, 255);
        Vector2 incoming = ImpactDirection;
        Vector2 tangent = incoming.RotatedBy(MathHelper.PiOver2);
        float phase = Projectile.identity * 0.83f;

        // Fast radial streaks visibly carry energy away from the hit, then brake and burn out.
        for (int i = 0; i < 14; i++)
        {
            Vector2 direction = (phase + MathHelper.TwoPi * i / 14f +
                Main.rand.NextFloat(-0.1f, 0.1f)).ToRotationVector2();
            Vector2 position = Projectile.Center + direction * Main.rand.NextFloat(28f, 46f);
            Vector2 velocity = direction * Main.rand.NextFloat(11f, 20f);
            FranParticleSystem.Spawn(new FranLineParticle(position, velocity,
                i % 4 == 0 ? Color.White : azure, Main.rand.NextFloat(24f, 48f),
                Main.rand.NextFloat(1.2f, 2f), Main.rand.Next(12, 18), 0.90f));
        }

        // A narrower forward spray preserves the falling hammer's momentum through the impact.
        for (int i = 0; i < 6; i++)
        {
            float spread = MathHelper.Lerp(-0.38f, 0.38f, i / 5f);
            Vector2 direction = incoming.RotatedBy(spread);
            Vector2 position = Projectile.Center + incoming * 26f + tangent * (i - 2.5f) * 7f;
            FranParticleSystem.Spawn(new FranLineParticle(position, direction * Main.rand.NextFloat(18f, 26f),
                new Color(195, 241, 255), Main.rand.NextFloat(38f, 64f), 1.7f,
                Main.rand.Next(10, 15), 0.88f));
        }
    }

    private void DrawPressureArcs(float age)
    {
        float progress = FranMotion.EaseOutCubic(FranMotion.Segment(age, 0f, 11f));
        float fade = 1f - FranMotion.Smooth(FranMotion.Segment(age, 2f, 12f));
        if (fade <= 0f)
            return;
        Vector2 incoming = ImpactDirection;
        Vector2 tangent = incoming.RotatedBy(MathHelper.PiOver2);
        Vector2 center = Projectile.Center - incoming * MathHelper.Lerp(12f, 30f, progress);
        float radius = MathHelper.Lerp(32f, 158f, progress);
        // Open at the front and back: the two side arcs sit above the wider ground pressure ring.
        for (int i = 0; i < 40; i++)
        {
            float a = MathHelper.TwoPi * i / 40f;
            float b = MathHelper.TwoPi * (i + 1) / 40f;
            if (MathF.Abs(MathF.Cos((a + b) * 0.5f)) < 0.34f)
                continue;
            Vector2 p = center + (tangent * MathF.Cos(a) + incoming * MathF.Sin(a) * 0.52f) * radius;
            Vector2 q = center + (tangent * MathF.Cos(b) + incoming * MathF.Sin(b) * 0.52f) * radius;
            FranFissionArc.DrawLine(Main.spriteBatch, p, q, new Color(108, 210, 255) * (fade * 0.24f), 3.6f);
            FranFissionArc.DrawLine(Main.spriteBatch, p, q, new Color(214, 245, 255) * (fade * 0.52f), 1.1f);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float seed = Projectile.identity * 0.83f;
        float age = 20f - Projectile.timeLeft;
        float coreFlash = 1f - FranMotion.Smooth(FranMotion.Segment(age, 0f, 4f));
        float expansion = FranMotion.EaseOutCubic(FranMotion.Segment(age, 1f, 13f));
        float blastOpacity = 1f - FranMotion.Smooth(FranMotion.Segment(age, 3f, 14f));
        Color azure = new(108, 210, 255);

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            azure * (0.58f * coreFlash), -MathHelper.PiOver2,
            new Vector2(178f, 122f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * coreFlash, 0f, new Vector2(202f, 78f));
        DrawPressureArcs(age);

        // One broken elliptical pressure front, drawn from pixels so no new ring texture is needed.
        float radius = MathHelper.Lerp(32f, 258f, expansion);
        for (int i = 0; i < 52; i++)
        {
            if ((i + Projectile.identity) % 13 < 3)
                continue;
            float a = MathHelper.TwoPi * i / 52f + seed;
            float b = MathHelper.TwoPi * (i + 1) / 52f + seed;
            Vector2 p = Projectile.Center + new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.28f) * radius;
            Vector2 q = Projectile.Center + new Vector2(MathF.Cos(b), MathF.Sin(b) * 0.28f) * radius;
            FranFissionArc.DrawLine(Main.spriteBatch, p, q, azure * (blastOpacity * 0.38f), 3f);
            FranFissionArc.DrawLine(Main.spriteBatch, p, q, Color.White * (blastOpacity * 0.5f), 0.9f);
        }
        FranVfx.EndAdditive();
        return false;
    }
    public override void OnKill(int timeLeft)
    {
        FranVfx.SparkBurst(Projectile.Center, new Color(105, 145, 255), 7, 3.5f, 0.8f);
    }
}
