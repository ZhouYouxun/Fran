using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Core;

/// <summary>
/// 月亮领主后的可选平衡来源。
/// 同时加载多个受支持的大型模组时只允许一个来源生效；灾厄拥有最高优先级。
/// </summary>
public enum FranOptionalPostMoonLordBalanceSource
{
    None,
    Calamity
}

/// <summary>灾厄月后伤害成长节点。这里只记录数值成长，不负责解锁战术。</summary>
public enum FranCalamityProgressionMilestone
{
    MoonLord,
    Providence,
    AnySentinel,
    AllSentinels,
    Polterghast,
    DevourerOfGods,
    Yharon,
    ExoMechsOrSupremeCalamitas,
    ExoMechsAndSupremeCalamitas
}

public static partial class FranBalance
{
    // 这里的数值全部是“比例”，不是最终攻击力。
    // 月亮领主后的原版攻击力和冲刺伤害都作为 170 的锚点；后续数值按
    // 月总锚点数值 / 170 × 当前比例 计算，并四舍五入为整数。
    public const int CalamityMoonLordRatio = 170;

    public static object[,] CalamityPostMoonLordProgression =
    {
        // 灾厄进度节点                                              比例
        { FranCalamityProgressionMilestone.MoonLord,                         170 }, // 月后初期
        { FranCalamityProgressionMilestone.Providence,                       200 }, // 击败亵渎天神 Providence
        { FranCalamityProgressionMilestone.AnySentinel,                      240 }, // 击败任意一个神之使徒
        { FranCalamityProgressionMilestone.AllSentinels,                     290 }, // 风暴编织者、无尽虚空、希格纳斯全部击败
        { FranCalamityProgressionMilestone.Polterghast,                      320 }, // 击败噬魂幽花 Polterghast
        { FranCalamityProgressionMilestone.DevourerOfGods,                   380 }, // 击败神明吞噬者 Devourer of Gods
        { FranCalamityProgressionMilestone.Yharon,                           480 }, // 击败丛林龙 Yharon
        { FranCalamityProgressionMilestone.ExoMechsOrSupremeCalamitas,       520 }, // Exo 机械或至尊灾厄，任意一个完成
        { FranCalamityProgressionMilestone.ExoMechsAndSupremeCalamitas,      750 }  // Exo 机械和至尊灾厄，二者全部完成
    };

    private const string CalamityModInternalName = "CalamityMod";
    private const string CalamityDownedBossSystemTypeName = "CalamityMod.DownedBossSystem";

    private static Mod cachedCalamityMod;
    private static Type cachedCalamityDownedBossSystemType;
    private static readonly Dictionary<string, PropertyInfo> CalamityDownedProperties = new(StringComparer.Ordinal);
    private static ulong cachedCalamityRatioUpdate = ulong.MaxValue;
    private static bool cachedCalamityRatioAvailable;
    private static int cachedCalamityRatio = CalamityMoonLordRatio;

    /// <summary>
    /// 选择唯一的月后大型模组平衡来源。未来加入 HJ 等兼容时，应继续把 Calamity
    /// 放在最前面；两者同时开启时直接使用 Calamity，不叠加两套比例。
    /// </summary>
    public static FranOptionalPostMoonLordBalanceSource GetOptionalPostMoonLordBalanceSource()
    {
        if (ModLoader.TryGetMod(CalamityModInternalName, out _))
            return FranOptionalPostMoonLordBalanceSource.Calamity;

        return FranOptionalPostMoonLordBalanceSource.None;
    }

    private static void ApplyOptionalPostMoonLordProgression(ref int baseDamage, ref int dashDamage)
    {
        if (!NPC.downedMoonlord || !TryGetOptionalPostMoonLordRatio(out int ratio))
            return;

        baseDamage = ScalePostMoonLordValue(baseDamage, ratio);
        dashDamage = ScalePostMoonLordValue(dashDamage, ratio);
    }

    private static bool TryGetOptionalPostMoonLordRatio(out int ratio)
    {
        ratio = CalamityMoonLordRatio;
        return GetOptionalPostMoonLordBalanceSource() switch
        {
            FranOptionalPostMoonLordBalanceSource.Calamity => TryGetCalamityPostMoonLordRatio(out ratio),
            _ => false
        };
    }

    public static bool TryGetCalamityPostMoonLordRatio(out int ratio)
    {
        ratio = CalamityMoonLordRatio;
        if (!NPC.downedMoonlord || !TryGetCalamityDownedBossSystemType(out _))
            return false;

        // 基础攻击力在绘制 UI、生成弹幕和结算命中时都可能被重复读取。
        // 每个游戏更新帧只反射一次灾厄进度，避免把可选兼容变成战斗热路径负担。
        if (cachedCalamityRatioUpdate == Main.GameUpdateCount)
        {
            ratio = cachedCalamityRatio;
            return cachedCalamityRatioAvailable;
        }

        CalamityProgressSnapshot progress = ReadCalamityProgressSnapshot();
        for (int row = 0; row < CalamityPostMoonLordProgression.GetLength(0); row++)
        {
            if (CalamityPostMoonLordProgression[row, 0] is not FranCalamityProgressionMilestone milestone ||
                CalamityPostMoonLordProgression[row, 1] is not int candidateRatio ||
                !IsCalamityMilestoneCompleted(milestone, progress))
                continue;

            // 使用进度表中最后一个已经完成的节点，保留表内明确写出的数值，
            // 不对用户设置的比例排序、平滑或自动取最大值。
            ratio = Math.Max(1, candidateRatio);
        }

        cachedCalamityRatioUpdate = Main.GameUpdateCount;
        cachedCalamityRatio = ratio;
        cachedCalamityRatioAvailable = true;
        return true;
    }

    private static int ScalePostMoonLordValue(int moonLordValue, int ratio) =>
        Math.Max(1, (int)Math.Round(
            moonLordValue * ratio / (double)CalamityMoonLordRatio,
            MidpointRounding.AwayFromZero));

    private static CalamityProgressSnapshot ReadCalamityProgressSnapshot() => new(
        GetCalamityDownedFlag("downedProvidence"),
        GetCalamityDownedFlag("downedCeaselessVoid"),
        GetCalamityDownedFlag("downedStormWeaver"),
        GetCalamityDownedFlag("downedSignus"),
        GetCalamityDownedFlag("downedPolterghast"),
        GetCalamityDownedFlag("downedDoG"),
        GetCalamityDownedFlag("downedYharon"),
        GetCalamityDownedFlag("downedExoMechs"),
        GetCalamityDownedFlag("downedCalamitas"));

    private static bool IsCalamityMilestoneCompleted(
        FranCalamityProgressionMilestone milestone,
        CalamityProgressSnapshot progress)
    {
        return milestone switch
        {
            FranCalamityProgressionMilestone.MoonLord => NPC.downedMoonlord,
            FranCalamityProgressionMilestone.Providence => progress.Providence,
            FranCalamityProgressionMilestone.AnySentinel =>
                progress.CeaselessVoid || progress.StormWeaver || progress.Signus,
            FranCalamityProgressionMilestone.AllSentinels =>
                progress.CeaselessVoid && progress.StormWeaver && progress.Signus,
            FranCalamityProgressionMilestone.Polterghast => progress.Polterghast,
            FranCalamityProgressionMilestone.DevourerOfGods => progress.DevourerOfGods,
            FranCalamityProgressionMilestone.Yharon => progress.Yharon,
            FranCalamityProgressionMilestone.ExoMechsOrSupremeCalamitas =>
                progress.ExoMechs || progress.SupremeCalamitas,
            FranCalamityProgressionMilestone.ExoMechsAndSupremeCalamitas =>
                progress.ExoMechs && progress.SupremeCalamitas,
            _ => false
        };
    }

    private readonly struct CalamityProgressSnapshot
    {
        public readonly bool Providence;
        public readonly bool CeaselessVoid;
        public readonly bool StormWeaver;
        public readonly bool Signus;
        public readonly bool Polterghast;
        public readonly bool DevourerOfGods;
        public readonly bool Yharon;
        public readonly bool ExoMechs;
        public readonly bool SupremeCalamitas;

        public CalamityProgressSnapshot(bool providence, bool ceaselessVoid, bool stormWeaver, bool signus,
            bool polterghast, bool devourerOfGods, bool yharon, bool exoMechs, bool supremeCalamitas)
        {
            Providence = providence;
            CeaselessVoid = ceaselessVoid;
            StormWeaver = stormWeaver;
            Signus = signus;
            Polterghast = polterghast;
            DevourerOfGods = devourerOfGods;
            Yharon = yharon;
            ExoMechs = exoMechs;
            SupremeCalamitas = supremeCalamitas;
        }
    }

    private static bool GetCalamityDownedFlag(string propertyName)
    {
        if (!TryGetCalamityDownedBossSystemType(out Type type))
            return false;

        if (!CalamityDownedProperties.TryGetValue(propertyName, out PropertyInfo property))
        {
            property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            CalamityDownedProperties[propertyName] = property;
        }

        try
        {
            return property?.GetValue(null) is true;
        }
        catch
        {
            // 灾厄内部结构变化时宁可停用可选成长，也不能阻止 Fran 或世界加载。
            return false;
        }
    }

    private static bool TryGetCalamityDownedBossSystemType(out Type type)
    {
        type = null;
        if (!ModLoader.TryGetMod(CalamityModInternalName, out Mod calamityMod))
        {
            cachedCalamityMod = null;
            cachedCalamityDownedBossSystemType = null;
            CalamityDownedProperties.Clear();
            cachedCalamityRatioUpdate = ulong.MaxValue;
            cachedCalamityRatioAvailable = false;
            cachedCalamityRatio = CalamityMoonLordRatio;
            return false;
        }

        if (!ReferenceEquals(cachedCalamityMod, calamityMod))
        {
            cachedCalamityMod = calamityMod;
            cachedCalamityDownedBossSystemType = calamityMod.GetType().Assembly.GetType(
                CalamityDownedBossSystemTypeName,
                throwOnError: false);
            CalamityDownedProperties.Clear();
            cachedCalamityRatioUpdate = ulong.MaxValue;
            cachedCalamityRatioAvailable = false;
            cachedCalamityRatio = CalamityMoonLordRatio;
        }

        type = cachedCalamityDownedBossSystemType;
        return type is not null;
    }
}
