using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

// A short-lived pair of soft blooms: blue body and blue-white center. The light
// contracts across its width and stretches along its drift, without steering the core.
internal sealed class FranMindCoreBloomParticle : FranParticle
{
    private readonly Vector2 initialSize;

    private FranMindCoreBloomParticle(Vector2 position, Vector2 velocity, Vector2 size,
        Color color, int lifetime)
    {
        Position = position;
        Velocity = velocity;
        initialSize = size;
        Color = color;
        Lifetime = lifetime;
        Rotation = velocity.ToRotation() + MathHelper.PiOver2;
    }

    // Shared by the live emitter and the offline GPU preview. Pixel dimensions keep
    // the light independent of the resolution of Fran's existing bloom texture.
    internal static void CreatePair(Vector2 position, Vector2 velocity, float age,
        float visualScale, out FranMindCoreBloomParticle body, out FranMindCoreBloomParticle core)
    {
        float appearance = Math.Clamp(age / 5f, 0f, 1f);
        Color blue = Color.Lerp(new Color(25, 98, 255), new Color(46, 179, 255), appearance);
        float size = Math.Clamp(visualScale, 0f, 3f);
        Vector2 drift = -velocity * 0.05f;
        body = new(position, drift,
            new Vector2(1.1f - 0.4f * appearance, 0.8f + 0.6f * appearance) *
            MathHelper.Lerp(130f, 70f, appearance) * size, blue * 0.8f, 7);
        core = new(position, drift, new Vector2(0.7f, 1.4f) *
            MathHelper.Lerp(70f, 40f, appearance) * size, Color.Lerp(blue, Color.White, 0.6f), 5);
    }

    protected override void Update()
    {
        Velocity *= 0.95f;
        Scale *= 0.95f;
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
    }

    public override void Draw(SpriteBatch spriteBatch) =>
        DrawBloom(spriteBatch, FranParticleAssets.Get("CL_BloomCircle"));

    internal void DrawBloom(SpriteBatch spriteBatch, Texture2D bloom)
    {
        float fade = 1f - MathF.Pow(Completion, 3f);
        Vector2 size = initialSize * Scale * new Vector2(MathF.Pow(0.88f, Time), MathF.Pow(1.12f, Time));
        spriteBatch.Draw(bloom, Position - Main.screenPosition, null, Color * (fade * fade), Rotation,
            new Vector2(bloom.Width, bloom.Height) * 0.5f,
            size / new Vector2(bloom.Width, bloom.Height), SpriteEffects.None, 0f);
    }
}
