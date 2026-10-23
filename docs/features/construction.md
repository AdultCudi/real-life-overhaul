# Construction

## In a sentence

Buildings going up in the city as the game itself would show them: a whole building wrapped to its roof in the game's own scaffolding and fabric while it is rebuilt, and on ground given to it a new building's hoarding, dig, slab, tower crane, frame storeys, cladding and wrap, stage by stage over game days.

## Where the code is

- Folder: [`Scripts/Civic`](../../Assets/Mods/RealLifeOverhaul/Scripts/Civic/), as two kinds of site of the construction programme ([Civic life](civic.md#construction-sites)): `SiteKind.Rebuild` and `SiteKind.Plot`.
- Read first: `WrapRules.cs` and `PlotRules.cs` (game-free), then `SiteWrap.cs`.

```
WrapRules            the game's scaffold round a building: its shell's sides, runs clear of doors and of what stands in the way,
                     the 5.17 m pitch to the roof, fabric from the second lift up, the stages, the pace (game-free; NavCheck WrapCheck)
BuildingShell        a building's solid boxes from its own colliders, in its own axes (no physics query; a faded building reads the same)
SiteKit              the game's own scaffold modules and fabric copied from a building that wears them (unbatched, scripts taken
                     off), and its concrete, metal and fence wire by name: once a city, behind the loading screen
SiteWrap             one wrap: laid out, set out and taken down a few pieces a frame out of view, every renderer in the building's
                     fade, hide and city map lists, a holder whose game extension names the building
PlotRules            a new building on given ground: its record ("plot:24x20:090:5" in the site's street field), the frame, the
                     slab, the cladding, the yard's crane, cabin, toilet and stacks, the fence and the gate (game-free)
PlotMeshes           a stage's boxes as one mesh in two materials, and the fence; CraneSlew: the jib's legacy Animation clip
SiteDresser.Rebuild  the rebuild's hoarding, wrap stage and crew; SiteDresser.Plot: a plot's stages and the camera hiding its frame
SiteDresser.Wraps    what is in view for a site that fills a building, the wraps stepped each frame while they change, the tests' wrap and crane
CivicDirector.Construction   the five test buttons
```

## What players see

**A rebuild** (one at a time in the city, about 160 working hours, drawn by the programme like any site; never the player's own building, never one of the mod's own places, none a DLC keeps):

1. Hoarding along the door's wall, the notice board, two people at it.
2. The game's scaffold going up every street side, half its lifts, no fabric; one of the crew on the first deck.
3. Wrapped to the roof, the fabric hung from the second lift up; people on the deck at work and a painter; the skip at the kerb.
4. The inspection: an inspector with a clipboard at the notice board, one person on the deck.
5. The scaffold coming down to half, no fabric; then the site is handed over and everything goes once nobody is looking.

The building's look itself is not changed: swapping it for another design at the end comes with the copy engine of the new buildings (feature/new-buildings) after both are merged.

**A new plot** (on ground given to it; about 320 working hours):

1. Hoarding along the front with a 6 m gate at the yard, the fence wire round the rest, the site cabin and a toilet in the yard.
2. The dig: the frame's footprint broken up, spoil heaped round it; two people digging.
3. The ground slab with its starter bars, the tower crane on its pad in the yard with its jib slewing, material stacks.
4. The frame rising a storey at a time (3.6 m a storey, columns on a grid of spans up to 6 m, a slab each, edge protection round the top).
5. Every storey but the top clad in metal panels, and the frame wrapped in the game's scaffolding, fabric on.

The reveal of the finished building comes with the copy engine, as for a rebuild. Whatever of a plot stands between the camera and the player is hidden the way the game hides its own buildings (their shadows kept).

## The game's scaffolding

- **What it is:** the Buildings scene's `SM_Scaffolding_8m` (6.0 m), `_4m` and `_Corner_4m` (3.0 m) modules, 3.04 m deep, and `SM_Scaffolding_LargeFabric` and `_SmallFabric`, stacked at 5.17 m on 26 of the game's buildings.
- **Layout:** one run along each street side (pavement under the whole depth, no other building in it), a corner module at each end (the far one turned a quarter, as the game's), 6 m modules between and one 3 m where the count is odd, slid along the slack until no post stands within 0.95 m of a door's middle, else split round the door. A tree, a sign or a lamp over a stretch splits the run; a bin or hydrant where a post would stand keeps the posts off it. Lifts: the top deck within half a lift of 0.6 m under the roof, at most ten, at most 360 pieces.
- **Fade:** the pieces' renderers join the building's `renderersToFade` and `renderersToHide` and its private set the city map swaps for its box (`_lodRenderers`), and leave them before a piece is destroyed; the pieces hang under a holder whose `ViewBlockingEntityExtension` names the building (set before the holder wakes), so the camera's ray meeting the scaffold fades the building with it, as the game's own scaffold does. Two lines of `Core/GameContract.cs` (part `site_wrap_fade`).
- **Pace:** nothing is set out or taken down while the wrap's box is in the camera's view (the test buttons excepted), never while the city map shows, six pieces a frame (twelve to take down, sixty behind the game's black screen).
- **Pieces:** copies of a building's own scaffold made once a city; where every building's is static-batched they are made from the game's meshes and materials found by name instead.

## Props and clips

The building sites' props are in a second props bundle, `Bundles/reallife_props_site` (PropAssets/README.md, "The second bundle"): `tower_crane_mast`, `tower_crane_jib` (turning about its pivot, the slewing ring, by Unity's Animation), `site_cabin`, `site_toilet`, `site_stack_blocks`, `site_stack_timber`, `site_stack_rebar`, `site_earthworks` and the `hammer`. Each is drawn as a plain stand-in of the same size (`CivicProps.CivicStandIns`) while the bundle is not installed. The clips `hammer_nail` and `rake_asphalt` (CityAssets, `clips_build.py`) are played when the anim bundle has them, `kneel_work` and `shovel_dig` otherwise.

## Going Public

The new kinds are values of the site's existing kind field, and a plot's ground is text in its existing street field: the save's line and the host's line of sites keep their shape and version, and nothing new is sent. The host draws the programme as before (seeded, the same on every machine); a guest follows it. The wrap and the plot's meshes are drawn by each machine from the same site records. The test buttons that change sites are the host's.

## Self-check lines (Developer diagnostics)

- `Construction check (wrap): N modules, N lifts, linked to the building's fade and its city map look: PASS` when a wrap has every piece it should.
- `Construction check (wrap down): every piece taken down, and the building's fade lists hold none of them: PASS` when it is gone.
- `Construction check (crane): mast ... slewing by the engine's own animation (100 s a round): PASS` for the test crane.
- `Construction: the game's scaffold pieces copied from the game's own scaffolding; concrete ..., metal ..., fence wire ...` once a city.

## Tests (Options, the civic section's tests)

- **Construction: wrap the nearest building in the game's scaffolding**: the nearest building with a door within 70 m, to its roof, fabric on, a few pieces a frame in view.
- **Construction: a rebuild site at the nearest building**: a rebuild site set out now.
- **Construction: advance the nearest site one stage**: the nearest site within 80 m moves to its next stage (a plot in its frame stage: a storey); at the last, it is handed over.
- **Construction: raise a frame storey here**: a storey more on the nearest plot within 60 m; with none, a plot on 24 by 20 m of ground ahead of the player (no building on it), at its first storey.
- **Construction: stand the tower crane here**: the crane alone, 12 m ahead, slewing.
