using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FranMod.Content.Companions;

/// <summary>Descendants retain their summon identity, separately from engine owner 255.</summary>
public sealed class FranCompanionProjectile : GlobalProjectile
{
    public override bool InstancePerEntity => true;
    internal int SummonId { get; private set; }
    internal int LogicalOwner { get; private set; } = -1;
    internal bool Linked => SummonId > 0;
    private int missingParentTicks;

    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        if (projectile.ModProjectile?.Mod != Mod || projectile.ModProjectile is FranCompanion)
            return;
        FranCompanion companion = null;
        if (source is EntitySource_Parent parent && parent.Entity is Projectile parentProjectile)
        {
            parentProjectile.TryGetGlobalProjectile(out FranCompanionProjectile parentTag);
            companion = parentProjectile.ModProjectile as FranCompanion ??
                FranCompanionSystem.Find(parentTag?.SummonId ?? 0);
            if (parentTag is { Linked: true } && companion is null)
            {
                SummonId = parentTag.SummonId;
                LogicalOwner = parentTag.LogicalOwner;
                projectile.active = false;
                return;
            }
        }
        companion ??= FranCompanionContext.Current;
        if (companion is null || projectile.owner != FranCompanionContext.VirtualSlot) return;
        SummonId = companion.SummonId;
        LogicalOwner = companion.PlayerOwner;
        projectile.DamageType = DamageClass.Summon;
        projectile.hostile = false;
        projectile.netImportant = true;
        projectile.netUpdate = true;
        if (companion.Retiring) projectile.active = false;
    }

    public override bool PreAI(Projectile projectile)
    {
        if (!Linked) return true;
        if (FranCompanionSystem.Find(SummonId) is not null) { missingParentTicks = 0; return true; }
        // Network ordering: a child packet can precede the parent. Freeze without doing damage.
        if (Main.netMode != NetmodeID.MultiplayerClient || ++missingParentTicks > 90)
            projectile.Kill();
        else projectile.timeLeft = System.Math.Max(projectile.timeLeft, 2);
        return false;
    }

    public override bool? CanDamage(Projectile projectile) => Linked &&
        (Main.netMode == NetmodeID.MultiplayerClient || FranCompanionSystem.Find(SummonId) is null) ? false : null;
    public override bool CanHitPlayer(Projectile projectile, Player target) => !Linked;
    public override bool CanHitPvp(Projectile projectile, Player target) => !Linked;
    public override bool? CanCutTiles(Projectile projectile) => Linked ? false : null;

    public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        => CreditOwner(projectile, target);

    public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        => CreditOwner(projectile, target); // Must precede StrikeNPC, so lethal hits grant boss loot.

    private void CreditOwner(Projectile projectile, NPC target)
    {
        if (!Linked || Main.netMode == NetmodeID.MultiplayerClient || LogicalOwner < 0 || LogicalOwner >= Main.maxPlayers) return;
        target.lastInteraction = LogicalOwner;
        target.playerInteraction[LogicalOwner] = true;
        if (target.realLife >= 0 && target.realLife < Main.maxNPCs)
        {
            Main.npc[target.realLife].lastInteraction = LogicalOwner;
            Main.npc[target.realLife].playerInteraction[LogicalOwner] = true;
        }
    }

    public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter writer)
    {
        bitWriter.WriteBit(Linked);
        if (Linked) { writer.Write(SummonId); writer.Write((byte)LogicalOwner); }
    }

    public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader reader)
    {
        if (!bitReader.ReadBit()) return;
        int id = reader.ReadInt32(); int owner = reader.ReadByte();
        // Clients never get to declare their own projectiles server-owned companion attacks.
        if (Main.netMode == NetmodeID.Server || id <= 0 || owner >= Main.maxPlayers ||
            projectile.owner != FranCompanionContext.VirtualSlot || projectile.ModProjectile?.Mod != Mod) return;
        SummonId = id;
        LogicalOwner = owner;
        projectile.DamageType = DamageClass.Summon;
        projectile.hostile = false;
    }
}
