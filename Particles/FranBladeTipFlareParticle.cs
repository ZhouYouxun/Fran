using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


// The release marker at the blade tip. Its long axis follows the cutting motion while the short
// axis follows the blade, making the flare belong to this swing rather than float like a generic star.
internal sealed class FranBladeTipFlareParticle : FranParticle
{
    private readonly Vector2 cutDirection;
    private readonly Vector2 bladeDirection;
    private readonly float intensity;

    public override bool Important => true;

    public FranBladeTipFlareParticle(Vector2 position, Vector2 cutDirection, Vector2 bladeDirection,
        Color color, float intensity, int lifetime)
    {
        Position = position;
        this.cutDirection = cutDirection.SafeNormalize(Vector2.UnitY);
        this.bladeDirection = bladeDirection.SafeNormalize(Vector2.UnitX);
        Color = color;
        this.intensity = intensity;
        Lifetime = lifetime;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float envelope = MathF.Sin(Completion * MathHelper.Pi);
        float expansion = 1f - MathF.Pow(1f - Completion, 3f);
        float longRadius = MathHelper.Lerp(10f, 54f, expansion) * intensity;
        float shortRadius = MathHelper.Lerp(5f, 24f, expansion) * intensity;

        FranParticleAssets.DrawPixels(spriteBatch, "BloomCircle", Position,
            Color * (0.58f * envelope), 0f, Vector2.One * (24f + 26f * intensity) * envelope);
        DrawLocalSpark(spriteBatch, cutDirection, longRadius * 2f, 7.2f * intensity,
            Color * (0.86f * envelope));
        DrawLocalSpark(spriteBatch, cutDirection, longRadius * 1.42f, 2.1f * intensity,
            Microsoft.Xna.Framework.Color.White * (0.94f * envelope));
        DrawLocalSpark(spriteBatch, bladeDirection, shortRadius * 2f, 5.0f * intensity,
            Color * (0.68f * envelope));
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranBladeTipFlareParticle), Position,
            Microsoft.Xna.Framework.Color.White * (0.72f * envelope), cutDirection.ToRotation(),
            new Vector2(28f + expansion * 34f, 16f + expansion * 10f) * intensity);
    }

    private void DrawLocalSpark(SpriteBatch spriteBatch, Vector2 direction, float length, float width, Color color)
    {
        FranParticleAssets.DrawPixels(spriteBatch, "GlowSpark", Position, color,
            direction.ToRotation() + MathHelper.PiOver2,
            new Vector2(MathHelper.Clamp(width, 1f, 18f), MathHelper.Clamp(length, 2f, 132f)));
    }
}
