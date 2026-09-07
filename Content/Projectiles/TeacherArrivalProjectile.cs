using FranMod.Content.Systems;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// Rebuildable visual projection. Destroying it never gives an item or reopens the character chance.
public sealed class TeacherArrivalProjectile : ModProjectile
{
    public override string Texture => FranTeacherVisuals.TexturePath;
    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;
    public override void SetDefaults()
    {
        Projectile.width = 24;
        Projectile.height = 100;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 120;
        Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        if (!TeacherArrivalSystem.Instance.TryGet((int)Projectile.ai[0], out TeacherArrivalRecord record))
        {
            if (++Projectile.localAI[0] > 60f)
                Projectile.Kill();
            return;
        }
        Projectile.localAI[0] = 0f;
        Projectile.timeLeft = 120;
        if (record.Age >= TeacherArrivalRules.LandingAge && record.ItemIndex >= 0)
        {
            Projectile.Kill();
            return;
        }
        Projectile.Center = TeacherArrivalRules.SwordTip(record.Landing, record.Age);
        if (!Main.dedServ && record.Age >= TeacherArrivalRules.Windup)
            Lighting.AddLight(Projectile.Center, 0.4f, 0.85f, 1.35f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        if (TeacherArrivalSystem.Instance.TryGet((int)Projectile.ai[0], out TeacherArrivalRecord record))
            TeacherArrivalVisuals.DrawArrival(record);
        return false;
    }
}
