using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


// Barrier fragments retain the shield's angled geometry as they fly away after a block or break.
internal sealed class FranBarrierShardParticle : FranParticle
{
    private readonly float spin;
    private readonly Vector2 startSize;

    public FranBarrierShardParticle(Vector2 position, Vector2 velocity, Color color, Vector2 size, int lifetime)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        startSize = size;
        Lifetime = lifetime;
        spin = Main.rand.NextFloat(-0.16f, 0.16f);
        Rotation = velocity.ToRotation();
    }

    protected override void Update()
    {
        Velocity *= 0.955f;
        Velocity.Y += 0.035f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.35f);
        Vector2 size = startSize * MathHelper.Lerp(1f, 0.18f, Completion);
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranBarrierShardParticle), Position,
            Color * (0.70f * fade), Rotation, size);
        FranParticleAssets.DrawPixels(spriteBatch, "NoiseSoft", Position,
            Color.White * (0.34f * fade), Rotation, size * new Vector2(0.35f, 0.72f));
    }
}
