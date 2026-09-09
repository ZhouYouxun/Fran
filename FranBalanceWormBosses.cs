using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace FranMod.Core;

public static partial class FranBalance
{
    // aiStyle == Worm 无法覆盖灾厄自定义 AI；realLife 也不是蠕虫专用标记。
    // 按命中的节段类型判断，不依赖 npc.boss、头部存活或 realLife，兼容世吞分裂。
    // 灾厄使用内部名称软兼容；未加载灾厄时无需引用其程序集。
    private static readonly HashSet<string> CalamityWormBossSegments = new(StringComparer.Ordinal)
    {
        // 荒漠灾虫及 Boss 战附属的小荒漠灾虫。
        "DesertScourgeHead", "DesertScourgeBody", "DesertScourgeTail",
        "DesertNuisanceHead", "DesertNuisanceBody", "DesertNuisanceTail",
        "DesertNuisanceHeadYoung", "DesertNuisanceBodyYoung", "DesertNuisanceTailYoung",
        // 血肉宿主召唤的三种血肉之虫；不包含非蠕虫的宿主与血肉囊。
        "PerforatorHeadSmall", "PerforatorBodySmall", "PerforatorTailSmall",
        "PerforatorHeadMedium", "PerforatorBodyMedium", "PerforatorTailMedium",
        "PerforatorHeadLarge", "PerforatorBodyLarge", "PerforatorTailLarge",
        // 渊海灾虫、星神游龙、风暴编织者。
        "AquaticScourgeHead", "AquaticScourgeBody", "AquaticScourgeBodyAlt", "AquaticScourgeTail",
        "AstrumDeusHead", "AstrumDeusBody", "AstrumDeusTail",
        "StormWeaverHead", "StormWeaverBody", "StormWeaverTail",
        // 神明吞噬者、塔纳托斯、始源妖龙。
        "DevourerofGodsHead", "DevourerofGodsBody", "DevourerofGodsTail",
        "ThanatosHead", "ThanatosBody1", "ThanatosBody2", "ThanatosTail",
        "PrimordialWyrmHead", "PrimordialWyrmBody", "PrimordialWyrmBodyAlt", "PrimordialWyrmTail",
        // 至尊灾厄召唤的 Sepulcher；只处理蠕虫节段。
        "SepulcherHead", "SepulcherBody", "SepulcherBodyEnergyBall", "SepulcherTail"
    };

    public static bool IsWormBossSegment(NPC target)
    {
        if (target.type is NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail
            or NPCID.TheDestroyer or NPCID.TheDestroyerBody or NPCID.TheDestroyerTail)
            return true;

        return target.ModNPC?.Mod.Name == "CalamityMod" &&
            CalamityWormBossSegments.Contains(target.ModNPC.Name);
    }
}
