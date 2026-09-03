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


public sealed class MagicBarrierProjectile : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 36; Projectile.height = 150; Projectile.timeLeft = 300;
        Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }
        Projectile.rotation = Projectile.ai[0] + MathHelper.PiOver2;
        Projectile.Center = owner.MountedCenter + Projectile.ai[0].ToRotationVector2() * 82f;
        Projectile.ai[1]++;
        float deploy = FranMotion.EaseOutBack(FranMotion.Segment(Projectile.ai[1], 0f, 14f));
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            Vector2 axis = Projectile.rotation.ToRotationVector2() * (74f * deploy);
            Vector2 lineStart = Projectile.Center - axis;
            Vector2 lineEnd = Projectile.Center + axis;
            foreach (Projectile other in Main.ActiveProjectiles)
            {
                float collisionPoint = 0f;
                bool intersects = Collision.CheckAABBvLineCollision(other.Hitbox.TopLeft(), other.Hitbox.Size(),
                    lineStart, lineEnd, 22f, ref collisionPoint);
                if (!other.hostile || other.damage <= 0 || !intersects)
                    continue;
                other.Kill();
                Projectile.ai[2]++;
                FranBurst.Spawn(Projectile.GetSource_FromThis(), other.Center, Projectile.owner,
                    FranBurstStyle.Barrier, 0.55f);
                Projectile.netUpdate = true;
                if (Projectile.ai[2] >= 3)
                {
                    Projectile.Kill();
                    break;
                }
            }
        }
        Lighting.AddLight(Projectile.Center, 0.08f, 0.32f, 0.55f);
        if (!Main.dedServ && ((int)Projectile.ai[1] & 1) == 0)
            FranParticleEffects.InwardSpark(Projectile.Center, new Color(105, 195, 255),
                48f * deploy, 82f * Math.Max(0.2f, deploy), 2.2f, 0.72f);
    }
    public override void OnKill(int timeLeft)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            FranBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center, Projectile.owner,
                FranBurstStyle.Barrier, 1.15f);
        FranVfx.SparkBurst(Projectile.Center, new Color(90, 185, 255), 14, 6f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float deploy = FranMotion.EaseOutBack(FranMotion.Segment(Projectile.ai[1], 0f, 14f));
        float integrity = 1f - FranMotion.Clamp01(Projectile.ai[2] / 3f);
        Vector2 axis = Projectile.rotation.ToRotationVector2() * (74f * deploy);
        Vector2 side = axis.RotatedBy(MathHelper.PiOver2) * 0.22f;
        Color voidColor = new(14, 7, 28, 210);
        Color color = Color.Lerp(new Color(78, 80, 225), new Color(185, 245, 255),
            (1f - integrity) * 0.52f);
        float pulse = 0.92f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4.8f + Projectile.identity) * 0.06f;

        // 吞光核心必须使用普通 AlphaBlend；纯黑放入 Additive 会失去可见贡献。
        FranVfx.DrawShared("Cal_CircularSmearSmokey", Projectile.Center, voidColor * (0.72f * deploy),
            Projectile.rotation + Main.GlobalTimeWrappedHourly * 0.16f,
            new Vector2(0.48f, 0.17f) * pulse * deploy);
        FranVfx.DrawShared("Cal_HighResHollowCircle", Projectile.Center, new Color(8, 4, 18, 185) * deploy,
            -Projectile.rotation, new Vector2(0.064f, 0.026f) * pulse * deploy);

        FranVfx.BeginAdditive();
        FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center,
            new Color(105, 70, 255) * (0.48f * deploy), Projectile.rotation,
            new Vector2(0.84f, 0.42f) * pulse * deploy);
        FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center,
            new Color(105, 210, 255) * (0.38f * deploy), Projectile.rotation + MathHelper.Pi,
            new Vector2(0.84f, 0.42f) * pulse * deploy);
        FranVfx.DrawShared("Cal_BloomRing", Projectile.Center, color * (0.42f * deploy),
            Main.GlobalTimeWrappedHourly * 0.7f, new Vector2(0.72f, 0.28f) * pulse * deploy);
        if (Projectile.ai[2] > 0f)
            FranVfx.DrawShared("Crack", Projectile.Center, new Color(170, 225, 255) * (0.16f + Projectile.ai[2] * 0.09f),
                Projectile.rotation, new Vector2(0.20f, 0.38f));
        FranVfx.DrawVertical("Cal_BloomLineSoftEdge", Projectile.Center - axis - side,
            Projectile.Center + axis - side, color * 0.58f, 15f);
        FranVfx.DrawVertical("Cal_BloomLineThick", Projectile.Center - axis + side,
            Projectile.Center + axis + side, Color.White * 0.72f, 5f);
        for (int i = -3; i <= 3; i++)
            FranVfx.DrawVertical("Cal_ThinEndedLine", Projectile.Center + axis * (i / 3f) - side,
                Projectile.Center + axis * (i / 3f) + side, color * 0.42f, 3f);
        FranVfx.DrawBloom(Projectile.Center, new Color(95, 110, 255), 38f, 0.28f * deploy);
        FranVfx.EndAdditive();

        Texture2D teacher = FranTeacherVisuals.Texture;
        Main.EntitySpriteDraw(teacher, Projectile.Center - Main.screenPosition, null,
            Color.Lerp(lightColor, Color.White, 0.18f), Projectile.rotation + MathHelper.PiOver4,
            teacher.Size() * 0.5f, 0.86f * FranTeacherVisuals.TextureScale, SpriteEffects.None, 0f);
        return false;
    }
}

public sealed class FlameFangAuraVisual : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = 10 * 60;
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
        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        int element = (int)Projectile.ai[0];
        bool active = element == 0 && fran.BlueResonanceTimer > 0;
        if (!owner.active || owner.dead || !active)
        {
            Projectile.Kill();
            return;
        }

        Projectile.Center = owner.Center;
        Vector3 light = element switch
        {
            1 => new Vector3(0.34f, 0.10f, 0.025f),
            2 => new Vector3(0.08f, 0.30f, 0.27f),
            3 => new Vector3(0.18f, 0.10f, 0.42f),
            _ => new Vector3(0.04f, 0.24f, 0.42f)
        };
        Lighting.AddLight(owner.Center, light);
        if (!Main.dedServ && Projectile.timeLeft % 5 == 0)
        {
            Color auraColor = element switch
            {
                1 => new Color(255, 105, 45),
                2 => new Color(105, 225, 195),
                3 => new Color(175, 115, 255),
                _ => new Color(105, 195, 255)
            };
            FranParticleEffects.AuraMote(owner.Center, auraColor, element == 1);
        }
        if (!Main.dedServ && Projectile.timeLeft % 14 == 0)
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 spawn = owner.Center + angle.ToRotationVector2() * Main.rand.NextFloat(30f, 48f);
            int dustType = element == 1 ? DustID.Torch : element == 2 ? DustID.Cloud :
                element == 3 ? DustID.Electric : DustID.BlueTorch;
            Dust dust = Dust.NewDustPerfect(spawn, dustType,
                (owner.Center - spawn).SafeNormalize(-Vector2.UnitY) * 0.7f - Vector2.UnitY * 0.8f,
                70, default, Main.rand.NextFloat(0.8f, 1.15f));
            dust.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float age = 10f * 60f - Projectile.timeLeft;
        float introduction = FranMotion.Smooth(FranMotion.Segment(age, 0f, 18f));
        float ending = FranMotion.Smooth(FranMotion.Segment(Projectile.timeLeft, 0f, 25f));
        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.4f + Projectile.identity);
        float opacity = introduction * ending;
        int element = (int)Projectile.ai[0];
        Color outer = element switch
        {
            1 => Color.Lerp(new Color(255, 75, 24), new Color(255, 205, 82), pulse * 0.55f),
            2 => Color.Lerp(new Color(60, 180, 170), new Color(205, 255, 240), pulse * 0.50f),
            3 => Color.Lerp(new Color(105, 60, 225), new Color(220, 190, 255), pulse * 0.55f),
            _ => Color.Lerp(new Color(25, 115, 225), new Color(165, 245, 255), pulse * 0.55f)
        };
        Color inner = Color.Lerp(outer, Color.White, 0.62f);

        FranVfx.BeginAdditive();
        FranVfx.DrawShared("Cal_CircularSmear", Projectile.Center, outer * (0.20f * opacity),
            Main.GlobalTimeWrappedHourly * 0.62f, new Vector2(0.72f, 0.48f) * (0.92f + pulse * 0.12f));
        FranVfx.DrawShared("Cal_BloomRing", Projectile.Center, outer * (0.24f * opacity),
            -Main.GlobalTimeWrappedHourly * 0.47f, 0.42f + pulse * 0.045f);
        FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center + new Vector2(0f, 4f),
            inner * (0.28f * opacity), Main.GlobalTimeWrappedHourly * 0.82f,
            new Vector2(0.38f, 0.31f));
        for (int i = 0; i < 5; i++)
        {
            float angle = Main.GlobalTimeWrappedHourly * (0.32f + i * 0.045f) + i * MathHelper.TwoPi / 5f;
            Vector2 bubble = Projectile.Center + angle.ToRotationVector2() * (30f + i % 2 * 10f + pulse * 4f);
            FranVfx.DrawShared("Cal_BloomCircle", bubble, (i % 2 == 0 ? outer : inner) * (0.11f * opacity),
                angle, 0.035f + i * 0.004f);
            FranVfx.DrawShared("Cal_SmallBloomRingLayered", bubble, Color.White * (0.12f * opacity),
                -angle, 0.18f + i * 0.015f);
        }
        FranVfx.DrawShared("Cal_BloomFlare", Projectile.Center, inner * (0.22f * opacity),
            Main.GlobalTimeWrappedHourly * 0.35f, new Vector2(0.075f, 0.022f));
        FranVfx.DrawBloom(Projectile.Center, outer, 38f + pulse * 7f, 0.18f * opacity);
        FranVfx.EndAdditive();
        return false;
    }
}
