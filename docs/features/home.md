# Home life and health

## In a sentence

Three needs the game does not have (hygiene, stress and a tidy home), sleep debt, fitness, meals and cooking from what the fridges hold, illness and the doctor, and home routines such as coffee, tidying and exercise.

## Where the code is

- Folder: [`Scripts/Home`](../../Assets/Mods/RealLifeOverhaul/Scripts/Home/), with the roommate in [`Roommates`](../../Assets/Mods/RealLifeOverhaul/Scripts/Home/Roommates/). The files are listed in the code block below.
- Read first: `HomeDirector.cs`, `HomeState.cs` and `HomeBalance.cs` (every number, game-free).

## How it talks to other features

- Social and Careers ask Home through `SocialLinks.Wellbeing` and `CareerLinks.Health` (a sick note), filled in by `Wiring/LifeLinks.cs`.
- Family shortens an illness and adds broken nights (`Wiring/FamilyLinks.cs`); Travel's hotels act on hygiene and stress.
- The roommate's links are `Wiring/RoommateLinks.cs`.
- Household staff ([household.md](household.md)) clean the home through `HomeTidy.Change` and put the cook's portions by as leftovers (`HomeState.PutBy`).

```
Scripts/Home/
  HomeState      per-save state: the three needs, fitness, sleep debt, five days of meals, the illness, a few dates: one line in modData (game-free)
  HomeBalance    every number (rates per game hour, thresholds, fees, odds), the game's food items by kind, the dishes (game-free)
  Cooking        what can be made from a list of what the fridges hold: a plan per dish, what is one unit short (game-free)
  HomeCookList   the cooking list, in the game's list window: the size, the leftovers, the dishes that can be made now, what is one ingredient short
  HomeOffers     "Click to cook" on cookers and "Click to make a coffee" on coffee machines: one entry in the game's list of click actions
  HomeFridgeButton  "Cook a meal" as a button in the game's own panel of a fridge
  HomeDirector   the one component: reads what the player is doing once a second, moves the needs; .Offers (the furniture's clicks, the doctor's panel, the bars), .Routines (starting a routine), .Tests (the test buttons)
  HomeHealth     the rules: needs by activity, mood modifiers, fitness, sleep quality, the body, falling ill and getting over it
  HomeTidy       the flat's tidiness = the game's own floor dirt
  HomePlace      cookers, coffee machines, fridges and the food in them, a clear piece of floor
  HomeActivity   the mod's routines as activities on the game's own panel; the routines: CookActivity (a dish from the cooking list), CoffeeActivity, TidyActivity, ExerciseActivity (floor exercises), WaitingRoomActivity (the doctor's waiting room)
  JogRun         going for a run: a loop of six checkpoints
  WakeRules      when the morning line is due after a sleep, its hours (lying down to the game's own end of the sleep) and its greeting (game-free)
  HomeReport     the "How am I doing" page and the morning's summary, with the city's news held while the player slept (Core/MorningNews)
  HomePhone      the "Wellbeing" contact and its call (WellbeingDialog)
  DoctorVisit    the doctor's consultation (ClinicDialog)
  HomeNeedsBar   hygiene, stress and home as three more of the game's bars, in a tab under its top bar
```

## What the game already does (and the mod leaves alone)

The base game has a whole activity system (`PlayerActivity`): a panel with a start button, a duration slider, a progress bar, fast-forward and a time skip, and behind it

- **sleep** in a bed, a car or a boat (`SleepActivity`: energy by the hour, more in a better bed, a happiness modifier);
- **shower, bath, sink and toilet** (`HygieneActivity`: only a happiness modifier, `ba:happinessmodifier_hygiene`; there is no hygiene need);
- **television, computer games, reading a book, a DJ booth** (`EntertainActivity`, seated on the nearest sofa or chair);
- **resting** on chairs, benches, loungers (`RestActivity`);
- **workouts** on fifteen kinds of machine, at home or in a gym (`WorkoutActivity`: treadmill, bench, squat station, pull-up bar, boxing bag, kettlebells, gym mat, fit ball, trampoline, outdoor calisthenics), with the sport-clothes rule in other people's gyms, a trainer's workout plan, and the gym's entrance fee (there is no membership);
- **swimming, tennis, golf, studying, employer jobs**;
- eating and drinking any item (from the hands, a bag or a fridge: `ItemHelper.TryToConsume`), energy, hunger, happiness with timed modifiers, a "wet" modifier in the rain, a "walk in the park" modifier;
- floor dirt and mopping, for businesses (`BuildingCleanliness`, `MopController`);
- the hospital only as where the player wakes after collapsing (2000), and plastic surgery.

It has **no** hygiene, stress or tidiness need, no illness, no fitness or diet, no cooking, no sleep quality, no doctor, no running, no washing machine and no medicine items.
Working out changes nothing on the body.
The player's animator has: `Sitting`, `Laying`, `Showering`, `Bathing`, `UsingSink`, `ReadingABook`, `ConsumingFoodSitting`, `Cleaning`/`CleaningIdle`, `Squats`, `Crunches`, `CrunchesOnFitBall`, `Boxing`, `Press`, `Running`, `PushUpIdle`, `KettlebellPull`, `Deadlift`, the calisthenics set (loops); `ConsumeFood`, `ConsumeDrink`, `Drink`, `UsingProducer`, `StretchArms`, `SweatRemove`, `SquatAction`, `PushUpJump`, `PressAction`, `BoxingPunch`, `Faint` (one-offs).

## What the mod adds

Nothing above is replaced.
The director looks at the game's own panel once a second (`ReadDoing`) and takes what the player is doing from the activity that is on it; the game minutes that went by (the time skip included) move the needs.

- **Needs** (0 to 100, per game hour): *hygiene* -3.3 awake, -1.2 asleep, -30 on a workout machine, -28 running; a shower or bath restores it in ten minutes, a sink as far as 70.

  *Stress* +0.5 awake, +3 on one of the mod's jobs, +2.5 at an employer job, +1.5 studying, more when ill, starving, short of sleep or in a messy flat; -14 at the television, -12 reading, -10 in the shower, -6 resting, -5 asleep, -8 to -14 exercising.

  *Home*: the tidiness of the player's home, which is **the game's own floor dirt** where the home has floor cells (living there puts a little on a third of the cells, never more than 60 a cell; the game's own mop takes it off; tidying up takes it off everywhere) and a figure in the mod's state where it has none. -1.6 an hour at home awake.

  The three are shown at home, while hygiene is under 35 or stress over 65, and while ill, as three more of the game's own bars (`HomeNeedsBar`, below).
  "Easy needs" halves what wears a need down and every chance of illness.

- **Consequences** are mood only, each one of the mod's happiness modifiers held on while its cause lasts: Unwashed -6 (under 25), Stressed -8 (over 75), Relaxed +4 (under 15), The flat is a mess -5 / A tidy home +4 (at home), Short of sleep -8 (more than 8 h owed), In good shape +4 (fitness 70), Eating well +4 / Living on junk food -5.

  Under hygiene 12, in somebody else's premises, people are said to keep their distance (once in six hours, a small mood loss).

- **Sleep**: quality 0 to 100 from the bed's quality, a car (-25), going to bed between 1 and 6 (-15) or in the daytime (-10), a messy flat, stress, hunger, illness, and the hours (7 or more +10, under 5 -20).
  68 and up is Well rested (+6, 10 h), under 45 Groggy (-5, 6 h).
  Sleep debt: 7.5 h a day is owed, an hour asleep pays off 0.7 to 1.3 h by quality.

  A morning summary once a day after three hours or more of a sleep the game itself ended or a hotel night (`WakeRules`).
  Only those count: the game's sleep in a bed, a car or a boat (`SleepActivity` while Started or Running) and a hotel room's night through its time skip; not the day rolling over, a load, or any other time skip, the mod's or another mod's.
  Its hours run from the last look before the player was seen asleep (once a second) to the minute the game ended the sleep, which `SleepActivity.Finish` marks with its event `ba:gameevent_playerenergyincreased` whether the sleep ran to its time, was stopped or had its time skip cancelled at speed; the sum of the looks lost that last look's minutes on a cancel.
  The game fixes a sleep's end when Start is pressed (the slider runs from 5 minutes to 24 hours and is remembered), so the walk to the bed comes off it: a full slider and a twelve-minute walk is 23.8 hours.
  It says "Good morning" from 04:00 to 11:59 and the same line without it at any other hour.
  With "The night's news in the morning summary" on, the city's news that came while the player slept (the weather, what is on, the city government and the works next door, the morning's papers) comes in it, in one notification or as few more as fit, 240 characters each; see [the night's news](../reference/game-interface.md#the-nights-news-held-for-the-morning-line-coremorningnews-coremorningnewsrules).
  Options > Tests: "Wake up to the morning summary with three pieces of news".

- **Routines on the game's panel** (`HomeActivity`: the player walks there, a short watched sequence, then the game's own time skip indoors for anything longer than 14 minutes; the stop button, running out of energy and a click elsewhere on the way all end it cleanly): *a dish* chosen on the cooking list, from the food in the home's fridges (see [Cooking](#cooking)).

  *coffee* at a coffee machine, by a click on it ("Click to make a coffee"; 10 min, 8 energy inside the game's daily limit for food and drink, three a day).
  *tidy up* (10 to 90 min, 1.1 points a minute).
  *stretching, push-ups, sit-ups* anywhere with 0.9 m of clear floor all round (1.0 m for push-ups; 10 to 60 min).

  The mod's floor clips are centred on the character and lie along the way they face (`pushup`: toes 0.79 m behind, crown 0.90 m ahead; `situp`: head 0.70 m behind, feet 0.60 m ahead), so the player is walked to the middle of that circle of floor and whichever way they face the body stays inside it.

- **Diet**: every meal of the last five days is cooked at home, from the fridge at home, bought and eaten out, or skipped (fewer than two meals in a day).
  What a meal was is told from where it was eaten: the game says only that something was.
- **Fitness** (0 to 100): 9 an hour on a machine, 11 running, 6 for floor exercises and sports, with the second hour of a day worth half the first; -0.7 a day from the third day without exercise.
  It is stamina: an energy spender of +0.006 a game minute at 0 to -0.010 at 100 (the game's own "living" drain is 0.04).

  **The body** follows on waking, 0.02 a night at most and 0.35 in all from the body the player made: strength +0.012 an hour of weights or floor exercise, fatness -0.008 an active hour, +0.004 a day of bad food and no exercise, -0.003 a day of good food.

- **Illness** (one at a time, none for three days after any; `HomeHealth.Catch`):
  - a cold (36 to 60 h): an hour in the rain without an umbrella 30 %, short of sleep 6 % a day, unwashed 5 % a day;
  - the flu (72 to 108 h): 1.5 % a day, doubled by a poor diet and again by sleep debt;
  - food poisoning (16 to 30 h): a burnt meal 12 %, cooking in a filthy flat 8 %;
  - back pain (40 to 64 h): an hour's exercise in a day at fitness under 20, 15 %; a night in the car 12 %;
  - burnout (72 to 120 h): stress at 90 for thirty hours on end.

  The first three are Life's own sickness (`LifeEvents.MakeSick`: its modifier, its drain, its doctor on the phone, the hospital) with a name and a course, and a sickness a life event gives is taken up as a cold.
  A night of six hours or more takes six hours off; stretching eases a bad back; a prescription halves what is left and the extra drain.

- **The doctor**: in a hospital or clinic, within 7 m of the desk of the game's own doctor (a `SpecialEmployeeController`; where the building has none, of the place the player came in), the game's panel over a place (`GameUi.WorldPanel`) offers a consultation (80) on the game's interact key.

  A wait of 20 to 35 minutes on the game's panel comes first, then the conversation: the diagnosis, a prescription (45), a sick note (20: for 48 h a failed job shift is no strike, `HomeHealth.SickNoteActive`), or a check-up with advice for someone well.

- **A run**: from the Wellbeing contact, out of doors on foot: six checkpoints 55 m apart on the pavement in a loop back to the start (34 m and a quarter more gain where the game marks a park), the game's own destination marker (`GameUi.GuideMark`, named "Run: checkpoint 2 of 6") and a ring at the next one.
  Only the minutes spent actually running count.
  Rain: a fifth less, and soaked.
- **The Wellbeing contact**: "How am I doing" (needs, illness, fitness and its trend, diet, sleep, one tip), and from it tidying, the exercises and the run.

Left out on purpose: laundry (the game has no machine), pharmacy items (it has none), a gym membership (the game charges an entrance fee), a timing game on the game's workout machines (the game's own activity runs them).

## Cooking

**Why a routine "offered" in 0.1.0 and 0.1.1 never ran.** A routine is one of the game's activities on the game's own panel.

When its start button is pressed the routine is "moving towards the activity", and the game's panel then listens for any new walk order: `PlayerActivityUI.CancelActivityMovement` hangs on `PlayerController.PlayerChangedNavigation` and, once `_allowCancelMoveTowards` is set, takes a walk order for the player clicking somewhere else: it stops the walk, puts the activity back to "not started", forgets it and closes, without a message.

That flag is set at the end of the frame in which the panel first sees the routine on its way (`CoroutineUtility.RunAfterFrameDelay(..., 1)` is one `WaitForEndOfFrame`), which is the frame of the press or the one after.
The game's own activities give their walk order at the end of the frame of the press, before the flag.

The mod gave its own (`player.SetGoal`, which ends in `SetNewDestination` and `PlayerChangedNavigation.Invoke()`) two frames after the press, so its own first step was taken for a click elsewhere and every routine with a step to walk called itself off: cooking and coffee always (the furniture's standing place is never where the player stands), tidying and the floor exercises whenever the clear floor was not under their feet.

In `Player.log` that is "Home: CookActivity offered." and then nothing.

Now (`HomeActivity.Start` / `SetOff`): the walk order is given at the end of the frame of the start, as the game's are.
If the panel drops the routine all the same, the routine is put on the panel once more and its walk order is given at once, before the panel can have seen it on its way; a second failure is a toast.

Every other way a start can come to nothing says so in one sentence: no way there on foot (the game's own "cannot reach"), the game would not take a walk order, the walk never began (a movement key held down), the furniture in use, the food gone from the fridge meanwhile, the game's panel busy, 20 seconds without the player being free.

**The game's food** (read from its item bundle, `defaultlocalgroup_assets_items`: 791 items; every goods item has `saturation`, 30 unless set, batteries and glass included, and the fridge takes anything with a saturation, so food is a list of ids, `HomeBalance.Foods`; anything else in a fridge is ignored):

| kind | the game's items (`ba:itemname_...`), saturation |
|---|---|
| meat | `groundbeef` 30, `chickenbreast` 30, `rawsausage` 30 |
| vegetables | `carrot` 20, `lettuce` 15, `tomato` 20, `onion` 30, `slicedonions` 30, `russetpotatoes` 30 |
| dairy | `milk` 30, `butter` 30, `cheese` 30 |
| grain | `dough` 30, `bakingmix` 30, `barley` 30 |
| fruit | `apple` 30, `banana` 40, `pear` 30, `grapes` 30 |
| fresh | `freshfood` 100 |
| frozen | `frozenfood` 25 |
| ready meals | `burger` 70, `hotdog` 60, `kabob` 90, `pizza` 100, `frenchfries` 50, `salad` 60, `cheeseplatter` 30 |
| snacks (never cooked) | `croissant` 25, `cupcake` 25, `donut` 25, `cottoncandy` 30, `popcorn` 30, `icecream` 50 |
| drinks (never cooked) | `beer`, `bottleofwine`, `margarita`, `martini`, `whisky` 25; `cupofcoffee`, `cupoftea`, `slushi` 10; `energydrink` 10; `sodacan` 5 |

**The dishes** (`HomeBalance.Dishes`; one batch; a portion fills the saturation of the items used, times the yield, shared between the portions, 100 at most):

| dish | takes | cooker | portions | minutes | yield | burns |
|---|---|---|---|---|---|---|
| Fruit bowl | 2 fruit | no | 1 | 5 | 1.2 | never |
| Salad | 2 vegetables | no | 1 | 10 | 1.4 | never |
| Cheese and fruit plate | 1 dairy, 1 fruit | no | 1 | 5 | 1.3 | never |
| Heated-up meal | 1 ready meal | yes | 1 | 10 | 1.25 | 3 % |
| Frozen meal, cooked | 1 frozen | yes | 1 | 20 | 2.6 | 5 % |
| Fry-up | 1 meat, 1 vegetable | yes | 1 | 20 | 1.5 | 6 % |
| Pancakes | 1 grain, 1 dairy | yes | 1 | 15 | 1.4 | 7 % |
| Home-cooked dinner | 1 fresh | yes | 2 | 45 | 1.5 | 8 % |
| Vegetable soup | 3 vegetables | yes | 2 | 35 | 1.7 | 5 % |
| Home-made pizza | 1 grain, 1 dairy, 1 vegetable | yes | 2 | 40 | 1.8 | 9 % |
| Roast dinner | 1 fresh, 1 meat, 1 vegetable | yes | 3 | 70 | 1.5 | 10 % |
| Stew | 2 meat, 3 vegetables | yes | 3 | 80 | 1.6 | 12 % |

Sizes: normal, double, family size are 1, 2 and 4 batches; the time is 1, 1.25 and 1.6 times (to the nearest 5 minutes), the chance of burning +0, +1 and +3 points, the mess 1, 1.2 and 1.5 times.
Stressed (over 75) or exhausted (energy under 20) adds 8 points to the chance of burning; "easy needs" halves it.

Burnt: the portion eaten fills 0.6 as much, nothing is put by, a mood loss, and food poisoning 12 %.
Leftovers: at most 8 portions, one lot that carries the name of the dish cooked last and the average of what its portions fill (`HomeState.PutBy`; `gro`, `grd`, `grf` in the save's line); reheating one takes 8 minutes at a cooker and never burns.

**What the player does.** At home, a click on a cooker opens the cooking list; so does the button "Cook a meal" in the game's own panel of a fridge (`HomeFridgeButton`).
A cooker is an item whose name has `stove`, `pizzaoven` or `grill` in it: the game offers "Click to cook" (`HomeOffers`).

The cooking list (`HomeCookList`) is the window the game lists its subway stations in, `GameUi.TravelList`, 1,100 wide with entries of 150.
The mod's camera frees the cursor while it is open, and the game's cancel key or the yellow button closes it.

The fridge's own use (the game's panel on a click) is not touched.
The list reads the fridges once (`HomePlace.ReadPantry`) and has: an entry for the size, whose button steps through normal, double and family size; the leftovers.
One entry per dish in the largest size up to the chosen one that the fridges allow, with its items by the game's own names and counts (the most filling of a kind first).

An entry with its button off for a dish of two or more items that lacks one unit, saying which.
And, under the headline, what the fridges hold and a note when there is no fridge, nothing in the fridges, nothing that makes a dish, or no cooker (then only what needs no cooking is listed).
Choosing a dish closes the list.

As soon as the player is free the dish goes on the game's activity panel and is started for them two frames later (the game's panel has to see it waiting once).
They walk to the nearest cooker (to the nearest fridge for what needs no cooking), and the items leave the fridges on arrival (`HomePlace.Take`, all or nothing).

`cook_stir` with `pan` plays at a cooker (the game's `UsingProducer` otherwise), and the game's time skip takes anything over 14 minutes.
At the end one portion is eaten through `GameManager.ChangeHunger` and the rest are put by.

With hunger at 100 nothing is eaten and every portion is put by.
Stopping early wastes the food.

`IHomeBridge.CookTogether` has no implementation in Home (`FamilyBridges.Home` is never set), so Family's "cook together" is its own short scene, as before.

**The fridge's button.** The game's panel for a fridge is the object `FridgeDetailedOverlay` (`DecorativeItemHolderOverlay`): a vertical list with one button per kind of item, `EmptyFridgeButton`, `ChangeClothesButton` (a wardrobe's) and `AddItemButton`.
The mod's button is a copy of `ChangeClothesButton` (the blue one), put under the items once per city.

A component of the mod's on that list is enabled each time the game switches the panel on (it fills the panel first), and shows the button if the panel's `linkedController` is a fridge of the home the player is in.
If the game's window for the list cannot be copied, a click cooks the first dish the fridges allow.

**The needs bars** (`HomeNeedsBar`).
The game's energy, hunger and happiness are three objects of 412 x 56 in `Topbar/Container/Left Bar/PlayerStats`, each the sprite `StatBarBackground` with a filled copy of it over it and a picture of 32; under the same bar hangs the accessories tab (`AccessoriesPanel`, 100 high).

The mod's three are a copy of that tab holding a copy of `PlayerStats`, hung under the game's bars (beside the accessories tab while that is open: its place is measured), with their own colours, pictures and a word each (the game's bars have no word; three new ones without would have to be guessed).
The copy is a child of the game's top bar and goes with it.

A bar is written when its whole number changes, at most once a second; the illness and how long it has to run is a line beside the bars in the game's yellow.

Test buttons: "Test: open the cooking list" and "Test: cook the first dish the fridges allow".
Pressed on the options page, both wait up to 20 seconds for the menus to be closed.
`Player.log` gets "Home: cooking list opened: 1 fridge(s), 1 cooker(s); food by kind: Meat 2 Vegetable 3; size 1; 3 dish(es) possible, 1 one short, 0 wanting a cooker, leftovers 0." and a line when a dish is done.

With "Developer diagnostics" on: `Home check (offer)` when the click actions and the fridge's button are in and for each piece of furniture marked, `(cooking list)` when the list opens (what it holds, the time it took) and closes (with why), `(choice)` for a click on a cooker, a coffee machine or the fridge's button, for the size and for the dish, `(needs bars)` when the bars are built, shown and hidden, `(doctor panel)` when the game's panel comes up or goes, `(run)` per checkpoint.

Not verified in the game (it could not be run): how the list reads at 1,100 wide with entries of three lines.
How the tab of three bars looks under the top bar and beside the accessories tab, and which of the game's pictures the bars got.
That the fridge's button lays out under the items.

That a routine now always sets off (the order of two end-of-frame coroutines started in the same frame is assumed to be the order they were started in, which the game's own activities rely on too; the second attempt does not depend on it).
The look of `cook_stir` at each kind of cooker.
That a fridge's standing place suits a routine.

That raw ingredients (meat, dough, milk) can be bought by the player in every save, since they are wholesale goods.

## Asset wish-list

`cook_stir` (with `pan`), `pushup`, `situp`, `stretch` and `run_checkpoint` are **delivered and played** by id; without the bundle the game's own animation (or the plain shape) is used.
`eat_sit` and `drink_stand` are not asked for yet: eating and drinking are the game's own gestures, played standing once the routine has let go of the player.

| clip id | length | loop | what | played today by |
|---|---|---|---|---|
| `cook_stir` | 3.0 s | loop | standing at a hob 0.45 m ahead, 0.92 m up: left hand on a pan handle, right stirring; holds `pan` (right + other hand) | the game's `UsingProducer` every 4 s |
| `pushup` | 2.0 s | loop | push-ups, hands under the shoulders, body straight; **as delivered the body is centred on the root** (toes 0.79 m behind, crown 0.90 m ahead), which is what the clear floor is measured round | the game's `PushUpIdle` + `PushUpJump` |
| `situp` | 2.4 s | loop | sit-ups, knees bent, hands at the temples | the game's `Crunches` |
| `stretch` | 10 s | loop | standing stretches: arms overhead, side bends, a forward fold; needs 0.8 m either side | `tai_chi`, else the game's `StretchArms` |
| `eat_sit` | 4.0 s | loop | seated at a table 0.75 m high, fork to mouth from a `plate` 0.35 m ahead | the game's `ConsumeFood` standing |
| `drink_stand` | 3.0 s | once | a mug lifted to the mouth and lowered; holds `mug` (right) | the game's `ConsumeDrink` |
| `shower` , `sofa_sit_tv`, `read_sit`, `treadmill_run`, `weights_curl` | | | not needed: the game's own `Showering`, `Sitting`, `ReadingABook`, `Running`, `KettlebellPull` are what its activities play | |

| prop id | size | pivot | for |
|---|---|---|---|
| `pan` | 0.26 across, 0.05 deep, handle 0.18 | centre of the base; handle along +X | `cook_stir` |
| `plate` | 0.26 across | centre of the base | `eat_sit` |
| `mug` | 0.08 x 0.10 | centre of the base; handle at +X | `drink_stand` |
| `run_checkpoint` | 1.6 across, 1.4 high | ground centre | the run's marker (a plain disc and post today) |
