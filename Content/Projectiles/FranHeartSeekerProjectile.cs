using System;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// 可造成伤害的心形星光弹幕；碰撞与多人伤害由服务端判定，不属于纯视觉粒子。
internal sealed class FranHeartSeekerProjectile : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 20;
        Projectile.height = 20;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 90;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 1;
    }

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        NPC target = FindTarget(620f);
        if (target is not null && age > 8f)
        {
            float speed = MathHelper.Clamp(Projectile.velocity.Length() + 0.12f, 8f, 17f);
            Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * speed;
            float turn = MathHelper.Lerp(0.045f, 0.16f, Utils.GetLerpValue(8f, 42f, age, true));
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, turn);
        }
        else if (Projectile.velocity.Length() < 12f)
            Projectile.velocity *= 1.018f;

        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        Lighting.AddLight(Projectile.Center, 0.38f, 0.08f, 0.30f);

        if (!Main.dedServ && ((int)age & 1) == 0)
        {
            FranParticleSystem.Spawn(new FranHeartParticle(Projectile.Center,
                -Projectile.velocity * 0.045f + Main.rand.NextVector2Circular(0.35f, 0.35f),
                Main.rand.NextBool(3) ? Color.White : new Color(255, 95, 190),
                Main.rand.NextFloat(0.11f, 0.19f), Main.rand.Next(16, 26), Main.rand.NextFloat(-0.02f, 0.02f)));
            FranParticleSystem.Spawn(new FranStarMoteParticle(Projectile.Center,
                -Projectile.velocity * 0.025f, Color.White, new Color(205, 105, 255),
                Main.rand.NextFloat(0.16f, 0.28f), Main.rand.Next(12, 20)));
        }
    }

    private NPC FindTarget(float range)
    {
        NPC best = null;
        float bestDistance = range * range;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy(Projectile))
                continue;
            float distance = Vector2.DistanceSquared(Projectile.Center, npc.Center);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = npc;
            }
        }
        return best;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        FranVfx.BeginAdditive();
        for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
        {
            Vector2 position = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            if (position == Projectile.Size * 0.5f)
                continue;
            float completion = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawShared("HeartParticle", position, new Color(255, 80, 180) * (0.18f * completion),
                Projectile.rotation, 0.16f * completion);
        }
        float pulse = 0.88f + MathF.Sin(Main.GlobalTimeWrappedHourly * 15f + Projectile.identity) * 0.1f;
        FranVfx.DrawShared("BloomCircle", Projectile.Center, new Color(225, 85, 255) * 0.48f, 0f, 0.18f * pulse);
        FranVfx.DrawShared("HeartParticle", Projectile.Center, new Color(255, 115, 205) * 0.88f,
            Projectile.rotation, 0.38f * pulse);
        FranVfx.DrawShared("HeartParticle", Projectile.Center, Color.White * 0.48f,
            Projectile.rotation, 0.19f * pulse);
        FranVfx.EndAdditive();
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranLightingBoltsSystem.SpawnMagicHearts(target.Center, -Projectile.velocity,
            new Color(255, 95, 190), 8, 0.82f);
        FranParticleEffects.HeartMagic(target.Center, Projectile.velocity, new Color(215, 105, 255), 0.85f, 7);
    }
}
