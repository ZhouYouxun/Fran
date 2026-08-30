using System;
using FranMod.Content.Players;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

public sealed class FranMagicStoneAbsorptionVfx : ModProjectile
{
    private bool Arrived
    {
        get => Projectile.localAI[1] > 0f;
        set => Projectile.localAI[1] = value ? 1f : 0f;
    }

    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 16;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
    }

    public override void SetDefaults()
    {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.timeLeft = 150;
    }

    public override bool? CanDamage() => false;

    public override void AI()
    {
        int targetIndex = Math.Clamp((int)Projectile.ai[0], 0, Main.maxPlayers - 1);
        Player target = Main.player[targetIndex];
        if (!target.active)
        {
            Projectile.Kill();
            return;
        }

        Projectile.localAI[0]++;
        Vector2 destination = target.MountedCenter + new Vector2(-target.direction * 15f, -24f);
        Vector2 toTarget = destination - Projectile.Center;
        float speed = MathHelper.Lerp(5f, 20f, MathHelper.Clamp(Projectile.localAI[0] / 70f, 0f, 1f));
        Vector2 desired = toTarget.SafeNormalize(Vector2.UnitY) * speed;
        Vector2 spiral = desired.RotatedBy(MathHelper.PiOver2) * MathF.Sin(Projectile.localAI[0] * 0.22f) * 0.18f;
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired + spiral, 0.12f);
        Projectile.rotation += 0.16f + Projectile.velocity.Length() * 0.01f;
        Lighting.AddLight(Projectile.Center, new Vector3(0.30f, 0.16f, 0.44f));

        if (!Main.dedServ && Projectile.localAI[0] % 4f == 0f)
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch,
                -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.8f, 0.8f), 90,
                new Color(170, 120, 255), Main.rand.NextFloat(0.7f, 1.05f));
            dust.noGravity = true;
        }

        if (toTarget.LengthSquared() <= 24f * 24f)
        {
            Arrived = true;
            Projectile.Kill();
        }
    }

    public override void OnKill(int timeLeft)
    {
        int targetIndex = Math.Clamp((int)Projectile.ai[0], 0, Main.maxPlayers - 1);
        if (!Arrived || Main.dedServ || targetIndex != Main.myPlayer)
            return;

        Player target = Main.player[targetIndex];
        SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.72f, Pitch = 0.32f }, target.Center);
        CombatText.NewText(target.Hitbox, new Color(210, 174, 255),
            Language.GetTextValue("Mods.Fran.Evolution.MagicStoneAcquired"));
        for (int i = 0; i < 18; i++)
        {
            Vector2 velocity = (MathHelper.TwoPi * i / 18f).ToRotationVector2() * Main.rand.NextFloat(1.8f, 4.6f);
            Dust dust = Dust.NewDustPerfect(target.MountedCenter, i % 4 == 0 ? DustID.GoldFlame : DustID.PurpleTorch,
                velocity, 80, default, Main.rand.NextFloat(0.85f, 1.25f));
            dust.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        Vector2 origin = new(0.5f);
        for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                continue;
            float completion = 1f - i / (float)Projectile.oldPos.Length;
            Vector2 center = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
            Color trail = Color.Lerp(new Color(76, 36, 130), new Color(185, 125, 255), completion) *
                (completion * 0.38f);
            Main.spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), trail,
                Projectile.rotation - i * 0.08f, origin, new Vector2(7f, 3f) * completion,
                SpriteEffects.None, 0f);
        }

        Vector2 position = Projectile.Center - Main.screenPosition;
        float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.12f;
        Main.spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), new Color(113, 64, 190) * 0.55f,
            Projectile.rotation, origin, new Vector2(19f, 19f) * pulse, SpriteEffects.None, 0f);
        Main.spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), new Color(210, 162, 255),
            Projectile.rotation, origin, new Vector2(10f, 10f) * pulse, SpriteEffects.None, 0f);
        Main.spriteBatch.Draw(pixel, position, new Rectangle(0, 0, 1, 1), Color.White,
            -Projectile.rotation, origin, new Vector2(4f, 4f), SpriteEffects.None, 0f);
        return false;
    }
}

public sealed class FranEvolutionEchoSlash : ModProjectile
{
    private const int TrailLength = 22;
    private readonly Vector2[] tipHistory = new Vector2[TrailLength];
    private bool initialized;
    private float currentAngle;
    private float currentReach;

    private Player Owner => Main.player[Projectile.owner];
    private int Duration => Math.Max(12, (int)Projectile.ai[0]);
    private float Progress => MathHelper.Clamp(Projectile.localAI[0] / Duration, 0f, 1f);

    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetDefaults()
    {
        Projectile.width = 40;
        Projectile.height = 40;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.ownerHitCheck = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
        Projectile.timeLeft = 2;
    }

    public override bool ShouldUpdatePosition() => false;

    public override bool? CanDamage() => Progress is >= 0.31f and <= 0.60f ? null : false;

    public override void AI()
    {
        if (!Owner.active || Owner.dead)
        {
            Projectile.Kill();
            return;
        }
        Projectile.timeLeft = 2;
        float aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction).ToRotation();
        float swing = SmoothStep(Progress, 0.18f, 0.68f);
        currentAngle = aim + MathHelper.Lerp(-1.62f, 1.42f, swing) * Owner.direction;
        currentReach = 96f + MathF.Sin(MathHelper.Clamp(Progress, 0f, 1f) * MathHelper.Pi) * 26f;
        Projectile.Center = Owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach * 0.55f;
        Projectile.rotation = currentAngle;

        for (int i = TrailLength - 1; i > 0; i--)
            tipHistory[i] = tipHistory[i - 1];
        tipHistory[0] = Owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach;
        if (!initialized)
        {
            initialized = true;
            for (int i = 1; i < TrailLength; i++)
                tipHistory[i] = tipHistory[0];
        }

        Lighting.AddLight(Projectile.Center, new Vector3(0.12f, 0.22f, 0.31f));
        Projectile.localAI[0]++;
        if (Projectile.localAI[0] >= Duration)
            Projectile.Kill();
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Owner.MountedCenter,
            Owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach, 25f, ref collisionPoint);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (Main.dedServ)
            return;
        Vector2 direction = currentAngle.ToRotationVector2();
        for (int i = 0; i < 8; i++)
        {
            Dust dust = Dust.NewDustPerfect(target.Center, i % 4 == 0 ? DustID.GoldFlame : DustID.MagicMirror,
                direction.RotatedByRandom(0.7f) * Main.rand.NextFloat(1.4f, 4.5f), 90, default,
                Main.rand.NextFloat(0.75f, 1.1f));
            dust.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        for (int i = TrailLength - 2; i >= 0; i--)
        {
            Vector2 start = tipHistory[i + 1] - Main.screenPosition;
            Vector2 end = tipHistory[i] - Main.screenPosition;
            float strength = 1f - i / (float)TrailLength;
            DrawLine(start, end, new Color(105, 80, 212) * (strength * 0.48f), 12f * strength + 1f);
            DrawLine(start, end, new Color(190, 237, 255) * (strength * 0.72f), 3f * strength + 0.5f);
        }
        return false;
    }

    private static float SmoothStep(float value, float start, float end)
    {
        float t = MathHelper.Clamp((value - start) / Math.Max(0.001f, end - start), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static void DrawLine(Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 edge = end - start;
        if (edge.LengthSquared() < 0.01f)
            return;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, start, new Rectangle(0, 0, 1, 1), color,
            edge.ToRotation(), new Vector2(0f, 0.5f), new Vector2(edge.Length(), width), SpriteEffects.None, 0f);
    }
}

public sealed class FranEvolutionUnlockSigil : ModProjectile
{
    private Player Owner => Main.player[Projectile.owner];

    public override string Texture => "Terraria/Images/MagicPixel";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = 42;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        if (!Owner.active || Owner.dead)
        {
            Projectile.Kill();
            return;
        }
        Projectile.Center = Owner.MountedCenter;
        Projectile.rotation += 0.045f;
        Lighting.AddLight(Projectile.Center, BranchColor().ToVector3() * 0.35f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float progress = 1f - Projectile.timeLeft / 42f;
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        float radius = MathHelper.Lerp(18f, 92f, 1f - MathF.Pow(1f - progress, 3f));
        Vector2 center = Projectile.Center - Main.screenPosition;
        Color color = BranchColor();
        const int segments = 24;
        for (int i = 0; i < segments; i++)
        {
            float angleA = MathHelper.TwoPi * i / segments + Projectile.rotation;
            float angleB = MathHelper.TwoPi * (i + 0.65f) / segments + Projectile.rotation;
            DrawLine(center + angleA.ToRotationVector2() * radius,
                center + angleB.ToRotationVector2() * radius, color * (opacity * 0.72f), 2f);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector2 direction = (Projectile.rotation + MathHelper.PiOver2 * i).ToRotationVector2();
            DrawLine(center + direction * radius * 0.20f, center + direction * radius * 0.82f,
                Color.White * (opacity * 0.45f), 1.2f);
        }
        return false;
    }

    private Color BranchColor() => FranEvolutionInfo.BranchColor((FranEvolutionBranch)Math.Clamp((int)Projectile.ai[0], 0, 2));

    private static void DrawLine(Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 edge = end - start;
        if (edge.LengthSquared() < 0.01f)
            return;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, start, new Rectangle(0, 0, 1, 1), color,
            edge.ToRotation(), new Vector2(0f, 0.5f), new Vector2(edge.Length(), width), SpriteEffects.None, 0f);
    }
}
