using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace FranMod.Content.Config;

public sealed class FranClientConfig : ModConfig
{
    public static FranClientConfig Instance { get; private set; }

    public override ConfigScope Mode => ConfigScope.ClientSide;

    [DefaultValue(false)]
    public bool ShowUltimateGauge;

    public override void OnLoaded() => Instance = this;
}
