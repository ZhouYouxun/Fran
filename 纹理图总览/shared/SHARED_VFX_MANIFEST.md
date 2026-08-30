# Fran shared VFX working set

This folder is the isolated, reviewable working set used by Fran's self-contained VFX code.
The mod does not reference the original source folders or any external mod at runtime.
The original 24-file base set was selected from the user's copyright-cleared `纹理图总览`.
The historical `Cal_` and `Red_` runtime names are retained so combat code and save compatibility do not churn.
As of 2026-08-29, every formerly Calamity/Redemption-derived file in this working set has been replaced by a Fran-original variant exported from `Assets/OriginalVFX` masters. The prefixes no longer describe the current image source.

The names below are stable runtime names. `Source` records where the current working copy came from so every file can be replaced later without changing combat code.

| Runtime file | Role | Source |
|---|---|---|
| SlashWide.png | broad melee arc | KsTexture/slash_01.png |
| SlashNarrow.png | narrow melee arc | KsTexture/slash_03.png |
| TraceLong.png | compressed-air and lightning trace | KsTexture/trace_04.png |
| TraceSoft.png | soft sword and dash trace | KsTexture/trace_07.png |
| Twirl.png | rotational energy flow | KsTexture/twirl_02.png |
| WindTwister.png | wind vortex accent | SuperTexturePack/fx_Twister1.png |
| SoftBloom.png | soft additive bloom | kenney_light-masks-1.0/Transparent/circle_a.png |
| HardBloom.png | hard additive bloom | kenney_light-masks-1.0/Transparent/circle_d.png |
| EnergyRing.png | compact energy ring | SuperTexturePack/fx_EnergyBolt7.png |
| Halo.png | large soft ring | KsTexture/circle_02.png |
| NoiseSoft.png | soft modulation noise | kenney_planets/Parts/noise00.png |
| NoiseSharp.png | sharp modulation noise | kenney_planets/Parts/noise15.png |
| WaterCaustics.png | flowing elemental modulation | kenney_light-masks-1.0/Transparent/water_caustics_d.png |
| Wavering.png | directional flow mask | flare/wavering.png |
| Flame.png | flame body | KsTexture/flame_03.png |
| Smoke.png | smoke body | KsTexture/smoke_03.png |
| Spark.png | impact spark | KsTexture/spark_04.png |
| Star.png | impact star | KsTexture/star_03.png |
| LightningBolt.png | main lightning trunk | SuperTexturePack/fx_Lightning5.png |
| LightningBranch.png | branched lightning trunk | SuperTexturePack/fx_Lightning7.png |
| LightningImpact.png | lightning contact flash | SuperTexturePack/fx_LightFlash2.png |
| Crack.png | barrier and impact fracture | SuperTexturePack/fx_Cracks1.png |
| MagicCircle.png | skill and barrier construction | KsTexture/magic_02.png |
| RingStreak.png | telegraph and release ring | kenney_light-masks-1.0/Transparent/circle_rings_b_streaks.png |
| Cal_JudgementWave.png | five-layer expanding crescent body | Fran original: `FranCrescentRelease` variant |
| Cal_StarOfJudgement.png | colored star body and white inner core | Fran original: `FranFlashFourPoint` variant |
| Cal_VerticalSmearLarge.png | broad directional wave underlayer | Fran original: `FranCrescentRelease` variant |
| Cal_HalfStar.png | wing-tip and release streak | Fran original: `FranFlashFourPoint` variant |
| Cal_CircularSmearSmokey.png | counter-rotating compression volume | Fran original: `FranOrbitSmear` variant |
| Cal_TrientCircularSmear.png | short-lived one-third circular blade | Fran original: `FranOrbitSmear` variant |
| Cal_SlashSmear.png | sharp inner arc of the compression finisher | Fran original: `FranCrescentRelease` variant |
| Red_WhiteFlare.png | Lightmass body and afterimage | Fran original: `FranFlashFourPoint` variant |
| Red_HolyGlow2.png | Lightmass hover aura | Fran original: `FranBloomCore` variant |
| Red_Trail4.png | Lightmass dual trail profile | Fran original: `FranSmearForward` variant |
| Cal_BloomCircle.png | soft core and fluorescent bubble body | Fran original: `FranBloomCore` variant |
| Cal_BloomRing.png | expanding release and impact ring | Fran original: `FranBrokenHalo` variant |
| Cal_ThinEndedLine.png | tapered textured spark and lightning segment | Fran original: `FranTaperedFilament` variant |
| Cal_BloomLineSoftEdge.png | broad soft barrier and beam layer | Fran original: `FranTaperedFilament` variant |
| Cal_BloomLineThick.png | compact bright barrier and beam layer | Fran original: `FranTaperedFilament` variant |
| Cal_ForwardSmear.png | compressed projectile head and thrust wake | Fran original: `FranSmearForward` variant |
| Cal_SemiCircularSmearSwipe.png | broad melee and void half-arc | Fran original: `FranOrbitSmear` variant |
| Cal_CircularSmear.png | rotational lightning, poison and charge shell | Fran original: `FranOrbitSmear` variant |
| Cal_PulseStar.png | overexposed magical star core | Fran original: `FranFlashFourPoint` variant |
| Cal_SmallBloomRingLayered.png | compact bubble and orbit ring | Fran original: `FranBrokenHalo` variant |
| Cal_GlowBlade.png | large luminous slash and air blade | Fran original: `FranCrescentRelease` variant |
| Cal_HighResHollowCircle.png | Nadir barrier rim and Xyk-style death pulse | Fran original: `FranBrokenHalo` variant |
| Cal_HighResFoggyCircleHardEdge.png | SeasSearing-style compressed pressure shell | Fran original: `FranBloomCore` variant |
| Cal_DrainLineBloom2.png | segmented Leonid constellation trail | Fran original: `FranTaperedFilament` variant |
| Cal_DrainLineBloom.png | narrow AzureThunder-style velocity filament | Fran original: `FranTaperedFilament` variant |
| Cal_BloomLineCap.png | capped constellation and beam endpoint | Fran original: `FranBeamCap` variant |
| Cal_BloomLineFade.png | tapered compression-airburst ray | Fran original: `FranTaperedFilament` variant |
| Cal_BloomFlare.png | cannon head and polluted aura flare | Fran original: `FranFlashFourPoint` variant |
| Cal_ShineFlare.png | impact and release white flash | Fran original: `FranFlashFourPoint` variant |
| Cal_FadedStarRing.png | Leonid orbit and lightning telegraph | Fran original: `FranBrokenHalo` variant |
| Cal_AresLaserMiddle.png | colored halo/body/white-core laser trunk | Fran original: `FranBeamTrunk` variant |
| Cal_AresLaserEnd.png | capped laser endpoints | Fran original: `FranBeamCap` variant |
| Cal_StarProj.png | compact star body and death shard | Fran original: `FranResonanceOrb` variant |
| Cal_FadeStreak.png | curved high-speed projectile wake | Fran original: `FranTaperedFilament` variant |
| Cal_SimpleStar.png | small orbit node and white magic core | Fran original: `FranFlashFourPoint` variant |
| Cal_BloomCirclePinpoint.png | focused cannon and lightning core | Fran original: `FranBloomCore` variant |

Exactly 60 PNG files are expected beside this manifest. This folder is now an audit mirror and is excluded from packaging; runtime files live in `Particles/Textures`, while the reusable generated masters live in `Assets/OriginalVFX`.
`SHARED_VFX_SHA256.txt` records exact file identities for later replacement and review.
