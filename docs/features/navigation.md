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

  While the interface kit's HUD is up, the panel also stands above its bottom bar (as the bar is laid out, whatever its height), under its left column and clear of its key chips, and is drawn over the kit's HUD (its own canvas, one order over it) and under every window; the kit's notice cards, objectives, world prompt and activity strip keep clear of it.

  The kit's item panel (what the player holds or drives, the Vehicle panel while driving) stands over the card in the left column, its left edge the column's and a grid step (8 px) above the card, so the middle of the screen and the key chips stay clear (`UiItemPanel.Place`).
  With no card it stands at the bottom left where the card would, its foot over the key chips wherever they reach under it.
  A panel taller than the room between the card and the objectives stands over the objectives' lower part (its canvas is kept at the card's order, over the HUD's: `ItemPanelKit.Lift`); where even the whole height over the card is too little (a short screen), it stands beside the card at the bottom, over the chips.

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

## The minimap in 2.5D, and the city map as the game draws it (`Scripts/Ui/Map`, `NavScene`, `NavMapCamera`, `NavHud.Scene`)

The owner's call (3 Nov 2026): the minimap looks flat seen from straight above, and chunky buildings rise when the view is tilted or turned. One option, on by default: "Minimap shows buildings as 2.5D blocks" (`nav_minimap25`). Off, the flat minimap is as it was. Display only: nothing is saved or sent. **The owner's decision of 5 Nov 2026: the full city map shows the game's own buildings again.** The mod's 2.5D picture of the city map (its render texture, its culling mask swap, its marker, its street names, its tint pass, its option `nav_map25`) was taken out: the game's city under its own camera is on screen at all times, panned or still, which also cured the twitching (the picture showed only while the camera stood still, so every pan flipped between it and the game's city). The map has the game's own player pin, street names, hover, tint, outline, pins, filters and route; what the mod keeps on it is its GPS route line (`NavCityMap`, under the game's pins) and the 2D and 3D button's tilt of the game's camera (`NavMapCamera`). The section "City map" of the options page keeps the map's place filters (`map_places_shown`) and the special names (`special_names`).

```
Ui/Map/  (game-free except Map25Scene, which uses only the engine, so the preview draws with it too)
  Map25Math     matrices as the engine keeps them, view, frustum, a place on the picture and back, half floats, a seeded generator
  Map25Palette  the mockups' colours and the shading rules (light from the west, a foot darkened in metres, podium and crown)
  Map25Builder  vertices and triangles into growing arrays, handed out as chunks (16 bit indices, colours as half floats)
  Map25Height   how tall a block is drawn: a knee and a cap that follow the lot's sides (the mockups' chunky blocks)
  Map25Blocks   one building: walls shaded by the way they face with a band of window colour every three floors, a heavier foot, cornice, parapet rim, a crown (setback) on a wide
  (+ .Props,    tower, water tank on legs, air conditioners, stair hut, the contact patch under it
   .Contact)
  Map25Layer    the kinds of drawing (blocks, crowns, contacts), a layer of chunks for each, and which a view draws (Map25Rules)
  Map25Set      the whole city in 192 m chunks, built once for the minimap
  Map25Ground   the land as a marching squares outline of the water grid, the roads as a band a lane with lane marking on the avenues, the
                trees and the green ground they make, the route's strip, the view cone
                trees and the green ground they make, the rings and roof lines of tinted buildings, the zones, the route's strip, the view cone
  Map25Under    land, green ground, roads, lane marking and trees made together, on a pool thread
  Map25View     the minimap's camera, its planes, the pitch the map button's tilt asks for, what a pixel is
  Map25Marks    the route, cone and halo as a few pixels of the picture, in metres where they lie
  Map25Scene    meshes from chunks a slice at a time, the command buffer, the one draw into the minimap's render texture, the self-test
Navigation/
  NavScene      makes the city from NavBase's outlines (with each building's ground and height, file format 3), the road graph and the game's
                trees, on a pool thread, then the meshes at 0.6 ms a frame
  NavMapCamera  the city map button's tilt of the game's own camera (nothing is drawn); NavMapLatch the minimap's black picture, latched for the session
  NavHud.Scene  the minimap: the picture in its view, the pins placed by the camera, drawn when the view moved a pixel
  NavDirector.Scene  when the scene is made, the route into the strips, the map camera on and off with the map, and let go
RealLifeOverhaul.Build/Shared/  (preview and checks only, never in the mod)
  Map25Fixture  a made-up city of the grain of the game's, and the heights' model
  Map25Real     the owner's own footprints and water from the file their minimap keeps; the heights, roads and trees made up, and said so
```

- **Why the minimap is a picture and the city map is not.** The minimap is made from data (the footprints the minimap already measured), some sixty chunks, drawn only when its view moves. The city map is the game's own city under its own camera, so it costs what the game's full city render cost before the mod, and the mod makes no render target and never swaps the camera's culling mask there (EconomyCheck holds this by reading the source).
- **One render, outside the game's pipeline.** A command buffer with the engine's own colour shader (`Hidden/Internal-Colored`) into an sRGB render texture, run from LateUpdate: no light, no exposure, no volume, so the picture is the mockup's at any hour. A 16 pixel self-test at the first use reads back where a mark lands and which way the depth runs (the built-in pipeline and a scriptable one differ in both) and picks the projection and the depth test that work; if none does, or the shader is missing, the feature stands down with one line and the game's own map and the flat minimap stay. A picture that comes out black is latched for the session (`NavMapLatch`, said once in the log): the map is never again covered by it.
- **How tall a block is** (`Map25Height`). The game's real heights run from a shed to a tower of well over a hundred metres on a lot ten metres wide, and a block that high on a thin lot is a needle that hides its streets. A height is kept as it is up to a knee (8 m and 1.4 m for each metre of the lot's shorter side: 22 m at 10 m, 29 m at 15 m, 43 m at 25 m); above it 15 per cent of the rest is drawn, up to a cap (6 m and 3.4 m a metre of the shorter side, never more than 10 m and 2 m a metre of the longer: a 10 by 10 m lot tops out at 30 m, an 8 by 30 m sliver at 33 m). The owner's 883 footprints with a height model (real p90 64 m, max 129 m) are drawn p90 36 m, max 76 m; with a deliberately tall one (max 258 m) the most is 95 m; the first build drew 181 m and 363 m. The tallest block is at most 4.4 times its shorter side. The pins and the route line are the game's own and stand at the game's real roof; the picture's roofs can be lower than a tall building's pin.
- **What is drawn, and when** (`Map25Kind`, `Map25Rules`): the blocks always; the crowns (the setback of a lot at least 26 m wide and 21 m tall: 22 per cent of the width in, 42 per cent of the height up, at most 45 per cent of the width) and the contact patches from a tenth of full height, so the flat picture has no frame inside a frame.
- **The rest of the block's detail.** The foot of a wall is darkened in metres from the ground (0.55 at the ground, 0.14 at 12 m, none from 28 m), not as a share of its height, and the wall's pieces break at those metres. A water tank is a drum on four thin legs with a lid and a cone, on some roofs with air conditioners and a stair hut.
- **The ground.** The roads are a band a lane, as wide as the gap they lie in; the avenues' lanes carry a dash of lane marking each, on the left. Trees come from `TreeController.AllTrees` (the places every tree of the game joins as it starts, read once when the scene is made, at most 14,000): a six sided dome in two greens, 13 vertices. The game keeps no polygon of a park, so green ground is where trees stand together: a 10 m square is green when at least seven trees stand in it and the eight round it (a row of street trees on each side of an avenue has six at the most). The squares are then closed and rounded as a ground is drawn: a square the green ones hold a majority of (five of the nine round it, itself counted) is green and a lone one or a spur of one is not, so a thin place among the trees is no square hole, and the outline is cut through the middle of the squares' edges as the coast is, so a corner is a slant and a park a polygon. The trees stand where they stand. The game's own trees and parks are not drawn behind the picture, so these stand in for them.
- **Sizes in pixels** (`Map25Marks`). The minimap's route line is 4.2 pixels of a minimap 300 wide, its casing 1.75 times that; the view cone is 55 pixels long (30 degrees either side), the halo's radius 11.6: the mockups' sizes at any zoom, turned into metres by what a pixel is at the middle of the picture, in steps of an eighth so a zoom writes the route again only when it matters.
- **The city map's tilt button** (`NavMapCamera`, on only while the map is open): the 2D button turns the game's own map camera flat (85 degrees) or back to the game's 47 by moving the virtual camera along the arc round the pivot; `CityMapCam.MoveCamera` recomputes the camera's place from its direction and distance every frame, so a pan, a zoom or a turn keeps the pitch. At the game's own tilt, the default, nothing at all is done. The game's tilt is given back as the map shuts. It works in every frame the map is open, panned or not, and never touches the main camera.
- **The minimap** draws the minimap's detail (a band every three floors) from a camera of its own, 40 degrees down, heading up when the minimap turns, into a render texture the size of its view; the route is a strip in the picture, the player's view cone and halo lie on the ground under the blocks, the destination and the pins are the panel's own objects placed by projection. Drawn again only when the view has moved a pixel's worth, turned half a degree or changed its scale, at most 30 times a second; nothing is drawn while the player stands still. The minimap shows no highlight.
- **The minimap's strip, compass and scale bar** (`NavHud.Strip`) sit inside the card's view: the district's name in capitals and the street under the player, a dot on a ring that goes round to where north is, and a bar of the longest round number of metres that takes a third of the view's width. They never touch the card's place, size, canvas order or the pointer (EconomyCheck's `Map25MinimapChecks` reads the source for that, beside the HUD fix's 22 checks).
- **The map check after the map shut** (RLO-PERF-022, the `route on the map` part of `MapParts`): the route's object off with the map and the camera's tilt given back; the look allocates nothing.
- **Memory** while on (the owner's 883 footprints with the height model): the minimap's set is 2.6 MB on the graphics card (graphics card only), the roads, lane marking, land, green ground and trees about 2 MB: some 5 MB in all.

### Cost

| when | what | cost (measured outside the game on the owner's 883 footprints with the height model, .NET) |
|---|---|---|
| once per game build | each building's ground and height, with its outline | in the one measure `NavBase` already does (0.8 ms a frame at most); the file grows by 8 bytes a building |
| once per city load | the blocks, the roads, the land, the green ground and the trees | 15 ms for the blocks, about 10 ms for the rest, on a pool thread (Mono is several times slower); then the meshes at 0.6 ms a frame, not behind the loading screen and not in a frame over 50 ms, for some 200 chunks |
| per frame, map shut | nothing | the object is inactive |
| per frame, city map open | the game's own city render, as before the mod; the mod's tilt compare when the button is not at the game's tilt | the game's own; nothing of the mod's drawn |
| per frame, minimap moving | the same into a small texture, at most 30 a second | the same; nothing while standing still |

EconomyCheck's `Map25Checks.cs`, `Map25LookChecks.cs` and `Map25LogicChecks.cs` (`--map25` runs them alone, `RLO_MAP25_REPORT=1` prints the figures) pin the minimap's build, the budget (also on the owner's own footprints when their minimap file is on the machine), byte for byte repeatability, the height policy, the foot, the crowns, the tank, the lane marking, the trees and parks, the pixel sizes, which kinds a view draws and in what order, the latch, and, by reading the source, that the city map never swaps the camera's culling mask and never makes a render target, that the tilt button moves only the game's own map camera and gives its tilt back, and that the map check after the map shut asks for that.

### Previews

`not-in-repo/previews/ui/map/` holds real renders by the renderer the game runs, drawn outside the game (`preview.ps1 -Method Map25Preview.Render`): the city is the owner's own, read from their minimap file (883 footprints and the water). What is made up, and said in `Map25Real`: the heights (a model: half low rise, most of the rest mid rise, a few towers, more near the middle), the roads (the gaps that run clear through the buildings of a window are avenues and streets, with lanes filling them), the trees (a street tree every 28 m, from one side of an avenue and the other in turn, thinned from the first previews' two every 14 m, which overstated them, and one park in the largest hole of the buildings), and the roads and trees keep to the land (none in the sea). The previews tint the way the game does, by the scene's own retint of the full map's set after the sets are built, so the minimap's pictures show no highlight. `real_full_flat_bare` and `real_mini_flat_bare` are the flat shots without the player's cone and halo, for measuring their share in rendered pixels. The two discs in the full shots are the preview's stand-ins for the interface's pins. A second set is drawn with a deliberately tall model.

### Not verified (the game could not be run)

That `Hidden/Internal-Colored` is found and draws in the game's player (the self-test says so in the log line "the minimap's 2.5D picture is made"), that the pins land on the minimap's roofs, that `TreeController.AllTrees` is complete when the scene is made and that the trees are the size of the picture's, the real heights, the look in the real city, and that the 2D button's tilt of the game's map camera holds while panning and zooming (`CityMapCam.MoveCamera` keeps the direction, read in its source). Each has a fallback that leaves the flat minimap or the game's own map.

## The city map's panels (`Scripts/Ui/MapDock`)

The map's side panels are drawn on the interface kit over the game's own windows. Those stay up underneath with only their own leaves hidden, and every press goes to the game's own widget. So every rule of the game's, Going Public's lines and the other mods' rows stay the game's. Each panel has its own option under "RLO interface": `map_filters` (the filters with the place card), `map_subway` (the list of stations), `map_picker` (the mod's choices of a place) and `map_district` (the district view's page). With one off, that window is the game's own again. The panels take the pointer only, because the stick and the movement keys pan and turn the map.

| File | What it is |
| --- | --- |
| `MapFiltersGame`, `MapKits` | binds the game's filter window and its station list to the host, installs and removes them, and puts a choice of a place on the HUD's modal piece |
| `MapLive` (`.Build`, `.Lines`, `.Picked`, `.Place`, `.Actions`, `.Picker`) | the one screen: looks at the game's state four times a second and lays out only when it changed |
| `UiMapFilters`, `UiMapPlace`, `UiMapDistrict`, `UiMapChrome` | the dock's pages, the route strip, the layers, the buttons, the labels (game-free, so the preview draws them and EconomyCheck lays them out) |
| `MapCountRules`, `MapSaved`, `MapBusy` | the counts, the quick chips, the place card's rules, the route's minutes, the saved places and the usual busy figures (game-free) |
| `MapStoreGame`, `MapSavedGame` | the game's side of those: the save's lines, the row of saved places on the map |
| `MapRouteTag` | the label on the route's line over the open map |

- **The filters page.** A rail of the groups with a count each, a search, quick chips (Food, Groceries, Fuel, Parking, Jobs, For rent, Your places) only where the game has lines for them, the count "11 of 31 on" (only lines the map can show now), the groups with their lines, each with a switch and a Locate. The game's own search filters the lines. Places that match the words are listed under the field. The lines are read from each group's own list, because a mod's rows are taken out of the game's table of lines, so the lines of Going Public (`bamp_players`) and of other mods show under "Other mod" and are only ever switched, never changed.
- **The picked place** (the game's building card) is a block at the foot of the dock: name, address, the way there (by road when the roads know it, else in a straight line), what it is now, Locate and Set destination. In taxi mode the button says the taxi's minutes and price. In subway mode the page is the list of stations with each one's district and where the player stands.
- **The place card** is the page the block opens. The rating and the reviews come from the Flare city's own figures, and a price tier ("$$") only shows where it can be worked out from the place's price against the city's typical one. The reviews' words are picked by the resident and the day from two lines for each rating and reason, so the same review always reads the same and nothing is saved. Tabs: Overview (address, how far it is, hours today, the unit for rent next door, busy times by the hour, what people say), Reviews and Business. Busy times are the simulation's own hourly multipliers, and "Busier than usual" is today's arrivals against a rolling average kept per place (`reallife:map_busy`, 64 places at most, the longest unlooked at dropped first). "Save" keeps a place in a list (`reallife:map_places`, 40 at most, the newest first) that is also a row of the map's filter list, "Saved places".
- **The route strip** sits over the top when Directions is chosen: Walk, Taxi, Subway and Your car with their minutes and fare and Go. Walking and driving come from the walking and driving graphs, the taxi's from `TaxiDirector.Quote` (its wait, its ride and its fare), the subway's from the two nearest stations (walk, wait, ride, walk) and only where that beats the way by road by a margin. Each option is as wide as its words need, and they give up room evenly only when the strip would meet the Layers panel. Go sets the destination as the game's own button does. By taxi it shuts the map and opens the City Cabs offer with its own Send.
- **On the map itself.** The Layers panel (the map's main lines as switches and Hide all), the map's buttons (north, centre on me, zoom in and out, tilt), a label "For rent next door" where that unit stands while the pointer rests on its row, and, when the district view is on, each district's name with the layer's figure (the cyan one is the district the page is about). The label on the route's line is `MapRouteTag`: Navigation hands it over (`NavDirector.RouteLabelTo`) when the route or the map changes and takes it back when the route is gone or the map shut. It shows the minutes and the way to go that the minimap's destination block shows, with a walker or a car, halfway along the line as drawn (`NavMath.Halfway`). The fare of a taxi or the subway is on the strip. Neither has a line of its own on the map.
- **The district view's page** has the ten layers as a rail, the city average, a donut where one helps (the classes, the rent by kind of space, who runs the businesses), the key (the kit's ramp, `UiTokens.Ramp`), the district the page is about with its rank and its residents, and every district ranked with its figure, how it moved this month and the share of space standing empty. The month's change comes from a snapshot kept once a calendar month (`reallife:district_months`, 12 months), so it says "no change yet" until a second month has been kept. The old window is still there when its switch is off.
- **The minimap's destination block** (the place's name and address, the minutes and the distance) is part of the minimap, see above.

### Cost

Nothing while the map is shut. While it is open, one look at the game's windows four times a second (a few dozen numbers) and a layout when something changed. The counts are made when the map opens, because the map pauses the game. The route's label is handed over only when the route or the map changes. The district tags are a few small pieces moved by the HUD's host, one projection each a frame while the view is on.

### Safety

Everything is read from this player's own city and nothing new goes on the network. The saved places and the busy figures are the player's own lines. Going Public's rows are only read and switched on and off, and nothing of another player's is changed or kept.

### Checks

EconomyCheck `--map5` runs the rules (counts, chips, the card, the route, the saved places, the months, busy times, the tags and every command reaching its call), the screens' own cases (`map_filters`, `map_subway`, `map_picker`, `map_district`) are laid out in every language, shape and input by `--ui`, and NavCheck holds `NavMath.Halfway`. `--map25` still holds the picture and the minimap.

### Previews

`not-in-repo/previews/ui/map5/` holds a sheet for every state in English, Portuguese and Chinese at 1080 and 720, with the keys, the pad and the pointer.

### Not verified (the game could not be run)

That the host binds the filter window and the station list as the games' windows are built, that the pointer's glide to a place of a choice works, that the HUD's host shows the tags and the labels over the map (it may sleep while the map is up), that the saved places' row and the taxi's hand-over to City Cabs work, and that the game's own building box does not also show beside the picked place's block.
