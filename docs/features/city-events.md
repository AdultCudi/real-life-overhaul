# City events

## In a sentence

Fires and the fire service, weather that matters (grip, crime, sight, street life) and street events such as markets, parades and film shoots, one large scene at a time.

## Where the code is

- Folder: [`Scripts/Events`](../../Assets/Mods/RealLifeOverhaul/Scripts/Events/) (namespace `RealLifeOverhaul.Events`).
- Read first: `EventDirector.cs` (the one scene that is on), `Weather.cs` (the factors other features read) and `EventCalendar.cs` (what is on which day).

| File | What it is |
| --- | --- |
| `EventDirector` | the one scene that is on (a fire or a street event), yielding to accidents and crimes; calendar announcements, the City Bulletin contact, 911, the test buttons, one line in modData |
| `FireScene` | one fire from first smoke to the engine leaving (car, bin, shopfront); HoseHold (hands on the nozzle); FireDamage (the player's own business: closure and repair bill) |
| `EventRoad` | the adapter over Scripts/Traffic (public surface only): lanes, the stretch to close, bringing a pooled vehicle; EngineLivery and FireEngine |
| `FireEffects` | flames, smoke, steam, water jet and firelight: seven pooled particle systems copied from the game's smoke; Fireworks (four pooled mesh-particle systems) |
| `Hose` | sixteen pooled segments laid along the ground from the engine to the nozzle, clear of vehicles and street furniture |
| `EventPeople` | the event pool of eight (a second CityPeople): people on a closed carriageway, things worn on a bone, a staff in the hand, walking in and off |
| `EventProps` | the props by id: the modelled one if the bundle has it, else a plain-shape stand-in to the wish-list's size and pivot |
| `FireOutfit` | turnout gear from the game's garments by name |
| `Weather` | the static factors other features read (accidents, crime, sight, street life, grip, traffic speed); WeatherEffects applies tyres, traffic pace, energy, forecast |
| `EventCalendar` | what is on which day: fixed days placed by how far through the year they fall, the rest by a roll that depends on the day and the save |
| `StreetEventScene` | market, farmers' market, parade, protest, fun run, film shoot, fireworks: one class, one closed stretch of road |

## How it talks to other features

- Other features read the weather's factors (`Events.Weather`) and ask `Events.EventDirector.IsActive`; see [Hooks other features need](#hooks-other-features-need-one-line-each).
- The road work goes through `Scripts/Traffic`'s public surface only (`EventRoad`).
- One street scene at a time is decided in [`Wiring.Scenes`](../reference/wiring.md#one-street-scene-at-a-time-wiringscenes).

`Scripts/Events`, namespace `RealLifeOverhaul.Events`, one component (`EventDirector`).
Options group "City events": a toggle and a frequency slider each for fires, weather and street events, and the test buttons.

**One large scene at a time.** The director runs one fire or one street event, and starts none while `TrafficAccidents.IsActive`, `CrimeDirector.IsActive` or `PoliceAttention.IsBusy`.
Others ask `Events.EventDirector.IsActive` (the crime director does; see [Hooks](#hooks-other-features-need-one-line-each) for the accident director).

**Budgets.** People: 8 of the event pool's own (made one every two seconds, and only once a scene or a due event needs them; none exist before that) and at most 2 officers from `People/ActorPool`.
Vehicles: at most 3 (fire engine, patrol car, the burning car).

Particles: 418 for a fire (3 flames x 36, 2 smoke columns x 96, steam 48, water jet 70) in seven systems and one unshadowed point light; 240 for fireworks (4 x 60) in four systems; never both.
Idle cost: one comparison a frame; once a second two flags and the hour; every twenty seconds a roll.

With a scene on: the marker per frame, the scene four times a second (most of it once a second), and while a nozzle is held two two-bone reaches in LateUpdate.
Savegame: one line of plain text in `modData` (`reallife:events`): the calendar's number, the days already announced, and the business a fire closed.

## Fires

A car (a private car in traffic pulls in, stops, its driver gets out and away), a bin (one of the street's, or the mod's own `street_bin` stood on clear pavement), or a shopfront (`CitySpots.Shopfront`: flames at the frontage and at the wall 4.3 m up, if there is wall there).
The lane beside it is closed through a `SceneCordon` of the director's own.

Twenty seconds after the flames, or at once on a 911 call, a fire engine is sent: a pooled truck dressed by `EngineLivery`, driven by `EmergencyDriver` to `SceneCordon.Stand` 12 m short of the fire in its lane.

With the modelled `fire_engine` in the bundle a Delivery Truck wears it: the body is made to that truck (the same 7.43 m, the same axles), so it wears the body with its wheels on the truck's own.
Any other truck, or a van, and every truck without the bundle, is repainted red with a light bar instead.

Three firefighters get out one a second: the first takes the nozzle to 3.6 to 5.5 m from the fire, the hose is laid behind them and the jet arcs on to the fire.

The second works the pump panel (on the modelled engine: 0.50 m off the panel of the fire's side, square to it, which is where `pump_panel` has its hands; the first gets out at the front door, the third at the crew door).
The third stands between the fire and the street.

The fire is knocked down in three stages of eight seconds (flames to 60 %, to 25 % with white smoke and steam, out), damped down for eleven, the hose is rolled up, the crew walk back and board, tape and cones come in, the burnt car is taken away out of view, the road opens and the engine leaves.

With the police option on a patrol car stands behind the engine and its officer works the traffic.
Up to three bystanders walk in to a line 13 to 19 m away and film.
If no engine can come the fire burns out by itself.
Hard limit 250 s.

*The player's own business.* Only through the game's own mechanisms: `BuildingRegistration.TemporarilyClose` (the switch the player has on the business screen) for 2 to 10 hours, reopened by the same switch; and a repair bill of 350 to 1 800 as an ordinary transaction.

Both scale with the seconds from first smoke to water (30 s is best, 120 s worst) and are reduced by the business's security level (up to 40 %), by warning the occupants at the door (15 %) and by calling 911 (10 %).
Stock is never touched.
A save's only business is spared in its first ten days.
A business the player had already closed is not reopened by the mod.

*Not done:* the extinguisher (the game has no such item); a fire engine attending a crashed car (see Hooks: `CrashFire` gives the wreck flames that die down by themselves).

## Weather

The game has rain and nothing else: no snow, fog, wind, temperature or forecast.
`Weather` derives:

| | from | accidents | crime | sight | street life | grip | traffic speed |
|---|---|---|---|---|---|---|---|
| mist | the game's dry spell before and after a shower (its own murk) | x1.3 | | x0.6 | | x0.95 | x0.92 |
| rain | drops falling | x1.5 | x0.85 | x0.85 | x0.6 | x0.82 | x0.88 |
| storm | a shower whose top intensity is 0.8 or more (read by reflection; if unreadable, never) | x1.9 | x0.7 | x0.7 | 0, `TakeCover` | x0.72 | x0.8 |
| heat wave | a summer day, by the day's roll | | x1.3 | | x0.85 | | |
| cold snap | a winter day, by the day's roll | | x0.85 | | x0.8 | | |
| ice | one cold-snap day in three | x1.4 | | | x0.7 | x0.75 | x0.9 |

Seasons are quarters of the game's year (`gameVariables.daysPerYear`, 60 by default), starting in midwinter.

`WeatherEffects` applies grip to the driven car's `LateralFrictionGrip` and `LongitudinalFrictionGrip` (the values `Driving/CarTuning` reads; recorded per wheel and written back exactly).
It narrows each pooled traffic vehicle's `minPossibleSpeed`/`maxPossibleSpeed` (the traffic system has no speed factor; it takes effect as cars are put on the road; originals written back).

It spends 1 to 2 energy an hour out of doors in heat (11 to 19 h) and cold (never below 15; `Weather.Wardrobe`, if anybody sets it, says whether a coat is worn).
It gives the morning forecast (toast and City Bulletin) and a warning when the weather turns.
While the player sleeps its toasts are held for the morning summary (`Core/MorningNews`): the director runs on game time, which the game's sleep pauses, so they would all come in the minute the player wakes.

*Not done, by design:* slipping on ice (nothing tasteful to be had from the knockdown system without editing it); a power cut is a notification only (the game has no safe way to switch a district's shops off); sight is exposed, not applied (see Hooks).

## Street events

On the calendar (`EventCalendar`): fireworks 21 to 24 h on the last day of the year and on the day 50.7 % through it.
A farmers' market 8 to 13 h every Saturday.
Otherwise a day has an event if its roll is under 0.08 x the slider: market 10 to 16, parade 12 to 15, protest 11 to 15, fun run 8 to 11, film shoot 9 to 17.

Announced at 18 h the day before and at 7 h on the day (toast and City Bulletin); while the player sleeps the toast is held for the morning summary.
With "Notifications about protests" off, a protest has no toast at all (what is on, the start, the end: `EventCalendar.Quiet`); it is held as ever, closes its street, and the City Bulletin still lists it.
It is held once, on a plain stretch of road 55 to 110 m from wherever the player is when it is due and they are out of doors, out of view; 20 to 32 m of road is closed with the cordon's tape and barriers; staff walk in, then the crowd.

At closing time (or in a thunderstorm) the crowd leaves, the staff pack and walk off, the tape comes down, and the props go once nobody is looking.

| event | on the closed road | the player can |
|---|---|---|
| market, farmers' market | up to three stalls (`vendor_cart`, `fruit_stand`, `coffee_cart`, a parasol), a vendor behind each, up to four customers who walk in, wait, pay and leave with a cup, a marshal | buy a snack for 6 (energy +6, mood) |
| parade | four marchers with banners and placards who form up and march end to end and back, two marshals, two onlookers on the pavement who clap as it passes | cheer (mood) |
| protest | five with placards, an organiser with a clipboard, two officers watching from one end | sign the petition (mood). Always peaceful: the crime system has no public way to arrest a bystander |
| fun run | runners with numbers who jog in, through and off, two marshals, onlookers | cheer (mood; the nearest runner waves) |
| film shoot | camera, two lamps, a chair; operator, director, assistant on the radio, two actors who play a take of about twenty seconds and wait between takes, a marshal | watch a take (mood); "Quiet on set" within 32 m |
| fireworks | a rack, a marshal, up to six people who walk in and watch; bursts 58 m up, 85 m off to one side | watch for twenty seconds (mood) |

Footfall for nearby businesses is not changed: the game rebuilds a business's promotion every day and its traffic index is not saved, so there is no safe mechanism; attending gives a mood instead.

## Hooks other features need (one line each)

- `Traffic/TrafficAccidents.Tick` (read-only to this work, so not made): the roll becomes `Random.value < Settings.AccidentFrequency * 0.02f * Events.Weather.AccidentFactor`, and its start condition gains `&& !Events.EventDirector.IsActive`.
- `Traffic/TrafficAccidents`, where a severe crash is staged: `Events.EventDirector.CrashFire(wreck)` sets that wreck alight for 50 s (flames, smoke, firelight; no engine).
- `Traffic/PursuitSight` and `Crime/PoliceAttention`: multiply their sight distances by `Events.Weather.SightFactor`.
- A wardrobe feature: `Events.Weather.Wardrobe = <IWardrobe>`.
- Made already: `Crime/CrimeDirector` (`* Events.Weather.CrimeFactor`, `&& !Events.EventDirector.IsActive`), `CityLife/CityLifeDirector` (`&& !Events.Weather.TakeCover`), `Phone/PhoneContacts` (the 911 dialog offers "Report a fire" and calls `Events.EventDirector.Report911()`).

## Asset wish-list

Everything below is asked for by id through `ClipLibrary.Has/TryGet` or `PropLibrary.TryGet`; when the bundle has it, it is used with no code change.
Until then: the fallback named.

Clips (Humanoid, in place, starting and ending in the standing idle unless said):

| id | length, loop | what | until then |
|---|---|---|---|
| `hose_hold` | 4 s loop, whole body | braced: feet apart, left foot forward, knees bent, leaning into it; right hand at the right hip, left hand 0.24 m ahead of it, both on a bar pointing forward and 12 degrees up, 1.02 m above the ground and 0.17 m right of centre; small recoil sway | **delivered** (palms on the nozzle 0.06 and 0.30 m along it). `HoseHold` keeps the wrists where the clip's own grip has them on the nozzle (`CityClip.Grip`: the nozzle on the right hand bone, the left wrist on the nozzle), so at the clip's aim nothing is corrected and at any other (3 to 45 degrees up) the palms stay on the grips; it used to put the wrists on the palms' points, 0.08 m too far along. Without the clip: `carry_box_hold` on the arms, both wrists put on those two points by `TwoBoneReach` |
| `pump_panel` | 4 s loop | standing square to a panel 0.5 m ahead: turning a valve at 1.1 m, reading a gauge at 1.4 m | `counter_wipe` |
| `keep_back` | 3 s loop | both palms out at chest height, a step forward, a look over the shoulder | `direct_traffic` |
| `crowd_look_up` | 6 s loop | head back, looking 50 degrees up, a hand shading the eyes now and then, pointing once | `watch_nod`, `take_photo`, `clap` |
| `placard_hold` | 4 s loop, right arm mask and a whole-body variant | a staff held upright in the right fist at 1.10 m, pumped up 0.15 m twice a loop | `paddle_hold` (its grip; the stop paddle's) |
| `camera_operate` | 6 s loop | at a tripod camera: right eye to an eyepiece 1.50 m up and 0.16 m ahead, right hand on a pan bar, left on the lens | **delivered**, for somebody 0.75 m behind `film_camera`'s pivot on the lens's line. `StreetEventScene.FilmSet` takes that place from where the camera really stands, `Arrivals` walks the operator's last steps on to it (within 0.08 m, the agent sent as far past it as it stops short) and the clip is played only there; anywhere else, and without the clip: `guard_stand` with `till_tap` |

Props (metres, X x Y x Z, pivot as said; all "lit" unless said):

| id | size | pivot and facing | until then |
|---|---|---|---|
| `fire_engine` | 2.72 x 3.20 x 7.43 | ground centre under the middle of the body, nose at +Z; axles at z +2.756 and -1.362 | **delivered, and rebuilt to the Delivery Truck** (2.72 x 3.01 x 7.43, wheelbase 4.118; the Freight Truck is 10.98 m, too long for a pumper): `EngineLivery` puts its axles on the truck's (the game's trucks have their origin under the rear axle, not their middle). On any other vehicle: the truck repainted red through property blocks, a light bar on its roof |
| `fire_bar_lens` | 0.60 x 0.13 x 0.30, glow | the middle of the underside of the engine's light bar; the lens at +X | **delivered**: a lit copy of one end of the modelled engine's light bar, shown over each end in turn by `BarFlash` in step with the two flashing lights (only while an engine is out) |
| `fire_light_bar` | 1.30 x 0.17 x 0.28, glow | centre of its underside; lies along X | shapes |
| `fire_helmet` | 0.30 x 0.19 x 0.40 | centre of the rim's lower edge, peak at +Z: put 0.085 m above the head bone | shapes |
| `fire_air_pack` | 0.34 x 0.75 x 0.40 | the upper-chest bone: cylinder on the back, harness, reflective bands front and back (as `mail_satchel` is modelled as worn) | shapes |
| `hose_nozzle` | 0.07 x 0.09 x 0.47 | the coupling at its back end; points along +Z; gripped 0.06 and 0.30 m along it | shapes |
| `street_bin` | 0.54 x 0.90 x 0.54 | ground centre | shapes |
| `placard` | 0.56 x 2.19 x 0.03 | the stop paddle's: the hand is 1.10 m above the pivot; the staff runs from 0.70 to 1.75 m (it is carried clear of the ground), the board above it faces +Z | shapes |
| `banner_pole` | 0.72 x 2.20 x 0.03 | as `placard`; a cloth 0.70 x 0.48 to +X of the staff | shapes |
| `runner_bib` | 0.22 x 0.17 x 0.01 | the upper-chest bone; on the chest 0.134 m ahead of it, 0.07 m below | shapes |
| `film_camera` | 0.9 x 1.7 x 0.9 | ground centre of the tripod; the lens looks along +Z, 1.47 m up | shapes |
| `light_stand` | 0.9 x 2.5 x 0.9, lamp glow | ground centre; the lamp 2.32 m up shines along +Z | shapes |
| `directors_chair` | 0.55 x 1.10 x 0.45 | ground centre; faces +Z | shapes |
| `firework_rack` | 1.2 x 0.55 x 0.5 | ground centre | shapes |
