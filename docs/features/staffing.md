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

## Not covered

The city's own services (police, courts, hospitals, the school's trusts) are not staffed by the player; the game's own businesses keep the game's hiring.
