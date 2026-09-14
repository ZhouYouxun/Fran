using System;
using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace FranMod.Content.Companions;

/// <summary>
/// Reference: ModSources/MoreAIPlayer/Companion/VirtualPlayerExecutionContext.cs.
/// Only the engine's reserved projectile-owner slot (255) is borrowed, never a real player's
/// slot. The detached Player supplies ShiFu's pose/stats while the networked summon owns life.
/// Scopes are nested and restored even when a projectile/mod hook throws. No Player.Update,
/// ItemCheck, inventory UI, equipment effects, or player-network packets run on this actor.
/// </summary>
internal static class FranCompanionContext
{
    internal const int VirtualSlot = 255;
    internal static FranCompanion Current { get; private set; }
    internal static Vector2 Aim(Player player) => IsActor(player) ? Current.AimWorld : Main.MouseWorld;
    internal static bool IsActor(Player player) => Current is not null && ReferenceEquals(Current.Actor, player);

    internal static bool Owns(Player actor, Projectile projectile) =>
        projectile.owner == actor.whoAmI && (!IsActor(actor) ||
            projectile.GetGlobalProjectile<FranCompanionProjectile>().SummonId == Current.SummonId);

    internal readonly struct Scope : IDisposable
    {
        private readonly Player previousPlayer;
        private readonly int previousLocal;
        private readonly FranCompanion previousContext;

        internal Scope(FranCompanion companion, bool simulation)
        {
            previousPlayer = Main.player[VirtualSlot];
            previousLocal = Main.myPlayer;
            previousContext = Current;
            Main.player[VirtualSlot] = companion.Actor;
            Current = companion;
            // Multiplayer clients must NEVER take authority. Server-owned projectiles already
            // use 255 on a server. Singleplayer needs the same local-only execution gate.
            if (simulation && Main.netMode == NetmodeID.SinglePlayer)
                Main.myPlayer = VirtualSlot;
        }

        public void Dispose()
        {
            Main.player[VirtualSlot] = previousPlayer;
            Main.myPlayer = previousLocal;
            Current = previousContext;
        }
    }
}
