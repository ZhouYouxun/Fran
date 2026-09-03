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

        Item.useTime = FranBalance.GetInt(nameof(FranBalance.TeacherUseTimeFrames), FranBalance.TeacherUseTimeFrames);
        Item.useAnimation = Item.useTime;
        Item.knockBack = FranBalance.GetFloat(nameof(FranBalance.TeacherKnockback), FranBalance.TeacherKnockback);
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
        FranEvolutionPlayer evolution = player.GetModPlayer<FranEvolutionPlayer>();
        int combo = fran.ConsumeNextCombo();
        damage = Math.Max(1, (int)(damage * FranBalance.GetComboDamageMultiplier(combo)));
        Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
        float attackSpeed = Math.Max(0.25f, player.GetAttackSpeed(DamageClass.Melee));
        float baseDuration = FranBalance.GetComboBaseDuration(combo);
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
        if (FranEvolutionInfo.SystemEnabled)
            tooltips.Add(new TooltipLine(Mod, "Evolution", Language.GetTextValue("Mods.Fran.Evolution.Tooltip"))
            { OverrideColor = new Color(195, 178, 245) });

        FranPlayer fran = Main.LocalPlayer.GetModPlayer<FranPlayer>();
        bool dashEnabled = fran.FranMode && !fran.HasExternalDash;
        tooltips.Add(new TooltipLine(Mod, "FranDashStatus", BuildDashStatus(fran))
        { OverrideColor = dashEnabled ? new Color(130, 235, 195) : new Color(165, 175, 190) });

        bool flightEnabled = fran.FranMode && Main.hardMode && !fran.HasExternalWings &&
            !Main.LocalPlayer.mount.Active;
        tooltips.Add(new TooltipLine(Mod, "FranFlightStatus", BuildFlightStatus(fran))
        { OverrideColor = flightEnabled ? new Color(105, 205, 255) : new Color(165, 175, 190) });

        // Intentionally kept as the requested placeholder until its final localization is supplied.
        tooltips.Add(new TooltipLine(Mod, GoldenPromptLineName, "请输入文本")
        { OverrideColor = new Color(241, 190, 76) });
    }

    private static string BuildDashStatus(FranPlayer fran)
    {
        string stateKey = !fran.FranMode
            ? "InactiveFranMode"
            : fran.HasExternalDash ? "Overridden" : "Enabled";
        return Language.GetTextValue("Mods.Fran.Abilities.Dash.Line",
            Language.GetTextValue($"Mods.Fran.Abilities.Dash.States.{stateKey}"));
    }

    private static string BuildFlightStatus(FranPlayer fran)
    {
        int tier = FranTelekineticFlightSystem.CurrentTier;
        string progressKey = tier <= 0
            ? "Mods.Fran.Abilities.Flight.Progress.Locked"
            : FranTelekineticFlightSystem.HasInfiniteFlight
                ? "Mods.Fran.Abilities.Flight.Progress.Infinite"
                : $"Mods.Fran.Abilities.Flight.Progress.Tier{tier}";
        string progress = Language.GetTextValue(progressKey);

        string stateKey = !fran.FranMode
            ? "InactiveFranMode"
            : !Main.hardMode
                ? "Locked"
                : fran.HasExternalWings
                    ? "Overridden"
                    : Main.LocalPlayer.mount.Active ? "Mounted" : "Enabled";
        string state = Language.GetTextValue($"Mods.Fran.Abilities.Flight.States.{stateKey}");

        return Language.GetTextValue("Mods.Fran.Abilities.Flight.Line", progress, state);
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
