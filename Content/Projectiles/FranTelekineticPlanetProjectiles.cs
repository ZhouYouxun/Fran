using System;
using FranMod.Content.Players;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

internal static class FranPlanetVisuals
{
    private const string Root = "Fran/Textures/kenney_planets/Planets/planet";

    public static string TexturePath(int variant) => $"{Root}{Math.Clamp(variant, 0, 9):D2}";

    public static Color Palette(int variant) => Math.Clamp(variant, 0, 9) switch
    {
        0 => new Color(104, 226, 222),
        1 => new Color(150, 125, 245),
        2 => new Color(245, 101, 55),
        3 => new Color(113, 211, 174),
        4 => new Color(105, 139, 183),
        5 => new Color(215, 165, 121),
        6 => new Color(176, 104, 137),
        7 => new Color(83, 204, 238),
        8 => new Color(245, 91, 43),
        _ => new Color(171, 91, 245)
    };
}

/// <summary>
/// Telekinetic Catapult's Hardmode bonus projectile. The owning client chooses the planet variant
/// and launch direction once. It brakes after entering the world, then accelerates until impact.
/// </summary>
public sealed class FranTelekineticPlanet : ModProjectile
{
    private const int ExtraUpdates = 2;
    private const int UpdatesPerTick = ExtraUpdates + 1;
    private const int FadeOutTicks = 30;
    private int Variant => Math.Clamp((int)Projectile.ai[0], 0, 9);
    private float Age => Projectile.localAI[0];
    private float AgeTicks => Age / UpdatesPerTick;
    private ref float FadeOutTimer => ref Projectile.ai[1];
    private bool FadingOut => FadeOutTimer > 0f;
    private float Diameter => 54f + (uint)Projectile.identity % 17u;
    private float Spin => (Projectile.identity % 2 == 0 ? 1f : -1f) *
        (0.035f + (uint)Projectile.identity % 9u * 0.004f);

    public override string Texture => FranPlanetVisuals.TexturePath(0);

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;
    }

    public override void SetDefaults()
    {
        Projectile.width = 38;
        Projectile.height = 38;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 150 * UpdatesPerTick;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.extraUpdates = ExtraUpdates;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10 * UpdatesPerTick;
    }

    public override bool? CanDamage() => !FadingOut && AgeTicks >= 4f ? null : false;

    public override void AI()
    {
        Projectile.localAI[0]++;
        Projectile.rotation += Spin;
        if (FadingOut)
        {
            UpdateFadeOut();
            return;
        }
        Projectile.Opacity = MathHelper.Clamp(AgeTicks / 7f, 0f, 1f);

        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitY);
        float speedPerTick = Projectile.velocity.Length() * UpdatesPerTick;
        if (AgeTicks <= 14f)
        {
            // The summoned mass visibly catches in the telekinetic field before being hurled.
            speedPerTick = MathHelper.Lerp(speedPerTick, 9.6f, 0.16f / UpdatesPerTick);
        }
        else
        {
            float accelerationProgress = MathHelper.Clamp((AgeTicks - 14f) / 40f, 0f, 1f);
            accelerationProgress = accelerationProgress * accelerationProgress * (3f - 2f * accelerationProgress);
            speedPerTick = Math.Min(46.5f, speedPerTick +
                MathHelper.Lerp(0.16f, 0.72f, accelerationProgress) / UpdatesPerTick);
        }
        Projectile.velocity = direction * (speedPerTick / UpdatesPerTick);

        Color color = FranPlanetVisuals.Palette(Variant);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * 0.58f);
        if (Main.dedServ || Projectile.numUpdates != 0)
            return;

        int timer = (int)AgeTicks;
        Vector2 back = -direction;
        Vector2 side = direction.RotatedBy(MathHelper.PiOver2);
        float phase = Projectile.identity * 0.71f + timer * 1.13f;
        if (timer % 2 == 0)
        {
            float sideSign = timer % 4 == 0 ? 1f : -1f;
            Vector2 trailVelocity = back * MathHelper.Lerp(1.8f, 3.4f,
                MathHelper.Clamp(speedPerTick / 46.5f, 0f, 1f)) + side * sideSign * 0.72f;
            FranParticleSystem.Spawn(new FranLightParticle(Projectile.Center, trailVelocity,
                0.30f + (timer % 3) * 0.05f, color, 16 + timer % 5, 0.72f, 1.25f, 2.6f));
        }
        if (timer % 5 == 0)
        {
            FranParticleSystem.Spawn(new FranGlowOrbParticle(
                Projectile.Center + phase.ToRotationVector2() * 7f,
                back * 1.25f + side * MathF.Sin(phase) * 0.45f, color,
                0.20f + (timer % 2) * 0.04f, 0.03f, 14 + timer % 4));
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        SpawnImpactExplosion();
        // OnHitNPC runs before vanilla consumes penetration. Convert the final hit into a harmless
        // fade state so the projectile is not killed abruptly when its second hit is spent.
        if (Projectile.penetrate <= 1)
            BeginFadeOut();
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        SpawnImpactExplosion();
        BeginFadeOut();
        return false;
    }

    private void SpawnImpactExplosion()
    {
        if (Projectile.owner == Main.myPlayer)
        {
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<FranTelekineticPlanetExplosion>(), Projectile.damage,
                Projectile.knockBack, Projectile.owner, Variant, Diameter);
        }
    }

    private void BeginFadeOut()
    {
        if (FadingOut)
            return;

        FadeOutTimer = 1f;
        Projectile.penetrate = -1;
        Projectile.friendly = false;
        Projectile.tileCollide = false;
        Projectile.timeLeft = Math.Max(Projectile.timeLeft, FadeOutTicks * UpdatesPerTick + 2);
        Projectile.netUpdate = true;
    }

    private void UpdateFadeOut()
    {
        FadeOutTimer++;
        float completion = MathHelper.Clamp((FadeOutTimer - 1f) / (FadeOutTicks * UpdatesPerTick), 0f, 1f);
        float fade = 1f - MathHelper.SmoothStep(0f, 1f, completion);
        Projectile.Opacity = fade;

        // Decelerate once per real game tick despite the projectile's extra updates.
        if (Projectile.numUpdates == 0)
            Projectile.velocity *= 0.90f;

        Color color = FranPlanetVisuals.Palette(Variant);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * (0.42f * fade));
        if (!Main.dedServ && Projectile.numUpdates == 0 && (int)(FadeOutTimer / UpdatesPerTick) % 4 == 0)
        {
            FranParticleSystem.Spawn(new FranGlowOrbParticle(Projectile.Center,
                -Projectile.velocity.SafeNormalize(Vector2.UnitY) * 0.7f, color,
                0.16f * fade, 0.02f, 12));
        }

        if (completion >= 1f)
            Projectile.Kill();
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = ModContent.Request<Texture2D>(FranPlanetVisuals.TexturePath(Variant)).Value;
        Color color = FranPlanetVisuals.Palette(Variant);
        float scale = Diameter / Math.Max(texture.Width, texture.Height);

        FranVfx.BeginAdditive();
        FranVfx.DrawBloom(Projectile.Center, color, Diameter * 0.72f,
            0.28f * Projectile.Opacity);
        FranVfx.DrawRing(Projectile.Center, Color.Lerp(color, Color.White, 0.25f), Diameter * 0.56f,
            0.16f * Projectile.Opacity, -Projectile.rotation, "BloomRing");
        FranVfx.EndAdditive();

        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.White * Projectile.Opacity, Projectile.rotation, texture.Size() * 0.5f,
            scale, SpriteEffects.None, 0f);
        return false;
    }
}

/// <summary>Short-lived synchronized area hit and coloured impact for a telekinetic planet.</summary>
public sealed class FranTelekineticPlanetExplosion : ModProjectile
{
    private const int Lifetime = 5;
    private int Variant => Math.Clamp((int)Projectile.ai[0], 0, 9);
    private float Diameter => MathHelper.Clamp(Projectile.ai[1], 48f, 82f);
    private float Progress => 1f - Projectile.timeLeft / (float)Lifetime;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 116;
        Projectile.height = 116;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
        Projectile.netImportant = true;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.timeLeft >= 3 ? null : false;

    public override void OnSpawn(IEntitySource source)
    {
        if (Main.dedServ)
            return;

        Color color = FranPlanetVisuals.Palette(Variant);
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.42f, Pitch = 0.24f }, Projectile.Center);
        float phase = Projectile.identity * 0.618f + Variant * 0.37f;
        const int moteCount = 12;
        for (int i = 0; i < moteCount; i++)
        {
            float angle = phase + MathHelper.TwoPi * i / moteCount;
            Vector2 velocity = angle.ToRotationVector2() * (3.6f + i % 3 * 1.15f);
            Color moteColor = Color.Lerp(color, Color.White, 0.12f + i % 4 * 0.10f);
            if (i % 2 == 0)
                FranParticleSystem.Spawn(new FranSmallBloomParticle(Projectile.Center, velocity,
                    moteColor, 13f + i % 3 * 2f, 2.5f, 14 + i % 4));
            else
                FranParticleSystem.Spawn(new FranSparkParticle(Projectile.Center, velocity, moteColor,
                    0.18f + i % 3 * 0.025f, new Vector2(0.36f, 0.82f), 15 + i % 4, 0.90f));
        }

        for (int i = 0; i < 3; i++)
        {
            float angle = phase + MathHelper.TwoPi * i / 3f + MathHelper.Pi / 6f;
            FranParticleSystem.Spawn(new FranSkillGlyphParticle(
                Projectile.Center + angle.ToRotationVector2() * 5f,
                angle.ToRotationVector2() * 2.4f, color, 0.46f, 13 + i));
        }

        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 2.2f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Color color = FranPlanetVisuals.Palette(Variant);
        float fade = MathF.Pow(1f - Progress, 1.4f);
        float radius = MathHelper.Lerp(Diameter * 0.46f, Diameter * 0.96f, Progress);
        FranVfx.BeginAdditive();
        FranVfx.DrawBloom(Projectile.Center, color, radius, 0.46f * fade);
        FranVfx.DrawRing(Projectile.Center, Color.Lerp(color, Color.White, 0.42f), radius,
            0.58f * fade, Progress * 0.7f, "BloomRing");
        FranVfx.EndAdditive();
        return false;
    }
}
