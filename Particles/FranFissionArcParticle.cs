using FranMod.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FranMod.Particles;

internal sealed class FranFissionArcParticle : FranParticle
{
    private readonly FranFissionArc arc;
    private readonly float growthFrames;
    private readonly float opacity;
    public override bool Important => true;

    public FranFissionArcParticle(Vector2 start, Vector2 end, Color color, float width,
        int seed, int lifetime = 16, float growthFrames = 3f, int depth = 2, float opacity = 1f)
    {
        Position = start;
        Color = color;
        Lifetime = lifetime;
        this.growthFrames = growthFrames;
        this.opacity = opacity;
        arc = new FranFissionArc(end - start, seed, width, depth);
    }

    public override void Draw(SpriteBatch spriteBatch) =>
        arc.Draw(spriteBatch, Position, Color, Time, Lifetime, growthFrames, opacity);
}
