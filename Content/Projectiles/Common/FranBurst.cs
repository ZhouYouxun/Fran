using System;
using System.Collections.Generic;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


public enum FranBurstStyle : byte
{
    Slash,
    Telekinetic,
    SkillTaker,
    Barrier,
    SwordArray,
    Flame,
    AirDraw,
    Lightning,
    ShipCutter,
    ThorsHammer,
    DashImpact,
    Potential
}

public sealed class FranBurst : ModProjectile
{
    private FranBurstStyle Style => (FranBurstStyle)(int)Projectile.ai[0];
    private float VisualScale => Math.Max(0.2f, Projectile.ai[1]);
    private float VisualRotation => Projectile.ai[2];

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public static int Spawn(IEntitySource source, Vector2 center, int owner, FranBurstStyle style, float scale,
        int damage = 0, float knockback = 0f, float? rotation = null)
    {
        return Projectile.NewProjectile(source, center, Vector2.Zero, ModContent.ProjectileType<FranBurst>(),
            damage, knockback, owner, (float)style, scale, rotation ?? Main.rand.NextFloat(MathHelper.TwoPi));
    }

    public static int SpawnForOwner(IEntitySource source, Vector2 center, int owner, FranBurstStyle style, float scale,
        int damage = 0, float knockback = 0f, float? rotation = null)
    {
        if (Main.netMode == NetmodeID.Server || Main.myPlayer != owner)
            return -1;
        return Spawn(source, center, owner, style, scale, damage, knockback, rotation);
    }

    public override void SetDefaults()
    {
        Projectile.width = 86;
        Projectile.height = 86;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 20;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage()
    {
        float progress = 1f - Projectile.timeLeft / 20f;
        return Projectile.damage > 0 && progress is >= 0.08f and <= 0.58f ? null : false;
    }

    public override void AI()
    {
        Vector2 center = Projectile.Center;
        float progress = 1f - Projectile.timeLeft / 20f;
        float eased = 1f - MathF.Pow(1f - progress, 3f);
        float radius = MathHelper.Lerp(14f, 72f * VisualScale, eased);
        int size = Math.Max(24, (int)(radius * 2f));
        Projectile.width = size;
        Projectile.height = size;
        Projectile.Center = center;

        if (Projectile.localAI[0]++ != 0f)
            return;

        Color color = StyleColor(Style);
        if (Main.netMode != NetmodeID.Server)
        {
            FranParticleEffects.Burst(Projectile.Center, color, VisualScale, Style, VisualRotation);
            if (Style is FranBurstStyle.ThorsHammer or FranBurstStyle.ShipCutter or FranBurstStyle.DashImpact)
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.48f, Pitch = Style == FranBurstStyle.ThorsHammer ? -0.35f : 0.08f }, Projectile.Center);
            else if (Style == FranBurstStyle.Barrier)
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.58f, Pitch = 0.32f }, Projectile.Center);

            float shake = Style switch
            {
                FranBurstStyle.ThorsHammer => 13f,
                FranBurstStyle.ShipCutter => 9f,
                FranBurstStyle.DashImpact => 5f,
                FranBurstStyle.AirDraw => 4f,
                FranBurstStyle.Flame or FranBurstStyle.Slash => 3.2f,
                FranBurstStyle.Barrier or FranBurstStyle.Lightning => 2.5f,
                FranBurstStyle.Potential => 0.8f,
                _ => 1.5f
            };
            Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, shake);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float progress = 1f - Projectile.timeLeft / 20f;
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        float eased = 1f - MathF.Pow(1f - progress, 3f);
        float radius = MathHelper.Lerp(14f, 72f * VisualScale, eased);
        Color color = StyleColor(Style);

        if (Style == FranBurstStyle.Barrier)
            FranVfx.DrawShared("Cal_CircularSmearSmokey", Projectile.Center,
                new Color(10, 5, 22, 190) * opacity, VisualRotation + progress,
                0.24f * VisualScale * (0.72f + eased * 0.42f));

        FranVfx.BeginAdditive();
        switch (Style)
        {
            case FranBurstStyle.Slash:
                FranVfx.DrawShared("SlashWide", Projectile.Center, color * (0.58f * opacity),
                    VisualRotation, 0.28f * VisualScale * (0.75f + eased * 0.35f));
                break;
            case FranBurstStyle.Telekinetic:
                FranVfx.DrawRing(Projectile.Center, color, radius, 0.58f * opacity, VisualRotation, "Cal_BloomRing");
                FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.68f * opacity),
                    VisualRotation, new Vector2(radius * 2.15f, radius * 1.18f));
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                    Color.White * (0.76f * opacity), VisualRotation, new Vector2(radius * 1.22f, radius * 0.68f));
                break;
            case FranBurstStyle.SkillTaker:
                float novaPulse = 0.90f + MathF.Sin(progress * MathHelper.Pi * 3f) * 0.10f;
                FranVfx.DrawShared("CL_BloomCircle", Projectile.Center,
                    new Color(82, 12, 128, 0) * (0.34f * opacity),
                    -VisualRotation - progress * 0.8f, radius * 2.15f / 200f);
                FranVfx.DrawShared("CL_BloomCircle", Projectile.Center,
                    new Color(205, 100, 255, 0) * (0.64f * opacity),
                    VisualRotation + progress, radius * 1.56f / 200f * novaPulse);
                FranVfx.DrawShared("CL_BloomCircle", Projectile.Center,
                    new Color(244, 214, 255, 0) * (0.78f * opacity),
                    -VisualRotation, radius * 0.72f / 200f);
                FranVfx.DrawRing(Projectile.Center, new Color(225, 160, 255), radius * 1.12f,
                    0.56f * opacity, VisualRotation + progress * 1.4f, "Cal_BloomRing");
                break;
            case FranBurstStyle.Barrier:
                FranVfx.DrawRing(Projectile.Center, color, radius, 0.46f * opacity,
                    VisualRotation, "Cal_HighResHollowCircle");
                FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center,
                    new Color(105, 75, 255) * (0.42f * opacity), VisualRotation,
                    0.52f * VisualScale * (0.72f + eased * 0.36f));
                FranVfx.DrawShared("Crack", Projectile.Center, color * (0.34f * opacity),
                    VisualRotation, 0.18f * VisualScale * (0.8f + eased * 0.4f));
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                    Color.White * (0.68f * opacity), VisualRotation,
                    new Vector2(radius * 1.72f, radius * 1.18f));
                break;
            case FranBurstStyle.SwordArray:
                for (int i = 0; i < 4; i++)
                {
                    float angle = VisualRotation + i * MathHelper.PiOver2;
                    Vector2 point = Projectile.Center + angle.ToRotationVector2() * radius * 0.46f;
                    FranVfx.DrawShared("Cal_ForwardSmear", point,
                        new Color(145, 255, 120) * (0.42f * opacity), angle - MathHelper.PiOver2,
                        new Vector2(0.10f, 0.035f) * VisualScale);
                }
                FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center, Color.White * (0.68f * opacity),
                    -VisualRotation, new Vector2(0.09f, 0.025f) * VisualScale);
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                    new Color(210, 255, 195) * (0.72f * opacity), VisualRotation,
                    new Vector2(radius * 1.42f, radius * 0.78f));
                break;
            case FranBurstStyle.Flame:
                FranVfx.DrawShared("Cal_CircularSmear", Projectile.Center,
                    new Color(95, 255, 80) * (0.38f * opacity), VisualRotation + progress * 1.4f,
                    0.42f * VisualScale * (0.82f + eased * 0.4f));
                FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center,
                    color * (0.48f * opacity), -VisualRotation - progress * 1.8f,
                    0.48f * VisualScale * (0.72f + eased * 0.38f));
                FranVfx.DrawRing(Projectile.Center, new Color(185, 255, 95), radius * 0.76f, 0.42f * opacity,
                    -VisualRotation, "Cal_BloomRing");
                break;
            case FranBurstStyle.AirDraw:
                FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.74f * opacity),
                    VisualRotation, new Vector2(radius * 2.35f, radius * 1.22f));
                FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center, Color.White * (0.50f * opacity),
                    VisualRotation, new Vector2(radius * 2.05f, radius * 0.62f));
                break;
            case FranBurstStyle.Lightning:
                FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.68f * opacity),
                    VisualRotation, new Vector2(radius * 1.86f, radius * 1.34f));
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                    Color.White * (0.84f * opacity), VisualRotation, new Vector2(radius * 1.54f, radius * 0.86f));
                FranVfx.DrawRing(Projectile.Center, color, radius, 0.42f * opacity,
                    VisualRotation, "Cal_BloomRing");
                for (int i = 0; i < 6; i++)
                    FranVfx.DrawTexturedSpark(Projectile.Center,
                        (VisualRotation + i * MathHelper.TwoPi / 6f).ToRotationVector2(), color,
                        radius * 1.05f, 3.5f * VisualScale, opacity * 0.72f);
                break;
            case FranBurstStyle.ShipCutter:
                for (int i = 0; i < 3; i++)
                    FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center,
                        color * ((0.58f - i * 0.12f) * opacity), VisualRotation - i * 0.12f,
                        (0.82f - i * 0.10f) * VisualScale * (0.72f + eased * 0.42f));
                FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center, Color.White * (0.56f * opacity),
                    -VisualRotation, new Vector2(0.16f, 0.035f) * VisualScale);
                break;
            case FranBurstStyle.ThorsHammer:
                float hammerFlash = 1f - FranMotion.Smooth(FranMotion.Segment(progress, 0f, 0.24f));
                float hammerAfterglow = 1f - FranMotion.Smooth(FranMotion.Segment(progress, 0.14f, 0.88f));
                float hammerExpansion = FranMotion.EaseOutCubic(FranMotion.Segment(progress, 0f, 0.58f));
                float hammerRadius = MathHelper.Lerp(28f, 112f * VisualScale, hammerExpansion);
                FranVfx.DrawShared("CL_BloomCircle", Projectile.Center,
                    new Color(52, 82, 255, 0) * (0.38f * hammerAfterglow), VisualRotation,
                    hammerRadius * 2.7f / 200f);
                FranVfx.DrawShared("CL_BloomCircle", Projectile.Center,
                    new Color(188, 238, 255, 0) * (0.62f * hammerAfterglow), -VisualRotation,
                    hammerRadius * 1.65f / 200f);
                FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
                    color * (0.84f * hammerAfterglow + 0.44f * hammerFlash),
                    VisualRotation, new Vector2(hammerRadius * 2.54f, hammerRadius * 1.62f));
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                    Color.White * (0.94f * hammerFlash + 0.46f * hammerAfterglow), VisualRotation,
                    new Vector2(hammerRadius * 1.94f, hammerRadius * 1.08f));
                FranVfx.DrawShared("Crack", Projectile.Center, color * (0.42f * hammerAfterglow),
                    -VisualRotation, 0.34f * VisualScale * (0.72f + hammerExpansion * 0.46f));
                FranVfx.DrawRing(Projectile.Center, color, hammerRadius, 0.64f * hammerAfterglow,
                    VisualRotation, "Cal_HighResHollowCircle");
                FranVfx.DrawRing(Projectile.Center, Color.White, hammerRadius * 0.62f,
                    0.46f * hammerAfterglow, -VisualRotation, "Cal_BloomRing");
                for (int i = 0; i < 12; i++)
                {
                    float angle = VisualRotation + MathHelper.TwoPi * i / 12f;
                    Vector2 direction = angle.ToRotationVector2();
                    FranVfx.DrawTexturedSpark(Projectile.Center + direction * hammerRadius * 0.18f,
                        direction, i % 3 == 0 ? Color.White : color,
                        hammerRadius * (i % 2 == 0 ? 1.16f : 0.82f),
                        4.6f * VisualScale, hammerAfterglow * 0.68f);
                }
                FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center,
                    Color.White * (0.96f * hammerFlash + 0.52f * hammerAfterglow),
                    VisualRotation, new Vector2(0.38f, 0.078f) * VisualScale);
                break;
            case FranBurstStyle.DashImpact:
                FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
                    new Color(135, 75, 190) * (0.72f * opacity), VisualRotation,
                    new Vector2(radius * 2.34f, radius * 1.12f));
                FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center,
                    new Color(205, 165, 255) * (0.46f * opacity), VisualRotation,
                    new Vector2(radius * 2.05f, radius * 0.58f));
                FranVfx.DrawRing(Projectile.Center, new Color(105, 55, 155), radius,
                    0.46f * opacity, -VisualRotation, "Cal_BloomRing");
                FranVfx.DrawShared("Star", Projectile.Center, new Color(205, 165, 255) * (0.34f * opacity),
                    -VisualRotation, 0.13f * VisualScale);
                break;
            case FranBurstStyle.Potential:
                FranVfx.DrawRing(Projectile.Center, color, radius, 0.54f * opacity,
                    VisualRotation + progress, "MagicCircle");
                FranVfx.DrawRing(Projectile.Center, Color.White, radius * 0.62f, 0.32f * opacity,
                    -VisualRotation - progress * 1.4f, "RingStreak");
                break;
        }
        if (Style != FranBurstStyle.SkillTaker)
            FranVfx.DrawBloom(Projectile.Center, color, radius * 0.72f, 0.28f * opacity,
                Style is FranBurstStyle.ThorsHammer or FranBurstStyle.ShipCutter);
        FranVfx.EndAdditive();
        return false;
    }

    private static Color StyleColor(FranBurstStyle style) => style switch
    {
        FranBurstStyle.SkillTaker => new Color(205, 100, 255),
        FranBurstStyle.Barrier => new Color(90, 185, 255),
        FranBurstStyle.Flame => new Color(255, 105, 55),
        FranBurstStyle.AirDraw => new Color(205, 245, 255),
        FranBurstStyle.Lightning => new Color(160, 115, 255),
        FranBurstStyle.ShipCutter => new Color(255, 215, 155),
        FranBurstStyle.ThorsHammer => new Color(105, 150, 255),
        FranBurstStyle.DashImpact => new Color(72, 34, 108),
        FranBurstStyle.Potential => new Color(145, 225, 255),
        _ => new Color(155, 225, 255)
    };
}
