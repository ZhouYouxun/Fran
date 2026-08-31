using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

/// <summary>
/// “辉光球”：Fran自己的双层辉光粒子。运动节奏参考常见的高速收束光球，
/// 使用Fran独立贴图，不依赖或再分发其他模组的视觉资产。
/// </summary>
internal sealed class FranGlowOrbParticle : FranParticle
{
    private readonly float startScale;
    private readonly float endScale;
    private readonly bool whiteCore;
    private readonly bool affectedByGravity;

    public FranGlowOrbParticle(Vector2 position, Vector2 velocity, Color color, float startScale, float endScale,
        int lifetime, bool whiteCore = true, FranParticleLayer layer = FranParticleLayer.AfterProjectiles,
        bool affectedByGravity = false)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.startScale = startScale;
        this.endScale = endScale;
        Lifetime = lifetime;
        this.whiteCore = whiteCore;
        this.affectedByGravity = affectedByGravity;
        Layer = layer;
    }

    protected override void Update()
    {
        Velocity *= 0.95f;
        if (affectedByGravity && Velocity.LengthSquared() < 144f)
        {
            Velocity.X *= 0.94f;
            Velocity.Y += 0.25f;
        }
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 0.72f) *
            MathHelper.Clamp(Completion * 8f, 0f, 1f);
        float size = MathHelper.Lerp(startScale, endScale, 1f - MathF.Pow(1f - Completion, 3f));
        Vector2 motionStretch = new(0.88f, 1f + MathHelper.Clamp(Velocity.Length() * 0.025f, 0f, 0.55f));
        FranParticleAssets.Draw(spriteBatch, "GlowOrbParticle", Position, Color * (0.78f * fade), Rotation,
            motionStretch * size);
        FranParticleAssets.Draw(spriteBatch, "BloomCircle", Position, Color * (0.38f * fade), Rotation,
            motionStretch * size * 0.72f);
        if (whiteCore)
            FranParticleAssets.Draw(spriteBatch, "BloomCircle", Position, Color.White * (0.64f * fade), Rotation,
                motionStretch * size * 0.18f);
    }
}

/// <summary>
/// Fran“光粒子”：一颗沿速度方向被压扁/拉长的三层发光粒子。
/// 用于挥剑释放与爆炸散射，与Dust、辉光球和神秘文字组成不同尺度的视觉层。
/// </summary>
internal sealed class FranLightParticle : FranParticle
{
    private readonly float opacity;
    private readonly float squishStrength;
    private readonly float maxSquish;
    private readonly float hueShift;

    public FranLightParticle(Vector2 position, Vector2 velocity, float scale, Color color, int lifetime,
        float opacity = 1f, float squishStrength = 1f, float maxSquish = 3f, float hueShift = 0f,
        FranParticleLayer layer = FranParticleLayer.AfterProjectiles)
    {
        Position = position;
        Velocity = velocity;
        Scale = scale;
        Color = color;
        Lifetime = lifetime;
        this.opacity = opacity;
        this.squishStrength = squishStrength;
        this.maxSquish = Math.Max(1f, maxSquish);
        this.hueShift = hueShift;
        Layer = layer;
    }

    protected override void Update()
    {
        Velocity *= Completion >= 0.34f ? 0.93f : 1.02f;
        Scale *= 0.95f;
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        if (hueShift != 0f)
        {
            Vector3 hsl = Main.rgbToHsl(Color);
            hsl.X = (hsl.X + hueShift) % 1f;
            if (hsl.X < 0f)
                hsl.X += 1f;
            Color = Main.hslToRgb(hsl);
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = Completion > 0.5f
            ? MathF.Sin(Completion * MathHelper.Pi) * 0.2f + 0.8f
            : MathF.Sin(Completion * MathHelper.Pi);
        fade *= opacity;
        float squish = MathHelper.Clamp(Velocity.Length() / 10f * squishStrength, 1f, maxSquish);
        float widthFactor = Math.Max(0.12f, 1f - squish * 0.30f);
        Vector2 bodySize = new(34f * Scale * widthFactor, 34f * Scale * squish);

        FranParticleAssets.DrawPixels(spriteBatch, "BloomCircle", Position, Color * (fade * 0.52f), Rotation,
            bodySize * 1.8f);
        FranParticleAssets.DrawPixels(spriteBatch, "GlowSpark", Position, Color * (fade * 0.82f), Rotation,
            bodySize * new Vector2(0.72f, 1.16f));
        FranParticleAssets.DrawPixels(spriteBatch, "GlowSpark", Position, Color.White * (fade * 0.92f), Rotation,
            bodySize * new Vector2(0.28f, 0.94f));
    }
}

internal sealed class FranSparkParticle : FranParticle
{
    private readonly Vector2 stretch;
    private readonly float drag;
    private readonly float gravity;
    private readonly bool critical;

    public FranSparkParticle(Vector2 position, Vector2 velocity, Color color, float scale, Vector2 stretch,
        int lifetime, float drag = 0.94f, float gravity = 0f, bool critical = false)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        this.stretch = stretch;
        Lifetime = lifetime;
        this.drag = drag;
        this.gravity = gravity;
        this.critical = critical;
    }

    protected override void Update()
    {
        Velocity *= drag;
        Velocity.Y += gravity;
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.7f);
        float shrink = MathHelper.Lerp(1f, 0.12f, Completion);
        string texture = critical ? "CritSpark" : "GlowSpark";
        // GlowSpark is a 2048 px source. Treat the public scale as visual size, not raw texture scale;
        // otherwise an occasional spark becomes a screen-wide rectangle despite having finite values.
        Vector2 usedSize = new Vector2(
            MathHelper.Clamp(54f * MathF.Abs(stretch.X) * Scale, 4f, 118f),
            MathHelper.Clamp(54f * MathF.Abs(stretch.Y) * Scale, 4f, 118f)) * shrink;
        FranParticleAssets.DrawPixels(spriteBatch, texture, Position, Color * fade, Rotation, usedSize);
        FranParticleAssets.DrawPixels(spriteBatch, texture, Position, Color.White * (0.72f * fade), Rotation,
            usedSize * new Vector2(0.38f, 0.82f));
    }
}

internal sealed class FranLineParticle : FranParticle
{
    private readonly float length;
    private readonly float width;
    private readonly float drag;

    public FranLineParticle(Vector2 position, Vector2 velocity, Color color, float length, float width,
        int lifetime, float drag = 0.92f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.length = length;
        this.width = width;
        Lifetime = lifetime;
        this.drag = drag;
    }

    protected override void Update() => Velocity *= drag;

    public override void Draw(SpriteBatch spriteBatch)
    {
        Vector2 direction = Velocity.SafeNormalize(Rotation.ToRotationVector2());
        float fade = MathF.Pow(1f - Completion, 2f);
        float usedLength = length * MathHelper.Lerp(1f, 0.25f, Completion);
        Vector2 start = Position - direction * usedLength * 0.55f;
        Vector2 end = Position + direction * usedLength * 0.45f;
        FranParticleAssets.DrawLine(spriteBatch, "ThinEndedLine", start, end, Color * (0.55f * fade), width * 2.8f);
        FranParticleAssets.DrawLine(spriteBatch, "ThinEndedLine", start, end, Color.White * (0.84f * fade), width * 0.62f);
    }
}

internal sealed class FranDirectionalRingParticle : FranParticle
{
    private readonly Vector2 startScale;
    private readonly Vector2 endScale;
    private readonly float spin;

    public FranDirectionalRingParticle(Vector2 position, Vector2 velocity, Color color, Vector2 startScale,
        Vector2 endScale, float rotation, int lifetime, float spin = 0f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.startScale = startScale;
        this.endScale = endScale;
        Rotation = rotation;
        Lifetime = lifetime;
        this.spin = spin;
    }

    protected override void Update()
    {
        Velocity *= 0.92f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Sin(Completion * MathHelper.Pi);
        Vector2 size = Vector2.Lerp(startScale, endScale, 1f - MathF.Pow(1f - Completion, 3f));
        FranParticleAssets.Draw(spriteBatch, "BloomRingAngled", Position, Color * (0.70f * fade), Rotation, size);
        FranParticleAssets.Draw(spriteBatch, "BloomRingAngled", Position, Color.White * (0.38f * fade), Rotation,
            size * new Vector2(0.72f, 0.58f));
    }
}

internal sealed class FranHeavySmokeParticle : FranParticle
{
    private readonly float opacity;
    private readonly float spin;
    private readonly int variant;
    private readonly bool glowing;
    public override bool Additive => glowing;

    public FranHeavySmokeParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime,
        float opacity, bool glowing = false, FranParticleLayer layer = FranParticleLayer.BehindProjectiles)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        Lifetime = lifetime;
        this.opacity = opacity;
        this.glowing = glowing;
        Layer = layer;
        variant = Main.rand.Next(7);
        spin = Main.rand.NextFloat(-0.035f, 0.035f);
        Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
    }

    protected override void Update()
    {
        Velocity *= 0.87f;
        Rotation += spin;
        Scale *= Time < Lifetime * 0.2f ? 1.018f : 0.982f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D texture = FranParticleAssets.Get("HeavySmoke");
        int frameCount = Math.Max(1, texture.Height / 80);
        int animation = Math.Min(frameCount - 1, (int)(Completion * frameCount));
        Rectangle frame = new(80 * variant, 80 * animation, 80, 80);
        float fade = Utils.GetLerpValue(1f, 0.74f, Completion, true);
        Color usedColor = Color * (opacity * fade);
        if (!glowing)
            usedColor = usedColor.MultiplyRGBA(Lighting.GetColor((Position / 16f).ToPoint()));
        FranParticleAssets.Draw(spriteBatch, "HeavySmoke", Position, usedColor, Rotation, Vector2.One * Scale, frame);
    }
}

internal sealed class FranStarMoteParticle : FranParticle
{
    private readonly Color bloomColor;
    private readonly float spin;
    private readonly int targetIndex;
    private readonly float homingStrength;

    public FranStarMoteParticle(Vector2 position, Vector2 velocity, Color color, Color bloomColor, float scale,
        int lifetime, int targetIndex = -1, float homingStrength = 0f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        this.bloomColor = bloomColor;
        Scale = scale;
        Lifetime = lifetime;
        this.targetIndex = targetIndex;
        this.homingStrength = homingStrength;
        spin = Main.rand.NextFloat(-0.18f, 0.18f);
        Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
    }

    protected override void Update()
    {
        if (targetIndex >= 0 && Main.npc.IndexInRange(targetIndex) && Main.npc[targetIndex].active)
        {
            Vector2 desired = (Main.npc[targetIndex].Center - Position).SafeNormalize(Vector2.Zero) * Math.Max(2f, Velocity.Length());
            Velocity = Vector2.Lerp(Velocity, desired, homingStrength);
        }
        Velocity *= 0.985f;
        Rotation += spin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float opacity = MathF.Sin(Completion * MathHelper.Pi);
        FranParticleAssets.Draw(spriteBatch, "BloomCircle", Position, bloomColor * (0.46f * opacity), 0f, Vector2.One * Scale * 0.52f);
        FranParticleAssets.Draw(spriteBatch, "Sparkle", Position, Color * (0.55f * opacity), Rotation + MathHelper.PiOver4, Vector2.One * Scale * 0.74f);
        FranParticleAssets.Draw(spriteBatch, "Sparkle", Position, Color.White * (0.92f * opacity), Rotation, Vector2.One * Scale * 0.43f);
    }
}

internal sealed class FranBladeChipParticle : FranParticle
{
    private readonly float gravity;
    private readonly bool electric;

    public FranBladeChipParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime,
        float gravity = 0.08f, bool electric = false)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        Lifetime = lifetime;
        this.gravity = gravity;
        this.electric = electric;
        Rotation = velocity.ToRotation();
    }

    protected override void Update()
    {
        Velocity *= 0.965f;
        Velocity.Y += gravity;
        Rotation += Math.Sign(Velocity.X) * 0.13f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.4f);
        string texture = electric ? "ElectricSpark" : "CritSpark";
        FranParticleAssets.Draw(spriteBatch, texture, Position, Color * fade, Rotation,
            new Vector2(0.55f, 1.4f) * Scale * MathHelper.Lerp(1f, 0.2f, Completion));
    }
}

internal sealed class FranHeartParticle : FranParticle
{
    private readonly float angularVelocity;
    private readonly float lift;
    private readonly bool large;

    public FranHeartParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime,
        float angularVelocity = 0f, bool large = false)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        Lifetime = lifetime;
        this.angularVelocity = angularVelocity;
        this.large = large;
        lift = Main.rand.NextFloat(0.015f, 0.045f);
    }

    protected override void Update()
    {
        Velocity = Velocity.RotatedBy(angularVelocity);
        Velocity.Y -= lift;
        Velocity *= 0.975f;
        Rotation = MathF.Sin(Time * 0.13f) * 0.12f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Sin(Completion * MathHelper.Pi);
        float pulse = 0.88f + MathF.Sin(Time * 0.38f) * 0.12f;
        string texture = large ? "BigHeart" : "HeartParticle";
        FranParticleAssets.Draw(spriteBatch, "BloomCircle", Position, Color * (0.28f * fade), 0f, Vector2.One * Scale * 0.44f);
        FranParticleAssets.Draw(spriteBatch, texture, Position, Color * (0.78f * fade), Rotation, Vector2.One * Scale * pulse);
        FranParticleAssets.Draw(spriteBatch, texture, Position, Color.White * (0.34f * fade), Rotation, Vector2.One * Scale * pulse * 0.58f);
    }
}

internal sealed class FranWaterParticle : FranParticle
{
    private readonly float gravity;
    public override bool Additive => false;

    public FranWaterParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime, float gravity = 0.14f)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        Lifetime = lifetime;
        this.gravity = gravity;
    }

    protected override void Update()
    {
        Velocity *= 0.96f;
        Velocity.Y += gravity;
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        Scale *= 0.965f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Pow(1f - Completion, 1.5f);
        FranParticleAssets.Draw(spriteBatch, "WaterFlavored", Position, Color * fade, Rotation, new Vector2(0.68f, 1.2f) * Scale);
        FranParticleAssets.Draw(spriteBatch, "WaterFlavored", Position, Color.White * (0.34f * fade), Rotation, new Vector2(0.28f, 0.8f) * Scale);
    }
}

internal sealed class FranToxicMoteParticle : FranParticle
{
    private readonly float wobbleSeed;
    public override bool Additive => false;

    public FranToxicMoteParticle(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        Lifetime = lifetime;
        wobbleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
    }

    protected override void Update()
    {
        Velocity.X += MathF.Sin(Time * 0.19f + wobbleSeed) * 0.045f;
        Velocity.Y -= 0.018f;
        Velocity *= 0.985f;
        Rotation += 0.025f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Sin(Completion * MathHelper.Pi);
        float pulse = 0.82f + MathF.Sin(Time * 0.3f + wobbleSeed) * 0.14f;
        FranParticleAssets.Draw(spriteBatch, "WaterGlob", Position, Color * (0.78f * fade), Rotation, Vector2.One * Scale * pulse);
        FranParticleAssets.Draw(spriteBatch, "BloomCircle", Position, new Color(160, 255, 75) * (0.22f * fade), 0f, Vector2.One * Scale * 0.32f);
    }
}
