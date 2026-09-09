using System;
using System.IO;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


public sealed class FranUltimateReadySigil : ModProjectile
{
    private const int Lifetime = 96;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!Main.hardMode || !owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }
        Projectile.Center = owner.Center;
        Lighting.AddLight(owner.Center, new Vector3(0.22f, 0.18f, 0.34f));
        if (Main.dedServ || Projectile.localAI[0]++ != 0f)
            return;

        SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.82f, Pitch = 0.12f }, owner.Center);
        for (int i = 0; i < 40; i++)
        {
            Vector2 velocity = (MathHelper.TwoPi * i / 40f).ToRotationVector2() *
                Main.rand.NextFloat(2.8f, 7.4f);
            Dust dust = Dust.NewDustPerfect(owner.Center, i % 5 == 0 ? DustID.GoldFlame : DustID.GemAmethyst,
                velocity, 55, default, Main.rand.NextFloat(0.85f, 1.45f));
            dust.noGravity = true;
        }
        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(owner.Center, 5f, 1800f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Player owner = Main.player[Projectile.owner];
        float age = Lifetime - Projectile.timeLeft;
        float appear = Utils.GetLerpValue(0f, 12f, age, true);
        float disappear = Utils.GetLerpValue(0f, 22f, Projectile.timeLeft, true);
        float opacity = appear * disappear;
        float expansion = FranMotion.EaseOutCubic(Utils.GetLerpValue(0f, Lifetime, age, true));
        float rotation = Main.GlobalTimeWrappedHourly * 1.3f;

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranResonanceOrb", owner.Center,
            new Color(105, 208, 242) * (0.42f * opacity), rotation,
            Vector2.One * (138f + expansion * 34f));
        FranVfx.DrawOriginalPixels("FranImpactBurst", owner.Center,
            new Color(180, 112, 255) * (0.38f * opacity), -rotation,
            Vector2.One * (126f + expansion * 46f));
        for (int i = 0; i < 10; i++)
        {
            float angle = rotation + i * MathHelper.TwoPi / 10f;
            Vector2 point = owner.Center + angle.ToRotationVector2() * (72f + expansion * 28f);
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", point,
                (i % 2 == 0 ? new Color(255, 215, 120) : new Color(112, 220, 255)) *
                (0.68f * opacity), angle, new Vector2(30f, 16f));
        }
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", owner.Center, Color.White * (0.82f * opacity),
            rotation, new Vector2(94f, 48f));
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranUltimateExecutionSeal : ModProjectile
{
    private const int Lifetime = 24;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override bool PreDraw(ref Color lightColor)
    {
        float duration = Math.Max(1f, Projectile.ai[0]);
        float progress = MathHelper.Clamp((Lifetime - Projectile.timeLeft) / duration, 0f, 1f);
        float pulse = 0.55f + 0.45f * MathF.Sin(progress * MathHelper.Pi * 5f);
        float rotation = Projectile.ai[1] + Main.GlobalTimeWrappedHourly * 1.8f;
        FranVfx.BeginAdditive();
        for (int i = 0; i < 8; i++)
        {
            float angle = rotation + i * MathHelper.PiOver4;
            Vector2 point = Projectile.Center + angle.ToRotationVector2() * (86f - progress * 26f);
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", point,
                (i % 2 == 0 ? new Color(255, 215, 120) : new Color(126, 208, 255)) *
                (0.62f * pulse), angle, new Vector2(38f, 18f));
        }
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(178, 105, 255) * (0.38f * pulse), -rotation, Vector2.One * (150f - progress * 34f));
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranUltimateStrikeBeat : ModProjectile
{
    private const int Lifetime = 24;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        bool final = Projectile.ai[0] > 0.5f;
        Color color = final ? new Color(255, 211, 112) : new Color(110, 205, 245);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * (final ? 0.85f : 0.35f));
        if (Main.dedServ || Projectile.localAI[0]++ != 0f)
            return;

        Vector2 direction = Projectile.ai[1].ToRotationVector2();
        int strike = (int)Projectile.ai[2];
        int normalStrikeCount = FranBalance.GetInt(nameof(FranBalance.UltimateNormalStrikeCount),
            FranBalance.UltimateNormalStrikeCount);
        float strikeProgress = MathHelper.Clamp(strike / (float)Math.Max(1, normalStrikeCount - 1), 0f, 1f);
        SoundEngine.PlaySound((final ? SoundID.Item122 : SoundID.Item71) with
        {
            Volume = final ? 1f : 0.72f,
            Pitch = final ? -0.34f : -0.12f + strike % 5 * 0.045f
        }, Projectile.Center);

        FranParticleEffects.BloomPulse(Projectile.Center, color, final ? 34f : 16f,
            final ? 246f : 108f, final ? 26 : 18, final ? 1f : 0.86f, true);
        FranParticleEffects.RingPulse(Projectile.Center, color,
            new Vector2(final ? 26f : 14f, final ? 42f : 22f),
            new Vector2(final ? 330f : 142f, final ? 162f : 72f),
            final ? 27 : 18, direction.ToRotation(), 0.018f, 0.94f);
        FranParticleEffects.RingPulse(Projectile.Center,
            final ? new Color(193, 132, 255) : new Color(207, 156, 255),
            Vector2.One * (final ? 28f : 12f),
            new Vector2(final ? 210f : 86f, final ? 318f : 132f),
            final ? 30 : 20, direction.ToRotation() + MathHelper.PiOver2, -0.022f, final ? 0.86f : 0.62f);
        FranParticleEffects.NeedleFan(Projectile.Center, direction, color,
            final ? 28 : 12, final ? 12f : 8.2f, final ? 1.42f : 0.92f, final ? 0.78f : 0.92f);
        FranParticleSystem.Spawn(new FranBladeImpactCrossParticle(Projectile.Center, direction,
            Color.Lerp(color, Color.White, 0.28f), final ? 2.4f : 1.35f, final ? 26 : 17));
        int edgeCount = final ? 18 : 7;
        for (int i = 0; i < edgeCount; i++)
        {
            Vector2 velocity = direction.RotatedByRandom(final ? 1.35f : 0.94f) *
                Main.rand.NextFloat(final ? 4.5f : 3f, final ? 13f : 8f);
            FranParticleSystem.Spawn(new FranBladeEdgeMoteParticle(Projectile.Center, velocity,
                velocity, color, Main.rand.NextFloat(final ? 34f : 22f, final ? 74f : 48f),
                Main.rand.NextFloat(1.5f, final ? 3.6f : 2.7f), Main.rand.Next(final ? 13 : 9, final ? 22 : 15)));
        }

        if (final || strike % 4 == 0)
            FranParticleEffects.MysticGlyphs(Projectile.Center,
                final ? new Color(255, 211, 112) : new Color(126, 220, 255),
                final ? 9 : 3, final ? 132f : 72f, final ? 1.22f : 0.76f,
                -direction * (final ? 1.1f : 0.45f));
        if (strike % 3 == 0)
            FranConstellationEffects.SpawnPolarRelease(Projectile.Center, direction, final ? 1.32f : 0.82f);

        int customParticleEstimate = 3 + (final ? 28 : 12) + 1 + edgeCount +
            (final || strike % 4 == 0 ? final ? 9 : 3 : 0);
        FranVanillaDustEffects.Impact(Projectile.Center, direction, FranSkill.None, color,
            customParticleEstimate, final ? 1.45f : 1.03f);
        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center,
            final ? 24f : MathHelper.Lerp(8.5f, 5.8f, strikeProgress), 2400f);

        if (final)
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.42f }, Projectile.Center);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        bool final = Projectile.ai[0] > 0.5f;
        float progress = 1f - Projectile.timeLeft / (float)Lifetime;
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        float rotation = Projectile.ai[1];
        float radius = MathHelper.Lerp(24f, final ? 238f : 106f, FranMotion.EaseOutCubic(progress));
        Color color = final ? new Color(255, 208, 106) : new Color(105, 211, 245);

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.72f * opacity),
            rotation, new Vector2(radius * 2.1f, radius * (final ? 1.12f : 0.78f)));
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center,
            new Color(188, 122, 255) * (0.48f * opacity), rotation,
            new Vector2(radius * 2.35f, radius * 0.52f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.88f * opacity),
            rotation, new Vector2(radius * 1.48f, radius * 0.66f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            color * (0.62f * opacity), rotation + MathHelper.PiOver2,
            new Vector2(radius * (final ? 1.42f : 0.82f), radius * (final ? 0.54f : 0.32f)));
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranUltimateSpectatorBlade : ModProjectile
{
    private const int Lifetime = 30;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 6;
        Projectile.height = 6;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool? CanDamage() => false;

    public override void AI()
    {
        float progress = 1f - Projectile.timeLeft / (float)Lifetime;
        Projectile.velocity = Projectile.velocity.RotatedBy(Projectile.ai[0] * (1f - progress)) * 0.982f;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.Opacity = MathHelper.Clamp(MathF.Sin(progress * MathHelper.Pi) * 1.45f, 0f, 1f);
        Color color = Projectile.ai[1] > 0.5f ? new Color(255, 210, 115) : new Color(105, 205, 245);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * (0.13f * Projectile.Opacity));
    }

    public override bool PreDraw(ref Color lightColor)
    {
        bool final = Projectile.ai[1] > 0.5f;
        Color color = final ? new Color(255, 210, 115) : new Color(115, 207, 245);
        float scale = MathHelper.Clamp(Projectile.ai[2], 0.65f, 1.5f);
        float opacity = Projectile.Opacity;
        FranVfx.BeginAdditive();
        for (int i = Projectile.oldPos.Length - 1; i >= 0; i -= 2)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                continue;
            float fade = 1f - i / (float)Projectile.oldPos.Length;
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            FranVfx.DrawOriginalPixels("FranSmearForward", point, color * (0.28f * fade * opacity),
                Projectile.oldRot[i], new Vector2(54f * fade, 13f * fade) * scale);
        }
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center, color * (0.84f * opacity),
            Projectile.rotation, new Vector2(final ? 64f : 42f, final ? 18f : 12f) * scale);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.62f * opacity),
            Projectile.rotation, new Vector2(25f, 13f) * scale);
        FranVfx.EndAdditive();
        return false;
    }
}
