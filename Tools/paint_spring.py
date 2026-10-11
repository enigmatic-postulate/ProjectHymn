#!/usr/bin/env python3
"""
Paints the Starlit Spring textures (Slice 11), all original and procedural:

  SpringWall_Albedo.png / _Normal.png / _Emission.png  the island's drum wall: pale lilac masonry, eight arched windows
                                                       onto the starry night with gold lattice, trim bands top and bottom
  SpringTiles_Albedo.png / _Normal.png                 the walking terrace: pearl tiles with small star inlays
  SpringRock_Albedo.png                                the island's underside: violet rock with crystal veins

    python3 Tools/paint_spring.py [out_dir]
"""
import sys
import numpy as np
from PIL import Image

OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/_Project/Art/Generated/Spring"
rng = np.random.default_rng(11)


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def value_noise(w, h, cells, seed):
    r = np.random.default_rng(seed)
    g = r.random((cells[1] + 1, cells[0] + 1))
    g[:, -1] = g[:, 0]                                   # tile horizontally
    ys = np.linspace(0, cells[1], h, endpoint=False)
    xs = np.linspace(0, cells[0], w, endpoint=False)
    yi, xi = ys.astype(int), xs.astype(int)
    yf, xf = ys - yi, xs - xi
    yf, xf = yf * yf * (3 - 2 * yf), xf * xf * (3 - 2 * xf)
    a = g[yi][:, xi]; b = g[yi][:, xi + 1]; c = g[yi + 1][:, xi]; d = g[yi + 1][:, xi + 1]
    top = a * (1 - xf) + b * xf
    bot = c * (1 - xf) + d * xf
    return top * (1 - yf[:, None]) + bot * yf[:, None]


def fbm(w, h, base, seed, octaves=5):
    s = np.zeros((h, w)); amp = 0.5
    for o in range(octaves):
        s += amp * value_noise(w, h, (base[0] * 2 ** o, base[1] * 2 ** o), seed + o)
        amp *= 0.5
    return s / (1 - 0.5 ** octaves)


def normal_from_height(hgt, strength):
    dx = (np.roll(hgt, -1, 1) - np.roll(hgt, 1, 1)) * strength
    dy = (np.roll(hgt, -1, 0) - np.roll(hgt, 1, 0)) * strength
    n = np.stack([-dx, dy, np.ones_like(hgt)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return ((n * 0.5 + 0.5) * 255).astype(np.uint8)


def save(arr, name, srgb=True):
    a = np.clip(arr, 0, 1)
    if srgb:
        a = a ** (1 / 2.2)
    Image.fromarray((a * 255).astype(np.uint8)).save(f"{OUT}/{name}")


# ---------------- The wall ----------------

def wall():
    W, H = 2048, 512
    y, x = np.mgrid[0:H, 0:W].astype(float)
    v = y / H                                         # 0 top .. 1 bottom
    stone = np.array([0.72, 0.66, 0.84])
    albedo = np.ones((H, W, 3)) * stone
    height = np.zeros((H, W))
    emission = np.zeros((H, W, 3))

    # Masonry: running bond, 16 px mortar-free courses with soft joints.
    course = 40.0
    row = np.floor(y / course)
    bx = (x + (row % 2) * 64) / 128.0
    jx = np.abs(bx - np.round(bx)) * 128
    jy = np.abs(y / course - np.round(y / course)) * course
    joint = np.minimum(smooth(0, 3.5, jx), smooth(0, 3.5, jy))
    brick_id = np.floor(bx) + row * 37
    tint = (np.sin(brick_id * 12.9898) * 43758.5453) % 1.0
    grain = fbm(W, H, (32, 8), 3)
    albedo *= (0.86 + 0.14 * tint[..., None]) * (0.85 + 0.25 * grain[..., None])
    albedo *= (0.55 + 0.45 * joint)[..., None]
    height += joint * 0.6 + grain * 0.25

    # Trim bands: a gold cornice at the top and a rose plinth at the bottom.
    top_band = smooth(0.0, 0.01, v) * (1 - smooth(0.075, 0.085, v))
    gold = np.array([0.92, 0.74, 0.36])
    albedo = albedo * (1 - top_band[..., None]) + gold * (0.75 + 0.25 * grain[..., None]) * top_band[..., None]
    height += top_band * 0.8
    emission += gold * 0.25 * top_band[..., None]
    rose_band = smooth(0.90, 0.91, v)
    albedo = albedo * (1 - rose_band[..., None]) + np.array([0.80, 0.52, 0.72]) * rose_band[..., None]
    height += rose_band * 0.5

    # Eight arched windows: dark starry glass, a lighter stone surround, gold lattice.
    arches = 8
    cell = W / arches
    cx = (np.floor(x / cell) + 0.5) * cell
    lx = (x - cx) / (cell * 0.5)                      # -1..1 across the cell
    aw, top, bottom = 0.42, 0.22, 0.80                # half width (cell units), spring line and sill (v)
    r = aw * cell * 0.5 / H                           # arch radius in v units (non-square px)
    spring = top + r
    def inside(scale):
        hw = aw * scale
        in_rect = (np.abs(lx) < hw) & (v > spring) & (v < bottom)
        dxv = lx * cell * 0.5 / H
        in_arc = (v <= spring) & (dxv ** 2 + (v - spring) ** 2 < (r * scale) ** 2)
        return in_rect | in_arc
    win = inside(1.0).astype(float)
    frame = inside(1.22).astype(float) - win
    reveal = win - inside(0.9).astype(float)             # the stone's depth just inside the frame
    albedo = albedo * (1 - frame[..., None]) + np.array([0.86, 0.82, 0.94]) * (0.9 + 0.1 * grain[..., None]) * frame[..., None]
    height += frame * 0.9
    glass = np.array([0.05, 0.06, 0.18]) + np.array([0.10, 0.04, 0.16]) * (1 - (v - top) / (bottom - top))[..., None]
    stars = (rng.random((H, W)) > 0.9965).astype(float)
    stars = np.maximum(stars, np.roll(stars, 1, 0)); stars = np.maximum(stars, np.roll(stars, 1, 1))   # 2x2 px
    albedo = albedo * (1 - win[..., None]) + glass * win[..., None]
    albedo *= (1 - 0.55 * reveal)[..., None]
    emission += win[..., None] * (glass * 0.6 + stars[..., None] * np.array([0.9, 0.9, 1.0]))

    # Water stains running down from the cornice, and a little grime toward the plinth.
    stain = value_noise(W, H, (96, 1), 77) * smooth(0.08, 0.5, v) * (1 - smooth(0.5, 0.9, v))
    albedo *= (1 - 0.18 * stain * (1 - win))[..., None]
    albedo *= (1 - 0.15 * smooth(0.6, 0.9, v) * (1 - win))[..., None]
    height -= win * 0.9

    # Lattice: two sets of diagonals inside each window.
    px = (x - cx) / 26.0
    py = y / 26.0
    d1 = np.abs(((px + py) % 2.0) - 1.0)
    d2 = np.abs(((px - py) % 2.0) - 1.0)
    bar = np.maximum(1 - smooth(0.06, 0.13, d1), 1 - smooth(0.06, 0.13, d2)) * win
    albedo = albedo * (1 - bar[..., None]) + gold * bar[..., None]
    emission += gold * 0.9 * bar[..., None]
    height += bar * 1.2

    save(albedo, "SpringWall_Albedo.png")
    Image.fromarray(normal_from_height(height, 2.5)).save(f"{OUT}/SpringWall_Normal.png")
    save(emission, "SpringWall_Emission.png")


# ---------------- The terrace tiles ----------------

def tiles():
    N = 1024
    y, x = np.mgrid[0:N, 0:N].astype(float)
    t = 128.0
    fx, fy = (x % t) / t, (y % t) / t
    edge = np.minimum(np.minimum(fx, 1 - fx), np.minimum(fy, 1 - fy))
    groove = smooth(0.0, 0.035, edge)
    tid = np.floor(x / t) + np.floor(y / t) * 13
    tint = (np.sin(tid * 78.233) * 43758.5453) % 1.0
    pearl = np.array([0.86, 0.84, 0.93])
    sheen = fbm(N, N, (8, 8), 21)
    albedo = pearl * (0.9 + 0.1 * tint[..., None]) * (0.88 + 0.18 * sheen[..., None])
    albedo = albedo * (0.55 + 0.45 * groove[..., None])
    # A small four-point star inlay on every other tile.
    ux, uy = (fx - 0.5) * 2, (fy - 0.5) * 2
    star = np.abs(ux) ** 0.5 + np.abs(uy) ** 0.5
    inlay = (1 - smooth(0.66, 0.74, star)) * ((np.floor(x / t) + np.floor(y / t)) % 2 == 0)
    albedo = albedo * (1 - inlay[..., None]) + np.array([0.92, 0.76, 0.42]) * inlay[..., None]
    height = groove * 0.7 + inlay * 0.3 + sheen * 0.1
    save(albedo, "SpringTiles_Albedo.png")
    Image.fromarray(normal_from_height(height, 3.0)).save(f"{OUT}/SpringTiles_Normal.png")


# ---------------- The underside rock ----------------

def rock():
    W, H = 1024, 512
    n = fbm(W, H, (8, 4), 41)
    n2 = fbm(W, H, (24, 12), 57)
    base = np.array([0.30, 0.24, 0.42]) * (0.6 + 0.6 * n[..., None]) + np.array([0.08, 0.02, 0.1]) * n2[..., None]
    n3 = fbm(W, H, (6, 3), 63)
    vein = np.exp(-((n3 - 0.5) / 0.012) ** 2) * smooth(0.45, 0.7, n2)
    base = base * (1 - vein[..., None]) + np.array([0.65, 0.45, 0.95]) * vein[..., None]
    save(base, "SpringRock_Albedo.png")


# ---------------- Particle sprite ----------------

def spark():
    N = 128
    y, x = np.mgrid[0:N, 0:N].astype(float)
    u, v = (x + 0.5) / N * 2 - 1, (y + 0.5) / N * 2 - 1
    r = np.sqrt(u * u + v * v)
    core = np.exp(-(r / 0.16) ** 2)
    halo = np.exp(-(r / 0.5) ** 2) * 0.35
    rays = (np.exp(-(np.abs(u) / 0.035) ** 2) + np.exp(-(np.abs(v) / 0.035) ** 2)) * np.exp(-(r / 0.75) ** 2) * 0.5
    a = np.clip(core + halo + rays, 0, 1) * smooth(1.0, 0.85, r)
    rgba = np.dstack([np.ones_like(a), np.ones_like(a), np.ones_like(a), a])
    Image.fromarray((rgba * 255).astype(np.uint8), "RGBA").save(f"{OUT}/SpringSpark.png")


if __name__ == "__main__":
    wall(); tiles(); rock(); spark()
    print("painted", OUT)
