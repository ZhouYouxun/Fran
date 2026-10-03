using System;
using System.IO;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles.Ultimate;

// Standalone port of YC_LeftBladeSwing's enhanced LEFT swing/recovery, not its right-click kit.
public sealed class FranVigilSwing : ModProjectile
{
    private int frame, recovery, animation = 45, side = -1, idle;
    private bool swinging, released, canHit;
    private float offset;
    private Player Owner => Main.player[Projectile.owner];
    public override string Texture => FranShiFuVisuals.TexturePath;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 32;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.timeLeft = 2;
        Projectile.netImportant = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }
    public override void OnSpawn(IEntitySource source)
    {
        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
        offset = MathHelper.ToRadians(-120 * Owner.direction);
        Projectile.rotation = Projectile.velocity.ToRotation() + offset;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => canHit ? null : false;
    public override void AI()
    {
        if (!Owner.active || Owner.dead || !Owner.GetModPlayer<FranVigilPlayer>().Active ||
            Owner.HeldItem.type != ModContent.ItemType<ShiFu>()) { Projectile.Kill(); return; }
        Projectile.timeLeft = 2;
        Projectile.Center = Owner.MountedCenter;
        Owner.heldProj = Projectile.whoAmI;
        Owner.itemTime = Owner.itemAnimation = 2;
        canHit = false;

        if (recovery > 0)
        {
            recovery--;
            offset = MathHelper.Lerp(offset, MathHelper.ToRadians(120f * side * Owner.direction), .1f);
        }
        else
        {
            if (!swinging && Owner.whoAmI == Main.myPlayer)
            {
                if (!Owner.channel || Owner.mouseInterface || Main.mapFullscreen || Main.blockMouse)
                {
                    if (++idle > 20) { Projectile.Kill(); return; }
                }
                else BeginSwing();
            }
            if (swinging)
            {
                float progress = FranVigilRules.SwingCompletion(frame, animation);
                float time = frame - animation / 3f, maximum = animation - animation / 3f;
                canHit = time > (int)(maximum * .2f) && time < (int)(maximum * .9f);
                float eased = ExponentialInOut(progress * .9f);
                offset = MathHelper.Lerp(offset, MathHelper.ToRadians(MathHelper.Lerp(
                    150f * side * Owner.direction, -120f * side * Owner.direction, eased)), .2f);
                if (!released && progress >= .28f)
                {
                    released = true;
                    ReleaseWave();
                }
                frame++;
                if (time >= maximum * .9f)
                {
                    swinging = canHit = false;
                    side = -side;
                    recovery = FranVigilRules.RecoveryFrames(animation);
                }
            }
        }
        Projectile.rotation = Projectile.velocity.ToRotation() + offset;
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);
        Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2 - .15f);
    }

    private void BeginSwing()
    {
        Projectile.velocity = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(Vector2.UnitX * Owner.direction);
        if (Math.Abs(Projectile.velocity.X) > .08f) Owner.ChangeDir(Math.Sign(Projectile.velocity.X));
        // Same 45-frame reference, using ShiFu's existing melee attack-speed/prefix benefit.
        animation = FranVigilRules.UseAnimation(Owner.GetWeaponAttackSpeed(Owner.HeldItem));
        frame = FranVigilRules.StartFrame(animation);
        swinging = true;
        released = false;
        idle = 0;
        Array.Clear(Projectile.localNPCImmunity);
        Projectile.numHits = 0;
        Projectile.damage = Owner.GetWeaponDamage(Owner.HeldItem);
        Projectile.CritChance = Owner.GetWeaponCrit(Owner.HeldItem);
        Projectile.netUpdate = true;
    }

    private void ReleaseWave()
    {
        Vector2 aim = Projectile.velocity;
        if (Owner.whoAmI == Main.myPlayer)
        {
            aim = (Main.MouseWorld - Owner.MountedCenter).SafeNormalize(aim);
            int index = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.MountedCenter + aim * 120f,
                aim * FranVigilRules.WaveSpeed, ModContent.ProjectileType<FranVigilWave>(),
                Math.Max(1, (int)(Projectile.damage * FranVigilRules.WaveDamageMultiplier)),
                Projectile.knockBack * .9f, Projectile.owner, 6f, side);
            if (Main.projectile.IndexInRange(index)) Main.projectile[index].CritChance = Projectile.CritChance;
        }
        if (Main.dedServ) return;
        SoundEngine.PlaySound(SoundID.Item74 with { Volume = .72f, Pitch = -.16f }, Owner.Center);
        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Owner.Center,
            4.5f * Owner.GetModPlayer<FranVigilPlayer>().Intensity);
        FranVigilWaveParticles.Ring(Owner.MountedCenter + aim * 120f, new Color(255, 214, 86), 1.7f, 18);
    }

    private static float ExponentialInOut(float t) => t is <= 0 or >= 1 ? t :
        // Preserve the reference helper's asymmetric exponent, including its sharp second-half snap.
        t < .5f ? MathF.Pow(2, 20 * t - 10) / 2 : (2 - MathF.Pow(2, -20 * t - 10)) / 2;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float collision = 0;
        Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2() *
            FranShiFuVisuals.BladeLengthFromGripPixels * FranShiFuVisuals.TextureScale;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center, tip, 28f, ref collision);
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(frame); writer.Write(recovery); writer.Write(animation); writer.Write(side);
        writer.Write(swinging); writer.Write(released); writer.Write(offset);
    }
    public override void ReceiveExtraAI(BinaryReader reader)
    {
        frame = reader.ReadInt32(); recovery = reader.ReadInt32(); animation = Math.Clamp(reader.ReadInt32(), 6, 180);
        side = reader.ReadInt32() >= 0 ? 1 : -1; swinging = reader.ReadBoolean(); released = reader.ReadBoolean();
        offset = reader.ReadSingle();
        if (!float.IsFinite(offset)) offset = 0;
        if (Math.Abs(Projectile.velocity.X) > .08f) Owner.ChangeDir(Math.Sign(Projectile.velocity.X));
        Array.Clear(Projectile.localNPCImmunity);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.rotation.ToRotationVector2();
        bool flipped = Owner.direction < 0;
        float rotation = Projectile.rotation + MathHelper.PiOver4 + (flipped ? MathHelper.PiOver2 : 0);
        Vector2 grip = Owner.MountedCenter + direction * 12;
        Vector2 position = grip - direction * FranShiFuVisuals.GripInset / 1.05f - Main.screenPosition;
        Texture2D sword = FranShiFuVisuals.Texture, glow = FranShiFuVisuals.GlowTexture;
        SpriteEffects effects = flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Main.EntitySpriteDraw(sword, position, null, Color.Lerp(lightColor, Color.White, .3f), rotation,
            new Vector2(flipped ? sword.Width : 0, sword.Height), FranShiFuVisuals.TextureScale, effects);
        Main.EntitySpriteDraw(glow, position, null, new Color(165, 224, 255, 0) * (.35f + .4f * Owner.GetModPlayer<FranVigilPlayer>().Intensity),
            rotation, new Vector2(flipped ? glow.Width : 0, glow.Height), FranShiFuVisuals.TextureScaleFor(glow), effects);
        return false;
    }
}
