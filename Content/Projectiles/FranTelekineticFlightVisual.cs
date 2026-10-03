using FranMod.Content.Players;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// Retains the existing flight-session projectile for held-sword visibility and networking.
// The board itself is drawn by FranTelekineticBoardLayer so it follows the player's draw position.
public sealed class FranTelekineticFlightVisual : ModProjectile
{
    public override string Texture => "Fran/Textures/师父飞剑-Sheet";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 2;
        Projectile.timeLeft = 2;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
        Projectile.hide = true;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override bool PreDraw(ref Color lightColor) => false;

    public override void AI()
    {
        if (!Main.player.IndexInRange(Projectile.owner))
        {
            Projectile.Kill();
            return;
        }
        Player owner = Main.player[Projectile.owner];
        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        if (!owner.active || owner.dead || owner.mount.Active || fran.HasExternalWings ||
            !fran.TelekineticFlightVisualActive)
        {
            Projectile.Kill();
            return;
        }
        Projectile.Center = owner.Center + Vector2.UnitY * owner.gravDir * (owner.height * 0.5f + 3f);
        Projectile.timeLeft = 2;
        if (!Main.dedServ && ++Projectile.localAI[0] % 10 == 0)
        {
            Vector2 position = Projectile.Center + new Vector2(-owner.direction * 22f, owner.gravDir * 3f);
            Vector2 velocity = -owner.velocity * 0.12f + new Vector2(-owner.direction * 0.6f, owner.gravDir * 0.3f);
            Dust dust = Dust.NewDustPerfect(position, DustID.BlueCrystalShard, velocity, 150,
                new Color(105, 155, 255), 0.55f);
            dust.noGravity = true;
            dust.noLight = true;
        }
    }
}
