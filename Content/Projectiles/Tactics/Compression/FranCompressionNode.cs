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


public sealed class FranCompressionNode : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 50; Projectile.height = 50; Projectile.timeLeft = 180;
        Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        Projectile.rotation += 0.035f + Projectile.ai[0] * 0.008f;
        Lighting.AddLight(Projectile.Center, 0.10f, 0.31f, 0.42f);
        if (!Main.dedServ && Projectile.timeLeft % 7 == 0)
            FranParticleSystem.Spawn(new FranResonanceNoteParticle(Projectile.Center,
                new Color(210, 250, 255), 28f + Projectile.ai[0] * 5f, 18, Projectile.rotation));
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float fade = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
        float pulse = 0.5f + MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + Projectile.identity) * 0.5f;
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(155, 225, 245) * (0.64f * fade), Projectile.rotation * 0.55f,
            Vector2.One * (58f + pulse * 10f));
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(210, 250, 255) * (0.42f * fade), -Projectile.rotation * 1.2f,
            Vector2.One * (70f + pulse * 12f));
        Vector2 axis = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        FranVfx.DrawTexturedSpark(Projectile.Center, axis, Color.White, 48f, 3.2f, 0.68f * fade);
        FranVfx.EndAdditive();
        return false;
    }

    public static void Place(Player owner, Terraria.DataStructures.IEntitySource source, Vector2 position,
        Vector2 direction, int stage)
    {
        int type = ModContent.ProjectileType<FranCompressionNode>();
        Projectile oldest = null;
        int count = 0;
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner != owner.whoAmI || projectile.type != type)
                continue;
            count++;
            if (oldest is null || projectile.timeLeft < oldest.timeLeft)
                oldest = projectile;
        }
        if (count >= 3)
            oldest?.Kill();
        Projectile.NewProjectile(source, position, direction.SafeNormalize(Vector2.UnitX * owner.direction),
            type, 0, 0f, owner.whoAmI, stage);
        SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.25f, Pitch = 0.45f + stage * 0.08f }, position);
    }

    public static int DetonateAll(Player owner, int damage, int pressure)
    {
        int count = 0;
        int type = ModContent.ProjectileType<FranCompressionNode>();
        foreach (Projectile node in Main.ActiveProjectiles)
        {
            if (node.owner != owner.whoAmI || node.type != type)
                continue;
            Vector2 direction = node.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
            Projectile.NewProjectile(node.GetSource_FromThis(), node.Center - direction * 42f, direction,
                ModContent.ProjectileType<FranCompressedCut>(),
                Math.Max(1, (int)(damage * (0.38f + pressure * 0.12f))), 4f,
                owner.whoAmI, pressure);
            FranParticleEffects.Compression(node.Center, direction, new Color(210, 250, 255), 0.82f, true);
            node.Kill();
            count++;
        }
        return count;
    }

    public static void DismissAll(Player owner)
    {
        int type = ModContent.ProjectileType<FranCompressionNode>();
        foreach (Projectile node in Main.ActiveProjectiles)
            if (node.owner == owner.whoAmI && node.type == type)
                node.Kill();
    }
}
