using System;
using System.Collections.Generic;
using FranMod.Core;

namespace FranMod.Content.Projectiles;

internal enum FranTelekineticShotKind { Core, Wave, Laser, Planet }

internal readonly record struct FranTelekineticShot(
    FranTelekineticShotKind Kind, float Offset, float Angle, float Scale, float Weight,
    bool SuppressLaunchBurst = false, int Count = 1);

/// <summary>发射与预算共用同一份四刀配方，防止修改弹幕数量后忘记同步配重。</summary>
internal static class FranTelekineticPattern
{
    internal static void Fill(List<FranTelekineticShot> shots, int stage, int combo, int tacticLayers, int facing)
    {
        shots.Clear();
        void AddCore(float offset, float weight) => shots.Add(new(FranTelekineticShotKind.Core,
            offset, 0f, 1f, stage >= 2 ? FranBalance.GetFloat(
                nameof(FranBalance.TelekineticHardmodeCoreDamageMultiplier),
                FranBalance.TelekineticHardmodeCoreDamageMultiplier) : weight));
        void AddWave(float offset, float angle, float scale, float weight) =>
            shots.Add(new(FranTelekineticShotKind.Wave, offset, angle, scale,
                stage >= 2 ? FranBalance.GetFloat(nameof(FranBalance.TelekineticHardmodeFoldBladeDamageMultiplier),
                    FranBalance.TelekineticHardmodeFoldBladeDamageMultiplier) : weight));
        void AddLaser(float offset, float scale, float weight, bool suppressLaunchBurst) =>
            shots.Add(new(FranTelekineticShotKind.Laser, offset, 0f, scale,
                stage >= 2 ? FranBalance.GetFloat(nameof(FranBalance.TelekineticHardmodeLaserDamageMultiplier),
                    FranBalance.TelekineticHardmodeLaserDamageMultiplier) : weight, suppressLaunchBurst));
        void AddPlanets() => shots.Add(new(FranTelekineticShotKind.Planet, 0f, 0f, 1f,
            FranBalance.GetFloat(nameof(FranBalance.TelekineticFinisherPlanetDamageMultiplier),
                FranBalance.TelekineticFinisherPlanetDamageMultiplier), Count: Telekinetic_GravityStarfall.PlanetCount));

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
                    AddCore(0f, single);
                    break;
                case 1:
                    AddCore(facing * 12f, single);
                    break;
                case 2:
                    AddCore(-17f, pair);
                    AddCore(17f, pair);
                    break;
                default:
                    AddCore(0f, heavy);
                    break;
            }
        }
        else if (stage == 1)
        {
            switch (combo)
            {
                case 0:
                    AddCore(0f, single);
                    break;
                case 1:
                    AddWave(-facing * 30f,
                        -facing * 0.08f, 1f, single);
                    break;
                case 2:
                    AddCore(-19f, triple);
                    AddCore(19f, triple);
                    AddWave(0f, 0f, 0.90f, triple);
                    break;
                default:
                    // 冲击波始终只有一枚：把原先双排的总伤害合并到居中波体。
                    AddWave(0f, 0f, 1.02f, pair * 2f);
                    break;
            }
        }
        else if (stage == 2)
        {
            switch (combo)
            {
                case 0:
                    AddLaser(0f, 0.96f, single, false);
                    break;
                case 1:
                    AddLaser(0f, 0.96f, pair, false);
                    AddWave(-facing * 42f,
                        facing * 0.07f, 0.88f, pair);
                    break;
                case 2:
                    AddLaser(-26f, 0.90f, triple, false);
                    AddLaser(26f, 0.90f, triple, true);
                    AddCore(0f, triple);
                    break;
                default:
                    AddLaser(0f, 1.34f, heavy, false);
                    break;
            }
        }
        else
        {
            switch (combo)
            {
                case 0:
                    AddLaser(0f, 0.98f, pair, false);
                    AddCore(0f, pair);
                    break;
                case 1:
                    AddLaser(0f, 0.98f, triple, false);
                    // 后期编排也不再并排复制冲击波，仍只发射一枚居中波体。
                    AddWave(0f, 0f, 0.88f, triple * 2f);
                    break;
                case 2:
                    AddLaser(-32f, 0.92f, quartet, false);
                    AddLaser(32f, 0.92f, quartet, true);
                    AddCore(-62f, quartet);
                    AddCore(62f, quartet);
                    break;
                default:
                    if (stage == 3)
                    {
                        AddLaser(0f, 1.28f, triple, false);
                        AddWave(0f, 0f, 0.92f, triple * 2f);
                    }
                    else
                    {
                        AddLaser(0f, 1.34f, heavy, false);
                        AddPlanets();
                    }
                    break;
            }
        }

        // 第三维被刻意压成唯一语义：A 是上方当前配方，每层 B 只多一枚侧翼念压核。
        float spacing = combo == 3 ? 38f : 31f;
        for (int i = 0; i < tacticLayers - 1; i++)
        {
            float side = (i % 2 == 0 ? 1f : -1f) * (i / 2 + 1) * spacing * facing;
            AddCore(side, FranBalance.GetFloat(nameof(FranBalance.TelekineticExtraLayerProjectileDamageMultiplier),
                FranBalance.TelekineticExtraLayerProjectileDamageMultiplier));
        }


    }

    internal static float GetCycleWeight(int stage, int tacticLayers)
    {
        float total = 0f;
        List<FranTelekineticShot> shots = new(16);
        for (int combo = 0; combo < 4; combo++)
        {
            Fill(shots, stage, combo, tacticLayers, 1);
            foreach (FranTelekineticShot shot in shots)
                total += FranBalance.GetComboDamageMultiplier(combo) * shot.Weight * shot.Count *
                    GetHitWeight(shot.Kind);
        }
        return total;
    }

    internal static float GetHitWeight(FranTelekineticShotKind kind)
    {
        if (kind == FranTelekineticShotKind.Planet)
            return 1f + FranBalance.GetFloat(nameof(FranBalance.TelekineticPlanetExplosionDamageMultiplier),
                FranBalance.TelekineticPlanetExplosionDamageMultiplier);
        if (kind != FranTelekineticShotKind.Wave)
            return 1f; // 念核与激光对同一目标只能命中一次，穿透其他目标另计。
        int hits = Math.Clamp(FranBalance.GetInt(nameof(FranBalance.TelekineticBudgetWaveSingleTargetHits),
            FranBalance.TelekineticBudgetWaveSingleTargetHits), 1, Math.Max(1, FranBalance.GetInt(
                nameof(FranBalance.TelekineticFoldBladeHitLimit), FranBalance.TelekineticFoldBladeHitLimit)));
        float retention = Math.Clamp(FranBalance.GetFloat(nameof(FranBalance.TelekineticFoldBladeHitDamageRetention),
            FranBalance.TelekineticFoldBladeHitDamageRetention), 0.01f, 1f);
        float total = 0f;
        float next = 1f;
        for (int i = 0; i < hits; i++)
        {
            total += next;
            next *= retention;
        }
        return total;
    }

    internal static float GetDamageScale(int stage, int fullLoadoutSlots)
    {
        if (stage < 2)
            return 1f; // 肉前已经实测的倍率原样保留。
        float reference = GetCycleWeight(1, FranBalance.GetTelekineticReferenceSlots());
        float current = GetCycleWeight(stage, Math.Max(1, fullLoadoutSlots));
        return current > 0f ? reference * FranBalance.GetTelekineticCycleGrowth(stage) / current : 0f;
    }
}
