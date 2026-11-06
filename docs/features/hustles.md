# Legal hustles: the Marketplace

## In a sentence

An app on the phone where the city's people sell used goods and cars that the player buys, collects at the seller's door or has brought home, fixes, cleans and sells on to buyers who haggle; and where clients ask for freelance visits (detailing a car, odd jobs, a junk run, a move), done at their door on the game's own work panel or left to a hired helper, with a rating from each client.

## Where the code is

- Folder: [`Scripts/Hustles`](../../Assets/Mods/RealLifeOverhaul/Scripts/Hustles/).
- Read first: `HustleDirector.cs` (the state, the day and the hour), `HustleCatalogue.cs` (every figure), `HustleRules.cs` (every rule), `HustleGame.cs` (the only file that touches the game).

| File | What it is |
| --- | --- |
| `HustleCatalogue` | the goods (sixteen kinds in four families), the four kinds of visit, and every figure (game-free) |
| `HustleState` | the day's listings, the player's things, buyers' offers, sales, visits and ratings (game-free) |
| `HustleStore` | the save's line `reallife:hustles` (game-free) |
| `HustleRules` | the morning's listings and requests, worth, haggling, repairs and cleaning, buyers and their counters, the hour, ratings and helpers (game-free) |
| `PickupRules` | collecting what was bought: the days a seller holds it, how near the player and the route must be, the doors asked for again, the repair of a save and the refund of what lapsed (game-free) |
| `HustleGame` | the game: the clock, money, the city's homes and doors, skills and energy, cars made, read and removed the game's own way, a van at a door, the repair shops |
| `HustleDirector` | the one component: the state, the hour, the save; its parts are Places (the panels at doors and the work on the clock), Actions (what the app calls) and Tests |
| `HustleWork` | a piece of work on the game's work panel (Jobs/ShiftSkip) |
| `HustleApp` | the app, a part file a tab: Listings, Yours, Offers, Freelance; `HustleColumns` its tables |
| `HustleText`, `HustleGuide` | its words and notices; its Guide page, options and test buttons |
| `HustleBridges` | what it asks of other features, filled in by [`Wiring/Wiring.HustleLinks.cs`](../../Assets/Mods/RealLifeOverhaul/Scripts/Wiring/Wiring.HustleLinks.cs) |

## What already existed, and what this builds on

- **The car showroom** (`Economy/Dealer`): a car's worth by kilometres and condition (`DealerRules.Worth`), a used car drawn from its owner with what they say of it and their least (`DealerRules.Used`), the haggling with an owner (`Haggle`) and a buyer's counter (`Counter`), names, the game's car list and colours (`DealerGame.Models`) and removing a car the game's own way. The Marketplace's cars are drawn and valued by the same rules, so a car is worth the same on both.
- **The game's cars**: a car bought is the game's own vehicle (`VehicleHelper.CreateAndSpawnVehicle`), its damage and dirt the game's own, so it is repaired and washed at a petrol station like any car of the player's, and the Marketplace reads its condition back from it.
- **The jobs** (`Scripts/Jobs`): `ShiftSkip`, the game's employer work panel with its clock and skip button, is the panel every piece of work here uses; `JobSpots.Front` the pavement at a door; the jobs' hired van (`JobRental`) is the van a junk run or a move hires, through `JobDirector.HireVan` and `ReturnVan`; no visit starts while a gig is on.
- **The economy's kerb finder** (`EconomyBridges.KerbSpot`): where a car bought is handed over.
- **The game's own panel at a place** (`GameUi.DoorPanel`), its notifications and question boxes, and its destination marker on an address.

The game sells nothing second hand and has no freelance work of the player's; the trades of the city expansion (`feature/city-expansion`) are not used.

## Buying

Each morning the city's people put up `GoodsADay` (9) goods and `CarsADay` (2) cars, each at the door of one of the city's homes (residential buildings with a door on the street, no DLC's, no private fence, no special service, ordered by street and number so the list is the same on every machine).

- Goods: a kind drawn from the sixteen, a true condition of 12 to 88 %, an electronics seller who says it is 8 to 22 points better than it is one time in four, an asking price of 82 to 130 % of what it is truly worth, and a least of 76 to 93 % of the asking price.
- Cars: drawn as the showroom's used cars are, with their faults and what the owner says.
- A thing's worth is its price new at today's price level times a share by condition (40, 160, 330, 500, 620, 700 thousandths at 0, 25, 50, 75, 90, 100 %); a car's is the showroom's, less up to 10 % for dirt.
- The listing shows what the seller says and what that would be worth. The truth is seen once it is the player's.
- An offer from 65 % of the asking price up: taken at their least or their last counter, else countered at their least and 30 % of the gap above it, never above the last counter; after three offers they sell to someone else. Negotiation skill brings their least down by up to 6 % of the asking price.
- Paid at once. Goods are collected at the seller's door (the door's panel: Collect) or brought home the next morning from 8 for 25 plus 5 % of the price. A car is collected: Take the keys makes it at a free stretch of kerb by the player.

## Collecting

- A seller holds what was bought for three days: the last day is the day of the purchase plus three (`PickupRules.HoldDays`). The Yours tab says `Collect by day N`; the thing's page says where, until when, and what happens after. At midnight after the last day the thing goes back to its seller and what was paid returns, as its own transaction (`Marketplace refund`) and a notice; a test thing returns nothing. Goods brought home never lapse.
- The pickup point is found once for a door and remembered: the pavement a step out from the door (`JobSpots.Front`), else the pavement within 14 m (a parked car there does not take it away), else the walking mesh within 8 m (a stoop, a yard), else the ground under the door. So a building the city has always has a point. A point found by a wider search is measured again once the player is within 25 m. A building the city's list lacks (a district standing late, a load not finished, a building taken down) is asked for again every four seconds, never remembered as missing.
- The route (a purchase, Show the way, a booked visit, the repair shop) is the game's own destination: the address is kept in the save as the game's Set destination does, and the marker is moved onto the pickup point, so it ends where the thing is. A route the game restores from the save after a load stands on the building and is moved onto the point once, never over a destination the player has set since.
- A parcel (the prop library's parcel_s, parcel_m or parcel_l by the kind of thing, stood from the jobs' props) stands at the point while the player is within 80 m, and goes when they are 140 m away or the thing is collected. Within 4.5 m of the point, on foot and outside, the door's panel offers Collect (or Take the keys for a car). By vehicle the panel does not show, so a notice says to get out, once, until the player leaves the place.
- Collect puts goods in the Yours tab (the Marketplace's own list of the player's things); a car is made at a free stretch of kerb by the player, as the game makes a bought vehicle. No goods become the game's items.
- A save from before things lapsed has its waiting things held three days from the day it loads; a waiting thing with no address to go to leaves the list and its money returns. A line in Player.log (`Hustles check (pickups on load)`) says how many.
- Going Public: nothing is sent. The point, the parcel and the route are this machine's own; a door is the city's building, which every machine has alike.
- NPC buyers: a buyer of the player's listed thing pays in the app (an offer answered on the Offers tab) and a car is taken by the buyer; nobody collects from a door, so the rules above have no NPC side.

## Fixing it up

- A repair by hand, at home or at any building of the player's (the panel over the player there): parts at 6 % of the price new, the kind's minutes on the work panel, and a chance of 45 % plus 0.5 % a point of repair experience (0 to 100), 15 % less for a thing under 25 %, between 15 and 95 %. It works: the condition rises to the kind's best, less up to 8; it fails: it loses 1 to 6. Experience rises 6 for a repair that worked, 3 for one that did not. Each attempt is drawn from the seed, the thing and the hour, so the same attempt comes out the same.
- A repair shop for electronics: the nearest electronics store open now (an appliance store when the city has none). Hand it in at the door, wait an hour on the work panel, pay up to 30 % of the price new by how bad it is (never under 25), and it comes back at 92 %.
- A clean, once, for furniture, clothes and shoes: 30 minutes, cleaning things for 8, and the kind's gain, never past 95 %.
- Stopping the work early does nothing and costs nothing.
- A car is repaired and washed the game's own way.

## Selling

- The player lists a thing at 70 to 150 % of its worth, in steps of 5 %.
- Each hour from 8 to 22, a listed thing with no offer draws a buyer with a chance of 7 % for goods and 4.5 % for a car, times the price's draw (2.6 at 60 % of worth, 1.8 at 80 %, 1 at its worth, 0.55 at 115 %, 0.25 at 130 %, 0.04 at 160 %) and the condition's (0.45 at nothing to 1.25 at 100 %).
- A buyer offers 72 to 95 % of the asking price, and would pay at most 95 to 118 % of the worth, never above the asking price. The offer stands six hours.
- Counter between their offer and the asking price: certain at their offer, falling to 10 % at their most, none above; the same counter always gets the same answer; refused, they come up a third of the way; two refused counters and they go.
- Sold: the money comes in as the game books a thing sold; a car leaves the game's vehicles when the buyer takes it (not while the player sits in it). A car the save no longer has (sold through the game, written off) leaves the list with a notice.

## Freelance visits

| visit | pays (start's prices) | work | needs | trains |
| --- | --- | --- | --- | --- |
| Car detailing | 150 | 2 h | nothing | cleaning |
| Odd jobs | 130 | 1 h 30 | nothing | the player's repair experience (a point a visit) |
| Junk run | 220 | 2 h | a van or a pickup at the door | delivery driving |
| A move | 360 | 1 h 30 at each of two addresses | a van or a pickup at both doors | delivery driving |

- Three requests a morning, four with a rating of 4.5 or better, two under 3. Each is wanted from an hour between 9 and 16, and the client waits four hours; the work may begin an hour early. Starting after the first hour is late.
- Pay is the table's at today's price level, times the client's own figure (90 to 115 %), and 10 % more for a rating of 4.5 or better.
- At the door, Start the job opens the work panel. With no van or pickup (cargo room of 14 or more) within 35 m of the door, Hire a van takes the jobs' van for the day (its fee, once a day), given back when the visit is paid.
- The stars: 3 for coming, up to 1.5 more by the skill the work draws on, half a star either way by chance, one off for coming late, two off for leaving before the end (paid for the minutes worked). Five stars bring a tip of 10 %.
- Booked and nobody came: one star. Called off: two. A request can be booked or sent a helper only while the client waits (`HustleRules.Takeable`); after that the visit is gone and the player is told.
- A helper takes 55 % of the pay, starts when the client wants and is done after the visit's hours; they are rated 3, 4 or 5 stars (one in four, two in four, one in four), and their stars count as the player's.
- The last twenty ratings make the player's rating.

## In the save

One line under `reallife:hustles` (`HustleStore`, version 1): the header, the ratings, the day's listings, the player's things, the offers, the sales kept and the visits. A line that cannot be read is kept as it was. Without the mod the key is ignored; a car bought is the game's own vehicle and stays. "Prepare this save for removing the mod" and "Reset the mod's data" remove the key with the feature (`Features`: `.Data`).

## Going Public

Every draw is from the save's own name for the playthrough (`GameInstance.seed`) and the day or hour, so the same save gives the same city. Nothing is sent. The doors used are the city's own buildings, nothing about them is changed, a test never uses a building another player rents, and only the player's own vehicles count as a van.

## Cost

A timer comparison a frame. Once a second: the day and hour compared, and the doors in play (a handful) measured against the player; a door's pavement is found once and remembered. Each new hour: the player's things valued and the hour's rules (microseconds). The city's homes are listed once a city. The app writes its rows when it opens, a tab is picked or something changes.

## Options and tests

Option `hustles` (Marketplace app: used goods and cars to flip, and freelance visits), on. Off: the app goes and nothing happens; the things bought stay in the save.

Test buttons (Options, Test tools), each writing a `Hustles check (...)` line:

- Marketplace: a laptop to collect at the nearest door
- Marketplace: check a pickup (buy, route, collect): a laptop bought for nothing at the nearest door; the route must end within 3 m of the point, the point must be on the walking mesh, a parcel must stand there, the door's panel must offer Collect at the point, and Collect must take the laptop. `Hustles check (test: pickup)` says PASS or FAIL and why
- Marketplace: a used car to collect at the nearest door
- Marketplace: fix a broken phone here (the place a repair is begun is waived)
- Marketplace: list a jacket with a buyer's offer now (opens the Offers tab on it)
- Marketplace: a client at the nearest door now
- Marketplace: a helper's visit done at once

What a test makes is marked as a test: selling it, a test repair or a test visit moves no money, a test's car cannot be sold through the game, and a test visit leaves the player's rating as it was.

## Checks

`EconomyCheck --hustles` (`HustleChecks.cs`): the catalogue, the morning drawn the same twice and differently the next day, the listings' bounds, haggling, worth, repairs by hand against their chance over many draws, the shop and the clean, buyers' draw and offers, counters, the hour (deliveries, lapses, missed visits, helpers), visits' pay and stars, a move's two stops, tests leaving the rating alone, the save's line read back field for field, a year of flipping and of visits against the jobs' pay, the timing, the source's layering, and the texts in every language.

Log lines with Developer diagnostics on: `Hustles: the listings of day ...`, `Hustles check (door panel): ...`, `Hustles check (work panel): ...`, `Hustles check (before a save): ...`. Always: `Hustles check (collected) ...`, `(car handed over)`, `(repair by hand)`, `(repair shop)`, `(clean)`, `(sold)`, `(visit)`, `(helper)`.

## Not checked in the game (the game could not be run)

Everything that is seen: the app's pages, the panel at a door and over the player indoors, the work panel's clock for each piece of work, a car made at the kerb, the hired van at a client's door, the guider on an address.

## For later

Proposals for the owner: goods as the game's own boxes carried in the hands and the car's boot; selling a car bought here to the player's own showroom, or buying one from it; auctions; hiring helpers by name with their own skill and wages; regular clients and contracts; a junk run to the city's tip.
