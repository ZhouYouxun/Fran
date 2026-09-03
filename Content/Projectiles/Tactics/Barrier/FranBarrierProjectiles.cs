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


public sealed class FranAutoBarrierProjectile : ModProjectile
{
    private static float MaxIntegrity => Math.Max(1, FranBalance.GetInt(
        nameof(FranBalance.MagicBarrierMaximumIntegrity), FranBalance.MagicBarrierMaximumIntegrity));
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 104; Projectile.height = 136; Projectile.timeLeft = 3600;
        Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.netImportant = true;
        Projectile.ai[1] = MaxIntegrity;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }
        if (Projectile.localAI[1] == 0f)
        {
            Projectile.localAI[1] = 1f;
            if (Projectile.ai[1] <= 0f)
                Projectile.ai[1] = MaxIntegrity;
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.46f, Pitch = 0.32f }, Projectile.Center);
            EmitShards(Projectile.Center, new Color(95, 190, 255), 8, true);
            FranParticleEffects.RingPulse(Projectile.Center, new Color(105, 205, 255), Vector2.One * 34f,
                new Vector2(126f, 162f), 22, 0f, 0.024f, 0.72f);
            FranParticleEffects.MysticGlyphs(Projectile.Center, new Color(125, 215, 255), 5, 54f, 0.82f);
            FranVanillaDustEffects.SkillRelease(Projectile.Center, -Vector2.UnitY,
                FranSkill.MagicBarrier, new Color(105, 205, 255), 13, 1f);
        }
        if (owner.whoAmI == Main.myPlayer &&
            owner.GetModPlayer<FranPlayer>().GetSkillStacks(FranSkill.MagicBarrier) <= 0)
        {
            Projectile.localAI[2] = 2f;
            Projectile.Kill();
            return;
        }
        Projectile.Center = owner.MountedCenter;
        Projectile.rotation += 0.012f;
        Projectile.timeLeft = 2;
        Projectile.ai[0]++;
        Projectile.ai[2] = Math.Max(0f, Projectile.ai[2] - 0.075f);
        Lighting.AddLight(Projectile.Center, 0.10f + Projectile.ai[2] * 0.18f,
            0.28f + Projectile.ai[2] * 0.22f, 0.48f + Projectile.ai[2] * 0.26f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float integrity = MathHelper.Clamp(Projectile.ai[1] / MaxIntegrity, 0f, 1f);
        float hitFlash = MathHelper.Clamp(Projectile.ai[2], 0f, 1f);
        FranBarrierRenderer.TryDraw(Projectile.Center, integrity, hitFlash, Projectile.rotation);
        return false;
    }

    public override void OnKill(int timeLeft)
    {
        bool broken = Projectile.localAI[2] == 1f || Projectile.ai[1] <= 0f;
        Color color = broken ? new Color(105, 185, 255) : new Color(85, 145, 205);
        SoundEngine.PlaySound((broken ? SoundID.Item122 : SoundID.Item27) with
            { Volume = broken ? 0.62f : 0.30f, Pitch = broken ? -0.12f : 0.42f }, Projectile.Center);
        EmitShards(Projectile.Center, color, broken ? 18 : 7, false);
        FranParticleEffects.RingPulse(Projectile.Center, color, new Vector2(72f, 92f),
            new Vector2(broken ? 184f : 126f, broken ? 218f : 158f), broken ? 24 : 17,
            Projectile.rotation, broken ? 0.052f : 0.024f, broken ? 1f : 0.58f);
        if (broken)
        {
            FranBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center, Projectile.owner,
                FranBurstStyle.Barrier, 1.08f);
            FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_Death(), Projectile.Center,
                -Vector2.UnitY, Projectile.owner, FranSpectatorPalette.Cyan, 5, 1.02f);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 5.5f);
        }
    }

    private static void EmitShards(Vector2 center, Color color, int count, bool inward)
    {
        if (Main.dedServ)
            return;
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Main.rand.NextVector2Unit();
            Vector2 position = inward ? center + direction * Main.rand.NextFloat(28f, 68f) : center;
            Vector2 velocity = direction * Main.rand.NextFloat(inward ? -4.5f : 2.5f, inward ? -1.8f : 7f);
            FranParticleSystem.Spawn(new FranBarrierShardParticle(position, velocity, color,
                new Vector2(Main.rand.NextFloat(12f, 24f), Main.rand.NextFloat(3f, 7f)), Main.rand.Next(14, 24)));
        }
    }

    public static bool IsActive(Player owner)
    {
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.owner == owner.whoAmI &&
                projectile.type == ModContent.ProjectileType<FranAutoBarrierProjectile>() && projectile.ai[1] > 0f)
                return true;
        return false;
    }

    public static bool HandlePlayerHit(Player owner, int damageTaken)
    {
        if (owner.whoAmI != Main.myPlayer)
            return false;
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner != owner.whoAmI ||
                projectile.type != ModContent.ProjectileType<FranAutoBarrierProjectile>() || projectile.ai[1] <= 0f)
                continue;

            projectile.ai[1] -= 1f;
            projectile.ai[2] = 1f;
            projectile.netUpdate = true;
            FranBurst.Spawn(projectile.GetSource_FromThis(), owner.Center, owner.whoAmI,
                FranBurstStyle.Barrier, 0.82f + Math.Min(0.34f, damageTaken / 180f),
                Math.Max(1, (int)(projectile.damage *
                    FranBalance.GetFloat(nameof(FranBalance.MagicBarrierCounterDamageMultiplier),
                        FranBalance.MagicBarrierCounterDamageMultiplier))), 5f);
            EmitShards(owner.Center, new Color(105, 205, 255), 10, false);
            FranParticleEffects.BloomPulse(owner.Center, new Color(105, 205, 255), 24f, 104f,
                16, 0.72f, true);
            FranParticleEffects.RingPulse(owner.Center, Color.White, new Vector2(62f, 82f),
                new Vector2(138f, 174f), 17, projectile.rotation, -0.036f, 0.64f);
            FranVanillaDustEffects.Impact(owner.Center, Vector2.UnitY, FranSkill.MagicBarrier,
                new Color(105, 205, 255), 18, 1f);
            SoundEngine.PlaySound(SoundID.NPCHit53 with { Volume = 0.50f, Pitch = 0.24f }, owner.Center);

            owner.immune = true;
            int immunityFrames = FranBalance.GetInt(nameof(FranBalance.MagicBarrierHitImmunityFrames),
                FranBalance.MagicBarrierHitImmunityFrames);
            owner.immuneTime = Math.Max(owner.immuneTime, immunityFrames);
            for (int i = 0; i < owner.hurtCooldowns.Length; i++)
                owner.hurtCooldowns[i] = Math.Max(owner.hurtCooldowns[i], immunityFrames);

            if (projectile.ai[1] <= 0f)
            {
                projectile.localAI[2] = 1f;
                owner.GetModPlayer<FranPlayer>().OnBarrierBroken();
                projectile.Kill();
            }
            return true;
        }
        return false;
    }
}

public sealed class FranBarrierCounterCut : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 12; Projectile.height = 12; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 16;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.timeLeft is <= 14 and >= 8 ? null : false;
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float collisionPoint = 0f;
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center,
            Projectile.Center + direction * (230f + Projectile.ai[0] * 42f), 13f + Projectile.ai[0] * 3f,
            ref collisionPoint);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float progress = 1f - Projectile.timeLeft / 16f;
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 end = Projectile.Center + direction * (230f + Projectile.ai[0] * 42f);
        Vector2 midpoint = (Projectile.Center + end) * 0.5f;
        float length = Vector2.Distance(Projectile.Center, end);
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranSmearForward", midpoint,
            new Color(90, 185, 255) * (opacity * 0.72f), direction.ToRotation(),
            new Vector2(length, 42f + Projectile.ai[0] * 6f));
        FranVfx.DrawVertical("Cal_ThinEndedLine", Projectile.Center, end, Color.White * (opacity * 0.82f), 5f);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", end, Color.White * (opacity * 0.84f),
            direction.ToRotation(), new Vector2(76f, 42f));
        FranVfx.EndAdditive();
        return false;
    }
}
