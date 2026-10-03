namespace FranMod.Core;

/// <summary>
/// 弗兰平衡驾驶舱：只保留世界进度、战术解锁、槽位和冲刺成长。
/// 战术的内部数值在 FranBalanceTacticsInternal.cs。
/// </summary>
public static partial class FranBalance
{
    // 1：核心大剑进度（世界节点、核心攻击力、核心大小、冲刺面板伤害、转盘倍率、转盘距离限制）
    // 核心攻击力按流程只升不降；读取时也会以前面各节点的最高值保底。
    public static object[,] CoreProgression =
    {
        // 转盘距离单位：像素；-1 表示无上限。
        // 世界 Boss / 条件                                      攻击力    大小      冲刺伤害    转盘伤害倍率    转盘距离限制
        {  FranProgressionMilestone.Initial,                     23,       0.80f,    16,         0.45f,          240             }, // 开局
        {  FranProgressionMilestone.EyeOfCthulhu,                23,       0.85f,    16,         0.45f,          280             }, // 克苏鲁之眼
        {  FranProgressionMilestone.AnyEvilBoss,                 26,       0.90f,    16,         0.45f,          320             }, // 邪恶 Boss
        {  FranProgressionMilestone.Skeletron,                   55,       0.95f,    16,         0.45f,          380             }, // 骷髅王
        {  FranProgressionMilestone.WallOfFlesh,                 70,       1.00f,    74,         0.45f,          440             }, // 困难模式初期
        {  FranProgressionMilestone.AnyMechanicalBoss,           75,       1.05f,    74,         0.45f,          500             }, // 任意机械 Boss
        {  FranProgressionMilestone.AnyTwoMechanicalBosses,      80,       1.10f,    74,         0.45f,          560             }, // 任意两只机械 Boss
        {  FranProgressionMilestone.AllMechanicalBosses,         91,       1.15f,    74,         0.45f,          620             }, // 机械三王
        {  FranProgressionMilestone.Plantera,                    130,      1.20f,    191,        0.45f,          720             }, // 世纪之花
        {  FranProgressionMilestone.Golem,                       187,      1.25f,    191,        0.45f,          -1              }, // 石巨人
        {  FranProgressionMilestone.LunaticCultist,              203,      1.30f,    191,        0.45f,          -1              }, // 邪教徒
        {  FranProgressionMilestone.MoonLord,                    276,      1.40f,    428,        0.45f,          -1              }  // 月亮领主
    };

    // 2：八种战术的解锁条件（战术、世界节点）
    public static object[,] TacticUnlockProgression =
    {
        { FranSkill.TelekineticCatapult,   FranProgressionMilestone.Initial },             // 念动弹射
        { FranSkill.SkillTaker,            FranProgressionMilestone.Initial },             // 技能夺取者
        { FranSkill.MagicBarrier,          FranProgressionMilestone.EyeOfCthulhu },        // 魔力屏障
        { FranSkill.MultipleBodyCreation,  FranProgressionMilestone.Skeletron },           // 多重分体
        { FranSkill.BlueResonance,         FranProgressionMilestone.WallOfFlesh },         // 蓝辉共鸣
        { FranSkill.ThunderChain,          FranProgressionMilestone.AnyMechanicalBoss },   // 雷链
        { FranSkill.PressurizedQuickdraw,  FranProgressionMilestone.Plantera },            // 压缩居合
        { FranSkill.ThorsHammer,           FranProgressionMilestone.Golem }                // 雷神之锤
    };

    // 3：战术槽位成长（世界节点、可用槽位）
    public static object[,] TacticSlotProgression =
    {
        { FranProgressionMilestone.Initial,       2 }, // 开局可装备两个战术槽
        { FranProgressionMilestone.WallOfFlesh,   3 }, // 血肉墙后开放第三个战术槽
        { FranProgressionMilestone.MoonLord,      4 }  // 月亮领主后开放第四个战术槽
    };

    // 4：冲刺速度成长（世界节点、相对当前完整冲刺速度的倍率）
    // 基础为 70%，困难模式为 85%，击败世纪之花后恢复为 100%。
    public static object[,] DashSpeedProgression =
    {
        { FranProgressionMilestone.Initial,      0.70f },
        { FranProgressionMilestone.WallOfFlesh,  0.85f },
        { FranProgressionMilestone.Plantera,     1.00f }
    };

}
