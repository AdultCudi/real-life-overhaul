# `Scripts/Population`

The city's population, the living economy overhaul's first part: real households and people in the city's buildings, their life, jobs and spending, and the customers every business takes from them. Under the master option Living population.

## Read first

- `PopulationCatalogue.cs`: every figure and where it comes from (game-free).
- `MeasureRules.cs`, `GenerateRules.cs`, `LifeRules.cs`, `LabourRules.cs`, `SpendRules.cs`, `PoolRules.cs`, `HousingRules.cs`, `RegionRules.cs`, `GameRules.cs`: the rules (game-free, checked by EconomyCheck's `PopulationChecks.cs` and `PopulationGamesChecks.cs`).
- `PopulationSim.cs`: the work in slices.
- `PopulationDirector.cs`: the timer, the day, the save and Going Public; `PopulationGame.cs` and `PopulationGame.Calculators.cs`: the only files that touch the game.
- `CityPopulation.cs` and `PopulationAttention.cs`: what other features read and feed.
- `Demand/`: what the people bring the mod's own businesses (the own bank, hotels, letting, the agency, the builder, the showroom, online shops, the Marketplace, the academy, broadband, the council, crime and the labour market): `PopDemandCatalogue.cs` (the recipes and figures), `PopDemandJob.cs` (the day's measure in slices), `PopDemandRules.cs` (bands, satisfaction, crime, leans, the anchor, the factors), `CityDemand.cs` (what the businesses' bridges read, wired in `Wiring/Wiring.DemandLinks.cs`), `PopDemandStore.cs`, `PopDemandShare.cs`, `PopDemandViews.cs`, `PopDemandGuide.cs`; the timing in `PopulationDirector.Demand.cs`.

## Documentation

How it works, its rules and its tests: [The city's population](../../../../../docs/features/population.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
