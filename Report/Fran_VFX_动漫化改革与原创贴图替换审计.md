# Fran VFX 动漫化改革与原创贴图替换审计

日期：2026-08-29

## 1. 本轮目标

本轮同时完成两件事：

1. 保留已经认可的左键战斗实现，不改 `TeacherCombatRebuild.cs` 的动作与刀光逻辑；重做其余技能/弹幕的视觉材质组织，使其更接近 Lunar Veil 的“主体材质 + 动作阶段 + 关键帧冲击”语言。
2. 把 Fran 活动 VFX 目录中来自 Calamity、Redemption 等参考模组的内测占位图片替换为 Fran 原创图片。可以继承“这类图片承担什么绘制功能”，但不能描摹原图的具体轮廓、细节或构图。

“参考 LV”在这里指动态组织方法，不表示复制 LV 图片。本轮指纹审计没有在活动目录中发现与 LV 或旅人纹理目录完全相同的图片。

## 2. 最终资源边界

活动目录 `Particles/Textures` 共 106 张 PNG：

- 75 张原先与 Calamity/Redemption 本地源或预处理参考目录逐字节相同，现已全部替换。
- 24 张与作者自己的/已确认许可纹理库逐字节相同，保持不变：`Crack`、`EnergyRing`、`Flame`、`Halo`、`HardBloom`、`LightningBolt`、`LightningBranch`、`LightningImpact`、`MagicCircle`、`NoiseSharp`、`NoiseSoft`、`RingStreak`、`SlashNarrow`、`SlashWide`、`Smoke`、`SoftBloom`、`Spark`、`Star`、`TraceLong`、`TraceSoft`、`Twirl`、`WaterCaustics`、`Wavering`、`WindTwister`。
- `CLCB_BlackSlash` 与 `CLCB_BrinyGhost` 没有命中四个参考纹理目录，且属于作者自己的现有项目资源，因此未因本轮“外来占位替换”而改动。
- `FranMysticGlyph1`～`5` 是作者于 2026-08-30 明确要求从自己的 `CalamityThrowingSpear/LightingBolts/Particles` 直接复制的五张自绘字形；复制后逐张 SHA-256 完全一致。
- `CLCB_BrinySlash` 虽有作者项目前缀，但文件指纹命中了 Calamity 源，因此已替换，不能只凭名称判断来源。

重新扫描 Calamity、Redemption 的源码树及 `D:\预处理文件夹\6 Texture` 后，106 张活动 PNG 中剩余的外来精确哈希匹配为 0；24 张许可库文件仍保持原哈希，另有 7 张作者自有项目资源。

## 3. 原创母版

母版位于 `Assets/OriginalVFX`，共 15 张：

| 母版 | 主要功能 |
|---|---|
| `FranPressureNeedle` | 小而精的弹丸主体、压缩气针 |
| `FranResonanceOrb` | 共鸣核心、锁定标记、稳定法术身体 |
| `FranFlashFourPoint` | 枪口、命中、消失的短促四向闪帧 |
| `FranImpactBurst` | 有方向的重击/爆发关键帧 |
| `FranSmearForward` | 释放、突进和高速飞行的前向 Smear |
| `FranBloomCore` | 花瓣式能量核心、光球、雾状壳 |
| `FranBrokenHalo` | 四段破环、扩散环、屏障和蓄力轮廓 |
| `FranTaperedFilament` | 拉伸线、火花、细光束和速度丝 |
| `FranCrescentRelease` | 大型弯月释放、压缩斩和宽刃波 |
| `FranOrbitSmear` | 旋转、环切、回旋风和雾化环 |
| `FranBeamTrunk` | 有白芯和偏移侧丝的光束主体 |
| `FranBeamCap` | 菱形针尖与回折侧翼组成的光束端点 |
| `FranHeartGlyph` | 心形弹丸/治疗/死亡意象 |
| `FranVaporPuff` | 烟尘、空气团、水雾和泡沫 |
| `FranLightningNeedle` | 受控折向的动漫雷电关键帧 |

全部母版均为灰白 RGBA，供运行时着色。生成时没有把四个参考模组的图片作为图像输入；提示词只描述用途、轮廓原则、透明背景和 Fran 的动漫战斗语言。

## 4. 75 张稳定运行时文件如何替换

旧文件名继续保留，目的是避免大面积改动运行时代码；旧名称中的 `Cal_`、`Red_` 只是历史接口，不再代表当前图片来源。

| 原创母版族 | 覆盖的旧接口族 |
|---|---|
| HeartGlyph | `BigHeart`、`HeartParticle`、`LiliesOfFinalityHeartParticle` |
| BloomCore | BloomCircle、FoggyCircle、GlowOrb、LargeBloom、HolyGlow |
| BrokenHalo | BloomRing、FadedStarRing、HollowCircle、SmallBloomRing |
| TaperedFilament | BloomLine、DrainLine、FadeStreak、ThinEndedLine、Crit/ElectricSpark |
| BeamCap / BeamTrunk | AresLaserEnd、AresLaserMiddle、BloomLineCap |
| CrescentRelease | GlowBlade、JudgementWave、SlashSmear、VerticalSmear |
| OrbitSmear | CircularSmear、SemiCircularSmear、TrientSmear、RaggedSmear |
| SmearForward | ForwardSmear、BrinySlash、Red_Trail4 |
| FlashFourPoint | BloomFlare、Half/FullStar、PulseStar、ShineFlare、SimpleStar、Sparkle、WhiteFlare |
| ResonanceOrb | StarProj |
| VaporPuff | HeavySmoke、SeaFoam、WaterFlavored、WaterFoam、WaterGlob |
| LightningNeedle | Bolt、Bolt2 |

导出只做透明边界裁切、旋转、镜像、缩放和帧排布，目标文件的画布尺寸保持不变。`Bolt2` 仍为 160×510 的三帧纵向图，每帧均有非空透明内容。

`纹理图总览/shared` 中存在的同名工作副本已同步更新，`SHARED_VFX_MANIFEST.md` 和 `SHARED_VFX_SHA256.txt` 也已改为当前原创来源与当前哈希。

## 5. 非左键动漫化改造

新增 `FranVfx.Original`、`DrawOriginal`、`DrawOriginalPixels`，把高分辨率原创母版按目标像素尺寸绘制，并继续受 520 像素绘制上限保护。

已改造的主要效果包括：

- 念动力弹：稳定气针身体、前向 Smear、弹头闪点、已有 Primitive Trail。
- 技能夺取飞灵：紫色共鸣身体、反向旋转外层和短闪帧。
- 教师幻影剑/重演斩：部署核心、释放 Smear 和短促闪点。
- 蓝色共鸣：稳定共鸣球、反向外壳、释放线与方向性冲击。
- 压缩节点/压缩斩/气压波/延迟空爆：明确的收束预兆、运动主体、爆发关键帧与退出层。
- 雷击下落、弹射雷电、分支雷电、雷神之锤：雷线承担运动，四向闪光与方向冲击承担接触帧。
- 魔法屏障与反击：稳定屏障纹章、受击闪帧和长距离方向 Smear。
- 火痕、风场、锤标记：分别使用橙色气针/Smear、旋转外壳、蓝色共鸣标记。
- `FranBurst`：除保留的普通左键 Slash 分支外，念动力、夺取、屏障、剑阵、空气、雷电、锤击和冲刺爆发均改用原创冲击/闪光/Smear 组合。

绘制仍遵守：AlphaBlend 显示稳定主体，Additive 只负责外壳、光芒和余辉。这样可以避免暗背景很亮、亮背景却完全消失的问题。

## 6. 已完成验证与未完成验证

已完成：

- 106 张活动 PNG 均可读取，透明通道均非空。
- 外来参考目录精确哈希匹配：0。
- 自有/许可库精确哈希保留：24。
- `Bolt2` 三帧结构与每帧 Alpha 内容检查通过。
- 代表性弯月、破环、雷电三帧、烟尘、心形和光束纹理已肉眼检查。
- 静态编译：0 警告、0 错误。
- tModLoader 完整资源构建与打包：0 警告、0 错误；已生成当前 `Fran.tmod`。

尚未完成：

- 没有启动 tModLoader 进行实机视觉验收。
- 没有证明所有尺寸在明暗背景、缩放、屏幕震动和高速运动中都达到最终观感。
- 没有完成多人同步、实际性能和低配置显卡验证。

## 7. 2026-08-30 战斗与动态绘制二次改造

- 压缩居合不再实例化飞行 `FranPressurizedTerraWave`；四段挥剑分别沿实际剑刃路径生成 5/6/8/7 个短延迟 `FranCompressionAirburst`，每个爆点叠气针、Bloom、圆环与原有向心/放射粒子。
- 念动弹射前三段分别用 2/3/4 条横向错开的平行发射槽，按 0/3/6/9 帧连续直射；鼠标只在挥剑释放时确定共同方向，气针飞行中不再读取目标点、转向或汇聚。第四段仍为 2200 像素定向线激光；`penetrate = -1`，可在一次攻击中穿透任意数量敌人。
- 多重分身没有目标时不再搜敌或原地退出：直接保存未经距离裁切的鼠标世界坐标，只保留 1 帧生成期，取消初始弯曲和摇摆并直线加速冲向该位置。
- 旧的“一个 shader 加 0～3 数字变体”拖尾已替换为四种独立材质 profile：气针、奥术飞灵、幻影剑、重锤。每种 profile 分别定义宽度包络、滚动速度、噪声强度、主纹理、外晕和白热核心比例。
- 魔力屏障新增球面噪声和 Fresnel 边缘 shader；受击强度、完整度、裂纹和扩散环分开控制，原纹理绘制保留为明暗背景降级层。
- 新增五类可复用粒子：贴图驱动气针、Bloom、真圆环、五张作者自绘神秘字形、AlphaBlend 黑光核心。黑光用有色 Additive Bloom/环作为外缘，避免纯黑 Additive 不可见。
- 新增统一 `FranVanillaDustEffects`：按每 4 个自定义粒子约配 1 个原版 Dust 的预算，把 Dust 控制在组合粒子数量约 20%；左键有效挥舞、左键命中、八种技能释放、全部 `FranBurst` 爆发及关键飞行弹幕均已接入。

本次 `.fx` 均已重新编译为 `.fxc`。源码与资源打包验证结论以每轮最后一次构建为准。仍未启动游戏；下一步实机检查应优先看：居合爆点是否过密或重复命中、2/3/4 枚念动弹是否保持平行且错时清楚、第四段光束宽度与击中窗口、多重分身无目标路径、Dust 是否接近五分之一视觉权重而不盖住原创主体、四种拖尾在高速转向时是否断裂、黑光在明暗背景下是否都有黑核，以及屏障是否遮住玩家。发现问题时应调整数量、目标绘制尺寸、宽度曲线和透明度，不要退回外来占位图。
