using System;
using FranMod.Core;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

/// <summary>以同阶段全选念动的一轮四刀为 X；按取整前的单目标理论命中计算。</summary>
internal static class FranTacticOutputBudget
{
    internal static float ReferenceCycle()
    {
        int stage = FranBalance.GetTelekineticPatternStage();
        int slots = FranBalance.GetAvailableTacticSlots();
        return FranTelekineticPattern.GetCycleWeight(stage, slots) *
            FranTelekineticPattern.GetDamageScale(stage, slots);
    }

    private static float SumSword()
    {
        float sum = 0f;
        for (int combo = 0; combo < 4; combo++)
            sum += FranBalance.GetComboDamageMultiplier(combo);
        return sum;
    }

    internal static float Scale(FranSkill skill)
    {
        int slots = FranBalance.GetAvailableTacticSlots();
        float raw = 0f;
        float ratio;
        switch (skill)
        {
            case FranSkill.SkillTaker:
                float mark = Math.Max(1f, FranBalance.GetFloat(
                    nameof(FranBalance.SkillTakerFullLoadoutMaximumDamageMultiplier),
                    FranBalance.SkillTakerFullLoadoutMaximumDamageMultiplier));
                ratio = FranBalance.GetFloat(nameof(FranBalance.TacticSkillTakerBudgetRatio), FranBalance.TacticSkillTakerBudgetRatio);
                // 额外输出 = 易伤新增剑伤 + 灵弹伤害（同样受易伤影响）。
                float targetWisps = Math.Max(0f, ReferenceCycle() * ratio - (mark - 1f) * SumSword());
                raw = SumSword() * FranBalance.GetInt(nameof(FranBalance.SkillTakerBaseWispCount),
                    FranBalance.SkillTakerBaseWispCount) * FranBalance.GetFloat(
                    nameof(FranBalance.SkillTakerWispDamageMultiplier), FranBalance.SkillTakerWispDamageMultiplier) * mark;
                return raw > 0f ? targetWisps / raw : 0f;
            case FranSkill.MultipleBodyCreation:
                ratio = FranBalance.GetFloat(nameof(FranBalance.TacticMultipleBodyBudgetRatio), FranBalance.TacticMultipleBodyBudgetRatio);
                for (int combo = 0; combo < 4; combo++)
                {
                    int first = combo == 3 ? FranBalance.GetInt(nameof(FranBalance.MultipleBodyFinisherBaseCount), FranBalance.MultipleBodyFinisherBaseCount)
                        : FranBalance.GetInt(nameof(FranBalance.MultipleBodyNormalBaseCount), FranBalance.MultipleBodyNormalBaseCount);
                    int extra = combo == 3 ? FranBalance.GetInt(nameof(FranBalance.MultipleBodyFinisherExtraLayerCount), FranBalance.MultipleBodyFinisherExtraLayerCount)
                        : FranBalance.GetInt(nameof(FranBalance.MultipleBodyNormalExtraLayerCount), FranBalance.MultipleBodyNormalExtraLayerCount);
                    float multiplier = combo == 3 ? FranBalance.GetFloat(nameof(FranBalance.MultipleBodyFinisherDamageMultiplier), FranBalance.MultipleBodyFinisherDamageMultiplier)
                        : FranBalance.GetFloat(nameof(FranBalance.MultipleBodyNormalDamageMultiplier), FranBalance.MultipleBodyNormalDamageMultiplier);
                    raw += FranBalance.GetComboDamageMultiplier(combo) * multiplier * (first + extra * (slots - 1)) / Math.Max(1, first);
                }
                break;
            case FranSkill.BlueResonance:
                ratio = FranBalance.GetFloat(nameof(FranBalance.TacticBlueResonanceBudgetRatio), FranBalance.TacticBlueResonanceBudgetRatio);
                for (int combo = 0; combo < 4; combo++)
                {
                    int first = combo == 3 ? FranBalance.GetInt(nameof(FranBalance.BlueResonanceFinisherBaseCount), FranBalance.BlueResonanceFinisherBaseCount)
                        : FranBalance.GetInt(nameof(FranBalance.BlueResonanceNormalBaseCount), FranBalance.BlueResonanceNormalBaseCount);
                    int count = first + (slots - 1) * FranBalance.GetInt(nameof(FranBalance.BlueResonanceExtraLayerCount), FranBalance.BlueResonanceExtraLayerCount);
                    float multiplier = combo == 3 ? FranBalance.GetFloat(nameof(FranBalance.BlueResonanceFinisherDamageMultiplier), FranBalance.BlueResonanceFinisherDamageMultiplier)
                        : FranBalance.GetFloat(nameof(FranBalance.BlueResonanceNormalDamageMultiplier), FranBalance.BlueResonanceNormalDamageMultiplier);
                    raw += FranBalance.GetComboDamageMultiplier(combo) * count * multiplier * 2f;
                }
                break;
            case FranSkill.ThorsHammer:
                ratio = Main.raining
                    ? FranBalance.GetFloat(nameof(FranBalance.TacticHammerRainBudgetRatio), FranBalance.TacticHammerRainBudgetRatio)
                    : FranBalance.GetFloat(nameof(FranBalance.TacticHammerBudgetRatio), FranBalance.TacticHammerBudgetRatio);
                // 全选一轮一锤，已经包括相对于普通两轮一锤的频率翻倍。
                raw = FranBalance.GetComboDamageMultiplier(3) * FranBalance.GetFloat(
                    nameof(FranBalance.ThorsHammerDropDamageMultiplier), FranBalance.ThorsHammerDropDamageMultiplier) *
                    (1f + FranBalance.GetFloat(nameof(FranBalance.ThorsHammerExtraLayerDamageMultiplier),
                        FranBalance.ThorsHammerExtraLayerDamageMultiplier) * (slots - 1)) *
                    (1f + FranBalance.GetFloat(nameof(FranBalance.ThorsHammerExplosionDamageMultiplier),
                        FranBalance.ThorsHammerExplosionDamageMultiplier));
                break;
            case FranSkill.PressurizedQuickdraw:
                ratio = FranBalance.GetFloat(nameof(FranBalance.TacticQuickdrawBudgetRatio), FranBalance.TacticQuickdrawBudgetRatio);
                for (int combo = 0; combo < 4; combo++)
                    raw += FranBalance.GetComboDamageMultiplier(combo) * FranBalance.GetQuickdrawAirburstCount(combo) *
                        FranBalance.GetQuickdrawAirburstDamageMultiplier(combo) *
                        (1f + FranBalance.GetFloat(nameof(FranBalance.QuickdrawExtraLayerDamageMultiplier),
                            FranBalance.QuickdrawExtraLayerDamageMultiplier) * (slots - 1));
                break;
            default:
                return 1f;
        }
        return raw > 0f ? ReferenceCycle() * ratio / raw : 0f;
    }

    internal static float CycleFrames(Player owner)
    {
        float speed = Math.Max(0.25f, owner.GetWeaponAttackSpeed(owner.HeldItem));
        float frames = 0f;
        for (int combo = 0; combo < 4; combo++)
            frames += Math.Max(12, (int)MathF.Round(FranBalance.GetComboBaseDuration(combo) *
                (combo == 3 ? 0.74f : 1f) / speed));
        return frames;
    }

    internal static float DiscAirburstMultiplier(Player owner)
    {
        // 全选居合没有四刀，按持续释放时相同的四刀耗时折算；每个滞留爆点对同一目标一次。
        float ratio = FranBalance.GetFloat(nameof(FranBalance.TacticQuickdrawBudgetRatio), FranBalance.TacticQuickdrawBudgetRatio);
        int interval = Math.Max(1, FranBalance.GetInt(nameof(FranBalance.QuickdrawFullDiscAirburstInterval),
            FranBalance.QuickdrawFullDiscAirburstInterval));
        return ReferenceCycle() * ratio * interval / CycleFrames(owner);
    }

    internal static float ThunderPerSwingRatio(bool primary, int boltCount, bool storm)
    {
        float single = FranBalance.GetFloat(nameof(FranBalance.TacticThunderSingleBudgetRatio), FranBalance.TacticThunderSingleBudgetRatio);
        float group = FranBalance.GetFloat(nameof(FranBalance.TacticThunderGroupBudgetRatio), FranBalance.TacticThunderGroupBudgetRatio);
        float weather = storm ? FranBalance.GetFloat(nameof(FranBalance.TacticThunderStormBudgetRatio),
            FranBalance.TacticThunderStormBudgetRatio) / Math.Max(0.001f, group) : 1f;
        // 第一刀布置电路，第二至第四刀落雷；三次落雷的剑击倍率之和归一到一轮。
        float swordWeights = FranBalance.GetComboDamageMultiplier(1) + FranBalance.GetComboDamageMultiplier(2) +
            FranBalance.GetComboDamageMultiplier(3);
        return ReferenceCycle() * weather * (primary ? single : Math.Max(0f, group - single) / Math.Max(1, boltCount - 1)) /
            Math.Max(0.001f, swordWeights);
    }
}
