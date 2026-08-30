using System;
using System.IO;
using FranMod.Content.Items;
using FranMod.Content.Players;
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
        Intermission,
        FinalPause,
        FinalStrike
    }

    private const int NormalStrikeCount = 12;
    private const int NormalStrikeFrames = 5;
    private const int IntermissionFrames = 2;
    private const int FinalPauseFrames = 10;
    private const int FinalStrikeFrames = 8;
    private const float GoldenAngle = 2.39996323f;
    private const float NormalCollisionWidth = 78f;
    private const float FinalCollisionWidth = 142f;

    private UltimatePhase phase;
    private int phaseTimer;
    private int strikeIndex;
    private int targetNpcIndex = -1;
    private bool initialized;
    private bool impactVisualSpawned;
    private float sequenceRotation;
    private Vector2 originalCenter;
    private Vector2 focusPoint;
    private Vector2 targetPoint;
    private Vector2 strikeStart;
    private Vector2 strikeEnd;
    private Vector2 collisionStart;
    private Vector2 collisionEnd;

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
        writer.Write(sequenceRotation);
        writer.WriteVector2(originalCenter);
        writer.WriteVector2(focusPoint);
        writer.WriteVector2(targetPoint);
        writer.WriteVector2(strikeStart);
        writer.WriteVector2(strikeEnd);
        writer.WriteVector2(collisionStart);
        writer.WriteVector2(collisionEnd);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        phase = (UltimatePhase)reader.ReadByte();
        phaseTimer = reader.ReadInt32();
        strikeIndex = reader.ReadInt32();
        targetNpcIndex = reader.ReadInt32();
        initialized = reader.ReadBoolean();
        impactVisualSpawned = reader.ReadBoolean();
        sequenceRotation = reader.ReadSingle();
        originalCenter = reader.ReadVector2();
        focusPoint = reader.ReadVector2();
        targetPoint = reader.ReadVector2();
        strikeStart = reader.ReadVector2();
        strikeEnd = reader.ReadVector2();
        collisionStart = reader.ReadVector2();
        collisionEnd = reader.ReadVector2();
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

        if (Authority)
            UpdateAuthoritativeSequence(owner);
        else
            UpdateClientPresentation(owner);

        MaintainOwnerState(owner);
        Projectile.Center = owner.Center;
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
        BeginNormalStrike(owner);
    }

    private void UpdateAuthoritativeSequence(Player owner)
    {
        phaseTimer++;
        switch (phase)
        {
            case UltimatePhase.NormalStrike:
                ApplyStrikeMovement(owner, NormalStrikeFrames);
                if (phaseTimer >= NormalStrikeFrames)
                    BeginIntermission();
                break;
            case UltimatePhase.Intermission:
                HoldAt(owner, strikeEnd);
                if (phaseTimer >= IntermissionFrames)
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
            UltimatePhase.NormalStrike => NormalStrikeFrames,
            UltimatePhase.Intermission => IntermissionFrames,
            UltimatePhase.FinalPause => FinalPauseFrames,
            _ => FinalStrikeFrames
        };
        phaseTimer = Math.Min(phaseTimer + 1, maximum);
        if (phase is UltimatePhase.NormalStrike or UltimatePhase.FinalStrike)
            ApplyStrikeMovement(owner, maximum);
        else
            HoldAt(owner, strikeEnd);
    }

    private void BeginNormalStrike(Player owner)
    {
        phase = UltimatePhase.NormalStrike;
        phaseTimer = 0;
        impactVisualSpawned = false;
        targetNpcIndex = FindBestTarget(owner, focusPoint);
        targetPoint = targetNpcIndex >= 0 ? Main.npc[targetNpcIndex].Center : focusPoint;

        float angle = sequenceRotation + strikeIndex * GoldenAngle;
        Vector2 axis = angle.ToRotationVector2();
        float entryRadius = 168f + 24f * MathF.Sin(strikeIndex * 1.31f);
        float exitRadius = 208f + 28f * MathF.Cos(strikeIndex * 0.83f);
        strikeStart = targetPoint + axis * entryRadius;
        strikeEnd = targetPoint - axis * exitRadius;
        collisionStart = strikeStart;
        collisionEnd = strikeStart;
        Projectile.friendly = true;
        ClearLocalNpcImmunity();
        WarpOwner(owner, strikeStart);
        SpawnStrikeSpectators(strikeStart, strikeEnd, false);
        Projectile.netUpdate = true;
    }

    private void BeginIntermission()
    {
        phase = UltimatePhase.Intermission;
        phaseTimer = 0;
        Projectile.friendly = false;
        collisionStart = strikeEnd;
        collisionEnd = strikeEnd;
        Projectile.netUpdate = true;
    }

    private void BeginFinalPause(Player owner)
    {
        phase = UltimatePhase.FinalPause;
        phaseTimer = 0;
        Projectile.friendly = false;
        targetNpcIndex = FindBestTarget(owner, focusPoint);
        targetPoint = targetNpcIndex >= 0 ? Main.npc[targetNpcIndex].Center : focusPoint;
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
        if (targetNpcIndex >= 0 && Main.npc.IndexInRange(targetNpcIndex) &&
            Main.npc[targetNpcIndex].active && Main.npc[targetNpcIndex].CanBeChasedBy())
            targetPoint = Main.npc[targetNpcIndex].Center;

        Vector2 direction = (targetPoint - owner.Center).SafeNormalize(
            (focusPoint - originalCenter).SafeNormalize(Vector2.UnitX * owner.direction));
        strikeStart = targetPoint - direction * 292f;
        strikeEnd = targetPoint + direction * 352f;
        collisionStart = strikeStart;
        collisionEnd = strikeStart;
        Projectile.friendly = true;
        ClearLocalNpcImmunity();
        WarpOwner(owner, strikeStart);
        SpawnStrikeSpectators(strikeStart, strikeEnd, true);
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
    }

    private static void HoldAt(Player owner, Vector2 center)
    {
        owner.Center = center;
        owner.velocity = Vector2.Zero;
    }

    private void MaintainOwnerState(Player owner)
    {
        owner.immune = true;
        owner.immuneTime = Math.Max(owner.immuneTime, 2);
        owner.noKnockback = true;
        owner.fallStart = (int)(owner.position.Y / 16f);
        owner.itemAnimation = 2;
        owner.itemTime = 2;
        owner.heldProj = Projectile.whoAmI;
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

    private void SpawnStrikeSpectators(Vector2 start, Vector2 end, bool final)
    {
        Vector2 direction = (end - start).SafeNormalize(Vector2.UnitX);
        Vector2 midpoint = (start + end) * 0.5f;
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), midpoint, Vector2.Zero,
            ModContent.ProjectileType<FranUltimateStrikeBeat>(), 0, 0f, Projectile.owner,
            final ? 1f : 0f, direction.ToRotation());

        int count = final ? 5 : 2;
        for (int i = 0; i < count; i++)
        {
            float spread = (i - (count - 1f) * 0.5f) * (final ? 0.12f : 0.22f);
            Vector2 velocity = direction.RotatedBy(spread) * (final ? 14f : 10f);
            float curvature = (i % 2 == 0 ? 1f : -1f) * (final ? 0.022f : 0.04f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), start, velocity,
                ModContent.ProjectileType<FranUltimateSpectatorBlade>(), 0, 0f, Projectile.owner,
                curvature, final ? 1f : 0f, 0.86f + i * 0.08f);
        }
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
            modifiers.SourceDamage *= 5f;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (!Authority || impactVisualSpawned)
            return;
        impactVisualSpawned = true;
        Projectile.netUpdate = true;
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
            UltimatePhase.NormalStrike => MathHelper.Clamp(phaseTimer / (float)NormalStrikeFrames, 0f, 1f),
            UltimatePhase.FinalStrike => MathHelper.Clamp(phaseTimer / (float)FinalStrikeFrames, 0f, 1f),
            _ => 1f
        };
        float flash = phase == UltimatePhase.FinalPause
            ? 0.45f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f)
            : MathF.Sin(progress * MathHelper.Pi);
        Color body = FinalStrike ? new Color(255, 218, 126) : new Color(96, 203, 242);
        Color edge = FinalStrike ? Color.White : new Color(207, 159, 255);

        Texture2D teacherTexture = TextureAssets.Item[ModContent.ItemType<Teacher>()].Value;
        float swordRotation = direction.ToRotation() + (strikeIndex % 2 == 0 ? 0.45f : -0.45f);
        Main.EntitySpriteDraw(teacherTexture, owner.Center - Main.screenPosition, null,
            Color.Lerp(lightColor, Color.White, 0.62f), swordRotation, teacherTexture.Size() * 0.5f,
            FinalStrike ? 1.42f : 1.24f, direction.X < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f);

        FranVfx.BeginAdditive();
        if (phase is UltimatePhase.NormalStrike or UltimatePhase.FinalStrike)
        {
            float length = Math.Min(500f, Vector2.Distance(collisionStart, collisionEnd) + 80f);
            Vector2 midpoint = (collisionStart + collisionEnd) * 0.5f;
            FranVfx.DrawOriginalPixels("FranSmearForward", midpoint, body * (0.58f * flash),
                direction.ToRotation(), new Vector2(length, FinalStrike ? 118f : 68f));
            FranVfx.DrawOriginalPixels("FranSmearForward", midpoint, Color.White * (0.52f * flash),
                direction.ToRotation(), new Vector2(length * 0.84f, FinalStrike ? 34f : 20f));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", owner.Center, edge * (0.72f * flash),
                direction.ToRotation(), new Vector2(FinalStrike ? 102f : 62f, FinalStrike ? 48f : 30f));
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
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = 18;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override bool PreDraw(ref Color lightColor)
    {
        float duration = Math.Max(1f, Projectile.ai[0]);
        float progress = MathHelper.Clamp((18f - Projectile.timeLeft) / duration, 0f, 1f);
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
    private const int Lifetime = 20;

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

        SoundEngine.PlaySound((final ? SoundID.Item122 : SoundID.Item71) with
        {
            Volume = final ? 0.92f : 0.46f,
            Pitch = final ? -0.28f : 0.18f
        }, Projectile.Center);
        for (int i = 0; i < (final ? 24 : 8); i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, final ? 9f : 5f);
            Dust dust = Dust.NewDustPerfect(Projectile.Center,
                i % 5 == 0 ? DustID.GoldFlame : DustID.GemAmethyst, velocity, 65, default,
                Main.rand.NextFloat(0.72f, final ? 1.45f : 1.05f));
            dust.noGravity = true;
        }
        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, final ? 14f : 2.4f, 1800f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        bool final = Projectile.ai[0] > 0.5f;
        float progress = 1f - Projectile.timeLeft / (float)Lifetime;
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        float rotation = Projectile.ai[1];
        float radius = MathHelper.Lerp(24f, final ? 160f : 78f, FranMotion.EaseOutCubic(progress));
        Color color = final ? new Color(255, 208, 106) : new Color(105, 211, 245);

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.72f * opacity),
            rotation, new Vector2(radius * 2.1f, radius * (final ? 1.12f : 0.78f)));
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center,
            new Color(188, 122, 255) * (0.48f * opacity), rotation,
            new Vector2(radius * 2.35f, radius * 0.52f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.88f * opacity),
            rotation, new Vector2(radius * 1.48f, radius * 0.66f));
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
