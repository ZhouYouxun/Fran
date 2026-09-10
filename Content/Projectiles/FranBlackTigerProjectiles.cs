using System;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// Shared non-damaging impact layer for ultimate blade and rift-laser hits. Style 0 is compact,
// 1 is a normal execution hit, and 2 is the finisher.
public sealed class FranBlackTigerImpactVisual : ModProjectile
{
    private const int Lifetime = 26;

    private int Age => Lifetime - Projectile.timeLeft;
    private int ImpactStyle => Math.Clamp((int)Projectile.ai[1], 0, 2);
    private Vector2 Axis => Projectile.ai[0].ToRotationVector2().SafeNormalize(Vector2.UnitX);
    private float ImpactScale => ImpactStyle switch
    {
        0 => 0.72f,
        1 => 1f,
        _ => 1.55f
    };

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public static void Spawn(IEntitySource source, Vector2 center, Vector2 direction, int owner, int style)
    {
        direction = direction.SafeNormalize(Vector2.UnitX);
        Projectile.NewProjectile(source, center, Vector2.Zero, ModContent.ProjectileType<FranBlackTigerImpactVisual>(),
            0, 0f, owner, direction.ToRotation(), Math.Clamp(style, 0, 2), Main.rand.NextFloat(MathHelper.TwoPi));
    }

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        Lighting.AddLight(Projectile.Center, 0.055f * ImpactScale, 0.018f, 0.09f * ImpactScale);
        if (Main.dedServ || Projectile.localAI[0]++ != 0f)
            return;

        int smokeCount = ImpactStyle switch { 0 => 3, 1 => 6, _ => 10 };
        int blackNeedleCount = ImpactStyle switch { 0 => 5, 1 => 8, _ => 14 };
        Vector2 normal = Axis.RotatedBy(MathHelper.PiOver2);
        for (int i = 0; i < smokeCount; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.9f, 4.8f) * ImpactScale;
            FranParticleSystem.Spawn(new FranHeavySmokeParticle(
                Projectile.Center + Main.rand.NextVector2Circular(12f, 12f) * ImpactScale,
                velocity, new Color(1, 0, 4), Main.rand.NextFloat(0.34f, 0.62f) * ImpactScale,
                Main.rand.Next(24, 38), 0.92f, false, FranParticleLayer.AfterProjectiles));
        }
        for (int i = 0; i < blackNeedleCount; i++)
        {
            float lane = i - (blackNeedleCount - 1f) * 0.5f;
            Vector2 direction = Axis.RotatedBy(lane * (ImpactStyle == 2 ? 0.10f : 0.14f));
            Vector2 position = Projectile.Center + normal * lane * 4.5f * ImpactScale;
            FranParticleSystem.Spawn(new FranNeedleParticle(position,
                direction * Main.rand.NextFloat(2.5f, 7.5f) * ImpactScale,
                new Color(0, 0, 0), new Vector2(48f, 9f) * ImpactScale,
                new Vector2(10f, 2f) * ImpactScale, Main.rand.Next(12, 20),
                direction.ToRotation(), 0.90f, 0f, additive: false));
        }
        int coreCount = ImpactStyle + 2;
        for (int i = 0; i < coreCount; i++)
            FranParticleSystem.Spawn(new FranBlackLightCoreParticle(
                Projectile.Center + normal * Main.rand.NextFloat(-18f, 18f) * ImpactScale,
                Main.rand.NextVector2Circular(0.7f, 0.7f), 16f * ImpactScale,
                Main.rand.NextFloat(72f, 116f) * ImpactScale, Main.rand.Next(18, 28),
                0.78f, Projectile.ai[2] + i * 0.71f, Main.rand.NextFloat(-0.045f, 0.045f)));
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float progress = Age / (float)Lifetime;
        float opacity = MathHelper.Clamp(MathF.Sin(progress * MathHelper.Pi) * 1.38f, 0f, 1f);
        float expansion = 1f - MathF.Pow(1f - progress, 3f);
        float scale = ImpactScale;
        Vector2 normal = Axis.RotatedBy(MathHelper.PiOver2);
        float halfLength = MathHelper.Lerp(38f, ImpactStyle == 2 ? 210f : 118f, expansion) * scale;
        float width = MathHelper.Lerp(28f, ImpactStyle == 2 ? 112f : 68f, expansion) * scale;

        FranVfx.DrawVertical("TraceSoft", Projectile.Center - Axis * halfLength,
            Projectile.Center + Axis * halfLength, new Color(0, 0, 0, 248) * (opacity * 0.92f), width);
        if (ImpactStyle == 2)
            FranVfx.DrawVertical("TraceSoft", Projectile.Center - normal * halfLength * 0.72f,
                Projectile.Center + normal * halfLength * 0.72f,
                new Color(0, 0, 0, 244) * (opacity * 0.78f), width * 0.68f);
        FranVfx.DrawSharedPixels("HighResFoggyCircleHardEdge", Projectile.Center,
            new Color(0, 0, 0, 246) * (opacity * 0.90f), Projectile.ai[2] + progress,
            Vector2.One * MathHelper.Lerp(42f, ImpactStyle == 2 ? 196f : 116f, expansion) * scale);

        int lobeCount = ImpactStyle == 2 ? 12 : 8;
        float lobeRadius = MathHelper.Lerp(8f, ImpactStyle == 2 ? 62f : 34f, expansion) * scale;
        for (int i = 0; i < lobeCount; i++)
        {
            Vector2 radial = (Projectile.ai[2] + MathHelper.TwoPi * i / lobeCount).ToRotationVector2();
            Vector2 position = Projectile.Center + radial * lobeRadius;
            FranVfx.DrawSharedPixels("WaterFlavored", position,
                new Color(0, 0, 0, 244) * (opacity * 0.66f),
                radial.ToRotation() - MathHelper.PiOver2,
                new Vector2(22f, MathHelper.Lerp(48f, 92f, expansion)) * scale);
        }
        if (ImpactStyle >= 1)
            FranVfx.DrawSharedPixels("FranBlackTigerDash", Projectile.Center,
                new Color(1, 0, 4, 238) * (opacity * 0.52f),
                Axis.ToRotation() - MathHelper.PiOver2,
                Vector2.One * (ImpactStyle == 2 ? 194f : 126f) * scale);

        FranVfx.BeginAdditive();
        Color violet = ImpactStyle == 2 ? new Color(181, 102, 236) : new Color(112, 54, 177);
        FranVfx.DrawRing(Projectile.Center, violet,
            MathHelper.Lerp(18f, ImpactStyle == 2 ? 176f : 92f, expansion) * scale,
            opacity * 0.58f, Projectile.ai[2], "FranVfx_BloomRing");
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            violet * (opacity * 0.42f), Axis.ToRotation(),
            new Vector2(Math.Min(500f, halfLength * 2.15f), width * 1.12f));
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center + Axis * halfLength * 0.28f,
            new Color(203, 178, 244) * (opacity * 0.22f), Axis.ToRotation(),
            new Vector2(halfLength * 1.55f, Math.Max(5f, width * 0.12f)));
        if (ImpactStyle == 2)
            FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
                new Color(85, 132, 220) * (opacity * 0.24f), normal.ToRotation(),
                new Vector2(halfLength * 1.54f, width * 0.82f));
        FranVfx.EndAdditive();
        return false;
    }
}
