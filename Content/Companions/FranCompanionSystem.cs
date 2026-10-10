using System;
using System.Collections.Generic;
using System.IO;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Companions;

public sealed class FranCompanionSystem : ModSystem
{
    internal const byte PacketType = 12;
    internal enum Command : byte { Summon, Dismiss, Tactic }
    private static readonly Dictionary<int, FranCompanion> CompanionCache = new();

    public override void Load()
    {
        FranCompanionAppearance.Load(Mod);
        On_Projectile.Update += UpdateOwnedProjectile;
        On_Projectile.Kill += KillOwnedProjectile;
        if (!Main.dedServ) On_Main.DrawProj += DrawOwnedProjectile;
    }

    public override void SetStaticDefaults() => FranCompanionAppearance.SetStaticDefaults();

    public override void Unload()
    {
        On_Projectile.Update -= UpdateOwnedProjectile;
        On_Projectile.Kill -= KillOwnedProjectile;
        if (!Main.dedServ) On_Main.DrawProj -= DrawOwnedProjectile;
        FranCompanionAppearance.Unload();
        CompanionCache.Clear();
    }

    public override void OnWorldLoad()
    {
        CompanionCache.Clear();
    }

    public override void OnWorldUnload()
    {
        CompanionCache.Clear();
        FranCompanionUI.Close();
    }

    internal static FranCompanion Find(int id)
    {
        if (id <= 0) return null;
        if (CompanionCache.TryGetValue(id, out FranCompanion cached))
        {
            if (cached.Projectile.active && !cached.Retiring && cached.SummonId == id &&
                ReferenceEquals(cached.Projectile.ModProjectile, cached)) return cached;
            CompanionCache.Remove(id);
        }
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is FranCompanion companion && companion.SummonId == id && !companion.Retiring)
            {
                CompanionCache[id] = companion;
                return companion;
            }
        return null;
    }

    internal static FranCompanion FindOwned(int owner)
    {
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is FranCompanion companion && companion.PlayerOwner == owner && !companion.Retiring)
                return companion;
        return null;
    }

    internal static FranCompanion Resolve(Projectile p)
    {
        // Main calls Update even for empty slots, before SetDefaults installs globals.
        // These hooks also see other mods' projectiles: never require our per-entity tag.
        if (!p.active || p.owner != FranCompanionContext.VirtualSlot ||
            !p.TryGetGlobalProjectile(out FranCompanionProjectile tag) || !tag.Linked) return null;
        return Find(tag.SummonId);
    }

    private static void UpdateOwnedProjectile(On_Projectile.orig_Update orig, Projectile projectile, int index)
    {
        FranCompanion companion = Resolve(projectile);
        if (companion is null) { orig(projectile, index); return; }
        using var scope = new FranCompanionContext.Scope(companion, simulation: true);
        orig(projectile, index);
    }

    private static void KillOwnedProjectile(On_Projectile.orig_Kill orig, Projectile projectile)
    {
        FranCompanion companion = Resolve(projectile);
        if (companion is null) { orig(projectile); return; }
        using var scope = new FranCompanionContext.Scope(companion, simulation: true);
        orig(projectile);
    }

    private static void DrawOwnedProjectile(On_Main.orig_DrawProj orig, Main self, int index)
    {
        FranCompanion companion = Resolve(Main.projectile[index]);
        if (companion is null) { orig(self, index); return; }
        using var scope = new FranCompanionContext.Scope(companion, simulation: false);
        orig(self, index);
    }

    internal static void RemoveChildren(int id)
    {
        if (id <= 0) return;
        foreach (Projectile child in Main.ActiveProjectiles)
        {
            if (!child.TryGetGlobalProjectile(out FranCompanionProjectile tag) || tag.SummonId != id) continue;
            // Dismissal must not detonate OnKill chains or spawn fresh projectiles.
            child.active = false;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.KillProjectile, -1, -1, null, child.identity, child.owner);
        }
    }

    internal static void Request(Command command, int summonId = 0, FranSkill tactic = FranSkill.None)
    {
        int player = Main.myPlayer;
        if (player < 0 || player >= Main.maxPlayers) return;
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            Execute(command, player, summonId, tactic);
            return;
        }
        if (Main.netMode != NetmodeID.MultiplayerClient) return;
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(PacketType); packet.Write((byte)command); packet.Write(summonId); packet.Write((byte)tactic);
        packet.Send();
    }

    internal static void HandlePacket(BinaryReader reader, int sender)
    {
        Command command = (Command)reader.ReadByte();
        int id = reader.ReadInt32(); FranSkill tactic = (FranSkill)reader.ReadByte();
        if (Main.netMode == NetmodeID.Server) Execute(command, sender, id, tactic);
    }

    internal static void Execute(Command command, int playerIndex, int id, FranSkill tactic)
    {
        if (playerIndex < 0 || playerIndex >= Main.maxPlayers) return;
        Player player = Main.player[playerIndex];
        if (!player.active || player.dead || player.CCed) return;
        FranCompanion existing = FindOwned(playerIndex);
        switch (command)
        {
            case Command.Dismiss when existing is not null:
                if (existing.SummonId == id && existing.CanInteract(playerIndex)) existing.Dismiss();
                break;
            case Command.Tactic when existing is not null && existing.SummonId == id && existing.CanInteract(playerIndex):
                if (tactic == FranSkill.None || FranSkillInfo.IsRealSkill(tactic) && FranSkillInfo.IsUnlocked(tactic))
                    existing.SetManual(tactic);
                break;
        }
    }
}
