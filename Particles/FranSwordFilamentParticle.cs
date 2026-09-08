using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

// 短促折线电丝：局部生成几何，只使用 Fran 的细线材质，不需要 Bolt/Bolt2 贴图。
internal sealed class FranSwordFilamentParticle : FranParticle
{
    private readonly Vector2[] points = new Vector2[7];
    private readonly float bend;

    public FranSwordFilamentParticle(Vector2 position, Vector2 velocity, Vector2 direction,
        Color color, float length, int lifetime, float bend = 0f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Lifetime = Math.Clamp(lifetime, 5, 14);
        this.bend = bend;
        Vector2 axis = direction.SafeNormalize(Vector2.UnitX);
        Vector2 side = axis.RotatedBy(MathHelper.PiOver2);
        length = MathHelper.Clamp(length, 14f, 58f);
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);
            points[i] = axis * ((t - 0.5f) * length) + side *
                (MathF.Sin(t * MathHelper.Pi) * Main.rand.NextFloat(-7f, 7f));
        }
    }

    protected override void Update()
    {
        Velocity = Velocity.RotatedBy(bend) * 0.88f;
        Rotation += bend;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.6f);
        float flicker = Time % 3 == 0 ? 0.66f : 1f;
        for (int i = 1; i < points.Length; i++)
        {
            Vector2 start = Position + points[i - 1].RotatedBy(Rotation);
            Vector2 end = Position + points[i].RotatedBy(Rotation);
            FranParticleAssets.DrawLine(spriteBatch, "ThinEndedLine", start, end,
                Color * (fade * flicker * 0.48f), 3.2f);
            FranParticleAssets.DrawLine(spriteBatch, "ThinEndedLine", start, end,
                Microsoft.Xna.Framework.Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, 0.72f) *
                (fade * flicker * 0.92f), 1.1f);
        }
    }
}
