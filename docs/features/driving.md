# Driving

## In a sentence

The car the player drives gets the performance of its kind (power, gearing and drag worked out from a target per vehicle type), the handling of its kind (weight, a soft ride that swings and settles, body roll and pitch, steering by speed, tyres that slide, brakes that build without locking), an eased throttle and a speed reading in the game's own vehicle panel.

## Where the code is

- Folder: [`Scripts/Driving`](../../Assets/Mods/RealLifeOverhaul/Scripts/Driving/).
- Read first: `CarCatalogue.cs` (the targets for every vehicle type, game-free), `CarModel.cs` (the physics model that finds power, gearing and drag) and `CarTuning.cs` (applies it to the driven car and puts every value back).

| File | What it is |
| --- | --- |
| `CarCatalogue` | every vehicle type's targets (0-100 km/h time, top speed) and kind, and the rule for unknown types (game-free) |
| `CarModel` | a car in a straight line as the game's physics moves it; finds the power, gearing and drag that meet a target (game-free) |
| `CarTuning` | reads the driven car, applies what CarModel found, puts every value back; the eased pedal |
| `CarDrag` | air drag on the driven car, one force per physics step, only while a tuned car is driven |
| `HandlingRules` | the handling's targets by kind and level, the six tuning sliders, the eased brake pedal (game-free) |
| `HandlingModel` | the handling as the game's vehicle physics works it: the quarter car, roll, pitch, tipping, the tyres, the steering by speed, the brakes, and the Garage's springs on top (game-free) |
| `CarHandling` | reads the driven car, puts its handling on, records every value and puts it back; the two check lines |
| `DrivingBridges` | what Driving asks of the Garage: its parts lifted off a car and laid back on top |
| `CarMeasure` | the "Measure this car" button: times a full-throttle run, off otherwise |
| `Sound/*` | the recorded car sounds of the Garage's option: see [Garage](garage.md#recorded-car-sounds) |

## How it talks to other features

- The Garage's performance parts tune a car through `CarModel` and a hook in `CarTuning` (`Garage/GarageTuning`); see [Garage](garage.md).
- Traffic is never tuned: only the car the player drives.
- The handling lies under the Garage's parts: `DrivingBridges.LiftParts` and `LayParts` (answered in `Wiring.DrivingLinks`) take the custom shop's springs, dampers, bars, weight, brakes and tyres off the car before the handling changes, and put them back on top after.
- Wet roads (`Events/Weather`) and worn tyres and brakes (`Transport/CarOwnership`) scale the tyres' grip and the brakes' torque on top of the handling as before: the handling changes neither figure.

## Cars: performance by kind

The game's cars are all 1400 kg (the Vord Pony 1200), share one four-speed gearbox and one engine curve, have no air drag, and are stopped by a speed limiter at the "max speed" of their vehicle type (50 to 100 km/h).

`Driving/CarTuning` gives the car being driven (never traffic) the performance of its kind instead: each vehicle type has a target in `CarCatalogue`, and `CarModel` works out from the car's own mass, wheels, grip, clutch and power curve the engine power, gear ratios and drag that meet it.

Top speed is where the engine's power in top gear equals drag plus rolling resistance (the limiter is switched off), so a car pulls hard to 50, steadily to 100 and builds slowly to its top speed.
Everything changed is recorded and put back when the player gets out, changes car, switches the option off or the mod unloads.

| vehicle type (`ba:vehicletype_`) | in game | kind | game: kW / final drive / limiter | target 0-100, top | applied: kW / final drive / drag (N per (m/s)²) | first gear ends | model: 0-50, 0-100, after 20 s |
|---|---|---|---|---|---|---|---|
| `honzamimic` | Honza Mimic | Economy | 45 / 8.51 / 60 | 12.5 s, 170 | 76 / 3.05 / 0.541 | 45 km/h | 4.2 s, 12.5 s, 124 km/h |
| `vordtiaravic` | Vord Tiara Vic | Saloon | 80 / 6.42 / 70 | 10.0 s, 190 | 107 / 2.73 / 0.587 | 45 | 4.4, 10.0, 143 |
| `bima320` | BIMA 320 | Saloon | 100 / 7.08 / 70 | 8.5 s, 210 | 125 / 2.75 / 0.525 | 45 | 3.8, 8.5, 158 |
| `mersaidis500` | Mersaidi S500 | Luxury | 175 / 6.76 / 80 | 6.5 s, 240 | 169 / 2.61 / 0.497 | 48 | 3.0, 6.5, 185 |
| `missamvillian` | Lytte L6 | Luxury | 175 / 8.51 / 80 | 6.2 s, 240 | 152 / 2.61 / 0.438 | 48 | 2.3, 6.2, 180 |
| `petrollsfanton` | Petrolls Fanton | Luxury | 175 / 7.61 / 80 | 6.8 s, 240 | 160 / 2.83 / 0.473 | 48 | 3.2, 6.8, 182 |
| `limo` | Petrolls Executive Limousine | Luxury | 75 / 8.00 / 80 | 10.5 s, 180 | 102 / 3.41 / 0.685 | 45 | 4.6, 10.5, 139 |
| `vordpony` | Vord Pony | Sports | 100 / 6.33 / 80 | 5.8 s, 240 | 163 / 2.36 / 0.468 | 48 | 2.8, 5.8, 194 |
| `ferdinand112` | Ferdinand 112 | Sports | 150 / 5.15 / 90 | 5.0 s, 265 | 217 / 2.14 / 0.477 | 53 | 2.3, 5.0, 212 |
| `mersaidimgagt` | Mersaidi MGA GT | Sports | 200 / 4.80 / 100 | 4.7 s, 280 | 234 / 2.02 / 0.438 | 56 | 2.2, 4.7, 222 |
| `anselmoaf90` | Anselmo AF90 | Sports | 200 / 4.80 / 100 | 4.5 s, 290 | 247 / 2.05 / 0.419 | 58 | 2.2, 4.5, 229 |
| `umcnunavut` | UMC Nunavut | Utility | 150 / 7.92 / 80 | 9.0 s, 190 | 119 / 3.60 / 0.705 | 45 | 4.0, 9.0, 150 |
| `vordv150` | Vord V150 | Utility | 130 / 7.62 / 80 | 10.0 s, 180 | 107 / 3.80 / 0.740 | 45 | 4.4, 10.0, 142 |
| `umcdesert`, `umcdesertdeliveryjob` | UMC Desert, Delivery Van | Van | 55 / 11.14 / 50 | 16.0 s, 140 | 66 / 4.89 / 0.924 | 45 | 4.9, 16.0, 110 |
| `mersaididash` | Mersaidi Dash | Van | 55 / 11.14 / 65 | 14.5 s, 150 | 71 / 4.56 / 0.804 | 45 | 4.6, 14.5, 116 |
| `deliverytruck` | Vord Courier D500 | Truck | 46 / 15.25 / 55 | 18.0 s, 135 | 57 / 5.41 / 1.106 | 45 | 6.7, 18.0, 105 |
| `freighttruckt1` | Freight Truck T1 | Truck | 35 / 15.25 / 50 | 20.0 s, 130 | 52 / 7.09 / 1.146 | 45 | 7.3, 20.0, 100 |
| `electricscooter`, `handtruck`, `flatbed` | | Untouched | | as the game made them | | | |

The "applied" and "model" columns are what `CarModel` gives for the game's own vehicle data (read from the game's files with a small command-line check, at both sliders 100 % and with the eased throttle).
The mod works the same sums out again from the live car each time the player gets in, and writes them to the log once per car ("Car tuning: ...").

They are a model of the physics, not a measurement: the options button "Measure this car" times a real run for comparison.

- **A type the table does not know** (a car from another mod) is placed by `CarCatalogue.Guess`: no fuel tank, carried in the hands or under 600 kg: untouched.
  The game's own max speed up to 55: truck (18 s, 135).
  Up to 65: van (15 s, 145) with cargo room for 14 or more, else economy (12 s, 170).
  Cargo 16 or more: utility (10 s, 180).

  Max speed 95 or more: sports (4.8 s, 270), 85 or more: sports (5.5 s, 250).
  Otherwise luxury (6.5 s, 240) if flagged so, else saloon (9 s, 200).
  To give it its own figures, add one `case` to `CarCatalogue.For`.

- **Cars of other mods with their own row**, by their whole type names (their data is in their own asset files, not read here, so the "applied" and "model" columns are worked out in the game only): Empire Motors' two car add-ons, sold at the game's dealership through that mod's desk (docs/other-mods.md), `empiremustanggt:vehicletype_mustanggt` (Mustang GT, Sports, 4.5 s, 250) and `empirebmwg82:vehicletype_empirebmwg82` (BMW G82, Sports, 4.0 s, 250).

  Without their rows they were placed by the game's own max speed they carry, which the guess cannot be sure of.

- **The two sliders** (70 to 150 %) scale the top speed and divide the 0-100 time.
  Top speed is never taken past 350 km/h; with a top speed turned down below 125 km/h the power is found as if for 125 and the car is then geared and dragged down to what was asked.
- **Gearing**: top gear turns the engine at 95 % of its limit at top speed; first gear runs out at a fifth of top speed (45 to 58 km/h), never so short that the engine's best torque spins the tyres; the gears between are evenly spaced.

  Driven gently the box changes up at 45 to 58 % of the rev limit by kind, with the pedal down at 95 %; a change takes 0.12 s (sports) to 0.45 s (trucks), less with the custom shop's gear change parts (see [Garage](garage.md#performance-parts)).

- **The eased throttle** ("Smooth throttle"): the car's own input handler calls `CarTuning.EasePedal` once a frame, which moves the pedal towards what the key asks: 0 to full in 0.7 s, slowly at first, and back in 0.15 s.
  A tap gives a gentle pull-away; an analogue trigger keeps its own feel.
  The brakes come on over 0.25 s through the physics' own `brakes.actuationTime`; with car handling on, the brake key is eased as well (Handling, below).

  In reverse the game swaps the pedals, and the one that drives is the one eased.

- **Brakes** are left alone by car tuning: with the game's tyres they stop a car from 100 km/h in 19 to 56 m and from its new top speed in 66 to 122 m. Car handling has brakes of its own (below).
- A car the sums cannot be done for (no combustion engine, no automatic gearbox, missing wheels or curve) is left exactly as the game made it, with one line in the log saying why.

## Handling

**What the game's own cars start from** (read from the game's files: each car's prefab and each vehicle type). The cars weigh 1400 kg (the Vord Pony 1200), with the centre of mass 0.2 to 0.4 m above the road (the vans 0.66 and 0.71). Every car's tyres are the game's ArcadeTireFrictionPreset (B 7.09, C 0.87, D 2, E 0.5), grip 1.3 front and 1.5 rear, slip stiffness 1.27 and 1.7: its curve never peaks and reaches 1.84 sideways on, about 4.5 g. The game's ground detection gives the asphalt preset only on a collider tagged "Road", a tag the game does not have, or on one texture of two terrains. The springs run from 5 000 N over 0.6 m to 19 300 N over 0.2 m (0.8 to 2.9 Hz; straight, but for the Mersaidi Dash's rising curve), every damper 5 000 N s/m, 1.4 times that in bump below 0.06 m/s and 1.6 in rebound below 0.05 m/s, 0.6 above: most cars never swing past rest after a bump, the stiffly sprung ones (Ferdinand 112, Mersaidi S500, Vord Pony) swing once. Every axle has a 4 200 N/m anti-roll bar and applies its tyres' forces 0.2 to 0.8 of the spring's length above the road. The rear brakes get 0.184 of the front's torque and there is no ABS, so the fronts of the cars with strong brakes lock, still pulling their most. The steering's lock falls to half (to 0.4 on the vans, trucks and older cars) by 54 km/h and to a fifth by 180 km/h, and the game's Ackermann sum turns the front wheels further than the lock on a short car. Each vehicle type has its own top speed, 50 to 100 km/h. With the key held the game's cars turn hard: the Anselmo AF90 at 2.4 g at 50 km/h and 3.4 g at 100.

**0.39.0 to 0.40.4, and now.** From 0.39.0 the handling replaced those values with figures of its own: tyres holding about 1.1 g (a quarter of the game's) on a slip stiffness of 1, soft springs damped at about 0.26 with no anti-roll bars, the centre of mass raised. A turn the game's car takes at a speed came out about twice as wide, so a player turning as they always had left the road, and the body floated. Now the handling is the car's own game values, read when the player gets in, with a little added (`HandlingModel.Design`):

| what | the game's own, read from the car | Light, Full, Heavy |
|---|---|---|
| weight | the body's mass (`Rigidbody.mass`, `VariableCenterOfMass.baseMass`) and inertia | a share of the way to what its kind weighs (`HandlingRules.KindMass`), 0.1, 0.25 and 0.5, never lighter than the game made it; the inertia scaled with the mass |
| springs, bars, tyres' ratings | each wheel's spring (force x its own curve of the compression), each axle's anti-roll bar, each tyre's load rating | scaled with the weight, so the car stands as high and rides at the same frequency; the springs' curves stay the game's |
| the centre of mass | `Rigidbody.centerOfMass` | the game's own |
| damping | the game's two-rate dampers | their shape kept; the rates set so that the first swing past rest after a 3 cm push (half the travel if less) gives a damping ratio of 0.55 at Full and 0.42 at Heavy, or the game's own where that is lighter; Light keeps the game's own |
| roll | the force points' height, the springs and the bars | the game's own roll per g and 0.15, 0.4 or 0.8 degrees more, from the force points brought down (15 cm at most, never below 2 cm), as far as rolling over allows |
| nose dive | the force points and the anti-squat figure | the game's own dive per g times 1.05, 1.2 or 1.4, by anti-squat |
| the tyres | the car's friction preset and slip stiffness | the game's own curve exactly up to the knee, flat past it: the give at the very limit. The knee lies 15% past the furthest any of the game's own car's tyres goes in its hardest turn that still holds the road (with the key held, at the type's own top speed, or the fastest below it at which the game's car keeps all four wheels down and its front tyres under 20 degrees of slip), and at least at 28, 20 or 16 degrees of front slip angle; the game's slip stiffness and grip; a ground preset with no surfaces of its own, so a terrain never swaps the tyres |
| rolling over | | a sudden turn's swing counted, the car rolls over only at 1.2 times what its tyres hold (the custom shop's grippiest tyres give 1.2): where it would sooner, the tyres give sooner (never before the game's hardest turn), and where even that is not enough the force points go up, which takes some of the roll away |
| steering | the speed curve, its smoothing, the lock | the game's own |
| brakes | the torque, the axles' shares, the game's ramp | the game's own full pedal on that car (the lower of its torque and its tyres), 0.92 of it at Heavy, both axles inside their tyres at their worst moment as the body dives (at most 0.95 then and 0.93 once settled), never so hard that the rear keeps less than 40% of its load; the game's own ABS module put on the car; the brake key eased from nothing to full in 0.15, 0.25 or 0.4 s |

So every turn the game's own car takes with the key held, up to its own top speed while it still holds the road, the car takes at the same radius (at most 3% wider: the little roll added moves a little weight out), its inner wheels keeping at least a tenth of their load, and the 9 m right turn, 15 m left turn and 30 m bend of the city's junctions and avenues at the speed the game's car takes them. Past that the tyres give a little more than the game's, and a tall van or truck slides before it would tip, which the game's own Mersaidi Dash, Lytte L6 and Vord Pony do not at their own top speed. The custom shop's parts go on top: a stiffer spring stays stiffer for its weight, the weight part's lighter car keeps its ride height and frequency, and on the grippiest tyres the car still slides before it rolls over.

Every one of the game's cars at Full (the game's own, then the handling's; "its hardest holding turn" is the turn with the key held at the speed the knee is measured at; NavCheck prints every level with `--handling`). The last six rows are cars whose figures the model cannot read beforehand, at assumed values: Empire Motors' two cars as built on the game's own sports car (the Mersaidi MGA GT's wheels, springs, tyres and steering at 100 km/h), and four cars at the edges of what another mod might bring. In the game the handling reads every car's own values when the player gets in, whatever they are.

| car | kind | kg | ride Hz | damping ratio | roll per g | force points up | dive per g | rolls over at | tyres hold, give past | its hardest holding turn | full pedal | from 100 km/h |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Honza Mimic | Economy | 1400, 1400 | 0.79 | 1.00, 0.55 | 4.57, 4.69 degrees | 0.12, 0.12 m | 2.76, 3.31 | 3.4, 3.3 g | 4.5, 2.7 g past 11 degrees | 2.15 g at 60 km/h | 1.44, 1.44 g | 30 m |
| Vord Tiara Vic | Saloon | 1400, 1438 | 0.80 | 1.00, 0.55 | 4.31, 4.16 degrees | 0.12, 0.13 m | 2.68, 3.21 | 3.6, 3.7 g | 4.5, 3.1 g past 14 degrees | 2.48 g at 70 km/h | 1.79, 1.79 g | 25 m |
| BIMA 320 | Saloon | 1400, 1438 | 1.62 | 1.00, 0.55 | 0.79, 1.19 degrees | 0.23, 0.18 m | 0.31, 0.37 | 7.4, 4.9 g | 4.5, 3.6 g past 20 degrees | 2.91 g at 70 km/h | 1.67, 1.67 g | 27 m |
| Mersaidi S500 | Luxury | 1400, 1562 | 2.77 | 0.37, 0.37 | 0.46, 0.58 degrees | 0.16, 0.12 m | 0.13, 0.16 | 5.0, 4.0 g | 4.5, 3.3 g past 16 degrees | 2.83 g at 80 km/h | 2.10, 2.10 g | 22 m |
| Lytte L6 | Luxury | 1400, 1562 | 1.42 | 1.00, 0.55 | 2.69, 2.19 degrees | 0.04, 0.09 m | 1.09, 1.31 | 2.8, 3.4 g | 4.5, 2.8 g past 11 degrees | 2.10 g at 60 km/h | 2.39, 2.14 g | 22 m |
| Petrolls Fanton | Luxury | 1400, 1562 | 0.84 | 1.00, 0.55 | 1.46, 1.86 degrees | 0.24, 0.22 m | 0.56, 0.67 | 9.0, 7.1 g | 4.5, 3.5 g past 20 degrees | 2.69 g at 80 km/h | 2.05, 2.05 g | 23 m |
| Petrolls Executive Limousine | Luxury | 1400, 1562 | 0.81 | 1.00, 0.55 | 0.90, 1.30 degrees | 0.24, 0.22 m | 0.27, 0.33 | 15.5, 10.7 g | 4.5, 3.5 g past 20 degrees | 2.35 g at 80 km/h | 1.78, 1.78 g | 25 m |
| Vord Pony | Sports | 1200, 1288 | 2.88 | 0.39, 0.39 | 0.65, 0.49 degrees | 0.16, 0.22 m | 0.22, 0.27 | 3.2, 4.2 g | 4.5, 3.5 g past 18 degrees | 2.83 g at 60 km/h | 2.20, 2.20 g | 21 m |
| Ferdinand 112 | Sports | 1400, 1438 | 2.76 | 0.37, 0.37 | 0.41, 0.53 degrees | 0.16, 0.12 m | 0.20, 0.24 | 5.8, 4.4 g | 4.5, 3.7 g past 22 degrees | 3.26 g at 90 km/h | 1.97, 1.97 g | 23 m |
| Mersaidi MGA GT | Sports | 1400, 1438 | 1.74 | 1.00, 0.55 | 0.64, 1.03 degrees | 0.20, 0.14 m | 0.30, 0.36 | 7.8, 4.7 g | 4.5, 3.7 g past 24 degrees | 3.42 g at 100 km/h | 2.20, 2.20 g | 21 m |
| Anselmo AF90 | Sports | 1400, 1438 | 1.71 | 0.91, 0.55 | 0.74, 1.11 degrees | 0.20, 0.14 m | 0.34, 0.41 | 6.7, 4.5 g | 4.5, 3.7 g past 24 degrees | 3.38 g at 100 km/h | 2.23, 2.23 g | 21 m |
| UMC Nunavut | Utility | 1400, 1638 | 1.14 | 1.00, 0.55 | -1.17, 0.33 degrees | 0.32, 0.17 m | 1.17, 1.40 | 99.0, 27.0 g | 4.5, 3.5 g past 20 degrees | 3.05 g at 80 km/h | 1.98, 1.98 g | 23 m |
| Vord V150 | Utility | 1400, 1638 | 0.76 | 1.00, 0.55 | 4.38, 3.85 degrees | 0.12, 0.14 m | 1.98, 2.37 | 3.5, 3.9 g | 4.3, 3.3 g past 16 degrees | 2.59 g at 80 km/h | 1.73, 1.73 g | 26 m |
| UMC Desert | Van | 1400, 1650 | 0.78 | 1.00, 0.55 | 6.50, 6.24 degrees | 0.30, 0.31 m | 2.94, 3.52 | 2.1, 2.2 g | 4.4, 1.8 g past 6 degrees | 1.27 g at 50 km/h | 1.09, 1.09 g | 39 m |
| Delivery Van | Van | 1400, 1650 | 0.78 | 1.00, 0.55 | 6.50, 6.24 degrees | 0.30, 0.31 m | 2.94, 3.52 | 2.1, 2.2 g | 4.4, 1.8 g past 6 degrees | 1.27 g at 50 km/h | 1.09, 1.09 g | 39 m |
| Mersaidi Dash | Van | 1400, 1650 | 1.16 | 1.00, 0.55 | 5.41, 3.76 degrees | 0.20, 0.35 m | 1.30, 1.56 | 1.5, 2.2 g | 3.8, 1.8 g past 6 degrees | 1.16 g at 60 km/h | 1.09, 1.09 g | 39 m |
| Vord Courier D500 | Truck | 1400, 1800 | 0.78 | 1.00, 0.55 | 4.05, 4.45 degrees | 0.12, 0.09 m | 1.65, 1.98 | 3.1, 2.8 g | 4.4, 2.3 g past 8 degrees | 1.48 g at 55 km/h | 1.02, 1.02 g | 41 m |
| Freight Truck T1 | Truck | 1400, 1800 | 0.81 | 1.00, 0.55 | 4.69, 4.93 degrees | 0.12, 0.10 m | 0.84, 1.01 | 2.4, 2.2 g | 4.5, 1.9 g past 6 degrees | 0.80 g at 50 km/h | 0.81, 0.81 g | 51 m |
| Empire Motors' muscle coupe (assumed) | Sports | 1400, 1438 | 1.75 | 1.00, 0.55 | 0.78, 1.13 degrees | 0.20, 0.15 m | 0.32, 0.39 | 6.4, 4.4 g | 4.5, 3.7 g past 23 degrees | 3.31 g at 100 km/h | 2.16, 2.16 g | 22 m |
| Empire Motors' sports coupe (assumed) | Sports | 1400, 1438 | 1.69 | 0.87, 0.55 | 0.72, 1.12 degrees | 0.20, 0.14 m | 0.28, 0.33 | 7.0, 4.5 g | 4.5, 3.6 g past 21 degrees | 3.20 g at 100 km/h | 2.26, 2.26 g | 21 m |
| another mod's tall van (assumed) | Van | 1900, 2025 | 1.02 | 1.00, 0.55 | 17.34, 4.75 degrees | 0.18, 0.76 m | 3.59, 2.72 | 0.7, 2.5 g | 3.2, 1.8 g past 8 degrees | 0.89 g at 40 km/h | 1.02, 1.02 g | 42 m |
| another mod's low coupe (assumed) | Sports | 1300, 1362 | 2.79 | 0.36, 0.36 | 0.22, 0.48 degrees | 0.14, 0.04 m | 0.10, 0.12 | 9.6, 4.4 g | 4.5, 3.6 g past 21 degrees | 3.35 g at 120 km/h | 2.10, 2.10 g | 22 m |
| another mod's heavy truck (assumed) | Truck | 4000, 4000 | 1.19 | 0.33, 0.33 | 6.05, 3.11 degrees | 0.12, 0.43 m | 1.01, 1.21 | 1.4, 2.6 g | 1.5, 1.3 g past 10 degrees | 0.90 g at 50 km/h | 0.49, 0.49 g | 83 m |
| another mod's city car (assumed) | Economy | 900, 988 | 0.92 | 1.00, 0.55 | 3.05, 3.01 degrees | 0.14, 0.14 m | 2.24, 2.69 | 3.6, 3.6 g | 4.5, 3.0 g past 12 degrees | 2.52 g at 55 km/h | 1.90, 1.90 g | 24 m |

Where the game's own car would roll over before its tyres let go (most of the softly sprung cars, the vans and the trucks), the tyres give sooner, past the game's hardest holding turn; where even that is not enough (the Mersaidi Dash, the Lytte L6 and the Vord Pony, and a little the UMC Desert, the Vord Tiara Vic and the Vord V150) the force points go up and the car leans a little less than the game's own. Where the springs are very stiff (the Ferdinand 112, the Mersaidi S500, the Vord Pony) the force points would have to go below the road for much more roll, so they roll only a little more than the game's. A heavier level rolls at least as much as a lighter one, except where rolling over holds it back (the fast sports cars, whose own turns reach 3.3 g).

**The custom shop's parts on top.** The Garage's parts act on the handling as they act on the game's own car: the springs part makes the spring its factor stiffer on its own curve and stands the car the part's height lower or higher at rest (`HandlingModel.FitSpring`), never into the 12 cm of travel a kerb needs; the weight part carries a lighter car at the same height and frequency; the dampers follow the square root of both factors, so the damping ratio stays; the force points move with the body; the bars take the factor; the brakes part the torque, only as far as the tyres take it (the axles' shares are set again for the parts on top); the tyres part the grip. NavCheck runs every car at every level with every build of springs, weight and tyres, wet and dry: still well damped, a stiffer spring stiffer for its weight, and sliding before rolling over even on the grippiest tyres. The custom shop's stopping figure is the handling's own model with the build on top (`HandlingModel.ShopStop`); NavCheck steps the car braking wheel by wheel, each corner's dive counted, for every car and level with every build of brakes, weight, tyres and springs at both of the game's onsets: the two agree within a metre, and no wheel asks more than its tyre holds.

**Every value comes back.** Everything is recorded first (the mass and inertia, every wheel's spring force and length, dampers' rates, force point, anti-squat, load rating, rolling resistance and tyres, each axle's bar and brake share, the two steering curves, the ground preset, the ABS module) and put back exactly when the player gets out or changes car, when the option or a slider changes, on a fault, when the driving feature is cleaned up and when the city goes (`CarTuning.Restore`), with the Garage's parts lifted off around it and laid back on top. The springs' curves, the dampers' shape, the tyres' slip stiffness and the centre of mass are never changed. The power, gearing and drag are worked out after the handling is on (for the new weight) and taken off before it.

**Testing.** With Developer diagnostics on, getting in writes one line to Player.log, "Handling check (getting in): ...": the kind and level; the weight, centre of mass, springs, ride, damping ratio, a kerb's travel, roll, force points, bars, dive, rolling over, what the tyres hold and where they give, the hardest turn they were measured at, steering and brakes against the game's own; then PASS, or what did not take. What the handling wrote is read back before the Garage's parts go on top of it, so a springs part on the car is never reported as a fault (0.40.4 said "the springs or tyres did not take" whenever the custom shop's parts re-fitted the springs on top). Getting out writes "Handling check (getting out): ...: PASS" once every value reads back as the game had it. The test button "Say what the car you drive has from its handling" gives a short notice, and the full line in Player.log whatever the diagnostics say. With the test tools shown, the options page's Troubleshooting has "Car handling tuning": six sliders from 50 to 150% (spring softness, bounce, body roll, tyre grip, steering at speed, how quickly the brakes build), around the level's figures as before: 100% is now the game's own springs, damping, grip and steering with the level's additions on top. A saved level and saved sliders keep working as they are.

Not seen in the game yet: how the turns, the little roll and the give past the limit feel against the model; that the game's ABS module, put on while the car runs, eases the brakes as on a car built with it; a car on the two terrains; Empire Motors' cars and other mods' cars, whose values the checks can only assume.
