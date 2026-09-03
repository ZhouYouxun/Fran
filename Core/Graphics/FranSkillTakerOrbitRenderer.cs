using System;
using System.Collections.Generic;
using FranMod.Content.Projectiles;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Core.Graphics;

// 技能夺取者的印记缠绕：以敌人为三维圆环中心，把后半圈放在 NPC 本体前绘制，
// 前半圈放在 NPC 本体后绘制。它只提交视觉图元，不生成弹幕，也没有任何伤害判定。
internal static class FranSkillTakerOrbitRenderer
{
    private const float DepthBoundaryEpsilon = 0.035f;
    private static bool shaderFaulted;

    public static bool TryDrawLayer(NPC npc, float progress, float fade, int tacticLayers, bool frontLayer)
    {
        if (Main.dedServ || shaderFaulted || npc is null || !npc.active || !IsFinite(npc.Center))
            return false;
        if (!float.IsFinite(progress) || !float.IsFinite(fade) ||
            !ShaderManager.TryGetShader("Fran.FranBladeTrailShader", out ManagedShader shader))
            return false;

        progress = MathHelper.Clamp(progress, 0f, 1f);
        fade = MathHelper.Clamp(fade, 0f, 1f);
        if (fade <= 0.01f)
            return true;

        tacticLayers = Math.Clamp(tacticLayers, 1, 4);
        int orbitCount = tacticLayers + (progress >= 0.72f ? 1 : 0);
        float baseRadius = MathHelper.Clamp(Math.Max(npc.width, npc.height) * 0.64f + 17f, 30f, 122f);
        float baseWidth = 4.2f + progress * 2.8f + (tacticLayers - 1) * 0.42f;
        float layerOpacity = fade * (0.60f + progress * 0.30f) * (frontLayer ? 1f : 0.48f);

        Color bodyColor = Color.Lerp(new Color(34, 4, 66), new Color(104, 24, 158), progress);
        Color edgeColor = Color.Lerp(new Color(158, 54, 242), new Color(240, 112, 255), progress);
        Color coreColor = Color.Lerp(new Color(226, 178, 255), Color.White, progress * 0.72f);

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
            shader.TrySetParameter("uOpacity", 1f);
            shader.TrySetParameter("uProfile", (float)FranProjectileTrailProfile.ArcaneWisp);
            shader.TrySetParameter("uFlowSpeed", -0.72f - progress * 0.48f);
            shader.TrySetParameter("uNoiseStrength", 0.70f + progress * 0.12f);
            shader.SetTexture(FranVfx.Original("FranTrailWispNoise"), 1, SamplerState.LinearWrap);
            shader.SetTexture(FranVfx.Original("FranTrailFilamentNoise"), 2, SamplerState.LinearWrap);

            for (int orbitIndex = 0; orbitIndex < orbitCount; orbitIndex++)
            {
                shader.TrySetParameter("uSeed", PositiveModulo(
                    npc.whoAmI * 0.173f + orbitIndex * 0.271f, 1f));
                List<List<Vector2>> segments = BuildLayerSegments(npc, progress, orbitCount,
                    orbitIndex, baseRadius, frontLayer);
                float orbitWidth = baseWidth * (0.92f + orbitIndex % 3 * 0.065f);
                float orbitOpacity = layerOpacity * (0.86f + orbitIndex % 2 * 0.10f);
                foreach (List<Vector2> segment in segments)
                {
                    DrawPass(segment, shader, orbitWidth * 1.82f, orbitOpacity * 0.74f,
                        1f, BlendState.Additive);
                    DrawPass(segment, shader, orbitWidth, orbitOpacity * 0.86f,
                        0f, BlendState.AlphaBlend);
                    DrawPass(segment, shader, Math.Max(1.45f, orbitWidth * 0.27f), orbitOpacity,
                        2f, BlendState.Additive);
                }
            }
            // 即使本帧的短弧完全落在另一侧，也代表着色器通道可用，不能误触发前景回退绘制。
            return true;
        }
        catch
        {
            shaderFaulted = true;
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

    public static void DrawFallback(NPC npc, float progress, float fade, int tacticLayers)
    {
        if (Main.dedServ || npc is null || !npc.active || !IsFinite(npc.Center))
            return;
        progress = MathHelper.Clamp(progress, 0f, 1f);
        fade = MathHelper.Clamp(fade, 0f, 1f);
        tacticLayers = Math.Clamp(tacticLayers, 1, 4);
        int orbitCount = tacticLayers + (progress >= 0.72f ? 1 : 0);
        float baseRadius = MathHelper.Clamp(Math.Max(npc.width, npc.height) * 0.64f + 17f, 30f, 122f);
        Color color = Color.Lerp(new Color(156, 50, 238), new Color(244, 126, 255), progress);

        FranVfx.BeginAdditive();
        try
        {
            for (int orbitIndex = 0; orbitIndex < orbitCount; orbitIndex++)
            {
                List<OrbitSample> samples = BuildOrbitSamples(npc, progress, orbitCount,
                    orbitIndex, baseRadius);
                for (int i = 1; i < samples.Count; i++)
                {
                    float completion = i / (float)(samples.Count - 1);
                    float opacity = MathF.Pow(1f - completion, 0.42f) * fade;
                    float width = StreakWidth(4.6f + progress * 2.2f, completion);
                    FranVfx.DrawVertical("Cal_BloomLineSoftEdge", samples[i - 1].Position,
                        samples[i].Position, color * (opacity * 0.30f), width * 2.1f);
                    FranVfx.DrawVertical("Cal_ThinEndedLine", samples[i - 1].Position,
                        samples[i].Position, Color.Lerp(color, Color.White, 0.72f) * (opacity * 0.78f),
                        Math.Max(1.15f, width * 0.25f));
                }
            }
        }
        finally
        {
            FranVfx.EndAdditive();
        }
    }

    public static void Unload() => shaderFaulted = false;

    private static List<List<Vector2>> BuildLayerSegments(NPC npc, float progress, int orbitCount,
        int orbitIndex, float baseRadius, bool frontLayer)
    {
        List<OrbitSample> samples = BuildOrbitSamples(npc, progress, orbitCount, orbitIndex, baseRadius);
        List<List<Vector2>> result = new(2);
        List<Vector2> current = null;

        for (int i = 0; i < samples.Count; i++)
        {
            OrbitSample sample = samples[i];
            bool belongsHere = frontLayer
                ? sample.Depth >= -DepthBoundaryEpsilon
                : sample.Depth <= DepthBoundaryEpsilon;
            if (belongsHere)
            {
                current ??= new List<Vector2>(samples.Count);
                if (i > 0 && !BelongsToLayer(samples[i - 1].Depth, frontLayer))
                    current.Add(DepthBoundary(samples[i - 1], sample));
                current.Add(sample.Position);
                continue;
            }

            if (current is null)
                continue;
            if (i > 0)
                current.Add(DepthBoundary(samples[i - 1], sample));
            if (current.Count >= 3)
                result.Add(current);
            current = null;
        }

        if (current is not null && current.Count >= 3)
            result.Add(current);
        return result;
    }

    private static List<OrbitSample> BuildOrbitSamples(NPC npc, float progress, int orbitCount,
        int orbitIndex, float baseRadius)
    {
        int pointCount = 16 + (int)MathF.Round(progress * 4f);
        float time = Main.GlobalTimeWrappedHourly;
        float angularSpeed = 4.15f + progress * 2.10f + orbitIndex * 0.12f;
        float phase = time * angularSpeed + orbitIndex * MathHelper.TwoPi / orbitCount;
        float arcSpan = 2.12f + progress * 0.48f;
        float planeRotation = orbitIndex % 4 switch
        {
            0 => -0.18f,
            1 => 0.86f,
            2 => -0.94f,
            _ => 0.34f
        };
        float tilt = orbitIndex % 3 switch
        {
            0 => 0.50f,
            1 => 0.72f,
            _ => 0.93f
        };
        float radius = baseRadius * (0.84f + orbitIndex % 4 * 0.075f) *
            (1f + MathF.Sin(time * 1.45f + orbitIndex * 1.7f) * 0.035f);
        Vector2 axisX = Vector2.UnitX.RotatedBy(planeRotation);
        Vector2 axisY = axisX.RotatedBy(MathHelper.PiOver2) * MathF.Cos(tilt);
        float depthScale = MathF.Sin(tilt);
        Vector2 center = npc.Center + Vector2.UnitY * npc.gfxOffY;
        List<OrbitSample> samples = new(pointCount);

        for (int i = 0; i < pointCount; i++)
        {
            float completion = i / (float)(pointCount - 1);
            float angle = phase - completion * arcSpan;
            float cosine = MathF.Cos(angle);
            float sine = MathF.Sin(angle);
            Vector2 position = center + (axisX * cosine + axisY * sine) * radius;
            samples.Add(new OrbitSample(position, sine * depthScale));
        }
        return samples;
    }

    private static void DrawPass(List<Vector2> points, ManagedShader shader, float width,
        float opacity, float passMode, BlendState blendState)
    {
        Main.instance.GraphicsDevice.BlendState = blendState;
        shader.TrySetParameter("uPassMode", passMode);
        PrimitiveSettings settings = new(
            completion => StreakWidth(width, completion),
            completion => Color.White * (MathF.Pow(1f - completion, 0.22f) * opacity),
            Smoothen: false,
            Pixelate: false,
            Shader: shader);
        PrimitiveRenderer.RenderTrail(points, settings, Math.Max(points.Count, 8));
    }

    // 延续 Blossom Flux 光环的轮廓：头部迅速展开，随后一路收细到尾端。
    private static float StreakWidth(float width, float completion)
    {
        completion = MathHelper.Clamp(completion, 0f, 1f);
        const float HeadRatio = 0.15f;
        if (completion < HeadRatio)
            return width * (0.15f + MathF.Sin(completion / HeadRatio * MathHelper.PiOver2) * 0.85f);
        return width * MathHelper.Lerp(1f, 0.035f,
            Utils.GetLerpValue(HeadRatio, 1f, completion, true));
    }

    private static bool BelongsToLayer(float depth, bool frontLayer) => frontLayer
        ? depth >= -DepthBoundaryEpsilon
        : depth <= DepthBoundaryEpsilon;

    private static Vector2 DepthBoundary(OrbitSample a, OrbitSample b)
    {
        float denominator = MathF.Abs(a.Depth) + MathF.Abs(b.Depth);
        float amount = denominator <= 0.0001f ? 0.5f : MathF.Abs(a.Depth) / denominator;
        return Vector2.Lerp(a.Position, b.Position, amount);
    }

    private static float PositiveModulo(float value, float modulus) =>
        (value % modulus + modulus) % modulus;

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    private readonly record struct OrbitSample(Vector2 Position, float Depth);
}
