using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace FranMod.Core;

internal static class FranTooltipTextEffects
{
    private static readonly Color[] TeacherPrismPalette =
    [
        new Color(76, 218, 255),  // telekinetic ice blue
        new Color(128, 145, 255), // sword-steel periwinkle
        new Color(211, 103, 244), // Fran violet
        new Color(255, 183, 92),  // Teacher gold
        new Color(118, 239, 206)  // magic-stone mint
    ];

    public static Color PrismaticColor(float position)
    {
        float wrapped = position % TeacherPrismPalette.Length;
        if (wrapped < 0f)
            wrapped += TeacherPrismPalette.Length;
        int index = (int)wrapped;
        float amount = wrapped - index;
        amount = amount * amount * (3f - 2f * amount);
        return Color.Lerp(TeacherPrismPalette[index],
            TeacherPrismPalette[(index + 1) % TeacherPrismPalette.Length], amount);
    }

    /// <summary>
    /// Draws a restrained travelling prism across Teacher's name. The dark indigo shadow keeps the
    /// letters readable; the one-pixel halo is deliberately smaller than Calamity's endgame blooms.
    /// </summary>
    public static void DrawTeacherPrismaticName(DrawableTooltipLine line)
    {
        Vector2 position = new(line.X, line.Y);
        float time = Main.GlobalTimeWrappedHourly;

        ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, line.Text,
            position + new Vector2(1.25f, 1.5f), new Color(18, 12, 35, 220),
            line.Rotation, line.Origin, line.BaseScale);

        string prefix = string.Empty;
        for (int i = 0; i < line.Text.Length; i++)
        {
            string glyph = line.Text[i].ToString();
            float advance = line.Font.MeasureString(prefix).X * line.BaseScale.X;
            Vector2 glyphPosition = position + (Vector2.UnitX * advance).RotatedBy(line.Rotation);
            float phase = time * 0.72f + advance * 0.018f;
            Color prism = PrismaticColor(phase);
            float breathe = 0.5f + 0.5f * MathF.Sin(time * 2.6f - i * 0.58f);
            Color body = Color.Lerp(prism, Color.White, 0.06f + breathe * 0.14f);
            Color halo = prism * (0.13f + breathe * 0.05f);

            for (int side = 0; side < 4; side++)
            {
                Vector2 offset = (MathHelper.PiOver2 * side + time * 0.20f)
                    .ToRotationVector2() * 1.05f;
                ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, glyph,
                    glyphPosition + offset, halo, line.Rotation, line.Origin, line.BaseScale);
            }

            ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, glyph,
                glyphPosition, body, line.Rotation, line.Origin, line.BaseScale);
            prefix += glyph;
        }
    }

    /// <summary>Draws stable golden body text with a soft halo and a travelling warm highlight.</summary>
    public static void DrawGolden(DrawableTooltipLine line)
    {
        Vector2 position = new(line.X, line.Y);
        float time = Main.GlobalTimeWrappedHourly;
        float pulse = 0.5f + 0.5f * MathF.Sin(time * 3.1f);
        float haloRadius = 1.35f + pulse * 0.45f;
        Color halo = new Color(255, 171, 42, 0) * (0.22f + pulse * 0.08f);

        const int HaloDraws = 10;
        for (int i = 0; i < HaloDraws; i++)
        {
            Vector2 offset = (MathHelper.TwoPi * i / HaloDraws + time * 0.35f)
                .ToRotationVector2() * haloRadius;
            ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, line.Text,
                position + offset, halo, line.Rotation, line.Origin, line.BaseScale);
        }

        ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, line.Text,
            position + new Vector2(1.25f, 1.5f), new Color(45, 24, 5, 210),
            line.Rotation, line.Origin, line.BaseScale);

        string prefix = string.Empty;
        for (int i = 0; i < line.Text.Length; i++)
        {
            string glyph = line.Text[i].ToString();
            float advance = line.Font.MeasureString(prefix).X * line.BaseScale.X;
            Vector2 glyphPosition = position + (Vector2.UnitX * advance).RotatedBy(line.Rotation);
            float travellingWave = 0.5f + 0.5f * MathF.Sin(time * 4.2f - i * 0.82f);
            travellingWave *= travellingWave;
            Color body = Color.Lerp(new Color(211, 132, 28),
                new Color(255, 246, 166), travellingWave);

            ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, glyph,
                glyphPosition, body, line.Rotation, line.Origin, line.BaseScale);
            prefix += glyph;
        }
    }
}
