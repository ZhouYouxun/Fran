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
using Terraria.DataStructures;
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

            case FranSkill.BlueResonance when fran.IsFullTacticLoadout(FranSkill.BlueResonance):
                // 全选蓝辉不再要求剑体先命中目标：每次挥刀固定生成一整组蓝辉弹。
                // 后续若剑体实际命中一个或多个敌人，OnSwordHit 仍会按每个命中目标追加同等的一组。
                SpawnBlueResonanceEchoes(owner, swing, combo, tacticLayers,
                    owner.MountedCenter + direction.SafeNormalize(Vector2.UnitX * owner.direction) * 36f,
                    swing.GetSource_FromThis(), encodedTarget: 0f);
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
                            FranBalance.ThorsHammerExtraLayerDamageMultiplier) * (tacticLayers - 1)) * FranTacticOutputBudget.Scale(FranSkill.ThorsHammer))),
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
                wispCount = swing.ModProjectile is TeacherSwingRebuild teacherSwing
                    ? teacherSwing.ReserveSkillTakerWisps(wispCount) : 0;
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
                SpawnBlueResonanceEchoes(owner, swing, combo, tacticLayers, target.Center,
                    swing.GetSource_OnHit(target), target.whoAmI + 1f);
                break;
        }
    }

    private static void SpawnBlueResonanceEchoes(Player owner, Projectile swing, int combo,
        int tacticLayers, Vector2 center, IEntitySource source, float encodedTarget)
    {
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
            Projectile.NewProjectile(source, center, burstVelocity,
                ModContent.ProjectileType<FranBlueResonanceEcho>(),
                Math.Max(1, (int)(swing.damage * (combo == 3
                    ? FranBalance.GetFloat(nameof(FranBalance.BlueResonanceFinisherDamageMultiplier),
                        FranBalance.BlueResonanceFinisherDamageMultiplier)
                    : FranBalance.GetFloat(nameof(FranBalance.BlueResonanceNormalDamageMultiplier),
                        FranBalance.BlueResonanceNormalDamageMultiplier)) * FranTacticOutputBudget.Scale(FranSkill.BlueResonance))),
                swing.knockBack * 0.3f, owner.whoAmI, encodedTarget,
                i, FranBlueResonanceEcho.EncodeEchoCount(echoCount));
        }
    }

    private static void SpawnTelekineticStagePattern(Player owner, Projectile swing, Vector2 target,
        int combo, int tacticLayers)
    {
        Vector2 direction = (target - owner.MountedCenter).SafeNormalize(Vector2.UnitX * owner.direction);
        int stage = FranBalance.GetTelekineticPatternStage();
        float damageScale = FranTelekineticPattern.GetDamageScale(stage, FranBalance.GetAvailableTacticSlots());
        List<FranTelekineticShot> shots = new(16);
        FranTelekineticPattern.Fill(shots, stage, combo, tacticLayers, owner.direction);
        foreach (FranTelekineticShot shot in shots)
        {
            float multiplier = shot.Weight * damageScale;
            switch (shot.Kind)
            {
                case FranTelekineticShotKind.Core:
                    SpawnTelekineticPressureCore(owner, swing, direction, shot.Offset, multiplier);
                    break;
                case FranTelekineticShotKind.Wave:
                    SpawnTelekineticFoldBlade(owner, swing, direction, shot.Offset, shot.Angle, shot.Scale, multiplier);
                    break;
                case FranTelekineticShotKind.Laser:
                    SpawnTelekineticLaser(owner, swing, direction, shot.Offset, shot.Scale, multiplier, shot.SuppressLaunchBurst);
                    break;
                case FranTelekineticShotKind.Planet:
                    SpawnTelekineticPlanetChain(owner, swing, target, multiplier, shot.Count);
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

        owner.GetModPlayer<FranPlayer>().ShakeScreen(owner.Center,
            (combo switch { 0 => 1.0f, 1 => 1.35f, 2 => 1.8f, _ => 2.8f }) + stage * 0.08f);
    }

    private static void SpawnTelekineticPressureCore(Player owner, Projectile swing, Vector2 direction, float lateralOffset, float damageScale)
    {
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        Vector2 spawn = owner.MountedCenter + direction * 28f + normal * lateralOffset;
        // Terratomere: shootSpeed 60 / 2, with a circular-edge velocity offset of 20%.
        // Formation counts, spawn offsets and base damage remain the tactic's own values.
        bool fullLoadout = owner.GetModPlayer<FranPlayer>().IsFullTacticLoadout(FranSkill.TelekineticCatapult);
        Vector2 launchVelocity = fullLoadout
            ? direction * 30f + Main.rand.NextVector2CircularEdge(6f, 6f)
            : direction.RotatedByRandom(MathHelper.ToRadians(2f)) * 30f;
        launchVelocity *= FranBalance.GetTelekineticCoreSpeedMultiplier();
        int index = Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, launchVelocity,
            ModContent.ProjectileType<Telekinetic_CondensedMindCore>(),
            Math.Max(1, (int)(swing.damage * damageScale)), swing.knockBack * 0.52f,
            owner.whoAmI, fullLoadout ? 1f : 0f);
        if (!Main.projectile.IndexInRange(index))
            return;
        Projectile core = Main.projectile[index];
        core.netUpdate = true;
    }

    private static void SpawnTelekineticFoldBlade(Player owner, Projectile swing, Vector2 baseDirection,
        float lateralOffset, float angleOffset, float scale, float damageScale)
    {
        if (!Main.hardMode)
            scale *= FranBalance.GetFloat(nameof(FranBalance.TelekineticFoldBladePreHardmodeScaleMultiplier),
                FranBalance.TelekineticFoldBladePreHardmodeScaleMultiplier);
        Vector2 normal = baseDirection.RotatedBy(MathHelper.PiOver2);
        Vector2 direction = baseDirection.RotatedBy(angleOffset).SafeNormalize(baseDirection);
        Vector2 spawn = owner.MountedCenter + baseDirection * 30f + normal * lateralOffset;
        float curveSign = MathF.Sign(MathF.Abs(angleOffset) > 0.001f ? angleOffset : lateralOffset);
        float launchSpeed = Math.Max(1f, FranBalance.GetFloat(
            nameof(FranBalance.TelekineticFoldBladeLaunchSpeed),
            FranBalance.TelekineticFoldBladeLaunchSpeed));
        int index = Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, direction * launchSpeed,
            ModContent.ProjectileType<Telekinetic_DestructiveWave>(),
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
            ModContent.ProjectileType<Telekinetic_Flash>(),
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
                        FranBalance.MultipleBodyNormalDamageMultiplier)) / Math.Max(1, firstLayerCount) * FranTacticOutputBudget.Scale(FranSkill.MultipleBodyCreation))),
                swing.knockBack * 0.48f, owner.whoAmI,
                FranTeacherPhantomBlade.EncodeLaunchDelay(delay, hasEnemy, fullLoadout),
                lockedTarget.X, lockedTarget.Y);
        }
    }

    private static void SpawnThunderCircuit(Player owner, Projectile swing, int combo, int tacticLayers,
        Vector2 swingDirection)
    {
        Vector2 mouseTarget = ClampTarget(owner, Main.MouseWorld, 780f);
        // 电路锚点、连接和分支仅负责演出，所有雷链伤害统一从预算落雷结算。

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
            int placementDamage = 0;

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
                    0,
                    swing.knockBack * 0.16f, owner.whoAmI, 0f, i, tacticLayers);
            }
            anchors = GetThunderCircuitAnchors(owner);
        }

        foreach (Projectile anchor in anchors)
            anchor.timeLeft = Math.Max(anchor.timeLeft, 132);

        if (combo == 1)
        {
            Vector2 start = owner.MountedCenter + swingDirection.SafeNormalize(Vector2.UnitX * owner.direction) * 24f;
            int linkDamage = 0;
            for (int i = 0; i < anchors.Count; i++)
            {
                Vector2 end = anchors[i].Center;
                SpawnThunderCircuitLink(swing, owner, start, end, linkDamage, i * 3,
                    0.88f + i * 0.05f, false);
                start = end;
            }
            SpawnThunderRain(owner, swing, anchors[^1].Center, combo, tacticLayers);
            return;
        }

        if (combo == 2)
        {
            int branchDamage = 0;
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
                0,
                swing.knockBack * 0.34f, owner.whoAmI, 0f, Math.Clamp(maximumJumps, 2, 7), tacticLayers);
            SpawnThunderRain(owner, swing, launchAnchor.Center, combo, tacticLayers);
            return;
        }

        NPC finalTarget = FindNearestTarget(mouseTarget, 760f);
        Vector2 focus = finalTarget?.Center ?? mouseTarget;
        int returnDamage = 0;
        for (int i = 0; i < anchors.Count; i++)
            SpawnThunderCircuitLink(swing, owner, anchors[i].Center, focus, returnDamage,
                i * 2, 1.05f + i * 0.06f, true);

        // The return currents identify the target first. Supporting and tracked bolts begin the storm,
        // while FranThunderCircuitFinisher delays the single heaviest focal strike until the circuit closes.
        int delay = 7 + anchors.Count * 2;
        SpawnThunderRain(owner, swing, focus, combo, tacticLayers, delay);
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
        int tacticLayers, int finalDelay = 0)
    {
        bool finisher = combo == 3;
        int supporting = (finisher
            ? FranBalance.GetInt(nameof(FranBalance.ThunderChainFinisherSupportingBoltCount), FranBalance.ThunderChainFinisherSupportingBoltCount)
            : FranBalance.GetInt(nameof(FranBalance.ThunderChainNormalSupportingBoltCount), FranBalance.ThunderChainNormalSupportingBoltCount)) + tacticLayers - 1;
        int tracked = (finisher
            ? FranBalance.GetInt(nameof(FranBalance.ThunderChainFinisherTrackedTargetCount), FranBalance.ThunderChainFinisherTrackedTargetCount)
            : FranBalance.GetInt(nameof(FranBalance.ThunderChainNormalTrackedTargetCount), FranBalance.ThunderChainNormalTrackedTargetCount)) + tacticLayers - 1;
        int count = Math.Max(2, 1 + supporting + tracked);
        int slots = FranBalance.GetAvailableTacticSlots();
        int fullCount = Math.Max(2, count + (slots - tacticLayers) * 2);
        List<NPC> targets = new();
        foreach (NPC npc in Main.ActiveNPCs)
            if (npc.CanBeChasedBy() && npc.DistanceSQ(focus) <= 650f * 650f &&
                npc.DistanceSQ(owner.Center) <= 980f * 980f)
                targets.Add(npc);
        targets.Sort((a, b) => a.DistanceSQ(focus).CompareTo(b.DistanceSQ(focus)));

        for (int i = 0; i < count; i++)
        {
            // 每轮齐射只给每个 NPC 一个标记，不足的雷落在附近空处，不能重复锁定主目标。
            NPC target = i < targets.Count ? targets[i] : null;
            Vector2 impact = target?.Center ?? FindEmptyThunderImpact(focus, targets, i, count);
            float multiplier = FranTacticOutputBudget.ThunderPerSwingRatio(i == 0, fullCount,
                Main.raining && Main.IsItStorming);
            int totalDamage = Math.Max(2, (int)(swing.damage * multiplier));
            if (i == 0 && finisher)
            {
                // 第四刀的主雷继续等电路收束后落下，避免再叠加一条无预算的重复主雷。
                Projectile.NewProjectile(swing.GetSource_FromThis(), impact, Vector2.Zero,
                    ModContent.ProjectileType<FranThunderCircuitFinisher>(), totalDamage,
                    swing.knockBack * 0.48f, owner.whoAmI, finalDelay,
                    target?.whoAmI + 1 ?? 0, tacticLayers);
                continue;
            }
            Vector2 spawn = impact + new Vector2(Main.rand.NextFloat(-130f, 130f), -720f);
            Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, Vector2.Zero,
                ModContent.ProjectileType<FranThunderBudgetStrike>(), totalDamage, 0f, owner.whoAmI,
                impact.X, impact.Y, target?.whoAmI + 1 ?? 0);
            Projectile.NewProjectile(swing.GetSource_FromThis(), impact, Vector2.Zero,
                ModContent.ProjectileType<FranThunderDropVisual>(), 0, 0f, owner.whoAmI,
                spawn.X, spawn.Y, Main.rand.NextFloat(100f) + (i == 0 ? finisher ? 2000f : 1000f : 0f));
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
