using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

// Local adapters of the six Calamity particles used by YC_AuricJudgementWave.
// Preserve their native texture scale/curves; no dependency on Calamity's particle manager.
namespace FranMod.Particles.Vigil
{
    internal static class GeneralParticleHandler
    {
        internal static void SpawnParticle(FranParticle particle) => FranParticleSystem.Spawn(particle, false);
    }

    internal abstract class WaveParticle : FranParticle
    {
        protected Color InitialColor;
        protected void DrawTexture(SpriteBatch batch, string name, Color color, Vector2 scale, float? angle = null)
        {
            Texture2D texture = FranParticleAssets.Get(name);
            batch.Draw(texture, Position - Main.screenPosition, null, color, angle ?? Rotation,
                texture.Size() * .5f, scale, SpriteEffects.None, 0);
        }
    }

    internal sealed class GlowOrbParticle : WaveParticle
    {
        private float fade = 1;
        internal GlowOrbParticle(Vector2 position, Vector2 velocity, bool gravity, int lifetime, float scale, Color color)
        { Position = position; Velocity = velocity; Lifetime = lifetime; Scale = scale; InitialColor = Color = color; }
        protected override void Update()
        {
            fade -= .1f; Scale *= .93f; Velocity *= .95f;
            Color = Color.Lerp(InitialColor, InitialColor * .2f, MathF.Pow(Completion, 3));
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }
        public override void Draw(SpriteBatch spriteBatch)
        {
            DrawTexture(spriteBatch, "VigilGlowOrb", Color, Vector2.One * Scale);
            DrawTexture(spriteBatch, "VigilGlowOrb", Color.White * Math.Max(0, fade), Vector2.One * Scale * .5f);
        }
    }

    internal class SparkParticle : WaveParticle
    {
        protected Vector2 stretch = new(.5f, 1.6f);
        protected virtual string Image => "VigilSpark";
        internal SparkParticle(Vector2 position, Vector2 velocity, bool gravity, int lifetime, float scale, Color color)
        { Position = position; Velocity = velocity; Lifetime = lifetime; Scale = scale; InitialColor = Color = color; }
        protected override void Update()
        {
            Scale *= .95f; Velocity *= .95f;
            Color = Color.Lerp(InitialColor, Color.Transparent, MathF.Pow(Completion, 3));
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }
        public override void Draw(SpriteBatch spriteBatch)
        {
            DrawTexture(spriteBatch, Image, Color, stretch * Scale);
            DrawTexture(spriteBatch, Image, Color, stretch * Scale * new Vector2(.45f, 1));
        }
    }

    internal sealed class CustomSpark : SparkParticle
    {
        private readonly float shrink;
        protected override string Image => "CL_BloomCircle";
        internal CustomSpark(Vector2 position, Vector2 velocity, string texture, bool gravity, int lifetime,
            float scale, Color color, Vector2 stretch, float shrinkSpeed) : base(position, velocity, gravity, lifetime, scale, color)
        { this.stretch = stretch; shrink = shrinkSpeed; }
        protected override void Update()
        {
            base.Update(); stretch *= new Vector2(1 - .2f * shrink, 1 + .2f * shrink);
        }
        public override void Draw(SpriteBatch spriteBatch) => DrawTexture(spriteBatch, Image,
            Color.Lerp(Color, Color.Transparent, MathF.Pow(Completion, 3)), stretch * Scale);
    }

    internal sealed class StrongBloom : WaveParticle
    {
        internal StrongBloom(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime)
        { Position = position; Velocity = velocity; InitialColor = color; Scale = scale; Lifetime = lifetime; Rotation = Main.rand.NextFloat(MathHelper.TwoPi); }
        protected override void Update()
        {
            Color = InitialColor * MathF.Sin(Completion * MathHelper.Pi);
            Velocity *= .95f; Lighting.AddLight(Position, Color.ToVector3());
        }
        public override void Draw(SpriteBatch spriteBatch) => DrawTexture(spriteBatch, "CL_BloomCircle", Color, Vector2.One * Scale);
    }

    internal sealed class DirectionalPulseRing : WaveParticle
    {
        private readonly float start, end;
        private readonly Vector2 squish;
        private float opacity;
        internal DirectionalPulseRing(Vector2 position, Vector2 velocity, Color color, Vector2 squish,
            float rotation, float start, float end, int lifetime)
        {
            Position = position; Velocity = velocity; InitialColor = color; this.squish = squish;
            Rotation = rotation; this.start = start; this.end = end; Scale = start; Lifetime = lifetime;
        }
        protected override void Update()
        {
            Scale = MathHelper.Lerp(start, end, 1 - MathF.Pow(1 - Completion, 4));
            opacity = MathF.Cos(Completion * MathHelper.PiOver2);
            Color = InitialColor * opacity; Velocity *= .95f; Lighting.AddLight(Position, Color.ToVector3());
        }
        public override void Draw(SpriteBatch spriteBatch) => DrawTexture(spriteBatch, "VigilRing", Color * opacity, squish * Scale);
    }

    internal sealed class GenericSparkle : WaveParticle
    {
        private readonly Color bloom;
        private readonly float spin, bloomScale;
        private float opacity;
        internal GenericSparkle(Vector2 position, Vector2 velocity, Color color, Color bloom, float scale,
            int lifetime, float rotationSpeed, float bloomScale)
        {
            Position = position; Velocity = velocity; Color = color; this.bloom = bloom; Scale = scale;
            Lifetime = lifetime; spin = rotationSpeed; this.bloomScale = bloomScale; Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }
        protected override void Update()
        {
            opacity = MathF.Sin(Completion * MathHelper.Pi); Velocity *= .95f;
            Rotation += spin * (Velocity.X > 0 ? 1 : -1); Lighting.AddLight(Position, bloom.ToVector3() * opacity);
        }
        public override void Draw(SpriteBatch spriteBatch)
        {
            float ratio = FranParticleAssets.Get("VigilSparkle").Height / (float)FranParticleAssets.Get("CL_BloomCircle").Height;
            DrawTexture(spriteBatch, "CL_BloomCircle", bloom * opacity * .5f, Vector2.One * Scale * bloomScale * ratio, 0);
            DrawTexture(spriteBatch, "VigilSparkle", Color * opacity * .5f, Vector2.One * Scale * .75f, Rotation + MathHelper.PiOver4);
            DrawTexture(spriteBatch, "VigilSparkle", Color * opacity, Vector2.One * Scale);
        }
    }

    public sealed class SquashDust : ModDust
    {
        public override string Texture => "Terraria/Images/Projectile_0";
        public override void OnSpawn(Dust dust) => dust.scale *= Main.rand.NextFloat(.8f, 1f);
        public override bool Update(Dust dust)
        {
            dust.rotation = dust.velocity.ToRotation() + MathHelper.PiOver2;
            dust.velocity *= .96f;
            dust.scale -= .045f * (dust.fadeIn + 1);
            Lighting.AddLight(dust.position, dust.color.ToVector3() * MathHelper.Clamp(dust.scale * .8f, 0, 1));
            if (dust.scale <= 0) dust.active = false;
            dust.position += dust.velocity;
            return false;
        }
        public override bool PreDraw(Dust dust)
        {
            Vector2 squash = new(Utils.Remap(dust.velocity.Length(), 2, 7, 1, .5f), Utils.Remap(dust.velocity.Length(), 2, 7, 1, 2.5f));
            Texture2D bloom = FranParticleAssets.Get("CL_BloomCircle"), solid = FranParticleAssets.Get("VigilSolidCircle");
            Color color = dust.color with { A = 0 };
            Main.spriteBatch.Draw(bloom, dust.position - Main.screenPosition, null, color, dust.rotation, bloom.Size() * .5f, squash * dust.scale * .1f, 0, 0);
            Main.spriteBatch.Draw(bloom, dust.position - Main.screenPosition, null, color * .85f, dust.rotation, bloom.Size() * .5f, squash * dust.scale * .04f, 0, 0);
            Main.spriteBatch.Draw(solid, dust.position - Main.screenPosition, null, Color.Lerp(dust.color, Color.White, .3f) with { A = 0 },
                dust.rotation, solid.Size() * .5f, squash * dust.scale * .075f, 0, 0);
            return false;
        }
    }
}

namespace FranMod.Particles
{
    internal static class FranVigilWaveParticles
    {
        internal static void Ring(Vector2 position, Color color, float scale, int lifetime) =>
            FranParticleSystem.Spawn(new Vigil.DirectionalPulseRing(position, Vector2.Zero, color, Vector2.One,
                Main.rand.NextFloat(MathHelper.TwoPi), .08f, scale, lifetime), false);
    }
}
