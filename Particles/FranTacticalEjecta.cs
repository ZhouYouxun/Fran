using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Particles;

/// <summary>
/// 主武器命中后沿斩击方向释放的战术液滴与撕裂碎屑。
/// <para>
/// 封装飞行、短暂无伤害期、拖尾绘制，以及落地或命中后的消散粒子。
/// 弹幕本体使用 Fran 的 BOLT，消散阶段使用本模组粒子资源，不依赖外部模组。
/// </para>
/// <para>
/// 通过 <see cref="SpawnCone"/> 创建锥形弹幕组：
/// <code>
/// FranTacticalEjecta.SpawnCone(source, position, direction, 3,
///     MathHelper.ToRadians(24f), 6f, 10f, damage, knockback, owner);
/// </code>
/// 未指定 color 参数时使用白色；需要匹配战术颜色时传入
/// <c>FranSkillInfo.Color(skill)</c>。颜色存入 Projectile.ai[0..2]，以便联机同步。
/// direction 表示喷射方向；绘制时会将 BOLT 的竖直长轴旋转至飞行方向。
/// </para>
/// </summary>
public sealed class FranTacticalEjecta : ModProjectile
{
    private const int ArmTicks = 7;
    private const int TileGraceTicks = 10;
    private const int Lifetime = 72;
    private bool impactPlayed;

    private int Age => (int)Projectile.localAI[0];

    // ai[0..2] 全为 0 表示未指定颜色，此时回退为白色。
    private Color Tint
    {
        get
        {
            float red = MathHelper.Clamp(Projectile.ai[0], 0f, 1f);
            float green = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
            float blue = MathHelper.Clamp(Projectile.ai[2], 0f, 1f);
            return red + green + blue <= 0.001f ? Color.White : new Color(red, green, blue);
        }
    }

    public override string Texture => "Fran/Textures/BOLT";

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 14;
        ProjectileID.Sets.TrailingMode[Type] = 0;
    }

    public override void SetDefaults()
    {
        Projectile.width = 14;
        Projectile.height = 14;
        Projectile.friendly = true;
        Projectile.hostile = false;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.penetrate = 1;
        Projectile.timeLeft = Lifetime;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.usesIDStaticNPCImmunity = true;
        Projectile.idStaticNPCHitCooldown = 10;
    }

    public override bool? CanDamage() => Age >= ArmTicks ? null : false;

    public override void AI()
    {
        Projectile.localAI[0]++;
        Projectile.tileCollide = Age >= TileGraceTicks;
        Projectile.rotation = Projectile.velocity.ToRotation();

        // 先像挥砍甩出的碎屑一样直射，随后才逐渐受重力影响；这样命中瞬间能读出明确的前方方向。
        if (Age >= 8)
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.16f, 15f);
        Projectile.velocity.X *= 0.996f;

        Color tint = Tint;
        Lighting.AddLight(Projectile.Center, tint.ToVector3() * (0.28f * Projectile.scale));
        if (Main.dedServ || Age <= 1)
            return;

        // 飞行中的细小伴生液滴只是辅助层；即使粒子达到上限，PreDraw 里的弹幕本体仍然可见。
        if (Main.rand.NextBool(2))
        {
            Vector2 wake = -Projectile.velocity * Main.rand.NextFloat(0.015f, 0.05f);
            FranParticleSystem.Spawn(new FranTacticalDropletParticle(
                Projectile.Center - Projectile.velocity * Main.rand.NextFloat(0.06f, 0.20f),
                wake, Color.Lerp(tint, Color.White, Main.rand.NextFloat(0.08f, 0.34f)),
                Main.rand.NextFloat(0.42f, 0.72f) * Projectile.scale, Main.rand.Next(11, 18), 0.07f));
        }

        if (Main.rand.NextBool(7))
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.TintableDustLighted,
                -Projectile.velocity * 0.055f + Main.rand.NextVector2Circular(0.65f, 0.65f),
                70, tint, Main.rand.NextFloat(0.58f, 0.92f) * Projectile.scale);
            dust.noGravity = false;
        }
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) =>
        SpawnImpact(Projectile.velocity.SafeNormalize(Vector2.UnitX));

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        SpawnImpact(oldVelocity.SafeNormalize(Vector2.UnitX));
        return true;
    }

    public override void OnKill(int timeLeft)
    {
        if (!impactPlayed)
            SpawnImpact(Projectile.velocity.SafeNormalize(Vector2.UnitX));
    }

    private void SpawnImpact(Vector2 incoming)
    {
        if (impactPlayed)
            return;
        impactPlayed = true;
        if (Main.dedServ)
            return;

        Color tint = Tint;
        Vector2 rebound = -incoming.SafeNormalize(-Vector2.UnitY);
        int dropletCount = 7;
        for (int i = 0; i < dropletCount; i++)
        {
            Vector2 velocity = rebound.RotatedByRandom(1.18f) * Main.rand.NextFloat(2.2f, 7.2f);
            Color particleColor = i % 4 == 0
                ? Color.Lerp(tint, Color.White, 0.72f)
                : Color.Lerp(tint, Color.White, Main.rand.NextFloat(0.04f, 0.24f));
            FranParticleSystem.Spawn(new FranTacticalDropletParticle(Projectile.Center, velocity,
                particleColor, Main.rand.NextFloat(0.58f, 1.02f) * Projectile.scale,
                Main.rand.Next(14, 25), Main.rand.NextFloat(0.12f, 0.19f)));
        }

        for (int i = 0; i < 3; i++)
        {
            Vector2 velocity = rebound.RotatedByRandom(0.82f) * Main.rand.NextFloat(3.8f, 7.6f);
            FranParticleSystem.Spawn(new FranLineParticle(Projectile.Center, velocity,
                i == 0 ? Color.White : tint, Main.rand.NextFloat(18f, 34f),
                Main.rand.NextFloat(1.0f, 2.1f), Main.rand.Next(8, 13)));
        }

        FranParticleSystem.Spawn(new FranGlowOrbParticle(Projectile.Center, Vector2.Zero, tint,
            0.18f * Projectile.scale, 0.02f, 10, true));
        for (int i = 0; i < 5; i++)
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.TintableDustLighted,
                rebound.RotatedByRandom(1.1f) * Main.rand.NextFloat(1.2f, 5.2f),
                65, tint, Main.rand.NextFloat(0.72f, 1.12f));
            dust.noGravity = i % 2 == 0;
        }

        SoundEngine.PlaySound(SoundID.NPCHit18 with
        {
            Volume = 0.18f,
            Pitch = 0.18f,
            PitchVariance = 0.16f,
            MaxInstances = 5
        }, Projectile.Center);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (Main.dedServ)
            return false;

        Color tint = Tint;
        float fadeIn = MathHelper.Clamp(Age / 4f, 0f, 1f);
        float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
        float fade = Math.Min(fadeIn, fadeOut);
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        Color drawColor = Color.Lerp(Color.White, tint, 0.58f);

        // Only a few faint BOLT impressions remain behind the projectile. This keeps the silhouette clean
        // and removes the old WaterGlob + NoiseSharp + SharpTears + bloom stack.
        for (int i = Math.Min(8, Projectile.oldPos.Length - 1); i >= 2; i -= 2)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                continue;
            float strength = 1f - i / 10f;
            DrawBolt(texture, Projectile.oldPos[i] + Projectile.Size * 0.5f, Projectile.oldRot[i],
                drawColor * (fade * strength * 0.20f), Projectile.scale * (0.72f + strength * 0.12f));
        }

        DrawBolt(texture, Projectile.Center, Projectile.rotation, drawColor * fade, Projectile.scale);
        return false;
    }

    private static void DrawBolt(Texture2D texture, Vector2 worldPosition, float rotation, Color color, float scale)
    {
        Main.spriteBatch.Draw(texture, worldPosition - Main.screenPosition, null, color,
            rotation + MathHelper.PiOver2, texture.Size() * 0.5f,
            new Vector2(0.82f, 0.94f) * scale, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// 在 direction 前方随机散射一束战术液滴。color 省略或传 null 时使用白色。
    /// 生成权只交给弹幕拥有者，避免同一个命中在联机中被客户端重复生成多束。
    /// </summary>
    internal static void SpawnCone(IEntitySource source, Vector2 position, Vector2 direction, int count,
        float totalSpread, float minSpeed, float maxSpeed, int damage, float knockback, int owner,
        Color? color = null)
    {
        if (owner != Main.myPlayer || count <= 0)
            return;

        direction = direction.SafeNormalize(Vector2.UnitX);
        Color tint = color ?? Color.White;
        float lowSpeed = Math.Max(0.1f, Math.Min(minSpeed, maxSpeed));
        float highSpeed = Math.Max(lowSpeed, Math.Max(minSpeed, maxSpeed));
        for (int i = 0; i < count; i++)
        {
            // totalSpread 表示整束的总夹角，例如传 30 度时实际范围为中心线左右各 15 度。
            Vector2 velocity = direction.RotatedByRandom(Math.Abs(totalSpread) * 0.5f) *
                Main.rand.NextFloat(lowSpeed, highSpeed);
            velocity += direction.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-0.45f, 0.45f);
            Spawn(source, position + Main.rand.NextVector2Circular(4f, 4f), velocity,
                damage, knockback, owner, tint, Main.rand.NextFloat(0.88f, 1.14f));
        }
    }

    private static void Spawn(IEntitySource source, Vector2 position, Vector2 velocity,
        int damage, float knockback, int owner, Color tint, float scale)
    {
        int index = Projectile.NewProjectile(source, position, velocity,
            ModContent.ProjectileType<FranTacticalEjecta>(), Math.Max(1, damage), knockback, owner,
            tint.R / 255f, tint.G / 255f, tint.B / 255f);
        if (!Main.projectile.IndexInRange(index))
            return;

        Main.projectile[index].scale = MathHelper.Clamp(scale, 0.72f, 1.35f);
        Main.projectile[index].netUpdate = true;
    }
}
