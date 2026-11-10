# Suppliers and contracts

## In a sentence

Under the living economy's master option **Living population**, with **Suppliers and contracts** on, nine suppliers that are firms of the city bring stock to the player's businesses beside the game's own wholesale stores: each has its own price, reliability, days to deliver, smallest order and way with a customer who pays late; a price can be fixed for 2, 4 or 8 weeks under take or pay; deliveries come late or short for real causes, told the day before, with a remedy when the supplier itself is at fault; a buyer follows rules the player sets; and the city's rival shops and the rival tycoons' outlets buy from the same suppliers by the same rules.

## Where the code is

- Folder: [`Scripts/Suppliers`](../../Assets/Mods/RealLifeOverhaul/Scripts/Suppliers/) (namespace `RealLifeOverhaul.Suppliers`). The kit's page is `Scripts/Ui/Ventures/SupplyPages.cs` (game-free) and `SupplyLive.cs`; the wiring is `Scripts/Wiring/Wiring.SupplierLinks.cs`.
- Option: "Suppliers and contracts" (`living_suppliers`, on by default, under `living_population`). Off, or with Living population off: nothing is delivered or charged, the app leaves the phone, and the line in the save stays as it was. The game's own wholesale stores, its weekly delivery contracts and its importers are never touched, on or off.
- Checks: EconomyCheck `--suppliers` (`SupplierChecks.cs`).

```
SupplierCatalogue            every figure, each with its unit and its source; the nine suppliers
SupplierState, SupplierStore the records and the save line reallife:suppliers (version 1)
SupplierRules                game-free, pure: the seeded draws, a fixed price, how a supplier judges a customer, how a delivery is judged to come, the remedy,
                             take or pay and the settlement, a supplier's own day, a rival shop's day
SupplierEngine               game-free: an offer, a signing, the hour's deliveries, the day, what is owed, a contract's end, the buyer's rules, the rivals;
                             ISupplierWorld is the world it works in (the game's side, or the checks' own)
SupplyFacts                  what the pages read; the tables of locale keys
CompanyDirector.SupplyBuyer   (Economy/Company) whether a manager decides about stock at a place today, and that manager's budget for a contract's first delivery
SupplierGame                 the one file that touches the game: the clock, cash through the mod's booking call, prices, places, goods, the city's shops, notices
SupplierBridges              what it asks of other features (Wiring.SupplierLinks fills them)
SupplierDirector (+ parts)   the timer (a second), the hour, the day, the rival shops a slice a second, facts, tests, the app
SupplierApp                  the app in the game's own frame; the kit's page is Ui/Ventures/SupplyPages
SupplierText, SupplierGuide  the words built from the state; the feature's entry, its options and its Guide page's figures
```

## What the game does

- A business buys stock from the wholesale stores' shelves (`PlayerItemPurchaser`), by a weekly delivery contract with one wholesale store set in BizMan (`DeliveryContract`, delivered on Mondays at 08:00 by `BusinessHelper.HandleWholesaleDeliveries`, with a delivery fee and the building's price index), or through a purchasing agent's plan with an importer for a warehouse (`ImportPartnership`).
- A product's wholesale price is worked out each time it is asked (`ItemHelper.GetWholesalePrice`), and the game's own market events raise it (a shortage 1.25, a backorder 1.5, `ProductMarketHelper.GetProductMarketEventMultiplier`); on backorder a product is not delivered at all. A product has a weekly limit (`Item.maxWholesaleOrderAmount`) unless the save switches limits off. Its price index (`ProductMarketEntry.importPriceIndex`, 0.5 to 1.3) is re-rolled for every product every day with an unseeded roll, on any change of business and at every export (`ItemHelper.GetWholesalePrice`, `CompetitionHelper.RunDaily`, `ProductMarketHelper`); **a supplier of the mod's never reads it** (below), so loading a save again for a low day finds nothing at a supplier. The game's own wholesale stores keep the roll as they have always had it.
- Goods can be exported from a warehouse at `GetWholesalePrice x gameVariables.exportMultiplier` (0.65 unless a custom game changes it).
- The mod's city economy ([Economy: the city](economy-city.md)) writes each product's wholesale price by the factor of its kind of goods (six kinds: the season, the weather, a city event, a shortage or a glut).

## The suppliers

Nine, three for each kind of goods (`SupplierCatalogue.Firms`). A level is thousandths of the market's price of the day; reliability is thousandths of deliveries whole and on the day in calm conditions; the smallest order is whole money at the start's prices and follows the city's price level.

| Id | Name | Kinds | Level | Reliability | Days | Smallest order | Late payment | Owner | Health rests at |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Kettle and Crane Provisions | fresh, groceries | 1030 | 975 | 1 | 300 | lenient | its own | 800 |
| 2 | Penny Barrow Cash and Carry | fresh, groceries, general | 950 | 900 | 3 | 1,500 | strict | tycoon 0 | 600 |
| 3 | Three Lanterns Grocers Supply | fresh, groceries | 990 | 945 | 2 | 700 | firm | its own | 700 |
| 4 | Bolt and Bobbin Cloth Hall | clothing | 945 | 905 | 3 | 1,800 | strict | its own | 600 |
| 5 | Silver Thimble Imports | clothing, general | 1025 | 970 | 2 | 500 | lenient | tycoon 1 | 800 |
| 6 | Loom and Ledger Trading | clothing, home | 985 | 940 | 2 | 900 | firm | its own | 700 |
| 7 | Copper Coil Wholesale | electronics, home | 950 | 900 | 4 | 2,500 | strict | its own | 600 |
| 8 | Northlight Tech Distribution | electronics | 1030 | 975 | 2 | 800 | lenient | tycoon 2 | 800 |
| 9 | Oak and Ink Home and Office | electronics, home, general | 990 | 945 | 2 | 1,000 | firm | its own | 700 |

They are firms, not a menu:

- **A place.** Each supplier's depot stands by one of the city's own wholesale stores or importers, a seeded pick made once a save (`SupplierDirector.Depots`). A business more than 2,500 m from it on the map waits a day longer for a first delivery (`SupplierCatalogue.FarMetres`, `SupplierGame.FarDays`).
- **An owner.** Three belong to the rival tycoons' holdings: 80 thousandths of what such a supplier takes from the player (`OwnerShare`, its margin) is added to that tycoon's worth, while the rivals are forward and no session keeps them at rest. A supplier that went under and came back is nobody's tycoon's any more.
- **Books of its own** (`SupplierRules.FirmDay`, once a day). Health runs from 0 to 1000. It rests at the supplier's own figure plus 3 for each shop of the city it serves (150 at most) plus 25 for each step it has grown, 950 at most. Under its rest it recovers 4 a day, and 1 more for each 10 shops it serves (4 more at most); over it, it loses 2 a day. Its book of fixed prices follows the market slowly (a 28th of the gap a day, never under the calm price), and 400 thousandths of its trade is on fixed prices: a market over the book costs it a point a day for each 10 thousandths of gap on that share, a market under it helps half as much. The city's cycle moves it by +2, 0, -2 or -4 a day (boom, steady, slowdown, recession). A strike or a fire at the depot starts by a seeded draw each day with a chance of its unreliability over 10 (1000 less its reliability, over 10, in whole thousandths: 2 to 10), runs 3 to 9 days and costs 15 a day.
- **Going under and growing.** At health 0 it goes under: its contracts end with nothing to pay, it is shut for 28 days, then new owners open it at health 500 and its first size. At health 850 or more for 30 days running it grows a step (three sizes): each step is 5 thousandths off its level and 10 thousandths more reliability (995 at most).
- Under health 400 its money trouble shows in its deliveries.

The city's wholesale stores are the first of the choices on the page, as they are: nothing of theirs is changed.

## A contract

1. **The goods.** A place's goods are what it sells: for a shop, the goods of its business type it stocks or sold, each at the units its tills sold in the last seven days (`BuildingRegistration.orderHistory`; a box where it has sold none yet); for a warehouse, the goods a purchasing agent's plan brings it, at the plan's amounts. An offer holds those of the kinds the supplier trades, at a share of that week (50, 75, 100, 125 or 150 per cent), in whole boxes, inside the game's weekly limit. A contract holds 12 products at most: **the ones the place sells most come first** (units at the day's price, ties by name, so the same on every machine), and the Offers page says how many were left out (`Quote.LeftOut`). Raw materials the exchange prices are never a supplier's ([futures](economy-markets.md)).
2. **The term.** One delivery at the day's price ("Order once"), or a price fixed for 2, 4 or 8 weekly deliveries. The first delivery is the supplier's days from signing (a day more for a far place); the rest follow every seven days at 08:00. One fixed price contract a place and supplier; 24 contracts running at most.
3. **The price.** A supplier quotes its **list price** of a product: the item's own wholesale price (the one the city's economy writes by its kind's factor) times the game's market-price multiplier (`SupplierRules.ListMills`), and for the price of the day the whole market's event on it (a shortage 1.25, a backorder 1.5). It never holds the importers' daily price index, which is re-rolled unseeded: so neither a one-off order nor a fixed price can be improved by loading again or watching a product for a low day (the game's own wholesale stores keep that roll). For one delivery: the price of the day times the supplier's level. For a fixed price: the **calm** price (the list price without the day's push on its kind) times the term's factor, the supplier's level and what the customer's standing takes off, rounded up to the mill (`SupplierRules.FixedMills`). The term's factor is 1000 plus the size of a shortage that is running or announced, for the share of the term's days it covers (`TermFactor`); a glut or a cheap season is never passed on. So in a calm market a fixed price is under the supplier's own price of the day, and it cannot be written at a momentary low: over 400 days of the importers' roll the offers do not move (a world that took the index moves them by more than 300 per mille, which the check would fail on).
4. **What the customer's standing does** (`SupplierRules.Discount`, thousandths off): the term 20, 35 or 50; a delivery worth 4 smallest orders 10, worth 10 of them 20; a credit rating of 740 or more 10, under 580 minus 15 (no rating reads as 640); each late payment in the last 60 days minus 10, minus 40 at most.
5. **Whether the supplier offers at all** (`SupplierRules.Refuse`): not while it is shut; not while the customer owes any supplier money; not for 90 days after a contract with it ended unpaid; not under its smallest order; a strict supplier not under a rating of 580, a firm one not under 500, a lenient one always.
6. **Nothing is paid at signing.** Every delivery is paid as it comes, through `BizBooks.Book` at the business, deducted from the game's tax under the supplier's name as the game deducts a wholesale store's. The keys are `reallife_transaction_supply_deliverycontract` (a delivery), `..._deliverycontract_owed` (a payment of what was owed), `reallife_transaction_supply_settlement` and `reallife_transaction_supply_credit`; the first two hold the game's own word for a delivery contract, so the Finance app's cash flow reads them as stock (`CashCategory`). A payment is made only from the cash over what the investors of the player's firms are owed (Tech's firm account, [startup money](startup-money.md)): it can never count as misuse of a firm's money or take a firm under its floor, and what is owed is booked at the contract's own business, never with no place.

## Take or pay

The one rule of a contract's burden: **every delivery is paid for in full when it comes, whether the place has room or not.** What finds no room stays with the supplier and never enters the game (`SupplierEngine.Deliver`). So a fixed price is a protection when the market rises and a burden when it falls or the shop sells less.

- **Ending early** costs the settlement: 250 thousandths of the value of the deliveries still to come, rounded up, less any credit the supplier owes on the contract (`SupplierRules.Settlement`, `SupplierEngine.BreakCost`). The game asks first.
- **A unit is charged** at the agreed price and never under what the product fetches on export that day, held to the market's price where export would pay more than the market (`SupplierRules.ChargeMills`).

## Late and short deliveries

How a delivery comes is judged once, the day before it is due, by `SupplierRules.Judge`: a draw from the save's seed, the supplier and the due day alone, against a chance made of real causes (thousandths, 900 at most together):

| Cause | Chance added | What happens | The supplier's fault |
| --- | --- | --- | --- |
| its own unreliability | 1000 less its reliability | 1 or 2 days late, or 600 to 900 thousandths brought | yes |
| a strike or a fire at its depot | 300 while it runs | 2 or 3 days late, or 400 to 700 brought | yes |
| its money trouble | half a thousandth for each point of health under 400 | 400 to 800 brought | yes |
| the weather of the due day | ice 250, a cold snap 80, a storm 80 (the seasons' summer storm days, of the save's seed), a heat wave 120 for fresh goods | a day late (ice two); in a heat wave fresh goods 700 brought | no |
| a city event in the streets | 100 (a parade, a protest, a fun run, a film shoot, fireworks) | a day late | no |
| fresh goods in winter | 50 | 700 to 900 of the fresh goods brought | no |
| a market shortage of a kind it brings | half the shortage's size | that kind rationed: 1000 less the size brought | no |

- The draw is the same for every customer of the supplier that day, the same however often it is asked, and the same whenever the contract was signed: ordering later or loading the save again changes nothing.
- **The weather of a day is what the mod honestly knows of it:** the spells (`Weather.SpellOf`) and the seasons' storm days (`SeasonLookRules.RainOn`) are both drawn from the save's own seed, so a delivery judged the day before, a rival shop's delivery and a load on another machine agree. The live sky is never read (a storm raging today is no forecast; `SupplierRules.Forecast`). With the weather option or the seasons off there is no storm.
- **The customer is told the day before** (a notice, and the contract's page): how late or how short, and why. They can order once from another supplier, buy from a wholesale store, or leave the shelf.
- A late delivery comes whole on its later day and the weeks after it are not moved. A short one is paid only for what comes. A product the game has on backorder across the whole market is not brought and not charged.
- **The remedy**, when the cause is the supplier's own and the customer did not sign knowing of its trouble: a credit of 100 thousandths of the value a short delivery did not bring, or the same 100 thousandths of the share of the week's goods a late delivery missed (14.3 thousandths of its value a day, 100 at most for seven days), so a delivery that comes whole is never credited more than the same days short would be (`SupplierRules.LateCredit`). It is set against the payment at once and carried where there is nothing to set it against; paid out when the contract ends (or by the next day's pass if a fault kept it), and lost, said, if the supplier has gone under. After 3 such failures in a contract the customer may leave with nothing to pay. **A contract signed while the supplier was in trouble (a strike or a fire at its depot) or short of money (health under 400) earns no credit and no failure from it** (`Contract.Knew`, set at signing): the customer saw it, so there is no rebate for signing into it. The notice the day before says so.

## If the customer cannot pay

Never a silent debt: what is owed is a list in the save, on the page and in a notice.

| Supplier | On the day | Days to pay | Fee |
| --- | --- | --- | --- |
| lenient | delivers on account | 7 | none |
| firm | delivers on account | 4 | 20 thousandths, once |
| strict | brings nothing and charges nothing for what it kept; after 2 such deliveries it ends the contract | 2 for a settlement | 50 thousandths on a settlement left unpaid (no delivery is ever on its account) |

- What is owed is taken from the cash over the investors' floor as far as it goes, the very day there is some (`SupplierEngine.Collect`), oldest first, booked at the contract's business; the page has a button for it too. **The fee of a supplier's late policy is added once to every sum put on its list, a delivery on account and a settlement alike** (`SupplierRules.WithFee`); so a strict supplier's 50 thousandths is charged on the settlement of a contract it ended or one the customer ended without the cash.
- **A bound on what is owed in all:** a supplier delivers on account only while the whole of what the customer owes suppliers, with this delivery and its fee, stays within 10,000 at the start's prices (`SupplierCatalogue.OwedMost`, following the city's price level). Past it a lenient or a firm supplier does as a strict one does: brings nothing, charges nothing for what it kept, and ends the contract after 2 such deliveries. So 24 contracts of 12 products with no cash behind them can never run up the list without bound. The save keeps 64 lines owed; past them a sum joins the oldest line of its contract, and the reader takes every line there is.
- Unpaid past its days, a delivery's price ends the contract: the settlement is owed on top (with the fee), the record counts a default and that supplier offers nothing for 90 days. **A settlement left unpaid past its days does the same to the record and the supplier's memory**, once (`OwedLine.Acted`); the contract it belongs to is over already, so there is nothing more to end. A debt is acted on once whatever its sum, and never grows by itself.
- While anything is owed no supplier offers terms. A debt is never dropped and never grows by itself.
- **A business closed, sold or no longer the player's:** its contracts end on the next day's pass (or at the delivery, whichever comes first), nothing more is brought, and the settlement is paid from the cash or, without it, owed and said.
- **A supplier gone under:** its contracts end with nothing to pay.

## The buyer

A contract has four rules (`Contract.Renew`, `RenewLimit`, `Cover`, `LeaveRule`), set on its page:

- **When the term ends:** ask me; renew with the same supplier; or take the cheapest offer among the suppliers that trade everything the contract brought (the offer is held to those kinds of goods).
- **Renew only up to:** the market's price, 5, 10 or 20 per cent over it, or any price. An offer over the limit is not taken and the player is told.
- **Cover a late or short delivery:** one delivery at the day's price from the quickest other supplier, for the units missing (a late delivery: the days' share of the week), in whole boxes.
- **Leave and switch** when the right to leave arises.

The rules run only where somebody who orders stock **decides today** (`ISupplierWorld.Buyer`): a manager of the mod's with stock orders on ([managers](economy-managers.md)) who is at work, or the district's regional manager standing in for one off sick (`CompanyDirector.DecidesStock`, which asks the managers' own `Decider`: a manager off sick with nobody to stand in decides nothing that day), or for a warehouse a purchasing agent of the game's whose plan brings goods there and who is at work. Without one the player is told 3 days before a term's last delivery and the day before a failed one.

**One gate for every order the buyer places** (a renewal, a switch after the right to leave, a cover; `SupplierEngine.Allowed`): somebody decides there today; the offer is inside the price limit of the contract's rules (the limit applies to all three, not only the renewal; a switch or cover over it is not made and the customer is told); the cash over the investors' floor covers what is already promised (the running contracts' deliveries and anything owed) and this offer's first delivery; and the manager's budget for the month, the one that limits their overtime and hires, has room for that first delivery, which it then spends (`CompanyDirector.SpendStock`). A purchasing agent has no budget of the mod's. The buyer's rules are copied into the new contract before its first delivery is judged, so a renewal or a cover whose delivery is tomorrow is covered at once if the draw says it fails. The player's own signing is not gated: it is their choice.

The company's manager who orders stock follows the contracts too: the week's order of the game's own delivery contract is smaller by what the supply contracts bring in the week it is for, and what they brought counts as come when the use is worked out (`ManagerRules.LessComing`, `WithSupply`).

What the manager's order counts as coming is what the contracts honestly expect to bring, each delivery once (`SupplierState.Coming`): none of a product on backorder, none from a supplier gone under, none from a strict supplier when the cash in hand does not pay the delivery, the share a short delivery will bring, a late one on the day it will come; a cover bought for what a failed delivery left missing is a delivery of its own and is counted beside it.

## The rivals

- **The city's rival shops** (the game's own businesses with a rival owner, as the game values their day): once a day from 01:00, 40 a second, each shop that sells goods buys its kind from a supplier of its own choosing (`SupplierRules.PickFirm`: the best of price, record and a draw of the shop's own among those open). 600 thousandths of them hold a fixed price, written at the supplier's book; the rest pay the day's price. Half a shop's day is the cost of its goods. **What comes off is what the shop paid against the price its day was valued at**: the game's valuation of a rival shop has deducted the day's wholesale price (the kind's factor) already (`EstimatedWeeklyIncomeHelper`), so a shop that buys at the wholesale stores pays nothing more, a shop on the day's price at a supplier pays only that supplier's level against it, and a shop on a fixed price gains or loses the difference between the market it was valued at and its book: the market's push is never counted twice and a contract shop has no standing subsidy (`SupplierRules.Rival`). On its delivery day of the week its supplier's delivery is judged by the very draw the player's is, and a late one costs it 60 thousandths of the day a day, one that brings nothing 300 (less in proportion). Never more than 150 thousandths of its day either way. The cents come off (or go on) the shop's last valued day (`BuildingRegistration.dailyIncomes`), which the game reads its rival's income, worth and closing from. **The entry charged is remembered for each shop** (the game's count of valued days and the bits of the last, saved with the line): after a load in the hour the game's valuation was part done, a shop still on the day before keeps the charge it has and is not charged twice. A day under way is finished before a save is written. The per shop loop makes no string and allocates nothing (checked).
- **The rival tycoons' outlets** ([rivals](../reference/wiring.md)): each buys 300 a day of goods at the start's prices by the same rule, out of the tycoon's worth (their day is valued at calm prices, so the market's push does cost them) (never under their floor); the month's sum and the failures are a line on the rich list and the tycoon's panel.
- **The paper:** a supplier's strike or fire, one gone under, back or grown, and the city's shops left short by a failure are pieces of the morning's City Ledger for 3 days.
- A supplier's customers are counted each day and feed its own books.

## Where the player finds it

- **BizMan, Deliveries** (where stock is bought today, on the RLO interface): a fact with the business's supply contracts that opens the Suppliers app on that business, and a button to it where the business has no delivery contract yet.
- **The Suppliers app** (last in the Business folder), three tabs: **Offers** (the place, the wholesale stores and the suppliers that trade what it sells, a supplier's character and its offer for the chosen term and quantity, Sign the contract or Order once), **Contracts** (each contract, its next delivery and how it will come, its price against the market, what is owed, its rules, End it, Leave, Pay what is owed) and **The market** (each kind's price against calm, a shortage or a glut, the payment record, the suppliers' news, the rivals).
- **A shelf's overlay:** the next delivery of a product counts a supply contract's.

## Going Public

Only places `GoingPublic.Mine` calls the player's own are read or delivered to, and every payment is the player's own cash. The suppliers' state is this save's and drawn from its own seed by a fixed hash; nothing is sent. A guest leaves the city's rival shops to the host's books; the tycoons rest in a session, and no tycoon's worth moves.

## The save

`reallife:suppliers`, version 1: the seed and the last day counted, each supplier (health, size, trouble, shut and reopening days, its book by kind, its customers, its depot), the contracts with their products and prices, the delivery judged for tomorrow, the rules and whether the supplier was in trouble when it was signed, what is owed (with whether it was acted on), the payment record, the tycoons' sums, the paper's notes, fourteen days of deliveries and, for each rival shop, the entry of its valued days that was charged last. A save without it loads with nothing in it; a newer line is read as far as it goes; a figure out of bounds is put inside them. **The reader never drops a contract that is running or has money owed on it, nor a line owed**: only the oldest ended contracts nobody owes, past the 12 the page keeps, are let go. Numbers are written and read in the invariant culture.

After a pause (the option off, a save from long ago) a delivery whose day passed is neither brought nor charged and each contract goes on at its next weekday (`SupplierEngine.Resume`): the term is counted in deliveries, so nothing is gained or lost by the pause.

**A time skip of several days** (a long sleep, a journey) is played day by day (`SupplierEngine.CatchUp`): each elapsed day runs the suppliers' books, the day's judgement and its 08:00 delivery hour in turn, so the suppliers' health, the 28 days shut, the strike draws, the collection of what is owed and the judgement of the day ahead do not depend on how the player slept. At most 14 days are played; the days before them are a pause, as above. A load after a pause plays none (`SupplierRules.PlayFrom`). The rival shops' day is once a day as the game values it.

**A delivery and its payment move together** (`SupplierEngine.Deliver`): the payment is followed at once, with no call of the world between, by the books of it and the contract's step to its next delivery; the goods are put down after, and a fault in the game's delivery call (or in the buyer's renewal after the last delivery) leaves the contract stepped and paid, so a load after it never charges the delivery again.

## Exploits closed

| Tried | Closed by | Check |
| --- | --- | --- |
| Reselling contract goods on export when the market has risen | a unit is never charged under what it fetches on export that day | "suppliers exploit (resale)" |
| Signing and breaking in a loop | nothing is gained at signing; breaking costs the settlement every time | "suppliers exploit (loop)" |
| Locking a glut's low price | a fixed price is written from the calm price; an announced shortage is priced in already; a term is 8 weeks at most | "suppliers exploit (a low locked)" |
| Keeping the shelves full so nothing is taken | take or pay | "suppliers exploit (quantities bind)" |
| Farming credits | a credit is a tenth of what a short delivery did not bring, or of the share of the week a late one missed; none for a failure the customer saw coming | "suppliers exploit (credits, short)", "(credits, late)" |
| Switching the option off to stretch a term | a term is counted in deliveries | "suppliers exploit (the pause)" |
| Loading again for a better delivery | the draw is of the seed, the supplier and the due day | "suppliers judge: the delivery of a day is judged the same ..." |
| Stacking contracts at one price | one fixed price contract a place and supplier | "suppliers offer: a place has one fixed price contract ..." |
| Locking a price on the importers' lowest day of their daily roll (or loading again until it is low) | a supplier's price never holds the importers' index; the game side prices from the item's own wholesale price | "suppliers price: over four hundred days ..." and "... never from the game's GetWholesalePrice()" |
| Running up what is owed (24 contracts of 12 products, no cash, a lenient supplier) | the buyer's gate asks the cash over the floor and the budget; a supplier delivers on account only up to 10,000 in all | "suppliers gate: with no cash 24 contracts ..." |
| Signing with a supplier on strike for the credits and the exit | a failure the customer saw coming earns neither | "suppliers remedy: signing with a supplier visibly on strike ..." |
| A rival shop charged twice on one valued day | the entry charged is remembered | "suppliers rivals: after a save and a load in the middle of the game's valuation ..." |
| Sleeping through days to skip a supplier's strike or a day's judgement | each elapsed day is played | "suppliers time skip: ten days slept through ..." |
| Stock bought cheap moved or sold at more than it cost | none found: see below | "suppliers exploit (resale)" |

## For car showroom supply

The supplier model is by kind of goods and by product name (`SupplierDef.Kinds`, `ContractLine.Item`), with the world behind `ISupplierWorld`. A feature that wants a supplier for each type of car can add suppliers to the catalogue with a kind of its own and hand the engine a world whose `Wants`, `Market` and `Deliver` speak of cars: offers, take or pay, the judging, the remedy and what is owed need no change.

## Cost

Nothing per frame and no Update. Once a second a few comparisons. Once a game hour one pass over the running contracts (24 at most). Once a game day the nine suppliers' books, the contracts and the owed list. Once a day, 40 rival shops a second, each a few hashes and no string or other allocation. A page is filled when it is written (a press or a changed revision). Measured in the checks (`suppliers cost`): with all 24 contracts of 12 products due on one morning the day's pass takes about 0.01 ms and the 08:00 delivery pass about 0.06 ms on the development machine (the check allows 20 ms), and the rival shops' pass takes about a microsecond a shop and allocates 0 bytes over 10,000 shops.

## Tests

Options, Tests, under Suppliers and contracts: say how every supplier stands; sign the cheapest short contract for my first business; bring every contract's next delivery now; make my first contract's next delivery late, or short; send a supplier under; run the rival shops' supply day now; put suppliers back as before. Each writes `Suppliers check (...)` lines.

## Not made, and why

- **A quantity a month.** The city's month is `daysPerYear / 12` days, 5 with the game's 60-day year (`MacroCatalogue.MonthDays`), shorter than the game's own delivery week (`DeliveryHelper`: every Monday), so a contract's rhythm is the week and its term is counted in weeks.
- **The game's own wholesale stores as contract partners of the mod.** Their weekly delivery contract is the game's own screen and save data (`DeliveryContract`); they stay as they are and are the first choice on the page.
- **A Flare post about a supplier going under or a shortage.** Flare's day posts are built from `FlareCityFacts` (`Social/Flare/FlareLiveModel.cs`: rain, the news bits, the price trend, the street events, the council's works and policy, the market's mover), each of its topics an enumerated `FlareTalk` or `FlareOfficialPost` (`Social/Flare/FlareLiveRules.cs`) with texts for every persona in every language. Nothing in them carries a supplier's doing, and a topic of its own is a new post family with its texts, outside this feature. Where the city talks about the market the shortage is already in `PriceTrend`; a supplier's strike, fire, collapse or return is shown where the rivals' doings show for this feature: the City Ledger for 3 days, the Suppliers app's market page, the tycoons' lines on the rich list and their panel.

## Not verified in the game

The app in both frames and its place in the Business folder; BizMan's fact and button; that goods a supplier brings show on shelves and in storage as the game's own deliveries do (and that a fault in the game's delivery call leaves what was put down in place); the tax statement's line under a supplier's name; the notices, among them the two that say no credit is due; the rich list's and the paper's lines; the rival shops' pass on a late save, and that the game's valuation leaves the entry charged as it was written (`dailyIncomes` last value); the manager's budget and `Decider` as `CompanyDirector.DecidesStock` and `SpendStock` read them; a time skip across several real days of play; the seasons' storm days against the sky they are drawn for.

## Things looked at and found sound

- **A business's sale value and moving goods between the player's businesses.** A sale values the stock at the cargo's own unit price times the game's selling multiplier (`ItemHelper.GetSellingPrice(CargoInstance)` = `pricePerUnit * amount * sellingMultiplier`, 0.75 on Normal; `ServicesGame.Fixtures`), and the unit price a supplier's goods carry is the price the customer was charged (`SupplierGame.Deliver`: `CargoInstance(item, units, mills / 1000f)`). The game's logistics move that cargo with its price (`LogisticsManagerPlanDestination.ProcessStockTarget` withdraws it from the warehouse's cargo and delivers it on). So goods bought cheap are worth at most 0.75 of what they cost in any business of the player's, and wherever they are. What a unit fetches beyond that is the game's export (`ProductMarketHelper.GetProductExportPrice` = wholesale price times the export multiplier, 0.65): charging never under it on the day of delivery closes the loop on the day; a later market spike lets stock bought on a fixed price earn what the game's own wholesale purchase would have earned, bounded by the game's weekly import and export limits, which is the property of the game's market, not of the contract.
- **The game's own BizMan frame (the RLO interface off).** Nothing of this feature is drawn in it: BizMan's supply fact, its button on the Deliveries page and the page's link are parts of the RLO BizMan screen (`Ui/BizMan/BizManScreen.Parts.cs`, `.Press.cs`), which only exists with the interface on. With it off the way in is the Suppliers app on the phone, built in the game's own frame (`SupplierApp`), which has all three tabs, the offers' rows and notes (including the products left out) and every press the kit's page has. A shelf's overlay line for the next delivery (`OverlayFactsLive`) is the mod's overlay and is shown only where the mod draws overlays.
