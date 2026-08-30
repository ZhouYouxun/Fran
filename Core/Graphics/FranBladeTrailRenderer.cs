using System;
using FranMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Core.Graphics;

// Teacher's blade light is made entirely from short, locally validated sprite segments.
// No history buffer or user primitive reaches the GPU, so a bad point cannot become a
// screen-spanning triangle. Animated radial waves provide the flowing Exoblade-like material.
internal static class FranBladeTrailRenderer
{
    private const float MaximumWorldRadius = 284f;
    private const float MaximumSegmentLength = 44f;

    public static bool TryDrawSlash(Vector2 pivot, float bladeAngle, float bladeReach, float swingProgress,
        Color energyColor, float visualStrength, int style, int swingDirection)
    {
        if (Main.dedServ || !IsFinite(pivot) || !float.IsFinite(bladeAngle) ||
            !float.IsFinite(bladeReach) || !float.IsFinite(swingProgress) || !float.IsFinite(visualStrength))
            return false;

        style = Math.Clamp(style, 0, 3);
        swingDirection = swingDirection < 0 ? -1 : 1;
        bladeReach = MathHelper.Clamp(bladeReach, 24f, 212f);
        visualStrength = MathHelper.Clamp(visualStrength, 0f, 1f);
        swingProgress = MathHelper.Clamp(swingProgress, 0f, 1f);
        if (visualStrength <= 0.018f)
            return false;

        if (style == 3)
            DrawPierce(pivot, bladeAngle, bladeReach, swingProgress, energyColor, visualStrength);
        else
            DrawArc(pivot, bladeAngle, bladeReach, swingProgress, energyColor, visualStrength, style,
                swingDirection);
        return true;
    }

    public static void DrawSlashFallback(Vector2 pivot, float bladeAngle, float bladeReach,
        Color energyColor, float visualStrength, int style, int swingDirection)
    {
        if (!IsFinite(pivot) || !float.IsFinite(bladeAngle) || !float.IsFinite(bladeReach) ||
            !float.IsFinite(visualStrength))
            return;

        style = Math.Clamp(style, 0, 3);
        swingDirection = swingDirection < 0 ? -1 : 1;
        bladeReach = MathHelper.Clamp(bladeReach, 24f, 212f);
        visualStrength = MathHelper.Clamp(visualStrength, 0f, 1f);
        int samples = style == 3 ? 15 : 24;
        float span = ArcSpan(style) * (0.70f + visualStrength * 0.30f);
        for (int i = samples - 1; i >= 0; i--)
        {
            float completion = i / (float)Math.Max(1, samples - 1);
            Vector2 position;
            if (style == 3)
            {
                Vector2 blade = bladeAngle.ToRotationVector2();
                position = pivot + blade * MathHelper.Lerp(22f, bladeReach, 1f - completion);
            }
            else
            {
                float angle = bladeAngle - swingDirection * span * MathF.Pow(completion, 0.88f);
                position = pivot + angle.ToRotationVector2() * bladeReach * 0.78f;
            }

            if (!SafeWorldPoint(position, pivot))
                continue;
            float fade = MathF.Pow(1f - completion, 0.62f) * visualStrength;
            DrawFallbackPoint(position, Color.Lerp(energyColor, Color.White, 0.34f) * (0.82f * fade),
                MathHelper.Clamp(2.2f + fade * 3.8f, 2f, 8f));
        }
    }

    public static void Unload() { }

    private static void DrawArc(Vector2 pivot, float bladeAngle, float bladeReach, float swingProgress,
        Color energyColor, float visualStrength, int style, int swingDirection)
    {
        int samples = style switch { 1 => 48, 2 => 44, _ => 40 };
        float span = ArcSpan(style) * (0.70f + visualStrength * 0.30f);
        float radius = bladeReach * (style == 1 ? 0.73f : style == 2 ? 0.76f : 0.78f);
        float baseHalfWidth = style switch { 1 => 31f, 2 => 43f, _ => 29f };
        float time = Main.GlobalTimeWrappedHourly;
        Color darkBody = Color.Lerp(style == 2 ? new Color(17, 12, 31) : new Color(10, 22, 35),
            energyColor, style == 2 ? 0.24f : 0.30f);
        Color edgeColor = Color.Lerp(energyColor, Color.White, style == 2 ? 0.28f : 0.40f);

        Vector2 previousCenter = default;
        float previousHalfWidth = 0f;
        float previousCompletion = 0f;
        bool hasPrevious = false;

        // Alpha material pass. Short overlapping strokes form one dense curved blade body.
        for (int i = 0; i < samples; i++)
        {
            float completion = i / (float)(samples - 1);
            ArcSample(pivot, bladeAngle, radius, span, swingProgress, style, swingDirection,
                baseHalfWidth, completion, out Vector2 center, out Vector2 outer, out float halfWidth);
            if (!SafeWorldPoint(center, pivot) || !SafeWorldPoint(outer, pivot))
            {
                hasPrevious = false;
                continue;
            }

            if (hasPrevious)
            {
                float fade = MathF.Pow(1f - (previousCompletion + completion) * 0.5f, 0.54f) * visualStrength;
                float width = MathHelper.Clamp((previousHalfWidth + halfWidth) * 1.12f, 5f, 92f);
                DrawSafeSegment("BloomLineFade", previousCenter, center, pivot,
                    darkBody * (0.90f * fade), width);
                DrawSafeSegment("BloomLineSoftEdge", previousCenter, center, pivot,
                    energyColor * (0.13f * fade), width * 0.72f);
            }

            previousCenter = center;
            previousHalfWidth = halfWidth;
            previousCompletion = completion;
            hasPrevious = true;
        }

        FranVfx.BeginAdditive();
        try
        {
            DrawArcTrack(pivot, bladeAngle, radius, span, swingProgress, style, swingDirection,
                baseHalfWidth, samples, energyColor, visualStrength, 0f, 0f, 0f,
                "BloomLineSoftEdge", baseHalfWidth * 2.05f + 22f, 0.19f);
            DrawOuterEdge(pivot, bladeAngle, radius, span, swingProgress, style, swingDirection,
                baseHalfWidth, samples, edgeColor, visualStrength);

            // Three phase-shifted filaments travel through the blade surface. Unlike the old radial
            // ribs, these remain curved and continuously change radial position over time.
            DrawArcTrack(pivot, bladeAngle, radius, span, swingProgress, style, swingDirection,
                baseHalfWidth, samples, Color.Lerp(energyColor, Color.White, 0.18f), visualStrength,
                -0.43f, 16.5f + style * 2.2f, -3.1f, "ThinEndedLine", style == 2 ? 8.0f : 6.2f, 0.48f);
            DrawArcTrack(pivot, bladeAngle, radius, span, swingProgress, style, swingDirection,
                baseHalfWidth, samples, Color.White, visualStrength,
                0.02f, 25f + style * 3.1f, 4.6f, "ThinEndedLine", style == 2 ? 5.2f : 4.1f, 0.62f);
            DrawArcTrack(pivot, bladeAngle, radius, span, swingProgress, style, swingDirection,
                baseHalfWidth, samples, edgeColor, visualStrength,
                0.46f, 37f + style * 4.4f, -6.3f, "ThinEndedLine", style == 2 ? 4.4f : 3.5f, 0.50f);

            for (int i = 3; i < samples - 2; i += style == 2 ? 3 : 4)
            {
                float completion = i / (float)(samples - 1);
                ArcSample(pivot, bladeAngle, radius, span, swingProgress, style, swingDirection,
                    baseHalfWidth, completion, out Vector2 center, out _, out float halfWidth);
                float wave = MathF.Sin(completion * (31f + style * 3f) - time * 5.2f + style);
                Vector2 radial = (center - pivot).SafeNormalize(Vector2.UnitX);
                Vector2 point = center + radial * halfWidth * wave * 0.54f;
                if (!SafeWorldPoint(point, pivot))
                    continue;
                float fade = MathF.Pow(1f - completion, 0.62f) * visualStrength;
                float tangent = (center - pivot).ToRotation() + MathHelper.PiOver2 * swingDirection;
                FranVfx.DrawSharedPixels(i % 2 == 0 ? "NoiseSharp" : "NoiseSoft", point,
                    Color.Lerp(energyColor, Color.White, i % 3 * 0.18f) * (0.28f * fade), tangent,
                    new Vector2(style == 2 ? 42f : 34f, style == 2 ? 15f : 11f));
            }
        }
        finally
        {
            FranVfx.EndAdditive();
        }
    }

    private static void DrawPierce(Vector2 pivot, float bladeAngle, float bladeReach, float swingProgress,
        Color energyColor, float visualStrength)
    {
        const int samples = 22;
        Vector2 blade = bladeAngle.ToRotationVector2();
        Vector2 normal = blade.RotatedBy(MathHelper.PiOver2);
        float startDistance = Math.Min(28f, bladeReach * 0.32f);
        float usableLength = Math.Max(8f, bladeReach - startDistance);
        float time = Main.GlobalTimeWrappedHourly;
        Color darkBody = Color.Lerp(new Color(9, 24, 37), energyColor, 0.30f);
        Vector2 previousCenter = default;
        float previousWidth = 0f;

        for (int i = 0; i < samples; i++)
        {
            float completion = i / (float)(samples - 1);
            float envelope = MathF.Sin(MathF.Pow(completion, 0.78f) * MathHelper.Pi) * 0.72f + 0.18f;
            float halfWidth = MathHelper.Clamp(20f * envelope * visualStrength + 2f, 2f, 22f);
            Vector2 center = pivot + blade * (startDistance + usableLength * completion) +
                normal * MathF.Sin(completion * 34f - time * 5.6f) * 1.8f;
            if (i > 0)
                DrawSafeSegment("BloomLineFade", previousCenter, center, pivot,
                    darkBody * (0.90f * visualStrength), (previousWidth + halfWidth) * 1.16f);
            previousCenter = center;
            previousWidth = halfWidth;
        }

        FranVfx.BeginAdditive();
        try
        {
            for (int track = -1; track <= 1; track++)
            {
                Vector2 previous = default;
                for (int i = 0; i < samples; i++)
                {
                    float completion = i / (float)(samples - 1);
                    float envelope = MathF.Sin(completion * MathHelper.Pi) * 0.72f + 0.18f;
                    float halfWidth = MathHelper.Clamp(20f * envelope * visualStrength + 2f, 2f, 22f);
                    float wave = MathF.Sin(completion * (32f + track * 5f) - time * (4.8f + track * 0.7f));
                    Vector2 point = pivot + blade * (startDistance + usableLength * completion) +
                        normal * (halfWidth * track * 0.52f + wave * 2.8f);
                    if (i > 0)
                        DrawSafeSegment(track == 0 ? "ThinEndedLine" : "BloomLineSoftEdge", previous, point,
                            pivot, (track == 0 ? Color.White : energyColor) *
                            (visualStrength * (track == 0 ? 0.72f : 0.42f)), track == 0 ? 4.2f : 8.5f);
                    previous = point;
                }
            }
        }
        finally
        {
            FranVfx.EndAdditive();
        }
    }

    private static void DrawOuterEdge(Vector2 pivot, float bladeAngle, float radius, float span,
        float swingProgress, int style, int swingDirection, float baseHalfWidth, int samples,
        Color color, float visualStrength)
    {
        Vector2 previous = default;
        for (int i = 0; i < samples; i++)
        {
            float completion = i / (float)(samples - 1);
            ArcSample(pivot, bladeAngle, radius, span, swingProgress, style, swingDirection,
                baseHalfWidth, completion, out _, out Vector2 point, out _);
            if (i > 0)
            {
                float fade = MathF.Pow(1f - completion, 0.52f) * visualStrength;
                DrawSafeSegment("BloomLineSoftEdge", previous, point, pivot, color * (0.64f * fade),
                    style == 2 ? 10.5f : 8.2f);
                DrawSafeSegment("ThinEndedLine", previous, point, pivot, Color.White * (0.78f * fade),
                    style == 2 ? 3.2f : 2.5f);
            }
            previous = point;
        }
    }

    private static void DrawArcTrack(Vector2 pivot, float bladeAngle, float radius, float span,
        float swingProgress, int style, int swingDirection, float baseHalfWidth, int samples,
        Color color, float visualStrength, float radialTrack, float waveFrequency, float waveSpeed,
        string textureName, float width, float opacity)
    {
        float time = Main.GlobalTimeWrappedHourly;
        Vector2 previous = default;
        for (int i = 0; i < samples; i++)
        {
            float completion = i / (float)(samples - 1);
            ArcSample(pivot, bladeAngle, radius, span, swingProgress, style, swingDirection,
                baseHalfWidth, completion, out Vector2 center, out _, out float halfWidth);
            float wave = waveFrequency == 0f ? 0f : MathF.Sin(completion * waveFrequency + time * waveSpeed + style);
            Vector2 radial = (center - pivot).SafeNormalize(Vector2.UnitX);
            Vector2 point = center + radial * halfWidth * radialTrack +
                radial * wave * (style == 2 ? 4.4f : 3.1f);
            if (i > 0)
            {
                float fade = MathF.Pow(1f - completion, style == 2 ? 0.46f : 0.58f) * visualStrength;
                float pulse = waveFrequency == 0f ? 1f : 0.72f + MathF.Abs(wave) * 0.28f;
                DrawSafeSegment(textureName, previous, point, pivot, color * (opacity * fade * pulse), width);
            }
            previous = point;
        }
    }

    private static void ArcSample(Vector2 pivot, float bladeAngle, float radius, float span,
        float swingProgress, int style, int swingDirection, float baseHalfWidth, float completion,
        out Vector2 center, out Vector2 outer, out float halfWidth)
    {
        float curved = MathF.Pow(completion, style == 1 ? 0.82f : 0.88f);
        float angle = bladeAngle - swingDirection * span * curved;
        float organicWave = MathF.Sin(completion * MathHelper.Pi) *
            MathF.Sin(completion * 19f + swingProgress * 7f + style * 1.9f +
                Main.GlobalTimeWrappedHourly * 2.2f);
        float centerRadius = radius + organicWave * (style == 2 ? 4.2f : 2.8f);
        float envelope = MathF.Pow(1f - completion, style == 2 ? 0.44f : 0.54f) *
            (0.82f + MathF.Sin(completion * MathHelper.Pi) * 0.22f);
        halfWidth = MathHelper.Clamp(baseHalfWidth * envelope, 2.2f, baseHalfWidth * 1.06f);
        Vector2 radial = angle.ToRotationVector2();
        center = pivot + radial * centerRadius;
        outer = pivot + radial * (centerRadius + halfWidth);
    }

    private static void DrawSafeSegment(string textureName, Vector2 start, Vector2 end, Vector2 pivot,
        Color color, float width)
    {
        if (!SafeWorldPoint(start, pivot) || !SafeWorldPoint(end, pivot) || !float.IsFinite(width))
            return;
        float lengthSquared = Vector2.DistanceSquared(start, end);
        if (!float.IsFinite(lengthSquared) || lengthSquared <= 0.01f ||
            lengthSquared > MaximumSegmentLength * MaximumSegmentLength)
            return;
        // The blade surface uses bounded local noise sprites, not a stretched pixel or beam
        // texture. Overlap hides the individual samples while the phase-shifted tracks above
        // make the material ripple along the swing like Exoblade's animated slash surface.
        Vector2 edge = end - start;
        float length = MathF.Sqrt(lengthSquared);
        float safeWidth = MathHelper.Clamp(MathF.Abs(width), 0.7f, 116f);
        string localTexture = textureName == "ThinEndedLine" ? "NoiseSharp" : "NoiseSoft";
        FranVfx.DrawSharedPixels(localTexture, (start + end) * 0.5f, color, edge.ToRotation(),
            new Vector2(Math.Min(MaximumSegmentLength + 8f, length + 8f), safeWidth));
    }

    private static float ArcSpan(int style) => style switch
    {
        1 => 2.72f,
        2 => 2.02f,
        3 => 0f,
        _ => 1.78f
    };

    private static bool SafeWorldPoint(Vector2 point, Vector2 pivot)
    {
        if (!IsFinite(point) || !IsFinite(pivot))
            return false;
        float distanceSquared = Vector2.DistanceSquared(point, pivot);
        return float.IsFinite(distanceSquared) && distanceSquared <= MaximumWorldRadius * MaximumWorldRadius;
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static void DrawFallbackPoint(Vector2 position, Color color, float radius)
    {
        if (!IsFinite(position) || !float.IsFinite(radius))
            return;
        float safeRadius = MathHelper.Clamp(radius, 1f, 8f);
        FranVfx.DrawSharedPixels("BloomCircle", position, color, 0f, Vector2.One * safeRadius * 2f);
    }
}
