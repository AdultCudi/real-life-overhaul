# Economy: the stock market, your listed company and trading bots

## In a sentence

The city's stock market in the **Stocks** app: the player's own businesses listed as a company with shareholders, orders that wait (stop loss, take profit, limits), trading bots the player can hire, the raw materials exchange with its futures and options, and the city's builders listed with a project's materials hedged.

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
| `MarketEngine.Own.cs` | the engine's part: the book priced and the first day at the open, the business, results, meetings, investors, bids, the founder's tape events, `Alone` |
| `MarketDirector.Own.cs`, `.OwnTests.cs` | the game's part: who the player is, the books, the steps of the way, the year's fees, asking, the hand-over, the founder's notifications, the dividend tax, the save tools; the test buttons |
| `MarketApp.Own.cs`, `Listing/ListingText.cs`, `Listing/ListingGuide.cs` | the Stocks app's page and the founder's ticket; the words; the Guide's figures |
| `Wiring/ListingLinks.cs` | the bridges: the books from the game's statements (the player's own businesses only), the facts for the audit, the day's city, the hand-over and a business sold (Economy/Services), the Company app's line, the Planner |

The first five are game-free and compiled by EconomyCheck (`ListingChecks.cs`), with the engine.

### The rules in short (every figure is in the catalogue and printed by the Guide)

- **To come to the market**: two years since the first business, four complete quarters of books, takings and profit over them above a least (at today's prices), a profit in three of them, and a clean audit (tax unpaid past its day, late returns, takings kept off the books, a bankruptcy or debt arrangement qualify it; a bribe that came out is disclosed).

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

- **Meetings**: the yearly one after the year's last results (pay, a payout when the reserve is held back, the chair), an investor's after their demand; the founder's shares for the board, the others by their year, with a drawn turnout.
  Half the shares or more carries every vote.

  A bid comes while the price is well under the worth, wins only under 30% held and if the holders take it; then the businesses pass to the buyer through Economy/Services' own hand-over of a sale (`ServicesDirector.HandOver`), a few an hour as the game lets them go, what is left after seven days paid for.

### The save

The market's own line (`reallife:markets`): a player's company is a record of `k` with its own fields after the rest (founder, name, ticker, sector, usual debt, the `OwnListing` figures, the founder's deals, its trade); the way to the market and the books are part `l`, a company set aside part `u`.
Neither part is in the city's line a host sends.

A line without them reads as before, and a market with no player's company writes exactly what it did.

### Tests (options page, the stock market's tests)

"Put a test company of mine on the market" (no requirement, nothing paid or raised; a test company's books if the save has too few, read again from the statements once it is gone), "My company's results the next market morning", "My company's yearly meeting at today's close", "An investor buys in to press my company's board", "A bid for my company" (a test's company hands nothing over), "Take my test company off the market".

### Log lines

- `Listing: advisers engaged on day 140 for 25000 (the company looks worth 7454935).`
- `Listing: the prospectus of Harbour Lights Group (HLG) on day 152: 248333 new shares (2500 bp) beside the founder's 745000, ...`
- `Listing check (asked): Deal 1000 for Harbour Lights Group at 160/30, quoted 6731 (673 cents a share): put on the tape for the next moment: PASS`
- `Listing: the year's fees for Harbour Lights Group: the exchange 5000, the audit 10000; findings 0.`
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
| A tender won | `ConstructionDirector.Award`, weight 1 the city's works, 2 a business's building, 3 a public building | the winner's mood up 160 bp a weight times 7/8, every other listed builder's down 160/8 a weight: they add up to nothing, as the market expects each to win its share; the player's company winning takes the 20 a weight off them all |
| Works handed over | `SiteHanded` of a rival's tender site, against the days it bid (`RivalHanded`) | on time 40 bp a weight, late minus 120 a weight |
| A building finished in the district | `FinishDevelopment` | its builder 50 bp, every other listed builder 6 |
| A bribe come out, a strike | drawn at the close from the day's secret (`MarketRandom.BuilderNews`), 5% and 10% a year a listed builder, only in a market that lists the builders' own rows | minus 350 bp and 6% of its usual margin for good; minus 150 bp |
| A rigged tender, a building of the year | kinds 7 and 8, for the civic game's events (joined at the merge) | minus 450 bp and 10% of its margin; plus 220 bp and 2% |

A move's size is scaled by the company's size (60% large, 150% small) and held to 600 bp. The mood fades as the market's moods do; a margin's move is priced at once, as a crisis's. Each named move is a piece of market news (`MarketNewsKind.Builder`: A the kind, B the move in bp, C the weight), a notification for a builder the player holds or watches, and a piece in the City Ledger's markets section.

### Building materials on the exchange

The game's recipes have no cement, steel, bricks or timber, so the exchange has its own (`DerivCatalogue.BuildingNames`, each name its word's key), in a group of their own (`MaterialGroup.Building`: 2.8% a day, half-life 16 market days): cement and steel a tonne (450 and 1600 at the start's prices), bricks a thousand (1100), timber a cubic metre (560), the save's price multiplier on each, moving with the city's price level as every material does. A merchant's lorry load is a round number of them. Glass is the game's own. They are settled in cash, never delivered, and trade without the city's economy (which writes the game's goods only).

The construction game's merchants price their loads by them (`BuildCatalogue.ExchangeItem`). `MarketDirector.MaterialMove` gives the construction game a material's move against its own price at the price level: a load's list price already follows the price level (`PlansRules.LoadPrice`), so the level is no longer counted twice, as it was with `MaterialFactor`.

### Hedging a project (`HedgeRules`, `MarketEngine.Hedge.cs`, `MarketDirector.Hedge.cs`)

A project ready to build or building has **Materials and hedge** in the Construction app (`ConstructionApp.Hedge.cs`): what it will still buy of each material from its stages (`Construction/ExposureRules`: the stage under way what it lacks and has not ordered, at once; each later stage's loads on the day its stage begins by the project's schedule; the usual merchant's price, the speciality's saving, the exchange's moves), and the futures that would hedge it.

**Hedge** buys a long future for each material and Friday: a purchase on a day is hedged to the last Friday on offer before it (that close's price is what the merchants charge the week after), one before the first is too near, one past the last is hedged to the last; units are the purchase's money at today's prices over the material's price today, so the future's gain or loss is the purchase's dearer or cheaper price (EconomyCheck holds the money to the future's price whatever the price does). Every rule of a future holds: in a session, caught up and not held after a load, the margin and commission, 40 contracts at once and 100,000 units on one material (the legs are held to the room), the broker's calls. A project with futures still open is not hedged again; once they have settled, what is left can be.

The project keeps its futures' ids and what each had made (`ProjectFile.Hedges`, `HedgeEnded`, the construction line), looked at each day and when its page is written; at the handover a notice says what the hedge saved or cost, and the finished page keeps it.

### A company of the player's with a construction business

A construction company's money is the player's own (`ConstructionGame.Take`), not its office's statement, which holds only its wages and rent; so the listed company's books (`ListingLinks.Statements`) add the construction company's own takings and costs by day (`BuildState.Books`, kept 400 days, counted by `TakeIn` and `PayOut` wherever the company's `Earned` and `Spent` were), and a company whose largest business is a construction company is placed in the construction sector. A company listed before this keeps the sector it was listed in.

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
