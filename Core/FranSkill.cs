using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;

namespace FranMod.Core;

public static class FranSkillInfo
{
    public static int Count => (int)FranSkill.Count;

    public static int CurrentStage => FranBalance.GetCurrentTacticStage();

    public static bool IsUnlocked(FranSkill skill) => FranBalance.IsTacticUnlocked(skill);

    public static Color Color(FranSkill skill) => skill switch
    {
        FranSkill.TelekineticCatapult => new Color(130, 225, 255),
        FranSkill.SkillTaker => new Color(205, 100, 255),
        FranSkill.MagicBarrier => new Color(90, 175, 255),
        FranSkill.MultipleBodyCreation => new Color(235, 245, 255),
        FranSkill.BlueResonance => Graphics.FranResonanceVisuals.Emerald,
        FranSkill.ThunderChain => new Color(155, 125, 255),
        FranSkill.PressurizedQuickdraw => new Color(210, 250, 255),
        FranSkill.ThorsHammer => new Color(115, 155, 255),
        _ => Microsoft.Xna.Framework.Color.White
    };

    public static string Name(FranSkill skill) =>
        Language.GetTextValue($"Mods.Fran.Skills.{skill}.DisplayName");

    public static string Description(FranSkill skill) =>
        Language.GetTextValue($"Mods.Fran.Skills.{skill}.Description");

    public static string FirstLayerText(FranSkill skill) =>
        skill == FranSkill.TelekineticCatapult
            ? Language.GetTextValue($"Mods.Fran.Skills.{skill}.CurrentState.Stage{FranBalance.GetTelekineticPatternStage()}")
            : Language.GetTextValue($"Mods.Fran.Skills.{skill}.FirstLayer");

    public static string AdditionalLayerText(FranSkill skill) =>
        Language.GetTextValue($"Mods.Fran.Skills.{skill}.AdditionalLayer");

    public static string FullLoadoutText(FranSkill skill) =>
        Language.GetTextValue($"Mods.Fran.Skills.{skill}.FullLoadout");

    public static string UnlockText(FranSkill skill) =>
        Language.GetTextValue($"Mods.Fran.Skills.{skill}.UnlockCondition");

    public static int Cooldown(FranSkill skill) => FranBalance.GetTacticCooldown(skill);

    public static bool IsRealSkill(FranSkill skill) => (int)skill >= 0 && (int)skill < Count;

    public static int BaseDamage => FranBalance.GetBaseDamage();
}
