using System.ComponentModel;
using Newtonsoft.Json;
using Terraria.ModLoader.Config;

namespace FranMod.Content.Config;

public sealed class FranClientConfig : ModConfig
{
    public static FranClientConfig Instance { get; private set; }

    public override ConfigScope Mode => ConfigScope.ClientSide;

    [DefaultValue(true)]
    public bool EnablePlayerTextureReplacement = true;

    [DefaultValue(true)]
    public bool EnableFranVoice = true;

    [DefaultValue(true)]
    public bool EnableCustomMainMenu = true;

    // This is an internal one-time migration flag, not a user-facing option. It lets a fresh
    // Fran install select its menu once without overriding later manual menu-theme choices.
    [JsonProperty("CustomMainMenuDefaultApplied")]
    private bool customMainMenuDefaultApplied;

    internal bool CustomMainMenuDefaultApplied => customMainMenuDefaultApplied;

    internal void MarkCustomMainMenuDefaultApplied() => customMainMenuDefaultApplied = true;

    // The font asset has already been handed off, but its replacement system is intentionally
    // independent from the menu and character rendering switches.
    [DefaultValue(true)]
    public bool EnableCustomFont = true;

    [DefaultValue(true)]
    public bool EnableModIconFlicker = true;

    [DefaultValue(true)]
    public bool EnableVideoPlayback = true;

    // The old setting was named ShowUltimateGauge and defaulted to false, which made the
    // three-minute resource invisible on a fresh install.  A negative option also migrates
    // existing config files safely: the retired false value cannot keep suppressing the HUD.
    [DefaultValue(false)]
    public bool HideUltimateGauge;

    [DefaultValue(false)]
    public bool AllowUltimateGaugeDragging;

    public override void OnLoaded() => Instance = this;
}
