using System;
using System.Collections.Generic;
using System.IO;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Content.Projectiles;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Companions;

/// <summary>Networked summon with an isolated player pose/combat state, never a network Player.</summary>
public sealed class FranCompanion : ModProjectile
{
    internal const float LeashRange = 1200f;
    private const float SearchRange = 950f;
    private const float InteractRange = 200f;
    private Player actor;
    private int evaluateTimer, heldTicks, noTargetTicks, stuckTicks, attackIndex = -1;
    private int attackIdentity = -1, syncTimer;
    private Vector2 previousOwnerPosition, previousPosition;
    private bool hasOwnerPosition;
    private bool pendingSwitch;
    private FranSkill pendingTactic;
    private float supportNeed;
    private readonly HashSet<int> enemyRoots = new();
    internal int SummonId { get; set; }
    internal bool Retiring { get; private set; }
    internal Player Actor => actor ??= CreateActor();
    internal int PlayerOwner => (int)Projectile.ai[0];
    internal FranSkill ActiveTactic => (FranSkill)(int)Projectile.ai[1];
    internal FranSkill ManualTactic => Projectile.ai[2] <= 0f ? FranSkill.None : (FranSkill)((int)Projectile.ai[2] - 1);
    internal bool Automatic => ManualTactic == FranSkill.None;
    internal int TargetIndex { get; private set; } = -1;
    internal Vector2 AimWorld { get; private set; }
    internal string Reason { get; private set; } = "Following";
    internal bool WantsAttack { get; private set; }
    internal int TeleportSequence { get; private set; }
    internal bool IsAuthority => Main.netMode != NetmodeID.MultiplayerClient;

    public override string Texture => $"Terraria/Images/Item_{ItemID.TitaniumMask}";

    public override void SetDefaults()
    {
        Projectile.width = 20;
        Projectile.height = 42;
        Projectile.netImportant = true;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 120;
        // Deliberately no contact damage, hurt box, NPC health, minion sacrifice, or loot bag.
    }

    public override bool? CanDamage() => false;
    public override bool ShouldUpdatePosition() => false;

    private Player CreateActor()
    {
        Player result = new() { whoAmI = FranCompanionContext.VirtualSlot, name = "Fran",
            active = true, dead = false, width = 20, height = 42, direction = 1,
            statLife = 100, statLifeMax = 100, statLifeMax2 = 100, immune = true,
            noKnockback = true, skinVariant = 4 };
        // One private fixed item is required by ShiFu.HeldItem; it is never exposed as a bag.
        result.inventory[0].SetDefaults(ModContent.ItemType<ShiFu>());
        result.selectedItem = 0;
        result.armor[10].SetDefaults(ItemID.TitaniumMask);
        result.armor[11].SetDefaults(ItemID.TitaniumBreastplate);
        result.armor[12].SetDefaults(ItemID.TitaniumLeggings);
        result.armor[13].SetDefaults(ItemID.AngelWings);
        result.hairColor = new Color(35, 32, 44);
        result.eyeColor = new Color(240, 196, 74);
        return result;
    }

    public override void AI()
    {
        if (SummonId <= 0 || PlayerOwner < 0 || PlayerOwner >= Main.maxPlayers)
            return; // ExtraAI can arrive after the initial vanilla projectile packet.
        Player owner = Main.player[PlayerOwner];
        if (!owner.active || owner.dead || Retiring)
        {
            if (IsAuthority)
                Dismiss();
            return;
        }
        Projectile.timeLeft = 120;
        Actor.active = true;
        Actor.dead = false;
        Actor.statLife = Actor.statLifeMax2 = 100;
        Actor.immune = true;
        Actor.immuneTime = 2;
        Actor.team = owner.team;
        Actor.hostile = false;
        Actor.GetDamage(DamageClass.Generic) = owner.GetTotalDamage(DamageClass.Summon);
        Actor.GetCritChance(DamageClass.Generic) = 0f;
        Actor.position = Projectile.position;
        Actor.velocity = Projectile.velocity;
        Actor.GetModPlayer<FranPlayer>().TickCompanionCombat(ActiveTactic);

        if (IsAuthority)
        {
            heldTicks++;
            if (hasOwnerPosition && Vector2.DistanceSquared(owner.Center, previousOwnerPosition) > 640f * 640f ||
                Vector2.DistanceSquared(Projectile.Center, owner.Center) > LeashRange * LeashRange)
                Recall(owner);
            previousOwnerPosition = owner.Center;
            hasOwnerPosition = true;
            if (--evaluateTimer <= 0)
            {
                evaluateTimer = FranCompanionDecision.EvaluationTicks;
                Evaluate(owner);
            }
            NPC target = GetTarget();
            if (target is null && TargetIndex != -1)
            {
                TargetIndex = -1;
                evaluateTimer = 0;
            }
            Move(owner, target);
            UpdateAttack(target);
            if (++syncTimer >= 12)
            {
                syncTimer = 0;
                Projectile.netUpdate = true;
            }
        }
        else
        {
            // Server positions are refreshed at 5Hz; local movement only predicts presentation.
            Projectile.position += Projectile.velocity;
        }
        Actor.position = Projectile.position;
        Actor.velocity = Projectile.velocity;
        Actor.channel = WantsAttack;
        if (Projectile.velocity.X != 0 && !HasLiveAttack())
            Actor.direction = Math.Sign(Projectile.velocity.X);
        if (!Main.dedServ)
        {
            using var scope = new FranCompanionContext.Scope(this, simulation: false);
            Actor.PlayerFrame();
            FranCompanionAppearance.Apply(Actor);
        }
    }

    private bool ValidEnemy(NPC npc, Player owner) => npc.active && npc.CanBeChasedBy(Projectile) &&
        Vector2.DistanceSquared(owner.Center, npc.Center) <= SearchRange * SearchRange;

    private NPC GetTarget() => TargetIndex >= 0 && TargetIndex < Main.maxNPCs &&
        ValidEnemy(Main.npc[TargetIndex], Main.player[PlayerOwner]) ? Main.npc[TargetIndex] : null;

    private static bool IsBoss(NPC npc) => npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] ||
        npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].boss;

    private void Evaluate(Player owner)
    {
        NPC previousTarget = GetTarget();
        NPC target = null;
        // A player's minion target is a focus request; otherwise preserve the current target
        // unless a boss or a meaningfully closer enemy deserves priority.
        NPC forced = owner.HasMinionAttackTargetNPC && owner.MinionAttackTargetNPC >= 0 &&
            owner.MinionAttackTargetNPC < Main.maxNPCs ? Main.npc[owner.MinionAttackTargetNPC] : null;
        float best = float.NegativeInfinity;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!ValidEnemy(npc, owner))
                continue;
            float score = (IsBoss(npc) ? 500 : 0) + (npc == previousTarget ? 160 : 0) -
                Vector2.Distance(Projectile.Center, npc.Center) * .25f + (npc == forced ? 2000 : 0);
            if (score > best) { best = score; target = npc; }
        }
        TargetIndex = target?.whoAmI ?? -1;
        float injured = 0f;
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player candidate = Main.player[i];
            if (CanSupport(owner, candidate) && Vector2.DistanceSquared(candidate.Center, Projectile.Center) < 1000f * 1000f)
                injured = Math.Max(injured, 1f - candidate.statLife / (float)Math.Max(1, candidate.statLifeMax2));
        }
        supportNeed = injured;
        enemyRoots.Clear();
        int clustered = 0;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!ValidEnemy(npc, owner)) continue;
            int root = npc.realLife >= 0 ? npc.realLife : npc.whoAmI;
            // Worm body segments are one enemy for selecting a single-target vs crowd tactic.
            if (enemyRoots.Add(root) && target is not null && Vector2.DistanceSquared(npc.Center, target.Center) < 300f * 300f)
                clustered++;
        }
        float distance = target is null ? 0 : Vector2.Distance(Actor.Center, target.Hitbox.ClosestPointInRect(Actor.Center));
        bool melee = target is not null && distance <= 205f &&
            Collision.CanHitLine(Actor.position, Actor.width, Actor.height, target.position, target.width, target.height);
        var situation = new FranCompanionDecision.Situation(target is not null, target is not null && IsBoss(target),
            target is not null && (IsBoss(target) || target.life > FranBalance.GetBaseDamage(Actor) * 8),
            enemyRoots.Count, clustered, distance, target?.velocity.Length() ?? 0f, injured,
            Main.raining, Main.raining && Main.cloudAlpha >= .8f, melee, Actor.GetModPlayer<FranPlayer>().StolenContactDamageTimer > 0);
        Span<bool> available = stackalloc bool[(int)FranSkill.Count];
        for (int i = 0; i < available.Length; i++) available[i] = FranSkillInfo.IsUnlocked((FranSkill)i);
        FranSkill choice;
        if (!Automatic && FranSkillInfo.IsUnlocked(ManualTactic))
            choice = ManualTactic;
        else
        {
            if (!Automatic) Projectile.ai[2] = 0f;
            // Queue the decision now; commit at the next boundary even when the 30-tick
            // evaluation cadence never lands exactly between two sword swings.
            choice = FranCompanionDecision.Select(situation, available, ActiveTactic, heldTicks, false);
        }
        if (choice != ActiveTactic)
        {
            pendingSwitch = true;
            pendingTactic = choice;
            // Let the perpetual disc coast to a halt, then adopt the pending selection.
            if (HasLiveAttack() && Main.projectile[attackIndex].ModProjectile is FranQuickdrawDiscHoldout)
            {
                WantsAttack = false;
            }
            else if (!ActionLocked())
                SetTactic(choice);
        }
        else pendingSwitch = false;
        Reason = !Automatic ? "Manual" : ActiveTactic == FranSkill.BlueResonance && injured >= .1f ? "Support" :
            target is null ? "Following" : IsBoss(target) ? "Boss" : clustered >= 3 ? "Crowd" : !melee ? "Pursuit" : "Combat";
        Projectile.netUpdate = true;
    }

    internal static bool CanSupport(Player owner, Player candidate) => candidate.active && !candidate.dead &&
        (candidate.whoAmI == owner.whoAmI || !candidate.hostile && !owner.hostile || owner.team != 0 && owner.team == candidate.team);

    private void SetTactic(FranSkill skill)
    {
        pendingSwitch = false;
        Projectile.ai[1] = (byte)skill;
        heldTicks = 0;
        Actor.GetModPlayer<FranPlayer>().ComboIndex = 0;
        Actor.GetModPlayer<FranPlayer>().HammerComboRounds = 0;
        Actor.GetModPlayer<FranPlayer>().TickCompanionCombat(skill, tickTimers: false);
        Projectile.netUpdate = true;
    }

    internal bool CanInteract(int player) => player == PlayerOwner && !Retiring &&
        Main.player[player].active && !Main.player[player].dead &&
        Vector2.DistanceSquared(Main.player[player].Center, Projectile.Center) <= InteractRange * InteractRange;

    internal void SetManual(FranSkill skill)
    {
        pendingSwitch = false;
        Projectile.ai[2] = skill == FranSkill.None ? 0 : (int)skill + 1;
        evaluateTimer = 0;
        heldTicks = FranCompanionDecision.MinimumHoldTicks;
        Projectile.netUpdate = true;
    }

    private bool HasLiveAttack() => attackIndex >= 0 && attackIndex < Main.maxProjectiles &&
        Main.projectile[attackIndex].active && Main.projectile[attackIndex].identity == attackIdentity &&
        Main.projectile[attackIndex].GetGlobalProjectile<FranCompanionProjectile>().SummonId == SummonId;

    private bool ActionLocked()
    {
        if (HasLiveAttack()) return true;
        FranPlayer fran = Actor.GetModPlayer<FranPlayer>();
        return noTargetTicks < 90 && (fran.ComboIndex != 0 || fran.HammerComboRounds != 0);
    }

    private void Move(Player owner, NPC target)
    {
        Vector2 destination = owner.Center + new Vector2(-owner.direction * 75f, -50f);
        if (target is not null)
        {
            float side = Math.Sign(Projectile.Center.X - target.Center.X);
            if (side == 0) side = -owner.direction;
            float spacing = ActiveTactic is FranSkill.TelekineticCatapult or FranSkill.MultipleBodyCreation ? 190f : 76f;
            destination = target.Center + new Vector2(side * (target.width * .5f + spacing), -Math.Min(35f, target.height * .2f));
        }
        Vector2 delta = destination - Projectile.Center;
        float speed = target is null ? 13f : 16f;
        Vector2 desired = delta.LengthSquared() < 9 ? Vector2.Zero : delta.SafeNormalize(Vector2.Zero) * Math.Min(speed, delta.Length() * .12f);
        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, .14f);
        Vector2 motion = Collision.TileCollision(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height, true, true);
        Projectile.position += motion;
        if (delta.LengthSquared() > 100f * 100f && Vector2.DistanceSquared(previousPosition, Projectile.position) < .25f)
            stuckTicks++;
        else stuckTicks = 0;
        if (stuckTicks > 90) Recall(owner);
        previousPosition = Projectile.position;
        Actor.position = Projectile.position;
    }

    internal void Recall(Player owner)
    {
        for (int ring = 0; ring < 10; ring++)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 center = owner.Center + new Vector2(side * (48 + ring * 16), -32 - ring * 16);
                Vector2 pos = center - Projectile.Size * .5f;
                if (pos.X < 32 || pos.Y < 32 || pos.X + Projectile.width > Main.maxTilesX * 16 - 32 ||
                    pos.Y + Projectile.height > Main.maxTilesY * 16 - 32 ||
                    Collision.SolidCollision(pos, Projectile.width, Projectile.height) ||
                    Collision.LavaCollision(pos, Projectile.width, Projectile.height)) continue;
                Projectile.Center = center;
                Projectile.velocity = Vector2.Zero;
                Actor.position = Projectile.position;
                stuckTicks = 0;
                TeleportSequence++;
                Projectile.netUpdate = true;
                ShowTeleport();
                return;
            }
        }
        // Do not force a teleport into solid terrain. Retry while ordinary flight continues.
    }

    private void UpdateAttack(NPC target)
    {
        if (target is null) noTargetTicks++; else noTargetTicks = 0;
        bool support = ActiveTactic == FranSkill.BlueResonance && supportNeed >= .10f;
        bool reachable = target is not null && Vector2.Distance(Actor.Center, target.Hitbox.ClosestPointInRect(Actor.Center)) < 820f &&
            Collision.CanHitLine(Actor.position, Actor.width, Actor.height, target.position, target.width, target.height);
        bool pendingManual = !Automatic && ActiveTactic != ManualTactic;
        if (pendingSwitch && !ActionLocked()) SetTactic(pendingTactic);
        if (!HasLiveAttack()) WantsAttack = (reachable || support) && !Retiring;
        else if (!reachable && !support || pendingManual || pendingSwitch) WantsAttack = false;
        Actor.channel = WantsAttack;
        AimWorld = target?.Center ?? Actor.Center + new Vector2(Actor.direction * 220f, 0f);
        if (HasLiveAttack()) return;
        if (noTargetTicks >= 90)
        {
            Actor.GetModPlayer<FranPlayer>().ComboIndex = 0;
            Actor.GetModPlayer<FranPlayer>().HammerComboRounds = 0;
        }
        if (pendingManual && !ActionLocked()) SetTactic(ManualTactic);
        EnsureBarrier();
        if (!WantsAttack) return;
        using var scope = new FranCompanionContext.Scope(this, simulation: true);
        FranPlayer fran = Actor.GetModPlayer<FranPlayer>();
        int damage = Actor.GetWeaponDamage(Actor.HeldItem);
        Vector2 aim = (AimWorld - Actor.MountedCenter).SafeNormalize(Vector2.UnitX * Actor.direction);
        int index;
        if (ActiveTactic == FranSkill.PressurizedQuickdraw)
        {
            fran.ComboIndex = fran.ComboReset = 0;
            index = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Actor.MountedCenter, aim,
                ModContent.ProjectileType<FranQuickdrawDiscHoldout>(), damage,
                Actor.HeldItem.knockBack * FranBalance.QuickdrawFullDiscKnockbackMultiplier,
                FranCompanionContext.VirtualSlot, fran.AvailableTacticSlots);
        }
        else
        {
            int combo = fran.ConsumeNextCombo();
            float duration = FranBalance.GetComboBaseDuration(combo) * (combo == 3 ? .74f : 1f);
            index = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Actor.MountedCenter, aim,
                ModContent.ProjectileType<ShiFuSwingRebuild>(),
                Math.Max(1, (int)(damage * FranBalance.GetComboDamageMultiplier(combo))), Actor.HeldItem.knockBack,
                FranCompanionContext.VirtualSlot, combo, Math.Max(12, (int)MathF.Round(duration / Actor.GetWeaponAttackSpeed(Actor.HeldItem))),
                ActiveTactic == FranSkill.None ? 0 : (int)ActiveTactic + 1);
            if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].ModProjectile is ShiFuSwingRebuild swing)
            {
                swing.UnscaledSwordDamage = damage;
                swing.SetTactics(fran.GetActiveTacticSnapshot());
                Main.projectile[index].netUpdate = true;
            }
        }
        if (index >= 0 && index < Main.maxProjectiles)
        {
            attackIndex = index;
            attackIdentity = Main.projectile[index].identity;
        }
    }

    private void EnsureBarrier()
    {
        if (ActiveTactic != FranSkill.MagicBarrier || Automatic) return;
        foreach (Projectile child in Main.ActiveProjectiles)
            if (child.ModProjectile is FranAutoBarrierProjectile &&
                child.GetGlobalProjectile<FranCompanionProjectile>().SummonId == SummonId) return;
        using var scope = new FranCompanionContext.Scope(this, simulation: true);
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Actor.Center, Vector2.Zero,
            ModContent.ProjectileType<FranAutoBarrierProjectile>(), 0, 0f, FranCompanionContext.VirtualSlot);
    }

    internal void Dismiss()
    {
        Retiring = true;
        WantsAttack = false;
        Actor.channel = false;
        Projectile.Kill();
    }

    public override void OnKill(int timeLeft)
    {
        Retiring = true;
        if (actor is not null) actor.active = false;
        FranCompanionSystem.RemoveChildren(SummonId);
    }

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(SummonId);
        writer.Write((short)TargetIndex);
        writer.Write(AimWorld.X); writer.Write(AimWorld.Y);
        writer.Write(WantsAttack);
        writer.Write(TeleportSequence);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        int id = reader.ReadInt32(); int target = reader.ReadInt16();
        Vector2 aim = new(reader.ReadSingle(), reader.ReadSingle());
        bool attacking = reader.ReadBoolean(); int teleport = reader.ReadInt32();
        if (Main.netMode == NetmodeID.Server || id <= 0 || !float.IsFinite(aim.X) || !float.IsFinite(aim.Y)) return;
        SummonId = id;
        TargetIndex = target >= -1 && target < Main.maxNPCs ? target : -1;
        AimWorld = aim;
        WantsAttack = attacking;
        if (TeleportSequence != teleport) { TeleportSequence = teleport; ShowTeleport(); }
    }

    private void ShowTeleport()
    {
        if (Main.dedServ) return;
        for (int i = 0; i < 16; i++)
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, Main.rand.NextVector2Circular(4f, 4f));
            dust.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (SummonId > 0 && !Retiring) FranCompanionAppearance.Draw(this);
        return false;
    }
}
