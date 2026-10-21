# Jobs

## In a sentence

Gigs the player takes from the **Gig Line** contact or a help-wanted board (flyers, cleaning, shelf stocking, the drinks cart, busking, courier rounds and ride-hail), worked in the game's own interface and paid by the piece.

## Where the code is

- Folder: [`Scripts/Jobs`](../../Assets/Mods/RealLifeOverhaul/Scripts/Jobs/) and its [`Kinds`](../../Assets/Mods/RealLifeOverhaul/Scripts/Jobs/Kinds/) subfolder, one class per job.
- Read first: `JobDirector.cs` (the one job that is on), `JobCatalogue.cs` (every figure), `Job.cs` (the base every job derives from), then one job in `Kinds/`, such as `FlyerJob.cs`.
- The employers and ranks behind the boards live in the same folder (`Career*.cs`): see [Careers](careers.md).

| File | What it is |
| --- | --- |
| `JobCatalogue` | every job: needs, pay per unit and per metre, energy, skill, daily amount, levels (game-free) |
| `JobState` | per-save experience, strikes, sackings, today's and this week's totals: one line in modData (game-free) |
| `JobDirector` | the one job that is on: start, 0.2 s tick, interruptions (indoors only in the one building a job names), pay, the test buttons; its parts are Boards, Ending, Pay, Street and Tests |
| `Job` | the base every job derives from, the pay slip, what a job is handed each tick, text helpers |
| `JobStand` | a place of work taken by walking on to it: the last step, the turn, the hold, the working pose, stepping away |
| `JobUniform` | work clothes over the player's own (the game's UpdateVisuals(list)); the director takes them off on every way out |
| `ShiftSkip` | the rest of a shift left to the game's clock: one of the game's activities on the game's own panel, as its employer job |
| `ShiftCard` | the shift's figures on the HUD: a copy of the game's job panel, five lines written only when they change |
| `CareerClock` | a shift's time as start to end in the game's clock format: the one formatter every text asks |
| `JobKeys` | the interaction key and the key names in texts: fills {key} and {key1} to {key3} |
| `JobPhone` | the Gig Line contact and its conversation, on the phone or face to face at a board |
| `JobBoards` | help-wanted boards outside a few service buildings: the same list as the Gig Line |
| `JobCast` | the two characters jobs put on the street (never the accident pool's) |
| `JobSpots` | door fronts and the ground beside the player's car, tested before anyone is put there |
| `JobSites` | a client's premises: which businesses will do, clear floor and shelves inside that the player can walk to |
| `IJobAnimations` | IJobAnimations and JobClip: the one door to another person's body; `ClipJobAnimations` plays the mod's clip where the bundle has one, `GameJobAnimations` the game's gesture where not |
| `JobBody` | the player's own body at work: held on the spot, a clip with its tool, or the game's own animation and hand object |
| `JobProps` | the jobs' props, pooled (modelled, or the prop that stands in, or a plain shape); HeldProp: one in a hand at its clip's grip |
| `JobCarry` | a box in the player's arms with the game's own carry |
| `JobRental` | a van hired for the day: the game's own job van, made and removed the game's own way |
| `Kinds/` | the jobs: FlyerJob, CourierJob, TaxiJob, CleanerJob, StockerJob, VendorJob, BuskerJob; and the two an employer's rank gives: DispatchJob (the planning desk), LeadJob (a shift in charge) |
| `CareerCatalogue` | employers, ranks, rotas and every figure of the career ladder (game-free) |
| `CareerState` | per-save employer, rank, rota, marks, review figures, employment history: one line in modData (game-free) |
| `CareerDirector` | where each employer is (a help-wanted board), its boss by the board, the rota, clocking on, payslip, reviews, booked sets |
| `CareerPhone` | the Work contact and its texts |
| `CareerDialog` | the Work contact's conversation, by phone or face to face with a boss: sign on, rota, clock on, sick, notice, CV |
| `CareerLinks` | the optional doors to other features: acquaintances (offered), law record and sick notes (asked for) |
| `DispatchList` | the dispatcher's planning board: the game's list window, written only when a parcel is placed or taken back |

## How it talks to other features

- Other features may read `JobDirector.ActiveJobId`, `LastJobId` and `EarnedToday` at any time. A shift also moves the player's mood (`ModState`); see [The hired van](#the-hired-van) below.
- The employer ladder's optional doors are in `Jobs/CareerLinks.cs`, filled in by [`Wiring/LifeLinks.cs`](../reference/wiring.md) (the record on a CV, a doctor's sick note).
- Shared pieces other features use: `ShiftCard` (the copied job panel), `ShiftSkip` (the rest of a shift left to the game's clock), `JobStand` and `JobUniform`.
- A shift in progress at a save is settled on load: see [A shift across a save](#a-shift-across-a-save-jobsave-payslip).
- To add a job, see [Adding a job](../contributing/adding-things.md#adding-a-job).

The game has its own work: employer jobs (stand at a station in someone's shop while time is skipped, paid by the hour), the delivery van (a company van, a hand truck, boxes of stock to three shops, 100 a drop) and food delivery (accepted at a restaurant's till, 25 plus 0.05 a metre).

The jobs here sit beside those and never touch them: a job cannot start while one of the game's missions is on, and ends if one is taken.
They are found on the phone (the Gig Line) and at the help-wanted boards.

| job | needs | a unit is | pays | a day |
|---|---|---|---|---|
| Flyers | nothing | a flyer taken | 7, +40 % on a busy street, +10 % for the whole bundle | 48 (two bundles of 24) |
| Cleaner | a business near that is open | an eighth of the client's floor cleaned (the game's own dirt, to 100 %) | 20, by the share of the dirt taken off | 24 (three floors of 8) |
| Shelf stocker | a shop near that is open | a box carried in from the kerb and shelved | 33, +10 % for the whole delivery | 18 (three deliveries of 6) |
| Drinks cart | a pavement with room for the cart | a customer served the drink they asked for | 18, tips of 2.75 to 9.50 for quick, +10 % if nobody walked off | 36 (three shifts of 12) |
| Busker | a pavement | a song | tips only: about 17 a song, times 0.5 to 1.5 for how many are about, 0.4 to 1.4 for the hour, 0.85 to 1.25 for the song, by how it was played; 2.75 to 8.25 from each listener | 16 (two sets of 8) |
| Courier | a car (own, or a van hired for 125 a day) | a parcel delivered | 50 + 0.085 a metre of the round, +15 % for beating the clock, tips of 5.50 to 16.50 | 15 (three rounds of 5) |
| Ride-hail | a car (own or hired), a reference (level 2 in any job), no police stop on a job for 2 days | a fare | 22 + 0.195 a metre, tip up to 30 % by comfort | 14 |

Every figure is in `JobCatalogue`, set against what the game itself pays (read from its data files by `JobAssets/scripts/extract_wages.py`: the one employer job, cashier, is 25 an hour; the game's own staff start at 12 an hour for cleaning, 16 for customer service, 18 for delivery driving; the van job is 100 a drop).

Pay is multiplied by 1 + 0.08 per level above the first (a level every 40 flyers, 30 patches, 20 boxes, 30 customers, 20 songs, 15 parcels, 12 fares; four levels) and by the "Job pay" option.
Deductions only ever reduce what a job pays; a slip never goes below zero.
Three failed shifts in a job and it lets the player go for three days; a good shift takes a strike off.

Work spends energy and trains a skill through the game's own systems, and never takes energy below 12.

## Pay against the game's

What the game pays and charges, read from its data files (`JobAssets/scripts/extract_wages.py`) and its code, and what the gigs were set against.
One game hour is one minute of play; a gig shift is four game hours (the hours an employer pays basic wage for).

| in the game | figure | where |
|---|---|---|
| the player's own job: cashier at El Gato Food Market | 25 an hour, 8 to 16, six days: 100 for a four-hour shift | `PlayerActivity/WorkActivity` pays `floor(hours x hourlySalary)` |
| its staff at no skill | cleaning 12 an hour, customer service 16, delivery driver 18 | `SkillData.baseHourlyWage` |
| its staff with skill | those times `1 + skill^1.05 / 100`: 19 to 29 at skill 50 | `EmployeeHelper.CalculateHourlyWageForSkill` |
| the delivery van job | 100 a drop (150 at one depot), three drops, about three game hours: about 100 an hour | `Vehicles.DeliveryDriverJob` |
| food delivery | 25 + 0.05 a metre within 500 m | `Player.FoodDeliveryJob` |
| a taxi ride (the player pays) | 0.15 a metre: 90 for 600 m | `TaxiSystem.GetPrice` |

The first release's gigs paid 14 to 22 an hour on foot (under the cashier's 25) and 30 to 55 with a car, which is what the maintainer found far too little against 48,500 in hand and 187 a day in costs.

The new table raises every gig's unit pay by about 2.8 (unskilled 2.75 to 2.9, courier and ride-hail 2.8, the two desk shifts 3.0), which puts a shift played well at 40 to 55 an hour on foot (1.6 to 2.2 times the cashier) and about 95 to 105 an hour of play with a car (the van job's 100).
Skilled work (courier, ride-hail, the desk shifts) pays more an hour than unskilled.

Nothing is said to cost more energy or to level faster.

| gig | unit pay before | unit pay now | whole shift before | whole shift now | an hour of the shift now |
|---|---|---|---|---|---|
| Flyers (24) | 2.50 | 7 | 60 | 168 | 42 |
| Cleaner (8) | 7 | 20 | 56 | 160 | 40 |
| Shelf stocker (6) | 12 | 33 | 72 | 198 | 50 |
| Drinks cart (12) | 6.50 | 18 | 78 | 216 | 54 (and tips) |
| Busker (8 songs, tips) | 6 | 17 | 4 to 16 a song | 11 to 45 a song | by the street and the hour |
| Courier (5) | 18 + 0.03 a metre | 50 + 0.085 a metre | 110 to 130 | 310 to 340 | 78 to 85 (about 100 of play) |
| Ride-hail (a 600 m fare) | 8 + 0.07 a metre = 50 | 22 + 0.195 a metre = 139 | 50 a fare | 139 a fare | about 100 of play |
| Dispatch desk (3 plans) | 14 a plan | 42 a plan | up to 42 | up to 126 | plus the rank's salary |
| Shift in charge (4 calls) | 12 a call | 36 a call | up to 48 | up to 144 | plus the rank's salary |

Everything else that is money moved with it, so no ratio changed: the tips in the courier's, cart's and busker's code.

These moved with it:

- the employers' basic wage by rank (gig ranks 4 to 9 an hour became 11 to 25, the salaried desk and lead ranks 18 to 30 became 36 to 60);
- the raise step (1 to 3);
- the cart rent (60 to 165) and the cart's takings and stock (11 and 3.5 to 30 and 9.5);
- the booked set's fee (45 to 125) and the hired van's daily fee (45 to 125);
- what a review counts a tip for (a point a dollar to a point for 3, twenty at most).

The 85 % rate of the game's clock (`ShiftRates.Share`), the crash and police-stop cuts, the level bonus (8 % a level) and the "Job pay" option are shares or factors and needed no change.
Every figure the Guide quotes (`job_table`, `job_van_fee`, and the pitches' `{pay}`, `{km}` and `{shift}`) is read from these constants.

## No two shifts alike (`Jobs/GigOffers`, `Jobs/GigWords`)

Everything that makes one shift of a gig differ from another is drawn in one place, `GigOffers.Draw`, once, from a seed made of the day, the hour and the gig (`Mix`, and a small xorshift `GigDice` that is the same on every machine).
Asking again in the same hour gives the same offer, so the Gig Line's quote is the shift that is worked.
The jobs roll none of this themselves; they read `JobContext.Offer`.

The Gig Line (`JobPhone.Offer`, also read out at the boards) says the fee, the size and the reasons in one sentence; the first line of the shift's panel (`ShiftFigures`) repeats the reasons ("Gig Line, a generous client, a busy day"); each event is a notification when it happens.
Employers' shifts, booked sets and the test buttons are flat: `GigOffer.Table`, the table's figures, none of this.

| what | range | average | shown |
|---|---|---|---|
| the client | tight 80 to 95 % of the fee (40 % of offers), fair 95 to 107.5 % (40 %), generous 107.5 to 137.5 % (20 %) | exactly 100 % | the quote, the panel |
| the day (which gigs are busy) | each day 2 or 3 of the 7 gigs are busy and pay 120 %; about one day in two one gig has nothing on and cannot be taken ("Nothing in that today"); every other offered shift pays 87.5 % | busy 2.5 and shut 0.5 a day; the fee over offered shifts exactly 100 % | the quote, the panel |
| the size | flyers 18 to 30, cleaning 6 to 10 units of floor, stocker 4 to 8 boxes, cart 9 to 15 customers, busker 6 to 10 songs, courier 3 to 7 parcels (ride-hail has no set size) | the table's number | the pitch, the panel |
| the street (flyers, cart, busker) | quiet 70 %, normal 100 %, rush 130 % (25 %, 50 %, 25 %): how many take a flyer, how soon customers come, what the hat holds | exactly 100 % | the panel |
| a big tipper (cart, busker, courier, ride-hail, shelf stocker) | 25 % of shifts, at one unit of the shift, 1 to 2 units of pay (a share of the fare for ride-hail) | 0.375 units a shift | a notification, the slip's tips |
| a speed bonus (flyers, cleaning, shelves) | 30 % of shifts, 0.5 to 1.5 units of pay if the work is done by hand in 60 % of the time allowed | 0.3 units a shift, taken as won | a notification at the start and at the end, the slip's bonus |
| a difficult customer (cart, cleaning) | 20 % of shifts: the cart's customer sends the first right drink back (3 seconds off their patience, a new order); the client finds a patch missed and 30 % of the dirtied cells are dirty again at 80 % of their dirt | none in money; the pay follows the floor | a notification |
| weather pay (outdoor gigs, weather option on) | rain +15 %, storm +25 %, heat wave or cold snap +10 %, ice +20 % | extra, only in bad weather | the quote, the panel |

Fair means: for a shift played well in fair weather the pay averages the table's figure.

The client's factor, the day's factor and the street's factor each average exactly 1, the size is symmetrical round the table's number, and the money the events are expected to add (a tip's and a speed bonus' chances times the middle of their ranges, as a share of the shift) is taken off every fee (`GigOffers.Calibration`, 0.94 to 1 by gig), so adding them back gives 1.

That is why the quoted fee of a gig with events averages a little under 100 % (stocker 90 %).
Weather pay is not taken off: it is a bonus for bad weather.
The pay slip is the game's notification as before; tips and the speed bonus are on its lines.

Checked offline by `CatalogueCheck` (`GigCheck.cs`), which draws about 5,500 seeded offers for each gig: the same day and hour give the same offer; the ranges above are kept.
The client, day and crowd factors average 1 within 2 %.
The size averages the table's within 3 %.
A whole shift, events included, averages the table's within 2.5 % (measured 0.993 to 1.002 of the table, 0.998 over all gigs).

The day's board has 2 or 3 busy and at most 1 shut gig a day and averages 2.49 and 0.51 over 3,000 days.
A career shift gets the flat table.
Weather pay applies out of doors only.

It also checks the pay table: every gig on foot pays at least 1.5 times the game's cashier an hour (the busker, whose pay is tips, at least the cashier's), courier and ride-hail more than the best unskilled gig and no more than the van job's 100 an hour, both desk units more than any unskilled gig's, and the own cart nets about the cart gig's pay.

Log lines with "Developer diagnostics" on: `Jobs: offer for 'cleaner' (day 14, hour 9, seed ...): Generous client x1.2 ...` when a gig starts; `Jobs 'x' event: a big tipper at unit ...`, `... the client adds ... if the shift is done within ...`, `... speed bonus kept ...`, `Jobs: vendor event: a difficult customer sends the drink back ...`, `Jobs: cleaner event: a difficult client points out a missed patch ...`.

## The cleaner works on the game's own dirt (`Kinds/CleanerJob`, `JobDirt`, `JobMop`)

How the game cleans: every interior has a floor of `DirtSpotObject`s, each reading its dirtiness (0 to 100) from its place in `BuildingRegistration.dirtSpots`; `BuildingCleanliness.GetCleanliness()` is 100 minus the mean dirtiness minus the mean over spots of 5 or more (100 when none shows).
The player takes a mop from a cleaning station (`CleaningStationController.OnCleaningStationClick`: `PlayerHelper.ItemInstanceInHands = ItemHelper.InitializeNewInstance("ba:itemname_mop")`), which shows the dirt highlighting (`MopController.AssignToPlayer`, `BuildingCleanlinessHelper.ShowDirtinessHighlighting`).

Clicking a dirt spot (`DirtSpotObject.Interact`, `BuildingManager.onFloorCellClick`) starts `MopController.FloorCellClick`, which cleans the clicked cell and those round it at 25 dirt a second shared among them, with the game's cleaning stance and a minimum of 0.2 s, and ends with the item panel's bar refreshed.
The game's tasks list stops flagging dirty floors at 90 %; the job asks 100 %, which is the game's own "no dirt shows" figure.

In a business that is not the player's the objects exist but `dirtSpots` is empty or missing (`BuildingHelper.ResetRegistrationOnRent` makes it only for the player's), the click is hooked only in the player's own business, the item panel reads 100 % and the station refuses anyone but the owner.

So `JobDirt` makes its own list for the client's floor (the same cells and order), puts dirt in it with `BuildingCleanliness.SetDirtiness` (patches, not single cells: some centres at random and the cells nearest them, at most six cells a unit, 55 plus 4 a unit above eight dirt each, 75 to 125 % of that), and hangs it on the client's registration while the player is inside.

The game's floor objects, its mop and its percentage then work as in the player's shop.
It is put back whenever the player leaves, when the job ends and while a save is written (`SaveHook`; the save is made on the main thread right after the event), so no job dirt is ever saved, and a business the player rents is refused outright.

`JobMop` gives and takes the mop with the game's own calls, hooks the mop's click method to the floor click event for the length of the mop, and sets the item panel's bar to the real figure.
If the game's dirt cannot be had (no floor cells, no floor object under the player, any exception), one line says why and the job uses the mod's own spills as before (`CleanerJob.Spills.cs`).

Pay is the fee (units times the unit's pay) times the share of the dirt taken off, the whole fee at 100 %, half or more is a shift done; the clock cleans the rest at 85 % and sets that share of the dirt to nothing through the game's call.
Log lines: `Jobs: cleaner: the game's own dirt is down at ...`, `Jobs: cleaner: the floor reads 62% clean (was 55%) ...`, `Jobs: cleaner: pay slip: ...`.

The cleaner and shelf stocker choose somebody else's open business within 15 to 260 metres. `JobSites.RandomClient` also checks the game's opening schedule for enough continuous open time: the walk (0.5 seconds a metre) plus the longer of the hand-played limit (330 seconds for cleaning, 300 for stocking) or the whole offer left to the clock (30 game minutes a floor unit, 40 a box), converted at the game's clock speed, plus 15 game minutes clear of closing. A shift that mixes the two fits inside that, and a client that closes anyway ends the shift without a strike (below). Adjoining slots and midnight are allowed; any closed hour stops the available time.

Both jobs check the game's current entry restrictions before working, before entering and during each step of a time skip. A client that closes or refuses entry calls the shift off as `JobEnd.Interrupted`, with no strike. Completed clock units are paid before the activity is cancelled; the usual end pays the hand work and gives back the dirt, mop and boxes. The clock learns when it starts how long the client stays open (`JobSites.MinutesOpen`) and counts no unit finished after that (`ClockRules.Units`): the game's time machine reports a run's minutes once, at its end, so a client found shut then is still paid for what was done before it closed. The client is named in a short notification. Diagnostics say which pick lacked open time and which client called a shift off. `EconomyCheck --client-hours` checks these paths and the texts in all nine languages offline.

## The drinks cart: the model of a shift in the game's own interface (`Kinds/VendorJob`)

The cart shift (an employee's rota shift at Uncle Lou's, or the gig) shows nothing of the mod's own.
`Job.NativeUi` is true for it, and the director then reads no key for it.
What the player sees and does is built from four shared pieces of `Scripts/Jobs`, each usable by any other job:

- **`JobStand`: the place is taken by walking on to it.** `Place(post, facing, reach, idleClip)` names a zone; the job calls `Tick` on its own tick.

  A player on foot inside the zone is held (`JobBody.Hold`, the game's navigation blocker), drawn the last step to the exact place and turned (a coroutine of the director's, per frame for the half second it takes and never otherwise), and given the looping clip as the working pose (`counter_wipe`, the living city's vendor's; the game's own standing if the bundle lacks it).

  A one-off motion goes through `Busy`, after which the pose comes back by itself.
  `StepAway` frees them and the zone will not take them again until they have left it by 0.6 m.
  The director calls `Clear` on every way a job can stop.

- **`JobUniform`: work clothes over the player's own.** The game dresses the player for a while with `AppearanceSetter.UpdateVisuals(list)` (its swimwear, the uniform at its own tills): the pieces of the list are drawn where there is one and the character's own data is not written.

  `Wear(player, CityLook.Vendor)` builds that list from the living city's outfit (`CityOutfits.Wear` into a list of the caller's own: white shirt with red trim, charcoal trousers, red hat) and calls it; `Off` puts back what was recoloured and calls `UpdateVisuals()`.
  Nothing reaches the save: a save made in uniform holds the player's own clothes and loads the same with or without the mod.

  Where the player is drawn by the detailed double (`Character/PlayerDouble`), the double is dressed through `PlayerDouble.Workwear`, a delegate it calls with the copy of the data it is about to dress from.
  The director calls `Off` in `EndJob` (every ending, a fault and the feature switched off included) and in `OnDestroy`.
  Each change is one rebuild of the player's look, counted in `Looks`.

- **`ShiftSkip`: the rest of the shift by the game's clock.** The game's employer job is `PlayerActivity.WorkActivity` on `PlayerActivityUI`: Start and Cancel with the game paused.
  Started, the player is kept at the till, the panel shows a progress bar with the time remaining, the game's skip button on its special key (it runs `TimeMachine.StartTimeMachine` to the end of the shift) and Stop.
  `Finish` pays the hourly wage for the minutes worked.

  `ShiftSkip` is an `IPlayerActivity` on the same panel with the same buttons and keys, for a job that implements `ISkippableShift` (how many minutes are left, what the offer says, what to do when the clock starts, as minutes pass and when it ends).

  No walk order is given (the player is already held at the stand), so the panel's cancel-on-walk-order trap (see Home) cannot fire; the activity is "on its way" for one frame because that is the step in which the game's panel lets the clock run.
  While `Job.PassingTime` is true the director does not take the running time machine for an interruption.

  Differences from the game's job: the game's fast-forward switch is offered by the game only indoors, so on the street the panel has the skip button alone; and no customers are shown while the clock runs.

- **`ShiftCard`: the figures, for the whole shift.** A copy of `PlayerHUD/CurrentJobUI/Panel` made asleep (`UiClone`), its two buttons and every raycast target taken off, three more lines copied from its own, a canvas of its own, 336 high.
  It hangs on the game's `CurrentJobUI`, in the game's place for it, and under the game's own panel while the player also has the game's job.
  `Title` and `Line(n, words, colour)` write only on change.

For the cart: the rule of the clock is `ShiftRates.Minutes` (20 game minutes a customer: twelve are the four hours an employer pays basic wage for) and `ShiftRates.Share` (85 % of a customer's pay, no tips, no "nobody walked off" bonus).

Units, energy, the level, the employer's hours, lateness, marks and the review are settled by the same code as a shift by hand (`Ctx.Pay`, `CareerDirector.ShiftEnded`); tips count towards a review, so a shift left to the clock scores lower.

The order and the three drinks are the game's panel over a place (`GameUi.WorldPanel`, now with four places: the game's interact, secondary, special and confirm keys) over the cart: three buttons, one per drink, because the game writes each button's key itself, runs it on the key or a click, and the three keys are the three the game sends to that panel.

Calling out is on the confirm key in both states of the panel; "Let time pass" and "Step away" are on the special and secondary keys while nobody is ordering, and do nothing for 0.7 s after an order ends (a key meant for a drink).
The pay slip is one notification (`reallife_job_slip_note`, or `reallife_career_slip_note` with the basic wage).

**Calling out** (`VendorJob.Hawk`).
Each of the game's pedestrians within 14 m (six at most) rolls once: annoyed with 0.10 + 0.13 for each call in the last 25 s.

Else tempted with 0.32 x the hour (0.5 at night, 0.8 before nine, 1.2 from eleven to two, 1.3 from four to nine) x the pavement (0.8 to 1.2 by how many are within 30 m) x the player's customer service skill (1 to 1.5) x the queue (0.6 with one at the cart, 0.3 with two), less a fifth for each recent call.

Tempted: the game's green face, up to two are borrowed (`PedestrianTakeover`, as the busker's listener) and look at the cart for four to seven seconds, and a customer more is added to the shift (four at most, never past a gig's daily amount) who arrives within a couple of seconds.
Annoyed: the red face and `JobClip.Annoyed`, and they are not asked again this shift.
Ten seconds between calls.

With three recent calls and police presence above zero, a warning by chance (0.12 + 0.03 x the presence option); the second ends calling out for the shift.
No officer is put on the street for it.
The player's motion is `JobClip.Hail` over the working pose; no sound is played (the game's sound list was not read for one that fits).

**The cart is solid** (`Core/SolidFootprint`, shared with the living city's two carts): see [What cuts the navigation mesh](../reference/performance.md#what-cuts-the-navigation-mesh-and-when).

`VendorJob.Placement` checks the whole cart, vendor and queue against crossing colliders, including triggers, before putting it down, through the same `CitySpots.ClearOfCrossings` the chess scenes use. The player can take their place only after the cart's cut has settled and the vendor, counter and queue are still reachable. A failed check takes the cart back and tries the next candidate after the mesh closes; with none left, the shift ends without a penalty.

`CartQueueRules` releases a customer with a failed route, no progress for six seconds or a walk longer than forty seconds, without counting a walk-off. A failed step from the queue to the counter also releases them. `VendorJob.Customers` bounds attempts to send a customer from an unseen starting place to forty seconds too, including refused routes and failed walk orders. Each failure puts the customer back among those to come; after four failures in a row the shift is one shorter. These recoveries are logged with Developer diagnostics on.

Log lines with "Developer diagnostics" on: `Jobs: vendor cart put down ...`, `Jobs check (cart solid): ... PASS`, `Jobs: stand entered ...`, `Jobs: uniform on (Vendor, ...; worn before: ...)`, `Jobs check (cart panel): ... PASS`, `Jobs: vendor: customer served by hand (...) in 2.8 s, tip 3 ...`, `Jobs: vendor: called out. Chances: ...`, `Jobs: vendor: the rest of the shift is left to the clock: ...`, `Jobs: vendor: shift skipped by the clock: 3.0 game hours ..., pay ..., energy ... -> ...`, `Jobs: stand left (...)`, `Jobs: uniform off ...`, `Jobs: pay slip shown as one of the game's notifications ...` or `Careers: payslip shown as ...`.

Not verified (the game could not be run): everything that is seen.

In particular how the copied job panel lays out with five lines, where the game's panel over the cart stands on screen with four buttons, whether the hat and shirt exist under the same names in the detailed character set (if not, the double keeps the player's own piece there and the outfit's log line says so), and that the walker's radius leaves room for a cut (the check line says).

**The other jobs on the same pieces.** Every job is now in the game's own interface: `NativeUi` is gone (all jobs are), and with it the mod's own objective plate, status text, number-key choices, key prompt and pay slip.

Shared by all:

- `ShiftFigures`: the card laid out as the cart lays it out, each line written only when a figure changes;
- `JobPanel`: the game's panel over a place, with one or two buttons by locale key, or the job's own four;
- `ShiftClock`: the offer, texts, rate, pay slip and log of "Let time pass" for the jobs other than the cart;
- `ShiftRates`: the 85% and the game minutes a unit, which is four game hours over the units of the shift (the cart's twenty minutes is the same sum).

`WorldPanel` gained `Follow`/`FollowLift` so that the box stays on a person who walks.

- **Flyers.** Canvasser uniform; the panel follows the passer-by in reach with "Offer a flyer", and stands over the player with "Let time pass" when nobody is in reach (10 game minutes a flyer, 85%, no busy bonus, no bundle bonus).
- **Cleaner.** Cleaner uniform; "Go in" over the door, "Mop it up" and "Let time pass" over each spill in reach (30 game minutes a spill, 85%).
- **Shelf stocker.** No uniform (the game has no shop assistant's outfit); "Pick up" and "Let time pass" over the pile, "Go in" over the door. "Shelf to stock" appears over the chosen shelf as soon as the box is carried in; "Put on the shelf" is enabled only within 1.3 metres, checked again when pressed (40 game minutes a box, 85%). Like the other job panels, it stands over the player while its place is behind the camera. Stepping outside clears the shelf marker before the street guider is used.
- **Busker.** `JobStand` on the pitch (the guitar pose when the clip bundle has the strumming clip).
  The three songs, "Strum" and "More" (which has "Step away" and "Let time pass") are the panel's buttons.
  30 game minutes a song at 85% of `BuskerJob.AverageTip`, the song's own tip formula for the crowd and hour now, the middle song and `AveragePlay` of the strums in time; no uniform.
- **Courier.** Card and panel buttons (collect, load, take, hand over, leave).
  No clock: the car would have to be moved.
- **Ride-hail.** Card only; nothing in it is done on foot with a key.
  No clock.
- **Dispatch desk and shift in charge.** Card and panel buttons for the choices (a button for each van and "Take one back"; "Check on them", what to say, what to do about an incident).
  No clock: every call is a decision.
  The dispatch desk's board is the game's list window (`DispatchList`).

`JobHud` is deleted: its status, countdown, choices, target diamond, pay slip, plate, marker and prompt were moved to `ShiftCard`, `WorldPanel` and `GuideMark`, and the director's drawing of them is gone.
`JobKeys` is unchanged: every member still has a caller outside the jobs.

Not checked in the game (nothing here has been run): any of the above.

In particular where each panel stands and how it reads, that the box follows a walking passer-by, that the canvasser and cleaner outfits fit, that the clock runs in a shop and on the open street without the player being moved, that the busker's idle pose and the stand sit well with the game's guitar performance, and the amounts the clock pays.

## A shift across a save (`JobSave`, `PaySlip`)

The game saves by itself every few minutes and at midnight (`GameManager.CheckAutoSave`), so a save in the middle of a shift is ordinary.
What a shift had paid slip by slip was always in the save's money.

What was lost on loading such a save was everything still to be settled at the end: the work done by hand since the last slip (most gigs pay it in one slip when they end), tips and bonuses its events had earned, the units of a clock run that was still going, and for an employer's shift the whole basic wage and the shift on the review's count.

**The rule: every job is settled, none is resumed.** A shift cannot be taken up where it was: all nine kinds find their place, people and props on the street when they begin (a pitch, a client's shop, a pile of boxes, passengers, a round's doors), and none has a state that is only counts and a place.
So:

- **At a save** (`JobDirector.OnSaveGame` -> `LiveLine`) one line goes under `reallife:job_live`: the job, the day and minute, what the shift has been paid (`job.Shift`), and what it is owed now (`Job.Unpaid()`), with the employer, the hourly wage as it is being paid, the late flag and the clock-on time for a rota shift.
  Nothing of the shift changes and it goes on.

  With no shift on, a test button's shift or a test rota shift, the line is written empty, so one is never left over from an earlier save.

- **What is owed** is the job's own word.
  `Job.Owed(out slip)` is the slip its settlement would pay if the shift stopped now through nobody's fault; each kind's `Settle` / `Pay` is now `if (!Owed(out slip)) return; _paid = true; Ctx.Pay(...)`, so the save's figure and the live pay are one piece of code.
  A round or a delivery stopped early has not beaten the clock or shelved the whole delivery, so those bonuses are not in it (`CourierJob`, `StockerJob`).

  `Unpaid()` adds the events' money to that slip, bounds it at nothing (`PaySlip.Bounded`, as `Director.Pay` bounds a slip), and adds what a clock run that is still going has done (`ShiftClock.Owed`; the drinks cart's own clock in `VendorJob.ClockOwed`).
  Ride-hail pays each fare as it ends and owes nothing; a fare in progress pays nothing, as on any other way out.

- **At a load** (`JobDirector.Slow`, once the jobs' state is loaded -> `SettleSaved`) the line is emptied first and then settled (`JobSave.Settle`), as a shift that ended early through nobody's fault: the owed slip is paid through `PayOut` (the game's transaction under the job's name, the energy, the skill, `JobState.Worked` for the level and the day's amount: exactly what `Pay` does).

  A rota shift gets `CareerDirector.SettleSaved` (the basic wage for the hours from clock-on to the save by `CareerCatalogue.PaidHours`, the rule `ShiftEnded` uses, and `CareerState.ShiftDone` with no complaint, only if work was done and the player still works there).
  A gig counts as a shift worked (`JobState.ShiftCut`).
  One notification says so (`reallife_job_saved_paid`, `_nothing`, `_rota`).

What that rules out:

| | how |
|---|---|
| lost earnings | the owed slip and the wage are paid on loading |
| double pay | only `Owed` is ever paid; `Paid` is carried for the review's totals and the log. The line is emptied before the money moves, and every later save writes it afresh |
| a free second shift | the units count towards the day's amount, and the rota day was handled when it was clocked on to (`CareerState.ClockOn`), so it is neither missed nor open again |
| a strike or a mark for saving | none: it is an interruption, as going indoors or to hospital is |
| a strike taken off for saving | none either: `ShiftCut` leaves the strikes alone (a shift that ends on the street by an interruption does take one off: that was left as it is) |
| a shift with no work done | pays nothing and counts as nothing (no wage, no shift, no complaint) |
| a line that does not add up | refused whole (`JobSave.Read`: a missing field, a figure below zero, not a number), logged, dropped |

Nothing of a job is left in a save or stuck after a load, and that was so before:

- the uniform is drawn over the player's own clothes and never saved (`JobUniform`);
- the cleaner's dirt is taken off the client's registration for the moment of the save, and its mop out of the hands (`CleanerJob.OnSaveGame`, `JobDirt.Suspend`);
- a sealed box in the hands or in a car is taken away after the load (`CourierJob.ClearLeftovers`: the courier's parcel and the stocker's box are the same item);
- a hired van is in its own line and is seen back (`JobRental`);
- the hold on the player, the marker, the shift card, the panels and the people are objects of the session that a load does not bring back.

"Prepare this save for removing the mod" and "Reset the mod's data" end the shift first (`JobDirector.Quit`: paid on its own terms) and then remove the key with the feature's others (`Features`: `.Data(..., "reallife:job_live")`).
Without the mod the key is ignored by the game.

**Home routines and leisure activities** carry nothing across a save and leave nothing behind: nothing of them is in modData while they run, they put no item in the player's hands, and their hold, clip, props and cards are objects of the session.

After a load they are simply over, and what each gives when it ends is not given: cooking has taken its ingredients from the fridges when it began and gives no meal (`CookActivity.Prepare`, as "stopped cooking" does).
Coffee, tidying and exercise give nothing for the minutes spent (their effect is worked out in `End`).
The waiting room does not open the doctor's visit.

A chess game's stake of 10 is not returned (`ChessGame`: only a game that never got going refunds it).
Fishing has paid for its bait and the fish in the bucket are not sold (`Fishing`: sold when it ends).
A drink at the bar is paid as it is bought and nothing more is lost.

The dance floor, sightseeing and the jog were looked at only for what they keep (nothing in the save) and what they take at the start (nothing).

Checked offline (`CatalogueCheck/SavedShiftCheck.cs`): 920 shifts (every gig, every rank of every employer, at random moments, some with nothing done, some sunk by a penalty) are written, read back field for field and settled.
The work, the hours and the wage are compared with the live rules written out a second time.
What had been paid before the save does not change what the load pays.

`PaidHours` is compared hour by hour with the formula `ShiftEnded` had.
A bounded slip plus the clock's pays what the two pay apart; fifteen bad lines are refused.
The key is registered and the three sentences have their blanks.

Log lines: `Jobs check (saved shift): '<job>' is on at the save: paid so far N for U unit(s), owed M for V ...: PASS` (diagnostics on), `Jobs check (saved shift): settled '<job>' saved on day D: paid M for V unit(s) owed ...: PASS` (always), `Careers: a shift at <employer> that was on at a save was settled: ...`.

Not checked in the game: any of it; in particular that the game's save event reaches the hook during each kind of shift (the cleaner's own hook shows it does for one), and what the figures are at a real midnight autosave.

## The hired van

A driving job taken with no car of the player's own within 60 m is given a hired van (`JobRental`, 125 once a day).
It is the game's own job van made exactly as its delivery job makes one (`DeliveryVehicleInstance`, `VehicleHelper.CreateAndSpawnVehicle`), with no mission.

The van's prefab carries the game's `DeliveryJobVehicle`, which takes the van off the save's vehicle list when the object goes and, once an hour, destroys any van that has no mission, is not driven and is not in view.

A save made in the middle of a hire therefore holds only a vehicle the base game made and removes by itself; with the mod loaded the van is also remembered by id in modData and taken back once the player is out of it.
During a hire that hourly check is unhooked for the one van.
If the prefab ever comes without the component the hire is refused.

For the rest of the mod: `JobDirector.ActiveJobId`, `LastJobId` and `EarnedToday` are safe to read at any time (a life event can ask whether the player drove a taxi today).
A good shift adds `ModState.MoodUp`, a failed one `MoodDown`, a sacking `MoodDownBig`.

## Job clips

Job code never names one of the game's animations.
It asks `IJobAnimations.Play(character, JobClip.X)`, which returns how long the beat lasts, or 0 if that character cannot do it.

`ClipJobAnimations` plays the mod's own clip for it when the bundle has one (its table lists the clips that will do, best first: `job_*` when they arrive, and until then the living city's `leaflet_offer`, `wave_hello` and `serve` where they fit) and otherwise hands over to `GameJobAnimations`, the nearest gesture the game's characters have.

A gesture (hail, hand over, receive, refuse, thank, knock, annoyed) is played on the masked layer with `BodyMask.Upper`: the legs stay the game's, so whoever makes it may be walking.
What moves the whole body by its nature (into and out of a car, to a boot, a shelf, a counter) is played whole, and the player is held still for it (`JobBody.Hold`).

`Moment(JobClip, "release" / "grip")` says when in the beat a hand lets go or closes; a job asks for that instant with `Ctx.At(time)` and gets it in `Job.Moment`, to the frame (the tick is five a second): that is when a flyer, a parcel or a cup changes hands.
`job_hail` is a loop and goes on until `Stop(person)`.
Also asked for by JobClip: `HandOverLeft` (`job_hand_over_l`), `ReceiveBox` (`job_receive_box`), `CarEnter`, `CarExit`, `LoadBoot`, `Stock` (`stock_shelf`), `Serve` (`serve_counter`).

On the player, by name through `JobBody.Clip`: `mop_floor` (else the game's `Cleaning` with its `Mop`), `guitar_strum` (else the game's own guitar performance from `StreetPerformerDataHelper`, with its music).
Whoever takes a parcel carries it in `carry_box_hold` (arms only, over the game's own walk and idle); only a bundle without it falls back to the whole-body `carry_box_walk`, kept in step with `MatchSpeed`.

`job_flyer_hold` is on the player's left arm for the whole flyer shift (`JobBody.Over`, `BodyMask.LeftArm`), walking included, and is taken up again after each offer.
`CarEnterLeft` / `CarExitLeft` are the `_l` clips for a door on the car's left: `JobSpots.AtDoor` gives the place the four are made for (square to the car, 0.75 m out from the middle of the rear seat, which is an estimate: the game's vehicles say nothing of their seats) and the side.

The clip is played only within 0.35 m of that place.
The clips first asked for, as designed (Humanoid, in place, starting and ending in the standing idle):

| JobClip | played today by | wanted clip | length | hand | prop socket |
|---|---|---|---|---|---|
| `Hail` | `DJEncouraging` | `job_hail`: right arm up and out towards the road, hand open, two small waves, lean forward on the toes | 1.6 s, loops cleanly | right | none |
| `HandOver` | `PressAction` | `job_hand_over`: holds an object out at chest height to someone 0.8 m in front, releases, arm returns | 1.2 s, release at 0.55 s | right (a mirrored `job_hand_over_l` for the flyer hand) | right hand: the object leaves the hand at the release |
| `Receive` | `PressAction` | `job_receive`: reaches forward, closes the hand on an object at chest height, draws it in to the body, small nod | 1.3 s, grip at 0.5 s | right; a two-handed `job_receive_box` for a parcel 0.4 m wide | right hand (or both, 0.4 m apart) from the grip on |
| `Refuse` | `IDontThinkSo` | `job_refuse`: palm up and out at shoulder height, head turns away, a half step back | 1.2 s | right | none |
| `Thank` | `DJEncouraging` | `job_thank`: a nod and a short open-hand wave at head height | 1.2 s | right | none |
| `Knock` | `PressAction` | `job_knock`: three raps with the knuckles at head height, 0.5 m in front, then a step back | 1.6 s | right | none |
| `Annoyed` | `IDontThinkSo` | `job_annoyed`: both hands thrown up, head shake, turns away | 1.4 s | both | none |

**All of these are now in `Bundles/reallife_anim`** under exactly those ids, with `job_car_enter` / `job_car_exit` (and `_l` for a door on the car's left side), `job_load_boot`, `job_flyer_hold`, `carry_box_hold`, and for jobs to come `mop_floor`, `stock_shelf`, `serve_counter`, `till_tap`, `carry_box_walk`, `guitar_strum`.
The table in [`CityAssets/README.md`](../../CityAssets/README.md) ("Jobs") has each one's length, the moment of its grip or release (`CityClip.Mark`), the hand, the prop it holds and where the character must stand.

They are made for the upper body and are meant to be played with `ActivityAnimator.PlayOver(clip, BodyMask.Upper)` (the legs stay the game's own) and `job_flyer_hold` with `BodyMask.LeftArm` over walking; `ClipLibrary.Has(id)` says whether a clip is there, so an `IJobAnimations` that plays them can fall back to `GameJobAnimations` beat by beat.
`ClipJobAnimations` and the jobs use them so.

## Job props

The parcel in the player's arms is the game's own sealed cardboard box, carried with the game's own two-handed carry (`JobCarry`).

Everything else comes from `JobProps.Take(id)`: the modelled prop of that id; failing that the one it is listed against in `JobProps.Alias` (`flyer` -> `leaflet`, `flyer_stack` -> `leaflet_stack`, `parcel_s/m/l` and `shelf_box` -> `parcel`, `mop_bucket` -> `bucket`, `vendor_cart` -> `coffee_cart`); failing that a plain shape for the few that pass as one (parcels, `taxi_sign`, `guitar_case`, `mop_bucket`, the cleaner's `floor_patch`, which is only ever a shape).
No cart and no help-wanted board is made of plain shapes: without the prop there is none.

A prop goes in a hand only through `HeldProp`, at the grip of a clip made for it and with the prop that clip names (`ClipLibrary` carries both); the last resort for the flyer stack alone, when no clip can be had at all, is `HandProp`'s plain shapes fitted from the forearm.
