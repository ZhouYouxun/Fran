using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace FranMod.Core;

// One source of truth for ShiFu's real body texture and world-progression scale.
public static class FranShiFuVisuals
{
    public const string TexturePath = "Fran/Textures/师父本体";
    public const string GhostTexturePath = "Fran/Textures/师父本体Ghost";
    private const float SuppliedTextureOriginalScale = 0.50f;
    private const float RequestedBaseEnlargement = 1.50f;

    public static float WorldSizeMultiplier =>
        RequestedBaseEnlargement * FranBalance.GetCoreSizeMultiplier();

    // 0.50 × 1.50 = 0.75 before the world-progression growth is applied.
    public static float TextureScale => SuppliedTextureOriginalScale * WorldSizeMultiplier;

    public static Texture2D Texture => ModContent.Request<Texture2D>(TexturePath).Value;
    public static Texture2D GhostTexture => ModContent.Request<Texture2D>(GhostTexturePath).Value;
}
