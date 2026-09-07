using System;
using System.Collections.Generic;
using System.IO;
using FranMod.Content.UI;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FranMod.Content.Items;

public sealed class FranDamageDebugger : ModItem
{
    public const int MaximumPanelDamage = 99_999_999;
    private int panelDamage = 105;

    public int PanelDamage => Math.Clamp(panelDamage, 1, MaximumPanelDamage);

    public override string Texture => $"Terraria/Images/Item_{ItemID.MulticolorWrench}";

    public override void SetDefaults()
    {
        Item.width = 30;
        Item.height = 30;
        Item.maxStack = 1;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = 12;
        Item.useAnimation = 12;
        Item.noMelee = true;
        Item.autoReuse = false;
        Item.UseSound = null;
        Item.value = 0;
        Item.rare = ItemRarityID.Red;
    }

    public override void OnCreated(ItemCreationContext context) =>
        panelDamage = FranBalance.GetProgressionBaseDamage();

    public override ModItem Clone(Item item)
    {
        FranDamageDebugger clone = (FranDamageDebugger)base.Clone(item);
        clone.panelDamage = PanelDamage;
        return clone;
    }

    public override bool CanResearch() => false;

    public override bool? UseItem(Player player)
    {
        if (Main.myPlayer == player.whoAmI)
        {
            bool opened = FranDamageDebugUISystem.Toggle(Item);
            SoundEngine.PlaySound(opened
                ? SoundID.MenuOpen with { Volume = 0.65f }
                : SoundID.MenuClose with { Volume = 0.58f }, player.Center);
        }
        return true;
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        tooltips.Add(new TooltipLine(Mod, "StoredDamage",
            Language.GetTextValue("Mods.Fran.DamageDebug.StoredDamage", PanelDamage))
        { OverrideColor = new Color(132, 218, 255) });
        tooltips.Add(new TooltipLine(Mod, "OverrideState",
            Language.GetTextValue(Item.favorited
                ? "Mods.Fran.DamageDebug.Enabled"
                : "Mods.Fran.DamageDebug.Disabled"))
        { OverrideColor = Item.favorited ? new Color(126, 238, 172) : new Color(186, 190, 202) });
    }

    internal void SetPanelDamage(int value) => panelDamage = Math.Clamp(value, 1, MaximumPanelDamage);

    internal static bool TryGetFavoritedDamage(Player player, out int damage)
    {
        damage = 0;
        if (player is null)
            return false;

        for (int slot = 0; slot < 50; slot++)
        {
            Item item = player.inventory[slot];
            if (!item.IsAir && item.favorited && item.ModItem is FranDamageDebugger debugger)
            {
                damage = debugger.PanelDamage;
                return true;
            }
        }
        return false;
    }

    public override void SaveData(TagCompound tag) => tag["PanelDamage"] = PanelDamage;

    public override void LoadData(TagCompound tag) =>
        SetPanelDamage(tag.ContainsKey("PanelDamage") ? tag.GetInt("PanelDamage") : 105);

    public override void NetSend(BinaryWriter writer) => writer.Write(PanelDamage);

    public override void NetReceive(BinaryReader reader) => SetPanelDamage(reader.ReadInt32());
}
