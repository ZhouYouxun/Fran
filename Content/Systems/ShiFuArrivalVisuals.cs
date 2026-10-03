using System;
using System.Collections.Generic;
using FranMod.Content.Players;
using FranMod.Content.Projectiles;
using FranMod.Core;
using FranMod.Core.Graphics;
using FranMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace FranMod.Content.Systems;

[Autoload(Side = ModSide.Client)]
public sealed class ShiFuArrivalVisuals : ModSystem
{
    private sealed class Presentation
    {
        public Vector2 Landing;
        public int Age;
        public ulong LastObserved;
        public FranFissionArc SkyArc;
    }
    private static readonly Dictionary<int, Presentation> scenes = new();
    private static readonly List<int> expired = new();
    private static ulong lastThunder;
    private static ulong lastImpact;
    private static readonly Color Cyan = new(96, 205, 255);
    private static readonly Color WhiteBlue = new(206, 242, 255);

    public override void OnWorldUnload() => Reset();
    public override void Unload() => Reset();
    private static void Reset()
    {
        scenes.Clear();
        expired.Clear();
        lastThunder = lastImpact = 0;
    }

    internal static void Update(ShiFuArrivalRecord record)
    {
        if (Main.dedServ || Main.gameMenu || record.Age >= ShiFuArrivalRules.SettledAge)
            return;
        if (!scenes.TryGetValue(record.Id, out Presentation scene))
        {
            scene = new Presentation {
                Landing = record.Landing, Age = record.Age - 1,
                SkyArc = new FranFissionArc(Vector2.UnitY * ShiFuArrivalRules.FallHeight,
                    record.Id * 7919, 5.4f)
            };
            scenes.Add(record.Id, scene);
        }
        int previousAge = scene.Age;
        scene.Age = Math.Max(scene.Age, record.Age);
        scene.LastObserved = Main.GameUpdateCount;
        if (!Nearby(record.Landing))
            return;
        if ((previousAge < 18 && scene.Age >= 18 ||
             previousAge < ShiFuArrivalRules.Windup && scene.Age >= ShiFuArrivalRules.Windup) &&
            (lastThunder == 0 || Main.GameUpdateCount - lastThunder >= 25))
        {
            // Vanilla lightning respects Terraria's intense-visual-effects accessibility option.
            Main.NewLightning();
            lastThunder = Main.GameUpdateCount;
        }
        if (previousAge < ShiFuArrivalRules.LandingAge && scene.Age >= ShiFuArrivalRules.LandingAge)
            Impact(record);
        int fallAge = scene.Age - ShiFuArrivalRules.Windup;
        if (fallAge >= 0 && fallAge < ShiFuArrivalRules.FallFrames && fallAge % 4 == 0)
        {
            Vector2 tip = ShiFuArrivalRules.SwordTip(record.Landing, scene.Age);
            FranFlatLightningEffects.Fracture(tip - Vector2.UnitY * 100f,
                tip + new Vector2(Main.rand.NextFloat(-38f, 38f), -10f), 0.7f,
                FranFlatLightningPalette.ThorsHammer, record.Id * 991 + fallAge, 1.5f, 12);
        }
    }

    private static bool Nearby(Vector2 landing) => Main.player.IndexInRange(Main.myPlayer) &&
        Vector2.DistanceSquared(Main.LocalPlayer.Center, landing) < 1300f * 1300f;

    private static void Impact(ShiFuArrivalRecord record)
    {
        Vector2 point = record.Landing - Vector2.UnitY * 7f;
        FranFlatLightningEffects.Burst(point, 1.1f, FranFlatLightningPalette.ThorsHammer, 7, 210f, true, 28f);
        FranParticleEffects.RingPulse(point, Cyan, new Vector2(18f, 6f),
            new Vector2(210f, 35f), 28, 0f, 0f, 0.8f);
        for (int i = 0; i < 22; i++)
        {
            Vector2 velocity = new Vector2(Main.rand.NextFloat(-5.8f, 5.8f), Main.rand.NextFloat(-6f, -1f));
            Dust dust = Dust.NewDustPerfect(point + new Vector2(Main.rand.NextFloat(-30f, 30f), 0),
                DustID.Stone, velocity, 65, default, Main.rand.NextFloat(0.75f, 1.35f));
            dust.noGravity = false;
        }
        if (lastImpact == 0 || Main.GameUpdateCount - lastImpact >= 15)
        {
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.58f, Pitch = -0.45f, MaxInstances = 2 }, point);
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 }, point);
            if (!Main.DisableIntenseVisualEffects)
                Main.LocalPlayer.GetModPlayer<FranPlayer>().ShakeScreen(point, 3.2f);
            lastImpact = Main.GameUpdateCount;
        }
    }

    public override void PostUpdateEverything()
    {
        expired.Clear();
        foreach ((int id, Presentation scene) in scenes)
        {
            // The spotlight fades to completion even if the sword is picked up during the fade.
            if (scene.LastObserved != Main.GameUpdateCount)
                scene.Age++;
            if (scene.Age >= ShiFuArrivalRules.SettledAge)
                expired.Add(id);
        }
        foreach (int id in expired)
            scenes.Remove(id);
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        layers.Insert(0, new LegacyGameInterfaceLayer("Fran: ShiFu arrival spotlight",
            DrawSpotlight, InterfaceScaleType.Game));
    }

    private static bool DrawSpotlight()
    {
        if (Main.gameMenu || scenes.Count == 0)
            return true;
        float darkness = 0f;
        foreach (Presentation scene in scenes.Values)
            if (Nearby(scene.Landing))
                darkness = Math.Max(darkness, ShiFuArrivalRules.Highlight(scene.Age));
        if (darkness <= 0.001f)
            return true;
        float dim = Main.DisableIntenseVisualEffects ? 0.13f : 0.32f;
        // Before vanilla UI: dim the world, never obscure inventory/tooltips.
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,
            new Rectangle(-Main.screenWidth, -Main.screenHeight, Main.screenWidth * 3, Main.screenHeight * 3),
            Color.Black * (darkness * dim));
        int drawn = 0;
        foreach ((int id, Presentation scene) in scenes)
        {
            float strength = ShiFuArrivalRules.Highlight(scene.Age);
            if (strength <= 0f || !Nearby(scene.Landing) || drawn++ >= 12)
                continue;
            DrawHalo(scene.Landing, strength);
            // Keep the actual planted blade bright above the dim overlay, not a duplicate after pickup.
            if (ShiFuArrivalSystem.Instance.TryGet(id, out ShiFuArrivalRecord record) &&
                Main.item.IndexInRange(record.ItemIndex) && Main.item[record.ItemIndex].active &&
                Main.item[record.ItemIndex].type == ModContent.ItemType<Content.Items.ShiFu>() &&
                Main.item[record.ItemIndex].GetGlobalItem<Content.Items.ShiFuArrivalWorldItem>().ArrivalId == id)
            {
                DrawBlade(scene.Landing, 1f, false);
                if (Vector2.DistanceSquared(Main.LocalPlayer.Center, scene.Landing) < 420f * 420f)
                    Utils.DrawBorderString(Main.spriteBatch, Language.GetTextValue("Mods.Fran.Notices.ShiFuArrivalPickup"),
                        scene.Landing - Main.screenPosition - new Vector2(0f, 142f),
                        WhiteBlue * strength, 0.72f, 0.5f);
            }
        }
        return true;
    }

    internal static void DrawArrival(ShiFuArrivalRecord record)
    {
        if (record.Age < ShiFuArrivalRules.Windup)
        {
            float charge = Math.Clamp(record.Age / (float)ShiFuArrivalRules.Windup, 0f, 1f);
            FranVfx.BeginAdditive();
            Ring(record.Landing - Vector2.UnitY * 6f, 18f + charge * 29f, 0.22f,
                Cyan * (charge * 0.5f), 1.2f);
            FranVfx.EndAdditive();
            return;
        }
        if (record.Age >= ShiFuArrivalRules.LandingAge)
        {
            DrawPlanted(record.Landing, record.Age, 1f);
            return;
        }
        Vector2 tip = ShiFuArrivalRules.SwordTip(record.Landing, record.Age);
        FranVfx.BeginAdditive();
        if (scenes.TryGetValue(record.Id, out Presentation scene))
            scene.SkyArc.Draw(Main.spriteBatch, record.Landing - Vector2.UnitY * ShiFuArrivalRules.FallHeight,
                Cyan, record.Age - ShiFuArrivalRules.Windup + 0.5f, ShiFuArrivalRules.FallFrames + 4, 2f, 0.85f);
        for (int i = 4; i >= 1; i--)
            DrawBlade(ShiFuArrivalRules.SwordTip(record.Landing, record.Age - i * 2), (5 - i) * 0.085f, true);
        FranVfx.EndAdditive();
        DrawBlade(tip, 1f, false);
    }

    internal static void DrawPlanted(Vector2 landing, int age, float opacity)
    {
        DrawHalo(landing, 0.08f + ShiFuArrivalRules.Highlight(age) * 0.75f);
        DrawBlade(landing, opacity, false);
    }

    private static void DrawBlade(Vector2 tip, float opacity, bool ghost)
    {
        Texture2D texture = ghost ? FranShiFuVisuals.GhostTexture : FranShiFuVisuals.Texture;
        // Art points from bottom-left hilt to top-right tip; rotate the tip straight into the ground.
        Vector2 origin = new(texture.Width - 4f, 4f);
        Main.spriteBatch.Draw(texture, tip - Main.screenPosition, null,
            (ghost ? Cyan : Color.White) * opacity, MathHelper.Pi * 0.75f,
            origin, FranShiFuVisuals.NormalizeScaleFor(texture, 0.70f), SpriteEffects.None, 0f);
    }

    private static void DrawHalo(Vector2 landing, float strength)
    {
        FranVfx.BeginAdditive();
        FranVfx.DrawBloom(landing - Vector2.UnitY * 52f, Cyan, 110f, strength * 0.36f);
        FranVfx.DrawBloom(landing - Vector2.UnitY * 26f, WhiteBlue, 40f, strength * 0.42f);
        Ring(landing - Vector2.UnitY * 6f, 54f, 0.22f, Cyan * (strength * 0.72f), 1.8f);
        Ring(landing - Vector2.UnitY * 6f, 72f, 0.22f, WhiteBlue * (strength * 0.3f), 1f);
        FranVfx.EndAdditive();
    }

    private static void Ring(Vector2 center, float radius, float squash, Color color, float width)
    {
        const int points = 40;
        for (int i = 0; i < points; i++)
        {
            float a = MathHelper.TwoPi * i / points;
            float b = MathHelper.TwoPi * (i + 1) / points;
            FranFissionArc.DrawLine(Main.spriteBatch,
                center + new Vector2(MathF.Cos(a), MathF.Sin(a) * squash) * radius,
                center + new Vector2(MathF.Cos(b), MathF.Sin(b) * squash) * radius, color, width);
        }
    }
}
