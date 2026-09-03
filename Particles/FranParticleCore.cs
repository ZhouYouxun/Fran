using System;
using System.Collections.Generic;
using FranMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Particles;

internal enum FranParticleLayer : byte
{
    BehindProjectiles,
    AfterProjectiles
}

internal abstract class FranParticle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Color Color;
    public float Rotation;
    public float Scale = 1f;
    public int Time;
    public int Lifetime = 20;
    public FranParticleLayer Layer = FranParticleLayer.AfterProjectiles;
    public virtual bool Additive => true;
    public virtual bool Important => false;
    public float Completion => MathHelper.Clamp(Time / (float)Math.Max(1, Lifetime), 0f, 1f);

    public bool Step()
    {
        Position += Velocity;
        Time++;
        Update();
        return Time < Lifetime && float.IsFinite(Position.X) && float.IsFinite(Position.Y) &&
            float.IsFinite(Velocity.X) && float.IsFinite(Velocity.Y) &&
            float.IsFinite(Rotation) && float.IsFinite(Scale);
    }

    protected virtual void Update() { }
    public abstract void Draw(SpriteBatch spriteBatch);
}

internal static class FranParticleAssets
{
    // Fran/Particles 是粒子运行时的唯一资源入口。Textures/Particles 可以保留共享材质副本，
    // 供弹幕 PreDraw 使用，但粒子类自身不会跨目录请求贴图。
    private const string Root = "Fran/Particles/";
    private const string ParticleRoot = Root;
    private const float MaximumRenderedDimension = 256f;

    public static Texture2D Get(string name) => ModContent.Request<Texture2D>(Root + name).Value;
    public static Texture2D GetParticle(string className) =>
        ModContent.Request<Texture2D>(ParticleRoot + className).Value;

    public static void Draw(SpriteBatch spriteBatch, string textureName, Vector2 worldPosition, Color color,
        float rotation, Vector2 scale, Rectangle? frame = null, SpriteEffects effects = SpriteEffects.None)
    {
        if (!float.IsFinite(worldPosition.X) || !float.IsFinite(worldPosition.Y) ||
            !float.IsFinite(rotation) || !float.IsFinite(scale.X) || !float.IsFinite(scale.Y) ||
            MathF.Abs(scale.X) > 96f || MathF.Abs(scale.Y) > 96f)
            return;

        Texture2D texture = Get(textureName);
        Rectangle source = frame ?? texture.Bounds;
        float renderedWidth = source.Width * MathF.Abs(scale.X);
        float renderedHeight = source.Height * MathF.Abs(scale.Y);
        if (!float.IsFinite(renderedWidth) || !float.IsFinite(renderedHeight) ||
            renderedWidth > MaximumRenderedDimension || renderedHeight > MaximumRenderedDimension)
            return;
        spriteBatch.Draw(texture, worldPosition - Main.screenPosition, source, color, rotation,
            source.Size() * 0.5f, scale, effects, 0f);
    }

    public static void DrawPixels(SpriteBatch spriteBatch, string textureName, Vector2 worldPosition, Color color,
        float rotation, Vector2 desiredSize, Rectangle? frame = null, SpriteEffects effects = SpriteEffects.None)
    {
        Texture2D texture = Get(textureName);
        Rectangle source = frame ?? texture.Bounds;
        desiredSize = new Vector2(MathHelper.Clamp(MathF.Abs(desiredSize.X), 0.5f, MaximumRenderedDimension),
            MathHelper.Clamp(MathF.Abs(desiredSize.Y), 0.5f, MaximumRenderedDimension));
        Draw(spriteBatch, textureName, worldPosition, color, rotation,
            new Vector2(desiredSize.X / Math.Max(1f, source.Width), desiredSize.Y / Math.Max(1f, source.Height)),
            frame, effects);
    }

    public static void DrawParticle(SpriteBatch spriteBatch, string className, Vector2 worldPosition, Color color,
        float rotation, Vector2 scale, Rectangle? frame = null, SpriteEffects effects = SpriteEffects.None)
    {
        if (!float.IsFinite(worldPosition.X) || !float.IsFinite(worldPosition.Y) ||
            !float.IsFinite(rotation) || !float.IsFinite(scale.X) || !float.IsFinite(scale.Y) ||
            MathF.Abs(scale.X) > 96f || MathF.Abs(scale.Y) > 96f)
            return;

        Texture2D texture = GetParticle(className);
        Rectangle source = frame ?? texture.Bounds;
        float renderedWidth = source.Width * MathF.Abs(scale.X);
        float renderedHeight = source.Height * MathF.Abs(scale.Y);
        if (!float.IsFinite(renderedWidth) || !float.IsFinite(renderedHeight) ||
            renderedWidth > MaximumRenderedDimension || renderedHeight > MaximumRenderedDimension)
            return;
        spriteBatch.Draw(texture, worldPosition - Main.screenPosition, source, color, rotation,
            source.Size() * 0.5f, scale, effects, 0f);
    }

    public static void DrawParticlePixels(SpriteBatch spriteBatch, string className, Vector2 worldPosition,
        Color color, float rotation, Vector2 desiredSize, Rectangle? frame = null,
        SpriteEffects effects = SpriteEffects.None)
    {
        Texture2D texture = GetParticle(className);
        Rectangle source = frame ?? texture.Bounds;
        desiredSize = new Vector2(MathHelper.Clamp(MathF.Abs(desiredSize.X), 0.5f, MaximumRenderedDimension),
            MathHelper.Clamp(MathF.Abs(desiredSize.Y), 0.5f, MaximumRenderedDimension));
        DrawParticle(spriteBatch, className, worldPosition, color, rotation,
            new Vector2(desiredSize.X / Math.Max(1f, source.Width),
                desiredSize.Y / Math.Max(1f, source.Height)), frame, effects);
    }

    public static void DrawLine(SpriteBatch spriteBatch, string textureName, Vector2 start, Vector2 end,
        Color color, float width)
    {
        Vector2 edge = end - start;
        float lengthSquared = edge.LengthSquared();
        if (!float.IsFinite(start.X) || !float.IsFinite(start.Y) ||
            !float.IsFinite(end.X) || !float.IsFinite(end.Y) || !float.IsFinite(width) ||
            !float.IsFinite(lengthSquared) || lengthSquared <= 0.001f || lengthSquared > 256f * 256f)
            return;

        Texture2D texture = Get(textureName);
        float length = MathF.Sqrt(lengthSquared);
        Vector2 scale = new(MathHelper.Clamp(width, 0.5f, 64f) / Math.Max(1f, texture.Width),
            length / Math.Max(1f, texture.Height));
        spriteBatch.Draw(texture, (start + end) * 0.5f - Main.screenPosition, null, color,
            edge.ToRotation() - MathHelper.PiOver2, texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
    }
}

[Autoload(Side = ModSide.Client)]
internal sealed class FranParticleSystem : ModSystem
{
    private const int ParticleLimit = 2200;
    private static readonly List<FranParticle> Active = new();
    private static readonly List<FranParticle> Pending = new();

    public override void Load() => On_Main.DrawProjectiles += DrawProjectiles;

    public override void Unload()
    {
        On_Main.DrawProjectiles -= DrawProjectiles;
        Active.Clear();
        Pending.Clear();
        FranVanillaDustEffects.ResetBudget();
    }

    public override void OnWorldUnload()
    {
        Active.Clear();
        Pending.Clear();
        FranVanillaDustEffects.ResetBudget();
    }

    public override void PostUpdateEverything()
    {
        if (Pending.Count > 0)
        {
            Active.AddRange(Pending);
            Pending.Clear();
        }

        for (int i = Active.Count - 1; i >= 0; i--)
        {
            if (!Active[i].Step())
                Active.RemoveAt(i);
        }
    }

    internal static void Spawn(FranParticle particle)
    {
        if (Main.dedServ || Main.gameMenu || particle is null || particle.Lifetime <= 0)
            return;

        int count = Active.Count + Pending.Count;
        if (count >= ParticleLimit && !particle.Important)
            return;

        if (count >= ParticleLimit + 120)
            return;

        Pending.Add(particle);
        FranVanillaDustEffects.RegisterCustomParticle(particle);
    }

    internal static int FreeSlots => Math.Max(0, ParticleLimit - Active.Count - Pending.Count);

    private static void DrawProjectiles(On_Main.orig_DrawProjectiles orig, Main self)
    {
        DrawLayer(FranParticleLayer.BehindProjectiles, false, BlendState.AlphaBlend);
        DrawLayer(FranParticleLayer.BehindProjectiles, true, BlendState.Additive);
        orig(self);
        DrawLayer(FranParticleLayer.AfterProjectiles, false, BlendState.AlphaBlend);
        DrawLayer(FranParticleLayer.AfterProjectiles, true, BlendState.Additive);
    }

    private static void DrawLayer(FranParticleLayer layer, bool additive, BlendState blendState)
    {
        bool any = false;
        foreach (FranParticle particle in Active)
        {
            if (particle.Layer == layer && particle.Additive == additive)
            {
                any = true;
                break;
            }
        }
        if (!any)
            return;

        Main.spriteBatch.Begin(SpriteSortMode.Deferred, blendState, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        foreach (FranParticle particle in Active)
        {
            if (particle.Layer == layer && particle.Additive == additive)
                particle.Draw(Main.spriteBatch);
        }
        Main.spriteBatch.End();
    }
}
