using Terraria;
using Terraria.ModLoader;
using FranMod.Core;

namespace FranMod.Content.Buffs;

public sealed class BlackCatWarriorBuff : ModBuff
{
    public override string Texture => "Fran/Textures/Buffs/黑猫Buff";

    public override void SetStaticDefaults()
    {
        Main.buffNoSave[Type] = true;
        Main.buffNoTimeDisplay[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.noFallDmg = true;
        player.nightVision = true;
        player.GetDamage(DamageClass.Melee) +=
            FranBalance.GetFloat(nameof(FranBalance.FranMeleeDamageBonus), FranBalance.FranMeleeDamageBonus);
        player.GetAttackSpeed(DamageClass.Melee) +=
            FranBalance.GetFloat(nameof(FranBalance.FranMeleeAttackSpeedBonus), FranBalance.FranMeleeAttackSpeedBonus);
    }
}
