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
        Projectile.timeLeft = 24;
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
            SpawnLayer(afterglow: false);
        }
        else if (age == 9)
            SpawnLayer(afterglow: true);

        Lighting.AddLight(Projectile.Center,
            Telekinetic_CondensedMindCore.TrailLightBlue.ToVector3() * (0.7f * (1f - age / 24f)));
    }

    private void SpawnLayer(bool afterglow)
    {
        Vector2 center = Projectile.Center;
        float rotation = Projectile.ai[0];
        Color blue = Telekinetic_CondensedMindCore.TrailBlue;
        Color paleBlue = Telekinetic_CondensedMindCore.TrailLightBlue;
        FranLightingBoltsSystem.SpawnMindCoreBurst(center, rotation, paleBlue, afterglow);

        // 两层合计：14 枚 LightingBolts 亮点 + 10 枚光粒子 + 8 枚火花 + 8 枚 Dust。
        // Dust 占生成数 20%；关闭通用粒子系统的自动补尘，保持显式配比。
        int lightCount = afterglow ? 3 : 7;
        for (int i = 0; i < lightCount; i++)
        {
            float angle = afterglow ? Main.rand.NextFloat(MathHelper.TwoPi)
                : rotation + MathHelper.Pi / 6f + MathHelper.TwoPi * i / lightCount;
            Vector2 direction = angle.ToRotationVector2();
            Vector2 position = center + direction * (afterglow ? 18f : 6f);
            Vector2 velocity = direction * Main.rand.NextFloat(afterglow ? 2f : 4f, afterglow ? 4f : 7f);
            FranParticleSystem.Spawn(new FranLightParticle(position, velocity,
                Main.rand.NextFloat(0.22f, 0.34f), Color.Lerp(blue, paleBlue, Main.rand.NextFloat()),
                afterglow ? 57 : 45, 0.85f, 1.6f, 2.4f, timeScale: 1f / 3f), spawnSupportingDust: false);
        }

        int sparkCount = afterglow ? 3 : 5;
        for (int i = 0; i < sparkCount; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3.5f, 8.5f);
            FranParticleSystem.Spawn(new FranSparkParticle(
                center + Main.rand.NextVector2Circular(12f, 12f), velocity,
                Color.Lerp(paleBlue, Color.White, Main.rand.NextFloat(0.1f, 0.45f)),
                Main.rand.NextFloat(0.20f, 0.32f), new Vector2(0.30f, 1.35f),
                Main.rand.Next(11, 18) * 3, drag: MathF.Pow(0.90f, 1f / 3f)), spawnSupportingDust: false);
        }

        int dustCount = afterglow ? 3 : 5;
        for (int i = 0; i < dustCount; i++)
        {
            Dust dust = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(10f, 10f),
                ModContent.DustType<FranCoreImpactDust>(), Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.6f, 4.2f),
                100, paleBlue, Main.rand.NextFloat(0.65f, 0.9f));
            dust.noGravity = true;
            dust.noLight = true;
        }
    }
}
// Impact-only blue torch dust with an explicit 45-frame lifetime; flight dust is unchanged.
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
        dust.scale = initialScale * (1f - ++age / 45f);
        dust.customData = (age, initialScale);
        if (age >= 45)
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
