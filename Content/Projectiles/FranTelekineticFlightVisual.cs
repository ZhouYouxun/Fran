using System;
using System.Collections.Generic;
using FranMod.Content.Players;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

public sealed class FranTelekineticFlightVisual : ModProjectile
{
    private const int HistoryLength = 12;
    private readonly Vector2[] positionHistory = new Vector2[HistoryLength];
    private float opacity;
    private float visualAge;
    private bool historyInitialized;

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
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
        List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
    {
        behindNPCs.Add(index);
    }

    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
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

        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        Projectile.Center = owner.Center;
        visualAge++;
        UpdateHistory(owner.Center);

        if (fran.TelekineticFlightVisualActive)
        {
            Projectile.timeLeft = 2;
            opacity = MathHelper.Lerp(opacity, 1f, 0.22f);
        }
        else
        {
            opacity *= 0.82f;
            if (opacity < 0.025f)
            {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 2;
        }

        Lighting.AddLight(GetTeacherCenter(owner), 0.12f * opacity, 0.27f * opacity, 0.45f * opacity);
        if (!Main.dedServ && fran.TelekineticFlightVisualActive && Main.GameUpdateCount % 3 == 0)
        {
            Vector2 center = GetTeacherCenter(owner);
            Vector2 velocity = -owner.velocity * 0.08f + Main.rand.NextVector2Circular(0.8f, 0.8f);
            Dust dust = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(30f, 8f), DustID.BlueTorch,
                velocity, 110, new Color(115, 210, 255), Main.rand.NextFloat(0.7f, 1.05f));
            dust.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || opacity <= 0.01f)
            return false;

        Texture2D fin = ModContent.Request<Texture2D>("Fran/Assets/Flight/TelekineticFlightFin").Value;
        Texture2D halo = ModContent.Request<Texture2D>("Fran/Assets/Flight/TelekineticFlightHalo").Value;
        Texture2D ribbon = ModContent.Request<Texture2D>("Fran/Assets/Flight/TelekineticFlightRibbon").Value;
        Texture2D teacher = TextureAssets.Item[ItemID.BreakerBlade].Value;

        Vector2 teacherCenter = GetTeacherCenter(owner);
        float speed = owner.velocity.Length();
        int facing = Math.Abs(owner.velocity.X) > 0.4f ? Math.Sign(owner.velocity.X) : owner.direction;
        if (facing == 0)
            facing = 1;
        Vector2 travelDirection = owner.velocity.SafeNormalize(Vector2.UnitX * facing);
        float bladeRotation = MathHelper.PiOver4 + (facing < 0 ? MathHelper.Pi : 0f) +
            MathHelper.Clamp(owner.velocity.Y * 0.012f, -0.16f, 0.16f);
        float pulse = 0.5f + 0.5f * MathF.Sin(visualAge * 0.17f);
        bool dashing = owner.GetModPlayer<FranPlayer>().DashTimer > 0;
        float intensity = opacity * (dashing ? 1.22f : 1f);

        FranVfx.BeginAdditive();

        Main.EntitySpriteDraw(halo, teacherCenter - Main.screenPosition + Vector2.UnitY * owner.gravDir * 3f,
            null, Color.White * (0.48f * intensity), visualAge * 0.012f * owner.gravDir,
            halo.Size() * 0.5f, 0.23f + pulse * 0.018f, SpriteEffects.None, 0f);
        Main.EntitySpriteDraw(halo, teacherCenter - Main.screenPosition + Vector2.UnitY * owner.gravDir * 3f,
            null, new Color(120, 185, 255) * (0.22f * intensity), -visualAge * 0.009f * owner.gravDir,
            halo.Size() * 0.5f, 0.31f + pulse * 0.025f, SpriteEffects.None, 0f);

        int pieceCount = FranTelekineticFlightSystem.CurrentTier switch
        {
            <= 1 => 3,
            2 => 5,
            _ => 7
        };
        for (int i = 0; i < pieceCount; i++)
        {
            float centerIndex = i - (pieceCount - 1) * 0.5f;
            float distanceFromCenter = Math.Abs(centerIndex);
            float wave = MathF.Sin(visualAge * 0.21f + i * 0.72f) * (2.2f + distanceFromCenter * 0.5f);
            Vector2 piecePosition = teacherCenter - Vector2.UnitX * facing * (15f + distanceFromCenter * 8f) +
                Vector2.UnitY * owner.gravDir * (centerIndex * 4.3f + wave);
            float rotation = (facing < 0 ? MathHelper.Pi : 0f) + centerIndex * 0.055f * owner.gravDir;
            float scale = 0.145f + (pieceCount - distanceFromCenter) * 0.006f + pulse * 0.008f;
            Main.EntitySpriteDraw(fin, piecePosition - Main.screenPosition, null,
                Color.White * ((0.20f + (pieceCount - i) * 0.025f) * intensity), rotation,
                fin.Size() * 0.5f, new Vector2(scale, scale * 0.86f), SpriteEffects.None, 0f);
        }

        if (speed > 1.2f)
        {
            float ribbonLength = MathHelper.Clamp(0.38f + speed * 0.025f, 0.42f, 0.82f);
            Vector2 ribbonCenter = teacherCenter - travelDirection * (28f + speed * 1.3f);
            Main.EntitySpriteDraw(ribbon, ribbonCenter - Main.screenPosition, null,
                Color.White * ((0.28f + MathHelper.Clamp(speed / 20f, 0f, 0.34f)) * intensity),
                travelDirection.ToRotation(), ribbon.Size() * 0.5f,
                new Vector2(ribbonLength, 0.42f + pulse * 0.04f), SpriteEffects.None, 0f);
        }

        FranVfx.DrawBloom(teacherCenter, new Color(80, 175, 255), 35f + pulse * 6f, 0.34f * intensity);
        FranVfx.DrawBloom(teacherCenter, Color.White, 13f + pulse * 3f, 0.25f * intensity, hard: true);
        FranVfx.EndAdditive();

        Color afterimageColor = new Color(105, 190, 255);
        for (int i = HistoryLength - 1; i >= 3; i -= 3)
        {
            if (positionHistory[i] == Vector2.Zero)
                continue;
            Vector2 oldTeacherCenter = GetTeacherCenter(owner, positionHistory[i]);
            float trailOpacity = (1f - i / (float)HistoryLength) * 0.18f * opacity;
            Main.EntitySpriteDraw(teacher, oldTeacherCenter - Main.screenPosition, null,
                afterimageColor * trailOpacity, bladeRotation, teacher.Size() * 0.5f, 0.88f,
                SpriteEffects.None, 0f);
        }

        Color environmentLight = Lighting.GetColor((int)(teacherCenter.X / 16f), (int)(teacherCenter.Y / 16f));
        Color bladeColor = Color.Lerp(environmentLight, Color.White, 0.34f);
        Main.EntitySpriteDraw(teacher, teacherCenter - Main.screenPosition, null, bladeColor * opacity,
            bladeRotation, teacher.Size() * 0.5f, 0.92f, SpriteEffects.None, 0f);
        return false;
    }

    private void UpdateHistory(Vector2 center)
    {
        if (!historyInitialized)
        {
            for (int i = 0; i < positionHistory.Length; i++)
                positionHistory[i] = center;
            historyInitialized = true;
            return;
        }

        for (int i = positionHistory.Length - 1; i > 0; i--)
            positionHistory[i] = positionHistory[i - 1];
        positionHistory[0] = center;
    }

    private static Vector2 GetTeacherCenter(Player owner) => GetTeacherCenter(owner, owner.Center);

    private static Vector2 GetTeacherCenter(Player owner, Vector2 playerCenter)
    {
        return playerCenter + Vector2.UnitY * owner.gravDir * (owner.height * 0.5f + 9f) -
            Vector2.UnitX * owner.direction * 3f;
    }
}
