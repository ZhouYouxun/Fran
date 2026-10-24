using System;
using System.Collections.Generic;
using FranMod.Core.LeonidStarlight;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Effects.LeonidStarlight
{
    // 狮子座星光特效的统一入口。
    //
    // 星光使用 Fran 本地粒子系统：
    //   · 纯客户端，不占弹幕槽位、不走网络同步（这把武器本身已经会同时刷很多弹幕）；
    //   · 从 AI/OnKill 里调用即可，这些回调在每个客户端都会跑，所有人都看得到。
    // 星光本身不造成任何伤害，只做点缀，不参与数值平衡。
    //
    // 绘制约定（本项目反复踩过的坑）：
    //   · 粒子由处理器用 BlendState.Additive 开批次 → 保留 alpha，整体乘不透明度；
    //   · 本文件给弹幕 PreDraw 用的 Draw* 助手跑在默认 AlphaBlend 批次里
    //     → 贴图是预乘 alpha 的，把 A 设成 0 就等价于加法混合，黑底自然消失。
    public static class LeonidStarlight
    {
        private static uint trackerReservationFrame;
        private static readonly List<Vector2> TrackerReservationCenters = new();

        private const float TrackerGroupRadius = 96f;
        private const float SpawnKeepChance = 0.8f;

        // 换世界时把星光的静态状态归零。
        internal static void ResetStaticState()
        {
            LeonidStarlightMote.ClearRegistry();
            trackerReservationFrame = 0u;
            TrackerReservationCenters.Clear();
        }

        private static Color DefaultColor(float phaseOffset) => LeonidVisualUtils.GetMeteorColor(phaseOffset);

        private static bool CanSpawn(int wanted) =>
            !Main.dedServ && FranParticleSystem.FreeSlots > wanted;

        // Preserve an exact 80% average even for tiny groups. The occasional one-star
        // difference also keeps repeated releases from looking mechanically identical.
        private static int ReducedCount(int originalCount)
        {
            if (originalCount <= 0)
                return 0;

            float desiredCount = originalCount * SpawnKeepChance;
            int reducedCount = (int)MathF.Floor(desiredCount);
            if (Main.rand.NextFloat() < desiredCount - reducedCount)
                reducedCount++;
            return reducedCount;
        }

        // Calls made on the same frame and near the same origin belong to one visual
        // release. Only the first batch in that release may assign a homing mote.
        private static bool TryReserveTracker(Vector2 center)
        {
            if (trackerReservationFrame != Main.GameUpdateCount)
            {
                trackerReservationFrame = Main.GameUpdateCount;
                TrackerReservationCenters.Clear();
            }

            float groupRadiusSquared = TrackerGroupRadius * TrackerGroupRadius;
            foreach (Vector2 reservedCenter in TrackerReservationCenters)
            {
                if (Vector2.DistanceSquared(center, reservedCenter) <= groupRadiusSquared)
                    return false;
            }

            TrackerReservationCenters.Add(center);
            return true;
        }

        private static void SpawnCore(
            Vector2 position,
            Vector2 velocity,
            Color color,
            LeonidStarlightShape shape,
            float scale,
            int hoverTime,
            int lifetime,
            float lanceSpeed,
            float homingRange,
            bool linksToSiblings,
            LeonidStarlightMotion motion,
            float motionPhase)
        {
            if (!CanSpawn(1))
                return;

            FranParticleSystem.Spawn(new LeonidStarlightMote(
                position, velocity, color, shape, scale, hoverTime, lifetime, lanceSpeed, homingRange,
                linksToSiblings, motion, motionPhase), spawnSupportingDust: false);
        }

        // ── 生成 ───────────────────────────────────────────────────

        // 四散爆开：流星炸开、狮首咆哮这类"从一点向外炸"的场合。
        // 每颗的悬停时长有随机偏移，锁定不会整齐划一地同时触发。
        public static void Burst(
            Vector2 center,
            int count,
            Color? color = null,
            LeonidStarlightShape shape = LeonidStarlightShape.Mote,
            float speed = 6f,
            float scale = 1f,
            int hoverTime = 26,
            int lifetime = 150,
            float lanceSpeed = 17f,
            float spawnRadius = 6f)
        {
            count = ReducedCount(count);
            if (count <= 0 || !CanSpawn(count))
                return;

            int trackerIndex = TryReserveTracker(center) ? Main.rand.Next(count) : -1;

            for (int i = 0; i < count; i++)
            {
                Color tint = color ?? DefaultColor(i * 0.21f);
                SpawnCore(
                    center + Main.rand.NextVector2Circular(spawnRadius, spawnRadius),
                    Main.rand.NextVector2Circular(speed, speed) * Main.rand.NextFloat(0.55f, 1f),
                    tint,
                    shape,
                    scale * Main.rand.NextFloat(0.75f, 1.25f),
                    hoverTime + Main.rand.Next(-6, 13),
                    lifetime + Main.rand.Next(-20, 21),
                    lanceSpeed * Main.rand.NextFloat(0.85f, 1.2f),
                    760f,
                    true,
                    i == trackerIndex ? LeonidStarlightMotion.Homing : LeonidStarlightMotion.BurstArc,
                    i * MathHelper.TwoPi / count + Main.rand.NextFloat(-0.18f, 0.18f));
            }
        }

        // 锥形喷射：沿着某个方向甩出去，用在发射/挥击的瞬间。
        public static void Spray(
            Vector2 center,
            Vector2 direction,
            int count,
            Color? color = null,
            LeonidStarlightShape shape = LeonidStarlightShape.Mote,
            float speed = 7f,
            float spread = 0.75f,
            float scale = 1f,
            int hoverTime = 22,
            int lifetime = 140,
            float lanceSpeed = 17f,
            bool guaranteeCount = false)
        {
            count = guaranteeCount ? count : ReducedCount(count);
            if (count <= 0 || !CanSpawn(count))
                return;

            direction = direction.SafeNormalize(Vector2.UnitY);
            int trackerIndex = TryReserveTracker(center) ? Main.rand.Next(count) : -1;
            for (int i = 0; i < count; i++)
            {
                Color tint = color ?? DefaultColor(i * 0.17f);
                Vector2 velocity = direction.RotatedByRandom(spread) * speed * Main.rand.NextFloat(0.6f, 1.15f);

                SpawnCore(
                    center + Main.rand.NextVector2Circular(5f, 5f),
                    velocity,
                    tint,
                    shape,
                    scale * Main.rand.NextFloat(0.8f, 1.2f),
                    hoverTime + Main.rand.Next(-5, 10),
                    lifetime,
                    lanceSpeed,
                    760f,
                    true,
                    i == trackerIndex ? LeonidStarlightMotion.Homing : LeonidStarlightMotion.SprayFan,
                    i * MathHelper.TwoPi / count + Main.rand.NextFloat(-0.16f, 0.16f));
            }
        }

        // 每次挥刀固定七颗（原来两颗，增加 250%）：中心星 + 三组镜像弧线。
        // 半径按黄金比例递进，角度按层展开，避免增加数量后随机堆在同一点。
        public static void SwingFan(Vector2 center, Vector2 direction)
        {
            const int count = 7;
            if (!CanSpawn(count))
                return;

            direction = direction.SafeNormalize(Vector2.UnitX);
            bool tracker = TryReserveTracker(center);
            SpawnCore(center, direction * 5.7f, DefaultColor(0f), LeonidStarlightShape.Mote,
                0.76f, 22, 140, 17f, 760f, true,
                tracker ? LeonidStarlightMotion.Homing : LeonidStarlightMotion.SwingArc, 0f);

            const float goldenRatio = 1.618034f;
            for (int layer = 0; layer < 3; layer++)
            {
                float radius = 3f * MathF.Pow(goldenRatio, layer);
                float angle = 0.16f + layer * 0.16f;
                float speed = 5.2f + layer * 0.75f;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    // 交错形态：总计四颗小十字星、三颗四芒星。
                    bool shard = (layer + (sign + 1) / 2) % 2 == 0;
                    Vector2 ray = direction.RotatedBy(sign * angle);
                    Vector2 position = center + direction * radius +
                        direction.RotatedBy(MathHelper.PiOver2) * (sign * radius);
                    SpawnCore(position, ray * speed, DefaultColor((layer + 1) * 0.17f),
                        shard ? LeonidStarlightShape.Shard : LeonidStarlightShape.Mote,
                        shard ? 0.64f : 0.76f, 22 + layer * 3, 140, 17f, 760f, true,
                        LeonidStarlightMotion.SwingArc, sign * (layer + 1));
                }
            }
        }

        // 「盲目正义」的历史星芒叠绘思路：尾部平方淡出、逐段缩小并扭转。
        // 使用独立生成的贴图；同时支持弹幕 AlphaBlend 与粒子 Additive 批次。
        internal static void DrawStarTrail(SpriteBatch spriteBatch, Texture2D texture,
            Vector2[] history, Vector2 centerOffset, Color innerColor, Color outerColor,
            float opacity, float scale, float rotation, int step = 2, float twist = 0.18f,
            bool additive = false)
        {
            if (history is null || history.Length == 0 || opacity <= 0.002f)
                return;

            for (int i = history.Length - 1; i >= 0; i -= Math.Max(1, step))
            {
                if (history[i] == Vector2.Zero)
                    continue;
                float progress = 1f - i / (float)history.Length;
                Color tint = Color.Lerp(outerColor, innerColor, progress);
                if (!additive)
                    tint.A = 0;
                spriteBatch.Draw(texture, history[i] + centerOffset - Main.screenPosition,
                    null, tint * (opacity * progress * progress), rotation + i * twist,
                    texture.Size() * 0.5f, scale * (0.3f + 0.7f * progress),
                    SpriteEffects.None, 0f);
            }
        }

    }

    internal sealed class LeonidStarlightResetSystem : ModSystem
    {
        public override void OnWorldUnload() => LeonidStarlight.ResetStaticState();
        public override void Unload()
        {
            LeonidStarlight.ResetStaticState();
            LeonidStarlightMaterials.Release();
        }
    }
}
