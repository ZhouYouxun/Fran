using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Core;

/// <summary>
/// Teacher's permanent story rarity. The base color remains animated in inventory and chat,
/// while Teacher's tooltip supplies the more detailed per-character prism rendering.
/// </summary>
public sealed class TeacherPrismaticRarity : ModRarity
{
    public override Color RarityColor =>
        FranTooltipTextEffects.PrismaticColor(Main.GlobalTimeWrappedHourly * 0.72f);

    // Teacher is a singular bonded weapon, so reforging must not move it into another rarity tier.
    public override int GetPrefixedRarity(int offset, float valueMult) => Type;
}
