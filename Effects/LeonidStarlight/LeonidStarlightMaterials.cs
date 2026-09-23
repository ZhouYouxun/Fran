using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Effects.LeonidStarlight;

// Original mathematical support shapes, generated once on the drawing thread.
// Only StarMote and StarShard are file-backed artwork for this effect.
internal static class LeonidStarlightMaterials
{
    private static Texture2D glow;
    private static Texture2D ring;

    internal static Texture2D Glow => glow ??= Create(ringShape: false);
    internal static Texture2D Ring => ring ??= Create(ringShape: true);

    private static Texture2D Create(bool ringShape)
    {
        const int size = 128;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = (new Vector2(x + 0.5f, y + 0.5f) - new Vector2(size / 2f)) / (size / 2f);
                float radius = p.Length();
                float intensity = ringShape
                    ? MathF.Exp(-MathF.Pow((radius - 0.72f) / 0.035f, 2f))
                    : MathF.Exp(-radius * radius * 8f) * MathHelper.Clamp((1f - radius) * 8f, 0f, 1f);
                // Premultiplied white, matching tML's PNG upload and additive batch.
                pixels[y * size + x] = Color.White * intensity;
            }
        }
        Texture2D texture = new(Main.graphics.GraphicsDevice, size, size);
        texture.SetData(pixels);
        return texture;
    }

    internal static void Release()
    {
        Texture2D oldGlow = glow;
        Texture2D oldRing = ring;
        glow = null;
        ring = null;
        if (oldGlow is not null || oldRing is not null)
            Main.QueueMainThreadAction(() =>
            {
                oldGlow?.Dispose();
                oldRing?.Dispose();
            });
    }
}
