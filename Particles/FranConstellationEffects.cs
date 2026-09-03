using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace FranMod.Particles;


internal static class FranConstellationEffects
{
    private static readonly Color ResonanceCyan = new(78, 202, 255);

    public static void SpawnBlueResonancePrelude(Vector2 center, Vector2 attackDirection, int combo)
    {
        if (Main.dedServ)
            return;

        int count = combo == 3 ? 4 : 5;
        FranConstellationGroup group = new(count);
        float baseAngle = attackDirection.SafeNormalize(Vector2.UnitX).ToRotation() + Main.rand.NextFloat(-0.28f, 0.28f);
        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + MathHelper.TwoPi * i / count;
            Vector2 radial = angle.ToRotationVector2();
            Vector2 position = center + radial * Main.rand.NextFloat(43f, 67f);
            Vector2 velocity = radial.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(0.42f, 0.78f);
            Color color = Color.Lerp(ResonanceCyan, new Color(186, 155, 255), i / (float)Math.Max(1, count - 1) * 0.32f);
            FranParticleSystem.Spawn(new FranConstellationMoteParticle(group, i, position, velocity,
                center, color, angle, Main.rand.NextFloat(9f, 13f)));
        }

        // Keep roughly one fifth of the release vocabulary in vanilla Dust, matching Fran's wider VFX mix.
        Dust dust = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(18f, 18f),
            DustID.TintableDustLighted, Main.rand.NextVector2Circular(0.8f, 0.8f), 100,
            Color.Lerp(ResonanceCyan, Color.White, 0.45f), 0.9f);
        dust.noGravity = true;
    }

    public static void SpawnPolarRelease(Vector2 center, Vector2 attackDirection, float scale)
    {
        if (Main.dedServ)
            return;

        scale = MathHelper.Clamp(scale, 0.65f, 1.35f);
        FranParticleSystem.Spawn(new FranBloomParticle(center, Vector2.Zero, ResonanceCyan,
            20f * scale, 78f * scale, 18, 0.82f, hardCenter: true));
        FranParticleSystem.Spawn(new FranRingPulseParticle(center, attackDirection.SafeNormalize(Vector2.UnitX) * 0.22f,
            ResonanceCyan, new Vector2(18f, 10f) * scale, new Vector2(110f, 54f) * scale,
            20, attackDirection.ToRotation(), 0.025f, 0.88f));

        float baseAngle = attackDirection.SafeNormalize(Vector2.UnitX).ToRotation();
        for (int i = 0; i < 5; i++)
        {
            Vector2 ray = (baseAngle + MathHelper.TwoPi * i / 5f).ToRotationVector2();
            Color color = i == 0 ? Color.White : Color.Lerp(ResonanceCyan, Color.White, 0.44f);
            FranParticleSystem.Spawn(new FranNeedleParticle(center + ray * 6f, ray * (2.6f + i * 0.16f),
                color, new Vector2(48f, 10f) * scale, new Vector2(12f, 3f) * scale,
                16, ray.ToRotation(), 0.91f));
        }

        for (int i = 0; i < 2; i++)
        {
            Vector2 dustVelocity = (baseAngle + MathHelper.TwoPi * i / 2f + Main.rand.NextFloat(-0.5f, 0.5f))
                .ToRotationVector2() * Main.rand.NextFloat(1.8f, 3.2f);
            Dust dust = Dust.NewDustPerfect(center, i == 0 ? DustID.Electric : DustID.TintableDustLighted,
                dustVelocity, 90, Color.Lerp(ResonanceCyan, Color.White, 0.55f), 1.05f * scale);
            dust.noGravity = true;
        }
    }
}
