using System;
using System.Collections.Generic;
using System.Linq;
using FranMod.Content.Projectiles;
using FranMod.Content.Systems;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FranMod.Content.Players;

public sealed class FranEvolutionPlayer : ModPlayer
{
    private readonly HashSet<string> claimedBosses = new(StringComparer.Ordinal);
    private readonly Dictionary<int, ulong> dashRefundLocks = new();
    private uint nodeMask;
    private int syncedEarnedPoints;

    private int directSequenceExpected;
    private int directSequenceHits;
    private int directSequenceTimer;
    private bool directSequenceUnhurt;
    private bool evolutionEchoReady;
    private bool hunterTempoReady;
    private int unityTimer;

    public uint NodeMask => nodeMask;
    public int EarnedPoints => Math.Max(FranEvolutionBossSystem.CalculateEarnedPoints(claimedBosses),
        syncedEarnedPoints);
    public int UsablePoints => Math.Min(EarnedPoints, FranEvolutionInfo.CurrentStagePointCap);
    public int SpentPoints => FranEvolutionInfo.SpentPoints(nodeMask);
    public int AvailablePoints => Math.Max(0, UsablePoints - SpentPoints);
    public bool UnityActive => unityTimer > 0 && HasNode(FranEvolutionNode.OneAsSword);
    public bool HunterTempoReady => hunterTempoReady && HasNode(FranEvolutionNode.HuntingTempo);
    public bool EvolutionEchoReady => evolutionEchoReady && HasNode(FranEvolutionNode.SelfEvolution);
    public bool CanAdjustEvolution => !IsEvolutionLockedByCombat();

    public override void Initialize()
    {
        claimedBosses.Clear();
        nodeMask = 0;
        syncedEarnedPoints = 0;
        ResetTransientState();
    }

    public override void SaveData(TagCompound tag)
    {
        if (claimedBosses.Count > 0)
            tag["EvolutionBosses"] = claimedBosses.OrderBy(key => key, StringComparer.Ordinal).ToList();
        if (nodeMask != 0)
            tag["EvolutionNodeMaskV2"] = (long)nodeMask;
    }

    public override void LoadData(TagCompound tag)
    {
        claimedBosses.Clear();
        if (tag.ContainsKey("EvolutionBosses"))
        {
            foreach (string key in tag.GetList<string>("EvolutionBosses"))
            {
                if (claimedBosses.Count >= FranEvolutionInfo.MaximumClaimedBosses)
                    break;
                if (FranEvolutionBossSystem.IsValidBossKey(key))
                    claimedBosses.Add(key);
            }
        }

        syncedEarnedPoints = FranEvolutionBossSystem.CalculateEarnedPoints(claimedBosses);
        uint loadedMask = tag.ContainsKey("EvolutionNodeMaskV2")
            ? (uint)Math.Clamp(tag.GetLong("EvolutionNodeMaskV2"), 0L, uint.MaxValue)
            : (uint)Math.Clamp(tag.GetInt("EvolutionNodeMask"), 0, ushort.MaxValue);
        nodeMask = FranEvolutionInfo.NormalizeMask(loadedMask, syncedEarnedPoints);
        ResetTransientState();
    }

    public override void OnEnterWorld()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer)
            FranModMain.SendEvolutionProfile(this);
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        if (Main.netMode != NetmodeID.Server || toWho == Player.whoAmI)
            return;
        int ignoreClient = toWho < 0 ? Player.whoAmI : fromWho;
        FranModMain.SendEvolutionState(Player.whoAmI, nodeMask, EarnedPoints,
            null, toWho, ignoreClient);
    }

    public override void PostUpdateEquips()
    {
        FranPlayer fran = Player.GetModPlayer<FranPlayer>();
        if (!fran.FranMode && !fran.HasTeacher)
            return;

        if (HasNode(FranEvolutionNode.ArcaneCirculation))
            Player.manaCost = Math.Max(0.1f, Player.manaCost - 0.08f);
        if (HasNode(FranEvolutionNode.FelineReflex))
            Player.moveSpeed += 0.05f;
        if (HasNode(FranEvolutionNode.SharedMana))
            Player.statManaMax2 += 20;
        if (HasNode(FranEvolutionNode.HarmonicGuard))
        {
            Player.statDefense += 3;
            Player.endurance += 0.02f;
        }
        if (HasNode(FranEvolutionNode.SynchronizedVolley))
            Player.GetDamage<MagicDamageClass>() += 0.06f;
        if (HasNode(FranEvolutionNode.EternalContract))
        {
            Player.GetDamage<GenericDamageClass>() += 0.05f;
            Player.statManaMax2 += 20;
            Player.statDefense += 3;
        }
    }

    public override void PostUpdate()
    {
        if (directSequenceTimer > 0 && --directSequenceTimer == 0)
            ResetDirectSequence();
        if (unityTimer > 0)
            unityTimer--;

        if (dashRefundLocks.Count > 0 && Main.GameUpdateCount % 120 == 0)
        {
            ulong now = Main.GameUpdateCount;
            foreach (int npcIndex in dashRefundLocks.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                dashRefundLocks.Remove(npcIndex);
        }
    }

    public override void OnRespawn() => ResetTransientState();

    public override void OnHurt(Player.HurtInfo info)
    {
        directSequenceUnhurt = false;
        unityTimer = 0;
    }

    public bool HasNode(FranEvolutionNode node) => FranEvolutionInfo.HasNode(nodeMask, node);

    public FranEvolutionUnlockFailure GetUnlockFailure(FranEvolutionNode node)
    {
        FranEvolutionNodeDefinition definition = FranEvolutionInfo.Get(node);
        if (definition is null)
            return FranEvolutionUnlockFailure.InvalidNode;
        if (HasNode(node))
            return FranEvolutionUnlockFailure.AlreadyUnlocked;
        if (definition.Prerequisite != FranEvolutionNode.None && !HasNode(definition.Prerequisite))
            return FranEvolutionUnlockFailure.MissingPrerequisite;
        if (FranSkillInfo.CurrentStage < definition.RequiredStage)
            return FranEvolutionUnlockFailure.StageLocked;
        if (AvailablePoints < definition.Cost)
            return FranEvolutionUnlockFailure.NotEnoughPoints;
        if (IsEvolutionLockedByCombat())
            return FranEvolutionUnlockFailure.InCombat;
        return FranEvolutionUnlockFailure.None;
    }

    public void RequestUnlock(FranEvolutionNode node)
    {
        if (!FranEvolutionInfo.IsValid(node))
            return;
        if (Main.netMode == NetmodeID.MultiplayerClient)
            FranModMain.SendEvolutionUnlockRequest(node);
        else
            TryUnlockAuthoritative(node);
    }

    public void RequestReset()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            FranModMain.SendEvolutionResetRequest();
        else
            TryResetAuthoritative();
    }

    internal bool TryUnlockAuthoritative(FranEvolutionNode node)
    {
        if (GetUnlockFailure(node) != FranEvolutionUnlockFailure.None)
            return false;
        nodeMask |= FranEvolutionInfo.Bit(node);
        AnnounceNewNodes(FranEvolutionInfo.Bit(node));
        if (Main.netMode == NetmodeID.Server)
            FranModMain.BroadcastEvolutionState(Player);
        return true;
    }

    internal bool TryResetAuthoritative()
    {
        if (nodeMask == 0 || IsEvolutionLockedByCombat())
            return false;
        nodeMask = 0;
        ResetTransientState();
        if (!Main.dedServ && Player.whoAmI == Main.myPlayer)
            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.65f, Pitch = -0.08f });
        if (Main.netMode == NetmodeID.Server)
            FranModMain.BroadcastEvolutionState(Player);
        return true;
    }

    internal bool TryClaimBoss(string bossKey)
    {
        if (!FranEvolutionBossSystem.IsValidBossKey(bossKey) ||
            claimedBosses.Count >= FranEvolutionInfo.MaximumClaimedBosses || !claimedBosses.Add(bossKey))
            return false;
        syncedEarnedPoints = FranEvolutionBossSystem.CalculateEarnedPoints(claimedBosses);
        return true;
    }

    internal IReadOnlyCollection<string> GetClaimedBosses() => claimedBosses;

    internal void MergeClientProfile(IEnumerable<string> bossKeys, uint requestedMask)
    {
        foreach (string key in bossKeys)
        {
            if (claimedBosses.Count >= FranEvolutionInfo.MaximumClaimedBosses)
                break;
            if (FranEvolutionBossSystem.IsValidBossKey(key))
                claimedBosses.Add(key);
        }
        syncedEarnedPoints = FranEvolutionBossSystem.CalculateEarnedPoints(claimedBosses);
        nodeMask = FranEvolutionInfo.NormalizeMask(requestedMask, syncedEarnedPoints);
        ResetTransientState();
    }

    internal void ReceiveEvolutionState(uint receivedMask, int receivedEarnedPoints,
        IReadOnlyCollection<string> receivedClaims)
    {
        uint oldMask = nodeMask;
        syncedEarnedPoints = Math.Clamp(receivedEarnedPoints, 0, ushort.MaxValue);
        if (receivedClaims is not null)
        {
            claimedBosses.Clear();
            foreach (string key in receivedClaims)
                if (FranEvolutionBossSystem.IsValidBossKey(key) &&
                    claimedBosses.Count < FranEvolutionInfo.MaximumClaimedBosses)
                    claimedBosses.Add(key);
            syncedEarnedPoints = FranEvolutionBossSystem.CalculateEarnedPoints(claimedBosses);
        }
        nodeMask = FranEvolutionInfo.NormalizeMask(receivedMask, syncedEarnedPoints);
        uint newBits = nodeMask & ~oldMask;
        if (newBits != 0)
            AnnounceNewNodes(newBits);
        if (nodeMask == 0 && oldMask != 0)
            ResetTransientState();
    }

    public int ComboResetDuration => HasNode(FranEvolutionNode.SharedVision) ? 125 :
        HasNode(FranEvolutionNode.MindSync) ? 105 : 70;

    public int AnalysisArmorPenetration => HasNode(FranEvolutionNode.DeeperAnalysis) ? 18 : 12;

    public float EvolutionEchoDamageMultiplier => HasNode(FranEvolutionNode.DeeperAnalysis) ? 0.70f : 0.50f;

    public int DashCooldownTicks => !HasNode(FranEvolutionNode.CatStep) ? 50 :
        HasNode(FranEvolutionNode.BlackCatAwakening) ? 34 :
        HasNode(FranEvolutionNode.FelineReflex) ? 38 : 42;

    public float DashSpeedMultiplier => 1f + (HasNode(FranEvolutionNode.SkyHunter) ? 0.10f : 0f) +
        (HasNode(FranEvolutionNode.BlackCatAwakening) ? 0.05f : 0f);

    public float DashImpactDamageMultiplier => 1.20f +
        (HasNode(FranEvolutionNode.PredatorSight) ? 0.20f : 0f) +
        (HasNode(FranEvolutionNode.BlackCatAwakening) ? 0.10f : 0f);

    public int DashImmunityBonus => HasNode(FranEvolutionNode.PhantomStep) ? 4 : 0;

    public bool AcceleratedUltimateCharge => HasNode(FranEvolutionNode.LimitResonance);

    public float BladeReachMultiplier
    {
        get
        {
            float multiplier = HasNode(FranEvolutionNode.EdgeCalibration) ? 1.06f : 1f;
            if (HasNode(FranEvolutionNode.ArcaneCirculation))
                multiplier *= 1.04f;
            if (UnityActive)
                multiplier *= HasNode(FranEvolutionNode.MutualGrowth) ? 1.12f : 1.10f;
            return multiplier;
        }
    }

    public float ConsumeSwingDurationMultiplier(int combo)
    {
        float multiplier = 1f;
        if (combo == 3 && HasNode(FranEvolutionNode.SharedRhythm))
            multiplier *= 0.88f;
        if (UnityActive)
            multiplier /= HasNode(FranEvolutionNode.MutualGrowth) ? 1.12f : 1.10f;
        if (HunterTempoReady)
        {
            hunterTempoReady = false;
            multiplier *= HasNode(FranEvolutionNode.BlackCatAwakening) ? 0.82f : 0.88f;
        }
        return multiplier;
    }

    public float DirectSwordDamageMultiplier(int combo)
    {
        float multiplier = HasNode(FranEvolutionNode.SwordSaintReasoning) ? 1.06f : 1f;
        if (!HasNode(FranEvolutionNode.ComboCalculation) || combo != directSequenceExpected ||
            directSequenceTimer <= 0 || directSequenceHits <= 0)
            return multiplier;
        float stepBonus = HasNode(FranEvolutionNode.SwordSaintReasoning) ? 0.04f : 0.03f;
        return multiplier * (1f + Math.Min(3, directSequenceHits) * stepBonus);
    }

    public void RegisterDirectSwordHit(int combo)
    {
        int sequenceWindow = ComboResetDuration;
        if (combo == 0)
        {
            directSequenceExpected = 1;
            directSequenceHits = 1;
            directSequenceTimer = sequenceWindow;
            directSequenceUnhurt = true;
            return;
        }

        if (directSequenceTimer <= 0 || combo != directSequenceExpected)
        {
            ResetDirectSequence();
            return;
        }

        directSequenceHits++;
        directSequenceTimer = sequenceWindow;
        if (combo < 3)
        {
            directSequenceExpected = combo + 1;
            return;
        }

        if (HasNode(FranEvolutionNode.SelfEvolution))
            evolutionEchoReady = true;
        if (HasNode(FranEvolutionNode.OneAsSword) && directSequenceUnhurt)
            unityTimer = (HasNode(FranEvolutionNode.MutualGrowth) ? 7 : 5) * 60;
        ResetDirectSequence(keepUnhurtFlag: true);
    }

    public bool TryConsumeEvolutionEcho()
    {
        if (!EvolutionEchoReady)
            return false;
        evolutionEchoReady = false;
        return true;
    }

    public void ApplyDashHit(NPC target)
    {
        if (HasNode(FranEvolutionNode.HuntingTempo))
            hunterTempoReady = true;
        if (!HasNode(FranEvolutionNode.EvolvedFootwork) || target is null || !target.active)
            return;

        ulong now = Main.GameUpdateCount;
        if (dashRefundLocks.TryGetValue(target.whoAmI, out ulong lockedUntil) && lockedUntil > now)
            return;
        dashRefundLocks[target.whoAmI] = now + 180;

        FranPlayer fran = Player.GetModPlayer<FranPlayer>();
        float remainingMultiplier = HasNode(FranEvolutionNode.BlackCatAwakening) ? 0.25f : 0.40f;
        fran.DashCooldown = Math.Max(0, (int)MathF.Ceiling(fran.DashCooldown * remainingMultiplier));
    }

    public void ApplyResonantPull(NPC target, Vector2 bladeTip)
    {
        if (!HasNode(FranEvolutionNode.ResonantPull) || Main.netMode == NetmodeID.MultiplayerClient ||
            target is null || !target.active || target.boss || target.knockBackResist <= 0f)
            return;

        Vector2 offset = bladeTip - target.Center;
        float distance = offset.Length();
        if (distance < 12f)
            return;
        float resonanceStrength = HasNode(FranEvolutionNode.SoulBridge) ? 1.40f : 1f;
        float speed = Math.Min(5f * resonanceStrength, distance * 0.08f * resonanceStrength) *
            MathHelper.Clamp(target.knockBackResist, 0.25f, 1f);
        target.velocity += offset.SafeNormalize(Vector2.Zero) * speed;
        if (target.velocity.LengthSquared() > 12f * 12f)
            target.velocity = target.velocity.SafeNormalize(Vector2.UnitY) * 12f;
        target.netUpdate = true;
    }

    private bool IsEvolutionLockedByCombat()
    {
        if (Player.dead || Player.GetModPlayer<FranPlayer>().UltimateActive)
            return true;
        const float bossLockDistanceSquared = 3200f * 3200f;
        foreach (NPC npc in Main.ActiveNPCs)
            if (npc.boss && Vector2.DistanceSquared(npc.Center, Player.Center) <= bossLockDistanceSquared)
                return true;
        return false;
    }

    private void AnnounceNewNodes(uint newBits)
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer)
            return;
        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.58f, Pitch = 0.24f }, Player.Center);
        FranEvolutionBranch firstBranch = FranEvolutionBranch.Teacher;
        bool foundBranch = false;
        for (int i = 0; i < FranEvolutionInfo.Count; i++)
        {
            if ((newBits & (1u << i)) == 0)
                continue;
            string name = Language.GetTextValue($"Mods.Fran.Evolution.Nodes.{(FranEvolutionNode)i}.DisplayName");
            FranEvolutionBranch branch = FranEvolutionInfo.Get((FranEvolutionNode)i).Branch;
            if (!foundBranch)
            {
                firstBranch = branch;
                foundBranch = true;
            }
            CombatText.NewText(Player.Hitbox, FranEvolutionInfo.BranchColor(branch),
                Language.GetTextValue("Mods.Fran.Evolution.NodeUnlocked", name));
        }
        if (foundBranch)
            Projectile.NewProjectile(Player.GetSource_Misc("FranEvolutionUnlock"), Player.Center, Vector2.Zero,
                ModContent.ProjectileType<FranEvolutionUnlockSigil>(), 0, 0f, Player.whoAmI, (int)firstBranch);
    }

    private void ResetDirectSequence(bool keepUnhurtFlag = false)
    {
        directSequenceExpected = 0;
        directSequenceHits = 0;
        directSequenceTimer = 0;
        if (!keepUnhurtFlag)
            directSequenceUnhurt = false;
    }

    private void ResetTransientState()
    {
        ResetDirectSequence();
        evolutionEchoReady = false;
        hunterTempoReady = false;
        unityTimer = 0;
        dashRefundLocks.Clear();
    }
}
