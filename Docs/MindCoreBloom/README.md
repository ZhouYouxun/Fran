# 凝念核困难模式附加柔光

本次只在 `Telekinetic_CondensedMindCore.EmitUpgradedFlightParticles` 中增加双层蓝色柔光；仍使用现有血肉墙里程碑开关。伤害、数量、追踪、速度、判定和困难模式前的绘制保持原样。

参考本地 Aetherflux Cannon 的 PhasedGodRay：主体是彩色柔光和偏白核心两层短寿命粒子，而不是激光贴图。独立的 Fran 粒子类采用相同的 7/5 帧寿命、横向收缩与纵向拉伸、轻微反向漂移、三次方淡出和加法叠加，颜色改为蓝色。不增加螺旋运动，也不增加第二条伤害弹幕。不引入外部代码或新素材；使用项目已有的 `Particles/CL_BloomCircle.png` 纯柔光图，避免当前 `BloomCircle.png` 的花纹图案。

每游戏帧固定发射三对，按已有历史位置采样；不随追踪时 extraUpdates 改变发射量。单颗凝念核稳定最多保留 36 个附加粒子，并服从 Fran 现有全局粒子预算。已有附加光环、圆环、星芒仍保留。

`Tests/MindCoreBloom/Render.csproj` 是离线预览入口：加载当前 Fran.dll，用反射调用实际 CreatePair、Step、DrawBloom；尾迹使用实际距离采样方法、当前编译材质及 Luminance PrimitiveRenderer。只建立隐藏 GPU 窗口，不启动游戏。输出 72 帧，覆盖实际尺寸、2.5 倍细节、明亮背景，并验证粒子寿命、路径及数量。预览聚焦新增柔光与尾迹，未绘制原有圆环、菱形头部、星芒及场景。

运行时将 tModLoader 的 `Libraries/Native/Windows` 加入 PATH，从项目根目录启动预览程序集，再运行 `make_preview.py` 生成聊天展示用 GIF/PNG。预览不是游戏内或多人体验验证。
