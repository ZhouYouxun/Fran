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
    private Vector2 formationOrigin;
    private float age;
    private bool launched;
    private int lockedTarget = -1;
    private float launchHeading;
    private Vector2 InitialDirection => (Destination - formationOrigin).SafeNormalize(Vector2.UnitX);
    private bool FullLoadoutSpeed => Projectile.ai[0] >= FullLoadoutSpeedFlag;
    private float LaunchDelay => Math.Max(1f, Projectile.ai[0] -
        (FullLoadoutSpeed ? FullLoadoutSpeedFlag : 0f));
    private Vector2 Destination => new(Projectile.ai[1], Projectile.ai[2]);

    public override string Texture => FranShiFuVisuals.TexturePath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 24;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1000;
    }

    public override void SetDefaults()
    {
        Projectile.width = 46;
        Projectile.height = 46;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 1;
        Projectile.extraUpdates = UpdatesPerFrame - 1;
        Projectile.timeLeft = 150 * UpdatesPerFrame;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
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
        writer.Write((short)lockedTarget);
        writer.Write(launchHeading);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        formationOrigin = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        age = reader.ReadSingle();
        launched = reader.ReadBoolean();
        lockedTarget = reader.ReadInt16();
        launchHeading = reader.ReadSingle();
    }

    public override bool ShouldUpdatePosition() => launched;
    public override bool? CanDamage() => launched ? null : false;

    public override void AI()
    {
        if (!launched)
        {
            Projectile.velocity = Vector2.Zero;
            // ai[1..2] 是生成时的鼠标快照，蓄势与门框不再随鼠标旋转。
            Vector2 forward = InitialDirection;
            Projectile.rotation = forward.ToRotation() + MathHelper.PiOver4;
            Projectile.Opacity = 0.78f * MathHelper.Clamp(age / 5f, 0f, 1f);
            if (age >= LaunchDelay)
            {
                launched = true;
                if (Projectile.owner == Main.myPlayer)
                    lockedTarget = SelectForwardTarget();
                if (ValidTarget())
                    forward = BoundedDirection(PredictTarget(Main.npc[lockedTarget]), InitialDirection.ToRotation(), 20f);
                launchHeading = forward.ToRotation();
                Projectile.velocity = forward * (FlightSpeed(0f) / UpdatesPerFrame);
                Projectile.timeLeft = Math.Max(1, FranBalance.GetInt(nameof(FranBalance.MultipleBodyFlightFrames),
                    FranBalance.MultipleBodyFlightFrames)) * UpdatesPerFrame;
                Array.Fill(Projectile.oldPos, Projectile.position);
                Projectile.Opacity = 0.92f;
                Projectile.netUpdate = true;
                if (!Main.dedServ)
                    SoundEngine.PlaySound(FranCombatSounds.MultipleBodyDashPrepare, Projectile.Center);
            }
        }
        else
        {
            // 只对起冲时选中的目标进行窄幅修正，越过目标后绝不掉头或换目标。
            if (ValidTarget())
            {
                Vector2 delta = Main.npc[lockedTarget].Center - Projectile.Center;
                if (Vector2.Dot(delta, Projectile.velocity) > 0f)
                {
                    Vector2 desired = BoundedDirection(PredictTarget(Main.npc[lockedTarget]), launchHeading, 6f);
                    desired = BoundedDirection(Projectile.Center + desired, InitialDirection.ToRotation(), 20f);
                    float current = Projectile.velocity.ToRotation();
                    float step = MathHelper.ToRadians(1.5f) / UpdatesPerFrame;
                    float turn = Math.Clamp(MathHelper.WrapAngle(desired.ToRotation() - current), -step, step);
                    Projectile.velocity = (current + turn).ToRotationVector2() * Projectile.velocity.Length();
                }
            }
            Projectile.velocity = Projectile.velocity.SafeNormalize(InitialDirection) *
                (FlightSpeed(age - LaunchDelay) / UpdatesPerFrame);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            Projectile.Opacity = 0.92f;
        }

        age += 1f / UpdatesPerFrame;
        if (!Main.dedServ && Projectile.numUpdates == 0)
        {
            Lighting.AddLight(Projectile.Center, new Vector3(0.10f, 0.24f, 0.42f) * Projectile.Opacity);
            SpawnPhaseParticles();
        }
    }

    private bool ValidTarget() => lockedTarget >= 0 && lockedTarget < Main.maxNPCs &&
        Main.npc[lockedTarget].CanBeChasedBy();

    private Vector2 BoundedDirection(Vector2 point, float heading, float degrees)
    {
        float desired = (point - Projectile.Center).SafeNormalize(heading.ToRotationVector2()).ToRotation();
        float limit = MathHelper.ToRadians(degrees);
        return (heading + Math.Clamp(MathHelper.WrapAngle(desired - heading), -limit, limit)).ToRotationVector2();
    }

    private int SelectForwardTarget()
    {
        int best = -1;
        float score = float.MaxValue;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy()) continue;
            Vector2 delta = npc.Center - Projectile.Center;
            float distance = delta.Length();
            if (distance < 1f || distance > 2400f) continue;
            float alignment = Vector2.Dot(delta / distance, InitialDirection);
            if (alignment < MathF.Cos(MathHelper.ToRadians(20f))) continue;
            float candidate = distance * (1f + (1f - alignment) * 4f);
            if (candidate < score) { score = candidate; best = npc.whoAmI; }
        }
        return best;
    }

    private Vector2 PredictTarget(NPC target)
    {
        // 使用加速曲线的路程积分求拦截时间，避免慢起步导致预判落后。
        float flightAge = Math.Max(0f, age - LaunchDelay);
        float startDistance = FlightDistance(flightAge);
        float low = 0f, high = 50f;
        for (int i = 0; i < 18; i++)
        {
            float middle = (low + high) * 0.5f;
            float distance = Vector2.Distance(Projectile.Center, target.Center + target.velocity * middle);
            if (FlightDistance(flightAge + middle) - startDistance < distance) low = middle;
            else high = middle;
        }
        return target.Center + target.velocity * ((low + high) * 0.5f);
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
        Vector2 forward = (launched ? Projectile.velocity : Destination - Projectile.Center).SafeNormalize(Vector2.UnitX);
        Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
        if (!launched)
        {
            if ((int)age % 3 != 0) return;
            Vector2 portal = formationOrigin - forward * 46f;
            for (int i = 0; i < 2; i++)
            {
                float phase = age * 0.20f + i * MathHelper.Pi + Projectile.identity * 0.6f;
                Vector2 position = FranPhantomVisuals.PortalPoint(portal, forward.ToRotation(), phase, 48f);
                Vector2 velocity = (portal - position) * 0.045f + forward * 2.6f;
                FranParticleSystem.Spawn(new FranSparkParticle(position, velocity, FranPhantomVisuals.Cyan,
                    0.22f, new Vector2(0.48f, 1.7f), 13), spawnSupportingDust: false);
            }
            return;
        }

        // 电灼长戟的李萨如点光 + 黄金角等离子体，按游戏帧发射而不是按四次子更新加倍。
        float time = age * 0.24f + Projectile.identity * 0.71f;
        Vector2 offset = forward * (MathF.Sin(time * 3f) * 9f) + normal * (MathF.Sin(time * 2f + 0.78f) * 18f);
        FranLightingBoltsSystem.SpawnBlueFlightLight(Projectile.Center + offset, -forward * 0.8f,
            FranPhantomVisuals.Cyan, 0.9f);
        if ((int)age % 2 != 0) return;
        for (int i = 0; i < 2; i++)
        {
            float phase = time + i * 2.399963f;
            Vector2 radial = forward * (MathF.Cos(phase) * 7f) + normal * (MathF.Sin(phase) * 24f);
            FranLightingBoltsSystem.SpawnBlueFlightLight(Projectile.Center + radial,
                -forward * 0.7f + normal * (MathF.Sin(phase) * 0.3f),
                i == 0 ? FranPhantomVisuals.Blue : FranPhantomVisuals.Cyan, 0.7f);
        }
    }

    private float DashSpeed() => Math.Max(16f,
        FranBalance.GetFloat(nameof(FranBalance.MultipleBodyDashSpeed), FranBalance.MultipleBodyDashSpeed) *
        (FullLoadoutSpeed ? FranBalance.GetFloat(nameof(FranBalance.MultipleBodyFullLoadoutSpeedMultiplier),
            FranBalance.MultipleBodyFullLoadoutSpeedMultiplier) : 1f));

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        SoundEngine.PlaySound(FranCombatSounds.MultipleBodyHit, Projectile.Center);
        // 切痕落在实际接触面，单次命中后立即销毁本体；爆炸只负责演出，不追加伤害。
        Projectile.Center = new Vector2(MathHelper.Clamp(Projectile.Center.X, target.Hitbox.Left, target.Hitbox.Right),
            MathHelper.Clamp(Projectile.Center.Y, target.Hitbox.Top, target.Hitbox.Bottom));
        if (Projectile.owner == Main.myPlayer)
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<FranPhantomMatrixImpact>(), 0, 0f, Projectile.owner,
                Projectile.velocity.ToRotation(), Projectile.identity);
        Projectile.Kill();
    }

    internal static float EncodeLaunchDelay(float delay, bool fullLoadout) =>
        MathHelper.Clamp(delay, 1f, 60f) + (fullLoadout ? FullLoadoutSpeedFlag : 0f);

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = FranShiFuVisuals.Texture;
        Vector2 direction = launched ? Projectile.velocity.SafeNormalize(Vector2.UnitX) :
            (Destination - Projectile.Center).SafeNormalize(Vector2.UnitX);
        if (launched)
        {
            if (!FranProjectileTrailRenderer.TryDraw(Projectile, FranProjectileTrailProfile.SpectralBlade,
                new Color(10, 40, 100), FranPhantomVisuals.Blue, FranPhantomVisuals.Cyan, 46f, 0.95f))
                FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.SpectralBlade,
                    FranPhantomVisuals.Blue, 46f, 0.95f);
            FranPhantomVisuals.DrawFlight(Projectile.Center, direction.ToRotation(), age,
                FlightDistance(Math.Max(0f, age - LaunchDelay)), Projectile.identity);
        }
        Vector2 portalCenter = formationOrigin - InitialDirection * 46f;
        FranPhantomVisuals.DrawPortal(portalCenter, InitialDirection.ToRotation(), age, LaunchDelay);
        float emergence = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(age / 9f, 0f, 1f));
        Vector2 drawCenter = launched ? Projectile.Center : Vector2.Lerp(portalCenter, formationOrigin, emergence);
        Main.EntitySpriteDraw(texture, drawCenter - Main.screenPosition, null,
            new Color(170, 215, 245) * Projectile.Opacity, Projectile.rotation,
            texture.Size() * 0.5f, 0.68f * FranShiFuVisuals.TextureScale * MathHelper.Lerp(0.16f, 1f, emergence), SpriteEffects.None, 0f);
        return false;
    }
}
