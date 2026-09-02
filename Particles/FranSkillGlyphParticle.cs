using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

// Paired with Particles/FranSkillGlyphParticle.png, sourced from the compact PulseStar material.
internal sealed class FranSkillGlyphParticle : FranParticle
{
    private readonly float spin;
    private readonly float baseScale;

    public FranSkillGlyphParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        baseScale = scale;
        Scale = scale;
        Lifetime = lifetime;
        spin = ((position.GetHashCode() & 1) == 0 ? 1f : -1f) * 0.14f;
        Rotation = velocity.LengthSquared() > 0.01f ? velocity.ToRotation() : 0f;
    }

    protected override void Update()
    {
        Velocity = Velocity.RotatedBy(spin * 0.08f) * 0.93f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Sin(Completion * MathHelper.Pi);
        float pulse = 0.82f + MathF.Sin(Time * 0.72f) * 0.18f;
        Vector2 size = Vector2.One * MathHelper.Clamp(baseScale * pulse * 18f, 5f, 34f);
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranSkillGlyphParticle), Position,
            Color * (0.72f * fade), Rotation, size);
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranSkillGlyphParticle), Position,
            Color.White * (0.48f * fade), -Rotation, size * 0.48f);
    }
}
