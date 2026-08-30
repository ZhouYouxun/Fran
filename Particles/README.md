# Fran 粒子与武器特效库

本目录是 Fran 私人本地版本的正式运行特效库。所有运行路径均使用 `Fran/Particles/Textures/`，不再依赖素材总览目录。

## 使用边界

- `Textures` 中原先与 Calamity/Redemption 参考目录相同的占位图已经全部替换成 Fran 原创用途变体；旧 `Cal_`、`Red_` 前缀只保留为稳定运行时接口。
- KS 等已确认许可库资源保持原样；`FranMysticGlyph1`～`5` 是作者从自己的 `CalamityThrowingSpear` 项目明确授权直接复制的五张自绘字形。
- 若未来增加任何外来图片，必须先记录来源与许可，并重新执行资源指纹审计。

## 自定义粒子类型

当前共有 24 种拥有独立更新/绘制逻辑的 Fran 粒子：

1. `FranGlowOrbParticle`：能量球、蓄能核心、命中 Bloom。
2. `FranSparkParticle`：有色外焰与白热核心组成的高速火花。
3. `FranLineParticle`：强化斩和突刺使用的长方向线。
4. `FranDirectionalRingParticle`：带方向压缩的命中环、屏障环和释放环。
5. `FranHeavySmokeParticle`：六阶段烟雾动画，区分普通烟与发光烟。
6. `FranStarMoteParticle`：Leonid 风格星光，支持追踪目标与独立 Bloom。
7. `FranBoltParticle`：三帧闪电碎片，有色外层与白色核心。
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

`FranHeartSeekerProjectile` 是能造成伤害的心形追踪弹幕，不属于纯视觉粒子；它保持服务端伤害判定，视觉上调用 Heart 与 StarMote。

## 系统文件

- `FranParticleCore.cs`：粒子生命周期、上限、延迟生成、前后绘制层与 Alpha/Additive 分流。
- `FranParticleTypes.cs`：除闪电外的主要粒子类型。
- `FranExpandedParticles.cs`：气针、Bloom、圆环、五字形和黑光核心。
- `FranVanillaDustEffects.cs`：原版 Dust 的统一配额、技能配色、挥舞、释放、飞行与命中入口。
- `FranBoltParticle.cs`：独立闪电粒子文件。
- `FranParticleEffects.cs`：供武器调用的组合效果与兼容入口。
- 根目录 `FranLightingBoltsSystem.cs`：由用户自有 `CLCBLightingBoltsSystem` 移植，负责 Terraria 原生池化 Sparkle 和 Fran 粒子的组合图案。

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

### 第四段：突刺

- 收剑、压缩、轴向伸出、短停、回收；
- 宽能量通道、细刃心、白色中轴三层突刺轨迹；
- ForwardSmear、轴向 Line、刀尖 Bolt 与集中命中爆点；
- 不使用宽圆弧冒充突刺；
- 不改变玩家或敌人的速度。

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

## 原版 Dust 的 20% 辅助层

- 这里的 20% 按“粒子数量预算”实现：每 4 个自定义粒子配约 1 个原版 Dust，即 `1 / (4 + 1) = 20%`；连续拖尾按其已有自定义粒子频率换算为约 1/6～1/8 帧一个 Dust。
- 左键四段在有效挥舞期持续生成刃缘 Dust，命中再按自定义粒子数补足碎屑；不再是每 2～3 帧偶尔出现一个几乎看不见的点。
- 八种技能统一经过技能释放 Dust；`FranBurst` 的所有爆发类型统一经过命中 Dust；气针、夺取飞灵、无目标分身和雷神锤另外拥有低频飞行 Dust。
- Dust 只承担约五分之一的小尺度火花、烟屑、魔力碎点和环境噪声。弹幕主体、shader 拖尾、Bloom、环和关键冲击帧仍由原创纹理与自定义粒子承担。
