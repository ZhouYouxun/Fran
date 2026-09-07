using FranMod.Content.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Items;

public sealed class TeacherArrivalDebugger : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.ThunderStaff}";

    public override void SetDefaults()
    {
        Item.width = 40;
        Item.height = 40;
        Item.maxStack = 1;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = Item.useAnimation = 30;
        Item.noMelee = true;
        Item.autoReuse = false;
        Item.consumable = false;
        Item.value = 0;
        Item.rare = ItemRarityID.Red;
    }

    public override bool? UseItem(Player player)
    {
        // UseItem also runs on remote peers. Only the user submits one request per click.
        if (player.whoAmI == Main.myPlayer)
            TeacherArrivalSystem.RequestDebugArrival(player, Main.MouseWorld);
        return true;
    }

    public override bool CanResearch() => false;
}
