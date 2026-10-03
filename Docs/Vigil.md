# 第三大招：守望 / Vigil

## 操作与边界

- 困难模式统一解锁三个大招。默认绝影 / Shadowless，第二个视界 / Event Horizon，第三个守望 / Vigil。已有角色保留上次选择。
- 右键轮盘右侧第三格选择守望；临时使用多重分体图标；第二大招视界借用第七战术图标，不占新按键。
- 300个游戏更新帧（正常60fps为5秒）充能，原大招键（默认G）开启，再按立即退出。形态无限持续，不耗魔、不自动到期；死亡或离开世界结束。
- 切换到其他物品不取消形态，但停止挥剑和新的流星；重新手持师父恢复。形态中不再积攒大招能量，退出后重新充能。
- 不能在形态中改选其他大招。更换选择保留已充能帧数并截断到新上限，避免五秒充满绝影/视界。
- 原战术配置仅被屏蔽，不会被删除；退出恢复。旧战术弹幕不继续执行、不得从OnKill遗留战斗效果。师父常规四段连招、所有战术能量发射均不调用。
- 无额外无敌、定身、视角锁定。发动初期挥剑震动由25%渐增，3秒达到稳定强度；退出立刻停止发射并快速收尾。

## 强化普通攻击

来源：`CalamityLegendsComeBack/Weapons/YharimsCrystal/LeftGeneral/YC_LeftBladeSwing.cs` 和 `YC_AuricJudgementWave.cs`。

- 两段方向交替挥动，使用参考的45帧基础动画：70%处进入，完成后65%时间收刀。基础攻速下连续光波间隔41帧（12帧出刀+29帧收刀）。不复制参考的右键技能。
- 攻速加成接入师父现有 `GetWeaponAttackSpeed`，保持近战饰品及词缀的原有收益。参考武器使用魔法攻速，这是宿主类型适配。
- 光波本体贴图逐字节复制 `CalamityMod/Projectiles/Melee/JudgementProj.png`。五层错位绘制、金红色值、翼尖光晕、起始1.04缩放、逐更新增大、2次额外更新、130更新寿命和消散曲线沿用参考。
- 光波使用的6类粒子与SquashDust更新/绘制移植到本模组粒子管理器，使用原素材和原参数。未引入Calamity运行时依赖；没有复制其特殊世界彩蛋、非本技能使用的粒子选项、武器被动和火焰debuff。
- 按要求移除`velocity *= 0.986`和后续命中`SourceDamage *= 0.88`；无限贯穿，局部NPC免疫8次更新，与参考一致。
- 未指定伤害暂定：剑身面板1倍、光波每次命中面板4倍、流星面板2倍，集中在`Core/FranVigilRules.cs`。普通破坏性波浪本身还有穿透衰减，守望没有。

## 流星

每150帧一颗，由服务器生成；使用原版 `ProjectileID.FallingStar` 的贴图，不照搬其掉落物/日出消失AI。瞄准附近1000像素内的最近敌人，否则落在玩家周围。高处起飞、蓝白星尾、细碎晶粒、落地环；到达目标高度之前穿地形，到达后正常碰撞。客户端不重复生成。无额外范围爆炸伤害。

## 存档与联机

八个左键战术完整保留，压缩居合/旋风斩仍为ID6，雷神之锤仍为ID7。轮盘、伙伴菜单、自动选择和推荐配装均保留第七战术；它的图标同时供第二大招视界临时占位，不影响技能本身。所有存档/数据包校验使用有效技能判定。

守望形态由服务器批准并广播，加入中的玩家接收当前形态；现有大招请求复用为进入/退出请求。挥剑状态、蓄能选择、方向及节奏同步；光波遵循原版拥有者弹幕权威，流星只由服务器创建。未进行双客户端实测。

## 验证

`Tests/Runtime/VigilSmokeTest.cs` 为显式开关隔离测试；普通包不包含测试类。检查默认选择、八技能存档兼容、299/300帧边界、形态切换、旧战术屏蔽与恢复、可移动可攻击、无继承无敌、41帧挥动节拍、波形大小/贯穿/恒速/恒伤、挥剑序列化、150帧流星频率、客户端不重复流星、死亡清理、素材入包。

源码编译、隔离服务器测试与离屏渲染不代表游戏内手感和双客户端验收。

2026-09-28实测：守望隔离运行测试43项通过；视界回归124项通过；7份光波相关素材SHA256均与来源一致；FNA/D3D11隐藏窗口离屏渲染光波12/60/100更新时刻正常。测试运行使用独立的临时世界与Mods目录，无真实玩家或角色存档参与。

## 素材来源

2026-09-28修正：按用户澄清恢复完整八战术，第七技能本体与满配旋风斩保留，第二大招视界仅借用其图标。新增旋风斩实际生成及图标映射检查后，守望隔离测试47项通过。若角色曾在移除第七技能的版本中保存，其已清空的槽位需手动重新装备；不猜测或修改角色存档。

本次按用户指定的一比一移植要求，直接取自本机Calamity源码资源；SHA256比对副本一致。该记录标明来源，不替代上游许可，也不表示重新授予素材许可。

- `Textures/Particles/FranVigilJudgementWave.png` ← `CalamityMod/Projectiles/Melee/JudgementProj.png`
- `Particles/VigilGlowOrb.png` ← `CalamityMod/Particles/GlowOrbParticle.png`
- `Particles/VigilSpark.png` ← `CalamityMod/Projectiles/StarProj.png`
- `Particles/VigilSparkle.png` ← `CalamityMod/Particles/Sparkle.png`
- `Particles/VigilRing.png` ← `CalamityMod/Particles/HollowCircleHardEdge.png`
- `Particles/VigilSolidCircle.png` ← `CalamityMod/ExtraTextures/BasicCircle.png`
- Bloom复用已有`Particles/CL_BloomCircle.png`，与上游`CalamityMod/Particles/BloomCircle.png`哈希一致。
