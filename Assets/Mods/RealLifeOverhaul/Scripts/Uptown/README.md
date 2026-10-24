# `Scripts/Uptown`

Uptown: a district of the mod's own north of Manhattan, past 1st Avenue. Its ground is laid at every load and never saved: the whole band of three rows of eighteen blocks, Manhattan's streets going on north through 1st Avenue's own junctions and 1st Avenue going on west and east, nineteen streets and three avenues meeting at junctions with lights, over walking ground grown into the city's own meshes from Manhattan's, with the game's own road modules copied along them, parking, people and three bus termini. Its lots are where the district's buildings stand.

## Read first

- `LayoutRules.cs`, `LayoutPlans.cs`, `LayoutStreets.cs`: the band's streets and lots and what the load lays of it (game-free, checked by NavCheck's `UptownCheck.cs`).
- `UptownLots.cs`: the lots for the features that build on them: `UptownLots`, `Lot`, `LotClass`, `GroundRect`, `UptownExit`.
- `GroundDirector.cs`: the load, step by step; `GroundGame.cs`, `GroundGame.Art.cs` and `GroundGame.Fog.cs` (the city map's fog, cut away over the band from `LayoutFog.cs`, data made offline from the game's own mesh): the only files that touch the game's scene for the ground.
- `GroundSession.cs`: whether the district may stand at this load (its option and the Going Public session).
- `BuildingsRules.cs`, then `LifeDirector.cs`: the district of the game on those lots: which lots are built from the first day and which stay open for the construction game, the offices leased to the city's rival builders, and the load step by step (`LifeGame*.cs` the only files of the district's life that touch the game).
- `SpecialsRules.cs`, then `LifeDirector.Specials.cs` and `LifeGame.Specials.cs`: the district's special places, each a copy of one of the game's special services (a bank's branch, the agencies, the stores, a supermarket, a clinic, a dealership, an importer) on a fixed lot, through the made buildings' engine.

## Documentation

How it works, the lots for other features, its rules and its tests: [Uptown](../../../../../docs/features/uptown.md).
The ground it is laid with: [New ground](../../../../../docs/features/new-ground.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
