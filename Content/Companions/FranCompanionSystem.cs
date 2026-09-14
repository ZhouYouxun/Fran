using System;
using System.IO;
using FranMod.Content.Items;
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
    private static int nextSummonId;
    private static readonly ulong[] LastSummonRequest = new ulong[255];

    public override void Load()
    {
        FranCompanionAppearance.Load(Mod);
        On_Projectile.Update += UpdateOwnedProjectile;
        On_Projectile.Kill += KillOwnedProjectile;
        if (!Main.dedServ) On_Main.DrawProj += DrawOwnedProjectile;
    }

    public override void Unload()
    {
        On_Projectile.Update -= UpdateOwnedProjectile;
        On_Projectile.Kill -= KillOwnedProjectile;
        if (!Main.dedServ) On_Main.DrawProj -= DrawOwnedProjectile;
        FranCompanionAppearance.Unload();
    }

    public override void OnWorldLoad() => Array.Clear(LastSummonRequest);
    public override void OnWorldUnload() => FranCompanionUI.Close();

    internal static FranCompanion Find(int id)
    {
        if (id <= 0) return null;
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is FranCompanion companion && companion.SummonId == id && !companion.Retiring)
                return companion;
        return null;
    }

    internal static FranCompanion FindOwned(int owner)
    {
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.ModProjectile is FranCompanion companion && companion.PlayerOwner == owner && !companion.Retiring)
                return companion;
        return null;
    }

    private static FranCompanion Resolve(Projectile p) =>
        Find(p.GetGlobalProjectile<FranCompanionProjectile>().SummonId);

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
            if (child.GetGlobalProjectile<FranCompanionProjectile>().SummonId != id) continue;
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
        bool holdingStaff = player.HeldItem.type == ModContent.ItemType<FranCompanionStaff>();
        switch (command)
        {
            case Command.Summon when holdingStaff:
                if (LastSummonRequest[playerIndex] != 0 && Main.GameUpdateCount - LastSummonRequest[playerIndex] < 20) return;
                LastSummonRequest[playerIndex] = Main.GameUpdateCount;
                if (existing is not null) { existing.Recall(player); return; }
                int index = Projectile.NewProjectile(player.GetSource_ItemUse(player.HeldItem),
                    player.Center - new Vector2(player.direction * 48f, 48f), Vector2.Zero,
                    ModContent.ProjectileType<FranCompanion>(), 0, 0f, FranCompanionContext.VirtualSlot,
                    playerIndex, (byte)FranSkill.TelekineticCatapult);
                if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].ModProjectile is FranCompanion summon)
                {
                    // NewProjectile initializes ai[] only for Main.myPlayer; root creation
                    // in singleplayer intentionally keeps the real player's identity intact.
                    summon.Projectile.ai[0] = playerIndex;
                    summon.Projectile.ai[1] = (byte)FranSkill.TelekineticCatapult;
                    summon.Projectile.ai[2] = 0f;
                    summon.SummonId = ++nextSummonId;
                    summon.Recall(player);
                    summon.Projectile.netUpdate = true;
                }
                break;
            case Command.Dismiss when existing is not null:
                if (holdingStaff || existing.SummonId == id && existing.CanInteract(playerIndex)) existing.Dismiss();
                break;
            case Command.Tactic when existing is not null && existing.SummonId == id && existing.CanInteract(playerIndex):
                if (tactic == FranSkill.None || FranSkillInfo.IsRealSkill(tactic) && FranSkillInfo.IsUnlocked(tactic))
                    existing.SetManual(tactic);
                break;
        }
    }
}
