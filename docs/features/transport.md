# Transport

## In a sentence

Bus stops and buses the mod drives itself, the game's subway taking a journey's time, and owning a car: wear, fuel, breakdowns, insurance and roadside help. The taxi you call by phone has [a page of its own](taxi.md).

## Where the code is

- Folder: [`Scripts/Transport`](../../Assets/Mods/RealLifeOverhaul/Scripts/Transport/), with the taxi in [`Taxi`](../../Assets/Mods/RealLifeOverhaul/Scripts/Transport/Taxi/).
- Read first: `TransitMap.cs` (the bus network), `TransitDirector.cs` (what of it is on the street), `TransitDirector.Player.cs` (the rider's side, in the game's own interface) and `CarOwnership.cs`.

| File | What it is |
| --- | --- |
| `TransitMap` | the bus network: six routes between subway stations along the road graph, stops by rule; built once per city load, in memory only |
| `TransitDirector` | what of it is on the street: three dressed stops, the stop that is served, its people, the bus, the player's ride; the test buttons |
| `BusStopDressing` | one stop's pole, flag, shelter and bench (modelled props when the bundle has them, plain shapes to the same size until then); on the city map every stop is a place of the "Bus stops" row (World/MapLinks) |
| `TransitBus` | one bus: a van from the traffic pool with a route board, driven by the traffic system on a scripted route, stopped "in point" at the stop |
| `BusBay` | the geometry of a bus bay and nothing else (game-free: NavCheck drives a model bus through it): where the bus stands, the curves in and out |
| `TransitBays` | a bay on the street as it is now: whether its length of the strip is free; the game's parked cars standing in it out of view are taken in |
| `TransitRide` | the time a ride takes (fade, the game's time machine, put down at the far end), for bus and subway; TransitState: day pass, ride counts, a fare owed back, in modData |
| `BusRider` | the bus ride seen: the game's walk to the front door, the fare, board_step up, the validator, the aisle, a seat (strap_hang on a full bus), the pull-away; at the far end up and off by the middle door; every way out |
| `IDrivenVehicle` | what BusDriver drives: the city bus and the tow truck, measured from their own middles |
| `BusInside` | the inside of the bus as a rider walks it (game-free: EconomyCheck walks every path; PropAssets/checks_service.py measures each place on the mesh), and the ride's fare and fade rules |
| `SubwayJourney` | the game's own subway taking a journey's time: the panel at each entrance, service by the clock (frequency, delays), the day pass, the City Transit contact |
| `CarDash` | what a car needs as one more reading in the game's own vehicle panel (a copy of its "Condition" reading) |
| `CarGarage` | CarRecord (per owned car, in modData by vehicle id), CarRules (every price, interval and chance; game-free) |
| `CarOwnership` | the driven car: distance, wear felt through grip and brake torque, thirst, breakdowns, what is due in a repair bay (for Garage/GarageBay), the game's panel over a broken-down car, insurance payouts, the daily round |
| `CarRoadside` | a mechanic who walks up, kneels at the car and walks off: breakdowns and fuel brought to the roadside |
| `CarPhone` | the Garage page and Roadside Assistance contacts |
| `Taxi/TaxiRules` | the taxi called from the phone, in plain numbers (game-free: NavCheck runs them): the call-out, a cancel, nobody came, the start bands, the quoted wait, the refusals, the watchdog |
| `Taxi/TaxiRoads` | which lane is the caller's, where out of view a taxi can be put, and the way between (one search over the shared road graph); TaxiBridges; TaxiPlan |
| `Taxi/TaxiCab` | the taxi itself: one of the game's own, taken from the traffic pool, driven to the caller by the mod's driver, stood there for them, let go in every ending |
| `Taxi/TaxiDirector` | one call at a time: quote, send, watch, arrival, the wait, and how a call ends (the game's ride, nobody came, a cancel, it cannot come); the test buttons |
| `Taxi/TaxiPhone` | the City Cabs contact and its conversation |

## How it talks to other features

- The bus is driven by the same driver as the response vehicles (`Traffic/EmergencyDriver`); the road search is `Core/RouteSearch.cs`, shared with the GPS.
- `TransitDirector.Riding` hides the minimap and keeps the taxi away (`Wiring/NavLinks.cs`, `TaxiLinks.cs`).
- The Garage's parts change wear and insurance through `GarageBridges.WearOf` and `InsuranceOf`.
- City hall's service policy scales the bus headway (`TransitDirector.Headway`).

What the game already has, and is left exactly as it is:

- **the subway**: twenty `SubwayStation` entrances in the street; click one, pay 3, the map opens in subway mode, pick a station, a camera flies there in seconds with no time passing;
- **taxis**: 0.15 a metre, 0.04 game minutes a metre, a fade and the time machine;
- a private driver;
- **fuel**: a tank per vehicle type, 1.30 a litre at a filling station, a 10-litre can for 30, a to-do at 10 %, the engine stops when dry;
- **bodywork damage**: repaired in a station's repair bay for damage x half the car's price; at 100 % the car will not drive;
- **dirt**: washed in the same bay for 50;
- **parking tickets and towing** (to `ba:towdestination_autorepairshop`).

The game has no bus, no bus stop and no vehicle interiors.

## Buses

**Why the bus of 0.43.0 never came, read from the traffic system's code.** It was a van handed to the traffic AI with a scripted route (`VehicleComponent.presetPath`), "the way the game drives its private-driver car".
Three things in that:

- *A scripted route does not obey lights or give-way points, and is crippled by the first one it meets.* A vehicle with a scripted route is given its next point as soon as it is within 5 m of the last (`TrafficManager.FixedUpdate` calls `DrivingAI.WaypointRequested`, which returns right after `SetNextWaypoint(presetPath[0])`), before the code that holds a vehicle at a red light (`activeActions` has `StopInPoint`: return) or asks a junction whether it may enter (`GiveWay`) is ever reached.

  The stop order a red light puts on a vehicle is lifted only for vehicles whose target is still the light's own point (`WaypointManager.TrafficLightChanged`); the scripted vehicle's target has moved on, so its order is never lifted, and a give-way order is lifted only in the code it never reaches.

  From its first red light or unsignalled junction on it drives in `DriveJob.StopInPoint`: braking for every lane point in turn, and the first time it is slower than one brake step within 10 m of its next point (behind a queue, say) it is stopped for good.
  After 100 s the mod let it go as traffic.
  (The game's own private driver has the same flaw; its routes are at most fifty points long.)

- *It needed an idle van by luck.* Only a van of the pool that was switched off would do, and the place it started from had to be free of traffic two car lengths each way, at one fixed lane point; every refusal was answered by trying exactly the same again ten seconds later.
- *The figure jumped because it was not a measurement.* It was a countdown to "due", put back up to 34 s at every refusal (and the refusals were logged only when a counter happened to line up: one chance in eight), counted down to nothing while the bus crept, and turned into game minutes with a clock rate measured over two seconds.

What cannot be told from the one log there is: its last transit line is the only bus that was ever put on the road that session, and the log ends before either "stands at its stop" or the 100 s "did not reach its stop" line, so that one bus was either still crawling when the game was closed or its vehicle was taken (which was not logged).

- **The network** (`TransitMap`, `RouteSearch`) is laid out by rule in the first seconds out of doors after a city load and kept in memory only.
  The ends of the routes are the game's subway stations in the order of `SubwayStationName`: up to six times over, the first station not yet used, and the unused station furthest from it (500 m to 2.8 km) that can be driven to **and back from**.

  A line's end is any plain lane point within 150 m of its station (0.43.0 took the one nearest point, which is often on a lane that only leads away: that, and never changing lane, is why six directions had "no way along the road graph").

  The way is the shortest along the lanes (A* from all of one end's points to any of the other's), with a change of lane allowed where the road graph offers one between two plain lanes running the same way, at the price of 30 m.

  A failed search says how far it got and whether it ran out of road or hit its limit, and every partner it never got near is dropped without a search of its own.
  Only a station with no partner both ways gets a line one way.
  `Transit check (network)` is the summary.

  A stop is looked for every 260 m in the 140 m after that, at the first lane point that is on a road proper, at street level, and straight (under 9 degrees over six points).
  It must have no junction, give-way or lane beside it on the kerb side within 26 m of line either way.

  It must have pavement 1.3 to 5.6 m to its right that is 1.5 m deep, with nothing static and no door within 2.4 m where the pole and the waiting people stand.

  Where the pavement is also bare and deep 3.2 m further on there is room for a shelter.
  A stop is named after the nearest building's address.

  Of the places that will do, one with a **bay** (below) is taken before one without: first in the 140 m, then up to 60 m further on, then up to 50 m before the place wanted (so two stops are never nearer than 210 m of line), and only then the first place without one, which is the place that was always taken.

  `Transit check (network)` counts the stops with a bay and gives every stop's kerb distance, with a word for why a stop has none.

- **Bays** (`BusBay`, `TransitBays`; the maintainer's report: "Busses are blocking traffic").
  The road and the pavement are the game's meshes and cannot be cut, so a bay is made of room that is there: the strip between a driving lane and the kerb where the game parks cars.
  - *Where there is one.* A stop has a bay where the kerb is at least 4.10 m from the lane's line (the lane's half of 1.65, 0.15 m clear of its edge, the 2.10 m body, 0.20 m to the kerb: `BusBay.KerbNeeded`), measured at the kerb that is there (the first step up of the road surface, where that comes before the navigation mesh's edge).

    The lane runs straight (its points within 0.35 m of its line) and plain, with no other lane between it and the kerb, for the whole run.
    And the strip is road at the lane's level, with the kerb keeping its line and nothing fixed standing in it, from about 17 m behind the pole to 16 m ahead of it (more where the strip is wider: the bus crosses further).

    A stop records where the bus stands (`Bay`, `BayOffset`), how much strip it takes (`BayBehind`, `BayAhead`) and how far back the strip will do for a vehicle with a longer nose (`BayRoom`).

  - *Why a bay is that long.* The driver turns a vehicle about its own origin (under the middle of a van), so the 3.3 m of bus ahead of that point swings towards the kerb as it comes in at an angle and the 3.3 m behind swings towards it as it turns out.

    A body that stands 0.2 m from the kerb has room for neither unless the angle falls away as fast as the gap closes: the last metres in and the first metres out are an exponential whose length constant is about that lever.
    The rest is a straight run across the strip at 16 degrees (up to 22 where the strip is wide) and three metres of easing off the lane's line.

    A vehicle whose curve will not fit the stop's bay (a long nose ahead of a rear axle on a wide strip) is given the lane's line instead, and the log says so.

  - *In.* The end of the bus's route is the curve in (a point of the mod's own every metre), the standing place and the point beyond it that the driver stops 1.2 m short of.

    From 6 m before the curve the driver is told to take a point as reached at 2 m, not 4.5 (`EmergencyDriver.Reach`: on the open lane it cuts every corner by 4.5 m, which here would be the kerb).
    It is also told where the kerb is (`EmergencyDriver.Kerb`).

    Its look ahead runs along its nose, which points at the pavement while it turns in; without that the people waiting at the stop, or the player standing at the kerb, would stop it half way across.

    With the kerb known, what the sweep finds beyond it does not count and nobody of the mod's people beyond it counts (`PeopleAhead`); everything on the carriageway counts as ever.
    It comes down to 4.5 m/s by where the curve begins (2.5 m/s2).
    Held up on the last 9 m by something that is in the bay after all, it stops there and that is its stand.

  - *Standing, and why traffic passes.* The game's drivers do not overtake a standing vehicle (nothing in the traffic system's code does).
    Each sees what is ahead with one trigger box on its nose, as wide as the vehicle itself (`VehicleComponent.OnTriggerEnter`, `colliderWidth` from that box; the box is turned with the steering, `UpdateTriggerJob`), and stops for any solid collider of a vehicle that the box touches.

    A bus in its bay has its nearest side 1.80 m or more from the lane's line; the widest thing the game drives on that line is taken as 2.60 m (`BusBay.WidestHalf`), which reaches 1.30 m.
    So the box of whatever passes never touches the bus, and nothing is switched off or excluded to make that so: the bus keeps its colliders, and a car that leaves its lane still stops for it.

  - *Out.* Doors closed, it stays where it is, frozen, and gives way: it goes when the lane is clear from 30 m behind its tail to 5 m beyond its nose (one box, `SceneParking.InStrip`, four times a second while it waits and never otherwise).

    After 15 s, when it is clear from 12 m behind (it edges out, and what comes up behind it sees it in its box and slows, as for any vehicle ahead).
    Never while something is beside it.
    No gap in 45 s and it is let go where it stands, out of the lane.
    Nothing is ever taken off the road to make a gap.

    The curve out is laid from where it really stands, so a bus that stopped short and askew gets out too.

  - *Kept free.* The game fills the strip by chance (`ParkingLaneGenerator`: a spot in four or so, again each day and when a business opens or shuts, never within 80 m of the player).

    When a stop is dressed and when a bus is put on the road for it, the game's parked cars for show that stand in the bay **out of view** are handed back to the game's own store of them, the way the game takes them in (the lane's `onReleaseVehicle`, then `ParkingSimulator.ReleaseParkedVehicle`).

    Nothing in view is touched, and nothing else at all: a vehicle of the player's, a delivery van, a wreck or a cart leaves the bay taken, the bus stops on the lane's line as it used to, and the bay is looked at again for the next bus.

    It is looked at once more as the bus comes up, 16 m before its curve; taken by then, the end of its route is put back on the lane's line.

  - *Marked.* A dressed stop with a bay has a bus stop's box painted on the road round where the bus stands (`BusStopDressing.Paint`, `RoadPaint`: decal projectors with copies of the game's own road paint material).

    It is a broken yellow line along its lane side from 3 m behind the bus to 2 m ahead of it, a taper in to the kerb over 4 m at each end (some 24 m in all) and the words BUS STOP between the line and the kerb, read the way the traffic runs.

    It is laid to the road's own fall, made once per dressing and gone with it.

    The rest of the zone, where the bus comes in and pulls out, has its parking cut back and no paint.
    Where the city has no road paint to copy, four yellow strips.
    Nothing solid.
    (The mod's props are queued as HDRP queues its own lit materials, 2225, so the paint lands on the road and not on a bus standing over it: `Shapes.TakesDecalsQueue`.)

  - *In the lane.* Where there is no bay, or it is taken, the bus stands on the lane's line as before, and for less time: 4 s where nobody is waiting or walking up, nobody gets out and the player is not within 35 m (`DwellEmpty`), 9 s otherwise, and up to 34 s only while the player is on foot within 35 m.
  - *Checked outside the game* (`NavCheck`, `BayCheck.cs`): the arithmetic, the two curves, and a bus driven in, stood and driven out by a model of the driver's steering and pacing.
    The runs are for the body on its van, on a van whose origin is 0.4 m behind its middle and on a vehicle turned about its rear axle, at four kerb distances.

    They are run with the physics step halved and doubled, coming up off the line and askew, slower and faster, and standing askew part of the way across.

    `NavCheck.dll --bays` prints what each run measured.
    In the model the body on its van stands within 3 cm of the bay's line and 1.3 degrees of it, its nearest corner 0.06 m clear of the lane's edge at the narrowest bay, never nearer the kerb than 0.13 m coming in and 0.06 m pulling out.
    The model is not the game's physics: `Transit check (arrival)` says what the real vehicle did.

- **A stop is solid** (`BusStopDressing`).
  From 0.2 every stop of the city is put up once, nearest first, two a second, when the network is ready, and stays up while the city is loaded (no waiting for the player to look away, nothing taken in behind them; the navigation mesh is still cut only near a player on foot).

  Before that: The three nearest stops within 190 m are dressed, out of view if a wait of three seconds allows it and at once after a load or an arrival (230 m to be taken in), from the modelled `bus_stop_sign` and `bus_shelter` (plain shapes of the same size without the bundle).

  Everyone on foot is moved on the navigation mesh, so the pole, the shelter's back, its end panel, the screen at its other end and the bench each have a carving `NavMeshObstacle` (the cordon's mechanism): children of the dressing, laid out while it is switched off, cut once when it is switched on, gone with it.

  A cut is wider than its box by the walker's radius (asked of the player's navigation agent type), so the boxes are the solid things themselves and the bench's is that much smaller than the bench: the open side stays open, the place in front of each seat stays on the mesh, and the 1.69 m of pavement between the shelter's posts and the kerb stays a way past.

  That way past is tested on the mesh before the shelter is shown; where it is not one strip, the stop is a pole only.
  `Transit check (stop dressed)` says, 1.6 s later, what the mesh says was really cut, and what the stop's bay is: its length, where the bus stands in it, free or taken by what, how many parked cars were taken in out of view, and that the marking was made.

  (This is the one place the mod cuts the mesh on a pavement: the rule against it in [Performance](../reference/performance.md#the-rules-in-short) is about closing a pavement, and this leaves it open.)

- **The bench** has two seats (never one for two), the sitter's feet 0.30 m in front of the seat's middle line, facing the road: what `sit_bench`, `sit_wait` and `sit_bench_up` are made for (seat 0.46 m up, as the model's).

  The player is offered "Sit down" within 1.5 m of a free seat, as a button of the stop's panel, and "Click to sit down" on the shelter from anywhere in view (the game walks them to the seat first): `TransitSeat`, held by the game's own navigation blocker, moved the last step and turned over a third of a second, then the three clips on the animator the way leisure plays its clips.

  Any key or click gets them up, and so does the bus arriving.
  The game's own seating is not used: it exists only inside its rest and entertainment activities, which run the clock.
  Somebody waiting may take the other seat (walks to it, sits, gets up when the bus stands); those standing wait by the pole, outside the shelter.

- **Service**: the nearest dressed stop within 80 m is served (a line's last stop too: the bus that ends there takes riders on, see Riding), and a dressed stop the player walks up to on foot takes the service over from another at once.
  Up to two people walk up from out of view.

  A bus comes every 150 s by day (from 0.1.4 never later than 20 minutes of the game clock, the drive in counted: `Wait`; and it stands up to 34 s for a player within 35 m), 100 s in the weekday rush (7 to 9, 16 to 19), 210 s in the evening, 220 s early, 360 s at night, a third longer in the rain.

  In the rush one bus in four is full.

- **The bus** (`TransitBus`) is a vehicle of the traffic pool wearing the modelled `minibus` (hidden under it wholly; without the bundle, a van with a lit route board).
  The body is made to the Mersaidi Dash (6.66 m, axles 4.02 m apart) and is put with its axles on the vehicle's own, read from its wheel meshes (`TrafficTools.Axles`), so on a Dash its wheels are exactly where the van's are.

  A Dash is taken before a van with another wheelbase, and that before any other vehicle (the log's `chassis` says which, and how far the axles are off).

  It is held the way a response unit is (ignored by the traffic AI, an empty scripted route as the mark that keeps the game from taking it off the road) and **driven by the mod** with `EmergencyDriver` along the line's own lane points: 40 km/h, 25 through junctions, stopping behind whatever is ahead, never out of its lane (`PullOver` with next to nothing: a bus passes nobody), to within a metre of the end of its route.

  What that driver does not know of this class adds.
  It pulls up at a red light (the light's own lane point, `Waypoint.stop`, within 170 m of the player; the game only runs the lights near the player, so a light further off, or one out of view that has not changed in 45 s, is not waited at).

  It stops two points before any lane point a scene has closed (`temporaryDisabled`: the cordon, a lane closed for roadworks), frozen while it stands, and goes on when they open.

  Which vehicle is not luck: an idle van.
  Else a van driving out of sight (taken off the road there).
  Else, under the modelled body only, any ordinary car or truck of 4.2 to 7.5 m in the same two ways.
  Never an ambulance, a taxi, a limousine, a private driver or a vehicle with a scripted route or held by a scene.

  Where it starts: a plain lane point of its line about 170 m before the stop and out of view, then others further back (to 330 m) and nearer (to 60 m), 14 m apart, up to eight tried for one free of traffic, none before a closed road.

- **It comes** (`TransitDirector`).
  When the timetable's wait is down to 30 s a bus is put on the road; a refusal is tried again every 2 s (from the third on it may start in view, more than 60 m from the player), logged at the first and every fifteenth, and the panel says DELAYED after six seconds of it.

  On the road it is watched: with no metre of progress for 40 s while out of view and more than 110 m off it is moved up its line, past what holds it (`Transit check (re-plan)`).
  With none for 150 s, or after 300 s out, it is given up with a notice and the next is sent.
  If the game takes its vehicle another is sent at once.

- **The figure** (`TransitEta`, run in a console test) only goes down.
  Before a bus is on the road it is the timetable's wait ("next bus in about N min"); with one on the road it is that bus's metres of route still to drive over the speed it has made while moving ("the bus is 120 m away, about 1 min"), never more than was shown before.

  When that cannot be kept (it stands more than 12 s, is 12 s behind for 6 s, was lost, none could set out) the panel says DELAYED and why, with no figure; when the bus has been moving again for two seconds one new figure is given from where it is.

  The game minutes shown are that figure at the measured rate of the game's clock, and are not allowed to rise for the rate's wobble either.

- **At the stop** it stands with the middle of its body (its door) beside the pole, door side to the kerb, frozen: in the stop's bay where there is one and it is free (0.2 m from the kerb, out of the lane), else on the lane's line.
  For 9 to 34 s (4 s in the lane where nobody has business with it).

  `Transit check (arrival)` gives metres along and aside and degrees off and, in a bay, the metres between the body's nearest corner and the kerb and between its nearest corner and the lane's edge (PASS: within 2.5 m of the pole, 3 degrees, nothing of it in the lane or over the kerb; on the lane's line, as before: 2.5 m, 1.2 m aside, 12 degrees).

  `(doors)` the place somebody steps in from (0.30 m outside the body at the door 0.76 m ahead of its middle, as the model documents and `board_step` wants: the step's edge 0.30 to 0.35 m ahead of the feet; up to 0.9 m further out if the mesh does not come that close) and whether anything stands between it and the kerb.

  Somebody may get out and walk off; those waiting get up, walk to the door one at a time and step in (`board_step`; the van: `job_car_enter`); `(departure)` counts them and, for a bus in its bay, says how many seconds it waited for the lane to be clear (the line is written when it goes).

  It follows the next 240 m of its line and is then taken off the road if nobody is looking, or handed back to the traffic AI as it is.
  The bus a rider sees pull away after a ride is put on the lane just past the bay's end, where one that has pulled out is.

- **Riding** (`TransitDirector.Player.cs`): everything the player is shown is the game's own interface (see [`RealLifeOverhaul.Build/docs/game-ui.md`](../../RealLifeOverhaul.Build/docs/game-ui.md), sections 9 to 11), and nothing of it runs per frame.
  - *Pointing.* The pole, the shelter and the standing bus each have a `TransitPointer`: an `EntityController` of the mod's on a trigger box on the game's layer for interactive items, with one entry at the front of the game's list of calls to action (`TransitCta`, as `HomeOffers`).

    The game outlines the stop, names it and offers "Click to choose a stop", "Click to sit down" or "Click to board" from anywhere in view, as it does for its own objects; a click from further off than reach walks the player over with the game's `PlayerController.SetGoal` and acts on arrival.

    The bus's box is the director's own, put where the bus stands (`TrafficTools.Footprint`) and switched off when it goes: nothing is added to the pooled vehicle.

  - *At the stop.* Within 3 m (`Reach`: the game's `EntityController.MaxReachDistance`) of the pole, the waiting place, the shelter's middle or the standing bus's body, the game's panel over a place (`GameUi.WorldPanel`: the box of a petrol pump) shows "Bus stop: name", the line (or "Your stop: name" once one is picked, or that the line ends here) and, right, the minutes, "At the stop", "Full" or "Delayed".

    Its buttons carry the game's own keys: "Choose a stop" on Interact ("Board: choose a stop" while a bus with room stands, "Change your stop" once one is picked), "Sit down" on SecondaryInteract while a free seat is within 1.5 m, "Get up" while seated.
    The earlier figures (panel at 13 m, boarding at 16 m, then 6 m from the bus) are gone.

  - *Where to.* On the city map, as the game offers its subway stations from an entrance (`GameUi.MapPicker`): the map opens over the stop, its filters are put away as the game puts them away for its stations, and so are the mod's own places (`MapPlaces.Quiet`).
    A copy of the game's window for its stations stands where the game's does, on the map's canvas, with the game's search field (it narrows the entries by name).

    Every stop offered is also one of the game's markers on the map, in the bus stops' colour, named while pointed at and picked by a click; the stop the player is at is marked in the game's red.
    Pointing at an entry glides the map to its stop after the game's own delay (0.2 s), as the game's entries do.

    The map holds the game's clock still, so a pick closes the map and is taken up on the director's next tick, once the game runs again.

    The window offers every stop of the network (`TransitTrips`), as the subway offers every station.
    First come the stops further along this line in the order the bus calls at them (the same bus: 0.06 game minutes a metre of line plus a minute a stop, times the game-speed setting).

    Then every other stop, nearest first, reached with a change of bus that is not played out (the straight line times 1.35 at the same rate, a minute a stop at one per 330 m, and 6 minutes for the change).

    Not offered: the stop itself and a stop of another line within 150 m, which is walked to.
    Each entry has the stop's name, its line and where that goes, the minutes and the fare; the first entry sells the day pass (7.50, buses and subway until midnight).
    One fare (2.75) covers a ride, the change included, as the subway's 3 covers any station.

  - *Boarding.* Picked while a bus with room stands, the fare is paid (`pay_tap`) and the ride begins.
    Picked while waiting, the stop is kept (`_booked`, a notice says so) and the player boards by themselves when a bus with room has stood for a second and they are at the stop or the bus; it is forgotten when the service at the stop ends (105 m away, or another stop walked up to).

    The bus waits up to 30 s under an open list, and for as long as the map is open (the map stops the game's clock).

  - *The ride* is unchanged: a fade with the game's time machine exactly as the game's taxi does it (`TaxiSystem.TravelCoroutine`), the put-down at the far stop's waiting place, a bus pulling away there.
    `Transit check (ride)` names the stops called at, or says "with a change of bus" with the metres of road it counted; `(put-down)` how far from the waiting place the player stands and whether that is pavement.
  - *If the game's parts are not there* (one `Game UI:` or `Transit:` warning each): without the list of calls to action the stops cannot be clicked and the panel still works.

    Without the subway's window on the map (or when the game will not open its map now: placement mode, say) the same list stands in the middle of the HUD (`GameUi.TravelList`), and without that the game's own yes/no question offers the one ride to the end of the line.
    If the game's panel is showing another place the stop's is tried again every quarter second and `(stop panel)` says FAIL meanwhile.

    `strap_hang` is not used: there is no interior to stand in.

- **Test buttons**: "dress the nearest bus stop" (in view; its check line follows), "send a bus to the nearest stop now" (a stop within 100 m; out of view if it can, else in view far off; says exactly what stopped it otherwise), the ride, the subway journey.

**Lines to judge a run by** (`Player.log`, one each): `Transit check (network)`, `(stop dressed)`, `(seat)`, `(bus assigned)`, `(en route)` each minute, `(re-plan)`, `(arrival)`, `(doors)`, `(departure)`, `(ride)`, `(put-down)`.

For the player's side `(pointer)` (once when the click actions are added, then at every click on a stop, a bench or the bus), `(stop panel)` each time the panel comes up, `(stop map)` (or `(stop list)`) at every opening and closing, `(choice)` at every pick and for the day pass, `(subway journey)` when a ride of the game's comes up; and `Transit: no bus could be put on the road ... yet (try N): <why>`.

**Not verified in the game** (it could not be run): everything about how the game's interface looks with the stop's content in it (where the panel stands over the pole, how wide its second line makes the box, the list in the middle of the HUD, two lines in an entry made for one; the copy of the map's window beside the game's own, the markers' name plates while pointed at, the glide).

That the game's pointer reaches a trigger box under the mod's camera (the game's physics has queries hit triggers, and the cursor is locked to the middle of the screen, not hidden, so the game's own ray is cast from the crosshair).
That the outline pass draws the mod's props when they are put on the game's outlined layer.

That the cancel key taken by the list does not also open the game's menu (the list's script runs first and resets the action, as the game does).
That a cut in the mesh is wider than its box by the walker's radius (if it is not, the bench's box is smaller than the bench by that much: `(stop dressed)` says "bench no").

That the navigation blocker also holds the player against the mod's own direct controls (any movement key gets them up anyway).
Where on the vehicle its origin is (the body is stood on the lowest point of what the vehicle shows; `(bus assigned)` gives the height and how far the body reaches beyond the vehicle's colliders).

The van's own size (the model's notes assume 6.8 x 2.26 m with a 4.02 m wheelbase; the log line gives the real figures).
Whether the lanes the road graph offers as "other lanes" are always same-way lanes (they are only used where their directions agree within 45 degrees).
How long the game's lights stay red.

Whether Industry City can be reached by lanes at all (if not, its stations simply get no line, and the pairing lines say so).

## Subway

The game's own subway is left as it is: click an entrance, its map opens in subway mode, pick a station, pay its 3 (`ba:transaction_subwayride`), and a camera flies there.
Standing within 7.5 m of an entrance on foot, the game's panel over a place (`GameUi.WorldPanel`) shows the station, how often trains run and the state of the service.

It has no button: an earlier version put a list of stations of its own on the interact key there, and players met that instead of the game's map.
Service follows the game's clock, the same on every load of the same day: a train every 3 minutes in the weekday rush, 5 by day, 7 from 20:00, 8 from 05:00, 12 at night.

In one three-hour window in eight there are delays of 6 to 14 minutes, which arrive as texts from the City Transit contact (it can also be called for the state of the service and a day pass).
The journey is the wait (a part of the interval, plus delays) and 2 minutes a stop (one per 650 m, at least one).

When the game's ride is over (`SubwaySystem.IsRiding` falls; its two stations are read as it rises), what the journey takes beyond the minutes the game's ride took is run under the bus ride's fade with the time machine (`TransitRide`, without the card's tap), and a note says where the player came up and how long the journey took.
The game's own ride gives its subway mood.

A day pass gives the game's fare back as the ride begins (`reallife_transaction_subway_pass`); civic life counts such a ride as not paid for, and counts no ride while a train is under way.
The night closure of a station for track work went with the mod's own list: the game's map offers every station and cannot be told that one is shut.

With "Subway journeys take time" off the game's subway is as it is, and a day pass still gives its fare back.
No late-night crime is tied in: the crime system has no call for staging something on the player other than its test button.

## Car ownership

Per car in the save's own list of the player's vehicles, a record in modData (`reallife:garage`, by vehicle instance id; dropped at load, at save and daily for cars the save no longer has).
All figures are in `CarRules`.
Prices are shares of the car's list price (`VehicleType.price`) between a floor and a ceiling.

The game's own figures, read from its data files: cars cost 2,500 (Honza Mimic), 8,000 (UMC Desert), 11,000 (Vord Tiara Vic), 44,000 to 65,000 (V150, BIMA 320, Mersaidi Dash, Nunavut), 72,500 to 98,000 (the trucks, S500), 130,000 to 250,000 (Ferdinand, Lytte, Pony, MGA GT, limousine), 520,000 (Anselmo) and 720,000 (Petrolls Fanton).
Tanks hold 55 to 80 litres (cars), 110 to 185 (SUVs, vans) and 250 to 378 (trucks) at 1.30 a litre.

Repairing a wreck is half the list price.
So a Honza is 120 to service, 180 for tyres, 90 for brakes, 40 a period to insure and 65 to register; a BIMA 320 is 575, 770, 385, 290 and 155; anything from 120,000 up is 900, 1,400, 700 and 300, with insurance from 780 (Ferdinand) to the ceiling of 2,500 (above 417,000).

| what | rule | cost |
|---|---|---|
| service | every 40 km (sports), 50 (luxury, trucks), 60 (others), 70 (economy) | 1.2 % of list price, 120 to 900 |
| tyres | last 120 km (sports), 200 (vans, trucks), 180 (others); below 30 % grip falls, to 88 % when bald | 1.6 %, 180 to 1,400 |
| brakes | last 110 km (sports), 120 (trucks), 150 (others); below 30 % brake torque falls, to 80 % | 0.8 %, 90 to 700 |
| breakdown | none until the service is overdue; then 0.4 % a km rising to 3 % a km a whole interval late; +1 % a km with 70 % damage | mechanic 95 (half if insured), or a tow 180 |
| fuel | the game's, used at 0.8 (economy) to 1.6 (truck) of its one rate; ten litres brought to the roadside | 34 + 10 x 2.60 = 60 |
| insurance | per car, 28 days, renews itself; pays 80 % of a repair bill over the first 100 | 0.6 % of list price, 40 to 2,500, +25 % per claim (a claim is forgotten after 56 days) |
| registration and inspection | once a game year (`daysPerYear`), renewable from 30 days before | 60 + 0.2 % (300 at most), 75 more when late |

A service is asked for standing in a filling station's repair bay (the game's own bay, trucks in the truck bay).
The game shows its own panel there ("Repair [F]", "Wash [P]": `GasStationOverlay` in the game's box over a place); `Garage/GarageBay` gives that panel a second line (what is due and what it comes to, `CarOwnership.BayLine`) and a third button, "Garage", on the game's confirm key.

Its list (`GarageList`) has the service as its first entry with every part of the bill and the time; "Order" has it all done behind a fade while 40 to 85 minutes pass, and tyres and brakes under 35 % are changed with it.

Worn tyres and brakes act through the wheels' own grip figures and the brakes' own torque; thirst through the fuel system's own multiplier; a breakdown through the car's own input hook (no pedal; the brakes work; hazard lights on).
Each is recorded before it is touched and put back when the player gets out, changes car, switches the option off, or the mod unloads.
"Easy car ownership" stops fuel use, wear and breakdowns.

The tank is the game's to show (its vehicle panel has the figure, orange from 50 % and red from 30 %), so the mod draws no gauge.
What the game does not know is one more reading in that panel, after "Condition" (`CarDash`, a copy of that reading made once per city): SERVICE DUE, TYRES WORN, BRAKES WORN in the game's orange, BREAKDOWN in its red, switched off while nothing needs attention.

Broken down or out of fuel and standing still anywhere but in a bay, the game's panel over a place stands over the car (`GameUi.WorldPanel`) with roadside assistance or a can of fuel on the game's interact key; `CarOwnership.TakesInteractKey` is true while it is up.

**How the game's bay panel is extended.** The game keeps one `GasStationOverlay` in a public field (`OverlayUI.gasStation`) and shows it through the interface `UI.Overlays.IOverlay`.
`RepairBayOverlay` derives from the game's class and implements the interface again, so the box's questions (second line, buttons) are answered by the mod while everything the game does with its own object (which station, repair, wash, refuelling) stays the game's code.

`GarageBay` puts one in the field the first time the box is down and puts the game's own back when the Garage goes.
In a bay the game's first button is named `RepairVehicle`: that is how the mod knows it is one, and of the vehicle's kind.

The game's interact keys are its own repair and wash and its special key also parks the car, so the workshop is on the confirm key; the game writes the key on the button, as on its own.

With "Developer diagnostics" on: `Garage check (bay)` when the panel is extended and when the button is first offered in a bay, `(workshop)` when the list opens (with what is due) and closes (with why), `(choice)` for each entry used, `Car check (dash)` when the reading changes, `(roadside panel)` when the panel over the car comes up or goes.

Not verified in the game: how the box looks with a second line and three buttons; that the confirm key is not taken by anything else while seated in a bay.

## Asset wish-list (drops in by id, no code change)

Props (`PropAssets`, metres, standing on their origin):

| id | size | pivot and facing | notes |
|---|---|---|---|
| `bus_stop_sign` | 0.60 x 2.85 x 0.07 | ground centre of the pole; the flag hangs to +X (away from the road), its faces to +Z and -Z | pole 0.07, flag 0.50 x 0.62 with its middle 2.42 up, a timetable case at 1.45; the route number is lettered by code at (0.30, 2.53) on both faces in a box 0.44 x 0.34 (the model's plain blue field is from 2.33 m up): leave the upper half of the flag plain |
| `bus_shelter` | 1.70 x 2.50 x 3.76 | ground centre of the floor (1.4 deep, 3.6 long); length along Z, open to -X, back at +X | bench seat 0.46 up along the back from z -1.3 to +0.6, end panel (route map, advert) at +Z; glass may be opaque |
| `city_bus` and its parts | 2.40 x 3.05 x 10.90 | ground centre under the middle of the bus, nose to +Z | **delivered**: the mod's own bus (`Transport/BusVehicle`), its body with the wheels, the door leaves and the lit lamps as props of their own that the code puts together ([`PropAssets/README.md`](../../PropAssets/README.md), "Service vehicles"); the shuttle body `minibus` it replaced is gone |
| `turnstile` | 0.30 x 1.00 x 1.20 | ground centre, reader on top at 0.975 | for a subway entrance, if entrances are ever dressed |
| `fuel_pump`, `jerry_can` | 0.5 x 1.6 x 0.4; 0.35 x 0.45 x 0.18 | ground centre; can: handle on top | the mechanic's can for fuel brought to the roadside |
| `warning_triangle` | 0.45 x 0.42 x 0.05 | ground centre, faces +Z, glow | stood behind a broken-down car |
| `car_wash_arch` | 3.6 x 3.2 x 1.0 | ground centre, drive through along Z | the game already washes cars in its repair bays |

Clips (`CityAssets`, Humanoid, in place, starting and ending in the standing idle):

| id | length | loop | what |
|---|---|---|---|
| `board_step` | 1.4 s | once | a step up through a bus door 0.35 m ahead, hand to a rail at shoulder height; for the modelled `city_bus` (`BusVehicle.Modelled`) |
| `strap_hang` | 6 s | loop | standing, one hand up on a strap, swaying: only if a ride is ever shown |
| `sit_bench` / `sit_bench_up` | 1.6 s each | once | onto and off a seat 0.46 m up, 0.30 m behind the feet; with a seated loop `sit_wait` (8 s) for the shelter's bench |
| `refuel_hold` | 5 s | loop | bent at the waist holding a jerry can to a filler 0.9 m up, 0.4 m ahead |
| `bonnet_check` | 6 s | loop | leaning over an open bonnet 0.9 m up, hands working 0.5 m ahead |

Also wanted: a mechanic's overalls in `CityOutfits`/`Uniforms` (the roadside mechanic is in ordinary clothes today).
