# Economy: the city

## In a sentence

The city gets an economy of its own: a central bank that sets a rate each month, a price index, good and bad times, districts with an income level, and wholesale prices that move.

## Where the code is

- Folder: [`Scripts/Economy/City`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/City/). The files are listed in the code block below.
- Read first: `CityEconomyDirector.cs`, `CityModel.cs` and `MacroCatalogue.cs` (game-free).
- The game's daily statement, which the city adds its lines to, is `Economy/GameBooks.cs`: see [The game's daily statement](#the-games-daily-statement-economygamebookscs) below.

## How it talks to other features

- The city's economy calls no other feature and none calls it: `Wiring/CityLinks.cs` gives it the season and weather and business pressure's footfall, copycat rivals and morning roll.
- Other features read it through `CityEconomy`; see [For other features](#for-other-features-cityeconomy).
- Every fixed amount of the mod's follows the city's price level through `Core/Prices`; see [The calendar and the price level](../reference/calendar-and-prices.md).

The third part of the economy (`Scripts/Economy/City`; options "The city's economy" and "How far the economy swings", ids `economy_city` and `economy_city_level`).
The city gets an economy of its own: a central bank that sets a rate each month, a price index, a cycle of good and bad times, districts with an income level, and wholesale prices that move.
It stands on the credit side above and runs only while that runs.

No method of the game's is patched and nothing of the mod's is ever written into saved game data.

```
Economy/City/       the first four are game-free and compiled by EconomyCheck
  MacroCatalogue      EVERY NUMBER of this part (the name: Scripts/CityLife already has a CityCatalogue)
  CityModel           CityState (what the save holds), CityDistrict, SupplyLine; CityModel (one day, one month, the central
                      bank's rule, and what the state means for footfall, prices and worth); CityAdjust (the adjustments' arithmetic)
  CityStore           the save's one line, "reallife:economy_city"
  CityEconomy         THE PUBLIC FACE for other features: State, BasePoints, PriceIndex, DistrictIncome(name), MonthTurned
  CityGame            THE GAME SIDE: districts from the game's neighbourhoods, the goods and their wholesale prices, the
                      player's businesses and their prices, the day's rent, wages and takings; CityBridges; CityShop
  CityText            the words: one way of writing each figure for the app, the paper, the Planner and a notification
  CityEconomyDirector keeps it going and applies it: the day, the hour, the save; its parts .Money (the rent, wages
                      and takings adjustments), .Rivals, .News (notifications and the paper) and .Tests
Economy/Finance/FinanceApp.City.cs   three lines on the Overview and the page "The city"
Wiring/CityLinks.cs                  the calendar and the weather, business pressure (footfall, rivals), the paper, the Planner's rows
```

## The model

Everything is a whole number, so a run is exact and a save holds exactly what was in memory: the rate in quarter points, inflation in hundredths of a per cent a year, the index in thousandths, an income in tenths of a point, a price factor in thousandths.
Nothing is random but `CityModel.Draw(seed, purpose, count, range)`: the seed is drawn once when the economy is opened for a save and kept in its line.

| what | figures (`MacroCatalogue`) |
| --- | --- |
| a month | `daysPerYear / 12` game days, 3 at least (5 with the game's 60-day year). The model counts its own days (`Age`): a month turns when `Age` is a multiple of the month's days. Days that pass while the option is off are not counted. |
| the central bank | the rate in points on top of the banks' own: starts at 0, -2 to +6, in quarters. Decided on the last day of each month for the next (`CityModel.Rule`): +0.25 for each whole point inflation is over the target's band (one quarter at least), the same downwards when it is under; a slowdown takes a quarter off the decision and a recession a half; with inflation in its band and no bad times, a quarter back towards 0. One point at most a decision. |
| prices | inflation starts at the target, 2% a year, band of half a point, held between -1% and +12%. Each month it moves half-way towards: the target, plus the cycle's push (boom +1.2, slowdown -0.8, recession -1.6), plus a slow pressure (-1.5 to +3.5, moving by up to 0.6 a month and losing a tenth of itself), plus the month's own surprise (-1 to +1.5), less 0.6 for each point of the rate of the month before last (the lag). The index starts at 100 and rises each month by a twelfth of inflation; held between 50 and 400. |
| the cycle | boom, steady, slowdown, recession. A state lasts 2 to 6 months; the first 3 months are steady. What follows what, in per cent (each save moves each row's two chances by up to 8): boom to steady 70, slowdown 30; steady to boom 45, steady 20, slowdown 35; slowdown to steady 65, recession 35; recession to steady 65, slowdown 35. So a recession only ever follows a slowdown. |
| a state's numbers | footfall +6 / 0 / -5 / -12 per cent; what a customer spends x1.04 / 1 / 0.97 / 0.93; the chance of a business trouble x0.85 / 1 / 1.15 / 1.35; property x1.10 / 1 / 0.95 / 0.85; how often a rival undercuts x1 / 1 / 1.5 / 2. |
| districts | one for each of the game's neighbourhoods (`NeighborhoodHelper.NeighborhoodsData`, not the global one). The start is the mean of 70, 100 and 135 by the game's own shares of working, middle and upper class (`NeighborhoodData`), 100 where it gives none. Each month: the cycle's push (boom +1, slowdown -0.8, recession -1.6), up to 1.5 of its own either way, and 15% of the way back to its start. Always 60 to 140 and within 20 points of its start. |
| supply | six kinds of goods (`KindOfType`: fresh produce and flowers; groceries and drink; clothing and gifts; electronics and appliances; home and office goods; general goods). An item's kind is that of the first business type in the list that sells it (`BusinessType.businessProducts`), so a specialist's goods are its own even if the supermarket has them too. A kind's factor is 1 plus the season's push (`SeasonPush`), the weather's (`WeatherPush`), the city event's (`EventPush`) and a running shortage or glut. A shortage (+15% to +40%) or a glut (-8% to -24%) is drawn with a chance of 7% a day, 55% of them shortages, for 3 to 8 days from the next day; one drawn a day at most, two running or announced at most. The factor is held between 0.7 and 1.5. |
| strength | Relaxed 50%, Standard 100%, Realistic 110%, Hardcore 125% of every swing: the cycle's numbers, the pushes on inflation, a district's monthly move, supply's pushes and shocks. Relaxed never enters a recession (where the table says one, the slowdown goes on), and a recession running when Relaxed is chosen ends at the next day. |

## What it does to the player, and how each is done and undone

The rule for every effect: (a) through a mechanism the mod already has and restores; else (b) a figure of the game's in memory that it reads when it computes, with its own value kept and put back when the feature stops, on a fault, on unload and before every save; else (c) a dated transaction of the mod's own.

| effect | way | how |
| --- | --- | --- |
| **loan rates** | (a) | `LendingTerms.BasePoints` is added to the rating's points before the rate is rounded and held to its floor, so `BankTerms` writes it on the banks' settings with everything else and the bank's dialogue and the Borrow tab show it. A bank's rate is a whole per cent in the game, so quarters count once they add up with the rating's points. The mod's own accounts count in hundredths: an overdraft and a card are opened at the rating's rate and charged `AccountRules.BaseBp` on top day by day (`CreditAccount.RateAgreedNow`); car finance and a mortgage take it once, when approved (`ProductTerms.FixedRateBp`). A loan or account already made keeps its rate. Both numbers are the mod's own and are 0 while this is off. |
| **wholesale and import prices** | (b) | The game works a wholesale price out each time it is asked (`ItemHelper.GetWholesalePrice`: the item's `wholesalePrice`, the market's import index, the game's price multiplier), on the wholesaler's shelf, on a delivery contract and at the importer. The item is an asset in memory, not part of a save. `CityGame.ApplyWholesale` writes `own x the kind's factor x the index / 100` on every retail product with a wholesale price, once a day and only where the price is not already that; a raw material the stock market's exchange prices (`CityBridges.MaterialFactor`, [Futures and options](economy-markets.md#futures-and-options-scriptsmarketsderivatives)) gets `own x the exchange's factor` instead, written again after each market close. `RestoreWholesale` puts every item's own price back exactly. Nothing to restore before a save: the items are not in it. |
| **footfall** | (a) | For each business the player runs: the cycle's per cent, a tenth of a per cent for each point its district is away from 100, and its prices against what the district bears; together -25% to +15%, and nothing under 2% either way. Written through business pressure's own promotion figure: `BusinessGame.Footfall` asks `BusinessGame.Ambient` each time it writes a business's figure, so one writer puts an issue's change and the city's on it together, each inside its own bounds. Applied each morning with the day's customers drawn again; kept each game hour; put at the game's own before every save and until the next game hour; the game itself writes its own figure every night. |
| **price tolerance** | (a) | The game keeps each business's prices (`BuildingRegistration.retailPrices`). They are judged against the game's own price for the neighbourhood (`Item.DefaultMarketPrice` x `CitizenHelper.averagePriceIndicesInNeighborhoods`, plus the 0.3 the game itself allows a monopoly), as a mean over the price list, and that against what the district bears: half a per cent more or less for each point its income is over or under its start, times what customers spend in the cycle. Up to 2% over costs nothing; then 0.8% of customers for each per cent over, 10% at most; at or under, +2%. It is part of the footfall above. The game's own table is read and never written (its figures reach rivals' saved prices). |
| **rents and wages** | (c) | After the game's daily run, its own rent and wage transactions of the day that ended are added up (`CityGame.DayFigures`: by the game's categories) and one transaction each adjusts them by `index / 100 - 1`: "Rent, cost of living adjustment", "Wages, cost of living adjustment". None within 1% of 100. |
| **takings** | (c) | The same for the day's revenue transactions, by `index / 100 x what customers spend - 1`: "Takings, prices and the economy". The game's shelf prices cannot follow the index (its customers judge them against fixed prices), so the index reaches takings this way; without it rents, wages and wholesale would rise for ever against takings that could not. |
| **property** | shown only | "Worth today" on the city's page: the game's market value (`Building.GetMarketValue`) times the cycle, the district against its start (0.4% a point) and the index. The game's sale price is not changed: its inputs also set rents the game saves. |
| **rivals** | (a) | Business pressure's morning roll is multiplied by the cycle's trouble number and its "undercut" kind weighs 1.5 or 2 times as much in a slowdown or a recession (`BusinessDirector.RollScale`, `WeightOf`). A business with a profit on each of the last 14 daily statements may draw a rival: each day, with a chance of 6% times the trouble number, the one that made the most gets business pressure's own "a competitor undercuts you" through its own rules (`BusinessDirector.StartIssue`), 21 days apart at least. Its notification, its answers and its end are that feature's. |
| **credit risk** | none added | The rating's "owed against income" already falls with income. Nothing hidden is added. |

An adjustment that would take money is never taken beyond the cash in hand (`CityAdjust.Take`): what cannot be taken is carried to the next day as it is, per stream, in the state.
The credit side settles its day first (it calls the city's economy when its own work on the day event is done), so an adjustment never takes the cash an account's payment was waiting for.

"Prepare this save for removing the mod" writes what was carried off.
The adjustments carry no category of the game's, so its daily statements, its tax and the Overview's rent and wages lines stay the game's own figures.

## What the player sees

- **Finance, Overview**: "Bank rate: banks' own +1.25", "Prices: 104.2, up 0.8% this month", "The economy: slowdown, month 2", and a button "The city".
- **Finance, The city** (a page of the Overview, not a sixth tab): in the box the rate, the next decision (or what was announced for tomorrow), prices, inflation, the economy, what rents, wages and takings come to against the game's figures, what is carried.

  In the table the districts (income, the month's change), the six kinds of goods (today's price against the usual, what runs or starts tomorrow, "you stock it"), each business (district, customers, prices over or suiting the district) and each building owned (the game's value and today's).

- **The paper** (`News.Extra`): two pieces a day at most, in this order: a shortage or a glut from tomorrow, a rival, the rate decision (the day before it takes effect, with its reason), a change of state, the month's prices, a district that moved 2 points or more.
- **The Planner** (rows of the Finance source): the rate decision on the day it takes effect, once announced; a shortage or a glut from tomorrow in goods the player's businesses stock.
- **Notifications**: the rate moved while something is owed on an overdraft or a card; a shortage or a glut from tomorrow in goods the player's businesses stock.
  "Stock" is read from their price lists and delivery contracts: the game's purchase transactions do not name what was bought.

## The save

One line, `reallife:economy_city`: `v=1~h:...~d:...~s:...~n:...` (`CityStore`): the header's 22 numbers, the districts (`name|start|income|a month ago`), supply (`factor|shock|from|days|size` per kind) and the pieces for the paper that are not out of date.
A second line, `reallife:economy_city_before_tests`, holds the first line as it was before a test tool changed it, while test changes are in place.
Both are registered with the feature.

A reader takes the fields it knows and skips parts it does not; a figure out of bounds is put inside them; a line that cannot be read is replaced by a new economy.

## For other features: `CityEconomy`

Read-only and safe at any time.
While the city's economy is off, not started or stopped by a fault every member answers as a city with no economy of the mod's would.

| member | while it runs | otherwise |
| --- | --- | --- |
| `CityEconomy.Running` | true | false |
| `CityEconomy.State` | the `CycleState` | `Steady` |
| `CityEconomy.BasePoints` | the rate in points on the banks' own, -2 to +6 | 0 |
| `CityEconomy.PriceIndex` | the index, 100 at the start | 100 |
| `CityEconomy.InflationPercent` | per cent a year | 2 |
| `CityEconomy.MonthOfState` | the month of the state, from 1 | 1 |
| `CityEconomy.DistrictIncome(name)` | 60 to 140, by the game's own name for the neighbourhood (`ba:neighborhood_...`); 100 for one it does not know | 100 |
| `CityEconomy.MonthTurned` | an event, raised once on the game's day event after the month's figures are in place; a handler that throws is logged and stops nobody | never raised |

## Added to other features (all additive)

- `Economy/Banking/Terms/LendingTerms`: `BasePoints`, and `For(score, cap, rate, basePoints)`.
  `ProductTerms`: `BaseBp`, `WithBase`, `FixedRateBp`; `RateBp` is by the rating alone.
  `AccountRules`: `BaseBp`, `BaseMultiplier`.
  `CreditAccount.RateAgreedNow`.
  `ProductOffer.AgreedBp`.
  `EconomyDirector`: `Second`, `AfterHour`, `AfterDay` (its one-second timer and its handlers of the game's hour and day events call the city's economy, which has no timer and no handler of its own in the game's events), `OpenCity`.
  `Finance.OpenCity`.
- `Business/BusinessCatalogue`: `AmbientMostDip`, `AmbientMostBoost`, `ClampAmbient`, `PromotionFor` with an ambient part.
  `BusinessGame`: `Ambient`, `LastOwn`, `LastWritten`.
  `BusinessDirector`: `RollScale`, `WeightOf`, `StartIssue(kind, reg)`, `Footfall(reg, redraw, newDay, out own, out written)`.
- `Social/News`: `Extra`, called once when the paper is written, after the business piece.
- `Wiring/EconomyLinks`: one call to `CityLinks.Rows` in the Finance source of the Planner.

## Cost

No Update, no timer and no handler of its own in the game's events, nothing per frame and nothing allocated per second.
Once a second (on the credit side's timer): four comparisons.
Each game hour: two comparisons for the rate, and one promotion figure written per business that carries a change.

Each game day: one tick of the model (a few dozen integer operations), one comparison for each good with a wholesale price and a write where it differs, one pass over the player's businesses and their price lists, one pass over the game's transaction history (the last thousand entries) for the adjustments, and the day's customers drawn again for each business whose footfall is changed.

That last one is the only work of any size: it is the game's own draw (`CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness`), done once more for those businesses at midnight, exactly as business pressure does for a business with an issue.
With diagnostics on the "applied" line gives the time of the whole day's application in milliseconds.
A business whose three parts come to under 2% is left alone.

## Options, tests, diagnostics

- "The city's economy" off: the rate is the banks' own, every wholesale price and footfall figure is the game's, no adjustment is made and the model stands still (its state stays in the save).
- "How far the economy swings": the four presets (Relaxed, Standard, Realistic, Hardcore).
- Test buttons: show the city's economy in the log; step a month; put the economy in a boom, steady, a slowdown, a recession; a shortage, a glut from tomorrow; bank rate up, down one point; raise, lower this district's income by 10; put the city's economy back (the state from before the first test change).
- Always written: `City economy: opened for this save on day n. ...` or `state loaded (version 1) ...`; `City economy: switched off for this session after an error in ...`.
- With "Developer diagnostics" on:
  - `Economy check (city, self-check at load): 312 good(s) with a wholesale price, 0 not as expected, the bank rate at the banks' own: PASS`
  - `Economy check (city, applied, the day, day 41): the rate +0.5, 104 wholesale price(s) written (fresh 1.134, ...), 3 footfall figure(s) written, 3 with the day's customers drawn again: <business> -7% (cycle -5, district -2, prices 0 at 99.1% of what the district bears, promotion 62 to 55); ...; 1.8 ms: PASS`
  - `Economy check (city, decision on day 40): inflation 3.10% against a target of 2.00%, the cycle Steady, the rate +0.25: moved by +0.25 from tomorrow (PricesHigh): PASS`
  - `Economy check (city, month 8 on day 41): the rate +0.25 to +0.5 (PricesHigh), inflation 3.10% to 2.95% a year (...), the index 103.9 to 104.2, the cycle Steady to Steady (month 3, 2 left): PASS`
  - `Economy check (city, adjustment for day 40): rent 1200 by the game, factor 1.042, so 50 more to pay; carried from before 0; 50 taken, 0 carried to tomorrow: PASS`
  - `Economy check (city, rival): ...`, `Economy check (city, before a save): 3 footfall figure(s) put at the game's own until the next game hour ...: PASS`
  - `Economy check (city, restored, the option is off): 104 wholesale price(s) and 3 footfall figure(s) put back, the bank rate back at the banks' own. ...: PASS`
- A fault anywhere in it switches the city's economy off for the session with one line, puts every figure back and leaves the game's daily run and the credit side alone.

## Checks

`EconomyCheck` compiles four more game-free files and runs 424 checks (`Checks3.cs` has the new ones):

- twenty years of the model from 60 seeds at each strength, with a 60-day and a 365-day year: every figure inside its bounds every day, the first three months steady, no recession on Relaxed, a decision announced on the last day of every month and exactly what takes effect, every shortage and glut announced the day before unchanged;
- the central bank's rule case by case and over its whole range;
- the footfall and tolerance arithmetic, and a district's way back to its start;
- the adjustments (rounding, the 1% band, carrying when cash is short, nothing lost over thirty days);
- the rate hook (nothing when off, added before the rounding and the floor, what follows it and what does not);
- the save's line (round trip at any point, a run carried on from a save, a later version, damage) and determinism.

## Not verified (the game could not be run)

That the game's neighbourhood data is loaded when the economy starts and what class shares each neighbourhood has.
That every wholesale path reads `Item.wholesalePrice` at purchase (read from the decompiled game: the shelf sign, `PlayerItemPurchaser`, `DeliveryContract.TotalPricePerDelivery`, the importer).
That the game's item assets keep a written price between a city unloading and the next one loading (the restore on unload is there for that).

How the kinds of goods divide the game's items (the "show" test button counts them).
That the day event follows the game's rent and wage transactions as the decompiled `GameManager.NewDay` says.
That three more lines and a second button fit the Overview's white box, which does not scroll, and how the city's page reads with every tab unmarked.
The time the second draw of customers takes with many businesses.

## The game's daily statement (`Economy/GameBooks.cs`)

The game builds each day's statement (`GameInstance.financialSummaries`) first thing on the next day, before `onNewDay`: for each business, four lines of costs summed from that day's transactions by the business's address and the game's categories (salaries, rent, marketing, licensing fees), with goods, sales and theft from its own records.
The game's bank sizes a loan by it (`PlayerHelper.CalculateDailyIncome`: the last seven days' profit), and EconoView and the daily summary show it.

A cost of the mod's that is one of those lines carries the category and the address: Company's wages (raises, overtime, sick and strike pay), marketing and licences (`CompanyGame.Take`), and the business issues' marketing, licence and staff costs (`BusinessGame.Charge`).

What is booked after midnight for the day that ended is put into that day's statement when the mod's day is done (`EconomyDirector.OnDay`, after `AfterDay`): the four lines summed again as the game sums them, and the totals with them (`GameBooks.Refresh`).
Insurance, accountants, hosting and the city's weekly account have no line there and stay in the mod's own books (Finance).

The city's cost of living adjusts the game's own rent and wages, and on a line of its own the game's shops and services at prices of its own (`CityGame.LivingTypes`: tickets, rides, the hospital and the doctor, gyms and entrance fees, shopping, fuel and stalls, fit-outs, training and hiring).

A charge under rent or salaries stays on those lines; a purchase at a wholesaler's is left out, as the city writes those goods' prices at today's; and the mod's refunds of a game charge (`EconomyBridges.GivenBack`: a ride the day pass covered, a lent car's ticket) count against it.
`CityGame.DayFigures` passes over the mod's own transactions, which are at today's prices already.

Each of the city's lines is booked where the game's own figures were (`DayLine`: by address, by the category the statement keeps them under, and deductible where the game's tax deducts them), shared out by those figures in whole money that always sums to the line (`CityAdjust.Split`; a carried amount goes with the day it is taken).
A line's refund shares are booked before its charges, so the cash they bring pays the charges beside them (`CityAdjust.BookParts`).
The game's tax counts charges alone, so a refund of rent or wages lowers the deduction the game kept under the name of the charges it adjusts (`CityGame.LowerDeduction`).

`GameBooks.Refresh` is called by the city once its lines are booked and again when the mod's day is done.
It sums the day's statement as the game sums it: a business's four lines by category, and its takings as the game's sales and the city's line on them.

The goods sold are scaled row by row so that they still add up; where the game booked no takings of its own, the takings are the sales of its order book (a factory's exports).

The mod's own takings go there too: online orders less refunds (`OnlineGame` putting each product's sales among the rows itself), the estate agency's commissions at its office, and the broadband company's bills at the headquarters (`EconomyBridges.Takings`, with its network as a row among the goods, `EconomyBridges.CostRow`).

Every sum is made afresh from what the save keeps and never from the statement itself, so that a second refresh counts nothing twice and loses nothing entered between the two.

A rented home's rent as its daily rent and the city's line on it, and the day's own figures (loans, health insurance, the headhunter, parking, salary income, and the wages of no business with the city's line on them).
So the game's taxes (sales of the period from the statements, deductions as booked), its bank (`PlayerHelper.CalculateDailyIncome`) and EconoView see the city's lines.

Readers of a statement take its takings as today's: an insurance premium's base is at today's prices and its share of the takings is not raised again (`CoverRules.Premium`); the rota planner, which reads the order history (the game's own prices), raises a customer's worth by the city's takings factor (`CityEconomyDirector.TakingsFactor`).

The property business's hotels and car hire are in no statement (a hotel is held in the mod's books, the hire lot is no business of the game's), so they pay their own tax when the game's tax year turns, on the same day as the game's: its rate on the year's takings less their running costs, nothing on a loss, and nothing while the year's business income in all is under the game's threshold (`EstateRules.TradingTax`, `EstateDirector.TradingYear`).

Their running costs are counted there and are not marked deductible, so nothing is deducted twice. The figures the Property app gives for this year (a building's rent and costs, a hotel's and the hire's takings and costs) start again on that same day (`EstateRules.YearTurned`).

## A building sold that comes back (`Economy/Estate/EstateRules.Sold.cs`, `EstateGame.Sold.cs`)

A sale (the game's own to a competitor, `RealEstateHelper.SimulateCompetitorBuyingPlayerBuildings`, or the agency's) takes the building out of the game's list of the player's (`GameInstance.realEstate`) and off the market, and gives it a rival owner. Another mod that keeps buildings of its own in its own save data can put the very same entry back as a save loads, with its old purchase day and its listing (a player's report, 18 and 20 Oct 2026: an underground car park sold for 3,000,000 was the player's and on the market again at every load, and sold again for the same price).

The property business now remembers each sale (`EstateState.Sold`, part `s` of its save line, version 3): when a building leaves the player's list and the game's books have its sale within the days they keep (`ba:transaction_buildingsold`, since that ownership's purchase), and when the agency completes one. As a save is read (before the buildings are read as the player's, with the game's books searched for sales of buildings held again from before them, for a save that came back that way earlier), each game hour (which the game runs first in its new day, before its daily run could sell it again) and each morning, a building remembered as sold that is the player's again is judged by `EstateRules.SoldBack`: bought before the sale's day and with no purchase of it in the game's books since (`ba:transaction_buildingbought`), it is the ownership that was sold and goes back to its buyer as the sale left it (`EstateGame.Release`, the same as a sale: its owner, the market rent on a unit the player still uses, off the player's list and off the market); bought on the sale's day or after, it is the player's and the sale is forgotten. No money moves either way. The player is told once a sale, in the game's notice that opens the Property app; one `Estate:` line says what was done. Nothing of it runs in a Going Public session, where who owns what is the host's record, and a house in the Hamptons is left to the game.

