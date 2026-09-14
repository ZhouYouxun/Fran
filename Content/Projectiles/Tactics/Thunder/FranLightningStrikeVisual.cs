using System;
using System.IO;
using FranMod.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// One shared, visual-only projectile for the ultimate and hammer. RGB is a numeric 24-bit ai value,
// not float bit reinterpretation; endpoints, seed, color, width and stagger survive projectile sync.
public sealed class FranLightningStrikeVisual : ModProjectile
{
    private const int Lifetime = 45;
    private static BasicEffect ribbonEffect;
    private FranLightningStrikeGeometry geometry;
    private VertexPositionColorTexture[] vertices;
    private float brightnessMultiplier = 1f;
    private float Age => Lifetime - Projectile.timeLeft;
    private Color Tint => new((byte)((int)Projectile.ai[1] >> 16),
        (byte)((int)Projectile.ai[1] >> 8), (byte)(int)Projectile.ai[1]);

    public override string Texture => "Fran/Textures/Particles/LightningStrikeGlow";

    public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2200;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 2;
        Projectile.friendly = Projectile.hostile = false;
        Projectile.penetrate = -1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    // Both callers spawn once on the owning client (or in single player), then normal sync relays it.
    internal static void Spawn(IEntitySource source, Vector2 start, Vector2 end, int owner,
        Color color, float halfWidth = 12f, int delay = 0, float brightness = 1f)
    {
        float length = Vector2.Distance(start, end);
        if (!float.IsFinite(length) || length < 8f || length > 1600f)
            return;
        int rgb = color.R << 16 | color.G << 8 | color.B;
        int index = Projectile.NewProjectile(source, start, end - start,
            ModContent.ProjectileType<FranLightningStrikeVisual>(), 0, 0f, owner,
            Main.rand.Next(1, 1 << 24), rgb,
            Math.Clamp(delay, 0, 12) * 32 + MathHelper.Clamp(halfWidth, 2f, 24f));
        if (Main.projectile.IndexInRange(index))
        {
            if (Main.projectile[index].ModProjectile is FranLightningStrikeVisual visual)
                visual.brightnessMultiplier = ClampBrightness(brightness);
            Main.projectile[index].netUpdate = true;
        }
    }

    public override void OnSpawn(IEntitySource source) =>
        Projectile.timeLeft = Lifetime + Math.Clamp((int)Projectile.ai[2] / 32, 0, 12);

    // Remote projectile creation does not call OnSpawn. Carry the remaining lifetime explicitly so
    // staggered hammer bolts and late snapshots keep their reveal timing on every client.
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write((ushort)Projectile.timeLeft);
        writer.Write(brightnessMultiplier);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Projectile.timeLeft = Math.Clamp((int)reader.ReadUInt16(), 1, Lifetime + 12);
        brightnessMultiplier = ClampBrightness(reader.ReadSingle());
    }

    private static float ClampBrightness(float value) =>
        float.IsFinite(value) ? MathHelper.Clamp(value, 0.1f, 3f) : 1f;

    public override void AI()
    {
        if (!float.IsFinite(Projectile.velocity.X) || !float.IsFinite(Projectile.velocity.Y) ||
            Projectile.velocity.LengthSquared() > 1600f * 1600f)
        {
            Projectile.Kill();
            return;
        }
        if (Main.dedServ || Age < 0f)
            return;
        EnsureGeometry();
        if (geometry.Bolts.Count == 0)
            return;
        Vector2[] main = geometry.Bolts[^1].Points;
        float brightness = 1f - MathHelper.Clamp(Age / 24f, 0f, 1f);
        for (int i = 0; i < main.Length; i += 16)
            Lighting.AddLight(Projectile.Center + main[i], Tint.ToVector3() * brightness * 0.65f * brightnessMultiplier);
    }

    private void EnsureGeometry()
    {
        if (geometry is not null)
            return;
        geometry = new FranLightningStrikeGeometry((uint)Projectile.ai[0], Vector2.Zero, Projectile.velocity);
        int count = 0;
        foreach (FranLightningStrikeGeometry.Bolt bolt in geometry.Bolts)
            count += (bolt.Points.Length - 1) * 6;
        vertices = new VertexPositionColorTexture[count];
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (Age < 0f || Main.dedServ)
            return false;
        EnsureGeometry();
        if (vertices.Length == 0)
            return false;

        // Flush sprites before drawing continuous triangle ribbons. This uses 1.4.4.9's BasicEffect,
        // not the unavailable 1.4.5 StormLightning shader or any newer game assembly.
        Main.spriteBatch.End();
        GraphicsDevice device = Main.instance.GraphicsDevice;
        BlendState blend = device.BlendState;
        DepthStencilState depth = device.DepthStencilState;
        RasterizerState rasterizer = device.RasterizerState;
        SamplerState sampler = device.SamplerStates[0];
        try
        {
            ribbonEffect ??= new BasicEffect(device) { VertexColorEnabled = true, TextureEnabled = true };
            ribbonEffect.World = Matrix.Identity;
            ribbonEffect.View = Main.GameViewMatrix.TransformationMatrix;
            ribbonEffect.Projection = Matrix.CreateOrthographicOffCenter(0f, device.Viewport.Width,
                device.Viewport.Height, 0f, -1f, 1f);
            ribbonEffect.Texture = ModContent.Request<Texture2D>(Texture).Value;
            device.BlendState = BlendState.Additive;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            device.SamplerStates[0] = SamplerState.LinearClamp;
            foreach (EffectPass pass in ribbonEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                DrawRibbon(device, Tint, 1.7f, 0.18f);
                DrawRibbon(device, Tint, 1f, 0.95f);
                // A narrow colored core preserves deep purple instead of whitening the entire bolt.
                DrawRibbon(device, Color.Lerp(Tint, Color.White, 0.28f), 0.22f, 0.8f);
            }
        }
        finally
        {
            device.BlendState = blend;
            device.DepthStencilState = depth;
            device.RasterizerState = rasterizer;
            device.SamplerStates[0] = sampler;
            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
        return false;
    }

    private void DrawRibbon(GraphicsDevice device, Color tint, float widthScale, float strength)
    {
        float life = MathHelper.Clamp(Age / Lifetime, 0f, 1f);
        float opacity = Remap(life, 0.1f, 0.25f, 0.5f, 1f) * Remap(life, 0.25f, 0.75f, 1f, 0f);
        int cursor = 0;
        foreach (FranLightningStrikeGeometry.Bolt bolt in geometry.Bolts)
        {
            float branchStrength = bolt.Depth == 0 ? 1f : 0.5f * MathF.Pow(0.8f, bolt.Depth - 1);
            VertexPositionColorTexture left = default, right = default;
            for (int i = 0; i < bolt.Points.Length; i++)
            {
                float along = i / (bolt.Points.Length - 1f);
                float progress = MathHelper.Lerp(bolt.StartProgress, bolt.EndProgress, along);
                float waveIn = Remap(life, 0f, 0.15f, 0f, 1f);
                float waveOut = Remap(life, 0.25f, 1f, 0f, 1f);
                float wave = Wave(progress, waveIn, 0f, 1f) * Wave(progress, waveOut, 1f, 0f);
                float width = Projectile.ai[2] % 32f * widthScale *
                    Remap(progress, 0.5f, 1f, 1f, 0.5f) * Remap(life, 0.5f, 1f, 1f, 0.5f);
                if (bolt.EndProgress < 1f)
                    width *= Remap(bolt.EndProgress - progress, 0.1f, 0f, 1f, 0f);
                Vector2 position = Projectile.Center + bolt.Points[i] - Main.screenPosition;
                Vector2 normal = bolt.Normals[i] * width;
                Color color = tint * (wave * opacity * branchStrength * strength * brightnessMultiplier);
                color.A = 255; // RGB already contains opacity; avoid multiplying it a second time.
                VertexPositionColorTexture nextLeft = new(new Vector3(position + normal, 0f), color, new Vector2(along, 0f));
                VertexPositionColorTexture nextRight = new(new Vector3(position - normal, 0f), color, new Vector2(along, 1f));
                if (i > 0)
                {
                    vertices[cursor++] = left;
                    vertices[cursor++] = right;
                    vertices[cursor++] = nextLeft;
                    vertices[cursor++] = right;
                    vertices[cursor++] = nextRight;
                    vertices[cursor++] = nextLeft;
                }
                left = nextLeft;
                right = nextRight;
            }
        }
        device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, cursor / 3);
    }

    private static float Wave(float along, float progress, float from, float to) =>
        Remap(along, MathHelper.Lerp(-0.5f, 1f, progress), MathHelper.Lerp(0f, 1.5f, progress), to, from);

    private static float Remap(float value, float from, float to, float low, float high) =>
        FranLightningStrikeGeometry.Remap(value, from, to, low, high);

    public override void Unload()
    {
        BasicEffect effect = ribbonEffect;
        ribbonEffect = null;
        if (effect is not null)
            Main.QueueMainThreadAction(effect.Dispose);
    }
}
