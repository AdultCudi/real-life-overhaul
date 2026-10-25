# Economy: the company's hierarchy

## In a sentence

One **company policy** every manager obeys when they decide by themselves, set for the company, a district (a region) and a store, a lower scope never past the one above; **escalations** that carry only real problems up from a store's manager to whoever may settle them, and to the player last; and, on the Company app's Managers tab, the policy centre, what is waiting for the player and a dashboard for each place.

## Where the code is

- Folder: [`Scripts/Economy/Company`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Company/). It stands on the managers ([economy-managers.md](economy-managers.md)) and changes none of their saved data.
- Read first: `PolicyRules.cs`, then `EscalationRules.cs`, then `CompanyDirector.Escalations.cs`.
- Option "Company hierarchy: policies, escalations, regions and executives", id `economy_hierarchy`, under "Real Life Overhaul: Running a business", on by default. It runs while the managers do.

```
Economy/Company/              the first six are game-free and compiled by EconomyCheck
  HierarchyCatalogue            the ranks and levels L0 to L4, shared with the careers (Scripts/Careers)
  PolicyRules                   the rules, their steps and starting steps, a scope held to the one above, templates
  EscalationRules               each trigger, who holds an escalation, who may settle which kind
  HierarchyRules                the regions a player makes, the reports' week
  HierarchyState                the policy sets, the watches, the escalations, the log
  HierarchyStore                the save's one line, "reallife:company_hierarchy"
  HierarchyGame                 a candidate of another place hired for a gap, a member of staff moved between stores
  CompanyDirector.Hierarchy     the policy in force at a place, the owner's changes, templates
  CompanyDirector.Escalations   the day's look at every place, raising, passing up and settling, the owner's answers
  CompanyDirector.HierarchyTests  a test button for each level and each trigger
  CompanyApp.Hierarchy          the pages: what waits, one escalation, the policy centre, templates, a dashboard
  HierarchyGuide                the Guide page's figures, from the catalogue
```

## The policy

Twenty-five rules under five headings, each a step of its own list (`PolicyCatalogue.Defs`):

| Heading | Rules |
| --- | --- |
| Hiring | the least skill a hire has; the most a hire is paid (a share of the going rate); where a gap is filled from (the place's own job board, the region's other boards, a move from a store with staff to spare) |
| Pay | the pay band; the largest raise at once; the days between two raises; who may agree past the band (nobody, the regional manager, the people chief, the owner) and how far |
| Demands the managers meet | a switch each for raises, rivals' offers, overtime and claims; the manager's month's budget; the most the finance chief adds to one running out |
| Performance | a profit target a day; a standing target; the tolerance; the days off target in a row; the weight of profit in a place's score |
| Escalation and reports | the days a gap may last; the share of a budget used; the days a request may wait; the standing floor; the leavers in a wave; how often the managers report |

- **Scopes.** The company's set has every rule. A region (a district, or one the player makes) and a store set only the rules they choose; the rest follow the scope above. Each rule has a way (`PolicyWay`): for most a higher step is looser (`LooseUp`), for the skill floor, the days between raises, the targets and the floor a higher step is stricter (`LooseDown`), and three rules are the company's alone (who approves exceptions, the weight of profit, the reports). `PolicyRules.Effective` takes the company's step, then the region's if it sets one, then the store's, each held to the strict side of the one above. A step past the one above is kept as the player chose it and shown held; it binds again if the one above loosens.
- **The start is what was.** The starting steps are what the managers did before: the band of `ManagerCatalogue.WageBandDefault`, no floor, a cap at the top of the band, no largest raise, nobody past the band, every demand answered, a report a day. A save that never opens the policy centre is decided exactly as before (EconomyCheck compares the hires over 2000 job boards and the ceilings over 4000 cases).
- **One copy.** The pay band and the budget are the managers' own steps (`ManagerCatalogue.WageBands`, `Budgets`). A manager's own band or budget is that store's rule: set on the manager's panel it is set in the policy, and the rule in force is written back into the manager's file each day (`PolicyMirror`), so the panel says what binds them. A save from before the policy keeps every manager's own band and budget that was not the start as their store's rule (`KeepOwnLimits`).
- **Templates.** Starting policy, Careful, Lean and Generous are built in (`PolicyCatalogue.Templates`); the player saves up to six of their own from the rules in force at any scope. Put on a lower scope, a template's steps are held to the one above and the company's own rules are left to it.

## What every automatic decision reads

| Decision | Reads |
| --- | --- |
| A raise (the manager's, at 9) | the pay band, the largest raise, the days between raises; past them, with an approver named, the request is left open and an exception is escalated at once |
| A rival's offer (at 9) | the pay band and the largest raise; past them, as a raise |
| Overtime, a hire | the budget in force with the finance chief's top-up |
| A hire (at 11) | the pay band, the hiring cap and the skill floor (`PolicyRules.Pick`); with nobody inside them on the place's own board, the region's other boards where the policy allows |
| Every round | the kind's switch of the policy (`PolicyAllows`): off, the kind is not decided and the company asks the player again |
| The day's notice at 18 | the reports' rule: every day, only on a Sunday with the week, or never |

## Escalations

Each day, after the managers' own day, every place with a manager in post is looked at once (`WatchPlace`), and only these raise one, each against the policy in force there:

| Kind | Trigger | Figures |
| --- | --- | --- |
| Staff short | the staff under the number the manager keeps, for the policy's days | A staff, B kept, C the day it began |
| Budget running out | the month's budget used past the policy's share | A spent, B budget |
| Requests unanswered | a request for a raise or a rival's offer waiting the policy's days, or a strike | A how many, B the oldest day, C 1 with a strike |
| Staff standing low | the staff's average standing under the floor two days running | A standing, B floor, C staff |
| Staff leaving | the policy's wave or more gone in a week or working their notice | A all, B gone, C working notice |
| Off target | the books' profit or the standing short of target past the tolerance for the policy's days | A profit, B target, C days |
| Past the pay band | a raise or an offer past what the manager may agree, with an approver named (raised at 9) | A asked, B the manager's ceiling, C 1 an offer |
| Policy proposal | an executive's change to a company rule | A rule, B proposed, C in force |

- One of each kind is open at a place at a time; its figures are brought up to date each day, and it is cleared when its cause goes. A place with no manager in post is the player's own and raises nothing.
- **Who holds it.** The first holder in post above the place's manager who may settle the kind under the policy (`EscalationRules.Next`, `Tries`): the region's regional manager (at work, not off sick), the area's executive (staffing and targets the operations chief, budgets the finance chief, pay and people the people chief), then the player. A holder tries once a round; if they cannot under the policy, it passes up to the next, and what reaches the player comes with one notice a round.
- **What a holder does.** A gap: candidates other places' job boards found, hired for the place inside its own rules and budget (`HierarchyGame.HireFor`, the game's MyEmployees way), then where moves are allowed the most skilled who fits from a store with more staff than it keeps (`HierarchyGame.Move`, as MyEmployees moves somebody). An exception: agreed if the pay asked is inside the band and the margin. Requests unanswered: answered inside the band, a strike left to the player. Low standing or leavers: the underpaid raised to the going rate inside the band. A budget: the policy's top-up, once a month. Off target: reported, never settled below the player.
- **The player's answers.** Fill the gap now (every source), add a month's budget, raise the underpaid to the going rate, agree or refuse an exception, approve or reject a proposal, or let it be (it is raised again only if its cause is still there).

## The pages

- **Waiting for you** (`hx:`), at the top of the Managers tab's directory: the escalations, newest first, filtered by who holds them; one (`he:`) with its story from the log, its figures and the player's answers, and links to the staff, the place's dashboard and its policy.
- **Company policy** (`pc:`): a scope picked at the top (the company, a region, a store with a manager), every rule under its heading with the company's, the region's and the store's own and the one in force (a held one in yellow); a rule (`pf:`) with its steps to pick, the scope above's first ("as above"); templates (`pt:`, `pk:`), saved from or put on the scope shown.
- **Dashboard** (`db:`), from each manager's panel: the staff against what is kept, the standing, the budget used, the score, yesterday's profit, what is pending there, and two quick actions inside the policy: fill gaps (the place's own board, then what the policy allows) and batch raises.

## Going Public

Only the player's own places are looked at, hired for or moved between (`GoingPublic.Mine` in `WatchPlace`, `ScopeOf`, `HierarchyGame`). Nothing is rolled: every escalation follows from the save's figures. Nothing is sent: a hire and a move are what the player does in MyEmployees.

## In the save

One line, `reallife:company_hierarchy` (`HierarchyStore`): `v=1^h:^c:^r:^s:^t:^w:^e:^l:^x:^a:`, the header, the company's steps, the regions' and the stores' own (a scope that sets nothing is not written), the templates, the places' watches, the escalations (120 kept, an open one never let go), the log (160), the executives and the stores put into a player's region. Every step is held inside its rule, a store never keeps a rule only the company sets, a part a later version adds is skipped, and a line that cannot be read is kept as it was (`Core/SaveLines`). A save without the line, or without the mod, is the company at the starting policy.

## Cost

Nothing per frame. Once a game day, one pass over the company's staff (`Tally`) and one over the places; at the staff hour, the exceptions. A page reads what it shows when it is written.

## Tests

Under the options, "Real Life Overhaul: Running a business": a button for each trigger (a gap, a budget, a request, low standing, a wave, a place off target, an exception) puts the cause in place at the first place with a manager (appointing one if none has one), runs the day's own look and the holder's try, and writes one line `Company check (hierarchy test, ...): ...: PASS` or `FAIL`; a made-up cause is taken away again so the next day clears it. L1 checks every manager decides by the band and the budget in force at their store and opens the first dashboard; L4 checks what waits for the player against the escalations kept.

## Checks

`HierarchyChecks.cs` (`EconomyCheck --hierarchy`): the shared ranks; the policy's catalogue; the start deciding as before; every rule over every company, region and store step (5486 cases) never past the one above; templates; each trigger at its edges; who holds an escalation and who may settle which kind; the regions; the save's line over a round trip, a damaged line, a later version and the caps; the director going through these rules and nothing per frame; the texts in every language and the Guide page; the time to read every rule at 64 stores. Made to fail once: with a lower scope taking the looser step and the days between raises ignored, 5 of them fail.

## Not checked in the game (the game could not be run)

That a candidate of another place's job board, given to the place with the gap, is hired as the game's own MyEmployees hires them. That a member of staff moved between stores takes their place at the new one as one moved in MyEmployees does. That the policy centre's selector fits the white box with a long scope list.
