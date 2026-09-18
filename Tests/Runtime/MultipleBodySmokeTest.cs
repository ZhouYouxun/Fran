// Opt-in isolated server: -p:FranMultipleBodyTests=true and -franmultiplebodytest.
using System;
using System.IO;
using System.Linq;
using FranMod.Content.Items;
using FranMod.Content.Projectiles;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Tests;

public sealed class MultipleBodySmokeTest : ModSystem
{
    private bool ran;
    private int checks;
    private readonly EntitySource_Misc source = new("MultipleBodyTest");
    public override bool IsLoadingEnabled(Mod mod) => Main.dedServ && Program.LaunchParameters.ContainsKey("-franmultiplebodytest");
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
        try { Run(); Console.WriteLine($"MULTIPLE_BODY_RUNTIME_PASS: {checks} checks"); Environment.Exit(0); }
        catch (Exception error) { Console.WriteLine("MULTIPLE_BODY_RUNTIME_FAIL: " + error); Environment.Exit(9); }
    }

    private void ClearProjectiles()
    {
        foreach (Projectile p in Main.ActiveProjectiles) p.active = false;
    }

    private Projectile SpawnBlade(Vector2 origin, Vector2 destination, bool full, int npc = -1)
    {
        int index = Projectile.NewProjectile(source, origin, Vector2.Zero,
            ModContent.ProjectileType<FranShiFuPhantomBlade>(), 100, 0f, 0,
            FranShiFuPhantomBlade.EncodeLaunchDelay(18f, full), destination.X, destination.Y);
        Projectile p = Main.projectile[index];
        Main.mouseX = (int)destination.X;
        Main.mouseY = (int)destination.Y;
        return p;
    }

    private static void Tick(Projectile p) => p.Update(p.whoAmI);

    private void Run()
    {
        Netplay.Disconnect = true;
        System.Threading.Thread.Sleep(500);
        Check(Main.player.All(p => !p.active), "isolated host has no real players");
        Main.myPlayer = 0;
        Main.netMode = NetmodeID.Server;
        Player owner = Main.player[0] = new Player { whoAmI = 0, active = true, name = "Phantom test",
            width = 20, height = 42, statLife = 400, statLifeMax = 400, statLifeMax2 = 400 };
        owner.Center = new Vector2(Main.spawnTileX * 16f, 1200f);
        owner.inventory[0].SetDefaults(ModContent.ItemType<ShiFu>());
        owner.selectedItem = 0;
        Vector2 center = owner.Center + new Vector2(400f, 0f);
        Main.screenPosition = Vector2.Zero;
        Main.mouseX = (int)center.X;
        Main.mouseY = (int)center.Y;
        foreach (NPC n in Main.ActiveNPCs) n.active = false;
        int targetIndex = NPC.NewNPC(source, (int)center.X, (int)center.Y, NPCID.BlueSlime);
        NPC target = Main.npc[targetIndex];
        target.life = target.lifeMax = 10_000_000;
        target.defense = 0;
        target.HideStrikeDamage = true;
        target.velocity = Vector2.Zero;
        target.Center = center;

        // Independently sum the previous 2/2/2/4 (+1/+1/+1/+2 per layer) formula.
        for (int stage = 0; stage < 3; stage++)
        {
            Main.hardMode = stage >= 1;
            NPC.downedMoonlord = stage == 2;
            int slots = FranBalance.GetAvailableTacticSlots();
            float scale = FranTacticOutputBudget.Scale(FranSkill.MultipleBodyCreation);
            for (int layers = 1; layers <= slots; layers++)
            {
                float oldCycle = 0f;
                for (int combo = 0; combo < 4; combo++)
                {
                    int first = combo == 3 ? 4 : 2;
                    int oldCount = first + (layers - 1) * (combo == 3 ? 2 : 1);
                    oldCycle += FranBalance.GetComboDamageMultiplier(combo) * oldCount *
                        (combo == 3 ? 1.24f : 0.86f) / first;
                }
                Check(Math.Abs(oldCycle - FranTacticOutputBudget.MultipleBodyCycleWeight(layers)) < 0.00001f,
                    $"old cycle budget preserved: stage {stage}, layers {layers}");
                if (layers == slots)
                    Check(Math.Abs(oldCycle * scale - FranTacticOutputBudget.ReferenceCycle() * 1.25f) < 0.001f,
                        "full-loadout output ratio preserved");
                foreach (int baseDamage in new[] { 97, 163, 1000 })
                {
                    int firstTotal = -1;
                    for (int combo = 0; combo < 4; combo++)
                    {
                        ClearProjectiles();
                        int swingIndex = Projectile.NewProjectile(source, owner.Center, Vector2.UnitX,
                            ModContent.ProjectileType<ShiFuSwingRebuild>(),
                            (int)(baseDamage * FranBalance.GetComboDamageMultiplier(combo)), 0f, 0, combo, 20);
                        Projectile swing = Main.projectile[swingIndex];
                        ((ShiFuSwingRebuild)swing.ModProjectile).UnscaledSwordDamage = baseDamage;
                        ShiFuImprintCombat.TriggerSwing(owner, swing, combo, FranSkill.MultipleBodyCreation,
                            layers, Vector2.UnitX, 200f);
                        Projectile[] bodies = Main.projectile.Where(p => p.active && p.ModProjectile is FranShiFuPhantomBlade).ToArray();
                        Check(bodies.Length == new[] { 4, 2, 4, 6 }[combo], "actual fixed spawn count");
                        Check(bodies.All(p => p.ai[0] == bodies[0].ai[0]), "entire phantom formation shares launch time");
                        int total = bodies.Sum(p => p.damage);
                        if (firstTotal < 0) firstTotal = total;
                        Check(total == firstTotal, "actual per-swing group damage equal even with fractional sword multipliers");
                        Check(Math.Abs(total * 4 - baseDamage * oldCycle * scale) <= 2.1f, "cycle rounding bounded to two damage");
                    }
                }
            }
        }

        // Uneven integer budgets still sum exactly, regardless of formation size.
        for (int total = 6; total < 10000; total += 7)
            for (int combo = 0; combo < 4; combo++)
                Check(Enumerable.Range(0, FranMultipleBodyPattern.Count(combo))
                    .Sum(i => FranMultipleBodyPattern.BladeDamage(total, combo, i)) == total, "integer remainder conserved");

        // Run real Projectile.Update / collision / NPC damage at different angles and target sizes.
        for (int size = 8; size <= 56; size += 24)
            for (int angle = 0; angle < 16; angle++)
                for (int full = 0; full <= 1; full++)
                {
                    ClearProjectiles();
                    target.width = target.height = size;
                    target.Center = center;
                    target.immune[0] = 0;
                    int before = target.life;
                    Vector2 origin = center + (angle * MathHelper.TwoPi / 16f).ToRotationVector2() * 280f;
                    Projectile p = SpawnBlade(origin, center, full == 1, targetIndex);
                    for (int frame = 0; frame < 18; frame++)
                    {
                        Tick(p);
                        Check(p.active && p.Center == origin && p.velocity == Vector2.Zero, "18-frame stationary aim");
                        Check(p.ModProjectile.CanDamage() == false && target.life == before, "aim cannot damage");
                    }
                    for (int frame = 0; frame < 16 && p.active; frame++)
                    {
                        Tick(p);
                        if (target.life < before) Check(!p.active, "hit removes body in the same update");
                    }
                    Check(target.life < before && !p.active, $"small target hit, size {size}, angle {angle}, full {full}");
                    Projectile[] impacts = Main.projectile.Where(v => v.active && v.ModProjectile is FranPhantomMatrixImpact).ToArray();
                    Check(impacts.Length == 1 && impacts[0].damage == 0 && impacts[0].ModProjectile.CanDamage() == false,
                        "one synchronized harmless matrix per actual hit");
                }

        ClearProjectiles();
        target.active = false;
        Projectile miss = SpawnBlade(center - Vector2.UnitX * 400, center, false);
        for (int i = 0; i < 34 && miss.active; i++) Tick(miss);
        Check(miss.active && miss.Center.X > center.X + 150f, "flight continues beyond cursor");
        Check(Math.Abs(miss.velocity.Length() * 4f - 112f * 0.67f) < 0.001f, "dash speed reduced by 33 percent");
        Vector2 fixedVelocity = miss.velocity;
        Main.mouseY += 600;
        for (int i = 0; i < 8; i++) Tick(miss);
        Check(miss.velocity == fixedVelocity, "moving cursor after launch cannot steer the blade");
        for (int i = 0; i < 100 && miss.active; i++) Tick(miss);
        Check(!miss.active && !Main.projectile.Any(v => v.active && v.ModProjectile is FranPhantomMatrixImpact),
            "finite flight lifetime, no impact matrix for a miss");

        ClearProjectiles();
        target.active = true;
        target.Center = center + new Vector2(0f, 160f);
        Projectile aimed = SpawnBlade(center - Vector2.UnitX * 300f, center, false);
        for (int i = 0; i < 10; i++) Tick(aimed);
        Main.mouseY = (int)center.Y - 120;
        for (int i = 0; i < 9; i++) Tick(aimed);
        Check(Vector2.Dot(aimed.velocity.SafeNormalize(Vector2.UnitX), Vector2.UnitX) > 0.99999f,
            "charge preserves original direction and excludes enemy outside 20 degrees");

        // 起冲时重新检查固定锥体；移动目标使用预判，飞行修正不得越界。
        for (int degrees = -16; degrees <= 16; degrees += 8)
            foreach (float verticalSpeed in new[] { -4f, 0f, 4f })
            {
                ClearProjectiles();
                Vector2 origin = center - Vector2.UnitX * 800f;
                target.Center = origin + MathHelper.ToRadians(degrees).ToRotationVector2() * 760f;
                target.velocity = new Vector2(1f, verticalSpeed);
                target.width = target.height = 18;
                target.immune[0] = 0;
                int lifeBefore = target.life;
                Projectile tracking = SpawnBlade(origin, center, false);
                for (int frame = 0; frame < 18; frame++) Tick(tracking);
                Main.mouseX -= 1400;
                Tick(tracking);
                float launchAngle = tracking.velocity.ToRotation();
                float previousAngle = launchAngle;
                for (int frame = 0; frame < 24 && tracking.active; frame++)
                {
                    target.position += target.velocity;
                    Tick(tracking);
                    float heading = tracking.velocity.ToRotation();
                    Check(Math.Abs(MathHelper.WrapAngle(heading)) <= MathHelper.ToRadians(20.01f), "stays within original 20-degree cone");
                    Check(Math.Abs(MathHelper.WrapAngle(heading - launchAngle)) <= MathHelper.ToRadians(6.01f), "flight correction limited to six degrees");
                    Check(Math.Abs(MathHelper.WrapAngle(heading - previousAngle)) <= MathHelper.ToRadians(1.51f), "turn rate limited per frame");
                    previousAngle = heading;
                }
                Check(target.life < lifeBefore && !tracking.active, "intercepts moving target without wide homing");
            }
        target.velocity = Vector2.Zero;
        ClearProjectiles();
        target.Center = center + new Vector2(600f, 30f);
        Projectile noTurnaround = SpawnBlade(center, center + Vector2.UnitX * 1000f, false);
        for (int i = 0; i < 19; i++) Tick(noTurnaround);
        Vector2 forwardVelocity = noTurnaround.velocity;
        target.Center = center - Vector2.UnitX * 400f;
        for (int i = 0; i < 12; i++) Tick(noTurnaround);
        Check(Vector2.Dot(noTurnaround.velocity.SafeNormalize(Vector2.UnitX), forwardVelocity.SafeNormalize(Vector2.UnitX)) > 0.99999f,
            "target teleporting behind cannot cause a turnaround");
        target.active = false;

        foreach (bool full in new[] { false, true })
        {
            ClearProjectiles();
            Projectile accelerating = SpawnBlade(center, center + Vector2.UnitX * 2000f, full);
            for (int i = 0; i < 18; i++) Tick(accelerating);
            float maximum = 75.04f * (full ? 1.15f : 1f);
            float previous = 0f;
            for (int frame = 0; frame <= 10; frame++)
            {
                Tick(accelerating);
                float speed = accelerating.velocity.Length() * 4f;
                Check(speed >= previous && speed <= maximum + 0.001f, "startup acceleration monotonic and bounded");
                if (frame == 0) Check(speed < maximum * 0.15f, "visible low-speed startup");
                if (frame == 4) Check(speed < maximum * 0.4f, "acceleration is sustained rather than instant");
                if (frame == 10) Check(Math.Abs(speed - maximum) < 0.001f, "reaches preserved top speed");
                previous = speed;
            }
        }

        // Six planets must reach and hit together, including moving, highly elongated targets.
        foreach (Vector2 size in new[] { new Vector2(8f), new Vector2(320f, 24f), new Vector2(24f, 320f) })
        {
            ClearProjectiles();
            target.active = true;
            target.width = (int)size.X;
            target.height = (int)size.Y;
            target.Center = center;
            target.life = 10000000;
            target.immune[0] = 0;
            Projectile[] planets = new Projectile[6];
            for (int slot = 0; slot < planets.Length; slot++)
            {
                Vector2 origin = Telekinetic_GravityStarfall.InitialOrbitPosition(center, target, 0, slot);
                int index = Projectile.NewProjectile(source, origin, Vector2.Zero,
                    ModContent.ProjectileType<Telekinetic_GravityStarfall>(), 100, 0f, 0,
                    Telekinetic_GravityStarfall.EncodeAppearance(slot, slot), targetIndex + 1, 0f);
                planets[slot] = Main.projectile[index];
                planets[slot].scale = 1.08f;
            }
            int initialLife = target.life;
            float stableRadius = 0f;
            for (int frame = 1; frame <= 85; frame++)
            {
                target.Center += new Vector2(2f, -.5f);
                foreach (Projectile planet in planets) Tick(planet);
                float radius = Vector2.Distance(planets[0].Center, target.Center);
                Check(planets.All(p => Math.Abs(Vector2.Distance(p.Center, target.Center) - radius) < .01f),
                    "all six planets share contraction radius on moving target");
                if (frame == 60) stableRadius = radius;
                if (frame > 60 && frame < 85)
                {
                    float progress = (frame - 60f) / 25f;
                    Check(Math.Abs(radius - stableRadius * (1f - progress * progress)) < .01f,
                        "planet contraction follows 25-frame curve, 20 percent faster");
                }
                if (frame < 85)
                    Check(target.life == initialLife && planets.All(p => p.ai[2] >= 0f), "no planet hits early");
            }
            Check(planets.All(p => p.ai[2] < 0f) && target.life < initialLife,
                "all six planets hit and fade on frame 85");
            Check(Main.projectile.Count(p => p.active && p.ModProjectile is Telekinetic_GravityStarfallExplosion) == 6,
                "simultaneous impact retains six explosions");
        }
        target.active = false;

        // Extra-AI roundtrip during charge and in flight: no repeat charge or divergent trajectory.
        foreach (int ticks in new[] { 7, 19 })
        {
            ClearProjectiles();
            Projectile original = SpawnBlade(center, center + Vector2.UnitX * 2000, true);
            for (int i = 0; i < ticks; i++) Tick(original);
            Projectile copy = SpawnBlade(original.Center, new Vector2(original.ai[1], original.ai[2]), true);
            copy.velocity = original.velocity;
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true)) original.ModProjectile.SendExtraAI(writer);
            stream.Position = 0;
            using (var reader = new BinaryReader(stream)) copy.ModProjectile.ReceiveExtraAI(reader);
            for (int i = 0; i < 4; i++)
            {
                Tick(original); Tick(copy);
                Check(original.Center == copy.Center && original.velocity == copy.velocity &&
                    original.ModProjectile.CanDamage() == copy.ModProjectile.CanDamage(), "synchronized phase and velocity");
            }
        }
    }
}
