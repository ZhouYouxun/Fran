# RedemptionAlpha 近战武器与特效库审查

## 1. 样本与版本

本地根目录：`C:\Users\wangk\Documents\My Games\Terraria\tModLoader\ModSources\RedemptionAlpha`

本地版本：`0.8.0.4200`

官方 [Melee weapons 分类](https://modofredemption.wiki.gg/wiki/Category:Melee_weapons) 当前显示 45 个页面，但其中包含一个分类说明页，实际列出 44 件武器。该页面最后编辑时间较早，名称和本地源码有少量差异。本报告以官网建立名称清单，以本地源码判断真实动作。

## 2. 先纠正一个预期

救赎并不是只有两三套完整的近战推动方式。它有一个非常薄的 `TrueMeleeProjectile` 基类，但这个基类主要负责：

- 按玩家近战攻速换算挥动速度。
- 提供使用时间/速度辅助函数。
- 设定近战伤害、无地形碰撞、持有者命中检查和“技术上属于真近战”的标记。

它没有统一定义挥动曲线、启动/攻击/收招相位，也没有统一绘制刀光。真正的运动和表现大多写在每把武器自己的弹幕中。因此，按源码实际行为应分成至少七个动作族。

基类入口：`Globals\Projectiles\RedeProjectile.cs:210-235`。

## 3. 七个近战动作族

### A. 原版挥舞 + 附加效果

代表：Elder Wood Sword、Cursed Grass Blade、Petrified Wood Sword、Bindeklinge。

物品本身仍由 Terraria 绘制原版摆臂/挥剑；区别只在命中事件、额外弹幕或附加斩痕。适合作为低阶武器，不适合作为 Fran 传奇武器的核心动作参考。

### B. 线性突刺、持握长柄与投枪

代表：Silver Rapier、Halbirdhouse、Bloodstained Pike、Crystal Glaive、Infectious Glaive、Xenium Lance、Xeno Xyston、Eaglecrest Javelin。

共同点是攻击轴线清晰：玩家手部、刀尖/枪尖、碰撞线和尾迹大体共线。部分长枪使用 Primitive Trail，投枪则进入自由飞行。这个家族最值得服务 Fran 左键第四段突刺。

### C. 单武器脚本化弧形持握

代表：Keeper's Claw、Zweihander、Sword Slicer、Dragon Cleaver、Blade of the Mountain、Sword of the Forgotten、Ophos' Forgotten Greatsword、Blind Justice、Tiny Cleaver、Wraith Slayer、Spellsong、Midnight、Mythril's Bane。

它们多继承 `TrueMeleeProjectile`，但各自计算 `Projectile.Center`、旋转、攻击窗口和附属效果。也就是说，它们共享的是最低限度的“持握弹幕身份”，不是一条统一轨迹。优点是每把武器很有个性，缺点是难以保证整套连段手感一致。

### D. 锤击、下砸与身体动作

代表：Iron Realm Hammer（本地类名 `PureIronHammer`）、Gravity Hammer、Hammer of Proving、Ukonvasara 的锤/斧形态。

这组武器的关键不是一条更粗的刀光，而是完整事件链：蓄势姿势 → 下降/砸落 → 接触地面 → 单帧爆点 → 震屏 → 沿地形传播的次级冲击。它们是救赎最值得学习的部分。

### E. 链、持续切割和物理工具

代表：Chomping Chains、Skull Digger's Skull Digger、Infected Eye、Automated Hacksaw。

这组的视觉来自持续接触、链节/锯刃运动和命中循环，不是一次挥砍。可用于研究“持续武器如何避免只有一团粒子”，但不直接适配 Fran 四段左键。

### F. 投掷、回旋、遥控与环绕

代表：Rad Throw、Cyber Chakram、Tidal Wake、Piercing Nebula、Sun-In-Palm、Bearded Hatchet。

核心是武器离手后的飞行状态和返回/遥控逻辑。它们可提供右键战术技能的概念，但不能拿来解释师傅本体的近战动作。

### G. 多形态状态机

代表：Ukonvasara、Blind Justice 的双形态、Sun-In-Palm 的两类投射状态。

这种结构由一次使用内部的模式切换、蓄力、释放和特殊输入组成。适合终局技能，不适合作为普通左键的基础层。

## 4. 锤类为什么有重量

### 4.1 Gravity Hammer

源码：`Items\Weapons\HM\Melee\GravityHammer_Proj.cs`

它不是数学曲线挥锤，而是 11 帧武器动画控制的状态机：

1. 按住时只在前 0–3 帧蓄势。
2. 松开后才继续播放动作，方向也随之锁定。
3. 只有第 8 帧可以伤害敌人和切割方块。
4. 第 8 帧给玩家横向少量推进、纵向增加 25 速度，使身体真的压向地面。
5. 检测下方实体方块后，播放撞击声、增加 20 震屏并记录 `SlamOrigin`。
6. 之后从冲击点向左右沿地面搜索，每隔一拍生成 `GravityHammer_GroundShock`。
7. `PreDraw` 用武器 11 帧图和一张 10 帧 `_Effect` 图同步叠画，特效只在释放阶段出现。

值得吸收的是“效果由接触事件触发”和“冲击沿真实地面传播”。不应吸收其给玩家追加纵向速度的部分到 Fran 普通左键。

### 4.2 Hammer of Proving

源码：`Items\Weapons\HM\Melee\HammerOfProving_Proj.cs`

它把玩家下落速度直接转化为伤害、震屏和地面攻击数量：

- 空中按住时加速下落；真正开始下落后武器才可伤害。
- 着地时的声音和震屏强度按垂直速度缩放。
- 伤害倍率读取玩家下落速度。
- 撞击后沿左右地面释放 `HolyHammer`，数量受连续 pogo 状态影响。
- 命中可反弹玩家，并继续下一次砸落循环。

这是一套“角色动作技能”，不是单纯武器挥舞。Fran 可以借用其接触分层，却不能把左键变成强制下砸。

### 4.3 Ukonvasara Axe

源码：`Items\Weapons\PostML\Melee\Ukonvasara_Axe.cs`

它的轨迹和 Primitive Trail 很强，但动作会旋转玩家、调用 `Owner.Move(Main.MouseWorld, 50, 10)`，下键还会把玩家速度直接设为 `(0, 50)`。这只适合作为独立终局技能参考，明确排除出 Fran 左键参考集。

## 5. 特效库的真实结构

### 5.1 Additive 统一绘制通道

`Effects\AdditiveCalls.cs` 定义 `AdditiveCallManager`。它每帧收集实现 `IDrawAdditive` 的活动弹幕与 NPC，再加入最多 1000 个手工调用对象，统一开启一次 `BlendState.Additive` 后绘制。

价值：发光对象不必让每个弹幕随意打断 SpriteBatch。局限：每帧扫描整个弹幕和 NPC 数组，且手工槽位管理较旧。

### 5.2 Primitive Trail

`Effects\PrimitiveTrails\TrailHelper.cs` 提供：

- 三个绘制层：弹幕下、缓存弹幕后、弹幕上。
- 颜色、端帽、位置、宽度、最大长度、Shader、消散速度的组合式配置。
- `ITrailProjectile` 与 `IManualTrailProjectile` 两种接入。
- 弹幕死亡后轨迹不立刻消失，而是进入独立消散阶段。
- 联机环境下的手动轨迹生成消息。

不足是 `TryTrailKill` 对多种弹幕类型使用硬编码名单。Fran 应学习“轨迹独立消散”，不要复制硬编码类型表。

### 5.3 ParticleLibrary V3

`Particles\RedeParticleSystemManager.cs` 依赖外部 ParticleLibrary，预分配多类粒子缓冲，并按 `BeforeDust`、`BeforeNPCs`、`BeforePlayers`、`BeforeSolidTiles` 等绘制层注册。常用接口包括：

- `CreateGlowParticle`
- `CreateShadowParticle`
- Slash、Laser、Pulse、Rainbow、Speed、Sharp 等粒子
- 普通与 Additive 分离的缓冲

部分缓冲容量达到 8192 或 16384。优点是高频粒子不需要每次分配对象，并且绘制层次明确；代价是系统规模很大，不适合为 Fran 九个技能原样搬入。

### 5.4 Shader 自动注册

`Effects\ShaderLoader.cs` 扫描模组内 `Effects/*.xnb`，把每个 Effect 注册到 `Filters.Scene["MoR:<name>"]`。本地效果含 Bloom、Beam、Electric、GlowTrail、Shockwave、Shield、Scan 等。

这证明救赎不是只靠一套“特效库”：它同时使用屏幕 Shader、Primitive Trail、外部粒子缓冲和普通逐帧贴图。

### 5.5 RenderTarget 与专用绘制层

`Effects\RenderTargets` 下还有基础、Shield、Ember 等目标/绘制层。它们用于先把对象画到独立目标，再进行遮罩、合成或屏幕空间处理。这个层级对 Fran 的屏障技能很有价值：屏障可以拥有自己的遮罩、受击闪烁和破裂合成，而不必和普通尘埃混在同一批次中。

### 5.6 震屏是事件结果

救赎多个武器会设置 `RedemptionScreen().ScreenShakeOrigin` 和 `ScreenShakeIntensity`。好的例子会在确认命中地面或目标后再设置，并按下落速度缩放；这比“挥动期间每帧震屏”更清楚。Fran 应集中处理震屏，并设置强度上限与距离衰减。

### 5.7 最常见也最有效的办法：逐帧贴图

很多救赎近战并没有复杂几何，而是在 `PreDraw` 中用与动作同步的多帧 `_Effect`、Slash 或 Glow 贴图。锤击之所以读得懂，关键是贴图帧严格跟随蓄势、挥落和接触帧，而不是 Shader 本身多高级。

## 6. 官网武器到本地源码索引

状态说明：`一致` 表示官网名称能直接对应本地类；`类名变化` 表示本地内部名不同；`目录外工具` 表示不在 `Items\Weapons\...\Melee`，但确属官网近战武器；`附带控制器` 表示还有一个非武器道具/控制类文件。

| 官网武器 | 本地入口 | 动作族 | 状态/重点 |
|---|---|---|---|
| Automated Hacksaw | `PostML\Melee\Hacksaw.cs` | E | 类名变化；持续锯切 |
| Bearded Hatchet | `Items\Tools\PreHM\BeardedHatchet.cs` | F | 目录外工具；投斧 |
| Bindeklinge | `PreHM\Melee\Bindeklinge.cs` | A/C | 原版挥舞并生成斩痕 |
| Blade of the Mountain | `PreHM\Melee\BladeOfTheMountain.cs` | C | 持握重剑 |
| Blind Justice, Demon's Terror | `HM\Melee\BlindJustice.cs` | C/G | 双弹幕/双形态 |
| Bloodstained Pike | `HM\Melee\BloodstainedPike.cs` | B | 长柄/投射 |
| Chomping Chains | `PreHM\Melee\ChompingChains.cs` | E | 链类持续武器 |
| Crystal Glaive | `HM\Melee\CrystalGlaive.cs` | B | Primitive Trail |
| Cursed Grass Blade | `PreHM\Melee\CursedGrassBlade.cs` | A | 原版挥舞 |
| Cyber Chakram | `HM\Melee\CyberChakram.cs` | F | 回旋武器 |
| Dragon Cleaver | `PreHM\Melee\DragonCleaver.cs` | C | 脚本化重斩 |
| Eaglecrest Javelin | `PreHM\Melee\EaglecrestJavelin.cs` | B/F | 投枪 |
| Elder Wood Sword | `PreHM\Melee\ElderWoodSword.cs` | A | 原版挥舞 |
| Forest Nymph's Sickle | `PreHM\Melee\ForestNymphsSickle.cs` | C | 持握镰刀 |
| Gravity Hammer | `HM\Melee\GravityHammer.cs` | D | 逐帧蓄力砸地 |
| Halbirdhouse | `PreHM\Melee\Halbirdhouse.cs` | B | 长柄持握 |
| Hammer of Proving | `HM\Melee\HammerOfProving.cs` | D | 下落速度驱动 |
| Infected Eye | `PostML\Melee\InfectedEye.cs` | E/F | 球链/离手 |
| Infectious Glaive | `HM\Melee\InfectiousGlaive.cs` | B | Primitive Trail |
| Iron Realm Hammer | `Items\Tools\PreHM\PureIronHammer.cs` | D | 本地类名 PureIronHammer |
| Iron Realm Sword | `PreHM\Melee\PureIronSword.cs` | C | 本地类名 PureIronSword |
| Keeper's Claw | `PreHM\Melee\KeepersClaw.cs` | C | 近距离脚本斩 |
| Midnight, Defiler of the Prince | `HM\Melee\Midnight.cs` | C | 斩击控制器 |
| Mythril's Bane | `PostML\Melee\MythrilsBane.cs` | C | 高阶斩击 |
| Noble's Halberd | `PreHM\Melee\NoblesHalberd.cs` | B/C | 长柄扫击 |
| Ophos' Forgotten Greatsword | `HM\Melee\ForgottenGreatsword.cs` | C | 巨剑持握 |
| Oversized Screwdriver | `HM\Melee\OversizedScrewdriver.cs` | C/E | 旋转工具式攻击 |
| Petrified Wood Sword | `HM\Melee\PetrifiedWoodSword.cs` | A | 原版挥舞 |
| Piercing Nebula | `PostML\Melee\PiercingNebulaWeapon.cs` | F | 离手/特殊投射 |
| Rad Throw | `HM\Melee\Chernobyl.cs` | F | 类名变化；本地显示名仍为 Rad Throw |
| Silver Rapier | `PreHM\Melee\SilverRapier.cs` | B | 线性突刺 |
| Skull Digger's Skull Digger | `PreHM\Melee\SkullDiggerFlail.cs` | E | 链锤 |
| Slayer's Rocket Fist | `HM\Melee\SlayerFist.cs` | F/G | 火箭拳 |
| Spellsong, Core of the West | `HM\Melee\Spellsong.cs` | C | 魔法化斩击 |
| Sun-In-Palm | `PostML\Melee\SunInThePalm.cs` | F/G | 两阶段能量投射 |
| Sword of the Forgotten | `PreHM\Melee\ForgottenSword.cs` | C | 持握剑 |
| Tidal Wake | `HM\Melee\TidalWake.cs` | F | yoyo/离手 |
| Tiny Cleaver | `HM\Melee\TinyCleaver.cs` | C | 快速脚本斩 |
| Ukonvasara | `PostML\Melee\Ukonvasara.cs` | D/G | 剑/锤/斧多形态 |
| Wraith Slayer | `HM\Melee\WraithSlayer.cs` | C | 持握斩击 |
| Xenium Lance | `PostML\Melee\XeniumLance.cs` | B | 高阶枪、Primitive Trail |
| Xeno Xyston | `PreHM\Melee\XenoXyston.cs` | B | 特殊 useStyle 值 100；持握长枪 |
| Zephos' Sword Slicer | `PreHM\Melee\SwordSlicer.cs` | C | 基础类还被 Zephos NPC 复用 |
| Zweihander | `PreHM\Melee\Zweihander.cs` | C | 大剑脚本斩 |

## 7. 本地额外/辅助条目

- `HM\Melee\SwordRemote.cs` 是控制/召回类物品，伤害类型为 Default，不应当算一把独立近战武器。
- `PreHM\Melee\FlintAndSteel.cs` 是工具式点火道具，虽然位于 Melee 目录，不应纳入官网近战战斗武器。
- `PostML\Melee` 下有 `TormentorsMaul.png`、`ShinkiteAxe.png`、`PoisiTwinblades.png`、`Lightstrike.png`、`LastRedemption.png`、`GirusLance.png`、`BladeOfDawn.png` 等仅贴图、无同名 C# 入口的资源；在此本地版本中不能当作已加载武器。

## 8. 给 Fran 的直接提炼

最值得拿走的不是某一张救赎刀光，而是四条生产规则：

1. 重击只在接触帧爆发；此前只给方向预兆和能量压缩。
2. 地面冲击必须读取实际接触点和地形，而不是在玩家脚下固定生成一排弹幕。
3. 一次攻击可以用一张短逐帧特效图承担主体，再用少量粒子补边，不必所有东西都做成自由弹幕。
4. 轨迹在武器消失后可以独立消散，但伤害窗口必须先结束。

救赎不适合直接成为 Fran 动作基类：它的弧形武器复用不足。它最适合成为“关键帧特效编排”和“锤击重量”的参考库。
