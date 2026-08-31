using System;
using System.Collections.Generic;
using FranMod.Content.Players;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

internal static class FranVfx
{
    private const string SharedRoot = "Fran/Textures/Particles/";
    private const string OriginalRoot = "Fran/Assets/OriginalVFX/";
    private const float MaximumDrawSegment = 1200f;
    private const float MaximumSpriteScale = 96f;
    private const float MaximumSpriteDimension = 520f;

    public static Texture2D Shared(string name) => ModContent.Request<Texture2D>(SharedRoot + name).Value;
    public static Texture2D Original(string name) => ModContent.Request<Texture2D>(OriginalRoot + name).Value;

    public static void BeginAdditive()
    {
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
    }

    public static void EndAdditive()
    {
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
    }

    // Local, bounded shader pulse. This follows LunarViel's immediate shader-pass pattern, but keeps
    // every sprite under Fran's 520 px safety ceiling so a bad parameter cannot become a screen-long quad.
    public static void DrawShaderPulse(string textureName, Vector2 worldPosition, Color color,
        float rotation, Vector2 desiredSize, float noiseScale = 1f, bool staticLines = true)
    {
        desiredSize = new Vector2(MathHelper.Clamp(MathF.Abs(desiredSize.X), 2f, 256f),
            MathHelper.Clamp(MathF.Abs(desiredSize.Y), 2f, 256f));
        if (!FranModMain.SlashShaderAvailable || FranModMain.SlashEffect?.Value is not Effect effect ||
            !IsFinite(worldPosition) || !IsFinite(desiredSize))
        {
            DrawSharedPixels(textureName, worldPosition, color * 0.52f, rotation, desiredSize);
            return;
        }

        Texture2D texture = Shared(textureName);
        Main.spriteBatch.End();
        bool shaderBatchStarted = false;
        bool drawn = false;
        try
        {
            effect.Parameters["uColor"]?.SetValue(color.ToVector3());
            effect.Parameters["uSecondaryColor"]?.SetValue(Color.White.ToVector3());
            effect.Parameters["uOpacity"]?.SetValue(1f);
            effect.Parameters["uSaturation"]?.SetValue(1f);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["coordinateZoomFactor"]?.SetValue(MathHelper.Clamp(noiseScale, 0.25f, 12f));
            effect.Parameters["useStaticLine"]?.SetValue(staticLines);
            effect.Parameters["useTrueNoise"]?.SetValue(true);
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            shaderBatchStarted = true;
            Main.spriteBatch.Draw(texture, worldPosition - Main.screenPosition, null, Color.White, rotation,
                texture.Size() * 0.5f, new Vector2(desiredSize.X / Math.Max(1f, texture.Width),
                    desiredSize.Y / Math.Max(1f, texture.Height)), SpriteEffects.None, 0f);
            drawn = true;
        }
        catch
        {
            // Shader failure is visual-only; restore Terraria's batch and use the bounded texture fallback below.
        }
        finally
        {
            if (shaderBatchStarted)
                Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
        if (!drawn)
            DrawSharedPixels(textureName, worldPosition, color * 0.52f, rotation, desiredSize);
    }

    public static void DrawShared(string name, Vector2 worldPosition, Color color, float rotation, Vector2 scale)
    {
        if (!IsFinite(worldPosition) || !float.IsFinite(rotation) || !IsFinite(scale) ||
            MathF.Abs(scale.X) > MaximumSpriteScale || MathF.Abs(scale.Y) > MaximumSpriteScale)
            return;

        Texture2D texture = Shared(name);
        float renderedWidth = texture.Width * MathF.Abs(scale.X);
        float renderedHeight = texture.Height * MathF.Abs(scale.Y);
        if (!float.IsFinite(renderedWidth) || !float.IsFinite(renderedHeight) ||
            renderedWidth > MaximumSpriteDimension || renderedHeight > MaximumSpriteDimension)
            return;
        Main.EntitySpriteDraw(texture, worldPosition - Main.screenPosition, null, color, rotation,
            texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
    }

    public static void DrawShared(string name, Vector2 worldPosition, Color color, float rotation, float scale) =>
        DrawShared(name, worldPosition, color, rotation, Vector2.One * scale);

    public static void DrawOriginal(string name, Vector2 worldPosition, Color color, float rotation, Vector2 scale)
    {
        if (!IsFinite(worldPosition) || !float.IsFinite(rotation) || !IsFinite(scale) ||
            MathF.Abs(scale.X) > MaximumSpriteScale || MathF.Abs(scale.Y) > MaximumSpriteScale)
            return;

        Texture2D texture = Original(name);
        float renderedWidth = texture.Width * MathF.Abs(scale.X);
        float renderedHeight = texture.Height * MathF.Abs(scale.Y);
        if (!float.IsFinite(renderedWidth) || !float.IsFinite(renderedHeight) ||
            renderedWidth > MaximumSpriteDimension || renderedHeight > MaximumSpriteDimension)
            return;
        Main.EntitySpriteDraw(texture, worldPosition - Main.screenPosition, null, color, rotation,
            texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
    }

    public static void DrawOriginal(string name, Vector2 worldPosition, Color color, float rotation, float scale) =>
        DrawOriginal(name, worldPosition, color, rotation, Vector2.One * scale);

    public static Vector2 ScaleForPixels(string name, Vector2 desiredSize)
    {
        Texture2D texture = Shared(name);
        return new Vector2(desiredSize.X / Math.Max(1f, texture.Width),
            desiredSize.Y / Math.Max(1f, texture.Height));
    }

    public static void DrawSharedPixels(string name, Vector2 worldPosition, Color color, float rotation,
        Vector2 desiredSize) => DrawShared(name, worldPosition, color, rotation, ScaleForPixels(name, desiredSize));

    public static Vector2 OriginalScaleForPixels(string name, Vector2 desiredSize)
    {
        Texture2D texture = Original(name);
        return new Vector2(desiredSize.X / Math.Max(1f, texture.Width),
            desiredSize.Y / Math.Max(1f, texture.Height));
    }

    public static void DrawOriginalPixels(string name, Vector2 worldPosition, Color color, float rotation,
        Vector2 desiredSize) => DrawOriginal(name, worldPosition, color, rotation,
            OriginalScaleForPixels(name, desiredSize));

    public static void DrawStretched(string name, Vector2 start, Vector2 end, Color color, float width)
    {
        if (!TryGetSafeSegment(start, end, out Vector2 edge, out float length) ||
            !float.IsFinite(width))
            return;
        Texture2D texture = Shared(name);
        Vector2 scale = new(length / Math.Max(1f, texture.Width),
            MathHelper.Clamp(MathF.Abs(width), 0.5f, 768f) / Math.Max(1f, texture.Height));
        Main.EntitySpriteDraw(texture, start - Main.screenPosition, null, color, edge.ToRotation(),
            new Vector2(0f, texture.Height * 0.5f), scale, SpriteEffects.None, 0f);
    }

    public static void DrawVertical(string name, Vector2 start, Vector2 end, Color color, float width)
    {
        if (!TryGetSafeSegment(start, end, out Vector2 edge, out float length) ||
            !float.IsFinite(width))
            return;
        Texture2D texture = Shared(name);
        Vector2 scale = new(MathHelper.Clamp(MathF.Abs(width), 0.5f, 768f) / Math.Max(1f, texture.Width),
            length / Math.Max(1f, texture.Height));
        Main.EntitySpriteDraw(texture, (start + end) * 0.5f - Main.screenPosition, null, color,
            edge.ToRotation() - MathHelper.PiOver2, texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
    }

    public static void DrawBloom(Vector2 center, Color color, float radius, float opacity = 1f, bool hard = false)
    {
        Texture2D texture = Shared(hard ? "HardBloom" : "SoftBloom");
        float scale = radius * 2f / Math.Max(texture.Width, texture.Height);
        DrawShared(hard ? "HardBloom" : "SoftBloom", center, color * opacity, 0f, scale);
    }

    public static void DrawRing(Vector2 center, Color color, float radius, float opacity, float rotation = 0f,
        string textureName = "EnergyRing")
    {
        Texture2D texture = Shared(textureName);
        float scale = radius * 2f / Math.Max(texture.Width, texture.Height);
        DrawShared(textureName, center, color * opacity, rotation, scale);
    }

    // Calamity-style laser language: a broad colored halo, a tighter body and an overexposed white core.
    // All three layers use real greyscale beam textures; none of the beam is assembled from a single-pixel strip.
    public static void DrawCappedLaser(Vector2 start, Vector2 end, Color haloColor, Color bodyColor,
        float width, float opacity)
    {
        if (!TryGetSafeSegment(start, end, out Vector2 edge, out _) ||
            !float.IsFinite(width) || !float.IsFinite(opacity) || width <= 0f || opacity <= 0f)
            return;

        width = MathHelper.Clamp(width, 0.5f, 256f);
        opacity = MathHelper.Clamp(opacity, 0f, 2f);

        DrawVertical("Cal_AresLaserMiddle", start, end, haloColor * (0.34f * opacity), width * 2.8f);
        DrawVertical("Cal_AresLaserMiddle", start, end, bodyColor * (0.78f * opacity), width * 1.12f);
        DrawVertical("Cal_AresLaserMiddle", start, end, Color.White * (0.92f * opacity), width * 0.34f);

        float capScale = width * 2.55f / Math.Max(1f, Shared("Cal_AresLaserEnd").Width);
        float rotation = edge.ToRotation() - MathHelper.PiOver2;
        DrawShared("Cal_AresLaserEnd", start, haloColor * (0.42f * opacity), rotation + MathHelper.Pi,
            new Vector2(capScale, capScale * 0.82f));
        DrawShared("Cal_AresLaserEnd", end, bodyColor * (0.72f * opacity), rotation,
            new Vector2(capScale, capScale * 0.82f));
    }

    // A jagged bolt assembled from a tapered greyscale streak. The geometry may be segmented,
    // but every visible segment has a textured falloff and a colored-halo/white-core hierarchy.
    public static void DrawTexturedLightning(Vector2 start, Vector2 end, Color color, float width,
        int segments, float seed, float opacity = 1f)
    {
        if (!TryGetSafeSegment(start, end, out Vector2 edge, out _) ||
            !float.IsFinite(width) || !float.IsFinite(seed) || !float.IsFinite(opacity) ||
            width <= 0f || opacity <= 0f)
            return;

        width = MathHelper.Clamp(width, 0.5f, 96f);
        opacity = MathHelper.Clamp(opacity, 0f, 2f);
        segments = Math.Clamp(segments, 2, 64);
        Vector2 normal = edge.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
        Vector2 previous = start;
        for (int i = 1; i <= segments; i++)
        {
            float completion = i / (float)segments;
            float envelope = MathF.Sin(completion * MathHelper.Pi);
            float jitterA = MathF.Sin(seed * 1.73f + i * 12.9898f);
            float jitterB = MathF.Sin(seed * 0.61f + i * 7.318f) * 0.42f;
            Vector2 next = Vector2.Lerp(start, end, completion) + normal *
                (jitterA + jitterB) * envelope * (5f + width * 0.9f);
            DrawVertical("Cal_ThinEndedLine", previous, next, color * (0.36f * opacity), width * 3.2f);
            DrawVertical("Cal_ThinEndedLine", previous, next, color * (0.86f * opacity), width * 1.22f);
            DrawVertical("Cal_ThinEndedLine", previous, next, Color.White * (0.92f * opacity), width * 0.34f);
            previous = next;
        }
    }

    public static void DrawTexturedSpark(Vector2 center, Vector2 direction, Color color, float length,
        float width, float opacity)
    {
        if (!IsFinite(center) || !IsFinite(direction) || !float.IsFinite(length) ||
            !float.IsFinite(width) || !float.IsFinite(opacity) || length <= 0f || width <= 0f || opacity <= 0f)
            return;
        length = MathHelper.Clamp(length, 1f, 512f);
        width = MathHelper.Clamp(width, 0.5f, 96f);
        opacity = MathHelper.Clamp(opacity, 0f, 2f);
        direction = direction.SafeNormalize(Vector2.UnitY);
        Vector2 start = center - direction * length * 0.24f;
        Vector2 end = center + direction * length * 0.76f;
        DrawVertical("Cal_ThinEndedLine", start, end, color * (0.42f * opacity), width * 2.6f);
        DrawVertical("Cal_ThinEndedLine", start, end, Color.White * (0.82f * opacity), width * 0.58f);
    }

    public static void DustLine(Vector2 start, Vector2 end, int dustType, float spacing = 10f, float scale = 1.2f)
    {
        if (Main.dedServ || !TryGetSafeSegment(start, end, out _, out float distance) ||
            !float.IsFinite(spacing) || !float.IsFinite(scale))
            return;
        spacing = MathHelper.Clamp(MathF.Abs(spacing), 2f, 128f);
        scale = MathHelper.Clamp(MathF.Abs(scale), 0.1f, 6f);
        int count = Math.Max(1, (int)(distance / spacing));
        for (int i = 0; i <= count; i++)
        {
            Dust dust = Dust.NewDustPerfect(Vector2.Lerp(start, end, i / (float)count), dustType, Vector2.Zero, 80, default, scale);
            dust.noGravity = true;
        }
    }

    public static void Burst(Vector2 center, int dustType, int count, float speed, float scale = 1.3f)
    {
        if (Main.dedServ)
            return;
        Color color = dustType switch
        {
            DustID.Torch or DustID.GoldFlame => new Color(255, 108, 45),
            DustID.PurpleTorch or DustID.Shadowflame => new Color(175, 105, 255),
            DustID.Electric => new Color(145, 190, 255),
            _ => new Color(115, 215, 255)
        };
        FranParticleEffects.SpawnImpact(center, Main.rand.NextVector2Unit(), color,
            Math.Max(6, count), speed, MathHelper.Clamp(scale, 0.65f, 1.7f));
        for (int i = 0; i < Math.Max(2, count / 4); i++)
        {
            Dust dust = Dust.NewDustPerfect(center, dustType, Main.rand.NextVector2Circular(speed, speed), 70, default,
                Main.rand.NextFloat(scale * 0.75f, scale * 1.25f));
            dust.noGravity = true;
        }
    }

    public static void SparkBurst(Vector2 center, Color color, int count, float speed, float scale = 1f)
    {
        if (Main.dedServ)
            return;
        FranParticleEffects.SpawnImpact(center, Main.rand.NextVector2Unit(), color,
            Math.Max(5, count), speed, MathHelper.Clamp(scale, 0.65f, 1.7f));
        for (int i = 0; i < Math.Max(1, count / 4); i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(speed * 0.35f, speed);
            Dust dust = Dust.NewDustPerfect(center, DustID.FireworksRGB, velocity, 40, color,
                Main.rand.NextFloat(0.6f, 1.05f) * scale);
            dust.noGravity = true;
            dust.fadeIn = 0.35f;
        }
    }

    public static NPC ClosestTarget(Vector2 center, float maxDistance, int ignored = -1)
    {
        NPC best = null;
        float bestDistance = maxDistance * maxDistance;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            float distance = Vector2.DistanceSquared(center, npc.Center);
            if (npc.whoAmI != ignored && npc.CanBeChasedBy() && distance < bestDistance)
            {
                best = npc;
                bestDistance = distance;
            }
        }
        return best;
    }

    private static bool TryGetSafeSegment(Vector2 start, Vector2 end, out Vector2 edge, out float length)
    {
        edge = end - start;
        length = 0f;
        if (!IsFinite(start) || !IsFinite(end) || !IsFinite(edge))
            return false;

        float lengthSquared = edge.LengthSquared();
        if (!float.IsFinite(lengthSquared) || lengthSquared <= 0.1f ||
            lengthSquared > MaximumDrawSegment * MaximumDrawSegment)
            return false;

        length = MathF.Sqrt(lengthSquared);
        return float.IsFinite(length);
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}

internal static class FranMotion
{
    public static float Clamp01(float value) => MathHelper.Clamp(value, 0f, 1f);

    public static float Segment(float value, float start, float end) =>
        Clamp01((value - start) / Math.Max(0.0001f, end - start));

    public static float Smooth(float value)
    {
        value = Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    public static float EaseInCubic(float value)
    {
        value = Clamp01(value);
        return value * value * value;
    }

    public static float EaseOutCubic(float value)
    {
        value = 1f - Clamp01(value);
        return 1f - value * value * value;
    }

    public static float EaseOutQuart(float value)
    {
        value = 1f - Clamp01(value);
        return 1f - value * value * value * value;
    }

    public static float EaseInOutCubic(float value)
    {
        value = Clamp01(value);
        return value < 0.5f ? 4f * value * value * value : 1f - MathF.Pow(-2f * value + 2f, 3f) * 0.5f;
    }

    public static float EaseOutBack(float value)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float shifted = Clamp01(value) - 1f;
        return 1f + c3 * shifted * shifted * shifted + c1 * shifted * shifted;
    }

    public static float Pulse(float value, float fadeInStart, float fadeInEnd, float fadeOutStart, float fadeOutEnd) =>
        Smooth(Segment(value, fadeInStart, fadeInEnd)) * (1f - Smooth(Segment(value, fadeOutStart, fadeOutEnd)));
}

public sealed class FloatingTeacherVisual : ModProjectile
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.BreakerBlade}";

    public override void SetDefaults()
    {
        Projectile.width = 30;
        Projectile.height = 60;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }

    public override bool? CanDamage() => false;

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        if (!owner.active || owner.dead || !fran.HasTeacher || fran.TelekineticFlightVisualActive ||
            owner.HeldItem.type == ModContent.ItemType<Content.Items.Teacher>() ||
            owner.HeldItem.type == ModContent.ItemType<Content.Items.TeacherExcavationTool>())
        {
            Projectile.Kill();
            return;
        }

        Vector2 target = owner.Center + new Vector2(-owner.direction * 32f, -30f + MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 5f);
        Projectile.Center = Vector2.Lerp(Projectile.Center, target, 0.18f);
        Projectile.rotation = -0.62f * owner.direction;
        Projectile.timeLeft = 2;
        Lighting.AddLight(Projectile.Center, 0.08f, 0.18f, 0.28f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Player owner = Main.player[Projectile.owner];
        Texture2D texture = TextureAssets.Item[ItemID.BreakerBlade].Value;
        Vector2 position = Projectile.Center - Main.screenPosition;
        Vector2 origin = texture.Size() * 0.5f;
        SpriteEffects effects = owner.direction == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        for (int i = 0; i < 5; i++)
            Main.EntitySpriteDraw(texture, position + (MathHelper.TwoPi * i / 5f).ToRotationVector2() * 2f, null,
                new Color(100, 205, 255) * 0.28f, Projectile.rotation, origin, 0.82f, effects, 0f);
        Main.EntitySpriteDraw(texture, position, null, lightColor, Projectile.rotation, origin, 0.82f, effects, 0f);
        return false;
    }
}

public sealed class TelekineticCannonProjectile : ModProjectile
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.BreakerBlade}";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 10;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 36; Projectile.height = 36; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 70;
        Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool? CanDamage() => Projectile.ai[0] == 0f ? null : false;

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }

        float age = Projectile.localAI[0]++;
        if (Projectile.ai[0] == 0f)
        {
            if (age < 7f && Projectile.velocity.Length() < 22f)
                Projectile.velocity *= 1.055f;
            if (age >= 20f || Vector2.DistanceSquared(Projectile.Center, owner.Center) > 680f * 680f)
            {
                Projectile.ai[0] = 1f;
                Projectile.tileCollide = false;
                Projectile.netUpdate = true;
            }
        }
        else
        {
            Vector2 toOwner = owner.MountedCenter - Projectile.Center;
            if (toOwner.LengthSquared() < 38f * 38f)
            {
                Projectile.Kill();
                return;
            }
            Vector2 desired = toOwner.SafeNormalize(Vector2.UnitX * owner.direction) * 23f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.20f);
        }

        Lighting.AddLight(Projectile.Center, 0.1f, 0.42f, 0.65f);
        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        if (!Main.dedServ && age > 2f)
            FranParticleEffects.StarlightWake(Projectile.Center, Projectile.velocity,
                Projectile.ai[0] == 0f ? new Color(135, 225, 255) : new Color(95, 155, 255));
        if (age > 3f && Main.rand.NextBool(4))
        {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch,
                -Projectile.velocity * 0.04f + Main.rand.NextVector2Circular(0.8f, 0.8f), 70, default, 1.05f);
            d.noGravity = true;
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float speedRatio = FranMotion.Clamp01((Projectile.velocity.Length() - 10f) / 13f);
        Color deepColor = new(35, 85, 210);
        Color edgeColor = Color.Lerp(new Color(55, 145, 255), new Color(185, 245, 255), speedRatio);
        float pulse = 0.86f + MathF.Sin(Main.GlobalTimeWrappedHourly * 18f + Projectile.identity) * 0.08f;
        float opacity = FranMotion.Smooth(FranMotion.Segment(Projectile.localAI[0], 0f, 4f)) *
            FranMotion.Smooth(FranMotion.Segment(Projectile.timeLeft, 0f, 14f));
        FranVfx.BeginAdditive();

        for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
        {
            Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            Vector2 newerCenter = Projectile.oldPos[i - 1] + Projectile.Size * 0.5f;
            if (oldCenter == Projectile.Size * 0.5f || newerCenter == Projectile.Size * 0.5f)
                continue;
            float completion = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawStretched("Cal_FadeStreak", oldCenter, newerCenter,
                Color.Lerp(deepColor, edgeColor, completion) * (completion * 0.32f * opacity),
                MathHelper.Lerp(4f, 18f, completion));
        }
        FranVfx.DrawVertical("TraceSoft", Projectile.Center - direction * (72f + speedRatio * 36f),
            Projectile.Center + direction * 12f, edgeColor * (0.38f * opacity), 18f + speedRatio * 8f);
        FranVfx.DrawShared("Cal_BloomCirclePinpoint", Projectile.Center, edgeColor * (0.72f * opacity),
            0f, 0.13f * pulse);
        FranVfx.DrawShared("Cal_BloomFlare", Projectile.Center, Color.White * (0.56f * opacity),
            Projectile.rotation, new Vector2(0.055f, 0.018f) * pulse);
        FranVfx.DrawShared("Cal_PulseStar", Projectile.Center, Color.White * (0.88f * opacity),
            -Projectile.rotation * 0.35f, 0.10f * pulse);
        FranVfx.EndAdditive();

        Texture2D texture = TextureAssets.Item[ItemID.BreakerBlade].Value;
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
            Color.Lerp(lightColor, Color.White, 0.22f), Projectile.rotation, texture.Size() * 0.5f,
            0.92f, SpriteEffects.None, 0f);
        return false;
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Projectile.ai[0] = 1f;
        Projectile.tileCollide = false;
        Projectile.velocity = -oldVelocity * 0.55f;
        Projectile.netUpdate = true;
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner,
            FranBurstStyle.Telekinetic, 0.58f);
    }
    public override void OnKill(int timeLeft)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_Death(), Projectile.Center, Projectile.owner,
            FranBurstStyle.Telekinetic, 0.78f);
        FranVfx.SparkBurst(Projectile.Center, new Color(120, 225, 255), 7, 4.5f);
    }
}

internal static class TeacherWaveVisuals
{
    public static void GetPalette(int stage, int element, out Color deep, out Color body, out Color edge)
    {
        if (element == 1)
        {
            deep = new Color(190, 28, 8);
            body = new Color(255, 92, 28);
        }
        else
        {
            deep = stage >= 5 ? new Color(40, 52, 205) : new Color(25, 105, 210);
            body = stage >= 4 ? new Color(70, 185, 255) : new Color(65, 158, 242);
        }
        edge = Color.Lerp(body, Color.White, stage >= 4 ? 0.74f : 0.58f);
    }

    public static void Light(Vector2 center, int element, float strength = 1f) =>
        Lighting.AddLight(center, (element == 1 ? new Vector3(0.84f, 0.20f, 0.03f) : new Vector3(0.08f, 0.34f, 0.62f)) * strength);

    public static void Hit(Projectile projectile, NPC target, int element, float size, int sparks, float sparkSpeed)
    {
        if (element == 1)
            target.AddBuff(BuffID.OnFire3, 180);
        FranBurst.SpawnForOwner(projectile.GetSource_OnHit(target), target.Center, projectile.owner,
            element == 1 ? FranBurstStyle.Flame : FranBurstStyle.Slash, size);
        FranVfx.SparkBurst(target.Center, element == 1 ? new Color(255, 110, 45) : new Color(125, 215, 255),
            sparks, sparkSpeed, 0.95f + size * 0.25f);
    }

    public static void Death(Projectile projectile, int element, float size, int dustCount, float dustSpeed)
    {
        FranVfx.Burst(projectile.Center, element == 1 ? DustID.Torch : DustID.BlueTorch,
            dustCount, dustSpeed, 1.05f);
        FranBurst.SpawnForOwner(projectile.GetSource_Death(), projectile.Center, projectile.owner,
            element == 1 ? FranBurstStyle.Flame : FranBurstStyle.Slash, size);
    }
}

// Calamity JudgementProj: five offset layers of the original greyscale wave, with independent X/Y expansion.
public sealed class TeacherCrescentWave : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    private int Stage => (int)Projectile.ai[0];
    private int Combo => (int)Projectile.ai[1];
    private int Element => (int)Projectile.ai[2];
    private float FaceLength => Stage >= 5 ? 92f : Stage >= 4 ? 82f : 72f;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 360;
    }

    public override void SetDefaults()
    {
        Projectile.width = 44;
        Projectile.height = 44;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 3;
        Projectile.timeLeft = 72;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 14;
    }

    public override bool? CanDamage() => Projectile.localAI[0] >= 2f && Projectile.timeLeft > 7 ? null : false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        float curveSign = Combo == 1 ? -1f : 1f;
        if (age < 7f)
            Projectile.velocity *= 1.035f;
        else if (Projectile.timeLeft < 15)
            Projectile.velocity *= 0.94f;

        float curve = curveSign * MathHelper.Lerp(0.0105f, 0.0015f, FranMotion.Clamp01(age / 50f));
        Projectile.velocity = Projectile.velocity.RotatedBy(curve);
        float speed = Projectile.velocity.Length();
        if (Stage >= 4 && age > 10f)
        {
            NPC target = FranVfx.ClosestTarget(Projectile.Center, 500f);
            if (target != null)
            {
                float desiredAngle = (target.Center - Projectile.Center).ToRotation();
                float steeredAngle = Projectile.velocity.ToRotation().AngleTowards(desiredAngle, 0.025f);
                Projectile.velocity = steeredAngle.ToRotationVector2() * speed;
            }
        }

        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ && age > 3f && age < 58f)
        {
            TeacherWaveVisuals.GetPalette(Stage, Element, out _, out Color wakeColor, out _);
            FranParticleEffects.CrescentWake(Projectile.Center, Projectile.velocity, wakeColor, FaceLength);
        }
        if (!Main.dedServ && age > 3f && age < 58f)
        {
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + normal * Main.rand.NextFloat(-FaceLength * 0.76f, FaceLength * 0.76f),
                    Element == 1 ? DustID.Torch : DustID.BlueTorch,
                    -direction * Main.rand.NextFloat(0.5f, 1.8f) + normal * Main.rand.NextFloat(-1.1f, 1.1f),
                    65, default, Main.rand.NextFloat(0.82f, 1.16f));
                dust.noGravity = true;
            }
        }
        TeacherWaveVisuals.Light(Projectile.Center, Element, 0.9f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Vector2 normal = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - normal * FaceLength, Projectile.Center + normal * FaceLength, 14f, ref collisionPoint);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float age = 72f - Projectile.timeLeft;
        float opacity = FranMotion.Smooth(FranMotion.Segment(age, 0f, 5f)) *
            (1f - FranMotion.Smooth(FranMotion.Segment(age, 57f, 72f)));
        TeacherWaveVisuals.GetPalette(Stage, Element, out Color deep, out Color body, out Color edge);
        Texture2D wave = FranVfx.Shared("Cal_JudgementWave");
        float waveFade = Utils.GetLerpValue(0f, 57f, Projectile.timeLeft, true);
        float expansion = MathHelper.Lerp(0.72f, 1.08f, FranMotion.Smooth(FranMotion.Segment(age, 0f, 27f)));
        float baseScale = FaceLength * 2f / wave.Width * expansion;
        float drawRotation = Projectile.rotation - MathHelper.PiOver2 + (Combo == 1 ? MathHelper.Pi : 0f);
        Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 normal = forward.RotatedBy(MathHelper.PiOver2);
        // This block already uses BlendState.Additive. Unlike Calamity's A=0 trick inside
        // AlphaBlend, a zero source alpha here suppresses the entire contribution.
        Color layerColor = body;
        Color coreColor = edge;

        FranVfx.BeginAdditive();
        FranVfx.DrawShared("Cal_VerticalSmearLarge", Projectile.Center - forward * 8f,
            deep * (0.16f * opacity), drawRotation,
            new Vector2(baseScale * 0.78f, baseScale * 0.46f));
        for (int i = 0; i < 5; i++)
        {
            Vector2 scale = new(1f - 0.2f * waveFade + 0.01f * i,
                1f + 0.45f * waveFade - 0.06f * i);
            Main.EntitySpriteDraw(wave, Projectile.Center - Main.screenPosition + forward * 9f * i,
                null, layerColor * (0.54f * opacity), drawRotation, wave.Size() * 0.5f,
                scale * baseScale, SpriteEffects.None, 0f);
        }
        Main.EntitySpriteDraw(wave, Projectile.Center - Main.screenPosition + forward * 12f,
            null, coreColor * (0.46f * opacity), drawRotation, wave.Size() * 0.5f,
            new Vector2(0.78f, 1.12f) * baseScale, SpriteEffects.None, 0f);

        DrawWingSparks(Projectile.Center + normal * FaceLength * 0.82f, normal, age, body, edge, opacity, 1f);
        DrawWingSparks(Projectile.Center - normal * FaceLength * 0.82f, -normal, age, body, edge, opacity, -1f);
        FranVfx.EndAdditive();
        return false;
    }

    private static void DrawWingSparks(Vector2 wing, Vector2 outward, float age, Color body, Color edge,
        float opacity, float phaseOffset)
    {
        for (int i = 0; i < 3; i++)
        {
            float sparkAge = (age + i * 4f + phaseOffset * 2f) % 15f;
            float completion = sparkAge / 15f;
            Vector2 position = wing + outward * sparkAge * (0.72f + i * 0.12f) +
                outward.RotatedBy(MathHelper.PiOver2) * MathF.Sin(age * 0.24f + i * 2.1f) * 4f;
            Color color = Color.Lerp(edge, body, completion);
            FranVfx.DrawShared("Cal_HalfStar", position, color * (opacity * (1f - completion) * 0.72f),
                outward.ToRotation(), new Vector2(0.38f, 1.05f) * (0.72f + i * 0.14f));
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) =>
        TeacherWaveVisuals.Hit(Projectile, target, Element, 0.48f, 7, 4.5f);

    public override void OnKill(int timeLeft) => TeacherWaveVisuals.Death(Projectile, Element, 0.42f, 10, 4.2f);
}

// Redemption Lightmass + Calamity StarofJudgement: hover, lock, dash, then fake-kill the body while the trail closes.
public sealed class TeacherStarlightMass : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    private int Stage => (int)Projectile.ai[0];
    private int Element => (int)Projectile.ai[2];

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 24;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;
    }

    public override void SetDefaults()
    {
        Projectile.width = 28;
        Projectile.height = 28;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 2;
        Projectile.timeLeft = 180;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 0;
    }

    private bool FakeKilling => Projectile.ai[1] >= 1f;

    public override bool? CanDamage() => !FakeKilling && Projectile.localAI[0] >= 30f ? null : false;

    public override void AI()
    {
        if (FakeKilling)
        {
            Projectile.localAI[1]++;
            Projectile.velocity = Vector2.Zero;
            Projectile.friendly = false;
            Projectile.alpha = 255;
            Projectile.timeLeft = 2;
            if (Projectile.localAI[1] >= 36f)
                Projectile.Kill();
            return;
        }

        float age = Projectile.localAI[0]++;
        if (Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height))
        {
            TriggerFakeKill();
            return;
        }

        if (age < 30f)
            Projectile.velocity *= 0.98f;
        else
        {
            NPC target = FranVfx.ClosestTarget(Projectile.Center, 900f);
            if (target != null)
            {
                float targetSpeed = age < 130f ? 30f : 50f;
                Vector2 desiredVelocity = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity) * targetSpeed;
                Projectile.velocity = (Projectile.velocity * 49f + desiredVelocity) / 50f;
            }
            else
                Projectile.velocity *= 0.98f;
        }

        float direction = Math.Sign(Projectile.velocity.X);
        if (direction == 0f)
            direction = 1f;
        Projectile.rotation += (Projectile.velocity.X + MathF.Abs(Projectile.velocity.Y) * direction) * 0.01f;

        if (!Main.dedServ)
        {
            TeacherWaveVisuals.GetPalette(Stage, Element, out _, out Color body, out Color edge);
            if (age < 30f)
                FranParticleEffects.StarlightOrbit(Projectile.Center, Color.Lerp(body, edge, 0.5f), age);
            else
                FranParticleEffects.StarlightWake(Projectile.Center, Projectile.velocity, edge);
        }

        if (!Main.dedServ && age > 30f && Main.rand.NextBool(3))
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.5f,
                Element == 1 ? DustID.GoldFlame : DustID.BlueTorch, -Projectile.velocity * Main.rand.NextFloat(0.08f, 0.18f),
                60, default, Main.rand.NextFloat(0.78f, 1.08f));
            dust.noGravity = true;
        }
        TeacherWaveVisuals.Light(Projectile.Center, Element, age < 30f ? 0.82f : 1.05f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float radius = 18f + Projectile.scale * 8f;
        Vector2 closest = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(),
            new Vector2(targetHitbox.Right, targetHitbox.Bottom));
        return Vector2.DistanceSquared(Projectile.Center, closest) <= radius * radius;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float age = Projectile.localAI[0];
        float fakeOpacity = FakeKilling ? 1f - FranMotion.Clamp01(Projectile.localAI[1] / 36f) : 1f;
        TeacherWaveVisuals.GetPalette(Stage, Element, out Color deep, out Color body, out Color edge);
        Color deepAdditive = deep;
        Color bodyAdditive = body;
        Color edgeAdditive = edge;

        FranVfx.BeginAdditive();
        Vector2 previous = Projectile.Center;
        for (int i = 0; i < Projectile.oldPos.Length; i++)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                break;
            float completion = 1f - i / (float)Projectile.oldPos.Length;
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            FranVfx.DrawStretched("Red_Trail4", point, previous,
                deepAdditive * (completion * completion * 0.30f * fakeOpacity), 84f * completion);
            FranVfx.DrawStretched("Red_Trail4", point, previous,
                edgeAdditive * (completion * completion * 0.34f * fakeOpacity), 42f * completion);
            if (i % 4 == 0)
                FranVfx.DrawShared("Red_WhiteFlare", point, bodyAdditive * (completion * 0.16f * fakeOpacity),
                    Projectile.oldRot[i], 0.26f + completion * 0.18f);
            previous = point;
        }

        if (!FakeKilling)
        {
            if (age < 30f)
            {
                float auraOpacity = 1f - age / 30f;
                float auraScale = MathHelper.Lerp(0.42f, 0.62f, FranMotion.Smooth(age / 30f));
                FranVfx.DrawShared("Red_HolyGlow2", Projectile.Center, Color.White * (0.18f * auraOpacity),
                    Projectile.rotation + age * 0.06f, auraScale);
                FranVfx.DrawShared("Red_HolyGlow2", Projectile.Center, Color.White * (0.13f * auraOpacity),
                    -Projectile.rotation - age * 0.04f, auraScale * 0.86f);
            }

            float pulse = 0.92f + MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity) * 0.08f;
            FranVfx.DrawShared("Red_WhiteFlare", Projectile.Center, bodyAdditive * 0.82f,
                Projectile.rotation, 0.64f * pulse);
            FranVfx.DrawShared("Cal_StarOfJudgement", Projectile.Center, bodyAdditive * 0.86f,
                Projectile.rotation, 0.62f * pulse);
            FranVfx.DrawShared("Cal_StarOfJudgement", Projectile.Center, Color.White,
                Projectile.rotation, 0.48f * pulse);
        }
        FranVfx.EndAdditive();
        return false;
    }

    private void TriggerFakeKill()
    {
        if (FakeKilling)
            return;
        Projectile.ai[1] = 1f;
        Projectile.localAI[1] = 0f;
        Projectile.friendly = false;
        Projectile.velocity = Vector2.Zero;
        Projectile.netUpdate = true;
        if (!Main.dedServ)
        {
            TeacherWaveVisuals.GetPalette(Stage, Element, out _, out Color body, out Color edge);
            FranParticleEffects.Implosion(Projectile.Center, Color.Lerp(body, edge, 0.55f), 1.05f);
            for (int i = 0; i < 8; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center,
                    Element == 1 ? DustID.GoldFlame : DustID.BlueTorch,
                    Main.rand.NextVector2Circular(4.5f, 4.5f), 40, default, Main.rand.NextFloat(0.95f, 1.28f));
                dust.noGravity = true;
            }
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        TeacherWaveVisuals.Hit(Projectile, target, Element, 0.48f, 8, 5.2f);
        TriggerFakeKill();
    }
}

// Finisher: a slow compression wheel that brakes, winds up, then surges and collapses.
public sealed class TeacherCompressionWave : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    private int Stage => (int)Projectile.ai[0];
    private int Element => (int)Projectile.ai[2];

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 2;
        ProjectileID.Sets.DrawScreenCheckFluff[Type] = 440;
    }

    public override void SetDefaults()
    {
        Projectile.width = 76;
        Projectile.height = 76;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 5;
        Projectile.timeLeft = 70;
        Projectile.tileCollide = true;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 17;
    }

    public override bool? CanDamage() => Projectile.localAI[0] >= 4f && Projectile.timeLeft > 8 ? null : false;

    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        Projectile.localAI[1] += 0.10f + FranMotion.Clamp01(age / 28f) * 0.16f;
        if (age < 9f)
            Projectile.velocity *= 0.94f;
        else if (age < 29f)
            Projectile.velocity *= 1.035f;
        else if (Projectile.timeLeft < 17)
            Projectile.velocity *= 0.92f;
        else
            Projectile.velocity *= 0.992f;

        float speed = Projectile.velocity.Length();
        if (Stage >= 4 && age > 18f)
        {
            NPC target = FranVfx.ClosestTarget(Projectile.Center, 520f);
            if (target != null)
            {
                float angle = Projectile.velocity.ToRotation().AngleTowards((target.Center - Projectile.Center).ToRotation(), 0.018f);
                Projectile.velocity = angle.ToRotationVector2() * speed;
            }
        }
        Projectile.rotation = Projectile.velocity.ToRotation();

        if (!Main.dedServ && age > 3f && Projectile.timeLeft > 8)
        {
            TeacherWaveVisuals.GetPalette(Stage, Element, out _, out Color body, out Color edge);
            FranParticleEffects.Compression(Projectile.Center, Projectile.velocity,
                Color.Lerp(body, edge, age > 22f ? 0.58f : 0.22f), age, age >= 22f);
        }

        if (!Main.dedServ && age > 4f && Projectile.timeLeft > 12 && Main.rand.NextBool(2))
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 radial = angle.ToRotationVector2();
            Dust dust = Dust.NewDustPerfect(Projectile.Center + radial * Main.rand.NextFloat(30f, 54f),
                Element == 1 ? DustID.Torch : DustID.BlueTorch, -radial * Main.rand.NextFloat(1.2f, 3f),
                70, default, Main.rand.NextFloat(0.88f, 1.24f));
            dust.noGravity = true;
        }
        TeacherWaveVisuals.Light(Projectile.Center, Element, 1.05f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float radius = Stage >= 5 ? 65f : 58f;
        Vector2 axis = Projectile.localAI[1].ToRotationVector2();
        float collisionPoint = 0f;
        if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - axis * radius, Projectile.Center + axis * radius, 23f, ref collisionPoint))
            return true;
        axis = axis.RotatedBy(MathHelper.PiOver2);
        collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - axis * radius, Projectile.Center + axis * radius, 23f, ref collisionPoint);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float age = 70f - Projectile.timeLeft;
        float opacity = FranMotion.Smooth(FranMotion.Segment(age, 0f, 7f)) *
            (1f - FranMotion.Smooth(FranMotion.Segment(age, 53f, 70f)));
        float windup = FranMotion.Smooth(FranMotion.Segment(age, 4f, 22f));
        float release = FranMotion.Smooth(FranMotion.Segment(age, 22f, 34f));
        float radius = MathHelper.Lerp(43f, Stage >= 5 ? 68f : 61f, windup) + release * 7f;
        float spin = Projectile.localAI[1];
        TeacherWaveVisuals.GetPalette(Stage, Element, out Color deep, out Color body, out Color edge);

        Color deepAdditive = deep;
        Color bodyAdditive = body;
        Color edgeAdditive = edge;
        float circularScale = radius * 2f / FranVfx.Shared("Cal_CircularSmearSmokey").Width;
        float trientScale = radius * 1.72f / FranVfx.Shared("Cal_TrientCircularSmear").Width;
        float slashScale = radius * 2.15f / FranVfx.Shared("Cal_SlashSmear").Width;

        FranVfx.BeginAdditive();
        for (int i = 10; i >= 4; i -= 3)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                continue;
            float completion = 1f - i / 14f;
            FranVfx.DrawShared("Cal_CircularSmearSmokey", Projectile.oldPos[i] + Projectile.Size * 0.5f,
                deepAdditive * (opacity * completion * 0.16f), spin - i * 0.14f,
                circularScale * completion);
        }

        FranVfx.DrawShared("Cal_CircularSmearSmokey", Projectile.Center,
            deepAdditive * (0.46f * opacity), -spin * 0.72f,
            new Vector2(circularScale * 1.15f, circularScale * 0.94f));
        FranVfx.DrawShared("Cal_CircularSmearSmokey",
            Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 6f,
            bodyAdditive * (0.42f * opacity), spin * 0.48f,
            new Vector2(circularScale * 0.92f, circularScale * 1.16f));

        for (int i = 0; i < 3; i++)
        {
            float angle = spin + i * MathHelper.TwoPi / 3f + MathF.Sin(age * 0.21f + i) * 0.13f;
            Vector2 radial = angle.ToRotationVector2();
            Vector2 bladeCenter = Projectile.Center + radial * radius * (0.12f + i * 0.07f);
            Color arcColor = Color.Lerp(body, edge, i * 0.24f);
            FranVfx.DrawShared("Cal_TrientCircularSmear", bladeCenter,
                arcColor * ((0.58f - i * 0.07f) * opacity), angle + MathHelper.PiOver2,
                trientScale * (1f - i * 0.08f));
            FranVfx.DrawShared("Cal_SlashSmear", bladeCenter + radial * 8f,
                edgeAdditive * ((0.28f - i * 0.035f) * opacity), angle,
                new Vector2(slashScale * (0.90f - i * 0.08f), slashScale * (0.72f + i * 0.05f)));
        }

        float jawScale = radius * 2.15f / FranVfx.Shared("Cal_VerticalSmearLarge").Width;
        FranVfx.DrawShared("Cal_VerticalSmearLarge", Projectile.Center, bodyAdditive * (0.22f * opacity),
            Projectile.rotation + MathHelper.PiOver2 - spin * 0.11f,
            new Vector2(jawScale * (0.82f + release * 0.24f), jawScale * 0.62f));
        FranVfx.DrawShared("Cal_VerticalSmearLarge", Projectile.Center, edgeAdditive * (0.16f * opacity),
            Projectile.rotation - MathHelper.PiOver2 + spin * 0.08f,
            new Vector2(jawScale * 0.72f, jawScale * (0.54f + release * 0.18f)));

        for (int i = 0; i < 5; i++)
        {
            float sparkAge = (age - 22f - i * 2.4f);
            if (sparkAge < 0f || sparkAge > 16f)
                continue;
            float completion = sparkAge / 16f;
            float angle = Projectile.rotation + i * MathHelper.TwoPi / 5f + spin * 0.18f;
            Vector2 sparkPosition = Projectile.Center + angle.ToRotationVector2() *
                MathHelper.Lerp(radius * 0.18f, radius * 1.15f, FranMotion.Smooth(completion));
            FranVfx.DrawShared("Cal_HalfStar", sparkPosition,
                edgeAdditive * ((1f - completion) * 0.74f * opacity), angle,
                new Vector2(0.44f, 1.18f) * (1f - completion * 0.46f));
        }
        FranVfx.EndAdditive();
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        Projectile.velocity *= 0.78f;
        TeacherWaveVisuals.Hit(Projectile, target, Element, 0.74f, 12, 6.3f);
    }

    public override void OnKill(int timeLeft) => TeacherWaveVisuals.Death(Projectile, Element, 0.72f, 16, 5.8f);
}

public sealed class SkillTakerPhantom : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 28; Projectile.height = 28; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = 1; Projectile.timeLeft = 55;
        Projectile.tileCollide = false; Projectile.extraUpdates = 1;
    }
    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        float speed = Projectile.velocity.Length();
        if (age < 8f && speed < 24f)
            speed *= 1.035f;

        if (speed > 0.01f)
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * speed;

        if (Projectile.timeLeft < 16)
            Projectile.velocity *= 0.965f;
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ && Main.rand.NextBool(2))
            FranParticleEffects.StarlightWake(Projectile.Center, Projectile.velocity, new Color(215, 115, 255));
        if (Main.rand.NextBool(3))
        {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, -Projectile.velocity * 0.04f,
                50, default, 1.1f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.38f, 0.08f, 0.55f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float speedRatio = FranMotion.Clamp01((Projectile.velocity.Length() - 12f) / 12f);
        float opacity = FranMotion.Smooth(FranMotion.Segment(Projectile.localAI[0], 0f, 5f)) *
            FranMotion.Smooth(FranMotion.Segment(Projectile.timeLeft, 0f, 16f));
        Color phantomColor = Color.Lerp(new Color(95, 45, 215), new Color(235, 175, 255), speedRatio);
        Color starlight = Color.Lerp(new Color(120, 160, 255), Color.White, speedRatio * 0.55f);
        FranVfx.BeginAdditive();

        // Leonid-like starlight: a curved, segmented constellation trail instead of a single rectangle.
        Vector2 previous = Projectile.Center;
        for (int i = 1; i < Projectile.oldPos.Length; i++)
        {
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            if (point == Projectile.Size * 0.5f)
                continue;
            float completion = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawStretched("Cal_DrainLineBloom2", point, previous,
                Color.Lerp(phantomColor, starlight, completion) * (0.52f * completion * opacity),
                9f + completion * 7f);
            if (i % 3 == 0)
                FranVfx.DrawShared("Cal_SimpleStar", point, starlight * (0.32f * completion * opacity),
                    Projectile.rotation + i * 0.73f, 0.035f + completion * 0.025f);
            previous = point;
        }

        float spin = Main.GlobalTimeWrappedHourly * 2.2f + Projectile.identity;
        FranVfx.DrawShared("Cal_FadedStarRing", Projectile.Center, phantomColor * (0.38f * opacity),
            spin, 0.115f + speedRatio * 0.025f);
        FranVfx.DrawShared("Cal_FadedStarRing", Projectile.Center, starlight * (0.24f * opacity),
            -spin * 0.72f, 0.075f + speedRatio * 0.018f);
        FranVfx.DrawShared("Cal_PulseStar", Projectile.Center, phantomColor * (0.76f * opacity),
            Projectile.rotation, new Vector2(0.15f, 0.095f));
        FranVfx.DrawShared("Cal_StarProj", Projectile.Center, Color.White * (0.92f * opacity),
            -Projectile.rotation * 0.45f, 0.18f);
        FranVfx.DrawBloom(Projectile.Center, starlight, 22f, 0.48f * opacity);
        FranVfx.EndAdditive();
        return false;
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        target.GetGlobalNPC<SkillTakerGlobalNPC>().Mark(Projectile.owner, 360);
        if (Main.netMode == NetmodeID.MultiplayerClient && Main.myPlayer == Projectile.owner)
            FranModMain.SendSkillTakerMark(target.whoAmI, Projectile.owner, 360);
        FranBurst.SpawnForOwner(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner,
            FranBurstStyle.SkillTaker, 0.72f);
        FranParticleEffects.ToxicBurst(target.Center, -Projectile.velocity, 7, 0.55f);
        FranVfx.SparkBurst(target.Center, new Color(205, 100, 255), 8, 4.5f);
        if (Main.myPlayer == Projectile.owner)
        {
            Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center,
                Main.rand.NextVector2Circular(2f, 2f), ModContent.ProjectileType<SkillTakerSoul>(),
                0, 0f, Projectile.owner);
            for (int i = 0; i < 3; i++)
            {
                Vector2 heartVelocity = (-Projectile.velocity.SafeNormalize(Vector2.UnitX))
                    .RotatedByRandom(0.85f) * Main.rand.NextFloat(5.5f, 9f);
                Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, heartVelocity,
                    ModContent.ProjectileType<FranHeartSeekerProjectile>(),
                    Math.Max(1, (int)(Projectile.damage * 0.34f)), Projectile.knockBack * 0.35f, Projectile.owner);
            }
        }
    }
}

public sealed class SkillTakerGlobalNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;
    private int owner = -1;
    private int timer;
    private int hitCounter;

    public void Mark(int player, int duration)
    {
        if (timer <= 0 || owner != player)
            hitCounter = 0;
        owner = player;
        timer = Math.Max(timer, duration);
    }

    public override void PostAI(NPC npc)
    {
        if (timer <= 0)
            return;
        timer--;
        Lighting.AddLight(npc.Center, 0.18f, 0.02f, 0.26f);
        if (Main.rand.NextBool(4))
        {
            Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(npc.width, npc.height) * 0.5f,
                DustID.PurpleTorch, -Vector2.UnitY, 80, default, 1.1f);
            d.noGravity = true;
        }
    }

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (timer <= 0)
            return;
        float fade = MathHelper.Clamp(timer / 45f, 0f, 1f);
        float radius = Math.Max(npc.width, npc.height) * 0.72f + 16f;
        FranVfx.BeginAdditive();
        FranVfx.DrawRing(npc.Center, new Color(170, 65, 235), radius, 0.28f * fade,
            Main.GlobalTimeWrappedHourly * 0.7f, "Cal_FadedStarRing");
        FranVfx.DrawRing(npc.Center, new Color(225, 165, 255), radius * 0.72f, 0.18f * fade,
            -Main.GlobalTimeWrappedHourly * 0.9f, "Cal_BloomRing");
        for (int i = 0; i < 5; i++)
        {
            float angle = Main.GlobalTimeWrappedHourly * 0.46f + i * MathHelper.TwoPi / 5f;
            FranVfx.DrawShared("Cal_SimpleStar", npc.Center + angle.ToRotationVector2() * radius * 0.72f,
                Color.White * (0.22f * fade), -angle, 0.035f);
        }
        FranVfx.EndAdditive();
    }

    public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) => HandleHit(npc, player.whoAmI);
    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) => HandleHit(npc, projectile.owner);

    private void HandleHit(NPC npc, int attacker)
    {
        if (timer <= 0 || attacker != owner || Main.myPlayer != owner || ++hitCounter % 3 != 0)
            return;
        SpawnSouls(npc, 1);
    }

    public override void OnKill(NPC npc)
    {
        if (timer > 0 && owner >= 0 && Main.myPlayer == owner)
            SpawnSouls(npc, npc.boss ? 2 : 1);
    }

    private void SpawnSouls(NPC npc, int count)
    {
        if (owner < 0 || owner >= Main.maxPlayers || !Main.player[owner].active)
            return;
        for (int i = 0; i < count; i++)
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Main.rand.NextVector2Circular(5f, 5f),
                ModContent.ProjectileType<SkillTakerSoul>(), 0, 0f, owner);
    }
}

public sealed class SkillTakerSoul : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 14; Projectile.height = 14; Projectile.friendly = false;
        Projectile.penetrate = -1; Projectile.timeLeft = 180;
        Projectile.tileCollide = false;
    }
    public override bool? CanDamage() => false;
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }
        if (Projectile.timeLeft < 168)
        {
            Vector2 toOwner = owner.MountedCenter - Projectile.Center;
            if (toOwner.LengthSquared() < 28f * 28f)
            {
                if (Main.myPlayer == Projectile.owner)
                    owner.GetModPlayer<FranPlayer>().GrantStolenSkill();
                FranBurst.SpawnForOwner(Projectile.GetSource_Death(), owner.MountedCenter,
                    Projectile.owner, FranBurstStyle.SkillTaker, 0.42f);
                Projectile.Kill();
                return;
            }
            Vector2 desired = toOwner.SafeNormalize(Vector2.UnitX * owner.direction) * 13f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);
        }
        if (!Main.dedServ && Main.rand.NextBool(2))
            FranParticleEffects.StarlightWake(Projectile.Center, Projectile.velocity, new Color(205, 105, 255));
        if (Main.rand.NextBool(3))
        {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, -Projectile.velocity * 0.05f,
                50, default, 1f);
            d.noGravity = true;
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float pulse = 0.85f + MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity) * 0.12f;
        FranVfx.BeginAdditive();
        FranVfx.DrawShared("Cal_FadedStarRing", Projectile.Center, new Color(145, 75, 255) * 0.42f,
            Main.GlobalTimeWrappedHourly * 1.8f, 0.055f * pulse);
        FranVfx.DrawShared("Cal_SimpleStar", Projectile.Center, new Color(215, 125, 255) * 0.72f,
            -Main.GlobalTimeWrappedHourly * 2.3f, 0.055f * pulse);
        FranVfx.DrawShared("Cal_StarProj", Projectile.Center, Color.White * 0.86f,
            Projectile.velocity.ToRotation(), 0.10f * pulse);
        FranVfx.DrawBloom(Projectile.Center, new Color(215, 125, 255), 14f * pulse, 0.56f);
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class MagicBarrierProjectile : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 36; Projectile.height = 150; Projectile.timeLeft = 300;
        Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead) { Projectile.Kill(); return; }
        Projectile.rotation = Projectile.ai[0] + MathHelper.PiOver2;
        Projectile.Center = owner.MountedCenter + Projectile.ai[0].ToRotationVector2() * 82f;
        Projectile.ai[1]++;
        float deploy = FranMotion.EaseOutBack(FranMotion.Segment(Projectile.ai[1], 0f, 14f));
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            Vector2 axis = Projectile.rotation.ToRotationVector2() * (74f * deploy);
            Vector2 lineStart = Projectile.Center - axis;
            Vector2 lineEnd = Projectile.Center + axis;
            foreach (Projectile other in Main.ActiveProjectiles)
            {
                float collisionPoint = 0f;
                bool intersects = Collision.CheckAABBvLineCollision(other.Hitbox.TopLeft(), other.Hitbox.Size(),
                    lineStart, lineEnd, 22f, ref collisionPoint);
                if (!other.hostile || other.damage <= 0 || !intersects)
                    continue;
                other.Kill();
                Projectile.ai[2]++;
                FranBurst.Spawn(Projectile.GetSource_FromThis(), other.Center, Projectile.owner,
                    FranBurstStyle.Barrier, 0.55f);
                Projectile.netUpdate = true;
                if (Projectile.ai[2] >= 3)
                {
                    Projectile.Kill();
                    break;
                }
            }
        }
        Lighting.AddLight(Projectile.Center, 0.08f, 0.32f, 0.55f);
        if (!Main.dedServ && ((int)Projectile.ai[1] & 1) == 0)
            FranParticleEffects.InwardSpark(Projectile.Center, new Color(105, 195, 255),
                48f * deploy, 82f * Math.Max(0.2f, deploy), 2.2f, 0.72f);
    }
    public override void OnKill(int timeLeft)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            FranBurst.Spawn(Projectile.GetSource_Death(), Projectile.Center, Projectile.owner,
                FranBurstStyle.Barrier, 1.15f);
        FranVfx.SparkBurst(Projectile.Center, new Color(90, 185, 255), 14, 6f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float deploy = FranMotion.EaseOutBack(FranMotion.Segment(Projectile.ai[1], 0f, 14f));
        float integrity = 1f - FranMotion.Clamp01(Projectile.ai[2] / 3f);
        Vector2 axis = Projectile.rotation.ToRotationVector2() * (74f * deploy);
        Vector2 side = axis.RotatedBy(MathHelper.PiOver2) * 0.22f;
        Color voidColor = new(14, 7, 28, 210);
        Color color = Color.Lerp(new Color(78, 80, 225), new Color(185, 245, 255),
            (1f - integrity) * 0.52f);
        float pulse = 0.92f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4.8f + Projectile.identity) * 0.06f;

        // Nadir-style swallowed-light core. This must remain in ordinary AlphaBlend;
        // black inside Additive would contribute nothing and make the intended depth disappear.
        FranVfx.DrawShared("Cal_CircularSmearSmokey", Projectile.Center, voidColor * (0.72f * deploy),
            Projectile.rotation + Main.GlobalTimeWrappedHourly * 0.16f,
            new Vector2(0.48f, 0.17f) * pulse * deploy);
        FranVfx.DrawShared("Cal_HighResHollowCircle", Projectile.Center, new Color(8, 4, 18, 185) * deploy,
            -Projectile.rotation, new Vector2(0.064f, 0.026f) * pulse * deploy);

        FranVfx.BeginAdditive();
        FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center,
            new Color(105, 70, 255) * (0.48f * deploy), Projectile.rotation,
            new Vector2(0.84f, 0.42f) * pulse * deploy);
        FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center,
            new Color(105, 210, 255) * (0.38f * deploy), Projectile.rotation + MathHelper.Pi,
            new Vector2(0.84f, 0.42f) * pulse * deploy);
        FranVfx.DrawShared("Cal_BloomRing", Projectile.Center, color * (0.42f * deploy),
            Main.GlobalTimeWrappedHourly * 0.7f, new Vector2(0.72f, 0.28f) * pulse * deploy);
        if (Projectile.ai[2] > 0f)
            FranVfx.DrawShared("Crack", Projectile.Center, new Color(170, 225, 255) * (0.16f + Projectile.ai[2] * 0.09f),
                Projectile.rotation, new Vector2(0.20f, 0.38f));
        FranVfx.DrawVertical("Cal_BloomLineSoftEdge", Projectile.Center - axis - side,
            Projectile.Center + axis - side, color * 0.58f, 15f);
        FranVfx.DrawVertical("Cal_BloomLineThick", Projectile.Center - axis + side,
            Projectile.Center + axis + side, Color.White * 0.72f, 5f);
        for (int i = -3; i <= 3; i++)
            FranVfx.DrawVertical("Cal_ThinEndedLine", Projectile.Center + axis * (i / 3f) - side,
                Projectile.Center + axis * (i / 3f) + side, color * 0.42f, 3f);
        FranVfx.DrawBloom(Projectile.Center, new Color(95, 110, 255), 38f, 0.28f * deploy);
        FranVfx.EndAdditive();

        Texture2D teacher = TextureAssets.Item[ItemID.BreakerBlade].Value;
        Main.EntitySpriteDraw(teacher, Projectile.Center - Main.screenPosition, null,
            Color.Lerp(lightColor, Color.White, 0.18f), Projectile.rotation + MathHelper.PiOver4,
            teacher.Size() * 0.5f, 0.86f, SpriteEffects.None, 0f);
        return false;
    }
}

public sealed class FlameFangAuraVisual : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = 10 * 60;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        int element = (int)Projectile.ai[0];
        bool active = element == 0 && fran.BlueResonanceTimer > 0;
        if (!owner.active || owner.dead || !active)
        {
            Projectile.Kill();
            return;
        }

        Projectile.Center = owner.Center;
        Vector3 light = element switch
        {
            1 => new Vector3(0.34f, 0.10f, 0.025f),
            2 => new Vector3(0.08f, 0.30f, 0.27f),
            3 => new Vector3(0.18f, 0.10f, 0.42f),
            _ => new Vector3(0.04f, 0.24f, 0.42f)
        };
        Lighting.AddLight(owner.Center, light);
        if (!Main.dedServ && Projectile.timeLeft % 5 == 0)
        {
            Color auraColor = element switch
            {
                1 => new Color(255, 105, 45),
                2 => new Color(105, 225, 195),
                3 => new Color(175, 115, 255),
                _ => new Color(105, 195, 255)
            };
            FranParticleEffects.AuraMote(owner.Center, auraColor, element == 1);
        }
        if (!Main.dedServ && Projectile.timeLeft % 14 == 0)
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 spawn = owner.Center + angle.ToRotationVector2() * Main.rand.NextFloat(30f, 48f);
            int dustType = element == 1 ? DustID.Torch : element == 2 ? DustID.Cloud :
                element == 3 ? DustID.Electric : DustID.BlueTorch;
            Dust dust = Dust.NewDustPerfect(spawn, dustType,
                (owner.Center - spawn).SafeNormalize(-Vector2.UnitY) * 0.7f - Vector2.UnitY * 0.8f,
                70, default, Main.rand.NextFloat(0.8f, 1.15f));
            dust.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float age = 10f * 60f - Projectile.timeLeft;
        float introduction = FranMotion.Smooth(FranMotion.Segment(age, 0f, 18f));
        float ending = FranMotion.Smooth(FranMotion.Segment(Projectile.timeLeft, 0f, 25f));
        float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.4f + Projectile.identity);
        float opacity = introduction * ending;
        int element = (int)Projectile.ai[0];
        Color outer = element switch
        {
            1 => Color.Lerp(new Color(255, 75, 24), new Color(255, 205, 82), pulse * 0.55f),
            2 => Color.Lerp(new Color(60, 180, 170), new Color(205, 255, 240), pulse * 0.50f),
            3 => Color.Lerp(new Color(105, 60, 225), new Color(220, 190, 255), pulse * 0.55f),
            _ => Color.Lerp(new Color(25, 115, 225), new Color(165, 245, 255), pulse * 0.55f)
        };
        Color inner = Color.Lerp(outer, Color.White, 0.62f);

        FranVfx.BeginAdditive();
        FranVfx.DrawShared("Cal_CircularSmear", Projectile.Center, outer * (0.20f * opacity),
            Main.GlobalTimeWrappedHourly * 0.62f, new Vector2(0.72f, 0.48f) * (0.92f + pulse * 0.12f));
        FranVfx.DrawShared("Cal_BloomRing", Projectile.Center, outer * (0.24f * opacity),
            -Main.GlobalTimeWrappedHourly * 0.47f, 0.42f + pulse * 0.045f);
        FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center + new Vector2(0f, 4f),
            inner * (0.28f * opacity), Main.GlobalTimeWrappedHourly * 0.82f,
            new Vector2(0.38f, 0.31f));
        for (int i = 0; i < 5; i++)
        {
            float angle = Main.GlobalTimeWrappedHourly * (0.32f + i * 0.045f) + i * MathHelper.TwoPi / 5f;
            Vector2 bubble = Projectile.Center + angle.ToRotationVector2() * (30f + i % 2 * 10f + pulse * 4f);
            FranVfx.DrawShared("Cal_BloomCircle", bubble, (i % 2 == 0 ? outer : inner) * (0.11f * opacity),
                angle, 0.035f + i * 0.004f);
            FranVfx.DrawShared("Cal_SmallBloomRingLayered", bubble, Color.White * (0.12f * opacity),
                -angle, 0.18f + i * 0.015f);
        }
        FranVfx.DrawShared("Cal_BloomFlare", Projectile.Center, inner * (0.22f * opacity),
            Main.GlobalTimeWrappedHourly * 0.35f, new Vector2(0.075f, 0.022f));
        FranVfx.DrawBloom(Projectile.Center, outer, 38f + pulse * 7f, 0.18f * opacity);
        FranVfx.EndAdditive();
        return false;
    }
}

public sealed class FloatingSwordArrayProjectile : ModProjectile
{
    private const int CycleLength = 48;
    private Vector2 formationAim;
    private bool initialized;

    public override string Texture => $"Terraria/Images/Item_{ItemID.BreakerBlade}";
    public override void SetDefaults()
    {
        Projectile.width = 42; Projectile.height = 42; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 240;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 16;
        Projectile.netImportant = true;
    }

    public override bool ShouldUpdatePosition() => false;

    private int CurrentCycle => Projectile.ai[0] < 0f
        ? 0
        : ((int)Projectile.ai[0] - 1 + CycleLength) % CycleLength;

    public override bool? CanDamage()
    {
        int cycle = CurrentCycle;
        return cycle is >= 16 and <= 31 ? null : false;
    }

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }

        if (!initialized)
        {
            initialized = true;
            formationAim = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
            Projectile.velocity = formationAim;
            Projectile.rotation = formationAim.ToRotation() + MathHelper.PiOver4;
        }

        int cycle = (int)Projectile.ai[0] % CycleLength;
        float slot = Projectile.ai[2];
        Vector2 formationForward = Vector2.UnitX * owner.direction;
        Vector2 formationPosition = owner.MountedCenter - formationForward * (48f + Math.Abs(slot) * 11f) +
            Vector2.UnitY * (-48f + slot * 24f);

        if (Projectile.ai[0] < 0f)
        {
            Projectile.Center = Vector2.Lerp(Projectile.Center, formationPosition, 0.30f);
            Projectile.rotation = formationForward.ToRotation() + slot * 0.075f * owner.direction + MathHelper.PiOver4;
            Projectile.ai[0]++;
            Lighting.AddLight(Projectile.Center, 0.12f, 0.18f, 0.25f);
            return;
        }

        if (cycle == 0 && Main.myPlayer == Projectile.owner)
        {
            NPC npc = FranVfx.ClosestTarget(Projectile.Center, 920f);
            Projectile.ai[1] = npc?.whoAmI ?? -1;
            Projectile.netUpdate = true;
        }

        if (cycle < 14)
        {
            float gather = FranMotion.EaseOutCubic(cycle / 14f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, formationPosition, 0.18f + gather * 0.16f);
            float fanAngle = formationForward.ToRotation() + slot * 0.075f * owner.direction;
            Projectile.rotation = fanAngle + MathHelper.PiOver4;
        }
        else if (cycle == 14)
        {
            Vector2 targetPosition = owner.Center + formationAim * 720f;
            int targetIndex = (int)Projectile.ai[1];
            if (Main.npc.IndexInRange(targetIndex) && Main.npc[targetIndex].CanBeChasedBy(Projectile))
                targetPosition = Main.npc[targetIndex].Center;
            Projectile.velocity = (targetPosition - Projectile.Center).SafeNormalize(formationAim)
                .RotatedBy(slot * 0.055f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            if (Main.myPlayer == Projectile.owner)
                Projectile.netUpdate = true;
        }
        else if (cycle < 32)
        {
            float dashProgress = FranMotion.Segment(cycle, 14f, 32f);
            float speedEnvelope = MathF.Pow(MathF.Sin(dashProgress * MathHelper.Pi), 0.58f);
            float speed = 8f + speedEnvelope * 34f;
            Projectile.Center += Projectile.velocity.SafeNormalize(formationAim) * speed;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            if (cycle == 16 && Main.myPlayer == Projectile.owner)
                FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.owner,
                    FranBurstStyle.SwordArray, 0.44f);
        }
        else
        {
            float recall = FranMotion.EaseOutCubic(FranMotion.Segment(cycle, 32f, CycleLength - 1f));
            Projectile.Center = Vector2.Lerp(Projectile.Center, formationPosition, 0.12f + recall * 0.25f);
            Vector2 returnDirection = (formationPosition - Projectile.Center).SafeNormalize(formationAim);
            Projectile.rotation = returnDirection.ToRotation() + MathHelper.PiOver4;
        }

        if (cycle == CycleLength - 1)
        {
            Projectile.Center = Vector2.Lerp(Projectile.Center, formationPosition, 0.55f);
            FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.owner,
                FranBurstStyle.SwordArray, 0.28f);
        }

        Projectile.ai[0]++;
        if (!Main.dedServ && cycle is >= 16 and < 32)
            FranParticleEffects.StarlightWake(Projectile.Center,
                (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2() * 12f,
                new Color(155, 235, 255));
        if (cycle is >= 16 and < 32 && Main.rand.NextBool(3))
        {
            Vector2 direction = (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2();
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, -direction * 2.2f,
                70, default, 0.95f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.2f, 0.27f, 0.35f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Item[ItemID.BreakerBlade].Value;
        int cycle = CurrentCycle;
        float dashIntensity = FranMotion.Pulse(cycle, 13f, 18f, 30f, 38f);
        Vector2 direction = (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2();
        Color resonanceBlue = Color.Lerp(new Color(55, 150, 255), new Color(205, 245, 255),
            0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.identity));
        FranVfx.BeginAdditive();

        FranVfx.DrawShared("Cal_ForwardSmear", Projectile.Center - direction * 28f,
            resonanceBlue * (0.16f + dashIntensity * 0.48f), Projectile.rotation - MathHelper.PiOver4,
            new Vector2(0.13f + dashIntensity * 0.09f, 0.06f + dashIntensity * 0.035f));
        FranVfx.DrawVertical("Cal_ThinEndedLine", Projectile.Center - direction * 96f,
            Projectile.Center + direction * 14f, resonanceBlue * (0.22f + dashIntensity * 0.52f),
            9f + dashIntensity * 12f);
        FranVfx.DrawVertical("Cal_ThinEndedLine", Projectile.Center - direction * 61f,
            Projectile.Center + direction * 12f, Color.White * (0.14f + dashIntensity * 0.68f),
            3f + dashIntensity * 4f);
        FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center + direction * 8f,
            Color.White * (0.24f + dashIntensity * 0.54f), Projectile.rotation,
            new Vector2(0.045f, 0.018f));
        FranVfx.DrawBloom(Projectile.Center, resonanceBlue, 15f + dashIntensity * 9f,
            0.16f + dashIntensity * 0.32f);
        for (int i = 4; i >= 1; i--)
            Main.EntitySpriteDraw(texture, Projectile.Center - direction * i * 9f - Main.screenPosition, null,
                resonanceBlue * (dashIntensity * 0.11f * (5 - i)), Projectile.rotation,
                texture.Size() * 0.5f, 0.72f, SpriteEffects.None, 0f);
        FranVfx.EndAdditive();
        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation,
            texture.Size() * 0.5f, 0.72f, SpriteEffects.None, 0f);
        return false;
    }
}

public sealed class FloatingSwordLaser : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 10; Projectile.height = 10; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = 2; Projectile.timeLeft = 50;
        Projectile.tileCollide = false; Projectile.extraUpdates = 2;
    }
    public override bool? CanDamage() => Projectile.localAI[0] >= 2f && Projectile.timeLeft >= 6 ? null : false;
    public override void AI()
    {
        if (Projectile.localAI[0]++ < 12f && Projectile.velocity.Length() < 23f)
            Projectile.velocity *= 1.025f;
        if (Projectile.timeLeft < 12)
            Projectile.velocity *= 0.94f;
        if (!Main.dedServ && ((int)Projectile.localAI[0] % 3 == 0))
            FranParticleEffects.StarlightWake(Projectile.Center, Projectile.velocity, new Color(155, 255, 115));
        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, Vector2.Zero, 70, default, 1f);
        d.noGravity = true;
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float age = 50f - Projectile.timeLeft;
        float opacity = FranMotion.Smooth(FranMotion.Segment(age, 0f, 4f)) *
            FranMotion.Smooth(FranMotion.Segment(Projectile.timeLeft, 0f, 12f));
        Color malachite = new(155, 255, 115);
        FranVfx.BeginAdditive();
        FranVfx.DrawCappedLaser(Projectile.Center - direction * 56f, Projectile.Center + direction * 10f,
            new Color(55, 180, 70), malachite, 4.4f, opacity);
        FranVfx.DrawShared("Cal_PulseStar", Projectile.Center, Color.White * (0.84f * opacity),
            Projectile.rotation, 0.065f);
        FranVfx.DrawBloom(Projectile.Center, malachite, 11f, 0.42f * opacity);
        FranVfx.EndAdditive();
        return false;
    }
    public override void OnKill(int timeLeft) =>
        FranBurst.SpawnForOwner(Projectile.GetSource_Death(), Projectile.Center, Projectile.owner,
            FranBurstStyle.SwordArray, 0.34f);
}

public sealed class AirDrawSlashProjectile : ModProjectile
{
    private bool released;
    private float currentBladeAngle;
    private int facing;
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 160; Projectile.height = 160; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 45;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool? CanDamage() => Projectile.timeLeft is <= 27 and >= 20 ? null : false;
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
        facing = Math.Sign(direction.X);
        if (facing == 0)
            facing = owner.direction;
        owner.ChangeDir(facing);
        float age = 45f - Projectile.timeLeft;
        float aimRotation = direction.ToRotation();
        float poseOffset = !released
            ? MathHelper.Lerp(0.54f * facing, -0.12f * facing,
                FranMotion.EaseInCubic(FranMotion.Segment(age, 0f, 18f)))
            : MathHelper.Lerp(-0.12f * facing, 0.18f * facing,
                FranMotion.Smooth(FranMotion.Segment(age, 18f, 34f)));
        currentBladeAngle = aimRotation + poseOffset;
        Projectile.Center = owner.MountedCenter + direction * 70f;
        Projectile.rotation = currentBladeAngle + MathHelper.PiOver4 +
            (facing < 0 ? MathHelper.PiOver2 : 0f);
        owner.heldProj = Projectile.whoAmI;
        owner.itemAnimation = owner.itemTime = 2;
        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full,
            currentBladeAngle - MathHelper.PiOver2);

        if (!released && Projectile.timeLeft <= 27)
        {
            released = true;
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.34f, Volume = 0.92f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.48f, Volume = 0.38f }, Projectile.Center);
            owner.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 3.6f);
            FranVfx.DustLine(owner.Center, owner.Center + direction * 190f, DustID.Cloud, 14f, 1.05f);
            FranParticleEffects.LineRelease(owner.Center + direction * 18f,
                owner.Center + direction * 235f, new Color(205, 245, 255), 20, 1.12f);
            FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), owner.Center + direction * 74f,
                Projectile.owner, FranBurstStyle.AirDraw, 1.15f);
        }
        if (!released)
            Lighting.AddLight(owner.Center, 0.18f, 0.32f, 0.38f);
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner,
            FranBurstStyle.AirDraw, 0.82f);
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (!released)
            return false;
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center - Projectile.velocity * 85f, Projectile.Center + Projectile.velocity * 120f,
            28f, ref collisionPoint);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Player owner = Main.player[Projectile.owner];
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float age = 45f - Projectile.timeLeft;
        FranVfx.BeginAdditive();
        if (released)
        {
            float opacity = FranMotion.Pulse(age, 17f, 19f, 24f, 34f);
            Vector2 start = owner.MountedCenter - direction * 78f;
            Vector2 end = owner.MountedCenter + direction * 248f;

            // One-frame-draw precision: a broad curved air shell, a compressed inner blade and split wakes.
            FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", owner.MountedCenter + direction * 42f,
                new Color(105, 205, 245) * (0.48f * opacity), currentBladeAngle + MathHelper.PiOver2,
                new Vector2(1.36f, 0.72f));
            FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", owner.MountedCenter + direction * 58f,
                Color.White * (0.32f * opacity), currentBladeAngle - MathHelper.PiOver2,
                new Vector2(1.02f, 0.54f));
            FranVfx.DrawVertical("Cal_GlowBlade", start, end,
                new Color(115, 225, 255) * (0.46f * opacity), 54f);
            FranVfx.DrawVertical("Cal_ThinEndedLine", start + direction * 28f, end,
                Color.White * (0.82f * opacity), 9f);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector2 normal = direction.RotatedBy(MathHelper.PiOver2) * (24f + age * 1.2f) * i;
                FranVfx.DrawStretched("Cal_FadeStreak", owner.MountedCenter + normal,
                    owner.MountedCenter + direction * 205f + normal * 0.28f,
                    new Color(120, 205, 235) * (0.24f * opacity), 13f);
            }
            FranVfx.DrawShared("Cal_ShineFlare", owner.MountedCenter + direction * 104f,
                Color.White * (0.68f * opacity), currentBladeAngle,
                new Vector2(0.14f, 0.035f));
        }
        else
        {
            float charge = FranMotion.EaseInCubic(FranMotion.Segment(age, 0f, 18f));
            float radius = MathHelper.Lerp(62f, 19f, charge);
            Color chargeColor = Color.Lerp(new Color(90, 175, 215), Color.White, charge * 0.72f);
            FranVfx.DrawRing(owner.MountedCenter, chargeColor, radius, 0.14f + charge * 0.34f,
                Main.GlobalTimeWrappedHourly * (0.8f + charge * 2.4f), "Cal_BloomRing");
            FranVfx.DrawShared("Cal_CircularSmear", owner.MountedCenter,
                chargeColor * (0.10f + charge * 0.30f),
                Main.GlobalTimeWrappedHourly * (1.1f + charge * 2f), 0.30f + charge * 0.10f);
            for (int i = 0; i < 3; i++)
            {
                float angle = Main.GlobalTimeWrappedHourly * (0.8f + charge * 1.9f) + i * MathHelper.TwoPi / 3f;
                Vector2 point = owner.MountedCenter + angle.ToRotationVector2() * radius;
                FranVfx.DrawTexturedSpark(point, (owner.MountedCenter - point).SafeNormalize(Vector2.UnitY),
                    chargeColor, 24f + charge * 22f, 2.5f + charge * 2f, 0.25f + charge * 0.45f);
            }
        }
        FranVfx.EndAdditive();

        Texture2D teacherTexture = TextureAssets.Item[ItemID.BreakerBlade].Value;
        SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 origin = new(facing < 0 ? teacherTexture.Width : 0f, teacherTexture.Height);
        Vector2 bladeCenter = owner.MountedCenter + currentBladeAngle.ToRotationVector2() * 8f - Main.screenPosition;
        Color bladeColor = Color.Lerp(lightColor, Color.White, released ? 0.28f : 0.12f);
        Main.EntitySpriteDraw(teacherTexture, bladeCenter, null, bladeColor, Projectile.rotation,
            origin, 1.05f, effects, 0f);
        return false;
    }
}

public sealed class LightningSpearProjectile : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 8; Projectile.height = 8; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 18;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.timeLeft is <= 16 and >= 9 ? null : false;
    public override bool? CanHitNPC(NPC target) =>
        Projectile.ai[1] >= 0f ? target.whoAmI == (int)Projectile.ai[1] : null;
    public override void AI()
    {
        Projectile.localAI[0]++;
        Projectile.rotation = Projectile.velocity.ToRotation();
        Vector2 end = Projectile.Center + Projectile.velocity;
        if (!Main.dedServ)
        {
            FranParticleEffects.InwardSpark(end, new Color(175, 135, 255), 18f, 62f, 5.8f, 0.82f);
            if (Projectile.localAI[0] == 1f)
            {
                FranLightingBoltsSystem.SpawnRuneActivation(end, new Color(155, 125, 255), 0.72f);
                FranLightingBoltsSystem.SpawnLightningFragments(end, new Color(125, 220, 255), 8, 0.62f);
            }
            if (((int)Projectile.localAI[0] & 1) == 0)
                FranParticleEffects.StarlightWake(end, Projectile.velocity.SafeNormalize(Vector2.UnitX) * 7f,
                    new Color(135, 225, 255));
        }
        if (Main.rand.NextBool(2))
        {
            Dust d = Dust.NewDustPerfect(Vector2.Lerp(Projectile.Center, end, Main.rand.NextFloat()),
                DustID.Electric, Main.rand.NextVector2Circular(1f, 1f),
                40, default, 1.05f);
            d.noGravity = true;
        }
        Lighting.AddLight(Vector2.Lerp(Projectile.Center, end, 0.5f), 0.28f, 0.22f, 0.62f);
    }

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
            Projectile.Center, Projectile.Center + Projectile.velocity, 12f, ref collisionPoint);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float opacity = MathF.Sin((18f - Projectile.timeLeft) / 18f * MathHelper.Pi);
        Color arcColor = Color.Lerp(new Color(120, 70, 225), new Color(135, 235, 255),
            0.35f + 0.15f * Projectile.ai[0]);
        float seed = Projectile.identity * 0.71f + Main.GlobalTimeWrappedHourly * 22f;
        Vector2 start = Projectile.Center;
        Vector2 end = Projectile.Center + Projectile.velocity;
        FranVfx.BeginAdditive();
        FranVfx.DrawTexturedLightning(start, end, arcColor, 5.5f, 10, seed, opacity);
        FranVfx.DrawTexturedLightning(start, end, Color.White, 2.1f, 8, seed + 4.7f, opacity * 0.76f);
        FranVfx.DrawShared("LightningImpact", end, arcColor * (0.50f * opacity),
            Projectile.rotation, 0.24f);
        FranVfx.DrawBloom(end, arcColor, 18f, 0.48f * opacity);
        FranVfx.EndAdditive();
        return false;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        FranVfx.SparkBurst(target.Center, new Color(185, 145, 255), 7, 5f, 0.95f);
        FranLightingBoltsSystem.SpawnThunderImpact(target.Center, new Color(155, 135, 255), 0.62f);
        Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(target.Center, 2f);
    }
    public override void OnKill(int timeLeft)
    {
        FranBurst.SpawnForOwner(Projectile.GetSource_Death(), Projectile.Center + Projectile.velocity,
            Projectile.owner, FranBurstStyle.Lightning, 0.38f);
    }
}

public sealed class ShipCutterProjectile : ModProjectile
{
    private float currentAngle;
    private float progress;
    private float currentReach;
    private float slashIntensity;
    private bool released;
    private int facing;
    public override string Texture => $"Terraria/Images/Item_{ItemID.BreakerBlade}";
    public override void SetDefaults()
    {
        Projectile.width = 210; Projectile.height = 210; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 70;
        Projectile.tileCollide = false; Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool? CanDamage() => progress is >= 0.39f and <= 0.66f ? null : false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }
        if (facing == 0)
        {
            facing = Math.Sign(Projectile.velocity.X);
            if (facing == 0)
                facing = owner.direction;
        }
        owner.ChangeDir(facing);

        progress = 1f - Projectile.timeLeft / 70f;
        float angleOffset;
        if (progress < 0.35f)
            angleOffset = MathHelper.Lerp(-1.64f, -2.18f,
                FranMotion.EaseInCubic(FranMotion.Segment(progress, 0f, 0.35f)));
        else if (progress < 0.55f)
            angleOffset = MathHelper.Lerp(-2.18f, 1.72f,
                FranMotion.EaseOutQuart(FranMotion.Segment(progress, 0.35f, 0.55f)));
        else if (progress < 0.70f)
            angleOffset = MathHelper.Lerp(1.72f, 1.80f,
                FranMotion.Smooth(FranMotion.Segment(progress, 0.55f, 0.70f)));
        else
            angleOffset = MathHelper.Lerp(1.80f, 1.53f,
                FranMotion.Smooth(FranMotion.Segment(progress, 0.70f, 1f)));

        currentAngle = Projectile.velocity.ToRotation() + angleOffset * facing;
        slashIntensity = FranMotion.Pulse(progress, 0.34f, 0.40f, 0.66f, 0.84f);
        currentReach = 108f + slashIntensity * 42f;
        Projectile.Center = owner.MountedCenter + currentAngle.ToRotationVector2() * currentReach * 0.62f;
        Projectile.rotation = currentAngle + MathHelper.PiOver4 + (facing < 0 ? MathHelper.PiOver2 : 0f);
        owner.itemAnimation = owner.itemTime = 2;
        owner.itemRotation = currentAngle + (facing < 0 ? MathHelper.Pi : 0f);
        owner.heldProj = Projectile.whoAmI;
        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentAngle - MathHelper.PiOver2);
        if (!released && progress >= 0.37f)
        {
            released = true;
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.22f, Volume = 0.95f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.48f, Volume = 0.62f }, Projectile.Center);
            owner.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 7f);
            FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), owner.MountedCenter + currentAngle.ToRotationVector2() * 215f,
                Projectile.owner, FranBurstStyle.ShipCutter, 1.65f);
            Vector2 releaseDirection = currentAngle.ToRotationVector2();
            FranParticleEffects.WaterBurst(owner.MountedCenter + releaseDirection * 205f,
                releaseDirection, new Color(115, 225, 255), 26, 1.25f);
            FranLightingBoltsSystem.SpawnSpectralWhispers(owner.MountedCenter + releaseDirection * 185f,
                new Color(135, 235, 255), 68f, 14);
        }
        if (!Main.dedServ && slashIntensity > 0.12f)
        {
            Vector2 bladeDirection = currentAngle.ToRotationVector2();
            Vector2 tangent = bladeDirection.RotatedBy(MathHelper.PiOver2 * facing);
            FranParticleEffects.MeleeTrail(owner.MountedCenter + bladeDirection * (250f + slashIntensity * 120f),
                bladeDirection, tangent, new Color(255, 210, 145), slashIntensity, 2, false);
        }
        Lighting.AddLight(Projectile.Center, 0.65f, 0.5f, 0.22f);
    }
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        Player owner = Main.player[Projectile.owner];
        float collisionPoint = 0f;
        return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), owner.MountedCenter,
            owner.MountedCenter + currentAngle.ToRotationVector2() * (350f + slashIntensity * 70f),
            42f + slashIntensity * 20f, ref collisionPoint);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Item[ItemID.BreakerBlade].Value;
        Player owner = Main.player[Projectile.owner];
        float releaseOpacity = slashIntensity;
        Vector2 direction = currentAngle.ToRotationVector2();
        FranVfx.BeginAdditive();
        if (!released)
        {
            float charge = FranMotion.EaseInCubic(FranMotion.Segment(progress, 0f, 0.36f));
            Color chargeColor = Color.Lerp(new Color(205, 150, 75), Color.White, charge * 0.64f);
            FranVfx.DrawRing(owner.MountedCenter, chargeColor, MathHelper.Lerp(102f, 38f, charge),
                0.12f + charge * 0.34f, -Main.GlobalTimeWrappedHourly * (0.45f + charge * 1.7f), "Cal_BloomRing");
            FranVfx.DrawShared("Cal_CircularSmear", owner.MountedCenter,
                new Color(255, 170, 70) * (0.11f + charge * 0.24f),
                Main.GlobalTimeWrappedHourly * (0.6f + charge * 1.4f), 0.52f - charge * 0.18f);
            for (int i = 0; i < 4; i++)
            {
                float angle = Main.GlobalTimeWrappedHourly * 0.72f + i * MathHelper.PiOver2;
                Vector2 point = owner.MountedCenter + angle.ToRotationVector2() * MathHelper.Lerp(94f, 31f, charge);
                FranVfx.DrawTexturedSpark(point, (owner.MountedCenter - point).SafeNormalize(Vector2.UnitY),
                    chargeColor, 22f + charge * 34f, 2.8f + charge * 2.4f, 0.22f + charge * 0.46f);
            }
            FranVfx.DrawBloom(owner.MountedCenter, chargeColor, 28f + charge * 18f, 0.08f + charge * 0.18f);
        }
        if (releaseOpacity > 0f)
        {
            Vector2 center = owner.MountedCenter + direction * 185f;
            Color outer = Color.Lerp(new Color(255, 135, 55), new Color(255, 235, 175), slashIntensity);
            // Heavy Calamity melee hierarchy: three offset curved shells, then a tight luminous blade.
            for (int i = 0; i < 3; i++)
            {
                float echo = 1f - i * 0.24f;
                float angle = currentAngle + MathHelper.PiOver2 - facing * i * 0.075f;
                FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", owner.MountedCenter + direction * (45f + i * 18f),
                    outer * (0.40f * echo * releaseOpacity), angle,
                    new Vector2(2.05f - i * 0.18f, 1.12f - i * 0.12f));
            }
            FranVfx.DrawShared("Cal_ForwardSmear", center, outer * (0.52f * releaseOpacity),
                currentAngle - MathHelper.PiOver2, new Vector2(0.54f, 0.16f));
            FranVfx.DrawSharedPixels("CLCB_BrinySlash", center + direction * 26f,
                new Color(105, 235, 255) * (0.46f * releaseOpacity),
                currentAngle + MathHelper.PiOver2, new Vector2(460f, 28f));
            FranVfx.DrawVertical("Cal_GlowBlade", owner.MountedCenter - direction * 42f,
                owner.MountedCenter + direction * 435f, outer * (0.56f * releaseOpacity), 86f);
            FranVfx.DrawVertical("Cal_ThinEndedLine", owner.MountedCenter + direction * 18f,
                owner.MountedCenter + direction * 410f, Color.White * (0.82f * releaseOpacity), 13f);
            FranVfx.DrawShared("Cal_ShineFlare", center + direction * 62f,
                Color.White * (0.72f * releaseOpacity), currentAngle,
                new Vector2(0.24f, 0.045f));
            FranVfx.DrawBloom(center, new Color(255, 205, 135), 82f, 0.28f * releaseOpacity);
        }
        FranVfx.EndAdditive();
        SpriteEffects effects = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 origin = new(facing < 0 ? texture.Width : 0f, texture.Height);
        Main.EntitySpriteDraw(texture, owner.MountedCenter + direction * 10f - Main.screenPosition, null,
            new Color(255, 225, 175), Projectile.rotation, origin, 3.25f, effects, 0f);
        return false;
    }
}

public sealed class ThorsHammerMarker : ModProjectile
{
    private int TargetIndex => (int)Projectile.ai[0];

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 120; Projectile.height = 120; Projectile.timeLeft = 75;
        Projectile.tileCollide = false; Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void AI()
    {
        NPC trackedTarget = GetTrackedTarget();
        if (trackedTarget is not null)
            Projectile.Center = trackedTarget.Center;

        float age = 75f - Projectile.timeLeft;
        float focus = FranMotion.EaseInCubic(FranMotion.Segment(age, 10f, 49f));
        Lighting.AddLight(Projectile.Center, 0.12f, 0.22f, 0.65f);
        if (!Main.dedServ && Projectile.timeLeft % (focus > 0.72f ? 1 : 2) == 0)
            FranParticleEffects.InwardSpark(Projectile.Center, new Color(115, 185, 255),
                MathHelper.Lerp(62f, 30f, focus), MathHelper.Lerp(108f, 52f, focus),
                MathHelper.Lerp(2.5f, 6f, focus), 0.8f + focus * 0.35f);
        if (Projectile.timeLeft % (focus > 0.72f ? 2 : 4) == 0 && !Main.dedServ)
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = MathHelper.Lerp(92f, 42f, focus);
            Vector2 spawn = Projectile.Center + angle.ToRotationVector2() * radius;
            Dust dust = Dust.NewDustPerfect(spawn, DustID.Electric,
                (Projectile.Center - spawn).SafeNormalize(Vector2.Zero) * MathHelper.Lerp(1.5f, 4.2f, focus),
                35, default, 0.9f + focus * 0.4f);
            dust.noGravity = true;
        }
        if (Projectile.timeLeft == 25)
        {
            if (Main.myPlayer == Projectile.owner)
            {
                Vector2 spawn = Projectile.Center + new Vector2(Main.rand.NextFloat(-130f, 130f), -720f);
                Vector2 velocity = (Projectile.Center - spawn).SafeNormalize(Vector2.UnitY) * 24f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawn, velocity,
                    ModContent.ProjectileType<FranThorsHammerDrop>(), Projectile.damage, Projectile.knockBack,
                    Projectile.owner, Projectile.Center.X, Projectile.Center.Y, TargetIndex);
            }
            Projectile.Kill();
            return;
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float age = 75f - Projectile.timeLeft;
        float introduction = FranMotion.EaseOutBack(FranMotion.Segment(age, 0f, 12f));
        float focus = FranMotion.EaseInCubic(FranMotion.Segment(age, 10f, 50f));
        float preflash = FranMotion.Smooth(FranMotion.Segment(age, 44f, 50f));
        float opacity = introduction * (1f - FranMotion.Smooth(FranMotion.Segment(age, 44f, 50f)));
        float outerRadius = MathHelper.Lerp(108f, 55f, focus);
        Color telegraphColor = Color.Lerp(new Color(65, 95, 225), Color.White, preflash * 0.72f);
        FranVfx.BeginAdditive();
        FranVfx.DrawRing(Projectile.Center, telegraphColor, outerRadius,
            opacity * (0.24f + focus * 0.42f), Main.GlobalTimeWrappedHourly * (0.45f + focus * 3.2f), "Cal_FadedStarRing");
        FranVfx.DrawRing(Projectile.Center, Color.White, MathHelper.Lerp(72f, 31f, focus),
            opacity * (0.10f + focus * 0.40f), -Main.GlobalTimeWrappedHourly * (0.8f + focus * 3.8f), "Cal_BloomRing");
        FranVfx.DrawShared("Cal_PulseStar", Projectile.Center,
            new Color(120, 190, 255) * (opacity * (0.18f + focus * 0.48f)),
            Main.GlobalTimeWrappedHourly * 1.7f, 0.22f + focus * 0.10f);
        FranVfx.DrawBloom(Projectile.Center, telegraphColor, 24f + preflash * 34f,
            opacity * (0.12f + preflash * 0.42f), preflash > 0.7f);
        for (int i = 0; i < 12; i++)
        {
            float angle = MathHelper.TwoPi * i / 12f + Main.GlobalTimeWrappedHourly * 0.24f;
            Vector2 point = Projectile.Center + angle.ToRotationVector2() * outerRadius;
            FranVfx.DrawTexturedSpark(point, (Projectile.Center - point).SafeNormalize(Vector2.UnitY),
                i % 2 == 0 ? telegraphColor : new Color(95, 210, 255),
                18f + focus * 30f, 2.4f + focus * 2.6f, opacity * (0.34f + focus * 0.54f));
        }
        FranVfx.EndAdditive();
        return false;
    }

    private NPC GetTrackedTarget()
    {
        if (!Main.npc.IndexInRange(TargetIndex))
            return null;
        NPC target = Main.npc[TargetIndex];
        return target.active && !target.friendly && target.life > 0 ? target : null;
    }
}

public sealed class ThorsHammerBolt : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 260; Projectile.height = 260; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = -1; Projectile.timeLeft = 20;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
    }
    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => Projectile.timeLeft is <= 19 and >= 12 ? null : false;
    public override void AI()
    {
        if (Projectile.localAI[0]++ == 0f)
        {
            FranBurst.SpawnForOwner(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.owner,
                FranBurstStyle.ThorsHammer, 1.8f);
            FranVfx.SparkBurst(Projectile.Center, new Color(120, 165, 255), 26, 12f, 1.25f);
            FranLightingBoltsSystem.SpawnThunderImpact(Projectile.Center, new Color(105, 175, 255), 1.35f);
            FranLightingBoltsSystem.SpawnLightningFragments(Projectile.Center, new Color(145, 235, 255), 28, 1.15f);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f, Volume = 1f }, Projectile.Center);
            FranSpectatorVfx.SpawnImpactPetals(Projectile.GetSource_FromThis(), Projectile.Center,
                -Vector2.UnitY, Projectile.owner, FranSpectatorPalette.Lightning, 7, 1.35f);
            Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 13f);
            if (!Main.dedServ)
            {
                for (int i = 0; i < 18; i++)
                {
                    Vector2 direction = Main.rand.NextVector2Unit();
                    FranParticleSystem.Spawn(new FranHeavySmokeParticle(
                        Projectile.Center + direction * Main.rand.NextFloat(8f, 54f),
                        direction * Main.rand.NextFloat(2.2f, 8.5f) - Vector2.UnitY * Main.rand.NextFloat(1f, 5f),
                        new Color(30, 42, 72), Main.rand.NextFloat(0.65f, 1.25f), Main.rand.Next(28, 45),
                        Main.rand.NextFloat(0.38f, 0.68f), glowing: true));
                }
                for (int i = 0; i < 24; i++)
                {
                    Vector2 direction = Main.rand.NextVector2Unit();
                    FranParticleSystem.Spawn(new FranBarrierShardParticle(Projectile.Center,
                        direction * Main.rand.NextFloat(4.5f, 15f),
                        i % 3 == 0 ? Color.White : new Color(105, 165, 255),
                        new Vector2(Main.rand.NextFloat(16f, 34f), Main.rand.NextFloat(3f, 8f)),
                        Main.rand.Next(18, 32)));
                }
            }
        }
        for (int i = 0; i < 2; i++)
        {
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(35f, 35f), DustID.Electric,
                Main.rand.NextVector2Circular(2f, 2f), 40, default, 1.5f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.5f, 0.6f, 1f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float seed = Projectile.identity * 0.83f + Main.GlobalTimeWrappedHourly * 26f;
        Vector2 start = Projectile.Center - Vector2.UnitY * 900f;
        Vector2 end = Projectile.Center + Vector2.UnitY * 38f;
        float age = 20f - Projectile.timeLeft;
        float opacity = FranMotion.Smooth(FranMotion.Segment(age, 0f, 2f)) *
            (1f - FranMotion.Smooth(FranMotion.Segment(age, 6f, 16f)));
        float coreFlash = 1f - FranMotion.Smooth(FranMotion.Segment(age, 1f, 7f));
        FranVfx.BeginAdditive();
        // This greyscale emission mask belongs in the glow pass. AlphaBlend exposes its dark RGB
        // bed as a rotating square during the hammer detonation.
        FranVfx.DrawShared("Cal_CircularSmearSmokey", Projectile.Center,
            new Color(72, 98, 205) * (0.48f * opacity), Main.GlobalTimeWrappedHourly * 1.7f,
            0.96f + coreFlash * 0.34f);
        Color azure = Color.Lerp(new Color(65, 105, 255), new Color(120, 245, 255), coreFlash * 0.6f);
        FranVfx.DrawCappedLaser(start, end, new Color(45, 60, 205), azure,
            18f + coreFlash * 14f, opacity);
        FranVfx.DrawTexturedLightning(start, end, azure, 12f + coreFlash * 5f, 13, seed, opacity);
        FranVfx.DrawTexturedLightning(start + new Vector2(-32f, 40f), end + new Vector2(-54f, -12f),
            new Color(95, 165, 255), 5f, 9, seed + 4.2f, opacity * 0.62f);
        FranVfx.DrawTexturedLightning(start + new Vector2(26f, 90f), end + new Vector2(62f, 4f),
            new Color(105, 225, 255), 4.5f, 8, seed + 8.7f, opacity * 0.54f);
        FranVfx.DrawShared("LightningImpact", Projectile.Center, azure * (0.78f * opacity), 0f, 0.90f);
        FranVfx.DrawShared("Cal_CircularSmear", Projectile.Center, azure * (0.62f * opacity),
            Main.GlobalTimeWrappedHourly * 7f, 0.82f + coreFlash * 0.25f);
        FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center, Color.White * (0.86f * opacity),
            0f, new Vector2(0.42f, 0.085f));
        for (int i = 0; i < 3; i++)
            FranVfx.DrawRing(Projectile.Center, i == 1 ? Color.White : azure,
                58f + i * 42f + (1f - coreFlash) * (58f + i * 24f),
                opacity * (0.62f - i * 0.14f), i * 0.7f + age * 0.06f,
                i == 2 ? "Cal_HighResHollowCircle" : "Cal_BloomRing");
        FranVfx.DrawBloom(Projectile.Center, Color.White, 96f, 0.48f * opacity, true);
        FranVfx.EndAdditive();
        return false;
    }
    public override void OnKill(int timeLeft)
    {
        FranVfx.SparkBurst(Projectile.Center, new Color(105, 145, 255), 7, 3.5f, 0.8f);
    }
}

// Fran's death punctuation follows the Xyk death animation's actual timing language:
// 25 frames of inward-moving sparks, then layered colored/white pulses and six axial streaks.
public sealed class FranDeathVisual : ModProjectile
{
    private const int CollapseTime = 25;
    private float Time => Projectile.localAI[0];

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = 43;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage() => false;

    public override void AI()
    {
        Projectile.localAI[0]++;
        float age = Time;
        if (age < CollapseTime)
        {
            if (!Main.dedServ && (int)age % 2 == 0)
            {
                FranParticleEffects.InwardSpark(Projectile.Center, new Color(105, 195, 255),
                    90f, 180f, 7.4f, 1.05f);
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawn = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(90f, 180f);
                Dust dust = Dust.NewDustPerfect(spawn, DustID.FireworksRGB,
                    (Projectile.Center - spawn).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3.8f, 7.4f),
                    40, new Color(105, 195, 255), Main.rand.NextFloat(0.65f, 1.05f));
                dust.noGravity = true;
            }
        }
        else if ((int)age == CollapseTime)
        {
            FranParticleEffects.Implosion(Projectile.Center, new Color(115, 205, 255), 1.55f);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.42f, Volume = 0.92f }, Projectile.Center);
            FranVfx.SparkBurst(Projectile.Center, new Color(115, 205, 255), 30, 15f, 1.15f);
            FranVfx.Burst(Projectile.Center, DustID.PurpleTorch, 18, 9f, 1.25f);
            if (Main.player.IndexInRange(Projectile.owner))
                Main.player[Projectile.owner].GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, 8f);
        }

        float light = age < CollapseTime ? MathHelper.Lerp(0.18f, 0.72f, age / CollapseTime) :
            MathHelper.Lerp(1f, 0f, FranMotion.Clamp01((age - CollapseTime) / 18f));
        Lighting.AddLight(Projectile.Center, new Vector3(0.16f, 0.38f, 0.62f) * light);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float age = Time;
        Color blue = new(75, 165, 255);
        Color cyan = new(125, 245, 255);
        FranVfx.BeginAdditive();

        if (age < CollapseTime)
        {
            float completion = FranMotion.Clamp01(age / CollapseTime);
            float collapse = FranMotion.EaseInCubic(completion);
            float radius = MathHelper.Lerp(220f, 15f, collapse);
            float opacity = 0.28f + completion * 0.62f;
            for (int i = 0; i < 6; i++)
            {
                float angle = i * MathHelper.TwoPi / 6f + Projectile.identity * 0.37f + age * 0.025f;
                Vector2 direction = angle.ToRotationVector2();
                Vector2 point = Projectile.Center + direction * radius;
                FranVfx.DrawTexturedSpark(point, -direction, i % 2 == 0 ? blue : cyan,
                    36f + completion * 54f, 3.5f + completion * 4.5f, opacity);
            }
            FranVfx.DrawShared("Cal_HighResHollowCircle", Projectile.Center, blue * (0.30f + completion * 0.34f),
                -age * 0.035f, 0.018f + (1f - completion) * 0.085f);
            FranVfx.DrawShared("Cal_PulseStar", Projectile.Center, Color.White * (completion * 0.62f),
                age * 0.08f, 0.08f + completion * 0.12f);
            FranVfx.DrawBloom(Projectile.Center, cyan, 18f + completion * 34f, 0.22f + completion * 0.36f);
        }
        else
        {
            float completion = FranMotion.Clamp01((age - CollapseTime) / 18f);
            float expansion = 1f - MathF.Pow(1f - completion, 4f);
            float opacity = 1f - completion;

            // Xyk's three colored + three white pulses, deliberately different in size and rotation.
            for (int i = 0; i < 3; i++)
            {
                float radius = MathHelper.Lerp(18f + i * 9f, 150f + i * 62f, expansion);
                FranVfx.DrawRing(Projectile.Center, i % 2 == 0 ? blue : cyan, radius,
                    opacity * (0.64f - i * 0.10f), i * 0.47f + age * 0.02f, "Cal_BloomRing");
                FranVfx.DrawRing(Projectile.Center, Color.White, radius * 0.58f,
                    opacity * (0.50f - i * 0.08f), -i * 0.61f - age * 0.018f, "Cal_BloomRing");
            }

            for (int i = 0; i < 6; i++)
            {
                float angle = i * MathHelper.Pi / 3f;
                Vector2 direction = angle.ToRotationVector2();
                Vector2 point = Projectile.Center + direction * expansion * 78f;
                FranVfx.DrawTexturedSpark(point, direction, i % 2 == 0 ? cyan : blue,
                    MathHelper.Lerp(42f, 126f, expansion), MathHelper.Lerp(9f, 2.5f, completion), opacity);
            }

            // Thirty small shards form the irregular outer break-up from the original death effect.
            for (int i = 0; i < 30; i++)
            {
                float angle = i * MathHelper.TwoPi / 30f + MathF.Sin(i * 4.13f + Projectile.identity) * 0.12f;
                float speed = 70f + (i % 7) * 11f;
                Vector2 point = Projectile.Center + angle.ToRotationVector2() * speed * expansion;
                FranVfx.DrawShared(i % 3 == 0 ? "Cal_SimpleStar" : "Cal_StarProj", point,
                    Color.Lerp(blue, Color.White, i % 4 * 0.18f) * (opacity * 0.52f),
                    angle + age * (i % 2 == 0 ? 0.06f : -0.045f), 0.032f + i % 5 * 0.004f);
            }

            FranVfx.DrawShared("Cal_HighResHollowCircle", Projectile.Center, cyan * (0.72f * opacity),
                MathHelper.PiOver4, 0.018f + expansion * 0.18f);
            FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center, Color.White * (0.88f * opacity),
                0f, new Vector2(0.48f, 0.09f) * (0.55f + expansion * 0.45f));
            FranVfx.DrawBloom(Projectile.Center, Color.White, MathHelper.Lerp(92f, 28f, completion), 0.62f * opacity, true);
        }

        FranVfx.EndAdditive();
        return false;
    }
}

public enum FranBurstStyle : byte
{
    Slash,
    Telekinetic,
    SkillTaker,
    Barrier,
    SwordArray,
    Flame,
    AirDraw,
    Lightning,
    ShipCutter,
    ThorsHammer,
    DashImpact,
    Potential
}

public sealed class FranBurst : ModProjectile
{
    private FranBurstStyle Style => (FranBurstStyle)(int)Projectile.ai[0];
    private float VisualScale => Math.Max(0.2f, Projectile.ai[1]);
    private float VisualRotation => Projectile.ai[2];

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public static int Spawn(IEntitySource source, Vector2 center, int owner, FranBurstStyle style, float scale,
        int damage = 0, float knockback = 0f, float? rotation = null)
    {
        return Projectile.NewProjectile(source, center, Vector2.Zero, ModContent.ProjectileType<FranBurst>(),
            damage, knockback, owner, (float)style, scale, rotation ?? Main.rand.NextFloat(MathHelper.TwoPi));
    }

    public static int SpawnForOwner(IEntitySource source, Vector2 center, int owner, FranBurstStyle style, float scale,
        int damage = 0, float knockback = 0f, float? rotation = null)
    {
        if (Main.netMode == NetmodeID.Server || Main.myPlayer != owner)
            return -1;
        return Spawn(source, center, owner, style, scale, damage, knockback, rotation);
    }

    public override void SetDefaults()
    {
        Projectile.width = 86;
        Projectile.height = 86;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 20;
        Projectile.tileCollide = false;
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;
    }

    public override bool ShouldUpdatePosition() => false;
    public override bool? CanDamage()
    {
        float progress = 1f - Projectile.timeLeft / 20f;
        return Projectile.damage > 0 && progress is >= 0.08f and <= 0.58f ? null : false;
    }

    public override void AI()
    {
        Vector2 center = Projectile.Center;
        float progress = 1f - Projectile.timeLeft / 20f;
        float eased = 1f - MathF.Pow(1f - progress, 3f);
        float radius = MathHelper.Lerp(14f, 72f * VisualScale, eased);
        int size = Math.Max(24, (int)(radius * 2f));
        Projectile.width = size;
        Projectile.height = size;
        Projectile.Center = center;

        if (Projectile.localAI[0]++ != 0f)
            return;

        Color color = StyleColor(Style);
        if (Main.netMode != NetmodeID.Server)
        {
            FranParticleEffects.Burst(Projectile.Center, color, VisualScale, Style, VisualRotation);
            if (Style is FranBurstStyle.ThorsHammer or FranBurstStyle.ShipCutter or FranBurstStyle.DashImpact)
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.48f, Pitch = Style == FranBurstStyle.ThorsHammer ? -0.35f : 0.08f }, Projectile.Center);
            else if (Style == FranBurstStyle.Barrier)
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.58f, Pitch = 0.32f }, Projectile.Center);

            float shake = Style switch
            {
                FranBurstStyle.ThorsHammer => 13f,
                FranBurstStyle.ShipCutter => 9f,
                FranBurstStyle.DashImpact => 5f,
                FranBurstStyle.AirDraw => 4f,
                FranBurstStyle.Flame or FranBurstStyle.Slash => 3.2f,
                FranBurstStyle.Barrier or FranBurstStyle.Lightning => 2.5f,
                FranBurstStyle.Potential => 0.8f,
                _ => 1.5f
            };
            Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(Projectile.Center, shake);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        float progress = 1f - Projectile.timeLeft / 20f;
        float opacity = MathF.Sin(progress * MathHelper.Pi);
        float eased = 1f - MathF.Pow(1f - progress, 3f);
        float radius = MathHelper.Lerp(14f, 72f * VisualScale, eased);
        Color color = StyleColor(Style);

        if (Style == FranBurstStyle.Barrier)
            FranVfx.DrawShared("Cal_CircularSmearSmokey", Projectile.Center,
                new Color(10, 5, 22, 190) * opacity, VisualRotation + progress,
                0.24f * VisualScale * (0.72f + eased * 0.42f));
        else if (Style == FranBurstStyle.DashImpact)
        {
            FranVfx.DrawShared("Cal_CircularSmearSmokey", Projectile.Center,
                new Color(8, 4, 15, 220) * opacity, VisualRotation + progress * 1.8f,
                0.46f * VisualScale * (0.70f + eased * 0.48f));
            FranVfx.DrawShared("Smoke", Projectile.Center,
                new Color(18, 8, 28, 175) * opacity, -VisualRotation - progress,
                0.38f * VisualScale * (0.76f + eased * 0.42f));
        }

        FranVfx.BeginAdditive();
        switch (Style)
        {
            case FranBurstStyle.Slash:
                FranVfx.DrawShared("SlashWide", Projectile.Center, color * (0.58f * opacity),
                    VisualRotation, 0.28f * VisualScale * (0.75f + eased * 0.35f));
                break;
            case FranBurstStyle.Telekinetic:
                FranVfx.DrawRing(Projectile.Center, color, radius, 0.58f * opacity, VisualRotation, "Cal_BloomRing");
                FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.68f * opacity),
                    VisualRotation, new Vector2(radius * 2.15f, radius * 1.18f));
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                    Color.White * (0.76f * opacity), VisualRotation, new Vector2(radius * 1.22f, radius * 0.68f));
                break;
            case FranBurstStyle.SkillTaker:
                FranVfx.DrawOriginalPixels("FranResonanceOrb", Projectile.Center, color * (0.62f * opacity),
                    VisualRotation + progress, Vector2.One * radius * 1.72f);
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                    Color.White * (0.66f * opacity), -VisualRotation, new Vector2(radius * 1.34f, radius * 0.72f));
                for (int i = 0; i < 5; i++)
                {
                    float angle = VisualRotation + i * MathHelper.TwoPi / 5f + progress * 1.6f;
                    Vector2 point = Projectile.Center + angle.ToRotationVector2() * radius * 0.78f;
                    FranVfx.DrawShared("Cal_SimpleStar", point, Color.White * (0.48f * opacity),
                        -angle, 0.045f * VisualScale);
                }
                break;
            case FranBurstStyle.Barrier:
                FranVfx.DrawRing(Projectile.Center, color, radius, 0.46f * opacity,
                    VisualRotation, "Cal_HighResHollowCircle");
                FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center,
                    new Color(105, 75, 255) * (0.42f * opacity), VisualRotation,
                    0.52f * VisualScale * (0.72f + eased * 0.36f));
                FranVfx.DrawShared("Crack", Projectile.Center, color * (0.34f * opacity),
                    VisualRotation, 0.18f * VisualScale * (0.8f + eased * 0.4f));
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                    Color.White * (0.68f * opacity), VisualRotation,
                    new Vector2(radius * 1.72f, radius * 1.18f));
                break;
            case FranBurstStyle.SwordArray:
                for (int i = 0; i < 4; i++)
                {
                    float angle = VisualRotation + i * MathHelper.PiOver2;
                    Vector2 point = Projectile.Center + angle.ToRotationVector2() * radius * 0.46f;
                    FranVfx.DrawShared("Cal_ForwardSmear", point,
                        new Color(145, 255, 120) * (0.42f * opacity), angle - MathHelper.PiOver2,
                        new Vector2(0.10f, 0.035f) * VisualScale);
                }
                FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center, Color.White * (0.68f * opacity),
                    -VisualRotation, new Vector2(0.09f, 0.025f) * VisualScale);
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                    new Color(210, 255, 195) * (0.72f * opacity), VisualRotation,
                    new Vector2(radius * 1.42f, radius * 0.78f));
                break;
            case FranBurstStyle.Flame:
                FranVfx.DrawShared("Cal_CircularSmear", Projectile.Center,
                    new Color(95, 255, 80) * (0.38f * opacity), VisualRotation + progress * 1.4f,
                    0.42f * VisualScale * (0.82f + eased * 0.4f));
                FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center,
                    color * (0.48f * opacity), -VisualRotation - progress * 1.8f,
                    0.48f * VisualScale * (0.72f + eased * 0.38f));
                FranVfx.DrawRing(Projectile.Center, new Color(185, 255, 95), radius * 0.76f, 0.42f * opacity,
                    -VisualRotation, "Cal_BloomRing");
                break;
            case FranBurstStyle.AirDraw:
                FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.74f * opacity),
                    VisualRotation, new Vector2(radius * 2.35f, radius * 1.22f));
                FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center, Color.White * (0.50f * opacity),
                    VisualRotation, new Vector2(radius * 2.05f, radius * 0.62f));
                break;
            case FranBurstStyle.Lightning:
                FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.68f * opacity),
                    VisualRotation, new Vector2(radius * 1.86f, radius * 1.34f));
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                    Color.White * (0.84f * opacity), VisualRotation, new Vector2(radius * 1.54f, radius * 0.86f));
                FranVfx.DrawRing(Projectile.Center, color, radius, 0.42f * opacity,
                    VisualRotation, "Cal_BloomRing");
                for (int i = 0; i < 6; i++)
                    FranVfx.DrawTexturedSpark(Projectile.Center,
                        (VisualRotation + i * MathHelper.TwoPi / 6f).ToRotationVector2(), color,
                        radius * 1.05f, 3.5f * VisualScale, opacity * 0.72f);
                break;
            case FranBurstStyle.ShipCutter:
                for (int i = 0; i < 3; i++)
                    FranVfx.DrawShared("Cal_SemiCircularSmearSwipe", Projectile.Center,
                        color * ((0.58f - i * 0.12f) * opacity), VisualRotation - i * 0.12f,
                        (0.82f - i * 0.10f) * VisualScale * (0.72f + eased * 0.42f));
                FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center, Color.White * (0.56f * opacity),
                    -VisualRotation, new Vector2(0.16f, 0.035f) * VisualScale);
                break;
            case FranBurstStyle.ThorsHammer:
                FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center, color * (0.76f * opacity),
                    VisualRotation, new Vector2(radius * 2.22f, radius * 1.46f));
                FranVfx.DrawOriginalPixels("FranFlashFourPoint", Projectile.Center,
                    Color.White * (0.86f * opacity), VisualRotation, new Vector2(radius * 1.78f, radius));
                FranVfx.DrawShared("Crack", Projectile.Center, color * (0.34f * opacity),
                    -VisualRotation, 0.30f * VisualScale);
                FranVfx.DrawRing(Projectile.Center, color, radius, 0.52f * opacity,
                    VisualRotation, "Cal_HighResHollowCircle");
                FranVfx.DrawShared("Cal_ShineFlare", Projectile.Center, Color.White * (0.78f * opacity),
                    VisualRotation, new Vector2(0.24f, 0.055f) * VisualScale);
                break;
            case FranBurstStyle.DashImpact:
                FranVfx.DrawOriginalPixels("FranImpactBurst", Projectile.Center,
                    new Color(135, 75, 190) * (0.72f * opacity), VisualRotation,
                    new Vector2(radius * 2.34f, radius * 1.12f));
                FranVfx.DrawOriginalPixels("FranSmearForward", Projectile.Center,
                    new Color(205, 165, 255) * (0.46f * opacity), VisualRotation,
                    new Vector2(radius * 2.05f, radius * 0.58f));
                FranVfx.DrawRing(Projectile.Center, new Color(105, 55, 155), radius,
                    0.46f * opacity, -VisualRotation, "Cal_BloomRing");
                FranVfx.DrawShared("Star", Projectile.Center, new Color(205, 165, 255) * (0.34f * opacity),
                    -VisualRotation, 0.13f * VisualScale);
                break;
            case FranBurstStyle.Potential:
                FranVfx.DrawRing(Projectile.Center, color, radius, 0.54f * opacity,
                    VisualRotation + progress, "MagicCircle");
                FranVfx.DrawRing(Projectile.Center, Color.White, radius * 0.62f, 0.32f * opacity,
                    -VisualRotation - progress * 1.4f, "RingStreak");
                break;
        }
        FranVfx.DrawBloom(Projectile.Center, color, radius * 0.72f, 0.28f * opacity,
            Style is FranBurstStyle.ThorsHammer or FranBurstStyle.ShipCutter);
        FranVfx.EndAdditive();
        return false;
    }

    private static Color StyleColor(FranBurstStyle style) => style switch
    {
        FranBurstStyle.SkillTaker => new Color(205, 100, 255),
        FranBurstStyle.Barrier => new Color(90, 185, 255),
        FranBurstStyle.Flame => new Color(255, 105, 55),
        FranBurstStyle.AirDraw => new Color(205, 245, 255),
        FranBurstStyle.Lightning => new Color(160, 115, 255),
        FranBurstStyle.ShipCutter => new Color(255, 215, 155),
        FranBurstStyle.ThorsHammer => new Color(105, 150, 255),
        FranBurstStyle.DashImpact => new Color(72, 34, 108),
        FranBurstStyle.Potential => new Color(145, 225, 255),
        _ => new Color(155, 225, 255)
    };
}

public sealed class FranDashTrail : ModProjectile
{
    private const int HistoryLength = 10;
    private const int FadeDuration = 11;
    private readonly Vector2[] centerHistory = new Vector2[HistoryLength];
    private int historyCount;
    private int visualAge;
    private int fadeAge;
    private bool initialized;
    private bool ending;
    private Vector2 dashOrigin;
    private Vector2 dashEnd;
    private Vector2 dashDirection;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = DashDurationWithFade;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
    }

    private int DashDurationWithFade => FranPlayer.DashDuration + FadeDuration + 2;

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }

        if (!initialized)
        {
            initialized = true;
            dashOrigin = Projectile.Center;
            dashEnd = owner.Center;
            dashDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
            Projectile.timeLeft = DashDurationWithFade;
            for (int i = 0; i < centerHistory.Length; i++)
                centerHistory[i] = dashOrigin;
            historyCount = 1;
            if (!Main.dedServ)
                FranParticleEffects.DashLaunch(dashOrigin, dashDirection * 16f);
        }

        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        if (!ending && fran.DashTimer > 0)
        {
            visualAge++;
            Vector2 motion = owner.velocity.LengthSquared() > 1f ? owner.velocity : Projectile.velocity;
            dashDirection = Vector2.Lerp(dashDirection, motion.SafeNormalize(dashDirection), 0.42f)
                .SafeNormalize(dashDirection);
            dashEnd = owner.Center;
            PushHistory(dashEnd);
            Projectile.Center = dashEnd;
            Lighting.AddLight(owner.Center, 0.14f, 0.06f, 0.23f);
            if (!Main.dedServ)
                FranParticleEffects.DashWake(owner.Center, motion, visualAge);
            return;
        }

        if (!ending)
        {
            ending = true;
            fadeAge = 0;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, FadeDuration + 1);
            if (!Main.dedServ)
                FranParticleEffects.DashBrake(dashEnd, dashDirection * 16f);
        }

        fadeAge++;
        Projectile.Center = dashEnd;
        Lighting.AddLight(dashEnd, 0.08f, 0.03f, 0.13f);
    }

    private void PushHistory(Vector2 center)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y))
            return;
        for (int i = centerHistory.Length - 1; i > 0; i--)
            centerHistory[i] = centerHistory[i - 1];
        centerHistory[0] = center;
        historyCount = Math.Min(historyCount + 1, centerHistory.Length);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (!initialized || historyCount < 1)
            return false;

        float fade = ending ? MathHelper.Clamp(1f - fadeAge / (float)FadeDuration, 0f, 1f) : 1f;
        float appear = MathHelper.Clamp(visualAge / 2f, 0f, 1f);
        float opacity = fade * appear;
        float rotation = dashDirection.ToRotation();
        float launchOpacity = ending ? 0f : MathHelper.Clamp(1f - visualAge / 9f, 0f, 1f);

        // AlphaBlend establishes a stable black body before any emission is added. This keeps the
        // Black Cat silhouette readable on bright backgrounds instead of turning transparent.
        for (int i = historyCount - 1; i >= 1; i--)
        {
            float ageFade = 1f - i / (float)(historyCount + 1);
            FranVfx.DrawVertical("TraceSoft", centerHistory[i], centerHistory[i - 1],
                new Color(5, 2, 10, 225) * (opacity * ageFade), 42f * ageFade + 8f);
        }
        for (int i = 1; i < historyCount; i += 2)
        {
            float ageFade = 1f - i / (float)(historyCount + 1);
            FranVfx.DrawShared("Smoke", centerHistory[i], new Color(18, 6, 29, 165) * (opacity * ageFade),
                rotation + i * 0.27f, 0.13f + ageFade * 0.10f);
        }
        if (launchOpacity > 0f)
            FranVfx.DrawShared("Cal_CircularSmearSmokey", dashOrigin - dashDirection * 8f,
                new Color(6, 2, 12, 230) * launchOpacity, rotation,
                new Vector2(0.58f, 0.30f) * (0.72f + visualAge * 0.045f));

        FranVfx.BeginAdditive();
        Vector2 normal = dashDirection.RotatedBy(MathHelper.PiOver2);
        for (int i = historyCount - 1; i >= 1; i--)
        {
            float ageFade = 1f - i / (float)(historyCount + 1);
            Vector2 start = centerHistory[i];
            Vector2 end = centerHistory[i - 1];
            Color body = Color.Lerp(new Color(68, 25, 108), new Color(187, 128, 231), ageFade);
            FranVfx.DrawVertical("TraceSoft", start, end, body * (opacity * ageFade * 0.62f),
                28f * ageFade + 3f);
            FranVfx.DrawVertical("TraceLong", start, end, new Color(218, 184, 255) * (opacity * ageFade * 0.54f),
                7f * ageFade + 1.2f);
            FranVfx.DrawVertical("Cal_ThinEndedLine", start, end, Color.White * (opacity * ageFade * 0.72f),
                2.3f * ageFade + 0.6f);

            if (i % 2 == 0)
            {
                float railOffset = 9f + ageFade * 8f;
                FranVfx.DrawVertical("Cal_ThinEndedLine", start + normal * railOffset,
                    end + normal * railOffset, new Color(133, 74, 195) * (opacity * ageFade * 0.34f),
                    2.2f + ageFade * 1.6f);
                FranVfx.DrawVertical("Cal_ThinEndedLine", start - normal * railOffset,
                    end - normal * railOffset, new Color(206, 157, 250) * (opacity * ageFade * 0.25f),
                    1.6f + ageFade * 1.2f);
            }
        }

        for (int i = 1; i < historyCount; i += 3)
        {
            float ageFade = 1f - i / (float)(historyCount + 1);
            FranVfx.DrawOriginalPixels("FranSmearForward", centerHistory[i],
                new Color(132, 70, 194) * (opacity * ageFade * 0.30f), rotation,
                new Vector2(76f + ageFade * 42f, 18f + ageFade * 12f));
        }

        Vector2 head = ending ? dashEnd : centerHistory[0];
        FranVfx.DrawBloom(head, new Color(112, 55, 171), 28f, opacity * 0.52f);
        FranVfx.DrawOriginalPixels("FranFlashFourPoint", head + dashDirection * 7f,
            Color.White * (opacity * 0.74f), rotation, new Vector2(74f, 21f));
        FranVfx.DrawOriginalPixels("FranPressureNeedle", head + dashDirection * 13f,
            new Color(222, 192, 255) * (opacity * 0.72f), rotation, new Vector2(82f, 11f));

        if (launchOpacity > 0f)
        {
            float launchProgress = MathHelper.Clamp(visualAge / 9f, 0f, 1f);
            FranVfx.DrawRing(dashOrigin, new Color(117, 55, 178), 22f + launchProgress * 45f,
                launchOpacity * 0.62f, rotation, "Cal_BloomRing");
            FranVfx.DrawOriginalPixels("FranImpactBurst", dashOrigin - dashDirection * 5f,
                new Color(194, 140, 238) * (launchOpacity * 0.54f), rotation,
                new Vector2(116f + launchProgress * 42f, 58f));
            for (int side = -1; side <= 1; side += 2)
                FranVfx.DrawOriginalPixels("FranSmearForward", dashOrigin - dashDirection * 9f + normal * side * 13f,
                    new Color(218, 178, 255) * (launchOpacity * 0.38f),
                    rotation + side * 0.23f, new Vector2(106f, 18f));
        }

        if (ending)
        {
            float brakeProgress = MathHelper.Clamp(fadeAge / (float)FadeDuration, 0f, 1f);
            float brakeOpacity = MathF.Sin(brakeProgress * MathHelper.Pi);
            FranVfx.DrawRing(dashEnd, new Color(106, 47, 165), 18f + brakeProgress * 54f,
                brakeOpacity * 0.66f, -rotation, "Cal_HighResHollowCircle");
            FranVfx.DrawOriginalPixels("FranImpactBurst", dashEnd, new Color(169, 105, 220) * (brakeOpacity * 0.52f),
                rotation, new Vector2(104f + brakeProgress * 54f, 52f));
            FranVfx.DrawOriginalPixels("FranFlashFourPoint", dashEnd, Color.White * (brakeOpacity * 0.78f),
                rotation, new Vector2(92f, 28f));
        }
        FranVfx.EndAdditive();
        return false;
    }
}
