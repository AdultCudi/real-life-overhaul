# Living with other authors' mods

The game loads every mod that is switched on into one program. Two mods that do the same thing to the same object
of the game (steer the camera, draw on the city map) do not fail: they both run, and the player sees a picture that
shakes or two route lines. This page is the rule this mod follows when that happens, and the table of the mods it
knows by name. The next clash is one more line in that table and, if it is a new kind, one more place that asks.

## The rule

1. **This mod is the one that gives way.** It never switches another mod off, never changes another mod's objects and
   never patches its code. What it can do is stop doing its own share. One exception, on the owner's word: Empire
   Motors' view from the driver's seat is switched off while this mod's camera key is V (see "Empire Motors").
2. **Stand down for what is seen, not for what is installed.** A mod that is switched on may be idle (the camera mod
   below starts switched off, behind a key). So where the game lets it be seen, this mod watches what happens to the
   game's object and stands down only while something else is driving it, and comes back when that stops. A name
   only supplies the sentence the player reads, and a shorter count.
3. **Never take turns.** Standing down takes a few frames in a row; coming back takes many more in a row
   (`CameraWatch`: 12 frames, 3 with a known mod installed, against 90). Two mods that alternate frame by frame are
   the fault this exists to prevent.
4. **Where it cannot be seen, divide the screen.** Nothing shows from outside whether another mod is drawing a route
   at this moment. There the split is fixed: the other mod keeps the game object it draws on, this mod keeps what is
   its own. The player gets one option to hand over more.
5. **Say so, in the game's own interface, once.** A notice the first time in a session (not on every load), one
   "Other mods:" warning in Player.log per mod found, a self-check line with Developer diagnostics on, and the options
   that do nothing drawn as the game draws a control that cannot be used, with a sentence that names the mod.
6. **Cost nothing.** Looking for mods is one walk over the loaded assemblies when a city loads and when the options
   page is drawn. Watching is a handful of loads a frame, with nothing allocated. State the cost in the class comment.

## How a mod is found (`Core/OtherMods`)

A mod is known by a type of its own: an assembly whose name begins as the table says (`OtherModName.Is`: the name
itself, or the name and then something that is not a letter, because some authors put the version in the assembly's
name) and which holds the named type. `OtherMods.Look(logger)` does the walk and logs one line per mod newly found;
`OtherMods.<mod>.Loaded` is a field read from then on. `OtherMods.Of(kind)` gives the first mod of a kind that is on.

## The table

| Mod (as its author names it) | Assembly begins | Known type | What it does to the game | What this mod does |
| --- | --- | --- | --- | --- |
| Immersion - RPG Camera | `Immersion` (ships as `Immersion-1.2.0-test8`) | `Immersion.ImmersionEntry` | Adds its own Cinemachine virtual camera and gives it priority 1000 while its camera is on, -1000 while it is off (its key, default off) or a screen is open; locks the pointer while on. | `Camera/CameraYield`: while that camera is live, `ThirdPersonCamera` gives the game's on-foot cameras their own figures back and frees the pointer, and `ChaseCamera` lets go. Back 90 clean frames after it stops. The Camera section's options are drawn unusable with "unavailable: the camera is with Immersion - RPG Camera". |
| Voogle Route | `VoogleRoute` | `VoogleRoute.VoogleRouteCityMod` | Draws its route as lines in the world: on the ground on foot and driving, and on the city map on the map's own layer (`CityMap.cityMapMask`); has its own click, bookmark panels and destination popup on the city map, and a phone app. No minimap on the HUD. | `Navigation/NavDirector`: nothing of this mod's is put on the city map (no `NavCityMap` object, no double click). This mod's route stays on its own minimap with its two lines, but the arrival notice and the end of the destination are left to that mod (one notice, one rule). With "Use the other mod's route line" on (the row is on the page only while that mod is on; default off) there is no route of this mod's at all. |
| Going Public (multiplayer, Workshop 3765662670) | `BigAmbitionsMP` | `BigAmbitionsMP.MPServer` | Lets several players share one city. Each plays their own save; the clock, weather, market, who rents and owns what and (near the host) the traffic are the host's. Patches the game with Harmony (about 800 patches): stops the game's time skip and pause in a session and turns them into a vote of all players, pins the game speed, puts back clock jumps it did not make, mirrors the host's traffic on a guest (the traffic system does not run there), puts other players' businesses and homes on each machine as rented by the player with their id as owner. | `Core/GoingPublic`: see "Going Public" below. |
| Going Public: Extras (3794606607) | `BampExtras` | `BampExtras.ModEntry` | Location and property sharing in Going Public's own hub and on the city map (its own markers; its own message type). | Nothing: logged only. |
| Going Public Expansion-Friend Trade (3798559131) | `ForeignTradeMultiplayer` | `ForeignTradeMultiplayer.InitEntry` | Trades between players through the ForeignTrade mod's money and stock, its own phone button (a copy of the game's), `ftmp:` transactions and save keys. | Nothing: logged only. Its button is one more child of the phone's grid, which the pages take in. |
| Going Public: Français (3794605892) | `BampFrancais` | `BampFrancais.ModEntry` | Rewrites Going Public's own English strings (a transpiler on its methods only). | Nothing: logged only. |
| Empire Motors (dealership, Workshop 3787690782) | `EmpireMotors` | `EmpireMotors.Core.EmpireMotorsMod` | A car dealership as a business type of its own: stock bought at wholesale, used and at auction, parked as the player's vehicles at the dealership; sold to customers it makes up, with financing, trade-ins and warranties; its own app in the full menu's top bar; a talking desk at the game's own dealership that sells its car add-ons; V in a car for a view from the driver's seat (kept off while this mod's camera key is V); F6, F7, F9, F12. No Harmony. | `Core/EmpireMotors`: see "Empire Motors" below. |
| Empire Motors: Mustang GT, Empire Motors: BMW G82 (its car add-ons) | `EmpireMustangGT`, `empirebmwg82` | `EmpireMustangGTMod`, `EmpireBMWG82Mod` | Each registers one car (`ModdingAPI.RegisterModVehicleType`) with its lights and a dashboard; the BMW puts itself on Empire Motors' desk. No Harmony. | Logged only; car tuning has a row for each car by its type name (`Driving/CarCatalogue`). |

## How a foreign camera is seen without a name (`Camera/CameraYield`)

Two findings, each read once a frame, neither needing to know who:

- **A live virtual camera above the game's priorities.** The game gives its own virtual cameras priority 1 or 0 and
  no other (`Helpers.CameraHelper.CameraTransition`). This mod's own (the chase camera at 100, the custom shop's at
  200) are named `RealLifeOverhaul ...`. Any other live camera with a priority over 1 is another mod's.
- **A camera that is not where the engine put it.** The engine's camera brain writes the camera's place at the end of
  a frame and nothing of the game's writes it afterwards (no script of the game sets the main camera's transform). If
  at the start of the next frame the camera is more than 5 cm or half a degree from there, something wrote it in
  between. Not looked at while the brain is blending between two cameras or is switched off (photo mode).

Nothing is counted while a screen is open: camera mods stand aside for the city map and the menus themselves. Nor
during the player's funeral, the one scene the game films with cameras that are not on its list (a timeline's).

## Going Public (`Core/GoingPublic`)

Read in its decompiled code (`evidence/othermods/GoingPublic`), never run. What it does that this mod meets:

- **One save per player.** Each player's own save (money, businesses, this mod's state) is kept on the host's machine and
  loaded on join; the shared world is laid over it. So this mod's money, records and phone are each player's, and right.
  The city's economy, government and stock market are not: they are the host's, one city for everybody (see "The shared
  city" below).
- **The clock is everyone's.** In a session it stops `TimeMachine.StartTimeMachine` as soon as it starts (and never runs
  `TimeMachine.Update`), refuses `GameSpeedController.SetPause`, `SetPauseState` and `DisableTimeControl`, unlocks a time
  lock every half second, sets `Time.timeScale` itself every frame, and pins back any jump of the clock it did not make.
  Time runs faster only by a vote of every player, from an activity the game's panel shows (the mod's own shift skip is
  one). So `GoingPublic.ClockShared` makes every skip of this mod's pass no time (custody, prison, rides, the garage,
  outings, the family), photo mode leave the clock running, and refuses what needs days or a night (holidays, hotel sleep).
- **Other players' buildings.** On each machine another player's business or home is `RentedByPlayer` with that player's
  id in `businessOwnerRivalId`; Going Public keeps it out of the game's own lists of the player's assets
  (`GameStatePatcher.HideFromOwnAssetLists`). Every place of this mod that asks "is this the player's?" asks
  `GoingPublic.Mine(reg)` (which calls that test) instead of `RentedByPlayer`; places that ask "is it free for the
  city?" still ask `!RentedByPlayer`, which is right for both.
- **A guest's traffic is the host's.** On a guest the traffic system is not run (`TrafficManager.Update`/`FixedUpdate`
  skipped) and the host's cars are placed from its figures. `TrafficTools.Ready` is false there; `TrafficTools.Street`
  is the old "out on a running street" test for what only needs the street (jobs, outings, leisure, two-wheelers).
- **Who owns what is the host's record.** The game's own ways of giving up or selling a business or building are patched
  to tell the host; this mod's own sale of a business, a building's sale and car hire do not, so they wait in a session.
  A guest cannot start a business of the city's (`CompetitionHelper.StartNewCompetitorBusiness` is host only): a guest's
  franchised shop is in the books only.
- **Closing a business.** `BuildingRegistration.TemporarilyClose` has a postfix that sends the player's own shop's state to
  everyone at once, and a prefix that routes a merged company's shop to its owner. This mod closes only the player's own
  (through `Mine`), so its closures reach the others.

Found by its assembly and `BigAmbitionsMP.MPServer`; then three statics are read by reflection once and called through
delegates: `MPServer.IsRunning` (host), `MPClient.IsClientInWorld` (guest), `GameStatePatcher.HideFromOwnAssetLists`.
Read once a second of real time (`GoingPublicWatch`); a change logs one warning, gives back any traffic held, and the
options page is drawn again. The notice is shown once a run, after the save is in. "Test: pretend a Going Public session"
steps host, guest, alone.

Diagnostics: `Other mods: 'Going Public' is also switched on (assembly BigAmbitionsMP). ...`, `Going Public: this
machine hosts a session. ...` / `... is a guest in a session. ...` / `... the session is over ...`.

Proven (read): everything above. Inferred (not seen with two players): that a franchised shop the host opens shows on the
guest, that the mod's shift skip goes through the vote as the game's own work does, and how the phone's pages sit with
Going Public's two buttons and the phone it makes one row taller.

### The shared city (`Core/SharedCity`, `Core/SharedCityWire`, `Core/GoingPublicLink`)

In a session the city's economy, its government and its stock market are ONE city for every player: the host's. Without
this each player's save ran its own and they drifted apart (a different bank rate, mayor, election and share prices on
every screen).

**How an add-on talks through Going Public** (read in `BampExtras/LocationNet.cs` and
`ForeignTradeMultiplayer/GoingPublicAdapter.cs`): there is no call to register a message. An add-on picks a number of
`MessageType` that Going Public does not use (Extras 250, Friend Trade 249; Going Public's own end at 222), puts a Harmony
prefix on its two receivers, `MPServer.OnReceive(MPLink peer, byte[] bytes)` (host) and `MPClient.OnReceive(byte[] bytes)`
(guest), takes the frames that are its own and returns false for them, and sends with Going Public's own
`MessageEnvelope`: `MPClient.SendEnvelope(env)` to the host, `MPLink.Send(env)` (or `MPServer.SendHubTo`) to one guest, a
loop over `MPServer._clients` for all. Every send is reliable and ordered (Steam's reliable lane, cut into fragments when
large; LiteNetLib's ReliableOrdered). The receivers run on the transport's thread (LiteNetLib polls on one of its own), so
the work is queued for the game's thread (the add-ons use `GameStatePatcher.EnqueueOnMainThread`). An envelope over 4 KB
of JSON is deflated by Going Public; an envelope with an `Attachment` (its binary frame, `02 'B' 'Z' 'A'`, the type in
bytes 4 and 5) is sent as it is. Going Public logs a size warning over 300 KB and sends anything; the host drops a frame
from a link that has not said hello to it (`_peerNames`). A guest is in the world when `MPClient.IsClientInWorld`; its
connection is up while `MPClient.IsConnected`. Going Public itself replays its world state to a joiner
(`SendWorldStateTo`, `ResendJoinerState` at its world-ready) and keeps nothing for an add-on: an add-on that needs a joiner
to have its state must send it itself.

**This mod's way in.** Message type **231** (free in Going Public's enum and its add-ons'; checked at start, the link stays
off if Going Public ever takes it). Everything is found by reflection when Going Public is switched on, Harmony included
(Going Public brings it; the game has none); nothing of either is referenced at build time. A missing piece leaves the link
off with one warning, and each player's city goes on as their own. The prefixes only copy this mod's bytes into a queue;
`SharedCity.Frame` drains it on the game's thread (a field read a frame while nothing has come).

**The messages** (`SharedCityWire`): `RLOZ`, the lengths, and deflated: `RLOC`, the layout's number, the kind, this mod's
version, a sequence, the parts (a letter and a text each) and a CRC-32. A damaged message is dropped whole; a message of
another version or layout is never applied. Kinds: Hello (a guest in the world, every 5 s until answered: a late join and a
rejoin are the same), Snapshot (the host's answer: every part it runs, whole), Update (host to every guest that said hello,
at most every 10 s of real time, only what changed), Request (a guest's doings, within the second), Refused (another
version: the guest is told once and keeps its own city), Resync (a part came out different: it is sent whole again).

| part | whole | an update | the player's own, kept |
|---|---|---|---|
| E economy (`Economy/City/CityShare`) | `CityStore`'s line without the player's figures | whole, when a day is counted | carried rent, wages, takings, the last rival, the goods stocked |
| G government (`Civic/CivicShare`) | mayor, council, levers, what is on its way, the election (candidates, polls, momentum, the count, every player's ballot), approval, heat, petition, council calendar | whole, after each hour run | donations, endorsement, leaflets, rallies, the ballot, scandal, favours, the weekly account, the petition's being theirs |
| W building sites | every site, none anybody's own | whole, each hour | the guest's own refit (ids from 1,000,000), when its shopfront was refitted, what is next door |
| N notices | (nothing) | the city's toasts and stories of the hours just run | the polls opening (each guest's own polling place) |
| S street events' calendar | the salt the calendar and the weather's spells are drawn from | (never changes) | |
| M stock market (`Markets/MarketShare`) | `MarketStore`'s city line (every company with all its candles, the indices, the news) and the tape from that day | the tape's new lines (day's secret, city of the day, events, the frontier) and a check of the prices | holdings, orders, trades, watch list, account, dividends owed, recent trading |

The market is exact (the same state, secret, city and events give the same moments to the cent), so a guest's market plays
the host's tape and never passes the host's furthest moment: no price is ever sent. Every player's trades move the one
market: in a session a trade's lasting move is an event on the host's tape (`EventTrade`, `EventVolume`) that every market
plays at the next moment; a day played in a session and replayed after a load takes those moves from the tape, once. Each
update carries the host's prices at its moment (a checksum); a guest that comes out different asks for the market again.

**A player's own listed company** (`Markets/Listing`) is a company of the market like any other, with its founder's hash
(`OwnListing.Owner`; `MarketState.Me` is this machine's player). The host's company is in the city's line and every guest
plays it from the host's tape: its day's books are in the host's city of the day (`MarketEnv.Own*`), its founder's deals,
buy-backs, new shares, pay, dividend, taking private, audit and a business sold are tape events (`MarketInputs.EventOwn`),
and only the founder's machine moves money for it (`MarketEngine.Mine`). A guest's own company is not in the host's city:
`MarketShare.Adopt` sets it aside whole (`ListingFile.Suspended`, in the guest's own part of the save) and nothing of it
moves; its way to the market waits. When the guest plays alone again (`MarketEngine.Alone`, on leaving or on opening a save
made as a guest) every other player's company leaves the market, the guest's holding in it paid at its last price, and
the guest's own company is back, the profit of the days away counted from its books at the next open.

**A guest steps none of it**: no day of the economy (`CityEconomyDirector.OnDay` does only the player's own part of the
day), no hour of the government (`CivicGovernment.FollowTick`), no site but its own refit (`CivicWorks.Shared`), no market
moment past the host's. It applies the host's state to its own game as alone (prices, the bank rate, footfall, the policies'
effects, posters, sites in the street). A guest's vote, donation, endorsement, leaflets, rally, petition, signature,
complaint, report of an unsafe site, a favour that became a story, the crime it saw and its trades are done at once in
its game and sent as requests; the host applies them to the city within bounds and they come back with the next update.
A guest's ballot is counted in the host's election (`GovState.OthersVotes`, at the end of the save's line).

**Leaving**: nothing is undone; each system steps again from the host's last state, which is now the player's own (but
for the market's companies of other players, which leave it, and the player's own company, which is back).
**Saves**: each system writes its state as ever, and on a guest that state is the host's city, so Going Public's
coordinated saves (`MPSaveCoordinator`, which runs the game's own save on every machine) keep the shared city in every
guest's save.

Not shared, and why: the paper's event log (accidents and crimes near each player: what each saw, on their own machine),
the wording of the paper's lines (drawn per machine), each player's own business and friends' pieces; each player's
settings (a guest's economy strength or street-event frequency still shapes what it applies; the host's settings shaped
the model); the market's city of the day counts the host's businesses only as competitors of the listed companies.

Test: Options, Test tools, Going Public: "Record the city as a host would send it" keeps a snapshot; "Apply the recorded
city as a guest" pretends a joined session and applies it through the very path a guest's messages take (then nothing of
the city moves until the pretence ends, and it carries on from there). `RealLifeOverhaul.Build/EconomyCheck`
(`SharedCityChecks.cs`) checks the messages, every part's snapshot and update, and sixty market days of a host and a guest
trading with the same prices at every check.

Diagnostics: `Other mods: Going Public's messaging is in place for the shared city ...`, `Shared city: this guest follows
the host's city: ...`, `Shared city: the whole city (...) sent to ...`, `Shared city: the host runs version ...`, `Stock
market: at d/p this guest's market is not the host's ...`, `Shared city: the host's city is no longer followed (...)`.

Proven (read, and checked offline): how the add-ons send and receive, the frame this mod's bytes are told apart by, the
messages, every part's snapshot and update and the market's lockstep. Inferred (not seen with two players): that Harmony's
prefix on Going Public's receivers takes this mod's frames on both transports as it does the add-ons', that a guest's
coordinated save runs this mod's save handlers on the guest's machine (Going Public calls the game's own
`SaveGameManager.Save` there), and that the host's frontier reaches a guest within the update's ten seconds.

## Empire Motors (`Core/EmpireMotors`)

Read in its decompiled code (`evidence/othermods/EmpireMotors`, version 1.20, and its car add-ons `EmpireMustangGT` and
`empirebmwg82` beside it), never run. This mod sits beside it and connects where it can. Since its own dealership
(`Economy/Dealer`, the business type `reallife_businesstype_cardealership`) the two kinds live side by side in one city:
see the last rows of the table below.

What it does:

- **The business.** `ModdingAPI.RegisterModBusinessType` with `empiremotors:businesstype_cardealership`: a retail building,
  no simulator, no customers of the game's (`spawnCustomers` false, no demand sets, no products), staff of customer service
  and cleaning, tagged `allowplayercreation` and `generatesrevenue`. So `BusinessGame.IsBusiness` and `Books.IsBusiness`
  are true for it, and every feature that lists the player's businesses has it (utilities: a retail place; the company:
  well-off customers, as the game's dealership; investors: industry). The game still gives it an order book's day each
  night, empty.
- **The stock.** Bought from wholesale listings, used offers (an inspection fee first) and live auctions against made-up
  bidders, and from customers' trade-ins. Each car is a `VehicleInstance` added to `GameInstance.VehicleInstances` with
  `SetStreetData` to the dealership, made in the showroom at a display point, and kept in its own records by the vehicle's
  id. Reconditioning, inspections, servicing and ageing are its own figures; it prices from `VehicleType.price`.
- **Selling.** A scheduler of its own makes up customers by the hour, more with customer service staff on shift at the
  counter (`EmployeeInstance.IsWorking`); offers and counter-offers, an abstract test drive, trade-ins, its own financing
  (a deposit now, weekly instalments it collects as receivables) and warranty packages (a premium, then claims by day). A
  car sold is paid as `ba:transaction_itemsold` with the dealership's address (with no address and no data when there is a
  trade-in) and taken out of the save.
- **Money.** `GameManager.ChangeMoneySafe` under types of its own, `empiremotors:transaction_...` (purchases, inspections,
  reconditioning, service, supplies, campaigns, a specialism, finance payments, warranty premiums and claims, and a refund of
  each), with no address, none of the game's categories and none marked deductible. So the game's statement and tax count
  nothing it takes or spends.
- **Saves.** One key of the save's `modData`, `empiremotors.save`: Odin's JSON (version 22) of its records, deals,
  receivables, warranties, auctions, campaigns, progression, supply orders and settings.
- **The interface.** A button `EmpireMotors_Btn` copied from the game's template into the full menu's top bar, after
  Market Insider (`SetSiblingIndex`), and a page under `AppsContainer`; opening it switches every other page there off, as
  the game and this mod do for theirs. Its own canvas, an auction tracker on the HUD, the game's own notifications. A
  postfix on `BizManBusiness.SetAddress` is in its code but never applied.
- **The game's own dealership.** Its office desks get a "talk" call to action (an entry in `CtaManager`'s list and a dialog
  in `CallDialogFactory`'s), which opens the game's own vehicle contract with the add-on cars in its list. The purchase is
  the game's (`ContractVehicleForSale`, `ba:transaction_vehiclebought`).
- **The camera and keys.** In any car the player drives, V switches to `indoorVehicleCamera` through `CameraHelper.SetCamera`
  and writes the main camera's place in `LateUpdate`, with Q and E to look aside. F6 (parked showroom cars' physics), F7
  without Shift (the showroom's cars on and off), F9 (alone, with Shift, with Ctrl and Shift: a car added to the stock of
  the dealership one stands in, one taken out, the stock reset) and F12 (a sale now) act always, whatever else is on the key.
- **The add-ons.** Each loads a `VehicleType` and its prefab from its own bundle and registers it; the BMW also puts itself
  on the desk's list (the Mustang is on it by name already). Their lights and the BMW's dashboard read the car's speed. Their
  figures are in their bundles, not in their code.

Where the two meet, and what this mod does:

| Meeting | Before | Now |
| --- | --- | --- |
| A car of the stock and this mod's ownership | taken for the player's own: on the Garage page, a record of wear, registration and insurance when driven, then a reminder a week to register it | `EmpireMotors.Stock` (parked at a dealership, or its id among the saved records): no Garage page, no record, no reminder, as a hire car (`Transport/CarPhone`, `CarOwnership`). Still tuned by its kind |
| Car finance's approval | stock bought while an approval stood was taken for the financed purchase | stock is skipped (`CreditDesk.Purchases`); the player's own purchase at the game's dealership, Empire Motors' desk included, is the game's own and is financed as ever |
| Car hire, custom shop, first licences | stock could go on hire (it would leave the showroom and its records) and into the custom shop, and counted as a car owned | refused with a reason of its own (`EstateGame.MayHire`, `GarageShop.Refusal`); not counted (`LicenceDirector.FirstRun`) |
| Selling a dealership | refused for "a vehicle at the building", which the player cannot move | refused until Empire Motors has sold the stock (`SaleBlock.Stock`); the stock and its records cannot go with the sale |
| The books | a dealership took nothing and paid wages and rent: business pressure's fees at their floor, the company, brands, a sale's value, business loans, the takings a debt is paid from, Flare and the investors saw a loss every day | `GameBooks.Refresh` counts its takings (car sales, finance payments, premiums, less refunds) and its costs (two rows among the goods); what has no address is shared evenly between the player's dealerships; its order book's day gets the takings and a customer a car |
| The game's tax | counted nothing of it | counts the takings in the statement, and `GameBooks.Deduct` gives it the day's costs to deduct as a shop's goods are, so the margin is taxed. Needs Money and credit on, as the mod's own lines do |
| V in a car | this mod's camera key, also V, switched this mod's camera for the whole game on each press; from 0.4.3 to 0.4.5 V in a car was left to Empire Motors, whose view hands the game's camera list to the game's `indoorVehicleCamera` (`CameraHelper.SetCamera`: the `vehicleCamera` at priority 0, and switched off), so the chase camera, which follows the `vehicleCamera`, let go; on a car with no seat of the kind it looks for (the game's own, as far as read) the view never moves the camera at all, which left the game's camera for driving inside a building and nothing for `CameraYield` to see | V in a car is this mod's again: while this mod's camera key is V (with any modifier, as its view reads V with any), its view's component is switched off (`Camera/EmpireView`: the first time the chase camera comes on or the key goes down in a car, a notice once), its own `OnDisable` puts the game's camera back and the camera the game wants is asked for again; it is switched on again when the key is moved off V or this mod is unloaded, and the chase camera stands aside for the view by `CameraYield`'s second finding as before. The one component of another author's this mod switches, on the owner's word: the two views cannot share a key and that mod has no setting for its own |
| F6, F7, F9, F12 | this mod's diagnostic keys F7 and F9 are the same | said beside the key on the options page (`EmpireMotors.KeyUse`); the player moves this mod's key |
| The add-on cars | placed by `CarCatalogue.Guess` from a max speed this mod cannot see beforehand | a row each: Sports, 4.5 s and 4.0 s, 250 km/h |
| The full menu's top bar | its button is one more child of the row | the pages take it in (`PhoneGridPager`, hardened in 0.3.5); it pages nothing and adds no component, so the pager does not step aside |
| Its app page | switches every other page off | the same rule as the game's and this mod's own: the last one opened shows |
| Utilities, the company, the rota | a retail business; its staff are customer service and cleaning | works as it is: the rota covers every station for the hours kept ("the game sends no customers here"), which suits a dealership whose sales need the counter staffed |
| Brands and franchises | a brand of the player's dealerships; a franchise of the type | works: a franchised dealership opens in the books only (the game has no business of the type for a rival) |
| Campaigns and footfall | the Company app sold campaigns for a dealership, and business pressure, the city and brands wrote its promotion figure, which its customers do not follow (Empire Motors makes them up; it reads only the opening hours and a temporary closure) | `BusinessGame.Promoted` is false for a dealership: no campaign can be booked (the app says Empire Motors brings its customers itself), no footfall is written; closures, strikes and opening hours still reach it |
| Online shops | | nothing to sell: it has no products |
| Going Public | | `GoingPublic.Mine` keeps another player's dealership out, as every other business |
| Saves and removal | its key is its own | nothing of this mod's touches `empiremotors.save` or its cars; this mod's records of a car of the stock are dropped when it is sold (`CarGarage.Prune`) |
| This mod's own dealership | | Both business types can be founded and run at once. Each keeps its own cars (by its own records and by the address of its own type), customers, money and app; `Core/Dealerships` answers "is this a dealership's stock?" for both, so every rule above holds for this mod's cars as well. Empire Motors' showroom tools act only in a building of its own type (`EmpireMotorsShowroomService.IsActiveShowroom`), and this mod's lot only in one of this mod's. This mod's dealership gets the game's footfall and campaigns (it is promoted as any shop), which Empire Motors' does not |

Left, and why: its own financing and warranties stay its own (they are the dealership's business with its customers, not
the player's credit); the custom shop has no body fit for the add-on cars (their meshes are in their bundles); a car of the
stock that was driven off the lot is still the dealership's only while its id is in that mod's records; what it books with
no address is shared evenly, as nothing says which dealership it was for; the day's costs are deducted the morning after,
so those of a tax year's last day go to the next year, as the mod's own late lines do.

Diagnostics: `Other mods: 'Empire Motors' is also switched on (assembly EmpireMotors). ...`, with Developer diagnostics
`Economy check (Empire Motors): day d the dealerships took ... and spent ... in the game's statements; ... given to the
game's tax to deduct: PASS.` and `Camera: another mod drives the camera (Empire Motors, by moving the game's camera
itself). ...`. EconomyCheck (`EmpireChecks.cs`) checks which transactions are takings and which costs, the share, a car of
the stock in the saved records and a day's deduction; NavCheck the three assemblies' names.

Proven (read): everything above about Empire Motors. Inferred (not seen with it installed): that its customers' sales
reach the statement on the morning after as the mod's own takings do, that a car of the stock driven off the lot keeps its
street data, and how its button and the pages sit together in the top bar.

## Adding the next one

1. Decompile it into `evidence/othermods/` and read what it does to which object of the game. Never copy from it.
2. Add its line to `OtherMods` (assembly prefix, a type every version has, its name, its kind) and to the table here.
3. If its kind exists, that is all: whoever asks `OtherMods.Of(kind)` or `CameraYield.StoodDown` already gives way.
4. If it is a new kind, decide by rule 2 or rule 4, put the question in the one "Game" file of the feature it touches,
   make the affected options say so (`Feature.StandsAside` or `Feature.Present` in `Meta/FeatureList.cs`), add a
   self-check line, and a test entry that pretends the mod is there if it cannot be staged otherwise.
5. Add offline checks for whatever counting or arithmetic the stand-down has (`NavCheck/PolishCheck.cs`).

## Diagnostics

- `Other mods: 'Voogle Route' is also switched on (assembly VoogleRoute). ...` (always logged, once per mod)
- `Camera: another mod drives the camera (Immersion - RPG Camera, through its own camera '...'). ...` (always logged)
- `Camera check: another mod drives the camera (Immersion - RPG Camera): stood down: PASS.`
- `Camera: nothing else has driven the camera for 90 frames. This mod's camera is back.`
- `Navigation check (other mod): Voogle Route is switched on: ...: PASS.`
- Options > Test tools > Camera > "Test: pretend another mod has the camera" runs the whole stand-down with nothing
  installed.

Proven (read in the decompiled code): the game's two priorities, that the game never writes the main camera's
transform, the camera mod's priorities and its key, where the route mod draws. Inferred (not seen in the game): that
the stand-down leaves no shake with the camera mod on, and how the unusable options look.
