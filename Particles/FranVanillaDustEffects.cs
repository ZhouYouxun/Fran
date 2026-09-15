using System;
using FranMod.Core;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace FranMod.Content.Projectiles;

// Every four accepted Fran particles produce exactly one supporting vanilla Dust:
// 4 custom + 1 Dust = a 20% vanilla share. The emission inherits authored motion and color
// from the particle/event instead of rolling an unrelated random direction.
internal static class FranVanillaDustEffects
{
    private readonly record struct DustIntent(Vector2 Position, Vector2 Direction, FranSkill Skill,
        Color Color, float Scale, ulong ExpiresAt);

    private static long acceptedCustomParticles;
    private static DustIntent latestIntent;

    internal static void ResetBudget()
    {
        acceptedCustomParticles = 0;
        latestIntent = default;
    }

    internal static void RegisterCustomParticle(FranParticle particle)
    {
        if (Main.dedServ || particle is null)
            return;

        acceptedCustomParticles++;
        if (acceptedCustomParticles % 4 != 0)
            return;

        bool intentActive = latestIntent.ExpiresAt >= Main.GameUpdateCount;
        Vector2 direction = particle.Velocity.LengthSquared() > 0.04f
            ? particle.Velocity.SafeNormalize(Vector2.UnitY)
            : intentActive ? latestIntent.Direction : Vector2.UnitY;
        float speed = MathHelper.Clamp(particle.Velocity.Length() * 0.24f + 0.8f, 0.8f, 5.8f);
        int cadence = (int)((acceptedCustomParticles / 4) % 4);
        float angleOffset = cadence switch { 0 => -0.22f, 1 => 0.12f, 2 => 0.26f, _ => -0.10f };
        Vector2 velocity = direction.RotatedBy(angleOffset) * speed;
        Vector2 tangent = direction.RotatedBy(MathHelper.PiOver2) * (cadence % 2 == 0 ? 1.8f : -1.8f);
        Vector2 position = particle.Position - direction * 3f + tangent;
        FranSkill skill = intentActive ? latestIntent.Skill : SkillForParticle(particle);
        Color color = intentActive ? Color.Lerp(particle.Color, latestIntent.Color, 0.36f) : particle.Color;
        float scale = intentActive ? latestIntent.Scale :
            MathHelper.Clamp(0.72f + particle.Scale * 0.18f, 0.65f, 1.25f);
        SpawnDesigned(position, velocity, skill, color, scale, cadence);
    }

    public static void SwingTrail(Vector2 origin, Vector2 tip, Vector2 tangent, FranSkill skill,
        Color color, int combo, int tick) =>
        RecordIntent(Vector2.Lerp(origin, tip, 0.72f), tangent, skill, color, combo == 2 ? 1.12f : 0.92f);

    public static void SkillRelease(Vector2 center, Vector2 direction, FranSkill skill, Color color,
        int customParticleEstimate = 8, float scale = 1f) =>
        RecordIntent(center, direction, skill, color, scale);

    public static void Impact(Vector2 center, Vector2 direction, FranSkill skill, Color color,
        int customParticleEstimate, float scale = 1f) =>
        RecordIntent(center, direction, skill, color, scale);

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
        RecordIntent(center, direction, skill, color, scale);
    }

    public static void ProjectileTrail(Vector2 center, Vector2 velocity, FranSkill skill, Color color,
        float spawnChance = 0.28f, float scale = 1f) =>
        RecordIntent(center, -velocity, skill, color, scale);

    private static void RecordIntent(Vector2 position, Vector2 direction, FranSkill skill, Color color, float scale)
    {
        if (Main.dedServ)
            return;
        latestIntent = new DustIntent(position, direction.SafeNormalize(Vector2.UnitY), skill, color,
            MathHelper.Clamp(scale, 0.55f, 1.5f), Main.GameUpdateCount + 2);
    }

    private static void SpawnDesigned(Vector2 position, Vector2 velocity, FranSkill skill, Color color,
        float scale, int cadence)
    {
        bool smoke = skill == FranSkill.PressurizedQuickdraw && cadence == 0;
        int dustType = smoke ? DustID.Smoke : DustType(skill);
        Dust dust = Dust.NewDustPerfect(position, dustType, velocity, smoke ? 90 : 35,
            smoke ? Color.Lerp(color, new Color(72, 78, 88), 0.72f) : color,
            MathHelper.Clamp(scale, 0.55f, 1.5f));
        dust.noGravity = !smoke;
        dust.fadeIn = smoke ? 0.84f : MathHelper.Clamp(scale + 0.14f, 0.78f, 1.55f);
    }

    private static FranSkill SkillForParticle(FranParticle particle) => particle switch
    {
        FranBoltParticle => FranSkill.ThunderChain,
        FranToxicMoteParticle => FranSkill.SkillTaker,
        FranWaterParticle => FranSkill.BlueResonance,
        FranBarrierShardParticle => FranSkill.MagicBarrier,
        FranNeedleParticle => FranSkill.PressurizedQuickdraw,
        FranBlackLightCoreParticle => FranSkill.SkillTaker,
        _ => FranSkill.None
    };

    private static int DustType(FranSkill skill) => skill switch
    {
        FranSkill.TelekineticCatapult => DustID.BlueTorch,
        FranSkill.SkillTaker => DustID.Shadowflame,
        FranSkill.MagicBarrier => DustID.BlueTorch,
        FranSkill.MultipleBodyCreation => DustID.WhiteTorch,
        FranSkill.BlueResonance => DustID.GreenTorch,
        FranSkill.ThunderChain => DustID.Electric,
        FranSkill.PressurizedQuickdraw => DustID.FireworksRGB,
        FranSkill.ThorsHammer => DustID.Electric,
        _ => DustID.FireworksRGB
    };
}
