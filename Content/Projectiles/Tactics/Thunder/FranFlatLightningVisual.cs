using System;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

internal enum FranFlatLightningPalette
{
    ThunderChain,
    ThorsHammer
}

// 雷电本体不绘制贴图。它以高更新率扫过路径，并用本模组的亮色粒子留下短促电迹。
internal static class FranFlatLightningEffects
{
    private const int UpdatesPerTick = 11;
    private const float GoldenAngle = 2.39996323f;

    public static void SpawnSegment(IEntitySource source, Vector2 start, Vector2 end, int owner,
        float intensity, FranFlatLightningPalette palette, float travelTicks)
    {
        if (Main.netMode == NetmodeID.Server || owner != Main.myPlayer)
            return;

        Vector2 offset = end - start;
        float distance = offset.Length();
        if (!float.IsFinite(distance) || distance < 2f)
            return;

        float step = distance / (Math.Max(0.5f, travelTicks) * UpdatesPerTick);
        Projectile.NewProjectile(source, start, offset.SafeNormalize(Vector2.UnitY) * step,
            ModContent.ProjectileType<FranFlatLightningVisual>(), 0, 0f, owner,
            distance, MathHelper.Clamp(intensity, 0.35f, 2.4f), (float)palette);
    }

    public static void EmitTravel(Vector2 center, Vector2 movement, float intensity,
        FranFlatLightningPalette palette, int sampleCount, int phase)
    {
        if (Main.dedServ)
            return;

        Vector2 direction = movement.SafeNormalize(Vector2.UnitX);
        float length = MathHelper.Clamp(movement.Length(), 2f, 42f);
        int count = Math.Clamp(sampleCount, 1, 5);
        for (int i = 0; i < count; i++)
        {
            float completion = count == 1 ? 0f : i / (float)(count - 1);
            Vector2 point = center - direction * length * completion;
            EmitPoint(point, direction, intensity, palette, phase * count + i);
        }
    }

    public static void EmitPoint(Vector2 center, Vector2 direction, float intensity,
        FranFlatLightningPalette palette, int phase)
    {
        if (Main.dedServ)
            return;

        direction = direction.SafeNormalize(Vector2.UnitY);
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        float oscillation = MathF.Sin(phase * GoldenAngle);
        Color body = palette == FranFlatLightningPalette.ThorsHammer
            ? new Color(92, 196, 255)
            : new Color(142, 126, 255);
        Color edge = palette == FranFlatLightningPalette.ThorsHammer
            ? new Color(174, 241, 255)
            : new Color(105, 220, 255);
        Color color = phase % 5 == 0 ? Color.White : Color.Lerp(body, edge, 0.5f + oscillation * 0.22f);
        Vector2 offset = normal * oscillation * 3.4f * intensity;
        Vector2 drift = -direction * (0.28f + intensity * 0.16f) + normal * oscillation * 0.36f;

        FranParticleSystem.Spawn(new FranSparkParticle(center + offset, drift, color,
            0.13f * intensity, new Vector2(0.42f, 1.75f), 9, 0.93f));

        if ((phase & 1) == 0)
            FranParticleSystem.Spawn(new FranBoltParticle(center - offset * 0.45f,
                -direction * 0.46f + normal * oscillation * 0.22f, color,
                0.16f * intensity, new Vector2(0.46f, 1.48f), 9));

        if (phase % 3 == 0)
            FranParticleSystem.Spawn(new FranLineParticle(center, drift * 1.2f,
                Color.Lerp(color, Color.White, 0.38f), 34f * intensity, 1.35f * intensity, 8, 0.90f));

        if (phase % 7 == 0)
            FranParticleSystem.Spawn(new FranSmallBloomParticle(center, Vector2.Zero, edge,
                15f * intensity, 3f, 8));
    }
}

public sealed class FranFlatLightningVisual : ModProjectile
{
    private float PathLength => Math.Max(1f, Projectile.ai[0]);
    private float Intensity => MathHelper.Clamp(Projectile.ai[1], 0.35f, 2.4f);
    private FranFlatLightningPalette Palette => (FranFlatLightningPalette)Math.Clamp((int)Projectile.ai[2], 0, 1);

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 240;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.extraUpdates = 10;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitY);
        float remaining = PathLength - Projectile.localAI[0];
        float step = Math.Min(Math.Max(1f, Projectile.velocity.Length()), remaining);
        if (remaining <= 0.01f || step <= 0f)
        {
            Projectile.Kill();
            return;
        }

        Projectile.Center += direction * step;
        Projectile.localAI[0] += step;
        int phase = (int)Projectile.localAI[1]++ + Projectile.identity * 13;
        FranFlatLightningEffects.EmitPoint(Projectile.Center, direction, Intensity, Palette, phase);

        if (Projectile.numUpdates == 0)
        {
            Color light = Palette == FranFlatLightningPalette.ThorsHammer
                ? new Color(80, 190, 255)
                : new Color(125, 120, 255);
            Lighting.AddLight(Projectile.Center, light.ToVector3() * (0.55f * Intensity));
        }

        if (Projectile.localAI[0] >= PathLength - 0.01f)
            Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor) => false;
}
