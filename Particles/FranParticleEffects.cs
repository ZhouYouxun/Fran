using System;
using FranMod.Core;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace FranMod.Content.Projectiles;

internal static class FranParticleEffects
{
    private static Color WhiteCore(Color color, float amount = 0.74f) => Color.Lerp(color, Color.White, amount);

    private static void Spawn(FranParticle particle) => FranParticleSystem.Spawn(particle);

    public static void MeleeTrail(Vector2 tip, Vector2 bladeDirection, Vector2 tangent, Color color,
        float intensity, int combo, bool flame)
    {
        if (Main.dedServ || intensity < 0.1f)
            return;

        bladeDirection = bladeDirection.SafeNormalize(Vector2.UnitX);
        tangent = tangent.SafeNormalize(bladeDirection.RotatedBy(MathHelper.PiOver2));
        float heavy = combo == 2 ? 1.45f : combo == 3 ? 1.2f : 1f;

        Spawn(new FranSparkParticle(tip - bladeDirection * Main.rand.NextFloat(3f, 18f),
            tangent * Main.rand.NextFloat(2.8f, 7.5f) * heavy + Main.rand.NextVector2Circular(1.2f, 1.2f),
            color, Main.rand.NextFloat(0.07f, 0.13f) * heavy,
            new Vector2(Main.rand.NextFloat(0.45f, 0.8f), Main.rand.NextFloat(1.5f, 2.4f)),
            Main.rand.Next(10, 17), critical: combo == 2));

        if (Main.rand.NextBool(combo == 2 ? 1 : 2))
            Spawn(new FranLineParticle(tip, tangent * Main.rand.NextFloat(1.8f, 5.4f), WhiteCore(color),
                Main.rand.NextFloat(24f, 52f) * heavy, Main.rand.NextFloat(1.5f, 3.2f), Main.rand.Next(8, 14)));

        if (Main.rand.NextBool(3))
            Spawn(new FranGlowOrbParticle(tip - bladeDirection * Main.rand.NextFloat(5f, 24f),
                tangent * Main.rand.NextFloat(0.4f, 1.5f), color,
                Main.rand.NextFloat(0.05f, 0.10f), Main.rand.NextFloat(0.015f, 0.04f), Main.rand.Next(8, 15), true));

        if (combo == 2 && Main.rand.NextBool(2))
            Spawn(new FranBladeChipParticle(tip + Main.rand.NextVector2Circular(10f, 10f),
                tangent * Main.rand.NextFloat(3.5f, 9f) + Main.rand.NextVector2Circular(2f, 2f), color,
                Main.rand.NextFloat(0.8f, 1.35f), Main.rand.Next(12, 20), 0.09f));

        if (combo == 3 && Main.rand.NextBool(2))
            Spawn(new FranBoltParticle(tip, -bladeDirection * Main.rand.NextFloat(1.5f, 4.5f), color,
                Main.rand.NextFloat(0.18f, 0.3f), new Vector2(0.55f, 1.4f), Main.rand.Next(8, 13)));

        if (flame && Main.rand.NextBool(3))
            Spawn(new FranHeavySmokeParticle(tip - bladeDirection * Main.rand.NextFloat(8f, 30f),
                -tangent * Main.rand.NextFloat(0.5f, 1.8f), new Color(255, 74, 25),
                Main.rand.NextFloat(0.22f, 0.38f), Main.rand.Next(18, 28), 0.42f, true));
    }

    public static void MeleeImpact(Vector2 center, Vector2 incomingDirection, Color color, int combo)
    {
        if (Main.dedServ)
            return;

        // Calamity's large sword hits generally use a small, authored set of direction-aligned
        // cuts around one focal event. Keep that readable sentence here instead of emitting a
        // randomized constellation of tiny stars on every contact.
        incomingDirection = incomingDirection.SafeNormalize(Vector2.UnitX);
        Vector2 normal = incomingDirection.RotatedBy(MathHelper.PiOver2);
        float intensity = combo == 2 ? 1.22f : combo == 3 ? 1.08f : 0.94f;
        Spawn(new FranDirectionalRingParticle(center, Vector2.Zero, color,
            new Vector2(0.07f, 0.13f), new Vector2(0.76f, 0.27f) * intensity,
            incomingDirection.ToRotation(), combo == 2 ? 15 : 12));

        int cutCount = combo == 2 ? 4 : combo == 3 ? 3 : 2;
        float middle = (cutCount - 1f) * 0.5f;
        for (int i = 0; i < cutCount; i++)
        {
            float lane = i - middle;
            Vector2 cutDirection = incomingDirection.RotatedBy(lane * 0.085f);
            Vector2 position = center - incomingDirection * (3f + MathF.Abs(lane) * 2f) + normal * lane * 6f;
            Vector2 velocity = incomingDirection * (2.6f + i * 0.55f) + normal * lane * 1.15f;
            Spawn(new FranBladeEdgeMoteParticle(position, velocity, cutDirection,
                i == cutCount / 2 ? Color.White : color,
                (combo == 2 ? 31f : 25f) - MathF.Abs(lane) * 3f,
                combo == 2 ? 1.7f : 1.45f, combo == 2 ? 12 : 10));
        }

        float arcSide = combo == 3 ? -1f : 1f;
        Spawn(new FranBladeArcFragmentParticle(center - incomingDirection * 4f,
            incomingDirection * 0.7f + normal * arcSide * 0.28f, incomingDirection, color,
            combo == 2 ? 32f : 26f, arcSide * (combo == 2 ? 0.86f : 0.68f),
            combo == 2 ? 2.8f : 2.2f, combo == 2 ? 13 : 11, arcSide * 0.018f));
    }

    public static void PhantomBladeExplosion(Vector2 center, Vector2 direction, float scale = 1f)
    {
        if (Main.dedServ)
            return;

        direction = direction.SafeNormalize(Vector2.UnitX);
        Color body = new(145, 215, 255);
        Color edge = new(225, 248, 255);
        Spawn(new FranDirectionalRingParticle(center, Vector2.Zero, body,
            new Vector2(0.08f, 0.14f), new Vector2(0.88f, 0.34f) * scale,
            direction.ToRotation(), 15));
        Spawn(new FranSmallBloomParticle(center, Vector2.Zero, edge,
            27f * scale, 7f * scale, 12));

        // A five-lane forward fan reads as a blade breaking apart, not as another radial star.
        for (int i = 0; i < 5; i++)
        {
            float lane = i - 2f;
            Vector2 ray = direction.RotatedBy(lane * 0.31f);
            float speed = (5.6f - MathF.Abs(lane) * 0.65f) * scale;
            Spawn(new FranBladeEdgeMoteParticle(center + ray * 5f, ray * speed, ray,
                i == 2 ? Color.White : body, (36f - MathF.Abs(lane) * 5f) * scale,
                (i == 2 ? 2.1f : 1.55f) * scale, 14 + (2 - (int)MathF.Abs(lane))));
        }

        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        for (int side = -1; side <= 1; side += 2)
            Spawn(new FranBladeArcFragmentParticle(center + normal * side * 4f,
                direction * 1.2f + normal * side * 1.6f, direction, body,
                27f * scale, side * 0.82f, 2.4f * scale, 14, side * 0.035f));

        FranVanillaDustEffects.Impact(center, direction, FranSkill.MultipleBodyCreation,
            edge, 9, 0.92f * scale);
    }

    public static void SpawnImpact(Vector2 center, Vector2 direction, Color color, int count, float speed,
        float scale = 1f)
    {
        direction = direction.SafeNormalize(Vector2.UnitX);
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = direction.RotatedByRandom(0.95f) * Main.rand.NextFloat(speed * 0.28f, speed);
            if (i % 3 == 0)
                Spawn(new FranLineParticle(center + Main.rand.NextVector2Circular(6f, 6f), velocity,
                    i % 5 == 0 ? Color.White : color, Main.rand.NextFloat(22f, 52f) * scale,
                    Main.rand.NextFloat(1.2f, 2.8f) * scale, Main.rand.Next(8, 17)));
            else
                Spawn(new FranSparkParticle(center + Main.rand.NextVector2Circular(8f, 8f), velocity,
                    i % 4 == 0 ? Color.White : color, Main.rand.NextFloat(0.06f, 0.13f) * scale,
                    new Vector2(0.5f, Main.rand.NextFloat(1.2f, 2.1f)), Main.rand.Next(10, 20), critical: i % 4 == 0));
        }
    }

    public static void CrescentWake(Vector2 center, Vector2 velocity, Color color, float width)
    {
        Vector2 back = -velocity.SafeNormalize(Vector2.UnitX);
        Spawn(new FranLineParticle(center + Main.rand.NextVector2Circular(width * 0.22f, width * 0.22f),
            back * Main.rand.NextFloat(0.8f, 2.4f), color, Main.rand.NextFloat(28f, 62f),
            Main.rand.NextFloat(1.2f, 2.6f), Main.rand.Next(10, 18)));
        if (Main.rand.NextBool(3))
            Spawn(new FranWaterParticle(center, back.RotatedByRandom(0.55f) * Main.rand.NextFloat(1.2f, 3.8f),
                Color.Lerp(color, Color.White, 0.18f), Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(14, 24), 0.04f));
    }

    public static void StarlightOrbit(Vector2 center, Color color, float phase)
    {
        float angle = phase * 0.12f + Main.rand.NextFloat(-0.22f, 0.22f);
        Vector2 position = center + angle.ToRotationVector2() * Main.rand.NextFloat(18f, 38f);
        Spawn(new FranStarMoteParticle(position, angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 0.45f,
            Color.White, color, Main.rand.NextFloat(0.28f, 0.55f), Main.rand.Next(18, 30)));
    }

    public static void StarlightWake(Vector2 center, Vector2 velocity, Color color)
    {
        Vector2 back = -velocity.SafeNormalize(Vector2.UnitX);
        Spawn(new FranStarMoteParticle(center + Main.rand.NextVector2Circular(8f, 8f),
            back.RotatedByRandom(0.7f) * Main.rand.NextFloat(0.5f, 2.5f), Color.White, color,
            Main.rand.NextFloat(0.24f, 0.48f), Main.rand.Next(15, 26)));
        if (Main.rand.NextBool(2))
            Spawn(new FranGlowOrbParticle(center, back * 0.8f, color, 0.09f, 0.02f, 12));
    }

    public static void Implosion(Vector2 center, Color color, float scale = 1f)
    {
        for (int i = 0; i < 18; i++)
        {
            Vector2 direction = (MathHelper.TwoPi * i / 18f).ToRotationVector2();
            Vector2 position = center + direction * Main.rand.NextFloat(55f, 110f) * scale;
            Spawn(new FranLineParticle(position, -direction * Main.rand.NextFloat(3f, 8f), color,
                Main.rand.NextFloat(28f, 58f) * scale, Main.rand.NextFloat(1.5f, 3f) * scale, Main.rand.Next(12, 20)));
        }
        Spawn(new FranDirectionalRingParticle(center, Vector2.Zero, color, Vector2.One * 0.9f * scale,
            Vector2.One * 0.12f * scale, 0f, 18));
    }

    public static void Compression(Vector2 center, Vector2 velocity, Color color, float phase, bool released)
    {
        Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);
        if (released)
        {
            Spawn(new FranDirectionalRingParticle(center, direction * 0.6f, color, new Vector2(0.08f, 0.16f),
                new Vector2(0.95f, 0.34f), direction.ToRotation(), 15));
            SpawnImpact(center, direction, color, 14, 9f, 1.15f);
        }
        else if (Main.rand.NextBool(2))
        {
            Vector2 position = center + Main.rand.NextVector2CircularEdge(48f, 48f);
            Spawn(new FranLineParticle(position, (center - position).SafeNormalize(Vector2.Zero) * 3.5f,
                color, 30f, 2f, 15));
        }
    }

    public static void InwardSpark(Vector2 center, Color color, float minRadius, float maxRadius, float speed,
        float scale = 1f)
    {
        Vector2 position = center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(minRadius, maxRadius);
        Spawn(new FranLineParticle(position, (center - position).SafeNormalize(Vector2.Zero) * speed,
            color, Main.rand.NextFloat(24f, 48f) * scale, Main.rand.NextFloat(1.4f, 2.8f) * scale,
            Main.rand.Next(12, 20)));
    }

    public static void LineRelease(Vector2 start, Vector2 end, Color color, int count, float strength = 1f)
    {
        Vector2 direction = (end - start).SafeNormalize(Vector2.UnitX);
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        for (int i = 0; i < count; i++)
        {
            Vector2 position = Vector2.Lerp(start, end, Main.rand.NextFloat()) + normal * Main.rand.NextFloat(-9f, 9f);
            Spawn(new FranLineParticle(position, direction.RotatedByRandom(0.35f) * Main.rand.NextFloat(2f, 6f) * strength,
                color, Main.rand.NextFloat(20f, 52f) * strength, Main.rand.NextFloat(1.2f, 2.6f), Main.rand.Next(8, 16)));
        }
    }

    public static void AuraMote(Vector2 center, Color color, bool flame, float radius = 42f)
    {
        Vector2 offset = Main.rand.NextVector2CircularEdge(radius, radius);
        if (flame)
            Spawn(new FranHeavySmokeParticle(center + offset, -offset.SafeNormalize(Vector2.UnitY) * 0.55f,
                color, Main.rand.NextFloat(0.16f, 0.28f), Main.rand.Next(18, 30), 0.35f, true));
        else
            Spawn(new FranGlowOrbParticle(center + offset, -offset.SafeNormalize(Vector2.UnitY) * 0.42f,
                color, 0.07f, 0.018f, Main.rand.Next(16, 28)));
    }

    public static void Burst(Vector2 center, Color color, float scale, FranBurstStyle style, float rotation)
    {
        Vector2 direction = rotation.ToRotationVector2();
        int customParticleEstimate = style switch
        {
            FranBurstStyle.SkillTaker => 20,
            FranBurstStyle.ThorsHammer => 18,
            FranBurstStyle.ShipCutter => 18,
            FranBurstStyle.DashImpact => 23,
            FranBurstStyle.AirDraw => 19,
            FranBurstStyle.Flame => 22,
            _ => 12
        };
        FranVanillaDustEffects.Burst(center, direction, color, style, customParticleEstimate, scale);
        switch (style)
        {
            case FranBurstStyle.Telekinetic:
                Implosion(center, color, scale);
                StarlightOrbit(center, color, rotation * 17f);
                break;
            case FranBurstStyle.SkillTaker:
                SkillTakerNovaExplosion(center, direction, color, scale);
                break;
            case FranBurstStyle.Barrier:
                BarrierWard(center, color, scale);
                break;
            case FranBurstStyle.SwordArray:
                BladeConstellation(center, color, scale, 12);
                break;
            case FranBurstStyle.Flame:
                for (int i = 0; i < 10; i++)
                    AuraMote(center + Main.rand.NextVector2Circular(18f, 18f), color, true, 48f * scale);
                SpawnImpact(center, direction, color, 12, 8f * scale, scale);
                break;
            case FranBurstStyle.AirDraw:
                LineRelease(center - direction * 50f * scale, center + direction * 70f * scale, color, 18, scale);
                Spawn(new FranDirectionalRingParticle(center, direction, color, new Vector2(0.06f, 0.12f),
                    new Vector2(1.15f, 0.42f) * scale, rotation, 15));
                break;
            case FranBurstStyle.Lightning:
            case FranBurstStyle.ThorsHammer:
                LightningBurst(center, color, style == FranBurstStyle.ThorsHammer ? 18 : 10, scale);
                break;
            case FranBurstStyle.ShipCutter:
                WaterBurst(center, direction, color, 18, scale);
                break;
            case FranBurstStyle.DashImpact:
                SpawnImpact(center, direction, color, 18, 11f * scale, scale);
                for (int i = 0; i < 5; i++)
                    Spawn(new FranHeavySmokeParticle(center + Main.rand.NextVector2Circular(16f, 16f),
                        -direction * Main.rand.NextFloat(1f, 4f), new Color(34, 14, 54),
                        Main.rand.NextFloat(0.25f, 0.45f) * scale, Main.rand.Next(18, 30), 0.75f));
                break;
            case FranBurstStyle.Potential:
                Implosion(center, color, scale * 1.2f);
                HeartMagic(center, direction, Color.Lerp(color, Color.Magenta, 0.35f), scale, 6);
                break;
            default:
                MeleeImpact(center, direction, color, 2);
                break;
        }
    }

    private static void SkillTakerNovaExplosion(Vector2 center, Vector2 direction, Color color, float scale)
    {
        direction = direction.SafeNormalize(Vector2.UnitX);
        Color pale = Color.Lerp(color, Color.White, 0.68f);
        Color deep = Color.Lerp(color, new Color(72, 8, 112), 0.46f);

        // Three differently timed circles give the detonation a bright core, a purple body and
        // a slower afterglow without falling back to the generic HardBloom plate.
        Spawn(new FranBloomCirclePulseParticle(center, Vector2.Zero, pale,
            12f * scale, 58f * scale, 11, 0.96f));
        Spawn(new FranBloomCirclePulseParticle(center, -direction * 0.18f, color,
            22f * scale, 118f * scale, 18, 0.84f, spin: 0.025f));
        Spawn(new FranBloomCirclePulseParticle(center, -direction * 0.10f, deep,
            34f * scale, 146f * scale, 24, 0.52f, spin: -0.018f));
        Spawn(new FranRingPulseParticle(center, Vector2.Zero, pale,
            Vector2.One * 18f * scale, Vector2.One * 126f * scale, 18,
            direction.ToRotation(), 0.026f, 0.78f));

        float phase = direction.ToRotation();
        for (int i = 0; i < 8; i++)
        {
            Vector2 ray = (phase + MathHelper.TwoPi * i / 8f).ToRotationVector2();
            Color rayColor = i % 3 == 0 ? pale : color;
            Spawn(new FranNeedleParticle(center + ray * 4f, ray * (4.8f + i % 2 * 1.1f) * scale,
                rayColor, new Vector2(44f, 5.5f) * scale, new Vector2(9f, 1.4f) * scale,
                13 + i % 3, ray.ToRotation(), 0.91f));
            if (i % 2 == 0)
                Spawn(new FranBloomCirclePulseParticle(center + ray * 8f, ray * 2.4f * scale,
                    rayColor, 10f * scale, 24f * scale, 13, 0.62f, 0.90f));
        }

        FranVanillaDustEffects.Impact(center, direction, FranSkill.SkillTaker, pale, 16, scale);
    }

    public static void DashLaunch(Vector2 center, Vector2 velocity)
    {
        if (Main.dedServ)
            return;

        Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);
        Vector2 back = -direction;
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        Color shadow = new(24, 8, 42);
        Color violet = new(122, 62, 184);
        Color edge = new(214, 176, 255);

        Spawn(new FranDirectionalRingParticle(center - direction * 8f, back * 0.35f, violet,
            new Vector2(0.08f, 0.20f), new Vector2(1.12f, 0.46f), direction.ToRotation(), 14));
        Spawn(new FranDirectionalRingParticle(center - direction * 14f, back * 0.22f, edge,
            new Vector2(0.05f, 0.12f), new Vector2(0.76f, 0.26f), direction.ToRotation(), 11));
        Spawn(new FranBloomParticle(center, back * 0.18f, violet, 58f, 9f, 12, 0.82f, hardCenter: true));

        for (int i = 0; i < 8; i++)
        {
            float side = Main.rand.NextFloat(-22f, 22f);
            Vector2 position = center + normal * side + direction * Main.rand.NextFloat(-8f, 12f);
            Vector2 particleVelocity = back.RotatedByRandom(0.34f) * Main.rand.NextFloat(4.5f, 10.5f) +
                normal * MathF.Sign(side == 0f ? Main.rand.NextFloat(-1f, 1f) : side) * Main.rand.NextFloat(0.3f, 1.6f);
            Spawn(new FranNeedleParticle(position, particleVelocity, i % 3 == 0 ? Color.White : edge,
                new Vector2(Main.rand.NextFloat(36f, 60f), Main.rand.NextFloat(5f, 9f)),
                new Vector2(10f, 2f), Main.rand.Next(10, 17), particleVelocity.ToRotation()));
        }

        for (int i = 0; i < 3; i++)
            Spawn(new FranHeavySmokeParticle(center - direction * Main.rand.NextFloat(4f, 18f) +
                normal * Main.rand.NextFloat(-16f, 16f), back.RotatedByRandom(0.5f) * Main.rand.NextFloat(1.4f, 3.8f),
                shadow, Main.rand.NextFloat(0.32f, 0.52f), Main.rand.Next(22, 34), 0.90f));

        FranVanillaDustEffects.SkillRelease(center, back, FranSkill.None, edge, 14, 0.92f);
    }

    public static void DashWake(Vector2 center, Vector2 velocity, int tick)
    {
        if (Main.dedServ || velocity.LengthSquared() < 1f)
            return;

        Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);
        Vector2 back = -direction;
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        float side = tick % 2 == 0 ? 1f : -1f;
        Color violet = tick % 3 == 0 ? new Color(188, 136, 238) : new Color(112, 55, 172);

        Spawn(new FranLineParticle(center - direction * Main.rand.NextFloat(10f, 28f) +
            normal * side * Main.rand.NextFloat(9f, 18f), back * Main.rand.NextFloat(2.8f, 5.6f), violet,
            Main.rand.NextFloat(34f, 68f), Main.rand.NextFloat(1.5f, 2.8f), Main.rand.Next(9, 15), 0.91f));

        if (tick % 2 == 0)
        {
            Spawn(new FranNeedleParticle(center - direction * Main.rand.NextFloat(18f, 42f) -
                normal * side * Main.rand.NextFloat(4f, 15f), back * Main.rand.NextFloat(3f, 7f),
                tick % 4 == 0 ? Color.White : new Color(205, 164, 255),
                new Vector2(Main.rand.NextFloat(34f, 58f), Main.rand.NextFloat(4f, 7f)), new Vector2(8f, 1.5f),
                Main.rand.Next(8, 14), back.ToRotation()));
            Spawn(new FranHeavySmokeParticle(center - direction * Main.rand.NextFloat(18f, 36f) +
                Main.rand.NextVector2Circular(8f, 8f), back.RotatedByRandom(0.40f) * Main.rand.NextFloat(1.2f, 3.4f),
                new Color(18, 7, 29), Main.rand.NextFloat(0.24f, 0.40f), Main.rand.Next(18, 28), 0.76f));
        }

        // Together with the authored wake particles this keeps vanilla Dust close to the project's
        // intended one-fifth support layer without allowing it to replace the readable dash silhouette.
        FranVanillaDustEffects.ProjectileTrail(center - direction * 8f, velocity, FranSkill.None,
            new Color(174, 112, 228), 0.44f, 0.88f);
    }

    public static void DashBrake(Vector2 center, Vector2 velocity)
    {
        if (Main.dedServ)
            return;

        Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);
        Vector2 back = -direction;
        Color violet = new(112, 53, 174);
        Color edge = new(218, 182, 255);

        Spawn(new FranDirectionalRingParticle(center, back * 0.25f, violet,
            new Vector2(0.10f, 0.22f), new Vector2(0.92f, 0.40f), direction.ToRotation(), 15));
        Spawn(new FranDirectionalRingParticle(center, Vector2.Zero, edge,
            new Vector2(0.06f, 0.14f), new Vector2(0.62f, 0.28f), direction.ToRotation(), 11));
        Spawn(new FranBloomParticle(center, Vector2.Zero, violet, 42f, 8f, 13, 0.72f, hardCenter: true));

        for (int i = 0; i < 9; i++)
        {
            Vector2 ray = direction.RotatedBy(Main.rand.NextFloat(-1.12f, 1.12f));
            Spawn(new FranLineParticle(center + ray * Main.rand.NextFloat(2f, 12f), ray * Main.rand.NextFloat(2.5f, 7f),
                i % 4 == 0 ? Color.White : edge, Main.rand.NextFloat(24f, 52f), Main.rand.NextFloat(1.2f, 2.8f),
                Main.rand.Next(9, 16)));
        }

        for (int i = 0; i < 3; i++)
            Spawn(new FranHeavySmokeParticle(center - direction * Main.rand.NextFloat(4f, 22f) +
                Main.rand.NextVector2Circular(9f, 9f), back.RotatedByRandom(0.65f) * Main.rand.NextFloat(0.8f, 2.8f),
                new Color(22, 8, 35), Main.rand.NextFloat(0.26f, 0.44f), Main.rand.Next(20, 32), 0.80f));

        FranVanillaDustEffects.Impact(center, direction, FranSkill.None, edge, 15, 0.9f);
    }

    public static void PotentialAura(Vector2 center, Color color)
    {
        Spawn(new FranStarMoteParticle(center, Main.rand.NextVector2Circular(0.8f, 0.8f), Color.White, color,
            Main.rand.NextFloat(0.24f, 0.48f), Main.rand.Next(18, 30)));
        if (Main.rand.NextBool(3))
            Spawn(new FranHeartParticle(center, Main.rand.NextVector2Circular(0.5f, 0.5f),
                Color.Lerp(color, new Color(255, 105, 205), 0.5f), Main.rand.NextFloat(0.18f, 0.3f), Main.rand.Next(22, 36)));
    }

    public static void HeartMagic(Vector2 center, Vector2 direction, Color color, float scale, int count)
    {
        direction = direction.SafeNormalize(Vector2.UnitY);
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = direction.RotatedByRandom(0.9f) * Main.rand.NextFloat(0.8f, 3.8f) * scale;
            Spawn(new FranHeartParticle(center + Main.rand.NextVector2Circular(12f, 12f), velocity,
                i % 3 == 0 ? Color.White : color, Main.rand.NextFloat(0.16f, 0.32f) * scale,
                Main.rand.Next(22, 40), Main.rand.NextFloat(-0.025f, 0.025f), i == 0 && scale > 1f));
        }
    }

    public static void BarrierWard(Vector2 center, Color color, float scale)
    {
        Spawn(new FranDirectionalRingParticle(center, Vector2.Zero, color, Vector2.One * 0.18f * scale,
            Vector2.One * 0.95f * scale, 0f, 24, 0.025f));
        Spawn(new FranDirectionalRingParticle(center, Vector2.Zero, Color.White, Vector2.One * 0.12f * scale,
            Vector2.One * 0.70f * scale, MathHelper.PiOver2, 18, -0.032f));
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * 44f * scale;
            Spawn(new FranStarMoteParticle(center + offset, offset.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero) * 0.6f,
                Color.White, color, 0.24f * scale, 28));
        }
    }

    public static void BladeConstellation(Vector2 center, Color color, float scale, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = (MathHelper.TwoPi * i / count).ToRotationVector2();
            Spawn(new FranLineParticle(center + direction * Main.rand.NextFloat(12f, 34f) * scale,
                direction * Main.rand.NextFloat(2f, 6f) * scale, i % 4 == 0 ? Color.White : color,
                Main.rand.NextFloat(34f, 72f) * scale, Main.rand.NextFloat(1.4f, 3f), Main.rand.Next(12, 22)));
            if (i % 3 == 0)
                Spawn(new FranBladeChipParticle(center + direction * 20f * scale, direction * 5f * scale,
                    color, Main.rand.NextFloat(0.7f, 1.2f) * scale, Main.rand.Next(12, 20), 0.02f));
        }
    }

    public static void LightningBurst(Vector2 center, Color color, int count, float scale)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 10f) * scale;
            Spawn(new FranBoltParticle(center + Main.rand.NextVector2Circular(10f, 10f), velocity,
                i % 4 == 0 ? Color.White : color, Main.rand.NextFloat(0.16f, 0.32f) * scale,
                new Vector2(Main.rand.NextFloat(0.4f, 0.75f), Main.rand.NextFloat(1.1f, 1.8f)), Main.rand.Next(9, 17)));
        }
        Spawn(new FranGlowOrbParticle(center, Vector2.Zero, color, 0.58f * scale, 0.04f, 14, true));
    }

    public static void WaterBurst(Vector2 center, Vector2 direction, Color color, int count, float scale)
    {
        direction = direction.SafeNormalize(Vector2.UnitX);
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = direction.RotatedByRandom(1.15f) * Main.rand.NextFloat(2f, 9f) * scale;
            Spawn(new FranWaterParticle(center + Main.rand.NextVector2Circular(10f, 10f), velocity,
                i % 4 == 0 ? Color.White : color, Main.rand.NextFloat(0.45f, 0.95f) * scale,
                Main.rand.Next(16, 28)));
        }
        Spawn(new FranDirectionalRingParticle(center, direction, color, new Vector2(0.08f, 0.14f),
            new Vector2(0.92f, 0.36f) * scale, direction.ToRotation(), 17));
    }

    public static void ToxicBurst(Vector2 center, Vector2 direction, int count, float scale = 1f)
    {
        direction = direction.SafeNormalize(Vector2.UnitY);
        Color acid = new(155, 255, 66);
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = direction.RotatedByRandom(1.35f) * Main.rand.NextFloat(1.2f, 6f) * scale;
            Spawn(new FranToxicMoteParticle(center + Main.rand.NextVector2Circular(12f, 12f), velocity,
                i % 4 == 0 ? new Color(225, 255, 120) : acid,
                Main.rand.NextFloat(0.65f, 1.25f) * scale, Main.rand.Next(22, 40)));
        }
    }

    public static void NeedleFan(Vector2 center, Vector2 direction, Color color, int count, float speed,
        float scale = 1f, float spread = 0.72f)
    {
        if (Main.dedServ)
            return;

        direction = direction.SafeNormalize(Vector2.UnitX);
        for (int i = 0; i < count; i++)
        {
            Vector2 ray = direction.RotatedBy(Main.rand.NextFloat(-spread, spread));
            float velocity = Main.rand.NextFloat(speed * 0.42f, speed);
            float length = Main.rand.NextFloat(36f, 68f) * scale;
            float width = Main.rand.NextFloat(9f, 16f) * scale;
            Spawn(new FranNeedleParticle(center + ray * Main.rand.NextFloat(2f, 14f), ray * velocity,
                i % 4 == 0 ? WhiteCore(color, 0.82f) : color,
                new Vector2(length, width), new Vector2(length * 0.28f, width * 0.28f),
                Main.rand.Next(10, 18), ray.ToRotation(), Main.rand.NextFloat(0.89f, 0.96f)));
        }
    }

    public static void BloomPulse(Vector2 center, Color color, float startSize, float endSize,
        int lifetime = 18, float opacity = 1f, bool hardCenter = false)
    {
        if (Main.dedServ)
            return;
        Spawn(new FranBloomParticle(center, Vector2.Zero, color, startSize, endSize, lifetime,
            opacity, 0.92f, hardCenter));
    }

    public static void RingPulse(Vector2 center, Color color, Vector2 startSize, Vector2 endSize,
        int lifetime = 18, float rotation = 0f, float spin = 0f, float opacity = 1f)
    {
        if (Main.dedServ)
            return;
        Spawn(new FranRingPulseParticle(center, Vector2.Zero, color, startSize, endSize,
            lifetime, rotation, spin, opacity));
    }

    public static void MysticGlyphs(Vector2 center, Color color, int count, float radius, float scale = 1f,
        Vector2? drift = null)
    {
        if (Main.dedServ)
            return;

        Vector2 baseDrift = drift ?? Vector2.Zero;
        float phase = Main.rand.NextFloat(MathHelper.TwoPi);
        for (int i = 0; i < count; i++)
        {
            float angle = phase + MathHelper.TwoPi * i / Math.Max(1, count) + Main.rand.NextFloat(-0.12f, 0.12f);
            Vector2 radial = angle.ToRotationVector2();
            Vector2 position = center + radial * Main.rand.NextFloat(radius * 0.72f, radius);
            Vector2 velocity = baseDrift + radial.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(0.2f, 0.7f);
            float size = Main.rand.NextFloat(16f, 25f) * scale;
            Spawn(new FranMysticGlyphParticle(position, velocity, color, Main.rand.Next(1, 6),
                size * 0.72f, size, Main.rand.Next(22, 34), angle + MathHelper.PiOver2,
                Main.rand.NextFloat(-0.025f, 0.025f)));
        }
    }

    public static void BlackLight(Vector2 center, Color coronaColor, float scale = 1f, int lifetime = 22)
    {
        if (Main.dedServ)
            return;

        Spawn(new FranBlackLightCoreParticle(center, Vector2.Zero, 18f * scale, 106f * scale,
            lifetime, 0.80f, Main.rand.NextFloat(MathHelper.TwoPi), 0.028f));
        Spawn(new FranBloomParticle(center, Vector2.Zero, coronaColor, 28f * scale, 136f * scale,
            lifetime, 0.72f, 0.92f, true));
        Spawn(new FranRingPulseParticle(center, Vector2.Zero, coronaColor,
            Vector2.One * 24f * scale, Vector2.One * 126f * scale, lifetime,
            Main.rand.NextFloat(MathHelper.TwoPi), -0.024f, 0.82f));
    }

    public static void CompressionSignature(Vector2 center, Vector2 direction, float scale)
    {
        Color cyan = new(75, 205, 255);
        direction = direction.SafeNormalize(Vector2.UnitX);
        BloomPulse(center, cyan, 18f * scale, 112f * scale, 17, 0.92f, true);
        RingPulse(center, new Color(135, 235, 255), Vector2.One * 18f * scale,
            Vector2.One * 122f * scale, 16, direction.ToRotation(), 0.03f, 0.92f);
        NeedleFan(center, direction, cyan, scale >= 0.9f ? 11 : 7, 8.5f * scale,
            0.82f * scale, scale >= 0.9f ? 1.08f : 0.82f);
    }
}
