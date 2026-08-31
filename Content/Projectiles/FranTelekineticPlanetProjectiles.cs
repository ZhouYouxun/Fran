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
    private int Variant => Math.Clamp((int)Projectile.ai[0], 0, 9);
    private float Age => Projectile.localAI[0];
    private bool Impacted => Projectile.localAI[1] > 0f;
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
        Projectile.penetrate = 1;
        Projectile.timeLeft = 150;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
    }

    public override bool? CanDamage() => Age >= 4f ? null : false;

    public override void AI()
    {
        Projectile.localAI[0]++;
        Projectile.rotation += Spin;
        Projectile.Opacity = MathHelper.Clamp(Age / 7f, 0f, 1f);

        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitY);
        float speed = Projectile.velocity.Length();
        if (Age <= 14f)
        {
            // The summoned mass visibly catches in the telekinetic field before being hurled.
            speed = MathHelper.Lerp(speed, 6.4f, 0.16f);
        }
        else
        {
            float accelerationProgress = MathHelper.Clamp((Age - 14f) / 40f, 0f, 1f);
            accelerationProgress = accelerationProgress * accelerationProgress * (3f - 2f * accelerationProgress);
            speed = Math.Min(31f, speed + MathHelper.Lerp(0.16f, 0.72f, accelerationProgress));
        }
        Projectile.velocity = direction * speed;

        Color color = FranPlanetVisuals.Palette(Variant);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * 0.58f);
        if (Main.dedServ)
            return;

        int timer = (int)Age;
        if (timer % 3 == 0)
        {
            Vector2 trailVelocity = -Projectile.velocity * Main.rand.NextFloat(0.08f, 0.16f) +
                Main.rand.NextVector2Circular(0.8f, 0.8f);
            FranParticleSystem.Spawn(new FranLightParticle(Projectile.Center, trailVelocity,
                Main.rand.NextFloat(0.30f, 0.50f), color, Main.rand.Next(15, 23), 0.72f, 1.25f, 2.6f));
        }
        if (timer % 6 == 0)
        {
            FranParticleSystem.Spawn(new FranGlowOrbParticle(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                -Projectile.velocity * 0.045f, color, Main.rand.NextFloat(0.18f, 0.28f), 0.03f,
                Main.rand.Next(12, 18)));
        }
        if (timer % 4 == 0)
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.TintableDustLighted, -Projectile.velocity * 0.035f, 80, color, 0.82f);
            dust.noGravity = true;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Impact();

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Impact();
        return false;
    }

    private void Impact()
    {
        if (Impacted)
            return;
        Projectile.localAI[1] = 1f;

        if (Projectile.owner == Main.myPlayer)
        {
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<FranTelekineticPlanetExplosion>(), Projectile.damage,
                Projectile.knockBack, Projectile.owner, Variant, Diameter);
        }
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
        FranParticleEffects.BloomPulse(Projectile.Center, color, Diameter * 0.32f, Diameter * 2.2f,
            18, 0.78f, true);
        FranParticleEffects.RingPulse(Projectile.Center, Color.Lerp(color, Color.White, 0.34f),
            Vector2.One * Diameter * 0.34f, Vector2.One * Diameter * 1.9f, 18,
            Main.rand.NextFloat(MathHelper.TwoPi), Main.rand.NextFloat(-0.04f, 0.04f), 0.86f);
        FranParticleEffects.SpawnImpact(Projectile.Center, Main.rand.NextVector2Unit(), color,
            10, 8.6f, 0.92f);
        FranParticleEffects.MysticGlyphs(Projectile.Center, color, 2, Diameter * 0.72f, 0.58f);

        for (int i = 0; i < 12; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4.8f, 10.5f);
            FranParticleSystem.Spawn(new FranLightParticle(Projectile.Center, velocity,
                Main.rand.NextFloat(0.42f, 0.82f), Color.Lerp(color, Color.White, Main.rand.NextFloat(0.12f, 0.52f)),
                Main.rand.Next(24, 38), 0.94f, 1.5f, 3.4f));
        }
        for (int i = 0; i < 7; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.2f, 6.8f);
            FranParticleSystem.Spawn(new FranGlowOrbParticle(Projectile.Center, velocity, color,
                Main.rand.NextFloat(0.24f, 0.46f), 0.04f, Main.rand.Next(16, 25)));
        }
        for (int i = 0; i < 5; i++)
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.TintableDustLighted,
                Main.rand.NextVector2Unit() * Main.rand.NextFloat(3.5f, 8f), 70, color,
                Main.rand.NextFloat(0.9f, 1.3f));
            dust.noGravity = true;
        }

        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 2.2f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Color color = FranPlanetVisuals.Palette(Variant);
        float fade = MathF.Pow(1f - Progress, 1.4f);
        float radius = MathHelper.Lerp(Diameter * 0.55f, Diameter * 1.45f, Progress);
        FranVfx.BeginAdditive();
        FranVfx.DrawBloom(Projectile.Center, color, radius, 0.62f * fade, true);
        FranVfx.DrawRing(Projectile.Center, Color.Lerp(color, Color.White, 0.42f), radius,
            0.76f * fade, Progress * 0.7f, "BloomRing");
        FranVfx.EndAdditive();
        return false;
    }
}
