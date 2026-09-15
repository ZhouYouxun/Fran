using System;
using System.Collections.Generic;
using FranMod.Content.Projectiles;
using FranMod.Core;
using Microsoft.Xna.Framework;
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
            FranBalance.UltimateDarknessMaximumOpacity), 0f, 0.90f);
        float uiScale = Math.Max(0.01f, Main.UIScale);
        Rectangle screen = new(-4, -4,
            (int)MathF.Ceiling(Main.screenWidth / uiScale) + 8,
            (int)MathF.Ceiling(Main.screenHeight / uiScale) + 8);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, screen,
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
