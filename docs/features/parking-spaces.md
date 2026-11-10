# Parking spaces

## In a sentence

A player buys or rents a single parking space in the car park under a building, without buying or renting the building: a car left there is parked and charged by the game's own rules, and the charge for it is paid back each night; the rival tycoons hold spaces by the same rules and the city's people hold the rest.

A player asked for it: "buy one parking space in a building".

## Where the code is

- Folder: [`Scripts/ParkSpaces`](../../Assets/Mods/RealLifeOverhaul/Scripts/ParkSpaces/); its pages stand with the Property app's own, in `Scripts/Ui/Estate` and `Scripts/Economy/Estate` (a file's namespace is its folder's).
- Read first: `SpaceCatalogue.cs` (every number and where it comes from) and `SpaceRules.cs` (every rule, game-free, run by EconomyCheck's `BizAsksChecks.cs`), then `SpaceDirector.cs` (the state, the hour, the day and the player's calls) and `SpaceGame.cs` (the only file that touches the game).

```
SpaceCatalogue        the numbers: the hourly charge, the term and share of a rent, the yield, the fee, the limits, the city's share, the tycoons' pace
SpaceRules            game-free: a car park's spaces, who holds them, the rent and the price, taking and giving up, the rent's terms, the car-hours, a car park gone, the tycoons
SpaceState            a car park as the rules see it, a space held, a tycoon's move, the save's state
SpaceStore            the save's one line, "reallife:parking_spaces"
SpaceGame             the clock, the city's buildings with a car park, the player's cars standing in one, the cash through BizBooks.Book, the notices; SpaceBridges
SpaceDirector         the one component: a second's look, the game's hour, the day's pass, Buy, Rent, Sell, SetEnding, the tycoons' line
SpaceDirector.Tests   Options, Tests
Ui/Estate/SpacePages  game-free: the car park's row and page on the kit, and the words built from the state (SpaceWords); the checks and previews lay out the same page
SpaceText             the same words in the game's language, and the feature's entry with its Guide facts (SpaceGuide)
Economy/Estate/EstateApp.Spaces   the Property app in the game's frame: the Market tab's rows and page, the Mine tab's rows
Ui/Estate/EstateLive.Spaces        the Property app on the kit: the same rows, SpacePages' page, the game's question box before money moves
```

Hooks in other files, each a line or a few: `EstateApp.cs` and `EstateApp.Property.cs` (the rows and the page), `EstateTables.cs` (a kind for the two filters), `EstateLive.Market.cs` and `EstateLive.Mine.cs` (the rows and the page), `UiScreens.Estate.cs` (the page's keys), `Rivals/RivalBridges.cs` and `RivalPage.cs` (the tycoons' line), `Economy/Finance/DayRules.cs` (the capital lines), `Wiring/Wiring.BizAsksLinks.cs`.

## What the game does

- A building may have a car park under it (`CityBuildingController.undergroundParkingEntrance`, an `UndergroundParkingEntrance` with a `parkingNumber`): the game has five, numbered 1 to 5, under residential and office buildings, and the mod's own copies of such buildings carry one with a number of their own ([New buildings](new-buildings.md)).
- A car driven in is kept there by the game (`VehicleInstance.streetName` is `ba:street_parking`, `streetNumber` the car park's number, `parkingState` Legal). No space comes with a building: every car in every car park, the owner's too, is charged `ParkingSimulator.UndergroundParkingHourlyFee`, 25 an hour (`ParkingSimulator.RunHourly`), taken the next morning as "public parking" (`RunDaily`). A car being driven and a thing carried by hand are not charged.
- The game's hour is `GlobalEvents.onNewHour`, raised just before `ParkingSimulator.RunHourly` in the same frame.

## What the feature does

- **A car park's spaces** (`SpaceRules.Capacity`): what the building code asks of the building over it, by its use and floor area (`PlansRules.ParkingNeeded`, `BuildCatalogue.ParkingPerSpace`: a space for every 120, 60, 80 or 200 m2 of homes, shops, offices or storage), at least 8 and at most 60. Spaces are numbered from 1.
- **Who holds them.** The player and the rival tycoons by records in the save; the city's people by a rule with nothing saved (`SpaceRules.CityHolds`): each space is drawn afresh every 45 days, on a day of its own, with the chance of the district's share (`CityShare`: 500 thousandths and 20 more for each dollar an hour the street's parking costs there, `NeighborhoodData.parkingPrice`, within 550 and 900). A space nobody holds is free, and the lowest numbered free space is the one offered.
- **The rent** (`RentMonth`): a term is 30 days, and its rent is 17% of the hourly charge round the clock for the term (the term and share the mod's surface car parks lease a bay by, `CarParkRules.LeaseDays` and `LeaseShare`), by what the district does to a property's value (`EstateRules.DistrictValue`, 840 to 1160 thousandths), named in tens: 3,060 in a middling district.
- **The price** (`Price`): the rent over a year of the save's length at the property market's yield for storage (84 thousandths, `EstateCatalogue.YieldMiddle`), by the property market's level (`EstateState.Real`; 1000 where the property business does not run), named in hundreds: 72,900 in a year of 60 days at the market's long-run level, 145,700 in one of 120.
- **Buying** (`SpaceDirector.Buy`): refused before any money (`SpaceRules.MayTake`) with the option off, in a Going Public session, for a car park that is not there or under a building of the player's own, at 12 spaces in all or 4 in one car park, with no space free, or without the cash; then the price is taken through `BizBooks.Book` and the space is the player's. The seller is the building's owner: the rival the game names as its owner (`BuildingRegistration.buildingOwnerRivalId`), otherwise the building's landlord. The game keeps no purse for a building's owner, so the money leaves the player's books as the price of a building bought from the city does.
- **Renting** (`Rent`): the same refusals, the first term paid at once. On the day after a term the next is taken; without the cash, or once the player has given it up (`SetEnding`), the space goes back. Nothing of a term is paid back, and time the option was off or the save was not played is never asked for (`RentPaid`).
- **Selling back** (`Sell`): the owner pays the lower of what was paid and today's price, less the property market's agent's fee of 15 thousandths (`SpaceRules.Back`). Never as much as was paid, so no buying and selling makes money, whatever the market does between. A rented space is never sold.
- **The car parks there by the game's own rules.** Nothing of the game's parking is changed. Each game hour (the game's own event) the director counts the player's cars the game is charging in each car park, by the game's own test (`SpaceGame.CarsIn`), as far as the player's spaces there go (`Entitled`, `Covered`), and at the day's turn the charge for those car-hours, 25 each, is paid back as a line of its own (`reallife_transaction_space_refund`). An hour is counted once and a day's hours are paid back once; no more than a day of every space of the largest car park is ever owed.
- **A building of the player's own.** Its free spaces are the player's already: nothing is bought or sold there, and the charge is paid back for as many cars as there are spaces nobody else holds. A space rented there before the building was bought ends at its term with nothing more to pay.
- **A car park that goes** (a building demolished, or rebuilt as another: `Seen`, `Gone`, `GonePay`). A space whose car park is not found is marked on the day's pass; after 3 days missing it is settled: a bought one at what was paid (no fee and no more), a rented one for its unused days. One that stands again before then (rebuilt with its number, or stood late at a load) keeps its spaces. A building that is sold keeps its car park and its spaces; only the seller of the next space changes.
- **The rival tycoons** (`SpaceRules.RivalLook`, the day's pass, never in a session). Every 20 days, each on a day of their own, a tycoon in play without a space buys, one time in two, the lowest free space of a car park drawn for them at the player's price, paid from their worth and only while it stays over their floor (the worth the rivals raise them to each day, `RivalRules.At`); one who has held a space 90 days sells it back, one time in four, for what the owner pays the player. One space each. Never in a building of the player's: its spaces are not sold without the player. Their space is a line on the rich list and on their panel (`RivalBridges.SpaceLine`).
- **Where the player finds it.** The Property app, where buildings are bought: each building with a car park is a row of the **Market** tab (the price, and "3 free", "You hold 2", "Full" or "Your building"); its page gives the price, the rent, the seller, who holds the spaces, **Buy a space** and **Rent a space** (each asked first in the game's question box) and the player's spaces there with **Sell it back** or **End the rental**. Every space held is a row of the **Mine** tab, where the player's property shows. The same in the game's frame and on the kit.

## Renting beside buying

Renting by the term belongs beside buying: the mod's surface car parks already let a bay by a term of 30 days ([Construction](construction.md), "Parking lots"), so a space under a building is rented by the same term and share, and its price is that rent at the property market's yield.

## The option

**Parking spaces to buy or rent** (`parking_spaces`, on), under Property. Off: nothing is counted, paid back, taken or sold, the tycoons do not look, and every car in a car park pays by the hour as before; the spaces stay in the save and count again when it is on.

## Going Public

A space is the player's own record, and what is paid back is the player's own charge. A car park is shared, and nothing is sent, so in a session no space is bought, rented or sold (refused before any money) and the tycoons rest; the spaces already held go on covering the player's own cars. Who of the city's people holds a space comes from the save's seed and the day.

## The save

One line, `reallife:parking_spaces`, version 1: the header (the seed, the last day and hour, the sums paid back and paid), the spaces held (the car park, the space, who, what was paid or the rent, the day, the day paid to, given up, missing since) and the car-hours of today not yet paid back. A save from before has no line. A line from a newer version is read as far as it goes; numbers are written in the invariant culture. In the day's result a space bought, sold back or settled is capital; its rent and the charges paid back count.

## Cost

Nothing per frame and no Update. A few comparisons a second; once a game hour a walk of the player's vehicles for each car park where the player has a space; once a day, at a load and when the page opens a walk of the city's buildings (no search of the scene).

## Tests

- Options, Test tools, each writing a `Parking spaces check (...)` line: **rent a space in the first car park with one free** (the real rent), **count this hour and pay the day's charges back now**, **a rival tycoon's look now**.
- `EconomyCheck --bizasks` (`BizAsksChecks.cs`): the numbers against their sources; a car park's spaces, the city's share and who holds a space; the rent and the price; no profit from buying and selling over two hundred swings of the market; every refusal before money; no space twice; the limits and a full car park; the rent's terms over more than a year, after time away and across a save in the middle of a term; the car-hours counted and paid back once, across a save; a car park gone; the tycoons over 900 days; the save (old, newer, damaged, a decimal comma); the page and each button; the sources (money only through the one call, the refusal before the money, a session, the option off, the game's own hour); the texts in eleven languages. Each rule was broken once to see its check fail.

## Not seen in the game

Everything above is built and checked offline. Not seen in play: the rows on the Market and Mine tabs, the car park's page and the question boxes, the charge coming back as its line the morning after a car stood in a space, the number of spaces each of the game's five car parks comes to, and a tycoon's line on the rich list.

## Left out

- A building of the player's own sells no space to a tycoon or to the city's people: the mod has no call by which a rival buys part of a player's building without the player (`SpaceRules.RivalLook` passes such a car park by).
- The seller's money: the game keeps no purse for a building's owner (`BuildingRegistration.buildingOwnerRivalId` is a name), so the price is not added to anybody's worth.
