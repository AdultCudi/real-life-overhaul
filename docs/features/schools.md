# Birchwood School

## In a sentence

A school for ages 5 to 17 on Uptown's west square, built of the city's own designs and the game's own pieces, and the city's twelve schools as one market: the player may buy Birchwood School and run it (its fee, classes, teachers' pay, scholarships and headteacher) against four education trusts and the council, who run theirs by the same rules, with terms, results day, inspections and a league table in the City Ledger; the player's own children can go there.

## Where the code is

- Folder: [`Scripts/Schools`](../../Assets/Mods/RealLifeOverhaul/Scripts/Schools/) (namespace `RealLifeOverhaul.Schools`).
- Read first: `SchoolRules.cs` (every rule, game-free), `SchoolGrounds.cs` (the campus on its block, game-free), then `SchoolsDirector.cs`.

```
SchoolRules          game-free: the catalogue of twelve schools, families sharing the paying places, teaching, results day,
                     inspections, a name's drift, staffing, the pay war, money, worth and prices, the owners' week, the
                     calendar, a day's pass, the district's lift
SchoolState          game-free: a school's record, the market's state, the City Ledger's news
SchoolStore          game-free: the line in the save (reallife:schools)
SchoolGrounds        game-free: the main school's lot, the hall and the junior block, the court, the green, the pitch, the
                     fence with its three gates, the families' squares
  Dressing           game-free: the court as two half courts with its fence, the paths, the lawns' trees, benches, table groups, bins,
                     bicycle stands, climbing frames and planters, the nine squares people gather in and when, the routes on foot
SchoolsBridges       what it asks of family life and the council; filled by Wiring.SchoolLinks
SchoolsGame          the one file that touches the game: the clock, money, notices, the main school declared and cut, the
                     pieces copied, the hall and the junior block, the families at the gates, the guider, the traffic
CampusLookRules      game-free: the three models' facts, the game's materials by name, where a model stands on a door or a plot, what is solid and carved, the load's lines
CampusLook           the game's side of the look: the materials found, the models stood (the main school over the copy engine's building, the blocks solid), measured, given back
SchoolFloors         the main school as the floors core holds it, the game's school on three floors
SchoolsDirector      the state and its timing, the save, the district save's view, the lift
  .Campus            the place at a load and once a second
  .Look              the models at a load (or when the bundles are read), the fallback to the copy engine's designs
  .LookTests         the look's two test entries
  .Market            a day's pass, the week's book and the player's levers
  .Paper             the City Ledger's pieces
  .Tests             the test entries
SchoolsPage          the Campus app's Schools tab (asked through CareersBridges.SchoolsTab)
SchoolsText          the locale keys
SchoolsGuide         the feature list's entry: the option, the line, the Guide's figures, the tests, the clean-up
```

One line or a few each elsewhere: `Core/Settings.cs` (`uptown_campus`), `Meta/OptionsTree.cs` (its section after the landmarks'), `Meta/FeatureCities.cs`, `Core/FeatureOwners.cs`, `Core/RealLifeOverhaulMod.cs` (registered after the landmarks; the director before the made buildings'), `NewBuildings/NewBuildingsRules.cs` (its street, Birchwood Green), `Uptown/LayoutRules.cs` (the campus's square), `Careers/CampusApp.cs` and `CampusApp.Schools.cs` (the tab), `Careers/CareersBridges.cs`, `Family/FamilyBridges.cs`, `Family/Children*.cs` (the fee, reports and school run), `Wiring/Wiring.cs` and `Wiring/Wiring.SchoolLinks.cs`.

## The campus

On block 202, the plan's west square, which was meant as a park and never laid as one: 70 by 120 m between Birch Street, Elm Street, Uptown Avenue and Summit Avenue.

- **The main school** on Uptown Avenue: a made building of the copy engine (a K1 office's design picked by its id) at 1 Birchwood Green, a street of the mod's own, its front a forecourt back from the gate, three storeys, cut out of both walking meshes. What is seen of it is its own model ("The look" below); the building's door, floors, walls and click are the engine's. The floors core holds it with the game's own school on each floor (structure D1, layout GarmentDistrictSchool): the juniors, the middle school and the seniors, five classrooms a floor; its door panel opens school days from 07:00 to 18:00. The game's classroom doors there belong to its business school's diplomas, so their colliders are switched off once a floor is laid.
- **The hall** on Birch Street and **the junior block** on Elm Street: their own models ("The look" below), solid and cut out of the walking meshes, standing clear of the fence; the city's designs fitted to their plots are the fallback only.
- **The courtyard:** between the hall's back and the junior block's (15.4 m, `SchoolGrounds.Courtyard`): the court (below), the plaza north of it with two benches, a table group and two trees, and the strips beside the court's fence left open to walk.
- **The court:** the game's basketball court plane is a HALF court (7.73 by 5.13 m at the scale the game places it, its hoop 0.6 m in from its baseline, at the middle of it; the Skatepark stands two back to back). The campus stands two the same way, each stretched evenly by 1.55 to 11.98 by 7.95 m (15.9 m in all), each with the game's hoop at its baseline facing in (the backboard to the baseline), a net hung from the rim (a short twelve-sided cone drawn with the game's chain-link material: the game has no netted hoop it draws on its own, the Skatepark's and Sunset Park's being static batches, and the hoop prefab has a bare rim) and the game's playground fence round the court: whole on the south baseline and the long sides, open 3.6 m in the middle of the north baseline to the plaza. **The plane lies at 0.09 m**, over the block ground's grass top (0.0552 m, its mesh, Build 3682): at 0.03 m, as it was, it was under the lawn, which was why the hoops stood on bare grass. The pitch lies at 0.09 m too.
- **The playing field:** the building sites' bundle's pitch (Crest Field's), stretched evenly by 0.76 to 39.5 by 36.5 m at the north end, 6 m in from the north fence and 15 m from the east and west (`PitchNorth`, `FieldMargin` 5 m: a margin to walk and plant in). No new prop.
- **The fence:** the game's playground fence modules (Industry City's, 1.4 m each), end to end round the block in seven runs, open at the main gate on Uptown Avenue, the side gate on Elm Street and the field's gate on Summit Avenue; one carving box a run.
- **The families:** in the school run's hours (07:30 to 09:00 and 14:45 to 16:15, Monday to Friday) and on a term's open day, while the player is within 300 m, the game's own pedestrians gather on the pavements before the main and side gates (`PeopleGame` zones).
- **On the map:** a Schools row (the paper icon, shown in the street too as every claimed building's row is) with a marker at the main gate, and the school in the map's search by its name (the card's path).

## The card

The game's building card (BuildingResume) read the main school as the K1 office it is built from: "1 Birchwood Green, 30, 660 m2 (K1), 50, OFFICE, Neighborhood: Uptown, CLOSED, right click for options", closed because the registration holds no business and so no hours (Player.log of 26 Oct 2026, a Friday at 14:56). The main school is now filed in the mod's one card path, `SpecialBuildings` (the university's, the precincts', the courthouse's and the hotels'), `SchoolsDirector.FileCard`: its **name** Birchwood School, its **kind** "School" (`reallife_schools_card_type`), the state line the game's own for a business (`buildingresume_open_closing_in_hours` and its three kin: "Open, closing in 4 hours" in green, "Closed, opening in 61 hours" in red) **by the school's own hours** (`SchoolRules.Open`: a school day, 07:00 to 18:00, whole game hours as the game's cards count them; the floors core's door opens by the same rule), **who runs it** in the card's owner line ("Run by the council", "Run by Meridian Schools", "Run by you", from the school's file) and the card's "click to enter" while it is open. `SpecialBuildings.Facts` gives an entry a callback the card asks once a second; `SpecialNames.Facts` writes the lines. Only what the card shows changes, never the save; with the option off the game's own card is back. The right-click options are the game's (the building's info in BizMan and, on the city map, set destination): renting is refused for every building of the mod's own book. The hall and the junior block are looks with no controller, so the game shows no card for them; the Schools row of the map's filters and the map's search list the school as the book names it.

## The dressing

Laid with the campus behind the loading screen from the game's own pieces by the shared finder (`GamePieces`), game-free in `SchoolGrounds.Dressing`, deterministic, nothing per frame, each piece cut out of both walking meshes as the parks' trees are (`SchoolsGame.Obstacle`):

- **Paths** of the game's pavement material (the parks' way, `ParksGame.PavingMesh`, 0.09 m): the main gate to the main school's door (following the door where the copy engine puts it), along the front to each yard and down each yard; the side gate and the field's gate to the plaza, the court's opening and the field; the green's cross path from the side gate to the hall's and the junior block's north ends (their doors face Birch and Elm Street, 0.2 m inside the fence, so no path can reach them: the paths end at their ends).
- **Trees** (the Vegetation root's broadleaf, birch, beech and maple), **benches** (the pavement bench, found in Madison Square Park's group where its own root is not there at a load), **table groups** (Madison Square Park's round table with four chairs people sit on), **litter bins**, **bicycle stands** (the street furniture's docks, by the gates), **climbing frames** (the Industry City playground's, the one piece of it not drawn in a static batch) and **planters** (the game's broadleaf bush: four on the forecourt 1.2 m in front of the main school's front wall at 13.5 and 17.5 m either side of its middle, two 1.0 m in front of the junior block's door).
- **Clear:** 0.9 m from the fence line, nothing in a gate's way, off the paths (the plaza's paved square aside), off the buildings' bodies, the court and the field.

## The people

The game has no child models: the people are its own pedestrians, adults and teenagers of its pools, so a pupil is as old as the game's youngest pedestrian; the families at the gates were always these. They gather in squares of ground, the game's own zones copied (`PeopleGame.Zone`), by the school's clock (`SchoolRules.CrowdMoments`), only while the player is within 300 m, none at night or at the weekend:

| Moment | Squares |
| --- | --- |
| School run (07:30 to 09:00, 14:45 to 16:15) and a term's open day | the pavements before the main and side gates, the forecourt before the main door |
| Morning break (10:30 to 10:50) | the plaza, the court, the two yards |
| Lunch (12:30 to 13:30) | the forecourt, the plaza, the court, the green's tables, the two yards |
| After classes (15:30 to 17:30) | the court and the field |

The gates are the ways in and out: the walking meshes join the campus to the pavements there, and the fence is cut out of them everywhere else. A load, a few seconds after the campus stands, writes one line, `Schools check (campus reach)`: whether people (the NPC agent type) and the player can walk each of nine routes from a gate to the main door, the yards, the court, the plaza, the hall's and the junior block's ends and the field on the walking meshes as grown. No basketball or hoops activity exists in the game or the mod: a "shoot hoops" one would need a clip of the mod's own (a dribble and a shot, the game's animation controller has none for a ball), a ball prop (not among the game's pieces: a new prop, in `reallife_props_2`), a place the click starts it at, the game's own `PlayerActivityUI` panel with its skip and fast forward as the chess and fishing have, and an NPC side (the same activity on the court's squares); it is not built.

Every piece is the game's, found once a load behind the loading screen (the one of its name nearest a fixed place of the game's scene, never one in a static batch); a piece not found is left out with one line.

## The look

Birchwood School's three buildings are modelled by hand in Blender (not-in-repo, exported as glTF) and shipped as props of the second props bundle, `reallife_props_2` (`PropAssets/models/birchwood_main`, `_hall`, `_junior`; PropAssets/README.md, "Buildings"). `Schools/CampusLook` (the game's side), `CampusLookRules` (game-free) and `SchoolsDirector.Look` stand them behind the loading screen with the rest of the campus.

- **Materials, and why.** Each model is one mesh with four or five submeshes, so four or five draw calls: the bundle's atlas for the limestone, frames, sashes (swatch_sash, the dark green black of the city's old windows), door, copper, lamps and the rest of the small parts (one tiny 512 x 256 atlas for all three), and the game's own **brick** (`M_Bricks_01`), **roof** (`M_Roof_02`) and **glass** (`M_Glass`: opaque, metallic, grey-blue 0.48, 0.53, 0.56, smoothness 0.87, so it reflects the street and reads dark; the bright teal `M_GlassBlueHue` was the first choice and is the fallback), the junior block's rooftop fence the game's **chain-link** (`M_FenceWire_Occlussion`). Found by name once a load, in one search of the game's materials in memory behind the loading screen (never in play); a kind the game does not have is a plain colour from the model. The other way, baking brick into the atlas, cannot tile (an atlas region is drawn once), so a wall would need a huge picture; the game's brick tiles by the model's world-scale UVs, has its normal map and costs one draw call. Triangles: the main school 14,806, the hall 2,756, the junior block 3,684 (21,246 of the bundle's 60,000), 541 KB on disk (the 26 Oct 2026 re-export: window sashes in their own dark swatch, slimmer sills and lintels, wall lanterns, the JUNIOR SCHOOL letters raised above the canopy, the cone bushes taken out: the planters are the game's own bushes).
- **The main school** keeps its copy-engine building: its controller, door, floors, walls and the click. Its own look is hidden by the engine's own path (`CopyGame.HideLook`, what a construction site's new look does) and the model stands over it with its **door on the building's own door**: the model's door (its front face 1.55 m in front of the front wall, in the middle) is put at `entranceDoors[0].doorTransform` and turned to face the way that door faces, so the click, the walk to the door, the way in, the saved place indoors, the floors core's panel and the door reach rules are untouched. The model's body then stands where that puts it: within 9 m across and 6 m along of where the copy engine's footprint stood, else the model is not stood and the building keeps the look it has, with one line. Its walking-mesh cut is the model's body and its entrance bay (the copy engine's footprint is not its shape). The model joins the building's see-through fade (its hide list) and the city map shows the model's box.
- **The hall and the junior block** stand on `SchoolGrounds.Blocks()` plots, their front wall `Front + 0.5` m back from the block's edge so what stands out of it (the hall's porch, the junior block's canopy) ends inside the fence, in the middle of the plot's width, facing their street. Each has a box collider over its body, joined to the game's see-through fade through the same `CopyLook` the city's copies use, a carving obstacle, and the map's box. With the bundle or a model missing the copy engine's design is asked for as it was, with one line.
- **Lit and faded as the city's copies:** the renderers take the lighting settings (probes, reflections, rendering layer) of the main school's own copy, the layer is the buildings', they are joined to the district's far squares (`FarDistricts.Include`), and the see-through fade is the game's (the models' atlas material has no reveal, so what fades is the model as a whole when the camera is blocked: it is hidden to its shadow, as the game's building is).
- **Measured once a load**, one line in Player.log: each model's triangles and draw calls, the game's materials found (and how long the search took), the time to place all three, and what the design the main school's model replaced drew. Options > Tests has the comparison for the hall and the junior block (`Schools check (compare)`) and a test that stands the look again (`Schools check (look)`).
- **Nothing per frame.** Going Public safe: a local look, nothing saved and nothing sent; old saves are unchanged; with the mod removed the game is the plain game.

## The market

| School | Kind | District | First owner |
| --- | --- | --- | --- |
| Birchwood School | the campus | Uptown | the council, for sale |
| Meridian Prep, Ashford House, Northbridge Academy, Calder College | private | Midtown, Murray Hill, Lower Manhattan, the Hamptons | the four trusts |
| The council's school of each district | free | the game's seven | the council |

- **Families** (3,000 of the city's children at schools their families pay for) share the private schools by each school's pull: its name squared, its fee against the fair fee (60 a day at the start's prices), its scholarships. A school takes what fits its places; the rest wait.
- **Teaching:** the head (35%), the teachers' skill (45%) and the pupils to a teacher against 18 (20%).
- **Results day** at the end of the summer term (two thirds through the year): teaching, intake (scholarships and a waiting list) and the name, with a year's luck of four points; the league table in the City Ledger.
- **Inspections** every 45 days a school: Outstanding, Good, Requires improvement or Inadequate.
- **The pay war** at each term's start: a school short of teachers takes them from the lowest payer when it pays a tenth more.
- **Money** by the day: paying pupils' fees (scholars pay none) less pay, the head, the pupils' running costs and the buildings' upkeep; a council school lives on the council's grant. The council's grants lever (Civic) lifts or lowers its schools' teachers.
- **Worth:** 600 days of its profit, never under 12 million; its owner asks 115%; the trusts pay the player 90%, or offer 105% after the player has held it three weeks.

## The owner's rule: what the NPCs do

| The player | The trusts and the council |
| --- | --- |
| Buys Birchwood at its owner's asking price | the trusts buy at the same price: Birchwood from the council after 14 days, any school a trust keeps losing |
| Sells it to the trusts, or takes their offer | the trusts offer for the player's school; it is never taken without the player |
| Sets the fee, opens and closes classes, sets pay and scholarships, hires a head | the trusts' week pulls the same levers by the same steps (`SchoolRules.Policy`), the council keeps its staff to the ratio |
| Pays more to take teachers | the pay war is one rule for all: a trust takes the player's teachers the same way |
| Enrols their children | the city's families fill every school's places |

## With the rest of the mod

- **Family:** a child of school age is enrolled from the Schools tab (`FamilyApi.SetSchool`: a private school to family life). The family's weekly charge takes the campus's fee instead of a private school's (none at the player's own), the reports and parents' evenings follow its results (`ISchoolBridge.StandingOf`), the school run and parents' evenings go to its main gate.
- **The Campus app:** a sixth tab, Schools: the league table, a school's page, Birchwood's running, its sale and the children.
- **The City Ledger:** the opening, a sale, results day, an inspection of the campus or of a school rated outstanding or inadequate, the pay war when it touches the campus, an open day; a lead goes to the front page and the Ledger's Flare account.
- **Uptown's lots:** the campus's name lifts the traffic of the lots within 160 m (2, 4 or 6 points with a name of 600, 700 or 800), chained after the landmarks' (`DistrictBuildings.TrafficLift`): more customers and the game's price per square metre with them. None in a Going Public session.

## Saves

- One line, `reallife:schools` ("v=1^a:...^s:schools^c:children^n:news"), and `reallife:schools_before_tests` while a test has changed something. Fields are appended at the end of a record; a newer line is read as far as this version knows it; a line this version cannot read is kept as it was read and nothing moves. An older save has no line: the campus opens on its next day's pass.
- The main school's door is named to the district save by the made buildings, the campus's street by this feature; its ground is Uptown's. The plain game never sees the street, and a save loads without the mod.

## Going Public

- **Deterministic:** the market runs from the line and the day alone; chance is FNV-1a over the year and the school.
- **Nothing sent, nothing of another's changed:** in a session nothing is bought or sold, the player's children never change the market's figures, and the district's lift is off. A save that owns Birchwood alone keeps it on that machine.

## Options

- **Birchwood School in Uptown** (on, `uptown_campus`), in its own section after the landmarks' under the city's options: off, its pieces, the hall and the junior block go at once and the main school from the next load, the market rests and nothing of the campus is charged (a child there stays at a private school as family life has it).

## Tests (Options > Tests)

| Button | Line |
| --- | --- |
| What is this ground? (Uptown's entry, in the city expansion's run) | `Ground check (what is this ground)` |
| Birchwood School: census | `Schools check (census)` |
| Birchwood School: stand its own look again | `Schools check (look)` |
| Birchwood School: compare its look with the city's designs | `Schools check (compare)` |
| Birchwood School: take me to its gate | `Schools check (take me)` |
| (the load) the game's card, the routes walked | `Schools check (card)`, `Schools check (campus reach)` |
| Birchwood School: the school run now | `Schools check (school run)` |
| Birchwood School: results day now | `Schools check (results day)` |
| Birchwood School: an inspection now | `Schools check (inspection)` |
| Birchwood School: a term starts now | `Schools check (term)` |
| Birchwood School: make it mine | `Schools check (make it mine)` |
| Birchwood School: enrol my child | `Schools check (enrol)` |
| Birchwood School: put everything back | `Schools check (back)` |

## Offline checks

EconomyCheck `SchoolChecks.cs` and `CampusDressingChecks.cs` (`--schools` alone: the card's rule by the hours, the court, the layout, the people, the sources and texts) and NavCheck `GroundSurveyCheck.cs`.

## Not verified without the game

How the models look in the game (the game's brick, roof, glass and chain-link at the model's UV scale, the lamps, the limestone's grime), whether the game has the four materials by those names in memory at a load, where the main school's door puts its body against the copy engine's footprint, the models' lighting and fade next to the city's own copies, the junior block's fence (single sided?) and the copy engine's designs as the fallback (the hall's and the junior block's, side by side); the pieces' heights on the block's ground (the court plane, the hoops, the benches) and the court's markings when stretched; the fence's look and its carving; the floors core's panel at the main school's door; the families at the gates; the Schools tab's look in the Campus app; the card's lines on the game's own card (the colour, the state line and the owner line beside its other lines, a second's delay after a change of hour), the court's plane at 0.09 m (its markings stretched 1.55 times), the nets on the hoops (their size on the rim), the paving over the main school's forecourt slab (its height), the table groups' and climbing frames' turn and room, whether people walk the campus (the load's reach line), M_Glass on the windows.
