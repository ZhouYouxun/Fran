using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FranMod.Particles;

// 复用共享斩痕纹理：一次短促的切开与碎裂，没有周期闪烁或大面积白光。
internal sealed class FranPhantomCutParticle : FranParticle
{
    public override bool Important => true;

    public FranPhantomCutParticle(Vector2 center, float rotation, float scale)
    {
        Position = center;
        Rotation = rotation;
        Scale = scale;
        Lifetime = 10;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.8f) * FranMod.Core.Graphics.FranPhantomVisuals.ImpactIntensity;
        float expansion = 1f - MathF.Pow(1f - Completion, 3f);
        Vector2 size = new Vector2(70f + expansion * 42f, 30f + expansion * 12f) * Scale;
        FranParticleAssets.DrawPixels(spriteBatch, "SlashSmear", Position,
            new Color(105, 180, 220) * (0.66f * fade), Rotation - 0.65f, size);
        FranParticleAssets.DrawPixels(spriteBatch, "SlashSmear", Position,
            new Color(200, 230, 245) * (0.48f * fade), Rotation + 1.1f, size * new Vector2(0.72f, 0.45f));
        FranParticleAssets.DrawPixels(spriteBatch, "VerticalSmearRagged", Position,
            new Color(115, 175, 205) * (0.28f * fade), Rotation,
            new Vector2(32f + expansion * 46f, 22f + expansion * 28f) * Scale);
    }
}
