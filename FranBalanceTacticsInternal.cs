namespace FranMod.Core;

/// <summary>
/// 战术的内部平衡参数。
/// <para>常规进度、战术解锁、槽位和冲刺速度请在 FranBalance.cs 调整；本文件由战斗实现维护。</para>
/// </summary>
public static partial class FranBalance
{
    // 仅剑身的蠕虫 Boss 和困难模式前多目标伤害规则。
    public static float SwingWormBossDamageMultiplier = 0.70f;
    public const int SwingBossSafeTargetCount = 4;
    public const float SwingBossExtraTargetPenalty = 0.06f;
    public const float SwingBossMinimumDamageMultiplier = 0.50f;
    public const int SwingNormalSafeTargetCount = 15;
    public const float SwingNormalExtraTargetPenalty = 0.04f;
    public const float SwingNormalMinimumDamageMultiplier = 0.60f;

    public static float GetSwingCrowdDamageMultiplier(int targetCount, bool bossFight, bool hardmode)
    {
        if (hardmode)
            return 1f;
        int safeCount = bossFight ? SwingBossSafeTargetCount : SwingNormalSafeTargetCount;
        float penalty = bossFight ? SwingBossExtraTargetPenalty : SwingNormalExtraTargetPenalty;
        float minimum = bossFight ? SwingBossMinimumDamageMultiplier : SwingNormalMinimumDamageMultiplier;
        return System.Math.Max(minimum, 1f - System.Math.Max(0, targetCount - safeCount) * penalty);
    }

    // 黑天虎大招的内部动作和伤害参数。
    public static float UltimateStrikeDamageMultiplier = 8f;
    public static int UltimateNormalStrikeCount = 36;
    public static int UltimateStartingStrikeFrames = 6;
    public static int UltimateStartingDriftFrames = 4;
    public static int UltimateEndingStrikeFrames = 1;
    public static int UltimateEndingDriftFrames = 1;
    public static int UltimateFinalPauseFrames = 20;
    public static int UltimateFinalStrikeFrames = 9;
    public static int UltimateDarknessFadeInFrames = 200;
    public static int UltimateDarknessFadeOutFrames = 400;
    public static float UltimateDarknessMaximumOpacity = 0.62f;

    // =====================================================================
    // 八种战术的输出预算、单位权重与重复叠层
    // =====================================================================
    // 单位 DamageMultiplier 是校准前的相对权重；弹幕最终倍率还会乘输出预算的统一校准系数。
    // 同一战术重复装备采用 A+B+B：第一层提供完整机制 A，后续每层重复提供相同增量 B。
    public static int[] TacticCooldownFrames =
    {
        0, 0, 150, 0, 0, 0, 0, 0
    };

    // 战术一：念动弹射。
    public static float TelekineticSingleProjectileDamageMultiplier = 0.72f;
    public static float TelekineticTwoShellDamageMultiplier = 0.42f;
    public static float TelekineticThreeShellDamageMultiplier = 0.31f;
    public static float TelekineticManyShellDamageMultiplier = 0.24f;
    public static float TelekineticHeavyProjectileDamageMultiplier = 0.88f;
    public static float TelekineticExtraLayerProjectileDamageMultiplier = 0.24f;
    public static float[] TelekineticFullLoadoutCycleGrowth =
    {
        1.20f, 1.30f, 1.40f
    };
    public static int TelekineticBudgetWaveSingleTargetHits = 4;

    // 以同阶段全选念动的一轮四刀为 X 的额外输出预算。
    public static float TacticSkillTakerBudgetRatio = 1.20f;
    public static float TacticMultipleBodyBudgetRatio = 1.25f;
    public static float TacticBlueResonanceBudgetRatio = 0.70f;
    public static float TacticThunderSingleBudgetRatio = 0.575f;
    public static float TacticThunderGroupBudgetRatio = 1.15f;
    public static float TacticQuickdrawBudgetRatio = 3.00f;
    public static float TacticHammerBudgetRatio = 1.80f;
    public static float TacticHammerRainBudgetRatio = 3.60f;
    public static int ThunderImpactExplosionSize = 75;
    public static int ThunderImpactExplosionLifetime = 2;
    public static int ThunderImpactExplosionPenetration = 99;
    public static int HammerImpactParticleMultiplier = 7;
    public static float HammerScreenShakeMultiplier = 1.5f;

    // 困难模式念动单位之间的相对分配权重。
    public static float TelekineticHardmodeLaserDamageMultiplier = 2f;
    public static float TelekineticHardmodeFoldBladeDamageMultiplier = 0.6f;
    public static float TelekineticHardmodeCoreDamageMultiplier = 1f;
    public static float TelekineticCorePreHardmodeSpeedMultiplier = 0.70f;
    public static float TelekineticCoreHardmodeSpeedMultiplier = 0.85f;
    public static int TelekineticFoldBladeHitLimit = 10;
    public static float TelekineticFoldBladePreHardmodeScaleMultiplier = 0.75f;
    public static float TelekineticFoldBladeLaunchSpeed = 60f;
    public static float TelekineticFoldBladeFlightDeceleration = 0.94f;
    public static float TelekineticFoldBladeHitSpeedRetention = 0.75f;
    public static float TelekineticFoldBladeHitDamageRetention = 0.75f;
    public static int[] TelekineticLaserDamagingHits =
    {
        1, 3, 3, 5
    };
    public static float TelekineticFinisherPlanetDamageMultiplier = 0.17f;
    public static float TelekineticPlanetExplosionDamageMultiplier = 0.75f;

    // 战术二：技能夺取者。
    public static float SkillTakerMinimumDamageMultiplier = 1.06f;
    public static float SkillTakerMaximumDamageMultiplier = 1.18f;
    public static float SkillTakerFullLoadoutMaximumDamageMultiplier = 1.42f;
    public static int SkillTakerBaseRampFrames = 420;
    public static int SkillTakerExtraLayerRampReductionFrames = 60;
    public static int SkillTakerBaseDecayFrames = 180;
    public static int SkillTakerExtraLayerDecayFrames = 60;
    public static int SkillTakerContinuousHitGraceFrames = 30;
    public static int SkillTakerBaseWispCount = 2;
    public const int SkillTakerMaximumWispsPerSwing = 20;
    public static float SkillTakerWispDamageMultiplier = 0.24f;
    public static int SkillTakerStolenContactDamageDurationFrames = 5 * 60;
    public static float SkillTakerStolenContactDamageWeaponCapRatio = 0.50f;

    // 战术三：魔力屏障。
    public static int MagicBarrierMaximumIntegrity = 5;
    public static int MagicBarrierExtraLayerIntegrity = 1;
    public static int MagicBarrierRecoveryDelayFrames = 180;
    public static int MagicBarrierRecoveryIntervalFrames = 90;
    public static float MagicBarrierActiveDefenseMultiplier = 2f;
    public static float MagicBarrierWeaponDamageMultiplier = 0.72f;
    public static float MagicBarrierBaseDamageReduction = 0.10f;
    public static float MagicBarrierExtraLayerDamageReduction = 0.05f;
    public static int MagicBarrierBaseDefense = 10;
    public static int MagicBarrierExtraLayerDefense = 5;
    public static float MagicBarrierFullLoadoutContactDamageMultiplier = 0.50f;
    public static int MagicBarrierFullLoadoutLifeRegen = 35;
    public static float MagicBarrierCounterDamageMultiplier = 0.86f;
    public static int MagicBarrierHitImmunityFrames = 18;

    // 战术四：多重分体。以下旧数量仅用于输出预算折算，实际每轮固定 4/2/4/6。
    public static int MultipleBodyNormalBaseCount = 2;
    public static int MultipleBodyFinisherBaseCount = 4;
    public static int MultipleBodyNormalExtraLayerCount = 1;
    public static int MultipleBodyFinisherExtraLayerCount = 2;
    public static float MultipleBodyNormalDamageMultiplier = 0.86f;
    public static float MultipleBodyFinisherDamageMultiplier = 1.24f;
    public static float MultipleBodyFullLoadoutSpeedMultiplier = 1.15f;
    public static int MultipleBodyAimFrames = 18;
    public static float MultipleBodyDashSpeed = 75.04f; // 原 112 × 0.67，降低 33%；每帧四次碰撞检查。
    public static int MultipleBodyFlightFrames = 90; // 发射后飞行寿命；经过鼠标不消失。
    public static int MultipleBodyAccelerationFrames = 10;

    // 战术五：蓝辉共鸣。
    public static int BlueResonanceScatterFrames = 10;
    public static int BlueResonanceHomingBlendFrames = 18;
    public static float BlueResonanceSpawnRadiusMinimum = 76f;
    public static float BlueResonanceSpawnRadiusMaximum = 176f;
    public static float BlueResonanceInitialDriftSpeedMinimum = 0.12f;
    public static float BlueResonanceInitialDriftSpeedMaximum = 0.38f;
    public static float BlueResonanceHomingStartingSpeed = 2.2f;
    public static float BlueResonanceEnemySpiralRadius = 132f;
    public static float BlueResonanceEnemySpiralAngularSpeed = 0.12f;
    public static int BlueResonanceEnemySpiralContractionFrames = 72;
    public static float BlueResonanceTrajectoryRandomness = 0.36f;
    public static int BlueResonanceNormalBaseCount = 3;
    public static int BlueResonanceFinisherBaseCount = 5;
    public static int BlueResonanceExtraLayerCount = 1;
    public static float BlueResonanceNormalDamageMultiplier = 0.18f;
    public static float BlueResonanceFinisherDamageMultiplier = 0.14f;
    public static float BlueResonanceEnemyFinalDamageMultiplier = 0.70f; // 蝶变共鸣仅对敌伤害再降低30%，不影响治疗。
    public static int BlueResonanceHealingLifeDivisor = 80;
    public static int BlueResonanceHealingMinimum = 2;
    public static int BlueResonanceHealingMaximum = 7;

    // 战术六：雷链。
    public static int ThunderChainNormalSupportingBoltCount = 1;
    public static int ThunderChainFinisherSupportingBoltCount = 2;
    public static int ThunderChainNormalTrackedTargetCount = 3;
    public static int ThunderChainFinisherTrackedTargetCount = 4;
    public static float ThunderChainFullLoadoutDamageMultiplier = 1.35f;
    public static float ThunderChainFullLoadoutLightningHalfWidth = 16f;

    // 战术七：压缩居合。
    public static int[] QuickdrawAirburstCounts =
    {
        5, 6, 8, 7
    };
    public static float[] QuickdrawAirburstDamageMultipliers =
    {
        0.18f, 0.16f, 0.13f, 0.15f
    };
    public static float QuickdrawExtraLayerDamageMultiplier = 0.15f;
    public static float QuickdrawFullLoadoutLifetimeMultiplier = 3f;
    public static int QuickdrawFullLoadoutKillHealMinimum = 10;
    public static int QuickdrawFullLoadoutKillHealMaximum = 20;
    public static float QuickdrawFullDiscKnockbackMultiplier = 1.30f;
    public static float QuickdrawNonFullEmissionMultiplier = 0.60f;
    public static float QuickdrawNonFullDamageMultiplier = 0.80f;
    public static float QuickdrawFullDiscMaximumAngularVelocity = 0.245f;
    public static float QuickdrawFullDiscSpinAcceleration = 0.026f;
    public static float QuickdrawFullDiscReleaseVelocityRetention = 0.91f;
    public static float QuickdrawFullDiscStopAngularVelocity = 0.009f;
    public static int QuickdrawFullDiscAirburstInterval = 4;
    public static int QuickdrawFullDiscLocalHitCooldown = 15;
    public static float QuickdrawFullDiscAirburstOpacityMultiplier = 0.70f;

    // 战术八：雷神之锤。
    public static int ThorsHammerRequiredComboRounds = 2;
    public static float ThorsHammerDropDamageMultiplier = 1.18f;
    public static float ThorsHammerExtraLayerDamageMultiplier = 0.22f;
    public static float ThorsHammerExplosionDamageMultiplier = 0.88f;
    public static float ThorsHammerBaseScreenShakePower = 13f;
}
