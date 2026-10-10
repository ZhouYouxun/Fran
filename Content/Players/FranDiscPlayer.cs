using FranMod.Content.Items;
using FranMod.Content.Projectiles;
using FranMod.Content.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace FranMod.Content.Players;

// Releasing near the hand must not shorten the attack beat or allow a second template
// to deal another hit immediately. This clock survives the disc projectile's recall.
public sealed class FranDiscPlayer : ModPlayer
{
    internal ulong AttackRecoveryUntil;
    internal bool Recovering => Main.GameUpdateCount < AttackRecoveryUntil;
    public override void Initialize() => AttackRecoveryUntil = 0;
    public override void UpdateDead() => AttackRecoveryUntil = 0;

    public override void PreUpdate()
    {
        if (Player.whoAmI != Main.myPlayer || Main.gamePaused || Main.playerInventory ||
            Player.dead || Player.CCed || Player.noItems || Player.mouseInterface ||
            FranUISystem.IsWheelOpen || Content.Companions.FranCompanionUI.IsOpen ||
            Content.Companions.FranCompanionUI.Hovering ||
            Player.HeldItem.ModItem is not ShiFu || !PlayerInput.Triggers.Current.MouseRight ||
            Player.GetModPlayer<FranPlayer>().UltimateActive ||
            Player.ownedProjectileCounts[ModContent.ProjectileType<FranSpinningDisc>()] > 0) return;

        bool interrupted = false;
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner != Player.whoAmI ||
                projectile.ModProjectile is not (ShiFuSwingRebuild or FranQuickdrawDiscHoldout)) continue;
            projectile.Kill();
            interrupted = true;
        }
        if (!interrupted) return;

        // Item reuse waits for itemAnimation. Directly replace the current held attack so
        // pressing RMB takes effect this frame even while LMB remains held.
        Item item = Player.HeldItem;
        Vector2 aim = (Main.MouseWorld - Player.MountedCenter).SafeNormalize(Vector2.UnitX * Player.direction);
        ShiFu.SpawnDisc(Player, Player.GetSource_ItemUse(item), aim, Player.GetWeaponDamage(item),
            Player.GetWeaponKnockback(item, item.knockBack));
        Player.altFunctionUse = 2;
        Player.itemTime = Player.itemAnimation = 2;
    }
}
