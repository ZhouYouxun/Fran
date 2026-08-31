using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;

namespace FranMod.Core;

internal enum FranBossProgressPhase : byte
{
    PreHardmode,
    Hardmode
}

internal enum FranBossProgressAction : byte
{
    SetSingle,
    SetPreHardmode,
    SetHardmode,
    SetAll,
    InvertAll
}

internal sealed class FranBossProgressEntry
{
    private readonly Func<bool> getDowned;
    private readonly Action<bool> setDowned;

    public FranBossProgressPhase Phase { get; }
    public string NameKey { get; }
    public string IconPath { get; }
    public string ProgressKey { get; }
    public bool Downed => getDowned();
    public string DisplayName => Language.GetTextValue($"Mods.Fran.BossProgress.Names.{NameKey}");

    public FranBossProgressEntry(FranBossProgressPhase phase, string nameKey, string iconName,
        Func<bool> getDowned, Action<bool> setDowned, string progressKey = null)
    {
        Phase = phase;
        NameKey = nameKey;
        IconPath = $"Fran/Assets/BossProgress/{iconName}";
        ProgressKey = progressKey ?? nameKey;
        this.getDowned = getDowned;
        this.setDowned = setDowned;
    }

    public void SetDowned(bool value) => setDowned(value);
}

internal static class FranBossProgressRegistry
{
    public static readonly FranBossProgressEntry[] Entries =
    [
        Pre("KingSlime", "KingSlime", () => NPC.downedSlimeKing, value => NPC.downedSlimeKing = value),
        Pre("EyeOfCthulhu", "EyeOfCthulhu", () => NPC.downedBoss1, value => NPC.downedBoss1 = value),
        Pre("EaterOfWorlds", "EaterOfWorlds", () => NPC.downedBoss2, value => NPC.downedBoss2 = value, "WorldEvilBoss"),
        Pre("BrainOfCthulhu", "BrainOfCthulhu", () => NPC.downedBoss2, value => NPC.downedBoss2 = value, "WorldEvilBoss"),
        Pre("QueenBee", "QueenBee", () => NPC.downedQueenBee, value => NPC.downedQueenBee = value),
        Pre("Skeletron", "Skeletron", () => NPC.downedBoss3, value => NPC.downedBoss3 = value),
        Pre("Deerclops", "Deerclops", () => NPC.downedDeerclops, value => NPC.downedDeerclops = value),
        Pre("WallOfFlesh", "WallOfFlesh", () => Main.hardMode, value => Main.hardMode = value),

        Hard("QueenSlime", "QueenSlime", () => NPC.downedQueenSlime, value => NPC.downedQueenSlime = value),
        Hard("Destroyer", "Destroyer", () => NPC.downedMechBoss1, value => NPC.downedMechBoss1 = value),
        Hard("Twins", "Twins", () => NPC.downedMechBoss2, value => NPC.downedMechBoss2 = value),
        Hard("SkeletronPrime", "SkeletronPrime", () => NPC.downedMechBoss3, value => NPC.downedMechBoss3 = value),
        Hard("Plantera", "Plantera", () => NPC.downedPlantBoss, value => NPC.downedPlantBoss = value),
        Hard("Golem", "Golem", () => NPC.downedGolemBoss, value => NPC.downedGolemBoss = value),
        Hard("DukeFishron", "DukeFishron", () => NPC.downedFishron, value => NPC.downedFishron = value),
        Hard("EmpressOfLight", "EmpressOfLight", () => NPC.downedEmpressOfLight, value => NPC.downedEmpressOfLight = value),
        Hard("LunaticCultist", "LunaticCultist", () => NPC.downedAncientCultist, value => NPC.downedAncientCultist = value),
        Hard("MoonLord", "MoonLord", () => NPC.downedMoonlord, value => NPC.downedMoonlord = value)
    ];

    public static Color PhaseColor(FranBossProgressPhase phase) => phase switch
    {
        FranBossProgressPhase.PreHardmode => new Color(112, 214, 151),
        FranBossProgressPhase.Hardmode => new Color(104, 186, 242),
        _ => Color.White
    };

    public static bool IsPhaseComplete(FranBossProgressPhase phase)
    {
        bool any = false;
        HashSet<string> checkedKeys = new(StringComparer.Ordinal);
        foreach (FranBossProgressEntry entry in Entries)
        {
            if (entry.Phase != phase || !checkedKeys.Add(entry.ProgressKey))
                continue;
            any = true;
            if (!entry.Downed)
                return false;
        }
        return any;
    }

    public static bool IsAllComplete()
    {
        HashSet<string> checkedKeys = new(StringComparer.Ordinal);
        foreach (FranBossProgressEntry entry in Entries)
        {
            if (checkedKeys.Add(entry.ProgressKey) && !entry.Downed)
                return false;
        }
        return true;
    }

    public static int Apply(FranBossProgressAction action, int index, bool state)
    {
        return action switch
        {
            FranBossProgressAction.SetSingle => ApplySingle(index, state),
            FranBossProgressAction.SetPreHardmode => ApplyPhase(FranBossProgressPhase.PreHardmode, state),
            FranBossProgressAction.SetHardmode => ApplyPhase(FranBossProgressPhase.Hardmode, state),
            FranBossProgressAction.SetAll => ApplyAll(state),
            FranBossProgressAction.InvertAll => InvertAll(),
            _ => 0
        };
    }

    private static int ApplySingle(int index, bool state)
    {
        if (index < 0 || index >= Entries.Length)
            return 0;
        FranBossProgressEntry entry = Entries[index];
        if (entry.Downed == state)
            return 0;
        entry.SetDowned(state);
        return 1;
    }

    private static int ApplyPhase(FranBossProgressPhase phase, bool state)
    {
        return ApplyFiltered(entry => entry.Phase == phase, state);
    }

    private static int ApplyAll(bool state)
    {
        return ApplyFiltered(_ => true, state);
    }

    private static int ApplyFiltered(Func<FranBossProgressEntry, bool> predicate, bool state)
    {
        int changed = 0;
        HashSet<string> changedKeys = new(StringComparer.Ordinal);
        foreach (FranBossProgressEntry entry in Entries)
        {
            if (!predicate(entry) || !changedKeys.Add(entry.ProgressKey) || entry.Downed == state)
                continue;
            entry.SetDowned(state);
            changed++;
        }
        return changed;
    }

    private static int InvertAll()
    {
        int changed = 0;
        HashSet<string> changedKeys = new(StringComparer.Ordinal);
        foreach (FranBossProgressEntry entry in Entries)
        {
            if (!changedKeys.Add(entry.ProgressKey))
                continue;
            entry.SetDowned(!entry.Downed);
            changed++;
        }
        return changed;
    }

    private static FranBossProgressEntry Pre(string nameKey, string iconName, Func<bool> getDowned,
        Action<bool> setDowned, string progressKey = null) =>
        new(FranBossProgressPhase.PreHardmode, nameKey, iconName, getDowned, setDowned, progressKey);

    private static FranBossProgressEntry Hard(string nameKey, string iconName, Func<bool> getDowned,
        Action<bool> setDowned) =>
        new(FranBossProgressPhase.Hardmode, nameKey, iconName, getDowned, setDowned);
}
