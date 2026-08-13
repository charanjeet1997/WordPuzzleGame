"""
Rebuilds game_background.png to match the "Word Wonders Screens" backdrop exactly.

The design composes it from layers:
  linear-gradient(180deg,#0d1b2e 0%,#122b3d 38%,#0a1a26 70%,#060f16 100%)
  + two radial aurora glows
  + 26 stars placed by a seeded LCG
  + two mountain silhouettes (clip-path polygons)
  + 9 pine triangles
  + a solid ground bar

Authored at 3x the 360x780 design canvas so it maps 1:1 onto the camera at PPU 260
(2340 px / 9 world units).
"""
import os
from PIL import Image, ImageDraw, ImageFilter

# Authored at the widest portrait aspect the devices file covers (iPad 4:3 -> 1755
# design units wide at a constant 2340 tall), so it still fills the screen on a tablet
# and simply crops at the sides on a 9:19.5 phone.
S = 3                      # design px -> texture px
DW, DH = 585, 780          # design units (585 = 1755/3, the iPad-width case)
W, H = DW * S, DH * S

out_dirs = [r"Assets/WordPuzzle/Sprites", r"Assets/WordPuzzle/Resources/Sprites"]
for d in out_dirs:
    os.makedirs(d, exist_ok=True)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def vertical_gradient(stops):
    """stops: list of (position 0-1, rgb)."""
    img = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(img)
    for y in range(H):
        t = y / float(H - 1)
        for i in range(len(stops) - 1):
            p0, c0 = stops[i]
            p1, c1 = stops[i + 1]
            if p0 <= t <= p1:
                local = 0 if p1 == p0 else (t - p0) / (p1 - p0)
                d.line([(0, y), (W, y)], fill=lerp(c0, c1, local))
                break
        else:
            d.line([(0, y), (W, y)], fill=stops[-1][1])
    return img.convert("RGBA")


def radial_glow(cx, cy, radius, rgb, peak_alpha):
    """radial-gradient(circle, rgba(...), transparent 70%)"""
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    steps = 90
    for i in range(steps, 0, -1):
        t = i / float(steps)                 # 1 at the rim, 0 at the centre
        a = 0.0 if t > 0.7 else peak_alpha * (1.0 - t / 0.7)
        if a <= 0:
            continue
        r = radius * t
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=rgb + (int(a * 255),))
    return layer.filter(ImageFilter.GaussianBlur(radius * 0.05))


def seeded_rand(seed):
    """The design's LCG: s = (s*9301 + 49297) % 233280; return s/233280."""
    s = seed

    def nxt():
        nonlocal s
        s = (s * 9301 + 49297) % 233280
        return s / 233280.0
    return nxt


def poly(points_pct, box):
    """clip-path polygon in % -> absolute points inside box (l, t, w, h)."""
    l, t, w, h = box
    return [(l + x / 100.0 * w, t + y / 100.0 * h) for x, y in points_pct]


def build():
    img = vertical_gradient([
        (0.00, (0x0d, 0x1b, 0x2e)),
        (0.38, (0x12, 0x2b, 0x3d)),
        (0.70, (0x0a, 0x1a, 0x26)),
        (1.00, (0x06, 0x0f, 0x16)),
    ])

    # aurora glows: top-left teal, top-right blue
    img.alpha_composite(radial_glow((-40 + 120) * S, (-60 + 120) * S, 120 * S, (90, 220, 190), 0.35))
    img.alpha_composite(radial_glow((DW + 60 - 105) * S, (20 + 105) * S, 105 * S, (80, 160, 220), 0.30))

    # stars - same generator and call order as the design, so the field matches
    rnd = seeded_rand(7)
    stars = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ds = ImageDraw.Draw(stars)
    for _ in range(30):
        w = (1 + rnd() * 1.6) * S
        h = (1 + rnd() * 1.6) * S
        alpha = 0.3 + rnd() * 0.5
        top = rnd() * 45 / 100.0 * H
        left = rnd() * 100 / 100.0 * W
        ds.ellipse([left, top, left + w, top + h], fill=(255, 255, 255, int(alpha * 255)))
    img.alpha_composite(stars.filter(ImageFilter.GaussianBlur(0.6)))

    d = ImageDraw.Draw(img)

    # mountains
    # Bases sit ON the ground bar. Floating them above it (as the raw design offsets do)
    # leaves a flat lit band between the peaks and the treeline.
    GROUND = 88
    d.polygon(poly([(0, 100), (0, 55), (30, 20), (55, 45), (75, 15), (100, 40), (100, 100)],
                   (-10 * S, H - (GROUND + 152) * S, DW * 0.58 * S, 152 * S)),
              fill=(0x0c, 0x21, 0x30, 255))
    # Wide overlap with ridge A, so its steep left edge is hidden instead of reading
    # as a vertical seam across the middle of the sky.
    d.polygon(poly([(0, 100), (10, 35), (35, 60), (55, 10), (80, 50), (100, 25), (100, 100)],
                   (DW * 0.36 * S, H - (GROUND + 156) * S, DW * 0.66 * S, 156 * S)),
              fill=(0x0a, 0x1c, 0x28, 255))

    # pines
    import math
    for i in range(math.ceil(DW / 42) + 1):
        x = (i * 42 - 8) * S
        h = (28 + (i % 3) * 13) * S
        base_y = H - 88 * S
        half = h * 0.32
        d.polygon([(x, base_y), (x + half * 2, base_y), (x + half, base_y - h)],
                  fill=(0x08, 0x14, 0x18, 255))

    # Ground. The design's flat #050b10 reads as a black void against the ridges, so it is
    # lifted and given a top-to-bottom ramp: lighter where it meets the treeline, darker at
    # the screen edge. The ramp also softens the hard horizontal seam under the pines.
    ground_h = 88 * S
    gtop, gbot = (0x11, 0x1f, 0x29), (0x07, 0x0e, 0x14)
    for i in range(ground_h):
        f = i / float(ground_h - 1)
        d.line([(0, H - ground_h + i), (W, H - ground_h + i)],
               fill=(int(gtop[0] + (gbot[0] - gtop[0]) * f),
                     int(gtop[1] + (gbot[1] - gtop[1]) * f),
                     int(gtop[2] + (gbot[2] - gtop[2]) * f), 255))

    for folder in out_dirs:
        img.convert("RGB").save(os.path.join(folder, "game_background.png"))
    print("game_background.png written at %dx%d" % (W, H))


if __name__ == "__main__":
    build()
