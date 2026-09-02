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
        phaseTimer++;
        switch (phase)
        {
            case UltimatePhase.NormalStrike:
                ApplyStrikeMovement(owner, GetCurrentStrikeFrames());
                if (phaseTimer >= GetCurrentStrikeFrames())
                    BeginImpactDrift();
                break;
            case UltimatePhase.ImpactDrift:
                ApplyImpactDrift(owner);
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
                HoldAt(owner, strikeEnd);
                if (phaseTimer >= FinalPauseFrames)
                    BeginFinalStrike(owner);
                break;
            case UltimatePhase.FinalStrike:
                ApplyStrikeMovement(owner, FinalStrikeFrames);
                if (phaseTimer >= FinalStrikeFrames)
                    Projectile.Kill();
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
        int riftDamage = Math.Max(1, (int)(Projectile.damage * (final
            ? FranBalance.GetFloat(nameof(FranBalance.UltimateFinalRiftDamageMultiplier),
                FranBalance.UltimateFinalRiftDamageMultiplier)
            : FranBalance.GetFloat(nameof(FranBalance.UltimateNormalRiftDamageMultiplier),
                FranBalance.UltimateNormalRiftDamageMultiplier))));
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

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        if (FinalStrike)
            modifiers.SourceDamage *= FranBalance.GetFloat(nameof(FranBalance.UltimateFinalStrikeDamageMultiplier),
                FranBalance.UltimateFinalStrikeDamageMultiplier);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        SpawnStrikeImpact(target.Center, FinalStrike);
        SpawnRiftLaser(target.Center, FinalStrike);
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

// A persistent target-centred composition layer.  It is not a hitbox: it gives the locked camera
// one stable arena while the player and the spectator blades repeatedly cross through it.
public sealed class FranUltimateTargetField : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = 2;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        int controllerIndex = (int)Projectile.ai[0];
        if (!Main.projectile.IndexInRange(controllerIndex))
        {
            Projectile.Kill();
            return;
        }

        Projectile controller = Main.projectile[controllerIndex];
        if (!controller.active || controller.owner != Projectile.owner ||
            controller.type != ModContent.ProjectileType<FranUltimateController>())
        {
            Projectile.Kill();
            return;
        }

        Projectile.timeLeft = 2;
        Projectile.Center = controller.Center;
        Projectile.localAI[0]++;
        Lighting.AddLight(Projectile.Center, new Vector3(0.16f, 0.32f, 0.46f));

        if (Main.dedServ || Projectile.localAI[1]++ != 0f)
            return;

        Color cyan = new(91, 211, 249);
        FranParticleEffects.BloomPulse(Projectile.Center, cyan, 22f, 154f, 24, 0.86f, true);
        FranParticleEffects.RingPulse(Projectile.Center, cyan, Vector2.One * 24f,
            new Vector2(196f, 124f), 25, Projectile.ai[1], 0.024f, 0.92f);
        FranParticleEffects.RingPulse(Projectile.Center, new Color(207, 161, 255), Vector2.One * 18f,
            new Vector2(132f, 198f), 28, Projectile.ai[1] + MathHelper.PiOver2, -0.022f, 0.72f);
        FranParticleEffects.MysticGlyphs(Projectile.Center, new Color(135, 222, 255), 5, 88f, 0.88f);
        FranConstellationEffects.SpawnBlueResonancePrelude(Projectile.Center,
            Projectile.ai[1].ToRotationVector2(), 0);
        FranVanillaDustEffects.SkillRelease(Projectile.Center, Projectile.ai[1].ToRotationVector2(),
            FranSkill.None, cyan, 24, 1.12f);
        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.72f, Pitch = -0.28f }, Projectile.Center);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float time = Projectile.localAI[0];
        float appear = Utils.GetLerpValue(0f, 10f, time, true);
        float rotation = Projectile.ai[1] + time * 0.018f;
        float pulse = 0.76f + 0.24f * MathF.Sin(time * 0.22f);
        Color cyan = new(88, 204, 245);
        Color violet = new(185, 126, 255);

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            cyan * (0.17f * appear * pulse), rotation, Vector2.One * 286f);
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            violet * (0.12f * appear), -rotation * 0.72f, new Vector2(248f, 178f));
        for (int i = 0; i < 12; i++)
        {
            float angle = rotation + MathHelper.TwoPi * i / 12f;
            float radius = 112f + MathF.Sin(time * 0.16f + i * 1.7f) * 12f;
            Vector2 point = Projectile.Center + angle.ToRotationVector2() * radius;
            Color color = i % 3 == 0 ? new Color(255, 211, 111) : i % 2 == 0 ? violet : cyan;
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", point,
                color * (0.44f * appear * pulse), angle,
                new Vector2(i % 3 == 0 ? 34f : 25f, i % 3 == 0 ? 17f : 12f));
        }
        FranVfx.EndAdditive();
        return false;
    }
}

// The launch shock is separate from the impact shock.  That separation is what makes each dash
// read as a complete action rather than a single texture teleported between two positions.
public sealed class FranUltimateDashLaunch : ModProjectile
{
    private const int Lifetime = 16;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        if (Main.dedServ || Projectile.localAI[0]++ != 0f)
            return;

        bool final = Projectile.ai[1] > 0.5f;
        Vector2 direction = Projectile.ai[0].ToRotationVector2();
        Color color = final ? new Color(255, 207, 108) : new Color(88, 207, 247);
        FranParticleEffects.RingPulse(Projectile.Center, color,
            new Vector2(12f, 24f), new Vector2(final ? 188f : 112f, final ? 82f : 58f),
            final ? 20 : 14, direction.ToRotation(), 0f, final ? 1f : 0.78f);
        FranParticleEffects.BloomPulse(Projectile.Center, color, 10f, final ? 98f : 54f,
            final ? 18 : 12, final ? 0.92f : 0.68f, true);
        FranParticleEffects.NeedleFan(Projectile.Center, direction, color, final ? 14 : 6,
            final ? 10f : 6.8f, final ? 1.18f : 0.76f, final ? 0.38f : 0.52f);
        FranVanillaDustEffects.SkillRelease(Projectile.Center, direction, FranSkill.None,
            color, final ? 22 : 9, final ? 1.2f : 0.9f);
        SoundEngine.PlaySound(SoundID.Item71 with
        {
            Volume = final ? 0.86f : 0.42f,
            Pitch = final ? -0.36f : 0.22f + ((int)Projectile.ai[2] % 4) * 0.025f
        }, Projectile.Center);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        bool final = Projectile.ai[1] > 0.5f;
        float progress = 1f - Projectile.timeLeft / (float)Lifetime;
        float fade = MathF.Pow(1f - progress, 1.7f);
        float radius = MathHelper.Lerp(18f, final ? 182f : 104f, FranMotion.EaseOutCubic(progress));
        float rotation = Projectile.ai[0];
        Color color = final ? new Color(255, 207, 108) : new Color(91, 211, 249);

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.58f * fade),
            rotation, new Vector2(radius * 1.42f, radius * 0.62f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.72f * fade),
            rotation, new Vector2(radius * 1.18f, radius * 0.28f));
        FranVfx.EndAdditive();
        return false;
    }
}

// A frozen cut remains at the target for a few frames.  Several residues can overlap during the
// accelerating half of the sequence, producing the anime-style cage of completed slashes.
public sealed class FranUltimateSlashResidue : ModProjectile
{
    private const int Lifetime = 30;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override bool PreDraw(ref Color lightColor)
    {
        bool final = Projectile.ai[1] > 0.5f;
        float progress = 1f - Projectile.timeLeft / (float)Lifetime;
        float appear = Utils.GetLerpValue(0f, 0.10f, progress, true);
        float fade = appear * MathF.Pow(1f - progress, final ? 0.78f : 1.28f);
        float expansion = FranMotion.EaseOutCubic(progress);
        float rotation = Projectile.ai[0];
        Color body = final ? new Color(255, 206, 101) : new Color(89, 205, 245);
        Color accent = final ? Color.White : new Color(195, 135, 255);
        float length = MathHelper.Lerp(final ? 360f : 176f, final ? 690f : 292f, expansion);

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center, body * (0.40f * fade),
            rotation, new Vector2(length, final ? 96f : 48f));
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center, Color.White * (0.62f * fade),
            rotation, new Vector2(length * 0.92f, final ? 18f : 10f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, accent * (0.64f * fade),
            rotation, new Vector2(final ? 310f : 142f, final ? 66f : 34f));
        if (final)
        {
            FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center,
                new Color(183, 126, 255) * (0.34f * fade), rotation + MathHelper.PiOver2,
                new Vector2(length * 0.62f, 62f));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.72f * fade),
                rotation + MathHelper.PiOver2, new Vector2(258f, 44f));
        }
        FranVfx.EndAdditive();
        return false;
    }
}

// Every confirmed dash hit opens a delayed rift through the victim.  Its black body is drawn in
// AlphaBlend so it cannot disappear on bright backgrounds; only the violet/cyan edge emission uses
// Additive.  The finisher upgrades the same grammar into two perpendicular rifts.
public sealed class FranUltimateBlackRiftLaser : ModProjectile
{
    private const int Lifetime = 24;
    private const int WindupFrames = 7;
    private const float CollisionHalfLength = 2300f;
    private const float VisualHalfLength = 1050f;

    private int Age => Lifetime - Projectile.timeLeft;
    private bool Final => Projectile.ai[1] > 0.5f;
    private Vector2 Axis => Projectile.ai[0].ToRotationVector2().SafeNormalize(Vector2.UnitY);

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;

    public override bool? CanDamage() => Age >= WindupFrames ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (Age < WindupFrames)
            return false;

        float width = Final ? 72f : 44f;
        if (AxisHits(targetHitbox, Axis, width))
            return true;
        return Final && AxisHits(targetHitbox, Axis.RotatedBy(MathHelper.PiOver2), width * 0.82f);
    }

    private bool AxisHits(Rectangle targetHitbox, Vector2 axis, float width)
    {
        float collisionPoint = 0f;
        Vector2 start = Projectile.Center - axis * CollisionHalfLength;
        Vector2 end = Projectile.Center + axis * CollisionHalfLength;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            start, end, width, ref collisionPoint);
    }

    public override void AI()
    {
        Color corona = Final ? new Color(193, 116, 255) : new Color(132, 72, 214);
        Lighting.AddLight(Projectile.Center, corona.ToVector3() * (Age >= WindupFrames ? 0.36f : 0.12f));
        if (Main.dedServ)
            return;

        if (Projectile.localAI[0] == 0f)
        {
            Projectile.localAI[0] = 1f;
            SpawnTelegraph(corona);
        }

        if (Projectile.localAI[1] == 0f && Age >= WindupFrames)
        {
            Projectile.localAI[1] = 1f;
            SpawnRelease(corona);
        }
    }

    private void SpawnTelegraph(Color corona)
    {
        FranParticleEffects.RingPulse(Projectile.Center, corona, Vector2.One * 10f,
            new Vector2(Final ? 112f : 72f, Final ? 38f : 24f), WindupFrames + 4,
            Axis.ToRotation(), 0.018f, Final ? 0.82f : 0.58f);
        if (Final || (int)Projectile.ai[2] % 3 == 0)
            FranParticleEffects.MysticGlyphs(Projectile.Center, corona, Final ? 6 : 2,
                Final ? 104f : 66f, Final ? 1.08f : 0.72f);
    }

    private void SpawnRelease(Color corona)
    {
        float scale = Final ? 1.72f : 1f;
        FranParticleEffects.BlackLight(Projectile.Center, corona, scale, Final ? 28 : 20);
        FranParticleEffects.RingPulse(Projectile.Center, corona,
            new Vector2(18f, 10f) * scale, new Vector2(184f, 84f) * scale,
            Final ? 28 : 20, Axis.ToRotation(), -0.024f, 0.92f);
        FranParticleEffects.NeedleFan(Projectile.Center, Axis, corona,
            Final ? 18 : 9, Final ? 11.5f : 8f, Final ? 1.30f : 0.88f, 0.34f);
        FranParticleEffects.NeedleFan(Projectile.Center, -Axis, corona,
            Final ? 18 : 9, Final ? 11.5f : 8f, Final ? 1.30f : 0.88f, 0.34f);

        int pathCount = Final ? 18 : 11;
        for (int i = 0; i < pathCount; i++)
        {
            float offset = MathHelper.Lerp(-VisualHalfLength * 0.86f, VisualHalfLength * 0.86f,
                (i + 0.5f) / pathCount);
            Vector2 position = Projectile.Center + Axis * offset +
                Axis.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-16f, 16f) * scale;
            FranParticleSystem.Spawn(new FranBlackLightCoreParticle(position,
                Axis * Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(8f, 14f) * scale,
                Main.rand.NextFloat(32f, 52f) * scale, Main.rand.Next(12, 19),
                Main.rand.NextFloat(0.58f, 0.82f), Axis.ToRotation(), Main.rand.NextFloat(-0.035f, 0.035f)));
            if (i % 2 == 0)
            {
                Vector2 velocity = Axis.RotatedByRandom(0.28f) * Main.rand.NextFloat(2.5f, 7.5f);
                FranParticleSystem.Spawn(new FranBladeEdgeMoteParticle(position, velocity, Axis,
                    Color.Lerp(corona, Color.White, 0.24f), Main.rand.NextFloat(22f, 48f) * scale,
                    Main.rand.NextFloat(1.4f, 2.8f) * scale, Main.rand.Next(10, 17)));
            }
        }

        int customEstimate = 3 + (Final ? 36 : 18) + pathCount + (pathCount + 1) / 2;
        FranVanillaDustEffects.Impact(Projectile.Center, Axis, FranSkill.SkillTaker, corona,
            customEstimate, Final ? 1.38f : 0.98f);
        SoundEngine.PlaySound(SoundID.Item122 with
        {
            Volume = Final ? 0.92f : 0.54f,
            Pitch = Final ? -0.48f : -0.18f + ((int)Projectile.ai[2] % 4) * 0.035f
        }, Projectile.Center);
        if (Final)
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.78f, Pitch = -0.56f }, Projectile.Center);
        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, Final ? 16f : 6f, 2600f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float telegraph = MathHelper.Clamp(Age / (float)WindupFrames, 0f, 1f);
        float activeProgress = MathHelper.Clamp((Age - WindupFrames) /
            (float)Math.Max(1, Lifetime - WindupFrames), 0f, 1f);
        bool active = Age >= WindupFrames;
        float opacity = active
            ? MathF.Pow(1f - activeProgress, 1.12f) * (0.88f + 0.12f * MathF.Sin(activeProgress * MathHelper.Pi))
            : MathHelper.Lerp(0.18f, 0.52f, FranMotion.Smooth(telegraph));
        float snap = active ? 1f : MathHelper.Lerp(0.12f, 0.24f, telegraph);
        float width = (Final ? 84f : 56f) * snap;
        float halfLength = Final ? VisualHalfLength + 90f : VisualHalfLength;

        DrawBlackAxis(Axis, halfLength, width, opacity);
        if (Final)
            DrawBlackAxis(Axis.RotatedBy(MathHelper.PiOver2), halfLength * 0.82f, width * 0.78f, opacity * 0.92f);
        FranVfx.DrawSharedPixels("HighResFoggyCircleHardEdge", Projectile.Center,
            new Color(0, 0, 0, 235) * opacity, Projectile.ai[0] + activeProgress,
            Vector2.One * (Final ? 186f : 122f) * snap);

        FranVfx.BeginAdditive();
        Color violet = Final ? new Color(205, 126, 255) : new Color(137, 77, 225);
        DrawEmissiveAxis(Axis, halfLength, width, violet, opacity, activeProgress);
        if (Final)
            DrawEmissiveAxis(Axis.RotatedBy(MathHelper.PiOver2), halfLength * 0.82f,
                width * 0.78f, new Color(116, 171, 255), opacity * 0.88f, activeProgress);
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            violet * (0.72f * opacity), Projectile.ai[0],
            new Vector2(Final ? 282f : 164f, Final ? 122f : 74f) * snap);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * (0.62f * opacity), Projectile.ai[0],
            new Vector2(Final ? 228f : 118f, Final ? 44f : 24f) * snap);
        FranVfx.EndAdditive();
        return false;
    }

    private void DrawBlackAxis(Vector2 axis, float halfLength, float width, float opacity)
    {
        Vector2 center = Projectile.Center - Main.screenPosition;
        Vector2 start = center - axis * halfLength;
        Vector2 end = center + axis * halfLength;
        DrawLine(start, center, new Color(38, 9, 55, 224) * (opacity * 0.92f), width * 1.34f);
        DrawLine(center, end, new Color(38, 9, 55, 224) * (opacity * 0.92f), width * 1.34f);
        DrawLine(start, center, new Color(0, 0, 0, 246) * opacity, width);
        DrawLine(center, end, new Color(0, 0, 0, 246) * opacity, width);
        DrawLine(start, center, new Color(7, 2, 12, 250) * opacity, width * 0.62f);
        DrawLine(center, end, new Color(7, 2, 12, 250) * opacity, width * 0.62f);
    }

    private void DrawEmissiveAxis(Vector2 axis, float halfLength, float width, Color color,
        float opacity, float activeProgress)
    {
        Vector2 center = Projectile.Center - Main.screenPosition;
        Vector2 start = center - axis * halfLength;
        Vector2 end = center + axis * halfLength;
        Vector2 normal = axis.RotatedBy(MathHelper.PiOver2);
        float edgeOffset = width * 0.56f;
        float edgeWidth = Math.Max(1.5f, width * 0.085f);
        DrawLine(start + normal * edgeOffset, center + normal * edgeOffset,
            color * (0.72f * opacity), edgeWidth);
        DrawLine(center + normal * edgeOffset, end + normal * edgeOffset,
            color * (0.72f * opacity), edgeWidth);
        DrawLine(start - normal * edgeOffset, center - normal * edgeOffset,
            color * (0.72f * opacity), edgeWidth);
        DrawLine(center - normal * edgeOffset, end - normal * edgeOffset,
            color * (0.72f * opacity), edgeWidth);

        for (int i = -2; i <= 2; i++)
        {
            Vector2 point = Projectile.Center + axis * i * 410f;
            float flicker = 0.64f + 0.36f * MathF.Sin(Projectile.identity * 0.73f + i * 1.8f + activeProgress * 18f);
            FranVfx.DrawOriginalPixels("FranSmearForward", point,
                color * (0.18f * opacity * flicker), axis.ToRotation(),
                new Vector2(500f, Math.Max(8f, width * 1.42f)));
        }
    }

    private static void DrawLine(Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 edge = end - start;
        if (edge.LengthSquared() < 0.01f || !float.IsFinite(width))
            return;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, start, new Rectangle(0, 0, 1, 1), color,
            edge.ToRotation(), new Vector2(0f, 0.5f), new Vector2(edge.Length(), Math.Max(0.5f, width)),
            SpriteEffects.None, 0f);
    }
}

public sealed class FranUltimateReadySigil : ModProjectile
{
    private const int Lifetime = 96;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }
        Projectile.Center = owner.Center;
        Lighting.AddLight(owner.Center, new Vector3(0.22f, 0.18f, 0.34f));
        if (Main.dedServ || Projectile.localAI[0]++ != 0f)
            return;

        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.82f, Pitch = 0.12f }, owner.Center);
        for (int i = 0; i < 40; i++)
        {
            Vector2 velocity = (MathHelper.TwoPi * i / 40f).ToRotationVector2() *
                Main.rand.NextFloat(2.8f, 7.4f);
            Dust dust = Dust.NewDustPerfect(owner.Center, i % 5 == 0 ? DustID.GoldFlame : DustID.GemAmethyst,
                velocity, 55, default, Main.rand.NextFloat(0.85f, 1.45f));
            dust.noGravity = true;
        }
        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(owner.Center, 5f, 1800f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Player owner = Main.player[Projectile.owner];
        float age = Lifetime - Projectile.timeLeft;
        float appear = Utils.GetLerpValue(0f, 12f, age, true);
        float disappear = Utils.GetLerpValue(0f, 22f, Projectile.timeLeft, true);
        float opacity = appear * disappear;
        float expansion = FranMotion.EaseOutCubic(Utils.GetLerpValue(0f, Lifetime, age, true));
        float rotation = Main.GlobalTimeWrappedHourly * 1.3f;

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranResonanceOrb", owner.Center,
            new Color(105, 208, 242) * (0.42f * opacity), rotation,
            Vector2.One * (138f + expansion * 34f));
        FranVfx.DrawOriginalPixels("FranImpactBurst", owner.Center,
            new Color(180, 112, 255) * (0.38f * opacity), -rotation,
            Vector2.One * (126f + expansion * 46f));
        for (int i = 0; i < 10; i++)
        {
            float angle = rotation + i * MathHelper.TwoPi / 10f;
            Vector2 point = owner.Center + angle.ToRotationVector2() * (72f + expansion * 28f);
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", point,
                (i % 2 == 0 ? new Color(255, 215, 120) : new Color(112, 220, 255)) *
                (0.68f * opacity), angle, new Vector2(30f, 16f));
        }
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", owner.Center, Color.White * (0.82f * opacity),
            rotation, new Vector2(94f, 48f));
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranUltimateExecutionSeal : ModProjectile
{
    private const int Lifetime = 24;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override bool PreDraw(ref Color lightColor)
    {
        float duration = Math.Max(1f, Projectile.ai[0]);
        float progress = MathHelper.Clamp((Lifetime - Projectile.timeLeft) / duration, 0f, 1f);
        float pulse = 0.55f + 0.45f * MathF.Sin(progress * MathHelper.Pi * 5f);
        float rotation = Projectile.ai[1] + Main.GlobalTimeWrappedHourly * 1.8f;
        FranVfx.BeginAdditive();
        for (int i = 0; i < 8; i++)
        {
            float angle = rotation + i * MathHelper.PiOver4;
            Vector2 point = Projectile.Center + angle.ToRotationVector2() * (86f - progress * 26f);
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", point,
                (i % 2 == 0 ? new Color(255, 215, 120) : new Color(126, 208, 255)) *
                (0.62f * pulse), angle, new Vector2(38f, 18f));
        }
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(178, 105, 255) * (0.38f * pulse), -rotation, Vector2.One * (150f - progress * 34f));
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranUltimateStrikeBeat : ModProjectile
{
    private const int Lifetime = 24;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        bool final = Projectile.ai[0] > 0.5f;
        Color color = final ? new Color(255, 211, 112) : new Color(110, 205, 245);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * (final ? 0.85f : 0.35f));
        if (Main.dedServ || Projectile.localAI[0]++ != 0f)
            return;

        Vector2 direction = Projectile.ai[1].ToRotationVector2();
        int strike = (int)Projectile.ai[2];
        SoundEngine.PlaySound((final ? SoundID.Item122 : SoundID.Item71) with
        {
            Volume = final ? 1f : 0.72f,
            Pitch = final ? -0.34f : -0.12f + strike % 5 * 0.045f
        }, Projectile.Center);

        FranParticleEffects.BloomPulse(Projectile.Center, color, final ? 34f : 16f,
            final ? 246f : 108f, final ? 26 : 18, final ? 1f : 0.86f, true);
        FranParticleEffects.RingPulse(Projectile.Center, color,
            new Vector2(final ? 26f : 14f, final ? 42f : 22f),
            new Vector2(final ? 330f : 142f, final ? 162f : 72f),
            final ? 27 : 18, direction.ToRotation(), 0.018f, 0.94f);
        FranParticleEffects.RingPulse(Projectile.Center,
            final ? new Color(193, 132, 255) : new Color(207, 156, 255),
            Vector2.One * (final ? 28f : 12f),
            new Vector2(final ? 210f : 86f, final ? 318f : 132f),
            final ? 30 : 20, direction.ToRotation() + MathHelper.PiOver2, -0.022f, final ? 0.86f : 0.62f);
        FranParticleEffects.NeedleFan(Projectile.Center, direction, color,
            final ? 28 : 12, final ? 12f : 8.2f, final ? 1.42f : 0.92f, final ? 0.78f : 0.92f);
        FranParticleSystem.Spawn(new FranBladeImpactCrossParticle(Projectile.Center, direction,
            Color.Lerp(color, Color.White, 0.28f), final ? 2.4f : 1.35f, final ? 26 : 17));
        int edgeCount = final ? 18 : 7;
        for (int i = 0; i < edgeCount; i++)
        {
            Vector2 velocity = direction.RotatedByRandom(final ? 1.35f : 0.94f) *
                Main.rand.NextFloat(final ? 4.5f : 3f, final ? 13f : 8f);
            FranParticleSystem.Spawn(new FranBladeEdgeMoteParticle(Projectile.Center, velocity,
                velocity, color, Main.rand.NextFloat(final ? 34f : 22f, final ? 74f : 48f),
                Main.rand.NextFloat(1.5f, final ? 3.6f : 2.7f), Main.rand.Next(final ? 13 : 9, final ? 22 : 15)));
        }

        if (final || strike % 4 == 0)
            FranParticleEffects.MysticGlyphs(Projectile.Center,
                final ? new Color(255, 211, 112) : new Color(126, 220, 255),
                final ? 9 : 3, final ? 132f : 72f, final ? 1.22f : 0.76f,
                -direction * (final ? 1.1f : 0.45f));
        if (strike % 3 == 0)
            FranConstellationEffects.SpawnPolarRelease(Projectile.Center, direction, final ? 1.32f : 0.82f);

        int customParticleEstimate = 3 + (final ? 28 : 12) + 1 + edgeCount +
            (final || strike % 4 == 0 ? final ? 9 : 3 : 0);
        FranVanillaDustEffects.Impact(Projectile.Center, direction, FranSkill.None, color,
            customParticleEstimate, final ? 1.45f : 1.03f);
        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center,
            final ? 24f : MathHelper.Lerp(8.5f, 5.8f, strike / 17f), 2400f);

        if (final)
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.42f }, Projectile.Center);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        bool final = Projectile.ai[0] > 0.5f;
        float progress = 1f - Projectile.timeLeft / (float)Lifetime;
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        float rotation = Projectile.ai[1];
        float radius = MathHelper.Lerp(24f, final ? 238f : 106f, FranMotion.EaseOutCubic(progress));
        Color color = final ? new Color(255, 208, 106) : new Color(105, 211, 245);

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.72f * opacity),
            rotation, new Vector2(radius * 2.1f, radius * (final ? 1.12f : 0.78f)));
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center,
            new Color(188, 122, 255) * (0.48f * opacity), rotation,
            new Vector2(radius * 2.35f, radius * 0.52f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.88f * opacity),
            rotation, new Vector2(radius * 1.48f, radius * 0.66f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            color * (0.62f * opacity), rotation + MathHelper.PiOver2,
            new Vector2(radius * (final ? 1.42f : 0.82f), radius * (final ? 0.54f : 0.32f)));
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranUltimateSpectatorBlade : ModProjectile
{
    private const int Lifetime = 30;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 6;
        Projectile.height = 6;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool? CanDamage() => false;

    public override void AI()
    {
        float progress = 1f - Projectile.timeLeft / (float)Lifetime;
        Projectile.velocity = Projectile.velocity.RotatedBy(Projectile.ai[0] * (1f - progress)) * 0.982f;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.Opacity = MathHelper.Clamp(MathF.Sin(progress * MathHelper.Pi) * 1.45f, 0f, 1f);
        Color color = Projectile.ai[1] > 0.5f ? new Color(255, 210, 115) : new Color(105, 205, 245);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * (0.13f * Projectile.Opacity));
    }

    public override bool PreDraw(ref Color lightColor)
    {
        bool final = Projectile.ai[1] > 0.5f;
        Color color = final ? new Color(255, 210, 115) : new Color(115, 207, 245);
        float scale = MathHelper.Clamp(Projectile.ai[2], 0.65f, 1.5f);
        float opacity = Projectile.Opacity;
        FranVfx.BeginAdditive();
        for (int i = Projectile.oldPos.Length - 1; i >= 0; i -= 2)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                continue;
            float fade = 1f - i / (float)Projectile.oldPos.Length;
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            FranVfx.DrawOriginalPixels("FranSmearForward", point, color * (0.28f * fade * opacity),
                Projectile.oldRot[i], new Vector2(54f * fade, 13f * fade) * scale);
        }
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center, color * (0.84f * opacity),
            Projectile.rotation, new Vector2(final ? 64f : 42f, final ? 18f : 12f) * scale);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.62f * opacity),
            Projectile.rotation, new Vector2(25f, 13f) * scale);
        FranVfx.EndAdditive();
        return false;
    }
}
