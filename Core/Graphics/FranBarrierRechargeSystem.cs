using System;
using FranMod.Content.Players;
using FranMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Core.Graphics;

[Autoload(Side = ModSide.Client)]
public sealed class FranBarrierRechargeSystem : ModSystem
{
    public override void PostDrawTiles()
    {
        Player owner = Main.LocalPlayer;
        if (Main.gameMenu || !owner.active || owner.dead)
            return;

        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        int skill = (int)FranSkill.MagicBarrier;
        int remaining = fran.SkillCooldowns[skill];
        int maximum = fran.SkillCooldownMaximums[skill];
        if (fran.GetSkillStacks(FranSkill.MagicBarrier) <= 0 || remaining <= 0 || maximum <= 0 ||
            FranAutoBarrierProjectile.IsActive(owner))
            return;

        // A visual preview only: recharge never creates a projectile or grants protection.
        float progress = MathHelper.Clamp(1f - remaining / (float)Math.Max(1, maximum), 0f, 1f);
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        try
        {
            FranBarrierRenderer.TryDraw(owner.MountedCenter + new Vector2(0f, owner.gfxOffY),
                progress, 0f, Main.GlobalTimeWrappedHourly * 0.72f);
        }
        finally
        {
            Main.spriteBatch.End();
        }
    }
}
