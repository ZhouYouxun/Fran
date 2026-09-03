using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FranMod.Particles;


// A true circular pulse rather than a directional impact oval. startSize/endSize may still be
// elliptical when an attack needs perspective or forward compression.
internal sealed class FranRingPulseParticle : FranParticle
{
    private readonly Vector2 startSize;
    private readonly Vector2 endSize;
    private readonly float spin;
    private readonly float opacity;

    public FranRingPulseParticle(Vector2 position, Vector2 velocity, Color color, Vector2 startSize,
        Vector2 endSize, int lifetime, float rotation = 0f, float spin = 0f, float opacity = 1f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.startSize = startSize;
        this.endSize = endSize;
        Lifetime = Math.Max(2, lifetime);
        Rotation = rotation;
        this.spin = spin;
        this.opacity = MathHelper.Clamp(opacity, 0f, 1.5f);
    }

    protected override void Update()
    {
        Velocity *= 0.94f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float appear = MathHelper.Clamp(Completion / 0.08f, 0f, 1f);
        float fade = appear * MathF.Pow(1f - Completion, 1.42f) * opacity;
        float eased = 1f - MathF.Pow(1f - Completion, 2.6f);
        Vector2 size = Vector2.Lerp(startSize, endSize, eased);
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranRingPulseParticle), Position,
            Color * (fade * 0.70f), Rotation, size);
        FranParticleAssets.DrawPixels(spriteBatch, "BloomRing", Position,
            Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, 0.62f) * (fade * 0.42f),
            -Rotation * 0.55f, size * 0.88f);
    }
}
