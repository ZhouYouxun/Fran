using System;
using FranMod.Core;

namespace FranMod.Content.Companions;

/// <summary>Pure combat policy: no world reads, random decisions, or per-frame allocations.</summary>
public static class FranCompanionDecision
{
    public const int EvaluationTicks = 30;
    public const int MinimumHoldTicks = 180;
    public const float SwitchMargin = 18f;

    public readonly record struct Situation(bool HasTarget, bool Boss, bool Durable, int Enemies,
        int ClusteredEnemies, float Distance, float Speed, float MissingHealthRatio,
        bool Rain, bool Thunderstorm, bool CanMelee, bool MarkEstablished);

    public static float Score(FranSkill tactic, Situation s)
    {
        if (tactic == FranSkill.MagicBarrier || !FranSkillInfoId(tactic))
            return float.NegativeInfinity;
        if (!s.HasTarget)
            return tactic == FranSkill.BlueResonance && s.MissingHealthRatio >= .20f ? 120f : 0f;
        return tactic switch
        {
            FranSkill.TelekineticCatapult => 40f + (!s.CanMelee ? 40 : 0) +
                (s.Speed > 9f ? 20 : 0) + (s.Enemies > 1 && s.ClusteredEnemies < 3 ? 10 : 0),
            FranSkill.SkillTaker => 25f + (s.Durable ? 45 : 0) + (s.CanMelee ? 22 : -35) +
                (s.MarkEstablished ? 18 : 0),
            FranSkill.MultipleBodyCreation => 38f + (s.Enemies >= 2 ? 32 : 0) +
                (s.Distance is > 160 and < 620 ? 20 : 0) - (s.Speed > 15 ? 15 : 0),
            FranSkill.BlueResonance => s.MissingHealthRatio >= .40f ? 200f :
                s.MissingHealthRatio >= .20f ? 140f : s.MissingHealthRatio >= .10f ? 55f : 15f,
            FranSkill.ThunderChain => 15f + Math.Min(5, s.ClusteredEnemies) * 19f +
                (s.Thunderstorm ? 32 : 0) - (s.Enemies < 3 ? 30 : 0),
            FranSkill.PressurizedQuickdraw => 28f + (s.Durable ? 35 : 0) +
                (s.CanMelee ? 43 : -35) - (s.Speed > 12 ? 25 : 0),
            FranSkill.ThorsHammer => 10f + (s.Durable ? 45 : -40) + (s.Boss ? 15 : 0) +
                (s.Rain ? 55 : 0) + (s.Speed < 6 ? 24 : 0),
            _ => float.NegativeInfinity
        };
    }

    private static bool FranSkillInfoId(FranSkill skill) => (byte)skill < (byte)FranSkill.Count;

    public static FranSkill Select(Situation situation, ReadOnlySpan<bool> unlocked,
        FranSkill current, int heldTicks, bool actionLocked)
    {
        bool currentValid = Available(current, unlocked);
        // Exclusion is a filter, not a low score: a shield can never win a tie or fallback.
        FranSkill best = FranSkill.None;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < (int)FranSkill.Count; i++)
        {
            FranSkill candidate = (FranSkill)i;
            if (!Available(candidate, unlocked))
                continue;
            float score = Score(candidate, situation);
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }
        if (!currentValid)
            return best;
        if (actionLocked)
            return current;
        bool urgentHealing = best == FranSkill.BlueResonance && situation.MissingHealthRatio >= .40f;
        if (heldTicks < MinimumHoldTicks && !urgentHealing)
            return current;
        if (!situation.HasTarget && situation.MissingHealthRatio < .20f)
            return current;
        return bestScore >= Score(current, situation) + SwitchMargin ? best : current;
    }

    private static bool Available(FranSkill skill, ReadOnlySpan<bool> unlocked) =>
        FranSkillInfoId(skill) && skill != FranSkill.MagicBarrier &&
        (int)skill < unlocked.Length && unlocked[(int)skill];
}
