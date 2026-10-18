# Careers

## In a sentence

Two things share the name: the six employers behind the help-wanted boards, whose ranks turn the gigs into a career, and the city's education and professions (courses, the university, twenty-five professions, working for yourself).

## Where the code is

- The employers and ranks: the `Career*.cs` files and `DispatchList.cs` in [`Scripts/Jobs`](../../Assets/Mods/RealLifeOverhaul/Scripts/Jobs/), with the jobs they grow out of ([Jobs](jobs.md)). Read `CareerCatalogue.cs` (every figure of the ladder) and `CareerDirector.cs` first.
- Education and the professions: [`Scripts/Careers`](../../Assets/Mods/RealLifeOverhaul/Scripts/Careers/), with `Study/` and `Work/`. Read `CareersDirector.cs` (the one component, in partial files) and `Study/StudyCatalogue.cs` first.

What `Scripts/Careers` holds: education and careers: the city's courses and the player's own skills, the professions at the city's employers, working life, working for oneself, the university, the family's schooling and an academy of the player's own.

| File | What it is |
| --- | --- |
| `Study/StudyCatalogue` | every course (short, diploma, degree, after a degree, certificate), the timetable, exams, grades, fees, grants, the student loan, learning and fading (game-free: EconomyCheck runs it) |
| `Study/StudyRules` (and .Learning, .Fees) | the rules of studying: classes joined and missed, homework, the exam's mark, retakes, fees, the loan, skills, a child's degree, tutoring, an academy's students (game-free) |
| `Work/ProCatalogue` | twenty-five professions, each a ladder of ranks on the professions' roles and the player's skills (game-free) |
| `Work/ProRules` | what a rank asks, an employer's score, the interview, the offer and haggling, the working day, notice, references, the city's vacancies (game-free) |
| `Work/LifeRules` | reviews, rises, bonuses, promotion, burnout, office matters, search firms, redundancy, benefit, options, the pension, the top seats of a rival's company, a conflict of interest (game-free) |
| `Work/OwnRules` | founding a company or a practice, a founder's record, restraint clauses and claims, freelance contracts (game-free) |
| `CareersModel, Work/WorkModel, CareersStore, Work/WorkStore` | the save's records and its one line (game-free) |
| `CareersDirector` | the one component (no Update): a one-second timer, the game's hour and day, the parts in partial files (.Study and .Coursework; .Work, .Vacancies and .Applying; .Desk and .DeskDay; .ClassPlay, .Prompt and .Floors; .Life, .Own, .Degrees, .Family, .Tests) |
| `Sitting` | a class, homework, an exam, a working day, a contract, tutoring or a ceremony left to the game's own activity panel and clock (Jobs/ShiftSkip) |
| `CampusApp, CareerApp` | the Campus and Career apps in the game's own frame (GameUi.AppShell) |
| `CampusBuilding` | Hudson City University: the largest office building of the city, with its sign |
| `AcademyType` | the player's own training academy, a business type of the game's (registered at the start, stripped for removal) |
| `CareersBridges` | what it asks of the others (Wiring/CareersLinks fills them in) |

## How it talks to other features

- The employers' optional doors are in `Jobs/CareerLinks.cs`. [`Wiring/LifeLinks.cs`](../reference/wiring.md) fills in the record on a CV and the sick note from Home; nobody fills in the social life's acquaintances (the social cast is a fixed eight).
- Education and the professions ask the others through `Careers/CareersBridges.cs`, filled in by `Wiring/CareersLinks.cs`: the home's internet for online classes (Utilities), a driving refresher (Licences), Standing, the city's economy for vacancies, the record, the student loan on the credit file, the council's grants, restraint clause claims as civil suits (Justice) and the tech sector's labour market.
- A shift in progress at a save is carried by the jobs' own line: see [A shift across a save](jobs.md#a-shift-across-a-save-jobsave-payslip).

## Employers behind the help-wanted boards

Six employers stand behind the help-wanted boards: each board's building is one employer's office (matched by the kind of service building, best fit first: `CareerCatalogue.Premises`; the boards are the same in every game, so the employers are too).

Walking up to a board that is an office, the key speaks to its boss, who stands by it (the careers' own cast of two: `JobCast` with a seeded look, so the same face every time; put out only when it cannot be seen from close by, shelved when the player is 100 m off).

On a day the player is on the rota there, whoever is on the same shift waits on the other side of the board and waves when the player walks up.
With no board in the bundle, or fewer than six, the employers without an office are spoken to through the **Work** contact on the phone.

A shift's time is never said as a bare hour.
`Jobs/CareerClock` is the one formatter: `Span(hour)` gives the start and the end in the game's clock format ("13:00 to 17:00", or with AM and PM under the game's 12 hour option), and `SpanWithWindow` adds when clocking on opens (`EarlyHours` before) and from when it is late (`OnTimeHours` after).

The boss's rota offer, the Work contact's "next shift", its too-early refusal, the reminders and the clocked-on line all pass it as `{time}`; the shift's panel shows it beside the employer's name; the Planner's rows carry the same two times as minutes and format them with its own `PlannerText.Time`.

At an office board the game's panel over a place offers "Speak to the office" on the game's interact key (`JobDirector.Boards`); the mod reads no key there and draws no prompt.

| employer | grows out of | references needed | ranks (what the shift is) |
|---|---|---|---|
| Brightline Promotions (Tasha Greene) | flyers | 0 | Leafleter (the flyer round) -> Street team lead (a shift in charge) |
| Brightside Contract Cleaning (Dolores Whitlock) | cleaner | 0 | Cleaner -> Crew lead (in charge) |
| Uncle Lou's Street Eats (Lou Ferrante) | drinks cart | 0 | Cart vendor -> Licensed cart owner (the cart is rented by the day, 165, and the takings are kept: 30 a customer less 9.50 stock) |
| Fairway Markets (Anand Rao) | shelf stocker | 1 | Stockroom assistant -> Shift lead -> Assistant manager (both in charge) |
| Hudson Parcel Co. (Marge Kowalski) | courier | 1 | Courier -> Senior courier (7 parcels, the firm's van free, +10 %) -> Dispatcher (the planning desk) -> Depot supervisor (in charge) |
| Five Borough Cabs (Sal DeMarco) | ride-hail | 2 | Cab driver (4 fares a shift) -> Premium driver (a Luxury-kind car by `Driving/CarCatalogue`, passengers mind 30 % sooner, fares +25 %, tips x2) -> Owner-operator (fares +45 %) |

The game has no taxi business type (taxis are transport the player buys), so the cab ladder ends at owner-operator and has no hook into a business.

The game's one employer job (the cashier) is left exactly as it is: an hour worked at it is counted, read-only, once an hour (`JobHelper.IsPlayerWorking` outside the player's own shops), shown on the CV, and every eight such hours count as one shift of experience when signing on with the retail chain.

- **Signing on** is face to face at the office or by phone (Work contact, "Apply for a job": the six employers in turn, the same page as the boss at the office, so the same checks and sign-on).
  The boss says what the work is in their own manner, then offers the three rotas in turn (Mon/Wed/Fri 08:00, Tue/Thu/Sat 13:00, Sat/Sun 10:00).

  References are counted as: +1 for the Gig Line's reference letter, +1 for each employer left on good terms, -1 for each left with a marked-down reference.
  Experience carries over: levels in the employer's own gig (two shifts each above the first) and in a related gig (one each) are credited towards the first review, never the whole of it.

- **The rota.** A shift is one run of the rank's work, clocked on to from the phone (Work) or at the office, from two hours before its hour.
  Up to an hour after is on time; up to three hours after is late (a mark, and an hour's basic docked); after that it is a no-show (a mark).
  The phone reminds three hours before.

  Three marks and the employer lets the player go with the reference marked down and a week's ban.
  Desk and lead shifts are clocked on to within 45 m of the office.

- **Sick.** "I'm not well today" is accepted, and a missed shift is excused, if `CareerLinks.Health` says a sick note covers the day, or with no health feature if the mod's own sickness (`ModState.IsSick`) is on.
- **Pay.** What the work itself pays (piece pay, by the rank's factor) plus the rank's basic wage for each game hour of the shift, four at most, paid when the shift ends well.
  The payslip lists both and a tax line that reads zero: the game takes no tax from wages (its cashier job pays hours x rate in full; `TaxHelper` taxes business sales and property only).
- **Reviews** are due every three rota shifts and are held with the boss.

  The numbers read out are the ones the outcome is decided from (`CareerCatalogue.ReviewScore`): 20 + 60 x the share of shifts on time (late counts 0.4), -12 a complaint (a failed shift), -6 a crash, -10 a police stop on the job, plus a point per dollar of tips a shift up to 20. 70 or more is a promotion, 55 a raise (+3 an hour, three at most per rank), under 35 a mark.

- **Leaving.** Notice is two more shifts (or seven days) and ends with a good reference if three shifts were worked and fewer than two marks stand; walking out marks the reference down and bans for a week.
- **The CV** is two pages of the Work contact's call: employer and rank, employment history (eight spells kept) with references, shifts and earnings; then gig levels, the reference letter, cashier hours and the record (police stops on a job; a law feature's line if `CareerLinks.Law` is set).
- **Booked sets.** From busker level 2 the Work contact books one set a day at an open business nearby: it is marked on the map, begins when the player walks up to its front, pays 125 from the venue on top of the hat for a set at least half played, and tips are 30 % better.

| rank | basic / h | the work pays | about, per game hour |
|---|---|---|---|
| Leafleter, Cleaner | 11 | as the gig | 51 to 53 |
| Stockroom assistant, Cart vendor | 14 | as the gig | 63 to 70 |
| Courier | 17 | as the gig | 95 to 100 |
| Senior courier | 25 | 7 parcels, firm's van, +10 % | 130 to 150 |
| Dispatcher | 40 | 42 a plan x (0.25 + 0.75 x quality), 3 plans | 65 to 72 (no car, no risk) |
| Street team lead, Crew lead | 36 | 36 a call made well, 4 calls | 72 to 76 |
| Shift lead | 40 | the same | 72 to 76 |
| Assistant manager | 52 | the same, +20 % | 90 to 95 |
| Depot supervisor | 60 | the same, +20 % | 100 to 105 |
| Cab driver | 14 | 4 fares, as the gig | 110 to 120 (a fare is about 80 s of driving) |
| Premium driver | 22 | fares +25 %, tips x2 | 170 to 190 (needs a Luxury car) |
| Owner-operator | 0 | fares +45 %, tips x2 | 190 to 200 |
| Licensed cart owner | 0 | takings of 30 a customer less 9.50 stock, less 165 a day for the cart | 80 to 125 a shift |

Set against the game: cashier 25 an hour, its own staff 12 to 18, the van job about 100.
A rota shift is worth about a fifth to a third more than the same work freelance.
The courier and cab ranks come to about the van job's 100 an hour of play, and the top two cab ranks to about twice it (they need a car costing tens of thousands).

The per-hour column is worked out from the catalogue's figures, not measured in play.
**Owners are paid less:** every job's pay, freelance or employed, is multiplied by `CareerCatalogue.OwnerFactor`: 1 with no business, 0.85 with one, 0.70 with two, 0.55 from three on.

**The dispatcher's desk** (`DispatchJob`, `DispatchList`): three batches of six parcels round the depot; each parcel goes on one of three vans (three a van) by that van's button on the game's panel over the desk (`JobPanel`, run on the game's keys or a click), 55 s a batch; its Take one back button takes the last one back.

The board is the game's list window (`GameUi.DeskList`): the parcels still waiting as a note at the top (how far and which way from the depot), then an entry for each van with what it carries and how long its round is, and a button to send the next parcel on it, and an entry to take one back.
The same buttons stay on the game's panel over the desk.

The window opens with each batch; the cancel key closes it for that batch.
If the game's list window cannot be copied the panel is all there is (`Jobs check (dispatch list) ... FAIL`).
The mod's own map is gone; nothing in this has been seen in the game.

A plan's cost is the driving (nearest parcel first, then home) plus a fixed amount per van sent out; the best and worst of the 729 possible loadings are found by trying them all, and the plan is paid by where it falls between them.
Two poor plans (under 0.4) are a bad shift.

**A shift in charge** (`LeadJob`): two of the team stand near the office doing what they are doing: writing up (`take_notes`), smoking (`smoke`) or stuck (`arms_crossed`).
The lead walks up to each and makes the call that fits (praise, a word, a hand), then decides two incidents from a deck of six, each a choice of three with one good, one passable and one bad answer.
Calls made well are the units paid and counted.

**Optional doors** (`Jobs/CareerLinks.cs`; each a static that is null until somebody sets it, every use wrapped):

| the career offers / asks | interface | used for |
|---|---|---|
| offers | `ICareerAcquaintances.Met(personId, nameKey, employerNameKey, isBoss)` via `CareerLinks.Social` | a social feature picking up the boss and the two colleagues as acquaintances |
| asks | `ICareerRecord { bool Clean; string Summary; }` via `CareerLinks.Law` | the record line on the CV |
| asks | `ICareerSickNote.Covers(day)` via `CareerLinks.Health` | sick notes; without it `ModState.IsSick` |
| offers | `CareerLinks.EmployerId`, `TitleKey`, `ShiftDueToday(day)` | anybody who wants to know where the player works |

State: one line, `reallife:career`, in modData (`CareerState`).
A shift in progress is carried by the jobs' own line (`reallife:job_live`, see [A shift across a save](jobs.md#a-shift-across-a-save-jobsave-payslip)): a load pays the basic wage for the hours clocked and counts the shift.

Test buttons (options, Jobs): sign on with the next employer (cycles all six, on a rota that works tomorrow); work a shift at my current rank now; my review is due now (opens the review face to face); promote me one rank; start a dispatcher shift; start a shift in charge; book a set near here.
Test shifts go on no record.

Wish-list for the asset pipelines.
The clips are already asked for by id, before today's stand-in, so they drop in with no code change; the props would each need a line of placement code and are listed as wishes only:

| id | what | used by |
|---|---|---|
| clip `desk_plan` | standing at a waist-high desk, moving markers on a board, 6 s loop, upper body | `DispatchJob` (today `clipboard_write` over the upper body) |

| clip `staff_struggle` | hands on hips, a shake of the head, looking down at something, 6 s loop | `LeadJob`'s "stuck" (today `arms_crossed`) |
| prop `dispatch_desk` | a lectern-height desk with a wall map, 1.2 x 1.4 x 0.5 m, read from +Z | (needs placement code) stood by the board on a dispatcher shift |
| prop `employer_sign` | a small wall plaque (0.6 x 0.3 m), six variants, one per employer name | (needs placement code) above each office's board |

## Classes, work desks and the computer at home

- **Attend in the Campus app** takes the player to the classroom (the right floor of Hudson City University, or the nearest desk at Manhattan Business School) and starts the game's own study activity at its desk: `SchoolDesk.Begin` presses the class for the player once the panel opens.
- **The class's key.** At the school's desks the class's button is on the game's secondary key (P), beside the game's own Start. In a classroom of the university the panel is the class's alone, and its button is on the game's Start key (Confirm, Enter): the secondary key is the panel's fast-forward switch once the class runs.
- **In a Going Public session.** Going Public takes every `StudyActivity` the game's panel is asked to show (a Harmony prefix on `PlayerActivityUI.Show`) and puts up its own paid training window for the game's course at that desk instead, or "Requires the ... course first". So in a session no class, exam or homework of the mod's goes through the game's study panel: `CareersDirector.OpenDesk` is the one place a desk's panel is opened, and by `ClassPlayRules.WayToDesk` it shows the desk's own activity of the mod's (`SchoolDesk.Own`, the class's panel alone, which is not a `StudyActivity`) in a session, and the game's study panel alone, as before. A click on a desk of the school stays Going Public's (the game's course): inside the school the class or exam is offered by the panel where the player stands (`ClassPlayRules.SchoolInPlace`, `InPlace`), whose button, like the Campus app's Attend, Sit and Homework, walks the player to the nearest desk and sits them there. At the university the game's own "Click to learn" over a classroom door is the mod's in a session (`DeskGame`'s click, `ClassPlayRules.TakesRoomClick`) and opens the room's own panel. With Developer diagnostics on, each desk's panel opened writes one "Careers check (desk panel)" line: in a session or alone, the way taken against the way wanted, PASS or why not. Options > Tests > "Pretend a Going Public session" shows the session's way without Going Public.
- **Online and distance classes and homework** are done at a computer in the player's home. `ComputerButtons` adds **Attend {course}** and **Do {course} homework** beside the game's **Pass the time**; `ComputerSitting` plays them as the game's `EntertainActivity` does. Which button shows is decided by `Study/ClassPlayRules.cs`, which the offline checks test.
- **A working day at the employer's.** The game's **Click to work** over the player's station or fitted desk walks them there and opens the game's work panel (`DeskWork`). On **Start working** they sit on the chair of a desk computer or a fitted desk and type now and then (`DeskGame.SitOn`), and stand at a till, a counter or another station whose people stand, in `WorkActivity`'s own pose (`DeskGame.SitAt`).
- **A working day from home** is begun from the home computer's own panel, at a computer with its chair: `ComputerButtons` adds **Start the working day** beside **Pass the time**, or **Work late** once the day is worked, or the working hours greyed before it and the desk's words greyed after it (too late, worked already). `ComputerSitting` plays the day as it plays an online class, after the desk's own checks and clock-in (`CareersDirector.WorkAtComputer`, `DeskBegin`), except that the player's energy running out does not end it, as it does not at the desk. Which button shows is decided by `Work/DeskRules.AtComputer`, which the offline checks test.
- **Minutes count once.** `ClassPlayRules.ClassTally` settles a sitting exactly once; a walk called off counts nothing, and a retake fee is charged only when the player sits down. A sitting that no desk, computer or panel holds any more is let go within a second (`DeskTick`), so it never leaves every class and day "under way".
- **Standing up.** Every way out of a desk, a station or the computer releases the game's navigation blocker in a `finally` (`DeskGame.StandUp` and `StandFrom`, `SchoolDesk.StandUp`, `ComputerSitting.StandUp`): nothing the game's calls before it throw can leave the player held where they sat.
- **Trying it.** Options > Tests has **Start a working day at my computer at home** (`TestComputerWork`): the rest of a working day begun at the home computer at once, whatever the hour and the job's days from home, with a job given if there is none, as **Start an online class at my computer at home now** does for a class.
- **The home's internet down.** Every activity at the home computer gives its refusal through `StudyRules.NoNetWhy`, from `CareersBridges.HomeInternet` (Utilities: up, or why it was down over a span of minutes). Only a provider's outage excuses (`StudyRules.Excuses`), decided once, when the activity's window has shut: an online class gone by with the outage over its join window is excused (`StudyRules.Excuse`: held, but left out of `Counted`, so out of the attendance share, the bar and the mark; `Enrolment.Excused` and `ExcusedDay`, saved at the end of the course's record, and said in the Campus app); a working day from home not worked with the outage over its start window is excused like a sick day (`ProRules.DayExcused`: no pay, no missed day, no warning, and the boss's text says why); a video interview with the outage over its window moves to the next weekday at the same hour (`ProRules.Reschedule`). No plan, one being put in and one cut for an unpaid bill excuse nothing. Homework and tutoring have no hour, so nothing is lost. The home's power is not part of its internet in the mod, and a storm's power cut is only news. Options > Tests: **Online class and day from home: go by in an outage now**, after Utilities' **Internet: an outage now**; Developer diagnostics write one "Careers check (online class gone by)", "(working day from home gone by)" or "(video interview gone by)" line each.
- **After a game update:** the part `computer_class` (`CustomizableButtonsOverlay.buttonTemplate`) carries the computer's buttons. If it fails, or the buttons stand down after a fault, the home computer loses the mod's buttons: online classes and homework can still be started at home from the Campus app, and a day from home from the Career app.

## Setting off

The game's activity panel drops a walk order it sees too late. Once the panel has seen an activity on its way to its place, `PlayerActivityUI.CancelActivityMovement` (on `PlayerController.PlayerChangedNavigation`) takes every walk order after the end of that frame for a click elsewhere: the activity goes back to "not started" and the panel closes without a word ([Home: Cooking](home.md#cooking) has the same flaw in the home routines).

So an activity of the mod's that walks the player to its place (`SchoolDesk`, `DeskWork`, `ComputerSitting`) sets off in three steps, whatever order the game's scripts run in:

1. The press leaves it "not started" for the rest of its frame; it only notes the press, so a second press does nothing.
2. In the next frame, once every script's `Update` has run, it is "moving towards" its place. The panel first sees that in the frame after.
3. In that frame, after the panel's `Update`, the walk order is given: before the end of the frame, when the panel starts listening.

With Developer diagnostics on, `Player.log` has a "Careers check (set off)" line for each walk (`DeskGame.SetOffSeen`): PASS when it got to the desk or the computer with the panel holding it, FAIL when the panel took the activity's own walk order for a click elsewhere.

Why: the class at a desk and the work desk used to be put on their way in the frame of the press and given their walk order a frame later. Whenever the panel saw them on their way in the frame of the press (which depends on whether the key or the click is read before the panel's `Update`), the walk was dropped: the panel closed and the player stayed where they were.
