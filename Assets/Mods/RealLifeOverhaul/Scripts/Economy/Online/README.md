# `Scripts/Economy/Online`

Online shops: a business or a warehouse of the player's sells what it stocks to the whole city. Orders come in by rule each game hour, are picked from the real stock by the staff on shift and delivered by the player's vans or a courier.

## Read first

- `OnlineDirector.cs`: keeps it going, and every effect and how it is undone.
- `OnlineCatalogue.cs`: every number that balances it (game-free).
- `OnlineRules.cs`: the rules (game-free).

## Documentation

How it works, its rules and its tests: [the game's daily statement (online sales on it)](../../../../../../docs/features/economy-city.md#the-games-daily-statement-economygamebookscs).
How the code is laid out, and how features meet: [Architecture](../../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
