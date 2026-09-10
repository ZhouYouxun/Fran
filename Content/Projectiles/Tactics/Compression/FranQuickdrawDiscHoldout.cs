using System;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Content.Systems;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// A full Pressurized Quickdraw loadout replaces the normal four-cut chain with one persistent
// circular holdout. The collision follows the actual moving blade rather than filling the disc.
public sealed class FranQuickdrawDiscHoldout : ModProjectile
{
    private const float SuppliedTextureBladeLength = 193.04f;
    private const float WeaponHiltOffset = 8f;
    private const int TrailStyle = 1;
    private const float GoldenStep = 0.618034f;

    private Player Owner => Main.player[Projectile.owner];
    private int TacticLayers => Math.Clamp((int)Projectile.ai[0], 1, FranPlayer.MaximumTacticSlots);

    private float currentAngle;
    private float previousAngle;
    private float currentReach;
    private float angularVelocity;
    private float visualStrength;
    private float spinProgress;
    private float whooshTravel;
    private float evolutionDamageMultiplier = 1f;
    private int facing;
    private int airburstTimer;
    private int airburstSequence;
    private bool initialized;
    private bool releasing;

    public override string Texture => FranShiFuVisuals.TexturePath;

    public override void SetStaticDefaults() =>
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

    public override void SetDefaults()
    {
        Projectile.width = 96;
        Projectile.height = 96;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.ownerHitCheck = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = Math.Max(4, FranBalance.GetInt(
            nameof(FranBalance.QuickdrawFullDiscLocalHitCooldown),
            FranBalance.QuickdrawFullDiscLocalHitCooldown));
        Projectile.netImportant = true;
        Projectile.timeLeft = 2;
    }

    public override bool ShouldUpdatePosition() => false;

    public override bool? CanDamage()
    {
        float stopSpeed = Math.Max(0.001f, FranBalance.GetFloat(
            nameof(FranBalance.QuickdrawFullDiscStopAngularVelocity),
            FranBalance.QuickdrawFullDiscStopAngularVelocity));
        return visualStrength > 0.18f && MathF.Abs(angularVelocity) > stopSpeed * 2f
            ? null
            : false;
    }

    public override void AI()
    {
        if (!Owner.active || Owner.dead || Owner.HeldItem.type != ModContent.ItemType<ShiFu>())
        {
            Projectile.Kill();
            return;
        }

        Projectile.timeLeft = 2;
        if (!initialized)
            InitializeDisc();

        FranPlayer fran = Owner.GetModPlayer<FranPlayer>();
        bool holding = Owner.channel && !Owner.noItems && !Owner.CCed &&
            fran.IsFullTacticLoadout(FranSkill.PressurizedQuickdraw);
        if (!holding && !releasing)
        {
            releasing = true;
            if (Main.myPlayer == Projectile.owner)
                Projectile.netUpdate = true;
        }

        previousAngle = currentAngle;
        float attackSpeed = MathHelper.Clamp(Owner.GetWeaponAttackSpeed(Owner.HeldItem), 0.50f, 2.50f);
        float maximumSpeed = Math.Max(0.02f, FranBalance.GetFloat(
            nameof(FranBalance.QuickdrawFullDiscMaximumAngularVelocity),
            FranBalance.QuickdrawFullDiscMaximumAngularVelocity)) * attackSpeed;
        float acceleration = Math.Max(0.001f, FranBalance.GetFloat(
            nameof(FranBalance.QuickdrawFullDiscSpinAcceleration),
            FranBalance.QuickdrawFullDiscSpinAcceleration)) * attackSpeed;

        if (!releasing)
        {
            angularVelocity = MoveTowards(angularVelocity, maximumSpeed * facing, acceleration);
            visualStrength = MathHelper.Lerp(visualStrength, 1f, 0.22f);
        }
        else
        {
            float retention = MathHelper.Clamp(FranBalance.GetFloat(
                nameof(FranBalance.QuickdrawFullDiscReleaseVelocityRetention),
                FranBalance.QuickdrawFullDiscReleaseVelocityRetention), 0.70f, 0.99f);
            angularVelocity *= retention;
            visualStrength = Math.Min(visualStrength,
                MathHelper.Clamp(MathF.Abs(angularVelocity) / Math.Max(0.04f, maximumSpeed * 0.30f), 0f, 1f));
            float stopSpeed = Math.Max(0.001f, FranBalance.GetFloat(
                nameof(FranBalance.QuickdrawFullDiscStopAngularVelocity),
                FranBalance.QuickdrawFullDiscStopAngularVelocity));
            if (MathF.Abs(angularVelocity) <= stopSpeed)
            {
                Projectile.Kill();
                return;
            }
        }

        currentAngle += angularVelocity;
        spinProgress = (spinProgress + MathF.Abs(angularVelocity) / MathHelper.TwoPi) % 1f;
        whooshTravel += MathF.Abs(angularVelocity);
        currentReach = CalculateReach();

        Vector2 blade = currentAngle.ToRotationVector2();
        // 与环境之刃的持续回旋一致：剑转到人物左侧就朝左，转到右侧就朝右。
        // facing 仍只负责固定旋转方向和剑贴图朝向，不能在这里改写，否则刀盘会每半圈反转。
        int playerFacing = Math.Sign(blade.X);
        if (playerFacing != 0)
            Owner.ChangeDir(playerFacing);
        Projectile.Center = Owner.MountedCenter + blade *
            (WeaponHiltOffset + (currentReach - WeaponHiltOffset) * 0.54f);
        Projectile.rotation = currentAngle + MathHelper.PiOver4 +
            (facing < 0 ? MathHelper.PiOver2 : 0f);
        Owner.heldProj = Projectile.whoAmI;
        Owner.itemTime = Owner.itemAnimation = 2;
        Owner.itemRotation = currentAngle + (Owner.direction < 0 ? MathHelper.Pi : 0f);
        Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            currentAngle - MathHelper.PiOver2);

        if (!releasing)
            EmitAirburstAlongBlade(blade);
        EmitRotationCadence(blade);
        Lighting.AddLight(Owner.MountedCenter + blade * currentReach * 0.64f,
            new Vector3(0.08f, 0.30f, 0.42f) * visualStrength);
    }

    private void InitializeDisc()
    {
        initialized = true;
        facing = Math.Sign(Projectile.velocity.X);
        if (facing == 0)
            facing = Owner.direction == 0 ? 1 : Owner.direction;
        Owner.ChangeDir(facing);
        float aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * facing).ToRotation();
        currentAngle = previousAngle = aim - 1.88f * facing;
        angularVelocity = 0.035f * facing;
        visualStrength = 0.12f;
        currentReach = CalculateReach();
        evolutionDamageMultiplier = Owner.GetModPlayer<FranEvolutionPlayer>()
            .DirectSwordDamageMultiplier(1);
    }

    private float CalculateReach()
    {
        FranEvolutionPlayer evolution = Owner.GetModPlayer<FranEvolutionPlayer>();
        float visibleBladeLength = SuppliedTextureBladeLength * 1.05f * FranShiFuVisuals.TextureScale;
        return WeaponHiltOffset + visibleBladeLength * evolution.BladeReachMultiplier;
    }

    private void EmitAirburstAlongBlade(Vector2 blade)
    {
        float minimumSpeed = Math.Max(0.001f, FranBalance.GetFloat(
            nameof(FranBalance.QuickdrawFullDiscStopAngularVelocity),
            FranBalance.QuickdrawFullDiscStopAngularVelocity)) * 5f;
        if (MathF.Abs(angularVelocity) < minimumSpeed || Main.myPlayer != Owner.whoAmI)
            return;

        int interval = Math.Max(1, FranBalance.GetInt(
            nameof(FranBalance.QuickdrawFullDiscAirburstInterval),
            FranBalance.QuickdrawFullDiscAirburstInterval));
        if (++airburstTimer < interval)
            return;
        airburstTimer = 0;

        float sequence = airburstSequence++ * GoldenStep % 1f;
        float radial = MathHelper.Lerp(0.40f, 0.93f, sequence);
        float spawnAngle = currentAngle - angularVelocity * 0.45f;
        Vector2 spawnDirection = spawnAngle.ToRotationVector2();
        float radius = MathHelper.Lerp(WeaponHiltOffset + 28f, currentReach - 18f, radial);
        Vector2 position = Owner.MountedCenter + spawnDirection * radius;
        float scale = 0.60f + MathF.Sin(sequence * MathHelper.Pi) * 0.12f;
        float damageScale = FranTacticOutputBudget.DiscAirburstMultiplier(Owner);
        int damage = Math.Max(1, (int)(Projectile.damage * damageScale));
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, blade,
            ModContent.ProjectileType<FranCompressionAirburst>(), damage,
            Projectile.knockBack * 0.35f, Owner.whoAmI,
            FranCompressionAirburst.EncodeDelay(4f, fullLoadout: true, fullDiscVisual: true),
            scale, spawnAngle);
    }

    private void EmitRotationCadence(Vector2 blade)
    {
        if (whooshTravel >= MathHelper.Pi)
        {
            whooshTravel %= MathHelper.Pi;
            if (!Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item60 with
                    { Volume = 0.46f * visualStrength, Pitch = 0.16f, PitchVariance = 0.06f },
                    Owner.Center);
        }

        if (Main.dedServ || Main.GameUpdateCount % 2 != 0 || visualStrength <= 0.18f)
            return;
        Vector2 tangent = blade.RotatedBy(MathHelper.PiOver2 * Math.Sign(angularVelocity));
        Vector2 position = Owner.MountedCenter + blade *
            Main.rand.NextFloat(currentReach * 0.48f, currentReach * 0.94f);
        FranParticleSystem.Spawn(FranSwingCadenceParticle.SparklePulse(position,
            tangent * Main.rand.NextFloat(1.8f, 3.6f), new Color(115, 222, 255),
            tangent.ToRotation(), Main.rand.NextFloat(0.34f, 0.72f)));
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        const float collisionWidth = 38f;
        Vector2 previousTip = Owner.MountedCenter + previousAngle.ToRotationVector2() * currentReach;
        Vector2 currentTip = Owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach;
        float tipTravel = Vector2.Distance(previousTip, currentTip);
        int samples = Math.Clamp((int)MathF.Ceiling(tipTravel / 16f), 1, 28);
        for (int sample = 0; sample <= samples; sample++)
        {
            float completion = sample / (float)samples;
            float angle = MathHelper.Lerp(previousAngle, currentAngle, completion);
            Vector2 blade = angle.ToRotationVector2();
            Vector2 start = Owner.MountedCenter + blade * WeaponHiltOffset;
            Vector2 end = Owner.MountedCenter + blade * currentReach;
            float collisionPoint = 0f;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    start, end, collisionWidth, ref collisionPoint))
                return true;
        }
        return false;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        modifiers.FinalDamage *= evolutionDamageMultiplier;
        FranEvolutionPlayer evolution = Owner.GetModPlayer<FranEvolutionPlayer>();
        if (evolution.HasNode(FranEvolutionNode.MagicStoneAnalysis) &&
            FranEvolutionBossSystem.IsAnalyzedFor(target, Owner.whoAmI))
            modifiers.ArmorPenetration += evolution.AnalysisArmorPenetration;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        Vector2 incoming = currentAngle.ToRotationVector2();
        FranEvolutionPlayer evolution = Owner.GetModPlayer<FranEvolutionPlayer>();
        FranEvolutionBossSystem.RegisterAnalysisHit(target, Owner, 1, Projectile.identity);
        evolution.ApplyResonantPull(target, Owner.MountedCenter + incoming * currentReach);
        FranParticleEffects.MeleeImpact(target.Center, incoming, new Color(105, 220, 255), 1);
        FranVanillaDustEffects.Impact(target.Center, incoming, FranSkill.PressurizedQuickdraw,
            new Color(105, 220, 255), 5, 0.92f);
        Owner.GetModPlayer<FranPlayer>().ShakeScreen(target.Center, 2.8f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 blade = currentAngle.ToRotationVector2();
        Vector2 trailPivot = Owner.MountedCenter + blade * WeaponHiltOffset;
        float trailReach = Math.Max(24f, currentReach - WeaponHiltOffset);
        Color energyColor = new(105, 220, 255);
        if (!FranBladeTrailRenderer.TryDrawSlash(trailPivot, currentAngle, trailReach,
                spinProgress, energyColor, visualStrength, TrailStyle, facing))
            FranBladeTrailRenderer.DrawSlashFallback(trailPivot, currentAngle, trailReach,
                energyColor, visualStrength, TrailStyle, facing);

        DrawWeapon(lightColor, energyColor);
        return false;
    }

    private void DrawWeapon(Color lightColor, Color energyColor)
    {
        Texture2D texture = FranShiFuVisuals.Texture;
        Texture2D ghostTexture = FranShiFuVisuals.GhostTexture;
        SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 origin = new(facing < 0 ? texture.Width : 0f, texture.Height);
        Vector2 ghostOrigin = new(facing < 0 ? ghostTexture.Width : 0f, ghostTexture.Height);
        Vector2 drawPosition = Owner.MountedCenter + currentAngle.ToRotationVector2() * WeaponHiltOffset -
            Main.screenPosition;
        float drawScale = 1.05f * FranShiFuVisuals.TextureScale;
        Color ghostColor = new(energyColor.R, energyColor.G, energyColor.B, 0);
        for (int i = 0; i < 12; i++)
        {
            Vector2 offset = (MathHelper.TwoPi * i / 12f).ToRotationVector2() *
                (4.2f * visualStrength);
            Main.EntitySpriteDraw(ghostTexture, drawPosition + offset, null,
                ghostColor * (0.12f * visualStrength), Projectile.rotation, ghostOrigin,
                drawScale, effects, 0f);
        }
        Main.EntitySpriteDraw(texture, drawPosition, null, lightColor * visualStrength,
            Projectile.rotation, origin, drawScale, effects, 0f);
    }

    private static float MoveTowards(float current, float target, float maximumDelta)
    {
        if (MathF.Abs(target - current) <= maximumDelta)
            return target;
        return current + MathF.Sign(target - current) * maximumDelta;
    }
}
