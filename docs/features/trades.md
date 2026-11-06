# Building trades

## In a sentence

Work in the city's building trades: at a works site's notice, the trades its stage has on (labourer to night security and the city's building inspector), each played in a game of its own on the game's panel and bar; at an architects' practice on a floor, design work, draughting, frames and tenders; courses, an apprenticeship and three ladders to climb.

## Where the code is

- Folder: [`Scripts/BuildTrades`](../../Assets/Mods/RealLifeOverhaul/Scripts/BuildTrades/), the trades' jobs in [`Scripts/Jobs/Kinds`](../../Assets/Mods/RealLifeOverhaul/Scripts/Jobs/Kinds/) (`TradeJob*`, `DesignJob*`, `TenderJob`).
- Read first: `TradeCatalogue.cs` (every trade, where it is on and what it asks), `TradeRules.cs` and `DesignRules.cs` (game-free), then `TradeJob.cs`.

| File | What it is |
| --- | --- |
| `TradeCatalogue` | the fifteen trades: the game each is played in, the kinds of site and the stages it is on, what it asks (a course, or points of a skill), what it teaches, its bar; the roles a construction company gives its people (game-free) |
| `TradeRules` | the timing bar (the marker's sweep, a crane's swing, the zone and how a press is graded), the match against the clock, the survey's corners, the inspection's checklist, readings and bribe, the night watch, a unit's pay by its quality, what a need holds against (game-free) |
| `DesignRules` | the architecture game: the brief, the floor plan's places and its four scores, the facade's satisfaction, the frame's spans and cheapest frame, the design's total (`DesignScore`) (game-free) |
| `TenderRules` | the estimator's tender: five items, the true rate among three, the margin, the rival's bid (game-free) |
| `DesignBook` | the trades' line in the save: the last eight designs, the builders' briefs waiting, the office's own count, a bribe that may come out (game-free) |
| `TradesBridges` | what the trades ask of Careers (courses, skills, learning) and civic life (the works sites, an unsafe site reported), and what a construction company asks of them (`Qualified`, `Commission`, `DesignOf`) |
| `TradesDirector` | the one component (no Update, a one-second timer): the practice's floor asked and let go, its panel over the desk, the save, a bribe coming out, the career's doors (`ICareerWorkplace`), the tests |
| `TradeOffice` | the practice: a `FloorPlace` of the floors core, furnished as a city office, its desk a few steps in from the lift |
| `SiteWork` | "Work here" in the site notice's conversation: the site's trades one at a time, a rota shift clocked on to |
| `TradesGame` | the address as the game writes it |
| `TradesGuide` | the feature list's entry, its options, the Guide's facts |
| `Jobs/Kinds/TradeJob` (.Timing, .Match, .Judge, .Walk, .Modes) | a site trade's shift: the stand, the hi-vis, the panel and the bar, the clock |
| `Jobs/Kinds/DesignJob` (.List) | the architect's, draughtsperson's and engineer's shift at the practice's desk, in the game's list window |
| `Jobs/Kinds/TenderJob` | the estimator's shift |

## How it talks to other features

- **Jobs** run every trade's shift (`JobDirector.Create`): the trades are `JobCatalogue.Trades`, kept in `JobState` with the gigs (`JobCatalogue.Recorded`), never on the Gig Line, so the day's busy and shut gigs stay the seven. An office trade (`JobNeed.Office`) is the one job taken indoors, on the practice's floor (`JobDirector.AtOffice`); `DesignJob` and `TenderJob` work inside it (`WorksInside`). `Job.Frame` is a per-frame hook, used only for a trade's marker on the bar.
- **Careers** give the courses (`Careers/Study/StudyCatalogue`: bricklaying, the crane ticket, construction management, the architect's licence; the skill `Skills.Masonry`), the player's points and what work teaches (`TradesBridges.Passed`, `Points`, `Learn`, wired in `Wiring/Wiring.TradesLinks.cs`). The professions' architect (`Careers/Work/ProCatalogue`) asks the licence from its second rung.
- **The career ladders** (`Jobs/CareerCatalogue`): three employers with no help-wanted board, reached by the Work contact. `CareerRank.JobId` gives a rank its own trade, `CareerRank.Needs` names a role whose need must be held to sign on at it or be promoted to it (`Jobs/CareerLinks.ICareerWorkplace.Lacks`, the trades' `TradesDirector`); `ICareerWorkplace.Depot` is the employer's place: the nearest works site, or the practice's desk.
- **Civic life** has the works sites (`Civic/CivicWorks`, read through `TradesBridges.Nearest` and `Site`) and the site notice's conversation (`Civic/CityHallPhone`), whose page has "Work here" when `CivicBridges.SiteWork` is set. An unsafe site an inspector fails goes through the notice's own report (`CivicWorks.ReportUnsafe`).
- **Floors** hold the practice (`FloorsDirector.Ask` and `Release`, a guest place: the towers' or the university's spare floor, or a building taken for places).
- **The construction company** (Scripts/Construction, feature/builders) hires the trades' roles and reads the player's designs; see below.

## What players see

### At a works site

The site's notice board (civic life's, at a rebuild, a scaffold, a refit and the other kinds that set one out) has the game's own key; its conversation's first page now has **Work here**. It offers the trades the site has on at its stage, one at a time, as the Gig Line offers gigs: what the work is, the pay for each unit done well, the units to a shift, the player's level, and why not if not (what is asked). **I'll take it** starts it where the player stands, facing the site, in the city's hi-vis (the painter in the painters' colours), in the trade's working pose. A shift due today on the player's rota at the builder's or the city's building control is offered first (**Clock on**).

| trade | site kinds | stages | asks | its game | a unit |
| --- | --- | --- | --- | --- | --- |
| Labourer | roadworks, utility, rebuild, plot | all | nothing | timing: dig, mix, carry | a load, 22 |
| Mason | rebuild, plot | 2 to 5 | nothing | timing, eight bricks a stretch | a stretch of wall, 34 |
| Scaffolder | scaffold, rebuild, plot | all | nothing | timing, six couplers | a lift tied in, 30 |
| Painter | scaffold, refit, rebuild | 2 to 5 | nothing | timing, six strokes | a panel, 22 |
| Roofer | rebuild, plot | 4 to 5 | the carpenter's or bricklaying diploma, or 20 joinery | timing | a run of tiles, 46 |
| Electrician | refit, utility, rebuild, plot | 2 to 5 | the electrician's diploma, or 35 electrical | match: brown, blue, green and yellow to live, neutral, earth | a circuit, 52 |
| Plumber | refit, utility, rebuild, plot | 2 to 5 | the plumber's diploma, or 35 plumbing | match: a corner, a branch, a join to elbow, tee, coupler | a run of pipe, 50 |
| Crane operator | plot | 2 to 5 | the crane ticket | timing, the load swinging | a load, 40 |
| Building inspector | every kind | all | construction management, civil engineering or architecture | judge: eight items sound or a fault, and the bribe | an item judged right, 55 |
| Surveyor | plot, rebuild | 1 to 2 | civil engineering or construction management | walk to each corner, then timing (the level) | a corner, 110 |
| Night security | roadworks, scaffold, rebuild, plot | after hours | the security licence, or 30 guarding | watch: a noise at a post, got to in time | a round, 46 |

Every unit pays its figure (times the level, the job pay option and the owners' factor, as every job) by how well it went: `TradeRules.UnitPay`, two fifths of it done badly up to all of it done perfectly; a whole shift at the best quality earns a tenth more. Four game hours a shift, as every gig: a trade that asks nothing pays about what a gig on foot does (44 to 51 an hour), one after a short course or a diploma 70 to 80, one after a degree about 110 (`JobCatalogue.Trades`, checked by `TradePayChecks`).

- **The timing bar.** The shift card's last two lines are the game's own bar (`ShiftCard.Bar`, `GameBar.Zone` and `Mark`): a green zone drawn for each stroke (its better middle in full green), a white marker running across and back once a sweep (the crane's load as a pendulum, eased at each end). The trade's button on the game's panel (its interact key, or a click) presses it: in the middle best (1), in the zone fair (0.7), outside a miss: the same stroke again (worth at most 0.5), and after two misses it is laid as it lies (nothing). The trade's helping skill widens the zone (0.6 % of the bar a point, 45 % at most). The marker is written in the frame (`Job.Frame`), one number when it moves a visible step.
- **The match.** The ask is on the panel's first line and the card ("The brown wire: which terminal?"), the three answers on the panel's three keys; the seconds left on the card and the panel. Right in under half the time is best, right fair, wrong or late again. Each pick is a little shorter than the last (never under 1.6 s); the skill gives more time. "More" turns the panel to Let time pass and Step away.
- **The survey.** The game's job marker goes to each corner of the land (a plot's own corners; at a rebuild 12 by 6 m of the building's front); within 1.4 m of the peg **Set up the level** holds the player there in the surveyor's pose and the bar's reading is pressed as in the timing.
- **The inspection.** The site's checklist is drawn from the site and the day (the same for anyone who looks that day; an unsafe site has three faults at least): each item a reading against its limit ("Guard rail height: 92 cm, limit 95 cm", "at least"), **Sound** or **Fault**. An item called right pays; a fault called right is found. With faults found the contractor may offer an envelope (45 % of the time; always in the test): two and a half shifts' pay to sign it off. **Refuse**, or no offer, and the site fails: an unsafe site is reported to the city through the notice's own report and stops for a working day. **Take the money** and it may come out three days later (30 %, drawn from the site and the day): a fine of twice it, and City Building Control lets its inspector go with the reference marked down.
- **The night watch.** After the site's hours, six rounds of 30 seconds; on a round with a noise (45 %) a notification says where (the timber stack, the block stack, the cabin, the skip), the marker goes there, and **Challenge them** within 2.5 m before 28 seconds catches the thief. A round with a thief caught pays double, one got away half. Nobody is put on the street: the noise and the catch are said in the game's notifications.
- **Let time pass** (every site trade but the inspector's): the rest of the shift by the game's clock (`ShiftClock`, `ShiftSkip`), each unit at a fair unit's pay (0.7) and the clock's 85 %. **Step away** lets the player go from the stand; walking more than 32 m off ends the shift.
- **Learning on the job.** Each unit done by hand teaches its trade's skill (a labourer's: a quarter of a point of masonry), the clock's half, through Careers at the shift's end.

### At the architects' practice

Vale and Hudson Architects asks the floors core for a floor of a building the mod holds (a tower's or the university's spare floor; with none, the building the core takes for places): never a building of its own. The building's door panel has its floor; the floor is furnished as a city office with the game's office designs first (`FitKind.CivicOffice`, graphic designer, law firm, headquarters). Its desk is 3.6 m in from where the player comes in (the lift is there), on the walkable floor once the furniture stands; within 2.6 m the game's panel over it has four buttons: **Draw up a design** (with the architecture degree; else **Draughting work**), **Engineer a frame**, **Price a tender**, and **Clock on** for the practice's rota shift due today or **Speak to the partner** (the Work contact face to face: signing on, a review). The player stands at the desk in the `desk_plan` pose.

- **The floor plan.** A typical floor of three rows of four 4.5 m bays (the front and the back have windows, the middle none). The brief (the list window's top: the client, the storeys, the flats a floor, a lift wanted) gives the rooms in order: the stairs, a lift (four storeys or more, or a luxury client), the lobby (on the ground floor, under a front bay), then the flats from the largest (a family flat three bays in a row, a two-bed two, a studio one). The game's list window has an entry for each place the next room can go, with what it gives ("daylight, steps to the stairs or the lift: 1") and **Put it here**; **Leave it out** for a flat, **Take one back**. The plan as it stands is a note at the top. Scored (`DesignRules.Score`): **fit** (the brief's flat bays placed; no stairs is 30 at most), **light** (the flats' bays with windows), **flow** (each flat's steps to a core: 1 at one step, 0.7, 0.45, 0.2; the lobby's to a core above it, a fifth), **cost** (100 within the budget, four points a per cent over: rooms, the cores, a corridor for each step past one, the lobby, by the storeys). The plan is 35 % fit, 25 % light, 20 % flow, 20 % cost.
- **The facade.** Four elements (walls, windows, the roof line, the entrance), four choices each, each with its cost (the walls by the storey); the client's taste for each (by the kind of client, with one thing they love and one they hate, said in the list). **Change** in the list turns an element to its next choice; the client's satisfaction stands on the shift card's bar (`GameBar.Fill`) as the choices change, less for a facade over its budget. **Show the client** judges it against the best there could be (all 256 tried).
- **The frame** (an engineer's shift, or an architect who also holds civil engineering or the chartered engineer's certificate): thirteen column lines across the 18 m front every 1.5 m, the two ends always standing; **Put a column** or **Take it away** on each, and **Change** the slab (thin 6 m, thick 9 m, post-tensioned 12 m). The card says the longest span against what the slab carries. **Check the frame**: nothing if a span is too long; else 100 at the cheapest frame that holds (found by trying every frame), less by its cost over that, and twelve points for each column in the middle of a bay (in a room's way).
- **A shift.** An architect's shift is one commission: the plan, the facade and (for an engineer) the frame, each stage paid 160 by its quality, 75 seconds for the plan (90 with the lobby), 60 for the others; out of time the stage is judged as it stands. A draughtsperson's is three sheets: one floor, the stairs drawn already, three flats, scored on the same rules (60 a sheet). An engineer's is two frames (220 each). A design is a decision: there is no Let time pass.
- **The tender** (the estimator): three tenders, each five items (groundworks, brickwork, steel and so on), each with what was paid last and how the market has moved since; the panel's three buttons are three rates, the true one (the last moved by the market, to the half unit) at a drawn place among them. Then a margin (4, 8 or 12 %). Won if under the rival builder's bid (5 to 15 % over the true cost), paid by how true the rates were (an item under its cost 0.5, over it 0.6), a fifth less if lost, and a bonus for a tender won at a profit. 70 seconds a tender.
- **Without the list window** (the game's could not be copied): the panel's fourth button puts each room at its best place, or a column on every wall; one line says so.

### Courses and careers

| course | where | what | opens |
| --- | --- | --- | --- |
| Bricklaying diploma | the school, Tue Thu Sat 13:00, six classes | masonry 45 | roofer; the mason's rank |
| Crane operator's ticket | the school, Mon Thu 13:00, three classes | handling 25 | crane operator |
| Construction management | the university, Mon Tue Thu 14:00, ten classes | delivery 35, planning 25 | inspector, surveyor, estimator, foreman, site manager |
| Architect's licence | studied at home, an exam at the school, after the architecture degree | planning 12, structures 8 | the practice's architect and partner; the professions' architect from the second rung |

The electrician's, plumber's and carpenter's diplomas, civil engineering, architecture, the chartered engineer's certificate, the security licence and bookkeeping already in the catalogue open their trades too. Construction management is the university's last course in person (floor 4, room 3), so every course before it keeps its room.

| employer | rank | basic an hour | its work | asks |
| --- | --- | --- | --- | --- |
| Hudson Brick and Build | Labourer | 11 | labourer | nothing |
| | Mason | 14 | mason | the bricklaying diploma, or 20 masonry |
| | Foreman | 36 | a shift in charge (the jobs' lead) at the site | 35 masonry, or construction management |
| | Site manager | 52 | a shift in charge, +20 % | construction management or civil engineering |
| City Building Control | Building inspector | 18 | inspector | a degree in building (to sign on) |
| | Senior inspector | 28 | inspector, +20 % | the same |
| | Chief inspector | 60 | a shift in charge, +20 % | the same |
| Vale and Hudson Architects | Draughtsperson | 11 | draughting | nothing |
| | Junior architect | 20 | architect | the architecture degree |
| | Architect | 30 | architect, +25 % | the licence |
| | Partner | 45 | architect, +50 % | the licence |

The rota, clocking on, reviews (every three shifts), raises and notice are the career's own ([Careers](careers.md)). A review that would promote the player to a rank they do not yet qualify for holds the promotion back and says what is missing (`reallife_career_review_held`); a raise is still given. **The apprenticeship**: a labourer's every unit teaches a quarter of a point of masonry, so about ten shifts make the mason's 20 points, and the labourer is promoted at the next good review.

## With the construction company (Scripts/Construction, feature/builders)

Agreed with the builders' side; neither branch names the other's types. Its `ConstructionBridges` has three delegates with safe defaults, filled in Wiring once both are merged:

- **Roles.** The company gives the game's own employees one of `TradeCatalogue.Roles`' ids: labourer, mason, foreman, site_manager, architect, engineer, crane_operator, electrician, plumber, estimator (and the rest: draughtsperson, licensed_architect, inspector, surveyor, security, painter, scaffolder, roofer, mason_rank). Stable strings: a saved role reads the same on both sides. `TradesBridges.Qualified(roleId)` says whether the player holds what the role asks; the company asks it for "architect" before the player designs their own project, and "engineer" for the engineer's sign off.
- **A design.** `TradesBridges.Commission(projectId, storeys, units, budget)` puts a brief for the company's project at the practice (`projectId` "build:<n>"; the same project asked again replaces its brief; a brief from `DesignRules.ForProject`, the same on every machine). When the player finishes it, `TradesBridges.DesignOf(projectId, out DesignScore)` answers (false until then) with:

```
DesignScore   Fit, Light, Cost, Flow   0 to 100, the floor plan
              Facade                   0 to 100, the client's satisfaction; -1 not designed
              Structure                0 to 100; -1 not engineered; 0 when the floors would not hold
              Total                    0 to 100: the plan 60 %, the facade 25 %, the frame 15 %, over the parts done
              Storeys, Units           the building and its flats
              CostPermille             thousandths of the whole budget (plan and facade) the design comes to
line          d1|fit|light|cost|flow|facade|structure|total|storeys|units|costpermille
```

The company reads Total, CostPermille, Storeys and Units. Until the company's branch merges nothing calls `Commission`; the practice draws its own briefs ("own:<n>").

## Going Public

- **Deterministic.** A site's checklist, its readings, a bribe offered and coming out, a project's brief and the office's are drawn from seeds of the site, the day and the project's id (`TradeDice`, `DesignRules.Hash`, never `string.GetHashCode` or an unseeded random), so every machine gives the same. A shift's own strokes and noises are the player's own play.
- **Nothing new sent.** The trades send nothing. The one change to shared state is an unsafe site failed by the inspector, through the notice's own report (`CivicWorks.ReportUnsafe`): a guest's report is its existing request to the host (`Share("u|", ...)`), which the host decides, as when a player reports a site at its notice.
- **Other players' things.** None is touched: the works sites are the city's; the practice is a place on a floor (every floor gives the building's indoor tag, [Floors](floors.md#going-public)); the trades' line is in this player's save.
- The site trades happen out of doors, where Going Public shows each player to the others as the game draws them; the uniform is drawn over the player's own clothes and never saved (`JobUniform`).

## Self-check lines (Developer diagnostics)

- `Trades check (site work): site 4 (kind 4, stage 3, working) has 6 trade(s) on: PASS` at the notice's Work here.
- `Trades check (bar): the game's bar on the shift card, for the mason's timing: PASS` (or FAIL: the zone in words on the card).
- `Trades check (stroke): 'mason' pressed at 0.52 against the zone 0.41 to 0.65: Best after 0 miss(es), worth 1.00: PASS` for each stroke; `Trades check (pick): ...` for each pick.
- `Trades check (learnt on the job): 8 unit(s) of 'labourer' taught skill 43 2000 thousandths: PASS`.
- `Trades check (practice desk): on floor 7 of ..., 3.5 m from the lift on the walkable floor: PASS`; `Trades check (practice panel): the game's own panel over the practice's desk offers ...: PASS`.
- `Trades check (design list): the game's list is open for the Plan, ... entries made so far: PASS`; `Trades check (satisfaction meter): the client's satisfaction on the game's bar on the shift card: PASS`; `Trades check (design score): 'architect' Plan judged 92 (quality 0.92), paid 147: PASS`; `Trades check (design book): own:3 finished: d1|...: PASS`.
- `Trades check (tender item): ...`, `Trades check (tender): bid ... against the rival's ... and the true cost ...: won, quality 0.90, paid ...: PASS`.
- `Trades check (commission): a brief for project 'build:2' is at the practice, 1 waiting: PASS`; `Trades check (bribe): the bribe of day 30 came out: fined twice it (1000), ...: PASS`.
- Always: `Jobs: trade 'mason' (Timing) begun at site 4 (kind 4, stage 3, ...)`, each unit's `Jobs: trade 'mason' unit 2 of 6 done by hand at quality 0.85, paid 29.`, the inspection's items and verdict, the watch's rounds.

## Tests (Options > Tests, with "Show test tools" on)

Each works when pressed or says why not in a notice, and writes its check line.

- **Trades: a mason's shift here now** (and an electrician's, a crane operator's, an inspection with the bribe, a survey, a night watch): the shift where the player stands, with no site needed, whatever they hold; nothing counts towards a level or a record.
- **Trades: the work at the nearest site**: the site's Work here pages face to face (a site within 80 m; the construction tests make one).
- **Trades: take me to the architects' practice**: by the building's own door to the practice's floor.
- **Trades: a design at the practice's desk**, **a tender at the practice's desk**: on the practice's floor, the shift at once.
- **Trades: a builder's brief at the practice**: a brief as the construction company's projects will send one ("test:<n>", five storeys, eighteen flats).
- **Trades: a bribe taken comes out today**: a bribe of three days ago at a site whose bribe comes out, settled at the next second.
- **Trades: twenty points of masonry learnt**: the apprenticeship's points at once; the mason's rank is open.

## Offline checks

EconomyCheck's `BuildTradesChecks.cs` (`--build-trades` alone): every trade a job of the trades and recorded, none on the Gig Line, what each asks a course the city teaches, every kind of site with work in some stage, night security only after hours, the construction company's ten roles; the four courses, masonry the last skill, the trades' courses the catalogue's last; the three ladders (each rank's work a job, each need a role, labourer and draughtsperson open to anyone, the apprenticeship within forty shifts, the professions' architect asking the licence); the bar's marker and swing, the zone always whole on the bar, the grades and worths, a unit's pay and the shift's bonus; the match's times and grades and fair asks; the survey's corners; the checklist the same on every look, no item twice, every reading saying whether it is at fault, an unsafe site's faults, the bribe's chances; the watch's noises; the design's briefs the same from a seed and a project, a good plan's scores, a dark plan, a flat left out, no stairs, the cost score, the places, the draughtsperson's sheet, the facade's satisfaction and its best, the budget, the taste, the frame's spans, the cheapest frame scoring 100, a column in a room, the total's weights, the score's line read back and bad lines refused; the tender's draws, the true rate among the three, the bid, the rival, the quality; the book's line read back, a brief replaced, the last eight kept, the bribe's days, a newer or damaged line refused; the trades' pay against the gigs' and the professions'; over the sources, no LINQ, no whole-scene search, no Update, nothing sent, the rules game-free, the guard's timer, the practice a floor of the floors core, the save, the bribe by the rule, only an unsafe site reported, the marker in the frame, the game's clothes, every way out, the director's jobs, the office trade indoors, Work here, the wiring, the options, the city table, the guard and registration, the check lines; and every text in en, pt and zh-cn, in the style, the notices short, the Guide's facts. CareersChecks and CampusChecks count the new courses.

## Clips

The trades play the animation bundle's clips (CityAssets): `shovel_dig`, `hammer_nail`, `paint_roll`, `kneel_work`, `inspect_look`, `clipboard_write`, `radio_talk`, `point_direct`, `map_read`, `guard_stand`, `arms_crossed`, `desk_plan` and `take_notes`, each the working pose at the stand while the trade's game runs. The mason asks for `brick_lay` first (standing at a wall course, placing a block and tapping it level, no tool), falling back to `hammer_nail` while a bundle lacks it (`ClipLibrary.Has`).

## Not verified without the game

Everything seen: how the bar reads with its zone on the card while the marker runs, that the game's panel's buttons fit the trades' words, the list window's layout with twelve places and the plan note, the practice's desk spot on a fitted floor and its panel, the stand at a notice board's reader spot, the night watch's posts on a real site, the crane game at a plot (plots come from the construction tests and, later, the builders' and Uptown's ground), and the pay in play.
