#!/usr/bin/env python3
"""
Paints the story cards for Project Hymn (VS-5 Part A): four for the intro on New Game and three for the ending after the
Summit; VS-6 adds chapter two's two-card prologue (the first visit to the Air Bell) and three-card ending (the Sunken Apse). Everything is procedural (gradients, fractal ridgelines, stars, glows and silhouettes, finished with a painted
grain and a vignette), in the palette of the stages: Saltmere's evening violet and gold, the cliffs' sea blue, the
summit's night. Original placeholders; no reference images.

    python3 Tools/paint_story.py [only_this_card]

Output: Assets/_Project/Resources/UI/Story/<card>.png (1280 x 720), loaded at run time by StoryCards.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT_DIR = "Assets/_Project/Resources/UI/Story"
W, H = 1280, 720
SS = 2                     # silhouettes are drawn at twice the size and scaled down (anti-aliasing)


# ---------------- Basics ----------------

def rgb(hexstr):
    h = hexstr.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def vgrad(stops):
    """A vertical gradient: stops are (y in 0..1, '#rrggbb')."""
    y = np.linspace(0, 1, H)
    out = np.zeros((H, 3))
    ys = [s[0] for s in stops]
    cs = [rgb(s[1]) for s in stops]
    for c in range(3):
        out[:, c] = np.interp(y, ys, [col[c] for col in cs])
    return np.broadcast_to(out[:, None, :], (H, W, 3)).copy()


def fbm1(x, seed, octaves=6, persistence=0.5):
    """1D fractal noise in -1..1 over x (any scale)."""
    rng = np.random.default_rng(seed)
    total = np.zeros_like(x, dtype=float)
    amp, freq, norm = 1.0, 1.0, 0.0
    for _ in range(octaves):
        n = int(np.ceil(x.max() * freq)) + 3
        pts = rng.uniform(-1, 1, n)
        xf = x * freq
        i = np.floor(xf).astype(int)
        f = xf - i
        f = f * f * (3 - 2 * f)
        total += amp * (pts[i] * (1 - f) + pts[i + 1] * f)
        norm += amp
        amp *= persistence
        freq *= 2.0
    return total / norm


def fbm2(seed, scale=160.0, octaves=5):
    """2D value noise over the canvas, 0..1."""
    rng = np.random.default_rng(seed)
    out = np.zeros((H, W))
    amp, norm = 1.0, 0.0
    s = scale
    for _ in range(octaves):
        gh, gw = int(H / s) + 2, int(W / s) + 2
        g = rng.uniform(0, 1, (gh, gw))
        img = Image.fromarray((g * 255).astype(np.uint8)).resize((int(gw * s), int(gh * s)), Image.BICUBIC)
        out += amp * np.array(img)[:H, :W] / 255.0
        norm += amp
        amp *= 0.5
        s /= 2.0
    return out / norm


def blend(base, color, alpha):
    a = np.clip(alpha, 0, 1)[..., None]
    return base * (1 - a) + np.asarray(color) * a


def add(base, color, amount):
    return base + np.asarray(color) * np.clip(amount, 0, None)[..., None]


YY, XX = np.mgrid[0:H, 0:W].astype(float)


def radial(cx, cy, r, power=2.0):
    d = np.sqrt((XX - cx) ** 2 + (YY - cy) ** 2) / r
    return np.clip(1 - d, 0, 1) ** power


def glow(img, cx, cy, r, color, strength=1.0, power=2.2):
    return add(img, rgb(color) if isinstance(color, str) else color, radial(cx, cy, r, power) * strength)


def stars(img, seed, count, y_max=1.0, bright=1.0, tint="#e8e4ff", fade_by_y=True):
    rng = np.random.default_rng(seed)
    layer = np.zeros((H, W))
    for _ in range(count):
        x, y = rng.uniform(0, W), rng.uniform(0, H * y_max) ** 1.0
        b = rng.uniform(0.25, 1.0) ** 2.2 * bright
        if fade_by_y:
            b *= np.clip(1.2 - y / (H * y_max), 0.15, 1)
        xi, yi = int(x), int(y)
        if 1 <= xi < W - 1 and 1 <= yi < H - 1:
            layer[yi, xi] += b
            if b > 0.55:
                layer[yi - 1:yi + 2, xi] += b * 0.35
                layer[yi, xi - 1:xi + 2] += b * 0.35
    blurred = np.array(Image.fromarray(np.clip(layer * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.6))) / 255.0
    return add(img, rgb(tint), np.clip(layer, 0, 1.5) + blurred * 1.2)


def ridge(img, base_y, amp, seed, color, scale=0.004, octaves=6, persistence=0.55, haze=None, haze_amount=0.0, top_light=None,
          light_amount=0.0):
    """Fill below a fractal ridgeline; returns (img, mask)."""
    x = np.arange(W) * scale + 1.0
    line = base_y + fbm1(x, seed, octaves, persistence) * amp
    mask = (YY >= line[None, :]).astype(float)
    # Soft edge
    mask = np.clip((YY - line[None, :]) / 1.2 + 0.5, 0, 1)
    col = np.broadcast_to(rgb(color), (H, W, 3)).copy()
    if haze is not None:
        depth = np.clip((YY - line[None, :]) / 220.0, 0, 1)[..., None]
        col = col * (1 - (1 - depth) * haze_amount) + rgb(haze) * (1 - depth) * haze_amount
    if top_light is not None:
        rim = np.clip(1 - (YY - line[None, :]) / 10.0, 0, 1) * mask
        col = col + rgb(top_light) * (rim * light_amount)[..., None]
    return blend(img, col, mask), mask, line


def poly_mask(shapes):
    """Anti-aliased mask from drawing callbacks on a supersampled canvas: shapes(draw, s) with s the scale."""
    m = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(m)
    shapes(d, SS)
    m = m.resize((W, H), Image.LANCZOS)
    return np.array(m).astype(float) / 255.0


def jagged(points, seed, amp, step=6.0):
    """Subdivide a polygon's edges and push each point out along the edge's normal by fractal noise (rocky outlines)."""
    rng = np.random.default_rng(seed)
    out = []
    n = len(points)
    for i in range(n):
        (x0, y0), (x1, y1) = points[i], points[(i + 1) % n]
        ln = math.hypot(x1 - x0, y1 - y0)
        k = max(1, int(ln / step))
        nx, ny = -(y1 - y0) / max(ln, 1e-6), (x1 - x0) / max(ln, 1e-6)
        rough = fbm1(np.linspace(0, ln / 60.0, k) + 1.0 + i * 7.0, seed + i, 5, 0.55) if k > 2 else np.zeros(k)
        for j in range(k):
            t = j / k
            e = amp * rough[j] * math.sin(math.pi * t) ** 0.3 if amp else 0.0
            out.append((x0 + (x1 - x0) * t + nx * e, y0 + (y1 - y0) * t + ny * e))
    return out


def soft(mask, radius):
    return np.array(Image.fromarray((np.clip(mask, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius))) / 255.0


# ---------------- Figures (KayKit-ish: big heads, small bodies) ----------------

def hero(d, s, x, y, h, facing=1, sword_up=False, shield=True, cape=True):
    """A cantor with sword and shield; (x, y) is the feet, h the height."""
    k = h * s
    X, Y = x * s, y * s
    head_r = k * 0.2
    body_top = Y - k * 0.62
    # Cape behind
    if cape:
        d.polygon([(X - k * 0.14 * facing, body_top + k * 0.02), (X - k * 0.34 * facing, Y - k * 0.04),
                   (X + k * 0.02 * facing, Y - k * 0.08)], fill=255)
    # Legs
    d.rectangle([X - k * 0.13, Y - k * 0.24, X - k * 0.03, Y], fill=255)
    d.rectangle([X + k * 0.03, Y - k * 0.24, X + k * 0.13, Y], fill=255)
    # Body
    d.polygon([(X - k * 0.17, body_top), (X + k * 0.17, body_top), (X + k * 0.2, Y - k * 0.2), (X - k * 0.2, Y - k * 0.2)], fill=255)
    # Head and helm crest
    d.ellipse([X - head_r, body_top - head_r * 1.85, X + head_r, body_top + head_r * 0.15], fill=255)
    d.polygon([(X - k * 0.03, body_top - head_r * 1.8), (X + k * 0.03, body_top - head_r * 1.8), (X, body_top - head_r * 2.4)], fill=255)
    # Shield on the off side
    if shield:
        sx = X - k * 0.2 * facing
        d.ellipse([sx - k * 0.13, body_top + k * 0.02, sx + k * 0.13, body_top + k * 0.32], fill=255)
    # Sword
    hx, hy = X + k * 0.2 * facing, body_top + k * 0.2
    if sword_up:
        d.line([(hx, hy), (hx + k * 0.12 * facing, hy - k * 0.62)], fill=255, width=max(2, int(k * 0.035)))
        d.line([(hx - k * 0.06, hy - k * 0.04), (hx + k * 0.08, hy - k * 0.02)], fill=255, width=max(2, int(k * 0.03)))
    else:
        d.line([(hx, hy), (hx + k * 0.42 * facing, hy + k * 0.2)], fill=255, width=max(2, int(k * 0.035)))


def skeleton(d, s, x, y, h, facing=1, blade_up=True, phase=0.0):
    k = h * s
    X, Y = x * s, y * s
    step = math.sin(phase) * k * 0.06
    w = max(2, int(k * 0.05))
    hip = (X, Y - k * 0.38)
    d.line([hip, (X - k * 0.08 + step, Y)], fill=255, width=w)
    d.line([hip, (X + k * 0.08 - step, Y)], fill=255, width=w)
    neck = (X + k * 0.04 * facing, Y - k * 0.7)
    d.line([hip, neck], fill=255, width=int(w * 1.5))
    for r in range(3):                                     # ribs
        yy = Y - k * (0.47 + r * 0.07)
        d.line([(X - k * 0.09, yy), (X + k * 0.11, yy)], fill=255, width=max(1, int(w * 0.6)))
    hr = k * 0.15
    d.ellipse([neck[0] - hr, neck[1] - hr * 1.9, neck[0] + hr, neck[1] + hr * 0.1], fill=255)
    sh = (X + k * 0.06 * facing, Y - k * 0.62)
    if blade_up:
        hand = (sh[0] + k * 0.16 * facing, sh[1] - k * 0.12)
        d.line([sh, hand], fill=255, width=w)
        d.line([hand, (hand[0] + k * 0.1 * facing, hand[1] - k * 0.55)], fill=255, width=int(w * 1.3))
    else:
        hand = (sh[0] + k * 0.18 * facing, sh[1] + k * 0.12)
        d.line([sh, hand], fill=255, width=w)
        d.line([hand, (hand[0] + k * 0.5 * facing, hand[1] + k * 0.08)], fill=255, width=int(w * 1.3))
    # Shield on the back arm
    d.ellipse([X - k * 0.3 * facing - k * 0.12, Y - k * 0.64, X - k * 0.3 * facing + k * 0.12, Y - k * 0.36], fill=255)


def conductor(d, s, x, y, h, staff_up=True):
    """The Last Cantor: a tall robed figure with a staff."""
    k = h * s
    X, Y = x * s, y * s
    d.polygon([(X - k * 0.08, Y - k * 0.78), (X + k * 0.08, Y - k * 0.78), (X + k * 0.26, Y), (X - k * 0.26, Y)], fill=255)
    hr = k * 0.1
    d.ellipse([X - hr, Y - k * 0.98, X + hr, Y - k * 0.76], fill=255)
    d.polygon([(X - hr * 1.2, Y - k * 0.86), (X + hr * 1.2, Y - k * 0.86), (X, Y - k * 1.08)], fill=255)       # hood point
    if staff_up:
        hand = (X + k * 0.22, Y - k * 0.82)
        d.line([(X + k * 0.06, Y - k * 0.66), hand], fill=255, width=max(2, int(k * 0.04)))
        d.line([(hand[0] - k * 0.03, Y - k * 0.2), (hand[0] + k * 0.06, Y - k * 1.42)], fill=255, width=max(2, int(k * 0.03)))
    else:
        d.line([(X + k * 0.1, Y - k * 0.5), (X + k * 0.7, Y - k * 0.04)], fill=255, width=max(2, int(k * 0.03)))


def house(d, s, x, y, w, h, roof=0.55):
    X, Y, Wd, Hh = x * s, y * s, w * s, h * s
    d.rectangle([X, Y - Hh, X + Wd, Y], fill=255)
    d.polygon([(X - Wd * 0.08, Y - Hh), (X + Wd * 0.5, Y - Hh - Wd * roof), (X + Wd * 1.08, Y - Hh)], fill=255)


def windows(img, xs, color="#ffcf7a", strength=1.0, seed=0):
    """Lit windows: a list of (x, y, w, h) rectangles, each glowing."""
    rng = np.random.default_rng(seed)
    for (x, y, w, h) in xs:
        m = poly_mask(lambda d, s, x=x, y=y, w=w, h=h: d.rectangle([x * s, y * s, (x + w) * s, (y + h) * s], fill=255))
        flicker = 0.8 + 0.2 * rng.random()
        img = blend(img, rgb(color) * (1.2 * flicker), m * strength)
        img = add(img, rgb(color), soft(m, 10) * 0.6 * strength)
    return img


# ---------------- Finishing ----------------

def finish(img, seed, vignette=0.55, grain=0.025, warm=0.0, bloom=0.35):
    img = np.clip(img, 0, None)
    # Bloom: blur what's brighter than 0.8 and add it back.
    bright = np.clip(img - 0.8, 0, None)
    b = np.stack([np.array(Image.fromarray(np.clip(bright[..., c] * 255, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(18))) / 255.0
                  for c in range(3)], -1)
    img = img + b * bloom * 2.0
    # Painted look: a light directional smear and canvas grain.
    n = fbm2(seed + 77, scale=6.0, octaves=2)
    img = img * (1 + (n[..., None] - 0.5) * 0.06)
    rng = np.random.default_rng(seed)
    img = img + rng.normal(0, grain, (H, W, 1))
    # Vignette
    vx = (XX / W - 0.5) * 1.6
    vy = (YY / H - 0.5) * 1.9
    v = np.clip(1 - (vx ** 2 + vy ** 2) * vignette, 0, 1) ** 1.3
    img = img * v[..., None]
    if warm:
        img = img * np.array([1 + warm, 1 + warm * 0.3, 1 - warm * 0.4])
    # Gentle tone curve (filmic shoulder)
    img = img / (1 + img * 0.18) * 1.18
    return np.clip(img, 0, 1)


def save(img, name):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = f"{OUT_DIR}/{name}.png"
    Image.fromarray((img * 255).astype(np.uint8)).save(path, optimize=True)
    print(path)


# ---------------- The cards ----------------

def staff_lines(img, y0, amp, phase, color, strength=1.0, width=2.2, beads=True, seed=0, x_fade=True, curve=1.0):
    """Five glowing lines of a musical staff waving across the picture, with notes like beads of light."""
    rng = np.random.default_rng(seed)
    xs = np.arange(W)
    for k in range(5):
        yk = y0 + k * 13 - curve * 0.00025 * (xs - W * 0.5) ** 2 + np.sin(xs / W * 2 * np.pi * 0.9 + phase) * amp
        d = np.abs(YY - yk[None, :])
        line = np.exp(-(d / width) ** 2)
        halo = np.exp(-(d / (width * 9)) ** 2) * 0.25
        fade = np.clip(np.sin(np.pi * xs / W) * 1.6, 0, 1)[None, :] if x_fade else 1.0
        img = add(img, rgb(color), (line + halo) * fade * strength)
    if beads:
        for _ in range(16):
            x = rng.uniform(W * 0.12, W * 0.88)
            yk = y0 + rng.integers(0, 9) * 6.5 - curve * 0.00025 * (x - W * 0.5) ** 2 + math.sin(x / W * 2 * math.pi * 0.9 + phase) * amp
            img = glow(img, x, yk, 22, color, strength * 1.3, 2.6)
            img = glow(img, x, yk, 5, "#ffffff", strength * 1.2, 1.4)
    return img


def card_intro_song():
    img = vgrad([(0, "#0b0a24"), (0.45, "#231a4e"), (0.62, "#5a3a78"), (0.7, "#b0648a")])
    img = stars(img, 1, 900, 0.68, 1.1)
    img = glow(img, W * 0.5, H * 0.66, 700, "#ff9a6a", 0.25, 1.8)
    # The Hymn: a staff of light arching over the sea.
    img = staff_lines(img, H * 0.3, 22, 0.4, "#ffd77a", 0.9, seed=3)
    img = staff_lines(img, H * 0.46, 14, 2.1, "#b9a2ff", 0.35, width=1.6, beads=False, seed=4)
    # Distant islands and the sea
    img, _, _ = ridge(img, H * 0.66, 18, 11, "#2a2050", scale=0.006, haze="#7a5a96", haze_amount=0.5)
    sea_top = int(H * 0.69)
    sea = vgrad([(0, "#2a1c4a"), (0.69, "#3a2a5e"), (1.0, "#0c0a1e")])
    # Reflection: the sky above the horizon, flipped and broken by ripples.
    rows = np.arange(sea_top, H)
    src = np.clip(2 * sea_top - rows, 0, H - 1)
    ripple = (np.sin(rows[:, None] * 0.9 + XX[sea_top:] * 0.02) * 6 * ((rows[:, None] - sea_top) / (H - sea_top) + 0.1)).astype(int)
    xs = np.clip(XX[sea_top:].astype(int) + ripple, 0, W - 1)
    refl = img[src[:, None], xs]
    img[sea_top:] = sea[sea_top:] * 0.55 + refl * 0.45
    # Sparkles on the water under the staff
    rng = np.random.default_rng(8)
    for _ in range(140):
        x, y = rng.uniform(W * 0.15, W * 0.85), rng.uniform(sea_top + 4, H)
        img = glow(img, x, y, rng.uniform(3, 9), "#ffd890", 0.5 * (1 - (y - sea_top) / (H - sea_top)), 1.5)
    return finish(img, 1, warm=0.02)


def card_intro_saltmere():
    img = vgrad([(0, "#2a2058"), (0.35, "#6b3f7c"), (0.55, "#d9776a"), (0.66, "#ffc27a")])
    img = stars(img, 2, 260, 0.35, 0.8)
    img = glow(img, W * 0.68, H * 0.64, 380, "#ffd28a", 0.9, 1.6)
    img = glow(img, W * 0.68, H * 0.64, 60, "#fff1c8", 1.4, 1.6)
    # Far headlands
    img, _, _ = ridge(img, H * 0.6, 26, 21, "#7a4a7a", scale=0.003, haze="#ffb084", haze_amount=0.55)
    # The sea
    sea_top = int(H * 0.66)
    img[sea_top:] = vgrad([(0.66, "#c87a74"), (0.8, "#5a3a6a"), (1.0, "#1e1636")])[sea_top:]
    rng = np.random.default_rng(5)
    for _ in range(220):
        x, y = rng.normal(W * 0.68, 120), rng.uniform(sea_top + 2, H * 0.95)
        img = glow(img, x, y, rng.uniform(2, 8), "#ffe0a0", 0.6 * (1 - (y - sea_top) / (H - sea_top)), 1.4)
    # The cliffs on the left and the village on them
    cliff = poly_mask(lambda d, s: d.polygon([(0, 300 * s), (130 * s, 318 * s), (260 * s, 352 * s), (420 * s, 410 * s),
                                               (480 * s, 470 * s), (520 * s, H * s), (0, H * s)], fill=255))
    cliff_col = vgrad([(0.4, "#3a2546"), (1.0, "#140e20")])
    img = blend(img, cliff_col, cliff)
    roofs = poly_mask(lambda d, s: [house(d, s, x, y, w, h) for (x, y, w, h) in
                                    [(40, 318, 70, 50), (120, 326, 60, 44), (190, 346, 80, 52), (285, 370, 58, 40), (350, 398, 64, 46)]])
    img = blend(img, rgb("#24182e"), roofs)
    img = windows(img, [(58, 290, 10, 12), (86, 296, 10, 12), (138, 302, 9, 11), (212, 318, 11, 13), (240, 324, 10, 12),
                        (300, 350, 9, 11), (370, 376, 10, 12)], seed=2)
    # Boats on the water with their oars, and the song rising from them
    boats = poly_mask(lambda d, s: [d.polygon([((x - 34) * s, y * s), ((x + 34) * s, y * s), ((x + 24) * s, (y + 9) * s),
                                               ((x - 24) * s, (y + 9) * s)], fill=255) or
                                    [d.ellipse([(x - 16 + i * 12 - 4) * s, (y - 13) * s, (x - 16 + i * 12 + 4) * s, (y - 3) * s], fill=255)
                                     for i in range(3)] or
                                    [d.line([((x - 16 + i * 12) * s, (y - 2) * s), ((x - 26 + i * 12) * s, (y + 16) * s)], fill=255, width=2 * s)
                                     for i in range(3)]
                                    for (x, y) in [(640, 520), (820, 560), (980, 505)]])
    img = blend(img, rgb("#1a1226"), boats)
    for (x, y) in [(640, 520), (820, 560), (980, 505)]:
        ribbon = np.zeros((H, W))
        ts = np.linspace(0, 1, 160)
        for t in ts:
            px = x + math.sin(t * 7 + x) * 26 * t
            py = y - 20 - t * 230
            ribbon += radial(px, py, 9 * (1 - t) + 3, 2.0) * (1 - t)
        img = add(img, rgb("#ffe39a"), np.clip(ribbon, 0, 1) * 0.6)
    return finish(img, 2, warm=0.03)


def card_intro_crack():
    img = vgrad([(0, "#120818"), (0.4, "#3a1030"), (0.62, "#8a2a3a"), (0.75, "#d0604a")])
    clouds = fbm2(31, scale=220, octaves=5)
    img = blend(img, rgb("#1a0a1e"), np.clip((clouds - 0.48) * 2.2, 0, 1) * np.clip(1.2 - YY / H, 0, 1) * 0.8)
    img = stars(img, 3, 220, 0.45, 0.6, tint="#ffd0d0")
    # The summit and the Last Cantor on it: far ranges, then the peak itself.
    img, _, _ = ridge(img, H * 0.62, 50, 7, "#2a1024", scale=0.003, haze="#8a3040", haze_amount=0.5)
    px, py = 640, 248
    peak_pts = jagged([(px - 30, py), (px + 34, py + 4), (px + 210, py + 150), (px + 420, py + 260), (W + 40, py + 330), (W + 40, H),
                       (-40, H), (-40, py + 340), (px - 380, py + 250), (px - 190, py + 120)], 71, 14)
    peak = poly_mask(lambda d, s: d.polygon([(x * s, y * s) for x, y in peak_pts], fill=255))
    img = blend(img, vgrad([(0.3, "#2a1220"), (1.0, "#0c0610")]), peak)
    lit = np.clip(peak - np.roll(soft(peak, 2), 4, axis=0), 0, 1)
    img = add(img, rgb("#ff8a6a"), lit * 0.9)
    img = glow(img, px, py - 70, 280, "#ff7a5a", 0.6, 1.6)
    fig = poly_mask(lambda d, s: conductor(d, s, px + 2, py + 3, 96, staff_up=True))
    img = blend(img, rgb("#0c0610"), fig)
    img = glow(img, px + 17, py - 133, 30, "#ffd0a0", 1.4, 2.0)
    # The staff of light breaking into shards around him
    rng = np.random.default_rng(13)
    shards = np.zeros((H, W))
    for i in range(70):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(80, 560)
        cx, cy = px + math.cos(a) * r, py - 130 + math.sin(a) * r * 0.55
        ln = rng.uniform(10, 46) * (1 - r / 700)
        ang = a + rng.uniform(-0.5, 0.5)
        m = poly_mask(lambda d, s, cx=cx, cy=cy, ln=ln, ang=ang: d.polygon(
            [((cx + math.cos(ang) * ln) * s, (cy + math.sin(ang) * ln * 0.6) * s),
             ((cx + math.cos(ang + 2.4) * ln * 0.3) * s, (cy + math.sin(ang + 2.4) * ln * 0.3) * s),
             ((cx + math.cos(ang - 2.4) * ln * 0.3) * s, (cy + math.sin(ang - 2.4) * ln * 0.3) * s)], fill=255))
        shards += m * rng.uniform(0.5, 1.0)
    img = add(img, rgb("#ffcf7a"), np.clip(shards, 0, 1) * 1.1)
    img = add(img, rgb("#ff9a5a"), soft(shards, 7) * 0.9)
    # The march: skeletons along the near ridge, against a band of red mist
    mist = np.exp(-((YY - H * 0.8) / 46) ** 2) * (0.6 + 0.4 * fbm2(43, scale=120, octaves=3))
    img = add(img, rgb("#b8343c"), mist * 0.55)
    img, near, near_line = ridge(img, H * 0.86, 22, 41, "#0a0508", scale=0.004, top_light="#c0504a", light_amount=0.5)
    march = poly_mask(lambda d, s: [skeleton(d, s, x, near_line[int(x)] + 2, 84 + (i % 3) * 8, facing=1, blade_up=(i % 2 == 0), phase=i * 1.3)
                                    for i, x in enumerate(range(60, 1240, 118))])
    img = blend(img, rgb("#0a0508"), march)
    img = add(img, rgb("#ff7060"), np.clip(march - np.roll(soft(march, 1.5), 2, axis=0), 0, 1) * 0.9)
    return finish(img, 3, vignette=0.7, warm=0.02)


def card_intro_cantor():
    img = vgrad([(0, "#0c1430"), (0.4, "#1c2a58"), (0.6, "#3a4a7a"), (0.72, "#6a6a94")])
    img = stars(img, 4, 600, 0.6, 1.0, tint="#dfe8ff")
    # The moon over the far summit
    img = glow(img, W * 0.72, H * 0.22, 260, "#9ab0ff", 0.35, 1.6)
    moon = radial(W * 0.72, H * 0.22, 46, 0.25)
    img = blend(img, rgb("#eef0ff"), np.clip(moon * 6, 0, 1))
    img, _, _ = ridge(img, H * 0.5, 90, 51, "#2c3460", scale=0.0022, haze="#6a7aaa", haze_amount=0.6, top_light="#aab8ff", light_amount=0.3)
    img, _, _ = ridge(img, H * 0.62, 40, 52, "#1c2246", scale=0.004, haze="#4a5a8a", haze_amount=0.4)
    # The street: houses with dark windows, lantern posts without flames, the gate
    ground = int(H * 0.8)
    img[ground:] = vgrad([(0.8, "#1a1a30"), (1.0, "#0a0a16")])[ground:]
    street = poly_mask(lambda d, s: [house(d, s, x, ground, w, h) for (x, w, h) in [(-20, 170, 140), (170, 130, 110), (1000, 150, 130), (1150, 160, 150)]]
                       + [d.rectangle([(x - 3) * s, (ground - 120) * s, (x + 3) * s, ground * s], fill=255) or
                          d.rectangle([(x - 10) * s, (ground - 138) * s, (x + 10) * s, (ground - 118) * s], fill=255)
                          for x in (360, 560, 760, 940)]
                       + [d.rectangle([690 * s, (ground - 170) * s, 720 * s, ground * s], fill=255),
                          d.rectangle([860 * s, (ground - 170) * s, 890 * s, ground * s], fill=255),
                          d.chord([690 * s, (ground - 230) * s, 890 * s, (ground - 110) * s], 180, 360, fill=255),
                          ])
    gate_hole = poly_mask(lambda d, s: d.chord([720 * s, (ground - 205) * s, 860 * s, (ground - 120) * s], 180, 360, fill=255)
                          or d.rectangle([720 * s, (ground - 163) * s, 860 * s, ground * s], fill=255))
    street = np.clip(street - gate_hole, 0, 1)
    img = blend(img, rgb("#10101e"), street)
    # One faint ember in a lantern: the last warmth
    img = glow(img, 560, ground - 128, 14, "#ffb060", 0.6, 2.0)
    # The cantor, seen from behind, at the gate
    fig = poly_mask(lambda d, s: hero(d, s, 790, ground + 2, 120, facing=1, sword_up=False))
    img = blend(img, rgb("#06060e"), fig)
    rim = np.clip(fig - np.roll(soft(fig, 2), 3, axis=1), 0, 1)
    img = add(img, rgb("#8aa0ff"), rim * 1.4)
    return finish(img, 4, vignette=0.6)


def card_end_summit():
    img = vgrad([(0, "#141838"), (0.35, "#3a3a6e"), (0.6, "#9a7aaa"), (0.72, "#f0c0b0")])
    img = stars(img, 5, 300, 0.4, 0.7)
    img = glow(img, W * 0.5, H * 0.75, 600, "#ffe0c0", 0.45, 1.5)
    clouds = fbm2(55, scale=260, octaves=4)
    band = np.exp(-((YY - H * 0.7) / 60) ** 2)
    img = blend(img, rgb("#f4dcd6"), np.clip((clouds - 0.4) * 2, 0, 1) * band * 0.7)
    # The summit stones
    img, _, line = ridge(img, H * 0.74, 30, 61, "#1e1a30", scale=0.003, top_light="#ffd0b0", light_amount=0.5)
    # The Last Cantor on his knees, his staff down; the cantor standing over him
    x0 = 560
    g = line[x0]
    fig = poly_mask(lambda d, s: (conductor(d, s, x0, g + 4, 60, staff_up=False),
                                  hero(d, s, x0 + 150, line[x0 + 150] + 2, 112, facing=-1, sword_up=False)))
    img = blend(img, rgb("#100c1a"), fig)
    rim = np.clip(fig - np.roll(soft(fig, 2), -3, axis=0), 0, 1)
    img = add(img, rgb("#ffc8a0"), rim * 1.2)
    # Mist across the foreground
    mist = fbm2(57, scale=180, octaves=4)
    img = blend(img, rgb("#c8b8d8"), np.clip((mist - 0.35) * 1.6, 0, 1) * np.clip((YY - H * 0.78) / 120, 0, 1) * 0.55)
    return finish(img, 5, warm=0.02)


def card_end_verses():
    img = vgrad([(0, "#0c0c26"), (0.5, "#24204e"), (0.75, "#4a3466")])
    img = stars(img, 6, 700, 0.7, 1.0)
    # The mountain on the left, the valley and Saltmere far below on the right
    img, _, line = ridge(img, H * 0.45, 70, 71, "#1a1636", scale=0.0016, octaves=5, persistence=0.45, top_light="#8a7ad0", light_amount=0.35)
    # The verse: a ribbon of gold flowing from the peak down to the village
    ribbon = np.zeros((H, W))
    for t in np.linspace(0, 1, 500):
        x = 180 + t * 860 + math.sin(t * 9) * 40 * (1 - t)
        y = 70 + t * 520 - math.sin(t * math.pi) * 120
        ribbon += radial(x, y, 14 + 10 * math.sin(t * 20) ** 2, 2.0) * 0.6
    mpts = jagged([(-40, 150), (120, 96), (180, 66), (232, 84), (330, 170), (450, 300), (590, 450), (760, 600), (800, H + 20), (-40, H + 20)], 73, 18)
    mount = poly_mask(lambda d, s: d.polygon([(x * s, y * s) for x, y in mpts], fill=255))
    img = blend(img, vgrad([(0.1, "#2a2244"), (1.0, "#0e0a1c")]), mount)
    rock = fbm2(75, scale=90, octaves=5)
    strata = 0.5 + 0.5 * np.sin((XX * 0.7 + YY) / 26.0 + rock * 6.0)
    img = blend(img, rgb("#3a3266"), mount * np.clip((rock - 0.45) * 1.6, 0, 1) * 0.6)
    img = blend(img, rgb("#0a0816"), mount * strata * 0.25)
    img = add(img, rgb("#ffcf7a"), mount * soft(np.clip(ribbon, 0, 1), 40) * 0.25)
    img = add(img, rgb("#9a8ae0"), np.clip(mount - np.roll(soft(mount, 2), 3, axis=0), 0, 1) * 0.8)
    img = add(img, rgb("#ffd37a"), np.clip(ribbon, 0, 1) * 1.2)
    img = add(img, rgb("#ff9a6a"), soft(np.clip(ribbon, 0, 1), 18) * 0.8)
    img = glow(img, 180, 70, 120, "#fff0c0", 0.9, 2.0)
    # Saltmere: a cluster of lights by the sea
    sea_top = int(H * 0.86)
    img[sea_top:] = blend(img[sea_top:], rgb("#141432"), np.ones((H - sea_top, W)) * 0.8)
    village = poly_mask(lambda d, s: [house(d, s, x, sea_top, w, h, roof=0.5) for (x, w, h) in
                                      [(900, 34, 26), (936, 28, 20), (968, 40, 30), (1012, 30, 22), (1046, 36, 28), (1086, 26, 18)]])
    img = blend(img, rgb("#0c0a18"), village)
    rng = np.random.default_rng(9)
    for _ in range(26):
        x, y = rng.uniform(895, 1120), rng.uniform(sea_top - 34, sea_top - 4)
        img = glow(img, x, y, rng.uniform(5, 14), "#ffc070", 1.0, 1.8)
    img = glow(img, 1000, sea_top - 20, 160, "#ffb060", 0.35, 1.8)
    for _ in range(60):
        x, y = rng.uniform(880, 1140), rng.uniform(sea_top + 3, H)
        img = glow(img, x, y, rng.uniform(2, 6), "#ffc070", 0.5, 1.4)
    return finish(img, 6)


def card_end_home():
    img = vgrad([(0, "#0e1030"), (0.45, "#2a2456"), (0.7, "#5a3a6a")])
    img = stars(img, 7, 500, 0.6, 0.9)
    # The star over the gate
    sx, sy = W * 0.5, H * 0.2
    img = glow(img, sx, sy, 300, "#ffd890", 0.45, 1.6)
    star = poly_mask(lambda d, s: d.polygon([((sx + math.cos(math.pi / 2 + i * math.pi / 5) * (34 if i % 2 == 0 else 14)) * s,
                                              (sy - math.sin(math.pi / 2 + i * math.pi / 5) * (34 if i % 2 == 0 else 14)) * s)
                                             for i in range(10)], fill=255))
    img = blend(img, rgb("#fff4d0") * 1.3, star)
    img = add(img, rgb("#ffd080"), soft(star, 12) * 1.2)
    # Fireworks of light: bursts of sparks
    rng = np.random.default_rng(17)
    for (cx, cy, col) in [(300, 170, "#ff9ad0"), (980, 140, "#9ad8ff"), (180, 300, "#ffe08a"), (1120, 290, "#c8a0ff")]:
        for _ in range(60):
            a = rng.uniform(0, 2 * math.pi)
            r = rng.uniform(20, 90)
            img = glow(img, cx + math.cos(a) * r, cy + math.sin(a) * r, rng.uniform(2, 5), col, 0.9 * (r / 90), 1.5)
        img = glow(img, cx, cy, 100, col, 0.25, 2)
    # The street: warm windows, lit lanterns, bunting, the gate, people
    ground = int(H * 0.82)
    img[ground:] = vgrad([(0.82, "#2a1e2a"), (1.0, "#120c16")])[ground:]
    img = glow(img, W * 0.5, ground, 700, "#ffa060", 0.35, 1.8)
    hs = [(-20, 190, 150), (175, 150, 120), (930, 160, 140), (1095, 200, 170)]
    street = poly_mask(lambda d, s: [house(d, s, x, ground, w, h) for (x, w, h) in hs]
                       + [d.rectangle([(x - 3) * s, (ground - 130) * s, (x + 3) * s, ground * s], fill=255) for x in (370, 520, 760, 900)]
                       + [d.rectangle([560 * s, (ground - 190) * s, 594 * s, ground * s], fill=255),
                          d.rectangle([686 * s, (ground - 190) * s, 720 * s, ground * s], fill=255),
                          d.chord([560 * s, (ground - 255) * s, 720 * s, (ground - 125) * s], 180, 360, fill=255)])
    hole = poly_mask(lambda d, s: d.chord([594 * s, (ground - 222) * s, 686 * s, (ground - 150) * s], 180, 360, fill=255)
                     or d.rectangle([594 * s, (ground - 186) * s, 686 * s, ground * s], fill=255))
    img = blend(img, rgb("#1a1020"), np.clip(street - hole, 0, 1))
    img = windows(img, [(20, ground - 110, 22, 26), (80, ground - 110, 22, 26), (130, ground - 60, 22, 26), (200, ground - 90, 20, 24),
                        (260, ground - 90, 20, 24), (960, ground - 100, 22, 26), (1030, ground - 100, 22, 26), (1130, ground - 120, 24, 28),
                        (1200, ground - 120, 24, 28)], seed=7)
    for x in (370, 520, 760, 900):
        img = glow(img, x, ground - 140, 16, "#fff0c0", 1.6, 1.6)
        img = glow(img, x, ground - 140, 90, "#ffb060", 0.6, 1.8)
    # Bunting between the lanterns
    flags = np.zeros((H, W))
    cols = ["#ff7a8a", "#ffd27a", "#8ad0ff", "#b89aff"]
    rng = np.random.default_rng(3)
    for (a, b) in [(370, 520), (520, 560), (720, 760), (760, 900)]:
        for i in range(int((b - a) / 18)):
            t = (i + 0.5) / int((b - a) / 18)
            x = a + (b - a) * t
            y = ground - 134 + math.sin(math.pi * t) * 22
            m = poly_mask(lambda d, s, x=x, y=y: d.polygon([((x - 7) * s, y * s), ((x + 7) * s, y * s), (x * s, (y + 14) * s)], fill=255))
            img = blend(img, rgb(cols[i % 4]) * 0.9, m)
    # People: villagers and the cantor
    people = poly_mask(lambda d, s: [hero(d, s, x, ground + 1, h, facing=f, shield=False, cape=False, sword_up=False) if k != 2 else
                                     hero(d, s, x, ground + 1, h, facing=f, sword_up=True)
                                     for k, (x, h, f) in enumerate([(430, 70, 1), (480, 62, -1), (640, 92, 1), (820, 66, -1), (860, 58, 1)])])
    img = blend(img, rgb("#0e0810"), people)
    rim = np.clip(people - np.roll(soft(people, 2), 3, axis=0), 0, 1)
    img = add(img, rgb("#ffb878"), rim * 1.1)
    return finish(img, 7, warm=0.04)


# ---------------- Chapter two: the Drowned Nave (VS-6) ----------------

def god_rays(img, seed, x_top, spread, color, strength=0.25, count=9):
    """Soft shafts of light falling from the surface, fanning out and fading with depth."""
    rng = np.random.default_rng(seed)
    rays = np.zeros((H, W))
    for _ in range(count):
        x0 = x_top + rng.uniform(-spread, spread)
        slope = rng.uniform(-0.35, 0.35)
        width = rng.uniform(18, 60)
        centre = x0 + slope * YY
        rays += np.exp(-((XX - centre) / (width + YY * 0.08)) ** 2) * rng.uniform(0.4, 1.0)
    rays *= np.clip(1.1 - YY / H, 0, 1) ** 1.4
    return add(img, rgb(color), rays * strength)


def bell_shape(d, s, cx, base, w, h):
    """A great bell standing mouth-down, its lip at y = base."""
    prof = [(0.0, 1.0), (0.18, 0.99), (0.3, 0.94), (0.36, 0.8), (0.4, 0.5), (0.46, 0.22), (0.56, 0.06), (0.58, 0.0)]
    pts = [(cx + px * w, base - py * h) for px, py in prof] + [(cx - px * w, base - py * h) for px, py in reversed(prof)]
    d.polygon([(x * s, y * s) for x, y in pts], fill=255)


def nave(d, s, x, base, w, h):
    """A drowned cathedral's silhouette: a long nave, two west towers with spires, buttresses and a broken roof."""
    d.rectangle([x * s, (base - h * 0.5) * s, (x + w) * s, base * s], fill=255)
    for tx in (x, x + w * 0.12):
        d.rectangle([tx * s, (base - h) * s, (tx + w * 0.1) * s, base * s], fill=255)
        d.polygon([(tx * s, (base - h) * s), ((tx + w * 0.05) * s, (base - h * 1.35) * s), ((tx + w * 0.1) * s, (base - h) * s)], fill=255)
    for i in range(6):
        bx = x + w * (0.3 + i * 0.12)
        d.polygon([(bx * s, (base - h * 0.5) * s), ((bx + w * 0.04) * s, (base - h * 0.62) * s), ((bx + w * 0.06) * s, (base - h * 0.5) * s)], fill=255)
    d.polygon([((x + w * 0.85) * s, (base - h * 0.5) * s), ((x + w) * s, (base - h * 0.75) * s), ((x + w) * s, (base - h * 0.5) * s)], fill=255)


def card_deep_descent():
    img = vgrad([(0, "#5ab8c0"), (0.18, "#2a7a8e"), (0.55, "#0e3a52"), (1.0, "#04121e")])
    img = god_rays(img, 81, W * 0.45, 380, "#bff6ff", 0.22, 12)
    # The sea floor and the cathedral on it
    img, _, line = ridge(img, H * 0.82, 18, 83, "#06141c", scale=0.003, top_light="#3a8a9a", light_amount=0.35)
    church = poly_mask(lambda d, s: nave(d, s, 470, line[700] + 6, 560, 260))
    img = blend(img, rgb("#0a2230"), church)
    img = add(img, rgb("#4ab0c0"), np.clip(church - np.roll(soft(church, 2), 3, axis=0), 0, 1) * 0.6)
    img = glow(img, 540, line[540] - 170, 28, "#9ff0ff", 0.9, 1.6)          # the rose window in the west front
    img = glow(img, 760, line[760] - 70, 220, "#2a9ab0", 0.25, 1.6)
    # The harbour chain from the surface, and the cantor climbing down it
    chain = poly_mask(lambda d, s: [d.ellipse([(345 - 5) * s, (y - 9) * s, (345 + 5) * s, (y + 9) * s], outline=255, width=int(2 * s))
                                    for y in range(-10, 420, 16)])
    img = blend(img, rgb("#0a1a22"), chain * 0.9)
    fig = poly_mask(lambda d, s: hero(d, s, 345, 470, 70, facing=1, sword_up=False))
    img = blend(img, rgb("#081620"), fig)
    img = add(img, rgb("#bff6ff"), np.clip(fig - np.roll(soft(fig, 2), 3, axis=0), 0, 1) * 0.9)
    # Bubbles rising from him, and marine snow everywhere
    rng = np.random.default_rng(85)
    for _ in range(26):
        img = glow(img, 345 + rng.normal(0, 14), rng.uniform(40, 420), rng.uniform(2, 6), "#dffcff", 0.7, 1.2)
    img = stars(img, 86, 500, 1.0, 0.45, tint="#bfe8f0", fade_by_y=False)
    return finish(img, 81, vignette=0.6)


def card_deep_airbell():
    img = vgrad([(0, "#0c3446"), (0.5, "#072030"), (1.0, "#02080e")])
    img = god_rays(img, 91, W * 0.5, 500, "#8fe0f0", 0.1, 8)
    floor_y = H * 0.84
    img, _, line = ridge(img, floor_y, 10, 93, "#05101a", scale=0.003)
    # The great bell from outside, and cut open: the lamplit air pocket and the camp inside it
    cx, base, bw, bh = W * 0.5, floor_y + 4, 520, 470
    bell = poly_mask(lambda d, s: bell_shape(d, s, cx, base, bw, bh))
    inner = poly_mask(lambda d, s: bell_shape(d, s, cx, base - 14, bw * 0.92, bh * 0.92))
    img = blend(img, vgrad([(0.2, "#3a2a1a"), (0.9, "#1a120a")]), bell)
    pocket = np.clip(inner * (YY < base - 60), 0, 1)
    img = blend(img, vgrad([(0.2, "#5a3a1e"), (0.8, "#8a5a2a")]), pocket)
    img = add(img, rgb("#c08a4a"), np.clip(bell - np.roll(soft(bell, 2), 3, axis=0), 0, 1) * 0.7)
    water = np.clip(inner * (YY >= base - 60), 0, 1)
    img = blend(img, rgb("#0e4a5a"), water * 0.9)
    img = add(img, rgb("#6ae0f0"), water * np.exp(-((YY - (base - 60)) / 6) ** 2) * 1.2)
    for lx in (cx - 170, cx - 40, cx + 110):
        img = glow(img, lx, base - 200, 150, "#ffb060", 0.55, 1.8)
        img = glow(img, lx, base - 200, 12, "#fff0c0", 1.4, 1.5)
    people = poly_mask(lambda d, s: (hero(d, s, cx - 110, base - 60, 64, facing=1),
                                     hero(d, s, cx + 20, base - 60, 60, facing=-1, sword_up=False, shield=False, cape=False),
                                     hero(d, s, cx + 170, base - 60, 54, facing=-1, sword_up=False, shield=False, cape=False)))
    img = blend(img, rgb("#1a0e06"), people * pocket.clip(0, 1))
    return finish(img, 91, warm=0.03, vignette=0.6)


def card_end2_precentor():
    img = vgrad([(0, "#0a2a3a"), (0.5, "#0e3e50"), (1.0, "#041018")])
    img = god_rays(img, 101, W * 0.5, 160, "#aef2ff", 0.3, 7)
    img, _, line = ridge(img, H * 0.78, 6, 103, "#08161e", scale=0.002, top_light="#5ac8d8", light_amount=0.5)
    # The great bell hanging still behind them
    bell = poly_mask(lambda d, s: bell_shape(d, s, W * 0.5, H * 0.5, 180, 210))
    img = blend(img, rgb("#1e3030"), bell * 0.85)
    img = add(img, rgb("#7ad8d0"), np.clip(bell - np.roll(soft(bell, 2), 3, axis=0), 0, 1) * 0.6)
    x0 = 520
    fig = poly_mask(lambda d, s: (conductor(d, s, x0, line[x0] + 4, 74, staff_up=False),
                                  hero(d, s, x0 + 200, line[x0 + 200] + 2, 112, facing=-1, sword_up=False)))
    img = blend(img, rgb("#061018"), fig)
    img = add(img, rgb("#aef2ff"), np.clip(fig - np.roll(soft(fig, 2), 3, axis=0), 0, 1) * 1.1)
    img = stars(img, 104, 400, 1.0, 0.35, tint="#bfe8f0", fade_by_y=False)
    return finish(img, 101, vignette=0.65)


def card_end2_bell():
    img = vgrad([(0, "#2a6a7a"), (0.4, "#0e3446"), (1.0, "#020a12")])
    cx, cy = W * 0.5, H * 0.66
    bell = poly_mask(lambda d, s: bell_shape(d, s, cx, cy, 200, 230))
    # Rings of sound spreading through the water
    for k, r in enumerate((150, 230, 330, 450, 590)):
        ring = np.exp(-((np.sqrt((XX - cx) ** 2 + ((YY - (cy - 110)) * 1.25) ** 2) - r) / (6 + k * 2)) ** 2)
        img = add(img, rgb("#7ae8f0"), ring * (0.5 - k * 0.07))
    img = blend(img, vgrad([(0.4, "#6a5a3a"), (0.8, "#2a2014")]), bell)
    img = add(img, rgb("#ffe0a0"), np.clip(bell - np.roll(soft(bell, 2), 3, axis=0), 0, 1) * 1.0)
    img = glow(img, cx, cy - 120, 260, "#ffd890", 0.35, 1.6)
    # Three verses rising to the light: ribbons of gold
    for i, (dx, ph) in enumerate(((-120, 0.0), (0, 1.3), (120, 2.6))):
        ribbon = np.zeros((H, W))
        for t in np.linspace(0, 1, 260):
            x = cx + dx * (1 - t * 0.5) + math.sin(t * 10 + ph) * 26
            y = cy - 240 - t * (cy - 220)
            ribbon += radial(x, y, 9 + 6 * (1 - t), 2.0) * 0.7
        img = add(img, rgb("#ffd37a"), np.clip(ribbon, 0, 1) * 1.1)
        img = add(img, rgb("#ff9a6a"), soft(np.clip(ribbon, 0, 1), 14) * 0.5)
    img = god_rays(img, 111, cx, 260, "#cff8ff", 0.18, 8)
    return finish(img, 111, warm=0.02, vignette=0.6)


def card_end2_road():
    img = vgrad([(0, "#0c0c28"), (0.4, "#26224e"), (0.55, "#3a3060")])
    img = stars(img, 121, 600, 0.55, 1.0)
    sea = int(H * 0.56)
    # Saltmere's cliffs on the left, lit windows; the sea below, and the deep's answer glowing under it
    img, _, line = ridge(img, H * 0.42, 40, 123, "#14122a", scale=0.002, top_light="#8a7ad0", light_amount=0.3)
    img[sea:] = vgrad([(0.5, "#0e2a3a"), (0.75, "#08202e"), (1.0, "#020810")])[sea:]
    img = glow(img, W * 0.62, H * 0.86, 360, "#3ac0d0", 0.6, 1.6)
    img = glow(img, W * 0.62, H * 0.86, 60, "#bff6ff", 0.8, 1.6)
    for k, r in enumerate((90, 160, 250)):
        ring = np.exp(-((np.sqrt((XX - W * 0.62) ** 2 + ((YY - H * 0.86) * 2.2) ** 2) - r) / 5) ** 2) * (YY > sea)
        img = add(img, rgb("#7ae8f0"), ring * (0.45 - k * 0.1))
    img = add(img, rgb("#c0d8ff"), np.exp(-((YY - sea) / 2.0) ** 2) * 0.5)
    rng = np.random.default_rng(125)
    for _ in range(22):
        x = rng.uniform(80, 520)
        y = line[int(x)] + rng.uniform(6, 50)
        img = glow(img, x, y, rng.uniform(4, 10), "#ffc070", 1.0, 1.8)
    for _ in range(40):
        x, y = rng.uniform(60, 1200), rng.uniform(sea + 4, sea + 60)
        img = glow(img, x, y, rng.uniform(2, 5), "#ffd890", 0.35, 1.4)
    return finish(img, 121, warm=0.02)


CARDS = {
    "intro_song": card_intro_song,
    "intro_saltmere": card_intro_saltmere,
    "intro_crack": card_intro_crack,
    "intro_cantor": card_intro_cantor,
    "end_summit": card_end_summit,
    "end_verses": card_end_verses,
    "end_home": card_end_home,
    "deep_descent": card_deep_descent,
    "deep_airbell": card_deep_airbell,
    "end2_precentor": card_end2_precentor,
    "end2_bell": card_end2_bell,
    "end2_road": card_end2_road,
}


def main():
    only = sys.argv[1] if len(sys.argv) > 1 else None
    for name, fn in CARDS.items():
        if only and name != only:
            continue
        save(fn(), name)


if __name__ == "__main__":
    main()
