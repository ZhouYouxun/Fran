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


public sealed class FranBlueResonanceEcho : ModProjectile
{
    private const int Lifetime = 150;
    private const int ScatterFrames = 14;
    private const float SearchRange = 3200f;
    private bool sourceBurstSpawned;
    private bool impactBurstSpawned;

    private float Age => Projectile.localAI[0];
    private bool HealingMode => Projectile.localAI[1] > 0.5f;

    internal static float EncodeEchoCount(int echoCount) => Math.Max(1, echoCount);

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;
    }

    public override void SetDefaults()
    {
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.friendly = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool? CanDamage() => Age >= ScatterFrames && !HealingMode ? null : false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        if (!sourceBurstSpawned)
        {
            sourceBurstSpawned = true;
            SpawnBloomBurst(Projectile.Center, 0.72f);
            // 一次蓝辉共鸣只由首枚通用弹幕生成一组波体光粒子，避免数量随弹幕数重复相乘。
            if (Projectile.ai[1] < 0.5f)
                FranWaveSquishyLightParticle.SpawnBlueResonanceBurst(Projectile.Center);
        }

        int healTargetIndex = FindInjuredPlayerIndex();
        bool healing = healTargetIndex >= 0;
        Projectile.localAI[1] = healing ? 1f : 0f;
        Projectile.friendly = age >= ScatterFrames && !healing;

        if (age < ScatterFrames)
        {
            Projectile.velocity *= 0.94f;
            Projectile.rotation += 0.16f * (Projectile.ai[1] % 2f == 0f ? 1f : -1f);
        }
        else if (healing)
        {
            Player targetPlayer = Main.player[healTargetIndex];
            HomeTowards(targetPlayer.Center, 15.5f, 0.16f);
            if (Vector2.DistanceSquared(Projectile.Center, targetPlayer.Center) <= 25f * 25f)
            {
                HealPlayer(targetPlayer);
                return;
            }
        }
        else
        {
            NPC targetNpc = FindEnemyTarget();
            if (targetNpc is not null)
                HomeTowards(targetNpc.Center + targetNpc.velocity * 1.5f, 19f, 0.19f);
            else
                Projectile.velocity *= 0.985f;
        }

        Projectile.rotation += 0.085f;
        Projectile.Opacity = Utils.GetLerpValue(0f, 5f, age, true) *
            Utils.GetLerpValue(0f, 16f, Projectile.timeLeft, true);
        Lighting.AddLight(Projectile.Center, 0.12f * Projectile.Opacity,
            0.48f * Projectile.Opacity, 0.68f * Projectile.Opacity);

        if (!Main.dedServ && (int)age % 2 == 0)
        {
            Color color = healing ? new Color(135, 235, 255) : new Color(70, 175, 255);
            FranParticleSystem.Spawn(new FranGlowOrbParticle(
                Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                -Projectile.velocity * 0.025f + Main.rand.NextVector2Circular(0.18f, 0.18f),
                color, Main.rand.NextFloat(0.16f, 0.26f), 0.025f, Main.rand.Next(10, 16)));
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        impactBurstSpawned = true;
        SpawnBloomBurst(target.Center, 0.94f);
        FranParticleEffects.SpawnImpact(target.Center,
            Projectile.velocity.SafeNormalize(Vector2.UnitX), new Color(85, 205, 255), 7, 4.6f, 0.86f);
    }

    public override void OnKill(int timeLeft)
    {
        if (!impactBurstSpawned)
            SpawnBloomBurst(Projectile.Center, 0.58f);
    }

    private void HomeTowards(Vector2 destination, float speed, float responsiveness)
    {
        Vector2 fallback = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 desiredVelocity = (destination - Projectile.Center).SafeNormalize(fallback) * speed;
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, responsiveness);
    }

    private int FindInjuredPlayerIndex()
    {
        int bestIndex = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player candidate = Main.player[i];
            if (!candidate.active || candidate.dead || candidate.statLife >= candidate.statLifeMax2 ||
                Vector2.DistanceSquared(candidate.Center, Projectile.Center) > SearchRange * SearchRange)
                continue;
            float missingRatio = 1f - candidate.statLife / (float)Math.Max(1, candidate.statLifeMax2);
            float distanceBias = 1f - MathHelper.Clamp(Vector2.Distance(candidate.Center, Projectile.Center) /
                SearchRange, 0f, 1f);
            float score = missingRatio * 3f + distanceBias * 0.18f;
            if (score <= bestScore)
                continue;
            bestScore = score;
            bestIndex = i;
        }
        return bestIndex;
    }

    private NPC FindEnemyTarget()
    {
        NPC best = null;
        float bestDistance = SearchRange * SearchRange;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy(Projectile))
                continue;
            float distance = Vector2.DistanceSquared(Projectile.Center, npc.Center);
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            best = npc;
        }
        return best;
    }

    private void HealPlayer(Player target)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            int divisor = Math.Max(1, FranBalance.GetInt(nameof(FranBalance.BlueResonanceHealingLifeDivisor),
                FranBalance.BlueResonanceHealingLifeDivisor));
            int minimum = FranBalance.GetInt(nameof(FranBalance.BlueResonanceHealingMinimum),
                FranBalance.BlueResonanceHealingMinimum);
            int maximum = Math.Max(minimum, FranBalance.GetInt(nameof(FranBalance.BlueResonanceHealingMaximum),
                FranBalance.BlueResonanceHealingMaximum));
            int healing = Math.Clamp(target.statLifeMax2 / divisor, minimum, maximum);
            target.GetModPlayer<FranPlayer>().ApplyResonanceHealing(healing);
        }
        impactBurstSpawned = true;
        SpawnBloomBurst(target.Center, 0.92f);
        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.26f, Pitch = 0.55f }, target.Center);
        Projectile.Kill();
    }

    private static void SpawnBloomBurst(Vector2 center, float intensity)
    {
        if (Main.dedServ)
            return;
        Color cyan = new(85, 205, 255);
        FranParticleSystem.Spawn(new FranBloomCirclePulseParticle(center, Vector2.Zero, cyan,
            18f * intensity, 76f * intensity, 18, 0.82f, 0.94f, 0.035f));
        FranParticleEffects.Implosion(center, cyan, 0.34f * intensity);
        FranVfx.SparkBurst(center, Color.Lerp(cyan, Color.White, 0.45f),
            Math.Max(4, (int)(7 * intensity)), 4.6f * intensity, 0.62f * intensity);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = Projectile.Opacity;
        float pulse = 0.88f + MathF.Sin(Main.GlobalTimeWrappedHourly * 8.2f + Projectile.identity * 0.73f) * 0.12f;
        Color cyan = HealingMode ? new Color(125, 230, 255) : new Color(70, 175, 255);
        FranVfx.BeginAdditive();
        for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
        {
            Vector2 old = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            Vector2 newer = Projectile.oldPos[i - 1] + Projectile.Size * 0.5f;
            if (old == Projectile.Size * 0.5f || newer == Projectile.Size * 0.5f)
                continue;
            float fade = (1f - i / (float)Projectile.oldPos.Length) * opacity;
            FranVfx.DrawVertical("BloomLineSoftEdge", old, newer, cyan * (0.24f * fade), 9f * fade + 2f);
            FranVfx.DrawVertical("FranVfx_ThinEndedLine", old, newer, Color.White * (0.42f * fade), 2.2f);
        }
        FranVfx.DrawShared("CL_BloomCircle", Projectile.Center,
            new Color(cyan.R, cyan.G, cyan.B, 0) * (0.78f * opacity), Projectile.rotation,
            0.18f * pulse);
        FranVfx.DrawSharedPixels("FranMysticGlyph1", Projectile.Center,
            cyan * (0.34f * opacity), -Projectile.rotation * 0.62f, Vector2.One * (28f * pulse));
        FranVfx.DrawShared("FranVfx_PulseStar", Projectile.Center, Color.White * (0.72f * opacity),
            Projectile.rotation * 0.35f, 0.105f * pulse);
        FranVfx.DrawBloom(Projectile.Center, Color.White, 13f * pulse, 0.64f * opacity, true);
        FranVfx.EndAdditive();
        return false;
    }
}
