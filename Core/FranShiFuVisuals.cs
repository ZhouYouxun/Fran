using System;
using System.Buffers.Binary;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace FranMod.Core;

// One source of truth for ShiFu's real body texture and world-progression scale.
public static class FranShiFuVisuals
{
    public const string TexturePath = "Fran/Textures/大剑/师父";
    public const string GhostTexturePath = "Fran/Textures/大剑/师父ghost";
    public const string GlowTexturePath = "Fran/Textures/大剑/师父glow";
    private const string TextureFilePath = "Fran/Textures/大剑/师父.png";
    private const float ReferenceTextureDiagonal = 282.8427f;
    private const float ReferenceTextureScale = 0.335f;
    // The grip midpoint sits about 28 source pixels along each axis from the bottom-left origin.
    private const float GripInsetDiagonalFraction = 0.14142136f;
    private const float RequestedBaseEnlargement = 1.50f;
    private static readonly Lazy<(int Width, int Height)> SourceSize = new(ReadSourceSize, true);

    public static float WorldSizeMultiplier =>
        RequestedBaseEnlargement * FranBalance.GetCoreSizeMultiplier();

    public static float TextureDiagonal => Diagonal(SourceSize.Value.Width, SourceSize.Value.Height);
    public static float GripInsetPixels => TextureDiagonal * GripInsetDiagonalFraction;
    public static float BladeLengthFromGripPixels => TextureDiagonal - GripInsetPixels;

    // Normalize by the detected source diagonal so changing export resolution does not change
    // the in-game size. Read packaged image metadata without GPU access on dedicated servers.
    public static float TextureScale => ScaleForDiagonal(TextureDiagonal);
    public static float GripInset => GripInsetPixels * 1.05f * TextureScale;
    public static float TextureScaleFor(Texture2D texture) =>
        ScaleForDiagonal(Diagonal(texture.Width, texture.Height));
    public static float NormalizeScaleFor(Texture2D texture, float scale) =>
        scale * ReferenceTextureDiagonal / Math.Max(1f, Diagonal(texture.Width, texture.Height));

    public static Texture2D Texture => ModContent.Request<Texture2D>(TexturePath).Value;
    public static Texture2D GhostTexture => ModContent.Request<Texture2D>(GhostTexturePath).Value;
    public static Texture2D GlowTexture => ModContent.Request<Texture2D>(GlowTexturePath).Value;

    private static float ScaleForDiagonal(float diagonal) =>
        ReferenceTextureScale * WorldSizeMultiplier * ReferenceTextureDiagonal / Math.Max(1f, diagonal);

    private static float Diagonal(int width, int height) =>
        MathF.Sqrt((float)width * width + (float)height * height);

    private static (int Width, int Height) ReadSourceSize()
    {
        // tModLoader converts PNG assets to rawimg v1 during packaging:
        // little-endian version, width, height, then RGBA pixels.
        byte[] raw = ModContent.GetFileBytes(TexturePath + ".rawimg");
        if (raw is not null)
        {
            if (raw.Length < 12 || BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(0, 4)) != 1)
                throw new InvalidOperationException($"Invalid rawimg header for {TexturePath}.");
            int rawWidth = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(4, 4));
            int rawHeight = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(8, 4));
            if (rawWidth <= 0 || rawHeight <= 0 || (long)rawWidth * rawHeight * 4 != raw.Length - 12L)
                throw new InvalidOperationException($"Invalid rawimg dimensions for {TexturePath}.");
            return (rawWidth, rawHeight);
        }

        // Retain compatibility with resource providers that keep the original PNG.
        byte[] png = ModContent.GetFileBytes(TextureFilePath);
        if (png is null || png.Length < 24 || png[0] != 137 || png[1] != 80 ||
            png[2] != 78 || png[3] != 71 || png[4] != 13 || png[5] != 10 ||
            png[6] != 26 || png[7] != 10)
            throw new InvalidOperationException($"Unable to read PNG dimensions for {TextureFilePath}.");

        int width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException($"Invalid PNG dimensions for {TextureFilePath}.");

        return (width, height);
    }
}
