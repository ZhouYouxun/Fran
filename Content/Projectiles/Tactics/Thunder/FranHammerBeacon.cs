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


public sealed class FranHammerBeacon : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 80; Projectile.height = 80; Projectile.timeLeft = 300;
        Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        Projectile.rotation += 0.025f + Projectile.ai[0] * 0.008f;
        Lighting.AddLight(Projectile.Center, 0.08f, 0.14f, 0.42f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float fade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.identity);
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(105, 145, 225) * (0.54f * fade), Projectile.rotation * 0.7f,
            Vector2.One * (82f + pulse * 12f));
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(115, 155, 255) * (0.30f * fade), -Projectile.rotation,
            Vector2.One * (96f + pulse * 16f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.38f * fade),
            -Projectile.rotation, new Vector2(62f, 36f));
        FranVfx.EndAdditive();
        return false;
    }

    public static void Place(Player owner, Terraria.DataStructures.IEntitySource source, Vector2 position, int stage)
    {
        int type = ModContent.ProjectileType<FranHammerBeacon>();
        int count = 0;
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.owner == owner.whoAmI && projectile.type == type)
                count++;
        if (count >= 3)
            return;
        Projectile.NewProjectile(source, position, Vector2.Zero, type, 0, 0f, owner.whoAmI, stage);
    }

    public static int ActivateAll(Player owner, int damage)
    {
        int count = 0;
        int type = ModContent.ProjectileType<FranHammerBeacon>();
        foreach (Projectile beacon in Main.ActiveProjectiles)
        {
            if (beacon.owner != owner.whoAmI || beacon.type != type)
                continue;
            Projectile.NewProjectile(beacon.GetSource_FromThis(), beacon.Center, Vector2.Zero,
                ModContent.ProjectileType<ThorsHammerBolt>(), Math.Max(1, (int)(damage * 0.92f)),
                6f, owner.whoAmI);
            beacon.Kill();
            count++;
        }
        return count;
    }
}
