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


public sealed class FranLightningBranches : ModProjectile
{
    private const int ActiveLifetime = 18;
    private float Age => Projectile.localAI[0];
    private float Delay => Math.Max(0f, Projectile.ai[1]);
    private float VisualScale => Projectile.ai[2] > 0f ? MathHelper.Clamp(Projectile.ai[2], 0.5f, 1.5f) : 1f;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 168; Projectile.height = 168; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = ActiveLifetime;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override void OnSpawn(Terraria.DataStructures.IEntitySource source) =>
        Projectile.timeLeft = ActiveLifetime + (int)Delay;
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage()
    {
        float activeAge = Age - Delay;
        return activeAge is >= 2f and <= 8f ? null : false;
    }
    public override void AI()
    {
        float activeAge = Projectile.localAI[0]++ - Delay;
        if (activeAge == 0f)
        {
            FranLightingBoltsSystem.SpawnThunderImpact(Projectile.Center, new Color(165, 125, 255),
                0.82f * VisualScale);
            for (int i = 0; i < 6; i++)
            {
                float angle = Projectile.velocity.ToRotation() + i * MathHelper.TwoPi / 6f;
                Vector2 end = Projectile.Center + angle.ToRotationVector2() *
                    (58f + (i % 2) * 26f) * VisualScale;
                FranFlatLightningEffects.SpawnSegment(Projectile.GetSource_FromThis(),
                    Projectile.Center, end, Projectile.owner, 0.68f * VisualScale,
                    FranFlatLightningPalette.ThunderChain, 0.8f);
            }
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.42f, Pitch = 0.26f }, Projectile.Center);
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float activeAge = Age - Delay;
        if (activeAge < 0f)
            return false;
        float progress = MathHelper.Clamp(activeAge / ActiveLifetime, 0f, 1f);
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            new Color(165, 125, 255) * (0.74f * opacity), Projectile.velocity.ToRotation(),
            new Vector2(142f, 96f) * VisualScale);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * (0.88f * opacity), Projectile.velocity.ToRotation(),
            new Vector2(96f, 54f) * VisualScale);
        FranVfx.DrawBloom(Projectile.Center, Color.White, 38f * VisualScale, opacity * 0.44f);
        FranVfx.EndAdditive();
        return false;
    }
}
