# Household staff

## In a sentence

A cleaner, a cook, a live-in butler and a mobile mechanic taken on for each of the player's homes through a phone contact, paid each visit at the city's going wages, seen at work at home, who keep the home clean, cook dinners into the fridge and wash, repair and service the cars at home.

## Where the code is

- Folder: [`Scripts/Home/Household`](../../Assets/Mods/RealLifeOverhaul/Scripts/Home/Household/).
- Read first: `HouseholdRules.cs` (every figure, game-free), `HouseholdDirector.cs` (the hour's round) and `HouseholdGame.cs` (the only file touching the cars, the floor and the fridge).

| File | What it is |
| --- | --- |
| `HouseholdRules` | the four kinds (days, hours, wage share, what a visit does), the going rate, wages, shopping, the call-out, the cook's dishes, the agency's candidates, who is shown at home, unpaid visits, the hours a late look makes up (game-free) |
| `HouseholdState`, `HouseholdStore` | the staff of a save and its line in modData, `reallife:home_household` (game-free) |
| `HouseholdDirector` | the one component: a look every five seconds (once a second in the staff's home), the hour's round of visits, hiring and letting go; `.Tests` the test tools |
| `HouseholdGame` | the clock, the home, money through the game's call, the game's wage for a cleaner, the home's tidiness and the fridge's portions (home life), the cars at home, wash, repair and service |
| `HouseholdPhone` | the "Household staff" contact and its call in the game's call panel |
| `HouseholdAtHome` | the member at work shown at home: one character, the roommate's kind of cast (`Roommates/RoommateCast`) |
| `HouseholdText`, `HouseholdGuide`, `HouseholdBridges` | the texts; the feature's entry, its option and test buttons; the doors to other features |

## How it talks to other features

- Home life: the cleaning is home life's own tidiness on the game's floor dirt (`HomeTidy.Change`, for the staff's home in particular), the cook's portions are home cooking's leftovers (`HomeState.PutBy`, at most `HomeBalance.LeftoversMost`, the dishes from `HomeBalance.Dishes`), reheated at a cooker as the player's own.
- `Wiring/Wiring.HouseholdLinks.cs` fills `HouseholdBridges`: the wage market from the economy (`StaffRules.Market`: the price level and the cycle, as the player's businesses' staff are priced), a dealership's stock and the partner's car left alone (`Dealerships.Stock`, `Family.PartnerCar.Is`), and car ownership's workshop service done where the car stands (`CarOwnership.ServiceWhereItStands`, the same rule of what is due and what a service does as the repair bay's, `Due` and `Done`).
- The home and the payment are the roommate's own lookups (`RoommateGame.HomeAt`, `AnyHome`, `Pay`).

## What a player does

1. Ring the **Household staff** contact (Contacts). With no home of their own the call says so.
2. The call lists who works for them, under each home's address, and offers **Take someone on** and **Your staff**.
3. Taking someone on asks first which home they are for when the player has more than one (one home a page, with who works there now: **This home** or **Another home**), then shows one person at a time, with their age, kind, days and hours, what they do and what they ask (an hour, a visit, a week; the mechanic's call-out and wash price). **Take them on** or **Someone else** (the next person, of the next kind that home has nobody of). One of each kind at each home, and at most four homes with staff (`HouseholdRules.HomesMost`, so sixteen people at most).
4. **Your staff** goes through the staff one page each (their home, days, visits and pay so far): **Move or let go**, then **Move them** (one page for each home of the player's that has nobody of their kind, within the four homes: **Move them here** or **Another home**) or **Let them go**.
5. From the next day they work, each visit is paid as it begins and its work done as it ends; at 21:00 the contact texts the day's note.
6. At home during a visit the member at work is seen: the cook at the cooker, the cleaner mopping and sweeping, the butler at either.
7. Letting someone go is at once; nothing more is owed. A move is at once too: they work at the new home from then on, at the same wage.

## The kinds

| kind | days | hours | wage | a visit |
| --- | --- | --- | --- | --- |
| cleaner | Monday, Wednesday, Friday | 10:00 to 13:00 | the going rate | the home's tidiness up by 60 + skill x 0.4 points (on the game's floor dirt where the home has it) |
| cook | every day | 17:00 to 19:00 | 1.25 x | buys the food (6 a portion at the start's prices) and cooks one of home cooking's dinners (home-cooked dinner, vegetable soup, home-made pizza, roast dinner, stew; never the same two days running); its portions go in the fridge as far as it has room |
| butler (lives in) | every day | 08:00 to 18:00 | 1.5 x | cleans as the cleaner, cooks as the cook (when the fridge has room), washes every car at home |
| mobile mechanic | Saturday | 10:00 | by the job | every car at home washed (50, the game's own wash), repaired (the game's repair bay's price, `CalculateRepairCost`) and serviced (car ownership's workshop at its prices: service, tyres and brakes under 35 %, the inspection), plus a call-out of 95 at the start's prices; nothing with no car at home |

**The going rate** is the game's own hourly wage for a cleaner of skill 50 (`EmployeeHelper.CalculateHourlyWageForSkill`, with the game's salary multiplier), times the city's wage market. A candidate asks the going rate times their kind's share times 0.8 + skill x 0.004 (skill 40 to 95), in whole five cents; a visit is its hours at that rate.

**At home** is the home of the player's a member works at, chosen when they were taken on or moved (`StaffMember.HomeStreet`, `HomeNumber`); each member's visits, work, sightings and cars follow their own home. A city flat and a house in the Hamptons are both the game's residential buildings and both homes here. A car is at home in that home's own garage (its address) or parked within 60 m of its door (for a Hamptons house: on its plot, the game's `HamptonsHouse.plotBounds`, or within 10 m of it), the player's own (the save's list), a motor vehicle, not a hand truck, not a dealership's stock or the partner's car, and not the car the player sits in.

**Paying.** A visit is paid through the game's own money call (`GameManager.ChangeMoneySafe`, one line in the statement: Cleaner, Cook and shopping, Butler and shopping, Mobile mechanic). Not enough money: they do not work that day (a notice); two unpaid visits in a row and they leave. A cook with the fridge full, or with home cooking off, takes the evening off unpaid. A home given up: each of its staff moves to the first home of the player's, in the save's order, that has nobody of their kind (the home given up holds no place among the four; `HouseholdRules.Rehome`), with a line in the day's note; with none they leave, with a notice.

**Time.** Each game hour is gone through once: a visit's end before a visit's start, the note at 21:00. A late look (a long sleep) makes up the hours missed, three days at most; a load or the option switched back on makes nothing up, so nobody is paid for days they were not there.

## Seen at work

Only the staff of the home the player is in are seen there (`HouseholdRules.OnShow` takes the home). One character at a time (the butler before the cook before the cleaner), made once a session and kept on the shelf (`RoommateCast`, its own object "RealLifeOverhaul Household Staff"), dressed from the member's look seed. Put at a place beside the kitchen's things (a cooker, an oven, a sink, a fridge) for the cook and the butler from 15:00, else on clear floor round the player; never within 1.8 m of the door, and never put down where the player is looking unless they came in in the last four seconds. Their clips change every 22 to 40 seconds: `cook_stir`, `counter_wipe`, `mop_floor`, `sweep`, the game's standing pose when none can be had. The mechanic works at the cars outside and is not shown. Nothing of this is saved or sent.

## Going Public

Every candidate comes from the save's own seed (`RoommateRules.Roll` and a seeded `System.Random`); the hours follow the shared clock; only the save's own cars and a home of the player's own (`GoingPublic.Mine`) are touched; nothing is sent over the network.

## The save

`reallife:home_household`, version 2: the seed, the last hour gone through, the first person's home (for an older reader), the candidates put forward by kind, the day's note, and per person kind, name, look, skill, cents an hour, the day taken on, visits, unpaid in a row, paid in all, the visit under way, its dish, its charge and their home. Version 1 had one home in the header for everybody: read by version 2, that home becomes each member's own and nothing else changes. An older version of the mod reading a version 2 line keeps everybody at the first person's home, one of each kind. A save loaded without the mod keeps the line and nothing else of the game is changed but the tidiness, the portions and the cars the staff saw to.

## Tests

Options, Test tools, Household staff: take on one of each; a day of the staff's work now; see the staff at work at home; the mobile mechanic's round now; household staff at a house in the Hamptons (stand in one: found as home, a cleaner's visit on its floor, the cars on its plot, then the cleaner shown, a second line saying whether inside the walls); household staff at two homes (a cleaner at each, a move refused, each shown at their own home only, the save's line read back); put household staff back as before. They work at the home the player is in when it has staff, else the first home with staff. Each writes one `Household staff check (...)` line with PASS or FAIL. With Developer diagnostics on, `Household staff check (at home)` when somebody is shown.
EconomyCheck's `HouseholdChecks.cs` covers the rules, the save and the texts (`--household` alone).

## A house in the Hamptons

The game builds a Hamptons house (`Building.IsHamptonsHouse`, size t) as the city building itself on a plot of its own, not an interior scene loaded at a door: walking onto the plot enters the house (`HamptonsHouse.OnEnterPlot`), garden and drive included, and the player is put on the indoor navigation mesh there while the street keeps the city's. So: the staff's look is in whichever staffed home the player is in; a place to be seen at work must be on the indoor mesh and inside the house's walls (`BuildingManager.interiorBounds`), looked for beside the house's own furniture first (a grill by the pool is not the kitchen); the house's floor cells, cleared when it became the player's and laid out by the game only on entering, are laid out before a cleaning through the game's own helper, which reads the city building for a Hamptons house (`BuildingCleanlinessHelper.GetDirtSpotsForBuilding`); a cleaning is read back from the home cleaned (`HomeTidy.ReadOf`); the cars at home are those on its plot.

Not verified in the game: the call's pages in the game's panel, the character's clips at a home's cooker, a mechanic's round on a car parked in a Hamptons garage, where a Hamptons house's interior bounds fall against its garden.
