# `Scripts/Loyalty`

Loyalty cards for the player's shops: a scheme a business, switched on from a Loyalty tab in BizMan. Customers served well become members, come back more often and spend a little more; the player gives back a share of what members spend; other owners' shops in the district run cards by the same rules.

## Read first

- `LoyaltyRules.cs`: every number and every rule (game-free).
- `LoyaltyDirector.cs` and `LoyaltyDirector.Day.cs`: the one component, its day and its hour.
- `LoyaltyGame.cs`: the only file that touches the game.
- `LoyaltyTab.cs`: the Loyalty tab in BizMan.

## Documentation

How it works, its rules and its tests: [Loyalty cards](../../../../../docs/features/loyalty.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
