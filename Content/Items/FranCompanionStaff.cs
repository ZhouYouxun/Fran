using FranMod.Content.Companions;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Items;

public sealed class FranCompanionStaff : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.PygmyStaff}";
    public override void SetDefaults()
    {
        Item.width = Item.height = 40;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = Item.useAnimation = 30;
        Item.noMelee = true;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.buyPrice(silver: 50);
        Item.UseSound = SoundID.Item44;
    }
    public override bool AltFunctionUse(Player player) => true;
    public override bool? UseItem(Player player)
    {
        if (player.whoAmI == Main.myPlayer)
            FranCompanionSystem.Request(player.altFunctionUse == 2
                ? FranCompanionSystem.Command.Dismiss : FranCompanionSystem.Command.Summon);
        return true;
    }
    public override void AddRecipes() => CreateRecipe()
        .AddIngredient(ItemID.FallenStar, 3)
        .AddIngredient(ItemID.Silk, 5)
        .AddRecipeGroup(RecipeGroupID.IronBar, 5)
        .AddTile(TileID.WorkBenches)
        .Register();
}
