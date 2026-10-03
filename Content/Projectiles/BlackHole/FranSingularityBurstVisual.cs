using System;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles.BlackHole;

// Both debris families are local presentation only. They never damage, pull, or collide.
public abstract class FranSingularityDebris : ModProjectile
{
    internal abstract bool IsSmoke { get; }
    internal int Lifetime => IsSmoke ? 54 : 34;
    internal float Progress => 1f - Projectile.timeLeft / (float)Lifetime;
    public override string Texture => "Fran/Assets/Textures/BlackHole/TransparentPixel";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 2;
        Projectile.friendly = Projectile.hostile = false;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.damage = 0;
    }
    public override bool? CanDamage() => false;
    public override bool? CanCutTiles() => false;
    public override bool PreDraw(ref Color lightColor) => false;
    public override void AI()
    {
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.velocity = Projectile.velocity.RotatedBy(IsSmoke ? Projectile.ai[1] * 0.024f : 0f) *
            (IsSmoke ? 0.954f : 0.97f);
    }
}

public sealed class FranSingularityShard : FranSingularityDebris { internal override bool IsSmoke => false; }
public sealed class FranSingularitySmoke : FranSingularityDebris { internal override bool IsSmoke => true; }

internal static class FranSingularityBurstVisual
{
    internal static void Spawn(Projectile hole)
    {
        if (Main.dedServ) return;
        // Fixed sector coverage keeps the blast dense in every direction. A seed derived from
        // network identity gives spectators the same burst without sending 84 cosmetic packets.
        Random random = new(unchecked(hole.identity * 397 ^ hole.owner * 7919));
        IEntitySource source = hole.GetSource_FromThis();
        for (int i = 0; i < 84; i++)
        {
            bool smoke = i >= 56;
            int count = smoke ? 28 : 56;
            int sector = smoke ? i - 56 : i;
            float angle = MathHelper.TwoPi * (sector + (float)random.NextDouble() * 0.7f) / count;
            float speed = smoke ? 8f + (float)random.NextDouble() * 13f : 20f + (float)random.NextDouble() * 28f;
            int type = smoke ? ModContent.ProjectileType<FranSingularitySmoke>() : ModContent.ProjectileType<FranSingularityShard>();
            Projectile.NewProjectile(source, hole.Center + angle.ToRotationVector2() * (smoke ? 32f : 12f),
                angle.ToRotationVector2() * speed, type, 0, 0f, 255,
                (float)random.NextDouble() * 100f, sector % 2 == 0 ? 1f : -1f);
        }
    }

    internal static void DrawAll()
    {
        bool exists = false;
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is FranSingularityDebris) { exists = true; break; }
        if (!exists || !ShaderManager.HasFinishedLoading) return;
        if (!ShaderManager.TryGetShader("Fran.FranSingularityShards", out ManagedShader shards) ||
            !ShaderManager.TryGetShader("Fran.FranSingularitySmoke", out ManagedShader smoke)) return;
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        try
        {
            // Draw smoke first, then sharper black wedges above it.
            for (int pass = 0; pass < 2; pass++)
                foreach (Projectile p in Main.ActiveProjectiles)
                {
                    if (p.ModProjectile is not FranSingularityDebris debris || debris.IsSmoke != (pass == 0)) continue;
                    float t = debris.Progress;
                    ManagedShader shader = debris.IsSmoke ? smoke : shards;
                    shader.TrySetParameter("progress", t);
                    shader.TrySetParameter("seed", p.ai[0]);
                    shader.Apply();
                    float size = 0.85f + p.ai[0] % 1f * 0.5f;
                    Vector2 scale = debris.IsSmoke
                        ? new Vector2(190f, 118f) * size * (0.7f + t * 1.9f)
                        : new Vector2(260f * (1f - t * 0.42f), 30f * (1f - t * 0.65f)) * size;
                    Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, p.Center - Main.screenPosition, null,
                        Color.White, p.rotation, new Vector2(0.5f), scale, SpriteEffects.None, 0f);
                }
        }
        finally { Main.spriteBatch.End(); }
    }
}
