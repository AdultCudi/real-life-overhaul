# Minimap and GPS

## In a sentence

A minimap on the game's HUD while the player is out in the city, and a GPS route along the roads to the game's destination, with the next turn, the distance left and the arrival time.

## Where the code is

- Folder: [`Scripts/Navigation`](../../Assets/Mods/RealLifeOverhaul/Scripts/Navigation/), and the shared road search in `Scripts/Core/RouteSearch.cs`. The files are listed in the code block below.
- Read first: `NavDirector.cs`, then the game-free files NavCheck checks.

## How it talks to other features

- `Wiring/NavLinks.cs` fills in `NavBridges`: the minimap is hidden during custody, a bus ride and photo mode, and the GPS's test button guides you to the nearest police station.
- `Wiring/TaxiLinks.cs` adds the called taxi to the minimap and lends the GPS's road graph to the taxi.

A minimap on the game's HUD while the player is out in the city, and a GPS: a route along the roads to the game's destination, drawn on the minimap and on the game's city map, with the next turn, the distance left and the time on the game's clock at which the player should arrive.
The base game has neither: its only navigation is the marker that points straight at a destination (`UI.Guiders`).

```
Core/
  RouteSearch     the A* search over the road graph (moved here from Transport: the bus network and the GPS both use
                  it). New: Begin / Advance, so a search can be done a slice at a time; Find is Begin + Advance and
                  gives what it gave before
  RoadGraph       the game's lane points as RouteSearch wants them (moved here from TransitMap, unchanged)
Navigation/       (namespace RealLifeOverhaul.Nav: "Navigation" is a type of the engine's interface)
  NavModel        game-free: the route as a line, turns, where the player is on it, leaving it and arriving, the
                  view's arithmetic, the grid for "which lane points are near", the walking graph, the map picture
  NavGraph        the roads once per city: the two graphs, the grid, the map picture (drawn on a pool thread),
                  street names from the buildings' addresses
  NavHud          the panel on the HUD: a copy of the game's task panel with the map, the markers and two lines in it
  NavRouteLine    a line as one ribbon of the interface (a Graphic), made when the route changes
  NavMarks        the places that matter, read from the game's markers and from Core/MapPlaces
  NavCityMap      the route on the game's city map, and the double click that sets a destination
  NavBaseRules    game-free: the city under the roads (buildings and water): a place on the picture and back, a
                  building's outline, the water grid and its settling, the drawing, the file the measured city is kept in
  NavBaseGame     the only place of it that reads the game: a building's outline, one look down for water, the palette
  NavBase         makes that picture once per city load: from the mod's folder, or by measuring the city in slices
  NavDirector.Base  when it is made and shown, its two checks and its three tests
  NavBridges      what it asks of the rest of the mod (Wiring/NavLinks answers); NavText, the feature's words
  NavDirector     the one component: what shows, the destination, the search, following the route, the tests
```

## What the game has, and what was built on it

Read in the decompiled game and in its scene data (`evidence/uitools`, and a study script that reads the road graph out of `level5`); nothing was seen running.

- **The destination.** Everything that sets a destination ends in `GuidersManager.SetGuiderTarget(..., Destination)`: a building's card, a task's button, this mod's "set destination" buttons and map markers (`Core/MapPlaces`, Law's nearest station, the Planner).
  The GPS reads `GuidersManager.destinationGuider.target` four times a second, and a job's (`jobDestinationGuider`) when no destination is set.
  So nothing that sets a destination knows of the GPS. Quest guiders and the private driver's are not routed to; they are markers on the minimap.
- **The game never clears a destination on arrival**; only the marker's own dismiss button does.
  On arrival the GPS says so (the game's notification), clears its route and leaves the marker, as the game does.
  It guides to the same place again only if the player has gone more than 150 m from it while the marker still stands.
- **The roads.** `CurrentSceneData.allWaypoints`: 18,791 lane points in the city of this version (17,282 lane edges, 97 km of lanes, 3.8 by 2.2 km), each named `Road_25-Lane_3-Waypoint_7`, `...-In`, `...-Out`, or, for a junction's own points, `...-Connector2` / `...-CConnect2`.
  A junction's points are named after the road left, the road entered, or another one, so turns are found from the geometry (below), never from which road a point claims.
  `roadIds` holds something else and is not used.

  The data has 322 ways through a junction that turn right, 112 that turn left and 432 straight on: routes in this city turn right more often than left because the roads do.

- **No street names in the road data.** Buildings have addresses.
  A road is named after the street most of the buildings along it give as their address (`NavGraph.StreetOf`: up to seven places along the road each ask for the nearest door within 45 m; two votes make a name).
  Where that fails the turn is said without a name.
- **The HUD** is several canvases, all laid out for 3840 x 2160 and scaled by height, and all re-scaled by the game's "interface zoom" option (`UIs.SetUIZooming`).
  Top: the bar, 183 high.
  Left column, 740 wide: the task panel from 320 below the top (as high as its list, 1,000 at most, foldable), the control hints at the bottom (as high as their content, absent when switched off).
  Bottom right: the phone, 628 x 1,230.

  Bottom middle: the address labels, and the item panel (up to 1,834 wide) when something is held or driven.
  Top right: the job panel and the notifications.
  There is no corner the game always leaves free; see "Where the panel stands".

- **The city map** is the city seen by a camera; its markers are interface objects the game moves to their places on the screen every frame the map is open.
  A click selects a building (whose card has "set destination"); a drag moves the map; nothing sets a destination at a spot.

## The minimap

- **The picture is made once per city, from data, not rendered.** Every lane of the road graph is drawn as a soft-edged band 4.6 m wide into one byte per pixel (`NavRaster`), on a thread of the pool, from arrays nothing else writes; the main thread then hands it to the graphics card (`Texture2D`, Alpha8, no mip maps, not readable).

  A second camera was not used: one HDRP render of the whole city costs an unknown amount under the loading screen, comes out as the hour and the weather have it (dark at night), and cannot be checked without the game.
  A drawn street map is the same every time, and reads like the map of a satnav.

- **Size:** as many pixels as fit in 4,096 on the longer side, at most 1.25 a metre.
  This city: 4,096 x 2,308 at 1.13 a metre, 9.0 MB on the graphics card.
  Its array of the same size in main memory is let go once it is uploaded.
- **Moving it costs one quad.** The picture is shown by one `RawImage` as wide as the view's diagonal; which part it shows is its texture window (`uvRect`), and turning with the player is the image's own rotation.
  Route and markers are children of an object turned and scaled the same way and moved so that the player is at its middle: their places are in metres and are never written again while they show.

  The panel has a canvas of its own, so a frame in which the player moved re-batches these few pictures and not the game's HUD.

- **Per frame** (`NavHud.Frame`, only while the panel shows): the player's place, heading and speed are compared with what was last written; standing still writes nothing.
  Moving writes the texture window (four vertices), one position and, when the heading changed by more than 0.1 degree, two rotations and the markers in view (32 at most) turned back upright.
  Nothing is allocated.
- **The view:** 260 m across at 100% zoom; from 8 m/s it widens smoothly to 1.9 times that at 28 m/s (`NavView.MetresAcross`), eased at 1.6 a second.
- **Markers** (`NavMarks`, read every 2 s; which are in view, 4 times a second; a marker is written only when what it shows changes): the game's guiders other than the one routed to; the game's permanent markers (the player's own buildings); the subway stations; every kind of place of this mod whose row on the city map is ticked.

  A marker is the game's own: the round base of its map markers (read once from the map's marker template) in the place's colour with the place's picture on it.
  The player is the map's own player icon and colour (`CityMapFilters.playerIcon`), turned by the game's own rule for it.
  The destination's marker is held at the view's edge while the place is outside it.

- **Where the panel stands** (`NavHud.Place`, 4 times a second, writes only on change).
  First choice: the game's left column, between the control hints and the task panel, the map as high as the room between them (it is square when there is room).
  Each of the game's panels is measured where it is on the screen, so the interface zoom and the screen's shape need no table.

  When a long task list leaves less than 240 units for the map there, the panel stands at the bottom beside the column instead, lifted over the address labels or the item panel if it would reach them (a 4:3 screen).
  With no room in either place it is hidden and one line says so.

- **Hidden** indoors, in the underground car park, on the subway and the casino boat, while the city map, the phone's full menu, the pause menu, the options, a building preview, the interior designer, a mini game or the time skip is on, at a funeral and on the way to hospital, whenever the game has switched its phone off (which it does with the rest of its HUD), and during custody, a bus ride and photo mode (`NavBridges.HudHeld`).

  As a child of the game's HUD it also goes with the game's own "hide the interface".

## The GPS

- **Driving** the route is searched on the road graph as the bus network uses it: lanes in their direction, a change of lane for 30 m extra.
  It begins on a lane within 14 m that runs the way the car points and ends on any lane within 30 m of the place (the single nearest within 400 m if there is none).

  If there is no way with the traffic (in this city 20% of random pairs: a lane that leads away and nowhere back, a stretch nothing drives to), the same streets are searched without the one-way rule and that way is shown; the log says so.

- **On foot** the same streets are searched with every lane usable both ways (`NavEdges.TwoWay`).
  That is what the route is: the way along the streets.
  Lanes are where cars drive, so the line runs down the carriageway while the player walks on the pavement beside it, and it knows no path through a park, a square or a building.

  A straight line would be wrong in a city of blocks and the game's marker already gives the direction; a search on the navigation mesh was not used because one call over the whole city cannot be sliced.

- **The search is sliced.** `RouteSearch.Advance` is called once a frame for 0.4 ms at most (the clock is read every 64 steps), until it finds a way or runs out of road.
  Measured outside the game on this city's graph (NavCheck, .NET 10 on the build machine, not the game's Mono): 3,400 steps on average, 14,105 at most (the whole city, on foot); a whole search 0.25 ms on average and 1.1 ms at most.

  Mono is several times slower, so the worst search is expected to take a handful of frames, each with 0.4 ms of it.
  Not measured in the game: the log line of every route gives the steps, the slices, the time in all and the longest slice.

- **Turns** (`NavRoute.FindTurns`): at every junction the route passes, the heading over the last 14 m of plain lane before the junction's own points against the heading over the first 14 m after them.
  Under 28 degrees nothing is said; to 55 "keep left/right"; to 150 "turn left/right"; above, a U-turn.
  Two junctions within 18 m, or two curves of one junction with a plain point between them, are one turn.
- **Following** (4 times a second): the nearest place on the route, looked for around the segment the player was on last.
  More than 32 m from the line in a car (22 m on foot) for 2 s asks for a new route, and never twice within 4 s.

  Arrived: within 28 m of the place in a car (14 m on foot), or at the end of the road in a car when the place is not on one.

- **The two lines** are put together again only when a figure in them changes (distances are rounded to 10, 50 or 100 m).
  The time is the game's clock: the lanes' speed limits at 75% for a drive, the player's own walking speed on foot, turned into minutes at the rate the game's clock has been running.
- **On the city map** the route is one line in the markers' layer, under the markers, worked out again only in a frame in which the map's camera moved (the game moves every marker in every frame the map is open, and is paused meanwhile).
  **Two clicks on one spot within 0.4 s** make that spot the destination (a building: the same call as its card's button).

  One click is not used: it drags the map and selects buildings, and a destination must not be replaced by accident.

- **Voogle Route** (another author's GPS mod, found by `Core/OtherMods`; see [`RealLifeOverhaul.Build/docs/other-mods.md`](../../RealLifeOverhaul.Build/docs/other-mods.md)): if it is switched on, the city map is left to it.
  It draws its route there as lines in the world on the map's own layer and has its own click, panels and markers on the map, so nothing of this mod's is added to the map: no route object, no double click.

  This mod's route is then on its own minimap only, with its two lines, where that mod draws nothing; the arrival notice and the end of the destination are that mod's.
  The option "Use the other mod's route line" (a row that is on the options page only while that mod is on; off by default) leaves the route to it altogether: only the minimap is shown.
  One warning says which.

## Costs

| when | what | cost |
|---|---|---|
| once per city | the driving graph | one pass over the lane points twice (as the bus network does); not measured in the game |
| once per city | the walking graph and the grid | 0.5 ms and 1.3 ms outside the game |
| once per city | the map picture | 34 to 50 ms outside the game, on a pool thread; then one upload of 9 MB on the main thread (not measured) |
| once per city | the panel | one copy of the game's task panel with its entries, cut down asleep; not measured |
| per route | the search | 0.4 ms a frame until done; see above |
| per route | the route's line, turns and the two meshes | under 0.1 ms outside the game |
| 4 times a second | visibility, placement, the destination, the place on the route, markers in view | a few dozen comparisons; text only when a rounded figure changes |
| every 2 s | the places read again | one pass over the game's markers and the mod's places |
| per frame, panel showing | `NavHud.Frame` | comparisons; when moving, one quad's texture window, one position, up to 34 rotations |
| per frame, panel hidden or options off | `NavDirector.Update` | one time comparison and two flag tests |

Each one-off step is named for the session record (`SessionWatch.Mark`: "the GPS's road graph", "the GPS's walking graph", "the minimap's picture", "the minimap's panel"), and its time is in its log line.
The steps are taken one a tick from the moment the city's roads exist, so they fall under the loading screen where the game is quick enough to have the roads there.

## Safety

Nothing is written to the save.
Every object made lives under the game's HUD or its map's marker layer and is destroyed when the director is (city unload, "prepare this save for removing the mod").
An error in the director switches the feature off for the session with one line and takes its objects off the screen; an error in the city map's part switches only that part off.

The game's map event is held with `EventHold.Keep`.
If the task panel, its headline or its mask is not there, the minimap is left off with one `Game UI:` line and the route is still drawn on the city map.

## Checks

`RealLifeOverhaul.Build/NavCheck` compiles `Core/RouteSearch.cs`, `Navigation/NavModel.cs` and `Transport/BusBay.cs` as they are: `dotnet build RealLifeOverhaul.Build/NavCheck -c Release`, then `dotnet RealLifeOverhaul.Build/NavCheck/bin/Release/net10.0/NavCheck.dll` ends with `ALL CHECKS PASSED`.
It checks the search whole against the search in slices, routes on a made-up grid of streets named the three ways the game names junction points, a one-way street driven and walked, the turn list, following, leaving the route, arriving, the line that is drawn, the view's arithmetic and the picture.

Given a file of lane points as its argument it also prints the timings quoted above (the file is made from the game's own data by a study script and is not in the repository).
The bus bays are checked in the same run (see [Bays](transport.md#buses)); `--bays` prints each run of the model bus.

The taxi called from the phone is checked in the same run too (`TaxiCheck.cs` with `Transport/Taxi/TaxiRules.cs`; see [A taxi by phone](taxi.md)).

## Log lines (with "Developer diagnostics" on)

- `Navigation: road graph made: 18791 lane points, ... edges (... of them changes of lane), 273 roads, ... ms.`
- `Navigation: walking graph and index made: ... edges, ... lane points in the index, ... ms.`
- `Navigation: map made: 4096 x 2308 pixels (1.13 a metre, 9.0 MB), ... lanes drawn in ... ms off the main thread, sent to the graphics card in ... ms.`
- `Navigation: minimap on the HUD, a copy of the game's task panel, in the left column; view 568 x 568 units; ...`
- `Navigation: route found to '...' (driving; a new destination): ... lane points, ... m, ... turns, ... search steps in ... slices, ... ms in all (longest slice ... ms).`
- `Navigation: 41 m off the route for 2 s (driving, 310 m along it): looking for a new route.`
- `Navigation: arrived at '...' (22 m from it, driving). ...`
- `Navigation: destination set by a double click on the city map: ...`
- `Navigation check (other mod): Voogle Route is switched on: this mod's route is drawn on its own minimap only, and nothing of this mod's is on the city map (no object there: PASS).`
- Always logged: the "Other mods:" warning for Voogle Route, and every part left off because the game is not built as expected.

## Not verified (the game could not be run)

Everything on screen.
In particular: that the copy of the task panel looks right at the sizes it is given.
That an Alpha8 picture is drawn in the image's colour by the interface's standard shader (if not, the roads are black).
That the mask's rounded corners cut the map.
The player icon's direction (taken from the game's own rule).
How the markers read at 46 units.

That the game's phone object is active whenever its HUD is in use (the panel is hidden when it is not).
The panel's place beside the column on a narrow screen.
That a double click on the map reaches this code as well as the map's drag; the names streets are given.
How far the walking line lies from the pavement.
Every time quoted from outside the game.

## The city under the roads (`NavBase`)

The option "Buildings and water on the minimap" (on by default; off, the minimap is what it was: roads, route, markers, and nothing of this is made or kept).

- **The game has no map picture.** Its city map is the city itself under a camera: `CityMap.Toggle` sets the main camera's culling mask and switches to the map's Cinemachine camera (`CityMap.cs` 221 to 284), `CityMapCam` moves that camera, and each building is drawn by its simple `lodVersion` mesh (`CityBuildingController.ToggleLodMode`, 230 to 244).
  No texture, sprite or render texture of the city exists in the game's code.
  So the base is drawn.
- **Buildings**: `CityManager.cityBuildingControllers`.
  An outline is the convex outline, seen from above, of the `lodVersion` renderer's box: the mesh's own box turned as the building is, or the engine's upright box when the renderer is part of a static batch (its mesh is then the whole batch's); all the building's renderers if it has no `lodVersion`.
- **Water**: the game has no water object, layer or list (Leisure's fishing found the same).
  The city is asked on a grid of 8 m squares, one look straight down each (`Physics.Raycast`), by the fishing rule: nothing there, or something more than 0.8 m below the nearest road that is neither road, building nor walkable (the navigation mesh), is open.

  Water is the open squares that reach the picture's edge without crossing ground, so a courtyard or a lawn stays ground.
  A square within 8 m of a lane point or inside a building is ground without asking.

- **Colours**: the game's interface palette (`GlobalReferences.colors`): buildings the roads' `lightGrey` at a third of their strength, water `darkBlue`.
  The game's map has no flat colours of its own to read.
- **The picture**: at most 2048 pixels on its long side, RGBA32, no smaller copies: 2048 x 1154 and 9.0 MB on the graphics card for the game's city (3.6 km by 2 km), 1.77 m a pixel.
  It covers the road picture's piece of the world from the same origin: pixel = (world - origin) x pixels a metre.

  One `RawImage` under the roads shows it, as large and turned as the roads' square; when the player has moved its window is written (four divisions).

- **Cost**: measuring takes 0.8 ms of a frame at most, for some seconds, once per version of the game; the result (outlines and grid, about 150 KB) is kept in `RealLifeOverhaul/minimap_base_<build>.bin` beside the saves.
  Every load after that: one read, one drawing on a pool thread, one upload.
- **Not seen running**: whether every piece of ground the city stands on has something solid under it far from the player, and what the palette's dark blue looks like on the panel.
  The tests "minimap, buildings and water alone" and "minimap, mark three buildings" are for judging both, and the two `Minimap check` lines say what was made.
