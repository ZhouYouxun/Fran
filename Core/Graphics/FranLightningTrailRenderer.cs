using System;
using System.Collections.Generic;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Core.Graphics;

/// <summary>
/// Continuous multi-pass lightning ribbons. Strong lightning avoids assembling a long
/// bolt from individually tapered sprites: one jagged point list is rendered as a primitive ribbon,
/// then repeated as a dark body, broad emission, colored conductor and overexposed white core.
/// </summary>
internal static class FranLightningTrailRenderer
{
    private const float MaximumLength = 1400f;
    private static bool rendererFaulted;

    public static bool TryDrawBolt(Vector2 start, Vector2 end, Color bodyColor, Color energyColor,
        float width, int segments, float seed, float opacity = 1f, float phase = 0f)
    {
        if (rendererFaulted || Main.dedServ || !IsFinite(start) || !IsFinite(end) ||
            !float.IsFinite(width) || !float.IsFinite(seed) || !float.IsFinite(opacity))
            return false;

        float lengthSquared = Vector2.DistanceSquared(start, end);
        if (!float.IsFinite(lengthSquared) || lengthSquared < 4f ||
            lengthSquared > MaximumLength * MaximumLength)
            return false;

        segments = Math.Clamp(segments, 4, 48);
        width = MathHelper.Clamp(MathF.Abs(width), 0.8f, 72f);
        opacity = MathHelper.Clamp(opacity, 0f, 1.5f);
        Vector2 edge = end - start;
        Vector2 normal = edge.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
        float length = MathF.Sqrt(lengthSquared);
        // A lightning bolt should read as one decisive fracture, not as a rope that writhes every frame.
        // Its geometry is therefore derived only from the stable seed. Callers may still animate width,
        // opacity and reveal timing without changing the route of the strike.
        float jitterScale = MathHelper.Clamp(length / segments * 0.23f + width * 0.18f, 2.5f, 13f);
        List<Vector2> points = new(segments + 1) { start };
        for (int i = 1; i < segments; i++)
        {
            float completion = i / (float)segments;
            float envelope = MathF.Sin(completion * MathHelper.Pi);
            float primary = MathF.Sin(seed * 1.73f + i * 12.9898f);
            float secondary = MathF.Sin(seed * 0.61f + i * 7.318f) * 0.46f;
            float fine = MathF.Sin(seed * 2.11f + i * 19.17f) * 0.18f;
            points.Add(Vector2.Lerp(start, end, completion) + normal *
                (primary + secondary + fine) * envelope * jitterScale);
        }
        points.Add(end);
        return TryDrawPath(points, bodyColor, energyColor, width, opacity);
    }

    public static bool TryDrawPath(IReadOnlyList<Vector2> sourcePoints, Color bodyColor,
        Color energyColor, float width, float opacity)
    {
        if (rendererFaulted || Main.dedServ || sourcePoints is null || sourcePoints.Count < 2 ||
            !float.IsFinite(width) || !float.IsFinite(opacity))
            return false;

        List<Vector2> points = new(sourcePoints.Count);
        Vector2 previous = default;
        for (int i = 0; i < sourcePoints.Count; i++)
        {
            Vector2 point = sourcePoints[i];
            if (!IsFinite(point))
                break;
            if (points.Count > 0)
            {
                float segmentLengthSquared = Vector2.DistanceSquared(previous, point);
                if (!float.IsFinite(segmentLengthSquared) || segmentLengthSquared > 180f * 180f)
                    break;
                if (segmentLengthSquared < 0.25f)
                    continue;
            }
            points.Add(point);
            previous = point;
        }
        if (points.Count < 2)
            return false;

        width = MathHelper.Clamp(MathF.Abs(width), 0.8f, 72f);
        opacity = MathHelper.Clamp(opacity, 0f, 1.5f);
        GraphicsDevice device = Main.instance.GraphicsDevice;
        BlendState previousBlend = device.BlendState;
        DepthStencilState previousDepth = device.DepthStencilState;
        RasterizerState previousRasterizer = device.RasterizerState;
        SamplerState previousSampler = device.SamplerStates[0];
        try
        {
            // The wide AlphaBlend pass makes the bolt readable over snow, sky, bloom and white bosses.
            DrawPass(points, width * 2.10f, bodyColor, opacity * 0.94f, BlendState.AlphaBlend);
            DrawPass(points, width * 5.20f, energyColor, opacity * 0.22f, BlendState.Additive);
            DrawPass(points, width * 2.35f, energyColor, opacity * 0.48f, BlendState.Additive);
            DrawPass(points, width * 1.08f, Color.Lerp(energyColor, Color.White, 0.34f),
                opacity * 0.92f, BlendState.Additive);
            DrawPass(points, Math.Max(1.2f, width * 0.34f), Color.White,
                opacity * 1.18f, BlendState.Additive);
            return true;
        }
        catch
        {
            rendererFaulted = true;
            return false;
        }
        finally
        {
            device.BlendState = previousBlend;
            device.DepthStencilState = previousDepth;
            device.RasterizerState = previousRasterizer;
            device.SamplerStates[0] = previousSampler;
        }
    }

    public static void Unload() => rendererFaulted = false;

    private static void DrawPass(List<Vector2> points, float width, Color color, float opacity,
        BlendState blendState)
    {
        Main.instance.GraphicsDevice.BlendState = blendState;
        PrimitiveSettings settings = new(
            completion => WidthEnvelope(width, completion),
            completion => color * (opacity * OpacityEnvelope(completion)),
            Smoothen: false,
            Pixelate: false);
        PrimitiveRenderer.RenderTrail(points, settings, Math.Max(points.Count, 12));
    }

    private static float WidthEnvelope(float width, float completion)
    {
        float tip = MathHelper.Lerp(0.44f, 1f,
            FranMod.Content.Projectiles.FranMotion.Smooth(
                FranMod.Content.Projectiles.FranMotion.Segment(completion, 0f, 0.10f)));
        float end = MathHelper.Lerp(1f, 0.50f,
            FranMod.Content.Projectiles.FranMotion.Smooth(
                FranMod.Content.Projectiles.FranMotion.Segment(completion, 0.88f, 1f)));
        return Math.Max(0.8f, width * tip * end);
    }

    private static float OpacityEnvelope(float completion) =>
        0.72f + MathF.Sin(completion * MathHelper.Pi) * 0.28f;

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
