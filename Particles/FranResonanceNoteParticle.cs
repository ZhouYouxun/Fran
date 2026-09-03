using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace FranMod.Particles;


// A rhythm mote used by Blue Resonance; it contracts toward the delayed repeat slash.
internal sealed class FranResonanceNoteParticle : FranParticle
{
    private readonly float orbit;
    private readonly float startRadius;
    private readonly Vector2 anchor;

    public FranResonanceNoteParticle(Vector2 position, Color color, float radius, int lifetime, float phase)
    {
        Position = position;
        anchor = position;
        Color = color;
        startRadius = radius;
        Lifetime = lifetime;
        orbit = phase;
    }

    protected override void Update()
    {
        float radius = startRadius * MathF.Pow(1f - Completion, 1.55f);
        float angle = orbit + Time * 0.24f;
        Position = anchor + angle.ToRotationVector2() * radius;
        Rotation = angle + MathHelper.PiOver2;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        float fade = MathF.Sin(Completion * MathHelper.Pi);
        FranParticleAssets.DrawParticle(spriteBatch, nameof(FranResonanceNoteParticle), Position,
            Color * (0.64f * fade), Rotation, Vector2.One * (0.16f + (1f - Completion) * 0.10f));
        FranParticleAssets.Draw(spriteBatch, "BloomCircle", Position,
            Color.White * (0.34f * fade), 0f, Vector2.One * 0.08f);
    }
}
