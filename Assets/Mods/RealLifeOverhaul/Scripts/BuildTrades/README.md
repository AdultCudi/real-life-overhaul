# `Scripts/BuildTrades`

The building trades: the trades at the city's works sites (Work here at a site's notice), each played in a game of its own on the game's panel and bar; the architects' practice on a floor of one of the mod's buildings (the floor plan, the facade and the frame, draughting, tenders); the trades' courses, an apprenticeship and three ladders. The shifts themselves are jobs (`Scripts/Jobs/Kinds`: `TradeJob`, `DesignJob`, `TenderJob`).

## Read first

- `TradeCatalogue.cs`: every trade, where and when it is on, what it asks and teaches; the roles a construction company gives (game-free).
- `TradeRules.cs`, `DesignRules.cs`, `TenderRules.cs`: the games and their scoring and pay (game-free).
- `TradesDirector.cs`: the one component (no Update): the practice's floor and panel, the save, the career's doors, the tests.
- `TradesBridges.cs`: what the trades ask of Careers and civic life, and what a construction company asks of them.

## Documentation

What players see, the scoring, the agreement with the construction company and the tests: [Building trades](../../../../../docs/features/trades.md).

Maintained by the Real Life Overhaul maintainer.
