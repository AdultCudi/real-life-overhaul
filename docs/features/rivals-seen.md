# Rivals seen around the city

## In a sentence

The game's rival owners and the rich list's tycoons each keep a seeded week that follows how their business is really doing; they stand at the places near the player as people of the living city, their car passes in the traffic, they can be greeted, and where they were seen is written on Flare and in the City Ledger, with the player noticed in turn.

A player asked for it: rival owners driving luxury cars, eating out or at the beach and the Hamptons, by how their business is doing.

## Where the code is

Folder [`Scripts/RivalsSeen`](../../Assets/Mods/RealLifeOverhaul/Scripts/RivalsSeen/), namespace `RealLifeOverhaul.RivalsSeen`.

| File | What it is |
| --- | --- |
| `SeenCatalogue` | every figure and every locale key: game-free |
| `SeenFile` | the save's file, with a rival as read today (`SeenSubject`), what is kept of them (`SeenRecord`), a sighting (`SeenNote`), a rival due at a place this hour (`SeenDue`), what the rules ask of the city (`ISeenCity`) and of the words (`ISeenWords`): game-free |
| `SeenRules` | when the feature starts and its seed, form and means from real figures, the weekly mark, the car, the week (`Where`), who is tried in a look and who goes, the spots by a door, the greeting's glance, a hello: game-free |
| `SeenDay` | the day's pass, the hour's pass, the player seen, a test's sighting and taking it back, the paper's lines: game-free, called by the director and by the checks alike |
| `SeenTable` | the day's table of the city: the car ladder, the eating places by district and rent, the buildings of the day's offices; the hour's places: game-free |
| `SeenLines` | a sighting's line in words, and a rival's few words for their pages: game-free |
| `SeenPage` | the pages' lines, made again only when something they say has changed: game-free |
| `SeenStore` | the save's line `reallife:rivals_seen`: game-free |
| `SeenDirector` | the state and its timing; its parts `.World` (who stands where, the panel, the greeting) and `.Tests` |
| `SeenGame` | the only file touching the game: the game's rivals and their weekly income, cars, the city's buildings read into the table, doors, the player's car and table, a car of the traffic pool put on a lane |
| `SeenDialog` | the greeting in the game's dialogue panel |
| `SeenText`, `SeenGuide`, `SeenBridges` | the game's words, the feature list's entry and options, what it asks of the rest of the mod |

Small hooks: `Wiring/Wiring.SeenLinks.cs` and `Wiring/Wiring.SeenTycoons.cs` (the tycoons from the rivals' own file: game-free), `Social/Flare/FlareDirector.Society.cs` (a line on the City Ledger's account), `Rivals/RivalBridges.SeenLine` and `RivalPage` (a tycoon's panel), `Rivals/RivalDirector.Broken`, `Ui/RivalsViews` and `Ui/Rivals/RivalsScreen` (the game's Rivals app on the interface kit).

## Who is seen

- **The game's own rival owners** (`RivalsHelper.GetAllRivalData`, the people of the game's Rivals app), read in the order of their ids. Their figure is the weekly income the Rivals app shows (`RivalData.WeeklyIncome`: the last seven days of their shops and offices), and the week before comes from the game's own history of that same figure (`RivalState.weeklyIncomeHistory`), so a week is set against a week of the same kind; their businesses are all they run, set against `numberOfBusinessesHistory`, which counts the same. Should the game's figure fail to be read (a business with no days on record), the mod sums the same days, leaving that business out, and says so once in the log. Their busiest shop or office gives their firm's name, their office and their district. One who owns a house in the Hamptons is known to (`Building.IsHamptonsHouse`).
- **The rich list's tycoons** (`Scripts/Rivals`), once they have come forward, through `SeenBridges.Tycoons` (`Wiring.SeenTycoons.Map`, game-free and driven by the checks): their worth, the worth at the rich list's last weekly mark, whether a counter of the player's holds them back (`SlowUntil`, `BarredUntil`), and the game's rival business their first outlet stands on (`RivalOutlet.Seat`) as their office, with its district.

Somebody gone by their source's own rules is seen nowhere and written about nowhere: a game rival who is defeated (`RivalsHelper.IsRivalDefeated`), a tycoon who was taken over. Both are asked in the morning and again as each hour turns (`SeenGame.Still`). A tycoon who has not come forward is not on the list at all. No system of the mod puts a rival in prison, kills or retires one, so those are the cases there are.

## When it starts

Nothing is loaded, played or put away before a save is ready, and nothing is loaded or played until the rivals' own feature has made up its mind (`SeenRules.Look` on `SeenRules.RivalsState`): running and awake, or known to be off (never made, its option off, or broken by a fault). While it is still starting, or at rest in a Going Public session, the feature waits, so the tycoons are on the day's list from the first pass and a load landing on a new day never plays the day without them. When the rivals' own feature comes on or goes off later in a day, the day's rivals are read again; the day is not played twice and nothing is pruned.

A new file's seed is a fixed hash of the playthrough's own id, made once with the save (`SeenRules.SeedOf` on the save's seed, else the character's id), so the same save draws the same week on every machine and every load. No draw comes from the engine's generator or the clock.

A loaded file is set against the game's day (`SeenRules.Settle`, and `SeenStore` on reading): a day of the player's last line or of a car past today is brought back to today, and a sighting of a day still to come is dropped, so a damaged or carried-over line never holds the player's lines or a car for days that never came.

## How the business is doing

Read once a week for each rival (`SeenRules.Mark`: on the first look and then every seventh day), so a rival's week is one week.

| | thriving | in trouble | otherwise |
| --- | --- | --- | --- |
| a rival owner | the week's income 8% up or more, or a business gained | 8% down or more, or a business lost | steady |
| a tycoon | their worth 8 per mille up or more in the week | 1 per mille down or more, or held back by a counter of the player's | steady |

With no week before to compare with, a rival is steady: nothing is guessed.

Their means are five tiers of the figure at the start's prices: an owner's weekly income from 0, 15,000, 60,000, 200,000 and 600,000; a tycoon is never under the fourth tier and is in the fifth from 100 million.

## The car

The ladder is the game's own cars that a showroom deals in, the cheapest first (`DealerGame.Models`), in six rungs. A rival's rung is their tier, one up when thriving and one down in trouble; which car of the rung is their own pick for good. A car is kept at least 14 days. The first car on record is taken quietly; a change after that is a sighting, "seen leaving in a new ..." for a dearer one and "has let the ... go" for a cheaper one. On a day that already has its three sightings a change waits for the next day.

## The week

`SeenRules.Where(seed, rival, form, tier, house in the Hamptons, gone, day, hour, coast)`, every draw a hash of the save's seed, the rival, the day and its purpose. A week starts on Monday, as the game's does. "Away" below is a thriving rival with a house in the Hamptons or from the fourth tier, in a city that has the Hamptons.

| when | thriving | steady | in trouble |
| --- | --- | --- | --- |
| working day, 9:00 to 18:00 | the office, until 17:00 | the office | the office |
| 12:00 to 14:00 | lunch out every day | lunch out 600 per mille of days, else the office | a table for one 250 per mille of days, else the office |
| 18:00 to 21:00 | | | still at the office |
| 19:00 to 21:00 | dinner out 700 per mille | dinner out 300 per mille | never |
| Saturday | away: the Hamptons 10:00 to 20:00, the beach 11:00 to 16:00; otherwise lunch in town by the working day's roll | where the city has the Hamptons, the beach 11:00 to 16:00 on 400 per mille of Saturdays; else lunch by the working day's roll | the office 9:00 to 18:00 |
| Sunday | away: the beach 11:00 to 16:00, there from 10:00; otherwise home | home | home |

Where they eat is one of the city's eating places (a business whose type is a restaurant, a coffee shop or a cafe) of their district, open at that hour by its own timetable. The places of a district are ordered by the rent of their address, then the address; a grade (modest, middling, smart, by the tier, a step up thriving and a step down in trouble) has its third of them and the day's pick says which. A tycoon with no outlet eats in one of the city's districts, theirs for good by the seed.

In a city without the Hamptons (`CityCheck.Here`) nobody goes there: nobody is on the beach or away, and a Sunday sees nobody.

## The city's table

`SeenTable`, made on the day's pass from the city's buildings (`SeenGame.Refresh`, once the city's list is there to read; the day is not counted as read until it is). Only what the rivals need is kept: the eating places, and the buildings at the addresses of the day's offices, found by number and then street with no string made for any business (a number none of the offices has is turned away by one test of a bit). As the hour turns, who is due somewhere and the building each is at are worked out once (`SeenTable.Places`).

## In the world

`SeenDirector.World`, once a second from the director's timer, nothing per frame.

- Every 6 seconds one rival at most is tried (`SeenRules.PutPick`): the first in the hour's seeded turn whose place has a building 18 to 110 metres from the player, who does not stand already and whose placing has not failed this hour. A meal needs the place open now.
- Their spot is on the pavement along the kerb either side of the point before the door (`JobSpots.Front`), 3.6 and then 5 metres along (`SeenRules.DoorSpot`): even past a double door's half width of 1 metre that is clear of the 2.4 metres the living city keeps round a door (`CitySpots.DoorKeepOut`). The spot must be clear and out of the camera's view (`SeenRules.MayStand`), so nobody appears in front of the player. That is 4 spot tests at the most. A placing with no spot, or at a place found shut, is not tried again until the hour turns, and the living city's counts of refused spots are left as they were.
- They are **one of the living city's own people exactly as that pool dresses them** (`CityPeople.Take(CityLook.Civilian)`): no character, face, body, hair or look of their own is made. So a rival has no face of their own in the street, and the living city (`Settings.CityLife`) must be on for anybody to stand there.
- At most 2 stand at once, and never when the living city has only 2 people left free.
- Their car: an idle car of the traffic pool of that very model is put on the lane that leads past the place, 70 metres or more back and out of view, and left to the game's traffic (`SeenGame.PassBy`, as the game's private driver summons a car). The pool names its cars as the game names a type's car (`PrivateDriverHelpers.GetAiVehiclePrefab`), and the car is matched on that name exactly. It is not held, so there is nothing to give back. Where the pool has no idle car of the model, no car passes.
- Within 4 metres, on foot, the game's own panel shows their name, where they are and **Say hello**. The panel is looked at once a second, and every 0.2 seconds while a rival stands within 12 metres (`SeenRules.Glance`), so a player running past at 6 metres a second is not missed.
- They go when their hour there is over and nobody is looking, when they are 160 metres behind, indoors, on a time skip, with the option, on a fault, or in a Going Public session (`SeenRules.Leaves`). Nobody goes by their own rules while the player is talking with them; whoever must go anyway (indoors, the option, a fault) has the greeting with them ended first (`SeenGame.EndTalk`, only a greeting of this feature's), so the game's dialogue never holds a person who is gone.

## The greeting

The game's dialogue panel (`SeenDialog`): a hello by their form (two ways each, another for somebody they know), then **Ask how business is** or **Admire the car** (only with a car on record), each answered by their form. A rival doing well is smug, one in trouble is short.

A greeting is counted before the talk opens (`SeenRules.Hello`), by the social life's own rule for a first talk of the day face to face (`SocialRules.Chat`): 4.5 points, once a day for each rival, to the scale's top of 100. A second greeting on the same day gains nothing. From 30 points (the social life's bar for a friend) their page also says where they will be next that day. Nothing else comes of it: no money, no standing, no favour.

## Sightings

`SeenDay.Run` once a game day: for each rival, the hours a sighting may be written at are looked at in turn: 12:00 (lunch, the beach), 19:00 (dinner, late at the office), 17:00 (the Hamptons) and 11:00 (the office on a Saturday). At each hour the week has them at a place with a line for their form, a roll of 180 per mille says whether somebody saw them, and the first that comes up is theirs for the day. So a rival out more is seen more, as rivals doing well are. One place line a rival a day at most (a change of car is news of its own), at most 3 sightings a day in all (a change of car counts), the lowest draws kept. 24 are kept in all, and a test's sightings beside them.

- A meal names a real eating place, the office names their firm; a meal with no eating place open is not written. The office's line says they were there all Saturday, so it is written on a Saturday only; working late is written on a working day.
- `SeenDay.Hour`, once a game hour: first whether the rivals are still there is asked (`SeenGame.Still`), and a line about somebody defeated or taken over since the morning, or no longer on the day's list, is dropped unsent and never printed. Then the sightings whose hour has come go out on Flare from the City Ledger's account (`FlareDirector.Society`: the same author and source as its morning headlines, the society topic as its tags, stamped at the sighting's hour and minute). At most 3 posts a day, the player's own line among them; a test's comes on top. One of a day that is over is marked without being sent.
- The morning paper prints up to 2 of the day before (`News.Extra`), a change of car first.
- The pages: a tycoon's panel in the Finance app has a line with their car and where they tend to be found; the chosen rival's head in the Rivals app has two facts, Drives and Usually. The Rivals app asks only for the rival whose head it shows (never the player or a defeated rival), and `SeenPage` gives the answer it kept unless something it says has changed (a day's pass, an hour's, a greeting, a load, a test, the language); a tycoon's line is kept the same way, and made again when the hour turns.

**A sighting never tells more than the rival's real state.** It is only written from `Where` and from the form the real figures gave at the week's mark; a place has a line only for the forms that can be there (`SeenCatalogue.PlaceLines`); no line has a figure or any number in it, in any language. What it hints is what the Rivals app and the rich list already show as a week's change.

## The player, seen in turn

Once the player runs a business, each game hour they are out in a car of the ladder's fourth rung or dearer, or inside an eating place that is not one of their own (`GoingPublic.Mine`), a roll of 120 per mille writes a line about it (`SeenDay.Player`), never twice within 6 days. It is one of the day's 3 posts: with 3 out already it waits for another day. It names the car or the place they are really in or at, goes out on Flare at once, is printed the next morning, and is said in a notification.

## Going Public

In a session the city's people are the host's and the tycoons rest. The whole feature rests with them (`SeenRules.Runs`): nobody appears, nothing is written, the save's line is left as it is. So no rival ever stands on one player's machine only and nothing is sent. Outside a session every draw is a hash of the save's seed, itself a hash of the playthrough's own id, so the same save gives the same week on every machine and every load.

## The option

"Rivals seen around the city" (`living_rivals_seen`, on), under the living economy's master option. Off: nobody appears, nothing is written, the save keeps its line.

## The save line

`reallife:rivals_seen`, version 1: `v=1^h:seed|day|hour|player day^r:record;...^n:note;...`. A record is key, name, figure, the figure before, businesses, businesses before, mark day, form, tier, rung, car, car day, known (tenths), greet day, greetings. A note is day, hour, kind, place, form, posted, key, name, firm, the eating place, district, car, the car before, test. Words are escaped; numbers are in the invariant culture. A line of before (with a sixteenth field on a record, the day the player last stood near them, which nothing read) loads whole and is written again without it; its sightings are not a test's. A newer version's line is read as far as it goes; an old save has no line. Every figure is put inside its bounds on reading, and no day of the line is past the line's own day.

## Tests

Options, Rivals seen around the city: who is where now; a rival beside you to greet; write a sighting now; take back what the tests did. Each writes a `Rivals seen check (...)` line. A test's sighting is marked as a test's: it comes on top of the day's posts, and up to 4 are kept beside the days' 24, never in their place. Taking back removes the tests' sightings from the pages and the paper and the rivals a test put beside the player, and nothing else: the days' marks, cars, sightings, posts and greetings stay as the days made them. A post already on Flare stays (Flare has no taking down). With Developer diagnostics on: `Rivals seen check (day N)`, `(in the world)` and `(greeting panel)`.

Offline: EconomyCheck's `RivalSeenChecks.cs` (`--rivalseen`). It drives the shipped rules, the day's table, the pages and the tycoons' mapping, and times the shipped passes.

## What it costs

Measured by the checks (`--rivalseen`, the least of three rounds, on a city of 622 businesses with 120 eating places and 22 rivals); before is the same pass of the code before these changes:

| | before | now |
| --- | --- | --- |
| the day's table, once or twice a game day | about 0.07 ms, 39,512 bytes (a key string for every business) | about 0.07 ms, 64 bytes (the sort's) |
| the day's pass | 0.021 ms, 302 bytes | 0.025 ms, 280 bytes |
| the hour's places | (in the game's code) | 0.015 ms, nothing allocated |
| a look whose placing fails | every due rival tried every 6 seconds, 2 spot tests each: up to 13,200 placings an hour | 0.0002 ms of its own, nothing allocated, at most 4 spot tests: 22 placings an hour at the most |
| the hour, posting three | 0.0003 ms, 106 bytes (without the words) | 0.003 ms, 720 bytes (the three lines' words); nothing to post: 0.0001 ms, nothing allocated |
| the Rivals app on a rebuild | 0.016 ms, 20,680 bytes (lines for all 22 rivals) | nothing changed: nothing made, nothing allocated; after a change 0.0005 ms; a tycoon's line 0.0003 ms, 352 bytes |

The glance at the greeting's panel is a walk of the 2 people standing, 5 times a second only while one stands within 12 metres.

## Not built, and why

- **A face of their own in the street.** Characters are made by hand by the mod's maintainer, so a rival wears whatever look the living city's pool hands out (`SeenDirector.World.cs`, `Put`).
- **A car standing at the kerb.** The mod can put a car of the traffic pool on a lane (`TrafficTools.Load`), which then drives; the only parked car it builds is the police prop (`TrafficEffects.CreateParkedPatrol`), and the game's parked cars are found by their footprint, not by their model (`CrimeCars.FindParked`). So their car passes and does not stand.
- **Meeting a rival for networking.** Positions belong to people of the social cast (`NetworkDirector`, through `SocialLinks.Met`, takes a `SocialPerson`); a rival is not one, so knowing them stays the greeting's own record.
- **The beach as a place.** The mod knows no spot of sand; a rival on the beach is in the Hamptons, and in the world stands by an eating place there.

## Not verified in the game

That the panel takes the game's box over a person of the living city; that the dialogue opens face to face with them; that a spot 3.6 or 5 metres along the kerb passes the living city's own tests at the city's real doors, and how often a placing fails; that the 0.2 second glance catches a player running past in practice; that the game's weekly income reads without a fault for every rival; how a line of this length reads on Flare.
