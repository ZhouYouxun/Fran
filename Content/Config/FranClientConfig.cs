using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace FranMod.Content.Config;

public sealed class FranClientConfig : ModConfig
{
    public static FranClientConfig Instance { get; private set; }

    public override ConfigScope Mode => ConfigScope.ClientSide;

    [DefaultValue(true)]
    public bool EnablePlayerTextureReplacement = true;

    [DefaultValue(false)]
    public bool EnableFranVoice;

    [DefaultValue(true)]
    public bool EnableCustomMainMenu = true;

    // The font asset has already been handed off, but its replacement system is intentionally
    // independent from the menu and character rendering switches.
    [DefaultValue(false)]
    public bool EnableCustomFont;

    [DefaultValue(true)]
    public bool EnableModIconFlicker = true;

    [DefaultValue(false)]
    public bool EnableVideoPlayback;

    [DefaultValue(false)]
    public bool AlwaysFloatShiFu;

    [DefaultValue(true)]
    public bool CarryShiFuOnBack = true;

    [DefaultValue(false)]
    public bool HoldSkillWheelToOpen;

    // The old setting was named ShowUltimateGauge and defaulted to false, which made the
    // three-minute resource invisible on a fresh install.  A negative option also migrates
    // existing config files safely: the retired false value cannot keep suppressing the HUD.
    [DefaultValue(false)]
    public bool HideUltimateGauge;

    [DefaultValue(false)]
    public bool AllowUltimateGaugeDragging;

    public override void OnLoaded() => Instance = this;
}
