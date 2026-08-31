using System;
using System.Collections.Generic;
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

internal static class TeacherImprintCombat
{
    public static void TriggerSwing(Player owner, Projectile swing, int combo, FranSkill skill,
        Vector2 direction, float reach)
    {
        if (Main.myPlayer != owner.whoAmI)
            return;

        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        FranVanillaDustEffects.SkillRelease(owner.MountedCenter + direction.SafeNormalize(Vector2.UnitX * owner.direction) * 26f,
            direction, skill, FranSkillInfo.Color(skill), 7 + combo * 2, combo == 3 ? 1.12f : 0.92f);
        switch (skill)
        {
            case FranSkill.TelekineticCatapult:
                Vector2 telekineticTarget = ClampTarget(owner, Main.MouseWorld, 2400f);
                if (combo == 3)
                    SpawnTelekineticLaser(owner, swing, telekineticTarget);
                else
                    SpawnTelekineticShells(owner, swing, telekineticTarget, combo + 2);
                if (Main.hardMode)
                    SpawnTelekineticPlanets(owner, swing, telekineticTarget, combo);
                break;

            case FranSkill.MultipleBodyCreation:
                SpawnMultipleBodies(owner, swing, direction, combo == 3 ? 4 : 2, combo);
                break;

            case FranSkill.ThunderChain:
                SpawnThunderVolley(owner, swing, combo);
                break;

            case FranSkill.ThorsHammer when combo == 3 && fran.AdvanceHammerRound():
                Vector2 hammerTarget = ClampTarget(owner, Main.MouseWorld, 720f);
                NPC hammerNpc = FindNearestTarget(hammerTarget, 720f);
                if (hammerNpc is not null)
                    hammerTarget = hammerNpc.Center;
                Projectile.NewProjectile(swing.GetSource_FromThis(), hammerTarget, Vector2.Zero,
                    ModContent.ProjectileType<ThorsHammerMarker>(), Math.Max(1, (int)(swing.damage * 1.18f)),
                    7f, owner.whoAmI, hammerNpc?.whoAmI ?? -1);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.58f, Pitch = -0.18f }, hammerTarget);
                break;
        }
    }

    public static void OnSwordHit(Player owner, Projectile swing, NPC target, int combo, FranSkill skill,
        Vector2 direction, int damageDone)
    {
        if (skill == FranSkill.None)
            return;

        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        FranImprintTargetGlobalNPC imprint = target.GetGlobalNPC<FranImprintTargetGlobalNPC>();
        if (skill == FranSkill.SkillTaker)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient && Main.myPlayer != owner.whoAmI)
                return;
            int stacks = imprint.AddSkillSeal(owner.whoAmI);
            if (!Main.dedServ)
            {
                for (int i = 0; i < 2 + stacks; i++)
                {
                    Vector2 velocity = Main.rand.NextVector2Circular(3.8f, 3.8f) - Vector2.UnitY * 1.2f;
                    FranParticleSystem.Spawn(new FranSkillGlyphParticle(target.Center, velocity,
                        new Color(210, 95, 255), 0.72f + stacks * 0.08f, Main.rand.Next(18, 26)));
                }
                if (stacks == 4)
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.36f, Pitch = 0.34f }, target.Center);
            }
            if (Main.myPlayer == owner.whoAmI)
            {
                FranBurst.Spawn(swing.GetSource_OnHit(target), target.Center, owner.whoAmI,
                    FranBurstStyle.SkillTaker, 0.72f + stacks * 0.08f);
                float randomAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 2; i++)
                {
                    float angle = randomAngle + i * MathHelper.Pi + Main.rand.NextFloat(-0.38f, 0.38f);
                    Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(7.5f, 10.5f);
                    Projectile.NewProjectile(swing.GetSource_OnHit(target), target.Center, velocity,
                        ModContent.ProjectileType<FranSkillTakerWisp>(),
                        Math.Max(1, (int)(swing.damage * (0.24f + stacks * 0.015f))),
                        swing.knockBack * 0.18f, owner.whoAmI, target.whoAmI, stacks);
                }
            }
            return;
        }
        if (Main.myPlayer != owner.whoAmI)
            return;

        switch (skill)
        {
            case FranSkill.BlueResonance:
                int echoCount = combo == 3 ? 3 : 1;
                for (int i = 0; i < echoCount; i++)
                {
                    float spread = (i - (echoCount - 1) * 0.5f) * 0.20f;
                    Projectile.NewProjectile(swing.GetSource_OnHit(target), target.Center,
                        direction.RotatedBy(spread), ModContent.ProjectileType<FranBlueResonanceEcho>(),
                        Math.Max(1, (int)(swing.damage * (combo == 3 ? 0.30f : 0.38f))),
                        swing.knockBack * 0.3f, owner.whoAmI, combo,
                        MathHelper.Clamp(target.width * 0.75f + 58f + i * 8f, 74f, 166f));
                }
                break;
        }
    }

    private static void SpawnTelekineticShells(Player owner, Projectile swing, Vector2 target, int count)
    {
        Vector2 direction = (target - owner.MountedCenter).SafeNormalize(Vector2.UnitX * owner.direction);
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        float centerIndex = (count - 1) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float lane = i - centerIndex;
            Vector2 side = normal * lane * 16f;
            float releaseDelay = i * 3f;
            Projectile.NewProjectile(swing.GetSource_FromThis(), owner.MountedCenter + direction * 30f + side,
                direction * 22f, ModContent.ProjectileType<FranTelekineticShell>(),
                Math.Max(1, (int)(swing.damage * (count switch { 2 => 0.42f, 3 => 0.31f, _ => 0.24f }))),
                swing.knockBack * 0.58f, owner.whoAmI, direction.X, direction.Y, releaseDelay);
        }
        FranSpectatorVfx.SpawnReleaseFan(swing.GetSource_FromThis(), owner.MountedCenter + direction * 28f,
            direction, owner.whoAmI, FranSpectatorPalette.Gold, Math.Min(4, count), 0.82f);
        owner.GetModPlayer<FranPlayer>().ShakeScreen(owner.Center, 0.8f + count * 0.13f);
    }

    private static void SpawnTelekineticLaser(Player owner, Projectile swing, Vector2 target)
    {
        Vector2 direction = (target - owner.MountedCenter).SafeNormalize(Vector2.UnitX * owner.direction);
        Projectile.NewProjectile(swing.GetSource_FromThis(), owner.MountedCenter + direction * 20f, direction,
            ModContent.ProjectileType<FranTelekineticLineStrike>(),
            Math.Max(1, (int)(swing.damage * 0.88f)), swing.knockBack * 0.72f,
            owner.whoAmI, 2200f, direction.ToRotation());
        FranSpectatorVfx.SpawnReleaseFan(swing.GetSource_FromThis(), owner.MountedCenter + direction * 32f,
            direction, owner.whoAmI, FranSpectatorPalette.Gold, 5, 1.12f);
        owner.GetModPlayer<FranPlayer>().ShakeScreen(owner.Center, 2.4f);
    }

    private static void SpawnTelekineticPlanets(Player owner, Projectile swing, Vector2 target, int combo)
    {
        int count = Main.rand.Next(2, 4);
        float centerIndex = (count - 1) * 0.5f;
        float focusX = MathHelper.Lerp(owner.Center.X, target.X, 0.58f);
        for (int i = 0; i < count; i++)
        {
            float lane = i - centerIndex;
            Vector2 origin = new(
                focusX + lane * Main.rand.NextFloat(86f, 112f) + Main.rand.NextFloat(-38f, 38f),
                owner.Center.Y - Main.rand.NextFloat(430f, 570f) - i * 46f);
            Vector2 velocity = (target - origin).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(14.5f, 18.5f);
            int variant = Main.rand.Next(10);
            Projectile.NewProjectile(swing.GetSource_FromThis(), origin, velocity,
                ModContent.ProjectileType<FranTelekineticPlanet>(),
                Math.Max(1, (int)(swing.damage * (combo == 3 ? 0.17f : 0.15f))),
                swing.knockBack * 0.46f, owner.whoAmI, variant);
        }

        FranParticleEffects.MysticGlyphs(owner.Center - Vector2.UnitY * 54f,
            new Color(178, 214, 255), count, 44f, 0.62f, -Vector2.UnitY * 0.25f);
    }

    private static void SpawnMultipleBodies(Player owner, Projectile swing, Vector2 direction, int count, int combo)
    {
        Vector2 requestedTarget = Main.MouseWorld;
        if (!float.IsFinite(requestedTarget.X) || !float.IsFinite(requestedTarget.Y))
            requestedTarget = owner.MountedCenter + direction.SafeNormalize(Vector2.UnitX * owner.direction) * 180f;
        NPC target = FindNearestTarget(requestedTarget, 430f);
        bool hasEnemy = target is not null;
        Vector2 lockedTarget = target?.Center ?? requestedTarget;
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        float centerIndex = (count - 1) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float formationIndex = i - centerIndex;
            Vector2 origin = owner.MountedCenter - direction * (72f + Math.Abs(formationIndex) * 13f) +
                normal * formationIndex * 48f - Vector2.UnitY * (20f + (i % 2) * 12f);
            float delay = 6f + i * 3f;
            Projectile.NewProjectile(swing.GetSource_FromThis(), origin, Vector2.Zero,
                ModContent.ProjectileType<FranTeacherPhantomBlade>(),
                Math.Max(1, (int)(swing.damage * (count == 4 ? 0.31f : 0.43f))),
                swing.knockBack * 0.48f, owner.whoAmI, hasEnemy ? delay : -delay,
                lockedTarget.X, lockedTarget.Y);
        }
    }

    private static void SpawnThunderVolley(Player owner, Projectile swing, int combo)
    {
        Vector2 mouseTarget = ClampTarget(owner, Main.MouseWorld, 780f);
        bool finisher = combo == 3;

        // Every volley has one unmistakable focal strike. Supporting bolts land first and frame it,
        // instead of presenting several equally important thin bolts around the cursor.
        SpawnThunderDrop(swing, owner, mouseTarget, 0.31f, mainStrike: true, finisher: finisher);

        int supportingBoltCount = finisher ? 2 : 1;
        for (int i = 0; i < supportingBoltCount; i++)
        {
            float horizontalOffset = supportingBoltCount == 1
                ? (combo % 2 == 0 ? -96f : 96f)
                : MathHelper.Lerp(-96f, 96f, i / (supportingBoltCount - 1f));
            Vector2 impact = mouseTarget + new Vector2(horizontalOffset,
                Main.rand.NextFloat(-14f, 28f));
            SpawnThunderDrop(swing, owner, impact, 0.31f, mainStrike: false, finisher: false);
        }

        List<NPC> candidates = new();
        foreach (NPC npc in Main.ActiveNPCs)
            if (npc.CanBeChasedBy() && Vector2.DistanceSquared(npc.Center, mouseTarget) <= 650f * 650f &&
                Vector2.DistanceSquared(npc.Center, owner.Center) <= 980f * 980f)
                candidates.Add(npc);
        candidates.Sort((left, right) =>
            Vector2.DistanceSquared(left.Center, mouseTarget).CompareTo(Vector2.DistanceSquared(right.Center, mouseTarget)));
        int trackedCount = Math.Min(combo == 3 ? 4 : 3, candidates.Count);
        for (int i = 0; i < trackedCount; i++)
            SpawnThunderDrop(swing, owner, candidates[i].Center, 0.37f, mainStrike: false, finisher: false);
    }

    private static void SpawnThunderDrop(Projectile swing, Player owner, Vector2 impact, float damageScale,
        bool mainStrike, bool finisher)
    {
        Vector2 spawn = impact + new Vector2(Main.rand.NextFloat(-130f, 130f), Main.rand.NextFloat(-780f, -620f));
        Vector2 velocity = (impact - spawn).SafeNormalize(Vector2.UnitY) * 12.5f;
        int index = Projectile.NewProjectile(swing.GetSource_FromThis(), spawn, velocity,
            ProjectileID.CultistBossLightningOrbArc, Math.Max(1, (int)(swing.damage * damageScale)),
            0f, owner.whoAmI, MathHelper.PiOver2, Main.rand.Next(100));
        if (Main.projectile.IndexInRange(index))
        {
            Projectile arc = Main.projectile[index];
            arc.friendly = true;
            arc.hostile = false;
            arc.DamageType = DamageClass.Melee;
            arc.penetrate = -1;
            arc.tileCollide = false;
            arc.usesLocalNPCImmunity = true;
            arc.localNPCHitCooldown = 60;
            arc.scale = mainStrike ? (finisher ? 1.6f : 1.35f) : 0.72f;
            arc.netUpdate = true;
        }
        float visualSeed = Main.rand.NextFloat(100f) + (mainStrike ? (finisher ? 2000f : 1000f) : 0f);
        Projectile.NewProjectile(swing.GetSource_FromThis(), impact, Vector2.Zero,
            ModContent.ProjectileType<FranThunderDropVisual>(), 0, 0f, owner.whoAmI,
            spawn.X, spawn.Y, visualSeed);
    }

    private static NPC FindNearestTarget(Vector2 center, float distance)
    {
        NPC best = null;
        float bestDistance = distance * distance;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy())
                continue;
            float candidate = Vector2.DistanceSquared(center, npc.Center);
            if (candidate >= bestDistance)
                continue;
            bestDistance = candidate;
            best = npc;
        }
        return best;
    }

    private static Vector2 ClampTarget(Player owner, Vector2 target, float maxDistance = 560f)
    {
        Vector2 offset = target - owner.MountedCenter;
        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y))
            return owner.MountedCenter;
        if (offset.LengthSquared() > maxDistance * maxDistance)
            offset = offset.SafeNormalize(Vector2.UnitX * owner.direction) * maxDistance;
        return owner.MountedCenter + offset;
    }
}

public sealed class FranImprintTargetGlobalNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;
    private int skillSealOwner = -1;
    private int skillSeals;
    private int skillSealTimer;

    public int AddSkillSeal(int owner)
    {
        if (skillSealOwner != owner || skillSealTimer <= 0)
            skillSeals = 0;
        skillSealOwner = owner;
        skillSeals = Math.Min(4, skillSeals + 1);
        skillSealTimer = 300;
        return skillSeals;
    }

    public float SkillTakerDamageMultiplier(int owner) =>
        skillSealOwner == owner && skillSealTimer > 0 ? 1f + skillSeals * 0.10f : 1f;

    public override void PostAI(NPC npc)
    {
        if (skillSealTimer > 0)
            skillSealTimer--;
    }

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (skillSealTimer <= 0)
            return;
        FranVfx.BeginAdditive();
        if (skillSealTimer > 0)
        {
            float radius = Math.Max(npc.width, npc.height) * 0.62f + 9f + skillSeals * 4f;
            FranVfx.DrawRing(npc.Center, new Color(210, 95, 255), radius, 0.16f + skillSeals * 0.07f,
                Main.GlobalTimeWrappedHourly * 0.8f, "Cal_FadedStarRing");
        }
        FranVfx.EndAdditive();
    }
}

public sealed class FranTelekineticShell : ModProjectile
{
    private float Age => Projectile.localAI[0];
    private float ReleaseDelay => MathHelper.Clamp(Projectile.ai[2], 0f, 12f);

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 20;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 26; Projectile.height = 26; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = 2; Projectile.timeLeft = 96;
        Projectile.tileCollide = false; Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 16;
    }

    public override bool? CanDamage() => Age > ReleaseDelay ? null : false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        Vector2 direction = new Vector2(Projectile.ai[0], Projectile.ai[1]).SafeNormalize(
            Projectile.velocity.SafeNormalize(Vector2.UnitX));
        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y))
        {
            Projectile.Kill();
            return;
        }

        if (age < ReleaseDelay)
        {
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = direction.ToRotation();
            Projectile.Opacity = MathHelper.Lerp(0.25f, 0.82f,
                FranMotion.Smooth(FranMotion.Segment(age, 0f, Math.Max(1f, ReleaseDelay))));
            Lighting.AddLight(Projectile.Center, 0.06f, 0.18f, 0.30f);
            return;
        }

        float flightAge = age - ReleaseDelay;
        float speed = MathHelper.Lerp(22f, 30f, FranMotion.Smooth(FranMotion.Segment(flightAge, 0f, 18f)));
        Projectile.velocity = direction * speed;
        Projectile.rotation = direction.ToRotation();
        Projectile.Opacity = 1f;
        Lighting.AddLight(Projectile.Center, 0.14f, 0.48f, 0.72f);
        FranVanillaDustEffects.ProjectileTrail(Projectile.Center, Projectile.velocity,
            FranSkill.TelekineticCatapult, new Color(115, 220, 255), 0.18f, 0.92f);
        if (!Main.dedServ && Projectile.timeLeft % 2 == 0)
        {
            FranParticleSystem.Spawn(new FranSkillGlyphParticle(Projectile.Center,
                -Projectile.velocity * 0.055f + Main.rand.NextVector2Circular(0.9f, 0.9f),
                new Color(130, 225, 255), 0.54f, 15));
            if (Projectile.timeLeft % 4 == 0)
                FranParticleSystem.Spawn(new FranGlowOrbParticle(Projectile.Center + Main.rand.NextVector2Circular(7f, 7f),
                    -Projectile.velocity * 0.035f, new Color(105, 215, 255), 0.34f, 0.06f, 13));
        }
    }
    public override void OnKill(int timeLeft)
    {
        SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.30f, Pitch = 0.34f }, Projectile.Center);
        FranParticleEffects.SpawnImpact(Projectile.Center, Projectile.velocity, new Color(130, 225, 255), 12, 6.4f, 1.02f);
        FranParticleEffects.BloomPulse(Projectile.Center, new Color(95, 210, 255), 12f, 82f, 15, 0.84f, true);
        FranParticleEffects.RingPulse(Projectile.Center, new Color(145, 235, 255), Vector2.One * 14f,
            Vector2.One * 92f, 15, Projectile.velocity.ToRotation(), 0.025f, 0.86f);
        FranParticleEffects.MysticGlyphs(Projectile.Center, new Color(115, 220, 255), 2, 31f, 0.78f,
            -Projectile.velocity * 0.025f);
        FranBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center, Projectile.owner,
            FranBurstStyle.Telekinetic, 0.76f);
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        Color color = new(115, 225, 255);
        FranParticleEffects.SpawnImpact(target.Center, Projectile.velocity, color, 8, 6.8f, 0.82f);
        FranParticleEffects.NeedleFan(target.Center, Projectile.velocity, color, 5, 6.6f, 0.72f, 0.62f);
        FranParticleSystem.Spawn(new FranDirectionalRingParticle(target.Center,
            Projectile.velocity.SafeNormalize(Vector2.UnitX) * 0.45f, color,
            new Vector2(0.06f, 0.12f), new Vector2(0.64f, 0.26f),
            Projectile.velocity.ToRotation(), 13));
        if (Projectile.numHits == 0)
            SoundEngine.PlaySound(SoundID.Item91 with { Volume = 0.18f, Pitch = 0.48f }, target.Center);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float pulse = 0.88f + MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity) * 0.12f;
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float bodyRotation = direction.ToRotation();
        if (!FranProjectileTrailRenderer.TryDraw(Projectile, FranProjectileTrailProfile.TelekineticNeedle,
                new Color(7, 39, 65), new Color(65, 185, 255), Color.White, 17f, 0.92f))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.TelekineticNeedle,
                new Color(65, 185, 255), 20f, 0.86f);

        // A readable authored body survives on bright backgrounds; emission is layered separately below.
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center,
            new Color(120, 220, 255) * 0.88f, bodyRotation, new Vector2(74f, 36f) * pulse);
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center - direction * 3f,
            new Color(65, 185, 255) * 0.42f, bodyRotation, new Vector2(84f, 43f) * pulse);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center + direction * 28f,
            Color.White * 0.68f, bodyRotation, new Vector2(30f, 18f) * pulse);
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 29f,
            new Color(80, 205, 255) * 0.34f, bodyRotation, new Vector2(88f, 24f));
        FranVfx.DrawBloom(Projectile.Center + direction * 11f, new Color(95, 205, 255), 34f, 0.46f);
        FranVfx.EndAdditive();
        return false;
    }

}

public sealed class FranTelekineticLineStrike : ModProjectile
{
    private const int Lifetime = 22;
    private float Age => Lifetime - Projectile.timeLeft;
    private float BeamLength => MathHelper.Clamp(Projectile.ai[0], 480f, 2400f);

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2500;

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Age is >= 5f and <= 11f ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center + direction * 12f, Projectile.Center + direction * BeamLength,
            22f, ref collisionPoint);
    }

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }

        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
        Projectile.velocity = direction;
        Projectile.Center = owner.MountedCenter + direction * 20f;
        Projectile.rotation = direction.ToRotation();
        Projectile.Opacity = FranMotion.Pulse(Age, 0f, 4f, 11f, Lifetime);

        if (Age == 0f)
        {
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.64f, Pitch = 0.26f }, Projectile.Center);
            FranParticleEffects.Implosion(Projectile.Center + direction * 18f, new Color(125, 225, 255), 0.82f);
            FranParticleEffects.BloomPulse(Projectile.Center + direction * 18f, new Color(95, 210, 255),
                18f, 118f, 18, 0.92f, true);
            FranParticleEffects.RingPulse(Projectile.Center + direction * 18f, new Color(155, 238, 255),
                new Vector2(18f, 28f), new Vector2(128f, 176f), 17,
                direction.ToRotation(), 0.022f, 0.92f);
            FranParticleEffects.MysticGlyphs(Projectile.Center + direction * 24f,
                new Color(135, 225, 255), 4, 42f, 0.92f, -direction * 0.32f);
            FranParticleEffects.LineRelease(Projectile.Center, Projectile.Center + direction * 170f,
                new Color(155, 235, 255), 14, 1.08f);
            FranVanillaDustEffects.SkillRelease(Projectile.Center + direction * 18f, direction,
                FranSkill.TelekineticCatapult, new Color(135, 225, 255), 14, 1.08f);
        }

        Lighting.AddLight(Projectile.Center + direction * 60f, 0.22f, 0.62f, 0.82f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        FranParticleEffects.SpawnImpact(target.Center, direction, new Color(120, 225, 255),
            7, 7.2f, 0.86f);
        FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_OnHit(target), target.Center, direction,
            Projectile.owner, FranSpectatorPalette.Gold, 3, 0.82f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 start = Projectile.Center + direction * 10f;
        Vector2 end = Projectile.Center + direction * BeamLength;
        float telegraph = 1f - FranMotion.Smooth(FranMotion.Segment(Age, 0f, 4f));
        float release = FranMotion.Pulse(Age, 3f, 5f, 10f, Lifetime);
        float width = 3f + release * 17f;

        FranVfx.BeginAdditive();
        int segments = Math.Max(1, (int)MathF.Ceiling(BeamLength / 760f));
        for (int i = 0; i < segments; i++)
        {
            Vector2 segmentStart = Vector2.Lerp(start, end, i / (float)segments);
            Vector2 segmentEnd = Vector2.Lerp(start, end, (i + 1f) / segments);
            FranVfx.DrawVertical("Cal_BloomLineSoftEdge", segmentStart, segmentEnd,
                new Color(65, 185, 255) * (0.42f * release + 0.12f * telegraph), width * 2.6f);
            FranVfx.DrawVertical("Cal_ThinEndedLine", segmentStart, segmentEnd,
                new Color(155, 235, 255) * (0.66f * release + 0.32f * telegraph), width * 0.72f);
            FranVfx.DrawVertical("Cal_ThinEndedLine", segmentStart, segmentEnd,
                Color.White * (0.88f * release + 0.36f * telegraph), Math.Max(1.3f, width * 0.20f));
        }
        FranVfx.DrawOriginalPixels("FranSmearForward", start - direction * 16f,
            new Color(120, 225, 255) * (0.56f * release), direction.ToRotation(),
            new Vector2(172f, 62f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", start, Color.White * (0.92f * release),
            direction.ToRotation(), new Vector2(104f, 54f));
        FranVfx.DrawOriginalPixels("FranPressureNeedle", end - direction * 42f,
            new Color(145, 235, 255) * (0.58f * release), direction.ToRotation(),
            new Vector2(118f, 34f));
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranRicochetLightning : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 18;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 18; Projectile.height = 18; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = 4; Projectile.timeLeft = 76;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 12;
    }
    public override void AI()
    {
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (Projectile.localAI[0]++ > 7f && Projectile.localAI[0] < 28f)
        {
            NPC target = FindBounceTarget(-1, 360f);
            if (target is not null)
            {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity.SafeNormalize(Vector2.UnitX)) * 19f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.075f);
            }
        }
        Lighting.AddLight(Projectile.Center, 0.18f, 0.14f, 0.58f);
        if (!Main.dedServ && Main.rand.NextBool(2))
            FranParticleSystem.Spawn(new FranBarrierShardParticle(Projectile.Center,
                -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(1.2f, 1.2f),
                new Color(155, 125, 255), new Vector2(12f, 4f), 10));
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranLightingBoltsSystem.SpawnThunderImpact(target.Center, new Color(155, 125, 255), 0.72f);
        SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.33f, Pitch = 0.25f + Projectile.ai[0] * 0.08f }, target.Center);
        Projectile.ai[0]++;
        NPC next = FindBounceTarget(target.whoAmI, 470f);
        if (Projectile.ai[0] >= 4f || next is null)
        {
            Projectile.Kill();
            return;
        }
        Projectile.Center = target.Center;
        Projectile.velocity = (next.Center - target.Center).SafeNormalize(Vector2.UnitX) * 20f;
        Projectile.damage = Math.Max(1, (int)(Projectile.damage * 0.82f));
        Projectile.localAI[0] = 0f;
        Projectile.netUpdate = true;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        FranVfx.BeginAdditive();
        Vector2 previous = Projectile.Center;
        for (int i = 0; i < Projectile.oldPos.Length; i++)
        {
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            if (point == Projectile.Size * 0.5f || Vector2.DistanceSquared(previous, point) > 90f * 90f)
                continue;
            float fade = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawTexturedLightning(previous, point, new Color(155, 125, 255),
                2.2f + fade * 2.8f, 3, Projectile.identity + i * 0.71f, fade * 0.74f);
            previous = point;
        }
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * 0.78f,
            Projectile.rotation, new Vector2(42f, 24f));
        FranVfx.EndAdditive();
        return false;
    }

    private NPC FindBounceTarget(int excluded, float distance)
    {
        NPC best = null;
        float bestDistance = distance * distance;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.whoAmI == excluded || !npc.CanBeChasedBy() || Projectile.localNPCImmunity[npc.whoAmI] > 0)
                continue;
            float candidate = Vector2.DistanceSquared(Projectile.Center, npc.Center);
            if (candidate < bestDistance)
            {
                bestDistance = candidate;
                best = npc;
            }
        }
        return best;
    }
}

public sealed class FranCompressionNode : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 50; Projectile.height = 50; Projectile.timeLeft = 180;
        Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        Projectile.rotation += 0.035f + Projectile.ai[0] * 0.008f;
        Lighting.AddLight(Projectile.Center, 0.10f, 0.31f, 0.42f);
        if (!Main.dedServ && Projectile.timeLeft % 7 == 0)
            FranParticleSystem.Spawn(new FranResonanceNoteParticle(Projectile.Center,
                new Color(210, 250, 255), 28f + Projectile.ai[0] * 5f, 18, Projectile.rotation));
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float fade = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
        float pulse = 0.5f + MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + Projectile.identity) * 0.5f;
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(155, 225, 245) * (0.64f * fade), Projectile.rotation * 0.55f,
            Vector2.One * (58f + pulse * 10f));
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(210, 250, 255) * (0.42f * fade), -Projectile.rotation * 1.2f,
            Vector2.One * (70f + pulse * 12f));
        Vector2 axis = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        FranVfx.DrawTexturedSpark(Projectile.Center, axis, Color.White, 48f, 3.2f, 0.68f * fade);
        FranVfx.EndAdditive();
        return false;
    }

    public static void Place(Player owner, Terraria.DataStructures.IEntitySource source, Vector2 position,
        Vector2 direction, int stage)
    {
        int type = ModContent.ProjectileType<FranCompressionNode>();
        Projectile oldest = null;
        int count = 0;
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner != owner.whoAmI || projectile.type != type)
                continue;
            count++;
            if (oldest is null || projectile.timeLeft < oldest.timeLeft)
                oldest = projectile;
        }
        if (count >= 3)
            oldest?.Kill();
        Projectile.NewProjectile(source, position, direction.SafeNormalize(Vector2.UnitX * owner.direction),
            type, 0, 0f, owner.whoAmI, stage);
        SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.25f, Pitch = 0.45f + stage * 0.08f }, position);
    }

    public static int DetonateAll(Player owner, int damage, int pressure)
    {
        int count = 0;
        int type = ModContent.ProjectileType<FranCompressionNode>();
        foreach (Projectile node in Main.ActiveProjectiles)
        {
            if (node.owner != owner.whoAmI || node.type != type)
                continue;
            Vector2 direction = node.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
            Projectile.NewProjectile(node.GetSource_FromThis(), node.Center - direction * 42f, direction,
                ModContent.ProjectileType<FranCompressedCut>(),
                Math.Max(1, (int)(damage * (0.38f + pressure * 0.12f))), 4f,
                owner.whoAmI, pressure);
            FranParticleEffects.Compression(node.Center, direction, new Color(210, 250, 255), 0.82f, true);
            node.Kill();
            count++;
        }
        return count;
    }

    public static void DismissAll(Player owner)
    {
        int type = ModContent.ProjectileType<FranCompressionNode>();
        foreach (Projectile node in Main.ActiveProjectiles)
            if (node.owner == owner.whoAmI && node.type == type)
                node.Kill();
    }
}

public sealed class FranTelekineticReturnBlade : ModProjectile
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.BreakerBlade}";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 12;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 42; Projectile.height = 42; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 70;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 12;
    }
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }
        float age = Projectile.localAI[0]++;
        if (age >= 17f)
        {
            Vector2 toOwner = owner.MountedCenter - Projectile.Center;
            if (toOwner.LengthSquared() < 28f * 28f) { Projectile.Kill(); return; }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity,
                toOwner.SafeNormalize(Vector2.UnitX * owner.direction) * 25f, 0.19f);
        }
        else if (age < 6f)
            Projectile.velocity *= 1.035f;
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        Lighting.AddLight(Projectile.Center, 0.12f, 0.34f, 0.54f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        FranVfx.BeginAdditive();
        for (int i = 1; i < Projectile.oldPos.Length; i++)
        {
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            if (point == Projectile.Size * 0.5f)
                continue;
            float fade = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawVertical("TraceLong", point - direction * 22f, point + direction * 4f,
                new Color(115, 225, 255) * (fade * 0.25f), 7f + fade * 8f);
        }
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 24f,
            new Color(120, 220, 255) * 0.44f, direction.ToRotation(), new Vector2(124f, 38f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center + direction * 14f,
            Color.White * 0.74f, direction.ToRotation(), new Vector2(48f, 28f));
        FranVfx.EndAdditive();
        Texture2D texture = TextureAssets.Item[ItemID.BreakerBlade].Value;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.Lerp(lightColor, Color.White, 0.26f), Projectile.rotation, texture.Size() * 0.5f,
            0.82f, SpriteEffects.None, 0f);
        return false;
    }
}

public sealed class FranSkillSealDetonation : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 116; Projectile.height = 116; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 18;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool? CanDamage() => Projectile.timeLeft is <= 16 and >= 11 ? null : false;
    public override void AI()
    {
        if (Projectile.localAI[0]++ == 0f)
        {
            SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.52f, Pitch = 0.24f }, Projectile.Center);
            FranParticleEffects.Implosion(Projectile.Center, new Color(210, 95, 255), 0.62f + Projectile.ai[0] * 0.09f);
            float sealScale = 0.68f + Projectile.ai[0] * 0.10f;
            FranParticleEffects.BlackLight(Projectile.Center, new Color(195, 72, 255), sealScale, 21);
            FranParticleEffects.MysticGlyphs(Projectile.Center, new Color(220, 120, 255),
                3 + (int)Projectile.ai[0], 42f + Projectile.ai[0] * 5f, sealScale);
            FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_FromThis(), Projectile.Center,
                Projectile.ai[2].ToRotationVector2(), Projectile.owner, FranSpectatorPalette.Purple,
                3 + (int)Projectile.ai[0], 0.82f + Projectile.ai[0] * 0.08f);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center,
                1.8f + Projectile.ai[0] * 0.22f);
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float progress = 1f - Projectile.timeLeft / 18f;
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        float radius = MathHelper.Lerp(14f, 74f + Projectile.ai[0] * 8f, FranMotion.EaseOutCubic(progress));
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(185, 70, 255) * (opacity * 0.62f), progress * 1.8f,
            Vector2.One * radius * 1.72f);
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            new Color(220, 125, 255) * (opacity * 0.72f), Projectile.ai[2],
            new Vector2(radius * 2.2f, radius * 1.34f));
        for (int i = 0; i < 3 + (int)Projectile.ai[0]; i++)
        {
            float angle = i * MathHelper.Pi / Math.Max(1, 2 + Projectile.ai[0]) + Projectile.rotation;
            Vector2 axis = angle.ToRotationVector2();
            FranVfx.DrawVertical("Cal_ThinEndedLine", Projectile.Center - axis * radius * 0.72f,
                Projectile.Center + axis * radius * 0.72f, Color.White * (opacity * 0.62f), 4f);
        }
        FranVfx.DrawBloom(Projectile.Center, new Color(220, 120, 255), 42f, opacity * 0.5f);
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranAutoBarrierProjectile : ModProjectile
{
    private const float MaxIntegrity = 5f;
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 104; Projectile.height = 136; Projectile.timeLeft = 3600;
        Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.netImportant = true;
        Projectile.ai[1] = MaxIntegrity;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }
        if (Projectile.localAI[1] == 0f)
        {
            Projectile.localAI[1] = 1f;
            if (Projectile.ai[1] <= 0f)
                Projectile.ai[1] = MaxIntegrity;
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.46f, Pitch = 0.32f }, Projectile.Center);
            EmitShards(Projectile.Center, new Color(95, 190, 255), 8, true);
            FranParticleEffects.RingPulse(Projectile.Center, new Color(105, 205, 255), Vector2.One * 34f,
                new Vector2(126f, 162f), 22, 0f, 0.024f, 0.72f);
            FranParticleEffects.MysticGlyphs(Projectile.Center, new Color(125, 215, 255), 5, 54f, 0.82f);
            FranVanillaDustEffects.SkillRelease(Projectile.Center, -Vector2.UnitY,
                FranSkill.MagicBarrier, new Color(105, 205, 255), 13, 1f);
        }
        if (owner.whoAmI == Main.myPlayer && owner.GetModPlayer<FranPlayer>().SelectedSkill != FranSkill.MagicBarrier)
        {
            Projectile.localAI[2] = 2f;
            Projectile.Kill();
            return;
        }
        Projectile.Center = owner.MountedCenter;
        Projectile.rotation += 0.012f;
        Projectile.timeLeft = 2;
        Projectile.ai[0]++;
        Projectile.ai[2] = Math.Max(0f, Projectile.ai[2] - 0.075f);
        Lighting.AddLight(Projectile.Center, 0.10f + Projectile.ai[2] * 0.18f,
            0.28f + Projectile.ai[2] * 0.22f, 0.48f + Projectile.ai[2] * 0.26f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float integrity = MathHelper.Clamp(Projectile.ai[1] / MaxIntegrity, 0f, 1f);
        float hitFlash = MathHelper.Clamp(Projectile.ai[2], 0f, 1f);
        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5.4f + Projectile.identity);
        if (!FranBarrierRenderer.TryDraw(Projectile.Center, integrity, hitFlash, Projectile.rotation))
            FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
                new Color(55, 145, 225) * (0.26f + integrity * 0.28f), -Projectile.rotation,
                new Vector2(126f, 158f) * (1f + hitFlash * 0.08f));

        // A restrained alpha body keeps the shield readable on bright backgrounds while the shader
        // supplies the moving spherical field and Fresnel edge.
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(52, 126, 202) * (0.13f + integrity * 0.16f), -Projectile.rotation * 1.3f,
            new Vector2(104f, 132f) * (1f + hitFlash * 0.08f));
        FranVfx.BeginAdditive();
        Color edgeColor = Color.Lerp(new Color(80, 165, 255), Color.White, hitFlash * 0.72f);
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(105, 205, 255) * (0.16f + integrity * 0.18f + hitFlash * 0.18f),
            Projectile.rotation * 1.8f, new Vector2(116f, 146f) * (1f + pulse * 0.025f));
        FranVfx.DrawRing(Projectile.Center, edgeColor, 48f + pulse * 4f + hitFlash * 9f,
            0.18f + integrity * 0.18f, Projectile.rotation, "Cal_HighResHollowCircle");
        for (int i = 0; i < (int)Projectile.ai[1]; i++)
        {
            float angle = -MathHelper.PiOver2 + (i - 2f) * 0.31f;
            Vector2 node = Projectile.Center + angle.ToRotationVector2() * (52f + pulse * 2f);
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", node,
                Color.Lerp(new Color(115, 215, 255), Color.White, 0.62f) * (0.52f + hitFlash * 0.34f),
                -angle, new Vector2(15f, 9f) * (1f + hitFlash * 0.24f));
        }
        FranVfx.DrawBloom(Projectile.Center, edgeColor, 34f + hitFlash * 32f,
            0.12f + hitFlash * 0.48f);
        if (hitFlash > 0.02f)
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * hitFlash,
                Projectile.rotation, new Vector2(136f, 82f) * (0.82f + hitFlash * 0.18f));
        FranVfx.EndAdditive();
        return false;
    }

    public override void OnKill(int timeLeft)
    {
        bool broken = Projectile.localAI[2] == 1f || Projectile.ai[1] <= 0f;
        Color color = broken ? new Color(105, 185, 255) : new Color(85, 145, 205);
        SoundEngine.PlaySound((broken ? SoundID.Item122 : SoundID.Item27) with
            { Volume = broken ? 0.62f : 0.30f, Pitch = broken ? -0.12f : 0.42f }, Projectile.Center);
        EmitShards(Projectile.Center, color, broken ? 18 : 7, false);
        FranParticleEffects.RingPulse(Projectile.Center, color, new Vector2(72f, 92f),
            new Vector2(broken ? 184f : 126f, broken ? 218f : 158f), broken ? 24 : 17,
            Projectile.rotation, broken ? 0.052f : 0.024f, broken ? 1f : 0.58f);
        if (broken)
        {
            FranBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center, Projectile.owner,
                FranBurstStyle.Barrier, 1.08f);
            FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_Death(), Projectile.Center,
                -Vector2.UnitY, Projectile.owner, FranSpectatorPalette.Cyan, 5, 1.02f);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 5.5f);
        }
    }

    private static void EmitShards(Vector2 center, Color color, int count, bool inward)
    {
        if (Main.dedServ)
            return;
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Main.rand.NextVector2Unit();
            Vector2 position = inward ? center + direction * Main.rand.NextFloat(28f, 68f) : center;
            Vector2 velocity = direction * Main.rand.NextFloat(inward ? -4.5f : 2.5f, inward ? -1.8f : 7f);
            FranParticleSystem.Spawn(new FranBarrierShardParticle(position, velocity, color,
                new Vector2(Main.rand.NextFloat(12f, 24f), Main.rand.NextFloat(3f, 7f)), Main.rand.Next(14, 24)));
        }
    }

    public static bool IsActive(Player owner)
    {
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.owner == owner.whoAmI &&
                projectile.type == ModContent.ProjectileType<FranAutoBarrierProjectile>() && projectile.ai[1] > 0f)
                return true;
        return false;
    }

    public static bool HandlePlayerHit(Player owner, int damageTaken)
    {
        if (owner.whoAmI != Main.myPlayer)
            return false;
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner != owner.whoAmI ||
                projectile.type != ModContent.ProjectileType<FranAutoBarrierProjectile>() || projectile.ai[1] <= 0f)
                continue;

            projectile.ai[1] -= 1f;
            projectile.ai[2] = 1f;
            projectile.netUpdate = true;
            FranBurst.Spawn(projectile.GetSource_FromThis(), owner.Center, owner.whoAmI,
                FranBurstStyle.Barrier, 0.82f + Math.Min(0.34f, damageTaken / 180f),
                Math.Max(1, (int)(projectile.damage * 0.86f)), 5f);
            EmitShards(owner.Center, new Color(105, 205, 255), 10, false);
            FranParticleEffects.BloomPulse(owner.Center, new Color(105, 205, 255), 24f, 104f,
                16, 0.72f, true);
            FranParticleEffects.RingPulse(owner.Center, Color.White, new Vector2(62f, 82f),
                new Vector2(138f, 174f), 17, projectile.rotation, -0.036f, 0.64f);
            FranVanillaDustEffects.Impact(owner.Center, Vector2.UnitY, FranSkill.MagicBarrier,
                new Color(105, 205, 255), 18, 1f);
            SoundEngine.PlaySound(SoundID.NPCHit53 with { Volume = 0.50f, Pitch = 0.24f }, owner.Center);

            owner.immune = true;
            owner.immuneTime = Math.Max(owner.immuneTime, 18);
            for (int i = 0; i < owner.hurtCooldowns.Length; i++)
                owner.hurtCooldowns[i] = Math.Max(owner.hurtCooldowns[i], 18);

            if (projectile.ai[1] <= 0f)
            {
                projectile.localAI[2] = 1f;
                owner.GetModPlayer<FranPlayer>().OnBarrierBroken();
                projectile.Kill();
            }
            return true;
        }
        return false;
    }
}

public sealed class FranBarrierCounterCut : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 12; Projectile.height = 12; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 16;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.timeLeft is <= 14 and >= 8 ? null : false;
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float collisionPoint = 0f;
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center,
            Projectile.Center + direction * (230f + Projectile.ai[0] * 42f), 13f + Projectile.ai[0] * 3f,
            ref collisionPoint);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float progress = 1f - Projectile.timeLeft / 16f;
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 end = Projectile.Center + direction * (230f + Projectile.ai[0] * 42f);
        Vector2 midpoint = (Projectile.Center + end) * 0.5f;
        float length = Vector2.Distance(Projectile.Center, end);
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranSmearForward", midpoint,
            new Color(90, 185, 255) * (opacity * 0.72f), direction.ToRotation(),
            new Vector2(length, 42f + Projectile.ai[0] * 6f));
        FranVfx.DrawVertical("Cal_ThinEndedLine", Projectile.Center, end, Color.White * (opacity * 0.82f), 5f);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", end, Color.White * (opacity * 0.84f),
            direction.ToRotation(), new Vector2(76f, 42f));
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranReplaySlash : ModProjectile
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.BreakerBlade}";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 10;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 48; Projectile.height = 48; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = 2; Projectile.timeLeft = 42;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 14;
    }
    private float Age => 42f - Projectile.timeLeft;
    public override bool? CanDamage() => Age >= Projectile.ai[0] ? null : false;
    public override void AI()
    {
        if (Age < Projectile.ai[0])
        {
            Projectile.velocity *= 0.72f;
            Projectile.Opacity = MathHelper.Clamp(Age / Math.Max(1f, Projectile.ai[0]), 0.18f, 0.72f);
        }
        else
        {
            if (Age == Projectile.ai[0])
            {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * (17f + Projectile.ai[1]);
                FranParticleEffects.LineRelease(Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 18f,
                    Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 34f,
                    new Color(225, 245, 255), 7, 0.54f);
            }
            Projectile.velocity *= 1.008f;
            Projectile.Opacity = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 0.72f);
        }
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        if (Age == Projectile.ai[0])
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.34f, Pitch = 0.18f + Projectile.ai[1] * 0.08f }, Projectile.Center);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        FranVfx.BeginAdditive();
        for (int i = 1; i < Projectile.oldPos.Length; i++)
        {
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            Vector2 previous = Projectile.oldPos[i - 1] + Projectile.Size * 0.5f;
            if (point == Projectile.Size * 0.5f || Vector2.DistanceSquared(point, previous) > 72f * 72f)
                continue;
            float fade = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawVertical("Cal_BloomLineSoftEdge", point, previous,
                new Color(220, 245, 255) * (fade * 0.38f * Projectile.Opacity), 13f * fade + 2f);
        }
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 24f,
            new Color(220, 245, 255) * (0.54f * Projectile.Opacity), direction.ToRotation(),
            new Vector2(126f, 44f));
        if (Age >= Projectile.ai[0] && Age <= Projectile.ai[0] + 5f)
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center + direction * 18f,
                Color.White * (0.74f * Projectile.Opacity), direction.ToRotation(), new Vector2(58f, 32f));
        FranVfx.EndAdditive();
        Texture2D texture = TextureAssets.Item[ItemID.BreakerBlade].Value;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.Lerp(new Color(80, 115, 145), Color.White, 0.52f) * Projectile.Opacity,
            Projectile.rotation, texture.Size() * 0.5f, 0.68f, SpriteEffects.None, 0f);
        return false;
    }
}

public sealed class FranBlueResonanceEcho : ModProjectile
{
    private bool constellationSpawned;
    private bool polarReleaseSpawned;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 10; Projectile.height = 10; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 34;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    private float Age => 34f - Projectile.timeLeft;
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Age is >= 20f and <= 24f ? null : false;
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float reach = Math.Max(60f, Projectile.ai[1]);
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - direction * reach * 0.5f, Projectile.Center + direction * reach * 0.5f,
            18f, ref collisionPoint);
    }
    public override void AI()
    {
        if (!Main.dedServ && !constellationSpawned)
        {
            constellationSpawned = true;
            FranConstellationEffects.SpawnBlueResonancePrelude(Projectile.Center, Projectile.velocity,
                (int)Projectile.ai[0]);
        }
        if (!Main.dedServ && Projectile.timeLeft > 14 && Projectile.timeLeft % 4 == 0)
            FranParticleSystem.Spawn(new FranResonanceNoteParticle(Projectile.Center,
                new Color(80, 205, 255), 34f, 20, Projectile.identity * 0.71f + Projectile.timeLeft));
        if (Age >= 20f && !polarReleaseSpawned)
        {
            polarReleaseSpawned = true;
            SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.24f, Pitch = 0.42f }, Projectile.Center);
            FranConstellationEffects.SpawnPolarRelease(Projectile.Center, Projectile.velocity,
                (int)Projectile.ai[0] == 3 ? 1.08f : 0.92f);
            FranParticleEffects.SpawnImpact(Projectile.Center, Projectile.velocity, new Color(80, 205, 255),
                5, 3.4f, 0.72f);
            FranSpectatorVfx.SpawnReleaseFan(Projectile.GetSource_FromThis(), Projectile.Center,
                Projectile.velocity, Projectile.owner, FranSpectatorPalette.Cyan, 3, 0.78f);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 1.4f);
        }
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if ((int)Projectile.ai[0] == 3 && Main.myPlayer == Projectile.owner)
        {
            Player owner = Main.player[Projectile.owner];
            owner.GetModPlayer<FranPlayer>().TryApplyResonanceHealing(Math.Clamp(owner.statLifeMax2 / 50, 2, 10));
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float anticipation = FranMotion.Smooth(FranMotion.Segment(Age, 0f, 18f));
        float release = FranMotion.Pulse(Age, 18f, 21f, 24f, 33f);
        float reach = Math.Max(60f, Projectile.ai[1]);
        Vector2 start = Projectile.Center - direction * reach * 0.5f;
        Vector2 end = Projectile.Center + direction * reach * 0.5f;
        float orbSize = MathHelper.Lerp(82f, 48f, anticipation) + release * 34f;
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(100, 205, 245) * (0.52f + anticipation * 0.30f),
            Main.GlobalTimeWrappedHourly * 0.44f, Vector2.One * orbSize);
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(65, 175, 255) * (0.24f + anticipation * 0.34f + release * 0.26f),
            -Main.GlobalTimeWrappedHourly * 0.88f,
            Vector2.One * (orbSize + 12f + release * 18f));
        FranVfx.DrawVertical("Cal_ThinEndedLine", start, end,
            new Color(80, 205, 255) * (0.12f + anticipation * 0.18f + release * 0.52f),
            3f + release * 10f);
        if (release > 0f)
        {
            Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
            FranVfx.DrawStretched("Cal_DrainLineBloom2", start + normal * 8f, end + normal * 8f,
                new Color(105, 220, 255) * (release * 0.32f), 7f);
            FranVfx.DrawStretched("Cal_DrainLineBloom2", start - normal * 8f, end - normal * 8f,
                Color.White * (release * 0.24f), 4f);
            FranVfx.DrawShared("SlashNarrow", Projectile.Center, Color.White * (release * 0.62f),
                direction.ToRotation(), new Vector2(0.44f, 0.16f));
            FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
                new Color(105, 220, 255) * (release * 0.74f), direction.ToRotation(),
                new Vector2(MathHelper.Clamp(reach, 82f, 176f), 62f));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                Color.White * (release * 0.86f), direction.ToRotation(), new Vector2(88f, 48f));
        }
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranFireScar : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 34; Projectile.height = 34; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 72;
        Projectile.tileCollide = true; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 18;
    }
    public override void AI()
    {
        Projectile.velocity *= 0.975f;
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ && Projectile.timeLeft % 3 == 0)
            FranParticleEffects.AuraMote(Projectile.Center, new Color(255, 92, 42), true, 20f);
        Lighting.AddLight(Projectile.Center, 0.44f, 0.12f, 0.025f);
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) =>
        target.AddBuff(BuffID.OnFire3, 240);
    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center,
            new Color(255, 115, 45) * (0.74f * opacity), Projectile.rotation, new Vector2(92f, 42f));
        FranVfx.BeginAdditive();
        FranVfx.DrawShared("Flame", Projectile.Center, new Color(255, 78, 22) * (opacity * 0.52f),
            Projectile.rotation, new Vector2(0.72f, 0.34f));
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 38f,
            new Color(255, 170, 55) * (opacity * 0.46f), Projectile.rotation, new Vector2(132f, 44f));
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranWindField : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 188; Projectile.height = 188; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 72;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 20;
    }
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        Projectile.rotation += 0.09f;
        if (Main.netMode != NetmodeID.MultiplayerClient)
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy() || npc.boss || Vector2.DistanceSquared(npc.Center, Projectile.Center) > 96f * 96f)
                    continue;
                npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * 0.16f * npc.knockBackResist;
            }
        Lighting.AddLight(Projectile.Center, 0.06f, 0.28f, 0.24f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float fade = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
        FranVfx.BeginAdditive();
        FranVfx.DrawShared("WindTwister", Projectile.Center, new Color(105, 235, 215) * (0.30f * fade),
            Projectile.rotation, 0.58f + pulse * 0.08f);
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(135, 245, 225) * (0.22f * fade), -Projectile.rotation,
            Vector2.One * (166f + pulse * 18f));
        for (int i = 0; i < 3; i++)
        {
            float angle = Projectile.rotation + i * MathHelper.TwoPi / 3f;
            Vector2 point = Projectile.Center + angle.ToRotationVector2() * (48f + pulse * 7f);
            FranVfx.DrawOriginalPixels("FranSmearForward", point,
                new Color(170, 255, 235) * (0.18f * fade), angle + MathHelper.PiOver2,
                new Vector2(92f, 26f));
        }
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranLightningBranches : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 168; Projectile.height = 168; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 18;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.timeLeft is <= 16 and >= 10 ? null : false;
    public override void AI()
    {
        if (Projectile.localAI[0]++ == 0f)
        {
            FranLightingBoltsSystem.SpawnThunderImpact(Projectile.Center, new Color(165, 125, 255), 0.82f);
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.42f, Pitch = 0.26f }, Projectile.Center);
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float progress = 1f - Projectile.timeLeft / 18f;
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        FranVfx.BeginAdditive();
        for (int i = 0; i < 6; i++)
        {
            float angle = Projectile.velocity.ToRotation() + i * MathHelper.TwoPi / 6f;
            Vector2 end = Projectile.Center + angle.ToRotationVector2() * (58f + (i % 2) * 26f);
            FranVfx.DrawTexturedLightning(Projectile.Center, end, new Color(165, 125, 255),
                4.2f, 6, Projectile.identity + i * 3.7f, opacity * 0.82f);
        }
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            new Color(165, 125, 255) * (0.74f * opacity), Projectile.velocity.ToRotation(),
            new Vector2(142f, 96f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * (0.88f * opacity), Projectile.velocity.ToRotation(), new Vector2(96f, 54f));
        FranVfx.DrawBloom(Projectile.Center, Color.White, 38f, opacity * 0.44f);
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranCompressedCut : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 10; Projectile.height = 10; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 18;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.timeLeft is <= 13 and >= 9 ? null : false;
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float collisionPoint = 0f;
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - direction * 26f, Projectile.Center + direction * (310f + Projectile.ai[0] * 46f),
            11f + Projectile.ai[0] * 2f, ref collisionPoint);
    }
    public override void AI()
    {
        if (Projectile.timeLeft == 13)
        {
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.78f, Pitch = -0.18f }, Projectile.Center);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 5f + Projectile.ai[0]);
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float age = 18f - Projectile.timeLeft;
        float warning = 1f - FranMotion.Smooth(FranMotion.Segment(age, 0f, 5f));
        float release = FranMotion.Pulse(age, 4f, 6f, 9f, 16f);
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 start = Projectile.Center - direction * 26f;
        Vector2 end = Projectile.Center + direction * (310f + Projectile.ai[0] * 46f);
        Vector2 midpoint = (start + end) * 0.5f;
        float length = Vector2.Distance(start, end);
        FranVfx.BeginAdditive();
        FranVfx.DrawVertical("Cal_ThinEndedLine", start, end,
            new Color(210, 250, 255) * (0.14f * warning + 0.76f * release), 2f + release * 12f);
        if (release > 0f)
        {
            FranVfx.DrawOriginalPixels("FranSmearForward", midpoint,
                new Color(155, 225, 255) * (release * 0.62f), direction.ToRotation(),
                new Vector2(length, 52f + Projectile.ai[0] * 5f));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", end, Color.White * (release * 0.82f),
                direction.ToRotation(), new Vector2(82f, 44f));
        }
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FranHammerBeacon : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 80; Projectile.height = 80; Projectile.timeLeft = 300;
        Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        Projectile.rotation += 0.025f + Projectile.ai[0] * 0.008f;
        Lighting.AddLight(Projectile.Center, 0.08f, 0.14f, 0.42f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float fade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.identity);
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(105, 145, 225) * (0.54f * fade), Projectile.rotation * 0.7f,
            Vector2.One * (82f + pulse * 12f));
        FranVfx.BeginAdditive();
        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            new Color(115, 155, 255) * (0.30f * fade), -Projectile.rotation,
            Vector2.One * (96f + pulse * 16f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center, Color.White * (0.38f * fade),
            -Projectile.rotation, new Vector2(62f, 36f));
        FranVfx.EndAdditive();
        return false;
    }

    public static void Place(Player owner, Terraria.DataStructures.IEntitySource source, Vector2 position, int stage)
    {
        int type = ModContent.ProjectileType<FranHammerBeacon>();
        int count = 0;
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.owner == owner.whoAmI && projectile.type == type)
                count++;
        if (count >= 3)
            return;
        Projectile.NewProjectile(source, position, Vector2.Zero, type, 0, 0f, owner.whoAmI, stage);
    }

    public static int ActivateAll(Player owner, int damage)
    {
        int count = 0;
        int type = ModContent.ProjectileType<FranHammerBeacon>();
        foreach (Projectile beacon in Main.ActiveProjectiles)
        {
            if (beacon.owner != owner.whoAmI || beacon.type != type)
                continue;
            Projectile.NewProjectile(beacon.GetSource_FromThis(), beacon.Center, Vector2.Zero,
                ModContent.ProjectileType<ThorsHammerBolt>(), Math.Max(1, (int)(damage * 0.92f)),
                6f, owner.whoAmI);
            beacon.Kill();
            count++;
        }
        return count;
    }
}
