using FranMod.Content.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Items;

public sealed class StrategyGuideBook : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.Book}";

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 20;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = 18;
        Item.useAnimation = 18;
        Item.noMelee = true;
        Item.autoReuse = false;
        Item.UseSound = null;
        Item.value = Item.buyPrice(silver: 20);
        Item.rare = ItemRarityID.Blue;
        Item.ResearchUnlockCount = 1;
    }

    public override bool? UseItem(Player player)
    {
        if (Main.myPlayer == player.whoAmI)
        {
            bool opened = FranStrategyGuideUISystem.Toggle();
            SoundEngine.PlaySound(opened
                ? SoundID.MenuOpen with { Volume = 0.65f, Pitch = 0.08f }
                : SoundID.MenuClose with { Volume = 0.58f, Pitch = 0.04f }, player.Center);
        }
        return true;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 10)
            .AddIngredient(ItemID.Silk, 2)
            .AddTile(TileID.WorkBenches)
            .Register();
    }

    public override Color? GetAlpha(Color lightColor) => Color.White;
}
