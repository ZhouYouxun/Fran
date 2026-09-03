using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


internal sealed class FranHeartParticle : FranParticle
{
    private readonly float angularVelocity;
    private readonly float lift;
    private readonly bool large;

    public FranHeartParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime,
        float angularVelocity = 0f, bool large = false)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        Lifetime = lifetime;
        this.angularVelocity = angularVelocity;
        this.large = large;
        lift = Main.rand.NextFloat(0.015f, 0.045f);
    }

    protected override void Update()
    {
        Velocity = Velocity.RotatedBy(angularVelocity);
        Velocity.Y -= lift;
        Velocity *= 0.975f;
        Rotation = MathF.Sin(Time * 0.13f) * 0.12f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Sin(Completion * MathHelper.Pi);
        float pulse = 0.88f + MathF.Sin(Time * 0.38f) * 0.12f;
        string texture = large ? "BigHeart" : nameof(FranHeartParticle);
        FranParticleAssets.Draw(spriteBatch, "BloomCircle", Position, Color * (0.28f * fade), 0f, Vector2.One * Scale * 0.44f);
        FranParticleAssets.Draw(spriteBatch, texture, Position, Color * (0.78f * fade), Rotation, Vector2.One * Scale * pulse);
        FranParticleAssets.Draw(spriteBatch, texture, Position, Color.White * (0.34f * fade), Rotation, Vector2.One * Scale * pulse * 0.58f);
    }
}
