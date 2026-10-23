# Uptown's landmarks

## In a sentence

Three megaprojects of the city's on blocks of their own in Uptown's first block, Summit Tower, Uptown Galleria and Crest Field: each held from the first load, announced in the City Ledger on its day, put out to tender in the construction game (the player's company bids against the city's eight builders) or built by the city's own works, set out as a site of the city's construction programme, handed over and opened with a ceremony; afterwards the tower's offices and the galleria's shops are let as the district's own, the lots round them get busier, and Crest Field has Uptown FC's home matches.

## Where the code is

- Folder: [`Scripts/Landmarks`](../../Assets/Mods/RealLifeOverhaul/Scripts/Landmarks/) (namespace `RealLifeOverhaul.Landmarks`).
- Read first: `LandmarkRules.cs` (every rule, game-free), then `LandmarksDirector.cs` and `LandmarksDirector.Works.cs`.

```
LandmarkRules        game-free: each landmark's blocks and site, the works' plot, the timetable and the pace, the phases,
                     the lift of the lots round them, the season (fixtures, scores, crowds), the stadium's pieces, the
                     crowds' squares, the plaza, the jobs
LandmarkStore        game-free: the line in the save (reallife:landmarks): each landmark's stage, block, tender, builder,
                     timetable, works site, opening and design
LandmarkBridges      what it asks of the construction game, the city's programme and Standing; filled by Wiring.LandmarkLinks
LandmarksGame        the one file that touches the game's scene: the clock, taking the player there, the pieces stood with
                     their boxes and cuts, the camera's view, the crowds' squares, a district building's traffic index
LandmarksDirector    the state and its timing (a second's look, the hour's and the day's passes), the save, the district
                     save's view of it, the lift
  .Works             the sites, the announcement, the tender or the city's own works, the timetable, the handover to the
                     district, the design, what the programme and the construction game ask of a landmark's works
  .Street            the pieces, the crowds, the traffic, the game's panel at a front, the city map's row, the opening
  .Paper             the names and words, the City Ledger's pieces
  .Tests             the twelve test entries, each a check of the test run
LandmarksText        the locale keys
LandmarksGuide       the feature list's entry: the option, the lines, the Guide's figures, the tests, the clean-up
```

One line or a few each elsewhere: `Core/Settings.cs` (`uptown_landmarks`), `Meta/OptionsTree.cs` (its section in the City group), `Meta/FeatureCities.cs` (New York's), `Core/FeatureOwners.cs`, `Core/RealLifeOverhaulMod.cs` (registered at game start after Uptown's, the director added after the construction game's), `Wiring/Wiring.cs` and `Wiring/Wiring.LandmarkLinks.cs`, `Wiring/Wiring.ConstructionLinks.cs` (the construction game's ten landmark bridges).

## The sites

Each landmark takes one block of the first block: every lot of it not built from the district's first day (Uptown's own rule, `DistrictBuildings.FirstDay`), the pair of street lots nearest 1st Avenue left to the day-one buildings. A block is taken in each landmark's order, the first whose every such lot is free (no building on it or waiting, nobody's land, no project, no development) and that no other landmark has:

| Landmark | Its blocks, in order | On a new save | Ground |
| --- | --- | --- | --- |
| Summit Tower | 111, 112, 113, 110, 107, 106, 108, 109 | between 4th and 3rd Street | 70 x 70.6 m, 7 lots |
| Uptown Galleria | 113, 112, 111, 110, 107, 106, 108, 109 | between 2nd and 1st Street | 70 x 71.6 m, 9 lots |
| Crest Field | 110, 112, 113, 111, 107, 106, 108, 109 | between 5th and 4th Street, by the buses' terminus | 70 x 71.6 m, 9 lots |

The stadium also needs 58 m of ground back from the avenue. A landmark still to be announced holds the block it would take today; announced, its block is settled for good. Its lots are **held**: `DistrictBuildings.Taken` says so (through `DistrictBuildings.HeldFor`), so the construction game's Land tab offers none of them, the developers pass them over, the public tenders never take one, and the test run never sends a test there; the Land tab shows each landmark as a row of its own (its state, its tender), and the city map has a **Landmarks** row with a marker at each front. Works going on or done stay held with the option off.

The works stand on a plot of the city's programme on the site: as wide and deep as the site, 60 m each way at most, its front on Uptown Avenue.

## How they are built

1. **Announced** on its day: alone, no sooner than day 12, 26 and 40 and 3, 17 and 31 days after the programme first ran in the save; in a Going Public session on those fixed days, the same on every machine. The City Ledger leads with it.
2. **Its design**, picked once and kept: of the city's own designs that fit both the works' frame and the ground the building stands on (`BuildingCopies.Fitting`), the tallest office for the tower and the largest shop (the best covering) for the galleria. With the copy engine off, the game's K office and M store with a design of their size at the load. The stadium is the mod's own.
3. **Out to tender** in the construction game for four days (`ConstructionDirector.LandmarkTender`): its value the cost of a plot of its use, floor area and storeys (offices of 14,000 m2 on ten storeys, shops of 9,000 on three, a stadium priced as 16,000 m2 of steel and concrete) with the public building's margin and the landmark's 40% more. The player bids in the Tenders tab as for any tender; the award is the construction game's own.
   - **The player wins:** a project of the company's like a public building's, on the landmark's site, wearing its design: crews, materials, the three inspections, payment half way and at the end, and the construction game's ribbon at the opening.
   - **A rival wins:** the landmark's own works on a site of the programme, the rival as its contractor, kept to the days they bid.
   - **Nobody can bid** (a Going Public session, the construction game or its tenders off, a tender lapsed): the city's own works, 48, 40 and 48 working days from three days after the announcement, one of the programme's contractors engaged.
4. **Its works** are a plot of the city's programme: hoarding, dig, slab, the tower crane, the frame storeys, cladding and wrap. A rival's or the city's site goes at the timetable's pace (`LandmarkRules.Pace`) and is handed over on its due day; only the host of a session sets one out. The City Ledger tells of each phase (groundworks done, topped out, fitting out).
5. **Handed over:** the tower stands over all its lots as one district building of Uptown's (`DistrictBuildings.FinishOver`), the galleria's main building over its avenue lots and the side pair nearest them, with a shop of its own on each lot behind (`DistrictBuildings.Finish`), all the city's, to let, from the next load; until then the programme's look of its design stands there. The stadium's stands go up at once, out of view, and the programme keeps no look of its own on its plot.
6. **Opened** the next day with a ceremony at its front from 10:00 to 18:00: a crowd of the game's own pedestrians on the pavement, the game's panel offering **Join the opening** (Standing in the district from the press), and the City Ledger's story. The player's own company has the construction game's ribbon instead.

## What they do

- **The lots round them:** once built, each lot's traffic index rises with how near it is (8, 10 and 6 points next to the tower, the galleria and the stadium, falling to none at 130, 150 and 170 m; 14 at most together), up to 80. It is Uptown's own index (`DistrictBuildings.TrafficOf`, through `DistrictBuildings.TrafficLift`), so the game's customers come by it, a building of the district gets it at once and from every load, and the construction game prices the land there by it.
- **Jobs:** some 1,400 people in the tower, 520 at the galleria and 260 at the stadium once open, 650 more on a match day: the ones in the tower and the galleria are the businesses' the game lets those buildings to; the figures are the City Ledger's and the Guide's.
- **Shops to let:** the galleria's main building and each of its shops are units of the district's own, let to the city's businesses or rented by the player through BizMan.
- **Match days:** Uptown FC play at home every Saturday after Crest Field opens, kicking off at 19:00. From 17:00 to 22:00 crowds stand on the pavements in front and up both side streets; the whole day the district's buildings within 200 m of the stadium have 12 more traffic. The panel at the front gives the next match and the last result; the City Ledger has the match in the morning and the score the next day (the score and the crowd from the day alone, the same on every machine).
- **The City Ledger and Flare:** the day's biggest landmark story leads the front page, so the City Ledger's account on Flare posts it as its first headline and the city's people answer it in the comments.

## The stadium: the props bundle's own

Crest Field is built of six props of the building sites' bundle (`PropAssets/scripts/props_landmarks.py`, `paint_landmarks.py`, `Bundles/reallife_props_site`): a 10 m section of a roofed main stand (nine rows of seats, the back wall to the street with the district's band and gates, a roof held by masts and ties), a 10 m section of an open end stand, a corner stair tower, a floodlight mast and its lamps (lit at night), and the pitch painted on the bundle's atlas with a running track round it; 2,802 triangles, the bundle 6,350 of its 12,000. `LandmarkRules.Stadium` lays them on the site: a forecourt with six turnstiles on the avenue, the main stands north and south, the ends west and east, the corners, a mast at each outer corner turned to the pitch. Each stand, corner and mast is a box the player walks round and a cut in both walking meshes. While the bundle is not installed each is drawn as plain shapes of its size.

## Saves

- One line, `reallife:landmarks` ("v=1^a:anchor|last day^l:records"), and `reallife:landmarks_before_tests` while a test has changed something. Fields are appended at the end of a record; a newer line is read as far as this version knows it; a line this version cannot read is kept as it was read and nothing moves.
- Everything of theirs in the game's part of the save is Uptown's: the tower's and the galleria's buildings are district buildings whose records the district save keeps out of every save. The landmarks also name their sites' ground and their buildings' doors to the district save (`DistrictPlaces.Add("landmarks", ...)`), so a save made inside one or on a site puts the player at the way out. A save loads without the mod: the landmarks are then simply gone, and come back with it.
- Uptown's own line gains a field: the lots a building stands over besides its own (`FinishedBuilding.Over`).

## Going Public

- **Deterministic:** the sites from the plan and the lots' claims alone, the timetable on fixed days in a session, the design picked from the copy engine's catalogue (the same on every machine), the season from the day alone.
- **Nothing new sent:** no tender in a session; the works are the programme's own records in the host's existing line; a guest knows the host's landmark site by where it stands.
- **Nobody's things changed:** the landmarks' buildings are the city's, never a deed made on one machine.
- **Known limit:** a save that built a landmark alone keeps it when it joins a session whose timetable has not reached it yet.

## Options

- **Landmarks of the new district** (on, `uptown_landmarks`): off, nothing is announced, tendered or held, no lift, no crowds, no pieces; works going on or done keep their lots held, and what stands as the district's stays.

## Self-check lines

With Developer diagnostics on: `Landmarks check (day N): ...: PASS`, `Landmarks check (pieces, stadium): N of N piece(s) the bundles' models, ...: PASS`, `Landmarks check (crowds): ...`, `Landmarks check (traffic): ...`, `Landmarks check (panel): ...`, `Landmarks check (joined): ...`. Always: `Landmarks: read ...`, `Landmarks: running.`, each landmark's announcement, tender, builder, design, handover and opening.

## Tests (Options > Tests, with "Show test tools" on; each a check of the city expansion's test run)

| Button | Label key | Line | Place, phase |
| --- | --- | --- | --- |
| Landmarks: census | `reallife_lmk_test_census` | `Landmarks check (census)` | Here, before the load |
| Landmarks: the next landmark announced now | `reallife_lmk_test_announce` | `Landmarks check (announce)` | Here, before the load |
| Landmarks: its tender awarded now | `reallife_lmk_test_award` | `Landmarks check (award)` | Here, before the load |
| Landmarks: Summit Tower finished now | `reallife_lmk_test_tower` | `Landmarks check (tower now)` | Here, before the load |
| Landmarks: Uptown Galleria finished now | `reallife_lmk_test_mall` | `Landmarks check (mall now)` | Here, before the load |
| Landmarks: Crest Field finished now | `reallife_lmk_test_stadium` | `Landmarks check (stadium now)` | Here, before the load |
| Landmarks: an opening day today, and you at it | `reallife_lmk_test_opening` | `Landmarks check (opening)` | Here, before the load |
| Landmarks: a match day now at Crest Field, and you at it | `reallife_lmk_test_match` | `Landmarks check (match)` | Here, before the load |
| Landmarks: take me to Crest Field / Summit Tower / Uptown Galleria | `reallife_lmk_test_take_stadium`, `_tower`, `_mall` | `Landmarks check (take me, stadium)`, `(take me, tower)`, `(take me, mall)` | Here, before the load |
| Landmarks: put everything back as before the tests | `reallife_lmk_test_back` | `Landmarks check (back)` | Here, at the end |

Each works from wherever the player is and brings forward what it needs: a landmark not announced is announced, a tender awarded, works handed over, a stadium finished and opened, the player taken to the front by the game's own way out of a building or a car. The put back restores the landmarks' state from before the first test, takes back the district buildings the tests finished that no load has stood yet, the tenders they opened and the sites they set out; a landmark whose building has stood at a load stays built with it (a save from before the tests undoes it).

## Offline checks

EconomyCheck `LandmarksChecks.cs` (`--landmarks` alone): the sites on the real plan (each landmark's block on a new save, every lot not built from the first day, the address on the avenue, the plot inside the site that the programme takes, the anchor and the shops, a K office and an M store fitting, the next block when a lot is claimed, none when all are, the same in any order, a block too shallow for the bowl); Uptown's span (its ground, its refusals, a size its own lot does not take, the lift's bounds, its line with and without spans); the timetable against the construction game's week and working days, its phases against the plot's stages, the pace; the lift and a match day's; the season (Saturdays after the opening, every club in turn, scores and crowds in bounds and the same everywhere); the stadium's pieces, the crowds on the pavements, the plaza round the tower; the line read back whole, bounded, a later and an older one; the construction game's landmark tender and project read back; the sources (no LINQ, game-free rules, one guarded Update, the scene in LandmarksGame alone, the bridges wired and put back, the session's rules, the shared files' lines, each test a check of the run writing its line); every text in en, pt and zh-cn with its placeholders, the notices' length, the names never the game's, What's new in all nine.

## Not verified without the game

Everything above is built and checked offline. Not seen in play: the stadium's pieces in the street at their scale, their boxes and cuts in the walking meshes; the tower and the galleria standing over their lots from the next load (the made buildings' ground check over a span); the designs the copy engine picks; the crowds of the game's pedestrians on the pavements (whether the pedestrians' mesh reaches each square); the traffic index's effect on the shops near them; the game's panel at a front; the map's row; the Land tab's rows; the City Ledger's lead story and its Flare headline.
