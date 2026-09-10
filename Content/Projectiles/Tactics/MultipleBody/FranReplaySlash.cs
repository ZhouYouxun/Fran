using System;
using System.Collections.Generic;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


public sealed class FranReplaySlash : ModProjectile
{
    public override string Texture => FranShiFuVisuals.TexturePath;
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 10;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 48; Projectile.height = 48; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = 2; Projectile.timeLeft = 42;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 14;
    }
    private float Age => 42f - Projectile.timeLeft;
    public override bool? CanDamage() => Age >= Projectile.ai[0] ? null : false;
    public override void AI()
    {
        if (Age < Projectile.ai[0])
        {
            Projectile.velocity *= 0.72f;
            Projectile.Opacity = MathHelper.Clamp(Age / Math.Max(1f, Projectile.ai[0]), 0.18f, 0.72f);
        }
        else
        {
            if (Age == Projectile.ai[0])
            {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * (17f + Projectile.ai[1]);
                FranParticleEffects.LineRelease(Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 18f,
                    Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 34f,
                    new Color(225, 245, 255), 7, 0.54f);
            }
            Projectile.velocity *= 1.008f;
            Projectile.Opacity = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 0.72f);
        }
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        if (Age == Projectile.ai[0])
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.34f, Pitch = 0.18f + Projectile.ai[1] * 0.08f }, Projectile.Center);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        FranVfx.BeginAdditive();
        for (int i = 1; i < Projectile.oldPos.Length; i++)
        {
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            Vector2 previous = Projectile.oldPos[i - 1] + Projectile.Size * 0.5f;
            if (point == Projectile.Size * 0.5f || Vector2.DistanceSquared(point, previous) > 72f * 72f)
                continue;
            float fade = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawVertical("BloomLineSoftEdge", point, previous,
                new Color(220, 245, 255) * (fade * 0.38f * Projectile.Opacity), 13f * fade + 2f);
        }
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 24f,
            new Color(220, 245, 255) * (0.54f * Projectile.Opacity), direction.ToRotation(),
            new Vector2(126f, 44f));
        if (Age >= Projectile.ai[0] && Age <= Projectile.ai[0] + 5f)
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center + direction * 18f,
                Color.White * (0.74f * Projectile.Opacity), direction.ToRotation(), new Vector2(58f, 32f));
        FranVfx.EndAdditive();
        Texture2D texture = FranShiFuVisuals.Texture;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.Lerp(new Color(80, 115, 145), Color.White, 0.52f) * Projectile.Opacity,
            Projectile.rotation, texture.Size() * 0.5f,
            0.68f * FranShiFuVisuals.TextureScale, SpriteEffects.None, 0f);
        return false;
    }
}
