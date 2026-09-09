using System.Collections.Generic;
using FranMod.Content.Players;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

public sealed class FranTelekineticFlightVisual : ModProjectile
{
    private float opacity;
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 2;
        Projectile.timeLeft = 2;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
        Projectile.hide = true;
    }
    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;
    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
        List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) => behindNPCs.Add(index);

    public override void AI()
    {
        if (!Main.player.IndexInRange(Projectile.owner))
        {
            Projectile.Kill();
            return;
        }
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }
        Projectile.Center = owner.Center + Vector2.UnitY * owner.gravDir * (owner.height * 0.5f + 9f);
        Projectile.spriteDirection = owner.direction;
        // 贴图剑尖原本朝右上。翻转与旋转配对，剑身水平朝向玩家面向；不随时间摇摆。
        Projectile.rotation = owner.direction < 0 ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        bool active = owner.GetModPlayer<FranPlayer>().TelekineticFlightVisualActive;
        opacity = active ? MathHelper.Lerp(opacity, 1f, 0.22f) : opacity * 0.82f;
        if (!active && opacity < 0.025f)
            Projectile.Kill();
        else
            Projectile.timeLeft = 2;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (opacity <= 0.01f)
            return false;
        Texture2D teacher = FranTeacherVisuals.Texture;
        Vector2 position = Projectile.Center - Main.screenPosition;
        Vector2 origin = teacher.Size() * 0.5f;
        float scale = 0.92f * 0.75f * FranTeacherVisuals.TextureScale;
        SpriteEffects effects = Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        // 仅描边与剑身：去除光圈、流线、残影和漂浮粒子。
        for (int i = 0; i < 8; i++)
            Main.EntitySpriteDraw(teacher, position + (MathHelper.TwoPi * i / 8f).ToRotationVector2() * 2f,
                null, new Color(40, 145, 255, 0) * opacity, Projectile.rotation, origin, scale, effects);
        Color environmentLight = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
        Main.EntitySpriteDraw(teacher, position, null, Color.Lerp(environmentLight, Color.White, 0.34f) * opacity,
            Projectile.rotation, origin, scale, effects);
        return false;
    }
}