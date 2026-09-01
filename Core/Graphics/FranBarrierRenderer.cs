using System;
using FranMod.Content.Projectiles;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Core.Graphics;

internal static class FranBarrierRenderer
{
    private static bool shaderFaulted;

    public static bool TryDraw(Vector2 center, float charge, float hitFlash, float rotation)
    {
        if (Main.dedServ || shaderFaulted || !IsFinite(center) ||
            !float.IsFinite(charge) || !float.IsFinite(hitFlash) || !float.IsFinite(rotation) ||
            !ShaderManager.TryGetShader("Fran.FranBarrierSurfaceShader", out ManagedShader shader))
            return false;

        charge = MathHelper.Clamp(charge, 0f, 1f);
        hitFlash = MathHelper.Clamp(hitFlash, 0f, 1f);
        if (charge <= 0.01f && hitFlash <= 0.01f)
            return false;

        GraphicsDevice device = Main.instance.GraphicsDevice;
        Texture previousTexture1 = device.Textures[1];
        SamplerState previousSampler1 = device.SamplerStates[1];
        bool originalBatchEnded = false;
        bool customBatchStarted = false;
        try
        {
            shader.TrySetParameter("uShieldColor", new Color(28, 96, 205));
            shader.TrySetParameter("uEdgeColor", Color.Lerp(new Color(85, 215, 255), Color.White, hitFlash * 0.62f));
            shader.TrySetParameter("uCoreColor", new Color(175, 242, 255));
            shader.TrySetParameter("uCharge", MathF.Sqrt(charge));
            shader.TrySetParameter("uHitFlash", hitFlash);
            shader.TrySetParameter("uOpacity", 0.78f + charge * 0.16f);
            shader.TrySetParameter("uRotation", rotation);
            shader.SetTexture(FranVfx.Shared("NoiseSoft"), 1, SamplerState.LinearWrap);

            Main.spriteBatch.End();
            originalBatchEnded = true;
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, shader.WrappedEffect,
                Main.GameViewMatrix.TransformationMatrix);
            customBatchStarted = true;

            // Rover Drive feeds a square noise canvas into its shield shader.  Our old draw used
            // Terraria's 1x1000 MagicPixel as the canvas and then scaled the whole texture, which
            // turned the mathematically circular shader into a world-height column.  A square
            // canvas preserves the shader's 0..1 UV sphere and keeps its dimensions finite.
            Texture2D canvas = FranVfx.Shared("NoiseSoft");
            Vector2 origin = canvas.Size() * 0.5f;
            Vector2 position = center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + rotation * 0.37f) * 0.018f;
            float diameter = (156f + hitFlash * 18f) * pulse;
            Vector2 scale = Vector2.One * diameter / canvas.Size();

            shader.TrySetParameter("uLayer", 0f);
            shader.Apply();
            Main.spriteBatch.Draw(canvas, position, null, Color.White, 0f, origin, scale,
                SpriteEffects.None, 0f);

            shader.TrySetParameter("uLayer", 1f);
            shader.Apply();
            Main.spriteBatch.Draw(canvas, position, null, Color.White, 0f, origin, scale * 1.025f,
                SpriteEffects.None, 0f);
            return true;
        }
        catch
        {
            shaderFaulted = true;
            return false;
        }
        finally
        {
            if (customBatchStarted)
                Main.spriteBatch.End();
            if (originalBatchEnded)
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
            device.Textures[1] = previousTexture1;
            device.SamplerStates[1] = previousSampler1;
        }
    }

    public static void Unload() => shaderFaulted = false;

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
