using System;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


public sealed class FranTeacherPhantomBlade : ModProjectile
{
    // 分身剑高速突进，接触目标后减速并逐步散去。
    private const float SeraphimInitialSpeed = 64f;
    private const float SeraphimOnHitSpeedFactor = 0.4f;
    private const float SeraphimSlowdownSpeed = 7f;
    private const int SeraphimSlowdownTime = 50;
    private const float DissipationMarker = 1000f;
    private static readonly float SeraphimSlowdownFactor = MathF.Pow(
        SeraphimSlowdownSpeed / SeraphimInitialSpeed, 1f / SeraphimSlowdownTime);
    private bool Dissipating => Projectile.ai[0] >= DissipationMarker;
    private float DissipationAge => Projectile.ai[0] - DissipationMarker;
    private bool HasInitialEnemy => !Dissipating && Projectile.ai[0] > 0f;
    private float StoredDelay => Math.Max(1f, MathF.Abs(Projectile.ai[0]));
    private float LaunchDelay => HasInitialEnemy ? StoredDelay : 1f;
    private float Age => Projectile.localAI[0];

    public override string Texture => FranTeacherVisuals.TexturePath;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 16;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 46;
        Projectile.height = 46;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 86;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 18;
    }

    public override bool? CanDamage() => !Dissipating && Age >= LaunchDelay ? null : false;

    public override void AI()
    {
        if (Dissipating)
        {
            UpdateDissipation();
            return;
        }

        float age = Projectile.localAI[0]++;
        Vector2 target = new(Projectile.ai[1], Projectile.ai[2]);
        if (age == 0f)
        {
            Projectile.localAI[1] = Projectile.Center.X;
            Projectile.localAI[2] = Projectile.Center.Y;
            Projectile.Opacity = 0f;
        }

        Vector2 formationOrigin = new(Projectile.localAI[1], Projectile.localAI[2]);
        Vector2 forward = (target - formationOrigin).SafeNormalize(Vector2.UnitX);
        if (age < LaunchDelay)
        {
            float reveal = FranMotion.Smooth(FranMotion.Segment(age, 1f, Math.Max(2f, LaunchDelay - 1f)));
            Projectile.Center = formationOrigin;
            Projectile.velocity = Vector2.Zero;
            Projectile.Opacity = reveal * 0.92f;
        }
        else
        {
            if (age == LaunchDelay)
            {
                Projectile.velocity = (target - Projectile.Center).SafeNormalize(forward) *
                    SeraphimInitialSpeed;
                Projectile.Opacity = 1f;
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.28f, Pitch = 0.24f }, Projectile.Center);
                FranParticleEffects.LineRelease(Projectile.Center - forward * 26f,
                    Projectile.Center + forward * 48f, new Color(225, 245, 255), 8, 0.64f);
            }

            float flightAge = age - LaunchDelay;
            Projectile.Opacity = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);

            Vector2 remaining = target - Projectile.Center;
            bool passedAimPoint = flightAge >= 1f && Vector2.Dot(remaining, Projectile.velocity) <= 0f;
            bool reachedAimPoint = passedAimPoint ||
                (!HasInitialEnemy && remaining.LengthSquared() <= 40f * 40f);
            if (flightAge >= 1f && reachedAimPoint)
            {
                BeginDissipation(false);
                return;
            }
        }

        Projectile.rotation = (age < LaunchDelay ? forward : Projectile.velocity.SafeNormalize(forward)).ToRotation() +
            MathHelper.PiOver4;
        Lighting.AddLight(Projectile.Center, 0.24f, 0.34f, 0.46f);
        if (age >= LaunchDelay)
            FranVanillaDustEffects.ProjectileTrail(Projectile.Center, Projectile.velocity,
                FranSkill.MultipleBodyCreation, new Color(225, 245, 255), 0.16f, 0.88f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (Dissipating)
            return;

        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        FranParticleEffects.MeleeImpact(target.Center, direction, new Color(185, 230, 255), 0);
        FranVanillaDustEffects.Impact(target.Center, direction, FranSkill.MultipleBodyCreation,
            new Color(225, 248, 255), 5, 0.82f);
        BeginDissipation(true);
    }

    private void BeginDissipation(bool contactedEnemy)
    {
        if (Dissipating)
            return;

        Projectile.velocity *= SeraphimOnHitSpeedFactor;
        Projectile.ai[0] = DissipationMarker;
        Projectile.friendly = false;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.timeLeft = SeraphimSlowdownTime + 2;
        Projectile.netUpdate = true;
        if (contactedEnemy)
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.22f, Pitch = 0.38f }, Projectile.Center);
    }

    private void UpdateDissipation()
    {
        float progress = MathHelper.Clamp(DissipationAge / SeraphimSlowdownTime, 0f, 1f);
        Projectile.velocity *= SeraphimSlowdownFactor;
        Projectile.rotation = Projectile.velocity.SafeNormalize(Vector2.UnitX).ToRotation() + MathHelper.PiOver4;
        Projectile.Opacity = MathF.Pow(1f - progress, 2f);

        float flashPulse = DissipationFlashPulse();
        float gatheredLight = (0.34f + progress * 0.82f) * (0.76f + flashPulse * 0.34f);
        Lighting.AddLight(Projectile.Center, new Vector3(0.08f, 0.36f, 0.78f) * gatheredLight);
        if (!Main.dedServ)
            SpawnDissipationSpiral();

        Projectile.ai[0]++;
        if (DissipationAge >= SeraphimSlowdownTime)
            Projectile.Kill();
    }

    private float DissipationFlashPulse()
    {
        float wave = MathF.Sin(DissipationAge * 1.42f + Projectile.identity * 0.73f) * 0.5f + 0.5f;
        return 0.68f + FranMotion.Smooth(wave) * 0.32f;
    }

    private void SpawnDissipationSpiral()
    {
        int tick = (int)DissipationAge;
        if (tick % 3 != 0)
            return;

        const float goldenAngle = 2.39996323f;
        float progress = MathHelper.Clamp(DissipationAge / SeraphimSlowdownTime, 0f, 1f);
        float radius = MathHelper.Lerp(7f, 31f, FranMotion.Smooth(progress));
        int count = tick % 9 == 0 ? 3 : 2;
        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        for (int i = 0; i < count; i++)
        {
            float sample = (i + 1f) / count;
            float angle = Projectile.identity * goldenAngle + tick * 0.31f + i * goldenAngle;
            Vector2 radial = angle.ToRotationVector2();
            Vector2 tangent = radial.RotatedBy(MathHelper.PiOver2);
            Vector2 position = Projectile.Center + radial * radius * MathF.Sqrt(sample);
            Vector2 velocity = tangent * (0.42f + sample * 0.52f) - forward * 0.28f;
            Color color = i == 0 ? Color.White : Color.Lerp(new Color(45, 145, 255),
                new Color(175, 235, 255), sample);
            FranParticleSystem.Spawn(new FranSparkParticle(position, velocity, color,
                0.075f + sample * 0.025f, new Vector2(0.48f, 1.32f), 11, 0.92f));
        }
    }

    public override void OnKill(int timeLeft)
    {
        if (!Dissipating || Main.dedServ)
            return;

        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        FranParticleEffects.PhantomBladeExplosion(Projectile.Center, direction, 0.92f);
        const int finalMoteCount = 6;
        float phase = Projectile.identity * 2.39996323f;
        for (int i = 0; i < finalMoteCount; i++)
        {
            Dust light = Dust.NewDustPerfect(Projectile.Center, DustID.RainbowMk2);
            light.color = Color.Lerp(new Color(35, 135, 255), Color.White, i / 5f);
            float angle = phase + MathHelper.TwoPi * i / finalMoteCount;
            light.velocity = angle.ToRotationVector2() * (i % 2 == 0 ? 4.8f : 3.2f);
            light.scale = 1.05f;
            light.noGravity = true;
        }
        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.34f, Pitch = 0.38f }, Projectile.Center);
        if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 1.8f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = FranTeacherVisuals.Texture;
        Vector2 origin = texture.Size() * 0.5f;
        if (!Dissipating && Age >= LaunchDelay && !FranProjectileTrailRenderer.TryDraw(Projectile,
                FranProjectileTrailProfile.SpectralBlade, new Color(18, 40, 62),
                new Color(145, 215, 255), Color.White, 21f, Projectile.Opacity * 0.82f))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.SpectralBlade,
                new Color(145, 215, 255), 24f, Projectile.Opacity);

        Vector2 direction = (!Dissipating && Age < LaunchDelay ?
            new Vector2(Projectile.ai[1], Projectile.ai[2]) - Projectile.Center :
            Projectile.velocity).SafeNormalize(Vector2.UnitX);
        if (Dissipating)
        {
            Texture2D ghostTexture = FranTeacherVisuals.GhostTexture;
            Vector2 ghostOrigin = ghostTexture.Size() * 0.5f;
            Vector2 baseDrawPosition = Projectile.Center - Main.screenPosition;
            float endFade = Utils.GetLerpValue(0f, 12f, Projectile.timeLeft, true);
            float drawScale = 0.68f * FranTeacherVisuals.TextureScale;
            float flashPulse = DissipationFlashPulse();
            float spreadCompletion = FranMotion.Smooth(FranMotion.Segment(
                DissipationAge, 0f, SeraphimSlowdownTime * 0.64f));
            float spreadRadius = spreadCompletion * 16f * FranTeacherVisuals.WorldSizeMultiplier;
            Color spreadColor = Color.Lerp(new Color(35, 125, 255),
                new Color(190, 235, 255), flashPulse) *
                (endFade * MathF.Sqrt(Math.Max(0f, Projectile.Opacity)) *
                MathHelper.Lerp(0.48f, 0.72f, flashPulse));
            Color trailingColor = Color.White * (Projectile.Opacity * endFade * 1.35f);

            const int orbitCopies = 9;
            const float goldenAngle = 2.39996323f;
            float orbitPhase = Projectile.identity * goldenAngle + DissipationAge * 0.045f;
            for (int i = 0; i < orbitCopies; i++)
            {
                float sample = (i + 1f) / orbitCopies;
                Vector2 offset = (orbitPhase + i * goldenAngle).ToRotationVector2() *
                    spreadRadius * MathF.Sqrt(sample);
                Main.EntitySpriteDraw(ghostTexture, baseDrawPosition + offset, null,
                    spreadColor * MathHelper.Lerp(0.46f, 0.92f, sample), Projectile.rotation,
                    ghostOrigin, drawScale,
                    SpriteEffects.None, 0f);
            }

            const int trailCopies = 7;
            Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
            float speedSpacing = MathHelper.Clamp(Projectile.velocity.Length() * 0.18f, 1.6f, 5.2f);
            float accumulatedDistance = 0f;
            for (int i = 0; i < trailCopies; i++)
            {
                accumulatedDistance += speedSpacing * MathF.Pow(1.34f, i);
                float completion = (i + 1f) / (trailCopies + 1f);
                float lateral = MathF.Sin(orbitPhase + i * goldenAngle) *
                    MathHelper.Lerp(0.5f, 2.4f, spreadCompletion);
                Vector2 drawPosition = baseDrawPosition - direction * accumulatedDistance + normal * lateral;
                float afterimageFade = MathF.Pow(1f - completion, 1.65f);
                Main.EntitySpriteDraw(ghostTexture, drawPosition, null,
                    trailingColor * afterimageFade, Projectile.rotation, ghostOrigin,
                    drawScale * MathHelper.Lerp(1f, 0.86f, completion), SpriteEffects.None, 0f);
            }

            Main.EntitySpriteDraw(texture, baseDrawPosition, null, trailingColor,
                Projectile.rotation, origin, drawScale, SpriteEffects.None, 0f);
            return false;
        }

        FranVfx.BeginAdditive();
        if (Age < LaunchDelay)
        {
            float reveal = FranMotion.Smooth(FranMotion.Segment(Age, 1f, Math.Max(2f, LaunchDelay * 0.72f)));
            float charge = FranMotion.Smooth(FranMotion.Segment(Age, LaunchDelay * 0.42f, LaunchDelay));
            FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
                new Color(175, 225, 255) * (0.24f * reveal + 0.38f * charge), -Projectile.rotation,
                Vector2.One * MathHelper.Lerp(52f, 38f, charge));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                Color.White * (0.16f * reveal + 0.68f * charge), direction.ToRotation(),
                new Vector2(42f + charge * 20f, 22f + charge * 10f));
        }
        else
        {
            FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 28f,
                new Color(220, 245, 255) * 0.58f, direction.ToRotation(), new Vector2(144f, 48f));
        }
        FranVfx.EndAdditive();

        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.Lerp(new Color(90, 135, 175), Color.White, 0.66f) * Projectile.Opacity,
            Projectile.rotation, origin, 0.68f * FranTeacherVisuals.TextureScale, SpriteEffects.None, 0f);
        return false;
    }

}
