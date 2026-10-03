using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Content.Projectiles;
using FranMod.Content.Projectiles.Ultimate;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FranMod.Tests;

public sealed class VigilSmokeTest : ModSystem
{
    private bool ran;
    private int checks;
    private readonly EntitySource_Misc source = new("VigilTest");
    public override bool IsLoadingEnabled(Mod mod) => Main.dedServ && Program.LaunchParameters.ContainsKey("-franvigiltest");
    public override void Load() => Main.OnTickForThirdPartySoftwareOnly += RunWhenReady;
    public override void Unload() => Main.OnTickForThirdPartySoftwareOnly -= RunWhenReady;
    private void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks++; }
    private int Count<T>() where T : ModProjectile => Main.projectile.Count(p => p.active && p.ModProjectile is T);
    private static void ForceHiddenSelection(FranPlayer fran, FranUltimateKind kind) =>
        typeof(FranPlayer).GetProperty(nameof(FranPlayer.SelectedUltimate))!.SetValue(fran, kind);
    private void RunWhenReady()
    {
        if (ran || Main.gameMenu) return;
        ran = true;
        try
        {
            Check(Main.worldName == "FranSingularityIsolatedTest" && Main.player.All(p => !p.active), "empty isolated test world");
            Netplay.Disconnect = true;
            Run(); Console.WriteLine($"VIGIL_RUNTIME_PASS: {checks} checks"); Environment.Exit(0);
        }
        catch (Exception error) { Console.WriteLine("VIGIL_RUNTIME_FAIL: " + error); Environment.Exit(9); }
    }
    private void Run()
    {
        Main.netMode = NetmodeID.SinglePlayer; Main.myPlayer = 0; Main.hardMode = true;
        NPC.downedPlantBoss = true;
        Player owner = Main.player[0] = new Player { whoAmI = 0, active = true, name = "Fran", width = 20, height = 42,
            statLife = 500, statLifeMax = 500, statLifeMax2 = 500 };
        owner.Center = new Vector2(Main.spawnTileX * 16, 1200);
        owner.inventory[0] = new Item(ModContent.ItemType<ShiFu>());
        owner.inventory[1] = new Item(ItemID.WoodenSword);
        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        FranVigilPlayer vigil = owner.GetModPlayer<FranVigilPlayer>();
        fran.ResetEffects();
        Check(fran.SelectedUltimate == FranUltimateKind.BlackHeavenlyTiger, "default ultimate remains first");
        Check(FranSkillInfo.Count == 8 && (int)FranSkill.ThorsHammer == 7, "saved IDs stable");
        Check(FranSkillInfo.Selectable.Length == 8 && FranSkillInfo.Selectable.Distinct().Count() == 8 &&
            FranSkillInfo.IsRealSkill(FranSkill.PressurizedQuickdraw) && FranSkillInfo.IsUnlocked(FranSkill.PressurizedQuickdraw),
            "all eight tactics including quickdraw available");
        TagCompound oldSave = new() { ["EquippedTacticsV1"] = new List<int> { 6, 7, 0 }, ["SelectedUltimateV1"] = 0 };
        fran.LoadData(oldSave);
        Check(fran.GetRawTactic(0) == FranSkill.PressurizedQuickdraw && fran.GetRawTactic(1) == FranSkill.ThorsHammer,
            "old quickdraw and hammer slots both preserved");
        Check(!fran.TrySelectUltimate(FranUltimateKind.Vigil) && !fran.TrySelectUltimate(FranUltimateKind.Singularity) &&
            !FranUltimateInfo.IsSelectable(FranUltimateKind.Vigil) && !FranUltimateInfo.IsSelectable(FranUltimateKind.Singularity),
            "later ultimates are hidden from players");
        ForceHiddenSelection(fran, FranUltimateKind.Vigil);
        TagCompound save = new(); fran.SaveData(save); fran.ReceiveUltimateSelection(FranUltimateKind.BlackHeavenlyTiger); fran.LoadData(save);
        Check(fran.SelectedUltimate == FranUltimateKind.BlackHeavenlyTiger, "saved unreleased choice falls back to the first");
        ForceHiddenSelection(fran, FranUltimateKind.Vigil);
        MethodInfo charge = typeof(FranPlayer).GetMethod("UpdateUltimateCharge", BindingFlags.NonPublic | BindingFlags.Instance);
        fran.UltimateChargeTicks = 0;
        for (int i = 0; i < 299; i++) charge.Invoke(fran, null);
        Check(!fran.UltimateReady && !fran.TryStartUltimate(owner.Center), "299 ticks cannot enter");
        charge.Invoke(fran, null);
        Check(fran.UltimateReady && fran.UltimateChargeTicks == 300, "exact five second charge");
        fran.ReceiveUltimateSelection(FranUltimateKind.BlackHeavenlyTiger);
        Check(!fran.UltimateReady && fran.UltimateChargeTicks == 300, "no five-second full execution exploit");
        ForceHiddenSelection(fran, FranUltimateKind.Vigil);
        int old = Projectile.NewProjectile(source, owner.Center, Vector2.UnitX,
            ModContent.ProjectileType<Telekinetic_DestructiveWave>(), 10, 0, 0);
        Check(fran.TryStartUltimate(owner.Center), "enter stance through existing hotkey entrypoint");
        Check(vigil.Active && fran.UltimateActive && !fran.BlockingUltimateActive && !Main.projectile[old].active, "stance replaces old attacks, not blocking ultimate");
        Check(fran.GetSkillStacks(FranSkill.ThorsHammer) == 0 && fran.GetActiveTacticCount() == 0 &&
            fran.GetActiveTacticSnapshot().All(s => s == FranSkill.None), "all tactics suppressed");
        owner.controlLeft = owner.controlJump = owner.controlUseItem = true; fran.SetControls();
        Check(owner.controlLeft && owner.controlJump && owner.controlUseItem && fran.CanUseItem(owner.HeldItem), "movement and left attack remain enabled");
        Check(!fran.ImmuneTo(default, 0, true), "no inherited execution invulnerability");
        Check(!fran.TrySelectUltimate(FranUltimateKind.Singularity), "cannot change choice inside stance");
        int delayed = Projectile.NewProjectile(source, owner.Center, Vector2.Zero,
            ModContent.ProjectileType<Telekinetic_GravityStarfall>(), 50, 0, 0);
        FranVigilAttackGate gate = new();
        Check(gate.CanDamage(Main.projectile[delayed]) == false && !gate.PreAI(Main.projectile[delayed]) &&
            !Main.projectile[delayed].active && Count<Telekinetic_GravityStarfallExplosion>() == 0,
            "late old-tactic packet is harmless and cannot release OnKill explosion");

        ShiFu sword = (ShiFu)owner.HeldItem.ModItem;
        Check(sword.CanUseItem(owner) && owner.HeldItem.useAnimation == 45, "reference useAnimation 45");
        owner.channel = true; owner.mouseInterface = false; Main.blockMouse = Main.mapFullscreen = false;
        Main.screenPosition = owner.Center - new Vector2(100, 100); Main.mouseX = 600; Main.mouseY = 100;
        sword.Shoot(owner, new EntitySource_ItemUse_WithAmmo(owner, owner.HeldItem, 0), owner.Center, Vector2.UnitX,
            owner.HeldItem.shoot, owner.GetWeaponDamage(owner.HeldItem), 5);
        Check(Count<FranVigilSwing>() == 1 && Count<ShiFuSwingRebuild>() == 0 && Count<FranQuickdrawDiscHoldout>() == 0, "left-click replacement only");
        FranVigilSwing swing = Main.projectile.Where(p => p.active).Select(p => p.ModProjectile).OfType<FranVigilSwing>().Single();
        List<int> timings = new(); int before = 0;
        for (int tick = 0; tick < 90; tick++)
        {
            swing.AI(); int now = Count<FranVigilWave>();
            if (now > before) timings.Add(tick);
            before = now;
        }
        Check(timings.SequenceEqual(new[] { 0, 41, 82 }), "reference cadence: 12 swing + 29 recovery ticks");
        FranVigilWave[] waves = Main.projectile.Where(p => p.active).Select(p => p.ModProjectile).OfType<FranVigilWave>().ToArray();
        Check(waves.Select(w => w.Projectile.ai[1]).SequenceEqual(new[] { -1f, 1f, -1f }), "two alternating swings");
        Projectile wave = waves[0].Projectile; float speed = wave.velocity.Length(); int damage = wave.damage;
        Check(wave.penetrate == -1 && wave.extraUpdates == 2 && Math.Abs(wave.scale - 1.04f) < .001f, "reference wave size/update count; infinite penetration");
        for (int i = 0; i < 100; i++) waves[0].AI();
        Check(Math.Abs(wave.velocity.Length() - speed) < .0001f && damage == wave.damage, "no velocity decay or damage mutation");
        wave.numHits = 99;
        NPC.HitModifiers hit = new() { SourceDamage = StatModifier.Default };
        waves[0].ModifyHitNPC(new NPC(), ref hit);
        Check(hit.SourceDamage == StatModifier.Default, "no hit-count damage attenuation");
        Check(waves[0].Colliding(default, new Rectangle((int)wave.Center.X, (int)wave.Center.Y, 16, 16)) == true,
            "wave circle collision");

        using MemoryStream data = new();
        using (BinaryWriter writer = new(data, System.Text.Encoding.UTF8, true)) swing.SendExtraAI(writer);
        int copyIndex = Projectile.NewProjectile(source, owner.Center, swing.Projectile.velocity,
            ModContent.ProjectileType<FranVigilSwing>(), 10, 0, 0);
        FranVigilSwing copy = (FranVigilSwing)Main.projectile[copyIndex].ModProjectile;
        copy.Projectile.active = false;
        data.Position = 0;
        using (BinaryReader reader = new(data, System.Text.Encoding.UTF8, true)) copy.ReceiveExtraAI(reader);
        using MemoryStream copyData = new();
        using (BinaryWriter writer = new(copyData, System.Text.Encoding.UTF8, true)) copy.SendExtraAI(writer);
        Check(data.ToArray().SequenceEqual(copyData.ToArray()), "swing network roundtrip");

        for (int i = 0; i < 149; i++) vigil.PostUpdate();
        Check(Count<FranVigilMeteor>() == 0, "slow meteor cadence before 150"); vigil.PostUpdate();
        Check(Count<FranVigilMeteor>() == 1, "one meteor every 150 ticks");
        Main.netMode = NetmodeID.MultiplayerClient;
        for (int i = 0; i < 300; i++) vigil.PostUpdate();
        Check(Count<FranVigilMeteor>() == 1, "clients do not duplicate meteors");
        Main.netMode = NetmodeID.SinglePlayer;
        owner.selectedItem = 1;
        for (int i = 0; i < 900; i++) vigil.PostUpdate();
        Check(vigil.Active && Count<FranVigilMeteor>() == 1, "item switching preserves form but pauses sky damage");
        owner.selectedItem = 0;
        for (int i = 0; i < 3601; i++) vigil.PostUpdate();
        Check(vigil.Active && fran.UltimateChargeTicks == 0, "no duration limit, no charge while active");
        Check(fran.TryStartUltimate(owner.Center) && !vigil.Active, "same hotkey exits without requiring charge");
        Check(Count<FranVigilSwing>() == 0 && Count<FranVigilWave>() == 0 && Count<FranVigilMeteor>() == 0, "exit removes form attacks");
        Check(fran.GetRawTactic(1) == FranSkill.ThorsHammer && fran.GetSkillStacks(FranSkill.TelekineticCatapult) == 1, "saved tactics restored");
        for (int i = 0; i < 300; i++) charge.Invoke(fran, null);
        Check(fran.TryStartUltimate(owner.Center), "reenter after five seconds");
        owner.dead = true; vigil.UpdateDead();
        Check(!vigil.Active, "death ends stance");
        owner.dead = false;
        fran.ReceiveTacticLoadout(Enumerable.Repeat(FranSkill.PressurizedQuickdraw, FranPlayer.MaximumTacticSlots).ToArray());
        Check(fran.IsFullTacticLoadout(FranSkill.PressurizedQuickdraw), "full quickdraw loadout restored");
        Check(sword.CanUseItem(owner) && owner.HeldItem.shoot == ModContent.ProjectileType<FranQuickdrawDiscHoldout>(),
            "normal form selects original whirlwind holdout");
        sword.Shoot(owner, new EntitySource_ItemUse_WithAmmo(owner, owner.HeldItem, 0), owner.Center, Vector2.UnitX,
            owner.HeldItem.shoot, owner.GetWeaponDamage(owner.HeldItem), 5);
        Check(Count<FranQuickdrawDiscHoldout>() == 1 && Count<FranVigilSwing>() == 0,
            "restored seventh tactic fires its original whirlwind");
        Check(typeof(FranMod.Content.UI.FranUISystem).GetMethod("DrawUltimateChoices", BindingFlags.NonPublic | BindingFlags.Static) is null,
            "no hidden-ultimate selector is drawn");
        foreach (string path in new[] { "Textures/Particles/FranVigilJudgementWave", "Particles/VigilGlowOrb", "Particles/VigilSpark",
            "Particles/VigilSparkle", "Particles/VigilRing", "Particles/VigilSolidCircle", "Particles/CL_BloomCircle" })
            Check((ModContent.GetFileBytes("Fran/" + path + ".rawimg") ??
                ModContent.GetFileBytes("Fran/" + path + ".png"))?.Length > 100, "self-contained wave asset: " + path);
    }
}
