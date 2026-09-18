using System;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace FranMod.Core.Graphics;

// Adapted from SussurroVisuals: procedural wings, veins, blue glow and a pale core.
// Uses AlphaBlend with zero alpha for glow, without changing SpriteBatch state.
internal static class FranResonanceVisuals
{
    internal static readonly Color Blue = new(80, 205, 255);
    internal static readonly Color Cyan = new(135, 235, 255);
    internal static readonly Color Pale = new(235, 250, 255);
    private static Texture2D Glow => FranParticleAssets.Get("FranResonanceGlow");

    internal static bool Visible(Vector2 position) => !Main.dedServ
        && Math.Abs(position.X - Main.screenPosition.X - Main.screenWidth * .5f) < Main.screenWidth * .5f + 160f
        && Math.Abs(position.Y - Main.screenPosition.Y - Main.screenHeight * .5f) < Main.screenHeight * .5f + 160f;

    private static Color Light(Color color, float opacity)
    {
        color *= MathHelper.Clamp(opacity, 0f, 1f);
        color.A = 0;
        return color;
    }

    internal static void Soft(Vector2 center, Vector2 size, Color color, float opacity, float rotation = 0f)
    {
        Texture2D texture = Glow;
        Main.spriteBatch.Draw(texture, center - Main.screenPosition, null, Light(color, opacity), rotation,
            texture.Size() * .5f, size / texture.Size(), SpriteEffects.None, 0f);
    }

    internal static void Line(Vector2 start, Vector2 end, Color color, float width, float opacity, float pigment = 0f)
    {
        Vector2 delta = end - start;
        float length = delta.Length();
        if (!float.IsFinite(length) || length < .01f)
            return;
        Color tint = Light(color, opacity);
        tint.A = (byte)(MathHelper.Clamp(pigment, 0f, 1f) * 255f);
        // MagicPixel 是纹理图集，必须限定为一个源像素，不能把整张纹理当作线条拉伸。
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, start - Main.screenPosition,
            new Rectangle(0, 0, 1, 1), tint, delta.ToRotation(), new Vector2(0f, .5f),
            new Vector2(length, Math.Max(.1f, width)), SpriteEffects.None, 0f);
    }

    internal static void Ring(Vector2 center, Vector2 radius, float rotation, float phase, Color color,
        float opacity, float width = 1f, float petals = 0f)
    {
        int segments = petals == 0f ? 64 : 112;
        Vector2 previous = Vector2.Zero;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * MathHelper.TwoPi / segments;
            float wave = petals == 0f ? 1f : .76f + .24f * MathF.Cos(angle * petals + phase);
            Vector2 current = center + (angle.ToRotationVector2() * radius * wave).RotatedBy(rotation);
            if (i > 0)
                Line(previous, current, color, width, opacity * (.65f + .35f * MathF.Sin(angle * 3f + phase)));
            previous = current;
        }
    }

    internal static void Star(Vector2 center, float radius, float rotation, float opacity, Color color)
    {
        Soft(center, new Vector2(radius * 4f), Blue, opacity * .35f);
        Soft(center, new Vector2(radius * 2f, radius * .32f), color, opacity, rotation);
        Soft(center, new Vector2(radius * .32f, radius * 2.6f), color, opacity, rotation);
    }

    internal static void DrawButterfly(Vector2 center, float rotation, float age, float size, float opacity)
    {
        float unfold = MathHelper.SmoothStep(.18f, 1f, MathHelper.Clamp(age / 7f, 0f, 1f));
        float flutter = unfold * (.42f + .58f * Math.Abs(MathF.Sin(age * .22f)));
        Soft(center, new Vector2(74f, 54f) * size, Blue, opacity * .4f, rotation);
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 upper = new Vector2(side * 11f * flutter, -6f) * size;
            Vector2 lower = new Vector2(side * 8f * flutter, 7f) * size;
            Soft(center + upper.RotatedBy(rotation), new Vector2(25f * flutter, 29f) * size,
                Cyan, opacity * .9f, rotation + side * .55f);
            Soft(center + lower.RotatedBy(rotation), new Vector2(20f * flutter, 20f) * size,
                Blue, opacity, rotation - side * .55f);
            // 闭合的蝶翼外轮廓与三条叶脉，在亮背景上仍能辨认扑翼。
            Vector2 last = center;
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f * MathF.PI;
                float waist = .38f + .62f * MathF.Sqrt(Math.Abs(MathF.Cos(t)));
                Vector2 wing = new(side * MathF.Sin(t) * (16f + 6f * MathF.Cos(t)) * flutter * waist,
                    -MathF.Sin(t * 2f) * 17f);
                Vector2 next = center + (wing * size).RotatedBy(rotation);
                if (i > 0)
                    Line(last, next, Cyan, 1.1f * size, opacity * .85f);
                last = next;
            }
            for (int vein = 0; vein < 3; vein++)
            {
                Vector2 tip = new Vector2(side * (15f - vein * 2f) * flutter, -12f + vein * 10f) * size;
                Line(center, center + tip.RotatedBy(rotation), Pale, .75f * size, opacity * .6f);
            }
        }
        Soft(center, new Vector2(5f, 29f) * size, Pale, opacity, rotation);
        for (int side = -1; side <= 1; side += 2)
            Line(center + new Vector2(0f, -8f * size).RotatedBy(rotation),
                center + new Vector2(side * 4f, -15f).RotatedBy(rotation) * size, Cyan, .7f * size, opacity * .7f);
        Star(center + new Vector2(0f, -8f * size).RotatedBy(rotation), 4f * size, rotation, opacity, Pale);
    }

    internal static void DrawHealSeal(Vector2 center, float age, float size)
    {
        float p = MathHelper.Clamp(age / 40f, 0f, 1f);
        float fade = MathF.Sin(MathF.PI * p) * (1f - p * .45f);
        float radius = (24f + 38f * (1f - MathF.Pow(1f - p, 3f))) * size;
        Soft(center, new Vector2(radius * 3f), Blue, fade * .23f);
        Ring(center, new Vector2(radius, radius * .85f), -.2f, age * .05f, Blue, fade * .8f, 1.6f);
        Ring(center, new Vector2(radius * .82f), .2f, -age * .07f, Cyan, fade * .8f, 1.1f, 6f);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * MathHelper.TwoPi / 6f + age * .018f;
            Star(center + angle.ToRotationVector2() * radius * .83f, (3.5f + 2f * fade) * size,
                angle, fade * .8f, Cyan);
        }
    }
}
