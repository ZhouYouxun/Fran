using Terraria.Audio;

namespace FranMod.Core;

// Authored combat sounds intentionally retain SoundStyle's default volume (1.0) and pitch (0.0).
internal static class FranCombatSounds
{
    internal static readonly SoundStyle MultipleBodyHit = new("Fran/Sounds/FranCombat/MultipleBodyHit");
    internal static readonly SoundStyle ThorsHammerHitArc = new("Fran/Sounds/FranCombat/ThorsHammerHitArc");
    internal static readonly SoundStyle ThorsHammerHitOrbitalCannon = new("Fran/Sounds/FranCombat/ThorsHammerHitOrbitalCannon");
    internal static readonly SoundStyle ThorsHammerAppearance = new("Fran/Sounds/FranCombat/ThorsHammerAppearance");
    internal static readonly SoundStyle ButterflyBeginTracking = new("Fran/Sounds/FranCombat/ButterflyBeginTracking");
    internal static readonly SoundStyle TelekineticWaveDeath = new("Fran/Sounds/FranCombat/TelekineticWaveDeath");
    internal static readonly SoundStyle UltimateReady = new("Fran/Sounds/FranCombat/UltimateReady");
}
