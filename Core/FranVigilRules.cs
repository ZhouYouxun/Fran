using System;

namespace FranMod.Core;

internal static class FranVigilRules
{
    internal const int ChargeFrames = 300;
    internal const int ReferenceUseAnimation = 45;
    internal const int MeteorInterval = 150;
    internal const float WaveDamageMultiplier = 4f;
    internal const float MeteorDamageMultiplier = 2f;
    internal const float WaveSpeed = 24f;
    // YC_LeftBladeSwing: enter at 70%, swing to 90% of the last 2/3, then recover 65%.
    internal static int UseAnimation(float attackSpeed) => Math.Max(6, (int)(ReferenceUseAnimation / Math.Max(.25f, attackSpeed)));
    internal static int StartFrame(int animation) => (int)(animation * .7f);
    internal static int RecoveryFrames(int animation) => Math.Max(1, (int)(animation * .65f));
    internal static float SwingCompletion(int frame, int animation) =>
        Math.Clamp((frame - animation / 3f) / (animation - animation / 3f), 0f, 1f);
}
