using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FranMod.Content.Players;
using FranMod.Content.Projectiles;
using FranMod.Content.Systems;
using FranMod.Core;
using FranMod.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod;

public sealed class FranModMain : Mod
{
    private const byte SyncFranModeMessage = 0;
    private const byte SyncCombatStateMessage = 1;
    private const byte SyncSkillTakerMarkMessage = 2;
    private const byte RequestUltimateMessage = 3;
    private const byte SyncUltimateChargeMessage = 4;
    private const byte RequestEvolutionUnlockMessage = 5;
    private const byte RequestEvolutionResetMessage = 6;
    private const byte SyncEvolutionStateMessage = 7;
    private const byte SubmitEvolutionProfileMessage = 8;
    private const byte RequestBossProgressChangeMessage = 9;
    private const byte SyncTacticLoadoutMessage = 10;
    public static ModKeybind SkillWheelKey { get; private set; }
    public static ModKeybind UltimateKey { get; private set; }
    public static ModKeybind DashKey { get; private set; }
    public static ModKeybind EvolutionKey { get; private set; }
    internal static Asset<Effect> SlashEffect { get; private set; }
    internal static bool SlashShaderAvailable { get; private set; }

    public override void Load()
    {
        SkillWheelKey = KeybindLoader.RegisterKeybind(this, "SkillWheel", "V");
        UltimateKey = KeybindLoader.RegisterKeybind(this, "PotentialRelease", "G");
        DashKey = KeybindLoader.RegisterKeybind(this, "FranDash", "LeftShift");
        EvolutionKey = KeybindLoader.RegisterKeybind(this, "MagicStoneCorridor", "J");

        if (!Main.dedServ)
        {
            try
            {
                SlashEffect = ModContent.Request<Effect>("Fran/Effects/FranSlash", AssetRequestMode.ImmediateLoad);
                GameShaders.Misc["Fran:Slash"] = new MiscShaderData(SlashEffect, "GlitchPass");
                SlashShaderAvailable = true;
            }
            catch (Exception exception)
            {
                // A visual-only asset must never prevent the entire character mod from loading.
                // TeacherSwingRebuild  keeps its layered additive ribbons and weapon afterimages as a fallback.
                SlashEffect = null;
                SlashShaderAvailable = false;
                Logger.Warn($"Fran slash shader could not be loaded. Falling back to additive drawing. {exception}");
            }
        }
    }

    public override void Unload()
    {
        FranBladeTrailRenderer.Unload();
        FranProjectileTrailRenderer.Unload();
        FranBarrierRenderer.Unload();
        SkillWheelKey = null;
        UltimateKey = null;
        DashKey = null;
        EvolutionKey = null;
        SlashEffect = null;
        SlashShaderAvailable = false;
    }

    internal static void SendFranMode(int playerIndex, bool active, int toWho = -1, int ignoreClient = -1)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(SyncFranModeMessage);
        packet.Write((byte)playerIndex);
        packet.Write(active);
        packet.Send(toWho, ignoreClient);
    }

    internal static void SendCombatState(int playerIndex, int dashTimer, Vector2 dashVelocity,
        int toWho = -1, int ignoreClient = -1)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(SyncCombatStateMessage);
        packet.Write((byte)playerIndex);
        packet.Write((byte)Math.Clamp(dashTimer, 0, FranPlayer.DashDuration));
        packet.Write(dashVelocity.X);
        packet.Write(dashVelocity.Y);
        packet.Send(toWho, ignoreClient);
    }

    internal static void SendTacticLoadout(int playerIndex, IReadOnlyList<FranSkill> tactics,
        int toWho = -1, int ignoreClient = -1)
    {
        if (Main.netMode == NetmodeID.SinglePlayer || tactics is null)
            return;
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(SyncTacticLoadoutMessage);
        packet.Write((byte)playerIndex);
        for (int i = 0; i < FranPlayer.MaximumTacticSlots; i++)
        {
            FranSkill skill = i < tactics.Count ? tactics[i] : FranSkill.None;
            packet.Write(FranSkillInfo.IsRealSkill(skill) ? (byte)skill : (byte)FranSkill.None);
        }
        packet.Send(toWho, ignoreClient);
    }

    private static void SendTacticLoadout(int playerIndex, FranPlayer fran,
        int toWho = -1, int ignoreClient = -1)
    {
        FranSkill[] tactics = new FranSkill[FranPlayer.MaximumTacticSlots];
        for (int i = 0; i < tactics.Length; i++)
            tactics[i] = fran.GetRawTactic(i);
        SendTacticLoadout(playerIndex, tactics, toWho, ignoreClient);
    }

    internal static void SendUltimateRequest(Vector2 focusPoint)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(RequestUltimateMessage);
        packet.Write(focusPoint.X);
        packet.Write(focusPoint.Y);
        packet.Send();
    }

    internal static void SendUltimateCharge(int playerIndex, int chargeTicks,
        int toWho = -1, int ignoreClient = -1)
    {
        if (Main.netMode != NetmodeID.Server)
            return;
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(SyncUltimateChargeMessage);
        packet.Write((byte)playerIndex);
        packet.Write((ushort)Math.Clamp(chargeTicks, 0, FranPlayer.UltimateChargeDuration));
        packet.Send(toWho, ignoreClient);
    }

    internal static void SendSkillTakerMark(int npcIndex, int owner, int duration,
        int toWho = -1, int ignoreClient = -1)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(SyncSkillTakerMarkMessage);
        packet.Write((ushort)npcIndex);
        packet.Write((byte)owner);
        packet.Write((ushort)Math.Clamp(duration, 0, 10 * 60));
        packet.Send(toWho, ignoreClient);
    }

    internal static void SendEvolutionUnlockRequest(FranEvolutionNode node)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient || !FranEvolutionInfo.IsValid(node))
            return;
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(RequestEvolutionUnlockMessage);
        packet.Write((byte)node);
        packet.Send();
    }

    internal static void SendEvolutionResetRequest()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(RequestEvolutionResetMessage);
        packet.Send();
    }

    internal static void SendEvolutionProfile(FranEvolutionPlayer evolution)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient || evolution is null)
            return;
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(SubmitEvolutionProfileMessage);
        packet.Write(evolution.NodeMask);
        // Character-side boss history is never authoritative. The server reconstructs points from its world.
        packet.Write((ushort)0);
        packet.Send();
    }

    internal static void SendEvolutionState(int playerIndex, uint nodeMask, int earnedPoints,
        IReadOnlyCollection<string> claims, int toWho = -1, int ignoreClient = -1)
    {
        if (Main.netMode != NetmodeID.Server || !ValidPlayer(playerIndex))
            return;
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(SyncEvolutionStateMessage);
        packet.Write((byte)playerIndex);
        packet.Write(nodeMask);
        packet.Write((ushort)Math.Clamp(earnedPoints, 0, ushort.MaxValue));
        bool includeClaims = claims is not null;
        packet.Write(includeClaims);
        if (includeClaims)
        {
            packet.Write((ushort)Math.Min(claims.Count, FranEvolutionInfo.MaximumClaimedBosses));
            int written = 0;
            foreach (string key in claims)
            {
                if (written++ >= FranEvolutionInfo.MaximumClaimedBosses)
                    break;
                WriteBossKey(packet, key);
            }
        }
        packet.Send(toWho, ignoreClient);
    }

    internal static void BroadcastEvolutionState(Player player)
    {
        if (Main.netMode != NetmodeID.Server || player is null || !player.active)
            return;
        FranEvolutionPlayer evolution = player.GetModPlayer<FranEvolutionPlayer>();
        SendEvolutionState(player.whoAmI, evolution.NodeMask, evolution.EarnedPoints,
            evolution.GetClaimedBosses(), player.whoAmI);
        SendEvolutionState(player.whoAmI, evolution.NodeMask, evolution.EarnedPoints,
            null, -1, player.whoAmI);
    }

    internal static void RequestBossProgressChange(FranBossProgressAction action, int index, bool state)
    {
        if (!Enum.IsDefined(action) ||
            (action == FranBossProgressAction.SetSingle &&
             (index < 0 || index >= FranBossProgressRegistry.Entries.Length)))
            return;

        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            FranBossProgressRegistry.Apply(action, index, state);
            return;
        }
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(RequestBossProgressChangeMessage);
        packet.Write((byte)action);
        packet.Write((byte)Math.Clamp(index, 0, byte.MaxValue));
        packet.Write(state);
        packet.Send();
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        byte messageType = reader.ReadByte();
        switch (messageType)
        {
            case SyncFranModeMessage:
            {
                int playerIndex = reader.ReadByte();
                bool active = reader.ReadBoolean();
                if (Main.netMode == NetmodeID.Server)
                    playerIndex = whoAmI;
                if (!ValidPlayer(playerIndex))
                    return;

                Main.player[playerIndex].GetModPlayer<FranPlayer>().ReceiveNetworkMode(active);
                if (Main.netMode == NetmodeID.Server)
                    SendFranMode(playerIndex, active, -1, whoAmI);
                break;
            }
            case SyncCombatStateMessage:
            {
                int playerIndex = reader.ReadByte();
                int dashTimer = reader.ReadByte();
                Vector2 dashVelocity = new(reader.ReadSingle(), reader.ReadSingle());
                if (Main.netMode == NetmodeID.Server)
                    playerIndex = whoAmI;
                if (!ValidPlayer(playerIndex))
                    return;

                dashTimer = Math.Clamp(dashTimer, 0, FranPlayer.DashDuration);
                float dashSpeed = dashVelocity.Length();
                if (!float.IsFinite(dashVelocity.X) || !float.IsFinite(dashVelocity.Y) ||
                    !float.IsFinite(dashSpeed) || dashTimer == 0 || dashSpeed < 1f)
                {
                    dashVelocity = Vector2.Zero;
                    dashTimer = 0;
                }
                else
                {
                    if (!Main.player[playerIndex].GetModPlayer<FranEvolutionPlayer>()
                            .HasNode(FranEvolutionNode.AerialHunt))
                        dashVelocity.Y = 0f;
                    if (dashVelocity.LengthSquared() > 22f * 22f)
                        dashVelocity = dashVelocity.SafeNormalize(Vector2.UnitX) * 22f;
                }

                Main.player[playerIndex].GetModPlayer<FranPlayer>()
                    .ReceiveNetworkCombatState(dashTimer, dashVelocity);
                if (Main.netMode == NetmodeID.Server)
                    SendCombatState(playerIndex, dashTimer, dashVelocity, -1, whoAmI);
                break;
            }
            case SyncSkillTakerMarkMessage:
            {
                int npcIndex = reader.ReadUInt16();
                int owner = reader.ReadByte();
                int duration = Math.Clamp((int)reader.ReadUInt16(), 0, 10 * 60);
                if (Main.netMode == NetmodeID.Server)
                    owner = whoAmI;
                if (!ValidPlayer(owner) || !ValidNpc(npcIndex) || duration <= 0)
                    return;

                Main.npc[npcIndex].GetGlobalNPC<SkillTakerGlobalNPC>().Mark(owner, duration);
                if (Main.netMode == NetmodeID.Server)
                    SendSkillTakerMark(npcIndex, owner, duration, -1, whoAmI);
                break;
            }
            case RequestUltimateMessage:
            {
                Vector2 focusPoint = new(reader.ReadSingle(), reader.ReadSingle());
                if (Main.netMode != NetmodeID.Server || !ValidPlayer(whoAmI))
                    return;
                Main.player[whoAmI].GetModPlayer<FranPlayer>().TryStartUltimate(focusPoint);
                break;
            }
            case SyncUltimateChargeMessage:
            {
                int playerIndex = reader.ReadByte();
                int chargeTicks = reader.ReadUInt16();
                if (Main.netMode != NetmodeID.MultiplayerClient || !ValidPlayer(playerIndex))
                    return;
                Main.player[playerIndex].GetModPlayer<FranPlayer>().ReceiveUltimateCharge(chargeTicks);
                break;
            }
            case RequestEvolutionUnlockMessage:
            {
                FranEvolutionNode node = (FranEvolutionNode)reader.ReadByte();
                if (Main.netMode != NetmodeID.Server || !ValidPlayer(whoAmI) || !FranEvolutionInfo.IsValid(node))
                    return;
                Main.player[whoAmI].GetModPlayer<FranEvolutionPlayer>().TryUnlockAuthoritative(node);
                break;
            }
            case RequestEvolutionResetMessage:
            {
                if (Main.netMode != NetmodeID.Server || !ValidPlayer(whoAmI))
                    return;
                Main.player[whoAmI].GetModPlayer<FranEvolutionPlayer>().TryResetAuthoritative();
                break;
            }
            case SyncEvolutionStateMessage:
            {
                int playerIndex = reader.ReadByte();
                uint mask = reader.ReadUInt32();
                int earnedPoints = reader.ReadUInt16();
                bool includesClaims = reader.ReadBoolean();
                if (Main.netMode != NetmodeID.MultiplayerClient || !ValidPlayer(playerIndex))
                    return;
                List<string> claims = null;
                if (includesClaims)
                {
                    int count = reader.ReadUInt16();
                    if (count > FranEvolutionInfo.MaximumClaimedBosses)
                        return;
                    claims = new List<string>(count);
                    for (int i = 0; i < count; i++)
                    {
                        string key = ReadBossKey(reader);
                        if (FranEvolutionBossSystem.IsValidBossKey(key))
                            claims.Add(key);
                    }
                }
                Main.player[playerIndex].GetModPlayer<FranEvolutionPlayer>()
                    .ReceiveEvolutionState(mask, earnedPoints, claims);
                break;
            }
            case SubmitEvolutionProfileMessage:
            {
                uint requestedMask = reader.ReadUInt32();
                int count = reader.ReadUInt16();
                if (Main.netMode != NetmodeID.Server || !ValidPlayer(whoAmI) ||
                    count > FranEvolutionInfo.MaximumClaimedBosses)
                    return;
                List<string> keys = new(count);
                for (int i = 0; i < count; i++)
                {
                    string key = ReadBossKey(reader);
                    if (FranEvolutionBossSystem.IsValidBossKey(key))
                        keys.Add(key);
                }
                FranEvolutionPlayer evolution = Main.player[whoAmI].GetModPlayer<FranEvolutionPlayer>();
                evolution.MergeClientProfile(keys, requestedMask);
                BroadcastEvolutionState(Main.player[whoAmI]);
                break;
            }
            case RequestBossProgressChangeMessage:
            {
                FranBossProgressAction action = (FranBossProgressAction)reader.ReadByte();
                int index = reader.ReadByte();
                bool state = reader.ReadBoolean();
                if (Main.netMode != NetmodeID.Server || !ValidPlayer(whoAmI) || !Enum.IsDefined(action))
                    return;
                Player requester = Main.player[whoAmI];
                if (requester.HeldItem.type != ModContent.ItemType<Content.Items.VanillaBossProgressAdjuster>() ||
                    (action == FranBossProgressAction.SetSingle &&
                     (index < 0 || index >= FranBossProgressRegistry.Entries.Length)))
                    return;
                if (FranBossProgressRegistry.Apply(action, index, state) > 0)
                    NetMessage.SendData(MessageID.WorldData);
                break;
            }
            case SyncTacticLoadoutMessage:
            {
                int playerIndex = reader.ReadByte();
                FranSkill[] tactics = new FranSkill[FranPlayer.MaximumTacticSlots];
                for (int i = 0; i < tactics.Length; i++)
                {
                    FranSkill skill = (FranSkill)reader.ReadByte();
                    tactics[i] = FranSkillInfo.IsRealSkill(skill) ? skill : FranSkill.None;
                }
                if (Main.netMode == NetmodeID.Server)
                    playerIndex = whoAmI;
                if (!ValidPlayer(playerIndex))
                    return;

                FranPlayer fran = Main.player[playerIndex].GetModPlayer<FranPlayer>();
                fran.ReceiveTacticLoadout(tactics);
                if (Main.netMode == NetmodeID.Server)
                    SendTacticLoadout(playerIndex, fran, -1, whoAmI);
                break;
            }
        }
    }

    private static bool ValidPlayer(int playerIndex) =>
        playerIndex >= 0 && playerIndex < Main.maxPlayers && Main.player[playerIndex].active;

    private static bool ValidNpc(int npcIndex) =>
        npcIndex >= 0 && npcIndex < Main.maxNPCs && Main.npc[npcIndex].active;

    private static void WriteBossKey(BinaryWriter writer, string key)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(key);
        int length = Math.Min(bytes.Length, FranEvolutionInfo.MaximumBossKeyLength);
        writer.Write((byte)length);
        writer.Write(bytes, 0, length);
    }

    private static string ReadBossKey(BinaryReader reader)
    {
        int length = reader.ReadByte();
        byte[] bytes = reader.ReadBytes(length);
        if (length > FranEvolutionInfo.MaximumBossKeyLength || bytes.Length != length)
            return string.Empty;
        return Encoding.ASCII.GetString(bytes);
    }
}
