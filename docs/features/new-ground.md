# New ground

## In a sentence

The machinery for ground the mod lays itself: the city's two walking meshes grown over it for the player and the pedestrians, streets appended to the game's traffic, the game's parking lanes copied along its kerbs, pedestrian zones and crossings, and bus termini, laid at every load and never saved, for a district of the mod's own. Players see nothing of it yet: the district that will use it is not built, so it has developer tests and no Guide page.

## Where the code is

- Folder: [`Scripts/NewGround`](../../Assets/Mods/RealLifeOverhaul/Scripts/NewGround/).
- Read first: `RoadRules.cs` and `GroundRules.cs` (game-free, run by NavCheck's `NewGroundCheck.cs`), then `GroundWork.cs`, the front door a district lays its streets and ground through.

```
RoadPlan          the road grid as the game has it at this load, a plan's roads, junctions and joins, what a build comes to (game-free)
RoadRules         the generator: lanes each way, turns through junctions, lane changes, junctions with lights or priority, cells,
                  names, spawn points, the checks every build passes, the stand-down where the game has a road (game-free)
GroundRules       a walking mesh's tile grid, boxes of new ground, ground plans, the tests' boxes and samples, the self-check lines (game-free)
WalkGame          the only file that calls Unity's navigation: the city's two walking meshes found at every load, samples,
                  paths, builds, updates, links, the old mesh's own triangles, the game's colliders on new ground
GroundBuilder     the walking core: the builder's dry run, then a job's passes over the city's own mesh, a tile at a time
RoadsGame         the only file that writes the traffic's road data: the append before the traffic starts and its undo, the new
                  junctions' lamps and their red lamp for a crossing, the joins found again, the census, the test road's stretch
ParkingGame       copies of the game's parking lanes, stood where a kerb wants them
PeopleGame        pedestrian zones and crossings, copies of the game's own
GroundWork        a district's streets and ground at every load, standing down where the game has built since
GroundDirector    the one component: the tests and what they lend (.Patch, .Tests, .Roads, .Parking)
NewGroundGuide    the feature's entry: the developer option and the seven tests; NewGroundText: the locale keys
```

One line each elsewhere: `Core/GameContract.cs` (part `new_ground_walk`, six members of the game's `Unity.AI.Navigation.NavMeshSurface`, which the mod is not built against), `Core/Settings.cs` (`newground_test_road`), `Meta/OptionsTree.cs` (the developer section), `Core/RealLifeOverhaulMod.cs` (the director added in the city's load, before the road data is read for anything), `Meta/FeatureCities.cs` (New York's), and `Transport/TransitMap.cs` (the termini).

## What the game does

- **Walking:** everything walks on two meshes baked in the editor, `NavMeshPlayer` (the player outdoors, agent type 1479372276, slope 26 degrees, the Roads layer baked too) and `NavMeshNPC` (pedestrians and the player indoors, agent type 0, slope 38 degrees), in tiles of 512 voxels of 0.07 m (35.84 m). The game never builds one at run time and has no links. The keys move the player with `NavMeshAgent.Move`, which stays on one mesh; a click sets a path.
- **Traffic:** the game's traffic system keeps its lanes in `CurrentSceneData` on the Streets scene: lane points on an 80 m grid, road ids, and junctions with lights or priority. It takes the lane points and road ids by reference when it starts, in the game's own "game loaded" call after the mods' city load, copies its junctions once and reads its grid live. A lane point north or east of the grid throws in the traffic every frame; one south or west of its corner is filed in the wrong cell.
- **Parking:** a `ParkingLaneGenerator` is a box on the ParkingArea layer that parks the game's cars along itself and makes the spots the player's car parks itself in. It uses neither the traffic nor the walking meshes.
- **Pedestrians:** people come onto the street at the game's `StationaryAiSpawner` zones 50 to 90 m from the player, and wait at a crosswalk trigger until the red lamp for the cars on its road is lit.

## What a district does with it

At every load, in the city's load (OnLoadAsync, behind the loading screen, before the traffic starts):

1. **Streets:** `GroundWork.Streets(plan, out RoadBuild build)` with a `RoadPlan`: its roads (a middle line, lanes each way, 3.5 m lanes, a speed, cars and trucks), its junctions (lights with the game's 10 s of green and 2 s of yellow, or priority with four roads at most) and its joins to the game's lanes (a fork from a lane of the game's into a new lane, or a merge back). Null when the streets are in the traffic; otherwise why they stood down, the road data as it was.
2. **Ground:** `GroundWork.Ground(plan)` with a `GroundPlan`: boxes of new ground (pavements, plots' fronts, squares; a box is its walking surface's height, size and turn), how many of the game's colliders stood on them when the plan was made, and which of the two meshes to grow. Null when both meshes walk over it.
3. **Buses:** `TransitMap.AddTerminus(at, nameKey, name)` for each terminus, before the routes are laid (`TransitMap.Prewarm`, once the traffic has started behind the loading screen). Lines begin and end there as at one of the game's subway stations; the termini go with the city.

Then, once the game is loaded:

4. **Parking:** for each kerb, `ParkingGame.Nearest` for a lane of the game's to copy, `ParkingGame.Room(template, at, turn, out why)`, then `ParkingGame.Copy(template, at, turn, out why)`. The copy is placed and sized before it wakes, so it parks the game's cars where it stands; `ParkingGame.Remove` gives them back to the game's pool.
5. **People:** `PeopleGame.Zone(at, yaw, size, out why)` on pavements and squares; for each crossing at a junction with lights, `PeopleGame.Crossing(PeopleGame.CrossingTemplate(), at, yaw, size, RoadsGame.RedLamp(build.Junctions[j], way))`. `CrossingTemplate` searches the scene once a city: behind the loading screen.

The district also widens the city map's bounds over its ground and measures the minimap's base again: `RoadsGame.MapBounds` reads the map camera's bounds as the game has them (its `CityMapCam`), and the census prints them.

## Every load, read from the game

Nothing of new ground is kept from an earlier load or written in the code: the road grid (its corner, its cell and its size) and the highest road id are read from `CurrentSceneData`, a join's lane point of the game's is found again within 8 m of its place (a game update renumbers lanes), the two walking meshes are found among the surfaces the game has switched on (the pedestrians' by the agent type of the game's own `NavMeshHelper.NpcNavMeshFilter`, the player's as the largest of the other), and each mesh's tile grid and build settings are its own. A source check fails if the front door holds a grid, a tile or a map figure of its own.

Each part also looks whether the game has built where it lays (a free update) and stands down with the reason, which the caller writes as one log line:

- **Streets:** a lane point of the game's within 6 m of a new one, joins apart.
- **Ground:** more of the game's colliders on the plan's boxes than when the plan was made.

## The road core

- **The generator** (`RoadRules.Build`) lays a plan as the game's traffic system lays its own (read from its baked lanes): traffic keeps right; a lane's points stand 4.8 m apart (the game's median is 4.81 m) and are named `Road_n-Lane_k-Waypoint_i`, the last before a junction `-Out` and the first after one `-In`; a turn through a junction is `-CConnect`, `-Connector`, `-CConnect` points that keep the road they come from, so the traffic's blinkers see the road change at the `-In` point; a left turn leaves from the lane nearest the middle, a right turn goes kerb to kerb, straight on goes lane by lane, the kerb lane feeding any extra lanes ahead; a lane change is a point a step ahead on the lane beside, the same way; a car may be put on the road on any plain point that is not a junction's stop or exit. Every road has a road id of its own above the game's, every point is filed in its cell, and every junction lists its stop points per road and its exits.
- **The checks** (`RoadRules.Check`), before anything is appended: every point at least 1 m inside the road grid and filed in its own cell (never a point off its north or east edge), every link both ways and no longer than 16 m, a link to the game's lanes only through a join, nothing that leads nowhere or that nothing leads into, names as the game reads them, no car put on the road at a turn, lane changes to a plain point nearby, road ids above the game's, speeds of 10 to 130 km/h, every junction with a way in and out, a priority junction four roads at most, lights with their times.
- **The append** (`RoadsGame.Append`): refused once the traffic has started (it is never started again); new arrays with the game's own points first, so no index the game or Going Public uses changes; the joins added to the game's points' neighbours or previous points; each new point filed in its cell and spawn list through the game's own `GridCell.AddWaypoint`; the new junctions after the game's (their light indices never change) and filed in their cells. Anything that fails is taken off again before the traffic sees it.
- **Lamps:** a new junction with lights gets plain lamp objects per road, which the game's lights switch on and off; a crossing over a road links to that road's red lamp (`RoadsGame.RedLamp`), and a street's art may put the game's own lamps under them.

## The mod's road users

They find the lane points only through `Core/RoadData` (a source check), which hands out the scene's array live, so an appended road reaches them all: the shared road graph (`Core/RoadGraph`) is built again for a new array, the traffic tools' lane grid is keyed to the array, and no save keeps a lane point's index. The GPS, taxis, the buses, the police's road map, road closures and street events all read the same arrays; the buses run between the game's subway stations and, from now on, the termini of the mod's own (above).

## The walking core

- **The dry run** (`GroundBuilder.DryRun`), once a load for each mesh before the city's own data is touched: on data of the mod's own 400 m below the player, with the city mesh's own settings, two tiles built together must join, a tile rebuilt alone without the agent's margin must keep the tile beside and the join, and an empty rebuild must clear its tile. If Unity's builder does not do all three, nothing is grown.
- **Growing the city's own mesh** (`GroundBuilder.Plan`, then three passes, each tile its own update with bounds 0.01 m inside it and the tiles beyond kept as they are):
  - **Fresh:** a tile with none of this mesh yet is built from the new ground's boxes with the agent's own margin (0.25 m).
  - **Join:** a tile that holds the old mesh is rebuilt from the old mesh's own triangles (`NavMesh.CalculateTriangulation`, read where the tile is, since the game's meshes need not be readable in a player's build) and the new ground, without a second margin (the triangles already keep it; the new ground's boxes are made 0.25 m smaller instead), so old and new are one surface the keys walk across. A plan's boxes reach about 0.5 m back over the old pavement (`GroundRules.JoinOverlap`, as the grow test's strip does), where the old mesh stops short of the kerb. Before anything is built, every sample of the old mesh on a tile to be rebuilt must lie on a triangle read from it, or the job stands down.
  - **Back:** the covered tiles rebuilt from the old triangles alone: the tests' giving back, and a district's mesh that failed.
  Samples of the tiles round about are printed before and compared after, so "polygons elsewhere unchanged" is measured, not assumed; a district's mesh that moved anywhere else is given back and stands down.
- **The fallback:** walking data of its own joined by two-way links (`WalkGame.Link`). A click walks over a link; the keys probably do not, which is why growing the city's own mesh comes first.

## Saves and loads

Nothing is saved. A district's streets, ground, parking, people and termini are laid again at every load, so a save always loads without the mod. The tests' ground is given back after a minute of play, indoors, at a load and when the city goes; the test road stays until the next load, as the traffic is never started again. The developer option is a setting, not a line of the save, and the notices stay out of the save.

## Going Public

Nothing new is sent. Appending at the end keeps every index the session sends (cars and lights by index). A street only one machine has would float the host's cars over nothing for the others, so `GroundWork` is never called in a session unless every machine runs this same version of the mod, and the test road and the parking copy are refused in a session. The walking meshes are each machine's own.

## Cost

The director is disabled while nothing of its own runs; while a test runs, four looks a second, and between looks one time comparison a frame (a source check). Streets: once a load, behind the loading screen; a district of 100 junctions and 180 streets builds and checks in under 400 ms offline (a timing check). Ground: the dry run once a load for each mesh, then each tile's update behind the loading screen; the time of a whole district's tiles is not known until it is built in a game (the tests time one tile and a 20 m patch).

## Self-check lines (Developer diagnostics)

- `Walk check (navmesh here): Player 9 of 9, NPC 3 of 9 at (x, z)`: both meshes sampled within 1 m at the player and at 8 points 10 m round.
- `Walk check (runtime build): Player N tiles in N ms, NPC N tiles in N ms, sample PASS, path from the player COMPLETE`, then `Walk check (sources): ...`, which of the game's meshes near the player could be build sources.
- `Walk check (dry run, Player): ... PASS (old tiles rebuilt with a margin of 0.00 m), in N ms`, for each mesh as the grow test begins (a district's dry run says only when it fails, in the reason it stands down with).
- `Walk check (grow one tile, Player): the new tile alone: path ...; with the tile beside rebuilt from the old mesh's own N triangles: path pavement to new ground COMPLETE: PASS, polygons elsewhere unchanged`, and when it is given back `Walk check (given back, Player): ... PASS`.
- `Walk check (link): path to the patch Player COMPLETE, NPC COMPLETE: PASS`, then after 20 s `Walk check (pedestrians on new ground): ... at most N people on the patch in 20 s: PASS`.
- `Roads check (census): corner (-4294.8, -2511.6), 80 m, 46 x 74, N lane points, N road ids (N roads), N junctions (N with lights, N priority), player in row r column c: PASS`, then `Walk check (census): ...` with both meshes' grids and the map's bounds.
- `Roads check (append): traffic started with 18791 + N lane points, ... road ids for all, N of N new stop points marked by their junctions: PASS`, then `Roads check (test road): cars that drove it: M in 2 game minutes`.
- `Parking check (copy): N cars parked, N spots for the player's car: PASS`.

## Tests

- **Options > Tests** (with "Show test tools" on), new ground's seven, each saying why in a notice when it cannot run, each giving back what it lent:
  - **Walk: what is under me**: the two meshes at the player and 10 m round.
  - **Walk: build a test patch**: 20 by 20 m of walking data of its own beside the player for both meshes, timed and sampled, for a minute of play.
  - **Walk: grow the city's own walking ground by one tile**: each mesh grown into the empty tile beside the pavement, flush with it, the tile beside rebuilt; walk onto it with the keys.
  - **Walk: link the patch**: the fallback, a 2 m two-way link from the pavement to the patch for each mesh, and a pedestrian zone of the game's own copied onto it; try the keys across the join.
  - **Roads: census**: the road data, the player's cell, both meshes and the map's bounds as the game has them.
  - **Roads: add a test road at the next load**: switches the developer option (Options, Troubleshooting, New ground (developer)): from the next load a copy of a straight stretch of a lane near where the save was made is appended as a road of its own, joined by a fork and a merge; nothing spawns on it, so every car on it chose the fork. Press again to stop it.
  - **Parking: copy a lane**: the nearest straight lane of the game's within 40 m copied at least 30 m along the street, its cars counted, for a minute of play.
- **NavCheck** `NewGroundCheck.cs`: the road grid's lookups and room; a town (a junction with lights and four arms joined to the game's lanes) and a block of four priority junctions, built and checked point by point (names, links both ways, turns, lane changes, spawn points, cells, stop points and exits); never a point off the grid, at any edge; every fault refused; the test road's copy; the stand-down where the game has a road; the cost; the walking rules (tiles, boxes, the patch, the strip, samples) and the self-check lines; the sources (the game-free files, only `WalkGame` calls navigation and only `RoadsGame` writes road data, every game member read by name a line of the contract, nothing saved, sent, random or LINQ, the road users reach the road data only through `RoadData`, the append's order, the director's place in the load and its cost, every update one tile keeping the rest, the parking copy placed before it wakes, the red lamp's name, the front door's stand-downs, the buses' ends); and every text in en, pt and zh-cn.
- **ContractCheck**: the six members of part `new_ground_walk` against the installed game's `Unity.AI.Navigation.dll`.
