using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Core;

public sealed class FranTelekineticFlightSystem : ModSystem
{
    private const string InvisibleWingTexture = "Fran/Assets/Flight/InvisibleWing";
    private static readonly int[] WingSlots = { -1, -1, -1, -1 };

    public static int CurrentTier => FranBalance.GetFlightTier();

    public static bool HasInfiniteFlight => FranBalance.HasInfiniteFlight();

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
        RefreshWingStats();
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
        RefreshWingStats();
        int slot = GetCurrentWingSlot();
        return slot > 0 ? ArmorIDs.Wing.Sets.Stats[slot] : default;
    }

    private static void RefreshWingStats()
    {
        for (int tier = 1; tier <= WingSlots.Length; tier++)
            SetWingStats(tier, new WingStats(
                FranBalance.GetFlightTime(tier),
                FranBalance.GetFlightRunSpeed(tier),
                FranBalance.GetFlightAcceleration(tier),
                FranBalance.GetFlightHasDownHover(tier),
                FranBalance.GetFlightDownHoverSpeed(tier),
                FranBalance.GetFlightDownHoverAcceleration(tier)));
    }

    private static void SetWingStats(int tier, WingStats stats)
    {
        int slot = WingSlots[tier - 1];
        if (slot > 0)
            ArmorIDs.Wing.Sets.Stats[slot] = stats;
    }
}
