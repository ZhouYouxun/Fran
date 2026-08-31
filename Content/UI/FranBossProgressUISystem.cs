using System;
using System.Collections.Generic;
using FranMod.Content.Items;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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
public sealed class FranBossProgressUISystem : ModSystem
{
    private const int Columns = 6;
    private const int SlotSize = 52;
    private const int SlotGap = 6;
    private const int Padding = 14;
    private const int TitleHeight = 38;
    private const int FooterHeight = 58;
    private static bool visible;
    private static bool previousMouseLeft;
    private static int feedbackTimer;

    private static int Rows => (FranBossProgressRegistry.Entries.Length + Columns - 1) / Columns;
    private static int PanelWidth => Padding * 2 + Columns * SlotSize + (Columns - 1) * SlotGap;
    private static int PanelHeight => Padding * 2 + TitleHeight + Rows * SlotSize + (Rows - 1) * SlotGap + FooterHeight;

    public static bool Toggle()
    {
        visible = !visible;
        // Opening the panel is itself caused by a left click. Seed our private edge detector
        // with that state so the opening click cannot immediately toggle a boss entry.
        previousMouseLeft = Main.mouseLeft;
        feedbackTimer = 0;
        return visible;
    }

    public override void OnWorldUnload() => ResetInputState();
    public override void Unload() => ResetInputState();

    public override void UpdateUI(GameTime gameTime)
    {
        bool mouseLeft = Main.mouseLeft;
        bool clicked = mouseLeft && !previousMouseLeft;
        previousMouseLeft = mouseLeft;

        if (!visible || Main.gameMenu)
            return;

        Player player = Main.LocalPlayer;
        if (!player.active || player.dead || player.HeldItem.type != ModContent.ItemType<VanillaBossProgressAdjuster>())
        {
            visible = false;
            return;
        }

        Rectangle panel = GetPanelRectangle();
        Point mouse = Main.MouseScreen.ToPoint();
        if (panel.Contains(mouse))
        {
            Main.blockMouse = true;
            player.mouseInterface = true;
            PlayerInput.LockVanillaMouseScroll("FranBossProgressAdjuster");
        }

        if (feedbackTimer > 0)
            feedbackTimer--;
        // Main.mouseLeftRelease may already have been consumed by vanilla inventory UI before
        // ModSystem.UpdateUI runs. A private frame-to-frame edge detector keeps this panel's
        // clicks independent of vanilla UI ordering while still firing only once per press.
        if (!clicked || !panel.Contains(mouse))
            return;

        for (int i = 0; i < FranBossProgressRegistry.Entries.Length; i++)
        {
            if (!GetSlotRectangle(panel, i).Contains(mouse))
                continue;
            FranBossProgressEntry entry = FranBossProgressRegistry.Entries[i];
            Request(FranBossProgressAction.SetSingle, i, !entry.Downed);
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            if (!GetFooterButtonRectangle(panel, i).Contains(mouse))
                continue;
            switch (i)
            {
                case 0:
                    Request(FranBossProgressAction.SetPreHardmode, 0,
                        !FranBossProgressRegistry.IsPhaseComplete(FranBossProgressPhase.PreHardmode));
                    break;
                case 1:
                    Request(FranBossProgressAction.SetHardmode, 0,
                        !FranBossProgressRegistry.IsPhaseComplete(FranBossProgressPhase.Hardmode));
                    break;
                case 2:
                    Request(FranBossProgressAction.SetAll, 0, !FranBossProgressRegistry.IsAllComplete());
                    break;
                default:
                    Request(FranBossProgressAction.InvertAll, 0, false);
                    break;
            }
            return;
        }
    }

    private static void Request(FranBossProgressAction action, int index, bool state)
    {
        FranModMain.RequestBossProgressChange(action, index, state);
        feedbackTimer = 9;
        SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.56f, Pitch = state ? 0.10f : -0.10f }, Main.LocalPlayer.Center);
    }

    private static void ResetInputState()
    {
        visible = false;
        previousMouseLeft = Main.mouseLeft;
        feedbackTimer = 0;
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text", StringComparison.Ordinal));
        if (index < 0)
            index = layers.Count;
        layers.Insert(index, new LegacyGameInterfaceLayer("Fran: Vanilla Boss Progress Adjuster", DrawInterface,
            InterfaceScaleType.UI));
    }

    private static bool DrawInterface()
    {
        if (!visible || Main.gameMenu)
            return true;

        Rectangle panel = GetPanelRectangle();
        Point mouse = Main.MouseScreen.ToPoint();
        DrawRect(panel, new Color(5, 9, 16, 236));
        DrawBorder(panel, new Color(105, 181, 222), 2);
        DrawCentered(Language.GetTextValue("Mods.Fran.BossProgress.Title"),
            new Rectangle(panel.X + Padding, panel.Y + 7, panel.Width - Padding * 2, 28),
            new Color(220, 242, 255), 0.72f);

        for (int i = 0; i < FranBossProgressRegistry.Entries.Length; i++)
        {
            FranBossProgressEntry entry = FranBossProgressRegistry.Entries[i];
            Rectangle slot = GetSlotRectangle(panel, i);
            bool hovered = slot.Contains(mouse);
            DrawBossSlot(entry, slot, hovered);
            if (hovered)
            {
                string status = Language.GetTextValue(entry.Downed
                    ? "Mods.Fran.BossProgress.Defeated"
                    : "Mods.Fran.BossProgress.Undefeated");
                Main.instance.MouseText(Language.GetTextValue("Mods.Fran.BossProgress.EntryHint",
                    entry.DisplayName, status));
            }
        }

        string[] buttonKeys = ["PreHardmode", "Hardmode", "All", "Invert"];
        for (int i = 0; i < buttonKeys.Length; i++)
        {
            Rectangle button = GetFooterButtonRectangle(panel, i);
            bool hovered = button.Contains(mouse);
            bool complete = i switch
            {
                0 => FranBossProgressRegistry.IsPhaseComplete(FranBossProgressPhase.PreHardmode),
                1 => FranBossProgressRegistry.IsPhaseComplete(FranBossProgressPhase.Hardmode),
                2 => FranBossProgressRegistry.IsAllComplete(),
                _ => false
            };
            Color color = i == 0 ? FranBossProgressRegistry.PhaseColor(FranBossProgressPhase.PreHardmode)
                : i == 1 ? FranBossProgressRegistry.PhaseColor(FranBossProgressPhase.Hardmode)
                : i == 2 ? new Color(246, 211, 112) : new Color(231, 154, 105);
            DrawButton(button, Language.GetTextValue($"Mods.Fran.BossProgress.Buttons.{buttonKeys[i]}"),
                color, hovered, complete);
            if (hovered)
                Main.instance.MouseText(Language.GetTextValue($"Mods.Fran.BossProgress.ButtonHints.{buttonKeys[i]}"));
        }
        return true;
    }

    private static void DrawBossSlot(FranBossProgressEntry entry, Rectangle slot, bool hovered)
    {
        Color phase = FranBossProgressRegistry.PhaseColor(entry.Phase);
        Color back = entry.Downed ? Color.Lerp(new Color(24, 31, 42), phase, 0.25f) : new Color(42, 47, 58);
        Color border = entry.Downed ? phase : new Color(116, 124, 141);
        if (hovered)
        {
            back = Color.Lerp(back, new Color(80, 91, 112), 0.46f);
            border = Color.Lerp(border, Color.White, 0.28f);
        }
        if (feedbackTimer > 0 && hovered)
            border = Color.Lerp(border, new Color(255, 226, 148), feedbackTimer / 9f);
        DrawRect(slot, back);
        DrawBorder(slot, border, 2);

        try
        {
            Texture2D texture = ModContent.Request<Texture2D>(entry.IconPath, AssetRequestMode.ImmediateLoad).Value;
            float fit = Math.Min(40f / Math.Max(1f, texture.Width), 40f / Math.Max(1f, texture.Height));
            float scale = fit * (hovered ? 1.08f : 1f);
            Color iconColor = entry.Downed ? Color.White : new Color(174, 180, 191) * 0.76f;
            Main.spriteBatch.Draw(texture, slot.Center.ToVector2(), null, iconColor, 0f,
                texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }
        catch
        {
            DrawRect(new Rectangle(slot.Center.X - 9, slot.Center.Y - 2, 18, 4), border);
            DrawRect(new Rectangle(slot.Center.X - 2, slot.Center.Y - 9, 4, 18), border);
        }

        DrawRect(new Rectangle(slot.X + 5, slot.Bottom - 8, slot.Width - 10, 3),
            entry.Downed ? phase : new Color(190, 87, 91));
    }

    private static void DrawButton(Rectangle button, string text, Color color, bool hovered, bool complete)
    {
        Color back = complete ? Color.Lerp(new Color(28, 35, 44), color, 0.24f) : new Color(39, 45, 56);
        if (hovered)
            back = Color.Lerp(back, new Color(78, 87, 104), 0.52f);
        DrawRect(button, back);
        DrawBorder(button, hovered ? Color.Lerp(color, Color.White, 0.42f) : color * 0.72f, 2);
        DrawCentered(text, button, complete ? Color.White : new Color(210, 216, 226), 0.50f);
    }

    private static Rectangle GetPanelRectangle() =>
        new((Main.screenWidth - PanelWidth) / 2, (Main.screenHeight - PanelHeight) / 2, PanelWidth, PanelHeight);

    private static Rectangle GetSlotRectangle(Rectangle panel, int index)
    {
        int row = index / Columns;
        int column = index % Columns;
        return new Rectangle(panel.X + Padding + column * (SlotSize + SlotGap),
            panel.Y + Padding + TitleHeight + row * (SlotSize + SlotGap), SlotSize, SlotSize);
    }

    private static Rectangle GetFooterButtonRectangle(Rectangle panel, int index)
    {
        int gap = 7;
        int width = (panel.Width - Padding * 2 - gap * 3) / 4;
        int y = panel.Bottom - Padding - 38;
        return new Rectangle(panel.X + Padding + index * (width + gap), y, width, 38);
    }

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
