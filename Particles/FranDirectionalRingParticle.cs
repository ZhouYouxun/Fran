using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


internal sealed class FranDirectionalRingParticle : FranParticle
{
    private readonly Vector2 startScale;
    private readonly Vector2 endScale;
    private readonly float spin;

    public FranDirectionalRingParticle(Vector2 position, Vector2 velocity, Color color, Vector2 startScale,
        Vector2 endScale, float rotation, int lifetime, float spin = 0f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.startScale = startScale;
        this.endScale = endScale;
        Rotation = rotation;
        Lifetime = lifetime;
        this.spin = spin;
    }

    protected override void Update()
    {
        Velocity *= 0.92f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Sin(Completion * MathHelper.Pi);
        Vector2 size = Vector2.Lerp(startScale, endScale, 1f - MathF.Pow(1f - Completion, 3f));
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranDirectionalRingParticle), Position,
            Color * (0.70f * fade), Rotation, size);
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranDirectionalRingParticle), Position,
            Color.White * (0.38f * fade), Rotation,
            size * new Vector2(0.72f, 0.58f));
    }
}
