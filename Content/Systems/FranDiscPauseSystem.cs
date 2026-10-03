using FranMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Systems;

// Projectile AI stops during autopause. The UI hook records that pause so the first
// resumed simulation frame cannot mistake UI-cleared controls for releasing RMB.
public sealed class FranDiscPauseSystem : ModSystem
{
    public override void UpdateUI(GameTime gameTime)
    {
        if (Main.gameMenu || Main.netMode != NetmodeID.SinglePlayer || !Main.gamePaused) return;
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.owner == Main.myPlayer && projectile.ModProjectile is FranSpinningDisc disc)
                disc.NotifyPaused();
    }
}
