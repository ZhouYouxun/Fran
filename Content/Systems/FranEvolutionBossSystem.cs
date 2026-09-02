using System;
using System.Collections.Generic;
using System.Linq;
using FranMod.Content.Players;
using FranMod.Content.Projectiles;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FranMod.Content.Systems;

public sealed class FranEvolutionBossSystem : ModSystem
{
    private const string WorldBossClaimsSaveKey = "FranEvolutionWorldBossesV1";

    private sealed class AnalysisProgress
    {
        public int ExpectedCombo;
        public int LastSwingIdentity = int.MinValue;
        public ulong WindowUntil;
        public ulong AnalyzedUntil;
    }

    private sealed class EncounterState
    {
        public readonly HashSet<int> Participants = new();
        public readonly Dictionary<int, AnalysisProgress> AnalysisByPlayer = new();
        public ulong LastSeenTick;
        public Vector2 LastPosition;
        public bool CompletionQueuedAsNew;
    }

    private static readonly Dictionary<string, EncounterState> Encounters = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Vector2> PendingAwards = new(StringComparer.Ordinal);
    private static readonly HashSet<string> StoredModBosses = new(StringComparer.Ordinal);
    private static readonly HashSet<string> CurrentWorldBosses = new(StringComparer.Ordinal);

    public static int CurrentWorldEarnedPoints => CalculateEarnedPoints(CurrentWorldBosses);

    public override void OnWorldLoad()
    {
        Encounters.Clear();
        PendingAwards.Clear();
        StoredModBosses.Clear();
        CurrentWorldBosses.Clear();
        RefreshCurrentWorldProgress();
    }

    public override void SaveWorldData(TagCompound tag)
    {
        if (StoredModBosses.Count > 0)
            tag[WorldBossClaimsSaveKey] = StoredModBosses.OrderBy(key => key, StringComparer.Ordinal).ToList();
    }

    public override void LoadWorldData(TagCompound tag)
    {
        StoredModBosses.Clear();
        if (tag.ContainsKey(WorldBossClaimsSaveKey))
        {
            foreach (string key in tag.GetList<string>(WorldBossClaimsSaveKey))
            {
                if (StoredModBosses.Count >= FranEvolutionInfo.MaximumClaimedBosses)
                    break;
                if (IsValidBossKey(key) && !IsVanillaProgressKey(key))
                    StoredModBosses.Add(key);
            }
        }
        RefreshCurrentWorldProgress();
    }

    public override void OnWorldUnload()
    {
        Encounters.Clear();
        PendingAwards.Clear();
        StoredModBosses.Clear();
        CurrentWorldBosses.Clear();
    }

    public override void PostUpdateNPCs()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        ulong now = Main.GameUpdateCount;
        if (now % 30 == 0 && RefreshCurrentWorldProgress())
            SynchronizePlayersWithWorldProgress();

        if (now % 30 == 0)
            RecordNearbyParticipants(now);

        if (PendingAwards.Count > 0)
        {
            foreach ((string key, Vector2 deathPosition) in PendingAwards.ToArray())
            {
                if (HasActiveEncounterNpc(key))
                    continue;
                AwardEncounter(key, deathPosition);
                PendingAwards.Remove(key);
            }
        }

        if (now % 120 == 0)
        {
            foreach (string key in Encounters.Where(pair => now - pair.Value.LastSeenTick > 600 &&
                         !PendingAwards.ContainsKey(pair.Key) && !HasActiveEncounterNpc(pair.Key))
                     .Select(pair => pair.Key).ToArray())
                Encounters.Remove(key);
        }
    }

    internal static void MarkParticipant(NPC npc, int playerIndex)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !Main.player.IndexInRange(playerIndex) ||
            !Main.player[playerIndex].active || !TryGetBossKey(npc, out string key))
            return;

        EncounterState state = GetOrCreate(key);
        state.Participants.Add(playerIndex);
        state.LastSeenTick = Main.GameUpdateCount;
        state.LastPosition = npc.Center;
    }

    internal static void QueueBossCompletion(NPC npc)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !IsCompletionNpc(npc) ||
            !TryGetBossKey(npc, out string key))
            return;

        EncounterState state = GetOrCreate(key);
        state.LastSeenTick = Main.GameUpdateCount;
        state.LastPosition = npc.Center;
        state.CompletionQueuedAsNew |= !CurrentWorldBosses.Contains(key);
        PendingAwards[key] = npc.Center;
    }

    public static void RegisterAnalysisHit(NPC npc, Player player, int combo, int swingIdentity)
    {
        if (npc is null || player is null || !player.active || combo is < 0 or > 3 ||
            !player.GetModPlayer<FranEvolutionPlayer>().HasNode(FranEvolutionNode.MagicStoneAnalysis) ||
            !TryGetBossKey(npc, out string key))
            return;

        EncounterState state = GetOrCreate(key);
        if (!state.AnalysisByPlayer.TryGetValue(player.whoAmI, out AnalysisProgress progress))
        {
            progress = new AnalysisProgress();
            state.AnalysisByPlayer[player.whoAmI] = progress;
        }

        // One wide sword swing may touch several worm segments or both members of a grouped boss.
        // Treat that as one combo stage instead of letting duplicate OnHit callbacks break the sequence.
        if (progress.LastSwingIdentity == swingIdentity)
            return;
        progress.LastSwingIdentity = swingIdentity;

        ulong now = Main.GameUpdateCount;
        if (combo == 0)
        {
            progress.ExpectedCombo = 1;
            progress.WindowUntil = now + 6 * 60;
            return;
        }

        if (progress.WindowUntil < now || combo != progress.ExpectedCombo)
        {
            progress.ExpectedCombo = 0;
            progress.WindowUntil = 0;
            return;
        }

        if (combo == 3)
        {
            progress.ExpectedCombo = 0;
            progress.WindowUntil = 0;
            progress.AnalyzedUntil = now + 8 * 60;
        }
        else
            progress.ExpectedCombo = combo + 1;
    }

    public static bool IsAnalyzedFor(NPC npc, int playerIndex)
    {
        if (!TryGetBossKey(npc, out string key) || !Encounters.TryGetValue(key, out EncounterState state) ||
            !state.AnalysisByPlayer.TryGetValue(playerIndex, out AnalysisProgress progress))
            return false;
        return progress.AnalyzedUntil >= Main.GameUpdateCount;
    }

    public static bool IsValidBossKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > FranEvolutionInfo.MaximumBossKeyLength)
            return false;
        foreach (char character in key)
            if (character > 127 || !char.IsLetterOrDigit(character) &&
                character is not '/' and not '_' and not '-' and not '.')
                return false;
        return key.Contains('/');
    }

    public static int GetPointReward(string key) => key switch
    {
        "Terraria/KingSlime" or "Terraria/EyeOfCthulhu" or "Terraria/WorldEvil" or
            "Terraria/QueenBee" or "Terraria/Deerclops" or "Terraria/Skeletron" => 2,
        "Terraria/WallOfFlesh" or "Terraria/QueenSlime" or "Terraria/Destroyer" or
            "Terraria/Twins" or "Terraria/SkeletronPrime" => 3,
        "Terraria/Plantera" or "Terraria/Golem" or "Terraria/DukeFishron" or
            "Terraria/EmpressOfLight" or "Terraria/LunaticCultist" or "Terraria/Betsy" => 4,
        "Terraria/MoonLord" => 5,
        _ => 3
    };

    public static int CalculateEarnedPoints(IEnumerable<string> bossKeys)
    {
        int points = 0;
        foreach (string key in bossKeys)
            if (IsValidBossKey(key))
                points += GetPointReward(key);
        return Math.Clamp(points, 0, ushort.MaxValue);
    }

    internal static void RefreshPlayerFromCurrentWorld(FranEvolutionPlayer evolution)
    {
        if (evolution is null || Main.netMode == NetmodeID.MultiplayerClient)
            return;
        RefreshCurrentWorldProgress();
        evolution.ApplyWorldProgress(CurrentWorldBosses);
    }

    private static bool RefreshCurrentWorldProgress()
    {
        HashSet<string> refreshed = new(StoredModBosses, StringComparer.Ordinal);
        AddVanillaBossProgress(refreshed);
        if (CurrentWorldBosses.SetEquals(refreshed))
            return false;

        CurrentWorldBosses.Clear();
        CurrentWorldBosses.UnionWith(refreshed);
        return true;
    }

    private static bool TryRegisterWorldBossCompletion(string key)
    {
        if (!IsValidBossKey(key))
            return false;

        bool wasCompleted = CurrentWorldBosses.Contains(key);
        if (!IsVanillaProgressKey(key) && StoredModBosses.Count < FranEvolutionInfo.MaximumClaimedBosses)
            StoredModBosses.Add(key);

        bool changed = RefreshCurrentWorldProgress();
        if (changed)
            SynchronizePlayersWithWorldProgress();
        return !wasCompleted && CurrentWorldBosses.Contains(key);
    }

    private static void SynchronizePlayersWithWorldProgress()
    {
        foreach (Player player in Main.ActivePlayers)
        {
            FranEvolutionPlayer evolution = player.GetModPlayer<FranEvolutionPlayer>();
            if (!evolution.ApplyWorldProgress(CurrentWorldBosses) || Main.netMode != NetmodeID.Server)
                continue;
            FranModMain.BroadcastEvolutionState(player);
        }
    }

    private static void AddVanillaBossProgress(HashSet<string> bosses)
    {
        AddIfDowned(bosses, "Terraria/KingSlime", NPC.downedSlimeKing);
        AddIfDowned(bosses, "Terraria/EyeOfCthulhu", NPC.downedBoss1);
        AddIfDowned(bosses, "Terraria/WorldEvil", NPC.downedBoss2);
        AddIfDowned(bosses, "Terraria/QueenBee", NPC.downedQueenBee);
        AddIfDowned(bosses, "Terraria/Deerclops", NPC.downedDeerclops);
        AddIfDowned(bosses, "Terraria/Skeletron", NPC.downedBoss3);
        AddIfDowned(bosses, "Terraria/WallOfFlesh", Main.hardMode);
        AddIfDowned(bosses, "Terraria/QueenSlime", NPC.downedQueenSlime);
        AddIfDowned(bosses, "Terraria/Destroyer", NPC.downedMechBoss1);
        AddIfDowned(bosses, "Terraria/Twins", NPC.downedMechBoss2);
        AddIfDowned(bosses, "Terraria/SkeletronPrime", NPC.downedMechBoss3);
        AddIfDowned(bosses, "Terraria/Plantera", NPC.downedPlantBoss);
        AddIfDowned(bosses, "Terraria/Golem", NPC.downedGolemBoss);
        AddIfDowned(bosses, "Terraria/DukeFishron", NPC.downedFishron);
        AddIfDowned(bosses, "Terraria/EmpressOfLight", NPC.downedEmpressOfLight);
        AddIfDowned(bosses, "Terraria/LunaticCultist", NPC.downedAncientCultist);
        AddIfDowned(bosses, "Terraria/Betsy", DD2Event.DownedInvasionT3);
        AddIfDowned(bosses, "Terraria/MoonLord", NPC.downedMoonlord);
    }

    private static void AddIfDowned(HashSet<string> bosses, string key, bool downed)
    {
        if (downed)
            bosses.Add(key);
    }

    private static bool IsVanillaProgressKey(string key) => key.StartsWith("Terraria/", StringComparison.Ordinal);

    internal static bool TryGetBossKey(NPC npc, out string key)
    {
        if (npc.realLife >= 0 && Main.npc.IndexInRange(npc.realLife))
        {
            NPC root = Main.npc[npc.realLife];
            if (root.active && root.ModNPC is not null && root.boss && IsValidBossKey(root.ModNPC.FullName))
            {
                key = root.ModNPC.FullName;
                return true;
            }
        }

        key = npc.type switch
        {
            NPCID.KingSlime => "Terraria/KingSlime",
            NPCID.EyeofCthulhu => "Terraria/EyeOfCthulhu",
            NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail or
                NPCID.BrainofCthulhu or NPCID.Creeper => "Terraria/WorldEvil",
            NPCID.QueenBee => "Terraria/QueenBee",
            NPCID.Deerclops => "Terraria/Deerclops",
            NPCID.SkeletronHead or NPCID.SkeletronHand => "Terraria/Skeletron",
            NPCID.WallofFlesh => "Terraria/WallOfFlesh",
            NPCID.QueenSlimeBoss => "Terraria/QueenSlime",
            NPCID.TheDestroyer or NPCID.TheDestroyerBody or NPCID.TheDestroyerTail or NPCID.Probe =>
                "Terraria/Destroyer",
            NPCID.Retinazer or NPCID.Spazmatism => "Terraria/Twins",
            NPCID.SkeletronPrime or NPCID.PrimeCannon or NPCID.PrimeLaser or NPCID.PrimeSaw or NPCID.PrimeVice =>
                "Terraria/SkeletronPrime",
            NPCID.Plantera or NPCID.PlanterasHook or NPCID.PlanterasTentacle => "Terraria/Plantera",
            NPCID.Golem or NPCID.GolemHead or NPCID.GolemHeadFree or NPCID.GolemFistLeft or NPCID.GolemFistRight =>
                "Terraria/Golem",
            NPCID.DukeFishron => "Terraria/DukeFishron",
            NPCID.HallowBoss => "Terraria/EmpressOfLight",
            NPCID.CultistBoss => "Terraria/LunaticCultist",
            NPCID.MoonLordCore or NPCID.MoonLordHead or NPCID.MoonLordHand or NPCID.MoonLordFreeEye =>
                "Terraria/MoonLord",
            NPCID.DD2Betsy => "Terraria/Betsy",
            _ => null
        };

        if (key is not null)
            return true;
        if (!npc.boss || npc.ModNPC is null)
            return false;
        key = npc.ModNPC.FullName;
        return IsValidBossKey(key);
    }

    private static bool IsCompletionNpc(NPC npc)
    {
        if (npc.ModNPC is not null)
            return npc.boss;
        return npc.type is NPCID.KingSlime or NPCID.EyeofCthulhu or NPCID.EaterofWorldsHead or
            NPCID.BrainofCthulhu or NPCID.QueenBee or NPCID.Deerclops or NPCID.SkeletronHead or
            NPCID.WallofFlesh or NPCID.QueenSlimeBoss or NPCID.TheDestroyer or NPCID.Retinazer or
            NPCID.Spazmatism or NPCID.SkeletronPrime or NPCID.Plantera or NPCID.Golem or NPCID.DukeFishron or
            NPCID.HallowBoss or NPCID.CultistBoss or NPCID.MoonLordCore or NPCID.DD2Betsy;
    }

    private static EncounterState GetOrCreate(string key)
    {
        if (!Encounters.TryGetValue(key, out EncounterState state))
        {
            state = new EncounterState();
            Encounters[key] = state;
        }
        return state;
    }

    private static void RecordNearbyParticipants(ulong now)
    {
        const float participationRadiusSquared = 2400f * 2400f;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!TryGetBossKey(npc, out string key))
                continue;
            EncounterState state = GetOrCreate(key);
            state.LastSeenTick = now;
            state.LastPosition = npc.Center;
            foreach (Player player in Main.ActivePlayers)
                if (!player.dead && Vector2.DistanceSquared(player.Center, npc.Center) <= participationRadiusSquared)
                    state.Participants.Add(player.whoAmI);
        }
    }

    private static bool HasActiveEncounterNpc(string key)
    {
        foreach (NPC npc in Main.ActiveNPCs)
            if (TryGetBossKey(npc, out string activeKey) && string.Equals(activeKey, key, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static void AwardEncounter(string key, Vector2 deathPosition)
    {
        if (!Encounters.TryGetValue(key, out EncounterState state))
            return;

        bool showFirstCompletionVfx = state.CompletionQueuedAsNew;
        bool newlyCompleted = TryRegisterWorldBossCompletion(key);
        if (!showFirstCompletionVfx && !newlyCompleted)
        {
            Encounters.Remove(key);
            return;
        }

        int pointReward = GetPointReward(key);

        foreach (int playerIndex in state.Participants)
        {
            if (!Main.player.IndexInRange(playerIndex))
                continue;
            Player player = Main.player[playerIndex];
            FranPlayer fran = player.GetModPlayer<FranPlayer>();
            if (!player.active || (!fran.FranMode && !fran.HasTeacher))
                continue;

            int projectileIndex = Projectile.NewProjectile(new EntitySource_Misc("FranBossMagicStone"),
                deathPosition, Vector2.Zero, ModContent.ProjectileType<FranMagicStoneAbsorptionVfx>(),
                0, 0f, playerIndex, playerIndex, pointReward);
            if (Main.projectile.IndexInRange(projectileIndex))
                Main.projectile[projectileIndex].netUpdate = true;
        }

        Encounters.Remove(key);
    }
}

public sealed class FranEvolutionBossGlobalNPC : GlobalNPC
{
    public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) =>
        FranEvolutionBossSystem.MarkParticipant(npc, player.whoAmI);

    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        if (projectile.ModProjectile is FranEvolutionEchoSlash)
            return;
        if (Main.player.IndexInRange(projectile.owner))
            FranEvolutionBossSystem.MarkParticipant(npc, projectile.owner);
    }

    public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) =>
        FranEvolutionBossSystem.MarkParticipant(npc, target.whoAmI);

    public override void OnKill(NPC npc) => FranEvolutionBossSystem.QueueBossCompletion(npc);
}
