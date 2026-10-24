"""Original periodic shader data; run with the workspace Python runtime."""
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/OriginalVFX/ShiFu'
OUT.mkdir(parents=True, exist_ok=True)
SIZE = 512
y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float64) / SIZE
rng = np.random.default_rng(184729)

def field(x, y, count, fx, fy):
    value = np.zeros_like(x)
    weight = 0
    for _ in range(count):
        kx = int(rng.integers(1, fx + 1))
        ky = int(rng.integers(1, fy + 1))
        amplitude = 1 / (1 + (kx / fx) ** 2 + (ky / fy) ** 2)
        value += np.sin(2 * np.pi * (kx * x + ky * y) + rng.uniform(0, 2 * np.pi)) * amplitude
        weight += amplitude ** 2
    return value / np.sqrt(weight)

# Domain-warped long folds, with no seams under LinearWrap sampling.
warp = field(x, y, 16, 3, 4)
folds = field(x + warp * .025, y + warp * .055, 30, 4, 19)
fine = field(x, y, 40, 13, 47)
flow = .5 + np.tanh(folds * .95 + fine * .16) * .47
erosion = .5 + np.tanh(field(x, y, 50, 19, 19) * .7 + field(x, y, 60, 73, 73) * .23) * .47
for name, data in [('Flow', flow), ('Erosion', erosion)]:
    pixels = np.uint8(np.clip(data, 0, 1) * 255)
    Image.fromarray(pixels).convert('RGB').save(OUT / f'{name}.png')
    print(f'{name}: {SIZE}x{SIZE}, neutral grayscale, range {pixels.min()}..{pixels.max()}')
