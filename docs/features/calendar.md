# The Calendar

## In a sentence

An app on the phone with the month the player is in, as the year they chose divides it, the days money moves on, their own time and what is on in the city, each read from the feature's own schedule.

## Where the code is

- Folder: [`Scripts/Calendar`](../../Assets/Mods/RealLifeOverhaul/Scripts/Calendar/).
- Read first: `CalendarDirector.cs` (puts the app on the phone, asks the sources for a month) and `MonthViewRules.cs` (the grid, the repeats, the birthdays, the order; game-free).
- The months are `Core/CalendarRules.cs` (`MonthOf`, `MonthStart`, `MonthEnd`, `MonthLength`), the stock market's months (`MarketCatalogue.MonthOf`).
- Its sources of the mod's features are in `Scripts/Wiring/Wiring.CalendarLinks.cs`, `Wiring.CalendarMoney.cs`, `Wiring.CalendarWork.cs` and `Wiring.CalendarPeople.cs`.

```
CalendarRules (Core)  the months of the year: twelfths by the day of the year      EconomyCheck/CalendarAppChecks.cs
MonthViewRules        the grid of weeks, repeats, birthdays, order, a day's sums    EconomyCheck/CalendarAppChecks.cs
CalendarList          the entries of a look and what moves every night; ICalendarSource
CalendarGame          the game's own days: the tax year, deliveries, the player's birthday, rent from buildings
CalendarDirector      the app on the phone, the sources, a look at a month
CalendarApp           the frame: the Month grid, the Day table, the headline, the buttons
CalendarApp.Box       the white box: a day's page, an entry's page with its app
CalendarGrid          the month's cells, copies of the game's table row
CalendarText          month names, money and hours as the game writes them
CalendarGuide         the feature's entry, the options block, the Guide page
CalendarDirector.Tests  "Show this month, and write it in the log"
```

## The months

The game has days and a year of so many of them (60 to 365, chosen when a game starts) and no months. A month of the calendar is the days whose day of the year falls in the same twelfth of the year: 5 days each in a 60-day year, 10 in a 120-day year, 30 in a 360-day year, and 30 or 31 in a 365-day year (31, 30, 31, 30, 31, 30, 30, 31, 30, 31, 30, 30). The first year is days 1 to the year's length; its first month is January, in midwinter as the weather has it. A year shorter than twelve days would have a month a day.

These are the stock market's months: its trading bots' statements come at the end of them. Something paid "by the month" in the mod counts a month of `CalendarRules.MonthDays` (a twelfth of the year, rounded down) from its own first day, so a bill or a policy falls on its own day, which the calendar shows, whatever its month.

The headline says the day the month ends on ("March, year 1. This month ends on day 90."), in the game's own day numbers.

## What it shows, and where each date comes from

Every source reads and changes nothing. Asked only when the app is shown.

| What | Where it comes from |
| --- | --- |
| Rent paid, wages, the game's loans, the mod's accounts, fines | Every night: `Economy/Books.Tomorrow`, as the Finance app adds them up. Not marked on the days |
| Rent from the player's let buildings | Every night: `RealEstate.DailyIncome`, as `RealEstateHelper` pays it |
| The tax year | The game's `TaxHelper.RunTaxPeriodBoundary`: each day that is a whole number of years |
| The tax bill due | The Planner's tax source (`TaxHelper.GetCurrentTaxesDueDay`) |
| Power, water and internet bills | `UtilitiesState.MonthStart` and a month of days, as `UtilitiesDirector.Day` bills; due `UtilitiesRules.DueIn` later. A bill sent: its due and cut days from the Planner's utilities source |
| Card statements, minimum payments | `CreditAccount.NextDay` every `CardStatementDays`, `DueDay` with `MinimumLeft` |
| The private lender | `SharkDebt.DueDay`, the sum as the Finance rows work it out |
| Car insurance, registration | `CarRecord.InsuredUntil` + 1 and every `CarRules.PolicyDays`; `RegisteredDay` + a year |
| Web hosting | `WebShop.HostDue` and every month |
| The paper delivered | `LedgerState.PaidUntil` + 1 and every week; the last morning of one cancelled |
| Newsstand, dividends, bonds, results, bills due, investors, the sale of a business, Standing's invitations | The Planner's sources of those features |
| The tax quarter, a return due, the accountant | `TaxFile.QuarterFrom` + `QuarterDays`, `ReturnDue`, `AccountantNext` and every month |
| Business cover, claims, licences | `Policy.Until` + 1, `Claim.PayDay` (filed), `Licence.Until`; daily premiums every night |
| A tech firm's managed service | `Contract.LastBilled` + 30 days and every 30 to `End`, as `FirmsRules.Work` bills; product subscriptions every night, a thirtieth of the month's |
| Review, profit share, benefit, pension | `Job.ReviewDay` + `LifeRules.ReviewEvery`; `(day - Hired) % 7 == 6`; `BenefitPaid` + 7 to `BenefitUntil`; the pension every night |
| Trading bots' statements | The last trading day of the market's month (`MarketEngine.BotClose`) |
| The central bank's decisions | `CityModel.DaysToTurn` and every month of the city's model |
| Shifts, working days, interviews, contracts, classes, exams, graduation | The Planner's work, profession, freelance and study sources |
| Court, custody, licences, family appointments, plans with friends, a checkout, sick notes | The Planner's sources |
| City events, elections, the council | The Planner's event and civic sources |
| Deliveries | The game's `FurnitureDeliveryContracts` (paid on delivery) and `vehicleDeliveryContracts` |
| Birthdays | The player: `CharacterData.ageInDays` as `PlayerHelper.IncreasePlayerAge` counts it; people met: `SocialPerson.Birthday`; children: `ChildRecord.Age` as `Children.Grow` grows it |
| The children's week | `FamilySave.LastWeekDay` + 7, `Children.WeekCharges` |
| The player's own entries | The Planner's notes |

The Planner's alerts (a payment missed yesterday, a grade changed, what waits at a business, the samples) are left to the Planner (`Wiring.CalendarLinks.PlannerDays`, its table of sources).

## The app

- Folder: Social and life (`PhoneLayoutRules.Defaults`), beside the Planner in the dock: it is the month of the player's life, money and time together; Money holds the apps that move money.
- **Month**: seven columns, Monday to Sunday (day 1 is a Monday), a cell a day: the day of the month, money in (green) and out (red) with a known amount, how many other things. Today in the game's yellow, the chosen day in the table's chosen colour, past days fainter.
- **Day**: a table of the chosen day: when, what, the money, the app; filtered by money, time and the city; a row's page has a button that opens its app (`PhoneApp.OpenById`).
- The box: the day's money, time and city (three of each, and how many more), the month's end and the money still to come, what moves every night.
- **Previous month**, **Next month**: back to the save's first month, on to twelve ahead.

## Cost

One timer comparison a frame. Once a second: the app put on or taken off with its option; while it is open, the game's day compared with the page's. The sources are asked when the app opens, a month is turned or a day begins while it is open: a month's look is a few hundred entries from pools, sorted once. Nothing is saved or sent.

## Going Public

Everything is read from the local save and the mod's own state: each player sees their own. Deliveries count only for buildings that are the player's (`GoingPublic.Mine`).

## Options and tests

- `calendar` (Calendar on the phone, on): off, the app is not on the phone.
- Test: "Show this month, and write it in the log": every entry of today's month, each source's count and the month's check line.
- With Developer diagnostics on, each look writes "Calendar check (...)": the month's days against the year, the count from each source, the time.

## Offline checks

`EconomyCheck/CalendarAppChecks.cs`: the months of every year length from 8 to 400 days (they make up the year, each a twelfth, the market's months every day); the day each month ends on; the grid (one to six weeks, each day in one cell of its weekday); each schedule's days against the feature's own daily rule run day by day (the tax year, the bills, the card's statements by `AccountRules.RunDay`, a managed service by `FirmsRules.Work`, the tax quarter by `TaxRules`, the central bank by the city's count, the player's, people's and children's birthdays, the profit share, the bots' statements); an empty month; the list's limit and order; no allocation on a look again; the sources' use of the rules; the texts in every language. `ContractCheck/PhoneLayoutChecks.cs`: the folder.
