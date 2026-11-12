# Credits: the RLO interface's font and icons

## Font

The RLO interface sets its type in one family, a free font under the SIL Open Font License, Version 1.1. Its licence
ships beside it here (`fonts/OFL-Overpass.txt`) and inside the font bundle (`Bundles/reallife_ui`):

- **Overpass**: Copyright 2021 The Overpass Project Authors (https://github.com/RedHatOfficial/Overpass). Used for every
  title, row, button and figure, in its Regular, SemiBold and Bold weights, with its tabular figures swapped in so a column
  of numbers lines up.

The three faces were taken from RedHatOfficial/Overpass at commit c580d28bfab7f39013568e684a65eeb23eff588d (fonts/ttf).
`scripts/fonts.py` pins each file by its SHA-256 and cuts the static faces the bundle holds. The font is not sold on its
own. It is part of the mod, as the licence allows.

## Icons

The interface's icons are Phosphor Icons, under the MIT License: Copyright (c) 2023 Phosphor Icons
(https://phosphoricons.com, https://github.com/phosphor-icons/core). The licence ships beside the font's licence
(`fonts/LICENSE-Phosphor.txt`) and inside the bundle (`Bundles/reallife_ui`).

The SVGs in `icons/` (the bold, duotone and fill weights the interface uses) were taken from phosphor-icons/core at commit
2b75f3ad12b420c9504ef05df8d2564a28f8500e. `scripts/icons.py` pins each file by its SHA-256 and turns its outlines into the
mod's own code (`Scripts/Ui/UiIconPaths.Data.cs`), which the mod fills into textures at runtime and tints by category. No
icon is changed in shape. They are part of the mod, as the licence allows.
