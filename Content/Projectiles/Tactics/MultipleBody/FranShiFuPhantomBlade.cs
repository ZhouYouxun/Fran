using System;
using System.IO;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

public sealed class FranShiFuPhantomBlade : ModProjectile
{
    private const float FullLoadoutSpeedFlag = 100f;
    private const int UpdatesPerFrame = 4;
    private const float TileGraceFraction = 0.30f;
    internal const int AttachmentFrames = 150;
    private const float BladeSize = 0.70f;
    private const float SwordTextureSize = 0.80f;
    private Vector2 formationOrigin;
    private float age;
    private bool launched;
    private int flightLifetimeFrames;
    private int attachedNpc = -1;
    private int attachedNpcType;
    private NPC attachedEntity;
    private Vector2 attachmentOffset;
    private float attachmentAge;
    private float impactHeading;
    private bool detonated;
    private bool discTargeting;
    private Vector2 discTargetOrigin;
    private int discTarget = -1;
    private int discIdentity = -1;
    private int discSlot = -1;
    private Vector2 discOffset;

    // Only right-click clones acquire enemies. Left-click clones keep their parallel cursor aim.
    internal void SetDiscTargeting(Projectile disc)
    {
        discTargeting = true;
        discTargetOrigin = disc.Center;
        discIdentity = disc.identity;
        discSlot = disc.whoAmI;
        discOffset = Projectile.Center - disc.Center;
        UpdateDiscTarget();
    }

    private void FollowDiscFormation()
    {
        if (!discTargeting || discIdentity < 0) return;
        Projectile carrier = Main.projectile.IndexInRange(discSlot) ? Main.projectile[discSlot] : null;
        bool Matches(Projectile p) => p is not null && p.active && p.owner == Projectile.owner &&
            p.identity == discIdentity && p.ModProjectile is FranSpinningDisc;
        if (!Matches(carrier))
        {
            carrier = null;
            foreach (Projectile candidate in Main.ActiveProjectiles)
                if (Matches(candidate)) { carrier = candidate; discSlot = candidate.whoAmI; break; }
        }
        if (carrier is null) return;
        discTargetOrigin = carrier.Center;
        formationOrigin = carrier.Center + discOffset;
        Projectile.Center = formationOrigin;
    }

    private void UpdateDiscTarget()
    {
        if (!discTargeting || Projectile.owner != Main.myPlayer) return;
        float range = Math.Max(1f, FranBalance.GetFloat(nameof(FranBalance.DiscPhantomTargetRange), FranBalance.DiscPhantomTargetRange));
        NPC target = discTarget >= 0 && discTarget < Main.maxNPCs ? Main.npc[discTarget] : null;
        if (target is null || !target.CanBeChasedBy() || target.DistanceSQ(discTargetOrigin) > range * range)
        {
            target = null;
            float bestDistance = range * range;
            foreach (NPC candidate in Main.ActiveNPCs)
            {
                float distance = candidate.DistanceSQ(discTargetOrigin);
                if (!candidate.CanBeChasedBy() || distance >= bestDistance) continue;
                bestDistance = distance;
                target = candidate;
            }
            discTarget = target?.whoAmI ?? -1;
        }
        if (target is null) return;
        Vector2 heading = (target.Center - Projectile.Center).SafeNormalize(InitialDirection);
        if (Vector2.DistanceSquared(heading, new Vector2(Projectile.ai[1], Projectile.ai[2])) > 0.0001f)
        {
            Projectile.ai[1] = heading.X;
            Projectile.ai[2] = heading.Y;
            Projectile.netUpdate = true;
        }
    }
    private bool Attached => attachedNpc >= 0;
    private Vector2 InitialDirection => new Vector2(Projectile.ai[1], Projectile.ai[2]).SafeNormalize(Vector2.UnitX);
    private bool FullLoadoutSpeed => Projectile.ai[0] >= FullLoadoutSpeedFlag;
    private float LaunchDelay => Math.Max(1f, Projectile.ai[0] -
        (FullLoadoutSpeed ? FullLoadoutSpeedFlag : 0f));

    public override string Texture => "Fran/Textures/师父飞剑-影分身弹";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 24;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
    }

    public override void SetDefaults()
    {
        Projectile.width = (int)MathF.Round(46f * BladeSize);
        Projectile.height = Projectile.width;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.extraUpdates = UpdatesPerFrame - 1;
        Projectile.timeLeft = 150 * UpdatesPerFrame;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override void OnSpawn(IEntitySource source) => formationOrigin = Projectile.Center;

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(formationOrigin.X);
        writer.Write(formationOrigin.Y);
        writer.Write(age);
        writer.Write(launched);
        writer.Write(flightLifetimeFrames);
        writer.Write((short)attachedNpc);
        writer.Write(attachedNpcType);
        writer.Write(attachmentOffset.X);
        writer.Write(attachmentOffset.Y);
        writer.Write(attachmentAge);
        writer.Write(impactHeading);
        writer.Write(discTargeting);
        writer.Write(discTargetOrigin.X);
        writer.Write(discTargetOrigin.Y);
        writer.Write(discTarget);
        writer.Write(discIdentity);
        writer.Write(discOffset.X);
        writer.Write(discOffset.Y);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        formationOrigin = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        age = reader.ReadSingle();
        launched = reader.ReadBoolean();
        flightLifetimeFrames = reader.ReadInt32();
        attachedNpc = reader.ReadInt16();
        attachedNpcType = reader.ReadInt32();
        attachmentOffset = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        attachmentAge = reader.ReadSingle();
        impactHeading = reader.ReadSingle();
        discTargeting = reader.ReadBoolean();
        discTargetOrigin = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        discTarget = reader.ReadInt32();
        discIdentity = reader.ReadInt32();
        discOffset = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        attachedEntity = Attached && attachedNpc < Main.maxNPCs ? Main.npc[attachedNpc] : null;
        if (Attached)
        {
            ApplyAttachedState();
            Projectile.timeLeft = Math.Max(8, (int)((AttachmentFrames - attachmentAge) * UpdatesPerFrame) + 8);
        }
        else
            Projectile.tileCollide = launched && age - LaunchDelay >= flightLifetimeFrames * TileGraceFraction;
    }

    public override bool ShouldUpdatePosition() => launched && !Attached && !detonated;
    public override bool? CanDamage() => launched && !Attached && !detonated ? null : false;
    public override bool? CanHitNPC(NPC target) => Attached || detonated ? false : null;

    public override void AI()
    {
        if (Attached)
        {
            UpdateAttachment();
            return;
        }
        if (!launched)
        {
            if (Projectile.numUpdates == 0)
            {
                FollowDiscFormation();
                UpdateDiscTarget();
            }
            Projectile.velocity = Vector2.Zero;
            // ai[1..2] 保存整组共用的单位方向，各剑从自己的位置平行出发。
            Vector2 forward = InitialDirection;
            Projectile.rotation = forward.ToRotation();
            Projectile.Opacity = 0.78f * MathHelper.Clamp(age / 5f, 0f, 1f);
            if (age >= LaunchDelay)
            {
                launched = true;
                Projectile.velocity = forward * (FlightSpeed(0f) / UpdatesPerFrame);
                flightLifetimeFrames = Math.Max(1, FranBalance.GetInt(nameof(FranBalance.MultipleBodyFlightFrames),
                    FranBalance.MultipleBodyFlightFrames));
                Projectile.timeLeft = flightLifetimeFrames * UpdatesPerFrame;
                Array.Fill(Projectile.oldPos, Projectile.position);
                Projectile.Opacity = 0.92f;
                Projectile.netUpdate = true;
            }
        }
        else
        {
            Projectile.velocity = InitialDirection * (FlightSpeed(age - LaunchDelay) / UpdatesPerFrame);
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.Opacity = 0.92f;
        }

        Projectile.tileCollide = launched && age - LaunchDelay >= flightLifetimeFrames * TileGraceFraction;
        // The grace period may end inside a thick wall. Do not let the blade escape through it.
        if (Projectile.tileCollide && Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height))
        {
            Detonate(Projectile.rotation);
            return;
        }
        age += 1f / UpdatesPerFrame;
        if (!Main.dedServ && Projectile.numUpdates == 0)
        {
            Lighting.AddLight(Projectile.Center, new Vector3(0.10f, 0.24f, 0.42f) * Projectile.Opacity *
                (launched ? FranPhantomVisuals.FlightBrightness : 1f));
            SpawnPhaseParticles();
        }
    }

    private float AccelerationFrames => Math.Max(1, FranBalance.GetInt(
        nameof(FranBalance.MultipleBodyAccelerationFrames), FranBalance.MultipleBodyAccelerationFrames));

    private float FlightSpeed(float time)
    {
        float progress = MathHelper.Clamp(time / AccelerationFrames, 0f, 1f);
        return DashSpeed() * (0.12f + 0.88f * progress * progress);
    }

    private float FlightDistance(float time)
    {
        float acceleration = AccelerationFrames;
        float t = MathHelper.Clamp(time, 0f, acceleration);
        return DashSpeed() * (0.12f * t + 0.88f * t * t * t / (3f * acceleration * acceleration) +
            Math.Max(0f, time - acceleration));
    }

    private void SpawnPhaseParticles()
    {
        Vector2 forward = InitialDirection;
        Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
        if (!launched)
        {
            if (discTargeting) return; // Disc clones condense on its rim; no detached portal field.
            if ((int)age % 3 != 0) return;
            Vector2 portal = formationOrigin - forward * 46f;
            for (int i = 0; i < 2; i++)
            {
                float phase = age * 0.20f + i * MathHelper.Pi + Projectile.identity * 0.6f;
                Vector2 position = FranPhantomVisuals.PortalPoint(portal, forward.ToRotation(), phase, 48f * FranPhantomVisuals.PortalScale(age));
                Vector2 velocity = (portal - position) * 0.045f + forward * 2.6f;
                FranParticleSystem.Spawn(new FranSparkParticle(position, velocity, FranPhantomVisuals.Cyan,
                    0.22f * FranPhantomVisuals.PortalScale(age), new Vector2(0.48f, 1.7f), 13), spawnSupportingDust: false);
            }
            return;
        }

        // 电灼长戟的李萨如点光 + 黄金角等离子体，按游戏帧发射而不是按四次子更新加倍。
        float time = age * 0.24f + Projectile.identity * 0.71f;
        Vector2 offset = forward * (MathF.Sin(time * 3f) * 9f) + normal * (MathF.Sin(time * 2f + 0.78f) * 18f);
        FranLightingBoltsSystem.SpawnBlueFlightLight(Projectile.Center + offset * FranPhantomVisuals.FlightSize, -forward * (0.8f * FranPhantomVisuals.FlightSize),
            FranPhantomVisuals.Cyan * FranPhantomVisuals.FlightBrightness, 0.9f * FranPhantomVisuals.FlightSize);
        if ((int)age % 2 != 0) return;
        for (int i = 0; i < 2; i++)
        {
            float phase = time + i * 2.399963f;
            Vector2 radial = forward * (MathF.Cos(phase) * 7f) + normal * (MathF.Sin(phase) * 24f);
            FranLightingBoltsSystem.SpawnBlueFlightLight(Projectile.Center + radial * FranPhantomVisuals.FlightSize,
                (-forward * 0.7f + normal * (MathF.Sin(phase) * 0.3f)) * FranPhantomVisuals.FlightSize,
                (i == 0 ? FranPhantomVisuals.Blue : FranPhantomVisuals.Cyan) * FranPhantomVisuals.FlightBrightness,
                0.7f * FranPhantomVisuals.FlightSize);
        }
    }

    private float DashSpeed() => Math.Max(16f,
        FranBalance.GetFloat(nameof(FranBalance.MultipleBodyDashSpeed), FranBalance.MultipleBodyDashSpeed) *
        (FullLoadoutSpeed ? FranBalance.GetFloat(nameof(FranBalance.MultipleBodyFullLoadoutSpeedMultiplier),
            FranBalance.MultipleBodyFullLoadoutSpeedMultiplier) : 1f));

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (Attached || detonated || Projectile.owner != Main.myPlayer) return;
        // Keep all existing damage on the first hit. The embedded blade and explosion are harmless.
        impactHeading = Projectile.velocity.ToRotation();
        Projectile.Center = new Vector2(MathHelper.Clamp(Projectile.Center.X, target.Hitbox.Left, target.Hitbox.Right),
            MathHelper.Clamp(Projectile.Center.Y, target.Hitbox.Top, target.Hitbox.Bottom));
        attachedNpc = target.whoAmI;
        attachedNpcType = target.netID;
        attachedEntity = target;
        attachmentOffset = Projectile.Center - target.Center;
        attachmentAge = 0f;
        ApplyAttachedState();
        Projectile.timeLeft = AttachmentFrames * UpdatesPerFrame + 8;
        Array.Fill(Projectile.oldPos, Projectile.position);
        Projectile.netUpdate = true;
        if (!Main.dedServ) SoundEngine.PlaySound(FranCombatSounds.MultipleBodyHit, Projectile.Center);
    }

    private void ApplyAttachedState()
    {
        Projectile.velocity = Vector2.Zero;
        Projectile.rotation = impactHeading;
        Projectile.friendly = false;
        Projectile.damage = 0;
        Projectile.tileCollide = false;
        Projectile.Opacity = 0.92f;
    }

    private void UpdateAttachment()
    {
        ApplyAttachedState();
        NPC target = attachedNpc < Main.maxNPCs ? Main.npc[attachedNpc] : null;
        if (target is null || !ReferenceEquals(target, attachedEntity) || !target.active ||
            target.life <= 0 || target.netID != attachedNpcType || target.dontTakeDamage)
        {
            Detonate(impactHeading);
            return;
        }
        Projectile.Center = target.Center + attachmentOffset;
        Projectile.gfxOffY = target.gfxOffY;
        attachmentAge += 1f / UpdatesPerFrame;
        if (attachmentAge >= AttachmentFrames)
            Detonate(impactHeading);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (!Attached && Projectile.tileCollide)
            Detonate(oldVelocity.ToRotation());
        return false;
    }

    private void Detonate(float heading)
    {
        // Only the owning simulation creates the networked, zero-damage visual and kills the blade.
        // Other peers wait for that update, so late NPC snapshots cannot trigger duplicate explosions.
        if (detonated || Projectile.owner != Main.myPlayer) return;
        detonated = true;
        Projectile.friendly = false;
        Projectile.damage = 0;
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
            ModContent.ProjectileType<FranPhantomMatrixImpact>(), 0, 0f, Projectile.owner,
            heading, Projectile.identity);
        Projectile.Kill();
    }

    internal static float EncodeLaunchDelay(float delay, bool fullLoadout) =>
        MathHelper.Clamp(delay, 1f, 60f) + (fullLoadout ? FullLoadoutSpeedFlag : 0f);

    private void DrawBlueSwordEchoes(Texture2D texture)
    {
        // SHPCKFast-style paired sword sprites along actual history, sampled across our 4 updates/frame.
        int historyCount = Math.Min(Projectile.oldPos.Length,
            (int)(Math.Max(0f, age - LaunchDelay) * UpdatesPerFrame));
        float cycle = Main.GlobalTimeWrappedHourly * 3f;
        int phase = (int)cycle % 3;
        Color first = phase == 0 ? Color.Cyan : phase == 1 ? Color.Blue : Color.LightBlue;
        Color next = phase == 0 ? Color.Blue : phase == 1 ? Color.LightBlue : Color.Cyan;
        Color tint = Color.Lerp(first, next, cycle % 1f);
        float scale = 0.68f * BladeSize * FranPhantomVisuals.FlightSize * FranShiFuVisuals.TextureScaleFor(texture);
        for (int sample = 7; sample >= 0; sample--)
        {
            int index = sample * 3 + 1;
            if (index >= historyCount || Projectile.oldPos[index] == Vector2.Zero) continue;
            float fade = 1f - (sample + 1f) / 9f;
            Color color = Color.Lerp(tint, Color.DeepSkyBlue, sample / 8f) *
                (0.40f * fade * Projectile.Opacity * FranPhantomVisuals.FlightBrightness);
            color.A = 0; // Additive sprite glow under Terraria's premultiplied alpha blend.
            Vector2 position = Vector2.Lerp(Projectile.Center, Projectile.oldPos[index] + Projectile.Size * 0.5f,
                FranPhantomVisuals.FlightSize) - Main.screenPosition;
            Main.EntitySpriteDraw(texture, position, null, color, Projectile.oldRot[index],
                texture.Size() * 0.5f, scale * 1.25f, SpriteEffects.None, 0f);
            Main.EntitySpriteDraw(texture, position, null, color * 0.7f, Projectile.oldRot[index],
                texture.Size() * 0.5f, scale * 1.25f * 0.7f, SpriteEffects.None, 0f);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        Vector2 direction = Attached ? impactHeading.ToRotationVector2() : launched ? Projectile.velocity.SafeNormalize(Vector2.UnitX) :
            InitialDirection;
        if (launched && !Attached)
        {
            // The renderer multiplies opacity into both shader and vertex alpha: the square root applies the requested brightness once.
            if (!FranProjectileTrailRenderer.TryDraw(Projectile, FranProjectileTrailProfile.SpectralBlade,
                new Color(10, 40, 100), FranPhantomVisuals.Blue, FranPhantomVisuals.Cyan, 46f * BladeSize * FranPhantomVisuals.FlightSize,
                0.95f * MathF.Sqrt(0.5f * FranPhantomVisuals.FlightBrightness), FranPhantomVisuals.FlightSize))
                FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.SpectralBlade,
                    FranPhantomVisuals.Blue, 46f * BladeSize * FranPhantomVisuals.FlightSize,
                    0.95f * 0.5f * FranPhantomVisuals.FlightBrightness, FranPhantomVisuals.FlightSize);
            DrawBlueSwordEchoes(texture);
            FranPhantomVisuals.DrawFlight(Projectile.Center, direction.ToRotation(), age,
                FlightDistance(Math.Max(0f, age - LaunchDelay)), Projectile.identity);
        }
        Vector2 portalCenter = discTargeting ? formationOrigin : formationOrigin - InitialDirection * 46f;
        if (!Attached && !discTargeting) FranPhantomVisuals.DrawPortal(portalCenter, InitialDirection.ToRotation(), age, LaunchDelay);
        float emergence = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(age / 9f, 0f, 1f));
        Vector2 drawCenter = launched ? Projectile.Center : Vector2.Lerp(portalCenter, formationOrigin, emergence);
        if (Attached) drawCenter += new Vector2(0f, Projectile.gfxOffY) - direction * (10f * BladeSize * SwordTextureSize);
        Main.EntitySpriteDraw(texture, drawCenter - Main.screenPosition, null,
            new Color(170, 215, 245) * Projectile.Opacity, Projectile.rotation,
            texture.Size() * 0.5f, 0.68f * BladeSize * SwordTextureSize * FranShiFuVisuals.TextureScaleFor(texture) * MathHelper.Lerp(0.16f, 1f, emergence), SpriteEffects.None, 0f);
        return false;
    }
}
