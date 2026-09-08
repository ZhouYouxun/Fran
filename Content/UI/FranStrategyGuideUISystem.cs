using System;
using System.Collections.Generic;
using FranMod.Content.Items;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
public sealed class FranStrategyGuideUISystem : ModSystem
{
    private const int MinimumSlots = 2;
    private const int MaximumSlots = 4;
    private const int OuterPadding = 16;
    private const int CardGap = 8;
    private const string TextRoot = "Mods.Fran.StrategyGuide";

    private static readonly GuideEntry[] TwoSlotEntries =
    [
        new("Page2.Entry1", FranSkill.TelekineticCatapult, FranSkill.TelekineticCatapult),
        new("Page2.Entry2", FranSkill.SkillTaker, FranSkill.SkillTaker),
        new("Page2.Entry3", FranSkill.MagicBarrier, FranSkill.MagicBarrier),
        new("Page2.Entry4", FranSkill.BlueResonance, FranSkill.BlueResonance),
        new("Page2.Entry5", FranSkill.TelekineticCatapult, FranSkill.SkillTaker),
        new("Page2.Entry6", FranSkill.MagicBarrier, FranSkill.BlueResonance)
    ];

    private static readonly GuideEntry[] ThreeSlotEntries =
    [
        new("Page3.Entry1", FranSkill.SkillTaker, FranSkill.SkillTaker, FranSkill.SkillTaker),
        new("Page3.Entry2", FranSkill.BlueResonance, FranSkill.BlueResonance, FranSkill.BlueResonance),
        new("Page3.Entry3", FranSkill.SkillTaker, FranSkill.SkillTaker, FranSkill.TelekineticCatapult),
        new("Page3.Entry4", FranSkill.MagicBarrier, FranSkill.MagicBarrier, FranSkill.BlueResonance),
        new("Page3.Entry5", FranSkill.MultipleBodyCreation, FranSkill.MultipleBodyCreation,
            FranSkill.PressurizedQuickdraw),
        new("Page3.Entry6", FranSkill.TelekineticCatapult, FranSkill.SkillTaker, FranSkill.MagicBarrier)
    ];

    private static readonly GuideEntry[] FourSlotEntries =
    [
        new("Page4.Entry1", FranSkill.TelekineticCatapult, FranSkill.TelekineticCatapult,
            FranSkill.TelekineticCatapult, FranSkill.TelekineticCatapult),
        new("Page4.Entry2", FranSkill.SkillTaker, FranSkill.SkillTaker,
            FranSkill.SkillTaker, FranSkill.SkillTaker),
        new("Page4.Entry3", FranSkill.MagicBarrier, FranSkill.MagicBarrier,
            FranSkill.MagicBarrier, FranSkill.MagicBarrier),
        new("Page4.Entry4", FranSkill.PressurizedQuickdraw, FranSkill.PressurizedQuickdraw,
            FranSkill.PressurizedQuickdraw, FranSkill.PressurizedQuickdraw),
        new("Page4.Entry5", FranSkill.SkillTaker, FranSkill.SkillTaker,
            FranSkill.TelekineticCatapult, FranSkill.BlueResonance),
        new("Page4.Entry6", FranSkill.MagicBarrier, FranSkill.MagicBarrier,
            FranSkill.BlueResonance, FranSkill.BlueResonance),
        new("Page4.Entry7", FranSkill.MultipleBodyCreation, FranSkill.MultipleBodyCreation,
            FranSkill.PressurizedQuickdraw, FranSkill.PressurizedQuickdraw),
        new("Page4.Entry8", FranSkill.TelekineticCatapult, FranSkill.SkillTaker,
            FranSkill.MagicBarrier, FranSkill.BlueResonance)
    ];

    private static bool visible;
    private static bool previousMouseLeft;
    private static bool previousMouseRight;
    private static int viewedSlots = MinimumSlots;

    public static bool Toggle()
    {
        visible = !visible;
        previousMouseLeft = Main.mouseLeft;
        previousMouseRight = Main.mouseRight;
        if (visible)
            viewedSlots = Math.Clamp(FranBalance.GetAvailableTacticSlots(), MinimumSlots, MaximumSlots);
        return visible;
    }

    public override void OnWorldUnload() => ResetState();
    public override void Unload() => ResetState();

    public override void UpdateUI(GameTime gameTime)
    {
        bool mouseLeft = Main.mouseLeft;
        bool mouseRight = Main.mouseRight;
        bool leftPressed = mouseLeft && !previousMouseLeft;
        bool rightPressed = mouseRight && !previousMouseRight;
        previousMouseLeft = mouseLeft;
        previousMouseRight = mouseRight;

        if (!visible || Main.gameMenu)
            return;

        Player player = Main.LocalPlayer;
        if (!player.active || player.dead || Main.playerInventory ||
            player.HeldItem.type != ModContent.ItemType<StrategyGuideBook>())
        {
            Close(silent: true);
            return;
        }

        Main.blockMouse = true;
        player.mouseInterface = true;
        PlayerInput.LockVanillaMouseScroll("FranStrategyGuide");

        if (rightPressed)
        {
            Close();
            return;
        }

        if (!leftPressed)
            return;

        Rectangle panel = GetPanelRectangle();
        Point mouse = Main.MouseScreen.ToPoint();
        if (GetCloseButton(panel).Contains(mouse))
        {
            Close();
            return;
        }

        for (int slotCount = MinimumSlots; slotCount <= MaximumSlots; slotCount++)
        {
            if (!GetTabRectangle(panel, slotCount).Contains(mouse))
                continue;
            viewedSlots = slotCount;
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.40f, Pitch = 0.12f });
            return;
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer =>
            layer.Name.Equals("Vanilla: Mouse Text", StringComparison.Ordinal));
        if (index < 0)
            index = layers.Count;
        layers.Insert(index, new LegacyGameInterfaceLayer("Fran: Strategy Guide", DrawInterface,
            InterfaceScaleType.UI));
    }

    private static bool DrawInterface()
    {
        if (!visible || Main.gameMenu)
            return true;

        Rectangle panel = GetPanelRectangle();
        Point mouse = Main.MouseScreen.ToPoint();
        int currentSlots = Math.Clamp(FranBalance.GetAvailableTacticSlots(), MinimumSlots, MaximumSlots);

        DrawRect(new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Color(0, 3, 8, 145));
        DrawRect(panel, new Color(5, 10, 18, 246));
        DrawBorder(panel, new Color(92, 188, 229), 2);
        DrawRect(new Rectangle(panel.X + 3, panel.Y + 3, panel.Width - 6, 2),
            new Color(170, 115, 255) * 0.72f);

        Rectangle close = GetCloseButton(panel);
        bool closeHovered = close.Contains(mouse);
        DrawRect(close, closeHovered ? new Color(108, 48, 68) : new Color(40, 30, 48));
        DrawBorder(close, closeHovered ? new Color(255, 155, 178) : new Color(139, 102, 151), 1);
        DrawCentered("×", close, Color.White, 0.72f);

        DrawCentered(Language.GetTextValue($"{TextRoot}.Title"),
            new Rectangle(panel.X + 48, panel.Y + 8, panel.Width - 96, 25),
            new Color(224, 245, 255), 0.78f);
        DrawCentered(Language.GetTextValue($"{TextRoot}.Subtitle"),
            new Rectangle(panel.X + 42, panel.Y + 32, panel.Width - 84, 20),
            new Color(145, 184, 204), 0.50f);

        for (int slotCount = MinimumSlots; slotCount <= MaximumSlots; slotCount++)
            DrawTab(panel, slotCount, currentSlots, mouse);

        Rectangle structure = GetStructureRectangle(panel);
        DrawRect(structure, new Color(14, 24, 36, 235));
        DrawBorder(structure, new Color(72, 132, 164), 1);
        string status = Language.GetTextValue($"{TextRoot}.CurrentStatus", currentSlots, viewedSlots);
        DrawText(status, new Vector2(structure.X + 10, structure.Y + 5),
            new Color(244, 211, 116), 0.52f, structure.Width - 20);
        DrawWrapped(Language.GetTextValue($"{TextRoot}.Page{viewedSlots}.Structure"),
            new Rectangle(structure.X + 10, structure.Y + 27, structure.Width - 20, structure.Height - 30),
            new Color(202, 222, 232), 0.50f, 2);

        GuideEntry[] entries = GetEntries(viewedSlots);
        Rectangle cardArea = GetCardArea(panel);
        int rows = (entries.Length + 1) / 2;
        int cardWidth = (cardArea.Width - CardGap) / 2;
        int cardHeight = (cardArea.Height - CardGap * (rows - 1)) / rows;
        for (int i = 0; i < entries.Length; i++)
        {
            int column = i % 2;
            int row = i / 2;
            Rectangle card = new(cardArea.X + column * (cardWidth + CardGap),
                cardArea.Y + row * (cardHeight + CardGap), cardWidth, cardHeight);
            DrawEntry(entries[i], card, card.Contains(mouse));
        }

        Rectangle summary = GetSummaryRectangle(panel);
        DrawRect(summary, new Color(18, 14, 31, 240));
        DrawBorder(summary, new Color(151, 105, 218), 1);
        DrawCentered(Language.GetTextValue($"{TextRoot}.FullLoadoutTitle"),
            new Rectangle(summary.X + 8, summary.Y + 3, summary.Width - 16, 20),
            new Color(224, 185, 255), 0.55f);
        DrawWrappedCentered(Language.GetTextValue($"{TextRoot}.FullLoadoutSummary"),
            new Rectangle(summary.X + 12, summary.Y + 23, summary.Width - 24, summary.Height - 27),
            new Color(215, 226, 236), 0.47f, 3);

        DrawCentered(Language.GetTextValue($"{TextRoot}.CloseHint"),
            new Rectangle(panel.X + 20, panel.Bottom - 23, panel.Width - 40, 18),
            new Color(112, 145, 161), 0.44f);
        return true;
    }

    private static void DrawTab(Rectangle panel, int slotCount, int currentSlots, Point mouse)
    {
        Rectangle tab = GetTabRectangle(panel, slotCount);
        bool selected = viewedSlots == slotCount;
        bool hovered = tab.Contains(mouse);
        Color accent = slotCount switch
        {
            2 => new Color(84, 183, 235),
            3 => new Color(145, 116, 232),
            _ => new Color(242, 185, 89)
        };
        Color back = selected ? Color.Lerp(new Color(22, 29, 42), accent, 0.28f) : new Color(25, 31, 42);
        if (hovered)
            back = Color.Lerp(back, new Color(82, 92, 112), 0.40f);
        DrawRect(tab, back);
        DrawBorder(tab, selected ? accent : new Color(74, 85, 103), selected ? 2 : 1);
        string label = Language.GetTextValue($"{TextRoot}.Tabs.Slots{slotCount}");
        if (slotCount == currentSlots)
            label = Language.GetTextValue($"{TextRoot}.Tabs.Current", label);
        DrawCentered(label, tab, selected ? Color.White : new Color(185, 199, 211), 0.54f);
    }

    private static void DrawEntry(GuideEntry entry, Rectangle card, bool hovered)
    {
        Color accent = FranSkillInfo.Color(entry.Skills[0]);
        Color back = hovered ? new Color(27, 38, 53, 248) : new Color(12, 20, 30, 242);
        DrawRect(card, back);
        DrawBorder(card, hovered ? Color.Lerp(accent, Color.White, 0.28f) : accent * 0.72f,
            hovered ? 2 : 1);

        const int iconSize = 22;
        const int iconGap = 3;
        int iconsWidth = entry.Skills.Length * iconSize + (entry.Skills.Length - 1) * iconGap;
        int iconsX = card.Right - 8 - iconsWidth;
        for (int i = 0; i < entry.Skills.Length; i++)
        {
            Rectangle iconBack = new(iconsX + i * (iconSize + iconGap), card.Y + 7, iconSize, iconSize);
            DrawRect(iconBack, new Color(4, 8, 14, 220));
            DrawBorder(iconBack, FranSkillInfo.Color(entry.Skills[i]) * 0.64f, 1);
            DrawSkillIcon(entry.Skills[i], iconBack);
        }

        string title = Language.GetTextValue($"{TextRoot}.{entry.Key}.Title");
        DrawText(title, new Vector2(card.X + 9, card.Y + 8),
            Color.Lerp(accent, Color.White, 0.48f), 0.56f,
            Math.Max(70, iconsX - card.X - 15));

        int bodyTop = card.Y + 34;
        DrawWrapped(Language.GetTextValue($"{TextRoot}.{entry.Key}.Body"),
            new Rectangle(card.X + 9, bodyTop, card.Width - 18, card.Bottom - bodyTop - 5),
            new Color(210, 224, 234), card.Height < 92 ? 0.43f : 0.48f,
            card.Height < 92 ? 2 : 3);
    }

    private static void DrawSkillIcon(FranSkill skill, Rectangle area)
    {
        Texture2D texture = ModContent.Request<Texture2D>(FranUISystem.SkillIconPath(skill)).Value;
        int size = Math.Min(area.Width - 2, area.Height - 2);
        Rectangle destination = new(area.Center.X - size / 2, area.Center.Y - size / 2, size, size);
        Main.spriteBatch.Draw(texture, destination, Color.White);
    }

    private static GuideEntry[] GetEntries(int slotCount) => slotCount switch
    {
        2 => TwoSlotEntries,
        3 => ThreeSlotEntries,
        _ => FourSlotEntries
    };

    private static Rectangle GetPanelRectangle()
    {
        int width = Math.Min(940, Math.Max(620, Main.screenWidth - 32));
        int height = Math.Min(690, Math.Max(520, Main.screenHeight - 32));
        return new Rectangle((Main.screenWidth - width) / 2, (Main.screenHeight - height) / 2,
            width, height);
    }

    private static Rectangle GetCloseButton(Rectangle panel) =>
        new(panel.Right - 38, panel.Y + 9, 27, 25);

    private static Rectangle GetTabRectangle(Rectangle panel, int slotCount)
    {
        const int gap = 8;
        int width = (panel.Width - OuterPadding * 2 - gap * 2) / 3;
        return new Rectangle(panel.X + OuterPadding + (slotCount - MinimumSlots) * (width + gap),
            panel.Y + 58, width, 32);
    }

    private static Rectangle GetStructureRectangle(Rectangle panel) =>
        new(panel.X + OuterPadding, panel.Y + 98, panel.Width - OuterPadding * 2, 64);

    private static Rectangle GetCardArea(Rectangle panel) =>
        new(panel.X + OuterPadding, panel.Y + 170, panel.Width - OuterPadding * 2,
            panel.Height - 170 - 104);

    private static Rectangle GetSummaryRectangle(Rectangle panel) =>
        new(panel.X + OuterPadding, panel.Bottom - 96, panel.Width - OuterPadding * 2, 68);

    private static void Close(bool silent = false)
    {
        if (!visible)
            return;
        visible = false;
        if (!silent)
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.58f, Pitch = 0.04f });
    }

    private static void ResetState()
    {
        visible = false;
        previousMouseLeft = Main.mouseLeft;
        previousMouseRight = Main.mouseRight;
        viewedSlots = MinimumSlots;
    }

    private static void DrawText(string text, Vector2 position, Color color, float scale, float maxWidth)
    {
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
        float actualScale = Math.Min(scale, maxWidth / Math.Max(1f, size.X));
        Utils.DrawBorderString(Main.spriteBatch, text, position, color, actualScale);
    }

    private static void DrawCentered(string text, Rectangle area, Color color, float scale)
    {
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
        float actualScale = Math.Min(scale, (area.Width - 6f) / Math.Max(1f, size.X));
        Vector2 position = area.Center.ToVector2() - size * actualScale * 0.5f;
        Utils.DrawBorderString(Main.spriteBatch, text, position, color, actualScale);
    }

    private static void DrawWrapped(string text, Rectangle area, Color color, float scale, int maxLines)
    {
        List<string> lines = WrapText(text, area.Width, scale);
        if (lines.Count > maxLines)
        {
            lines.RemoveRange(maxLines, lines.Count - maxLines);
            lines[^1] = lines[^1].TrimEnd('…') + "…";
        }
        float lineHeight = FontAssets.MouseText.Value.MeasureString("国").Y * scale;
        float y = area.Y;
        foreach (string line in lines)
        {
            Utils.DrawBorderString(Main.spriteBatch, line, new Vector2(area.X, y), color, scale);
            y += lineHeight;
        }
    }

    private static void DrawWrappedCentered(string text, Rectangle area, Color color, float scale,
        int maxLines)
    {
        List<string> lines = WrapText(text, area.Width, scale);
        if (lines.Count > maxLines)
        {
            lines.RemoveRange(maxLines, lines.Count - maxLines);
            lines[^1] = lines[^1].TrimEnd('…') + "…";
        }
        float lineHeight = FontAssets.MouseText.Value.MeasureString("国").Y * scale;
        float y = area.Center.Y - lines.Count * lineHeight * 0.5f;
        foreach (string line in lines)
        {
            Vector2 size = FontAssets.MouseText.Value.MeasureString(line) * scale;
            Utils.DrawBorderString(Main.spriteBatch, line,
                new Vector2(area.Center.X - size.X * 0.5f, y), color, scale);
            y += lineHeight;
        }
    }

    private static List<string> WrapText(string text, float maxWidth, float scale)
    {
        List<string> lines = [];
        string remaining = text.Trim();
        while (remaining.Length > 0)
        {
            int bestLength = 1;
            int lastWhitespace = -1;
            for (int i = 0; i < remaining.Length; i++)
            {
                if (char.IsWhiteSpace(remaining[i]))
                    lastWhitespace = i;
                if (FontAssets.MouseText.Value.MeasureString(remaining[..(i + 1)]).X * scale <= maxWidth)
                {
                    bestLength = i + 1;
                    continue;
                }
                if (lastWhitespace > 0)
                    bestLength = lastWhitespace;
                break;
            }
            string line = remaining[..bestLength].Trim();
            lines.Add(line.Length == 0 ? remaining[..1] : line);
            remaining = remaining[bestLength..].TrimStart();
        }
        return lines;
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

    private sealed class GuideEntry
    {
        public string Key { get; }
        public FranSkill[] Skills { get; }

        public GuideEntry(string key, params FranSkill[] skills)
        {
            Key = key;
            Skills = skills;
        }
    }
}
