using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


internal sealed class FranStarMoteParticle : FranParticle
{
    private readonly Color bloomColor;
    private readonly float spin;
    private readonly int targetIndex;
    private readonly float homingStrength;

    public FranStarMoteParticle(Vector2 position, Vector2 velocity, Color color, Color bloomColor, float scale,
        int lifetime, int targetIndex = -1, float homingStrength = 0f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.bloomColor = bloomColor;
        Scale = scale;
        Lifetime = lifetime;
        this.targetIndex = targetIndex;
        this.homingStrength = homingStrength;
        spin = Main.rand.NextFloat(-0.18f, 0.18f);
        Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
    }

    protected override void Update()
    {
        if (targetIndex >= 0 && Main.npc.IndexInRange(targetIndex) && Main.npc[targetIndex].active)
        {
            Vector2 desired = (Main.npc[targetIndex].Center - Position).SafeNormalize(Vector2.Zero) * Math.Max(2f, Velocity.Length());
            Velocity = Vector2.Lerp(Velocity, desired, homingStrength);
        }
        Velocity *= 0.985f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float opacity = MathF.Sin(Completion * MathHelper.Pi);
        FranParticleAssets.Draw(spriteBatch, "BloomCircle", Position, bloomColor * (0.46f * opacity), 0f, Vector2.One * Scale * 0.52f);
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranStarMoteParticle), Position,
            Color * (0.55f * opacity), Rotation + MathHelper.PiOver4, Vector2.One * Scale * 0.74f);
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranStarMoteParticle), Position,
            Color.White * (0.92f * opacity), Rotation, Vector2.One * Scale * 0.43f);
    }
}
