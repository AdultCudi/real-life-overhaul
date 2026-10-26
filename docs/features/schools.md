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
- **The courtyard:** between the hall's back and the junior block's (15.4 m, `SchoolGrounds.Courtyard`): the game's basketball court plane (with its markings, from Factory3's yard) stretched evenly to 19.2 by 12 m, its two hoops, four of the game's sittable benches (two each side of the court, in the strips beside it, and two at its north end) and four street trees.
- **The playing field:** the building sites' bundle's pitch (Crest Field's), 52 by 40 m at the north end. No new prop.
- **The fence:** the game's playground fence modules (Industry City's, 1.4 m each), end to end round the block in seven runs, open at the main gate on Uptown Avenue, the side gate on Elm Street and the field's gate on Summit Avenue; one carving box a run.
- **The families:** in the school run's hours (07:30 to 09:00 and 14:45 to 16:15, Monday to Friday) and on a term's open day, while the player is within 300 m, the game's own pedestrians gather on the pavements before the main and side gates (`PeopleGame` zones).
- **On the map:** a Schools row with a marker at the main gate.

Every piece is the game's, found once a load behind the loading screen (the one of its name nearest a fixed place of the game's scene, never one in a static batch); a piece not found is left out with one line.

## The look

Birchwood School's three buildings are modelled by hand in Blender (not-in-repo, exported as glTF) and shipped as props of the second props bundle, `reallife_props_2` (`PropAssets/models/birchwood_main`, `_hall`, `_junior`; PropAssets/README.md, "Buildings"). `Schools/CampusLook` (the game's side), `CampusLookRules` (game-free) and `SchoolsDirector.Look` stand them behind the loading screen with the rest of the campus.

- **Materials, and why.** Each model is one mesh with four or five submeshes, so four or five draw calls: the bundle's atlas for the limestone, frames, door, copper, lamps and the rest of the small parts (one tiny 512 x 256 atlas for all three), and the game's own **brick** (`M_Bricks_01`), **roof** (`M_Roof_02`) and **glass** (`M_GlassBlueHue`), the junior block's rooftop fence the game's **chain-link** (`M_FenceWire_Occlussion`). Found by name once a load, in one search of the game's materials in memory behind the loading screen (never in play); a kind the game does not have is a plain colour from the model. The other way, baking brick into the atlas, cannot tile (an atlas region is drawn once), so a wall would need a huge picture; the game's brick tiles by the model's world-scale UVs, has its normal map and costs one draw call. Triangles: the main school 14,786, the hall 2,576, the junior block 3,740 (21,102 of the bundle's 60,000), 521 KB on disk.
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
| Birchwood School: the school run now | `Schools check (school run)` |
| Birchwood School: results day now | `Schools check (results day)` |
| Birchwood School: an inspection now | `Schools check (inspection)` |
| Birchwood School: a term starts now | `Schools check (term)` |
| Birchwood School: make it mine | `Schools check (make it mine)` |
| Birchwood School: enrol my child | `Schools check (enrol)` |
| Birchwood School: put everything back | `Schools check (back)` |

## Offline checks

EconomyCheck `SchoolChecks.cs` (`--schools` alone) and NavCheck `GroundSurveyCheck.cs`.

## Not verified without the game

How the models look in the game (the game's brick, roof, glass and chain-link at the model's UV scale, the lamps, the limestone's grime), whether the game has the four materials by those names in memory at a load, where the main school's door puts its body against the copy engine's footprint, the models' lighting and fade next to the city's own copies, the junior block's fence (single sided?) and the copy engine's designs as the fallback (the hall's and the junior block's, side by side); the pieces' heights on the block's ground (the court plane, the hoops, the benches) and the court's markings when stretched; the fence's look and its carving; the floors core's panel at the main school's door; the families at the gates; the Schools tab's look in the Campus app.
