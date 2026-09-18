using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace FranMod.Core.Graphics;

// 电灼长戟的宽肩尾迹/双轨粒子 + 矩阵核心的四维旋转投影。
// 所有坐标都在世界空间；使用 AlphaBlend 的 A=0 光线，不切换外部 SpriteBatch。
internal static class FranPhantomVisuals
{
    internal const int ImpactLifetime = 44;
    internal const float ImpactIntensity = 0.70f;
    internal static readonly Color Blue = new(40, 125, 255);
    internal static readonly Color Cyan = new(95, 220, 255);
    private static readonly Rectangle Pixel = new(0, 0, 1, 1);

    private static Color Light(Color color, float opacity) =>
        new Color(color.R, color.G, color.B, 0) * MathHelper.Clamp(opacity, 0f, 1f);

    private static void Line(Vector2 a, Vector2 b, Color color, float width)
    {
        Vector2 delta = b - a;
        float length = delta.Length();
        if (!float.IsFinite(length) || length < 0.01f || length > 1200f) return;
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, a - Main.screenPosition, Pixel, color,
            MathF.Atan2(delta.Y, delta.X), new Vector2(0f, 0.5f), new Vector2(length, width), SpriteEffects.None, 0f);
    }

    private static void Texture(string name, Vector2 center, Vector2 size, float angle, Color color)
    {
        Texture2D texture = ModContent.Request<Texture2D>("Fran/Particles/" + name).Value;
        Main.spriteBatch.Draw(texture, center - Main.screenPosition, null, color, angle,
            new Vector2(texture.Width, texture.Height) * 0.5f,
            size / new Vector2(texture.Width, texture.Height), SpriteEffects.None, 0f);
    }

    private static Vector2 Axis(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

    internal static Vector2 PortalPoint(Vector2 center, float angle, float phase, float radius) =>
        center + Axis(angle) * (MathF.Cos(phase) * radius * 0.30f) +
        Axis(angle + MathHelper.PiOver2) * (MathF.Sin(phase) * radius);

    internal static void DrawPortal(Vector2 center, float angle, float age, float launchDelay)
    {
        float open = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(age / 7f, 0f, 1f));
        float close = 1f - MathHelper.Clamp((age - launchDelay) / 8f, 0f, 1f);
        float visibility = open * close;
        if (visibility <= 0f) return;
        float radius = 52f * visibility;
        Texture("BloomCircle", center, new Vector2(radius * 0.72f, radius * 2.1f), angle,
            new Color(3, 10, 30) * (0.88f * visibility));
        // 两道透视门框与断续旋转刻度，中央暗面留出钻出剑体的空间。
        for (int layer = 0; layer < 2; layer++)
        {
            float r = radius * (1f + layer * 0.15f);
            for (int i = 0; i < 48; i++)
            {
                if (layer == 1 && i % 6 >= 4) continue;
                float phase = i * MathHelper.TwoPi / 48f + age * (layer == 0 ? 0.025f : -0.035f);
                Vector2 a = PortalPoint(center, angle, phase, r);
                Vector2 b = PortalPoint(center, angle, phase + MathHelper.TwoPi / 48f, r);
                Line(a, b, Light(Blue, 0.22f * visibility), layer == 0 ? 7f : 4f);
                Line(a, b, Light(layer == 0 ? Cyan : Blue, 0.82f * visibility), layer == 0 ? 1.8f : 1.2f);
            }
        }
        for (int i = 0; i < 6; i++)
        {
            float phase = i * MathHelper.TwoPi / 6f - age * 0.045f;
            Vector2 node = PortalPoint(center, angle, phase, radius * 1.15f);
            Texture("GlowSpark", node, new Vector2(5f, 16f) * visibility,
                angle, Light(Cyan, 0.7f * visibility));
        }
    }

    internal static void DrawFlight(Vector2 center, float angle, float age, float travelDistance, int seed)
    {
        Vector2 forward = Axis(angle);
        Vector2 normal = Axis(angle + MathHelper.PiOver2);
        float length = Math.Min(360f, travelDistance);
        if (length < 1f) return;
        // 在宽肩纹理拖尾外再绘制双轨电流，颜色固定为蓝/青，时间只影响形状。
        for (int strand = 0; strand < 2; strand++)
        {
            Vector2 previous = center;
            for (int i = 0; i <= 32; i++)
            {
                float t = i / 32f;
                float envelope = MathF.Sin(MathF.PI * t) * (1f - t * 0.55f);
                float phase = t * 11f - age * 0.27f + seed * 0.71f + strand * MathF.PI;
                Vector2 next = center - forward * (length * t) + normal * (MathF.Sin(phase) * 24f * envelope);
                if (i > 0)
                {
                    Line(previous, next, Light(Blue, 0.22f * (1f - t)), 7f * (1f - t) + 1f);
                    Line(previous, next, Light(Cyan, 0.80f * (1f - t)), 1.8f * (1f - t) + 0.5f);
                }
                previous = next;
            }
        }
        Texture("ForwardSmear", center - forward * 54f, new Vector2(150f, 52f), angle, Light(Blue, 0.72f));
    }

    private static void Rotate(ref float a, ref float b, float angle)
    {
        float oldA = a;
        a = oldA * MathF.Cos(angle) - b * MathF.Sin(angle);
        b = oldA * MathF.Sin(angle) + b * MathF.Cos(angle);
    }

    internal static float Variation(int seed, int channel)
    {
        uint bits = unchecked((uint)seed * 747796405u + (uint)channel * 2891336453u + 277803737u);
        bits = ((bits >> (int)((bits >> 28) + 4)) ^ bits) * 277803737u;
        return ((bits >> 22) ^ bits) / (float)uint.MaxValue;
    }

    internal static float MatrixScale(float age) => MathHelper.Lerp(0.10f, 0.20f,
        MathHelper.Clamp(age / ImpactLifetime, 0f, 1f));

    internal static float ImpactSpread(int seed, int channel = 200) => 2f + Variation(seed, channel) * 2f;

    internal static void ProjectMatrix(Span<Vector2> vertices, Vector2 center, float angle, float age, int seed)
    {
        float progress = MathHelper.Clamp(age / ImpactLifetime, 0f, 1f);
        // 沿用上版半径曲线，初始缩至 10%，最终缩至 20%。
        float strike = 1f - MathF.Pow(1f - MathHelper.Clamp(age / 4f, 0f, 1f), 3f);
        float spread = MathHelper.Lerp(1f, ImpactSpread(seed), MathHelper.SmoothStep(0f, 1f, progress));
        float radius = (64f + 37f * strike + 43f * progress) * MatrixScale(age) * spread;
        float time = age * (0.008f + Variation(seed, 1) * 0.012f) + Variation(seed, 2) * MathHelper.TwoPi;
        Matrix rotation = Matrix.CreateFromYawPitchRoll(Variation(seed, 3) * 6f + time * 0.7f,
            Variation(seed, 4) * 6f + time * 0.5f, Variation(seed, 5) * 6f + time * 0.2f);
        Vector2 forward = Axis(angle);
        Vector2 normal = Axis(angle + MathHelper.PiOver2);
        for (int i = 0; i < 16; i++)
        {
            Vector4 point = new((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f,
                (i & 4) == 0 ? -1f : 1f, (i & 8) == 0 ? -1f : 1f);
            Rotate(ref point.X, ref point.W, time * 0.83f);
            Rotate(ref point.Y, ref point.Z, time * 0.54f);
            Rotate(ref point.Z, ref point.W, time * 0.37f);
            float perspective4 = 2.4f / Math.Max(1.2f, 3.4f - point.W);
            Vector3 p = Vector3.Transform(new Vector3(point.X, point.Y, point.Z) * perspective4, rotation);
            float perspective3 = 5f / Math.Max(2.5f, 5f + p.Z);
            // 冲击方向拉长，中心向前推进，避免均匀的圆周散射。
            vertices[i] = center + forward * ((32f * strike + progress * 28f) * MatrixScale(age) * spread) +
                (forward * (p.X * 1.15f) + normal * (p.Y * 0.82f)) * (radius * perspective3);
        }
    }

    // 命中专用：烟冠采用现有完整烟雾材质；矩阵和碎片始终逐顶点投影、逐边绘制。
    private static void ImpactCloud(Vector2 center, float radius, float angle, float progress, int variant, Color color)
    {
        Texture2D texture = ModContent.Request<Texture2D>("Fran/Particles/FranHeavySmokeParticle").Value;
        // 该资源是单张烟团，不是 80px 分帧图集，切块会露出方形边界。
        Vector2 size = new(texture.Width, texture.Height);
        Main.spriteBatch.Draw(texture, center - Main.screenPosition, null, color, angle + progress * .25f,
            size * .5f, new Vector2(radius * 2.3f, radius * 1.8f) / size,
            variant % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
    }

    private static void ImpactArc(Vector2 center, float radius, float start, float sweep,
        float squash, float tilt, Color color, float width)
    {
        Vector2 forward = Axis(tilt), side = Axis(tilt + MathHelper.PiOver2);
        Vector2 previous = center + (forward * MathF.Cos(start) + side * (MathF.Sin(start) * squash)) * radius;
        const int segments = 28;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float phase = start + sweep * t;
            Vector2 next = center + (forward * MathF.Cos(phase) + side * (MathF.Sin(phase) * squash)) * radius;
            float taper = MathF.Sin(t * MathHelper.Pi);
            Line(previous, next, color * (taper * .22f), width * 3.5f);
            Line(previous, next, color * taper, width);
            previous = next;
        }
    }

    private static void ImpactShard(Vector2 center, float radius, float age, int seed, float fade)
    {
        Span<Vector2> points = stackalloc Vector2[8];
        Matrix rotation = Matrix.CreateFromYawPitchRoll(Variation(seed, 301) * 6f + age * .055f,
            Variation(seed, 302) * 6f + age * .038f, Variation(seed, 303) * 6f - age * .025f);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f,
                (i & 4) == 0 ? -.6f : .6f);
            Vector3 point = Vector3.Transform(corner, rotation);
            points[i] = center + new Vector2(point.X, point.Y) * (radius * 4f / Math.Max(2f, 4f + point.Z));
        }
        for (int i = 0; i < 8; i++)
            for (int bit = 0; bit < 3; bit++)
            {
                int other = i ^ (1 << bit);
                if (other < i) continue;
                Line(points[i], points[other], new Color(2, 9, 26) * (fade * .7f), 2.8f);
                Line(points[i], points[other], Light(bit == 2 ? Cyan : Blue, fade * .75f), .8f);
            }
    }

    internal static void DrawMatrixImpact(Vector2 center, float angle, float age, int seed)
    {
        float progress = MathHelper.Clamp(age / ImpactLifetime, 0f, 1f);
        if (progress >= 1f) return;
        float fade = MathF.Pow(1f - progress, 1.4f) * ImpactIntensity;
        // 快速撑开、缓慢散去：烟冠/魔力丝/冲击弧各用自己的扩张时钟。
        float burst = 1f - MathF.Pow(1f - MathHelper.Clamp(age / 18f, 0f, 1f), 3f);
        float spread = ImpactSpread(seed);
        float radius = (4f + 23f * burst + 8f * progress) * spread;
        Vector2 forward = Axis(angle);
        Vector2 core = center + forward * (radius * .19f);
        float bodyFade = fade * (1f - MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((age - 15f) / 22f, 0f, 1f)));

        // 黑键式不规则烟冠，方向和瓣大小由种子固定，避免逐帧随机闪动。
        for (int lobe = 0; lobe < 7; lobe++)
        {
            float phase = angle + lobe * MathHelper.TwoPi / 7f + (Variation(seed, 320 + lobe) - .5f) * .65f;
            float local = MathHelper.Clamp((age - Variation(seed, 330 + lobe) * 3f) / 25f, 0f, 1f);
            float expansion = 1f - MathF.Pow(1f - local, 3f);
            float reach = (6f + 22f * expansion) * ImpactSpread(seed, 340 + lobe);
            // 前方烟瓣更远，后方烟瓣收紧，保留剑砸穿敌人的方向感。
            reach *= .78f + .22f * MathF.Cos(phase - angle);
            Vector2 plume = core + Axis(phase) * reach * .72f;
            float size = (8f + radius * (.20f + Variation(seed, 350 + lobe) * .13f)) * (1f - progress * .35f);
            ImpactCloud(plume, size, phase + local, local, lobe, new Color(4, 13, 34) * (bodyFade * .82f));
            ImpactCloud(plume - Axis(phase) * size * .12f, size * .87f, phase + local, local, lobe,
                Light(Blue, bodyFade * (.17f + Variation(seed, 360 + lobe) * .13f)));
            ImpactArc(plume, size * .72f, phase - 1.8f + local, 2.2f, .72f, angle,
                Light(Cyan, bodyFade * .32f), .9f);
        }
        Texture("BloomCircle", core, new Vector2(radius * 1.65f, radius * 1.28f), angle,
            new Color(3, 10, 28) * (bodyFade * .82f));
        Texture("BloomCircle", core, new Vector2(radius * 1.28f, radius * .97f), angle + progress * .4f,
            Light(Blue, bodyFade * .24f));
        // 小而集中的青色爆心只亮一次，外层始终保留深蓝层次。
        float punch = MathF.Pow(1f - MathHelper.Clamp(age / 10f, 0f, 1f), 1.5f);
        Texture("GlowSpark", center, new Vector2(12f + 10f * punch, 35f + 28f * punch),
            angle + MathHelper.PiOver2, Light(Cyan, punch * .85f * ImpactIntensity));
        Texture("SlashSmear", core, new Vector2(72f + burst * 35f, 32f), angle - .55f,
            Light(Cyan, punch * .7f * ImpactIntensity));

        // 深靛式弯曲魔力丝：宽底色衬细线，分瓣速度/弯曲方向错开。
        for (int petal = 0; petal < 6; petal++)
        {
            float local = MathHelper.Clamp((age - Variation(seed, 370 + petal) * 4f) / 32f, 0f, 1f);
            if (local <= 0f || local >= 1f) continue;
            float expansion = 1f - MathF.Pow(1f - local, 3f);
            float petalFade = MathF.Pow(1f - local, 1.3f) * ImpactIntensity;
            float phase = angle + petal * MathHelper.TwoPi / 6f + (Variation(seed, 380 + petal) - .5f) * .8f;
            float reach = (5f + 30f * expansion) * ImpactSpread(seed, 390 + petal) *
                (.80f + .20f * MathF.Cos(phase - angle));
            float curl = (petal % 2 == 0 ? 1f : -1f) * (.6f + Variation(seed, 400 + petal) * .8f);
            Vector2 previous = core;
            for (int segment = 1; segment <= 14; segment++)
            {
                float t = segment / 14f;
                float curve = phase + MathF.Sin(t * MathHelper.Pi) * curl * (1f - local * .5f);
                Vector2 next = core + Axis(curve) * (reach * t);
                float strength = MathF.Sin(t * MathHelper.Pi) * petalFade;
                Line(previous, next, Light(Blue, strength * .24f), 4f * (1f - t) + 1f);
                Line(previous, next, Light(Cyan, strength * .74f), 1.15f - t * .45f);
                previous = next;
            }
        }
        // 和弦式错相冲击波：并非匀速同心圆，各层有独立延迟、倾斜和缺口。
        for (int wave = 0; wave < 3; wave++)
        {
            float local = MathHelper.Clamp((age - wave * 3f) / (23f + wave * 4f), 0f, 1f);
            if (local <= 0f || local >= 1f) continue;
            float waveRadius = (4f + (28f - wave * 3f) * (1f - MathF.Pow(1f - local, 3f))) *
                ImpactSpread(seed, 410 + wave);
            float waveFade = MathF.Pow(1f - local, 1.25f) * ImpactIntensity;
            ImpactArc(core + forward * (waveRadius * .16f), waveRadius,
                -.9f + Variation(seed, 420 + wave) * 1.6f, 4.3f + Variation(seed, 430 + wave),
                .60f + wave * .1f, angle + (wave - 1) * .45f,
                Light(wave == 1 ? Blue : Cyan, waveFade * .75f), 1.5f - wave * .25f);
        }

        // 真实四维投影作为爆心骨架；展开后断裂，让烟冠和魔力流承担爆炸主体。
        Span<Vector2> vertices = stackalloc Vector2[16];
        ProjectMatrix(vertices, center, angle, age, seed);
        float matrixFade = fade * (1f - MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((age - 10f) / 25f, 0f, 1f)));
        float fracture = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((age - 6f) / 19f, 0f, 1f));
        for (int vertex = 0; vertex < 16; vertex++)
            for (int bit = 0; bit < 4; bit++)
            {
                int other = vertex ^ (1 << bit);
                if (other < vertex) continue;
                float variation = Variation(seed, vertex * 4 + bit + 20);
                Vector2 a = vertices[vertex], b = vertices[other];
                Vector2 end = Vector2.Lerp(a, b, 1f - fracture * (.35f + variation * .45f));
                Line(a, end, Light(Blue, .14f * matrixFade), 3f);
                Line(a, end, Light(bit == 3 ? Cyan : Blue, (.35f + variation * .3f) * matrixFade), .9f);
            }

        for (int shard = 0; shard < 5; shard++)
        {
            float local = MathHelper.Clamp((age - 4f - Variation(seed, 440 + shard) * 5f) / 32f, 0f, 1f);
            if (local <= 0f || local >= 1f) continue;
            float shardAngle = angle + (Variation(seed, 450 + shard) - .5f) * 2.8f;
            Vector2 axis = Axis(shardAngle);
            float reach = (5f + (22f + Variation(seed, 460 + shard) * 12f) *
                (1f - MathF.Pow(1f - local, 2f))) * ImpactSpread(seed, 470 + shard);
            Vector2 position = center + axis * reach;
            float shardFade = MathF.Sin(MathHelper.Clamp(local * 5f, 0f, 1f) * MathHelper.PiOver2) *
                MathF.Pow(1f - local, 1.2f);
            ImpactShard(position, (2.5f + Variation(seed, 480 + shard) * 3f) * (1f - local * .35f),
                age, seed + shard * 31, shardFade * ImpactIntensity);
            Line(position - axis * (7f * shardFade), position, Light(Cyan, shardFade * .7f * ImpactIntensity), .9f);
        }
    }
}
