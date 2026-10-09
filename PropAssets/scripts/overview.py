"""Stacks preview sheets into one image, for looking over several props at once: python overview.py out.png a.png b.png ..."""
import os
import sys

sys.path.append(os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "_tools", "pylib"))
from PIL import Image  # noqa: E402

out, paths = sys.argv[1], sys.argv[2:]
tiles = [Image.open(p).convert("RGB") for p in paths]
tiles = [t.resize((1200, round(t.height * 1200 / t.width)), Image.LANCZOS) for t in tiles]
joined = Image.new("RGB", (1200, sum(t.height for t in tiles)))
y = 0
for tile in tiles:
    joined.paste(tile, (0, y))
    y += tile.height
joined.save(out)
