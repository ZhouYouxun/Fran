using System;
using System.Collections.Generic;
using FranMod.Content.UI;
using FranMod.Content.Players;
using FranMod.Content.Projectiles;
using FranMod.Content.Projectiles.Ultimate;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace FranMod.Content.Items;

public sealed class ShiFu : ModItem
{
    private const string GuidanceLinePrefix = "ShiFuGuidance";
    private const string GoldenEvolutionRoadLineName = "GoldenEvolutionRoad";
    private const string TacticSelectionLineName = "TacticSelection";
    private const string FullLoadoutStatusLineName = "FullLoadoutStatus";
    private const int TacticIconTooltipWidthPadding = 36;
    private static readonly Color[] GuidanceLineColors =
    [
        new(126, 212, 255),
        new(157, 226, 255),
        new(150, 235, 190),
        new(190, 198, 255)
    ];

    public override string Texture => FranShiFuVisuals.TexturePath;

    // The held projectile draws the sword; hiding the item graphic must not exclude melee prefixes.
    public override bool MeleePrefix() => true;

    public override void SetStaticDefaults()
    {
        ItemID.Sets.BonusAttackSpeedMultiplier[Type] = 0.5f;
    }

    public override void SetDefaults()
    {
        Item.width = 74;
        Item.height = 74;
        Item.damage = FranBalance.GetInitialBaseDamage();
        Item.DamageType = DamageClass.Melee;
        Item.knockBack = FranBalance.GetFloat(nameof(FranBalance.ShiFuKnockback), FranBalance.ShiFuKnockback);
        Item.useTime = FranBalance.GetInt(nameof(FranBalance.ShiFuUseTimeFrames), FranBalance.ShiFuUseTimeFrames);
        Item.useAnimation = Item.useTime;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.noUseGraphic = true;
        Item.noMelee = true;
        Item.channel = true;
        Item.autoReuse = true;
        Item.shoot = ModContent.ProjectileType<ShiFuSwingRebuild>();
        Item.shootSpeed = 1f;
        Item.rare = ModContent.RarityType<ShiFuPrismaticRarity>();
        Item.value = 0;
    }

    public override bool CanResearch() => false;

    private static bool WantsDisc(Player player) => player.altFunctionUse == 2 ||
        player.whoAmI == Main.myPlayer && PlayerInput.Triggers.Current.MouseRight;

    public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
    {
        damage.Base += FranBalance.GetBaseDamage(player) - Item.damage;
        int butterflySlots = player.GetModPlayer<FranPlayer>().GetSkillStacks(FranSkill.BlueResonance);
        damage *= Math.Max(0f, 1f - butterflySlots * FranBalance.GetFloat(
            nameof(FranBalance.BlueResonanceWeaponDamagePenaltyPerSlot),
            FranBalance.BlueResonanceWeaponDamagePenaltyPerSlot));
    }

    public override bool AltFunctionUse(Player player) => true;

    public override bool CanUseItem(Player player)
    {
        if (player.GetModPlayer<FranDiscPlayer>().Recovering) return false;
        Item.useTime = FranBalance.GetInt(nameof(FranBalance.ShiFuUseTimeFrames), FranBalance.ShiFuUseTimeFrames);
        Item.useAnimation = Item.useTime;
        Item.knockBack = FranBalance.GetFloat(nameof(FranBalance.ShiFuKnockback), FranBalance.ShiFuKnockback);
        Item.channel = true;
        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        if (WantsDisc(player) && fran.UltimateActive) return false;
        Item.shoot = WantsDisc(player)
            ? ModContent.ProjectileType<FranSpinningDisc>()
            : fran.VigilActive
            ? ModContent.ProjectileType<FranVigilSwing>()
            : fran.IsFullTacticLoadout(FranSkill.PressurizedQuickdraw)
            ? ModContent.ProjectileType<FranQuickdrawDiscHoldout>()
            : ModContent.ProjectileType<ShiFuSwingRebuild>();
        if (fran.VigilActive) Item.useAnimation = Item.useTime = FranVigilRules.ReferenceUseAnimation;
        return player.ownedProjectileCounts[ModContent.ProjectileType<FranSpinningDisc>()] == 0 &&
            player.ownedProjectileCounts[ModContent.ProjectileType<ShiFuSwingRebuild>()] == 0 &&
            player.ownedProjectileCounts[ModContent.ProjectileType<FranQuickdrawDiscHoldout>()] == 0 &&
            player.ownedProjectileCounts[ModContent.ProjectileType<FranVigilSwing>()] == 0;
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
        int type, int damage, float knockback)
    {
        FranPlayer fran = player.GetModPlayer<FranPlayer>();
        Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
        if (WantsDisc(player))
        {
            SpawnDisc(player, source, aim, damage, knockback);
            return false;
        }
        if (fran.VigilActive)
        {
            fran.ComboIndex = 0;
            fran.ComboReset = 0;
            Projectile.NewProjectile(source, player.MountedCenter, aim,
                ModContent.ProjectileType<FranVigilSwing>(), damage, knockback, player.whoAmI);
            return false;
        }

        if (fran.IsFullTacticLoadout(FranSkill.PressurizedQuickdraw))
        {
            // Full quickdraw retains its original sustained whirlwind outside Vigil.
            fran.ComboIndex = 0;
            fran.ComboReset = 0;
            int layers = Math.Clamp(fran.GetSkillStacks(FranSkill.PressurizedQuickdraw), 1, FranPlayer.MaximumTacticSlots);
            float discKnockback = knockback * FranBalance.GetFloat(
                nameof(FranBalance.QuickdrawFullDiscKnockbackMultiplier), FranBalance.QuickdrawFullDiscKnockbackMultiplier);
            Projectile.NewProjectile(source, player.MountedCenter, aim,
                ModContent.ProjectileType<FranQuickdrawDiscHoldout>(), damage, discKnockback, player.whoAmI, layers);
            return false;
        }

        FranEvolutionPlayer evolution = player.GetModPlayer<FranEvolutionPlayer>();
        int combo = fran.ConsumeNextCombo();
        int unscaledSwordDamage = damage;
        damage = Math.Max(1, (int)(damage * FranBalance.GetComboDamageMultiplier(combo)));
        // The disc and the left chain share the exact attack-speed and fourth-beat clock.
        int duration = FranSwordTiming.ConsumeBeatDuration(player, combo);
        FranSkill skill = fran.HasSelectedSkill ? fran.SelectedSkill : FranSkill.None;
        int packedImprint = skill == FranSkill.None ? 0 : (int)skill + 1;
        int projectileIndex = Projectile.NewProjectile(source, player.MountedCenter, aim, type, damage, knockback, player.whoAmI,
            combo, duration, packedImprint);
        if (Main.projectile.IndexInRange(projectileIndex) &&
            Main.projectile[projectileIndex].ModProjectile is ShiFuSwingRebuild swing)
        {
            swing.UnscaledSwordDamage = unscaledSwordDamage;
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

    internal static void SpawnDisc(Player player, IEntitySource source, Vector2 aim, int damage, float knockback)
    {
        int index = Projectile.NewProjectile(source, player.MountedCenter, aim,
            ModContent.ProjectileType<FranSpinningDisc>(), damage, knockback, player.whoAmI);
        if (Main.projectile.IndexInRange(index) && Main.projectile[index].ModProjectile is FranSpinningDisc disc)
        {
            disc.InitializeAttack(damage, player.GetModPlayer<FranPlayer>().GetActiveTacticSnapshot());
            Main.projectile[index].CritChance = player.GetWeaponCrit(player.HeldItem);
            Main.projectile[index].netUpdate = true;
        }
    }

    internal static string WheelOpenInstruction() => Language.GetTextValue(
        ModContent.GetInstance<FranMod.Content.Config.FranClientConfig>().HoldSkillWheelToOpen
            ? "Mods.Fran.Items.ShiFu.WheelBoundHold"
            : "Mods.Fran.Items.ShiFu.WheelBound",
        GetAssignedKeyOrDefault(FranModMain.SkillWheelKey));

    internal static string GetAssignedKeyOrDefault(ModKeybind keybind) =>
        TryGetAssignedKey(keybind, out string key) ? key : "LeftAlt";

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        foreach (TooltipLine line in tooltips)
            if (line.Mod == "Terraria" && line.Name.StartsWith("Tooltip", StringComparison.Ordinal))
            {
                line.Text = line.Text.Replace("{WheelInstruction}", WheelOpenInstruction());
            }
        ApplyGuidanceLineColors(tooltips);
        FranPlayer fran = Main.LocalPlayer.GetModPlayer<FranPlayer>();
        InsertUltimateTooltip(tooltips, fran);
        InsertTacticStatus(tooltips, fran);
        InsertEvolutionAndMovementStatus(tooltips, fran);

        tooltips.Add(new TooltipLine(Mod, GoldenEvolutionRoadLineName,
            Language.GetTextValue("Mods.Fran.Items.ShiFu.GoldenEvolutionRoad"))
        { OverrideColor = new Color(241, 190, 76) });
    }

    private void ApplyGuidanceLineColors(List<TooltipLine> tooltips)
    {
        for (int i = 0; i < GuidanceLineColors.Length; i++)
        {
            int index = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name == $"Tooltip{i}");
            if (index < 0)
                continue;

            tooltips[index] = new TooltipLine(Mod, $"{GuidanceLinePrefix}{i}", tooltips[index].Text)
            { OverrideColor = GuidanceLineColors[i] };
        }
    }

    private void InsertUltimateTooltip(List<TooltipLine> tooltips, FranPlayer fran)
    {
        string text = fran.UltimateUnlocked
            ? Language.GetTextValue("Mods.Fran.Items.ShiFu.UltimateTooltip", GetAssignedKeyOrUnbound(FranModMain.UltimateKey))
            : Language.GetTextValue("Mods.Fran.Items.ShiFu.UltimateLocked");
        int index = FindGuidanceEndIndex(tooltips);
        int lineNumber = 0;
        foreach (string line in text.Split('\n'))
            tooltips.Insert(index++, new TooltipLine(Mod, $"Ultimate{lineNumber++}", line.TrimEnd('\r')));
    }

    private void InsertTacticStatus(List<TooltipLine> tooltips, FranPlayer fran)
    {
        TooltipLine selectionLine = new(Mod, TacticSelectionLineName,
            Language.GetTextValue("Mods.Fran.Items.ShiFu.TacticSelection") +
            new string(' ', TacticIconTooltipWidthPadding) + "\n ")
        { OverrideColor = new Color(150, 215, 255) };

        FranSkill fullLoadoutSkill = fran.GetEquippedTactic(0);
        bool fullLoadoutActive = FranSkillInfo.IsRealSkill(fullLoadoutSkill) &&
            fran.IsFullTacticLoadout(fullLoadoutSkill);
        string fullLoadoutText = fullLoadoutActive
            ? Language.GetTextValue("Mods.Fran.Items.ShiFu.FullLoadoutActive",
                FranSkillInfo.Name(fullLoadoutSkill), GetFullLoadoutEffectText(fullLoadoutSkill))
            : Language.GetTextValue("Mods.Fran.Items.ShiFu.FullLoadoutInactive");
        TooltipLine fullLoadoutLine = new(Mod, FullLoadoutStatusLineName, fullLoadoutText)
        {
            OverrideColor = fullLoadoutActive
                ? FranSkillInfo.Color(fullLoadoutSkill)
                : new Color(155, 165, 178)
        };

        int rightClickLineIndex = tooltips.FindIndex(line =>
            IsGuidanceLine(line, 1));
        int insertionIndex = rightClickLineIndex >= 0
            ? rightClickLineIndex + 1
            : Math.Min(2, tooltips.Count);
        tooltips.Insert(insertionIndex, selectionLine);
        tooltips.Insert(insertionIndex + 1, fullLoadoutLine);
    }

    private void InsertEvolutionAndMovementStatus(List<TooltipLine> tooltips, FranPlayer fran)
    {
        int index = FindGuidanceEndIndex(tooltips);

        if (FranEvolutionInfo.SystemEnabled)
            tooltips.Insert(index++, new TooltipLine(Mod, "Evolution",
                Language.GetTextValue("Mods.Fran.Evolution.Tooltip"))
            { OverrideColor = new Color(195, 178, 245) });

        tooltips.Insert(index++, new TooltipLine(Mod, "FranDashStatus", BuildDashStatus())
        { OverrideColor = new Color(130, 235, 195) });

        bool flightEnabled = FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh) &&
            !fran.HasExternalWings;
        tooltips.Insert(index, new TooltipLine(Mod, "FranFlightStatus", BuildFlightStatus(fran))
        { OverrideColor = flightEnabled ? new Color(105, 205, 255) : new Color(165, 175, 190) });
    }

    private int FindGuidanceEndIndex(List<TooltipLine> tooltips)
    {
        int index = tooltips.FindLastIndex(line =>
            (line.Mod == "Terraria" && line.Name.StartsWith("Tooltip", StringComparison.Ordinal)) ||
            (line.Mod == Mod.Name && (
                line.Name.StartsWith(GuidanceLinePrefix, StringComparison.Ordinal) ||
                line.Name.StartsWith("Ultimate", StringComparison.Ordinal) ||
                line.Name == TacticSelectionLineName ||
                line.Name == FullLoadoutStatusLineName ||
                line.Name == "Evolution" ||
                line.Name == "FranDashStatus" ||
                line.Name == "FranFlightStatus")));
        return index >= 0 ? index + 1 : Math.Min(2, tooltips.Count);
    }

    private bool IsGuidanceLine(TooltipLine line, int number) =>
        (line.Mod == "Terraria" && line.Name == $"Tooltip{number}") ||
        (line.Mod == Mod.Name && line.Name == $"{GuidanceLinePrefix}{number}");

    private static string GetFullLoadoutEffectText(FranSkill skill)
    {
        string text = FranSkillInfo.FullLoadoutText(skill);
        int separator = text.IndexOf('：');
        if (separator < 0)
            separator = text.IndexOf(':');
        return separator >= 0 ? text[(separator + 1)..].TrimStart() : text;
    }

    private static string BuildDashStatus()
    {
        if (TryGetAssignedKey(FranModMain.DashKey, out string key))
            return Language.GetTextValue("Mods.Fran.Abilities.Dash.Bound", key);
        return Language.GetTextValue("Mods.Fran.Abilities.Dash.Unbound");
    }

    internal static string GetAssignedKeyOrUnbound(ModKeybind keybind) =>
        TryGetAssignedKey(keybind, out string key) ? key :
            Language.GetTextValue("Mods.Fran.Items.ShiFu.UnboundKey");

    private static bool TryGetAssignedKey(ModKeybind keybind, out string assignedKey)
    {
        assignedKey = string.Empty;
        if (Main.dedServ || keybind is null)
            return false;

        try
        {
            foreach (string key in keybind.GetAssignedKeys())
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
        string stateKey = !FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh)
            ? "Locked"
            : fran.HasExternalWings ? "WingsEquipped" : "Enabled";
        return Language.GetTextValue($"Mods.Fran.Abilities.Flight.{stateKey}");
    }

    public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
    {
        if (line.Mod == Mod.Name && line.Name == GoldenEvolutionRoadLineName)
        {
            FranCrystalTooltipEffects.DrawCrystalWarning(line);
            return false;
        }

        if (line.Mod == Mod.Name && line.Name == TacticSelectionLineName)
        {
            DrawTacticSelectionWithIcons(line);
            return false;
        }

        if (line.Mod == "Terraria" && line.Name == "ItemName")
        {
            FranTooltipTextEffects.DrawShiFuPrismaticName(line);
            return false;
        }

        return true;
    }

    private static void DrawTacticSelectionWithIcons(DrawableTooltipLine line)
    {
        string label = line.Text.TrimEnd();
        float uiScale = line.BaseScale.X;
        int boxSize = Math.Max(26, (int)MathF.Round(34f * uiScale));
        float labelHeight = line.Font.MeasureString(label).Y * uiScale;
        Vector2 labelPosition = new(line.X, line.Y + (boxSize - labelHeight) * 0.5f);
        ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, label, labelPosition,
            line.OverrideColor ?? Color.White, line.Rotation, line.Origin, line.BaseScale);

        float labelWidth = line.Font.MeasureString(label).X * uiScale;
        DrawTacticIcons(line, new Vector2(line.X + labelWidth + 8f * uiScale, line.Y));
    }

    private static void DrawTacticIcons(DrawableTooltipLine line, Vector2 origin)
    {
        FranPlayer fran = Main.LocalPlayer.GetModPlayer<FranPlayer>();
        int slots = Math.Min(fran.AvailableTacticSlots, FranPlayer.MaximumTacticSlots);
        float uiScale = line.BaseScale.X;
        int boxSize = Math.Max(26, (int)MathF.Round(34f * uiScale));
        int gap = Math.Max(4, (int)MathF.Round(6f * uiScale));
        int iconSize = Math.Max(16, boxSize - Math.Max(8, (int)MathF.Round(10f * uiScale)));
        int left = (int)MathF.Round(origin.X);
        int top = (int)MathF.Round(origin.Y);

        for (int slot = 0; slot < slots; slot++)
        {
            FranSkill skill = fran.GetEquippedTactic(slot);
            bool equipped = FranSkillInfo.IsRealSkill(skill);
            Color accent = equipped ? FranSkillInfo.Color(skill) : new Color(82, 96, 115);
            Rectangle box = new(left + slot * (boxSize + gap), top, boxSize, boxSize);
            DrawTooltipRectangle(box, equipped ? new Color(14, 25, 43, 230) : new Color(10, 15, 25, 190));
            DrawTooltipBorder(box, Color.Lerp(accent, Color.White, equipped ? 0.36f : 0.10f), 2);

            if (!equipped)
                continue;

            Texture2D texture = ModContent.Request<Texture2D>(FranUISystem.SkillIconPath(skill)).Value;
            float scale = Math.Min(iconSize / (float)Math.Max(1, texture.Width),
                iconSize / (float)Math.Max(1, texture.Height));
            Main.spriteBatch.Draw(texture, new Vector2(box.Center.X, box.Center.Y), null, Color.White,
                0f, texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }
    }

    private static void DrawTooltipRectangle(Rectangle rectangle, Color color) =>
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, rectangle, color);

    private static void DrawTooltipBorder(Rectangle rectangle, Color color, int thickness)
    {
        DrawTooltipRectangle(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        DrawTooltipRectangle(new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        DrawTooltipRectangle(new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        DrawTooltipRectangle(new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
}
