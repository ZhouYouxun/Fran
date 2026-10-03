using FranMod.Content.Players;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// Like vanilla's hoverboard, draw with the player, behind the feet, using seven vertical frames.
public sealed class FranTelekineticBoardLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Wings);

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
    {
        Player player = drawInfo.drawPlayer;
        return !player.dead && !player.mount.Active && !drawInfo.hideEntirePlayer &&
            !player.GetModPlayer<FranPlayer>().HasExternalWings &&
            player.GetModPlayer<FranPlayer>().TelekineticFlightVisualActive;
    }

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        Player player = drawInfo.drawPlayer;
        Texture2D texture = ModContent.Request<Texture2D>("Fran/Textures/师父飞剑-Sheet").Value;
        Rectangle frame = texture.Frame(1, 7, 0, (int)(Main.GameUpdateCount / 6 % 7));
        // The deck surface is eight pixels below the frame top. Keep it touching the soles.
        Vector2 position = (drawInfo.Position - Main.screenPosition +
            new Vector2(player.width * 0.5f, player.gravDir > 0f ? player.height : 0f)).Floor();
        Vector2 origin = new(frame.Width * 0.5f, player.gravDir > 0f ? 8f : frame.Height - 8f);
        float visibility = (1f - drawInfo.shadow) * drawInfo.stealth;
        if (drawInfo.shadow == 0f)
        {
            float pulse = 0.22f + 0.05f * (float)System.Math.Sin(Main.GameUpdateCount * 0.08);
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = (MathHelper.PiOver2 * i).ToRotationVector2();
                drawInfo.DrawDataCache.Add(new DrawData(texture, position + offset, frame,
                    new Color(65, 140, 255, 0) * pulse * visibility, player.bodyRotation,
                    origin, 1f, drawInfo.playerEffect, 0f));
            }
        }
        Color light = Color.Lerp(drawInfo.colorArmorBody, Color.White, 0.25f) * visibility;
        drawInfo.DrawDataCache.Add(new DrawData(texture, position, frame, light,
            player.bodyRotation, origin, 1f, drawInfo.playerEffect, 0f));
    }
}
