#!/usr/bin/env python3
"""
The lawn texture of the city maps' grass areas (Assets/Textures/Ground/Lawn/lawn_col.png and lawn_nrm.png): a tileable
top view of mown grass, many short blades in varied greens over darker soil and thatch, with a normal map from the
blades' heights. Made from code (no download): python3 Tools/lawn_texture.py   (needs numpy and Pillow)
"""
import math, os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

N = 1024          # pixels; the texture covers 2 m (about 2 mm a pixel)
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Textures", "Ground", "Lawn")


def tile_noise(n, cells, rng):
    """Smooth tileable value noise in 0..1."""
    g = rng.random((cells, cells))
    x = np.arange(n) * cells / n
    i0 = np.floor(x).astype(int); f = x - i0; f = f * f * (3 - 2 * f)
    i1 = (i0 + 1) % cells
    a = g[i0][:, i0] * (1 - f)[None, :] + g[i0][:, i1] * f[None, :]
    b = g[i1][:, i0] * (1 - f)[None, :] + g[i1][:, i1] * f[None, :]
    return a * (1 - f)[:, None] + b * f[:, None]


def main():
    rng = np.random.default_rng(7); r = random.Random(7)
    # soil and thatch underneath, with patches
    patch = 0.6 * tile_noise(N, 8, rng) + 0.4 * tile_noise(N, 32, rng)
    base = np.zeros((N, N, 3))
    soil = np.array([0.20, 0.17, 0.10]); thatch = np.array([0.30, 0.30, 0.13])
    base[:] = soil
    base += (thatch - soil) * patch[..., None]
    base *= (0.85 + 0.3 * tile_noise(N, 128, rng))[..., None]
    img = Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8))
    height = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(img); h = ImageDraw.Draw(height)
    lush = tile_noise(N, 6, rng)   # greener and denser in places
    greens = [(0.22, 0.38, 0.12), (0.27, 0.45, 0.14), (0.33, 0.50, 0.17), (0.20, 0.33, 0.11), (0.38, 0.52, 0.20), (0.42, 0.50, 0.22)]
    blades = 90000
    for k in range(blades):
        x, y = r.random() * N, r.random() * N
        lu = lush[int(y) % N, int(x) % N]
        if r.random() > 0.55 + 0.45 * lu: continue
        layer = k / blades                     # later blades lie on top: lighter (more light reaches them)
        c = greens[r.randrange(len(greens))]
        tone = 0.65 + 0.55 * layer + r.uniform(-0.08, 0.08)
        if r.random() < 0.04: c = (0.48, 0.45, 0.24)   # a dry blade
        col = tuple(int(max(0, min(1, v * tone)) * 255) for v in c)
        ang = r.uniform(0, 2 * math.pi); ln = r.uniform(7, 20); w = r.choice([1, 1, 2, 2, 3])
        dx, dy = math.cos(ang) * ln, math.sin(ang) * ln
        hv = int(80 + 175 * layer)
        for ox in (-N, 0, N):                  # drawn again across the edges: tileable
            for oy in (-N, 0, N):
                if -30 < x + ox < N + 30 and -30 < y + oy < N + 30:
                    d.line([(x + ox, y + oy), (x + ox + dx, y + oy + dy)], fill=col, width=w)
                    h.line([(x + ox, y + oy), (x + ox + dx, y + oy + dy)], fill=hv, width=w)
    # a few clover leaves and daisies
    for k in range(260):
        x, y = r.random() * N, r.random() * N
        if r.random() < 0.85:
            col = (int(255 * 0.24), int(255 * 0.42), int(255 * 0.16))
            for a in range(3):
                ang = a * 2.094 + r.random()
                cx, cy = x + math.cos(ang) * 4, y + math.sin(ang) * 4
                d.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=col); h.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=230)
        else:
            d.ellipse([x - 3, y - 3, x + 3, y + 3], fill=(236, 236, 228)); d.ellipse([x - 1, y - 1, x + 1, y + 1], fill=(230, 190, 40))
    os.makedirs(OUT, exist_ok=True)
    img = img.filter(ImageFilter.GaussianBlur(0.6))
    img.save(os.path.join(OUT, "lawn_col.png"), optimize=True)
    hgt = np.asarray(height.filter(ImageFilter.GaussianBlur(1.2)), dtype=np.float32) / 255.0
    gx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * 2.0; gy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * 2.0
    nrm = np.stack([-gx, gy, np.ones_like(hgt)], -1); nrm /= np.linalg.norm(nrm, axis=-1, keepdims=True)
    Image.fromarray(((nrm * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(OUT, "lawn_nrm.png"), optimize=True)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
