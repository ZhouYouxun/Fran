using System;
using System.Collections.Generic;
using System.Reflection;
using FranMod.Content.Buffs;
using FranMod.Content.Config;
using FranMod.Content.Items;
using FranMod.Content.Projectiles;
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
    public Vector2 UltimateGaugeAnchor = DefaultUltimateGaugeAnchor;
    public int DashTimer;
    public int DashCooldown;
    public Vector2 DashVelocity;
    public int ComboIndex;
    public int ComboReset;
    public int BlueResonanceTimer;
    public int StolenSkillTimer;
    public int HammerComboRounds;
    public int ResonanceHealCooldown;
    public readonly int[] SkillCooldowns = new int[FranSkillInfo.Count];
    public readonly int[] SkillCooldownMaximums = new int[FranSkillInfo.Count];
    public bool TelekineticFlightEnabled { get; private set; }
    public bool TelekineticFlightVisualActive { get; private set; }

    private readonly HashSet<int> dashHitNpcs = new();
    private static readonly Dictionary<Type, PropertyInfo> ExternalDashIdProperties = new();
    private bool introShown;
    private bool wasFranMode;
    private bool networkFranMode;
    private bool teacherBonded;
    private bool teacherToolGranted;
    private bool restoreTeacherAfterRespawn;
    private bool dashStartedThisTick;
    private int teacherRecoveryCooldown;
    private float screenShakePower;
    private Vector2 ultimateCameraFocus;
    private int ultimateCameraLockTime;
    private int ultimateChargeSyncTimer;
    private bool ultimateReadyAnnounced;
    private bool telekineticFlightSession;

    public bool HasTeacher
    {
        get
        {
            int type = ModContent.ItemType<Teacher>();
            return Player.HasItem(type) || Player.whoAmI == Main.myPlayer && Main.mouseItem.type == type;
        }
    }
    public int AvailableTacticSlots => FranBalance.GetAvailableTacticSlots();
    public FranSkill SelectedSkill => GetEquippedTactic(0);
    public bool HasSelectedSkill => GetActiveTacticCount() > 0;
    // dashDelay is runtime state, not proof that another dash is equipped. In particular,
    // Calamity's built-in DefaultDash may touch it before Fran reads input this frame.
    public bool HasExternalDash => Player.dashType != 0 || HasNamedExternalDash();
    public bool HasExternalWings => Player.wingsLogic > 0 &&
        !FranTelekineticFlightSystem.IsTelekineticWingSlot(Player.wingsLogic);
    public bool UltimateReady => UltimateChargeTicks >= UltimateChargeDuration;
    public bool UltimateActive => Player.ownedProjectileCounts[ModContent.ProjectileType<FranUltimateController>()] > 0;

    private bool HasNamedExternalDash()
    {
        // Calamity and several other dash frameworks expose their selected dash as DashID while
        // keeping Player.dashType at zero. Read that convention without taking a hard dependency.
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
        // Calamity reports DefaultDash through DashID even when no dash accessory is equipped.
        // Fran replaces only this fallback while Fran mode is active; real Calamity/vanilla
        // accessory dashes still win through their distinct DashID or Player.dashType.
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
        if (UltimateChargeTicks > 0)
            tag["UltimateChargeTicks"] = Math.Clamp(UltimateChargeTicks, 0, UltimateChargeDuration);
        if (Vector2.DistanceSquared(UltimateGaugeAnchor, DefaultUltimateGaugeAnchor) > 0.000001f)
        {
            tag["UltimateGaugeX"] = UltimateGaugeAnchor.X;
            tag["UltimateGaugeY"] = UltimateGaugeAnchor.Y;
        }
        if (introShown)
            tag["IntroShown"] = true;
        if (teacherBonded)
            tag["TeacherBonded"] = true;
        if (teacherToolGranted)
            tag["TeacherToolGranted"] = true;
    }

    public override void LoadData(TagCompound tag)
    {
        ClearTacticArray();
        if (tag.ContainsKey("EquippedTacticsV1"))
        {
            IList<int> saved = tag.GetList<int>("EquippedTacticsV1");
            for (int i = 0; i < Math.Min(MaximumTacticSlots, saved.Count); i++)
                equippedTactics[i] = saved[i] >= 0 && saved[i] < FranSkillInfo.Count
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
            if (value >= 0 && value < FranSkillInfo.Count)
                equippedTactics[0] = (FranSkill)value;
        }
        UltimateChargeTicks = Math.Clamp(tag.GetInt("UltimateChargeTicks"), 0, UltimateChargeDuration);
        float gaugeX = tag.ContainsKey("UltimateGaugeX") ? tag.GetFloat("UltimateGaugeX") : DefaultUltimateGaugeAnchor.X;
        float gaugeY = tag.ContainsKey("UltimateGaugeY") ? tag.GetFloat("UltimateGaugeY") : DefaultUltimateGaugeAnchor.Y;
        UltimateGaugeAnchor = new Vector2(MathHelper.Clamp(gaugeX, 0f, 1f),
            MathHelper.Clamp(gaugeY, 0f, 1f));
        ultimateReadyAnnounced = UltimateReady;
        introShown = tag.GetBool("IntroShown");
        teacherBonded = tag.GetBool("TeacherBonded");
        teacherToolGranted = tag.GetBool("TeacherToolGranted");
    }

    public override void ResetEffects()
    {
        TelekineticFlightEnabled = false;
        bool localAuthority = Main.netMode == NetmodeID.SinglePlayer || Player.whoAmI == Main.myPlayer;
        FranMode = IsFranName(Player.name) || (localAuthority ? HasFavoritedHeadwear() : networkFranMode);
        if (!FranMode)
            return;

        Player.AddBuff(ModContent.BuffType<BlackCatWarriorBuff>(), 2);
    }

    public override void PostUpdateEquips()
    {
        if (!FranMode || !Main.hardMode || Player.mount.Active || HasExternalWings)
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

    public override void FrameEffects()
    {
        if (!FranMode)
            return;

        // Prototype appearance: vanilla cat ears are forced over the equipped helmet.
        // The final character sprites can replace this without touching gameplay code.
        Player.head = ContentSamples.ItemsByType[ItemID.CatEars].headSlot;
        Player.body = ContentSamples.ItemsByType[ItemID.FamiliarShirt].bodySlot;
        Player.legs = ContentSamples.ItemsByType[ItemID.FamiliarPants].legSlot;
    }

    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        if (!FranMode)
            return;

        // A readable Fran silhouette made entirely from vanilla player layers.
        drawInfo.drawPlayer.head = ContentSamples.ItemsByType[ItemID.CatEars].headSlot;
        drawInfo.drawPlayer.body = ContentSamples.ItemsByType[ItemID.FamiliarShirt].bodySlot;
        drawInfo.drawPlayer.legs = ContentSamples.ItemsByType[ItemID.FamiliarPants].legSlot;
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

    public override void OnEnterWorld()
    {
        EnsureTeacher();
    }

    public override void CopyClientState(ModPlayer targetCopy)
    {
        FranPlayer copy = (FranPlayer)targetCopy;
        copy.FranMode = FranMode;
        Array.Copy(equippedTactics, copy.equippedTactics, MaximumTacticSlots);
    }

    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        FranPlayer copy = (FranPlayer)clientPlayer;
        if (copy.FranMode != FranMode)
            FranModMain.SendFranMode(Player.whoAmI, FranMode);
        if (!TacticLoadoutEquals(copy.equippedTactics))
            FranModMain.SendTacticLoadout(Player.whoAmI, equippedTactics);
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        FranModMain.SendFranMode(Player.whoAmI, FranMode, toWho, fromWho);
        FranModMain.SendCombatState(Player.whoAmI, DashTimer, DashVelocity, toWho, fromWho);
        FranModMain.SendTacticLoadout(Player.whoAmI, equippedTactics, toWho, fromWho);
        if (Main.netMode == NetmodeID.Server)
            FranModMain.SendUltimateCharge(Player.whoAmI, UltimateChargeTicks, toWho, fromWho);
    }

    internal void ReceiveNetworkMode(bool active) => networkFranMode = active;

    internal void ReceiveNetworkCombatState(int dashTimer, Vector2 dashVelocity)
    {
        DashTimer = dashTimer;
        DashVelocity = dashVelocity;
        dashStartedThisTick = dashTimer > 0;
        dashHitNpcs.Clear();
    }

    internal void ReceiveUltimateCharge(int chargeTicks)
    {
        UltimateChargeTicks = Math.Clamp(chargeTicks, 0, UltimateChargeDuration);
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

    public override void PostUpdate()
    {
        if (Player.dead)
        {
            TelekineticFlightVisualActive = false;
            telekineticFlightSession = false;
            Player.fullRotation = 0f;
            return;
        }

        if (FranMode || HasTeacher)
            teacherBonded = true;
        if (Player.whoAmI == Main.myPlayer && teacherBonded)
        {
            EnsureTeacherToolInitialGrant();
            RecallTeacherFromStorage();
            if (teacherRecoveryCooldown > 0)
                teacherRecoveryCooldown--;
            EnsureTeacher();
        }

        if (!FranMode)
        {
            wasFranMode = false;
            TelekineticFlightVisualActive = false;
            telekineticFlightSession = false;
            if (DashTimer > 0 || DashVelocity != Vector2.Zero)
                EndDash(sync: Player.whoAmI == Main.myPlayer);
            if (HasTeacher)
            {
                RemoveLockedTactics();
                UpdateTimers();
                MaintainSelectedImprint();
                EnsureFloatingTeacher();
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
        EnsureFloatingTeacher();
    }

    public override void PreUpdateMovement()
    {
        if (!FranMode || DashTimer <= 0 && !UltimateActive)
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
        if (!UltimateActive && !Content.UI.FranUISystem.IsWheelOpen)
            return;
        if (Content.UI.FranUISystem.IsWheelOpen && !UltimateActive)
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
    }

    public override bool CanUseItem(Item item) => !UltimateActive && !Content.UI.FranUISystem.IsWheelOpen;

    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        if (!FranMode)
            return;
        int barrierLayers = GetSkillStacks(FranSkill.MagicBarrier);
        if (barrierLayers > 0 && FranAutoBarrierProjectile.IsActive(Player))
            modifiers.SourceDamage *= 1f -
                FranBalance.GetFloat(nameof(FranBalance.MagicBarrierDamageReductionPerLayer),
                    FranBalance.MagicBarrierDamageReductionPerLayer) *
                Math.Min(MaximumTacticSlots, barrierLayers);
        if (FranClientConfig.Instance?.EnableFranVoice ?? true)
        {
            modifiers.DisableSound();
            SoundEngine.PlaySound(SoundID.PlayerHit with { Pitch = 0.28f, Volume = 0.72f }, Player.Center);
        }
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
        if (Player.whoAmI == Main.myPlayer && Player.difficulty == PlayerDifficultyID.MediumCore && teacherBonded && HasTeacher)
        {
            int teacherType = ModContent.ItemType<Teacher>();
            for (int i = 0; i < Player.inventory.Length; i++)
            {
                if (Player.inventory[i].type != teacherType)
                    continue;
                Player.inventory[i].TurnToAir();
                restoreTeacherAfterRespawn = true;
            }
            if (Main.mouseItem.type == teacherType)
            {
                Main.mouseItem.TurnToAir();
                restoreTeacherAfterRespawn = true;
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
        HammerComboRounds = 0;
        ResonanceHealCooldown = 0;
        screenShakePower = 0f;
        dashHitNpcs.Clear();
        Array.Clear(SkillCooldowns);
        Array.Clear(SkillCooldownMaximums);

        if (Player.whoAmI == Main.myPlayer)
        {
            FranModMain.SendCombatState(Player.whoAmI, 0, Vector2.Zero);
            if (restoreTeacherAfterRespawn)
            {
                restoreTeacherAfterRespawn = false;
                Player.QuickSpawnItem(Player.GetSource_Misc("FranTeacherRespawn"), ModContent.ItemType<Teacher>());
            }
        }
    }

    public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) =>
        FranMode && (DashTimer > 0 || UltimateActive);

    public int ConsumeNextCombo()
    {
        int current = ComboIndex;
        ComboIndex = (ComboIndex + 1) % 4;
        ComboReset = Player.GetModPlayer<FranEvolutionPlayer>().ComboResetDuration;
        return current;
    }

    public FranSkill GetEquippedTactic(int slot)
    {
        if (slot < 0 || slot >= Math.Min(AvailableTacticSlots, MaximumTacticSlots))
            return FranSkill.None;
        return FranSkillInfo.IsRealSkill(equippedTactics[slot]) ? equippedTactics[slot] : FranSkill.None;
    }

    public int GetActiveTacticCount()
    {
        int count = 0;
        for (int i = 0; i < Math.Min(AvailableTacticSlots, MaximumTacticSlots); i++)
            if (FranSkillInfo.IsRealSkill(equippedTactics[i]))
                count++;
        return count;
    }

    public int GetSkillStacks(FranSkill skill)
    {
        if (!FranSkillInfo.IsRealSkill(skill))
            return 0;
        int count = 0;
        for (int i = 0; i < Math.Min(AvailableTacticSlots, MaximumTacticSlots); i++)
            if (equippedTactics[i] == skill)
                count++;
        return count;
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

    public bool AdvanceHammerRound()
    {
        HammerComboRounds++;
        if (HammerComboRounds < FranBalance.GetInt(nameof(FranBalance.ThorsHammerRequiredComboRounds),
                FranBalance.ThorsHammerRequiredComboRounds))
            return false;
        HammerComboRounds = 0;
        return true;
    }

    public bool TryApplyResonanceHealing(int amount)
    {
        if (ResonanceHealCooldown > 0 || amount <= 0 || Player.statLife >= Player.statLifeMax2)
            return false;
        int healed = Math.Min(amount, Player.statLifeMax2 - Player.statLife);
        Player.statLife += healed;
        Player.HealEffect(healed, true);
        ResonanceHealCooldown = FranBalance.GetInt(nameof(FranBalance.BlueResonanceHealingCooldownFrames),
            FranBalance.BlueResonanceHealingCooldownFrames);
        return true;
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
        if (ResonanceHealCooldown > 0)
            ResonanceHealCooldown--;
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

    public bool ConsumeStolenSkillBoost()
    {
        if (StolenSkillTimer <= 0)
            return false;
        StolenSkillTimer = 0;
        return true;
    }

    private void UpdateUltimateCharge()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || UltimateActive || UltimateReady)
            return;

        FranEvolutionPlayer evolution = Player.GetModPlayer<FranEvolutionPlayer>();
        int pulseInterval = Math.Max(1, FranBalance.GetInt(nameof(FranBalance.EvolutionUltimateChargePulseInterval),
            FranBalance.EvolutionUltimateChargePulseInterval));
        int chargeGain = evolution.AcceleratedUltimateCharge && Main.GameUpdateCount % (ulong)pulseInterval == 0
            ? Math.Max(1, FranBalance.GetInt(nameof(FranBalance.EvolutionUltimateChargePulseAmount),
                FranBalance.EvolutionUltimateChargePulseAmount))
            : 1;
        UltimateChargeTicks += chargeGain;
        if (UltimateChargeTicks >= UltimateChargeDuration)
        {
            UltimateChargeTicks = UltimateChargeDuration;
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

        if (FranModMain.UltimateKey.JustPressed && UltimateReady && !UltimateActive)
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

            // Match Calamity's input contract: once a manual dash key is assigned, vanilla
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
        !UltimateActive && !HasExternalDash && DashCooldown <= 0 && !Player.mount.Active && !Player.pulley &&
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
            evolution.DashSpeedMultiplier;
        DashTimer = DashDuration;
        DashCooldown = evolution.DashCooldownTicks;
        DashVelocity = direction * dashSpeed;
        Player.velocity = DashVelocity;
        // Cancel a fallback framework dash that may have reacted to the same double tap earlier
        // in the update order. Equipped external dashes never reach StartDash in the first place.
        Player.dashDelay = 0;
        if (Math.Abs(direction.X) > 0.05f)
            Player.ChangeDir(Math.Sign(direction.X));
        dashStartedThisTick = true;
        dashHitNpcs.Clear();
        FranModMain.SendCombatState(Player.whoAmI, DashTimer, DashVelocity);
        Player.GiveImmuneTimeForCollisionAttack(DashDuration + 2 + evolution.DashImmunityBonus);
        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.18f, Volume = 0.65f }, Player.Center);
        ShakeScreen(Player.Center, 2.4f);
        if (Player.whoAmI == Main.myPlayer)
            Projectile.NewProjectile(Player.GetSource_Misc("FranDashVisual"), Player.Center, DashVelocity,
                ModContent.ProjectileType<FranDashTrail>(), 0, 0f, Player.whoAmI, DashDuration);
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
                new Color(38, 22, 58), 1.20f);
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
                int damage = (int)(FranSkillInfo.BaseDamage * evolution.DashImpactDamageMultiplier);
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
            ApplyDashRollVisual();
            if (collisionChangedVelocity && Player.whoAmI == Main.myPlayer)
                FranModMain.SendCombatState(Player.whoAmI, DashTimer, DashVelocity);
        }
    }

    private void ApplyDashRollVisual()
    {
        float progress = 1f - (DashTimer - 1f) / Math.Max(1f, DashDuration - 1f);
        float rollDirection = Math.Abs(DashVelocity.X) > 0.05f
            ? Math.Sign(DashVelocity.X)
            : Player.direction == 0 ? 1f : Player.direction;
        Player.fullRotationOrigin = Player.Size * 0.5f;
        Player.fullRotation = rollDirection * MathHelper.TwoPi * 2f * progress * Player.gravDir;
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

    private void EnsureTeacher()
    {
        if ((!FranMode && !teacherBonded) || HasTeacher || Player.whoAmI != Main.myPlayer || teacherRecoveryCooldown > 0)
            return;
        Player.QuickSpawnItem(Player.GetSource_Misc("FranModeTeacher"), ModContent.ItemType<Teacher>());
        if (!HasTeacher)
            teacherRecoveryCooldown = 5 * 60;
    }

    private void EnsureTeacherToolInitialGrant()
    {
        if (teacherToolGranted)
            return;

        int toolType = ModContent.ItemType<TeacherExcavationTool>();
        if (!Player.HasItem(toolType) && Main.mouseItem.type != toolType)
            Player.QuickSpawnItem(Player.GetSource_Misc("FranTeacherToolForm"), toolType);
        teacherToolGranted = true;
    }

    private void RecallTeacherFromStorage()
    {
        int teacherType = ModContent.ItemType<Teacher>();
        bool found = false;

        void ClearStorage(Item[] storage)
        {
            if (storage is null)
                return;
            for (int i = 0; i < storage.Length; i++)
            {
                if (storage[i].type != teacherType)
                    continue;
                storage[i].TurnToAir();
                found = true;
            }
        }

        if (Player.trashItem.type == teacherType)
        {
            Player.trashItem.TurnToAir();
            found = true;
        }

        ClearStorage(Player.bank.item);
        ClearStorage(Player.bank2.item);
        ClearStorage(Player.bank3.item);
        ClearStorage(Player.bank4.item);

        if (Player.chest >= 0 && Player.chest < Main.maxChests && Main.chest[Player.chest] is Chest chest)
        {
            for (int slot = 0; slot < chest.item.Length; slot++)
            {
                if (chest.item[slot].type != teacherType)
                    continue;
                chest.item[slot].TurnToAir();
                found = true;
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    NetMessage.SendData(MessageID.SyncChestItem, -1, -1, null, Player.chest, slot);
            }
        }

        if (found && !HasTeacher)
        {
            teacherRecoveryCooldown = 0;
            Player.QuickSpawnItem(Player.GetSource_Misc("FranTeacherRecall"), teacherType);
        }
    }

    private void EnsureFloatingTeacher()
    {
        int flightVisualType = ModContent.ProjectileType<FranTelekineticFlightVisual>();
        if (!HasTeacher || Player.dead || TelekineticFlightVisualActive ||
            Player.ownedProjectileCounts[flightVisualType] > 0)
            return;

        if (Player.HeldItem.type == ModContent.ItemType<Teacher>() ||
            Player.HeldItem.type == ModContent.ItemType<TeacherExcavationTool>())
            return;

        int type = ModContent.ProjectileType<FloatingTeacherVisual>();
        if (Player.ownedProjectileCounts[type] == 0 && Player.whoAmI == Main.myPlayer)
            Projectile.NewProjectile(Player.GetSource_Misc("FloatingTeacher"), Player.Center, Vector2.Zero, type, 0, 0f, Player.whoAmI);
    }

    private bool HasFavoritedHeadwear()
    {
        int type = ModContent.ItemType<FransHeadwear>();
        for (int i = 0; i < 50; i++)
            if (Player.inventory[i].type == type && Player.inventory[i].favorited)
                return true;
        return false;
    }

    private static bool IsFranName(string name) =>
        string.Equals(name?.Trim(), "Fran", StringComparison.OrdinalIgnoreCase) || name?.Trim() is "フラン" or "弗兰";

    private static Color LitColor(Color baseColor, Color environmentLight) =>
        new(baseColor.ToVector3() * environmentLight.ToVector3());

    private void ShowIntroduction()
    {
        if (introShown || Player.whoAmI != Main.myPlayer)
            return;
        introShown = true;
        Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroTitle"), new Color(145, 225, 255));
        Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroCombat"), Color.White);
        Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroSkills"), new Color(185, 215, 255));
        Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroEvolution"), new Color(205, 178, 245));
        Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroDash"), new Color(185, 215, 255));
        Main.NewText(Language.GetTextValue("Mods.Fran.Notices.IntroPotential"), new Color(225, 235, 255));
    }

    internal bool TryStartUltimate(Vector2 requestedFocus)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !FranMode || Player.dead || !UltimateReady ||
            UltimateActive)
            return false;

        if (!float.IsFinite(requestedFocus.X) || !float.IsFinite(requestedFocus.Y))
            requestedFocus = Player.Center + Vector2.UnitX * Player.direction * 480f;
        Vector2 offset = requestedFocus - Player.Center;
        if (offset.LengthSquared() > 1800f * 1800f)
            requestedFocus = Player.Center + offset.SafeNormalize(Vector2.UnitX * Player.direction) * 1800f;

        int teacherType = ModContent.ItemType<Teacher>();
        Item teacher = Player.HeldItem.type == teacherType ? Player.HeldItem : null;
        if (teacher is null)
            for (int i = 0; i < Player.inventory.Length; i++)
                if (Player.inventory[i].type == teacherType)
                {
                    teacher = Player.inventory[i];
                    break;
                }
        int teacherDamage = teacher is null
            ? Math.Max(1, (int)Player.GetDamage(DamageClass.Melee).ApplyTo(FranSkillInfo.BaseDamage))
            : Math.Max(1, Player.GetWeaponDamage(teacher));
        int strikeDamage = Math.Max(1, (int)(teacherDamage *
            FranBalance.GetFloat(nameof(FranBalance.UltimateStrikeDamageMultiplier),
                FranBalance.UltimateStrikeDamageMultiplier)));

        if (DashTimer > 0)
            EndDash(sync: false);
        int projectileIndex = Projectile.NewProjectile(Player.GetSource_Misc("FranUltimate"), Player.Center,
            Vector2.Zero, ModContent.ProjectileType<FranUltimateController>(), strikeDamage, 8f,
            Player.whoAmI, requestedFocus.X, requestedFocus.Y);
        if (!Main.projectile.IndexInRange(projectileIndex))
            return false;

        UltimateChargeTicks = 0;
        ultimateChargeSyncTimer = 0;
        ultimateReadyAnnounced = false;
        if (Main.netMode == NetmodeID.Server)
            FranModMain.SendUltimateCharge(Player.whoAmI, 0);
        return true;
    }

    private void AnnounceUltimateReady()
    {
        if (ultimateReadyAnnounced)
            return;
        ultimateReadyAnnounced = true;
        Projectile.NewProjectile(Player.GetSource_Misc("FranUltimateReady"), Player.Center, Vector2.Zero,
            ModContent.ProjectileType<FranUltimateReadySigil>(), 0, 0f, Player.whoAmI);
    }

    public void SetUltimateGaugeAnchor(Vector2 normalizedAnchor) =>
        UltimateGaugeAnchor = new Vector2(MathHelper.Clamp(normalizedAnchor.X, 0f, 1f),
            MathHelper.Clamp(normalizedAnchor.Y, 0f, 1f));
}
