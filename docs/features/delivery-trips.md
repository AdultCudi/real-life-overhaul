# Delivery trips and food spoilage

## In a sentence

Under the living economy's master option **Living population**, with **Delivery trips** on, the player's logistics routes run as delivery trips (a vehicle and its driver from the route's warehouse at each of its departure times, round its stops and back, the goods delivered on arrival by the game's own delivery), and with **Food spoilage** on, fresh goods at the player's places keep so many days by their kind, longer when kept cold (inside a fridge or a freezer for the few goods the game gives a cold shelf, by the fridges and freezers standing at a shop, warehouse or factory for all the rest), the oldest sold first; the city's rival shops pay the same supply and spoilage in their books.

## Where the code is

- Folder: [`Scripts/Economy/Logistics`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Logistics/); the pages in `Scripts/Ui/BizMan/Ops/BizManScreen.Trips.cs` (with lines in `BizManScreen.Managers.cs` and `BizManScreen.Plant.cs`); the roads in `Scripts/Wiring/Wiring.LogisticsLinks.cs`; the game's pieces in `Scripts/Core/GameContract.Logistics.cs`; the managers' smaller orders in `Scripts/Economy/Company/CompanyDirector.ManagerDecisions.cs`.
- Options: "Delivery trips" (`living_trips`) and "Food spoilage" (`living_spoilage`), both under "Living population" (`living_population`), both on by default. Off, the game runs every route at once at 02:00 and nothing spoils, as before.
- Option: "Food shelf life (% of the usual time)" (`living_spoilage_pace`, `Settings.LivingSpoilagePace`), a slider from 25 to 400 per cent, 100 by default, on the same section of the options page as the two switches and greyed with "Food spoilage" (the old pages and the kit draw the one row of `Meta/OptionsTree.cs`). There is no second way to switch spoilage off.

```
Economy/Logistics/              the first six are game-free and compiled by EconomyCheck (LogisticsChecks.cs)
  TripRules                       departures, the timeline, drive time by vehicle, fuel, wages, whole boxes, stops shared over the slots, the rivals' supply
  TripState                       a route's departures, a trip and its stops, a run of the log
  TripStore                       the save's line, "reallife:trips"
  SpoilRules                      a product's kind, its shelf life out of and in cold storage, batches, oldest first, expiry (with a cold share), the managers' cap, the rivals' loss
  SpoilState                      a place's batches and its seven days
  SpoilStore                      the save's line, "reallife:spoilage"
  LogisticsGame                   the plans, the hold, the slots, a stop's needs, the game's own delivery, the city's places, the pump's price, the fuel
  LogisticsGame.Spoil             the places, what spoils there and what their fridges keep cold, throwing out by the game's own calls, the rivals' valued days
  LogisticsBridges                the road between two places (the GPS's graph)
  LogisticsDirector               the start, the second, the hold of the game's run, the save
  LogisticsDirector.Trips         departures, a trip leaving, arriving and back, roads, what the pages read
  LogisticsDirector.Spoil         a place a second, the turn of the day, the notice, goods moved by a trip, what the pages read
  LogisticsDirector.Give          food that would spoil given to charity in the evening, the overnight words
  FoodGiftRules                   what is given, what earns, the ceilings (game-free)
  LogisticsDirector.Rivals        the rival shops' books once a day
  LogisticsDirector.Tests         the two tests
  LogisticsGuide                  the feature's entry and the Guide page's figures
```

## Trips

1. **The hold.** In the economy's hour (inside the game's own, before its run of every route at 02:00, `GameManager.RunMainGameTick`), each route of the player's whose warehouse and stops are all theirs has its stops swapped for none, so the game's run sends nothing; the game's end of the hour (`ba:gameevent_newhour`) puts them back in the same frame. The second after and the save put them back too, should that end not come.
2. **Departures.** Each route keeps its first run, its runs a day (1 to 4) and the hours between (2 to 12), set on its page. A route not set leaves at 02:00, and at 14:00 too when it carries goods that keep a week or less. Departures missed while the trips were not running are not made up.
3. **A trip.** At a departure every slot of the warehouse with a vehicle and a driver at work takes its share of the route's stops (each vehicle's own number, the manager's skill's extra from the first slot on, as `LogisticsManagerPlan.CalculateMaxDestinations` counts them). It loads for 15 minutes, takes what each stop needs (its target less what it holds, an exporter's whole target, as the game reckons it) of the warehouse's goods not already on the road, in whole boxes until the vehicle's room is full, drives each leg at the vehicle's pace (vans 22 km/h through the city, trucks 18, cars 26), unloads 10 minutes at each stop and drives back. A vehicle still out leaves when it is back.
4. **Roads.** A leg is measured once on the GPS's road graph, a slice of a search a second on a search of its own; until then it is the straight line and 35 per cent.
5. **Arrival.** The goods stay in the warehouse until the stop is reached; there the stop's targets are set for the moment to what it holds and the trip's load, the game's own `DeliverDestination` delivers them (shelves first, then storage, an exporter's takings, its reports of what could not go) and the targets are put back. What moved is what left the warehouse.
6. **Money.** Fuel (vans 12 litres per 100 km, trucks 26, cars 7.5, at the pump's price, read from `GasStationOverlay`) is paid at the warehouse when the vehicle leaves, as a running cost (`reallife_transaction_trips_fuel`). The driver's hourly wage over the trip's hours is shown, not charged: the game already pays the drivers.
7. **The log** keeps the last seven days' runs (at most 240), in place of the game's record of movements on the route's page.

## Spoilage

1. **Kinds and lives** (days out of and in cold storage): fruit, vegetables and flowers 6 and 12, dairy 5 and 14, bakery 4 and 7, meat 3 and 8, made food 4 and 8, frozen 3 and 90, by the product's name; any product the game weighs at the till is produce. Packaged goods, drinks and the rest never spoil.
   The first figure is the life on a shelf, in storage or on a grill; the second is the life of a piece kept cold (see **Cold storage** below).
   These are the usual lives. The slider scales them all at one place, `SpoilRules.Life` (through `SpoilRules.Scale`): the usual life times the pace, rounded to whole days and never under one day for goods that spoil, so a quarter of the time keeps fruit 2 days and a fridge's frozen food 23, four times as long keeps fruit 24 days. Every reader of a life goes through it: the places' turn of a day, the managers' orders and their short-lived routes, the trips, the rivals' loss, and the stock and buy pages. The managers' cap and the short-lived test read the scaled life, so goods that keep a week or less at the usual pace may not at a slow one. The Guide's figures are the usual lives.
2. **Batches.** Each place keeps each product as batches by the day they came in, at most six (the two oldest folded at their mean day). Once a second one place is matched to what it holds by the game's own count: less is taken from the oldest (sold, used or moved), more came in today. A trip's goods take their batches to the stop. A save from before, or a place first seen, takes all it holds as stocked that day.
3. **The turn of a day.** A place first looked at after midnight throws out what is past its life by the game's own calls (storage first, then the shelves, a warehouse's record of goods out saying so), its cost noted. One notice a day sums it. The stock page shows **Spoils in** and what spoiled today; a factory's page shows the week's share of what came in that spoiled.

### Cold storage: which products go inside a fridge, and what a fridge does for the rest

What the game lets into which furniture (decompiled build 3682; the lists are data of each item, read at run time, never assumed):

- A fridge or freezer that is a **shelf** (a drinks fridge, a large drinks fridge, an industrial fridge, an industrial freezer, a small industrial freezer: `ItemType.ShowcaseShelf`, `ShowcaseShelfController`) holds only what its own list `Item.itemsThatCanShowcase` names (`Producer.CanAddToInventory`). That is soda and energy drinks in the drinks fridges, **fresh food** in the industrial fridge, **frozen food** and **ice cream** in the freezers. No burger, hot dog, pizza, kebab, chips, burger bun, dough, ground beef, raw sausage or chicken, dairy, bakery or produce is on any cold shelf's list. Burgers and kebabs go on the industrial grill, hot dogs on the hot dog grill, pizza in the pizza oven, chips in the fryer, salad in the salad bar, and the rest in storage shelves and pallets.
- The two **staff fridges** (the standard fridge and the luxury fridge, `FridgeController`) take any food that fills (`saturation` above 0, `FridgeController.CanStore`), 10 slots of 5 pieces each, so 50 pieces of a food in boxes of 500 (`Item.cargoCapacity`, `cargoCapacityMultiplier`, `CargoInstance.GetMaxStockCapacity`). So a burger can be put in a staff fridge, but only 50 at a time; the mod's rule gives the staff fridge the same 50.

The rule:

1. A product that some cold shelf of the game lists (`LogisticsGame.ShownCold`: fresh food, frozen food, ice cream) keeps today's rule exactly. It has its cold life only if some of it sits inside a fridge or a freezer, and then the whole place keeps all of it as long as its coldest holder.
2. Every other product that spoils (burgers, hot dogs, pizza, kebabs, chips, dough, raw meat, dairy, bakery, produce and the rest) is kept cold by the fridges and freezers **standing at the place**, wherever the stock itself sits. Each piece of cold furniture has a room by its name (`SpoilRules.ColdRoom`): 1,000 pieces for an industrial fridge or an industrial freezer, 500 for a small industrial freezer, 50 for a standard or luxury fridge (the staff fridges, the game's own 10 slots of 5), and none for a drinks fridge or a large drinks fridge, which are for drinks. A box is 500 pieces and a busy fast food restaurant sells several hundred burgers a day, so a freezer (4,800 to buy, an industrial fridge 3,300, a small freezer 2,800) covers a day or two of one product and a very busy place wants two to four. The rooms add up over the place and are **shared by all such goods together** in proportion to their stock, in whole pieces (`SpoilRules.Share`: each good's floor of room times its stock over the total, the pieces left over one each to the largest remainders, ties to the lower name), so one unit is never 1,000 for each product. The pieces covered are the whole room or the stock, whichever is less.
3. The **newest** pieces are the covered ones. The oldest are sold first, so the stock that leaves first is the stock that is not kept cold, and a delivery that comes in is kept cold before the older stock ever is. A batch that straddles the line is split by pieces. Covered pieces keep the cold life, the rest the ordinary life, each at the slider's pace (`SpoilRules.Expired` with a cold share, `SpoilRules.SpoilsIn`: the oldest piece is an ordinary one while any is held). What is due is always the oldest pieces, so the day's throwing out takes the oldest first as before.
4. Only a place the player runs counts as a shop: a building that is not residential (`LogisticsGame.IsShop`). A home's fridge keeps nothing cold for the rule, and a staff fridge at home is not shop equipment. It is the player's own places only, as everything of spoilage is (`LogisticsGame.Owned`, `GoingPublic.Mine`), and in a Going Public session the pace stays 100 on every machine.
5. Nothing is saved for it. The covered count is read again from the furniture each second a place is looked at (`SpoilPlace.Chilled`, `ColdLives`, not saved), and the batches are the same as before, so a save from before loads as it was and the new life applies from the next turn of a day. Stock already at a place keeps its batches. A fridge's own contents are not a separate case: a burger in a staff fridge counts in the place's room like any burger at the place.

What the player is told: the Guide's page "Delivery trips" (Food spoilage), the notice at the turn of a day, the option's tip, and the **Spoils in** column of a warehouse's stock page, which reads for example "8 days, 400 kept cold" (the days are the oldest ordinary piece's).

### Giving spoiling food to charity

Option "Give spoiling food to charity" (`living_spoilage_give`, `Settings.LivingSpoilageGive`), **off to begin with**, in the section of food spoilage and needing it. Code: `FoodGiftRules` (game-free), `LogisticsDirector.Give.cs`, `GivingDirector.GiveFood`, wired in `Wiring.LogisticsLinks` (`LogisticsBridges.FoodGiftReady`, `FoodGift`, `Neighbourhood`, `StaffLift`).

1. **What is given.** From 19:00 (`FoodGiftRules.EveningHour`), once a day for each of the player's places, whatever the next turn of the day would throw out (`FoodGiftRules.DueTomorrow`, the same `SpoilRules.Expired` with the same lives and cold share the turn uses, so cold storage still decides when a piece is due) is taken out of the stock by the game's own call (`LogisticsGame.Discard`) and out of the batches (`SpoilRules.Take`) and goes to the food bank. The turn then finds none of it, so no piece is counted twice, and nothing is thrown out as well. It happens only when giving can take it (giving runs, the city's people are running, not in a Going Public session); otherwise the food is thrown out as ever.
2. **What it is worth.** At what the stock cost the player (`Discard`'s cost), never the shelf price. No cash moves, so it is always a loss in money: the food was paid for, and a deduction saves only a share of its cost (the game's tax rate is under 100 per cent). The food bank's pot gets the whole cost (the city's households benefit); the player's giving gets only the part that earns.
3. **What earns.** Per place, product and day, the pieces that count are 3 per cent (`FoodGiftRules.SalesShare`) of what the place sold of that product over its last seven days (pieces that left it and were not thrown out, given, or carried off by a trip: `SpoilPlace.Sold`, a ring of seven days, kept only while the option is on). The share is chosen so that an honest shop, which wastes about a tenth of one day's sales, is inside it (3 per cent of a week's sales is about a fifth of an average day's), and so that a shop that orders ten times what it sells earns what the honest one earns and no more. A place that sells nothing earns nothing, so stock carried in on its last day is measured against that place's own sales. Pieces over the share are still given and earn nothing. Moving stock by hand out of a place counts there as use, which the share and the ceilings below bound.
4. **What earns it: the player's giving.** The earning part, at cost, goes into the year's gifts (`GivingState.TaxGifts`, `FoodGifts`, the food bank's player book), so it is deducted from tax by the same claim, under the same 6 per cent cap and carry forward, as any gift, and shown on the Giving page as "Food from your shops given this tax year, at cost" in both interfaces. It does not touch the monthly integrity curve.
5. **The other gains, once a day a place gave something that earned (at least one whole dollar of earning), within ceilings** (`FoodGiftRules.Grant`): 1 point of neighbourhood standing in the place's district (Fame's `Upkept`, no custom boost) with at most 2 points a week for a place and 2 for the player over all places; the place's staff lifted 2 points of satisfaction (`BusinessGame.Mood`) on at most 2 days a week for a place; the player's good mood (`ModState.MoodUp`) once a day on at most 3 days a week. Volume buys nothing, and many small places do not multiply it.
6. **The notice.** At the overnight round after a gift the day's one notice (`reallife_spoil_note_gave`) says how many pieces went to charity from how many places, what they were worth at cost, and what was still thrown out. The log has a line for each place.
7. **Save.** The sales rings, each place's standing and staff weeks and the player's standing and mood weeks are optional records of the spoilage line (`S`, `G`, `M`), written only when not empty, so a save from before, or with the option never on, writes and reads the line it always did and changes nothing. The Giving line has one more trailing field (`FoodGifts`). Going Public: only the player's own places, no cash and nothing sent.
8. **Checks.** `--logistics`, `FoodGiftChecks.cs`: an honest month, the farmer's month (ten times the sales every day), many places, stock carried in, the ceilings, a loss at any tax rate, no piece twice, cold storage, the option off, the save and the texts.

### Changing the slider in a save

A batch is stored as the day it came in, so a shorter or longer life alone would throw stock out at once or make old stock fresh. Each place therefore remembers the pace its batches were last read at (`SpoilPlace.Pace`, saved as an optional last field of the place's record only when it is not 100, so a save from before reads as 100 and writes the same line) and, at its next look, `SpoilRules.Rebase` moves each batch's day so that it keeps the share of its life it has used: half way through stays half way through, at the new life. A batch not yet due is held at least one day from due, one already due stays due, and every piece is kept. Changing back gives the age it had, to the day's rounding. Goods moved by a trip between places read at different paces are rebased to the place they arrive at.

### Going Public

In a session (host or guest) the pace is 100 whatever the slider says (`Settings.SpoilPaceNow`), so every machine spoils the same and nothing is sent over the network. The slider's row says it waits (`GoingPublic.SpoilPaceSentence`). When the session ends the places rebase to the player's own setting by the rule above.

## The player's managers and the rivals


- The mod's managers order no more of a product that keeps a week or less than sells before it spoils (`SpoilRules.OrderCap`), and their routes run as trips by the same rules. The wholesalers' deliveries stay the game's weekly ones.
- Once a day, after the game has valued the city's shops (`CompetitionHelper`), each rival shop's last valued day pays a van's round trip to the nearest wholesale store or importer (fuel and a driver at 18 an hour), the hours its goods are on the road (half a per cent of the day's sales an hour) and the share of its fresh goods that spoils (by its kind of shop: handling, and what outlives its life between deliveries every four days), never more than a quarter of the day. Their income, worth and closing are read from those days. Going Public's guests leave it to the host.

## Tests

- Options > Tests > **Load each route as its next run would, nothing moved**: each route as its next departure would load it, nothing moved or paid, one line each with PASS or FAIL.
- Options > Tests > **Count the fresh batches at every place now**: a whole round of the places at once, each product's batches against what is held.
- Self-check lines with Verbose on: `Trips check (the night's run)`, `Trips check (a departure ...)`, `Trips check (a stop ...)`.

- Options > "Food shelf life": at 100 nothing differs; move it in a save with fresh stock and the stock page's "Spoils in" changes while nothing is thrown out at once.
- Offline: `--logistics` in EconomyCheck (`LogisticsChecks.cs`, `SpoilPace`, `SpoilCold`).

## Known limits

- Nothing is spawned in the street: the trips are timed and paid, the vehicles stay where they are.
- The game's weekly wholesale deliveries are the game's: only the routes the logistics managers run are trips.
- The product kinds are by name; a new product of a game update or another mod never spoils unless the game weighs it.
- A freezer covers its room even when it is full of frozen food: the rule counts the furniture's size, not its free space.
- The old interface's own stock page is the game's and has no Spoils in column; the covered count is shown on the kit's stock page, and the Guide, the notice and the option's tip say the rule on both.
