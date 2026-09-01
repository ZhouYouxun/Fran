using System;
using FranMod.Content.Config;
using Microsoft.Xna.Framework;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace FranMod.Content.Systems;

[Autoload(Side = ModSide.Client)]
public sealed class FranFontSystem : ModSystem
{
    private Asset<DynamicSpriteFont> customMouseText;
    private Asset<DynamicSpriteFont> customDeathText;
    private Asset<DynamicSpriteFont> previousMouseText;
    private Asset<DynamicSpriteFont> previousDeathText;
    private bool assetsReady;
    private bool replacementApplied;

    public override void OnModLoad()
    {
        try
        {
            customMouseText = ModContent.Request<DynamicSpriteFont>(
                "Fran/Assets/Fonts/FranMouseText", AssetRequestMode.ImmediateLoad);
            customDeathText = ModContent.Request<DynamicSpriteFont>(
                "Fran/Assets/Fonts/FranDeathText", AssetRequestMode.ImmediateLoad);
            assetsReady = true;
        }
        catch (Exception exception)
        {
            customMouseText = null;
            customDeathText = null;
            assetsReady = false;
            Mod.Logger.Error("Fran's custom fonts could not be loaded. Terraria's current fonts will remain active.", exception);
        }
    }

    public override void PostSetupContent() => RefreshReplacementState();

    public override void UpdateUI(GameTime gameTime) => RefreshReplacementState();

    public override void PostUpdateEverything() => RefreshReplacementState();

    public override void Unload()
    {
        RestorePreviousFonts();
        customMouseText = null;
        customDeathText = null;
        previousMouseText = null;
        previousDeathText = null;
        assetsReady = false;
    }

    private void RefreshReplacementState()
    {
        if (!assetsReady)
            return;

        bool enabled = FranClientConfig.Instance?.EnableCustomFont ?? true;
        if (enabled)
            ApplyCustomFonts();
        else
            RestorePreviousFonts();
    }

    private void ApplyCustomFonts()
    {
        if (replacementApplied)
            return;

        previousMouseText = FontAssets.MouseText;
        previousDeathText = FontAssets.DeathText;
        FontAssets.MouseText = customMouseText;
        FontAssets.DeathText = customDeathText;
        replacementApplied = true;
    }

    private void RestorePreviousFonts()
    {
        if (!replacementApplied)
            return;

        // Restore only assets that Fran still owns. If another enabled mod replaced either
        // global font after Fran, unloading or disabling Fran must not overwrite that mod.
        if (ReferenceEquals(FontAssets.MouseText, customMouseText) && previousMouseText is not null)
            FontAssets.MouseText = previousMouseText;
        if (ReferenceEquals(FontAssets.DeathText, customDeathText) && previousDeathText is not null)
            FontAssets.DeathText = previousDeathText;

        previousMouseText = null;
        previousDeathText = null;
        replacementApplied = false;
    }
}
