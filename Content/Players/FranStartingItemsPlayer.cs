using System.Collections.Generic;
using FranMod.Content.Items;
using Terraria;
using Terraria.ModLoader;

namespace FranMod.Content.Players;

public sealed class FranStartingItemsPlayer : ModPlayer
{
    public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
    {
        if (!mediumCoreDeath)
            yield return new Item(ModContent.ItemType<Foreword>());
    }
}
