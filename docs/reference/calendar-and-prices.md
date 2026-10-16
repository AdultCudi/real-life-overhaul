# The calendar and the price level

Every feature reads the date and every fixed amount the same way: the calendar from `Core/GameCalendar.cs` (its rules, game-free and checked by EconomyCheck, in `Core/CalendarRules.cs`), and money at the city's prices from `Core/Prices.cs`.

## The calendar

The game counts days and has a year of so many of them, chosen when a game is started (60 to 365; `gameVariables.daysPerYear`, 60 in a save that says nothing), but no months, seasons or dates.
Every part of the mod reads the calendar from `GameCalendar` (the rules, game-free and checked by EconomyCheck, are `CalendarRules`):

- **The year**: the save's own, or 60 when it says less than 8 days.
- **The month**: a twelfth of the year, three days at least (`MacroCatalogue.MonthDays`: 5 days in a 60-day year, 30 in a 365-day one), the city economy's month.
  Something paid by the month is priced by the day and paid a month's worth at a time (`GameCalendar.Monthly`), so the year's length changes nothing by the day, as the game's own daily rent and wages are not.

  Car insurance runs a month (`CarRules.PolicyDays`, its premium in proportion to the four weeks its figures are written for); childcare and private school are by the day (32, 79, 54), paid by the week and named by the month.

- **The seasons**: the weather's (the year begins in midwinter; winter to a sixth, spring to five twelfths, summer to two thirds, autumn to eleven twelfths, winter again), counted from spring where travel and property count theirs (`SeasonFromSpring`).
  `EventCalendar` and `Weather.SeasonOf` read them from here.

## The price level

Money at the city's prices is `Prices.At(amount)` (`Core/Prices.cs`: the mod's, not the economy's, as every feature reads it): an amount written at the start's prices (the city's price index at 100), at today's, rounded as prices are named (five cents under 10, fifty cents under 100, 5 under 1,000, 10 under 10,000, 50 above; at the start's level an amount is itself).

The level is the very factor the city adjusts the game's own rents, wages, shops and services by (`CityModel.Costs`, handed in by `Wiring/EconomyLinks` as `Prices.Factor`), so a price of the mod's and a game charge adjusted by the city are always at one level; within 1% of the start (`PriceRules.Quiet`, the city's own `AdjustQuiet`) it is the start's.
While the city's economy is off the level stays at the start's.

Every fixed amount of the mod's goes through it: fines, fees, fares, bail, lawyers and court costs, garage and car costs, bikes, clothes, drinks, family costs, hotels and trips, the banks' floors and caps, job and gig pay, the bounds of what a crime or an event can take, and the cash an event or a crime needs on the player.

The features with an index of their own (Brands, Company, Estate, Online, Services) read `Prices.Level`.
Texts name no amounts: the Guide's figures are `.Fact`s read from the code.
A life event's prompt fills `{cost}` (its price, `LifeEvent.CostNow`) and `{sum}` (the first fixed payout of its yes answer, `LifeEvent.Quote`) in its body, its answers being plain keys of the game's box (CatalogueCheck fails an event text that names a `$` figure).

Prompts and toasts are filled by the code that shows them.
The licence quiz fills today's fines (`TheoryTest.Figures`).
