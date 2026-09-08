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
    private const string GoldenPromptLineName = "GoldenPrompt";
    private const string TacticSelectionLineName = "TacticSelection";
    private const string FullLoadoutStatusLineName = "FullLoadoutStatus";

    public override string Texture => FranTeacherVisuals.TexturePath;

    public override void SetDefaults()
    {
        Item.width = 74;
        Item.height = 74;
        Item.damage = FranBalance.GetInitialBaseDamage();
        Item.DamageType = DamageClass.Melee;
        Item.knockBack = FranBalance.GetFloat(nameof(FranBalance.TeacherKnockback), FranBalance.TeacherKnockback);
        Item.useTime = FranBalance.GetInt(nameof(FranBalance.TeacherUseTimeFrames), FranBalance.TeacherUseTimeFrames);
        Item.useAnimation = Item.useTime;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.noUseGraphic = true;
        Item.noMelee = true;
        Item.channel = true;
        Item.autoReuse = true;
        Item.shoot = ModContent.ProjectileType<TeacherSwingRebuild>();
        Item.shootSpeed = 1f;
        Item.rare = ModContent.RarityType<TeacherPrismaticRarity>();
        Item.value = 0;
        // A favorited item cannot be dropped, trashed, sold, quick-stacked into a chest,
        // or placed in an item frame. Refreshing this in UpdateInventory prevents the
        // player from manually removing the binding.
        Item.favorited = true;
    }

    public override void UpdateInventory(Player player) => Item.favorited = true;

    public override bool CanResearch() => false;

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.Wood, 10)
            .AddIngredient(ItemID.StoneBlock, 20)
            .AddIngredient(ItemID.Gel, 5)
            .AddTile(TileID.WorkBenches)
            .Register();
    }

    public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
    {
        damage.Base += FranBalance.GetBaseDamage(player) - Item.damage;
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

        Item.useTime = FranBalance.GetInt(nameof(FranBalance.TeacherUseTimeFrames), FranBalance.TeacherUseTimeFrames);
        Item.useAnimation = Item.useTime;
        Item.knockBack = FranBalance.GetFloat(nameof(FranBalance.TeacherKnockback), FranBalance.TeacherKnockback);
        Item.channel = true;
        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        Item.shoot = fran.IsFullTacticLoadout(FranSkill.PressurizedQuickdraw)
            ? ModContent.ProjectileType<FranQuickdrawDiscHoldout>()
            : ModContent.ProjectileType<TeacherSwingRebuild>();
        return player.ownedProjectileCounts[ModContent.ProjectileType<TeacherSwingRebuild>()] == 0 &&
            player.ownedProjectileCounts[ModContent.ProjectileType<FranQuickdrawDiscHoldout>()] == 0;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
        int type, int damage, float knockback)
    {
        if (player.altFunctionUse == 2)
            return false;

        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
        if (fran.IsFullTacticLoadout(FranSkill.PressurizedQuickdraw))
        {
            // 全选压缩居合是一套独立的持续形态，不消耗也不推进普通四段连招。
            fran.ComboIndex = 0;
            fran.ComboReset = 0;
            int layers = Math.Clamp(fran.GetSkillStacks(FranSkill.PressurizedQuickdraw),
                1, FranPlayer.MaximumTacticSlots);
            Projectile.NewProjectile(source, player.MountedCenter, aim,
                ModContent.ProjectileType<FranQuickdrawDiscHoldout>(), damage, knockback,
                player.whoAmI, layers);
            return false;
        }

        FranEvolutionPlayer evolution = player.GetModPlayer<FranEvolutionPlayer>();
        int combo = fran.ConsumeNextCombo();
        damage = Math.Max(1, (int)(damage * FranBalance.GetComboDamageMultiplier(combo)));
        float attackSpeed = Math.Max(0.25f, player.GetAttackSpeed(DamageClass.Melee));
        float baseDuration = FranBalance.GetComboBaseDuration(combo);
        // 第四刀快速直推；时长仍写入 ai[1]，使持剑、战术和远端玩家共用同一时钟。
        if (combo == 3)
            baseDuration *= 0.74f;
        float evolutionDuration = evolution.ConsumeSwingDurationMultiplier(combo);
        int duration = Math.Max(12, (int)MathF.Round(baseDuration / attackSpeed * evolutionDuration));
        FranSkill skill = fran.HasSelectedSkill ? fran.SelectedSkill : FranSkill.None;
        int packedImprint = skill == FranSkill.None ? 0 : (int)skill + 1;
        int projectileIndex = Projectile.NewProjectile(source, player.MountedCenter, aim, type, damage, knockback, player.whoAmI,
            combo, duration, packedImprint);
        if (Main.projectile.IndexInRange(projectileIndex) &&
            Main.projectile[projectileIndex].ModProjectile is TeacherSwingRebuild swing)
        {
            swing.SetTactics(fran.GetActiveTacticSnapshot());
            Main.projectile[projectileIndex].netUpdate = true;
        }
        if (combo == 0 && evolution.TryConsumeEvolutionEcho() && player.whoAmI == Main.myPlayer)
            Projectile.NewProjectile(source, player.MountedCenter, aim,
                ModContent.ProjectileType<FranEvolutionEchoSlash>(),
                Math.Max(1, (int)(damage * evolution.EvolutionEchoDamageMultiplier)),
                knockback * 0.35f, player.whoAmI, duration);
        return false;
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        FranPlayer fran = Main.LocalPlayer.GetModPlayer<FranPlayer>();
        InsertTacticStatus(tooltips, fran);

        if (FranEvolutionInfo.SystemEnabled)
            tooltips.Add(new TooltipLine(Mod, "Evolution", Language.GetTextValue("Mods.Fran.Evolution.Tooltip"))
            { OverrideColor = new Color(195, 178, 245) });

        tooltips.Add(new TooltipLine(Mod, "FranDashStatus", BuildDashStatus())
        { OverrideColor = new Color(130, 235, 195) });

        bool flightEnabled = Main.hardMode && !fran.HasExternalWings;
        tooltips.Add(new TooltipLine(Mod, "FranFlightStatus", BuildFlightStatus(fran))
        { OverrideColor = flightEnabled ? new Color(105, 205, 255) : new Color(165, 175, 190) });

        // Intentionally kept as the requested placeholder until its final localization is supplied.
        tooltips.Add(new TooltipLine(Mod, GoldenPromptLineName,
            Language.GetTextValue("Mods.Fran.Items.Teacher.GoldenPrompt"))
        { OverrideColor = new Color(241, 190, 76) });
    }

    private void InsertTacticStatus(List<TooltipLine> tooltips, FranPlayer fran)
    {
        int availableSlots = Math.Min(fran.AvailableTacticSlots, FranPlayer.MaximumTacticSlots);
        int activeCount = fran.GetActiveTacticCount();
        TooltipLine selectionLine = new(Mod, TacticSelectionLineName,
            Language.GetTextValue("Mods.Fran.Items.Teacher.TacticSelection", activeCount, availableSlots))
        { OverrideColor = new Color(150, 215, 255) };

        FranSkill fullLoadoutSkill = fran.GetEquippedTactic(0);
        bool fullLoadoutActive = FranSkillInfo.IsRealSkill(fullLoadoutSkill) &&
            fran.IsFullTacticLoadout(fullLoadoutSkill);
        string fullLoadoutText = fullLoadoutActive
            ? Language.GetTextValue("Mods.Fran.Items.Teacher.FullLoadoutActive",
                FranSkillInfo.Name(fullLoadoutSkill))
            : Language.GetTextValue("Mods.Fran.Items.Teacher.FullLoadoutInactive");
        TooltipLine fullLoadoutLine = new(Mod, FullLoadoutStatusLineName, fullLoadoutText)
        {
            OverrideColor = fullLoadoutActive
                ? FranSkillInfo.Color(fullLoadoutSkill)
                : new Color(155, 165, 178)
        };

        int rightClickLineIndex = tooltips.FindIndex(line =>
            line.Mod == "Terraria" && line.Name == "Tooltip1");
        int insertionIndex = rightClickLineIndex >= 0
            ? rightClickLineIndex + 1
            : Math.Min(2, tooltips.Count);
        tooltips.Insert(insertionIndex, selectionLine);
        tooltips.Insert(insertionIndex + 1, fullLoadoutLine);
    }

    private static string BuildDashStatus()
    {
        if (TryGetAssignedDashKey(out string key))
            return Language.GetTextValue("Mods.Fran.Abilities.Dash.Bound", key);
        return Language.GetTextValue("Mods.Fran.Abilities.Dash.Unbound");
    }

    private static bool TryGetAssignedDashKey(out string assignedKey)
    {
        assignedKey = string.Empty;
        if (Main.dedServ || FranModMain.DashKey is null)
            return false;

        try
        {
            foreach (string key in FranModMain.DashKey.GetAssignedKeys())
            {
                if (string.IsNullOrWhiteSpace(key) || key.Equals("None", StringComparison.OrdinalIgnoreCase))
                    continue;
                assignedKey = key;
                return true;
            }
        }
        catch
        {
            // 按键设置面板重建输入配置的瞬间，当作未绑定处理即可。
        }
        return false;
    }

    private static string BuildFlightStatus(FranPlayer fran)
    {
        string stateKey = !Main.hardMode
            ? "Locked"
            : fran.HasExternalWings ? "WingsEquipped" : "Enabled";
        return Language.GetTextValue($"Mods.Fran.Abilities.Flight.{stateKey}");
    }

    public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
    {
        if (line.Mod == "Terraria" && line.Name == "ItemName")
        {
            FranTooltipTextEffects.DrawTeacherPrismaticName(line);
            return false;
        }

        if (line.Mod != Mod.Name || line.Name != GoldenPromptLineName)
            return true;

        FranTooltipTextEffects.DrawGolden(line);
        return false;
    }
}
