using System;
using System.Collections.Generic;
using System.IO;
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


public sealed class FranTelekineticShell : ModProjectile
{
    private const float FullLoadoutHomingFlag = 100f;
    private ref float Age => ref Projectile.localAI[0];
    private ref float DissipationAge => ref Projectile.localAI[1];
    private ref float DeathEffectReleased => ref Projectile.localAI[2];
    private bool FullLoadoutHoming => Projectile.ai[2] >= FullLoadoutHomingFlag;
    private float ReleaseDelay => MathHelper.Clamp(Projectile.ai[2] -
        (FullLoadoutHoming ? FullLoadoutHomingFlag : 0f), 0f, 12f);
    private bool Dissipating => DissipationAge > 0f;
    private bool Heavy => Projectile.scale >= 1.15f;
    private FranTelekineticSkirmishMotion skirmishMotion;
    private FranTelekineticSkirmishMotion SkirmishMotion => skirmishMotion ??= new();
    private int lockedTargetIndex = -1;
    private int lockedTargetType;
    private bool HasMotionAuthority => Main.netMode == NetmodeID.SinglePlayer || Projectile.owner == Main.myPlayer;

    internal void ConfigureFormation(int lane)
    {
        SkirmishMotion.ConfigureLane(lane);
        // 给一次脱靶回勾留出时间；无目标、二次脱靶或命中都会提前消散。
        Projectile.timeLeft = FranTelekineticSkirmishMotion.MaximumLifetime;
    }

    internal static float EncodeReleaseDelay(float releaseDelay, bool fullLoadoutHoming) =>
        MathHelper.Clamp(releaseDelay, 0f, 12f) + (fullLoadoutHoming ? FullLoadoutHomingFlag : 0f);

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 20;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 26; Projectile.height = 26; Projectile.friendly = true;
        // penetrate = 2 是为了留出命中后的无伤害消散阶段；
        // 第一次命中依然会立即主动关闭伤害，不会真正伤害第二个目标。
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = 2; Projectile.timeLeft = 120;
        Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }

    public override bool? CanDamage() => Age > ReleaseDelay && !Dissipating ? null : false;

    public override void AI()
    {
        float age = Age++;
        if (FullLoadoutHoming && HasMotionAuthority && Projectile.timeLeft <= 12 && !Dissipating)
            BeginDissipation();
        if (Dissipating)
        {
            UpdateDissipation();
            return;
        }

        Vector2 direction = new Vector2(Projectile.ai[0], Projectile.ai[1]).SafeNormalize(
            Projectile.velocity.SafeNormalize(Vector2.UnitX));
        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y))
        {
            Projectile.Kill();
            return;
        }

        if (age < ReleaseDelay)
        {
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = direction.ToRotation();
            Projectile.Opacity = MathHelper.Lerp(0.25f, 0.82f,
                FranMotion.Smooth(FranMotion.Segment(age, 0f, Math.Max(1f, ReleaseDelay))));
            Lighting.AddLight(Projectile.Center, 0.06f, 0.18f, 0.30f);
            return;
        }

        float flightAge = age - ReleaseDelay;
        float initialSpeedMultiplier = FranBalance.GetFloat(
            nameof(FranBalance.TelekineticPressureCoreInitialSpeedMultiplier),
            FranBalance.TelekineticPressureCoreInitialSpeedMultiplier);
        float acceleration = FranBalance.GetFloat(
            nameof(FranBalance.TelekineticPressureCoreAcceleration),
            FranBalance.TelekineticPressureCoreAcceleration);
        int accelerationFrames = Math.Max(0, FranBalance.GetInt(
            nameof(FranBalance.TelekineticPressureCoreAccelerationFrames),
            FranBalance.TelekineticPressureCoreAccelerationFrames));
        // 念压核不使用 extraUpdates；前二十个正常帧加速，随后保持最高速度。
        float initialSpeed = (Heavy ? 8.4f : 7.2f) * initialSpeedMultiplier;
        float speed = initialSpeed;
        if (flightAge > 1f && Projectile.velocity.LengthSquared() > 1f)
        {
            speed = Projectile.velocity.Length();
            if (flightAge <= accelerationFrames)
                speed *= acceleration;
        }
        if (FullLoadoutHoming)
        {
            float nominalSpeed = initialSpeed * MathF.Pow(acceleration,
                MathHelper.Clamp(flightAge - 1f, 0f, Math.Max(0, accelerationFrames - 1)));
            UpdateSkirmishMotion(direction, nominalSpeed);
            if (Dissipating)
                return;
        }
        else
        {
            Projectile.velocity = direction * speed;
        }
        Projectile.rotation = Projectile.velocity.SafeNormalize(direction).ToRotation();
        Projectile.Opacity = 1f;
        Lighting.AddLight(Projectile.Center, 0.10f, 0.36f, 0.58f);
        FranVanillaDustEffects.ProjectileTrail(Projectile.Center, Projectile.velocity,
            FranSkill.TelekineticCatapult, new Color(105, 205, 245), 0.12f, 0.72f);
        if (!Main.dedServ && Projectile.timeLeft % 3 == 0)
        {
            FranParticleSystem.Spawn(new FranSkillGlyphParticle(Projectile.Center,
                -Projectile.velocity * 0.042f + Main.rand.NextVector2Circular(0.55f, 0.55f),
                new Color(105, 215, 255), Heavy ? 0.54f : 0.42f, 14));
        }
    }

    public override void OnKill(int timeLeft)
    {
        ReleaseDeathEffect();
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (Dissipating)
            return;
        Color color = new(115, 225, 255);
        FranParticleEffects.SpawnImpact(target.Center, Projectile.velocity, color, Heavy ? 12 : 8,
            Heavy ? 8.2f : 6.2f, Heavy ? 1.02f : 0.78f);
        FranParticleEffects.NeedleFan(target.Center, Projectile.velocity, color, Heavy ? 7 : 5,
            6.6f, Heavy ? 0.90f : 0.66f, 0.58f);
        FranParticleSystem.Spawn(new FranDirectionalRingParticle(target.Center,
            Projectile.velocity.SafeNormalize(Vector2.UnitX) * 0.45f, color,
            new Vector2(0.06f, 0.12f), new Vector2(Heavy ? 0.82f : 0.58f, Heavy ? 0.32f : 0.24f),
            Projectile.velocity.ToRotation(), Heavy ? 16 : 13));
        SoundEngine.PlaySound(SoundID.Item91 with { Volume = Heavy ? 0.30f : 0.18f, Pitch = 0.42f }, target.Center);
        BeginDissipation();
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float pulse = 0.88f + MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity) * 0.12f;
        Vector2 launchDirection = new Vector2(Projectile.ai[0], Projectile.ai[1]).SafeNormalize(Vector2.UnitX);
        Vector2 direction = Projectile.velocity.SafeNormalize(launchDirection);
        float bodyRotation = direction.ToRotation();
        // 沿用原念压核素材：减速横掠时略收拢，切入加速时拉长，不再始终保持同一种针形。
        float stretch = FullLoadoutHoming ? MathHelper.Lerp(0.78f, 1.18f,
            MathHelper.Clamp((Projectile.velocity.Length() - 5f) / 21f, 0f, 1f)) : 1f;
        Vector2 bodyScale = new Vector2(stretch, 1f / MathF.Sqrt(stretch)) * Projectile.scale * pulse;

        if (Age <= ReleaseDelay)
        {
            float charge = FranMotion.Smooth(FranMotion.Segment(Age, 0f, Math.Max(1f, ReleaseDelay)));
            FranVfx.BeginAdditive();
            for (int i = 0; i < 3; i++)
            {
                float angle = Main.GlobalTimeWrappedHourly * (Heavy ? 4.2f : 5.4f) +
                    i * MathHelper.TwoPi / 3f + Projectile.identity * 0.31f;
                Vector2 offset = angle.ToRotationVector2() * MathHelper.Lerp(15f, 6f, charge) * Projectile.scale;
                FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center + offset,
                    new Color(80, 175, 225) * (0.48f + charge * 0.32f), angle + MathHelper.PiOver2,
                    new Vector2(20f, 8f) * Projectile.scale);
            }
            FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
                new Color(120, 225, 255) * (0.42f + charge * 0.38f), -bodyRotation,
                Vector2.One * (Heavy ? 28f : 21f) * Projectile.scale * pulse);
            FranVfx.DrawBloom(Projectile.Center, new Color(80, 195, 255),
                (Heavy ? 48f : 34f) * Projectile.scale, 0.34f + charge * 0.18f, true);
            FranVfx.EndAdditive();
            return false;
        }

        if (!FranProjectileTrailRenderer.TryDraw(Projectile, FranProjectileTrailProfile.TelekineticNeedle,
                new Color(8, 35, 62), new Color(55, 178, 235), Color.White,
                (Heavy ? 21f : 14f) * Projectile.scale, 0.90f * Projectile.Opacity))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.TelekineticNeedle,
                new Color(65, 185, 255), (Heavy ? 25f : 18f) * Projectile.scale, 0.82f * Projectile.Opacity);

        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center,
            new Color(75, 155, 205) * (0.92f * Projectile.Opacity), bodyRotation,
            new Vector2(Heavy ? 88f : 54f, Heavy ? 42f : 30f) * bodyScale);
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center - direction * 3f,
            new Color(75, 195, 255) * (0.46f * Projectile.Opacity), bodyRotation,
            new Vector2(Heavy ? 98f : 62f, Heavy ? 48f : 35f) * bodyScale);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center + direction * (28f * stretch),
            Color.White * (0.68f * Projectile.Opacity), bodyRotation,
            new Vector2(Heavy ? 38f : 28f, Heavy ? 22f : 16f) * bodyScale);
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * (29f * stretch),
            new Color(80, 205, 255) * (0.30f * Projectile.Opacity), bodyRotation,
            new Vector2((Heavy ? 105f : 72f) * stretch, Heavy ? 28f : 20f) * Projectile.scale);
        FranVfx.EndAdditive();
        return false;
    }

    private void UpdateSkirmishMotion(Vector2 launchDirection, float nominalSpeed)
    {
        TelekineticSkirmishPhase previousPhase = SkirmishMotion.Phase;
        NPC target = ResolveSkirmishTarget();
        if (target is null && lockedTargetIndex >= 0 && HasMotionAuthority)
        {
            lockedTargetIndex = -1;
            SkirmishMotion.LoseTarget();
            Projectile.netUpdate = true;
        }
        if (target is null && SkirmishMotion.WantsTarget && HasMotionAuthority && (int)Age % 6 == 0)
        {
            float range = Math.Max(1f, FranBalance.GetFloat(
                nameof(FranBalance.TelekineticPressureCoreHomingRange), FranBalance.TelekineticPressureCoreHomingRange));
            target = FindNearestHomingTarget(range);
            if (target is not null)
            {
                lockedTargetIndex = target.whoAmI;
                lockedTargetType = target.netID;
                Projectile.netUpdate = true;
            }
        }

        TelekineticSkirmishTarget? focus = target is null ? null : new(target.Center, target.velocity,
            Math.Max(target.width, target.height) * 0.5f);
        int scatterFrames = FranBalance.GetInt(nameof(FranBalance.TelekineticPressureCoreHomingDelayFrames),
            FranBalance.TelekineticPressureCoreHomingDelayFrames);
        Projectile.velocity = SkirmishMotion.Step(Projectile.Center, Projectile.velocity,
            launchDirection, nominalSpeed, scatterFrames, focus);
        // 非持有者只预测轨迹；不能因为暂未收到目标同步而提前关闭伤害。
        if (SkirmishMotion.Finished && HasMotionAuthority)
            BeginDissipation();
        if (HasMotionAuthority && (previousPhase != SkirmishMotion.Phase || (int)Age % 15 == 0))
            Projectile.netUpdate = true;
    }

    private NPC ResolveSkirmishTarget() => Main.npc.IndexInRange(lockedTargetIndex) &&
        Main.npc[lockedTargetIndex].netID == lockedTargetType && Main.npc[lockedTargetIndex].CanBeChasedBy(Projectile)
        ? Main.npc[lockedTargetIndex] : null;

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Age);
        writer.Write(DissipationAge);
        writer.Write((short)lockedTargetIndex);
        writer.Write(lockedTargetType);
        writer.Write(Projectile.scale);
        writer.Write((short)Projectile.timeLeft);
        SkirmishMotion.Write(writer);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Age = reader.ReadSingle();
        // 已经命中/消散的状态不可被稍早的飞行同步包回滚。
        DissipationAge = Math.Max(DissipationAge, reader.ReadSingle());
        lockedTargetIndex = reader.ReadInt16();
        lockedTargetType = reader.ReadInt32();
        Projectile.scale = MathHelper.Clamp(reader.ReadSingle(), 0.1f, 4f);
        Projectile.timeLeft = Math.Clamp((int)reader.ReadInt16(), 1, FranTelekineticSkirmishMotion.MaximumLifetime);
        int hitboxSize = Math.Max(18, (int)MathF.Round(26f * Projectile.scale));
        Projectile.Resize(hitboxSize, hitboxSize);
        SkirmishMotion.Read(reader);
        if (Dissipating)
        {
            Projectile.friendly = false;
            Projectile.damage = 0;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 12);
            ReleaseDeathEffect();
        }
    }

    private NPC FindNearestHomingTarget(float maximumDistance)
    {
        NPC closestTarget = null;
        float closestDistance = maximumDistance;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy(Projectile))
                continue;
            float distance = Projectile.Distance(npc.Center);
            if (distance >= closestDistance)
                continue;
            closestDistance = distance;
            closestTarget = npc;
        }
        return closestTarget;
    }

    private void BeginDissipation()
    {
        if (Dissipating)
            return;
        ReleaseDeathEffect();
        DissipationAge = 1f;
        Projectile.friendly = false;
        Projectile.damage = 0;
        Projectile.velocity *= FullLoadoutHoming ? 0.72f : 0.30f;
        Projectile.timeLeft = Math.Min(Projectile.timeLeft, 12);
        Projectile.netUpdate = true;
    }

    private void UpdateDissipation()
    {
        DissipationAge++;
        Projectile.velocity *= FullLoadoutHoming ? 0.90f : 0.66f;
        Projectile.Opacity = MathHelper.Clamp(1f - DissipationAge / 12f, 0f, 1f);
        Projectile.rotation = Projectile.velocity.SafeNormalize(Projectile.rotation.ToRotationVector2()).ToRotation();
        Lighting.AddLight(Projectile.Center, 0.04f * Projectile.Opacity, 0.15f * Projectile.Opacity,
            0.24f * Projectile.Opacity);
        if (DissipationAge >= 12f)
            Projectile.Kill();
    }

    private void ReleaseDeathEffect()
    {
        if (DeathEffectReleased > 0f)
            return;
        DeathEffectReleased = 1f;
        if (Main.dedServ)
            return;

        Vector2 direction = Projectile.velocity.SafeNormalize(Projectile.rotation.ToRotationVector2());
        Color cyan = new(105, 215, 255);
        FranParticleEffects.Implosion(Projectile.Center, cyan, Heavy ? 0.62f : 0.42f);
        FranParticleEffects.BloomPulse(Projectile.Center, new Color(72, 178, 245), 12f,
            Heavy ? 92f : 66f, Heavy ? 15 : 12, Heavy ? 0.82f : 0.62f, true);
        FranParticleEffects.RingPulse(Projectile.Center, new Color(180, 238, 255),
            new Vector2(10f, 22f), new Vector2(Heavy ? 96f : 68f, Heavy ? 24f : 18f),
            Heavy ? 15 : 12, direction.ToRotation(), 0f, Heavy ? 0.82f : 0.64f);
        for (int i = 0; i < (Heavy ? 7 : 5); i++)
        {
            Vector2 velocity = direction.RotatedByRandom(0.72f) * Main.rand.NextFloat(2.2f, 6.2f);
            FranParticleSystem.Spawn(new FranLineParticle(Projectile.Center, velocity,
                Main.rand.NextBool(3) ? Color.White : cyan, Main.rand.NextFloat(16f, 30f),
                Main.rand.NextFloat(1.2f, 2.2f), Main.rand.Next(8, 13), 0.88f));
        }
    }
}

/// <summary>
/// 折念刃：使用泰拉刃式实体波体作为底层，并在其上叠加 Fran 的着色器冲击波。
/// 本体最多造成十次近战伤害，命中后降低速度与后续伤害。
/// </summary>
public sealed class FranTelekineticFoldBlade : ModProjectile
{
    // 保留着色器实现，当前只显示泰拉刃式波体。
    private static readonly bool ShaderWaveEnabled = false;
    private const float ActiveLifetime = 43f;
    private const float DamageEndTime = 34f;
    private ref float Age => ref Projectile.localAI[0];
    private ref float DamagingHitCount => ref Projectile.localAI[1];
    private ref float DissipationAge => ref Projectile.localAI[2];
    private bool Dissipating => DissipationAge > 0f;
    private bool SuppressDeathBurst => Projectile.ai[1] == 1f;
    private float BaseScale => Projectile.ai[2] > 0f ? Projectile.ai[2] : 1f;
    private float SwingSign => Projectile.ai[0] < 0f ? -1f : 1f;
    private int DamagingHitLimit => Math.Max(1, FranBalance.GetInt(
        nameof(FranBalance.TelekineticFoldBladeHitLimit), FranBalance.TelekineticFoldBladeHitLimit));

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.TerraBlade2Shot}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 700;
    }

    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 90;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
        Projectile.ownerHitCheck = true;
        Projectile.ownerHitCheckDistance = 300f;
    }

    public override bool? CanDamage() => !Dissipating && Age < DamageEndTime &&
        DamagingHitCount < DamagingHitLimit ? null : false;

    public override void AI()
    {
        if (Dissipating)
        {
            UpdateDissipation();
            return;
        }

        Age++;
        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Projectile.rotation = forward.ToRotation();
        Projectile.ownerHitCheck = Age <= 6f;

        float contraction = 1f - MathF.Pow(1f - Utils.Remap(
            Age, ActiveLifetime * 0.2f, ActiveLifetime, 0f, 1f), 2f);
        Projectile.scale = MathHelper.Lerp(1.5f, 1f, contraction) * BaseScale;
        Projectile.Opacity = Utils.Remap(Age, 0f, 9f, 0f, 1f) *
            Utils.Remap(Age, ActiveLifetime - 12f, ActiveLifetime, 1f, 0f);

        if (Projectile.velocity.Length() > 8f)
        {
            float deceleration = MathHelper.Clamp(FranBalance.GetFloat(
                nameof(FranBalance.TelekineticFoldBladeFlightDeceleration),
                FranBalance.TelekineticFoldBladeFlightDeceleration), 0.01f, 1f);
            Projectile.velocity *= deceleration;
        }

        Vector2 lightPosition = Projectile.Center + forward * 85f * Projectile.scale;
        Lighting.AddLight(lightPosition, new Vector3(0.12f, 0.58f, 1f) * Projectile.Opacity);

        if (!Main.dedServ)
            FranLightingBoltsSystem.SpawnTelekineticWaveFlightDust(
                Projectile.Center, Projectile.velocity, Projectile.rotation, Projectile.scale,
                Projectile.Opacity);

        if (Age >= DamageEndTime)
        {
            Projectile.friendly = false;
            Projectile.damage = 0;
            Age += 3f;
            Projectile.velocity *= 0.76f;
        }

        if (Age >= ActiveLifetime)
            Projectile.Kill();
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float coneLength = 90f * Projectile.scale;
        const float maximumAngle = MathHelper.PiOver4;
        return targetHitbox.IntersectsConeFastInaccurate(Projectile.Center, coneLength,
            Projectile.rotation, maximumAngle) &&
            Collision.CanHit(Projectile.Center, 0, 0, targetHitbox.Center.ToVector2(), 0, 0);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        DamagingHitCount++;
        float speedRetention = MathHelper.Clamp(FranBalance.GetFloat(
            nameof(FranBalance.TelekineticFoldBladeHitSpeedRetention),
            FranBalance.TelekineticFoldBladeHitSpeedRetention), 0.01f, 1f);
        float damageRetention = MathHelper.Clamp(FranBalance.GetFloat(
            nameof(FranBalance.TelekineticFoldBladeHitDamageRetention),
            FranBalance.TelekineticFoldBladeHitDamageRetention), 0.01f, 1f);
        Projectile.velocity *= speedRetention;
        Projectile.damage = Math.Max(1, (int)(Projectile.damage * damageRetention));
        Projectile.netUpdate = true;

        if (!Main.dedServ)
            FranLightingBoltsSystem.SpawnTelekineticWaveImpact(
                Main.rand.NextVector2FromRectangle(target.Hitbox), Projectile.velocity,
                Projectile.scale);

        if (DamagingHitCount >= DamagingHitLimit)
            BeginDissipation(suppressDeathBurst: false);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Projectile.position += oldVelocity;
        Projectile.velocity = oldVelocity * 0.01f;
        BeginDissipation(suppressDeathBurst: true);
        return false;
    }

    public override void OnKill(int timeLeft)
    {
        if (!SuppressDeathBurst && !Main.dedServ)
            FranLightingBoltsSystem.SpawnTelekineticWaveDissipation(
                Projectile.Center, Projectile.velocity, Projectile.rotation, Projectile.scale);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = MathHelper.Clamp(Projectile.Opacity, 0f, 1f);
        DrawTerraStyleWave(opacity);

        if (ShaderWaveEnabled && !FranProjectileTrailRenderer.TryDrawTelekineticWave(Projectile, opacity))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.TelekineticWave,
                new Color(75, 195, 255), 118f * Projectile.scale, 0.72f * opacity);
        return false;
    }

    private void DrawTerraStyleWave(float opacity)
    {
        Texture2D texture = TextureAssets.Projectile[ProjectileID.TerraBlade2Shot].Value;
        Rectangle source = texture.Frame(1, 4);
        Vector2 origin = source.Size() * 0.5f;
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        float drawProgress = Utils.Remap(Age, 0f, ActiveLifetime + 5f, 0f, 1f);
        float closure = 1f - drawProgress;
        float scale = Projectile.scale;
        SpriteEffects first = SwingSign >= 0f ? SpriteEffects.None : SpriteEffects.FlipVertically;
        SpriteEffects second = first ^ SpriteEffects.FlipVertically;

        float lighting = Lighting.GetColor(Projectile.Center.ToTileCoordinates()).ToVector3().Length() /
            MathF.Sqrt(3f);
        lighting = Utils.Remap(0.5f + lighting * 0.5f, 0.2f, 1f, 0f, 1f);

        Color deepBlue = new(18, 68, 155);
        Color bodyBlue = new(35, 170, 255);
        Color edgeBlue = new(135, 230, 255);
        Color ambient = Color.White * opacity * 0.5f;
        ambient.A = (byte)(ambient.A * (1f - lighting));
        ambient *= lighting * 0.5f;

        float opening = SwingSign * MathHelper.PiOver4 * 0.5f * closure;
        Main.EntitySpriteDraw(texture, drawPosition, source, deepBlue * lighting * opacity,
            Projectile.rotation - opening, origin, scale * 0.95f, first);
        Main.EntitySpriteDraw(texture, drawPosition, source, deepBlue * lighting * opacity,
            Projectile.rotation + opening, origin, scale * 0.95f, second);
        Main.EntitySpriteDraw(texture, drawPosition, source, ambient * 0.15f,
            Projectile.rotation + SwingSign * 0.01f, origin, scale, first);
        Main.EntitySpriteDraw(texture, drawPosition, source, ambient * 0.15f,
            Projectile.rotation - SwingSign * 0.01f, origin, scale, second);
        Main.EntitySpriteDraw(texture, drawPosition, source, bodyBlue * lighting * opacity * 0.30f,
            Projectile.rotation + SwingSign * 0.25f * closure, origin, scale, first);
        Main.EntitySpriteDraw(texture, drawPosition, source, bodyBlue * lighting * opacity * 0.30f,
            Projectile.rotation - SwingSign * 0.25f * closure, origin, scale, second);
        Main.EntitySpriteDraw(texture, drawPosition, source, edgeBlue * lighting * opacity * 0.55f,
            Projectile.rotation + SwingSign * 0.15f * closure, origin, scale * 0.975f, first);

        Rectangle coreFrame = texture.Frame(1, 4, 0, 3);
        Main.EntitySpriteDraw(texture, drawPosition, coreFrame, Color.White * (0.72f * opacity),
            Projectile.rotation + SwingSign * 0.05f * closure, origin, scale, first);
        Main.EntitySpriteDraw(texture, drawPosition, coreFrame, Color.White * (0.58f * opacity),
            Projectile.rotation - SwingSign * 0.05f, origin, scale * 0.8f, first);
        Main.EntitySpriteDraw(texture, drawPosition, coreFrame, Color.White * (0.42f * opacity),
            Projectile.rotation - SwingSign * 0.10f, origin, scale * 0.6f, first);

        DrawWaveSparkles(texture, drawPosition, bodyBlue, drawProgress, opacity, scale);
    }

    private void DrawWaveSparkles(Texture2D waveTexture, Vector2 center, Color color,
        float progress, float opacity, float scale)
    {
        for (float i = -9f; i < 9f; i++)
        {
            float edgeRotation = Projectile.rotation + SwingSign * i * MathHelper.TwoPi * -0.025f;
            Vector2 position = center + edgeRotation.ToRotationVector2() *
                (waveTexture.Width * 0.5f - 6f) * scale;
            float edgeStrength = MathF.Abs(i) / 9f;
            DrawPrettyStarSparkle(opacity, position,
                new Color(255, 255, 255, 0) * (opacity * edgeStrength), color, progress,
                edgeRotation, new Vector2(0f, Utils.Remap(progress, 0f, 1f, 3f, 0f)) * scale,
                Vector2.One * scale);
        }

        for (float side = -1f; side <= 1f; side += 0.5f)
        {
            if (side == 0f)
                continue;
            Vector2 position = center + (Projectile.rotation + side * MathHelper.Pi * 0.75f * progress)
                .ToRotationVector2() * (waveTexture.Width * 0.5f - 4f) * scale;
            float strength = Utils.Remap(MathF.Abs(side), 0f, 1f, 1f, 0.5f);
            Vector2 sparkleScale = new Vector2(Utils.Remap(progress, 0f, 1f, 4f, 1f)) *
                scale * strength;
            DrawPrettyStarSparkle(opacity, position, new Color(255, 255, 255, 0) *
                (opacity * 0.5f), color, progress, MathHelper.PiOver4, sparkleScale,
                Vector2.One * scale * strength);
            DrawPrettyStarSparkle(opacity, position, new Color(255, 255, 255, 0) *
                (opacity * 0.5f), color, progress, 0f,
                new Vector2(2f, Utils.Remap(progress, 0f, 1f, 4f, 1f)) * scale * strength,
                Vector2.One * scale * strength);
        }

        Vector2 tip = center + Projectile.rotation.ToRotationVector2() *
            (waveTexture.Width * 0.5f - 4f) * scale;
        DrawPrettyStarSparkle(opacity, tip, new Color(255, 255, 255, 0) * (opacity * 0.5f),
            color, progress, MathHelper.PiOver4,
            new Vector2(Utils.Remap(progress, 0f, 1f, 4f, 1f)) * scale,
            Vector2.One * scale * 1.5f);
        DrawPrettyStarSparkle(opacity, tip, new Color(255, 255, 255, 0) * (opacity * 0.5f),
            color, progress, 0f,
            new Vector2(2f, Utils.Remap(progress, 0f, 1f, 4f, 1f)) * scale,
            Vector2.One * scale * 1.5f);
    }

    private static void DrawPrettyStarSparkle(float opacity, Vector2 position, Color drawColor,
        Color shineColor, float progress, float rotation, Vector2 scale, Vector2 fatness)
    {
        Texture2D texture = TextureAssets.Extra[ExtrasID.SharpTears].Value;
        Vector2 origin = texture.Size() * 0.5f;
        float fade = Utils.GetLerpValue(0f, 0.5f, progress, true) *
            Utils.GetLerpValue(1f, 0.5f, progress, true);
        Color largeColor = shineColor * (opacity * 0.5f * fade);
        largeColor.A = 0;
        Color smallColor = drawColor * (0.5f * fade);
        Vector2 horizontal = new Vector2(fatness.X * 0.5f, scale.X) * fade;
        Vector2 vertical = new Vector2(fatness.Y * 0.5f, scale.Y) * fade;

        Main.EntitySpriteDraw(texture, position, null, largeColor, MathHelper.PiOver2 + rotation,
            origin, horizontal, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, position, null, largeColor, rotation,
            origin, vertical, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, position, null, smallColor, MathHelper.PiOver2 + rotation,
            origin, horizontal * 0.6f, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, position, null, smallColor, rotation,
            origin, vertical * 0.6f, SpriteEffects.None);
    }

    private void BeginDissipation(bool suppressDeathBurst)
    {
        if (Dissipating)
            return;
        DissipationAge = 1f;
        Projectile.ai[1] = suppressDeathBurst ? 1f : 2f;
        Projectile.friendly = false;
        Projectile.damage = 0;
        Projectile.timeLeft = Math.Min(Projectile.timeLeft, 12);
        Projectile.netUpdate = true;
    }

    private void UpdateDissipation()
    {
        DissipationAge++;
        Projectile.velocity *= 0.67f;
        Projectile.Opacity = MathHelper.Clamp(1f - DissipationAge / 10f, 0f, 1f);
        Projectile.rotation = Projectile.velocity.SafeNormalize(
            Projectile.rotation.ToRotationVector2()).ToRotation();
        if (DissipationAge >= 10f)
            Projectile.Kill();
    }
}

// 困难模式后才进入状态机的高速蓝色激光。其 15 次子更新保留利落飞行，
// 而命中耗尽后的减速与淡出每个真实帧只推进一次。
public sealed class FranTelekineticHalleyBolt : ModProjectile
{
    private ref float Time => ref Projectile.ai[0];
    private ref float DamagingHitCount => ref Projectile.ai[1];
    private ref float DissipationAge => ref Projectile.ai[2];
    private int DamagingHitLimit => Math.Max(1, (int)Projectile.localAI[0]);
    private bool SuppressLaunchBurst => Projectile.localAI[1] > 0f;
    private bool Dissipating => DissipationAge > 0f;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 32;
        Projectile.height = 32;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 2;
        Projectile.MaxUpdates = 15;
        Projectile.timeLeft = 20 * Projectile.MaxUpdates;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool? CanDamage() => Dissipating ? false : null;

    public override void AI()
    {
        EnsureConfigured();
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (Dissipating)
        {
            UpdateDissipation();
            return;
        }

        Time++;

        bool drawingUpdate = Projectile.numUpdates % 3 == 0;
        if (Time > 6f && drawingUpdate && !Main.dedServ)
        {
            float scaleBoost = MathHelper.Clamp(Time * 0.005f, 0f, 2f);
            FranParticleSystem.Spawn(new FranHalleySparkParticle(Projectile.Center, Projectile.velocity,
                new Color(8, 35, 156), (3.2f + scaleBoost) * Projectile.scale, 7));
            FranParticleSystem.Spawn(new FranHalleySparkParticle(Projectile.Center, Projectile.velocity,
                new Color(184, 215, 245), (1.6f + scaleBoost) * Projectile.scale, 7));
        }
        else if (Time == 5f && !Main.dedServ && !SuppressLaunchBurst)
        {
            FranParticleSystem.Spawn(new FranHalleyPulseRingParticle(Projectile.Center,
                Projectile.velocity * 0.75f, Color.Aqua, new Vector2(1f, 2.5f) * Projectile.scale,
                Projectile.rotation,
                0.20f, 0.03f, 20));
            FranParticleSystem.Spawn(new FranHalleyPulseRingParticle(Projectile.Center,
                Projectile.velocity * 0.40f, Color.DodgerBlue, new Vector2(1f, 2.5f) * Projectile.scale,
                Projectile.rotation,
                0.10f, 0.025f, 35));
            for (int i = 0; i <= 25; i++)
            {
                Dust wide = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? 172 : 206,
                    Projectile.velocity.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.3f, 1.6f));
                wide.scale = Main.rand.NextFloat(1.6f, 2.5f) * Projectile.scale;
                wide.noGravity = true;
                Dust needle = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? 172 : 206,
                    Projectile.velocity.RotatedByRandom(0.06f) * Main.rand.NextFloat(0.8f, 3.1f));
                needle.scale = Main.rand.NextFloat(1.35f, 2.1f) * Projectile.scale;
                needle.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.62f, Pitch = 0.26f }, Projectile.Center);
        }

        if (Projectile.numUpdates == 0)
            Lighting.AddLight(Projectile.Center, Color.MediumBlue.ToVector3() * 0.4f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        EnsureConfigured();
        SoundEngine.PlaySound(SoundID.DD2_CrystalCartImpact with { Volume = 0.64f }, Projectile.Center);
        for (int i = 0; i < 14; i++)
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? 172 : 206,
                Projectile.velocity.RotatedByRandom(0.5f) * Main.rand.NextFloat(0.2f, 2.1f));
            dust.scale = Main.rand.NextFloat(1.1f, 1.9f) * Projectile.scale;
            dust.noGravity = true;
        }

        DamagingHitCount++;
        if (DamagingHitCount >= DamagingHitLimit)
            BeginDissipation();
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = MathHelper.Clamp(Projectile.Opacity, 0f, 1f);
        Vector2 direction = Projectile.velocity.SafeNormalize(Projectile.rotation.ToRotationVector2());
        float rotation = direction.ToRotation();
        float pulse = 0.92f + MathF.Sin(Main.GlobalTimeWrappedHourly * 18f + Projectile.identity) * 0.08f;

        FranVfx.BeginAdditive();
        FranVfx.DrawShared("CL_BloomCircle", Projectile.Center,
            new Color(45, 135, 255, 0) * (0.52f * opacity), rotation,
            0.48f * pulse * Projectile.scale);
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 18f,
            new Color(65, 175, 255) * (0.48f * opacity), rotation,
            new Vector2(104f, 24f) * pulse * Projectile.scale);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center + direction * 12f,
            Color.White * (0.78f * opacity), rotation,
            new Vector2(38f, 20f) * pulse * Projectile.scale);
        FranVfx.EndAdditive();
        return false;
    }

    private void EnsureConfigured()
    {
        if (Projectile.localAI[0] > 0f)
            return;

        Projectile.localAI[0] = FranBalance.GetTelekineticLaserDamagingHits();
        Projectile.localAI[1] = DissipationAge < 0f ? 1f : 0f;
        DissipationAge = 0f;
        Projectile.penetrate = DamagingHitLimit + 1;
        Projectile.Opacity = 1f;
    }

    private void BeginDissipation()
    {
        if (Dissipating)
            return;

        // The extra penetrate point is deliberately non-damaging: it keeps the projectile alive
        // long enough to visibly lose momentum instead of being deleted on its final real hit.
        DissipationAge = 1f;
        Projectile.friendly = false;
        Projectile.damage = 0;
        Projectile.velocity *= 0.42f;
        Projectile.timeLeft = Math.Min(Projectile.timeLeft, 9 * Projectile.MaxUpdates);
        Projectile.netUpdate = true;
    }

    private void UpdateDissipation()
    {
        if (Projectile.numUpdates != 0)
            return;

        DissipationAge++;
        Projectile.velocity *= 0.62f;
        Projectile.Opacity = MathHelper.Clamp(1f - DissipationAge / 9f, 0f, 1f);
        Lighting.AddLight(Projectile.Center, Color.MediumBlue.ToVector3() * (0.24f * Projectile.Opacity));
        if (DissipationAge >= 9f)
            Projectile.Kill();
    }
}
