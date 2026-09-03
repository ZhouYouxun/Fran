using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Particles;


/// <summary>
/// 战术液滴消散时飞出的非发光实体碎屑。它与弹幕的 BOLT 本体是两层不同的视觉：
/// BOLT 负责“看见一滴东西在飞”，本粒子负责“它撞碎之后还留下有重量的液体残片”。
/// </summary>
internal sealed class FranTacticalDropletParticle : FranParticle
{
    private readonly float gravity;
    private readonly float startScale;
    private readonly float spin;

    public override bool Additive => false;

    public FranTacticalDropletParticle(Vector2 position, Vector2 velocity, Color color,
        float scale, int lifetime, float gravity)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        startScale = MathHelper.Clamp(scale, 0.18f, 1.5f);
        Lifetime = Math.Max(5, lifetime);
        this.gravity = gravity;
        spin = Main.rand.NextFloat(-0.12f, 0.12f);
        Rotation = velocity.ToRotation();
    }

    protected override void Update()
    {
        Velocity *= 0.965f;
        Velocity.Y += gravity;
        Rotation = Velocity.LengthSquared() > 0.01f ? Velocity.ToRotation() : Rotation + spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.35f);
        float shrink = MathHelper.Lerp(1f, 0.18f, Completion);
        float speedStretch = 1f + MathHelper.Clamp(Velocity.Length() * 0.08f, 0f, 1.1f);
        Vector2 bodySize = new Vector2(13f * startScale * speedStretch, 8f * startScale) * shrink;
        Color shadow = Color.Lerp(Color, new Color(10, 11, 18), 0.52f);
        FranParticleAssets.DrawParticlePixels(spriteBatch, nameof(FranTacticalDropletParticle), Position,
            shadow * (0.82f * fade), Rotation, bodySize);
        FranParticleAssets.DrawPixels(spriteBatch, "NoiseSharp", Position,
            Microsoft.Xna.Framework.Color.Lerp(Color, Microsoft.Xna.Framework.Color.White, 0.64f) *
            (0.54f * fade), Rotation, bodySize * new Vector2(0.66f, 0.30f));
    }
}
