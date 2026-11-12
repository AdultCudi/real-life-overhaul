# Taxi and delivery shifts in your own car

## In a sentence

The player signs on at a desk outside the cab office or the parcel depot, on the game's own work panel, and drives metered fares or timed delivery runs in their own vehicle for as long as they like: the firm keeps its share, the fuel and the repairs are the driver's, and the firm's other drivers work by the same sums.

The owner asked for it: taxi and delivery side jobs with the player's own car, started at a desk at a place.

## Where the code is

- Folder: [`Scripts/SideJobs`](../../Assets/Mods/RealLifeOverhaul/Scripts/SideJobs/); the driving is the jobs' own, in `Scripts/Jobs` (the ride-hail and courier jobs in desk mode).
- Read first: `SideJobRules.cs` (every rule and figure, game-free, run by EconomyCheck's `SideJobChecks.cs`, flag `--side-jobs`), then `SideJobDirector.cs` (the desks' panel, signing on and off, the driver's figures).
- Option: "Taxi and delivery shifts in your own car" (`side_jobs`), under Jobs, on by default. Off, no desk stands, a shift that is on is called off and paid, and the Gig Line is as before. It also needs Jobs (`jobs`) on.

```
SideJobs/
  SideJobRules            game-free: which vehicle a desk takes, the cab's meter by distance and time, the firm's share, a ride's mark and
                          its tip, a vehicle's room, each drop's clock and pay, the fuel's cost, a firm's books for its other drivers
  SideJobState            game-free: the driver's figures at each desk (today, the day before, ever) and the seed of the firms' books
  SideJobStore            the save's one line, "reallife:side_jobs"
  SideJobBridges          what it asks of the rest of the mod: the firms' boards, the driver's vehicle, the jobs' director, the pump's price
  SideJobGame             the only file that reads the game: the day, a vehicle's seats, room, condition and tank, the notices
  SideJobDesks            the two desks: where they stand, the check line of each, the culling
  SideJobDirector         the one component: a second's look, the desk's panel, the sign-on on the work panel, the shift's end, the save
  SideJobDirector.Tests   Options, Tests
  SideJobText             the keys, and the feature's entry with its Guide facts (SideJobGuide)
Jobs/
  DeskShift               what a desk fixed at signing on, handed to the job with its context (as an employer's shift or a booked set is)
  JobDirector             TakeDesk (the gates), QuitDesk, OnDeskShift, and the police's bill passed to the job on (OnStopped)
  Kinds/TaxiJob.Desk      the cab shift: the car signed on with, the meter, the mark, the tip, the office's books, the day
  Kinds/CourierJob.Desk   the delivery shift: the pick-up at a business, the vehicle's room, each drop's clock and pay, run after run
Wiring/Wiring.SideJobLinks   the bridges filled in
```

## What was there already, and what this adds

The mod already had almost all of the driving, and none of it is written twice:

| already there | where | used as |
| --- | --- | --- |
| Ride-hail: a passenger at a door, boarding at the kerb, the ride's comfort (hard stops, bumps, speeding, crashes, police stops), the roof sign | `Jobs/Kinds/TaxiJob*`, `TaxiComfortRules` | the cab shift's driving, unchanged |
| Courier rounds: a pick-up, the parcel carried with the game's own box, doors in any order, the hand-over | `Jobs/Kinds/CourierJob*` | the delivery shift's driving, unchanged |
| The taxi endorsement and the licence classes (car, commercial, heavy, motorcycle) | `Licences` | the desks' gates |
| A car's seats (the game keeps none) and its kind | `Driving/CarCatalogue` | the cab office's gate |
| The employers Five Borough Cabs and Hudson Parcel Co., each at a help-wanted board outside a service building, on the map's Job offices row | `Jobs/CareerCatalogue`, `CareerDirector`, `JobBoards` | the cab office and the depot |
| The job's marker as the GPS's destination (`NavDirector` routes to a job's marker when the player has set none) | `Navigation`, `JobDirector.Guide` | the route to each fare and drop |
| Pay through `BizBooks.Book` as salary income, at the level's factor, the pay option and the city's price level (`JobDirector.PayScale`) | `Jobs/JobDirector.Pay` | every fare and run |
| The game's work panel for a mod's own shift (`ShiftSkip`), the game's panel over a place (`WorldPanel`) | `Jobs`, `Core/GameUi` | signing on |
| The player's own vehicle, never another Going Public player's (`JobDirector.NearestCar`, `GoingPublic.MineVehicle`) | `Jobs`, `Core` | the vehicle a desk reads |
| The phone's taxi (City Cabs sends one of the game's taxis) | `Transport/Taxi` | not touched: that is the player as a passenger |

What is new: the two desks and signing on at them; the gates on the vehicle (seats, room, condition, tank); the meter by distance and time; the firm's share; the ride marked for lateness and for what the police booked; the delivery run collected at a real business, sized by the vehicle, each drop on its own clock; run after run until the driver signs off; the desk's own day; the fuel priced at the end; the firms' books.

## The desks

- **Where.** Each firm's office is the help-wanted board the employers' ladder gave it (`CareerDirector.DepotOf`: the cab firm's at a car dealership, gas station or truck garage, the parcel firm's at an importer, truck garage or warehouse, else the first board free). The desk stands against the wall of that building, the other side of the door from the board (3 m along from the door, else 5.8 m, else beyond the board), where `CitySpots.Shopfront` finds clear pavement, at least 2 m from the door and 2.6 m from the board so that the board's reader and its boss are out of the desk's reach (1.2 m).
- **No building is stood.** The feature uses buildings of the city's that the jobs already use, so the new buildings' registration (card, BizMan, map filter, owner, occupants) does not apply; the office is on the map through the employers' Job offices row.
- **What it is.** A lectern-height desk of plain shapes with a sign in the firm's colour (yellow for cabs, blue for parcels), drawn no further than the game's far band (`ModCulling`, the boards' kind). A modelled desk can take its place later; the props bundle has none.
- **Checked as it is stood**, at every city load: `Side jobs check (desk, Cab): stands outside '<shop>', <address>, 3.0 m from the door and 5.4 m from the firm's board (wanted: at least 2.0 and 2.6): PASS`, or `FAIL` with the reason (the firm has no board in this city; no clear wall in the four places).

## Signing on

1. On foot at a desk, the game's panel over it has **Sign on** on the interact key or a click (`WorldPanel`, as at a board).
2. The desk reads the driver's own vehicle within 80 m (`SideJobGame.Read`) and asks `SideJobRules.Refusal`:

| | cab office | parcel depot |
| --- | --- | --- |
| a vehicle of the driver's own, with an engine | yes | yes |
| a valid licence for its class (`Licences.Driving.ClassOf`: a van's is the commercial one, a heavy truck's the heavy one) | yes | yes |
| the taxi endorsement | yes | no |
| seats, the driver's included | 4 or more | any |
| room (the boxes the game says it holds) | any | 2 or more |
| condition (100% less the game's damage) | 60% or better | 40% or better |
| the tank | 10% or more | 10% or more |
| the desk's day | 24 fares | 30 drops |

   A refusal is one notice that says what is wanted.
3. The sign-on goes on the game's own work panel (`ShiftSkip.Offer`, the panel of the game's El Gato job): the vehicle, the terms, what is left of the day, the firm's books of the day before, and Start and Cancel. Start runs 10 minutes on the game's clock, with its own skip.
4. When the panel has closed, `JobDirector.TakeDesk` starts the gig as a desk shift: it asks again what the Gig Line asks (a free player on the street, not sacked, the energy, the endorsement, no police stop on a job for two days), but the desk's day in place of the Gig Line's, no reference and never a hired van.

A hired van, a job van of the game's and another Going Public player's car are never the driver's own.

## A cab shift

The ride-hail job as it is, with these differences (`TaxiJob.Desk.cs`):

- **The car** is the one signed on with: a fare does not board another (the card says so).
- **The meter** (`SideJobRules.Fare`): 85% of the gig's fare by distance (22 and 0.195 a metre) and 0.21 for each second of the ride, counted no further than 1.5 times the ride's par (its metres over 7.5, and 20 seconds). A ride at par comes to the gig's fare; a slow one to 8% more at most; standing still earns nothing; a ride under 250 m is no fare (the job plans none under 280 m).
- **The office's share** is 20% of the meter. The slip carries what reaches the driver.
- **The mark** (`SideJobRules.Quality`) starts as the passenger's comfort and loses up to 25 for a ride slower than par, 5 for a pick-up in the last quarter of the passenger's patience, 8 for each offence on a police bill during the ride (`PoliceAttention.Stopped`, passed on by `JobDirector.OnStopped`) and 30 for having driven off from the police first.
- **The tip** (`SideJobRules.TipShare`) is 26% of the meter at 90 or better, 15% at 75, 6% at 55, else nothing: the middle of what the gig's passengers give. Crashes, police stops and a ride under 30 cut the slip as on the gig.
- **The day**: 24 fares; the shift ends, done, when they are driven.

## A delivery shift

The courier round as it is, with these differences (`CourierJob.Desk.cs`):

- **The pick-up** is one of the city's businesses that is somebody else's and open (`JobSites.AnyClient`), 40 to 420 m from the driver. A business that cooks (`SideJobRules.IsFood`, by its type's name) sends meals; any other, parcels.
- **The run's size** is the vehicle's room (`SideJobRules.Drops`: two and one for every four boxes it holds, 8 at most; 3 for meals) and never more than the day has left.
- **Each drop has its own clock** from loading (`SideJobRules.DueIn`: 50 seconds, the planned road to it at 4.5 m/s, and 38 seconds a door; a meal's is four fifths of that). The card counts down the drop being headed for.
- **Each drop's pay** (`SideJobRules.DropPay`): the gig's 50 a door and 0.085 a metre of its own leg, at the driver's rate, 15% more for a meal, times its punctuality (`Punctual`): 110% with a quarter of its time to spare, 100% on time, then less by the second down to 50% at two minutes late. A run all on time pays what the gig's round does.
- **Run after run**: a whole run is paid and the next is planned at once; signing off between runs leaves nothing undone. A run not finished 1.6 times its own time after loading is called back, as a round out of time is.
- **The day**: 30 drops.

## Money, fuel and wear

- Every fare and run is one of the jobs' slips: `BizBooks.Book` under the gig's transaction (`reallife_transaction_job_taxi`, `reallife_transaction_job_courier`) in the game's salary income category, so the game's statements and its tax count it. The level, the Job pay option and the city's price level are in the rate. A fare or a drop driven from a desk counts towards the gig's level and the Gig Line's own day as one from the Gig Line does (`JobState.Worked`); the desk's day is counted apart, in the feature's own line.
- **Fuel** is the driver's by the game's own burning of it. The director reads the tank once a second while a shift is on, sums what was burned and prices it at the game's pump (`LogisticsGame.PumpCents`) for the notice at signing off. Nothing is charged twice.
- **Wear**: the mod keeps no wear by distance, so none is added; damage is the game's, and a damaged vehicle is refused at the desk.
- One notice at signing off: the work, what the customers paid, what the firm kept, the tips, what reached the driver, and the fuel.

## The other drivers

The game has no taxi business and the mod models no wage for a driver, so the firms' side is their books (`SideJobRules.Books`): each day so many other drivers (6 to 12 at the cab office, 5 to 10 at the depot), each with a level and a day's work inside the desk's day, each fare by the same meter and share and each drop by the same pay and punctuality, at the city's price level. They are drawn from the save's seed, the day and the firm, so they are the same whenever they are asked, and nothing is saved but the seed. The sign-on panel shows the day before's: how many drivers, what customers paid, what the firm kept, what the best of them cleared. The employers' own ranks (Cab driver to Owner-operator, Courier to Depot supervisor) are untouched.

## Save

`reallife:side_jobs`: the seed, the day, and for each desk the day's figures, the day before's and the totals. A shift that is on at a save is the jobs' (`reallife:job_live`): settled at the load, never resumed, so a loaded game has no shift on.

## Going Public

Only the player's own vehicle is read and driven; the passengers and recipients are the jobs' own street actors, as on the gigs; nothing is sent. The firms' books come from the save's seed. The sign-on uses the game's work panel as the Marketplace's work does.

## Cost

No Update. A one-second look: a few comparisons, the distance to two desks, and one read of the tank while a shift is on. The desks are stood once a city load (two shopfront tests each at most four times). A frame-by-frame wait only for the moment the work panel takes to close after signing on. The books are a few hundred sums on a press.

## Tests (Options, Test tools, Jobs)

- **Side jobs: sign on for a cab shift from where you stand** and **... for a delivery shift ...**: the shift in desk mode in the player's own vehicle near by, whatever its seats, condition or their licences; counts for nothing.
- **Side jobs: write the desks and the firms' books to Player.log**: where each desk stands, the driver's day, each firm's books of the day before asked twice, with PASS or FAIL.
- Log lines with Developer diagnostics on: `Side jobs check (desk panel)`, `(sign-on)`, `Jobs check (desk run)`; always: `Side jobs check (desk, ...)`, `(signed off)`, `Jobs: taxi desk fare: ...`, `Jobs: courier desk drop ...`.

## Not checked in the game

Nothing here has been run in the game. In particular: where the desks come to stand and how the plain-shape desk looks; that the work panel opens on the street beside a desk; that the 10 minutes end with the shift starting; and how the card reads with a drop's own countdown.
