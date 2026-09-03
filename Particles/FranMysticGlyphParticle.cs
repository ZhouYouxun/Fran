using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FranMod.Particles;


// Five hand-drawn glyphs supplied by the project author. They use AlphaBlend so the fine white
// strokes remain stable on bright backgrounds; a separate bloom can be layered by the caller.
internal sealed class FranMysticGlyphParticle : FranParticle
{
    private readonly int glyphIndex;
    private readonly float startSize;
    private readonly float endSize;
    private readonly float spin;
    private readonly float gravity;

    public override bool Additive => false;

    public FranMysticGlyphParticle(Vector2 position, Vector2 velocity, Color color, int glyphIndex,
        float startSize, float endSize, int lifetime, float rotation = 0f, float spin = 0f,
        float gravity = 0f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.glyphIndex = Math.Clamp(glyphIndex, 1, 5);
        this.startSize = Math.Max(1f, startSize);
        this.endSize = Math.Max(1f, endSize);
        Lifetime = Math.Max(2, lifetime);
        Rotation = rotation;
        this.spin = spin;
        this.gravity = gravity;
    }

    protected override void Update()
    {
        Velocity *= 0.96f;
        Velocity.Y += gravity;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float appear = MathHelper.Clamp(Completion / 0.16f, 0f, 1f);
        float disappear = MathHelper.Clamp((1f - Completion) / 0.28f, 0f, 1f);
        float fade = appear * disappear;
        float size = MathHelper.Lerp(startSize, endSize, 1f - MathF.Pow(1f - Completion, 2f));
        FranParticleAssets.DrawPixels(spriteBatch, "SoftBloom", Position,
            Color * (fade * 0.18f), Rotation, Vector2.One * size * 1.55f);
        string glyphTexture = glyphIndex == 1 ? nameof(FranMysticGlyphParticle) : $"FranMysticGlyph{glyphIndex}";
        FranParticleAssets.DrawPixels(spriteBatch, glyphTexture, Position,
            Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, 0.48f) * (fade * 0.92f),
            Rotation, Vector2.One * size);
    }
}
