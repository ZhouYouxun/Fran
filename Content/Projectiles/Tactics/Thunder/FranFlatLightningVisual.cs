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

// Shared visual-only discharge: frozen pixel fractures plus original single-frame after-arcs.
internal static class FranFlatLightningEffects
{
    private const int UpdatesPerTick = 11;
    private const float GoldenAngle = 2.39996323f;

    public static Color BodyColor(FranFlatLightningPalette palette) =>
        palette == FranFlatLightningPalette.ThorsHammer ? new Color(92, 196, 255) : new Color(150, 112, 255);

    public static Color EdgeColor(FranFlatLightningPalette palette) =>
        palette == FranFlatLightningPalette.ThorsHammer ? new Color(194, 242, 255) : new Color(112, 213, 255);

    public static void Fracture(Vector2 start, Vector2 end, float intensity,
        FranFlatLightningPalette palette, int seed, float growthFrames = 3f, int lifetime = 16)
    {
        if (Main.dedServ || Main.gameMenu || !Visible(start, end) || FranParticleSystem.FreeSlots < 12)
            return;
        float distance = Vector2.Distance(start, end);
        if (!float.IsFinite(distance) || distance < 7f || distance > 2400f)
            return;
        FranParticleSystem.Spawn(new FranFissionArcParticle(start, end, BodyColor(palette),
            MathHelper.Clamp(2.5f * intensity, 0.8f, 6.5f), seed, lifetime, growthFrames));
    }

    public static void Burst(Vector2 center, float intensity, FranFlatLightningPalette palette,
        int count, float radius, bool ground = false, float innerRadius = 7f)
    {
        if (Main.dedServ || !Visible(center, center))
            return;
        float phase = Main.rand.NextFloat(MathHelper.TwoPi);
        for (int i = 0; i < Math.Clamp(count, 1, 9); i++)
        {
            float angle = phase + MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.22f, 0.22f);
            Vector2 direction = angle.ToRotationVector2();
            Vector2 reach = direction * radius * Main.rand.NextFloat(0.65f, 1.12f);
            if (ground)
                reach.Y *= 0.28f;
            Vector2 start = center + reach.SafeNormalize(Vector2.UnitX) * Math.Max(0f, innerRadius);
            Fracture(start, center + reach, intensity, palette, Main.rand.Next(), 2.5f, 14);
            EmitPoint(center + reach * 0.55f, reach, intensity, palette, i * 2);
        }
    }

    // Spatial sampling keeps fast long links continuous without increasing damaging projectile updates.
    public static void EmitSpan(Vector2 start, Vector2 end, float intensity,
        FranFlatLightningPalette palette, int phase)
    {
        if (Main.dedServ || !Visible(start, end))
            return;
        float length = Vector2.Distance(start, end);
        if (!float.IsFinite(length) || length < 0.5f)
            return;
        int samples = Math.Clamp((int)MathF.Ceiling(length / 24f), 1, 28);
        Vector2 direction = (end - start).SafeNormalize(Vector2.UnitX);
        for (int i = 0; i < samples; i++)
            EmitPoint(Vector2.Lerp(start, end, (i + 0.5f) / samples), direction,
                intensity, palette, phase + i * 2);
    }

    private static bool Visible(Vector2 start, Vector2 end) =>
        Math.Max(start.X, end.X) >= Main.screenPosition.X - 700f &&
        Math.Min(start.X, end.X) <= Main.screenPosition.X + Main.screenWidth + 700f &&
        Math.Max(start.Y, end.Y) >= Main.screenPosition.Y - 700f &&
        Math.Min(start.Y, end.Y) <= Main.screenPosition.Y + Main.screenHeight + 700f;

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
        if (Main.dedServ || intensity <= 0.05f || !Visible(center, center))
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
        if (Main.dedServ || intensity <= 0.05f || FranParticleSystem.FreeSlots < 8 || !Visible(center, center))
            return;

        direction = direction.SafeNormalize(Vector2.UnitY);
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        float oscillation = MathF.Sin(phase * GoldenAngle);
        Color body = BodyColor(palette);
        Color edge = EdgeColor(palette);
        Color color = phase % 5 == 0 ? Color.White : Color.Lerp(body, edge, 0.5f + oscillation * 0.22f);
        Vector2 offset = normal * oscillation * 7f * intensity;
        Vector2 drift = -direction * (0.28f + intensity * 0.16f) + normal * oscillation * 0.36f;

        if ((phase & 1) == 0)
            FranParticleSystem.Spawn(new FranArcParticle(center + offset, drift,
                direction.RotatedBy(oscillation * 0.22f), color,
                (48f + 18f * MathF.Abs(oscillation)) * intensity,
                (30f + 8f * MathF.Abs(oscillation)) * intensity, 10 + Math.Abs(phase % 4)));

        if (phase % 5 == 0)
            FranParticleSystem.Spawn(new FranLineParticle(center, drift * 1.2f,
                Color.Lerp(color, Color.White, 0.38f), 22f * intensity, 1.1f * intensity, 6, 0.90f));
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
        if (Projectile.localAI[1] == 0f)
        {
            float travelTicks = PathLength / Math.Max(1f, Projectile.velocity.Length()) / 11f;
            FranFlatLightningEffects.Fracture(Projectile.Center, Projectile.Center + direction * PathLength,
                Intensity, Palette, Projectile.identity * 397 ^ Projectile.owner,
                MathHelper.Clamp(travelTicks, 0.8f, 5f));
        }
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
