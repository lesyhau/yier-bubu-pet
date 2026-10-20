"""Render assets/logo/*.svg into the app icon and logo PNGs.

    pip install cairosvg pillow
    python tools/make_icon.py

Sizes up to 32 px use logo-small.svg (simplified, no background tile) so the tray icon stays readable.
"""
import io
import os

import cairosvg
from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
LOGO = os.path.join(ROOT, "assets", "logo")
RES = os.path.join(ROOT, "src", "YierBubuPet", "Resources")


def render(size):
    svg = "logo-small.svg" if size <= 32 else "logo.svg"
    png = cairosvg.svg2png(url=os.path.join(LOGO, svg), output_width=size, output_height=size)
    return Image.open(io.BytesIO(png)).convert("RGBA")


if __name__ == "__main__":
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    images = [render(s) for s in sizes]
    images[-1].save(os.path.join(RES, "icon.ico"), format="ICO",
                    sizes=[(s, s) for s in sizes], append_images=images[:-1])
    render(256).save(os.path.join(RES, "logo.png"))
    render(512).save(os.path.join(LOGO, "logo.png"))
    print("wrote icon.ico, Resources/logo.png, assets/logo/logo.png")
