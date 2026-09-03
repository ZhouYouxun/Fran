using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


// A short-lived strand shed from the cutting edge. Unlike a generic spark, it preserves the
// slash's direction and leaves a two-layer filament that reads as a piece of the blade light.
internal sealed class FranBladeEdgeMoteParticle : FranParticle
{
    private readonly float startLength;
    private readonly float startWidth;
    private readonly float drag;

    public FranBladeEdgeMoteParticle(Vector2 position, Vector2 velocity, Vector2 direction, Color color,
        float length, float width, int lifetime, float drag = 0.93f)
    {
        Position = position;
        Velocity = velocity;
        Rotation = direction.SafeNormalize(Vector2.UnitX).ToRotation();
        Color = color;
        startLength = length;
        startWidth = width;
        Lifetime = lifetime;
        this.drag = drag;
    }

    protected override void Update()
    {
        Velocity *= drag;
        Rotation += Math.Sign(Velocity.X) * 0.012f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.25f);
        float length = startLength * MathHelper.Lerp(1f, 0.28f, Completion);
        float width = Math.Max(1.2f, startWidth * MathHelper.Lerp(2.8f, 0.8f, Completion));

        // Calamity-style squished sprite particle: its size is local and independently capped by
        // DrawPixels. It never constructs a segment from two world-space endpoints.
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranBladeEdgeMoteParticle), Position,
            Color * (0.78f * fade), Rotation + MathHelper.PiOver2, new Vector2(width * 2.8f, length));
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranBladeEdgeMoteParticle), Position,
            Microsoft.Xna.Framework.Color.White * (0.92f * fade), Rotation + MathHelper.PiOver2,
            new Vector2(Math.Max(1f, width * 0.72f), length * 0.82f));
    }
}
