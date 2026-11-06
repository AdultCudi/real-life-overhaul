# `Scripts/Outlook`

The Economy app: an app on the game's phone that draws the city's economy as dashboards in a look of its own (dark to begin with, or light), with a small daily history of the key figures in the save and plain advice worked out from them. The phone's id for it is `RealLifeEconomy`; "Outlook" is only this folder's name.

## Read first

- `OutlookCatalogue.cs`: every number of the app (game-free).
- `OutlookRules.cs`, `OutlookInsights.cs`, `OutlookScale.cs`, `OutlookTheme.cs`: the verdict, the advice, the charts' scaling and the colours (game-free).
- `OutlookGame.cs`: the only file that reads the game. Everything else other features keep comes through `OutlookBridges`, filled in by `Scripts/Wiring/Wiring.OutlookLinks.cs`.
- `OutlookDirector.cs`: owns the history and the timing.

## Documentation

How it works, the figures it draws, the tabs and the advice's rules: [The Economy app](../../../../../docs/features/economy-app.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
