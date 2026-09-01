using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Core;

public sealed class FranTelekineticFlightSystem : ModSystem
{
    private const string InvisibleWingTexture = "Fran/Assets/Flight/InvisibleWing";
    private static readonly int[] WingSlots = { -1, -1, -1, -1 };

    public static int CurrentTier
    {
        get
        {
            if (!Main.hardMode)
                return 0;
            if (NPC.downedGolemBoss)
                return 4;
            if (NPC.downedPlantBoss)
                return 3;
            if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3)
                return 2;
            return 1;
        }
    }

    public static bool HasInfiniteFlight => Main.hardMode && NPC.downedEmpressOfLight;

    public override void Load()
    {
        for (int i = 0; i < WingSlots.Length; i++)
        {
            WingSlots[i] = EquipLoader.AddEquipTexture(Mod, InvisibleWingTexture, EquipType.Wings,
                name: $"FranTelekineticFlightTier{i + 1}");
        }
    }

    public override void PostSetupContent()
    {
        SetWingStats(1, new WingStats(100, 7f, 1.15f));
        SetWingStats(2, new WingStats(130, 8f, 1.65f));
        SetWingStats(3, new WingStats(160, 9f, 2.15f, true, 8f, 5f));
        SetWingStats(4, new WingStats(180, 9.75f, 2.5f, true, 9f, 6f));
    }

    public override void Unload()
    {
        for (int i = 0; i < WingSlots.Length; i++)
            WingSlots[i] = -1;
    }

    public static int GetCurrentWingSlot()
    {
        int tier = CurrentTier;
        return tier <= 0 ? -1 : WingSlots[tier - 1];
    }

    public static bool IsTelekineticWingSlot(int slot)
    {
        if (slot <= 0)
            return false;

        for (int i = 0; i < WingSlots.Length; i++)
            if (WingSlots[i] == slot)
                return true;
        return false;
    }

    public static WingStats GetCurrentWingStats()
    {
        int slot = GetCurrentWingSlot();
        return slot > 0 ? ArmorIDs.Wing.Sets.Stats[slot] : default;
    }

    private static void SetWingStats(int tier, WingStats stats)
    {
        int slot = WingSlots[tier - 1];
        if (slot > 0)
            ArmorIDs.Wing.Sets.Stats[slot] = stats;
    }
}
