using FranMod.Content.Projectiles.BlackHole;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Items;

public sealed class SingularityFocus : ModItem
{
    // Keep the prototype source available without shipping its test weapon in the first release.
    public override bool IsLoadingEnabled(Mod mod) => false;

    public override string Texture => $"Terraria/Images/Item_{ItemID.LastPrism}";

    public override void SetDefaults()
    {
        Item.width = Item.height = 32;
        Item.damage = 90;
        Item.DamageType = DamageClass.Magic;
        Item.knockBack = 0f;
        Item.mana = 4;
        Item.useTime = Item.useAnimation = 30;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.channel = true;
        Item.noMelee = true;
        Item.shoot = ModContent.ProjectileType<SustainedBlackHole>();
        Item.shootSpeed = 0f;
        Item.rare = ItemRarityID.Pink;
        Item.value = Item.buyPrice(gold: 5);
        Item.UseSound = SoundID.Item8;
    }

    public override bool CanUseItem(Player player) => SustainedBlackHole.FindSoleBlackHole() is null &&
        FranSingularityUltimate.FindAny() is null;

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
        Vector2 velocity, int type, int damage, float knockback)
    {
        if (player.whoAmI == Main.myPlayer && SustainedBlackHole.FindSoleBlackHole() is null &&
            FranSingularityUltimate.FindAny() is null)
            Projectile.NewProjectile(source, SustainedBlackHole.ClampDestination(player, Main.MouseWorld),
                Vector2.Zero, type, damage, knockback, player.whoAmI);
        return false;
    }

    public override void AddRecipes() => CreateRecipe()
        .AddIngredient(ItemID.FallenStar, 10)
        .AddIngredient(ItemID.MeteoriteBar, 8)
        .AddIngredient(ItemID.Diamond)
        .AddTile(TileID.WorkBenches)
        .Register();
}
