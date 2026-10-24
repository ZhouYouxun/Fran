using System;
using FranMod.Core;
using FranMod.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// One persistent half-size blade per Multiple Body slot. The parent owns the
// input, combo clock, sounds and skill releases; these blades only cut.
public sealed class FranOrbitingDiscBlade : ModProjectile
{
    private const float SizeRatio = 0.5f;
    private int age;
    private Vector2 previousCenter;
    private float previousRotation;
    private bool positioned;

    private int Slot => Math.Clamp((int)Projectile.ai[1], 0, 3);
    private float BladeRadius => FranShiFuVisuals.BladeLengthFromGripPixels *
        1.05f * FranShiFuVisuals.TextureScale * SizeRatio;
    public override string Texture => "Fran/Textures/师父飞剑-影分身弹";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 24;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
        Projectile.netImportant = true;
        Projectile.timeLeft = 2;
    }

    public override bool ShouldUpdatePosition() => false;

    private FranSpinningDisc Parent()
    {
        int index = (int)Projectile.ai[0];
        if (!Main.projectile.IndexInRange(index)) return null;
        Projectile parent = Main.projectile[index];
        return parent.active && parent.owner == Projectile.owner &&
            parent.identity == (int)Projectile.ai[2] ? parent.ModProjectile as FranSpinningDisc : null;
    }

    public override void AI()
    {
        FranSpinningDisc parent = Parent();
        if (parent is null)
        {
            // Network delivery may create the child before its parent on a peer.
            if (++age > 20) Projectile.Kill();
            else Projectile.timeLeft = 2;
            return;
        }
        if (parent.IsReturning)
        {
            Projectile.Kill();
            return;
        }

        Projectile.timeLeft = 2;
        age++;
        if (positioned)
        {
            previousCenter = Projectile.Center;
            previousRotation = Projectile.rotation;
        }
        int bladeCount = Math.Max(1, parent.OrbitingBladeCount);
        float orbitSpeed = Slot switch { 0 => 0.046f, 1 => -0.034f, 2 => 0.061f, _ => -0.052f };
        float orbitRadius = bladeCount == 4 ? (Slot % 2 == 0 ? 32f : 64f) : Slot == 0 ? 32f : 64f;
        float slotAngle = Slot * MathHelper.TwoPi / bladeCount;
        float orbitAngle = age * orbitSpeed + slotAngle;
        Projectile.Center = parent.Projectile.Center + orbitAngle.ToRotationVector2() * orbitRadius;
        Projectile.rotation = parent.Projectile.rotation + slotAngle;
        if (!positioned)
        {
            positioned = true;
            previousCenter = Projectile.Center;
            previousRotation = Projectile.rotation;
        }
        Projectile.damage = Math.Max(1, (int)MathF.Round(parent.Projectile.damage *
            parent.OrbitingBladeDamageMultiplier));
        Projectile.CritChance = parent.Projectile.CritChance;
        Projectile.localNPCHitCooldown = parent.Projectile.localNPCHitCooldown;
        Lighting.AddLight(Projectile.Center, parent.DiscAccent.ToVector3() * 0.22f * parent.DiscOpacity);
    }

    public override bool? CanDamage() => positioned && Parent() is { IsReturning: false } ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float width = 12f * FranBalance.GetCoreSizeMultiplier();
        float travel = MathF.Abs(Projectile.rotation - previousRotation) * BladeRadius +
            Vector2.Distance(previousCenter, Projectile.Center);
        int samples = Math.Clamp((int)MathF.Ceiling(travel / Math.Max(6f, width * 0.6f)), 4, 24);
        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector2 center = Vector2.Lerp(previousCenter, Projectile.Center, t);
            Vector2 blade = MathHelper.Lerp(previousRotation, Projectile.rotation, t).ToRotationVector2();
            float collision = 0f;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), center,
                center + blade * BladeRadius, width, ref collision)) return true;
        }
        return false;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        FranSpinningDisc parent = Parent();
        if (parent is null || !positioned) return false;
        float opacity = parent.DiscOpacity;
        Color accent = parent.DiscAccent;
        FranDiscVisuals.DrawDisc(Projectile.Center, BladeRadius, Projectile.rotation, accent, opacity);
        Texture2D sword = ModContent.Request<Texture2D>(Texture).Value;
        Vector2 position = Projectile.Center - Main.screenPosition;
        float swordScale = BladeRadius / Math.Max(1, sword.Width);
        Vector2 origin = new(0f, sword.Height * 0.5f);
        Main.EntitySpriteDraw(sword, position, null, lightColor * opacity, Projectile.rotation,
            origin, swordScale, SpriteEffects.None);
        Color glow = accent * (opacity * 0.42f);
        glow.A = 0;
        Main.EntitySpriteDraw(sword, position, null, glow, Projectile.rotation,
            origin, swordScale * 1.06f, SpriteEffects.None);
        FranDiscVisuals.DrawPhantomSpin(Projectile.Center, Projectile.rotation, accent, opacity,
            SizeRatio, sword);
        return false;
    }
}
