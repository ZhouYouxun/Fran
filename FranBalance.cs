namespace FranMod.Core;

/// <summary>
/// 弗兰平衡驾驶舱：集中保存本体成长、战术解锁、槽位成长和八种战术的固定参数。
/// 运行时统一读取本文件中的配置值，战斗实现中不再维护同类常量。
/// </summary>
public static partial class FranBalance
{
    // 1：核心大剑进度（世界节点、核心攻击力、核心大小、冲刺面板伤害）
    // 本表只管师父本体及其派生数值，不参与战术解锁条件的判定。
    // 以“任意一只机械 Boss 后 75 点”为锚点，伤害统一按旧表的 1/8 缩放并取最近整数。
    // 背包中收藏伤害调试器时，只覆盖第二列的核心攻击力。
    public static object[,] CoreProgression =
    {
        // 世界 Boss / 条件                              攻击力   大小   冲刺伤害
        { FranProgressionMilestone.Initial,                          13,  1.00f,      16 }, // 刚开局
        { FranProgressionMilestone.EyeOfCthulhu,                     23,  1.00f,      16 }, // 击败克苏鲁之眼
        { FranProgressionMilestone.AnyEvilBoss,                      34,  1.00f,      16 }, // 击败任意一个邪恶世界 Boss
        { FranProgressionMilestone.Skeletron,                        38,  1.00f,      16 }, // 击败骷髅王
        { FranProgressionMilestone.WallOfFlesh,                      62,  1.10f,      74 }, // 击败血肉墙
        { FranProgressionMilestone.AnyMechanicalBoss,                75,  1.10f,      74 }, // 击败任意一只机械 Boss
        { FranProgressionMilestone.AnyTwoMechanicalBosses,           88,  1.20f,      74 }, // 击败任意两只机械 Boss
        { FranProgressionMilestone.AllMechanicalBosses,             109,  1.20f,      74 }, // 机械三王全部击败
        { FranProgressionMilestone.Plantera,                        159,  1.20f,     191 }, // 击败世纪之花
        { FranProgressionMilestone.Golem,                           242,  1.30f,     191 }, // 击败石巨人
        { FranProgressionMilestone.LunaticCultist,                  234,  1.30f,     191 }, // 击败拜月教邪教徒
        { FranProgressionMilestone.MoonLord,                        356,  1.40f,     428 }  // 击败月亮领主
    };

    // 2：八种战术的解锁条件（战术、世界节点）
    public static object[,] TacticUnlockProgression =
    {
        { FranSkill.TelekineticCatapult,   FranProgressionMilestone.Initial },             // 念动弹射：默认解锁
        { FranSkill.SkillTaker,            FranProgressionMilestone.Initial },             // 技能夺取者：默认解锁
        { FranSkill.MagicBarrier,          FranProgressionMilestone.EyeOfCthulhu },        // 魔力屏障：克苏鲁之眼后解锁
        { FranSkill.MultipleBodyCreation,  FranProgressionMilestone.Skeletron },           // 多重分体：骷髅王后解锁
        { FranSkill.BlueResonance,         FranProgressionMilestone.WallOfFlesh },         // 蓝辉共鸣：血肉墙后解锁
        { FranSkill.ThunderChain,          FranProgressionMilestone.AnyMechanicalBoss },   // 雷链：任意机械 Boss 后解锁
        { FranSkill.PressurizedQuickdraw,  FranProgressionMilestone.Plantera },            // 压缩居合：世纪之花后解锁
        { FranSkill.ThorsHammer,           FranProgressionMilestone.Golem }                // 雷神之锤：石巨人后解锁
    };

    // 3：战术槽位成长（世界节点、可用槽位）
    public static object[,] TacticSlotProgression =
    {
        { FranProgressionMilestone.Initial,       2 }, // 开局可装备两个战术槽
        { FranProgressionMilestone.WallOfFlesh,   3 }, // 血肉墙后开放第三个战术槽
        { FranProgressionMilestone.MoonLord,      4 }  // 月亮领主后开放第四个战术槽
    };

    // =====================================================================
    // 黑天虎大招：普通斩、终结斩及命中裂隙统一使用师父当前面板伤害的固定倍率。
    // 每刀开始时重新读取面板；不叠加普通四刀倍率或旧终结斩的额外倍率。
    // =====================================================================
    public static float UltimateStrikeDamageMultiplier = 8f;
    public static int UltimateNormalStrikeCount = 36;         // 保持 36 次高速斩，之后仍接一次终结斩
    public static int UltimateStartingStrikeFrames = 6;       // 原 9 帧 × 0.70，取整为 6 帧
    public static int UltimateStartingDriftFrames = 4;        // 原 6 帧 × 0.70，取整为 4 帧；起始整轮 10 帧
    public static int UltimateEndingStrikeFrames = 1;         // 末段以完整出刀间隔 2 帧为优先目标
    public static int UltimateEndingDriftFrames = 1;          // 1 帧穿斩 + 1 帧漂移 = 2 帧一次
    public static int UltimateFinalPauseFrames = 20;          // 保留终结斩前的停顿
    public static int UltimateFinalStrikeFrames = 9;          // 保留独立终结斩的动作时长，不是高速连斩间隔

    // =====================================================================
    // 4：八种战术的固定倍率与重复叠层
    // =====================================================================
    // 统一口径：这里的 0.50f 表示“当前剑击伤害的 50%”，不是固定伤害数值。
    // 师父本体只通过上方 CoreProgression 成长；战术倍率不会因为世界进度自动升降。
    // 同一战术重复装备采用 A+B+B：第一层提供完整机制 A，后续每层重复提供相同增量 B。
    // 解锁较晚只代表机制较晚开放，不代表它必须拥有更高倍率；频率、范围、防御和治疗也计入强度。

    public static int[] TacticCooldownFrames =
    {
        0,   // 念动弹射：无独立冷却，每刀触发
        0,   // 技能夺取者：无独立冷却，每次剑击命中触发
        150, // 魔力屏障：破裂后 150 帧，也就是 2.5 秒重构
        0,   // 多重分体：无独立冷却，每刀触发
        0,   // 蓝辉共鸣：无战术冷却，治疗另有共享冷却
        0,   // 雷链：无独立冷却，跟随四段剑术推进
        0,   // 压缩居合：无独立冷却，每刀触发空气爆点
        0    // 雷神之锤：用完整连段轮数限制，不使用普通帧冷却
    };

    // ---------------------------------------------------------------------
    // 战术一：念动弹射（开局解锁）
    // 念动弹射使用“当前时期 × 固定四刀”的状态机，不改变师父本体的四段剑术。
    // 以下倍率按第一层 A 的基础阵列数量分摊伤害；后续每层 B 只固定增加一枚念压核。
    // ---------------------------------------------------------------------
    public static float TelekineticSingleProjectileDamageMultiplier = 0.72f; // A 中只有 1 枚弹幕时：该弹幕为当前剑击的 72%
    public static float TelekineticTwoShellDamageMultiplier = 0.42f;   // 齐射共 2 枚时：每枚光弹为当前剑击的 42%
    public static float TelekineticThreeShellDamageMultiplier = 0.31f; // 齐射共 3 枚时：每枚光弹为当前剑击的 31%
    public static float TelekineticManyShellDamageMultiplier = 0.24f;  // 齐射达到 4 枚以上：每枚光弹为当前剑击的 24%
    public static float TelekineticHeavyProjectileDamageMultiplier = 0.88f; // 单一重型念压核或重型激光为当前剑击的 88%
    public static float TelekineticExtraLayerProjectileDamageMultiplier = 0.24f; // 每层 B 新增的单枚侧翼念压核为当前剑击的 24%
    public static float TelekineticPressureCoreInitialSpeedMultiplier = 1.70f; // 念压核实际起飞速度提升为原来的 1.70 倍
    public static float TelekineticPressureCoreAcceleration = 1.035f; // 念压核加速阶段内每个正常帧的速度倍率
    public static int TelekineticPressureCoreAccelerationFrames = 20; // 起飞二十帧后停止加速并保持最高速度
    public static float TelekineticPressureCoreHomingRange = 920f; // 全槽念动弹射时，念压核的首次锁敌/失敌补锁半径
    public static int TelekineticPressureCoreHomingDelayFrames = 9; // 散射基准帧数；侧翼按层级错峰，最终限制在 8～12 帧
    // 掠近、弧扫、切入和一次回勾的运动参数集中在 FranTelekineticSkirmishMotion 中。
    public static int TelekineticFoldBladeHitLimit = 10; // 折念刃冲击波最多造成十次伤害
    public static float TelekineticFoldBladeLaunchSpeed = 60f; // 泰拉刃式高速起步
    public static float TelekineticFoldBladeFlightDeceleration = 0.94f; // 高于最低速度时每帧保留的速度
    public static float TelekineticFoldBladeHitSpeedRetention = 0.75f; // 每次命中后保留的飞行速度
    public static float TelekineticFoldBladeHitDamageRetention = 0.75f; // 每次命中后保留的后续伤害
    public static int[] TelekineticLaserDamagingHits =
    {
        1, // 肉前预留档：当前状态机尚未使用激光
        2, // 困难模式：可造成 2 次有效命中
        3, // 世纪之花后：可造成 3 次有效命中
        5  // 月亮领主后：可造成 5 次有效命中
    };
    public static float TelekineticFinisherPlanetDamageMultiplier = 0.17f;  // 月亮领主后第四刀：每颗星球直接命中为当前剑击的 17%
    public static float TelekineticPlanetExplosionDamageMultiplier = 0.75f; // 星球爆炸为该颗星球直接伤害的 75%

    // ---------------------------------------------------------------------
    // 战术二：技能夺取者（开局解锁）
    // 第一层 A 施加会成长、也会缓慢衰减的易伤印记；重复层 B 只缩短成长时间并延长衰减时间。
    // ---------------------------------------------------------------------
    public static float SkillTakerMinimumDamageMultiplier = 1.10f;   // 标记刚建立时，目标受到 1.10 倍伤害
    public static float SkillTakerMaximumDamageMultiplier = 1.30f;   // 无论装配几层，易伤上限始终固定为 1.30 倍
    public static float SkillTakerFullLoadoutMaximumDamageMultiplier = 1.50f; // 全选：易伤上限提升至 1.50 倍，成长时间仍由普通叠层规则决定
    public static int SkillTakerBaseRampFrames = 420;                // 第一层 A：持续攻击约 7 秒达到易伤上限
    public static int SkillTakerExtraLayerRampReductionFrames = 60;  // 重复层 B：每层使达到上限所需时间缩短 1 秒
    public static int SkillTakerBaseDecayFrames = 180;               // 第一层 A：停手后约 3 秒缓慢衰减并消失
    public static int SkillTakerExtraLayerDecayFrames = 60;          // 重复层 B：每层使标记额外维持 1 秒
    public static int SkillTakerContinuousHitGraceFrames = 30;       // 两次命中间隔不超过半秒时视为持续攻击
    public static int SkillTakerBaseWispCount = 2;                   // 首层附带的紫色灵弹数量；重复层不再增加
    public static float SkillTakerWispDamageMultiplier = 0.24f;      // 每枚紫色灵弹为当前剑击的 24%

    // ---------------------------------------------------------------------
    // 战术三：魔力屏障（开局解锁）
    // 第一层 A 创建屏障并提供基础防护；重复层 B 只线性增加减伤与防御，不增加耐久和反击次数。
    // 当当前全部可用战术槽都装配魔力屏障时，额外触发敌人接触伤害减半与生命回复 +35。
    // ---------------------------------------------------------------------
    public static int MagicBarrierMaximumIntegrity = 5;                  // 屏障固定承受 5 次攻击后破裂
    public static float MagicBarrierWeaponDamageMultiplier = 0.72f;      // 屏障记录伤害为当前武器面板伤害的 72%
    public static float MagicBarrierBaseDamageReduction = 0.10f;         // 第一层 A：获得 10% 伤害减免
    public static float MagicBarrierExtraLayerDamageReduction = 0.05f;   // 重复层 B：每层再获得 5% 伤害减免
    public static int MagicBarrierBaseDefense = 10;                       // 第一层 A：获得 10 点防御力
    public static int MagicBarrierExtraLayerDefense = 5;                  // 重复层 B：每层再获得 5 点防御力
    public static float MagicBarrierFullLoadoutContactDamageMultiplier = 0.50f; // 当前全部可用槽位均为屏障时，敌人接触伤害减半
    public static int MagicBarrierFullLoadoutLifeRegen = 35;              // 全选：屏障生效期间生命回复增加 35
    public static float MagicBarrierCounterDamageMultiplier = 0.86f;     // 每次受击反击为屏障记录伤害的 86%
    public static int MagicBarrierHitImmunityFrames = 18;                // 屏障成功抵挡后给予 18 帧受击保护

    // ---------------------------------------------------------------------
    // 战术四：多重分体（骷髅王后解锁）
    // 这里的伤害倍率是第一层整组分身的总伤害；代码按第一层数量换算单体伤害。
    // 重复层只增加分身数量，每个新增分身沿用同一个单体倍率，不再额外叠乘伤害倍率。
    // ---------------------------------------------------------------------
    public static int MultipleBodyNormalBaseCount = 2;                   // 第一层 A：普通三刀各生成 2 个分身
    public static int MultipleBodyFinisherBaseCount = 4;                 // 第一层 A：第四刀生成 4 个分身
    public static int MultipleBodyNormalExtraLayerCount = 1;             // 重复层 B：普通三刀每层增加 1 个分身
    public static int MultipleBodyFinisherExtraLayerCount = 2;           // 重复层 B：第四刀每层增加 2 个分身
    public static float MultipleBodyNormalDamageMultiplier = 0.86f;      // 普通三刀：整组分身总伤害为当前剑击的 86%
    public static float MultipleBodyFinisherDamageMultiplier = 1.24f;    // 第四刀：整组分身总伤害为当前剑击的 124%
    public static float MultipleBodyFullLoadoutSpeedMultiplier = 1.15f;  // 全选：分体突进速度为普通状态的 1.15 倍

    // ---------------------------------------------------------------------
    // 战术五：蓝辉共鸣（骷髅王后解锁）
    // 倍率是“单枚蓝辉弹”的伤害；重复层每次固定增加一枚，因此天然是 A+B+B。
    // ---------------------------------------------------------------------
    public static int BlueResonanceNormalBaseCount = 3;                  // 第一层 A：普通三刀命中时迸发 3 枚蓝辉弹
    public static int BlueResonanceFinisherBaseCount = 5;                // 第一层 A：第四刀命中时迸发 5 枚蓝辉弹
    public static int BlueResonanceExtraLayerCount = 1;                  // 重复层 B：每层固定增加 1 枚蓝辉弹
    public static float BlueResonanceNormalDamageMultiplier = 0.18f;     // 普通三刀：每枚蓝辉弹为当前剑击的 18%
    public static float BlueResonanceFinisherDamageMultiplier = 0.14f;   // 第四刀：每枚蓝辉弹为当前剑击的 14%
    public static int BlueResonanceHealingLifeDivisor = 80;              // 每枚治疗弹基础值为目标最大生命除以 80
    public static int BlueResonanceHealingMinimum = 2;                   // 单次治疗最少恢复 2 点生命
    public static int BlueResonanceHealingMaximum = 7;                   // 每枚治疗弹最多恢复 7 点生命；叠层只增加弹幕数量
    public static float BlueResonanceFullLoadoutHealingMultiplier = 1.50f; // 全选：最终治疗量乘 1.50

    // ---------------------------------------------------------------------
    // 战术六：雷链（血肉墙后解锁）
    // 主雷与伴随雷共用主雷倍率；追踪雷单独使用追踪倍率。
    // 重复层增加固定数量的伴随雷和追踪目标；额外全局增伤默认关闭，避免数量与倍率交叉膨胀。
    // ---------------------------------------------------------------------
    public static float ThunderChainMainDamageMultiplier = 0.31f;        // 每道主雷或伴随雷为当前剑击的 31%
    public static float ThunderChainTrackedDamageMultiplier = 0.37f;     // 每道自动追踪雷为当前剑击的 37%
    public static float ThunderChainExtraLayerDamageMultiplier = 0f;     // 保持 0 可严格维持 A+B+B；重复层已通过数量增伤
    public static int ThunderChainNormalSupportingBoltCount = 1;         // 普通阶段基础追加 1 道伴随雷
    public static int ThunderChainFinisherSupportingBoltCount = 2;       // 第四刀基础追加 2 道伴随雷
    public static int ThunderChainNormalTrackedTargetCount = 3;          // 普通阶段最多自动锁定 3 个敌人
    public static int ThunderChainFinisherTrackedTargetCount = 4;        // 第四刀最多自动锁定 4 个敌人
    public static float ThunderChainFullLoadoutRainDamageMultiplier = 2.50f; // 全选且世界正在下雨时，每一道雷的伤害乘 2.50

    // ---------------------------------------------------------------------
    // 战术七：压缩居合（机械三王后解锁）
    // 数组从左到右依次对应第一、第二、第三、第四刀；倍率是“单个空气爆点”的伤害。
    // ---------------------------------------------------------------------
    public static int[] QuickdrawAirburstCounts =
    {
        5, // 第一刀：5 个爆点
        6, // 第二刀：6 个爆点
        8, // 第三刀：8 个爆点
        7  // 第四刀：7 个爆点
    };
    public static float[] QuickdrawAirburstDamageMultipliers =
    {
        0.18f, // 第一刀：每个爆点为当前剑击的 18%，整组理论值 90%
        0.16f, // 第二刀：每个爆点为当前剑击的 16%，整组理论值 96%
        0.13f, // 第三刀：每个爆点为当前剑击的 13%，整组理论值 104%
        0.15f  // 第四刀：每个爆点为当前剑击的 15%，整组理论值 105%
    };
    public static float QuickdrawExtraLayerDamageMultiplier = 0.15f; // 重复层 B：每层只增加“基础爆点伤害”的 15%
    public static float QuickdrawFullLoadoutLifetimeMultiplier = 1.70f; // 全选：原地聚合爆点的视觉持续时间乘 1.70

    // ---------------------------------------------------------------------
    // 战术八：雷神之锤（机械三王后解锁）
    // 以完整四段连招轮数限制触发频率；倍率分为锤体直接命中与随后爆炸两段。
    // ---------------------------------------------------------------------
    public static int ThorsHammerRequiredComboRounds = 2;                 // 完成第 2 轮四段连招时召唤雷神之锤
    public static float ThorsHammerDropDamageMultiplier = 1.18f;          // 锤体直接命中为第四刀伤害的 118%
    public static float ThorsHammerExtraLayerDamageMultiplier = 0.22f;    // 重复层 B：每层只增加“基础锤体伤害”的 22%
    public static float ThorsHammerExplosionDamageMultiplier = 0.88f;     // 爆炸为锤体实际伤害的 88%
    public static float ThorsHammerBaseScreenShakePower = 13f;            // 第一层雷神之锤爆炸的基础屏幕震动等级
}
