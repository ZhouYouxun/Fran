using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


internal sealed class FranToxicMoteParticle : FranParticle
{
    private readonly float wobbleSeed;
    public override bool Additive => false;

    public FranToxicMoteParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        Lifetime = lifetime;
        wobbleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
    }

    protected override void Update()
    {
        Velocity.X += MathF.Sin(Time * 0.19f + wobbleSeed) * 0.045f;
        Velocity.Y -= 0.018f;
        Velocity *= 0.985f;
        Rotation += 0.025f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Sin(Completion * MathHelper.Pi);
        float pulse = 0.82f + MathF.Sin(Time * 0.3f + wobbleSeed) * 0.14f;
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranToxicMoteParticle), Position,
            Color * (0.78f * fade), Rotation, Vector2.One * Scale * pulse);
        FranParticleAssets.Draw(spriteBatch, "BloomCircle", Position, new Color(160, 255, 75) * (0.22f * fade), 0f, Vector2.One * Scale * 0.32f);
    }
}
