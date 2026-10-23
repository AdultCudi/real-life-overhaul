"""The finishes of the build module (props_build.py), the second bundle's: see atlas.py, "the later rounds", and README,
"The second bundle". Its atlas holds the shared swatches and these only, so nothing of the first bundle's lettering is
painted again.
"""


def register(a):
    swatches = {
        "build_cladding": ((0.82, 0.83, 0.80), 0.0, 0.30, 0.8),     # a site cabin's painted steel sides, dirtier at the foot
        "build_rust": ((0.36, 0.20, 0.11), 0.6, 0.30, 0.4),         # steel bars left out in the weather
        "build_earth": ((0.31, 0.23, 0.15), 0.0, 0.10, 0.5),        # dug ground, a little darker at the foot of a bank
        "build_block": ((0.55, 0.55, 0.53), 0.0, 0.15, 0.4),        # concrete blocks
        "build_portable": ((0.10, 0.30, 0.58), 0.0, 0.45, 0.6),     # a portable toilet's moulded blue
    }
    return swatches, {}
