from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[2]
out = root / 'bin/Delivery'
out.mkdir(parents=True, exist_ok=True)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 24)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 17)
frames = []
for source in sorted((root / 'bin/MindCoreBloomValidation/frames').glob('*.png')):
    frame = Image.open(source).convert('RGB')
    draw = ImageDraw.Draw(frame)
    draw.text((28, 18), '凝念核 · 困难模式新增蓝色双层柔光', font=font, fill=(220, 241, 255))
    draw.text((28, 58), '实际粒子代码 + 原有尾迹材质的离线 GPU 渲染', font=small, fill=(140, 172, 198))
    draw.text((28, 105), '1× 实际尺寸', font=small, fill=(157, 207, 233))
    draw.text((28, 262), '2.5× 光效细节', font=small, fill=(157, 207, 233))
    draw.text((28, 470), '1× 亮背景', font=small, fill=(16, 43, 63))
    draw.text((1074, 608), '仅展示新增柔光与尾迹；非游戏截图', font=small,
              fill=(21, 49, 68), anchor='rm')
    frames.append(frame)
frames[18].save(out / 'MindCoreBloom-preview.png')
frames[0].save(out / 'MindCoreBloom-preview.gif', save_all=True,
               append_images=frames[1:], duration=33, loop=0, disposal=2)
print('Saved MindCoreBloom-preview.png and MindCoreBloom-preview.gif')
