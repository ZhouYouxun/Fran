using FranMod.Content.Projectiles.BlackHole;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace FranMod.Content.Systems.BlackHole;

[Autoload(Side = ModSide.Client)]
public sealed class SingularityRenderer : ModSystem
{
    private static Projectile RenderedHole => FranSingularityUltimate.FindAny() is { } ultimate
        ? ultimate.BodyVisible ? ultimate.Projectile : null : SustainedBlackHole.FindSoleBlackHole();
    private const int TargetSize = 384;
    private RenderTarget2D bodyTarget;
    private Asset<Texture2D> noise;
    private bool targetReady;
    private int renderedOwner = -1;
    private int renderedIdentity = -1;

    public override void OnModLoad()
    {
        noise = ModContent.Request<Texture2D>("Fran/Assets/Textures/BlackHole/AccretionNoise", AssetRequestMode.ImmediateLoad);
        RenderTargetManager.RenderTargetUpdateLoopEvent += PrepareBody;
        On_TimeLogger.DetailedDrawTime += DrawBody;
    }

    public override void OnModUnload()
    {
        RenderTargetManager.RenderTargetUpdateLoopEvent -= PrepareBody;
        On_TimeLogger.DetailedDrawTime -= DrawBody;
        RenderTarget2D oldTarget = bodyTarget;
        bodyTarget = null;
        Main.QueueMainThreadAction(() => oldTarget?.Dispose());
        noise = null;
        targetReady = false;
    }

    public override void OnWorldUnload()
    {
        targetReady = false;
        renderedOwner = renderedIdentity = -1;
        DisableLens();
    }

    private static bool Visible(Projectile hole)
    {
        if (Main.gameMenu || hole is null || hole.scale <= 0.003f)
            return false;
        Vector2 position = Vector2.Transform(hole.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
        float margin = SustainedBlackHole.WorldDiameter * hole.scale * Main.GameViewMatrix.Zoom.X;
        return position.X > -margin && position.X < Main.screenWidth + margin &&
            position.Y > -margin && position.Y < Main.screenHeight + margin;
    }

    private static void DisableLens()
    {
        if (!ShaderManager.HasFinishedLoading || !ShaderManager.TryGetFilter("Fran.FranSingularityLens", out ManagedScreenFilter filter))
            return;
        // Opacity alone is not sufficient: filters may linger during their fade.
        filter.TrySetParameter("effectStrength", 0f);
        filter.Deactivate();
    }

    public override void UpdateUI(GameTime gameTime)
    {
        Projectile hole = RenderedHole;
        if (!Visible(hole) || !ShaderManager.HasFinishedLoading)
        {
            DisableLens();
            return;
        }
        if (!ShaderManager.TryGetFilter("Fran.FranSingularityLens", out ManagedScreenFilter filter))
            return;

        Vector2 center = Vector2.Transform(hole.Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
        filter.TrySetParameter("centerUV", center / new Vector2(Main.screenWidth, Main.screenHeight));
        filter.TrySetParameter("radiusPixels", 300f * hole.scale * Main.GameViewMatrix.Zoom.X);
        filter.TrySetParameter("effectStrength", hole.ModProjectile is FranSingularityUltimate ultimate
            ? ultimate.VisualStrength : MathHelper.SmoothStep(0f, 1f, hole.scale));
        filter.Activate();
    }

    private void PrepareBody()
    {
        targetReady = false;
        Projectile hole = RenderedHole;
        if (!Visible(hole) || !ShaderManager.HasFinishedLoading ||
            !ShaderManager.TryGetShader("Fran.FranSingularityBody", out ManagedShader shader))
            return;

        GraphicsDevice device = Main.instance.GraphicsDevice;
        if (bodyTarget is null || bodyTarget.IsDisposed)
            bodyTarget = new RenderTarget2D(device, TargetSize, TargetSize);
        RenderTargetBinding[] previousTargets = device.GetRenderTargets();
        Viewport previousViewport = device.Viewport;
        try
        {
            device.SetRenderTarget(bodyTarget);
            device.Clear(Color.Transparent);
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            try
            {
                shader.TrySetParameter("tilt", hole.rotation);
                shader.SetTexture(noise, 1, SamplerState.LinearWrap);
                shader.Apply();
                Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, TargetSize, TargetSize), Color.White);
            }
            finally
            {
                Main.spriteBatch.End();
            }
            renderedOwner = hole.owner;
            renderedIdentity = hole.identity;
            targetReady = true;
        }
        finally
        {
            device.SetRenderTargets(previousTargets);
            device.Viewport = previousViewport;
        }
    }

    private void DrawBody(On_TimeLogger.orig_DetailedDrawTime orig, int detailedDrawType)
    {
        // The same post-world layer used by WoTG's pet black hole: lens the world
        // first, then overlay the small raymarched body, before the UI is drawn.
        if (detailedDrawType == 36 && targetReady && bodyTarget is not null && !bodyTarget.IsDisposed)
        {
            Projectile hole = RenderedHole;
            if (Visible(hole) && hole.owner == renderedOwner && hole.identity == renderedIdentity)
            {
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                Main.spriteBatch.Draw(bodyTarget, hole.Center - Main.screenPosition, null, Color.White,
                    0f, new Vector2(TargetSize * 0.5f), SustainedBlackHole.WorldDiameter * hole.scale / TargetSize,
                    SpriteEffects.None, 0f);
                Main.spriteBatch.End();
            }
        }
        if (detailedDrawType == 36)
            FranSingularityBurstVisual.DrawAll();
        orig(detailedDrawType);
    }
}
