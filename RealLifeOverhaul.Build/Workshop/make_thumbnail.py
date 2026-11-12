"""Makes the Workshop thumbnail from one of the author's own screenshots.

  python -I RealLifeOverhaul.Build/Workshop/make_thumbnail.py

Source: thumbnail_source.jpg beside this file, the square of a night drive down 2nd Avenue that holds none of the
interface. The top and the foot are darkened so that the title and the line under it read; nothing else is drawn
over the scene. The lettering is Bahnschrift, which comes with Windows.
Result: Assets/Mods/RealLifeOverhaul/thumbnail.jpg, 1080x1080. The game's uploader takes the first .png, .jpg or
.jpeg in the root of the mod folder that is no larger than its limit (1 MB), whatever its name, so this must stay
the only picture there.
"""
import os
from PIL import Image, ImageDraw, ImageEnhance, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', '..', 'Assets', 'Mods', 'RealLifeOverhaul', 'thumbnail.jpg')
SIDE = 1080
INK, AMBER, GREY, NIGHT = (244, 239, 228), (242, 169, 59), (190, 200, 214), (10, 16, 28)

scene = Image.open(os.path.join(HERE, 'thumbnail_source.jpg')).convert('RGB').resize((SIDE, SIDE), Image.LANCZOS)
scene = ImageEnhance.Color(ImageEnhance.Contrast(scene).enhance(1.08)).enhance(1.12)
shade = Image.new('L', (1, SIDE))
for y in range(SIDE):
    top = max(0.0, 1 - y / 330.0) ** 1.4 * 235
    foot = max(0.0, (y - (SIDE - 300)) / 300.0) ** 1.3 * 235
    shade.putpixel((0, y), int(min(255, max(top, foot))))
picture = Image.composite(Image.new('RGB', (SIDE, SIDE), NIGHT), scene, shade.resize((SIDE, SIDE)))
draw = ImageDraw.Draw(picture)


def font(size, weight):
    face = ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf', size)
    face.set_variation_by_axes([weight, 100])
    return face


def centred(text, y, face, fill, gap):
    x = (SIDE - (sum(draw.textlength(c, font=face) + gap for c in text) - gap)) / 2
    for c in text:
        draw.text((x, y), c, font=face, fill=fill)
        x += draw.textlength(c, font=face) + gap


centred('REAL LIFE', 44, font(132, 700), INK, 6)
centred('OVERHAUL', 168, font(132, 700), AMBER, 6)
draw.rectangle([(SIDE - 120) / 2, 318, (SIDE + 120) / 2, 323], fill=INK)
centred('Everyday life for Big Ambitions', SIDE - 150, font(46, 500), INK, 1)
centred('careers  ·  family  ·  police  ·  markets  ·  a city that lives', SIDE - 84, font(30, 400), GREY, 1)
picture.save(OUT, quality=90)
print(os.path.getsize(OUT), 'bytes')
