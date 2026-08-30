# WeaponOut 与 Calamity 特殊剑逐把审查：Fran 左键重构依据

> 审查日期：2026-08-15  
> 本地 CalamityMod：`2.2.2`，提交 `1a8cebd27e`  
> 本轮性质：只读审查与设计定案，不修改 Fran 战斗代码

## 一、先给结论：上一版为什么只能得到 60 分

上一版的问题不是“少放了几颗粒子”，而是表现结构本身没有学到 Calamity 的核心：

1. 动作曲线、有效伤害窗口、刀光几何和粒子发射没有共享同一套攻击进度与空间锚点。
2. 一张宽刀光、一条历史残影和一批通用粒子被叠在四段动作上；四段攻击主要只是尺寸、数量和亮度不同，没有各自的视觉语法。
3. 刀光没有明确分工。Calamity 的高规格挥舞经常同时存在外层有色运动面、内层高亮刃心、武器本体辉光或重影；上一版基本只有一个“泛光扇面”。
4. 粒子没有明确出生位置和职责。刀尖火花、刃缘碎屑、挥舞方向线、蓄力烟尘、命中冲击环本应来自不同锚点；上一版更像在武器附近随机撒效果。
5. `Dust`、Calamity 粒子、贴图刀光、Primitive Trail 和命中特效不是互相替代的四种方案，而是可以在同一次攻击中分别承担材质、速度、体积、连续几何和接触反馈。
6. 当前待机持握通过 `HeldTeacherVisual` 设置 `heldProj`、`itemRotation` 和前臂姿势，所以玩家不攻击时仍把师傅举在身前。这与用户要求的“背后 45 度挂剑”完全相反。

因此，下一轮不能继续在旧效果上“修补”。左键应保留已经确认的四段战术动作，但重新建立一套从动作骨架到绘制分层的完整合同。

## 二、WeaponOut：该学的是挂背架构，不是照抄一个角度

用户提供的链接当前会跳转到 [WeaponOutLite 仓库](https://github.com/zzp198-old/WeaponOutLite)。它的核心不是生成一个常驻持握弹幕，而是用 `PlayerDrawLayer` 在玩家绘制流程中添加武器：

- `WeaponBackLayer` 放在背部相关绘制层；
- `WeaponFrontLayer` 放在手持物品相关绘制层；
- `itemAnimation > 0` 时不绘制待机武器，避免与真正攻击动作重叠；
- 根据 `useStyle`、贴图长宽、`noUseGraphic` 等信息决定是背负还是前持；
- 绘制时统一处理玩家朝向、重力方向、走路帧偏移、武器缩放、光照和贴图原点。

需要特别澄清：WeaponOutLite 并不是给每把剑写死 45 度。它的背负旋转会根据贴图长度稍作调整。Fran 不需要复制它的通用分类器，只需要吸收以下结构：

1. 师傅被选中；
2. 玩家没有进行左键、右键或技能动作；
3. 没有师傅已经脱手部署的形态；
4. 在玩家背后图层按“师傅专属固定 45 度”绘制；
5. 攻击开始后立即让位给真实攻击控制器。

Fran 的最终待机姿态应为：剑柄靠近肩后或腰后，剑身沿背部斜向延伸，跟随玩家朝向与重力翻转，不修改前臂姿势、不强制玩家转向鼠标、不占用 `heldProj`。这才是“挂在身后”，而不是把现有持握弹幕挪到玩家背后。

许可边界：该仓库页面未见明确 LICENSE 文件。本项目只采用“PlayerDrawLayer + 空闲门控 + 背后图层”的通用思路，不逐字复制它的实现。

## 三、Calamity 并不存在一个包办一切的特殊剑基础类

官方 [Special swords 页面](https://calamitymod.wiki.gg/wiki/Special_swords) 把这类武器描述为朝鼠标方向自定义挥舞、经常交替正反方向且动作差异很大的特殊近战武器。本地源码进一步证明，它们至少分成三套控制方式。

### 3.1 `BaseCustomUseStyleProjectile`：持握与生命周期容器

源码：`CalamityMod\Projectiles\BaseProjectiles\BaseCustomUseStyleProjectile.cs`

它统一了：

- 武器物品绑定、攻击生命周期和连段编号；
- 玩家前后手臂姿势、朝向、武器偏移与缩放；
- 命中开关、碰撞体定位和攻击开始/结束钩子。

它不统一实际挥舞曲线，也不自动提供 Calamity 粒子或完整刀光。Earth、Hellkite、Holy Collider 等武器仍在自身 `UseStyle()` 和 `PreDraw()` 中编排动作与视觉。因此它更像“自定义动作的可靠容器”。

### 3.2 `BaseSwordHoldoutProjectile`：真正可复用的挥舞骨架

源码：`CalamityMod\Projectiles\BaseProjectiles\BaseSwordHoldoutProjectile.cs`

它统一了：

- 启动、挥舞、收招三阶段；
- 交替挥向、挥舞宽度、距离、近战攻速和近战尺寸；
- 当前/历史旋转与缩放；
- 刀身线段碰撞；
- 武器残影；
- 可选的 Primitive Trail、宽度函数、颜色函数和 `ExobladeSlash` Shader；
- 在指定攻击进度触发次级弹幕。

这套类最大的价值不是“省代码”，而是让动作、碰撞、残影和连续刀光都使用同一套进度。Fran 只有一把传奇武器，不必照搬成庞大继承体系，但必须拥有同等明确的阶段合同。

### 3.3 独立 `ModProjectile` 状态机：为独特武器让路

P-GSS Auger、Basher、Grand Dad、Exoblade，以及 Biome Blade 的大量姿态并不依赖上述通用挥舞骨架。它们直接维护状态、进度、命中窗口和专属绘制。这说明 Calamity 的统一之处并不是“每把武器继承同一个类”，而是以下表现职责始终清楚：

- 谁决定动作；
- 谁决定碰撞；
- 谁绘制连续运动面；
- 谁绘制刃心、武器辉光和重影；
- 哪类粒子表现速度、材质、蓄力或魔法；
- 哪类 Dust 做低成本碎屑与环境噪声；
- 命中时追加哪些一次性效果。

## 四、Calamity 一次高规格挥舞通常有哪些分层

并非每把武器都必须拥有下表所有层；但 Earth、Hellkite、Devil's Devastation、Grand Dad 等高规格武器会同时启用多层，而且每层分工不同。

| 层级 | 典型实现 | 职责 |
|---|---|---|
| 动作骨架 | 分段缓动、状态机、正反交替 | 决定武器何时蓄势、加速、穿过目标、收招 |
| 武器本体 | 原贴图、glowmask、动态形变 | 保证玩家看得见真正的剑，而不是只看见光团 |
| 武器重影/轮廓 | 20～25 个偏移副本或历史残影 | 增加能量体积与高速感，不承担主刀光 |
| 外层运动面 | `VerticalSmearLarge`、`CircularSmear*`、大范围 Swoosh | 告诉玩家挥舞覆盖范围与方向，通常颜色较深、范围较宽 |
| 内层刃心 | 白热细弧、第二层 Smear、能量剑身重建 | 强调真正切割路径；比外层更窄、更亮、存续更短 |
| 连续轨迹 | Primitive Trail + Shader | 让高速动作仍保持连续曲线，不因逐帧贴图出现断口 |
| 自定义粒子 | GlowSpark、CustomSpark、LineParticle、CritSpark 等 | 分别表现刀尖火花、长方向线、蓄力能量、魔法符号、暴击碎片 |
| Dust | Fireworks、SquashDust、材料碎屑等 | 填充小尺度噪声、碎渣和材质感，数量可以多但不能抢主轮廓 |
| 命中爆发 | Bloom、冲击环、定向火花、音效 | 只在接触帧确认“击中了”，与常驻挥舞特效区分 |
| 次级实体 | 陨石、投剑、光束、爆炸、追击 | 表达武器机制；不能拿来掩饰近战动作本身不完整 |

用户所说的“至少两种挥值”判断是正确的。以 Earth 为例，同一次表现不是一张扇形贴图：它会组合武器幽灵轮廓、多层大型 Smear、由大量 `BloomCircle` 切片重建的能量剑身、GlowSpark/CustomSpark/LargeBloom、Fireworks Dust，以及命中的长短火花、双色冲击环、声音和次级陨石。各层各司其职，绝不是把粒子数量调大。

## 五、截图中 22 把特殊剑逐把审查

### 5.1 困难模式前

| 武器 | 主控制器/继承 | 挥舞与绘制组成 | 对 Fran 的有效启示 |
|---|---|---|---|
| Basher | `BasherHoldout : ModProjectile` | 独立交替挥舞；`VerticalSmearThin` 类速度面；武器本体；Pearlwood 材料 Dust；像素 SquashDust 负责冲击碎屑 | 低阶攻击也需“主挥舞面 + 材质碎屑 + 接触碎屑”，但不必堆满所有层 |
| Broken Biome Blade | 中央持握为独立状态机；Pure Clarity 使用 `BaseCustomUseStyleProjectile`，其余姿态多为独立控制器 | 调谐仪式使用 Bloom/Sparkle；冰姿态用 Mist、Snowflake、CritSpark；荒漠姿态用烟、Bloom、石屑；腐化姿态另写运动 | 不同招式要更换粒子语法与几何，不是只换颜色 |
| Old Lord Claymore | `BaseCustomUseStyleProjectile` | 多重武器光晕；大型纵向 Smear；武器本体与 glowmask；挥舞 Spark/SquashDust；命中 Dust 和分层音效 | 可作为普通重斩的基础范例：轮廓、主刀光、细碎速度粒子三层齐全 |
| P-GSS: Auger | `AugerHoldout : ModProjectile` + `AugerSlash : ModProjectile` | 独立持握/磁吸/强化斩状态；彩色刃层与白色核心；BloomCircle；沿斩击产生 SquashDust；命中追加 Arsenal Dust | 强化斩应改变动作与刀光比例，而不只是把第一次斩击放大 |

### 5.2 困难模式

| 武器 | 主控制器/继承 | 挥舞与绘制组成 | 对 Fran 的有效启示 |
|---|---|---|---|
| Baleful Harvester | `BaseCustomUseStyleProjectile` | Smear/半圆 Swipe；Bloom 与刀尖星光；CritSpark 表现挥舞；命中骷髅粒子作为机制标记 | 刀尖高亮、运动粒子和命中标志是三件不同的事 |
| Biome Blade | 中央调谐状态机 + 多姿态控制器 | 水姿态用泡沫/符印；冰姿态用雾/雪花；荒漠姿态用烟/石屑；腐化姿态用冲刺斩；森林姿态用绿光/藤蔓；天界姿态用持续 Smear | 同一武器可以保持骨架一致，但每种能力必须拥有自己的视觉词汇 |
| Comet Quasher | `BaseCustomUseStyleProjectile` | 大量幽灵轮廓；本体 glowmask；挥舞时 Pulse、重烟、Fireworks Dust；命中蓝色 Spark 和陨石 | 重型钝击的主体是体积与落点；陨石只是机制回报，不应替代挥击本身 |
| Exalted Oathblade | `BaseCustomUseStyleProjectile` | 武器光晕、大型 Smear、本体；挥舞 SquashDust 与恶魔符印；命中双层 Bloom、定向符印斩、GlowSpark 云 | 魔法符号应绑定挥舞/命中事件，而不是无意义地常驻在剑周围 |
| Forbidden Oathblade | `BaseCustomUseStyleProjectile` | 与 Exalted 同族的光晕、Smear、符印和投剑结构，但规模更克制 | 同一视觉家族可以按阶段升级，而不是每阶段完全换主题 |
| Grand Guardian | `BaseCustomUseStyleProjectile` | 多重轮廓、大型 Smear、本体 glowmask；GlowOrb、GlowSpark、CustomSpark、Fireworks Dust；命中爆破弹幕 | 粒子家族很多，但主轮廓仍由大刀光和武器本体承担 |
| Hellkite | `BaseCustomUseStyleProjectile` | 轮廓、大型破碎 Smear、本体与多层蓄力辉光；Line、AltSpark、GlowOrb、HeavySmoke、Point、Rainbow/Fireworks Dust；命中再追加线粒子、双色 Bloom 和 Spark | 最典型的“粒子分工”范例：线表现速度，烟表现热体积，点/球表现蓄能，Dust 表现火花，Bloom 表现接触 |
| Lucrecia | `BaseSwordHoldoutProjectile` | 约 310° 宽挥舞；通用阶段与碰撞骨架；CritSpark、CircularSmearVFX、纵向 Smear Spark、GlowSpark；DNA 轨迹和光弹 | 转圈攻击需要连续大角度几何和离心粒子，不该用几张离散扇面拼接 |
| Majestic Guard | `BaseCustomUseStyleProjectile` | 轮廓、大型 Smear、本体 glowmask；挥舞线粒子与 Fireworks Dust；命中金银方向线 | 克制型武器也依靠明确的主刀光，不能因为粒子少就只剩武器贴图 |
| Mantis Claws | `MantisClawHoldout : BaseCustomUseStyleProjectile` + `MantisClawSlash : ModProjectile` | Slash 实体本身使用多层染色 Smear；几乎不依赖大批自定义粒子 | 证明“粒子越多越好”是错的；只要专属刀光几何足够清楚，快速攻击可非常简洁 |

### 5.3 月后

| 武器 | 主控制器/继承 | 挥舞与绘制组成 | 对 Fran 的有效启示 |
|---|---|---|---|
| Devil's Devastation | `BaseCustomUseStyleProjectile` | 由大量 BloomLine 切片构成巨型能量刃；武器轮廓；大型 Smear/CircularTrail；挥舞 SquashDust、符印、Bloom；命中环、定向符印、GlowSpark、Spark | 超规格攻击仍需一条明确的刃形；大量粒子只为刃形和命中服务 |
| Earth | `BaseCustomUseStyleProjectile` | 轮廓；同一状态可叠两层大型 Smear；另一状态使用更大的单层 Smear；大量 BloomCircle 切片重建彩色能量剑身；多类 Spark/Bloom/Dust；命中多层火花与冲击环 | Fran 第一/第二段的整体参考，以及第三段强化斩的层级参考；禁止照搬它的敌人强制位移 |
| Grand Dad | `GrandDadHoldout : ModProjectile` | 独立重击状态机；像素化管理器；多重轮廓；像素 Circular Smear；独立 Additive 刃心；Void/Squash 像素 Dust；命中黑蓝双层 Spark 与大量碎屑 | 第三段应学习它的重击分层：外部重量面、内部锐利核心、像素碎片和延迟命中爆发 |
| Holy Collider | `BaseCustomUseStyleProjectile` | 充能状态；武器轮廓与 glowmask；Line、Providence Mark、LargeBloom、Squash/Fireworks Dust；弹幕碰撞与命中产生环和圣火 | 充能符号必须对应充能机制；Fran 普通左键不应无缘无故复制神圣印记 |
| Maw of Infinity | `BaseSwordHoldoutProjectile` | 通用挥舞阶段；青/粉两侧 GlowSpark 流；独立 Jaws 覆盖层；DoG 火焰与颚部弹幕 | 粒子可沿刀身不同侧发射，空间锚点比“随机围绕玩家”更重要 |
| Mirror Blade | `BaseSwordHoldoutProjectile` | 快速定向斩、历史残影；反射敌弹；命中生成爆破/斩击实体，子斩使用 VoidSpark 与 SquashDust | 快速斩可依靠短残影和专属子斩；无需给每段都塞大环 |
| The Mutilator | `BaseSwordHoldoutProjectile` | 通用 Shader Trail；短武器残影；红/暗红/金色轨迹；命中 DustyCircle Pulse、治疗球与血肉音效 | 屏障/治疗等机制应有专属命中回馈，但不能与普通左键效果混淆 |
| Stellar Striker | `BaseCustomUseStyleProjectile` | 玩家中心 CircularSmearSmokey；多重武器轮廓；Pulse、GlowSpark、Fireworks Dust；命中 Spark 与陨石 | 圆形 Smear 只适合真正绕玩家旋转的攻击；位置语义必须正确 |

## 六、用户另外点名的两个直接参考

### 6.1 Exoblade：第三段不是“第一段调大”

`ExobladeProj : ModProjectile` 使用独立状态机。它的普通斩与突进/撞击状态分开；斩击轨迹由挥舞进度生成连续采样点，再交给 Primitive Renderer 与专属 Shader 绘制。它还会对武器本体做动态形变，并把发射窗口绑定到攻击进度。

对 Fran 第三段应该吸收的是：

- 更短、更明确的预备；
- 更陡的中段加速；
- 更窄但更白热的刃心；
- 与第一段不同的连续几何和命中爆发；
- 刀身本体也参与强化感，而不是只增加外部粒子。

明确排除：Exoblade 的冲刺、反弹、光束、吸血和玩家速度写入都不是 Fran 左键第三段的必要组成。Fran 四段左键均不得强制移动玩家或敌人。

### 6.2 Gilded Proboscis：第四段必须是轴向突刺

`GildedProboscisProj : BaseSwordHoldoutProjectile` 主要通过轴向距离变化表达蓄力与前刺，而不是用大圆弧假装突刺。它使用直线碰撞、刀尖 GlowSpark、BloomCircle Spark、ScarletDevilStreak 和分级命中反馈。

Fran 第四段应该吸收：

- 收剑压缩；
- 沿鼠标轴线快速伸出；
- 很小的角度摆动；
- 狭长的穿刺轨迹；
- 刀尖爆点和轴向速度线；
- 快速回收。

明确排除：任何玩家位移、敌人击退脚本或把一张宽扇形刀光旋转 90° 冒充枪刺。

## 七、Fran 当前源码与 Calamity 的实际差距

### 7.1 当前具备的东西

- `TeacherSwing` 已经有四段动作曲线、直线碰撞和攻击历史数组；
- `PreDraw()` 已经尝试绘制 Trace、Slash、元素效果和武器本体；
- `FranParticleSystem` 有 Spark、Glow、Ring、Smoke、Flame、Shard 六种通用类型；
- 已经能区分 Alpha 与 Additive；
- 命中有粒子和轻度震屏。

这些说明旧实现不是毫无基础，但它们只够支撑“能看见一把自定义挥舞武器”，还没有达到 Calamity 式完整表现。

### 7.2 必须推倒重建的部分

1. **待机姿态错误**：`HeldTeacherVisual` 强制前臂伸直、朝鼠标转向并把师傅举在前方。应改为 PlayerDrawLayer 挂背。
2. **主刀光几何错误**：`SlashWide/SlashNarrow` 是固定贴图叠加；Trace 历史只是在过去角度画线，不能稳定构成连续、可控宽度的外层运动面与内层刃心。
3. **四段共享同一视觉配方**：现状主要通过 `combo` 改尺寸和粒子倍率，缺少四套动作语义。
4. **粒子锚点不足**：通用 `MeleeTrail(tip, bladeDirection, tangent...)` 仍不足以区分刀尖、刃中、刀背、剑柄、旋转中心和命中法线。
5. **绘制深度过于单一**：粒子系统统一在全部弹幕后绘制，只分 Alpha/Additive；无法像 Calamity 那样让部分烟尘在武器后、刃心在武器上、命中闪光在最前。
6. **Dust 角色缺失**：当前主要依赖自建粒子；缺少低成本材料碎屑、火星和像素碎渣这一层。
7. **命中层与挥舞层不够分离**：环和 Glow 很容易在移动过程中也出现，导致玩家无法一眼判断“正在挥”还是“已经命中”。
8. **黑灰圆洞没有语义**：Smoke、Ring 或暗色 Bloom 若没有对应充能、冲击、屏障或黑暗爆炸机制，只会形成用户所说的灰洞。所有圆形效果必须有明确事件来源。

## 八、下一版左键必须遵守的完整表现合同

### 8.1 共用骨架

每段攻击都必须有以下数据，并由同一攻击进度驱动：

- `Startup / Active / Recovery` 三阶段；
- 武器角度、轴向距离、武器缩放或轻微形变；
- 有效命中窗口；
- 外层刀光采样点；
- 内层刃心采样点；
- 粒子锚点：剑柄、刃中、刀尖、刀刃法线、运动切线；
- 命中方向与命中点；
- 发射次级效果的唯一事件帧。

这不是为了建立复杂继承树，而是避免动作系统和绘制系统各算各的。

### 8.2 第一段：批砍

- 中等启动，明显加速穿过鼠标方向，快速收招；
- 一层宽而较暗的有色外弧，表达覆盖范围；
- 一层窄而明亮的白蓝刃心，表达真正切割路径；
- 2～4 帧武器本体残影；
- 刀尖产生细长 Spark，刃缘产生少量碎屑 Dust；
- 命中才出现小型定向 Bloom 与短促碎片喷射；
- 不出现大型圆环、烟洞或不明飞行弹幕。

### 8.3 第二段：转圈

- 以玩家为真实旋转中心，连续约一周的大角度轨迹；
- 外层为连续圆形运动带，内层为跟随真实剑刃的窄刃心；
- 粒子沿运动切线离心飞出，而不是随机向四周爆炸；
- 玩家中心只允许非常克制的转动涡流，不放巨大遮挡环；
- 命中多个敌人时，每个命中点各自产生小反馈，不能每次覆盖整个屏幕。

### 8.4 第三段：强化批砍

- 不是第一段放大版，而是更短预备、更陡加速、更硬的停止；
- Earth 式宽大有色重量面 + 更窄的白热核心，两层时间错开；
- Grand Dad 式少量像素/方形碎片用于强调强度，但不改变动漫式高能刀光主色；
- 武器本体在有效帧出现更强 glowmask/轻微拉伸；
- 命中爆发可有双色 Bloom、沿斩向喷射的长短火花和更明显音效/震屏；
- 仍然不改变玩家或敌人的速度。

### 8.5 第四段：突刺

- 收剑、压缩、前刺、短停、回收五个清楚瞬间；
- 取消宽圆弧，改用狭长 Primitive Pierce Trail；
- 外层是一条暗色细长能量通道，内层是一条白亮轴线；
- 速度线沿刺击反方向退散，刀尖产生集中 Spark；
- 命中点产生小而亮的前向爆点，可以带一圈极短的压缩冲击环；
- 不生成与突刺方向无关的侧向弹幕，不强制位移。

## 九、粒子与绘制的最低规格

下一版至少需要按职责拆出以下类别，而不是继续只用六种通用外观随意套用：

1. `TipSpark`：刀尖细长速度火花；
2. `EdgeChip`：刃缘短寿命碎片/Dust；
3. `DirectionalLine`：强化斩或突刺的长方向线；
4. `GlowOrb`：蓄力或能量聚集，禁止无机制时乱放；
5. `PixelChip`：只属于第三段强化斩或黑暗冲刺爆炸；
6. `ImpactSpark`：命中法线方向喷射；
7. `ImpactBloom`：命中确认；
8. `Smoke/Mist`：只用于确实需要体积或暗影的能力，不是通用填充物；
9. `OuterSwoosh`：连续外层运动面；
10. `InnerBladeCore`：连续内层刃心；
11. `WeaponAfterimage`：少量武器本体历史；
12. `PierceTrail`：第四段专用轴向轨迹。

绘制顺序至少应拆成：玩家/武器后方烟尘与外辉光、外层刀光、武器本体、内层刃心与刀尖光、最前方命中爆发。Alpha 与 Additive 只是混合方式，不等于绘制深度。

## 十、最终审查结论

Calamity 的精髓不是“资源多”，也不是“每次挥剑同时生成所有粒子”。它真正做对的是：

> 先用动作与连续几何把这一击说清楚，再让每一种贴图、粒子、Dust、声音和命中事件只负责一句话。

Fran 下一版左键应保留四段玩法定义，但待机持握、刀光几何、粒子锚点、绘制深度和四段视觉语法都需要按本报告重构。继续在当前 `SlashWide + Trace + 通用粒子配方` 上增加数量，只会让画面更满，不会更接近 Calamity，也不会更接近动漫里的高能刀光。

## 十一、主要证据路径

- WeaponOutLite：`WeaponLayer.cs`、`WeaponDrawInfo.cs`
- Calamity 基础类：`Projectiles\BaseProjectiles\BaseCustomUseStyleProjectile.cs`
- Calamity 基础类：`Projectiles\BaseProjectiles\BaseSwordHoldoutProjectile.cs`
- Calamity 核心参考：`Projectiles\Melee\EarthHoldout.cs`
- Calamity 核心参考：`Projectiles\Melee\ExobladeProj.cs`
- Calamity 核心参考：`Projectiles\Melee\GrandDadHoldout.cs`
- Calamity 核心参考：`Projectiles\Melee\HellkiteHoldout.cs`
- Calamity 突刺参考：`Projectiles\Melee\Spears\GildedProboscisProj.cs`
- Fran 当前左键：`Content\Projectiles\FranProjectiles.cs` 中的 `TeacherSwing`
- Fran 当前待机：同文件中的 `HeldTeacherVisual`
- Fran 当前粒子：`Content\Projectiles\FranParticles.cs`

