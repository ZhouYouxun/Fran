using System;
using FranMod.Content.Companions;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

internal static class FranPlanetVisuals
{
    private const string Root = "Fran/Textures/kenney_planets/Planets/planet";

    public static string TexturePath(int variant) => $"{Root}{Math.Clamp(variant, 0, 9):D2}";

    public static Color Palette(int variant) => Math.Clamp(variant, 0, 9) switch
    {
        0 => new Color(104, 226, 222),
        1 => new Color(150, 125, 245),
        2 => new Color(245, 101, 55),
        3 => new Color(113, 211, 174),
        4 => new Color(105, 139, 183),
        5 => new Color(215, 165, 121),
        6 => new Color(176, 104, 137),
        7 => new Color(83, 204, 238),
        8 => new Color(245, 91, 43),
        _ => new Color(171, 91, 245)
    };

}

/// <summary>
/// 六体念动力引力坠落：星体高速入轨、稳定公转，再维持角速度螺旋坠向锁定目标。
/// </summary>
public sealed class Telekinetic_GravityStarfall : ModProjectile
{
    // 蓄力附加层暂时关闭；代码保留，便于以后单独恢复。
    private static readonly bool ChargeVisualsEnabled = false;
    internal const int PlanetCount = 6;
    private const int OrbitIngressTicks = 42;
    private const int StableOrbitTicks = 18;
    private const int CollapseStaggerTicks = 3;
    private const int SpiralCollapseTicks = 30;
    private const int FadeOutTicks = 12;
    private const float InitialRadiusOffset = 200f;
    private const float StableAngularSpeed = 0.042f;
    private const float InitialAngularBoost = 0.18f;
    private int EncodedAppearance => Math.Max(0, (int)Projectile.ai[0]);
    private int Variant => EncodedAppearance % 10;
    private int OrbitSlot => Math.Clamp(EncodedAppearance / 10, 0, PlanetCount - 1);
    private ref float EncodedTarget => ref Projectile.ai[1];
    private ref float StateTimer => ref Projectile.ai[2];
    private bool FadingOut => StateTimer < 0f;
    private float AgeTicks => Math.Max(0f, StateTimer);
    private float SpiralStartTick => OrbitIngressTicks + StableOrbitTicks + OrbitSlot * CollapseStaggerTicks;
    private bool Spiraling => !FadingOut && AgeTicks >= SpiralStartTick;
    private ref float FadeOutTimer => ref Projectile.localAI[1];
    private float Diameter => 54f + (uint)Projectile.identity % 17u;
    private float Spin => (Projectile.identity % 2 == 0 ? 1f : -1f) *
        (0.035f + (uint)Projectile.identity % 9u * 0.004f);
    private Vector2 lastFocus;
    private bool focusInitialized;
    private float lockedOrbitRadius;
    private bool orbitRadiusInitialized;

    public override string Texture => FranPlanetVisuals.TexturePath(0);

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;
    }

    public override void SetDefaults()
    {
        Projectile.width = 38;
        Projectile.height = 38;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 600;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.extraUpdates = 0;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;

    public override bool? CanDamage() => Spiraling ? null : false;

    public override bool? CanHitNPC(NPC target)
    {
        NPC lockedTarget = ResolveTarget();
        return Spiraling && lockedTarget is not null && target.whoAmI == lockedTarget.whoAmI
            ? null
            : false;
    }

    public override void AI()
    {
        if (FadingOut)
        {
            UpdateFadeOut();
            return;
        }

        StateTimer++;
        UpdateOrbitalMotion();
    }

    internal static int EncodeAppearance(int variant, int orbitSlot) =>
        Math.Clamp(variant, 0, 9) + Math.Clamp(orbitSlot, 0, PlanetCount - 1) * 10;

    internal static Vector2 InitialOrbitPosition(Vector2 focus, NPC target, int owner, int orbitSlot)
    {
        float phase = InitialOrbitPhase(owner, target?.whoAmI ?? -1, orbitSlot);
        float radius = StableOrbitRadius(target) + InitialRadiusOffset;
        return focus + phase.ToRotationVector2() * radius;
    }

    private void UpdateOrbitalMotion()
    {
        Player owner = Main.player[Projectile.owner];
        NPC target = ResolveTarget();
        Vector2 focus = ResolveFocus(owner, target);
        if (!orbitRadiusInitialized)
        {
            lockedOrbitRadius = StableOrbitRadius(target);
            orbitRadiusInitialized = true;
        }
        float stableRadius = lockedOrbitRadius;
        float radius;
        if (AgeTicks < OrbitIngressTicks)
        {
            float ingress = MathHelper.Clamp(AgeTicks / OrbitIngressTicks, 0f, 1f);
            radius = MathHelper.Lerp(stableRadius + InitialRadiusOffset, stableRadius,
                MathHelper.SmoothStep(0f, 1f, ingress));
        }
        else if (!Spiraling)
        {
            radius = stableRadius;
        }
        else
        {
            float collapse = MathHelper.Clamp((AgeTicks - SpiralStartTick) / SpiralCollapseTicks, 0f, 1f);
            float gravitationalFall = collapse * collapse;
            radius = MathHelper.Lerp(stableRadius, 0f, gravitationalFall);
        }

        float angle = CalculateOrbitAngle(AgeTicks);
        Vector2 previousCenter = Projectile.Center;
        Projectile.Center = focus + angle.ToRotationVector2() * radius;
        Projectile.velocity = Projectile.Center - previousCenter;
        Projectile.rotation += Spin;
        Projectile.Opacity = MathHelper.SmoothStep(0f, 1f,
            MathHelper.Clamp(AgeTicks / 14f, 0f, 1f));

        if (ChargeVisualsEnabled && !Spiraling)
            SpawnChargeParticles();
        if (Spiraling)
            UpdateSpiralEffects();

        if (AgeTicks >= SpiralStartTick + SpiralCollapseTicks)
        {
            SpawnImpactExplosion(Projectile.velocity);
            BeginFadeOut();
        }
    }

    private float CalculateOrbitAngle(float age)
    {
        float decelerationAge = Math.Min(age, OrbitIngressTicks);
        float boostAngle = InitialAngularBoost *
            (decelerationAge - decelerationAge * decelerationAge / (2f * OrbitIngressTicks));
        return InitialOrbitPhase(Projectile.owner, (int)EncodedTarget - 1, OrbitSlot) +
            StableAngularSpeed * age + boostAngle;
    }

    private Vector2 ResolveFocus(Player owner, NPC target)
    {
        if (target is not null)
        {
            lastFocus = target.Center;
            focusInitialized = true;
            return lastFocus;
        }

        if (!focusInitialized)
        {
            float phase = InitialOrbitPhase(Projectile.owner, (int)EncodedTarget - 1, OrbitSlot);
            lastFocus = Projectile.Center - phase.ToRotationVector2() *
                (StableOrbitRadius(null) + InitialRadiusOffset);
            if (!float.IsFinite(lastFocus.X) || !float.IsFinite(lastFocus.Y))
                lastFocus = GetFallbackAim(owner);
            focusInitialized = true;
        }
        return lastFocus;
    }

    private static float InitialOrbitPhase(int owner, int targetIndex, int orbitSlot) =>
        owner * 0.61f + Math.Max(0, targetIndex) * 0.37f +
        MathHelper.TwoPi * Math.Clamp(orbitSlot, 0, PlanetCount - 1) / PlanetCount;

    private static float StableOrbitRadius(NPC target)
    {
        float targetExtent = target is null
            ? 40f
            : Math.Max(target.width, target.height) * 0.5f;
        return MathHelper.Clamp(targetExtent + 150f, 200f, 620f);
    }

    private void SpawnChargeParticles()
    {
        Color color = FranPlanetVisuals.Palette(Variant);
        float charge = MathHelper.Clamp(AgeTicks / OrbitIngressTicks, 0f, 1f);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * (0.20f + charge * 0.54f));
        if (Main.dedServ)
            return;

        int timer = (int)AgeTicks;
        if (timer % 3 == 0)
        {
            float phase = Projectile.identity * 0.71f + timer * 1.37f;
            Vector2 radial = phase.ToRotationVector2();
            Vector2 position = Projectile.Center + radial * MathHelper.Lerp(42f, 13f, charge);
            FranParticleSystem.Spawn(new FranGlowOrbParticle(position,
                -radial * MathHelper.Lerp(1.2f, 3.6f, charge), color,
                0.16f + charge * 0.13f, 0.025f, 13));
        }
        if (timer % 6 == 0)
            FranParticleSystem.Spawn(new FranSkillGlyphParticle(Projectile.Center,
                Vector2.Zero, Color.Lerp(color, Color.White, 0.25f),
                0.30f + charge * 0.24f, 12));
    }

    private void UpdateSpiralEffects()
    {
        Projectile.Opacity = 1f;
        Color color = FranPlanetVisuals.Palette(Variant);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * 0.78f);

        if (Main.dedServ || (int)AgeTicks % 2 != 0)
            return;

        float collapse = MathHelper.Clamp((AgeTicks - SpiralStartTick) / SpiralCollapseTicks, 0f, 1f);
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitY);
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        Vector2 offset = normal * MathF.Sin(AgeTicks * 0.7f + OrbitSlot) *
            MathHelper.Lerp(12f, 38f, collapse) * Projectile.scale;
        for (int side = -1; side <= 1; side += 2)
        {
            FranParticleSystem.Spawn(new FranSmallBloomParticle(Projectile.Center + offset * side,
                -Projectile.velocity * 0.45f, color, 22f * Projectile.scale,
                4f * Projectile.scale, 10));
        }
    }

    private NPC ResolveTarget()
    {
        int targetIndex = (int)EncodedTarget - 1;
        return Main.npc.IndexInRange(targetIndex) && Main.npc[targetIndex].active &&
               Main.npc[targetIndex].CanBeChasedBy()
            ? Main.npc[targetIndex]
            : null;
    }

    private static Vector2 GetFallbackAim(Player owner)
    {
        Vector2 aim = owner.whoAmI == Main.myPlayer ? FranCompanionContext.Aim(owner) : owner.Center + Vector2.UnitY * 720f;
        Vector2 offset = aim - owner.MountedCenter;
        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y))
            return owner.Center + Vector2.UnitY * 720f;
        if (offset.LengthSquared() > 2400f * 2400f)
            offset = offset.SafeNormalize(Vector2.UnitY) * 2400f;
        return owner.MountedCenter + offset;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (FadingOut)
            return;
        SpawnImpactExplosion(Projectile.velocity);
        BeginFadeOut();
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float nearestX = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
        float nearestY = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
        float radius = 80f * Projectile.scale;
        return Vector2.DistanceSquared(Projectile.Center, new Vector2(nearestX, nearestY)) <= radius * radius;
    }

    private void SpawnImpactExplosion(Vector2 impactVelocity)
    {
        if (Projectile.owner == Main.myPlayer)
        {
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<Telekinetic_GravityStarfallExplosion>(),
                Math.Max(1, (int)(Projectile.damage * FranBalance.GetFloat(
                    nameof(FranBalance.TelekineticPlanetExplosionDamageMultiplier),
                    FranBalance.TelekineticPlanetExplosionDamageMultiplier))), Projectile.knockBack,
                Projectile.owner, Variant, Diameter * 1.35f,
                impactVelocity.SafeNormalize(Vector2.UnitY).ToRotation());
        }
    }

    private void BeginFadeOut()
    {
        if (FadingOut)
            return;

        StateTimer = -1f;
        FadeOutTimer = 0f;
        Projectile.penetrate = -1;
        Projectile.friendly = false;
        Projectile.tileCollide = false;
        Projectile.velocity *= 0.18f;
        Projectile.timeLeft = FadeOutTicks + 2;
        Projectile.netUpdate = true;
    }

    private void UpdateFadeOut()
    {
        if (Projectile.numUpdates == 0)
        {
            FadeOutTimer++;
            Projectile.Center += Projectile.velocity;
            Projectile.velocity *= 0.72f;
        }

        float completion = MathHelper.Clamp(FadeOutTimer / FadeOutTicks, 0f, 1f);
        Projectile.Opacity = 1f - MathHelper.SmoothStep(0f, 1f, completion);
        Color color = FranPlanetVisuals.Palette(Variant);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * (0.46f * Projectile.Opacity));
        if (completion >= 1f)
            Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = ModContent.Request<Texture2D>(FranPlanetVisuals.TexturePath(Variant)).Value;
        Color color = FranPlanetVisuals.Palette(Variant);
        float scale = Diameter / Math.Max(texture.Width, texture.Height) * Projectile.scale;
        float charge = MathHelper.Clamp(AgeTicks / OrbitIngressTicks, 0f, 1f);
        Vector2 visualCenter = Projectile.Center;
        if (!Spiraling && !FadingOut)
            visualCenter += Vector2.UnitY * MathF.Sin(AgeTicks * 0.22f + Projectile.identity) * 4f;

        if (ChargeVisualsEnabled && !Spiraling && !FadingOut)
        {
            FranVfx.BeginAdditive();
            float chargePower = charge * charge * charge;
            FranVfx.DrawBloom(visualCenter, color,
                MathHelper.Lerp(Diameter * 0.74f, Diameter * 2.8f, chargePower),
                (0.20f + chargePower * 0.42f) * Projectile.Opacity);
            FranVfx.DrawBloom(visualCenter, Color.White,
                MathHelper.Lerp(Diameter * 0.32f, Diameter * 1.15f, chargePower),
                (0.12f + chargePower * 0.34f) * Projectile.Opacity);
            FranVfx.DrawRing(visualCenter, Color.Lerp(color, Color.White, 0.34f),
                MathHelper.Lerp(Diameter * 0.90f, Diameter * 0.56f, charge),
                (0.18f + charge * 0.28f) * Projectile.Opacity,
                -Projectile.rotation * 1.6f, "BloomRing");
            FranVfx.EndAdditive();
        }
        else if (Spiraling)
        {
            FranVfx.BeginAdditive();
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 8; i++)
            {
                float layerFade = (1f - i / 8f) * Projectile.Opacity;
                FranVfx.DrawOriginalPixels("FranSmearForward",
                    Projectile.Center - direction * (18f + i * 9f), color * (0.26f * layerFade),
                    direction.ToRotation(), new Vector2(92f + i * 16f, 30f - i * 1.8f) * Projectile.scale);
            }
            FranVfx.DrawBloom(Projectile.Center, color, Diameter * 1.22f,
                0.46f * Projectile.Opacity);
            FranVfx.EndAdditive();
        }

        Main.EntitySpriteDraw(texture, visualCenter - Main.screenPosition, null,
            Color.White * Projectile.Opacity, Projectile.rotation, texture.Size() * 0.5f,
            scale, SpriteEffects.None, 0f);
        return false;
    }
}

/// <summary>Short-lived synchronized area hit and coloured impact for a telekinetic planet.</summary>
public sealed class Telekinetic_GravityStarfallExplosion : ModProjectile
{
    private const int Lifetime = 12;
    private int Variant => Math.Clamp((int)Projectile.ai[0], 0, 9);
    private float Diameter => MathHelper.Clamp(Projectile.ai[1], 64f, 118f);
    private float ImpactRotation => Projectile.ai[2];
    private float Progress => 1f - Projectile.timeLeft / (float)Lifetime;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 160;
        Projectile.height = 160;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
        Projectile.netImportant = true;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.timeLeft >= Lifetime - 3 ? null : false;

    public override void OnSpawn(IEntitySource source)
    {
        if (Main.dedServ)
            return;

        Color color = FranPlanetVisuals.Palette(Variant);
        Vector2 impactDirection = ImpactRotation.ToRotationVector2();
        Vector2 impactNormal = impactDirection.RotatedBy(MathHelper.PiOver2);
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.62f, Pitch = -0.10f }, Projectile.Center);
        SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.44f, Pitch = 0.16f }, Projectile.Center);
        FranParticleEffects.BloomPulse(Projectile.Center, color, 32f, 196f, 18, 0.96f, true);
        FranParticleEffects.BloomPulse(Projectile.Center, Color.White, 20f, 126f, 15, 0.94f, true);
        FranParticleEffects.RingPulse(Projectile.Center, Color.Lerp(color, Color.White, 0.28f),
            new Vector2(24f, 40f), new Vector2(214f, 72f), 18,
            ImpactRotation, 0.012f, 0.88f);
        FranParticleEffects.RingPulse(Projectile.Center, Color.White,
            new Vector2(14f, 25f), new Vector2(142f, 44f), 13,
            ImpactRotation, -0.010f, 0.76f);
        FranParticleEffects.LineRelease(Projectile.Center - impactDirection * 72f,
            Projectile.Center + impactDirection * 128f, Color.Lerp(color, Color.White, 0.62f), 18, 1.34f);

        float phase = Projectile.identity * 0.618f + Variant * 0.37f;
        const int moteCount = 24;
        for (int i = 0; i < moteCount; i++)
        {
            float angle = phase + MathHelper.TwoPi * i / moteCount + (i % 3 - 1) * 0.035f;
            Vector2 radial = angle.ToRotationVector2();
            Vector2 velocity = radial * (4.8f + i % 4 * 1.55f) +
                impactDirection * (i % 2 == 0 ? 2.8f : -0.8f);
            Color moteColor = Color.Lerp(color, Color.White, 0.12f + i % 4 * 0.10f);
            if (i % 3 == 0)
                FranParticleSystem.Spawn(new FranSmallBloomParticle(Projectile.Center, velocity,
                    moteColor, 17f + i % 4 * 2f, 3f, 16 + i % 5));
            else if (i % 3 == 1)
                FranParticleSystem.Spawn(new FranLightParticle(Projectile.Center + radial * 6f, velocity,
                    0.30f + i % 4 * 0.035f, moteColor, 16 + i % 5,
                    0.88f, 1.35f, 3.2f));
            else
                FranParticleSystem.Spawn(new FranSparkParticle(Projectile.Center, velocity, moteColor,
                    0.21f + i % 4 * 0.025f, new Vector2(0.34f, 1.08f), 17 + i % 5, 0.91f));
        }

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector2 velocity = impactDirection * (5.8f + i * 2.4f) +
                    impactNormal * side * (1.4f + i * 0.85f);
                FranParticleSystem.Spawn(new FranHalleySparkParticle(
                    Projectile.Center + impactNormal * side * (5f + i * 2f), velocity,
                    i == 0 ? Color.White : color, 1.5f - i * 0.12f, 12 + i));
            }
        }

        for (int i = 0; i < 18; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(5.5f, 18f);
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.FireworksRGB, velocity,
                0, Color.Lerp(color, Color.White, Main.rand.NextFloat(0.28f, 0.72f)),
                Main.rand.NextFloat(0.75f, 1.18f));
            dust.noGravity = true;
        }

        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 4.6f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Color color = FranPlanetVisuals.Palette(Variant);
        float fade = MathF.Pow(1f - Progress, 1.25f);
        float radius = MathHelper.Lerp(Diameter * 0.58f, Diameter * 1.95f, Progress);
        Vector2 impactDirection = ImpactRotation.ToRotationVector2();
        FranVfx.BeginAdditive();
        FranVfx.DrawBloom(Projectile.Center, color, radius, 0.64f * fade);
        FranVfx.DrawBloom(Projectile.Center, Color.White, radius * 0.52f, 0.70f * fade);
        FranVfx.DrawRing(Projectile.Center, Color.Lerp(color, Color.White, 0.42f), radius,
            0.72f * fade, ImpactRotation + Progress * 0.34f, "BloomRing");
        FranVfx.DrawOriginalPixels("FranSmearForward",
            Projectile.Center - impactDirection * MathHelper.Lerp(8f, 44f, Progress),
            color * (0.52f * fade), ImpactRotation,
            new Vector2(MathHelper.Lerp(92f, 218f, Progress),
                MathHelper.Lerp(46f, 18f, Progress)));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * (0.86f * fade), ImpactRotation,
            Vector2.One * MathHelper.Lerp(62f, 126f, Progress));
        FranVfx.EndAdditive();
        return false;
    }
}
