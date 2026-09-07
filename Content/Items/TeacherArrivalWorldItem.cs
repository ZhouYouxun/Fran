using System;
using System.IO;
using FranMod.Content.Systems;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FranMod.Content.Items;

// This is the real Teacher item, not a second collectible currency or a client-side reward.
// Its planted presentation is stripped on pickup; ordinary Teacher items keep their existing behavior.
public sealed class TeacherArrivalWorldItem : GlobalItem
{
    public override bool InstancePerEntity => true;
    public override bool AppliesToEntity(Item entity, bool lateInstantiation) =>
        entity.type == ModContent.ItemType<Teacher>();
    internal int ArrivalId { get; private set; }
    internal Vector2 Landing { get; private set; }
    private int age;

    internal void Plant(int id, Vector2 landing, int eventAge)
    {
        ArrivalId = id;
        Landing = landing;
        age = eventAge;
    }

    // Vanilla worlds use the system's ledger. Persist metadata too for world-item persistence mods.
    public override void SaveData(Item item, TagCompound tag)
    {
        if (ArrivalId <= 0)
            return;
        tag["ArrivalId"] = ArrivalId;
        tag["LandingX"] = Landing.X;
        tag["LandingY"] = Landing.Y;
    }

    public override void LoadData(Item item, TagCompound tag)
    {
        Vector2 landing = new(tag.GetFloat("LandingX"), tag.GetFloat("LandingY"));
        int id = tag.GetInt("ArrivalId");
        if (id > 0 && TeacherArrivalRules.Finite(landing))
            Plant(id, landing, TeacherArrivalRules.SettledAge);
    }

    public override void NetSend(Item item, BinaryWriter writer)
    {
        writer.Write(ArrivalId);
        if (ArrivalId <= 0)
            return;
        writer.Write(Landing.X);
        writer.Write(Landing.Y);
        writer.Write(age);
    }

    public override void NetReceive(Item item, BinaryReader reader)
    {
        ArrivalId = reader.ReadInt32();
        if (ArrivalId <= 0)
            return;
        Landing = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        age = Math.Clamp(reader.ReadInt32(), 0, TeacherArrivalRules.SettledAge);
        if (!TeacherArrivalRules.Finite(Landing))
            ArrivalId = 0;
    }

    public override void Update(Item item, ref float gravity, ref float maxFallSpeed)
    {
        if (ArrivalId <= 0)
            return;
        gravity = maxFallSpeed = 0f;
        Anchor(item);
    }

    public override void PostUpdate(Item item)
    {
        if (ArrivalId <= 0)
            return;
        age = Math.Min(age + 1, TeacherArrivalRules.SettledAge);
        Anchor(item);
        item.timeSinceItemSpawned = 0;
        if (!Main.dedServ)
        {
            float highlight = TeacherArrivalRules.Highlight(age);
            Lighting.AddLight(Landing - Vector2.UnitY * 45f,
                new Vector3(0.28f, 0.66f, 1f) * (0.18f + highlight * 1.35f));
        }
    }

    private void Anchor(Item item)
    {
        item.Center = Landing - Vector2.UnitY * 30f;
        item.velocity = Vector2.Zero;
    }

    public override void GrabRange(Item item, Player player, ref int grabRange)
    {
        if (ArrivalId > 0)
            grabRange = 24;
    }

    public override bool GrabStyle(Item item, Player player)
    {
        if (ArrivalId <= 0)
            return false;
        Anchor(item);
        return true;
    }

    public override bool CanPickup(Item item, Player player) =>
        ArrivalId <= 0 || age >= TeacherArrivalRules.LandingAge + TeacherArrivalRules.HighlightHold &&
            player.ItemSpace(item).CanTakeItemToPersonalInventory;

    public override bool OnPickup(Item item, Player player)
    {
        if (ArrivalId > 0)
        {
            TeacherArrivalSystem.NotifyPickup(ArrivalId, item.whoAmI);
            ArrivalId = 0;
        }
        return true;
    }

    public override void UpdateInventory(Item item, Player player) => ArrivalId = 0;

    public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor,
        Color alphaColor, ref float rotation, ref float scale, int whoAmI)
    {
        if (ArrivalId <= 0)
            return true;
        TeacherArrivalVisuals.DrawPlanted(Landing, age, 1f);
        return false;
    }
}
