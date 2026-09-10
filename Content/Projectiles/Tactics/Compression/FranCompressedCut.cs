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


public sealed class FranCompressedCut : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 10; Projectile.height = 10; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 18;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.timeLeft is <= 13 and >= 9 ? null : false;
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float collisionPoint = 0f;
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - direction * 26f, Projectile.Center + direction * (310f + Projectile.ai[0] * 46f),
            11f + Projectile.ai[0] * 2f, ref collisionPoint);
    }
    public override void AI()
    {
        if (Projectile.timeLeft == 13)
        {
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.78f, Pitch = -0.18f }, Projectile.Center);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 5f + Projectile.ai[0]);
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float age = 18f - Projectile.timeLeft;
        float warning = 1f - FranMotion.Smooth(FranMotion.Segment(age, 0f, 5f));
        float release = FranMotion.Pulse(age, 4f, 6f, 9f, 16f);
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 start = Projectile.Center - direction * 26f;
        Vector2 end = Projectile.Center + direction * (310f + Projectile.ai[0] * 46f);
        Vector2 midpoint = (start + end) * 0.5f;
        float length = Vector2.Distance(start, end);
        FranVfx.BeginAdditive();
        FranVfx.DrawVertical("FranVfx_ThinEndedLine", start, end,
            new Color(210, 250, 255) * (0.14f * warning + 0.76f * release), 2f + release * 12f);
        if (release > 0f)
        {
            FranVfx.DrawOriginalPixels("FranSmearForward", midpoint,
                new Color(155, 225, 255) * (release * 0.62f), direction.ToRotation(),
                new Vector2(length, 52f + Projectile.ai[0] * 5f));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", end, Color.White * (release * 0.82f),
                direction.ToRotation(), new Vector2(82f, 44f));
        }
        FranVfx.EndAdditive();
        return false;
    }
}
