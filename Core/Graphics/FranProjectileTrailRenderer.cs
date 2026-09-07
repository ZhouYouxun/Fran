using System;
using System.Collections.Generic;
using FranMod.Content.Projectiles;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Core.Graphics;

internal enum FranProjectileTrailProfile
{
    TelekineticNeedle,
    ArcaneWisp,
    SpectralBlade,
    HeavyHammer,
    // 折念刃的宽而圆钝波体；着色器使用重压分支，几何轮廓单独定义。
    TelekineticWave
}

// Texture-driven projectile ribbons. Geometry is shared, but every projectile family owns its
// width envelope, two material maps, flow direction and a distinct shader branch. This keeps the
// trails related without reducing them to the same ribbon with a different color.
internal static class FranProjectileTrailRenderer
{
    private const float MaximumTrailRadius = 760f;
    private const float MaximumSegmentLength = 58f;
    private static bool shaderTrailFaulted;
    private static bool telekineticWaveShaderFaulted;

    public static bool TryDraw(Projectile projectile, FranProjectileTrailProfile profile,
        Color bodyColor, Color edgeColor, Color coreColor, float halfWidth, float opacity)
    {
        if (Main.dedServ || shaderTrailFaulted || projectile.oldPos is null || projectile.oldPos.Length < 2 ||
            !IsFinite(projectile.Center) || !float.IsFinite(halfWidth) || !float.IsFinite(opacity))
            return false;

        List<Vector2> points = CollectSafePoints(projectile);
        if (points.Count < 3 || !ShaderManager.TryGetShader("Fran.FranBladeTrailShader", out ManagedShader shader))
            return false;

        float maximumHalfWidth = profile == FranProjectileTrailProfile.TelekineticWave ? 96f : 68f;
        halfWidth = MathHelper.Clamp(MathF.Abs(halfWidth), 3f, maximumHalfWidth);
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
                FranVfx.DrawVertical("BloomLineSoftEdge", points[i], points[i - 1],
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

    public static bool TryDrawTelekineticWave(Projectile projectile, float opacity)
    {
        if (Main.dedServ || telekineticWaveShaderFaulted || projectile.oldPos is null ||
            !IsFinite(projectile.Center) || !float.IsFinite(opacity))
            return false;

        List<Vector2> points = BuildTelekineticWavePoints(projectile);
        if (points.Count < 6 || !ShaderManager.TryGetShader("Fran.FranTelekineticWaveShader",
                out ManagedShader shader))
            return false;

        opacity = MathHelper.Clamp(opacity, 0f, 1f);
        if (opacity <= 0.01f)
            return false;

        GraphicsDevice device = Main.instance.GraphicsDevice;
        BlendState previousBlend = device.BlendState;
        DepthStencilState previousDepth = device.DepthStencilState;
        RasterizerState previousRasterizer = device.RasterizerState;
        SamplerState previousSampler0 = device.SamplerStates[0];
        SamplerState previousSampler1 = device.SamplerStates[1];
        Texture previousTexture1 = device.Textures[1];
        try
        {
            shader.TrySetParameter("uOpacity", 1f);
            shader.SetTexture(ModContent.Request<Texture2D>("Terraria/Images/Misc/Perlin").Value,
                1, SamplerState.LinearWrap);
            // _Wave writes A=0 and is effectively submitted through Terraria's premultiplied
            // AlphaBlend state. That produces a pure additive RGB result; forcing Additive here
            // would multiply the already-faded color by zero source alpha.
            device.BlendState = BlendState.AlphaBlend;

            float WidthFunction(float completion)
            {
                float sine = MathF.Sin(MathHelper.Clamp(completion, 0f, 1f) * MathHelper.Pi);
                return 200f * MathHelper.Lerp(0.25f, 1f,
                    MathF.Pow(MathF.Max(0f, sine), 0.60f));
            }

            Color[] palette =
            [
                new Color(230, 252, 255),
                new Color(125, 225, 255),
                new Color(50, 150, 240),
                new Color(14, 52, 115)
            ];
            Color ColorFunction(float completion)
            {
                int paletteIndex = Math.Clamp((int)(completion * (palette.Length - 1)),
                    0, palette.Length - 1);
                Color color = palette[paletteIndex] * ((1f - completion) * opacity * 1.2f);
                color.A = 0;
                return color;
            }

            PrimitiveSettings wave = new(
                WidthFunction,
                ColorFunction,
                Smoothen: false,
                Pixelate: false,
                Shader: shader);
            PrimitiveRenderer.RenderTrail(points, wave, points.Count);
            return true;
        }
        catch
        {
            telekineticWaveShaderFaulted = true;
            return false;
        }
        finally
        {
            device.BlendState = previousBlend;
            device.DepthStencilState = previousDepth;
            device.RasterizerState = previousRasterizer;
            device.SamplerStates[0] = previousSampler0;
            device.SamplerStates[1] = previousSampler1;
            device.Textures[1] = previousTexture1;
        }
    }

    public static void Unload()
    {
        shaderTrailFaulted = false;
        telekineticWaveShaderFaulted = false;
    }

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
            FranProjectileTrailProfile.TelekineticWave => MathF.Pow(1f - completion, 0.30f),
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
            // 对标 UnholyEssence_Wave：中段饱满，两端保留圆钝宽度，不收成尖角。
            FranProjectileTrailProfile.TelekineticWave => MathHelper.Lerp(0.25f, 1f,
                MathF.Pow(MathF.Max(0f, MathF.Sin(completion * MathHelper.Pi)), 0.60f)),
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
        FranProjectileTrailProfile.TelekineticWave => FranVfx.Original("FranTrailWispNoise"),
        _ => FranVfx.Original("FranTrailFractureNoise")
    };

    private static Texture2D DetailTexture(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => FranVfx.Original("FranTrailFractureNoise"),
        FranProjectileTrailProfile.ArcaneWisp => FranVfx.Original("FranTrailFilamentNoise"),
        FranProjectileTrailProfile.SpectralBlade => FranVfx.Original("FranTrailFilamentNoise"),
        FranProjectileTrailProfile.TelekineticWave => FranVfx.Original("FranTrailFilamentNoise"),
        _ => FranVfx.Original("FranTrailWispNoise")
    };

    private static float AuraScale(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 1.62f,
        FranProjectileTrailProfile.ArcaneWisp => 1.88f,
        FranProjectileTrailProfile.SpectralBlade => 1.54f,
        FranProjectileTrailProfile.TelekineticWave => 1.92f,
        _ => 1.78f
    };

    private static float CoreScale(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 0.24f,
        FranProjectileTrailProfile.ArcaneWisp => 0.30f,
        FranProjectileTrailProfile.SpectralBlade => 0.22f,
        FranProjectileTrailProfile.TelekineticWave => 0.16f,
        _ => 0.18f
    };

    private static float FlowSpeed(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 1.8f,
        FranProjectileTrailProfile.ArcaneWisp => -0.82f,
        FranProjectileTrailProfile.SpectralBlade => 1.15f,
        FranProjectileTrailProfile.TelekineticWave => 1.46f,
        _ => 2.25f
    };

    private static float NoiseStrength(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 0.52f,
        FranProjectileTrailProfile.ArcaneWisp => 0.86f,
        FranProjectileTrailProfile.SpectralBlade => 0.68f,
        FranProjectileTrailProfile.TelekineticWave => 0.62f,
        _ => 0.74f
    };

    private static float AuraOpacity(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 0.58f,
        FranProjectileTrailProfile.ArcaneWisp => 0.78f,
        FranProjectileTrailProfile.SpectralBlade => 0.62f,
        FranProjectileTrailProfile.TelekineticWave => 0.74f,
        _ => 0.72f
    };

    private static float BodyOpacity(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.ArcaneWisp => 0.88f,
        FranProjectileTrailProfile.TelekineticWave => 0.96f,
        _ => 1f
    };

    private static float CoreOpacity(FranProjectileTrailProfile profile) => profile switch
    {
        FranProjectileTrailProfile.TelekineticNeedle => 1f,
        FranProjectileTrailProfile.ArcaneWisp => 0.74f,
        FranProjectileTrailProfile.SpectralBlade => 0.94f,
        FranProjectileTrailProfile.TelekineticWave => 1f,
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

    private static List<Vector2> BuildTelekineticWavePoints(Projectile projectile)
    {
        List<Vector2> controls = new(projectile.oldPos?.Length ?? 0);
        if (projectile.oldPos is null)
            return controls;
        Vector2 forward = projectile.velocity.SafeNormalize(Vector2.UnitX);
        Vector2 frontOffset = forward * (projectile.width * 1.2f + 4f);
        foreach (Vector2 oldPosition in projectile.oldPos)
        {
            if (oldPosition == Vector2.Zero)
                break;
            Vector2 point = oldPosition + projectile.Size * 0.5f + frontOffset;
            if (!IsFinite(point) || Vector2.DistanceSquared(point, projectile.Center) >
                MaximumTrailRadius * MaximumTrailRadius)
                break;
            controls.Add(point);
        }
        return ResampleCatmullRomByDistance(controls, 60);
    }

    // Calamity's renderer smooths the eight oldPos controls to sixty points and then evaluates
    // width/color by travelled distance. Luminance uses point index, so equal-distance resampling
    // reproduces the same visible profile while keeping every generated segment bounded.
    private static List<Vector2> ResampleCatmullRomByDistance(List<Vector2> controls, int resultCount)
    {
        if (controls.Count < 5)
            return new List<Vector2>();

        const int SamplesPerSegment = 12;
        List<Vector2> dense = new((controls.Count - 1) * SamplesPerSegment + 1) { controls[0] };
        for (int segment = 0; segment < controls.Count - 1; segment++)
        {
            Vector2 p0 = controls[Math.Max(segment - 1, 0)];
            Vector2 p1 = controls[segment];
            Vector2 p2 = controls[segment + 1];
            Vector2 p3 = controls[Math.Min(segment + 2, controls.Count - 1)];
            for (int step = 1; step <= SamplesPerSegment; step++)
            {
                Vector2 point = Vector2.CatmullRom(p0, p1, p2, p3,
                    step / (float)SamplesPerSegment);
                if (!IsFinite(point) || Vector2.DistanceSquared(point, dense[^1]) >
                    MaximumSegmentLength * MaximumSegmentLength)
                    return new List<Vector2>();
                dense.Add(point);
            }
        }

        float[] distances = new float[dense.Count];
        for (int i = 1; i < dense.Count; i++)
            distances[i] = distances[i - 1] + Vector2.Distance(dense[i - 1], dense[i]);
        float totalLength = distances[^1];
        if (!float.IsFinite(totalLength) || totalLength <= 0.01f)
            return new List<Vector2>();

        List<Vector2> result = new(resultCount);
        int denseIndex = 1;
        for (int i = 0; i < resultCount; i++)
        {
            float targetDistance = totalLength * i / (resultCount - 1f);
            while (denseIndex < distances.Length - 1 && distances[denseIndex] < targetDistance)
                denseIndex++;
            float span = distances[denseIndex] - distances[denseIndex - 1];
            float local = span <= 0.0001f ? 0f :
                (targetDistance - distances[denseIndex - 1]) / span;
            result.Add(Vector2.Lerp(dense[denseIndex - 1], dense[denseIndex], local));
        }
        return result;
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
