# Character quality

## In a sentence

Character quality: detailed player in the city, a scalp under its hair, character-screen lighting, and the mod's garments in the game's own clothes screens.

## Where the code is

- Folder: [`Scripts/Character`](../../Assets/Mods/RealLifeOverhaul/Scripts/Character/). The looks themselves are built by the asset pipeline in [`CharacterAssets`](../../CharacterAssets/README.md).
- Read first: `CharacterQuality.cs` (the entry points), `CharacterCatalogue.cs` (the looks on offer) and `ModLooks.cs`.

| File | What it is |
| --- | --- |
| `CharacterQuality` | entry points (city, character creation); hair, looks and textures on every change of look, options |
| `CharacterCatalogue` | the new looks on offer (one row each); the picks, which belong to one character (the one being created, or the loaded save's: never PlayerPrefs) and give way, per kind, to a piece picked in the game's own screens |
| `GridLooks` | the mod's garments as entries of the game's own grids (Torso, Legs, Feet) with their colourways in its colour row: the creator's starter garments, the clothes screen's wardrobe and shop |
| `ModAssets` | the asset bundle: read once in the background, bind poses taken from the game's head mesh |
| `ModLooks` | puts the scalp underlay (and any picked look) on the characters that show the player; undoes itself |
| `PlayerDouble` | the player drawn with the game's detailed character set, posed from the player's skeleton |
| `HairLook` | puts the hair finish on the hair and beard of the characters that show the player; undoes itself |
| `HairFinish` | what a finished hair layer is (depth prepass, strand maps, gloss from the colour); game-free, the HDRP preview compiles it too |
| `CharacterLighting` | fill and rim light for character creation and the shop preview; undoes itself |
| `CharacterTools` | telling the two character sets apart, copying one inertly, sharper textures |

## How it talks to other features

- Leisure sells the mod's garments at the clothes rack ([Leisure](leisure.md#clothes)); the character creator offers the Everyday tier.
- Characters' looks for the street's people are built by `People/Looks`; when and how often is in [Performance](../reference/performance.md#when-characters-looks-are-built-peoplelooks).

## Rules for looks

- A look is accepted in the HDRP preview (`CharacterAssets/preview.ps1`), next to the game's own, at play distance and close up, never in Blender; code that decides how a look is drawn is kept game-free and listed in `CharacterAssets/PreviewProject/shared_sources.txt`, so the preview runs it too.
- New looks are meshes and textures only, in the one asset bundle, loaded once per session in the background.
  Materials are always copies of materials the game is already drawing with (never change shader keywords on them), only for the characters that show the player, and with a row in `Character/CharacterCatalogue`.

  The player's picks are kept by id in the save's modData, per save; a pick never outranks a piece the player then picks in one of the game's own screens (`CharacterCatalogue.Observe`).
