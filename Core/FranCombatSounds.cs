using Terraria.Audio;

namespace FranMod.Core;

// Authored combat sounds intentionally retain SoundStyle's default volume (1.0) and pitch (0.0).
internal static class FranCombatSounds
{
    internal static readonly SoundStyle 多重分身命中 = new("Fran/Sounds/FranCombat/多重分身命中");
    internal static readonly SoundStyle 雷神之锤命中电弧 = new("Fran/Sounds/FranCombat/雷神之锤命中电弧");
    internal static readonly SoundStyle 雷神之锤命中轨道炮 = new("Fran/Sounds/FranCombat/雷神之锤命中轨道炮");
    internal static readonly SoundStyle 雷神之锤显现 = new("Fran/Sounds/FranCombat/雷神之锤显现");
    internal static readonly SoundStyle 蝴蝶开始追踪 = new("Fran/Sounds/FranCombat/蝴蝶开始追踪");
    internal static readonly SoundStyle 念动光波消散 = new("Fran/Sounds/FranCombat/念动光波消散");
    internal static readonly SoundStyle 大招蓄势完成 = new("Fran/Sounds/FranCombat/大招蓄势完成");
}
