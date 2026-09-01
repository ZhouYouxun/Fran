using System;
using System.IO;
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

// The replacement runtime for Teacher's left-click chain. No player or NPC velocity is written here.
public sealed class TeacherSwingRebuild : ModProjectile
{
    private const int TrailSamples = 72;
    private const int SamplesPerTick = 4;
    private readonly Vector2[] tipHistory = new Vector2[TrailSamples];
    private readonly Vector2[] midHistory = new Vector2[TrailSamples];
    private readonly float[] trailStrength = new float[TrailSamples];
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
    private float visualStrength;
    private int facing;
    private bool initialized;
    private bool attackSoundPlayed;
    private bool peakFlareEmitted;
    private bool imprintReleaseTriggered;
    private bool evolutionSequenceRegistered;
    private float evolutionDamageMultiplier = 1f;
    private int quickdrawBurstStep = -1;

    public override string Texture => $"Terraria/Images/Item_{ItemID.BreakerBlade}";

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
        for (int i = 0; i < tactics.Length; i++)
            writer.Write(FranSkillInfo.IsRealSkill(tactics[i]) ? (byte)tactics[i] : (byte)FranSkill.None);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
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
            _ => progress is >= 0.23f and <= 0.61f
        };
        return active ? null : false;
    }

    public override void AI()
    {
        if (!Owner.active || Owner.dead || Owner.HeldItem.type != ModContent.ItemType<Teacher>())
        {
            Projectile.Kill();
            return;
        }

        Projectile.timeLeft = 2;
        if (!initialized)
            InitializeAttack();

        previousAngle = currentAngle;
        previousReach = currentReach;
        ComputePose(Progress, out currentAngle, out currentReach, out visualStrength);
        currentReach *= Owner.GetModPlayer<FranEvolutionPlayer>().BladeReachMultiplier;
        UpdateTrail();

        Vector2 direction = currentAngle.ToRotationVector2();
        Projectile.Center = Owner.MountedCenter + direction * currentReach * 0.54f;
        Projectile.rotation = currentAngle + MathHelper.PiOver4 + (facing < 0 ? MathHelper.PiOver2 : 0f);
        Owner.heldProj = Projectile.whoAmI;
        Owner.itemTime = Owner.itemAnimation = 2;
        Owner.itemRotation = currentAngle + (facing < 0 ? MathHelper.Pi : 0f);
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            currentAngle - MathHelper.PiOver2);
        Lighting.AddLight(Projectile.Center, ElementColor().ToVector3() * (0.22f + visualStrength * 0.42f));

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
            _ => aim - 0.13f * facing
        };
        previousAngle = currentAngle;
        FranEvolutionPlayer evolution = Owner.GetModPlayer<FranEvolutionPlayer>();
        currentReach = previousReach = (Combo == 3 ? 58f : Combo == 2 ? 126f : 112f) *
            evolution.BladeReachMultiplier;
        evolutionDamageMultiplier = evolution.DirectSwordDamageMultiplier(Combo);
        for (int i = 0; i < TrailSamples; i++)
        {
            tipHistory[i] = Owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach;
            midHistory[i] = Owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach * 0.58f;
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
                reach = 112f + FranMotion.Pulse(progress, 0.12f, 0.24f, 0.62f, 0.83f) * 18f;
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
                reach = 116f + FranMotion.Pulse(progress, 0.08f, 0.19f, 0.84f, 0.96f) * 12f;
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
                float compress = FranMotion.Smooth(FranMotion.Segment(progress, 0f, 0.23f));
                float thrust = FranMotion.EaseOutQuart(FranMotion.Segment(progress, 0.23f, 0.48f));
                float hold = FranMotion.Smooth(FranMotion.Segment(progress, 0.48f, 0.62f));
                float recovery = FranMotion.Smooth(FranMotion.Segment(progress, 0.62f, 1f));
                angle = aim + MathHelper.Lerp(-0.13f, -0.22f, compress) * facing;
                angle = MathHelper.Lerp(angle, aim + 0.025f * facing, thrust);
                angle = MathHelper.Lerp(angle, aim, recovery);
                reach = MathHelper.Lerp(62f, 43f, compress);
                reach = MathHelper.Lerp(reach, 188f, thrust);
                reach = MathHelper.Lerp(reach, 176f, hold);
                reach = MathHelper.Lerp(reach, 84f, recovery);
                strength = FranMotion.Pulse(progress, 0.20f, 0.27f, 0.61f, 0.76f);
                break;
            }
        }
    }

    private void UpdateTrail()
    {
        for (int sample = 1; sample <= SamplesPerTick; sample++)
        {
            float completion = sample / (float)SamplesPerTick;
            float angle = MathHelper.Lerp(previousAngle, currentAngle, completion);
            float reach = MathHelper.Lerp(previousReach, currentReach, completion);
            for (int i = TrailSamples - 1; i > 0; i--)
            {
                tipHistory[i] = tipHistory[i - 1];
                midHistory[i] = midHistory[i - 1];
                trailStrength[i] = trailStrength[i - 1];
            }
            Vector2 direction = angle.ToRotationVector2();
            tipHistory[0] = Owner.MountedCenter + direction * reach;
            midHistory[0] = Owner.MountedCenter + direction * reach * (Combo == 3 ? 0.28f : 0.55f);
            trailStrength[0] = visualStrength;
        }
    }

    private void EmitSwingEffects()
    {
        if (CanDamage() == false || Main.dedServ)
            return;

        Color color = ElementColor();
        Vector2 blade = currentAngle.ToRotationVector2();
        Vector2 tangent = (tipHistory[0] - tipHistory[Math.Min(4, TrailSamples - 1)]).SafeNormalize(blade.RotatedBy(MathHelper.PiOver2));
        Vector2 tip = tipHistory[0];

        EmitQuickdrawAirbursts(tip, blade, tangent);

        bool attackStartedNow = false;
        if (!attackSoundPlayed)
        {
            attackSoundPlayed = true;
            attackStartedNow = true;
            SoundStyle sound = Combo == 2 ? SoundID.Item71 : Combo == 3 ? SoundID.Item1 : SoundID.Item60;
            SoundEngine.PlaySound(sound with { Volume = Combo == 2 ? 0.92f : 0.72f, Pitch = Combo == 2 ? -0.34f : Combo == 3 ? 0.2f : -0.08f }, Owner.Center);
        }

        if (attackStartedNow)
        {
            float releaseIntensity = Combo switch { 2 => 1.12f, 3 => 0.92f, 1 => 0.82f, _ => 0.74f };
            FranParticleSystem.Spawn(new FranBladeTipFlareParticle(tip, tangent, blade,
                Color.Lerp(color, Color.White, 0.18f), releaseIntensity, Combo == 2 ? 14 : 11));
            if (Combo is 2 or 3)
            {
                Vector2 releaseDirection = Combo == 3 ? blade : tangent;
                FranSpectatorVfx.SpawnReleaseFan(Projectile.GetSource_FromThis(), tip - releaseDirection * 5f,
                    releaseDirection, Owner.whoAmI, FranSpectatorVfx.ForSkill(ImprintSkill),
                    Combo == 2 ? 3 : 2, Combo == 2 ? 1.05f : 0.88f);
                Owner.GetModPlayer<FranPlayer>().ShakeScreen(tip, Combo == 2 ? 1.4f : 1f);
            }
        }

        float peakTime = Combo switch { 0 => 0.40f, 1 => 0.48f, 2 => 0.43f, _ => 0.42f };
        if (!peakFlareEmitted && Progress >= peakTime)
        {
            peakFlareEmitted = true;
            float peakIntensity = Combo switch { 2 => 1.36f, 3 => 1.14f, 1 => 1.02f, _ => 1.06f };
            FranParticleSystem.Spawn(new FranBladeTipFlareParticle(tip - blade * (Combo == 3 ? 2f : 6f),
                tangent, blade, Color.Lerp(color, Color.White, 0.36f), peakIntensity, Combo == 2 ? 13 : 10));
        }

        float swingActivity = SwingActivity();
        EmitArcShedLightParticles(blade, tangent, color, swingActivity);
        EmitStructuredTrailParticles(tip, blade, tangent, color, swingActivity);
        EmitAnalyticArcFragments(tip, blade, tangent, color, swingActivity);
        EmitSupportingDust(tip, tangent, color);

        if ((HasTactic(FranSkill.ThunderChain) || HasTactic(FranSkill.ThorsHammer)) &&
            swingActivity > 0.32f && (int)Projectile.localAI[0] % 3 == 0)
        {
            Vector2 electricShedDirection = Combo == 3 ? -blade : tangent;
            FranParticleSystem.Spawn(new FranBoltParticle(tip, electricShedDirection * 2.5f,
                new Color(165, 125, 255),
                Main.rand.NextFloat(0.16f, 0.28f), new Vector2(0.5f, 1.4f), Main.rand.Next(9, 15)));
        }
    }

    private float SwingActivity()
    {
        float activeStart = Combo switch { 0 => 0.16f, 1 => 0.11f, 2 => 0.23f, _ => 0.23f };
        float activeEnd = Combo switch { 0 => 0.63f, 1 => 0.84f, 2 => 0.62f, _ => 0.61f };
        float activeProgress = FranMotion.Segment(Progress, activeStart, activeEnd);
        return MathF.Sin(activeProgress * MathHelper.Pi);
    }

    private void EmitArcShedLightParticles(Vector2 blade, Vector2 tangent, Color color, float swingActivity)
    {
        int timer = (int)Projectile.localAI[0];
        int interval = Combo switch { 2 => 2, 1 => 4, 3 => 3, _ => 3 };
        if (swingActivity <= 0.12f || timer % interval != 0)
            return;

        int maximumHistoryIndex = Math.Min(Combo == 2 ? 11 : 8, TrailSamples - 2);
        int historyIndex = Main.rand.Next(maximumHistoryIndex + 1);
        Vector2 arcPoint = tipHistory[historyIndex];
        Vector2 olderPoint = tipHistory[Math.Min(historyIndex + SamplesPerTick, TrailSamples - 1)];
        Vector2 localTangent = (arcPoint - olderPoint).SafeNormalize(tangent);
        float alongBlade = Combo == 3
            ? Main.rand.NextFloat(0.76f, 1f)
            : Main.rand.NextFloat(0.62f, 0.98f);
        Vector2 position = Vector2.Lerp(Owner.MountedCenter, arcPoint, alongBlade) +
            Main.rand.NextVector2Circular(2.5f, 2.5f);
        Vector2 releaseDirection = Combo == 3 ? blade : localTangent;
        float speedScale = MathHelper.Lerp(0.78f, 1.08f, swingActivity);
        Vector2 velocity = releaseDirection.RotatedByRandom(Combo == 3 ? 0.045f : Combo == 1 ? 0.16f : 0.10f) *
            Main.rand.NextFloat(Combo == 2 ? 4.2f : 3f, Combo == 2 ? 7.2f : 5.8f) * speedScale +
            (Combo == 3 ? Vector2.Zero : blade * Main.rand.NextFloat(0.20f, 0.78f)) +
            (Combo == 3 ? Vector2.Zero : Owner.velocity * 0.10f);
        Color particleColor = timer % 4 == 0 ? Color.White :
            Color.Lerp(color, new Color(255, 220, 132), 0.20f);
        FranParticleSystem.Spawn(new FranLightParticle(position, velocity,
            Main.rand.NextFloat(0.30f, Combo == 2 ? 0.62f : 0.52f), particleColor,
            Main.rand.Next(14, Combo == 2 ? 23 : 21), 0.80f, 1.16f, 2.6f,
            timer % 7 == 0 ? 0.0025f : 0f));

        // Round glow is an accent for the rotational and heavy stages only. The clean cut and
        // thrust keep their silhouettes directional instead of filling the path with bubbles.
        if (Combo is not (1 or 2) || swingActivity < 0.38f)
            return;

        int orbInterval = Combo == 2 ? 6 : 7;
        if (timer % orbInterval != 0)
            return;
        int orbHistoryIndex = Main.rand.Next(maximumHistoryIndex + 1);
        Vector2 orbPoint = tipHistory[orbHistoryIndex];
        Vector2 orbOlderPoint = tipHistory[Math.Min(orbHistoryIndex + SamplesPerTick, TrailSamples - 1)];
        Vector2 orbTangent = (orbPoint - orbOlderPoint).SafeNormalize(tangent);
        Vector2 orbPosition = Vector2.Lerp(Owner.MountedCenter, orbPoint,
            Combo == 3 ? Main.rand.NextFloat(0.78f, 1f) : Main.rand.NextFloat(0.66f, 1f));
        Vector2 orbVelocity = orbTangent.RotatedByRandom(0.18f) * Main.rand.NextFloat(1.8f, 3.8f) +
            blade * Main.rand.NextFloat(0.15f, 0.72f) + Owner.velocity * 0.08f;
        FranParticleSystem.Spawn(new FranGlowOrbParticle(
            orbPosition + Main.rand.NextVector2Circular(3f, 3f), orbVelocity,
            Color.Lerp(color, Color.White, 0.20f), Main.rand.NextFloat(0.14f, 0.23f),
            0.025f, Main.rand.Next(13, 20), true));
    }

    private void EmitSupportingDust(Vector2 tip, Vector2 tangent, Color color)
    {
        int timer = (int)Projectile.localAI[0];
        int interval = Combo switch { 2 => 3, 3 => 5, _ => 4 };
        if (timer % interval != 0)
            return;

        FranVanillaDustEffects.SwingTrail(Owner.MountedCenter, tip, tangent, ImprintSkill,
            color, Combo, timer);
    }

    private void EmitQuickdrawAirbursts(Vector2 tip, Vector2 blade, Vector2 tangent)
    {
        int quickdrawLayers = GetTacticStackCount(FranSkill.PressurizedQuickdraw);
        if (quickdrawLayers <= 0 || Main.myPlayer != Owner.whoAmI)
            return;

        float activeStart = Combo switch { 0 => 0.16f, 1 => 0.11f, 2 => 0.23f, _ => 0.23f };
        float activeEnd = Combo switch { 0 => 0.63f, 1 => 0.84f, 2 => 0.62f, _ => 0.61f };
        int burstCount = Combo switch { 0 => 5, 1 => 6, 2 => 8, _ => 7 };
        float phase = FranMotion.Segment(Progress, activeStart, activeEnd);
        int targetStep = Math.Min(burstCount - 1, (int)(phase * burstCount));

        while (quickdrawBurstStep < targetStep)
        {
            quickdrawBurstStep++;
            int step = quickdrawBurstStep;
            float completion = (step + 0.5f) / burstCount;
            float radial = Combo == 3
                ? MathHelper.Lerp(0.30f, 0.96f, completion)
                : 0.46f + ((step * 0.618034f + Combo * 0.17f) % 1f) * 0.50f;
            float side = (step % 2 == 0 ? -1f : 1f) * (5f + 5f * MathF.Sin(completion * MathHelper.Pi));
            Vector2 position = Vector2.Lerp(Owner.MountedCenter, tip, radial) + tangent * side;
            float scale = (Combo switch
            {
                2 => 0.62f + 0.14f * MathF.Sin(completion * MathHelper.Pi),
                3 => 0.70f + 0.12f * completion,
                _ => 0.66f + 0.16f * MathF.Sin(completion * MathHelper.Pi)
            }) * (1f + 0.10f * (quickdrawLayers - 1));
            float damageScale = Combo switch { 0 => 0.18f, 1 => 0.16f, 2 => 0.13f, _ => 0.15f };
            int burstDamage = Math.Max(1, (int)(Projectile.damage * damageScale *
                (1f + 0.15f * (quickdrawLayers - 1))));
            float delay = 2f + step % 3;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, blade,
                ModContent.ProjectileType<FranCompressionAirburst>(), burstDamage,
                Projectile.knockBack * 0.35f, Owner.whoAmI, delay, scale,
                currentAngle + step * 0.73f);
        }
    }

    private void EmitStructuredTrailParticles(Vector2 tip, Vector2 blade, Vector2 tangent, Color color,
        float swingActivity)
    {
        int timer = (int)Projectile.localAI[0];
        switch (Combo)
        {
            case 0:
                if (timer % 2 != 0)
                    return;
                Vector2 edgePosition = Vector2.Lerp(Owner.MountedCenter, tip, Main.rand.NextFloat(0.68f, 0.98f));
                FranParticleSystem.Spawn(new FranBladeEdgeMoteParticle(edgePosition,
                    tangent.RotatedByRandom(0.14f) * Main.rand.NextFloat(1.8f, 3.5f), tangent,
                    Color.Lerp(color, Color.White, 0.28f), Main.rand.NextFloat(24f, 36f),
                    Main.rand.NextFloat(1.4f, 2.1f), Main.rand.Next(9, 14)));
                if (timer % 4 == 0)
                {
                    Vector2 wakePosition = Vector2.Lerp(Owner.MountedCenter, tip, Main.rand.NextFloat(0.50f, 0.86f));
                    FranParticleSystem.Spawn(new FranBladeWakeSliverParticle(wakePosition,
                        -tangent * Main.rand.NextFloat(0.25f, 0.68f), blade.RotatedByRandom(0.07f),
                        Color.Lerp(color, new Color(195, 242, 255), 0.30f), Main.rand.NextFloat(18f, 29f),
                        Main.rand.NextFloat(2.1f, 3.3f), Main.rand.Next(8, 13), Main.rand.NextFloat(-0.022f, 0.022f)));
                }
                break;

            case 1:
                if (timer % 3 == 0)
                {
                    Vector2 orbitPoint = Vector2.Lerp(Owner.MountedCenter, tip, Main.rand.NextFloat(0.74f, 0.98f));
                    FranParticleSystem.Spawn(new FranBladeEdgeMoteParticle(orbitPoint,
                        tangent * Main.rand.NextFloat(2.2f, 4.4f), tangent, Color.Lerp(color, Color.White, 0.42f),
                        Main.rand.NextFloat(20f, 32f), Main.rand.NextFloat(1.2f, 1.9f), Main.rand.Next(9, 14)));
                    FranParticleSystem.Spawn(new FranStarMoteParticle(orbitPoint, tangent * 0.9f,
                        Color.White, color, Main.rand.NextFloat(0.18f, 0.34f), Main.rand.Next(12, 19)));
                }
                if (timer % 6 == 0)
                    FranParticleSystem.Spawn(new FranBladeWakeSliverParticle(
                        Vector2.Lerp(Owner.MountedCenter, tip, Main.rand.NextFloat(0.58f, 0.82f)),
                        -tangent * 0.45f, tangent.RotatedByRandom(0.10f), color,
                        Main.rand.NextFloat(16f, 25f), Main.rand.NextFloat(1.7f, 2.7f), Main.rand.Next(8, 12),
                        facing * 0.018f));
                break;

            case 2:
                if (swingActivity > 0.24f && timer % 2 == 0)
                {
                    Vector2 heavyPoint = Vector2.Lerp(Owner.MountedCenter, tip, Main.rand.NextFloat(0.55f, 0.98f));
                    FranParticleSystem.Spawn(new FranBladeWakeSliverParticle(heavyPoint,
                        -tangent * Main.rand.NextFloat(0.45f, 1.1f), blade.RotatedByRandom(0.14f),
                        Color.Lerp(color, Color.White, 0.16f), Main.rand.NextFloat(24f, 42f),
                        Main.rand.NextFloat(2.8f, 4.6f), Main.rand.Next(10, 16), Main.rand.NextFloat(-0.035f, 0.035f)));
                    FranParticleSystem.Spawn(new FranBladeChipParticle(tip,
                        tangent.RotatedByRandom(0.36f) * Main.rand.NextFloat(4f, 8.5f), color,
                        Main.rand.NextFloat(0.85f, 1.3f), Main.rand.Next(12, 19), 0.07f,
                        HasTactic(FranSkill.ThunderChain) || HasTactic(FranSkill.ThorsHammer)));
                }
                if (swingActivity > 0.54f && timer % 5 == 0)
                    FranParticleSystem.Spawn(new FranHeavySmokeParticle(tip - blade * 18f,
                        -tangent * Main.rand.NextFloat(0.7f, 1.7f), Color.Lerp(color, new Color(24, 20, 42), 0.62f),
                        Main.rand.NextFloat(0.20f, 0.32f), Main.rand.Next(18, 25), 0.44f, true));
                break;

            default:
                if (swingActivity > 0.16f && timer % 2 == 0)
                {
                    Vector2 thrustPoint = tip - blade * Main.rand.NextFloat(10f, 54f);
                    FranParticleSystem.Spawn(new FranBladeEdgeMoteParticle(thrustPoint,
                        -blade * Main.rand.NextFloat(1.8f, 4.2f), blade,
                        Color.Lerp(color, Color.White, 0.46f), Main.rand.NextFloat(26f, 46f),
                        Main.rand.NextFloat(1.1f, 1.8f), Main.rand.Next(8, 13), 0.91f));
                }
                if (swingActivity > 0.42f && timer % 4 == 0)
                    FranParticleSystem.Spawn(new FranBoltParticle(tip, -blade * 2f, color, 0.18f,
                        new Vector2(0.36f, 1.25f), 9));
                break;
        }
    }

    private void EmitAnalyticArcFragments(Vector2 tip, Vector2 blade, Vector2 tangent, Color color,
        float swingActivity)
    {
        // Only the two broad cuts shed curved pieces of the blade material. The spin already owns
        // circular stars/orbs, while the thrust must remain strictly collinear.
        if (Combo is not (0 or 2) || swingActivity < 0.28f)
            return;

        int timer = (int)Projectile.localAI[0];
        int interval = Combo == 2 ? 3 : 5;
        if (timer % interval != 0)
            return;

        Vector2 center = Vector2.Lerp(Owner.MountedCenter, tip, Main.rand.NextFloat(0.58f, 0.94f));
        Vector2 velocity = tangent.RotatedByRandom(Combo == 2 ? 0.24f : 0.14f) *
            Main.rand.NextFloat(0.8f, Combo == 2 ? 3.6f : 2.5f) - blade * Main.rand.NextFloat(0.1f, 0.7f);
        float arc = (Combo == 2 ? 0.82f : 0.68f) * facing;
        FranParticleSystem.Spawn(new FranBladeArcFragmentParticle(center, velocity, tangent,
            Color.Lerp(color, Color.White, Combo == 2 ? 0.20f : 0.36f),
            Main.rand.NextFloat(Combo == 2 ? 22f : 14f, Combo == 2 ? 36f : 28f), arc,
            Main.rand.NextFloat(1.0f, Combo == 2 ? 2.9f : 2.1f), Main.rand.Next(8, 14),
            facing * Main.rand.NextFloat(0.018f, Combo == 2 ? 0.038f : 0.028f)));
    }

    private void TriggerImprintRelease()
    {
        if (imprintReleaseTriggered || ImprintSkill == FranSkill.None)
            return;
        float triggerTime = Combo switch { 0 => 0.42f, 1 => 0.49f, 2 => 0.45f, _ => 0.43f };
        if (Progress < triggerTime)
            return;
        imprintReleaseTriggered = true;
        for (int skillIndex = 0; skillIndex < FranSkillInfo.Count; skillIndex++)
        {
            FranSkill skill = (FranSkill)skillIndex;
            int layers = GetTacticStackCount(skill);
            if (layers > 0)
                TeacherImprintCombat.TriggerSwing(Owner, Projectile, Combo, skill, layers,
                    currentAngle.ToRotationVector2(), currentReach);
        }
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float collisionPoint = 0f;
        float width = Combo switch
        {
            2 => 46f,
            3 => 24f,
            1 => 38f,
            _ => 32f
        };
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Owner.MountedCenter,
            Owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach, width, ref collisionPoint);
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        modifiers.FinalDamage *= evolutionDamageMultiplier;
        FranEvolutionPlayer evolution = Owner.GetModPlayer<FranEvolutionPlayer>();
        if (evolution.HasNode(FranEvolutionNode.MagicStoneAnalysis) &&
            FranEvolutionBossSystem.IsAnalyzedFor(target, Owner.whoAmI))
            modifiers.ArmorPenetration += evolution.AnalysisArmorPenetration;
        int skillTakerLayers = GetTacticStackCount(FranSkill.SkillTaker);
        if (skillTakerLayers > 0)
            modifiers.FinalDamage *= target.GetGlobalNPC<FranImprintTargetGlobalNPC>()
                .SkillTakerDamageMultiplier(Owner.whoAmI, skillTakerLayers);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
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
        // 主武器真实命中才会触发这一束战术液滴。无附着时保持白色；有附着时使用技能自己的颜色，
        // 颜色与飞行、拖尾、消散粒子一起由 FranTacticalEjecta 负责，调用方不需要再补绘制代码。
        Color ejectaColor = ImprintSkill == FranSkill.None ? Color.White : FranSkillInfo.Color(ImprintSkill);
        int ejectaCount = Combo switch { 2 => 5, 3 => 4, 1 => 4, _ => 3 };
        int ejectaDamage = Math.Max(1, (int)(damageDone * (Combo == 2 ? 0.12f : 0.16f)));
        FranTacticalEjecta.SpawnCone(Projectile.GetSource_OnHit(target),
            target.Center + incoming * 8f, incoming, ejectaCount,
            MathHelper.ToRadians(Combo == 2 ? 30f : 24f), Combo == 2 ? 7.2f : 6.4f,
            Combo == 3 ? 11.8f : 10.4f, ejectaDamage, Projectile.knockBack * 0.22f,
            Owner.whoAmI, ejectaColor);
        FranParticleEffects.MeleeImpact(target.Center, incoming, color, Combo);
        float impactIntensity = Combo switch { 2 => 1.32f, 3 => 1.10f, 1 => 1.02f, _ => 0.92f };
        FranParticleSystem.Spawn(new FranBladeImpactCrossParticle(target.Center, incoming,
            Color.Lerp(color, Color.White, 0.18f), impactIntensity, Combo == 2 ? 17 : 14));
        int edgeCount = Combo == 2 ? 5 : Combo == 3 ? 4 : 3;
        for (int i = 0; i < edgeCount; i++)
        {
            Vector2 velocity = incoming.RotatedByRandom(Combo == 3 ? 0.38f : 0.72f) *
                Main.rand.NextFloat(3.2f, Combo == 2 ? 8.2f : 6.8f);
            FranParticleSystem.Spawn(new FranBladeEdgeMoteParticle(target.Center, velocity, velocity, color,
                Main.rand.NextFloat(18f, Combo == 2 ? 36f : 30f), Main.rand.NextFloat(1.2f, 2f), Main.rand.Next(8, 12)));
        }
        FranVanillaDustEffects.Impact(target.Center, incoming, ImprintSkill, color,
            8 + edgeCount + (Combo == 2 ? 10 : 0), Combo == 2 ? 1.12f : 0.94f);
        if (Combo == 2)
            FranParticleEffects.BladeConstellation(target.Center, color, 0.72f, 10);
        if (Combo == 3)
            FranParticleEffects.LineRelease(target.Center - incoming * 34f, target.Center + incoming * 42f,
                color, 10, 0.8f);
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
                TeacherImprintCombat.OnSwordHit(Owner, Projectile, target, Combo, skill, layers,
                    incoming, damageDone);
        }
        Owner.GetModPlayer<FranPlayer>().ShakeScreen(target.Center, Combo == 2 ? 6.5f : Combo == 3 ? 4.2f : 2.8f);
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
        if (!FranBladeTrailRenderer.TryDrawSlash(Owner.MountedCenter, currentAngle, currentReach, Progress,
                color, visualStrength, Combo, facing))
        {
            FranBladeTrailRenderer.DrawSlashFallback(Owner.MountedCenter, currentAngle, currentReach,
                color, visualStrength, Combo, facing);
        }

        if (visualStrength <= 0.012f)
            return;

        FranVfx.BeginAdditive();
        Vector2 blade = currentAngle.ToRotationVector2();
        Vector2 center = Owner.MountedCenter + blade * currentReach * 0.58f;
        float sign = facing;
        switch (Combo)
        {
            case 0:
                // The first cut stays clean: a small tip corona supports the analytic material arc.
                FranVfx.DrawShared("GlowOrbParticle", tipHistory[0], color * (0.34f * visualStrength),
                    0f, 0.15f);
                FranVfx.DrawShared("HalfStar", tipHistory[0], Color.White * (0.50f * visualStrength),
                    tangentRotation(), 0.56f);
                break;

            case 1:
                // The second cut is a continuous wheel. Two restrained circular smears make the rotation
                // legible while the analytic trail remains the actual blade material.
                FranVfx.DrawShared("CircularSmearSmokey", Owner.MountedCenter,
                    color * (0.30f * visualStrength), currentAngle * 0.58f, 0.76f + visualStrength * 0.10f);
                FranVfx.DrawShared("CircularSmear", Owner.MountedCenter,
                    Color.Lerp(color, Color.White, 0.68f) * (0.20f * visualStrength),
                    -currentAngle * 0.32f, 0.61f + visualStrength * 0.08f);
                FranVfx.DrawShared("HalfStar", tipHistory[0], Color.White * (0.62f * visualStrength),
                    currentAngle, 0.72f);
                break;

            case 2:
                // The third cut is the weight peak: dark material mass, a ragged colored edge and a short hot cleft.
                FranVfx.DrawShared("VerticalSmearRagged", center - blade * 6f,
                    color * (0.36f * visualStrength), currentAngle + MathHelper.PiOver2 + sign * 0.30f,
                    0.72f * (0.78f + visualStrength * 0.28f));
                FranVfx.DrawShared("VerticalSmearThin", center + blade * 8f,
                    Color.White * (0.52f * visualStrength), currentAngle + MathHelper.PiOver2 + sign * 0.16f,
                    0.57f * (0.76f + visualStrength * 0.24f));
                FranVfx.DrawShared("SlashSmear", center + blade * 18f,
                    Color.Lerp(color, Color.White, 0.32f) * (0.24f * visualStrength),
                    currentAngle + MathHelper.PiOver2 - sign * 0.10f, 0.52f);
                FranVfx.DrawShared("HalfStar", tipHistory[0], Color.White * (0.78f * visualStrength),
                    currentAngle, 1.02f);
                break;

            default:
                // The fourth cut resolves the chain as a thrust. Its accents are collinear and capped;
                // no random diagonal streak is allowed to dilute the forward direction.
                Vector2 start = Owner.MountedCenter + blade * 20f;
                Vector2 end = Owner.MountedCenter + blade * currentReach;
                DrawSafeVertical("BloomLineSoftEdge", start, end, color * (0.31f * visualStrength), 26f);
                DrawSafeVertical("BloomLineFade", start, end,
                    Color.Lerp(color, Color.White, 0.68f) * (0.54f * visualStrength), 8.5f);
                DrawSafeVertical("ThinEndedLine", start, end, Color.White * (0.76f * visualStrength), 2.8f);
                FranVfx.DrawShared("ForwardSmear", center + blade * 8f, color * (0.28f * visualStrength),
                    currentAngle, new Vector2(0.44f, 0.105f));
                FranVfx.DrawShared("HalfStar", tipHistory[0], Color.White * (0.72f * visualStrength),
                    currentAngle, 0.80f);
                break;
        }

        // A restrained energy body ties every stage back to the physical sword. The trail changes by stage;
        // the blade itself retains the same colored body and white cutting spine throughout the chain.
        int slices = Combo switch { 2 => 16, 1 => 12, 3 => 10, _ => 10 };
        for (int i = 1; i <= slices; i++)
        {
            float completion = i / (float)slices;
            float taper = MathF.Sin(completion * MathHelper.Pi) * (Combo == 2 ? 1.05f : 0.72f) + 0.16f;
            Vector2 point = Owner.MountedCenter + blade * MathHelper.Lerp(24f, currentReach, completion);
            FranVfx.DrawShared("BloomCircle", point,
                color * (visualStrength * 0.105f * taper), currentAngle + MathHelper.PiOver2,
                new Vector2(0.048f + taper * 0.021f, 0.085f + taper * 0.042f));
            if (i % 4 == 0)
                FranVfx.DrawShared("BloomCircle", point, Color.White * (visualStrength * 0.16f * taper),
                    currentAngle + MathHelper.PiOver2, new Vector2(0.015f, 0.044f));
        }
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
        Texture2D texture = TextureAssets.Item[ItemID.BreakerBlade].Value;
        SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 origin = new(facing < 0 ? texture.Width : 0f, texture.Height);
        float extra = facing < 0 ? MathHelper.PiOver2 : 0f;

        for (int i = 20; i >= 2; i -= 4)
        {
            float fade = 1f - i / 24f;
            Vector2 center = Owner.MountedCenter + (tipHistory[i] - Owner.MountedCenter).SafeNormalize(Vector2.UnitX) * 8f - Main.screenPosition;
            float angle = (tipHistory[i] - Owner.MountedCenter).ToRotation();
            Main.EntitySpriteDraw(texture, center, null, energyColor * (0.10f * fade * trailStrength[i]),
                angle + MathHelper.PiOver4 + extra, origin, 1.05f + (Combo == 2 ? 0.05f : 0f), effects, 0f);
        }

        Vector2 drawPosition = Owner.MountedCenter + currentAngle.ToRotationVector2() * 8f - Main.screenPosition;
        for (int i = 0; i < (Combo == 2 ? 12 : 7); i++)
        {
            Vector2 offset = (MathHelper.TwoPi * i / (Combo == 2 ? 12f : 7f)).ToRotationVector2() *
                (Combo == 2 ? 4.5f : 2.6f) * visualStrength;
            Main.EntitySpriteDraw(texture, drawPosition + offset, null,
                energyColor * (0.16f * visualStrength), Projectile.rotation, origin,
                1.05f + (Combo == 2 ? 0.07f : 0f), effects, 0f);
        }
        Main.EntitySpriteDraw(texture, drawPosition, null, lightColor, Projectile.rotation, origin,
            1.05f + (Combo == 2 ? 0.07f : 0f), effects, 0f);
    }
}

// WeaponOut-style idle presentation: draw Teacher behind the player and never pose the front arm.
internal sealed class TeacherBackDrawPlayer : ModPlayer
{
    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        if (drawInfo.shadow != 0f)
            return;

        Player player = drawInfo.drawPlayer;
        if (!player.active || player.dead || player.HeldItem.type != ModContent.ItemType<Teacher>() ||
            player.itemAnimation > 0 || player.ownedProjectileCounts[ModContent.ProjectileType<TeacherSwingRebuild>()] > 0)
            return;

        bool deployed = player.ownedProjectileCounts[ModContent.ProjectileType<TelekineticCannonProjectile>()] > 0 ||
            player.ownedProjectileCounts[ModContent.ProjectileType<FranTelekineticReturnBlade>()] > 0 ||
            player.ownedProjectileCounts[ModContent.ProjectileType<FloatingSwordArrayProjectile>()] > 0 ||
            player.ownedProjectileCounts[ModContent.ProjectileType<ShipCutterProjectile>()] > 0 ||
            player.ownedProjectileCounts[ModContent.ProjectileType<AirDrawSlashProjectile>()] > 0;
        if (deployed)
            return;

        Texture2D texture = TextureAssets.Item[ItemID.BreakerBlade].Value;
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
        DrawData sword = new(texture, position, null, drawInfo.colorArmorBody, rotation, origin, 1.02f, effects, 0f)
        {
            shader = 0
        };
        drawInfo.DrawDataCache.Insert(0, sword);

        Color goldOutline = new Color(255, 196, 62) * 0.62f;
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * 1.35f;
            DrawData outline = new(texture, position + offset, null, goldOutline,
                rotation, origin, 1.02f, effects, 0f) { shader = 0 };
            drawInfo.DrawDataCache.Insert(0, outline);
        }
    }
}
