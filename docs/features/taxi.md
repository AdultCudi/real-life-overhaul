# Taxi by phone

## In a sentence

The **City Cabs** contact sends one of the game's own taxis to the street you are in; the game's click, map, fare and ride stay the game's, and the mod brings the taxi and charges a call-out.

## Where the code is

- Folder: [`Scripts/Transport/Taxi`](../../Assets/Mods/RealLifeOverhaul/Scripts/Transport/Taxi/). The files are listed in the code block below.
- Read first: `TaxiRules.cs` (every number and rule, game-free, run by NavCheck's `TaxiCheck.cs`) and `TaxiDirector.cs` (one call from quote to ending).

## How it talks to other features

- `Wiring/TaxiLinks.cs` fills in `TaxiBridges`: whether the player is held (police, a driving test, prison), riding a bus, whether it is wet, the GPS's road graph, and the taxi's mark on the minimap.
- Red lights and closed roads for any vehicle the mod drives are `Traffic/RouteGates`, written out from the bus's rules.

The contact **City Cabs** sends one of the game's own taxis to the street the caller is in.
The game's click, map, fare and ride are untouched: the mod brings the taxi and charges a call-out, and that is all.

```
TaxiRules      every number and rule, game-free                     NavCheck/TaxiCheck.cs runs them
TaxiRoads      the caller's lane, the places a taxi may start, the way between
TaxiCab        the vehicle: taken, driven, stood, let go
TaxiDirector   one call: quote, send, watch, arrival, wait, ending   0.25 s timer while a taxi is out; switched off otherwise
TaxiPhone      the contact and the game's dialogue panel
Wiring/TaxiLinks   the holds, the weather, the GPS's road graph, the minimap
Traffic/RouteGates red lights and closed roads for a vehicle the mod drives (the bus's rules, written out for anybody)
```

**What the game has, and what was taken from it.** A taxi is a pooled traffic vehicle whose prefab carries `TaxiController` (an `EntityController` and `ITaxi`): its click stops the vehicle (`StopNow`, 60), walks the player to it and opens the map in taxi mode (`CityMap.SetTaxiMode`).
`TaxiSystem.TravelTo` charges 0.15 a metre, hides the player, runs the time machine and puts the taxi back with `TaxiController.InstantiateVehicle` (`Manager.RemoveVehicle`, then `TrafficManager.LoadVehicle(prefab, waypoint)`).

The game also has a car that comes when called, the private driver (`PrivateDriverHelpers.SummonPrivateDriverVehicle`, `PrivateDriverVehicle`).
From it:

- *taken:* how the vehicle is put on the road (`LoadVehicle` on a lane point that is no junction's and that the camera does not see).
  That it is the game's own object the player then clicks.
  That one may be out at a time.
  That the game's `AiCarRescueCheck` (which removes a car standing 20 s unseen, 40 s seen, and leaves only the private driver alone) must be kept off it;
- *not taken:* the driving.
  The private driver is given a scripted route (`VehicleComponent.presetPath`) and stopped by `PrivateDriverVehicle.Update` every frame.
  A scripted route drives through its first red light and stalls behind the first queue (see [Buses](transport.md#buses)), and a per-frame `Update` on the vehicle is not this mod's way.

  Nor its route finder (a breadth-first walk of at most fifty lane points that allocates a list per branch), its guider (`DirectionGuiderType.PrivateDriver`, which the game's own private driver resets), or its component added to the pooled vehicle.

**Which vehicle, and why it is a real taxi when it arrives** (`TaxiCab.Pick`, `Send`).

One pass over the traffic pool for a vehicle with `TaxiController` that nobody holds (`presetPath == null`): an idle one, else one that is driving out of sight (not ignored by the traffic AI, the game's own visibility script says unseen, neither of the mod's two view tests sees it, and it is 70 m or more from the player), which is taken off the road there.

It is put on the plan's start with `TrafficManager.LoadVehicle`, only ever while an idle vehicle of that prefab exists (with none, that call would take any vehicle of the prefab's name, seen or not).
Nothing is added to the vehicle and nothing on it is changed, so the object that stands for the caller is the same `TaxiController` the game's click, map and ride expect.

**The way** (`TaxiRoads.Find`).

One pass over the lane points gives the caller's lane: the plain lane points on their level, within 60 m, that are no further off than the nearest plus 5 m.
It gives the places a taxi may start too: one plain lane point in four between 250 and 450 m as the crow flies, 400 to 650 m in the wet.

The camera's 110 m is well inside that, and both view tests are still asked for any nearer the camera than 115 m.

One `RouteSearch.Find` from all of the starts to the lane: the answer is the start nearest by road and the way from it.
The graph is the GPS's when it has one (`NavDirector.Roads`: places and edges shared, four working arrays of the taxi's own), else `RoadGraph.Build` once per city on the first call (named for the session record).
A route more than 2.2 times the band's far end is refused.

A start traffic stands on (`LoadVehicle` answers null) is left out and the search run again, four times at most.

**The drive** (`TaxiCab`).
As a bus: ignored by the traffic AI, an empty `presetPath` as the mark that keeps the game from taking it off the road, the rescue check switched off, `EmergencyDriver.BeginRoute` along the route with `MayPass` off (it keeps its lane), 11 m/s and 7 through a junction.

`RouteGates` (the bus's rules for red lights and closed lane points, written out as two static functions; `TransitBus` keeps its own copy, which was proven in the game and is not touched) says where it pulls up and when it may go on; standing at a gate it is kinematic.
Held up with 14 m or less of its route left (the caller standing in the lane for it), it stops there and has arrived.

**Where it stops.** The last place of the route is the caller's lane point.
With 17 m left, once, and only if it is within 12 degrees of the lane's line, `SceneParking.KerbRoom` is asked (the test a car stopped by the police gets: the strip beside it is carriageway, no other lane, nothing standing in it): if there is room it pulls in by that much (`EmergencyDriver.PullOver`) and is braked with 4.5 m left.

Else it stops on the lane's line, 1.2 m short of the route's end as every route of the driver's does.

**Standing, and the hand-over.** The taxi stays held and kinematic: nothing drives it away, the game does not remove it, traffic queues behind it or the kerb keeps it out of the way.

It is not handed back to the traffic AI with a `StopNow` issued again and again (what the game's click does, once, for a hailed taxi): a vehicle in the AI's hands is removed by `AiCarRescueCheck` after 20 s unseen and by the pool once it is out of range, and a caller 80 m away causes both.

On a held vehicle the game's click still works as on any taxi: its own `StopNow` and `DriveAway` only write the traffic manager's two state arrays (`TrafficManager.UpdateDrivingState`), which nothing acts on for an ignored vehicle.

**What tells the ride's start.** `TaxiSystem.IsTraveling` turning true while the last taxi the game's map had was this one.
`CityMap.Taxi` cannot be read at that moment: `TravelCoroutine` closes the map in its first synchronous stretch and the map forgets its taxi as it closes, before `_travelCoroutine` is even assigned.

So the map's taxi is noted from `GlobalEvents.onCityMapToggle` (kept with `EventHold.Keep` while a taxi is out, taken off when the call ends): the event is raised on opening with `Taxi` already set (`SetTaxiMode` sets it, then toggles), and on closing before it is cleared.
A map opened plainly has no taxi and changes nothing.

The ride check is the first thing in a tick, before the vehicle is looked at, because the time machine that follows shuts traffic down.

**How a call ends** (`TaxiDirector`), each with one `Taxi check` line and everything released by `Stand`:

| ending | what tells it | money | the taxi |
|---|---|---|---|
| the ride | `IsTraveling`, the map's taxi was this one | the call-out | handed back to the traffic AI at once (`ReturnToTraffic`): the game then removes and re-instantiates it after the ride, as it does any taxi |
| another taxi | `IsTraveling`, the map's taxi was another (hailed, the private driver) | a cancel's | traffic if seen, else off the road |
| nobody came | 60 real seconds standing (20 more, once, for a caller within 15 m) | half the call-out | the same |
| cancelled | "Cancel it" on a second call, or the test | free above 100 m of road to drive, else half | the same |
| went another way | in a vehicle, indoors or in a car park, on the subway, the boat or a bus | a cancel's | the same |
| held | `TaxiBridges.Held` | none | the same |
| cannot come | got nowhere for 40 s out of view with over 110 m to go: one new plan, nearer; 150 s without a metre or 300 s out: given up. Or the game took the vehicle (a building, fast-forward, the pool) | none | off the road |
| option off, clean-up, a fault | the tick; `Features.Cleanup`; `Fail` | none | traffic if seen, else off the road |
| the city closes, a save is loaded | `OnDestroy` | none | off the road |

No charge is forced: `TaxiRules.Payable` takes whole money and never more than the save holds, through `GameManager.ChangeMoneySafe` with `force: false`, under `reallife_transaction_taxi_callout`, `_cancel` and `_noshow`.
Nothing is in the save but the contact.

**Cost.** Idle: nothing (the component is disabled; it runs for the first moments of a city to add its contact).
A call: one pass over the lane points, one over the pool, one search (figures in the `(route)` line).
While a taxi is out: the 0.25 s tick (a few flag reads, the gate walk over at most 45 m of route) and the driver's own physics step for its one vehicle.

The minimap asks for the taxi's place when it gathers its marks, every two seconds.

**Log lines** (with "Developer diagnostics" on): `Taxi check (call)`, `(route)`, `(taxi found)`, `(en route)` once a minute, `(re-plan)`, `(arrival)`, `(wait)`, `(ride)`, `(no show)`, `(cancel)`, `(released)`.

**Never run in the game.** Read from the decompiled code and checked by the compiler and `NavCheck` only: that the city's traffic pool has a taxi idle or out of sight often enough (if not, the call is refused with "No taxi is free right now").

That the taxi's `VehicleTypes` is allowed on the lanes it is routed over (only lanes that allow it are taken as starts and ends; the graph's edges are the car graph's).
How the game's hover outline and click behave on a kinematic vehicle.
That the player's walk to a taxi pulled in to the kerb ends at its door as it does in the lane.

How the taken taxi looks driving off in the second the screen fades.
Whether `KerbRoom` finds room often on this city's streets, most of which have parked cars at the kerb (then it stops in the lane, as a hailed taxi does).
