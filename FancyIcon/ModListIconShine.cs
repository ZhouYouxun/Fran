using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using FranMod.Content.Config;

namespace FranMod.FancyIcon;

/// <summary>Adds a soft, runtime-generated diagonal shine to Fran's Mods-menu icon.</summary>
[Autoload(Side = ModSide.Client)]
public sealed class ModListIconShine : ModSystem
{
    private const string OurModName = "Fran";

    private static readonly FieldInfo UIImageTextureField = typeof(UIImage).GetField(
        "_texture", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo UIImageNonReloadingTextureField = typeof(UIImage).GetField(
        "_nonReloadingTexture", BindingFlags.NonPublic | BindingFlags.Instance);

    private ILHook uiModItemOnInitializeHook;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        try
        {
            Type uiModItemType = typeof(ModLoader).Assembly.GetType(
                "Terraria.ModLoader.UI.UIModItem", throwOnError: true);
            MethodInfo onInitializeMethod = uiModItemType.GetMethod(
                "OnInitialize", BindingFlags.Public | BindingFlags.Instance);
            uiModItemOnInitializeHook = new ILHook(onInitializeMethod, ReplaceIconWithShineIcon);
        }
        catch (Exception exception)
        {
            // UIModItem is internal tModLoader UI. If its layout changes in a future version,
            // keep Fran loadable and simply fall back to the ordinary static icon.
            Mod.Logger.Warn($"{OurModName}: could not enable the mod-list icon shine.", exception);
        }
    }

    public override void Unload()
    {
        uiModItemOnInitializeHook?.Dispose();
        uiModItemOnInitializeHook = null;

        // Runtime Texture2D disposal must return to the graphics thread during a mod reload.
        if (!Main.dedServ)
            Main.QueueMainThreadAction(ShineEffectToolkit.Unload);
    }

    private static void ReplaceIconWithShineIcon(ILContext il)
    {
        ILCursor cursor = new(il);
        Type uiModItemType = typeof(ModLoader).Assembly.GetType(
            "Terraria.ModLoader.UI.UIModItem", throwOnError: true);
        FieldInfo modIconField = uiModItemType.GetField(
            "_modIcon", BindingFlags.NonPublic | BindingFlags.Instance);
        PropertyInfo modNameProperty = uiModItemType.GetProperty(
            "ModName", BindingFlags.Public | BindingFlags.Instance);

        cursor.GotoNext(MoveType.Before, instruction => instruction.MatchStfld(modIconField));
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.Emit(OpCodes.Callvirt, modNameProperty.GetMethod);
        cursor.EmitDelegate((UIImage originalIcon, string modName) =>
        {
            if (modName != OurModName ||
                !(FranClientConfig.Instance?.EnableModIconFlicker ?? true))
                return (UIElement)originalIcon;

            return new ShineIconElement(originalIcon);
        });
    }

    private sealed class ShineIconElement : UIElement
    {
        private readonly UIImage originalIcon;
        private readonly Texture2D originalIconTexture;
        private readonly ShineEffectToolkit.State shine = new();

        public ShineIconElement(UIImage originalIcon)
        {
            this.originalIcon = originalIcon;
            originalIconTexture = GetTexture(originalIcon);

            Left = originalIcon.Left;
            Top = originalIcon.Top;
            Width = originalIcon.Width;
            Height = originalIcon.Height;

            originalIcon.Left.Set(0f, 0f);
            originalIcon.Top.Set(0f, 0f);
            originalIcon.Width.Set(0f, 1f);
            originalIcon.Height.Set(0f, 1f);
            Append(originalIcon);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (FranClientConfig.Instance?.EnableModIconFlicker ?? true)
                ShineEffectToolkit.Advance(shine);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);
            if (!(FranClientConfig.Instance?.EnableModIconFlicker ?? true))
                return;
            ShineEffectToolkit.Draw(spriteBatch, shine, originalIconTexture,
                originalIcon.GetDimensions().ToRectangle());
        }

        private static Texture2D GetTexture(UIImage image)
        {
            Asset<Texture2D> asset = UIImageTextureField?.GetValue(image) as Asset<Texture2D>;
            return asset?.Value ?? UIImageNonReloadingTextureField?.GetValue(image) as Texture2D;
        }
    }
}
