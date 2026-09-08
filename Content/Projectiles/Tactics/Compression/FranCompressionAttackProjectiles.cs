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


public sealed class FranPressurizedTerraWave : ModProjectile
{
    private bool Finisher => Projectile.ai[0] > 0.5f;
    private bool airburstsSpawned;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 28;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 500;
    }

    public override void SetDefaults()
    {
        Projectile.width = 46;
        Projectile.height = 46;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 100;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override void AI()
    {
        if (Projectile.localAI[1] == 0f)
        {
            Projectile.localAI[1] = 1f;
            Projectile.localAI[2] = Projectile.identity % 2 == 0 ? 1f : -1f;
            Projectile.scale = Finisher ? 0.82f : 1.34f;
            if (Finisher)
                Projectile.timeLeft = 78;
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * (Finisher ? 20f : 12.5f);
            SoundEngine.PlaySound(SoundID.Item60 with
                { Volume = Finisher ? 0.48f : 0.34f, Pitch = Finisher ? 0.32f : -0.08f }, Projectile.Center);
            FranParticleEffects.LineRelease(Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 26f,
                Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 72f,
                new Color(145, 225, 255), Finisher ? 9 : 13, Finisher ? 0.72f : 1.02f);
            FranSpectatorVfx.SpawnReleaseFan(Projectile.GetSource_FromThis(), Projectile.Center,
                Projectile.velocity, Projectile.owner, FranSpectatorPalette.Cyan, Finisher ? 4 : 2,
                Finisher ? 1.08f : 0.86f);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center,
                Finisher ? 2f : 1.2f);
        }

        float age = Projectile.localAI[0]++;
        Vector2 fallback = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        NPC target = FindTarget(760f);
        Vector2 aimDirection = fallback;
        if (target is not null)
        {
            Vector2 intercept = target.Center + target.velocity * (Finisher ? 3.2f : 5.2f);
            aimDirection = (intercept - Projectile.Center).SafeNormalize(fallback);
        }

        float acceleration = FranMotion.Smooth(FranMotion.Segment(age, 0f, Finisher ? 13f : 20f));
        float lateDrag = FranMotion.Smooth(FranMotion.Segment(age, Finisher ? 48f : 62f,
            Finisher ? 72f : 94f));
        float startSpeed = Finisher ? 20f : 12.5f;
        float peakSpeed = Finisher ? 34f : 25f;
        float speed = MathHelper.Lerp(startSpeed, peakSpeed, acceleration) * MathHelper.Lerp(1f, 0.66f, lateDrag);
        float openingCurve = Projectile.localAI[2] * MathHelper.Lerp(Finisher ? 0.12f : 0.22f, 0f,
            FranMotion.Smooth(FranMotion.Segment(age, 0f, Finisher ? 20f : 34f)));
        float weave = MathF.Sin(age * (Finisher ? 0.27f : 0.20f) + Projectile.identity * 0.71f) *
            (Finisher ? 0.045f : 0.085f);
        Vector2 desired = aimDirection.RotatedBy(openingCurve + weave) * speed;
        float steering = target is null ? 0.055f : MathHelper.Lerp(0.075f, 0.16f, acceleration);
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, steering);
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.Opacity = MathHelper.Clamp(Projectile.timeLeft / 15f, 0f, 1f);
        Lighting.AddLight(Projectile.Center, 0.15f, 0.52f, 0.62f);
        if (!Main.dedServ && Projectile.timeLeft % 3 == 0)
            FranParticleSystem.Spawn(new FranBladeWakeSliverParticle(Projectile.Center,
                -Projectile.velocity * 0.04f + Main.rand.NextVector2Circular(1.2f, 1.2f),
                Projectile.velocity.SafeNormalize(Vector2.UnitX), new Color(125, 235, 255),
                Main.rand.NextFloat(12f, 22f), Main.rand.NextFloat(2f, 4.5f), 14,
                Main.rand.NextFloat(-0.04f, 0.04f)));

        float detonationAge = Finisher ? 30f : 42f;
        if (age >= detonationAge && Main.myPlayer == Projectile.owner)
        {
            Vector2 airburstCenter = target?.Center + target.velocity * 1.5f ??
                Projectile.Center + Projectile.velocity * 1.8f;
            SpawnAirburstSequence(airburstCenter);
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float collisionPoint = 0f;
        float length = (Finisher ? 78f : 112f) * Projectile.scale;
        float width = (Finisher ? 32f : 45f) * Projectile.scale;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - direction * 22f * Projectile.scale,
            Projectile.Center + direction * length, width, ref collisionPoint);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner,
            FranBurstStyle.AirDraw, Finisher ? 0.52f : 0.68f);
        if (Main.myPlayer == Projectile.owner)
            SpawnAirburstSequence(target.Center + target.velocity * 1.5f);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (Main.myPlayer == Projectile.owner)
            SpawnAirburstSequence(Projectile.Center + oldVelocity.SafeNormalize(Vector2.UnitX) * 24f);
        return true;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float pulse = 0.94f + MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity) * 0.06f;
        float trailWidth = (Finisher ? 25f : 42f) * Projectile.scale;

        if (!FranProjectileTrailRenderer.TryDraw(Projectile, FranProjectileTrailProfile.TelekineticNeedle,
                new Color(8, 44, 66), new Color(65, 205, 255), Color.White, trailWidth,
                Projectile.Opacity * (Finisher ? 0.90f : 0.96f)))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.TelekineticNeedle,
                new Color(75, 210, 255), trailWidth * 1.35f, Projectile.Opacity);

        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 bodySize = new(Finisher ? 88f : 122f, Finisher ? 38f : 54f);
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center - direction * 5f,
            new Color(120, 225, 250) * (0.82f * Projectile.Opacity), Projectile.rotation,
            bodySize * Projectile.scale * pulse);
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center - direction * 8f,
            new Color(65, 195, 255) * (0.38f * Projectile.Opacity), Projectile.rotation,
            bodySize * Projectile.scale * 1.16f);
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 44f,
            new Color(125, 230, 255) * (0.46f * Projectile.Opacity), Projectile.rotation,
            new Vector2(Finisher ? 126f : 174f, Finisher ? 30f : 48f) * Projectile.scale);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center + direction * 27f,
            Color.White * (0.66f * Projectile.Opacity), Projectile.rotation,
            new Vector2(Finisher ? 44f : 60f, Finisher ? 24f : 34f) * Projectile.scale);
        FranVfx.DrawBloom(Projectile.Center, new Color(95, 225, 255), 66f * Projectile.scale,
            0.38f * Projectile.Opacity, !Finisher);
        FranVfx.EndAdditive();
        return false;
    }

    private void SpawnAirburstSequence(Vector2 center)
    {
        if (airburstsSpawned)
            return;
        airburstsSpawned = true;

        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
        int count = Finisher ? 5 : 3;
        float centerIndex = (count - 1) * 0.5f;
        float spacing = Finisher ? 38f : 54f;
        int burstDamage = Math.Max(1, (int)(Projectile.damage * (Finisher ? 0.28f : 0.42f)));
        for (int i = 0; i < count; i++)
        {
            float lane = i - centerIndex;
            float normalizedLane = centerIndex <= 0f ? 0f : MathF.Abs(lane) / centerIndex;
            float focus = 1f - normalizedLane;
            float waveOffset = MathF.Sin(Projectile.identity * 0.73f + i * 1.91f) * (Finisher ? 7f : 10f);
            Vector2 position = center + normal * (lane * spacing + waveOffset) +
                forward * (focus * (Finisher ? 28f : 36f) - normalizedLane * 14f);
            float scale = Finisher ? 0.74f + focus * 0.10f : 0.92f + focus * 0.12f;
            float delay = 6f + MathF.Abs(lane) * 3f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, forward,
                ModContent.ProjectileType<FranCompressionAirburst>(), burstDamage,
                Projectile.knockBack * 0.55f, Projectile.owner, delay, scale,
                Projectile.identity * 0.73f + i * 1.17f);
        }

        Projectile.netUpdate = true;
        Projectile.Kill();
    }

    private NPC FindTarget(float distance)
    {
        NPC best = null;
        float bestScore = distance * distance;
        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy())
                continue;
            Vector2 offset = npc.Center - Projectile.Center;
            float candidateDistance = offset.LengthSquared();
            if (candidateDistance >= distance * distance)
                continue;
            float alignment = Vector2.Dot(forward, offset.SafeNormalize(forward));
            if (alignment < -0.18f)
                continue;
            float score = candidateDistance * MathHelper.Lerp(1.45f, 0.72f,
                MathHelper.Clamp((alignment + 1f) * 0.5f, 0f, 1f));
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = npc;
        }
        return best;
    }
}

public sealed class FranCompressionAirburst : ModProjectile
{
    private const int BaseLifetime = 40;
    private const float CollisionDiameter = 7f * 16f;
    private const float CollisionRadius = CollisionDiameter * 0.5f;
    private const float FullLoadoutLifetimeFlag = 100f;
    private const float FullDiscVisualFlag = 200f;
    private bool FullDiscVisual => Projectile.ai[0] >= FullDiscVisualFlag;
    private bool FullLoadoutLifetime => Projectile.ai[0] >= FullLoadoutLifetimeFlag;
    private int Lifetime => FullLoadoutLifetime
        ? Math.Max(BaseLifetime, (int)Math.Round(BaseLifetime * FranBalance.GetFloat(
            nameof(FranBalance.QuickdrawFullLoadoutLifetimeMultiplier),
            FranBalance.QuickdrawFullLoadoutLifetimeMultiplier), MidpointRounding.AwayFromZero))
        : BaseLifetime;
    private float Age => Lifetime - Projectile.timeLeft;
    private float Delay => MathHelper.Clamp(Projectile.ai[0] -
        (FullDiscVisual ? FullDiscVisualFlag : FullLoadoutLifetime ? FullLoadoutLifetimeFlag : 0f), 4f, 16f);
    private float BurstScale => MathHelper.Clamp(Projectile.ai[1], 0.55f, 1.25f);
    private float BlastRadius => 108f * BurstScale;
    private float VisualOpacity => FullDiscVisual
        ? MathHelper.Clamp(FranBalance.GetFloat(
            nameof(FranBalance.QuickdrawFullDiscAirburstOpacityMultiplier),
            FranBalance.QuickdrawFullDiscAirburstOpacityMultiplier), 0f, 1f)
        : 1f;

    internal static float EncodeDelay(float delay, bool fullLoadout, bool fullDiscVisual = false) =>
        MathHelper.Clamp(delay, 4f, 16f) +
        (fullDiscVisual ? FullDiscVisualFlag : fullLoadout ? FullLoadoutLifetimeFlag : 0f);

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;

    public override void SetDefaults()
    {
        Projectile.width = (int)CollisionDiameter;
        Projectile.height = (int)CollisionDiameter;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = BaseLifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override void OnSpawn(Terraria.DataStructures.IEntitySource source) =>
        Projectile.timeLeft = Lifetime;

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Age >= Delay && Age <= Delay + 6f ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Vector2 closest = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
        return Vector2.DistanceSquared(Projectile.Center, closest) <= CollisionRadius * CollisionRadius;
    }

    public override void AI()
    {
        Projectile.rotation += 0.075f + BurstScale * 0.015f;
        if (Age < Delay)
        {
            if (!Main.dedServ && (int)Age % 2 == 0)
                FranParticleEffects.InwardSpark(Projectile.Center,
                    new Color(105, 225, 255) * VisualOpacity,
                    46f * BurstScale, 92f * BurstScale, 5.5f,
                    0.76f * BurstScale * VisualOpacity);
            Lighting.AddLight(Projectile.Center,
                new Vector3(0.08f, 0.30f, 0.42f) * VisualOpacity);
            return;
        }

        if (Projectile.localAI[0] == 0f)
        {
            Projectile.localAI[0] = 1f;
            SpawnBurstEffects();
        }

        float fade = 1f - FranMotion.Smooth(FranMotion.Segment(Age, Delay + 7f, Lifetime));
        Lighting.AddLight(Projectile.Center,
            new Vector3(0.20f, 0.62f, 0.78f) * (fade * VisualOpacity));
    }

    private void SpawnBurstEffects()
    {
        float opacity = VisualOpacity;
        SoundEngine.PlaySound(SoundID.Item14 with
            { Volume = 0.24f * opacity, Pitch = 0.22f + BurstScale * 0.12f,
                PitchVariance = 0.08f, MaxInstances = FullDiscVisual ? 2 : 4 },
            Projectile.Center);
        if (!FullDiscVisual)
            FranParticleEffects.Implosion(Projectile.Center, new Color(90, 215, 255), 0.52f * BurstScale);
        FranParticleEffects.CompressionSignature(Projectile.Center,
            Projectile.ai[2].ToRotationVector2(), BurstScale, opacity);
        FranVanillaDustEffects.Impact(Projectile.Center, Projectile.ai[2].ToRotationVector2(),
            FranSkill.PressurizedQuickdraw, new Color(90, 215, 255) * opacity,
            FullDiscVisual ? 8 : 20, 0.82f * BurstScale);
        if (!Main.dedServ)
        {
            if (!FullDiscVisual)
            {
                int rayCount = BurstScale >= 0.9f ? 18 : 14;
                float baseAngle = Projectile.ai[2];
                for (int i = 0; i < rayCount; i++)
                {
                    Vector2 direction = (baseAngle + MathHelper.TwoPi * i / rayCount +
                        Main.rand.NextFloat(-0.08f, 0.08f)).ToRotationVector2();
                    Color color = i % 4 == 0 ? Color.White : new Color(80, 205, 255);
                    FranParticleSystem.Spawn(new FranLineParticle(Projectile.Center + direction * 5f,
                        direction * Main.rand.NextFloat(4.5f, 11f) * BurstScale, color,
                        Main.rand.NextFloat(38f, 78f) * BurstScale,
                        Main.rand.NextFloat(1.6f, 3.4f) * BurstScale, Main.rand.Next(12, 22), 0.90f));
                    if (i % 3 == 0)
                        FranParticleSystem.Spawn(new FranBoltParticle(Projectile.Center + direction * 12f,
                            direction * Main.rand.NextFloat(2.5f, 6f), new Color(105, 230, 255),
                            Main.rand.NextFloat(0.16f, 0.28f) * BurstScale,
                            new Vector2(0.48f, 1.45f), Main.rand.Next(10, 17)));
                }
            }
            for (int i = 0; i < (FullDiscVisual ? 2 : 5); i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(2.8f, 2.8f) - Vector2.UnitY * 0.6f;
                FranParticleSystem.Spawn(new FranHeavySmokeParticle(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 18f), velocity,
                    new Color(22, 60, 78), Main.rand.NextFloat(0.32f, 0.58f) * BurstScale * opacity,
                    Main.rand.Next(20, 32), 0.32f, true));
            }
        }
        if (!FullDiscVisual)
        {
            FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_FromThis(), Projectile.Center,
                Projectile.ai[2].ToRotationVector2(), Projectile.owner, FranSpectatorPalette.Cyan,
                BurstScale >= 0.9f ? 5 : 3, 0.72f + BurstScale * 0.34f);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>()
                .ShakeScreen(Projectile.Center, 2.6f * BurstScale);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = VisualOpacity;
        Color cyan = new Color(75, 205, 255) * opacity;
        Color pale = new Color(175, 245, 255) * opacity;
        float seed = Projectile.ai[2];
        if (Age < Delay)
        {
            float charge = FranMotion.Smooth(FranMotion.Segment(Age, 0f, Delay));
            float radius = MathHelper.Lerp(82f, 22f, charge) * BurstScale;
            FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
                new Color(95, 205, 235) * (opacity * (0.42f + charge * 0.34f)), Projectile.rotation * 0.55f,
                Vector2.One * MathHelper.Lerp(90f, 52f, charge) * BurstScale);
            FranVfx.BeginAdditive();
            FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
                pale * (0.18f + charge * 0.46f), -Projectile.rotation * 1.35f,
                Vector2.One * (radius * 1.55f));
            FranVfx.DrawSharedPixels("Cal_HighResFoggyCircleHardEdge", Projectile.Center,
                cyan * (0.10f + charge * 0.20f), Projectile.rotation,
                Vector2.One * radius * 1.42f);
            for (int i = 0; i < 6; i++)
            {
                float angle = seed + i * MathHelper.TwoPi / 6f + Projectile.rotation * 0.6f;
                Vector2 direction = angle.ToRotationVector2();
                Vector2 position = Projectile.Center + direction * radius * 0.62f;
                FranVfx.DrawSharedPixels("BloomLineSoftEdge", position, pale * (0.18f + charge * 0.30f),
                    direction.ToRotation() + MathHelper.PiOver2,
                    new Vector2(8f, 34f + charge * 24f) * BurstScale);
            }
            FranVfx.EndAdditive();
            return false;
        }

        float expansion = FranMotion.EaseOutCubic(FranMotion.Segment(Age, Delay, Delay + 14f));
        float fade = 1f - FranMotion.Smooth(FranMotion.Segment(Age, Delay + 8f, Lifetime));
        float shellRadius = MathHelper.Lerp(18f, BlastRadius * 1.18f, expansion);
        float coreFlash = 1f - FranMotion.Smooth(FranMotion.Segment(Age, Delay, Delay + 7f));
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            pale * (0.82f * coreFlash + 0.34f * fade), seed,
            new Vector2(MathHelper.Lerp(88f, BlastRadius * 2.05f, expansion),
                MathHelper.Lerp(58f, BlastRadius * 1.18f, expansion)));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * (opacity * 0.94f * coreFlash), seed, new Vector2(136f, 74f) * BurstScale);
        FranVfx.DrawSharedPixels("Cal_HighResFoggyCircleHardEdge", Projectile.Center,
            cyan * (0.46f * fade), Projectile.rotation, Vector2.One * shellRadius * 2f);
        FranVfx.DrawRing(Projectile.Center, pale, shellRadius * 0.88f,
            0.72f * fade, Projectile.rotation * 1.7f, "Cal_BloomRing");
        FranVfx.DrawRing(Projectile.Center, Color.White, shellRadius * 0.52f,
            opacity * 0.52f * fade, -Projectile.rotation * 2.1f, "Cal_FadedStarRing");
        for (int i = 0; i < 8; i++)
        {
            float angle = seed + i * MathHelper.TwoPi / 8f;
            Vector2 direction = angle.ToRotationVector2();
            float rayLength = MathHelper.Lerp(32f, 104f * BurstScale, expansion) *
                (i % 2 == 0 ? 1f : 0.72f);
            Vector2 position = Projectile.Center + direction * rayLength * 0.42f;
            FranVfx.DrawSharedPixels("BloomLineSoftEdge", position,
                (i % 3 == 0 ? Color.White * opacity : pale) * (0.52f * fade),
                direction.ToRotation() + MathHelper.PiOver2,
                new Vector2(10f + coreFlash * 5f, rayLength));
        }
        FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center,
            Color.White * (opacity * 0.82f * coreFlash),
            seed, new Vector2(0.22f, 0.055f) * BurstScale);
        FranVfx.DrawBloom(Projectile.Center, pale, 38f + coreFlash * 34f,
            opacity * (0.38f * fade + coreFlash * 0.38f), true);
        FranVfx.EndAdditive();
        return false;
    }
}
