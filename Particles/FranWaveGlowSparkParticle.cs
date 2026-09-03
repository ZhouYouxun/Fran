using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

// _Wave 命中时 GlowSparkParticle 的本地视觉对应物。
internal sealed class FranWaveGlowSparkParticle : FranParticle
{
    private readonly Color initialColor;
    private Vector2 squash;

    public FranWaveGlowSparkParticle(Vector2 position, Vector2 velocity, Color color,
        float scale, Vector2 squash, int lifetime)
    {
        Position = position;
        Velocity = velocity;
        Color = initialColor = color;
        Scale = scale;
        this.squash = squash;
        Lifetime = lifetime;
        Rotation = velocity.ToRotation() + MathHelper.PiOver2;
    }

    protected override void Update()
    {
        Scale *= 0.95f;
        Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(Completion, 3f));
        Velocity *= 0.95f;
        squash.X *= 0.8f;
        squash.Y *= 1.2f;
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        // 固定初始缩放与逐帧收缩共同限制大范围细长火花的尺寸，
        // 因此不使用通用的 256 像素粒子裁剪。
        Texture2D texture = FranParticleAssets.GetParticle(nameof(FranWaveGlowSparkParticle));
        spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation,
            texture.Size() * 0.5f, squash * Scale, SpriteEffects.None, 0f);
    }
}
