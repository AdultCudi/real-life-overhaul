# Rivals seen around the city

## In a sentence

The game's rival owners and the rich list's tycoons each keep a seeded week that follows how their business is really doing; they stand at the places near the player as people of the living city, their car passes in the traffic, they can be greeted, and where they were seen is written on Flare and in the City Ledger, with the player noticed in turn.

A player asked for it: rival owners driving luxury cars, eating out or at the beach and the Hamptons, by how their business is doing.

## Where the code is

Folder [`Scripts/RivalsSeen`](../../Assets/Mods/RealLifeOverhaul/Scripts/RivalsSeen/), namespace `RealLifeOverhaul.RivalsSeen`.

| File | What it is |
| --- | --- |
| `SeenCatalogue` | every figure and every locale key: game-free |
| `SeenModel` | a rival as read today (`SeenSubject`), what is kept of them (`SeenRecord`), a sighting (`SeenNote`), the save's file, what the rules ask of the city (`ISeenCity`): game-free |
| `SeenRules` | form and means from real figures, the weekly mark, the car, the week (`Where`), who the world may show, a greeting: game-free |
| `SeenDay` | the day's pass, the hour's pass, the player seen, the paper's lines: game-free, called by the director and by the checks alike |
| `SeenStore` | the save's line `reallife:rivals_seen`: game-free |
| `SeenDirector` | the state and its timing; its parts `.World` (who stands where, the panel, the greeting) and `.Tests` |
| `SeenGame` | the only file touching the game: the game's rivals, cars, eating places and doors, the player's car and table, a car of the traffic pool put on a lane |
| `SeenDialog` | the greeting in the game's dialogue panel |
| `SeenText`, `SeenGuide`, `SeenBridges` | the built words, the feature list's entry and options, what it asks of the rest of the mod |

Small hooks: `Wiring/Wiring.SeenLinks.cs`, `Social/Flare/FlareDirector.Society.cs` (a line on the City Ledger's account), `Rivals/RivalBridges.SeenLine` and `RivalPage` (a tycoon's panel), `Ui/RivalsViews` and `Ui/Rivals/RivalsScreen` (the game's Rivals app on the interface kit).

## Who is seen

- **The game's own rival owners** (`RivalsHelper.GetAllRivalData`, the people of the game's Rivals app), read in the order of their ids. Their figure is the takings of the last seven days of everything they run (the game's `dailyIncomes`); the week before comes from the game's own history (`RivalState.weeklyIncomeHistory`, `numberOfBusinessesHistory`). Their busiest business gives their firm's name, their office and their district. One who owns a house in the Hamptons is known to (`Building.IsHamptonsHouse`).
- **The rich list's tycoons** (`Scripts/Rivals`), once they have come forward, through `SeenBridges.Tycoons`: their worth, the worth at the rich list's last weekly mark, whether a counter of the player's holds them back (`SlowUntil`, `BarredUntil`), and the game's rival business their first outlet stands on (`RivalOutlet.Seat`) as their office.

Somebody gone by their source's own rules is seen nowhere and written about nowhere: a game rival who is defeated (`RivalsHelper.IsRivalDefeated`), a tycoon who was taken over. A tycoon who has not come forward is not on the list at all. No system of the mod puts a rival in prison, kills or retires one, so those are the cases there are.

## How the business is doing

Read once a week for each rival (`SeenRules.Mark`: on the first look and then every seventh day), so a rival's week is one week.

| | thriving | in trouble | otherwise |
| --- | --- | --- | --- |
| a rival owner | the week's takings 8% up or more, or a business gained | 8% down or more, or a business lost | steady |
| a tycoon | their worth 8 per mille up or more in the week | 1 per mille down or more, or held back by a counter of the player's | steady |

With no week before to compare with, a rival is steady: nothing is guessed.

Their means are five tiers of the figure at the start's prices: an owner's week of takings from 0, 15,000, 60,000, 200,000 and 600,000; a tycoon is never under the fourth tier and is in the fifth from 100 million.

## The car

The ladder is the game's own cars that a showroom deals in, the cheapest first (`DealerGame.Models`), in six rungs. A rival's rung is their tier, one up when thriving and one down in trouble; which car of the rung is their own pick for good. A car is kept at least 14 days. The first car on record is taken quietly; a change after that is a sighting, "seen leaving in a new ..." for a dearer one and "has let the ... go" for a cheaper one. On a day that already has its three sightings a change waits for the next day.

## The week

`SeenRules.Where(seed, rival, form, tier, house in the Hamptons, gone, day, hour)`, every draw a hash of the save's seed, the rival, the day and its purpose. A week starts on Monday, as the game's does.

| when | thriving | steady | in trouble |
| --- | --- | --- | --- |
| working day, 9:00 to 18:00 | the office, until 17:00 | the office | the office |
| 12:00 to 14:00 | lunch out every day | lunch out 600 per mille of days, else the office | a table for one 250 per mille of days, else the office |
| 18:00 to 21:00 | | | still at the office |
| 19:00 to 21:00 | dinner out 700 per mille | dinner out 300 per mille | never |
| Saturday | the Hamptons 10:00 to 20:00, the beach 11:00 to 16:00, with a house there or from the fourth tier; otherwise lunch in town | the beach 11:00 to 16:00 on 400 per mille of Saturdays, else lunch by the working day's roll | the office 9:00 to 18:00 |
| Sunday | the beach 11:00 to 16:00, there from 10:00 | home | home |

Where they eat is one of the city's eating places (a business whose type is a restaurant, a coffee shop or a cafe) of their district, open at that hour by its own timetable. The places of a district are ordered by the rent of their address; a grade (modest, middling, smart, by the tier, a step up thriving and a step down in trouble) has its third of them and the day's pick says which. A tycoon with no outlet eats in one of the city's districts, theirs for good by the seed.

In a city without the Hamptons (`CityCheck.Here`) nobody goes there.

## In the world

`SeenDirector.World`, once a second from the director's timer, nothing per frame.

- When the hour turns, who is somewhere and the building each is at are worked out once (`SeenRules.Due`, `SeenGame.Venue`).
- Every 6 seconds each place is set against where the player is. A rival whose place is 18 to 110 metres away, out of the camera's view, is put on the pavement by its door (`JobSpots.Front`), one a look.
- They are **one of the living city's own people exactly as that pool dresses them** (`CityPeople.Take(CityLook.Civilian)`): no character, face, body, hair or look of their own is made. So a rival has no face of their own in the street, and the living city (`Settings.CityLife`) must be on for anybody to stand there.
- At most 2 stand at once, and never when the living city has only 2 people left free.
- Their car: an idle car of the traffic pool of that very model is put on the lane that leads past the place, 70 metres or more back and out of view, and left to the game's traffic (`SeenGame.PassBy`, as the game's private driver summons a car). It is not held, so there is nothing to give back. Where the pool has no idle car of the model, no car passes.
- Within 4 metres, on foot, the game's own panel shows their name, where they are and **Say hello**.
- They go when their hour there is over and nobody is looking, when they are 160 metres behind, indoors, on a time skip, with the option, on a fault, or in a Going Public session.

## The greeting

The game's dialogue panel (`SeenDialog`): a hello by their form (two ways each, another for somebody they know), then **Ask how business is** or **Admire the car** (only with a car on record), each answered by their form. A rival doing well is smug, one in trouble is short.

A greeting is counted before the talk opens, by the social life's own rule for a first talk of the day face to face (`SocialRules.Chat`): 4.5 points, once a day for each rival, to the scale's top of 100. A second greeting on the same day gains nothing. From 30 points (the social life's bar for a friend) their page also says where they will be next that day. Nothing else comes of it: no money, no standing, no favour.

## Sightings

`SeenDay.Run` once a game day: for each rival, at the hours of lunch, dinner, the beach, the Hamptons, late at the office and the office on a Saturday, where the week has them; where that place has a line for their form, a roll of 180 per mille says whether somebody saw them. At most one a rival, at most 3 a day (a change of car counts), the lowest draws kept. 24 are kept in all.

- A meal names a real eating place, the office names their firm; a meal with no eating place open is not written.
- `SeenDay.Hour`, once a game hour: the sightings whose hour has come go out on Flare from the City Ledger's account (`FlareDirector.Society`: the same author and source as its morning headlines), at most 3 a day. One of a day that is over is marked without being sent.
- The morning paper prints up to 2 of the day before (`News.Extra`), a change of car first.
- The pages: a tycoon's panel in the Finance app has a line with their car and where they tend to be found; the chosen rival's head in the Rivals app has two facts, Drives and Usually.

**A sighting never tells more than the rival's real state.** It is only written from `Where` and from the form the real figures gave at the week's mark; a place has a line only for the forms that can be there (`SeenCatalogue.PlaceLines`); no line has a figure in it. What it hints is what the Rivals app and the rich list already show as a week's change.

## The player, seen in turn

Once the player runs a business, each game hour they are out in a car of the ladder's fourth rung or dearer, or inside an eating place, a roll of 120 per mille writes a line about it (`SeenDay.Player`), never twice within 6 days. It names the car or the place they are really in or at, goes out on Flare at once, is printed the next morning, and is said in a notification.

## Going Public

In a session the city's people are the host's and the tycoons rest. The whole feature rests with them (`SeenRules.Runs`): nobody appears, nothing is written, the save's line is left as it is. So no rival ever stands on one player's machine only and nothing is sent. Outside a session every draw is a hash of the save's seed (the rivals' own seed where they have one), so the same save gives the same week on every machine and every load.

## The option

"Rivals seen around the city" (`living_rivals_seen`, on), under the living economy's master option. Off: nobody appears, nothing is written, the save keeps its line.

## The save line

`reallife:rivals_seen`, version 1: `v=1^h:seed|day|hour|player day^r:record;...^n:note;...`. A record is key, name, figure, the figure before, businesses, businesses before, mark day, form, tier, rung, car, car day, known (tenths), greet day, greetings, met day. A note is day, hour, kind, place, form, posted, key, name, firm, the eating place, district, car, the car before. Words are escaped; numbers are in the invariant culture. A newer version's line is read as far as it goes; an old save has no line.

## Tests

Options, Rivals seen around the city: who is where now; a rival beside you to greet; write a sighting now; put everything back as before. Each writes a `Rivals seen check (...)` line. With Developer diagnostics on: `Rivals seen check (day N)`, `(in the world)` and `(greeting panel)`.

Offline: EconomyCheck's `RivalSeenChecks.cs` (`--rivalseen`).

## Not built, and why

- **A face of their own in the street.** Characters are made by hand by the mod's maintainer, so a rival wears whatever look the living city's pool hands out (`SeenDirector.World.cs`, `Put`).
- **A car standing at the kerb.** The mod can put a car of the traffic pool on a lane (`TrafficTools.Load`), which then drives; the only parked car it builds is the police prop (`TrafficEffects.CreateParkedPatrol`), and the game's parked cars are found by their footprint, not by their model (`CrimeCars.FindParked`). So their car passes and does not stand.
- **Meeting a rival for networking.** Positions belong to people of the social cast (`NetworkDirector`, through `SocialLinks.Met`, takes a `SocialPerson`); a rival is not one, so knowing them stays the greeting's own record.
- **The beach as a place.** The mod knows no spot of sand; a rival on the beach is in the Hamptons, and in the world stands by an eating place there.

## Not verified in the game

That the panel takes the game's box over a person of the living city; that the dialogue opens face to face with them; that the traffic pool names its cars after the game's vehicle types (`PrivateDriverHelpers.GetAiVehiclePrefab` suggests so); that an eating place's door has pavement to stand on; how a line of this length reads on Flare.
