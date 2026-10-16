# Driving licences

## In a sentence

A driving school with a theory test and a driven test, licence classes, penalty points and suspension, and enforcement at the roadside.

## Where the code is

- Folder: [`Scripts/Licences`](../../Assets/Mods/RealLifeOverhaul/Scripts/Licences/) and its [`Driving`](../../Assets/Mods/RealLifeOverhaul/Scripts/Licences/Driving/) subfolder. The files are listed in the code block below.
- Read first: `Licences.cs` (the one door for the rest of the mod), `LicenceState.cs` (classes, points and suspension, game-free) and `LicenceDirector.cs`.

## How it talks to other features

- Other features ask `Licences.Driving.Has(LicenceClass)`. A court's suspension and the licence's own are one date (`Wiring/LicenceLinks.cs`), so "suspended" has one answer.
- An arrest made by Licences is served through the courts' custody; see [Who does what at an arrest](../reference/wiring.md#who-does-what-at-an-arrest).

Everything is under `Scripts/Licences/` (and its `Driving/`).
Nothing unknown to the base game goes into a save: licences and points live in the save's `modData` as one plain line (`reallife:licences`), and the driving school's car is taken off the save's vehicle list for the length of each serialisation (`TestCar.BeforeSave`).

```
Licences/
  Licences          the one door for the rest of the mod: Licences.Driving.Has(LicenceClass), Licences.Law (ILawBridge), listeners
  LicenceState      classes, validity, points, suspension, retests: game-free rules and the modData line (LicenceLaw has every figure)
  LicenceDirector   loads/saves, the first-run grant, the desks' panel, the one test that is on, enforcement's tick, the test buttons
  SchoolDesk        the game's panel at a desk and the school's list (services, the licence card as a second page)
  SchoolScreens     the school's one list window (a GameUi.DeskList) and the plain report page
  LicenceProps      the things stood in the world, by id: the modelled prop if the bundle has it, else a plain-shape stand-in
  TheoryTest        a written test as a question card in the list: no repeats, a question from every subject, three answers and a way to give up
  Driving/
    DrivingSchool      what the school offers and when (DrivingSchool), the desks at the school buildings (LicenceDesks),
                       the Licensing Office contact (LicencePhone) and the conversation both use (DrivingSchoolDialog)
    DrivingQuestions   the 66 theory questions in seven subjects
    PracticalTest      a lesson or test, driven: examiner, checkpoints, what is marked, the bay, the result page
    TestRoute          five to seven checkpoints along real lanes and a marked bay at the kerb
    TestCar            the school's car: made the game's way, never in a save, the player seen out of it when the drive ends (and again if they get back in), taken back with the game's own delete
    DrivingEnforcement penalty points, the licence asked for at a stop, impound, arrest, the routine roadside check
```

## Driving licence: the rules

| class | covers | how it is got |
|---|---|---|
| Car | every car, SUV and pickup (`CarKind` Economy, Saloon, Luxury, Sports, Utility) | theory test (60; 10 of 66 questions, 8 to pass, a pass counts 30 days), one lesson (90), the driven test (220) |
| Commercial | vans and trucks (`CarKind.Van`, `CarKind.Truck`) | a valid car licence, one van lesson (90), the driven test in the school's van with the bay reversed into (380) |
| Taxi endorsement | carrying passengers for hire | a car licence held 7 days with at most 5 points, 180 |
| Motorcycle | every motorbike of the Garage (`Licences.Driving.TwoWheelerOf` says what a vehicle of the save is; a bicycle is none) | stands alone: the theory test, then 180 at the desk for the ride assessment. The school has no motorbike to lend, so nothing is driven: the assessment is recorded as passed at the desk (`SchoolService.Motorcycle`) |
| none needed | bicycle, electric scooter, hand truck, flatbed trolley, anything without an engine (`CarKind.Untouched`) | |

A licence runs 120 days; it can be renewed (90 each, at a desk or by phone) from 14 days before it runs out until 30 days after, and after that it is the tests again.
A failed driven test can be sat again from the next day for 120.

The first time the rules run on a save, a player who owns a car is given a car licence (and a commercial one if they own a van or truck, and a motorcycle one if the Garage lists a motorbike they own: `MotorcycleDecided` is saved with the line `moto=issued,expires,decided`, absent in an older save, and asked of `Licences.Driving.OwnsMotorbike` once a second until the Garage has read its list).

A save that owns nothing starts without one unless "Start with a driving licence" is on.
With "Driving needs a licence" off, `Licences.Driving.Has` is always true and nothing is policed.

Driving unlicensed is possible and is never discovered by magic: only when police actually stop the player (`PoliceAttention.Stopped`, `PolicePursuit.CaughtAfterFleeing`, or the routine roadside check: about once in forty minutes at the wheel, never within two days of the last, by a patrol car that comes up behind with its lights on; driving on from one is not chased but costs 400 and 3 points at the next stop).

| found at a stop | fine | vehicle | further |
|---|---|---|---|
| no licence | 800 | impounded: towed to a repair-shop bay by the game's own tow, the city pound's fee (300, `LawBook.ImpoundFee`) | the third time in 30 days: arrest (6 h, `ILawBridge.Custody`) |
| a licence for another class (a car licence on a motorbike or a van, a motorcycle licence in a car) | 500 | impounded | as above |
| expired, still renewable | 250 | stays | |
| suspended | 1500 | impounded | the second time: arrest |

A hired or job van is not impounded (it is not the player's): it "goes back to its owner".
The school's car on a lesson or test is lawful and is never checked.

| booked offence | points | | booked offence | points |
|---|---|---|---|---|
| red light | 3 | | failing to stop (fleeing) | 6 |
| speeding | 3 | | caught after leaving a collision | 8 |
| driving on the pavement | 4 | | a person injured (each, up to three) | 4 |
| against the traffic | 4 | | a death | 9 |
| drove on from a roadside check | 3 | | | |

Points count for 21 days.
Twelve suspend every licence for 7 days, after which the record starts from zero.

## The school in the game's own interface

- Desk: the game's panel over a place (`GameUi.WorldPanel` through `GameUi.DoorPanel`) says whether the school is open and has "Speak to the instructor" on the interact key.

  The list (`SchoolDesk`, a `GameUi.TravelList` on the shared `SchoolScreens.Window`) has an entry per service with the fee and what is needed under its name and its button off, with the reason, when it cannot be had (closed hours, the fee, a test still to pass, a wait after a failure).
  The texts are the dialogue's own pitch lines.

  Renewals, the taxi endorsement and the motorcycle assessment are done at once and the list stays; a test or a lesson closes it and starts as before.
  The licence card is a second page of the same list (a note per licence, one for the points, one for a suspension, Back): a card is short facts with names, which is what the list's entries are.

- Written test: a question card.
  The headline is the test, the first info line the question, the second "Question 4 of 12"; three entries "Answer 1..3" carry the answers; "Give up the test" counts as a failed paper (new).
  The list is `Locked`.
  An answer is a notification; the end is a page with the score and Close.
- Driven test and lesson: a copy of the game's job panel (`Jobs/ShiftCard`, reused as it is) with the instruction, the checkpoints passed, the faults and the time left, written five times a second only when a line changes.
  The next checkpoint is the game's destination marker (`GameUi.GuideMark`).
  At the bay the panel over a place has "I have parked" on the interact key.
  The result is a report page with Close.

  The school's car logic is unchanged.

- Fallbacks: no window, no written test (the fee is returned), the desk speaks through the dialogue panel, results are notifications.
  No job panel: each instruction is a notification.
- Log lines: "Licences check (desk panel)", "(desk list)", "(theory): question 4 of 12 ...: PASS", "(report)", "(choice)".
- Not checked in the game: any of it.

## The driven test

The school's car (a Honza Mimic; a Mersaidi Dash for the commercial class) is put at the roadside near the desk, the examiner walks out from the desk and gets in when the player is at the wheel, and the route is driven to a marked bay.

Marked ten times a second from the car's body against `Crime/RoadMap`:

- crossing a stop line on red (serious), stopping past it (minor);
- more than 10 km/h over the lane's limit for two seconds (minor) or 30 over (serious);
- out of the lane for three seconds (minor), on the pavement or against the traffic (serious);
- a stop harder than 9 m/s² from speed (minor), a touch (minor) or a collision of 2.5 m/s or more (serious);
- the parking: inside the box and within 12 degrees (else minor; more than 0.6 m over or 25 degrees off is serious), reversed into for the commercial class.

Pass: no serious fault and at most six minor ones.
A lesson is the same drive with each fault said as it happens.
Where no kerb within 150 m has room for a bay the test ends with a stop at a marker.
Signalling and stop signs are not modelled (the road data has traffic lights only).

## Bridges and hooks

- `Licences.Law` (`ILicenceLawBridge`): `Record(offenceId, arrested)`, `Custody(hours)`.
  `DefaultLaw` records nothing and uses `PoliceAttention.TakeIntoCustody`.
  A law feature sets `Licences.Law = itsBridge`.
- **For Jobs (not applied, Jobs is not ours to edit):** in `JobDirector.Available`, for the ride-hail job, `if (!Licences.Driving.Has(LicenceClass.Taxi)) { reasonKey = "reallife_job_no_taxi_licence"; return false; }` and for any driving job `Licences.Driving.MayDrive(car)`.
- Added elsewhere, all additive: `PoliceAttention.Stopped` (event, raised in `Ticket` and `Bust`) and `PoliceAttention.TakeIntoCustody(hours)`; `PolicePursuit.PeopleCharged` (event, raised in `ChargeForPeople`); `NearbyInteraction` stands aside while `LicenceDirector.TakesInteractKey`; two `TestRefusal` values (`OnJob`, `NotOnFoot`); settings, options and registration in `Core`.

## Asset wish-list (drops in by id, no code change)

| id | what | size, pivot |
|---|---|---|
| prop `driving_school_sign` | a sandwich board with a learner plate (white square, red L) on both faces | 0.62 wide, 1.05 tall, 0.30 deep; origin on the ground at its middle, faces along +Z |
| prop `test_checkpoint_beacon` | a slim marker post with a small flag | 0.22 across, 2.9 tall; origin on the ground |

Both are in the prop bundle now, and the code fits them as built:

- `driving_school_sign` came out 0.52 deep (feet at z +-0.26), not 0.30.
  Its middle stands 0.55 m from the wall (`DrivingSchool.FromWall`), so it reaches from 0.29 to 0.81 m out; the spot is tested clear to 1.6 m out, and the desk's place is 0.6 m beyond that.
  Nothing to change.
- `test_checkpoint_beacon` flies its flag to its own +X and shows it along +Z and -Z. `PracticalTest.Beacon` turns it so that a face looks back down the lane at the driver and the flag points in at the checkpoint (it stood unturned, the flag to wherever the world's +X was).
- `parking_box_marker` and `parking_box_posts` are in the bundle and deliberately not used: they are one fixed bay of 2.40 x 5.50 m, and the bay that is judged is sized to each route and vehicle (`TestRoute.BayHalfWidth`, `BayHalfLength`; `LicenceProps.SizeBay`).
  Drawing a box of another size than the one that is marked would be a lie, and scaling the posts' mesh makes their poles oval.
  The code-drawn bay stays.

## Firearms (removed in 0.37)

Firearms (licences, gun shops, a range, carrying, shootouts) were part of the mod until 0.37 and were removed.
What remains is `LicenceDirector.ForgetFirearms`, which once, on loading an old save, clears its firearms line (`reallife:firearms` in `modData`) and the two phone contacts `reallife-firearms-unit` and `reallife-gunshop`, and the two locale keys of those names that it needs; plus the three `reallife_transaction_gun*` labels, kept so that old bank entries still have a name.
