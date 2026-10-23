# Law

## In a sentence

Police precincts in the city's own buildings, arrests and custody served in their holding cells, and a criminal record and reputation that follow the player.

## Where the code is

- Folder: [`Scripts/Law`](../../Assets/Mods/RealLifeOverhaul/Scripts/Law/).
- Read first: `LawBook.cs` (the offence table and its sums, game-free), `LawDirector.cs` (the one tick a second), `Custody.cs` (one arrest from the street to the cell) and `Record.cs` (the record and both reputations).

| File | What it is |
| --- | --- |
| `LawBook` | the offence table and its sums: hours per charge, the two-day cap, repeat offenders, bail, the lawyer (game-free) |
| `Record` | Record (the criminal record), Reputation (police and public, scrutiny) and LawState: their four lines in modData |
| `Precincts` | which buildings are the precincts (one per district, fixed by address), their map markers, the dressed frontage |
| `Custody` | one arrest: the scene on the street, the ride, the cell as a time skip, bail and lawyer, release, resume after a load |
| `CustodyList` | the holding cell on screen: the game's list window with the charge sheet, release clock, three entries |
| `StationDesk` | the front desk: the game's panel at the counter and the game's list window (DeskList), a second page for the record |
| `LawDirector` | the one tick a second: load and save, dressing, the daily step, thanks from passers-by, the watchdog, test buttons; Arrests: the one call each for whoever catches the player |
| `RecordPhone` | the Police Records Desk contact: record, reputation, time since the last offence, nearest precinct |

## How it talks to other features

- Other features read the record and reputations through `Law.Reputation`, `Law.Record` and `Law.Custody`; see [Record and reputation](#record-and-reputation).
- Everyone else asks "has the player a record?" through `Wiring.Standing`, which reads Law's record and Justice's convictions together.
- The courts serve their hours in Law's cell with `Law.Custody.Hold`; see [A court's hold](#a-courts-hold-custodyhold) and [Who does what at an arrest](../reference/wiring.md#who-does-what-at-an-arrest).
- The precincts are the first user of [map places](../reference/map-places.md).

Police precincts, custody that is served at one, and a record that follows the player.
Options group "Law": six toggles (precincts, custody, arrests for serious offences, effects of record and reputation, walk-in stations, police stations on the map) and seven test buttons.

## Precincts

The game has no police station: no building type, no model, only the parked police car prop (`Vehicles/ParkedVehicles/PoliceCar`).
So one existing building in each of the six city districts is designated (never the Hamptons, never one of the game's own service buildings, never a home, never the player's own premises).

Since players walk in, it has to be **a unit that stands empty and to let** (`AvailableForRent`, business type empty, no layout, no items, its DLC owned): the game loads the bare interior of such a unit through its own door, and nothing of the game's lives in it.

Of the district's empty offices, large shop units, warehouses and shops, the likeliest fourteen (by kind, then nearest the middle of the district) have their frontage measured once per city load, and the best score wins (`Precincts.Measure`).

The score: a sign band of its own over the shopfront 25; flat wall either side of the door at chest height 3 a place (eight places) and above head height 2 a place (four); pavement depth 5 a metre (three); a kerb with the road beyond it 10; and a little for the kind of building.

The result is written into the save by address (`reallife:law_precincts`) and stays, with two exceptions that are both said to the player:

- a save from before stations could be entered is looked over once (`reallife:law_station`, first field): a precinct in a unit that cannot be walked into moves to one that can, and one whose frontage scores 30 less than the best gives way to it;
- a precinct whose unit has since been let (to the player or to a rival) moves to another empty one in its district (`Precincts.Keep`, every five seconds; never while the player is inside it or being held). The game's own night of letting no longer takes a precinct's empty unit (it is held: [the game's own letting](../reference/map-places.md#the-games-own-letting-leaves-them-alone)), so this is now a special rival's defence or a change of the game's own.

A district with no empty unit keeps a precinct that cannot be walked into: its front desk answers at the door (the same panel, with the front office drawn behind it).
The log names all six once per load, with each one's score and whether it is walk-in.

- **Map**: the precincts are the first user of `Core/MapPlaces` (see [Map places](../reference/map-places.md)).
  They have a "Police stations" row in the map's filter list under Special buildings, and a shield marker per precinct with its name on it (on the map while the row is ticked, and in the street within 100 m).

  A click on a marker sets a destination, and the game's own card for the building reads the precinct's name, "Police station" and "Open day and night", with the game's own "set destination" button.

  The option "Police stations on the map" takes all of it away.

- **Frontage** (`PrecinctFrontage`), for the one precinct the player is within 260 m of (one set of objects, built once and moved; taken in beyond 330 m).
  Every piece is measured against that building before it goes up; one that does not fit is left off and the log says why (one line per visit, `Law: frontage: ...`):
  - the *fascia*: the building's own wide sign over the shopfront (which would read "for rent") is switched off, with its hanging logo signs, and a police-blue board of exactly its measured box put in its place, lettered POLICE and the precinct's name.
    The game's own `UpdateSign` puts the signs back when the frontage is taken in;
  - the standing `precinct_sign` with the precinct's name, against a flat stretch of wall beside the door (`CitySpots.Shopfront`, then the board and both posts tested against everything solid);
  - a `station_awning` over the door, only on flat wall above it, with its whole volume clear, pavement under it, and never across the fascia;
  - twin `blue_lamp`s on brackets either side of the door, 2.2 m up, the same distance out on both sides where the wall allows (their glass changes to a brighter paint when the street lights come on);
  - a `police_shield_emblem` and a `notice_board` on flat wall (a list of places either side of the door, tried in turn);
  - POLICE lettered on the door, where rays find the door's own face flat;
  - up to six `bollard`s on the kerb strip either side of the way in (never straight in front of the door) and a `flag_pole` further along: just beyond the edge of where people walk, only where the road is the other side of that edge, on level ground, touching nothing; each cut out of the navigation mesh;
  - up to three patrol cars at the kerb within 48 m (the game's police car prop, placed by the tests `PolicePresence` parks by, and clear of the bollards);
  - two officers either side of the door (`guard_stand`, or `officer_guard_idle` once it exists), and one thing at a time on a slow timer: an officer comes out of the door, walks to the nearest patrol car, talks on the radio and goes back in.
    An officer walks in off the street from out of view.

    And now and then, with the player within 75 m, a suspect in handcuffs (`cuffed_walk`, `CuffPose`) is walked in with an officer a pace behind.
    Whoever goes in is gone at the door, as the game's own shoppers are.
    All but the suspect are people the pool may take back at any moment.

    A wall piece needs a flat patch its own size (five rays square on to the wall that agree to 6 cm), a clear box in front of it, and no other piece on that patch.
    Nothing is put out or taken in where the camera is looking, except under a dark screen.

    **Not done**: tinted windows (the game clears every property block on a building's renderers whenever its map outline changes, so a tint could not be kept or safely put back).

## Inside a station

How the player gets in: the precinct's doors are given the game's own `alwaysOpen` flag (remembered and put back), so clicking the building and walking to the door enters it as any open business; and within 5 m of the door the interaction key is the station's (`LawDirector.TakesInteractKey`; the prompt says so) and calls the game's own `BuildingManager.EnterBuilding`.

Nothing about the unit is changed: it is still to let, its registration, rent and ownership are untouched, and a save made inside one loads without the mod as the player standing in an empty unit.
If the game does not get the player in within seven seconds, its half-done entry is put right and that precinct's front desk answers at the door for the rest of the session.

The room (`StationInterior`) is furnished the moment the game's "entered building" event fires, with the screen still dark, and cleared on its "left building" event.
If that event was missed (a save loaded inside) or the floor was not there yet, the room is furnished behind the next dark screen or pause, never in play (`LawDirector.FurnishOwed`); with Developer diagnostics on, `Law check (furnish)` gives the time it took.
The game's design for each precinct's unit is found and read on a worker thread when the precincts are chosen, and the floor is measured once, for the station and its back office alike.

Every unit is a different size and shape, so the room is **measured**, on the navigation mesh the game walks its customers on: how far the floor runs straight in from the door, how far to either side at each depth, whether a wall runs straight where something stands against it.

Each piece needs its whole footprint walkable; wall pieces go up only where a ray finds the wall's real surface; what does not fit is left out and named in the log (`Law: <precinct>: inside: ...`):

- the **front desk** (`precinct_desk`) across the room 3 to 6.5 m in, in the middle where the room is wide, always with a way round one end; the precinct's name on its face; the desk sergeant behind it;
- a **waiting bench** (`precinct_bench`) along a side wall by the entrance with somebody waiting (`queue_wait`), and a `notice_board` on the wall opposite;
- one or two **office desks** along the side walls behind the counter, an officer at each (`take_notes`);
- the **holding cell** at the back: a square, clear back corner closed with two `holding_cell_bars` panels (the side one shortened to the depth there is), or the whole back of a room no wider than one panel; a bench inside; and a closed **evidence-room door** in the back wall beside it.

With **Furnished police stations** on (`law_station_furniture`, `StationInterior.Furnish.cs`), the game's own furniture then goes round all of it.

Each piece is a copy of one of the game's item prefabs (`PrefabHelper.LoadPrefabAssetByName`), made under a switched-off holder so that none of its scripts runs, and stripped while asleep of what makes it an item: the ground and direction indicators, attachment points and their decals, navigation targets, the "for sale" sign, a counter's waiting line, and every script, collider and obstacle (`Core/GameFurniture.cs`).

What is left is the game's meshes and materials; a lamp keeps its light with its shadows off.
Every item prefab stands on the floor at its pivot with its front towards +Z (read from the game's own prefabs); sizes are measured from the copy's meshes.

Placed in the room's frame, only where the floor and a straight stretch of wall have room, and never on floor already taken: the way from the door to the counter, round both its ends, to the cell and in front of the bars, the sergeant's and the visitor's places and the officers' desks are taken first.

- on the counter (sergeant's side) a `ComputerMonitor`, an `OfficePhone` and a `StackOfBooks`; on each officer's desk a monitor, a phone and a `DeskPlant01`;
- along the side walls, in front of the counter: a `WaterCooler`, a `GroundPlant03`, a `TrashBin`; behind it: up to two `OfficeLockers`, a `Printer`, a `StorageShelf`, a `CheapCoffeeMachine`, up to two `UniformLocker`, a plant; each with its back to the wall and 0.55 m kept free in front of it;
- on the walls, where a ray finds the surface: an `ExitSign` over the door, a `LargeWallClock` in the public part, two `FramedCertificate` behind the counter (never where the notice board hangs);
- up to three flush `CeilingLampSquare` down the middle where a ray finds a ceiling; a `CarpetBigBasic` in the waiting part where all of it is floor;
- in a corner cell big enough for it beside its bench: a `ToiletClassic` against the back wall and a `SinkWallModern` on its side wall.

One pass on the way in, under the game's fade: a few thousand walkable-floor samples, and the first copy of each piece (kept for every later visit, moved to the next station's own places).
The log line names how many pieces went in.
Off, the station is the counter, bench, desks and cell only.

Not seen in the game yet: how the pieces look in the units' rooms, whether every piece's front is the side the game's own placing uses (read from the prefabs' direction indicators and seat points), and whether a unit has a ceiling a ray can find.

Furniture and bars are cut out of the navigation mesh.
The sergeant is a pool character of the scene's own; the other three are presence characters.
A room too small for any of it keeps the front desk service where the player stands.

**The front desk** (`StationDesk`; within 2.3 m of the counter the game's panel over a place (`GameUi.DoorPanel`) offers "Speak to the desk officer" on the interact key, at a door that can be walked into "Go into the station"): the list has an entry for each thing, with the cost or state under its name and the button off, with the reason, when it cannot be had; answers are notifications.

Report a crime or an accident (exactly what a 911 call would do about what is going on nearby, +2 police reputation for coming in, and never a "wasted call"); pay outstanding fines (`ModState.PendingFine`, +1).
The record (a second page: both reputations, then a note for each line, newest first, and Back).
Collect a car from the pound (see custody; 250, brought to the kerb when the player steps outside).

Apply for a licence (only if `Law.Bridges.Licensing` is set).
A police report after a break-in or a car break-in in the last seven days (15; reference `PR-<precinct>-<day>-<number>`; handed to `Law.Bridges.PoliceReport` if set); hand yourself in (see below); visiting hours.

```csharp
Law.Bridges.Licensing = () => ...;                    // the desk then offers "Apply for a licence or permit"
Law.Bridges.LicensingLabelKey = "...";                // optional: its button's text
Law.Bridges.PoliceReport = (kind, reference) => ...;  // kind: Bridges.BreakIn / CarBreakIn / Accident
Law.Bridges.NoteIncident(Law.Bridges.Accident);       // something a report can be asked for has happened
Law.LawDirector.Inside                                // the precinct the player is standing in, or null
```

## Arrest and custody

| charge (`Law.Charge`) | when | hours | bail |
|---|---|---|---|
| `WastedCalls` wasting police time | the fifth 911 call about nothing in one day | 2 to 4 | 300 |
| `FledStop` fleeing a traffic stop | told to pull over, drove on, caught (`PoliceAttention`) | 4 to 8 | 1,500 |
| `HitAndRun` reckless driving causing a crash, leaving the scene | hit a car and drove off, caught (`PolicePursuit`); or crashed while fleeing a stop | 8 to 16 | 3,000 |
| `InjuryFled` injuring a pedestrian and leaving the scene | caught after driving off | 18 to 30 | 8,000 |
| `DeathStayed` causing the death of a pedestrian | stopped and waited: arrested on the spot | 24 to 36 | none |
| `DeathFled` causing a death and leaving the scene | caught after driving off | 36 to 48 | none |

Several charges: the longest in full plus a quarter of each of the others.
A repeat offender (held before in the last 14 game days) serves 15 % longer per time, at most 60 %; bail goes up the same way.

**Nothing is ever longer than 48 hours.** The fines are not in this table: whoever stops the player charges them, once, exactly as before (`CrimeLaw`, `PolicePursuit`'s constants) and passes the sum to the charge sheet, which shows it and charges nothing.
Every figure is in `LawBook`.

What happens, in order (`Custody`):

1. The charge, the sentence and the precinct are announced, and the record gets its line.
2. Out of doors, with the car stopped: the player is taken out of it and held where they stand.

   An officer walks up behind them (from the patrol car that made the stop, otherwise from pavement out of view), and the player's hands go up (`hands_up`, `hands_up_hold`).
   Then the handcuffs: the officer comes to stand 0.9 m from them, the player is turned on the spot to have their back to the officer, and the pair of clips made for exactly that is started in one frame.

   They are `arrest_cuffed` on the player and `officer_cuff` whole-body on the officer with its last pose held; the officer's half is played only from 0.9 m give or take 0.25, the reach otherwise.

   At their `click` the handcuffs leave the officer's hand and `CuffPose`'s pair is on the wrists: one pair is ever seen.
   Then `cuffed_stand` (on the player's own body the way `JobBody` plays clips on it).
   Indoors, still rolling, knocked down, or with nobody to be had from the pool, this step is skipped: nothing is faked.

3. The screen goes dark.
   The player is taken out of any vehicle, building or car park and put on the pavement in front of the nearest precinct's door, facing the street, and that precinct is dressed.
4. The cell: a panel over the dark screen with the precinct's name, the charge sheet, the fines already paid and a clock that counts game time, while the game's own time skip runs to the release time (the same call its hospital and taxi use, so businesses, rent and everything else go on).
   Energy comes back as on the game's own benches and hunger is topped up when low.

   Three choices, by button or number key: sit it out; **pay bail** (not for a death), which ends it at once; **call a lawyer** (750, or 600 for somebody the public thinks well of), once, which strikes off a third of what is left.

5. Release outside the door: energy at least 45, hunger at least 50, a summary, a bad mood for a while, and the car that was left at the scene brought to a kerb space near the precinct for the city pound's fee (300, `LawBook.ImpoundFee`, the same for a car taken for driving without a licence) (`VehicleTow.BringNear`; a repair shop if there is no space; a message if it could not be moved).

**At a precinct that can be walked into** (and with "Walk-in police stations" on), steps 3 to 5 go on inside: from the pavement the player is taken through the door by the game's own way in and put in the holding cell before the screen clears, facing the bars and kept there.

The cell's list is then at the right of the screen with the cell in view beside it, and the time skip runs without the game's blur.
On release the screen goes dark for a moment, the player is at the front desk, is signed out by the sergeant and walks to the door, which the game takes them out of in the usual way: release is from the front door.

The car left at the scene is then **at the pound** (`reallife:law_station`): any front desk has it brought to the kerb for the impound fee.
A room with no cell (too small, an odd shape) holds the player at the desk behind the full-screen panel; if the game's way in fails, everything is as it was (the pavement, the full-screen panel, the car brought round at once).

**Handing oneself in**: after getting away from hurting somebody the player is wanted (`LawState.WantedCharges`: the charge they ran from).
For seven game days the front desk offers "hand yourself in": that charge for half the hours and half the bail, served in the cell like any other stay.
Any arrest settles it.

Safety, inside: the bars are solid, so on every way out of custody (release, the watchdog, a fault) the player is put at the front desk, and if that cannot be done the bars are taken away; the room is cleared when the player leaves it for any reason.

A save made in the cell loads, without the mod, as the player standing in an empty unit; with the mod they walk out and the rest of the stay is resumed (the precincts are only designated once the player is out of doors).

Safety: the stay is one line in modData (`reallife:law_custody`).
A save made in the middle has the player standing on the pavement outside the precinct, which is all the base game sees if the mod is removed.
With the mod, a load resumes what is left, or releases at once if the time has passed, the option was switched off or anything about it does not add up.

`LawDirector` ends custody (everything put back, the player free where they stand) if the routine shows no sign of life for 25 real seconds, if the save says held and nothing is running, or on any fault; a stay never takes more than seven real minutes on screen.
If the player cannot be moved (the hospital has them, no way to the door) the arrest is dropped: the fines stand, the record keeps the line.

### A court's hold (`Custody.Hold`)

```csharp
// False, with nothing done, if custody cannot take them now (off, no precinct, somebody already held, the courts off).
bool Law.Custody.Hold(float hours, string reasonKey, int bail, Action released);
Law.Custody.HoldBailed            // read in "released": the hold ended because its bail was paid
Law.Bridges.CourtOn = () => ...;  // asked whether whoever orders holds still stands behind them (Wiring: Justice.Active)
```

Hours ordered by a court are served exactly where an arrest's are, by the same code: the wait for a free screen, the dark screen, out of any car or building (`Transport`), the pavement outside the nearest precinct, through its door into the holding cell (`GoInside`, `PutInCell`), the cell's list, the game's own time skip to the release time, the front desk and the door (`Release`, `ReleaseInside`).

A player who is already standing in a station is put in that station's cell without going out.
What differs, all of it decided by `CustodyState.Court`:

| | an arrest | a court's hold |
|---|---|---|
| in the street | the officer, hands up, cuffs | nothing is played: one notification, then the dark screen |
| the note in the cell | CHARGE SHEET: the charges and what was paid | COURT ORDER: the caller's sentence (`reasonKey`: `JusticeLaw.HoldRemand`, `HoldSentence`) |
| entries | sit it out, bail, a lawyer | sit it out; bail only if the caller set one (`CustodyList.Shape`); never a lawyer |
| hours | `LawBook.Sentence` | the caller's, bounded to 1 to `LawBook.MostHours` (48) |
| the police record | a line, closed with the hours served | no line: the conviction is the courts' own, and a second line would move the reputation twice for one offence. So it is not counted by `Record.TimesHeld` either |
| mood | `MoodDownBig` | `MoodDown`, as the courts' own time skip gave |
| could not be moved (at the wheel of a car that will not stop, no way to the door) | the arrest is dropped | the hours pass where the player is, behind the full-screen list: a court's hours are never let off |
| the courts go (switched off, the feature at fault) | | the hold ends at the next look (eight a second, with the clock line) and at a load |

A car the player was sitting in is remembered and treated as an arrest's: at the pound if they are let out inside, brought to a kerb space for the impound fee otherwise.

`released` is called exactly once, last thing in `Cleanup`, on every way out (served, bail, the watchdog, a fault, the component destroyed); it is not kept in the save, so a hold resumed after a load ends without it, and `ILawBridge.Holding` is what the courts ask instead.

Every guarantee of an arrest's stay holds for a court's because it is the same routine and the same state: the watchdog in `LawDirector` (25 s without a sign of life, "the save says held, nothing is running", any fault), `HeldMost` (seven real minutes on screen), `LeaveCell` and `OpenCell` for the bars, `Cleanup` on destroy.

**The save.** `reallife:law_custody` has two fields more at its end: `|1|<reason key>` for a court's hold (`|0|` for an arrest).
A line with nine fields, from before, is an arrest.
`Active` on load is "has charges, or is a court's, and has a release time".

A save made in the cell loads without the mod as the player standing in an empty unit (the base game ignores the key); with the mod they walk out and what is left is served, or they are let out if the time has passed, the option is off, or the courts are off.
A build of the mod from before this reads the nine fields it knows, finds no charges and holds nobody.

One thing can come out of order: a save made during **remand** and loaded has its hearing waiting too (`JusticeState.HearNow`).
The courts' panel waits for `Custody.Active`, which is false from the load until the stay is taken up again (once the player is out of doors and the precincts are designated), so the hearing can be held first and the rest of the remand served after it.

Neither is lost and nothing is stuck: the hearing pauses the game, and the stay waits for `LifeEvents.IsSafeForEvent`.

Log lines with diagnostics on: `Law check (court hold): taken to <precinct>: in the holding cell: PASS` (or "no cell in this room, held at the desk", "held outside the door"; "could not be moved, held where they are" is the FAIL), `... signed out at the front desk of <precinct> after N h of M, then the door: PASS`, `... let out at the door of ...`, `... a save made during the hold was loaded with N h left: resumed`, `... ended by the watchdog (...)`.

Not checked in the game: any of it.
A screenshot of the cell with the COURT ORDER note and a single "Sit it out" entry settles the list; the log lines above settle the path.

With "Custody" off, the two old routes fall back to what they did before (a 2 to 5 hour time skip where caught).
With "Arrests for serious offences" off, a death is only an arrest if the player drove off, and 911 calls about nothing only cost the fine.

Not wired, because the player cannot do them: stealing and violence on foot.
`Law.Arrests` and `Law.Custody.Arrest(charges, paid, patrol)` are there for whoever adds them: one row in `LawBook`, one call.

## Record and reputation

- **Record** (`reallife:law_record`, at most 40 lines): every ticket, every stay in a cell after an arrest (with its hours, fines and how it ended) and every time the player got away after hurting somebody.
  Hours a court ordered are not a line here (they are in the Legal file with the conviction).
- **Police reputation** (0 to 100, starts at 55): falls with each line by the amount in `LawBook` (a ticket 3, a fatal hit-and-run 25), rises 2 for every game day with nothing new on the record (up to 75) and with calls that led to an arrest.
- **Scrutiny**: two lines in the last five days and traffic offences are 15 % more likely to be seen; three, 30 %, and an officer on foot no longer warns before a jaywalking ticket; four or more, 50 %.
  A message says when it starts and when a clean spell has ended it.
- **Wanted**: after getting away from a hit-and-run the car is known for the rest of the day (the flag `PoliceAttention` already kept for fleeing a stop: every other offence is seen, fines half as much again).
- **Public reputation** (0 to 100, starts at 55): rises 4 with everything that earns the "helped" mood (staying with a casualty, reporting an accident, stopping a thief; three a day count), falls when the player hurts somebody, drifts back up to 55.

  From 70 a passer-by says thanks about once a day (a real pedestrian within six metres, a smile over their head) and the lawyer charges less; from 85 they press a few dollars into the player's hand.

- **The Record page**: the Police Records Desk contact on the phone reads out both reputations, how long since the last offence, whether patrols are watching, then the record line by line, newest first.

For other features (all safe to read at any time; empty or neutral until the save has loaded):

```csharp
Law.Reputation.Police            // 0..100
Law.Reputation.Public            // 0..100
Law.Reputation.Liked             // Public >= 70
Law.Reputation.Scrutiny          // 1 .. 1.5, a field read
Law.Reputation.GoodDeed()        // what ModState.AddModifier(ModState.Helped) already calls
Law.Record.Clean                 // nothing at all on it
Law.Record.Has(Law.Charge.DeathFled)            // ever
Law.Record.Has(Law.Charge.FledStop, 7)          // in the last 7 game days
Law.Record.CleanFor(14)          // nothing new in 14 game days (a job that wants a clean record)
Law.Record.DaysSinceLastOffence  // -1 if none
Law.Record.TimesHeld(30)         // stays in a cell in the last 30 game days
Law.Record.Count, Law.Record.Entry(i)           // day, charges (bits), hours, fines, outcome
Law.Custody.Active               // being arrested or held right now (an arrest's stay or a court's)
Law.Custody.Hold(hours, reasonKey, bail, released)   // a court's order served in the cell; false if it cannot be taken
Law.LawDirector.NearestPrecinct(position)       // its Name, Door, Building; null before the city is ready
Law.Arrests.FledStop / HitAndRun / StayedAtScene / WastedCall / Escaped / Cited   // the ways in
```

## The desk and the cell in the game's own interface

- The panel at a station (door or counter) is `GameUi.WorldPanel` through `GameUi.DoorPanel`; the desk's menu is a `GameUi.TravelList` made by a `GameUi.DeskList` ("RealLifeOverhaul Law"); the cell is a second `DeskList` ("RealLifeOverhaul Custody") that is `Locked` (no Cancel button, the cancel key left to the game).
- The game's time skip switches the HUD off, so the cell's window is moved onto the skip's own canvas (`TravelList.MoveTo`) once the skip has started and back to the HUD's when it closes.
  The clock line is written only when the minute changes.
  Number keys 1 to 3 no longer choose in the cell: the entries are clicked.
- Fallbacks: if the game's list cannot be copied, the desk's panel has the report, the fines, the car and the police report on its own four keys; the cell serves the sentence with the charges told once in a notification and no bail or lawyer.
- The licence entry shows only when `Law.Bridges.Licensing` is set; nothing in this build sets it.
- Log lines: "Law check (desk panel): ...: PASS", "Law check (desk list): the game's list is open: ...: PASS", "Law check (choice): ...", "Law check (cell list): ...".
- Not checked in the game: any of it.
  In particular whether the list is drawn on the skip's canvas at the right size and place (a screenshot settles it), and whether the cancel key is left alone by the locked list.

## Test buttons

"Arrest me now" (minor: fled a stop; serious: hit-and-run with an injury; fatal hit-and-run) runs everything from where the player stands, in a car or on foot, indoors too; it charges no fines (in play those are charged by whoever stops the player), so the charge sheet has no "paid" line.
The arrest begins when the options menu is closed (it waits up to 40 seconds for whatever has the screen).

"Show the nearest precinct" marks it as the destination and says how far it is.
"Open the front desk here" opens the nearest precinct's desk wherever the player stands (the one they are in, if they are in one).

"Dress the nearest precinct again and log what fitted" takes the frontage in and puts it out at once, in view, and writes the frontage line (every piece: up, or left off and why) and the room's line to the log; it wants the precinct within 250 m and the player out of doors.
"Clear my record" wipes the record and resets both reputations.

Each says why if it cannot run (`TestRefusal`, with `NoPrecinct` and `ScreenBusy` added for these).

## Wish-list for the asset pipelines

Everything below drops in by id with no code change: the code already asks `PropLibrary` / `ClipLibrary` for each and uses its stand-in (or does without) until it is there.
The one exception is marked.

| prop id | size (m) | pivot and layout |
|---|---|---|
| `precinct_sign` | 1.80 x 2.90 x 0.08 | ground centre, read from +Z, nothing behind z -0.04 (it stands 0.12 m off a wall). Two posts 0.07 square at x +-0.86. A police-blue board 1.65 wide from 2.05 to 2.85 m up (over everybody's head) with POLICE in white, centred 2.56 m up; a white rule at 2.27 m; **a blank blue strip 1.50 x 0.17 centred 2.15 m up** where the code letters the precinct's name (z +0.027). The right-hand post top (0.86, 2.90, 0) carries the lamp |
| `blue_lamp` | 0.20 x 0.27 x 0.20 | centre of its base (it sits on a post top, and on the wall brackets either side of the door); glow material: a blue glass cylinder 0.17 across and 0.24 high under a dark cap |

The pieces below are placed by their **bounds**, not their pivot (`StationProps.Holder`): a floor piece is stood on the lowest point of its mesh with its footprint centred, a wall piece is hung by the middle of its back face (its lowest z).
So only the facing matters, and the size is read from the mesh (the cell is made as wide as the panel really is).

| prop id | size (m) | facing and layout |
|---|---|---|
| `precinct_desk` | 2.4 x 1.1 x 0.8 | floor. The public stand at +Z, staff at -Z. The code letters the precinct's name on the +Z face, centred 0.55 m up, in a strip 1.9 x 0.14: leave it plain |
| `precinct_bench` | 1.8 x 0.85 x 0.5 | floor. Back at -Z (against a wall), sat on from +Z; seat 0.45 m up |
| `holding_cell_bars` | 3.0 x 2.4 x 0.06 | floor. A flat panel in the XY plane with a door in it; seen from both sides. It is stretched along X by up to +-12 % to meet a wall, and the side panel of a corner cell shortened to as little as 77 % |
| `notice_board` | 1.2 x 0.9 x 0.05 | wall. Read from +Z; hung with its middle 1.55 m up, indoors and out |
| `police_shield_emblem` | 0.8 x 0.8 x 0.05 | wall. Read from +Z; hung 2.0 m up beside the door (or higher, over it) |
| `station_awning` | 2.4 x 0.5 x 1.1 | wall. Projects towards +Z from its back face; hung with its middle 2.75 m up (underside 2.5 m) over the door |
| `flag_pole` | 1.3 x 5.1 x 0.3 | placed by its own pivot: the foot of the pole at the origin, on the ground. The flag flies towards +X from the top (the code clears a box 1.4 long there) |
| `bollard` | 0.16 x 0.9 x 0.16 | floor |
| `office_desk` | 1.4 x 0.75 x 0.7 | floor. Whoever works at it stands at +Z; its back (-Z) goes against a wall. **New id**: a plain desk with a screen, built from shapes until it exists |
| `evidence_door` | 1.04 x 2.12 x 0.05 | wall, standing on the floor: a closed steel door in its frame, read from +Z. **Delivered.** The model has EVIDENCE on its plate in the mesh (English only) and the code letters only the stand-in (`reallife_law_station_evidence`): **for localisation** the model's plate would have to be left plain and lettered by the code at (0, 1.62) as the stand-in is. Likewise `precinct_desk` carries POLICE on its fascia in the mesh: the code's precinct name now goes in the plain strip under that word (0.34 m up, on the panel), where it used to be lettered across it |

| clip id | length, loop | what |
|---|---|---|
| `arrest_cuffed` | 2.4 s, once | **delivered and played** on whoever is arrested (`Crime/ArrestSequence`, so every crime scene, the roadside stop and the sting; `Law/Custody` for the player): straight out of `hands_up_hold` and on into `cuffed_stand` (`ActorRole.LeadInto`); mark `click` at 1.5 s |
| `officer_cuff` | 2.4 s, once | **delivered and played**, whole body (it steps in), its last pose held (`ActorRole.Act(clip, ClipEnd.Hold)`) until the suspect is walked off. The officer walks to the place it is measured from, 0.9 m STRAIGHT BEHIND the suspect and facing the same way (`ActorRole.GoExactly`; the suspect has turned their back first), and both clips start in the same tick; the last centimetres (0.25 m at most) are closed under the cross-fade, and from further off it is the old reach and the cut. The right hand holds `handcuffs` until the `click`, when they are put away and `CuffPose` shows its pair on the wrists: one pair is ever seen |
| `officer_guard_idle` | 8 s, loops | standing easy by a door: thumbs on the belt, weight from one foot to the other, a slow look left and right. For the officers at a precinct door, who only stand today |
| `cuffed_car_enter`, `cuffed_car_enter_l` | 2.0 s, once | **delivered and played** by `Crime/ArrestSequence` at the patrol car: the suspect walks to the place `job_car_enter` is made for (`StreetSpots.AtRearDoor`: square to the car through the seat, feet 0.75 m out from its middle; the `_l` clip on the car's left), turns square, and gets in in the clip, taken off the street at its `seated` mark. Only where their own agent has a whole route to that place and stops within 0.2 m of it; otherwise gone with the sound of the door as before. The door does not open (the game's traffic cars have none that does), as for the taxi's passenger. The player's own ride to the precinct is still a fade |
