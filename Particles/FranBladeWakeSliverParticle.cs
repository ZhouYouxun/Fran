using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


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
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranBladeWakeSliverParticle), Position,
            Microsoft.Xna.Framework.Color.White * (0.78f * fade), Rotation,
            new Vector2(length * 0.84f, Math.Max(1f, width * 0.72f)));
    }
}
