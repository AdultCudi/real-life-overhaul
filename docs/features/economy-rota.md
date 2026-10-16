# Economy: the staff rota planner

## In a sentence

The **Rota** tab of the Company app: for every place the player runs it shows the game's own customer forecast by the hour, the opening hours that pay for their staff, and writes a week's rota at the press of a button.

## Where the code is

- Folder: [`Scripts/Economy/Company`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Company/) (`Rota*.cs`, `CompanyDirector.Rota.cs`, `CompanyApp.Rota.cs`). The files are listed in the code block below.
- Read first: `RotaSolver.cs` and `RotaLadder.cs`.

## How it talks to other features

- It reads the game's own staff, opening hours and order history, and nothing of the mod's is counted twice; see [What is read](#what-is-read-and-why-nothing-of-the-mods-is-counted-twice).
- It keeps nothing in a save.

A tab of the Company app, "Rota" (`Scripts/Economy/Company/Rota*.cs`, `CompanyDirector.Rota.cs`, `CompanyApp.Rota.cs`; option "Staff rota planner", id `economy_rota`, under "Real Life Overhaul: Running a business", on by default).
For every place the player runs it shows the game's own customer forecast for each hour of the week, the opening hours that pay for their staff, the people those hours take against the people there are, and it writes a week's rota when the player presses a button.

It keeps nothing in a save and has no Harmony patch.

```
Economy/Company/        the first six are game-free and compiled by EconomyCheck
  RotaModel               the planner's numbers (the game's limits among them) and its plain data: problem, plan, demand
  RotaSolver              the rota itself: limits and goals, the start, placing an hour, a demand met or not
  RotaSolver.Shifts       whole-week stations, shifts for those with a least number of hours or days, shifts by value
  RotaSolver.Improve      the improving pass under a fixed budget of moves, and the rating of a rota
  RotaAdvice              an hour's crew, the hours worth opening, what each hour at each station is worth, a week in
                          money, the people a week takes, a station that is missing
  RotaLedger              what a fill may touch in a week that is there, what putting the week back restores, the stamp
  RotaGame                the places, what a business was worked out from, names; everything from the game is here
  RotaGame.Read           one business into plain numbers: stations, skills, the week it keeps, customers, worth, fees
  RotaGame.Demands        the game's rota demands, each asked of the demand itself
  RotaGame.Write          the rota and hours written, the week as it was kept and put back, as RotaLedger decides
  CompanyDirector.Rota    the list of places, the jobs that work a business out a few steps a frame, the buttons, tests
  CompanyApp.Rota         the tab: the places, one business's week, one day's hours
  RotaGuide               the Guide page's figures, from RotaModel
```

## What the game already had, and what was therefore built

The game has a schedule screen and, with an HR manager at a headquarters, an auto fill (`ScheduleAutoFiller`, Google OR-Tools on a worker thread) that puts the employees of a business on its stations for the hours it is open and keeps their demands.
It is left as it is.
It does not say which hours are worth opening, how many customers will come, how many people that takes or which stations are worth staffing.

Those are what was built, with a rota of the mod's own making (no OR-Tools: the mod references nothing new).

## How each kind of place is planned

| Place | Forecast | Hours | Rota |
| --- | --- | --- | --- |
| A shop of any type on a retail building, a gym, a hairdresser, a nightclub, a casino, a cinema, a theatre | the game's calculator for it (`CustomerEntriesCalculatorFactory`): `GetCustomersByHour` for each of 168 hours | advised | service stations by the customers each adds, support stations while open, cleaning by the dirt |
| An office that takes customers (a law firm, an agency) | the game's office calculator without its cap at the desks staffed now | advised | one customer a staffed desk an hour, as `OfficeBusinessSimulator` serves them |
| A factory, or any type the game sends no customers | none | the hours it keeps | every work station every open hour, cleaning by the stations at work |
| A headquarters | none | the game's office hours (its screen refuses to change them) | one person a desk for the whole week, the game's own rule there |
| A warehouse | none | none | none: the page says the game gives its drivers their vehicles |

A place with no assignable station, or with nobody assigned, says so in a sentence.
A business the game's figures cannot be read for is left out for the session with one line in Player.log, and its page says so.

## What is read, and why nothing of the mod's is counted twice

- **Customers**: `GetInitialCustomers` and `GetCustomersByHour`, which hold capacity, the promotion figure and the type's day and hour factors.
  The city's economy, a campaign, overtime and a strike of the mod's change customers by writing that promotion figure (`BusinessGame.Footfall`, through `Wiring/CompanyLinks`), so the game's figure already has them for today and they are not added again.

  A weekday on which a campaign, overtime, a strike or one of business pressure's issues will have ended or begun is asked of the game's calculator with the promotion figure the mod will have written by then.

  That figure comes from `BusinessCatalogue.Written`, the one function `BusinessGame.Footfall` writes with (the issue's change and the ambient one each inside their own bounds, the result a promotion of 0 to 100), so a business already at 100 is forecast no gain from a campaign.
  The figure stands in the business's record only for the length of the asking (`RotaGame.ReadCustomers`); `CompanyDirector.RotaDays` works it out.

- **What a customer brings**: the business's `orderHistory` of the last 7 days (revenue, wholesale cost, customers); with fewer than 20 customers on it, the products on sale at their prices by the arithmetic of `CustomerEntriesCalculatorRetail`.
  The page says which.
- **Stations**: `BuildingRegistration.GetAssignableItems`; what one serves an hour by `ItemHelper.GetItemsSortedByCapacity` (the rule of `RetailBusinessSimulator.GetMaxCustomerCapacityThisHour`).
  A station that lacks one of its furniture requirements is passed over, as the game's simulation passes over it.
- **Skills**: one of the employee's skills among the item's `suitableSkills`; how well by skill and satisfaction.
- **Demands**: `HoursWorkingPerWeek` and `WorksOnItem` by their public members; `DaysWorkingPerWeek`, `FreeOnDays`, `HasNoShiftBetweenHours` and `NoSpecificShift` by asking the demand's own `Fulfilled` or `AcceptsWorkStation` about an employee who is nobody with a made-up week.
  No private member of the game is read.
- **Fees**: the cinema's and the theatre's licensing fee for a day it opens.
  **Cleaning**: the game's dirt figures.

## The rota (`RotaSolver`)

Never broken: a skill for the station, one place at a time, a shift of at most 12 hours, a day of at most 14, one person for the week at a headquarters desk.
Wanted, in this order: the most value covered, the most demands met (weighed 2, 3 and 5 as `JobDemand.Impact`), the lowest wages, few and long shifts.

Demands that set an upper end are limits; a least number of hours, a number of days and a station asked for are goals.
If hours stay uncovered, demands that are not of high priority stop being limits (the game's own `skipOptional` step) and give way only where that covers an hour; those of high priority never do.

Two starts are made and the better kept; the improving pass counts its moves against a fixed budget, so the same business always gets the same rota.

## What is written, and how it is undone

Only on the player's button, after the game's own confirm dialogue where a rota is written over or hours change: `ScheduleDay.workShifts`, and with "Use these hours and fill the rota" `isOpen` and `openingHourSlots`.
`RotaLedger` decides, and is checked offline:

- **Left alone**: every shift of somebody the planner did not read (a delivery driver), and everybody who is "fixed": whoever holds a headquarters desk, and whoever has a shift at a station that was not read (one that lacks a furniture requirement).
  A fill only ever fills desks that are empty.
- **Taken out**: every other shift of the people it read.
  **Written**: the plan's shifts, none for a station that is not in the building at that moment, none for somebody not assigned there at that moment, none on a closed day.
- **Afterwards** what the game does after its own auto fill (`ScheduleAutoFiller.ApplyResult`, `ScheduleAutoFillerHelper.OnCompleted`) and when its schedule screen closes (`BizManSchedule.OnDisable`), with two things left out on purpose: the game's `UpdateHQPlans` is never called, and `UnAssignWork` with the "idle" task is taken only for somebody who had a shift and has none now and who holds no headquarters plan.

  The game has no call that gives a headhunter's, a logistics manager's or a purchasing agent's plan back, so the planner never takes one.

- **Put back**: the week kept (each day's switch, its opening hours, every shift) is restored where the person still works there and the station still stands; shifts of those left alone stay as they are today.
  As a fill changes no plan, that is everything a fill changed; the "idle" task of somebody who has a shift again is closed by the game.
- **Never from a stale reading**: what was worked out carries a stamp of everything it was worked out from (every assignable item by id and whether it lacks a requirement, prices, hours, shifts, everybody assigned with wage, satisfaction, skills and demands).
  The stamp is compared again in the frame that writes; a business that changed is read again, and after three such readings nothing is written and the player is told.
- **Never in part**: a business with more than 64 stations (one bit each in the solver) or 120 employees is refused with a sentence; nothing of it is read in part and nothing of its rota is touched.
  The game's building size data is asset data and is not in the decompiled code, so the largest business the game allows is not known here.
- **The late opening permit**: while licences are on and the business holds no permit, hours from 22:00 and before 05:00 are barred from the advice, so "Use these hours" can never put it in breach.
  The page says what the permit would add (hours and money), and says so when the business opens late without one as it stands.

## The ladder of plans (`RotaLadder.cs`, `RotaLadder.Steps.cs`)

The Rota page of a business no longer shows two extremes ("with the staff", "with all it takes").
It shows a ladder, every rung a set of opening hours with a rota, all valued the same way (`RotaLadder.Evaluate`):

| Rung | What it is |
| --- | --- |
| As it is | the hours and the rota the business has |
| Best with the people you have | the better of: the week as it is; the hours kept with a new rota; those hours less the ones nobody can keep the smallest crew for; the hours worth opening, cut to what the people can keep. Never worse than the week as it is, by construction |
| Hire 1, Hire 2, ... | one more person at the going wage (`StaffRules.Going` of the game's wage formula for the station's skill at a middling value), for the group of stations where that adds most (up to three groups are tried), counted for a full week of 40 hours and no more. Kept only if it adds at least 5 per cent of the week before and at least 50.00 a week. The ladder ends at the first hire that does not, and at four hires |

A week is: what the customers served leave (at the cleanliness they find, and for goods only while there are some: see "Goods" below), less wages, licensing fees, the game's hourly theft, the risk of a break-in on a night shut, and what unmet demands cost.
So a week open round the clock is shown only if every step to it pays.

**Staff first or lean.** Staff first: every demand is a limit (the solver is given them all at the game's high priority, which never gives way), and the least hours people ask for are wanted at stations that would otherwise stand empty, as the game's own auto fill gives everybody their least hours.

Lean: only hours that pay are wanted, the solver lets demands below high priority give way to an hour, and what that costs is counted.
Left alone the planner makes the hours kept in both and takes whichever comes to more; the player's choice for a business is kept in the save under `reallife:economy_rota` (`RotaStore`, version 1; a line of another version is not read and is left untouched until a choice is made).

For a small shop the default comes out as staff first: losing its only cashier costs more than the hours asked for.

**Cleaning hours** are chosen one move at a time by what each adds to what the week's customers leave, less the cleaner's wage: one hour more, or the same hour more on every day (a single hour changes nothing where the floor is dirtier than one hour cleans).
Staff first, the hours a cleaner asks for beyond those are added from closing time backwards, spread evenly over the days.

Where the mod's cleaning service cleans a business (`CompanyBridges.Cleaned`, set in `Wiring/ServicesLinks.cs` from `CleaningService.Covers`) no cleaning hour is wanted and the floor is taken to stay as it is.

**Goods** (`RotaStock.cs`, read by `RotaGame.Stock.cs`).

In a shop of the game's retail simulators, every product sold off a shelf (and, where the business type needs paper bags, the bags at the tills) is counted one by one:

- what its shelves hold (`CargoInstance.GetMaxStockCapacity`);
- what is on site: the shelves, the stock containers and any vehicle parked there, as `ReStockingHelper.CountAvailableStock` counts them;
- the box places of the business's storage less those other goods take;
- what a customer takes of it and what a unit leaves, by the same source as what a customer is worth (the books of the last days, else the game's own arithmetic for a customer's order);
- what the delivery contracts that repeat bring on a Monday (`DeliveryContractItem.amount`, with the weekly limit `Item.maxWholesaleOrderAmount`);
- what the logistics managers' delivery plans top it up to each morning (`ItemAmountTarget.targetAmount`, read as `LogisticsManagerPlan.GetPlannedDeliveries` would run it, without its side effects).

Each week of a plan is run hour by hour (`RotaStockRun`): the delivery plan at 2, the contract at 8 on a Monday (onto the shelves, then into storage a box at a time; what finds no room goes back, as `ItemHelper.DeliverCargoToBuilding` refunds it), then the hour's customers buy what is there.
The shelves are filled again from storage the moment they run empty (`ItemHelper.SubtractFromStock`, `RefillSingleItemWithLimit`), so what sells out is everything on site.

Without a bag nobody buys goods (`GetIdealPointOfSale`).
The goods run two weeks, the first from what is there now, and the second is counted, so a plan comes to what it comes to week after week with the deliveries there are.

An Urgent Order is a delivery once (the contract goes back to Mondays after it, and the game lowers its amount to what is left of the week's limit), so it is not counted; neither is a contract that does not repeat, a backorder or a shortage at the wholesaler (the page says those).

A product nothing brings is not capped: it is taken to be restocked by hand, and the page says how long what is there lasts.
A shelf whose item takes nothing on a sale (`producerSettings.ResourcesRequired` off) is no limit.
With no product capped the week is valued exactly as before.

The page lists each product that runs out in the advised plan with the hour, what its customers would have spent and what would lift it (`RotaLift`): room for the deliveries, a higher target for the delivery plan, a larger contract or an Urgent Order, or past the weekly limit a second wholesaler or a delivery plan from a warehouse.

Where the next hire adds too little because the goods run out first, the ladder ends with that reason (`RotaEnd.Stock`).

**What is written.** "Fill the rota" writes the rota for the hours kept (the ladder's `Fill`).
"Use this plan" is on the page of the best week with the people when it differs from the week as it is; the confirm dialogue says the hours open now and after and what the plan is worth.

A rung that takes a hire is never written (`RotaLadder.Writable`, checked again by the director before the job and before the write); its page shows a button that cannot be pressed, who to hire, at what wage, and where.
Every hour a plan opens or closes is listed with its reason and what it comes to now and in the plan.

## Hours a week are compulsory, and shifts follow the customers (`RotaFit.cs`)

**The range.** The game's hours demand is `Entities.Employee.JobDemands.Requirements/HoursWorkingPerWeek.cs`: `MinHours` (:20) and `MaxHours` (:22) are the two ends of a slider from 0 to 50 (:17) kept in each demand's own asset (`jobdemand_parttime`, `jobdemand_fulltime`), and `Fulfilled` (:24) asks for the assigned hours to be inside both.

The figures themselves are in the game's data, not its code, so they could not be read offline; the planner reads them from the game for every employee (`RotaGame.Demands.cs:89`) and uses whatever they are.
The game's own auto fill gives exactly the least (:98).

**The rule.** In every rota the planner makes (the one "Fill the rota" writes and every rung above the week as it is), whoever asks for hours a week works no fewer than the least and no more than the most, or is given no hours at all and is named with the reason.
It holds in staff first and in lean, for every skill.
How:

- the most: every hours demand is given to the solver at the game's high priority, which never gives way (`RotaLadder.Steps.cs`, `Build`);
- the least: the hours people ask for are wanted at their stations, the busiest hours first, also where the station would otherwise stand empty (`Lay`).
  Cleaning hours are chosen by what they bring, lengthened to the cleaners' least and made one shift a day around the busy hours (`CleanOne`, `CleanBlocks`).

  After the solver, `RotaFit.Reach` gives whoever is still short an empty hour beside a shift they have (a wanted one first, then the busiest), or an hour a colleague with more than their own least can spare at an end of a shift;

- whoever still has some hours but fewer than their least is left out of that week, one person at a time, and the week is made again without them (`Fitted`); nobody is ever written short.

  A rung says who it leaves without hours and why (`RotaLeft`): their own limits against the opening hours (with the hours open to them), the others at the same stations (what they ask for between them against what the stations hold), or the cleaning service.
  A rung may be used (`RotaRung.Usable`, `RotaLadder.Writable`) only if nobody in it works outside their range.

**When the hours kept are too few.** The plan with the people then has one more candidate: the hours kept and more, the best paying hour beside an open one added one at a time (never an hour barred for want of a permit) until the opening hours hold the least hours of everybody who was left out (`Extend`).

Its page lists those hours as "opened so that ... gets the hours they ask for" with what they earn or cost.
Candidates are ranked first by how many people keep the rule, then by what the week comes to, so that plan may come to less than the week as it is: the page shows the difference.

"Fill the rota" on hours that cannot hold somebody says so in its dialogue with the figures and offers that plan in its place.

**Shifts follow the customers.** What an hour at a till is worth is its customers, so the busiest hours are covered first; hours given beyond need are valued by the customers of the hour.

After the solver, `RotaFit` mends shifts of a single hour (lengthened, or given up where the holder stays in range) and then moves shifts towards the customers until no move is left: a shift an hour later or earlier, an end hour of a long shift to a busier hour beside another shift of the same person, and a break that a 12 hour shift forces moved to the quietest hour.

So within a day a busier hour never has fewer people at the tills than a quieter one unless a rule forbids the move: skill, one place at a time, the 12 hour shift, the 14 hour day, the person's own demands, or whole shifts (no further piece of work on a day, none of a single hour).

## What the game does, as read from its code (decompiled `BigAmbitions`)

- **An unmanned till sells nothing (away).** `Buildings.Retail.Simulation/RetailBusinessSimulator.cs:106` (`LoadPointsOfSaleWithCustomerSatisfactionThisHour`) keeps only tills whose employee gives a customer satisfaction above nothing; `ProcessCustomer` (:229) with no till leaves every retail entry unpaid (`ProcessItemInOrder`, :296) and completes the order.
  `Helpers/BusinessHelper.cs:234` (`CreateDailyOrderHistory`) counts only orders with a paid entry as customers and revenue.

  **With the player inside** customers queue at an employee station (`CustomerJoinQueue.cs:38`) and complain where none is available; an order is only added to the day's books when it completes (`Customer.cs:383`).
  The planner therefore values an open hour without the smallest crew at no sales.
  Not verified in the running game.

- **A till's customers an hour are a hard limit, and skill does not change it.** `RetailBusinessSimulator.cs:214` (`GetMaxCustomerCapacityThisHour`) sums `Item.addedCustomersPerHour` of the manned stations of each group and takes the smallest group; customers beyond it are marked completed and lost (:176).
  Skill enters only as the order's service quality: `Entities/EmployeeInstance.cs:1037` (skill x satisfaction x 0.01).

  The forecast itself stops at what all stations serve (`registration.customerCapacity`), so the page shows the uncapped figure beside it ("36 come, the stations serve 20") and what one more station would serve.

- **Satisfaction and sales.** `Helpers/BusinessHelper.cs:822` (`UpdateSatisfaction`): overall = (service + pricing + cleanliness + facility) / 4, the last three over orders with a paid entry.
  `AI.Customers.CustomerEntries/CustomerEntriesCalculator.cs:43`: what customers buy goes with (overall - 50) / 100 + 1.
  One point of cleanliness is therefore a quarter of a point of satisfaction, 1 / (4 x (50 + satisfaction)) of sales.
- **Nights.** The game's forecast gives every hour its own figure; the self-check line prints the week's customers by the hour of the day.
  Nothing in `BusinessHelper.RunHourly` (:454) charges an open hour beyond wages and, for a cinema or theatre, the day's licensing fee.

  Theft (`Helpers/BusinessSecurityHelper.cs:133`, `SimulateTheft`) follows the customers of the last hour, not the time of day: thieves a shelf = customers x 0.04 x (1 - security); under one it is the chance that one unit goes from a stocked shelf, and from one on the code takes nothing (`num2` is only set in the branch under one).
  The security level counts guards only while they are on a shift (:82, `IsWorking`).

  The mod's break-ins (`Scripts/Crime/OffscreenCrimes.cs:77`, `BreakIn` :120) are rolled once a night in the small hours and only hit a shop that is shut then, so an hour open at 01:00 removes that risk.
  The planner counts the expected loss of each night shut (chance 4 per cent x the frequency option x exposure, shared among the player's shops, times a quarter of the shelves up to 2,500 and the repair).

  The late opening permit bars the hours from 22:00 and before 05:00 as before.
  `HasNoShiftBetweenHours` demands are read as limits as before.

- **Cleaning.** `Buildings.BuildingTypes.Shared.Dirtiness/BuildingCleanliness.cs:35` (`ApplyDirt`): each dirtying adds 1.2 to two thirds of the spots an item touches.
  :112 (`SimulateCleaning`, hourly from `BuildingCleanlinessHelper.cs:22` on a day the business opens): every spot loses the cleaners' skill x satisfaction / 100, divided by the cleaners the floor needs.
  :64 (`GetCleanliness`) = 100 - mean dirt - mean visible dirt (five points and more), which an order carries into the satisfaction above.
  `CleaningStationController.cs` only places the cleaner in the room.

  The planner runs the week hour by hour with the mean dirt (twice, so that it starts as dirty as it ends).

- **An unmet demand.** `Entities/EmployeeInstance.cs:744` (`UpdateSatisfaction`, hourly): plus the weight of each demand met and minus that of each unmet (`JobDemand.Impact`, `Entities.Employee.JobDemands/JobDemand.cs:43`: 0.2, 0.3, 0.5), no more than 0.6 an hour; with a third or fewer met it cannot rise above 60, with two thirds or fewer above 80.
  `AI.Employees/LowSatisfactionComplaint.cs:13` and `EmployeeInstance.cs:1305`: at nothing the employee resigns at once.

  `AI.Employees/UnfulfilledDemandsComplaint.cs:19`: a complaint about an unmet demand gives 72 hours to meet it or pay a bonus (not counted: it comes at a random time).
  `Helpers/EmployeeHelper.cs:154`: more than 14 hours worked in a day gives a chance of a sick day (the solver never rosters more than 14).

  The planner counts: hours to the resignation = satisfaction x 10 / (weights unmet - weights met); the sales the person's customers leave less their wages (or their wages, if more) for the 7 days the post is taken to stand empty, spread over the weeks to the resignation; and the worse work on the way (service is skill x satisfaction).
  **The 7 days are the planner's own figure**, not the game's.

- **Hiring.** Candidates come from the MyEmployees app and job boards (`UI.Smartphone.Apps.MyEmployees/JobBoardCandidateGenerator.cs:22`); a signing bonus is optional (`AI.Employees.SalaryNegotiation/CandidateSalaryNegotiation.cs:70` starts it at nothing).
  The planner counts no one-off cost for a hire.
- **Stock.** `RetailBusinessSimulator.cs:296`: an entry is sold only from a shelf that holds it, and shelves are refilled from storage at the hour the last work shift of the day ends (:424).
  **Not counted**: the plans take the shelves to be full.
  A plan that sells more needs deliveries to match; the Guide says so.

## A worked example (printed by `EconomyCheck --rota-example`)

A gift shop: one till (20 customers an hour), one cleaning station.
786 customers a week (112 a day, 12 in the busiest hour, a few through the night).
A customer leaves 20.86 after the goods.
A cashier at 12.00 who asks for 20 to 30 hours, a cleaner at 9.00 who asks for 35 to 45.
Open 68 hours with the cashier rostered for 30 and the cleaner for 4.

Going wages 12.50 and 9.50; satisfaction 70, cleanliness 80; theft 0.23 a customer; a night shut 3.24.

| Rung | Open | With the cashier | Staff hours | Served of those who come | Sales | Wages | Theft | Night | Demands | Week | Gain |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| As it is | 68 | 30 | 34 | 307 of 572 | 6,209.79 | 396.00 | 131.56 | 22.68 | 36.16 | 5,623.39 | |
| Best with the people | 68 | 30 | 65 | 314 of 572 | 6,818.42 | 675.00 | 131.56 | 22.68 | 0 | 5,989.18 | 365.79 |
| Hire 1 cashier | 70 | 69 | 104 | 587 of 592 | 12,598.60 | 1,162.50 | 136.16 | 22.68 | 0 | 11,277.26 | 5,288.08 |
| Hire 2 cashiers | 109 | 108 | 143 | 725 of 726 | 15,380.07 | 1,650.00 | 166.98 | 16.20 | 0 | 13,546.89 | 2,269.63 |
| Hire 3 cashiers | 145 | 143 | 178 | 782 of 784 | 16,512.57 | 2,088.00 | 180.32 | 3.24 | 0 | 14,241.01 | 694.12 |

The best week with the people, as written: the cashier (asks 20 to 30, gets 30) stands at the till 11:00 to 14:00 on Monday and Tuesday, 10:00 to 14:00 on Wednesday and 10:00 to 15:00 from Thursday to Sunday, the 30 busiest hours of the 68.
The cleaner (asks 35 to 45, gets 35) works 10:00 to 15:00 every day, one shift around the busy hours.

In every plan everybody is inside their range.
As it is, 265 customers a week find nobody at the till in the 38 open hours without the cashier; nothing more can be sold without somebody at the till, so each further rung is a cashier of 40 hours.

The third still adds 694.12, over the 5 per cent (677.34) a rung must add, because in this example a customer or two an hour through the night leave more than a cashier's wage.
With no customers at night no plan opens those hours (checked).

**Where this contradicts the earlier figures.** The earlier page said for this shop: now 6,008.02; "with the staff" 13,021.36 at 90 hours; "with all it takes" 14,631.96 at 147 hours.
The first left out theft, the night risk, the dirty floor and the cleaner's unmet demand.

The second had the one cashier work 90 hours a week against a demand for at most 30, and counted nothing for it; the game would take 0.3 of satisfaction an hour and the cashier would resign within two weeks.

The earlier advice also gave the cleaner the fewest hours that kept up with the dirt at closing time (four to six a week), which the hour by hour run shows to be the wrong place and too few.
The third assumed four people appeared at once; the ladder shows what each one adds.

## Cost

Nothing runs per frame, per second or per hour while nothing is asked.
The list of places is read on the day event, when the app opens and after a rota is written: one walk over the save's buildings and one over its employees.

A business is worked out when it is picked on the tab or a button is pressed, as a job: the game is read in one frame, then the arithmetic and the rota are made a few steps a frame, for 2 ms of each frame at most, by a coroutine that exists only while a job runs.

`RotaSolver.Start`/`Step` and `RotaAdvisor.Begin`/`Step` do the same steps in the same order as their one-go forms, so the result is the same (checked on 100 generated businesses).

Measured by `EconomyCheck` on the largest business it is asked to bear (40 stations, 60 employees, 7 days, 4,480 wanted hours): a rota is about 1,800 steps and 1.5 to 2 ms in all, its longest single step under 0.1 ms, the longest step of the advice under 0.1 ms; nothing is allocated once the arrays have grown.
Those are figures of the .NET runtime the checker runs on.

On a runtime eight times slower that is some seven frames of 2 ms for a rota, where one go would have held a frame for 15 ms or more.
With "Developer diagnostics" on, "Company check (rota, ...)" and "Company: rota written for ..." say the real milliseconds, the frames and the longest share of a frame.

The ladder of plans is made by the same job, after the advice: `RotaLadder.Begin`/`Step`, the same steps as its one-go `Make` (checked on the example and on the largest business).
A step is one piece of a candidate week (the worth of its hours, a day's worth of cleaning moves, one step of its rota, its valuation).

For the largest business (40 stations, 60 employees) that is about 16,000 steps and 13 ms in all on the checker's runtime, the longest step about 0.2 to 0.5 ms (the check takes the best of five runs, since one run alone can be held up by the machine); for the example shop about 2,300 steps.

A small shop's ladder makes up to some fifteen candidate rotas, so on a runtime eight times slower its page may take a second or so of frames at 2 ms each to appear; the page says the week is being worked out meanwhile.
With "Developer diagnostics" on, "Company check (rota plans, ...)" gives the ladder in short form, the customers by the hour of the day and every rate it was valued with.

## Test tools

Four buttons: open the rota of the busiest business, show that business's plans in the log (the ladder in one line, then every rung with what its week is made of), fill every business's rota (without the dialogue; the weeks as they were are kept), and put every rota back.

## Checks

`EconomyCheck` compiles the ten game-free files (and `Business/BusinessCatalogue.cs`, for the promotion figure) and runs 434 checks of them: `RotaChecks.cs`, `RotaLedgerChecks.cs`, 91 in `RotaPlanChecks.cs` for the ladder and 90 in `RotaRangeChecks.cs` for the hours rule.
The hours rule is checked over 330 generated shops of one to four stations with part-time, full-time and no demand, short and long hours:

- everybody in every rota made is inside their range or without hours, and no rota breaks a limit to get there;
- where the open hours hold every cashier's least hours none is left out (existence by counting, exact for one till and days of at most twelve open hours), and whoever is left out has the true reason and no hours;
- no busier hour of a day has fewer people at the tills than a quieter one where a legal move could change it;
- no shift of a single hour; lengthened hours are the best paying ones; stepped is one-shot.

The ladder's checks: an unmanned hour sells nothing and still costs its theft.
A till's limit and a second till; that skill changes no count; theft, night risk, fees; cleanliness and the hour cleaned; the cleaning service's rule.
What an unmet demand costs, figure by figure, and against the wages that meet it in both modes.
The best week never worse than the week as it is.

Each gain as shown and each hire rung paying; a hire rung never writable.
Every changed hour with its reason.
No hour opened that nobody comes in; barred hours in no plan.
The same ladder every time and a step at a time.
The choices' line in the save.
The time of the longest step): every kind of demand.

The limits on a shift and a day as behaviour at each limit; one place at a time; skills; the headquarters rule.
What gives way and what never does, with the reason told.
No employees, no stations, closed all week, no customers; an hour's crew.

The hours advised (never an hour that loses money, at most two blocks of two hours or more, a day's fee, no late block without the permit); cleaning; the people a week takes; a missing station; a week in money.
Who is left alone and what a fill takes out and writes (never a station or a person that is not there).

That a week filled and put back is the week before, also when filled twice, and what is not restored when somebody left or a station was sold.
That a headquarters desk holder whom no rota would give a desk keeps theirs.
The stamp (a station swapped for another changes it).
The promotion figure of a day ahead against the function that writes it, with its bounds and its stop at 100.

That a rota and an advice made in steps are the ones made in one go.
The time of the largest business and of its longest step.
And 240 generated businesses against a plain rota filled hour by hour in the order of the week.
Against that plain rota it never covers less value.

With every demand of high priority it never leaves more demands unmet, and covers at least as many hours in 116 of 120 businesses (in the other four it covers fewer hours that are worth more).

With demands of every priority it covers more hours in 51 of 120 and leaves more demands unmet in 13 of them, each time for more value covered: optional demands give way to cover an hour, as they are meant to.

## Not checked in the game (the game could not be run)

That six tabs fit the Company app's bar.
That the week's box, with its lines and up to three rows of buttons, fits the white box, and that "Use these hours and fill the rota" fits a button.
That the game's confirm dialogue shows over the phone.
That each of the game's demands answers the made-up week as its decompiled code says.

That the game's schedule screen shows a written rota as it shows its own.
What the forecast is against a day really played.
The milliseconds on the game's Mono, and above all those of the one frame that reads a large business from the game (its stations' capacities, its staff's demands and 168 hours of the calculator), which is not cut into steps.

That the coroutine runs while the phone is open (it waits a frame, not game time).

Of the ladder of plans: that six columns and the people's rows read well in the plans table, that the titles of columns that do not sort are readable (the kit shows them as the game shows a button that cannot be pressed) and that a page opens at its first row (a different table is shown from the top: `DataTable.Show`).

That the game's selector row shows the three choices in the white box, and that a button set not to be pressed looks so.
That an unmanned till sells nothing with the player inside as it does away.
That the dirt and cleaning rates read from a real building give a believable week (the self-check line prints them).
That theft in a played week is near the figure counted.

The seven days a post is taken to stand empty are the planner's own figure.

Of the goods: that what a real shop's shelves, storage, contracts and delivery plans are read as matches what the game shows in BizMan.
That a product is seen to run out when the forecast says (the books undercount what a customer would take of a product that ran out in the days they cover, so the cap may come late rather than early).

That the box places counted for storage match what a delivery really fits.
And how the goods rows read in the plans table.
