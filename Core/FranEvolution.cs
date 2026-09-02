using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace FranMod.Core;

public enum FranEvolutionNode : byte
{
    // Save files and multiplayer state store these ordinal values. Append new nodes; never reorder them.
    EdgeCalibration,
    ComboCalculation,
    MagicStoneAnalysis,
    SelfEvolution,
    CatStep,
    AerialHunt,
    HuntingTempo,
    EvolvedFootwork,
    MindSync,
    SharedRhythm,
    ResonantPull,
    OneAsSword,

    // Teacher constellation additions (7 stars total).
    ArcaneCirculation,
    DeeperAnalysis,
    SwordSaintReasoning,

    // Fran constellation additions (9 stars total).
    FelineReflex,
    SkyHunter,
    PredatorSight,
    PhantomStep,
    BlackCatAwakening,

    // Bond constellation additions (12 stars total).
    SharedMana,
    SharedVision,
    HarmonicGuard,
    SoulBridge,
    SynchronizedVolley,
    LimitResonance,
    MutualGrowth,
    EternalContract,
    Count,
    None = byte.MaxValue
}

public enum FranEvolutionBranch : byte
{
    Teacher,
    Fran,
    Bond
}

public enum FranEvolutionUnlockFailure : byte
{
    None,
    InvalidNode,
    AlreadyUnlocked,
    MissingPrerequisite,
    StageLocked,
    NotEnoughPoints,
    InCombat
}

public sealed class FranEvolutionNodeDefinition
{
    public FranEvolutionNode Node { get; }
    public FranEvolutionBranch Branch { get; }
    public int Cost { get; }
    public int RequiredStage { get; }
    public FranEvolutionNode Prerequisite { get; }

    public FranEvolutionNodeDefinition(FranEvolutionNode node, FranEvolutionBranch branch, int cost,
        int requiredStage, FranEvolutionNode prerequisite)
    {
        Node = node;
        Branch = branch;
        Cost = cost;
        RequiredStage = requiredStage;
        Prerequisite = prerequisite;
    }
}

public static class FranEvolutionInfo
{
    public const int MaximumSpendablePoints = 51;
    public const int MaximumClaimedBosses = 256;
    public const int MaximumBossKeyLength = 64;

    private static readonly FranEvolutionNodeDefinition[] Definitions =
    [
        new(FranEvolutionNode.EdgeCalibration, FranEvolutionBranch.Teacher, 1, 1, FranEvolutionNode.None),
        new(FranEvolutionNode.ComboCalculation, FranEvolutionBranch.Teacher, 1, 2, FranEvolutionNode.EdgeCalibration),
        new(FranEvolutionNode.MagicStoneAnalysis, FranEvolutionBranch.Teacher, 2, 3, FranEvolutionNode.ComboCalculation),
        new(FranEvolutionNode.SelfEvolution, FranEvolutionBranch.Teacher, 2, 4, FranEvolutionNode.MagicStoneAnalysis),

        new(FranEvolutionNode.CatStep, FranEvolutionBranch.Fran, 1, 1, FranEvolutionNode.None),
        new(FranEvolutionNode.AerialHunt, FranEvolutionBranch.Fran, 1, 2, FranEvolutionNode.CatStep),
        new(FranEvolutionNode.HuntingTempo, FranEvolutionBranch.Fran, 2, 3, FranEvolutionNode.AerialHunt),
        new(FranEvolutionNode.EvolvedFootwork, FranEvolutionBranch.Fran, 2, 4, FranEvolutionNode.HuntingTempo),

        new(FranEvolutionNode.MindSync, FranEvolutionBranch.Bond, 1, 1, FranEvolutionNode.None),
        new(FranEvolutionNode.SharedRhythm, FranEvolutionBranch.Bond, 1, 2, FranEvolutionNode.MindSync),
        new(FranEvolutionNode.ResonantPull, FranEvolutionBranch.Bond, 2, 3, FranEvolutionNode.SharedRhythm),
        new(FranEvolutionNode.OneAsSword, FranEvolutionBranch.Bond, 2, 4, FranEvolutionNode.ResonantPull),

        new(FranEvolutionNode.ArcaneCirculation, FranEvolutionBranch.Teacher, 2, 2,
            FranEvolutionNode.EdgeCalibration),
        new(FranEvolutionNode.DeeperAnalysis, FranEvolutionBranch.Teacher, 2, 3,
            FranEvolutionNode.MagicStoneAnalysis),
        new(FranEvolutionNode.SwordSaintReasoning, FranEvolutionBranch.Teacher, 3, 4,
            FranEvolutionNode.SelfEvolution),

        new(FranEvolutionNode.FelineReflex, FranEvolutionBranch.Fran, 1, 1,
            FranEvolutionNode.CatStep),
        new(FranEvolutionNode.SkyHunter, FranEvolutionBranch.Fran, 2, 2,
            FranEvolutionNode.AerialHunt),
        new(FranEvolutionNode.PredatorSight, FranEvolutionBranch.Fran, 2, 2,
            FranEvolutionNode.HuntingTempo),
        new(FranEvolutionNode.PhantomStep, FranEvolutionBranch.Fran, 2, 3,
            FranEvolutionNode.EvolvedFootwork),
        new(FranEvolutionNode.BlackCatAwakening, FranEvolutionBranch.Fran, 3, 4,
            FranEvolutionNode.PhantomStep),

        new(FranEvolutionNode.SharedMana, FranEvolutionBranch.Bond, 1, 1,
            FranEvolutionNode.MindSync),
        new(FranEvolutionNode.SharedVision, FranEvolutionBranch.Bond, 1, 2,
            FranEvolutionNode.SharedRhythm),
        new(FranEvolutionNode.HarmonicGuard, FranEvolutionBranch.Bond, 2, 2,
            FranEvolutionNode.SharedMana),
        new(FranEvolutionNode.SoulBridge, FranEvolutionBranch.Bond, 2, 3,
            FranEvolutionNode.ResonantPull),
        new(FranEvolutionNode.SynchronizedVolley, FranEvolutionBranch.Bond, 2, 3,
            FranEvolutionNode.SharedVision),
        new(FranEvolutionNode.LimitResonance, FranEvolutionBranch.Bond, 2, 3,
            FranEvolutionNode.SynchronizedVolley),
        new(FranEvolutionNode.MutualGrowth, FranEvolutionBranch.Bond, 3, 4,
            FranEvolutionNode.OneAsSword),
        new(FranEvolutionNode.EternalContract, FranEvolutionBranch.Bond, 3, 4,
            FranEvolutionNode.MutualGrowth)
    ];

    public static int Count => (int)FranEvolutionNode.Count;

    public static FranEvolutionNodeDefinition Get(FranEvolutionNode node) =>
        IsValid(node) ? Definitions[(int)node] : null;

    public static bool IsValid(FranEvolutionNode node) => (int)node >= 0 && (int)node < Count;

    public static uint Bit(FranEvolutionNode node) => IsValid(node) ? 1u << (int)node : 0u;

    public static bool HasNode(uint mask, FranEvolutionNode node) => (mask & Bit(node)) != 0;

    public static int SpentPoints(uint mask)
    {
        int spent = 0;
        for (int i = 0; i < Count; i++)
            if ((mask & (1u << i)) != 0)
                spent += Definitions[i].Cost;
        return spent;
    }

    public static uint NormalizeMask(uint mask, int earnedPoints)
    {
        uint normalized = 0;
        int spent = 0;
        int limit = Math.Clamp(earnedPoints, 0, MaximumSpendablePoints);
        for (int i = 0; i < Count; i++)
        {
            if ((mask & (1u << i)) == 0)
                continue;

            FranEvolutionNodeDefinition definition = Definitions[i];
            if (definition.Prerequisite != FranEvolutionNode.None &&
                !HasNode(normalized, definition.Prerequisite))
                continue;
            if (spent + definition.Cost > limit)
                continue;

            normalized |= Bit(definition.Node);
            spent += definition.Cost;
        }
        return normalized;
    }

    public static int CurrentStagePointCap => FranBalance.GetEvolutionPointCap();

    public static Color BranchColor(FranEvolutionBranch branch) => branch switch
    {
        FranEvolutionBranch.Teacher => new Color(126, 105, 232),
        FranEvolutionBranch.Fran => new Color(244, 190, 70),
        _ => new Color(92, 218, 236)
    };
}
