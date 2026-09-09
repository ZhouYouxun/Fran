using FranMod.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.NPCs;

public sealed class FranTeacherShop : GlobalNPC
{
    public override void ModifyShop(NPCShop shop)
    {
        if (shop.NpcType == NPCID.Merchant)
            shop.Add(new Item(ModContent.ItemType<Teacher>()) { shopCustomPrice = Item.buyPrice(gold: 20) });
    }
}
