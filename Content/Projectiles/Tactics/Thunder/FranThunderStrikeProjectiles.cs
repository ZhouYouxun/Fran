using System;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


public sealed class FranThunderStrikeProjectile : ModProjectile
{
    private const float MainStrikeMarker = 1f;
    private const float FinisherMarker = 2f;
    private Vector2 Destination => new(Projectile.ai[0], Projectile.ai[1]);
    private bool MainStrike => Projectile.ai[2] >= MainStrikeMarker;
    private bool Finisher => Projectile.ai[2] >= FinisherMarker;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 28;
        Projectile.height = 28;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.extraUpdates = 4;
        Projectile.timeLeft = Projectile.MaxUpdates * 11;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = Projectile.MaxUpdates * 60;
    }

    public override void AI()
    {
        Vector2 destination = Destination;
        if (!float.IsFinite(destination.X) || !float.IsFinite(destination.Y))
        {
            Projectile.Kill();
            return;
        }

        float speedPerUpdate = Finisher ? 40f : MainStrike ? 38f : 34f;
        Vector2 remaining = destination - Projectile.Center;
        if (Projectile.localAI[0] == 0f && remaining.LengthSquared() > speedPerUpdate * speedPerUpdate)
        {
            Projectile.velocity = remaining.SafeNormalize(Vector2.UnitY) * speedPerUpdate;
            return;
        }

        // Hold the invisible hitbox at the exact strike point for one rendered frame. This keeps the
        // high-update projectile accurate without allowing it to fly beyond the target and leave a tail.
        Projectile.Center = destination;
        Projectile.velocity = Vector2.Zero;
        if (Projectile.localAI[0] == 0f)
        {
            Projectile.localAI[0] = 1f;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, Projectile.MaxUpdates + 1);
            Projectile.netUpdate = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) => false;
}

public sealed class FranThunderDropVisual : ModProjectile
{
    private const int MaximumLifetime = 16;
    private const float MainStrikeMarker = 1000f;
    private const float FinisherMarker = 2000f;
    private float Age => Projectile.localAI[0];
    private bool MainStrike => Projectile.ai[2] >= MainStrikeMarker;
    private bool Finisher => Projectile.ai[2] >= FinisherMarker;
    private float Seed => Projectile.ai[2] % MainStrikeMarker;
    private float ImpactAge => 4f;
    private float VisualLifetime => Finisher ? 16f : MainStrike ? 13f : 9f;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = MaximumLifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        if (age == 0f)
        {
            Vector2 start = new(Projectile.ai[0], Projectile.ai[1]);
            float intensity = Finisher ? 1.45f : MainStrike ? 1.08f : 0.62f;
            FranFlatLightningEffects.SpawnSegment(Projectile.GetSource_FromThis(), start,
                Projectile.Center, Projectile.owner, intensity,
                FranFlatLightningPalette.ThunderChain, ImpactAge);
        }

        if (Projectile.localAI[1] == 0f && age >= ImpactAge)
        {
            Projectile.localAI[1] = 1f;
            TriggerImpact();
        }

        float impactLight = MainStrike && age >= ImpactAge
            ? 1f - FranMotion.Smooth(FranMotion.Segment(age, ImpactAge, VisualLifetime - 1f))
            : 0f;
        Lighting.AddLight(Projectile.Center,
            0.16f + impactLight * (Finisher ? 0.52f : 0.34f),
            0.18f + impactLight * (Finisher ? 0.62f : 0.42f),
            0.48f + impactLight * 0.52f);

        if (age >= VisualLifetime - 1f)
            Projectile.Kill();
    }

    private void TriggerImpact()
    {
        Color violet = new(155, 125, 255);
        Color azure = new(125, 205, 255);
        FranFlatLightningEffects.Burst(Projectile.Center, Finisher ? 0.95f : MainStrike ? 0.75f : 0.45f,
            FranFlatLightningPalette.ThunderChain, Finisher ? 7 : MainStrike ? 5 : 3,
            Finisher ? 178f : MainStrike ? 126f : 54f, ground: MainStrike);
        if (!MainStrike)
        {
            FranLightingBoltsSystem.SpawnThunderImpact(Projectile.Center, violet, 0.54f);
            FranVfx.SparkBurst(Projectile.Center, azure, 5, 4.2f, 0.52f);
            FranVanillaDustEffects.Impact(Projectile.Center, -Vector2.UnitY, FranSkill.ThunderChain,
                azure, 24, 0.72f);
            if (Projectile.identity % 3 == 0)
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.20f, Pitch = 0.28f }, Projectile.Center);
            return;
        }

        float intensity = Finisher ? 1.32f : 1f;
        FranLightingBoltsSystem.SpawnThunderImpact(Projectile.Center, violet, 1.18f * intensity);
        FranVfx.SparkBurst(Projectile.Center, azure, Finisher ? 22 : 16,
            Finisher ? 10.5f : 8.2f, Finisher ? 1.15f : 0.92f);
        FranVanillaDustEffects.Impact(Projectile.Center, -Vector2.UnitY, FranSkill.ThunderChain,
            azure, Finisher ? 52 : 40, Finisher ? 1.42f : 1.18f);
        FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_FromThis(), Projectile.Center,
            -Vector2.UnitY, Projectile.owner, FranSpectatorPalette.Lightning, Finisher ? 7 : 5,
            Finisher ? 1.42f : 1.15f);

        SpawnGroundArcs(Finisher ? 7 : 4, Finisher ? 1.28f : 1f);
        if (!Main.dedServ)
            Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, Finisher ? 10.5f : 7.4f);
        SoundEngine.PlaySound(SoundID.Item122 with
        {
            Volume = Finisher ? 1f : 0.82f,
            Pitch = Finisher ? -0.34f : -0.22f
        }, Projectile.Center);
        SoundEngine.PlaySound(SoundID.Item93 with
        {
            Volume = Finisher ? 0.62f : 0.48f,
            Pitch = Finisher ? -0.12f : 0.02f
        }, Projectile.Center);
    }

    private void SpawnGroundArcs(int count, float scale)
    {
        if (Main.netMode == NetmodeID.Server || Main.myPlayer != Projectile.owner)
            return;

        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector2 velocity = new Vector2(side, 0f).RotatedBy(Main.rand.NextFloat(-0.24f, 0.14f)) *
                Main.rand.NextFloat(6.8f, 11.8f) * scale;
            float curvature = -side * Main.rand.NextFloat(0.004f, 0.012f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity,
                ModContent.ProjectileType<FranThunderGroundArcVisual>(), 0, 0f, Projectile.owner,
                curvature, Seed + i * 8.17f, scale * Main.rand.NextFloat(0.84f, 1.12f));
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 end = Projectile.Center;
        FranVfx.BeginAdditive();

        if (MainStrike && Age < ImpactAge)
        {
            float warningProgress = FranMotion.Smooth(FranMotion.Segment(Age, 0f, ImpactAge));
            float warningRadius = MathHelper.Lerp(Finisher ? 142f : 108f, Finisher ? 30f : 24f, warningProgress);
            float warningOpacity = MathF.Sin(warningProgress * MathHelper.Pi) * (Finisher ? 0.92f : 0.72f);
            FranVfx.DrawRing(end, new Color(100, 145, 255), warningRadius, warningOpacity,
                -Main.GlobalTimeWrappedHourly * 3.4f, "Cal_BloomRing");
            FranVfx.DrawRing(end, Color.White, warningRadius * 0.62f, warningOpacity * 0.62f,
                Main.GlobalTimeWrappedHourly * 4.8f, "Cal_FadedStarRing");
            for (int i = 0; i < (Finisher ? 8 : 6); i++)
            {
                float angle = MathHelper.TwoPi * i / (Finisher ? 8f : 6f) + Seed * 0.03f;
                Vector2 point = end + angle.ToRotationVector2() * warningRadius;
                FranVfx.DrawTexturedSpark(point, (end - point).SafeNormalize(Vector2.UnitY),
                    i % 2 == 0 ? new Color(115, 175, 255) : new Color(165, 110, 255),
                    20f + warningProgress * 34f, 2.4f + warningProgress * 2.8f, warningOpacity);
            }
        }

        if (Age >= ImpactAge - 1f)
        {
            float impact = 1f - FranMotion.Smooth(FranMotion.Segment(Age, ImpactAge, VisualLifetime - 1f));
            float expansion = FranMotion.Smooth(FranMotion.Segment(Age, ImpactAge, VisualLifetime - 1f));
            Vector2 burstSize = MainStrike
                ? new Vector2(Finisher ? 248f : 194f, Finisher ? 174f : 132f)
                : new Vector2(92f, 66f);
            FranVfx.DrawOriginalPixels("FranImpactBurst", end,
                new Color(105, 155, 255) * (impact * (MainStrike ? 0.94f : 0.56f)), MathHelper.PiOver2,
                burstSize);
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", end, Color.White * (impact * 0.92f),
                MathHelper.PiOver2, burstSize * new Vector2(0.76f, 0.58f));
            if (MainStrike)
            {
                Color ringColor = Color.Lerp(new Color(112, 105, 255), new Color(115, 230, 255),
                    Finisher ? 0.58f : 0.34f);
                float ringRadius = MathHelper.Lerp(34f, Finisher ? 190f : 142f, expansion);
                FranVfx.DrawRing(end, ringColor, ringRadius, impact * 0.76f,
                    Seed * 0.1f + expansion, "Cal_BloomRing");
                FranVfx.DrawRing(end, Color.White, ringRadius * 0.66f, impact * 0.48f,
                    -Seed * 0.07f - expansion, "Cal_HighResHollowCircle");
            }
            FranVfx.DrawBloom(end, Color.White, MainStrike ? (Finisher ? 106f : 82f) : 38f,
                impact * (MainStrike ? 0.62f : 0.32f), MainStrike);
        }
        FranVfx.EndAdditive();
        return false;
    }
}

// 无伤害的余电弹幕：每道电弧沿独立曲线移动并留下拖尾，随后自然熄灭。
public sealed class FranThunderGroundArcVisual : ModProjectile
{
    private const int Lifetime = 30;
    private float Age => Projectile.localAI[0];

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 15;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 6;
        Projectile.height = 6;
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
        float progress = MathHelper.Clamp(age / Lifetime, 0f, 1f);
        float randomTurn = MathF.Sin(Projectile.ai[1] + age * 1.73f) * 0.012f;
        Projectile.velocity = Projectile.velocity.RotatedBy(Projectile.ai[0] + randomTurn) * 0.966f;
        Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y, 0f, 0.10f);
        Projectile.rotation = Projectile.velocity.ToRotation();
        Projectile.Opacity = MathHelper.Clamp(MathF.Sin(progress * MathHelper.Pi) * 1.45f, 0f, 1f);
        Lighting.AddLight(Projectile.Center, 0.08f * Projectile.Opacity, 0.16f * Projectile.Opacity,
            0.38f * Projectile.Opacity);

        FranFlatLightningEffects.EmitTravel(Projectile.Center, Projectile.velocity,
            MathHelper.Clamp(Projectile.ai[2], 0.62f, 1.65f) * Projectile.Opacity,
            FranFlatLightningPalette.ThunderChain, 2, (int)age + Projectile.identity * 7);

        if (!Main.dedServ && (int)age % 6 == 0 && Main.rand.NextBool(2))
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric,
                -Projectile.velocity * Main.rand.NextFloat(0.04f, 0.12f) + Main.rand.NextVector2Circular(0.8f, 0.8f),
                35, new Color(135, 190, 255), Main.rand.NextFloat(0.72f, 1.05f));
            dust.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor) => false;
}
