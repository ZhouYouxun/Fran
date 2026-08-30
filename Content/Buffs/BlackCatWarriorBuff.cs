using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Buffs;

public sealed class BlackCatWarriorBuff : ModBuff
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.CatEars}";

    public override void SetStaticDefaults()
    {
        Main.buffNoSave[Type] = true;
        Main.buffNoTimeDisplay[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.noFallDmg = true;
        player.nightVision = true;
        player.GetDamage(DamageClass.Melee) += 0.10f;
        player.GetAttackSpeed(DamageClass.Melee) += 0.10f;
    }
}
