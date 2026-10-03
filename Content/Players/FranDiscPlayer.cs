using Terraria;
using Terraria.ModLoader;

namespace FranMod.Content.Players;

// Releasing near the hand must not shorten the attack beat or allow a second template
// to deal another hit immediately. This clock survives the disc projectile's recall.
public sealed class FranDiscPlayer : ModPlayer
{
    internal ulong AttackRecoveryUntil;
    internal bool Recovering => Main.GameUpdateCount < AttackRecoveryUntil;
    public override void Initialize() => AttackRecoveryUntil = 0;
    public override void UpdateDead() => AttackRecoveryUntil = 0;
}
