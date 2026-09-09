using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Items;

public sealed class Foreword : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.Keybrand}";

    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;
        Item.maxStack = 1;
        Item.noMelee = true;
        Item.value = 0;
        Item.rare = ItemRarityID.White;
        Item.ResearchUnlockCount = 1;
    }
}
