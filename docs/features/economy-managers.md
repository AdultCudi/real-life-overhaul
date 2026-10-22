# Economy: managers for your empire

## In a sentence

The **Managers** tab of the Company app: a manager for each business, warehouse and factory, chosen from its own staff, and a regional manager for a district from a headquarters, who make the minor decisions the player gives them inside the player's limits, each one written down with what it changed so it can be undone.

## Where the code is

- Folder: [`Scripts/Economy/Company`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Company/) (`Manager*.cs`, `CompanyDirector.Manager*.cs`, `CompanyApp.Managers.cs`). The files are listed in the code block below.
- Read first: `ManagerRules.cs`, then `CompanyDirector.Managers.cs`.
- Option "Managers for your businesses", id `economy_managers`, under "Real Life Overhaul: Running a business", on by default. It runs while running a business does.

```
Economy/Company/              the first three are game-free and compiled by EconomyCheck
  ManagerRules                  the kinds, the catalogue, a mistake's chance and bound, each kind's decision inside its limit
  ManagerState                  a manager's file (limits, month, stock looks, products left to the player), a decision, the log
  ManagerStore                  the save's one line, "reallife:economy_managers"
  ManagerGame                   everything read from or done to the game: staff, contracts, stock on site, prices, candidates
  CompanyDirector.Managers      the places read, who decides, the hours, the day (allowances, the month's report), the player's buttons
  CompanyDirector.ManagerDecisions   each kind's decision and its undo
  CompanyDirector.ManagerTests  the three test buttons
  CompanyApp.Managers           the tab
  ManagerGuide                  the Guide page's figures, all from the catalogue
```

## What the game and the mod already did, and so what was built

| Minor decision | Already automated | Built here |
| --- | --- | --- |
| Prices | The game's Pricing Manager (an HQ plan per district, `PricingManagerPlan`), and a brand's kept prices | A shop's prices in a district no Pricing Manager covers and no brand keeps, by the game's own arithmetic (`PricingManagerHelper.ComputeSuggestion`) at the manager's skill, inside a price band |
| Deliveries between warehouses and shops, factory inputs | The game's Logistics Manager tops a place up to its targets every morning; a Purchasing Agent imports | Nothing for those products: a product a round tops up is left to it |
| Ordering stock | A repeating Delivery Contract brings fixed amounts every Monday | The week's amounts of each repeating contract set on Sunday to last the week and a stock floor, from the use since the last look |
| Hiring to replace staff | The game's Headhunter replaces those who resign or retire (with an HQ) | A candidate from the place's own job board hired the game's way (`EmployeeHelper.HireCandidate`) when the staff fall below the number to keep |
| Rotas | The mod's rota planner writes a week at a press | Looked at every Sunday evening, and written only when the week has a gap and the new week keeps all the staff on it |
| Wages and raises | The mod's staff part: requests, rivals' offers, overtime | Answered inside a wage band and a monthly budget |
| Insurance claims | The mod's cover part: a claim to put in within five days | Put in when it pays more than it adds to the next term |
| Training, health insurance, cleaning | The game's HR Manager; the game's cleaners and the mod's cleaning service | Nothing |

## Who decides

- A manager is one of the place's own employees, hired through the game's own flow and appointed on the tab. They keep their wage and shifts and get an allowance a day on top (a deductible wage cost, `reallife_transaction_co_manager`).
- Their skill is the game's value of their main skill. A decision's chance of a mistake is `ManagerRules.MistakeChance`: the most at nothing, none at 100, and less again with a regional manager over them.
- A regional manager is an employee at a headquarters who is on none of the game's HQ plans. They review the district's managers and stand in, a little less skilled, for one off sick. A manager off sick with nobody to stand in decides nothing that day.

## Every effect, and how it is undone

| Kind | Hour | What it does | Undo |
| --- | --- | --- | --- |
| Raises | 9 | `StaffRules.Answer` with the answer inside the wage band | The raise and the standing taken back, the request open again; for a fortnight |
| Rivals' offers | 9 | `StaffRules.OfferAnswer`, matched inside the band | The raise or the notice taken back, the offer open again, while they have not gone |
| Overtime | 9 | `AnswerOvertime`, agreed inside the month's budget | Stopped from today, its standing and the budget's unused days given back |
| Claims | 9 | `CoverRules.File` when worth it; else left open for the player | Back to open until it is paid |
| Stock orders | Sunday 12 | The contract lines' `amount`, as BizMan's contract screen sets it | The amounts put back until the game's lock (Sunday 20:00); the products become the player's |
| Prices | 8 | The brands' writer (`BrandsGame.KeepPrice`, as the game's price cell writes), through `CompanyBridges.WritePrice` | The prices put back for a fortnight; the products become the player's |
| Weekly rota | Sunday 21 | The rota planner's own fill job (`JobManager`), written only as `ManagerRules.Rota` says (see below), with the week before kept | `RotaPutBack`, while the week is kept (until the game is closed or another save loads) |
| Replacing staff | 11 | `EmployeeHelper.HireCandidate` of a job board candidate inside the band and the budget | They leave the game's way (`Core/StaffLeave`) for three days |

- A figure the player changes in the game after the manager set it (an amount, a price) is taken as the player's: the decision is marked "Changed by you" and the product is left alone until the player hands it back.
- With a manager answering a kind at a place, the company sends no notice asking the player about it (`ManagerRules.Answers`). A manager's own request or offer is always the player's. With the option off, a post stood down or the kind taken back, the notices ask the player again, and nothing of the game's is left changed by the switch.
- Money that has moved stays moved.

## A switch for each task

- The manager's own panel (a place picked on the tab) has a section, What they decide, with a switch for each kind its place has: the game's own settings menu switch, as the Mod settings app has them (`GameUi/InfoBox.Toggle`, made from `OptionRows.SwitchRow`; where the menu cannot be copied, the box's tick switch, then a button). Under each, its limit, or for the rota when it is written. A kind's own page has the same switch and its limits.
- The switches are the record's `Tasks` bits (since 0.44.0): every kind the place has is on when a manager is appointed, so nothing changes for a save that never touched them, and nothing new is written to the save.
- Off, the kind never runs: `ManagerRules.Runs` is asked by every round (the hours' and the test button's), the company asks the player about it again (`ManagerRules.Answers`), and every decision comes only at its own hour (`ManagerRules.Due`).
- With the weekly rota off the manager never touches a BizMan schedule: the only place a manager's week is asked for is `DecideRota`; switching it off drops a week of theirs still waiting (`ManagerRotaDropped`), and one under way is stopped just before it would be written (`ManagerRotaJudged` asks `ManagerRotaGiven`). So another schedule mod, or the player, keeps the rota without the manager writing over it.

## The weekly rota: whole or not at all

- **Why it went wrong (0.44.0 to 0.47.0).** Every Sunday at 21 the manager wrote the planner's fill of the week, whatever the week was like. The fill takes out every shift of those it may move and writes its own; anybody it gave no hours was made idle (`RotaGame.Write`, then `After`: `UnAssignWork` and the game's idle task), and the player's own checks before a fill (somebody's least hours not held, `FillLeft`; a fill that covers less than the week) were never asked for a manager. So a rota could change with nothing having happened, staff could drop off it, and hours manned before could be left empty.
- **Now (`ManagerRules.Rota`, `CompanyDirector.ManagerRotaJudged`).** Once a week, Sunday at 21, and only then. The week as it is is left alone if every wanted hour has somebody and everybody who can work one of its stations has a shift. Otherwise the planner's new week, for the same opening hours, is written only if nobody on the rota now is taken off it, no hour that has somebody now is left with nobody, and it mends something (more hours covered or more of the staff on it). Otherwise nothing of the schedule is touched; where the week had a gap the manager could not mend whole, their log says why ("Left the rota as it was", figure A -3 or -4, closed). Those the planner leaves as they are (a headquarters desk holder, somebody at a station it does not read) count for neither side.
- A week written still keeps the week before it to put back (the manager's decision's undo), and closes the older rota decisions only when it is written.

## Going Public

Every place is one `GoingPublic.Mine` calls the player's own (`RotaGame.ReadPlaces`), and the price writer and the hire ask again. Decisions are rolled from the company's saved seed with the day, place, kind and subject, so a save makes the same ones. Nothing is sent: what a manager changes is what the player changes in the game's own screens.

## In the save

One line, `reallife:economy_managers`: `v=1^h:...^m:...^d:...` (`ManagerStore`): the month's first day and the next decision's number; a record per manager (24 whole numbers, the place or district, the employee, the headquarters, the stock looks and the products left to the player); and the last 160 decisions with their six figures and detail. A newer version adds fields at the end of a record or a part with a new letter; every figure is bounded and every list capped, and a line that cannot be read is kept as it was (`Core/SaveLines`).

## Cost

Nothing per frame. Once a game hour, a comparison of the hour against the kinds' hours. At a kind's hour, one pass over the places with a manager. On the day, the places and staff read once (one pass over the save's buildings and staff, as the rota planner reads them), the allowances paid and the decisions too late to undo closed. A week's rota is worked out by the rota planner's own job, a few milliseconds a frame.

## Tests

Three buttons under the options: make the most skilled employee of a business its manager with every kind it has; let every manager decide everything now (whatever the hour and the day); undo every decision that can still be undone.

## Checks

`ManagerTaskChecks.cs`: for every kind at every kind of place, its switch off means it never runs in four weeks of hours while the others run as before, and the company asks again; every switch off means nothing at all; each kind only at its own hour, the rota only on a Sunday at 21; the rota's call over a whole week (left alone even when the planner would shuffle it), a gap mended with everybody kept (written), somebody taken off, fewer hours covered, nothing mended (not written), a new hire put on, a desk holder not counted, the same call every time; the director going through those rules and nothing else (the hour by `Due`, the rounds by `Runs`, one place that asks for a manager's week, judged just before writing); a save of 0.44.0 loading with every kind on and writing back the very same line. Made to fail once: with `Runs` ignoring the switch and `Rota` always writing (the old behaviour), 19 of them fail.


`EconomyCheck` (`ManagerChecks.cs`): each place's kinds; the mistake's chance over every skill and review, the roll's spread and that a save rolls the same; every kind's decision inside its limit with and without a mistake (raises over a grid of pay, asks and ceilings, offers, overtime against the budget, claims against the loading, orders over use, stock, floors, limits and skills, prices over markets, suggestions and bands, hires over two thousand random lists); the weekly counts of the game's delivery contracts; who decides when a manager is off sick; the company asking again when the managers are off, the post empty or the kind taken back; a figure the player changed taken as theirs; the undo windows; the month's report; the save's line over a round trip, a later version, a damaged line and the log's cap; the texts in every language and What's new.

## Not checked in the game (the game could not be run)

That the seventh tab fits the Company app's bar. That a job board candidate hired by a manager takes their place as one hired from MyEmployees does. That a contract's amount set on a Sunday at noon is the one the game delivers on the Monday. The milliseconds of the price round in a late save.
