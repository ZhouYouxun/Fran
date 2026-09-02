using System;
using System.Collections.Generic;
using FranMod.Content.Players;
using FranMod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace FranMod.Content.Projectiles;

public sealed class FranTelekineticFlightVisual : ModProjectile
{
    private const int HistoryLength = 12;
    private readonly Vector2[] positionHistory = new Vector2[HistoryLength];
    private readonly float[] rotationHistory = new float[HistoryLength];
    private readonly DirectionalStroke[] finStrokes = new DirectionalStroke[7];
    private readonly DirectionalStroke[] ribbonStrokes = new DirectionalStroke[3];
    private float opacity;
    private float visualAge;
    private float finSpawnDistance;
    private float ribbonSpawnDistance;
    private float directionalVisibility;
    private float hoverIntensity;
    private float risingIntensity;
    private float fallingIntensity;
    private Vector2 effectCenter;
    private Vector2 smoothedFlowVelocity;
    private Vector2 flowDirection = Vector2.UnitX;
    private int nextFinStroke;
    private int nextRibbonStroke;
    private bool historyInitialized;
    private bool effectCenterInitialized;

    private struct DirectionalStroke
    {
        public bool Active;
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public float Age;
        public float Lifetime;
        public float Scale;
        public float Strength;
    }

    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.None}";

    public override void SetDefaults()
    {
        Projectile.width = 2;
        Projectile.height = 2;
        Projectile.timeLeft = 2;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.netImportant = true;
        Projectile.hide = true;
    }

    public override bool? CanDamage() => false;

    public override bool ShouldUpdatePosition() => false;

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
        List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
    {
        behindNPCs.Add(index);
    }

    public override void AI()
    {
        if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
        {
            Projectile.Kill();
            return;
        }

        Player owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead)
        {
            Projectile.Kill();
            return;
        }

        FranPlayer fran = owner.GetModPlayer<FranPlayer>();
        bool active = fran.TelekineticFlightVisualActive;
        visualAge++;
        if (!effectCenterInitialized)
        {
            effectCenter = GetTeacherCenter(owner);
            int initialFacing = owner.direction == 0 ? 1 : owner.direction;
            flowDirection = Vector2.UnitX * initialFacing;
            effectCenterInitialized = true;
        }

        UpdateFlowMomentum(owner, active);
        UpdateFlightState(owner, active);
        if (active)
        {
            effectCenter = GetTeacherCenter(owner);
        }
        else
        {
            // Once flight ends, the visual system keeps its residual velocity and decelerates
            // instead of remaining glued to the player while merely fading its alpha.
            effectCenter += smoothedFlowVelocity * 0.55f;
        }

        Projectile.Center = effectCenter;
        UpdateHistory(effectCenter, GetTeacherRotation());
        UpdateDirectionalStrokes(owner, active);

        if (active)
        {
            Projectile.timeLeft = 2;
            opacity = MathHelper.Lerp(opacity, 1f, 0.22f);
        }
        else
        {
            opacity *= 0.82f;
            if (opacity < 0.025f)
            {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 2;
        }

        Lighting.AddLight(effectCenter, 0.12f * opacity, 0.27f * opacity, 0.45f * opacity);
        if (!Main.dedServ && active && directionalVisibility > 0.12f && Main.GameUpdateCount % 3 == 0)
        {
            Vector2 normal = flowDirection.RotatedBy(MathHelper.PiOver2);
            Vector2 center = effectCenter - flowDirection * Main.rand.NextFloat(8f, 38f) +
                normal * Main.rand.NextFloat(-5f, 5f);
            Vector2 velocity = -flowDirection * Main.rand.NextFloat(0.45f, 1.15f) -
                smoothedFlowVelocity * 0.035f + normal * Main.rand.NextFloat(-0.16f, 0.16f);
            Dust dust = Dust.NewDustPerfect(center, DustID.BlueTorch,
                velocity, 110, new Color(115, 210, 255), Main.rand.NextFloat(0.7f, 1.05f));
            dust.noGravity = true;
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Player owner = Main.player[Projectile.owner];
        if (!owner.active || opacity <= 0.01f)
            return false;

        Texture2D fin = ModContent.Request<Texture2D>("Fran/Assets/Flight/TelekineticFlightFin").Value;
        Texture2D halo = ModContent.Request<Texture2D>("Fran/Assets/Flight/TelekineticFlightHalo").Value;
        Texture2D ribbon = ModContent.Request<Texture2D>("Fran/Assets/Flight/TelekineticFlightRibbon").Value;
        Texture2D teacher = FranTeacherVisuals.Texture;

        Vector2 teacherCenter = effectCenterInitialized ? effectCenter : GetTeacherCenter(owner);
        float bladeRotation = GetTeacherRotation();
        float pulse = 0.5f + 0.5f * MathF.Sin(visualAge * 0.17f);
        bool dashing = owner.GetModPlayer<FranPlayer>().DashTimer > 0;
        float intensity = opacity * (dashing ? 1.22f : 1f);
        float stateScale = 1f + hoverIntensity * 0.13f + risingIntensity * 0.07f - fallingIntensity * 0.04f;

        FranVfx.BeginAdditive();

        Main.EntitySpriteDraw(halo, teacherCenter - Main.screenPosition + Vector2.UnitY * owner.gravDir * 3f,
            null, Color.White * (0.48f * intensity), visualAge * 0.012f * owner.gravDir,
            halo.Size() * 0.5f, (0.23f + pulse * 0.018f) * stateScale, SpriteEffects.None, 0f);
        Main.EntitySpriteDraw(halo, teacherCenter - Main.screenPosition + Vector2.UnitY * owner.gravDir * 3f,
            null, new Color(120, 185, 255) * (0.22f * intensity), -visualAge * 0.009f * owner.gravDir,
            halo.Size() * 0.5f, (0.31f + pulse * 0.025f) * stateScale, SpriteEffects.None, 0f);

        // Hover is intentionally built from directionless blooms and a ring. Directional streaks
        // stop spawning at low speed, so no arrow-shaped texture has to fake motion while stationary.
        if (hoverIntensity > 0.02f)
        {
            int moteCount = FranTelekineticFlightSystem.CurrentTier switch
            {
                <= 1 => 3,
                2 => 5,
                _ => 7
            };
            float orbitRadius = 18f + hoverIntensity * 8f;
            for (int i = 0; i < moteCount; i++)
            {
                float angle = MathHelper.TwoPi * i / moteCount + visualAge * 0.025f * owner.gravDir;
                Vector2 orbit = new Vector2(MathF.Cos(angle) * orbitRadius,
                    MathF.Sin(angle) * orbitRadius * 0.38f);
                FranVfx.DrawBloom(teacherCenter + orbit, new Color(115, 205, 255),
                    4.5f + pulse * 1.5f, hoverIntensity * 0.32f * intensity, hard: true);
            }
            FranVfx.DrawRing(teacherCenter, new Color(100, 190, 255), 28f + pulse * 3f,
                hoverIntensity * 0.22f * intensity, visualAge * -0.018f * owner.gravDir);
        }

        for (int i = 0; i < finStrokes.Length; i++)
        {
            DirectionalStroke stroke = finStrokes[i];
            if (!stroke.Active)
                continue;
            float life = stroke.Age / stroke.Lifetime;
            float strokeOpacity = FlowEnvelope(life) * stroke.Strength * opacity;
            Main.EntitySpriteDraw(fin, stroke.Position - Main.screenPosition, null,
                Color.White * (strokeOpacity * 0.42f), stroke.Rotation, fin.Size() * 0.5f,
                new Vector2(stroke.Scale * (0.92f + life * 0.14f), stroke.Scale * 0.88f),
                SpriteEffects.None, 0f);
        }

        for (int i = 0; i < ribbonStrokes.Length; i++)
        {
            DirectionalStroke stroke = ribbonStrokes[i];
            if (!stroke.Active)
                continue;
            float life = stroke.Age / stroke.Lifetime;
            float strokeOpacity = FlowEnvelope(life) * stroke.Strength * opacity;
            float ribbonLength = MathHelper.Clamp(stroke.Scale * (0.90f + life * 0.20f), 0.34f, 0.78f);
            Main.EntitySpriteDraw(ribbon, stroke.Position - Main.screenPosition, null,
                Color.White * (strokeOpacity * 0.50f), stroke.Rotation, ribbon.Size() * 0.5f,
                new Vector2(ribbonLength, 0.36f + life * 0.07f), SpriteEffects.None, 0f);
        }

        FranVfx.DrawBloom(teacherCenter, new Color(80, 175, 255), 35f + pulse * 6f,
            (0.24f + directionalVisibility * 0.10f) * intensity);
        FranVfx.DrawBloom(teacherCenter, Color.White, 13f + pulse * 3f, 0.25f * intensity, hard: true);
        FranVfx.EndAdditive();

        Color afterimageColor = new Color(105, 190, 255);
        for (int i = HistoryLength - 1; i >= 3; i -= 3)
        {
            if (positionHistory[i] == Vector2.Zero)
                continue;
            float trailOpacity = (1f - i / (float)HistoryLength) * 0.18f * opacity;
            Main.EntitySpriteDraw(teacher, positionHistory[i] - Main.screenPosition, null,
                afterimageColor * trailOpacity, rotationHistory[i], teacher.Size() * 0.5f,
                0.88f * FranTeacherVisuals.TextureScale,
                SpriteEffects.None, 0f);
        }

        Color environmentLight = Lighting.GetColor((int)(teacherCenter.X / 16f), (int)(teacherCenter.Y / 16f));
        Color bladeColor = Color.Lerp(environmentLight, Color.White, 0.34f);
        Main.EntitySpriteDraw(teacher, teacherCenter - Main.screenPosition, null, bladeColor * opacity,
            bladeRotation, teacher.Size() * 0.5f,
            0.92f * FranTeacherVisuals.TextureScale, SpriteEffects.None, 0f);
        return false;
    }

    private void UpdateFlowMomentum(Player owner, bool active)
    {
        Vector2 targetVelocity = active ? owner.velocity : Vector2.Zero;
        float currentSpeed = smoothedFlowVelocity.Length();
        float targetSpeed = targetVelocity.Length();
        float response = targetSpeed > currentSpeed ? 0.16f : 0.075f;
        smoothedFlowVelocity = Vector2.Lerp(smoothedFlowVelocity, targetVelocity, response);
        if (smoothedFlowVelocity.LengthSquared() < 0.0025f)
            smoothedFlowVelocity = Vector2.Zero;

        float speed = smoothedFlowVelocity.Length();
        if (speed > 0.12f)
        {
            Vector2 targetDirection = smoothedFlowVelocity / speed;
            if (speed < 0.75f)
                flowDirection = targetDirection;
            else
            {
                float currentAngle = flowDirection.ToRotation();
                float angleDifference = MathHelper.WrapAngle(targetDirection.ToRotation() - currentAngle);
                float maxTurn = MathHelper.Lerp(0.045f, 0.085f,
                    MathHelper.Clamp(speed / 12f, 0f, 1f));
                flowDirection = (currentAngle + MathHelper.Clamp(angleDifference, -maxTurn, maxTurn))
                    .ToRotationVector2();
            }
        }

        float speedVisibility = MathHelper.SmoothStep(0f, 1f,
            MathHelper.Clamp((speed - 0.35f) / 2.65f, 0f, 1f));
        float alignmentVisibility = 1f;
        if (active && owner.velocity.LengthSquared() > 0.25f)
        {
            Vector2 actualDirection = owner.velocity.SafeNormalize(flowDirection);
            float alignment = Vector2.Dot(flowDirection, actualDirection);
            alignmentVisibility = MathHelper.SmoothStep(0f, 1f,
                MathHelper.Clamp((alignment - 0.10f) / 0.75f, 0f, 1f));
        }

        float targetVisibility = speedVisibility * alignmentVisibility;
        directionalVisibility = MathHelper.Lerp(directionalVisibility, targetVisibility,
            targetVisibility < directionalVisibility ? 0.34f : 0.16f);

    }

    private void UpdateFlightState(Player owner, bool active)
    {
        float gravityVelocity = owner.velocity.Y * owner.gravDir;
        float hoverTarget = active && owner.controlJump && owner.controlDown ? 1f : 0f;
        float risingTarget = active && owner.controlJump && gravityVelocity < -0.45f ? 1f : 0f;
        float fallingTarget = active && !owner.controlJump && gravityVelocity > 0.8f ? 1f : 0f;
        hoverIntensity = MathHelper.Lerp(hoverIntensity, hoverTarget, hoverTarget > hoverIntensity ? 0.18f : 0.11f);
        risingIntensity = MathHelper.Lerp(risingIntensity, risingTarget, risingTarget > risingIntensity ? 0.16f : 0.10f);
        fallingIntensity = MathHelper.Lerp(fallingIntensity, fallingTarget, fallingTarget > fallingIntensity ? 0.16f : 0.10f);
    }

    private void UpdateDirectionalStrokes(Player owner, bool active)
    {
        UpdateStrokeArray(finStrokes);
        UpdateStrokeArray(ribbonStrokes);

        float speed = smoothedFlowVelocity.Length();
        if (!active || directionalVisibility < 0.18f || speed < 0.55f)
            return;
        if (owner.velocity.LengthSquared() > 0.25f &&
            Vector2.Dot(flowDirection, owner.velocity.SafeNormalize(flowDirection)) < 0.75f)
            return;

        int finCapacity = FranTelekineticFlightSystem.CurrentTier switch
        {
            <= 1 => 3,
            2 => 5,
            _ => 7
        };
        float finSpacing = FranTelekineticFlightSystem.CurrentTier switch
        {
            <= 1 => 20f,
            2 => 15f,
            _ => 11f
        };
        finSpawnDistance += speed;
        if (!HasActiveStroke(finStrokes, finCapacity))
            finSpawnDistance = Math.Max(finSpawnDistance, finSpacing);
        while (finSpawnDistance >= finSpacing)
        {
            finSpawnDistance -= finSpacing;
            if (!TrySpawnStroke(finStrokes, finCapacity, ref nextFinStroke, speed, isRibbon: false))
            {
                finSpawnDistance = Math.Min(finSpawnDistance + finSpacing, finSpacing);
                break;
            }
        }

        if (speed < 2.2f)
            return;
        int ribbonCapacity = speed > 11f ? 3 : 2;
        float ribbonSpacing = speed > 11f ? 25f : 34f;
        ribbonSpawnDistance += speed;
        if (!HasActiveStroke(ribbonStrokes, ribbonCapacity))
            ribbonSpawnDistance = Math.Max(ribbonSpawnDistance, ribbonSpacing);
        while (ribbonSpawnDistance >= ribbonSpacing)
        {
            ribbonSpawnDistance -= ribbonSpacing;
            if (!TrySpawnStroke(ribbonStrokes, ribbonCapacity, ref nextRibbonStroke, speed, isRibbon: true))
            {
                ribbonSpawnDistance = Math.Min(ribbonSpawnDistance + ribbonSpacing, ribbonSpacing);
                break;
            }
        }
    }

    private bool TrySpawnStroke(DirectionalStroke[] strokes, int capacity, ref int cursor, float speed,
        bool isRibbon)
    {
        int slot = -1;
        for (int attempt = 0; attempt < capacity; attempt++)
        {
            int candidate = (cursor + attempt) % capacity;
            if (strokes[candidate].Active)
                continue;
            slot = candidate;
            cursor = candidate + 1;
            break;
        }
        if (slot < 0)
            return false;

        float centerIndex = slot - (capacity - 1) * 0.5f;
        float fanAngle = centerIndex * (isRibbon ? 0.018f : 0.035f);
        Vector2 axis = flowDirection.RotatedBy(fanAngle);
        Vector2 normal = axis.RotatedBy(MathHelper.PiOver2);
        float laneOffset = centerIndex * (isRibbon ? 1.2f : 2.1f);
        float retainedSpeed = speed * (isRibbon ? 0.48f : 0.62f);
        retainedSpeed = Math.Max(retainedSpeed, isRibbon ? 1.1f : 1.35f);

        // The generated streaks have their perceived leading edge on local -X. Rotating by PI
        // maps that edge onto the emitted world velocity. Once spawned, rotation never follows
        // the player again: an old horizontal stroke keeps horizontal momentum through a turn.
        strokes[slot] = new DirectionalStroke
        {
            Active = true,
            Position = effectCenter - axis * (isRibbon ? 7f : 4f) + normal * laneOffset,
            Velocity = axis * retainedSpeed,
            Rotation = axis.ToRotation() + MathHelper.Pi,
            Age = 0f,
            Lifetime = isRibbon ? 21f : 25f,
            Scale = isRibbon
                ? MathHelper.Clamp(0.34f + speed * 0.018f, 0.36f, 0.65f)
                : MathHelper.Clamp(0.115f + speed * 0.0025f, 0.12f, 0.16f),
            Strength = directionalVisibility
        };
        return true;
    }

    private static void UpdateStrokeArray(DirectionalStroke[] strokes)
    {
        for (int i = 0; i < strokes.Length; i++)
        {
            if (!strokes[i].Active)
                continue;

            DirectionalStroke stroke = strokes[i];
            float life = stroke.Age / stroke.Lifetime;
            // Acceleration and braking are scalar changes along the stored axis. No lateral force
            // or player turn is allowed to rotate a directional texture after emission.
            float brakingBlend = MathHelper.SmoothStep(0f, 1f,
                MathHelper.Clamp((life - 0.10f) / 0.25f, 0f, 1f));
            stroke.Velocity *= MathHelper.Lerp(1.018f, 0.955f, brakingBlend);
            stroke.Position += stroke.Velocity;
            stroke.Age++;
            if (stroke.Age >= stroke.Lifetime)
                stroke.Active = false;
            strokes[i] = stroke;
        }
    }

    private static bool HasActiveStroke(DirectionalStroke[] strokes, int capacity)
    {
        for (int i = 0; i < capacity; i++)
            if (strokes[i].Active)
                return true;
        return false;
    }

    private static float FlowEnvelope(float phase)
    {
        float fadeIn = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(phase / 0.16f, 0f, 1f));
        float fadeOut = 1f - MathHelper.SmoothStep(0f, 1f,
            MathHelper.Clamp((phase - 0.62f) / 0.38f, 0f, 1f));
        return fadeIn * fadeOut;
    }

    private float GetTeacherRotation()
    {
        return MathHelper.PiOver4 +
            MathHelper.Clamp(smoothedFlowVelocity.Y * 0.009f, -0.13f, 0.13f);
    }

    private void UpdateHistory(Vector2 center, float rotation)
    {
        if (!historyInitialized)
        {
            for (int i = 0; i < positionHistory.Length; i++)
            {
                positionHistory[i] = center;
                rotationHistory[i] = rotation;
            }
            historyInitialized = true;
            return;
        }

        for (int i = positionHistory.Length - 1; i > 0; i--)
        {
            positionHistory[i] = positionHistory[i - 1];
            rotationHistory[i] = rotationHistory[i - 1];
        }
        positionHistory[0] = center;
        rotationHistory[0] = rotation;
    }

    private static Vector2 GetTeacherCenter(Player owner) => GetTeacherCenter(owner, owner.Center);

    private static Vector2 GetTeacherCenter(Player owner, Vector2 playerCenter)
    {
        return playerCenter + Vector2.UnitY * owner.gravDir * (owner.height * 0.5f + 9f);
    }
}
