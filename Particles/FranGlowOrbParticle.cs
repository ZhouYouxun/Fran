using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

// 主贴图与类同名；资源授权记录统一保存在 Particles/README.md。
internal sealed class FranGlowOrbParticle : FranParticle
{
    private readonly float startScale;
    private readonly float endScale;
    private readonly bool whiteCore;
    private readonly bool affectedByGravity;

    public FranGlowOrbParticle(Vector2 position, Vector2 velocity, Color color, float startScale, float endScale,
        int lifetime, bool whiteCore = true, FranParticleLayer layer = FranParticleLayer.AfterProjectiles,
        bool affectedByGravity = false)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.startScale = startScale;
        this.endScale = endScale;
        Lifetime = lifetime;
        this.whiteCore = whiteCore;
        this.affectedByGravity = affectedByGravity;
        Layer = layer;
    }

    protected override void Update()
    {
        Velocity *= 0.95f;
        if (affectedByGravity && Velocity.LengthSquared() < 144f)
        {
            Velocity.X *= 0.94f;
            Velocity.Y += 0.25f;
        }
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 0.72f) * MathHelper.Clamp(Completion * 8f, 0f, 1f);
        float size = MathHelper.Lerp(startScale, endScale, 1f - MathF.Pow(1f - Completion, 3f));
        Vector2 stretch = new(0.88f, 1f + MathHelper.Clamp(Velocity.Length() * 0.025f, 0f, 0.55f));
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranGlowOrbParticle), Position,
            Color * (0.86f * fade), Rotation, stretch * size);
        if (whiteCore)
            FranParticleAssets.DrawParticle(spriteBatch, nameof(FranGlowOrbParticle), Position,
                Color.White * (0.58f * fade), Rotation, stretch * size * 0.42f);
    }
}
