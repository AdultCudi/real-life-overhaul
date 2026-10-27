# `Scripts/Uptown`

Uptown and Cedar Heights: the two districts of the mod's own north of Manhattan, past 1st Avenue (this folder's name and the words "the band" are for both). Its ground is laid at every load and never saved: the whole band of three rows of eighteen blocks, Manhattan's streets going on north through 1st Avenue's own junctions and 1st Avenue going on west and east, nineteen streets and three avenues meeting at junctions with lights, over walking ground grown into the city's own meshes from Manhattan's, with the game's own road modules copied along them, parking, people and three bus termini. Its lots are where the district's buildings stand.

## Read first

- `DistrictSplit.cs`, `DistrictCensus.cs`, `SplitRules.cs`, then `LifeDirector.Split.cs` and `LifeGame.Split.cs`: the band is two districts of the game, Uptown (east) and Cedar Heights (west), cut along 5th Street (docs/features/uptown.md, "Two districts"): which district a lot, a block or a point is in, each district's figures, the quota of large stores the plan keeps (`LayoutRules.Rebalance`, `Pin`), the census against the game's old districts (`LifeDirector.SplitCensus.cs`), and what the first load after the split does with a save made when the band was one district (game-free, checked by EconomyCheck's `UptownSplitChecks.cs`).
- `LayoutRules.cs`, `LayoutPlans.cs`, `LayoutStreets.cs`: the band's streets and lots and what the load lays of it (game-free, checked by NavCheck's `UptownCheck.cs`).
- `UptownLots.cs`: the lots for the features that build on them: `UptownLots`, `Lot`, `LotClass`, `GroundRect`, `UptownExit`.
- `GroundDirector.cs`: the load, step by step; `GroundGame.cs`, `GroundGame.Art.cs`, `GroundGame.Surfaces.cs` (the surfaces standing now, for the check of `SurfaceRules.cs`: no two road or ground surfaces at one height over one place) `GroundGame.Fog.cs` (the city map's fog, cut away over the band from `LayoutFog.cs`, data made offline from the game's own mesh) and `GroundGame.Edges.cs` (the districts' edges: a bank of fog behind each stretch of wall and a barricade across each street end, from `EdgeRules.cs`, with `GroundDirector.Edges.cs`): the only files that touch the game's scene for the ground.
- `GroundSession.cs`: whether the district may stand at this load (its option and the Going Public session).
- `BuildingsRules.cs`, then `LifeDirector.cs`: the district of the game on those lots: which lots are built from the first day and which stay open for the construction game, the offices leased to the city's rival builders, and the load step by step (`LifeGame*.cs` the only files of the district's life that touch the game).
- `SpecialsRules.cs`, then `LifeDirector.Specials.cs` and `LifeGame.Specials.cs`: the district's special places, each a copy of one of the game's special services (a bank's branch, the agencies, the stores, a supermarket, a clinic, a dealership, an importer) on a fixed lot, through the made buildings' engine.
- `BigPlacesRules.cs`, then `LifeDirector.BigPlaces.cs`: its wholesale store and gas station, copies of the game's own whole, the lots they hold and where each stands (game-free, checked by EconomyCheck's `UptownBigChecks.cs`); their tests in `LifeDirector.BigTests.cs` and `LifeGame.BigPlaces.cs`.
- `StreetsRules.cs`, then `GroundDirector.Streets.cs` and `StreetsGame.cs`: its streets as the buses and pavements use them: the two bus routes to every block and their reach, the street furniture at Manhattan's spacing (game-free, checked by NavCheck's `UptownStreetsCheck.cs`), laid with the art; its three tests; `StreetsGuide.cs`, `StreetsText.cs`.
- `ParksRules.cs`, then `GroundDirector.Parks.cs` and `ParksGame.cs`: its two parks, laid from the game's own trees, benches, lamps, fountain and statue with the street furniture (game-free, checked by NavCheck's `UptownParksCheck.cs`); `GamePieces.cs` and `PieceRules.cs`, the one finder for the pieces of the game's scene the campus, the street furniture and the parks copy.
- `CompleteRules.cs`, then `LifeDirector.Complete.cs` and `LifeGame.Complete.cs`: every finished building checked as a whole building of the game's at its stand and at every load, a city's building with no owner given one; `DemandRules.cs`, then `LifeDirector.Market.cs` and `LifeGame.Market.cs`: the district's demand for each use and its market level in Uptown's record, and a packed block's traffic (both game-free, `EconomyCheck/BuildModeChecks.cs`).

## Documentation

How it works, the lots for other features, its rules and its tests: [Uptown](../../../../../docs/features/uptown.md).
The ground it is laid with: [New ground](../../../../../docs/features/new-ground.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
