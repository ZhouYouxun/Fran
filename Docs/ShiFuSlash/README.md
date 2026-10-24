# 师父普通挥舞材质重做

本次仅替换普通连段前三刀的刀光材质、弧面几何及旧刀盘叠层。第四刀轴向推出、技能、判定、伤害、速度、音效与现有粒子机制不在本次修改范围内。拔刀技能仍使用原绘制器。

参考本地灾厄 Exoblade 的连续曲面、明暗分层、亮刃与尾部消散。新 FX、XNB 和两张噪声图均由本项目制作，未引入灾厄的着色器或贴图。

- `Effects/FranShiFuSlash.fx`：双层流动噪声、域扭曲、侵蚀、冷色亮刃；预乘透明主体与独立加法发光两遍绘制。
- `Core/Graphics/FranShiFuSlashRenderer.cs`：97 个角度采样组成连续弧面，半径限制、有限值检查、绘制状态恢复。
- `Content/Projectiles/ShiFuCombatRebuild.cs`：从实际 `ComputePose` 取样，外缘跟随可见剑尖，蓄势不提前出现扇面，收刀时尾部追上剑尖。
- `Assets/OriginalVFX/ShiFu/Flow.png`、`Erosion.png`：512×512 中性灰度无缝纹理；由 `generate_materials.py` 使用固定随机种子制作，可复现。

验证入口：`Tests/ShiFuSlash/Render.csproj`。需要 tModLoader 的原生库目录加入 PATH，从项目根目录运行。验证使用实际渲染器、实际 `ComputePose` 与新 XNB，通过隐藏 GPU 窗口导出前三刀/左右方向/明暗背景共 96 帧，同时检查人物中心空白、绘制半径边界、可见刀光不为空和 SpriteBatch 恢复。此检查不是游戏内体验或多人联机测试。

另生成 36 帧冻结姿势的流动材质预览，确认时间参数改变实际 GPU 输出；`make_preview.py` 将其整理成带说明的动图。包检查确认 DLL 与 XNB 和本次构建字节一致、两张 rawimg 噪声图存在。

本地 XNBCompiler 放在 `bin/ShiFuSlashValidation/compiler` 的独立副本中使用，不改共享编译工具。工具可能在报告编译成功后退出异常；新 XNB 已通过实际 GPU 加载和绘制验证。

游戏占用正式 Fran.tmod 时，使用 `-tmlsavedirectory` 指向独立 Sandbox 打包。最终文件置于 `bin/Delivery/Fran-ShiFuSlash.tmod`；安装时文件名应为 `Fran.tmod`。游戏运行中不强制覆盖占用文件。
