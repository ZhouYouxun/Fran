using System;
using System.Collections.Generic;
using FranMod.Content.Players;
using FranMod.Content.Projectiles;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace FranMod.Content.Items;

public sealed class Teacher : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.BreakerBlade}";

    public override void SetDefaults()
    {
        Item.width = 74;
        Item.height = 74;
        Item.damage = 20;
        Item.DamageType = DamageClass.Melee;
        Item.knockBack = 7f;
        Item.useTime = 25;
        Item.useAnimation = 25;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.noUseGraphic = true;
        Item.noMelee = true;
        Item.channel = true;
        Item.autoReuse = true;
        Item.shoot = ModContent.ProjectileType<TeacherSwingRebuild>();
        Item.shootSpeed = 1f;
        Item.rare = ItemRarityID.Quest;
        Item.value = 0;
        // A favorited item cannot be dropped, trashed, sold, quick-stacked into a chest,
        // or placed in an item frame. Refreshing this in UpdateInventory prevents the
        // player from manually removing the binding.
        Item.favorited = true;
    }

    public override void UpdateInventory(Player player) => Item.favorited = true;

    public override bool CanResearch() => false;

    public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
    {
        // Teacher grows with world progression without permanently mutating the item.
        damage.Base += Core.FranSkillInfo.BaseDamage - Item.damage;
    }

    public override bool AltFunctionUse(Player player) => true;

    public override bool CanUseItem(Player player)
    {
        if (player.altFunctionUse == 2)
        {
            Item.channel = false;
            Item.shoot = ProjectileID.None;
            return false;
        }

        Item.useTime = 25;
        Item.useAnimation = Item.useTime;
        Item.channel = true;
        Item.shoot = ModContent.ProjectileType<TeacherSwingRebuild>();
        return player.ownedProjectileCounts[Item.shoot] == 0;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
        int type, int damage, float knockback)
    {
        if (player.altFunctionUse == 2)
            return false;

        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        int combo = fran.ConsumeNextCombo();
        damage = Math.Max(1, (int)(damage * (combo switch
        {
            1 => 0.82f,
            2 => 1.38f,
            3 => 1.48f,
            _ => 1f
        })));
        Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
        float attackSpeed = Math.Max(0.25f, player.GetAttackSpeed(DamageClass.Melee));
        float baseDuration = combo switch
        {
            0 => 21f,
            1 => 22f,
            2 => 19f,
            _ => 25f
        };
        int duration = Math.Max(12, (int)MathF.Round(baseDuration / attackSpeed));
        FranSkill skill = fran.HasSelectedSkill ? fran.SelectedSkill : FranSkill.None;
        int packedImprint = skill == FranSkill.None ? 0 : (int)skill + 1;
        Projectile.NewProjectile(source, player.MountedCenter, aim, type, damage, knockback, player.whoAmI,
            combo, duration, packedImprint);
        return false;
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        int stage = Core.FranSkillInfo.CurrentStage;
        tooltips.Add(new TooltipLine(Mod, "Stage", Language.GetTextValue("Mods.Fran.Stages.Current",
            Language.GetTextValue($"Mods.Fran.Stages.Stage{stage}.Summary")))
        { OverrideColor = new Color(145, 220, 255) });
        if (stage < 4)
        {
            tooltips.Add(new TooltipLine(Mod, "NextStage", Language.GetTextValue("Mods.Fran.Stages.Next",
                Language.GetTextValue($"Mods.Fran.Stages.Stage{stage + 1}.Unlock"),
                Language.GetTextValue($"Mods.Fran.Stages.Stage{stage + 1}.Summary")))
            { OverrideColor = new Color(180, 190, 205) });
        }
    }
}
