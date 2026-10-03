namespace FranMod.Content.Projectiles;

// Both attack templates provide the same per-beat budget to skill effects.
internal interface IShiFuAttack
{
    int SkillBaseDamage { get; }
    int ReserveSkillTakerWisps(int requested);
}
