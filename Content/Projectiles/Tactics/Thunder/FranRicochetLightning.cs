using System;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


public sealed class FranRicochetLightning : ModProjectile
{
    private int MaximumJumps => Projectile.ai[1] > 0f ? Math.Clamp((int)Projectile.ai[1], 2, 7) : 4;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 18; Projectile.height = 18; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = 4; Projectile.timeLeft = 76;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 12;
    }
    public override void AI()
    {
        if (Projectile.localAI[0] == 0f)
            Projectile.penetrate = MaximumJumps;
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (Projectile.localAI[0]++ > 7f && Projectile.localAI[0] < 28f)
        {
            NPC target = FindBounceTarget(-1, 360f);
            if (target is not null)
            {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity.SafeNormalize(Vector2.UnitX)) * 19f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.075f);
            }
        }
        Lighting.AddLight(Projectile.Center, 0.18f, 0.14f, 0.58f);
        FranFlatLightningEffects.EmitTravel(Projectile.Center, Projectile.velocity,
            0.72f, FranFlatLightningPalette.ThunderChain, 3,
            (int)Projectile.localAI[0] + Projectile.identity * 5);
        if (!Main.dedServ && Main.rand.NextBool(2))
            FranParticleSystem.Spawn(new FranBarrierShardParticle(Projectile.Center,
                -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(1.2f, 1.2f),
                new Color(155, 125, 255), new Vector2(12f, 4f), 10));
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranLightingBoltsSystem.SpawnThunderImpact(target.Center, new Color(155, 125, 255), 0.72f);
        SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.33f, Pitch = 0.25f + Projectile.ai[0] * 0.08f }, target.Center);
        Projectile.ai[0]++;
        NPC next = FindBounceTarget(target.whoAmI, 470f);
        if (Projectile.ai[0] >= MaximumJumps || next is null)
        {
            Projectile.Kill();
            return;
        }
        Projectile.Center = target.Center;
        Projectile.velocity = (next.Center - target.Center).SafeNormalize(Vector2.UnitX) * 20f;
        Projectile.damage = Math.Max(1, (int)(Projectile.damage * 0.82f));
        Projectile.localAI[0] = 0f;
        Projectile.netUpdate = true;
    }
    public override bool PreDraw(ref Color lightColor) => false;

    private NPC FindBounceTarget(int excluded, float distance)
    {
        NPC best = null;
        float bestDistance = distance * distance;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.whoAmI == excluded || !npc.CanBeChasedBy() || Projectile.localNPCImmunity[npc.whoAmI] > 0)
                continue;
            float candidate = Vector2.DistanceSquared(Projectile.Center, npc.Center);
            if (candidate < bestDistance)
            {
                bestDistance = candidate;
                best = npc;
            }
        }
        return best;
    }
}
