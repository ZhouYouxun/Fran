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
    private static bool wheelOpen;
    private static Vector2 wheelCenter;
    private static int hoveredSkill = -1;
    private static int previousHovered = -2;
    private static bool previousWheelHeld;
    private static bool draggingUltimateGauge;
    private static Vector2 ultimateGaugeDragOffset;

    public override void UpdateUI(GameTime gameTime)
    {
        bool wheelHeld = Main.mouseRight || FranModMain.SkillWheelKey.Current;
        bool wheelPressed = wheelHeld && !previousWheelHeld;
        bool wheelReleased = !wheelHeld && previousWheelHeld;
        previousWheelHeld = wheelHeld;

        if (Main.gameMenu || Main.LocalPlayer is null)
        {
            wheelOpen = false;
            return;
        }

        Player player = Main.LocalPlayer;
        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        UpdateUltimateGaugeDrag(player, fran);
        bool allowed = !Main.playerInventory && !player.dead && player.HeldItem.type == ModContent.ItemType<Teacher>();
        if (!allowed)
        {
            wheelOpen = false;
            return;
        }

        if (wheelPressed)
        {
            wheelOpen = true;
            float minX = Math.Min(210f, Main.screenWidth * 0.5f);
            float maxX = Math.Max(minX, Main.screenWidth - minX);
            float minY = Math.Min(190f, Main.screenHeight * 0.45f);
            float maxY = Math.Max(minY, Main.screenHeight - 285f);
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
            hoveredSkill = HoveredIndex(Main.MouseScreen - wheelCenter);
            if (hoveredSkill != previousHovered)
            {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.28f, Pitch = 0.16f });
                previousHovered = hoveredSkill;
            }

            if (wheelReleased)
            {
                if (hoveredSkill >= 0)
                    fran.SelectSkill((FranSkill)hoveredSkill);
                else
                    fran.SelectSkill(FranSkill.None);
                wheelOpen = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
            }
        }
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

        if (fran.FranMode && ModContent.GetInstance<FranClientConfig>().ShowUltimateGauge)
            DrawUltimateGauge(fran);
        DrawMatrix(fran);
        if (wheelOpen)
            DrawWheel(fran);
        return true;
    }

    private static void UpdateUltimateGaugeDrag(Player player, FranPlayer fran)
    {
        if (!fran.FranMode || !ModContent.GetInstance<FranClientConfig>().ShowUltimateGauge ||
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

        Rectangle track = new(outer.X + 7, outer.Y + 7, outer.Width - 14, outer.Height - 14);
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
        DrawCentered(text, outer, Color.White, 0.52f);
    }

    private static void DrawMatrix(FranPlayer fran)
    {
        Vector2 anchor = Main.LocalPlayer.Center - Main.screenPosition + new Vector2(-78f, -26f);
        anchor.X = MathHelper.Clamp(anchor.X, 24f, Main.screenWidth - 74f);
        anchor.Y = MathHelper.Clamp(anchor.Y, 72f, Main.screenHeight - 70f);
        int selected = fran.HasSelectedSkill ? (int)fran.SelectedSkill : -1;
        float time = Main.GlobalTimeWrappedHourly;

        // The matrix is one small instrument rather than nine unrelated color chips. A subdued
        // lattice ties the outer frames together while leaving the colored cores readable.
        Color lattice = new Color(83, 139, 166) * 0.20f;
        for (int row = 0; row < 3; row++)
        {
            DrawLine(anchor + new Vector2(0f, row * 20f), anchor + new Vector2(40f, row * 20f), lattice, 1f);
            DrawLine(anchor + new Vector2(row * 20f, 0f), anchor + new Vector2(row * 20f, 40f), lattice, 1f);
        }

        (int Row, int Col)[] slots =
        {
            (0, 0), (0, 1), (0, 2), (1, 2),
            (2, 2), (2, 1), (2, 0), (1, 0)
        };
        for (int index = 0; index < FranSkillInfo.Count; index++)
        {
            FranSkill skill = (FranSkill)index;
            Vector2 center = anchor + new Vector2(slots[index].Col * 20f, slots[index].Row * 20f);
            bool unlocked = FranSkillInfo.IsUnlocked(skill);
            bool active = index == selected;
            Color color = unlocked ? FranSkillInfo.Color(skill) : new Color(42, 48, 56);
            int cooldown = fran.SkillCooldowns[index];
            float cooldownRatio = cooldown > 0
                ? cooldown / (float)Math.Max(1, fran.SkillCooldownMaximums[index])
                : 0f;
            DrawLayeredSkillNode(center, active ? 15f : 13f, color, unlocked, active,
                false, cooldownRatio, true, time + index * 0.37f);
        }

        Color coreColor = fran.HasSelectedSkill ? FranSkillInfo.Color(fran.SelectedSkill) : new Color(155, 175, 188);
        DrawLayeredSkillNode(anchor + new Vector2(20f, 20f), 11f, coreColor, true,
            fran.HasSelectedSkill, false, 0f, true, time * 0.7f);
    }

    private static void DrawWheel(FranPlayer fran)
    {
        const float radius = 154f;
        const float slotSize = 31f;
        float time = Main.GlobalTimeWrappedHourly;

        for (int i = 0; i < FranSkillInfo.Count; i++)
        {
            FranSkill skill = (FranSkill)i;
            float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * i / FranSkillInfo.Count;
            Vector2 direction = angle.ToRotationVector2();
            Vector2 center = wheelCenter + direction * radius;
            bool unlocked = FranSkillInfo.IsUnlocked(skill);
            bool hovered = hoveredSkill == i;
            bool selected = fran.SelectedSkill == skill;
            Color color = unlocked ? FranSkillInfo.Color(skill) : new Color(55, 62, 70);

            DrawLine(wheelCenter + direction * 48f, wheelCenter + direction * (radius - slotSize),
                color * (hovered ? 0.82f : 0.22f), hovered ? 3f : 1f);
            int cooldown = fran.SkillCooldowns[i];
            float cooldownRatio = cooldown > 0
                ? cooldown / (float)Math.Max(1, fran.SkillCooldownMaximums[i])
                : 0f;
            DrawLayeredSkillNode(center, hovered ? 32f : selected ? 29f : 27f, color,
                unlocked, selected, hovered, cooldownRatio, false, time + i * 0.41f);
        }

        bool pureSwordHovered = hoveredSkill < 0;
        bool pureSwordSelected = !fran.HasSelectedSkill;
        DrawLayeredSkillNode(wheelCenter, pureSwordHovered ? 33f : 29f,
            new Color(185, 205, 218), true, pureSwordSelected, pureSwordHovered,
            0f, false, time);

        for (int i = 0; i < 32; i++)
        {
            float angle = MathHelper.TwoPi * i / 32f + time * 0.08f;
            Vector2 a = wheelCenter + angle.ToRotationVector2() * 68f;
            Vector2 b = wheelCenter + (angle + MathHelper.TwoPi / 32f * 0.62f).ToRotationVector2() * 68f;
            DrawLine(a, b, new Color(70, 165, 205) * 0.48f, 2f);
        }

        if (hoveredSkill >= 0)
        {
            FranSkill hovered = (FranSkill)hoveredSkill;
            bool unlocked = FranSkillInfo.IsUnlocked(hovered);
            string name = FranSkillInfo.Name(hovered);
            string description = unlocked ? FranSkillInfo.Description(hovered) : FranSkillInfo.UnlockText(hovered);
            DrawCentered(name, new Rectangle((int)wheelCenter.X - 150, (int)wheelCenter.Y - 34, 300, 30),
                unlocked ? FranSkillInfo.Color(hovered) : new Color(160, 160, 165), 0.72f);
            Rectangle descriptionPanel = new((int)wheelCenter.X - 240, (int)wheelCenter.Y + 194, 480, 78);
            DrawRect(descriptionPanel, new Color(5, 10, 16, 225));
            DrawBorder(descriptionPanel, unlocked ? FranSkillInfo.Color(hovered) * 0.72f : new Color(90, 95, 105), 2);
            DrawWrappedCentered(description, new Rectangle(descriptionPanel.X + 12, descriptionPanel.Y + 7,
                descriptionPanel.Width - 24, descriptionPanel.Height - 14), unlocked ? Color.White : new Color(185, 185, 190), 0.50f);
        }
        else
        {
            DrawCentered(Language.GetTextValue("Mods.Fran.UI.PureSwordsmanship"),
                new Rectangle((int)wheelCenter.X - 150, (int)wheelCenter.Y - 34, 300, 30),
                new Color(220, 235, 242), 0.72f);
            Rectangle descriptionPanel = new((int)wheelCenter.X - 240, (int)wheelCenter.Y + 194, 480, 78);
            DrawRect(descriptionPanel, new Color(5, 10, 16, 225));
            DrawBorder(descriptionPanel, new Color(120, 150, 165), 2);
            DrawWrappedCentered(Language.GetTextValue("Mods.Fran.UI.PureSwordsmanshipDescription"),
                new Rectangle(descriptionPanel.X + 12, descriptionPanel.Y + 7,
                    descriptionPanel.Width - 24, descriptionPanel.Height - 14), Color.White, 0.50f);
        }
    }

    private static int HoveredIndex(Vector2 offset)
    {
        if (offset.LengthSquared() < 55f * 55f)
            return -1;
        Vector2 direction = offset.SafeNormalize(-Vector2.UnitY);
        int best = 0;
        float bestDot = -2f;
        for (int i = 0; i < FranSkillInfo.Count; i++)
        {
            Vector2 slotDirection = (-MathHelper.PiOver2 + MathHelper.TwoPi * i / FranSkillInfo.Count).ToRotationVector2();
            float dot = Vector2.Dot(direction, slotDirection);
            if (dot > bestDot) { bestDot = dot; best = i; }
        }
        return best;
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

    private static void DrawDiamond(Vector2 center, float size, Color fill, Color border, float borderThickness)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        if (borderThickness > 0f && border != Color.Transparent)
            Main.spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), border, MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(size), SpriteEffects.None, 0f);
        float innerSize = Math.Max(0f, size - borderThickness);
        if (innerSize > 0f && fill != Color.Transparent)
            Main.spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), fill, MathHelper.PiOver4,
                new Vector2(0.5f), new Vector2(innerSize), SpriteEffects.None, 0f);
    }

    private static void DrawLayeredSkillNode(Vector2 center, float size, Color color, bool unlocked,
        bool selected, bool hovered, float cooldownRatio, bool compact, float phase)
    {
        cooldownRatio = MathHelper.Clamp(cooldownRatio, 0f, 1f);
        float pulse = 0.5f + MathF.Sin(phase * 3.2f) * 0.5f;
        float outerRadius = size * 0.72f + (hovered ? pulse * 1.2f : selected ? pulse * 0.55f : 0f);
        float frameThickness = compact ? 1f : hovered || selected ? 2f : 1.35f;
        Color disabled = new(58, 66, 74);
        Color frameColor = unlocked ? color : disabled;
        frameColor *= hovered ? 0.98f : selected ? 0.86f : compact ? 0.62f : 0.68f;

        // Layer one: a dark carrier plate and a detached four-part frame. The empty channel around
        // the core is intentional—it gives the node a construction instead of reading as one color chip.
        DrawDiamond(center, size + (compact ? 1f : 3f), new Color(4, 9, 14) * (compact ? 0.72f : 0.86f),
            Color.Transparent, 0f);
        DrawSegmentedDiamondFrame(center, outerRadius + (compact ? 0.7f : 1.4f),
            new Color(2, 5, 9) * 0.90f, frameThickness + (compact ? 1f : 1.5f));
        DrawSegmentedDiamondFrame(center, outerRadius, frameColor, frameThickness);

        // Layer two: a compact energy core. Cooldown compresses and darkens only this layer, so the
        // player can still identify the skill from the outer frame while seeing that its power is sealed.
        float coreSize = size * (compact ? 0.46f : 0.43f) * MathHelper.Lerp(1f, 0.72f, cooldownRatio);
        Color coreColor = unlocked ? Color.Lerp(color, new Color(18, 24, 32), cooldownRatio * 0.72f) : disabled;
        float coreOpacity = unlocked ? MathHelper.Lerp(0.94f, 0.46f, cooldownRatio) : 0.52f;
        Color coreBorder = hovered ? Color.White : selected
            ? Color.Lerp(color, Color.White, 0.72f)
            : Color.Lerp(frameColor, Color.White, compact ? 0.20f : 0.12f);
        DrawDiamond(center, coreSize + (compact ? 2.1f : 3.2f), new Color(3, 8, 13) * 0.94f,
            coreBorder * (hovered || selected ? 0.94f : 0.54f), compact ? 1f : 1.4f);
        DrawDiamond(center, coreSize, coreColor * coreOpacity, Color.Transparent, 0f);

        if (hovered || selected)
        {
            float glintSize = Math.Max(1.2f, coreSize * (compact ? 0.24f : 0.20f));
            DrawDiamond(center, glintSize, Color.White * (hovered ? 0.92f : 0.68f),
                Color.Transparent, 0f);
        }

        // Hover/selection gains four tiny terminals instead of another filled slab.
        if (!compact && (hovered || selected))
        {
            float terminalDistance = outerRadius + 3.2f + pulse * 1.4f;
            Color terminalColor = hovered ? Color.White : Color.Lerp(color, Color.White, 0.42f);
            for (int i = 0; i < 4; i++)
            {
                Vector2 terminal = center + (MathHelper.PiOver2 * i).ToRotationVector2() * terminalDistance;
                DrawDiamond(terminal, hovered ? 3.4f : 2.7f, terminalColor * (0.58f + pulse * 0.28f),
                    Color.Transparent, 0f);
            }
        }
    }

    private static void DrawSegmentedDiamondFrame(Vector2 center, float radius, Color color, float thickness)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(radius) ||
            !float.IsFinite(thickness) || radius <= 0f || thickness <= 0f)
            return;

        Vector2[] points =
        [
            center - Vector2.UnitY * radius,
            center + Vector2.UnitX * radius,
            center + Vector2.UnitY * radius,
            center - Vector2.UnitX * radius
        ];
        for (int i = 0; i < 4; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[(i + 1) % 4];
            DrawLine(Vector2.Lerp(start, end, 0.08f), Vector2.Lerp(start, end, 0.42f), color, thickness);
            DrawLine(Vector2.Lerp(start, end, 0.58f), Vector2.Lerp(start, end, 0.92f), color, thickness);
        }
    }

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
