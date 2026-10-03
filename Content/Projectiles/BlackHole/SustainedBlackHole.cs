using System;
using FranMod.Content.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles.BlackHole;

public sealed class SustainedBlackHole : ModProjectile
{
    public const float WorldDiameter = 640f;
    public const float MaximumRange = 800f;
    public const int FadeInFrames = 30;
    public const int FadeOutFrames = 30;
    public const int SustainManaCost = 4;
    public const int SustainManaInterval = 30;

    public override string Texture => "Fran/Assets/Textures/BlackHole/TransparentPixel";
    private ref float Age => ref Projectile.ai[0];
    private ref float ClosingTime => ref Projectile.ai[1];

    // The same election is used for drawing, damage and duplicate removal. Owner/identity
    // are network-stable, unlike whoAmI, so simultaneous client spawns converge.
    public static Projectile FindSoleBlackHole()
    {
        int type = ModContent.ProjectileType<SustainedBlackHole>();
        Projectile selected = null;
        foreach (Projectile candidate in Main.ActiveProjectiles)
        {
            if (candidate.type != type)
                continue;
            if (selected is null || candidate.owner < selected.owner ||
                (candidate.owner == selected.owner && candidate.identity < selected.identity))
                selected = candidate;
        }
        return selected;
    }

    public static Vector2 ClampDestination(Player player, Vector2 destination)
    {
        Vector2 offset = destination - player.Center;
        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y))
            offset = new Vector2(player.direction * 240f, 0f);
        if (offset.LengthSquared() > MaximumRange * MaximumRange)
            offset = Vector2.Normalize(offset) * MaximumRange;
        Vector2 target = player.Center + offset;
        target.X = MathHelper.Clamp(target.X, 32f, Main.maxTilesX * 16f - 32f);
        target.Y = MathHelper.Clamp(target.Y, 32f, Main.maxTilesY * 16f - 32f);
        return target;
    }

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 220;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Magic;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.timeLeft = 2;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 15;
        Projectile.scale = 0f;
    }

    public override void AI()
    {
        if (FindSoleBlackHole() != Projectile)
        {
            Projectile.friendly = false;
            if (Main.netMode != NetmodeID.MultiplayerClient || Projectile.owner == Main.myPlayer)
                Projectile.Kill();
            return;
        }

        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
        {
            Projectile.Kill();
            return;
        }

        Player owner = Main.player[Projectile.owner];
        Projectile.timeLeft = 2;
        Age++;
        if (!owner.active || owner.dead)
            BeginClosing();

        if (ClosingTime == 0f && Projectile.owner == Main.myPlayer)
        {
            if (!owner.channel || owner.noItems || owner.CCed || owner.HeldItem.type != ModContent.ItemType<SingularityFocus>())
                BeginClosing();
            else if ((int)Age % SustainManaInterval == 0 &&
                !owner.CheckMana(SustainManaCost, true, true))
                BeginClosing();
            else
            {
                owner.manaRegenDelay = Math.Max(owner.manaRegenDelay, 60);
                Vector2 destination = ClampDestination(owner, Main.MouseWorld);
                Vector2 desiredVelocity = (destination - Projectile.Center) * 0.14f;
                if (desiredVelocity.LengthSquared() > 32f * 32f)
                    desiredVelocity = Vector2.Normalize(desiredVelocity) * 32f;
                Projectile.velocity = desiredVelocity;
                if ((int)Age % 6 == 0)
                    Projectile.netUpdate = true;
            }
        }

        if (ClosingTime > 0f)
        {
            Projectile.friendly = false;
            Projectile.velocity *= 0.84f;
            ClosingTime++;
            if (ClosingTime >= FadeOutFrames)
            {
                Projectile.Kill();
                return;
            }
        }
        else
        {
            owner.itemTime = 2;
            owner.itemAnimation = 2;
            owner.ChangeDir(Projectile.Center.X >= owner.Center.X ? 1 : -1);
        }

        float grow = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));
        float shrink = 1f - MathHelper.SmoothStep(0f, 1f, ClosingTime / FadeOutFrames);
        Projectile.scale = grow * shrink;
        Projectile.rotation = MathHelper.Lerp(Projectile.rotation,
            MathHelper.Clamp(Projectile.velocity.X * 0.008f, -0.16f, 0.16f), 0.08f);
    }

    private void BeginClosing()
    {
        if (ClosingTime != 0f)
            return;
        ClosingTime = 1f;
        Projectile.netUpdate = true;
    }

    public override bool? CanDamage() => ClosingTime == 0f && Age >= FadeInFrames &&
        FindSoleBlackHole() == Projectile;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Vector2 nearest = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
        float radius = 110f * Projectile.scale;
        return Vector2.DistanceSquared(nearest, Projectile.Center) <= radius * radius;
    }

    public override bool PreDraw(ref Color lightColor) => false;
}
