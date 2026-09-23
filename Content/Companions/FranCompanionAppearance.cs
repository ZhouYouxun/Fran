using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Companions;

internal static class FranCompanionAppearance
{
    // Character artwork has ONE integration point. Supply tML equipment sheets here:
    // Textures/角色图/Fran_Head.png, Fran_Body.png, Fran_Legs.png (see that folder's README).
    // Each missing sheet independently falls back to vanilla titanium vanity armor.
    private const string Root = "Fran/Textures/角色图/";
    private static int head = -1, body = -1, legs = -1;

    internal static void Load(Mod mod)
    {
        head = Register(mod, "Fran_Head", EquipType.Head);
        body = Register(mod, "Fran_Body", EquipType.Body);
        legs = Register(mod, "Fran_Legs", EquipType.Legs);
    }

    private static int Register(Mod mod, string name, EquipType type) =>
        ModContent.HasAsset(Root + name)
            ? EquipLoader.AddEquipTexture(mod, Root + name, type, name: name)
            : -1;

    internal static void Unload() => head = body = legs = -1;

    internal static void SetStaticDefaults()
    {
        if (head >= 0)
        {
            ArmorIDs.Head.Sets.DrawHead[head] = false;
            ArmorIDs.Head.Sets.DrawFullHair[head] = false;
            ArmorIDs.Head.Sets.DrawHatHair[head] = false;
        }
        if (body >= 0)
        {
            ArmorIDs.Body.Sets.HidesTopSkin[body] = true;
            ArmorIDs.Body.Sets.HidesArms[body] = true;
        }
        if (legs >= 0)
            ArmorIDs.Legs.Sets.HidesBottomSkin[legs] = true;
    }

    internal static void ApplyPlayerAppearance(Player actor)
    {
        actor.head = head >= 0 ? head : ContentSamples.ItemsByType[ItemID.CatEars].headSlot;
        actor.body = body >= 0 ? body : ContentSamples.ItemsByType[ItemID.FamiliarShirt].bodySlot;
        actor.legs = legs >= 0 ? legs : ContentSamples.ItemsByType[ItemID.FamiliarPants].legSlot;
    }

    internal static void Apply(Player actor)
    {
        actor.head = head >= 0 ? head : ContentSamples.ItemsByType[ItemID.TitaniumMask].headSlot;
        actor.body = body >= 0 ? body : ContentSamples.ItemsByType[ItemID.TitaniumBreastplate].bodySlot;
        actor.legs = legs >= 0 ? legs : ContentSamples.ItemsByType[ItemID.TitaniumLeggings].legSlot;
        actor.wings = actor.wingsLogic = ContentSamples.ItemsByType[ItemID.AngelWings].wingSlot;
        actor.wingFrame = (int)(Main.GameUpdateCount / 6 % 4);
        actor.wingTime = actor.wingTimeMax = 180;
    }

    internal static void Draw(FranCompanion companion)
    {
        Player actor = companion.Actor;
        using var scope = new FranCompanionContext.Scope(companion, simulation: false);
        // PlayerRenderer owns its batches, just as the MoreAIPlayer full-body preview does.
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
            Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
            null, Main.GameViewMatrix.TransformationMatrix);
        try
        {
            using var playerOverride = new Main.CurrentPlayerOverride(actor);
            Main.PlayerRenderer.DrawPlayer(Main.Camera, actor, actor.position, 0f, Vector2.Zero);
        }
        finally
        {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer,
                null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
