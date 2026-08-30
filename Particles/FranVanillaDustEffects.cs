using System;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace FranMod.Content.Projectiles;

// Vanilla Dust is a supporting material layer, not a replacement for authored projectile bodies.
// One Dust per four custom particles gives Dust roughly 20% of the combined particle count:
// dust / (custom + dust) = 1 / 5. Every method below uses that same bounded budget rule.
internal static class FranVanillaDustEffects
{
    private const float TargetDustShare = 0.20f;

    public static void SwingTrail(Vector2 origin, Vector2 tip, Vector2 tangent, FranSkill skill,
        Color color, int combo, int tick)
    {
        if (Main.dedServ)
            return;

        int count = combo == 2 && tick % 2 == 0 ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            Vector2 position = Vector2.Lerp(origin, tip, Main.rand.NextFloat(0.34f, 1f)) +
                Main.rand.NextVector2Circular(3f, 3f);
            Vector2 velocity = tangent.SafeNormalize(Vector2.UnitY).RotatedByRandom(combo == 2 ? 0.34f : 0.20f) *
                Main.rand.NextFloat(1.8f, combo == 2 ? 6.2f : 4.1f);
            Spawn(position, velocity, skill, color, Main.rand.NextFloat(0.86f, combo == 2 ? 1.22f : 1.06f),
                tick + i, smokeChance: skill == FranSkill.PressurizedQuickdraw ? 4 : 10);
        }
    }

    public static void SkillRelease(Vector2 center, Vector2 direction, FranSkill skill, Color color,
        int customParticleEstimate = 8, float scale = 1f)
    {
        if (Main.dedServ)
            return;

        direction = direction.SafeNormalize(Vector2.UnitX);
        int count = DustCountForCustom(customParticleEstimate);
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = direction.RotatedByRandom(0.48f) * Main.rand.NextFloat(2.2f, 6.8f) * scale;
            Vector2 position = center + direction * Main.rand.NextFloat(2f, 18f) +
                Main.rand.NextVector2Circular(6f, 6f) * scale;
            Spawn(position, velocity, skill, color, Main.rand.NextFloat(0.90f, 1.28f) * scale,
                i, smokeChance: skill == FranSkill.PressurizedQuickdraw ? 3 : 9);
        }
    }

    public static void Impact(Vector2 center, Vector2 direction, FranSkill skill, Color color,
        int customParticleEstimate, float scale = 1f)
    {
        if (Main.dedServ)
            return;

        direction = direction.SafeNormalize(Vector2.UnitX);
        int count = DustCountForCustom(customParticleEstimate);
        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = direction.RotatedByRandom(1.05f) * Main.rand.NextFloat(2.4f, 8.4f) * scale;
            Spawn(center + Main.rand.NextVector2Circular(8f, 8f) * scale, velocity, skill,
                i % 4 == 0 ? Color.Lerp(color, Color.White, 0.72f) : color,
                Main.rand.NextFloat(0.88f, 1.32f) * scale, i,
                smokeChance: skill == FranSkill.PressurizedQuickdraw ? 3 : 8);
        }
    }

    public static void Burst(Vector2 center, Vector2 direction, Color color, FranBurstStyle style,
        int customParticleEstimate, float scale = 1f)
    {
        FranSkill skill = style switch
        {
            FranBurstStyle.Telekinetic => FranSkill.TelekineticCatapult,
            FranBurstStyle.SkillTaker => FranSkill.SkillTaker,
            FranBurstStyle.Barrier => FranSkill.MagicBarrier,
            FranBurstStyle.SwordArray => FranSkill.MultipleBodyCreation,
            FranBurstStyle.Lightning => FranSkill.ThunderChain,
            FranBurstStyle.ThorsHammer => FranSkill.ThorsHammer,
            FranBurstStyle.AirDraw => FranSkill.PressurizedQuickdraw,
            _ => FranSkill.None
        };
        Impact(center, direction, skill, color, customParticleEstimate, scale);
    }

    public static void ProjectileTrail(Vector2 center, Vector2 velocity, FranSkill skill, Color color,
        float spawnChance = 0.28f, float scale = 1f)
    {
        if (Main.dedServ || !Main.rand.NextBool((int)MathHelper.Clamp(MathF.Round(1f / Math.Max(0.05f, spawnChance)), 1f, 20f)))
            return;

        Vector2 back = -velocity.SafeNormalize(Vector2.UnitX);
        Spawn(center + Main.rand.NextVector2Circular(4f, 4f),
            back.RotatedByRandom(0.42f) * Main.rand.NextFloat(0.5f, 2.4f), skill, color,
            Main.rand.NextFloat(0.72f, 1.02f) * scale, unchecked((long)Main.GameUpdateCount),
            smokeChance: skill == FranSkill.PressurizedQuickdraw ? 4 : 12);
    }

    private static int DustCountForCustom(int customParticleEstimate)
    {
        if (customParticleEstimate <= 0)
            return 0;
        float dustPerCustom = TargetDustShare / (1f - TargetDustShare);
        return Math.Clamp((int)MathF.Ceiling(customParticleEstimate * dustPerCustom), 1, 12);
    }

    private static void Spawn(Vector2 position, Vector2 velocity, FranSkill skill, Color color,
        float scale, long sequence, int smokeChance)
    {
        int dustType = DustType(skill);
        bool smoke = smokeChance > 0 && sequence % smokeChance == 0;
        if (smoke)
            dustType = DustID.Smoke;

        Dust dust = Dust.NewDustPerfect(position, dustType, velocity, smoke ? 90 : 35,
            smoke ? new Color(80, 90, 105) : color, MathHelper.Clamp(scale, 0.55f, 1.8f));
        dust.noGravity = !smoke;
        dust.fadeIn = smoke ? 0.85f : MathHelper.Clamp(scale + 0.18f, 0.8f, 1.8f);
    }

    private static int DustType(FranSkill skill) => skill switch
    {
        FranSkill.TelekineticCatapult => DustID.BlueTorch,
        FranSkill.SkillTaker => DustID.Shadowflame,
        FranSkill.MagicBarrier => DustID.BlueTorch,
        FranSkill.MultipleBodyCreation => DustID.WhiteTorch,
        FranSkill.BlueResonance => DustID.BlueTorch,
        FranSkill.ThunderChain => DustID.Electric,
        FranSkill.PressurizedQuickdraw => DustID.FireworksRGB,
        FranSkill.ThorsHammer => DustID.Electric,
        _ => DustID.FireworksRGB
    };
}
