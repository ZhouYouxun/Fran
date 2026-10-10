using System;
using Luminance.Core.Graphics;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

// 同步一个无伤害的短命发射器，各客户端各自播放粒子，不搬运爆炸动画或切割贴图。
public sealed class FranCoreImpactBurst : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_0";

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 2;
        Projectile.friendly = false;
        Projectile.hostile = false;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 18;
        Projectile.noEnchantmentVisuals = true;
    }

    public override bool? CanDamage() => false;
    public override bool PreDraw(ref Color lightColor) => false;

    public override void AI()
    {
        int age = (int)Projectile.localAI[0]++;
        if (Main.dedServ)
            return;

        if (age == 0)
        {
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.45f, Pitch = 0.2f, MaxInstances = 3 },
                Projectile.Center);
            SpawnLayer(release: false);
            if (Projectile.ai[1] > 0f)
                FranLightingBoltsSystem.SpawnMindCoreJudgementHitLights(
                    Projectile.Center, Projectile.ai[0].ToRotationVector2());
        }
        else if (age == 5)
            SpawnLayer(release: true);

        Lighting.AddLight(Projectile.Center,
            Telekinetic_CondensedMindCore.TrailLightBlue.ToVector3() * (0.7f * (1f - age / 18f)));
    }

    private void SpawnLayer(bool release)
    {
        Vector2 center = Projectile.Center;
        float rotation = Projectile.ai[0];
        Color blue = Telekinetic_CondensedMindCore.TrailBlue;
        Color paleBlue = Telekinetic_CondensedMindCore.TrailLightBlue;
        Vector2 forward = rotation.ToRotationVector2();
        Vector2 side = forward.RotatedBy(MathHelper.PiOver2);
        // 撞击先收束成核，再沿入射方向排出短碎片，避免二次长尾放射爆发。
        FranParticleSystem.Spawn(new FranDirectionalRingParticle(center, Vector2.Zero, paleBlue,
            release ? new Vector2(0.02f, 0.045f) : new Vector2(0.08f, 0.16f),
            release ? new Vector2(0.10f, 0.20f) : new Vector2(0.02f, 0.045f),
            rotation, release ? 10 : 6), spawnSupportingDust: false);
        for (int i = 0; i < 6; i++)
        {
            float angle = MathHelper.TwoPi * i / 6f;
            Vector2 offset = forward * MathF.Cos(angle) * 22f + side * MathF.Sin(angle) * 14f;
            if (!release)
                FranParticleSystem.Spawn(new FranLightParticle(center + offset, -offset * 0.18f,
                    0.22f, Color.Lerp(blue, paleBlue, i / 5f), 6, 0.85f, 1.6f, 2.4f), spawnSupportingDust: false);
            else
            {
                Vector2 velocity = forward * Main.rand.NextFloat(5f, 10f) + side * (i - 2.5f) * 0.8f;
                FranParticleSystem.Spawn(new FranBladeEdgeMoteParticle(center + side * (i - 2.5f) * 2f,
                    velocity, forward, i % 3 == 0 ? Color.White : paleBlue,
                    Main.rand.NextFloat(12f, 24f), 1f, 11, 0.86f), spawnSupportingDust: false);
            }
        }
        if (!release)
            return;
        for (int i = 0; i < 4; i++)
        {
            Dust dust = Dust.NewDustPerfect(center + side * (i - 1.5f) * 3f,
                ModContent.DustType<FranCoreImpactDust>(), forward * 2f + side * (i - 1.5f) * 0.5f,
                100, paleBlue, Main.rand.NextFloat(0.55f, 0.75f));
            dust.noGravity = true;
            dust.noLight = true;
        }
    }
}
// 撞击碎屑快速消退；旧飞行 Dust 类型保留注册以兼容现有内容。
public sealed class FranCoreImpactDust : ModDust
{
    public override string Texture => "Terraria/Images/Dust";
    public override void OnSpawn(Dust dust)
    {
        dust.frame = new Rectangle(10 * (DustID.BlueTorch % 100),
            30 * (DustID.BlueTorch / 100) + Main.rand.Next(3) * 10, 8, 8);
        dust.customData = (0, dust.scale);
    }
    public override bool Update(Dust dust)
    {
        (int age, float initialScale) = dust.customData is ValueTuple<int, float> state ? state : (0, dust.scale);
        dust.position += dust.velocity;
        dust.velocity *= 0.97f;
        dust.scale = initialScale * (1f - ++age / 16f);
        dust.customData = (age, initialScale);
        if (age >= 16)
            dust.active = false;
        return false;
    }
    public override Color? GetAlpha(Dust dust, Color lightColor) => dust.color * (1f - dust.alpha / 255f);
}
public abstract class FranCoreDust : ModDust
{
    private ArmorShaderData blueShader;
    protected abstract int ReferenceType { get; }
    public override string Texture => "Terraria/Images/Dust";
    public override void SetStaticDefaults() => UpdateType = ReferenceType;
    public override void OnSpawn(Dust dust)
    {
        dust.frame = new Rectangle(10 * (ReferenceType % 100),
            30 * (ReferenceType / 100) + Main.rand.Next(3) * 10, 8, 8);
        dust.noLight = true;
        if (ShaderManager.TryGetShader("Fran.FranCoreSpriteTint", out ManagedShader shader))
        {
            shader.TrySetParameter("uBlue", Telekinetic_CondensedMindCore.TrailLightBlue);
            blueShader ??= new ArmorShaderData(ModContent.Request<Effect>(
                "Fran/Assets/AutoloadedEffects/Shaders/Primitives/FranCoreSpriteTint"), "AutoloadPass");
            dust.shader = blueShader;
        }
    }
    public override Color? GetAlpha(Dust dust, Color lightColor)
    {
        int originalType = dust.type;
        Color original;
        try
        {
            dust.type = ReferenceType;
            original = dust.GetAlpha(lightColor);
        }
        finally { dust.type = originalType; }
        float brightness = Math.Max(original.R, Math.Max(original.G, original.B)) / 255f;
        // The shader converts even baked-in green pixels. Keep the vertex color neutral
        // so blue is not multiplied into the result a second time.
        Color blue = (dust.shader is null ? Telekinetic_CondensedMindCore.TrailLightBlue : Color.White) * brightness;
        blue.A = original.A;
        return blue;
    }
}

public sealed class FranCoreDimDust : FranCoreDust { protected override int ReferenceType => 131; }
public sealed class FranCoreBrightDust : FranCoreDust { protected override int ReferenceType => 294; }
