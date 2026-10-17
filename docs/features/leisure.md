# Leisure

## In a sentence

Things to do for fun at real places in the city (street chess, fishing, dancing, drinks, sightseeing, the film programme), the mod's clothes as something you buy, a little night life, a sound bed and photo mode.

## Where the code is

- Folder: [`Scripts/Leisure`](../../Assets/Mods/RealLifeOverhaul/Scripts/Leisure/) (namespace `RealLifeOverhaul.Leisure`). The files are listed in the code block below.
- Read first: `LeisureDirector.cs` (the one activity that is on), `LeisureActivity.cs` (the base every activity derives from) and `LeisureApi.cs` (the door for the rest of the mod).

## How it talks to other features

- Other features start an activity with `Leisure.Activities.Start(kind, place, companion)` and hear the end on `Leisure.Activities.Finished`; see [For the rest of the mod](#for-the-rest-of-the-mod).
- Drinking and then driving is reported to the police (`PoliceAttention.Report(Offence.DrunkDriving)`); see [Drink at the wheel](#drink-at-the-wheel).
- The mod's garments come from `Character/CharacterCatalogue`; see [Character](character.md).
- Family and Travel have bridges for leisure, but nobody fills them in; see [Every bridge, and who answers it](../reference/wiring.md#every-bridge-and-who-answers-it).

Everything under `Scripts/Leisure` (namespace `RealLifeOverhaul.Leisure`): things the player does for fun at real places in the game's city, the mod's clothes as something bought, a little night life, a sound bed and photo mode.
One component, `LeisureDirector`, owns all of it except sound and photo mode, which are two small components it adds.

```
Leisure/
  LeisureDirector   the one activity that is on, what is on offer where the player stands (one look a second),
                    interruptions, the result card, drink at the wheel; parts: .Offer (what is on offer),
                    .Cinema (the film programme), .Tests (the test buttons)
  LeisureActivity   the base every activity derives from; Sweep, the one timing interaction they share
  LeisureApi        Activities.Start / Stop / Finished for the rest of the mod; LeisureMoney
  LeisureState      this save's records, landmarks, catch, clothes bought, outfits, drinks: plain strings in modData
  LeisureScreen     the game's panels, job panel, marker and notifications for all of it
  LeisureBody       the player's body at play: held, a clip with its tool (HeldTool), an arm loop, one of the game's dances
  LeisureProps      pooled props: from PropLibrary by id, else a stand-in of plain shapes (see the wish-list)
  LeisureVenues     what the game's city has: the items of the building the player is in, landmarks, the water's edge
  Activities/       ChessGame (+ ChessCorner, the chess player on the pavement), Fishing, NightOut (DanceFloor, BarDrinks, Sightseeing)
  Shops/            ClothesShop (prices, what is owned, the rule), ClothesRack (the prop in a store), ClothesScreen (the game's clothes screen: the shop at the rack, the wardrobe at home)
  Night/            NightLife (two people outside an open venue in the evening), TipsyDriving
  Sound/            LeisureSound (the four ambient loops, the cue tones, the radio's name on the HUD)
  Photo/            PhotoMode
```

## What the game's city has, and what is built on it

Found by reading the game's code (`BuildingRegistration.businessTypeName`, the item names of an interior, the scene's MonoBehaviours); nothing is guessed at run time beyond what is listed under "Unverified".

| the game has | leisure uses it for |
|---|---|
| nightclubs (`ba:businesstype_nightclub`) with dance floor items, cocktail bar items (`ba:itemname_*cocktailbar*`) and nine player dances (`BaseHuman.SetDance`) | the dance floor (a rhythm game on the game's own dances) and drinks at the bar |
| clothing stores (`ba:businesstype_clothingstore`) with `ba:itemname_clothingrack*`; no way to buy clothes to wear (clothing is retail stock; the wardrobe changes outfits for free) | the Real Life Overhaul rack: the game's clothes screen as a shop of the mod's garments, tried on and bought |
| the clothes screen of wardrobe and dresser furniture in homes (`ChangeCharacterClothesUI`) | the mod's clothes the player owns, in its grids after the game's pieces |
| cinemas and theatres where the game itself seats the player and passes the time ("watch show", with its own mood) | only what was missing: a programme (what is on today) and a verdict on what was watched |
| the pier's big wheel and rides, the casino boat's pier, golf course, tennis courts, swimming pool, bridges, twenty named subway stations, the hospital | landmarks to see and tick off; the waterside ones also say where a line can be cast |
| pavements (through `CitySpots`) and the mod's own `chess_table` prop and `chess_sit` clip | the chess player on the pavement |
| a car radio with four stations (`RadioPlayer`), footsteps by surface (`StepTrigger`), rain loops (`RainAudio`) | left alone; the radio's station and song are named on the HUD |

**Not in the game, so not built** (they were left out because the city has no such place): basketball hoops, bars as a business of their own (hence no pool or darts: a nightclub's bar is the only bar), bowling.
The game's own arcade machines, golf, tennis, gym machines, swimming, carnival rides and casino are already playable by the player through the game and are not touched.

## The activities

Each is started by the prompt that appears where it can be played (the interaction key), by its test button, or by `Leisure.Activities.Start(kind, place, companion)`.
One at a time, never during a job, a police stop, a mission screen or in a vehicle.

Every way out (played to the end, walked away, a load, hospital, time skipped, knocked down, the option switched off, an error) goes through `LeisureDirector.EndActivity`: the body is released first (`LeisureBody.Release`: clip disposed, hands emptied, dance stopped, the hold let go), then the activity's `End` gives back whatever else it put out.
A best score is kept per save for chess, fishing, dancing and sightseeing.

| activity | where | how it plays | costs | pays / mood | record |
|---|---|---|---|---|---|
| Street chess | at the mod's chess table, where a chess player sits by day in the dry (put out of view 30 to 70 m off, one at a time, never while the living city has its own chess table out) | walk to the free stool (marked), sit (`chess_sit`); five positions, three moves each on a 22 s clock: 2 points for the move, 1 for a sound one, 0 for a blunder; the hand makes the move chosen (`chess_move_player`, from and back to the sitting loop); he answers each in a line; an onlooker to the side of the table, where the pool can spare one, cheers once (`cheer_once`) on a win | stake 10 | 7+ of 10 wins the stake (MoodUp), 5 or 6 draws it (Social), under 5 loses it | points |
| Fishing | standing still at the water's edge within 320 m of a waterside landmark, facing open water | the rod in the hand at the grip the four rod clips share: `fish_wait` between casts and while the float is out, `cast_rod` for the cast (the float and line leave the tip at its `release` mark, 0.9 s), `reel_in` while a fish is on, `fish_land` when it comes out; they cross-fade with the rod in the hand and turn it themselves. Five casts: stop the cursor in the zone to cast (a better cast: shorter wait, better fish), wait for the bite, strike inside the fish's window (a strike at nothing scares it; three and the cast is over), then three timed pulls (the zone narrower the heavier the fish; two misses and it is off). Key 3 packs up early | bait 6 | the catch is sold by weight (0 to about 110 a fish); MoodUp for a catch, MoodUpBig for a new kind or 3 kg | heaviest fish; kinds caught (8) |
| Dancing | in a nightclub, on or beside a dance floor item | the player dances one of the game's nine dances; a cursor crosses the meter once a beat (0.86 s), 24 beats: the key in the zone scores 1, in the bright middle 3; every sixth in a row changes the dance | free (the club's own entrance fee is the game's) | MoodUp from 12 on the beat, MoodUpBig from 18 | points + 2 x longest run |
| Drinks | in a nightclub, within 2.8 m of a cocktail bar item | beer 8, cocktail 14, soft drink 4; the glass is stood on the bar and the player steps up to it and drinks with `drink_bar` (the glass on the bar before the clip's `grip` mark and after its `release`, in the right hand between) where the bar measures as one that clip fits; anywhere else drunk where they stand (`sip` with a glass). See  [The bar](#the-activities) | the price | MoodUp; from two drinks in the blood the Tipsy mood (+3, 3 h) instead, and the car wanders | none |
| Sightseeing | within a landmark's radius (35 to 70 m), on foot | the player stops, turns their back to the landmark (SightseeingRules: away along the line from its point, unless standing within 1 m of it) and takes a selfie with it behind them (`selfie` with its phone; `take_photo` where they stand if the bundle has no selfie); the place is ticked off | free | MoodUp for a first sight, MoodUpBig for the last one on the list | landmarks seen |
| Film programme | inside a cinema or theatre | nothing to play: the house names today's film on the way in; a show watched (the game's own seat) for 45 game minutes or more gets a verdict | the game's own | MoodUpBig, MoodUp or nothing, by the film | shows watched |

Why chess is a reading game and not a board: a whole game of chess with an engine and a board interface would be a mod of its own, and is not what anybody stops on the pavement for.
Five positions with three candidate moves keep what is chess about it (seeing the fork, not moving the pinned piece, swapping when ahead) and take two minutes.

The clock is only there so that it is not a quiz.
The eight positions are dealt in a different order each game.

Drink: each drink is 0.8 (beer) or 1.2 (cocktail) and wears off at one per ninety game minutes (`LeisureState.DrinksAt`).
The bar stops serving alcohol at four.
From two, `LeisureDirector.Tipsy` is true and `TipsyDriving` (switched on by the director only while the player is at the wheel and tipsy) adds a slow uneven turning pull to the driven car, stronger the more was drunk: one `AddTorque` per physics step, nothing changed on the car.

**It is an offence**: `LeisureDirector.Wander` calls `PoliceAttention.Report(Offence.DrunkDriving)` while the player drives tipsy, and any stop finds it (see [Wiring](../reference/wiring.md): fine 900, 6 points, a court case the second time).

The bar (`BarDrinks.Serve`): `drink_bar` is made for a bar top 1.05 m up with the glass on it 0.35 m ahead of the drinker and 0.06 m to their right.

The game's bar is an item of unknown shape, so it is measured when a drink is ordered, on the bar item's own colliders.
A ray from the player at 1.02 m finds the face they would stand at (it must be square to them within 60 degrees).

A ray down where the glass would stand must find a level top 1.05 m up (5 cm either way), and the place to stand (toes 0.25 m from the edge) must be on the floor's navigation mesh.

If all of that holds the glass is stood on the bar (`drink_glass`) and the place is marked.
Once the player is within half a metre of it (nine seconds are allowed) they are held, brought the last step (`LeisureBody.Settle`), turned square to the bar, and the clip is played with empty hands.

`LeisureBody.PickUp` puts the glass in the hand at the clip's grip at its `grip` mark, and `SetDown` stands it back on the bar at `release`: two comparisons a frame while a drink is being drunk, nothing otherwise.

If any of it fails, or the player does not step up, the drink is drunk where they stand with `sip`, as before.

## For the rest of the mod

```csharp
bool started = Leisure.Activities.Start(Leisure.LeisureKind.Chess, null, friend);       // or with: out string refusalKey
Leisure.Activities.Finished += (kind, score, played) => { ... };                         // once per activity
Leisure.LeisureKind now = Leisure.Activities.Current;  bool tipsy = Leisure.LeisureDirector.Tipsy;
```

`Start` plays the activity where the player stands if it can be played there (the same test the test buttons make) and answers false with a locale key otherwise; it never throws.
`place`, when given, must be within 6 m of the player: bring them there first.

`companion` is somebody who came along: the player gets the Social mood at the end; the companion is never moved, animated or released by leisure (whoever brought them keeps them).
Chess needs the chess player to be out; `Start` does not conjure a table.

`Leisure.Shopping.Buy(itemId)` (also with `out string refusalKey` and `free: true` for a gift), `Price(itemId)` and `Owns(itemId)` sell the one thing there is to sell today: the mod's garments and shoes, by their catalogue id.
Groceries and gifts were to be added only when another feature needs them; none does yet, and there is no second inventory.

## Clothes

The mod's garments and shoes (`Character/CharacterCatalogue`: tees, jeans, bomber, summer dress, three shoes, in their colourways) are bought at the **Real Life Overhaul rack** in any clothing store.

The rack is a prop of the mod's own, stood on a tested piece of shop floor while the player is inside: clear of the walkable area's edges by 1.1 m, nothing of the shop's inside its footprint, walkable to from the door, with a carving obstacle on the prop itself, gone with it.
In a store with no such floor it is the nearest of the store's own racks.

The game has no shoe shop: shoes are on the same rack.

The rack's key opens the **game's own clothes screen** (`ChangeCharacterClothesUI`, what a wardrobe's "Change clothes" opens: the player's preview, the categories, a grid of pieces, a row of colours and Save) as a shop (`Leisure/Shops/ClothesScreen`): the Torso, Legs and Feet grids hold the mod's garments in every colour, each tile with its price or "Owned" (`Character/GridLooks`, the tiles the character creator has too), and the game's own pieces of those three kinds are put away.

A click tries a garment on the preview; Save reads "Buy (price)" for what is tried and not owned, greyed while the money is not there.
Buying pays (`LeisureMoney`) and keeps it on; closing the screen without buying takes off what was tried, back to what they came in wearing.
A Save is told from a closing by the save's list of the player's pieces: the game's SaveClothes puts a new one in its place.

The price goes by the garment:

| garment | tier | price |
|---|---|---|
| T-shirt | Everyday | 35 |
| Jeans | Everyday | 60 |
| Court shoes | Everyday | 70 |
| Runners | Smart | 95 |
| Summer dress | Smart | 120 |
| Bomber jacket | Smart | 140 |
| Derby shoes | Premium | 220 |
| anything new without a line in `ClothesShop.Price` | by its tier | 50 / 110 / 200 |

**"Clothes must be bought"** is kept per save (`LeisureState.MustBuy`, with the ids owned): on for a game begun with this version (which owns whatever was picked during character creation: the creator offers the Everyday tier only, `CharacterCatalogue.Starter`, as entries of its own grids; see [`CharacterAssets/README.md`](../../CharacterAssets/README.md), "Whose picks"), off for a save that was already going, so nobody loses what they wear.
The tier of a garment is the catalogue's (`CharacterCatalogue.TierOf`); `ClothesShop.Tier` only passes it on.

The options toggle speaks for the save that is loaded.
While it is on, anything picked on the options page that is not owned is taken off again with a toast, and the dropdowns name unowned entries "(not bought yet)": the game builds a dropdown from the array it was given at start, so entries cannot be removed from it, only renamed.

At home the mod asks nothing of a **wardrobe or dresser**: its own "Change clothes" opens the same screen, and what the player owns of the mod's is in its Torso, Legs and Feet grids after the game's pieces, in the colours bought.
Save keeps what was picked; closing without saving puts back what was worn (the game asks first, as it does for its own pieces).

The screen dresses employees in uniforms too: then nothing of the mod's is in it.
(Until 0.3.0 a key at the furniture opened a list of the mod's own with three saved outfits; the game's panel was often showing another place there, so the key did nothing.
The outfits saved then are still in the save; the game's screen has none.)

## Night life

Between 20:00 and 03:00, in the dry, two people of the living city's pool stand talking outside the nearest open nightclub, cinema, theatre or casino door within 75 m (`talk_a` / `talk_b`, both or neither), put there and taken away only out of view, for five minutes or until the player is 120 m off.

Never while the living city has its own door scene out (`door_queue`, `doorman`) or fewer than four of its eight people free.

Deliberately not done, and why:

- **brighter signs**: each sign has its own material instance that the game colours and switches itself (`BuildingSignController.ToggleLight`), and a property block over that cannot be checked without running the game;
- **more taxis at closing time**: the traffic pool is filled once at start and has no knob for one kind of car, and the hourly group percentages are overwritten by the game;
- **fewer walkers late**: the game sets pedestrian numbers by the hour itself;
- **street musicians**: the game spawns its own performers, and the living city already gives them an audience.

## Sound

Checked first, and left to the game: footsteps already differ by surface, rain has its own loops, the car has a radio, and the mod's sirens are already positional (`TrafficEffects.AddSiren`: 3D, logarithmic roll-off from 4 to 95 m, Doppler 0.5, pitched low), so there was nothing to fix there.

The game has no birds, crickets, crowd or far-traffic clips in its code, so the four loops are made from arithmetic once per session (one per quarter second after the city loads, a few milliseconds each):

- birds (06:00 to 19:00, more before 09:00);
- crickets (21:00 to 05:00);
- a low hum (always out of doors, less at night);
- a murmur that follows how many of the game's pedestrians are within 18 m (counted every two seconds).

Four looping 2D sources and one for cues; volumes are moved on a quarter-second timer, a layer at nothing is stopped, indoors and in the rain they fade out, in a vehicle they drop to 40 %.
All are routed to the game's city mixer group (master and effects volume apply) and stop with the game's pause.
One slider per layer.

## Photo mode

`KeyAction.PhotoMode` (F10: the game binds nothing to it).
The game is paused through its own `GameSpeedController.SetPause` (the frame goes on being drawn) and its pause key is locked out; the Cinemachine brain is switched off and the main camera moved directly; the game's interface is hidden with the game's own `ScreenshotController.ToggleUIVisibility`, the mod puts nothing of its own on screen.
Mouse: look.
W A S D: fly.
X / Z: up / down.
Shift / Ctrl: fast / slow.

Wheel: zoom (field of view 8 to 100).
J / L: roll. , / .: exposure.
K: filter (none, warm, cool, black and white, vivid, faded).
G: depth of field, Y / U: its focus distance.
1: hide the player.
2: hide or show the game's interface (the controls and settings come in a notification).
3: reset.
Enter: save a PNG at the screen's resolution to `<persistentDataPath>/RealLifeOverhaul/Shots`.

Exposure, filters and depth of field are a Volume (priority 1100) that exists only while the mode is on.
Everything is recorded on the way in and put back on every way out (the key, a menu opening, the game unpaused by anything else, the city unloading, an error).
The camera stays within 150 m of the player.

The fixed keys live in `Core/Keys.cs` (`PhotoKeys`) and were chosen from keys the game does nothing with while paused (not Space, Q, E or F).

## On screen: the game's own interface (Leisure)

The mod draws nothing of its own for leisure (`LeisureHud` is gone; everything is `LeisureScreen`, which owns the pieces below).
None of this has been seen in the game.

| screen | what it is now | if it cannot be made |
|---|---|---|
| a place where chess, fishing, a dance, a drink, the rack or a view is on offer | the game's panel over the place (`GameUi.DoorPanel`): the offer as the first line, "Go ahead" on the button | tried again once a second while the game's panel shows another place |
| the objective and the figures of an activity | the headline and lines of a copy of the game's job panel at the top right (`ShiftCard`): the activity's name, what to do, the status | what is asked is sent as a notification each time it changes |
| the prompt ("cast", "strike", "pull in the green", "on the beat") and the choices (the chess moves, the drinks, "pack up") | buttons of the game's panel over the player: the prompt on the first, each choice on the next ones, the game writing each key and running it on that key or a click. A button's words are a locale key filled from the activity's text (`reallife_leisure_button`) | as above |
| the marker | the game's destination marker (`GameUi.GuideMark`) | not shown while a job or an outing has the marker |
| the timing meter (fishing: cast and pull; the dance beat) and the chess clock | two lines of the job panel: what it asks with the zone as percentages, and a gauge of twenty cells in plain letters (`=` good, `#` best, `|` the cursor; for the clock `#` is what is left). The action is the panel's button. Timings, windows and judging are the activities' own and untouched | no gauge without the job panel; the button and the words still work through the notification |
| the rack's shop and the wardrobe | the game's own clothes screen (`ChangeCharacterClothesUI`): the mod's garments as tiles of its grids (copies of its own entries) and colours in its colour row; in the shop its Save reads "Buy (price)" | the screen is left as the game made it (`Clothes: the mod's clothes are left out of the game's clothes screen ...`); the options page still has the clothes |
| what a game or a visit adds up to; the records; the radio's station and song | one of the game's notifications | none needed |

Why the meter is not the game's activity panel.
`PlayerActivityUI` was read: showing an activity pauses the game (`gameSpeed.SetPause(true)`), and its progress bar is written only by `SetUpProgressBar`, which runs when a whole minute of game time has passed (`OnActivityRunning`) or inside `UpdateActivityDisplay`, which also makes its labels and buttons again.

A cursor that crosses in about a second cannot be a progress bar there and could not be moved without remaking the panel every frame.
The job panel's line with a text gauge was used instead, as the fallback allowed.

Cost: per frame only `LeisureScreen.Gauge`, which compares one cell number and writes a line only when the cursor is in another cell (about twenty times a pass, only while a meter is up).
Everything else is told on the tick (five a second) or on a button press, and only when it changed.

Log lines with diagnostics on: `Leisure check (prompt panel)` and `Leisure check (activity card)`, each ending PASS or FAIL. Behaviour that changed: the keys 1 to 3 and the interact key are the panel's buttons now (the game shows the player's own keys).
The browser's up and down are the list's scroll.

Photo mode starts with the game's interface showing and the controls in a notification (the "2" key hides or shows the game's interface, and a picture is always taken with it away).
The mod's own help and settings strips, the dimming of the screen (nothing used it) and the corner strip for the radio are gone.

## Performance

Idle: `LeisureDirector` one timer test a frame and a look round once a second (the offers are a handful of distance tests; the pass over a building's items happens once per building entered, the scene search for landmarks once per city load; the water test, three rays, only while the player stands still near the water).
`LeisureSound` one timer test a frame and four volume comparisons a quarter second.

`PhotoMode` one key test a frame; `TipsyDriving` switched off.
Per frame only while an activity is on: the meter's cursor (one cell number compared), for fishing the float and the line (the rod is turned by its clips; only the stand-in for a bundle without them aims it), and for a drink at the bar two timed moments.
Strings are built when something changes (a key press, a new step), never on a timer.

Each of the three components catches what its `Update` throws, logs once, puts back what it changed and stays off for the session.

## Unverified (the game could not be run)

- **The water's edge** is measured, not known: the game has no water object, layer or height.
  Fishing is offered where the walkable ground ends within 3 m and 5.75 and 7.5 m ahead there is something named water, nothing at all under the sky, or a surface 0.8 m or more below that nobody can walk or drive on, and only within 320 m of a waterside landmark.

  It may be offered at a drop that is not water, or refused at water the rays read as ground.
  The float is shown only where a surface was hit.

- **The fishing clips** (`fish_wait`, `cast_rod`, `reel_in`, `fish_land`) are played whole-body on the held player with `fishing_rod` at their shared grip, each held on its last pose (which is `fish_wait`'s first) until the next: seen only on the clip pipeline's sheets, not in the game.
  `fish_wait` holds the rod 22 degrees to the left of straight ahead, so the tip is not over the float's line; the line is drawn from wherever the tip really is.

  A bundle that lacks any of the four gets the old stand-in (`umbrella_hold` on the right arm and the rod aimed each frame).

- **The bar** is measured, not known (see [The bar](#the-activities)): whether the game's cocktail bar item has colliders that give a level top at 1.05 m has not been seen; if it does not, every drink is the standing `sip`.
- **Landmarks**: found by type in the scene; which of several bridges (the most westerly) and whether every listed subway station exists in the scene is not known.
  A missing one is simply not on the list.
- **The rack's floor test** may find no clear floor in a store whose interior has a large collider round it; the store's own racks are then used and no prop is stood.
- **The film verdict** needs the game's temporary mood for watching a show to be named `...watching_show` (it is in the game's legacy map under that name); if it is called something else, the programme is named but no verdict follows.
- **Depth of field** in photo mode needs the camera's frame settings to allow it; if they do not, G does nothing.
  Hiding the game's interface relies on `ScreenshotController` (the game's own F7 debug toggle).
- **The dance** relies on `BaseHuman.SetDance` holding while the player is under a navigation blocker.
- **Mixer routing**: whether the city mixer group is silenced by the game's pause snapshot is asset data; the layers are also paused directly whenever time stands still.
- The sound of the four synthesised loops has not been heard by anybody.

## Wish-list for the asset pass

All drop in by id with no code change (`LeisureProps.Take` prefers `PropLibrary`; `ClipLibrary.Has` gates each clip), except where a line says what to change.
**Delivered and played:** `cast_rod`, `fish_wait`, `reel_in`, `fish_land`, `drink_bar`, `chess_move_player`, and the props `fishing_rod`, `fishing_float`, `fishing_line`, `drink_glass`, `clothes_rack`.

**Delivered, not played yet:** `throw_ball`, `cocktail_glass`, `beer_bottle` and `shopping_bag`; and `browse_rack` and `try_on_turn`, played at the rack until it opened the game's clothes screen (which pauses the game and shows the preview, not the player).

| clip | length | loop | what it is |
|---|---|---|---|
| `cast_rod` | 1.6 s | once | right arm back over the shoulder and forward, `fishing_rod` in the right hand; mark `release` at 0.9 s (the line goes out) |
| `fish_wait` | 8 s | loop | standing, rod held out low in both hands (right at the grip, left 0.3 m up), a small shift of weight; replaces `umbrella_hold` in `Fishing.Begin` |
| `reel_in` | 1.2 s | loop | rod tip up, right hand winding at the reel (0.40 m up the rod, below it), leaning back a little |
| `fish_land` | 2.0 s | once | rod lifted high, a step back, a look at the catch |
| `dance_loop` | not needed | | the game's nine dances are used |
| `drink_bar` | 3.6 s | once | at a bar 1.05 m high: lifts a glass from 0.35 m ahead, drinks, sets it down; mark `grip` 0.5, `release` 3.0; replaces `sip` in `BarDrinks` |
| `browse_rack` | 4.0 s | loop | upper body: right hand moves along a rail 1.55 m up, 0.5 m ahead, pushing hangers apart |
| `try_on_turn` | 3.0 s | once | a slow quarter turn each way with the arms a little out, looking down at oneself |
| `sit_cinema` | not needed | | the game seats the player itself |
| `chess_move_player` | 2.4 s | once | seated as `chess_sit`: reaches to the board 0.45 m ahead, moves a piece, sits back (played when a move is chosen) |
| `throw_ball` | 1.4 s | once | only if a basketball court is ever added to the city: a free throw, release at 0.8 s |

| prop | size (m) | pivot | notes |
|---|---|---|---|
| `fishing_rod` | 0.07 x 0.10 x 2.10 | the butt of the handle; **the rod runs along +Z**, reel under it at z 0.40 | stand-in exists (three round lengths and a reel) |
| `fishing_float` | 0.045 x 0.11 x 0.045 | the waterline (red above, white below) | stand-in exists |
| `fishing_line` | unit length along +Z | one end | scaled along Z to the span each frame; stand-in exists (a 4 mm thread) |
| `drink_glass` | 0.075 x 0.12 x 0.075 | ground centre | held at the `coffee_cup` grip; stand-in exists. Also wanted: `beer_bottle` (0.065 x 0.23), `cocktail_glass` (0.11 x 0.16) |
| `clothes_rack` | 1.44 x 1.76 x 0.56 | ground centre; rail along X at 1.55 m, **browsed from +Z**; a small sign on top | stand-in exists (rail, two feet, seven hanging blocks of colour, a green sign) |
| `shopping_bag` | 0.32 x 0.38 x 0.12 | top centre of the handles | not used yet: for carrying out what was bought |
| `basketball`, `pool_cue`, `dartboard` | | | only if the city ever gets a court or a bar |
| `radio` | | | not needed: the car radio is the game's |

| sound | what it should be (a loop with no audible join, unless said) |
|---|---|
| `RealLifeOverhaul Ambience Birds` | 20 to 30 s of small city birds (sparrows, a pigeon), sparse, no wind |
| `RealLifeOverhaul Ambience Crickets` | 10 s of night insects, even, quiet |
| `RealLifeOverhaul Ambience City` | 20 s of far traffic: a low rumble, no horns, no close cars |
| `RealLifeOverhaul Ambience Crowd` | 20 s of a pavement crowd's murmur, no words that can be made out |
| cue tones (`Cue Tick`, `Good`, `Bad`, `Bite`, `Shutter`) | one-shots: a soft click, a rising two-note, a low two-note, a small bell, a camera shutter |

The loops are made in `LeisureSound.Build`; to use recorded clips, load them by those names there instead of calling the four generator functions.

## Drink at the wheel

**Drink at the wheel** is now an offence of the police's own (`Offence.DrunkDriving`, `CrimeLaw.DrunkDrivingFine` 900): `PoliceAttention.Report(Offence.DrunkDriving)` is called by `LeisureDirector.Wander` while the player drives tipsy (one look every 25 s at most, seen with the odds of driving on the pavement), and at any stop the officer at the window books it with whatever the stop was for (`Breathalyse`).

Licences adds 6 points (`PointCode.DrunkDriving`); the second time inside the three weeks points count for it is a case as well (`JusticeLaw.Reckless`, the roadside fine credited), unless the player was caught after running, which is before the court already.

## Clothes prices after wardrobe batch two

**Clothes prices after wardrobe batch two.** The shop priced by text in the id; the catalogue now has garments the text did not know (chinos, suit trousers, pencil skirt, evening dress, boots, loafers) and a tier of its own.
`ClothesShop.Tier` now reads `CharacterCatalogue.Garments` (shoes: derby Premium, runner, boot and loafer Smart, court Everyday), so the tier on the rack is the tier in the label.

The evening dress was caught by the summer dress's `dress_` price (120); it now goes by its tier (Premium, 200).
The seven first prices are unchanged.
