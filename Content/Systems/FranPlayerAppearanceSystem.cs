using FranMod.Content.Players;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Content.Systems;

public sealed class FranPlayerAppearanceSystem : ModSystem
{
    public override void Load()
    {
        if (!Main.dedServ)
            On_Player.PlayerFrame += RestorePausedAppearance;
    }

    public override void Unload()
    {
        if (!Main.dedServ)
            On_Player.PlayerFrame -= RestorePausedAppearance;
    }

    private static void RestorePausedAppearance(On_Player.orig_PlayerFrame orig, Player player)
    {
        orig(player);

        // PlayerFrame rebuilds equipment slots, then returns before ModPlayer.FrameEffects
        // during autopause. Restore only our visual slots before PlayerDrawSet caches its
        // skin visibility, composite clothing and hair settings from those slots.
        if (Main.gamePaused && !Main.gameMenu)
            player.GetModPlayer<FranPlayer>().ApplyFranAppearance();
    }
}
