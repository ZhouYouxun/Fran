using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Terraria;
using Terraria.GameContent.Events;

namespace FranMod.Core;

public enum FranProgressionMilestone
{
    Initial,
    KingSlime,
    EyeOfCthulhu,
    EaterOfWorlds,
    BrainOfCthulhu,
    AnyEvilBoss,
    QueenBee,
    Deerclops,
    Skeletron,
    DarkMage,
    WallOfFlesh,
    QueenSlime,
    Destroyer,
    Twins,
    SkeletronPrime,
    AnyMechanicalBoss,
    AnyTwoMechanicalBosses,
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

/// <summary>FranBalance.cs 的内部接线与其余固定战斗参数。</summary>
public static partial class FranBalance
{
    // 技术容量只负责存档和联机包的固定长度，不代表实际可用槽位。
    // 实际槽位由下方 TacticSlotProgression 决定，目前最终上限是 3。
    public static int TacticStorageCapacity = 4;

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

    // 魔石回廊：以下是现有进化节点给予的实战数值，节点关系本身仍由 FranEvolution 定义。
    public static float EvolutionArcaneManaCostReduction = 0.08f;       // 魔力循环：魔力消耗降低
    public static float EvolutionFelineMoveSpeedBonus = 0.05f;          // 猫科反射：移动速度
    public static int EvolutionSharedManaBonus = 20;                    // 共享魔力：最大魔力
    public static int EvolutionHarmonicGuardDefense = 3;                // 和谐守护：防御
    public static float EvolutionHarmonicGuardEndurance = 0.02f;        // 和谐守护：减伤
    public static float EvolutionSynchronizedMagicDamageBonus = 0.06f;  // 同步齐射：魔法伤害
    public static float EvolutionEternalGenericDamageBonus = 0.05f;     // 永恒契约：全伤害
    public static int EvolutionEternalManaBonus = 20;                   // 永恒契约：最大魔力
    public static int EvolutionEternalDefense = 3;                      // 永恒契约：防御
    public static int EvolutionComboResetBaseFrames = 70;               // 基础连段保留时间
    public static int EvolutionComboResetMindSyncFrames = 105;          // 心灵同步后的连段保留时间
    public static int EvolutionComboResetSharedVisionFrames = 125;      // 共享视野后的连段保留时间
    public static int EvolutionAnalysisArmorPenetration = 12;           // 魔石分析：护甲穿透
    public static int EvolutionDeeperAnalysisArmorPenetration = 18;     // 深层分析：护甲穿透
    public static float EvolutionEchoDamageMultiplier = 0.50f;          // 自我进化回声斩倍率
    public static float EvolutionDeeperEchoDamageMultiplier = 0.70f;    // 深层分析后的回声斩倍率
    public static int EvolutionCatStepDashCooldownFrames = 42;          // 猫步：冲刺冷却
    public static int EvolutionFelineReflexDashCooldownFrames = 38;     // 猫科反射：冲刺冷却
    public static int EvolutionAwakeningDashCooldownFrames = 34;        // 黑猫觉醒：冲刺冷却
    public static float EvolutionSkyHunterDashSpeedBonus = 0.10f;       // 天空猎手：冲刺速度
    public static float EvolutionAwakeningDashSpeedBonus = 0.05f;       // 黑猫觉醒：额外冲刺速度
    public static float EvolutionPredatorDashDamageBonus = 0.20f;       // 捕食视野：冲刺伤害
    public static float EvolutionAwakeningDashDamageBonus = 0.10f;      // 黑猫觉醒：额外冲刺伤害
    public static int EvolutionPhantomStepImmunityFrames = 4;           // 幻影步：额外无敌帧
    public static int EvolutionUltimateChargePulseInterval = 5;         // 极限共鸣：每隔多少帧追加充能
    public static int EvolutionUltimateChargePulseAmount = 2;           // 极限共鸣：该帧获得的充能量
    public static float EvolutionEdgeReachMultiplier = 1.06f;           // 刃口校准：剑身触及范围
    public static float EvolutionArcaneReachMultiplier = 1.04f;         // 魔力循环：额外范围
    public static float EvolutionUnityReachMultiplier = 1.10f;          // 人剑合一：范围
    public static float EvolutionMutualUnityReachMultiplier = 1.12f;    // 相互成长后的人剑合一范围
    public static float EvolutionSharedRhythmFinisherDuration = 0.88f;  // 共享节奏：第四剑耗时倍率
    public static float EvolutionHunterTempoDuration = 0.88f;           // 狩猎节奏：下一剑耗时倍率
    public static float EvolutionAwakeningHunterTempoDuration = 0.82f;  // 黑猫觉醒后的狩猎节奏倍率
    public static float EvolutionSwordSaintDamageMultiplier = 1.06f;    // 剑圣推演：师父本体伤害
    public static float EvolutionSequenceDamagePerHit = 0.03f;          // 连段计算：每次连续命中增伤
    public static float EvolutionSwordSaintSequenceDamagePerHit = 0.04f;// 剑圣推演后的每次连续命中增伤
    public static int EvolutionMaximumSequenceDamageHits = 3;           // 连段计算最多计入几次命中
    public static int EvolutionUnityDurationSeconds = 5;                // 人剑合一持续秒数
    public static int EvolutionMutualUnityDurationSeconds = 7;          // 相互成长后持续秒数
    public static int EvolutionDashRefundTargetLockFrames = 180;        // 同一敌人的冲刺返还锁
    public static float EvolutionDashRefundRemainingMultiplier = 0.40f;// 进化步法：剩余冷却倍率
    public static float EvolutionAwakeningDashRefundRemainingMultiplier = 0.25f; // 黑猫觉醒后倍率
    public static float EvolutionResonantPullSpeed = 5f;                // 共鸣牵引基础速度
    public static float EvolutionResonantPullDistanceRatio = 0.08f;     // 共鸣牵引按距离换算的速度
    public static float EvolutionSoulBridgePullMultiplier = 1.40f;      // 灵魂桥接：牵引强化
    public static float EvolutionResonantPullSpeedCap = 12f;            // 共鸣牵引速度上限

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
        GetCurrentCoreProgression(out int damage, out _);
        return damage;
    }

    public static int GetInitialBaseDamage()
    {
        foreach ((FranProgressionMilestone milestone, int damage, float _) in
                 FranBalanceSource.GetCoreProgressionTable(nameof(CoreProgression), CoreProgression))
            if (milestone == FranProgressionMilestone.Initial)
                return Math.Max(1, damage);
        return 1;
    }

    public static float GetCoreSizeMultiplier()
    {
        GetCurrentCoreProgression(out _, out float sizeMultiplier);
        return sizeMultiplier;
    }

    public static float GetCurrentDamageMultiplier() =>
        GetBaseDamage() / (float)Math.Max(1, GetInitialBaseDamage());

    private static void GetCurrentCoreProgression(out int damage, out float sizeMultiplier)
    {
        damage = GetInitialBaseDamageFallback();
        sizeMultiplier = GetInitialCoreSizeFallback();
        int bestRank = -1;
        foreach ((FranProgressionMilestone milestone, int candidateDamage, float candidateSize) in
                 FranBalanceSource.GetCoreProgressionTable(nameof(CoreProgression), CoreProgression))
        {
            int rank = GetDamageProgressionRank(milestone);
            if (!IsMilestoneCompleted(milestone) || rank < bestRank)
                continue;
            bestRank = rank;
            damage = Math.Max(1, candidateDamage);
            sizeMultiplier = Math.Max(0.1f, candidateSize);
        }
    }

    private static int GetInitialBaseDamageFallback()
    {
        for (int row = 0; row < CoreProgression.GetLength(0); row++)
            if (CoreProgression[row, 0] is FranProgressionMilestone.Initial && CoreProgression[row, 1] is int value)
                return Math.Max(1, value);
        return 1;
    }

    private static float GetInitialCoreSizeFallback()
    {
        for (int row = 0; row < CoreProgression.GetLength(0); row++)
            if (CoreProgression[row, 0] is FranProgressionMilestone.Initial &&
                CoreProgression[row, 2] is float value)
                return Math.Max(0.1f, value);
        return 1f;
    }

    public static bool IsTacticUnlocked(FranSkill skill)
    {
        foreach ((FranSkill candidate, FranProgressionMilestone milestone) in
                 FranBalanceSource.GetEnumEnumTable<FranSkill, FranProgressionMilestone>(
                     nameof(TacticUnlockProgression), TacticUnlockProgression))
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
                 FranBalanceSource.GetEnumIntTable<FranProgressionMilestone>(
                     nameof(TacticSlotProgression), TacticSlotProgression))
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
        // 原版只保存一个邪恶 Boss 旗标，普通世界只能结合世界邪恶类型区分这两个节点。
        FranProgressionMilestone.EaterOfWorlds => NPC.downedBoss2 && !WorldGen.crimson,
        FranProgressionMilestone.BrainOfCthulhu => NPC.downedBoss2 && WorldGen.crimson,
        FranProgressionMilestone.AnyEvilBoss => NPC.downedBoss2,
        FranProgressionMilestone.QueenBee => NPC.downedQueenBee,
        FranProgressionMilestone.Deerclops => NPC.downedDeerclops,
        FranProgressionMilestone.Skeletron => NPC.downedBoss3,
        FranProgressionMilestone.DarkMage => DD2Event.DownedInvasionT1,
        FranProgressionMilestone.WallOfFlesh => Main.hardMode,
        FranProgressionMilestone.QueenSlime => NPC.downedQueenSlime,
        FranProgressionMilestone.Destroyer => NPC.downedMechBoss1,
        FranProgressionMilestone.Twins => NPC.downedMechBoss2,
        FranProgressionMilestone.SkeletronPrime => NPC.downedMechBoss3,
        FranProgressionMilestone.AnyMechanicalBoss => NPC.downedMechBossAny,
        FranProgressionMilestone.AnyTwoMechanicalBosses =>
            (NPC.downedMechBoss1 ? 1 : 0) + (NPC.downedMechBoss2 ? 1 : 0) +
            (NPC.downedMechBoss3 ? 1 : 0) >= 2,
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

    private static int GetDamageProgressionRank(FranProgressionMilestone milestone) => milestone switch
    {
        FranProgressionMilestone.Initial or FranProgressionMilestone.KingSlime => 0,
        FranProgressionMilestone.EyeOfCthulhu => 10,
        FranProgressionMilestone.EaterOfWorlds or FranProgressionMilestone.BrainOfCthulhu or
            FranProgressionMilestone.AnyEvilBoss or FranProgressionMilestone.QueenBee or
            FranProgressionMilestone.Deerclops => 20,
        FranProgressionMilestone.Skeletron or FranProgressionMilestone.DarkMage => 30,
        FranProgressionMilestone.WallOfFlesh or FranProgressionMilestone.QueenSlime => 40,
        FranProgressionMilestone.Destroyer or FranProgressionMilestone.Twins or
            FranProgressionMilestone.SkeletronPrime or FranProgressionMilestone.AnyMechanicalBoss or
            FranProgressionMilestone.Ogre => 50,
        FranProgressionMilestone.AnyTwoMechanicalBosses => 60,
        FranProgressionMilestone.AllMechanicalBosses => 70,
        FranProgressionMilestone.Plantera or FranProgressionMilestone.DukeFishron or
            FranProgressionMilestone.EmpressOfLight => 80,
        FranProgressionMilestone.Golem or FranProgressionMilestone.Betsy => 90,
        FranProgressionMilestone.LunaticCultist => 100,
        FranProgressionMilestone.MoonLord => 110,
        _ => -1
    };

    private static int GetHighestCompletedValue(string fieldName, object[,] fallback, int defaultValue,
        int minimum, int maximum)
    {
        int result = defaultValue;
        foreach ((FranProgressionMilestone milestone, int value) in
                 FranBalanceSource.GetEnumIntTable<FranProgressionMilestone>(fieldName, fallback))
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
    private static ulong cachedCheckTick = ulong.MaxValue;

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

    public static List<(FranProgressionMilestone Milestone, int Damage, float Size)> GetCoreProgressionTable(
        string fieldName, object[,] fallback)
    {
        List<(FranProgressionMilestone, int, float)> parsed = new();
        foreach (string row in GetRows(fieldName))
        {
            List<string> cells = SplitCells(row);
            if (cells.Count >= 3 && TryParseEnum(cells[0], out FranProgressionMilestone milestone) &&
                TryParseInt(cells[1], out int damage) && TryParseFloat(cells[2], out float size))
                parsed.Add((milestone, damage, size));
        }
        if (parsed.Count > 0)
            return parsed;

        for (int row = 0; row < fallback.GetLength(0); row++)
        {
            if (fallback[row, 0] is not FranProgressionMilestone milestone || fallback[row, 1] is not int damage)
                continue;
            object rawSize = fallback[row, 2];
            if (rawSize is float floatSize)
                parsed.Add((milestone, damage, floatSize));
            else if (rawSize is double doubleSize)
                parsed.Add((milestone, damage, (float)doubleSize));
            else if (rawSize is int intSize)
                parsed.Add((milestone, damage, intSize));
        }
        return parsed;
    }

    public static List<(TEnum Milestone, float Value)> GetEnumFloatTable<TEnum>(string fieldName, object[,] fallback)
        where TEnum : struct, Enum
    {
        List<(TEnum, float)> parsed = new();
        foreach (string row in GetRows(fieldName))
        {
            List<string> cells = SplitCells(row);
            if (cells.Count >= 2 && TryParseEnum(cells[0], out TEnum milestone) &&
                TryParseFloat(cells[1], out float value))
                parsed.Add((milestone, value));
        }
        if (parsed.Count > 0)
            return parsed;

        for (int row = 0; row < fallback.GetLength(0); row++)
        {
            if (fallback[row, 0] is not TEnum milestone)
                continue;
            object rawValue = fallback[row, 1];
            if (rawValue is float floatValue)
                parsed.Add((milestone, floatValue));
            else if (rawValue is int intValue)
                parsed.Add((milestone, intValue));
            else if (rawValue is double doubleValue)
                parsed.Add((milestone, (float)doubleValue));
        }
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
        value = default;
        Match match = Regex.Match(cell.Trim(), @"(?:\w+\.)?(?<value>\w+)\s*$", RegexOptions.CultureInvariant);
        return match.Success && Enum.TryParse(match.Groups["value"].Value, out value);
    }

    private static bool TryParseInt(string cell, out int value)
    {
        value = default;
        Match match = Regex.Match(cell, @"[-+]?\d+", RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseFloat(string cell, out float value)
    {
        value = default;
        Match match = Regex.Match(cell, @"[-+]?(?:\d+\.\d+|\d+|\.\d+)f?", RegexOptions.CultureInvariant);
        return match.Success && float.TryParse(match.Value.TrimEnd('f', 'F'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out value);
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
        // 一帧内可能有大量弹幕读取平衡值；每帧最多检查一次文件时间，避免无意义的磁盘查询。
        ulong currentTick = Main.GameUpdateCount;
        if (cachedText is not null && cachedCheckTick == currentTick)
            return cachedText;
        cachedCheckTick = currentTick;

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
