using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;

namespace FranMod.Core;

public enum FranSkill : byte
{
    TelekineticCatapult,
    SkillTaker,
    MagicBarrier,
    MultipleBodyCreation,
    BlueResonance,
    ThunderChain,
    PressurizedQuickdraw,
    ThorsHammer,
    Count,
    None = byte.MaxValue
}

public static class FranSkillInfo
{
    public static int Count => (int)FranSkill.Count;

    public static int RequiredStage(FranSkill skill) => skill switch
    {
        <= FranSkill.MagicBarrier => 1,
        <= FranSkill.BlueResonance => 2,
        <= FranSkill.ThunderChain => 3,
        _ => 4
    };

    public static int CurrentStage
    {
        get
        {
            if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3)
                return 4;
            if (Main.hardMode)
                return 3;
            if (NPC.downedBoss3)
                return 2;
            return 1;
        }
    }

    public static bool IsUnlocked(FranSkill skill) => CurrentStage >= RequiredStage(skill);

    public static Color Color(FranSkill skill) => skill switch
    {
        FranSkill.TelekineticCatapult => new Color(130, 225, 255),
        FranSkill.SkillTaker => new Color(205, 100, 255),
        FranSkill.MagicBarrier => new Color(90, 175, 255),
        FranSkill.MultipleBodyCreation => new Color(235, 245, 255),
        FranSkill.BlueResonance => new Color(80, 205, 255),
        FranSkill.ThunderChain => new Color(155, 125, 255),
        FranSkill.PressurizedQuickdraw => new Color(210, 250, 255),
        FranSkill.ThorsHammer => new Color(115, 155, 255),
        _ => Microsoft.Xna.Framework.Color.White
    };

    public static string Name(FranSkill skill) =>
        Language.GetTextValue($"Mods.Fran.Skills.{skill}.DisplayName");

    public static string Description(FranSkill skill) =>
        Language.GetTextValue($"Mods.Fran.Skills.{skill}.Description");

    public static string UnlockText(FranSkill skill) =>
        Language.GetTextValue($"Mods.Fran.Stages.Stage{RequiredStage(skill)}.Unlock");

    public static int Cooldown(FranSkill skill) => skill switch
    {
        FranSkill.TelekineticCatapult => 0,
        FranSkill.SkillTaker => 0,
        FranSkill.MagicBarrier => 150,
        FranSkill.MultipleBodyCreation => 0,
        FranSkill.BlueResonance => 0,
        FranSkill.ThunderChain => 0,
        FranSkill.PressurizedQuickdraw => 0,
        FranSkill.ThorsHammer => 0,
        _ => 0
    };

    public static bool IsRealSkill(FranSkill skill) => (int)skill >= 0 && (int)skill < Count;

    public static int BaseDamage
    {
        get
        {
            if (NPC.downedMoonlord)
                return 180;
            if (NPC.downedPlantBoss)
                return 115;
            if (NPC.downedMechBossAny)
                return 82;
            if (Main.hardMode)
                return 56;
            if (NPC.downedBoss3)
                return 34;
            return 20;
        }
    }
}
