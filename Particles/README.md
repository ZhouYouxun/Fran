# Fran 粒子与武器特效库

本目录是 Fran 项目专用的粒子资源库。拥有专属位图的单贴图粒子必须把
`类名.cs` 与 `类名.png` 放在这里，并通过 `Fran/Particles/<类名>` 加载。粒子系统使用的共享
Bloom、字形、火花、拖尾条、噪声等材质也必须在本目录保存完整副本；原图可以继续保留在
`Textures/Particles` 或 `Assets/OriginalVFX`，供弹幕 `PreDraw` 和其他非粒子绘制器使用。

`FranParticleCore.cs` 与 `FranParticleEffects.cs` 负责生命周期和组合效果，不直接对应单个粒子。
具体粒子已经拆分为独立同名 `.cs`；拥有主贴图的粒子同时使用同名 `.png`，其余辅助层继续
读取本目录中的共享材质。共享贴图是否齐全以 `Textures/Particles/*.png` 到本目录的哈希一致镜像为准。

## 使用边界

- `FranGlowOrbParticle.png` 与 `FranSmallBloomParticle.png` 的资源许可已经确认；本目录保存原文件
  哈希一致的副本，不通过外部模组运行时路径加载。
- `FranBoltParticle.cs/.png`、`FranSkillGlyphParticle.cs/.png`、`FranGlowOrbParticle.cs/.png`、
  `FranSmallBloomParticle.cs/.png` 是当前强制执行的一一对应专属资源。
- `Textures/Particles` 的共享材质在本目录保留哈希一致副本；`FranParticleAssets` 只从 VIP 路径读取。
- 粒子用到的 `FranPressureNeedle`、`FranFlashFourPoint`、`FranResonanceOrb` 同样在 VIP 保存副本，不跨区读取 `Assets/OriginalVFX`。
- `LargeBloom.png` 即使已进入 VIP，仍属于大面积完整绘制素材，只能作为受控绘制层使用，禁止交给粒子生成器批量释放。
- KS 等已确认许可库资源保持原样；`FranMysticGlyph1`～`5` 是作者从自己的旧项目明确授权直接复制的五张自绘字形。
- 若未来增加任何外来图片，必须先记录来源与许可，并重新执行资源指纹审计。

## 自定义粒子类型

当前共有 34 种拥有独立更新/绘制逻辑的 Fran 粒子：

1. `FranGlowOrbParticle`：能量球、蓄能核心、命中 Bloom。
2. `FranSparkParticle`：有色外焰与白热核心组成的高速火花。
3. `FranLineParticle`：强化斩和突刺使用的长方向线。
4. `FranDirectionalRingParticle`：带方向压缩的命中环、屏障环和释放环。
5. `FranHeavySmokeParticle`：六阶段烟雾动画，区分普通烟与发光烟。
6. `FranStarMoteParticle`：Leonid 风格星光，支持追踪目标与独立 Bloom。
7. `FranBoltParticle`：原有三帧闪电碎片；新雷电链和雷锤的主电弧改用下面的单帧资源。
8. `FranBladeChipParticle`：重斩刃屑、像素碎片和电火花。
9. `FranHeartParticle`：魔法心形粒子，可漂浮、弯曲和脉动。
10. `FranWaterParticle`：水滴、海沫与水系切割碎液。
11. `FranToxicMoteParticle`：从 Seas Searing 视觉语言提炼的污染液滴/毒雾微粒。
12. `FranSkillGlyphParticle`：原有的程序化技能符号粒子。
13. `FranBarrierShardParticle`：屏障受击、破裂和回收碎片。
14. `FranResonanceNoteParticle`：蓝色共鸣使用的音符/波节。
15. `FranBladeEdgeMoteParticle`：沿真实剑刃边缘释放的短针亮点。
16. `FranBladeWakeSliverParticle`：沿挥动切线移动的狭长刃片余迹。
17. `FranBladeTipFlareParticle`：挥砍开始与峰值关键帧的刀尖闪光。
18. `FranBladeImpactCrossParticle`：命中瞬间的方向性交叉冲击。
19. `FranBladeArcFragmentParticle`：从解析挥砍弧上分离的短弧碎片。
20. `FranNeedleParticle`：直接使用 `FranPressureNeedle` 母版轮廓的可复用气针；形状与贴图强关联。
21. `FranBloomParticle`：支持软核/硬核、独立膨胀和衰减曲线的 Bloom。
22. `FranRingPulseParticle`：真正圆环或可压扁为透视椭圆的扩散环，与方向性命中环分工。
23. `FranMysticGlyphParticle`：五张作者自绘字形随机轮换，使用 AlphaBlend 保住细线主体。
24. `FranBlackLightCoreParticle`：AlphaBlend 黑色遮光核心；必须另叠有色 Additive 外缘，禁止把纯黑直接送入 Additive。
25. `FranSmallBloomParticle`：小尺寸、轮廓明确的授权 Bloom；像素尺寸被硬限制在 40px 内，供爆炸碎光等粒子层使用。
26. `FranBloomCirclePulseParticle`：只绘制 `CL_BloomCircle`，不附加 SoftBloom/HardBloom 底盘；用于紫色 Nova 弹体、尾迹和分层爆发。
27. `FranLightParticle`：由 BloomCircle 与纵向 GlowSpark 组合的通用亮点，支持速度挤压。
28. `FranHalleyPulseRingParticle`：念动彗星专用的脉冲环，使用同名 VIP 贴图。
29. `FranHalleySparkParticle`：念动彗星专用的拉伸火花，使用同名 VIP 贴图。
30. `FranConstellationMoteParticle`：星座组控制的轨道亮点，按共享组生命周期更新。
31. `FranTacticalDropletParticle`：蓝辉等战术使用的液滴/碎液粒子。
32. `FranSwingCadenceParticle`：普通挥砍使用的短火花、脉冲与线状节奏粒子。
33. `FranWaveSquishyLightParticle`：蓝辉共鸣命中时爆发的可挤压波体光粒子。
34. `FranWaveGlowSparkParticle`：可复用的径向收缩火花；折念刃冲击波不调用该粒子。

`FranHeartSeekerProjectile` 是能造成伤害的心形追踪弹幕，不属于纯视觉粒子；它保持服务端伤害判定，视觉上调用 Heart 与 StarMote。

## 系统文件

- `FranParticleCore.cs`：粒子生命周期、上限、延迟生成、前后绘制层、Alpha/Additive 分流与专属同名贴图入口。
- 各个 `Fran*Particle.cs`：独立保存对应粒子的更新和绘制逻辑。
- `FranExpandedParticles.cs`：气针、Bloom、圆环、五字形和黑光核心。
- `FranVanillaDustEffects.cs`：原版 Dust 的统一配额和动作意图；每接纳 4 个自定义粒子固定生成 1 个 Dust。
- `FranBoltParticle.cs`：独立闪电粒子文件。
- `FranParticleEffects.cs`：供武器调用的组合效果与兼容入口。
- `Content/Systems/FranLightingBoltsSystem.cs`：负责 Terraria 原生池化 Sparkle 与 Fran 粒子的组合图案。

## 左键四段表现合同

### 第一段：批砍

- 连续有色外轨迹；
- 白热内刃心；
- 刀中轴线；
- 大型 Smear 与细 Smear；
- 武器历史残影、轮廓与本体；
- Tip Spark、Line、GlowOrb、Blade Dust；
- 命中方向环、Bloom、长短火花。

### 第二段：转圈

- 玩家中心连续圆形运动面；
- 内层圆形刃心；
- 72 点历史轨迹，每游戏帧插入 4 个采样；
- 沿运动切线离心的 Star、Spark、Line 与 Dust；
- 不改变玩家纵向/横向速度。

### 第三段：强化批砍

- 更长的预备和更陡的有效段；
- 大型破碎 Smear；
- 紧窄白热核心；
- 额外 SlashSmear；
- 能量剑身切片；
- HeavySmoke、BladeChip、Bolt、Spark、Line 与强化 Dust；
- 命中追加剑屑星阵与更强冲击层。

### 第四段：定向推出

- 出刀时锁定鼠标方向，剑沿轴线推出再收回；
- 整段不改变剑的角度，不让玩家本体跟随位移；
- 第二、第四段攻击距离均为第一、第三段的 `1.2` 倍；
- 不移植参考武器的格挡、无敌帧、充能或敌对弹幕减伤；
- 不改变玩家或敌人的速度，也不恢复箭头、突破门环等叠层特效。

## 右键能力的粒子分工

- 念动力：气针、Bloom、圆环、五字形、StarMote、GlowOrb 与 Line；前三段以鼠标方向确定共同主轴，分别用 2/3/4 条平行发射槽按 3 帧间隔连续直射，飞行中不追踪、不弯曲、不汇聚，第四段为无限敌人穿透的定向光束。
- 技能掠夺：黑光核心、紫色发光外缘、五字形、Heart、StarMote 与 DirectionalRing；命中释放伤害型 HeartSeeker。
- 屏障：球面噪声/Fresnel shader、克制的圆环与五字形、守卫 Star、碎片和向心 Line。
- 浮游剑阵：Line、BladeChip、StarMote。
- 火焰牙/元素附魔：Glowing HeavySmoke、GlowOrb、火花 Dust；其他元素改用 Water/Bolt。
- 空气压缩：挥剑路径上的延迟空爆；每个爆点组合气针、Bloom、圆环、Line 与 Spark，不再先发射飞行气压弹。
- 雷鸣魔法：Bolt、Line、GlowOrb，并调用 `FranLightingBoltsSystem`。
- 断舰：Water、DirectionalRing、Spectral Sparkle 与重斩火花。
- 雷神之锤：Bolt、ThunderImpact、Ionizing Sparkle、DirectionalRing。

## 雷电专用资源（2026-09-03）

- `FranArcParticle.cs/.png`：单帧灰白透明电弧。素材由内置图像生成功能从文字描述创建；PNG 保留生成时的透明通道。长轴为竖直方向，运行时按像素尺寸缩放、旋转、镜像和着色，不按三帧裁切。
- `FranFissionArcParticle.cs`：短时裂变主雷，无配套 PNG。`Core/Graphics/FranFissionArc.cs` 在创建时生成有限的分叉折线，用原版 `MagicPixel` 的 1×1 区域加法绘制，不使用外部粒子、贴图或 shader。
- 闪电链在导通前沿和命中窗口内补余电；雷锤落点先劈主雷，再展开地面裂纹。所有新增电弧粒子只负责视觉，伤害由原有弹幕管理。

## 原版 Dust 的 20% 辅助层

- 这里的 20% 按“粒子数量预算”在 `FranParticleSystem.Spawn` 统一实现：每接纳 4 个自定义粒子固定配 1 个原版 Dust，即 `1 / (4 + 1) = 20%`。
- Dust 的位置、运动轴、侧向偏移、类型与颜色来自粒子本身和最近的技能动作意图；只使用固定节奏序列，不再用随机方向、随机速度掩盖缺少设计的问题。
- 左键四段在有效挥舞期持续生成刃缘 Dust，命中再按自定义粒子数补足碎屑；不再是每 2～3 帧偶尔出现一个几乎看不见的点。
- 八种技能统一经过技能释放 Dust；`FranBurst` 的所有爆发类型统一经过命中 Dust；气针、夺取飞灵、无目标分身和雷神锤另外拥有低频飞行 Dust。
- Dust 只承担约五分之一的小尺度火花、烟屑、魔力碎点和环境噪声。弹幕主体、shader 拖尾、Bloom、环和关键冲击帧仍由原创纹理与自定义粒子承担。
