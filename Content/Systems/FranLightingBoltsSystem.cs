using FranMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Renderers;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod;

// Terraria 原生池化粒子与 Fran 自定义粒子使用不同的注册入口，调用时保持两套生命周期独立。
[Autoload(Side = ModSide.Client)]
internal sealed class FranLightingBoltsSystem : ModSystem
{
    private static ParticlePool<PrettySparkleParticle> sparklePool;

    public override void Load() => sparklePool = new ParticlePool<PrettySparkleParticle>(420, () => new PrettySparkleParticle());
    public override void Unload() => sparklePool = null;

    private static void Sparkle(Vector2 position, Vector2 velocity, Color color, Vector2 scale, int lifetime,
        float rotation = 0f, float additive = 0.55f)
    {
        if (Main.dedServ || Main.gameMenu || sparklePool is null)
            return;

        PrettySparkleParticle particle = sparklePool.RequestParticle();
        particle.ColorTint = color;
        particle.LocalPosition = position;
        particle.Velocity = velocity;
        particle.Rotation = rotation;
        particle.Scale = scale;
        particle.FadeInNormalizedTime = 0.02f;
        particle.FadeOutNormalizedTime = 0.92f;
        particle.TimeToLive = lifetime;
        particle.FadeInEnd = System.Math.Max(1, lifetime / 4);
        particle.FadeOutStart = System.Math.Max(2, lifetime * 2 / 3);
        particle.FadeOutEnd = lifetime;
        particle.AdditiveAmount = additive;
        Main.ParticleSystem_World_OverPlayers.Add(particle);
    }

    // 凝念核：六向规则亮点先展开，随后四枚随机亮点打散轮廓。
    // 分身/蝶变共鸣共用原版 Lighting Bolts 光点，不附加软光团或 Dust。
    public static void SpawnBlueFlightLight(Vector2 position, Vector2 velocity, Color color, float scale = 1f)
    {
        if (Main.dedServ || Main.gameMenu) return;
        Sparkle(position, velocity, color,
            new Vector2(Main.rand.NextFloat(0.65f, 1.05f), Main.rand.NextFloat(0.20f, 0.36f)) * scale,
            Main.rand.Next(14, 23), velocity.ToRotation(), 0.58f);
    }

    public static void SpawnButterflyLights(Vector2 center)
    {
        if (Main.dedServ || Main.gameMenu) return;
        for (int i = 0; i < 12; i++)
        {
            Vector2 direction = Main.rand.NextVector2Unit();
            SpawnBlueFlightLight(center + direction * Main.rand.NextFloat(4f, 16f),
                direction * Main.rand.NextFloat(0.8f, 2.4f),
                Color.Lerp(new Color(45, 130, 255), new Color(135, 230, 255), Main.rand.NextFloat()), 0.85f);
        }
    }

    // 这里只注册 PrettySparkleParticle，不隐式补充 Dust 或 Fran 粒子。
    public static void SpawnMindCoreBurst(Vector2 center, float rotation, Color color, bool afterglow)
    {
        if (Main.dedServ || Main.gameMenu)
            return;

        int count = afterglow ? 6 : 8;
        for (int i = 0; i < count; i++)
        {
            float angle = afterglow ? Main.rand.NextFloat(MathHelper.TwoPi)
                : rotation + MathHelper.TwoPi * i / count;
            Vector2 direction = angle.ToRotationVector2();
            float radius = afterglow ? Main.rand.NextFloat(15f, 29f) : 9f;
            Vector2 velocity = direction * (afterglow ? Main.rand.NextFloat(0.6f, 1.6f) : 2.2f);
            Color tint = Color.Lerp(color, Color.White, i % 3 == 0 ? 0.55f : 0.12f);
            Sparkle(center + direction * radius, velocity, tint,
                new Vector2(afterglow ? 0.72f : 1.10f, 0.25f),
                afterglow ? 48 : 36, angle, 0.65f);
        }
    }

    // Judgement 光波的十字星节奏：只使用本模组已有的原版 PrettySparkle 粒子池。
    public static void SpawnTelekineticEmissionStar(Vector2 center, Vector2 forward, float scale = 1f)
    {
        if (Main.dedServ || Main.gameMenu) return;
        float rotation = forward.SafeNormalize(Vector2.UnitX).ToRotation();
        Vector2 drift = -forward.SafeNormalize(Vector2.UnitX) * 0.35f;
        Sparkle(center, drift, new Color(80, 185, 255),
            new Vector2(1.20f, 0.28f) * scale, 16, rotation, 0.42f);
        Sparkle(center, drift, new Color(210, 245, 255),
            new Vector2(1.20f, 0.28f) * scale, 16,
            rotation + MathHelper.PiOver2, 0.42f);
    }

    // 凝念核命中后只补四枚小光点，避免覆盖它原本的命中碎片和光环。
    public static void SpawnMindCoreJudgementHitLights(Vector2 center, Vector2 forward)
    {
        if (Main.dedServ || Main.gameMenu) return;
        float rotation = forward.SafeNormalize(Vector2.UnitX).ToRotation();
        Sparkle(center, Vector2.Zero, new Color(215, 245, 255),
            new Vector2(1.05f, 0.28f), 18, rotation, 0.45f);
        Sparkle(center, Vector2.Zero, new Color(85, 185, 255),
            new Vector2(1.05f, 0.28f), 18, rotation + MathHelper.PiOver2, 0.45f);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 offset = (rotation + MathHelper.PiOver2).ToRotationVector2() * (side * 7f);
            Sparkle(center + offset, offset * 0.12f, new Color(125, 215, 255),
                new Vector2(0.56f, 0.21f), 15, rotation, 0.34f);
        }
    }

    public static void SpawnIonizingRadiation(Vector2 position, Color color, float scale = 1f)
    {
        const int arms = 3;
        for (int arm = 0; arm < arms; arm++)
        {
            float baseAngle = MathHelper.ToRadians(-30f) + MathHelper.TwoPi * arm / arms;
            for (int point = 0; point < 4; point++)
            {
                float angle = baseAngle + MathHelper.ToRadians(20f) * point;
                Vector2 offset = angle.ToRotationVector2() * (8f + point * 5f) * scale;
                Sparkle(position + offset, offset.SafeNormalize(Vector2.Zero) * 0.16f, color,
                    new Vector2(1.6f, 0.46f) * scale, 28, angle, 0.42f);
            }
        }
    }

    public static void SpawnFlowerPattern(Vector2 position, Color petalColor, Color coreColor, float scale = 1f)
    {
        const int petals = 5;
        for (int petal = 0; petal < petals; petal++)
        {
            float baseAngle = MathHelper.TwoPi * petal / petals;
            for (int point = 1; point <= 5; point++)
            {
                float completion = point / 5f;
                float curve = System.MathF.Sin(completion * MathHelper.Pi) * 0.24f;
                float angle = baseAngle + curve;
                Vector2 offset = angle.ToRotationVector2() * 34f * completion * scale;
                Sparkle(position + offset, angle.ToRotationVector2() * 0.12f, petalColor,
                    new Vector2(1.35f, 0.48f) * scale, 38, angle, 0.44f);
            }
        }
        Sparkle(position, Vector2.Zero, coreColor, new Vector2(2.1f, 0.8f) * scale, 44, 0f, 0.62f);
    }

    public static void SpawnSpectralWhispers(Vector2 position, Color color, float radius = 52f, int count = 12)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(radius * 0.42f, radius);
            Vector2 velocity = new(System.MathF.Sin(i * 1.7f) * 0.22f, Main.rand.NextFloat(-0.65f, -0.12f));
            Sparkle(position + offset, velocity, i % 4 == 0 ? Color.White : color,
                Vector2.One * Main.rand.NextFloat(0.8f, 1.35f), Main.rand.Next(34, 56), angle, 0.58f);
        }
    }

    public static void SpawnConvergingMagic(Vector2 center, Color color, float radius, int count, float scale = 1f)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Main.rand.NextVector2CircularEdge(radius, radius);
            Vector2 velocity = -offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1.8f, 4.6f);
            Sparkle(center + offset, velocity, i % 5 == 0 ? Color.White : color,
                new Vector2(1.5f, 0.38f) * scale, Main.rand.Next(20, 34), velocity.ToRotation(), 0.62f);
        }
    }

    /// <summary>
    /// 折念刃飞行点缀：使用 Terraria 原版 PrettySparkleParticle 粒子池。
    /// 每次调用只生成一枚细长闪片，避免重新形成遮住波体的发光点云。
    /// </summary>
    public static void SpawnTelekineticWaveFlightSpark(Vector2 center, Vector2 forward,
        float halfWidth, float scale = 1f)
    {
        forward = forward.SafeNormalize(Vector2.UnitX);
        Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
        Vector2 position = center + normal * Main.rand.NextFloat(-halfWidth, halfWidth) -
            forward * Main.rand.NextFloat(4f, 24f) * scale;
        Vector2 velocity = -forward * Main.rand.NextFloat(0.25f, 0.70f) +
            normal * Main.rand.NextFloat(-0.12f, 0.12f);
        Color color = Main.rand.NextBool(4)
            ? Color.White
            : Color.Lerp(new Color(65, 160, 255), new Color(145, 230, 255), Main.rand.NextFloat());

        Sparkle(position, velocity, color,
            new Vector2(Main.rand.NextFloat(0.70f, 1.05f), Main.rand.NextFloat(0.12f, 0.20f)) * scale,
            Main.rand.Next(10, 15), forward.ToRotation(), 0.62f);
    }

    /// <summary>
    /// 沿冲击波外缘生成亮蓝色飞行碎光，分布方式与泰拉刃的大型剑气一致。
    /// </summary>
    public static void SpawnTelekineticWaveFlightDust(Vector2 center, Vector2 velocity,
        float rotation, float scale, float opacity)
    {
        if (Main.dedServ || opacity <= 0.01f)
            return;

        for (int i = 0; i < 3; i++)
        {
            if (Main.rand.NextFloat() >= opacity + 0.1f)
                continue;
            float spread = Main.rand.NextFloatDirection();
            float edgeRotation = rotation + spread * MathHelper.PiOver2 * 0.9f;
            Vector2 position = center + edgeRotation.ToRotationVector2() * 85f * scale;
            Dust dust = Dust.NewDustPerfect(position, DustID.BlueTorch,
                velocity * 0.7f, 100, new Color(90, 205, 255), 0.56f * opacity);
            dust.noGravity = true;
            dust.velocity -= velocity * Main.rand.NextFloat(0.08f, 0.20f);
            dust.position -= dust.velocity * 4f;
        }
    }

    /// <summary>
    /// 泰拉刃式命中星光：四向外层、两道横向切光和四向亮芯，整体改为亮蓝色。
    /// </summary>
    public static void SpawnTelekineticWaveImpact(Vector2 position, Vector2 movement,
        float scale)
    {
        if (Main.dedServ)
            return;

        const int lifetime = 30;
        float baseRotation = movement.SafeNormalize(Vector2.UnitX).ToRotation() + MathHelper.PiOver2;
        Color outer = new(55, 175, 255, 128);
        Color inner = new(135, 235, 255, 235);

        for (int i = 0; i < 4; i++)
        {
            Vector2 arm = (baseRotation + MathHelper.PiOver2 * i).ToRotationVector2() * 4f;
            Vector2 sparkleScale = new Vector2(3f, 0.5f) * 1.1f * scale;
            Vector2 sparkleVelocity = -arm * 0.2f;
            if ((i & 1) == 1)
            {
                sparkleScale *= 1.5f;
                sparkleVelocity *= 2f;
            }
            Sparkle(position, sparkleVelocity, outer, sparkleScale, lifetime,
                arm.ToRotation(), 0.35f);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 arm = (baseRotation + MathHelper.PiOver2 * side).ToRotationVector2() * 2f;
            Sparkle(position, arm.RotatedBy(MathHelper.PiOver2) * 0.5f,
                new Color(120, 230, 255, 170), new Vector2(3f, 0.5f) * 1.1f * scale,
                lifetime, arm.ToRotation(), 0.35f);
        }

        for (int i = 0; i < 4; i++)
        {
            Vector2 arm = (baseRotation + MathHelper.PiOver2 * i).ToRotationVector2() * 4f;
            Vector2 sparkleScale = new Vector2(3f, 0.5f) * 0.7f * scale;
            Vector2 sparkleVelocity = arm * 0.2f;
            if ((i & 1) == 1)
            {
                sparkleScale *= 1.5f;
                sparkleVelocity *= 2f;
            }
            Sparkle(position, sparkleVelocity, inner, sparkleScale, lifetime,
                arm.ToRotation(), 0.50f);

            Dust outward = Dust.NewDustPerfect(position, DustID.BlueTorch,
                arm.RotatedByRandom(MathHelper.TwoPi * 0.025f) * Main.rand.NextFloat(),
                80, new Color(95, 205, 255), 0.8f * scale);
            outward.noGravity = true;
            Dust inward = Dust.NewDustPerfect(position, DustID.BlueTorch,
                -arm.RotatedByRandom(MathHelper.TwoPi * 0.025f) * Main.rand.NextFloat(),
                60, Color.White, 1.15f * scale);
            inward.noGravity = true;
        }
    }

    /// <summary>
    /// 沿完整波刃轮廓释放三段亮蓝碎光，用于自然消失与十次伤害耗尽后的收束。
    /// </summary>
    public static void SpawnTelekineticWaveDissipation(Vector2 center, Vector2 velocity,
        float rotation, float scale)
    {
        if (Main.dedServ)
            return;

        SpawnWaveDissipationBand(center, velocity, rotation, scale, 20, 60f, 85f, 0f);
        SpawnWaveDissipationBand(center, velocity, rotation, scale, 10, 70f, 85f, 0.2f);
        SpawnWaveDissipationBand(center, velocity, rotation, scale, 10, 60f, 75f, 0.5f);
    }

    private static void SpawnWaveDissipationBand(Vector2 center, Vector2 velocity,
        float rotation, float scale, int count, float minimumRadius, float maximumRadius,
        float rearOffset)
    {
        Vector2 forward = rotation.ToRotationVector2();
        for (int i = 0; i < count; i++)
        {
            float spread = Main.rand.NextFloatDirection();
            float edgeRotation = rotation + spread * MathHelper.PiOver2 * 0.9f;
            Vector2 position = center + edgeRotation.ToRotationVector2() *
                Main.rand.NextFloat(minimumRadius, maximumRadius) * scale;
            float randomizedRearOffset = rearOffset + Main.rand.NextFloat() * 0.25f;
            position -= forward * randomizedRearOffset * 70f * scale;
            Dust dust = Dust.NewDustPerfect(position, DustID.BlueTorch,
                velocity.RotatedBy(spread * MathHelper.PiOver4) * 0.2f * Main.rand.NextFloat(),
                70, i % 4 == 0 ? Color.White : new Color(85, 200, 255),
                1.4f * (1f - System.MathF.Abs(spread)));
            dust.noGravity = true;
            dust.position -= dust.velocity * Main.rand.NextFloat() * 3f;
            dust.scale *= 1f - System.Math.Min(0.85f, randomizedRearOffset * 0.35f);
        }
    }

    public static void SpawnLightningFragments(Vector2 center, Color color, int count = 14, float scale = 1f)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.8f, 11f) * scale;
            FranParticleSystem.Spawn(new FranBoltParticle(center + Main.rand.NextVector2Circular(10f, 10f), velocity,
                i % 4 == 0 ? Color.White : color, Main.rand.NextFloat(0.18f, 0.34f) * scale,
                new Vector2(Main.rand.NextFloat(0.4f, 0.7f), Main.rand.NextFloat(1.1f, 1.9f)), Main.rand.Next(10, 18)));
        }
    }

    public static void SpawnMagicHearts(Vector2 center, Vector2 direction, Color color, int count = 7, float scale = 1f)
    {
        direction = direction.SafeNormalize(-Vector2.UnitY);
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = direction.RotatedByRandom(0.9f) * Main.rand.NextFloat(0.8f, 3.4f) * scale;
            FranParticleSystem.Spawn(new FranHeartParticle(center + Main.rand.NextVector2Circular(12f, 12f), velocity,
                i % 3 == 0 ? Color.White : color, Main.rand.NextFloat(0.17f, 0.31f) * scale,
                Main.rand.Next(24, 42), Main.rand.NextFloat(-0.026f, 0.026f), i == 0 && scale > 1.15f));
        }
    }

    public static void SpawnRuneActivation(Vector2 center, Color color, float scale = 1f)
    {
        SpawnFlowerPattern(center, color, Color.White, 0.72f * scale);
        SpawnConvergingMagic(center, color, 72f * scale, 14, scale);
        FranParticleSystem.Spawn(new FranDirectionalRingParticle(center, Vector2.Zero, color,
            Vector2.One * 0.12f * scale, Vector2.One * 0.92f * scale, 0f, 22, 0.035f));
    }

    public static void SpawnThunderImpact(Vector2 center, Color color, float scale = 1f)
    {
        SpawnLightningFragments(center, color, 20, scale);
        SpawnIonizingRadiation(center, Color.Lerp(color, Color.White, 0.4f), scale);
        FranParticleSystem.Spawn(new FranGlowOrbParticle(center, Vector2.Zero, color,
            0.72f * scale, 0.04f, 15, true));
        FranParticleSystem.Spawn(new FranDirectionalRingParticle(center, Vector2.Zero, color,
            new Vector2(0.08f, 0.12f), new Vector2(1.2f, 0.46f) * scale, 0f, 16));
    }

    /// <summary>
    /// 普通前三剑的原版池化闪光点缀。三种节奏分别使用十字闪、沿弧双闪与向刃聚拢，
    /// 只补充原有自定义粒子，不把不同剑式统一成同一种雷电效果。
    /// </summary>
    public static void SpawnShiFuSwingAccent(Vector2 position, Vector2 bladeDirection,
        Vector2 tangent, Color color, int combo, float intensity, int tick)
    {
        if (Main.dedServ || combo is < 0 or > 2 || intensity <= 0.08f)
            return;

        bladeDirection = bladeDirection.SafeNormalize(Vector2.UnitX);
        tangent = tangent.SafeNormalize(bladeDirection.RotatedBy(MathHelper.PiOver2));
        intensity = MathHelper.Clamp(intensity, 0f, 1f);

        switch (combo)
        {
            case 0:
                if (tick % 5 != 0)
                    return;
                Sparkle(position, tangent * 0.28f,
                    Color.Lerp(color, new Color(255, 224, 174), 0.20f),
                    new Vector2(1.45f, 0.24f) * (0.72f + intensity * 0.28f),
                    13, tangent.ToRotation(), 0.52f);
                Sparkle(position, bladeDirection * 0.12f, Color.White,
                    new Vector2(0.72f, 0.18f) * (0.68f + intensity * 0.24f),
                    10, bladeDirection.ToRotation(), 0.46f);
                break;

            case 1:
                if (tick % 6 != 0)
                    return;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 offset = bladeDirection * side * (3f + intensity * 3f);
                    Sparkle(position + offset, tangent * 0.34f,
                        side > 0 ? Color.White : Color.Lerp(color, Color.White, 0.44f),
                        new Vector2(1.08f, 0.22f) * (0.72f + intensity * 0.22f),
                        14, tangent.ToRotation(), 0.55f);
                }
                break;

            default:
                if (tick % 4 != 1)
                    return;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 offset = bladeDirection * side * 5f;
                    Sparkle(position + offset, tangent * (0.65f + intensity * 0.42f),
                        side > 0 ? Color.White : Color.Lerp(color, Color.White, 0.58f),
                        new Vector2(1.72f, 0.27f) * (0.78f + intensity * 0.30f),
                        15, tangent.ToRotation(), 0.62f);
                }
                break;
        }
    }

    /// <summary>第四刀沿剑身前移的闪星，集中在推出阶段，收剑时停止补发。</summary>
    public static void SpawnShiFuFinisherTrack(Vector2 origin, Vector2 bladeDirection,
        float reach, Color color, float progress, int tick)
    {
        if (Main.dedServ || (tick & 1) != 0 || progress > 0.24f)
            return;
        Vector2 forward = bladeDirection.SafeNormalize(Vector2.UnitX);
        Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
        bool release = progress >= 0.10f;
        float front = MathHelper.Lerp(0.38f, 0.94f, MathHelper.Clamp((progress - 0.06f) / 0.18f, 0f, 1f));
        for (int i = 0; i < 3; i++)
        {
            float lane = (i - 1) * 3f;
            Vector2 position = origin + forward * reach * (front - i * 0.07f) + normal * lane;
            Vector2 velocity = release ? forward * (3f + i) : forward - normal * lane * 0.15f;
            Sparkle(position, velocity, i == 1 ? Color.White : color,
                release ? new Vector2(0.90f, 0.16f) : new Vector2(0.28f, 0.28f),
                release ? 7 : 11, forward.ToRotation(), 0.65f);
        }
    }

    /// <summary>CLCB 星格的布局思路：少量错落光点加一枚十字主星，不铺满白色光带。</summary>
    public static void SpawnShiFuFinisherBurst(Vector2 center, Vector2 bladeDirection,
        Color color, float scale = 1f)
    {
        if (Main.dedServ)
            return;
        Vector2 forward = bladeDirection.SafeNormalize(Vector2.UnitX);
        Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
        for (int i = 0; i < 6; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector2 offset = -forward * (i / 2 * 8f) + normal * side * (3f + i * 0.6f);
            Sparkle(center + offset * scale, forward * 2.5f + normal * side * 0.4f,
                i % 3 == 0 ? new Color(255, 232, 179) : Color.Lerp(color, Color.White, 0.4f),
                new Vector2(0.32f, 0.32f) * scale, 12 + i % 3, i * 0.8f, 0.66f);
        }
        for (int axis = 0; axis < 2; axis++)
            Sparkle(center, forward * 1.5f, axis == 0 ? Color.White : color,
                new Vector2(axis == 0 ? 1.6f : 0.85f, 0.18f) * scale,
                8, forward.ToRotation() + axis * MathHelper.PiOver2, 0.78f);
    }
}
