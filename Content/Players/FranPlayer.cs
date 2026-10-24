using System;
using System.Collections.Generic;
using System.Reflection;
using FranMod.Content.Buffs;
using FranMod.Content.Companions;
using FranMod.Content.Config;
using FranMod.Content.Items;
using FranMod.Content.Projectiles;
using FranMod.Content.Projectiles.BlackHole;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace FranMod.Content.Players;

public sealed class FranPlayer : ModPlayer
{
    public static int DashDuration =>
        FranBalance.GetInt(nameof(FranBalance.DashDurationFrames), FranBalance.DashDurationFrames);
    public static int UltimateChargeDuration =>
        FranBalance.GetInt(nameof(FranBalance.UltimateChargeDurationFrames), FranBalance.UltimateChargeDurationFrames);
    // 存档与联机协议仍保留四格技术容量；实际可用格数完全由 FranBalance 的槽位表决定。
    public static int MaximumTacticSlots => FranBalance.TacticStorageCapacity;
    public static readonly Vector2 DefaultUltimateGaugeAnchor = new(0.5f, 0f);

    public bool FranMode { get; private set; }
    private readonly FranSkill[] equippedTactics = new FranSkill[FranBalance.TacticStorageCapacity];
    public int UltimateChargeTicks;
    public FranUltimateKind SelectedUltimate { get; private set; }
    public Vector2 UltimateGaugeAnchor = DefaultUltimateGaugeAnchor;
    public int DashTimer;
    public int DashCooldown;
    public Vector2 DashVelocity;
    public int ComboIndex;
    public int ComboReset;
    public int BlueResonanceTimer;
    public int StolenSkillTimer;
    public int StolenContactDamage;
    public int StolenContactDamageTimer;
    public int HammerComboRounds;
    private int telekineticPlanetFinisherRounds;
    public readonly int[] SkillCooldowns = new int[FranSkillInfo.Count];
    public readonly int[] SkillCooldownMaximums = new int[FranSkillInfo.Count];
    public bool TelekineticFlightEnabled { get; private set; }
    public bool TelekineticFlightVisualActive { get; private set; }

    private readonly HashSet<int> dashHitNpcs = new();
    private static readonly Dictionary<Type, PropertyInfo> ExternalDashIdProperties = new();
    private bool introShown;
    private bool receivedStartingShiFu;
    private bool wasFranMode;
    private bool networkFranMode;
    private bool shiFuBonded;
    private int restoreShiFuAfterRespawnCount;
    private bool dashStartedThisTick;
    private float screenShakePower;
    private Vector2 ultimateCameraFocus;
    private int ultimateCameraLockTime;
    private int ultimateChargeSyncTimer;
    private bool ultimateReadyAnnounced;
    private bool telekineticFlightSession;
    private bool hasWorldProgressContext;
    private string worldProgressContext;

    public bool HasShiFu
    {
        get
        {
            int type = ModContent.ItemType<ShiFu>();
            return Player.HasItem(type);
        }
    }
    public int AvailableTacticSlots => FranBalance.GetAvailableTacticSlots();
    public FranSkill SelectedSkill => GetEquippedTactic(0);
    public bool HasSelectedSkill => GetActiveTacticCount() > 0;
    // dashDelay is runtime state, not proof that another dash is equipped. Some external
    // dash frameworks may touch it before Fran reads input this frame.
    public bool HasExternalDash => Player.dashType != 0 || HasNamedExternalDash();
    public bool HasExternalWings => Player.wingsLogic > 0 &&
        !FranTelekineticFlightSystem.IsTelekineticWingSlot(Player.wingsLogic);
    public bool UltimateUnlocked => FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh);
    public int SelectedUltimateChargeDuration => SelectedUltimate == FranUltimateKind.Vigil ? FranVigilRules.ChargeFrames : UltimateChargeDuration;
    public bool VigilActive => Player.GetModPlayer<FranVigilPlayer>().Active;
    public bool UltimateReady => UltimateUnlocked && UltimateChargeTicks >= SelectedUltimateChargeDuration;
    public bool ExecutionUltimateActive => Player.ownedProjectileCounts[ModContent.ProjectileType<FranUltimateController>()] > 0;
    internal FranSingularityUltimate ActiveSingularity => FranSingularityUltimate.Find(Player.whoAmI);
    public bool BlockingUltimateActive => ExecutionUltimateActive || ActiveSingularity is not null;
    public bool UltimateActive => BlockingUltimateActive || VigilActive;
    public bool HoldingShiFu => Player.HeldItem.type == ModContent.ItemType<ShiFu>();

    private bool HasNamedExternalDash()
    {
        // Some dash frameworks expose their selected dash as DashID while keeping Player.dashType
        // at zero. Read that convention without taking a hard dependency.
        foreach (ModPlayer modPlayer in Player.ModPlayers)
        {
            if (ReferenceEquals(modPlayer, this))
                continue;
            Type type = modPlayer.GetType();
            if (!ExternalDashIdProperties.TryGetValue(type, out PropertyInfo property))
            {
                property = type.GetProperty("DashID", BindingFlags.Instance | BindingFlags.Public);
                ExternalDashIdProperties[type] = property;
            }
            if (property?.PropertyType != typeof(string))
                continue;
            try
            {
                if (property.GetValue(modPlayer) is string id && !string.IsNullOrWhiteSpace(id) &&
                    !IsNonBlockingFrameworkDefaultDash(type, id))
                    return true;
            }
            catch
            {
                // A third-party compatibility probe must never break Fran's own movement update.
            }
        }
        return false;
    }

    private static bool IsNonBlockingFrameworkDefaultDash(Type modPlayerType, string dashId)
    {
        // One common fallback dash can be reported even when no dash accessory is equipped.
        // Fran replaces only this fallback while Fran mode is active; real accessory dashes
        // still win through their distinct DashID or Player.dashType.
        return modPlayerType.FullName == "CalamityMod.CalPlayer.CalamityPlayer" &&
            dashId.EndsWith("/DefaultDash", StringComparison.OrdinalIgnoreCase);
    }

    public override void Initialize()
    {
        ClearTacticArray();
    }

    public override void SaveData(TagCompound tag)
    {
        List<int> savedTactics = new(MaximumTacticSlots);
        for (int i = 0; i < MaximumTacticSlots; i++)
            savedTactics.Add(FranSkillInfo.IsRealSkill(equippedTactics[i]) ? (int)equippedTactics[i] : -1);
        tag["EquippedTacticsV1"] = savedTactics;
        tag["SelectedUltimateV1"] = (int)SelectedUltimate;
        if (UltimateChargeTicks > 0)
            tag["UltimateChargeTicks"] = Math.Clamp(UltimateChargeTicks, 0, SelectedUltimateChargeDuration);
        if (Vector2.DistanceSquared(UltimateGaugeAnchor, DefaultUltimateGaugeAnchor) > 0.000001f)
        {
            tag["UltimateGaugeX"] = UltimateGaugeAnchor.X;
            tag["UltimateGaugeY"] = UltimateGaugeAnchor.Y;
        }
        if (introShown)
            tag["IntroShown"] = true;
        if (shiFuBonded)
            tag["ShiFuBonded"] = true;
        if (restoreShiFuAfterRespawnCount > 0)
            tag["ShiFuRespawnCount"] = restoreShiFuAfterRespawnCount;
        if (receivedStartingShiFu)
            tag["ReceivedStartingShiFu"] = true;
    }

    public override void LoadData(TagCompound tag)
    {
        ClearTacticArray();
        if (tag.ContainsKey("EquippedTacticsV1"))
        {
            IList<int> saved = tag.GetList<int>("EquippedTacticsV1");
            for (int i = 0; i < Math.Min(MaximumTacticSlots, saved.Count); i++)
                equippedTactics[i] = saved[i] >= 0 && saved[i] < FranSkillInfo.Count && FranSkillInfo.IsRealSkill((FranSkill)saved[i])
                    ? (FranSkill)saved[i]
                    : FranSkill.None;
        }
        else
        {
            int value;
            if (tag.ContainsKey("SelectedSkillV2"))
                value = tag.GetInt("SelectedSkillV2");
            else
            {
                int legacy = tag.ContainsKey("SelectedSkill") ? tag.GetInt("SelectedSkill") : -1;
                value = legacy switch
                {
                    5 => -1,
                    6 => (int)FranSkill.ThunderChain,
                    7 => (int)FranSkill.PressurizedQuickdraw,
                    8 => (int)FranSkill.ThorsHammer,
                    _ => legacy
                };
            }
            if (value >= 0 && value < FranSkillInfo.Count && FranSkillInfo.IsRealSkill((FranSkill)value))
                equippedTactics[0] = (FranSkill)value;
        }
        UltimateChargeTicks = Math.Clamp(tag.GetInt("UltimateChargeTicks"), 0, UltimateChargeDuration);
        ReceiveUltimateSelection((FranUltimateKind)tag.GetInt("SelectedUltimateV1"));
        UltimateChargeTicks = Math.Min(UltimateChargeTicks, SelectedUltimateChargeDuration);
        float gaugeX = tag.ContainsKey("UltimateGaugeX") ? tag.GetFloat("UltimateGaugeX") : DefaultUltimateGaugeAnchor.X;
        float gaugeY = tag.ContainsKey("UltimateGaugeY") ? tag.GetFloat("UltimateGaugeY") : DefaultUltimateGaugeAnchor.Y;
        UltimateGaugeAnchor = new Vector2(MathHelper.Clamp(gaugeX, 0f, 1f),
            MathHelper.Clamp(gaugeY, 0f, 1f));
        ultimateReadyAnnounced = UltimateReady;
        introShown = tag.GetBool("IntroShown");
        shiFuBonded = tag.GetBool("ShiFuBonded");
        restoreShiFuAfterRespawnCount = Math.Clamp(tag.GetInt("ShiFuRespawnCount"), 0, 59);
        receivedStartingShiFu = tag.GetBool("ReceivedStartingShiFu");
    }

    public override void OnEnterWorld()
    {
        telekineticPlanetFinisherRounds = 0;
        if (Player.whoAmI != Main.myPlayer || receivedStartingShiFu)
            return;

        receivedStartingShiFu = true;
        Player.QuickSpawnItem(Player.GetSource_Misc("FranStartingShiFu"), ModContent.ItemType<ShiFu>());
    }

    public override void ResetEffects()
    {
        TelekineticFlightEnabled = false;
        bool localAuthority = Main.netMode == NetmodeID.SinglePlayer || Player.whoAmI == Main.myPlayer;
        // ShiFu in the inventory is the sole activation route; remote players use synced state.
        FranMode = localAuthority ? HasShiFu : networkFranMode;
        if (!FranMode)
            return;

        Player.AddBuff(ModContent.BuffType<BlackCatWarriorBuff>(), 2);
    }

    public override void PostUpdateEquips()
    {
        if (!FranMode)
            return;

        int barrierLayers = GetEquippedMagicBarrierLayers();
        if (barrierLayers > 0)
        {
            int defense = FranBalance.GetInt(nameof(FranBalance.MagicBarrierBaseDefense),
                FranBalance.MagicBarrierBaseDefense) +
                FranBalance.GetInt(nameof(FranBalance.MagicBarrierExtraLayerDefense),
                    FranBalance.MagicBarrierExtraLayerDefense) * (barrierLayers - 1);
            Player.statDefense += Math.Max(0, defense);
            bool barrierActive = FranAutoBarrierProjectile.IsActive(Player);
            if (barrierActive)
                Player.statDefense *= FranBalance.GetFloat(nameof(FranBalance.MagicBarrierActiveDefenseMultiplier),
                    FranBalance.MagicBarrierActiveDefenseMultiplier);
            if (barrierActive && IsFullTacticLoadout(FranSkill.MagicBarrier))
                Player.lifeRegen += Math.Max(0, FranBalance.GetInt(
                    nameof(FranBalance.MagicBarrierFullLoadoutLifeRegen),
                    FranBalance.MagicBarrierFullLoadoutLifeRegen));
        }

        if (!FranBalance.IsMilestoneCompleted(FranProgressionMilestone.WallOfFlesh) ||
            Player.mount.Active || HasExternalWings)
            return;

        int wingSlot = FranTelekineticFlightSystem.GetCurrentWingSlot();
        if (wingSlot <= 0)
            return;

        WingStats stats = FranTelekineticFlightSystem.GetCurrentWingStats();
        Player.wingsLogic = wingSlot;
        Player.wingTimeMax = Math.Max(Player.wingTimeMax, stats.FlyTime);
        TelekineticFlightEnabled = true;

        // Empress of Light is the final mastery breakpoint. Refilling before movement makes the
        // built-in wing logic infinite without granting that benefit to an equipped external wing.
        if (FranTelekineticFlightSystem.HasInfiniteFlight)
            Player.wingTime = Math.Max(Player.wingTime, Player.wingTimeMax);
    }

    public override void FrameEffects() => ApplyFranAppearance();

    internal void ApplyFranAppearance()
    {
        if (FranCompanionContext.IsActor(Player))
        {
            FranCompanionAppearance.Apply(Player);
            return;
        }
        if (!FranMode)
            return;

        FranCompanionAppearance.ApplyPlayerAppearance(Player);
    }

    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        if (FranCompanionContext.IsActor(Player))
            return;
        if (!FranMode)
            return;

        // A readable Fran silhouette made entirely from vanilla player layers.
        Color environmentLight = Lighting.GetColor((int)(Player.Center.X / 16f), (int)(Player.Center.Y / 16f));
        Color skin = LitColor(new Color(244, 215, 198), environmentLight);
        drawInfo.colorHair = LitColor(new Color(36, 34, 49), environmentLight);
        drawInfo.colorEyes = LitColor(new Color(246, 202, 74), environmentLight);
        drawInfo.colorHead = skin;
        drawInfo.colorBodySkin = skin;
        drawInfo.colorLegs = skin;
        drawInfo.colorShirt = LitColor(new Color(42, 43, 58), environmentLight);
        drawInfo.colorUnderShirt = LitColor(new Color(98, 78, 122), environmentLight);
        drawInfo.colorPants = LitColor(new Color(30, 31, 43), environmentLight);
        drawInfo.colorShoes = LitColor(new Color(22, 22, 30), environmentLight);
    }

    public override void ModifyScreenPosition()
    {
        if (Player.whoAmI != Main.myPlayer)
            return;

        // During the execution sequence the target owns the composition.  The player is
        // deliberately allowed to cross the frame instead of dragging the camera with every dash.
        if (ultimateCameraLockTime > 0)
        {
            Main.screenPosition = ultimateCameraFocus - new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            ultimateCameraLockTime--;
        }

        if (screenShakePower > 0.05f)
        {
            Main.screenPosition += Main.rand.NextVector2Circular(screenShakePower, screenShakePower);
            screenShakePower *= 0.82f;
        }
        else
            screenShakePower = 0f;

        Main.screenPosition.X = MathHelper.Clamp(Main.screenPosition.X, 0f,
            Math.Max(0f, Main.maxTilesX * 16f - Main.screenWidth));
        Main.screenPosition.Y = MathHelper.Clamp(Main.screenPosition.Y, 0f,
            Math.Max(0f, Main.maxTilesY * 16f - Main.screenHeight));
    }

    public override void CopyClientState(ModPlayer targetCopy)
    {
        FranPlayer copy = (FranPlayer)targetCopy;
        copy.FranMode = FranMode;
        copy.SelectedUltimate = SelectedUltimate;
        Array.Copy(equippedTactics, copy.equippedTactics, MaximumTacticSlots);
    }

    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        FranPlayer copy = (FranPlayer)clientPlayer;
        if (copy.FranMode != FranMode)
            FranModMain.SendFranMode(Player.whoAmI, FranMode);
        if (copy.SelectedUltimate != SelectedUltimate)
            FranModMain.SendUltimateSelection(Player.whoAmI, SelectedUltimate);
        if (!TacticLoadoutEquals(copy.equippedTactics))
            FranModMain.SendTacticLoadout(Player.whoAmI, equippedTactics);
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        FranModMain.SendFranMode(Player.whoAmI, FranMode, toWho, fromWho);
        FranModMain.SendCombatState(Player.whoAmI, DashTimer, DashVelocity, toWho, fromWho);
        FranModMain.SendTacticLoadout(Player.whoAmI, equippedTactics, toWho, fromWho);
        FranModMain.SendUltimateSelection(Player.whoAmI, SelectedUltimate, toWho, fromWho);
        if (Main.netMode == NetmodeID.Server)
            FranModMain.SendUltimateCharge(Player.whoAmI, UltimateChargeTicks, toWho, fromWho);
    }

    internal void ReceiveNetworkMode(bool active) => networkFranMode = active;

    internal void ReceiveUltimateSelection(FranUltimateKind kind)
    {
        SelectedUltimate = FranUltimateInfo.IsSelectable(kind) ? kind : FranUltimateKind.BlackHeavenlyTiger;
        // Preserve elapsed charge, never convert a five-second charge into a full execution.
        UltimateChargeTicks = Math.Min(UltimateChargeTicks, SelectedUltimateChargeDuration);
        ultimateReadyAnnounced = UltimateReady;
    }

    internal bool TrySelectUltimate(FranUltimateKind kind)
    {
        if (!UltimateUnlocked || UltimateActive || !FranUltimateInfo.IsSelectable(kind) || SelectedUltimate == kind)
            return false;
        ReceiveUltimateSelection(kind);
        FranModMain.SendUltimateSelection(Player.whoAmI, kind);
        return true;
    }

    internal void ReceiveNetworkCombatState(int dashTimer, Vector2 dashVelocity)
    {
        DashTimer = dashTimer;
        DashVelocity = dashVelocity;
        dashStartedThisTick = dashTimer > 0;
        dashHitNpcs.Clear();
    }

    internal void ReceiveUltimateCharge(int chargeTicks)
    {
        bool wasReady = UltimateReady;
        UltimateChargeTicks = UltimateUnlocked ? Math.Clamp(chargeTicks, 0, SelectedUltimateChargeDuration) : 0;
        if (!wasReady && UltimateReady && Player.whoAmI == Main.myPlayer)
            SoundEngine.PlaySound(FranCombatSounds.大招蓄势完成, Player.Center);
        ultimateReadyAnnounced = UltimateReady;
    }

    internal void ReceiveTacticLoadout(ReadOnlySpan<FranSkill> tactics)
    {
        ClearTacticArray();
        int count = Math.Min(MaximumTacticSlots, tactics.Length);
        for (int i = 0; i < count; i++)
        {
            FranSkill skill = tactics[i];
            if (FranSkillInfo.IsRealSkill(skill) && FranSkillInfo.IsUnlocked(skill))
                equippedTactics[i] = skill;
        }
    }

    public override void PreUpdate() => MaintainUltimateHeldItem();

    private void MaintainUltimateHeldItem()
    {
        ActiveSingularity?.LockHeldWeapon(Player);
        if (!UltimateActive)
            return;
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.owner == Player.whoAmI && projectile.ModProjectile is FranUltimateController controller)
            {
                controller.LockHeldWeapon(Player);
                break;
            }
    }

    // Detached companion actors deliberately skip PostUpdate (input, tool grants, camera,
    // evolution and player packets). Only their independent combat clock/loadout is advanced.
    internal void TickCompanionCombat(FranSkill tactic, bool tickTimers = true)
    {
        FranMode = true;
        int count = Math.Min(AvailableTacticSlots, MaximumTacticSlots);
        bool unlocked = FranSkillInfo.IsRealSkill(tactic) && FranSkillInfo.IsUnlocked(tactic);
        for (int i = 0; i < equippedTactics.Length; i++)
            equippedTactics[i] = unlocked && i < count ? tactic : FranSkill.None;
        if (tickTimers)
            UpdateTimers();
    }

    public override void PostUpdate()
    {
        RefreshWorldProgressContext();
        if (!UltimateUnlocked)
        {
            UltimateChargeTicks = 0;
            ultimateChargeSyncTimer = 0;
            ultimateReadyAnnounced = false;
        }

        if (Player.dead)
        {
            TelekineticFlightVisualActive = false;
            telekineticFlightSession = false;
            Player.fullRotation = 0f;
            return;
        }

        if (HasShiFu)
            shiFuBonded = true;
        RemoveRetiredShiFuExcavationTools();

        if (!FranMode)
        {
            wasFranMode = false;
            TelekineticFlightVisualActive = false;
            telekineticFlightSession = false;
            if (DashTimer > 0 || DashVelocity != Vector2.Zero)
                EndDash(sync: Player.whoAmI == Main.myPlayer);
            if (HasShiFu)
            {
                RemoveLockedTactics();
                UpdateTimers();
                MaintainSelectedImprint();
                EnsureFloatingShiFu();
            }
            return;
        }

        if (!wasFranMode)
        {
            wasFranMode = true;
            ShowIntroduction();
        }

        RemoveLockedTactics();
        UpdateTimers();
        MaintainSelectedImprint();
        UpdateUltimateCharge();
        UpdateInputs();
        UpdateDash();
        UpdateTelekineticFlightVisual();
        EnsureFloatingShiFu();
    }

    private void RefreshWorldProgressContext()
    {
        // All progression reads use the live world flags. Only character-owned transient state
        // needs explicit cleanup when the player changes worlds.
        string currentContext = Main.ActiveWorldFileData?.Path ?? $"{Main.worldID}:{Main.worldName}";
        if (!hasWorldProgressContext)
        {
            hasWorldProgressContext = true;
            worldProgressContext = currentContext;
            return;
        }
        if (string.Equals(worldProgressContext, currentContext, StringComparison.Ordinal))
            return;

        worldProgressContext = currentContext;
        UltimateChargeTicks = 0;
        ultimateChargeSyncTimer = 0;
        ultimateReadyAnnounced = false;
        HammerComboRounds = 0;
        telekineticPlanetFinisherRounds = 0;
        RemoveLockedTactics();
    }

    public override void PreUpdateMovement()
    {
        if (!FranMode || DashTimer <= 0 && !ExecutionUltimateActive)
            return;

        // Fran's dash is an impulse rather than a forced movement rail. Aerial Hunt may provide
        // an eight-direction impulse; without that evolution the original horizontal behavior remains.
        Player.immune = true;
        Player.immuneTime = Math.Max(Player.immuneTime, 2);
        for (int i = 0; i < Player.hurtCooldowns.Length; i++)
            Player.hurtCooldowns[i] = Math.Max(Player.hurtCooldowns[i], 2);
    }

    public override void SetControls()
    {
        if (ActiveSingularity is { } singularity)
        {
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            if (singularity.WeaponLocked)
            {
                Player.controlThrow = false;
                Player.controlInv = false;
                singularity.LockHeldWeapon(Player);
            }
            return;
        }
        if (!BlockingUltimateActive && !Content.UI.FranUISystem.IsWheelOpen)
            return;
        if (Content.UI.FranUISystem.IsWheelOpen && !BlockingUltimateActive)
        {
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            return;
        }
        Player.controlLeft = false;
        Player.controlRight = false;
        Player.controlUp = false;
        Player.controlDown = false;
        Player.controlJump = false;
        Player.controlUseItem = false;
        Player.controlUseTile = false;
        Player.controlHook = false;
        Player.controlMount = false;
        Player.controlThrow = false;
        Player.controlInv = false;
        MaintainUltimateHeldItem();
    }

    public override bool CanUseItem(Item item) => !BlockingUltimateActive && !Content.UI.FranUISystem.IsWheelOpen;

    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        if (!FranMode)
            return;
        int barrierLayers = GetEquippedMagicBarrierLayers();
        if (barrierLayers > 0)
        {
            float damageReduction = FranBalance.GetFloat(nameof(FranBalance.MagicBarrierBaseDamageReduction),
                FranBalance.MagicBarrierBaseDamageReduction) +
                FranBalance.GetFloat(nameof(FranBalance.MagicBarrierExtraLayerDamageReduction),
                    FranBalance.MagicBarrierExtraLayerDamageReduction) * (barrierLayers - 1);
            modifiers.SourceDamage *= 1f - MathHelper.Clamp(damageReduction, 0f, 0.95f);
        }
        if (FranClientConfig.Instance?.EnableFranVoice ?? false)
        {
            modifiers.DisableSound();
            SoundEngine.PlaySound(SoundID.PlayerHit with { Pitch = 0.28f, Volume = 0.72f }, Player.Center);
        }
    }

    public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers)
    {
        int barrierLayers = GetActiveMagicBarrierLayers();
        if (barrierLayers > 0 && IsFullTacticLoadout(FranSkill.MagicBarrier))
            modifiers.FinalDamage *= FranBalance.GetFloat(
                nameof(FranBalance.MagicBarrierFullLoadoutContactDamageMultiplier),
                FranBalance.MagicBarrierFullLoadoutContactDamageMultiplier);
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        if (!FranMode || GetSkillStacks(FranSkill.MagicBarrier) <= 0)
            return;
        FranAutoBarrierProjectile.HandlePlayerHit(Player, info.Damage);
    }

    public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genGore,
        ref PlayerDeathReason damageSource)
    {
        if (Player.whoAmI == Main.myPlayer && Player.difficulty == PlayerDifficultyID.MediumCore && shiFuBonded && HasShiFu)
        {
            int shiFuType = ModContent.ItemType<ShiFu>();
            for (int i = 0; i < Player.inventory.Length; i++)
            {
                if (Player.inventory[i].type != shiFuType)
                    continue;
                restoreShiFuAfterRespawnCount += Player.inventory[i].stack;
                Player.inventory[i].TurnToAir();
            }
            if (Main.mouseItem.type == shiFuType)
            {
                restoreShiFuAfterRespawnCount += Main.mouseItem.stack;
                Main.mouseItem.TurnToAir();
            }
        }

        if (!FranMode)
            return true;

        playSound = false;
        genGore = false;
        if (Player.whoAmI == Main.myPlayer)
            Projectile.NewProjectile(Player.GetSource_FromThis(), Player.Center, Vector2.Zero,
                ModContent.ProjectileType<FranDeathVisual>(), 0, 0f, Player.whoAmI);

        // The controller above owns the readable 25-frame collapse and detonation.
        // Keep only a restrained smoke underlay here so it does not bury the textured death effect.
        for (int i = 0; i < 12; i++)
        {
            Dust dust = Dust.NewDustPerfect(Player.Center, i % 4 == 0 ? DustID.PurpleTorch : DustID.Smoke,
                Main.rand.NextVector2Circular(3.4f, 3.4f), 145, default, Main.rand.NextFloat(0.75f, 1.25f));
            dust.noGravity = true;
        }
        return true;
    }

    public override void OnRespawn()
    {
        DashTimer = 0;
        DashCooldown = 0;
        DashVelocity = Vector2.Zero;
        dashStartedThisTick = false;
        TelekineticFlightVisualActive = false;
        telekineticFlightSession = false;
        Player.fullRotation = 0f;
        ComboIndex = 0;
        ComboReset = 0;
        BlueResonanceTimer = 0;
        StolenSkillTimer = 0;
        StolenContactDamage = 0;
        StolenContactDamageTimer = 0;
        HammerComboRounds = 0;
        screenShakePower = 0f;
        dashHitNpcs.Clear();
        Array.Clear(SkillCooldowns);
        Array.Clear(SkillCooldownMaximums);

        if (Player.whoAmI == Main.myPlayer)
        {
            FranModMain.SendCombatState(Player.whoAmI, 0, Vector2.Zero);
            if (restoreShiFuAfterRespawnCount > 0)
            {
                int count = restoreShiFuAfterRespawnCount;
                restoreShiFuAfterRespawnCount = 0;
                for (int i = 0; i < count; i++)
                    Player.QuickSpawnItem(Player.GetSource_Misc("FranShiFuRespawn"), ModContent.ItemType<ShiFu>());
            }
        }
    }

    public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) =>
        FranCompanionContext.IsActor(Player) || FranMode && (DashTimer > 0 || ExecutionUltimateActive);

    public int ConsumeNextCombo()
    {
        int current = ComboIndex;
        ComboIndex = (ComboIndex + 1) % 4;
        ComboReset = Player.GetModPlayer<FranEvolutionPlayer>().ComboResetDuration;
        return current;
    }

    public bool AdvanceTelekineticPlanetFinisher()
    {
        int interval = Math.Max(1, FranBalance.GetInt(
            nameof(FranBalance.TelekineticFinisherPlanetCycleInterval),
            FranBalance.TelekineticFinisherPlanetCycleInterval));
        telekineticPlanetFinisherRounds++;
        return telekineticPlanetFinisherRounds % interval == 0;
    }

    public FranSkill GetEquippedTactic(int slot)
    {
        if (VigilActive || slot < 0 || slot >= Math.Min(AvailableTacticSlots, MaximumTacticSlots))
            return FranSkill.None;
        return FranSkillInfo.IsRealSkill(equippedTactics[slot]) ? equippedTactics[slot] : FranSkill.None;
    }

    public int GetActiveTacticCount()
    {
        if (VigilActive) return 0;
        int count = 0;
        for (int i = 0; i < Math.Min(AvailableTacticSlots, MaximumTacticSlots); i++)
            if (FranSkillInfo.IsRealSkill(equippedTactics[i]))
                count++;
        return count;
    }

    public int GetSkillStacks(FranSkill skill)
    {
        if (VigilActive || !FranSkillInfo.IsRealSkill(skill))
            return 0;
        int count = 0;
        for (int i = 0; i < Math.Min(AvailableTacticSlots, MaximumTacticSlots); i++)
            if (equippedTactics[i] == skill)
                count++;
        return count;
    }

    public bool IsFullTacticLoadout(FranSkill skill)
    {
        if (!FranSkillInfo.IsRealSkill(skill))
            return false;
        int availableSlots = Math.Min(AvailableTacticSlots, MaximumTacticSlots);
        return availableSlots > 0 && GetActiveTacticCount() == availableSlots &&
            GetSkillStacks(skill) == availableSlots;
    }

    private int GetActiveMagicBarrierLayers() => FranAutoBarrierProjectile.IsActive(Player)
        ? GetEquippedMagicBarrierLayers() : 0;

    private int GetEquippedMagicBarrierLayers()
    {
        if (!FranMode)
            return 0;

        int availableSlots = Math.Min(AvailableTacticSlots, MaximumTacticSlots);
        return Math.Clamp(GetSkillStacks(FranSkill.MagicBarrier), 0, availableSlots);
    }

    public FranSkill[] GetActiveTacticSnapshot()
    {
        FranSkill[] result = new FranSkill[MaximumTacticSlots];
        Array.Fill(result, FranSkill.None);
        for (int i = 0; i < Math.Min(AvailableTacticSlots, MaximumTacticSlots); i++)
            result[i] = GetEquippedTactic(i);
        return result;
    }

    internal FranSkill GetRawTactic(int slot) => slot >= 0 && slot < MaximumTacticSlots
        ? equippedTactics[slot]
        : FranSkill.None;

    public bool TryEquipTactic(FranSkill skill)
    {
        if (!FranSkillInfo.IsRealSkill(skill) || !FranSkillInfo.IsUnlocked(skill))
            return false;
        int slotCount = Math.Min(AvailableTacticSlots, MaximumTacticSlots);
        for (int i = 0; i < slotCount; i++)
        {
            if (FranSkillInfo.IsRealSkill(equippedTactics[i]))
                continue;
            equippedTactics[i] = skill;
            AnnounceTacticChange(skill, equipped: true);
            return true;
        }

        FranSkill removed = equippedTactics[0];
        for (int i = 1; i < slotCount; i++)
            equippedTactics[i - 1] = equippedTactics[i];
        equippedTactics[slotCount - 1] = skill;
        if (removed == FranSkill.ThorsHammer && GetSkillStacks(FranSkill.ThorsHammer) == 0)
            HammerComboRounds = 0;
        AnnounceTacticChange(skill, equipped: true);
        return true;
    }

    public bool TryUnequipTactic(int slot)
    {
        if (slot < 0 || slot >= Math.Min(AvailableTacticSlots, MaximumTacticSlots) ||
            !FranSkillInfo.IsRealSkill(equippedTactics[slot]))
            return false;
        FranSkill removed = equippedTactics[slot];
        for (int i = slot; i < MaximumTacticSlots - 1; i++)
            equippedTactics[i] = equippedTactics[i + 1];
        equippedTactics[^1] = FranSkill.None;
        if (removed == FranSkill.ThorsHammer && GetSkillStacks(FranSkill.ThorsHammer) == 0)
            HammerComboRounds = 0;
        AnnounceTacticChange(removed, equipped: false);
        return true;
    }

    // Kept for old callers and old saves: selecting now means starting a one-tactic loadout.
    public void SelectSkill(FranSkill skill)
    {
        ClearTacticArray();
        if (FranSkillInfo.IsRealSkill(skill) && FranSkillInfo.IsUnlocked(skill))
            equippedTactics[0] = skill;
        AnnounceTacticChange(skill, FranSkillInfo.IsRealSkill(skill));
    }

    private void AnnounceTacticChange(FranSkill skill, bool equipped)
    {
        SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.2f, Volume = 0.65f }, Player.Center);
        if (Main.myPlayer != Player.whoAmI)
            return;
        Color color = FranSkillInfo.IsRealSkill(skill) ? FranSkillInfo.Color(skill) : new Color(190, 205, 215);
        string text = FranSkillInfo.IsRealSkill(skill)
            ? Language.GetTextValue(equipped ? "Mods.Fran.UI.TacticEquipped" : "Mods.Fran.UI.TacticUnequipped",
                FranSkillInfo.Name(skill), GetSkillStacks(skill))
            : Language.GetTextValue("Mods.Fran.UI.PureSwordsmanship");
        CombatText.NewText(Player.Hitbox, color, text);
    }

    private void ClearTacticArray() => Array.Fill(equippedTactics, FranSkill.None);

    private bool TacticLoadoutEquals(FranSkill[] other)
    {
        if (other is null || other.Length < MaximumTacticSlots)
            return false;
        for (int i = 0; i < MaximumTacticSlots; i++)
            if (equippedTactics[i] != other[i])
                return false;
        return true;
    }

    private void RemoveLockedTactics()
    {
        bool removedHammer = false;
        for (int i = 0; i < MaximumTacticSlots; i++)
        {
            FranSkill skill = equippedTactics[i];
            if (!FranSkillInfo.IsRealSkill(skill) || FranSkillInfo.IsUnlocked(skill))
                continue;
            removedHammer |= skill == FranSkill.ThorsHammer;
            equippedTactics[i] = FranSkill.None;
        }
        CompactTactics();
        if (removedHammer && GetSkillStacks(FranSkill.ThorsHammer) == 0)
            HammerComboRounds = 0;
    }

    private void CompactTactics()
    {
        int write = 0;
        for (int read = 0; read < MaximumTacticSlots; read++)
        {
            if (!FranSkillInfo.IsRealSkill(equippedTactics[read]))
                continue;
            equippedTactics[write++] = equippedTactics[read];
        }
        while (write < MaximumTacticSlots)
            equippedTactics[write++] = FranSkill.None;
    }

    public bool IsImprintReady(FranSkill skill) => FranSkillInfo.IsRealSkill(skill) &&
        SkillCooldowns[(int)skill] <= 0;

    public void StartImprintCooldown(FranSkill skill, int ticks = -1)
    {
        if (!FranSkillInfo.IsRealSkill(skill))
            return;
        int cooldown = ticks >= 0 ? ticks : FranSkillInfo.Cooldown(skill);
        SkillCooldowns[(int)skill] = Math.Max(SkillCooldowns[(int)skill], cooldown);
        SkillCooldownMaximums[(int)skill] = Math.Max(SkillCooldownMaximums[(int)skill], cooldown);
    }

    public bool AdvanceHammerRound(int tacticLayers)
    {
        tacticLayers = Math.Clamp(tacticLayers, 1, MaximumTacticSlots);
        bool fullHammerLoadout = tacticLayers == AvailableTacticSlots &&
            IsFullTacticLoadout(FranSkill.ThorsHammer);
        int requiredRounds = fullHammerLoadout ? 1 : FranBalance.GetInt(
            nameof(FranBalance.ThorsHammerRequiredComboRounds), FranBalance.ThorsHammerRequiredComboRounds);
        HammerComboRounds++;
        if (HammerComboRounds < Math.Max(1, requiredRounds))
            return false;
        HammerComboRounds = 0;
        return true;
    }

    public bool ApplyResonanceHealing(int amount)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || amount <= 0 ||
            Player.statLife >= Player.statLifeMax2)
            return false;
        int healed = Math.Min(amount, Player.statLifeMax2 - Player.statLife);
        Player.statLife += healed;
        Player.HealEffect(healed, true);
        return true;
    }

    private float discResonanceHealingRemainder;

    public bool ApplyDiscResonanceHealing(int amount)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || amount <= 0 ||
            Player.statLife >= Player.statLifeMax2)
            return false;
        float scaled = amount * 0.8f + discResonanceHealingRemainder;
        int healing = (int)(scaled + 0.0001f);
        discResonanceHealingRemainder = scaled - healing;
        return ApplyResonanceHealing(healing);
    }

    public void ShakeScreen(Vector2 source, float power, float maxDistance = 1400f)
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer || power <= 0f)
            return;

        float falloff = 1f - MathHelper.Clamp(Vector2.Distance(Player.Center, source) / maxDistance, 0f, 1f);
        screenShakePower = Math.Max(screenShakePower, power * falloff);
    }

    internal void LockUltimateCamera(Vector2 focus)
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer ||
            !float.IsFinite(focus.X) || !float.IsFinite(focus.Y))
            return;

        ultimateCameraFocus = focus;
        // The controller refreshes this every update.  Two ticks cover normal network jitter
        // without letting a stale lock survive after the ultimate ends.
        ultimateCameraLockTime = 2;
    }

    internal void ClearUltimateCamera()
    {
        if (Player.whoAmI == Main.myPlayer)
            ultimateCameraLockTime = 0;
    }

    private void UpdateTimers()
    {
        if (DashCooldown > 0)
            DashCooldown--;
        if (ComboReset > 0)
        {
            bool pauseForCatStep = DashTimer > 0 &&
                Player.GetModPlayer<FranEvolutionPlayer>().HasNode(FranEvolutionNode.CatStep);
            if (!pauseForCatStep && --ComboReset == 0)
                ComboIndex = 0;
        }
        if (BlueResonanceTimer > 0)
            BlueResonanceTimer--;
        if (StolenSkillTimer > 0)
            StolenSkillTimer--;
        if (StolenContactDamageTimer > 0 && --StolenContactDamageTimer == 0)
            StolenContactDamage = 0;
        for (int i = 0; i < SkillCooldowns.Length; i++)
            if (SkillCooldowns[i] > 0)
            {
                SkillCooldowns[i]--;
                if (SkillCooldowns[i] == 0)
                    SkillCooldownMaximums[i] = 0;
            }
    }

    private void MaintainSelectedImprint()
    {
        if (Player.whoAmI != Main.myPlayer || Player.dead || GetSkillStacks(FranSkill.MagicBarrier) <= 0 ||
            !IsImprintReady(FranSkill.MagicBarrier))
            return;
        int barrierType = ModContent.ProjectileType<FranAutoBarrierProjectile>();
        if (Player.ownedProjectileCounts[barrierType] <= 0)
            Projectile.NewProjectile(Player.GetSource_Misc("FranBarrierImprint"), Player.MountedCenter,
                Vector2.Zero, barrierType, Math.Max(1, (int)(Player.GetWeaponDamage(Player.HeldItem) *
                    FranBalance.GetFloat(nameof(FranBalance.MagicBarrierWeaponDamageMultiplier),
                        FranBalance.MagicBarrierWeaponDamageMultiplier))), 3f,
                Player.whoAmI);
    }

    public void OnBarrierBroken()
    {
        StartImprintCooldown(FranSkill.MagicBarrier);
    }

    public void GrantStolenSkill() => StolenSkillTimer = Math.Max(StolenSkillTimer, 6 * 60);

    public void AddStolenContactDamage(int contactDamage)
    {
        if (contactDamage <= 0)
            return;

        int weaponDamage = Math.Max(1, Player.GetWeaponDamage(Player.HeldItem));
        int cap = Math.Max(1, (int)MathF.Floor(weaponDamage * FranBalance.GetFloat(
            nameof(FranBalance.SkillTakerStolenContactDamageWeaponCapRatio),
            FranBalance.SkillTakerStolenContactDamageWeaponCapRatio)));
        StolenContactDamage = Math.Min(cap, StolenContactDamage + contactDamage);
        StolenContactDamageTimer = Math.Max(1, FranBalance.GetInt(
            nameof(FranBalance.SkillTakerStolenContactDamageDurationFrames),
            FranBalance.SkillTakerStolenContactDamageDurationFrames));
    }

    public int GetStolenContactDamage(int weaponDamage)
    {
        if (StolenContactDamageTimer <= 0)
            return 0;

        int cap = Math.Max(1, (int)MathF.Floor(Math.Max(1, weaponDamage) * FranBalance.GetFloat(
            nameof(FranBalance.SkillTakerStolenContactDamageWeaponCapRatio),
            FranBalance.SkillTakerStolenContactDamageWeaponCapRatio)));
        return Math.Min(StolenContactDamage, cap);
    }

    public bool ConsumeStolenSkillBoost()
    {
        if (StolenSkillTimer <= 0)
            return false;
        StolenSkillTimer = 0;
        return true;
    }

    private void UpdateUltimateCharge()
    {
        if (!UltimateUnlocked || Main.netMode == NetmodeID.MultiplayerClient || UltimateActive || UltimateReady)
            return;

        FranEvolutionPlayer evolution = Player.GetModPlayer<FranEvolutionPlayer>();
        int pulseInterval = Math.Max(1, FranBalance.GetInt(nameof(FranBalance.EvolutionUltimateChargePulseInterval),
            FranBalance.EvolutionUltimateChargePulseInterval));
        int chargeGain = SelectedUltimate != FranUltimateKind.Vigil && evolution.AcceleratedUltimateCharge && Main.GameUpdateCount % (ulong)pulseInterval == 0
            ? Math.Max(1, FranBalance.GetInt(nameof(FranBalance.EvolutionUltimateChargePulseAmount),
                FranBalance.EvolutionUltimateChargePulseAmount))
            : 1;
        UltimateChargeTicks += chargeGain;
        if (UltimateChargeTicks >= SelectedUltimateChargeDuration)
        {
            UltimateChargeTicks = SelectedUltimateChargeDuration;
            AnnounceUltimateReady();
            if (Main.netMode == NetmodeID.Server)
                FranModMain.SendUltimateCharge(Player.whoAmI, UltimateChargeTicks);
            return;
        }

        if (Main.netMode == NetmodeID.Server && ++ultimateChargeSyncTimer >= 60)
        {
            ultimateChargeSyncTimer = 0;
            FranModMain.SendUltimateCharge(Player.whoAmI, UltimateChargeTicks);
        }
    }

    private void UpdateInputs()
    {
        if (Player.whoAmI != Main.myPlayer || Main.playerInventory || Player.mouseInterface)
            return;

        if (FranModMain.UltimateKey.JustPressed && (VigilActive || HoldingShiFu && UltimateReady && !UltimateActive))
        {
            Vector2 focusPoint = Main.MouseWorld;
            if (Main.netMode == NetmodeID.MultiplayerClient)
                FranModMain.SendUltimateRequest(focusPoint);
            else
                TryStartUltimate(focusPoint);
            return;
        }

        bool canDash = CanStartFranDash();
        bool dashKeyBound = IsDashKeyBound();
        if (dashKeyBound)
        {
            if (canDash && FranModMain.DashKey.JustPressed)
                StartDash(ResolveManualDashDirection());

            // Once a manual dash key is assigned, vanilla
            // left/right double taps are not a secondary trigger.
            return;
        }

        if (!canDash)
            return;

        if (Player.controlLeft && Player.releaseLeft && Player.doubleTapCardinalTimer[3] < 15)
            StartDash(-Vector2.UnitX);
        else if (Player.controlRight && Player.releaseRight && Player.doubleTapCardinalTimer[2] < 15)
            StartDash(Vector2.UnitX);
    }

    private static bool IsDashKeyBound()
    {
        if (Main.dedServ || FranModMain.DashKey is null)
            return false;

        try
        {
            foreach (string key in FranModMain.DashKey.GetAssignedKeys())
            {
                if (!string.IsNullOrWhiteSpace(key) && !key.Equals("None", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            // Input profiles can be rebuilt while the controls menu is applying changes.
            // Falling back to the double-tap route is safe for that one frame.
        }
        return false;
    }

    private bool CanStartFranDash() =>
        !BlockingUltimateActive && !HasExternalDash && DashCooldown <= 0 && !Player.mount.Active && !Player.pulley &&
        Player.grappling[0] == -1 && !Player.tongued;

    private Vector2 ResolveManualDashDirection()
    {
        FranEvolutionPlayer evolution = Player.GetModPlayer<FranEvolutionPlayer>();
        if (evolution.HasNode(FranEvolutionNode.AerialHunt))
        {
            Vector2 input = new((Player.controlRight ? 1f : 0f) - (Player.controlLeft ? 1f : 0f),
                (Player.controlDown ? 1f : 0f) - (Player.controlUp ? 1f : 0f));
            Vector2 desired = input.LengthSquared() > 0.01f
                ? input
                : Main.MouseWorld - Player.Center;
            if (desired.LengthSquared() > 0.01f)
            {
                float snappedAngle = MathF.Round(desired.ToRotation() / MathHelper.PiOver4) * MathHelper.PiOver4;
                return snappedAngle.ToRotationVector2();
            }
        }

        // Original horizontal priority: held direction, current momentum, then facing direction.
        if (Player.controlRight && !Player.controlLeft)
            return Vector2.UnitX;
        if (Player.controlLeft && !Player.controlRight)
            return -Vector2.UnitX;
        if (Math.Abs(Player.velocity.X) > 0.01f)
            return Vector2.UnitX * Math.Sign(Player.velocity.X);
        return Vector2.UnitX * (Player.direction == 0 ? 1 : Player.direction);
    }

    private void StartDash(Vector2 direction)
    {
        direction = direction.SafeNormalize(Vector2.UnitX * (Player.direction == 0 ? 1 : Player.direction));
        FranEvolutionPlayer evolution = Player.GetModPlayer<FranEvolutionPlayer>();
        float dashSpeed = FranBalance.GetFloat(nameof(FranBalance.DashBaseSpeed), FranBalance.DashBaseSpeed) *
            evolution.DashSpeedMultiplier * FranBalance.GetDashProgressionSpeedMultiplier();
        DashTimer = DashDuration;
        DashCooldown = evolution.DashCooldownTicks;
        DashVelocity = direction * dashSpeed;
        // Horizontal Black Tiger dashes preserve jump/fall momentum. Omnidirectional
        // dashes keep their authored vertical impulse.
        Player.velocity = Math.Abs(direction.Y) <= 0.05f
            ? new Vector2(DashVelocity.X, Player.velocity.Y)
            : DashVelocity;
        // Cancel a fallback framework dash that may have reacted to the same double tap earlier
        // in the update order. Equipped external dashes never reach StartDash in the first place.
        Player.dashDelay = 0;
        if (Math.Abs(direction.X) > 0.05f)
            Player.ChangeDir(Math.Sign(direction.X));
        dashStartedThisTick = true;
        dashHitNpcs.Clear();
        Player.fullRotation = 0f;
        FranModMain.SendCombatState(Player.whoAmI, DashTimer, DashVelocity);
        Player.GiveImmuneTimeForCollisionAttack(DashDuration + 2 + evolution.DashImmunityBonus);
        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.18f, Volume = 0.65f }, Player.Center);
        SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.55f, Volume = 0.24f }, Player.Center);
        ShakeScreen(Player.Center, 3.5f);
        if (Player.whoAmI == Main.myPlayer)
        {
            Projectile.NewProjectile(Player.GetSource_Misc("FranDashVisual"), Player.Center, DashVelocity,
                ModContent.ProjectileType<FranDashTrail>(), 0, 0f, Player.whoAmI, DashDuration);
        }
    }

    private void UpdateDash()
    {
        if (DashTimer <= 0)
            return;

        bool justStarted = dashStartedThisTick;
        dashStartedThisTick = false;

        bool collisionChangedVelocity = false;
        if (!justStarted && Player.velocity.LengthSquared() < 1.25f * 1.25f)
        {
            DashTimer = 0;
            collisionChangedVelocity = true;
        }

        Lighting.AddLight(Player.Center, 0.10f, 0.04f, 0.16f);
        if (!Main.dedServ && Main.GameUpdateCount % 2 == 0)
        {
            Vector2 dashMotion = Player.velocity;
            Dust dust = Dust.NewDustPerfect(Player.Center - dashMotion * 0.45f, DustID.Shadowflame,
                -dashMotion * 0.12f + Main.rand.NextVector2Circular(1.2f, 1.2f), 110,
                new Color(154, 94, 218), 1.20f);
            dust.noGravity = true;
        }

        if (Main.netMode != NetmodeID.Server && Player.whoAmI == Main.myPlayer)
        {
            Rectangle hitArea = Player.Hitbox;
            hitArea.Offset((int)(Player.velocity.X * 0.5f), (int)(Player.velocity.Y * 0.5f));
            hitArea.Inflate(6, 6);
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy() || dashHitNpcs.Contains(npc.whoAmI) || !hitArea.Intersects(npc.Hitbox) ||
                    (!npc.noTileCollide && !Collision.CanHit(Player.position, Player.width, Player.height,
                        npc.position, npc.width, npc.height)))
                    continue;
                dashHitNpcs.Add(npc.whoAmI);
                FranEvolutionPlayer evolution = Player.GetModPlayer<FranEvolutionPlayer>();
                int damage = Math.Max(1, (int)(FranBalance.GetDashDamage() *
                    evolution.DashImpactDamageMultiplier));
                FranBurst.Spawn(Player.GetSource_Misc("FranDash"), npc.Center, Player.whoAmI,
                    FranBurstStyle.DashImpact, 1f, damage, 6f, Player.velocity.ToRotation());
                Player.GetModPlayer<FranEvolutionPlayer>().ApplyDashHit(npc);
            }
        }

        if (!justStarted && DashTimer > 0)
            DashTimer--;

        if (DashTimer <= 0)
            EndDash(sync: Player.whoAmI == Main.myPlayer);
        else
        {
            // The Black Heavenly Tiger dash is a direct body-check, not a somersault. Holding the
            // rotation at zero also prevents another visual system from leaving a partial roll behind.
            Player.fullRotation = 0f;
            if (collisionChangedVelocity && Player.whoAmI == Main.myPlayer)
                FranModMain.SendCombatState(Player.whoAmI, DashTimer, DashVelocity);
        }
    }

    private void UpdateTelekineticFlightVisual()
    {
        // Zero vertical speed is not proof of landing: horizontal flight and down-hover can both
        // hold Y at zero. Vanilla restores wingTime only after actual ground contact, so require
        // both a stationary vertical axis and a fully restored timer before ending the flight VFX.
        bool landed = Math.Abs(Player.velocity.Y) <= 0.01f && Player.wingTime >= Player.wingTimeMax;
        if (!TelekineticFlightEnabled || Player.mount.Active || landed)
            telekineticFlightSession = false;
        else if (Player.controlJump && Player.wingTime > 0f)
            telekineticFlightSession = true;

        TelekineticFlightVisualActive = TelekineticFlightEnabled && telekineticFlightSession;
        if (!TelekineticFlightVisualActive || Main.dedServ || Player.whoAmI != Main.myPlayer)
            return;

        int visualType = ModContent.ProjectileType<FranTelekineticFlightVisual>();
        if (Player.ownedProjectileCounts[visualType] <= 0)
            Projectile.NewProjectile(Player.GetSource_Misc("FranTelekineticFlight"), Player.Center,
                Vector2.Zero, visualType, 0, 0f, Player.whoAmI);
    }

    private void EndDash(bool sync)
    {
        DashTimer = 0;
        DashVelocity = Vector2.Zero;
        dashStartedThisTick = false;
        dashHitNpcs.Clear();
        Player.fullRotation = 0f;
        if (sync)
            FranModMain.SendCombatState(Player.whoAmI, 0, Vector2.Zero);
    }

    // Keep the retired item registered just long enough to remove it safely from existing character saves.
    // Removing its type outright would turn already-owned copies into Terraria's visible Unloaded Item placeholder.
    private void RemoveRetiredShiFuExcavationTools()
    {
        int toolType = ModContent.ItemType<ShiFuExcavationTool>();
        int guideType = ModContent.ItemType<StrategyGuideBook>();
        RemoveFromStorage(Player.inventory, toolType);
        RemoveFromStorage(Player.inventory, guideType);
        RemoveFromStorage(Player.bank.item, toolType);
        RemoveFromStorage(Player.bank.item, guideType);
        RemoveFromStorage(Player.bank2.item, toolType);
        RemoveFromStorage(Player.bank2.item, guideType);
        RemoveFromStorage(Player.bank3.item, toolType);
        RemoveFromStorage(Player.bank3.item, guideType);
        RemoveFromStorage(Player.bank4.item, toolType);
        RemoveFromStorage(Player.bank4.item, guideType);

        if (Player.trashItem.type is var trashType && (trashType == toolType || trashType == guideType))
            Player.trashItem.TurnToAir();
        if (Player.whoAmI == Main.myPlayer && Main.mouseItem.type is var mouseType &&
            (mouseType == toolType || mouseType == guideType))
            Main.mouseItem.TurnToAir();
    }

    private static void RemoveFromStorage(Item[] storage, int retiredType)
    {
        foreach (Item item in storage)
            if (item.type == retiredType)
                item.TurnToAir();
    }

    private void EnsureFloatingShiFu()
    {
        if (!ModContent.GetInstance<FranClientConfig>().AlwaysFloatShiFu)
            return;

        int flightVisualType = ModContent.ProjectileType<FranTelekineticFlightVisual>();
        if (!HasShiFu || Player.dead || TelekineticFlightVisualActive ||
            Player.ownedProjectileCounts[flightVisualType] > 0)
            return;

        if (Player.HeldItem.type == ModContent.ItemType<ShiFu>())
            return;

        int type = ModContent.ProjectileType<FloatingShiFuVisual>();
        if (Player.ownedProjectileCounts[type] == 0 && Player.whoAmI == Main.myPlayer)
            Projectile.NewProjectile(Player.GetSource_Misc("FloatingShiFu"), Player.Center, Vector2.Zero, type, 0, 0f, Player.whoAmI);
    }

    private static Color LitColor(Color baseColor, Color environmentLight) =>
        new(baseColor.ToVector3() * environmentLight.ToVector3());

    private void ShowIntroduction()
    {
        if (introShown || Player.whoAmI != Main.myPlayer)
            return;
        introShown = true;
        Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroTitle"), new Color(145, 225, 255));
        Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroCombat"), Color.White);
        Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroSkills", ShiFu.WheelOpenInstruction()), new Color(185, 215, 255));
        if (FranEvolutionInfo.SystemEnabled)
            Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroEvolution"), new Color(205, 178, 245));
        Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroDash"), new Color(185, 215, 255));
        Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroPotential"), new Color(225, 235, 255));
    }

    internal bool TryStartUltimate(Vector2 requestedFocus)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient && VigilActive)
        {
            Player.GetModPlayer<FranVigilPlayer>().SetActive(false);
            return true;
        }
        if (!UltimateUnlocked || Main.netMode == NetmodeID.MultiplayerClient || !FranMode || Player.dead || !UltimateReady ||
            UltimateActive || !HoldingShiFu)
            return false;

        if (!float.IsFinite(requestedFocus.X) || !float.IsFinite(requestedFocus.Y))
            requestedFocus = Player.Center + Vector2.UnitX * Player.direction * 480f;
        Vector2 offset = requestedFocus - Player.Center;
        if (offset.LengthSquared() > 1800f * 1800f)
            requestedFocus = Player.Center + offset.SafeNormalize(Vector2.UnitX * Player.direction) * 1800f;

        int strikeDamage = GetUltimateStrikeDamage();

        if (SelectedUltimate == FranUltimateKind.Singularity &&
            (FranSingularityUltimate.FindAny() is not null || SustainedBlackHole.FindSoleBlackHole() is not null))
            return false;

        if (SelectedUltimate == FranUltimateKind.Vigil)
        {
            UltimateChargeTicks = 0;
            ultimateChargeSyncTimer = 0;
            ultimateReadyAnnounced = false;
            Player.GetModPlayer<FranVigilPlayer>().SetActive(true);
            if (Main.netMode == NetmodeID.Server) FranModMain.SendUltimateCharge(Player.whoAmI, 0);
            return true;
        }

        if (DashTimer > 0)
            EndDash(sync: false);
        int ultimateType = SelectedUltimate == FranUltimateKind.Singularity
            ? ModContent.ProjectileType<FranSingularityUltimate>() : ModContent.ProjectileType<FranUltimateController>();
        if (SelectedUltimate == FranUltimateKind.Singularity)
            strikeDamage = Math.Max(1, Player.GetWeaponDamage(Player.HeldItem));
        int projectileIndex = Projectile.NewProjectile(Player.GetSource_Misc("FranUltimate"), Player.Center,
            Vector2.Zero, ultimateType, strikeDamage, 8f,
            Player.whoAmI, requestedFocus.X, requestedFocus.Y, Player.selectedItem);
        if (!Main.projectile.IndexInRange(projectileIndex))
            return false;

        if (SelectedUltimate == FranUltimateKind.Singularity)
            foreach (Projectile active in Main.ActiveProjectiles)
                if (active.owner == Player.whoAmI && active.ModProjectile is ShiFuSwingRebuild or FranQuickdrawDiscHoldout or FranSpinningDisc)
                    active.Kill();

        UltimateChargeTicks = 0;
        ultimateChargeSyncTimer = 0;
        ultimateReadyAnnounced = false;
        if (Main.netMode == NetmodeID.Server)
            FranModMain.SendUltimateCharge(Player.whoAmI, 0);
        return true;
    }

    internal int GetUltimateStrikeDamage()
    {
        // 黑天虎仍以师父为伤害来源；切到其他物品不会借用其他武器的面板。
        int shiFuType = ModContent.ItemType<ShiFu>();
        Item shiFu = Player.HeldItem.type == shiFuType ? Player.HeldItem : null;
        if (shiFu is null)
            for (int i = 0; i < Player.inventory.Length; i++)
                if (Player.inventory[i].type == shiFuType)
                {
                    shiFu = Player.inventory[i];
                    break;
                }
        int shiFuDamage = shiFu is null
            ? Math.Max(1, (int)Player.GetDamage(DamageClass.Melee).ApplyTo(FranBalance.GetBaseDamage(Player)))
            : Math.Max(1, Player.GetWeaponDamage(shiFu));
        return Math.Max(1, (int)(shiFuDamage *
            FranBalance.GetFloat(nameof(FranBalance.UltimateStrikeDamageMultiplier),
                FranBalance.UltimateStrikeDamageMultiplier)));
    }

    private void AnnounceUltimateReady()
    {
        if (!UltimateUnlocked || ultimateReadyAnnounced)
            return;
        ultimateReadyAnnounced = true;
        if (!Main.dedServ)
            SoundEngine.PlaySound(FranCombatSounds.大招蓄势完成, Player.Center);
        Projectile.NewProjectile(Player.GetSource_Misc("FranUltimateReady"), Player.Center, Vector2.Zero,
            ModContent.ProjectileType<FranUltimateReadySigil>(), 0, 0f, Player.whoAmI);
    }

    public void SetUltimateGaugeAnchor(Vector2 normalizedAnchor) =>
        UltimateGaugeAnchor = new Vector2(MathHelper.Clamp(normalizedAnchor.X, 0f, 1f),
            MathHelper.Clamp(normalizedAnchor.Y, 0f, 1f));
}
