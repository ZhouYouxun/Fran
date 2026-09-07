using System;
using System.IO;
using Microsoft.Xna.Framework;

namespace FranMod.Content.Projectiles;

internal enum TelekineticSkirmishPhase : byte
{
    Scatter,
    Capture,
    Sweep,
    Strike,
    Coast,
    Finished
}

internal readonly record struct TelekineticSkirmishTarget(Vector2 Center, Vector2 Velocity, float Radius);

/// <summary>
/// 念压核的目标相对运动。所有时间以正常帧计；只计算速度，不瞬移、不产生伤害或子弹幕。
/// 编队左右两翼沿相反方向掠近，锁定目标后只允许一次脱靶回勾。
/// </summary>
internal sealed class FranTelekineticSkirmishMotion
{
    public const int MaximumLifetime = 280;
    private const int CaptureTimeout = 48;
    private const int SweepTimeout = 25;
    private const int StrikeTimeout = 44;
    private const int CoastFrames = 10;
    private const float CaptureTurnLimit = 0.12f;
    private const float StrikeTurnLimit = 0.15f;

    public TelekineticSkirmishPhase Phase { get; private set; }
    public int Lane { get; private set; }
    public bool Returned { get; private set; }
    public bool WantsTarget => Phase == TelekineticSkirmishPhase.Capture;
    public bool Finished => Phase == TelekineticSkirmishPhase.Finished;
    private int phaseAge;
    private float lastBearing;
    private float sweptAngle;
    private float closestStrikeDistance = float.MaxValue;
    private bool bearingInitialized;
    private int LaneRank => Math.Min(Math.Abs(Lane), 4);
    private float OrbitSign => Lane < 0 ? 1f : -1f;

    public void ConfigureLane(int lane) => Lane = Math.Clamp(lane, -8, 8);

    public Vector2 Step(Vector2 position, Vector2 velocity, Vector2 launchDirection,
        float nominalSpeed, int scatterFrames, TelekineticSkirmishTarget? target)
    {
        nominalSpeed = Math.Clamp(nominalSpeed, 4f, 45f);
        phaseAge++;
        if (Phase == TelekineticSkirmishPhase.Scatter)
        {
            float spread = Lane == 0 ? 0.08f : Math.Sign(Lane) * (0.18f + LaneRank * 0.065f);
            Vector2 heading = Rotate(Unit(launchDirection, Vector2.UnitX), spread);
            if (phaseAge >= Math.Clamp(scatterFrames + LaneRank, 8, 12))
                Enter(TelekineticSkirmishPhase.Capture);
            return heading * nominalSpeed;
        }

        if (Phase == TelekineticSkirmishPhase.Coast)
        {
            // 脱靶先滑出十帧；不允许当场掉头，也不重新无限追踪。
            if (phaseAge >= CoastFrames)
            {
                Returned = true;
                Enter(TelekineticSkirmishPhase.Capture);
            }
            return velocity * 0.988f;
        }

        if (Finished)
            return velocity;

        if (target is not TelekineticSkirmishTarget focus)
        {
            if (Phase != TelekineticSkirmishPhase.Capture)
                LoseTarget();
            else if (phaseAge >= 20)
                Enter(TelekineticSkirmishPhase.Finished);
            return velocity * 0.992f;
        }

        Vector2 radial = position - focus.Center;
        float distance = radial.Length();
        Vector2 outward = Unit(radial, -Unit(velocity, launchDirection));
        Vector2 tangent = new Vector2(-outward.Y, outward.X) * OrbitSign;
        float orbitRadius = Math.Clamp(focus.Radius + 66f + LaneRank * 15f, 82f, 230f);

        if (Phase == TelekineticSkirmishPhase.Capture)
        {
            if (distance <= orbitRadius * 1.20f)
            {
                Enter(TelekineticSkirmishPhase.Sweep);
                lastBearing = MathF.Atan2(radial.Y, radial.X);
                bearingInitialized = true;
            }
            else if (phaseAge >= CaptureTimeout)
            {
                Enter(TelekineticSkirmishPhase.Strike);
            }
        }

        if (Phase == TelekineticSkirmishPhase.Capture || Phase == TelekineticSkirmishPhase.Sweep)
        {
            float radiusError = Math.Clamp((distance - orbitRadius) / orbitRadius, -1.2f, 3f);
            float orbitSpeed = Math.Min(nominalSpeed * 0.75f, 15f + LaneRank);
            // 移动捕获点的切向速度 + 向轨道回归的径向力，再通过惯性与转角上限合成。
            Vector2 desired = (tangent - outward * radiusError * 2.2f) * orbitSpeed + focus.Velocity * 0.65f;
            float desiredSpeed = Math.Clamp(desired.Length(), orbitSpeed * 0.70f, nominalSpeed);
            Vector2 result = Steer(velocity, desired, desiredSpeed, 0.24f, CaptureTurnLimit);

            if (Phase == TelekineticSkirmishPhase.Sweep)
            {
                float bearing = MathF.Atan2(radial.Y, radial.X);
                if (bearingInitialized)
                    sweptAngle += Math.Clamp(WrapAngle(bearing - lastBearing) * OrbitSign, -0.2f, 0.2f);
                lastBearing = bearing;
                bearingInitialized = true;
                float requiredArc = Returned ? 1.05f : 1.05f + LaneRank * 0.23f;
                if (sweptAngle >= requiredArc || phaseAge >= SweepTimeout)
                    Enter(TelekineticSkirmishPhase.Strike);
            }
            return result;
        }

        float commitment = Math.Clamp(phaseAge / 10f, 0f, 1f);
        commitment = commitment * commitment * (3f - 2f * commitment);
        float leadFrames = Math.Clamp(distance / Math.Max(nominalSpeed, 1f), 0f, 8f);
        Vector2 inward = Unit(focus.Center + focus.Velocity * leadFrames - position, -outward);
        Vector2 strikeHeading = tangent * (1f - commitment) + inward * (0.35f + commitment * 1.65f);
        // 横掠时先收速，朝向真正切进目标后再释放速度。
        // 单纯高速追着目标转会形成打不中小敌人的稳定圆轨道。
        float alignment = Math.Clamp(Vector2.Dot(Unit(velocity, inward), inward), 0f, 1f);
        float turningSpeed = Math.Clamp(distance * StrikeTurnLimit * 0.55f, 2.4f, nominalSpeed);
        float strikeSpeed = MathHelper.Lerp(turningSpeed, nominalSpeed, alignment * alignment * alignment);
        Vector2 strikeVelocity = Steer(velocity, strikeHeading, strikeSpeed, 0.30f, StrikeTurnLimit);
        if (phaseAge >= 12)
            closestStrikeDistance = Math.Min(closestStrikeDistance, distance);
        bool passedTarget = closestStrikeDistance < focus.Radius + 90f &&
            distance > closestStrikeDistance + 34f && Vector2.Dot(velocity, -radial) < 0f;
        if (phaseAge >= StrikeTimeout || phaseAge >= 18 && passedTarget)
            Enter(Returned ? TelekineticSkirmishPhase.Finished : TelekineticSkirmishPhase.Coast);
        return strikeVelocity;
    }

    public void LoseTarget()
    {
        Enter(Returned ? TelekineticSkirmishPhase.Finished : TelekineticSkirmishPhase.Coast);
    }

    private void Enter(TelekineticSkirmishPhase phase)
    {
        Phase = phase;
        phaseAge = 0;
        sweptAngle = 0f;
        bearingInitialized = false;
        closestStrikeDistance = float.MaxValue;
    }

    private static Vector2 Steer(Vector2 velocity, Vector2 desired, float speed, float response, float maxTurn)
    {
        Vector2 current = Unit(velocity, Unit(desired, Vector2.UnitX));
        Vector2 inertial = Vector2.Lerp(velocity, Unit(desired, current) * speed, response);
        float angle = Math.Clamp(WrapAngle(MathF.Atan2(inertial.Y, inertial.X) -
            MathF.Atan2(current.Y, current.X)), -maxTurn, maxTurn);
        float nextSpeed = velocity.Length() + Math.Clamp(speed - velocity.Length(), -1.25f, 1.1f);
        return Rotate(current, angle) * Math.Max(1f, nextSpeed);
    }

    private static Vector2 Unit(Vector2 value, Vector2 fallback) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && value.LengthSquared() > 0.0001f
            ? Vector2.Normalize(value)
            : fallback;

    private static Vector2 Rotate(Vector2 v, float angle) => new(
        v.X * MathF.Cos(angle) - v.Y * MathF.Sin(angle),
        v.X * MathF.Sin(angle) + v.Y * MathF.Cos(angle));

    private static float WrapAngle(float angle) => MathF.IEEERemainder(angle, MathF.Tau);

    public void Write(BinaryWriter writer)
    {
        writer.Write((byte)Phase);
        writer.Write((sbyte)Lane);
        writer.Write(Returned);
        writer.Write(phaseAge);
        writer.Write(lastBearing);
        writer.Write(sweptAngle);
        writer.Write(closestStrikeDistance);
        writer.Write(bearingInitialized);
    }

    public void Read(BinaryReader reader)
    {
        Phase = (TelekineticSkirmishPhase)Math.Clamp((int)reader.ReadByte(), 0, (int)TelekineticSkirmishPhase.Finished);
        ConfigureLane(reader.ReadSByte());
        Returned = reader.ReadBoolean();
        phaseAge = Math.Clamp(reader.ReadInt32(), 0, 600);
        lastBearing = reader.ReadSingle();
        sweptAngle = reader.ReadSingle();
        closestStrikeDistance = reader.ReadSingle();
        bearingInitialized = reader.ReadBoolean();
    }
}
