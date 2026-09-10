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
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;


public sealed class SkillTakerPhantom : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }
    public override void SetDefaults()
    {
        Projectile.width = 28; Projectile.height = 28; Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee; Projectile.penetrate = 1; Projectile.timeLeft = 55;
        Projectile.tileCollide = false; Projectile.extraUpdates = 1;
    }
    public override void AI()
    {
        float age = Projectile.localAI[0]++;
        float speed = Projectile.velocity.Length();
        if (age < 8f && speed < 24f)
            speed *= 1.035f;

        if (speed > 0.01f)
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * speed;

        if (Projectile.timeLeft < 16)
            Projectile.velocity *= 0.965f;
        Projectile.rotation = Projectile.velocity.ToRotation();
        if (!Main.dedServ && Main.rand.NextBool(2))
            FranParticleEffects.StarlightWake(Projectile.Center, Projectile.velocity, new Color(215, 115, 255));
        if (Main.rand.NextBool(3))
        {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, -Projectile.velocity * 0.04f,
                50, default, 1.1f);
            d.noGravity = true;
        }
        Lighting.AddLight(Projectile.Center, 0.38f, 0.08f, 0.55f);
    }
    public override bool PreDraw(ref Color lightColor)
    {
        Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
        float speedRatio = FranMotion.Clamp01((Projectile.velocity.Length() - 12f) / 12f);
        float opacity = FranMotion.Smooth(FranMotion.Segment(Projectile.localAI[0], 0f, 5f)) *
            FranMotion.Smooth(FranMotion.Segment(Projectile.timeLeft, 0f, 16f));
        Color phantomColor = Color.Lerp(new Color(95, 45, 215), new Color(235, 175, 255), speedRatio);
        Color starlight = Color.Lerp(new Color(120, 160, 255), Color.White, speedRatio * 0.55f);
        FranVfx.BeginAdditive();

        // 分段弧形星轨代替单一矩形拖尾，使飞行方向更清楚。
        Vector2 previous = Projectile.Center;
        for (int i = 1; i < Projectile.oldPos.Length; i++)
        {
            Vector2 point = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            if (point == Projectile.Size * 0.5f)
                continue;
            float completion = 1f - i / (float)Projectile.oldPos.Length;
            FranVfx.DrawStretched("FranVfx_DrainLineBloom2", point, previous,
                Color.Lerp(phantomColor, starlight, completion) * (0.52f * completion * opacity),
                9f + completion * 7f);
            if (i % 3 == 0)
                FranVfx.DrawShared("FranVfx_SimpleStar", point, starlight * (0.32f * completion * opacity),
                    Projectile.rotation + i * 0.73f, 0.035f + completion * 0.025f);
            previous = point;
        }

        float spin = Main.GlobalTimeWrappedHourly * 2.2f + Projectile.identity;
        FranVfx.DrawShared("FranVfx_FadedStarRing", Projectile.Center, phantomColor * (0.38f * opacity),
            spin, 0.115f + speedRatio * 0.025f);
        FranVfx.DrawShared("FranVfx_FadedStarRing", Projectile.Center, starlight * (0.24f * opacity),
            -spin * 0.72f, 0.075f + speedRatio * 0.018f);
        FranVfx.DrawShared("FranVfx_PulseStar", Projectile.Center, phantomColor * (0.76f * opacity),
            Projectile.rotation, new Vector2(0.15f, 0.095f));
        FranVfx.DrawShared("FranVfx_StarProj", Projectile.Center, Color.White * (0.92f * opacity),
            -Projectile.rotation * 0.45f, 0.18f);
        FranVfx.DrawBloom(Projectile.Center, starlight, 22f, 0.48f * opacity);
        FranVfx.EndAdditive();
        return false;
    }
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        target.GetGlobalNPC<SkillTakerGlobalNPC>().Mark(Projectile.owner, 360);
        if (Main.netMode == NetmodeID.MultiplayerClient && Main.myPlayer == Projectile.owner)
            FranModMain.SendSkillTakerMark(target.whoAmI, Projectile.owner, 360);
        FranBurst.SpawnForOwner(Projectile.GetSource_OnHit(target), target.Center, Projectile.owner,
            FranBurstStyle.SkillTaker, 0.72f);
        FranParticleEffects.ToxicBurst(target.Center, -Projectile.velocity, 7, 0.55f);
        FranVfx.SparkBurst(target.Center, new Color(205, 100, 255), 8, 4.5f);
        if (Main.myPlayer == Projectile.owner)
        {
            Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center,
                Main.rand.NextVector2Circular(2f, 2f), ModContent.ProjectileType<SkillTakerSoul>(),
                0, 0f, Projectile.owner);
            for (int i = 0; i < 3; i++)
            {
                Vector2 heartVelocity = (-Projectile.velocity.SafeNormalize(Vector2.UnitX))
                    .RotatedByRandom(0.85f) * Main.rand.NextFloat(5.5f, 9f);
                Projectile.NewProjectile(Projectile.GetSource_OnHit(target), target.Center, heartVelocity,
                    ModContent.ProjectileType<FranHeartSeekerProjectile>(),
                    Math.Max(1, (int)(Projectile.damage * 0.34f)), Projectile.knockBack * 0.35f, Projectile.owner);
            }
        }
    }
}

public sealed class SkillTakerGlobalNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;
    private int owner = -1;
    private int timer;
    private int hitCounter;

    public void Mark(int player, int duration)
    {
        if (timer <= 0 || owner != player)
            hitCounter = 0;
        owner = player;
        timer = Math.Max(timer, duration);
    }

    public override void PostAI(NPC npc)
    {
        if (timer <= 0)
            return;
        timer--;
        Lighting.AddLight(npc.Center, 0.18f, 0.02f, 0.26f);
        if (Main.rand.NextBool(4))
        {
            Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(npc.width, npc.height) * 0.5f,
                DustID.PurpleTorch, -Vector2.UnitY, 80, default, 1.1f);
            d.noGravity = true;
        }
    }

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (timer <= 0)
            return;
        float fade = MathHelper.Clamp(timer / 45f, 0f, 1f);
        float radius = Math.Max(npc.width, npc.height) * 0.72f + 16f;
        FranVfx.BeginAdditive();
        FranVfx.DrawRing(npc.Center, new Color(170, 65, 235), radius, 0.28f * fade,
            Main.GlobalTimeWrappedHourly * 0.7f, "FranVfx_FadedStarRing");
        FranVfx.DrawRing(npc.Center, new Color(225, 165, 255), radius * 0.72f, 0.18f * fade,
            -Main.GlobalTimeWrappedHourly * 0.9f, "FranVfx_BloomRing");
        for (int i = 0; i < 5; i++)
        {
            float angle = Main.GlobalTimeWrappedHourly * 0.46f + i * MathHelper.TwoPi / 5f;
            FranVfx.DrawShared("FranVfx_SimpleStar", npc.Center + angle.ToRotationVector2() * radius * 0.72f,
                Color.White * (0.22f * fade), -angle, 0.035f);
        }
        FranVfx.EndAdditive();
    }

    public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) => HandleHit(npc, player.whoAmI);
    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) => HandleHit(npc, projectile.owner);

    private void HandleHit(NPC npc, int attacker)
    {
        if (timer <= 0 || attacker != owner || Main.myPlayer != owner || ++hitCounter % 3 != 0)
            return;
        SpawnSouls(npc, 1);
    }

    public override void OnKill(NPC npc)
    {
        if (timer > 0 && owner >= 0 && Main.myPlayer == owner)
            SpawnSouls(npc, npc.boss ? 2 : 1);
    }

    private void SpawnSouls(NPC npc, int count)
    {
        if (owner < 0 || owner >= Main.maxPlayers || !Main.player[owner].active)
            return;
        for (int i = 0; i < count; i++)
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Main.rand.NextVector2Circular(5f, 5f),
                ModContent.ProjectileType<SkillTakerSoul>(), 0, 0f, owner);
    }
}

public sealed class SkillTakerSoul : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";
    public override void SetDefaults()
    {
        Projectile.width = 14; Projectile.height = 14; Projectile.friendly = false;
        Projectile.penetrate = -1; Projectile.timeLeft = 180;
        Projectile.tileCollide = false;
    }
    public override bool? CanDamage() => false;
    public override void AI()
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }
        if (Projectile.timeLeft < 168)
        {
            Vector2 toOwner = owner.MountedCenter - Projectile.Center;
            if (toOwner.LengthSquared() < 28f * 28f)
            {
                if (Main.myPlayer == Projectile.owner)
                    owner.GetModPlayer<FranPlayer>().GrantStolenSkill();
                FranBurst.SpawnForOwner(Projectile.GetSource_Death(), owner.MountedCenter,
                    Projectile.owner, FranBurstStyle.SkillTaker, 0.42f);
                Projectile.Kill();
                return;
            }
            Vector2 desired = toOwner.SafeNormalize(Vector2.UnitX * owner.direction) * 13f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);
        }
        if (!Main.dedServ && Main.rand.NextBool(2))
            FranParticleEffects.StarlightWake(Projectile.Center, Projectile.velocity, new Color(205, 105, 255));
        if (Main.rand.NextBool(3))
        {
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, -Projectile.velocity * 0.05f,
                50, default, 1f);
            d.noGravity = true;
        }
    }
    public override bool PreDraw(ref Color lightColor)
    {
        float pulse = 0.85f + MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.identity) * 0.12f;
        FranVfx.BeginAdditive();
        FranVfx.DrawShared("FranVfx_FadedStarRing", Projectile.Center, new Color(145, 75, 255) * 0.42f,
            Main.GlobalTimeWrappedHourly * 1.8f, 0.055f * pulse);
        FranVfx.DrawShared("FranVfx_SimpleStar", Projectile.Center, new Color(215, 125, 255) * 0.72f,
            -Main.GlobalTimeWrappedHourly * 2.3f, 0.055f * pulse);
        FranVfx.DrawShared("FranVfx_StarProj", Projectile.Center, Color.White * 0.86f,
            Projectile.velocity.ToRotation(), 0.10f * pulse);
        FranVfx.DrawBloom(Projectile.Center, new Color(215, 125, 255), 14f * pulse, 0.56f);
        FranVfx.EndAdditive();
        return false;
    }
}
