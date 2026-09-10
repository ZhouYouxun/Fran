using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

internal enum FranSwingParticleStyle : byte
{
    CritSpark,
    SparklePulse,
    DrainLine,
    DrainLineBloom
}

// 普通挥砍使用的小型节奏粒子。贴图比例、阻尼和生命周期保持统一，
// 每次挥砍只调整生成位置、运动方向与颜色。
internal sealed class FranSwingCadenceParticle : FranParticle
{
    private readonly FranSwingParticleStyle style;
    private readonly Color initialColor;
    private readonly float originalScale;
    private readonly float finalScale;
    private readonly float baseOpacity;
    private readonly int frame;
    private Vector2 squash;
    private float opacity = 1f;

    public override bool Additive => style != FranSwingParticleStyle.DrainLine;

    private FranSwingCadenceParticle(Vector2 position, Vector2 velocity, Color color,
        FranSwingParticleStyle style, float scale, Vector2 squash, int lifetime,
        float finalScale = 0f, float rotation = 0f, float baseOpacity = 1f)
    {
        Position = position;
        Velocity = velocity;
        Color = initialColor = color;
        this.style = style;
        Scale = originalScale = scale;
        this.squash = squash;
        Lifetime = Math.Max(4, lifetime);
        this.finalScale = finalScale;
        Rotation = rotation;
        this.baseOpacity = baseOpacity;
        frame = Main.rand.Next(4);
    }

    public static FranSwingCadenceParticle CritSpark(Vector2 position, Vector2 velocity,
        Color color, int lifetime = 15) =>
        new(position, velocity, color, FranSwingParticleStyle.CritSpark, 1f, Vector2.One, lifetime);

    public static FranSwingCadenceParticle SparklePulse(Vector2 position, Vector2 velocity,
        Color color, float rotation, float originalScale, float finalScale = 0.2f,
        int lifetime = 23) =>
        new(position, velocity, color, FranSwingParticleStyle.SparklePulse, originalScale,
            new Vector2(2f, 1f), lifetime, finalScale, rotation);

    public static FranSwingCadenceParticle DrainLine(Vector2 position, Vector2 velocity,
        Color color, float scale, int lifetime = 19) =>
        new(position, velocity, color, FranSwingParticleStyle.DrainLine, scale,
            new Vector2(0.5f, 1.6f), lifetime,
            rotation: velocity.ToRotation() + MathHelper.PiOver2);

    public static FranSwingCadenceParticle DrainLineBloom(Vector2 position, Vector2 velocity,
        Color color, float scale, int lifetime = 30) =>
        new(position, velocity, color, FranSwingParticleStyle.DrainLineBloom, scale,
            new Vector2(0.5f, 1.6f), lifetime,
            rotation: velocity.ToRotation() + MathHelper.PiOver2);

    protected override void Update()
    {
        switch (style)
        {
            case FranSwingParticleStyle.CritSpark:
                // CritSpark 生命周期短于标准淡出窗口，因此从生成时开始缓慢衰减，避免末帧突消。
                Position += Velocity;
                if (Time > Lifetime - 20)
                    opacity *= 0.9f;
                Velocity *= 0.85f;
                break;

            case FranSwingParticleStyle.SparklePulse:
                float pulseProgress = 1f - MathF.Pow(1f - Completion, 4f);
                Scale = MathHelper.Lerp(originalScale, finalScale, pulseProgress);
                opacity = MathF.Cos(Completion * MathHelper.PiOver2) * baseOpacity;
                Velocity *= 0.95f;
                Lighting.AddLight(Position, initialColor.ToVector3() * opacity);
                break;

            default:
                Scale *= 0.95f;
                opacity = 1f - MathF.Pow(Completion, 3f);
                Velocity *= 0.95f;
                Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
                break;
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        switch (style)
        {
            case FranSwingParticleStyle.CritSpark:
                Rectangle source = new(0, frame * 16, 14, 16);
                FranParticleAssets.DrawParticle(spriteBatch, nameof(FranSwingCadenceParticle), Position,
                    initialColor * opacity, Rotation, Vector2.One, source);
                break;

            case FranSwingParticleStyle.SparklePulse:
                // 脉冲只绘制一层 Sparkle，不叠加额外 Bloom 底盘。
                FranParticleAssets.Draw(spriteBatch, "FranSparkle", Position,
                    initialColor * (opacity * opacity), Rotation, squash * Scale);
                break;

            default:
                string texture = style == FranSwingParticleStyle.DrainLine
                    ? "FranDrainLine"
                    : "FranDrainLineBloom";
                FranParticleAssets.Draw(spriteBatch, texture, Position,
                    initialColor * opacity, Rotation, squash * Scale);
                FranParticleAssets.Draw(spriteBatch, texture, Position,
                    initialColor * opacity, Rotation, squash * Scale * new Vector2(0.45f, 1f));
                break;
        }
    }
}
