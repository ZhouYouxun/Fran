using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

// Original single-frame arc. Geometry, tint, mirroring and lifetime supply variation, not an atlas.
internal sealed class FranArcParticle : FranParticle
{
    private readonly Vector2 pixels;
    private readonly SpriteEffects mirror;

    public FranArcParticle(Vector2 position, Vector2 velocity, Vector2 direction, Color color,
        float length, float width, int lifetime)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Lifetime = Math.Clamp(lifetime, 5, 22);
        Rotation = direction.SafeNormalize(Vector2.UnitY).ToRotation() - MathHelper.PiOver2;
        pixels = new Vector2(MathHelper.Clamp(width, 8f, 90f), MathHelper.Clamp(length, 12f, 180f));
        mirror = (Main.rand.NextBool() ? SpriteEffects.FlipHorizontally : SpriteEffects.None) |
            (Main.rand.NextBool() ? SpriteEffects.FlipVertically : SpriteEffects.None);
    }

    protected override void Update() => Velocity *= 0.86f;

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.45f);
        // Preserve the route/length while the filament cools. Shrinking both axes looks like confetti.
        Vector2 size = pixels * new Vector2(1f - Completion * 0.30f, 1f - Completion * 0.08f);
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranArcParticle), Position,
            Color * (fade * 0.88f), Rotation, size, effects: mirror);
        // Same geometry and origin: a shifted/scaled second arc would make a visibly doubled outline.
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranArcParticle), Position,
            Color.Lerp(Color, Color.White, 0.82f) * (fade * 0.28f), Rotation, size, effects: mirror);
    }
}
