using System;
using System.Collections.Generic;
using System.Reflection;
using FranMod.Content.Config;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace FranMod.Content.MainMenu;

/// <summary>
/// Fran's optional title-screen theme. The still artwork owns the composition while restrained,
/// code-driven falling flames give the menu motion without baking an animation into the source image.
/// </summary>
[Autoload(Side = ModSide.Client)]
public sealed class FranMainMenu : ModMenu
{
    private const string BackgroundPath = "Fran/主菜单背景/FranMenuBackground";
    private const string FlameTexturePath = "Fran/Textures/Particles/Flame";
    private const int MaximumFlames = 44;

    private readonly List<FallingFlame> flames = [];
    private Asset<Texture2D> background;
    private Asset<Texture2D> flameTexture;
    private int spawnTimer;

    public override string DisplayName => Language.GetTextValue("Mods.Fran.MainMenu.DisplayName");

    public override bool IsAvailable => FranClientConfig.Instance?.EnableCustomMainMenu ?? true;

    public override int Music => MusicID.Title;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        background = ModContent.Request<Texture2D>(BackgroundPath, AssetRequestMode.ImmediateLoad);
        flameTexture = ModContent.Request<Texture2D>(FlameTexturePath, AssetRequestMode.ImmediateLoad);
    }

    public override void Unload()
    {
        background = null;
        flameTexture = null;
        flames.Clear();
    }

    public override void OnSelected() => SeedFlames();

    public override void OnDeselected() => flames.Clear();

    internal void ApplyAsDefaultMenuOnce()
    {
        FranClientConfig config = FranClientConfig.Instance;
        if (config is null || config.CustomMainMenuDefaultApplied || !config.EnableCustomMainMenu)
            return;

        try
        {
            // tModLoader persists the last selected menu but currently exposes no public setter.
            // Set it during content loading, before MenuLoader restores the saved menu, then mark
            // this migration complete so later player-selected themes remain respected.
            FieldInfo lastSelectedMenu = typeof(MenuLoader).GetField("LastSelectedModMenu",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo saveConfig = typeof(ConfigManager).GetMethod("Save",
                BindingFlags.Static | BindingFlags.NonPublic);

            if (lastSelectedMenu is null || saveConfig is null)
            {
                Mod.Logger.Warn("Fran could not apply its one-time default main menu because " +
                    "tModLoader's menu/config internals have changed.");
                return;
            }

            lastSelectedMenu.SetValue(null, FullName);
            Main.SaveSettings();

            config.MarkCustomMainMenuDefaultApplied();
            saveConfig.Invoke(null, [config]);
        }
        catch (Exception exception)
        {
            Mod.Logger.Warn($"Fran could not apply its one-time default main menu. {exception}");
        }
    }

    public override void Update(bool isOnTitleScreen)
    {
        if (!IsAvailable)
        {
            flames.Clear();
            return;
        }

        for (int i = flames.Count - 1; i >= 0; i--)
        {
            FallingFlame flame = flames[i];
            flame.Age++;

            // Every particle owns a finite sprite,
            // lifetime and smoothly changing velocity. The direction is inverted here so
            // these flames fall from the top instead of rising from the bottom.
            float sway = MathF.Sin(flame.Age * 0.045f + flame.Phase);
            Vector2 idealVelocity = new(sway * flame.Drift, flame.FallSpeed);
            flame.Velocity = Vector2.Lerp(flame.Velocity, idealVelocity, 0.035f);
            flame.Position += flame.Velocity;
            flame.Rotation = sway * 0.12f;

            if (flame.Age >= flame.Lifetime || flame.Position.Y > Main.screenHeight + 70f)
                flames.RemoveAt(i);
        }

        if (--spawnTimer <= 0 && flames.Count < MaximumFlames)
        {
            flames.Add(CreateFlame(initial: false));
            spawnTimer = Main.rand.Next(4, 9);
        }
    }

    public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter,
        ref float logoRotation, ref float logoScale, ref Color drawColor)
    {
        if (!IsAvailable || background?.Value is null)
            return true;

        drawColor = Color.White;

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        DrawCover(spriteBatch, background.Value);
        DrawReadabilityShade(spriteBatch);
        spriteBatch.End();

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        DrawFlames(spriteBatch);
        spriteBatch.End();

        // Restore the title screen's normal batch so tModLoader can draw its logo and menu controls.
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        return true;
    }

    private void SeedFlames()
    {
        flames.Clear();
        if (!IsAvailable)
            return;

        int initialCount = Math.Clamp(Main.screenWidth / 70, 18, 34);
        for (int i = 0; i < initialCount; i++)
            flames.Add(CreateFlame(initial: true));
        spawnTimer = 1;
    }

    private static FallingFlame CreateFlame(bool initial)
    {
        float depth = Main.rand.NextFloat(0.55f, 1.35f);
        float fallSpeed = MathHelper.Lerp(1.1f, 3.35f, depth / 1.35f);
        int lifetime = Math.Clamp((int)((Main.screenHeight + 150f) / fallSpeed) + 80, 260, 900);

        return new FallingFlame
        {
            Position = new Vector2(Main.rand.NextFloat(-24f, Main.screenWidth + 24f),
                initial ? Main.rand.NextFloat(-40f, Main.screenHeight + 20f) : Main.rand.NextFloat(-72f, -24f)),
            Velocity = new Vector2(Main.rand.NextFloat(-0.45f, 0.45f), fallSpeed),
            FallSpeed = fallSpeed,
            Drift = Main.rand.NextFloat(0.35f, 1.15f),
            Phase = Main.rand.NextFloat(MathHelper.TwoPi),
            Scale = depth,
            DrawColor = Color.Lerp(new Color(255, 62, 8), new Color(255, 185, 54), Main.rand.NextFloat()),
            Age = initial ? Main.rand.Next(18, Math.Max(19, lifetime / 2)) : 0,
            Lifetime = lifetime
        };
    }

    private static void DrawCover(SpriteBatch spriteBatch, Texture2D texture)
    {
        float scale = Math.Max(Main.screenWidth / (float)texture.Width,
            Main.screenHeight / (float)texture.Height);
        Vector2 size = texture.Size() * scale;
        Rectangle destination = new(
            (int)((Main.screenWidth - size.X) * 0.5f),
            (int)((Main.screenHeight - size.Y) * 0.5f),
            (int)MathF.Ceiling(size.X),
            (int)MathF.Ceiling(size.Y));
        spriteBatch.Draw(texture, destination, Color.White);
    }

    private static void DrawReadabilityShade(SpriteBatch spriteBatch)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Rectangle screen = new(0, 0, Main.screenWidth, Main.screenHeight);
        spriteBatch.Draw(pixel, screen, new Color(7, 2, 13, 34));

        // The vanilla menu list normally occupies the left side. A feathered shade keeps its
        // text readable at every resolution without hiding the official battle composition.
        int shadeWidth = Math.Max(280, (int)(Main.screenWidth * 0.47f));
        const int strips = 22;
        for (int i = 0; i < strips; i++)
        {
            float completion = i / (float)(strips - 1);
            int x0 = i * shadeWidth / strips;
            int x1 = (i + 1) * shadeWidth / strips;
            float opacity = MathF.Pow(1f - completion, 1.65f) * 0.34f;
            spriteBatch.Draw(pixel, new Rectangle(x0, 0, x1 - x0 + 1, Main.screenHeight),
                Color.Black * opacity);
        }
    }

    private void DrawFlames(SpriteBatch spriteBatch)
    {
        if (flameTexture?.Value is null)
            return;

        Texture2D texture = flameTexture.Value;
        Vector2 origin = texture.Size() * 0.5f;

        foreach (FallingFlame flame in flames)
        {
            float fadeIn = MathHelper.Clamp(flame.Age / 18f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((flame.Lifetime - flame.Age) / 54f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            if (fade <= 0f)
                continue;

            float pulse = 0.88f + MathF.Sin(flame.Age * 0.12f + flame.Phase) * 0.12f;
            Vector2 outerSize = new Vector2(17f, 25f) * flame.Scale * pulse;
            Vector2 outerScale = outerSize / texture.Size();

            spriteBatch.Draw(texture, flame.Position, null, flame.DrawColor * (fade * 0.72f), flame.Rotation,
                origin, outerScale,
                SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, flame.Position + Vector2.UnitY * (3f * flame.Scale), null,
                new Color(255, 239, 170) * (fade * 0.52f), flame.Rotation, origin, outerScale * 0.46f,
                SpriteEffects.None, 0f);
        }
    }

    private sealed class FallingFlame
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float FallSpeed;
        public float Drift;
        public float Phase;
        public float Scale;
        public float Rotation;
        public Color DrawColor;
        public int Age;
        public int Lifetime;
    }
}

[Autoload(Side = ModSide.Client)]
internal sealed class FranMainMenuDefaultSystem : ModSystem
{
    public override void PostSetupContent() =>
        ModContent.GetInstance<FranMainMenu>().ApplyAsDefaultMenuOnce();
}
