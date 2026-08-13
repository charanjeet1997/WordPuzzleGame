import os
from PIL import Image, ImageDraw

OUTPUT_DIR = r"d:\Projects\Unity\GameProj\WordsOfWonders\Assets\WordPuzzle\Sprites"
os.makedirs(OUTPUT_DIR, exist_ok=True)

def create_meta_file(file_path):
    meta_content = f"""fileFormatVersion: 2
guid: {os.urandom(16).hex()}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipmapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    convertToNormalMap: 0
    defaultNormalMap: 0
    grayScaleToAlpha: 0
    generateCubemap: 6
    cubemapConvolution: 0
    seamlessCubemap: 0
    textureFormat: 1
    maxTextureSize: 2048
    textureSettings:
      serializedVersion: 2
      filterMode: 1
      aniso: 1
      mipBias: 0
      wrapU: 1
      wrapV: 1
      wrapW: 1
    nPOTScale: 0
    lightmap: 0
    compressionQuality: 50
    spriteMode: 1
    spriteExtrude: 1
    spriteMeshType: 1
    alignment: 0
    spritePivot: {{x: 0.5, y: 0.5}}
    spritePixelsToUnits: 100
    spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
    spriteGenerateFallbackPhysicsShape: 1
    alphaUsage: 1
    alphaIsTransparency: 1
    spriteTessellationDetail: -1
    textureType: 8
    textureShape: 1
"""
    with open(file_path + ".meta", "w") as f:
        f.write(meta_content)

def save_image(img, name):
    p1 = os.path.join(OUTPUT_DIR, name)
    img.save(p1, "PNG")
    create_meta_file(p1)
    print(f"Generated asset in Assets/WordPuzzle/Sprites: {name}")

def gen_wheel_bg():
    size = 512
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    center = size // 2
    radius = size // 2 - 20
    for r in range(radius + 15, radius, -1):
        alpha = int(80 * (1 - (r - radius) / 15))
        draw.ellipse([center - r, center - r, center + r, center + r], fill=(40, 180, 255, alpha))
    draw.ellipse([center - radius, center - radius, center + radius, center + radius], fill=(15, 25, 45, 230), outline=(60, 200, 255, 255), width=8)
    inner_r = radius - 35
    draw.ellipse([center - inner_r, center - inner_r, center + inner_r, center + inner_r], outline=(255, 255, 255, 40), width=3)
    save_image(img, "letter_wheel_bg.png")

def gen_node_normal():
    size = 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    center = size // 2
    r = size // 2 - 12
    draw.ellipse([center - r, center - r, center + r, center + r], fill=(24, 38, 68, 240), outline=(75, 140, 220, 255), width=6)
    highlight_r = r - 10
    draw.arc([center - highlight_r, center - highlight_r, center + highlight_r, center + highlight_r], start=200, end=340, fill=(255, 255, 255, 120), width=5)
    save_image(img, "letter_node_normal.png")

def gen_node_selected():
    size = 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    center = size // 2
    r = size // 2 - 12
    for gr in range(r + 10, r, -1):
        alpha = int(100 * (1 - (gr - r) / 10))
        draw.ellipse([center - gr, center - gr, center + gr, center + gr], fill=(50, 220, 255, alpha))
    draw.ellipse([center - r, center - r, center + r, center + r], fill=(40, 160, 240, 255), outline=(255, 255, 255, 255), width=7)
    save_image(img, "letter_node_selected.png")

def gen_tile_hidden():
    size = 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    margin = 10
    corner = 30
    draw.rounded_rectangle([margin, margin, size - margin, size - margin], radius=corner, fill=(30, 42, 66, 230), outline=(90, 130, 180, 255), width=5)
    save_image(img, "grid_tile_hidden.png")

def gen_tile_revealed():
    size = 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    margin = 10
    corner = 30
    draw.rounded_rectangle([margin, margin, size - margin, size - margin], radius=corner, fill=(245, 248, 255, 255), outline=(255, 215, 0, 255), width=7)
    save_image(img, "grid_tile_revealed.png")

def gen_coin_icon():
    size = 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    center = size // 2
    r = size // 2 - 15
    draw.ellipse([center - r, center - r, center + r, center + r], fill=(255, 200, 0, 255), outline=(230, 140, 0, 255), width=8)
    inner_r = r - 25
    draw.ellipse([center - inner_r, center - inner_r, center + inner_r, center + inner_r], outline=(255, 240, 150, 255), width=5)
    draw.polygon([
        (center, center - inner_r + 10),
        (center + 12, center - 6),
        (center + 28, center - 6),
        (center + 15, center + 8),
        (center + 20, center + 25),
        (center, center + 14),
        (center - 20, center + 25),
        (center - 15, center + 8),
        (center - 28, center - 6),
        (center - 12, center - 6)
    ], fill=(255, 245, 180, 255))
    save_image(img, "icon_coin.png")

def gen_hint_icon():
    size = 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    center = size // 2
    draw.ellipse([center - 60, center - 80, center + 60, center + 40], fill=(255, 230, 80, 255), outline=(255, 255, 255, 255), width=6)
    draw.rectangle([center - 30, center + 30, center + 30, center + 75], fill=(180, 190, 200, 255), outline=(100, 110, 120, 255), width=5)
    draw.line([(center - 25, center + 90), (center + 25, center + 90)], fill=(255, 220, 0, 255), width=8)
    save_image(img, "icon_hint.png")

def gen_shuffle_icon():
    size = 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    center = size // 2
    r = 75
    draw.arc([center - r, center - r, center + r, center + r], start=30, end=330, fill=(255, 255, 255, 255), width=14)
    draw.polygon([(center + r - 10, center + 10), (center + r + 25, center + 25), (center + r + 5, center - 25)], fill=(255, 255, 255, 255))
    save_image(img, "icon_shuffle.png")

def gen_button_play():
    w, h = 512, 160
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.rounded_rectangle([10, 10, w - 10, h - 10], radius=40, fill=(40, 200, 100, 255), outline=(255, 255, 255, 255), width=6)
    save_image(img, "button_play.png")

def gen_theme_meadow():
    w, h = 1080, 1920
    img = Image.new("RGBA", (w, h), (20, 30, 50, 255))
    draw = ImageDraw.Draw(img)
    for y in range(h):
        r = int(15 + (40 - 15) * (y / h))
        g = int(30 + (90 - 30) * (y / h))
        b = int(70 + (140 - 70) * (y / h))
        draw.line([(0, y), (w, y)], fill=(r, g, b, 255))
    draw.ellipse([-200, 1200, 1300, 2200], fill=(25, 75, 55, 255))
    draw.ellipse([-100, 1350, 900, 2100], fill=(35, 110, 75, 255))
    save_image(img, "theme_green_meadow.png")

def gen_theme_starlight():
    w, h = 1080, 1920
    img = Image.new("RGBA", (w, h), (10, 15, 30, 255))
    draw = ImageDraw.Draw(img)
    import random
    random.seed(42)
    for _ in range(300):
        sx = random.randint(0, w)
        sy = random.randint(0, h // 2 + 300)
        sr = random.randint(1, 4)
        alpha = random.randint(150, 255)
        draw.ellipse([sx - sr, sy - sr, sx + sr, sy + sr], fill=(255, 255, 255, alpha))
    draw.polygon([(0, h), (0, 1400), (300, 1100), (600, 1350), (900, 1050), (w, 1300), (w, h)], fill=(18, 25, 45, 255))
    save_image(img, "theme_starlight_peak.png")

if __name__ == "__main__":
    gen_wheel_bg()
    gen_node_normal()
    gen_node_selected()
    gen_tile_hidden()
    gen_tile_revealed()
    gen_coin_icon()
    gen_hint_icon()
    gen_shuffle_icon()
    gen_button_play()
    gen_theme_meadow()
    gen_theme_starlight()
    print("Texture assets generated inside Assets/WordPuzzle/Sprites!")
