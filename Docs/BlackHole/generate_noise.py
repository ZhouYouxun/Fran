"""Generate original seamless accretion noise using only the Python standard library.
Run from any working directory. No downloaded textures are used.
"""
from pathlib import Path
import math
import random
import struct
import zlib

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'Assets/Textures/BlackHole'

def write_png(path, width, height, pixels):
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
    raw = b''.join(b'\0' + pixels[y*width*4:(y+1)*width*4] for y in range(height))
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) +
                     chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))

def main():
    DEST.mkdir(parents=True, exist_ok=True)
    rng = random.Random(731602)
    layers = [(n, [[rng.random() for _ in range(n)] for _ in range(n)]) for n in (4, 8, 16, 32, 64)]
    def noise(x, y, layer):
        n, grid = layer
        x, y = (x % 1) * n, (y % 1) * n
        ix, iy = int(x), int(y)
        a, b = x - ix, y - iy
        a, b = a*a*(3-2*a), b*b*(3-2*b)
        top = grid[iy][ix] * (1-a) + grid[iy][(ix+1)%n] * a
        bottom = grid[(iy+1)%n][ix] * (1-a) + grid[(iy+1)%n][(ix+1)%n] * a
        return top * (1-b) + bottom * b
    pixels = bytearray()
    size = 256
    for y in range(size):
        for x in range(size):
            u, v = x/size, y/size
            warp = noise(u, v, layers[0]) - 0.5
            warped_u = u + math.sin(v * math.tau * 2) * 0.1 + warp * 0.17
            value = sum(noise(warped_u, v, layer) * (0.52**i) for i, layer in enumerate(layers)) / sum(0.52**i for i in range(len(layers)))
            filaments = 0.5 + 0.5 * math.sin((v * 15 + warp * 2.4) * math.tau)
            value = max(0, min(1, 0.10 + value**1.65 * 1.02 + filaments * 0.13))
            c = round(value * 255)
            pixels.extend((c, c, c, 255))
    write_png(DEST/'AccretionNoise.png', size, size, pixels)
    write_png(DEST/'TransparentPixel.png', 1, 1, bytes((0, 0, 0, 0)))
    print('Generated AccretionNoise.png (256x256) and TransparentPixel.png (1x1).')

if __name__ == '__main__':
    main()
