using System;
using FranMod.Content.Players;
using FranMod.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    private FranFissionArc[] corona;
    private const int Lifetime = 180;
    private const int StableVisualFrames = 15;
    private const int UnstableVisualFrames = 10;
    private const float UnstableVisualOffset = 5f;
    private const string StarTexture = "Fran/Textures/Particles/KsTexture/star_09";
    private const string TwirlTexture = "Fran/Textures/Particles/KsTexture/twirl_01";
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
        float opacity = deploy * ending;
        float rotation = Main.GlobalTimeWrappedHourly * 3.2f;
        // DragonRageC 的原始脉动幅度是 0.12；雷链球只保留一半，也就是 0.06。
        float pulse = 1f + 0.06f * MathF.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi);
        float scale = 0.2f * Projectile.scale * pulse;
        Vector2 visualCenter = Projectile.Center + GetUnstableVisualOffset();
        Color lightningColor = Order % 2 == 0 ? new Color(156, 112, 255) : new Color(103, 213, 255);
        Texture2D star = ModContent.Request<Texture2D>(StarTexture).Value;
        Texture2D twirl = ModContent.Request<Texture2D>(TwirlTexture).Value;

        // 完整保留 DragonRageC 能量火球的 star_09 橙色星核与两层 twirl_01；
        // 只按需求把大小脉动幅度减半，并沿用本弹幕自身的出现/消失透明度。
        Color starColor = Color.Orange * opacity;
        starColor.A = 0;
        Main.EntitySpriteDraw(star, visualCenter - Main.screenPosition, null, starColor, rotation,
            star.Size() * 0.5f, scale, SpriteEffects.None, 0f);

        for (int i = 0; i < 2; i++)
        {
            float offsetAngle = rotation * (i == 0 ? 1.8f : -1.2f);
            float ringScale = scale * (i == 0 ? 0.8f : 0.7f);
            Color ringColor = (i == 0 ? Color.OrangeRed : Color.White) * (0.6f * opacity);
            ringColor.A = 0;
            Main.EntitySpriteDraw(twirl, visualCenter - Main.screenPosition, null, ringColor, offsetAngle,
                twirl.Size() * 0.5f, ringScale, SpriteEffects.None, 0f);
        }

        // Small independent fractures blink around the electrode instead of rotating solid wires.
        FranVfx.BeginAdditive();
        if (corona is null)
        {
            corona = new FranFissionArc[4];
            for (int i = 0; i < corona.Length; i++)
                corona[i] = new FranFissionArc((MathHelper.TwoPi * i / 4f + Projectile.identity).ToRotationVector2()
                    * (32f + TacticLayers * 2f), Projectile.identity * 397 + i, 1.1f, 1);
        }
        for (int i = 0; i < 4; i++)
        {
            corona[i].Draw(Main.spriteBatch, visualCenter, lightningColor,
                (Age + i * 4) % 22f, 15f, 2f, opacity * 0.78f);
        }
        FranVfx.EndAdditive();
        return false;
    }

    private Vector2 GetUnstableVisualOffset()
    {
        int cycleLength = StableVisualFrames + UnstableVisualFrames;
        int cycleFrame = Math.Max(0, (int)Age - 1) % cycleLength;
        if (cycleFrame < StableVisualFrames)
            return Vector2.Zero;

        // 不插值：不稳定阶段每帧直接跳到中心上下左右的一个 5 像素位置。
        uint hash = unchecked((uint)(Projectile.identity * 374761393 +
            (int)Age * 668265263 + Order * 1442695041));
        hash ^= hash >> 13;
        return (hash & 3u) switch
        {
            0u => new Vector2(UnstableVisualOffset, 0f),
            1u => new Vector2(-UnstableVisualOffset, 0f),
            2u => new Vector2(0f, UnstableVisualOffset),
            _ => new Vector2(0f, -UnstableVisualOffset)
        };
    }
}

/// <summary>
/// A stationary, progressively drawn conductor. Position is the first endpoint and velocity stores
/// the full edge vector, so both endpoints are synchronized without dedicating four AI fields.
/// </summary>
public sealed class FranThunderCircuitLink : ModProjectile
{
    private FranFissionArc conductor;
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
            if (!Main.dedServ)
                conductor = new FranFissionArc(End - Start, Projectile.identity * 397 ^ Projectile.owner,
                    (Returning ? 2.6f : 2.1f) * Intensity);
            SoundEngine.PlaySound(SoundID.Item93 with
            {
                Volume = Returning ? 0.34f : 0.25f,
                Pitch = Returning ? -0.02f : 0.24f,
                MaxInstances = 8
            }, End);
        }

        float travel = FranMotion.Smooth(FranMotion.Segment(activeAge, 0f, 5f));
        Vector2 pulsePoint = Vector2.Lerp(Start, End, travel);
        if (activeAge is >= 1f and <= 5f)
        {
            float previous = FranMotion.Smooth(FranMotion.Segment(activeAge - 1f, 0f, 5f));
            FranFlatLightningEffects.EmitSpan(Vector2.Lerp(Start, End, previous), pulsePoint,
                Intensity, FranFlatLightningPalette.ThunderChain, (int)activeAge * 22);
        }
        else if (activeAge is >= 6f and <= 12f && (int)activeAge % 2 == 0)
            FranFlatLightningEffects.EmitSpan(Start, End, Intensity * 0.68f,
                FranFlatLightningPalette.ThunderChain, (int)activeAge * 22);
        if (activeAge is >= 0f and <= 12f)
            Lighting.AddLight(pulsePoint, 0.12f, 0.19f, 0.55f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranLightingBoltsSystem.SpawnThunderImpact(target.Center,
            Returning ? new Color(188, 125, 255) : new Color(112, 205, 255),
            0.58f * Intensity);
        FranFlatLightningEffects.Burst(target.Center, Intensity * 0.55f,
            FranFlatLightningPalette.ThunderChain, 3, 45f * Intensity);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float activeAge = Age - Delay;
        if (activeAge <= 0f)
            return false;

        Color violet = Returning ? new Color(187, 110, 255) : new Color(144, 112, 255);
        FranVfx.BeginAdditive();
        conductor?.Draw(Main.spriteBatch, Start, violet, activeAge, 23f, 5f);
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
        if (!Fired && (int)Age % 5 == 0 && !Main.dedServ)
        {
            float radius = MathHelper.Lerp(140f + TacticLayers * 10f, 28f, charge);
            for (int i = 0; i < 3; i++)
            {
                Vector2 outer = Projectile.Center + (Age * 0.37f + MathHelper.TwoPi * i / 3f)
                    .ToRotationVector2() * radius;
                FranFlatLightningEffects.Fracture(outer, Projectile.Center, 0.6f,
                    FranFlatLightningPalette.ThunderChain, Projectile.identity * 397 + (int)Age + i,
                    2f, 10);
            }
        }
        Lighting.AddLight(Projectile.Center, 0.13f + charge * 0.32f, 0.16f + charge * 0.38f,
            0.42f + charge * 0.55f);
    }

    private void FireFinalBolt()
    {
        Vector2 impact = Projectile.Center;
        Vector2 spawn = impact + new Vector2(Main.rand.NextFloat(-90f, 90f), Main.rand.NextFloat(-820f, -700f));
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawn, Vector2.Zero,
            ModContent.ProjectileType<FranThunderBudgetStrike>(), Projectile.damage,
            Projectile.knockBack, Projectile.owner, impact.X, impact.Y, TargetIndex + 1);

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
            rotation, "FranVfx_BloomRing");
        FranVfx.DrawRing(Projectile.Center, Color.White, collapseRadius * 0.62f, opacity * 0.48f,
            -rotation * 1.35f, "FranVfx_FadedStarRing");

        if (!Fired)
        {
            Vector2 guideStart = Projectile.Center - Vector2.UnitY * MathHelper.Lerp(230f, 520f, charge);
            FranVfx.DrawSharedPixels("FranVfx_DrainLineBloom", (guideStart + Projectile.Center) * 0.5f,
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
