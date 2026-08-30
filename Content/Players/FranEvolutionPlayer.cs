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
    private ushort nodeMask;
    private int syncedClaimedBossCount;

    private int directSequenceExpected;
    private int directSequenceHits;
    private int directSequenceTimer;
    private bool directSequenceUnhurt;
    private bool evolutionEchoReady;
    private bool hunterTempoReady;
    private int unityTimer;

    public ushort NodeMask => nodeMask;
    public int EarnedPoints => Math.Max(claimedBosses.Count, syncedClaimedBossCount);
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
        syncedClaimedBossCount = 0;
        ResetTransientState();
    }

    public override void SaveData(TagCompound tag)
    {
        if (claimedBosses.Count > 0)
            tag["EvolutionBosses"] = claimedBosses.OrderBy(key => key, StringComparer.Ordinal).ToList();
        if (nodeMask != 0)
            tag["EvolutionNodeMask"] = (int)nodeMask;
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

        syncedClaimedBossCount = claimedBosses.Count;
        ushort loadedMask = (ushort)Math.Clamp(tag.GetInt("EvolutionNodeMask"), 0, ushort.MaxValue);
        nodeMask = FranEvolutionInfo.NormalizeMask(loadedMask, claimedBosses.Count);
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
        syncedClaimedBossCount = claimedBosses.Count;
        return true;
    }

    internal IReadOnlyCollection<string> GetClaimedBosses() => claimedBosses;

    internal void MergeClientProfile(IEnumerable<string> bossKeys, ushort requestedMask)
    {
        foreach (string key in bossKeys)
        {
            if (claimedBosses.Count >= FranEvolutionInfo.MaximumClaimedBosses)
                break;
            if (FranEvolutionBossSystem.IsValidBossKey(key))
                claimedBosses.Add(key);
        }
        syncedClaimedBossCount = claimedBosses.Count;
        nodeMask = FranEvolutionInfo.NormalizeMask(requestedMask, claimedBosses.Count);
        ResetTransientState();
    }

    internal void ReceiveEvolutionState(ushort receivedMask, int receivedClaimedCount,
        IReadOnlyCollection<string> receivedClaims)
    {
        ushort oldMask = nodeMask;
        syncedClaimedBossCount = Math.Clamp(receivedClaimedCount, 0, FranEvolutionInfo.MaximumClaimedBosses);
        if (receivedClaims is not null)
        {
            claimedBosses.Clear();
            foreach (string key in receivedClaims)
                if (FranEvolutionBossSystem.IsValidBossKey(key) &&
                    claimedBosses.Count < FranEvolutionInfo.MaximumClaimedBosses)
                    claimedBosses.Add(key);
            syncedClaimedBossCount = claimedBosses.Count;
        }
        nodeMask = FranEvolutionInfo.NormalizeMask(receivedMask, syncedClaimedBossCount);
        ushort newBits = (ushort)(nodeMask & ~oldMask);
        if (newBits != 0)
            AnnounceNewNodes(newBits);
        if (nodeMask == 0 && oldMask != 0)
            ResetTransientState();
    }

    public int ComboResetDuration => HasNode(FranEvolutionNode.MindSync) ? 105 : 70;

    public float BladeReachMultiplier
    {
        get
        {
            float multiplier = HasNode(FranEvolutionNode.EdgeCalibration) ? 1.06f : 1f;
            if (UnityActive)
                multiplier *= 1.10f;
            return multiplier;
        }
    }

    public float ConsumeSwingDurationMultiplier(int combo)
    {
        float multiplier = 1f;
        if (combo == 3 && HasNode(FranEvolutionNode.SharedRhythm))
            multiplier *= 0.88f;
        if (UnityActive)
            multiplier /= 1.10f;
        if (HunterTempoReady)
        {
            hunterTempoReady = false;
            multiplier *= 0.88f;
        }
        return multiplier;
    }

    public float DirectSwordDamageMultiplier(int combo)
    {
        if (!HasNode(FranEvolutionNode.ComboCalculation) || combo != directSequenceExpected ||
            directSequenceTimer <= 0 || directSequenceHits <= 0)
            return 1f;
        return 1f + Math.Min(3, directSequenceHits) * 0.03f;
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
            unityTimer = 5 * 60;
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
        fran.DashCooldown = Math.Max(0, (int)MathF.Ceiling(fran.DashCooldown * 0.40f));
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
        float speed = Math.Min(5f, distance * 0.08f) * MathHelper.Clamp(target.knockBackResist, 0.25f, 1f);
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

    private void AnnounceNewNodes(ushort newBits)
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer)
            return;
        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.58f, Pitch = 0.24f }, Player.Center);
        FranEvolutionBranch firstBranch = FranEvolutionBranch.Teacher;
        bool foundBranch = false;
        for (int i = 0; i < FranEvolutionInfo.Count; i++)
        {
            if ((newBits & (1 << i)) == 0)
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
