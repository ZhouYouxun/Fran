using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FranMod.Content.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace FranMod.Content.UI;

[Autoload(Side = ModSide.Client)]
public sealed class FranDamageDebugUISystem : ModSystem
{
    private const int PanelWidth = 430;
    private const int PanelHeight = 190;
    private static bool visible;
    private static bool previousMouseLeft;
    private static Item targetItem;
    private static string input = string.Empty;
    private static int feedbackTimer;
    private static int invalidTimer;

    public static bool Toggle(Item item)
    {
        if (visible && ReferenceEquals(targetItem, item))
        {
            Close();
            return false;
        }

        targetItem = item;
        input = item.ModItem is FranDamageDebugger debugger
            ? debugger.PanelDamage.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        visible = true;
        previousMouseLeft = Main.mouseLeft;
        feedbackTimer = 0;
        invalidTimer = 0;
        return true;
    }

    public override void OnWorldUnload() => Close();
    public override void Unload() => Close();

    public override void UpdateUI(GameTime gameTime)
    {
        bool mouseLeft = Main.mouseLeft;
        bool clicked = mouseLeft && !previousMouseLeft;
        previousMouseLeft = mouseLeft;

        if (!visible || Main.gameMenu)
            return;

        Player player = Main.LocalPlayer;
        if (!TryResolveTarget(player, out FranDamageDebugger debugger, out int inventorySlot))
        {
            Close();
            return;
        }

        Rectangle panel = GetPanelRectangle();
        Rectangle inputBox = GetInputRectangle(panel);
        Rectangle applyButton = GetApplyRectangle(panel);
        Point mouse = Main.MouseScreen.ToPoint();
        if (panel.Contains(mouse))
        {
            Main.blockMouse = true;
            player.mouseInterface = true;
            PlayerInput.LockVanillaMouseScroll("FranDamageDebugger");
        }

        if (Main.keyState.IsKeyDown(Keys.Escape) && !Main.oldKeyState.IsKeyDown(Keys.Escape))
        {
            Close();
            return;
        }

        PlayerInput.WritingText = true;
        Main.instance.HandleIME();
        input = FilterInput(Main.GetInputText(input));

        bool pressedEnter = Main.keyState.IsKeyDown(Keys.Enter) && !Main.oldKeyState.IsKeyDown(Keys.Enter);
        if (pressedEnter || clicked && applyButton.Contains(mouse))
            Apply(player, debugger, inventorySlot);

        if (feedbackTimer > 0)
            feedbackTimer--;
        if (invalidTimer > 0)
            invalidTimer--;
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text", StringComparison.Ordinal));
        if (index < 0)
            index = layers.Count;
        layers.Insert(index, new LegacyGameInterfaceLayer("Fran: Damage Debugger", DrawInterface,
            InterfaceScaleType.UI));
    }

    private static bool DrawInterface()
    {
        if (!visible || Main.gameMenu)
            return true;

        Rectangle panel = GetPanelRectangle();
        Rectangle inputBox = GetInputRectangle(panel);
        Rectangle applyButton = GetApplyRectangle(panel);
        Point mouse = Main.MouseScreen.ToPoint();
        bool enabled = targetItem?.favorited == true;
        Color accent = enabled ? new Color(96, 224, 170) : new Color(106, 190, 238);

        DrawRect(panel, new Color(5, 10, 18, 240));
        DrawBorder(panel, accent, 2);
        DrawCentered(Language.GetTextValue("Mods.Fran.DamageDebug.Title"),
            new Rectangle(panel.X + 18, panel.Y + 10, panel.Width - 36, 28),
            new Color(224, 244, 255), 0.72f);
        DrawCentered(Language.GetTextValue(enabled
                ? "Mods.Fran.DamageDebug.Active"
                : "Mods.Fran.DamageDebug.Inactive"),
            new Rectangle(panel.X + 18, panel.Y + 41, panel.Width - 36, 24),
            enabled ? new Color(126, 238, 172) : new Color(190, 196, 208), 0.55f);

        DrawRect(inputBox, new Color(18, 27, 40, 248));
        DrawBorder(inputBox, invalidTimer > 0 ? new Color(245, 105, 112) : accent, 2);
        string shown = input;
        if ((Main.GameUpdateCount / 24u & 1u) == 0u)
            shown += "|";
        Utils.DrawBorderString(Main.spriteBatch, shown,
            new Vector2(inputBox.X + 12, inputBox.Center.Y - 12), Color.White, 0.62f);

        bool hovered = applyButton.Contains(mouse);
        DrawRect(applyButton, hovered ? new Color(58, 102, 126) : new Color(34, 68, 86));
        DrawBorder(applyButton, hovered ? Color.White : accent, 2);
        DrawCentered(Language.GetTextValue("Mods.Fran.DamageDebug.Apply"), applyButton,
            feedbackTimer > 0 ? new Color(255, 228, 142) : Color.White, 0.60f);

        string footerKey = invalidTimer > 0 ? "Invalid" : feedbackTimer > 0 ? "Saved" : "Hint";
        DrawCentered(Language.GetTextValue($"Mods.Fran.DamageDebug.{footerKey}"),
            new Rectangle(panel.X + 20, panel.Bottom - 47, panel.Width - 40, 28),
            invalidTimer > 0 ? new Color(245, 116, 122) : new Color(176, 205, 222), 0.52f);
        return true;
    }

    private static void Apply(Player player, FranDamageDebugger debugger, int inventorySlot)
    {
        if (!int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < 1)
        {
            invalidTimer = 90;
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.45f, Pitch = 0.18f });
            return;
        }

        debugger.SetPanelDamage(value);
        input = debugger.PanelDamage.ToString(CultureInfo.InvariantCulture);
        feedbackTimer = 90;
        invalidTimer = 0;
        SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.62f, Pitch = 0.16f });
        if (Main.netMode == NetmodeID.MultiplayerClient)
            NetMessage.SendData(MessageID.SyncEquipment, -1, -1, null,
                player.whoAmI, inventorySlot, targetItem.prefix);
    }

    private static bool TryResolveTarget(Player player, out FranDamageDebugger debugger, out int inventorySlot)
    {
        debugger = null;
        inventorySlot = -1;
        if (player is null || !player.active || player.dead || targetItem is null)
            return false;

        for (int slot = 0; slot < 50; slot++)
        {
            if (!ReferenceEquals(player.inventory[slot], targetItem))
                continue;
            debugger = targetItem.ModItem as FranDamageDebugger;
            inventorySlot = slot;
            return debugger is not null;
        }
        return false;
    }

    private static string FilterInput(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        StringBuilder digits = new(8);
        foreach (char character in value)
        {
            if (character is < '0' or > '9')
                continue;
            if (digits.Length == 8)
                break;
            digits.Append(character);
        }
        return digits.ToString();
    }

    private static void Close()
    {
        visible = false;
        targetItem = null;
        input = string.Empty;
        feedbackTimer = 0;
        invalidTimer = 0;
        if (!Main.drawingPlayerChat)
            PlayerInput.WritingText = false;
    }

    private static Rectangle GetPanelRectangle() =>
        new((Main.screenWidth - PanelWidth) / 2, (Main.screenHeight - PanelHeight) / 2,
            PanelWidth, PanelHeight);

    private static Rectangle GetInputRectangle(Rectangle panel) =>
        new(panel.X + 28, panel.Y + 76, 272, 48);

    private static Rectangle GetApplyRectangle(Rectangle panel) =>
        new(panel.X + 310, panel.Y + 76, 92, 48);

    private static void DrawCentered(string text, Rectangle area, Color color, float scale)
    {
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
        float actualScale = Math.Min(scale, (area.Width - 8f) / Math.Max(1f, size.X));
        Vector2 position = area.Center.ToVector2() - size * actualScale * 0.5f;
        Utils.DrawBorderString(Main.spriteBatch, text, position, color, actualScale);
    }

    private static void DrawRect(Rectangle rectangle, Color color) =>
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, rectangle, color);

    private static void DrawBorder(Rectangle rectangle, Color color, int thickness)
    {
        DrawRect(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        DrawRect(new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        DrawRect(new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        DrawRect(new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
}
