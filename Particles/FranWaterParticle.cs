using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


internal sealed class FranWaterParticle : FranParticle
{
    private readonly float gravity;
    public override bool Additive => false;

    public FranWaterParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime, float gravity = 0.14f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        Lifetime = lifetime;
        this.gravity = gravity;
    }

    protected override void Update()
    {
        Velocity *= 0.96f;
        Velocity.Y += gravity;
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        Scale *= 0.965f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.5f);
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranWaterParticle), Position,
            Color * fade, Rotation, new Vector2(0.68f, 1.2f) * Scale);
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranWaterParticle), Position,
            Color.White * (0.34f * fade), Rotation, new Vector2(0.28f, 0.8f) * Scale);
    }
}
