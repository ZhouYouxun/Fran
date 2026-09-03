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
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


// 每次确认命中后延迟展开裂隙。黑色主体使用 AlphaBlend 保持明亮背景下的可读性，
// 紫色与青色边缘使用 Additive；终结段在同一结构上增加垂直裂隙。
public sealed class FranUltimateBlackRiftLaser : ModProjectile
{
    private const int Lifetime = 24;
    private const int WindupFrames = 7;
    private const float CollisionHalfLength = 2300f;
    private const float VisualHalfLength = 1050f;

    private int Age => Lifetime - Projectile.timeLeft;
    private bool Final => Projectile.ai[1] > 0.5f;
    private Vector2 Axis => Projectile.ai[0].ToRotationVector2().SafeNormalize(Vector2.UnitY);

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

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
        Projectile.netImportant = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;

    public override bool? CanDamage() => Age >= WindupFrames ? null : false;

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (Age < WindupFrames)
            return false;

        float width = Final ? 72f : 44f;
        if (AxisHits(targetHitbox, Axis, width))
            return true;
        return Final && AxisHits(targetHitbox, Axis.RotatedBy(MathHelper.PiOver2), width * 0.82f);
    }

    private bool AxisHits(Rectangle targetHitbox, Vector2 axis, float width)
    {
        float collisionPoint = 0f;
        Vector2 start = Projectile.Center - axis * CollisionHalfLength;
        Vector2 end = Projectile.Center + axis * CollisionHalfLength;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            start, end, width, ref collisionPoint);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            FranBlackTigerImpactVisual.Spawn(Projectile.GetSource_OnHit(target), target.Center,
                Axis, Projectile.owner, Final ? 1 : 0);
    }

    public override void AI()
    {
        Color corona = Final ? new Color(193, 116, 255) : new Color(132, 72, 214);
        Lighting.AddLight(Projectile.Center, corona.ToVector3() * (Age >= WindupFrames ? 0.36f : 0.12f));
        if (Main.dedServ)
            return;

        if (Projectile.localAI[0] == 0f)
        {
            Projectile.localAI[0] = 1f;
            SpawnTelegraph(corona);
        }

        if (Projectile.localAI[1] == 0f && Age >= WindupFrames)
        {
            Projectile.localAI[1] = 1f;
            SpawnRelease(corona);
        }
    }

    private void SpawnTelegraph(Color corona)
    {
        FranParticleEffects.RingPulse(Projectile.Center, corona, Vector2.One * 10f,
            new Vector2(Final ? 112f : 72f, Final ? 38f : 24f), WindupFrames + 4,
            Axis.ToRotation(), 0.018f, Final ? 0.82f : 0.58f);
        if (Final || (int)Projectile.ai[2] % 3 == 0)
            FranParticleEffects.MysticGlyphs(Projectile.Center, corona, Final ? 6 : 2,
                Final ? 104f : 66f, Final ? 1.08f : 0.72f);
    }

    private void SpawnRelease(Color corona)
    {
        float scale = Final ? 1.72f : 1f;
        FranParticleEffects.BlackLight(Projectile.Center, corona, scale, Final ? 28 : 20);
        FranParticleEffects.RingPulse(Projectile.Center, corona,
            new Vector2(18f, 10f) * scale, new Vector2(184f, 84f) * scale,
            Final ? 28 : 20, Axis.ToRotation(), -0.024f, 0.92f);
        FranParticleEffects.NeedleFan(Projectile.Center, Axis, corona,
            Final ? 18 : 9, Final ? 11.5f : 8f, Final ? 1.30f : 0.88f, 0.34f);
        FranParticleEffects.NeedleFan(Projectile.Center, -Axis, corona,
            Final ? 18 : 9, Final ? 11.5f : 8f, Final ? 1.30f : 0.88f, 0.34f);

        int pathCount = Final ? 18 : 11;
        for (int i = 0; i < pathCount; i++)
        {
            float offset = MathHelper.Lerp(-VisualHalfLength * 0.86f, VisualHalfLength * 0.86f,
                (i + 0.5f) / pathCount);
            Vector2 position = Projectile.Center + Axis * offset +
                Axis.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-16f, 16f) * scale;
            FranParticleSystem.Spawn(new FranBlackLightCoreParticle(position,
                Axis * Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(8f, 14f) * scale,
                Main.rand.NextFloat(32f, 52f) * scale, Main.rand.Next(12, 19),
                Main.rand.NextFloat(0.58f, 0.82f), Axis.ToRotation(), Main.rand.NextFloat(-0.035f, 0.035f)));
            if (i % 2 == 0)
            {
                Vector2 velocity = Axis.RotatedByRandom(0.28f) * Main.rand.NextFloat(2.5f, 7.5f);
                FranParticleSystem.Spawn(new FranBladeEdgeMoteParticle(position, velocity, Axis,
                    Color.Lerp(corona, Color.White, 0.24f), Main.rand.NextFloat(22f, 48f) * scale,
                    Main.rand.NextFloat(1.4f, 2.8f) * scale, Main.rand.Next(10, 17)));
            }
        }

        int customEstimate = 3 + (Final ? 36 : 18) + pathCount + (pathCount + 1) / 2;
        FranVanillaDustEffects.Impact(Projectile.Center, Axis, FranSkill.SkillTaker, corona,
            customEstimate, Final ? 1.38f : 0.98f);
        SoundEngine.PlaySound(SoundID.Item122 with
        {
            Volume = Final ? 0.92f : 0.54f,
            Pitch = Final ? -0.48f : -0.18f + ((int)Projectile.ai[2] % 4) * 0.035f
        }, Projectile.Center);
        if (Final)
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.78f, Pitch = -0.56f }, Projectile.Center);
        Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, Final ? 16f : 6f, 2600f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float telegraph = MathHelper.Clamp(Age / (float)WindupFrames, 0f, 1f);
        float activeProgress = MathHelper.Clamp((Age - WindupFrames) /
            (float)Math.Max(1, Lifetime - WindupFrames), 0f, 1f);
        bool active = Age >= WindupFrames;
        float opacity = active
            ? MathF.Pow(1f - activeProgress, 1.12f) * (0.88f + 0.12f * MathF.Sin(activeProgress * MathHelper.Pi))
            : MathHelper.Lerp(0.18f, 0.52f, FranMotion.Smooth(telegraph));
        float snap = active ? 1f : MathHelper.Lerp(0.12f, 0.24f, telegraph);
        float width = (Final ? 84f : 56f) * snap;
        float halfLength = Final ? VisualHalfLength + 90f : VisualHalfLength;

        DrawBlackAxis(Axis, halfLength, width, opacity);
        if (Final)
            DrawBlackAxis(Axis.RotatedBy(MathHelper.PiOver2), halfLength * 0.82f, width * 0.78f, opacity * 0.92f);
        FranVfx.DrawSharedPixels("HighResFoggyCircleHardEdge", Projectile.Center,
            new Color(0, 0, 0, 235) * opacity, Projectile.ai[0] + activeProgress,
            Vector2.One * (Final ? 186f : 122f) * snap);

        FranVfx.BeginAdditive();
        Color violet = Final ? new Color(205, 126, 255) : new Color(137, 77, 225);
        DrawEmissiveAxis(Axis, halfLength, width, violet, opacity, activeProgress);
        if (Final)
            DrawEmissiveAxis(Axis.RotatedBy(MathHelper.PiOver2), halfLength * 0.82f,
                width * 0.78f, new Color(116, 171, 255), opacity * 0.88f, activeProgress);
        FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
            violet * (0.72f * opacity), Projectile.ai[0],
            new Vector2(Final ? 282f : 164f, Final ? 122f : 74f) * snap);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
            Color.White * (0.62f * opacity), Projectile.ai[0],
            new Vector2(Final ? 228f : 118f, Final ? 44f : 24f) * snap);
        FranVfx.EndAdditive();
        return false;
    }

    private void DrawBlackAxis(Vector2 axis, float halfLength, float width, float opacity)
    {
        Vector2 center = Projectile.Center - Main.screenPosition;
        Vector2 start = center - axis * halfLength;
        Vector2 end = center + axis * halfLength;
        DrawLine(start, center, new Color(38, 9, 55, 224) * (opacity * 0.92f), width * 1.34f);
        DrawLine(center, end, new Color(38, 9, 55, 224) * (opacity * 0.92f), width * 1.34f);
        DrawLine(start, center, new Color(0, 0, 0, 246) * opacity, width);
        DrawLine(center, end, new Color(0, 0, 0, 246) * opacity, width);
        DrawLine(start, center, new Color(7, 2, 12, 250) * opacity, width * 0.62f);
        DrawLine(center, end, new Color(7, 2, 12, 250) * opacity, width * 0.62f);
    }

    private void DrawEmissiveAxis(Vector2 axis, float halfLength, float width, Color color,
        float opacity, float activeProgress)
    {
        Vector2 center = Projectile.Center - Main.screenPosition;
        Vector2 start = center - axis * halfLength;
        Vector2 end = center + axis * halfLength;
        Vector2 normal = axis.RotatedBy(MathHelper.PiOver2);
        float edgeOffset = width * 0.56f;
        float edgeWidth = Math.Max(1.5f, width * 0.085f);
        DrawLine(start + normal * edgeOffset, center + normal * edgeOffset,
            color * (0.72f * opacity), edgeWidth);
        DrawLine(center + normal * edgeOffset, end + normal * edgeOffset,
            color * (0.72f * opacity), edgeWidth);
        DrawLine(start - normal * edgeOffset, center - normal * edgeOffset,
            color * (0.72f * opacity), edgeWidth);
        DrawLine(center - normal * edgeOffset, end - normal * edgeOffset,
            color * (0.72f * opacity), edgeWidth);

        for (int i = -2; i <= 2; i++)
        {
            Vector2 point = Projectile.Center + axis * i * 410f;
            float flicker = 0.64f + 0.36f * MathF.Sin(Projectile.identity * 0.73f + i * 1.8f + activeProgress * 18f);
            FranVfx.DrawOriginalPixels("FranSmearForward", point,
                color * (0.18f * opacity * flicker), axis.ToRotation(),
                new Vector2(500f, Math.Max(8f, width * 1.42f)));
        }
    }

    private static void DrawLine(Vector2 start, Vector2 end, Color color, float width)
    {
        Vector2 edge = end - start;
        if (edge.LengthSquared() < 0.01f || !float.IsFinite(width))
            return;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, start, new Rectangle(0, 0, 1, 1), color,
            edge.ToRotation(), new Vector2(0f, 0.5f), new Vector2(edge.Length(), Math.Max(0.5f, width)),
            SpriteEffects.None, 0f);
    }
}
