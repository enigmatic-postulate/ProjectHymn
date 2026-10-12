#!/usr/bin/env python3
"""
Paints the Project Hymn title logo (VS-4): "PROJECT HYMN" in a gold-to-rose gradient serif with a violet outline and a
soft glow, over a five-line staff, with a four-point star glinting on the Y. Original; DejaVu Serif Bold (Bitstream
Vera licence). Output: Assets/_Project/Resources/UI/HymnLogo.png (loaded at run time by the title menu).

    python3 Tools/paint_logo.py
"""
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUT = "Assets/_Project/Resources/UI/HymnLogo.png"
W, H = 2048, 560
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf"


def text_mask(text, size, spacing, y):
    font = ImageFont.truetype(FONT, size)
    widths = [font.getbbox(ch)[2] - font.getbbox(ch)[0] if ch != " " else size * 0.35 for ch in text]
    total = sum(widths) + spacing * (len(text) - 1)
    x = (W - total) / 2
    m = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(m)
    xs = []
    for ch, w in zip(text, widths):
        if ch != " ":
            d.text((x - font.getbbox(ch)[0], y), ch, font=font, fill=255)
        xs.append(x)
        x += w + spacing
    return m, xs, widths


def main():
    small, _, _ = text_mask("PROJECT", 120, 34, 70)
    big, xs, widths = text_mask("HYMN", 300, 40, 170)
    mask = np.maximum(np.array(small), np.array(big)).astype(float) / 255.0

    # The staff behind the title: five fine lines across the big word, fading at the ends.
    staff = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(staff)
    for k in range(5):
        yy = 250 + k * 40
        d.line([(160, yy), (W - 160, yy)], fill=255, width=4)
    staff = np.array(staff.filter(ImageFilter.GaussianBlur(0.8))).astype(float) / 255.0
    xr = np.linspace(-1, 1, W)
    staff *= np.clip(1.4 - np.abs(xr) * 1.25, 0, 1)[None, :]

    y = np.linspace(0, 1, H)[:, None]
    top = np.array([1.0, 0.93, 0.62])
    mid = np.array([0.98, 0.74, 0.36])
    bot = np.array([0.95, 0.52, 0.62])
    grad = np.where(y < 0.55, top + (mid - top) * (y / 0.55), mid + (bot - mid) * ((y - 0.55) / 0.45))
    grad = np.broadcast_to(grad[:, None, :], (H, W, 3))

    outline = np.array(Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(13))).astype(float) / 255.0
    glow = np.array(Image.fromarray((outline * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(26))).astype(float) / 255.0

    rgb = np.zeros((H, W, 3))
    a = np.zeros((H, W))
    glow_col = np.array([0.85, 0.55, 1.0])
    rgb += glow_col * glow[..., None]
    a = np.maximum(a, glow * 0.75)
    staff_col = np.array([0.95, 0.85, 1.0])
    rgb = rgb * (1 - staff[..., None] * 0.9) + staff_col * staff[..., None] * 0.9
    a = np.maximum(a, staff * 0.7)
    out_col = np.array([0.18, 0.08, 0.32])
    rgb = rgb * (1 - outline[..., None]) + out_col * outline[..., None]
    a = np.maximum(a, outline)
    # Inner bevel: lighter at the top of each letter.
    shade = np.array(Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(5))).astype(float) / 255.0
    bevel = np.clip(shade - np.roll(shade, 6, axis=0), -1, 1)
    fill = np.clip(grad * (1.0 + bevel[..., None] * 0.9), 0, 1)
    rgb = rgb * (1 - mask[..., None]) + fill * mask[..., None]
    a = np.maximum(a, mask)

    # A four-point star glinting at the top right of the Y.
    yi = 1                     # "HYMN"[1]
    sx, sy = xs[yi] + widths[yi] * 0.92, 205
    yy, xx = np.mgrid[0:H, 0:W].astype(float)
    dx, dy = (xx - sx) / 90.0, (yy - sy) / 90.0
    star = np.exp(-(np.abs(dx) * 14 + np.abs(dy) * 1.2) ** 1.2) + np.exp(-(np.abs(dy) * 14 + np.abs(dx) * 1.2) ** 1.2)
    star += np.exp(-(dx * dx + dy * dy) * 30) * 1.5
    star = np.clip(star, 0, 1)
    rgb = rgb * (1 - star[..., None]) + np.array([1, 1, 1]) * star[..., None]
    a = np.maximum(a, star)

    img = np.dstack([np.clip(rgb, 0, 1), np.clip(a, 0, 1)])
    Image.fromarray((img * 255).astype(np.uint8), "RGBA").save(OUT)
    print("painted", OUT)


if __name__ == "__main__":
    main()
