using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace FranMod.Particles;


// 蓝辉共鸣星光依次经历点燃、漂移、回拉锁定、穿刺与熄灭，全部使用 Fran 本地材质绘制。
internal sealed class FranConstellationMoteParticle : FranParticle
{
    private const int KindleEnd = 5;
    private const int DriftEnd = 13;
    private const int LockEnd = 19;
    private const int LanceEnd = 26;

    private readonly FranConstellationGroup group;
    private readonly int groupIndex;
    private readonly Vector2 anchor;
    private readonly Vector2[] trail = new Vector2[6];
    private readonly float phase;
    private readonly float baseSize;
    private float lockProgress;

    public override bool Important => true;

    public FranConstellationMoteParticle(FranConstellationGroup group, int groupIndex,
        Vector2 position, Vector2 velocity, Vector2 anchor, Color color, float phase, float baseSize)
    {
        this.group = group;
        this.groupIndex = groupIndex;
        this.anchor = anchor;
        this.phase = phase;
        this.baseSize = MathHelper.Clamp(baseSize, 7f, 18f);
        Position = position;
        Velocity = velocity;
        Color = color;
        Lifetime = 32;
        Rotation = phase;
        for (int i = 0; i < trail.Length; i++)
            trail[i] = position;
        group.Positions[groupIndex] = position;
    }

    protected override void Update()
    {
        for (int i = trail.Length - 1; i > 0; i--)
            trail[i] = trail[i - 1];
        trail[0] = Position;

        Vector2 radial = (Position - anchor).SafeNormalize(phase.ToRotationVector2());
        Vector2 tangent = radial.RotatedBy(MathHelper.PiOver2);
        if (Time <= KindleEnd)
        {
            Velocity *= 0.78f;
            Velocity += tangent * MathF.Sin(Time * 0.75f + phase) * 0.11f;
        }
        else if (Time <= DriftEnd)
        {
            Velocity *= 0.88f;
            Velocity += tangent * (0.17f + MathF.Sin(Time * 0.42f + phase) * 0.07f);
            Velocity -= radial * 0.025f;
        }
        else if (Time <= LockEnd)
        {
            lockProgress = MathHelper.Clamp((Time - DriftEnd) / (float)(LockEnd - DriftEnd), 0f, 1f);
            Vector2 drawBack = radial * MathHelper.Lerp(0.35f, 1.75f, lockProgress);
            Velocity = Vector2.Lerp(Velocity, drawBack, 0.42f);
            Rotation -= 0.09f;
        }
        else if (Time <= LanceEnd)
        {
            float lance = MathHelper.Clamp((Time - LockEnd) / (float)(LanceEnd - LockEnd), 0f, 1f);
            float speed = MathHelper.Lerp(4.5f, 15.5f, lance * lance);
            Velocity = Vector2.Lerp(Velocity,
                (anchor - Position).SafeNormalize(-radial) * speed, 0.56f);
            Rotation += 0.18f;
        }
        else
        {
            Velocity *= 0.18f;
            lockProgress = 0f;
        }

        group.Positions[groupIndex] = Position;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float appear = MathHelper.Clamp(Time / (float)KindleEnd, 0f, 1f);
        float quench = Time <= LanceEnd
            ? 1f
            : MathHelper.Clamp((Lifetime - Time) / (float)(Lifetime - LanceEnd), 0f, 1f);
        float opacity = appear * quench;
        if (opacity <= 0.01f)
            return;

        if (Time <= LanceEnd)
        {
            int nextIndex = (groupIndex + 1) % group.Positions.Length;
            Vector2 next = group.Positions[nextIndex];
            if (Vector2.DistanceSquared(Position, next) is > 9f and < 180f * 180f)
                FranParticleAssets.DrawLine(spriteBatch, "ThinEndedLine", Position, next,
                    Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, 0.38f) * (0.16f * opacity), 1.25f);
        }

        for (int i = trail.Length - 1; i >= 1; i--)
        {
            float trailFade = 1f - i / (float)trail.Length;
            FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranConstellationMoteParticle), trail[i],
                Color * (opacity * trailFade * trailFade * 0.24f), Rotation - i * 0.12f,
                new Vector2(baseSize * trailFade, baseSize * 0.56f * trailFade));
        }

        if (lockProgress > 0.01f)
        {
            float ringSize = MathHelper.Lerp(baseSize * 4.8f, baseSize * 1.75f, lockProgress);
            FranParticleAssets.DrawPixels(spriteBatch, "BloomRing", Position,
                Color * (opacity * lockProgress * 0.42f), -Rotation, Vector2.One * ringSize);
        }

        float kindleFlash = Time <= KindleEnd ? 1f - Time / (float)KindleEnd : 0f;
        float headSize = baseSize * (0.74f + kindleFlash * 0.9f);
        FranParticleAssets.DrawPixels(spriteBatch, "FranResonanceOrb", Position,
            Color * (opacity * 0.34f), -Rotation * 0.7f, Vector2.One * headSize * 2.5f);
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranConstellationMoteParticle), Position,
            Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, 0.68f) * (opacity * 0.86f), Rotation,
            new Vector2(headSize * 1.35f, headSize * 0.72f));
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranConstellationMoteParticle), Position,
            Microsoft.Xna.Framework.Color.White * (opacity * 0.66f), Rotation + MathHelper.PiOver2,
            new Vector2(headSize * 0.74f, headSize * 0.38f));
    }
}
