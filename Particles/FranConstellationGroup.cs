using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace FranMod.Particles;


// A small shared state object lets a burst of motes draw real constellation links without a
// process-wide registry. Once the particles expire the complete group can be collected normally.
internal sealed class FranConstellationGroup
{
    public readonly Vector2[] Positions;

    public FranConstellationGroup(int count)
    {
        Positions = new Vector2[Math.Max(2, count)];
    }
}
