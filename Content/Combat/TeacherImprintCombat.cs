using System;
using System.Collections.Generic;
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


internal static class TeacherImprintCombat
{
    public static void TriggerSwing(Player owner, Projectile swing, int combo, FranSkill skill,
        int tacticLayers, Vector2 direction, float reach)
    {
        if (Main.myPlayer != owner.whoAmI)
            return;
        tacticLayers = Math.Clamp(tacticLayers, 1, FranPlayer.MaximumTacticSlots);

        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        FranVanillaDustEffects.SkillRelease(owner.MountedCenter + direction.SafeNormalize(Vector2.UnitX * owner.direction) * 26f,
            direction, skill, FranSkillInfo.Color(skill), 7 + combo * 2,
            combo == 3 ? 1.12f : 0.92f);
        switch (skill)
        {
            case FranSkill.TelekineticCatapult:
                Vector2 telekineticTarget = ClampTarget(owner, Main.MouseWorld, 2400f);
                SpawnTelekineticStagePattern(owner, swing, telekineticTarget, combo, tacticLayers);
                break;

            case FranSkill.MultipleBodyCreation:
                int bodyBaseCount = combo == 3
                    ? FranBalance.GetInt(nameof(FranBalance.MultipleBodyFinisherBaseCount),
                        FranBalance.MultipleBodyFinisherBaseCount)
                    : FranBalance.GetInt(nameof(FranBalance.MultipleBodyNormalBaseCount),
                        FranBalance.MultipleBodyNormalBaseCount);
                int bodyExtraCount = combo == 3
                    ? FranBalance.GetInt(nameof(FranBalance.MultipleBodyFinisherExtraLayerCount),
                        FranBalance.MultipleBodyFinisherExtraLayerCount)
                    : FranBalance.GetInt(nameof(FranBalance.MultipleBodyNormalExtraLayerCount),
                        FranBalance.MultipleBodyNormalExtraLayerCount);
                SpawnMultipleBodies(owner, swing, direction,
                    bodyBaseCount + (tacticLayers - 1) * bodyExtraCount, bodyBaseCount, combo,
                    fran.IsFullTacticLoadout(FranSkill.MultipleBodyCreation));
                break;

            case FranSkill.ThunderChain:
                SpawnThunderCircuit(owner, swing, combo, tacticLayers, direction);
                break;

            case FranSkill.ThorsHammer when combo == 3 && fran.AdvanceHammerRound(tacticLayers):
                NPC hammerNpc = FranHammerTargeting.Select(owner,
                    (swing.ModProjectile as TeacherSwingRebuild)?.HammerHitTargets);
                Vector2 hammerTarget = hammerNpc?.Center ?? owner.Center;
                Projectile.NewProjectile(swing.GetSource_FromThis(), hammerTarget, Vector2.Zero,
                    ModContent.ProjectileType<ThorsHammerMarker>(), Math.Max(1, (int)(swing.damage *
                        FranBalance.GetFloat(nameof(FranBalance.ThorsHammerDropDamageMultiplier),
                            FranBalance.ThorsHammerDropDamageMultiplier) *
                        (1f + FranBalance.GetFloat(nameof(FranBalance.ThorsHammerExtraLayerDamageMultiplier),
                            FranBalance.ThorsHammerExtraLayerDamageMultiplier) * (tacticLayers - 1)))),
                    7f, owner.whoAmI, EncodeOptionalNpc(hammerNpc), tacticLayers, swing.identity + 1f);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.58f, Pitch = -0.18f }, hammerTarget);
                break;
        }
    }

    public static void OnSwordHit(Player owner, Projectile swing, NPC target, int combo, FranSkill skill,
        int tacticLayers, Vector2 direction, int damageDone)
    {
        if (skill == FranSkill.None)
            return;
        tacticLayers = Math.Clamp(tacticLayers, 1, FranPlayer.MaximumTacticSlots);

        FranImprintTargetGlobalNPC imprint = target.GetGlobalNPC<FranImprintTargetGlobalNPC>();
        if (skill == FranSkill.SkillTaker)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient && Main.myPlayer != owner.whoAmI)
                return;
            float markStrength = imprint.ApplySkillTakerMark(owner.whoAmI, tacticLayers);
            if (!Main.dedServ)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector2 velocity = Main.rand.NextVector2Circular(3.8f, 3.8f) - Vector2.UnitY * 1.2f;
                    FranParticleSystem.Spawn(new FranSkillGlyphParticle(target.Center, velocity,
                        new Color(210, 95, 255), 0.72f + markStrength * 0.12f, Main.rand.Next(18, 26)));
                }
                if (markStrength >= 0.995f)
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.36f, Pitch = 0.34f }, target.Center);
            }
            if (Main.myPlayer == owner.whoAmI)
            {
                FranBurst.Spawn(swing.GetSource_OnHit(target), target.Center, owner.whoAmI,
                    FranBurstStyle.SkillTaker, 0.76f + markStrength * 0.08f, rotation: direction.ToRotation());
                float launchAngle = direction.SafeNormalize(Vector2.UnitX).ToRotation();
                int wispCount = FranBalance.GetInt(nameof(FranBalance.SkillTakerBaseWispCount),
                    FranBalance.SkillTakerBaseWispCount);
                for (int i = 0; i < wispCount; i++)
                {
                    // Positive rotation is clockwise in Terraria's screen coordinates. Every wisp
                    // therefore leaves in one evenly spaced clockwise formation with no random
                    // phase, jitter, speed, or alternating turn sign.
                    float angle = launchAngle + i * MathHelper.TwoPi / wispCount;
                    Vector2 velocity = angle.ToRotationVector2() * 12.5f;
                    Projectile.NewProjectile(swing.GetSource_OnHit(target), target.Center, velocity,
                        ModContent.ProjectileType<FranSkillTakerWisp>(),
                        Math.Max(1, (int)(swing.damage *
                            FranBalance.GetFloat(nameof(FranBalance.SkillTakerWispDamageMultiplier),
                                FranBalance.SkillTakerWispDamageMultiplier))),
                        swing.knockBack * 0.18f, owner.whoAmI, target.whoAmI, markStrength);
                }
            }
            return;
        }
        if (Main.myPlayer != owner.whoAmI)
            return;

        switch (skill)
        {
            case FranSkill.BlueResonance:
                bool fullBlueResonance = owner.GetModPlayer<FranPlayer>()
                    .IsFullTacticLoadout(FranSkill.BlueResonance);
                int echoCount = (combo == 3
                    ? FranBalance.GetInt(nameof(FranBalance.BlueResonanceFinisherBaseCount),
                        FranBalance.BlueResonanceFinisherBaseCount)
                    : FranBalance.GetInt(nameof(FranBalance.BlueResonanceNormalBaseCount),
                        FranBalance.BlueResonanceNormalBaseCount)) +
                    (tacticLayers - 1) * FranBalance.GetInt(nameof(FranBalance.BlueResonanceExtraLayerCount),
                        FranBalance.BlueResonanceExtraLayerCount);
                float burstAngle = -MathHelper.PiOver2 + Main.rand.NextFloat(-0.18f, 0.18f);
                for (int i = 0; i < echoCount; i++)
                {
                    float spread = echoCount <= 1 ? 0f : MathHelper.Lerp(-1.18f, 1.18f, i / (echoCount - 1f));
                    Vector2 burstVelocity = (burstAngle + spread).ToRotationVector2() *
                        Main.rand.NextFloat(5.8f, 8.4f);
                    Projectile.NewProjectile(swing.GetSource_OnHit(target), target.Center,
                        burstVelocity, ModContent.ProjectileType<FranBlueResonanceEcho>(),
                        Math.Max(1, (int)(swing.damage * (combo == 3
                            ? FranBalance.GetFloat(nameof(FranBalance.BlueResonanceFinisherDamageMultiplier),
                                FranBalance.BlueResonanceFinisherDamageMultiplier)
                            : FranBalance.GetFloat(nameof(FranBalance.BlueResonanceNormalDamageMultiplier),
                                FranBalance.BlueResonanceNormalDamageMultiplier)))),
                        swing.knockBack * 0.3f, owner.whoAmI, target.whoAmI + 1f,
                        i, FranBlueResonanceEcho.EncodeEchoCount(echoCount, fullBlueResonance));
                }
                break;
        }
    }

    private static void SpawnTelekineticStagePattern(Player owner, Projectile swing, Vector2 target,
        int combo, int tacticLayers)
    {
        Vector2 direction = (target - owner.MountedCenter).SafeNormalize(Vector2.UnitX * owner.direction);
        int stage = FranBalance.GetTelekineticPatternStage();
        float single = FranBalance.GetTelekineticFormationDamageMultiplier(1);
        float pair = FranBalance.GetTelekineticFormationDamageMultiplier(2);
        float triple = FranBalance.GetTelekineticFormationDamageMultiplier(3);
        float quartet = FranBalance.GetTelekineticFormationDamageMultiplier(4);
        float heavy = FranBalance.GetFloat(nameof(FranBalance.TelekineticHeavyProjectileDamageMultiplier),
            FranBalance.TelekineticHeavyProjectileDamageMultiplier);

        // 第一维：固定四刀。第二维：当前世界时期。
        // 该状态机只生成附加弹幕，主武器始终保持固定四段剑招。
        if (stage == 0)
        {
            switch (combo)
            {
                case 0:
                    SpawnTelekineticPressureCore(owner, swing, direction, 0f, 2f, 0.94f, single);
                    break;
                case 1:
                    SpawnTelekineticPressureCore(owner, swing, direction, owner.direction * 12f, 5f, 0.98f, single);
                    break;
                case 2:
                    SpawnTelekineticPressureCore(owner, swing, direction, -17f, 1f, 0.88f, pair);
                    SpawnTelekineticPressureCore(owner, swing, direction, 17f, 3f, 0.88f, pair);
                    break;
                default:
                    SpawnTelekineticPressureCore(owner, swing, direction, 0f, 7f, 1.36f, heavy);
                    break;
            }
        }
        else if (stage == 1)
        {
            switch (combo)
            {
                case 0:
                    SpawnTelekineticPressureCore(owner, swing, direction, 0f, 2f, 0.98f, single);
                    break;
                case 1:
                    SpawnTelekineticFoldBlade(owner, swing, direction, -owner.direction * 30f,
                        -owner.direction * 0.08f, 1f, single);
                    break;
                case 2:
                    SpawnTelekineticPressureCore(owner, swing, direction, -19f, 1f, 0.86f, triple);
                    SpawnTelekineticPressureCore(owner, swing, direction, 19f, 3f, 0.86f, triple);
                    SpawnTelekineticFoldBlade(owner, swing, direction, 0f, 0f, 0.90f, triple);
                    break;
                default:
                    // 冲击波始终只有一枚：把原先双排的总伤害合并到居中波体。
                    SpawnTelekineticFoldBlade(owner, swing, direction, 0f, 0f, 1.02f, pair * 2f);
                    break;
            }
        }
        else if (stage == 2)
        {
            switch (combo)
            {
                case 0:
                    SpawnTelekineticLaser(owner, swing, direction, 0f, 0.96f, single, false);
                    break;
                case 1:
                    SpawnTelekineticLaser(owner, swing, direction, 0f, 0.96f, pair, false);
                    SpawnTelekineticFoldBlade(owner, swing, direction, -owner.direction * 42f,
                        owner.direction * 0.07f, 0.88f, pair);
                    break;
                case 2:
                    SpawnTelekineticLaser(owner, swing, direction, -26f, 0.90f, triple, false);
                    SpawnTelekineticLaser(owner, swing, direction, 26f, 0.90f, triple, true);
                    SpawnTelekineticPressureCore(owner, swing, direction, 0f, 6f, 0.84f, triple);
                    break;
                default:
                    SpawnTelekineticLaser(owner, swing, direction, 0f, 1.34f, heavy, false);
                    break;
            }
        }
        else
        {
            switch (combo)
            {
                case 0:
                    SpawnTelekineticLaser(owner, swing, direction, 0f, 0.98f, pair, false);
                    SpawnTelekineticPressureCore(owner, swing, direction, 0f, 7f, 0.86f, pair);
                    break;
                case 1:
                    SpawnTelekineticLaser(owner, swing, direction, 0f, 0.98f, triple, false);
                    // 后期编排也不再并排复制冲击波，仍只发射一枚居中波体。
                    SpawnTelekineticFoldBlade(owner, swing, direction, 0f, 0f, 0.88f, triple * 2f);
                    break;
                case 2:
                    SpawnTelekineticLaser(owner, swing, direction, -32f, 0.92f, quartet, false);
                    SpawnTelekineticLaser(owner, swing, direction, 32f, 0.92f, quartet, true);
                    SpawnTelekineticPressureCore(owner, swing, direction, -62f, 5f, 0.78f, quartet);
                    SpawnTelekineticPressureCore(owner, swing, direction, 62f, 8f, 0.78f, quartet);
                    break;
                default:
                    if (stage == 3)
                    {
                        SpawnTelekineticLaser(owner, swing, direction, 0f, 1.28f, triple, false);
                        SpawnTelekineticFoldBlade(owner, swing, direction, 0f, 0f, 0.92f, triple * 2f);
                    }
                    else
                    {
                        SpawnTelekineticLaser(owner, swing, direction, 0f, 1.34f, heavy, false);
                        SpawnTelekineticPlanetChain(owner, swing, target);
                    }
                    break;
            }
        }

        // 第三维被刻意压成唯一语义：A 是上方当前配方，每层 B 只多一枚侧翼念压核。
        SpawnTelekineticLayerCores(owner, swing, direction, combo, tacticLayers - 1);

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

        owner.GetModPlayer<FranPlayer>().ShakeScreen(owner.Center,
            (combo switch { 0 => 1.0f, 1 => 1.35f, 2 => 1.8f, _ => 2.8f }) + stage * 0.08f);
    }

    private static void SpawnTelekineticPressureCore(Player owner, Projectile swing, Vector2 direction,
        float lateralOffset, float releaseDelay, float scale, float damageScale, int? formationLane = null)
    {
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        Vector2 spawn = owner.MountedCenter + direction * 28f + normal * lateralOffset;
        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        bool fullTelekineticLoadout = fran.IsFullTacticLoadout(FranSkill.TelekineticCatapult);
        float initialSpeed = (scale >= 1.15f ? 8.4f : 7.2f) * FranBalance.GetFloat(
            nameof(FranBalance.TelekineticPressureCoreInitialSpeedMultiplier),
            FranBalance.TelekineticPressureCoreInitialSpeedMultiplier);
        int index = Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, direction * initialSpeed,
            ModContent.ProjectileType<FranTelekineticShell>(),
            Math.Max(1, (int)(swing.damage * damageScale)), swing.knockBack * 0.52f,
            owner.whoAmI, direction.X, direction.Y,
            FranTelekineticShell.EncodeReleaseDelay(releaseDelay, fullTelekineticLoadout));
        if (!Main.projectile.IndexInRange(index))
            return;

        Projectile core = Main.projectile[index];
        core.scale = scale;
        int hitboxSize = Math.Max(18, (int)MathF.Round(26f * scale));
        core.Resize(hitboxSize, hitboxSize);
        if (fullTelekineticLoadout && core.ModProjectile is FranTelekineticShell shell)
        {
            int lane = formationLane ?? Math.Sign(lateralOffset) * (Math.Abs(lateralOffset) >= 40f ? 2 : 1);
            shell.ConfigureFormation(lane);
        }
        core.netUpdate = true;
    }

    private static void SpawnTelekineticFoldBlade(Player owner, Projectile swing, Vector2 baseDirection,
        float lateralOffset, float angleOffset, float scale, float damageScale)
    {
        Vector2 normal = baseDirection.RotatedBy(MathHelper.PiOver2);
        Vector2 direction = baseDirection.RotatedBy(angleOffset).SafeNormalize(baseDirection);
        Vector2 spawn = owner.MountedCenter + baseDirection * 30f + normal * lateralOffset;
        float curveSign = MathF.Sign(MathF.Abs(angleOffset) > 0.001f ? angleOffset : lateralOffset);
        float launchSpeed = Math.Max(1f, FranBalance.GetFloat(
            nameof(FranBalance.TelekineticFoldBladeLaunchSpeed),
            FranBalance.TelekineticFoldBladeLaunchSpeed));
        int index = Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, direction * launchSpeed,
            ModContent.ProjectileType<FranTelekineticFoldBlade>(),
            Math.Max(1, (int)(swing.damage * damageScale)), swing.knockBack * 0.56f,
            owner.whoAmI, curveSign, 0f, scale);
        if (Main.projectile.IndexInRange(index))
        {
            Main.projectile[index].scale = scale;
            Main.projectile[index].netUpdate = true;
        }
    }

    private static void SpawnTelekineticLaser(Player owner, Projectile swing, Vector2 direction,
        float lateralOffset, float scale, float damageScale, bool suppressLaunchBurst)
    {
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        Vector2 spawn = owner.MountedCenter + direction * 26f + normal * lateralOffset;
        int index = Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, direction * 11.8f,
            ModContent.ProjectileType<FranTelekineticHalleyBolt>(),
            Math.Max(1, (int)(swing.damage * damageScale)), swing.knockBack * 0.64f,
            owner.whoAmI, 0f, 0f, suppressLaunchBurst ? -1f : 0f);
        if (!Main.projectile.IndexInRange(index))
            return;

        Projectile laser = Main.projectile[index];
        laser.scale = scale;
        int hitboxSize = Math.Max(24, (int)MathF.Round(32f * scale));
        laser.Resize(hitboxSize, hitboxSize);
        laser.netUpdate = true;
    }

    private static void SpawnTelekineticLayerCores(Player owner, Projectile swing, Vector2 direction,
        int combo, int extraLayers)
    {
        if (extraLayers <= 0)
            return;

        float damageScale = FranBalance.GetFloat(nameof(FranBalance.TelekineticExtraLayerProjectileDamageMultiplier),
            FranBalance.TelekineticExtraLayerProjectileDamageMultiplier);
        float spacing = combo == 3 ? 38f : 31f;
        for (int i = 0; i < extraLayers; i++)
        {
            float side = (i % 2 == 0 ? 1f : -1f) * (i / 2 + 1) * spacing * owner.direction;
            SpawnTelekineticPressureCore(owner, swing, direction, side, 3f + i * 2f,
                combo == 3 ? 0.76f : 0.70f, damageScale, Math.Sign(side) * (i / 2 + 3));
        }
    }

    private static void SpawnTelekineticPlanetChain(Player owner, Projectile swing, Vector2 target)
    {
        NPC lockedTarget = FindNearestTarget(target, 1000f);
        Vector2 focus = lockedTarget?.Center ?? target;
        float damageScale = FranBalance.GetFloat(nameof(FranBalance.TelekineticFinisherPlanetDamageMultiplier),
            FranBalance.TelekineticFinisherPlanetDamageMultiplier);
        int damage = Math.Max(1, (int)(swing.damage * damageScale));
        int baseVariant = Math.Abs(swing.identity * 3 + 15 + (int)(Main.GameUpdateCount % 10)) % 10;
        for (int orbitSlot = 0; orbitSlot < FranTelekineticPlanet.PlanetCount; orbitSlot++)
        {
            int variant = (baseVariant + orbitSlot * 7) % 10;
            Vector2 spawn = FranTelekineticPlanet.InitialOrbitPosition(focus, lockedTarget,
                owner.whoAmI, orbitSlot);
            int planetIndex = Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, Vector2.Zero,
                ModContent.ProjectileType<FranTelekineticPlanet>(), damage, swing.knockBack * 0.46f,
                owner.whoAmI, FranTelekineticPlanet.EncodeAppearance(variant, orbitSlot),
                EncodeOptionalNpc(lockedTarget), 0f);
            if (!Main.projectile.IndexInRange(planetIndex))
                continue;

            Main.projectile[planetIndex].scale = 1.08f;
            Main.projectile[planetIndex].netUpdate = true;
        }
    }

    private static void SpawnMultipleBodies(Player owner, Projectile swing, Vector2 direction, int count,
        int firstLayerCount, int combo, bool fullLoadout)
    {
        Vector2 requestedTarget = Main.MouseWorld;
        if (!float.IsFinite(requestedTarget.X) || !float.IsFinite(requestedTarget.Y))
            requestedTarget = owner.MountedCenter + direction.SafeNormalize(Vector2.UnitX * owner.direction) * 180f;
        NPC target = FindNearestTarget(requestedTarget, 430f);
        bool hasEnemy = target is not null;
        Vector2 lockedTarget = target?.Center ?? requestedTarget;
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        float centerIndex = (count - 1) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float formationIndex = i - centerIndex;
            Vector2 origin = owner.MountedCenter - direction * (72f + Math.Abs(formationIndex) * 13f) +
                normal * formationIndex * 48f - Vector2.UnitY * (20f + (i % 2) * 12f);
            float delay = 6f + i * 3f;
            Projectile.NewProjectile(swing.GetSource_FromThis(), origin, Vector2.Zero,
                ModContent.ProjectileType<FranTeacherPhantomBlade>(),
                Math.Max(1, (int)(swing.damage * (combo == 3
                    ? FranBalance.GetFloat(nameof(FranBalance.MultipleBodyFinisherDamageMultiplier),
                        FranBalance.MultipleBodyFinisherDamageMultiplier)
                    : FranBalance.GetFloat(nameof(FranBalance.MultipleBodyNormalDamageMultiplier),
                        FranBalance.MultipleBodyNormalDamageMultiplier)) / Math.Max(1, firstLayerCount))),
                swing.knockBack * 0.48f, owner.whoAmI,
                FranTeacherPhantomBlade.EncodeLaunchDelay(delay, hasEnemy, fullLoadout),
                lockedTarget.X, lockedTarget.Y);
        }
    }

    private static void SpawnThunderCircuit(Player owner, Projectile swing, int combo, int tacticLayers,
        Vector2 swingDirection)
    {
        Vector2 mouseTarget = ClampTarget(owner, Main.MouseWorld, 780f);
        float layerDamage = GetThunderWeatherDamageMultiplier(owner, tacticLayers) * (1f +
            FranBalance.GetFloat(nameof(FranBalance.ThunderChainExtraLayerDamageMultiplier),
                FranBalance.ThunderChainExtraLayerDamageMultiplier) * (tacticLayers - 1));
        float mainDamage = FranBalance.GetFloat(nameof(FranBalance.ThunderChainMainDamageMultiplier),
            FranBalance.ThunderChainMainDamageMultiplier);
        float trackedDamage = FranBalance.GetFloat(nameof(FranBalance.ThunderChainTrackedDamageMultiplier),
            FranBalance.ThunderChainTrackedDamageMultiplier);

        if (combo == 0)
        {
            ClearThunderCircuit(owner);
            int baseAnchorCount = 1 + FranBalance.GetInt(
                nameof(FranBalance.ThunderChainNormalSupportingBoltCount),
                FranBalance.ThunderChainNormalSupportingBoltCount);
            int anchorCount = Math.Clamp(baseAnchorCount + tacticLayers - 1, 2, 5);
            NPC target = FindNearestTarget(mouseTarget, 520f);
            Vector2 destination = target?.Center ?? mouseTarget;
            Vector2 forward = (destination - owner.MountedCenter).SafeNormalize(
                swingDirection.SafeNormalize(Vector2.UnitX * owner.direction));
            Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
            int placementDamage = Math.Max(1, (int)(swing.damage * mainDamage * layerDamage / anchorCount));

            for (int i = 0; i < anchorCount; i++)
            {
                float completion = (i + 1f) / anchorCount;
                float pathCompletion = MathHelper.Lerp(0.28f, 0.92f, completion);
                float weave = i == anchorCount - 1 ? 0f :
                    MathF.Sin((i + 1f) * 2.399963f) * (48f + 8f * (i % 2));
                Vector2 position = Vector2.Lerp(owner.MountedCenter + forward * 52f,
                    destination, pathCompletion) + normal * weave;
                int attachedTarget = i == anchorCount - 1 && target is not null ? target.whoAmI + 1 : 0;
                int node = Projectile.NewProjectile(swing.GetSource_FromThis(), position, Vector2.Zero,
                    ModContent.ProjectileType<FranThunderCircuitNode>(), placementDamage,
                    swing.knockBack * 0.16f, owner.whoAmI, attachedTarget, i, tacticLayers);
                if (Main.projectile.IndexInRange(node))
                    Main.projectile[node].scale = 0.92f + i * 0.055f + (tacticLayers - 1) * 0.035f;
            }
            return;
        }

        List<Projectile> anchors = GetThunderCircuitAnchors(owner);
        if (anchors.Count == 0)
        {
            // A tactic can be equipped in the middle of the global four-hit sword combo. In that case,
            // seed a compact fallback circuit instead of silently losing stages two through four.
            Vector2 forward = (mouseTarget - owner.MountedCenter).SafeNormalize(
                swingDirection.SafeNormalize(Vector2.UnitX * owner.direction));
            Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < 2; i++)
            {
                Vector2 position = Vector2.Lerp(owner.MountedCenter, mouseTarget, 0.48f + i * 0.36f) +
                    normal * (i == 0 ? -42f : 18f);
                Projectile.NewProjectile(swing.GetSource_FromThis(), position, Vector2.Zero,
                    ModContent.ProjectileType<FranThunderCircuitNode>(),
                    Math.Max(1, (int)(swing.damage * mainDamage * layerDamage * 0.5f)),
                    swing.knockBack * 0.16f, owner.whoAmI, 0f, i, tacticLayers);
            }
            anchors = GetThunderCircuitAnchors(owner);
        }

        foreach (Projectile anchor in anchors)
            anchor.timeLeft = Math.Max(anchor.timeLeft, 132);

        if (combo == 1)
        {
            Vector2 start = owner.MountedCenter + swingDirection.SafeNormalize(Vector2.UnitX * owner.direction) * 24f;
            int linkDamage = Math.Max(1, (int)(swing.damage * trackedDamage * layerDamage));
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector2 end = anchors[i].Center;
                SpawnThunderCircuitLink(swing, owner, start, end, linkDamage, i * 3,
                    0.88f + i * 0.05f, false);
                start = end;
            }
            SpawnThunderRain(owner, swing, anchors[^1].Center, combo, tacticLayers, includeMainStrike: true);
            return;
        }

        if (combo == 2)
        {
            int branchDamage = Math.Max(1, (int)(swing.damage * mainDamage * layerDamage /
                Math.Max(1, anchors.Count)));
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector2 radialDirection = (anchors[i].Center - owner.MountedCenter).SafeNormalize(
                    swingDirection.SafeNormalize(Vector2.UnitX));
                Projectile.NewProjectile(swing.GetSource_FromThis(), anchors[i].Center, radialDirection,
                    ModContent.ProjectileType<FranLightningBranches>(), branchDamage,
                    swing.knockBack * 0.14f, owner.whoAmI, 0f, i * 2f, 0.72f + i * 0.05f);
            }

            Projectile launchAnchor = anchors[^1];
            NPC target = FindNearestTarget(mouseTarget, 760f) ?? FindNearestTarget(launchAnchor.Center, 760f);
            Vector2 launchDirection = (target?.Center ?? mouseTarget) - launchAnchor.Center;
            if (launchDirection.LengthSquared() < 4f)
                launchDirection = swingDirection;
            int maximumJumps = FranBalance.GetInt(nameof(FranBalance.ThunderChainNormalTrackedTargetCount),
                FranBalance.ThunderChainNormalTrackedTargetCount) + tacticLayers - 1;
            Projectile.NewProjectile(swing.GetSource_FromThis(), launchAnchor.Center,
                launchDirection.SafeNormalize(Vector2.UnitX * owner.direction) * 18f,
                ModContent.ProjectileType<FranRicochetLightning>(),
                Math.Max(1, (int)(swing.damage * trackedDamage * layerDamage)),
                swing.knockBack * 0.34f, owner.whoAmI, 0f, Math.Clamp(maximumJumps, 2, 7), tacticLayers);
            SpawnThunderRain(owner, swing, launchAnchor.Center, combo, tacticLayers, includeMainStrike: true);
            return;
        }

        NPC finalTarget = FindNearestTarget(mouseTarget, 760f);
        Vector2 focus = finalTarget?.Center ?? mouseTarget;
        int returnDamage = Math.Max(1, (int)(swing.damage * trackedDamage * layerDamage));
        for (int i = 0; i < anchors.Count; i++)
            SpawnThunderCircuitLink(swing, owner, anchors[i].Center, focus, returnDamage,
                i * 2, 1.05f + i * 0.06f, true);

        // The return currents identify the target first. Supporting and tracked bolts begin the storm,
        // while FranThunderCircuitFinisher delays the single heaviest focal strike until the circuit closes.
        SpawnThunderRain(owner, swing, focus, combo, tacticLayers, includeMainStrike: false);
        int delay = 7 + anchors.Count * 2;
        Projectile.NewProjectile(swing.GetSource_FromThis(), focus, Vector2.Zero,
            ModContent.ProjectileType<FranThunderCircuitFinisher>(),
            Math.Max(1, (int)(swing.damage * mainDamage * layerDamage)),
            swing.knockBack * 0.48f, owner.whoAmI, delay,
            finalTarget is null ? 0f : finalTarget.whoAmI + 1f, tacticLayers);
    }

    private static void SpawnThunderCircuitLink(Projectile swing, Player owner, Vector2 start, Vector2 end,
        int damage, int delay, float intensity, bool returning)
    {
        Vector2 edge = end - start;
        if (edge.LengthSquared() < 16f)
            return;
        Projectile.NewProjectile(swing.GetSource_FromThis(), start, edge,
            ModContent.ProjectileType<FranThunderCircuitLink>(), damage, swing.knockBack * 0.22f,
            owner.whoAmI, delay, intensity, returning ? 1f : 0f);
    }

    private static List<Projectile> GetThunderCircuitAnchors(Player owner)
    {
        List<Projectile> anchors = new();
        int nodeType = ModContent.ProjectileType<FranThunderCircuitNode>();
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.owner == owner.whoAmI && projectile.type == nodeType)
                anchors.Add(projectile);
        anchors.Sort((left, right) => left.ai[1].CompareTo(right.ai[1]));
        return anchors;
    }

    private static void ClearThunderCircuit(Player owner)
    {
        int nodeType = ModContent.ProjectileType<FranThunderCircuitNode>();
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.owner == owner.whoAmI && projectile.type == nodeType)
                projectile.Kill();
    }

    private static void SpawnThunderRain(Player owner, Projectile swing, Vector2 focus, int combo,
        int tacticLayers, bool includeMainStrike)
    {
        bool finisher = combo == 3;
        float layerDamage = GetThunderWeatherDamageMultiplier(owner, tacticLayers) * (1f +
            FranBalance.GetFloat(nameof(FranBalance.ThunderChainExtraLayerDamageMultiplier),
                FranBalance.ThunderChainExtraLayerDamageMultiplier) * (tacticLayers - 1));
        float mainDamage = FranBalance.GetFloat(nameof(FranBalance.ThunderChainMainDamageMultiplier),
            FranBalance.ThunderChainMainDamageMultiplier);
        if (includeMainStrike)
            SpawnThunderRainDrop(swing, owner, focus, mainDamage * layerDamage,
                mainStrike: true, finisher: false);

        int supportingBoltCount = (finisher
            ? FranBalance.GetInt(nameof(FranBalance.ThunderChainFinisherSupportingBoltCount),
                FranBalance.ThunderChainFinisherSupportingBoltCount)
            : FranBalance.GetInt(nameof(FranBalance.ThunderChainNormalSupportingBoltCount),
                FranBalance.ThunderChainNormalSupportingBoltCount)) + tacticLayers - 1;
        for (int i = 0; i < supportingBoltCount; i++)
        {
            float horizontalOffset = supportingBoltCount == 1
                ? (combo % 2 == 0 ? -96f : 96f)
                : MathHelper.Lerp(-112f, 112f, i / (supportingBoltCount - 1f));
            Vector2 impact = focus + new Vector2(horizontalOffset, Main.rand.NextFloat(-18f, 30f));
            SpawnThunderRainDrop(swing, owner, impact, mainDamage * layerDamage,
                mainStrike: false, finisher: false);
        }

        List<NPC> candidates = new();
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.CanBeChasedBy() && Vector2.DistanceSquared(npc.Center, focus) <= 650f * 650f &&
                Vector2.DistanceSquared(npc.Center, owner.Center) <= 980f * 980f)
                candidates.Add(npc);
        }
        candidates.Sort((left, right) =>
            Vector2.DistanceSquared(left.Center, focus).CompareTo(Vector2.DistanceSquared(right.Center, focus)));
        int trackedCount = Math.Min((finisher
            ? FranBalance.GetInt(nameof(FranBalance.ThunderChainFinisherTrackedTargetCount),
                FranBalance.ThunderChainFinisherTrackedTargetCount)
            : FranBalance.GetInt(nameof(FranBalance.ThunderChainNormalTrackedTargetCount),
                FranBalance.ThunderChainNormalTrackedTargetCount)) + tacticLayers - 1, candidates.Count);
        float trackedDamage = FranBalance.GetFloat(nameof(FranBalance.ThunderChainTrackedDamageMultiplier),
            FranBalance.ThunderChainTrackedDamageMultiplier);
        for (int i = 0; i < trackedCount; i++)
            SpawnThunderRainDrop(swing, owner, candidates[i].Center, trackedDamage * layerDamage,
                mainStrike: false, finisher: false);
    }

    private static void SpawnThunderRainDrop(Projectile swing, Player owner, Vector2 impact,
        float damageScale, bool mainStrike, bool finisher)
    {
        Vector2 spawn = impact + new Vector2(Main.rand.NextFloat(-130f, 130f),
            Main.rand.NextFloat(-800f, -650f));
        float strikeMarker = finisher ? 2f : mainStrike ? 1f : 0f;
        Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, Vector2.Zero,
            ModContent.ProjectileType<FranThunderStrikeProjectile>(),
            Math.Max(1, (int)(swing.damage * damageScale)), 0f, owner.whoAmI,
            impact.X, impact.Y, strikeMarker);

        float visualSeed = Main.rand.NextFloat(100f) +
            (mainStrike ? (finisher ? 2000f : 1000f) : 0f);
        Projectile.NewProjectile(swing.GetSource_FromThis(), impact, Vector2.Zero,
            ModContent.ProjectileType<FranThunderDropVisual>(), 0, 0f, owner.whoAmI,
            spawn.X, spawn.Y, visualSeed);
    }

    private static float GetThunderWeatherDamageMultiplier(Player owner, int tacticLayers)
    {
        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        return Main.raining && tacticLayers == fran.AvailableTacticSlots &&
               fran.IsFullTacticLoadout(FranSkill.ThunderChain)
            ? Math.Max(0f, FranBalance.GetFloat(
                nameof(FranBalance.ThunderChainFullLoadoutRainDamageMultiplier),
                FranBalance.ThunderChainFullLoadoutRainDamageMultiplier))
            : 1f;
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
