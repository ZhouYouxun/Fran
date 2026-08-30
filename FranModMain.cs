using System;
using System.IO;
using FranMod.Content.Players;
using FranMod.Content.Projectiles;
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
    public static ModKeybind SkillWheelKey { get; private set; }
    public static ModKeybind UltimateKey { get; private set; }
    public static ModKeybind DashKey { get; private set; }
    internal static Asset<Effect> SlashEffect { get; private set; }
    internal static bool SlashShaderAvailable { get; private set; }

    public override void Load()
    {
        SkillWheelKey = KeybindLoader.RegisterKeybind(this, "SkillWheel", "V");
        UltimateKey = KeybindLoader.RegisterKeybind(this, "PotentialRelease", "G");
        DashKey = KeybindLoader.RegisterKeybind(this, "FranDash", "LeftShift");

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
                float dashSpeed = Math.Abs(dashVelocity.X);
                if (!float.IsFinite(dashVelocity.X) || !float.IsFinite(dashVelocity.Y) ||
                    !float.IsFinite(dashSpeed) || dashTimer == 0 || dashSpeed < 1f)
                {
                    dashVelocity = Vector2.Zero;
                    dashTimer = 0;
                }
                else
                    dashVelocity = new Vector2(MathHelper.Clamp(dashVelocity.X, -22f, 22f), 0f);

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
        }
    }

    private static bool ValidPlayer(int playerIndex) =>
        playerIndex >= 0 && playerIndex < Main.maxPlayers && Main.player[playerIndex].active;

    private static bool ValidNpc(int npcIndex) =>
        npcIndex >= 0 && npcIndex < Main.maxNPCs && Main.npc[npcIndex].active;
}
