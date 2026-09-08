using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace FranMod.Particles;

// 每刀有自己的主材质和节奏；细光、星点、电丝交错，文字只作为第二刀的主识别层。
internal static class FranTeacherSwingEffects
{
    private static void Spawn(FranParticle particle) =>
        FranParticleSystem.Spawn(particle, spawnSupportingDust: false);

    public static void Swing(Vector2 origin, Vector2 position, Vector2 blade, Vector2 tangent,
        Color color, int combo, int tick, float activity)
    {
        if (Main.dedServ || activity <= 0.08f)
            return;

        Color accent = Color.Lerp(color, combo == 0 ? new Color(255, 213, 143) :
            new Color(190, 146, 255), combo == 0 ? 0.32f : 0.28f);
        Vector2 drift = tangent * Main.rand.NextFloat(2.8f, 5.2f);
        Vector2 scatter = position + Main.rand.NextVector2Circular(7f, 7f);

        // All three cuts mix a moving light with alternating crisp stars and short lightning.
        Spawn(new FranLightParticle(scatter, drift + blade * 0.6f,
            Main.rand.NextFloat(0.13f, 0.21f), accent, 10, 0.8f, 1.5f, 2f));
        if (tick % 3 == 0)
            Spawn(new FranStarMoteParticle(scatter, drift * 0.35f, Color.White,
                accent, combo == 1 ? 0.24f : 0.18f, Main.rand.Next(12, 19)));
        if (tick % (combo == 2 ? 2 : 5) == 1)
            Spawn(new FranSwordFilamentParticle(scatter, drift * 0.6f, tangent,
                accent, combo == 2 ? 38f : 23f, 7, combo == 1 ? 0.045f : -0.02f));

        switch (combo)
        {
            case 0:
                if ((tick & 1) == 0)
                    Spawn(new FranBladeEdgeMoteParticle(scatter, drift * 1.5f + blade * 1.8f,
                        tangent, accent, Main.rand.NextFloat(12f, 22f), 1.1f, 9));
                break;
            case 1:
                if (tick % 3 == 1)
                {
                    // 内圈文字与外圈光点错开；全五种字形轮换，短暂漂浮而非拖成长光带。
                    Vector2 glyphPosition = Vector2.Lerp(origin, position, 0.82f);
                    int glyph = (tick / 3) % 5 + 1;
                    Spawn(new FranMysticGlyphParticle(glyphPosition, tangent * 0.85f + blade * 0.35f,
                        accent, glyph, 17f, 22f, 23, Main.rand.NextFloat(-0.25f, 0.25f),
                        Math.Sign(tangent.X) * 0.015f));
                }
                break;
            case 2:
                Spawn(new FranBladeEdgeMoteParticle(scatter, tangent * Main.rand.NextFloat(7f, 11f),
                    tangent, Color.Lerp(color, Color.White, 0.38f),
                    Main.rand.NextFloat(22f, 36f), 1.5f, 8, 0.86f));
                break;
        }
    }

    public static void FinisherFlow(Vector2 origin, Vector2 blade, float reach, Color color,
        float progress, int tick)
    {
        if (Main.dedServ)
            return;

        Vector2 side = blade.RotatedBy(MathHelper.PiOver2);
        Color violet = Color.Lerp(color, new Color(193, 142, 255), 0.36f);
        bool gathering = progress < 0.18f;
        bool scattering = progress > 0.52f;
        int count = scattering ? 2 : 4;
        for (int i = 0; i < count; i++)
        {
            float sequence = (tick * 0.381966f + i * 0.271f) % 1f;
            float distance = reach * MathHelper.Lerp(0.26f, 0.82f, sequence);
            float lane = MathF.Sin(tick * 1.35f + i * MathHelper.PiOver2) *
                (gathering ? 23f : scattering ? 18f : 10f);
            Vector2 position = origin + blade * distance + side * lane;
            Vector2 velocity = gathering
                ? blade * 5f - side * lane * 0.32f
                : blade * Main.rand.NextFloat(scattering ? 3f : 13f, scattering ? 6f : 19f) +
                    side * lane * (scattering ? 0.24f : -0.06f);
            Color tint = i % 2 == 0 ? color : violet;
            Spawn(new FranLightParticle(position, velocity, Main.rand.NextFloat(0.13f, 0.22f),
                tint, scattering ? 8 : 7, 0.86f, 1.7f, 2.7f));
            if (!gathering && !scattering && i < 2)
                Spawn(new FranBladeEdgeMoteParticle(position, velocity * 0.82f, blade,
                    tint, Main.rand.NextFloat(20f, 34f), 1.25f, 7, 0.82f));
        }

        if ((tick & 1) == 0)
        {
            Vector2 sparkPosition = origin + blade * reach * Main.rand.NextFloat(0.40f, 0.82f) +
                side * Main.rand.NextFloat(-18f, 18f);
            Spawn(new FranSwordFilamentParticle(sparkPosition, blade * (scattering ? 2f : 7f),
                scattering ? side : blade, violet, Main.rand.NextFloat(24f, 48f), 6));
            Spawn(new FranStarMoteParticle(sparkPosition, side * Main.rand.NextFloat(-2.5f, 2.5f),
                Color.White, color, 0.18f, 12));
        }

        global::FranMod.FranLightingBoltsSystem.SpawnTeacherFinisherTrack(
            origin, blade, reach, color, progress, tick);
    }

    public static void FinisherPulse(Vector2 origin, Vector2 blade, float reach, Color color, int stage)
    {
        if (Main.dedServ)
            return;

        Vector2 side = blade.RotatedBy(MathHelper.PiOver2);
        Vector2 center = origin + blade * reach * (0.38f + stage * 0.21f);
        Color accent = Color.Lerp(color, new Color(206, 157, 255), stage * 0.18f);
        // 三个脉冲依次沿剑身向前，横向碎屑和前冲细丝穿插，不把闪光全部堆在一点。
        for (int i = 0; i < 8; i++)
        {
            float spread = (i - 3.5f) / 3.5f;
            Vector2 velocity = blade * Main.rand.NextFloat(8f, 16f) + side * spread * (5f + stage * 2f);
            Vector2 position = center + side * spread * 9f;
            Spawn(new FranBladeEdgeMoteParticle(position, velocity, velocity,
                i % 3 == 0 ? Color.White : accent, 19f + stage * 4f, 1.3f, 8, 0.85f));
            if ((i & 1) == 0)
                Spawn(new FranStarMoteParticle(position, side * spread * 3.2f + blade * 1.1f,
                    Color.White, accent, 0.20f, 14));
        }
        Spawn(new FranDirectionalRingParticle(center, blade * 3f, accent,
            new Vector2(0.03f, 0.07f), new Vector2(0.20f, 0.48f), blade.ToRotation(), 8));
        for (int sign = -1; sign <= 1; sign += 2)
            Spawn(new FranSwordFilamentParticle(center + side * sign * 14f,
                blade * 8f + side * sign * 2f, blade, accent, 44f, 7));
        global::FranMod.FranLightingBoltsSystem.SpawnTeacherFinisherBurst(center, blade, accent,
            0.65f + stage * 0.10f);
    }
}
