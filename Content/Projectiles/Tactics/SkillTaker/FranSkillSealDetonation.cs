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
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


public sealed class FranSkillSealDetonation : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 116; Projectile.height = 116; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 18;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool? CanDamage() => Projectile.timeLeft is <= 16 and >= 11 ? null : false;
    public override void AI()
    {
        if (Projectile.localAI[0]++ == 0f)
        {
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.52f, Pitch = 0.24f }, Projectile.Center);
            FranParticleEffects.Implosion(Projectile.Center, new Color(210, 95, 255), 0.62f + Projectile.ai[0] * 0.09f);
            float sealScale = 0.68f + Projectile.ai[0] * 0.10f;
            FranParticleEffects.BlackLight(Projectile.Center, new Color(195, 72, 255), sealScale, 21);
            FranParticleEffects.MysticGlyphs(Projectile.Center, new Color(220, 120, 255),
                3 + (int)Projectile.ai[0], 42f + Projectile.ai[0] * 5f, sealScale);
            FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_FromThis(), Projectile.Center,
                Projectile.ai[2].ToRotationVector2(), Projectile.owner, FranSpectatorPalette.Purple,
                3 + (int)Projectile.ai[0], 0.82f + Projectile.ai[0] * 0.08f);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center,
                1.8f + Projectile.ai[0] * 0.22f);
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float progress = 1f - Projectile.timeLeft / 18f;
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        float radius = MathHelper.Lerp(14f, 74f + Projectile.ai[0] * 8f, FranMotion.EaseOutCubic(progress));
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(185, 70, 255) * (opacity * 0.62f), progress * 1.8f,
            Vector2.One * radius * 1.72f);
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            new Color(220, 125, 255) * (opacity * 0.72f), Projectile.ai[2],
            new Vector2(radius * 2.2f, radius * 1.34f));
        for (int i = 0; i < 3 + (int)Projectile.ai[0]; i++)
        {
            float angle = i * MathHelper.Pi / Math.Max(1, 2 + Projectile.ai[0]) + Projectile.rotation;
            Vector2 axis = angle.ToRotationVector2();
            FranVfx.DrawVertical("Cal_ThinEndedLine", Projectile.Center - axis * radius * 0.72f,
                Projectile.Center + axis * radius * 0.72f, Color.White * (opacity * 0.62f), 4f);
        }
        FranVfx.DrawBloom(Projectile.Center, new Color(220, 120, 255), 42f, opacity * 0.5f);
        FranVfx.EndAdditive();
        return false;
    }
}
