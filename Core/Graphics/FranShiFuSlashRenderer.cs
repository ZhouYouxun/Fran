using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Core.Graphics;

// Only the normal combo uses this material. Quickdraw keeps its own renderer.
internal static class FranShiFuSlashRenderer
{
    internal const int Samples = 97;
    private static Effect material;
    private static Texture2D flow, erosion;
    private static readonly VertexPositionColorTexture[] mesh = new VertexPositionColorTexture[Samples * 2];

    internal static void Unload()
    {
        material = null;
        flow = erosion = null;
    }

    internal static void Draw(Vector2 pivot, ReadOnlySpan<float> angles, float reach,
        Color energy, float strength, int style)
    {
        if (Main.dedServ || angles.Length != Samples || !Finite(pivot) ||
            !float.IsFinite(reach) || !float.IsFinite(strength) || strength < 0.015f)
            return;
        reach = Math.Clamp(reach, 24f, 480f);
        strength = Math.Clamp(strength, 0f, 1f);
        float thickness = style == 2 ? 0.49f : style == 1 ? 0.39f : 0.40f;
        for (int i = 0; i < Samples; i++)
        {
            if (!float.IsFinite(angles[i])) return;
            float t = i / (float)(Samples - 1);
            Vector2 radial = angles[i].ToRotationVector2();
            // Finite local coordinates and a capped radius prevent invalid screen-wide triangles.
            Vector2 outside = pivot + radial * reach - Main.screenPosition;
            float taper = 1f - MathF.Pow(t, 2.2f) * 0.80f;
            Vector2 inside = pivot + radial * reach * (1f - thickness * taper) - Main.screenPosition;
            Color opacity = Color.White * strength;
            mesh[i * 2] = new(new Vector3(outside, 0), opacity, new(t, 0));
            mesh[i * 2 + 1] = new(new Vector3(inside, 0), opacity, new(t, 1));
        }

        material ??= ModContent.Request<Effect>("Fran/Effects/FranShiFuSlash", AssetRequestMode.ImmediateLoad).Value;
        flow ??= ModContent.Request<Texture2D>("Fran/Assets/OriginalVFX/ShiFu/Flow", AssetRequestMode.ImmediateLoad).Value;
        erosion ??= ModContent.Request<Texture2D>("Fran/Assets/OriginalVFX/ShiFu/Erosion", AssetRequestMode.ImmediateLoad).Value;
        material.Parameters["ArcTransform"].SetValue(Main.GameViewMatrix.TransformationMatrix *
            Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, 0, 1));
        material.Parameters["AnimationTime"].SetValue(Main.GlobalTimeWrappedHourly);
        material.Parameters["EnergyTint"].SetValue(energy.ToVector3());
        material.Parameters["ShadowTint"].SetValue(Color.Lerp(new Color(7, 16, 31), energy, 0.12f).ToVector3());

        GraphicsDevice device = Main.spriteBatch.GraphicsDevice;
        Main.spriteBatch.End();
        Texture old0 = device.Textures[0], old1 = device.Textures[1];
        SamplerState sampler0 = device.SamplerStates[0], sampler1 = device.SamplerStates[1];
        BlendState blend = device.BlendState;
        DepthStencilState depth = device.DepthStencilState;
        RasterizerState raster = device.RasterizerState;
        try
        {
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            for (int pass = 0; pass < 2; pass++)
            {
                device.BlendState = pass == 0 ? BlendState.AlphaBlend : BlendState.Additive;
                material.Parameters["PassMode"].SetValue((float)pass);
                material.CurrentTechnique.Passes[0].Apply();
                device.Textures[0] = flow;
                device.Textures[1] = erosion;
                device.SamplerStates[0] = device.SamplerStates[1] = SamplerState.LinearWrap;
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, mesh, 0, mesh.Length - 2);
            }
        }
        finally
        {
            device.Textures[0] = old0;
            device.Textures[1] = old1;
            device.SamplerStates[0] = sampler0;
            device.SamplerStates[1] = sampler1;
            device.BlendState = blend;
            device.DepthStencilState = depth;
            device.RasterizerState = raster;
            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }

    private static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);
}
