using System;
using FranMod.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

// _Wave 使用的 SquishyLightParticle 的本地视觉对应物；颜色由调用方决定。
// 主贴图与本类同名，Bloom 辅层使用粒子库中的 CL_BloomCircle。
internal sealed class FranWaveSquishyLightParticle : FranParticle
{
    private float opacity;
    private readonly float squishStrength;
    private readonly float maximumSquish;

    public FranWaveSquishyLightParticle(Vector2 position, Vector2 velocity, float scale,
        Color color, int lifetime, float opacity, float squishStrength, float maximumSquish = 3f)
    {
        Position = position;
        Velocity = velocity;
        Scale = scale;
        Color = color;
        Lifetime = lifetime;
        this.opacity = opacity;
        this.squishStrength = squishStrength;
        this.maximumSquish = maximumSquish;
    }

    /// <summary>
    /// 蓝辉共鸣命中时的波体光粒子爆发。
    /// 由一圈较整齐的径向波体与少量向上的散射波体组成，粒子本身不造成伤害。
    /// </summary>
    public static void SpawnBlueResonanceBurst(Vector2 center)
    {
        if (Main.dedServ)
            return;

        const int radialCount = 12;
        float rotationOffset = Main.rand.NextFloat(MathHelper.TwoPi);
        for (int i = 0; i < radialCount; i++)
        {
            float angle = rotationOffset + MathHelper.TwoPi * i / radialCount +
                Main.rand.NextFloat(-0.10f, 0.10f);
            Vector2 direction = angle.ToRotationVector2();
            Vector2 velocity = direction * Main.rand.NextFloat(3.4f, 7.8f);
            Color color = i % 4 == 0
                ? FranResonanceVisuals.Pale
                : Color.Lerp(FranResonanceVisuals.Blue, FranResonanceVisuals.Cyan, Main.rand.NextFloat());
            FranParticleSystem.Spawn(new FranWaveSquishyLightParticle(
                center + direction * Main.rand.NextFloat(3f, 13f), velocity,
                Main.rand.NextFloat(0.34f, 0.58f), color, Main.rand.Next(16, 24),
                1f, Main.rand.NextFloat(1.25f, 1.85f), 2.6f));
        }

        // 蓝辉共鸣的通用弹幕向上绽开，这四枚波体负责把爆发方向接到弹幕上。
        for (int i = 0; i < 4; i++)
        {
            Vector2 velocity = (-Vector2.UnitY).RotatedByRandom(0.52f) * Main.rand.NextFloat(5.2f, 9.2f);
            Color color = Main.rand.NextBool(3) ? FranResonanceVisuals.Pale : FranResonanceVisuals.Cyan;
            FranParticleSystem.Spawn(new FranWaveSquishyLightParticle(
                center + Main.rand.NextVector2Circular(7f, 7f), velocity,
                Main.rand.NextFloat(0.38f, 0.64f), color, Main.rand.Next(17, 25),
                1f, Main.rand.NextFloat(1.45f, 2.05f), 2.8f));
        }
    }

    protected override void Update()
    {
        Velocity *= Completion >= 0.34f ? 0.93f : 1.02f;
        opacity = Completion > 0.5f
            ? MathF.Sin(Completion * MathHelper.Pi) * 0.2f + 0.8f
            : MathF.Sin(Completion * MathHelper.Pi);
        Scale *= 0.95f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D light = FranParticleAssets.GetParticle(nameof(FranWaveSquishyLightParticle));
        Texture2D bloom = FranParticleAssets.Get("CL_BloomCircle");
        float squish = MathHelper.Clamp(Velocity.Length() / 10f * squishStrength,
            1f, maximumSquish);
        float rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        Vector2 scale = new(Scale - Scale * squish * 0.3f, Scale * squish);
        float bloomSizeCorrection = light.Height / (float)Math.Max(1, bloom.Height);

        FranParticleAssets.Draw(spriteBatch, "CL_BloomCircle", Position,
            Color * (opacity * 0.8f), rotation, scale * (2f * bloomSizeCorrection));
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranWaveSquishyLightParticle),
            Position, Color * (opacity * 0.8f), rotation, scale * 1.1f);
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranWaveSquishyLightParticle),
            Position, Color.Lerp(Color, FranResonanceVisuals.Pale, 0.58f) * (opacity * 0.9f), rotation, scale);
    }
}
