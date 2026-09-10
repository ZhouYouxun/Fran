using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FranMod.Particles;


// A single-texture circle pulse for nova projectiles. Unlike FranBloomParticle it has
// no SoftBloom/HardBloom backing plate, matching the clean circular body of the reference nova.
internal sealed class FranBloomCirclePulseParticle : FranParticle
{
    private readonly float startSize;
    private readonly float endSize;
    private readonly float opacity;
    private readonly float drag;
    private readonly float spin;

    public FranBloomCirclePulseParticle(Vector2 position, Vector2 velocity, Color color,
        float startSize, float endSize, int lifetime, float opacity = 1f, float drag = 0.92f,
        float spin = 0f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.startSize = Math.Max(1f, startSize);
        this.endSize = Math.Max(1f, endSize);
        Lifetime = Math.Max(2, lifetime);
        this.opacity = MathHelper.Clamp(opacity, 0f, 1.5f);
        this.drag = MathHelper.Clamp(drag, 0f, 1.04f);
        this.spin = spin;
        Rotation = velocity.LengthSquared() > 0.01f ? MathF.Atan2(velocity.Y, velocity.X) : 0f;
    }

    protected override void Update()
    {
        Velocity *= drag;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float appear = MathHelper.Clamp(Completion / 0.10f, 0f, 1f);
        float fade = appear * MathF.Pow(1f - Completion, 1.35f) * opacity;
        float eased = 1f - MathF.Pow(1f - Completion, 2.4f);
        float size = MathHelper.Lerp(startSize, endSize, eased);
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranBloomCirclePulseParticle), Position,
            Color * fade, Rotation, Vector2.One * size);
    }
}
