using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Terraria;
using Terraria.GameContent.Events;

namespace FranMod.Core;

/// <summary>
/// Fran 的原版世界进度节点。这里既包含逐个 Boss，也包含少量必须同时满足的组合条件。
/// </summary>
public enum FranProgressionMilestone
{
    Initial,
    KingSlime,
    EyeOfCthulhu,
    EaterOfWorlds,
    BrainOfCthulhu,
    QueenBee,
    Deerclops,
    Skeletron,
    DarkMage,
    WallOfFlesh,
    QueenSlime,
    Destroyer,
    Twins,
    SkeletronPrime,
    Ogre,
    AllMechanicalBosses,
    Plantera,
    Golem,
    DukeFishron,
    EmpressOfLight,
    LunaticCultist,
    Betsy,
    MoonLord
}

/// <summary>
/// Fran 的核心平衡入口。
/// <para>
/// 所有平衡值都使用变量，不使用 const。游戏运行时会直接读取本源码文件中最新保存的数字；
/// 因此可以保持游戏开启，修改并保存 FranBalance.cs，再回到游戏观察结果，不需要 ModConfig。
/// 如果保存过程中源码暂时不完整，运行时会安全回退到本次编译时的默认值。
/// </para>
/// </summary>
public static class FranBalance
{
    // 技术容量只负责存档和联机包的固定长度，不代表实际可用槽位。
    // 实际槽位由下方 TacticSlotProgression 决定，目前最终上限是 3。
    public static int TacticStorageCapacity = 4;

    // =====================================================================
    // 第一部分：原版每个 Boss 节点后的师父基础伤害
    // =====================================================================
    // 第二列是“满足该行条件后，基础伤害至少提升到多少”。
    // 可选 Boss 可能被越级击败，因此运行时取所有已满足节点中的最高值，不依赖表格顺序。
    // 目前新增节点沿用旧版 20/34/56/82/115/180 阶梯，先保持既有实战行为；
    // 后续只需要改第二列数字并保存，就能在游戏内立即重新读取。
    public static object[,] VanillaBossDamageProgression =
    {
        // 原版 Boss / 条件                                      基础伤害
        { FranProgressionMilestone.Initial,                         20 }, // 初始 / 尚未击败 Boss
        { FranProgressionMilestone.KingSlime,                       20 }, // 史莱姆王
        { FranProgressionMilestone.EyeOfCthulhu,                    20 }, // 克苏鲁之眼
        { FranProgressionMilestone.EaterOfWorlds,                   20 }, // 世界吞噬怪（与克苏鲁之脑共用原版进度旗标）
        { FranProgressionMilestone.BrainOfCthulhu,                  20 }, // 克苏鲁之脑（与世界吞噬怪共用原版进度旗标）
        { FranProgressionMilestone.QueenBee,                        20 }, // 蜂王
        { FranProgressionMilestone.Deerclops,                       20 }, // 独眼巨鹿
        { FranProgressionMilestone.DarkMage,                        20 }, // 黑暗魔法师 / 撒旦军队一级
        { FranProgressionMilestone.Skeletron,                       34 }, // 骷髅王
        { FranProgressionMilestone.WallOfFlesh,                     56 }, // 血肉墙 / 进入困难模式
        { FranProgressionMilestone.QueenSlime,                      56 }, // 史莱姆皇后
        { FranProgressionMilestone.Destroyer,                       82 }, // 毁灭者
        { FranProgressionMilestone.Twins,                           82 }, // 双子魔眼
        { FranProgressionMilestone.SkeletronPrime,                  82 }, // 机械骷髅王
        { FranProgressionMilestone.Ogre,                            82 }, // 食人魔 / 撒旦军队二级
        { FranProgressionMilestone.AllMechanicalBosses,             82 }, // 组合条件：机械三王全部击败
        { FranProgressionMilestone.Plantera,                       115 }, // 世纪之花
        { FranProgressionMilestone.Golem,                          115 }, // 石巨人
        { FranProgressionMilestone.DukeFishron,                    115 }, // 猪龙鱼公爵
        { FranProgressionMilestone.EmpressOfLight,                 115 }, // 光之女皇
        { FranProgressionMilestone.LunaticCultist,                 115 }, // 拜月教邪教徒
        { FranProgressionMilestone.Betsy,                          115 }, // 贝茜 / 撒旦军队三级
        { FranProgressionMilestone.MoonLord,                       180 }  // 月亮领主
    };

    // =====================================================================
    // 第二部分：八种战术的解锁条件
    // =====================================================================
    // 第一列必须与 FranSkill 枚举对应；第二列可以直接改成任意上方进度节点。
    public static object[,] TacticUnlockProgression =
    {
        { FranSkill.TelekineticCatapult,   FranProgressionMilestone.Initial },              // 念动弹射：开局
        { FranSkill.SkillTaker,            FranProgressionMilestone.Initial },              // 技能夺取者：开局
        { FranSkill.MagicBarrier,          FranProgressionMilestone.Initial },              // 魔力屏障：开局
        { FranSkill.MultipleBodyCreation,  FranProgressionMilestone.Skeletron },            // 多重分体：骷髅王后
        { FranSkill.BlueResonance,         FranProgressionMilestone.Skeletron },            // 蓝辉共鸣：骷髅王后
        { FranSkill.ThunderChain,          FranProgressionMilestone.WallOfFlesh },           // 雷链：血肉墙后
        { FranSkill.PressurizedQuickdraw,  FranProgressionMilestone.AllMechanicalBosses },  // 压缩居合：机械三王全部击败
        { FranSkill.ThorsHammer,           FranProgressionMilestone.AllMechanicalBosses }   // 雷神之锤：机械三王全部击败
    };

    // 技能轮仍以四个叙事阶段展示。它只控制 UI 阶段文字；真正能否使用以上面的 Boss 条件表为准。
    public static int[] TacticDisplayStages =
    {
        1, // 念动弹射
        1, // 技能夺取者
        1, // 魔力屏障
        2, // 多重分体
        2, // 蓝辉共鸣
        3, // 雷链
        4, // 压缩居合
        4  // 雷神之锤
    };

    public static object[,] TacticStageProgression =
    {
        { FranProgressionMilestone.Initial,                 1 }, // 第一阶段：开局
        { FranProgressionMilestone.Skeletron,               2 }, // 第二阶段：骷髅王后
        { FranProgressionMilestone.WallOfFlesh,             3 }, // 第三阶段：血肉墙后
        { FranProgressionMilestone.AllMechanicalBosses,     4 }  // 第四阶段：机械三王全部击败
    };

    // =====================================================================
    // 第三部分：战术槽位成长
    // =====================================================================
    // 最终设计上限为三个战术：开局两个，击败血肉墙后提升到三个。
    public static object[,] TacticSlotProgression =
    {
        { FranProgressionMilestone.Initial,          2 }, // 初始：2 个战术槽
        { FranProgressionMilestone.WallOfFlesh,      3 }  // 血肉墙后：3 个战术槽（最终上限）
    };

    // =====================================================================
    // 基础剑术与弗兰固有属性
    // =====================================================================
    public static int TeacherUseTimeFrames = 25;             // 师父物品基础使用时间
    public static float TeacherKnockback = 7f;                // 师父基础击退
    public static float FranMeleeDamageBonus = 0.10f;         // 弗兰模式：近战伤害 +10%
    public static float FranMeleeAttackSpeedBonus = 0.10f;    // 弗兰模式：近战攻速 +10%

    public static float[] ComboDamageMultipliers =
    {
        1.00f, // 第一剑：横斩
        0.82f, // 第二剑：回旋
        1.38f, // 第三剑：重斩
        1.48f  // 第四剑：突刺终结
    };

    public static float[] ComboBaseDurationFrames =
    {
        21f, // 第一剑
        22f, // 第二剑
        19f, // 第三剑
        25f  // 第四剑
    };

    // =====================================================================
    // 战术通用与各战术伤害参数
    // =====================================================================
    public static int[] TacticCooldownFrames =
    {
        0,   // 念动弹射
        0,   // 技能夺取者
        150, // 魔力屏障破裂后 2.5 秒重构
        0,   // 多重分体
        0,   // 蓝辉共鸣
        0,   // 雷链
        0,   // 压缩居合
        0    // 雷神之锤
    };

    // 念动弹射：前三剑气针、第四剑激光、困难模式星球。
    public static float TelekineticTwoShellDamageMultiplier = 0.42f;
    public static float TelekineticThreeShellDamageMultiplier = 0.31f;
    public static float TelekineticManyShellDamageMultiplier = 0.24f;
    public static float TelekineticLaserDamageMultiplier = 0.88f;
    public static float TelekineticLaserExtraLayerMultiplier = 0.25f;
    public static float TelekineticPlanetDamageMultiplier = 0.15f;
    public static float TelekineticFinisherPlanetDamageMultiplier = 0.17f;

    // 技能夺取者：印记只强化师父本体剑击；灵弹是额外追踪伤害。
    public static int SkillTakerMaximumSeals = 4;
    public static float SkillTakerDamagePerSeal = 0.10f;
    public static float SkillTakerExtraLayerDamagePerSeal = 0.03f;
    public static int SkillTakerBaseWispCount = 2;
    public static float SkillTakerWispDamageMultiplier = 0.24f;
    public static float SkillTakerWispDamagePerSeal = 0.015f;

    // 魔力屏障：每次受击消耗一点耐久，破裂后进入战术冷却。
    public static int MagicBarrierMaximumIntegrity = 5;
    public static float MagicBarrierWeaponDamageMultiplier = 0.72f;
    public static float MagicBarrierDamageReductionPerLayer = 0.10f;
    public static float MagicBarrierCounterDamageMultiplier = 0.86f;
    public static int MagicBarrierHitImmunityFrames = 18;

    // 多重分体：数量增长与总伤害增长分开，避免以后调整数量时误改总伤害。
    public static int MultipleBodyNormalBaseCount = 2;
    public static int MultipleBodyFinisherBaseCount = 4;
    public static int MultipleBodyNormalExtraLayerCount = 1;
    public static int MultipleBodyFinisherExtraLayerCount = 2;
    public static float MultipleBodyNormalDamageMultiplier = 0.86f;
    public static float MultipleBodyFinisherDamageMultiplier = 1.24f;
    public static float MultipleBodyExtraLayerDamageMultiplier = 0.18f;

    // 蓝辉共鸣：终结剑拥有更多剑痕；治疗共享一个全局冷却。
    public static int BlueResonanceNormalBaseCount = 1;
    public static int BlueResonanceFinisherBaseCount = 3;
    public static int BlueResonanceExtraLayerCount = 1;
    public static float BlueResonanceNormalDamageMultiplier = 0.38f;
    public static float BlueResonanceFinisherDamageMultiplier = 0.30f;
    public static int BlueResonanceHealingCooldownFrames = 180;
    public static int BlueResonanceHealingLifeDivisor = 50;
    public static int BlueResonanceHealingMinimum = 2;
    public static int BlueResonanceHealingMaximum = 10;

    // 雷链：主雷/伴随雷使用同一倍率，自动追踪雷使用略高倍率。
    public static float ThunderChainMainDamageMultiplier = 0.31f;
    public static float ThunderChainTrackedDamageMultiplier = 0.37f;
    public static float ThunderChainExtraLayerDamageMultiplier = 0.12f;
    public static int ThunderChainNormalSupportingBoltCount = 1;
    public static int ThunderChainFinisherSupportingBoltCount = 2;
    public static int ThunderChainNormalTrackedTargetCount = 3;
    public static int ThunderChainFinisherTrackedTargetCount = 4;

    // 压缩居合：四段剑术在剑弧上产生不同数量的压缩空气爆点。
    public static int[] QuickdrawAirburstCounts = { 5, 6, 8, 7 };
    public static float[] QuickdrawAirburstDamageMultipliers = { 0.18f, 0.16f, 0.13f, 0.15f };
    public static float QuickdrawExtraLayerDamageMultiplier = 0.15f;
    public static float QuickdrawExtraLayerSizeMultiplier = 0.10f;

    // 雷神之锤：完成指定轮数的四段剑术后，在第二轮终结剑召唤。
    public static int ThorsHammerRequiredComboRounds = 2;
    public static float ThorsHammerDropDamageMultiplier = 1.18f;
    public static float ThorsHammerExtraLayerDamageMultiplier = 0.22f;
    public static float ThorsHammerExplosionDamageMultiplier = 0.88f;

    // =====================================================================
    // 冲刺与黑天虎大招
    // =====================================================================
    public static int DashDurationFrames = 12;                 // 主动冲刺持续时间
    public static float DashBaseSpeed = 16f;                   // 主动冲刺基础速度
    public static int DashBaseCooldownFrames = 50;             // 未投入猫步时的基础冷却
    public static float DashBaseImpactDamageMultiplier = 1.20f;// 冲刺接触伤害 = 当前基础伤害 × 此倍率

    public static int UltimateChargeDurationFrames = 10800;    // 黑天虎自然充能：180 秒
    public static float UltimateStrikeDamageMultiplier = 1.15f;// 大招每次普通斩的基础倍率
    public static int UltimateNormalStrikeCount = 18;          // 普通高速斩次数
    public static int UltimateStartingStrikeFrames = 9;        // 第一轮普通斩耗时
    public static int UltimateEndingStrikeFrames = 4;          // 最后一轮普通斩耗时
    public static int UltimateStartingDriftFrames = 6;         // 第一轮命中漂移耗时
    public static int UltimateEndingDriftFrames = 2;           // 最后一轮命中漂移耗时
    public static int UltimateFinalPauseFrames = 20;           // 终结斩前停顿
    public static int UltimateFinalStrikeFrames = 9;           // 终结斩耗时
    public static float UltimateFinalStrikeDamageMultiplier = 5f;   // 终结斩伤害倍率
    public static float UltimateNormalRiftDamageMultiplier = 0.42f; // 普通斩裂隙激光倍率
    public static float UltimateFinalRiftDamageMultiplier = 0.55f;  // 终结斩裂隙激光倍率

    // =====================================================================
    // 魔石回廊点数上限与念动飞行
    // =====================================================================
    public static object[,] EvolutionPointCapProgression =
    {
        { FranProgressionMilestone.Initial,                 12 }, // 开局
        { FranProgressionMilestone.Skeletron,               18 }, // 骷髅王后
        { FranProgressionMilestone.WallOfFlesh,             28 }, // 血肉墙后
        { FranProgressionMilestone.AllMechanicalBosses,     40 }, // 机械三王后
        { FranProgressionMilestone.Golem,                   47 }, // 石巨人后
        { FranProgressionMilestone.MoonLord,                51 }  // 月亮领主后
    };

    public static object[,] FlightTierProgression =
    {
        { FranProgressionMilestone.WallOfFlesh,              1 }, // 肉后：第一档念动飞行
        { FranProgressionMilestone.AllMechanicalBosses,      2 }, // 机械三王后：第二档
        { FranProgressionMilestone.Plantera,                 3 }, // 世纪之花后：第三档并获得下悬停
        { FranProgressionMilestone.Golem,                    4 }  // 石巨人后：第四档
    };

    public static int[] FlightTimeFrames = { 100, 130, 160, 180 };
    public static float[] FlightRunSpeed = { 7f, 8f, 9f, 9.75f };
    public static float[] FlightAcceleration = { 1.15f, 1.65f, 2.15f, 2.5f };
    public static bool[] FlightHasDownHover = { false, false, true, true };
    public static float[] FlightDownHoverSpeed = { 0f, 0f, 8f, 9f };
    public static float[] FlightDownHoverAcceleration = { 0f, 0f, 5f, 6f };
    public static FranProgressionMilestone InfiniteFlightMilestone = FranProgressionMilestone.EmpressOfLight;

    public static int GetBaseDamage()
    {
        int result = GetInitialBaseDamage();
        foreach ((FranProgressionMilestone milestone, int damage) in
                 FranBalanceSource.GetEnumIntTable(nameof(VanillaBossDamageProgression), VanillaBossDamageProgression))
        {
            if (IsMilestoneCompleted(milestone))
                result = Math.Max(result, damage);
        }
        return Math.Max(1, result);
    }

    public static int GetInitialBaseDamage()
    {
        foreach ((FranProgressionMilestone milestone, int damage) in
                 FranBalanceSource.GetEnumIntTable(nameof(VanillaBossDamageProgression), VanillaBossDamageProgression))
            if (milestone == FranProgressionMilestone.Initial)
                return Math.Max(1, damage);
        return 20;
    }

    public static bool IsTacticUnlocked(FranSkill skill)
    {
        foreach ((FranSkill candidate, FranProgressionMilestone milestone) in
                 FranBalanceSource.GetEnumEnumTable(nameof(TacticUnlockProgression), TacticUnlockProgression))
            if (candidate == skill)
                return IsMilestoneCompleted(milestone);
        return false;
    }

    public static int GetTacticDisplayStage(FranSkill skill)
    {
        int index = (int)skill;
        int[] values = FranBalanceSource.GetIntArray(nameof(TacticDisplayStages), TacticDisplayStages);
        return index >= 0 && index < values.Length ? Math.Clamp(values[index], 1, 4) : 1;
    }

    public static int GetCurrentTacticStage() =>
        GetHighestCompletedValue(nameof(TacticStageProgression), TacticStageProgression, 1, 1, 4);

    public static int GetAvailableTacticSlots() =>
        GetHighestCompletedValue(nameof(TacticSlotProgression), TacticSlotProgression, 1, 0,
            Math.Max(1, TacticStorageCapacity));

    public static int GetMaximumTacticSlots()
    {
        int maximum = 1;
        foreach ((FranProgressionMilestone _, int count) in
                 FranBalanceSource.GetEnumIntTable(nameof(TacticSlotProgression), TacticSlotProgression))
            maximum = Math.Max(maximum, count);
        return Math.Clamp(maximum, 1, Math.Max(1, TacticStorageCapacity));
    }

    public static int GetTacticCooldown(FranSkill skill) =>
        GetArrayValue(nameof(TacticCooldownFrames), TacticCooldownFrames, (int)skill, 0);

    public static float GetComboDamageMultiplier(int combo) =>
        GetArrayValue(nameof(ComboDamageMultipliers), ComboDamageMultipliers, combo, 1f);

    public static float GetComboBaseDuration(int combo) =>
        GetArrayValue(nameof(ComboBaseDurationFrames), ComboBaseDurationFrames, combo,
            GetFloat(nameof(TeacherUseTimeFrames), TeacherUseTimeFrames));

    public static int GetQuickdrawAirburstCount(int combo) =>
        GetArrayValue(nameof(QuickdrawAirburstCounts), QuickdrawAirburstCounts, combo, 1);

    public static float GetQuickdrawAirburstDamageMultiplier(int combo) =>
        GetArrayValue(nameof(QuickdrawAirburstDamageMultipliers), QuickdrawAirburstDamageMultipliers, combo, 0.1f);

    public static int GetEvolutionPointCap() =>
        GetHighestCompletedValue(nameof(EvolutionPointCapProgression), EvolutionPointCapProgression, 0, 0, 65535);

    public static int GetFlightTier() =>
        GetHighestCompletedValue(nameof(FlightTierProgression), FlightTierProgression, 0, 0, 4);

    public static bool HasInfiniteFlight() =>
        IsMilestoneCompleted(FranBalanceSource.GetEnum(nameof(InfiniteFlightMilestone), InfiniteFlightMilestone));

    public static int GetFlightTime(int tier) =>
        GetArrayValue(nameof(FlightTimeFrames), FlightTimeFrames, tier - 1, 0);

    public static float GetFlightRunSpeed(int tier) =>
        GetArrayValue(nameof(FlightRunSpeed), FlightRunSpeed, tier - 1, 0f);

    public static float GetFlightAcceleration(int tier) =>
        GetArrayValue(nameof(FlightAcceleration), FlightAcceleration, tier - 1, 0f);

    public static bool GetFlightHasDownHover(int tier) =>
        GetArrayValue(nameof(FlightHasDownHover), FlightHasDownHover, tier - 1, false);

    public static float GetFlightDownHoverSpeed(int tier) =>
        GetArrayValue(nameof(FlightDownHoverSpeed), FlightDownHoverSpeed, tier - 1, 0f);

    public static float GetFlightDownHoverAcceleration(int tier) =>
        GetArrayValue(nameof(FlightDownHoverAcceleration), FlightDownHoverAcceleration, tier - 1, 0f);

    public static int GetInt(string fieldName, int fallback) => FranBalanceSource.GetInt(fieldName, fallback);
    public static float GetFloat(string fieldName, float fallback) => FranBalanceSource.GetFloat(fieldName, fallback);

    public static bool IsMilestoneCompleted(FranProgressionMilestone milestone) => milestone switch
    {
        FranProgressionMilestone.Initial => true,
        FranProgressionMilestone.KingSlime => NPC.downedSlimeKing,
        FranProgressionMilestone.EyeOfCthulhu => NPC.downedBoss1,
        FranProgressionMilestone.EaterOfWorlds => NPC.downedBoss2,
        FranProgressionMilestone.BrainOfCthulhu => NPC.downedBoss2,
        FranProgressionMilestone.QueenBee => NPC.downedQueenBee,
        FranProgressionMilestone.Deerclops => NPC.downedDeerclops,
        FranProgressionMilestone.Skeletron => NPC.downedBoss3,
        FranProgressionMilestone.DarkMage => DD2Event.DownedInvasionT1,
        FranProgressionMilestone.WallOfFlesh => Main.hardMode,
        FranProgressionMilestone.QueenSlime => NPC.downedQueenSlime,
        FranProgressionMilestone.Destroyer => NPC.downedMechBoss1,
        FranProgressionMilestone.Twins => NPC.downedMechBoss2,
        FranProgressionMilestone.SkeletronPrime => NPC.downedMechBoss3,
        FranProgressionMilestone.Ogre => DD2Event.DownedInvasionT2,
        FranProgressionMilestone.AllMechanicalBosses =>
            NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3,
        FranProgressionMilestone.Plantera => NPC.downedPlantBoss,
        FranProgressionMilestone.Golem => NPC.downedGolemBoss,
        FranProgressionMilestone.DukeFishron => NPC.downedFishron,
        FranProgressionMilestone.EmpressOfLight => NPC.downedEmpressOfLight,
        FranProgressionMilestone.LunaticCultist => NPC.downedAncientCultist,
        FranProgressionMilestone.Betsy => DD2Event.DownedInvasionT3,
        FranProgressionMilestone.MoonLord => NPC.downedMoonlord,
        _ => false
    };

    private static int GetHighestCompletedValue(string fieldName, object[,] fallback, int defaultValue,
        int minimum, int maximum)
    {
        int result = defaultValue;
        foreach ((FranProgressionMilestone milestone, int value) in
                 FranBalanceSource.GetEnumIntTable(fieldName, fallback))
            if (IsMilestoneCompleted(milestone))
                result = Math.Max(result, value);
        return Math.Clamp(result, minimum, maximum);
    }

    private static int GetArrayValue(string fieldName, int[] fallback, int index, int defaultValue)
    {
        int[] values = FranBalanceSource.GetIntArray(fieldName, fallback);
        return index >= 0 && index < values.Length ? values[index] : defaultValue;
    }

    private static float GetArrayValue(string fieldName, float[] fallback, int index, float defaultValue)
    {
        float[] values = FranBalanceSource.GetFloatArray(fieldName, fallback);
        return index >= 0 && index < values.Length ? values[index] : defaultValue;
    }

    private static bool GetArrayValue(string fieldName, bool[] fallback, int index, bool defaultValue)
    {
        bool[] values = FranBalanceSource.GetBoolArray(fieldName, fallback);
        return index >= 0 && index < values.Length ? values[index] : defaultValue;
    }
}

/// <summary>
/// 只负责从 ModSources/Fran/FranBalance.cs 读取最新保存的平衡值。
/// 这是内部工具，不是 ModConfig，也不会把个人设置写进角色或世界存档。
/// </summary>
internal static class FranBalanceSource
{
    private static string cachedPath;
    private static DateTime cachedWriteTime;
    private static string cachedText;

    public static int GetInt(string fieldName, int fallback)
    {
        string source = GetSourceText();
        if (source is null)
            return fallback;
        Match match = Regex.Match(source,
            $@"\b{Regex.Escape(fieldName)}\b\s*=\s*(?<value>[-+]?\d+)", RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups["value"].Value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out int value) ? value : fallback;
    }

    public static float GetFloat(string fieldName, float fallback)
    {
        string source = GetSourceText();
        if (source is null)
            return fallback;
        Match match = Regex.Match(source,
            $@"\b{Regex.Escape(fieldName)}\b\s*=\s*(?<value>[-+]?(?:\d+\.\d+|\d+|\.\d+)f?)",
            RegexOptions.CultureInvariant);
        return match.Success && float.TryParse(match.Groups["value"].Value.TrimEnd('f', 'F'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }

    public static int[] GetIntArray(string fieldName, int[] fallback)
    {
        string initializer = GetInitializer(fieldName);
        if (initializer is null)
            return fallback;
        List<int> values = new();
        foreach (Match match in Regex.Matches(StripComments(initializer), @"[-+]?\d+"))
            if (int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                values.Add(value);
        return values.Count > 0 ? values.ToArray() : fallback;
    }

    public static float[] GetFloatArray(string fieldName, float[] fallback)
    {
        string initializer = GetInitializer(fieldName);
        if (initializer is null)
            return fallback;
        List<float> values = new();
        foreach (Match match in Regex.Matches(StripComments(initializer), @"[-+]?(?:\d+\.\d+|\d+|\.\d+)f?"))
            if (float.TryParse(match.Value.TrimEnd('f', 'F'), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value))
                values.Add(value);
        return values.Count > 0 ? values.ToArray() : fallback;
    }

    public static bool[] GetBoolArray(string fieldName, bool[] fallback)
    {
        string initializer = GetInitializer(fieldName);
        if (initializer is null)
            return fallback;
        List<bool> values = new();
        foreach (Match match in Regex.Matches(StripComments(initializer), @"\b(?:true|false)\b",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            values.Add(match.Value.Equals("true", StringComparison.OrdinalIgnoreCase));
        return values.Count > 0 ? values.ToArray() : fallback;
    }

    public static TEnum GetEnum<TEnum>(string fieldName, TEnum fallback) where TEnum : struct, Enum
    {
        string source = GetSourceText();
        if (source is null)
            return fallback;
        Match match = Regex.Match(source,
            $@"\b{Regex.Escape(fieldName)}\b\s*=\s*(?:\w+\.)?(?<value>\w+)", RegexOptions.CultureInvariant);
        return match.Success && Enum.TryParse(match.Groups["value"].Value, out TEnum value) ? value : fallback;
    }

    public static List<(TEnum Milestone, int Value)> GetEnumIntTable<TEnum>(string fieldName, object[,] fallback)
        where TEnum : struct, Enum
    {
        List<(TEnum, int)> parsed = new();
        foreach (string row in GetRows(fieldName))
        {
            List<string> cells = SplitCells(row);
            if (cells.Count >= 2 && TryParseEnum(cells[0], out TEnum milestone) &&
                TryParseInt(cells[1], out int value))
                parsed.Add((milestone, value));
        }
        if (parsed.Count > 0)
            return parsed;

        for (int row = 0; row < fallback.GetLength(0); row++)
            if (fallback[row, 0] is TEnum milestone && fallback[row, 1] is int value)
                parsed.Add((milestone, value));
        return parsed;
    }

    public static List<(TFirst First, TSecond Second)> GetEnumEnumTable<TFirst, TSecond>(string fieldName,
        object[,] fallback) where TFirst : struct, Enum where TSecond : struct, Enum
    {
        List<(TFirst, TSecond)> parsed = new();
        foreach (string row in GetRows(fieldName))
        {
            List<string> cells = SplitCells(row);
            if (cells.Count >= 2 && TryParseEnum(cells[0], out TFirst first) &&
                TryParseEnum(cells[1], out TSecond second))
                parsed.Add((first, second));
        }
        if (parsed.Count > 0)
            return parsed;

        for (int row = 0; row < fallback.GetLength(0); row++)
            if (fallback[row, 0] is TFirst first && fallback[row, 1] is TSecond second)
                parsed.Add((first, second));
        return parsed;
    }

    private static IEnumerable<string> GetRows(string fieldName)
    {
        string initializer = GetInitializer(fieldName);
        if (initializer is null)
            yield break;

        int depth = 0;
        int start = -1;
        bool inString = false;
        for (int i = 0; i < initializer.Length; i++)
        {
            char character = initializer[i];
            if (character == '"' && (i == 0 || initializer[i - 1] != '\\'))
                inString = !inString;
            if (inString)
                continue;
            if (character == '{')
            {
                if (depth == 0)
                    start = i + 1;
                depth++;
            }
            else if (character == '}')
            {
                depth--;
                if (depth == 0 && start >= 0)
                {
                    yield return initializer.Substring(start, i - start);
                    start = -1;
                }
            }
        }
    }

    private static List<string> SplitCells(string row)
    {
        row = StripComments(row);
        List<string> cells = new();
        int start = 0;
        int depth = 0;
        for (int i = 0; i < row.Length; i++)
        {
            if (row[i] is '(' or '[' or '{')
                depth++;
            else if (row[i] is ')' or ']' or '}')
                depth--;
            else if (row[i] == ',' && depth == 0)
            {
                cells.Add(row.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }
        if (start < row.Length)
            cells.Add(row.Substring(start).Trim());
        return cells;
    }

    private static bool TryParseEnum<TEnum>(string cell, out TEnum value) where TEnum : struct, Enum
    {
        Match match = Regex.Match(cell.Trim(), @"(?:\w+\.)?(?<value>\w+)\s*$", RegexOptions.CultureInvariant);
        return match.Success && Enum.TryParse(match.Groups["value"].Value, out value);
    }

    private static bool TryParseInt(string cell, out int value)
    {
        Match match = Regex.Match(cell, @"[-+]?\d+", RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static string GetInitializer(string fieldName)
    {
        string source = GetSourceText();
        if (source is null)
            return null;
        Match match = Regex.Match(source, $@"\b{Regex.Escape(fieldName)}\b\s*=", RegexOptions.CultureInvariant);
        if (!match.Success)
            return null;
        int braceStart = source.IndexOf('{', match.Index + match.Length);
        if (braceStart < 0)
            return null;

        int depth = 0;
        bool inString = false;
        for (int i = braceStart; i < source.Length; i++)
        {
            char character = source[i];
            if (character == '"' && (i == 0 || source[i - 1] != '\\'))
                inString = !inString;
            if (inString)
                continue;
            if (character == '{')
                depth++;
            else if (character == '}' && --depth == 0)
                return source.Substring(braceStart + 1, i - braceStart - 1);
        }
        return null;
    }

    private static string GetSourceText()
    {
        string path = ResolveSourcePath();
        if (path is null)
            return null;
        DateTime writeTime = File.GetLastWriteTimeUtc(path);
        if (path == cachedPath && writeTime == cachedWriteTime && cachedText is not null)
            return cachedText;
        try
        {
            cachedText = File.ReadAllText(path);
            cachedPath = path;
            cachedWriteTime = writeTime;
            return cachedText;
        }
        catch
        {
            return cachedText;
        }
    }

    private static string ResolveSourcePath()
    {
        string savePath = Main.SavePath;
        if (!string.IsNullOrWhiteSpace(savePath))
        {
            string sourcePath = Path.Combine(savePath, "ModSources", "Fran", "FranBalance.cs");
            if (File.Exists(sourcePath))
                return sourcePath;
        }

        string current = Directory.GetCurrentDirectory();
        if (!string.IsNullOrWhiteSpace(current))
        {
            string direct = Path.Combine(current, "FranBalance.cs");
            if (File.Exists(direct))
                return direct;
            string nested = Path.Combine(current, "ModSources", "Fran", "FranBalance.cs");
            if (File.Exists(nested))
                return nested;
        }
        return null;
    }

    private static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(text, @"//.*", string.Empty);
    }
}
