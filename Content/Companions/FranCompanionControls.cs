using Terraria;
using Terraria.ModLoader;

namespace FranMod.Content.Companions;

public sealed class FranCompanionControls : ModPlayer
{
    public override void SetControls()
    {
        if (Player.whoAmI != Main.myPlayer || !FranCompanionUI.IsOpen && !FranCompanionUI.Hovering)
            return;
        Player.controlUseTile = false;
        if (FranCompanionUI.IsOpen || Main.mouseRight)
            Player.controlUseItem = false;
    }
}
