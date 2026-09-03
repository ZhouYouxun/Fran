using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FranMod.Particles;


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
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranBlackLightCoreParticle), Position,
            new Color(0, 0, 0) * fade, Rotation, Vector2.One * size);
        FranParticleAssets.DrawPixels(spriteBatch, "CircularSmearSmokey", Position,
            Color * (fade * 0.82f), -Rotation * 0.72f, Vector2.One * size * 0.76f);
    }
}
