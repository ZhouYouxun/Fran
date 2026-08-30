using System;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

internal enum FranSpectatorPalette
{
    Cyan,
    Purple,
    Gold,
    Orange,
    Mint,
    Lightning
}

// These are real projectiles so their motion has a stable lifetime and trail history, but they
// deliberately have no combat flags and CanDamage always returns false.
internal static class FranSpectatorVfx
{
    public static FranSpectatorPalette ForSkill(FranSkill skill) => skill switch
    {
        FranSkill.SkillTaker => FranSpectatorPalette.Purple,
        FranSkill.TelekineticCatapult => FranSpectatorPalette.Gold,
        FranSkill.MultipleBodyCreation => FranSpectatorPalette.Mint,
        FranSkill.ThunderChain or FranSkill.ThorsHammer => FranSpectatorPalette.Lightning,
        _ => FranSpectatorPalette.Cyan
    };

    public static Color ColorFor(int palette) => (FranSpectatorPalette)Math.Clamp(palette, 0, 5) switch
    {
        FranSpectatorPalette.Purple => new Color(205, 90, 255),
        FranSpectatorPalette.Gold => new Color(255, 205, 92),
        FranSpectatorPalette.Orange => new Color(255, 122, 58),
        FranSpectatorPalette.Mint => new Color(105, 245, 205),
        FranSpectatorPalette.Lightning => new Color(145, 170, 255),
        _ => new Color(92, 220, 255)
    };

    public static void SpawnReleaseFan(IEntitySource source, Vector2 center, Vector2 direction, int owner,
        FranSpectatorPalette palette, int count = 3, float scale = 1f)
    {
        if (!CanSpawn(owner))
            return;

        direction = direction.SafeNormalize(Vector2.UnitX * Main.player[owner].direction);
        count = Math.Clamp(count, 1, 5);
        float middle = (count - 1f) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float offset = i - middle;
            float spread = offset * 0.16f + Main.rand.NextFloat(-0.035f, 0.035f);
            Vector2 velocity = direction.RotatedBy(spread) * Main.rand.NextFloat(6.4f, 9.2f);
            float curveSign = offset == 0f ? (Main.rand.NextBool() ? 1f : -1f) : MathF.Sign(offset);
            float curvature = curveSign * Main.rand.NextFloat(0.026f, 0.052f);
            Projectile.NewProjectile(source, center + direction * Main.rand.NextFloat(-4f, 7f), velocity,
                ModContent.ProjectileType<FranSpectatorRibbonProjectile>(), 0, 0f, owner,
                curvature, (float)palette, scale * Main.rand.NextFloat(0.86f, 1.12f));
        }
    }

    public static void SpawnImpactPetals(IEntitySource source, Vector2 center, Vector2 incoming, int owner,
        FranSpectatorPalette palette, int count = 4, float scale = 1f)
    {
        if (!CanSpawn(owner))
            return;

        incoming = incoming.SafeNormalize(Vector2.UnitX * Main.player[owner].direction);
        count = Math.Clamp(count, 1, 7);
        float baseAngle = incoming.ToRotation() + MathHelper.Pi;
        for (int i = 0; i < count; i++)
        {
            float fan = count == 1 ? 0f : MathHelper.Lerp(-1.02f, 1.02f, i / (count - 1f));
            float angle = baseAngle + fan + Main.rand.NextFloat(-0.12f, 0.12f);
            Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4.4f, 8.6f);
            float curve = (i % 2 == 0 ? 1f : -1f) * Main.rand.NextFloat(0.052f, 0.092f);
            Projectile.NewProjectile(source, center + velocity.SafeNormalize(Vector2.UnitX) * 4f, velocity,
                ModContent.ProjectileType<FranSpectatorPetalProjectile>(), 0, 0f, owner,
                curve, (float)palette, scale * Main.rand.NextFloat(0.82f, 1.18f));
        }
    }

    private static bool CanSpawn(int owner) => !Main.dedServ && Main.player.IndexInRange(owner) &&
        Main.myPlayer == owner && Main.player[owner].active;
}

public sealed class FranSpectatorRibbonProjectile : ModProjectile
{
    private const int Lifetime = 32;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 13;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override bool? CanDamage() => false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        float progress = age / Lifetime;
        float curvature = Projectile.ai[0] * MathF.Pow(1f - MathHelper.Clamp(progress, 0f, 1f), 1.35f);
        Projectile.velocity = Projectile.velocity.RotatedBy(curvature) * 0.974f;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.Opacity = MathHelper.Clamp(MathF.Sin(progress * MathHelper.Pi) * 1.45f, 0f, 1f);
        Color color = FranSpectatorVfx.ColorFor((int)Projectile.ai[1]);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * (0.15f * Projectile.Opacity));
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Color color = FranSpectatorVfx.ColorFor((int)Projectile.ai[1]);
        float scale = MathHelper.Clamp(Projectile.ai[2], 0.45f, 1.65f);
        float opacity = Projectile.Opacity;
        Vector2 center = Projectile.Center;

        FranVfx.DrawOriginalPixels("FranPressureNeedle", center, color * (0.42f * opacity),
            Projectile.rotation, new Vector2(32f, 10f) * scale);
        FranVfx.BeginAdditive();
        for (int i = Projectile.oldPos.Length - 1; i >= 0; i -= 2)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                continue;
            float trailFade = 1f - i / (float)Projectile.oldPos.Length;
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            float rotation = Projectile.oldRot[i];
            FranVfx.DrawOriginalPixels("FranSmearForward", point,
                color * (opacity * trailFade * 0.22f), rotation,
                new Vector2(48f * trailFade, 11f * trailFade) * scale);
        }
        FranVfx.DrawOriginalPixels("FranPressureNeedle", center, color * (0.82f * opacity),
            Projectile.rotation, new Vector2(38f, 12f) * scale);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", center, Color.White * (0.68f * opacity),
            Projectile.rotation, new Vector2(20f, 12f) * scale);
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranSpectatorPetalProjectile : ModProjectile
{
    private const int Lifetime = 38;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 11;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 7;
        Projectile.height = 7;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    public override bool? CanDamage() => false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        float progress = age / Lifetime;
        float reversal = MathHelper.Lerp(1f, -0.36f, MathHelper.Clamp((progress - 0.34f) / 0.66f, 0f, 1f));
        Projectile.velocity = Projectile.velocity.RotatedBy(Projectile.ai[0] * reversal) *
            MathHelper.Lerp(0.955f, 0.992f, progress);
        Projectile.velocity.Y -= 0.018f;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.Opacity = MathHelper.Clamp(MathF.Sin(progress * MathHelper.Pi) * 1.55f, 0f, 1f);
        Color color = FranSpectatorVfx.ColorFor((int)Projectile.ai[1]);
        Lighting.AddLight(Projectile.Center, color.ToVector3() * (0.11f * Projectile.Opacity));
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Color color = FranSpectatorVfx.ColorFor((int)Projectile.ai[1]);
        float scale = MathHelper.Clamp(Projectile.ai[2], 0.45f, 1.7f);
        float opacity = Projectile.Opacity;
        Vector2 center = Projectile.Center;

        FranVfx.DrawOriginalPixels("FranPressureNeedle", center, color * (0.36f * opacity),
            Projectile.rotation, new Vector2(27f, 9f) * scale);
        FranVfx.BeginAdditive();
        for (int i = Projectile.oldPos.Length - 1; i >= 0; i -= 2)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                continue;
            float trailFade = 1f - i / (float)Projectile.oldPos.Length;
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            FranVfx.DrawOriginalPixels("FranPressureNeedle", point,
                color * (opacity * trailFade * 0.28f), Projectile.oldRot[i],
                new Vector2(24f * trailFade, 7f * trailFade) * scale);
        }
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", center,
            Color.Lerp(color, Color.White, 0.72f) * (0.76f * opacity), Projectile.rotation,
            new Vector2(22f, 13f) * scale);
        FranVfx.EndAdditive();
        return false;
    }
}
