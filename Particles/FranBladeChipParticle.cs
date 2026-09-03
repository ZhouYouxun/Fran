using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


internal sealed class FranBladeChipParticle : FranParticle
{
    private readonly float gravity;
    private readonly bool electric;

    public FranBladeChipParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime,
        float gravity = 0.08f, bool electric = false)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        Lifetime = lifetime;
        this.gravity = gravity;
        this.electric = electric;
        Rotation = velocity.ToRotation();
    }

    protected override void Update()
    {
        Velocity *= 0.965f;
        Velocity.Y += gravity;
        Rotation += Math.Sign(Velocity.X) * 0.13f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.4f);
        string texture = electric ? "ElectricSpark" : nameof(FranBladeChipParticle);
        FranParticleAssets.Draw(spriteBatch, texture, Position, Color * fade, Rotation,
            new Vector2(0.55f, 1.4f) * Scale * MathHelper.Lerp(1f, 0.2f, Completion));
    }
}
