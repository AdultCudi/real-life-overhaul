# Car suppliers

## In a sentence

The owner of a Car Showroom chooses a supplier for each kind of new car, each with its own price and delivery; a manager fills a showroom's standing order from them each morning, and the city's other dealers buy from the same suppliers by the same rules.

A player asked for it: "different suppliers for each type of car at the showroom".

## Where the code is

- Folder: [`Scripts/Economy/Dealer/Supply`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Dealer/Supply/), a part of the Car Showroom (`Scripts/Economy/Dealer`) with its own option; its kit page stands with the Car Showroom's own, in `Scripts/Ui/Estate`.
- Read first: `CarSupplyCatalogue.cs` (the table) and `CarSupplyRules.cs` (every rule, game-free, run by EconomyCheck's `BizAsksChecks.cs`), then `DealerDirector.Supply.cs`.

```
CarSupplyCatalogue      the kinds, the three suppliers, each one's price for each kind and its delivery, the bounds
CarSupplyRules          game-free: a supplier's price and delivery, a showroom's price, the other dealers' suppliers, what a manager orders in the morning (Morning, Plan), the deliveries cap, where a delivery's car comes from
CarSupplyState          the supplier of each kind, each showroom's standing order and the day its manager last ordered for it, the deliveries a supplier is bringing
CarSupplyStore          the save's one line, "reallife:dealer_supply"
DealerDirector.Supply   the director's part: the price and the hour a new car comes, an order that is sent, SetSupplier, SetKeep, the morning's standing orders, the tests; CarSupplyBridges
Ui/Estate/CarSupplyPages   game-free: the suppliers' page on the kit and the words built from the table (SupplyWords); the checks and previews lay out the same page
CarSupplyText           the same words in the game's language, and the feature's entry with its Guide facts (CarSupplyGuide)
DealerApp.Supply        the Car Showroom app in the game's frame: the Market tab's summary and a page a kind
Ui/Estate/DealerLive.Supply   the Car Showroom app on the kit: the link over the market's table and CarSupplyPages' page
```

Hooks in the showroom's own files, each a line or a few: `DealerDirector.Actions.cs` (`Buy`: the price and the order), `DealerDirector.Business.cs` (a delivery's source and notice), `DealerDirector.Clock.cs` and `DealerDay.cs` (the morning), `RivalRules.cs` (the other dealers), `DealerDirector.cs` (the save), `DealerApp.Market.cs` and `Ui/Estate/DealerLive.Market.cs` (a new car's page), `UiScreens.Estate.cs` (the page's keys), `Wiring/Wiring.BizAsksLinks.cs`. The promise that the car paid for is the car that stands (below) has its game-free rules in `Scripts/Economy/Dealer/CarStandRules.cs` and its director's part in `Scripts/Economy/Dealer/DealerDirector.Stand.cs`, in the showroom's own folder, with the reading back in `DealerGame.cs`.

## What was there

The game has no showroom of the player's own: the Car Showroom is the mod's business type. It bought every new car from one wholesaler: six listings each morning (`DealerRules.Morning`) at 895 to 935 thousandths of a car's worth new, less 10 thousandths for a respected showroom (`DealerRules.WholesalePrice`), on the lot at once. The kinds of car are the six the showroom deals in, as the mod reads the game's vehicle types (`CarCatalogue`, `CarKind` 1 to 6): economy cars, saloons, luxury cars, sports cars, SUVs and pickups, vans. Lots won at the trade auction come 2 or 3 days later at 08:00 as deliveries that hold a place on the lot (16 at most).

## What the feature does

- **The suppliers** (`CarSupplyCatalogue`), prices in thousandths of the wholesaler's listed price for the same car:

| Supplier | Economy | Saloons | Luxury | Sports | SUVs and pickups | Vans | Delivery |
| --- | --- | --- | --- | --- | --- | --- | --- |
| City Motor Wholesale | 1000 | 1000 | 1000 | 1000 | 1000 | 1000 | at once |
| Harbour Line Imports | 965 | 972 | 1012 | 1015 | 985 | 968 | 3 days |
| Kingsway Prestige Trade | 1014 | 990 | 962 | 960 | 974 | 1012 | 2 days |

  No price is under 960 or over 1015: no supplier is further from the wholesaler than the wholesaler's own listings span (40 thousandths). The delivery days are the trade auction's own (`DealerCatalogue.DeliveryDaysMost` and `DeliveryDaysLeast`), at its hour, 08:00. Every other supplier is cheaper for some kinds and dearer for others, and none of them delivers at once.
- **A new car's price** (`CarSupplyRules.ShowroomPrice`, `DealerDirector.NewPrice`): the listed price by the supplier's share for the kind, named in fifties, then the showroom's reputation perk as the wholesaler gives it. The pages show it and the one purchase path charges it (`DealerDirector.Buy`).
- **An order** (`BuyShipped`). A car from a supplier that delivers later is refused before any money when 16 cars are already on their way, then paid for at once and sent: a delivery of the showroom's own list that holds its place on the lot and arrives the supplier's days on at 08:00 (`CarSupplyRules.Due`), as a new car with the supplier's notice. Nothing is drawn at the moment of ordering, so a reload changes neither price nor day. An order is never cancelled, and paid back only in the one case below: a car the game cannot stand when it comes.
- **Changing a supplier** changes no car already ordered: a delivery keeps what it cost and when it is due.
- **Nothing to gain between suppliers.** A car sold straight to the trade pays 780 thousandths of its worth (`DealerCatalogue.TradeShare`); the cheapest supplier's cheapest car at the best reputation costs more than the trade's 780 thousandths of its worth. The lowest it can come to is about 850 thousandths: 895 for the wholesaler's lowest listing, 960 of that for the cheapest supplier's best kind, less the best reputation's 10. Named in fifties, a cheap car can land at or a little under 850, so the claim is only that it stays above 780, which is what the check holds (`WholesaleLeast` x `PriceLeast` x the perk against `TradeShare`). EconomyCheck holds every new car of three hundred mornings, from every supplier, dearer than the trade's price for it the same day.
- **Standing orders and the manager** (`CarSupplyRules.Morning`, `Plan`, `SupplyMorning`). For each showroom the owner sets how many new cars of each kind it keeps (0 to 12; 0 is none). Each hour of the day's market the managers' morning asks, for each showroom with a standing order not yet filled that day, whether a manager of the company can decide its stock today (`CompanyDirector.Decides(place, ManagerTask.Stock)`: the door every decision of a manager passes, `CompanyDay.Deciding`, with the Managers tab's Stock orders task; a manager off sick with nobody to stand in decides nothing, so the order waits). Only when somebody decides is the showroom marked as ordered for the day (`CarSupplyKeep.Day`): a day nobody decides is not marked and is asked again at the next hour, and a marked one is not asked again that day however often the hour is worked or the save is loaded. The manager orders for each kind short of its number (the cars on the lot and on their way count) the cheapest new cars of the kind the market lists that day, from the kind's supplier at its price, while the lot has a free place, a delivery can be taken (`CarSupplyRules.WaysFull`) and the cash at hand covers the car. No listing is ordered twice, and every car goes through `Buy`. Without such a manager the number is kept and the owner buys by hand.
- **The other dealers** (`CarSupplyRules.RivalSupplier`, `RivalBuys`, `RivalRules.Morning`). Each of the six dealers buys each kind from a supplier of its own, the same all game: one whose usual price is under a car's worth takes the cheapest for the kind, one at 1100 thousandths or more takes the wholesaler's at once, one between draws for each kind (one time in two the cheapest). A car it restocks costs it what its supplier asks, so it asks that much less or more, and it is on its lot only when its supplier has brought it: until then a buyer does not compare it with the player's (`RivalRules.Cheapest`), and it is not sold: a car still on its way, or arriving that morning, is not rolled for a sale (`RivalRules.Morning`). The suppliers' page says how many dealers buy each kind from whom, and the offers page's "rivals ask" rows follow. The dealers' existing price checks run once more with the suppliers on as shipped, and a rival's ask stays a whole price in fifties within the suppliers' span of what it asks without them.
- **Where the owner finds it.** The Car Showroom app's **Market** tab, where new cars are bought: **Suppliers** opens the page with a step a kind for its supplier (its price against the listed price and its delivery under it), the standing order of a showroom with a step a kind, and where the other dealers buy. A new car's page shows its supplier, its price and when it comes, and the question before buying says the day a car on order arrives. In the game's frame the Market tab lists the kinds' suppliers and **Choose suppliers** opens a page a kind.

## The car paid for is the car that stands

Every car the showroom pays for becomes a vehicle in one place, `DealerGame.MakeCar`, through `DealerDirector.Arrive`: a car bought at once (`Buy`), a car that comes later (`Arrivals`: a supplier's order, or a lot won at the trade auction) and a buyer's car taken in part exchange. The model travels by name from the market to the game's vehicle (a listing is found by its number, never by its place in the list, so a morning's refresh between choosing and paying cannot turn one car into another), so what can differ is what the game makes of it. The showroom reads that back (`CarStandRules`, game-free):

- **Before any money.** The game must have the model (`DealerGame.Knows`). A listing of a car it no longer has, a car mod taken out or a changed game, is refused with a notice and nothing is charged (`reallife_dealer_refuse_model`), for a car at once and a car on order alike.
- **When it is made.** `DealerGame.Spawn` reads back which model the game stood (the object's `VehicleController.vehicleType`) against the model asked for (`CarStandRules.Verdict`). A car that is another model, or one the game throws on, is taken out again whole, its object and its entry in the save, so nothing half made is left on the lot. The car's number is given only for a car that stands.
- **What is owed.** A car bought at once that cannot be made has its whole price given back, under the line the purchase was booked under, so the showroom's stock row nets to nothing, and the notice says why (`reallife_dealer_note_stand_unknown`, `_wrong`, `_failed`). A car on its way whose model the game has not, or cannot stand as paid for, when it comes (`Arrivals`, `Unstood`) has all it cost given back and its delivery and order ended, the same notice. A lot with no place for it is not the model's fault: the car waits, as before. A part exchange the game cannot stand goes straight to the trade, as one with no place always did.
- **When the player walks in.** `DealerGame.Park` reads each car's vehicle, the save's entry and the object the game stood, against the model paid for. A car whose vehicle is another model has no bay, and `DealerDirector.DropWrong` takes it off the lot and the stock, lets go its offers and bay, and gives back all it cost (`CarStandRules.Refund`), with the notice, in the same step as a car the save lost is taken off (`DropLost`), when the player walks in and when a lot is laid out again.

With Developer diagnostics on, each car made writes `Dealership check (a car made)`, the model paid for against the model that stands, PASS or FAIL; a car taken off, or paid back, writes its line whether or not they are on, naming both models as the game has them.

## The option

**Suppliers for the Car Showroom** (`economy_dealer_supply`, on), under the Car Showroom. Off: every new car comes from the wholesaler at once at its listed price, exactly as before, no standing order is filled and the other dealers restock as before; cars already ordered still arrive on their terms. The choices stay in the save.

## Going Public

A showroom is the player's own business and its market is drawn from its own seed on the owner's machine. Nothing new is sent and nobody else's showroom is touched.

## The save

One line, `reallife:dealer_supply`, version 1: the supplier of each kind, each showroom's standing order with the day its manager last ordered for it, and which deliveries are a supplier's. The cars on order are the dealership's own deliveries, saved with it. A save from before has no line: every kind comes from the wholesaler. A line from a newer version is read as far as it goes.

## With "Suppliers and contracts"

The general supplier model for other goods is built separately. The two could join by making `CarSupplyCatalogue`'s three rows suppliers of that model (a firm each, with its health and its late or short deliveries), keeping `CarSupplyRules.Price`, `Due` and `Plan` as the showroom's side: a price share and a delivery for a kind are all the showroom asks of a supplier.

## Cost

Nothing per frame. A price is a multiplication; once a morning a pass over the market's few listings for each showroom with a standing order.

## Tests

- Options, Test tools, each writing a `Dealership check (...)` line: **order the first new car from the cheapest supplier for its kind**, **a manager's morning order now** (a standing order of one more car of a kind listed today, filled as a manager would).
- `EconomyCheck --showroom-stand` (`ShowroomStandChecks.cs`): the verdict on every pair of models, who is at fault, that a refund leaves the player with the cash they had, a listing found by its number after a refresh, the model saved and read back by name, the one place a vehicle is made and what it reads back and takes out again, the order of a purchase (the model known, the money taken, the car made, the whole price given back, the notice), a car on its way and a car found wrong when walking in, nothing per frame or sent, and the texts.
- `EconomyCheck --bizasks` (`BizAsksChecks.cs`): the table's shape and bounds; a price and a delivery; an old save and the option off cost exactly what they did; nothing to gain against the trade; a manager's orders inside every cap (the number, the lot's room, the cash, the deliveries), never a listing twice, never a car on its way again; the managers' morning (a day nobody decides is not marked, once a day, a showroom at a time, the option off); the other dealers, their cars on their way never sold before they arrive, and their prices with the suppliers on; the save (old, newer, damaged, a decimal comma, an order's mark); the page and each step; the sources; the texts in eleven languages. Each rule was broken once to see its check fail.

## Not seen in the game

Everything above is built and checked offline. Not seen in play: the link and the page on the Market tab, a car on order arriving with its notice, and a manager's morning orders at a showroom with a manager appointed. Not seen in play either: a car the game stands as another model, or cannot stand at all (no such car has been made to happen; the reading back and the payment back are checked in the sources and the rules), and the notices that say so.
