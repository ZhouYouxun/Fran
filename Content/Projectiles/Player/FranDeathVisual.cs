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


// 死亡演出先持续25帧回收火花，再叠加彩色、白色脉冲与六条轴向闪光。
public sealed class FranDeathVisual : ModProjectile
{
    private const int CollapseTime = 25;
    private float Time => Projectile.localAI[0];

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = 43;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => false;

    public override void AI()
    {
        Projectile.localAI[0]++;
        float age = Time;
        if (age < CollapseTime)
        {
            if (!Main.dedServ && (int)age % 2 == 0)
            {
                FranParticleEffects.InwardSpark(Projectile.Center, new Color(105, 195, 255),
                    90f, 180f, 7.4f, 1.05f);
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawn = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(90f, 180f);
                Dust dust = Dust.NewDustPerfect(spawn, DustID.FireworksRGB,
                    (Projectile.Center - spawn).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3.8f, 7.4f),
                    40, new Color(105, 195, 255), Main.rand.NextFloat(0.65f, 1.05f));
                dust.noGravity = true;
            }
        }
        else if ((int)age == CollapseTime)
        {
            FranParticleEffects.Implosion(Projectile.Center, new Color(115, 205, 255), 1.55f);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.42f, Volume = 0.92f }, Projectile.Center);
            FranVfx.SparkBurst(Projectile.Center, new Color(115, 205, 255), 30, 15f, 1.15f);
            FranVfx.Burst(Projectile.Center, DustID.PurpleTorch, 18, 9f, 1.25f);
            if (Main.player.IndexInRange(Projectile.owner))
                Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 8f);
        }

        float light = age < CollapseTime ? MathHelper.Lerp(0.18f, 0.72f, age / CollapseTime) :
            MathHelper.Lerp(1f, 0f, FranMotion.Clamp01((age - CollapseTime) / 18f));
        Lighting.AddLight(Projectile.Center, new Vector3(0.16f, 0.38f, 0.62f) * light);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float age = Time;
        Color blue = new(75, 165, 255);
        Color cyan = new(125, 245, 255);
        FranVfx.BeginAdditive();

        if (age < CollapseTime)
        {
            float completion = FranMotion.Clamp01(age / CollapseTime);
            float collapse = FranMotion.EaseInCubic(completion);
            float radius = MathHelper.Lerp(220f, 15f, collapse);
            float opacity = 0.28f + completion * 0.62f;
            for (int i = 0; i < 6; i++)
            {
                float angle = i * MathHelper.TwoPi / 6f + Projectile.identity * 0.37f + age * 0.025f;
                Vector2 direction = angle.ToRotationVector2();
                Vector2 point = Projectile.Center + direction * radius;
                FranVfx.DrawTexturedSpark(point, -direction, i % 2 == 0 ? blue : cyan,
                    36f + completion * 54f, 3.5f + completion * 4.5f, opacity);
            }
            FranVfx.DrawShared("Cal_HighResHollowCircle", Projectile.Center, blue * (0.30f + completion * 0.34f),
                -age * 0.035f, 0.018f + (1f - completion) * 0.085f);
            FranVfx.DrawShared("Cal_PulseStar", Projectile.Center, Color.White * (completion * 0.62f),
                age * 0.08f, 0.08f + completion * 0.12f);
            FranVfx.DrawBloom(Projectile.Center, cyan, 18f + completion * 34f, 0.22f + completion * 0.36f);
        }
        else
        {
            float completion = FranMotion.Clamp01((age - CollapseTime) / 18f);
            float expansion = 1f - MathF.Pow(1f - completion, 4f);
            float opacity = 1f - completion;

            // 三层彩色脉冲与三层白色脉冲使用不同尺寸和旋转角度。
            for (int i = 0; i < 3; i++)
            {
                float radius = MathHelper.Lerp(18f + i * 9f, 150f + i * 62f, expansion);
                FranVfx.DrawRing(Projectile.Center, i % 2 == 0 ? blue : cyan, radius,
                    opacity * (0.64f - i * 0.10f), i * 0.47f + age * 0.02f, "Cal_BloomRing");
                FranVfx.DrawRing(Projectile.Center, Color.White, radius * 0.58f,
                    opacity * (0.50f - i * 0.08f), -i * 0.61f - age * 0.018f, "Cal_BloomRing");
            }

            for (int i = 0; i < 6; i++)
            {
                float angle = i * MathHelper.Pi / 3f;
                Vector2 direction = angle.ToRotationVector2();
                Vector2 point = Projectile.Center + direction * expansion * 78f;
                FranVfx.DrawTexturedSpark(point, direction, i % 2 == 0 ? cyan : blue,
                    MathHelper.Lerp(42f, 126f, expansion), MathHelper.Lerp(9f, 2.5f, completion), opacity);
            }

            // Thirty small shards form the irregular outer break-up from the original death effect.
            for (int i = 0; i < 30; i++)
            {
                float angle = i * MathHelper.TwoPi / 30f + MathF.Sin(i * 4.13f + Projectile.identity) * 0.12f;
                float speed = 70f + (i % 7) * 11f;
                Vector2 point = Projectile.Center + angle.ToRotationVector2() * speed * expansion;
                FranVfx.DrawShared(i % 3 == 0 ? "Cal_SimpleStar" : "Cal_StarProj", point,
                    Color.Lerp(blue, Color.White, i % 4 * 0.18f) * (opacity * 0.52f),
                    angle + age * (i % 2 == 0 ? 0.06f : -0.045f), 0.032f + i % 5 * 0.004f);
            }

            FranVfx.DrawShared("Cal_HighResHollowCircle", Projectile.Center, cyan * (0.72f * opacity),
                MathHelper.PiOver4, 0.018f + expansion * 0.18f);
            FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center, Color.White * (0.88f * opacity),
                0f, new Vector2(0.48f, 0.09f) * (0.55f + expansion * 0.45f));
            FranVfx.DrawBloom(Projectile.Center, Color.White, MathHelper.Lerp(92f, 28f, completion), 0.62f * opacity, true);
        }

        FranVfx.EndAdditive();
        return false;
    }
}
