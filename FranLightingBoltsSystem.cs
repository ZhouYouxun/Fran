using FranMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Renderers;
using Terraria.ModLoader;

namespace FranMod;

// Ported and consolidated from the user's CLCBLightingBoltsSystem for Fran's private build.
// Terraria's pooled particles and Fran's custom particles remain separate systems on purpose.
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
}
