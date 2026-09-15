// Opt-in test build only: dotnet build -p:FranCompanionTests=true. Excluded from normal builds
// AND from tModLoader's source compilation through buildIgnore=Tests/*.
using System;
using System.Collections.Generic;
using FranMod.Content.Companions;
using FranMod.Content.Items;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Tests;

public sealed class CompanionSmokeTest : ModSystem
{
    private bool ran;
    private int checks;
    private readonly HashSet<string> seen = new();
    public override bool IsLoadingEnabled(Mod mod) => Main.dedServ && Program.LaunchParameters.ContainsKey("-francompaniontest");
    public override void Load() => Main.OnTickForThirdPartySoftwareOnly += TryRun;
    public override void Unload() => Main.OnTickForThirdPartySoftwareOnly -= TryRun;

    private void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("COMPANION TEST FAILED: " + label);
        checks++;
    }

    private void TryRun()
    {
        if (ran || Main.gameMenu) return;
        ran = true;
        try { Run(); Console.WriteLine($"COMPANION_RUNTIME_PASS: {checks} checks"); Environment.Exit(0); }
        catch (Exception error) { Console.WriteLine("COMPANION_RUNTIME_FAIL: " + error); Environment.Exit(9); }
    }

    private void Run()
    {
        Netplay.Disconnect = true;
        System.Threading.Thread.Sleep(500); // Let the test server's socket loop retire before installing synthetic fixtures.
        for (int i = 0; i < Main.maxPlayers; i++)
            Check(!Main.player[i].active, "isolated test server must have no players");
        Main.hardMode = true;
        // Simulated singleplayer authority uses combat text, absent in dedicated-server setup.
        for (int i = 0; i < Main.combatText.Length; i++) Main.combatText[i] ??= new CombatText();
        NPC.downedBoss1 = NPC.downedBoss3 = NPC.downedMechBoss1 = NPC.downedPlantBoss = NPC.downedGolemBoss = true;
        // Use a clear patch high above the world's surface, without changing any tiles.
        Player real = Main.player[0] = new Player { whoAmI = 0, active = true, name = "Companion test owner",
            statLife = 400, statLifeMax = 400, statLifeMax2 = 400, width = 20, height = 42 };
        real.Center = new Vector2(Main.spawnTileX * 16f, 1200f);
        real.inventory[0].SetDefaults(ModContent.ItemType<FranCompanionStaff>());
        real.selectedItem = 0;
        Player originalSentinel = Main.player[255];
        // Main updates every slot, including never-initialized projectiles. Global hooks
        // must not assume SetDefaults has attached per-entity globals to those slots.
        new Projectile().Update(Main.maxProjectiles - 1);
        Check(FranCompanionSystem.Resolve(new Projectile()) is null, "uninitialized slot bypasses companion context");
        Check(FranCompanionSystem.Resolve(new Projectile { active = true, owner = 255 }) is null,
            "missing global data is safe even on an active slot");
        for (int frame = 0; frame < 120; frame++) Tick(originalSentinel, real);
        FranCompanionSystem.Execute(FranCompanionSystem.Command.Summon, 0, 0, FranSkill.None);
        FranCompanion summon = FranCompanionSystem.FindOwned(0);
        Check(summon is not null, "summoned by actual item request");
        Check(!ReferenceEquals(summon.Actor, real) && ReferenceEquals(Main.player[0], real), "independent player actor");
        Check(summon.Actor.HeldItem.type == ModContent.ItemType<ShiFu>(), "fixed ShiFu");
        int npcIndex = NPC.NewNPC(new EntitySource_Misc("FranCompanionTest"), (int)real.Center.X + 230, (int)real.Center.Y, NPCID.BlueSlime);
        NPC target = Main.npc[npcIndex];
        target.life = target.lifeMax = 10_000_000;
        target.width = 130; target.height = 130;
        target.damage = 80;
        target.defense = 0;
        target.HideStrikeDamage = true; // No font/GPU assets exist in this headless test host.
        target.velocity = Vector2.Zero;
        target.Center = real.Center + new Vector2(230, 0);
        // Dedicated server is the supported headless environment. Singleplayer visuals need
        // a real client: changing netMode to zero here would access absent lighting/font assets.
        for (int net = 2; net <= 2; net += 2)
        {
            Main.netMode = net;
            Main.myPlayer = net == 0 ? 0 : 255;
            foreach (FranSkill tactic in Enum.GetValues<FranSkill>())
            {
                if ((int)tactic >= (int)FranSkill.Count) continue;
                FranCompanionSystem.RemoveChildren(summon.SummonId);
                summon.Actor.GetModPlayer<Content.Players.FranPlayer>().ComboIndex = 0;
                summon.Actor.GetModPlayer<Content.Players.FranPlayer>().HammerComboRounds = 0;
                summon.SetManual(tactic);
                int lifeBefore = target.life;
                seen.Clear();
                for (int frame = 0; frame < 500; frame++) Tick(originalSentinel, real);
                Check(summon.ActiveTactic == tactic, $"manual tactic {tactic}, mode {net}, active={summon.ActiveTactic}, manual={summon.ManualTactic}, rootActive={summon.Projectile.active}, combo={summon.Actor.GetModPlayer<Content.Players.FranPlayer>().ComboIndex}, seen={string.Join(',', seen)}");
                Check(target.life < lifeBefore, $"actual damage {tactic}, mode {net}");
                string required = tactic switch
                {
                    FranSkill.TelekineticCatapult => "Telekinetic_CondensedMindCore",
                    FranSkill.SkillTaker => "FranSkillTakerWisp",
                    FranSkill.MagicBarrier => "FranAutoBarrierProjectile",
                    FranSkill.MultipleBodyCreation => "FranShiFuPhantomBlade",
                    FranSkill.BlueResonance => "FranBlueResonanceEcho",
                    FranSkill.ThunderChain => "FranThunderBudgetStrike",
                    FranSkill.PressurizedQuickdraw => "FranQuickdrawDiscHoldout",
                    FranSkill.ThorsHammer => "ThorsHammerMarker",
                    _ => ""
                };
                Check(seen.Contains(required), $"released {required}, saw [{string.Join(",", seen)}]");
                Console.WriteLine($"TACTIC_PASS mode={net} tactic={tactic} damage={lifeBefore - target.life}");
            }
        }
        Check(target.playerInteraction[0], "damage credited to summoner before death");
        Main.netMode = NetmodeID.Server; Main.myPlayer = 255;
        summon.SetManual(FranSkill.None);
        for (int frame = 0; frame < 400; frame++) Tick(originalSentinel, real);
        Check(summon.Automatic && summon.ActiveTactic != FranSkill.MagicBarrier, "return to auto never chooses barrier");
        var previous = summon.ManualTactic;
        FranCompanionSystem.Execute(FranCompanionSystem.Command.Tactic, 1, summon.SummonId, FranSkill.MagicBarrier);
        Check(summon.ManualTactic == previous, "another player cannot change Fran");
        summon.Projectile.Center = real.Center + new Vector2(1800, 0);
        Tick(originalSentinel, real);
        Check(Vector2.Distance(summon.Projectile.Center, real.Center) < 260, "long-distance teleport");
        summon.SetManual(FranSkill.BlueResonance);
        real.statLife = 160;
        for (int frame = 0; frame < 500; frame++) Tick(originalSentinel, real);
        Check(real.statLife > 160, "blue heals real summoner, not synthetic actor");
        summon.Dismiss();
        Check(FranCompanionSystem.FindOwned(0) is null, "dismiss removes root");
        foreach (Projectile p in Main.ActiveProjectiles)
            Check(p.GetGlobalProjectile<FranCompanionProjectile>().SummonId != summon.SummonId, "dismiss removes descendants");
        Check(ReferenceEquals(Main.player[255], originalSentinel) && ReferenceEquals(Main.player[0], real), "restores engine slots");
    }

    private void Tick(Player originalSentinel, Player real)
    {
        // Netplay's socket thread retires disconnected slots; the test has no real socket.
        real.active = true;
        real.dead = false;
        int local = Main.myPlayer;
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile p = Main.projectile[i];
            bool wasActive = p.active;
            if (wasActive && p.ModProjectile is not null) seen.Add(p.ModProjectile.GetType().Name);
            p.Update(i);
            if (wasActive && p.ModProjectile is FranCompanion root && !p.active)
                throw new Exception($"Root lost: pos={p.position} time={p.timeLeft} owner={root.PlayerOwner} netOwner={p.owner} realActive={real.active} realDead={real.dead} retiring={root.Retiring} id={root.SummonId} tag={p.GetGlobalProjectile<FranCompanionProjectile>().SummonId} bounds={Main.leftWorld},{Main.topWorld},{Main.rightWorld},{Main.bottomWorld}");
        }
        Check(ReferenceEquals(Main.player[255], originalSentinel) && ReferenceEquals(Main.player[0], real) && Main.myPlayer == local,
            "projectile tick restores sentinel and local identity");
    }
}
