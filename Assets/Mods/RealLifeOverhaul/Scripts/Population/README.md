# `Scripts/Population`

The city's population, the living economy overhaul's first part: real households and people in the city's buildings, their life, jobs and spending, and the customers every business takes from them. Under the master option Living population.

## Read first

- `PopulationCatalogue.cs`: every figure and where it comes from (game-free).
- `MeasureRules.cs`, `GenerateRules.cs`, `LifeRules.cs`, `LabourRules.cs`, `SpendRules.cs`, `PoolRules.cs`, `HousingRules.cs`, `RegionRules.cs`: the rules (game-free, checked by EconomyCheck's `PopulationChecks.cs`).
- `PopulationSim.cs`: the work in slices.
- `PopulationDirector.cs`: the timer, the day, the save and Going Public; `PopulationGame.cs` and `PopulationGame.Calculators.cs`: the only files that touch the game.
- `CityPopulation.cs` and `PopulationAttention.cs`: what other features read and feed.

## Documentation

How it works, its rules and its tests: [The city's population](../../../../../docs/features/population.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
