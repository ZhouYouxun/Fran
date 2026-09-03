using System;
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


public sealed class FranThorsHammerDrop : ModProjectile
{
    private const int PackedTargetStride = 256;
    private bool Impacted => Projectile.localAI[1] > 0f;
    private float Age => Projectile.localAI[0];
    private int PackedTarget => (int)Projectile.ai[2] % PackedTargetStride;
    private int TargetIndex => PackedTarget - 1;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.PaladinsHammerFriendly}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 16;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;
    }

    public override void SetDefaults()
    {
        Projectile.width = 74;
        Projectile.height = 74;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool? CanDamage() => Age >= 3f && !Impacted ? null : false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        Vector2 anchor = new(Projectile.ai[0], Projectile.ai[1]);
        NPC trackedTarget = GetTrackedTarget();
        Vector2 target = trackedTarget?.Center + trackedTarget.velocity * 1.2f ?? anchor;
        Vector2 fallback = Projectile.velocity.SafeNormalize(Vector2.UnitY);
        float speed = MathHelper.Lerp(30f, 68f, FranMotion.Smooth(FranMotion.Segment(age, 0f, 24f)));
        Vector2 desired = (target - Projectile.Center).SafeNormalize(fallback) * speed;
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.22f);
        Projectile.rotation += 0.42f * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);
        const float visualIntensity = 1f;
        Lighting.AddLight(Projectile.Center, 0.38f * visualIntensity, 0.68f * visualIntensity,
            1.18f * visualIntensity);
        FranVanillaDustEffects.ProjectileTrail(Projectile.Center, Projectile.velocity,
            FranSkill.ThorsHammer, new Color(105, 185, 255),
            0.36f * visualIntensity, 1.34f * visualIntensity);

        if (!Main.dedServ)
        {
            FranFlatLightningEffects.EmitTravel(Projectile.Center, Projectile.velocity,
                0.84f, FranFlatLightningPalette.ThorsHammer, 3,
                (int)age + Projectile.identity * 11);
            const int particlePasses = 1;
            for (int i = 0; i < particlePasses; i++)
            {
                FranParticleSystem.Spawn(new FranBarrierShardParticle(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f) * visualIntensity,
                    -Projectile.velocity * 0.075f + Main.rand.NextVector2Circular(2.1f, 2.1f) * visualIntensity,
                    new Color(120, 205, 255),
                    new Vector2(Main.rand.NextFloat(16f, 31f), Main.rand.NextFloat(3f, 7f)) * visualIntensity, 17));
                FranParticleSystem.Spawn(new FranLightParticle(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 16f) * visualIntensity,
                    -Projectile.velocity * Main.rand.NextFloat(0.055f, 0.11f) +
                    Main.rand.NextVector2Circular(1.4f, 1.4f) * visualIntensity,
                    Main.rand.NextFloat(0.44f, 0.76f) * visualIntensity,
                    Main.rand.NextBool(3) ? Color.White : new Color(105, 185, 255),
                    Main.rand.Next(15, 24), 0.88f, 1.35f, 3.2f));
            }
            if ((int)age % 2 == 0)
                FranParticleSystem.Spawn(new FranGlowOrbParticle(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 18f),
                    -Projectile.velocity * 0.045f + Main.rand.NextVector2Circular(1f, 1f),
                    new Color(105, 185, 255), Main.rand.NextFloat(0.20f, 0.34f) * visualIntensity, 0.03f,
                    Main.rand.Next(13, 20)));
            if ((int)age % 3 == 0)
                FranParticleSystem.Spawn(new FranHeavySmokeParticle(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    -Projectile.velocity * 0.045f, new Color(28, 44, 82), Main.rand.NextFloat(0.52f, 0.86f), 27,
                    0.56f, true));
        }

        Vector2 remaining = target - Projectile.Center;
        bool passedTarget = age >= 3f && Vector2.Dot(remaining, Projectile.velocity) <= 0f;
        if (remaining.LengthSquared() <= 42f * 42f || passedTarget || age >= 46f)
            Impact(target);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Impact(target.Center);

    private void Impact(Vector2 center)
    {
        if (Impacted)
            return;
        Projectile.localAI[1] = 1f;
        Projectile.Center = center;
        if (Main.myPlayer == Projectile.owner)
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), center, Vector2.Zero,
                ModContent.ProjectileType<ThorsHammerBolt>(), Math.Max(1, (int)(Projectile.damage *
                    FranBalance.GetFloat(nameof(FranBalance.ThorsHammerExplosionDamageMultiplier),
                        FranBalance.ThorsHammerExplosionDamageMultiplier))),
                Projectile.knockBack, Projectile.owner);
        Projectile.netUpdate = true;
        Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor)
    {
        const float visualIntensity = 1f;
        Texture2D texture = TextureAssets.Projectile[ProjectileID.PaladinsHammerFriendly].Value;
        Vector2 origin = texture.Size() * 0.5f;
        if (!FranProjectileTrailRenderer.TryDraw(Projectile, FranProjectileTrailProfile.HeavyHammer,
                new Color(8, 24, 64), new Color(80, 165, 255), Color.White, 38f, 0.94f))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.HeavyHammer,
                new Color(80, 165, 255), 44f, 0.92f);
        FranVfx.BeginAdditive();
        Vector2 fallDirection = Projectile.velocity.SafeNormalize(Vector2.UnitY);
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - fallDirection * 42f,
            new Color(105, 185, 255) * (0.62f * visualIntensity), fallDirection.ToRotation(),
            new Vector2(186f, 60f) * visualIntensity);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            new Color(195, 238, 255) * (0.82f * visualIntensity), fallDirection.ToRotation(),
            new Vector2(104f, 56f) * visualIntensity);
        FranVfx.DrawBloom(Projectile.Center, new Color(95, 175, 255),
            76f * visualIntensity, 0.72f * visualIntensity, true);
        FranVfx.EndAdditive();

        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White,
            Projectile.rotation, origin, 1.05f, SpriteEffects.None, 0f);
        return false;
    }

    private NPC GetTrackedTarget()
    {
        if (TargetIndex < 0 || !Main.npc.IndexInRange(TargetIndex))
            return null;
        NPC target = Main.npc[TargetIndex];
        return target.active && !target.friendly && target.life > 0 ? target : null;
    }
}
