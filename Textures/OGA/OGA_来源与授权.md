# 0815 下载素材：来源与使用状态

这批文件是为 Fran 的二次元技能特效收集的 **2D VFX 贴图原料**，只选了 CC0 资源。CC0 允许复制、修改、商用与随模组再发布；保留本文件是为了以后排查来源，而不是要求在游戏内署名。

## 可直接使用

| 文件/目录 | 内容 | 来源 | 授权 | 校验值（SHA-256） |
|---|---|---|---|---|
| `OGA_Radial_Lightning_Spritesheet_CC0.png` | 放射闪电精灵表 | https://opengameart.org/content/radial-lightning-effect | CC0 1.0 | `070D7244C6450EB92F3BFCE773D5D5BF871A275DE73F7DBBFB718EF856FA3D99` |
| `OGA_Radial_Lightning_Glow_CC0.png` | 带辉光的随机闪电 | https://opengameart.org/content/radial-lightning-effect | CC0 1.0 | `7366CAB30D84D5089674EB795F216D81299611EDA3C74DF849597E9459B1BC20` |
| `OGA_Radial_Lightning_Random_CC0.png` | 随机分叉闪电 | https://opengameart.org/content/radial-lightning-effect | CC0 1.0 | `D9CEDF5FFEED6E34B2B0C7132886ED4A993B95BFB33C794716034ED2D4B09A4B` |
| `OGA_Spring_VFX_Pack_CC0.zip` 与 `OGA_Spring_VFX_Pack_CC0/` | 7 张完整 JRPG 技能精灵表：樱花、藤蔓、花粉、群蜂、跳跃、胡萝卜与彩蛋爆发 | https://opengameart.org/content/spring-vfx-pack | CC0 1.0 | `CB35DDC80E3AAF4566863B21C23FCC6BE62EC82BD51204E22F756882D4765997` |
| `OGA_Fire_Wrath_Magic_Effect_CC0.zip` 与目录 | 27 帧手绘火焰斩、火环、爆发与纹章 | https://opengameart.org/content/fire-wrath-magic-effect | CC0 1.0 | `D21DDD244BA1D0B5F2FA50D74D1427DD20695481B24A40DBDD517B8F96CEAF07` |
| `OGA_Arcane_Magic_Effect_CC0.zip` 与目录 | 42 帧奥术投射物、魔法阵与能量爆发 | https://opengameart.org/content/arcane-magic-effect | CC0 1.0 | `E404DD03289E41C87E64A67A781D4C5DC2B91CB1A45EB54883314231F1FD7B8D` |
| `OGA_Cosmic_Time_Magic_Effect_CC0.zip` 与目录 | 26 帧时间、传送、漩涡与宇宙主题效果 | https://opengameart.org/content/cosmic-time-magic-effect | CC0 1.0 | `FCAB45AA464ADB3CD23306004C126F804CCE0BF51FF0DD194BAF4CE1FD8938C5` |
| `OGA_Angel_Wings_Effect_CC0.zip` 与目录 | 41 帧手绘翼状增益、升华与守护效果 | https://opengameart.org/content/angel-wings-effect | CC0 1.0 | `343F104E91890F4E1008A3622325C229ECDE47AC1E97ADEEA68D62B81E7120A6` |
| `OGA_Light_Magic_Effect_CC0.zip` 与目录 | 26 帧光弹、闪光、光环与命中效果 | https://opengameart.org/content/light-magic-effect | CC0 1.0 | `8B8CDA8BCBD8B143BEA75C5BF9107547204FB05B2FC4D28B9CE9823EEA6D5C4B` |

## 推荐的 Fran 用法

- **剑技命中**：三张闪电图能做短寿命爆发层，使用 Additive 绘制并染为白芯 + 青蓝/紫红边色。
- **雷鸣/爆发技能**：三张 `OGA_Radial_Lightning_*` 用作短寿命冲击层；不要把整张图原色平铺在角色身上。
- **自然/召唤类招式**：`OGA_Spring_VFX_Pack_CC0/20250519bloomingSakura-Sheet.png` 和 `20250520pollenDust-Sheet.png` 可作为樱花或元素碎片的完整帧动画候选；它们是成品精灵表，不应与剑气主拖尾混为同一层。
- **动漫式大招骨架**：Fire Wrath 的火环/斩击、Arcane 的魔法阵、Angel Wings 的翼状展开是完整帧动画。可以直接借用其“先构型、后爆发、再散开”的节奏，但按 Fran 的技能色与时序组合，避免把多个成品动画无节制叠在一起。
- **护盾、蓄力、魔法阵**：用粒子包的圆环/噪声/柔光图做三层不同转速的遮罩，再由代码染色。
- **斩击残影**：仍应以现在的 `SlashWide`、`SlashNarrow` 和代码生成的曲线拖尾为主体；新素材负责火花、闪光和命中收尾。
