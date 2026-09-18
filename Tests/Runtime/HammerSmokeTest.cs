// Opt-in isolated server only: -p:FranHammerTests=true and -franhammertest.
using System;
using System.IO;
using System.Linq;
using FranMod.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Tests;

public sealed class HammerSmokeTest : ModSystem
{
    private bool ran;
    private int checks;
    private readonly EntitySource_Misc source = new("HammerTest");
    public override bool IsLoadingEnabled(Mod mod) => Main.dedServ && Program.LaunchParameters.ContainsKey("-franhammertest");
    public override void Load() => Main.OnTickForThirdPartySoftwareOnly += TryRun;
    public override void Unload() => Main.OnTickForThirdPartySoftwareOnly -= TryRun;
    private void Check(bool success, string label)
    {
        if (!success) throw new Exception(label);
        checks++;
    }
    private void TryRun()
    {
        if (ran || Main.gameMenu) return;
        ran = true;
        try { Run(); Console.WriteLine($"HAMMER_RUNTIME_PASS: {checks} checks"); Environment.Exit(0); }
        catch (Exception e) { Console.WriteLine("HAMMER_RUNTIME_FAIL: " + e); Environment.Exit(9); }
    }
    private static void ClearProjectiles()
    {
        foreach (Projectile p in Main.ActiveProjectiles) p.active = false;
    }
    private NPC Enemy(Vector2 center)
    {
        NPC npc = Main.npc[NPC.NewNPC(source, (int)center.X, (int)center.Y, NPCID.BlueSlime)];
        npc.Center = center;
        npc.life = npc.lifeMax = 10000000;
        npc.defense = 0;
        npc.velocity = Vector2.Zero;
        npc.HideStrikeDamage = true;
        return npc;
    }
    private Projectile Marker(Vector2 center) => Main.projectile[Projectile.NewProjectile(source, center,
        Vector2.Zero, ModContent.ProjectileType<ThorsHammerMarker>(), 100, 7f, 0)];
    private static void Tick(Projectile p) => p.Update(p.whoAmI);
    private static void Aim(Vector2 point)
    {
        Main.screenPosition = Vector2.Zero;
        Main.mouseX = (int)point.X;
        Main.mouseY = (int)point.Y;
    }
    private void Run()
    {
        Netplay.Disconnect = true;
        System.Threading.Thread.Sleep(500);
        Check(Main.player.All(p => !p.active), "no real players on isolated host");
        Main.myPlayer = 0;
        Main.netMode = NetmodeID.Server;
        Player owner = Main.player[0] = new Player { whoAmI = 0, active = true, name = "Hammer test",
            width = 20, height = 42, statLife = 400, statLifeMax = 400, statLifeMax2 = 400 };
        owner.Center = new Vector2(Main.spawnTileX * 16f, 1200f);
        foreach (NPC n in Main.ActiveNPCs) n.active = false;
        NPC nearPlayer = Enemy(owner.Center + new Vector2(30f, 0f));
        NPC nearCursor = Enemy(owner.Center + new Vector2(650f, 0f));
        Aim(nearCursor.Center);
        Check(FranHammerTargeting.Select(owner) == nearCursor, "cursor distance rather than player distance");
        nearPlayer.boss = true;
        Check(FranHammerTargeting.Select(owner) == nearPlayer, "attackable boss priority");
        nearCursor.boss = true;
        Check(FranHammerTargeting.Select(owner) == nearCursor, "nearest cursor boss among bosses");
        nearCursor.dontTakeDamage = true;
        Check(FranHammerTargeting.Select(owner) == nearPlayer, "invulnerable boss excluded");
        nearPlayer.immortal = true;
        Check(FranHammerTargeting.Select(owner) is null, "immortal boss excluded");
        nearCursor.dontTakeDamage = false;
        nearCursor.boss = false;
        Check(FranHammerTargeting.Select(owner) == nearCursor, "normal enemy fallback despite invulnerable bosses");
        nearPlayer.immortal = false;
        nearPlayer.boss = false;
        nearCursor.realLife = nearPlayer.whoAmI;
        nearPlayer.boss = true;
        Check(FranHammerTargeting.Select(owner) == nearCursor, "attackable segmented boss part priority");
        nearCursor.realLife = -1;
        nearPlayer.boss = false;
        Check(FranHammerTargeting.GetLockedTarget(-1) is null && FranHammerTargeting.GetLockedTarget(99999) is null,
            "invalid target indexes safe");

        ClearProjectiles();
        Vector2 seal = owner.Center + new Vector2(100f, -220f);
        Aim(nearPlayer.Center);
        Projectile marker = Marker(seal);
        for (int i = 0; i < 50; i++)
        {
            Tick(marker);
            Check(marker.active && marker.Center == seal, "fixed overhead seal for full old windup");
            Check(!Main.projectile.Any(p => p.active && p.ModProjectile is FranThorsHammerDrop), "no early launch");
            Check(marker.ModProjectile.CanDamage() == false, "charge cannot damage");
        }
        using (MemoryStream bytes = new())
        {
            marker.ModProjectile.SendExtraAI(new BinaryWriter(bytes));
            bytes.Position = 0;
            Projectile remote = Marker(seal);
            remote.ModProjectile.ReceiveExtraAI(new BinaryReader(bytes));
            Check(remote.timeLeft == marker.timeLeft, "remaining charge time round trip");
            remote.active = false;
        }
        Aim(nearCursor.Center);
        Tick(marker);
        Projectile hammer = Main.projectile.Single(p => p.active && p.ModProjectile is FranThorsHammerDrop);
        Check(!marker.active && hammer.Center == seal, "release from seal at original frame 50");
        Check((int)hammer.ai[2] % 256 == nearCursor.whoAmI + 1, "release samples current cursor");
        Check(hammer.damage == 100 && hammer.knockBack == 7f, "launch damage and knockback preserved");
        Aim(nearPlayer.Center);
        Check(FranHammerTargeting.Resolve(owner, nearCursor.whoAmI) == nearCursor, "post-launch cursor does not change lock");
        int lifeBefore = nearCursor.life;
        for (int i = 0; i < 60 && hammer.active; i++) Tick(hammer);
        Check(!hammer.active && nearCursor.life < lifeBefore, "hammer reaches and damages chosen target");
        Projectile blast = Main.projectile.Single(p => p.active && p.ModProjectile is ThorsHammerBolt);
        Check(blast.damage == 88 && blast.width == 260 && blast.height == 260, "explosion multiplier and area preserved");
        Check(blast.ai[0] > 0.5f, "impact direction retained after collision positioning");
        Tick(blast);
        Projectile[] bolts = Main.projectile.Where(p => p.active && p.ModProjectile is FranLightningStrikeVisual).ToArray();
        Check(bolts.Length == 7, "seven thick outward branches");
        foreach (Projectile bolt in bolts)
        {
            Check(!bolt.friendly && bolt.ModProjectile.CanDamage() == false, "lightning is visual only");
            Vector2 start = bolt.Center - blast.Center;
            Check(start.Length() < 25f && Vector2.Dot(start, bolt.velocity) > 0f, "branch travels out from impact");
            Check((int)bolt.ai[1] == (65 << 16 | 160 << 8 | 255), "blue lightning palette");
        }
        Check(!Main.projectile.Any(p => p.active && p.ModProjectile is FranFlatLightningVisual), "old sky beam absent");
        nearCursor.dontTakeDamage = true;
        Check(FranHammerTargeting.Resolve(owner, nearCursor.whoAmI) == nearPlayer, "lost lock reacquires valid enemy");

        ClearProjectiles();
        nearPlayer.active = nearCursor.active = false;
        Vector2 fallbackPoint = seal + new Vector2(600f, 150f);
        Aim(fallbackPoint);
        Projectile empty = Marker(seal);
        for (int i = 0; i < 51 && empty.active; i++) Tick(empty);
        Projectile fallbackHammer = Main.projectile.Single(p => p.active && p.ModProjectile is FranThorsHammerDrop);
        Check(!empty.active && (int)fallbackHammer.ai[2] % 256 == 0,
            "no enemy releases a cursor-directed hammer");
        Check(new Vector2(fallbackHammer.ai[0], fallbackHammer.ai[1]) == fallbackPoint,
            "fallback captures release-time cursor");
        Aim(seal - new Vector2(500f, 400f));
        nearPlayer.active = true;
        nearPlayer.boss = true;
        Tick(fallbackHammer);
        Check((int)fallbackHammer.ai[2] % 256 == 0 &&
            new Vector2(fallbackHammer.ai[0], fallbackHammer.ai[1]) == fallbackPoint,
            "new boss and mouse movement cannot steal cursor fallback");
        for (int i = 0; i < 60 && fallbackHammer.active; i++) Tick(fallbackHammer);
        Projectile fallbackBlast = Main.projectile.Single(p => p.active && p.ModProjectile is ThorsHammerBolt);
        Check(!fallbackHammer.active && Vector2.Distance(fallbackBlast.Center, fallbackPoint) < 0.01f,
            "cursor fallback detonates at the saved cursor point");
        ClearProjectiles();
        nearCursor.active = true;
        nearCursor.dontTakeDamage = false;
        Projectile spectator = Marker(seal);
        spectator.timeLeft = 25;
        Main.myPlayer = 1;
        Tick(spectator);
        Check(!Main.projectile.Any(p => p.active && p.ModProjectile is FranThorsHammerDrop), "spectator cannot duplicate release");
    }
}
