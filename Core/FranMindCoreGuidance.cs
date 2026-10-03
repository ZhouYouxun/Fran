using System;
using Microsoft.Xna.Framework;

namespace FranMod.Core;

// 念核先估计短距离交会点，再用旧追踪算法的速度与转向预算约束它。
internal static class FranMindCoreGuidance
{
    internal const float AcquisitionRadius = 400f;
    internal const float PursuitSpeed = 24f;
    private const float Inertia = 20f;

    internal static Vector2 Steer(Vector2 velocity, Vector2 offset, Vector2 targetVelocity,
        int updatesPerTick, float pursuitSpeed)
    {
        float distance = offset.Length();
        Vector2 direction = distance > 0.0001f ? offset / distance : Vector2.UnitY;
        Vector2 direct = (velocity * Inertia + direction * pursuitSpeed) / (Inertia + 1f);
        float speed = velocity.Length();
        if (speed < 0.0001f || direct.LengthSquared() < 0.000001f)
            return direct;

        // NPC.velocity 按游戏帧计，弹幕 velocity 按子更新计；预瞄不得多乘 extraUpdates。
        float leadTime = Math.Min(6f, distance / (Math.Max(speed, pursuitSpeed) * Math.Max(1, updatesPerTick)) * 0.35f);
        Vector2 lead = targetVelocity * leadTime * Math.Clamp(distance / 64f, 0f, 1f);
        float leadLength = lead.Length();
        if (leadLength < 0.0001f)
            return direct;
        if (leadLength > 48f)
            lead *= 48f / leadLength;

        Vector2 desired = offset + lead;
        if (desired.LengthSquared() < 0.000001f)
            return direct;
        float oldTurn = Math.Abs(SignedAngle(velocity, direct));
        float turn = Math.Clamp(SignedAngle(velocity, desired), -oldTurn, oldTurn);
        float cosine = MathF.Cos(turn);
        float sine = MathF.Sin(turn);
        return new Vector2(velocity.X * cosine - velocity.Y * sine,
            velocity.X * sine + velocity.Y * cosine) * (direct.Length() / speed);
    }

    private static float SignedAngle(Vector2 from, Vector2 to) =>
        MathF.Atan2(from.X * to.Y - from.Y * to.X, Vector2.Dot(from, to));
}
