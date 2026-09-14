using System;
using System.Collections.Generic;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace FranMod.Content.Companions;

public sealed class FranCompanionUI : ModSystem
{
    private static int openId;
    private static bool previousRight, previousLeft;
    private static Rectangle panel;
    internal static bool IsOpen => openId != 0;
    internal static bool Hovering { get; private set; }
    internal static void Close() => openId = 0;
    private static string Text(string key, params object[] args) =>
        Language.GetTextValue("Mods.Fran.Companion." + key, args);

    // Run before players use items, so right-clicking Fran cannot also activate a weapon/tile.
    public override void PreUpdatePlayers()
    {
        if (Main.dedServ || Main.gameMenu) return;
        UpdateInput();
    }

    public override void UpdateUI(GameTime gameTime)
    {
        // Autopause stops world updates but the panel must remain usable and closable.
        if (Main.gamePaused && !Main.gameMenu) UpdateInput();
    }

    private static void UpdateInput()
    {
        bool right = Main.mouseRight && !previousRight;
        bool left = Main.mouseLeft && !previousLeft;
        previousRight = Main.mouseRight;
        previousLeft = Main.mouseLeft;
        Player player = Main.LocalPlayer;
        if (!player.active || player.dead || Main.playerInventory)
        {
            Close(); Hovering = false; return;
        }
        FranCompanion own = FranCompanionSystem.FindOwned(player.whoAmI);
        Hovering = own is not null && own.CanInteract(player.whoAmI) &&
            new Rectangle((int)own.Projectile.position.X - 12, (int)own.Projectile.position.Y - 10,
                own.Projectile.width + 24, own.Projectile.height + 20).Contains(Main.MouseWorld.ToPoint());
        if (IsOpen && (own is null || own.SummonId != openId || !own.CanInteract(player.whoAmI) || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)))
            Close();
        if (right && (Hovering || IsOpen))
        {
            openId = IsOpen ? 0 : own.SummonId;
            player.controlUseTile = player.controlUseItem = false;
            Main.mouseRightRelease = false;
        }
        if (Hovering || IsOpen)
        {
            player.mouseInterface = true;
            player.controlUseTile = false;
            if (IsOpen) player.controlUseItem = false;
        }
        if (!IsOpen) return;
        panel = new Rectangle(Math.Max(10, (int)(Main.screenWidth / Main.UIScale - 370) / 2),
            Math.Max(10, (int)(Main.screenHeight / Main.UIScale - 448) / 2), 370, 448);
        Vector2 mouse = new(Main.mouseX / Main.UIScale, Main.mouseY / Main.UIScale);
        if (!left) return;
        if (!panel.Contains(mouse.ToPoint())) { Close(); return; }
        for (int row = 0; row < 10; row++)
        {
            if (!Row(row).Contains(mouse.ToPoint())) continue;
            if (row == 9)
            {
                FranCompanionSystem.Request(FranCompanionSystem.Command.Dismiss, openId);
                Close(); return;
            }
            FranSkill skill = row == 0 ? FranSkill.None : (FranSkill)(row - 1);
            if (skill != FranSkill.None && !FranSkillInfo.IsUnlocked(skill)) return;
            FranCompanionSystem.Request(FranCompanionSystem.Command.Tactic, openId, skill);
        }
    }

    private static Rectangle Row(int index) => new(panel.X + 12, panel.Y + 80 + index * 34, panel.Width - 24, 30);

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index < 0) index = layers.Count;
        layers.Insert(index, new LegacyGameInterfaceLayer("Fran: Companion Tactics", Draw, InterfaceScaleType.UI));
    }

    private static bool Draw()
    {
        if (Main.gameMenu) return true;
        FranCompanion own = FranCompanionSystem.FindOwned(Main.myPlayer);
        if (own is null) return true;
        if (!IsOpen)
        {
            if (Hovering)
                Main.instance.MouseText(Text("Hover", own.Automatic ? Text("Automatic") : Text("Manual"),
                    FranSkillInfo.Name(own.ActiveTactic)));
            return true;
        }
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, panel, new Color(18, 23, 40, 240));
        Utils.DrawBorderString(Main.spriteBatch, Text("Title"), new Vector2(panel.X + 14, panel.Y + 12), new Color(160, 222, 255), .92f);
        Utils.DrawBorderString(Main.spriteBatch, Text("Current", FranSkillInfo.Name(own.ActiveTactic)),
            new Vector2(panel.X + 14, panel.Y + 40), Color.White, .72f);
        Utils.DrawBorderString(Main.spriteBatch, Text("Hint"), new Vector2(panel.X + 14, panel.Y + 60), Color.LightGray, .62f);
        Vector2 mouse = new(Main.mouseX / Main.UIScale, Main.mouseY / Main.UIScale);
        for (int row = 0; row < 10; row++)
        {
            Rectangle area = Row(row);
            FranSkill tactic = row is > 0 and < 9 ? (FranSkill)(row - 1) : FranSkill.None;
            bool enabled = tactic == FranSkill.None || FranSkillInfo.IsUnlocked(tactic);
            bool selected = row == 0 ? own.Automatic : row < 9 && !own.Automatic && own.ManualTactic == tactic;
            Color background = selected ? new Color(45, 82, 106) : new Color(30, 36, 56);
            if (area.Contains(mouse.ToPoint()) && enabled) background = Color.Lerp(background, Color.SlateGray, .35f);
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, area, background);
            string label = row == 0 ? Text("Automatic") : row == 9 ? Text("Dismiss") : FranSkillInfo.Name(tactic);
            if (!enabled) label += Text("Locked");
            else if (tactic == FranSkill.MagicBarrier) label += Text("ManualOnly");
            if (selected) label = "◆ " + label;
            Utils.DrawBorderString(Main.spriteBatch, label, new Vector2(area.X + 10, area.Y + 5),
                enabled ? Color.White : Color.Gray, .78f);
        }
        return true;
    }
}
