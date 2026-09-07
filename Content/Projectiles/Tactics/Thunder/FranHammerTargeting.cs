using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace FranMod.Content.Projectiles;

internal static class FranHammerTargeting
{
    // All distances are measured from the player, with no cursor-based search radius.
    public static NPC Select(Player owner, IReadOnlySet<int> hitTargets = null)
    {
        NPC boss = null;
        NPC hit = null;
        NPC nearest = null;
        float bossDistance = float.MaxValue;
        float hitDistance = float.MaxValue;
        float nearestDistance = float.MaxValue;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!CanTrack(npc))
                continue;
            float distance = Vector2.DistanceSquared(owner.Center, npc.Center);
            if (IsBoss(npc) && distance < bossDistance)
            {
                boss = npc;
                bossDistance = distance;
            }
            if (hitTargets?.Contains(npc.whoAmI) == true && distance < hitDistance)
            {
                hit = npc;
                hitDistance = distance;
            }
            if (distance < nearestDistance)
            {
                nearest = npc;
                nearestDistance = distance;
            }
        }
        return boss ?? hit ?? nearest;
    }

    // Keep the selected normal enemy; a live boss takes precedence and a lost target is replaced.
    public static NPC Resolve(Player owner, int currentIndex)
    {
        NPC preferred = Select(owner);
        if (preferred is not null && IsBoss(preferred))
            return preferred;
        return GetLockedTarget(currentIndex) ?? preferred;
    }

    public static NPC GetLockedTarget(int index) =>
        index >= 0 && index < Main.npc.Length && CanTrack(Main.npc[index]) ? Main.npc[index] : null;

    private static bool CanTrack(NPC npc) => npc is not null && npc.active && !npc.friendly && npc.life > 0 &&
        (npc.CanBeChasedBy() || npc.boss || HasBossRoot(npc));

    private static bool IsBoss(NPC npc) => npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || HasBossRoot(npc);

    // Worm body/tail sections do not necessarily carry their head's boss flag.
    private static bool HasBossRoot(NPC npc) => npc.realLife >= 0 && npc.realLife < Main.npc.Length &&
        Main.npc[npc.realLife] is NPC root && root.active && root.boss;
}
