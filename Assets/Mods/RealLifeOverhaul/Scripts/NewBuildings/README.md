# `Scripts/NewBuildings`

Buildings of the mod's own, copied at every load from the city's own building designs: a building a feature declares stands at an address on a street of the mod's own, with the game's own door, interior and map look, and is never saved; decorative copies give another feature's place a look of the city's own, a construction site's finished building first of all.

## Read first

- `NewBuildingsRules.cs`: a design's name, the pick, where a design stands on its lot, the ground's sums, the lot taken, what refuses a declaration (game-free).
- `MadeBuildings.cs`: declaring a building of the mod's own, and finding it after the load.
- `BuildingCopies.cs`: decorative copies for other features, and a building's own look hidden and shown.
- `NewBuildingsDirector.cs`: the load step by step, the places named to the district save, the save guard's timing; `NewBuildingsDirector.Copies.cs` the copies, `NewBuildingsDirector.Tests.cs` the six test entries.
- `GateRules.cs` and `WarehouseExit.cs`: which garage doors a copy keeps, and a car put out of one of the mod's warehouses moved clear of the door's entrance trigger.
- `MapRules.cs`: the city map's filters and a building's registration with them, and the city's list grown and shortened once for each address (game-free).
- `MadeGame.cs`, `MadeGame.City.cs`, `MadeGame.Map.cs`, `MadeGame.Save.cs` and `CopyGame.cs`, `CopyGame.Ground.cs`, `WarehouseExit.cs`: the only files that touch the game.

## Documentation

What it does and how other features use it: [New buildings](../../../../../docs/features/new-buildings.md). How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
