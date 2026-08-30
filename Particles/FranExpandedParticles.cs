using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FranMod.Particles;

// A texture-authored pressure needle. Its silhouette, taper and internal highlight come from
// FranPressureNeedle instead of being approximated by a stretched square or a generic dust line.
internal sealed class FranNeedleParticle : FranParticle
{
    private readonly Vector2 startSize;
    private readonly Vector2 endSize;
    private readonly float drag;
    private readonly float spin;
    private readonly bool additive;

    public override bool Additive => additive;

    public FranNeedleParticle(Vector2 position, Vector2 velocity, Color color, Vector2 startSize,
        Vector2 endSize, int lifetime, float rotation, float drag = 0.94f, float spin = 0f,
        bool additive = true)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.startSize = startSize;
        this.endSize = endSize;
        Lifetime = Math.Max(2, lifetime);
        Rotation = rotation;
        this.drag = MathHelper.Clamp(drag, 0f, 1.04f);
        this.spin = spin;
        this.additive = additive;
    }

    protected override void Update()
    {
        Velocity *= drag;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float appear = MathHelper.Clamp(Completion / 0.12f, 0f, 1f);
        float fade = appear * (1f - Completion) * (1f - Completion);
        float eased = 1f - MathF.Pow(1f - Completion, 3f);
        Vector2 size = Vector2.Lerp(startSize, endSize, eased);
        FranParticleAssets.DrawOriginalPixels(spriteBatch, "FranPressureNeedle", Position,
            Color * fade, Rotation, size);
    }
}

// A reusable soft energy bloom. This is deliberately separate from sparks so attacks can stage
// anticipation, contact and afterglow with different expansion curves.
internal sealed class FranBloomParticle : FranParticle
{
    private readonly float startSize;
    private readonly float endSize;
    private readonly float opacity;
    private readonly float drag;
    private readonly bool hardCenter;

    public FranBloomParticle(Vector2 position, Vector2 velocity, Color color, float startSize,
        float endSize, int lifetime, float opacity = 1f, float drag = 0.92f, bool hardCenter = false)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.startSize = Math.Max(1f, startSize);
        this.endSize = Math.Max(1f, endSize);
        Lifetime = Math.Max(2, lifetime);
        this.opacity = MathHelper.Clamp(opacity, 0f, 1.5f);
        this.drag = MathHelper.Clamp(drag, 0f, 1.04f);
        this.hardCenter = hardCenter;
    }

    protected override void Update() => Velocity *= drag;

    public override void Draw(SpriteBatch spriteBatch)
    {
        float appear = MathHelper.Clamp(Completion / 0.1f, 0f, 1f);
        float fade = appear * MathF.Pow(1f - Completion, 1.65f) * opacity;
        float eased = 1f - MathF.Pow(1f - Completion, 3f);
        float size = MathHelper.Lerp(startSize, endSize, eased);
        FranParticleAssets.DrawPixels(spriteBatch, hardCenter ? "HardBloom" : "SoftBloom", Position,
            Color * (fade * 0.72f), Rotation, Vector2.One * size);
        FranParticleAssets.DrawPixels(spriteBatch, "BloomCircle", Position,
            Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, hardCenter ? 0.72f : 0.38f) * (fade * 0.42f),
            -Rotation * 0.7f, Vector2.One * size * (hardCenter ? 0.34f : 0.48f));
    }
}

// A true circular pulse rather than a directional impact oval. startSize/endSize may still be
// elliptical when an attack needs perspective or forward compression.
internal sealed class FranRingPulseParticle : FranParticle
{
    private readonly Vector2 startSize;
    private readonly Vector2 endSize;
    private readonly float spin;
    private readonly float opacity;

    public FranRingPulseParticle(Vector2 position, Vector2 velocity, Color color, Vector2 startSize,
        Vector2 endSize, int lifetime, float rotation = 0f, float spin = 0f, float opacity = 1f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.startSize = startSize;
        this.endSize = endSize;
        Lifetime = Math.Max(2, lifetime);
        Rotation = rotation;
        this.spin = spin;
        this.opacity = MathHelper.Clamp(opacity, 0f, 1.5f);
    }

    protected override void Update()
    {
        Velocity *= 0.94f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float appear = MathHelper.Clamp(Completion / 0.08f, 0f, 1f);
        float fade = appear * MathF.Pow(1f - Completion, 1.42f) * opacity;
        float eased = 1f - MathF.Pow(1f - Completion, 2.6f);
        Vector2 size = Vector2.Lerp(startSize, endSize, eased);
        FranParticleAssets.DrawPixels(spriteBatch, "HighResHollowCircleHardEdge", Position,
            Color * (fade * 0.70f), Rotation, size);
        FranParticleAssets.DrawPixels(spriteBatch, "BloomRing", Position,
            Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, 0.62f) * (fade * 0.42f),
            -Rotation * 0.55f, size * 0.88f);
    }
}

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
        FranParticleAssets.DrawPixels(spriteBatch, $"FranMysticGlyph{glyphIndex}", Position,
            Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, 0.48f) * (fade * 0.92f),
            Rotation, Vector2.One * size);
    }
}

// The subtractive-looking body of "black light". It must remain in AlphaBlend: black under
// Additive contributes zero light and becomes invisible. Colored coronae are separate particles.
internal sealed class FranBlackLightCoreParticle : FranParticle
{
    private readonly float startSize;
    private readonly float endSize;
    private readonly float opacity;
    private readonly float spin;

    public override bool Additive => false;
    public override bool Important => true;

    public FranBlackLightCoreParticle(Vector2 position, Vector2 velocity, float startSize,
        float endSize, int lifetime, float opacity = 0.78f, float rotation = 0f, float spin = 0f)
    {
        Position = position;
        Velocity = velocity;
        Color = new Color(3, 0, 8);
        this.startSize = Math.Max(1f, startSize);
        this.endSize = Math.Max(1f, endSize);
        Lifetime = Math.Max(2, lifetime);
        this.opacity = MathHelper.Clamp(opacity, 0f, 0.92f);
        Rotation = rotation;
        this.spin = spin;
    }

    protected override void Update()
    {
        Velocity *= 0.91f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float appear = MathHelper.Clamp(Completion / 0.1f, 0f, 1f);
        float fade = appear * MathF.Pow(1f - Completion, 1.18f) * opacity;
        float eased = 1f - MathF.Pow(1f - Completion, 2.35f);
        float size = MathHelper.Lerp(startSize, endSize, eased);
        FranParticleAssets.DrawPixels(spriteBatch, "HighResFoggyCircleHardEdge", Position,
            new Color(0, 0, 0) * fade, Rotation, Vector2.One * size);
        FranParticleAssets.DrawPixels(spriteBatch, "CircularSmearSmokey", Position,
            Color * (fade * 0.82f), -Rotation * 0.72f, Vector2.One * size * 0.76f);
    }
}
