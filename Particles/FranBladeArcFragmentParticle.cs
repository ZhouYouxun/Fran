using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


// A small crescent shed from the analytic blade surface. The curved smear is a local sprite with a
// hard pixel-size cap; it never constructs geometry from a chain of endpoint pairs.
internal sealed class FranBladeArcFragmentParticle : FranParticle
{
    private readonly float startRadius;
    private readonly float arcAngle;
    private readonly float startWidth;
    private readonly float spin;

    public FranBladeArcFragmentParticle(Vector2 position, Vector2 velocity, Vector2 direction, Color color,
        float radius, float arcAngle, float width, int lifetime, float spin)
    {
        Position = position;
        Velocity = velocity;
        Rotation = direction.SafeNormalize(Vector2.UnitX).ToRotation();
        Color = color;
        startRadius = MathHelper.Clamp(radius, 8f, 42f);
        this.arcAngle = MathHelper.Clamp(arcAngle, -1.4f, 1.4f);
        startWidth = MathHelper.Clamp(width, 0.8f, 5.5f);
        Lifetime = Math.Max(4, lifetime);
        this.spin = MathHelper.Clamp(spin, -0.16f, 0.16f);
    }

    protected override void Update()
    {
        Velocity *= 0.91f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.4f);
        float radius = startRadius * MathHelper.Lerp(0.72f, 1.18f, Completion);
        float contraction = MathHelper.Lerp(1f, 0.38f, Completion);
        float width = Math.Max(0.7f, startWidth * contraction);
        SpriteEffects flip = arcAngle < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranBladeArcFragmentParticle), Position,
            Color * (0.68f * fade), Rotation,
            new Vector2(radius * 2.15f, Math.Max(4f, width * 7.2f)), effects: flip);
        FranParticleAssets.DrawPixels(spriteBatch, "SlashNarrow", Position,
            Microsoft.Xna.Framework.Color.White * (0.82f * fade), Rotation,
            new Vector2(radius * 1.72f, Math.Max(1.2f, width * 1.05f)), effects: flip);
    }
}
