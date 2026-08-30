# Homeward Journey 近战挥舞审查

## 1. 样本

本地模组包：`C:\Users\wangk\Documents\My Games\Terraria\tModLoader\Mods\ModPacks\HOME01\Mods\ContinentOfJourney.tmod`

本轮读取的解包源码：`C:\Users\wangk\AppData\Local\Temp\Codex-HomewardJourney-0.8.70.88`

本地版本：`0.8.70.88`

公开清单：[Homeward Journey Weapons](https://terrariamods.wiki.gg/wiki/Homeward_Journey/Weapons)

## 2. 总结先行

“旅人大多数剑还是原版 swing”只说对了一半。

- 单把剑的基础物品动画大多确实保留 `ItemUseStyleID.Swing`，没有像灾厄那样每把剑都用持握弹幕完全接管手臂和武器。
- 但多把专属剑会在原版挥舞上叠加独立 `_Slash` 弹幕，扩展判定和大面积 Additive 斩痕。
- 更重要的是，旅人有一套全局 `GiantSlashGlobalItem + GiantSlash` 系统。装备腰带后，它能接管符合条件的模组剑与原版剑，把普通挥舞替换成可蓄力的 270° 自定义大挥砍。

所以旅人不是“几乎没有自定义挥舞”，而是把自定义挥舞集中放进一套横跨多把武器的装备系统，而不是平均分散到每把剑里。

## 3. 本地近战入口的量化结果

静态扫描 `Items` 下继承 `ModItem` 且设置 `Item.DamageType = DamageClass.Melee` 的入口，得到 81 个本地类。按使用方式和是否隐藏物品贴图分组：

| 使用形式 | 数量 | 主要含义 |
|---|---:|---|
| Swing，隐藏贴图且关闭物品近战 | 36 | 投刀、回旋镖、独立弹幕、特殊工具；不是持握自定义剑 |
| Shoot，隐藏贴图且关闭物品近战 | 18 | 长枪、yoyo、flail 和其他离手近战 |
| Swing，显示物品且保留物品近战 | 17 | 主要的宽剑/普通剑；大多仍用原版摆臂 |
| Thrust，显示物品且保留物品近战 | 8 | 基础 Rapier 系列；原版式刺击并附玩家冲刺 |
| Swing，隐藏贴图但保留物品近战 | 1 | Zodiac Sign 的特殊混合配置 |
| Thrust，隐藏贴图且关闭物品近战 | 1 | Propeller Rapier 的独立弹幕版本 |

这 81 个入口不能等同于 81 把自定义挥舞武器。大量内容只是近战伤害类型的投掷物或 yoyo。

## 4. 官方近战分类与本地意义

### 4.1 Swords

官网列出的主要剑：

- Pre-Hardmode：Enchanted Blade、Kukri、Vein。
- Hardmode：Ifrit、Inside Job、Shoreline、Sword of Flower、Windy Scythe。
- Post-Moon Lord：Anger、Cosmic Broadsword、Da Capo、Destroyer Blade、Doctor Expeller、Frostgrief、Fury、It Lived、Materialism、Passione、Sweep Hand、Zodiac Sign。

本地源码里可直接找到的显示式 Swing 剑入口包括：`CosmicBoardsword`、`DaCapo`、`DawnsBorder`、`DestroyerBlade`、`DoctorExpeller`、`EnchantedBlade`、`InsideJob`、`Kukri`、`Lifesaber`、`Shoreline`、`SwordOfFlower`、`Tizona`、`TornadoScythe`、`Vein` 等。

它们不都拥有完整持握控制器。典型做法是：原版物品挥舞负责角色姿势，`UseAnimation` 额外生成一枚跟随动画的斩痕弹幕。

本地 17 个“显示物品并保留物品近战”的 Swing 入口可进一步拆成：

| 本地类 | 基础动作 | 额外行为 | 是否完整接管持握 |
|---|---|---|---|
| CosmicBoardsword | 原版 Swing | 额外射击、专属大斩痕、扩大物品命中范围 | 否 |
| DaCapo | 原版 Swing | `DaCapoSlash`、自定义命中范围 | 否 |
| DawnsBorder | 原版 Swing | `_Slash` 与额外发射物；含强化版本 | 否 |
| DestroyerBlade | 原版 Swing | 发射 TornadoScythe | 否 |
| DoctorExpeller | 原版 Swing | 发射 Apple 系弹幕 | 否 |
| EnchantedBlade | 原版 Swing | 以物品/命中附加为主 | 否 |
| HammerheadDrill | 原版 Swing | 工具/特殊附加行为 | 否 |
| InsideJob | 原版 Swing | `InsideJob_Slash`、扩大命中范围 | 否 |
| Kukri | 原版 Swing | `Kukri_Slash` 与额外刀刃 | 否 |
| Lifesaber | 原版 Swing | 物品附加效果 | 否 |
| ShadowPickaxe | 原版 Swing | 工具，不应算专属剑 | 否 |
| Shoreline | 原版 Swing | `Shoreline_Slash` 与水系发射物 | 否 |
| StoneTouch（文件 SilkTouch） | 原版 Swing | 物品附加效果 | 否 |
| SwordOfFlower | 原版 Swing | `SwordOfFlower_Slash`，命中生成花系攻击 | 否 |
| Tizona | 原版 Swing | `Tizona_Slash` 与 FireBolt | 否 |
| TornadoScythe | 原版 Swing | 发射 TornadoScythe | 否 |
| Vein | 原版 Swing | 物品/命中附加为主 | 否 |

这里的“否”很关键：专属 Slash 可能完全接管视觉判定，却没有像 Calamity 持握弹幕那样统一控制武器贴图、手臂和三段相位。

### 4.2 Spears 与 Rapiers

Spears 基本走独立发射弹幕；Rapier 基础系列则直接使用 `ItemUseStyleID.Thrust`。

铜、铁、铅、银、钨、锡、金、铂 Rapier 共用一套非常直接的身体推动逻辑：

- 起手将 `player.velocity.X` 设为面向方向乘 10。
- 冷却末段再给水平速度。
- 命中后按目标速度反弹玩家，通常还给负 Y 速度。

Propeller Rapier、Wild Pointer、Spear of Escape 等也使用 `RapierTimer` 组织冲刺/冷却。

这套设计能制造“刺进去再弹回”的玩法，但与用户对 Fran 左键“绝不强制位移”的要求冲突，不能作为第四段突刺的移动参考。Fran 只能学它的攻击轴线，不能学速度写入。

### 4.3 Flail、Yoyo、Boomerang、Thrown items

这些内容大量使用 `noUseGraphic = true`、`noMelee = true` 和独立弹幕。它们属于近战伤害类型，却不属于本轮要研究的“手持师傅怎么挥”。

公开类别包括：Falling Action；Casta、Jugemo、Zounic、Eye of the Stomp、Fifteen o'Clock、Moth To Fire；Rolling Cutter、Quick Boomerang、Bloomerang、Rising Action；以及整套飞刀和投掷近战。

它们对 Fran 的意义主要在右键战术概念，而非左键动作。

## 5. 专属剑的 Slash 叠加层

本地明确存在的专属斩痕类包括：

- `DawnsBorder_Slash`
- `TrueDawnsBorder_Slash`
- `InsideJob_Slash`
- `Kukri_Slash`
- `MyRecitalAndYours_Slash`
- `Shoreline_Slash`
- `SwordOfFlower_Slash`
- `Tizona_Slash`
- `DaCapoSlash`

对应的物品入口会在 `UseAnimation` 生成斩痕；如果腰带的 `GiantSlash` 已启用，通常会跳过这枚专属斩痕，避免两套大挥舞重叠。

以 `DawnsBorder_Slash` 为例：

- 时间长度读取玩家物品动画。
- 弹幕位置使用 `cos/sin` 形成半圆运动，同时逐渐扩大半径。
- 绘制时反复结束/开始 SpriteBatch，切到 Additive。
- 一张 170×多帧斩痕图负责主体，再叠 `SlashImage_1`、`SlashImage_4`、`SlashImage_5` 作为白芯、端点 flare 和外光。
- 判定不是物品小矩形，而是随半径扩张的圆形/区域近似。

优点：制作成本低，一套素材能产生非常大的视觉面积。缺点：刀光比刀身更像一个扩张的扇形法术，动作几何没有灾厄那样严密。

## 6. GiantSlash / 腰带系统

源码：

- `Projectiles\Meelee\GiantSlash.cs`
- `Items\Accessories\MeleeExpansion\Belts.cs`

官网说明：[Belts](https://terrariamods.wiki.gg/wiki/Homeward_Journey/Belts)

### 6.1 接管条件

`GiantSlashGlobalItem` 记录物品原始的 channel、noMelee、autoReuse、useTurn、noUseGraphic。对于符合以下条件的物品：

- 近战伤害类型。
- Swing 使用方式。
- 原本不是 noMelee。
- 不是镐、斧、锤工具。
- 或在专门维护的原版剑 ID 表中。

装备腰带后，它会临时把物品改为 channel、noMelee、隐藏贴图，并生成 `GiantSlash` 持握弹幕。未启用时恢复原始字段。

### 6.2 蓄力与挥动

- 蓄力期间持续把物品动画/使用时间维持在最大值。
- 充能计数按近战攻速平方和物品动画长度平方根换算。
- 约在 90、150、210、270 的内部计数达到四个强化档位。
- 腰带只决定允许达到的最大档位。
- 松开后固定方向，执行约 270° 的大挥砍。
- 旋转增量使用正弦分布，中段更快、两端更慢。
- 攻击判定使用从玩家手部到刀尖的线段，宽度 18。
- 充能档位提高范围、伤害、暴击和原武器发射物数量。

### 6.3 绘制

- 读取玩家当前持有物的真实贴图并自行绘制。
- 首次绘制时遍历贴图像素，计算非透明像素的平均颜色。
- 根据武器贴图对角线估算刀长，并对少数原版剑做长度特例。
- Additive 下叠加武器白光、彩色大斩痕、多层白芯和端部光斑。
- 充能档位越高，斩痕尺寸越大。

这套思路很适合“让大量不同剑都获得同一个系统动作”，也是旅人与灾厄、救赎最大的区别。

### 6.4 它为什么不适合直接放进 Fran

`GiantSlash.OnHitNPC` 为了保留原剑效果，手工兼容多个原版剑，并主动调用 `NPCLoader`、`PlayerLoader`、`ItemLoader` 的命中钩子；发射物部分也有大量原版类型特判。这是一套面向“任意剑”的兼容层，复杂度来自兼容面，而不是动作本身。

Fran 只有一把确定的传奇武器，不需要：

- 动态扫描武器贴图求平均色。
- 维护任意原版剑 ID 与发射物对应表。
- 手动转发其他物品的命中钩子。
- 用同一张大斩痕处理每一个攻击阶段。

Fran 应学习“一个系统控制整条全流程成长线”，但应使用预先设计的四段动作和专属视觉素材。

## 7. Homeward Journey 的特效技术路线

旅人没有显示出与灾厄同等统一的 Primitive Trail 挥舞基类，也没有救赎那样独立的大型粒子管理层。它的近战视觉更常见以下组合：

1. 普通 `ModProjectile` 计算 sin/cos 轨迹。
2. 在 `PreDraw/PostDraw` 内手工切换到 Additive。
3. 重复绘制几张 `SlashImage_*`，用不同颜色、尺寸、帧区和旋转组成刀光。
4. 使用 Dust 补充边缘速度感。
5. 用自定义大范围碰撞补足原版物品判定。

优点是直接、产量高、容易让画面“很大”；不足是 SpriteBatch 状态切换散落在弹幕内，斩痕几何与刀身可能不完全一致。

## 8. 给 Fran 的直接提炼

值得吸收：

- 用一套统一控制器管理一把传奇武器从开局到终局，而不是进度每提升一次就换掉整个攻击实现。
- 强化档位只放大明确的动作参数：范围、伤害窗口、刀光厚度和接触反馈。
- 专属招式与全局替换系统必须互斥，避免两套刀光叠在一起。
- 武器贴图、刀身判定和斩痕颜色需要建立明确关联。

不应吸收：

- Rapier 对玩家水平速度的强制覆盖。
- 用一张通用大扇形斩痕表达所有斩击。
- 为一把确定武器建立“兼容任意剑”的大量特判。
- 在每个弹幕里随意 `SpriteBatch.End/Begin`；Fran 应集中管理 Additive 绘制通道。

## 9. 最终定位

Homeward Journey 不是 Fran 左键轨迹的最佳参考；Calamity 明显更适合。它最值得借鉴的是“系统覆盖面”和“用装备/成长状态统一改造基础武器”的产品思路，以及低成本多层 Additive 斩痕的制作经验。
