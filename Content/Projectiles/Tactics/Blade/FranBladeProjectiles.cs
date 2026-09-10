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


public sealed class FloatingSwordArrayProjectile : ModProjectile
{
    private const int CycleLength = 48;
    private Vector2 formationAim;
    private bool initialized;

    public override string Texture => FranTeacherVisuals.TexturePath;
    public override void SetDefaults()
    {
        Projectile.width = 42; Projectile.height = 42; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 240;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 16;
        Projectile.netImportant = true;
    }

    public override bool ShouldUpdatePosition() => false;

    private int CurrentCycle => Projectile.ai[0] < 0f
        ? 0
        : ((int)Projectile.ai[0] - 1 + CycleLength) % CycleLength;

    public override bool? CanDamage()
    {
        int cycle = CurrentCycle;
        return cycle is >= 16 and <= 31 ? null : false;
    }

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
            formationAim = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
            Projectile.velocity = formationAim;
            Projectile.rotation = formationAim.ToRotation() + MathHelper.PiOver4;
        }

        int cycle = (int)Projectile.ai[0] % CycleLength;
        float slot = Projectile.ai[2];
        Vector2 formationForward = Vector2.UnitX * owner.direction;
        Vector2 formationPosition = owner.MountedCenter - formationForward * (48f + Math.Abs(slot) * 11f) +
            Vector2.UnitY * (-48f + slot * 24f);

        if (Projectile.ai[0] < 0f)
        {
            Projectile.Center = Vector2.Lerp(Projectile.Center, formationPosition, 0.30f);
            Projectile.rotation = formationForward.ToRotation() + slot * 0.075f * owner.direction + MathHelper.PiOver4;
            Projectile.ai[0]++;
            Lighting.AddLight(Projectile.Center, 0.12f, 0.18f, 0.25f);
            return;
        }

        if (cycle == 0 && Main.myPlayer == Projectile.owner)
        {
            NPC npc = FranVfx.ClosestTarget(Projectile.Center, 920f);
            Projectile.ai[1] = npc?.whoAmI ?? -1;
            Projectile.netUpdate = true;
        }

        if (cycle < 14)
        {
            float gather = FranMotion.EaseOutCubic(cycle / 14f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, formationPosition, 0.18f + gather * 0.16f);
            float fanAngle = formationForward.ToRotation() + slot * 0.075f * owner.direction;
            Projectile.rotation = fanAngle + MathHelper.PiOver4;
        }
        else if (cycle == 14)
        {
            Vector2 targetPosition = owner.Center + formationAim * 720f;
            int targetIndex = (int)Projectile.ai[1];
            if (Main.npc.IndexInRange(targetIndex) && Main.npc[targetIndex].CanBeChasedBy(Projectile))
                targetPosition = Main.npc[targetIndex].Center;
            Projectile.velocity = (targetPosition - Projectile.Center).SafeNormalize(formationAim)
                .RotatedBy(slot * 0.055f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            if (Main.myPlayer == Projectile.owner)
                Projectile.netUpdate = true;
        }
        else if (cycle < 32)
        {
            float dashProgress = FranMotion.Segment(cycle, 14f, 32f);
            float speedEnvelope = MathF.Pow(MathF.Sin(dashProgress * MathHelper.Pi), 0.58f);
            float speed = 8f + speedEnvelope * 34f;
            Projectile.Center += Projectile.velocity.SafeNormalize(formationAim) * speed;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            if (cycle == 16 && Main.myPlayer == Projectile.owner)
                FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.owner,
                    FranBurstStyle.SwordArray, 0.44f);
        }
        else
        {
            float recall = FranMotion.EaseOutCubic(FranMotion.Segment(cycle, 32f, CycleLength - 1f));
            Projectile.Center = Vector2.Lerp(Projectile.Center, formationPosition, 0.12f + recall * 0.25f);
            Vector2 returnDirection = (formationPosition - Projectile.Center).SafeNormalize(formationAim);
            Projectile.rotation = returnDirection.ToRotation() + MathHelper.PiOver4;
        }

        if (cycle == CycleLength - 1)
        {
            Projectile.Center = Vector2.Lerp(Projectile.Center, formationPosition, 0.55f);
            FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.owner,
                FranBurstStyle.SwordArray, 0.28f);
        }

        Projectile.ai[0]++;
        if (!Main.dedServ && cycle is >= 16 and < 32)
            FranParticleEffects.StarlightWake(Projectile.Center,
                (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2() * 12f,
                new Color(155, 235, 255));
        if (cycle is >= 16 and < 32 && Main.rand.NextBool(3))
        {
            Vector2 direction = (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2();
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, -direction * 2.2f,
                70, default, 0.95f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.2f, 0.27f, 0.35f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = FranTeacherVisuals.Texture;
        int cycle = CurrentCycle;
        float dashIntensity = FranMotion.Pulse(cycle, 13f, 18f, 30f, 38f);
        Vector2 direction = (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2();
        Color resonanceBlue = Color.Lerp(new Color(55, 150, 255), new Color(205, 245, 255),
            0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.identity));
        FranVfx.BeginAdditive();

        FranVfx.DrawShared("FranVfx_ForwardSmear", Projectile.Center - direction * 28f,
            resonanceBlue * (0.16f + dashIntensity * 0.48f), Projectile.rotation - MathHelper.PiOver4,
            new Vector2(0.13f + dashIntensity * 0.09f, 0.06f + dashIntensity * 0.035f));
        FranVfx.DrawVertical("FranVfx_ThinEndedLine", Projectile.Center - direction * 96f,
            Projectile.Center + direction * 14f, resonanceBlue * (0.22f + dashIntensity * 0.52f),
            9f + dashIntensity * 12f);
        FranVfx.DrawVertical("FranVfx_ThinEndedLine", Projectile.Center - direction * 61f,
            Projectile.Center + direction * 12f, Color.White * (0.14f + dashIntensity * 0.68f),
            3f + dashIntensity * 4f);
        FranVfx.DrawShared("FranVfx_ShineFlare", Projectile.Center + direction * 8f,
            Color.White * (0.24f + dashIntensity * 0.54f), Projectile.rotation,
            new Vector2(0.045f, 0.018f));
        FranVfx.DrawBloom(Projectile.Center, resonanceBlue, 15f + dashIntensity * 9f,
            0.16f + dashIntensity * 0.32f);
        for (int i = 4; i >= 1; i--)
            Main.EntitySpriteDraw(texture, Projectile.Center - direction * i * 9f - Main.screenPosition, null,
                resonanceBlue * (dashIntensity * 0.11f * (5 - i)), Projectile.rotation,
                texture.Size() * 0.5f, 0.72f * FranTeacherVisuals.TextureScale, SpriteEffects.None, 0f);
        FranVfx.EndAdditive();
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation,
            texture.Size() * 0.5f, 0.72f * FranTeacherVisuals.TextureScale, SpriteEffects.None, 0f);
        return false;
    }
}

public sealed class FloatingSwordLaser : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 10; Projectile.height = 10; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = 2; Projectile.timeLeft = 50;
        Projectile.tileCollide = false; Projectile.extraUpdates = 2;
    }
    public override bool? CanDamage() => Projectile.localAI[0] >= 2f && Projectile.timeLeft >= 6 ? null : false;
    public override void AI()
    {
        if (Projectile.localAI[0]++ < 12f && Projectile.velocity.Length() < 23f)
            Projectile.velocity *= 1.025f;
        if (Projectile.timeLeft < 12)
            Projectile.velocity *= 0.94f;
        if (!Main.dedServ && ((int)Projectile.localAI[0] % 3 == 0))
            FranParticleEffects.StarlightWake(Projectile.Center, Projectile.velocity, new Color(155, 255, 115));
        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, Vector2.Zero, 70, default, 1f);
        d.noGravity = true;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float age = 50f - Projectile.timeLeft;
        float opacity = FranMotion.Smooth(FranMotion.Segment(age, 0f, 4f)) *
            FranMotion.Smooth(FranMotion.Segment(Projectile.timeLeft, 0f, 12f));
        Color malachite = new(155, 255, 115);
        FranVfx.BeginAdditive();
        FranVfx.DrawCappedLaser(Projectile.Center - direction * 56f, Projectile.Center + direction * 10f,
            new Color(55, 180, 70), malachite, 4.4f, opacity);
        FranVfx.DrawShared("FranVfx_PulseStar", Projectile.Center, Color.White * (0.84f * opacity),
            Projectile.rotation, 0.065f);
        FranVfx.DrawBloom(Projectile.Center, malachite, 11f, 0.42f * opacity);
        FranVfx.EndAdditive();
        return false;
    }
    public override void OnKill(int timeLeft) =>
        FranBurst.SpawnForOwner(Projectile.GetSource_Death(), Projectile.Center, Projectile.owner,
            FranBurstStyle.SwordArray, 0.34f);
}

public sealed class AirDrawSlashProjectile : ModProjectile
{
    private bool released;
    private float currentBladeAngle;
    private int facing;
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 160; Projectile.height = 160; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 45;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool? CanDamage() => Projectile.timeLeft is <= 27 and >= 20 ? null : false;
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
        facing = Math.Sign(direction.X);
        if (facing == 0)
            facing = owner.direction;
        owner.ChangeDir(facing);
        float age = 45f - Projectile.timeLeft;
        float aimRotation = direction.ToRotation();
        float poseOffset = !released
            ? MathHelper.Lerp(0.54f * facing, -0.12f * facing,
                FranMotion.EaseInCubic(FranMotion.Segment(age, 0f, 18f)))
            : MathHelper.Lerp(-0.12f * facing, 0.18f * facing,
                FranMotion.Smooth(FranMotion.Segment(age, 18f, 34f)));
        currentBladeAngle = aimRotation + poseOffset;
        Projectile.Center = owner.MountedCenter + direction * 70f;
        Projectile.rotation = currentBladeAngle + MathHelper.PiOver4 +
            (facing < 0 ? MathHelper.PiOver2 : 0f);
        owner.heldProj = Projectile.whoAmI;
        owner.itemAnimation = owner.itemTime = 2;
        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            currentBladeAngle - MathHelper.PiOver2);

        if (!released && Projectile.timeLeft <= 27)
        {
            released = true;
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.34f, Volume = 0.92f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.48f, Volume = 0.38f }, Projectile.Center);
            owner.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 3.6f);
            FranVfx.DustLine(owner.Center, owner.Center + direction * 190f, DustID.Cloud, 14f, 1.05f);
            FranParticleEffects.LineRelease(owner.Center + direction * 18f,
                owner.Center + direction * 235f, new Color(205, 245, 255), 20, 1.12f);
            FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), owner.Center + direction * 74f,
                Projectile.owner, FranBurstStyle.AirDraw, 1.15f);
        }
        if (!released)
            Lighting.AddLight(owner.Center, 0.18f, 0.32f, 0.38f);
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner,
            FranBurstStyle.AirDraw, 0.82f);
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (!released)
            return false;
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - Projectile.velocity * 85f, Projectile.Center + Projectile.velocity * 120f,
            28f, ref collisionPoint);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Player owner = Main.player[Projectile.owner];
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float age = 45f - Projectile.timeLeft;
        FranVfx.BeginAdditive();
        if (released)
        {
            float opacity = FranMotion.Pulse(age, 17f, 19f, 24f, 34f);
            Vector2 start = owner.MountedCenter - direction * 78f;
            Vector2 end = owner.MountedCenter + direction * 248f;

            // One-frame-draw precision: a broad curved air shell, a compressed inner blade and split wakes.
            FranVfx.DrawShared("FranVfx_SemiCircularSmearSwipe", owner.MountedCenter + direction * 42f,
                new Color(105, 205, 245) * (0.48f * opacity), currentBladeAngle + MathHelper.PiOver2,
                new Vector2(1.36f, 0.72f));
            FranVfx.DrawShared("FranVfx_SemiCircularSmearSwipe", owner.MountedCenter + direction * 58f,
                Color.White * (0.32f * opacity), currentBladeAngle - MathHelper.PiOver2,
                new Vector2(1.02f, 0.54f));
            FranVfx.DrawVertical("FranVfx_GlowBlade", start, end,
                new Color(115, 225, 255) * (0.46f * opacity), 54f);
            FranVfx.DrawVertical("FranVfx_ThinEndedLine", start + direction * 28f, end,
                Color.White * (0.82f * opacity), 9f);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector2 normal = direction.RotatedBy(MathHelper.PiOver2) * (24f + age * 1.2f) * i;
                FranVfx.DrawStretched("FranVfx_FadeStreak", owner.MountedCenter + normal,
                    owner.MountedCenter + direction * 205f + normal * 0.28f,
                    new Color(120, 205, 235) * (0.24f * opacity), 13f);
            }
            FranVfx.DrawShared("FranVfx_ShineFlare", owner.MountedCenter + direction * 104f,
                Color.White * (0.68f * opacity), currentBladeAngle,
                new Vector2(0.14f, 0.035f));
        }
        else
        {
            float charge = FranMotion.EaseInCubic(FranMotion.Segment(age, 0f, 18f));
            float radius = MathHelper.Lerp(62f, 19f, charge);
            Color chargeColor = Color.Lerp(new Color(90, 175, 215), Color.White, charge * 0.72f);
            FranVfx.DrawRing(owner.MountedCenter, chargeColor, radius, 0.14f + charge * 0.34f,
                Main.GlobalTimeWrappedHourly * (0.8f + charge * 2.4f), "FranVfx_BloomRing");
            FranVfx.DrawShared("FranVfx_CircularSmear", owner.MountedCenter,
                chargeColor * (0.10f + charge * 0.30f),
                Main.GlobalTimeWrappedHourly * (1.1f + charge * 2f), 0.30f + charge * 0.10f);
            for (int i = 0; i < 3; i++)
            {
                float angle = Main.GlobalTimeWrappedHourly * (0.8f + charge * 1.9f) + i * MathHelper.TwoPi / 3f;
                Vector2 point = owner.MountedCenter + angle.ToRotationVector2() * radius;
                FranVfx.DrawTexturedSpark(point, (owner.MountedCenter - point).SafeNormalize(Vector2.UnitY),
                    chargeColor, 24f + charge * 22f, 2.5f + charge * 2f, 0.25f + charge * 0.45f);
            }
        }
        FranVfx.EndAdditive();

        Texture2D teacherTexture = FranTeacherVisuals.Texture;
        SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 origin = new(facing < 0 ? teacherTexture.Width : 0f, teacherTexture.Height);
        Vector2 bladeCenter = owner.MountedCenter + currentBladeAngle.ToRotationVector2() * 8f - Main.screenPosition;
        Color bladeColor = Color.Lerp(lightColor, Color.White, released ? 0.28f : 0.12f);
        Main.EntitySpriteDraw(teacherTexture, bladeCenter, null, bladeColor, Projectile.rotation,
            origin, 1.05f * FranTeacherVisuals.TextureScale, effects, 0f);
        return false;
    }
}

public sealed class LightningSpearProjectile : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 8; Projectile.height = 8; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 18;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.timeLeft is <= 16 and >= 9 ? null : false;
    public override bool? CanHitNPC(NPC target) =>
        Projectile.ai[1] >= 0f ? target.whoAmI == (int)Projectile.ai[1] : null;
    public override void AI()
    {
        Projectile.localAI[0]++;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Vector2 end = Projectile.Center + Projectile.velocity;
        if (!Main.dedServ)
        {
            FranParticleEffects.InwardSpark(end, new Color(175, 135, 255), 18f, 62f, 5.8f, 0.82f);
            if (Projectile.localAI[0] == 1f)
            {
                FranLightingBoltsSystem.SpawnRuneActivation(end, new Color(155, 125, 255), 0.72f);
                FranLightingBoltsSystem.SpawnLightningFragments(end, new Color(125, 220, 255), 8, 0.62f);
            }
            if (((int)Projectile.localAI[0] & 1) == 0)
                FranParticleEffects.StarlightWake(end, Projectile.velocity.SafeNormalize(Vector2.UnitX) * 7f,
                    new Color(135, 225, 255));
        }
        if (Main.rand.NextBool(2))
        {
            Dust d = Dust.NewDustPerfect(Vector2.Lerp(Projectile.Center, end, Main.rand.NextFloat()),
                DustID.Electric, Main.rand.NextVector2Circular(1f, 1f),
                40, default, 1.05f);
            d.noGravity = true;
        }
        Lighting.AddLight(Vector2.Lerp(Projectile.Center, end, 0.5f), 0.28f, 0.22f, 0.62f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center, Projectile.Center + Projectile.velocity, 12f, ref collisionPoint);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = MathF.Sin((18f - Projectile.timeLeft) / 18f * MathHelper.Pi);
        Color arcColor = Color.Lerp(new Color(120, 70, 225), new Color(135, 235, 255),
            0.35f + 0.15f * Projectile.ai[0]);
        float seed = Projectile.identity * 0.71f + Main.GlobalTimeWrappedHourly * 22f;
        Vector2 start = Projectile.Center;
        Vector2 end = Projectile.Center + Projectile.velocity;
        FranVfx.BeginAdditive();
        FranVfx.DrawTexturedLightning(start, end, arcColor, 5.5f, 10, seed, opacity);
        FranVfx.DrawTexturedLightning(start, end, Color.White, 2.1f, 8, seed + 4.7f, opacity * 0.76f);
        FranVfx.DrawShared("LightningImpact", end, arcColor * (0.50f * opacity),
            Projectile.rotation, 0.24f);
        FranVfx.DrawBloom(end, arcColor, 18f, 0.48f * opacity);
        FranVfx.EndAdditive();
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranVfx.SparkBurst(target.Center, new Color(185, 145, 255), 7, 5f, 0.95f);
        FranLightingBoltsSystem.SpawnThunderImpact(target.Center, new Color(155, 135, 255), 0.62f);
        Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(target.Center, 2f);
    }
    public override void OnKill(int timeLeft)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_Death(), Projectile.Center + Projectile.velocity,
            Projectile.owner, FranBurstStyle.Lightning, 0.38f);
    }
}

public sealed class ShipCutterProjectile : ModProjectile
{
    private float currentAngle;
    private float progress;
    private float currentReach;
    private float slashIntensity;
    private bool released;
    private int facing;
    public override string Texture => FranTeacherVisuals.TexturePath;
    public override void SetDefaults()
    {
        Projectile.width = 210; Projectile.height = 210; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 70;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool? CanDamage() => progress is >= 0.39f and <= 0.66f ? null : false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }
        if (facing == 0)
        {
            facing = Math.Sign(Projectile.velocity.X);
            if (facing == 0)
                facing = owner.direction;
        }
        owner.ChangeDir(facing);

        progress = 1f - Projectile.timeLeft / 70f;
        float angleOffset;
        if (progress < 0.35f)
            angleOffset = MathHelper.Lerp(-1.64f, -2.18f,
                FranMotion.EaseInCubic(FranMotion.Segment(progress, 0f, 0.35f)));
        else if (progress < 0.55f)
            angleOffset = MathHelper.Lerp(-2.18f, 1.72f,
                FranMotion.EaseOutQuart(FranMotion.Segment(progress, 0.35f, 0.55f)));
        else if (progress < 0.70f)
            angleOffset = MathHelper.Lerp(1.72f, 1.80f,
                FranMotion.Smooth(FranMotion.Segment(progress, 0.55f, 0.70f)));
        else
            angleOffset = MathHelper.Lerp(1.80f, 1.53f,
                FranMotion.Smooth(FranMotion.Segment(progress, 0.70f, 1f)));

        currentAngle = Projectile.velocity.ToRotation() + angleOffset * facing;
        slashIntensity = FranMotion.Pulse(progress, 0.34f, 0.40f, 0.66f, 0.84f);
        currentReach = 108f + slashIntensity * 42f;
        Projectile.Center = owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach * 0.62f;
        Projectile.rotation = currentAngle + MathHelper.PiOver4 + (facing < 0 ? MathHelper.PiOver2 : 0f);
        owner.itemAnimation = owner.itemTime = 2;
        owner.itemRotation = currentAngle + (facing < 0 ? MathHelper.Pi : 0f);
        owner.heldProj = Projectile.whoAmI;
        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentAngle - MathHelper.PiOver2);
        if (!released && progress >= 0.37f)
        {
            released = true;
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.22f, Volume = 0.95f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.48f, Volume = 0.62f }, Projectile.Center);
            owner.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 7f);
            FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), owner.MountedCenter + currentAngle.ToRotationVector2() * 215f,
                Projectile.owner, FranBurstStyle.ShipCutter, 1.65f);
            Vector2 releaseDirection = currentAngle.ToRotationVector2();
            FranParticleEffects.WaterBurst(owner.MountedCenter + releaseDirection * 205f,
                releaseDirection, new Color(115, 225, 255), 26, 1.25f);
            FranLightingBoltsSystem.SpawnSpectralWhispers(owner.MountedCenter + releaseDirection * 185f,
                new Color(135, 235, 255), 68f, 14);
        }
        if (!Main.dedServ && slashIntensity > 0.12f)
        {
            Vector2 bladeDirection = currentAngle.ToRotationVector2();
            Vector2 tangent = bladeDirection.RotatedBy(MathHelper.PiOver2 * facing);
            FranParticleEffects.MeleeTrail(owner.MountedCenter + bladeDirection * (250f + slashIntensity * 120f),
                bladeDirection, tangent, new Color(255, 210, 145), slashIntensity, 2, false);
        }
        Lighting.AddLight(Projectile.Center, 0.65f, 0.5f, 0.22f);
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Player owner = Main.player[Projectile.owner];
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), owner.MountedCenter,
            owner.MountedCenter + currentAngle.ToRotationVector2() * (350f + slashIntensity * 70f),
            42f + slashIntensity * 20f, ref collisionPoint);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = FranTeacherVisuals.Texture;
        Player owner = Main.player[Projectile.owner];
        float releaseOpacity = slashIntensity;
        Vector2 direction = currentAngle.ToRotationVector2();
        FranVfx.BeginAdditive();
        if (!released)
        {
            float charge = FranMotion.EaseInCubic(FranMotion.Segment(progress, 0f, 0.36f));
            Color chargeColor = Color.Lerp(new Color(205, 150, 75), Color.White, charge * 0.64f);
            FranVfx.DrawRing(owner.MountedCenter, chargeColor, MathHelper.Lerp(102f, 38f, charge),
                0.12f + charge * 0.34f, -Main.GlobalTimeWrappedHourly * (0.45f + charge * 1.7f), "FranVfx_BloomRing");
            FranVfx.DrawShared("FranVfx_CircularSmear", owner.MountedCenter,
                new Color(255, 170, 70) * (0.11f + charge * 0.24f),
                Main.GlobalTimeWrappedHourly * (0.6f + charge * 1.4f), 0.52f - charge * 0.18f);
            for (int i = 0; i < 4; i++)
            {
                float angle = Main.GlobalTimeWrappedHourly * 0.72f + i * MathHelper.PiOver2;
                Vector2 point = owner.MountedCenter + angle.ToRotationVector2() * MathHelper.Lerp(94f, 31f, charge);
                FranVfx.DrawTexturedSpark(point, (owner.MountedCenter - point).SafeNormalize(Vector2.UnitY),
                    chargeColor, 22f + charge * 34f, 2.8f + charge * 2.4f, 0.22f + charge * 0.46f);
            }
            FranVfx.DrawBloom(owner.MountedCenter, chargeColor, 28f + charge * 18f, 0.08f + charge * 0.18f);
        }
        if (releaseOpacity > 0f)
        {
            Vector2 center = owner.MountedCenter + direction * 185f;
            Color outer = Color.Lerp(new Color(255, 135, 55), new Color(255, 235, 175), slashIntensity);
            // 三层错位弧形外壳包围紧凑亮刃，形成重型近战层次。
            for (int i = 0; i < 3; i++)
            {
                float echo = 1f - i * 0.24f;
                float angle = currentAngle + MathHelper.PiOver2 - facing * i * 0.075f;
                FranVfx.DrawShared("FranVfx_SemiCircularSmearSwipe", owner.MountedCenter + direction * (45f + i * 18f),
                    outer * (0.40f * echo * releaseOpacity), angle,
                    new Vector2(2.05f - i * 0.18f, 1.12f - i * 0.12f));
            }
            FranVfx.DrawShared("FranVfx_ForwardSmear", center, outer * (0.52f * releaseOpacity),
                currentAngle - MathHelper.PiOver2, new Vector2(0.54f, 0.16f));
            FranVfx.DrawSharedPixels("FranLegacyBrinySlash", center + direction * 26f,
                new Color(105, 235, 255) * (0.46f * releaseOpacity),
                currentAngle + MathHelper.PiOver2, new Vector2(460f, 28f));
            FranVfx.DrawVertical("FranVfx_GlowBlade", owner.MountedCenter - direction * 42f,
                owner.MountedCenter + direction * 435f, outer * (0.56f * releaseOpacity), 86f);
            FranVfx.DrawVertical("FranVfx_ThinEndedLine", owner.MountedCenter + direction * 18f,
                owner.MountedCenter + direction * 410f, Color.White * (0.82f * releaseOpacity), 13f);
            FranVfx.DrawShared("FranVfx_ShineFlare", center + direction * 62f,
                Color.White * (0.72f * releaseOpacity), currentAngle,
                new Vector2(0.24f, 0.045f));
            FranVfx.DrawBloom(center, new Color(255, 205, 135), 82f, 0.28f * releaseOpacity);
        }
        FranVfx.EndAdditive();
        SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 origin = new(facing < 0 ? texture.Width : 0f, texture.Height);
        Main.EntitySpriteDraw(texture, owner.MountedCenter + direction * 10f - Main.screenPosition, null,
            new Color(255, 225, 175), Projectile.rotation, origin,
            3.25f * FranTeacherVisuals.TextureScale, effects, 0f);
        return false;
    }
}
