using System;
using FranMod.Content.Companions;
using FranMod.Core;

int checks = 0;
void Check(bool result, string scenario)
{
    if (!result) throw new Exception("FAILED: " + scenario);
    checks++;
}
bool[] all = new bool[(int)FranSkill.Count];
Array.Fill(all, true);
var ordinary = new FranCompanionDecision.Situation(true, false, false, 1, 1, 110, 3, 0, false, false, true, false);
FranSkill Pick(FranCompanionDecision.Situation s, FranSkill current = FranSkill.None, int age = 600, bool locked = false) =>
    FranCompanionDecision.Select(s, all, current, age, locked);
Check(Pick(ordinary with { Enemies = 6, ClusteredEnemies = 6 }) == FranSkill.ThunderChain, "dense wave uses lightning");
Check(Pick(ordinary with { Distance = 500, Speed = 14, CanMelee = false }) == FranSkill.TelekineticCatapult, "fast distant enemy uses telekinesis");
Check(Pick(ordinary with { Enemies = 2, ClusteredEnemies = 1, Distance = 280 }) == FranSkill.MultipleBodyCreation, "spread pair uses phantom blades");
Check(Pick(ordinary with { Boss = true, Durable = true, Speed = 8 }) == FranSkill.PressurizedQuickdraw, "mobile durable boss uses sustained disc");
Check(Pick(ordinary with { Boss = true, Durable = true, Rain = true }) == FranSkill.ThorsHammer, "rain boosts heavy boss strike");
Check(Pick(ordinary with { Durable = true, MarkEstablished = true }) == FranSkill.SkillTaker, "established debuff favors sustained pressure");
Check(Pick(ordinary with { MissingHealthRatio = .55f }, FranSkill.ThunderChain, 10) == FranSkill.BlueResonance, "urgent healing overrides minimum hold at safe boundary");
Check(Pick(ordinary with { MissingHealthRatio = .55f }, FranSkill.ThunderChain, 600, true) == FranSkill.ThunderChain, "healing does not interrupt a committed swing");
Check(Pick(ordinary with { MissingHealthRatio = .25f }, FranSkill.TelekineticCatapult, 10) == FranSkill.TelekineticCatapult, "moderate injury respects minimum hold");
Check(Pick(ordinary with { HasTarget = false }, FranSkill.MultipleBodyCreation) == FranSkill.MultipleBodyCreation, "idle keeps current tactic");
Check(Pick(ordinary with { HasTarget = false, MissingHealthRatio = .4f }) == FranSkill.BlueResonance, "full blue can heal without an enemy");
Check(Pick(ordinary with { HasTarget = false }, FranSkill.MagicBarrier, 0, true) != FranSkill.MagicBarrier, "auto never retains a forbidden shield");
var early = new bool[(int)FranSkill.Count];
early[0] = early[1] = true;
Check(FranCompanionDecision.Select(ordinary with { Boss = true, Durable = true }, early, FranSkill.None, 999, false) == FranSkill.SkillTaker, "early boss uses unlocked sustained tactic");
Check(FranCompanionDecision.Select(ordinary, new bool[8], FranSkill.None, 999, false) == FranSkill.None, "empty pool falls back to pure swordsmanship");
var shieldOnly = new bool[8]; shieldOnly[(int)FranSkill.MagicBarrier] = true;
Check(FranCompanionDecision.Select(ordinary, shieldOnly, FranSkill.None, 999, false) == FranSkill.None, "shield-only pool stays empty");
// Exhaust all unlock masks and previous tactics, including invalid/stale packet values.
for (int mask = 0; mask < 256; mask++)
{
    bool[] unlocked = new bool[8];
    for (int i = 0; i < 8; i++) unlocked[i] = (mask & (1 << i)) != 0;
    for (int previous = 0; previous < 256; previous++)
    {
        FranSkill selected = FranCompanionDecision.Select(ordinary with { Enemies = 8, ClusteredEnemies = 5, Rain = true },
            unlocked, (FranSkill)previous, 0, true);
        Check(selected != FranSkill.MagicBarrier, "shield exclusion across all unlock/prior-state combinations");
        Check(selected == FranSkill.None || (int)selected < 8 && unlocked[(int)selected], "never selects unavailable tactic");
    }
}
Console.WriteLine($"PASS: {checks} companion policy checks.");
