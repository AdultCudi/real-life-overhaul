"""Lays the shots of a hairstyle plan out as one sheet: python -I hairstyle_sheet.py <shots folder> <out image> [columns] [filter]

Rows by view (play distance first), columns by colour and by game/mod. Play-distance crops are shown at three times
their size (they are 200 px of the game's 1080p frame), close-ups reduced to the same cell.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "_tools", "pylib"))
from PIL import Image, ImageDraw  # noqa: E402

folder, out = sys.argv[1], sys.argv[2]
columns = int(sys.argv[3]) if len(sys.argv) > 3 else 0
only = sys.argv[4] if len(sys.argv) > 4 else ""
CELL = int(os.environ.get("HAIRSTYLE_CELL", "420"))

names = sorted(n[:-4] for n in os.listdir(folder) if n.endswith(".png") and only in n)


def key(name):
    parts = name.split("_")      # p_<colour>_<stage>_<view+yaw>_<label>
    return (parts[0] != "p", parts[0], parts[2] != "city", parts[3], parts[1], parts[4])


names.sort(key=key)
if not columns:
    columns = len({(n.split("_")[1], n.split("_")[4]) for n in names if n.startswith(names[0][0])}) or 4
cells = []
for name in names:
    image = Image.open(os.path.join(folder, name + ".png")).convert("RGB")
    image = image.resize((CELL, CELL), Image.NEAREST if image.width < CELL / 1.5 else Image.LANCZOS)
    cells.append((name, image))
rows = (len(cells) + columns - 1) // columns
sheet = Image.new("RGB", (columns * (CELL + 4), rows * (CELL + 18)), (24, 24, 24))
draw = ImageDraw.Draw(sheet)
for i, (name, image) in enumerate(cells):
    x, y = (i % columns) * (CELL + 4), (i // columns) * (CELL + 18)
    sheet.paste(image, (x, y + 16))
    draw.text((x + 3, y + 2), name, fill=(235, 235, 235))
if out.lower().endswith(".jpg"):
    sheet.save(out, quality=88)
else:
    sheet.save(out)
print("sheet", out, sheet.size, len(cells), "shots")
