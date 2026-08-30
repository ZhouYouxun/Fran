# Calamity 自定义挥舞架构审查

## 1. 样本

本地根目录：`C:\Users\wangk\Documents\My Games\Terraria\tModLoader\ModSources\CalamityMod`

本地版本：`2.2.2`

本报告以用户截图中的 “Special swords” 为主要范围，并额外检查用户明确点名的 Earth、Exoblade 和 Gilded Proboscis。

## 2. Calamity 不是只有一套自定义挥舞

本地源码至少存在四种层次：

1. `BaseSwordHoldoutProjectile`：规范化程度最高的通用挥舞/突刺基类。
2. `BaseCustomUseStyleProjectile`：只统一持握、手臂和使用生命周期，具体运动由武器自写。
3. 完全独立的武器状态机：Exoblade、Biome Blade、Grand Dad、Auger 等。
4. 普通物品使用逻辑 + 独立视觉/攻击弹幕。

因此，学习 Calamity 时不能只搜索一个“自定义挥舞基类”，也不能把 Earth 当成灾厄全部近战的代表。

## 3. 两个基础框架

### 3.1 BaseSwordHoldoutProjectile

源码：`Projectiles\BaseProjectiles\BaseSwordHoldoutProjectile.cs`

这是最适合作为 Fran 左键架构参考的部分。

它统一了：

- 物品与持握弹幕的配对入口 `BaseSwordHoldoutItem`。
- `StartupTime`、`swingTime`、`CooldownTime` 三段时间。
- `inStartup`、`inSwing`、`inCooldown` 三个明确状态。
- `StartupCompletion`、`SwingCompletion`、`CooldownCompletion` 进度。
- `SwingFunction()` 作为可覆写的轨迹曲线；默认用 SmoothStep 完成一段弧。
- 挥舞宽度、起手/收招角度、武器与玩家距离、交替挥向。
- 近战攻速和武器尺寸修正。
- 手臂复合姿势、`heldProj`、朝向、旋转历史。
- 刀身线段碰撞，而不是只靠弹幕小方框。
- 可选的 Primitive Trail、颜色函数、宽度函数和 Shader。
- `shootCheck()`，把发射物事件绑定到挥舞时刻。
- ExtraAI 网络同步。

这个基类最关键的优点是：动作进度、刀身位置、碰撞和轨迹可以使用同一套数据。

### 3.2 BaseCustomUseStyleProjectile

源码：`Projectiles\BaseProjectiles\BaseCustomUseStyleProjectile.cs`

这是一个更宽松的“持握容器”。它统一：

- 绑定物品 ID 和防止离手后继续存在。
- 玩家前/后手臂姿势。
- 武器绝对位置、旋转偏移、手臂偏移。
- 使用开始、持续、结束的生命周期钩子。
- `CanHit` 和碰撞入口。

但它不替武器决定挥舞曲线。Earth、Holy Collider、Mantis Claws 等都在 `UseStyle()` 中自写运动。它适合特殊动作，不适合直接保证 Fran 四段左键的一致手感。

## 4. 用户点名的三把参考武器

### 4.1 Earth：连段节奏参考，不可照搬命中推动

源码：`Projectiles\Melee\EarthHoldout.cs`

Earth 继承 `BaseCustomUseStyleProjectile`，自己维护 `swingCount`、使用时长、停顿、淡入和刀刃发光。单双数挥击使用不同起止角，核心插值使用 `ExpInOutEasing`，使中段突然加速，起手和末端相对收敛。

值得吸收：

- 连段编号直接决定轨迹，而不是每次随机。
- 加速发生在挥击中段，能够制造“先带动重量，再爆发通过目标”的感觉。
- 刀光方向与武器最终旋转一致。
- 普通斩与更大范围挥击能共用一套持握框架。

明确排除：

- `OnHitNPC` 调用 `target.MoveNPC(...)` 强制移动敌人。用户已明确要求 Fran 左键不能造成强制位移，所以只能学轨迹和节奏，不能学命中推动。

### 4.2 Exoblade：强化第三段的速度曲线和刀光生成参考

源码：`Projectiles\Melee\ExobladeProj.cs`

Exoblade 是独立状态机，不继承上述两种挥舞基类。它把攻击分成普通挥击与 `BonkDash` 两种状态。

普通挥击的关键：

- 使用分段曲线 `SlowStart`、`SwingFast`、`EndSwing`。
- 中段快速通过目标，末段减速。
- `DirectionAtProgress()` 同时服务武器方向和刀光采样。
- `GenerateSlashPoints()` 生成连续弧线，使用 `PrimitiveRenderer` 和 `ExobladeSlash` Shader 绘制。
- 武器本体还通过 `SwingSprite` Shader 做动态压缩/拉伸，而不仅是旋转一张贴图。
- 能量束发射只发生在挥击时间内的指定窗口。

这非常适合 Fran 第三段“更快、更严重的第一段强化版”：同一方向语义，更短的启动、更陡的中段、更窄但更亮的刀路、接触帧更强。

明确排除：

- `BonkDash` 会直接写 `Owner.velocity`，结束时还会把速度乘 0.2，并在命中后反弹。这是特殊突进状态，不能进入 Fran 普通左键。
- Exoblade 的光束、爆炸、吸血等并不是“更强斩击”的必要组成，不应一起照搬。

### 4.3 Gilded Proboscis：第四段突刺参考

源码：`Projectiles\Melee\Spears\GildedProboscisProj.cs`

它继承 `BaseSwordHoldoutProjectile`，把 `OffsetDistance` 当作枪尖前伸深度：

- 启动阶段先把武器收回。
- 有效攻击阶段按幂函数把前伸距离从约 -40 推到 90。
- 收招阶段将武器回收到安全距离。
- 线段碰撞长度为 196。
- `SwingFunction()` 只保留较小角度变化，攻击主体由轴向位移表达。

这说明“像长枪一样戳一下”不应该使用大圆弧贴图假装突刺，而应使用：固定轴线 + 刀尖前伸 + 线段碰撞 + 轴向残影。

其蓄力/特殊分支有额外位移与粒子逻辑；Fran 普通第四段只取几何和相位，不取玩家速度写入。

## 5. 截图中 Special swords 的源码分类

| 武器 | 主要控制器 | 架构族 | 侦查结论 |
|---|---|---|---|
| Basher | 独立/CustomUse 入口 | 宽松持握 | 早期简单特种剑，不作为最终轨迹模板 |
| Broken Biome Blade | `PureClarity` 等姿态 | BaseCustomUse | 一把武器按 attunement 切动作；适合学模式组织 |
| Old Lord Claymore | `OldLordClaymoreHoldout` | BaseCustomUse | 自写重剑动作 |
| P-GSS Auger | `DraedonsArsenal\AugerHoldout` | 独立状态机 | 普通交替斩、右键吸引区、强化大斩；系统耦合较高 |
| Baleful Harvester | `BalefulHarvesterHoldout` | BaseCustomUse | 镰刀式自写动作 |
| Biome Blade | `TrueBiomeBlade`/多个姿态 | CustomUse + 独立视觉持握 | 多姿态组合，不是一条通用剑轨迹 |
| Comet Quasher | `CometQuasherHoldout` | BaseCustomUse | 重型武器状态 |
| Exalted Oathblade | `ExaltedOathbladeHoldout` | BaseCustomUse | 持握斩 + 次级投剑 |
| Forbidden Oathblade | `ForbiddenOathbladeHoldout` | BaseCustomUse | 与 Exalted 同族但内容不同 |
| Grand Guardian | `GrandGuardianHoldout` | BaseCustomUse | 自写防守/攻击姿态 |
| Hellkite | `HellkiteHoldout` | BaseCustomUse | 自写重剑/火焰表现 |
| Lucrecia | `LucreciaHoldout` | BaseSwordHoldout | 使用规范化相位与线段碰撞 |
| Majestic Guard | `MajesticGuardHoldout` | BaseCustomUse | 偏防守姿态 |
| Mantis Claws | `MantisClawHoldout` | BaseCustomUse | 短程快速动作 |
| Devil's Devastation | `DevilsDevastationHoldout` | BaseCustomUse | 持握 + 投掷刀刃 |
| Earth | `EarthHoldout` | BaseCustomUse | 交替斩、强中段缓动、会推敌人 |
| Grand Dad | `GrandDadHoldout` | 独立状态机 | 不走两套通用基类 |
| Holy Collider | `HolyColliderHoldout` | BaseCustomUse | 大型重武器姿态 |
| Maw of Infinity | `MawOfInfinityHoldout` | BaseSwordHoldout | 规范化相位族 |
| Mirror Blade | `MirrorBladeProjectile` | BaseSwordHoldout | 规范化相位族 |
| The Mutilator | `MutilatorSwordProj` | BaseSwordHoldout | 规范化相位族 |
| Stellar Striker | `StellarStrikerHoldout` | BaseCustomUse | 自写重击动作 |

这张表说明截图里的武器也不是“全都由一个 Custom Swing 系统生成”。灾厄真正统一的是几种底层契约，武器仍允许完全自写。

## 6. Calamity 的特效组织

### 6.1 连续几何优先

高阶剑光常由轨迹采样点、宽度函数、颜色函数和 Shader 共同生成。刀光不是固定尺寸的单张扇形贴图，因此能随武器弧度、速度和长度改变。

### 6.2 历史轨迹与当前轨迹分离

`oldRotations` 或自建采样点描述过去路径；武器当前位置只负责当前刀身。这样残影能延迟消散，碰撞却只在有效窗口发生。

### 6.3 Shader 只负责材质，不负责解释动作

Exoblade 的 Voronoi、Eternity Streak、火色参数等只是给已经正确的轨迹上材质。先有正确几何，再有 Shader；反过来做只会得到漂亮但不明所以的光团。

### 6.4 武器本体也参与动感

Exoblade 在挥击时做压缩/拉伸；部分武器有轮廓、尖端 flare、武器 glowmask。刀身和刀光不是两个互不相干的对象。

## 7. Calamity 与 Redemption 的核心区别

| 维度 | Calamity | Redemption |
|---|---|---|
| 共用动作框架 | 强；至少有规范相位基类和宽松持握基类 | 弱；`TrueMeleeProjectile` 很薄，曲线多在单武器内 |
| 刀光主体 | 连续采样轨迹、Primitive Trail、Shader | 逐帧效果贴图、Additive、少数 Primitive Trail |
| 重击表现 | 动作曲线、形变、Shader、次级事件组合 | 接触关键帧、震屏、地形冲击、逐帧贴图非常突出 |
| 碰撞 | 多用刀身线段或轨迹相关线段 | 因武器而异，统一程度较低 |
| 玩家位移 | 特殊状态常与动作深度耦合 | 锤/终局武器常直接推动或旋转玩家 |
| 适合 Fran 学什么 | 左键骨架、突刺几何、强化斩曲线 | 接触帧重量、地面反馈、逐帧特效编排 |

## 8. 对 Fran 的采用结论

左键四段应建立在 `BaseSwordHoldoutProjectile` 这一类思想上，但做成 Fran 自己更小、更明确的控制器：

- 一次批砍：中等启动、宽弧、较低尾迹厚度。
- 一次转圈：连续大角度，不靠多发飞行弹幕表达。
- 一次强化批砍：采用 Exoblade 式分段缓动和更强武器形变，但不发光束、不位移玩家/敌人。
- 一次突刺：采用 Gilded Proboscis 式轴向 Offset、线段碰撞和枪尖残影。

Earth 可以作为第一、第二段的节奏参照；Exoblade 作为第三段的速度/几何参照；Gilded Proboscis 作为第四段的轴线参照。三者都只能提炼动作结构，不能整把复制。

