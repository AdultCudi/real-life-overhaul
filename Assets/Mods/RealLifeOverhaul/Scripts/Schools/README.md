# `Scripts/Schools`

Birchwood School and the city's schools: a campus on Uptown's west square (block 202, Birch to Elm Street, Uptown to Summit Avenue), made of the city's own designs and the game's own pieces, and the city's twelve schools as one market the player may buy into, whose education trusts and council run theirs by the same rules.

## Read first

- `SchoolRules.cs`: every rule of the market, game-free (checked by EconomyCheck's `SchoolChecks.cs`); `SchoolState.cs` its records.
- `SchoolGrounds.cs` and `SchoolGrounds.Dressing.cs`: where everything of the campus stands on its block (the court, the paths, the lawns, the squares people gather in), game-free.
- `SchoolsDirector.cs`, then its parts: `.Campus` (the place), `.Market` (a day's pass and the player's levers), `.Paper` (the City Ledger), `.Tests`.
- `SchoolsGame.cs`: the only file that touches the game's scene, the player, the clock and money.

## Documentation

How it works, its rules, its tests: [Birchwood School](../../../../../docs/features/schools.md).
The district it stands in: [Uptown](../../../../../docs/features/uptown.md).

Maintained by the Real Life Overhaul maintainer.
