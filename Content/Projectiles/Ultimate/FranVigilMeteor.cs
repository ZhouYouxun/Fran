using System;
using FranMod.Content.Players;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles.Ultimate;

public sealed class FranVigilMeteor : ModProjectile
{
    // Actual vanilla Falling Star projectile art, not an approximation or item icon.
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 22;
        Projectile.friendly = true; Projectile.DamageType = DamageClass.Melee;
        Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.penetrate = 3; Projectile.timeLeft = 180;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 20;
    }
    public override void AI()
    {
        if (Projectile.owner >= 255 || !Main.player[Projectile.owner].GetModPlayer<FranVigilPlayer>().Active)
        { Projectile.Kill(); return; }
        Projectile.rotation += .18f;
        if (Projectile.Center.Y >= Projectile.ai[0]) Projectile.tileCollide = true;
        if (Main.dedServ) return;
        Lighting.AddLight(Projectile.Center, new Vector3(.2f, .5f, .8f));
        if (Projectile.timeLeft % 3 == 0)
            FranParticleSystem.Spawn(new FranGlowOrbParticle(Projectile.Center, -Projectile.velocity * .08f,
                new Color(100, 180, 255), .16f, .03f, 18), false);
        if (Projectile.timeLeft % 5 == 0)
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12, 12),
                DustID.BlueCrystalShard, -Projectile.velocity * .12f, 120, new Color(145, 200, 255), .8f);
            dust.noGravity = true;
        }
    }
    public override void OnKill(int timeLeft)
    {
        if (Main.dedServ) return;
        SoundEngine.PlaySound(SoundID.Item10 with { Volume = .45f, Pitch = .2f }, Projectile.Center);
        FranVigilWaveParticles.Ring(Projectile.Center, new Color(100, 195, 255), .8f, 16);
        for (int i = 0; i < 10; i++)
            FranParticleSystem.Spawn(new FranSparkParticle(Projectile.Center,
                Main.rand.NextVector2CircularEdge(6, 6), new Color(120, 205, 255), .45f, new Vector2(.35f, 1), 20), false);
        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 2f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D star = TextureAssets.Projectile[Type].Value;
        Texture2D bloom = FranParticleAssets.Get("CL_BloomCircle");
        for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
        {
            if (Projectile.oldPos[i] == Vector2.Zero) continue;
            float fade = 1 - i / (float)Projectile.oldPos.Length;
            Vector2 position = Projectile.oldPos[i] + Projectile.Size * .5f - Main.screenPosition;
            Main.EntitySpriteDraw(star, position, null, new Color(80, 135, 255, 0) * fade * .45f,
                Projectile.rotation - i * .12f, star.Size() * .5f, .3f + fade * .6f, 0);
        }
        Main.EntitySpriteDraw(bloom, Projectile.Center - Main.screenPosition, null, new Color(90, 160, 255, 0) * .55f,
            0, bloom.Size() * .5f, .25f, 0);
        Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null, Color.White,
            Projectile.rotation, star.Size() * .5f, 1.05f, 0);
        return false;
    }
}
