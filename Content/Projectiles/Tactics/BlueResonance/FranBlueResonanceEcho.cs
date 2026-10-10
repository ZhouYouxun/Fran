using System;
using FranMod.Content.Companions;
using System.Collections.Generic;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Core.LeonidStarlight;
using FranMod.Effects.LeonidStarlight;
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
    private int ScatterFrames => Math.Max(1, FranBalance.GetInt(nameof(FranBalance.BlueResonanceScatterFrames),
        FranBalance.BlueResonanceScatterFrames));
    private const float SearchRange = 3200f;
    private bool sourceBurstSpawned;
    private bool impactBurstSpawned;
    private int discTargetlessFrames;
    private int homingTargetKey = int.MinValue;
    private float homingAge;

    private float Age => Projectile.localAI[0];
    private bool HealingMode => Projectile.localAI[1] > 0.5f;
    private bool DiscEmission => Projectile.ai[2] < 0f;
    private const int DiscTargetlessGraceFrames = 90;
    private const int DiscTargetlessFadeFrames = 30;

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
        Projectile.timeLeft = 150;
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
            SpawnBloomBurst(Projectile.Center, Projectile.ai[2] < 0f ? 0.32f : 0.72f);
            // 每组首枚释放原版 Lighting Bolts，不再生成 SquishyLight 软光团。
            if (Projectile.ai[1] < 0.5f && Projectile.ai[2] >= 0f)
                FranLightingBoltsSystem.SpawnButterflyLights(Projectile.Center);
        }

        int healTargetIndex = FindInjuredPlayerIndex();
        bool healing = healTargetIndex >= 0;
        NPC discTarget = DiscEmission && !healing ? FindEnemyTarget() : null;
        Projectile.localAI[1] = healing ? 1f : 0f;
        Projectile.friendly = age >= ScatterFrames && !healing;

        if (age < ScatterFrames)
        {
            // The surrounding motes hover briefly before choosing where to go.
            ResetHoming();
            Projectile.velocity *= 0.96f;
            Projectile.rotation += 0.16f * (Projectile.ai[1] % 2f == 0f ? 1f : -1f);
        }
        else if (healing)
        {
            Player targetPlayer = Main.player[healTargetIndex];
            AdvanceHoming(-healTargetIndex - 1);
            HomeTowards(targetPlayer.Center, 15.5f, 0.16f);
            if (Vector2.DistanceSquared(Projectile.Center, targetPlayer.Center) <= 25f * 25f)
            {
                HealPlayer(targetPlayer);
                return;
            }
        }
        else
        {
            NPC targetNpc = DiscEmission ? discTarget : FindEnemyTarget();
            if (targetNpc is not null)
            {
                AdvanceHoming(targetNpc.whoAmI + 1);
                HomeTowardsEnemy(targetNpc);
            }
            else
            {
                ResetHoming();
                Projectile.velocity *= 0.985f;
            }
        }

        if (age == ScatterFrames)
        {
            if (!Main.dedServ)
                SoundEngine.PlaySound(FranCombatSounds.ButterflyBeginTracking, Projectile.Center);
            SpawnDepartureStarlight();
        }

        Projectile.rotation += 0.085f;
        if (DiscEmission)
        {
            // Count consecutive frames without a usable enemy; an injured player
            // remains a valid healing destination and cancels the fade as well.
            discTargetlessFrames = healing || discTarget is not null ? 0 : discTargetlessFrames + 1;
            if (discTargetlessFrames >= DiscTargetlessGraceFrames + DiscTargetlessFadeFrames &&
                (Projectile.owner == Main.myPlayer || Main.netMode == NetmodeID.Server))
            {
                Projectile.Kill();
                return;
            }
        }
        float discFade = DiscEmission ? 1f - Utils.GetLerpValue(DiscTargetlessGraceFrames,
            DiscTargetlessGraceFrames + DiscTargetlessFadeFrames, discTargetlessFrames, true) : 1f;
        Projectile.Opacity = Utils.GetLerpValue(0f, 5f, age, true) *
            Utils.GetLerpValue(0f, 16f, Projectile.timeLeft, true) * discFade;
        Lighting.AddLight(Projectile.Center, FranResonanceVisuals.Blue.ToVector3() * (0.72f * Projectile.Opacity));

        if (FranResonanceVisuals.Visible(Projectile.Center) && (int)age % 2 == 0 &&
            (!DiscEmission || Projectile.Opacity > 0.08f))
        {
            Color color = healing ? FranResonanceVisuals.Cyan : FranResonanceVisuals.Blue;
            FranLightingBoltsSystem.SpawnBlueFlightLight(
                Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                -Projectile.velocity * 0.025f + Main.rand.NextVector2Circular(0.18f, 0.18f),
                color, 0.65f * (DiscEmission ? Projectile.Opacity : 1f));
        }
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        modifiers.FinalDamage *= FranBalance.GetFloat(nameof(FranBalance.BlueResonanceEnemyFinalDamageMultiplier),
            FranBalance.BlueResonanceEnemyFinalDamageMultiplier);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        impactBurstSpawned = true;
        LeonidStarlight.Burst(target.Center, 4, shape: LeonidStarlightShape.Mote,
            speed: 4.8f, scale: 0.56f, hoverTime: 16, lifetime: 78, lanceSpeed: 12f, spawnRadius: 5f);
        SpawnBloomBurst(target.Center, 0.94f);
        FranResonanceBloomParticle.SpawnBurst(target.Center, Projectile.velocity.ToRotation(), 0.85f);
        FranParticleEffects.SpawnImpact(target.Center,
            Projectile.velocity.SafeNormalize(Vector2.UnitX), FranResonanceVisuals.Cyan, 7, 4.6f, 0.86f);
    }

    public override void OnKill(int timeLeft)
    {
        if (!impactBurstSpawned && !DiscEmission)
            SpawnBloomBurst(Projectile.Center, 0.58f);
    }

    // 每只蝴蝶结束悬停、准备起步追踪时，沿其反方向甩出两颗原创星光。
    private void SpawnDepartureStarlight()
    {
        Vector2 backward = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
        LeonidStarlight.Spray(Projectile.Center, backward, 2,
            color: LeonidVisualUtils.GetMeteorColor(Projectile.ai[1] * 0.37f),
            shape: LeonidStarlightShape.Mote, speed: 4.3f, spread: 0.22f, scale: 0.68f,
            hoverTime: 16, lifetime: 82, lanceSpeed: 12f, guaranteeCount: true);
    }

    private void HomeTowards(Vector2 destination, float speed, float responsiveness)
    {
        int blendFrames = Math.Max(1, FranBalance.GetInt(nameof(FranBalance.BlueResonanceHomingBlendFrames),
            FranBalance.BlueResonanceHomingBlendFrames));
        float blend = MathHelper.Clamp(homingAge / blendFrames, 0f, 1f);
        blend = blend * blend * (3f - 2f * blend);
        float startingSpeed = MathHelper.Clamp(FranBalance.GetFloat(
            nameof(FranBalance.BlueResonanceHomingStartingSpeed), FranBalance.BlueResonanceHomingStartingSpeed),
            0.1f, speed);
        speed = MathHelper.Lerp(startingSpeed, speed, blend);
        responsiveness *= MathHelper.Lerp(0.30f, 1f, blend);
        Vector2 fallback = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 desiredVelocity = (destination - Projectile.Center).SafeNormalize(fallback) * speed;
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, responsiveness);
    }

    private void HomeTowardsEnemy(NPC target)
    {
        float randomness = MathHelper.Clamp(FranBalance.GetFloat(
            nameof(FranBalance.BlueResonanceTrajectoryRandomness),
            FranBalance.BlueResonanceTrajectoryRandomness), 0f, 0.75f);
        float radiusVariation = MathHelper.Lerp(1f - randomness, 1f + randomness, RandomUnit(0));
        float speedVariation = MathHelper.Lerp(1f - randomness * 0.42f,
            1f + randomness * 0.42f, RandomUnit(1));
        float responseVariation = MathHelper.Lerp(1f - randomness * 0.30f,
            1f + randomness * 0.30f, RandomUnit(2));
        float angularVariation = MathHelper.Lerp(1f - randomness * 0.65f,
            1f + randomness * 0.65f, RandomUnit(3));
        float handedness = RandomUnit(4) < 0.5f ? -1f : 1f;
        float phase = RandomUnit(5) * MathHelper.TwoPi;
        float prediction = MathHelper.Lerp(0.7f, 2.3f, RandomUnit(6));

        Vector2 targetPoint = target.Center + target.velocity * prediction;
        float distance = Vector2.Distance(Projectile.Center, targetPoint);
        float baseRadius = Math.Max(0f, FranBalance.GetFloat(
            nameof(FranBalance.BlueResonanceEnemySpiralRadius), FranBalance.BlueResonanceEnemySpiralRadius));
        float angularSpeed = Math.Max(0.01f, FranBalance.GetFloat(
            nameof(FranBalance.BlueResonanceEnemySpiralAngularSpeed),
            FranBalance.BlueResonanceEnemySpiralAngularSpeed));
        int contractionFrames = Math.Max(1, FranBalance.GetInt(
            nameof(FranBalance.BlueResonanceEnemySpiralContractionFrames),
            FranBalance.BlueResonanceEnemySpiralContractionFrames));

        float contraction = MathHelper.Clamp(homingAge / contractionFrames, 0f, 1f);
        contraction = contraction * contraction * (3f - 2f * contraction);
        float spiralRadius = Math.Min(baseRadius * radiusVariation,
            distance * MathHelper.Lerp(0.24f, 0.46f, RandomUnit(7)));
        spiralRadius *= MathHelper.Lerp(1f, 0.05f, contraction);
        spiralRadius *= Utils.GetLerpValue(12f, 72f, distance, true);
        float angle = phase + handedness * angularSpeed * angularVariation * homingAge;
        Vector2 spiralDestination = targetPoint + angle.ToRotationVector2() * spiralRadius;
        HomeTowards(spiralDestination, 19f * speedVariation, 0.19f * responseVariation);
    }

    private void AdvanceHoming(int targetKey)
    {
        if (homingTargetKey != targetKey)
        {
            homingTargetKey = targetKey;
            homingAge = 0f;
            return;
        }
        homingAge++;
    }

    private void ResetHoming()
    {
        homingTargetKey = int.MinValue;
        homingAge = 0f;
    }

    // Stable per-projectile variation gives every member of a large group its own path without
    // consuming Main.rand during AI or making multiplayer clients choose unrelated spirals.
    private float RandomUnit(int channel)
    {
        uint value = unchecked((uint)Projectile.identity * 747796405u +
            (uint)(Projectile.owner + 1) * 2891336453u +
            (uint)((int)Projectile.ai[1] + 1) * 277803737u + (uint)(channel + 1) * 1597334677u);
        value ^= value >> 16;
        value *= 2246822519u;
        value ^= value >> 13;
        value *= 3266489917u;
        value ^= value >> 16;
        return (value & 0x00FFFFFFu) / 16777216f;
    }

    private int FindInjuredPlayerIndex()
    {
        int bestIndex = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player candidate = Main.player[i];
            FranCompanionProjectile companion = Projectile.GetGlobalProjectile<FranCompanionProjectile>();
            if (companion.Linked && !FranCompanion.CanSupport(Main.player[companion.LogicalOwner], candidate))
                continue;
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
            FranPlayer fran = target.GetModPlayer<FranPlayer>();
            if (DiscEmission)
                fran.ApplyDiscResonanceHealing(healing);
            else
                fran.ApplyResonanceHealing(healing);
        }
        impactBurstSpawned = true;
        SpawnBloomBurst(target.Center, 0.92f);
        FranResonanceBloomParticle.SpawnBurst(target.Center, Projectile.velocity.ToRotation(), 0.92f,
            includeHealSeal: Main.rand.NextBool(3));
        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.26f, Pitch = 0.55f }, target.Center);
        Projectile.Kill();
    }

    private static void SpawnBloomBurst(Vector2 center, float intensity)
    {
        if (!FranResonanceVisuals.Visible(center))
            return;
        Color cyan = FranResonanceVisuals.Blue;
        FranParticleSystem.Spawn(new FranBloomCirclePulseParticle(center, Vector2.Zero, cyan,
            18f * intensity, 76f * intensity, 18, 0.82f, 0.94f, 0.035f));
        FranParticleEffects.Implosion(center, cyan, 0.34f * intensity);
        FranVfx.SparkBurst(center, Color.Lerp(cyan, Color.White, 0.45f),
            Math.Max(4, (int)(7 * intensity)), 4.6f * intensity, 0.62f * intensity);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = Projectile.Opacity;
        if (opacity <= 0f || !FranResonanceVisuals.Visible(Projectile.Center))
            return false;

        // Fine luminous wake leaves room for the wing silhouette.
        for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
        {
            if (Projectile.oldPos[i] == Vector2.Zero || Projectile.oldPos[i - 1] == Vector2.Zero)
                continue;
            Vector2 old = Projectile.oldPos[i] + Projectile.Size * .5f;
            Vector2 newer = Projectile.oldPos[i - 1] + Projectile.Size * .5f;
            float fade = (1f - i / (float)Projectile.oldPos.Length) * opacity;
            FranResonanceVisuals.Line(old, newer, FranResonanceVisuals.Blue, 2.4f * fade, .30f * fade);
            FranResonanceVisuals.Line(old, newer, FranResonanceVisuals.Cyan, .8f, .44f * fade);
        }
        float phase = RandomUnit(8) * MathHelper.TwoPi;
        float rotation = Projectile.velocity.LengthSquared() > .01f
            ? Projectile.velocity.ToRotation() + MathHelper.PiOver2
            : MathF.Sin(Age * .035f + phase) * .18f;
        float size = .95f + RandomUnit(9) * .16f;
        // Phase affects wingbeats only after the shared opening animation.
        float flutterAge = Age + phase * 4f * Utils.GetLerpValue(7f, 16f, Age, true);
        FranResonanceVisuals.DrawButterfly(Projectile.Center, rotation, flutterAge, size, opacity);
        return false;
    }
}
