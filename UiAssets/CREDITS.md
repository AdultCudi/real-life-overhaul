# Credits: the RLO interface's fonts

The RLO interface sets its type in two families. Both are free fonts under the SIL Open Font License, Version 1.1.
Their licences ship beside them here (`fonts/OFL-*.txt`) and inside the font bundle (`Bundles/reallife_ui`):

- **Source Serif 4**: Copyright 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source'.
  Designed by Frank Grießhammer. Used for heads and messages, at its display and text optical sizes.
- **Libre Franklin**: Copyright 2015 The Libre Franklin Project Authors (https://github.com/impallari/Libre-Franklin).
  Designed by Pablo Impallari, Rodrigo Fuenzalida and Nhung Nguyen, after Morris Fuller Benton's Franklin Gothic.
  Used for labels, buttons, prompts and figures.

Both were taken from google/fonts at commit 6c1dbe27dcf7d817d4b62cec033ba77fa8e2a5ec. `scripts/fonts.py` pins each file by
its SHA-256 and cuts the static faces the bundle holds. The fonts are not sold on their own. They are part of the mod,
as the licence allows.
