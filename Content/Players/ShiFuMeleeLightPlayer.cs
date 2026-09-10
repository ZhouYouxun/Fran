using System;
using FranMod.Content.Items;
using FranMod.Content.Projectiles;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Content.Players;

/// <summary>
/// Owns ShiFu's continuous melee light independently from individual swing projectiles.
/// This keeps the light centered on the player and prevents combo or tactic transitions from flashing.
/// </summary>
public sealed class ShiFuMeleeLightPlayer : ModPlayer
{
    private const float MinimumActiveStrength = 1.2f;
    private const float MaximumActiveStrength = 1.6f;
    private const float FadeInStep = 1f / 12f;
    private const float FadeOutStep = 1f / 24f;
    private const float ColorBlendRate = 0.12f;

    private static readonly Color DefaultLightColor = new(115, 222, 255);

    private float activation;
    private Vector3 currentColor;
    private bool colorInitialized;

    public override void Initialize()
    {
        activation = 0f;
        currentColor = DefaultLightColor.ToVector3();
        colorInitialized = true;
    }

    public override void UpdateDead()
    {
        activation = 0f;
        currentColor = DefaultLightColor.ToVector3();
        colorInitialized = true;
    }

    public override void PostUpdate()
    {
        if (Main.dedServ)
            return;

        bool holdingShiFu = Player.HeldItem.type == ModContent.ItemType<ShiFu>();
        bool requested = holdingShiFu && IsLeftAttackHeld();
        float step = requested ? FadeInStep : FadeOutStep;
        activation = Approach(activation, requested ? 1f : 0f, step);

        Vector3 targetColor = ResolveTargetColor();
        if (!colorInitialized)
        {
            currentColor = targetColor;
            colorInitialized = true;
        }
        else
        {
            currentColor = Vector3.Lerp(currentColor, targetColor, ColorBlendRate);
        }

        if (activation <= 0f)
            return;

        float smoothActivation = MathHelper.SmoothStep(0f, 1f, activation);
        float activeStrength = MathHelper.Lerp(MinimumActiveStrength, MaximumActiveStrength, smoothActivation);
        Lighting.AddLight(Player.Center, currentColor * (activeStrength * smoothActivation));
    }

    private bool IsLeftAttackHeld()
    {
        if (Player.whoAmI == Main.myPlayer)
        {
            bool physicalLeftHeld = Main.mouseLeft && !Main.playerInventory && !Main.gameMenu;
            bool routedLeftHeld = Player.controlUseItem && Player.altFunctionUse != 2;
            return physicalLeftHeld || routedLeftHeld;
        }

        int swingType = ModContent.ProjectileType<ShiFuSwingRebuild>();
        return Player.channel || Player.ownedProjectileCounts[swingType] > 0;
    }

    private Vector3 ResolveTargetColor()
    {
        FranPlayer fran = Player.GetModPlayer<FranPlayer>();
        Color color = fran.HasSelectedSkill ? FranSkillInfo.Color(fran.SelectedSkill) : DefaultLightColor;
        return color.ToVector3();
    }

    private static float Approach(float value, float target, float maximumChange)
    {
        if (value < target)
            return Math.Min(value + maximumChange, target);
        if (value > target)
            return Math.Max(value - maximumChange, target);
        return target;
    }
}
