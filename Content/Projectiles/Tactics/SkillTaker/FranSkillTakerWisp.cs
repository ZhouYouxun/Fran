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


public sealed class FranSkillTakerWisp : ModProjectile
{
    private const float DeploymentTime = 18f;
    private const float TrackingRampTime = 36f;
    private const float ReturnSpeed = 27f;
    private float Age => Projectile.localAI[0];

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 88;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool? CanDamage() => Age >= DeploymentTime ? null : false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        if (age < DeploymentTime)
        {
            // Positive angles turn clockwise in screen space. Never alternate the sign by
            // identity: every extra projectile belongs to the same clockwise deployment.
            float clockwiseCurve = MathHelper.Lerp(0.070f, 0.022f, age / DeploymentTime);
            Projectile.velocity = Projectile.velocity.RotatedBy(clockwiseCurve) * 1.010f;
        }
        else
        {
            NPC target = FindTarget();
            if (target is not null)
            {
                Vector2 fallback = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 intercept = target.Center + target.velocity * 2.5f;
                float trackingPower = MathHelper.Clamp((age - DeploymentTime) / TrackingRampTime, 0f, 1f);
                float speed = MathHelper.Lerp(18f, ReturnSpeed, trackingPower);
                float steering = MathHelper.Lerp(0.08f, 0.30f, trackingPower);
                Vector2 desired = (intercept - Projectile.Center).SafeNormalize(fallback) * speed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, steering);
            }
            else
                Projectile.velocity *= 0.992f;

            if (age == DeploymentTime)
            {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.20f, Pitch = 0.52f }, Projectile.Center);
                FranParticleEffects.Implosion(Projectile.Center, new Color(205, 95, 255), 0.42f);
            }
        }

        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Projectile.rotation = direction.ToRotation();
        Lighting.AddLight(Projectile.Center, 0.34f, 0.07f, 0.48f);
        if (!Main.dedServ)
            SpawnNovaTrail(direction);
    }

    private void SpawnNovaTrail(Vector2 direction)
    {
        Vector2 side = direction.RotatedBy(MathHelper.PiOver2);
        Color trailColor = ((int)Age & 1) == 0 ? new Color(224, 150, 255) : new Color(174, 62, 245);
        FranParticleSystem.Spawn(new FranBloomCirclePulseParticle(Projectile.Center,
            -direction * 0.8f, trailColor, 16f, 32f, 10, 0.72f, 0.91f));

        Dust dust = Dust.NewDustPerfect(
            Projectile.Center - direction * Main.rand.NextFloat(3f, 12f) + side * Main.rand.NextFloat(-6f, 6f),
            DustID.PurpleTorch,
            -direction * Main.rand.NextFloat(0.6f, 2f) + side * Main.rand.NextFloat(-0.3f, 0.3f),
            0,
            new Color(220, 130, 255),
            Main.rand.NextFloat(0.75f, 1.2f));
        dust.noGravity = true;
    }

    private NPC FindTarget()
    {
        NPC best = null;
        float bestDistance = 720f * 720f;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy())
                continue;
            float distance = Vector2.DistanceSquared(Projectile.Center, npc.Center);
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            best = npc;
        }
        return best;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner,
            FranBurstStyle.SkillTaker, 0.48f + Projectile.ai[1] * 0.025f,
            rotation: Projectile.velocity.ToRotation());
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = MathHelper.Clamp(Projectile.timeLeft / 16f, 0f, 1f);
        FranVfx.BeginAdditive();
        float pulse = 0.9f + MathF.Sin(Age * 0.24f) * 0.1f;
        FranVfx.DrawShared("CL_BloomCircle", Projectile.Center,
            new Color(205, 100, 255, 0) * (0.42f * opacity), Projectile.rotation, 0.44f * pulse);
        FranVfx.EndAdditive();
        return false;
    }
}
