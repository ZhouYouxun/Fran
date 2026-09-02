using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

// Paired with Particles/FranSmallBloomParticle.png, copied from Calamity with the author's permission.
internal sealed class FranSmallBloomParticle : FranParticle
{
    private readonly float startSize;
    private readonly float endSize;

    public FranSmallBloomParticle(Vector2 position, Vector2 velocity, Color color, float startSize,
        float endSize, int lifetime)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.startSize = MathHelper.Clamp(startSize, 3f, 40f);
        this.endSize = MathHelper.Clamp(endSize, 1f, 32f);
        Lifetime = Math.Max(4, lifetime);
        Rotation = velocity.ToRotation();
    }

    protected override void Update()
    {
        Velocity *= 0.91f;
        Rotation += 0.035f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Sin(Completion * MathHelper.Pi);
        float smooth = Completion * Completion * (3f - 2f * Completion);
        float size = MathHelper.Lerp(startSize, endSize, smooth);
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranSmallBloomParticle), Position,
            Color * (0.70f * fade), Rotation, Vector2.One * size);
    }
}
