using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;

// The paired texture lives beside this class as Particles/FranBoltParticle.png.
internal sealed class FranBoltParticle : FranParticle
{
    private readonly Vector2 stretch;
    private readonly int variant;
    private readonly bool flip;

    public FranBoltParticle(Vector2 position, Vector2 velocity, Color color, float scale, Vector2 stretch, int lifetime)
    {
        Position = position;
        Velocity = velocity;
        Color = color;
        Scale = scale;
        this.stretch = stretch;
        Lifetime = lifetime;
        variant = Main.rand.Next(3);
        flip = Main.rand.NextBool();
    }

    protected override void Update()
    {
        Velocity *= 0.94f;
        Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        Scale *= 0.96f;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Texture2D texture = FranParticleAssets.GetParticle(nameof(FranBoltParticle));
        Rectangle frame = texture.Frame(1, 3, 0, variant);
        float fade = System.MathF.Pow(1f - Completion, 2.4f);
        Vector2 size = stretch * Scale;
        SpriteEffects effects = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranBoltParticle), Position,
            Color * (0.72f * fade), Rotation, size, frame, effects);
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranBoltParticle), Position,
            Color.White * (0.68f * fade), Rotation,
            size * 0.52f, frame, effects);
    }
}
