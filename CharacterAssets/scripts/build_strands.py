"""Paints the two strand maps every finished hair layer shares (see Character/HairFinish.cs).

    python -I build_strands.py <build folder> <pylib folder>

    hair_strand_mask.png    R = how glossy each strand is: fine streaks running down the texture, tileable.
                            Goes into the hair shader's smoothness mask, which also scales the highlight.
    hair_strand_normal.png  tangent-space normals of the same kind of streaks, so light breaks across strands.

Both are generated from noise alone (nothing of the game's goes in). The game's hair textures all run their
strands down the V axis, which is the only thing these maps assume.
"""
import json
import os
import sys

out_dir, pylib = sys.argv[1], sys.argv[2]
sys.path.insert(0, pylib)

import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

SIZE = 512
rng = np.random.default_rng(7)


def periodic_noise(length, octaves):
    """1-D noise that wraps: a sum of random-phase sines, octave by octave."""
    x = np.arange(length) / length
    total = np.zeros(length)
    for frequency, amplitude in octaves:
        for _ in range(3):
            k = max(1, int(round(frequency * rng.uniform(0.7, 1.3))))
            total += amplitude * np.sin(2 * np.pi * (k * x + rng.uniform()))
    return total


def streaks():
    """Height of a bundle of strands: each column its own strand, drifting sideways slowly down the texture."""
    across = periodic_noise(SIZE, ((6, 0.5), (19, 0.6), (47, 0.8), (101, 0.8), (190, 0.5)))
    # A slow sideways drift so the streaks are not ruler-straight, and a slow change along each strand.
    drift = periodic_noise(SIZE, ((1, 5.0), (3, 2.0)))
    along = periodic_noise(SIZE, ((2, 0.25), (5, 0.15)))
    height = np.zeros((SIZE, SIZE))
    columns = np.arange(SIZE)
    for row in range(SIZE):
        shifted = (columns + drift[row]) % SIZE
        low = np.floor(shifted).astype(int)
        mix = shifted - low
        height[row] = across[low % SIZE] * (1 - mix) + across[(low + 1) % SIZE] * mix + along[row] * 0.5
    height -= height.min()
    return height / height.max()


textures = os.path.join(out_dir, "textures")
os.makedirs(textures, exist_ok=True)
height = streaks()

# Gloss: most strands middling, a few bright ones, a few dull ones.
mask = np.clip(0.12 + 0.88 * np.power(height, 1.6), 0, 1)
Image.fromarray((np.dstack([mask, mask, mask]) * 255).astype(np.uint8)).save(os.path.join(textures, "hair_strand_mask.png"))

# Normals from the height's slope across the strands (the slope along them is negligible by construction).
slope = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 3.0
normal = np.dstack([-slope, np.zeros_like(slope), np.ones_like(slope)])
normal /= np.linalg.norm(normal, axis=2, keepdims=True)
Image.fromarray(((normal * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(textures, "hair_strand_normal.png"))
with open(os.path.join(out_dir, "catalogue_strands.json"), "w") as f:
    json.dump({"items": [], "textures": [
        {"name": "hair_strand_mask", "file": "textures/hair_strand_mask.png", "type": "mask", "maxSize": SIZE},
        {"name": "hair_strand_normal", "file": "textures/hair_strand_normal.png", "type": "normal", "maxSize": SIZE}]}, f, indent=1)
print("strand maps", SIZE, "px, mean gloss %.2f" % mask.mean())
