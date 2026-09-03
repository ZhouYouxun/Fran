using System;
using FranMod.Content.Players;
using FranMod.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

/// <summary>
/// A persistent electrode left behind by the first Thunder Chain sword stroke. The node deals one
/// small placement pulse, then becomes an addressable part of the circuit for the remaining combo.
/// </summary>
public sealed class FranThunderCircuitNode : ModProjectile
{
    private const int Lifetime = 180;
    private float Age => Projectile.localAI[0];
    private int AttachedTargetIndex => (int)Projectile.ai[0] - 1;
    private int Order => Math.Max(0, (int)Projectile.ai[1]);
    private int TacticLayers => Math.Clamp((int)Projectile.ai[2], 1, FranPlayer.MaximumTacticSlots);

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;

    public override void SetDefaults()
    {
        Projectile.width = 56;
        Projectile.height = 56;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;

    public override bool? CanDamage() => Age is >= 1f and <= 6f ? null : false;

    public override void AI()
    {
        Projectile.localAI[0]++;
        Projectile.rotation += 0.025f + Order * 0.003f;

        if (AttachedTargetIndex >= 0 && Main.npc.IndexInRange(AttachedTargetIndex))
        {
            NPC target = Main.npc[AttachedTargetIndex];
            if (target.active && target.CanBeChasedBy(Projectile))
                Projectile.Center = target.Center;
        }

        if (Age == 1f)
        {
            FranLightingBoltsSystem.SpawnThunderImpact(Projectile.Center,
                Order % 2 == 0 ? new Color(154, 112, 255) : new Color(104, 210, 255),
                0.48f + 0.06f * TacticLayers);
            SoundEngine.PlaySound(SoundID.Item93 with
            {
                Volume = 0.20f,
                Pitch = 0.34f + Order * 0.035f,
                MaxInstances = 6
            }, Projectile.Center);
        }

        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8.2f + Order * 1.73f);
        Lighting.AddLight(Projectile.Center, 0.10f + pulse * 0.07f, 0.13f + pulse * 0.08f,
            0.38f + pulse * 0.18f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranLightingBoltsSystem.SpawnThunderImpact(target.Center, new Color(130, 180, 255), 0.44f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float deploy = FranMotion.EaseOutBack(FranMotion.Segment(Age, 0f, 11f));
        float ending = FranMotion.Smooth(FranMotion.Segment(Projectile.timeLeft, 0f, 18f));
        float pulse = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Order * 1.31f);
        float opacity = deploy * ending;
        float radius = (21f + 2.5f * TacticLayers) * Projectile.scale * pulse;
        Color darkBody = Order % 2 == 0 ? new Color(35, 27, 78) : new Color(24, 43, 82);
        Color color = Order % 2 == 0 ? new Color(156, 112, 255) : new Color(103, 213, 255);

        FranVfx.DrawRing(Projectile.Center, darkBody, radius * 1.18f, opacity * 0.72f,
            -Projectile.rotation, "Cal_HighResHollowCircle");

        FranVfx.BeginAdditive();
        FranVfx.DrawRing(Projectile.Center, color, radius, opacity * 0.64f,
            Projectile.rotation, "Cal_BloomRing");
        FranVfx.DrawRing(Projectile.Center, Color.White, radius * 0.56f, opacity * 0.38f,
            -Projectile.rotation * 1.4f, "Cal_FadedStarRing");

        // 两条交织电流与短横向支路共同组成稳定导体，并保持弗兰的蓝紫配色。
        for (int i = 0; i < 4; i++)
        {
            float angle = Projectile.rotation * (i % 2 == 0 ? 1f : -1f) + MathHelper.TwoPi * i / 4f;
            Vector2 tangent = angle.ToRotationVector2();
            Vector2 normal = tangent.RotatedBy(MathHelper.PiOver2);
            Vector2 start = Projectile.Center + tangent * radius * 0.24f + normal * 4f;
            Vector2 end = Projectile.Center + tangent * radius * 1.20f - normal * 4f;
            if (!FranLightningTrailRenderer.TryDrawBolt(start, end, darkBody, color,
                    2.2f + TacticLayers * 0.18f, 6,
                    Projectile.identity * 0.77f + i * 3.19f, opacity * 0.72f,
                    Main.GlobalTimeWrappedHourly * 7f))
                FranVfx.DrawTexturedLightning(start, end, color, 2.2f + TacticLayers * 0.18f,
                    5, Projectile.identity * 0.77f + i * 3.19f, opacity * 0.72f);
        }

        FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center,
            color * (opacity * 0.74f), Projectile.rotation, new Vector2(radius * 1.26f));
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * (opacity * 0.58f), Projectile.rotation, new Vector2(radius * 1.68f, radius * 0.72f));
        FranVfx.EndAdditive();
        return false;
    }
}

/// <summary>
/// A stationary, progressively drawn conductor. Position is the first endpoint and velocity stores
/// the full edge vector, so both endpoints are synchronized without dedicating four AI fields.
/// </summary>
public sealed class FranThunderCircuitLink : ModProjectile
{
    private const int BaseLifetime = 34;
    private float Age => Projectile.localAI[0];
    private float Delay => Math.Max(0f, Projectile.ai[0]);
    private float Intensity => MathHelper.Clamp(Projectile.ai[1], 0.45f, 1.8f);
    private bool Returning => Projectile.ai[2] > 0.5f;
    private Vector2 Start => Projectile.Center;
    private Vector2 End => Projectile.Center + Projectile.velocity;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = BaseLifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override void OnSpawn(Terraria.DataStructures.IEntitySource source) =>
        Projectile.timeLeft = BaseLifetime + (int)Delay;

    public override bool ShouldUpdatePosition() => false;

    public override bool? CanDamage()
    {
        float activeAge = Age - Delay;
        return activeAge is >= 5f and <= 12f ? null : false;
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Start, End, 11f * Intensity, ref collisionPoint);
    }

    public override void AI()
    {
        Projectile.localAI[0]++;
        float activeAge = Age - Delay;
        if (activeAge == 1f)
        {
            SoundEngine.PlaySound(SoundID.Item93 with
            {
                Volume = Returning ? 0.34f : 0.25f,
                Pitch = Returning ? -0.02f : 0.24f,
                MaxInstances = 8
            }, End);
        }

        float travel = FranMotion.Smooth(FranMotion.Segment(activeAge, 0f, 8f));
        Vector2 pulsePoint = Vector2.Lerp(Start, End, travel);
        if (activeAge is >= 0f and <= 12f)
            Lighting.AddLight(pulsePoint, 0.12f, 0.19f, 0.55f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranLightingBoltsSystem.SpawnThunderImpact(target.Center,
            Returning ? new Color(188, 125, 255) : new Color(112, 205, 255),
            0.58f * Intensity);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float activeAge = Age - Delay;
        if (activeAge <= 0f)
            return false;

        float growth = FranMotion.Smooth(FranMotion.Segment(activeAge, 0f, 8f));
        float fade = 1f - FranMotion.Smooth(FranMotion.Segment(activeAge, 15f, 29f));
        float opacity = Math.Max(0f, fade);
        Vector2 growingEnd = Vector2.Lerp(Start, End, growth);
        Vector2 direction = (growingEnd - Start).SafeNormalize(Vector2.UnitX);
        Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
        float phase = Main.GlobalTimeWrappedHourly * (Returning ? -15f : 12f) + Projectile.identity * 0.71f;
        Color body = Returning ? new Color(54, 25, 88) : new Color(30, 43, 96);
        Color violet = Returning ? new Color(187, 110, 255) : new Color(144, 112, 255);
        Color azure = new(103, 218, 255);

        FranVfx.BeginAdditive();
        float weaveOffset = MathF.Sin(phase) * 5f * Intensity;
        bool firstStrand = FranLightningTrailRenderer.TryDrawBolt(
            Start + normal * weaveOffset, growingEnd - normal * weaveOffset,
            body, violet, 5.4f * Intensity, 14, Projectile.identity * 0.83f + 2.4f,
            opacity, phase);
        bool secondStrand = FranLightningTrailRenderer.TryDrawBolt(
            Start - normal * weaveOffset, growingEnd + normal * weaveOffset,
            new Color(22, 46, 82), azure, 3.7f * Intensity, 13,
            Projectile.identity * 1.13f + 8.2f, opacity * 0.90f, -phase * 0.82f);
        if (!firstStrand)
            FranVfx.DrawTexturedLightning(Start + normal * weaveOffset,
                growingEnd - normal * weaveOffset, violet, 5.4f * Intensity, 14,
                Projectile.identity * 0.83f + 2.4f, opacity);
        if (!secondStrand)
            FranVfx.DrawTexturedLightning(Start - normal * weaveOffset,
                growingEnd + normal * weaveOffset, azure, 3.7f * Intensity, 13,
                Projectile.identity * 1.13f + 8.2f, opacity * 0.90f);

        for (int i = 1; i <= 4; i++)
        {
            float completion = i / 5f * growth;
            Vector2 center = Vector2.Lerp(Start, growingEnd, completion);
            float rung = MathF.Sin(phase + i * 1.77f) * 6f * Intensity;
            Vector2 rungStart = center - normal * (5f + rung);
            Vector2 rungEnd = center + normal * (5f + rung);
            if (!FranLightningTrailRenderer.TryDrawBolt(rungStart, rungEnd,
                    new Color(31, 35, 82), Color.White, 1.45f * Intensity, 4,
                    Projectile.identity + i * 5.13f, opacity * 0.64f, phase + i))
                FranVfx.DrawTexturedLightning(rungStart, rungEnd, Color.White,
                    1.45f * Intensity, 3, Projectile.identity + i * 5.13f, opacity * 0.64f);
        }

        FranVfx.DrawOriginalPixels("FranFlashFourPoint", growingEnd,
            Color.White * (opacity * (0.56f + 0.22f * growth)), direction.ToRotation(),
            new Vector2(38f, 18f) * Intensity);
        FranVfx.EndAdditive();
        return false;
    }
}

/// <summary>
/// Holds the closing circuit long enough for every return current to reach the focus, then calls the
/// sole sky bolt of the combo. This keeps the final strike readable instead of making every swing rain.
/// </summary>
public sealed class FranThunderCircuitFinisher : ModProjectile
{
    private const int Lifetime = 54;
    private float Age => Projectile.localAI[0];
    private int FireDelay => Math.Max(1, (int)Projectile.ai[0]);
    private int TargetIndex => (int)Projectile.ai[1] - 1;
    private int TacticLayers => Math.Clamp((int)Projectile.ai[2], 1, FranPlayer.MaximumTacticSlots);
    private bool Fired => Projectile.localAI[1] > 0f;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => false;

    public override void AI()
    {
        Projectile.localAI[0]++;
        if (!Fired && TargetIndex >= 0 && Main.npc.IndexInRange(TargetIndex))
        {
            NPC target = Main.npc[TargetIndex];
            if (target.active && target.CanBeChasedBy(Projectile))
                Projectile.Center = target.Center;
        }

        if (!Fired && Age >= FireDelay)
        {
            Projectile.localAI[1] = 1f;
            if (Projectile.owner == Main.myPlayer)
                FireFinalBolt();
        }

        float charge = FranMotion.Smooth(FranMotion.Segment(Age, 0f, FireDelay));
        Lighting.AddLight(Projectile.Center, 0.13f + charge * 0.32f, 0.16f + charge * 0.38f,
            0.42f + charge * 0.55f);
    }

    private void FireFinalBolt()
    {
        Vector2 impact = Projectile.Center;
        Vector2 spawn = impact + new Vector2(Main.rand.NextFloat(-90f, 90f), Main.rand.NextFloat(-820f, -700f));
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawn, Vector2.Zero,
            ModContent.ProjectileType<FranThunderStrikeProjectile>(), Projectile.damage,
            Projectile.knockBack, Projectile.owner, impact.X, impact.Y, 2f);

        Projectile.NewProjectile(Projectile.GetSource_FromThis(), impact, Vector2.Zero,
            ModContent.ProjectileType<FranThunderDropVisual>(), 0, 0f, Projectile.owner,
            spawn.X, spawn.Y, 2000f + Main.rand.NextFloat(100f));

        int nodeType = ModContent.ProjectileType<FranThunderCircuitNode>();
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner != Projectile.owner || projectile.type != nodeType)
                continue;
            projectile.timeLeft = Math.Min(projectile.timeLeft, 13);
            projectile.netUpdate = true;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float charge = FranMotion.Smooth(FranMotion.Segment(Age, 0f, FireDelay));
        float fade = Fired ? 1f - FranMotion.Smooth(FranMotion.Segment(Age, FireDelay + 5f, Lifetime - 2f)) : 1f;
        float opacity = Math.Max(0f, fade);
        float collapseRadius = MathHelper.Lerp(146f + TacticLayers * 10f, 28f, charge);
        float rotation = Main.GlobalTimeWrappedHourly * 2.8f + Projectile.identity * 0.13f;
        Color violet = new(167, 105, 255);
        Color azure = new(103, 218, 255);

        FranVfx.BeginAdditive();
        FranVfx.DrawRing(Projectile.Center, violet, collapseRadius, opacity * 0.78f,
            rotation, "Cal_BloomRing");
        FranVfx.DrawRing(Projectile.Center, Color.White, collapseRadius * 0.62f, opacity * 0.48f,
            -rotation * 1.35f, "Cal_FadedStarRing");

        // 分支电冠在落雷前向内汇聚，命中后反向裂解，使蓄力与爆发阶段清楚分离。
        int branchCount = 6 + TacticLayers;
        for (int i = 0; i < branchCount; i++)
        {
            float angle = MathHelper.TwoPi * i / branchCount + rotation * (i % 2 == 0 ? 0.22f : -0.17f);
            Vector2 direction = angle.ToRotationVector2();
            float branchLength = (Fired ? 118f : collapseRadius * 0.76f) * (0.84f + (i % 3) * 0.10f);
            Vector2 outer = Projectile.Center + direction * branchLength;
            Vector2 start = Fired ? Projectile.Center : outer;
            Vector2 end = Fired ? outer : Projectile.Center;
            Color branchColor = i % 2 == 0 ? violet : azure;
            if (!FranLightningTrailRenderer.TryDrawBolt(start, end,
                    new Color(33, 24, 78), branchColor,
                    (Fired ? 4.7f : 3.2f) + TacticLayers * 0.16f, 8,
                    Projectile.identity * 0.67f + i * 4.13f,
                    opacity * (Fired ? 0.86f : 0.72f), Age * (Fired ? 0.82f : -0.34f)))
                FranVfx.DrawTexturedLightning(start, end, branchColor,
                    (Fired ? 4.7f : 3.2f) + TacticLayers * 0.16f, 7,
                    Projectile.identity * 0.67f + i * 4.13f + (Fired ? Age : 0f),
                    opacity * 0.72f);
        }

        if (!Fired)
        {
            Vector2 guideStart = Projectile.Center - Vector2.UnitY * MathHelper.Lerp(230f, 520f, charge);
            FranVfx.DrawSharedPixels("Cal_DrainLineBloom", (guideStart + Projectile.Center) * 0.5f,
                azure * (opacity * charge * 0.30f), 0f,
                new Vector2(9f + charge * 6f, Vector2.Distance(guideStart, Projectile.Center)));
        }
        else
        {
            float flash = 1f - FranMotion.Smooth(FranMotion.Segment(Age, FireDelay, FireDelay + 12f));
            FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
                violet * (flash * 0.82f), MathHelper.PiOver2, new Vector2(258f, 174f));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                Color.White * (flash * 0.94f), MathHelper.PiOver2, new Vector2(196f, 118f));
        }

        FranVfx.EndAdditive();
        return false;
    }
}
