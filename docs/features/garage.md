# Garage

## In a sentence

A custom shop for cars in the game's repair bays (paint, finishes, fitted parts and performance parts) and bicycles and motorbikes made from the game's scooters, to hire, buy and ride.

## Where the code is

- Folder: [`Scripts/Garage`](../../Assets/Mods/RealLifeOverhaul/Scripts/Garage/).
- Read first: `GarageDirector.cs` (the one component that ticks), `GarageCatalogue.cs` (paint chart, parts and every price, game-free), `GarageCar.cs` (one car wearing its build) and `TwoWheelers.cs`.

| File | What it is |
| --- | --- |
| `GarageDirector` | the one component that ticks: builds onto spawned cars, underglow after dark, the timers of the lists and panels, the test buttons; GarageSummary ("My vehicles", in the game's list) |
| `GarageBay` | the game's own panel in a repair bay with one more line and a "Garage" button (RepairBayOverlay); the workshop's list; GarageList (the one list window the Garage's screens share) |
| `GarageCatalogue` | paint chart, finishes, performance parts with effects and trade-offs, every price; GarageBuild (one car's build, one line of text) (game-free) |
| `GarageState` | the save's part: builds by vehicle id, which scooters are two-wheelers; GarageBridges (what other features may set or ask) |
| `GarageFit` | every car body as measured (roof, boot deck, nose) and which fitted part goes on which car |
| `GarageCar` | one car wearing its build: paint through the game's own property blocks, lamps, underglow, fitted parts, brakes, tyres, springs, weight; puts all of it back |
| `GarageTuning` | power, gearing and drag for a build through CarModel; the hook for Driving/CarTuning; the shop's before-and-after figures |
| `GarageShop` | the repair bays, the turntable camera, the shop's pages in the game's list, the game's colour picker, the bill; GarageDragArea (the invisible drag surface of the turntable view) |
| `GarageText` | a locale key in, the line out |
| `Driving/Sound/*` | the recorded car sounds: engines by kind, the exhausts' and turbos' sounds, the turntable's revving (see "Recorded car sounds") |
| `TwoWheelers` | bicycles and motorbikes as re-bodied scooters: making, removing, hiring, buying, energy, helmet, falls; the game's panel at a dock, rack or stand, the stand's list, the game's marker on one |
| `BikeMount` | one scooter as a bicycle or motorbike: body, numbers, the rider's seat, hands and feet; LegIk |
| `BikeBody` | the four models (BikeCatalogue) and their stand-in bodies built from tubes and rings (MeshMaker) |
| `BikePlaces` | bike-share docks, bicycle racks, motorbike stands: where they go and what stands there |
| `BikeClearRules` | where a bike may be left: what it may not be left in, the places tried round it, whose may be moved (game-free) |

## How it talks to other features

- `GarageBridges` is what other features may set or ask: wet roads (`Wiring/LifeLinks.cs`), wear and insurance read by `Transport/CarOwnership`, and the motorcycle licence (`Wiring/LicenceLinks.cs`).
- Performance parts tune the car through `Driving/CarModel`; see [Driving](driving.md).

Car customisation and two-wheelers, in `Scripts/Garage`.
Everything is kept in the save's modData, keyed by the vehicle's own id; the vehicles in the save stay exactly what the base game made, so a save loaded without the mod shows stock cars and ordinary scooters.

## How the game paints a car (read from its files with `GarageAssets/scripts/extract_vehicles.py`)

- Every player vehicle is **one body mesh on one material** of the shader `Shader Graphs/SH_Vehicle` (18 materials, one per model; the scooter too).
  Paint, glass, lamps, trim, number plate and the wheels all share that material's one texture; a mask in the texture says where the paint is.
  The wheels are separate renderers on the same material.
- `CarFeatures.SetColor(VehicleColor)` writes three properties into a `MaterialPropertyBlock` on each of `CarFeatures.bodyMeshes`: **Tint** (`Color_3d0f0cdbe6b74be28a1a5be5bab71dea`), **FresnelColor** (`Color_f78fac473bac467092fb27521e9f71ea`, the colour the paint turns at a glancing angle) and **FresnelPower** (`Vector1_481fa2a8a5e94165a039319bfd512b76`).
  It is called once, from `VehicleController.SetVehicleInstance`, with the `VehicleColor` whose name is in `VehicleInstance.vehicleColorName` (chosen at the dealership).
  Dirt is a fourth property, `_Dirtiness`, written the same way (get block, set, set block), so ours survive it.
- The shader has **no metallic, clear-coat or per-part property**.
  What else it has: `SmoothnessBoost` (`Vector1_41991b689ccf4932ad504975ac34399a`, one gloss factor for the whole material), `_EmissionColor` (the colour of every lamp), and the lamp and indicator strengths the game drives itself.

So: **paint** is Tint in any colour, a **finish** is FresnelColor + FresnelPower + SmoothnessBoost, **lamp tint** is `_EmissionColor` and the colour of the car's own headlamp `Light`.
All through the same property blocks, once per change.

And these cannot be done on the game's cars, and are not offered: **window tint** (the glass has no material or property of its own), **wheel colour** (the rims are in the shared texture, outside the paint mask), **number plate text** (the plate is painted into the body texture), **two-tone roofs, stripes and liveries** (one material and one UV atlas per car; a flat stripe laid over a curved bonnet either floats or cuts into it, on every car differently).

A matte finish dulls the glass and trim too, because SmoothnessBoost is material-wide; the shop says so.

## The custom shop

At every repair bay the game has (the `GasStationTrigger`s its `GasStationController`s list as repair stations: auto repair shops and truck garages).
Drive a car of your own into a bay, stop, and use the "Garage" button of the game's panel there (see [Car ownership](transport.md#car-ownership)); "Custom shop" is the second entry of its list.

The car is held where it stands: its pedals taken off the keys, its body frozen the way the car wash freezes it.
The mod's own camera goes onto a turntable round it (drag on the free part of the screen to turn, wheel to move in and out).

The game's list window stands left of the middle, clear of the game's task panel, phone and item panel, with the car seen to its right (the camera stands a fifth of its distance to the left of its line to the car).

That window is `GarageList`: a copy of the window the game lists its subway stations in, 1,100 wide with entries of 150 so that an entry can say what a part does.

Its first page has the bill ("Pay"), one entry per page below, and "All stock"; a page has "Back" and one entry per choice, whose button reads "Chosen" and is off for the one in the cart.
Every choice is on the car at once; the lines under the headline show the money, the bill and the car's figures before and after.

A colour of one's own is mixed in the game's own colour picker (`UIs.customColorPicker`, the one its interior designer uses: the car is repainted as the colour moves; its button keeps the colour, and also adds it to the game's list of the player's colours, which is what that picker does; closed any other way, the paint is as it was).

**Pay** keeps the build; **Cancel** (the list's yellow button or the game's cancel key, or getting out, or walking away) puts the car back as it came in; **All stock** takes everything off.

Work is instant: the game has no notion of a car that is away being worked on, and a car hidden or taken out of the save's vehicle list for a few hours would be a save the base game cannot account for.

| page | what | price |
|---|---|---|
| Paint | 42 chart colours in six families of seven shades (an entry per family, written in the shade chosen, whose button steps to the next shade), the colour it was bought in, or any colour mixed in the game's colour picker | 600 chart, 900 mixed, times the body factor |
| Finish | gloss, satin, matte, metallic, pearl | 150 / 300 / 500 / 700 / 1 200, times the body factor |
| Lights | lamp tint: standard, warm white, ice blue, rally yellow; underglow kit in seven colours | 150; 400 (another colour later 100) |
| Body | boot spoiler, roof bars, bull bar: each only on the bodies it fits; other wheels when the prop bundle has them | 450, 350, 600; wheels 1 200 |
| Parts | one part in each of eight slots (below); an entry's button steps to the slot's next part | a multiple of the part base |

Body factor: economy 1, saloon 1.2, utility 1.4, van 1.5, luxury, sports and trucks 2.
Taking a fitted thing off costs 50.
Half of everything spent on a car counts as value added (`GarageBridges.ValueAdded`).

**Fitted parts are placed from each body's measured shape**, not its bounding box: `GarageAssets/scripts/extract_fit.py` reads every triangle of every car body and writes one row per car into `GarageFit.cs` (roof: where it is level, its height at the bars, its half width, the height under the feet; boot deck: height, slope, half width; nose: its low and high edge, half width, bonnet height).

A **spoiler** is offered where the boot deck slopes under 25 degrees and is at least 0.25 m below the roof (AF90, 112, Mimic, limousine, MGA GT, S500, Fanton, Pony), **roof bars** where the roof is level for 0.9 m (everything but the sports cars and the trucks), a **bull bar** on SUVs, pickups and vans (Nunavut, V150, Desert), as tall as the upright part of the nose allows.

The Mersaidi Dash has no measured body (its prefab is built differently) and gets none; neither does a car from another mod.
Stand-ins are plain black shapes from `Core/Shapes`; the modelled parts replace them without a code change when the prop bundle has them (wish-list below).

## Performance parts

Layered on the per-car model of "Cars" above: a build **divides the 0-100 km/h time and multiplies the top speed**, and `CarModel.Solve` finds the power, gearing and drag for the new targets.

Brakes, tyres, springs, weight and the engine's note are changed on the car itself (NWH: `brakes.maxTorque`, the wheels' friction grip, spring force and length, damper rates, the axles' anti-roll force, the body's mass, the engine sound's pitch and volume), recorded first and put back exactly.

Part base by kind: economy 1 000, saloon 1 800, utility 2 500, van 2 000, truck 3 500, luxury 4 500, sports 6 000.

| slot | part | x base | effect | cost in use |
|---|---|---|---|---|
| Engine | Stage 1 / 2 / 3 | 1.0 / 2.2 / 4.0 | acceleration +6 / +12 / +20 %, top speed +2 / +4 / +7 % | wears 5 / 12 / 25 % faster; stage 3 insurance +10 % |
| Exhaust | Sports / Straight-through | 0.5 / 0.9 | acceleration +3 / +5 %, top +1 / +2 %, higher and louder note | straight-through is not street legal |
| Turbo | Turbocharger / Big turbo | 2.5 / 4.5 | acceleration +10 / +18 %, top +5 / +8 % | wears 10 / 22 % faster; big turbo insurance +10 % |
| Gearing | Short / Long final drive | 1.2 | acceleration +7 %, top -8 % / acceleration -6 %, top +6 % | |
| Brakes | Sports / Race | 0.8 / 1.8 | brake torque +25 / +50 % | race: wears 5 % faster |
| Tyres | All-weather / Sports / Semi-slick | 0.7 / 0.6 / 1.2 | wet grip +15 % / dry +10 %, wet -10 % / dry +20 %, wet -25 % | semi-slick: wears 8 % faster |
| Suspension | Sports springs / Coilovers / Lift kit (SUVs, pickups, vans) | 0.9 / 1.8 / 1.0 | stiffness and anti-roll +25 % and 2 cm lower / +50 % and 4 cm lower / +10 % and 4 cm higher | |
| Weight | Lightened interior / Stripped out | 1.0 / 2.5 | weight -5 / -10 % (as much quicker) | stripped out: insurance +5 % |

However the parts stack, a build is never asked for more than 1.6 times the acceleration or 1.25 times the top speed.
A car is lowered only inside the travel its own suspension already uses (at most a quarter of what is left of the spring at rest, never more than 5 cm), and never into the 12 cm of bump travel a kerb needs.
The springs part stands the car its height lower or higher at rest whatever the spring under it, on that spring's own curve (the Mersaidi Dash's rises): a stiffer spring sinks less under the same weight, so its length is chosen for the height (`Driving/HandlingModel.FitSpring`), and the roll centre moves with the body. The weight part's lighter car is carried at the same height and frequency (its springs take the weight's factor too), so it never stands higher and rolls over sooner. The dampers take the square root of the springs' and the weight's factors, so a stiffer spring or a lighter car swings as little as before.
With car handling on (docs/features/driving.md, Handling), the parts act on the handling: Driving lifts them off before it changes a car and lays them back on top (`GarageCar.LiftHandling`, `LayHandling`, through `Wiring.DrivingLinks`), so a part is never under the handling and never applied twice. The brakes part raises the deceleration only as far as the tyres take it: the handling sets the axles' brake shares again for the parts on top.
"Wet" is the game's rain unless a Weather feature sets `GarageBridges.RoadIsWet`.

The shop shows 0-100 and top speed from the model (when the player sits in the car and car tuning is on) and the stopping distance from 100 km/h from the brakes, tyres and weight (with car handling on the car, by the handling's own model: the axles' brake shares, the tyres' grip along and the eased pedal's rise, `Driving/HandlingModel.ShopStop`); what is applied is logged once per car and build ("Garage: ..."), and "Measure this car" times the real thing.

**The hook for `Driving/CarTuning`** (two lines; until they are there, `GarageTuning` does the same work itself after CarTuning has run: an engine power modifier, the gear ratios, and the difference in drag as its own force, each put back exactly; with car tuning off it falls back to the power modifier and the game's speed limiter):

```csharp
// CarTuning.Update, the end of the "options" expression: a build that changes is a change of options
    | (Mathf.Clamp(Settings.CarAcceleration, 70, 150) << 10) | (GarageTuning.Stamp << 18);
// CarTuning.Apply, after "float topKmh = ..." and before CarModel.Solve
GarageModifiers garage = GarageTuning.Claim(car.vehicleInstance); seconds /= garage.Acceleration; topKmh = Mathf.Min(topKmh * garage.TopSpeed, MaxTopKmh);
```

The first call of `Claim` makes `GarageTuning` stand down for good, so the two never both apply.

A straight-through exhaust driven above 36 km/h is noticed by the police now and then (a look every 30 s, 8 %, once a day at most): `GarageBridges.PoliceNotice("exhaust", 120)` if a Crime feature has set it, otherwise a fine of 120 through `Crime.Fine`.
For a Transport feature: `GarageBridges.WearOf(id)`, `InsuranceOf(id)`, `ValueAdded(id)`, `LoudnessOf(id)`, `IsTwoWheeler(id)`, and `GarageSummary.Lines` for a garage page of its own.

## Recorded car sounds

The option "Recorded car sounds" (`garage_sounds`, on by default) gives the car the player drives, and the car on the shop's turntable, real recorded engines; everything else keeps the game's own sound.
The code is in [`Scripts/Driving/Sound`](../../Assets/Mods/RealLifeOverhaul/Scripts/Driving/Sound/), the files in [`Sounds`](../../Assets/Mods/RealLifeOverhaul/Sounds/) with [`SOURCES.md`](../../Assets/Mods/RealLifeOverhaul/Sounds/SOURCES.md) (every file's recording, author, licence, cut, changes and measurements).

| File | What it is |
| --- | --- |
| `CarSoundRules` | the engines' loops and their revs, the crossfade, load, the exhausts' and turbos' sounds, the shop's revs, the squeal (game-free; NavCheck's CarSoundCheck) |
| `CarSoundBank` | reads the 33 clips once a session from the mod's folder (UnityWebRequestMultimedia, in the background as the mod starts) |
| `CarSoundCar` | one car playing its recorded engine on sources of the mod's own on the car's own sound holders |
| `CarSoundGame` | the one place that meets NWH: the SoundManager, the engine, the wheels, the throttle key |
| `CarSoundDirector` | which car plays, every way back to the game's own sound, the self-check lines, the test button |
| `CarSoundBridges` | what it asks of the Garage (Wiring/CarSoundLinks): the build's exhaust and turbo, the turntable's car |

**The engines** (`CarSoundRules.VoiceFor`): economy cars and saloons a small petrol four (a Ford Ranger's 2.5, recorded at the engine), luxury cars, SUVs, pickups and the two muscle cars (Vord Pony, the Mustang GT of another mod) a V8 (a Chevrolet 350), the other sports cars a sports engine (an Audi R8's V10), vans and trucks a diesel (a four-cylinder diesel recorded under the bonnet while driven).
Each is a set of loops cut from steady holds of a real engine: idle, low, mid and high (the petrol has idle, low and high: its recording holds three speeds), each above idle with an off-throttle loop beside it.

| engine | loops at (rpm, from each recording's firing frequency) |
| --- | --- |
| petrol | 855, 2 076, 2 928 |
| V8 | 996, 2 177, 2 766, 3 611 |
| sports | 804, 1 970, 2 767, 4 062 |
| diesel | 783, 1 227, 1 650, 2 577 |

**How it plays.** The game's NWH sound system has one engine source (`EngineRunningComponent`: one clip, pitched by `RPMPercent * pitchRange + pitchOffset`, its volume by load, its distortion set on the whole engine mixer group) and cannot cross between recordings.
So the loops play on audio sources of the mod's own, added to the car's own NWH source holders (`SoundManager.engineSourceGO`, `exhaustSourceGO`, `otherSourceGO`) on its own mixer groups (`engineMixerGroup`, `surfaceNoiseMixerGroup`), with its spatial blend, Doppler level and master volume, so the game's volume, the in-car muffling and the falloff apply as to its own.
Meanwhile the game's engine note, pops, turbo whistle and flutter and skid sources on that car are muted (`AudioSource.mute`, nothing else of them changed) and the note's distortion (`maxDistortion`) is 0; both are given back.
Once a frame, from the powertrain (`engine.OutputRPM`, `ThrottlePosition`, `idleRPM`, `revLimiterRPM`): the loops keep their recordings' ratios to each other, with the idle loop at the car's idle (moved up together if the top loop would be pitched past 1.6 at the rev limit, the idle never deeper than 0.75); two neighbours cross over equal-power across the middle half of their span on a scale of ratios, each pitched by the revs over its own; on and off the throttle cross equal-power by the throttle.
A loop with nothing to play is stopped.

**The parts.** The exhaust and turbo of the Garage's parts page are heard (on the turntable the cart, else the build, and stock with the Garage's performance parts off):

| part | price (x part base) | trade-off | sound |
| --- | --- | --- | --- |
| stock exhaust | | | the engine as recorded |
| Sports exhaust | 0.5 | | 1.2 times as loud, a little lower, rawer off the throttle, the odd pop after lifting off from high revs |
| Straight-through exhaust | 0.9 | not street legal (fines) | 1.4 times as loud, lower, raw off the throttle, pops and bangs after lifting off |
| Turbocharger | 2.5 | wears 10 % faster | whistles as boost builds (spool 0.6 s), flutters on lifting off with boost |
| Big turbo | 4.5 | wears 22 % faster, insurance +10 % | slower (1.0 s), lower and louder whistle, bigger flutter |

The parts page says how each sounds, and the shop's lines say to hold the throttle key: on the turntable the revs are the sound's own, rising towards 85 % of the rev limit while the key (the player's own throttle binding, through NWH's input providers) is held and falling back to idle when it is let go, so a build is heard before it is paid for.
Tyres: a recorded squeal, as loud as the game's own skid sound reckons the slip of the loudest wheel, on the surfaces where the game plays a skid.

**Every way back.** The game's own sound comes back the frame it is no longer wanted: getting out or into another car, the shop closing on a car nobody drives, a load (`LoadScene.isLoading`), the city going (the director's end), the option or the Garage off, a fault (logged; off for the session); a car that is gone takes the mod's sources with it.
Only the local player's own car is ever held (`GoingPublic.MineVehicle`); traffic (Gley's `EngineSoundComponent`), the bus, the tow truck and the two-wheelers are never touched. Nothing is saved and nothing is sent.

With Developer diagnostics on: `Car sound check (entering a car)` (the engine, clips in memory of 33, the mod's sources, the game's sources quieted) and `Car sound check (leaving a car)` (the game's sources back as they were).
Options, Tests: "Check the recorded car sounds".
Not verified in the game: the loudness against the game's own engine (`EngineVolume` 0.7), the game's cars' idle and rev limit (read live; the loops are placed on them), how the turntable's revving sounds with the car frozen.

## Two-wheelers

The game's electric scooter is an NWH vehicle with a motorcycle module (lean, steering), a 45 kW engine, one forward gear, 250 kg, a 45 km/h limiter, and a rider the game attaches to it in a standing pose with the hands on its bar.

**A bicycle or motorbike here is one of those scooters**: made with `VehicleHelper.CreateAndSpawnVehicle(new VehicleInstance("ba:vehicletype_electricscooter"), ...)`, ridden with `ScooterController` untouched, saved in the save's own vehicle list. modData says which ids are what (`reallife:garage_bikes`).
`BikeMount` then, on the spawned object only:

- **body**: the scooter's renderers are set not to draw (`forceRenderingOff`; nothing is removed) and a body is built under it (`BikeBody.Build`): the modelled bike when the bundle has it, else a stand-in of tubes, rings and boxes in three shared meshes per part (a diamond frame, spoked wheels, bars, saddle, cranks and pedals; or tank, seat, engine, forks, exhaust, fat tyres).

  Either way wheels turn with the speed, the fork and front wheel steer, the cranks go round with level pedals while the rider pedals.
  The modelled bike is put together from its parts (`<id>_frame`, `_fork`, `_wheel` or `_wheel_front` / `_wheel_rear`, `_crank`, `_pedal`: at most nine renderers, all shared meshes), each on the object `BikeMount` turns; nothing turns while nobody rides.

- **colour**: what is painted (frame tubes, mudguards, tank, tail, fairing) is a part of its own on a neutral swatch (`<id>_frame_paint`, `<id>_fork_paint`) drawn with `PropLibrary.Tinted(colour)`: the atlas material with its base colour multiplied, ONE shared instanced material per colour (never per bike, no property blocks), gone with the library's other materials when the city unloads.

  The colour is `BikeRecord.Colour`, else `BikeDef.Colour`, which is the colour the whole model is painted in the atlas (city 0x124D70, road and sport 0x9E090A, roadster 0x090F2B).
  A bike that only stands on show in that colour (`Build(..., still: true)`: the shops' racks) is the whole model as one mesh, one draw call; the docks' bicycles are in the share scheme's blue, so they are parts.

- **grips**: `BikeDef.Grip` is per model and measured on its bar by `PropAssets\scripts\checks_garage.py`: the town bar's rubber grips, the TOPS of the road bicycle's drop bar (0.13 either side of the stem: the one part of a drop bar that lies along X, which is what the game's hand rig holds), the roadster's bar, the sport bike's clip-ons under the top yoke.
- **numbers**: a bicycle gets a small "engine" behind the pedals (1.2 or 1.6 kW for the scooter's 250 kg), almost no engine braking, a bicycle's rolling resistance, air drag sized so that steady pedalling holds 38 or 50 km/h (24 and 32 before 0.1.4, which felt slow against the city), and no engine sound.

  A motorbike gets power, final drive and drag from `CarModel.Solve` for its targets, the limiter off and 1.6 times the brakes.
  Logged once per kind ("Two-wheelers: ...").

- **rider**: the game's own riding pose, moved so that the hips are over and just above the seat (fitted to the character's own hip height), the hands on our grips through the game's own hand rig, and after the animation each frame the legs put on the pedals (following the cranks) or the footpegs (`LegIk`, knee forwards).

  A helmet follows the head on a motorbike once the player owns one, turning with it (`BikeMount.HeadSquare` reads from the avatar's T-pose how the Head bone's own axes stand to the character; if the avatar does not say, the helmet keeps the player's heading as before).

  With the clip bundle the mod's own loops are played instead, chosen by what the rider is doing (`BikeDef.Ride`, `Coast`, `Stand`; `BikeMount.Pose`: three comparisons a frame while ridden, the engine told only on a change that has lasted 0.3 s, 0.1 s to set off):

  | | riding (a bicycle: pedalling) | freewheeling | stopped (under 0.35 m/s, no throttle; riding again over 0.8 m/s) |
  |---|---|---|---|
  | city bicycle | `cycle_ride` | `cycle_coast` | `cycle_stand` |
  | road bicycle | `cycle_ride_road` | `cycle_coast_road` | `cycle_stand_road` |
  | roadster | `moto_ride` | | `moto_stand` |
  | sport bike | `moto_ride_sport` | | `moto_stand_sport` |

A model's own clip missing, the town bicycle's or the roadster's is used; none at all, the game's pose.
On a bicycle the feet stay on the pedals through `LegIk` while riding and freewheeling; stopped, the clip has the left foot on the ground, so `LegIk` is faded out over 0.3 s and the cranks are brought round (backwards, as on a freewheel) to the right pedal forward, where the clip's right foot is.

The bike itself stays upright (the game's scooter physics): the stopped clips lean the rider, not the machine.

| model | at | price | goes |
|---|---|---|---|
| City bicycle | bike-share docks (hire 15, back to any dock by the end of the next day, else collected with a 25 late fee); racks outside supermarkets | 350 | about 38 km/h, 0.06 energy a second of pedalling |
| Road bicycle | racks outside supermarkets | 900 | about 50 km/h, 0.08 energy a second |
| Motorbike | stands outside car dealerships (at repair garages if the city has no dealership open) | 7 500 | 0-100 in 6.0 s, 150 km/h |
| Sport motorbike | the same stands | 18 000 | 0-100 in 4.5 s, 190 km/h |
| Helmet | the same stands | 120 | riding without one: a look every 30 s above 18 km/h, 10 %, once a day, fine 60 |

Docks stand beside every other one of the game's scooter stands (eight at most), racks outside up to five supermarkets, stands outside up to four dealerships: found once per city load, in a fixed order, each on pavement with room for the whole thing.

Within 3.5 m of one the game's panel over a place (`GameUi.WorldPanel`) names it and has one button on the game's interact key: "Hire" (with the fee), "Return" (seated on a hired bicycle that stands), or "Browse", which opens the game's list with what the rack or stand sells (buy, a helmet, sell an owned one standing by for half its price).
`GarageDirector.TakesInteractKey` is true while the panel is up.

Holding the throttle is pedalling: it costs energy through the game's own `EnergyHelper`, never below 14; a worn-out rider has half the push; letting go coasts.
A motorbike asks `GarageBridges.HasMotorcycleLicence` (null: yes) only at the sale: the dealer warns in a sentence if it is no and sells all the same.
Riding unlicensed is policed by Licences (the warning on getting on, the roadside check: fine, tow, arrest the third time).

Coming off (most of the speed lost in one physics step, or the bike on its side while moving) calls `GarageBridges.RiderFalls(speed)` if a Ragdoll feature has set it, otherwise the bike stops and the rider is on their feet.
Traffic sees a ridden two-wheeler as it sees a ridden scooter: the game puts its collider on the vehicles layer.

**Where one is put, and finding it.** A place faces along its pavement, so "beside it" is the shopfront on one side and the kerb on the other.

A bike handed over is therefore stood **along the pavement** from the place (2.6 m, 3.4 m, then the corners, the sides last), and a spot must pass four tests: ground under it (`BikePlaces.StandHeight`: the ground ray, or the pavement's own surface where that is higher), pavement (only if no pavement spot is free is level ground in sight accepted), room for the whole bike (`Fits`), and **a clear line from the place to it** (`BikePlaces.Seen`).

The last one matters: `Fits` asks whether a box touches anything, a wall is a skin of triangles, and a box wholly behind a shopfront touches nothing, so a spot indoors "fits" (v0.35.0 tried the sides first, without the line: a motorbike bought at a stand a step out from a dealership wall could be made behind that wall, the likeliest reason the first one bought was never seen).

The test buttons and "Bring it here" use `BikePlaces.SpotAhead`: 2.2 m in front of the player on the ground under that point, side on, in their sight.

**Where a bike is left.** The game holds a scooter nobody is on wherever its rider got off (`ScooterController.ExitVehicle` turns its physics off and freezes it), and asks nothing about room. A rider who comes off against a parking meter or a post (the step that stops the bike is the one its box went into the thing), or pulls up with the bars over a meter's head, left the bike in it; got on again, its boxes were inside the meter and it could not be ridden out (a player's report, 19 Oct 2026). So the moment a bike is got off (the game's own way off, a fall, the dock's return: all come through `TwoWheelers.Frame`) and the moment it is got on, `TwoWheelers.Unpin` asks `BikeMount.Pinned` whether its frame box or its bars box, each less a 3 cm skin, is in anything solid (`BikeClearRules.Pins`: street furniture, buildings, vehicles; never the ground, people or zones), leaving out its own colliders and the player. A bike of the player's own (`GoingPublic.MineVehicle`) found in something, out of doors, is stood upright, facing as it was, on the nearest of 40 places round it (`BikeClearRules.Place`: rings of 0.5 to 2.6 m, either side first) that is pavement level with it (else ground within a step), clear of solid things and people, with no wall between (`BikePlaces.Unwalled`). With none clear it stays, and "Bring it here" is the way out. Two box queries per getting off or on; NavCheck's BikeClearCheck checks the rules and when they are asked.

**Its weight once ridden.** The scooter's centre of mass and inertia are the game's own: `VariableCenterOfMass` writes them (an inertia of 234.4, 208.3 and 67.7 kg m²) each time the game lets a scooter go (`VehicleController.SetFreeze(false)`, as the player gets on). `BikeMount` keeps them before its boxes change and writes them back after each change, and does both only while the body is free to turn (`BikeRules.KeepMass`, `BikeRules.PutMassBack`): the game holds every scooter nobody is on frozen (`SetFreeze(true)`, every constraint), and Unity freezes a body's turning by setting its inertia about that axis to zero, which the physics takes as infinite. A bike dressed while it stood, as every one is after a save loads, kept that zero and wrote it back as it was got on: its front wheel steered and it went straight on, with only a few centimetres of sideways slide (a player's report, 19 Oct 2026; every model, since 0.27.5). Now they are kept when a bike is made, if its body is free then, and again each time it is got on, when the game has just let it go; nothing is written into a bike the game holds. Getting on writes `Two-wheelers: turning check (got on): ...`: the PASS with Developer diagnostics on, a FAIL always. NavCheck's BikeTurnCheck checks the rules, the sources, and a model of what the game and the mod do to the body for every way back on (saved standing or in the saddle and loaded, bought, got off and on, fallen from, stood back and held, dressed again).

- The game marks the player's cars on the map and, within 100 m, on the street (`CityMap.AddPoi`, set on `VehicleController.poi`); it leaves scooters out on purpose.
  `BikeMount.Mark` gives ours that same marker under their own name; the game hides it while it is ridden and destroys it with the vehicle.
- On buying, hiring, a test button, "Show me" and "Bring it here" the game's own destination marker (`GameUi.GuideMark`: the guider the game marks a job's destination with) is on the bike for 20 s (30 s for "Show me"), until the player is within 3 m, on it, or indoors; the toast says how far and which way.

  A job or an outing that has that marker keeps it, and the bike's is asked for again four times a second.
  Nothing per frame.

- "My vehicles" gives every two-wheeler its address, district and distance, with **Show me** and **Bring it here** (on foot, out of doors; free from within 25 m, otherwise 20; one the game no longer has in the world is made again, the same sort and colour).
- An owned one found behind a building's wall when a save loads (`BikePlaces.Walled`: pavement near it, a wall between) is said once, with the way out.
- Parking: a two-wheeler's `parkingState` is `Undefined`, so the game's hourly public-parking charge and its parking tickets ("electric scooter") skip it.
- The log says everything about one that is made, and again two seconds on ("Two-wheelers: made ...", "two seconds on ..."): position, heading, tilt, the ground found under it, pavement or not, how many of the body's renderers are drawn, forced off or without a material, their bounds, how many of the scooter's own are hidden or still drawn, active state, layer, scale, collider layer, rigidbody, physics, map marker.

  One that is no longer where it was put (fallen, flown, on its side) is stood back once and held as the game holds a parked scooter.

**Saving.** The game removes a scooter four hours after it was last looked at (`AutoDestroyVehicle`); on ours that component is switched off, and they are saved with `lastSeen` empty, so with the mod they stay.
Without the mod they are scooters, and the game clears them away by its own rule once they have been seen and left.

An owned one that went that way is stood back where it was last left, the next time the save is loaded with the mod.
A build is dropped from modData when its car is no longer in the save (sold).

Not done: carrying a bicycle; a phone entry of its own for "My vehicles" (it is one of the Garage's lists in the game's window, from the workshop's list and the options page, and `GarageSummary.Lines` for another feature's page; a car is an entry to read with its button off, a two-wheeler has an entry for "Show me" and one for "Bring it here").

The item panel still calls a two-wheeler an electric scooter, because to the game it is one.

Not verified in the game (it could not be run): that the v0.35.0 motorbike was behind the wall (the code allows it and nothing else found does; the log line above settles it), whether the pavement's ground collider is on a layer the ground ray sees (hence `StandHeight`), and how the scooter's physics settles one that is made with nobody on it. That a frozen scooter reads an inertia of zero is from Unity's own documentation of `Rigidbody.inertiaTensor` (rotation constraints are made by zeroing the inertia about the frozen axes), not measured in the game; the turning check's line says what a bike got on has.

## Wish-list (assets that replace a stand-in with no code change)

Props, in `Bundles/reallife_props` under these ids (metres; +Z forwards, +Y up):

| id | size | pivot | notes |
|---|---|---|---|
| `car_spoiler` | 1.40 wide x 0.18 high x 0.26 deep | on the boot deck under the middle of its two feet (feet at x = +-0.42) | scaled in X to the deck; dark, satin |
| `car_roof_bar` | 1.20 wide x 0.11 high x 0.06 deep | on the roof under the middle, feet at x = +-0.52 | one bar; two are fitted; scaled in X |
| `car_bull_bar` | 1.00 wide x 0.60 high x 0.14 deep | bottom centre of the hoop; the mounts reach 0.12 m behind it | scaled in X and Y |
| `wheel_5spoke`, `wheel_mesh`, `wheel_steel` | radius 0.34, 0.22 wide | centre of the wheel, axle along X, outer face at -X | a whole wheel, tyre included: it replaces the car's own; scaled to each car's wheel |
| `bicycle_city`, `bicycle_road` | 1.78 long x 1.14 high x 0.56 wide | ground under the middle; wheels (radius 0.34) at z = -0.52 / +0.58 (road -0.49 / +0.55); saddle top 0.93 at z -0.30 (road 0.95, -0.29); grips at (+-0.26, 1.14, 0.16) (road: the tops of its drop bar, +-0.13, 1.00, 0.42); bottom bracket (0.29, -0.08) (road 0.28, -0.07) | delivered whole and in parts (`_frame`, `_frame_paint`, `_fork`, `_fork_paint`, `_wheel`, `_crank`, `_pedal`): see [`PropAssets/README.md`](../../PropAssets/README.md), "Garage" |
| `motorbike_standard`, `motorbike_sport` | 2.05 long x 1.10 high x 0.75 wide | ground under the middle; wheels (radius 0.33) at z = -0.651 / +0.723 (the scooter's own); seat top 0.83 at z -0.20 (sport 0.84, -0.22); grips (+-0.31, 1.075, 0.30) (sport: clip-ons, +-0.27, 0.915, 0.42); pegs (+-0.24, 0.34, -0.10) (sport +-0.24, 0.38, -0.16) | whole and in parts (`_frame`, `_frame_paint`, `_fork`, `_fork_paint`, `_wheel_front`, `_wheel_rear`) |
| `helmet` | 0.27 x 0.29 x 0.30 | the base of the skull (the Head bone), +Z the face | full-face |
| `bike_dock` | 2.6 wide x 1.25 high x 0.2 deep | ground centre, 0.80 m behind the hoops' line (the bikes stand at the pivot, front wheels in the hoops) | three hoops 0.8 m apart, a pay post at +X |

Clips, in `Bundles/reallife_anim` (Humanoid, in place, loops; the table under "rider" above, the numbers in [`CityAssets/README.md`](../../CityAssets/README.md), "Garage"):

- `cycle_ride`: seated upright, the ground of the clip 0.93 m below the saddle surface, hands forward at 1.14 m, legs anything (they are placed on the pedals afterwards);
- `moto_ride`: seated, seat 0.83 m up, feet on pegs 0.34 m up and 0.24 m out, hands on bars at 1.075 m and 0.50 m ahead of the hips;
- for each model its freewheeling and stopped loops, and the road bicycle's and sport bike's own riding loops.

Sounds: a freewheel tick and a bell for bicycles, an air tool for the shop.

## Testing

Options, "Custom shop and two-wheelers": open the custom shop on the current or nearest car wherever it stands; a random full build (free); restore to stock; "My vehicles"; a city bicycle, road bicycle, motorbike or sport motorbike 2.2 m ahead, side on, with the marker on it.

A button that cannot do it says why (`Core/TestRefusal`: not loaded, option off, no car of yours within 20 m, not a car, a job's or hire company's vehicle, shop already open, in a vehicle, no clear ground, indoors, traffic not running).
`Player.log` has one line for each thing applied.

The shop and "My vehicles" asked for on the options page open when the menus are closed (the game's list stands on the HUD, which is not shown under a menu), and a notification says so.

With "Developer diagnostics" on: `Garage check (custom shop list)` when the shop's list opens, on each page and when it closes (with why); `(choice)` for every choice with the bill; `(place panel)` when the game's panel comes up or goes at a dock, rack or stand; `(stand list)`, `(my vehicles)` and `(marker)` likewise.
Not verified in the game: how the list reads at 1,100 wide with entries of three lines.

That a name written in a dark colour is readable (a colour darker than 22 % grey is lightened for the name only).
That the game's colour picker draws over the HUD outside its interior designer.
That the turntable's drag surface, the first object of the HUD's canvas, takes the drag where nothing of the HUD is in front of it.
