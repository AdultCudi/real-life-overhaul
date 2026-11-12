# Economy: the stock market, your listed company and trading bots

## In a sentence

The city's stock market in the **Stocks** app: the player's own businesses listed as a company with shareholders, orders that wait (stop loss, take profit, limits), trading bots the player can hire, the raw materials exchange with its futures and options, the city's builders listed with a project's materials hedged, and the extras: splits by each board's own level, an index fund, block trades and a bank of the player's own listed by itself.

## Where the code is

- Folder: [`Scripts/Markets`](../../Assets/Mods/RealLifeOverhaul/Scripts/Markets/), with [`Listing`](../../Assets/Mods/RealLifeOverhaul/Scripts/Markets/Listing/), [`Bonds`](../../Assets/Mods/RealLifeOverhaul/Scripts/Markets/Bonds/) and [`Derivatives`](../../Assets/Mods/RealLifeOverhaul/Scripts/Markets/Derivatives/).
- Read first: `MarketDirector.cs` (the market on the game's clock), `MarketEngine.cs` (game-free) and `MarketApp.cs`.
- How the market itself began is on the [investors](economy-investors.md#the-stock-market-market) page.

## How it talks to other features

- The market calls no other feature and none calls it: `Wiring/MarketLinks.cs` reads the city on a market day, the events that move prices, the game's rivals, the banks and the Planner's rows.
- `Wiring/ListingLinks.cs` gives the listed company the player's books, its businesses and a sale's hand-over.
- `Wiring/MarketLinks.cs` and `Wiring/CityLinks.cs` give the raw materials exchange the city's own prices of its goods and the city's economy the exchange's prices to write as the importers' ([Futures and options](#futures-and-options-scriptsmarketsderivatives)).
- `Wiring/Wiring.MarketsBuildLinks.cs` gives the market the construction game's news of its rival builders and the construction game a project's hedge ([The city's builders and hedging a project](#the-citys-builders-and-hedging-a-project-buildermarket-hedgerules)).

## Your company on the market (`Scripts/Markets/Listing`)

The player's businesses together come to the city's stock market (Scripts/Markets) as a company with shareholders (option "Listing your own company", id `markets_listing`, under the stock market's header, on by default).

It has no Update and no Harmony patch: its price, results, meetings and bids ride the market's own steps (`MarketEngine.Own.cs`), its way to the market and its year ride the market director's one-second timer (`MarketDirector.Own.cs`), and the Stocks app and the Company app write its pages when they are opened.

### What the game already had, and what was therefore built

| The game's own | Built here |
| --- | --- |
| Personal wealth (`PlayerHelper.GetPersonalWealth`: cash, funds, assets, loans) and a goal on it; a business's daily valuation (`CompetitionHelper.UpdateDailyValuation`) and a rival's business valued to buy it over (`CalculateAiOwnedValuation`, BizMan) | The company's worth by the market's own model (MarketValuation) on its real books |
| Investment funds in EconoView, which follow a yearly rate | Nothing: a fund is not a share |
| No shares of the player's company, no investors in it | (The mod's Ventures investors take a share of one business's profit; untouched) the company listed, its float, shareholders, meetings, bids |
| Business sales tax once a year on statements' sales, less what is marked deductible (`TaxHelper`, `TaxCalculationHelper.GetBusinessSales`) | The audit and the exchange's fees marked deductible; the offer's money and the holders' part are no sales; a tax withheld from market dividends |

### Files

| File | What it is |
| --- | --- |
| `Listing/ListingCatalogue.cs` | every figure: requirements, fees, the offer and the book, the company on the market, meetings, investors, bids, the dividend tax |
| `Listing/ListingModel.cs` | `OwnListing` (on `Stock.Own`: the founder, the reserve, pay, board, what the market follows, meetings, deals), `ListingFile` (on `MarketState.Listing`: the way to the market, the books, a hand-over), the enums, `ListingMoney` |
| `Listing/ListingRules.cs` | the price from the books (`Day`, `Figures`, `Morning`), the reserve (`ToReserve`), quarters and results, dividends, the founder's news, closed times, short swings, the year's limits, the price to take it private |
| `Listing/ListingBook.cs` | the books by quarter, the requirements, the audit's findings, the fees, the shares, the prospectus, the book's price, the stock on its first day |
| `Listing/ListingVotes.cs` | what the holders make of the year, the votes, investors' reasons, a bid's chance and acceptance, the dividend tax |
| `Listing/ListingFirm.cs` | a firm of the player's that listed on its own (a tech startup, [Startup exits](startup-exits.md)): the asks, the stock made on its first day, a firm's statements read from the day's env |
| `MarketEngine.Own.cs` | the engine's part: the book priced and the first day at the open, the business, results, meetings, investors, bids, the founder's tape events, `Alone` |
| `MarketEngine.Firms.cs`, `MarketDirector.Firms.cs` | a firm's stock and a city startup's made, grown, ended and swept, and the founder's deals on a firm's stock told to the tech sector; the whole company's deals, buy-back, going private and a won bid are told the same way (`CompanyDealt`, `CompanyTaken`) for the capital gains on the return |
| `MarketDirector.Own.cs`, `.OwnTests.cs` | the game's part: who the player is, the books, the steps of the way, the year's fees, asking, the hand-over, the founder's notifications, the dividend tax, the save tools; the test buttons |
| `MarketApp.Own.cs`, `Listing/ListingText.cs`, `Listing/ListingGuide.cs` | the Stocks app's page and the founder's ticket; the words; the Guide's figures |
| `Wiring/ListingLinks.cs` | the bridges: the books from the game's statements (the player's own businesses only), the facts for the audit, the day's city, the hand-over and a business sold (Economy/Services), the Company app's line, the Planner |

A firm of the player's that listed on its own is a second kind of `OwnListing`, marked `Firm` (see [Startup exits](startup-exits.md)): it has no reserve, only the founder's deals are open, it is never bid for or split, and its days are its own statements, carried on the tape after the eight columns of the whole company's. `MineListed` (the whole company) skips it and `MineFirms` finds it.

The first five are game-free and compiled by EconomyCheck (`ListingChecks.cs`), with the engine.

### The rules in short (every figure is in the catalogue and printed by the Guide)

- **To come to the market**: two years since the first business (the oldest of yours still running, counted from the day it started, which the mod keeps in the save as `reallife:founding` because the game's moving company leaves a moved business without it: `Core/FoundingGame`; a business moved before the mod kept it counts from the first day of the game's statements for the building it left), four complete quarters of books, takings and profit over them above a least (at today's prices), a profit in three of them, and a clean audit (tax unpaid past its day, late returns, takings kept off the books, a bankruptcy or debt arrangement qualify it; a bribe that came out is disclosed).

  Advisers' retainer, the audit's fee, the underwriters and the exchange as the catalogue says.

- **The price** (`ListingBook.Price`): what the books say a share is worth by `MarketValuation` (all the shares, old and new; the last year at most the two years' mean; the last quarter a patch; growth half believed; debt), 12% under it, a range 8% either side, a book drawn from the pricing day's secret and moved by the index's month, Standing, a disclosure and the float, never 15% outside the range.

  Under the least the founder takes, called off.

- **On the market**: its business is the player's businesses' real statements a day at a time (`MarketEnv.Own*`, on the tape), followed over a quarter and a year, a day beyond three times the year's usual counted as no more.
  The council's policies are priced as for any company and fade out of the reckoning as the year's figures show them; growth is read at results.

  The reserve keeps the other holders' part of each day's profit after tax and pay (forced from the founder's cash, given back out of a loss) and is in the price; dividends are paid from it.
  No crisis, windfall, failure or penny rule is drawn for it.

- **The founder** deals at the market under the rules: closed from the last third of a quarter to its results, no sale in the half-year lock-up, at most 90% held, 10% bought and 10% sold a game year, every deal read by the market, and a gain on shares bought and sold within a quarter of a year paid back to the company.

  Buy-backs (10% a year), new shares (20% a year at 5% under the bid) and taking private (25% over the price and never under the year's highest close, with half the shares) need the board.
  A business sold while listed puts the other holders' part of its price into the reserve.

  The founder is paid no dividend on their own shares: each day the other holders' part of the profit is taken from the founder's cash into the reserve (`ListingRules.ToReserve`, `Others` = shares less the founder's), so the founder keeps their own stake's part of the profit as cash, at any stake, and dividends go to the other holders (`MarketEngine.Dividends`, paid on a position, never on `OwnListing.Founder`).
  Under half the shares the founder no longer carries every vote (`ListingVotes.BoardCarries`), under 30% a bid can win; the least the founder may hold is nothing, but a sale is at most 10% of the company a game year after the half-year lock-up. The prospectus offers 10% to 60% of the company (`ListingCatalogue.FloatSteps`).
  The most of any other company one holder may have is the option "The most of a company you may hold" (`MarketCatalogue.HoldingCapBp`: 10% by default, 25%, 50% or no limit), and of a rival tycoon's holding the 25% that is free float (`HoldingFloatBp`). Nothing happens at 50%: the market has no control rule for the player's shares in another company. A rival is taken over through its business (`MarketDirector.TakeOverHolding`), not by shares.

- **Meetings**: the yearly one after the year's last results (pay, a payout when the reserve is held back, the chair), an investor's after their demand; the founder's shares for the board, the others by their year, with a drawn turnout.
  Half the shares or more carries every vote.

  A bid comes while the price is well under the worth, wins only under 30% held and if the holders take it; then the businesses pass to the buyer through Economy/Services' own hand-over of a sale (`ServicesDirector.HandOver`), a few an hour as the game lets them go, what is left after seven days paid for.

- **The sector** (`ListingSector`): the one the company earns most in now, never its first business's for good.
  On the way to the market it follows the last quarter's takings by trade (`MarketBridges.TradeTakings`, every business of the player's) until the prospectus is out.
  Listed, each market morning adds yesterday's takings by trade (the day's `MarketEnv.TradeOf`, already on the tape) to a mix that forgets over about a quarter, and at each quarter's turn a review moves it: at once when it earned nothing in its sector since the last review, else when another sector leads by 10% of the takings at two reviews in a row, so a company split near evenly never flickers.
  A trade's sector is the catalogue's first company in it; the tech sector's five firms count as web development (software).
  A move keeps the sector indices level, remakes the company's profile for the new sector with its own margin and debt, and is news (`OwnSector`): a notice and a City Ledger line. A company listed before this is moved by its last quarter's books at the load, once.

### The save

The market's own line (`reallife:markets`): a player's company is a record of `k` with its own fields after the rest (founder, name, ticker, sector, usual debt, the `OwnListing` figures, the founder's deals, its trade, then its takings by trade with a review's lead, empty for a company not followed); the way to the market and the books are part `l`, a company set aside part `u`.
Neither part is in the city's line a host sends.

A line without them reads as before, and a market with no player's company writes exactly what it did.

### Tests (options page, the stock market's tests)

"Put a test company of mine on the market" (no requirement, nothing paid or raised; a test company's books if the save has too few, read again from the statements once it is gone), "My company's results the next market morning", "My company's yearly meeting at today's close", "An investor buys in to press my company's board", "A bid for my company" (a test's company hands nothing over), "Take my test company off the market", "Check my company's sector" (the last quarter's books now, as an older save's load does; not in a Going Public session).

### Log lines

- `Listing: advisers engaged on day 140 for 25000 (the company looks worth 7454935).`
- `Listing: the prospectus of Harbour Lights Group (HLG) on day 152: 248333 new shares (2500 bp) beside the founder's 745000, ...`
- `Listing check (asked): Deal 1000 for Harbour Lights Group at 160/30, quoted 6731 (673 cents a share): put on the tape for the next moment: PASS`
- `Listing: the year's fees for Harbour Lights Group: the exchange 5000, the audit 10000; findings 0.`
- `Listing check (sector): from Restaurants to Specialists (Harbour Lights Group on the market; the last 15 days' takings by trade: clothing 300000 (Stores), jewelry 450000 (Specialists); next review on day 226): PASS`
- `Listing: Harbour Lights Group's business is followed from now; by the last 15 days' takings it moves from Restaurants to Specialists.`
- `Listing: Harbour Lights Group was taken over at 840 cents a share by a private buyer; its 6 business(es) pass to the buyer by day 212.`
- `Selling a business: handed over to a private buyer in a takeover: ...`

### Not verified (the game could not be run)

Everything on screen: how many lines the Stocks app's white box holds on the company's page (the listed page is the longest: a selector, about eight lines and three rows of buttons); the game's text box for a name and a count; the notifications' length.
That the statements' addresses compare as the mod's other readers compare them (`Books.Key`).

That `ServicesGame.Complete` hands a business over cleanly on the day of a takeover, as it does for a sale.
How the figures feel in play: the requirements against what businesses earn, how often investors and bids come.

## Orders that wait and trading bots (`Scripts/Markets`)

Stop losses, take profits and limits on the Stocks app's ticket, and trading bots the player can hire (option "Trading bots", id `markets_bots`, under the stock market's header, on by default).
No Update and no Harmony patch: the orders fill and the bots trade on the market's own steps (`MarketEngine.Orders.cs`, `MarketEngine.Bots.cs`), the option is looked at on the market director's one-second timer, and the Stocks app writes the Bots tab when it is opened.

### Orders

| Kind on the ticket | What it is | Fills |
| --- | --- | --- |
| At the market | `OrderType.Market` | at once at the offer or the bid, moved by its size |
| Limit (to buy or to sell) | `OrderType.Limit` | at its limit or the step's first price if better, once the step's range reached it; a quarter of the step's volume at most |
| Stop loss | `OrderType.Stop`, `Order.Closing` | once the step's bid (a buy: the offer) reached it, at the market's price the step ends on (`MarketTrading.MarketPrice`): the next price, under the stop after a fast fall; at the open, the open's price (a gap) |
| Take profit | `OrderType.Limit`, `Order.Closing` | as a limit |

A closing order is placed only on what is held (`OrderWhy.NothingHeld`, `NoShares`) and on the far side of the price (`StopSide`: a sale's stop under the bid; `ProfitSide`: a sale's limit over the bid), holds nothing back from other orders (`MarketTrading.Reserved`), never fills for more than is held at its moment (`MarketEngine.Closable`: no short is opened in a margin account), and when nothing is held after a fill the others on the company are cancelled (`CloseClosing`).

Each lasts the day or until cancelled (60 market days), and the Orders tab lists them with the cancel button as before.
A plain stop (a version before closing orders, or a stop to open a position) now fills the same way: at the next price, not at its own.

### Bots

| File | What it is |
| --- | --- |
| `Bot.cs` | `BotFirm`, `Bot`, `BotHolding`, `BotOrder` and the enums |
| `MarketBots.cs`, `.Decide.cs` | the four firms; every figure; the fill booked (`Fill`); the room a company leaves; splits; the strategies' scores and exits and `Decide` |
| `MarketEngine.Bots.cs` | hiring, settings, selling up, starting again, letting go; the open, each step and the close; the month's fee and share (`Settle`); dividends and a company leaving; `BotNote` for the director |
| `MarketDirector.Bots.cs` | the option, the asks from the app, the notifications, the dividend tax on a bot's dividends, the save tools, the test |
| `MarketApp.Bots.cs` | the Bots tab and its box; a bot's holdings on Portfolio, its trades marked on History |
| `TradingGuide.cs` | the Guide page's figures |

| Firm | Strategy | Rule (on daily closes) | Fee a month | Share of new profit | To begin with |
| --- | --- | --- | --- | --- | --- |
| Meridian Trend Partners | trend following | in while close > SMA 20 > SMA 50, out under SMA 50; large and mid | 0.20% of its worth | 20% | 20% a holding, stop 10%, loss 35% |
| Quayside Reversion Fund | mean reversion | in at RSI(5) < 30 over SMA 50, out at RSI(5) > 60 or after 10 market days; large and mid | 0.15% | 20% | 20%, 10%, 35% |
| Larkspur Momentum | momentum | the best gains over 60 market days to a week ago, held for a month; small too | 0.20% | 25% | 25%, 15%, 50% |
| Harbourline Income | dividend income | the highest yields of four profitable quarters that covered them, turned over monthly; large and mid | 0.10% | 10% | 10%, none, 35% |

Settings: 10, 20, 25, 50 or 100% of its worth a holding; a stop 5, 10, 15 or 25% under what a holding cost, or none; the most it may lose 20, 35, 50, 75 or 90% of its capital (`Base`).
Least capital 2500, least fee 5 a month, no purchase under 100 (all at the start's prices).

A firm's fee is for the market days it traded in the month (none while stopped); its share is of the worth over the high-water mark, which then moves up.
Both are forced from the player's cash (`reallife_transaction_shares_bot_fee`, `_bot_share`) and taken from the bot's money.

- **Money.** A bot trades the player's own cash, up to its capital: each purchase and sale is `wallet.Take`/`Give` with the player's own keys (`shares_bought`, `_sold`, `_commission`, the name "company (firm)"), so it is in the game's transactions and under investments in Finance exactly as a trade in a cash account.

  `Bot.Cash` is what of its money is not in shares; a purchase the player's cash cannot pay for is not made (one notification a day).
  A margin account is not used.
  Its dividends are paid to the player's cash and taxed as theirs (`DividendTax`), without a notification for each.

- **Timing.** It decides at each close (and when hired or started while the market is shut) on the closes and results out by then, and trades at the first step of the next session at `MarketPrice` of that moment: sales first.

  Stops are looked at against the open's bid and each step's low, and sold at the market's price then; the floor after each step and at the open, everything sold at the market then (a gap goes under it).

- **What protects the market.** No foresight (the next session's secret does not exist at the close it decides at).
  The spread, the size's move and the commission on every trade.
  Its trades' lasting move and volume put on the price as the player's (`ImpactPermanent`), counted in the player's recent trading (`Stock.Impact`), so the player's own next trade pays for them too.

  The player's holding and every bot's together held to a tenth of a company (`MarketBots.Room`).
  No player's company, no company under a bid, nothing listed under 20 market days.
  Nothing changed while the market is behind or held after a load.

- **Going Public.** Bots live in the save's line only (never the city's), come into the host's market with a guest (`MarketShare.Adopt`) and trade there as the player does: a host's bot trade is a tape event (`EventTrade`, `EventVolume`), a guest's goes to the host as the player's `i|stock|micros|shares` request.
  Nothing new is sent.

### The save

Part `b` of `reallife:markets`, one record a bot: firm, status, capital, base, money, settings, days, peak and drawdown, high-water mark, record, month, then its holdings (`id,shares,cost,stop,opened,gain,merged`) and what it waits to trade.

A bot's shares are also written into the player's holding of part `p` (the holding's `merged` field says so) and taken out again on reading, so a version that knows no bots reads them as the player's own shares, paid for with the player's money.

A dividend owed on a bot's shares is a record of `d` with the firm after it (an older version pays it to the player); a bot's trade in `t` has the firm after it; a closing order in `o` has a 1 after it (an older version reads a stop or a limit).
A line without bots and closing orders is written exactly as before.

### Tests

Options page: "A month's statement for my trading bots now".
EconomyCheck (`MarketChecks5.cs`): closing orders' rules and gaps.

The checks cover the bots' rules: every fill at the price of its moment and booked as the player's, decided before the next day's secret, the same on two machines, moving prices and shared as a player's trade.
They cover fees and the high-water mark, the floor and a jump through it, splits, a company leaving, letting go, and the save's line (and read without part `b`).

And 24 seeds of two 60-day years for each strategy: losses and gains in every one, none beating holding the market with its dividends.

### The shares a bot trades, payouts and dividend news

The Bots tab's box has three pages of settings, chosen by its first selector: how it trades (the three above), the shares it trades, and its payouts.
A line over them says both in words.

- **Scope** (`BotScope`, `MarketBots.InScope`, `Allowed`).
  `Market`: what its firm buys (`Buyable`), as before.
  `Picked`: only the companies picked for it, at most 12 (`Bot.Picks`; the picker offers the player's own listed company, the picks, what the player and their bots hold and what they watch).
  `Held`: only what the player holds themselves, their own company too while they are its founder.

  A company the player chose is bought whatever its size, never one under a bid, a penny share or listed under 20 market days.
  A holding outside its scope is sold at the next session (`Decide`).
  Another player's company is never in a scope.

- **The player's own company** (`MarketBots.OwnRoom`, `OwnCount`, checked in `MarketEngine.BotFillNow` for every fill).
  By the founder's rules: nothing in the closed time (`ListingRules.Closed`).
  No sale within the short-swing time of a purchase nor a purchase within it of a sale (`OwnSwingDays`: a year / 4).
  The founder's own deals and the bots' together within the founder's year's limit (`FounderDealMostBpYear`).
  The founder's and the bots' shares within `FounderMostBp`.

  And every bot together at most 10% of its ordinary day's volume (`OwnVolumePct`, `Stock.Adv`) in a day.
  It reads the company and the player's own bots only, and writes only the bots (their own counts), so every machine of a Going Public session plays the company alike.
  A bot holding its shares is not let go (`BotWhy.OwnShares`): it sells them up first, and with the option off it is sold up and then let go.

  A bot under its floor that may not sell them yet keeps selling up.

- **Payouts** (`MarketBots.Payout`, `PayDue`).
  A share of new profit (0 to 100% in steps of 10), daily, weekly (a week's last market day) or monthly (with the month's statement, after its fee).
  Profit is `Realised + Dividends - Fees`; it is new only above the best reached (`PayMark`), so a loss is carried forward.
  What is owed is paid out of the bot's money not in shares, the rest waits (`PayOwed`).

  The bot trades the player's own cash, so no money moves: a payout takes the sum off `Bot.Cash`, and `Base`, `Peak`, `HighWater` and `MonthWorth` come down with it, so it is never counted a loss.
  A weekly payout has a notification (`BotNoteKind.Paid`), a daily one is in the statement, and the statement says what the month paid (`BotNote.D`, `reallife_mkt_bot_note_statement_paid`).
  The table's profit counts what was paid out.

- **Dividend news** (`MarketNewsKind.Paid`).
  Each dividend paid to the player, on their shares or a bot's, is a line of the market news (in the save's line, never in the city's line nor the City Ledger).

  The director keeps a `DividendTally` and tells it once a frame: one notification with its company (and bot), or one for several with how many and how much (option "A notification when a dividend is paid to you", id `markets_dividend_notes`, on by default).

- **The save.** Each bot record of part `b` has after what it waits to trade: scope, picks (`id:id`), payout share and time, `PayMark`, `PayOwed`, `PaidOut`, `MonthPaid`, and its trading in the player's company.
  A record without them reads as its whole market and no payout, with what it made before reckoned already (`PayMark` is its profit then).

EconomyCheck (`MarketChecks7.cs`): the scopes' rules and runs, a holding outside its scope sold, the player's company's every limit and a purchase through the market held to the day's share, letting go refused and then done.
Payouts' times and arithmetic, runs over six seeds where no money is made by paying and the statements add up to what was paid.
The news line for each dividend, kept in the save's line and not in the city's.

The line round trip and a line from before; two machines alike; the texts in every language; the cost.

### Not verified (the game could not be run)

That seven tabs fit the Stocks app's bar, and the Bots tab's box (four lines, three selectors and a row of buttons) the white box; the game's text box for the capital; the notifications on screen; the transactions' names in Finance and EconoView; how the bots feel over a long game.

## Futures and options (`Scripts/Markets/Derivatives`)

The raw materials exchange, futures on the raw materials factories buy, and options on those materials and on the city's larger companies, on the Stocks app's Futures tab (option "Futures and options", id `markets_derivatives`, under the stock market's header, on by default).

No Update and no Harmony patch: the exchange's prices and every contract's money ride the market's own closes (`MarketEngine.Derivatives.cs`, called in `CloseBooks`), the option and the city are looked at on the market director's one-second timer, and the Futures tab is written when it is shown.

### What the game already had, and what was therefore built

The game's factories (`Buildings.Factory.FactoryBusinessSimulator`) make goods from recipes (`BigAmbitions.Factories.Recipes.Recipe`); 59 of their ingredients are made by no recipe and are bought from the importers (`Entities.ImportPartnership.DoDeliveries`, each day at 08:00, `ImportProduct.Price` = `ItemHelper.GetWholesalePrice` x the market event's multiplier).

`GetWholesalePrice` multiplies the item's `wholesalePrice` by the item's import index and the save's price multiplier, but `ProductMarketHelper.UpdateMarketDemand` returns before it sets an index for an item that is not a demanded product (`CanItemHaveMarketDemand`), and none of the 59 is: no market entry is made for them, so their index is 1 and no shortage or backorder is ever drawn for them.
In the game a raw material's price never moves.

The city's economy (Economy/City) moved them only as general goods, all together, with the general kind's supply shocks (announced a day ahead) and the monthly price index.

So the price movement was designed first, from the city's economy and the market's tape:

| | Built here |
| --- | --- |
| The price | Each material on the exchange (`RawMaterial`, the city's, in part `m` of the market's line, the host's in a Going Public session) has `price = its own x the price level x e^x`. At every market close `x' = phi x + mu + sigma (rho zg + sqrt(1 - rho^2) zi)`: a Gaussian autoregression drawn from the day's secret (the tape's, so a replayed day sets the same prices and nobody knows a close before it is played), half its group's draw and half its own, `mu` set so the long-run mean is exactly its own price at the price level. |
| The importers | The price a close sets is the importers' until the next: `CityBridges.MaterialFactor` gives `CityGame.ApplyWholesale` the exchange's factor for a raw material in place of its kind's factor and the index, and the market director asks the city to write it again after each close (`MarketBridges.MaterialsFixed`, `CityEconomyDirector.RewriteWholesale`). With the option off, the market stopped or the city's economy off, the city prices them as before. |
| The groups | Farm and field (20 materials), dairy, meat and bakery (11), drinks and flavourings (7), metals, glass and stone (6), electronic parts (8), paper, cloth and plastic (7); any other is Other. |

| Group | A day's move | Half-life | A week's move | The long run | Margin |
| --- | --- | --- | --- | --- | --- |
| Farm and field | 4.0% | 8 market days | 7.6% | 10.0% | 23% |
| Dairy, meat and bakery | 3.0% | 10 | 5.9% | 8.3% | 18% |
| Drinks and flavourings | 2.4% | 10 | 4.7% | 6.7% | 15% |
| Metals, glass and stone | 3.2% | 14 | 6.5% | 10.4% | 20% |
| Electronic parts | 2.6% | 14 | 5.3% | 8.5% | 16% |
| Paper, cloth and plastic | 2.2% | 12 | 4.4% | 6.7% | 14% |
| Other | 2.5% | 12 | 5.0% | 7.6% | 16% |

(A week's move and the long run are one spread of the ln price; the margin is three spreads of a week's move, 10% to 40%.)

### Contracts

| Kind | What it is | Its price | At its day |
| --- | --- | --- | --- |
| A future bought or sold | a material's price fixed for the week after a Friday | the price expected at that close (`MaterialRules.Forward`, exact), 0.3% either side | settled at the price that close sets: a future bought with a place is delivered, any other in cash |
| A call or a put on a material | the right to what that close's price is over (or under) its strike | Black on the exact forward and spread, 8% over and 0.15% of the price either side | paid in cash in the money, else it lapses |
| A call or a put on shares | the same on a city company's share | the market's own moves to its day as the GARCH expects them, its results day, the business's own news at the opens, and jumps mixed in at their chances (a crisis, a windfall, a bid, a crash; `OptionRules.ShareFair`), the same markup | at the mean of the last hour of steps of its day; a company that leaves settles at what it paid out a share; a split changes strike and shares, worth the same |

- **Days.** The next four Fridays at least two closes away (`DerivRules.Days`).
  A material's contract is for the week from the Monday after: the importers charge that Friday close's price until the next.
- **Strikes.** The price now (a material's forward, a share's price) and 5% and 10% either way, four figures kept.
- **Quantities.** 500 to 25,000 units; 100 to 5,000 shares.
- **Limits.** 40 contracts at once; 100,000 units under contract on one material; on a company at most half a day's usual volume and half the most a player may hold.
- **Options on shares** only on the city's own companies, large or middling, 20 market days on the market, not under a bid, distress under 0.15, no property trust, no player's company, no rival's holding.
- **Commission** the market's own (0.10%, never under its least) on a future's worth or an option's premium.
- **Trading** only while the market trades (`MarketDirector.Trading`: in a session, caught up, not held after a load).

### The margin account and the broker's rules

A future's margin is taken from the cash into the account (`DerivBook.Balance`, in cents) when it is bought.
At each close every future is marked to its new fair price and the day's move settled in the account, rounded against the player; what is over the margins is paid back to the cash.

Under 75% of the margins at a close: a call for what brings it back to the margins, due by the next close (`MarketCatalogue.CallDays`), paid on the Futures tab (`PayCall`).

Unmet at that close, or under 80% of the maintenance (`ForcedBelowPct`) at any close, the broker closes the futures at that close's prices, the largest margin first, until the account is a tenth over its maintenance; with none open, what the account owes is forced from the cash (`MarketMoney.Shortfall`).

### Delivery

A future bought may name one of the player's warehouses or factories (`MaterialGame.Places`: rented, theirs, `GoingPublic.Mine`).

At its day's close the engine settles its money to the day's price and tells the director (`DerivNoteKind.Deliver`); after the frame's market work the director puts the goods on the place's pallets as the game's importers do (`ItemHelper.DeliverCargoToBuilding` with the warehouse-storage tag), as many as fit and as the cash pays for, and charges them to the place (`reallife_transaction_future_importdelivery`, deductible as an import).

With what the future made, the goods cost the agreed price; what did not fit or could not be paid is settled in cash already.
In a Going Public session every future settles in cash: the goods in a warehouse are the session's to move.

### Fair and not to be played

- The forward is the true expectation, so a future bought or sold and held loses the spread and the commission on average, whenever it is bought (EconomyCheck: 120,000 futures over eight seeds settle within 0.6% of their fair price; buying after a rise, after a fall or on a flat day earns nothing).
- An option on a material is priced exactly, so every option of every kind, strike and week pays out less than it costs (1.2 million over eight seeds: 1.00 of their fair premium, 0.90 of what they cost).
- An option on shares is priced from the market's own model; over four seeds of five years 1.7 million pay out 0.99 of their fair premium and 0.90 of what they cost, each strike and week's call and put together less than they cost, none far off its fair premium.
- The exchange's draws are the tape's: a reload replays the same closes and nothing is traded until the market is past the furthest moment played (`HoldAfterLoad`), so a load gives no foresight and no second draw.
- Nothing is traded at or after a close: the prices a close sets are known only once it is played, and the next contract is two closes away.
- A share option settles at the last hour's mean price, so no trade at the close decides it; the shares under option are limited to half a day's usual volume.
- Every sum is rounded against the player.

### Going Public

The exchange is the city's: part `m` is in the city's line a host sends (an older RLO reads past it), and every machine plays the host's closes with the host's secrets, so the prices are the same on each.

A guest keeps the host's own prices for the materials it has; the market's checksum is unchanged, so an older guest is never asked to take the market again.

Each player's contracts are their own (part `q`, never in the city's line), settled on their own machine against the shared prices; they move no price and nothing new is sent.

### Files

| File | What it is |
| --- | --- |
| `Derivatives/DerivCatalogue.cs` | every figure: the groups and their moves, the days, quantities and strikes, the spreads and markup, the margin, the limits, the share options' figures |
| `Derivatives/DerivContract.cs` | `RawMaterial`, `DerivContract`, `DerivBook`, the enums, `DerivNote`, `DerivQuote` |
| `Derivatives/MaterialRules.cs` | the price process: a close, the forward and spread after so many closes, the first price, the materials known |
| `Derivatives/OptionRules.cs` | the bell curve and Black's price; a share's variance to its day, its jumps, its forward and its fair premium |
| `Derivatives/DerivRules.cs` | the days, the money of a move, the spreads, the margin, the limits, which companies, the settlement price, a split |
| `Derivatives/DerivStore.cs` | parts `m` and `q` of the market's line |
| `MarketEngine.Derivatives.cs` | the engine's part: quotes, buying and closing, the call paid, the close's marks, settlements and margin, a company gone, leaving, the test tools |
| `Derivatives/MaterialGame.cs` | the game's part: the raw materials from the recipes, the places, a delivery, what the factories use |
| `MarketDirector.Derivatives.cs` | the director's part: the option and the city followed, the materials learned, the importers' prices written again, the tab's questions, the notifications, deliveries, the tests |
| `MarketApp.Derivatives.cs`, `Derivatives/DerivText.cs`, `Derivatives/DerivGuide.cs` | the Futures tab and its tickets; the words; the Guide's figures |

The first six are game-free and compiled by EconomyCheck (`DerivChecks.cs`, `DerivFairChecks.cs`; `--derivatives` runs them alone).

### The save

Part `m`, a record a material: name, group, its own price, x, the last price set, its day, the price level, the last 30 closes.

Part `q`: the margin account's header (the next id, the balance, a call's day and amount, commission and results in all), then a record a contract (kind, material or company, units, price or strike, premium, mark, margin, days, the place and its name, the test mark, how it ended, its price, result, units delivered and fees).

A line without them reads with no exchange and no contracts; a market with neither writes exactly what it did.

### Tests (options page, the stock market's tests)

"Open the Futures tab", "My futures and options settle now" (at today's prices, a delivery included), "My futures' materials move a tenth against me" (the margin looked at as at a close: a call, or the broker; refused in a Going Public session, where the exchange is everyone's).

### Log lines

- `Stock market: the raw materials exchange has 59 of the game's raw materials (59 new, 59 in the game's recipes).`
- `Stock market: bought Barley, the week from day 50: 5000 at 252731 micros a unit, margin 290, to be delivered to Dockside Warehouse.`
- `Stock market check (futures and options, day 47): Deliver ...; the margin account 0 for margins of 0: PASS`
- `City economy check (wholesale written again): 59 price(s) to the raw materials exchange's or their kind's: PASS`

### Not verified (the game could not be run)

That nine tabs fit the Stocks app's bar and a ticket (five selectors, three lines and a button) the white box; the notifications on screen; the transactions' names in Finance and EconoView; that `ItemHelper.DeliverCargoToBuilding` puts a future's goods on a factory's pallets as it does a warehouse's; that the importers charge the exchange's price from the close (read from the game's code: `ImportProduct.Price`); how the prices feel over a long game.

## The city's builders and hedging a project (`BuilderMarket`, `HedgeRules`)

The construction game's rival builders on the stock market, moved by what they do in it; the exchange's own building materials; and a project's materials hedged with futures from the Construction app (option "Builders on the market and hedging materials", id `markets_builders`, under the stock market's header, on by default).

No Update and no Harmony patch: the builder news waits in a list of 32 at most and goes onto the tape once a second (`MarketDirector.BuildersSecond`), the engine plays it at the next moment and draws a builder's own news at the close, and the Construction app writes its page when it is opened.

### Who is listed

| Rival builder (Civic's contractors, in their order) | On the market |
| --- | --- |
| Dunmore and Pike Civil | Dunmore and Pike Civil (DPC, 147), mid |
| Hartwell Groundworks | Hartwell Groundworks (HWG, 149), small |
| Calloway Brothers Scaffolding | private, a family firm |
| Ostrander Building Co. | Ostrander Building Co. (OBC, 146), mid |
| Tri-Street Utilities | Tri-Street Utilities (TSU, 148), small |
| Mercer Shopfitters | private |
| Ironbridge Contracting | Ironbridge Civil Works (IBW, 32): its contracting arm, so the sector never has two companies of the name |
| Fenwick and Daughters | private, a family firm |

The four new rows (`BuilderMarket.OwnRows`) are catalogue companies of the construction sector like any other: quarterly dividends, results, the bots, options on shares. They join a market only while the option is on (`MarketSetup.Open` and `Join`); once listed they stay. Gorsebank Construction is the construction company's trade (`MarketInputs.Trades`' newest, "construction").

### What moves them (`BuilderMarket`, `MarketEngine.Builders.cs`)

| News | From | Its move |
| --- | --- | --- |
| A tender won | `ConstructionDirector.Award`, weight 1 the city's works, 2 a business's building or refit, 3 a public building, 4 one of Uptown's landmarks (the largest) | the winner's mood up 160 bp a weight times 7/8, every other listed builder's down 160/8 a weight: they add up to nothing, as the market expects each to win its share; the player's company winning takes the 20 a weight off them all |
| Works handed over | `SiteHanded` of a rival's tender site, against the days it bid (`RivalHanded`) | on time 40 bp a weight, late minus 120 a weight |
| A building finished in the district | `FinishDevelopment` | its builder 50 bp, every other listed builder 6 |
| A bribe come out, a strike | drawn at the close from the day's secret (`MarketRandom.BuilderNews`), 5% and 10% a year a listed builder, only in a market that lists the builders' own rows | minus 350 bp and 6% of its usual margin for good; minus 150 bp |
| A tender ring found out, a building of the year | the city's inquiry finding a ring (`ConstructionDirector.Rings`' `RingFound`, each listed member, as the City Ledger's `reallife_bld_paper_ring_found` story names them); the building of the year's builder (`ConstructionDirector.Award`) | minus 450 bp and 10% of its margin; plus 220 bp and 2% |

A move's size is scaled by the company's size (60% large, 150% small) and held to 600 bp. The mood fades as the market's moods do; a margin's move is priced at once, as a crisis's. Each named move is a piece of market news (`MarketNewsKind.Builder`: A the kind, B the move in bp, C the weight), a notification for a builder the player holds or watches, and a piece in the City Ledger's markets section.

### Building materials on the exchange

The game's recipes have no cement, steel, bricks or timber, so the exchange has its own (`DerivCatalogue.BuildingNames`, each name its word's key), in a group of their own (`MaterialGroup.Building`: 2.8% a day, half-life 16 market days): cement and steel a tonne (450 and 1600 at the start's prices), bricks a thousand (1100), timber a cubic metre (560), the save's price multiplier on each, moving with the city's price level as every material does. A merchant's lorry load is a round number of them. Glass is the game's own. They are settled in cash, never delivered, and trade without the city's economy (which writes the game's goods only).

The construction game's merchants price their loads by them (`BuildCatalogue.ExchangeItem`). `MarketDirector.MaterialMove` gives the construction game a material's move against its own price at the price level: a load's list price already follows the price level (`PlansRules.LoadPrice`), so the level is no longer counted twice, as it was with `MaterialFactor`.

### Hedging a project (`HedgeRules`, `MarketEngine.Hedge.cs`, `MarketDirector.Hedge.cs`)

A project ready to build or building has **Materials and hedge** in the Construction app (`ConstructionApp.Hedge.cs`): what it will still buy of each material from its stages (`Construction/ExposureRules`: the stage under way what it lacks and has not ordered, at once; each later stage's loads on the day its stage begins by the project's schedule; the usual merchant's price, the speciality's saving, the exchange's moves), and the futures that would hedge it.

**Hedge** buys a long future for each material and Friday: a purchase on a day is hedged to the last Friday on offer before it (that close's price is what the merchants charge the week after), one before the first is too near, one past the last is hedged to the last; units are the purchase's money at today's prices over the material's price today, so the future's gain or loss is the purchase's dearer or cheaper price (EconomyCheck holds the money to the future's price whatever the price does). Every rule of a future holds: in a session, caught up and not held after a load, the margin and commission, 40 contracts at once and 100,000 units on one material (the legs are held to the room), the broker's calls. A project with futures still open is not hedged again; once they have settled, what is left can be.

The project keeps its futures' ids and what each had made (`ProjectFile.Hedges`, `HedgeEnded`, the construction line), looked at each day and when its page is written; at the handover a notice says what the hedge saved or cost, and the finished page keeps it.

### A company of the player's with a construction business

A construction company's money is the player's own (`ConstructionGame.Take`), not its office's statement, which holds only its wages and rent; so the listed company's books (`ListingLinks.Statements`) add the construction company's own takings and costs by day (`BuildState.Books`, kept 400 days, counted by `TakeIn` and `PayOut` wherever the company's `Earned` and `Spent` were), and a construction company's works count towards the construction sector in the listed company's sector (`ListingSector`).

### Going Public

The builders' news is the tape's own event kind (`MarketInputs.EventBuilder`, A the builder, B the kind and weight): the host's construction game tells it, every machine plays it at the same moment from the host's tape, and a guest's own is dropped by its engine as every guest's event is. A builder's own news is drawn from the day's secret and only where the builders' own rows are listed, which the host's market decides. The building materials are in part `m` of the city's line. Nothing new is sent; each player's hedge is their own futures (part `q`) and their own project. The test entries refuse in a session.

### Tests (options page, the stock market's tests)

"A rival builder wins a public building's tender now", "A bribery scandal at a rival builder now", "Put the builders' share prices back as before the tests"; "Hedge the test project's materials with futures now" (a block of homes ready to build, the test tools' own, hedged by contracts marked as theirs at any hour), "The test project ends now: what its hedge saved or cost" (its futures settled at today's prices), "Put the hedge tests back: futures undone, test project gone" (every test contract closed and its money given back, to the broker's rounding); "Check how a company with a construction business is listed". Each is a public static method and writes one `Builders on the market check (...)`, `Hedging check (...)` or `Listing check (...)` line. All seven are in the city expansion's test run (`.Check(...)` in `BuildersGuide`, where the player stands, before the load), the two put backs last of them.

### Log lines

- `Builders on the market check (a rival builder wins a tender now): Ostrander Building Co. (OBC) 28.00 to 29.18 (+4.21%, wanted up), 4 of the 4 other listed builder(s) down, its news in the market's: PASS`
- `Stock market: a hedge of 7 of 7 future(s) bought for 1712000 of purchases to come, margin 361000, commission 1712.`
- `Construction: project 12's hedge saved 4310.`
- `Hedging check (the test project's end): 7 of 7 future(s) settled at today's prices; the hedge cost 2210, 0 still open: PASS`

### Not verified (the game could not be run)

The Materials and hedge page's length in the white box (five materials, up to five futures' lines, a quote and two notes), the notifications on screen, the builders' news in the City Ledger, the units' words, a hedge's futures on the Futures tab, the transactions in Finance; how often the construction game gives the market news in a long game, and how the builders' prices feel with it.

## Stock market extras (`Scripts/Markets/Extras`)

Splits by each board's own level, shares put together under a unit, the founder's own split, an index fund, block trades, a bank of the player's own on the market by itself, and the rival tycoons doing the same (option "Stock market extras", id `markets_extras`, under the stock market's header, on by default; a player's suggestion).

No Update and no Harmony patch: splits are decided at the market's closes and take effect at its opens (`MarketEngine.Extras.cs`), the fund's fee and distributions ride the closes, the player's own push fades with the market's steps, the bank's way to the market and the rivals' day are looked at on the market director's one-second timer (`MarketDirector.Extras.cs`, `.ExtrasBank.cs`), and the sheets are built when they are shown.

### What was there, and what was therefore built

| Was there | Built here |
| --- | --- |
| A split of two, three or five for one when a city company or the player's whole company closes over 300.00 (`MarketCatalogue.SplitAboveCents`), five market days after it is announced (`MarketLife.Split`) | Each board's own level; shares put together; the founder's own split; the player's drawn lines, alerts and rules and the rivals' blocks follow a split; a split decided on this build waits for a declared dividend to leave the price |
| No fund: the game's investment funds follow a yearly rate and are no share | The City Index Fund over the city's index |
| One order held to a fifth of a day's usual volume (`OrderShareOfVolumePct`), a large order worked a slice a step | A block: many shares in one deal at the market's own price for its size |
| The player's whole company listed together (`Listing`), a tech firm with recurring revenue listed on its own (`ListingFirm`); the bank's takings only lines of the headquarters' statement | The bank's own books, and a listing of its own; a firm's real takings counted for its way to the market |
| A rival's "buying shares in a listed company" was a line in the loans feed and nothing else | A real block, units of the fund, and a bank or lab of their own listed |

### Files

| File | What it is |
| --- | --- |
| `Extras/ExtrasCatalogue.cs` | every figure; `ExtrasKeys`, the keys of the reasons |
| `Extras/ExtrasState.cs` | `ExtrasState` on `MarketState.Extras`: the units, the fund's standing, cents kept, splits for the charts, the splits decided on this build that may wait (`SplitWaits`), the player's push, what was told, the rivals' books, `BankFloat` (its stage, offer, days with the capital of each, the stake last seen, the capital that counts) |
| `Extras/ExtrasStore.cs` | the save line `reallife:stock_extras` |
| `Extras/SplitRules.cs` | a board's level, who puts shares together and by how many, the founder's reasons, the wait for a dividend, a consolidation applied, what follows a split |
| `Extras/FundRules.cs` | what a unit holds, a deal quoted and booked, the close (fee, distributions), a company that leaves |
| `Extras/BlockRules.cs` | a block quoted, its least and most, the 5% steps told |
| `Extras/BankFloatRules.cs` | the bank's own days and quarters, when it may list, the capital that counts (`Held`), its worth, the prospectus, the price and its cut, the offer's money, the stake and the owner's part of a payout (`Stake`, `Payout`), the stock's most |
| `Extras/RivalMarket.cs` | the tycoons' day: units, a block bought or sold, an arm listed |
| `Extras/MarketEngine.Extras.cs` | the engine's part: a split at an open, the decisions at a close, the tape's event, fund deals, blocks, a rival's block, the bank's price |
| `Extras/MarketDirector.Extras.cs`, `.ExtrasBank.cs`, `.ExtrasTests.cs` | the game's part: the option, what the app asks, notifications, the tax on distributions, the bank's way, the rivals' day, the tests |
| `Extras/ExtrasBridges.cs`, `Wiring/Wiring.StockExtrasLinks.cs` | the bridges: the bank, Standing, the tax return, the rivals, the company's books |
| `Extras/ExtrasText.cs`, `Extras/ExtrasGuide.cs` | the words; the Guide's figures |
| `Ui/Stocks/StocksBuild.Extras.cs`, `StocksScreen.Extras.cs` | the four sheets and what their buttons ask |

The first eight and the engine's part are game-free and compiled by EconomyCheck (`StockxChecks.cs`, `.Rules.cs`, `.More.cs`, `.Fix.cs`; `--stockx` runs them alone). The rules the director calls for the bank's payouts and the rivals' looks are game-free too (`BankFloatRules.Payout`, `OwnBankRules.PublicIn`, `OwnBankRules.OwnerShare`, `RivalMarket.NextLook`), so the checks drive them and not the source text.

### Splits

- **Each board's own level** (`SplitRules.LevelOf`): from a company's id, one of thirteen levels from 180.00 to 300.00 in steps of 10.00. A city company that closes over its level and at or under 300.00 splits by the old ratio rule (`MarketLife.SplitRatioAt`: two for one, three from 600.00, five from 900.00); over 300.00 the old rule still splits every company, option on or off. A player's own company splits by the old rule or when its founder asks.
- **Shares put together** (`WantsReverse`, `ReverseRatio`): a city company or a tycoon's holding that has closed under a unit (the penny rule's 1.00 at today's prices) for 10 to 14 market days running, with distress under 0.5, no bid and no dividend (none a quarter, none declared and still in the price), puts 2, 5, 10, 20 or 50 old shares into one new: the smallest that takes the price to 10.00 at today's prices. A company nearer failing, or one still paying yesterday's dividend on a collapsed price, is left to the penny rule, which delists at 20 days as before: put together and kept on the market, such a company would go on paying a dividend its earnings no longer bear (EconomyCheck's rules of thumb showed buying distressed companies 12% a year over what they should return). From the 14th day the notice would end after the penny rule's day, so nothing is decided.
- **The founder's own** (`FounderWhy`, `MarketEngine.AskSplit`; the Listing page's **Split the shares**): 2, 3, 5 or 10 new for one, or 2, 5 or 10 old into one. The founder must chair the board; no bid and no split announced; no split takes a share under 5.00 at today's prices and no consolidation over 150.00, under every board's level. A firm with investors (`OwnListing.Firm`), the bank's stock too, is never split: its shares are also lines of a cap table and staff options counted outside the market (`MarketEngine.Own.cs`, the old rule's own exception).
- **Decided once, heard everywhere**: a board's level, a consolidation and the founder's ask are decided where a close is played for the first time on a market of its own (`ExtrasCorporate`) and put on the tape as `MarketInputs.EventSplit` (A the company, B the ratio, under nothing for old shares put together). Every machine and every replayed day takes them from the tape, whatever its own option; a founder's is tested again at its moment. The split is announced then (`MarketNewsKind.Split`, or `Reverse` = 34) and takes effect five market days on.
- **At the open** (`MarketEngine.SplitNow`): a split waits while a dividend is declared and has not left the price (a dividend a share in whole cents cannot be divided without somebody gaining by the rounding), then every count and price changes in one moment: the old `MarketLife.Split` for new shares, `SplitRules.Reverse` for old shares put together. Only a split this build decided waits: the ones that came by the tape are marked when they are announced (`ExtrasState.SplitWaits`, a stock and its day, saved in part `w`). The old rule's own split (over 300.00 at a close, `MarketEngine.Close.cs`, `MarketEngine.Own.cs`) is not on the tape, so it takes effect on its day as it always did and never waits: a day already played, or a tape from an older build, plays as it was. The mark lives in each machine's own extras, so a guest who joins in the days between a split's announcement and its day takes it without the wait. The player's holding, open orders, stops, bots, options, the founder's shares and reserve, candles, results, the dividend a share and, in a forward split, the dividends declared since the last yearly meeting (`OwnListing.YearDividendCents`, which the meeting reads against the price then) change as before; new here are the player's drawn lines and price alerts (`ChartBook`), the rules written on the company (`RuleBot`: its shares, shares a trade, a grid's level), the rivals' blocks and a note for the chart.
- **A part of a share** left by a consolidation is paid for at the price before, not rounded until the sum is: whole money into the account that holds the shares (the wallet, or a margin account's cash; a short pays), the cents under a unit kept in `ExtrasState.OddCents` and paid with the next. The cost that went with it is taken as a gain or a loss, so the cost of what is kept and what was paid for add up to the cost before. What is left of an open order is divided, not its two figures apart: an order of 100 with 5 filled, ten into one, has 9 left to fill (the 5 filled were part of the holding that was divided). An order with no whole share left is cancelled, a worked order's money settled.
- **The chart** shows candles at today's share count, so no crash is drawn; the company page's readings begin with the last split ("Split 2 for 1 on day 61. Earlier prices are shown at today's share count.", `StocksBuild.SplitLine`); the last 48 splits are remembered.

### The City Index Fund (`FundRules`)

- **What a unit holds**: the city's index (free float, no company over a tenth: `MarketIndex`) over 10, in money, times the fund's standing against it (`ExtrasState.Track`): 100.00 a unit at 1,000 points. It is read from the companies' prices of the moment it is asked, never from a close.
- **Dealing** (the Portfolio's **Index fund**; `FundRules.Quote`, `MarketEngine.FundTrade`): a unit is dealt at what its slice of every company costs at that company's own offer (sold: its bid), each slice moved by its size as an order of that size is (`MarketTrading.Impact`, the player's recent trading in that company counted) and never at a price the player's own lasting push made better for them (`ExtrasState.Pushes`: every fill of theirs, their bots' and their founder's deals, fading as the mood fades). Each slice then counts in the player's recent trading in its company. The market's commission (0.10%, never under its least) on top. From and into the cash, whatever the account; while the market trades, caught up and not held after a load. Fund deals move no price.
- **Limits**: no slice more of its company than one order may be, and the units held stand for at most 2% of the whole market's float (`FundHoldingBp`).
- **The fee**: 0.30% a year (`FundFeeBp`), a market day's share taken out of the standing at each close, once a day (`TrackDay`), whether units are held or not.
- **A company that leaves**: the index's divisor keeps the index where it was, but the fund held the company; the standing changes by its weight times what its holders were paid against its price (`FundRules.Left`, called in `Leave` before the company is taken out). Listings and splits change nothing a unit holds.
- **Distributions**: at the close of a company's record day the units held are owed the dividend of the shares behind them, on that company's own payment day (`ExtrasState.Dues`). The fund's dues are counted (`MarketEngine.ExtrasDues`) after the players' own dividends and before any company leaves at that close (`Corporate`: taken over, delisted by the penny rule, failed), so a company that leaves on its record day still owes the fund its dividend; paid into the cash to the whole unit of money, the cents kept for the next, and taxed as a dividend is (`ListingVotes.DividendTax`, `ListingMoney.DividendTax`). Units bought after the record day are owed nothing of it.
- **Tax**: as shares are. The mod's quarterly return counts gains on a founder's own stake (`ExitGains`); gains on ordinary shares, and so on units, are in the account's "Realised" and in no return.
- **With the option off** no unit is bought; units held can still be sold, the fee still comes out and distributions are still passed on.
- **Going Public**: the units, the standing and the push are each player's own, in their own line; a deal moves no price and sends nothing. A guest's units are counted on the host's index while they are a guest, as their shares are on the host's prices.

### Block trades (`BlockRules`)

- **What it is** (the Portfolio's **Block trade**, on the company last opened; `MarketEngine.Block`): more shares than one order may be (at least an order's most and one) in one deal, as one market order filled by the market's own fill (`MarketTrading.Fill`): the offer or the bid moved by the square root of its size over a day's volume, the lasting quarter of that move on the price, the commission, the history, and in a Going Public session the trade's move sent as any trade's is.
- **Limits, none of them new**: the most one holder may have (`MarketTrading.MostHeld`: the option's tenth, quarter, half or all; of a tycoon's holding its free float of 25%); no company under a bid; one that has traded 20 market days (28 days of the calendar); no player's own company, a firm's or the bank's stock too (its founder deals by the founder's rules on its page); shares sold must be held and not already asked to be sold; the cash, or a margin account's rules. At the most of a tycoon's holding the takeover on the rivals' page opens as before.
- **Told**: a holding that passes a whole 5% of a company, up or down, is market news (`MarketNewsKind.Block` = 35: the holding in bp, bought or sold, and the tycoon's holding for a rival's), in the market's news, the City Ledger's markets section and a notification for a rival's block of what the player holds or watches. However the holding was built: every fill of the player's own goes through `MarketEngine.ExtrasHeld` (a block, an ordinary or worked order, a dividend put back), and the step it stands on is kept, so a sale that takes it under a step is told as well and a few shares more on the same step tell nothing. With the option off nothing is told. Shares held by the player's bots are not counted in the player's holding. Told on the machine that dealt.
- **Not to be played** (EconomyCheck, eight seeds): a block bought and sold again loses the spread, both moves and two commissions; the same shares as orders placed at the same moment cost at least as much; no block takes a holding past the most. Orders spread over hours pay less of the move, as a worked order does: that is the market's own patience, not a block's.

### A bank of your own, and a studio, on the market (`BankFloatRules`)

- **Why they could not**: the bank's takings were lines of the headquarters' statement, which the market reads only inside the player's whole company (`ListingLinks.Statements`), and the one way to list a single firm asked for a tech firm (`TechDirector.ExitBankers`). A tech firm's way opened only on recurring revenue (`ExitRules.ListReady` on `FirmsRules.Arr`), which is nothing for a game studio (`FirmsRules.MonthlyRevenue` returns 0 for a game) and for work done by the project.
- **A studio or a consultancy**: the way to the market now counts a firm's month as its recurring revenue or its steady takings, whichever is more (`ExitRules.ListMonth`, `TechDirector.ListMonthOf`), and its worth as its books' value on the steady month, or what the market's model makes of its own statements when that is more (`ExitRules.ListValue`, `TechDirector.ListValueOf`, `ExitBridges.BooksWorth`). Steady takings (`FirmsRules.Steady`) are the lesser of the firm's last month and the month before it, so one launch's payout or one bid's upfront lifts a month and not the other and never stands for the firm; a firm with less than two months of takings behind it (`TechStatusRules.ChangeFromDays`, 60 days) has none. They leave out what the player's own other businesses paid the firm (a contract whose client is theirs: `Firm.Self30`, `SelfDays`, `SelfPrev30`, kept by the same day's roll as the takings, `FirmPlace.TodaySelf`), money from one pocket to the other. The firm's page and its usual worth for an offer to buy it (`TechDirector.FirmValue`) are unchanged. Everything after is the firm's existing path.
- **The bank's own books**: each day the bank books tells its income, its profit after savers' interest, losses and running costs, and the capital it held that evening, the offer's money not counted in it (`OwnBankBridges.DayBooked`); its days are kept for the whole look back a whole company's listing reads, three game years (`ListingCatalogue.BooksLookBackYears`, at most 600 days), and 12 quarters for its books (`BankFloat.Days`, `Books`).
- **When it may** (`BankFloatRules.Ready`; the Listing page's **List your bank**): a bank running a game year, no test's, four complete quarters of its own books with a profit in three, clear of the regulator and no run, the player's whole company neither listed nor on its way (the bank is part of it already), the option on and the market this machine's own.
- **Its worth**: the market's model on those books (`ListingBook.Worth`, as any company's), never over 3 times the capital it held throughout (`BankBookMost`, `BankFloatRules.Held`): the least of its own capital over the last year (the four quarters its books are judged on) and now, so cash put in for a day and taken out again counts for nothing. Savers' deposits and the cash that covers them are not among what the market is told (`BankFacts` holds no more than whether it runs, is a test's, is clear of the regulator, is gone, the day it was founded, its capital and its name).
- **The way**: advisers engaged for the usual retainer from the player's cash (`ListingBook.Retainer`: 0.50% of the worth, 25,000 to 400,000 at the start's prices), every test made first; 10, 15, 20, 25, 30 or 40% of the bank offered in new shares; the usual range (12% under the worth a share, 8% either side); the book priced at the open five market days on from the day's secret as a company's is (`ListingBook.Price`), called off under the range's bottom, the retainer then spent. On the pricing day the capital that counts is read again (`BankFloatRules.Held`) and the price is cut so the founder's shares are worth no more than three times it: capital taken out since the advisers were engaged cuts the price, or calls the offer off when it falls under the range's bottom. A bank that is watched, in breach or in a run on that day, or is a test's, has its offer called off (`BankFloatRules.PriceReady`).
- **The offer's money** (the new shares at the price, less the underwriters' 5% or 4% and the exchange's 0.10%) goes into the bank's capital (`OwnBankDirector.PublicCapital`, `OwnBankRules.PublicIn`), never the player's cash, and stays there while the stock trades: it is marked as the public's (`OwnBank.Public`, saved in the bank's line) and counted among what the bank keeps whatever it pays out (`OwnBankRules.Keep`), so no pay, dividend, automatic dividend or sale can take it. Once the bank's stock has left the market it is released (`OwnBankDirector.PublicRelease`).
- **Listed**: a stock among the banks made by the path a firm's is (`MarketEngine.ListFirm`, place `#bank`), the founder locked up half a year, its days the bank's own (`ExitStockLinks.Env`). The bank's income is in one stock only: while it trades, every day the bank recorded over the whole three year look back is taken out of the whole company's books (`StockExtrasLinks.Books`, `BankFloatRules.Sum`), so a whole company listing after the bank counts none of it twice. What holds on the stock's value: it is worth no more than three times the capital that counts at any open (`OwnListing.CapCents`, set on the stock itself from the capital that counts and the offer's money, saved with the stock so every machine plays the same; `MarketEngine.ExtrasCap` brings a price over it back to it; a price under it is left), and the capital that counts includes the offer's money, which cannot leave. A price can pass it between two opens by the day's trading; it rests there no day. From then on what the bank pays its owner out of its capital (the owner's pay, a dividend by hand or automatic, a sale, what a winding up leaves) is every holder's by their shares: the owner has their part, rounded down (`OwnBankGame.Owner`, `OwnBankRules.OwnerShare`, `BankFloatRules.Payout`), never the whole while the bank is listed: the stake is read from the bank's stock, or when the stock cannot be found (a guest in another player's city has it set aside) from the last stake seen (`BankFloat.Founder`, `Shares`, in the save's own line), or from the offer's own shares. The rest is counted as the other holders' (`BankFloat.PaidOthers`). A bank in breach or in a run pays no pay, dividend or automatic dividend and cannot be sold (`OwnBankRules.MostOut`, `OwnerPay`, `Sellable`), listed or not; its book is not priced in a watch, a breach or a run. The founder deals in its shares on its own page by the founder's rules; each deal is told to the tax return once, with what the bank was founded with as the shares' cost (`TechDirector.StakeDealt`, basis line `#bank`). A bank sold or closed takes its stock off the market.

### The rival tycoons (`RivalMarket`, `MarketDirector.RivalsDay`)

Each tycoon who is forward looks at the market every 30 days, on a day of their own, on a market of this machine's own; every choice is drawn from the market's id, the tycoon and the day. Every day passed is played: the director asks `RivalMarket.NextLook` until none is due, so the days between two of its looks (a load, a long sleep) are not skipped; each look is drawn from the day it is for, at most 12 at once (`RivalLooksMost`) and the rest the next second. A tycoon who never looked starts with their latest day within a month.

- **The fund**: units for 0.5% of their worth at a look until they hold 1.5% of it.
- **A block**: of a listed company for 1% of their worth, one time in three another tycoon's holding; at most 5% of one company and what any holder may have, four companies at once; bought over the offer and sold under the bid by the market's own depth rule, so they lose by a round trip too. Its lasting move and shares go on the tape as a trade's (`EventTrade`, `EventVolume`), and a holding past 5% is told. One look in five they sell the oldest instead, the gain or loss into their worth. A tycoon's money is conserved: no cash is kept for a tycoon, a purchase of a block (or of units) is a swap inside their worth, and nothing leaves `Tycoon.Worth` when they buy; what they gain or lose comes into it when the block is sold or its company leaves (`RivalGain`), and the units' gain is not counted, as they are never sold. Their announced move "buying shares in a listed company" (`RivalMoveKind.Shares`) is now such a block.
- **An arm**: one look in four, once, a tycoon whose holding is listed puts a bank or a lab of it on the market (`MarketEngine.ListStartup`: "Karen Bank", "Luthor Labs"), worth 8% of their fortune, among the banks or the software companies, and from then a company like any other: it splits, pays, fails or is bid for.
- **Shown**: the rich list and each tycoon's panel on the Finance app's rivals page (`RivalBridges.MarketLine`), the market's news and the City Ledger. Their stake in and bid for the player's company are the rivals' own moves, as before.

### The save

`reallife:stock_extras`, version 1, a line of its own: parts `f` (units, cost, realised, paid, fees, the standing, its day, cents kept), `d` (distributions owed), `s` (splits), `w` (splits decided on this build that may wait: the stock and the day), `p` (the player's push), `t` (holdings told), `r` (each tycoon's units, look, arm and blocks), `b` (the bank's way, with at its end the stake last seen and the capital that counts), `k` (its days: day, income, profit and the capital of the evening, under nothing when not known), `q` (its quarters). A save without them starts with nothing; a part or a field of a later version is passed over; a line that cannot be read is kept as it was. Never in the line a host sends. The market's own line keeps a consolidation to come as a ratio under nothing in the split's own field, and a bank's stock keeps its cap after the figures an older version reads. The own bank's line keeps the money kept for the public after its crew (`^e`), a tech firm's line the takings its owner's own businesses paid it after the month before (`FirmsStore`).

### An older build with this build's save

An older build (one without the extras) reads the market's line as far as it knows it. A consolidation to come is a ratio under nothing: it clamps the ratio to nothing and then clears the split's day (`MarketStore.Stocks.cs`), so the shares are not put together and the penny rule still stands behind them; the two news kinds it does not know (34 and 35) are dropped when the market's line is read; the tape's split event (kind 21) has no row in its table of kicks, so it moves nothing, and a Going Public guest on an older build plays a market without these splits. The `reallife:stock_extras` line is a key it does not read and leaves in the save untouched. The bank's stock, a firm of the player's with the place `#bank`, is a place the older build's sweep of firms does not find among the tech sector's, so it takes the stock off the market paying nothing (the offer's money stays in the bank, and the older build pays the owner every payout whole); nothing throws and nothing the player holds as money is lost. Back on this build the bank's stage reads as listed with its stock gone, and the bank's way ends (`BankLeft`). Read from the older build's code, not run.

### Tests (options page, the stock market's extras)

"A company I hold splits two for one at the next open", "A company I hold puts its shares together, ten into one, at the next open", "Check the index fund against its parts now", "Quote the largest block of a rival's holding now", "A rival tycoon looks at the stock market now", "Check my bank's own books for a listing". Each writes one `Stock extras check (...)` line; none starts in a Going Public session.

### Log lines

- `Stock market check (split, day 61): Northgate Stores 2 for 1, now 2400000 shares at 9120 cents; the player holds 400: PASS`
- `Stock market check (fund): bought 250 unit(s) at 10441 cents against 10420 held by a unit (20 bp), 26129 with a commission of 26; PASS`
- `Stock market check (block): bought 150000 of Luthor Everything Co. at 4486 cents (648 bp against the block), holding 1080 bp of at most 2500: PASS`
- `Stock market: the bank Harbour Bank (HBK) listed on day 212 at 950 cents: 200000 new shares raised 1800000 into its capital after 95000 to the underwriters and 10000 to the exchange; the founder keeps 800000 shares.`
- `Stock market: Space Karen looked at the market on day 95: 3800 unit(s) of the fund bought (3800 held); bought 21000 of Northgate Stores at 4810 cents for 1010100.`

### Not verified (the game could not be run)

That the four sheets fit the Stocks desk's sheet as their samples do; that the Portfolio's bar holds two buttons more and the Listing page's one more; the notifications on screen; the fund's and a block's transactions in Finance and EconoView; the rich list's and the tycoon's panel's new line; that the headquarters' statement and the bank's own days agree to the day; how often boards split and the tycoons deal over a long game; that the bank's stock stays under its cap through a real game year of trading; what an older build does with this build's save (read from its code, not run); that a guest who joins in the days a split waits on a dividend sees it wait.
