using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Items;

public sealed class FransHeadwear : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.CatEars}";

    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 20;
        Item.value = Item.buyPrice(gold: 1);
        Item.rare = ItemRarityID.Blue;
        Item.maxStack = 1;
    }
}

public sealed class FranZoologistShop : GlobalNPC
{
    public override void ModifyShop(NPCShop shop)
    {
        if (shop.NpcType == NPCID.BestiaryGirl)
            shop.Add<FransHeadwear>();
    }
}
