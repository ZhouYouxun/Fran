using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


internal sealed class FranSparkParticle : FranParticle
{
    private readonly Vector2 stretch;
    private readonly float drag;
    private readonly float gravity;
    private readonly bool critical;

    public FranSparkParticle(Vector2 position, Vector2 velocity, Color color, float scale, Vector2 stretch,
        int lifetime, float drag = 0.94f, float gravity = 0f, bool critical = false)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        this.stretch = stretch;
        Lifetime = lifetime;
        this.drag = drag;
        this.gravity = gravity;
        this.critical = critical;
    }

    protected override void Update()
    {
        Velocity *= drag;
        Velocity.Y += gravity;
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.7f);
        float shrink = MathHelper.Lerp(1f, 0.12f, Completion);
        string texture = critical ? "CritSpark" : nameof(FranSparkParticle);
        // 将公开缩放值换算为像素尺寸，避免大尺寸源纹理导致粒子比例失控。
        Vector2 usedSize = new Vector2(
            MathHelper.Clamp(54f * MathF.Abs(stretch.X) * Scale, 4f, 118f),
            MathHelper.Clamp(54f * MathF.Abs(stretch.Y) * Scale, 4f, 118f)) * shrink;
        FranParticleAssets.DrawPixels(spriteBatch, texture, Position, Color * fade, Rotation, usedSize);
        FranParticleAssets.DrawPixels(spriteBatch, texture, Position, Color.White * (0.72f * fade), Rotation,
            usedSize * new Vector2(0.38f, 0.82f));
    }
}
