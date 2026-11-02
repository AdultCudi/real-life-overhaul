# Delivery trips and food spoilage

## In a sentence

Under the living economy's master option **Living population**, with **Delivery trips** on, the player's logistics routes run as delivery trips (a vehicle and its driver from the route's warehouse at each of its departure times, round its stops and back, the goods delivered on arrival by the game's own delivery), and with **Food spoilage** on, fresh goods at the player's places keep so many days by their kind, longer in a fridge or a freezer, the oldest sold first; the city's rival shops pay the same supply and spoilage in their books.

## Where the code is

- Folder: [`Scripts/Economy/Logistics`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Logistics/); the pages in `Scripts/Ui/BizMan/Ops/BizManScreen.Trips.cs` (with lines in `BizManScreen.Managers.cs` and `BizManScreen.Plant.cs`); the roads in `Scripts/Wiring/Wiring.LogisticsLinks.cs`; the game's pieces in `Scripts/Core/GameContract.Logistics.cs`; the managers' smaller orders in `Scripts/Economy/Company/CompanyDirector.ManagerDecisions.cs`.
- Options: "Delivery trips" (`living_trips`) and "Food spoilage" (`living_spoilage`), both under "Living population" (`living_population`), both on by default. Off, the game runs every route at once at 02:00 and nothing spoils, as before.

```
Economy/Logistics/              the first six are game-free and compiled by EconomyCheck (LogisticsChecks.cs)
  TripRules                       departures, the timeline, drive time by vehicle, fuel, wages, whole boxes, stops shared over the slots, the rivals' supply
  TripState                       a route's departures, a trip and its stops, a run of the log
  TripStore                       the save's line, "reallife:trips"
  SpoilRules                      a product's kind, its shelf life out of and in cold storage, batches, oldest first, expiry, the managers' cap, the rivals' loss
  SpoilState                      a place's batches and its seven days
  SpoilStore                      the save's line, "reallife:spoilage"
  LogisticsGame                   the plans, the hold, the slots, a stop's needs, the game's own delivery, the city's places, the pump's price, the fuel
  LogisticsGame.Spoil             the places, what spoils there, throwing out by the game's own calls, the rivals' valued days
  LogisticsBridges                the road between two places (the GPS's graph)
  LogisticsDirector               the start, the second, the hold of the game's run, the save
  LogisticsDirector.Trips         departures, a trip leaving, arriving and back, roads, what the pages read
  LogisticsDirector.Spoil         a place a second, the turn of the day, the notice, goods moved by a trip, what the pages read
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

1. **Kinds and lives** (days out of and in a fridge or a freezer): fruit, vegetables and flowers 6 and 12, dairy 5 and 14, bakery 4 and 7, meat 3 and 8, made food 4 and 8, frozen 3 and 90, by the product's name; any product the game weighs at the till is produce. Packaged goods, drinks and the rest never spoil. A place keeps a product as long as its coldest holder (any furniture named a fridge or a freezer).
2. **Batches.** Each place keeps each product as batches by the day they came in, at most six (the two oldest folded at their mean day). Once a second one place is matched to what it holds by the game's own count: less is taken from the oldest (sold, used or moved), more came in today. A trip's goods take their batches to the stop. A save from before, or a place first seen, takes all it holds as stocked that day.
3. **The turn of a day.** A place first looked at after midnight throws out what is past its life by the game's own calls (storage first, then the shelves, a warehouse's record of goods out saying so), its cost noted. One notice a day sums it. The stock page shows **Spoils in** and what spoiled today; a factory's page shows the week's share of what came in that spoiled.

## The player's managers and the rivals


- The mod's managers order no more of a product that keeps a week or less than sells before it spoils (`SpoilRules.OrderCap`), and their routes run as trips by the same rules. The wholesalers' deliveries stay the game's weekly ones.
- Once a day, after the game has valued the city's shops (`CompetitionHelper`), each rival shop's last valued day pays a van's round trip to the nearest wholesale store or importer (fuel and a driver at 18 an hour), the hours its goods are on the road (half a per cent of the day's sales an hour) and the share of its fresh goods that spoils (by its kind of shop: handling, and what outlives its life between deliveries every four days), never more than a quarter of the day. Their income, worth and closing are read from those days. Going Public's guests leave it to the host.

## Tests

- Options > Tests > **Load each route as its next run would, nothing moved**: each route as its next departure would load it, nothing moved or paid, one line each with PASS or FAIL.
- Options > Tests > **Count the fresh batches at every place now**: a whole round of the places at once, each product's batches against what is held.
- Self-check lines with Verbose on: `Trips check (the night's run)`, `Trips check (a departure ...)`, `Trips check (a stop ...)`.

## Known limits

- Nothing is spawned in the street: the trips are timed and paid, the vehicles stay where they are.
- The game's weekly wholesale deliveries are the game's: only the routes the logistics managers run are trips.
- The product kinds are by name; a new product of a game update or another mod never spoils unless the game weighs it.
