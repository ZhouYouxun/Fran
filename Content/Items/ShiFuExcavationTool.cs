using System;
using System.Collections.Generic;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace FranMod.Content.Items;

internal readonly record struct ShiFuToolStats(int Stage, int PickPower, int AxePower,
    int HammerPower, int UseTime, int UseAnimation, int TileBoost, int Damage);

internal static class ShiFuToolProgression
{
    internal const int FinalStage = 7;

    internal static ShiFuToolStats Current
    {
        get
        {
            int stage = NPC.downedMoonlord ? 7 :
                NPC.downedGolemBoss ? 6 :
                NPC.downedPlantBoss ? 5 :
                NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3 ? 4 :
                Main.hardMode ? 3 :
                NPC.downedBoss3 ? 2 :
                NPC.downedBoss2 ? 1 : 0;

            return stage switch
            {
                1 => new ShiFuToolStats(stage, 70, 75, 70, 12, 18, 2, 12),
                2 => new ShiFuToolStats(stage, 100, 100, 70, 10, 16, 3, 18),
                3 => new ShiFuToolStats(stage, 150, 125, 80, 8, 16, 4, 28),
                4 => new ShiFuToolStats(stage, 200, 175, 100, 7, 15, 5, 38),
                5 => new ShiFuToolStats(stage, 200, 200, 110, 6, 14, 6, 48),
                6 => new ShiFuToolStats(stage, 210, 225, 120, 5, 13, 7, 58),
                7 => new ShiFuToolStats(stage, 250, 250, 150, 4, 12, 8, 72),
                _ => new ShiFuToolStats(stage, 55, 50, 45, 14, 18, 1, 8)
            };
        }
    }

    internal static string StageName(int stage) =>
        Language.GetTextValue($"Mods.Fran.ToolProgression.Stages.Stage{Math.Clamp(stage, 0, FinalStage)}");

    internal static string NextUnlock(int stage) => stage >= FinalStage
        ? Language.GetTextValue("Mods.Fran.ToolProgression.FinalStage")
        : Language.GetTextValue($"Mods.Fran.ToolProgression.Unlocks.Stage{stage + 1}");
}

/// <summary>
/// ShiFu's restrained utility form. Left click is always a pickaxe; right click is always a hamaxe.
/// All powers are derived from synchronized world progression, so the item carries no private upgrade state.
/// </summary>
public sealed class ShiFuExcavationTool : ModItem
{
    public override string Texture => "Fran/Content/Items/StellarCleaver";

    public override void SetStaticDefaults()
    {
        ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        ItemID.Sets.UsesBetterMeleeItemLocation[Type] = true;
    }

    public override void SetDefaults()
    {
        Item.width = 49;
        Item.height = 49;
        Item.DamageType = DamageClass.Melee;
        Item.knockBack = 4f;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.UseSound = SoundID.Item1;
        Item.autoReuse = true;
        Item.useTurn = true;
        Item.scale = 1f;
        Item.value = 0;
        Item.rare = ItemRarityID.Quest;
        Item.favorited = true;
        ApplyStats(ShiFuToolProgression.Current, hamaxeMode: false);
    }

    public override Vector2? HoldoutOffset() => new(-25f, -25f);

    public override bool AltFunctionUse(Player player) => true;

    public override bool CanUseItem(Player player)
    {
        ApplyStats(ShiFuToolProgression.Current, player.altFunctionUse == 2);
        return base.CanUseItem(player);
    }

    public override void UpdateInventory(Player player)
    {
        Item.favorited = true;
        if (player.HeldItem != Item || player.itemAnimation <= 0)
            ApplyStats(ShiFuToolProgression.Current, hamaxeMode: false);
    }

    public override bool CanResearch() => false;

    public override void MeleeEffects(Player player, Rectangle hitbox)
    {
        if (Main.dedServ || player.itemAnimation <= 0)
            return;

        ShiFuToolStats stats = ShiFuToolProgression.Current;
        Color bodyColor = Color.Lerp(new Color(104, 205, 255), new Color(255, 202, 78),
            stats.Stage / (float)ShiFuToolProgression.FinalStage * 0.34f);
        Vector2 effectCenter = hitbox.Center.ToVector2();
        Lighting.AddLight(effectCenter, bodyColor.ToVector3() * 0.32f);
        if (player.itemAnimation % 3 != 0)
            return;

        Vector2 radial = (effectCenter - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
        Vector2 tangent = radial.RotatedBy(-MathHelper.PiOver2 * player.direction * player.gravDir);
        Vector2 position = effectCenter + Main.rand.NextVector2Circular(4f, 4f);
        Vector2 velocity = tangent.RotatedByRandom(0.16f) * Main.rand.NextFloat(1.8f, 3.6f) +
            radial * Main.rand.NextFloat(0.25f, 0.85f) + player.velocity * 0.10f;
        FranParticleSystem.Spawn(new FranLightParticle(position, velocity,
            Main.rand.NextFloat(0.24f, 0.38f), bodyColor, Main.rand.Next(12, 18), 0.62f, 1.05f, 2.1f));

        if (Main.rand.NextBool(4))
            FranParticleSystem.Spawn(new FranGlowOrbParticle(position, velocity * 0.58f,
                Color.Lerp(bodyColor, Color.White, 0.22f), Main.rand.NextFloat(0.11f, 0.18f),
                0.02f, Main.rand.Next(11, 16)));

        if (Main.rand.NextBool(5))
        {
            Dust dust = Dust.NewDustPerfect(position, DustID.TintableDustLighted,
                velocity * 0.72f, 95, bodyColor, Main.rand.NextFloat(0.62f, 0.86f));
            dust.noGravity = true;
        }
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        ShiFuToolStats stats = ShiFuToolProgression.Current;
        tooltips.Add(new TooltipLine(Mod, "ShiFuToolStage",
            Language.GetTextValue("Mods.Fran.ToolProgression.Current",
                ShiFuToolProgression.StageName(stats.Stage), stats.PickPower, stats.AxePower,
                stats.HammerPower, stats.TileBoost, stats.UseTime))
        { OverrideColor = new Color(145, 220, 255) });
        tooltips.Add(new TooltipLine(Mod, "ShiFuToolNext",
            Language.GetTextValue("Mods.Fran.ToolProgression.Next",
                ShiFuToolProgression.NextUnlock(stats.Stage)))
        { OverrideColor = stats.Stage >= ShiFuToolProgression.FinalStage
            ? new Color(241, 190, 76) : new Color(180, 190, 205) });
    }

    private void ApplyStats(ShiFuToolStats stats, bool hamaxeMode)
    {
        Item.damage = stats.Damage;
        Item.useTime = stats.UseTime;
        Item.useAnimation = stats.UseAnimation;
        Item.tileBoost = stats.TileBoost;
        Item.pick = hamaxeMode ? 0 : stats.PickPower;
        // Terraria stores axe power in units of five percentage points.
        Item.axe = hamaxeMode ? stats.AxePower / 5 : 0;
        Item.hammer = hamaxeMode ? stats.HammerPower : 0;
    }
}
