# `Scripts/Construction`

The construction game: a construction company of the player's (a business type of the mod's, run from the Construction app), its land and projects from the plans to the keys, planning permission at City Hall, sites that go up in the city's construction programme at the crews' pace, materials, plant, crews and money, tenders against the city's eight builders, and the city's developers building on the district's lots.

## Read first

- `BuildCatalogue.cs`: every number and every name the texts are made of (game-free).
- The rules, game-free and checked offline (`EconomyCheck/BuildersChecks.cs`): `LandRules.cs`, `PlansRules.cs`, `PlanningRules.cs`, `SiteRules.cs`, `MoneyRules.cs`, `TenderRules.cs`, `FirmRules.cs`, `DevelopmentRules.cs`, `CommissionRules.cs` (a builder of the city's hired by the player), `BuildRandom.cs`.
- `BuildState.cs` and `BuildStore.cs`: the save's one line.
- `ConstructionDirector.cs`: the state and its timing; its parts are named for what they do (`.Projects`, `.Sites`, `.Day`, `.Market`, `.Actions`, `.App`, `.Asks`, `.Tests`, and `.Hire` and `.HireTests` for a builder the player hires, with or without a company).
- `ConstructionBridges.cs`: everything it asks of the rest of the mod, each with a default; `Wiring/Wiring.ConstructionLinks.cs` fills them in.
- `ConstructionGame.cs`: the one file that reads or changes the game.

## Documentation

What players see, the wiring with the city's programme, the copy engine, the new district and the building trades, Going Public, the save, the tests and the checks: [Construction](../../../../../docs/features/construction.md#the-construction-game).

Maintained by the Real Life Overhaul maintainer.
