# Hiring professionals

## In a sentence

The player hires professionals for their businesses in Open Roles: ads on a jobs board, applicants screened, interviewed and made offers, a search firm briefed for senior people, and a search firm briefed from headquarters for a data centre's crew. The days a hire takes can be paid for: a search is rushed so its candidates come tomorrow, and a hire's notice is bought out so they start tomorrow.

A player asked for it: "pay an extra fee based on the number of days required to expedite the process" for staff found through headhunting firms.

## Where the code is

- Folder: [`Scripts/Tech/Hiring`](../../Assets/Mods/RealLifeOverhaul/Scripts/Tech/Hiring/) (the catalogue, the rules, the save line and the game side), the director's part in [`Scripts/Tech/TechDirector.Hiring.cs`](../../Assets/Mods/RealLifeOverhaul/Scripts/Tech/TechDirector.Hiring.cs), and the Open Roles app twice: in the game's frame (`Scripts/Tech/People/RolesApp.Hiring.cs`, `RolesApp.Crew.cs`) and on the RLO interface (`Scripts/Ui/Work/RolesLive.cs`, `RolesLive.People.cs`).
- Read first: `HiringCatalogue.cs` (every number), `HiringRules.cs` (every rule, game-free, run by EconomyCheck's `HiringChecks.cs`, `BuyoutChecks.cs` and `SearchRushChecks.cs`), then `TechDirector.Hiring.cs` (the state, the day, the player's calls) and `HiringGame.cs` (the file that touches the game).

## How the days of a search firm's hire go

| Step | Days | Where |
|---|---|---|
| The firm's search | 4 to 8 for a senior search, 2 to 4 for a data centre's crew, drawn when it is briefed and kept as the search's `Due` day | `StartSearch`, `StartCrewSearch` |
| The answer to an offer | the next day | `HiringRules.Offer`, `Answers` |
| The notice | for someone in work, 7 days and 5 more for each level (17 for a senior, 27 for a principal); someone out of work starts in 1 to 3 days | `HiringRules.NoticeFor`, `Answer` |
| The start | the first day after the notice, on the game's staff list or the mod's payroll | `TechDirector.Starts` |

Two of the waits can be paid for. The notice has been buyable since 0.63.0 (`HiringRules.BuyoutPrice`: the pay for the days saved by one and a half, from the hire's page once they have accepted). The firm's own search could not be shortened until the search rush.

## The search rush

- **What it does** (`HiringRules.Rush`, called from one place, `TechDirector.RushSearch`). A search that has brought no candidates and is due after tomorrow is brought forward to tomorrow, the soonest any search delivers, because the day's own run (`day >= Due`) has passed for today. The day's run then delivers it as it delivers any search; nothing else is changed, so there is no new field in the save.
- **The price** (`HiringRules.RushPrice`): `HiringCatalogue.SearchRushBp` (500, a twentieth) of the search's fee for each day saved. A senior engineer's search with a fee of 28,000 costs 1,400 for each day saved, 8,400 for six. A senior search has 3 to 7 days to save (15% to 35% of its fee again), a crew search 1 to 3 (5% to 15%). The same rule for both, in whole cents, held at the most a sum can be.
- **Who can** (`HiringRules.CanRush`). Only a search, only before its candidates are in, only with a day left to save, and never twice: once rushed it is due tomorrow. The reasons are texts of their own (`reallife_hire_rush_delivered`, `reallife_hire_rush_soon`).
- **Who pays** (`TechDirector.RushSearch`). The business the search is for pays the firm; for a crew search that is headquarters, as its retainer was, or the data centre if there is none now. The price is worked out again when the question is answered, never taken from the page, so a day that passed while the box was open saves a day less and costs less, never more. A business without the cash is refused whole, with the amount and the business named. The money is booked as `reallife_transaction_tech_recruitment_rush`, a recruiting cost among BizMan's lines (`BizLines.CostKeys`).
- **Asked before it is paid.** On a search's page in Open Roles, Search tab: a line with the price, the share, the day the candidates come and the day they were to, and the button **Rush the search**; it asks through the game's question box with the same figures. On the RLO interface the Search tab has a band **Rush a search**, a button for each search that can be rushed with the price on it, and the same question box. The Search tab and a new search's page also say what a rush is before the firm is briefed.
- **The option.** **Rush a search firm's search** (`tech_search_rush`, on), in the jobs board's section and needing it. Off: no line, button or band is shown and `RushRefusal` refuses; searches take their days.
- **The test.** Options, Test tools: **My oldest search can be rushed** puts the oldest search that has not delivered at least three days off, so that its page offers the rush, and writes a `Tech check (search rush test, ...)` line. Each rush writes `Tech check (search rush, ...)` under Developer diagnostics: the days saved, the price booked against the rule's, the day it now delivers against the day wanted.

## Rivals and the city

No rival or startup in the mod briefs a search firm. A rival's ad is a line of its own that fills by chance from the open market (`HiringRules.Day`), a startup short of people takes from the open market (`ExitPlaces.Hire`), and neither reaches a search firm's candidates, who are marked passive and are the player's alone (`HireOffers.Day`). So no rival waits on a search and there is nothing for a rival to rush. The one wait a rival has, the notice of a person it poaches from the player, it buys out by the rule the player buys out a notice by (`TechDirector.RivalBuyout`, `HiringRules.BuyoutPrice`), and it pays the player. If a rival is ever given a search, the rush is a rule of `HiringRules` and not of a page, so it can be given the same price; `SearchRushChecks.cs` holds the present state (the market's days never add a search of a rival's).

## Candidates of a search on the RLO interface

The interface's Applicants tab listed only the candidates of job ads, so with it on a search firm's three candidates were delivered and never shown, and neither a hire from a search nor the notice buy out could be reached. Now a search with candidates still in play is a row of the "Open roles" list (named like "Software engineer search"), and its candidates are the table's rows. A search sets no top pay, so their page shows the going rate in its place and the table leaves out the "asks less than the pay you set" line (`RolesLive.People.cs`, `SearchHasLive`).

## Going Public

The hiring is the player's own: nothing is sent, and a rush is the player's own money at the player's own business (headquarters is found through `GoingPublic.Mine`). Nothing in it differs between machines: the price is a fixed share of a fee, with no roll.

## Cost

Nothing per frame. A rush is a click: one pass over the player's ads (twelve job ads and the searches, a few dozen records at most) when the Search tab is drawn, and one payment.

## Checks

`EconomyCheck --search-rush` (`SearchRushChecks.cs`, also in the full run): the price and its edges; which searches can be rushed; that a rush only ever brings a search forward, to tomorrow, by the days it saved, and never twice; that a rushed senior and crew search deliver by the day's own rules; that no rival or startup holds a search; the sources (one place decides, paid before the day moves, the question on every page, the kit's list of a search's candidates); the option, the money's key, the test, the Guide's figure and the texts.

## Not seen in the game

Everything above is built and checked offline. Not seen in play: the line and button on a search's page, the question box, the band on the RLO interface and a search firm's candidates in its Applicants tab.
