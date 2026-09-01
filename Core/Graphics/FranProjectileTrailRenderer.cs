using System;
using System.Collections.Generic;
using FranMod.Content.Projectiles;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Core.Graphics;

internal enum FranProjectileTrailProfile
{
    TelekineticNeedle,
    ArcaneWisp,
    SpectralBlade,
    HeavyHammer
}

// Texture-driven projectile ribbons. Geometry is shared, but every projectile family owns its
// width envelope, two material maps, flow direction and a distinct shader branch. This keeps the
// trails related without reducing them to the same ribbon with a different color.
internal static class FranProjectileTrailRenderer
{
    private const float MaximumTrailRadius = 760f;
    private const float MaximumSegmentLength = 58f;
    private static bool shaderTrailFaulted;

    public static bool TryDraw(Projectile projectile, FranProjectileTrailProfile profile,
        Color bodyColor, Color edgeColor, Color coreColor, float halfWidth, float opacity)
    {
        if (Main.dedServ || shaderTrailFaulted || projectile.oldPos is null || projectile.oldPos.Length < 2 ||
            !IsFinite(projectile.Center) || !float.IsFinite(halfWidth) || !float.IsFinite(opacity))
            return false;

        List<Vector2> points = CollectSafePoints(projectile);
        if (points.Count < 3 || !ShaderManager.TryGetShader("Fran.FranBladeTrailShader", out ManagedShader shader))
            return false;

        halfWidth = MathHelper.Clamp(MathF.Abs(halfWidth), 3f, 68f);
        opacity = MathHelper.Clamp(opacity, 0f, 1f);
        if (opacity <= 0.01f)
            return false;

        GraphicsDevice device = Main.instance.GraphicsDevice;
        BlendState previousBlend = device.BlendState;
        DepthStencilState previousDepth = device.DepthStencilState;
        RasterizerState previousRasterizer = device.RasterizerState;
        SamplerState previousSampler0 = device.SamplerStates[0];
        SamplerState previousSampler1 = device.SamplerStates[1];
        SamplerState previousSampler2 = device.SamplerStates[2];
        Texture previousTexture1 = device.Textures[1];
        Texture previousTexture2 = device.Textures[2];
        try
        {
            shader.TrySetParameter("uBodyColor", bodyColor);
            shader.TrySetParameter("uEdgeColor", edgeColor);
            shader.TrySetParameter("uCoreColor", coreColor);
            shader.TrySetParameter("uOpacity", opacity);
            shader.TrySetParameter("uProfile", (float)profile);
            shader.TrySetParameter("uFlowSpeed", FlowSpeed(profile));
            shader.TrySetParameter("uNoiseStrength", NoiseStrength(profile));
            float seed = (projectile.identity * 0.61803395f + projectile.type * 0.131f) % 1f;
            shader.TrySetParameter("uSeed", seed);
            shader.SetTexture(MaterialTexture(profile), 1, SamplerState.LinearWrap);
            shader.SetTexture(DetailTexture(profile), 2, SamplerState.LinearWrap);

            DrawPass(points, shader, profile, halfWidth * AuraScale(profile), opacity * AuraOpacity(profile),
                1f, BlendState.Additive);
            DrawPass(points, shader, profile, halfWidth, opacity * BodyOpacity(profile),
                0f, BlendState.AlphaBlend);
            DrawPass(points, shader, profile, Math.Max(2.5f, halfWidth * CoreScale(profile)),
                opacity * CoreOpacity(profile), 2f, BlendState.Additive);
            return true;
        }
        catch
        {
            shaderTrailFaulted = true;
            return false;
        }
        finally
        {
            device.BlendState = previousBlend;
            device.DepthStencilState = previousDepth;
            device.RasterizerState = previousRasterizer;
            device.SamplerStates[0] = previousSampler0;
            device.SamplerStates[1] = previousSampler1;
            device.SamplerStates[2] = previousSampler2;
            device.Textures[1] = previousTexture1;
            device.Textures[2] = previousTexture2;
        }
    }

    public static void DrawFallback(Projectile projectile, FranProjectileTrailProfile profile,
        Color color, float width, float opacity)
    {
        List<Vector2> points = CollectSafePoints(projectile);
        if (points.Count < 2)
            return;

        FranVfx.BeginAdditive();
        try
        {
            for (int i = points.Count - 1; i >= 1; i--)
            {
                float completion = i / (float)(points.Count - 1);
                float fade = MathF.Pow(1f - completion, 0.58f) * opacity;
                float segmentWidth = WidthEnvelope(profile, width, completion);
                FranVfx.DrawVertical("Cal_BloomLineSoftEdge", points[i], points[i - 1],
                    color * (fade * 0.32f), MathHelper.Clamp(segmentWidth * 1.9f, 2f, 86f));
                FranVfx.DrawVertical("Cal_ThinEndedLine", points[i], points[i - 1],
                    Color.Lerp(color, Color.White, 0.64f) * (fade * 0.68f),
                    MathHelper.Clamp(segmentWidth * 0.24f, 1.2f, 8f));
            }
        }
        finally
        {
            FranVfx.EndAdditive();
        }
    }

    public static void Unload() => shaderTrailFaulted = false;

    private static void DrawPass(List<Vector2> points, ManagedShader shader,
        FranProjectileTrailProfile profile, float width, float opacity, float passMode, BlendState blendState)
    {
        Main.instance.GraphicsDevice.BlendState = blendState;
        shader.TrySetParameter("uPassMode", passMode);
        PrimitiveSettings settings = new(
            completion => WidthEnvelope(profile, width, completion),
            completion => Color.White * (MathF.Pow(1f - completion, 0.30f) * opacity),
            Smoothen: false,
            Pixelate: false,
            Shader: shader);
        PrimitiveRenderer.RenderTrail(points, settings, points.Count);
    }

    private static float WidthEnvelope(FranProjectileTrailProfile profile, float width, float completion)
    {
        completion = MathHelper.Clamp(completion, 0f, 1f);
        float tail = profile switch
        {
            FranProjectileTrailProfile.TelekineticNeedle => MathF.Pow(1f - completion, 0.82f),
            FranProjectileTrailProfile.ArcaneWisp => MathF.Pow(1f - completion, 0.46f),
            FranProjectileTrailProfile.SpectralBlade => MathF.Pow(1f - completion, 0.68f),
            _ => MathF.Pow(1f - completion, 0.34f)
        };
        float headShape = profile switch
        {
            // A needle starts at a sharp point and opens into a pressure wake.
            FranProjectileTrailProfile.TelekineticNeedle => MathHelper.Lerp(0.10f, 1f,
                FranMotion.Smooth(FranMotion.Segment(completion, 0f, 0.18f))),
            // Wisps breathe through the middle rather than preserving a rigid beam silhouette.
            FranProjectileTrailProfile.ArcaneWisp => 0.70f + MathF.Sin(completion * MathHelper.Pi) * 0.38f,
            // Spectral blades read as a hard wedge with a broad cutting shoulder.
            FranProjectileTrailProfile.SpectralBlade => MathHelper.Lerp(0.22f, 1f,
                FranMotion.Smooth(FranMotion.Segment(completion, 0f, 0.22f))) *
                (0.90f + MathF.Sin(completion * MathHelper.Pi) * 0.18f),
            // Hammer wakes retain mass and break into repeating compression knots.
            _ => 0.92f + MathF.Sin(completion * MathHelper.Pi * 4f) * 0.10f
        };
        return MathHelper.Clamp(width * tail * headShape, 1.2f, 96f);
    }

    private static Texture2D MaterialTexture(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => FranVfx.Original("FranTrailFilamentNoise"),
        FranProjectileTrailProfile.ArcaneWisp => FranVfx.Original("FranTrailWispNoise"),
        FranProjectileTrailProfile.SpectralBlade => FranVfx.Original("FranTrailFractureNoise"),
        _ => FranVfx.Original("FranTrailFractureNoise")
    };

    private static Texture2D DetailTexture(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => FranVfx.Original("FranTrailFractureNoise"),
        FranProjectileTrailProfile.ArcaneWisp => FranVfx.Original("FranTrailFilamentNoise"),
        FranProjectileTrailProfile.SpectralBlade => FranVfx.Original("FranTrailFilamentNoise"),
        _ => FranVfx.Original("FranTrailWispNoise")
    };

    private static float AuraScale(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 1.62f,
        FranProjectileTrailProfile.ArcaneWisp => 1.88f,
        FranProjectileTrailProfile.SpectralBlade => 1.54f,
        _ => 1.78f
    };

    private static float CoreScale(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 0.24f,
        FranProjectileTrailProfile.ArcaneWisp => 0.30f,
        FranProjectileTrailProfile.SpectralBlade => 0.22f,
        _ => 0.18f
    };

    private static float FlowSpeed(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 1.8f,
        FranProjectileTrailProfile.ArcaneWisp => -0.82f,
        FranProjectileTrailProfile.SpectralBlade => 1.15f,
        _ => 2.25f
    };

    private static float NoiseStrength(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 0.52f,
        FranProjectileTrailProfile.ArcaneWisp => 0.86f,
        FranProjectileTrailProfile.SpectralBlade => 0.68f,
        _ => 0.74f
    };

    private static float AuraOpacity(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 0.58f,
        FranProjectileTrailProfile.ArcaneWisp => 0.78f,
        FranProjectileTrailProfile.SpectralBlade => 0.62f,
        _ => 0.72f
    };

    private static float BodyOpacity(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.ArcaneWisp => 0.88f,
        _ => 1f
    };

    private static float CoreOpacity(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 1f,
        FranProjectileTrailProfile.ArcaneWisp => 0.74f,
        FranProjectileTrailProfile.SpectralBlade => 0.94f,
        _ => 0.88f
    };

    private static List<Vector2> CollectSafePoints(Projectile projectile)
    {
        List<Vector2> points = new(projectile.oldPos?.Length + 1 ?? 1);
        Vector2 head = projectile.Center;
        if (!IsFinite(head))
            return points;
        points.Add(head);

        if (projectile.oldPos is null)
            return points;
        foreach (Vector2 oldPosition in projectile.oldPos)
        {
            if (oldPosition == Vector2.Zero)
                break;
            Vector2 point = oldPosition + projectile.Size * 0.5f;
            if (!IsFinite(point))
                break;
            float headDistance = Vector2.DistanceSquared(point, head);
            float segmentDistance = Vector2.DistanceSquared(point, points[^1]);
            if (!float.IsFinite(headDistance) || !float.IsFinite(segmentDistance) ||
                headDistance > MaximumTrailRadius * MaximumTrailRadius ||
                segmentDistance > MaximumSegmentLength * MaximumSegmentLength)
                break;
            if (segmentDistance > 0.25f)
                points.Add(point);
        }
        return points;
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
