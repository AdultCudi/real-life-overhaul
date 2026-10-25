# `Scripts/Shops`

New kinds of shop: an ice cream parlour, a tea bar and a tobacconist, business types of the game's registered through its mod interface, selling the game's own goods from its own furniture and run by the game's own simulation of the shop each is made like.

## Read first

- `ShopsCatalogue.cs`: the three kinds, their goods, sources, hours, days, shelves and the game types they trade as and become (game-free).
- `ShopsRules.cs`: the hour and day shares, the game's help page of a kind, the icons (game-free).
- `ShopsGame.cs`: the only file that touches the game: register, furnish, offer, strip, and what the tests read.
- `ShopsDirector.cs`: the start, the city load and the option.

## Documentation

How it works, its rules and its tests: [New kinds of shop](../../../../../docs/features/shops.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
