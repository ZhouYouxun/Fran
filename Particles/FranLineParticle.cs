using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


internal sealed class FranLineParticle : FranParticle
{
    private readonly float length;
    private readonly float width;
    private readonly float drag;

    public FranLineParticle(Vector2 position, Vector2 velocity, Color color, float length, float width,
        int lifetime, float drag = 0.92f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.length = length;
        this.width = width;
        Lifetime = lifetime;
        this.drag = drag;
    }

    protected override void Update() => Velocity *= drag;

    public override void Draw(SpriteBatch spriteBatch)
    {
        Vector2 direction = Velocity.SafeNormalize(Rotation.ToRotationVector2());
        float fade = MathF.Pow(1f - Completion, 2f);
        float usedLength = length * MathHelper.Lerp(1f, 0.25f, Completion);
        Vector2 start = Position - direction * usedLength * 0.55f;
        Vector2 end = Position + direction * usedLength * 0.45f;
        FranParticleAssets.DrawLine(spriteBatch, nameof(FranLineParticle), start, end, Color * (0.55f * fade), width * 2.8f);
        FranParticleAssets.DrawLine(spriteBatch, nameof(FranLineParticle), start, end, Color.White * (0.84f * fade), width * 0.62f);
    }
}
