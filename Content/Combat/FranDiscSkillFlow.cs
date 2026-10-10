using System;
using System.Collections.Generic;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// The four sword beats remain the accounting clock. Individual emissions are woven
// across that clock instead of replaying four simultaneous sword-release effects.
internal sealed class FranDiscSkillFlow
{
    internal readonly record struct Emission(FranSkill Skill, int Combo, int Index, int Count, int Layers);
    private readonly List<Emission>[] streams = new List<Emission>[FranSkillInfo.Count];
    private readonly int[] next = new int[FranSkillInfo.Count];
    private bool configured;

    internal void BeginBeat(Player owner, ReadOnlySpan<FranSkill> tactics, int beat, int cycle)
    {
        if (!configured || beat == 0)
        {
            configured = true;
            for (int s = 0; s < streams.Length; s++)
            {
                FranSkill skill = (FranSkill)s;
                int layers = 0;
                foreach (FranSkill value in tactics) if (value == skill) layers++;
                List<Emission> stream = streams[s] ??= new();
                stream.Clear();
                if (layers == 0) continue;
                int[] counts = new int[4];
                int largest = 0;
                for (int combo = 0; combo < 4; combo++)
                {
                    counts[combo] = ShiFuImprintCombat.DiscEmissionCount(owner, skill, combo, layers, cycle);
                    largest = Math.Max(largest, counts[combo]);
                }
                // Interleave the reduced budgets; don't collect the strongest fourth-beat
                // projectiles into another visible burst.
                for (int i = 0; i < largest; i++)
                    for (int combo = 0; combo < 4; combo++)
                        if (i < counts[combo]) stream.Add(new(skill, combo, i, counts[combo], layers));
            }
        }
        for (int s = 0; s < streams.Length; s++)
        {
            next[s] = 0;
            while (next[s] < streams[s].Count && ReleaseTime(s, next[s]) <= beat) next[s]++;
        }
    }

    // Different streams get different phases so mixed loadouts don't all flash together.
    private float ReleaseTime(int skill, int index) =>
        (index + 0.22f + skill * 0.071f) * 4f / streams[skill].Count;

    internal void Update(Player owner, FranSpinningDisc disc, float cycleProgress)
    {
        if (!configured || Main.myPlayer != owner.whoAmI) return;
        for (int s = 0; s < streams.Length; s++)
            while (next[s] < streams[s].Count && ReleaseTime(s, next[s]) <= cycleProgress)
            {
                Emission emission = streams[s][next[s]++];
                ShiFuImprintCombat.EmitDiscSkill(owner, disc, emission);
            }
    }
}

internal static partial class ShiFuImprintCombat
{
    // Each beat keeps four fifths of the left-hand projectile count over five cycles.
    // Offsets spread the missing projectiles across beats instead of blanking a whole burst.
    internal static int DiscBudgetCount(int leftCount, int cycle, int offset) =>
        Math.Max(0, ((Math.Max(0, cycle) + 1) * Math.Max(0, leftCount) * 4 + offset) / 5 -
            (Math.Max(0, cycle) * Math.Max(0, leftCount) * 4 + offset) / 5);

    // Integer groups retain exactly 70% of the left-hand count over ten cycles.
    internal static int DiscButterflyCount(int leftCount, int cycle, int offset) =>
        Math.Max(0, ((Math.Max(0, cycle) + 1) * Math.Max(0, leftCount) * 7 + offset) / 10 -
            (Math.Max(0, cycle) * Math.Max(0, leftCount) * 7 + offset) / 10);

    internal static int LeftTelekineticProjectileCount(Player owner, int combo, int layers)
    {
        List<FranTelekineticShot> shots = new(16);
        FranTelekineticPattern.Fill(shots, FranBalance.GetTelekineticPatternStage(), combo, layers, owner.direction);
        int count = 0;
        foreach (FranTelekineticShot shot in shots) count += shot.Count;
        return count;
    }

    internal static int DiscEmissionCount(Player owner, FranSkill skill, int combo, int layers, int cycle)
    {
        bool full = owner.GetModPlayer<FranPlayer>().IsFullTacticLoadout(skill);
        switch (skill)
        {
            case FranSkill.TelekineticCatapult:
                return DiscBudgetCount(LeftTelekineticProjectileCount(owner, combo, layers), cycle, combo);
            case FranSkill.PressurizedQuickdraw when !full:
                int leftCount = Math.Max(1, (int)MathF.Round(Math.Max(1, FranBalance.GetQuickdrawAirburstCount(combo)) *
                    FranBalance.GetFloat(nameof(FranBalance.QuickdrawNonFullEmissionMultiplier), FranBalance.QuickdrawNonFullEmissionMultiplier),
                    MidpointRounding.AwayFromZero));
                return DiscBudgetCount(leftCount, cycle, combo + 3);
            default:
                return 0;
        }
    }

    internal static void EmitDiscSkill(Player owner, FranSpinningDisc disc,
        FranDiscSkillFlow.Emission emission)
    {
        Projectile attack = disc.Projectile;
        int combo = emission.Combo;
        int layers = emission.Layers;
        int damage = disc.SkillDamageForBeat(combo);
        Vector2 blade = attack.rotation.ToRotationVector2();
        bool full = owner.GetModPlayer<FranPlayer>().IsFullTacticLoadout(emission.Skill);
        switch (emission.Skill)
        {
            case FranSkill.TelekineticCatapult:
                int stage = FranBalance.GetTelekineticPatternStage();
                List<FranTelekineticShot> reference = new(16);
                FranTelekineticPattern.Fill(reference, stage, combo, layers, owner.direction);
                float weight = 0f;
                foreach (FranTelekineticShot shot in reference)
                    weight += shot.Weight * shot.Count * FranTelekineticPattern.GetHitWeight(shot.Kind);
                float perCore = weight * FranTelekineticPattern.GetDamageScale(stage, FranBalance.GetAvailableTacticSlots()) /
                    Math.Max(1, LeftTelekineticProjectileCount(owner, combo, layers));
                // The live rotating edge determines the scatter direction; a small jitter keeps
                // it organic. Origins follow the disc even while the player drags it around.
                Vector2 direction = blade.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f));
                SpawnTelekineticPressureCore(owner, attack, direction, 0f, perCore,
                    attack.Center + direction * disc.Radius * 0.82f, damage);
                DiscReleaseDust(attack.Center + direction * disc.Radius * 0.82f, direction, emission.Skill);
                break;
            case FranSkill.PressurizedQuickdraw:
                float scale = FranBalance.GetQuickdrawAirburstDamageMultiplier(combo) *
                    FranBalance.GetFloat(nameof(FranBalance.QuickdrawNonFullDamageMultiplier), FranBalance.QuickdrawNonFullDamageMultiplier) *
                    (1f + FranBalance.GetFloat(nameof(FranBalance.QuickdrawExtraLayerDamageMultiplier),
                        FranBalance.QuickdrawExtraLayerDamageMultiplier) * (layers - 1)) * FranTacticOutputBudget.Scale(emission.Skill);
                disc.EmitOrbitingAirbursts(Math.Max(1, (int)(damage * scale)), 0.8f, fullLoadout: false);
                break;
        }
    }

    private static void DiscReleaseDust(Vector2 position, Vector2 direction, FranSkill skill)
    {
        if (!Main.dedServ)
            FranVanillaDustEffects.SkillRelease(position, direction.RotatedBy(MathHelper.PiOver2),
                skill, FranSkillInfo.Color(skill), 2, 0.55f);
    }

}
