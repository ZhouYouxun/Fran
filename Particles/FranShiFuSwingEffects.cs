using System;
using FranMod.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace FranMod.Particles;

// 每刀有自己的主材质和节奏；细光、星点、电丝交错，文字只作为第二刀的主识别层。
internal static class FranShiFuSwingEffects
{
    private static void Spawn(FranParticle particle) =>
        FranParticleSystem.Spawn(particle, spawnSupportingDust: false);

    public static void Swing(Vector2 origin, Vector2 position, Vector2 tangent,
        Color color, int combo, int tick, float activity, int lifetime, bool resonance = false)
    {
        if (Main.dedServ || activity <= 0.08f)
            return;

        Color accent = resonance ? FranResonanceVisuals.Cyan : Color.Lerp(color,
            combo == 0 ? new Color(255, 213, 143) : new Color(190, 146, 255), combo == 0 ? 0.32f : 0.28f);
        Vector2 drift = tangent * Main.rand.NextFloat(1.5f, 3f) * MathHelper.Lerp(0.65f, 1f, activity);
        Vector2 scatter = position + tangent * Main.rand.NextFloat(-1.5f, 1.5f);

        // tick 是剑路采样序号。不同点缀分占刃口上的不同采样点，沿同一切线脱离。
        if ((tick & 1) == 0)
            Spawn(new FranLightParticle(scatter, drift,
                Main.rand.NextFloat(0.13f, 0.21f), accent, Math.Min(lifetime, 10), 0.8f, 1.5f, 2f));
        if (tick % 4 == 2)
            Spawn(new FranStarMoteParticle(scatter, drift * 0.35f, Color.White,
                accent, combo == 1 ? 0.24f : 0.18f, lifetime));
        if (tick % (combo == 2 ? 3 : 6) == 1)
            Spawn(new FranSwordFilamentParticle(scatter, drift * 0.6f, tangent,
                accent, combo == 2 ? 38f : 23f, Math.Min(lifetime, 7), combo == 1 ? 0.045f : -0.02f));

        switch (combo)
        {
            case 0:
                if (tick % 3 == 0)
                    Spawn(new FranBladeEdgeMoteParticle(scatter, drift * 1.5f,
                        tangent, accent, Main.rand.NextFloat(12f, 22f), 1.1f, Math.Min(lifetime, 9)));
                break;
            case 1:
                if (tick % 4 == 2)
                {
                    // 内圈文字与外圈光点错开；全五种字形轮换，短暂漂浮而非拖成长光带。
                    Vector2 glyphPosition = Vector2.Lerp(origin, position, 0.82f);
                    int glyph = (tick / 4) % 5 + 1;
                    Spawn(new FranMysticGlyphParticle(glyphPosition, drift * 0.25f,
                        accent, glyph, 17f, 22f, lifetime, Main.rand.NextFloat(-0.25f, 0.25f),
                        Math.Sign(tangent.X) * 0.015f));
                }
                break;
            case 2:
                if ((tick & 1) == 0)
                    Spawn(new FranBladeEdgeMoteParticle(scatter, tangent * Main.rand.NextFloat(5f, 8f),
                        tangent, Color.Lerp(color, Color.White, 0.38f),
                        Main.rand.NextFloat(22f, 36f), 1.5f, Math.Min(lifetime, 8), 0.86f));
                break;
        }
    }

    public static void FinisherFlow(Vector2 origin, Vector2 blade, float reach, Color color,
        float progress, int tick, bool resonance = false)
    {
        if (Main.dedServ)
            return;

        Vector2 side = blade.RotatedBy(MathHelper.PiOver2);
        Color violet = resonance ? FranResonanceVisuals.Cyan : Color.Lerp(color, new Color(193, 142, 255), 0.36f);
        bool gathering = progress < 0.10f;
        bool scattering = progress > 0.24f;
        int count = scattering ? 1 : gathering ? 2 : 3;
        float front = MathHelper.Lerp(0.38f, 0.94f, MathHelper.Clamp((progress - 0.06f) / 0.18f, 0f, 1f));
        for (int i = 0; i < count; i++)
        {
            float distance = reach * (front - i * 0.08f);
            float lane = (i % 2 == 0 ? -1f : 1f) * (gathering ? 5f : 3f);
            Vector2 position = origin + blade * distance + side * lane;
            Vector2 velocity = gathering
                ? blade * 3f - side * lane * 0.15f
                : blade * Main.rand.NextFloat(scattering ? 1f : 7f, scattering ? 2f : 11f);
            Color tint = i % 2 == 0 ? color : violet;
            Spawn(new FranLightParticle(position, velocity, Main.rand.NextFloat(0.13f, 0.22f),
                tint, scattering ? 8 : 7, 0.86f, 1.7f, 2.7f));
            if (!gathering && !scattering && i < 2)
                Spawn(new FranBladeEdgeMoteParticle(position, velocity * 0.82f, blade,
                    tint, Main.rand.NextFloat(20f, 34f), 1.25f, 7, 0.82f));
        }

        if (tick % 3 == 1 && !scattering)
        {
            Vector2 sparkPosition = origin + blade * reach * front + side * Main.rand.NextFloat(-3f, 3f);
            Spawn(new FranSwordFilamentParticle(sparkPosition, blade * 4f,
                blade, violet, Main.rand.NextFloat(24f, 48f), 6));
            Spawn(new FranStarMoteParticle(sparkPosition, blade * 1.2f,
                Color.White, color, 0.18f, 12));
        }

        if (tick % 4 == 0 && !scattering)
        {
            Dust dust = Dust.NewDustPerfect(origin + blade * reach * front,
                DustID.FireworksRGB, blade * 1.2f, 90, color, 0.55f);
            dust.noGravity = true;
        }

        global::FranMod.FranLightingBoltsSystem.SpawnShiFuFinisherTrack(
            origin, blade, reach, color, progress, tick);
    }

    public static void FinisherPulse(Vector2 center, Vector2 blade, Color color, int stage, bool resonance = false)
    {
        if (Main.dedServ)
            return;

        Vector2 side = blade.RotatedBy(MathHelper.PiOver2);
        Color accent = Color.Lerp(color, resonance ? FranResonanceVisuals.Cyan : new Color(206, 157, 255), stage * 0.18f);
        // 推出时依次点亮剑身前半段、刃端和剑尖，碎屑保持窄幅前冲。
        int count = 4 + stage * 2;
        for (int i = 0; i < count; i++)
        {
            float spread = i / (float)(count - 1) * 2f - 1f;
            Vector2 velocity = blade * Main.rand.NextFloat(4f + stage * 2f, 7f + stage * 3f) + side * spread * 1.3f;
            Vector2 position = center + side * spread * 4f;
            Spawn(new FranBladeEdgeMoteParticle(position, velocity, velocity,
                i % 3 == 0 ? Color.White : accent, 19f + stage * 4f, 1.3f, 8, 0.85f));
            if ((i & 1) == 0)
                Spawn(new FranStarMoteParticle(position, side * spread * 0.5f + blade * 1.1f,
                    Color.White, accent, 0.20f, 14));
        }
        Spawn(new FranDirectionalRingParticle(center, blade * 3f, accent,
            new Vector2(0.03f, 0.07f), new Vector2(0.20f, 0.48f), blade.ToRotation(), 8));
        for (int sign = -1; sign <= 1; sign += 2)
            Spawn(new FranSwordFilamentParticle(center + side * sign * 4f,
                blade * 6f + side * sign * 0.5f, blade, accent, 44f, 7));
        global::FranMod.FranLightingBoltsSystem.SpawnShiFuFinisherBurst(center, blade, accent,
            0.65f + stage * 0.10f);
    }
}
