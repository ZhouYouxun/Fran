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
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


public sealed class Telekinetic_CondensedMindCore : ModProjectile
{
    private bool hasHit;
    private int flightVisualTick;
    internal static readonly Color TrailBlue = new(28, 145, 235);
    internal static readonly Color TrailLightBlue = new(55, 178, 235);
    private bool UpgradedVisual => FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh);
    private float UpgradeVisualScale => Projectile.ai[2] > 0f ? Projectile.ai[2] : 1f;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        ProjectileID.Sets.TrailCacheLength[Type] = FranProjectileTrailRenderer.TelekineticCoreHistoryLength;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 12;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = !FranBalance.IsMilestoneCompleted(FranProgressionMilestone.EyeOfCthulhu);
        Projectile.timeLeft = 135;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
        Projectile.extraUpdates = 1;
    }

    public override void AI()
    {
        // 击败克苏鲁之眼后才解锁念核穿墙；追踪不依赖技能槽是否全选。
        Projectile.tileCollide = !FranBalance.IsMilestoneCompleted(FranProgressionMilestone.EyeOfCthulhu);
        NPC target = ResolveHomingTarget();
        Projectile.extraUpdates = target is null ? 1 : 2;
        if (target is not null && !hasHit)
            Projectile.velocity = FranMindCoreGuidance.Steer(Projectile.velocity,
                target.Center - Projectile.Center, target.velocity, Projectile.MaxUpdates,
                FranMindCoreGuidance.PursuitSpeed * FranBalance.GetTelekineticCoreSpeedMultiplier());
        Projectile.scale = Utils.GetLerpValue(0f, 8f, Projectile.timeLeft, true);
        Projectile.rotation = Projectile.velocity.ToRotation();

        // 粒子按游戏帧发射，追踪时增加子更新不会把视觉密度也提高。
        if (!Main.dedServ && Projectile.numUpdates == 0 && !hasHit)
        {
            EmitCoreFlightParticles();
            if (UpgradedVisual)
                EmitUpgradedFlightParticles();
        }
    }

    private NPC ResolveHomingTarget()
    {
        bool CanTrack(NPC npc) => npc.CanBeChasedBy(Projectile, false) &&
            Projectile.WithinRange(npc.Center, FranMindCoreGuidance.AcquisitionRadius + npc.width / 2 + npc.height / 2);

        // ai[1] 存目标序号+1；保持当前目标，出范围/失效才重新选择，避免怪群之间来回摆尾。
        int index = (int)Projectile.ai[1] - 1;
        if (Main.npc.IndexInRange(index) && CanTrack(Main.npc[index]))
            return Main.npc[index];
        if (Main.myPlayer != Projectile.owner)
            return null;

        NPC closest = null;
        float closestDistance = 25000f;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!CanTrack(npc))
                continue;
            float distance = Projectile.Distance(npc.Center);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = npc;
            }
        }
        int selected = closest is null ? 0 : closest.whoAmI + 1;
        if (Projectile.ai[1] != selected)
        {
            Projectile.ai[1] = selected;
            Projectile.netUpdate = true;
        }
        return closest;
    }

    private void EmitCoreFlightParticles()
    {
        int step = flightVisualTick++;
        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 side = forward.RotatedBy(MathHelper.PiOver2);
        if (step % 2 == 0)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector2 offset = side * sign * (8f + step % 3);
                FranParticleSystem.Spawn(new FranLightParticle(Projectile.Center - forward * 8f + offset,
                    -forward * 1.5f - offset * 0.24f, 0.16f, sign < 0 ? TrailBlue : TrailLightBlue,
                    8, 0.75f, 1.2f, 1.8f), spawnSupportingDust: false);
            }
        }
        if (step % 6 == 0)
            FranParticleSystem.Spawn(new FranDirectionalRingParticle(Projectile.Center - forward * 12f,
                -forward * 1.2f, TrailLightBlue, new Vector2(0.035f, 0.10f),
                new Vector2(0.015f, 0.035f), Projectile.rotation, 9), spawnSupportingDust: false);
    }

    private void EmitUpgradedFlightParticles()
    {
        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Lighting.AddLight(Projectile.Center, TrailLightBlue.ToVector3() * 0.32f * Projectile.scale);
        if (flightVisualTick % 3 == 0)
            FranParticleSystem.Spawn(new FranDirectionalRingParticle(Projectile.Center,
                -forward * 0.5f, TrailLightBlue, new Vector2(0.045f, 0.045f) * UpgradeVisualScale,
                new Vector2(0.016f, 0.016f) * UpgradeVisualScale, Projectile.rotation, 11),
                spawnSupportingDust: false);
        if (flightVisualTick % 9 == 0)
            FranLightingBoltsSystem.SpawnTelekineticEmissionStar(
                Projectile.Center, forward, 0.52f * UpgradeVisualScale);
    }

    public override bool? CanDamage() => hasHit ? false : null;

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        // 命中时实时判断，Boss 死亡或进入困难模式后，已发射的念核也不再享受增伤。
        if (FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh))
            return;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.life <= 0 || !(npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type]))
                continue;
            modifiers.FinalDamage *= Math.Max(0f, FranBalance.GetFloat(
                nameof(FranBalance.TelekineticCorePreHardmodeBossDamageMultiplier),
                FranBalance.TelekineticCorePreHardmodeBossDamageMultiplier));
            return;
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (Projectile.oldPos[0] == Vector2.Zero)
            return false;
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.oldPos[0] + Projectile.Size * 0.5f, Projectile.Center,
            UpgradedVisual ? 5f : 0f, ref collisionPoint);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (hasHit)
            return;
        hasHit = true;
        // 只同步一次 Fran 粒子爆发；后续消散不再生成追随敌人的切割特效。
        if (Main.myPlayer == Projectile.owner)
            Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, Vector2.Zero,
                ModContent.ProjectileType<FranCoreImpactBurst>(), 0, 0f, Projectile.owner,
                Projectile.velocity.ToRotation(), UpgradedVisual ? 1f : 0f);
        Projectile.timeLeft = Math.Min(Projectile.timeLeft, 12);
        Projectile.velocity *= 0.2f;
        Projectile.damage = 0;
        Projectile.netUpdate = true;
    }

    public override void OnKill(int timeLeft)
    {
        // 未命中的核心在寿命结束处释放同一套爆发，命中过的核心避免重复播放。
        if (Main.myPlayer != Projectile.owner || hasHit)
            return;
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
            ModContent.ProjectileType<FranCoreImpactBurst>(), 0, 0f, Projectile.owner,
            Projectile.rotation);
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(hasHit);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        hasHit |= reader.ReadBoolean();
        if (hasHit)
        {
            Projectile.damage = 0;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 12);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (!FranProjectileTrailRenderer.TryDrawTelekineticCore(Projectile,
                TrailBlue, TrailLightBlue, Color.White, 1f))
            FranProjectileTrailRenderer.DrawTelekineticCoreFallback(Projectile, TrailLightBlue);
        FranProjectileTrailRenderer.DrawTelekineticCoreHead(Projectile, TrailBlue, TrailLightBlue);
        if (UpgradedVisual)
            DrawUpgradedCoreHalo();
        return false;
    }

    private void DrawUpgradedCoreHalo()
    {
        float pulse = 0.92f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);
        float size = Projectile.scale * UpgradeVisualScale * pulse;
        Vector2 forward = Projectile.rotation.ToRotationVector2();
        Vector2 side = forward.RotatedBy(MathHelper.PiOver2);
        FranVfx.BeginAdditive();
        try
        {
            FranVfx.DrawSharedPixels("BloomCircle", Projectile.Center,
                TrailLightBlue * (0.42f * Projectile.scale), Projectile.rotation,
                new Vector2(36f, 36f) * size);
            for (int i = 0; i < 4; i++)
            {
                Vector2 ray = (i % 2 == 0 ? forward : side) * (i < 2 ? 1f : -1f);
                FranVfx.DrawVertical("FranVfx_ThinEndedLine", Projectile.Center + ray * 3f * size,
                    Projectile.Center + ray * 18f * size, TrailLightBlue * 0.66f, 2.1f * size);
            }
            FranVfx.DrawSharedPixels("BloomCircle", Projectile.Center,
                Color.White * (0.58f * Projectile.scale), Projectile.rotation,
                new Vector2(15f, 15f) * size);
            if (Projectile.ai[0] == 2f)
            {
                Vector2 tip = Projectile.Center + forward * 11f * size;
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", tip,
                    new Color(65, 190, 255, 0) * (0.78f * Projectile.scale),
                    Projectile.rotation, new Vector2(58f, 39f) * size);
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", tip,
                    new Color(255, 255, 255, 0) * (0.94f * Projectile.scale),
                    Projectile.rotation, new Vector2(38f, 31f) * size);
            }
        }
        finally { FranVfx.EndAdditive(); }
    }
}

/// <summary>
/// 折念刃：使用泰拉刃式实体波体作为底层，并在其上叠加 Fran 的着色器冲击波。
/// 本体最多造成十次近战伤害，命中后降低速度与后续伤害。
/// </summary>
public sealed class Telekinetic_DestructiveWave : ModProjectile
{
    // 保留着色器实现，当前只显示泰拉刃式波体。
    private static readonly bool ShaderWaveEnabled = false;
    private const float VisualScaleMultiplier = 1.30f;
    private const float ActiveLifetime = 43f;
    private const float DamageEndTime = 34f;
    private const float TerrainDissipationLifetime = 16f;
    private const int FirstEmissionFrame = 6;
    private const int EmissionInterval = 7;
    private const int EmissionPulses = 4;
    private bool emitsPattern;
    private int emissionStage;
    private int emissionCombo;
    private int emissionLayers;
    private int emissionFacing;
    private int emissionDamage;
    private ref float Age => ref Projectile.localAI[0];
    private ref float DamagingHitCount => ref Projectile.localAI[1];
    private ref float DissipationAge => ref Projectile.localAI[2];
    private bool Dissipating => Projectile.ai[1] != 0f;
    private bool SuppressDeathBurst => Projectile.ai[1] == 1f;
    private float BaseScale => Projectile.ai[2] > 0f ? Projectile.ai[2] : 1f;
    private float SwingSign => Projectile.ai[0] < 0f ? -1f : 1f;
    private int DamagingHitLimit => Math.Max(1, FranBalance.GetInt(
        nameof(FranBalance.TelekineticFoldBladeHitLimit), FranBalance.TelekineticFoldBladeHitLimit));

    internal void EnablePatternEmission(int stage, int combo, int layers, int facing)
    {
        emitsPattern = true;
        emissionStage = stage;
        emissionCombo = combo;
        emissionLayers = layers;
        emissionFacing = facing;
        emissionDamage = Projectile.damage;
        Projectile.netUpdate = true;
    }

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
        Projectile.tileCollide = !FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh);
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
        Projectile.tileCollide = !FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh);
        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Projectile.rotation = forward.ToRotation();
        Projectile.ownerHitCheck = Age <= 6f;

        float contraction = 1f - MathF.Pow(1f - Utils.Remap(
            Age, ActiveLifetime * 0.2f, ActiveLifetime, 0f, 1f), 2f);
        Projectile.scale = MathHelper.Lerp(1.5f, 1f, contraction) * BaseScale;
        Projectile.Opacity = Utils.Remap(Age, 0f, 9f, 0f, 1f) *
            Utils.Remap(Age, ActiveLifetime - 12f, ActiveLifetime, 1f, 0f);

        // 肉前保留原版遇阻消散；困难模式后的光波穿过方块。
        if (Projectile.tileCollide && IsWaveFrontBlocked())
        {
            BeginDissipation(suppressDeathBurst: true);
            UpdateDissipation();
            return;
        }

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

        if (emitsPattern && Age >= FirstEmissionFrame &&
            ((int)Age - FirstEmissionFrame) % EmissionInterval == 0)
        {
            int pulse = ((int)Age - FirstEmissionFrame) / EmissionInterval;
            if (pulse < EmissionPulses)
                EmitPatternShots(pulse, forward);
        }

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
        // 已到达实体方块：留在接触处收拢闪光，不能把整个波体推进墙内。
        Projectile.velocity = Vector2.Zero;
        BeginDissipation(suppressDeathBurst: true);
        return false;
    }

    private bool IsWaveFrontBlocked()
    {
        float reach = 110f * Projectile.scale;
        for (float side = -1f; side <= 1f; side += 0.5f)
        {
            Vector2 direction = (Projectile.rotation + side * MathHelper.PiOver4 * 0.25f)
                .ToRotationVector2();
            if (Collision.CanHit(Projectile.Center + direction * reach * 0.5f, 0, 0,
                    Projectile.Center + direction * reach, 0, 0))
                return false;
        }
        return true;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Age);
        writer.Write(DamagingHitCount);
        writer.Write(DissipationAge);
        writer.Write(emitsPattern);
        if (emitsPattern)
        {
            writer.Write((byte)emissionStage);
            writer.Write((byte)emissionCombo);
            writer.Write((byte)emissionLayers);
            writer.Write((sbyte)emissionFacing);
            writer.Write(emissionDamage);
        }
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Age = reader.ReadSingle();
        DamagingHitCount = reader.ReadSingle();
        DissipationAge = reader.ReadSingle();
        emitsPattern = reader.ReadBoolean();
        if (emitsPattern)
        {
            emissionStage = reader.ReadByte();
            emissionCombo = reader.ReadByte();
            emissionLayers = reader.ReadByte();
            emissionFacing = reader.ReadSByte();
            emissionDamage = reader.ReadInt32();
        }
        if (Dissipating)
        {
            Projectile.friendly = false;
            Projectile.damage = 0;
        }
    }

    private void EmitPatternShots(int pulse, Vector2 forward)
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active)
            return;
        List<FranTelekineticShot> shots = new(16);
        FranTelekineticPattern.Fill(shots, emissionStage, emissionCombo, emissionLayers, emissionFacing);
        float damageScale = FranTelekineticPattern.GetDamageScale(emissionStage,
            FranBalance.GetAvailableTacticSlots());
        Vector2 side = forward.RotatedBy(MathHelper.PiOver2);
        int index = 0;
        foreach (FranTelekineticShot shot in shots)
        {
            if (shot.Kind == FranTelekineticShotKind.Planet)
                continue;
            if (index++ % EmissionPulses != pulse)
                continue;

            // Judgement 的小星从波刃散开，先沿弧线甩出，再交给凝念核原有追踪。
            float flank = (pulse % 2 == 0 ? -1f : 1f) * (0.58f + pulse / 2 * 0.22f);
            Vector2 release = Projectile.Center + forward * 28f * Projectile.scale +
                side * (flank * 82f * Projectile.scale + shot.Offset * 0.35f);
            if (!Main.dedServ)
                FranLightingBoltsSystem.SpawnTelekineticEmissionStar(release, side * flank, 1.15f);
            if (Main.myPlayer != Projectile.owner)
                continue;
            float multiplier = shot.Weight * damageScale;
            if (shot.Kind == FranTelekineticShotKind.Core)
            {
                Vector2 launch = (-forward * 0.62f + side * flank).SafeNormalize(-forward) * 30f;
                ShiFuImprintCombat.SpawnTelekineticPressureCore(owner, Projectile, forward, 0f,
                    multiplier, release, emissionDamage, shot.Scale, launch, judgementStar: true);
            }
            else
                ShiFuImprintCombat.SpawnTelekineticFoldBlade(owner, Projectile, forward,
                    shot.Offset, shot.Angle, shot.Scale, multiplier, release, emissionDamage);
        }
    }

    public override void OnKill(int timeLeft)
    {
        if (!SuppressDeathBurst && !Main.dedServ)
        {
            SoundEngine.PlaySound(FranCombatSounds.TelekineticWaveDeath, Projectile.Center);
            FranLightingBoltsSystem.SpawnTelekineticWaveDissipation(
                Projectile.Center, Projectile.velocity, Projectile.rotation, Projectile.scale);
        }
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
        if (Dissipating && SuppressDeathBurst)
            drawProgress = MathHelper.Lerp(drawProgress, 1f,
                MathHelper.Clamp(DissipationAge / TerrainDissipationLifetime, 0f, 1f));
        float closure = 1f - drawProgress;
        // 仅放大念动弹射的蓝色波体与附着闪光；命中锥体仍读取原始 Projectile.scale。
        float scale = Projectile.scale * VisualScaleMultiplier;
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

        if (Dissipating && SuppressDeathBurst)
        {
            // 独立于发射淡入和环境亮度的蓝白闪光，贴墙发射也能看清停留的波体。
            float flash = Utils.GetLerpValue(1f, 5f, DissipationAge, true) *
                Utils.GetLerpValue(12f, 5f, DissipationAge, true);
            Color flashColor = new Color(190, 240, 255, 0) * flash;
            Main.EntitySpriteDraw(texture, drawPosition, coreFrame, flashColor,
                Projectile.rotation, origin, scale, first);
            DrawPrettyStarSparkle(flash, drawPosition + Projectile.rotation.ToRotationVector2() *
                70f * Projectile.scale, flashColor, edgeBlue, 0.5f, Projectile.rotation,
                new Vector2(2f, 3f) * scale, Vector2.One * scale);
        }
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
        Projectile.timeLeft = Math.Min(Projectile.timeLeft,
            suppressDeathBurst ? (int)TerrainDissipationLifetime + 2 : 12);
        Projectile.netUpdate = true;
    }

    private void UpdateDissipation()
    {
        DissipationAge++;
        float lifetime = SuppressDeathBurst ? TerrainDissipationLifetime : 10f;
        // 原版遇阻的三段速度保留：0.94 × 0.8 × 0.88，连续刹车而非瞬间降到 1%。
        Projectile.velocity *= SuppressDeathBurst ? 0.94f * 0.8f * 0.88f : 0.67f;
        if (SuppressDeathBurst && Projectile.velocity.LengthSquared() < 0.25f)
            Projectile.velocity = Vector2.Zero;
        Projectile.Opacity = SuppressDeathBurst
            ? Utils.GetLerpValue(lifetime, 5f, DissipationAge, true)
            : MathHelper.Clamp(1f - DissipationAge / lifetime, 0f, 1f);
        Projectile.rotation = Projectile.velocity.SafeNormalize(
            Projectile.rotation.ToRotationVector2()).ToRotation();
        if (SuppressDeathBurst)
            Lighting.AddLight(Projectile.Center, new Vector3(0.22f, 0.68f, 1f) * Projectile.Opacity);
        if (DissipationAge >= lifetime)
            Projectile.Kill();
    }
}

// 困难模式后才进入状态机的高速蓝色激光。其 15 次子更新保留利落飞行，
// 而命中耗尽后的减速与淡出每个真实帧只推进一次。
public sealed class Telekinetic_Flash : ModProjectile
{
    private const int RainMaxUpdates = 7;
    private const float RainSpeed = 20f;
    private int rainStage;
    private int rainTarget = -1;
    private int rainNextDamage;
    private ref float Time => ref Projectile.ai[0];
    private ref float DamagingHitCount => ref Projectile.ai[1];
    private ref float DissipationAge => ref Projectile.ai[2];
    private int DamagingHitLimit => Math.Max(1, (int)Projectile.localAI[0]);
    private bool SuppressLaunchBurst => Projectile.localAI[1] > 0f;
    private bool Dissipating => DissipationAge > 0f;

    internal static void SpawnElysianRain(IEntitySource source, Projectile parent, NPC target, int damage)
    {
        Vector2 spawn = target.Center + new Vector2(Main.rand.NextFloat(-450f, 450f),
            Main.rand.NextFloat(-950f, -750f));
        SpawnRainLaser(source, parent.owner, spawn, target, target.Center, damage,
            parent.knockBack * 0.52f, parent.CritChance, 1);
    }

    private static void SpawnRainLaser(IEntitySource source, int owner, Vector2 spawn,
        NPC target, Vector2 fallbackTarget, int damage, float knockback, int critChance, int stage)
    {
        float speed = RainSpeed * Math.Max(0.01f, FranBalance.GetFloat(
            nameof(FranBalance.TelekineticLaserSpeedMultiplier),
            FranBalance.TelekineticLaserSpeedMultiplier));
        Vector2 velocity = PredictRainAim(spawn, target?.Center ?? fallbackTarget,
            target?.velocity ?? Vector2.Zero, speed);
        int index = Projectile.NewProjectile(source, spawn, velocity, ModContent.ProjectileType<Telekinetic_Flash>(),
            damage, knockback, owner, 0f, 0f, -1f);
        if (!Main.projectile.IndexInRange(index))
            return;
        Projectile rain = Main.projectile[index];
        rain.CritChance = critChance;
        if (rain.ModProjectile is Telekinetic_Flash flash)
            flash.ConfigureRain(stage, target?.whoAmI ?? -1, damage);
        if (!Main.dedServ)
            FranLightingBoltsSystem.SpawnMindCoreBurst(spawn, velocity.ToRotation(),
                new Color(110, 205, 255), false);
    }

    private static Vector2 PredictRainAim(Vector2 spawn, Vector2 targetPosition,
        Vector2 targetVelocity, float speed)
    {
        // Same four-step Euler lead and update-count adjustment as Elysian Arrow's rain.
        float previousTime = 0f;
        Vector2 predicted = targetPosition;
        for (int i = 0; i < 4; i++)
        {
            float time = Vector2.Distance(spawn, predicted) / speed / RainMaxUpdates;
            predicted += targetVelocity * (time - previousTime);
            previousTime = time;
        }
        return (predicted - spawn).SafeNormalize(Vector2.UnitY) * speed;
    }

    private void ConfigureRain(int stage, int targetIndex, int nextDamage)
    {
        rainStage = stage;
        rainTarget = targetIndex;
        rainNextDamage = nextDamage;
        Projectile.MaxUpdates = RainMaxUpdates;
        Projectile.timeLeft = 110;
        Projectile.localAI[0] = 1f;
        Projectile.penetrate = 2;
        Projectile.Opacity = 1f;
        Projectile.netUpdate = true;
    }

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
            EmitLaserSparks(Projectile.Center, Projectile.velocity, Projectile.scale, Time);
        }
        else if (Time == 5f && !Main.dedServ && !SuppressLaunchBurst)
        {
            EmitLaserLaunchRings(Projectile.Center, Projectile.velocity,
                Projectile.rotation, Projectile.scale);
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

    internal static void EmitLaserSparks(Vector2 position, Vector2 velocity,
        float projectileScale, float time, float sizeMultiplier = 1f,
        bool spawnSupportingDust = true, float brightnessMultiplier = 1f)
    {
        float scaleBoost = MathHelper.Clamp(time * 0.005f, 0f, 2f);
        FranParticleSystem.Spawn(new FranHalleySparkParticle(position, velocity,
            new Color(8, 35, 156) * brightnessMultiplier, (3.2f + scaleBoost) * projectileScale * sizeMultiplier, 7),
            spawnSupportingDust: spawnSupportingDust);
        FranParticleSystem.Spawn(new FranHalleySparkParticle(position, velocity,
            new Color(184, 215, 245) * brightnessMultiplier, (1.6f + scaleBoost) * projectileScale * sizeMultiplier, 7),
            spawnSupportingDust: spawnSupportingDust);
    }

    internal static void EmitLaserLaunchRings(Vector2 position, Vector2 velocity,
        float rotation, float projectileScale, float sizeMultiplier = 1f,
        bool spawnSupportingDust = true, float brightnessMultiplier = 1f)
    {
        FranParticleSystem.Spawn(new FranHalleyPulseRingParticle(position,
            velocity * 0.75f, Color.Aqua * brightnessMultiplier, new Vector2(1f, 2.5f) * projectileScale * sizeMultiplier,
            rotation, 0.20f, 0.03f, 20), spawnSupportingDust: spawnSupportingDust);
        FranParticleSystem.Spawn(new FranHalleyPulseRingParticle(position,
            velocity * 0.40f, Color.DodgerBlue * brightnessMultiplier, new Vector2(1f, 2.5f) * projectileScale * sizeMultiplier,
            rotation, 0.10f, 0.025f, 35), spawnSupportingDust: spawnSupportingDust);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        EnsureConfigured();
        if (rainStage == 1)
        {
            rainTarget = target.whoAmI;
            rainNextDamage = Math.Max(1, (int)(rainNextDamage * 0.8f));
        }
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

    public override void OnKill(int timeLeft)
    {
        if (rainStage != 1 || Main.myPlayer != Projectile.owner)
            return;
        NPC target = Main.npc.IndexInRange(rainTarget) && Main.npc[rainTarget].CanBeChasedBy()
            ? Main.npc[rainTarget] : null;
        if (target is null)
        {
            float closest = 2000f * 2000f;
            foreach (NPC candidate in Main.ActiveNPCs)
            {
                if (!candidate.CanBeChasedBy())
                    continue;
                float distance = Vector2.DistanceSquared(Projectile.Center, candidate.Center);
                if (distance < closest)
                {
                    closest = distance;
                    target = candidate;
                }
            }
        }
        Vector2 center = target?.Center ?? Projectile.Center;
        Vector2 spawn = center + new Vector2(Main.rand.NextFloat(-450f, 450f),
            Main.rand.NextFloat(750f, 950f));
        SpawnRainLaser(Projectile.GetSource_FromThis(), Projectile.owner, spawn, target,
            center, Math.Max(1, rainNextDamage), Projectile.knockBack, Projectile.CritChance, 2);
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((byte)rainStage);
        writer.Write((short)rainTarget);
        writer.Write(rainNextDamage);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        rainStage = reader.ReadByte();
        rainTarget = reader.ReadInt16();
        rainNextDamage = reader.ReadInt32();
        if (rainStage > 0)
        {
            Projectile.MaxUpdates = RainMaxUpdates;
            Projectile.localAI[0] = 1f;
            Projectile.penetrate = 2;
            Projectile.Opacity = 1f;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        DrawLaserAppearance(Projectile);
        return false;
    }

    internal static void DrawLaserAppearance(Projectile projectile, float sizeMultiplier = 1f,
        float brightnessMultiplier = 1f)
    {
        float opacity = MathHelper.Clamp(projectile.Opacity * brightnessMultiplier, 0f, 1f);
        Vector2 direction = projectile.velocity.SafeNormalize(projectile.rotation.ToRotationVector2());
        float rotation = direction.ToRotation();
        float pulse = 0.92f + MathF.Sin(Main.GlobalTimeWrappedHourly * 18f + projectile.identity) * 0.08f;

        FranVfx.BeginAdditive();
        FranVfx.DrawShared("CL_BloomCircle", projectile.Center,
            new Color(45, 135, 255, 0) * (0.52f * opacity), rotation,
            0.48f * pulse * projectile.scale * sizeMultiplier);
        FranVfx.DrawOriginalPixels("FranSmearForward", projectile.Center - direction * 18f * sizeMultiplier,
            new Color(65, 175, 255) * (0.48f * opacity), rotation,
            new Vector2(104f, 24f) * pulse * projectile.scale * sizeMultiplier);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", projectile.Center + direction * 12f * sizeMultiplier,
            Color.White * (0.78f * opacity), rotation,
            new Vector2(38f, 20f) * pulse * projectile.scale * sizeMultiplier);
        FranVfx.EndAdditive();
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
