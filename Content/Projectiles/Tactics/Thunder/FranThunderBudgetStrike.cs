using System;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

/// <summary>每条落雷只直击一个预先标记的 NPC；其余伤害由独立落点爆炸结算。</summary>
public sealed class FranThunderBudgetStrike : ModProjectile
{
    private Vector2 Destination => new(Projectile.ai[0], Projectile.ai[1]);
    private int TargetIndex => (int)Projectile.ai[2] - 1;
    private float Age => Projectile.localAI[0];
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 28;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 7;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Age == 4f ? null : false;
    public override bool? CanHitNPC(NPC target) => target.whoAmI == TargetIndex ? null : false;
    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) => modifiers.SourceDamage *= 0.5f;
    public override void AI()
    {
        Projectile.localAI[0]++;
        if (Age >= 4f)
            Projectile.Center = Destination;
        if (Age == 5f)
        {
            if (Projectile.owner == Main.myPlayer)
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Destination, Vector2.Zero,
                    ModContent.ProjectileType<FranThunderImpactExplosion>(),
                    Math.Max(1, Projectile.damage - Projectile.damage / 2), 0f, Projectile.owner);
            Projectile.Kill();
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        // 被分配目标的落雷在命中前显示短暂标记，落点按发射时坐标固定。
        if (TargetIndex >= 0 && Age < 4f)
        {
            FranVfx.BeginAdditive();
            FranVfx.DrawRing(Destination, new Color(120, 185, 255), 22f, 0.7f, 0f);
            FranVfx.EndAdditive();
        }
        return false;
    }
}

public sealed class FranThunderImpactExplosion : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = FranBalance.GetInt(nameof(FranBalance.ThunderImpactExplosionSize),
            FranBalance.ThunderImpactExplosionSize);
        Projectile.timeLeft = FranBalance.GetInt(nameof(FranBalance.ThunderImpactExplosionLifetime),
            FranBalance.ThunderImpactExplosionLifetime);
        Projectile.penetrate = FranBalance.GetInt(nameof(FranBalance.ThunderImpactExplosionPenetration),
            FranBalance.ThunderImpactExplosionPenetration);
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool PreDraw(ref Color lightColor) => false;
}