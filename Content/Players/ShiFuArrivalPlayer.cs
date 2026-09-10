using System;
using FranMod.Content.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FranMod.Content.Players;

public sealed class ShiFuArrivalPlayer : ModPlayer
{
    internal Guid CharacterId { get; private set; }
    internal bool OpportunitySpent { get; private set; }
    internal bool ProfileReady { get; private set; }
    internal int EntryAge;
    private bool profileAcknowledged;
    private int profileRetry;
    private int readinessTimer;
    internal bool PresentationReady;
    internal ulong LastReadinessTick;

    public override void Initialize() => CharacterId = Guid.NewGuid();

    public override void SaveData(TagCompound tag)
    {
        tag["ArrivalCharacterId"] = CharacterId.ToString("N");
        tag["ArrivalOpportunitySpent"] = OpportunitySpent;
    }

    public override void LoadData(TagCompound tag)
    {
        if (Guid.TryParse(tag.GetString("ArrivalCharacterId"), out Guid id) && id != Guid.Empty)
            CharacterId = id;
        OpportunitySpent = tag.GetBool("ArrivalOpportunitySpent");
    }

    public override void OnEnterWorld()
    {
        EntryAge = 0;
        profileRetry = 0;
        if (Player.whoAmI != Main.myPlayer)
            return;
        ProfileReady = Main.netMode == NetmodeID.SinglePlayer;
        profileAcknowledged = ProfileReady;
        if (!ProfileReady)
            ShiFuArrivalSystem.SendProfile(this);
    }

    public override void PostUpdate()
    {
        if (Player.whoAmI != Main.myPlayer)
            return;
        bool ready = Main.hasFocus && !Main.gameMenu && !Main.playerInventory && !Main.mapFullscreen &&
            !Player.dead && !Player.GetModPlayer<FranPlayer>().UltimateActive;
        if (Main.netMode == NetmodeID.SinglePlayer)
            PresentationReady = ready;
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            if (!profileAcknowledged && ++profileRetry >= 180)
            {
                profileRetry = 0;
                ShiFuArrivalSystem.SendProfile(this);
            }
            if (!OpportunitySpent && (++readinessTimer >= 60 || ready != PresentationReady))
            {
                readinessTimer = 0;
                PresentationReady = ready;
                ShiFuArrivalSystem.SendReadiness(ready);
            }
        }
    }

    internal bool AcceptProfile(Guid id, bool spent)
    {
        if (id == Guid.Empty || ProfileReady && CharacterId != id)
            return false;
        CharacterId = id;
        // A retry cannot reopen a chance that the server has already consumed.
        OpportunitySpent |= spent;
        ProfileReady = true;
        return true;
    }

    internal void Acknowledge(Guid id, bool spent)
    {
        if (id != CharacterId)
            return;
        OpportunitySpent |= spent;
        profileAcknowledged = true;
    }

    internal void Spend() => OpportunitySpent = true;
}
