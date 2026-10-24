using System;
using System.Collections.Generic;
using System.IO;
using FranMod.Content.Config;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Content.Systems;
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

// The replacement runtime for ShiFu's left-click chain. No player or NPC velocity is written here.
public sealed class ShiFuSwingRebuild : ModProjectile, IShiFuAttack
{
    private const int TrailSamples = 72;
    private const int SamplesPerTick = 4;
    private const int GhostAuraDraws = 20;
    // 从握柄中段到剑尖的源图长度；刀光与攻击长度都以握持点为基准。
    private const float WeaponHiltOffset = 8f;
    // 第四剑持剑起点只额外前移 8 像素，且永不随成长放大。
    private const float FourthWeaponMaximumPush = 8f;
    private readonly Vector2[] tipHistory = new Vector2[TrailSamples];
    private readonly Vector2[] midHistory = new Vector2[TrailSamples];
    private readonly float[] trailStrength = new float[TrailSamples];
    private readonly float[] weaponOffsetHistory = new float[TrailSamples];
    private readonly float[] slashAngles = new float[FranShiFuSlashRenderer.Samples];
    private readonly FranSkill[] tactics =
    [
        FranSkill.None, FranSkill.None, FranSkill.None, FranSkill.None
    ];

    private Player Owner => Main.player[Projectile.owner];
    private int Combo => Math.Clamp((int)Projectile.ai[0], 0, 3);
    private int Duration => Math.Max(12, (int)Projectile.ai[1]);
    private int PackedImprint => Math.Max(0, (int)Projectile.ai[2]);
    private FranSkill ImprintSkill
    {
        get
        {
            for (int i = 0; i < tactics.Length; i++)
                if (FranSkillInfo.IsRealSkill(tactics[i]))
                    return tactics[i];
            int code = PackedImprint & 15;
            return code is >= 1 and <= (int)FranSkill.Count ? (FranSkill)(code - 1) : FranSkill.None;
        }
    }
    private float Progress => MathHelper.Clamp(Projectile.localAI[0] / Duration, 0f, 1f);

    private float currentAngle;
    private float previousAngle;
    private float currentReach;
    private float previousReach;
    private float currentWeaponOffset;
    private float previousWeaponOffset;
    private float visualStrength;
    private int facing;
    private bool initialized;
    private bool attackSoundPlayed;
    private int fourthReleaseStage;
    private int fourthFlowStep;
    private int swingParticleStep;
    private float swingParticleRemainder;
    private float swingParticleSpacing;
    private bool imprintReleaseTriggered;
    private bool evolutionSequenceRegistered;
    private float evolutionDamageMultiplier = 1f;
    private int quickdrawBurstStep = -1;
    private HashSet<int> hammerHitTargets;
    private int skillTakerWispsSpawned;
    private readonly HashSet<int> directHitTargets = new();

    internal int ReserveSkillTakerWisps(int requested)
    {
        int count = Math.Clamp(requested, 0, Math.Max(0,
            FranBalance.SkillTakerMaximumWispsPerSwing - skillTakerWispsSpawned));
        skillTakerWispsSpawned += count;
        return count;
    }

    internal IReadOnlySet<int> HammerHitTargets => hammerHitTargets;
    internal bool HammerHitWindowEnded => Progress > 0.88f;
    // 在主剑乘刀段倍率并取整之前保存；分身用相同基础值均分整轮伤害。
    internal int UnscaledSwordDamage { get; set; }
    int IShiFuAttack.SkillBaseDamage => UnscaledSwordDamage;
    int IShiFuAttack.ReserveSkillTakerWisps(int requested) => ReserveSkillTakerWisps(requested);

    public override string Texture => FranShiFuVisuals.TexturePath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;
    }

    public override void SetDefaults()
    {
        Projectile.width = 96;
        Projectile.height = 96;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.ownerHitCheck = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
        Projectile.netImportant = true;
        Projectile.timeLeft = 2;
    }

    public void SetTactics(ReadOnlySpan<FranSkill> equipped)
    {
        Array.Fill(tactics, FranSkill.None);
        for (int i = 0; i < Math.Min(tactics.Length, equipped.Length); i++)
            if (FranSkillInfo.IsRealSkill(equipped[i]))
                tactics[i] = equipped[i];
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(UnscaledSwordDamage);
        for (int i = 0; i < tactics.Length; i++)
            writer.Write(FranSkillInfo.IsRealSkill(tactics[i]) ? (byte)tactics[i] : (byte)FranSkill.None);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        UnscaledSwordDamage = Math.Max(0, reader.ReadInt32());
        for (int i = 0; i < tactics.Length; i++)
        {
            FranSkill skill = (FranSkill)reader.ReadByte();
            tactics[i] = FranSkillInfo.IsRealSkill(skill) ? skill : FranSkill.None;
        }
    }

    public override bool ShouldUpdatePosition() => false;

    public override bool? CanDamage()
    {
        float progress = Progress;
        bool active = Combo switch
        {
            0 => progress is >= 0.16f and <= 0.63f,
            1 => progress is >= 0.11f and <= 0.84f,
            2 => progress is >= 0.23f and <= 0.62f,
            _ => progress is >= 0.06f and <= 0.88f
        };
        return active ? null : false;
    }

    public override void AI()
    {
        if (!Owner.active || Owner.dead || Owner.HeldItem.type != ModContent.ItemType<ShiFu>())
        {
            Projectile.Kill();
            return;
        }

        Projectile.timeLeft = 2;
        if (!initialized)
            InitializeAttack();

        previousAngle = currentAngle;
        previousReach = currentReach;
        previousWeaponOffset = currentWeaponOffset;
        ComputePose(Progress, out currentAngle, out float poseReach, out visualStrength);
        FranEvolutionPlayer evolution = Owner.GetModPlayer<FranEvolutionPlayer>();
        currentWeaponOffset = Combo == 3 ? FourthWeaponPushOffset(Progress) : 0f;
        float bladeLength = UnifiedBladeLength(poseReach, evolution);
        currentReach = WeaponHiltOffset + currentWeaponOffset + bladeLength;
        UpdateTrail();

        Vector2 direction = currentAngle.ToRotationVector2();
        Projectile.Center = Owner.MountedCenter + direction *
            (WeaponHiltOffset + currentWeaponOffset + bladeLength * 0.54f);
        Projectile.rotation = currentAngle + MathHelper.PiOver4 + (facing < 0 ? MathHelper.PiOver2 : 0f);
        Owner.heldProj = Projectile.whoAmI;
        Owner.itemTime = Owner.itemAnimation = 2;
        Owner.itemRotation = currentAngle + (facing < 0 ? MathHelper.Pi : 0f);
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            currentAngle - MathHelper.PiOver2);
        EmitSwingEffects();
        TriggerImprintRelease();
        Projectile.localAI[0]++;
        if (Projectile.localAI[0] >= Duration)
            Projectile.Kill();
    }

    private void InitializeAttack()
    {
        initialized = true;
        facing = Math.Sign(Projectile.velocity.X);
        if (facing == 0)
            facing = Owner.direction;
        Owner.ChangeDir(facing);
        float aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * facing).ToRotation();
        currentAngle = Combo switch
        {
            0 => aim - 2.08f * facing,
            1 => aim - 1.88f * facing,
            2 => aim - 2.44f * facing,
            _ => aim
        };
        previousAngle = currentAngle;
        FranEvolutionPlayer evolution = Owner.GetModPlayer<FranEvolutionPlayer>();
        float initialPoseReach = Combo is 1 or 3 ? 132f * 1.20f : 132f;
        float initialBladeLength = UnifiedBladeLength(initialPoseReach, evolution);
        currentReach = previousReach = WeaponHiltOffset + initialBladeLength;
        currentWeaponOffset = previousWeaponOffset = 0f;
        evolutionDamageMultiplier = evolution.DirectSwordDamageMultiplier(Combo);
        for (int i = 0; i < TrailSamples; i++)
        {
            tipHistory[i] = Owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach;
            midHistory[i] = Owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach * 0.58f;
            weaponOffsetHistory[i] = 0f;
        }
    }

    private void ComputePose(float progress, out float angle, out float reach, out float strength)
    {
        float aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * facing).ToRotation();
        switch (Combo)
        {
            case 0:
            {
                float startup = FranMotion.Smooth(FranMotion.Segment(progress, 0f, 0.16f));
                float swing = FranMotion.EaseOutQuart(FranMotion.Segment(progress, 0.16f, 0.60f));
                float recovery = FranMotion.Smooth(FranMotion.Segment(progress, 0.60f, 1f));
                float offset = MathHelper.Lerp(-2.08f, -2.23f, startup);
                offset = MathHelper.Lerp(offset, 1.38f, swing);
                offset = MathHelper.Lerp(offset, 1.28f, recovery);
                angle = aim + offset * facing;
                reach = 132f + FranMotion.Pulse(progress, 0.12f, 0.24f, 0.62f, 0.83f) * 29f;
                strength = FranMotion.Pulse(progress, 0.13f, 0.21f, 0.64f, 0.82f);
                break;
            }
            case 1:
            {
                float startup = FranMotion.Smooth(FranMotion.Segment(progress, 0f, 0.11f));
                float spin = FranMotion.EaseOutCubic(FranMotion.Segment(progress, 0.11f, 0.84f));
                float recovery = FranMotion.Smooth(FranMotion.Segment(progress, 0.84f, 1f));
                float offset = MathHelper.Lerp(-1.88f, -2.02f, startup);
                offset = MathHelper.Lerp(offset, -2.02f + MathHelper.TwoPi * 1.06f, spin);
                offset = MathHelper.Lerp(offset, -2.02f + MathHelper.TwoPi * 1.06f + 0.08f, recovery);
                angle = aim + offset * facing;
                reach = (132f + FranMotion.Pulse(progress, 0.08f, 0.19f, 0.84f, 0.96f) * 29f) * 1.20f;
                strength = FranMotion.Pulse(progress, 0.09f, 0.17f, 0.85f, 0.95f);
                break;
            }
            case 2:
            {
                float startup = FranMotion.EaseInCubic(FranMotion.Segment(progress, 0f, 0.23f));
                float swing = FranMotion.EaseOutQuart(FranMotion.Segment(progress, 0.23f, 0.56f));
                float stop = FranMotion.EaseOutBack(FranMotion.Segment(progress, 0.56f, 0.69f));
                float recovery = FranMotion.Smooth(FranMotion.Segment(progress, 0.69f, 1f));
                float offset = MathHelper.Lerp(-2.44f, -2.70f, startup);
                offset = MathHelper.Lerp(offset, 1.60f, swing);
                offset = MathHelper.Lerp(offset, 1.47f, stop);
                offset = MathHelper.Lerp(offset, 1.34f, recovery);
                angle = aim + offset * facing;
                reach = 132f + FranMotion.Pulse(progress, 0.18f, 0.27f, 0.61f, 0.78f) * 29f;
                strength = FranMotion.Pulse(progress, 0.19f, 0.27f, 0.62f, 0.76f);
                break;
            }
            default:
            {
                // 第四剑方向在出刀时锁定，只沿该轴线推出再收回。
                // 1.2x 剑身长度仍随武器范围成长；整把剑的推出距离固定，不随流程或进化放大。
                angle = aim;
                reach = 132f * 1.20f;
                strength = FranMotion.Pulse(progress, 0.03f, 0.10f, 0.84f, 0.94f);
                break;
            }
        }
    }

    private static float FourthWeaponPush(float progress)
    {
        return progress < 0.16f
            ? FranMotion.EaseOutQuart(FranMotion.Segment(progress, 0.02f, 0.16f))
            : 1f - FranMotion.Smooth(FranMotion.Segment(progress, 0.24f, 0.76f));
    }

    private static float FourthWeaponPushOffset(float progress) =>
        FourthWeaponPush(progress) * FourthWeaponMaximumPush;

    private float WeaponDrawScale() =>
        (1.05f + (Combo == 2 ? 0.07f : 0f)) * FranShiFuVisuals.TextureScale;

    private float UnifiedBladeLength(float poseReach, FranEvolutionPlayer evolution)
    {
        // 132 是第一、第三剑的 1x 基准；第二、第四剑现有的 1.2x 以及前三剑的伸展脉冲
        // 已包含在 poseReach 中。阶段尺寸只通过实际贴图缩放进入一次。
        float poseRatio = Math.Max(0.1f, poseReach / 132f);
        // 第二剑的 1.2x 与伸展脉冲会叠成约 1.46x，导致回旋刀光悬在实际剑尖外。
        // 将第二剑整套共用几何收回 30%；剑光和碰撞共同读取该长度，避免二次缩放。
        float visualAlignment = Combo == 1 ? 0.70f : 1f;
        float visibleBladeLength = FranShiFuVisuals.BladeLengthFromGripPixels * WeaponDrawScale();
        float alignedLength = visibleBladeLength * poseRatio *
            evolution.BladeReachMultiplier * visualAlignment;
        // 即使投入额外范围进化，第二剑也不能再次越过固定不变的武器贴图尖端。
        return Combo == 1 ? Math.Min(alignedLength, visibleBladeLength) : alignedLength;
    }

    private void UpdateTrail()
    {
        for (int sample = 1; sample <= SamplesPerTick; sample++)
        {
            float completion = sample / (float)SamplesPerTick;
            float angle = MathHelper.Lerp(previousAngle, currentAngle, completion);
            float reach = MathHelper.Lerp(previousReach, currentReach, completion);
            float weaponOffset = MathHelper.Lerp(previousWeaponOffset, currentWeaponOffset, completion);
            for (int i = TrailSamples - 1; i > 0; i--)
            {
                tipHistory[i] = tipHistory[i - 1];
                midHistory[i] = midHistory[i - 1];
                trailStrength[i] = trailStrength[i - 1];
                weaponOffsetHistory[i] = weaponOffsetHistory[i - 1];
            }
            Vector2 direction = angle.ToRotationVector2();
            tipHistory[0] = Owner.MountedCenter + direction * reach;
            midHistory[0] = Combo == 3
                ? Owner.MountedCenter + direction * (weaponOffset + (reach - weaponOffset) * 0.28f)
                : Owner.MountedCenter + direction * reach * 0.55f;
            trailStrength[0] = visualStrength;
            weaponOffsetHistory[0] = weaponOffset;
        }
    }

    private void EmitSwingEffects()
    {
        if (Main.dedServ)
            return;

        Color color = ElementColor();
        // 基础粒子按剑路/推出进度取样；跨过窗口边界的快速挥砍也能补齐这一段。
        EmitSwingCadenceParticles(color);
        if (CanDamage() == false)
            return;

        Vector2 blade = currentAngle.ToRotationVector2();
        Vector2 tangent = (tipHistory[0] - tipHistory[Math.Min(4, TrailSamples - 1)]).SafeNormalize(blade.RotatedBy(MathHelper.PiOver2));
        Vector2 tip = tipHistory[0];

        EmitQuickdrawAirbursts(tip, blade, tangent);

        if (!attackSoundPlayed)
        {
            attackSoundPlayed = true;
            if (Combo == 3)
                SoundEngine.PlaySound(SoundID.Item105, Owner.Center);
            else
            {
                SoundStyle sound = Combo == 2 ? SoundID.Item71 : SoundID.Item60;
                SoundEngine.PlaySound(sound with { Volume = Combo == 2 ? 0.92f : 0.72f,
                    Pitch = Combo == 2 ? -0.34f : -0.08f }, Owner.Center);
            }
        }

        float swingActivity = SwingActivity();
        if ((HasTactic(FranSkill.ThunderChain) || HasTactic(FranSkill.ThorsHammer)) &&
            swingActivity > 0.32f && (int)Projectile.localAI[0] % 3 == 0)
        {
            Vector2 electricShedDirection = Combo == 3 ? -blade : tangent;
            FranParticleSystem.Spawn(new FranBoltParticle(tip, electricShedDirection * 2.5f,
                new Color(165, 125, 255),
                Main.rand.NextFloat(0.16f, 0.28f), new Vector2(0.5f, 1.4f), Main.rand.Next(9, 15)), spawnSupportingDust: false);
        }
    }

    private float SwingActivity()
    {
        float activeStart = Combo switch { 0 => 0.16f, 1 => 0.11f, 2 => 0.23f, _ => 0.06f };
        float activeEnd = Combo switch { 0 => 0.63f, 1 => 0.84f, 2 => 0.62f, _ => 0.88f };
        float activeProgress = FranMotion.Segment(Progress, activeStart, activeEnd);
        return MathF.Sin(activeProgress * MathHelper.Pi);
    }

    private void EmitSwingCadenceParticles(Color color)
    {
        if (Combo == 3)
        {
            EmitFourthCompressionParticles(currentAngle.ToRotationVector2(), color);
            return;
        }

        float start = Combo switch { 0 => 0.16f, 1 => 0.11f, _ => 0.23f };
        float end = Combo switch { 0 => 0.60f, 1 => 0.84f, _ => 0.56f };
        float from = Math.Max(start, (Projectile.localAI[0] - 1f) / Duration);
        float to = Math.Min(end, Progress);
        if (to <= from)
            return;

        ComputePose(from, out float fromAngle, out _, out _);
        ComputePose(to, out float toAngle, out _, out _);
        float sweep = Math.Abs(toAngle - fromAngle);
        if (sweep <= 0.0001f)
            return;

        // 仍按剑路累计释放量；每次间距小幅浮动，避免粒子沿圆弧等距排队。
        // 余量跨帧保留，高攻速时也从本帧实际扫过的剑路补齐。
        float spacing = Combo == 1 ? 0.24f : 0.22f;
        if (swingParticleSpacing <= 0f)
            swingParticleSpacing = spacing * Main.rand.NextFloat(0.75f, 1.25f);
        float motion = MathHelper.Clamp(sweep / (to - from) / 32f, 0f, 1f);
        float reach = ParticleBladeLength();
        float remaining = sweep;
        while (swingParticleRemainder + remaining >= swingParticleSpacing)
        {
            float travel = swingParticleSpacing - swingParticleRemainder;
            remaining -= travel;
            float distance = sweep - remaining;
            float fraction = distance / sweep;
            Vector2 blade = MathHelper.Lerp(fromAngle, toAngle, fraction).ToRotationVector2();
            Vector2 tangent = blade.RotatedBy(MathHelper.PiOver2 * Math.Sign(toAngle - fromAngle));
            Vector2 origin = Owner.MountedCenter + blade * WeaponHiltOffset;
            EmitBladeParticleSample(origin, blade, tangent, reach, color, motion);
            swingParticleRemainder = 0f;
            swingParticleSpacing = spacing * Main.rand.NextFloat(0.75f, 1.25f);
        }
        swingParticleRemainder += remaining;
    }

    // 与 DrawWeapon 的贴图端点一致，攻击判定的伸展倍率不参与粒子定位。
    private float ParticleBladeLength() => Math.Max(24f,
        FranShiFuVisuals.TextureDiagonal * WeaponDrawScale() - FranShiFuVisuals.GripInset);

    private static float FourthPulseProgress(int stage) => 0.08f + stage * 0.06f;
    private static float FourthPulseAlongBlade(int stage) => 0.56f + stage * 0.20f;

    private void EmitBladeParticleSample(Vector2 origin, Vector2 blade, Vector2 localTangent,
        float reach, Color color, float motion)
    {
        FranShiFuSwingEffects.RotatingShed(origin, blade, localTangent, reach,
            color, Combo, swingParticleStep++, motion, ImprintSkill == FranSkill.BlueResonance);
    }

    private void EmitFourthCompressionParticles(Vector2 blade, Color color)
    {
        float usableReach = ParticleBladeLength();
        // 流光随推出阶段前移；收剑后半段只让已有粒子自然消退。
        while (fourthFlowStep < 12 && Progress >= 0.06f + fourthFlowStep * 0.028f)
        {
            float sampleProgress = 0.06f + fourthFlowStep * 0.028f;
            Vector2 origin = Owner.MountedCenter + blade * (WeaponHiltOffset + FourthWeaponPushOffset(sampleProgress));
            FranShiFuSwingEffects.FinisherFlow(origin, blade, usableReach, color,
                sampleProgress, fourthFlowStep, ImprintSkill == FranSkill.BlueResonance);
            fourthFlowStep++;
        }

        // 用进度跨阈值触发，在最低 12 帧时长和高攻速下也不会漏掉任一段。
        while (fourthReleaseStage < 3 && Progress >= FourthPulseProgress(fourthReleaseStage))
        {
            float releaseProgress = FourthPulseProgress(fourthReleaseStage);
            Vector2 origin = Owner.MountedCenter + blade * (WeaponHiltOffset + FourthWeaponPushOffset(releaseProgress));
            Vector2 center = origin + blade * usableReach * FourthPulseAlongBlade(fourthReleaseStage);
            FranShiFuSwingEffects.FinisherPulse(center, blade, color, fourthReleaseStage,
                ImprintSkill == FranSkill.BlueResonance);
            fourthReleaseStage++;
        }
    }

    private void EmitQuickdrawAirbursts(Vector2 tip, Vector2 blade, Vector2 tangent)
    {
        int quickdrawLayers = GetTacticStackCount(FranSkill.PressurizedQuickdraw);
        if (quickdrawLayers <= 0 || Main.myPlayer != Owner.whoAmI)
            return;

        float activeStart = Combo switch { 0 => 0.16f, 1 => 0.11f, 2 => 0.23f, _ => 0.06f };
        float activeEnd = Combo switch { 0 => 0.63f, 1 => 0.84f, 2 => 0.62f, _ => 0.88f };
        bool fullQuickdrawLoadout = Owner.GetModPlayer<FranPlayer>()
            .IsFullTacticLoadout(FranSkill.PressurizedQuickdraw);
        int burstCount = Math.Max(1, FranBalance.GetQuickdrawAirburstCount(Combo));
        if (!fullQuickdrawLoadout)
        {
            float emissionMultiplier = Math.Max(0f, FranBalance.GetFloat(
                nameof(FranBalance.QuickdrawNonFullEmissionMultiplier),
                FranBalance.QuickdrawNonFullEmissionMultiplier));
            burstCount = Math.Max(1, (int)MathF.Round(burstCount * emissionMultiplier,
                MidpointRounding.AwayFromZero));
        }
        float phase = FranMotion.Segment(Progress, activeStart, activeEnd);
        int targetStep = Math.Min(burstCount - 1, (int)(phase * burstCount));

        while (quickdrawBurstStep < targetStep)
        {
            quickdrawBurstStep++;
            int step = quickdrawBurstStep;
            int cadenceCombo = Combo;
            float completion = (step + 0.5f) / burstCount;
            float radial = Combo == 3
                ? MathHelper.Lerp(0.30f, 0.96f, completion)
                : 0.46f + ((step * 0.618034f + cadenceCombo * 0.17f) % 1f) * 0.50f;
            float side = (step % 2 == 0 ? -1f : 1f) * (5f + 5f * MathF.Sin(completion * MathHelper.Pi));
            Vector2 position = Vector2.Lerp(Owner.MountedCenter, tip, radial) + tangent * side;
            float scale = (cadenceCombo switch
            {
                2 => 0.62f + 0.14f * MathF.Sin(completion * MathHelper.Pi),
                3 => 0.70f + 0.12f * completion,
                _ => 0.66f + 0.16f * MathF.Sin(completion * MathHelper.Pi)
            });
            float damageScale = FranBalance.GetQuickdrawAirburstDamageMultiplier(Combo);
            if (!fullQuickdrawLoadout)
                damageScale *= Math.Max(0f, FranBalance.GetFloat(
                    nameof(FranBalance.QuickdrawNonFullDamageMultiplier),
                    FranBalance.QuickdrawNonFullDamageMultiplier));
            int burstDamage = Math.Max(1, (int)(Projectile.damage * damageScale *
                (1f + FranBalance.GetFloat(nameof(FranBalance.QuickdrawExtraLayerDamageMultiplier),
                    FranBalance.QuickdrawExtraLayerDamageMultiplier) * (quickdrawLayers - 1)) *
                FranTacticOutputBudget.Scale(FranSkill.PressurizedQuickdraw)));
            float delay = 2f + step % 3;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, blade,
                ModContent.ProjectileType<FranCompressionAirburst>(), burstDamage,
                Projectile.knockBack * 0.35f, Owner.whoAmI,
                FranCompressionAirburst.EncodeDelay(delay, fullQuickdrawLoadout), scale,
                currentAngle + step * 0.73f);
        }
    }

    private void TriggerImprintRelease()
    {
        if (imprintReleaseTriggered || ImprintSkill == FranSkill.None)
            return;
        float triggerTime = Combo switch { 0 => 0.42f, 1 => 0.49f, 2 => 0.45f, _ => 0.46f };
        if (Progress < triggerTime)
            return;
        imprintReleaseTriggered = true;
        for (int skillIndex = 0; skillIndex < FranSkillInfo.Count; skillIndex++)
        {
            FranSkill skill = (FranSkill)skillIndex;
            int layers = GetTacticStackCount(skill);
            if (layers > 0)
                ShiFuImprintCombat.TriggerSwing(Owner, Projectile, Combo, skill, layers,
                    currentAngle.ToRotationVector2(), currentReach);
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float width = Combo switch
        {
            2 => 46f,
            3 => 24f,
            1 => 38f,
            _ => 32f
        };
        Vector2 previousTip = Owner.MountedCenter + previousAngle.ToRotationVector2() * previousReach;
        Vector2 currentTip = Owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach;
        float tipTravel = Vector2.Distance(previousTip, currentTip);
        int sweepSamples = Math.Clamp((int)MathF.Ceiling(tipTravel / Math.Max(10f, width * 0.55f)), 1, 24);

        for (int sample = 0; sample <= sweepSamples; sample++)
        {
            float completion = sample / (float)sweepSamples;
            float angle = MathHelper.Lerp(previousAngle, currentAngle, completion);
            float reach = MathHelper.Lerp(previousReach, currentReach, completion);
            float weaponOffset = MathHelper.Lerp(previousWeaponOffset, currentWeaponOffset, completion);
            Vector2 blade = angle.ToRotationVector2();
            Vector2 collisionStart = Owner.MountedCenter + blade * (WeaponHiltOffset + weaponOffset);
            Vector2 collisionEnd = Owner.MountedCenter + blade * reach;
            float collisionPoint = 0f;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    collisionStart, collisionEnd, width, ref collisionPoint))
                return true;
        }

        return false;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        if (FranBalance.IsWormBossSegment(target))
            modifiers.FinalDamage *= Math.Clamp(FranBalance.GetFloat(
                nameof(FranBalance.SwingWormBossDamageMultiplier),
                FranBalance.SwingWormBossDamageMultiplier), 0f, 1f);
        if (!FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh))
        {
            bool bossFight = false;
            foreach (NPC npc in Main.ActiveNPCs)
                if (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type])
                {
                    bossFight = true;
                    break;
                }
            int targetCount = directHitTargets.Count + (directHitTargets.Contains(target.whoAmI) ? 0 : 1);
            modifiers.FinalDamage *= FranBalance.GetSwingCrowdDamageMultiplier(targetCount, bossFight, false);
        }
        modifiers.FinalDamage *= evolutionDamageMultiplier;
        int stolenContactDamage = Owner.GetModPlayer<FranPlayer>().GetStolenContactDamage(
            Owner.GetWeaponDamage(Owner.HeldItem));
        if (stolenContactDamage > 0)
            modifiers.FlatBonusDamage += stolenContactDamage;
        FranEvolutionPlayer evolution = Owner.GetModPlayer<FranEvolutionPlayer>();
        if (evolution.HasNode(FranEvolutionNode.MagicStoneAnalysis) &&
            FranEvolutionBossSystem.IsAnalyzedFor(target, Owner.whoAmI))
            modifiers.ArmorPenetration += evolution.AnalysisArmorPenetration;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        directHitTargets.Add(target.whoAmI);
        if (Combo == 3 && HasTactic(FranSkill.ThorsHammer) && Main.myPlayer == Projectile.owner)
            (hammerHitTargets ??= new HashSet<int>()).Add(target.whoAmI);
        FranEvolutionPlayer evolution = Owner.GetModPlayer<FranEvolutionPlayer>();
        if (!evolutionSequenceRegistered)
        {
            evolutionSequenceRegistered = true;
            evolution.RegisterDirectSwordHit(Combo);
        }
        FranEvolutionBossSystem.RegisterAnalysisHit(target, Owner, Combo, Projectile.identity);
        evolution.ApplyResonantPull(target, tipHistory[0]);

        Color color = ElementColor();
        Vector2 incoming = currentAngle.ToRotationVector2();
        FranParticleEffects.MeleeImpact(target.Center, incoming, color, Combo);
        FranVanillaDustEffects.Impact(target.Center, incoming, ImprintSkill, color,
            Combo == 2 ? 6 : 5, Combo == 2 ? 1.12f : 0.94f);
        if (Combo is 2 or 3)
            FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_OnHit(target), target.Center, incoming,
                Owner.whoAmI, FranSpectatorVfx.ForSkill(ImprintSkill), Combo == 2 ? 5 : 3,
                Combo == 2 ? 1.08f : 0.88f);
        if (HasTactic(FranSkill.ThunderChain) || HasTactic(FranSkill.ThorsHammer))
            FranLightingBoltsSystem.SpawnThunderImpact(target.Center, new Color(155, 125, 255), Combo == 2 ? 0.9f : 0.65f);
        for (int skillIndex = 0; skillIndex < FranSkillInfo.Count; skillIndex++)
        {
            FranSkill skill = (FranSkill)skillIndex;
            int layers = GetTacticStackCount(skill);
            if (layers > 0)
                ShiFuImprintCombat.OnSwordHit(Owner, Projectile, target, Combo, skill, layers,
                    incoming, damageDone);
        }
        Owner.GetModPlayer<FranPlayer>().ShakeScreen(target.Center,
            (Combo == 2 ? 6.5f : Combo == 3 ? 4.2f : 2.8f) * 0.7f);
    }

    private Color ElementColor()
    {
        if (ImprintSkill != FranSkill.None)
            return FranSkillInfo.Color(ImprintSkill);
        return Combo == 2 ? new Color(95, 178, 255) : new Color(115, 222, 255);
    }

    private bool HasTactic(FranSkill skill) => GetTacticStackCount(skill) > 0;

    private int GetTacticStackCount(FranSkill skill)
    {
        int count = 0;
        for (int i = 0; i < tactics.Length; i++)
            if (tactics[i] == skill)
                count++;
        if (count == 0 && ImprintSkill == skill)
            count = 1;
        return count;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Color color = ElementColor();
        DrawAdditiveTrail(color);
        DrawWeapon(lightColor, color);
        return false;
    }

    private void DrawAdditiveTrail(Color color)
    {
        Vector2 blade = currentAngle.ToRotationVector2();
        Vector2 trailPivot = Owner.MountedCenter + blade * (WeaponHiltOffset + currentWeaponOffset);
        // Re-evaluate the actual swing curve; a fixed arc would lead the blade during windup
        // and keep rotating after it has stopped. Clamp the tail to this swing's active interval.
        if (Combo != 3 && visualStrength > 0.012f)
        {
            float start = Combo == 0 ? 0.16f : Combo == 1 ? 0.11f : 0.23f;
            float end = Combo == 0 ? 0.60f : Combo == 1 ? 0.84f : 0.56f;
            float head = MathHelper.Clamp(Progress, start, end);
            float tail = Math.Max(start, head - (Combo == 1 ? 0.24f : 0.20f));
            // Release the retained arc during recovery instead of reversing its direction.
            tail = MathHelper.Lerp(tail, head, FranMotion.Segment(Progress, end, end + 0.19f));
            for (int i = 0; i < slashAngles.Length; i++)
                ComputePose(MathHelper.Lerp(head, tail, i / (float)(slashAngles.Length - 1)),
                    out slashAngles[i], out _, out _);
            FranShiFuSlashRenderer.Draw(trailPivot, slashAngles, ParticleBladeLength(),
                color, visualStrength, Combo);
        }

        if (visualStrength <= 0.012f)
            return;

        FranVfx.BeginAdditive();
        Vector2 particleTip = trailPivot + blade * ParticleBladeLength();
        switch (Combo)
        {
            case 0:
                // A restrained tip accent supports the continuous shader surface.
                Vector2 firstSlashVisualTip = particleTip;
                FranVfx.DrawShared("GlowOrbParticle", firstSlashVisualTip, color * (0.34f * visualStrength),
                    0f, 0.15f);
                FranVfx.DrawShared("HalfStar", firstSlashVisualTip, Color.White * (0.50f * visualStrength),
                    tangentRotation(), 0.56f);
                break;

            case 1:
                FranVfx.DrawShared("HalfStar", particleTip, Color.White * (0.62f * visualStrength),
                    currentAngle, 0.72f);
                break;

            case 2:
                Vector2 thirdSlashVisualTip = particleTip;
                FranVfx.DrawShared("HalfStar", thirdSlashVisualTip, Color.White * (0.78f * visualStrength),
                    tangentRotation(), 1.02f);
                break;

            default:
                // 三次短闪由近至远推进，间隙露出剑身；没有圆弧刀光或同位三重叠绘。
                float compressedReach = ParticleBladeLength();
                for (int stage = 0; stage < 3; stage++)
                {
                    float start = FourthPulseProgress(stage) - 0.02f;
                    float pulse = FranMotion.Pulse(Progress, start, start + 0.02f,
                        start + 0.045f, start + 0.08f);
                    if (pulse <= 0.01f)
                        continue;
                    Vector2 focus = trailPivot + blade * compressedReach * FourthPulseAlongBlade(stage);
                    Color tint = Color.Lerp(color, ImprintSkill == FranSkill.BlueResonance
                        ? FranResonanceVisuals.Cyan : new Color(198, 147, 255), stage * 0.16f);
                    FranVfx.DrawSharedPixels("BloomCircle", focus, tint * (pulse * 0.46f),
                        0f, Vector2.One * (18f + pulse * 14f));
                    FranVfx.DrawSharedPixels("HalfStar", focus, Color.White * (pulse * 0.88f),
                        currentAngle, new Vector2(35f + stage * 5f, 13f));
                }
                break;

        }

        // Disabled by design: the 45-stamp extension read as a long, wavering laser on every swing.
        FranVfx.EndAdditive();

        float tangentRotation() => (tipHistory[0] - tipHistory[Math.Min(4, TrailSamples - 1)])
            .SafeNormalize(blade.RotatedBy(MathHelper.PiOver2)).ToRotation();
    }

    private static void DrawSafeVertical(string textureName, Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 edge = end - start;
        float lengthSquared = edge.LengthSquared();
        if (!float.IsFinite(start.X) || !float.IsFinite(start.Y) ||
            !float.IsFinite(end.X) || !float.IsFinite(end.Y) ||
            !float.IsFinite(lengthSquared) || lengthSquared <= 0.1f || lengthSquared > 256f * 256f)
            return;

        FranVfx.DrawVertical(textureName, start, end, color, MathHelper.Clamp(width, 0.8f, 64f));
    }

    private void DrawWeapon(Color lightColor, Color energyColor)
    {
        Texture2D texture = FranShiFuVisuals.Texture;
        Texture2D ghostTexture = FranShiFuVisuals.GhostTexture;
        Texture2D glowTexture = FranShiFuVisuals.GlowTexture;
        SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 origin = new(facing < 0 ? texture.Width : 0f, texture.Height);
        Vector2 ghostOrigin = new(facing < 0 ? ghostTexture.Width : 0f, ghostTexture.Height);
        Vector2 glowOrigin = new(facing < 0 ? glowTexture.Width : 0f, glowTexture.Height);
        float extra = facing < 0 ? MathHelper.PiOver2 : 0f;

        for (int i = 20; i >= 2; i -= 4)
        {
            float fade = 1f - i / 24f;
            Vector2 center = Owner.MountedCenter +
                (tipHistory[i] - Owner.MountedCenter).SafeNormalize(Vector2.UnitX) *
                (WeaponHiltOffset + weaponOffsetHistory[i]) - Main.screenPosition;
            float angle = (tipHistory[i] - Owner.MountedCenter).ToRotation();
            Main.EntitySpriteDraw(texture, center, null, energyColor * (0.10f * fade * trailStrength[i]),
                angle + MathHelper.PiOver4 + extra, origin,
                (1.05f + (Combo == 2 ? 0.05f : 0f)) * FranShiFuVisuals.TextureScale, effects, 0f);
        }

        Vector2 drawPosition = Owner.MountedCenter + currentAngle.ToRotationVector2() *
            (WeaponHiltOffset + currentWeaponOffset - FranShiFuVisuals.GripInset) - Main.screenPosition;
        float auraRadius = (Combo == 2 ? 6.5f : 5.5f) * visualStrength;
        Color ghostColor = new(energyColor.R, energyColor.G, energyColor.B, 0);
        for (int i = 0; i < GhostAuraDraws; i++)
        {
            Vector2 offset = (MathHelper.TwoPi * i / GhostAuraDraws).ToRotationVector2() * auraRadius;
            Main.EntitySpriteDraw(ghostTexture, drawPosition + offset, null,
                ghostColor * (0.15f * visualStrength), Projectile.rotation, ghostOrigin,
                (1.05f + (Combo == 2 ? 0.07f : 0f)) * FranShiFuVisuals.TextureScaleFor(ghostTexture), effects, 0f);
        }
        Main.EntitySpriteDraw(texture, drawPosition, null, lightColor, Projectile.rotation, origin,
            (1.05f + (Combo == 2 ? 0.07f : 0f)) * FranShiFuVisuals.TextureScale, effects, 0f);
        Main.EntitySpriteDraw(glowTexture, drawPosition, null,
            Color.White * (0.72f * visualStrength), Projectile.rotation, glowOrigin,
            (1.05f + (Combo == 2 ? 0.07f : 0f)) * FranShiFuVisuals.TextureScaleFor(glowTexture), effects, 0f);
    }
}

// WeaponOut-style idle presentation: draw ShiFu behind the player and never pose the front arm.
internal sealed class ShiFuBackDrawPlayer : ModPlayer
{
    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        if (drawInfo.shadow != 0f)
            return;

        Player player = drawInfo.drawPlayer;
        if (!ModContent.GetInstance<FranClientConfig>().CarryShiFuOnBack)
            return;
        if (!player.active || player.dead || player.HeldItem.type != ModContent.ItemType<ShiFu>() ||
            player.itemAnimation > 0 || player.ownedProjectileCounts[ModContent.ProjectileType<ShiFuSwingRebuild>()] > 0)
            return;

        bool deployed = player.ownedProjectileCounts[ModContent.ProjectileType<TelekineticCannonProjectile>()] > 0 ||
            player.ownedProjectileCounts[ModContent.ProjectileType<FranTelekineticReturnBlade>()] > 0 ||
            player.ownedProjectileCounts[ModContent.ProjectileType<FloatingSwordArrayProjectile>()] > 0 ||
            player.ownedProjectileCounts[ModContent.ProjectileType<ShipCutterProjectile>()] > 0 ||
            player.ownedProjectileCounts[ModContent.ProjectileType<AirDrawSlashProjectile>()] > 0;
        if (deployed)
            return;

        Texture2D texture = FranShiFuVisuals.Texture;
        Texture2D glowTexture = FranShiFuVisuals.GlowTexture;
        int facing = player.direction;
        SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        // The sword is mounted through its mass centre. Using a bottom corner here pins the blade's
        // end to Fran and makes the full texture dangle far below her even though the angle is right.
        Vector2 origin = texture.Size() * 0.5f;
        Vector2 position = player.MountedCenter - Main.screenPosition +
            new Vector2(-facing * 5f, -4f * player.gravDir + player.gfxOffY);
        float bladeAngle = facing > 0 ? MathHelper.Pi * 0.75f : MathHelper.Pi * 0.25f;
        float rotation = bladeAngle + MathHelper.PiOver4 + (facing < 0 ? MathHelper.PiOver2 : 0f);

        // Insert the normally lit sword first, then place the outline draws before it in the cache.
        // This keeps the gold strictly behind the sprite instead of tinting the whole blade like the old cyan ghost copies.
        float textureScale = 1.02f * FranShiFuVisuals.TextureScale;
        DrawData sword = new(texture, position, null, drawInfo.colorArmorBody, rotation, origin, textureScale, effects, 0f)
        {
            shader = 0
        };
        drawInfo.DrawDataCache.Insert(0, sword);

        // Match the centred back-mounted sword, not the corner-anchored swing rendering.
        Vector2 glowOrigin = glowTexture.Size() * 0.5f;
        DrawData glow = new(glowTexture, position, null,
            Color.White * 0.72f, rotation, glowOrigin,
            1.02f * FranShiFuVisuals.TextureScaleFor(glowTexture), effects, 0f) { shader = 0 };
        // Overlay the sword while keeping both entries behind the player's existing draw data.
        drawInfo.DrawDataCache.Insert(1, glow);

        Color goldOutline = new Color(255, 196, 62) * 0.62f;
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * 1.35f;
            DrawData outline = new(texture, position + offset, null, goldOutline,
                rotation, origin, textureScale, effects, 0f) { shader = 0 };
            drawInfo.DrawDataCache.Insert(0, outline);
        }
    }
}
