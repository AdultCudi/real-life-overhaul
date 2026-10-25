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
SchoolFloors         the main school as the floors core holds it, the game's school on three floors
SchoolsDirector      the state and its timing, the save, the district save's view, the lift
  .Campus            the place at a load and once a second
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

- **The main school** on Uptown Avenue: a made building of the copy engine (a K1 office's design picked by its id) at 1 Birchwood Green, a street of the mod's own, its front a forecourt back from the gate, three storeys, cut out of both walking meshes. The floors core holds it with the game's own school on each floor (structure D1, layout GarmentDistrictSchool): the juniors, the middle school and the seniors, five classrooms a floor; its door panel opens school days from 07:00 to 18:00. The game's classroom doors there belong to its business school's diplomas, so their colliders are switched off once a floor is laid.
- **The hall** on Birch Street and **the junior block** on Elm Street: decorative copies of the city's designs fitted to their plots, cut out of the walking meshes once they stand.
- **The courtyard:** the game's basketball court plane (with its markings, from Factory3's yard) stretched evenly to 24 by 15 m, its two hoops, four of the game's sittable benches and four street trees.
- **The playing field:** the building sites' bundle's pitch (Crest Field's), 52 by 40 m at the north end. No new prop.
- **The fence:** the game's playground fence modules (Industry City's, 1.4 m each), end to end round the block in seven runs, open at the main gate on Uptown Avenue, the side gate on Elm Street and the field's gate on Summit Avenue; one carving box a run.
- **The families:** in the school run's hours (07:30 to 09:00 and 14:45 to 16:15, Monday to Friday) and on a term's open day, while the player is within 300 m, the game's own pedestrians gather on the pavements before the main and side gates (`PeopleGame` zones).
- **On the map:** a Schools row with a marker at the main gate.

Every piece is the game's, found once a load behind the loading screen (the one of its name nearest a fixed place of the game's scene, never one in a static batch); a piece not found is left out with one line.

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

The designs the copy engine picks for the main school, the hall and the junior block and how they look side by side; the pieces' heights on the block's ground (the court plane, the hoops, the benches) and the court's markings when stretched; the fence's look and its carving; the floors core's panel at the main school's door; the families at the gates; the Schools tab's look in the Campus app.
