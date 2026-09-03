using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


// A hit-specific directional seal: expanding ring, cutting cross, and hot center. It is emitted only
// on actual contact, so impact has a distinct visual sentence instead of merely increasing spark count.
internal sealed class FranBladeImpactCrossParticle : FranParticle
{
    private readonly Vector2 direction;
    private readonly float intensity;

    public override bool Important => true;

    public FranBladeImpactCrossParticle(Vector2 position, Vector2 direction, Color color, float intensity,
        int lifetime)
    {
        Position = position;
        this.direction = direction.SafeNormalize(Vector2.UnitX);
        Color = color;
        this.intensity = intensity;
        Lifetime = lifetime;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.35f);
        float expansion = 1f - MathF.Pow(1f - Completion, 3f);
        float radius = MathHelper.Lerp(8f, 46f, expansion) * intensity;
        Vector2 cross = direction.RotatedBy(MathHelper.PiOver2);
        Vector2 diagonal = direction.RotatedBy(MathHelper.PiOver4);

        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranBladeImpactCrossParticle), Position,
            Color * (0.78f * fade), direction.ToRotation(),
            new Vector2(0.16f + expansion * 0.62f, 0.08f + expansion * 0.24f) * intensity);
        DrawAxis(spriteBatch, direction, radius, Color * (0.92f * fade), 5.2f * intensity);
        DrawAxis(spriteBatch, cross, radius * 0.54f,
            Microsoft.Xna.Framework.Color.White * (0.96f * fade), 2.2f * intensity);
        DrawAxis(spriteBatch, diagonal, radius * 0.35f, Color * (0.66f * fade), 2f * intensity);
        FranParticleAssets.DrawPixels(spriteBatch, "BloomCircle", Position,
            Microsoft.Xna.Framework.Color.White * (0.84f * fade), 0f,
            Vector2.One * (24f + 18f * intensity) * fade);
    }

    private void DrawAxis(SpriteBatch spriteBatch, Vector2 axis, float radius, Color color, float width)
    {
        FranParticleAssets.DrawPixels(spriteBatch, "GlowSpark", Position, color,
            axis.ToRotation() + MathHelper.PiOver2,
            new Vector2(MathHelper.Clamp(width, 1f, 18f), MathHelper.Clamp(radius * 2f, 2f, 132f)));
    }
}
