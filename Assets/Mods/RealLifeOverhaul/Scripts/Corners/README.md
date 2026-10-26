# `Scripts/Corners`

The corner buildings of build mode: seven buildings made for a block's corner (a bodega with flats, a diner, a bank, an art deco tower, a glass tower, a warehouse loft and a pre-war block of flats), each a model of the second props bundle stood over the copy engine's building of its type, facing both streets, with live business signs.

## Read first

- `CornerRules.cs`: the models' facts, the four turns to a block's two streets, the fit, the ground of the building beneath, the door, the solid boxes, what each sign says and its colour; game-free (checked by EconomyCheck's `CornerChecks.cs`).
- `CornerLook.cs`: the only file that stands a model over a building of the game's, moves its doors and its own door sign to the entrance, and hides its look for good; registered with Uptown's `DistrictBuildings` at game start. `CornerLook.Checks.cs`: the tests' check.
- `CornerSigns.cs`: the live signs in the game's font, written on the game's registration change event.
- The construction game reaches it through its bridges (`Wiring/Wiring.CornerLinks.cs`); the corner site rule is Construction's `CellRules.Corner`.

## Documentation

How it works, its rules, its tests: [Construction, corner buildings](../../../../../docs/features/construction.md#corner-buildings).
The models: [PropAssets/README.md](../../../../../PropAssets/README.md), "The corner buildings".

Maintained by the Real Life Overhaul maintainer.
