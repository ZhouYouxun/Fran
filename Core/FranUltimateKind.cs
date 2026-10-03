namespace FranMod.Core;

// Persisted IDs: never reorder existing choices.
public enum FranUltimateKind : byte
{
    BlackHeavenlyTiger,
    Singularity,
    Vigil
}

public static class FranUltimateInfo
{
    // Keep the later implementations in the binary while making only the launch ultimate selectable.
    public static bool IsAvailable(FranUltimateKind kind) =>
        kind is FranUltimateKind.BlackHeavenlyTiger or FranUltimateKind.Singularity or FranUltimateKind.Vigil;

    public static bool IsSelectable(FranUltimateKind kind) => kind == FranUltimateKind.BlackHeavenlyTiger;
}
