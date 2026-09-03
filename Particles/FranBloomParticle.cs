using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FranMod.Particles;


// A reusable soft energy bloom. This is deliberately separate from sparks so attacks can stage
// anticipation, contact and afterglow with different expansion curves.
internal sealed class FranBloomParticle : FranParticle
{
    private readonly float startSize;
    private readonly float endSize;
    private readonly float opacity;
    private readonly float drag;
    private readonly bool hardCenter;

    public FranBloomParticle(Vector2 position, Vector2 velocity, Color color, float startSize,
        float endSize, int lifetime, float opacity = 1f, float drag = 0.92f, bool hardCenter = false)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.startSize = Math.Max(1f, startSize);
        this.endSize = Math.Max(1f, endSize);
        Lifetime = Math.Max(2, lifetime);
        this.opacity = MathHelper.Clamp(opacity, 0f, 1.5f);
        this.drag = MathHelper.Clamp(drag, 0f, 1.04f);
        this.hardCenter = hardCenter;
    }

    protected override void Update() => Velocity *= drag;

    public override void Draw(SpriteBatch spriteBatch)
    {
        float appear = MathHelper.Clamp(Completion / 0.1f, 0f, 1f);
        float fade = appear * MathF.Pow(1f - Completion, 1.65f) * opacity;
        float eased = 1f - MathF.Pow(1f - Completion, 3f);
        float size = MathHelper.Lerp(startSize, endSize, eased);
        FranParticleAssets.DrawPixels(spriteBatch, hardCenter ? "HardBloom" : nameof(FranBloomParticle), Position,
            Color * (fade * 0.72f), Rotation, Vector2.One * size);
        FranParticleAssets.DrawPixels(spriteBatch, "BloomCircle", Position,
            Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, hardCenter ? 0.72f : 0.38f) * (fade * 0.42f),
            -Rotation * 0.7f, Vector2.One * size * (hardCenter ? 0.34f : 0.48f));
    }
}
