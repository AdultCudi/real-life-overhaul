# Startup exits

## In a sentence

A funded or bootstrapped tech firm of the player's can be bought (a listed company or a startup of the city makes an offer, priced by revenue and growth), can go to the market through bankers, a roadshow, a pricing day and a 180 day lock-up, and can sell some of its owner's shares at a later round. A failing firm may be taken for its team. The city's startups are real places: a founder who is a person of the city, a floor of a tech tower, people hired from the market the player hires from. Built on [Startup money](startup-money.md): every sale pays through the firm account.

## Where the code is

- Rules, game-free: `Scripts/Tech/Venture/ExitRules.cs` (offers, the sale's settlement, the listing, the shares), `ExitPlaces.cs` (founders, floors, hiring), `ExitCatalogue.cs` (figures and model), `ExitStore.cs` (save line `reallife:tech_exits`), `ExitBridges.cs`.
- The game's side: `Scripts/Tech/TechDirector.Exits.cs` (the day, the sale, the listing), `TechDirector.ExitsTests.cs`, `Scripts/Wiring/Wiring.ExitLinks.cs` (the market's banks and listed companies, the game's hand over of a business, the population's record, two desk sessions), `Scripts/Ui/Tech/TechLive.Exits.cs` (the pages).
- Option: none of its own; it runs with Startups and does nothing without them.

## Offers

A firm taking `RoundBar[Seed]` a month or more draws an offer on the day, seeded: 15 per mille a day, plus one for each 25 basis points of its month's growth (the firms table's own change), scaled by the market's mood, at most two standing, ten days each. The price is `FirmsRules.Value` (a multiple of revenue that grows with growth) scaled by the mood, plus a premium: 10 to 30 per cent from a listed company, up to 15 from a startup that has the cash. Accept, decline, or counter once (a tenth, a fifth or a third more): the chance rises with Selling, Analysis and Relationships. The same choices are a desk session, "An offer for your firm". Offers are never backdated.

## The sale

The business passes to the buyer by the game's own hand over of a business (`ServicesDirector.HandOver`: staff go along, the lease ends); the buyer's money comes into the wallet and the investors' lines come out of it, before the player has anything. `ExitRules.Settle`: the pot is the price, what the firm account holds and a director's loan still out, less the bridge (paid first); `VentureRules.Waterfall` shares it as it stands; the venture firms and the public are owed their lines; the rest is the player's. What the price and wallet cannot cover is the investors' claim (and the charge for a clear pattern), as at a wind down. A sale for nothing is exactly a wind down. Money counted as taken is inside the pot the investors share, so it is paid from the player's proceeds first or stays owed.

Staff option holders get no cash.

## Capital gains

Every way the player is paid for a stake notes a capital gain on the day: a firm sold (and a team taken for the hire), the vested shares sold after a round or after a lock-up, and an angel stake sold with a startup of the city. The gain is what the wallet gained less the cost basis, never under nothing, and joins the quarter's return beside a business sale's gain (`CompanyBridges.StartupGains`, summed with `OtherIncome` by `TaxRules.OtherIncome`). The basis is what the player paid for shares (`Holder.Invested`, used in proportion to the shares sold) and, for a firm sold with its business, the stock and furniture at the game's resale price in the player's share of the firm. The incorporation fee and running costs were deductible when paid and are not counted again. City startups and rivals have no return. Save line `reallife:tech_exit_gains`; check `--exit-tax`.

## The way to the market

"Ask for a listing" opens the firm's page of exits. Bankers (the stock market's three largest banks, the largest reaches furthest and keeps most), a retainer paid by the firm; a roadshow (desk session or page: wide, deep, quick) sets the demand from the market's mood, Selling, Relationships and the bank's reach and fixes the range and the pricing day; the pricing day draws the price inside the range (called off under 85 per cent of fair value); the public buys a fifth, the money after the bank's spread reaches the firm as money raised (the wallet must keep it), and the player's shares are locked up 180 days. The market engine holds only catalogue companies, rivals' holdings and one listing of the whole company, so the firm is not a second stock in the Stocks app: its public line is in its cap table (`HolderKind.Public`). The whole company's own listing stays one button away.

## Shares sold later

After a priced round, for 30 days, the player may sell a fifth of their vested shares to the round's investors at the round's price less 10 to 25 per cent (Selling and Relationships take it down); a listed firm's shares may be sold for a quarter after the lock-up at the market's spread. The money is the player's own, booked with no address; the firm account and the floor are not touched.

This is the only way the player sells their own shares. The firm page's "Sell my shares" button opens this page, and the sale keeps its rules: vested shares only, inside the window, the round's limit counted in the save line `reallife:tech_exits` (so a reload gives no room back), the lock-up after a listing, and the capital gain noted once. The older button that sold a tenth of all shares at four fifths of the post-money value is gone. A player who sold unvested shares with it keeps what they got and has nothing more to sell until shares vest. City startups and rivals never sell through this path.

## Acqui-hire

A funded firm with under 45 days of its account left, burning, may be taken for its team at 300 thousandths of what was put in. Same settlement: investors get what the waterfall gives, and no claim follows when nothing was taken. A job offer for the player is left out: Careers has no way to hand the player one.

## The city's startups

Each startup has a founder record (age, sex, skill): a resident linked from the living population (`LinkKind.Founder`, from the field its sector works in) when it runs, an invented one when not. It lets a place in one of the towers (fewest startups first, seeded), shown above the companies' floors (`Headquarters.PlaceStartups`), with its address and a Set destination button on its page. It takes people from the labour market the player hires from (`ExitPlaces.Hire`), marking them taken, so a hire one makes is gone for the other. Startups already in an older save get all this on the first day seen.

## Tests and checks

Options, Tests: a buyer makes my first firm an offer, my first firm's book is priced now, the city's startups hire from the market now. `EconomyCheck --startup-exits`.
