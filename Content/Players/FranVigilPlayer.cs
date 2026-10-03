using System;
using FranMod.Content.Items;
using FranMod.Content.Projectiles;
using FranMod.Content.Projectiles.Ultimate;
using FranMod.Core;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Players;

// Server-owned stance. Not saved: death / leaving a world ends it, item switching only pauses attacks.
public sealed class FranVigilPlayer : ModPlayer
{
    public bool Active { get; private set; }
    internal int Age { get; private set; }
    internal float Intensity => MathHelper.SmoothStep(.25f, 1f, Math.Min(Age / 180f, 1f));
    private int meteorTimer;

    internal void SetActive(bool active)
    {
        ReceiveState(active, 0);
        if (Main.netMode == NetmodeID.Server) FranModMain.SendVigilState(Player.whoAmI, active, Age);
    }

    internal void ReceiveState(bool active, int age)
    {
        bool changed = Active != active;
        Active = active;
        Age = Math.Max(0, age);
        if (!changed) return;
        meteorTimer = 0;
        FranPlayer fran = Player.GetModPlayer<FranPlayer>();
        fran.ComboIndex = fran.ComboReset = 0;
        Player.itemAnimation = Player.itemTime = 0;
        Player.heldProj = -1;
        foreach (Projectile p in Main.ActiveProjectiles)
            if (p.owner == Player.whoAmI && (active ? FranVigilAttackGate.IsReplaced(p) :
                p.ModProjectile is FranVigilSwing or FranVigilWave or FranVigilMeteor))
            {
                // Do not run old OnKill combat payloads when replacing the moveset.
                p.active = false;
                if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.KillProjectile, -1, -1, null, p.identity, p.owner);
            }
        if (!Main.dedServ)
        {
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = .65f, Pitch = active ? -.25f : .15f }, Player.Center);
            FranParticleSystem.Spawn(new FranDirectionalRingParticle(Player.Center, Vector2.Zero,
                new Color(115, 205, 255), Vector2.One * .1f, Vector2.One * .8f, 0f, 22), false);
        }
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        if (Main.netMode == NetmodeID.Server) FranModMain.SendVigilState(Player.whoAmI, Active, Age, toWho, fromWho);
    }

    public override void UpdateDead()
    {
        if (Active && Main.netMode != NetmodeID.MultiplayerClient) SetActive(false);
    }

    public override void OnEnterWorld() { Active = false; Age = meteorTimer = 0; }

    public override void PostUpdate()
    {
        if (!Active) return;
        if (Player.dead || !Main.hardMode || !Player.GetModPlayer<FranPlayer>().HasShiFu)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient) SetActive(false);
            return;
        }
        Age = Math.Min(Age + 1, 3600);
        if (Player.HeldItem.type != ModContent.ItemType<ShiFu>()) { meteorTimer = 0; return; }
        if (Main.netMode == NetmodeID.MultiplayerClient || ++meteorTimer < FranVigilRules.MeteorInterval) return;
        meteorTimer = 0;
        Vector2 target = Player.Center + new Vector2(Main.rand.NextFloat(-480, 480), 140);
        float nearest = 1000f * 1000f;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (!npc.CanBeChasedBy()) continue;
            float distance = Vector2.DistanceSquared(Player.Center, npc.Center);
            if (distance >= nearest) continue;
            nearest = distance;
            target = npc.Center + npc.velocity * 12f;
        }
        Vector2 spawn = target + new Vector2(Main.rand.NextFloat(-360, 360), -850);
        spawn.Y = Math.Max(80, spawn.Y);
        Projectile.NewProjectile(Player.GetSource_Misc("VigilMeteor"), spawn,
            (target - spawn).SafeNormalize(Vector2.UnitY) * 20f, ModContent.ProjectileType<FranVigilMeteor>(),
            Math.Max(1, (int)(Player.GetWeaponDamage(Player.HeldItem) * FranVigilRules.MeteorDamageMultiplier)),
            4f, Player.whoAmI, target.Y);
    }
}

// Existing projectiles carry tactic snapshots. Prevent them from leaking their AI into the new form.
public sealed class FranVigilAttackGate : GlobalProjectile
{
    internal static bool IsReplaced(Projectile p) => p.ModProjectile is
        ShiFuSwingRebuild or FranQuickdrawDiscHoldout or FranEvolutionEchoSlash or
        Telekinetic_CondensedMindCore or Telekinetic_DestructiveWave or Telekinetic_Flash or
        Telekinetic_GravityStarfall or Telekinetic_GravityStarfallExplosion or
        TelekineticCannonProjectile or ShiFuCrescentWave or ShiFuStarlightMass or ShiFuCompressionWave or
        FranTelekineticReturnBlade or FranCoreImpactBurst or FranBlueResonanceEcho or
        FranShiFuPhantomBlade or FranReplaySlash or FranPhantomMatrixImpact or
        FranCompressedCut or FranCompressionNode or FranCompressionAirburst or FranPressurizedTerraWave or
        FranAutoBarrierProjectile or MagicBarrierProjectile or FranBarrierCounterCut or
        SkillTakerPhantom or SkillTakerSoul or FranSkillTakerWisp or FranSkillSealDetonation or
        FranThunderStrikeProjectile or FranThunderBudgetStrike or FranThunderImpactExplosion or
        FranThunderCircuitNode or FranThunderCircuitLink or FranThunderCircuitFinisher or FranLightningBranches or
        FranFireScar or FranWindField or FlameFangAuraVisual or
        FranHammerBeacon or FranThorsHammerDrop or ThorsHammerMarker or ThorsHammerBolt or FranRicochetLightning or
        FloatingSwordArrayProjectile or FloatingSwordLaser or AirDrawSlashProjectile or LightningSpearProjectile or ShipCutterProjectile;

    public override bool PreAI(Projectile projectile)
    {
        if (projectile.owner >= 255 || !IsReplaced(projectile) ||
            !Main.player[projectile.owner].GetModPlayer<FranVigilPlayer>().Active) return true;
        projectile.active = false;
        return false;
    }

    public override bool? CanDamage(Projectile projectile) => projectile.owner < 255 && IsReplaced(projectile) &&
        Main.player[projectile.owner].GetModPlayer<FranVigilPlayer>().Active ? false : null;
}
