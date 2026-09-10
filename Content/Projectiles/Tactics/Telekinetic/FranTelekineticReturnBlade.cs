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


public sealed class FranTelekineticReturnBlade : ModProjectile
{
    public override string Texture => FranShiFuVisuals.TexturePath;
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 42; Projectile.height = 42; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 70;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 12;
    }
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }
        float age = Projectile.localAI[0]++;
        if (age >= 17f)
        {
            Vector2 toOwner = owner.MountedCenter - Projectile.Center;
            if (toOwner.LengthSquared() < 28f * 28f) { Projectile.Kill(); return; }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                toOwner.SafeNormalize(Vector2.UnitX * owner.direction) * 25f, 0.19f);
        }
        else if (age < 6f)
            Projectile.velocity *= 1.035f;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        Lighting.AddLight(Projectile.Center, 0.12f, 0.34f, 0.54f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        FranVfx.BeginAdditive();
        for (int i = 1; i < Projectile.oldPos.Length; i++)
        {
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            if (point == Projectile.Size * 0.5f)
                continue;
            float fade = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawVertical("TraceLong", point - direction * 22f, point + direction * 4f,
                new Color(115, 225, 255) * (fade * 0.25f), 7f + fade * 8f);
        }
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 24f,
            new Color(120, 220, 255) * 0.44f, direction.ToRotation(), new Vector2(124f, 38f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center + direction * 14f,
            Color.White * 0.74f, direction.ToRotation(), new Vector2(48f, 28f));
        FranVfx.EndAdditive();
        Texture2D texture = FranShiFuVisuals.Texture;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.Lerp(lightColor, Color.White, 0.26f), Projectile.rotation, texture.Size() * 0.5f,
            0.82f * FranShiFuVisuals.TextureScale, SpriteEffects.None, 0f);
        return false;
    }
}
