using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

internal sealed class FranHalleySparkParticle : FranParticle
{
    private readonly Color initialColor;

    public FranHalleySparkParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime)
    {
        Position = position;
        Velocity = velocity;
        Color = initialColor = color;
        Scale = scale;
        Lifetime = lifetime;
    }

    protected override void Update()
    {
        Scale *= 0.95f;
        Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(Completion, 3f));
        Velocity *= 0.95f;
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D texture = FranParticleAssets.GetParticle(nameof(FranHalleySparkParticle));
        Vector2 scale = new Vector2(0.5f, 1.6f) * Scale;
        spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation,
            texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation,
            texture.Size() * 0.5f, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0f);
    }
}
