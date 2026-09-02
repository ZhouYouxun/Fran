namespace FranMod.Core;

/// <summary>
/// 弗兰平衡驾驶舱：这里只放需要直接调整的三张进度表。
/// 游戏运行时会读取本文件中最新保存的值；表格尚未写完整时会安全使用编译值。
/// </summary>
public static partial class FranBalance
{
    // 1：核心大剑进度（世界节点、核心攻击力、核心大小）
    public static object[,] CoreProgression =
    {
        // 世界 Boss / 条件                                      攻击力   大小
        { FranProgressionMilestone.Initial,                         105,  1.00f },
        { FranProgressionMilestone.KingSlime,                       105,  1.00f },
        { FranProgressionMilestone.EyeOfCthulhu,                    180,  1.00f },
        { FranProgressionMilestone.EaterOfWorlds,                   270,  1.00f },
        { FranProgressionMilestone.BrainOfCthulhu,                  270,  1.00f },
        { FranProgressionMilestone.AnyEvilBoss,                     270,  1.00f },
        { FranProgressionMilestone.QueenBee,                        270,  1.00f },
        { FranProgressionMilestone.Deerclops,                       270,  1.00f },
        { FranProgressionMilestone.Skeletron,                       300,  1.00f },
        { FranProgressionMilestone.DarkMage,                        300,  1.10f },
        { FranProgressionMilestone.WallOfFlesh,                     495,  1.10f },
        { FranProgressionMilestone.QueenSlime,                      495,  1.10f },
        { FranProgressionMilestone.Destroyer,                       600,  1.10f },
        { FranProgressionMilestone.Twins,                           600,  1.10f },
        { FranProgressionMilestone.SkeletronPrime,                  600,  1.10f },
        { FranProgressionMilestone.AnyMechanicalBoss,               600,  1.10f },
        { FranProgressionMilestone.Ogre,                            600,  1.10f },
        { FranProgressionMilestone.AnyTwoMechanicalBosses,          705,  1.20f },
        { FranProgressionMilestone.AllMechanicalBosses,             870,  1.20f },
        { FranProgressionMilestone.Plantera,                       1275,  1.20f },
        { FranProgressionMilestone.DukeFishron,                    1275,  1.20f },
        { FranProgressionMilestone.EmpressOfLight,                 1275,  1.20f },
        { FranProgressionMilestone.Golem,                          1935,  1.30f },
        { FranProgressionMilestone.Betsy,                          1935,  1.30f },
        { FranProgressionMilestone.LunaticCultist,                 1875,  1.30f },
        { FranProgressionMilestone.MoonLord,                       2850,  1.40f }
    };

    // 2：八种战术的解锁条件（战术、世界节点）
    public static object[,] TacticUnlockProgression =
    {
        { FranSkill.TelekineticCatapult,   FranProgressionMilestone.Initial },
        { FranSkill.SkillTaker,            FranProgressionMilestone.Initial },
        { FranSkill.MagicBarrier,          FranProgressionMilestone.Initial },
        { FranSkill.MultipleBodyCreation,  FranProgressionMilestone.Skeletron },
        { FranSkill.BlueResonance,         FranProgressionMilestone.Skeletron },
        { FranSkill.ThunderChain,          FranProgressionMilestone.WallOfFlesh },
        { FranSkill.PressurizedQuickdraw,  FranProgressionMilestone.AllMechanicalBosses },
        { FranSkill.ThorsHammer,           FranProgressionMilestone.AllMechanicalBosses }
    };

    // 3：战术槽位成长（世界节点、可用槽位）
    public static object[,] TacticSlotProgression =
    {
        { FranProgressionMilestone.Initial,       2 },
        { FranProgressionMilestone.WallOfFlesh,   3 }
    };
}
