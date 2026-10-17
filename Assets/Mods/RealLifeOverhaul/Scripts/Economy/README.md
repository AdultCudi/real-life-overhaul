# `Scripts/Economy`

The economy: the player's money and borrowing (the credit rating, lending, credit products, debt and the bank apps, all in `Banking`), the city's economy, investors, running a business and the rota, property, brands, online shops, the Car Showroom and the cleaning service, each part in a folder of its own.

## Read first

- `Banking/README.md`: what lives where in the player's banking, and the one place for interest and terms.
- `Banking/EconomyDirector.cs`: the core: the credit side, whose timer and day the other parts follow.
- `EconomyBridges.cs`: what the economy asks of the others.
- `GameBooks.cs`: the game's daily statement, with the mod's costs on it.
- `Company/CompanyDirector.cs`: a complete feature to copy.

## Documentation

How it works, its rules and its tests: [the economy pages](../../../../../docs/features/README.md#economy).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
