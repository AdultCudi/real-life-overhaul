# Uptown

## In a sentence

Uptown is a district of the mod's own north of Manhattan: at every load the whole band of three rows of eighteen blocks is laid, Manhattan's streets going on north past 1st Avenue through its own junctions, 1st Avenue going on west and east, nineteen streets and three avenues meeting at junctions with lights, over ground both walking meshes grow into from Manhattan's, with the game's own road modules, lamps, lights, crosswalks, parking, people and three bus termini, and lots for the district's buildings; nothing of it is saved, so a save loads without it.

## What a player sees

- **At the old wall.** The old dead ends north of 1st Avenue are gone: their cones, barriers and closed signs, the low backdrop behind them and the invisible wall across each street. Standing on 1st Avenue's north pavement, the player looks up 9th to 1st Street as they go on north, between the backs of 1st Avenue's buildings, to Uptown Avenue 147 m on, and on to Summit and Crest Avenue. 1st Avenue's own buildings, doors and pavements are the game's and stay as they were.
- **Walking in.** The keys walk the player straight up any of the nine streets from 1st Avenue's corner, across where the wall stood, on either pavement or the carriageway, and onto every block, street and avenue of the band; the pavements, the carriageways and the blocks are one surface, so the player steps off a kerb or onto a lot as on Manhattan's. At 1st Avenue's two ends the avenue goes on, walkable, past the walls that stood across it. Pedestrians walk every pavement of the band and cross the avenues at every junction.
- **Driving in.** The game's traffic turns into eight of Manhattan's streets at 1st Avenue's lights (5th Street, which 1st Avenue's traffic passes without a junction, has its lanes from Uptown Avenue north), into 1st Avenue West through 9th Street's corner, and drives the whole band: every street and avenue meets at a junction with lights, the avenue's two ways on one green and the street's two arms on the other, as at the game's own crossings. 1st Avenue East begins at Hill Street: 1st Avenue's corner at 1st Street takes no new way in. Parked cars line the streets' west kerbs and both of the avenues'.
- **The band.** Three rows of eighteen blocks from Willow to Lake Street, 422 lots round them, the first block (row 1 between 9th and 1st Street) behind the backs of 1st Avenue's buildings. Its edge is walled as the city's own edge is: north of Crest Avenue's north pavement, west of Willow Street, east of Lake Street, and south of 1st Avenue's continuations. Past the walls the streets go on north one length and the avenues one length west and east, with blocks' grounds round about, so the camera never looks out on nothing.
- **On the maps.** The city map pans over it; the minimap draws its streets, its ground and the buildings the mod stands there. Bus lines end at its three termini, on the south pavements of Uptown Avenue (between 5th and 4th Street), Summit Avenue (between Maple and Cedar Street) and Crest Avenue (between Ridge and Grove Street).

## Where the code is

- Folder: [`Scripts/Uptown`](../../Assets/Mods/RealLifeOverhaul/Scripts/Uptown/).
- Read first: `LayoutRules.cs` (the band and its lots), `LayoutPlans.cs` (what the load lays), `UptownLots.cs` (the lots for other features), `GroundDirector.cs` (the load, step by step).

```
LayoutStreets        game-free: the band's 22 streets, their ids, names, short names and first numbers
LayoutRules          game-free: the grid on the game's own, the lots of the band and of the first block, their ground
LayoutPlans          game-free: the band's lanes, walking ground, modules, walls, colliders, hiding, zones, termini
UptownLots           the lots for the district's features and the builders: UptownLots, Lot, LotClass, GroundRect, UptownExit
GroundSession        whether the district may stand at this load: its option and the Going Public session
GroundDirector       the load, the stand-downs, the late parking and people, the clean-up
  .Tests             the three test entries
GroundGame           the Game file: the backdrop and its strays, the walls, the colliders, the map's bounds
  .Art               the game's road modules and block grounds copied: names, lights, crosswalks, signs; parking lanes and zones
GroundGuide          the feature's entry: its option, its tests, its clean-up
GroundText           the locale keys
```

One line each elsewhere: `Core/Settings.cs` (`uptown`), `Meta/OptionsTree.cs` (its section in the City group), `Meta/FeatureCities.cs` (New York's), `Core/RealLifeOverhaulMod.cs` (the director, before the new buildings' in the city's load), `Core/GameContract.cs` (part `uptown_labels`: `Road.streetName`, optional), `Core/GoingPublic.cs` (the session's mod check), `NewBuildings/NewBuildingsRules.cs` (Uptown's streets among the mod's own) and `Workshop/locales.py` (the street names, fixed words in every language).

## How it talks to other features

- **New ground** ([New ground](new-ground.md)) lays it: `GroundWork.Streets`, `GroundWork.Ground` (both meshes, grown in shared updates and joined to the city's own), `GroundWork.TakeBack`, `RoadsGame.LampOf` and `RedLamp` for the lights, `ParkingGame` and `PeopleGame` for the parking and the people.
- **The buses** (`TransitMap.AddTerminus`): Uptown's three termini are ends of the network, laid behind the loading screen.
- **The minimap** reads the band as it reads the city: its lanes are among the game's (so its streets are drawn and its rectangle reaches over the band), the rays that measure the ground find its ground colliders, and the buildings the mod stands there come through `NavBridges.MoreBuildings`.
- **New buildings** ([New buildings](new-buildings.md)): Uptown's 22 streets are among the mod's own (`MadeStreets.All`), so a building on its lots has an address the game writes and finds; the lots are open before the city's buildings are made at the load.
- **The district's life and buildings** and **the builders** read the lots (below). The district save is told the district's ground and its way out by the district's own feature, from `UptownLots.Ground` and `UptownLots.Exit`.

## The layout

All of it is fixed data (`LayoutRules`, `LayoutStreets`, `LayoutPlans`), the same on every machine and in every version, measured from the game's scene at Manhattan's north edge (Build 3682) and checked against the game at every load.

- **The grid** is the game's own: Manhattan's north to south streets every 87.3 m (a street's 100 m module, a crossing's 37.3 m), 9th Street's middle at x -301.67; its west to east avenues every 147.2 m, 1st Avenue's middle at z 464.58. A street is 17.3 m wide kerb to kerb with its pavements, an avenue 27.3 m.
- **The band** is three rows of blocks north of 1st Avenue, 18 columns from five streets west of 9th Street to five east of 1st Street (x -746.8 to 841.9, z 450.9 to 919.8 with 1st Avenue's continuations): nineteen streets (Manhattan's nine continued, Willow, Birch, Elm, Maple and Cedar Street west of them, Hill, Ridge, Grove, Park and Lake Street east of them) and three avenues (Uptown, Summit and Crest Avenue). Seventeen of its blocks are squares with no lots.
- **The first block** is row 1 between 9th and 1st Street: eight blocks behind the backs of 1st Avenue's buildings, where the band meets Manhattan.
- **Streets and numbers.** Every street has an id of the mod's own (`reallife:street_uptown_<name>`), under the prefix no street of the game's can have, and a name in the texts. A street that continues one of Manhattan's shows that street's name, with numbers from 1001, which the game's (82 at most on any street) never reach; the others number from 1. Odd numbers on a street's west side and an avenue's south side, even on the other, two apart in order from the street's south end or the avenue's east end.
- **The lots** run round each block, behind its streets' pavements: a strip along the avenue north of it, one along the avenue south of it (rows 2 and 3), and one along each street; in the first block the south strip is 1st Avenue's buildings, and the lots begin behind their backs. Each lot is of a size class with the footprint of the game's buildings of those sizes, picked so the band keeps Manhattan's mix:

| Class | The game's sizes | Footprint (m) | Manhattan | The band (422) | The first block (83) |
| --- | --- | --- | --- | --- | --- |
| Small | A, B, C, F | 11 x 24 | 64% | 277 (65.6%) | 67 |
| Office | D, J, K | 23 x 32 | 14% | 60 (14.2%) | 7 |
| Big | M, L, N | 23 x 31 | 13% | 55 (13.0%) | 6 |
| Warehouse | H, I, P, Q | 29 x 39 | 7% | 30 (7.1%) | 3 |

## The lots

`Scripts/Uptown/UptownLots.cs`, a stable surface for the district's features and the builders:

```csharp
IReadOnlyList<Lot> all = UptownLots.All;          // the band's 422, in id order
IReadOnlyList<Lot> first = UptownLots.FirstBlock; // the first block's 83, where the band meets Manhattan
Lot lot = UptownLots.Get("uptown_1_09_04");      // by its id, or null

if (UptownLots.Open(lot))                         // laid and walkable at this load: every lot of the band while it stands
{
    // lot.X, UptownLots.GroundY, lot.Z: its middle on the ground
    // lot.Yaw: whole degrees, as CivicWorks.StartPlot takes a plot (its front at -Z turned by it)
    // lot.Facing: as MadeLot has it, 0 = +Z, the way the street lies from the lot
    // lot.Width (along the street), lot.Depth: whole metres
    // lot.Street, lot.Number: its address; lot.Class and lot.Sizes: the game's sizes that fit
    // lot.Block: Row * 100 + Column
}

int seen = UptownLots.Stamp;                      // changes when the lots open or close: a look once a second is one comparison
UptownLots.Closing += why => { };                 // raised first in the feature's clean-up, each handler in its own try
```

| Member | What it is |
| --- | --- |
| `Lot.Id` | `uptown_<row>_<column, two digits>_<n, two digits>`: letters, digits and underscores, safe in any line of a store. Never changes. |
| `Lot.Row`, `Column`, `Block`, `First` | Its block: row 1 to 3 north of 1st Avenue, column 1 to 18 west to east (the block east of 9th Street is 6); `First` for the first block's. |
| `Lot.Street`, `Number`, `Order` | Its address, and its place along its side of the street. |
| `Lot.X`, `Z`, `Yaw`, `Facing` | Its middle and its turn: `Yaw` as `CivicWorks.StartPlot`, `Facing = (Yaw + 180) % 360` as NewBuildings' `MadeLot`. 0 or 180 faces an avenue, 90 or 270 a street. |
| `Lot.Width`, `Depth`, `Class`, `Sizes` | Its footprint in whole metres, its class, the game's building sizes of its class. |
| `UptownLots.Ready`, `Why` | The district stands at this load; else why not, in English for the log. |
| `UptownLots.Open(lot)` | `Ready` and a lot of the plan: every lot of the band, as the whole band is laid. |
| `UptownLots.Closed`, `Closing` | The district was closed by its feature's clean-up and stays closed until the next load; `Closing(why)` comes first. |
| `UptownLots.Ground`, `Exit` | The district's ground as laid now (23 rectangles: the first block's eight blocks from their backs, its nine streets past the old wall and its stretch of Uptown Avenue; the band west of 9th and east of 1st Street; the rows north of Uptown Avenue between them; 1st Avenue's two continuations) and its way out for the player and the cars: 1st Avenue's north side at 5th Street (91.18, 0, 473.33), heading 270, in Midtown. |

`Ready` is set by the ground's step in the city's load, behind the loading screen, before the city's buildings are made: a building of the mod's own that declares itself with `Wanted = () => UptownLots.Ready` stands on a lot laid at that very load. It is false again when the city goes. A handler on `Closing` subscribes once a city and leaves when the city goes. Every lot has the player's walking ground under its middle and its corners a metre in and both meshes a metre out of its door, and nothing of the game's stands on it but, at lot `uptown_1_14_04`, the crown of a tree on 1st Street's east pavement, which leans 2 m over its corner from 3.5 m up (the offline scan, below).

## At every load

In the city's load (OnLoadAsync), behind the loading screen, before the traffic starts, in the director's `Awake`; the whole band or nothing:

1. **Asked.** Its option (`uptown`, on by default, read only at a load), New York (the city check leaves the director out in another city) and the session (below).
2. **Found.** Everything of the game's it is laid against, before anything changes: the backdrop round the city (`MapBorder`) and its invisible walls, the city map's camera, the road modules to copy (9th Street's 100 m south of 1st Avenue, 1st Avenue's 50 m east of 9th Street, and each street's own crossing at 1st Avenue, by name, place and the size of its mesh, so a module the game batched with others is never copied) and a block's ground (`SM_120-70 GroundPlane`). One missing, and nothing is laid.
3. **The backdrop put away** where the band stands: every piece of it (a renderer, a collider or a level of detail) whose middle lies at an old street end, on the band, on 1st Avenue's continuations or under the scenery past the walls, about 500 of them; its block grounds stay, the lots' ground. 1st Avenue's own buildings are not the backdrop's and are never touched. The one stray of the Hamptons' scene that stands on the band (a parked bicycle in the first block) goes with it. The game's nine walls across the street ends and the two across 1st Avenue at 9th and 1st Street are switched off; the wall across 9th Street's south-west corner stays. The ground's check (step 6) counts every collider of the game's left on the district's ground.
4. **The streets** (`GroundWork.Streets`): 119 roads, 56 lengths of street and 63 of avenue: each of eight continued streets' two lanes from 1st Avenue's junction, through it (its stop points and exits given to the game's junction, which lights the new approach from its own pole that faced the dead end), to Uptown Avenue; every street's two lanes between its avenues; every avenue's four lanes between its streets; 1st Avenue West's last length through 9th Street's corner into 1st Avenue; 67 junctions with lights, the avenue's two ways on one green and the street's two arms on the other. About 10,200 lane points, 20 given to the game's junctions.
5. **The art.** The game's modules copied along the band, made asleep and stripped before they wake: each street's 100 m (57) and each avenue's 50 m (64) named on the map with its own name; each crossing (67) with its light poles hung under the lamps its junction switches, pole by pole to the approach whose light it is, the poles no approach comes to dark; its crosswalks waiting for the red of the road they cross, those onto no walking ground taken away; signs that name another street taken down; a block's ground on every block where the backdrop keeps none; and past the walls 19 street lengths, 8 avenue lengths and their grounds as scenery, with no parking, lights or names. The art stands before the ground grows, so what of it stands on the ground is walked round.
6. **The walking ground** (`GroundWork.Ground`), first the player's, then the pedestrians' (below): the game's colliders on it counted against none; everything solid standing on it gathered; both of the city's meshes grown over the whole band in a few shared updates of Unity's builder and joined to the city's own.
7. **The edge.** Five walls of the game's height round the band; three ground colliders (the city's own ground ends at z 606, short of Uptown Avenue) and one road collider under each carriageway (62).
8. **The maps.** The city map's bounds widened over the band and 60 m round it; **the bus termini** are added before the buses' routes are laid.
9. **The lots open** (`UptownLots.Laid`).
10. **Once the game is loaded** (its late call, the loading screen still up): a parking lane along each copied street's west kerb and both of each avenue's kerbs (185), each a copy of the template module's own (new ground's `ParkingGame`), and 111 pedestrians' zones on the pavements.

### The walking ground

The player moves by the walking mesh itself (the keys push the player's agent with `NavMeshAgent.Move`), so the band is walkable only where the game's own meshes are: the ground is grown into the game's two meshes (`NavMeshPlayer` and `NavMeshNPC`), never laid beside them.

- **The player's** (145 boxes): every street, avenue and block of the band whole; in the first block, each carriageway from z 476 and each pavement from z 478.5, over Manhattan's own ground at the old dead ends, and each block from 0.3 m behind the backs of 1st Avenue's buildings; 1st Avenue's continuations over its old corners to x -310 and 404.
- **The pedestrians'** (239 boxes): every pavement of the band, the first block's as the player's, 1st Avenue's continuations' north pavements, and a crosswalk over each avenue on both sides of every junction. Never a carriageway nor a block.
- **What stands there.** The ground keeps 0.3 m off the scaffolds and building fronts that stand on five of the first block's old pavements and off the game's wall that stays at 9th Street's corner; a strip narrower than a metre beside one is let go. Everything else solid on it (lamps, trees, bins, bags, meters, the copied modules' own furniture) is built as no walking surface, at least two voxels across.
- **One surface.** The agent's margin (0.25 m) is kept only where the ground meets nothing: a kerb between a carriageway and its pavement, or a block and its street, stays one surface. (Made smaller on every side, the boxes left half a metre at every kerb: the owner's sidewalks of 23 Oct.)
- **Joined.** Where the band meets the city the game's own mesh is kept and the new ground overlaps it: by 2.5 m or more over every carriageway, 0.5 m or more over every pavement, 1.5 m or more at 1st Avenue's corners, the agent's margin taken off (measured from the game's data; the pavements began 7 m short of it before, so nobody could walk onto them from Manhattan: the owner's sidewalks of 23 Oct). The tiles over the band's ground are rebuilt whole, so what the game baked on the backdrop there goes; in the tiles where the band meets the city, only the old ground that 1st Avenue's seeds stand on is kept.
- **Grown.** The covered tiles go to Unity's builder as a few rectangles, each one update that builds its tiles on every core at once: 618 tiles of the player's mesh, 601 new in 4 updates and 17 joining in 10. In the lab, a player of the game's Unity on stand-ins of the game's own meshes: both meshes grown in 2.5 s (the player's 2.0 s, the pedestrians' 0.5 s), Manhattan's ground unchanged.

### The game's ground checked

Each step that meets something of the game's where the district goes stands down with one log line, `Uptown: not laid at this load: ...`, and everything laid before it is given back the same load: the backdrop shown again, the old walls switched on again, the streets taken back off the traffic's data (`GroundWork.TakeBack`), the mod's own pieces taken away. So the district never covers the game's content:

- **Streets:** a lane of the game's within 6 m of a new one, the joins apart; one of 1st Avenue's junctions or 9th Street's corner not found.
- **Ground:** any collider of the game's on the walking ground but a small prop, where the plan was made with none (a free update that builds north of 1st Avenue). Proven offline for Build 3682: with the backdrop and the stray put away and the old walls opened, the scan of every level the city loads finds none on either plan.
- **Art:** a module or the block's ground to copy missing, moved, resized or batched.

The pedestrians' ground failing alone leaves the district standing without pedestrians.

## Going Public

The district's streets, ground and lots must be on every machine of a session or on none, so it stands in a session only where every machine is known to run this mod in this version (`GroundSession.Agrees`): on a host whose Going Public refuses a player whose mods differ from its own (`GoingPublic.RefusesOtherMods`), and on a guest whose mods Going Public did not find to differ from the host's (`GoingPublic.ModsAsHost`). Elsewhere in a session it is not laid, with one line. Nothing of it is sent; the lanes and their order are the same on every machine, so the traffic the host runs is the guests' too. A guest whose mods match a host that lets in other mods lays it alone: the host's traffic never drives it there.

## Switching off and the clean-up

- **The option** is read at a load: off, nothing of the district is laid at the next load, and the game is as without it. The option's row says it applies at the next load.
- **The feature's clean-up** (the options page's, five faults in a row, the save tools' removal, another city) closes the district until the next load: `UptownLots.Closing` first, then every lot closed; its parking lanes and pedestrians' zones give their cars and people back to the game's pools. Its ground, art and walls stay until the city goes: the traffic, once started, keeps its streets.
- **When the city goes** the mod's own pieces go (the copied modules, the walls and colliders, the parking, the zones), the map's bounds are put back, and the game's own pieces go with their scene. The save tools stopping the mod in a city that stays leave what stands until it goes.

## On the maps

- **The minimap** draws the band: its lanes are among the game's, so its streets are drawn with the city's and the picture's rectangle reaches over it (x to about 860, z to about 936); the ground is measured anew the first time (the kept measurement is for the city's own rectangle), and the rays find its ground colliders as land; the buildings the mod stands on its lots are drawn through `NavBridges.MoreBuildings`. Its buildings and water picture, 2,048 pixels at most a side, now holds about 4.05 km of city instead of 3.6 km (the city with the first block alone, at 1.77 m a pixel): about 1.98 m a pixel.
- **The city map** pans over the band (its bounds 60 m past it). The game's fog over the city's border (`MapBorder/BorderOverlayFog`, a mesh at 46 m with the playable city cut out of it) still lies over the band: its mesh cannot be read at run time, and switching it off would clear the fog round the whole city. Left as the game has it; a choice for the owner.

## Cost

The director is disabled while no test runs: no frame cost. The load, behind the loading screen: two searches of the scene for the road modules, the backdrop's pieces walked once, the streets' build (the band's plans and build together under 400 ms offline, a timing check), 215 modules and up to 74 grounds copied, both walking meshes grown (2.5 s in the lab on 16 cores; the game's own line gives its time), the parking and zones once the game is loaded. The traffic test looks four times a second for two game minutes.

## Self-check lines

In Player.log, the first lines always, the rest with Developer diagnostics on:

- `Uptown: the Player mesh walkable on 145 of the new ground's 145 boxes; 618 tiles (601 new in 4 update(s), 17 joined to the city's own ground in 10), N of the city's own triangles kept, 1 of N islands of old ground joined, N things standing on it walked round, built in N ms.` and the same for the NPC mesh.
- `Uptown: laid behind the loading screen in N ms: N lane points (N in junctions, N where cars may start) on 119 road(s), 67 junction(s) (67 with lights), N join(s) to the game's lanes, 10 stop point(s) and 10 exit(s) given to the game's junctions in N ms; N pieces of the game's copied (N blocks' grounds, N past the walls) in N ms; the player's walking ground grown in N ms, the pedestrians' in N ms; N pieces of the backdrop and 1 strays put away, 11 of the game's walls opened, 70 walls and colliders built, 3 bus termini; 422 lots open.`, or `Uptown: not laid at this load: why.`
- `Uptown: once the game was loaded, 185 parking lanes along its kerbs and 111 pedestrians' zones.`
- `Uptown check (streets): Roads check (append): traffic started with ... marked by their junctions: PASS`, in the game's late call.
- `Uptown check (census): ...: PASS`, `Uptown check (path, Player): ... COMPLETE: PASS`, `Uptown check (path, NPC): ...`, `Uptown check (traffic): cars that drove its streets: N in 2 game minutes (...): PASS`, from the tests.
- `Minimap check (base): ... world rectangle (...) to (about 860, about 936) ...: PASS`, once the minimap's picture is made.

## Tests

Under Options > Tests (each says why in a notice when it cannot run; none changes the city, so each runs in a Going Public session too, and each works from wherever the player is):

- **Uptown: census**: whether the band stands and how it was laid, piece by piece, against what the plans lay: streets, junctions, walking ground, the backdrop and its strays, walls, modules, grounds, scenery, lights, crosswalks, signs, parking, zones, the map's bounds, the lots, and whether the player stands on the district's ground.
- **Uptown: a path from Manhattan into Uptown**: on both walking meshes, from 1st Avenue's north pavement at the foot of the continued street nearest the player to Uptown Avenue's terminus in the first block; COMPLETE for both.
- **Uptown: traffic into Uptown** (on foot): puts the player on 1st Avenue's north pavement at the foot of the continued street nearest them (the game's cars come out only round the player), then counts the cars whose next lane point is one of the district's for two game minutes. From there the player walks north with the keys, past where the wall stood, into the district.

## Offline checks

NavCheck `UptownCheck.cs`: the band's streets on the game's grid (ids, names, short names, numbers that never meet the game's); the lots (about 430 in the band, 80 to 100 in the first block, Manhattan's mix within 1.5 points, ids, each class's footprint inside its block with its front on the street it names, 2 m between lots, none on a street, every address once, numbers in order, every lot open while the band stands and none while it does not); the band's ground (every lot on it, 1st Avenue between its corners, its buildings and Manhattan never on it) and the way out; the streets built and joined through a model of each of 1st Avenue's junctions and 9th Street's corner as the game baked them (the 119 roads and 67 junctions, the arms, the turns and exits each junction of the game's makes, every lane point named once and on the road grid, the lanes on the band's ground, the same build twice, the stand-down where the game has a road or a junction is not found); the walking ground (every lot's middle, corners and door, each plan one surface, the pedestrians off the carriageways and blocks and over every avenue at every junction, off what stands on the old corners, the seeds on 1st Avenue's own ground, the band's tiles rebuilt whole); the joins (each street's carriageway and pavements and 1st Avenue's two corners overlapping the game's meshes as measured from its data, no gap at a kerb once the agent's margin is kept, the margin kept at the edge, no hole in the band's ground); the modules, walls (closed all round, the eleven opened the game's), colliders, hiding, zones, termini and map; the census's counts; the lots' life at a load (closing tells every handler first, once); the cost; the sources (the layout game-free, the scene touched in the Game files alone, the load's order, every stand-down giving back, the session asked, the contract's line, the director's place, the strays of one scene alone, no LINQ, nothing saved, sent or left to chance); every text in en, pt and zh-cn, and What's new's line in all nine languages. NewGroundCheck holds the shapes the ground is grown with (`GroundShapes`). EconomyCheck's new buildings checks hold its streets to the mod's street rules.

The scans behind its figures (not in the repository, the navlab's facts): the game's walking meshes where the band meets them, the game's walls and colliders over the band in every level the city loads (round colliders too), what the load puts away, and the band as NavCheck lays it (`NavCheck --uptown` writes it) checked against them: no collider of the game's counted on either plan, the things walked round, and what stands on each lot.

## What only the game can tell

The time the walking meshes take to grow over the band on the player's machine; how the copied modules and grounds look at the old dead ends, at 1st Avenue's corners and where they meet the backdrop past the walls; the lights at each junction coming on with their approach; the pedestrians' use of the crosswalks; the game's cars taking the new streets and the cost of 10,000 more lane points to its traffic; the parking lanes filling; the bus lines to the termini; the map's labels and the border's fog over the band; the minimap's picture measured anew.

## The district and its buildings

North of Manhattan, on the ground the lots lay, Uptown is a district of the game with an id of the mod's own, `reallife_neighborhood_uptown`. Most of its lots (351 of the band's 422) stand from the first day as buildings of the game's own, eight of them the offices of the construction game's rival builders; the rest are open land for the construction game, and every building finished there joins the district at the next load. Its buildings take part in the game's own simulation as its districts' do: the game's businesses open there, rents and real estate are priced there, BizMan, the Market Insider and the city map's rows and search know it, the city's people shop and work there, and the player rents and buys there. Everything that names it leaves every save for the mod's own section and comes back at a load ([The district save](district-save.md)), so a save loads without the mod at any time: Uptown and what was in it are then simply gone, and come back with the mod.

### Where the code is

- Folder: [`Scripts/Uptown`](../../Assets/Mods/RealLifeOverhaul/Scripts/Uptown/), the files below (the lots, the ground and the streets are the ground's own files in the same folder).
- Read first: `DistrictValues.cs` and `BuildingsRules.cs` (every rule, game-free, run by EconomyCheck's `UptownLifeChecks.cs`), then `LifeDirector.cs` (the load, step by step).

```
DistrictValues       game-free: Uptown's figures from Midtown's and Hell's Kitchen's, Build 3682's written down, its people's
                     class table and price index as the game makes them, its quad on the city map, its land's price
BuildingsRules       game-free: the lots built from the first day and the open ones, the offices leased to other features,
                     a lot's traffic, the game's structure a lot is built with, what refuses a finished building
LifeStore            game-free: the district's line in the save (the buildings the construction game finished)
LifeText             the locale keys
LifeGame             the one part that touches the game: the district in the game's lookups and tables (.Buildings its
                     buildings in the game's lists, its first life, the generator for new buildings alone; .Leases a leased
                     office's business; .Map the city map; .Tests, .Checks and .Player what the tests and the stand-down
                     read and ask)
LifeDirector         the load, the stand-down, the district save's view of it (.Lots the construction game's side, .Leases
                     the leased offices, .Tests and .Checks the tests)
DistrictBuildings    the construction game's and the landmarks' way in, and DistrictLease, a business leased there
LifeGuide            the tests in the feature's entry (the ground's entry owns the option and the clean-up)
```

### A district of the game

The game keeps its districts in `NeighborhoodHelper` (a private lookup that `GetData` reads and throws without, a list and an array), which it makes again from its data every time a save is read. Uptown's record is a `NeighborhoodData` the mod makes with `ScriptableObject.CreateInstance`, and goes in three steps:

1. **When a save has been read and before its city's scenes are built** (the district save's early step): the record into the lookup, the list and the array; an empty list of its buildings in `BuildingHelper.AllNeighbourhoodBuildings` (the game's daily pass reads one for every district with stats); its row in the people's class table and price index (`CitizenHelper`, only once the game has made them, as its own start throws on a row put there before it) and in the wholesalers' table; and its share of offices and shops the game keeps empty to let, in the two building types' targets (given back as the game had them when the city goes). Every list the game builds as the scenes load then has Uptown as one of its own: BizMan's districts, the Market Insider's, the people's tables.
2. **In the city's load**: its buildings declared to the made buildings' engine as buildings of the game's own (below), made behind the loading screen; then each that stands into the game's list of all buildings and the district's list, a carving box over it on the walking meshes, and the district's quad and row on the city map. This step comes before the district save's, which then puts back what the save kept of them.
3. **In the game's late "game loaded" call**: where the save never had Uptown, its stats (its first days from the save's seed) and the game's own generator for it alone (`CityGenerator.InitializeCity` and `DistributeBuildingsToRivals`, the engine's chance seeded from the save and put back after), as the game sets up a district it adds to an older save; a new city is set up by the game's own generator with the rest. Where the save had Uptown, its buildings new to the save (a lot a newer plan builds, or one that could not stand before) are set up by the same generator for them alone: the district's other buildings are out of the game's list of all buildings for the call (the generator walks that list by district) and the list is put back exactly as it was, so their businesses, tenants and landlords are untouched. Then the first load of each building the construction game finished, and the leased offices' businesses (below).

It lives only where the ground's own verdict says (`GroundSession.Agrees`: the option, and a Going Public session only when every machine runs this mod in this version), in a save of New York's (a city set up whose districts' stats name none of New York's is another city's), and with its buildings: at a load where none stands (the ground not laid, the feature away in another city), it leaves the game's districts again at the city's step, and what names it is kept in the mod's section.

**Its figures** are Midtown's and Hell's Kitchen's, half and half, read from the game's own data at every load (Build 3682's are written down for when they cannot be read, and the census says which changed):

| Figure | Midtown | Hell's Kitchen | Uptown |
| --- | --- | --- | --- |
| working, middle and upper class | 0, 6, 94 % | 6, 82, 12 % | 3, 44, 53 % |
| real estate multiplier | 2.2 | 1.3 | 1.75 (land 35,969.5 a square metre) |
| parking, an hour | $15 | $7.50 | $11.25 |
| customers' weight | 0.9 | 0.7 | 0.8 |
| cars on its streets | 100 % | 90 % | 95 % |
| interior score asked for | 50 | 0 | 25 |
| marketing's strength | 0.5 | 0.7 | 0.6 |
| a business closes under, a day | $850 | $700 | $775 |
| offices and shops kept empty to let | 15 and 6 % | 20 and 10 % | 18 and 8 % |

The base price of a square metre (20,554) and the rent's multiplier (0.00056) are the two's own, and offices' goods sell there. Its people's price index is 1.553, worked out as the game works out every district's. On the city map its quad is a copy of Midtown's laid over its ground, its south edge where Manhattan's zones end (they reach past the old wall), in a green blue no district of the game's has, at the game's own alpha, with Midtown's icon. Its name is its id's text in the locales, as the game names its own districts.

**Its row on the city map** stands with the game's own district rows, under Neighbourhoods, after the game's seven. The game builds its filter list once, when its UI scene first starts (the filters' object, `Canvases/CityMap/MapFilters`, is stored active in that scene, so its `Start` builds the list before any city step of the mod's), and Uptown's record has no zone yet then, so the game makes no row for it. The city's step gives it its zone and makes its row by the game's own private `CreateFilter`, which puts a row made after the list is built at the very end of the list, after the last category's rows (it instantiates its template under the list); so the row is moved up to the end of the districts' own rows, where the game stands each of its own (`LifeGame.Place`). The census and the map row test say where it stands (`LifeGame.RowPlaced`). Ticked, the game's own filters show its zone and outline its buildings (`CityMapFilters.ApplyFilters`, with the map open).

### Its buildings

- **From the first day** (`BuildingsRules.Built`, the owner's "Mostly built"): every lot of the band but those left open and the one kept empty, 351 of 422. Left open: every lot of a block a landmark takes first (the tower's 111, the mall's 113 and the stadium's 110, `DistrictBuildings.KeptOpen`, set by the landmarks at game start whatever their option says), and on every other block its share, a quarter in the first block (where the city's developers build first and the land is nearest Manhattan) and an eighth elsewhere, halves up, at least one a block, picked by their ids' hashes. Always built: the first block's quarter built before the band was filled (21 lots nearest Manhattan, `BuildingsRules.DayOne`, whose records saves hold) and the eight leased offices (below). 44 of the first block's 83 lots and 307 of the other 339 are built; 70 lots are open, 39 of them in the first block (25 of those the landmarks'), at least one on every one of the 37 blocks with lots. The same lots in any order of the plan, on every machine.
- **Kept empty:** a lot something of the game's own stands into (`BuildingsRules.Clear`, from the ground's scan of the game's scene, navlab/facts/band_clear.py): `uptown_1_14_04`, 1002 1st Street, over whose front the crown of a tree on 1st Street's east pavement leans some 2 m from 3.5 m up, where a building stood on the lot's front line would meet it. It is never built, leased or counted among its block's open lots, and it is taken for the construction game and the landmarks (`DistrictBuildings.Taken`), so nothing is ever sold, developed or finished there.
- **A building a save holds** stays: a lot not built by the rule whose registration the district save keeps from an earlier load is built too (`LifeGame.Kept`), so a change of the plan never takes away a building of a save.
- **Each is built** with one of the game's structures for its class's sizes (never one kept for the game's special buildings), picked by its id's hash among them in one order (a leased office among the office ones), in a design the copy engine picks for its size, and with the floor area of the game's building its design copies. Where no design of that size fits the lot, it takes another of its class's sizes (`BuildingsRules.Others`, the copy engine's `MadeSpec.Others`): one of each other size, from its own on in the same order and round again, of its own type where that size takes it (offices alone for a leased office), the first with a design that fits. Measured from the game's city scenes (every design the copy engine would list, against the plan's lots): every size of the small (A, B, C, F), office (D, J, K) and big (L, M, N) lots has a design that fits them, and of the warehouse sizes H and I do but P and Q never do (their smallest that can be copied are 34.9 by 36.6 and 48.9 by 69.5 m against a lot of 29 by 39 m). So the 16 warehouse lots whose hash picks P or Q are built as H, and no lot stands empty for want of a design; a lot whose ground the copy engine refuses still stands down with its line in Player.log.
- **A lot's traffic index:** 38 on an avenue, 22 on a street, 2 less for each 25 m further from Manhattan than the plan's nearest lot (five steps at most), then up to 3 more or less by its id's hash, kept from 10 to 60.
- **The open lots** are for the construction game: its land for sale and the city's developers' sites. A building it finishes stands from the next load (below).
- **Its registrations** are the save's own while the city runs: the one the district save keeps for its address (`DistrictSaveDirector.KeptRegistration`), or a new one in the save's list. Nothing asks the game for them before the district save's city step (`GetBuildingRegistration` would make an empty one and the kept one would be let go).
- **Never claimed in the special buildings' book**, so the game lets them as its own: its daily pass starts businesses there, the rivals own and let them, and BizMan lists them as it lists the game's. The leased offices alone are held off the market.
- **Their look** is the game's own: each copy keeps the game's controller, whose box (the city map's stand-in) every building of the game's scenes stores as a shadow only and the game switches only on the map's toggle; the census judges each against it ([New buildings](new-buildings.md), "The look of a copy").

### The leased offices: the city's rival builders

Eight office lots of the band are leased to businesses other features run there (`BuildingsRules.Leased`): of the office lots outside the first block (whose blocks the landmarks take), one a block at most, in the order of their ids' hashes, as many as the construction game's rival builders. The construction game leases them through the wiring (`DistrictBuildings.Lessee`, whatever any option says, so every machine leases the same): lease `i` is rival `i`'s office.

- **Built as an office:** the leased lot's building takes one of the game's office structures for its sizes (D2, J1 or K1), picked by its id.
- **Its business, the game's own way:** at the game's late "game loaded" call, where the building is new at this load or stands empty to let and nobody's, the game's own start of a business (`CompetitionHelper.StartNewCompetitorBusiness`, as its daily pass opens one, the engine's chance seeded from the save and put back after) opens a business of the construction company's type there, with the hours, layout and sign of one of the game's own graphic design studios (picked by the lease's index among those whose layout the game has for the building's structure). Then the lessee's own: the rival's name (the same in every language), BizMan's description, no rival of the game's as its owner (the card and BizMan then show neither, as the game does for such a business), and seven days of income (its crews times $1,200 a day) for the game's valuation, which never closes it for low income while it has fewer than fourteen. Its building's landlord stays the game's.
- **Laid out as the game's studios:** each of the game's layout sets of the graphic design studio (A3, C1, D2, J1, K1) is filed under the construction company's type as well, in the game's own cache of them (`BusinessLayoutSetHelper.GetAllBusinessLayoutSets`), so the game furnishes the office when the player goes in, staffs it and values it as one of its own. The game makes no business of that type itself (it has no AI business of it), so its own picks are as before; a set the game does not find again is taken out and the office not set up.
- **Kept:** its registration is the save's, kept by the district save like every other of Uptown's; at each load the business is there already, or moves back in where the unit stands empty. It is known by its business type at its own lot, never by its name, which is text in the player's language (`BuildingsRules.LeaseOf`, `LeasedBy`): a name or description that is no longer the lease's (a text changed since it moved in) is put back, its sign drawn again and the game's registration event raised, and the load's line says how many were renamed.
- **Held off the market** in the special buildings' book (refused for rent, sale and takeover in the game's pages while Uptown lives), its own sign kept. Never the player's unit, nor another player's in a Going Public session.
- **On the map and in BizMan:** the map's row for the construction company's type (its "active businesses" row) outlines the eight; BizMan presents each as a business of the game's.
- **In the Construction app:** the Rivals tab gives each rival's office address, and its button makes it the game's destination (`DistrictBuildings.LeaseStands`, `LeaseDestination`).

### The mod's tables by district

| Table | Uptown | Why |
| --- | --- | --- |
| Precincts | left out | six stations in New York's districts; its streets are served by the nearest |
| Hotels | left out | every hotel slot is in one of New York's districts |
| Broadband (the player's own network) | left out | the network is built district by district over New York's seven |
| Internet | takes part | wireless and copper reach every district, so they reach its businesses |
| Civic wards | takes part | an eighth ward after the seven, whose saved indices stay |
| The city economy | takes part | its income starts from its own class shares (1,176, between its neighbours') |
| Brands | takes part | its units are sites like any district's; in the brands' order it comes after the game's |
| Flare | takes part | a home district of the city's people after the seven; another city's Flare keeps the seven |
| The district view | takes part | its crimes are counted by its id |
| Online | takes part | after the districts known, whose orders keep their indices |
| Faults, Hiring, Estate | take part | they read the game's own id on its buildings and registrations |
| Fame | takes part | its people's opinion of the player, by its id |
| Social | takes part | a person's home district keeps its name, as Uptown comes after the game's |

### Saves

Every record that names its district, its streets or its buildings leaves the game's part of every save and comes back right after: its registrations with everything in them, its stats, its market events, its products' demand, a ticked row of the city map's filters, and every record that names one of its addresses; the player inside one of its buildings is written outside its door, the player and the cars on its ground at the way out on 1st Avenue, a car inside one parked in Midtown. The district's own line (`reallife:uptown`) holds the buildings the construction game finished, and the save tools leave it with the district's records.

### Going Public

Uptown is the same on every machine with the same mod: its figures, its lots, its first buildings, their structures and traffic, its leased offices and their businesses, and its first life are worked out from fixed data and the save's seed alone, and nothing is sent. It lives only in a session the ground's verdict agrees to. When a session stops being one, it stands down for the rest of the session, and the player is told. A building of another player's is never set up, and a finished building is the city's in a session, never a deed made on one machine.

### Standing down

At a load with no building of it, when the ground's feature cleans up (another city, its faults, the save tools), or when a session's verdict turns: the player is taken out of a building of it by the game's own way out; then every record that names it leaves the save in memory for the rest of the city, kept in the mod's section (`DistrictSaveDirector.KeepOut`); its buildings leave the game's lists and are hidden, never destroyed; the district leaves the game's list and array of districts and the city map. Its record stays in the game's lookup for anything that still holds its id. It comes back at the next load where it may live.

### For the construction game

```csharp
if (DistrictBuildings.Lives && !DistrictBuildings.Taken(lotId))   // UptownLots.Get(lotId) for its place and its sizes
{
    int traffic = DistrictBuildings.TrafficOf(lotId);              // what its building's traffic index will be, 10 to 60
    float land = DistrictBuildings.LandPricePerSqm;                // the district's land, a square metre
    bool raised = DistrictBuildings.Finish(lotId, design, size, version, type, storeys, FinishOwner.Player, price, out string why);
}
```

- `Lives`: Uptown is a district of the game with its buildings standing at this load. `Stamp` changes whenever a lot's state does, so a look once a second is one comparison.
- `Taken(lotId)`: a building stands there, from the first day or finished, or one finished there waits for the next load: not for sale. `Finished(lotId)`: a district building stands there at this load, so a look of the construction game's own is not wanted.
- `Finish(...)`: from the next load the building stands there as one of the district's, in the design named (a `DesignEntry`'s name, or null for the copy engine's pick, the same on every machine), with the game's size, version and type, and `storeys` of floor area (1 to 12; 0 for the design's own). `FinishOwner.Player`: at its first load the game's own deed is written for the player as BizMan's purchase writes one, with `price`; `FinishOwner.City`: let to the city's businesses. Refused, with why: Uptown not living, a lot unknown or taken, the game's special type, a size the lot's class does not take, a structure the game lacks, storeys outside 0 to 12, a player's deed in a Going Public session, a price that is no sum of money.
- `Lessee`: set by the construction game's wiring, a `DistrictLease` for each leased office (the business type, the type it borrows layouts, hours and sign from, its name, BizMan's description key and its daily income). `LeasedLot(i)`: the lot of lease `i`, fixed data. `LeaseStands(i, out address)` and `LeaseDestination(i)`: whether its business stands at this load, its address, and the game's destination set at its door.

### For the landmarks

Uptown's landmarks ([Uptown's landmarks](landmarks.md)) stand on whole blocks of the first block:

- `DistrictBuildings.FinishOver(lotId, over, design, size, version, type, storeys, owner, price, out why)`: a building finished on its own lot and standing over `over`, other empty lots of the same block, too: its address its own lot's, its ground the rectangle round all of them (`BuildingsRules.SpanGround`), its size any of the game's (its design stands only on ground it fits). Refused, with why, for a lot not in the plan, in another block, taken or named twice, or more than 16 lots (`BuildingsRules.SpanRefusal`). Every lot is `Taken` from then on, and `Finished` while it stands. Uptown's line keeps the other lots as an eleventh field of the record (`FinishedBuilding.Over`); a record without it reads as before.
- `DistrictBuildings.HeldFor`: set at game start by the landmarks; `Taken` is true for a lot a landmark holds as well, so nothing else is put on it. `Occupied(lotId)` is `Taken` without the held lots, what the landmarks ask of their own.
- `DistrictBuildings.FirstDay(lotId)`: the plan's lots built from the first day, fixed data known before the city loads (worked out again if `KeptOpen` changes).
- `DistrictBuildings.KeptOpen`: set at game start by the landmarks (`LandmarkRules.OwnBlock`, each landmark's own block), whatever their option says: those blocks stay open from the first day but for the old quarter's lots, so each site is its block's.
- `DistrictBuildings.TrafficLift`: set at game start by the landmarks, from their own line of the save; `TrafficOf` adds it to a lot's own index (`BuildingsRules.Lifted`: never under the game's least, 80 at most), and the district's buildings are declared with it. `LiftsChanged()` moves `Stamp` when it changes.
- `DistrictBuildings.Standing(list)`: the district's buildings standing at this load; `Withdraw(lotId, out why)`: a building finished but not yet stood at any load taken back (the tests' way back).

### Tests

- Offline: EconomyCheck's `UptownLifeChecks.cs` (its figures, the price index and class order, its quad, the first day's buildings and their traffic in any order of the lots, the structures, every refusal, its line's round trip and bounds, the district save's view, the city check with it, every table above with it and without, and the sources: only its game files write the game's tables of districts, the load's order, the stand-down's order, nothing left to chance, nothing a frame) and `UptownFillChecks.cs` (`--uptown-fill` runs both and the landmarks' alone; on the real plan of 422 lots: the built and open lots and every block's share, the old quarter and the leases always built, the landmarks' blocks open and each landmark's site on its own block with none of its lots built, the developers' open lots, the same lots and leases in any order, the leases' lots and number against the rivals', a lease's business known by its type whatever its name, the office structure, the other structures a building takes where no design of its size fits (one of each other size from its own on, its type kept, offices alone for a lease, every size of its class reached for each of the 351; the lot the tree leans over neither built nor leased, taken, and never counted open), the lanes' squares against a whole pass and their time, the boxes tried by their reach against the turned test of every box and a separate test of the four axes, and their time, the fill's whole load work without the game's code timed; the sources: the load's declarations and kept lots, the generator for new buildings alone with the list put back, the leases by the game's own start of a business, seeded, never a player's, the layouts filed and checked, the map row placed, the look judged, the ground check's boxes by their reach, the other structures tried only for want of a design and the record made with the one taken, the tests in the run with their lines, the wiring; and the texts and What's new in all nine languages); ContractCheck (every member of the game's read by name, and the save's fields that can name a district).
- In the game, Options > Tests, each with one "Uptown check (...)" line in Player.log:
  - "Uptown: district census": the district found by the game's own `GetData`, in its list, its zone and its filter row (and that row among the game's district rows), its buildings built from the first day, kept and finished, its open lots, its leased offices with their businesses, every building looking as the game's own, its buildings in the game's lists and their registrations in the save's.
  - "Uptown: a business opens there": the game's own daily pass for a new business, run for Uptown alone.
  - "Uptown: rent a unit there": the nearest unit to let made the destination; renting it in BizMan is the player's, with the game's own button.
  - "Uptown: save and read back": a recovery save written and read back by the game's own reader, gone through for anything of Uptown, the save in memory as before; the save is kept, to load with the mod switched off.
  - "Uptown: the city's builders' offices" (in the test run): each leased office's business there, laid out (the game finds its layout), staffed and valued (the game's own valuation reads it) as the game's office businesses are, on the map's row of its type and held off the market; the nearest made the destination.
  - "Uptown: its row on the city map" (in the test run): its row among the game's district rows, named in the player's language, its zone among the game's zones and the row's data naming the district.
  - "Uptown: frame cost of its buildings" (in the test run, at an open lot of the first block): the frame measured for 4 s with the district's buildings, then 4 s with their looks hidden; what they add a building in view, against the copy engine's allowance for a copy in view (0.05 ms).

### Cost

- **The load:** the made buildings' engine makes all of the district's at once behind the loading screen (its line says what measuring and making cost, and each building's average; with Developer diagnostics on, `Made buildings check (load time)` wants under 5 ms each). Its ground check sorts the traffic lanes' 20,000 points into 50 m squares once a load, so a lot looks at the points near it (360 lots' checks under 20 ms offline, where a whole pass each was some 7 million point tests), and tries the game's boxes (868 buildings and at most 2,065 fixed obstacles in the game's levels) by the lot's reach before the turned test (352 lots over 2,934 boxes in 1.6 ms offline against 15.5 ms). Unity's own copying, measured in a built player of the game's Unity with copies as heavy as the game's copyable buildings: 0.12 ms a copy at the median weight, 0.17 ms at the 90th percentile, so some 40 to 60 ms for the band's 351; what the game's behaviours do as a copy wakes comes on top and is only in the load's line. All of the fill's own work that runs no game code, timed offline on the real plan as a load does it (an offline check, wanted under 30 ms): which lots are built and leased with each one's structures, 0.8 ms; the lanes' 20,176 points into squares, 1.9 ms; the 351 buildings' lane and box checks, 1.8 ms; 4.6 ms in all. On 23 Oct the first block's 22 made buildings took 277 ms, 150 ms of it the load's three searches of the scene, which are done once whatever the count.
- **The walking meshes:** each standing building carves its footprint (a stationary `NavMeshObstacle`). Measured in a built player of the game's Unity on two walking meshes of the game's settings over the whole band: all 352 footprints cut on both meshes within the first frame, in 2.5 ms; a parked car beside them, which the game's cars carve, re-cut in 0.1 ms. So carving stays.
- **A frame:** a copy draws what the game's building of its design draws (the same renderers, materials and roof props' LOD groups; never a building the game batches statically); the frame cost test measures the district's in view.
- **Nothing a frame** of its own: the director's one time comparison.
