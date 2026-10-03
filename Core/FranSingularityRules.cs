using System;

namespace FranMod.Core;

// Pure timing/damage rules shared by the controller and boundary tests. Flight time starts
// when the hole leaves the blade, independently of the preceding charge animation.
public static class FranSingularityRules
{
    public const int ChargeFrames = 120;
    public const int MinimumFlightFrames = 300;
    public const int MaximumFlightFrames = 900;
    public const int CollapseFrames = 12;
    public const int AftermathFrames = 36;
    public const float MaximumScale = 0.75f;
    public const float DamageRadius = 180f;
    public const float PullRadius = 720f;
    public const float ExplosionRadius = 900f;
    public const float MaximumRange = 800f;

    public static bool CanDetonate(int flightFrames) => flightFrames >= MinimumFlightFrames;
    public static bool ShouldDetonate(int flightFrames, bool requested, bool ownerDead, bool weaponChanged) =>
        flightFrames >= MaximumFlightFrames || CanDetonate(flightFrames) && (requested || ownerDead || weaponChanged);

    // Distribute rounding over ten ticks: a 23-panel sword deals 23 base damage in ten ticks,
    // rather than permanently rounding each tick down to 2 (or up to 3).
    public static int TickDamage(int panel, int tick) => (int)(
        (long)Math.Max(0, panel) * (tick % 10 + 1) / 10 - (long)Math.Max(0, panel) * (tick % 10) / 10);
    public static int ExplosionDamage(int panel) => (int)Math.Min(int.MaxValue, (long)Math.Max(1, panel) * 6);
}
