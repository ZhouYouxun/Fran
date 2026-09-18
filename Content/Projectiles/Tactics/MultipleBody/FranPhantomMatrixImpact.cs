using FranMod.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// 独立且同步的纯视觉弹幕：不受普通粒子数量上限影响，不增加输出或命中范围。
public sealed class FranPhantomMatrixImpact : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_0";
    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 700;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 2;
        Projectile.friendly = Projectile.hostile = false;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = FranPhantomVisuals.ImpactLifetime;
        Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        if (Projectile.localAI[0]++ == 0f && !Main.dedServ)
        {
            FranParticleEffects.PhantomBladeExplosion(Projectile.Center, Projectile.ai[0].ToRotationVector2(), 0.72f);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.46f, Pitch = -0.12f }, Projectile.Center);
        }
        if (!Main.dedServ && ((int)Projectile.ai[2] == 5 || (int)Projectile.ai[2] == 11))
            FranParticleEffects.PhantomMatrixAftershock(Projectile.Center, Projectile.ai[0].ToRotationVector2());
        Projectile.ai[2]++;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        FranPhantomVisuals.DrawMatrixImpact(Projectile.Center, Projectile.ai[0], Projectile.ai[2], (int)Projectile.ai[1]);
        return false;
    }
}
