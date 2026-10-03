using FranMod.Content.Projectiles.BlackHole;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Systems.BlackHole;

public sealed class SingularityLimitSystem : ModSystem
{
    public override void PostUpdateProjectiles()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;
        // Resolve a late standalone-weapon spawn packet without covering an active ultimate.
        if (FranSingularityUltimate.FindAny() is not null)
        {
            foreach (Projectile projectile in Main.ActiveProjectiles)
                if (projectile.ModProjectile is SustainedBlackHole) projectile.Kill();
            return;
        }
        Projectile selected = SustainedBlackHole.FindSoleBlackHole();
        if (selected is null)
            return;
        foreach (Projectile candidate in Main.ActiveProjectiles)
        {
            if (candidate.type == selected.type && candidate != selected)
                candidate.Kill();
        }
    }
}
