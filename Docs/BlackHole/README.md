# 引力坍缩仪 / Singularity Focus

新增独立魔法武器，内部物品名 `SingularityFocus`。使用原版最终棱镜图标；无需额外安装众神之怒。

## 使用

- 工作台制作：10 坠落之星、8 陨石锭、1 钻石。
- 按住左键生成并持续维持黑洞，黑洞平滑跟随鼠标，操控距离最多 800 像素。
- 基础魔法伤害 90，核心每 15 更新帧可命中同一敌人一次。
- 使用时消耗 4 魔力；维持每 30 更新帧再消耗 4 魔力。
- 松开、切换物品、控制受限、死亡或魔力不足时，约半秒内缩小消失。
- 本武器的黑洞全世界最多一个，收缩期间也占用名额。没有额外全屏闪白、震屏或声音循环。

## 文件

- `Content/Items/SingularityFocus.cs`：武器和配方。
- `Content/Projectiles/BlackHole/SustainedBlackHole.cs`：持续施法、移动、消散、核心碰撞、统一单实例选择。
- `Content/Systems/BlackHole/SingularityLimitSystem.cs`：服务器端剔除同时召唤造成的重复弹幕。
- `Content/Systems/BlackHole/SingularityRenderer.cs`：固定 384×384 局部画布、游戏画面透镜、绘制与卸载。
- `Assets/AutoloadedEffects/Shaders/BlackHole/FranSingularityBody.fx/.fxc`：75 步三维圆环体积采样。
- `Assets/AutoloadedEffects/Filters/FranSingularityLens.fx/.fxc`：以黑洞自身为中心的局部背景扭曲。
- `Assets/Textures/BlackHole/AccretionNoise.png`：256×256 原创周期噪声。
- `Assets/Textures/BlackHole/TransparentPixel.png`：1×1 透明占位。
- `Localization/*.Singularity.hjson`：独立中英文条目。

## 渲染设计与来源

三维主体参考并改编自 Lucille Karma 的 WrathOfTheGodsPublic：

https://github.com/TheFifthCircle/WrathOfTheGodsPublic/blob/7cb5b86c770e73d6853749b2b688d478ba3326a7/Assets/AutoloadedEffects/Shaders/SkyAndZoneEffects/RealBlackHoleShader.fx

保留其 75 次采样、受中心影响的路径、压扁圆环、0.32 弧度观察倾角、橙红吸积盘和高光轮廓。采用宠物版类似的主体／屏幕透镜分离结构，主体渲染于局部透明画布；透镜围绕实际黑洞中心运作。暗部覆盖并入主体程序，减少额外绘制。

参数和 Shader 名称使用 Fran 独立前缀；纹理由本目录 `generate_noise.py` 生成，使用固定种子的周期多层值噪声、坐标扰动与细丝纹理，不复制参考模组图片。执行 `python Docs/BlackHole/generate_noise.py` 可复现资源，仅依赖 Python 标准库。

384×384×75 约为 1106 万次体积步进／绘制帧，外加一个较简单的屏幕滤镜；没有黑洞或不在视野内时跳过主体绘制。该估算不等同于实测帧率。

联机使用标准弹幕同步，按 owner/identity 做确定性选择；绘制和伤害只承认一个胜出实例，服务器删除其余实例。即使两名玩家同时施法，也不会在同一客户端叠画两个黑洞。网络延迟期间各客户端最初可能看到自己的预测实例，最终由服务器同步收敛。

## 检查记录

2026-09-28：

- C# 源码编译成功，0 警告、0 错误。
- 两份 HLSL 均通过 FXC 编译，已附带 `.fxc`，无需运行时临时编译；FXC 仅给出其 Effects API 通用弃用提示。
- 使用 CPU 重算主体公式检查吸积盘、亮弧和黑色中心的形状；该结果不是游戏截图，也不证明 GPU／游戏内表现。
- 检查了自动加载路径、纹理资源、中英文本、主体合成阶段、消散期间数量限制和服务器重复清理逻辑。
- 未启动游戏、未生成或安装 `.tmod`；画面缩放、反重力、实际帧率和双客户端联机仍待游戏内验收。

本次只新增武器相关文件。原有角色、战术、师父和其他未提交修改不属于本次修改范围。
