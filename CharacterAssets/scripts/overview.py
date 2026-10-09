"""Stacks preview sheets into one overview image: python overview.py <output name> <sheet name> ..."""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.append(os.path.join(os.path.dirname(HERE), "_tools", "pylib"))
from PIL import Image  # noqa: E402

previews = os.path.join(os.path.dirname(HERE), "previews")
width = 1000
rows = []
for name in sys.argv[2:]:
    img = Image.open(os.path.join(previews, name + ".png")).convert("RGB")
    rows.append(img.resize((width, round(img.height * width / img.width)), Image.LANCZOS))
sheet = Image.new("RGB", (width, sum(r.height for r in rows)))
y = 0
for row in rows:
    sheet.paste(row, (0, y))
    y += row.height
sheet.save(os.path.join(previews, sys.argv[1] + ".png"))
