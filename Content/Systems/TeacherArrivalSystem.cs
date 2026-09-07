using System;
using System.Collections.Generic;
using System.IO;
using FranMod.Content.Items;
using FranMod.Content.Players;
using FranMod.Content.Projectiles;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FranMod.Content.Systems;

internal sealed class TeacherArrivalRecord
{
    public int Id;
    public Guid CharacterId;
    public bool IsDebug;
    public Vector2 Landing;
    public int Age;
    public int ItemIndex = -1;
    public int ProjectionIndex = -1;
    public int MissingItemTicks;
}

// Server owns the ledger, spawning and world persistence. Vanilla owns inventory pickup.
// Character lifetime and world lifetime are deliberately separate: other players may take the sword.
public sealed class TeacherArrivalSystem : ModSystem
{
    private enum Message : byte { Profile, Acknowledgement, Event, Pickup, Removed, Readiness, DebugArrival }
    // Keep the world-sync payload comfortably below Terraria's 16-bit packet length.
    private const int MaximumPending = 512;
    private readonly Dictionary<int, TeacherArrivalRecord> arrivals = new();
    private readonly HashSet<Guid> spentCharacters = new();
    private int nextId = 1;
    internal static TeacherArrivalSystem Instance => ModContent.GetInstance<TeacherArrivalSystem>();
    internal IEnumerable<TeacherArrivalRecord> Arrivals => arrivals.Values;
    internal bool TryGet(int id, out TeacherArrivalRecord record) => arrivals.TryGetValue(id, out record);

    public override void ClearWorld()
    {
        arrivals.Clear();
        spentCharacters.Clear();
        nextId = 1;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        var saved = new List<TagCompound>();
        foreach (TeacherArrivalRecord record in arrivals.Values)
            saved.Add(new TagCompound {
                ["Id"] = record.Id, ["Character"] = record.CharacterId.ToString("N"),
                ["Debug"] = record.IsDebug,
                ["X"] = record.Landing.X, ["Y"] = record.Landing.Y,
                // Reload restores the planted sword, never a second thunder performance.
                ["Age"] = TeacherArrivalRules.SettledAge
            });
        tag["TeacherArrivals"] = saved;
        var spent = new List<string>();
        foreach (Guid id in spentCharacters)
            spent.Add(id.ToString("N"));
        tag["TeacherArrivalSpent"] = spent;
        tag["TeacherArrivalNextId"] = nextId;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        ClearWorld();
        foreach (string value in tag.GetList<string>("TeacherArrivalSpent"))
            if (Guid.TryParse(value, out Guid id) && id != Guid.Empty)
                spentCharacters.Add(id);
        foreach (TagCompound saved in tag.GetList<TagCompound>("TeacherArrivals"))
        {
            int id = saved.GetInt("Id");
            Vector2 landing = new(saved.GetFloat("X"), saved.GetFloat("Y"));
            bool isDebug = saved.GetBool("Debug");
            if (id <= 0 || id == int.MaxValue || !ValidPosition(landing) ||
                !Guid.TryParse(saved.GetString("Character"), out Guid character) || !isDebug && character == Guid.Empty)
                continue;
            arrivals[id] = new TeacherArrivalRecord {
                Id = id, CharacterId = character, IsDebug = isDebug,
                Landing = landing, Age = TeacherArrivalRules.SettledAge
            };
            if (!isDebug)
                spentCharacters.Add(character);
            nextId = Math.Max(nextId, id + 1);
        }
        nextId = Math.Max(nextId, Math.Clamp(tag.GetInt("TeacherArrivalNextId"), 1, int.MaxValue - 1));
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(arrivals.Count);
        foreach (TeacherArrivalRecord record in arrivals.Values)
            WriteRecord(writer, record);
    }

    public override void NetReceive(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > MaximumPending)
            throw new InvalidDataException("Invalid Teacher arrival count.");
        arrivals.Clear();
        for (int i = 0; i < count; i++)
        {
            TeacherArrivalRecord record = ReadRecord(reader);
            if (ValidPosition(record.Landing))
                arrivals[record.Id] = record;
        }
    }

    // PostUpdateWorld is server/single-player only. Presentation clocks must also tick on clients.
    public override void PostUpdateTime()
    {
        foreach (TeacherArrivalRecord record in arrivals.Values)
        {
            record.Age = Math.Min(record.Age + 1, TeacherArrivalRules.SettledAge);
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (record.Age >= TeacherArrivalRules.LandingAge)
                    MaintainWorldItem(record);
                MaintainProjection(record);
            }
            if (!Main.dedServ)
                TeacherArrivalVisuals.Update(record);
        }
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        foreach (Player player in Main.ActivePlayers)
        {
            TeacherArrivalPlayer arrival = player.GetModPlayer<TeacherArrivalPlayer>();
            if (!arrival.ProfileReady)
                continue;
            if (spentCharacters.Contains(arrival.CharacterId) && !arrival.OpportunitySpent)
            {
                arrival.Spend();
                Acknowledge(player.whoAmI, arrival);
            }
            bool presentationReady = arrival.PresentationReady && (Main.netMode == NetmodeID.SinglePlayer ||
                Main.GameUpdateCount - arrival.LastReadinessTick < 180);
            if (!player.dead && presentationReady)
                arrival.EntryAge = Math.Min(arrival.EntryAge + 1, TeacherArrivalRules.EntryDelay + 120);
            if (!TeacherArrivalRules.Eligible(arrival.ProfileReady, arrival.OpportunitySpent,
                    spentCharacters.Contains(arrival.CharacterId), !player.dead, arrival.EntryAge) ||
                (Main.GameUpdateCount + (ulong)player.whoAmI * 7) % 60 != 0 ||
                arrivals.Count >= MaximumPending || nextId == int.MaxValue || !presentationReady)
                continue;
            if (TryFindLanding(player, out Vector2 landing))
                StartArrival(player, arrival, landing);
        }
    }

    private void StartArrival(Player player, TeacherArrivalPlayer arrival, Vector2 landing)
    {
        var record = new TeacherArrivalRecord {
            Id = nextId++, CharacterId = arrival.CharacterId, Landing = landing
        };
        arrivals.Add(record.Id, record);
        spentCharacters.Add(arrival.CharacterId);
        arrival.Spend();
        Acknowledge(player.whoAmI, arrival);
        BeginPresentation(record);
    }

    internal static void RequestDebugArrival(Player player, Vector2 target)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            Instance.StartDebugArrival(target);
        else if (Main.netMode == NetmodeID.MultiplayerClient && player.whoAmI == Main.myPlayer)
        {
            ModPacket packet = Packet(Message.DebugArrival);
            packet.Write(target.X);
            packet.Write(target.Y);
            packet.Send();
        }
    }

    private void StartDebugArrival(Vector2 target)
    {
        // Only engine safety limits apply. No profile, lifetime, delay, surface, roof or visibility gate.
        if (Main.netMode == NetmodeID.MultiplayerClient || !TeacherArrivalRules.Finite(target) ||
            arrivals.Count >= MaximumPending || nextId == int.MaxValue)
            return;
        Vector2 landing = FindDebugLanding(target);
        var record = new TeacherArrivalRecord {
            Id = nextId++, CharacterId = Guid.Empty, IsDebug = true, Landing = landing
        };
        arrivals.Add(record.Id, record);
        // Debug rewards persist and can be picked up, but never modify any character's opportunity.
        BeginPresentation(record);
    }

    private static Vector2 FindDebugLanding(Vector2 target)
    {
        int x = Math.Clamp((int)(Math.Clamp(target.X, 160f, (Main.maxTilesX - 11) * 16f) / 16f),
            10, Main.maxTilesX - 11);
        int y = Math.Clamp((int)(Math.Clamp(target.Y, 160f, (Main.maxTilesY - 11) * 16f) / 16f),
            10, Main.maxTilesY - 11);
        // Prefer a nearby tile top under the cursor, including underground/platforms.
        // If there is no ground, still play at the cursor instead of silently rejecting a debug click.
        for (int yy = Math.Max(10, y - 6); yy <= Math.Min(Main.maxTilesY - 11, y + 30); yy++)
        {
            Tile tile = Framing.GetTileSafely(x, yy);
            Tile above = Framing.GetTileSafely(x, yy - 1);
            if (tile.HasTile && !tile.IsActuated &&
                (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]) &&
                (!above.HasTile || above.IsActuated || !Main.tileSolid[above.TileType]))
                return new Vector2(x * 16f + 8f, yy * 16f + 7f + (tile.IsHalfBlock ? 8f : 0f));
        }
        return new Vector2(x * 16f + 8f, y * 16f + 7f);
    }

    private void BeginPresentation(TeacherArrivalRecord record)
    {
        Broadcast(record);
        MaintainProjection(record);
        // Do not truncate or replace rain that was already occurring naturally.
        if (!Main.raining)
        {
            Main.StartRain();
            Main.rainTime = 600;
            Main.maxRaining = 0.55f;
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }
    }

    private void MaintainProjection(TeacherArrivalRecord record)
    {
        bool needProjection = record.Age < TeacherArrivalRules.LandingAge || record.ItemIndex < 0;
        if (!needProjection)
            return;
        if (Main.projectile.IndexInRange(record.ProjectionIndex) &&
            Main.projectile[record.ProjectionIndex] is Projectile current && current.active &&
            current.type == ModContent.ProjectileType<TeacherArrivalProjectile>() && (int)current.ai[0] == record.Id)
            return;
        if (Main.GameUpdateCount % 30 != 0 && record.Age > 0)
            return;
        record.ProjectionIndex = Projectile.NewProjectile(new EntitySource_Misc("TeacherArrival"),
            record.Landing, Vector2.Zero, ModContent.ProjectileType<TeacherArrivalProjectile>(),
            0, 0f, Main.myPlayer, record.Id);
    }

    private void MaintainWorldItem(TeacherArrivalRecord record)
    {
        if (Main.item.IndexInRange(record.ItemIndex))
        {
            Item current = Main.item[record.ItemIndex];
            if (current.active && current.type == ModContent.ItemType<Teacher>() &&
                current.GetGlobalItem<TeacherArrivalWorldItem>().ArrivalId == record.Id)
            {
                record.MissingItemTicks = 0;
                return;
            }
            // Give the normal pickup packet and its notification time to arrive.
            if (++record.MissingItemTicks < 90)
                return;
            record.ItemIndex = -1;
        }
        // Cooperate with world-item persistence mods / soft reloads: adopt an existing projection.
        for (int i = 0; i < Main.maxItems; i++)
            if (Main.item[i].active && Main.item[i].type == ModContent.ItemType<Teacher>() &&
                Main.item[i].GetGlobalItem<TeacherArrivalWorldItem>().ArrivalId == record.Id)
            {
                record.ItemIndex = i;
                return;
            }
        // Never evict another world item just to create this event's reward.
        bool free = false;
        for (int i = 0; i < Main.maxItems; i++)
            if (!Main.item[i].active && Main.timeItemSlotCannotBeReusedFor[i] == 0) { free = true; break; }
        if (!free)
            return;
        int index = Item.NewItem(new EntitySource_Misc("TeacherArrivalLanding"), record.Landing,
            ModContent.ItemType<Teacher>(), 1, noBroadcast: true, noGrabDelay: true);
        if (index < 0 || index >= Main.maxItems)
            return;
        Item item = Main.item[index];
        item.GetGlobalItem<TeacherArrivalWorldItem>().Plant(record.Id, record.Landing, record.Age);
        item.velocity = Vector2.Zero;
        item.Center = record.Landing - Vector2.UnitY * 30f;
        item.newAndShiny = false;
        record.ItemIndex = index;
        record.MissingItemTicks = 0;
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncItem, -1, -1, null, index);
        Broadcast(record);
    }

    private bool TryFindLanding(Player player, out Vector2 landing)
    {
        landing = default;
        if (!TeacherArrivalRules.Finite(player.Center) || player.Center.Y > Main.worldSurface * 16f ||
            player.Center.Y < 320f || player.pulley)
            return false;
        int px = (int)(player.Center.X / 16f);
        int py = (int)(player.Bottom.Y / 16f);
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int side = (attempt % 2 == 0 ? player.direction : -player.direction);
            int x = px + side * (9 + attempt / 2);
            if (x < 12 || x >= Main.maxTilesX - 12)
                continue;
            for (int y = Math.Max(12, py - 7); y <= Math.Min(Main.maxTilesY - 12, py + 15); y++)
            {
                if (!SolidFloor(x, y) || !SolidFloor(x - 1, y) || !SolidFloor(x + 1, y))
                    continue;
                bool clear = true;
                for (int xx = x - 1; xx <= x + 1 && clear; xx++)
                    for (int yy = y - 1; yy >= 10; yy--)
                    {
                        Tile tile = Framing.GetTileSafely(xx, yy);
                        if (tile.LiquidAmount > 0 || tile.HasTile && !tile.IsActuated &&
                            (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                        { clear = false; break; }
                    }
                Vector2 candidate = new(x * 16f + 8f, y * 16f + 7f);
                if (!clear || !Collision.CanHitLine(player.Center, 1, 1,
                        candidate - Vector2.UnitY * 44f, 1, 1))
                    continue;
                bool occupied = false;
                foreach (TeacherArrivalRecord other in arrivals.Values)
                    if (Vector2.DistanceSquared(other.Landing, candidate) < 80f * 80f) { occupied = true; break; }
                if (occupied)
                    continue;
                landing = candidate;
                return true;
            }
        }
        return false;
    }

    private static bool SolidFloor(int x, int y)
    {
        Tile tile = Framing.GetTileSafely(x, y);
        return tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] &&
            !Main.tileSolidTop[tile.TileType] && !tile.IsHalfBlock && tile.Slope == SlopeType.Solid &&
            tile.LiquidAmount == 0;
    }

    private static bool ValidPosition(Vector2 p) => TeacherArrivalRules.Finite(p) &&
        p.X >= 160f && p.Y >= 160f && p.X < (Main.maxTilesX - 10) * 16f && p.Y < (Main.maxTilesY - 10) * 16f;

    private static ModPacket Packet(Message message)
    {
        ModPacket packet = ModContent.GetInstance<FranModMain>().GetPacket();
        packet.Write(FranModMain.TeacherArrivalMessage);
        packet.Write((byte)message);
        return packet;
    }

    internal static void SendProfile(TeacherArrivalPlayer player)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;
        ModPacket packet = Packet(Message.Profile);
        packet.Write(player.CharacterId.ToByteArray());
        packet.Write(player.OpportunitySpent);
        packet.Send();
    }

    internal static void SendReadiness(bool ready)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;
        ModPacket packet = Packet(Message.Readiness);
        packet.Write(ready);
        packet.Send();
    }

    private static void Acknowledge(int playerIndex, TeacherArrivalPlayer player)
    {
        if (Main.netMode != NetmodeID.Server)
            return;
        ModPacket packet = Packet(Message.Acknowledgement);
        packet.Write(player.CharacterId.ToByteArray());
        packet.Write(player.OpportunitySpent);
        packet.Send(playerIndex);
    }

    private static void Broadcast(TeacherArrivalRecord record)
    {
        if (Main.netMode != NetmodeID.Server)
            return;
        ModPacket packet = Packet(Message.Event);
        WriteRecord(packet, record);
        packet.Send();
    }

    internal static void NotifyPickup(int id, int itemIndex)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            Instance.arrivals.Remove(id);
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = Packet(Message.Pickup);
            packet.Write(id);
            packet.Write((short)itemIndex);
            packet.Send();
        }
    }

    internal static void HandlePacket(BinaryReader reader, int sender)
    {
        Message message = (Message)reader.ReadByte();
        bool server = Main.netMode == NetmodeID.Server;
        bool client = Main.netMode == NetmodeID.MultiplayerClient;
        switch (message)
        {
            case Message.DebugArrival when server:
            {
                Vector2 target = new(reader.ReadSingle(), reader.ReadSingle());
                // Validate the requesting item, not the ordinary ceremony's eligibility conditions.
                if (Main.player.IndexInRange(sender) && Main.player[sender].active &&
                    Main.player[sender].HeldItem.type == ModContent.ItemType<TeacherArrivalDebugger>())
                    Instance.StartDebugArrival(target);
                break;
            }
            case Message.Readiness when server:
            {
                bool ready = reader.ReadBoolean();
                if (!Main.player.IndexInRange(sender) || !Main.player[sender].active)
                    return;
                var player = Main.player[sender].GetModPlayer<TeacherArrivalPlayer>();
                player.PresentationReady = ready;
                player.LastReadinessTick = Main.GameUpdateCount;
                break;
            }
            case Message.Profile when server:
            {
                Guid id = new(reader.ReadBytes(16));
                bool spent = reader.ReadBoolean();
                if (!Main.player.IndexInRange(sender) || !Main.player[sender].active)
                    return;
                var player = Main.player[sender].GetModPlayer<TeacherArrivalPlayer>();
                if (!player.AcceptProfile(id, spent || Instance.spentCharacters.Contains(id)))
                    return;
                Acknowledge(sender, player);
                break;
            }
            case Message.Acknowledgement when client:
                Main.LocalPlayer.GetModPlayer<TeacherArrivalPlayer>().Acknowledge(
                    new Guid(reader.ReadBytes(16)), reader.ReadBoolean());
                break;
            case Message.Event when client:
            {
                TeacherArrivalRecord record = ReadRecord(reader);
                if (ValidPosition(record.Landing) && record.Id > 0)
                    Instance.arrivals[record.Id] = record;
                break;
            }
            case Message.Pickup when server:
            {
                int id = reader.ReadInt32();
                int index = reader.ReadInt16();
                // Death can arrive ahead of this notification under latency; native item ownership is authoritative.
                if (!Main.player.IndexInRange(sender) || !Main.player[sender].active ||
                    !Instance.arrivals.TryGetValue(id, out TeacherArrivalRecord record) || record.ItemIndex != index ||
                    record.Age < TeacherArrivalRules.LandingAge + TeacherArrivalRules.HighlightHold ||
                    index < 0 || index >= Main.maxItems ||
                    (Main.item[index].playerIndexTheItemIsReservedFor != sender &&
                     Vector2.DistanceSquared(Main.player[sender].Center, record.Landing) > 240f * 240f))
                    return;
                // The normal item-owner/SyncItem protocol arbitrates simultaneous grabs.
                Instance.arrivals.Remove(id);
                ModPacket packet = Packet(Message.Removed);
                packet.Write(id);
                packet.Send();
                break;
            }
            case Message.Removed when client:
                Instance.arrivals.Remove(reader.ReadInt32());
                break;
        }
    }

    private static void WriteRecord(BinaryWriter writer, TeacherArrivalRecord record)
    {
        writer.Write(record.Id);
        writer.Write(record.CharacterId.ToByteArray());
        writer.Write(record.Landing.X);
        writer.Write(record.Landing.Y);
        writer.Write(record.Age);
        writer.Write((short)record.ItemIndex);
        writer.Write(record.IsDebug);
    }

    private static TeacherArrivalRecord ReadRecord(BinaryReader reader) => new() {
        Id = reader.ReadInt32(), CharacterId = new Guid(reader.ReadBytes(16)),
        Landing = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
        Age = Math.Clamp(reader.ReadInt32(), 0, TeacherArrivalRules.SettledAge),
        ItemIndex = reader.ReadInt16(), IsDebug = reader.ReadBoolean()
    };
}
