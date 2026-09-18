using System;
using System.Collections.Generic;
using FranMod.Content.Config;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
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
    // Player-left shorthand: three independent pixel sigils, not a magazine or item-slot strip.
    private const int TacticSigilWidth = 24;
    private const int TacticSigilPadding = 5;
    private const int TacticRuneDiameter = 11;
    private const int TacticRuneGap = 4;
    private const int TacticSigilLeftOffset = 92;
    private const int TacticSigilScreenMargin = 12;
    private const int EquippedTacticSlotSize = 58;
    private const float EquippedTacticSlotOffsetY = 190f;
    private const float WheelRadius = 92.4f;
    private const float WheelCollapsedRadius = 57f;
    private const float WheelHitRadius = 30f;
    private const float WheelFadeInSpeed = 0.30f;
    private const float WheelFadeOutSpeed = 0.22f;
    private const float NativeSkillIconSize = 40f;
    private const string SkillIconRoot = "Fran/Textures/技能组图标/";
    private const string PureSwordsmanshipIcon = SkillIconRoot + "FranSkill_09_PureSwordsmanship";
    private const string UltimateIcon = SkillIconRoot + "FranSkill_10_Ultimate_BlackHeavenlyTiger";

    private static bool wheelOpen;
    private static Vector2 wheelCenter;
    private static int hoveredSkill = -1;
    private static int hoveredEquippedSlot = -1;
    private static int previousHovered = -2;
    private static bool previousWheelHeld;
    private static bool previousLeftMouseHeld;
    private static float wheelVisibility;
    private static bool draggingUltimateGauge;
    private static bool previousGaugeMouseHeld;
    private static Vector2 ultimateGaugeDragOffset;
    private static float tacticSigilHoverFade;
    private const float WheelBodyTextScale = 0.8f;
    private static readonly List<WheelText> wheelText = new();
    private static bool collectingWheelText;
    private readonly record struct WheelText(string Text, Vector2 Position, Color Color, float Scale);

    public static bool IsWheelOpen => wheelOpen;

    public override void UpdateUI(GameTime gameTime)
    {
        bool wheelHeld = IsSkillWheelHeld();
        bool wheelPressed = wheelHeld && !previousWheelHeld;
        bool leftPressed = Main.mouseLeft && !previousLeftMouseHeld;
        bool holdToOpen = ModContent.GetInstance<FranClientConfig>().HoldSkillWheelToOpen;
        previousWheelHeld = wheelHeld;
        previousLeftMouseHeld = Main.mouseLeft;

        if (Main.gameMenu || Main.LocalPlayer is null)
        {
            wheelOpen = false;
            wheelVisibility = 0f;
            return;
        }

        Player player = Main.LocalPlayer;
        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        bool allowed = !Content.Companions.FranCompanionUI.IsOpen && !Content.Companions.FranCompanionUI.Hovering &&
            !FranEvolutionUISystem.IsOpen && !Main.playerInventory && !player.dead &&
            player.HeldItem.type == ModContent.ItemType<ShiFu>();
        if (!allowed)
        {
            if (wheelOpen)
                CloseWheel();
            AdvanceWheelAnimation();
            return;
        }

        if (holdToOpen)
        {
            if (wheelHeld)
            {
                if (!wheelOpen)
                {
                    wheelOpen = true;
                    OpenWheelAtMouse();
                }
            }
            else if (wheelOpen)
            {
                CloseWheel();
            }
        }
        else if (wheelPressed)
        {
            if (wheelOpen)
            {
                CloseWheel();
            }
            else
            {
                wheelOpen = true;
                OpenWheelAtMouse();
            }
        }

        AdvanceWheelAnimation();

        if (wheelOpen)
        {
            PlayerInput.LockVanillaMouseScroll("FranSkillWheel");
            bool buttonsReady = wheelVisibility >= 0.45f;
            hoveredSkill = buttonsReady ? FindHoveredSkill(Main.MouseScreen) : -1;
            hoveredEquippedSlot = buttonsReady ? FindHoveredEquippedSlot(fran, Main.MouseScreen) : -1;
            int hoverToken = hoveredSkill >= 0 ? hoveredSkill : hoveredEquippedSlot >= 0
                ? 100 + hoveredEquippedSlot : -1;
            if (hoverToken != previousHovered)
            {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.28f, Pitch = 0.16f });
                previousHovered = hoverToken;
            }

            if (leftPressed)
            {
                if (hoveredSkill >= 0)
                {
                    if (fran.TryEquipTactic((FranSkill)hoveredSkill))
                        SoundEngine.PlaySound(SoundID.Item37);
                }
                else if (hoveredEquippedSlot >= 0)
                {
                    if (fran.TryUnequipTactic(hoveredEquippedSlot))
                        SoundEngine.PlaySound(SoundID.Item37);
                }
            }

            Main.blockMouse = true;
            player.mouseInterface = true;
        }
    }

    private static bool IsSkillWheelHeld()
    {
        bool keyBound = IsSkillWheelKeyBound();
        return keyBound ? FranModMain.SkillWheelKey.Current : Main.mouseRight;
    }

    private static bool IsSkillWheelKeyBound()
    {
        if (FranModMain.SkillWheelKey is null)
            return false;

        try
        {
            foreach (string key in FranModMain.SkillWheelKey.GetAssignedKeys())
            {
                if (string.IsNullOrWhiteSpace(key) || key.Equals("None", StringComparison.OrdinalIgnoreCase))
                    continue;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static void OpenWheelAtMouse()
    {
        float minX = Math.Min(300f, Main.screenWidth * 0.5f);
        float maxX = Math.Max(minX, Main.screenWidth - minX);
        float minY = Math.Min(180f, Main.screenHeight * 0.40f);
        int[] rows = GetWheelDescriptionRowHeights();
        float maxY = Math.Max(122f, Main.screenHeight - 228f - GetWheelDescriptionHeight(rows) - 12f);
        minY = Math.Min(minY, maxY);
        wheelCenter = new Vector2(MathHelper.Clamp(Main.MouseScreen.X, minX, maxX),
            MathHelper.Clamp(Main.MouseScreen.Y, minY, maxY));
        previousHovered = -2;
        SoundEngine.PlaySound(SoundID.MenuOpen);
    }

    private static void CloseWheel()
    {
        wheelOpen = false;
        hoveredSkill = -1;
        hoveredEquippedSlot = -1;
        previousHovered = -2;
        SoundEngine.PlaySound(SoundID.MenuClose);
    }

    private static void AdvanceWheelAnimation()
    {
        float speed = wheelOpen ? WheelFadeInSpeed : WheelFadeOutSpeed;
        wheelVisibility = MathHelper.Clamp(wheelVisibility + (wheelOpen ? speed : -speed), 0f, 1f);
    }

    private static float WheelAnimationEase =>
        wheelVisibility * wheelVisibility * (3f - 2f * wheelVisibility);

    private static float AnimatedWheelRadius =>
        MathHelper.Lerp(WheelCollapsedRadius, WheelRadius, WheelAnimationEase);

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (index < 0)
            index = layers.Count;
        layers.Insert(index, new LegacyGameInterfaceLayer("Fran: Tactic Sigils",
            DrawTacticSigilHud, InterfaceScaleType.Game));
        layers.Insert(index + 1, new LegacyGameInterfaceLayer("Fran: Character HUD", DrawHud,
            InterfaceScaleType.UI));
    }

    private static bool DrawTacticSigilHud()
    {
        if (Main.gameMenu || Main.LocalPlayer is null)
            return true;

        Player player = Main.LocalPlayer;
        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        bool holdingShiFu = player.HeldItem.type == ModContent.ItemType<ShiFu>();
        if (fran.FranMode || holdingShiFu)
            DrawMatrix(fran);
        return true;
    }

    private static bool DrawHud()
    {
        if (Main.gameMenu || Main.LocalPlayer is null)
        {
            draggingUltimateGauge = false;
            previousGaugeMouseHeld = Mouse.GetState().LeftButton == ButtonState.Pressed;
            return true;
        }

        Player player = Main.LocalPlayer;
        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        // Keep input and drawing inside the same UI-scaled layer, including during autopause.
        UpdateUltimateGaugeDrag(player, fran);
        bool holdingShiFu = player.HeldItem.type == ModContent.ItemType<ShiFu>();
        if (!fran.FranMode && !holdingShiFu)
            return true;

        if (fran.FranMode && fran.UltimateUnlocked && !ModContent.GetInstance<FranClientConfig>().HideUltimateGauge)
            DrawUltimateGauge(fran);
        if (wheelVisibility > 0.001f)
            DrawWheelPointClamped(fran);
        return true;
    }

    private static void DrawWheelPointClamped(FranPlayer fran)
    {
        // Pixel icons and downscaled font glyphs need different sampling; text is drawn in a second pass.
        Main.spriteBatch.End();
        wheelText.Clear();
        collectingWheelText = true;
        try
        {
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
            DrawWheel(fran);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
            collectingWheelText = false;
            foreach (WheelText text in wheelText)
                DrawWheelText(text);
        }
        finally
        {
            collectingWheelText = false;
            wheelText.Clear();
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
        }
    }

    private static void UpdateUltimateGaugeDrag(Player player, FranPlayer fran)
    {
        // Inventory and other vanilla UI can consume Main.mouseLeft before this layer draws.
        bool leftHeld = Mouse.GetState().LeftButton == ButtonState.Pressed;
        bool leftPressed = leftHeld && !previousGaugeMouseHeld;
        previousGaugeMouseHeld = leftHeld;
        FranClientConfig config = ModContent.GetInstance<FranClientConfig>();
        if (!fran.FranMode || !fran.UltimateUnlocked || config.HideUltimateGauge || !config.AllowUltimateGaugeDragging ||
            !Main.hasFocus || player.dead || Main.ingameOptionsWindow || Main.inFancyUI ||
            Main.mapFullscreen || FranEvolutionUISystem.IsOpen || wheelOpen)
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

        if (!draggingUltimateGauge && hovered && leftPressed)
        {
            draggingUltimateGauge = true;
            ultimateGaugeDragOffset = Main.MouseScreen - gauge.TopLeft();
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f, Pitch = 0.1f });
        }

        if (!draggingUltimateGauge)
            return;
        if (!leftHeld)
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
        int activeCount = fran.GetActiveTacticCount();
        if (activeCount <= 0)
        {
            tacticSigilHoverFade = 0f;
            return;
        }

        Rectangle panelArea = GetTacticSigilArea(Main.LocalPlayer, activeCount);
        bool hovered = panelArea.Contains(Main.MouseScreen.ToPoint());
        tacticSigilHoverFade += ((hovered ? 1f : 0f) - tacticSigilHoverFade) *
            (hovered ? 0.18f : 0.10f);
        float opacity = 1f - tacticSigilHoverFade * 0.90f;

        FranSkill firstSkill = fran.GetEquippedTactic(0);
        Color panelTheme = FranSkillInfo.IsRealSkill(firstSkill)
            ? FranSkillInfo.Color(firstSkill) : new Color(82, 92, 108);
        DrawTacticSigilSpine(panelArea, panelTheme, opacity);

        int firstRuneCenterY = panelArea.Y + TacticSigilPadding + TacticRuneDiameter / 2;
        for (int slot = 0; slot < activeCount; slot++)
        {
            FranSkill skill = fran.GetEquippedTactic(slot);
            if (!FranSkillInfo.IsRealSkill(skill))
                continue;

            Point runeCenter = new(panelArea.Center.X,
                firstRuneCenterY + slot * (TacticRuneDiameter + TacticRuneGap));
            DrawTacticRune(skill, runeCenter, fran.SkillCooldowns[(int)skill] <= 0, opacity);
        }
    }

    private static void DrawWheel(FranPlayer fran)
    {
        float time = Main.GlobalTimeWrappedHourly;
        float animation = WheelAnimationEase;
        float opacity = animation;
        float detailsOpacity = MathHelper.Clamp((wheelVisibility - 0.18f) / 0.82f, 0f, 1f);
        detailsOpacity = detailsOpacity * detailsOpacity * (3f - 2f * detailsOpacity);
        float radius = AnimatedWheelRadius;

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

            float lineStartRadius = MathHelper.Lerp(38f, 49f, animation);
            float lineEndRadius = Math.Max(lineStartRadius + 2f, radius - 24f);
            DrawLine(wheelCenter + direction * lineStartRadius, wheelCenter + direction * lineEndRadius,
                color * ((hovered ? 0.82f : 0.22f) * opacity), hovered ? 3f : 1f);
            int cooldown = fran.SkillCooldowns[i];
            float cooldownRatio = cooldown > 0
                ? cooldown / (float)Math.Max(1, fran.SkillCooldownMaximums[i])
                : 0f;
            DrawNativeSkillIcon(center, SkillIconPath(skill),
                unlocked ? Color.White : new Color(105, 111, 120),
                (hovered ? 1f : stacks > 0 ? 0.98f : unlocked ? 0.82f : 0.46f) *
                MathHelper.Lerp(1f, 0.38f, cooldownRatio) * opacity);
            if (stacks > 0)
                DrawStackBadge(center + new Vector2(NativeSkillIconSize * 0.31f,
                    NativeSkillIconSize * 0.25f), stacks, color, opacity);
        }

        DrawNativeSkillIcon(wheelCenter, PureSwordsmanshipIcon, Color.White, 0.96f * opacity);

        for (int i = 0; i < 32; i++)
        {
            float angle = MathHelper.TwoPi * i / 32f + time * 0.08f;
            float innerRingRadius = MathHelper.Lerp(35f, 47.6f, animation);
            Vector2 a = wheelCenter + angle.ToRotationVector2() * innerRingRadius;
            Vector2 b = wheelCenter + (angle + MathHelper.TwoPi / 32f * 0.62f).ToRotationVector2() *
                innerRingRadius;
            DrawLine(a, b, new Color(70, 165, 205) * (0.48f * opacity), 2f);
        }

        string slotText = Language.GetTextValue("Mods.Fran.UI.TacticSlots",
            fran.GetActiveTacticCount(), fran.AvailableTacticSlots);
        DrawCentered(slotText, new Rectangle((int)wheelCenter.X - 170, (int)wheelCenter.Y + 113, 340, 24),
            new Color(205, 229, 239) * detailsOpacity, 0.8f);
        DrawCentered(Language.GetTextValue("Mods.Fran.UI.TacticControls"),
            new Rectangle((int)wheelCenter.X - 220, (int)wheelCenter.Y + 136, 440, 22),
            new Color(175, 201, 214) * detailsOpacity, 0.7f);

        DrawEquippedTacticSlots(fran, detailsOpacity);

        FranSkill detailSkill = hoveredSkill >= 0 ? (FranSkill)hoveredSkill : hoveredEquippedSlot >= 0
            ? fran.GetEquippedTactic(hoveredEquippedSlot) : FranSkill.None;
        Color panelColor = FranSkillInfo.IsRealSkill(detailSkill)
            ? FranSkillInfo.Color(detailSkill) : new Color(105, 145, 164);
        string title;
        string firstLine;
        string secondLine;
        string thirdLine;
        Color textColor;
        if (FranSkillInfo.IsRealSkill(detailSkill))
        {
            bool unlocked = FranSkillInfo.IsUnlocked(detailSkill);
            title = FranSkillInfo.Name(detailSkill);
            firstLine = unlocked ? FranSkillInfo.FirstLayerText(detailSkill) : FranSkillInfo.UnlockText(detailSkill);
            secondLine = unlocked ? FranSkillInfo.AdditionalLayerText(detailSkill) : string.Empty;
            thirdLine = unlocked ? FranSkillInfo.FullLoadoutText(detailSkill) : string.Empty;
            panelColor = unlocked ? panelColor : new Color(160, 160, 165);
            textColor = unlocked ? Color.White : new Color(185, 185, 190);
        }
        else
        {
            title = Language.GetTextValue("Mods.Fran.UI.PureSwordsmanship");
            firstLine = Language.GetTextValue("Mods.Fran.UI.PureSwordsmanshipLine1");
            secondLine = Language.GetTextValue("Mods.Fran.UI.PureSwordsmanshipLine2");
            thirdLine = string.Empty;
            panelColor = new Color(220, 235, 242);
            textColor = Color.White;
        }
        string[] detailLines = { firstLine, secondLine, thirdLine };
        int panelWidth = GetWheelDescriptionWidth(title, detailLines);
        int[] rowHeights = GetWheelDescriptionRowHeights(detailLines, panelWidth);
        int panelX = Math.Clamp((int)wheelCenter.X - panelWidth / 2, 12,
            Math.Max(12, Main.screenWidth - panelWidth - 12));
        int panelHeight = GetWheelDescriptionHeight(rowHeights);
        int panelY = Math.Min((int)wheelCenter.Y + 228, Math.Max(12, Main.screenHeight - panelHeight - 12));
        Rectangle descriptionPanel = new(panelX, panelY, panelWidth, panelHeight);
        DrawRect(descriptionPanel, new Color(5, 10, 16, 230) * detailsOpacity);
        DrawBorder(descriptionPanel, panelColor * (0.78f * detailsOpacity), 2);
        DrawCentered(title, new Rectangle(descriptionPanel.X + 12, descriptionPanel.Y + 5,
            descriptionPanel.Width - 24, 24), panelColor * detailsOpacity, 0.9f);
        int rowY = descriptionPanel.Y + 36;
        for (int i = 0; i < detailLines.Length; i++)
        {
            DrawWrappedCentered(detailLines[i], new Rectangle(descriptionPanel.X + 12, rowY,
                descriptionPanel.Width - 24, rowHeights[i]), textColor * detailsOpacity, WheelBodyTextScale);
            rowY += rowHeights[i] + 6;
        }
    }

    private static int WheelDescriptionWidth => Math.Min(520, Math.Max(240, Main.screenWidth - 24));

    private static int GetWheelDescriptionWidth(string title, string[] detailLines)
    {
        float longest = FontAssets.MouseText.Value.MeasureString(title).X * 0.9f;
        foreach (string text in detailLines)
            foreach (string line in WrapText(text, WheelDescriptionWidth - 24, WheelBodyTextScale))
                longest = Math.Max(longest, FontAssets.MouseText.Value.MeasureString(line).X * WheelBodyTextScale);
        // 当前实际显示的最长行加左右各 12 像素留白；长文仍保留换行和屏幕宽度上限。
        return Math.Clamp((int)MathF.Ceiling(longest) + 24, 96, WheelDescriptionWidth);
    }

    private static int GetWheelDescriptionHeight(int[] rows) => 48 + rows[0] + rows[1] + rows[2] + 12;

    private static int[] GetWheelDescriptionRowHeights(string[] detailLines = null, int panelWidth = 0)
    {
        if (panelWidth <= 0)
            panelWidth = WheelDescriptionWidth;
        float lineHeight = FontAssets.MouseText.Value.MeasureString("国").Y * WheelBodyTextScale;
        int[] rows = { (int)MathF.Ceiling(lineHeight), (int)MathF.Ceiling(lineHeight), (int)MathF.Ceiling(lineHeight) };
        void Include(int row, string text) => rows[row] = Math.Max(rows[row],
            (int)MathF.Ceiling(WrapText(text, panelWidth - 24, WheelBodyTextScale).Count * lineHeight));
        if (detailLines is not null)
        {
            for (int i = 0; i < rows.Length; i++)
                Include(i, detailLines[i]);
            return rows;
        }
        // 打开轮盘时按所有战术的最大高度留出屏幕空间，绘制时按当前文字计算。
        for (int i = 0; i < FranSkillInfo.Count; i++)
        {
            FranSkill skill = (FranSkill)i;
            Include(0, FranSkillInfo.FirstLayerText(skill));
            Include(0, FranSkillInfo.UnlockText(skill));
            Include(1, FranSkillInfo.AdditionalLayerText(skill));
            Include(2, FranSkillInfo.FullLoadoutText(skill));
        }
        Include(0, Language.GetTextValue("Mods.Fran.UI.PureSwordsmanshipLine1"));
        Include(1, Language.GetTextValue("Mods.Fran.UI.PureSwordsmanshipLine2"));
        return rows;
    }

    private static int FindHoveredSkill(Vector2 mouse)
    {
        for (int i = 0; i < FranSkillInfo.Count; i++)
        {
            float angle = -MathHelper.PiOver2 + MathHelper.TwoPi * i / FranSkillInfo.Count;
            Vector2 center = wheelCenter + angle.ToRotationVector2() * AnimatedWheelRadius;
            if (Vector2.DistanceSquared(mouse, center) <= WheelHitRadius * WheelHitRadius)
                return i;
        }
        return -1;
    }

    private static int FindHoveredEquippedSlot(FranPlayer fran, Vector2 mouse)
    {
        int slotCount = Math.Min(fran.AvailableTacticSlots, FranBalance.TacticStorageCapacity);
        for (int slot = 0; slot < slotCount; slot++)
            if (GetEquippedTacticSlotArea(slotCount, slot).Contains(mouse.ToPoint()))
                return slot;
        return -1;
    }

    private static Rectangle GetTacticSigilArea(Player owner, int runeCount)
    {
        int panelHeight = TacticSigilPadding * 2 + runeCount * TacticRuneDiameter +
            Math.Max(0, runeCount - 1) * TacticRuneGap;
        Vector2 panelCenter = owner.Center - Main.screenPosition +
            new Vector2(-TacticSigilLeftOffset, owner.gfxOffY - 8f);
        int x = Utils.Clamp((int)(panelCenter.X - TacticSigilWidth * 0.5f),
            TacticSigilScreenMargin, Main.screenWidth - TacticSigilWidth - TacticSigilScreenMargin);
        int y = Utils.Clamp((int)(panelCenter.Y - panelHeight * 0.5f),
            TacticSigilScreenMargin, Main.screenHeight - panelHeight - TacticSigilScreenMargin);
        return new Rectangle(x, y, TacticSigilWidth, panelHeight);
    }

    private static void DrawTacticSigilSpine(Rectangle area, Color themeColor, float opacity)
    {
        int x = area.Center.X;
        Color shadow = new Color(4, 8, 13) * (0.72f * opacity);
        Color line = Color.Lerp(new Color(54, 69, 86), themeColor, 0.30f) * (0.62f * opacity);
        DrawRect(new Rectangle(x - 1, area.Y + 2, 3, area.Height - 4), shadow);
        DrawRect(new Rectangle(x, area.Y + 3, 1, area.Height - 6), line);
        DrawRect(new Rectangle(x - 4, area.Y + 2, 9, 1), line * 0.78f);
        DrawRect(new Rectangle(x - 4, area.Bottom - 3, 9, 1), line * 0.78f);
    }

    private static void DrawTacticRune(FranSkill skill, Point center, bool ready, float opacity)
    {
        Color themeColor = FranSkillInfo.Color(skill);
        Color shadow = new Color(5, 9, 15) * (0.88f * opacity);
        Color edge = Color.Lerp(themeColor, Color.White, ready ? 0.24f : 0.05f) * (0.78f * opacity);
        Color fill = (ready ? Color.Lerp(themeColor, Color.White, 0.06f) : new Color(29, 36, 47)) *
            (0.90f * opacity);

        // Five stepped bars make a compact pixel diamond. Each tactic remains a pure color block,
        // but the independent crest silhouette no longer reads as ammunition.
        DrawRect(new Rectangle(center.X - 2, center.Y - 6, 5, 13), shadow);
        DrawRect(new Rectangle(center.X - 4, center.Y - 4, 9, 9), shadow);
        DrawRect(new Rectangle(center.X - 6, center.Y - 2, 13, 5), shadow);
        DrawRect(new Rectangle(center.X - 1, center.Y - 5, 3, 11), edge);
        DrawRect(new Rectangle(center.X - 3, center.Y - 3, 7, 7), edge);
        DrawRect(new Rectangle(center.X - 5, center.Y - 1, 11, 3), edge);
        DrawRect(new Rectangle(center.X - 1, center.Y - 3, 3, 7), fill);
        DrawRect(new Rectangle(center.X - 3, center.Y - 1, 7, 3), fill);
        if (ready)
            DrawRect(new Rectangle(center.X, center.Y - 2, 1, 2), Color.White * (0.62f * opacity));
    }

    private static void DrawEquippedTacticSlots(FranPlayer fran, float opacity)
    {
        int slotCount = Math.Min(fran.AvailableTacticSlots, FranBalance.TacticStorageCapacity);
        if (slotCount <= 0)
            return;

        Rectangle first = GetEquippedTacticSlotArea(slotCount, 0);
        Rectangle last = GetEquippedTacticSlotArea(slotCount, slotCount - 1);
        Rectangle connectedGroup = new(first.X, first.Y, last.Right - first.X, first.Height);
        DrawRect(connectedGroup, new Color(4, 9, 15, 188) * opacity);
        DrawBorder(connectedGroup, new Color(73, 112, 130) * (0.72f * opacity), 2);

        for (int slot = 0; slot < slotCount; slot++)
        {
            Rectangle slotArea = GetEquippedTacticSlotArea(slotCount, slot);
            Vector2 center = slotArea.Center.ToVector2();
            FranSkill skill = fran.GetEquippedTactic(slot);
            bool hovered = hoveredEquippedSlot == slot;
            Color frame = FranSkillInfo.IsRealSkill(skill)
                ? FranSkillInfo.Color(skill) : new Color(73, 112, 130);

            if (slot > 0)
                DrawRect(new Rectangle(slotArea.X - 1, slotArea.Y + 3, 2, slotArea.Height - 6),
                    frame * ((hovered ? 0.92f : 0.58f) * opacity));
            if (hovered)
            {
                DrawRect(slotArea, new Color(18, 25, 34, 224) * opacity);
                DrawBorder(slotArea, Color.Lerp(frame, Color.White, 0.44f) * opacity, 3);
            }

            if (FranSkillInfo.IsRealSkill(skill))
            {
                int cooldown = fran.SkillCooldowns[(int)skill];
                float cooldownRatio = cooldown > 0
                    ? cooldown / (float)Math.Max(1, fran.SkillCooldownMaximums[(int)skill]) : 0f;
                DrawNativeSkillIcon(center, SkillIconPath(skill), Color.White,
                    MathHelper.Lerp(1f, 0.42f, cooldownRatio) * opacity);
            }
            else
                DrawCentered((slot + 1).ToString(), slotArea, new Color(90, 125, 141) * opacity, 0.62f);
        }
    }

    private static Rectangle GetEquippedTacticSlotArea(int slotCount, int slot)
    {
        float groupLeft = wheelCenter.X - slotCount * EquippedTacticSlotSize * 0.5f;
        int x = (int)MathF.Round(groupLeft + slot * EquippedTacticSlotSize);
        int y = (int)MathF.Round(wheelCenter.Y + EquippedTacticSlotOffsetY - EquippedTacticSlotSize * 0.5f);
        return new Rectangle(x, y, EquippedTacticSlotSize, EquippedTacticSlotSize);
    }

    // Bind by skill identity rather than enum/array position. This mapping is shared by the wheel
    // and equipped slots, so a later enum reorder cannot silently swap two authored icons.
    internal static string SkillIconPath(FranSkill skill) => skill switch
    {
        FranSkill.TelekineticCatapult => SkillIconRoot + "FranSkill_01_TelekineticCatapult",
        FranSkill.SkillTaker => SkillIconRoot + "FranSkill_02_SkillTaker",
        FranSkill.MagicBarrier => SkillIconRoot + "FranSkill_03_MagicBarrier",
        FranSkill.MultipleBodyCreation => SkillIconRoot + "FranSkill_04_MultipleBodyCreation",
        FranSkill.BlueResonance => SkillIconRoot + "FranSkill_05_BlueResonance",
        FranSkill.ThunderChain => SkillIconRoot + "FranSkill_06_ThunderChain",
        FranSkill.PressurizedQuickdraw => SkillIconRoot + "FranSkill_07_PressurizedQuickdraw",
        FranSkill.ThorsHammer => SkillIconRoot + "FranSkill_08_ThorsHammer",
        _ => PureSwordsmanshipIcon
    };

    private static void DrawSkillIcon(Vector2 center, float size, string texturePath, Color tint,
        float opacity)
    {
        Texture2D texture = ModContent.Request<Texture2D>(texturePath).Value;
        int pixels = Math.Max(1, (int)MathF.Round(size * SkillIconDrawScale));
        Rectangle destination = new((int)MathF.Round(center.X - pixels * 0.5f),
            (int)MathF.Round(center.Y - pixels * 0.5f), pixels, pixels);
        Main.spriteBatch.Draw(texture, destination, tint * MathHelper.Clamp(opacity, 0f, 1f));
    }

    private static void DrawNativeSkillIcon(Vector2 center, string texturePath, Color tint, float opacity)
    {
        Texture2D texture = ModContent.Request<Texture2D>(texturePath).Value;
        Vector2 pixelCenter = new(MathF.Round(center.X), MathF.Round(center.Y));
        Main.spriteBatch.Draw(texture, pixelCenter, null,
            tint * MathHelper.Clamp(opacity, 0f, 1f), 0f, texture.Size() * 0.5f, 1f,
            SpriteEffects.None, 0f);
    }

    private static void DrawStackBadge(Vector2 center, int stacks, Color color, float opacity)
    {
        string text = $"×{stacks}";
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * 0.72f;
        DrawText(text, center - size * 0.5f,
            Color.Lerp(color, Color.White, 0.52f) * opacity, 0.72f);
    }

    private static void DrawCentered(string text, Rectangle area, Color color, float scale)
    {
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
        float actualScale = Math.Min(scale, area.Width / Math.Max(1f, size.X));
        Vector2 pos = new(area.Center.X - size.X * actualScale * 0.5f, area.Center.Y - size.Y * actualScale * 0.5f);
        DrawText(text, pos, color, actualScale);
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
            DrawText(line, position, color, scale);
            y += lineHeight;
        }
    }

    private static void DrawText(string text, Vector2 position, Color color, float scale)
    {
        if (collectingWheelText)
            wheelText.Add(new WheelText(text, position, color, scale));
        else
            Utils.DrawBorderString(Main.spriteBatch, text, position, color, scale);
    }

    private static void DrawWheelText(WheelText text)
    {
        Vector2 screen = Vector2.Transform(text.Position, Main.UIScaleMatrix);
        screen = new Vector2(MathF.Round(screen.X), MathF.Round(screen.Y));
        Vector2 position = Vector2.Transform(screen, Matrix.Invert(Main.UIScaleMatrix));
        float outline = 1f / Math.Max(0.01f, Main.UIScale);
        var font = FontAssets.MouseText.Value;
        Color shadow = Color.Black * (text.Color.A / 255f);
        for (int i = 0; i < 4; i++)
        {
            Vector2 offset = i switch
            {
                0 => new Vector2(-outline, 0f), 1 => new Vector2(outline, 0f),
                2 => new Vector2(0f, -outline), _ => new Vector2(0f, outline)
            };
            Main.spriteBatch.DrawString(font, text.Text, position + offset, shadow, 0f,
                Vector2.Zero, text.Scale, SpriteEffects.None, 0f);
        }
        Main.spriteBatch.DrawString(font, text.Text, position, text.Color, 0f,
            Vector2.Zero, text.Scale, SpriteEffects.None, 0f);
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
