using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace FranMod.Content.Config;

public sealed class FranClientConfig : ModConfig
{
    public static FranClientConfig Instance { get; private set; }

    public override ConfigScope Mode => ConfigScope.ClientSide;

    // The old setting was named ShowUltimateGauge and defaulted to false, which made the
    // three-minute resource invisible on a fresh install.  A negative option also migrates
    // existing config files safely: the retired false value cannot keep suppressing the HUD.
    [DefaultValue(false)]
    public bool HideUltimateGauge;

    [DefaultValue(false)]
    public bool AllowUltimateGaugeDragging;

    public override void OnLoaded() => Instance = this;
}
