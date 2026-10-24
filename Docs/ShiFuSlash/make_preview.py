from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[2]
out = root / 'bin/Delivery'
out.mkdir(parents=True, exist_ok=True)
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 21)
small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
frames = []
for source in sorted((root / 'bin/ShiFuSlashValidation/animation').glob('*.png')):
    frame = Image.open(source).convert('RGB')
    draw = ImageDraw.Draw(frame)
    draw.text((24, 15), '师父 · 普通挥舞材质重做', font=font, fill=(221, 240, 250))
    for x, text in [(210, '第一刀'), (630, '第二刀'), (1050, '第三刀')]:
        draw.text((x, 440), text, font=font, anchor='mm', fill=(169, 221, 238))
    draw.text((1235, 24), '离线 GPU 材质预览 · 非游戏截图', font=small, anchor='rm', fill=(143, 161, 180))
    frames.append(frame)
frames[12].save(out / 'ShiFuSlash-preview.png')
frames[0].save(out / 'ShiFuSlash-preview.gif', save_all=True, append_images=frames[1:], duration=50, loop=0)
print('Saved offline preview PNG and GIF to bin/Delivery')
