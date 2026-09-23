# 蝶变共鸣：狮源流星小星光与大星光

## 资源范围

- `Particles/Leonid/StarMote.png`：小十字星光，内置 image_gen 依据文字独立生成。
- `Particles/Leonid/StarShard.png`：大四芒星碎片，内置 image_gen 依据文字独立生成。
- 未向图像生成器提交 CalamityMod 贴图作为参考或编辑输入。
- PNG 保留生成结果的原始像素与透明通道，绘制时按小星光 512、大星光 165 的原始像素标尺折算缩放。
- 辅助柔光与锁定环由 `LeonidStarlightMaterials` 的数学函数生成并缓存，不读取第三方贴图。
- 不引入北极星爆发大幕、Halo、Needle 或其贴图。
- 上次对九张既有共享贴图的覆盖已撤销，恢复到本次迁移前的 Git 版本；这里的两种星光不再引用这些共享贴图。这不是对模组其他既有素材的授权审计。
- 无引用的 `Particles/FranLegacyBrinyGhost.png` 已删除，可以通过 Git 历史恢复。

## 实现范围

保留用户提供的 LeonidProgenitor 星光运动状态、配色和无伤害定位。拖尾实际调用 `DrawStarTrail`：从旧到新叠绘历史位置，尺寸由 0.3 倍递增到 1 倍，透明度按进度平方衰减，每段旋转 0.18 弧度。使用新的独立实现和本地 AI 贴图表达这个绘制思路。

挥刀释放一小一大；蝴蝶起步向后释放两颗小星光；命中时释放约 3–4 颗较小星光（沿用原批量粒子的 80% 数量保留机制）。粒子容量耗尽时仍受本地性能上限限制。

## 生成提示词

### StarMote

Use case: stylized-concept. Asset type: original standalone 2D game particle texture for additive blending. Create exactly ONE small delicate luminous cross-shaped star mote at the exact center of a square transparent PNG. Pure white/grayscale light only, runtime code will tint it blue-violet. Four very slender tapering cardinal rays, vertical rays slightly longer than horizontal, a tiny bright center, subtly soft edges. Star tip-to-tip span 80% of the canvas height and 65% of width. Fade all the way into true transparent alpha; empty transparent corners and generous transparent margins. No other stars, rings, dust, lettering, frames, backgrounds, checkerboard pattern, shadows, weapons, scenery or watermark. Smooth clean VFX texture, readable when scaled to 24-36 pixels. Independently designed from this text, do not imitate any existing game asset. Output only the transparent sprite.

### StarShard

Use case: stylized-concept. Asset type: original standalone 2D game particle texture for additive blending. Create exactly ONE luminous four-point star fragment centered on a square transparent PNG. Pure white/grayscale light only, runtime code will tint it. A slightly substantial brilliant diamond-shaped center with four sharp smoothly tapered cardinal tips; softly concave sides between the tips. Vertical tips slightly longer, no extra rays. Tip-to-tip width and height roughly 80% of canvas, balanced central mass and thin delicate tips. Tiny soft luminous edge, no large cloudy halo. True transparent alpha outside the star with clear margins and empty corners. No extra stars, circles, symbols, dust, text, background, checkerboard pattern, weapon silhouettes or watermark. Smooth clean VFX sprite readable at 30-50 pixels. Independently designed from this text, do not imitate any existing game asset. Output only the transparent sprite.
