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

    // Three-layer beam language: a broad colored halo, a tighter body and an overexposed white core.
    // All three layers use real greyscale beam textures; none of the beam is assembled from a single-pixel strip.
    public static void DrawCappedLaser(Vector2 start, Vector2 end, Color haloColor, Color bodyColor,
        float width, float opacity)
    {
        if (!TryGetSafeSegment(start, end, out Vector2 edge, out _) ||
            !float.IsFinite(width) || !float.IsFinite(opacity) || width <= 0f || opacity <= 0f)
            return;

        width = MathHelper.Clamp(width, 0.5f, 256f);
        opacity = MathHelper.Clamp(opacity, 0f, 2f);

        DrawVertical("FranVfx_AresLaserMiddle", start, end, haloColor * (0.34f * opacity), width * 2.8f);
        DrawVertical("FranVfx_AresLaserMiddle", start, end, bodyColor * (0.78f * opacity), width * 1.12f);
        DrawVertical("FranVfx_AresLaserMiddle", start, end, Color.White * (0.92f * opacity), width * 0.34f);

        float capScale = width * 2.55f / Math.Max(1f, Shared("FranVfx_AresLaserEnd").Width);
        float rotation = edge.ToRotation() - MathHelper.PiOver2;
        DrawShared("FranVfx_AresLaserEnd", start, haloColor * (0.42f * opacity), rotation + MathHelper.Pi,
            new Vector2(capScale, capScale * 0.82f));
        DrawShared("FranVfx_AresLaserEnd", end, bodyColor * (0.72f * opacity), rotation,
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
            DrawVertical("FranVfx_ThinEndedLine", previous, next, color * (0.36f * opacity), width * 3.2f);
            DrawVertical("FranVfx_ThinEndedLine", previous, next, color * (0.86f * opacity), width * 1.22f);
            DrawVertical("FranVfx_ThinEndedLine", previous, next, Color.White * (0.92f * opacity), width * 0.34f);
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
        DrawVertical("FranVfx_ThinEndedLine", start, end, color * (0.42f * opacity), width * 2.6f);
        DrawVertical("FranVfx_ThinEndedLine", start, end, Color.White * (0.82f * opacity), width * 0.58f);
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
