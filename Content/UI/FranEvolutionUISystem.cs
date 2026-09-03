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

/// <summary>
/// A full-screen constellation chart whose interaction is resolved in the same UI draw pass as
/// its nodes. This keeps mouse hit testing aligned with Terraria's UI scale.
/// </summary>
public sealed class FranEvolutionUISystem : ModSystem
{
    private const float NodeRadius = 26f;
    private const string StellarBackdropTexture = "Fran/Textures/Particles/SoftBloom";
    private const string StellarSparkleTexture = "Fran/Textures/Particles/Sparkle";
    private const string StellarLineTexture = "Fran/Textures/Particles/BloomLine";
    private const string StellarRingTexture = "Fran/Textures/Particles/BloomRing";
    private const string StellarHoverTexture = "Fran/Textures/KsTexture/magic_05";

    private static readonly Color StellarDormant = new(66, 78, 128, 210);
    private static readonly Color StellarAvailable = new(174, 153, 255, 245);
    private static readonly Color StellarLit = new(255, 222, 116, 255);

    // The original twelve ordinals remain in their old order. New stars are appended and laid out
    // by branch so old saves continue to mean the same thing.
    private static readonly Vector2[] NormalizedNodePositions =
    [
        // Teacher: original four-star spine.
        new(0.50f, 0.43f), new(0.44f, 0.35f), new(0.40f, 0.25f), new(0.45f, 0.14f),
        // Fran: original four-star spine.
        new(0.43f, 0.54f), new(0.35f, 0.58f), new(0.27f, 0.65f), new(0.18f, 0.74f),
        // Bond: original four-star spine.
        new(0.57f, 0.54f), new(0.65f, 0.58f), new(0.73f, 0.65f), new(0.82f, 0.74f),
        // Teacher additions: 7 stars total.
        new(0.56f, 0.35f), new(0.61f, 0.25f), new(0.55f, 0.14f),
        // Fran additions: 9 stars total.
        new(0.39f, 0.68f), new(0.31f, 0.76f), new(0.24f, 0.55f), new(0.15f, 0.61f),
        new(0.23f, 0.79f),
        // Bond additions: 12 stars total.
        new(0.61f, 0.68f), new(0.69f, 0.76f), new(0.53f, 0.68f), new(0.57f, 0.79f),
        new(0.76f, 0.55f), new(0.85f, 0.61f), new(0.77f, 0.79f), new(0.88f, 0.77f)
    ];

    // These extra lines complete each constellation silhouette without adding hidden unlock rules.
    private static readonly (FranEvolutionNode A, FranEvolutionNode B)[] DecorativeConnections =
    [
        (FranEvolutionNode.ComboCalculation, FranEvolutionNode.ArcaneCirculation),
        (FranEvolutionNode.MagicStoneAnalysis, FranEvolutionNode.DeeperAnalysis),
        (FranEvolutionNode.SelfEvolution, FranEvolutionNode.SwordSaintReasoning),
        (FranEvolutionNode.ArcaneCirculation, FranEvolutionNode.DeeperAnalysis),
        (FranEvolutionNode.DeeperAnalysis, FranEvolutionNode.SwordSaintReasoning),

        (FranEvolutionNode.AerialHunt, FranEvolutionNode.FelineReflex),
        (FranEvolutionNode.HuntingTempo, FranEvolutionNode.SkyHunter),
        (FranEvolutionNode.PredatorSight, FranEvolutionNode.PhantomStep),
        (FranEvolutionNode.EvolvedFootwork, FranEvolutionNode.BlackCatAwakening),
        (FranEvolutionNode.SkyHunter, FranEvolutionNode.BlackCatAwakening),

        (FranEvolutionNode.SharedRhythm, FranEvolutionNode.SharedMana),
        (FranEvolutionNode.ResonantPull, FranEvolutionNode.SharedVision),
        (FranEvolutionNode.ResonantPull, FranEvolutionNode.SynchronizedVolley),
        (FranEvolutionNode.OneAsSword, FranEvolutionNode.MutualGrowth),
        (FranEvolutionNode.SharedMana, FranEvolutionNode.HarmonicGuard),
        (FranEvolutionNode.SharedVision, FranEvolutionNode.SoulBridge),
        (FranEvolutionNode.SynchronizedVolley, FranEvolutionNode.LimitResonance),
        (FranEvolutionNode.SoulBridge, FranEvolutionNode.MutualGrowth),
        (FranEvolutionNode.LimitResonance, FranEvolutionNode.EternalContract),
        (FranEvolutionNode.MutualGrowth, FranEvolutionNode.EternalContract)
    ];

    private static bool open;
    private static int hoveredNode = -1;
    private static int previousHoveredNode = -2;

    public static bool IsOpen => FranEvolutionInfo.SystemEnabled && open;

    public override void Unload()
    {
        open = false;
        hoveredNode = -1;
        previousHoveredNode = -2;
    }

    public override void UpdateUI(GameTime gameTime)
    {
        if (!FranEvolutionInfo.SystemEnabled)
        {
            Close(silent: true);
            return;
        }

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

        if (allowed && (FranModMain.EvolutionKey?.JustPressed == true ||
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

        PlayerInput.LockVanillaMouseScroll("FranMagicStoneConstellations");
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (!FranEvolutionInfo.SystemEnabled)
            return;

        int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
        if (index < 0)
            index = layers.Count;
        layers.Insert(index, new LegacyGameInterfaceLayer("Fran: Backgroundless Evolution Constellations",
            DrawInterface, InterfaceScaleType.UI));
    }

    private static bool DrawInterface()
    {
        if (!FranEvolutionInfo.SystemEnabled)
            return true;

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
        Rectangle canvas = GetCanvas();
        Vector2[] centers = GetNodeCenters(canvas);
        UpdateHoverAndInteractions(Main.LocalPlayer, evolution, canvas, centers);
        DrawNightSkyVeil(canvas);
        DrawConstellations(canvas, centers, evolution);
        DrawHeader(canvas, evolution);
        DrawBranchLabels(canvas);
        DrawNodeLabels(centers, evolution);
        DrawDescription(canvas, evolution);
        DrawResetText(canvas, evolution);
        return true;
    }

    private static void Open()
    {
        open = true;
        hoveredNode = -1;
        previousHoveredNode = -2;
        SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.62f, Pitch = 0.05f });
    }

    private static void Close(bool silent)
    {
        if (open && !silent)
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.58f });
        open = false;
        hoveredNode = -1;
        previousHoveredNode = -2;
    }

    private static Rectangle GetCanvas()
    {
        int width = Math.Min(1360, Math.Max(760, Main.screenWidth - 48));
        int height = Math.Min(860, Math.Max(560, Main.screenHeight - 40));
        return new Rectangle((Main.screenWidth - width) / 2, (Main.screenHeight - height) / 2, width, height);
    }

    private static Rectangle GetEntryButton()
    {
        Vector2 center = Main.LocalPlayer.Center - Main.screenPosition + new Vector2(78f, -8f);
        center.X = MathHelper.Clamp(center.X, 33f, Main.screenWidth - 33f);
        center.Y = MathHelper.Clamp(center.Y, 33f, Main.screenHeight - 33f);
        return Utils.CenteredRectangle(center, new Vector2(30f));
    }

    private static Rectangle GetResetButton(Rectangle canvas) =>
        new(canvas.Right - 204, canvas.Bottom - 40, 184, 28);

    private static Vector2[] GetNodeCenters(Rectangle canvas)
    {
        Vector2[] result = new Vector2[FranEvolutionInfo.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = canvas.TopLeft() + NormalizedNodePositions[i] * canvas.Size();
        return result;
    }

    private static Vector2 GetCore(Rectangle canvas) =>
        canvas.TopLeft() + new Vector2(0.50f, 0.48f) * canvas.Size();

    private static void UpdateHoverAndInteractions(Player player, FranEvolutionPlayer evolution,
        Rectangle canvas, Vector2[] centers)
    {
        Vector2 mouse = new(Main.mouseX, Main.mouseY);
        bool mouseInside = canvas.Contains(Main.mouseX, Main.mouseY);
        player.mouseInterface = mouseInside;
        if (mouseInside)
            Main.blockMouse = true;

        hoveredNode = -1;
        float hitRadiusSquared = (NodeRadius + 10f) * (NodeRadius + 10f);
        for (int i = 0; i < centers.Length; i++)
        {
            if (Vector2.DistanceSquared(mouse, centers[i]) > hitRadiusSquared)
                continue;
            hoveredNode = i;
            break;
        }

        if (hoveredNode != previousHoveredNode)
        {
            if (hoveredNode >= 0)
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.34f, Pitch = 0.18f });
            previousHoveredNode = hoveredNode;
        }

        // Match Leonid's proven input path: handle the click in the same interface pass that owns
        // the displayed node positions, after confirming the mouse is inside the chart.
        if (!mouseInside || !Main.mouseLeft || !Main.mouseLeftRelease)
            return;

        Rectangle resetButton = GetResetButton(canvas);
        if (resetButton.Contains(Main.mouseX, Main.mouseY))
        {
            if (evolution.NodeMask != 0 && evolution.CanAdjustEvolution)
            {
                evolution.RequestReset();
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.68f, Pitch = -0.12f });
            }
            else
                SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.35f, Pitch = 0.28f });
            return;
        }

        if (hoveredNode < 0)
            return;

        FranEvolutionNode node = (FranEvolutionNode)hoveredNode;
        FranEvolutionUnlockFailure failure = evolution.GetUnlockFailure(node);
        if (failure == FranEvolutionUnlockFailure.None)
        {
            evolution.RequestUnlock(node);
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.74f, Pitch = 0.14f });
        }
        else if (failure != FranEvolutionUnlockFailure.AlreadyUnlocked)
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.38f, Pitch = 0.30f });
    }

    private static void DrawNightSkyVeil(Rectangle canvas)
    {
        Rectangle veil = new(canvas.X - 18, canvas.Y - 14, canvas.Width + 36, canvas.Height + 28);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, veil, new Color(4, 7, 20, 168));
    }

    private static void DrawEntryButton()
    {
        Rectangle hitbox = GetEntryButton();
        bool hovered = hitbox.Contains(Main.MouseScreen.ToPoint());
        Vector2 center = hitbox.Center.ToVector2();
        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.8f);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        DrawStellarSparkle(Main.spriteBatch, center, Color.White,
            hovered ? new Color(205, 165, 255) : new Color(137, 104, 231),
            hovered ? 18f : 14f + pulse * 2f, 3);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

        if (hovered)
            Main.instance.MouseText(Language.GetTextValue("Mods.Fran.Evolution.OpenHint"));
    }

    private static void DrawHeader(Rectangle canvas, FranEvolutionPlayer evolution)
    {
        DrawCenteredText(Language.GetTextValue("Mods.Fran.Evolution.Title"),
            new Vector2(canvas.Center.X, canvas.Y + 12f), new Color(226, 235, 255), 1.08f);
        string points = Language.GetTextValue("Mods.Fran.Evolution.PointSummary", evolution.AvailablePoints,
            evolution.SpentPoints, evolution.EarnedPoints, FranEvolutionInfo.CurrentStagePointCap);
        DrawCenteredText(points, new Vector2(canvas.Center.X, canvas.Y + 42f),
            new Color(184, 202, 222), 0.68f);
    }

    private static void DrawBranchLabels(Rectangle canvas)
    {
        DrawCenteredText(Language.GetTextValue("Mods.Fran.Evolution.Branches.Teacher"),
            canvas.TopLeft() + new Vector2(0.50f, 0.085f) * canvas.Size(),
            FranEvolutionInfo.BranchColor(FranEvolutionBranch.Teacher), 0.72f);
        DrawCenteredText(Language.GetTextValue("Mods.Fran.Evolution.Branches.Fran"),
            canvas.TopLeft() + new Vector2(0.17f, 0.49f) * canvas.Size(),
            FranEvolutionInfo.BranchColor(FranEvolutionBranch.Fran), 0.72f);
        DrawCenteredText(Language.GetTextValue("Mods.Fran.Evolution.Branches.Bond"),
            canvas.TopLeft() + new Vector2(0.83f, 0.49f) * canvas.Size(),
            FranEvolutionInfo.BranchColor(FranEvolutionBranch.Bond), 0.72f);
    }

    private static void DrawConstellations(Rectangle canvas, Vector2[] centers,
        FranEvolutionPlayer evolution)
    {
        SpriteBatch spriteBatch = Main.spriteBatch;
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

        Vector2 core = GetCore(canvas);
        DrawCelestialField(spriteBatch, canvas, core);

        int connectionIndex = 0;
        for (int i = 0; i < FranEvolutionInfo.Count; i++)
        {
            FranEvolutionNode node = (FranEvolutionNode)i;
            FranEvolutionNodeDefinition definition = FranEvolutionInfo.Get(node);
            Vector2 start = definition.Prerequisite == FranEvolutionNode.None
                ? core
                : centers[(int)definition.Prerequisite];
            DrawProgressConnection(spriteBatch, start, centers[i], node, evolution, connectionIndex++);
        }

        foreach ((FranEvolutionNode a, FranEvolutionNode b) in DecorativeConnections)
            DrawDecorativeConnection(spriteBatch, centers[(int)a], centers[(int)b], a, b, evolution,
                connectionIndex++);

        DrawStellarCore(spriteBatch, core, evolution);
        for (int i = 0; i < FranEvolutionInfo.Count; i++)
        {
            FranEvolutionNode node = (FranEvolutionNode)i;
            FranEvolutionNodeDefinition definition = FranEvolutionInfo.Get(node);
            FranEvolutionUnlockFailure failure = evolution.GetUnlockFailure(node);
            bool purchased = evolution.HasNode(node);
            bool available = failure == FranEvolutionUnlockFailure.None;
            bool hovered = hoveredNode == i;
            Color branch = FranEvolutionInfo.BranchColor(definition.Branch);
            Color starColor = purchased ? Color.White
                : available ? Color.Lerp(branch, Color.White, 0.58f)
                : new Color(124, 140, 198);
            Color bloomColor = purchased ? Color.Lerp(branch, StellarLit, 0.32f)
                : available ? Color.Lerp(branch, StellarAvailable, 0.42f)
                : StellarDormant;
            float size = purchased ? 34f : available ? 29f : 23f;
            if (hovered)
                size *= 1.20f;

            DrawStellarSparkle(spriteBatch, centers[i], starColor, bloomColor, size, i + 17);
            if (!hovered)
                continue;
            Texture2D hoverGlow = ModContent.Request<Texture2D>(StellarHoverTexture).Value;
            float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.2f);
            DrawTexturePixels(spriteBatch, hoverGlow, centers[i], bloomColor * (0.36f + pulse * 0.20f),
                -Main.GlobalTimeWrappedHourly * 0.36f, Vector2.One * (62f + pulse * 7f));
        }

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
    }

    private static void DrawCelestialField(SpriteBatch spriteBatch, Rectangle canvas, Vector2 center)
    {
        Texture2D bloom = ModContent.Request<Texture2D>(StellarBackdropTexture).Value;
        float breath = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 0.72f);

        // These are transparent light emissions, not a panel or backdrop rectangle.
        DrawTexturePixels(spriteBatch, bloom, center + new Vector2(-220f, -80f),
            new Color(48, 71, 190) * (0.055f + breath * 0.018f), 0f, new Vector2(520f, 330f));
        DrawTexturePixels(spriteBatch, bloom, center + new Vector2(250f, 105f),
            new Color(151, 78, 213) * (0.045f + (1f - breath) * 0.015f), 0f,
            new Vector2(470f, 300f));

        for (int i = 0; i < 54; i++)
        {
            float x = canvas.X + 28f + Hash01(i * 37 + 11) * (canvas.Width - 56f);
            float y = canvas.Y + 58f + Hash01(i * 53 + 29) * (canvas.Height - 126f);
            float speed = 0.82f + Hash01(i * 71 + 5) * 1.28f;
            float twinkle = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * speed + i * 1.71f);
            float size = 2.6f + Hash01(i * 97 + 17) * 4.8f;
            Color color = Color.Lerp(new Color(102, 145, 255), new Color(225, 207, 255),
                Hash01(i * 43 + 3));
            DrawStellarSparkle(spriteBatch, new Vector2(x, y), color * (0.35f + twinkle * 0.24f),
                color * 0.26f, size, i + 101);
        }
    }

    private static void DrawProgressConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
        FranEvolutionNode destination, FranEvolutionPlayer evolution, int index)
    {
        FranEvolutionNodeDefinition definition = FranEvolutionInfo.Get(destination);
        bool purchased = evolution.HasNode(destination);
        bool prerequisiteMet = definition.Prerequisite == FranEvolutionNode.None ||
            evolution.HasNode(definition.Prerequisite);
        Color branch = FranEvolutionInfo.BranchColor(definition.Branch);
        Color color = purchased ? Color.Lerp(branch, StellarLit, 0.22f)
            : prerequisiteMet ? Color.Lerp(StellarDormant, branch, 0.48f) : StellarDormant;
        DrawStellarLine(spriteBatch, start, end, color, purchased ? 3.2f : 2.0f,
            purchased ? 0.72f : prerequisiteMet ? 0.38f : 0.30f);
        if (purchased)
            DrawTravelingLight(spriteBatch, start, end, branch, index);
    }

    private static void DrawDecorativeConnection(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
        FranEvolutionNode a, FranEvolutionNode b, FranEvolutionPlayer evolution, int index)
    {
        bool bright = evolution.HasNode(a) && evolution.HasNode(b);
        FranEvolutionBranch branch = FranEvolutionInfo.Get(a).Branch;
        Color color = bright ? Color.Lerp(FranEvolutionInfo.BranchColor(branch), StellarLit, 0.16f)
            : StellarDormant;
        DrawStellarLine(spriteBatch, start, end, color, bright ? 2.5f : 1.8f,
            bright ? 0.50f : 0.26f);
        if (bright)
            DrawTravelingLight(spriteBatch, start, end, FranEvolutionInfo.BranchColor(branch), index);
    }

    private static void DrawTravelingLight(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
        Color branch, int index)
    {
        float travel = (Main.GlobalTimeWrappedHourly * 0.16f + index * 0.173f) % 1f;
        travel = travel * travel * (3f - 2f * travel);
        DrawStellarSparkle(spriteBatch, Vector2.Lerp(start, end, travel), Color.White,
            Color.Lerp(branch, StellarLit, 0.48f), 7.2f, 211 + index);
    }

    private static void DrawStellarCore(SpriteBatch spriteBatch, Vector2 center,
        FranEvolutionPlayer evolution)
    {
        int orbitCount = Math.Min(18, (evolution.EarnedPoints + 2) / 3);
        for (int i = 0; i < orbitCount; i++)
        {
            float angle = MathHelper.TwoPi * i / Math.Max(1, orbitCount) +
                Main.GlobalTimeWrappedHourly * (i % 2 == 0 ? 0.13f : -0.10f);
            Vector2 position = center + angle.ToRotationVector2() * (45f + i % 3 * 4f);
            Color color = Color.Lerp(new Color(161, 112, 242), StellarLit, (i % 4) / 7f);
            DrawStellarSparkle(spriteBatch, position, Color.White * 0.68f, color, 6.4f, 311 + i);
        }

        Texture2D ring = ModContent.Request<Texture2D>(StellarRingTexture).Value;
        DrawTexturePixels(spriteBatch, ring, center, new Color(149, 113, 235) * 0.58f,
            Main.GlobalTimeWrappedHourly * 0.32f, new Vector2(76f));
        DrawStellarSparkle(spriteBatch, center, Color.White,
            Color.Lerp(new Color(137, 104, 231), StellarLit, 0.24f), 36f, 7);
    }

    private static void DrawNodeLabels(Vector2[] centers, FranEvolutionPlayer evolution)
    {
        for (int i = 0; i < FranEvolutionInfo.Count; i++)
        {
            FranEvolutionNode node = (FranEvolutionNode)i;
            FranEvolutionNodeDefinition definition = FranEvolutionInfo.Get(node);
            bool purchased = evolution.HasNode(node);
            Color branch = FranEvolutionInfo.BranchColor(definition.Branch);
            string label = purchased ? "★" : definition.Cost.ToString();
            DrawCenteredText(label, centers[i] + new Vector2(0f, 22f),
                purchased ? Color.Lerp(branch, Color.White, 0.48f) : new Color(211, 219, 241),
                hoveredNode == i ? 0.78f : 0.66f);
        }
    }

    private static void DrawDescription(Rectangle canvas, FranEvolutionPlayer evolution)
    {
        if (hoveredNode < 0)
        {
            DrawCenteredText(Language.GetTextValue("Mods.Fran.Evolution.Overview"),
                new Vector2(canvas.Center.X, canvas.Bottom - 26f), new Color(201, 215, 229), 0.58f,
                canvas.Width - 430f);
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

        float left = canvas.X + 22f;
        float bottom = canvas.Bottom - 14f;
        Utils.DrawBorderString(Main.spriteBatch, $"✦ {name}", new Vector2(left, bottom - 82f), branch, 0.78f);
        Utils.DrawBorderString(Main.spriteBatch, body, new Vector2(left, bottom - 49f),
            new Color(226, 233, 248), 0.56f);
        Utils.DrawBorderString(Main.spriteBatch, status, new Vector2(left, bottom - 23f),
            failure == FranEvolutionUnlockFailure.None || evolution.HasNode(node)
                ? new Color(142, 232, 199) : new Color(212, 171, 148), 0.53f);
    }

    private static void DrawResetText(Rectangle canvas, FranEvolutionPlayer evolution)
    {
        Rectangle hitbox = GetResetButton(canvas);
        bool hovered = hitbox.Contains(Main.MouseScreen.ToPoint());
        string text = evolution.NodeMask == 0
            ? Language.GetTextValue("Mods.Fran.Evolution.ResetEmpty")
            : !evolution.CanAdjustEvolution
                ? Language.GetTextValue("Mods.Fran.Evolution.ResetLocked")
                : Language.GetTextValue("Mods.Fran.Evolution.ResetFree");
        Color color = evolution.NodeMask == 0 || !evolution.CanAdjustEvolution
            ? new Color(112, 119, 142)
            : hovered ? Color.White : new Color(214, 198, 255);
        DrawCenteredText(text, hitbox.Center.ToVector2(), color, hovered ? 0.70f : 0.63f,
            hitbox.Width);
    }

    private static void DrawStellarSparkle(SpriteBatch spriteBatch, Vector2 position, Color color,
        Color bloomColor, float size, int index)
    {
        Texture2D star = ModContent.Request<Texture2D>(StellarSparkleTexture).Value;
        float opacity = 0.80f + 0.20f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + index * 0.83f);
        float rotation = Main.GlobalTimeWrappedHourly * (0.72f + index % 7 * 0.035f) + index * 0.71f;
        DrawTexturePixels(spriteBatch, star, position, bloomColor * opacity * 0.34f,
            rotation + MathHelper.PiOver4, new Vector2(size * 0.78f));
        DrawTexturePixels(spriteBatch, star, position, color * opacity, rotation, new Vector2(size));
    }

    private static void DrawStellarLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
        Color color, float thickness, float opacity)
    {
        Vector2 edge = end - start;
        if (edge.LengthSquared() <= 0.01f)
            return;
        Texture2D line = ModContent.Request<Texture2D>(StellarLineTexture).Value;
        Vector2 origin = new(0f, line.Height * 0.5f);
        Vector2 scale = new(edge.Length() / Math.Max(1f, line.Width),
            thickness / Math.Max(1f, line.Height));
        // Match LP's hierarchy: every edge owns a dormant underglow, while purchased paths add a crisp core.
        // This wider base remains legible against the live world without turning the chart into a solid panel.
        spriteBatch.Draw(line, start, null, color * (opacity * 0.30f), edge.ToRotation(), origin,
            new Vector2(scale.X, scale.Y * 2.8f), SpriteEffects.None, 0f);
        spriteBatch.Draw(line, start, null, color * opacity, edge.ToRotation(),
            origin, scale, SpriteEffects.None, 0f);

        Texture2D star = ModContent.Request<Texture2D>(StellarSparkleTexture).Value;
        Vector2 capSize = Vector2.One * Math.Max(4f, thickness * 2.2f);
        DrawTexturePixels(spriteBatch, star, start, color * opacity * 0.56f, 0f, capSize);
        DrawTexturePixels(spriteBatch, star, end, color * opacity * 0.56f, 0f, capSize);
    }

    private static void DrawTexturePixels(SpriteBatch spriteBatch, Texture2D texture, Vector2 position,
        Color color, float rotation, Vector2 pixelSize)
    {
        Vector2 size = texture.Size();
        Vector2 scale = new(pixelSize.X / Math.Max(1f, size.X), pixelSize.Y / Math.Max(1f, size.Y));
        spriteBatch.Draw(texture, position, null, color, rotation, size * 0.5f, scale,
            SpriteEffects.None, 0f);
    }

    private static float Hash01(int seed)
    {
        uint value = (uint)seed;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        return (value & 0x00FFFFFF) / 16777215f;
    }

    private static void DrawCenteredText(string text, Vector2 center, Color color, float scale,
        float maximumWidth = float.MaxValue)
    {
        Vector2 size = FontAssets.MouseText.Value.MeasureString(text);
        float actualScale = Math.Min(scale, maximumWidth / Math.Max(1f, size.X));
        Utils.DrawBorderString(Main.spriteBatch, text, center - size * actualScale * 0.5f,
            color, actualScale);
    }
}
