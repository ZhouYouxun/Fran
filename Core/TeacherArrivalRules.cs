using System;
using Microsoft.Xna.Framework;

namespace FranMod.Core;

// Pure timing/identity rules, shared by the saved world event and its visual projections.
internal static class TeacherArrivalRules
{
    public const int EntryDelay = 600;
    public const int Windup = 90;
    public const int FallFrames = 32;
    public const int LandingAge = Windup + FallFrames;
    public const int HighlightHold = 90;
    public const int HighlightFade = 60;
    public const int SettledAge = LandingAge + HighlightHold + HighlightFade;
    public const float FallHeight = 1100f;

    public static bool Eligible(bool profileReady, bool spent, bool worldSpent, bool alive, int age) =>
        profileReady && !spent && !worldSpent && alive && age >= EntryDelay;

    public static float Highlight(int age)
    {
        int landedAge = age - LandingAge;
        if (landedAge < 0 || landedAge >= HighlightHold + HighlightFade)
            return 0f;
        float enter = Math.Clamp(landedAge / 10f, 0f, 1f);
        float exit = 1f - Math.Clamp((landedAge - HighlightHold) / (float)HighlightFade, 0f, 1f);
        return enter * exit * exit * (3f - 2f * exit);
    }

    public static Vector2 SwordTip(Vector2 landing, int age)
    {
        float t = Math.Clamp((age - Windup) / (float)FallFrames, 0f, 1f);
        return landing - Vector2.UnitY * (FallHeight * (1f - t * t));
    }

    public static bool Finite(Vector2 point) => float.IsFinite(point.X) && float.IsFinite(point.Y);
}
