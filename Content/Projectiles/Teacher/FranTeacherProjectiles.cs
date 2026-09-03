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


public sealed class FloatingTeacherVisual : ModProjectile
{
    public override string Texture => FranTeacherVisuals.TexturePath;

    public override void SetDefaults()
    {
        Projectile.width = 30;
        Projectile.height = 60;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }

    public override bool? CanDamage() => false;

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        int flightVisualType = ModContent.ProjectileType<FranTelekineticFlightVisual>();
        if (!owner.active || owner.dead || !fran.HasTeacher || fran.TelekineticFlightVisualActive ||
            owner.ownedProjectileCounts[flightVisualType] > 0 ||
            owner.HeldItem.type == ModContent.ItemType<Content.Items.Teacher>() ||
            owner.HeldItem.type == ModContent.ItemType<Content.Items.TeacherExcavationTool>())
        {
            Projectile.Kill();
            return;
        }

        Vector2 target = owner.Center + new Vector2(-owner.direction * 32f, -30f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 5f);
        Projectile.Center = Vector2.Lerp(Projectile.Center, target, 0.18f);
        Projectile.rotation = -0.62f * owner.direction;
        Projectile.timeLeft = 2;
        Lighting.AddLight(Projectile.Center, 0.08f, 0.18f, 0.28f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Player owner = Main.player[Projectile.owner];
        Texture2D texture = FranTeacherVisuals.Texture;
        Vector2 position = Projectile.Center - Main.screenPosition;
        Vector2 origin = texture.Size() * 0.5f;
        SpriteEffects effects = owner.direction == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        for (int i = 0; i < 5; i++)
            Main.EntitySpriteDraw(texture, position + (MathHelper.TwoPi * i / 5f).ToRotationVector2() * 2f, null,
                new Color(100, 205, 255) * 0.28f, Projectile.rotation, origin,
                0.82f * FranTeacherVisuals.TextureScale, effects, 0f);
        Main.EntitySpriteDraw(texture, position, null, lightColor, Projectile.rotation, origin,
            0.82f * FranTeacherVisuals.TextureScale, effects, 0f);
        return false;
    }
}

public sealed class TelekineticCannonProjectile : ModProjectile
{
    public override string Texture => FranTeacherVisuals.TexturePath;
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 10;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 36; Projectile.height = 36; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 70;
        Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool? CanDamage() => Projectile.ai[0] == 0f ? null : false;

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }

        float age = Projectile.localAI[0]++;
        if (Projectile.ai[0] == 0f)
        {
            if (age < 7f && Projectile.velocity.Length() < 22f)
                Projectile.velocity *= 1.055f;
            if (age >= 20f || Vector2.DistanceSquared(Projectile.Center, owner.Center) > 680f * 680f)
            {
                Projectile.ai[0] = 1f;
                Projectile.tileCollide = false;
                Projectile.netUpdate = true;
            }
        }
        else
        {
            Vector2 toOwner = owner.MountedCenter - Projectile.Center;
            if (toOwner.LengthSquared() < 38f * 38f)
            {
                Projectile.Kill();
                return;
            }
            Vector2 desired = toOwner.SafeNormalize(Vector2.UnitX * owner.direction) * 23f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.20f);
        }

        Lighting.AddLight(Projectile.Center, 0.1f, 0.42f, 0.65f);
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        if (!Main.dedServ && age > 2f)
            FranParticleEffects.StarlightWake(Projectile.Center, Projectile.velocity,
                Projectile.ai[0] == 0f ? new Color(135, 225, 255) : new Color(95, 155, 255));
        if (age > 3f && Main.rand.NextBool(4))
        {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch,
                -Projectile.velocity * 0.04f + Main.rand.NextVector2Circular(0.8f, 0.8f), 70, default, 1.05f);
            d.noGravity = true;
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float speedRatio = FranMotion.Clamp01((Projectile.velocity.Length() - 10f) / 13f);
        Color deepColor = new(35, 85, 210);
        Color edgeColor = Color.Lerp(new Color(55, 145, 255), new Color(185, 245, 255), speedRatio);
        float pulse = 0.86f + MathF.Sin(Main.GlobalTimeWrappedHourly * 18f + Projectile.identity) * 0.08f;
        float opacity = FranMotion.Smooth(FranMotion.Segment(Projectile.localAI[0], 0f, 4f)) *
            FranMotion.Smooth(FranMotion.Segment(Projectile.timeLeft, 0f, 14f));
        FranVfx.BeginAdditive();

        for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
        {
            Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            Vector2 newerCenter = Projectile.oldPos[i - 1] + Projectile.Size * 0.5f;
            if (oldCenter == Projectile.Size * 0.5f || newerCenter == Projectile.Size * 0.5f)
                continue;
            float completion = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawStretched("Cal_FadeStreak", oldCenter, newerCenter,
                Color.Lerp(deepColor, edgeColor, completion) * (completion * 0.32f * opacity),
                MathHelper.Lerp(4f, 18f, completion));
        }
        FranVfx.DrawVertical("TraceSoft", Projectile.Center - direction * (72f + speedRatio * 36f),
            Projectile.Center + direction * 12f, edgeColor * (0.38f * opacity), 18f + speedRatio * 8f);
        FranVfx.DrawShared("Cal_BloomCirclePinpoint", Projectile.Center, edgeColor * (0.72f * opacity),
            0f, 0.13f * pulse);
        FranVfx.DrawShared("Cal_BloomFlare", Projectile.Center, Color.White * (0.56f * opacity),
            Projectile.rotation, new Vector2(0.055f, 0.018f) * pulse);
        FranVfx.DrawShared("Cal_PulseStar", Projectile.Center, Color.White * (0.88f * opacity),
            -Projectile.rotation * 0.35f, 0.10f * pulse);
        FranVfx.EndAdditive();

        Texture2D texture = FranTeacherVisuals.Texture;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.Lerp(lightColor, Color.White, 0.22f), Projectile.rotation, texture.Size() * 0.5f,
            0.92f * FranTeacherVisuals.TextureScale, SpriteEffects.None, 0f);
        return false;
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Projectile.ai[0] = 1f;
        Projectile.tileCollide = false;
        Projectile.velocity = -oldVelocity * 0.55f;
        Projectile.netUpdate = true;
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner,
            FranBurstStyle.Telekinetic, 0.58f);
    }
    public override void OnKill(int timeLeft)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_Death(), Projectile.Center, Projectile.owner,
            FranBurstStyle.Telekinetic, 0.78f);
        FranVfx.SparkBurst(Projectile.Center, new Color(120, 225, 255), 7, 4.5f);
    }
}

internal static class TeacherWaveVisuals
{
    public static void GetPalette(int stage, int element, out Color deep, out Color body, out Color edge)
    {
        if (element == 1)
        {
            deep = new Color(190, 28, 8);
            body = new Color(255, 92, 28);
        }
        else
        {
            deep = stage >= 5 ? new Color(40, 52, 205) : new Color(25, 105, 210);
            body = stage >= 4 ? new Color(70, 185, 255) : new Color(65, 158, 242);
        }
        edge = Color.Lerp(body, Color.White, stage >= 4 ? 0.74f : 0.58f);
    }

    public static void Light(Vector2 center, int element, float strength = 1f) =>
        Lighting.AddLight(center, (element == 1 ? new Vector3(0.84f, 0.20f, 0.03f) : new Vector3(0.08f, 0.34f, 0.62f)) * strength);

    public static void Hit(Projectile projectile, NPC target, int element, float size, int sparks, float sparkSpeed)
    {
        if (element == 1)
            target.AddBuff(BuffID.OnFire3, 180);
        FranBurst.SpawnForOwner(projectile.GetSource_OnHit(target), target.Center, projectile.owner,
            element == 1 ? FranBurstStyle.Flame : FranBurstStyle.Slash, size);
        FranVfx.SparkBurst(target.Center, element == 1 ? new Color(255, 110, 45) : new Color(125, 215, 255),
            sparks, sparkSpeed, 0.95f + size * 0.25f);
    }

    public static void Death(Projectile projectile, int element, float size, int dustCount, float dustSpeed)
    {
        FranVfx.Burst(projectile.Center, element == 1 ? DustID.Torch : DustID.BlueTorch,
            dustCount, dustSpeed, 1.05f);
        FranBurst.SpawnForOwner(projectile.GetSource_Death(), projectile.Center, projectile.owner,
            element == 1 ? FranBurstStyle.Flame : FranBurstStyle.Slash, size);
    }
}

// 五层错位灰度波纹分别控制横向与纵向扩张，避免所有层完全重合。
public sealed class TeacherCrescentWave : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    private int Stage => (int)Projectile.ai[0];
    private int Combo => (int)Projectile.ai[1];
    private int Element => (int)Projectile.ai[2];
    private float FaceLength => Stage >= 5 ? 92f : Stage >= 4 ? 82f : 72f;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 360;
    }

    public override void SetDefaults()
    {
        Projectile.width = 44;
        Projectile.height = 44;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 72;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 14;
    }

    public override bool? CanDamage() => Projectile.localAI[0] >= 2f && Projectile.timeLeft > 7 ? null : false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        float curveSign = Combo == 1 ? -1f : 1f;
        if (age < 7f)
            Projectile.velocity *= 1.035f;
        else if (Projectile.timeLeft < 15)
            Projectile.velocity *= 0.94f;

        float curve = curveSign * MathHelper.Lerp(0.0105f, 0.0015f, FranMotion.Clamp01(age / 50f));
        Projectile.velocity = Projectile.velocity.RotatedBy(curve);
        float speed = Projectile.velocity.Length();
        if (Stage >= 4 && age > 10f)
        {
            NPC target = FranVfx.ClosestTarget(Projectile.Center, 500f);
            if (target != null)
            {
                float desiredAngle = (target.Center - Projectile.Center).ToRotation();
                float steeredAngle = Projectile.velocity.ToRotation().AngleTowards(desiredAngle, 0.025f);
                Projectile.velocity = steeredAngle.ToRotationVector2() * speed;
            }
        }

        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ && age > 3f && age < 58f)
        {
            TeacherWaveVisuals.GetPalette(Stage, Element, out _, out Color wakeColor, out _);
            FranParticleEffects.CrescentWake(Projectile.Center, Projectile.velocity, wakeColor, FaceLength);
        }
        if (!Main.dedServ && age > 3f && age < 58f)
        {
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + normal * Main.rand.NextFloat(-FaceLength * 0.76f, FaceLength * 0.76f),
                    Element == 1 ? DustID.Torch : DustID.BlueTorch,
                    -direction * Main.rand.NextFloat(0.5f, 1.8f) + normal * Main.rand.NextFloat(-1.1f, 1.1f),
                    65, default, Main.rand.NextFloat(0.82f, 1.16f));
                dust.noGravity = true;
            }
        }
        TeacherWaveVisuals.Light(Projectile.Center, Element, 0.9f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Vector2 normal = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - normal * FaceLength, Projectile.Center + normal * FaceLength, 14f, ref collisionPoint);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float age = 72f - Projectile.timeLeft;
        float opacity = FranMotion.Smooth(FranMotion.Segment(age, 0f, 5f)) *
            (1f - FranMotion.Smooth(FranMotion.Segment(age, 57f, 72f)));
        TeacherWaveVisuals.GetPalette(Stage, Element, out Color deep, out Color body, out Color edge);
        Texture2D wave = FranVfx.Shared("Cal_JudgementWave");
        float waveFade = Utils.GetLerpValue(0f, 57f, Projectile.timeLeft, true);
        float expansion = MathHelper.Lerp(0.72f, 1.08f, FranMotion.Smooth(FranMotion.Segment(age, 0f, 27f)));
        float baseScale = FaceLength * 2f / wave.Width * expansion;
        float drawRotation = Projectile.rotation - MathHelper.PiOver2 + (Combo == 1 ? MathHelper.Pi : 0f);
        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
        // This block already uses BlendState.Additive. Unlike Calamity's A=0 trick inside
        // AlphaBlend, a zero source alpha here suppresses the entire contribution.
        Color layerColor = body;
        Color coreColor = edge;

        FranVfx.BeginAdditive();
        FranVfx.DrawShared("Cal_VerticalSmearLarge", Projectile.Center - forward * 8f,
            deep * (0.16f * opacity), drawRotation,
            new Vector2(baseScale * 0.78f, baseScale * 0.46f));
        for (int i = 0; i < 5; i++)
        {
            Vector2 scale = new(1f - 0.2f * waveFade + 0.01f * i,
                1f + 0.45f * waveFade - 0.06f * i);
            Main.EntitySpriteDraw(wave, Projectile.Center - Main.screenPosition + forward * 9f * i,
                null, layerColor * (0.54f * opacity), drawRotation, wave.Size() * 0.5f,
                scale * baseScale, SpriteEffects.None, 0f);
        }
        Main.EntitySpriteDraw(wave, Projectile.Center - Main.screenPosition + forward * 12f,
            null, coreColor * (0.46f * opacity), drawRotation, wave.Size() * 0.5f,
            new Vector2(0.78f, 1.12f) * baseScale, SpriteEffects.None, 0f);

        DrawWingSparks(Projectile.Center + normal * FaceLength * 0.82f, normal, age, body, edge, opacity, 1f);
        DrawWingSparks(Projectile.Center - normal * FaceLength * 0.82f, -normal, age, body, edge, opacity, -1f);
        FranVfx.EndAdditive();
        return false;
    }

    private static void DrawWingSparks(Vector2 wing, Vector2 outward, float age, Color body, Color edge,
        float opacity, float phaseOffset)
    {
        for (int i = 0; i < 3; i++)
        {
            float sparkAge = (age + i * 4f + phaseOffset * 2f) % 15f;
            float completion = sparkAge / 15f;
            Vector2 position = wing + outward * sparkAge * (0.72f + i * 0.12f) +
                outward.RotatedBy(MathHelper.PiOver2) * MathF.Sin(age * 0.24f + i * 2.1f) * 4f;
            Color color = Color.Lerp(edge, body, completion);
            FranVfx.DrawShared("Cal_HalfStar", position, color * (opacity * (1f - completion) * 0.72f),
                outward.ToRotation(), new Vector2(0.38f, 1.05f) * (0.72f + i * 0.14f));
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) =>
        TeacherWaveVisuals.Hit(Projectile, target, Element, 0.48f, 7, 4.5f);

    public override void OnKill(int timeLeft) => TeacherWaveVisuals.Death(Projectile, Element, 0.42f, 10, 4.2f);
}

// 星光质量体依次经历悬停、锁定与突进；主体淡出后，拖尾继续完成闭合。
public sealed class TeacherStarlightMass : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    private int Stage => (int)Projectile.ai[0];
    private int Element => (int)Projectile.ai[2];

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 24;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;
    }

    public override void SetDefaults()
    {
        Projectile.width = 28;
        Projectile.height = 28;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 0;
    }

    private bool FakeKilling => Projectile.ai[1] >= 1f;

    public override bool? CanDamage() => !FakeKilling && Projectile.localAI[0] >= 30f ? null : false;

    public override void AI()
    {
        if (FakeKilling)
        {
            Projectile.localAI[1]++;
            Projectile.velocity = Vector2.Zero;
            Projectile.friendly = false;
            Projectile.alpha = 255;
            Projectile.timeLeft = 2;
            if (Projectile.localAI[1] >= 36f)
                Projectile.Kill();
            return;
        }

        float age = Projectile.localAI[0]++;
        if (Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height))
        {
            TriggerFakeKill();
            return;
        }

        if (age < 30f)
            Projectile.velocity *= 0.98f;
        else
        {
            NPC target = FranVfx.ClosestTarget(Projectile.Center, 900f);
            if (target != null)
            {
                float targetSpeed = age < 130f ? 30f : 50f;
                Vector2 desiredVelocity = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity) * targetSpeed;
                Projectile.velocity = (Projectile.velocity * 49f + desiredVelocity) / 50f;
            }
            else
                Projectile.velocity *= 0.98f;
        }

        float direction = Math.Sign(Projectile.velocity.X);
        if (direction == 0f)
            direction = 1f;
        Projectile.rotation += (Projectile.velocity.X + MathF.Abs(Projectile.velocity.Y) * direction) * 0.01f;

        if (!Main.dedServ)
        {
            TeacherWaveVisuals.GetPalette(Stage, Element, out _, out Color body, out Color edge);
            if (age < 30f)
                FranParticleEffects.StarlightOrbit(Projectile.Center, Color.Lerp(body, edge, 0.5f), age);
            else
                FranParticleEffects.StarlightWake(Projectile.Center, Projectile.velocity, edge);
        }

        if (!Main.dedServ && age > 30f && Main.rand.NextBool(3))
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.5f,
                Element == 1 ? DustID.GoldFlame : DustID.BlueTorch, -Projectile.velocity * Main.rand.NextFloat(0.08f, 0.18f),
                60, default, Main.rand.NextFloat(0.78f, 1.08f));
            dust.noGravity = true;
        }
        TeacherWaveVisuals.Light(Projectile.Center, Element, age < 30f ? 0.82f : 1.05f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float radius = 18f + Projectile.scale * 8f;
        Vector2 closest = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(),
            new Vector2(targetHitbox.Right, targetHitbox.Bottom));
        return Vector2.DistanceSquared(Projectile.Center, closest) <= radius * radius;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float age = Projectile.localAI[0];
        float fakeOpacity = FakeKilling ? 1f - FranMotion.Clamp01(Projectile.localAI[1] / 36f) : 1f;
        TeacherWaveVisuals.GetPalette(Stage, Element, out Color deep, out Color body, out Color edge);
        Color deepAdditive = deep;
        Color bodyAdditive = body;
        Color edgeAdditive = edge;

        FranVfx.BeginAdditive();
        Vector2 previous = Projectile.Center;
        for (int i = 0; i < Projectile.oldPos.Length; i++)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                break;
            float completion = 1f - i / (float)Projectile.oldPos.Length;
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            FranVfx.DrawStretched("Red_Trail4", point, previous,
                deepAdditive * (completion * completion * 0.30f * fakeOpacity), 84f * completion);
            FranVfx.DrawStretched("Red_Trail4", point, previous,
                edgeAdditive * (completion * completion * 0.34f * fakeOpacity), 42f * completion);
            if (i % 4 == 0)
                FranVfx.DrawShared("Red_WhiteFlare", point, bodyAdditive * (completion * 0.16f * fakeOpacity),
                    Projectile.oldRot[i], 0.26f + completion * 0.18f);
            previous = point;
        }

        if (!FakeKilling)
        {
            if (age < 30f)
            {
                float auraOpacity = 1f - age / 30f;
                float auraScale = MathHelper.Lerp(0.42f, 0.62f, FranMotion.Smooth(age / 30f));
                FranVfx.DrawShared("Red_HolyGlow2", Projectile.Center, Color.White * (0.18f * auraOpacity),
                    Projectile.rotation + age * 0.06f, auraScale);
                FranVfx.DrawShared("Red_HolyGlow2", Projectile.Center, Color.White * (0.13f * auraOpacity),
                    -Projectile.rotation - age * 0.04f, auraScale * 0.86f);
            }

            float pulse = 0.92f + MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity) * 0.08f;
            FranVfx.DrawShared("Red_WhiteFlare", Projectile.Center, bodyAdditive * 0.82f,
                Projectile.rotation, 0.64f * pulse);
            FranVfx.DrawShared("Cal_StarOfJudgement", Projectile.Center, bodyAdditive * 0.86f,
                Projectile.rotation, 0.62f * pulse);
            FranVfx.DrawShared("Cal_StarOfJudgement", Projectile.Center, Color.White,
                Projectile.rotation, 0.48f * pulse);
        }
        FranVfx.EndAdditive();
        return false;
    }

    private void TriggerFakeKill()
    {
        if (FakeKilling)
            return;
        Projectile.ai[1] = 1f;
        Projectile.localAI[1] = 0f;
        Projectile.friendly = false;
        Projectile.velocity = Vector2.Zero;
        Projectile.netUpdate = true;
        if (!Main.dedServ)
        {
            TeacherWaveVisuals.GetPalette(Stage, Element, out _, out Color body, out Color edge);
            FranParticleEffects.Implosion(Projectile.Center, Color.Lerp(body, edge, 0.55f), 1.05f);
            for (int i = 0; i < 8; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center,
                    Element == 1 ? DustID.GoldFlame : DustID.BlueTorch,
                    Main.rand.NextVector2Circular(4.5f, 4.5f), 40, default, Main.rand.NextFloat(0.95f, 1.28f));
                dust.noGravity = true;
            }
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        TeacherWaveVisuals.Hit(Projectile, target, Element, 0.48f, 8, 5.2f);
        TriggerFakeKill();
    }
}

// 终结波先减速压缩，再蓄势突进并在末端坍缩。
public sealed class TeacherCompressionWave : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    private int Stage => (int)Projectile.ai[0];
    private int Element => (int)Projectile.ai[2];

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 440;
    }

    public override void SetDefaults()
    {
        Projectile.width = 76;
        Projectile.height = 76;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 5;
        Projectile.timeLeft = 70;
        Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 17;
    }

    public override bool? CanDamage() => Projectile.localAI[0] >= 4f && Projectile.timeLeft > 8 ? null : false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        Projectile.localAI[1] += 0.10f + FranMotion.Clamp01(age / 28f) * 0.16f;
        if (age < 9f)
            Projectile.velocity *= 0.94f;
        else if (age < 29f)
            Projectile.velocity *= 1.035f;
        else if (Projectile.timeLeft < 17)
            Projectile.velocity *= 0.92f;
        else
            Projectile.velocity *= 0.992f;

        float speed = Projectile.velocity.Length();
        if (Stage >= 4 && age > 18f)
        {
            NPC target = FranVfx.ClosestTarget(Projectile.Center, 520f);
            if (target != null)
            {
                float angle = Projectile.velocity.ToRotation().AngleTowards((target.Center - Projectile.Center).ToRotation(), 0.018f);
                Projectile.velocity = angle.ToRotationVector2() * speed;
            }
        }
        Projectile.rotation = Projectile.velocity.ToRotation();

        if (!Main.dedServ && age > 3f && Projectile.timeLeft > 8)
        {
            TeacherWaveVisuals.GetPalette(Stage, Element, out _, out Color body, out Color edge);
            FranParticleEffects.Compression(Projectile.Center, Projectile.velocity,
                Color.Lerp(body, edge, age > 22f ? 0.58f : 0.22f), age, age >= 22f);
        }

        if (!Main.dedServ && age > 4f && Projectile.timeLeft > 12 && Main.rand.NextBool(2))
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 radial = angle.ToRotationVector2();
            Dust dust = Dust.NewDustPerfect(Projectile.Center + radial * Main.rand.NextFloat(30f, 54f),
                Element == 1 ? DustID.Torch : DustID.BlueTorch, -radial * Main.rand.NextFloat(1.2f, 3f),
                70, default, Main.rand.NextFloat(0.88f, 1.24f));
            dust.noGravity = true;
        }
        TeacherWaveVisuals.Light(Projectile.Center, Element, 1.05f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float radius = Stage >= 5 ? 65f : 58f;
        Vector2 axis = Projectile.localAI[1].ToRotationVector2();
        float collisionPoint = 0f;
        if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - axis * radius, Projectile.Center + axis * radius, 23f, ref collisionPoint))
            return true;
        axis = axis.RotatedBy(MathHelper.PiOver2);
        collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - axis * radius, Projectile.Center + axis * radius, 23f, ref collisionPoint);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float age = 70f - Projectile.timeLeft;
        float opacity = FranMotion.Smooth(FranMotion.Segment(age, 0f, 7f)) *
            (1f - FranMotion.Smooth(FranMotion.Segment(age, 53f, 70f)));
        float windup = FranMotion.Smooth(FranMotion.Segment(age, 4f, 22f));
        float release = FranMotion.Smooth(FranMotion.Segment(age, 22f, 34f));
        float radius = MathHelper.Lerp(43f, Stage >= 5 ? 68f : 61f, windup) + release * 7f;
        float spin = Projectile.localAI[1];
        TeacherWaveVisuals.GetPalette(Stage, Element, out Color deep, out Color body, out Color edge);

        Color deepAdditive = deep;
        Color bodyAdditive = body;
        Color edgeAdditive = edge;
        float circularScale = radius * 2f / FranVfx.Shared("Cal_CircularSmearSmokey").Width;
        float trientScale = radius * 1.72f / FranVfx.Shared("Cal_TrientCircularSmear").Width;
        float slashScale = radius * 2.15f / FranVfx.Shared("Cal_SlashSmear").Width;

        FranVfx.BeginAdditive();
        for (int i = 10; i >= 4; i -= 3)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                continue;
            float completion = 1f - i / 14f;
            FranVfx.DrawShared("Cal_CircularSmearSmokey", Projectile.oldPos[i] + Projectile.Size * 0.5f,
                deepAdditive * (opacity * completion * 0.16f), spin - i * 0.14f,
                circularScale * completion);
        }

        FranVfx.DrawShared("Cal_CircularSmearSmokey", Projectile.Center,
            deepAdditive * (0.46f * opacity), -spin * 0.72f,
            new Vector2(circularScale * 1.15f, circularScale * 0.94f));
        FranVfx.DrawShared("Cal_CircularSmearSmokey",
            Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 6f,
            bodyAdditive * (0.42f * opacity), spin * 0.48f,
            new Vector2(circularScale * 0.92f, circularScale * 1.16f));

        for (int i = 0; i < 3; i++)
        {
            float angle = spin + i * MathHelper.TwoPi / 3f + MathF.Sin(age * 0.21f + i) * 0.13f;
            Vector2 radial = angle.ToRotationVector2();
            Vector2 bladeCenter = Projectile.Center + radial * radius * (0.12f + i * 0.07f);
            Color arcColor = Color.Lerp(body, edge, i * 0.24f);
            FranVfx.DrawShared("Cal_TrientCircularSmear", bladeCenter,
                arcColor * ((0.58f - i * 0.07f) * opacity), angle + MathHelper.PiOver2,
                trientScale * (1f - i * 0.08f));
            FranVfx.DrawShared("Cal_SlashSmear", bladeCenter + radial * 8f,
                edgeAdditive * ((0.28f - i * 0.035f) * opacity), angle,
                new Vector2(slashScale * (0.90f - i * 0.08f), slashScale * (0.72f + i * 0.05f)));
        }

        float jawScale = radius * 2.15f / FranVfx.Shared("Cal_VerticalSmearLarge").Width;
        FranVfx.DrawShared("Cal_VerticalSmearLarge", Projectile.Center, bodyAdditive * (0.22f * opacity),
            Projectile.rotation + MathHelper.PiOver2 - spin * 0.11f,
            new Vector2(jawScale * (0.82f + release * 0.24f), jawScale * 0.62f));
        FranVfx.DrawShared("Cal_VerticalSmearLarge", Projectile.Center, edgeAdditive * (0.16f * opacity),
            Projectile.rotation - MathHelper.PiOver2 + spin * 0.08f,
            new Vector2(jawScale * 0.72f, jawScale * (0.54f + release * 0.18f)));

        for (int i = 0; i < 5; i++)
        {
            float sparkAge = (age - 22f - i * 2.4f);
            if (sparkAge < 0f || sparkAge > 16f)
                continue;
            float completion = sparkAge / 16f;
            float angle = Projectile.rotation + i * MathHelper.TwoPi / 5f + spin * 0.18f;
            Vector2 sparkPosition = Projectile.Center + angle.ToRotationVector2() *
                MathHelper.Lerp(radius * 0.18f, radius * 1.15f, FranMotion.Smooth(completion));
            FranVfx.DrawShared("Cal_HalfStar", sparkPosition,
                edgeAdditive * ((1f - completion) * 0.74f * opacity), angle,
                new Vector2(0.44f, 1.18f) * (1f - completion * 0.46f));
        }
        FranVfx.EndAdditive();
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        Projectile.velocity *= 0.78f;
        TeacherWaveVisuals.Hit(Projectile, target, Element, 0.74f, 12, 6.3f);
    }

    public override void OnKill(int timeLeft) => TeacherWaveVisuals.Death(Projectile, Element, 0.72f, 16, 5.8f);
}
