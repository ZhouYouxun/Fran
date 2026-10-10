using System;
using FranMod.Content.Projectiles;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Core.Graphics;

internal static class FranDiscVisuals
{
    private static Vector2[] tipPoints = Array.Empty<Vector2>();
    private static VertexPosition2DColorTexture[] tipVertices = Array.Empty<VertexPosition2DColorTexture>();
    private static Texture2D Material(string name) => ModContent.Request<Texture2D>("Fran/Assets/Textures/SpinningDisc/" + name).Value;

    public static void DrawConnection(Vector2 start, Vector2 end, Color accent, float opacity, int identity)
    {
        Vector2 delta = end - start;
        float length = delta.Length();
        if (!float.IsFinite(length) || length < 12f || opacity <= 0f) return;
        Vector2 normal = delta.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
        // Fixed world-space pitch; stretching the link adds turns rather than stretching stars.
        float phase = Main.GlobalTimeWrappedHourly * 2.6f + identity * 0.37f;
        float amplitude = Math.Min(17f, length * 0.065f);
        int samples = Math.Clamp((int)(length / 6f), 8, 150);
        FranVfx.BeginAdditive();
        try
        {
            // Depth-sort back and front portions: crossing brightness and size rotate with the helix.
            for (int layer = 0; layer < 2; layer++)
            {
                for (int strand = 0; strand < 2; strand++)
                {
                    float offset = strand * MathHelper.Pi;
                    for (int i = 1; i <= samples; i++)
                    {
                        float t = i / (float)samples;
                        float angle = t * length / 100f * MathHelper.TwoPi - phase + offset;
                        float depth = MathF.Cos(angle);
                        if ((depth >= 0f ? 1 : 0) != layer) continue;
                        Vector2 a = Point((i - 1f) / samples, offset);
                        Vector2 b = Point(t, offset);
                        Color tint = Color.Lerp(accent, Color.White, strand == 0 ? 0.68f : 0.22f);
                        float brightness = (0.34f + (depth + 1f) * 0.23f) * opacity;
                        Line(a, b, tint * (brightness * 0.14f), 5f);
                        Line(a, b, tint * (brightness * 0.7f), layer == 0 ? 0.8f : 1.35f);
                        if (i % 5 == 0)
                            Star(b, tint, brightness, 9f + (depth + 1f) * 3f, angle * 0.3f, i % 10 == 0);
                    }
                }
                if (layer == 0)
                {
                    int bridges = Math.Max(1, (int)(length / 28f));
                    for (int i = 1; i < bridges; i++)
                    {
                        float t = i / (float)bridges;
                        Vector2 a = Point(t, 0), b = Point(t, MathHelper.Pi);
                        float pulse = 0.5f + 0.5f * MathF.Sin(t * 14f - phase * 1.7f);
                        Line(a, b, Color.Lerp(accent, Color.White, 0.5f) * (opacity * (0.12f + pulse * 0.13f)), 0.8f);
                    }
                }
            }
            // Light packets travel from hand to blade, on opposite phases of the two strands.
            for (int i = 0; i < 2; i++)
            {
                float t = (Main.GlobalTimeWrappedHourly * 0.45f + i * 0.5f) % 1f;
                Star(Point(t, i * MathHelper.Pi), accent, opacity * MathF.Sin(t * MathHelper.Pi), 20f, phase, true);
            }
            Star(start, accent, opacity * 0.65f, 20f, phase * 0.2f, false);
            Star(end, accent, opacity * 0.85f, 29f, -phase * 0.3f, true);
            Texture2D ring = Material("FadedStarRing");
            Main.EntitySpriteDraw(ring, end - Main.screenPosition, null, accent * (opacity * 0.25f),
                -phase * 0.2f, ring.Size() * 0.5f, 30f / ring.Width, SpriteEffects.None);
        }
        finally { FranVfx.EndAdditive(); }

        Vector2 Point(float t, float strandPhase)
        {
            float envelope = MathF.Sin(MathHelper.Pi * t);
            float angle = t * length / 100f * MathHelper.TwoPi - phase + strandPhase;
            return Vector2.Lerp(start, end, t) + normal * (MathF.Sin(angle) * amplitude * envelope);
        }
    }

    private static void Line(Vector2 a, Vector2 b, Color tint, float width)
    {
        Vector2 edge = b - a;
        Main.EntitySpriteDraw(TextureAssets.MagicPixel.Value, a - Main.screenPosition, new Rectangle(0, 0, 1, 1),
            tint, edge.ToRotation(), new Vector2(0, 0.5f), new Vector2(edge.Length(), width), SpriteEffects.None);
    }

    private static void Star(Vector2 center, Color tint, float opacity, float size, float angle, bool shard)
    {
        // LeonidStarlightMote's three layers: soft base, colored flare, smaller white center.
        Texture2D flare = Material(shard ? "PulseStar" : "ShineFlare");
        Texture2D glow = Material("BloomCirclePinpoint");
        Vector2 position = center - Main.screenPosition;
        Main.EntitySpriteDraw(glow, position, null, tint * (opacity * 0.28f), 0f,
            glow.Size() * 0.5f, size * 2.1f / glow.Width, SpriteEffects.None);
        Main.EntitySpriteDraw(flare, position, null, tint * opacity, angle,
            flare.Size() * 0.5f, size / Math.Max(flare.Width, flare.Height), SpriteEffects.None);
        Main.EntitySpriteDraw(flare, position, null, Color.White * (opacity * 0.8f), angle,
            flare.Size() * 0.5f, size * 0.45f / Math.Max(flare.Width, flare.Height), SpriteEffects.None);
    }

    public static void DrawDisc(Vector2 center, float radius, float angle, Color accent, float opacity)
    {
        FranVfx.BeginAdditive();
        try
        {
            Draw("CircularSmearSmokey", radius, angle * 0.8f, accent * (opacity * 0.24f));
            Draw("FranThirdSlashSmear", radius * 0.98f, angle, Color.Lerp(accent, Color.White, 0.45f) * (opacity * 0.36f));
            Draw("CircularSmear", radius * 0.78f, angle * 1.1f, accent * (opacity * 0.2f));
            Texture2D bloom = Material("BloomFlare");
            Main.EntitySpriteDraw(bloom, center - Main.screenPosition, null, accent * (opacity * 0.15f), angle * 0.4f,
                bloom.Size() * 0.5f, 36f / bloom.Width, SpriteEffects.None);
        }
        finally { FranVfx.EndAdditive(); }
        void Draw(string name, float r, float rotation, Color color)
        {
            Texture2D texture = FranVfx.Shared(name);
            FranVfx.DrawShared(name, center, color, rotation, r * 2f / Math.Max(texture.Width, texture.Height));
        }
    }

    public static void DrawTipTrail(Vector2[] history, int count, Color tint, float opacity, float scale)
    {
        if (count < 3 || opacity <= 0f) return;
        int vertexCount = BuildTipRibbon(history, Math.Min(count, history.Length), scale);
        if (vertexCount < 6) return;
        // Flush sprite layers before drawing GPU primitives; restore the normal projectile batch afterwards.
        Main.spriteBatch.End();
        GraphicsDevice device = Main.instance.GraphicsDevice;
        BlendState blend = device.BlendState;
        DepthStencilState depth = device.DepthStencilState;
        RasterizerState raster = device.RasterizerState;
        SamplerState sampler = device.SamplerStates[1];
        Texture texture = device.Textures[1];
        try
        {
            if (ShaderManager.TryGetShader("Fran.FranDiscTipShader", out ManagedShader shader))
            {
                shader.TrySetParameter("uTint", tint);
                shader.TrySetParameter("uOpacity", opacity);
                shader.SetTexture(ModContent.Request<Texture2D>("Fran/Assets/TrailNoiseReference/FranTrailBlobNoise").Value,
                    1, SamplerState.LinearWrap);
                Luminance.Common.Utilities.Utilities.CalculatePrimitiveMatrices(Main.screenWidth, Main.screenHeight,
                    out Matrix view, out Matrix projection);
                shader.TrySetParameter("uWorldViewProjection", view * projection);
                device.BlendState = BlendState.AlphaBlend;
                device.DepthStencilState = DepthStencilState.None;
                device.RasterizerState = RasterizerState.CullNone;
                shader.Apply();
                // The history already contains angular substeps. Feed it directly to the
                // GPU: the generic renderer's second resampling can collapse adjacent points.
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, tipVertices, 0, vertexCount - 2);
            }
        }
        finally
        {
            device.BlendState = blend;
            device.DepthStencilState = depth;
            device.RasterizerState = raster;
            device.SamplerStates[1] = sampler;
            device.Textures[1] = texture;
            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }

    // The True Night's Edge / Blazing Phantom Blade spinning silhouette: several
    // angular afterimages of the same blade, a brighter rotating triad, and a
    // sharp white highlight. Use ShiFu's own textures so this stays an overlay.
    public static void DrawPhantomSpin(Vector2 center, float angle, Color accent, float opacity,
        float sizeMultiplier, Texture2D orbitBlade = null)
    {
        if (!FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh) || opacity <= 0f) return;
        bool miniature = orbitBlade is not null;
        Texture2D ghost = orbitBlade ?? FranShiFuVisuals.GhostTexture;
        Texture2D glow = miniature ? orbitBlade : FranShiFuVisuals.GlowTexture;
        float bladeRadius = FranShiFuVisuals.BladeLengthFromGripPixels *
            1.05f * FranShiFuVisuals.TextureScale * sizeMultiplier;
        float ghostScale = miniature ? bladeRadius / Math.Max(1, ghost.Width) :
            1.05f * sizeMultiplier * FranShiFuVisuals.TextureScaleFor(ghost);
        float glowScale = miniature ? ghostScale :
            1.05f * sizeMultiplier * FranShiFuVisuals.TextureScaleFor(glow);
        Vector2 screenCenter = center - Main.screenPosition;
        Color saturated = Color.Lerp(accent, Color.White, 0.4f);
        saturated.A = 0;
        FranVfx.BeginAdditive();
        try
        {
            for (int trail = 2; trail >= 0; trail--)
            {
                float fade = (3f - trail) / 3f;
                for (int spoke = 0; spoke < 3; spoke++)
                {
                    float rotation = angle - trail * 0.17f + spoke * MathHelper.TwoPi / 3f;
                    Vector2 bladePosition = miniature ? screenCenter : screenCenter -
                        rotation.ToRotationVector2() * FranShiFuVisuals.GripInset * sizeMultiplier;
                    Main.EntitySpriteDraw(ghost, bladePosition, null,
                        saturated * (opacity * fade * 0.13f), rotation + (miniature ? 0f : MathHelper.PiOver4),
                        miniature ? new Vector2(0f, ghost.Height * 0.5f) : new Vector2(0f, ghost.Height),
                        ghostScale, SpriteEffects.None);
                    if (trail == 0)
                        Main.EntitySpriteDraw(glow, bladePosition, null,
                            Color.White * (opacity * 0.12f), rotation + (miniature ? 0f : MathHelper.PiOver4),
                            miniature ? new Vector2(0f, glow.Height * 0.5f) : new Vector2(0f, glow.Height),
                            glowScale, SpriteEffects.None);
                }
            }
            Texture2D tear = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            Vector2 highlight = screenCenter + angle.ToRotationVector2() *
                bladeRadius * 0.84f;
            Main.EntitySpriteDraw(tear, highlight, null, saturated * (opacity * 0.32f),
                angle + MathHelper.PiOver4, tear.Size() * 0.5f,
                new Vector2(0.35f, 0.7f) * sizeMultiplier, SpriteEffects.None);
        }
        finally { FranVfx.EndAdditive(); }
    }

    private static int BuildTipRibbon(Vector2[] history, int count, float scale)
    {
        if (tipPoints.Length < count) Array.Resize(ref tipPoints, count);
        if (tipVertices.Length < count * 2) Array.Resize(ref tipVertices, count * 2);
        int points = 0;
        for (int i = 0; i < count; i++)
        {
            Vector2 position = history[i];
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) continue;
            if (points > 0 && Vector2.DistanceSquared(position, tipPoints[points - 1]) < 0.0001f) continue;
            tipPoints[points++] = position;
        }
        if (points < 3) return 0;

        Vector2 lastTangent = (tipPoints[1] - tipPoints[0]).SafeNormalize(Vector2.UnitX);
        for (int i = 0; i < points; i++)
        {
            float t = i / (float)(points - 1);
            // Retain the established ribbon width, taper, and progression scaling.
            float width = 4f * (3f + 13f * MathF.Sin(MathHelper.Pi * t)) * scale * (1f - t);
            Vector2 tangent = (tipPoints[Math.Min(i + 1, points - 1)] - tipPoints[Math.Max(0, i - 1)])
                .SafeNormalize(lastTangent);
            lastTangent = tangent;
            Vector2 normal = new(-tangent.Y, tangent.X);
            Vector2 center = tipPoints[i] - Main.screenPosition;
            // Both neighboring triangles share this same edge, including the averaged
            // tangent. UVs keep the original shader's width-normalized ribbon convention.
            tipVertices[i * 2] = new(center - normal * width, Color.White, new Vector2(t, 0.5f - width * 0.5f), width);
            tipVertices[i * 2 + 1] = new(center + normal * width, Color.White, new Vector2(t, 0.5f + width * 0.5f), width);
        }
        return points * 2;
    }
}
