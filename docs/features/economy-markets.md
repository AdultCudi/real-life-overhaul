# Economy: the stock market, your listed company and trading bots

## In a sentence

The city's stock market in the **Stocks** app: the player's own businesses listed as a company with shareholders, orders that wait (stop loss, take profit, limits) and trading bots the player can hire.

## Where the code is

- Folder: [`Scripts/Markets`](../../Assets/Mods/RealLifeOverhaul/Scripts/Markets/), with [`Listing`](../../Assets/Mods/RealLifeOverhaul/Scripts/Markets/Listing/) and [`Bonds`](../../Assets/Mods/RealLifeOverhaul/Scripts/Markets/Bonds/).
- Read first: `MarketDirector.cs` (the market on the game's clock), `MarketEngine.cs` (game-free) and `MarketApp.cs`.
- How the market itself began is on the [investors](economy-investors.md#the-stock-market-market) page.

## How it talks to other features

- The market calls no other feature and none calls it: `Wiring/MarketLinks.cs` reads the city on a market day, the events that move prices, the game's rivals, the banks and the Planner's rows.
- `Wiring/ListingLinks.cs` gives the listed company the player's books, its businesses and a sale's hand-over.

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
