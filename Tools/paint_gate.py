#!/usr/bin/env python3
"""
Derives height maps for the village kit's stone from the kit's own albedo (VS-7 gate pass), so URP's parallax can
sink the mortar and stand the stones proud without any new geometry.

The kit ships albedo and normals but no height. Stone reads darker in the joints and lighter on the face, so a
blurred, contrast-stretched luminance is a good height: the result is blurred enough not to shimmer, and the
histogram is flattened so the joints reach black and the faces white.

    python3 Tools/paint_gate.py [out_dir]

Output: Assets/_Project/Art/Generated/Village/T_<name>_Height.png
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

SRC = "Assets/_Project/Art/Imported/Village/Textures"
OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/_Project/Art/Generated/Village"
NAMES = ["T_UnevenBrick_BaseColor", "T_Brick_BaseColor", "T_RockTrim_BaseColor"]


def height(name):
    img = Image.open(f"{SRC}/{name}.png").convert("RGB")
    a = np.asarray(img).astype(np.float32) / 255.0
    lum = 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]
    # Blur away the grain: parallax on pixel noise shimmers as the camera moves.
    lum = np.asarray(Image.fromarray((lum * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2.0))) / 255.0
    # Stretch 2nd..98th percentile to the full range, so joints bottom out and faces top out.
    lo, hi = np.percentile(lum, 2), np.percentile(lum, 98)
    h = np.clip((lum - lo) / max(1e-5, hi - lo), 0, 1)
    # Lean on the joints: a gentle S-curve deepens the dark lines without flattening the faces.
    h = h * h * (3 - 2 * h)
    out = f"{OUT}/{name.replace('_BaseColor', '')}_Height.png"
    Image.fromarray((h * 255).astype(np.uint8)).save(out)
    print(out, img.size)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for n in NAMES:
        height(n)
