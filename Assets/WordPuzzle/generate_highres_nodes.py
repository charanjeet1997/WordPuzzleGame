import os
from PIL import Image, ImageDraw

def create_directory(path):
    if not os.path.exists(path):
        os.makedirs(path)

output_dir = r"Assets/WordPuzzle/Resources/Sprites"
create_directory(output_dir)

# Letter Node Normal (256x256 high-res dark blue node with cyan glowing border)
def generate_node_normal():
    size = 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.ellipse([8, 8, size - 8, size - 8], fill=(24, 34, 56, 255), outline=(56, 189, 248, 255), width=12)
    draw.ellipse([24, 24, size - 24, size - 24], outline=(147, 197, 253, 140), width=6)
    img.save(os.path.join(output_dir, "letter_node_normal.png"))

# Letter Node Selected (256x256 high-res gold glowing node)
def generate_node_selected():
    size = 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.ellipse([8, 8, size - 8, size - 8], fill=(234, 179, 8, 255), outline=(255, 255, 255, 255), width=14)
    draw.ellipse([24, 24, size - 24, size - 24], outline=(254, 240, 138, 200), width=8)
    img.save(os.path.join(output_dir, "letter_node_selected.png"))

if __name__ == "__main__":
    generate_node_normal()
    generate_node_selected()
    print("High-res node sprites generated.")
