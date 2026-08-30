using System;
using System.Collections.Generic;
using FranMod.Content.Players;
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

public sealed class FranEvolutionUISystem : ModSystem
{
    private const int ResetHoldDuration = 36;
    private static readonly Vector2[] NormalizedNodePositions =
    [
        new(0.40f, 0.62f), new(0.32f, 0.49f), new(0.24f, 0.36f), new(0.17f, 0.22f),
        new(0.60f, 0.62f), new(0.68f, 0.49f), new(0.76f, 0.36f), new(0.83f, 0.22f),
        new(0.50f, 0.59f), new(0.50f, 0.45f), new(0.50f, 0.31f), new(0.50f, 0.17f)
    ];

    private static bool open;
    private static int hoveredNode = -1;
    private static int previousHoveredNode = -2;
    private static int resetHoldTicks;
    private static bool resetTriggered;

    public static bool IsOpen => open;

    public override void Unload()
    {
        open = false;
        hoveredNode = -1;
        previousHoveredNode = -2;
        resetHoldTicks = 0;
        resetTriggered = false;
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (Main.gameMenu || Main.LocalPlayer is null)
        {
            Close(silent: true);
            return;
        }

        Player player = Main.LocalPlayer;
        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        bool allowed = !player.dead && (fran.FranMode || fran.HasTeacher);
        Rectangle entryButton = GetEntryButton();
        bool entryHovered = allowed && !open && entryButton.Contains(Main.MouseScreen.ToPoint());

        if (entryHovered)
        {
            Main.blockMouse = true;
            player.mouseInterface = true;
        }

        if (allowed && (FranModMain.EvolutionKey.JustPressed ||
            entryHovered && Main.mouseLeft && Main.mouseLeftRelease))
        {
            if (open)
                Close(silent: false);
            else
                Open();
        }

        if (!allowed)
        {
            Close(silent: true);
            return;
        }
        if (!open)
            return;

        Main.blockMouse = true;
        player.mouseInterface = true;
        PlayerInput.LockVanillaMouseScroll("FranMagicStoneCorridor");

        Rectangle panel = GetPanel();
        Vector2[] centers = GetNodeCenters(panel);
        hoveredNode = -1;
        for (int i = 0; i < centers.Length; i++)
            if (Vector2.DistanceSquared(Main.MouseScreen, centers[i]) <= 24f * 24f)
            {
                hoveredNode = i;
                break;
            }

        if (hoveredNode != previousHoveredNode)
        {
            if (hoveredNode >= 0)
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.27f, Pitch = 0.18f });
            previousHoveredNode = hoveredNode;
        }

        FranEvolutionPlayer evolution = player.GetModPlayer<FranEvolutionPlayer>();
        Rectangle resetButton = GetResetButton(panel);
        bool resetHovered = resetButton.Contains(Main.MouseScreen.ToPoint());
        if (resetHovered && Main.mouseLeft)
        {
            if (!resetTriggered && evolution.NodeMask != 0 && evolution.CanAdjustEvolution)
            {
                resetHoldTicks++;
                if (resetHoldTicks >= ResetHoldDuration)
                {
                    resetTriggered = true;
                    evolution.RequestReset();
                }
            }
        }
        else
        {
            resetHoldTicks = 0;
            if (!Main.mouseLeft)
                resetTriggered = false;
        }

        if (hoveredNode >= 0 && !resetHovered && Main.mouseLeft && Main.mouseLeftRelease)
        {
            FranEvolutionNode node = (FranEvolutionNode)hoveredNode;
            FranEvolutionUnlockFailure failure = evolution.GetUnlockFailure(node);
            if (failure == FranEvolutionUnlockFailure.None)
                evolution.RequestUnlock(node);
            else if (failure != FranEvolutionUnlockFailure.AlreadyUnlocked)
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.42f, Pitch = -0.42f });
        }
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (index < 0)
            index = layers.Count;
        layers.Insert(index, new LegacyGameInterfaceLayer("Fran: Magic Stone Corridor", DrawInterface,
            InterfaceScaleType.UI));
    }

    private static bool DrawInterface()
    {
        if (Main.gameMenu || Main.LocalPlayer is null)
            return true;
        FranPlayer fran = Main.LocalPlayer.GetModPlayer<FranPlayer>();
        if (!fran.FranMode && !fran.HasTeacher)
            return true;

        if (!open)
        {
            DrawEntryButton();
            return true;
        }

        FranEvolutionPlayer evolution = Main.LocalPlayer.GetModPlayer<FranEvolutionPlayer>();
        Rectangle panel = GetPanel();
        Vector2[] centers = GetNodeCenters(panel);
        DrawBackdrop(panel);
        DrawHeader(panel, evolution);
        DrawConnections(panel, centers, evolution);
        DrawTeacherCore(panel, evolution);
        DrawNodes(centers, evolution);
        DrawDescription(panel, evolution);
        DrawResetButton(panel, evolution);
        return true;
    }

    private static void Open()
    {
        open = true;
        hoveredNode = -1;
        previousHoveredNode = -2;
        resetHoldTicks = 0;
        resetTriggered = false;
        SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.62f, Pitch = 0.05f });
    }

    private static void Close(bool silent)
    {
        if (open && !silent)
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.58f });
        open = false;
        hoveredNode = -1;
        previousHoveredNode = -2;
        resetHoldTicks = 0;
        resetTriggered = false;
    }

    private static Rectangle GetPanel()
    {
        int width = Math.Min(860, Math.Max(560, Main.screenWidth - 54));
        int height = Math.Min(620, Math.Max(460, Main.screenHeight - 54));
        return new Rectangle((Main.screenWidth - width) / 2, (Main.screenHeight - height) / 2, width, height);
    }

    private static Rectangle GetEntryButton()
    {
        Vector2 anchor = Main.LocalPlayer.Center - Main.screenPosition + new Vector2(-78f, 30f);
        anchor.X = MathHelper.Clamp(anchor.X, 18f, Main.screenWidth - 50f);
        anchor.Y = MathHelper.Clamp(anchor.Y, 118f, Main.screenHeight - 50f);
        return new Rectangle((int)anchor.X, (int)anchor.Y, 30, 30);
    }

    private static Rectangle GetResetButton(Rectangle panel) =>
        new(panel.Right - 190, panel.Bottom - 44, 162, 28);

    private static Vector2[] GetNodeCenters(Rectangle panel)
    {
        Vector2[] result = new Vector2[FranEvolutionInfo.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = panel.TopLeft() + NormalizedNodePositions[i] * panel.Size();
        return result;
    }

    private static void DrawEntryButton()
    {
        Rectangle button = GetEntryButton();
        bool hovered = button.Contains(Main.MouseScreen.ToPoint());
        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.8f);
        DrawRect(button, new Color(5, 8, 16, hovered ? 240 : 205));
        DrawBorder(button, hovered ? new Color(205, 165, 255) : new Color(105, 88, 176), hovered ? 2 : 1);
        Vector2 center = button.Center.ToVector2();
        DrawDiamond(center, hovered ? 11f : 9f, new Color(164, 104, 236) * (0.78f + pulse * 0.18f),
            Color.White * (hovered ? 0.72f : 0.38f), 1f);
        if (hovered)
            Main.instance.MouseText(Language.GetTextValue("Mods.Fran.Evolution.OpenHint"));
    }

    private static void DrawBackdrop(Rectangle panel)
    {
        DrawRect(new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), new Color(1, 3, 8, 188));
        DrawRect(panel, new Color(4, 7, 15, 244));
        DrawBorder(panel, new Color(32, 54, 76, 245), 4);
        DrawBorder(new Rectangle(panel.X + 4, panel.Y + 4, panel.Width - 8, panel.Height - 8),
            new Color(129, 101, 208) * 0.65f, 1);

        float time = Main.GlobalTimeWrappedHourly;
        Vector2 center = panel.Center.ToVector2();
        for (int i = 0; i < 46; i++)
        {
            float x = panel.X + 18f + ((i * 83.17f) % Math.Max(1f, panel.Width - 36f));
            float y = panel.Y + 18f + ((i * 47.63f) % Math.Max(1f, panel.Height - 36f));
            float shimmer = 0.28f + 0.20f * MathF.Sin(time * 1.7f + i * 0.91f);
            DrawDiamond(new Vector2(x, y), i % 7 == 0 ? 2.4f : 1.4f,
                new Color(156, 206, 238) * shimmer, Color.Transparent, 0f);
        }

        for (int ring = 0; ring < 3; ring++)
        {
            float radius = 105f + ring * 44f;
            int segments = 44;
            for (int i = 0; i < segments; i++)
            {
                float a = MathHelper.TwoPi * i / segments + time * (ring % 2 == 0 ? 0.015f : -0.012f);
                float b = MathHelper.TwoPi * (i + 0.58f) / segments + time * (ring % 2 == 0 ? 0.015f : -0.012f);
                DrawLine(center + a.ToRotationVector2() * radius, center + b.ToRotationVector2() * radius,
                    new Color(67, 104, 138) * 0.11f, 1f);
            }
        }
    }

    private static void DrawHeader(Rectangle panel, FranEvolutionPlayer evolution)
    {
        DrawCentered(Language.GetTextValue("Mods.Fran.Evolution.Title"),
            new Rectangle(panel.X + 120, panel.Y + 15, panel.Width - 240, 30),
            new Color(226, 235, 255), 0.82f);
        string points = Language.GetTextValue("Mods.Fran.Evolution.PointSummary", evolution.AvailablePoints,
            evolution.SpentPoints, evolution.EarnedPoints, FranEvolutionInfo.CurrentStagePointCap);
        DrawCentered(points, new Rectangle(panel.X + 80, panel.Y + 46, panel.Width - 160, 24),
            new Color(184, 202, 222), 0.53f);

        DrawCentered(Language.GetTextValue("Mods.Fran.Evolution.Branches.Teacher"),
            new Rectangle(panel.X + 26, panel.Y + 74, panel.Width / 3, 24),
            FranEvolutionInfo.BranchColor(FranEvolutionBranch.Teacher), 0.57f);
        DrawCentered(Language.GetTextValue("Mods.Fran.Evolution.Branches.Bond"),
            new Rectangle(panel.Center.X - panel.Width / 6, panel.Y + 74, panel.Width / 3, 24),
            FranEvolutionInfo.BranchColor(FranEvolutionBranch.Bond), 0.57f);
        DrawCentered(Language.GetTextValue("Mods.Fran.Evolution.Branches.Fran"),
            new Rectangle(panel.Right - panel.Width / 3 - 26, panel.Y + 74, panel.Width / 3, 24),
            FranEvolutionInfo.BranchColor(FranEvolutionBranch.Fran), 0.57f);
    }

    private static void DrawConnections(Rectangle panel, Vector2[] centers, FranEvolutionPlayer evolution)
    {
        Vector2 core = panel.TopLeft() + new Vector2(0.5f, 0.735f) * panel.Size();
        DrawBranchConnection(core, centers[0], FranEvolutionNode.EdgeCalibration, evolution);
        DrawBranchConnection(core, centers[4], FranEvolutionNode.CatStep, evolution);
        DrawBranchConnection(core, centers[8], FranEvolutionNode.MindSync, evolution);

        for (int branch = 0; branch < 3; branch++)
        {
            int start = branch * 4;
            for (int tier = 1; tier < 4; tier++)
                DrawBranchConnection(centers[start + tier - 1], centers[start + tier],
                    (FranEvolutionNode)(start + tier), evolution);
        }
    }

    private static void DrawBranchConnection(Vector2 start, Vector2 end, FranEvolutionNode destination,
        FranEvolutionPlayer evolution)
    {
        FranEvolutionNodeDefinition definition = FranEvolutionInfo.Get(destination);
        bool purchased = evolution.HasNode(destination);
        bool prerequisiteMet = definition.Prerequisite == FranEvolutionNode.None ||
            evolution.HasNode(definition.Prerequisite);
        Color color = FranEvolutionInfo.BranchColor(definition.Branch);
        DrawLine(start, end, new Color(10, 17, 27) * 0.95f, 7f);
        DrawLine(start, end, color * (purchased ? 0.88f : prerequisiteMet ? 0.33f : 0.13f),
            purchased ? 3f : 1.5f);
        if (purchased)
        {
            float flow = (Main.GlobalTimeWrappedHourly * 0.65f + (int)destination * 0.17f) % 1f;
            DrawDiamond(Vector2.Lerp(start, end, flow), 4.2f, Color.White * 0.82f,
                Color.Transparent, 0f);
        }
    }

    private static void DrawTeacherCore(Rectangle panel, FranEvolutionPlayer evolution)
    {
        Vector2 center = panel.TopLeft() + new Vector2(0.5f, 0.735f) * panel.Size();
        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.6f);
        int orbitCount = Math.Min(FranEvolutionInfo.MaximumSpendablePoints, evolution.EarnedPoints);
        for (int i = 0; i < orbitCount; i++)
        {
            float angle = MathHelper.TwoPi * i / Math.Max(1, orbitCount) + Main.GlobalTimeWrappedHourly * 0.12f;
            Vector2 position = center + angle.ToRotationVector2() * (55f + (i % 2) * 7f);
            DrawDiamond(position, 3.7f, new Color(184, 126, 244) * 0.64f, Color.White * 0.32f, 0.7f);
        }

        DrawDiamond(center, 37f + pulse * 2f, new Color(12, 14, 28) * 0.95f,
            new Color(132, 108, 220) * 0.72f, 2f);
        DrawLine(center + new Vector2(0f, -25f), center + new Vector2(0f, 24f),
            new Color(220, 230, 242), 4f);
        DrawLine(center + new Vector2(-12f, 12f), center + new Vector2(12f, 12f),
            new Color(244, 194, 80), 3f);
        DrawLine(center + new Vector2(0f, -24f), center + new Vector2(-6f, -14f),
            new Color(184, 220, 240), 2f);
        DrawLine(center + new Vector2(0f, -24f), center + new Vector2(6f, -14f),
            new Color(184, 220, 240), 2f);
    }

    private static void DrawNodes(Vector2[] centers, FranEvolutionPlayer evolution)
    {
        float time = Main.GlobalTimeWrappedHourly;
        for (int i = 0; i < FranEvolutionInfo.Count; i++)
        {
            FranEvolutionNode node = (FranEvolutionNode)i;
            FranEvolutionNodeDefinition definition = FranEvolutionInfo.Get(node);
            FranEvolutionUnlockFailure failure = evolution.GetUnlockFailure(node);
            bool purchased = evolution.HasNode(node);
            bool hovered = hoveredNode == i;
            bool available = failure == FranEvolutionUnlockFailure.None;
            Color branch = FranEvolutionInfo.BranchColor(definition.Branch);
            float pulse = 0.5f + 0.5f * MathF.Sin(time * 4f + i * 0.61f);

            float size = hovered ? 24f : purchased ? 22f : 20f;
            Color fill = purchased ? branch * (0.74f + pulse * 0.16f)
                : available ? Color.Lerp(new Color(18, 24, 36), branch, 0.34f)
                : new Color(25, 29, 37) * 0.90f;
            Color border = hovered ? Color.White
                : purchased ? Color.Lerp(branch, Color.White, 0.42f)
                : available ? branch * 0.78f
                : new Color(70, 77, 88);

            DrawDiamond(centers[i], size + 4f, new Color(3, 6, 12) * 0.96f, border, hovered ? 2.2f : 1.4f);
            DrawDiamond(centers[i], size * 0.58f, fill, Color.Transparent, 0f);
            if (purchased)
                DrawDiamond(centers[i], 4.4f + pulse * 1.2f, Color.White * 0.90f, Color.Transparent, 0f);

            DrawCentered(definition.Cost.ToString(),
                new Rectangle((int)centers[i].X - 12, (int)centers[i].Y - 12, 24, 24),
                purchased ? new Color(8, 12, 19) : new Color(228, 233, 240), 0.47f);
        }
    }

    private static void DrawDescription(Rectangle panel, FranEvolutionPlayer evolution)
    {
        Rectangle description = new(panel.X + 24, panel.Bottom - 112, panel.Width - 238, 88);
        DrawRect(description, new Color(3, 7, 13, 232));
        DrawBorder(description, new Color(46, 75, 98) * 0.85f, 1);

        if (hoveredNode < 0)
        {
            DrawWrappedCentered(Language.GetTextValue("Mods.Fran.Evolution.Overview"),
                new Rectangle(description.X + 12, description.Y + 9, description.Width - 24,
                    description.Height - 18), new Color(201, 215, 229), 0.51f);
            return;
        }

        FranEvolutionNode node = (FranEvolutionNode)hoveredNode;
        FranEvolutionNodeDefinition definition = FranEvolutionInfo.Get(node);
        Color branch = FranEvolutionInfo.BranchColor(definition.Branch);
        string name = Language.GetTextValue($"Mods.Fran.Evolution.Nodes.{node}.DisplayName");
        string body = Language.GetTextValue($"Mods.Fran.Evolution.Nodes.{node}.Description");
        FranEvolutionUnlockFailure failure = evolution.GetUnlockFailure(node);
        string status = evolution.HasNode(node)
            ? Language.GetTextValue("Mods.Fran.Evolution.Status.Active")
            : failure == FranEvolutionUnlockFailure.StageLocked
                ? Language.GetTextValue("Mods.Fran.Evolution.Status.StageLocked", definition.RequiredStage)
                : Language.GetTextValue($"Mods.Fran.Evolution.Status.{failure}");
        DrawCentered(name, new Rectangle(description.X + 8, description.Y + 5, description.Width - 16, 22),
            branch, 0.61f);
        DrawWrappedCentered(body, new Rectangle(description.X + 12, description.Y + 27,
            description.Width - 24, 38), Color.White, 0.46f);
        DrawCentered(status, new Rectangle(description.X + 8, description.Bottom - 22, description.Width - 16, 18),
            failure == FranEvolutionUnlockFailure.None || evolution.HasNode(node) ? new Color(142, 232, 199) :
                new Color(212, 171, 148), 0.43f);
    }

    private static void DrawResetButton(Rectangle panel, FranEvolutionPlayer evolution)
    {
        Rectangle button = GetResetButton(panel);
        bool hovered = button.Contains(Main.MouseScreen.ToPoint());
        float ratio = resetTriggered ? 1f : MathHelper.Clamp(resetHoldTicks / (float)ResetHoldDuration, 0f, 1f);
        DrawRect(button, new Color(9, 12, 20, 235));
        DrawBorder(button, hovered ? new Color(221, 177, 103) : new Color(88, 78, 93), hovered ? 2 : 1);
        if (ratio > 0f)
            DrawRect(new Rectangle(button.X + 3, button.Y + 3, (int)((button.Width - 6) * ratio), button.Height - 6),
                new Color(174, 111, 84) * 0.52f);
        string text = evolution.NodeMask == 0
            ? Language.GetTextValue("Mods.Fran.Evolution.ResetEmpty")
            : !evolution.CanAdjustEvolution
                ? Language.GetTextValue("Mods.Fran.Evolution.ResetLocked")
            : Language.GetTextValue("Mods.Fran.Evolution.ResetHold");
        DrawCentered(text, button, evolution.NodeMask == 0 || !evolution.CanAdjustEvolution
            ? new Color(110, 113, 122)
            : Color.White, 0.48f);
    }

    private static void DrawCentered(string text, Rectangle area, Color color, float scale)
    {
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
        float actualScale = Math.Min(scale, area.Width / Math.Max(1f, size.X));
        Vector2 position = new(area.Center.X - size.X * actualScale * 0.5f,
            area.Center.Y - size.Y * actualScale * 0.5f);
        Utils.DrawBorderString(Main.spriteBatch, text, position, color, actualScale);
    }

    private static void DrawWrappedCentered(string text, Rectangle area, Color color, float scale)
    {
        List<string> lines = WrapText(text, area.Width, scale);
        float lineHeight = FontAssets.MouseText.Value.MeasureString("国").Y * scale;
        float y = area.Center.Y - lines.Count * lineHeight * 0.5f;
        foreach (string line in lines)
        {
            Vector2 size = FontAssets.MouseText.Value.MeasureString(line) * scale;
            Utils.DrawBorderString(Main.spriteBatch, line, new Vector2(area.Center.X - size.X * 0.5f, y),
                color, scale);
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

    private static void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        Vector2 edge = end - start;
        if (edge.LengthSquared() < 0.1f)
            return;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, start, new Rectangle(0, 0, 1, 1), color,
            edge.ToRotation(), new Vector2(0f, 0.5f), new Vector2(edge.Length(), thickness), SpriteEffects.None, 0f);
    }
}
