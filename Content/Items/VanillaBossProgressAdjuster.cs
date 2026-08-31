using FranMod.Content.UI;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Items;

public sealed class VanillaBossProgressAdjuster : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.SuspiciousLookingEye}";

    public override void SetDefaults()
    {
        Item.width = 28;
        Item.height = 28;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = 12;
        Item.useAnimation = 12;
        Item.noMelee = true;
        Item.autoReuse = false;
        Item.UseSound = null;
        Item.value = 0;
        Item.rare = ItemRarityID.Cyan;
        Item.ResearchUnlockCount = 1;
    }

    public override bool CanUseItem(Player player) => true;

    public override bool? UseItem(Player player)
    {
        if (Main.myPlayer == player.whoAmI)
        {
            bool opened = FranBossProgressUISystem.Toggle();
            SoundEngine.PlaySound(opened
                ? SoundID.MenuOpen with { Volume = 0.65f, Pitch = 0.08f }
                : SoundID.MenuClose with { Volume = 0.58f, Pitch = 0.04f }, player.Center);
        }
        return true;
    }

    public override Color? GetAlpha(Color lightColor) => Color.White;
}
