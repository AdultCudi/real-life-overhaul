# Economy: running a business

## In a sentence

What running a business has and the game leaves out: staff who know the going rate, insurance, campaigns that are measured, a quarterly tax return with an accountant, licences that lapse, and rows in the Planner for all of it, in the **Company** app.

## Where the code is

- Folder: [`Scripts/Economy/Company`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Company/). The files are listed in the code block below; it is the folder to copy when you start a feature, since it has every layer of the pattern.
- Read first: `CompanyDirector.cs`, `CompanyCatalogue.cs` and `CompanyStore.cs`.

## How it talks to other features

- The company calls no other feature and none calls it: `Wiring/CompanyLinks.cs` uses business pressure's footfall, closures, disputes, rivals, resignations, audits and inspections, and opens a tax case with Justice.
- The [staff rota planner](economy-rota.md) is a tab of the same app.

The fifth part of the economy (`Scripts/Economy/Company`; options under "Real Life Overhaul: Running a business", ids `economy_company`, `economy_company_level` and one switch for each part: `economy_company_staff`, `_cover`, `_marketing`, `_tax`, `_hide`, `_licences`).

It adds what running a business has and the game leaves out: staff who know the going rate, insurance, campaigns that are measured, a quarterly return with an accountant and cash kept off the books, licences that lapse, and rows in the Planner for all of it.
It has its own director, its own line in the save and its own app on the phone, "Company".

It runs only while the credit side does, as the city's economy does.

```
Economy/Company/        the first eight are game-free and compiled by EconomyCheck
  CompanyCatalogue        every number, and the enums that are saved as numbers
  CompanyState            what is kept for a save: businesses, staff, claims, reports, tax
  StaffRules              the going rate, raises, a rival's offer, a strike, overtime, sick pay
  CoverRules              premiums, excess, claims, renewal
  CampaignRules           cost, effect, the report's arithmetic
  TaxRules                the quarter and its return, the accountant, cash off the books
  LicenceRules            which licences, terms, the free first term, renewal
  CompanyStore            the save's one line, "reallife:economy_company"
  CompanyGame             everything read from or done to the game, and CompanyBridges (what it asks of the mod)
  CompanyDirector         the timer's look, starting and standing down, faults, the app, the Planner's rows
  CompanyDirector.Day     the reading, the hour, the day, the save
  CompanyDirector.Effects footfall and money
  CompanyDirector.Staff / .Cover / .Marketing / .Tax / .Licences    one part each: its day, its answers, its rows
  CompanyDirector.Tests   the test tools
  CompanyApp              the frame, the tabs, the Overview
  CompanyApp.Staff / .Cover / .Marketing / .Tax / .Licences         one tab's page each
  CompanyTables           the app's tables as data
  CompanyText             the words; CompanyGuide: the Guide page's figures, all from the catalogue
Wiring/CompanyLinks.cs  business pressure, fires, break-ins, the courts, the paper, the Planner
```

**Adding a part** is a rules file (game-free, with checks), a `CompanyDirector.X.cs` that fills in the partial methods `XDay` and `XAlerts`, a `CompanyApp.X.cs` that fills in `WriteX`, a value of `CompanyPart`, a switch in the options and the names in the catalogue.
The core calls a part's day inside its own `try`: a fault there switches that part off for the session with one line and the others go on.

## What the game already had, and what was therefore built

| Part | The game's own, left as it is | Built here |
| --- | --- | --- |
| Staff | A wage per employee from a formula by skill (`EmployeeHelper.CalculateHourlyWageForSkill`), satisfaction from job demands (`Entities.Employee.JobDemands`: a clean workplace, hours and days, health insurance, equipment), complaints, a bonus, training that raises the wage, sick days without pay (`EmployeeHelper.RunDaily` sets `isAbsent`), poaching of teams by its special rivals (`EmployeeInstance.PoachByRival`) | A going rate that moves, raises asked for, a rival's offer to one employee, a strike, overtime, sick pay for whoever the game has off sick. No sick list of the mod's own and no flu spell: the game already decides who is sick |
| Insurance | Health insurance for staff only | Fire, theft and liability policies for a business |
| Marketing | A daily marketing budget through an agency (`BuildingRegistration.marketingCampaigns`), which raises the promotion figure for as long as it is paid, with no end and no report | Campaigns with a price, an end and a report, on top of it |
| Tax | A yearly tax on the businesses' sales less deductible expenses, above a threshold, with a late fee and seizure (`TaxHelper`, `TaxCalculationHelper`); property tax | A quarterly return to file, an accountant, cash off the books. Deductible costs are marked the game's own way, so nothing is worked out twice |
| Licences | "Licensing fees" for films and plays (`LicensingFeesHelper`): a cost of what is on, not a permit | Four permits to trade |

## Every effect, and which of the three safe ways it uses

The rule is the city's economy's: no game method is patched, and no figure of the mod's is written into the game's saved data. (a) is a mechanism the mod already has and restores, (b) a figure of the game's in memory that is not saved, (c) a dated transaction of the mod's own. (b) is not used here at all.

| Effect | Way | How it is undone |
| --- | --- | --- |
| A campaign's, overtime's and a strike's customers | (a) `BusinessGame.Ambient`: `Wiring/CompanyLinks` answers with the city's economy's and the company's together, and business pressure's own writer puts the sum in the promotion figure inside its own bounds (25 fewer to 15 more) | The figure is put at the game's own before every save, when the feature or the part stops for any reason, and by the game itself every night. `CompanyDirector.FootfallOf` is a function of the files and the day, so it is right whoever asks |
| A strike's shut doors, a closure for a missing food or alcohol licence | (a) `BusinessDirector.CloseFor`, business pressure's own closure | Business pressure reopens it on its hour, and the player can at any time. Only while business pressure is on |
| An audit after two late returns, an inspection where a licence is missing | (a) `BusinessDirector.StartIssue`, by business pressure's own rules: it may say no | It is business pressure's own issue from then on |
| An employee who takes a rival's offer leaves | The game's own resignation, as business pressure calls it (`BusinessGame.Leave`) | Not undone: they have left |
| An agreed raise, sick pay, overtime, a strike's payment, premiums, a claim paid, a campaign's cost, a discount day's takings, the accountant's fee, a rebate, a penalty, back-tax and its fine, the tax kept back, a licence's fee and fine | (c) sixteen transaction keys, `reallife_transaction_co_...`, each with a `_label` | Money that has moved has moved |
| A court case for tax kept back | `Justice.Summon` through `Justice/TaxCases` | It is one of Justice's own cases |

**Wages are never written.** `EmployeeInstance.hourlyWage` is saved, so a raise agreed here is `StaffFile.Raise`, hundredths an hour, kept by the game's own id for the employee (`EmployeeInstance.id`).

Each night it is paid for the hours the game counted that day (`workedHoursToday`, read on the hour event that opens the new day, before the game's run clears it; a delivery driver's fixed day as the game pays it) as one transaction per business, "Wages, agreed raise: {business}", taken whatever the cash is, as the game takes its own wages.

An employee who is no longer at a business of the player's is no longer read, and their file is dropped with the raise in it the next time the staff are read: on the day's pass, or when the Company app opens.
So is the file of somebody who has resigned (satisfaction at nothing) or is waiting to be replaced through the headhunter: the game gives their place to the newcomer under the same id (`HeadhunterPlan.ReplaceEmployee`), and the newcomer starts afresh.

**Sick pay follows the game's own absences.** `GameManager.NewDay` raises the day event before `EmployeeHelper.RunDaily` picks the new day's absences, so on the day's pass `isAbsent` still says who was off on the day that has ended: sick pay is paid for that day and dated on it.
Nobody is made sick by the mod and `nextSickDay` is not written.

**Deductible costs** are marked with `TransactionInfo.SetTaxDeductibleName`, which is how the game's tax reads its own.
The name is the cost's label as words ("Business insurance"), the way the game itself passes a business's name there, so the game's tax notice reads the same in a save the mod has left.

The game keeps those sums in its own list for its tax year; that list is the one thing of the game's saved data this feature adds to, through the game's own call and by the game's own rule.

**The day's books.** The game makes yesterday's statement before the mod's day, and the mod's costs booked for that day are put into it afterwards (`Economy/GameBooks.Refresh`, as often as asked, nothing counted twice).
The day's pass brings it up to date before reading it, so the city's lines and a dealership's sales booked earlier that morning are in the takings it works from (cash off the books, a discount day).
After the parts have booked their own costs for that day (raises, sick pay, overtime, a give-away) it brings it up to date and reads it again: the profit the app shows for yesterday and the run of losses the Planner counts are the Finance app's figures.
An average day's takings is over the statements of the last seven days that the game has, so a business younger than a week is not taken to have taken nothing before it opened.

**A campaign's report** counts what the campaign cost once, as its cost: its price, booked the day before its first day, and what a discount day or a giveaway gave away on its days are put back into the profit of the days the game's statement took them from.
The days before are scaled to as many as the campaign's days with figures, so that the two halves compare day for day.

**Cash off the books** does not touch the game's count of sales.
The tax the hidden share would bear at `gameVariables.taxPercentage` is paid back each morning, and only while the last year's income passes the game's own threshold (`TaxHelper.MeetsIncomeTaxThreshold`), since under it the game takes nothing.

## The numbers (`CompanyCatalogue`)

Strictness is Relaxed, Standard, Realistic, Hardcore; a figure with four values is in that order.
Money is at a price index of 100 where "at prices" is said, and follows `CityEconomy.PriceIndex`.

| What | Value |
| --- | --- |
| Going rate against the game's wage formula | boom +4%, slowdown -2%, recession -5%; 0.2% for each point of district income from 100; between 80% and 130%; then at today's prices (the index's whole move). What somebody is paid is counted the same way: the game's wage at today's prices, as the city's cost of living pays it (`StaffFile.Wage`), and the raise on top, so prices moving make nobody underpaid by themselves |
| Underpaid from, and for how long before asking | 15, 10, 8, 6% under; 10, 7, 6, 5 days |
| A request | 4 days to answer; "offer less" gives half; 14 days before the same employee asks again |
| Standing | starts 60, calm level 65; a day underpaid -1, a day paid fairly +1 up to calm; agree +10, less -8, refuse -18, an offer matched +5 |
| Sick pay | none, 50% or 100% of a day's pay; standing -3, 0, +2 a sick day; statutory is the default |
| A rival's offer | chance 0, 1.5, 2.5, 4% a day; to a skill of 50 or more, 8% or more under; the going rate and 5%; 3 days to answer, 2 days' notice; 21 days before the same employee, 7 before anybody |
| A strike | when 60% of two or more stand at 30 or lower; called a day ahead; 0, 2, 3, 4 days; 25% fewer customers, shut 8 hours from 9:00; 21 days' rest; the payment is 16 hours' pay each (+25 standing); a raise for all leaves nobody under 60; sat out, 40 |
| Overtime | offered on 30% of busy days (a campaign running or a boom); 2 days to answer; 3 days of 2 extra hours at 150%; 4% more customers; 10 days' rest; +2 standing |
| Insurers (three) | price 85, 100, 125% of list; excess 30, 15, 5% of the loss and 200, 100, 50 at least; pay 6, 4, 2 days after a claim; decline at 2, 3, 4 claims in a term |
| Cover (fire, theft, liability) | 12, 10, 8 a day and 0.4, 0.5, 0.3% of an average day's takings, at prices; counts a loss up to 20,000, 8,000, 5,000; district 0.2, -0.3, 0.2% a point of income, between 85 and 115% |
| Claims | 5 days to put in; each adds 25% to the next term; a term is a game year, 20 days at least; a policy lapses after 3 premiums missed running; 24 claims kept |
| Campaigns (flyers, radio, discount day, refit) | 150, 1,200, 0, 2,500 at prices; 3, 6, 1, 20 days; 5, 8, 12, 3% more customers; 12% at most |
| Campaign factors | local kinds: 0.5% for each point of district income on the business's side, between 80 and 120%; boom 110, slowdown 85, recession 70%; the same kind again loses 50% straight away, nothing after 10 days; a discount day gives away 15% of takings; 8 reports kept |
| The return | a quarter is three months of the city's (a twelfth of the year each, three days at least); 5 days to file; penalty 0.5% of the quarter's takings, 200 to 5,000, again after each 5 days, 3 at most; 2 late running bring an audit |
| Accountants (three) | 300, 700, 1,500 a month at prices; a rebate of 2, 4, 7% of the quarter's deductible costs; audit risk at 100, 80, 60%; the cheapest errs on 20% of returns (half a late penalty) and charges 50% more with cash off the books |
| Cash off the books | 0, 5, 10, 20, 30% of takings, 10% at most on Relaxed; risk +0.02 points a day for each per cent, -0.4 a day when clean, 25% a day at most; fine 25, 50, 75, 100% of the back-tax; court from 5,000, never on Relaxed |
| Licences (food hygiene, alcohol, late opening, signage) | 400, 900, 300, 150 at prices; 30, 30, 20, 40 days; reminder 3 days before; 5 days' grace for a new business; the fine is twice the fee; shut 6 hours for the first two; an inspection is asked for on 10% of days one is missing; late is open past 22:00 or before 5:00 |
| Kept | 64 businesses, 400 staff, 24 claims, 8 reports, 40 Planner rows; the game's statements are read 45 days back at most; a loss three days running is said |

Which business types sell food or alcohol, and which sell to the well-off or on price (for how well a district suits a local campaign), are four short lists of the game's type names in `CompanyGame`.
A business needs the late opening permit by its own opening hours in the game (`BuildingRegistration.scheduleDays`).

## In the save

One line, `reallife:economy_company`: `v=1^h:...^t:...^s:...^e:...^c:...^r:...` (`CompanyStore`): the header's seven numbers, tax's twenty, a record per business (its place, twenty numbers, three policies of seven, four licences of three, when the last campaign of each kind offered from outside ended, and what the running campaign has given away so far), a record per employee (the game's id, the place, ten numbers, a rival's name), the claims and the reports.
Parts are divided by `^` because a place, written as business pressure writes one, contains `~`.
Every figure is a whole number.

A newer version adds fields at the end of a record or a part with a new letter; this reader takes the fields it knows and skips the rest, puts a figure out of its bounds inside them and caps every list.
A second line, `reallife:economy_company_before_tests`, holds the first as it was before the first test tool changed it.

Both are registered in the feature list, so the self-check and "prepare this save for removing the mod" know them.
What is read from the game each day (a wage, a going rate, the hours, a business's name and books) is never saved.

## Public entries added to other features

Each is a new small file and one line that raises it; nothing else in those features was changed.

- `Events/FireLosses.cs`: `FireLosses.Billed(business, bill)`, raised in `FireDamage.Apply` after the repair bill.
- `Crime/BreakIns.cs`: `BreakIns.Happened(business, stock, repair)`, raised in `OffscreenCrimes.BreakIn`.
- `Business/BusinessEvents.cs`: `BusinessEvents.Closed(kind, place, outcome)`, raised where an issue closes (`BusinessIssues.Done`), and `BusinessEvents.Fined(kind, place, amount)`, raised where an inspection fines.
- `Justice/TaxCases.cs`: `TaxCases.File(amount)` and `TaxCases.Open(number)`, and one row at the end of `JusticeLaw.All`, the charge `tax_evasion` (a misdemeanour: fine 3,000, 12 hours of community service, 7 days of probation, 24 hours of custody, before the court's own bounds).
- `Wiring/Wiring.cs`: one line each in `Connect` and `Disconnect`.
  `Wiring/CompanyLinks` sets `BusinessGame.Ambient` to the city's economy's and the company's together and `News.Extra` to its own after the one that was there, and puts both back on disconnect.
- `Core/Settings.cs`, `Core/RealLifeOverhaulMod.cs`, `Meta/FeatureList.cs`: the options, the director and the feature, each directly after the city's economy's.
- `Economy/Finance`: nothing added.
  The Company app uses `FinanceTable`, `FinanceRow`, `FinanceCellView` and `FinanceText` as they are.

## Cost

Nothing runs per frame and nothing has an `Update`.
Once a second (the credit side's timer): a few comparisons.

- **The day's pass**, once a game day, on the game's day event.
  The player's businesses are found by one walk over the save's buildings (as the city's economy does) and each one's week of opening hours; the game's statements are walked back eight days at most (45 as a bound); every employee is read once, with one comparison of addresses per business each.

  With 30 businesses and 300 staff that is some ten thousand comparisons and a few hundred small strings: about a millisecond.
  The rules are one pass over the staff and one over the businesses.
  The footfall figure is written only where the company adds something, and the day's customers are not drawn again where the city's economy has just done so.

  A campaign's report walks the statements once for each of its days, on the day it ends.
  With "Developer diagnostics" on the pass prints what it took: "Company check (day N): ... reading the game x ms, the rules y ms, footfall and rows z ms".

- **The hour**: one multiplication per business, and a write of the figure where the company adds something.
  On the hour that opens a day, one look at each employee who has an agreed raise.
- **The app** is built once per city from the game's MyEmployees (as Finance is) and written when it opens, when a tab or a row is picked and when something has changed.
  Opening it reads the staff once.
- **Cash off the books** asks the game once a day, only while something is hidden, whether its tax falls due (`TaxCalculationHelper.GetBusinessSales`, one walk over the game's statements).

## Diagnostics

With "Developer diagnostics" on, lines that begin "Company check (" say: each day's pass with its milliseconds; each decision with its inputs ("staff, {name}", "staff, a rival's offer", "staff, a strike at", "cover, a loss at", "cover, {business}", "marketing, the report for", "tax, the audit risk", "tax, quarter N closed", "licences, {business}"); each transaction with how it was worked out ("transaction, {key}, {name}"); each footfall figure applied and restored ("footfall, {business}"); "before a save"; "restored, {why}"; and "options".

What the player does in the app is written whether or not diagnostics are on, as lines that begin "Company: ".

## Test tools

Nine buttons under the options.

Each puts one thing in place at the first business or employee it fits and opens the app on it:

- a raise request, a poaching offer, a strike tomorrow;
- a fire claim on a test policy (no fire and no bill);
- a campaign that ends at once with its report (flyers as if run over the last three days: nothing charged, no customers added, the report from the game's real figures);
- a return due now;
- an audit of cash off the books (it puts 6,000 in the audit's way if nothing was kept back, and really charges what it finds and may really open a case);
- a licence that lapses now;
- "put the company back", which restores the state from before the first test.

Money that has moved since stays moved.

## Checks

`EconomyCheck` compiles eight more game-free files and runs 649 checks in all (`Checks5.cs` has the 225 new ones): the going rate's bounds and direction over every index, state and district.
Raises, a rival's offer and a strike from scripted histories at each strictness (200 days of underpaid staff refused every raise: no strike on Relaxed, one within sixty days otherwise); overtime and sick pay.

Premiums, excess, claims, the cap and the insurer that will not renew.
Every campaign's bounds, repeats, and the report's arithmetic against a scripted series.
The return, its penalties and the run of late ones; the accountants.
The risk curve, back-tax, the fine and the court's threshold.
Licence terms, the free first term, renewing by itself.
The save's line across a version bump, with figures out of bounds and names with dividers in them.

And that the same seed gives the same history at every strictness.

## Not checked in the game (the game could not be run)

That the Company app's five tabs and its pages fit the white box as Finance's do (the longest page is a strike's or an accountant's, eight or nine lines and one row of buttons).
That the row of three buttons in a request for a raise reads well.
That the icon's glyph (`Icon-Shop`) is in memory on the phone.

That `workedHoursToday` still holds the ended day's hours on the hour event that opens the new day, as the decompiled `GameManager.NewDay` says.
That the game's tax notice shows the deductible labels as words.
That business pressure's closure and the company's strike read well together when both features are on, and what the player sees when a campaign's customers are cut by business pressure's bounds in a boom.

That a tax case opened by a summons is heard as Justice's other summonses are.
The milliseconds of the day's pass in a late save.
