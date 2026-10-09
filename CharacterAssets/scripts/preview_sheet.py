"""Lays preview shots out as one labelled sheet.

    python -I preview_sheet.py <pylib> <out.png> <columns> <scale> label=file [label=file ...]

A file may be given as file@x,y,w,h to cut a region out first (used for the player's screenshots).
"""
import sys

sys.path.insert(0, sys.argv[1])
from PIL import Image, ImageDraw  # noqa: E402

out, columns, scale = sys.argv[2], int(sys.argv[3]), float(sys.argv[4])
cells = []
for item in sys.argv[5:]:
    label, _, path = item.partition("=")
    path, _, region = path.partition("@")
    image = Image.open(path).convert("RGB")
    if region:
        x, y, w, h = (int(v) for v in region.split(","))
        image = image.crop((x, y, x + w, y + h))
    image = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))),
                         Image.LANCZOS if scale < 1 else Image.NEAREST)
    cells.append((label, image))

width = max(image.width for _, image in cells)
height = max(image.height for _, image in cells) + 16
rows = (len(cells) + columns - 1) // columns
sheet = Image.new("RGB", (columns * width + (columns - 1) * 4, rows * height + (rows - 1) * 4), (24, 24, 24))
draw = ImageDraw.Draw(sheet)
for i, (label, image) in enumerate(cells):
    x, y = (i % columns) * (width + 4), (i // columns) * (height + 4)
    sheet.paste(image, (x, y + 16))
    draw.text((x + 3, y + 2), label, fill=(235, 235, 235))
sheet.save(out)
print("sheet", out, sheet.size)
