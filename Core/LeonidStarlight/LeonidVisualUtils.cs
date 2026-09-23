using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace FranMod.Core.LeonidStarlight
{
    public static class LeonidVisualUtils
    {
        public static readonly Color StratusBlue = new(82, 216, 255);
        public static readonly Color DeepStratusBlue = new(18, 42, 168);
        public static readonly Color MoonViolet = new(172, 150, 255);
        public static readonly Color MoonWhite = new(224, 240, 255);
        public static readonly Color StarGold = new(255, 226, 104);
        public static readonly Color NightSkyBlue = new(12, 38, 128);

        public static Color GetMeteorColor(float phaseOffset = 0f)
        {
            float phase = Main.GlobalTimeWrappedHourly * 2.2f + phaseOffset;
            float pulse = 0.5f + 0.5f * (float)System.Math.Sin(phase);
            Color skyBody = Color.Lerp(StratusBlue, MoonViolet, pulse * 0.7f);
            Color brightBody = Color.Lerp(skyBody, MoonWhite, 0.18f + 0.12f * pulse);
            return Color.Lerp(brightBody, StarGold, 0.05f + 0.04f * (1f - pulse));
        }

        public static void SpawnStratusDust(Vector2 center, Vector2 direction, float scale = 1f, int alpha = 100)
        {
            Color color = Color.Lerp(StratusBlue, MoonWhite, Main.rand.NextFloat(0.28f));
            Vector2 velocity = -direction.SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(0.15f, 0.75f) + Main.rand.NextVector2Circular(0.75f, 0.75f);
            Dust dust = Dust.NewDustPerfect(
                center,
                Main.rand.NextBool(3) ? DustID.Electric : DustID.TintableDustLighted,
                velocity,
                alpha,
                color,
                Main.rand.NextFloat(0.65f, 1.15f) * scale);
            dust.noGravity = true;
            dust.fadeIn = Main.rand.NextFloat(0.08f, 0.26f);
        }

    }
}
