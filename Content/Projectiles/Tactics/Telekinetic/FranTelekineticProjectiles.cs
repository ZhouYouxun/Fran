using System;
using System.Collections.Generic;
using System.IO;
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


public sealed class Telekinetic_CondensedMindCore : ModProjectile
{
    private bool hasHit;
    internal static readonly Color TrailBlue = new(28, 145, 235);
    internal static readonly Color TrailLightBlue = new(55, 178, 235);

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        ProjectileID.Sets.TrailCacheLength[Type] = 136;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 12;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = !NPC.downedBoss1;
        Projectile.timeLeft = 135;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
        Projectile.extraUpdates = 1;
    }

    public override void AI()
    {
        // 击败克苏鲁之眼后才解锁念核穿墙；追踪仍由发射时的全选标记独立控制。
        Projectile.tileCollide = !NPC.downedBoss1;
        // This helper updates homing every sub-update and raises extraUpdates
        // from 1 to 2 while a target is available; velocity is NOT divided by MaxUpdates.
        NPC closest = null;
        float closestDistance = 25000f;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            // ai[0] snapshots full-loadout eligibility at launch and is synced by vanilla.
            if (Projectile.ai[0] != 1f)
                break;
            float extraDistance = npc.width / 2 + npc.height / 2;
            if (!npc.CanBeChasedBy(Projectile, false) || !Projectile.WithinRange(npc.Center, 400f + extraDistance))
                continue;
            float distance = Projectile.Distance(npc.Center);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = npc;
            }
        }
        Projectile.extraUpdates = closest is null ? 1 : 2;
        if (closest is not null)
            Projectile.velocity = (Projectile.velocity * 20f +
                (closest.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) *
                (24f * FranBalance.GetTelekineticCoreSpeedMultiplier())) / 21f;
        Projectile.scale = Utils.GetLerpValue(0f, 8f, Projectile.timeLeft, true);
        Projectile.rotation = Projectile.velocity.ToRotation();

        if (!Main.dedServ)
        {
            bool dim = Main.rand.NextBool(5);
            Dust dust = Dust.NewDustPerfect(Projectile.Center,
                dim ? ModContent.DustType<FranCoreDimDust>() : ModContent.DustType<FranCoreBrightDust>(),
                -Projectile.velocity * Main.rand.NextFloat(0.05f, 0.3f));
            dust.noGravity = true;
            dust.scale = Main.rand.NextFloat(0.75f, 0.95f);
            if (dim)
                dust.scale = Main.rand.NextFloat(0.55f, 0.75f);
            else
                dust.fadeIn = 0.5f;
        }
    }

    public override bool? CanDamage() => hasHit ? false : null;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (Projectile.oldPos[0] == Vector2.Zero)
            return false;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.oldPos[0] + Projectile.Size * 0.5f, Projectile.Center);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (hasHit)
            return;
        hasHit = true;
        // 只同步一次 Fran 粒子爆发；后续消散不再生成追随敌人的切割特效。
        if (Main.myPlayer == Projectile.owner)
            Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, Vector2.Zero,
                ModContent.ProjectileType<FranCoreImpactBurst>(), 0, 0f, Projectile.owner,
                Projectile.velocity.ToRotation());
        Projectile.timeLeft = Math.Min(Projectile.timeLeft, 12);
        Projectile.velocity *= 0.2f;
        Projectile.damage = 0;
        Projectile.netUpdate = true;
    }

    public override void OnKill(int timeLeft)
    {
        // 未命中的核心在寿命结束处释放同一套爆发，命中过的核心避免重复播放。
        if (Main.myPlayer != Projectile.owner || hasHit)
            return;
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
            ModContent.ProjectileType<FranCoreImpactBurst>(), 0, 0f, Projectile.owner,
            Projectile.rotation);
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(hasHit);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        hasHit |= reader.ReadBoolean();
        if (hasHit)
        {
            Projectile.damage = 0;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 12);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (!FranProjectileTrailRenderer.TryDrawTelekineticCore(Projectile,
                TrailBlue, TrailLightBlue, Color.White, 1f))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.TelekineticNeedle,
                TrailLightBlue, 12f * Projectile.scale, 1f);
        // 飞行顶端沿用 Fran 自己的贴图。
        FranVfx.DrawOriginalPixels("FranPressureNeedle", Projectile.Center,
            new Color(115, 225, 255), Projectile.rotation,
            new Vector2(54f, 30f) * Projectile.scale);
        return false;
    }
}

/// <summary>
/// 折念刃：使用泰拉刃式实体波体作为底层，并在其上叠加 Fran 的着色器冲击波。
/// 本体最多造成十次近战伤害，命中后降低速度与后续伤害。
/// </summary>
public sealed class Telekinetic_DestructiveWave : ModProjectile
{
    // 保留着色器实现，当前只显示泰拉刃式波体。
    private static readonly bool ShaderWaveEnabled = false;
    private const float VisualScaleMultiplier = 1.30f;
    private const float ActiveLifetime = 43f;
    private const float DamageEndTime = 34f;
    private const float TerrainDissipationLifetime = 16f;
    private ref float Age => ref Projectile.localAI[0];
    private ref float DamagingHitCount => ref Projectile.localAI[1];
    private ref float DissipationAge => ref Projectile.localAI[2];
    private bool Dissipating => Projectile.ai[1] != 0f;
    private bool SuppressDeathBurst => Projectile.ai[1] == 1f;
    private float BaseScale => Projectile.ai[2] > 0f ? Projectile.ai[2] : 1f;
    private float SwingSign => Projectile.ai[0] < 0f ? -1f : 1f;
    private int DamagingHitLimit => Math.Max(1, FranBalance.GetInt(
        nameof(FranBalance.TelekineticFoldBladeHitLimit), FranBalance.TelekineticFoldBladeHitLimit));

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.TerraBlade2Shot}";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 8;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 700;
    }

    public override void SetDefaults()
    {
        Projectile.width = 8;
        Projectile.height = 8;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 90;
        Projectile.ignoreWater = true;
        Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 10;
        Projectile.ownerHitCheck = true;
        Projectile.ownerHitCheckDistance = 300f;
    }

    public override bool? CanDamage() => !Dissipating && Age < DamageEndTime &&
        DamagingHitCount < DamagingHitLimit ? null : false;

    public override void AI()
    {
        if (Dissipating)
        {
            UpdateDissipation();
            return;
        }

        Age++;
        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Projectile.rotation = forward.ToRotation();
        Projectile.ownerHitCheck = Age <= 6f;

        float contraction = 1f - MathF.Pow(1f - Utils.Remap(
            Age, ActiveLifetime * 0.2f, ActiveLifetime, 0f, 1f), 2f);
        Projectile.scale = MathHelper.Lerp(1.5f, 1f, contraction) * BaseScale;
        Projectile.Opacity = Utils.Remap(Age, 0f, 9f, 0f, 1f) *
            Utils.Remap(Age, ActiveLifetime - 12f, ActiveLifetime, 1f, 0f);

        // 原版 TerraBlade2Shot 会检测波体前沿，先减速，再由中心的方块碰撞停住。
        if (IsWaveFrontBlocked())
        {
            BeginDissipation(suppressDeathBurst: true);
            UpdateDissipation();
            return;
        }

        if (Projectile.velocity.Length() > 8f)
        {
            float deceleration = MathHelper.Clamp(FranBalance.GetFloat(
                nameof(FranBalance.TelekineticFoldBladeFlightDeceleration),
                FranBalance.TelekineticFoldBladeFlightDeceleration), 0.01f, 1f);
            Projectile.velocity *= deceleration;
        }

        Vector2 lightPosition = Projectile.Center + forward * 85f * Projectile.scale;
        Lighting.AddLight(lightPosition, new Vector3(0.12f, 0.58f, 1f) * Projectile.Opacity);

        if (!Main.dedServ)
            FranLightingBoltsSystem.SpawnTelekineticWaveFlightDust(
                Projectile.Center, Projectile.velocity, Projectile.rotation, Projectile.scale,
                Projectile.Opacity);

        if (Age >= DamageEndTime)
        {
            Projectile.friendly = false;
            Projectile.damage = 0;
            Age += 3f;
            Projectile.velocity *= 0.76f;
        }

        if (Age >= ActiveLifetime)
            Projectile.Kill();
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float coneLength = 90f * Projectile.scale;
        const float maximumAngle = MathHelper.PiOver4;
        return targetHitbox.IntersectsConeFastInaccurate(Projectile.Center, coneLength,
            Projectile.rotation, maximumAngle) &&
            Collision.CanHit(Projectile.Center, 0, 0, targetHitbox.Center.ToVector2(), 0, 0);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        DamagingHitCount++;
        float speedRetention = MathHelper.Clamp(FranBalance.GetFloat(
            nameof(FranBalance.TelekineticFoldBladeHitSpeedRetention),
            FranBalance.TelekineticFoldBladeHitSpeedRetention), 0.01f, 1f);
        float damageRetention = MathHelper.Clamp(FranBalance.GetFloat(
            nameof(FranBalance.TelekineticFoldBladeHitDamageRetention),
            FranBalance.TelekineticFoldBladeHitDamageRetention), 0.01f, 1f);
        Projectile.velocity *= speedRetention;
        Projectile.damage = Math.Max(1, (int)(Projectile.damage * damageRetention));
        Projectile.netUpdate = true;

        if (!Main.dedServ)
            FranLightingBoltsSystem.SpawnTelekineticWaveImpact(
                Main.rand.NextVector2FromRectangle(target.Hitbox), Projectile.velocity,
                Projectile.scale);

        if (DamagingHitCount >= DamagingHitLimit)
            BeginDissipation(suppressDeathBurst: false);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        // 已到达实体方块：留在接触处收拢闪光，不能把整个波体推进墙内。
        Projectile.velocity = Vector2.Zero;
        BeginDissipation(suppressDeathBurst: true);
        return false;
    }

    private bool IsWaveFrontBlocked()
    {
        float reach = 110f * Projectile.scale;
        for (float side = -1f; side <= 1f; side += 0.5f)
        {
            Vector2 direction = (Projectile.rotation + side * MathHelper.PiOver4 * 0.25f)
                .ToRotationVector2();
            if (Collision.CanHit(Projectile.Center + direction * reach * 0.5f, 0, 0,
                    Projectile.Center + direction * reach, 0, 0))
                return false;
        }
        return true;
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Age);
        writer.Write(DamagingHitCount);
        writer.Write(DissipationAge);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Age = reader.ReadSingle();
        DamagingHitCount = reader.ReadSingle();
        DissipationAge = reader.ReadSingle();
        if (Dissipating)
        {
            Projectile.friendly = false;
            Projectile.damage = 0;
        }
    }

    public override void OnKill(int timeLeft)
    {
        if (!SuppressDeathBurst && !Main.dedServ)
        {
            SoundEngine.PlaySound(FranCombatSounds.TelekineticWaveDeath, Projectile.Center);
            FranLightingBoltsSystem.SpawnTelekineticWaveDissipation(
                Projectile.Center, Projectile.velocity, Projectile.rotation, Projectile.scale);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = MathHelper.Clamp(Projectile.Opacity, 0f, 1f);
        DrawTerraStyleWave(opacity);

        if (ShaderWaveEnabled && !FranProjectileTrailRenderer.TryDrawTelekineticWave(Projectile, opacity))
            FranProjectileTrailRenderer.DrawFallback(Projectile, FranProjectileTrailProfile.TelekineticWave,
                new Color(75, 195, 255), 118f * Projectile.scale, 0.72f * opacity);
        return false;
    }

    private void DrawTerraStyleWave(float opacity)
    {
        Texture2D texture = TextureAssets.Projectile[ProjectileID.TerraBlade2Shot].Value;
        Rectangle source = texture.Frame(1, 4);
        Vector2 origin = source.Size() * 0.5f;
        Vector2 drawPosition = Projectile.Center - Main.screenPosition;
        float drawProgress = Utils.Remap(Age, 0f, ActiveLifetime + 5f, 0f, 1f);
        if (Dissipating && SuppressDeathBurst)
            drawProgress = MathHelper.Lerp(drawProgress, 1f,
                MathHelper.Clamp(DissipationAge / TerrainDissipationLifetime, 0f, 1f));
        float closure = 1f - drawProgress;
        // 仅放大念动弹射的蓝色波体与附着闪光；命中锥体仍读取原始 Projectile.scale。
        float scale = Projectile.scale * VisualScaleMultiplier;
        SpriteEffects first = SwingSign >= 0f ? SpriteEffects.None : SpriteEffects.FlipVertically;
        SpriteEffects second = first ^ SpriteEffects.FlipVertically;

        float lighting = Lighting.GetColor(Projectile.Center.ToTileCoordinates()).ToVector3().Length() /
            MathF.Sqrt(3f);
        lighting = Utils.Remap(0.5f + lighting * 0.5f, 0.2f, 1f, 0f, 1f);

        Color deepBlue = new(18, 68, 155);
        Color bodyBlue = new(35, 170, 255);
        Color edgeBlue = new(135, 230, 255);
        Color ambient = Color.White * opacity * 0.5f;
        ambient.A = (byte)(ambient.A * (1f - lighting));
        ambient *= lighting * 0.5f;

        float opening = SwingSign * MathHelper.PiOver4 * 0.5f * closure;
        Main.EntitySpriteDraw(texture, drawPosition, source, deepBlue * lighting * opacity,
            Projectile.rotation - opening, origin, scale * 0.95f, first);
        Main.EntitySpriteDraw(texture, drawPosition, source, deepBlue * lighting * opacity,
            Projectile.rotation + opening, origin, scale * 0.95f, second);
        Main.EntitySpriteDraw(texture, drawPosition, source, ambient * 0.15f,
            Projectile.rotation + SwingSign * 0.01f, origin, scale, first);
        Main.EntitySpriteDraw(texture, drawPosition, source, ambient * 0.15f,
            Projectile.rotation - SwingSign * 0.01f, origin, scale, second);
        Main.EntitySpriteDraw(texture, drawPosition, source, bodyBlue * lighting * opacity * 0.30f,
            Projectile.rotation + SwingSign * 0.25f * closure, origin, scale, first);
        Main.EntitySpriteDraw(texture, drawPosition, source, bodyBlue * lighting * opacity * 0.30f,
            Projectile.rotation - SwingSign * 0.25f * closure, origin, scale, second);
        Main.EntitySpriteDraw(texture, drawPosition, source, edgeBlue * lighting * opacity * 0.55f,
            Projectile.rotation + SwingSign * 0.15f * closure, origin, scale * 0.975f, first);

        Rectangle coreFrame = texture.Frame(1, 4, 0, 3);
        Main.EntitySpriteDraw(texture, drawPosition, coreFrame, Color.White * (0.72f * opacity),
            Projectile.rotation + SwingSign * 0.05f * closure, origin, scale, first);
        Main.EntitySpriteDraw(texture, drawPosition, coreFrame, Color.White * (0.58f * opacity),
            Projectile.rotation - SwingSign * 0.05f, origin, scale * 0.8f, first);
        Main.EntitySpriteDraw(texture, drawPosition, coreFrame, Color.White * (0.42f * opacity),
            Projectile.rotation - SwingSign * 0.10f, origin, scale * 0.6f, first);

        DrawWaveSparkles(texture, drawPosition, bodyBlue, drawProgress, opacity, scale);

        if (Dissipating && SuppressDeathBurst)
        {
            // 独立于发射淡入和环境亮度的蓝白闪光，贴墙发射也能看清停留的波体。
            float flash = Utils.GetLerpValue(1f, 5f, DissipationAge, true) *
                Utils.GetLerpValue(12f, 5f, DissipationAge, true);
            Color flashColor = new Color(190, 240, 255, 0) * flash;
            Main.EntitySpriteDraw(texture, drawPosition, coreFrame, flashColor,
                Projectile.rotation, origin, scale, first);
            DrawPrettyStarSparkle(flash, drawPosition + Projectile.rotation.ToRotationVector2() *
                70f * Projectile.scale, flashColor, edgeBlue, 0.5f, Projectile.rotation,
                new Vector2(2f, 3f) * scale, Vector2.One * scale);
        }
    }

    private void DrawWaveSparkles(Texture2D waveTexture, Vector2 center, Color color,
        float progress, float opacity, float scale)
    {
        for (float i = -9f; i < 9f; i++)
        {
            float edgeRotation = Projectile.rotation + SwingSign * i * MathHelper.TwoPi * -0.025f;
            Vector2 position = center + edgeRotation.ToRotationVector2() *
                (waveTexture.Width * 0.5f - 6f) * scale;
            float edgeStrength = MathF.Abs(i) / 9f;
            DrawPrettyStarSparkle(opacity, position,
                new Color(255, 255, 255, 0) * (opacity * edgeStrength), color, progress,
                edgeRotation, new Vector2(0f, Utils.Remap(progress, 0f, 1f, 3f, 0f)) * scale,
                Vector2.One * scale);
        }

        for (float side = -1f; side <= 1f; side += 0.5f)
        {
            if (side == 0f)
                continue;
            Vector2 position = center + (Projectile.rotation + side * MathHelper.Pi * 0.75f * progress)
                .ToRotationVector2() * (waveTexture.Width * 0.5f - 4f) * scale;
            float strength = Utils.Remap(MathF.Abs(side), 0f, 1f, 1f, 0.5f);
            Vector2 sparkleScale = new Vector2(Utils.Remap(progress, 0f, 1f, 4f, 1f)) *
                scale * strength;
            DrawPrettyStarSparkle(opacity, position, new Color(255, 255, 255, 0) *
                (opacity * 0.5f), color, progress, MathHelper.PiOver4, sparkleScale,
                Vector2.One * scale * strength);
            DrawPrettyStarSparkle(opacity, position, new Color(255, 255, 255, 0) *
                (opacity * 0.5f), color, progress, 0f,
                new Vector2(2f, Utils.Remap(progress, 0f, 1f, 4f, 1f)) * scale * strength,
                Vector2.One * scale * strength);
        }

        Vector2 tip = center + Projectile.rotation.ToRotationVector2() *
            (waveTexture.Width * 0.5f - 4f) * scale;
        DrawPrettyStarSparkle(opacity, tip, new Color(255, 255, 255, 0) * (opacity * 0.5f),
            color, progress, MathHelper.PiOver4,
            new Vector2(Utils.Remap(progress, 0f, 1f, 4f, 1f)) * scale,
            Vector2.One * scale * 1.5f);
        DrawPrettyStarSparkle(opacity, tip, new Color(255, 255, 255, 0) * (opacity * 0.5f),
            color, progress, 0f,
            new Vector2(2f, Utils.Remap(progress, 0f, 1f, 4f, 1f)) * scale,
            Vector2.One * scale * 1.5f);
    }

    private static void DrawPrettyStarSparkle(float opacity, Vector2 position, Color drawColor,
        Color shineColor, float progress, float rotation, Vector2 scale, Vector2 fatness)
    {
        Texture2D texture = TextureAssets.Extra[ExtrasID.SharpTears].Value;
        Vector2 origin = texture.Size() * 0.5f;
        float fade = Utils.GetLerpValue(0f, 0.5f, progress, true) *
            Utils.GetLerpValue(1f, 0.5f, progress, true);
        Color largeColor = shineColor * (opacity * 0.5f * fade);
        largeColor.A = 0;
        Color smallColor = drawColor * (0.5f * fade);
        Vector2 horizontal = new Vector2(fatness.X * 0.5f, scale.X) * fade;
        Vector2 vertical = new Vector2(fatness.Y * 0.5f, scale.Y) * fade;

        Main.EntitySpriteDraw(texture, position, null, largeColor, MathHelper.PiOver2 + rotation,
            origin, horizontal, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, position, null, largeColor, rotation,
            origin, vertical, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, position, null, smallColor, MathHelper.PiOver2 + rotation,
            origin, horizontal * 0.6f, SpriteEffects.None);
        Main.EntitySpriteDraw(texture, position, null, smallColor, rotation,
            origin, vertical * 0.6f, SpriteEffects.None);
    }

    private void BeginDissipation(bool suppressDeathBurst)
    {
        if (Dissipating)
            return;
        DissipationAge = 1f;
        Projectile.ai[1] = suppressDeathBurst ? 1f : 2f;
        Projectile.friendly = false;
        Projectile.damage = 0;
        Projectile.timeLeft = Math.Min(Projectile.timeLeft,
            suppressDeathBurst ? (int)TerrainDissipationLifetime + 2 : 12);
        Projectile.netUpdate = true;
    }

    private void UpdateDissipation()
    {
        DissipationAge++;
        float lifetime = SuppressDeathBurst ? TerrainDissipationLifetime : 10f;
        // 原版遇阻的三段速度保留：0.94 × 0.8 × 0.88，连续刹车而非瞬间降到 1%。
        Projectile.velocity *= SuppressDeathBurst ? 0.94f * 0.8f * 0.88f : 0.67f;
        if (SuppressDeathBurst && Projectile.velocity.LengthSquared() < 0.25f)
            Projectile.velocity = Vector2.Zero;
        Projectile.Opacity = SuppressDeathBurst
            ? Utils.GetLerpValue(lifetime, 5f, DissipationAge, true)
            : MathHelper.Clamp(1f - DissipationAge / lifetime, 0f, 1f);
        Projectile.rotation = Projectile.velocity.SafeNormalize(
            Projectile.rotation.ToRotationVector2()).ToRotation();
        if (SuppressDeathBurst)
            Lighting.AddLight(Projectile.Center, new Vector3(0.22f, 0.68f, 1f) * Projectile.Opacity);
        if (DissipationAge >= lifetime)
            Projectile.Kill();
    }
}

// 困难模式后才进入状态机的高速蓝色激光。其 15 次子更新保留利落飞行，
// 而命中耗尽后的减速与淡出每个真实帧只推进一次。
public sealed class Telekinetic_Flash : ModProjectile
{
    private ref float Time => ref Projectile.ai[0];
    private ref float DamagingHitCount => ref Projectile.ai[1];
    private ref float DissipationAge => ref Projectile.ai[2];
    private int DamagingHitLimit => Math.Max(1, (int)Projectile.localAI[0]);
    private bool SuppressLaunchBurst => Projectile.localAI[1] > 0f;
    private bool Dissipating => DissipationAge > 0f;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 32;
        Projectile.height = 32;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 2;
        Projectile.MaxUpdates = 15;
        Projectile.timeLeft = 20 * Projectile.MaxUpdates;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool? CanDamage() => Dissipating ? false : null;

    public override void AI()
    {
        EnsureConfigured();
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (Dissipating)
        {
            UpdateDissipation();
            return;
        }

        Time++;

        bool drawingUpdate = Projectile.numUpdates % 3 == 0;
        if (Time > 6f && drawingUpdate && !Main.dedServ)
        {
            float scaleBoost = MathHelper.Clamp(Time * 0.005f, 0f, 2f);
            FranParticleSystem.Spawn(new FranHalleySparkParticle(Projectile.Center, Projectile.velocity,
                new Color(8, 35, 156), (3.2f + scaleBoost) * Projectile.scale, 7));
            FranParticleSystem.Spawn(new FranHalleySparkParticle(Projectile.Center, Projectile.velocity,
                new Color(184, 215, 245), (1.6f + scaleBoost) * Projectile.scale, 7));
        }
        else if (Time == 5f && !Main.dedServ && !SuppressLaunchBurst)
        {
            FranParticleSystem.Spawn(new FranHalleyPulseRingParticle(Projectile.Center,
                Projectile.velocity * 0.75f, Color.Aqua, new Vector2(1f, 2.5f) * Projectile.scale,
                Projectile.rotation,
                0.20f, 0.03f, 20));
            FranParticleSystem.Spawn(new FranHalleyPulseRingParticle(Projectile.Center,
                Projectile.velocity * 0.40f, Color.DodgerBlue, new Vector2(1f, 2.5f) * Projectile.scale,
                Projectile.rotation,
                0.10f, 0.025f, 35));
            for (int i = 0; i <= 25; i++)
            {
                Dust wide = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? 172 : 206,
                    Projectile.velocity.RotatedByRandom(0.4f) * Main.rand.NextFloat(0.3f, 1.6f));
                wide.scale = Main.rand.NextFloat(1.6f, 2.5f) * Projectile.scale;
                wide.noGravity = true;
                Dust needle = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? 172 : 206,
                    Projectile.velocity.RotatedByRandom(0.06f) * Main.rand.NextFloat(0.8f, 3.1f));
                needle.scale = Main.rand.NextFloat(1.35f, 2.1f) * Projectile.scale;
                needle.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.62f, Pitch = 0.26f }, Projectile.Center);
        }

        if (Projectile.numUpdates == 0)
            Lighting.AddLight(Projectile.Center, Color.MediumBlue.ToVector3() * 0.4f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        EnsureConfigured();
        SoundEngine.PlaySound(SoundID.DD2_CrystalCartImpact with { Volume = 0.64f }, Projectile.Center);
        for (int i = 0; i < 14; i++)
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center, Main.rand.NextBool(3) ? 172 : 206,
                Projectile.velocity.RotatedByRandom(0.5f) * Main.rand.NextFloat(0.2f, 2.1f));
            dust.scale = Main.rand.NextFloat(1.1f, 1.9f) * Projectile.scale;
            dust.noGravity = true;
        }

        DamagingHitCount++;
        if (DamagingHitCount >= DamagingHitLimit)
            BeginDissipation();
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = MathHelper.Clamp(Projectile.Opacity, 0f, 1f);
        Vector2 direction = Projectile.velocity.SafeNormalize(Projectile.rotation.ToRotationVector2());
        float rotation = direction.ToRotation();
        float pulse = 0.92f + MathF.Sin(Main.GlobalTimeWrappedHourly * 18f + Projectile.identity) * 0.08f;

        FranVfx.BeginAdditive();
        FranVfx.DrawShared("CL_BloomCircle", Projectile.Center,
            new Color(45, 135, 255, 0) * (0.52f * opacity), rotation,
            0.48f * pulse * Projectile.scale);
        FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center - direction * 18f,
            new Color(65, 175, 255) * (0.48f * opacity), rotation,
            new Vector2(104f, 24f) * pulse * Projectile.scale);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center + direction * 12f,
            Color.White * (0.78f * opacity), rotation,
            new Vector2(38f, 20f) * pulse * Projectile.scale);
        FranVfx.EndAdditive();
        return false;
    }

    private void EnsureConfigured()
    {
        if (Projectile.localAI[0] > 0f)
            return;

        Projectile.localAI[0] = FranBalance.GetTelekineticLaserDamagingHits();
        Projectile.localAI[1] = DissipationAge < 0f ? 1f : 0f;
        DissipationAge = 0f;
        Projectile.penetrate = DamagingHitLimit + 1;
        Projectile.Opacity = 1f;
    }

    private void BeginDissipation()
    {
        if (Dissipating)
            return;

        // The extra penetrate point is deliberately non-damaging: it keeps the projectile alive
        // long enough to visibly lose momentum instead of being deleted on its final real hit.
        DissipationAge = 1f;
        Projectile.friendly = false;
        Projectile.damage = 0;
        Projectile.velocity *= 0.42f;
        Projectile.timeLeft = Math.Min(Projectile.timeLeft, 9 * Projectile.MaxUpdates);
        Projectile.netUpdate = true;
    }

    private void UpdateDissipation()
    {
        if (Projectile.numUpdates != 0)
            return;

        DissipationAge++;
        Projectile.velocity *= 0.62f;
        Projectile.Opacity = MathHelper.Clamp(1f - DissipationAge / 9f, 0f, 1f);
        Lighting.AddLight(Projectile.Center, Color.MediumBlue.ToVector3() * (0.24f * Projectile.Opacity));
        if (DissipationAge >= 9f)
            Projectile.Kill();
    }
}
