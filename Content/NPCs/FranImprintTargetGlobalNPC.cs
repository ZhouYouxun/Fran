using System;
using System.Collections.Generic;
using FranMod.Content.Players;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


public sealed class FranImprintTargetGlobalNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;
    private int skillSealOwner = -1;
    private int skillSealTacticLayers = 1;
    private int skillSealFramesSinceHit;
    private float skillSealSustainedFrames;

    private bool HasSkillTakerMark => skillSealOwner >= 0;

    private int SkillTakerRampFrames => Math.Max(120,
        FranBalance.GetInt(nameof(FranBalance.SkillTakerBaseRampFrames),
            FranBalance.SkillTakerBaseRampFrames) -
        FranBalance.GetInt(nameof(FranBalance.SkillTakerExtraLayerRampReductionFrames),
            FranBalance.SkillTakerExtraLayerRampReductionFrames) * (skillSealTacticLayers - 1));

    private int SkillTakerDecayFrames => Math.Max(60,
        FranBalance.GetInt(nameof(FranBalance.SkillTakerBaseDecayFrames),
            FranBalance.SkillTakerBaseDecayFrames) +
        FranBalance.GetInt(nameof(FranBalance.SkillTakerExtraLayerDecayFrames),
            FranBalance.SkillTakerExtraLayerDecayFrames) * (skillSealTacticLayers - 1));

    private int SkillTakerHitGraceFrames => Math.Max(1,
        FranBalance.GetInt(nameof(FranBalance.SkillTakerContinuousHitGraceFrames),
            FranBalance.SkillTakerContinuousHitGraceFrames));

    private float SkillTakerRampProgress => Utils.GetLerpValue(60f, SkillTakerRampFrames,
        skillSealSustainedFrames, true);

    private float SkillTakerFade => skillSealFramesSinceHit <= SkillTakerHitGraceFrames
        ? 1f
        : 1f - Utils.GetLerpValue(SkillTakerHitGraceFrames, SkillTakerDecayFrames,
            skillSealFramesSinceHit, true);

    public float ApplySkillTakerMark(int owner, int tacticLayers)
    {
        if (skillSealOwner != owner)
            skillSealSustainedFrames = 0f;
        skillSealOwner = owner;
        skillSealTacticLayers = Math.Clamp(tacticLayers, 1, FranPlayer.MaximumTacticSlots);
        skillSealFramesSinceHit = 0;
        return SkillTakerRampProgress;
    }

    private void ContinueSkillTakerMark(int owner)
    {
        if (!HasSkillTakerMark || skillSealOwner != owner)
            return;
        skillSealFramesSinceHit = 0;
        if (Main.player.IndexInRange(owner) && Main.player[owner].active)
            skillSealTacticLayers = Math.Clamp(Main.player[owner].GetModPlayer<FranPlayer>()
                .GetSkillStacks(FranSkill.SkillTaker), 1, FranPlayer.MaximumTacticSlots);
    }

    public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
    {
        if (!HasSkillTakerMark)
            return;
        float minimum = FranBalance.GetFloat(nameof(FranBalance.SkillTakerMinimumDamageMultiplier),
            FranBalance.SkillTakerMinimumDamageMultiplier);
        float maximum = Math.Max(minimum, FranBalance.GetFloat(
            nameof(FranBalance.SkillTakerMaximumDamageMultiplier),
            FranBalance.SkillTakerMaximumDamageMultiplier));
        if (Main.player.IndexInRange(skillSealOwner) && Main.player[skillSealOwner].active &&
            Main.player[skillSealOwner].GetModPlayer<FranPlayer>()
                .IsFullTacticLoadout(FranSkill.SkillTaker))
            maximum = Math.Max(maximum, FranBalance.GetFloat(
                nameof(FranBalance.SkillTakerFullLoadoutMaximumDamageMultiplier),
                FranBalance.SkillTakerFullLoadoutMaximumDamageMultiplier));
        modifiers.FinalDamage *= MathHelper.Lerp(minimum, maximum, SkillTakerRampProgress);
    }

    public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) =>
        ContinueSkillTakerMark(player.whoAmI);

    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            ContinueSkillTakerMark(projectile.owner);
    }

    public override void PostAI(NPC npc)
    {
        if (!HasSkillTakerMark)
            return;

        skillSealFramesSinceHit++;
        if (skillSealFramesSinceHit <= SkillTakerHitGraceFrames)
            skillSealSustainedFrames = Math.Min(SkillTakerRampFrames, skillSealSustainedFrames + 1f);
        else
            skillSealSustainedFrames = Math.Max(0f,
                skillSealSustainedFrames - SkillTakerRampFrames / (float)SkillTakerDecayFrames);

        if (skillSealFramesSinceHit < SkillTakerDecayFrames)
            return;
        skillSealOwner = -1;
        skillSealFramesSinceHit = 0;
        skillSealSustainedFrames = 0f;
    }

    public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (!HasSkillTakerMark)
            return true;
        float progress = SkillTakerRampProgress;
        float fade = SkillTakerFade;
        if (fade > 0f)
            FranSkillTakerOrbitRenderer.TryDrawLayer(npc, progress, fade, skillSealTacticLayers,
                frontLayer: false);
        return true;
    }

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (!HasSkillTakerMark)
            return;
        float progress = SkillTakerRampProgress;
        float fade = SkillTakerFade;
        if (fade <= 0f)
            return;

        bool shaderTrailAvailable = FranSkillTakerOrbitRenderer.TryDrawLayer(npc, progress, fade,
            skillSealTacticLayers, frontLayer: true);
        if (!shaderTrailAvailable)
            FranSkillTakerOrbitRenderer.DrawFallback(npc, progress, fade, skillSealTacticLayers);

        FranVfx.BeginAdditive();
        float radius = Math.Max(npc.width, npc.height) * 0.62f + 12f + progress * 9f;
        Color markColor = Color.Lerp(new Color(175, 70, 255), new Color(245, 165, 255), progress);
        FranVfx.DrawRing(npc.Center, markColor, radius, (0.08f + progress * 0.08f) * fade,
            Main.GlobalTimeWrappedHourly * (0.7f + progress * 1.3f), "Cal_FadedStarRing");
        FranVfx.DrawRing(npc.Center, Color.White, radius * 0.72f, (0.035f + progress * 0.055f) * fade,
            -Main.GlobalTimeWrappedHourly * (1.1f + progress * 1.7f), "Cal_BloomRing");
        FranVfx.EndAdditive();
    }
}
