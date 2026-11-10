# Subway in Uptown

## In a sentence

Uptown and Cedar Heights have three subway stations of their own, each a copy of one of the game's entrances carrying the game's own station, joined to the game's network at every load: the game's travel window lists them, a ride between any of them and any of the game's twenty stations is the game's own ride at the game's own fare, they are on the city map and its filter, a taxi can be sent to one, and the city's people come up and go down there; nothing of it is saved or sent.

## Where the code is

- Folder: [`Scripts/Subway`](../../Assets/Mods/RealLifeOverhaul/Scripts/Subway/) (namespace `RealLifeOverhaul.Subway`), with the registry of the mod's own stations in `Scripts/Core/OwnStations.cs` and the contract's line in `Scripts/Core/GameContract.Stations.cs`.
- Option: "Subway stations in Uptown and Cedar Heights" (`uptown_subway`, on by default; Options, section "Subway in Uptown and Cedar Heights"). It acts at once, in play: on, the stations are stood; off, they go (a ride under way is let finish first). Off, everything is as before the feature.
- Checks: NavCheck `UptownSubwayCheck.cs` (the ground; `--subway` prints each station in figures) and EconomyCheck `--subway` (`SubwayChecks.cs`).

```
StationsRules              game-free: the three sites, the entrance's measures, what a station keeps clear of, where its rider comes up, its place in the
                           game's sorted list, the search, when they stand and when they may go, the people of the line
StationEntrance            the game's own SubwayStation on the copy (a subclass), named another way, with a call on the map's toggle that is taken off again;
                           StationTaxiStop: what a taxi is sent to in its place
StationsGame               the one file that touches the game: copy, stand, cut, join the game's list, window and search, take away, the check
StationsDirector (+ parts) when they stand and go (.cs), the people of the line (.Riders), the tests (.Tests)
StationsGuide, StationsText the feature's entry, its option and tests; the locale keys
```

One line each elsewhere: `Core/Settings.cs` (`uptown_subway`), `Core/FeatureOwners.cs`, `Meta/FeatureCities.cs` (New York's), `Meta/OptionsTree.cs` (its section), `Core/RealLifeOverhaulMod.cs` (the director, after the districts' ground, the campus, the landmarks and the buses), `Core/GameMembers.cs` (a station's name asked of the mod's own first), `Transport/TransitMap.cs` (the bus network's ends are the game's stations alone), `Leisure/LeisureVenues.cs` (the landmarks keyed to the game's stations leave the mod's out), `Transport/SubwayJourney.cs` (log names) and `Uptown/GroundDirector.Streets.cs` (the districts' older test counts them).

## What the game has

- **A station** is a `SubwayStation` (an `EntityController`) on an entrance object in the street: its mesh `SM_Subway` with three levels of detail, a solid box, two lamps and a travel point 2.809 m ahead of its origin (its `navMeshTargets`). None of the twenty is drawn in a static batch, and each is whole on its own object, so one can be copied (the level files, Build 3682).
- **The list** is run-time data: `CityManager.subwayStations` is filled once the city's scenes are loaded with every `SubwayStation` found, and the travel window's entries are made from it then (`CityManager.OnScenesLoaded`, CityManager.cs 84 to 86; `CityMapSubwayStations.LoadStations`, sorted by the stations' localised names). The map's marker, outline and filter walk that list at every toggle (`CityMap.ToggleSubwayStations`).
- **A ride** (`SubwaySystem.TravelToCoroutine`): the fare is `SubwayStation.PricePerRide`, 3, whatever the distance (`ba:transaction_subwayride`), refused without the money; the player is hidden, a camera flies from the station left to the station picked along a straight line at the system's `travelSpeed` (the flight's seconds are the metres over that speed; by the Manhattan Bridge between Industry City or the Hamptons and the rest), no game time passes, and the player is put on the walking mesh nearest the picked station's travel point (`WarpSafely`, within 3 m).
- **A name** is fixed: `SubwayStation.GetNameKey()` is `"subwaystation_" + stationName.ToStringFast()`, `stationName` is one of the twenty values of `SubwayStationName`, `EnumHelpers.ToStringFast` throws for any other value (EnumHelpers.cs 146 to 171) and `GetNameKey` is not virtual. The game reads it in three places: the hover box over an entity (`OverlayHelper.GetOverlayHeaderText`, after the entity's own `customOverlayHeaderKey`), the travel window (`CityMapSubwayStationEntry.Setup`, and `CityMapSubwayStations.FilterStations` for its search) and the place card for a taxi's destination (`BuildingResume.UpdateDetails(SubwayStation)`).
- **Nobody of the game's pedestrians uses a station**: no file of the game's AI names one.

## How a station of the mod's own is made

A copy of an entrance is a station the game's code accepts everywhere (every test of the game's is `is SubwayStation`), but it would be named as the entrance it was copied from. The mod carries no patching library, so the name is given another way in each of the three places:

- **The station** is `StationEntrance`, a subclass of the game's `SubwayStation`, put on the copy in place of the copied one while the copy is asleep (it is instantiated under a switched off object, so the game's own station on it never wakes). It keeps the copied entrance's `stationName` value, which is never read for its name, and its `customOverlayHeaderKey` is its own name's key, which the game's hover box reads first.
- **The travel window:** an entry of the game's own kind (`CityMapSubwayStationEntry`, a copy of the window's template, set up by the game's own `Setup`) with its label's key set to the station's, put into the window's private list of entries (the contract's one line, `own_stations`) and among the window's rows at the place its name has in the order of the names (`StationsRules.InsertAt`, as the game sorted its own). The game's own search reads a name from the name's value, so a listener of the mod's, put after the game's each time the window is shown (`StationListWatch`), shows the mod's entries by their own names (`StationsRules.Shown`).
- **A taxi's destination:** on the game's map in taxi mode a click on the station hands the game's card a stop of the station's own (`StationTaxiStop`, an entity that is no station, with the game's `TaxiDestinationData` naming it by the station's key), so every path that fills the card names it rightly, and the taxi puts its rider down where the subway's riders come up.
- **Every reader of the mod** asks `GameMembers.NameKey`, which asks `OwnStations` first.
- **The game's Start** for a station hangs a call on the map's toggle that is never taken off and would reach an entrance after it has gone; the subclass does what the two Starts do itself, with a call of its own that it takes off when it goes. Its travel point is not moved by the game's own validation: it is put on ground that was checked.

The station is then added to `CityManager.subwayStations`, after the game's own, and everything else is the game's: the fare, the ride, the map's marker (`CityMapPoi`), the outline, the filter line, the price in the hover box.

## Where the three stand, and why

The plan's three squares are the only blocks of the band that are not lots: the campus in the west (202), a park in the middle (309) and a park in the east (117), each in another row. Every other block is built to its frontage, and thirteen have open runs the player builds on. So there is one station by each square (`StationsRules.Sites`, fixed data), 648 and 713 m apart:

| Station | Where | Origin (x, z), turn | Its rider comes up at | Nearest lot | Nearest land to build on |
| --- | --- | --- | --- | --- | --- |
| Cedar Heights Birchwood Station (`birchwood`) | Uptown Avenue's north pavement beside Birchwood School's campus, 19.7 m east of the block's west corner, west of the main gate, its mouth to the gate | (-622.52, 623.08), 90 | (-619.71, 623.08), 3.5 m from where the main gate's crowd gathers | 23.8 m | 80 m |
| Cedar Heights Summit Park Station (`summit`) | the lawn of the middle park's south edge on Summit Avenue, east of the head of the park's spine, its mouth on the spine | (7.71, 774.38), 270 | (4.90, 774.38), on the spine | 27.9 m | 81 m |
| Uptown East Park Station (`east`) | the lawn of the east park's north edge on Uptown Avenue, west of the head of the park's spine, its mouth on the spine | (698.45, 596.38), 90 | (701.26, 596.38), on the spine | 27.9 m | 67 m |

- **In the parks** an entrance stands as Manhattan's do, on the block's own ground behind the pavement: its middle line 1.75 m inside the park's edge, in the strip of lawn the park's trees keep off (3.5 m), its mouth 0.3 m from the spine's edge, so its rider comes up on the spine, 1.02 m from its middle: ground the player's walking mesh and the pedestrians' both hold. The whole pavement stays free.
- **By the school** the campus is fenced 0.3 m inside its block's edge and every block round it is built, so the entrance stands on the avenue's 3.7 m pavement at the kerb's side: its middle line 1.35 m from the kerb, 0.21 m to the kerb and 1.21 m of pavement behind it as the way past (1.1 m wanted), between the avenue module's first lamp post (1.6 m behind its far end) and the main gate's crowd.
- **The entrance** is the game's own: 2.27 m wide, 3.2 m behind its origin and 2.03 m ahead of it, 3.05 m high, its origin 0.115 m into its ground as Midtown's stand (a lawn's top 0.0492 m, an avenue's pavement 0.058 m).
- **Each keeps clear** (the rules, asked by the director before the game's side stands it, and by NavCheck): every lot of the plan and the big special places by 0.5 m, the carriageways (its block's lawn or pavement holds it whole, with where its rider comes up), the pavements' furniture and its park's trees, bushes, benches, lamps and bins by 1.2 m, the avenue's lamp posts by 0.6 m, the campus's gates' crowds, and every bus stop the routes want and every terminus by 28 m (the stretch the furniture keeps). A kerb a car stops at is 1.3 to 5.5 m from where a rider comes up.

## At a load, and in play

Once the game has loaded its city (its late call, the loading screen still up, after the districts' ground, parks and campus and the bus routes), while the option is on, the districts stand and the game has stations of its own (`StationsRules.Wanted`), each station of the plan:

1. **The rules' look** at its ground as above (`StationsDirector.Plan`).
2. **The game's side** (`StationsGame.Stand`), refusing before anything is made: the game's subway, its window and the window's list of entries there; an entrance of the game's that can be copied (its own mesh on its own object, never a static batch, the game's solid box; the nearest to the districts, Midtown North West's); that entrance the shape the plan was measured for (within 0.3 m); nothing solid on its ground (one box, from 0.4 m above the ground to its canopy); and its rider coming up on the player's walking mesh and the pedestrians' (within 0.45 m).
3. **Made and joined:** the copy asleep, the station replaced, the stop and the travel point added, one carving box that cuts the entrance out of both walking meshes (0.2 m narrower than the entrance each side: a cut is wider than its box by the walker's radius) and goes with it; then the game's list, the mod's registry, the window's entry and the search.
4. **The self-check**, two seconds on (the cut is in by then): `Subway check (stations): ...` with Developer diagnostics on, or as a warning when something is not as wanted.

A station that cannot stand says why in one line and leaves nothing behind; the others stand. Switched on in play, the same happens at once.

**Going** (the option off, the districts closing, the feature's clean-up, the city unloading): never under a rider (`StationsRules.Removal`). A ride of the game's that begins or ends at one of them is let finish first, since the game brings its rider up at the station's own travel point; a map open on the list of stations from one of them is shut first. Then each station leaves the game's list (which the game walks at every toggle of its map), its entry leaves the window, its marker leaves the map's list of markers, the game's card lets go of its stop, and the entrance is destroyed with its cut, so the walking meshes are whole again. A player standing in the districts loses nothing else: the streets, the buses and the taxis are as they were.

## Fare and time

- **The fare** is the game's own, 3 a ride, taken by the game's own travel; the mod never books, gives or waives it here. The day pass of the City Transit contact gives it back as on any ride ([Transport](transport.md)).
- **The ride** is the game's camera flight, in a straight line at the game's own speed for the distance.
- **The journey** ("Subway journeys take time", [Transport](transport.md)) counts them as any station: a wait by the service's clock, then 2 game minutes a stop, a stop for every 650 m of straight line and one at the least. From Midtown North West Station that is 1 stop to Birchwood (359 m), 1 to Summit Park (461 m) and 2 to the east park (1,023 m).
- **At an entrance** the game's panel over a place names the station, the minutes between trains and the state of the service, as at the game's own.

## On the maps and in the lists

The game's map marks them with its subway marker under its "Subway Station" filter and in subway mode, outlines them, and takes a click as a pick; the RLO interface's list of stations mirrors the game's entries, so they are in it at their names' places, with their district; the place card's route strip offers the subway by the nearest station to each end; the minimap's marks read the same list.

## The city's people on the line

At the entrance the player is near (within 70 m, kept to 95), by the trains of the service (`SubwayJourney.Frequency`: every 3 minutes in the weekday rush, 5 by day, 7 from 20:00, 8 from 05:00, 12 at night):

- a train brings somebody up, who walks off 34 m along the pavement: from nine trains in ten in the weekday rush, six by day and in the evening, two between midnight and five;
- somebody walks in from 34 m along the pavement and goes down: to five trains in ten, two at night.

Which trains, who (a seeded look, half of them women) and which of eight ways are mixed from the day, the train and the station (`StationsRules.ComesUp`, `GoesDown`, `RiderLook`, `RiderBearing`), so they are the same on every machine and never chance. Two people at the most, made behind the loading screen; under the option "People at the stops". They are beside the bus stops' own people, whose pace nothing here touches: the buses keep every stop and every waiter they had, and reach every corner of the districts; the subway is the long way in and out. The game's own pedestrians use no station anywhere, and the population's model has no way of travel to change (its people take jobs by the distance between districts, `LabourRules`), so nothing is claimed there. The rival tycoons are not seen in the street, so none rides.

## Saves

Nothing is saved: three fixed places stood again at every load. A save made before the feature has the stations from its first load with it; a save made with them loads without the mod or with the option off as if they had never been. A station cannot stand twice: a site that stands is refused, and nothing of an earlier city is held.

## Going Public

Nothing is sent. The stations are fixed data, the same on every machine that has the districts and the option; a ride is each player's own, as in the base game, and another player sees its rider where the ride put them, on the districts' ground, which every machine of a session has or none. The bus network's ends leave the mod's stations out, so the lines are the same whatever the option says. The people of the line are seen by each player alone, as the bus stops' are. Where one machine has the option off, its player simply has no entrances there.

## Cost

One time comparison a frame. A look once a second (the option, the districts, the distance to three stations, the clock) and four a second only while somebody of the line is out. Standing a station is one copy of an entrance, one box query and two samples of the walking meshes, behind the loading screen or on the option's press. Nothing is searched for in the scene.

## Self-check lines

- `Subway: 3 of 3 stations of the districts' own stood at the load and joined to the game's 20: ...`, each with its place and where its riders come up, or why it was not stood.
- `Subway check (stations): 3 standing, 3 of the districts' own in the game's list of 23; birchwood: in the game's list 1, in its window 1, named its own, riders come up on the player's ground yes and the pedestrians' yes, cut out of the walking ground yes; ...: PASS`.
- `Subway: N station(s) of the districts' own taken out of the game's list, its window and its map, and away (why); 20 of the game's own stand as they did.`
- `Subway check (rider): ...` from its test.
- `Uptown check (subway): N station(s) of the districts' own stand among the game's ...` from the districts' older test.

## Tests

Options > Tests, each saying why in a notice when it cannot run:

- **Subway: check the stations of Uptown and Cedar Heights**: the self-check's line and a notice (in the city expansion's test run too).
- **Subway: take me to the next station of the two districts**: on foot, where its riders come up; each press the next one.
- **Subway: ride from the nearest of the game's stations to one of the two districts'**: the game's map in subway mode from that station, then the game's own travel, at the game's fare.
- **Subway: somebody comes up at the station nearest me**: one rider of the line now.

## What only the game can tell

That a copy of the entrance draws, with its lamps lit at night, and stands level on a park's lawn and on the avenue's pavement (the heights are from the plan's surfaces, not measured in the game); that the outline pass draws the copy's pieces; how the game's travel window looks with three more rows and that the rows stand in name order in every language; that the hover box shows the name and the price as over the game's stations; that the walking meshes' cut leaves the way past on Birchwood's pavement and the spine free; that the pedestrians walk round the entrances; that the camera's flight to the districts and back reads well over the districts' ground; that the game's ambience of a station (its sound sources are counted once when the city starts) is heard or not at the copies; how the people of the line look as they come up and are taken in at the mouth, where no clip of stairs exists.
