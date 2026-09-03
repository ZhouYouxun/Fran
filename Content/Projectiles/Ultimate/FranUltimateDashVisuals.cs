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


// 以目标为中心的持续构图层，不参与伤害判定；镜头、角色和观战剑影共用该锚点。
public sealed class FranUltimateTargetField : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = 2;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        int controllerIndex = (int)Projectile.ai[0];
        if (!Main.projectile.IndexInRange(controllerIndex))
        {
            Projectile.Kill();
            return;
        }

        Projectile controller = Main.projectile[controllerIndex];
        if (!controller.active || controller.owner != Projectile.owner ||
            controller.type != ModContent.ProjectileType<FranUltimateController>())
        {
            Projectile.Kill();
            return;
        }

        Projectile.timeLeft = 2;
        Projectile.Center = controller.Center;
        Projectile.localAI[0]++;
        Lighting.AddLight(Projectile.Center, new Vector3(0.16f, 0.32f, 0.46f));

        if (Main.dedServ || Projectile.localAI[1]++ != 0f)
            return;

        Color cyan = new(91, 211, 249);
        FranParticleEffects.BloomPulse(Projectile.Center, cyan, 22f, 154f, 24, 0.86f, true);
        FranParticleEffects.RingPulse(Projectile.Center, cyan, Vector2.One * 24f,
            new Vector2(196f, 124f), 25, Projectile.ai[1], 0.024f, 0.92f);
        FranParticleEffects.RingPulse(Projectile.Center, new Color(207, 161, 255), Vector2.One * 18f,
            new Vector2(132f, 198f), 28, Projectile.ai[1] + MathHelper.PiOver2, -0.022f, 0.72f);
        FranParticleEffects.MysticGlyphs(Projectile.Center, new Color(135, 222, 255), 5, 88f, 0.88f);
        FranConstellationEffects.SpawnBlueResonancePrelude(Projectile.Center,
            Projectile.ai[1].ToRotationVector2(), 0);
        FranVanillaDustEffects.SkillRelease(Projectile.Center, Projectile.ai[1].ToRotationVector2(),
            FranSkill.None, cyan, 24, 1.12f);
        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.72f, Pitch = -0.28f }, Projectile.Center);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float time = Projectile.localAI[0];
        float appear = Utils.GetLerpValue(0f, 10f, time, true);
        float rotation = Projectile.ai[1] + time * 0.018f;
        float pulse = 0.76f + 0.24f * MathF.Sin(time * 0.22f);
        Color cyan = new(88, 204, 245);
        Color violet = new(185, 126, 255);

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            cyan * (0.17f * appear * pulse), rotation, Vector2.One * 286f);
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            violet * (0.12f * appear), -rotation * 0.72f, new Vector2(248f, 178f));
        for (int i = 0; i < 12; i++)
        {
            float angle = rotation + MathHelper.TwoPi * i / 12f;
            float radius = 112f + MathF.Sin(time * 0.16f + i * 1.7f) * 12f;
            Vector2 point = Projectile.Center + angle.ToRotationVector2() * radius;
            Color color = i % 3 == 0 ? new Color(255, 211, 111) : i % 2 == 0 ? violet : cyan;
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", point,
                color * (0.44f * appear * pulse), angle,
                new Vector2(i % 3 == 0 ? 34f : 25f, i % 3 == 0 ? 17f : 12f));
        }
        FranVfx.EndAdditive();
        return false;
    }
}

// 起步冲击与命中冲击分别绘制，使每次突进具有完整的起势和收势。
public sealed class FranUltimateDashLaunch : ModProjectile
{
    private const int Lifetime = 16;

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
        if (Main.dedServ || Projectile.localAI[0]++ != 0f)
            return;

        bool final = Projectile.ai[1] > 0.5f;
        Vector2 direction = Projectile.ai[0].ToRotationVector2();
        Color color = final ? new Color(255, 207, 108) : new Color(88, 207, 247);
        FranParticleEffects.RingPulse(Projectile.Center, color,
            new Vector2(12f, 24f), new Vector2(final ? 188f : 112f, final ? 82f : 58f),
            final ? 20 : 14, direction.ToRotation(), 0f, final ? 1f : 0.78f);
        FranParticleEffects.BloomPulse(Projectile.Center, color, 10f, final ? 98f : 54f,
            final ? 18 : 12, final ? 0.92f : 0.68f, true);
        FranParticleEffects.NeedleFan(Projectile.Center, direction, color, final ? 14 : 6,
            final ? 10f : 6.8f, final ? 1.18f : 0.76f, final ? 0.38f : 0.52f);
        FranVanillaDustEffects.SkillRelease(Projectile.Center, direction, FranSkill.None,
            color, final ? 22 : 9, final ? 1.2f : 0.9f);
        SoundEngine.PlaySound(SoundID.Item71 with
        {
            Volume = final ? 0.86f : 0.42f,
            Pitch = final ? -0.36f : 0.22f + ((int)Projectile.ai[2] % 4) * 0.025f
        }, Projectile.Center);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        bool final = Projectile.ai[1] > 0.5f;
        float progress = 1f - Projectile.timeLeft / (float)Lifetime;
        float fade = MathF.Pow(1f - progress, 1.7f);
        float radius = MathHelper.Lerp(18f, final ? 182f : 104f, FranMotion.EaseOutCubic(progress));
        float rotation = Projectile.ai[0];
        Color color = final ? new Color(255, 207, 108) : new Color(91, 211, 249);

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.58f * fade),
            rotation, new Vector2(radius * 1.42f, radius * 0.62f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.72f * fade),
            rotation, new Vector2(radius * 1.18f, radius * 0.28f));
        FranVfx.EndAdditive();
        return false;
    }
}

// 定格斩痕会在目标处短暂残留；连续斩痕可以重叠形成封锁感。
public sealed class FranUltimateSlashResidue : ModProjectile
{
    private const int Lifetime = 30;

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
        bool final = Projectile.ai[1] > 0.5f;
        float progress = 1f - Projectile.timeLeft / (float)Lifetime;
        float appear = Utils.GetLerpValue(0f, 0.10f, progress, true);
        float fade = appear * MathF.Pow(1f - progress, final ? 0.78f : 1.28f);
        float expansion = FranMotion.EaseOutCubic(progress);
        float rotation = Projectile.ai[0];
        Color body = final ? new Color(255, 206, 101) : new Color(89, 205, 245);
        Color accent = final ? Color.White : new Color(195, 135, 255);
        float length = MathHelper.Lerp(final ? 360f : 176f, final ? 690f : 292f, expansion);

        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center, body * (0.40f * fade),
            rotation, new Vector2(length, final ? 96f : 48f));
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center, Color.White * (0.62f * fade),
            rotation, new Vector2(length * 0.92f, final ? 18f : 10f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, accent * (0.64f * fade),
            rotation, new Vector2(final ? 310f : 142f, final ? 66f : 34f));
        if (final)
        {
            FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center,
                new Color(183, 126, 255) * (0.34f * fade), rotation + MathHelper.PiOver2,
                new Vector2(length * 0.62f, 62f));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.72f * fade),
                rotation + MathHelper.PiOver2, new Vector2(258f, 44f));
        }
        FranVfx.EndAdditive();
        return false;
    }
}
