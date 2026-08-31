using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace FranMod.Core;

internal static class FranTooltipTextEffects
{
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
