# Staff and roles

## In a sentence

An exact, searchable reference of the staff of every business and workplace the mod adds or changes, and of every role: how many of each role a business needs and per what, what each does to the result, where more stops helping, what happens without, how to hire a role (the game's hiring, the Tech labour market, a search firm, a crew search, or by itself) and how to train one.

## Where the code is

- Folder: [`Scripts/Staffing`](../../Assets/Mods/RealLifeOverhaul/Scripts/Staffing/) (the table, the effects, the pages, the lookups) and [`Scripts/Meta`](../../Assets/Mods/RealLifeOverhaul/Scripts/Meta/) (`StaffView`, `StaffApp`, `StaffHelpGame`, `StaffGuide`).
- Read first: `StaffTable.cs` and its parts. Every number in a page is a figure the table read from a catalogue or a rule (`InfraCatalogue`, `FirmsCatalogue`, `TalentCatalogue`, `HiringCatalogue`, `BuildCatalogue`, `OwnBankCatalogue`, `StudyCatalogue` and the rest); the text of a sentence holds none.
- The checks are `EconomyCheck/StaffChecks.cs`.

## What it covers

- **Tech:** the data centre, the cloud, the hyperscaler, the software, game, consultancy, cybersecurity and AI studios, the game's web development agency and graphic design studio, and the tech path's scale-up step.
- **Money and work:** the player's own bank and its branch, the car showroom, the online shop, the cleaning service, the estate agency, the hotel, the construction company, the academy, the broadband company, the company's executives and the three new shops.
- **Roles:** the 37 professional roles of the talent catalogue, and the roles of the table's own (crews a venture takes on by itself, the game's own employees by skill, the construction company's office roles, the hotel's staff levels).

## Where it can be read

- **The Staff and roles app** (a window of the game's help in the mod's dark colours, as the Settings app is): the businesses and the roles each from A to Z, a search box that filters both as you type (any part of a name, capitals and accents ignored, the match in bold, as the game's help does), a page for each with a button to every entry it names, back and forward.
- **The Guide:** a page "Staff and roles" says what it is and lists every entry from A to Z, with a button that opens the window. The Guide's tech pages end with a pointer to it.
- **The game's own Help (F1):** each entry's page is written into the game's language table under the key the help reads, and the two lists are added to the help's categories (`GameContract.HelpCategories`, `HelpLoad`), so F1 lists and searches them like its own pages. Business and role pages link to each other with the help's own mark-down links.
- **The Discord guide and Uncle Fred:** `guidefacts` writes a `staff` part of facts.json from the built mod and the shipped en.json; `guide_build.py` makes a post for each entry (listed in Start here under "Staff and roles, A to Z"), and the bot's export makes a file for each.

## How it keeps to the code

- The data centre's technicians are counted by `InfraRules.TechnicianRate`, the table's own rate; the cloud's, hyperscaler's, firms', construction company's and other figures are constants their rules read (those that were bare numbers in a rule were made constants: `FirmsCatalogue.ProductBoostMost` and its neighbours, `InfraCatalogue.OutageShortExtra`, `BuildCatalogue.TradeCapTenths` and others).
- `StaffEffects` runs `TalentRules` and `FirmsRules` on small made-up firms (a game built by one, two or four programmers; a feature built by three) so "a sensible number" is what the rules give.
- The rivals' adverts ask for the families the tech businesses' staffing names (`StaffIndex.RivalFamilyShares`), so the player's firms and the rivals read the one table.
- Options > Tests has "Check the Staff and roles reference".

## New hires: a role and a seat

A hire at a tech firm needs two things before they work: a role in the mod's files and a shift at a desk in the game's schedule.

- **A role for every professional.** Each of the player's programmers, designers, support staff, lawyers and HR staff has a file (`Scripts/Tech/People/TalentStore.cs`), whatever their number. The bound is 10,000 files, far above the employees any save holds. It was 600: a player's save of some six thousand employees held exactly 600 files for every day of its log, so each hire after the 600th had no role, was left out of every firm's crew and of the Team tab (the Tech app then said nobody worked at the firm), and a data centre's crew taken on the payroll was refused until somebody left. A bound that is ever met is now said once in the log, and the line is read in one pass however many files it holds.
- **A seat for a hire at a tech firm.** The game seats an employee only on a shift, and a hire arrives with none. The Tech part's hour (`TechDirector.Seating.cs`) finds every programmer, graphic designer or customer service hire of the player's tech firms (the five types, the web agency and the graphic designer; a lawyer or an HR manager is left alone) hired in the last 30 days who has no shift at a desk and is not in training, whichever way they were hired: the game's job board, recruitment office or headhunter, or the mod's jobs board or search firm. It gives each a week of shifts at a free desk. Twenty are seated an hour at most, so a backlog is worked off over several hours; a hire for whom no desk is free is tried again each day, and the player is told once, with the reason: the business has no opening hours, no desk they may use, or every desk is taken in the hours it is open.
- **How the week is made** (`Scripts/Tech/Firms/SeatPlan.cs`, game-free, and `SeatShifts.cs`, the one file that touches the game's schedule). Weeks are tried in a fixed order: five days of eight hours, then four of ten, six of seven and so on, the days starting on each weekday, early in the open hours (from the eighth hour where they leave room) and then late. Each is asked of the hire's own demands through the game's own test, `JobDemand.Fulfilled`, on a week holding their shifts alone (days a week, days free, hours not worked, hours a week); a wish to work at an item decides which desk is preferred. The first week that keeps every demand and has a free desk is written, the same desk on every day where one is free; if none can, the one that breaks the least. A week under half the hours wanted is not a seat: the answer is that no desk is free.
- **What it never does.** It adds shifts at hours nobody has; it removes and moves none, starts no auto fill of the game's and aborts one still working on the business before it writes. A hire who has a shift at a desk is left as the player made it. The employee's weekly hours, days and desks are brought up to date and the game's idle task checked, as its own schedule screen does.
- **Size.** The rota planner of the Company app refuses a business of over 64 stations or 120 staff (`docs/features/economy-rota.md`), so a large firm's new hires were never planned, and its managers could not write its rota. The seat rule has no such limit: it works a desk at a time.
- **Rivals (NPC parity).** The rival companies and startups of the tech sector have no individual staff to place: their team is a number of work points a day (`SectorRules.RivalPoints`, `SectorCatalogue.CompanyTeam`) and a rival's job ad is filled from the same seekers as the player's (`HiringRules.Day`), so a rival's hire is placed by construction and loses nobody to a bound. The game's own rival businesses staff their stations themselves, as the game runs them.
- **Option and test.** "Seat new hires at a desk with shifts" (`tech_seat_hires`, on by default, needs the professionals) under the professionals' section; off, the player gives every hire their shifts in BizMan as before. Options > Tests has "Seat my idle tech staff at free desks", which seats every idle professional of the player's tech firms at once, whenever hired (200 at most), and reads each back for a shift at a placed desk. With Developer diagnostics on, `Tech check (seat, ...)` lines say what was seated and what was not.
- **Checks.** EconomyCheck `--tech-staff` (`TechStaffChecks.cs`): a save of 601, 2,000 and 6,100 professionals written and read whole, the seat rule on fixtures (the usual week, shared desks, round the clock, each kind of demand, ranks, why not) and on 300 random businesses, the sources and the texts.

## Not covered

The city's own services (police, courts, hospitals, the school's trusts) are not staffed by the player; the game's own businesses keep the game's hiring.
