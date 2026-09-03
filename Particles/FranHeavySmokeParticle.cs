using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


internal sealed class FranHeavySmokeParticle : FranParticle
{
    private readonly float opacity;
    private readonly float spin;
    private readonly int variant;
    private readonly bool glowing;
    public override bool Additive => glowing;

    public FranHeavySmokeParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime,
        float opacity, bool glowing = false, FranParticleLayer layer = FranParticleLayer.BehindProjectiles)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        Lifetime = lifetime;
        this.opacity = opacity;
        this.glowing = glowing;
        Layer = layer;
        variant = Main.rand.Next(7);
        spin = Main.rand.NextFloat(-0.035f, 0.035f);
        Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
    }

    protected override void Update()
    {
        Velocity *= 0.87f;
        Rotation += spin;
        Scale *= Time < Lifetime * 0.2f ? 1.018f : 0.982f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D texture = FranParticleAssets.GetParticle(nameof(FranHeavySmokeParticle));
        int frameCount = Math.Max(1, texture.Height / 80);
        int animation = Math.Min(frameCount - 1, (int)(Completion * frameCount));
        Rectangle frame = new(80 * variant, 80 * animation, 80, 80);
        float fade = Utils.GetLerpValue(1f, 0.74f, Completion, true);
        Color usedColor = Color * (opacity * fade);
        if (!glowing)
            usedColor = usedColor.MultiplyRGBA(Lighting.GetColor((Position / 16f).ToPoint()));
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranHeavySmokeParticle), Position,
            usedColor, Rotation, Vector2.One * Scale, frame);
    }
}
