# Fran主菜单背景制作与接入交接报告

## 1. 当前实现

Fran已经注册了一个独立的tModLoader主菜单主题：

- 代码：`Content/MainMenu/FranMainMenu.cs`
- 背景：`主菜单背景/FranMenuBackground.png`
- 配置：`FranClientConfig.EnableCustomMainMenu`，默认开启
- 音乐：暂用原版 `MusicID.Title`
- Logo：保留原版Terraria/tModLoader Logo
- 动态层：代码实时绘制金橙色下落余烬与柔光，不依赖视频或着色器

关闭配置后，Fran主题会通过 `ModMenu.IsAvailable` 从可选主题中退出；它只影响当前客户端，不参与世界存档或多人同步。

## 2. 图片来源与本轮处理

源画面来自TV动画《转生成为魔剑Ⅱ》官网的无文字战斗主视觉：

- 官网：`https://tenken-anime.com/`
- 源图：`https://tenken-anime.com/assets/top/vis-h1.webp`

源图是3000×4220竖图。本轮通过身份保持型图像编辑扩展成1672×941横屏PNG：保留弗兰、师父、敌人、服装、表情和原本的橙火/紫焰关系；补全左右洞窟环境，在左侧留出原版菜单按钮的可读区，并去除文字、Logo、日期和水印。最终图片约为16:9，运行时仍使用cover算法适配其他宽高比。

仓库不启用仍在审查的 `Textures/FranArtwork/Portraits`。

## 3. 绘制流程

`FranMainMenu.PreDrawLogo` 每帧执行以下顺序：

1. 以 `AlphaBlend + LinearClamp` 把背景按cover模式铺满全屏。
2. 叠加很轻的全屏压暗和左侧渐变遮罩，保证原版菜单文字可读。
3. 切换到Additive绘制下落余烬；辉光复用Fran原创 `FranBloomCore`，火芯使用原版 `MagicPixel`。
4. 恢复tModLoader期望的SpriteBatch状态并返回 `true`，继续绘制原版Logo和按钮。

背景铺满公式：

```text
xScale = screenWidth / textureWidth
yScale = screenHeight / textureHeight
scale = max(xScale, yScale)
drawSize = textureSize * scale
drawPosition = (screenSize - drawSize) / 2
```

余烬列表有数量上限，纹理通过 `ModContent.Request` 缓存；菜单不会逐帧创建Texture2D。专用服务器因客户端自动加载限制而不会注册该菜单类。

## 4. 后续换图规则

若画师交付正式背景，只需覆盖 `主菜单背景/FranMenuBackground.png`，资源路径和代码无需改变。建议：

- 3840×2160 PNG；最低2560×1440。
- 不把菜单文字或Logo烘进图片。
- 顶部中央预留Logo区，左侧保留按钮可读区。
- 角色脸部、师父剑身与关键光效放在中央70%安全区，以承受4:3和超宽屏裁切。
- 非像素插画使用平滑缩放，不要改成 `PointClamp`。

如果未来提供专属Logo，可新增独立透明PNG并由代码绘制；背景图本身仍保持无字。

## 5. 实机验收清单

静态构建不能替代以下实机检查：

1. 在主菜单主题切换中选择Fran，确认背景、原版Logo和按钮都正常。
2. 关闭/开启“启用弗兰主菜单界面”，确认主题可用性变化正确。
3. 检查1920×1080、2560×1440、16:10、4:3和超宽屏是否无黑边，人物脸与剑身没有被裁坏。
4. 检查不同UI缩放下左侧按钮仍清楚、落焰不会遮住文字。
5. 进入世界、退出世界、切换主题和重载模组时没有SpriteBatch状态异常或残留粒子。
6. 专用服务器不加载背景纹理，也不执行任何菜单绘制。

## 6. 视频边界

主菜单现在是“静态横屏图＋轻量实时粒子”，不是视频。以后即使增加剧情视频，也不要让菜单或玩法进度依赖视频解码；视频接入细节见 `Video/README_视频播放接入说明.md`。
