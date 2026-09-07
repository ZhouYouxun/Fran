using System;
using System.IO;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Core;
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


public sealed class FranUltimateController : ModProjectile
{
    private enum UltimatePhase : byte
    {
        NormalStrike,
        ImpactDrift,
        FinalPause,
        FinalStrike
    }

    private static int NormalStrikeCount => FranBalance.GetInt(nameof(FranBalance.UltimateNormalStrikeCount),
        FranBalance.UltimateNormalStrikeCount);
    private static int StartingStrikeFrames => FranBalance.GetInt(nameof(FranBalance.UltimateStartingStrikeFrames),
        FranBalance.UltimateStartingStrikeFrames);
    private static int EndingStrikeFrames => FranBalance.GetInt(nameof(FranBalance.UltimateEndingStrikeFrames),
        FranBalance.UltimateEndingStrikeFrames);
    private static int StartingImpactDriftFrames => FranBalance.GetInt(nameof(FranBalance.UltimateStartingDriftFrames),
        FranBalance.UltimateStartingDriftFrames);
    private static int EndingImpactDriftFrames => FranBalance.GetInt(nameof(FranBalance.UltimateEndingDriftFrames),
        FranBalance.UltimateEndingDriftFrames);
    private static int FinalPauseFrames => FranBalance.GetInt(nameof(FranBalance.UltimateFinalPauseFrames),
        FranBalance.UltimateFinalPauseFrames);
    private static int FinalStrikeFrames => FranBalance.GetInt(nameof(FranBalance.UltimateFinalStrikeFrames),
        FranBalance.UltimateFinalStrikeFrames);
    private const float GoldenAngle = 2.39996323f;
    private const float NormalCollisionWidth = 78f;
    private const float FinalCollisionWidth = 142f;

    private UltimatePhase phase;
    private int phaseTimer;
    private int strikeIndex;
    private int targetNpcIndex = -1;
    private bool initialized;
    private bool impactVisualSpawned;
    private bool riftLaserSpawned;
    private float sequenceRotation;
    private Vector2 originalCenter;
    private Vector2 focusPoint;
    private Vector2 targetPoint;
    private Vector2 strikeStart;
    private Vector2 strikeEnd;
    private Vector2 collisionStart;
    private Vector2 collisionEnd;
    private Vector2 impactDriftVelocity;

    private bool Authority => Main.netMode != NetmodeID.MultiplayerClient;
    private Player Owner => Main.player[Projectile.owner];
    private bool FinalStrike => phase == UltimatePhase.FinalStrike;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1800;
    }

    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 2;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;

    public override bool? CanDamage() =>
        (phase is UltimatePhase.NormalStrike or UltimatePhase.FinalStrike) &&
        Vector2.DistanceSquared(collisionStart, collisionEnd) > 4f ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (CanDamage() == false)
            return false;
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            collisionStart, collisionEnd, FinalStrike ? FinalCollisionWidth : NormalCollisionWidth,
            ref collisionPoint);
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((byte)phase);
        writer.Write(phaseTimer);
        writer.Write(strikeIndex);
        writer.Write(targetNpcIndex);
        writer.Write(initialized);
        writer.Write(impactVisualSpawned);
        writer.Write(riftLaserSpawned);
        writer.Write(sequenceRotation);
        writer.WriteVector2(originalCenter);
        writer.WriteVector2(focusPoint);
        writer.WriteVector2(targetPoint);
        writer.WriteVector2(strikeStart);
        writer.WriteVector2(strikeEnd);
        writer.WriteVector2(collisionStart);
        writer.WriteVector2(collisionEnd);
        writer.WriteVector2(impactDriftVelocity);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        phase = (UltimatePhase)reader.ReadByte();
        phaseTimer = reader.ReadInt32();
        strikeIndex = reader.ReadInt32();
        targetNpcIndex = reader.ReadInt32();
        initialized = reader.ReadBoolean();
        impactVisualSpawned = reader.ReadBoolean();
        riftLaserSpawned = reader.ReadBoolean();
        sequenceRotation = reader.ReadSingle();
        originalCenter = reader.ReadVector2();
        focusPoint = reader.ReadVector2();
        targetPoint = reader.ReadVector2();
        strikeStart = reader.ReadVector2();
        strikeEnd = reader.ReadVector2();
        collisionStart = reader.ReadVector2();
        collisionEnd = reader.ReadVector2();
        impactDriftVelocity = reader.ReadVector2();
    }

    public override void AI()
    {
        Player owner = Owner;
        if (!owner.active || owner.dead)
        {
            if (Authority)
                Projectile.Kill();
            return;
        }

        Projectile.timeLeft = 2;
        if (!initialized)
        {
            if (!Authority)
                return;
            InitializeSequence(owner);
        }

        UpdateLockedTargetPosition();
        owner.GetModPlayer<FranPlayer>().LockUltimateCamera(targetPoint);

        if (Authority)
            UpdateAuthoritativeSequence(owner);
        else
            UpdateClientPresentation(owner);

        // 终结斩结束时 OnKill 已释放玩家状态，不要在本帧又把它锁回大招状态。
        if (!Projectile.active)
            return;
        MaintainOwnerState(owner);
        // The controller itself remains at the execution focus.  Visual companions can therefore
        // follow one synced anchor while the owner repeatedly crosses the frame.
        Projectile.Center = targetPoint;
    }

    private void InitializeSequence(Player owner)
    {
        initialized = true;
        originalCenter = owner.Center;
        focusPoint = new Vector2(Projectile.ai[0], Projectile.ai[1]);
        if (!float.IsFinite(focusPoint.X) || !float.IsFinite(focusPoint.Y))
            focusPoint = owner.Center + Vector2.UnitX * owner.direction * 480f;
        sequenceRotation = Projectile.identity * 0.731f + owner.whoAmI * 0.47f;
        strikeIndex = 0;
        targetNpcIndex = FindBestTarget(owner, focusPoint);
        targetPoint = targetNpcIndex >= 0 ? Main.npc[targetNpcIndex].Center : focusPoint;
        Projectile.Center = targetPoint;
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), targetPoint, Vector2.Zero,
            ModContent.ProjectileType<FranUltimateTargetField>(), 0, 0f, Projectile.owner,
            Projectile.whoAmI, sequenceRotation);
        BeginNormalStrike(owner);
    }

    private void UpdateAuthoritativeSequence(Player owner)
    {
        // 在下一帧才离开已完成阶段，保留本帧的碰撞线与伤害开关。
        // 否则 1 帧穿斩会在 Terraria 执行命中检测之前就被漂移阶段清空，变成空斩。
        switch (phase)
        {
            case UltimatePhase.NormalStrike:
                if (phaseTimer >= GetCurrentStrikeFrames())
                    BeginImpactDrift();
                break;
            case UltimatePhase.ImpactDrift:
                if (phaseTimer >= GetCurrentImpactDriftFrames())
                {
                    strikeIndex++;
                    if (strikeIndex < NormalStrikeCount)
                        BeginNormalStrike(owner);
                    else
                        BeginFinalPause(owner);
                }
                break;
            case UltimatePhase.FinalPause:
                if (phaseTimer >= FinalPauseFrames)
                    BeginFinalStrike(owner);
                break;
            case UltimatePhase.FinalStrike:
                if (phaseTimer >= FinalStrikeFrames)
                {
                    Projectile.Kill();
                    return;
                }
                break;
        }

        phaseTimer++;
        switch (phase)
        {
            case UltimatePhase.NormalStrike:
                ApplyStrikeMovement(owner, GetCurrentStrikeFrames());
                break;
            case UltimatePhase.ImpactDrift:
                ApplyImpactDrift(owner);
                break;
            case UltimatePhase.FinalPause:
                HoldAt(owner, strikeEnd);
                break;
            case UltimatePhase.FinalStrike:
                ApplyStrikeMovement(owner, FinalStrikeFrames);
                break;
        }
    }

    private void UpdateClientPresentation(Player owner)
    {
        int maximum = phase switch
        {
            UltimatePhase.NormalStrike => GetCurrentStrikeFrames(),
            UltimatePhase.ImpactDrift => GetCurrentImpactDriftFrames(),
            UltimatePhase.FinalPause => FinalPauseFrames,
            _ => FinalStrikeFrames
        };
        phaseTimer = Math.Min(phaseTimer + 1, maximum);
        if (phase is UltimatePhase.NormalStrike or UltimatePhase.FinalStrike)
            ApplyStrikeMovement(owner, maximum);
        else if (phase == UltimatePhase.ImpactDrift)
            ApplyImpactDrift(owner);
        else
            HoldAt(owner, strikeEnd);
    }

    private void BeginNormalStrike(Player owner)
    {
        phase = UltimatePhase.NormalStrike;
        phaseTimer = 0;
        impactVisualSpawned = false;
        riftLaserSpawned = false;
        EnsureTarget(owner);

        float angle = sequenceRotation + strikeIndex * GoldenAngle;
        Vector2 axis = angle.ToRotationVector2();
        float entryRadius = 292f + 42f * MathF.Sin(strikeIndex * 1.31f);
        float exitRadius = 258f + 36f * MathF.Cos(strikeIndex * 0.83f);
        strikeStart = targetPoint + axis * entryRadius;
        strikeEnd = targetPoint - axis * exitRadius;
        collisionStart = strikeStart;
        collisionEnd = strikeStart;
        impactDriftVelocity = Vector2.Zero;
        Projectile.damage = owner.GetModPlayer<FranPlayer>().GetUltimateStrikeDamage();
        Projectile.friendly = true;
        ClearLocalNpcImmunity();
        WarpOwner(owner, strikeStart);
        SpawnStrikeLaunch(strikeStart, strikeEnd, false);
        Projectile.netUpdate = true;
    }

    private void BeginImpactDrift()
    {
        phase = UltimatePhase.ImpactDrift;
        phaseTimer = 0;
        Projectile.friendly = false;
        collisionStart = strikeEnd;
        collisionEnd = strikeEnd;
        Vector2 direction = (strikeEnd - strikeStart).SafeNormalize(Vector2.UnitX);
        float retainedSpeed = Math.Min(22f, Math.Max(8f, Projectile.velocity.Length() * 0.32f));
        impactDriftVelocity = direction * retainedSpeed;
        if (!impactVisualSpawned)
            SpawnStrikeImpact(targetPoint, false);
        Projectile.netUpdate = true;
    }

    private void BeginFinalPause(Player owner)
    {
        phase = UltimatePhase.FinalPause;
        phaseTimer = 0;
        Projectile.friendly = false;
        EnsureTarget(owner);
        Vector2 pauseDirection = (targetPoint - owner.Center).SafeNormalize(
            (strikeEnd - strikeStart).SafeNormalize(Vector2.UnitX * owner.direction));
        strikeEnd = targetPoint - pauseDirection * 184f;
        WarpOwner(owner, strikeEnd);
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), targetPoint, Vector2.Zero,
            ModContent.ProjectileType<FranUltimateExecutionSeal>(), 0, 0f, Projectile.owner,
            FinalPauseFrames, sequenceRotation);
        Projectile.netUpdate = true;
    }

    private void BeginFinalStrike(Player owner)
    {
        phase = UltimatePhase.FinalStrike;
        phaseTimer = 0;
        impactVisualSpawned = false;
        riftLaserSpawned = false;
        if (targetNpcIndex >= 0 && Main.npc.IndexInRange(targetNpcIndex) &&
            Main.npc[targetNpcIndex].active && Main.npc[targetNpcIndex].CanBeChasedBy())
            targetPoint = Main.npc[targetNpcIndex].Center;

        Vector2 direction = (targetPoint - owner.Center).SafeNormalize(
            (focusPoint - originalCenter).SafeNormalize(Vector2.UnitX * owner.direction));
        strikeStart = targetPoint - direction * 382f;
        strikeEnd = targetPoint + direction * 468f;
        collisionStart = strikeStart;
        collisionEnd = strikeStart;
        Projectile.damage = owner.GetModPlayer<FranPlayer>().GetUltimateStrikeDamage();
        Projectile.friendly = true;
        ClearLocalNpcImmunity();
        WarpOwner(owner, strikeStart);
        SpawnStrikeLaunch(strikeStart, strikeEnd, true);
        Projectile.netUpdate = true;
    }

    private void ApplyStrikeMovement(Player owner, int duration)
    {
        Vector2 previous = owner.Center;
        float progress = MathHelper.Clamp(phaseTimer / (float)Math.Max(1, duration), 0f, 1f);
        float eased = 1f - MathF.Pow(1f - progress, FinalStrike ? 3f : 2f);
        Vector2 current = Vector2.Lerp(strikeStart, strikeEnd, eased);
        Vector2 movement = current - previous;
        owner.Center = current;
        owner.velocity = movement;
        collisionStart = previous;
        collisionEnd = current;
        Projectile.velocity = movement;
        if (movement.LengthSquared() > 0.1f)
            owner.ChangeDir(movement.X >= 0f ? 1 : -1);

        SpawnTravelEffects(previous, current, FinalStrike);
        if (Authority && !impactVisualSpawned && progress >= (FinalStrike ? 0.44f : 0.46f))
            SpawnStrikeImpact(targetPoint, FinalStrike);
    }

    private void ApplyImpactDrift(Player owner)
    {
        Vector2 previous = owner.Center;
        owner.Center += impactDriftVelocity;
        owner.velocity = impactDriftVelocity;
        collisionStart = previous;
        collisionEnd = owner.Center;
        Projectile.velocity = impactDriftVelocity;
        SpawnTravelEffects(previous, owner.Center, false);
        impactDriftVelocity *= 0.58f;
    }

    private static void HoldAt(Player owner, Vector2 center)
    {
        owner.Center = center;
        owner.velocity = Vector2.Zero;
    }

    private void UpdateLockedTargetPosition()
    {
        if (IsTargetValid(targetNpcIndex))
            targetPoint = Main.npc[targetNpcIndex].Center;
    }

    private void EnsureTarget(Player owner)
    {
        if (IsTargetValid(targetNpcIndex))
        {
            targetPoint = Main.npc[targetNpcIndex].Center;
            return;
        }

        Vector2 searchFocus = float.IsFinite(targetPoint.X) && float.IsFinite(targetPoint.Y)
            ? targetPoint
            : focusPoint;
        targetNpcIndex = FindBestTarget(owner, searchFocus);
        targetPoint = targetNpcIndex >= 0 ? Main.npc[targetNpcIndex].Center : searchFocus;
    }

    private static bool IsTargetValid(int npcIndex) =>
        Main.npc.IndexInRange(npcIndex) && Main.npc[npcIndex].active && Main.npc[npcIndex].CanBeChasedBy();

    private int GetCurrentStrikeFrames()
    {
        float sequenceProgress = strikeIndex / (float)Math.Max(1, NormalStrikeCount - 1);
        return Math.Max(1, (int)MathF.Round(MathHelper.Lerp(StartingStrikeFrames, EndingStrikeFrames,
            sequenceProgress)));
    }

    private int GetCurrentImpactDriftFrames()
    {
        float sequenceProgress = strikeIndex / (float)Math.Max(1, NormalStrikeCount - 1);
        return Math.Max(1, (int)MathF.Round(MathHelper.Lerp(StartingImpactDriftFrames,
            EndingImpactDriftFrames, sequenceProgress)));
    }

    private void MaintainOwnerState(Player owner)
    {
        Vector2 swordDirection = (strikeEnd - strikeStart).SafeNormalize(Vector2.UnitX * owner.direction);
        int facing = swordDirection.X < 0f ? -1 : 1;
        owner.ChangeDir(facing);
        owner.immune = true;
        owner.immuneTime = Math.Max(owner.immuneTime, 2);
        owner.noKnockback = true;
        owner.fallStart = (int)(owner.position.Y / 16f);
        owner.itemAnimation = 2;
        owner.itemTime = 2;
        owner.heldProj = Projectile.whoAmI;
        owner.itemRotation = swordDirection.ToRotation() + (facing < 0 ? MathHelper.Pi : 0f);
        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            swordDirection.ToRotation() - MathHelper.PiOver2);
        for (int i = 0; i < owner.hurtCooldowns.Length; i++)
            owner.hurtCooldowns[i] = Math.Max(owner.hurtCooldowns[i], 2);
    }

    private void WarpOwner(Player owner, Vector2 center)
    {
        Vector2 topLeft = center - owner.Size * 0.5f;
        owner.Teleport(topLeft, -1);
        owner.velocity = Vector2.Zero;
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, owner.whoAmI,
                topLeft.X, topLeft.Y, -1);
    }

    private void SpawnStrikeLaunch(Vector2 start, Vector2 end, bool final)
    {
        Vector2 direction = (end - start).SafeNormalize(Vector2.UnitX);
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), start, Vector2.Zero,
            ModContent.ProjectileType<FranUltimateDashLaunch>(), 0, 0f, Projectile.owner,
            direction.ToRotation(), final ? 1f : 0f, strikeIndex);

        // These are deliberately non-damaging projectiles.  They arrive from independent orbits
        // and keep the locked target visually under siege while the player performs the real cut.
        int count = final ? 10 : 4;
        for (int i = 0; i < count; i++)
        {
            float angle = sequenceRotation + strikeIndex * 0.83f + MathHelper.TwoPi * i / count;
            Vector2 radial = angle.ToRotationVector2();
            Vector2 spectatorStart = targetPoint + radial * (final ? 330f : 224f + i % 2 * 34f);
            Vector2 velocity = (targetPoint - spectatorStart).SafeNormalize(-radial) *
                (final ? 20f : 13.5f + i % 2 * 1.8f);
            float curvature = (i % 2 == 0 ? 1f : -1f) * (final ? 0.012f : 0.026f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), spectatorStart, velocity,
                ModContent.ProjectileType<FranUltimateSpectatorBlade>(), 0, 0f, Projectile.owner,
                curvature, final ? 1f : 0f, 0.82f + i % 3 * 0.10f);
        }
    }

    private void SpawnStrikeImpact(Vector2 impactPoint, bool final)
    {
        if (!Authority || impactVisualSpawned)
            return;

        impactVisualSpawned = true;
        Vector2 direction = (strikeEnd - strikeStart).SafeNormalize(Vector2.UnitX);
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), impactPoint, Vector2.Zero,
            ModContent.ProjectileType<FranUltimateStrikeBeat>(), 0, 0f, Projectile.owner,
            final ? 1f : 0f, direction.ToRotation(), strikeIndex);
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), impactPoint, Vector2.Zero,
            ModContent.ProjectileType<FranUltimateSlashResidue>(), 0, 0f, Projectile.owner,
            direction.ToRotation(), final ? 1f : 0f, strikeIndex);
        Projectile.netUpdate = true;
    }

    private void SpawnRiftLaser(Vector2 impactPoint, bool final)
    {
        if (!Authority || riftLaserSpawned)
            return;

        riftLaserSpawned = true;
        Vector2 direction = (strikeEnd - strikeStart).SafeNormalize(Vector2.UnitX);
        float riftAngle = direction.ToRotation() + MathHelper.PiOver2 +
            Main.rand.NextFloat(final ? -0.12f : -0.52f, final ? 0.12f : 0.52f);
        // 裂隙也是独立伤害攻击，继承本刀的面板 × 8，不再另乘 0.42 / 0.55。
        int riftDamage = Projectile.damage;
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), impactPoint, Vector2.Zero,
            ModContent.ProjectileType<FranUltimateBlackRiftLaser>(), riftDamage,
            Projectile.knockBack * 0.35f, Projectile.owner, riftAngle, final ? 1f : 0f, strikeIndex);
        Projectile.netUpdate = true;
    }

    private static void SpawnTravelEffects(Vector2 previous, Vector2 current, bool final)
    {
        if (Main.dedServ)
            return;

        Vector2 movement = current - previous;
        float distance = movement.Length();
        if (distance < 1f)
            return;

        Vector2 direction = movement / distance;
        Color color = final ? new Color(255, 207, 105) : new Color(88, 204, 245);
        int samples = Math.Clamp((int)MathF.Ceiling(distance / 34f), 1, final ? 8 : 5);
        for (int i = 0; i < samples; i++)
        {
            Vector2 point = Vector2.Lerp(previous, current, (i + 0.5f) / samples) +
                Main.rand.NextVector2Circular(4f, 4f);
            float length = Main.rand.NextFloat(final ? 38f : 24f, final ? 72f : 48f);
            FranParticleSystem.Spawn(new FranBladeWakeSliverParticle(point,
                -direction * Main.rand.NextFloat(0.3f, 1.4f), direction, color, length,
                final ? 3.2f : 2.2f, Main.rand.Next(8, 13), Main.rand.NextFloat(-0.018f, 0.018f)));
        }

        FranVanillaDustEffects.ProjectileTrail((previous + current) * 0.5f, movement,
            FranSkill.None, color, final ? 0.9f : 0.72f, final ? 1.18f : 0.94f);
    }

    private void ClearLocalNpcImmunity()
    {
        if (Projectile.localNPCImmunity is not null)
            Array.Clear(Projectile.localNPCImmunity, 0, Projectile.localNPCImmunity.Length);
    }

    private static int FindBestTarget(Player owner, Vector2 focus)
    {
        int best = -1;
        float bestScore = float.MaxValue;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy())
                continue;
            float ownerDistance = Vector2.DistanceSquared(owner.Center, npc.Center);
            if (ownerDistance > 1900f * 1900f)
                continue;
            float focusDistance = Vector2.DistanceSquared(focus, npc.Center);
            float score = focusDistance * 0.72f + ownerDistance * 0.28f;
            if (focusDistance > 900f * 900f)
                score += 900f * 900f;
            if (score < bestScore)
            {
                bestScore = score;
                best = npc.whoAmI;
            }
        }
        return best;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        SpawnStrikeImpact(target.Center, FinalStrike);
        SpawnRiftLaser(target.Center, FinalStrike);
        if (Authority)
            FranBlackTigerImpactVisual.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                (strikeEnd - strikeStart).SafeNormalize(Vector2.UnitX), Projectile.owner,
                FinalStrike ? 2 : 1);
    }

    public override void OnKill(int timeLeft)
    {
        if (!Main.player.IndexInRange(Projectile.owner))
            return;
        Player owner = Owner;
        owner.heldProj = -1;
        owner.itemAnimation = 0;
        owner.itemTime = 0;
        owner.velocity *= 0.2f;
        owner.GetModPlayer<FranPlayer>().ClearUltimateCamera();
        if (!Authority || owner.dead)
            return;

        Vector2 landingCenter = FindSafeLanding(owner, owner.Center, originalCenter);
        WarpOwner(owner, landingCenter);
    }

    private static Vector2 FindSafeLanding(Player owner, Vector2 preferred, Vector2 fallback)
    {
        if (!Collision.SolidCollision(preferred - owner.Size * 0.5f, owner.width, owner.height))
            return preferred;
        for (int radius = 24; radius <= 144; radius += 24)
            for (int i = 0; i < 12; i++)
            {
                Vector2 candidate = preferred + (MathHelper.TwoPi * i / 12f).ToRotationVector2() * radius;
                if (!Collision.SolidCollision(candidate - owner.Size * 0.5f, owner.width, owner.height))
                    return candidate;
            }
        return fallback;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (!initialized)
            return false;
        Player owner = Owner;
        Vector2 direction = (strikeEnd - strikeStart).SafeNormalize(Vector2.UnitX * owner.direction);
        float progress = phase switch
        {
            UltimatePhase.NormalStrike => MathHelper.Clamp(phaseTimer / (float)GetCurrentStrikeFrames(), 0f, 1f),
            UltimatePhase.FinalStrike => MathHelper.Clamp(phaseTimer / (float)FinalStrikeFrames, 0f, 1f),
            _ => 1f
        };
        float flash = phase == UltimatePhase.FinalPause
            ? 0.45f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f)
            : MathF.Sin(progress * MathHelper.Pi);
        Color body = FinalStrike ? new Color(255, 218, 126) : new Color(96, 203, 242);
        Color edge = FinalStrike ? Color.White : new Color(207, 159, 255);

        Texture2D teacherTexture = FranTeacherVisuals.Texture;
        int facing = direction.X < 0f ? -1 : 1;
        SpriteEffects swordEffects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 swordOrigin = new(facing < 0 ? teacherTexture.Width : 0f, teacherTexture.Height);
        float swordRotation = direction.ToRotation() + MathHelper.PiOver4 +
            (facing < 0 ? MathHelper.PiOver2 : 0f);
        Vector2 swordPosition = owner.MountedCenter + direction * 8f - Main.screenPosition;
        Main.EntitySpriteDraw(teacherTexture, swordPosition, null,
            Color.Lerp(lightColor, Color.White, 0.62f), swordRotation, swordOrigin,
            (FinalStrike ? 1.42f : 1.24f) * FranTeacherVisuals.TextureScale, swordEffects, 0f);

        FranVfx.BeginAdditive();
        if (phase is UltimatePhase.NormalStrike or UltimatePhase.FinalStrike)
        {
            float length = Math.Min(FinalStrike ? 920f : 620f,
                Math.Max(Vector2.Distance(collisionStart, collisionEnd) + 120f,
                    Vector2.Distance(strikeStart, strikeEnd) * (0.34f + progress * 0.66f)));
            Vector2 midpoint = Vector2.Lerp(strikeStart, strikeEnd, progress * 0.5f);
            FranVfx.DrawOriginalPixels("FranSmearForward", midpoint, body * (0.58f * flash),
                direction.ToRotation(), new Vector2(length, FinalStrike ? 118f : 68f));
            FranVfx.DrawOriginalPixels("FranSmearForward", midpoint, Color.White * (0.52f * flash),
                direction.ToRotation(), new Vector2(length * 0.84f, FinalStrike ? 34f : 20f));
            FranVfx.DrawOriginalPixels("FranImpactBurst", targetPoint,
                edge * (0.22f * flash), direction.ToRotation() + MathHelper.PiOver2,
                new Vector2(FinalStrike ? 260f : 132f, FinalStrike ? 76f : 42f));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", owner.Center, edge * (0.72f * flash),
                direction.ToRotation(), new Vector2(FinalStrike ? 102f : 62f, FinalStrike ? 48f : 30f));

            int ghostCount = FinalStrike ? 5 : 3;
            for (int i = 1; i <= ghostCount; i++)
            {
                float ghostProgress = MathHelper.Clamp(progress - i * (FinalStrike ? 0.055f : 0.08f), 0f, 1f);
                Vector2 ghostPosition = Vector2.Lerp(strikeStart, strikeEnd, ghostProgress);
                Main.EntitySpriteDraw(teacherTexture, ghostPosition + direction * 8f - Main.screenPosition, null,
                    body * (0.15f * (1f - i / (ghostCount + 1f)) * flash), swordRotation,
                    swordOrigin, (FinalStrike ? 1.38f : 1.20f) * FranTeacherVisuals.TextureScale,
                    swordEffects, 0f);
            }
        }
        if (phase == UltimatePhase.FinalPause)
        {
            float rotation = Main.GlobalTimeWrappedHourly * 1.6f;
            for (int i = 0; i < 8; i++)
            {
                float angle = rotation + i * MathHelper.PiOver4;
                Vector2 point = targetPoint + angle.ToRotationVector2() * (78f + flash * 10f);
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", point,
                    Color.Lerp(body, edge, i % 2 * 0.55f) * (0.52f * flash), angle,
                    new Vector2(34f, 18f));
            }
        }
        FranVfx.EndAdditive();
        return false;
    }
}
