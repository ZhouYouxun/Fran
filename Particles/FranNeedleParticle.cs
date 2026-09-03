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
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranNeedleParticle), Position,
            Color * fade, Rotation, size);
    }
}
