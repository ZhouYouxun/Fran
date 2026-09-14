using System;
using FranMod.Content.Projectiles;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Core.Graphics;

internal static class FranBarrierRenderer
{
    private static bool shaderFaulted;
    private static bool shaderMissingReported;

    public static bool TryDraw(Vector2 center, float charge, float hitFlash, float rotation)
    {
        if (Main.dedServ || shaderFaulted || !IsFinite(center) ||
            !float.IsFinite(charge) || !float.IsFinite(hitFlash) || !float.IsFinite(rotation))
            return false;

        charge = MathHelper.Clamp(charge, 0f, 1f);
        hitFlash = MathHelper.Clamp(hitFlash, 0f, 1f);
        if (charge <= 0.01f && hitFlash <= 0.01f)
            return false;

        if (!ShaderManager.TryGetShader("Fran.FranBarrierSurfaceShader", out ManagedShader shader))
        {
            if (!shaderMissingReported)
                ModLoader.GetMod("Fran")?.Logger.Warn("Energy barrier shader is not available: Fran.FranBarrierSurfaceShader.");
            shaderMissingReported = true;
            return false;
        }
        shaderMissingReported = false;

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
            shader.TrySetParameter("uCharge", charge);
            shader.TrySetParameter("uHitFlash", hitFlash);
            shader.TrySetParameter("uOpacity", 0.94f * charge);
            shader.TrySetParameter("uRotation", rotation);

            Main.spriteBatch.End();
            originalBatchEnded = true;
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, shader.WrappedEffect,
                Main.GameViewMatrix.TransformationMatrix);
            customBatchStarted = true;

            Texture2D canvas = FranVfx.Shared("NoiseSoft");
            shader.SetTexture(canvas, 1, SamplerState.LinearWrap);
            Vector2 origin = canvas.Size() * 0.5f;
            Vector2 position = center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.9f + rotation * 0.37f) * 0.018f;
            float diameter = (156f + hitFlash * 18f) * pulse;
            // SpriteBatch scales source pixels, not UVs; normalize both dimensions for a round shield.
            Vector2 scale = new Vector2(diameter) / canvas.Size();

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
        catch (Exception exception)
        {
            shaderFaulted = true;
            ModLoader.GetMod("Fran")?.Logger.Warn($"Energy barrier drawing failed. {exception}");
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

    public static void Unload()
    {
        shaderFaulted = false;
        shaderMissingReported = false;
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
