using System;
using FranMod.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

// Cosmetic particles only: no projectile slots, targeting or healing side effects.
internal sealed class FranResonanceBloomParticle : FranParticle
{
    public override bool Additive => false;

    internal static void SpawnBurst(Vector2 center, float direction, float size)
    {
        if (!FranResonanceVisuals.Visible(center))
            return;
        FranParticleSystem.Spawn(new FranResonanceBloomParticle
        {
            Position = center, Scale = size, Rotation = direction,
            Color = FranResonanceVisuals.Mint, Lifetime = 32
        }, spawnSupportingDust: false);
        for (int i = 0; i < 3; i++)
        {
            float angle = direction + i * MathHelper.TwoPi / 3f;
            FranParticleSystem.Spawn(new FranResonanceButterflyParticle
            {
                Position = center, Velocity = angle.ToRotationVector2() * (2.2f + i * .3f),
                Rotation = angle + MathHelper.PiOver2, Scale = .42f * size,
                Color = FranResonanceVisuals.Mint, Lifetime = 24
            }, spawnSupportingDust: false);
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!FranResonanceVisuals.Visible(Position))
            return;
        FranResonanceVisuals.DrawHealSeal(Position, Completion * 40f, Scale);
        float fade = MathF.Pow(1f - Completion, 1.4f);
        float radius = (12f + 42f * (1f - MathF.Pow(1f - Completion, 3f))) * Scale;
        FranResonanceVisuals.Ring(Position, new Vector2(radius), Rotation,
            Completion * 3f, FranResonanceVisuals.Mint, fade * .8f, 1.1f, 7f);
        FranResonanceVisuals.Star(Position, 18f * Scale * (1f - Completion),
            Rotation, fade, FranResonanceVisuals.Heart);
    }
}

internal sealed class FranResonanceButterflyParticle : FranParticle
{
    public override bool Additive => false;

    protected override void Update()
    {
        Velocity *= .94f;
        Velocity.Y -= .015f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (FranResonanceVisuals.Visible(Position))
            FranResonanceVisuals.DrawButterfly(Position, Rotation, Time + 8f,
                Scale * (1f - Completion * .3f), 1f - Completion * Completion);
    }
}
