using FranMod.Content.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace FranMod.Content.Projectiles;

internal static class FranHammerTargeting
{
    // Called by the owning client at release. Companion actors use their own combat aim.
    public static NPC Select(Player owner)
    {
        Vector2 aim = FranCompanionContext.Aim(owner);
        NPC boss = null;
        NPC nearest = null;
        float bossDistance = float.MaxValue;
        float nearestDistance = float.MaxValue;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!CanTrack(npc) || !InSelectionView(owner, npc))
                continue;
            float distance = Vector2.DistanceSquared(aim, npc.Center);
            if (IsBoss(npc) && distance < bossDistance)
            {
                boss = npc;
                bossDistance = distance;
            }
            if (distance < nearestDistance)
            {
                nearest = npc;
                nearestDistance = distance;
            }
        }
        return boss ?? nearest;
    }

    // Cursor movement after launch must not redirect a hammer away from its selected enemy.
    public static NPC Resolve(Player owner, int currentIndex) => GetLockedTarget(currentIndex) ?? Select(owner);

    public static NPC GetLockedTarget(int index) =>
        index >= 0 && index < Main.npc.Length && CanTrack(Main.npc[index]) ? Main.npc[index] : null;

    private static bool InSelectionView(Player owner, NPC npc)
    {
        // Only the player's owning client has the relevant camera. Companion aim remains autonomous.
        if (Main.dedServ || FranCompanionContext.IsActor(owner))
            return true;
        Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
        Vector2 center = Main.screenPosition + screenSize * 0.5f;
        Vector2 halfSize = screenSize * 0.5f / Main.GameViewMatrix.Zoom;
        return npc.Right.X >= center.X - halfSize.X && npc.Left.X <= center.X + halfSize.X &&
            npc.Bottom.Y >= center.Y - halfSize.Y && npc.Top.Y <= center.Y + halfSize.Y;
    }

    private static bool CanTrack(NPC npc) => npc is not null && npc.active && !npc.friendly && npc.life > 0 &&
        !npc.dontTakeDamage && !npc.immortal && npc.CanBeChasedBy();

    private static bool IsBoss(NPC npc) => npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || HasBossRoot(npc);

    // Worm body/tail sections do not necessarily carry their head's boss flag.
    private static bool HasBossRoot(NPC npc) => npc.realLife >= 0 && npc.realLife < Main.npc.Length &&
        Main.npc[npc.realLife] is NPC root && root.active && root.boss;
}
