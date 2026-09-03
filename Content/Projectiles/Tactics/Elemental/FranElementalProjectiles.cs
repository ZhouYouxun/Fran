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


public sealed class FranFireScar : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 34; Projectile.height = 34; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 72;
        Projectile.tileCollide = true; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 18;
    }
    public override void AI()
    {
        Projectile.velocity *= 0.975f;
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ && Projectile.timeLeft % 3 == 0)
            FranParticleEffects.AuraMote(Projectile.Center, new Color(255, 92, 42), true, 20f);
        Lighting.AddLight(Projectile.Center, 0.44f, 0.12f, 0.025f);
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) =>
        target.AddBuff(BuffID.OnFire3, 240);
    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center,
            new Color(255, 115, 45) * (0.74f * opacity), Projectile.rotation, new Vector2(92f, 42f));
        FranVfx.BeginAdditive();
        FranVfx.DrawShared("Flame", Projectile.Center, new Color(255, 78, 22) * (opacity * 0.52f),
            Projectile.rotation, new Vector2(0.72f, 0.34f));
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 38f,
            new Color(255, 170, 55) * (opacity * 0.46f), Projectile.rotation, new Vector2(132f, 44f));
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranWindField : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 188; Projectile.height = 188; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 72;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 20;
    }
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        Projectile.rotation += 0.09f;
        if (Main.netMode != NetmodeID.MultiplayerClient)
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy() || npc.boss || Vector2.DistanceSquared(npc.Center, Projectile.Center) > 96f * 96f)
                    continue;
                npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * 0.16f * npc.knockBackResist;
            }
        Lighting.AddLight(Projectile.Center, 0.06f, 0.28f, 0.24f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float fade = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
        FranVfx.BeginAdditive();
        FranVfx.DrawShared("WindTwister", Projectile.Center, new Color(105, 235, 215) * (0.30f * fade),
            Projectile.rotation, 0.58f + pulse * 0.08f);
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(135, 245, 225) * (0.22f * fade), -Projectile.rotation,
            Vector2.One * (166f + pulse * 18f));
        for (int i = 0; i < 3; i++)
        {
            float angle = Projectile.rotation + i * MathHelper.TwoPi / 3f;
            Vector2 point = Projectile.Center + angle.ToRotationVector2() * (48f + pulse * 7f);
            FranVfx.DrawOriginalPixels("FranSmearForward", point,
                new Color(170, 255, 235) * (0.18f * fade), angle + MathHelper.PiOver2,
                new Vector2(92f, 26f));
        }
        FranVfx.EndAdditive();
        return false;
    }
}
