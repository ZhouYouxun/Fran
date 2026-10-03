using System;
using System.IO;
using System.Linq;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Content.Projectiles.BlackHole;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FranMod.Tests;

// Opt-in isolated host only. Normal builds exclude this file.
public sealed class SingularitySmokeTest : ModSystem
{
    private bool ran;
    private int checks;
    private Player owner;
    private FranPlayer fran;
    private readonly EntitySource_Misc source = new("SingularityTest");
    public override bool IsLoadingEnabled(Mod mod) => Main.dedServ && Program.LaunchParameters.ContainsKey("-fransingularitytest");
    public override void Load() => Main.OnTickForThirdPartySoftwareOnly += TryRun;
    public override void Unload() => Main.OnTickForThirdPartySoftwareOnly -= TryRun;
    private void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks++;
    }
    private void TryRun()
    {
        if (ran || Main.gameMenu) return;
        ran = true;
        try
        {
            Check(Main.worldName == "FranSingularityIsolatedTest", "isolated test world only");
            Check(Main.player.All(p => !p.active), "no real players");
            Netplay.Disconnect = true;
            Run();
            Console.WriteLine($"SINGULARITY_RUNTIME_PASS: {checks} checks");
            Environment.Exit(0);
        }
        catch (Exception error) { Console.WriteLine("SINGULARITY_RUNTIME_FAIL: " + error); Environment.Exit(9); }
    }
    private NPC Enemy(Vector2 center, float knockback = 1f)
    {
        NPC npc = Main.npc[NPC.NewNPC(source, (int)center.X, (int)center.Y, NPCID.BlueSlime)];
        npc.Center = center;
        npc.life = npc.lifeMax = 1000000;
        npc.defense = 0;
        npc.velocity = Vector2.Zero;
        npc.knockBackResist = knockback;
        npc.HideStrikeDamage = true;
        return npc;
    }
    private FranSingularityUltimate Create()
    {
        foreach (Projectile p in Main.ActiveProjectiles) p.active = false;
        foreach (NPC npc in Main.ActiveNPCs) npc.active = false;
        owner.dead = false;
        owner.active = true;
        owner.selectedItem = 0;
        fran.ResetEffects();
        ForceHiddenSelection(FranUltimateKind.Singularity);
        fran.UltimateChargeTicks = FranPlayer.UltimateChargeDuration;
        Check(fran.TryStartUltimate(owner.Center + new Vector2(450, -80)), "cast through existing ultimate entrypoint");
        Check(fran.UltimateChargeTicks == 0, "shared charge consumed once");
        return FranSingularityUltimate.Find(0);
    }
    private void ForceHiddenSelection(FranUltimateKind kind) =>
        typeof(FranPlayer).GetProperty(nameof(FranPlayer.SelectedUltimate))!.SetValue(fran, kind);
    private void Launch(FranSingularityUltimate hole)
    {
        for (int i = 0; i < FranSingularityRules.ChargeFrames; i++) hole.AI();
        Check(hole.Phase == FranSingularityUltimate.HolePhase.Flight && hole.PhaseAge == 0, "flight starts after charge");
        Check(Math.Abs(hole.Projectile.scale - .75f) < .0001f, "480 px visual diameter");
        Check(Vector2.Distance(hole.Projectile.Center, hole.SwordTip()) < .01f, "launch at transformed sword tip");
    }
    private void FlightTo(FranSingularityUltimate hole, int age)
    {
        while (hole.Phase == FranSingularityUltimate.HolePhase.Flight && hole.PhaseAge < age) hole.AI();
    }
    private void Run()
    {
        Main.netMode = NetmodeID.SinglePlayer;
        Main.myPlayer = 0;
        Main.hardMode = true;
        owner = Main.player[0] = new Player { whoAmI = 0, active = true, name = "Fran", width = 20, height = 42,
            statLife = 500, statLifeMax = 500, statLifeMax2 = 500 };
        owner.Center = new Vector2(Main.spawnTileX * 16, 1200);
        owner.inventory[0] = new Item(ModContent.ItemType<ShiFu>());
        owner.inventory[1] = new Item(ItemID.WoodenSword);
        fran = owner.GetModPlayer<FranPlayer>();

        foreach (int panel in new[] { 1, 9, 23, 104, 276, 527, 12345 })
        {
            int total = 0;
            for (int tick = 0; tick < 60; tick++) total += FranSingularityRules.TickDamage(panel, tick);
            Check(total == panel * 6, "exact fractional panel budget over 60 ticks");
        }
        Check(!FranSingularityRules.ShouldDetonate(299, true, true, true), "299 cannot cancel");
        Check(FranSingularityRules.ShouldDetonate(300, true, false, false), "300 manual detonation");
        Check(!FranSingularityRules.ShouldDetonate(899, false, false, false), "899 still controllable");
        Check(FranSingularityRules.ShouldDetonate(900, false, false, false), "900 forced detonation");
        Check(!fran.TrySelectUltimate(FranUltimateKind.Vigil) && !fran.TrySelectUltimate(FranUltimateKind.Singularity) &&
            !FranUltimateInfo.IsSelectable(FranUltimateKind.Vigil) && !FranUltimateInfo.IsSelectable(FranUltimateKind.Singularity),
            "later ultimates stay hidden from players");
        ForceHiddenSelection(FranUltimateKind.Singularity);
        TagCompound save = new(); fran.SaveData(save);
        fran.LoadData(save);
        Check(fran.SelectedUltimate == FranUltimateKind.BlackHeavenlyTiger, "unreleased saved selection falls back to the first");

        FranSingularityUltimate hole = Create();
        Check(!fran.ImmuneTo(default, 0, true), "black hole does not inherit execution invulnerability");
        int panelDamage = owner.GetWeaponDamage(owner.inventory[0]);
        Check(!fran.TrySelectUltimate(FranUltimateKind.BlackHeavenlyTiger), "cannot change choice while active");
        Launch(hole);
        NPC target = Enemy(hole.Projectile.Center, 0f);
        int life = target.life;
        for (int i = 0; i < 60; i++)
        {
            target.Center = hole.Projectile.Center;
            int prior = target.life;
            hole.AI();
            Check(prior - target.life == FranSingularityRules.TickDamage(panelDamage, i), "one real hit each frame");
        }
        Check(life - target.life == panelDamage * 6, "one second equals panel times six before defense");
        Check(target.velocity == Vector2.Zero, "immune enemy still damaged but not pulled");

        NPC movable = Enemy(hole.Projectile.Center + new Vector2(300, 0), 1f);
        NPC immune = Enemy(hole.Projectile.Center + new Vector2(300, 20), 0f);
        NPC boss = Enemy(hole.Projectile.Center + new Vector2(300, 40), 1f); boss.boss = true;
        hole.PullEnemies();
        Check(movable.velocity.X < 0f, "susceptible minor enemy attracted");
        Check(immune.velocity == Vector2.Zero && boss.velocity == Vector2.Zero, "immune enemies and bosses not moved");
        foreach (NPC npc in Main.ActiveNPCs) npc.active = false;
        FlightTo(hole, 299);
        hole.ReceiveInput(hole.Projectile.Center, true);
        owner.selectedItem = 1; fran.PreUpdate();
        Check(owner.selectedItem == 0, "weapon pinned before 5 seconds");
        hole.AI();
        Check(hole.Phase == FranSingularityUltimate.HolePhase.Flight && hole.PhaseAge == 300,
            "early detonation input not queued");
        hole.ReceiveInput(hole.Projectile.Center, true); hole.AI();
        Check(hole.Phase == FranSingularityUltimate.HolePhase.Collapse, "right click at 5 seconds starts collapse");
        target = Enemy(hole.Projectile.Center + new Vector2(850, 0));
        NPC outside = Enemy(hole.Projectile.Center + new Vector2(1000, 0));
        life = target.life;
        for (int i = 0; i < FranSingularityRules.CollapseFrames; i++) hole.AI();
        Check(life - target.life == panelDamage * 6, "large blast uses six times original sword panel");
        Check(outside.life == 1000000, "blast has finite radius");
        life = target.life;
        for (int i = 0; i < FranSingularityRules.AftermathFrames; i++) hole.AI();
        Check(target.life == life && !hole.Projectile.active, "explosion hits only once and controller expires");

        hole = Create(); Launch(hole); FlightTo(hole, 300);
        owner.selectedItem = 1; fran.PreUpdate(); hole.AI();
        Check(owner.selectedItem == 1 && hole.Phase == FranSingularityUltimate.HolePhase.Collapse,
            "weapon switch after 5 seconds detonates without repinning");
        hole = Create(); Launch(hole); FlightTo(hole, 100); owner.dead = true;
        FlightTo(hole, 300);
        Check(hole.Phase == FranSingularityUltimate.HolePhase.Flight, "early death preserves compulsory duration");
        hole.AI();
        Check(hole.Phase == FranSingularityUltimate.HolePhase.Collapse, "dead owner's hole detonates at 5 seconds");
        target = Enemy(hole.Projectile.Center);
        life = target.life;
        for (int i = 0; i < FranSingularityRules.CollapseFrames; i++) hole.AI();
        Check(life - target.life == panelDamage * 6, "death does not suppress explosion damage");
        hole = Create(); Launch(hole); FlightTo(hole, 899); hole.AI();
        Check(hole.Phase == FranSingularityUltimate.HolePhase.Flight && hole.PhaseAge == 900, "900 full flight ticks");
        hole.AI();
        Check(hole.Phase == FranSingularityUltimate.HolePhase.Collapse, "automatic 15 second detonation");

        hole = Create(); Launch(hole); FlightTo(hole, 45);
        using MemoryStream state = new();
        using (BinaryWriter writer = new(state, System.Text.Encoding.UTF8, true)) hole.SendExtraAI(writer);
        int copyIndex = Projectile.NewProjectile(source, hole.Projectile.Center, Vector2.Zero,
            ModContent.ProjectileType<FranSingularityUltimate>(), 1, 0, 0);
        FranSingularityUltimate copy = (FranSingularityUltimate)Main.projectile[copyIndex].ModProjectile;
        Main.netMode = NetmodeID.MultiplayerClient;
        state.Position = 0;
        using (BinaryReader reader = new(state, System.Text.Encoding.UTF8, true)) copy.ReceiveExtraAI(reader);
        Check(copy.Phase == hole.Phase && copy.PhaseAge == hole.PhaseAge, "phase clock serializes");
        target = Enemy(copy.Projectile.Center); life = target.life;
        copy.AI();
        Check(target.life == life && target.velocity == Vector2.Zero, "client prediction never damages or pulls");
        Main.netMode = NetmodeID.SinglePlayer;
        foreach (int type in new[] { ModContent.ProjectileType<FranSingularityShard>(), ModContent.ProjectileType<FranSingularitySmoke>() })
        {
            Projectile debris = Main.projectile[Projectile.NewProjectile(source, owner.Center, Vector2.One, type, 0, 0, 255)];
            Check(!debris.friendly && !debris.hostile && debris.damage == 0 && debris.ModProjectile.CanDamage() == false,
                "black shader debris strictly harmless");
        }
        foreach (string asset in new[] { "FranSingularityShards", "FranSingularitySmoke" })
            Check(ModContent.GetFileBytes("Fran/Assets/AutoloadedEffects/Shaders/BlackHole/" + asset + ".fxc")?.Length > 100,
                "compiled explosion shader packaged");
    }
}
