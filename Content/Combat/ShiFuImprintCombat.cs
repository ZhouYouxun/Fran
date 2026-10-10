using System;
using System.Collections.Generic;
using FranMod.Content.Companions;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Effects.LeonidStarlight;
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


internal static partial class ShiFuImprintCombat
{
    private static Vector2 AttackOrigin(Player owner, Projectile attack) =>
        attack.ModProjectile is FranSpinningDisc ? attack.Center : owner.MountedCenter;
    private static int SkillSourceDamage(Projectile attack, int combo) =>
        attack.ModProjectile is FranSpinningDisc disc ? disc.SkillDamageForBeat(combo) : attack.damage;

    public static void TriggerSwing(Player owner, Projectile swing, int combo, FranSkill skill,
        int tacticLayers, Vector2 direction, float reach, NPC discHitTarget = null)
    {
        if (Main.myPlayer != owner.whoAmI)
            return;
        tacticLayers = Math.Clamp(tacticLayers, 1, FranPlayer.MaximumTacticSlots);

        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        if (swing.ModProjectile is not FranSpinningDisc)
            FranVanillaDustEffects.SkillRelease(AttackOrigin(owner, swing) + direction.SafeNormalize(Vector2.UnitX * owner.direction) * 26f,
                direction, skill, FranSkillInfo.Color(skill), 7 + combo * 2,
                combo == 3 ? 1.12f : 0.92f);
        switch (skill)
        {
            case FranSkill.TelekineticCatapult:
                if (swing.ModProjectile is FranSpinningDisc)
                {
                    SpawnDiscTelekineticCores(owner, swing, combo, tacticLayers);
                    break;
                }
                Vector2 telekineticTarget = ClampTarget(owner, FranCompanionContext.Aim(owner), 2400f);
                SpawnTelekineticStagePattern(owner, swing, telekineticTarget, combo, tacticLayers);
                break;

            case FranSkill.MultipleBodyCreation:
                SpawnMultipleBodies(owner, swing, direction, tacticLayers, combo,
                    fran.IsFullTacticLoadout(FranSkill.MultipleBodyCreation));
                break;

            case FranSkill.ThunderChain:
                SpawnThunderCircuit(owner, swing, combo, tacticLayers, direction,
                    fran.IsFullTacticLoadout(FranSkill.ThunderChain), discHitTarget);
                break;

            case FranSkill.BlueResonance:
                SpawnButterflyResonanceStarlight(AttackOrigin(owner, swing), owner.direction, direction, reach);
                if (fran.IsFullTacticLoadout(FranSkill.BlueResonance))
                {
                    // 全选蓝辉不再要求剑体先命中目标：每次挥刀固定生成一整组蓝辉弹。
                    // 后续若剑体实际命中一个或多个敌人，OnSwordHit 仍会按每个命中目标追加同等的一组。
                    SpawnBlueResonanceEchoes(owner, swing, combo, tacticLayers,
                        AttackOrigin(owner, swing) + direction.SafeNormalize(Vector2.UnitX * owner.direction) * 36f,
                        swing.GetSource_FromThis(), encodedTarget: 0f);
                }
                break;

            case FranSkill.ThorsHammer when combo == 3 && fran.AdvanceHammerRound(tacticLayers):
                if (swing.ModProjectile is FranSpinningDisc disc && !disc.AllowHammerDrop())
                    break;
                Vector2 hammerTarget = owner.Center + new Vector2(Main.rand.NextFloat(-150f, 150f),
                    Main.rand.NextFloat(-260f, -170f));
                Projectile.NewProjectile(swing.GetSource_FromThis(), hammerTarget, Vector2.Zero,
                    ModContent.ProjectileType<ThorsHammerMarker>(), Math.Max(1, (int)(SkillSourceDamage(swing, combo) *
                        FranBalance.GetFloat(nameof(FranBalance.ThorsHammerDropDamageMultiplier),
                            FranBalance.ThorsHammerDropDamageMultiplier) *
                        (1f + FranBalance.GetFloat(nameof(FranBalance.ThorsHammerExtraLayerDamageMultiplier),
                            FranBalance.ThorsHammerExtraLayerDamageMultiplier) * (tacticLayers - 1)) * FranTacticOutputBudget.Scale(FranSkill.ThorsHammer))),
                    7f, owner.whoAmI, 0f, tacticLayers);
                SoundEngine.PlaySound(FranCombatSounds.ThorsHammerAppearance, hammerTarget);
                break;
        }
    }

    public static void OnSwordHit(Player owner, Projectile swing, NPC target, int combo, FranSkill skill,
        int tacticLayers, Vector2 direction, int damageDone, bool firstDiscHit = true)
    {
        if (skill == FranSkill.None)
            return;
        tacticLayers = Math.Clamp(tacticLayers, 1, FranPlayer.MaximumTacticSlots);

        FranImprintTargetGlobalNPC imprint = target.GetGlobalNPC<FranImprintTargetGlobalNPC>();
        bool discHit = swing.ModProjectile is FranSpinningDisc;
        Vector2 contact = swing.ModProjectile is FranSpinningDisc disc ? disc.ContactPoint(target) : target.Center;
        if (skill == FranSkill.SkillTaker)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient && Main.myPlayer != owner.whoAmI)
                return;
            float markStrength = imprint.ApplySkillTakerMark(owner.whoAmI, tacticLayers,
                FranCompanionContext.IsActor(owner) ? FranCompanionContext.Current.SummonId : 0);
            owner.GetModPlayer<FranPlayer>().AddStolenContactDamage(target.damage);
            if (!Main.dedServ)
            {
                for (int i = 0; i < (discHit ? 2 : 4); i++)
                {
                    Vector2 velocity = Main.rand.NextVector2Circular(3.8f, 3.8f) - Vector2.UnitY * 1.2f;
                    FranParticleSystem.Spawn(new FranSkillGlyphParticle(contact, velocity,
                        new Color(210, 95, 255), (discHit ? 0.46f : 0.72f) + markStrength * 0.12f,
                        discHit ? Main.rand.Next(10, 16) : Main.rand.Next(18, 26)));
                }
                if (markStrength >= 0.995f)
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.36f, Pitch = 0.34f }, target.Center);
            }
            if (Main.myPlayer == owner.whoAmI)
            {
                FranBurst.Spawn(swing.GetSource_OnHit(target), contact, owner.whoAmI,
                    FranBurstStyle.SkillTaker, (discHit ? 0.42f : 0.76f) + markStrength * 0.08f, rotation: direction.ToRotation());
                float launchAngle = direction.SafeNormalize(Vector2.UnitX).ToRotation();
                int wispCount = FranBalance.GetInt(nameof(FranBalance.SkillTakerBaseWispCount),
                    FranBalance.SkillTakerBaseWispCount);
                if (swing.ModProjectile is FranSpinningDisc wispDisc)
                    wispCount = firstDiscHit ? DiscBudgetCount(wispCount, wispDisc.SkillCycle, combo) : 0;
                wispCount = swing.ModProjectile is IShiFuAttack attack
                    ? attack.ReserveSkillTakerWisps(wispCount) : 0;
                for (int i = 0; i < wispCount; i++)
                {
                    // Positive rotation is clockwise in Terraria's screen coordinates. Every wisp
                    // therefore leaves in one evenly spaced clockwise formation with no random
                    // phase, jitter, speed, or alternating turn sign.
                    float angle = launchAngle + i * MathHelper.TwoPi / wispCount;
                    Vector2 velocity = angle.ToRotationVector2() * 12.5f;
                    Projectile.NewProjectile(swing.GetSource_OnHit(target), contact, velocity,
                        ModContent.ProjectileType<FranSkillTakerWisp>(),
                        Math.Max(1, (int)(SkillSourceDamage(swing, combo) *
                            FranBalance.GetFloat(nameof(FranBalance.SkillTakerWispDamageMultiplier),
                                FranBalance.SkillTakerWispDamageMultiplier) * FranTacticOutputBudget.Scale(FranSkill.SkillTaker))),
                        swing.knockBack * 0.18f, owner.whoAmI, -1f, markStrength);
                }
            }
            return;
        }
        if (Main.myPlayer != owner.whoAmI)
            return;

        switch (skill)
        {
            case FranSkill.BlueResonance:
                if (swing.ModProjectile is FranSpinningDisc echoDisc)
                {
                    if (!firstDiscHit) break;
                    int count = DiscButterflyCount(BlueEchoCount(combo, tacticLayers), echoDisc.SkillCycle, combo);
                    SpawnBlueResonanceEchoes(owner, swing, combo, tacticLayers, target.Center,
                        swing.GetSource_OnHit(target), target.whoAmI + 1f,
                        damageOverride: echoDisc.SkillDamageForBeat(combo), emissionLimit: count);
                    break;
                }
                SpawnBlueResonanceEchoes(owner, swing, combo, tacticLayers, target.Center,
                    swing.GetSource_OnHit(target), target.whoAmI + 1f);
                break;
        }
    }

    private static int BlueEchoCount(int combo, int tacticLayers) => (combo == 3
            ? FranBalance.GetInt(nameof(FranBalance.BlueResonanceFinisherBaseCount),
                FranBalance.BlueResonanceFinisherBaseCount)
            : FranBalance.GetInt(nameof(FranBalance.BlueResonanceNormalBaseCount),
                FranBalance.BlueResonanceNormalBaseCount)) +
            (tacticLayers - 1) * FranBalance.GetInt(nameof(FranBalance.BlueResonanceExtraLayerCount),
                FranBalance.BlueResonanceExtraLayerCount);

    private static void SpawnBlueResonanceEchoes(Player owner, Projectile swing, int combo,
        int tacticLayers, Vector2 center, IEntitySource source, float encodedTarget,
        int singleIndex = -1, int damageOverride = 0, int emissionLimit = -1)
    {
        int echoCount = BlueEchoCount(combo, tacticLayers);
        float phase = Main.rand.NextFloat(MathHelper.TwoPi);
        float minimumRadius = Math.Max(12f, FranBalance.GetFloat(
            nameof(FranBalance.BlueResonanceSpawnRadiusMinimum), FranBalance.BlueResonanceSpawnRadiusMinimum));
        float maximumRadius = Math.Max(minimumRadius, FranBalance.GetFloat(
            nameof(FranBalance.BlueResonanceSpawnRadiusMaximum), FranBalance.BlueResonanceSpawnRadiusMaximum));
        float minimumDrift = Math.Max(0f, FranBalance.GetFloat(
            nameof(FranBalance.BlueResonanceInitialDriftSpeedMinimum),
            FranBalance.BlueResonanceInitialDriftSpeedMinimum));
        float maximumDrift = Math.Max(minimumDrift, FranBalance.GetFloat(
            nameof(FranBalance.BlueResonanceInitialDriftSpeedMaximum),
            FranBalance.BlueResonanceInitialDriftSpeedMaximum));
        for (int i = Math.Max(0, singleIndex); i < (singleIndex < 0
            ? (emissionLimit < 0 ? echoCount : Math.Min(echoCount, emissionLimit)) : singleIndex + 1); i++)
        {
            // A golden-angle spread keeps large groups separated while the jitter prevents each
            // swing from producing the same regular ring.
            float angle = phase + i * 2.39996323f + Main.rand.NextFloat(-0.48f, 0.48f);
            Vector2 radial = angle.ToRotationVector2();
            Vector2 spawnPosition = center + radial * Main.rand.NextFloat(minimumRadius, maximumRadius);
            // The echoes materialize around the center and barely drift before committing to a target.
            Vector2 driftDirection = radial.RotatedBy(Main.rand.NextFloat(-MathHelper.Pi, MathHelper.Pi));
            Vector2 initialVelocity = driftDirection * Main.rand.NextFloat(minimumDrift, maximumDrift);
            bool discEmission = swing.ModProjectile is FranSpinningDisc;
            if (swing.ModProjectile is FranSpinningDisc disc)
            {
                radial = (swing.rotation + (singleIndex < 0 ? i * 0.22f : 0f)).ToRotationVector2();
                spawnPosition = swing.Center + radial * disc.Radius * 0.88f;
                // Disc echoes start with a visible radial scatter, then retain the
                // existing healing/enemy homing once their opening phase ends.
                initialVelocity = radial * Main.rand.NextFloat(5.5f, 7.5f) +
                    radial.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1.2f, 1.2f);
            }
            Projectile.NewProjectile(source, spawnPosition, initialVelocity,
                ModContent.ProjectileType<FranBlueResonanceEcho>(),
                Math.Max(1, (int)((damageOverride > 0 ? damageOverride : swing.damage) * (combo == 3
                    ? FranBalance.GetFloat(nameof(FranBalance.BlueResonanceFinisherDamageMultiplier),
                        FranBalance.BlueResonanceFinisherDamageMultiplier)
                    : FranBalance.GetFloat(nameof(FranBalance.BlueResonanceNormalDamageMultiplier),
                        FranBalance.BlueResonanceNormalDamageMultiplier)) * FranTacticOutputBudget.Scale(FranSkill.BlueResonance))),
                swing.knockBack * 0.3f, owner.whoAmI, encodedTarget,
                i, FranBlueResonanceEcho.EncodeEchoCount(echoCount) * (discEmission ? -1f : 1f));
        }
    }

    private static void SpawnDiscTelekineticCores(Player owner, Projectile disc, int combo, int layers)
    {
        if (disc.ModProjectile is not FranSpinningDisc cutter) return;
        int stage = FranBalance.GetTelekineticPatternStage();
        int leftCount = LeftTelekineticProjectileCount(owner, combo, layers);
        int count = DiscBudgetCount(leftCount, cutter.SkillCycle, combo);
        // Convert the left-click projectile pool into cores, keeping four fifths of
        // its count and budget over five cycles. Waves and planets stay left-only.
        List<FranTelekineticShot> reference = new(16);
        FranTelekineticPattern.Fill(reference, stage, combo, layers, owner.direction);
        float weight = 0f;
        foreach (FranTelekineticShot shot in reference)
            weight += shot.Weight * shot.Count * FranTelekineticPattern.GetHitWeight(shot.Kind);
        float perCore = weight * FranTelekineticPattern.GetDamageScale(stage, FranBalance.GetAvailableTacticSlots()) /
            Math.Max(1, leftCount);
        float phase = Main.rand.NextFloat(MathHelper.TwoPi);
        for (int i = 0; i < count; i++)
        {
            float angle = phase + MathHelper.TwoPi * (i + Main.rand.NextFloat(-0.32f, 0.32f)) / count;
            SpawnTelekineticPressureCore(owner, disc, angle.ToRotationVector2(), 0f, perCore,
                damageOverride: cutter.SkillDamageForBeat(combo));
        }
        FranParticleEffects.BloomPulse(disc.Center, new Color(70, 165, 255), 12f, 50f, 10, 0.55f, true);
    }

    private static void SpawnTelekineticStagePattern(Player owner, Projectile swing, Vector2 target,
        int combo, int tacticLayers)
    {
        Vector2 direction = (target - owner.MountedCenter).SafeNormalize(Vector2.UnitX * owner.direction);
        int stage = FranBalance.GetTelekineticPatternStage();
        float damageScale = FranTelekineticPattern.GetDamageScale(stage, FranBalance.GetAvailableTacticSlots());
        List<FranTelekineticShot> shots = new(16);
        FranTelekineticPattern.Fill(shots, stage, combo, tacticLayers, owner.direction);
        bool waveCarriesShots = stage >= 2;
        if (waveCarriesShots)
        {
            int waveIndex = Projectile.NewProjectile(swing.GetSource_FromThis(), owner.MountedCenter + direction * 28f,
                direction * Math.Max(1f, FranBalance.GetFloat(nameof(FranBalance.TelekineticFoldBladeLaunchSpeed),
                    FranBalance.TelekineticFoldBladeLaunchSpeed)),
                ModContent.ProjectileType<Telekinetic_DestructiveWave>(), swing.damage,
                swing.knockBack, owner.whoAmI, owner.direction, 0f, 1f);
            if (Main.projectile.IndexInRange(waveIndex) &&
                Main.projectile[waveIndex].ModProjectile is Telekinetic_DestructiveWave wave)
                wave.EnablePatternEmission(stage, combo, tacticLayers, owner.direction);
        }
        foreach (FranTelekineticShot shot in shots)
        {
            float multiplier = shot.Weight * damageScale;
            switch (shot.Kind)
            {
                case FranTelekineticShotKind.Core:
                    if (!waveCarriesShots)
                        SpawnTelekineticPressureCore(owner, swing, direction, shot.Offset, multiplier,
                            visualScale: shot.Scale);
                    break;
                case FranTelekineticShotKind.Wave:
                    if (!waveCarriesShots)
                        SpawnTelekineticFoldBlade(owner, swing, direction, shot.Offset, shot.Angle, shot.Scale, multiplier);
                    break;
                case FranTelekineticShotKind.Planet:
                    // 配方预算仍按原来的单轮权重计算：两轮触发一次、触发时伤害翻倍，
                    // 既保持长期平均输出，也不改右键由同一配方换算出的念核伤害。
                    if (owner.GetModPlayer<FranPlayer>().AdvanceTelekineticPlanetFinisher())
                        SpawnTelekineticPlanetChain(owner, swing, target, multiplier * Math.Max(0f,
                            FranBalance.GetFloat(nameof(FranBalance.TelekineticFinisherPlanetReleaseDamageMultiplier),
                                FranBalance.TelekineticFinisherPlanetReleaseDamageMultiplier)), shot.Count);
                    break;
            }
        }
        Vector2 muzzle = owner.MountedCenter + direction * 28f;
        float releaseScale = (combo switch { 0 => 0.72f, 1 => 0.88f, 2 => 1.08f, _ => 1.42f }) *
            (1f + stage * 0.035f);
        FranSpectatorVfx.SpawnReleaseFan(swing.GetSource_FromThis(), muzzle, direction,
            owner.whoAmI, FranSpectatorPalette.Cyan, Math.Min(5, combo + 2), releaseScale);
        FranParticleEffects.LineRelease(muzzle - direction * 20f,
            muzzle + direction * (combo == 3 ? 104f : 62f), new Color(175, 230, 255),
            combo == 3 ? 12 : 5 + combo * 2, releaseScale);

        if (combo == 3)
        {
            FranParticleEffects.BloomPulse(muzzle, new Color(70, 165, 255), 16f, 112f,
                13, 0.92f, true);
            FranParticleEffects.RingPulse(muzzle, new Color(185, 235, 255),
                new Vector2(14f, 34f), new Vector2(112f, 18f), 14,
                direction.ToRotation(), 0f, 0.92f);
        }

    }

    internal static void SpawnTelekineticPressureCore(Player owner, Projectile swing, Vector2 direction, float lateralOffset, float damageScale,
        Vector2? spawnOverride = null, int damageOverride = 0, float visualScale = 1f,
        Vector2? launchOverride = null, bool judgementStar = false)
    {
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        Vector2 spawn = spawnOverride ?? AttackOrigin(owner, swing) + direction * 28f + normal * lateralOffset;
        // High-speed blade pressure shot, with a circular-edge velocity offset of 20%.
        // Formation counts, spawn offsets and base damage remain the tactic's own values.
        bool fullLoadout = owner.GetModPlayer<FranPlayer>().IsFullTacticLoadout(FranSkill.TelekineticCatapult);
        Vector2 launchVelocity = fullLoadout
            ? direction * 30f + Main.rand.NextVector2CircularEdge(6f, 6f)
            : direction.RotatedByRandom(MathHelper.ToRadians(2f)) * 30f;
        launchVelocity = (launchOverride ?? launchVelocity) * FranBalance.GetTelekineticCoreSpeedMultiplier();
        int index = Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, launchVelocity,
            ModContent.ProjectileType<Telekinetic_CondensedMindCore>(),
            Math.Max(1, (int)((damageOverride > 0 ? damageOverride : swing.damage) * damageScale)), swing.knockBack * 0.52f,
            owner.whoAmI, judgementStar ? 2f : 0f, 0f, visualScale);
        if (!Main.projectile.IndexInRange(index))
            return;
        Projectile core = Main.projectile[index];
        core.netUpdate = true;
    }

    internal static void SpawnTelekineticFoldBlade(Player owner, Projectile swing, Vector2 baseDirection,
        float lateralOffset, float angleOffset, float scale, float damageScale, Vector2? spawnOverride = null,
        int damageOverride = 0)
    {
        if (!FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh))
            scale *= FranBalance.GetFloat(nameof(FranBalance.TelekineticFoldBladePreHardmodeScaleMultiplier),
                FranBalance.TelekineticFoldBladePreHardmodeScaleMultiplier);
        Vector2 normal = baseDirection.RotatedBy(MathHelper.PiOver2);
        Vector2 direction = baseDirection.RotatedBy(angleOffset).SafeNormalize(baseDirection);
        Vector2 spawn = spawnOverride ?? owner.MountedCenter + baseDirection * 30f + normal * lateralOffset;
        float curveSign = MathF.Sign(MathF.Abs(angleOffset) > 0.001f ? angleOffset : lateralOffset);
        float launchSpeed = Math.Max(1f, FranBalance.GetFloat(
            nameof(FranBalance.TelekineticFoldBladeLaunchSpeed),
            FranBalance.TelekineticFoldBladeLaunchSpeed));
        int index = Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, direction * launchSpeed,
            ModContent.ProjectileType<Telekinetic_DestructiveWave>(),
            Math.Max(1, (int)((damageOverride > 0 ? damageOverride : swing.damage) * damageScale)),
            swing.knockBack * 0.56f,
            owner.whoAmI, curveSign, 0f, scale);
        if (Main.projectile.IndexInRange(index))
        {
            Main.projectile[index].scale = scale;
            Main.projectile[index].netUpdate = true;
        }
    }

    private static void SpawnTelekineticPlanetChain(Player owner, Projectile swing, Vector2 target, float damageScale, int planetCount)
    {
        NPC lockedTarget = FindNearestTarget(target, 1000f);
        Vector2 focus = lockedTarget?.Center ?? target;
        int damage = Math.Max(1, (int)(swing.damage * damageScale));
        int baseVariant = Math.Abs(swing.identity * 3 + 15 + (int)(Main.GameUpdateCount % 10)) % 10;
        for (int orbitSlot = 0; orbitSlot < planetCount; orbitSlot++)
        {
            int variant = (baseVariant + orbitSlot * 7) % 10;
            Vector2 spawn = Telekinetic_GravityStarfall.InitialOrbitPosition(focus, lockedTarget,
                owner.whoAmI, orbitSlot);
            int planetIndex = Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, Vector2.Zero,
                ModContent.ProjectileType<Telekinetic_GravityStarfall>(), damage, swing.knockBack * 0.46f,
                owner.whoAmI, Telekinetic_GravityStarfall.EncodeAppearance(variant, orbitSlot),
                EncodeOptionalNpc(lockedTarget), 0f);
            if (!Main.projectile.IndexInRange(planetIndex))
                continue;

            Main.projectile[planetIndex].scale = 1.08f;
            Main.projectile[planetIndex].netUpdate = true;
        }
    }

    private static void SpawnMultipleBodies(Player owner, Projectile swing, Vector2 direction,
        int layers, int combo, bool fullLoadout)
    {
        if (swing.ModProjectile is FranSpinningDisc)
            return;
        Vector2 requestedTarget = ClampTarget(owner, FranCompanionContext.Aim(owner), 2400f);
        Vector2 lockedTarget = requestedTarget;
        direction = (lockedTarget - owner.MountedCenter).SafeNormalize(Vector2.UnitX * owner.direction);
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        int count = FranMultipleBodyPattern.Count(combo);
        // 优先使用主剑取整前的基础伤害；兼容没有提供该值的外部生成来源。
        float baseDamage = swing.ModProjectile is IShiFuAttack { SkillBaseDamage: > 0 } sword
            ? sword.SkillBaseDamage
            : swing.damage / Math.Max(0.001f, FranBalance.GetComboDamageMultiplier(combo));
        int groupDamage = Math.Max(6, (int)MathF.Round(baseDamage *
            FranTacticOutputBudget.MultipleBodyCycleWeight(layers) *
            FranTacticOutputBudget.Scale(FranSkill.MultipleBodyCreation) / 4f));
        float centerIndex = (count - 1) * 0.5f;
        int releaseDelay = FranBalance.GetInt(nameof(FranBalance.MultipleBodyAimFrames), FranBalance.MultipleBodyAimFrames);
        for (int i = 0; i < count; i++)
        {
            float formationIndex = i - centerIndex;
            Vector2 origin = owner.MountedCenter - direction * (72f + Math.Abs(formationIndex) * 13f) +
                normal * formationIndex * 48f - Vector2.UnitY * (20f + (i % 2) * 12f);
            float delay = releaseDelay;
            int index = Projectile.NewProjectile(swing.GetSource_FromThis(), origin, Vector2.Zero,
                ModContent.ProjectileType<FranShiFuPhantomBlade>(),
                FranMultipleBodyPattern.BladeDamage(groupDamage, combo, i),
                swing.knockBack * 0.48f, owner.whoAmI,
                FranShiFuPhantomBlade.EncodeLaunchDelay(delay, fullLoadout),
                direction.X, direction.Y);
            if (Main.projectile.IndexInRange(index))
                Main.projectile[index].netUpdate = true;
            // This owner-side random interval is sent through ai[0], keeping every client in step.
            releaseDelay += Main.rand.Next(2, 5);
        }
    }

    private static void SpawnThunderCircuit(Player owner, Projectile swing, int combo, int tacticLayers,
        Vector2 swingDirection, bool fullLoadout, NPC discHitTarget = null)
    {
        bool discAttack = swing.ModProjectile is FranSpinningDisc;
        Vector2 origin = AttackOrigin(owner, swing);
        SoundEngine.PlaySound(SoundID.Item93, origin);
        Vector2 mouseTarget = discAttack ? discHitTarget?.Center ?? origin :
            ClampTarget(owner, FranCompanionContext.Aim(owner), 780f);
        // 电路锚点、连接和分支仅负责演出，所有雷链伤害统一从预算落雷结算。

        if (combo == 0)
        {
            ClearThunderCircuit(owner);
            int baseAnchorCount = 1 + FranBalance.GetInt(
                nameof(FranBalance.ThunderChainNormalSupportingBoltCount),
                FranBalance.ThunderChainNormalSupportingBoltCount);
            int anchorCount = discAttack ? 4 : Math.Clamp(baseAnchorCount + tacticLayers - 1, 2, 5);
            NPC target = FindNearestTarget(mouseTarget, 520f);
            Vector2 destination = target?.Center ?? mouseTarget;
            Vector2 forward = (destination - origin).SafeNormalize(
                swingDirection.SafeNormalize(Vector2.UnitX * owner.direction));
            Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
            int placementDamage = 0;

            for (int i = 0; i < anchorCount; i++)
            {
                float completion = (i + 1f) / anchorCount;
                float pathCompletion = MathHelper.Lerp(0.28f, 0.92f, completion);
                float weave = i == anchorCount - 1 ? 0f :
                    MathF.Sin((i + 1f) * 2.399963f) * (48f + 8f * (i % 2));
                Vector2 position = Vector2.Lerp(origin + forward * 52f,
                    destination, pathCompletion) + normal * weave;
                if (discAttack)
                    position = origin + (Main.GlobalTimeWrappedHourly * 1.1f +
                        MathHelper.TwoPi * i / 4f).ToRotationVector2() * 120f;
                int attachedTarget = !discAttack && i == anchorCount - 1 && target is not null ? target.whoAmI + 1 : 0;
                int node = Projectile.NewProjectile(swing.GetSource_FromThis(), position, Vector2.Zero,
                    ModContent.ProjectileType<FranThunderCircuitNode>(), placementDamage,
                    swing.knockBack * 0.16f, owner.whoAmI, attachedTarget, i, tacticLayers);
                if (Main.projectile.IndexInRange(node))
                {
                    Projectile anchor = Main.projectile[node];
                    anchor.scale = 0.92f + i * 0.055f + (tacticLayers - 1) * 0.035f;
                    if (discAttack && anchor.ModProjectile is FranThunderCircuitNode discNode)
                        discNode.AttachToDisc(swing, i);
                }
            }
            if (discAttack)
            {
                // A hit must make the first stage visibly arc around the cutter;
                // these links are visual only, as on the left-click circuit.
                List<Projectile> ring = GetThunderCircuitAnchors(owner);
                for (int i = 0; i < (ring.Count == 2 ? 1 : ring.Count); i++)
                    SpawnThunderCircuitLink(swing, owner, ring[i].Center,
                        ring[(i + 1) % ring.Count].Center, 0, i * 2, 0.75f, false, fullLoadout);
            }
            return;
        }

        List<Projectile> anchors = GetThunderCircuitAnchors(owner);
        if (anchors.Count == 0)
        {
            // A tactic can be equipped in the middle of the global four-hit sword combo. In that case,
            // seed a compact fallback circuit instead of silently losing stages two through four.
            Vector2 forward = (mouseTarget - origin).SafeNormalize(
                swingDirection.SafeNormalize(Vector2.UnitX * owner.direction));
            Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < (discAttack ? 4 : 2); i++)
            {
                Vector2 position = discAttack
                    ? origin + (Main.GlobalTimeWrappedHourly * 1.1f + i * MathHelper.PiOver2)
                        .ToRotationVector2() * 120f
                    : Vector2.Lerp(origin, mouseTarget, 0.48f + i * 0.36f) +
                        normal * (i == 0 ? -42f : 18f);
                int node = Projectile.NewProjectile(swing.GetSource_FromThis(), position, Vector2.Zero,
                    ModContent.ProjectileType<FranThunderCircuitNode>(),
                    0,
                    swing.knockBack * 0.16f, owner.whoAmI, 0f, i, tacticLayers);
                if (discAttack && Main.projectile.IndexInRange(node) &&
                    Main.projectile[node].ModProjectile is FranThunderCircuitNode discNode)
                    discNode.AttachToDisc(swing, i);
            }
            anchors = GetThunderCircuitAnchors(owner);
        }

        for (int i = 0; i < anchors.Count; i++)
        {
            Projectile anchor = anchors[i];
            anchor.timeLeft = Math.Max(anchor.timeLeft, 132);
            if (discAttack)
            {
                if (anchor.ModProjectile is FranThunderCircuitNode discNode)
                    discNode.AttachToDisc(swing, i);
            }
        }

        if (combo == 1)
        {
            Vector2 start = origin + swingDirection.SafeNormalize(Vector2.UnitX * owner.direction) * 24f;
            int linkDamage = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector2 end = anchors[i].Center;
                SpawnThunderCircuitLink(swing, owner, start, end, linkDamage, i * 3,
                    0.88f + i * 0.05f, false, fullLoadout);
                start = end;
            }
            SpawnThunderRain(owner, swing, discHitTarget?.Center ?? anchors[^1].Center,
                combo, tacticLayers, fullLoadout);
            return;
        }

        if (combo == 2)
        {
            int branchDamage = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector2 radialDirection = (anchors[i].Center - origin).SafeNormalize(
                    swingDirection.SafeNormalize(Vector2.UnitX));
                Projectile.NewProjectile(swing.GetSource_FromThis(), anchors[i].Center, radialDirection,
                    ModContent.ProjectileType<FranLightningBranches>(), branchDamage,
                    swing.knockBack * 0.14f, owner.whoAmI, 0f, i * 2f,
                    FranLightningBranches.EncodeVisualScale(0.72f + i * 0.05f, fullLoadout));
            }

            Projectile launchAnchor = anchors[^1];
            NPC target = FindNearestTarget(mouseTarget, 760f) ?? FindNearestTarget(launchAnchor.Center, 760f);
            Vector2 launchDirection = (target?.Center ?? mouseTarget) - launchAnchor.Center;
            if (launchDirection.LengthSquared() < 4f)
                launchDirection = swingDirection;
            int maximumJumps = FranBalance.GetInt(nameof(FranBalance.ThunderChainNormalTrackedTargetCount),
                FranBalance.ThunderChainNormalTrackedTargetCount) + tacticLayers - 1;
            if (fullLoadout)
                SpawnFullLoadoutRicochetVisual(swing, owner, launchAnchor.Center, launchDirection,
                    Math.Clamp(maximumJumps, 2, 7));
            else
            {
                Projectile.NewProjectile(swing.GetSource_FromThis(), launchAnchor.Center,
                    launchDirection.SafeNormalize(Vector2.UnitX * owner.direction) * 18f,
                    ModContent.ProjectileType<FranRicochetLightning>(),
                    0,
                    swing.knockBack * 0.34f, owner.whoAmI, 0f, Math.Clamp(maximumJumps, 2, 7), tacticLayers);
            }
            SpawnThunderRain(owner, swing, discHitTarget?.Center ?? launchAnchor.Center,
                combo, tacticLayers, fullLoadout);
            return;
        }

        NPC finalTarget = FindNearestTarget(mouseTarget, 760f);
        Vector2 focus = discHitTarget?.Center ?? finalTarget?.Center ?? mouseTarget;
        int returnDamage = 0;
        for (int i = 0; i < anchors.Count; i++)
            SpawnThunderCircuitLink(swing, owner, anchors[i].Center, focus, returnDamage,
                i * 2, 1.05f + i * 0.06f, true, fullLoadout);

        // The return currents identify the target first. Supporting and tracked bolts begin the storm,
        // while FranThunderCircuitFinisher delays the single heaviest focal strike until the circuit closes.
        int delay = 7 + anchors.Count * 2;
        SpawnThunderRain(owner, swing, focus, combo, tacticLayers, fullLoadout, delay);
    }

    // 原样迁入狮源流星的两种星光粒子；只作为蝶变共鸣每一次挥刀的无伤害视觉点缀。
    private static void SpawnButterflyResonanceStarlight(Vector2 center, int facing, Vector2 direction, float reach)
    {
        Vector2 bladeDirection = direction.SafeNormalize(Vector2.UnitX * facing);
        Vector2 origin = center + bladeDirection * MathHelper.Clamp(reach * 0.42f, 42f, 94f);
        LeonidStarlight.Spray(origin, bladeDirection, 1, shape: LeonidStarlightShape.Mote,
            speed: 5.7f, spread: 0.48f, scale: 0.76f, guaranteeCount: true);
        LeonidStarlight.Spray(origin, bladeDirection, 1, shape: LeonidStarlightShape.Shard,
            speed: 4.8f, spread: 0.34f, scale: 0.64f, guaranteeCount: true);
    }

    private static void SpawnThunderCircuitLink(Projectile swing, Player owner, Vector2 start, Vector2 end,
        int damage, int delay, float intensity, bool returning, bool fullLoadout)
    {
        Vector2 edge = end - start;
        if (edge.LengthSquared() < 16f)
            return;
        Projectile.NewProjectile(swing.GetSource_FromThis(), start, edge,
            ModContent.ProjectileType<FranThunderCircuitLink>(), damage, swing.knockBack * 0.22f,
            owner.whoAmI, delay, intensity, FranThunderCircuitLink.EncodeMode(returning, fullLoadout));
    }

    private static void SpawnFullLoadoutRicochetVisual(Projectile swing, Player owner, Vector2 start,
        Vector2 launchDirection, int maximumJumps)
    {
        HashSet<int> visited = new();
        Vector2 current = start;
        Vector2 fallbackDirection = launchDirection.SafeNormalize(Vector2.UnitX * owner.direction);
        float width = FranBalance.GetFloat(nameof(FranBalance.ThunderChainFullLoadoutLightningHalfWidth),
            FranBalance.ThunderChainFullLoadoutLightningHalfWidth);

        for (int jump = 0; jump < maximumJumps; jump++)
        {
            NPC next = null;
            float bestDistance = (jump == 0 ? 760f : 470f);
            float bestDistanceSquared = bestDistance * bestDistance;
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (visited.Contains(npc.whoAmI) || !npc.CanBeChasedBy() ||
                    npc.DistanceSQ(AttackOrigin(owner, swing)) > 980f * 980f)
                    continue;
                float distance = npc.DistanceSQ(current);
                if (distance >= bestDistanceSquared)
                    continue;
                bestDistanceSquared = distance;
                next = npc;
            }

            Vector2 end;
            if (next is null)
            {
                if (jump > 0)
                    break;
                end = current + fallbackDirection * 360f;
            }
            else
            {
                visited.Add(next.whoAmI);
                end = next.Center;
                fallbackDirection = (end - current).SafeNormalize(fallbackDirection);
            }

            FranLightningStrikeVisual.Spawn(swing.GetSource_FromThis(), current, end, owner.whoAmI,
                new Color(122, 82, 240), width, Math.Min(12, jump * 2), 1.15f);
            current = end;
            if (next is null)
                break;
        }
    }

    private static List<Projectile> GetThunderCircuitAnchors(Player owner)
    {
        List<Projectile> anchors = new();
        int nodeType = ModContent.ProjectileType<FranThunderCircuitNode>();
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (FranCompanionContext.Owns(owner, projectile) && projectile.type == nodeType)
                anchors.Add(projectile);
        anchors.Sort((left, right) => left.ai[1].CompareTo(right.ai[1]));
        return anchors;
    }

    private static void ClearThunderCircuit(Player owner)
    {
        int nodeType = ModContent.ProjectileType<FranThunderCircuitNode>();
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (FranCompanionContext.Owns(owner, projectile) && projectile.type == nodeType)
                projectile.Kill();
    }

    private static void SpawnThunderRain(Player owner, Projectile swing, Vector2 focus, int combo,
        int tacticLayers, bool fullLoadout, int finalDelay = 0)
    {
        bool finisher = combo == 3;
        int supporting = (finisher
            ? FranBalance.GetInt(nameof(FranBalance.ThunderChainFinisherSupportingBoltCount), FranBalance.ThunderChainFinisherSupportingBoltCount)
            : FranBalance.GetInt(nameof(FranBalance.ThunderChainNormalSupportingBoltCount), FranBalance.ThunderChainNormalSupportingBoltCount)) + tacticLayers - 1;
        int tracked = (finisher
            ? FranBalance.GetInt(nameof(FranBalance.ThunderChainFinisherTrackedTargetCount), FranBalance.ThunderChainFinisherTrackedTargetCount)
            : FranBalance.GetInt(nameof(FranBalance.ThunderChainNormalTrackedTargetCount), FranBalance.ThunderChainNormalTrackedTargetCount)) + tacticLayers - 1;
        int leftCount = Math.Max(2, 1 + supporting + tracked);
        int count = swing.ModProjectile is FranSpinningDisc disc
            ? Math.Max(1, DiscBudgetCount(leftCount, disc.SkillCycle, combo)) : leftCount;
        int slots = FranBalance.GetAvailableTacticSlots();
        int fullCount = Math.Max(2, leftCount + (slots - tacticLayers) * 2);
        float primaryWeight = FranTacticOutputBudget.ThunderPerSwingRatio(true, fullCount, fullLoadout);
        float secondaryWeight = FranTacticOutputBudget.ThunderPerSwingRatio(false, fullCount, fullLoadout);
        float discDamageCorrection = swing.ModProjectile is FranSpinningDisc
            ? 0.8f * (primaryWeight + (leftCount - 1) * secondaryWeight) /
                Math.Max(0.001f, primaryWeight + (count - 1) * secondaryWeight)
            : 1f;
        List<NPC> targets = new();
        foreach (NPC npc in Main.ActiveNPCs)
            if (npc.CanBeChasedBy() && npc.DistanceSQ(focus) <= 650f * 650f &&
                npc.DistanceSQ(AttackOrigin(owner, swing)) <= 980f * 980f)
                targets.Add(npc);
        targets.Sort((a, b) => a.DistanceSQ(focus).CompareTo(b.DistanceSQ(focus)));

        for (int i = 0; i < count; i++)
        {
            // 每轮齐射只给每个 NPC 一个标记，不足的雷落在附近空处，不能重复锁定主目标。
            NPC target = i < targets.Count ? targets[i] : null;
            Vector2 impact = target?.Center ?? FindEmptyThunderImpact(focus, targets, i, count);
            float multiplier = FranTacticOutputBudget.ThunderPerSwingRatio(i == 0, fullCount,
                fullLoadout);
            int totalDamage = Math.Max(2, (int)(SkillSourceDamage(swing, combo) * multiplier * discDamageCorrection));
            if (i == 0 && finisher)
            {
                // 第四刀的主雷继续等电路收束后落下，避免再叠加一条无预算的重复主雷。
                Projectile.NewProjectile(swing.GetSource_FromThis(), impact, Vector2.Zero,
                    ModContent.ProjectileType<FranThunderCircuitFinisher>(), totalDamage,
                    swing.knockBack * 0.48f, owner.whoAmI, finalDelay,
                    target?.whoAmI + 1 ?? 0,
                    FranThunderCircuitFinisher.EncodeTacticLayers(tacticLayers, fullLoadout));
                continue;
            }
            Vector2 spawn = impact + new Vector2(Main.rand.NextFloat(-130f, 130f), -720f);
            Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, Vector2.Zero,
                ModContent.ProjectileType<FranThunderBudgetStrike>(), totalDamage, 0f, owner.whoAmI,
                impact.X, impact.Y, target?.whoAmI + 1 ?? 0);
            Projectile.NewProjectile(swing.GetSource_FromThis(), impact, Vector2.Zero,
                ModContent.ProjectileType<FranThunderDropVisual>(), 0, 0f, owner.whoAmI,
                spawn.X, spawn.Y, FranThunderDropVisual.EncodeMarker(Main.rand.NextFloat(100f),
                    i == 0, finisher, fullLoadout));
        }
    }

    private static Vector2 FindEmptyThunderImpact(Vector2 focus, List<NPC> targets, int index, int count)
    {
        Vector2 direction = (MathHelper.TwoPi * index / count).ToRotationVector2();
        float radius = 120f;
        Vector2 impact = focus;
        for (int attempt = 0; attempt < 24; attempt++, radius += 64f)
        {
            impact = focus + direction * radius;
            Rectangle area = new((int)impact.X - 38, (int)impact.Y - 38, 76, 76);
            bool occupied = false;
            foreach (NPC npc in targets)
                if (area.Intersects(npc.Hitbox))
                {
                    occupied = true;
                    break;
                }
            if (!occupied)
                return impact;
        }
        return impact;
    }
    private static NPC FindNearestTarget(Vector2 center, float distance)
    {
        NPC best = null;
        float bestDistance = distance * distance;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy())
                continue;
            float candidate = Vector2.DistanceSquared(center, npc.Center);
            if (candidate >= bestDistance)
                continue;
            bestDistance = candidate;
            best = npc;
        }
        return best;
    }

    // Projectile AI values are serialized as non-negative numeric state throughout this chain.
    // Zero means "use the fixed mouse anchor"; a real NPC is stored as whoAmI + 1.
    private static float EncodeOptionalNpc(NPC npc) => npc is null ? 0f : npc.whoAmI + 1f;

    private static Vector2 ClampTarget(Player owner, Vector2 target, float maxDistance = 560f)
    {
        Vector2 offset = target - owner.MountedCenter;
        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y))
            return owner.MountedCenter;
        if (offset.LengthSquared() > maxDistance * maxDistance)
            offset = offset.SafeNormalize(Vector2.UnitX * owner.direction) * maxDistance;
        return owner.MountedCenter + offset;
    }
}
