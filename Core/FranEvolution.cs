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
    public const int MaximumSpendablePoints = 18;
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
        new(FranEvolutionNode.OneAsSword, FranEvolutionBranch.Bond, 2, 4, FranEvolutionNode.ResonantPull)
    ];

    public static int Count => (int)FranEvolutionNode.Count;

    public static FranEvolutionNodeDefinition Get(FranEvolutionNode node) =>
        IsValid(node) ? Definitions[(int)node] : null;

    public static bool IsValid(FranEvolutionNode node) => (int)node >= 0 && (int)node < Count;

    public static ushort Bit(FranEvolutionNode node) => IsValid(node) ? (ushort)(1 << (int)node) : (ushort)0;

    public static bool HasNode(ushort mask, FranEvolutionNode node) => (mask & Bit(node)) != 0;

    public static int SpentPoints(ushort mask)
    {
        int spent = 0;
        for (int i = 0; i < Count; i++)
            if ((mask & (1 << i)) != 0)
                spent += Definitions[i].Cost;
        return spent;
    }

    public static ushort NormalizeMask(ushort mask, int earnedPoints)
    {
        ushort normalized = 0;
        int spent = 0;
        int limit = Math.Clamp(earnedPoints, 0, MaximumSpendablePoints);
        for (int i = 0; i < Count; i++)
        {
            if ((mask & (1 << i)) == 0)
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

    public static int CurrentStagePointCap
    {
        get
        {
            if (NPC.downedMoonlord)
                return 18;
            if (NPC.downedGolemBoss)
                return 16;
            if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3)
                return 14;
            if (Main.hardMode)
                return 11;
            return 7;
        }
    }

    public static Color BranchColor(FranEvolutionBranch branch) => branch switch
    {
        FranEvolutionBranch.Teacher => new Color(126, 105, 232),
        FranEvolutionBranch.Fran => new Color(244, 190, 70),
        _ => new Color(92, 218, 236)
    };
}
