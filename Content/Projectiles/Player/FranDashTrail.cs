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


public sealed class FranDashTrail : ModProjectile
{
    private const int HistoryLength = 10;
    private const int FadeDuration = 11;
    private readonly Vector2[] centerHistory = new Vector2[HistoryLength];
    private int historyCount;
    private int visualAge;
    private int fadeAge;
    private bool initialized;
    private bool ending;
    private Vector2 dashOrigin;
    private Vector2 dashEnd;
    private Vector2 dashDirection;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = DashDurationWithFade;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    private int DashDurationWithFade => FranPlayer.DashDuration + FadeDuration + 2;

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }

        if (!initialized)
        {
            initialized = true;
            dashOrigin = Projectile.Center;
            dashEnd = owner.Center;
            dashDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
            Projectile.timeLeft = DashDurationWithFade;
            for (int i = 0; i < centerHistory.Length; i++)
                centerHistory[i] = dashOrigin;
            historyCount = 1;
            if (!Main.dedServ)
                FranParticleEffects.DashLaunch(dashOrigin, dashDirection * 16f);
        }

        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        if (!ending && fran.DashTimer > 0)
        {
            visualAge++;
            Vector2 motion = owner.velocity.LengthSquared() > 1f ? owner.velocity : Projectile.velocity;
            dashDirection = Vector2.Lerp(dashDirection, motion.SafeNormalize(dashDirection), 0.42f)
                .SafeNormalize(dashDirection);
            dashEnd = owner.Center;
            PushHistory(dashEnd);
            Projectile.Center = dashEnd;
            Lighting.AddLight(owner.Center, 0.14f, 0.06f, 0.23f);
            if (!Main.dedServ)
                FranParticleEffects.DashWake(owner.Center, motion, visualAge);
            return;
        }

        if (!ending)
        {
            ending = true;
            fadeAge = 0;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, FadeDuration + 1);
            if (!Main.dedServ)
                FranParticleEffects.DashBrake(dashEnd, dashDirection * 16f);
        }

        fadeAge++;
        Projectile.Center = dashEnd;
        Lighting.AddLight(dashEnd, 0.08f, 0.03f, 0.13f);
    }

    private void PushHistory(Vector2 center)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y))
            return;
        for (int i = centerHistory.Length - 1; i > 0; i--)
            centerHistory[i] = centerHistory[i - 1];
        centerHistory[0] = center;
        historyCount = Math.Min(historyCount + 1, centerHistory.Length);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (!initialized || historyCount < 1)
            return false;

        float fade = ending ? MathHelper.Clamp(1f - fadeAge / (float)FadeDuration, 0f, 1f) : 1f;
        float appear = MathHelper.Clamp(visualAge / 2f, 0f, 1f);
        float opacity = fade * appear;
        float rotation = dashDirection.ToRotation();
        float launchOpacity = ending ? 0f : MathHelper.Clamp(1f - visualAge / 9f, 0f, 1f);
        float launchProgress = MathHelper.Clamp(visualAge / 9f, 0f, 1f);
        float brakeProgress = ending ? MathHelper.Clamp(fadeAge / (float)FadeDuration, 0f, 1f) : 0f;
        float brakeOpacity = ending ? MathF.Sin(brakeProgress * MathHelper.Pi) : 0f;
        Vector2 normal = dashDirection.RotatedBy(MathHelper.PiOver2);
        Vector2 head = ending ? dashEnd : centerHistory[0];
        float tigerRotation = rotation - MathHelper.PiOver2;
        float tigerPulse = 1f + MathF.Sin((visualAge + fadeAge) * 0.82f) * 0.045f;
        float tigerDiameter = ending
            ? MathHelper.Lerp(118f, 76f, brakeProgress)
            : 112f * tigerPulse;
        Vector2 tigerSize = Vector2.One * tigerDiameter;

        // 黑色主体使用 AlphaBlend，外缘辉光使用 Additive；主体始终保持明确的冲刺方向。
        for (int i = historyCount - 1; i >= 1; i--)
        {
            float ageFade = 1f - i / (float)(historyCount + 1);
            FranVfx.DrawVertical("TraceSoft", centerHistory[i], centerHistory[i - 1],
                new Color(2, 0, 5, 236) * (opacity * ageFade * 0.78f), 30f * ageFade + 5f);
        }
        for (int i = 3; i < historyCount; i += 3)
        {
            float echoFade = (1f - i / (float)(historyCount + 1)) * opacity;
            FranVfx.DrawSharedPixels("FranBlackTigerDash", centerHistory[i],
                Color.Black * (echoFade * 0.30f), tigerRotation, Vector2.One * (76f - i * 2f));
        }
        FranVfx.DrawSharedPixels("FranBlackTigerDash", head + dashDirection * 2f,
            new Color(1, 0, 3, 248) * (opacity * 0.72f), tigerRotation, tigerSize * 1.04f);
        if (launchOpacity > 0f)
            FranVfx.DrawSharedPixels("FranBlackTigerDash", dashOrigin - dashDirection * 5f,
                new Color(1, 0, 3, 246) * (launchOpacity * 0.58f), tigerRotation,
                Vector2.One * (168f + launchProgress * 30f));
        if (ending)
            FranVfx.DrawSharedPixels("FranBlackTigerDash", dashEnd,
                new Color(1, 0, 3, 244) * (brakeOpacity * 0.54f), tigerRotation,
                Vector2.One * MathHelper.Lerp(96f, 154f, brakeProgress));

        FranVfx.BeginAdditive();
        for (int i = historyCount - 1; i >= 1; i--)
        {
            float ageFade = 1f - i / (float)(historyCount + 1);
            Vector2 start = centerHistory[i];
            Vector2 end = centerHistory[i - 1];
            Color body = Color.Lerp(new Color(68, 25, 108), new Color(187, 128, 231), ageFade);
            FranVfx.DrawVertical("TraceSoft", start, end, body * (opacity * ageFade * 0.38f),
                20f * ageFade + 2.5f);
            FranVfx.DrawVertical("TraceLong", start, end, new Color(218, 184, 255) * (opacity * ageFade * 0.40f),
                5.8f * ageFade + 1f);
            FranVfx.DrawVertical("Cal_ThinEndedLine", start, end, Color.White * (opacity * ageFade * 0.54f),
                1.9f * ageFade + 0.5f);

            if (i % 2 == 0)
            {
                float railOffset = 9f + ageFade * 8f;
                FranVfx.DrawVertical("Cal_ThinEndedLine", start + normal * railOffset,
                    end + normal * railOffset, new Color(133, 74, 195) * (opacity * ageFade * 0.34f),
                    2.2f + ageFade * 1.6f);
                FranVfx.DrawVertical("Cal_ThinEndedLine", start - normal * railOffset,
                    end - normal * railOffset, new Color(206, 157, 250) * (opacity * ageFade * 0.25f),
                    1.6f + ageFade * 1.2f);
            }
        }

        for (int i = 1; i < historyCount; i += 3)
        {
            float ageFade = 1f - i / (float)(historyCount + 1);
            FranVfx.DrawOriginalPixels("FranSmearForward", centerHistory[i],
                new Color(132, 70, 194) * (opacity * ageFade * 0.30f), rotation,
                new Vector2(76f + ageFade * 42f, 18f + ageFade * 12f));
        }

        // 单色虎形遮罩只负责黑色主体外侧的蓝紫薄辉光，确保明亮背景下仍能辨认轮廓。
        for (int i = 3; i < historyCount; i += 3)
        {
            float echoFade = (1f - i / (float)(historyCount + 1)) * opacity;
            FranVfx.DrawSharedPixels("FranBlackTigerDash", centerHistory[i],
                new Color(162, 67, 214) * (echoFade * 0.13f), tigerRotation,
                Vector2.One * (78f - i * 2f));
        }
        FranVfx.DrawSharedPixels("FranBlackTigerDash", head + dashDirection * 3f - normal * 1.5f,
            new Color(73, 194, 238) * (opacity * 0.11f), tigerRotation, tigerSize * 1.08f);
        FranVfx.DrawSharedPixels("FranBlackTigerDash", head + dashDirection * 3f + normal * 1.2f,
            new Color(205, 72, 224) * (opacity * 0.21f), tigerRotation, tigerSize * 0.98f);
        FranVfx.DrawSharedPixels("FranBlackTigerDash", head + dashDirection * 5f,
            new Color(224, 202, 255) * (opacity * 0.08f), tigerRotation, tigerSize * 0.84f);

        // Six converging cheek streaks turn the sustained phase into a pounce, while remaining
        // directional enough to read at full dash speed.
        if (!ending)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int claw = 0; claw < 3; claw++)
                {
                    Vector2 clawStart = head - dashDirection * (43f + claw * 5f) +
                        normal * side * (24f + claw * 6f);
                    Vector2 clawEnd = head + dashDirection * (16f - claw * 2f) +
                        normal * side * (15f + claw * 5f);
                    FranVfx.DrawVertical("TraceLong", clawStart, clawEnd,
                        new Color(123, 66, 188) * (opacity * 0.22f), 4.4f - claw * 0.55f);
                    FranVfx.DrawVertical("Cal_ThinEndedLine", clawStart, clawEnd,
                        new Color(211, 182, 249) * (opacity * 0.15f), 1.35f);
                }
            }
        }

        FranVfx.DrawBloom(head, new Color(112, 55, 171), 25f, opacity * 0.32f);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", head + dashDirection * 7f,
            Color.White * (opacity * 0.52f), rotation, new Vector2(68f, 18f));
        FranVfx.DrawOriginalPixels("FranPressureNeedle", head + dashDirection * 13f,
            new Color(222, 192, 255) * (opacity * 0.54f), rotation, new Vector2(78f, 9f));

        if (launchOpacity > 0f)
        {
            Vector2 launchTigerSize = Vector2.One * (158f + launchProgress * 44f);
            FranVfx.DrawSharedPixels("FranBlackTigerDash", dashOrigin - dashDirection * 5f,
                new Color(89, 34, 143) * (launchOpacity * 0.34f), tigerRotation, launchTigerSize);
            FranVfx.DrawSharedPixels("FranBlackTigerDash", dashOrigin - dashDirection * 4f,
                new Color(188, 154, 235) * (launchOpacity * 0.13f), tigerRotation,
                launchTigerSize * 0.88f);
            FranVfx.DrawRing(dashOrigin, new Color(117, 55, 178), 22f + launchProgress * 45f,
                launchOpacity * 0.62f, rotation, "Cal_BloomRing");
            FranVfx.DrawOriginalPixels("FranImpactBurst", dashOrigin - dashDirection * 5f,
                new Color(194, 140, 238) * (launchOpacity * 0.54f), rotation,
                new Vector2(116f + launchProgress * 42f, 58f));
            for (int side = -1; side <= 1; side += 2)
                FranVfx.DrawOriginalPixels("FranSmearForward", dashOrigin - dashDirection * 9f + normal * side * 13f,
                    new Color(218, 178, 255) * (launchOpacity * 0.38f),
                    rotation + side * 0.23f, new Vector2(106f, 18f));
        }

        if (ending)
        {
            FranVfx.DrawSharedPixels("FranBlackTigerDash", dashEnd,
                new Color(76, 27, 122) * (brakeOpacity * 0.28f), tigerRotation,
                Vector2.One * MathHelper.Lerp(92f, 164f, brakeProgress));
            FranVfx.DrawRing(dashEnd, new Color(106, 47, 165), 18f + brakeProgress * 54f,
                brakeOpacity * 0.66f, -rotation, "Cal_HighResHollowCircle");
            FranVfx.DrawOriginalPixels("FranImpactBurst", dashEnd, new Color(169, 105, 220) * (brakeOpacity * 0.52f),
                rotation, new Vector2(104f + brakeProgress * 54f, 52f));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", dashEnd, Color.White * (brakeOpacity * 0.78f),
                rotation, new Vector2(92f, 28f));
        }
        FranVfx.EndAdditive();
        return false;
    }
}
