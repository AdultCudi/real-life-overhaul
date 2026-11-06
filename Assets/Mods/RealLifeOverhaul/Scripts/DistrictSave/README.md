# `Scripts/DistrictSave`

The records of the mod's own places (its district, its streets, the buildings it makes) kept in the mod's part of the save: taken out of the game's lists at every save and put back right after, and put back at every load, so that a save loads without the mod at any time.

## Read first

- `DistrictPlaces.cs`: how a feature names the places it makes (game-free).
- `DistrictSaveDirector.cs`: the save's step, the put back, the two load passes.
- `DistrictSaveGame.cs`: every field of the save that can name a place, by its path (`.Records`, `.Elements`, `.StandIns`, `.Restore`).

## Documentation

How it works, its rules and its tests: [The district save](../../../../../docs/features/district-save.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
