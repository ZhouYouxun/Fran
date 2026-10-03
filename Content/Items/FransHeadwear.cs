using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Items;

public sealed class FransHeadwear : ModItem
{
    public override string Texture => "Fran/Textures/弗兰头饰";

    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 20;
        Item.rare = ItemRarityID.Gray;
        Item.value = 0;
        Item.maxStack = 1;
        Item.accessory = false;
    }
}

public sealed class FranZoologistShop : GlobalNPC
{
    public override void ModifyShop(NPCShop shop)
    {
        // Fran mode is now tied to carrying ShiFu; keep the retired headwear out of shops.
    }
}
