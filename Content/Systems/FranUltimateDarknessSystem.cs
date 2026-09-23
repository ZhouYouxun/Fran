using System;
using System.Collections.Generic;
using FranMod.Content.Projectiles;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace FranMod.Content.Systems;

/// <summary>
/// Gradually darkens the game world during Black Heavenly Tiger while leaving the HUD readable.
/// The darkness holds through the final cut, then recovers at half the fade-in speed.
/// </summary>
public sealed class FranUltimateDarknessSystem : ModSystem
{
    private static float darknessProgress;

    public override void OnWorldLoad() => darknessProgress = 0f;

    public override void OnWorldUnload() => darknessProgress = 0f;

    public override void PostUpdateEverything()
    {
        if (Main.dedServ)
            return;

        bool ultimateActive = false;
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.ModProjectile is FranUltimateController)
            {
                ultimateActive = true;
                break;
            }

        int fadeFrames = Math.Max(1, ultimateActive
            ? FranBalance.GetInt(nameof(FranBalance.UltimateDarknessFadeInFrames),
                FranBalance.UltimateDarknessFadeInFrames)
            : FranBalance.GetInt(nameof(FranBalance.UltimateDarknessFadeOutFrames),
                FranBalance.UltimateDarknessFadeOutFrames));
        float target = ultimateActive ? 1f : 0f;
        darknessProgress = Approach(darknessProgress, target, 1f / fadeFrames);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Interface Logic 1");
        if (index < 0)
            index = 0;
        layers.Insert(index, new LegacyGameInterfaceLayer("Fran: Black Heavenly Tiger Darkness",
            DrawDarkness, InterfaceScaleType.UI));
    }

    private static bool DrawDarkness()
    {
        if (Main.gameMenu || darknessProgress <= 0.0001f)
            return true;

        float eased = darknessProgress * darknessProgress * (3f - 2f * darknessProgress);
        float maximumOpacity = MathHelper.Clamp(FranBalance.GetFloat(
            nameof(FranBalance.UltimateDarknessMaximumOpacity),
            FranBalance.UltimateDarknessMaximumOpacity), 0f, 0.90f) * 0.50f;
        // Use the graphics device's live viewport instead of Main.screenWidth/screenHeight. The UI
        // layer can apply its own scaling transform, so extend by one complete viewport on every
        // side: the center viewport stays covered at high resolutions, ultrawide aspect ratios and
        // every supported UI scale without relying on a fixed 8K-sized rectangle.
        Viewport viewport = Main.instance.GraphicsDevice.Viewport;
        Rectangle cover = new(-viewport.Width, -viewport.Height,
            viewport.Width * 3, viewport.Height * 3);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, cover,
            new Color(3, 0, 8) * (eased * maximumOpacity));
        return true;
    }

    private static float Approach(float value, float target, float step)
    {
        if (value < target)
            return Math.Min(value + step, target);
        if (value > target)
            return Math.Max(value - step, target);
        return target;
    }
}
