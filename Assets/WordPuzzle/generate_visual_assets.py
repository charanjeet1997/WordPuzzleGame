"""
Sprite set for Wonders of Word, generated to match the "Word Wonders Screens" design.

USS supports no gradients, no box-shadow and no blur, so every gradient fill, rim and
drop shadow from the design is baked into a PNG here and applied as a 9-sliced
background-image.

Scale: the design canvas is 360x780 CSS px; the UI panel reference is 1080x1920, so UI
values are design px x3. Sprites are authored so their 9-slice corners equal the design
corner radius x3.
"""
import os
from PIL import Image, ImageDraw, ImageFilter

SS = 4  # supersample factor

output_dir = r"Assets/WordPuzzle/Sprites"
resources_dir = r"Assets/WordPuzzle/Resources/Sprites"
for d in (output_dir, resources_dir):
    if not os.path.exists(d):
        os.makedirs(d)

# ------------------------------------------------------------------ palette (from design)
GOLD_TOP = (255, 226, 160)      # #ffe2a0
GOLD_BOT = (230, 178, 90)       # #e6b25a
GOLD_LEDGE = (120, 75, 15, 102)  # 0 8px 0 rgba(120,75,15,.4)
GOLD_INK = (74, 44, 5)          # #4a2c05

GLASS_FILL = (10, 25, 35, 140)          # rgba(10,25,35,.55)
GLASS_RIM = (255, 255, 255, 36)         # rgba(255,255,255,.14)

PAUSE_TOP, PAUSE_BOT = (23, 50, 71), (14, 33, 48)   # #173247 -> #0e2130
CLEAR_TOP, CLEAR_BOT = (26, 58, 77), (14, 33, 48)   # #1a3a4d -> #0e2130
CARD_RIM = (255, 255, 255, 40)

TILE_HIDDEN_FILL = (255, 255, 255, 26)   # rgba(255,255,255,.10)
TILE_HIDDEN_RIM = (255, 255, 255, 71)    # rgba(255,255,255,.28)
NODE_FILL = (255, 255, 255, 31)          # rgba(255,255,255,.12)
NODE_RIM = (255, 255, 255, 64)           # rgba(255,255,255,.25)


def save(img, name):
    img.save(os.path.join(output_dir, name))
    img.save(os.path.join(resources_dir, name))


def vgrad_rgba(w, h, top, bottom, alpha=255):
    img = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / float(max(1, h - 1))
        d.line([(0, y), (w, y)], fill=(
            int(top[0] + (bottom[0] - top[0]) * t),
            int(top[1] + (bottom[1] - top[1]) * t),
            int(top[2] + (bottom[2] - top[2]) * t),
            alpha))
    return img


def rounded_mask(size, radius, inset=0):
    m = Image.new("L", (size, size), 0)
    ImageDraw.Draw(m).rounded_rectangle(
        [inset, inset, size - 1 - inset, size - 1 - inset], radius=radius, fill=255)
    return m


def panel(name, size, radius, fill, rim, rim_w):
    """Flat translucent panel with a hairline rim. 9-slice inset = radius + rim_w."""
    s, r = size * SS, radius * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=r, fill=fill,
                        outline=rim, width=max(1, rim_w * SS))
    save(img.resize((size, size), Image.LANCZOS), name)


def gradient_panel(name, size, radius, top, bottom, rim, rim_w, ledge=None, ledge_h=0):
    """Vertical-gradient card/button, optional baked bottom ledge (the design's 0 8px 0 shadow)."""
    s, r = size * SS, radius * SS
    body = vgrad_rgba(s, s, top, bottom)
    body.putalpha(rounded_mask(s, r))

    if ledge is not None and ledge_h > 0:
        lh = ledge_h * SS
        base = Image.new("RGBA", (s, s + lh), (0, 0, 0, 0))
        shadow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
        ImageDraw.Draw(shadow).rounded_rectangle([0, 0, s - 1, s - 1], radius=r, fill=ledge)
        base.alpha_composite(shadow, (0, lh))
        base.alpha_composite(body, (0, 0))
        body = base.resize((size, size + ledge_h), Image.LANCZOS)
        d = ImageDraw.Draw(body)
        save(body, name)
        return

    d = ImageDraw.Draw(body)
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=r, outline=rim, width=max(1, rim_w * SS))
    save(body.resize((size, size), Image.LANCZOS), name)


# ------------------------------------------------------------------ UI panels (9-slice)
def generate_ui_panels():
    # chapter card / toast: design radius 18 -> 54 ref px
    panel("dc_panel_soft.png", 160, 54, GLASS_FILL, GLASS_RIM, 3)
    # HUD pills, word preview: design radius 16 -> 48 ref px
    panel("dc_pill_soft.png", 128, 48, GLASS_FILL, GLASS_RIM, 3)
    # icon buttons: rgba(255,255,255,.09) + rgba(255,255,255,.16)
    panel("dc_icon_soft.png", 128, 48, (255, 255, 255, 23), (255, 255, 255, 41), 3)
    # secondary modal row: rgba(255,255,255,.08) / .18
    panel("dc_row_soft.png", 128, 42, (255, 255, 255, 20), (255, 255, 255, 46), 3)
    # tertiary modal row: rgba(255,255,255,.05) / .12
    panel("dc_row_faint.png", 128, 42, (255, 255, 255, 13), (255, 255, 255, 31), 3)
    # reward chip: rgba(0,0,0,.25), design radius 14 -> 42
    panel("dc_chip_dark.png", 128, 42, (0, 0, 0, 64), (0, 0, 0, 0), 0)

    # gold buttons, with the design's solid bottom ledge baked in
    # No baked ledge: a 9-slice cannot preserve a bottom-only shadow band without
    # collapsing the vertical middle region, so the gradient is the sprite's whole job.
    gradient_panel("dc_btn_gold.png", 240, 80, GOLD_TOP, GOLD_BOT, (255, 255, 255, 64), 2)
    gradient_panel("dc_btn_gold_sm.png", 128, 42, GOLD_TOP, GOLD_BOT, (255, 255, 255, 64), 2)

    # modal cards
    gradient_panel("dc_card_pause.png", 200, 66, PAUSE_TOP, PAUSE_BOT, CARD_RIM, 3)
    gradient_panel("dc_card_clear.png", 200, 72, CLEAR_TOP, CLEAR_BOT, CARD_RIM, 3)


# ------------------------------------------------------------------ world sprites
def generate_grid_tiles():
    """Design cell: 40px, radius 8 -> radius/size = 0.2, so 26px on a 128px sprite."""
    size, s, r = 128, 128 * SS, 26 * SS

    hidden = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(hidden).rounded_rectangle(
        [0, 0, s - 1, s - 1], radius=r, fill=TILE_HIDDEN_FILL,
        outline=TILE_HIDDEN_RIM, width=5 * SS)
    save(hidden.resize((size, size), Image.LANCZOS), "grid_tile_hidden.png")

    body = vgrad_rgba(s, s, GOLD_TOP, GOLD_BOT)
    body.putalpha(rounded_mask(s, r))
    save(body.resize((size, size), Image.LANCZOS), "grid_tile_revealed.png")


def generate_nodes():
    size, s, p = 128, 128 * SS, 2 * SS

    normal = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(normal).ellipse([p, p, s - p, s - p], fill=NODE_FILL,
                                   outline=NODE_RIM, width=3 * SS)
    save(normal.resize((size, size), Image.LANCZOS), "letter_node_normal.png")

    body = vgrad_rgba(s, s, GOLD_TOP, GOLD_BOT)
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).ellipse([p, p, s - p, s - p], fill=255)
    body.putalpha(mask)
    save(body.resize((size, size), Image.LANCZOS), "letter_node_selected.png")


def generate_wheel_backdrop():
    size, s, p = 256, 256 * SS, 4 * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(img).ellipse([p, p, s - p, s - p], fill=(10, 25, 35, 105),
                                outline=(255, 255, 255, 30), width=3 * SS)
    save(img.resize((size, size), Image.LANCZOS), "wheel_backdrop.png")


def generate_coin():
    """radial-gradient(circle at 35% 30%, #fff3c9, #e6b25a 60%, #a8752a)"""
    size, s = 128, 128 * SS
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx, cy, rad = s * 0.35, s * 0.30, s * 0.92
    steps = 140
    for i in range(steps, 0, -1):
        t = i / float(steps)
        if t > 0.6:
            u = (t - 0.6) / 0.4
            col = (int(230 + (168 - 230) * u), int(178 + (117 - 178) * u), int(90 + (42 - 90) * u))
        else:
            u = t / 0.6
            col = (int(255 + (230 - 255) * u), int(243 + (178 - 243) * u), int(201 + (90 - 201) * u))
        rr = rad * t
        d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=col + (255,))
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).ellipse([0, 0, s - 1, s - 1], fill=255)
    img.putalpha(mask)
    save(img.resize((size, size), Image.LANCZOS), "icon_coin.png")


def generate_stars():
    """Exact star polygon from the design's clip-path."""
    poly = [(50, 0), (63, 35), (100, 38), (72, 60), (82, 96),
            (50, 75), (18, 96), (28, 60), (0, 38), (37, 35)]
    size, s = 128, 128 * SS

    for name, filled in (("star_filled.png", True), ("star_empty.png", False)):
        img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
        pts = [(x / 100.0 * s, y / 100.0 * s) for x, y in poly]
        if filled:
            grad = vgrad_rgba(s, s, GOLD_TOP, GOLD_BOT)
            mask = Image.new("L", (s, s), 0)
            ImageDraw.Draw(mask).polygon(pts, fill=255)
            grad.putalpha(mask)
            img = grad
        else:
            ImageDraw.Draw(img).polygon(pts, fill=(255, 255, 255, 38))
        save(img.resize((size, size), Image.LANCZOS), name)


def generate_menu_icons():
    """Shapes taken from the design's clip-path / border definitions, drawn as sprites so
    they do not depend on the font having a matching glyph."""
    size, s = 128, 128 * SS

    # Speaker: polygon(0% 30%,35% 30%,65% 0%,65% 100%,35% 70%,0% 70%)
    poly = [(0, 30), (35, 30), (65, 0), (65, 100), (35, 70), (0, 70)]
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    pts = [(x / 100.0 * s * 0.86 + s * 0.07, y / 100.0 * s * 0.86 + s * 0.07) for x, y in poly]
    d = ImageDraw.Draw(img)
    d.polygon(pts, fill=(255, 255, 255, 235))
    # two arcs suggesting output
    for i, rr in enumerate((0.20, 0.30)):
        box = [s * (0.55 - rr), s * (0.5 - rr), s * (0.55 + rr), s * (0.5 + rr)]
        d.arc(box, -55, 55, fill=(255, 255, 255, 200 - i * 60), width=5 * SS)
    save(img.resize((size, size), Image.LANCZOS), "icon_sound.png")

    # Settings: ring with spokes
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pad = s * 0.20
    d.ellipse([pad, pad, s - pad, s - pad], outline=(255, 255, 255, 235), width=7 * SS)
    d.ellipse([s * 0.42, s * 0.42, s * 0.58, s * 0.58], fill=(255, 255, 255, 235))
    save(img.resize((size, size), Image.LANCZOS), "icon_settings.png")

    # Pause: two bars, from the HUD design (4x13 px each)
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    bw, bh = s * 0.13, s * 0.46
    for cx in (s * 0.38, s * 0.62):
        d.rounded_rectangle([cx - bw / 2, s * 0.5 - bh / 2, cx + bw / 2, s * 0.5 + bh / 2],
                            radius=bw * 0.25, fill=(255, 255, 255, 240))
    save(img.resize((size, size), Image.LANCZOS), "icon_pause.png")

    # Shuffle: open circle with an arrow head, matching the design's spinner glyph
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    pad = s * 0.22
    d.arc([pad, pad, s - pad, s - pad], 40, 330, fill=(207, 232, 234, 240), width=8 * SS)
    d.polygon([(s * 0.78, s * 0.20), (s * 0.90, s * 0.34), (s * 0.68, s * 0.36)],
              fill=(207, 232, 234, 240))
    save(img.resize((size, size), Image.LANCZOS), "icon_shuffle.png")

    # Hint: teardrop bulb, design uses border-radius 50% 50% 50% 4px rotated -45deg
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([s * 0.28, s * 0.22, s * 0.72, s * 0.66], fill=(74, 44, 5, 255))
    d.polygon([(s * 0.42, s * 0.62), (s * 0.58, s * 0.62), (s * 0.50, s * 0.82)],
              fill=(74, 44, 5, 255))
    save(img.resize((size, size), Image.LANCZOS), "icon_hint.png")


if __name__ == "__main__":
    generate_ui_panels()
    generate_grid_tiles()
    generate_nodes()
    generate_wheel_backdrop()
    generate_coin()
    generate_stars()
    generate_menu_icons()
    print("Generated design-matched sprites.")
