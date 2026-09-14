namespace FranMod.Core;

// Persisted/networked IDs: append new tactics before Count; never reorder existing values.
public enum FranSkill : byte
{
    TelekineticCatapult,
    SkillTaker,
    MagicBarrier,
    MultipleBodyCreation,
    BlueResonance,
    ThunderChain,
    PressurizedQuickdraw,
    ThorsHammer,
    Count,
    None = byte.MaxValue
}
