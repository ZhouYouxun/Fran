using System;
using System.Collections.Generic;
using System.Reflection;
using FranMod.Content.Buffs;
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
    public const int DashDuration = 12;
    public const int UltimateChargeDuration = 3 * 60 * 60;
    private const int DashCooldownNormal = 50;

    public bool FranMode { get; private set; }
    public FranSkill SelectedSkill = FranSkill.None;
    public int UltimateChargeTicks;
    public Vector2 UltimateGaugeAnchor = new(0.5f, 0.075f);
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

    private readonly HashSet<int> dashHitNpcs = new();
    private static readonly Dictionary<Type, PropertyInfo> ExternalDashIdProperties = new();
    private bool introShown;
    private bool wasFranMode;
    private bool networkFranMode;
    private bool teacherBonded;
    private bool restoreTeacherAfterRespawn;
    private bool dashStartedThisTick;
    private int teacherRecoveryCooldown;
    private float screenShakePower;
    private int ultimateChargeSyncTimer;
    private bool ultimateReadyAnnounced;

    public bool HasTeacher
    {
        get
        {
            int type = ModContent.ItemType<Teacher>();
            return Player.HasItem(type) || Player.whoAmI == Main.myPlayer && Main.mouseItem.type == type;
        }
    }
    public bool HasSelectedSkill => FranSkillInfo.IsRealSkill(SelectedSkill);
    public bool HasExternalDash => Player.dashType != 0 || Player.dashDelay != 0 || HasNamedExternalDash();
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
                if (property.GetValue(modPlayer) is string id && !string.IsNullOrWhiteSpace(id))
                    return true;
            }
            catch
            {
                // A third-party compatibility probe must never break Fran's own movement update.
            }
        }
        return false;
    }

    public override void Initialize()
    {
        SelectedSkill = FranSkill.None;
    }

    public override void SaveData(TagCompound tag)
    {
        tag["SelectedSkillV2"] = (int)SelectedSkill;
        if (UltimateChargeTicks > 0)
            tag["UltimateChargeTicks"] = Math.Clamp(UltimateChargeTicks, 0, UltimateChargeDuration);
        if (Vector2.DistanceSquared(UltimateGaugeAnchor, new Vector2(0.5f, 0.075f)) > 0.000001f)
        {
            tag["UltimateGaugeX"] = UltimateGaugeAnchor.X;
            tag["UltimateGaugeY"] = UltimateGaugeAnchor.Y;
        }
        if (introShown)
            tag["IntroShown"] = true;
        if (teacherBonded)
            tag["TeacherBonded"] = true;
    }

    public override void LoadData(TagCompound tag)
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
        SelectedSkill = value >= 0 && value < FranSkillInfo.Count ? (FranSkill)value : FranSkill.None;
        UltimateChargeTicks = Math.Clamp(tag.GetInt("UltimateChargeTicks"), 0, UltimateChargeDuration);
        float gaugeX = tag.ContainsKey("UltimateGaugeX") ? tag.GetFloat("UltimateGaugeX") : 0.5f;
        float gaugeY = tag.ContainsKey("UltimateGaugeY") ? tag.GetFloat("UltimateGaugeY") : 0.075f;
        UltimateGaugeAnchor = new Vector2(MathHelper.Clamp(gaugeX, 0f, 1f),
            MathHelper.Clamp(gaugeY, 0f, 1f));
        ultimateReadyAnnounced = UltimateReady;
        introShown = tag.GetBool("IntroShown");
        teacherBonded = tag.GetBool("TeacherBonded");
    }

    public override void ResetEffects()
    {
        bool localAuthority = Main.netMode == NetmodeID.SinglePlayer || Player.whoAmI == Main.myPlayer;
        FranMode = IsFranName(Player.name) || (localAuthority ? HasFavoritedHeadwear() : networkFranMode);
        if (!FranMode)
            return;

        Player.AddBuff(ModContent.BuffType<BlackCatWarriorBuff>(), 2);
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
        if (Player.whoAmI != Main.myPlayer || screenShakePower <= 0.05f)
            return;

        Main.screenPosition += Main.rand.NextVector2Circular(screenShakePower, screenShakePower);
        screenShakePower *= 0.82f;
    }

    public override void OnEnterWorld()
    {
        EnsureTeacher();
    }

    public override void CopyClientState(ModPlayer targetCopy)
    {
        ((FranPlayer)targetCopy).FranMode = FranMode;
    }

    public override void SendClientChanges(ModPlayer clientPlayer)
    {
        if (((FranPlayer)clientPlayer).FranMode != FranMode)
            FranModMain.SendFranMode(Player.whoAmI, FranMode);
    }

    public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
    {
        FranModMain.SendFranMode(Player.whoAmI, FranMode, toWho, fromWho);
        FranModMain.SendCombatState(Player.whoAmI, DashTimer, DashVelocity, toWho, fromWho);
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

    public override void PostUpdate()
    {
        if (Player.dead)
            return;

        if (FranMode || HasTeacher)
            teacherBonded = true;
        if (Player.whoAmI == Main.myPlayer && teacherBonded)
        {
            RecallTeacherFromStorage();
            if (teacherRecoveryCooldown > 0)
                teacherRecoveryCooldown--;
            EnsureTeacher();
        }

        if (!FranMode)
        {
            wasFranMode = false;
            if (DashTimer > 0 || DashVelocity != Vector2.Zero)
                EndDash(sync: Player.whoAmI == Main.myPlayer);
            if (HasTeacher)
            {
                if (HasSelectedSkill && !FranSkillInfo.IsUnlocked(SelectedSkill))
                    SelectedSkill = FranSkill.None;
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

        if (HasSelectedSkill && !FranSkillInfo.IsUnlocked(SelectedSkill))
            SelectedSkill = FranSkill.None;
        UpdateTimers();
        MaintainSelectedImprint();
        UpdateUltimateCharge();
        UpdateInputs();
        UpdateDash();
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
        if (!UltimateActive)
            return;
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

    public override bool CanUseItem(Item item) => !UltimateActive;

    public override void ModifyHurt(ref Player.HurtModifiers modifiers)
    {
        if (!FranMode)
            return;
        if (SelectedSkill == FranSkill.MagicBarrier && FranAutoBarrierProjectile.IsActive(Player))
            modifiers.SourceDamage *= 0.72f;
        modifiers.DisableSound();
        SoundEngine.PlaySound(SoundID.PlayerHit with { Pitch = 0.28f, Volume = 0.72f }, Player.Center);
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        if (!FranMode || SelectedSkill != FranSkill.MagicBarrier)
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

    public void SelectSkill(FranSkill skill)
    {
        if (skill != FranSkill.None && (!FranSkillInfo.IsRealSkill(skill) || !FranSkillInfo.IsUnlocked(skill)))
            return;
        FranSkill previous = SelectedSkill;
        if (previous != skill)
        {
            if (previous == FranSkill.ThorsHammer)
                HammerComboRounds = 0;
        }
        SelectedSkill = skill;
        SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.2f, Volume = 0.65f }, Player.Center);
        if (Main.myPlayer == Player.whoAmI && skill != FranSkill.None)
            CombatText.NewText(Player.Hitbox, FranSkillInfo.Color(skill), FranSkillInfo.Name(skill));
        else if (Main.myPlayer == Player.whoAmI)
            CombatText.NewText(Player.Hitbox, new Color(190, 205, 215),
                Language.GetTextValue("Mods.Fran.UI.PureSwordsmanship"));
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
        if (HammerComboRounds < 2)
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
        ResonanceHealCooldown = 180;
        return true;
    }

    public void ShakeScreen(Vector2 source, float power, float maxDistance = 1400f)
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer || power <= 0f)
            return;

        float falloff = 1f - MathHelper.Clamp(Vector2.Distance(Player.Center, source) / maxDistance, 0f, 1f);
        screenShakePower = Math.Max(screenShakePower, power * falloff);
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
        if (Player.whoAmI != Main.myPlayer || Player.dead || SelectedSkill != FranSkill.MagicBarrier ||
            !IsImprintReady(FranSkill.MagicBarrier))
            return;
        int barrierType = ModContent.ProjectileType<FranAutoBarrierProjectile>();
        if (Player.ownedProjectileCounts[barrierType] <= 0)
            Projectile.NewProjectile(Player.GetSource_Misc("FranBarrierImprint"), Player.MountedCenter,
                Vector2.Zero, barrierType, Math.Max(1, (int)(Player.GetWeaponDamage(Player.HeldItem) * 0.72f)), 3f,
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

        UltimateChargeTicks++;
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
        if (canDash && FranModMain.DashKey.JustPressed)
        {
            StartDash(ResolveManualDashDirection());
            return;
        }

        if (canDash && Player.controlLeft && Player.releaseLeft && Player.doubleTapCardinalTimer[3] < 15)
            StartDash(-Vector2.UnitX);
        else if (canDash && Player.controlRight && Player.releaseRight && Player.doubleTapCardinalTimer[2] < 15)
            StartDash(Vector2.UnitX);
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
        float dashSpeed = 16f;
        DashTimer = DashDuration;
        DashCooldown = Player.GetModPlayer<FranEvolutionPlayer>().HasNode(FranEvolutionNode.CatStep)
            ? 42
            : DashCooldownNormal;
        DashVelocity = direction * dashSpeed;
        Player.velocity = DashVelocity;
        if (Math.Abs(direction.X) > 0.05f)
            Player.ChangeDir(Math.Sign(direction.X));
        dashStartedThisTick = true;
        dashHitNpcs.Clear();
        FranModMain.SendCombatState(Player.whoAmI, DashTimer, DashVelocity);
        Player.GiveImmuneTimeForCollisionAttack(DashDuration + 2);
        SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.18f, Volume = 0.65f }, Player.Center);
        ShakeScreen(Player.Center, 1.8f);
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
        if (!Main.dedServ)
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
                int damage = (int)(FranSkillInfo.BaseDamage * 1.2f);
                FranBurst.Spawn(Player.GetSource_Misc("FranDash"), npc.Center, Player.whoAmI,
                    FranBurstStyle.DashImpact, 1f, damage, 6f);
                Player.GetModPlayer<FranEvolutionPlayer>().ApplyDashHit(npc);
            }
        }

        if (!justStarted && DashTimer > 0)
            DashTimer--;

        if (DashTimer <= 0)
            EndDash(sync: Player.whoAmI == Main.myPlayer);
        else if (collisionChangedVelocity && Player.whoAmI == Main.myPlayer)
            FranModMain.SendCombatState(Player.whoAmI, DashTimer, DashVelocity);
    }

    private void EndDash(bool sync)
    {
        DashTimer = 0;
        DashVelocity = Vector2.Zero;
        dashStartedThisTick = false;
        dashHitNpcs.Clear();
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
        if (!HasTeacher || Player.dead)
            return;

        if (Player.HeldItem.type == ModContent.ItemType<Teacher>())
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
        int strikeDamage = Math.Max(1, (int)(teacherDamage * 1.15f));

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
