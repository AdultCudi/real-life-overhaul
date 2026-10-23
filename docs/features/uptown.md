# Uptown

## In a sentence

Uptown is a district of the mod's own north of Manhattan: at every load Manhattan's streets go on north past 1st Avenue, through 1st Avenue's own junctions, to a new avenue, over ground both walking meshes grow over from Manhattan's pavements, with the game's own road modules, lamps, lights, crosswalks, parking, people and a bus terminus, and lots for the district's buildings; nothing of it is saved, so a save loads without it.

## What a player sees

- **At the wall.** The old dead ends north of 1st Avenue are gone: their cones, barriers and closed signs, the low backdrop behind them and the invisible wall across each street. Standing on 1st Avenue's north pavement, the player looks up 9th to 1st Street as they go on north, between the backs of 1st Avenue's buildings, to Uptown Avenue 147 m on. 1st Avenue's own buildings, doors and pavements are the game's and stay as they were.
- **Walking in.** The keys walk the player up any of the nine streets from 1st Avenue's corner, across where the wall stood, on the pavements or the carriageway, and onto the first block's lots and Uptown Avenue. Pedestrians come up the east pavements and stand and walk along Uptown Avenue.
- **Driving in.** Cars of the game's traffic turn into eight of the streets at 1st Avenue's lights (5th Street, which 1st Avenue's traffic passes without a junction there, has no lanes, as at 1st Avenue) and meet Uptown Avenue at a junction with lights at each street. Parked cars line the streets' west kerbs and both of the avenue's.
- **The first block.** Eight blocks between 9th and 1st Street, from the backs of 1st Avenue's buildings to Uptown Avenue: 83 lots on the city's own block grounds, open for the district's buildings. The district's edge is walled as the city's own edge is: north of Uptown Avenue's north pavement, west of 9th Street and east of 1st Street.
- **On the map.** The city map pans over it, its streets named; the minimap draws its streets. A bus line ends at Uptown's terminus on Uptown Avenue, between 5th and 4th Street.

## Where the code is

- Folder: [`Scripts/Uptown`](../../Assets/Mods/RealLifeOverhaul/Scripts/Uptown/).
- Read first: `LayoutRules.cs` (the band and its lots), `LayoutPlans.cs` (what the load lays), `UptownLots.cs` (the lots for other features), `GroundDirector.cs` (the load, step by step).

```
LayoutStreets        game-free: the band's 22 streets, their ids, names, short names and first numbers
LayoutRules          game-free: the grid on the game's own, the lots of the band and of the first block, their ground
LayoutPlans          game-free: the first block's lanes, walking ground, modules, walls, colliders, hiding, zones, terminus
UptownLots           the lots for the district's features and the builders: UptownLots, Lot, LotClass, GroundRect, UptownExit
GroundSession        whether the district may stand at this load: its option and the Going Public session
GroundDirector       the load, the stand-downs, the late parking and people, the clean-up
  .Tests             the three test entries
GroundGame           the Game file: the backdrop, the walls, the colliders, the map's bounds
  .Art               the game's road modules copied: names, lights, crosswalks, signs; parking lanes and zones
GroundGuide          the feature's entry: its option, its tests, its clean-up
GroundText           the locale keys
```

One line each elsewhere: `Core/Settings.cs` (`uptown`), `Meta/OptionsTree.cs` (its section in the City group), `Meta/FeatureCities.cs` (New York's), `Core/RealLifeOverhaulMod.cs` (the director, before the new buildings' in the city's load), `Core/GameContract.cs` (part `uptown_labels`: `Road.streetName`, optional), `Core/GoingPublic.cs` (the session's mod check), `NewBuildings/NewBuildingsRules.cs` (Uptown's streets among the mod's own) and `Workshop/locales.py` (the street names, fixed words in every language).

## How it talks to other features

- **New ground** ([New ground](new-ground.md)) lays it: `GroundWork.Streets`, `GroundWork.Ground` (both meshes), `GroundWork.TakeBack`, `RoadsGame.LampOf` and `RedLamp` for the lights, `ParkingGame` and `PeopleGame` for the parking and the people.
- **The buses** (`TransitMap.AddTerminus`): Uptown's terminus is one end of the network, laid behind the loading screen.
- **New buildings** ([New buildings](new-buildings.md)): Uptown's 22 streets are among the mod's own (`MadeStreets.All`), so a building on its lots has an address the game writes and finds; the lots are open before the city's buildings are made at the load.
- **The district's life and buildings** and **the builders** read the lots (below). The district save is told the district's ground and its way out by the district's own feature, from `UptownLots.Ground` and `UptownLots.Exit`.

## The layout

All of it is fixed data (`LayoutRules`, `LayoutStreets`), the same on every machine and in every version, measured from the game's scene at Manhattan's north edge (Build 3682) and checked against the game at every load.

- **The grid** is the game's own: Manhattan's north to south streets every 87.3 m (a street's 100 m module, a crossing's 37.3 m), 9th Street's middle at x -301.67; its west to east avenues every 147.2 m, 1st Avenue's middle at z 464.58. A street is 17.3 m wide kerb to kerb with its pavements, an avenue 27.3 m.
- **The band** is three rows of blocks north of 1st Avenue, 18 columns from five streets west of 9th Street to five east of 1st Street: nineteen streets (Manhattan's nine continued, Willow, Birch, Elm, Maple and Cedar Street west of them, Hill, Ridge, Grove, Park and Lake Street east of them) and three avenues (Uptown, Summit and Crest Avenue). Seventeen of its blocks are squares with no lots.
- **The first block** is row 1 between 9th and 1st Street: eight blocks, laid first and the only one laid now.
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
IReadOnlyList<Lot> first = UptownLots.FirstBlock; // the first block's 83
Lot lot = UptownLots.Get("uptown_1_09_04");      // by its id, or null

if (UptownLots.Open(lot))                         // laid and walkable at this load: Ready, and in the first block
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
| `Lot.Row`, `Column`, `Block`, `First` | Its block: row 1 to 3 north of 1st Avenue, column 1 to 18 west to east (the block east of 9th Street is 6). |
| `Lot.Street`, `Number`, `Order` | Its address, and its place along its side of the street. |
| `Lot.X`, `Z`, `Yaw`, `Facing` | Its middle and its turn: `Yaw` as `CivicWorks.StartPlot`, `Facing = (Yaw + 180) % 360` as NewBuildings' `MadeLot`. 0 or 180 faces an avenue, 90 or 270 a street. |
| `Lot.Width`, `Depth`, `Class`, `Sizes` | Its footprint in whole metres, its class, the game's building sizes of its class. |
| `UptownLots.Ready`, `Why` | The district stands at this load; else why not, in English for the log. |
| `UptownLots.Open(lot)` | `Ready` and the lot is in the first block. |
| `UptownLots.Closed`, `Closing` | The district was closed by its feature's clean-up and stays closed until the next load; `Closing(why)` comes first. |
| `UptownLots.Ground`, `Exit` | The district's ground as laid now (18 rectangles: the eight blocks from their backs, the nine streets past the old wall, Uptown Avenue's band) and its way out for the player and the cars: 1st Avenue's north pavement at 5th Street (91.18, 0, 473.33), heading 270, in Midtown. |

`Ready` is set by the ground's step in the city's load, behind the loading screen, before the city's buildings are made: a building of the mod's own that declares itself with `Wanted = () => UptownLots.Ready` stands on a lot laid at that very load. It is false again when the city goes. A handler on `Closing` subscribes once a city and leaves when the city goes.

## At every load

In the city's load (OnLoadAsync), behind the loading screen, before the traffic starts, in the director's `Awake`; the whole first block or nothing:

1. **Asked.** Its option (`uptown`, on by default, read only at a load), New York (the city check leaves the director out in another city) and the session (below).
2. **Found.** Everything of the game's it is laid against, before anything changes: the backdrop round the city (`MapBorder`) and its invisible walls, the city map's camera, and the road modules to copy: 9th Street's 100 m south of 1st Avenue, 1st Avenue's 50 m east of 9th Street, and each street's own crossing at 1st Avenue (by name, place and the size of its mesh, so a module the game batched with others is never copied). One missing, and nothing is laid.
3. **The backdrop put away** where the district stands: every piece of it (a renderer, a collider or a level of detail) whose middle lies at an old street end or on the first block, about 300 of them; its block grounds stay, the lots' ground. 1st Avenue's own buildings are not the backdrop's and are never touched. The game's nine walls across the street ends are switched off with it: the ground's check (step 5) counts every collider of the game's left on the district's ground.
4. **The streets** (`GroundWork.Streets`): each continued street's two lanes from 1st Avenue's junction, through it (its stop points and exits given to the game's junction, which lights the new approach from its own pole that faced the dead end), to Uptown Avenue; Uptown Avenue's four lanes between the eight junctions with lights, its two ways on one green.
5. **The walking ground** (`GroundWork.Ground`): the player's over every carriageway from the old dead end, the pavements, the lots and Uptown Avenue; then the pedestrians' over the pavements, a strip up each east pavement from 1st Avenue's corner, and the crosswalks over the avenue. Both join Manhattan's pavements across the old wall.
6. **The edge.** Three walls of the game's height round the first block; a ground collider under it all (the city's own ground ends at z 606, short of Uptown Avenue) and one under each carriageway. The outermost pavements keep 0.5 m off the edge, where the backdrop's buildings east of 1st Street stand onto its pavement.
7. **The art.** The game's modules copied along the streets, made asleep and stripped before they wake: each street's 100 m named on the map with its own name; each crossing's light poles hung under the lamps its junction switches, pole by pole to the approach whose light it is, the poles no approach comes to dark as at the game's own T junctions; its crosswalks waiting for the red of the road they cross, those onto no walking ground taken away; the avenue's sign, which names 1st Avenue, taken down (a crossing copied from another street's takes its street sign down too).
8. **The map** pans over the district, to z 700; **the bus terminus** is added before the buses' routes are laid.
9. **The lots open** (`UptownLots.Laid`).
10. **Once the game is loaded** (its late call, the loading screen still up): a parking lane along each copied street's west kerb and both of the avenue's kerbs, each a copy of the template module's own (new ground's `ParkingGame`), and seventeen pedestrians' zones on the pavements.

### The game's ground checked

Each step that meets something of the game's where the district goes stands down with one log line, `Uptown: not laid at this load: ...`, and everything laid before it is given back the same load: the backdrop shown again, the old walls switched on again, the streets taken back off the traffic's data (`GroundWork.TakeBack`), the mod's own pieces taken away. So the district never covers the game's content:

- **Streets:** a lane of the game's within 6 m of a new one, the joins apart; 1st Avenue's junction not found at a street end.
- **Ground:** any collider of the game's on the walking ground, where the plan was made with none (a free update that builds north of 1st Avenue).
- **Art:** a module to copy missing, moved, resized or batched.

The pedestrians' ground failing alone leaves the district standing without pedestrians.

## Going Public

The district's streets, ground and lots must be on every machine of a session or on none, so it stands in a session only where every machine is known to run this mod in this version (`GroundSession.Agrees`): on a host whose Going Public refuses a player whose mods differ from its own (`GoingPublic.RefusesOtherMods`), and on a guest whose mods Going Public did not find to differ from the host's (`GoingPublic.ModsAsHost`). Elsewhere in a session it is not laid, with one line. Nothing of it is sent; the lanes and their order are the same on every machine, so the traffic the host runs is the guests' too. A guest whose mods match a host that lets in other mods lays it alone: the host's traffic never drives it there.

## Switching off and the clean-up

- **The option** is read at a load: off, nothing of the district is laid at the next load, and the game is as without it. The option's row says it applies at the next load.
- **The feature's clean-up** (the options page's, five faults in a row, the save tools' removal, another city) closes the district until the next load: `UptownLots.Closing` first, then every lot closed; its parking lanes and pedestrians' zones give their cars and people back to the game's pools. Its ground, art and walls stay until the city goes: the traffic, once started, keeps its streets.
- **When the city goes** the mod's own pieces go (the copied modules, the walls and colliders, the parking, the zones), the map's bounds are put back, and the game's own pieces go with their scene. The save tools stopping the mod in a city that stays leave what stands until it goes.

## Cost

The director is disabled while no test runs: no frame cost. The load: two searches of the scene for the road modules (behind the loading screen), the backdrop's pieces walked once, the streets' build (under 100 ms offline, a timing check), the walking ground's tiles grown behind the loading screen (their time is in the load's line: it is not known until the game builds them), 26 modules copied. The traffic test looks four times a second for two game minutes.

## Self-check lines

In Player.log, the first two always, the rest with Developer diagnostics on:

- `Uptown: laid behind the loading screen in N ms: N lane points (N in junctions, N where cars may start) on N road(s), 8 junction(s) (8 with lights), N join(s) to the game's lanes, 8 stop point(s) and 8 exit(s) given to the game's junctions; the player's walking ground grown in N ms, the pedestrians' in N ms; N pieces of the backdrop put away, 9 of the game's walls opened, 14 walls and colliders built, 26 road modules copied; 83 lots open.`, or `Uptown: not laid at this load: why.`
- `Uptown: once the game was loaded, 25 parking lanes along its kerbs and 17 pedestrians' zones.`
- `Uptown check (streets): Roads check (append): traffic started with ... marked by their junctions: PASS`, in the game's late call.
- `Uptown check (census): ...: PASS`, `Uptown check (path, Player): ... COMPLETE: PASS`, `Uptown check (path, NPC): ...`, `Uptown check (traffic): cars that drove its streets: N in 2 game minutes (...): PASS`, from the tests.

## Tests

Under Options > Tests (each says why in a notice when it cannot run; none changes the city, so each runs in a Going Public session too, and each works from wherever the player is):

- **Uptown: census**: whether the first block stands and how it was laid, piece by piece, against what the plans lay: streets, junctions, walking ground, the backdrop, walls, modules, lights, crosswalks, signs, parking, zones, the map's bounds, the lots, and whether the player stands on the district's ground.
- **Uptown: a path from Manhattan into Uptown**: on both walking meshes, from 1st Avenue's north pavement at the foot of the continued street nearest the player to the first block's middle on Uptown Avenue; COMPLETE for both.
- **Uptown: traffic into Uptown** (on foot): puts the player on 1st Avenue's north pavement at the foot of the continued street nearest them (the game's cars come out only round the player), then counts the cars whose next lane point is one of the district's for two game minutes. From there the player walks north with the keys, past where the wall stood, into the district.

## Offline checks

NavCheck `UptownCheck.cs`: the band's streets on the game's grid (ids, names, short names, numbers that never meet the game's); the lots (about 430 in the band, 80 to 100 in the first block, Manhattan's mix within 1.5 points, ids, each class's footprint inside its block with its front on the street it names, 2 m between lots, none on a street, every address once, numbers in order, only the first block open and only while it stands); the ground and the way out (1st Avenue's north side and its doors left to the game); the streets built and joined through a model of each of 1st Avenue's junctions as the game baked them (the turns and the exits each makes, every lane point on the road grid, the same build twice, the stand-down where the game has a road, no junction found); the walking ground under every lot's middle and corners and at its door, joined to Manhattan's, the measured colliders of the game's under no box; the modules, walls, colliders, hiding, zones, terminus and map; the census's counts; the lots' life at a load (closing tells every handler first, once); the cost; the sources (the layout game-free, the scene touched in the Game files alone, the load's order, every stand-down giving back, the session asked, the contract's line, the director's place, no LINQ, nothing saved, sent or left to chance); every text in en, pt and zh-cn, and What's new's line in all nine languages. EconomyCheck's new buildings checks hold its streets to the mod's street rules.

## What only the game can tell

The time the walking meshes take to grow over the first block; how the copied modules look at the old dead ends and where they meet the backdrop north of Uptown Avenue; the lights at each junction coming on with their approach; the pedestrians' use of the crosswalks; the game's cars taking the new streets; the parking lanes filling; the bus line to the terminus; the map's labels.
