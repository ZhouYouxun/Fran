using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

// A short-lived strand shed from the cutting edge. Unlike a generic spark, it preserves the
// slash's direction and leaves a two-layer filament that reads as a piece of the blade light.
internal sealed class FranBladeEdgeMoteParticle : FranParticle
{
    private readonly float startLength;
    private readonly float startWidth;
    private readonly float drag;

    public FranBladeEdgeMoteParticle(Vector2 position, Vector2 velocity, Vector2 direction, Color color,
        float length, float width, int lifetime, float drag = 0.93f)
    {
        Position = position;
        Velocity = velocity;
        Rotation = direction.SafeNormalize(Vector2.UnitX).ToRotation();
        Color = color;
        startLength = length;
        startWidth = width;
        Lifetime = lifetime;
        this.drag = drag;
    }

    protected override void Update()
    {
        Velocity *= drag;
        Rotation += Math.Sign(Velocity.X) * 0.012f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.25f);
        float length = startLength * MathHelper.Lerp(1f, 0.28f, Completion);
        float width = Math.Max(1.2f, startWidth * MathHelper.Lerp(2.8f, 0.8f, Completion));

        // Calamity-style squished sprite particle: its size is local and independently capped by
        // DrawPixels. It never constructs a segment from two world-space endpoints.
        FranParticleAssets.DrawPixels(spriteBatch, "GlowSpark", Position,
            Color * (0.78f * fade), Rotation + MathHelper.PiOver2, new Vector2(width * 2.8f, length));
        FranParticleAssets.DrawPixels(spriteBatch, "GlowSpark", Position,
            Microsoft.Xna.Framework.Color.White * (0.92f * fade), Rotation + MathHelper.PiOver2,
            new Vector2(Math.Max(1f, width * 0.72f), length * 0.82f));
    }
}

// A material-looking sliver left inside the blade light. It is a bounded sprite particle rather
// than a stretched MagicPixel segment, so it cannot turn into an infinite rectangle.
internal sealed class FranBladeWakeSliverParticle : FranParticle
{
    private readonly float startLength;
    private readonly float startWidth;
    private readonly float spin;

    public override bool Additive => false;

    public FranBladeWakeSliverParticle(Vector2 position, Vector2 velocity, Vector2 direction, Color color,
        float length, float width, int lifetime, float spin)
    {
        Position = position;
        Velocity = velocity;
        Rotation = direction.SafeNormalize(Vector2.UnitX).ToRotation();
        Color = color;
        startLength = length;
        startWidth = width;
        Lifetime = lifetime;
        this.spin = spin;
    }

    protected override void Update()
    {
        Velocity *= 0.90f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float appear = Utils.GetLerpValue(0f, 0.16f, Completion, true);
        float fade = appear * MathF.Pow(1f - Completion, 1.6f);
        float length = startLength * MathHelper.Lerp(1f, 0.18f, Completion);
        float width = Math.Max(1f, startWidth * MathHelper.Lerp(1f, 0.35f, Completion));

        FranParticleAssets.DrawPixels(spriteBatch, "NoiseSharp", Position,
            Color * (0.72f * fade), Rotation, new Vector2(length, width * 3.4f));
        FranParticleAssets.DrawPixels(spriteBatch, "SlashNarrow", Position,
            Microsoft.Xna.Framework.Color.White * (0.78f * fade), Rotation,
            new Vector2(length * 0.84f, Math.Max(1f, width * 0.72f)));
    }
}

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
        FranParticleAssets.DrawPixels(spriteBatch, "HalfStar", Position,
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

        FranParticleAssets.Draw(spriteBatch, "BloomRingAngled", Position,
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

// A small crescent shed from the analytic blade surface. The curved smear is a local sprite with a
// hard pixel-size cap; it never constructs geometry from a chain of endpoint pairs.
internal sealed class FranBladeArcFragmentParticle : FranParticle
{
    private readonly float startRadius;
    private readonly float arcAngle;
    private readonly float startWidth;
    private readonly float spin;

    public FranBladeArcFragmentParticle(Vector2 position, Vector2 velocity, Vector2 direction, Color color,
        float radius, float arcAngle, float width, int lifetime, float spin)
    {
        Position = position;
        Velocity = velocity;
        Rotation = direction.SafeNormalize(Vector2.UnitX).ToRotation();
        Color = color;
        startRadius = MathHelper.Clamp(radius, 8f, 42f);
        this.arcAngle = MathHelper.Clamp(arcAngle, -1.4f, 1.4f);
        startWidth = MathHelper.Clamp(width, 0.8f, 5.5f);
        Lifetime = Math.Max(4, lifetime);
        this.spin = MathHelper.Clamp(spin, -0.16f, 0.16f);
    }

    protected override void Update()
    {
        Velocity *= 0.91f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.4f);
        float radius = startRadius * MathHelper.Lerp(0.72f, 1.18f, Completion);
        float contraction = MathHelper.Lerp(1f, 0.38f, Completion);
        float width = Math.Max(0.7f, startWidth * contraction);
        SpriteEffects flip = arcAngle < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;
        FranParticleAssets.DrawPixels(spriteBatch, "SlashSmear", Position,
            Color * (0.68f * fade), Rotation,
            new Vector2(radius * 2.15f, Math.Max(4f, width * 7.2f)), effects: flip);
        FranParticleAssets.DrawPixels(spriteBatch, "SlashNarrow", Position,
            Microsoft.Xna.Framework.Color.White * (0.82f * fade), Rotation,
            new Vector2(radius * 1.72f, Math.Max(1.2f, width * 1.05f)), effects: flip);
    }
}
