# Economy: selling a business

## In a sentence

The **Sell** tab of the Services app: a business of the player's is valued, put on the market at an asking price, and sold to a buyer by name, a rival of the game's among them; the rivals sell their own weaker businesses too, offered to the player first.

## Where the code is

- Folder: [`Scripts/Economy/Services`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Services/) (`SaleRules*.cs`, `ServicesGame.Sale.cs`, `ServicesGame.Rivals.cs`, `ServicesDirector.Sale.cs`, `ServicesDirector.Rivals.cs`, `ServicesApp.Sale.cs`). The files are listed in the code block below.
- Read first: `SaleRules.cs`, then `SaleRules.Buyers.cs` and `SaleRules.Quality.cs`.

## How it talks to other features

- It shares the cleaning service's line of the save (`reallife:economy_services`, `ServicesStore`) and its app.
- Investors are bought out through Ventures, business loans are paid off through the credit core, and the save's investors are buyers by name (`Wiring/ServicesLinks.cs`).
- A buyer that is a chain is one of the stock market's listed companies of that trade (`Markets/MarketCatalogue`).
- It stands down in a Going Public session: who rents what is the host's record there.

Option "Selling a business", id `economy_sale`, under the credit core, on by default.

```
Economy/Services/       the first three are game-free and compiled by EconomyCheck
  SaleRules               the value line by line, asking prices, offers, counters, parts and the clause
  SaleRules.Buyers        named buyers: rivals within their purse, the staff, chains, investors; a rival's own sale
  SaleRules.Quality       the quality share of the goodwill, and what would raise the price, line by line
  ServicesGame.Sale       one business into plain numbers, what blocks a sale, the sale itself on the game's own calls
  ServicesGame.Rivals     the game's rivals as buyers and sellers, quality as the game scores it, the takeover
  ServicesDirector.Sale   listings, offers on the day, accept, refuse, counter, the quick sale, parts and clauses
  ServicesDirector.Rivals the rivals' sales on the day, the player's purchase, the two rival tests
  ServicesApp.Sale        the Sell tab: the businesses, the offers, the rivals' sales and each one's page
```

## What a business is worth

The base is the investors' valuation: so many days of its recent daily profit. It is taken by its kind, its district's income, the economy and how many days of books it has. Its quality then takes that goodwill from 85% to 120%:

- **Interior.** The game's own interior score (`InteriorScoreCalculator.GetInteriorScorePercentage`) against the least the district's customers want (`NeighborhoodData.minimumInteriorScore`, as `InteriorDesignCustomerDemand` tests it). Under it takes 10 points off; above it adds up to 5 in proportion to a full score.
- **Customer satisfaction.** The game's `Satisfaction.overall`, 0 to 100, which its own customers follow. 20% of its distance from 50, so up to 10 points either way.
- **Staff skill.** The mean main skill of the staff there (the player not counted). 10% of its distance from 50, so up to 5 points either way.

The game has no lease term (rent is by the day), so no line counts the lease left.

The stock and furniture are added at the price the game pays when a lease ends.

## What would raise the price

`SaleRules.Raises` works each line out by valuing the business again with that one figure changed: the interior to what the district wants (or to a full score once it is there), customer satisfaction and the staff's skill 10 higher, and a full week of books. A line that adds nothing and is not met is left out; one already good shows as good. The business's page lists them under "What would raise the price", each with about how much more it would bring.

## Who buys

Every offer comes from somebody by name, kept in the offer (`SaleOffer.Who`, `WhoName`, `WhoCount`). The turn's kind of buyer comes first; with nobody of that kind the next kind that has somebody makes it.

- **A rival of the game's.** Any rival that is not beaten, runs at least one business, and may run one of the game's businesses that has a layout for that building (the default's own rival, a special rival for a wildcard, or one of the wholesale or import rivals by its goods, as `CompetitionHelper.GetRivalIdForBusinessDefault` gives them out). Its purse is 26 weeks of its weekly takings (`RivalData.WeeklyIncome`); it bids only if that is 85% of the value, and never offers more. A building the player owns stays the player's, so no rival bids there. On accepting, a rival that can no longer buy withdraws with a notice. After the sale the rival opens its business there through the game's own `StartNewCompetitorBusiness`, and the game's rival lists are refreshed.
- **The staff.** Their most skilled employee, by name, with the staff's number.
- **A chain.** One of the listed companies of the business's trade, or of its sector with none in the trade; never one still to come to the market nor a tycoon's holding.
- **An investor.** One of the save's investors; with Ventures off, its investment firms.

The pick among a kind is drawn from the save's seed and taken in the order of the ids, so the same save gives the same buyer. A buyer never has two offers for the same business.

An offer made before named buyers keeps its kind and one of the three old names.

## Rivals selling their own

On the day event, at least 10 days after the last, with a chance of 40% a day, one of the city's three weakest rival businesses (by the mean of the game's last seven daily takings) goes up for sale, if its rival runs at least two. Only businesses under one of the game's unbranded names are sold, so a special rival's brand stays with it; never one at an address the game sets aside or a DLC keeps. The price is 95% of the game's own takeover price (`CalculateAiOwnedValuation` by the rival's accept rate). Two at most are open at once.

The player may buy it for 5 days: the same checks as BizMan's takeover (inside the building, the diplomas, a special rival's building, money), paid under the game's own transaction, then the game's own `BizManPresentation.OvertakeBusiness`. After that another rival that can run it (the same business, so the same goods) and whose purse covers the price takes it over: only `businessOwnerRivalId` changes and the business trades on. With none, the seller keeps it. A business that is no longer the seller's as it was put up comes off sale.

## The save

Version 1 still. The offer record gains three fields at its end; the header gains the day of the last rival's sale; a new part `v` holds the rivals' sales. A line written before has none of them and loads with no names and no sales.

## Tests

Options > Tests: "An offer from a rival for my best business" and "A rival puts a business up for sale", beside the earlier offer, paper and part tests.
