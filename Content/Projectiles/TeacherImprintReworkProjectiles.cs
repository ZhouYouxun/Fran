using System;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

public sealed class FranSkillTakerWisp : ModProjectile
{
    private const float OutwardTime = 13f;
    private float Age => Projectile.localAI[0];

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 88;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool? CanDamage() => Age >= OutwardTime ? null : false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        if (age < OutwardTime)
        {
            float outwardCurve = (Projectile.identity % 2 == 0 ? 1f : -1f) *
                MathHelper.Lerp(0.045f, 0.012f, age / OutwardTime);
            Projectile.velocity = Projectile.velocity.RotatedBy(outwardCurve) * 0.94f;
        }
        else
        {
            NPC target = FindTarget();
            if (target is not null)
            {
                Vector2 fallback = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 intercept = target.Center + target.velocity * 4f;
                float weave = MathF.Sin(age * 0.23f + Projectile.identity * 0.91f) * 0.10f;
                Vector2 desired = (intercept - Projectile.Center).SafeNormalize(fallback).RotatedBy(weave) * 18.5f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.105f);
            }
            else
                Projectile.velocity = Projectile.velocity.RotatedBy(
                    MathF.Sin(age * 0.19f + Projectile.identity) * 0.012f) * 1.012f;

            if (age == OutwardTime)
            {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.20f, Pitch = 0.52f }, Projectile.Center);
                FranParticleEffects.Implosion(Projectile.Center, new Color(205, 95, 255), 0.42f);
            }
        }

        Projectile.rotation += 0.24f;
        Lighting.AddLight(Projectile.Center, 0.34f, 0.07f, 0.48f);
        FranVanillaDustEffects.ProjectileTrail(Projectile.Center, Projectile.velocity,
            FranSkill.SkillTaker, new Color(210, 95, 255), 0.12f, 0.86f);
        if (!Main.dedServ && Projectile.timeLeft % 3 == 0)
            FranParticleSystem.Spawn(new FranSkillGlyphParticle(Projectile.Center,
                -Projectile.velocity * 0.045f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                new Color(215, 105, 255), 0.48f, 14));
    }

    private NPC FindTarget()
    {
        NPC fallback = null;
        NPC best = null;
        float fallbackDistance = 720f * 720f;
        float bestDistance = fallbackDistance;
        int originalTarget = (int)Projectile.ai[0];
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy())
                continue;
            float distance = Vector2.DistanceSquared(Projectile.Center, npc.Center);
            if (npc.whoAmI == originalTarget)
            {
                if (distance < fallbackDistance)
                {
                    fallbackDistance = distance;
                    fallback = npc;
                }
                continue;
            }
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            best = npc;
        }
        return best ?? fallback;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner,
            FranBurstStyle.SkillTaker, 0.48f + Projectile.ai[1] * 0.025f);
        FranParticleEffects.SpawnImpact(target.Center, Projectile.velocity, new Color(215, 95, 255),
            7, 5.5f, 0.72f);
        FranParticleEffects.BlackLight(target.Center, new Color(195, 70, 255),
            0.46f + Projectile.ai[1] * 0.025f, 18);
        FranParticleEffects.MysticGlyphs(target.Center, new Color(225, 125, 255), 2, 27f,
            0.68f, -Projectile.velocity * 0.018f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = MathHelper.Clamp(Projectile.timeLeft / 16f, 0f, 1f);
        if (!FranProjectileTrailRenderer.TryDraw(Projectile, FranProjectileTrailProfile.ArcaneWisp,
                new Color(58, 16, 84), new Color(205, 72, 255), Color.White, 14f, opacity * 0.92f))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.ArcaneWisp,
                new Color(190, 65, 255), 18f, opacity);
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(190, 75, 235) * (0.72f * opacity), Projectile.rotation * 0.45f,
            new Vector2(46f, 46f));
        FranVfx.BeginAdditive();
        float pulse = 0.92f + MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity) * 0.08f;
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(215, 100, 255) * (0.42f * opacity), -Projectile.rotation * 0.7f,
            new Vector2(56f, 56f) * pulse);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * (0.70f * opacity), Projectile.velocity.ToRotation(), new Vector2(34f, 22f) * pulse);
        FranVfx.DrawBloom(Projectile.Center, new Color(200, 75, 255), 28f, 0.62f * opacity);
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranTeacherPhantomBlade : ModProjectile
{
    private bool HasInitialEnemy => Projectile.ai[0] > 0f;
    private float StoredDelay => Math.Max(1f, MathF.Abs(Projectile.ai[0]));
    private float LaunchDelay => HasInitialEnemy ? StoredDelay : 1f;
    private float Age => Projectile.localAI[0];

    public override string Texture => $"Terraria/Images/Item_{ItemID.BreakerBlade}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 16;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 46;
        Projectile.height = 46;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 86;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 18;
    }

    public override bool? CanDamage() => Age >= LaunchDelay ? null : false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        Vector2 target = new(Projectile.ai[1], Projectile.ai[2]);
        if (age == 0f)
        {
            Projectile.localAI[1] = Projectile.Center.X;
            Projectile.localAI[2] = Projectile.Center.Y;
            Projectile.Opacity = 0.12f;
        }

        Vector2 formationOrigin = new(Projectile.localAI[1], Projectile.localAI[2]);
        Vector2 forward = (target - formationOrigin).SafeNormalize(Vector2.UnitX);
        if (age < LaunchDelay)
        {
            Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
            Vector2 hover = formationOrigin - forward * (10f + age * 1.4f) +
                normal * MathF.Sin(age * 0.65f + Projectile.identity) * 5f;
            Projectile.Center = Vector2.Lerp(Projectile.Center, hover, 0.38f);
            Projectile.velocity = Vector2.Zero;
            Projectile.Opacity = MathHelper.Lerp(Projectile.Opacity, 0.76f, 0.28f);
        }
        else
        {
            if (age == LaunchDelay)
            {
                float side = Projectile.identity % 2 == 0 ? 1f : -1f;
                float launchCurve = HasInitialEnemy ? side * 0.24f : 0f;
                Projectile.velocity = (target - Projectile.Center).SafeNormalize(forward).RotatedBy(launchCurve) *
                    (HasInitialEnemy ? 23f : 28f);
                Projectile.Opacity = 0.92f;
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.28f, Pitch = 0.24f }, Projectile.Center);
                FranParticleEffects.LineRelease(Projectile.Center - forward * 26f,
                    Projectile.Center + forward * 48f, new Color(225, 245, 255), 8, 0.64f);
            }

            float flightAge = age - LaunchDelay;
            NPC flightTarget = HasInitialEnemy ? FindFlightTarget(target, 620f) : null;
            Vector2 aimPoint = flightTarget?.Center + flightTarget.velocity * 3.5f ?? target;
            Vector2 fallback = Projectile.velocity.SafeNormalize(forward);
            float speed = MathHelper.Lerp(HasInitialEnemy ? 23f : 28f, HasInitialEnemy ? 38f : 43f,
                FranMotion.Smooth(FranMotion.Segment(flightAge, 0f, 24f)));
            float sideCurve = HasInitialEnemy ? (Projectile.identity % 2 == 0 ? 1f : -1f) *
                MathHelper.Lerp(0.20f, 0.015f, FranMotion.Smooth(FranMotion.Segment(flightAge, 0f, 30f))) : 0f;
            float weave = HasInitialEnemy ?
                MathF.Sin(flightAge * 0.24f + Projectile.identity * 0.77f) * 0.055f : 0f;
            Vector2 desired = (aimPoint - Projectile.Center).SafeNormalize(fallback)
                .RotatedBy(sideCurve + weave) * speed;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, HasInitialEnemy ? 0.135f : 0.28f);
            Projectile.Opacity = Math.Min(0.92f, MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f));

            if (!HasInitialEnemy && flightAge >= 4f && Vector2.DistanceSquared(Projectile.Center, target) <= 32f * 32f)
            {
                FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), target, Projectile.owner,
                    FranBurstStyle.SwordArray, 0.42f);
                FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_FromThis(), target,
                    Projectile.velocity, Projectile.owner, FranSpectatorPalette.Mint, 3, 0.72f);
                Projectile.Kill();
                return;
            }
        }

        Projectile.rotation = (age < LaunchDelay ? forward : Projectile.velocity.SafeNormalize(forward)).ToRotation() +
            MathHelper.PiOver4;
        Lighting.AddLight(Projectile.Center, 0.24f, 0.34f, 0.46f);
        if (age >= LaunchDelay)
            FranVanillaDustEffects.ProjectileTrail(Projectile.Center, Projectile.velocity,
                FranSkill.MultipleBodyCreation, new Color(225, 245, 255), 0.16f, 0.88f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner,
            FranBurstStyle.SwordArray, 0.48f);
        FranParticleEffects.SpawnImpact(target.Center, Projectile.velocity, new Color(225, 245, 255),
            7, 5.6f, 0.72f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Item[ItemID.BreakerBlade].Value;
        Vector2 origin = texture.Size() * 0.5f;
        if (Age >= LaunchDelay && !FranProjectileTrailRenderer.TryDraw(Projectile,
                FranProjectileTrailProfile.SpectralBlade, new Color(18, 40, 62),
                new Color(145, 215, 255), Color.White, 21f, Projectile.Opacity * 0.82f))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.SpectralBlade,
                new Color(145, 215, 255), 24f, Projectile.Opacity);

        FranVfx.BeginAdditive();
        Vector2 direction = (Age < LaunchDelay ? new Vector2(Projectile.ai[1], Projectile.ai[2]) - Projectile.Center :
            Projectile.velocity).SafeNormalize(Vector2.UnitX);
        if (Age < LaunchDelay)
        {
            float deploy = FranMotion.Smooth(FranMotion.Segment(Age, 0f, LaunchDelay));
            FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
                new Color(175, 225, 255) * (0.52f * deploy), -Projectile.rotation,
                Vector2.One * (58f - deploy * 18f));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                Color.White * (0.58f * deploy), direction.ToRotation(), new Vector2(56f, 30f));
        }
        else
        {
            FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 28f,
                new Color(220, 245, 255) * 0.58f, direction.ToRotation(), new Vector2(144f, 48f));
        }
        FranVfx.DrawBloom(Projectile.Center, new Color(180, 225, 255), 30f, 0.34f * Projectile.Opacity);
        FranVfx.EndAdditive();

        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.Lerp(new Color(90, 135, 175), Color.White, 0.66f) * Projectile.Opacity,
            Projectile.rotation, origin, 0.68f, SpriteEffects.None, 0f);
        return false;
    }

    private NPC FindFlightTarget(Vector2 intendedTarget, float distance)
    {
        NPC best = null;
        float bestScore = distance * distance;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy())
                continue;
            float projectileDistance = Vector2.DistanceSquared(Projectile.Center, npc.Center);
            if (projectileDistance > distance * distance)
                continue;
            float score = projectileDistance + Vector2.DistanceSquared(intendedTarget, npc.Center) * 0.42f;
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = npc;
        }
        return best;
    }
}

public sealed class FranThunderDropVisual : ModProjectile
{
    private const int MaximumLifetime = 30;
    private const float MainStrikeMarker = 1000f;
    private const float FinisherMarker = 2000f;
    private float Age => Projectile.localAI[0];
    private bool MainStrike => Projectile.ai[2] >= MainStrikeMarker;
    private bool Finisher => Projectile.ai[2] >= FinisherMarker;
    private float Seed => Projectile.ai[2] % MainStrikeMarker;
    private float ImpactAge => Finisher ? 12f : MainStrike ? 10f : 8f;
    private float VisualLifetime => Finisher ? 30f : MainStrike ? 26f : 20f;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = MaximumLifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        if (Projectile.localAI[1] == 0f && age >= ImpactAge)
        {
            Projectile.localAI[1] = 1f;
            TriggerImpact();
        }

        float impactLight = MainStrike && age >= ImpactAge
            ? 1f - FranMotion.Smooth(FranMotion.Segment(age, ImpactAge, ImpactAge + 10f))
            : 0f;
        Lighting.AddLight(Projectile.Center,
            0.16f + impactLight * (Finisher ? 0.52f : 0.34f),
            0.18f + impactLight * (Finisher ? 0.62f : 0.42f),
            0.48f + impactLight * 0.52f);

        if (age >= VisualLifetime - 1f)
            Projectile.Kill();
    }

    private void TriggerImpact()
    {
        Color violet = new(155, 125, 255);
        Color azure = new(125, 205, 255);
        if (!MainStrike)
        {
            FranLightingBoltsSystem.SpawnThunderImpact(Projectile.Center, violet, 0.54f);
            FranVfx.SparkBurst(Projectile.Center, azure, 5, 4.2f, 0.52f);
            FranVanillaDustEffects.Impact(Projectile.Center, -Vector2.UnitY, FranSkill.ThunderChain,
                azure, 24, 0.72f);
            if (Projectile.identity % 3 == 0)
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.20f, Pitch = 0.28f }, Projectile.Center);
            return;
        }

        float intensity = Finisher ? 1.32f : 1f;
        FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.owner,
            FranBurstStyle.Lightning, Finisher ? 1.78f : 1.48f);
        FranLightingBoltsSystem.SpawnThunderImpact(Projectile.Center, violet, 1.18f * intensity);
        FranVfx.SparkBurst(Projectile.Center, azure, Finisher ? 22 : 16,
            Finisher ? 10.5f : 8.2f, Finisher ? 1.15f : 0.92f);
        FranVanillaDustEffects.Impact(Projectile.Center, -Vector2.UnitY, FranSkill.ThunderChain,
            azure, Finisher ? 52 : 40, Finisher ? 1.42f : 1.18f);
        FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_FromThis(), Projectile.Center,
            -Vector2.UnitY, Projectile.owner, FranSpectatorPalette.Lightning, Finisher ? 7 : 5,
            Finisher ? 1.42f : 1.15f);

        SpawnGroundArcs(Finisher ? 7 : 4, Finisher ? 1.28f : 1f);
        if (!Main.dedServ)
            Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, Finisher ? 10.5f : 7.4f);
        SoundEngine.PlaySound(SoundID.Item122 with
        {
            Volume = Finisher ? 1f : 0.82f,
            Pitch = Finisher ? -0.34f : -0.22f
        }, Projectile.Center);
        SoundEngine.PlaySound(SoundID.Item93 with
        {
            Volume = Finisher ? 0.62f : 0.48f,
            Pitch = Finisher ? -0.12f : 0.02f
        }, Projectile.Center);
    }

    private void SpawnGroundArcs(int count, float scale)
    {
        if (Main.netMode == NetmodeID.Server || Main.myPlayer != Projectile.owner)
            return;

        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector2 velocity = new Vector2(side, 0f).RotatedBy(Main.rand.NextFloat(-0.24f, 0.14f)) *
                Main.rand.NextFloat(6.8f, 11.8f) * scale;
            float curvature = -side * Main.rand.NextFloat(0.004f, 0.012f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity,
                ModContent.ProjectileType<FranThunderGroundArcVisual>(), 0, 0f, Projectile.owner,
                curvature, Seed + i * 8.17f, scale * Main.rand.NextFloat(0.84f, 1.12f));
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 start = new(Projectile.ai[0], Projectile.ai[1]);
        Vector2 end = Projectile.Center;
        float fall = FranMotion.Smooth(FranMotion.Segment(Age, MainStrike ? 1f : 0f, ImpactAge));
        Vector2 fallingEnd = Vector2.Lerp(start, end, fall);
        float fadeOut = 1f - FranMotion.Smooth(FranMotion.Segment(Age, ImpactAge + 2f, VisualLifetime));
        float opacity = Math.Max(0f, fadeOut) * MathHelper.Clamp(fall * 1.8f + 0.10f, 0f, 1f);
        float seed = Seed + Projectile.identity * 0.73f;
        float intensity = Finisher ? 1.28f : MainStrike ? 1f : 0.58f;
        float trunkWidth = (Finisher ? 22f : MainStrike ? 16f : 6.2f) *
            (0.86f + 0.14f * MathF.Sin(Main.GlobalTimeWrappedHourly * 31f + seed));
        int segments = Finisher ? 16 : MainStrike ? 14 : 10;
        Color stableBody = MainStrike ? new Color(36, 42, 104) : new Color(42, 50, 112);

        // Alpha-blended body remains readable on bright terrain; additive passes below supply emission.
        FranVfx.DrawTexturedLightning(start, fallingEnd, stableBody, trunkWidth * 0.78f,
            segments, seed, opacity * 0.72f);
        FranVfx.BeginAdditive();
        Vector2 fallDirection = (fallingEnd - start).SafeNormalize(Vector2.UnitY);
        Vector2 fallNormal = fallDirection.RotatedBy(MathHelper.PiOver2);

        if (MainStrike && Age < ImpactAge)
        {
            float warningProgress = FranMotion.Smooth(FranMotion.Segment(Age, 0f, ImpactAge));
            float warningRadius = MathHelper.Lerp(Finisher ? 142f : 108f, Finisher ? 30f : 24f, warningProgress);
            float warningOpacity = MathF.Sin(warningProgress * MathHelper.Pi) * (Finisher ? 0.92f : 0.72f);
            FranVfx.DrawRing(end, new Color(100, 145, 255), warningRadius, warningOpacity,
                -Main.GlobalTimeWrappedHourly * 3.4f, "Cal_BloomRing");
            FranVfx.DrawRing(end, Color.White, warningRadius * 0.62f, warningOpacity * 0.62f,
                Main.GlobalTimeWrappedHourly * 4.8f, "Cal_FadedStarRing");
            for (int i = 0; i < (Finisher ? 8 : 6); i++)
            {
                float angle = MathHelper.TwoPi * i / (Finisher ? 8f : 6f) + Seed * 0.03f;
                Vector2 point = end + angle.ToRotationVector2() * warningRadius;
                FranVfx.DrawTexturedSpark(point, (end - point).SafeNormalize(Vector2.UnitY),
                    i % 2 == 0 ? new Color(115, 175, 255) : new Color(165, 110, 255),
                    20f + warningProgress * 34f, 2.4f + warningProgress * 2.8f, warningOpacity);
            }
        }

        for (int i = 0; i < 3; i++)
        {
            float completion = MathHelper.Clamp(fall - i * 0.11f, 0f, 1f);
            Vector2 point = Vector2.Lerp(start, end, completion) + fallNormal *
                MathF.Sin(seed + i * 2.2f) * (10f + i * 4f) * intensity;
            FranVfx.DrawSharedPixels("Cal_DrainLineBloom", point,
                (i == 0 ? Color.White : new Color(105, 220, 255)) * (opacity * (0.58f - i * 0.12f)),
                fallDirection.ToRotation() + MathHelper.PiOver2,
                new Vector2((MainStrike ? 11f : 7f) - i, (MainStrike ? 88f : 58f) - i * 12f));
        }

        Color trunkColor = Color.Lerp(new Color(112, 105, 255), new Color(115, 230, 255),
            Finisher ? 0.58f : 0.34f);
        FranVfx.DrawTexturedLightning(start, fallingEnd, trunkColor, trunkWidth,
            segments, seed, opacity * (MainStrike ? 0.96f : 0.62f));
        FranVfx.DrawCappedLaser(start, fallingEnd, new Color(45, 54, 175), Color.White,
            trunkWidth * (MainStrike ? 0.64f : 0.46f), opacity * (MainStrike ? 0.64f : 0.28f));

        if (MainStrike && fall > 0.34f)
        {
            int branchCount = Finisher ? 4 : 2;
            for (int i = 0; i < branchCount; i++)
            {
                float branchSign = i % 2 == 0 ? -1f : 1f;
                float anchorProgress = 0.42f + (i / 2) * 0.14f;
                Vector2 branchStart = Vector2.Lerp(start, fallingEnd, Math.Min(anchorProgress, fall));
                Vector2 branchDirection = fallDirection.RotatedBy(branchSign * (0.48f + i * 0.055f));
                Vector2 branchEnd = branchStart + branchDirection * (Finisher ? 145f : 105f) * fall;
                FranVfx.DrawTexturedLightning(branchStart, branchEnd,
                    i % 2 == 0 ? new Color(130, 105, 255) : new Color(105, 220, 255),
                    Finisher ? 5.2f : 4.1f, 7, seed + 5.1f + i * 3.7f, opacity * 0.58f);
            }
        }

        if (Age >= ImpactAge - 1f)
        {
            float impact = 1f - FranMotion.Smooth(FranMotion.Segment(Age, ImpactAge, ImpactAge + (MainStrike ? 13f : 8f)));
            float expansion = FranMotion.Smooth(FranMotion.Segment(Age, ImpactAge, ImpactAge + 12f));
            Vector2 burstSize = MainStrike
                ? new Vector2(Finisher ? 248f : 194f, Finisher ? 174f : 132f)
                : new Vector2(92f, 66f);
            FranVfx.DrawOriginalPixels("FranImpactBurst", end,
                new Color(105, 155, 255) * (impact * (MainStrike ? 0.94f : 0.56f)), MathHelper.PiOver2,
                burstSize);
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", end, Color.White * (impact * 0.92f),
                MathHelper.PiOver2, burstSize * new Vector2(0.76f, 0.58f));
            if (MainStrike)
            {
                float ringRadius = MathHelper.Lerp(34f, Finisher ? 190f : 142f, expansion);
                FranVfx.DrawRing(end, trunkColor, ringRadius, impact * 0.76f,
                    Seed * 0.1f + expansion, "Cal_BloomRing");
                FranVfx.DrawRing(end, Color.White, ringRadius * 0.66f, impact * 0.48f,
                    -Seed * 0.07f - expansion, "Cal_HighResHollowCircle");
            }
            FranVfx.DrawBloom(end, Color.White, MainStrike ? (Finisher ? 106f : 82f) : 38f,
                impact * (MainStrike ? 0.62f : 0.32f), MainStrike);
        }
        FranVfx.EndAdditive();
        return false;
    }
}

// A harmless spectator projectile: each arc has its own curved trajectory and trail, then burns out.
// It gives the focal strike a lingering electric aftermath without adding hidden damage instances.
public sealed class FranThunderGroundArcVisual : ModProjectile
{
    private const int Lifetime = 30;
    private float Age => Projectile.localAI[0];

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 15;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 6;
        Projectile.height = 6;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override bool? CanDamage() => false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        float progress = MathHelper.Clamp(age / Lifetime, 0f, 1f);
        float randomTurn = MathF.Sin(Projectile.ai[1] + age * 1.73f) * 0.012f;
        Projectile.velocity = Projectile.velocity.RotatedBy(Projectile.ai[0] + randomTurn) * 0.966f;
        Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y, 0f, 0.10f);
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.Opacity = MathHelper.Clamp(MathF.Sin(progress * MathHelper.Pi) * 1.45f, 0f, 1f);
        Lighting.AddLight(Projectile.Center, 0.08f * Projectile.Opacity, 0.16f * Projectile.Opacity,
            0.38f * Projectile.Opacity);

        if (!Main.dedServ && (int)age % 6 == 0 && Main.rand.NextBool(2))
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric,
                -Projectile.velocity * Main.rand.NextFloat(0.04f, 0.12f) + Main.rand.NextVector2Circular(0.8f, 0.8f),
                35, new Color(135, 190, 255), Main.rand.NextFloat(0.72f, 1.05f));
            dust.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float scale = MathHelper.Clamp(Projectile.ai[2], 0.62f, 1.65f);
        float opacity = Projectile.Opacity;
        Vector2 previous = Projectile.Center;

        for (int i = 0; i < Projectile.oldPos.Length; i += 2)
        {
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            if (Projectile.oldPos[i] == Vector2.Zero || Vector2.DistanceSquared(previous, point) > 80f * 80f)
                continue;
            float fade = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawVertical("Cal_ThinEndedLine", previous, point,
                new Color(28, 34, 92) * (opacity * fade * 0.72f), 4.8f * scale * fade);
            previous = point;
        }

        FranVfx.BeginAdditive();
        previous = Projectile.Center;
        for (int i = 0; i < Projectile.oldPos.Length; i += 2)
        {
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            if (Projectile.oldPos[i] == Vector2.Zero || Vector2.DistanceSquared(previous, point) > 80f * 80f)
                continue;
            float fade = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawTexturedLightning(previous, point,
                i % 4 == 0 ? new Color(155, 115, 255) : new Color(105, 215, 255),
                3.1f * scale * Math.Max(0.35f, fade), 3, Projectile.ai[1] + i * 2.7f,
                opacity * fade * 0.82f);
            previous = point;
        }
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.72f * opacity),
            Projectile.rotation, new Vector2(30f, 15f) * scale);
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranPressurizedTerraWave : ModProjectile
{
    private bool Finisher => Projectile.ai[0] > 0.5f;
    private bool airburstsSpawned;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 28;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 500;
    }

    public override void SetDefaults()
    {
        Projectile.width = 46;
        Projectile.height = 46;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 100;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override void AI()
    {
        if (Projectile.localAI[1] == 0f)
        {
            Projectile.localAI[1] = 1f;
            Projectile.localAI[2] = Projectile.identity % 2 == 0 ? 1f : -1f;
            Projectile.scale = Finisher ? 0.82f : 1.34f;
            if (Finisher)
                Projectile.timeLeft = 78;
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * (Finisher ? 20f : 12.5f);
            SoundEngine.PlaySound(SoundID.Item60 with
                { Volume = Finisher ? 0.48f : 0.34f, Pitch = Finisher ? 0.32f : -0.08f }, Projectile.Center);
            FranParticleEffects.LineRelease(Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 26f,
                Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 72f,
                new Color(145, 225, 255), Finisher ? 9 : 13, Finisher ? 0.72f : 1.02f);
            FranSpectatorVfx.SpawnReleaseFan(Projectile.GetSource_FromThis(), Projectile.Center,
                Projectile.velocity, Projectile.owner, FranSpectatorPalette.Cyan, Finisher ? 4 : 2,
                Finisher ? 1.08f : 0.86f);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center,
                Finisher ? 2f : 1.2f);
        }

        float age = Projectile.localAI[0]++;
        Vector2 fallback = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        NPC target = FindTarget(760f);
        Vector2 aimDirection = fallback;
        if (target is not null)
        {
            Vector2 intercept = target.Center + target.velocity * (Finisher ? 3.2f : 5.2f);
            aimDirection = (intercept - Projectile.Center).SafeNormalize(fallback);
        }

        float acceleration = FranMotion.Smooth(FranMotion.Segment(age, 0f, Finisher ? 13f : 20f));
        float lateDrag = FranMotion.Smooth(FranMotion.Segment(age, Finisher ? 48f : 62f,
            Finisher ? 72f : 94f));
        float startSpeed = Finisher ? 20f : 12.5f;
        float peakSpeed = Finisher ? 34f : 25f;
        float speed = MathHelper.Lerp(startSpeed, peakSpeed, acceleration) * MathHelper.Lerp(1f, 0.66f, lateDrag);
        float openingCurve = Projectile.localAI[2] * MathHelper.Lerp(Finisher ? 0.12f : 0.22f, 0f,
            FranMotion.Smooth(FranMotion.Segment(age, 0f, Finisher ? 20f : 34f)));
        float weave = MathF.Sin(age * (Finisher ? 0.27f : 0.20f) + Projectile.identity * 0.71f) *
            (Finisher ? 0.045f : 0.085f);
        Vector2 desired = aimDirection.RotatedBy(openingCurve + weave) * speed;
        float steering = target is null ? 0.055f : MathHelper.Lerp(0.075f, 0.16f, acceleration);
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, steering);
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.Opacity = MathHelper.Clamp(Projectile.timeLeft / 15f, 0f, 1f);
        Lighting.AddLight(Projectile.Center, 0.15f, 0.52f, 0.62f);
        if (!Main.dedServ && Projectile.timeLeft % 3 == 0)
            FranParticleSystem.Spawn(new FranBladeWakeSliverParticle(Projectile.Center,
                -Projectile.velocity * 0.04f + Main.rand.NextVector2Circular(1.2f, 1.2f),
                Projectile.velocity.SafeNormalize(Vector2.UnitX), new Color(125, 235, 255),
                Main.rand.NextFloat(12f, 22f), Main.rand.NextFloat(2f, 4.5f), 14,
                Main.rand.NextFloat(-0.04f, 0.04f)));

        float detonationAge = Finisher ? 30f : 42f;
        if (age >= detonationAge && Main.myPlayer == Projectile.owner)
        {
            Vector2 airburstCenter = target?.Center + target.velocity * 1.5f ??
                Projectile.Center + Projectile.velocity * 1.8f;
            SpawnAirburstSequence(airburstCenter);
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float collisionPoint = 0f;
        float length = (Finisher ? 78f : 112f) * Projectile.scale;
        float width = (Finisher ? 32f : 45f) * Projectile.scale;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - direction * 22f * Projectile.scale,
            Projectile.Center + direction * length, width, ref collisionPoint);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner,
            FranBurstStyle.AirDraw, Finisher ? 0.52f : 0.68f);
        if (Main.myPlayer == Projectile.owner)
            SpawnAirburstSequence(target.Center + target.velocity * 1.5f);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (Main.myPlayer == Projectile.owner)
            SpawnAirburstSequence(Projectile.Center + oldVelocity.SafeNormalize(Vector2.UnitX) * 24f);
        return true;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float pulse = 0.94f + MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity) * 0.06f;
        float trailWidth = (Finisher ? 25f : 42f) * Projectile.scale;

        if (!FranProjectileTrailRenderer.TryDraw(Projectile, FranProjectileTrailProfile.TelekineticNeedle,
                new Color(8, 44, 66), new Color(65, 205, 255), Color.White, trailWidth,
                Projectile.Opacity * (Finisher ? 0.90f : 0.96f)))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.TelekineticNeedle,
                new Color(75, 210, 255), trailWidth * 1.35f, Projectile.Opacity);

        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 bodySize = new(Finisher ? 88f : 122f, Finisher ? 38f : 54f);
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center - direction * 5f,
            new Color(120, 225, 250) * (0.82f * Projectile.Opacity), Projectile.rotation,
            bodySize * Projectile.scale * pulse);
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center - direction * 8f,
            new Color(65, 195, 255) * (0.38f * Projectile.Opacity), Projectile.rotation,
            bodySize * Projectile.scale * 1.16f);
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 44f,
            new Color(125, 230, 255) * (0.46f * Projectile.Opacity), Projectile.rotation,
            new Vector2(Finisher ? 126f : 174f, Finisher ? 30f : 48f) * Projectile.scale);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center + direction * 27f,
            Color.White * (0.66f * Projectile.Opacity), Projectile.rotation,
            new Vector2(Finisher ? 44f : 60f, Finisher ? 24f : 34f) * Projectile.scale);
        FranVfx.DrawBloom(Projectile.Center, new Color(95, 225, 255), 66f * Projectile.scale,
            0.38f * Projectile.Opacity, !Finisher);
        FranVfx.EndAdditive();
        return false;
    }

    private void SpawnAirburstSequence(Vector2 center)
    {
        if (airburstsSpawned)
            return;
        airburstsSpawned = true;

        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
        int count = Finisher ? 5 : 3;
        float centerIndex = (count - 1) * 0.5f;
        float spacing = Finisher ? 38f : 54f;
        int burstDamage = Math.Max(1, (int)(Projectile.damage * (Finisher ? 0.28f : 0.42f)));
        for (int i = 0; i < count; i++)
        {
            float lane = i - centerIndex;
            float normalizedLane = centerIndex <= 0f ? 0f : MathF.Abs(lane) / centerIndex;
            float focus = 1f - normalizedLane;
            float waveOffset = MathF.Sin(Projectile.identity * 0.73f + i * 1.91f) * (Finisher ? 7f : 10f);
            Vector2 position = center + normal * (lane * spacing + waveOffset) +
                forward * (focus * (Finisher ? 28f : 36f) - normalizedLane * 14f);
            float scale = Finisher ? 0.74f + focus * 0.10f : 0.92f + focus * 0.12f;
            float delay = 6f + MathF.Abs(lane) * 3f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, forward,
                ModContent.ProjectileType<FranCompressionAirburst>(), burstDamage,
                Projectile.knockBack * 0.55f, Projectile.owner, delay, scale,
                Projectile.identity * 0.73f + i * 1.17f);
        }

        Projectile.netUpdate = true;
        Projectile.Kill();
    }

    private NPC FindTarget(float distance)
    {
        NPC best = null;
        float bestScore = distance * distance;
        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy())
                continue;
            Vector2 offset = npc.Center - Projectile.Center;
            float candidateDistance = offset.LengthSquared();
            if (candidateDistance >= distance * distance)
                continue;
            float alignment = Vector2.Dot(forward, offset.SafeNormalize(forward));
            if (alignment < -0.18f)
                continue;
            float score = candidateDistance * MathHelper.Lerp(1.45f, 0.72f,
                MathHelper.Clamp((alignment + 1f) * 0.5f, 0f, 1f));
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = npc;
        }
        return best;
    }
}

public sealed class FranCompressionAirburst : ModProjectile
{
    private const int Lifetime = 40;
    private float Age => Lifetime - Projectile.timeLeft;
    private float Delay => MathHelper.Clamp(Projectile.ai[0], 4f, 16f);
    private float BurstScale => MathHelper.Clamp(Projectile.ai[1], 0.55f, 1.25f);
    private float BlastRadius => 108f * BurstScale;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;

    public override void SetDefaults()
    {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Age >= Delay && Age <= Delay + 6f ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float expansion = FranMotion.Smooth(FranMotion.Segment(Age, Delay, Delay + 6f));
        float radius = MathHelper.Lerp(34f * BurstScale, BlastRadius, expansion);
        Vector2 closest = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
        return Vector2.DistanceSquared(Projectile.Center, closest) <= radius * radius;
    }

    public override void AI()
    {
        Projectile.rotation += 0.075f + BurstScale * 0.015f;
        if (Age < Delay)
        {
            if (!Main.dedServ && (int)Age % 2 == 0)
                FranParticleEffects.InwardSpark(Projectile.Center, new Color(105, 225, 255),
                    46f * BurstScale, 92f * BurstScale, 5.5f, 0.76f * BurstScale);
            Lighting.AddLight(Projectile.Center, 0.08f, 0.30f, 0.42f);
            return;
        }

        if (Projectile.localAI[0] == 0f)
        {
            Projectile.localAI[0] = 1f;
            SpawnBurstEffects();
        }

        float fade = 1f - FranMotion.Smooth(FranMotion.Segment(Age, Delay + 7f, Lifetime));
        Lighting.AddLight(Projectile.Center, 0.20f * fade, 0.62f * fade, 0.78f * fade);
    }

    private void SpawnBurstEffects()
    {
        SoundEngine.PlaySound(SoundID.Item14 with
            { Volume = 0.24f, Pitch = 0.22f + BurstScale * 0.12f, PitchVariance = 0.08f, MaxInstances = 4 },
            Projectile.Center);
        FranParticleEffects.Implosion(Projectile.Center, new Color(90, 215, 255), 0.52f * BurstScale);
        FranParticleEffects.CompressionSignature(Projectile.Center, Projectile.ai[2].ToRotationVector2(), BurstScale);
        FranVanillaDustEffects.Impact(Projectile.Center, Projectile.ai[2].ToRotationVector2(),
            FranSkill.PressurizedQuickdraw, new Color(90, 215, 255), 20, 0.82f * BurstScale);
        if (!Main.dedServ)
        {
            int rayCount = BurstScale >= 0.9f ? 18 : 14;
            float baseAngle = Projectile.ai[2];
            for (int i = 0; i < rayCount; i++)
            {
                Vector2 direction = (baseAngle + MathHelper.TwoPi * i / rayCount +
                    Main.rand.NextFloat(-0.08f, 0.08f)).ToRotationVector2();
                Color color = i % 4 == 0 ? Color.White : new Color(80, 205, 255);
                FranParticleSystem.Spawn(new FranLineParticle(Projectile.Center + direction * 5f,
                    direction * Main.rand.NextFloat(4.5f, 11f) * BurstScale, color,
                    Main.rand.NextFloat(38f, 78f) * BurstScale,
                    Main.rand.NextFloat(1.6f, 3.4f) * BurstScale, Main.rand.Next(12, 22), 0.90f));
                if (i % 3 == 0)
                    FranParticleSystem.Spawn(new FranBoltParticle(Projectile.Center + direction * 12f,
                        direction * Main.rand.NextFloat(2.5f, 6f), new Color(105, 230, 255),
                        Main.rand.NextFloat(0.16f, 0.28f) * BurstScale,
                        new Vector2(0.48f, 1.45f), Main.rand.Next(10, 17)));
            }
            for (int i = 0; i < 5; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(2.8f, 2.8f) - Vector2.UnitY * 0.6f;
                FranParticleSystem.Spawn(new FranHeavySmokeParticle(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 18f), velocity,
                    new Color(22, 60, 78), Main.rand.NextFloat(0.32f, 0.58f) * BurstScale,
                    Main.rand.Next(20, 32), 0.32f, true));
            }
        }
        FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_FromThis(), Projectile.Center,
            Projectile.ai[2].ToRotationVector2(), Projectile.owner, FranSpectatorPalette.Cyan,
            BurstScale >= 0.9f ? 5 : 3, 0.72f + BurstScale * 0.34f);
        Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 2.6f * BurstScale);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Color cyan = new(75, 205, 255);
        Color pale = new(175, 245, 255);
        float seed = Projectile.ai[2];
        if (Age < Delay)
        {
            float charge = FranMotion.Smooth(FranMotion.Segment(Age, 0f, Delay));
            float radius = MathHelper.Lerp(82f, 22f, charge) * BurstScale;
            FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
                new Color(95, 205, 235) * (0.42f + charge * 0.34f), Projectile.rotation * 0.55f,
                Vector2.One * MathHelper.Lerp(90f, 52f, charge) * BurstScale);
            FranVfx.BeginAdditive();
            FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
                pale * (0.18f + charge * 0.46f), -Projectile.rotation * 1.35f,
                Vector2.One * (radius * 1.55f));
            FranVfx.DrawSharedPixels("Cal_HighResFoggyCircleHardEdge", Projectile.Center,
                cyan * (0.10f + charge * 0.20f), Projectile.rotation,
                Vector2.One * radius * 1.42f);
            for (int i = 0; i < 6; i++)
            {
                float angle = seed + i * MathHelper.TwoPi / 6f + Projectile.rotation * 0.6f;
                Vector2 direction = angle.ToRotationVector2();
                Vector2 position = Projectile.Center + direction * radius * 0.62f;
                FranVfx.DrawSharedPixels("Cal_BloomLineFade", position, pale * (0.18f + charge * 0.30f),
                    direction.ToRotation() + MathHelper.PiOver2,
                    new Vector2(8f, 34f + charge * 24f) * BurstScale);
            }
            FranVfx.EndAdditive();
            return false;
        }

        float expansion = FranMotion.EaseOutCubic(FranMotion.Segment(Age, Delay, Delay + 14f));
        float fade = 1f - FranMotion.Smooth(FranMotion.Segment(Age, Delay + 8f, Lifetime));
        float shellRadius = MathHelper.Lerp(18f, BlastRadius * 1.18f, expansion);
        float coreFlash = 1f - FranMotion.Smooth(FranMotion.Segment(Age, Delay, Delay + 7f));
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            pale * (0.82f * coreFlash + 0.34f * fade), seed,
            new Vector2(MathHelper.Lerp(88f, BlastRadius * 2.05f, expansion),
                MathHelper.Lerp(58f, BlastRadius * 1.18f, expansion)));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * (0.94f * coreFlash), seed, new Vector2(136f, 74f) * BurstScale);
        FranVfx.DrawSharedPixels("Cal_HighResFoggyCircleHardEdge", Projectile.Center,
            cyan * (0.46f * fade), Projectile.rotation, Vector2.One * shellRadius * 2f);
        FranVfx.DrawRing(Projectile.Center, pale, shellRadius * 0.88f,
            0.72f * fade, Projectile.rotation * 1.7f, "Cal_BloomRing");
        FranVfx.DrawRing(Projectile.Center, Color.White, shellRadius * 0.52f,
            0.52f * fade, -Projectile.rotation * 2.1f, "Cal_FadedStarRing");
        for (int i = 0; i < 8; i++)
        {
            float angle = seed + i * MathHelper.TwoPi / 8f;
            Vector2 direction = angle.ToRotationVector2();
            float rayLength = MathHelper.Lerp(32f, 104f * BurstScale, expansion) *
                (i % 2 == 0 ? 1f : 0.72f);
            Vector2 position = Projectile.Center + direction * rayLength * 0.42f;
            FranVfx.DrawSharedPixels("Cal_BloomLineFade", position,
                (i % 3 == 0 ? Color.White : pale) * (0.52f * fade),
                direction.ToRotation() + MathHelper.PiOver2,
                new Vector2(10f + coreFlash * 5f, rayLength));
        }
        FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center, Color.White * (0.82f * coreFlash),
            seed, new Vector2(0.22f, 0.055f) * BurstScale);
        FranVfx.DrawBloom(Projectile.Center, pale, 38f + coreFlash * 34f,
            0.38f * fade + coreFlash * 0.38f, true);
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranThorsHammerDrop : ModProjectile
{
    private bool Impacted => Projectile.localAI[1] > 0f;
    private float Age => Projectile.localAI[0];
    private int TargetIndex => (int)Projectile.ai[2];

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.PaladinsHammerFriendly}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 16;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;
    }

    public override void SetDefaults()
    {
        Projectile.width = 74;
        Projectile.height = 74;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool? CanDamage() => Age >= 3f && !Impacted ? null : false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        Vector2 anchor = new(Projectile.ai[0], Projectile.ai[1]);
        NPC trackedTarget = GetTrackedTarget();
        Vector2 target = trackedTarget?.Center + trackedTarget.velocity * 1.2f ?? anchor;
        Vector2 fallback = Projectile.velocity.SafeNormalize(Vector2.UnitY);
        float speed = MathHelper.Lerp(30f, 68f, FranMotion.Smooth(FranMotion.Segment(age, 0f, 24f)));
        float corkscrew = MathF.Sin(age * 0.25f + Projectile.identity * 0.61f) *
            MathHelper.Lerp(0.13f, 0.018f, FranMotion.Smooth(FranMotion.Segment(age, 0f, 34f)));
        Vector2 desired = (target - Projectile.Center).SafeNormalize(fallback).RotatedBy(corkscrew) * speed;
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);
        Projectile.rotation += 0.42f * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);
        Lighting.AddLight(Projectile.Center, 0.38f, 0.68f, 1.18f);
        FranVanillaDustEffects.ProjectileTrail(Projectile.Center, Projectile.velocity,
            FranSkill.ThorsHammer, new Color(105, 185, 255), 0.36f, 1.34f);

        if (!Main.dedServ)
        {
            FranParticleSystem.Spawn(new FranBarrierShardParticle(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                -Projectile.velocity * 0.075f + Main.rand.NextVector2Circular(2.1f, 2.1f),
                new Color(120, 205, 255), new Vector2(Main.rand.NextFloat(16f, 31f), Main.rand.NextFloat(3f, 7f)), 17));
            FranParticleSystem.Spawn(new FranLightParticle(
                Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                -Projectile.velocity * Main.rand.NextFloat(0.055f, 0.11f) + Main.rand.NextVector2Circular(1.4f, 1.4f),
                Main.rand.NextFloat(0.44f, 0.76f), Main.rand.NextBool(3) ? Color.White : new Color(105, 185, 255),
                Main.rand.Next(15, 24), 0.88f, 1.35f, 3.2f));
            if ((int)age % 2 == 0)
                FranParticleSystem.Spawn(new FranGlowOrbParticle(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 18f),
                    -Projectile.velocity * 0.045f + Main.rand.NextVector2Circular(1f, 1f),
                    new Color(105, 185, 255), Main.rand.NextFloat(0.20f, 0.34f), 0.03f,
                    Main.rand.Next(13, 20)));
            if ((int)age % 3 == 0)
                FranParticleSystem.Spawn(new FranHeavySmokeParticle(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    -Projectile.velocity * 0.045f, new Color(28, 44, 82), Main.rand.NextFloat(0.52f, 0.86f), 27,
                    0.56f, true));
        }

        Vector2 remaining = target - Projectile.Center;
        bool passedTarget = age >= 3f && Vector2.Dot(remaining, Projectile.velocity) <= 0f;
        if (remaining.LengthSquared() <= 42f * 42f || passedTarget || age >= 46f)
            Impact(target);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Impact(target.Center);

    private void Impact(Vector2 center)
    {
        if (Impacted)
            return;
        Projectile.localAI[1] = 1f;
        Projectile.Center = center;
        if (Main.myPlayer == Projectile.owner)
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), center, Vector2.Zero,
                ModContent.ProjectileType<ThorsHammerBolt>(), Math.Max(1, (int)(Projectile.damage * 0.88f)),
                Projectile.knockBack, Projectile.owner);
        Projectile.netUpdate = true;
        Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[ProjectileID.PaladinsHammerFriendly].Value;
        Vector2 origin = texture.Size() * 0.5f;
        if (!FranProjectileTrailRenderer.TryDraw(Projectile, FranProjectileTrailProfile.HeavyHammer,
                new Color(8, 24, 64), new Color(80, 165, 255), Color.White, 38f, 0.94f))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.HeavyHammer,
                new Color(80, 165, 255), 44f, 0.92f);
        FranVfx.BeginAdditive();
        Vector2 fallDirection = Projectile.velocity.SafeNormalize(Vector2.UnitY);
        Vector2 lightningRear = Projectile.Center - fallDirection * 176f;
        FranVfx.DrawVertical("Cal_BloomLineSoftEdge", lightningRear,
            Projectile.Center, new Color(95, 175, 255) * 0.66f, 48f);
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - fallDirection * 42f,
            new Color(105, 185, 255) * 0.62f, fallDirection.ToRotation(), new Vector2(186f, 60f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            new Color(195, 238, 255) * 0.82f, fallDirection.ToRotation(), new Vector2(104f, 56f));
        FranVfx.DrawTexturedLightning(lightningRear, Projectile.Center, new Color(125, 225, 255),
            7.5f, 8, Projectile.identity * 0.73f + Main.GlobalTimeWrappedHourly * 21f, 0.78f);
        FranVfx.DrawBloom(Projectile.Center, new Color(95, 175, 255), 76f, 0.72f, true);
        FranVfx.EndAdditive();

        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White,
            Projectile.rotation, origin, 1.05f, SpriteEffects.None, 0f);
        return false;
    }

    private NPC GetTrackedTarget()
    {
        if (!Main.npc.IndexInRange(TargetIndex))
            return null;
        NPC target = Main.npc[TargetIndex];
        return target.active && !target.friendly && target.life > 0 ? target : null;
    }
}
