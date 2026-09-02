using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

internal sealed class FranHalleyPulseRingParticle : FranParticle
{
    private readonly float originalScale;
    private readonly float finalScale;
    private readonly Vector2 squish;
    private readonly Color baseColor;

    public FranHalleyPulseRingParticle(Vector2 position, Vector2 velocity, Color color, Vector2 squish,
        float rotation, float originalScale, float finalScale, int lifetime)
    {
        Position = position;
        Velocity = velocity;
        baseColor = color;
        this.squish = squish;
        Rotation = rotation;
        this.originalScale = originalScale;
        this.finalScale = finalScale;
        Scale = originalScale;
        Lifetime = lifetime;
    }

    protected override void Update()
    {
        float pulseProgress = 1f - MathF.Pow(1f - Completion, 4f);
        Scale = MathHelper.Lerp(originalScale, finalScale, pulseProgress);
        Velocity *= 0.95f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float opacity = MathF.Cos(Completion * MathHelper.PiOver2);
        Texture2D texture = FranParticleAssets.GetParticle(nameof(FranHalleyPulseRingParticle));
        spriteBatch.Draw(texture, Position - Main.screenPosition, null, baseColor * (opacity * opacity),
            Rotation, texture.Size() * 0.5f, Scale * squish, SpriteEffects.None, 0f);
    }
}
