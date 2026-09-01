using System;
using System.Collections.Generic;
using FranMod.Content.Config;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace FranMod.Content.UI;

public sealed class FranUISystem : ModSystem
{
    private const float SkillIconDrawScale = 0.75f;
    private const string SkillIconRoot = "Fran/Textures/技能组图标/32/";
    private const string PureSwordsmanshipIcon = SkillIconRoot + "FranSkill_09_PureSwordsmanship";
    private const string UltimateIcon = SkillIconRoot + "FranSkill_10_Ultimate_BlackHeavenlyTiger";
    private static readonly string[] SkillIcons =
    [
        SkillIconRoot + "FranSkill_01_TelekineticCatapult",
        SkillIconRoot + "FranSkill_02_SkillTaker",
        SkillIconRoot + "FranSkill_03_MagicBarrier",
        SkillIconRoot + "FranSkill_04_MultipleBodyCreation",
        SkillIconRoot + "FranSkill_05_BlueResonance",
        SkillIconRoot + "FranSkill_06_ThunderChain",
        SkillIconRoot + "FranSkill_07_PressurizedQuickdraw",
        SkillIconRoot + "FranSkill_08_ThorsHammer"
    ];

    private static bool wheelOpen;
    private static Vector2 wheelCenter;
    private static int hoveredSkill = -1;
    private static int hoveredEquippedSlot = -1;
    private static int previousHovered = -2;
    private static bool previousWheelHeld;
    private static bool draggingUltimateGauge;
    private static Vector2 ultimateGaugeDragOffset;

    public static bool IsWheelOpen => wheelOpen;

    public override void UpdateUI(GameTime gameTime)
    {
        bool wheelHeld = Main.mouseRight || FranModMain.SkillWheelKey.Current;
        bool wheelPressed = wheelHeld && !previousWheelHeld;
        previousWheelHeld = wheelHeld;

        if (Main.gameMenu || Main.LocalPlayer is null)
        {
            wheelOpen = false;
            return;
        }

        Player player = Main.LocalPlayer;
        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        UpdateUltimateGaugeDrag(player, fran);
        bool allowed = !FranEvolutionUISystem.IsOpen && !Main.playerInventory && !player.dead &&
            player.HeldItem.type == ModContent.ItemType<Teacher>();
        if (!allowed)
        {
            if (wheelOpen)
                CloseWheel();
            return;
        }

        if (wheelPressed)
        {
            if (wheelOpen)
            {
                CloseWheel();
                return;
            }

            wheelOpen = true;
            float minX = Math.Min(300f, Main.screenWidth * 0.5f);
            float maxX = Math.Max(minX, Main.screenWidth - minX);
            float minY = Math.Min(180f, Main.screenHeight * 0.40f);
            float maxY = Math.Max(minY, Main.screenHeight - 344f);
            wheelCenter = new Vector2(MathHelper.Clamp(Main.MouseScreen.X, minX, maxX),
                MathHelper.Clamp(Main.MouseScreen.Y, minY, maxY));
            previousHovered = -2;
            SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.55f });
        }

        if (wheelOpen)
        {
            PlayerInput.LockVanillaMouseScroll("FranSkillWheel");
            Main.blockMouse = true;
            player.mouseInterface = true;
            hoveredSkill = FindHoveredSkill(Main.MouseScreen);
            hoveredEquippedSlot = FindHoveredEquippedSlot(fran, Main.MouseScreen);
            int hoverToken = hoveredSkill >= 0 ? hoveredSkill : hoveredEquippedSlot >= 0
                ? 100 + hoveredEquippedSlot : -1;
            if (hoverToken != previousHovered)
            {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.28f, Pitch = 0.16f });
                previousHovered = hoverToken;
            }

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                if (hoveredSkill >= 0)
                    fran.TryEquipTactic((FranSkill)hoveredSkill);
                else if (hoveredEquippedSlot >= 0)
                    fran.TryUnequipTactic(hoveredEquippedSlot);
            }
        }
    }

    private static void CloseWheel()
    {
        wheelOpen = false;
        hoveredSkill = -1;
        hoveredEquippedSlot = -1;
        previousHovered = -2;
        SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (index < 0)
            index = layers.Count;
        layers.Insert(index, new LegacyGameInterfaceLayer("Fran: Character HUD", DrawHud, InterfaceScaleType.UI));
    }

    private static bool DrawHud()
    {
        if (Main.gameMenu || Main.LocalPlayer is null)
            return true;

        Player player = Main.LocalPlayer;
        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        bool holdingTeacher = player.HeldItem.type == ModContent.ItemType<Teacher>();
        if (!fran.FranMode && !holdingTeacher)
            return true;

        if (fran.FranMode && !ModContent.GetInstance<FranClientConfig>().HideUltimateGauge)
            DrawUltimateGauge(fran);
        DrawMatrix(fran);
        if (wheelOpen)
            DrawWheel(fran);
        return true;
    }

    private static void UpdateUltimateGaugeDrag(Player player, FranPlayer fran)
    {
        FranClientConfig config = ModContent.GetInstance<FranClientConfig>();
        if (!fran.FranMode || config.HideUltimateGauge || !config.AllowUltimateGaugeDragging ||
            Main.playerInventory)
        {
            draggingUltimateGauge = false;
            return;
        }

        Rectangle gauge = GetUltimateGaugeRect(fran);
        Point mouse = Main.MouseScreen.ToPoint();
        bool hovered = gauge.Contains(mouse);
        if (hovered || draggingUltimateGauge)
        {
            Main.blockMouse = true;
            player.mouseInterface = true;
        }

        if (!draggingUltimateGauge && hovered && Main.mouseLeft && Main.mouseLeftRelease)
        {
            draggingUltimateGauge = true;
            ultimateGaugeDragOffset = Main.MouseScreen - gauge.TopLeft();
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f, Pitch = 0.1f });
        }

        if (!draggingUltimateGauge)
            return;
        if (!Main.mouseLeft)
        {
            draggingUltimateGauge = false;
            return;
        }

        Vector2 topLeft = Main.MouseScreen - ultimateGaugeDragOffset;
        topLeft.X = MathHelper.Clamp(topLeft.X, 8f, Math.Max(8f, Main.screenWidth - gauge.Width - 8f));
        topLeft.Y = MathHelper.Clamp(topLeft.Y, 8f, Math.Max(8f, Main.screenHeight - gauge.Height - 8f));
        Vector2 center = topLeft + new Vector2(gauge.Width, gauge.Height) * 0.5f;
        fran.SetUltimateGaugeAnchor(new Vector2(center.X / Math.Max(1f, Main.screenWidth),
            center.Y / Math.Max(1f, Main.screenHeight)));
    }

    private static Rectangle GetUltimateGaugeRect(FranPlayer fran)
    {
        const int width = 188;
        const int height = 22;
        Vector2 center = new(fran.UltimateGaugeAnchor.X * Main.screenWidth,
            fran.UltimateGaugeAnchor.Y * Main.screenHeight);
        center.X = MathHelper.Clamp(center.X, width * 0.5f + 8f, Main.screenWidth - width * 0.5f - 8f);
        center.Y = MathHelper.Clamp(center.Y, height * 0.5f + 8f, Main.screenHeight - height * 0.5f - 8f);
        return new Rectangle((int)(center.X - width * 0.5f), (int)(center.Y - height * 0.5f), width, height);
    }

    private static void DrawUltimateGauge(FranPlayer fran)
    {
        Rectangle outer = GetUltimateGaugeRect(fran);
        float value = MathHelper.Clamp(fran.UltimateChargeTicks / (float)FranPlayer.UltimateChargeDuration, 0f, 1f);
        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5.5f);
        Color border = fran.UltimateReady
            ? Color.Lerp(new Color(255, 211, 105), Color.White, pulse * 0.42f)
            : new Color(92, 175, 215);

        DrawRect(outer, new Color(5, 8, 15, 224));
        DrawBorder(outer, new Color(18, 31, 48, 245), 3);
        DrawBorder(new Rectangle(outer.X + 2, outer.Y + 2, outer.Width - 4, outer.Height - 4), border, 1);

        DrawSkillIcon(outer.TopLeft() + new Vector2(12f, outer.Height * 0.5f), 18f, UltimateIcon,
            Color.White, fran.UltimateReady ? 1f : 0.84f);

        Rectangle track = new(outer.X + 27, outer.Y + 7, outer.Width - 34, outer.Height - 14);
        DrawRect(track, new Color(21, 18, 38, 235));
        int filledWidth = (int)(track.Width * value);
        if (filledWidth > 0)
        {
            Rectangle fill = new(track.X, track.Y, filledWidth, track.Height);
            DrawRect(fill, Color.Lerp(new Color(91, 74, 178), new Color(103, 218, 245), value) * 0.92f);
            if (fran.UltimateReady)
                DrawRect(new Rectangle(fill.X, fill.Y, fill.Width, 1), Color.White * (0.65f + pulse * 0.25f));
        }

        for (int i = 1; i < 10; i++)
        {
            int x = track.X + track.Width * i / 10;
            DrawRect(new Rectangle(x, track.Y, 1, track.Height), new Color(4, 7, 14, 175));
        }

        string text = fran.UltimateReady
            ? Language.GetTextValue("Mods.Fran.UI.UltimateReady")
            : Language.GetTextValue("Mods.Fran.UI.UltimateCharge", (int)MathF.Floor(value * 100f));
        DrawCentered(text, new Rectangle(outer.X + 24, outer.Y, outer.Width - 24, outer.Height),
            Color.White, 0.52f);
    }

    private static void DrawMatrix(FranPlayer fran)
    {
        Vector2 anchor = Main.LocalPlayer.Center - Main.screenPosition + new Vector2(-76f, -30f);
        anchor.X = MathHelper.Clamp(anchor.X, 24f, Main.screenWidth - 72f);
        anchor.Y = MathHelper.Clamp(anchor.Y, 72f, Main.screenHeight - 72f);
        int activeCount = fran.GetActiveTacticCount();
        if (activeCount == 0)
        {
            DrawOutlinedSkillIcon(anchor + new Vector2(16f), 30f, PureSwordsmanshipIcon,
                Color.White, new Color(128, 178, 202), 0.88f, 2);
            return;
        }

        for (int slot = 0; slot < fran.AvailableTacticSlots; slot++)
        {
            FranSkill skill = fran.GetEquippedTactic(slot);
            if (!FranSkillInfo.IsRealSkill(skill))
                continue;
            Vector2 center = anchor + new Vector2(slot % 2 * 34f + 16f, slot / 2 * 34f + 16f);
            int cooldown = fran.SkillCooldowns[(int)skill];
            float cooldownRatio = cooldown > 0
                ? cooldown / (float)Math.Max(1, fran.SkillCooldownMaximums[(int)skill])
                : 0f;
            DrawOutlinedSkillIcon(center, 30f, SkillIconPath(skill), Color.White,
                FranSkillInfo.Color(skill), MathHelper.Lerp(0.92f, 0.38f, cooldownRatio), 2);
        }
    }

    private static void DrawWheel(FranPlayer fran)
    {
        const float radius = 132f;
        const float iconSize = 58f;
        float time = Main.GlobalTimeWrappedHourly;

        for (int i = 0; i < FranSkillInfo.Count; i++)
        {
            FranSkill skill = (FranSkill)i;
            float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * i / FranSkillInfo.Count;
            Vector2 direction = angle.ToRotationVector2();
            Vector2 center = wheelCenter + direction * radius;
            bool unlocked = FranSkillInfo.IsUnlocked(skill);
            bool hovered = hoveredSkill == i;
            int stacks = fran.GetSkillStacks(skill);
            Color color = unlocked ? FranSkillInfo.Color(skill) : new Color(55, 62, 70);

            DrawLine(wheelCenter + direction * 52f, wheelCenter + direction * (radius - 38f),
                color * (hovered ? 0.82f : 0.22f), hovered ? 3f : 1f);
            int cooldown = fran.SkillCooldowns[i];
            float cooldownRatio = cooldown > 0
                ? cooldown / (float)Math.Max(1, fran.SkillCooldownMaximums[i])
                : 0f;
            float drawSize = hovered ? iconSize + 8f : stacks > 0 ? iconSize + 3f : iconSize;
            DrawOutlinedSkillIcon(center, drawSize, SkillIconPath(skill),
                unlocked ? Color.White : new Color(105, 111, 120),
                stacks > 0 ? Color.Lerp(color, Color.White, 0.32f) : color,
                (hovered ? 1f : stacks > 0 ? 0.98f : unlocked ? 0.82f : 0.46f) *
                MathHelper.Lerp(1f, 0.38f, cooldownRatio), hovered ? 4 : 3);
            if (stacks > 0)
                DrawStackBadge(center + new Vector2(drawSize * 0.31f, drawSize * 0.25f), stacks, color);
        }

        DrawOutlinedSkillIcon(wheelCenter, 64f, PureSwordsmanshipIcon, Color.White,
            fran.HasSelectedSkill ? new Color(92, 142, 165) : new Color(210, 232, 242), 0.96f, 3);

        for (int i = 0; i < 32; i++)
        {
            float angle = MathHelper.TwoPi * i / 32f + time * 0.08f;
            Vector2 a = wheelCenter + angle.ToRotationVector2() * 68f;
            Vector2 b = wheelCenter + (angle + MathHelper.TwoPi / 32f * 0.62f).ToRotationVector2() * 68f;
            DrawLine(a, b, new Color(70, 165, 205) * 0.48f, 2f);
        }

        string slotText = Language.GetTextValue("Mods.Fran.UI.TacticSlots",
            fran.GetActiveTacticCount(), fran.AvailableTacticSlots);
        DrawCentered(slotText, new Rectangle((int)wheelCenter.X - 170, (int)wheelCenter.Y + 37, 340, 32),
            new Color(205, 229, 239), 0.68f);
        DrawCentered(Language.GetTextValue("Mods.Fran.UI.TacticControls"),
            new Rectangle((int)wheelCenter.X - 220, (int)wheelCenter.Y + 68, 440, 28),
            new Color(145, 176, 191), 0.56f);

        for (int slot = 0; slot < fran.AvailableTacticSlots; slot++)
        {
            Vector2 center = EquippedSlotCenter(fran.AvailableTacticSlots, slot);
            Rectangle slotRect = new((int)center.X - 29, (int)center.Y - 29, 58, 58);
            FranSkill skill = fran.GetEquippedTactic(slot);
            bool hovered = hoveredEquippedSlot == slot;
            Color frame = FranSkillInfo.IsRealSkill(skill) ? FranSkillInfo.Color(skill) : new Color(73, 112, 130);
            DrawRect(slotRect, new Color(4, 9, 15, hovered ? 230 : 188));
            DrawBorder(slotRect, hovered ? Color.Lerp(frame, Color.White, 0.44f) : frame * 0.72f,
                hovered ? 3 : 2);
            if (FranSkillInfo.IsRealSkill(skill))
                DrawOutlinedSkillIcon(center, hovered ? 52f : 48f, SkillIconPath(skill), Color.White,
                    frame, 1f, hovered ? 3 : 2);
            else
                DrawCentered((slot + 1).ToString(), slotRect, new Color(90, 125, 141), 0.62f);
        }

        FranSkill detailSkill = hoveredSkill >= 0 ? (FranSkill)hoveredSkill : hoveredEquippedSlot >= 0
            ? fran.GetEquippedTactic(hoveredEquippedSlot) : FranSkill.None;
        Rectangle descriptionPanel = new((int)wheelCenter.X - 300, (int)wheelCenter.Y + 228, 600, 116);
        DrawRect(descriptionPanel, new Color(5, 10, 16, 230));
        Color panelColor = FranSkillInfo.IsRealSkill(detailSkill)
            ? FranSkillInfo.Color(detailSkill) : new Color(105, 145, 164);
        DrawBorder(descriptionPanel, panelColor * 0.78f, 2);
        if (FranSkillInfo.IsRealSkill(detailSkill))
        {
            bool unlocked = FranSkillInfo.IsUnlocked(detailSkill);
            int stacks = fran.GetSkillStacks(detailSkill);
            DrawCentered(FranSkillInfo.Name(detailSkill),
                new Rectangle(descriptionPanel.X + 12, descriptionPanel.Y + 5,
                    descriptionPanel.Width - 24, 27), unlocked ? panelColor : new Color(160, 160, 165), 0.78f);
            string description = unlocked
                ? Language.GetTextValue("Mods.Fran.UI.TacticLayerDetails", stacks,
                    FranSkillInfo.FirstLayerText(detailSkill), FranSkillInfo.AdditionalLayerText(detailSkill))
                : FranSkillInfo.UnlockText(detailSkill);
            DrawWrappedCentered(description, new Rectangle(descriptionPanel.X + 14, descriptionPanel.Y + 34,
                descriptionPanel.Width - 28, descriptionPanel.Height - 40),
                unlocked ? Color.White : new Color(185, 185, 190), 0.66f);
        }
        else
        {
            DrawCentered(Language.GetTextValue("Mods.Fran.UI.PureSwordsmanship"),
                new Rectangle(descriptionPanel.X + 12, descriptionPanel.Y + 5,
                    descriptionPanel.Width - 24, 27), new Color(220, 235, 242), 0.78f);
            DrawWrappedCentered(Language.GetTextValue("Mods.Fran.UI.PureSwordsmanshipDescription"),
                new Rectangle(descriptionPanel.X + 14, descriptionPanel.Y + 34,
                    descriptionPanel.Width - 28, descriptionPanel.Height - 40), Color.White, 0.66f);
        }
    }

    private static int FindHoveredSkill(Vector2 mouse)
    {
        for (int i = 0; i < FranSkillInfo.Count; i++)
        {
            float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * i / FranSkillInfo.Count;
            Vector2 center = wheelCenter + angle.ToRotationVector2() * 132f;
            if (Vector2.DistanceSquared(mouse, center) <= 38f * 38f)
                return i;
        }
        return -1;
    }

    private static int FindHoveredEquippedSlot(FranPlayer fran, Vector2 mouse)
    {
        for (int slot = 0; slot < fran.AvailableTacticSlots; slot++)
            if (Vector2.DistanceSquared(mouse, EquippedSlotCenter(fran.AvailableTacticSlots, slot)) <= 31f * 31f)
                return slot;
        return -1;
    }

    private static Vector2 EquippedSlotCenter(int slotCount, int slot)
    {
        const float spacing = 70f;
        return wheelCenter + new Vector2((slot - (slotCount - 1) * 0.5f) * spacing, 190f);
    }

    private static string SkillIconPath(FranSkill skill) =>
        FranSkillInfo.IsRealSkill(skill) ? SkillIcons[(int)skill] : PureSwordsmanshipIcon;

    private static void DrawSkillIcon(Vector2 center, float size, string texturePath, Color tint,
        float opacity)
    {
        Texture2D texture = ModContent.Request<Texture2D>(texturePath).Value;
        int pixels = Math.Max(1, (int)MathF.Round(size * SkillIconDrawScale));
        Rectangle destination = new((int)MathF.Round(center.X - pixels * 0.5f),
            (int)MathF.Round(center.Y - pixels * 0.5f), pixels, pixels);
        Main.spriteBatch.Draw(texture, destination, tint * MathHelper.Clamp(opacity, 0f, 1f));
    }

    private static void DrawOutlinedSkillIcon(Vector2 center, float size, string texturePath, Color tint,
        Color outline, float opacity, int outlineThickness)
    {
        Texture2D texture = ModContent.Request<Texture2D>(texturePath).Value;
        int pixels = Math.Max(1, (int)MathF.Round(size * SkillIconDrawScale));
        Rectangle destination = new((int)MathF.Round(center.X - pixels * 0.5f),
            (int)MathF.Round(center.Y - pixels * 0.5f), pixels, pixels);
        float alpha = MathHelper.Clamp(opacity, 0f, 1f);
        int thickness = Math.Max(1, outlineThickness);
        for (int x = -thickness; x <= thickness; x++)
        for (int y = -thickness; y <= thickness; y++)
        {
            if (x == 0 && y == 0 || x * x + y * y > thickness * thickness + 1)
                continue;
            Rectangle shifted = destination;
            shifted.Offset(x, y);
            Main.spriteBatch.Draw(texture, shifted, outline * (alpha * 0.92f));
        }
        Main.spriteBatch.Draw(texture, destination, tint * alpha);
    }

    private static void DrawStackBadge(Vector2 center, int stacks, Color color)
    {
        string text = $"×{stacks}";
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * 0.56f;
        Utils.DrawBorderString(Main.spriteBatch, text, center - size * 0.5f,
            Color.Lerp(color, Color.White, 0.52f), 0.56f);
    }

    private static void DrawCentered(string text, Rectangle area, Color color, float scale)
    {
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
        float actualScale = Math.Min(scale, area.Width / Math.Max(1f, size.X));
        Vector2 pos = new(area.Center.X - size.X * actualScale * 0.5f, area.Center.Y - size.Y * actualScale * 0.5f);
        Utils.DrawBorderString(Main.spriteBatch, text, pos, color, actualScale);
    }

    private static void DrawWrappedCentered(string text, Rectangle area, Color color, float scale)
    {
        List<string> lines = WrapText(text, area.Width, scale);
        float lineHeight = FontAssets.MouseText.Value.MeasureString("国").Y * scale;
        float totalHeight = lineHeight * lines.Count;
        float y = area.Center.Y - totalHeight * 0.5f;
        foreach (string line in lines)
        {
            Vector2 size = FontAssets.MouseText.Value.MeasureString(line) * scale;
            Vector2 position = new(area.Center.X - size.X * 0.5f, y);
            Utils.DrawBorderString(Main.spriteBatch, line, position, color, scale);
            y += lineHeight;
        }
    }

    private static List<string> WrapText(string text, float maxWidth, float scale)
    {
        List<string> lines = new();
        string remaining = text.Trim();
        while (remaining.Length > 0)
        {
            int bestLength = 1;
            int lastWhitespace = -1;
            for (int i = 0; i < remaining.Length; i++)
            {
                if (char.IsWhiteSpace(remaining[i]))
                    lastWhitespace = i;
                string candidate = remaining[..(i + 1)];
                if (FontAssets.MouseText.Value.MeasureString(candidate).X * scale <= maxWidth)
                {
                    bestLength = i + 1;
                    continue;
                }
                if (lastWhitespace > 0)
                    bestLength = lastWhitespace;
                break;
            }

            string line = remaining[..bestLength].Trim();
            if (line.Length == 0)
                line = remaining[..1];
            lines.Add(line);
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

    private static void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        Vector2 edge = end - start;
        if (edge.LengthSquared() < 0.1f)
            return;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, start, new Rectangle(0, 0, 1, 1), color,
            edge.ToRotation(), new Vector2(0f, 0.5f), new Vector2(edge.Length(), thickness), SpriteEffects.None, 0f);
    }
}
