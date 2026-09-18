using System;

namespace FranMod.Content.Projectiles;

// 数量与层数无关；旧数量参数只参与保留原输出预算的折算。
internal static class FranMultipleBodyPattern
{
    internal static int Count(int combo) => combo switch { 1 => 2, 3 => 6, _ => 4 };

    internal static float CycleWeight(ReadOnlySpan<float> swordWeights, int layers,
        int normalBase, int normalExtra, int finisherBase, int finisherExtra,
        float normalDamage, float finisherDamage)
    {
        float normal = normalDamage * (normalBase + normalExtra * (layers - 1)) / Math.Max(1, normalBase);
        float finisher = finisherDamage * (finisherBase + finisherExtra * (layers - 1)) / Math.Max(1, finisherBase);
        return (swordWeights[0] + swordWeights[1] + swordWeights[2]) * normal + swordWeights[3] * finisher;
    }

    // 将整数余数分给前几个分身，避免数量不同导致每刀丢失不同的取整伤害。
    internal static int BladeDamage(int totalDamage, int combo, int index)
    {
        int count = Count(combo);
        return Math.Max(1, totalDamage / count + (index < totalDamage % count ? 1 : 0));
    }
}
