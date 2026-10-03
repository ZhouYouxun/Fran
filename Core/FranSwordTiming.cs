using System;
using FranMod.Content.Players;
using Terraria;

namespace FranMod.Core;

internal static class FranSwordTiming
{
    // Both templates advance the same four beats, including the shortened fourth beat
    // and the same attack-speed/evolution modifiers. Hammer uses the same fourth-beat event.
    internal static int ConsumeBeatDuration(Player owner, int combo)
    {
        float duration = FranBalance.GetComboBaseDuration(combo) * (combo == 3 ? 0.74f : 1f);
        float evolution = owner.GetModPlayer<FranEvolutionPlayer>().ConsumeSwingDurationMultiplier(combo);
        return Math.Max(12, (int)MathF.Round(duration / Math.Max(0.25f,
            owner.GetWeaponAttackSpeed(owner.HeldItem)) * evolution));
    }
}
