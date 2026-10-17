#!/usr/bin/env python3
"""
Paints the Drowned Nave's textures (VS-6), all original and procedural:

  DrownedStone_Albedo.png / _Normal.png   the cathedral's limestone under water: worn ashlar courses, sea-green and
                                          grey, algae stains running down from the joints, pitting and barnacle crust
  DrownedTop_Albedo.png                   the floors' tops: silt and sand ripples with patches of weed and shell grit
  Bubble.png                              a bubble for the particles: a bright rim, a highlight, an almost clear middle
  Fish.png                                a small fish silhouette for the schools (white, with alpha)

    python3 Tools/paint_drowned.py [out_dir]
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from paint_spring import fbm, normal_from_height, smooth  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/_Project/Art/Generated/Drowned"
rng = np.random.default_rng(23)


def save(arr, name, srgb=True, alpha=None):
    a = np.clip(arr, 0, 1)
    if srgb:
        a = a ** (1 / 2.2)
    img = (a * 255).astype(np.uint8)
    if alpha is not None:
        img = np.dstack([img, (np.clip(alpha, 0, 1) * 255).astype(np.uint8)])
    Image.fromarray(img).save(f"{OUT}/{name}")
    print(f"{OUT}/{name}")


def stone():
    W = H = 1024
    y, x = np.mgrid[0:H, 0:W].astype(float)
    course = 128.0
    row = np.floor(y / course)
    width = 256.0
    bx = (x + (row % 2) * width * 0.5 + (row % 3) * 37) / width
    jx = np.abs(bx - np.round(bx)) * width
    jy = np.abs(y / course - np.round(y / course)) * course
    joint = np.minimum(smooth(0, 6, jx), smooth(0, 6, jy))
    # Each block a slightly different tone
    block_id = np.floor(bx) * 7.31 + row * 3.17
    tone = (np.sin(block_id * 12.9898) * 43758.5453) % 1.0
    n1 = fbm(W, H, (8, 8), 3)
    n2 = fbm(W, H, (32, 32), 5)
    base = np.array([0.34, 0.42, 0.4])
    albedo = np.ones((H, W, 3)) * base
    albedo *= (0.7 + 0.45 * tone)[..., None]
    albedo *= (0.75 + 0.45 * n1)[..., None]
    # Algae stains running down from the joints and the top
    drip = fbm(W, H, (64, 4), 9)
    stain = np.clip((1 - smooth(0, 40, jy * (y % course > course / 2))) * 0 + (drip - 0.45) * 2.2, 0, 1) * smooth(0.3, 0.9, n1)
    algae = np.array([0.16, 0.34, 0.2])
    albedo = albedo * (1 - stain[..., None] * 0.85) + algae * stain[..., None] * 0.85
    # Barnacle crust: small pale dots clustered in places
    crust = (fbm(W, H, (128, 128), 13) > 0.62) * smooth(0.55, 0.8, fbm(W, H, (6, 6), 17))
    albedo = albedo * (1 - crust[..., None] * 0.5) + np.array([0.78, 0.8, 0.72]) * crust[..., None] * 0.5
    # Joints dark and green
    albedo *= (0.3 + 0.7 * joint)[..., None]
    albedo = albedo * (1 - (1 - joint)[..., None] * 0.3) + np.array([0.08, 0.2, 0.14]) * (1 - joint)[..., None] * 0.3
    save(albedo, "DrownedStone_Albedo.png")
    height = joint * 0.7 + n2 * 0.2 + crust * 0.25 - (fbm(W, H, (48, 48), 21) > 0.68) * 0.15
    Image.fromarray(normal_from_height(height, 6.0)).save(f"{OUT}/DrownedStone_Normal.png")
    print(f"{OUT}/DrownedStone_Normal.png")


def top():
    W = H = 1024
    y, x = np.mgrid[0:H, 0:W].astype(float)
    n = fbm(W, H, (6, 6), 31)
    ripples = 0.5 + 0.5 * np.sin((x * 0.06 + n * 9.0) + np.sin(y * 0.02) * 2.0)
    sand = np.array([0.4, 0.42, 0.34]) * (0.8 + 0.25 * ripples[..., None]) * (0.85 + 0.2 * n[..., None])
    weed = smooth(0.55, 0.75, fbm(W, H, (10, 10), 33))
    grit = (fbm(W, H, (160, 160), 35) > 0.66) * 0.5
    albedo = sand * (1 - weed[..., None] * 0.75) + np.array([0.14, 0.3, 0.16]) * weed[..., None] * 0.75
    albedo = albedo * (1 - grit[..., None] * 0.3) + np.array([0.85, 0.82, 0.75]) * grit[..., None] * 0.3
    save(albedo, "DrownedTop_Albedo.png")


def bubble():
    S = 128
    y, x = np.mgrid[0:S, 0:S].astype(float)
    r = np.sqrt((x - S / 2 + 0.5) ** 2 + (y - S / 2 + 0.5) ** 2) / (S / 2)
    rim = np.exp(-((r - 0.85) / 0.07) ** 2)
    inside = np.clip(1 - r, 0, 1) * 0.12
    hl = np.exp(-(((x - S * 0.36) ** 2 + (y - S * 0.32) ** 2) / (S * 0.08) ** 2)) * 0.9
    a = np.clip(rim + inside + hl, 0, 1) * (r < 1)
    save(np.ones((S, S, 3)), "Bubble.png", srgb=False, alpha=a)


def fish():
    W, H = 128, 64
    y, x = np.mgrid[0:H, 0:W].astype(float)
    u = x / W
    v = (y - H / 2) / (H / 2)
    body = (np.abs(v) < np.sin(np.clip((u - 0.12) / 0.7, 0, 1) * np.pi) * 0.55) & (u > 0.12) & (u < 0.82)
    tail = (u > 0.78) & (np.abs(v) < (u - 0.78) * 2.4) & (u < 0.98)
    a = (body | tail).astype(float)
    from PIL import ImageFilter
    a = np.array(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))) / 255.0
    save(np.ones((H, W, 3)), "Fish.png", srgb=False, alpha=a)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    stone()
    top()
    bubble()
    fish()
