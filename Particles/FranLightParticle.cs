using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


/// <summary>
/// Fran“光粒子”：一颗沿速度方向被压扁/拉长的三层发光粒子。
/// 用于挥剑释放与爆炸散射，与Dust、辉光球和神秘文字组成不同尺度的视觉层。
/// </summary>
internal sealed class FranLightParticle : FranParticle
{
    private readonly float opacity;
    private readonly float squishStrength;
    private readonly float maxSquish;
    private readonly float hueShift;
    private readonly float timeScale;

    public FranLightParticle(Vector2 position, Vector2 velocity, float scale, Color color, int lifetime,
        float opacity = 1f, float squishStrength = 1f, float maxSquish = 3f, float hueShift = 0f,
        FranParticleLayer layer = FranParticleLayer.AfterProjectiles, float timeScale = 1f)
    {
        Position = position;
        Velocity = velocity;
        Scale = scale;
        Color = color;
        Lifetime = lifetime;
        this.opacity = opacity;
        this.squishStrength = squishStrength;
        this.maxSquish = Math.Max(1f, maxSquish);
        this.hueShift = hueShift;
        this.timeScale = timeScale;
        Layer = layer;
    }

    protected override void Update()
    {
        Velocity *= MathF.Pow(Completion >= 0.34f ? 0.93f : 1.02f, timeScale);
        Scale *= MathF.Pow(0.95f, timeScale);
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        if (hueShift != 0f)
        {
            Vector3 hsl = Main.rgbToHsl(Color);
            hsl.X = (hsl.X + hueShift) % 1f;
            if (hsl.X < 0f)
                hsl.X += 1f;
            Color = Main.hslToRgb(hsl);
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = Completion > 0.5f
            ? MathF.Sin(Completion * MathHelper.Pi) * 0.2f + 0.8f
            : MathF.Sin(Completion * MathHelper.Pi);
        fade *= opacity;
        float squish = MathHelper.Clamp(Velocity.Length() / 10f * squishStrength, 1f, maxSquish);
        float widthFactor = Math.Max(0.12f, 1f - squish * 0.30f);
        Vector2 bodySize = new(34f * Scale * widthFactor, 34f * Scale * squish);

        FranParticleAssets.DrawPixels(spriteBatch, "BloomCircle", Position, Color * (fade * 0.52f), Rotation,
            bodySize * 1.8f);
        FranParticleAssets.DrawPixels(spriteBatch, nameof(FranLightParticle), Position, Color * (fade * 0.82f), Rotation,
            bodySize * new Vector2(0.72f, 1.16f));
        FranParticleAssets.DrawPixels(spriteBatch, nameof(FranLightParticle), Position, Color.White * (fade * 0.92f), Rotation,
            bodySize * new Vector2(0.28f, 0.94f));
    }
}
