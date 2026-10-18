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

  Driven gently the box changes up at 45 to 58 % of the rev limit by kind, with the pedal down at 95 %; a change takes 0.12 s (sports) to 0.45 s (trucks).

- **The eased throttle** ("Smooth throttle"): the car's own input handler calls `CarTuning.EasePedal` once a frame, which moves the pedal towards what the key asks: 0 to full in 0.7 s, slowly at first, and back in 0.15 s.
  A tap gives a gentle pull-away; an analogue trigger keeps its own feel.
  The brakes come on over 0.25 s through the physics' own `brakes.actuationTime`; with car handling on, the brake key is eased as well (Handling, below).

  In reverse the game swaps the pedals, and the one that drives is the one eased.

- **Brakes** are left alone by car tuning: with the game's tyres they stop a car from 100 km/h in 19 to 56 m and from its new top speed in 66 to 122 m. Car handling has brakes of its own (below).
- A car the sums cannot be done for (no combustion engine, no automatic gearbox, missing wheels or curve) is left exactly as the game made it, with one line in the log saying why.

## Handling

The game's cars all weigh 1400 kg (the Vord Pony 1200), with the centre of mass 0.2 to 0.4 m above the road (the vans 0.66 and 0.77). Their tyres keep the arcade friction preset their prefabs were saved with: the game's ground detection gives the asphalt preset only on a collider tagged "Road", a tag the game does not have, or on one texture of two terrains, so the curve every car drives on keeps rising with slip and never peaks, up to about 4.5 g to the side. The springs run from 5 000 N over 0.6 m to 19 300 N over 0.2 m (a ride of 0.8 to 2.9 Hz) and every damper is the same 5 000 N s/m, 1.4 times that in bump and 1.6 in rebound below 0.05 m/s: a damping ratio of 0.6 to 2.1, so a body never swings. The tyres' forces act 0.2 to 0.8 of the spring's length above the road, so most cars hardly roll or pitch. The rear brakes get 0.18 of the front's torque, there is no ABS, and the steering's lock falls to half by 54 km/h and to a fifth by 180 km/h.

With "Car handling" on, `CarHandling` gives the car the player drives (never traffic, the bus, the tow truck, a parked car or another player's car in a Going Public session) the handling of its kind (`CarCatalogue`), worked out by `HandlingModel` from the car's own axles, tracks, spring tops, wheels and centre of mass, on the game's own vehicle physics (NWH Vehicle Physics 2):

| what | made of, in the game's physics | what the handling sets |
|---|---|---|
| weight | the body's mass (`Rigidbody.mass`, `VariableCenterOfMass.baseMass`), its inertia, the centre of mass | the kind's mass; the centre of mass raised to the kind's height where the game's is lower; the inertia scaled with the mass if the engine does not |
| the wobble | each wheel's spring (force = `maxForce` x its curve of the compression) and two-rate damper (bump and rebound rates, a factor below a knee speed and one above) | a spring for the kind's ride frequency on the corner's own load, as long as the game's rest height plus the sag (at least 12 cm of bump travel), with a bump stop in its last 35% (3 then 9 times the rate); dampers for the damping ratio, rebound 1.6 times bump, the full rate up to 0.2 m/s and half of it above |
| roll and pitch | the tyres' forces applied `forceApplicationPointDistance` x the spring's length above the road; the anti-roll bars (`WheelGroup.antiRollBarForce`, a force from the two springs' difference); the anti-squat torque | the roll centre at the kind's height; the bars for the kind's roll per g, from the centre of mass's height, the tracks and the springs; anti-squat for its dive per g |
| the tyres | the friction preset's curve of the slip for the side force, its D figure for the most along, the friction circle between them | a preset of the mod's for each axle: the magic formula peaking at the kind's slip angle (the rear a tenth later) and keeping 93% of its peak far past it; its level for the kind's grip on the game's own front grip, the rear's from the car's own grip ratio with 4% more; a ground preset with no surfaces of its own, so the two terrains never put the game's asphalt tyres on the car |
| the turns | `Steering`: the lock x a curve of the speed x the key, smoothed by a time from a second curve | full lock at walking pace; at speed the bend the front tyres hold at their peak, plus 1.1 times their peak slip angle; the smoothing from the game's 0.05 s at rest to 0.2 s at 180 km/h |
| the brakes | `Brakes`: the pedal ramped over `actuationTime`, times `maxTorque` and each axle's `brakeCoefficient`; the game's ABS module (`ABSModuleWrapper`) | each axle's share for the kind's full-pedal deceleration (0.85 to 0.92 g for cars, 0.75 for vans, 0.65 for trucks), both axles asking the same share of their tyres at their own worst moment as the body dives (the front's load lags the brakes at first, the rear's swings past its settled share), 0.98 at most then and 0.95 once settled, each axle's corner stepped as the game steps it; the game's own ABS module put on the car for the wet; the brake key eased like the throttle, from nothing to full in 0.5 s, slow at first |
| momentum | the wheels' rolling resistance, the tyres' load rating | rolling resistance of 1.2% of the load; the load rating scaled with the weight |

The kinds at the whole feel (`HandlingRules`):

| kind | kg | centre of mass, at least | ride | damping ratio | roll per g | dive per g | roll centre | grip to the side, along | peak slip | brakes, full pedal |
|---|---|---|---|---|---|---|---|---|---|---|
| Economy | 1250 | 0.50 m | 1.25 Hz | 0.24 | 6.5 degrees | 2.4 | 0.05 m | 0.95 g, 1.00 g | 12 degrees | 0.86 g |
| Saloon | 1550 | 0.52 | 1.20 | 0.24 | 6.0 | 2.4 | 0.06 | 1.00, 1.00 | 12 | 0.88 |
| Luxury | 2050 | 0.52 | 1.05 | 0.23 | 6.5 | 2.6 | 0.08 | 1.00, 1.00 | 13 | 0.88 |
| Sports | 1550 | 0.45 | 1.45 | 0.26 | 4.5 | 1.8 | 0.05 | 1.12, 1.10 | 10 | 0.92 |
| Utility | 2350 | 0.62 | 1.15 | 0.24 | 7.5 | 2.6 | 0.14 | 0.90, 0.95 | 14 | 0.85 |
| Van | 2400 | 0.75 | 1.20 | 0.24 | 7.0 | 2.6 | 0.25 | 0.85, 0.95 | 14 | 0.75 |
| Truck | 3000 | 0.80 | 1.25 | 0.24 | 6.5 | 2.2 | 0.22 | 0.80, 0.85 | 15 | 0.65 |

Where the springs alone already hold the body stiffer than the roll asked for (the sports cars, the freight truck), the roll centre comes down towards the road instead of a bar going below nothing; where even that cannot do it, the car rolls less. Anti-squat stays between -1 and 1, so a long truck pitches less than asked.

The option's levels: **Light** (the Relaxed preset) has half the extra weight and half the raise of the centre of mass, 12% firmer springs, 0.03 more damping (0.27 at most), 0.65 of the roll and dive, 12% more grip to the side and 10% along, the rear 8% grippier, the peaks 10% sooner, the full pedal a seventh more of its grippier tyres (as far as 0.98 of them at the worst moment allows) and built in 0.4 s. **Full** (the default and the Realistic preset) is as above. **Heavy** (the Hardcore preset) is 8% heavier, 4% softer, 0.03 less damped (0.21 at least), 10% more roll and dive, 8% less grip to the side and 5% along, the rear only 1.5% grippier (lifting off brings the tail round), the peaks 12% later, the full pedal a fourteenth less of its slidier tyres and built in 0.6 s, the steering reaching 1.2 times the peak slip. **Off** leaves every car as the game made it.

On the game's own cars at the whole feel (the game's own, then the handling's):

| car | kind | kg | centre of mass up | ride, front | damping ratio | sag, of the spring | roll per g | dive per g | grip to the side | brakes, full pedal |
|---|---|---|---|---|---|---|---|---|---|---|
| Honza Mimic | Economy | 1400, 1250 | 0.32, 0.50 m | 0.79, 1.25 Hz | 2.09, 0.25 | 0.66, 0.44 | 4.6, 6.5 degrees | 2.8, 2.4 | 4.5, 0.95 g | 1.41, 0.86 g |
| Vord Tiara Vic | Saloon | 1400, 1550 | 0.31, 0.52 | 0.80, 1.20 | 2.10, 0.25 | 0.65, 0.46 | 4.3, 6.0 | 2.7, 2.4 | 4.5, 1.00 | 1.98, 0.88 |
| BIMA 320 | Saloon | 1400, 1550 | 0.34, 0.52 | 1.62, 1.20 | 1.07, 0.25 | 0.31, 0.46 | 0.8, 6.0 | 0.3, 2.4 | 4.5, 1.00 | 2.54, 0.88 |
| Mersaidi S500 | Luxury | 1400, 2050 | 0.32, 0.52 | 2.77, 1.05 | 0.63, 0.23 | 0.16, 0.57 | 0.5, 6.5 | 0.1, 2.6 | 4.5, 1.00 | 3.32, 0.88 |
| Lytte L6 | Luxury | 1400, 2050 | 0.31, 0.52 | 1.42, 1.05 | 1.25, 0.23 | 0.62, 0.65 | 2.7, 6.5 | 1.1, 2.6 | 4.5, 1.00 | 3.32, 0.88 |
| Petrolls Fanton | Luxury | 1400, 2050 | 0.33, 0.52 | 0.84, 1.05 | 1.85, 0.23 | 0.70, 0.60 | 1.5, 6.5 | 0.6, 2.6 | 4.5, 1.00 | 3.22, 0.88 |
| Petrolls Executive Limousine | Luxury | 1400, 2050 | 0.29, 0.52 | 0.81, 1.05 | 1.78, 0.23 | 0.76, 0.65 | 0.9, 6.5 | 0.3, 2.6 | 4.5, 1.00 | 2.39, 0.88 |
| Vord Pony | Sports | 1200, 1550 | 0.41, 0.45 | 2.88, 1.45 | 0.66, 0.27 | 0.15, 0.41 | 0.6, 4.5 | 0.2, 1.8 | 4.5, 1.12 | 3.26, 0.92 |
| Ferdinand 112 | Sports | 1400, 1550 | 0.30, 0.45 | 2.76, 1.45 | 0.63, 0.27 | 0.16, 0.41 | 0.4, 4.5 | 0.2, 1.8 | 4.5, 1.12 | 3.31, 0.92 |
| Mersaidi MGA GT | Sports | 1400, 1550 | 0.31, 0.45 | 1.74, 1.45 | 0.95, 0.27 | 0.33, 0.41 | 0.6, 4.5 | 0.3, 1.8 | 4.5, 1.12 | 3.27, 0.92 |
| Anselmo AF90 | Sports | 1400, 1550 | 0.32, 0.45 | 1.71, 1.45 | 0.94, 0.27 | 0.34, 0.42 | 0.7, 4.5 | 0.3, 1.8 | 4.5, 1.12 | 3.24, 0.92 |
| UMC Nunavut | Utility | 1400, 2350 | 0.20, 0.62 | 1.13, 1.15 | 1.43, 0.25 | 0.48, 0.48 | -1.2, 7.5 | 1.2, 2.6 | 4.5, 0.90 | 2.57, 0.85 |
| Vord V150 | Utility | 1400, 2350 | 0.33, 0.62 | 0.76, 1.15 | 2.00, 0.25 | 0.72, 0.53 | 4.4, 7.5 | 2.0, 2.6 | 4.5, 0.90 | 1.71, 0.85 |
| UMC Desert, Delivery Van | Van | 1400, 2400 | 0.66, 0.75 | 0.78, 1.20 | 2.05, 0.25 | 0.69, 0.48 | 6.5, 7.0 | 2.9, 2.6 | 4.5, 0.85 | 1.07, 0.75 |
| Mersaidi Dash | Van | 1400, 2400 | 0.77, 0.79 | 0.87, 1.20 | 1.52, 0.25 | 0.83, 0.59 | 8.5, 7.0 | 2.2, 2.5 | 4.5, 0.85 | 1.07, 0.68 |
| Vord Courier D500 | Truck | 1400, 3000 | 0.41, 0.80 | 0.78, 1.25 | 2.06, 0.25 | 0.68, 0.45 | 4.0, 6.5 | 1.6, 2.2 | 4.5, 0.80 | 1.00, 0.64 |
| Freight Truck T1 | Truck | 1400, 3000 | 0.54, 0.80 | 0.81, 1.25 | 2.13, 0.25 | 0.64, 0.42 | 4.7, 6.5 | 0.8, 1.0 | 4.5, 0.80 | 0.79, 0.62 |

The game's damping ratio is its low-speed bump damping against the spring and the corner's mass; the handling's is how fast the body's swings die away when the model steps a corner the way the game's physics does (0.23 to 0.27, where a road car is about 0.35 to 0.5): a 3 cm push swings past once or twice and is gone within two seconds. The Lytte L6 and the Mersaidi Dash stand 4 and 5 cm higher when driven than the game had them, for the 12 cm of bump travel a 10 cm kerb needs. "Brakes" is the full pedal's deceleration (the game's: the lower of its torque and its tyres). From 100 km/h a saloon takes 45 m with the full pedal on and 51 m from the moment the key goes down, the pedal building over half a second; a van 52 and 58 m; a truck 61 and 67 m. At Light a saloon brakes at 1.0 g, at Heavy at 0.78 g. The Mersaidi Dash is held to 0.68 g by the game's own brake torque (2 500 N m a wheel, all its front wheels are given, with its rear tyres at their limit at the deepest of its dive) and the freight truck to 0.62 g; the Garage's sports and race brakes take the Dash to 0.78 and 0.83 g.

**The custom shop's parts on top.** The Garage's parts act on the handling as they act on the game's own car: the springs part makes the spring its factor stiffer and stands the car the part's height lower or higher at rest (`HandlingModel.FitSpring`: a stiffer spring sinks less, so its length is chosen for the height), never into the 12 cm of travel a kerb needs; the dampers follow the square root of the springs' factor, so the damping ratio stays; the roll centre moves with the body; the bars take the factor; the weight part the weight; the brakes part the torque, only as far as the tyres take it (the axles' shares are set again for the parts on top, so on a saloon sports and race brakes alike stop it from 100 km/h about a metre shorter, and with semi-slick tyres as well 8 m shorter); the tyres part the grip. NavCheck runs every car at every level with every build of springs, weight and tyres, wet and dry: the damping ratio stays between 0.2 and 0.3, the sag inside the spring, a kerb leaves travel, a part's springs are stiffer than the handling's, and no car tips in a sudden turn at 1.1 times its grippiest tyres' grip. The custom shop's stopping figure is the handling's own model with the build on top (`HandlingModel.ShopStop`: the axles' brake shares, the tyres' grip along and the eased pedal's rise); NavCheck steps the car braking wheel by wheel, the brakes' onset and each corner's dive counted, for every car and level with every build of brakes, weight, tyres and springs at both of the game's onsets: the two agree within a metre (to the millimetre, as no wheel asks more than its tyre holds).

**Every value comes back.** Everything is recorded first (the mass, the centre of mass and inertia, every wheel's spring, curve, dampers, force point, anti-squat, load rating, rolling resistance, tyres and slip stiffness, each axle's bar and brake share, the two steering curves, the ground preset, the ABS module) and put back exactly when the player gets out or changes car, when the option or a slider changes, on a fault, when the driving feature is cleaned up and when the city goes (`CarTuning.Restore`), with the Garage's parts lifted off around it and laid back on top. The power, gearing and drag are worked out after the handling is on (for the new weight) and taken off before it.

**Testing.** With Developer diagnostics on, getting in writes one line to Player.log, "Handling check (getting in): ...": the kind and level; the weight, centre of mass, springs, damping ratio (designed, and as the swings die away), sag, bump travel and a kerb, roll, dive, tipping, tyres, steering and brakes against the game's own; then what the car has, read back (the spring curve, the tyres' peak, the steering curve, the ground preset, the ABS module, the inertia); and PASS, or why not. Getting out writes "Handling check (getting out): ...: PASS" once every value reads back as the game had it. The test button "Say what the car you drive has from its handling" gives a short notice, and the full line in Player.log whatever the diagnostics say. With the test tools shown, the options page's Troubleshooting has "Car handling tuning": six sliders from 50 to 150% (spring softness, bounce, body roll, tyre grip, steering at speed, how quickly the brakes build) that the car being driven takes at once; each change writes "Car handling tuning: ..." to Player.log.

Not seen in the game yet: how the swings, the roll and the slides feel against the model; whether the engine scales the inertia with the mass (the getting-in line says which way it went); that the game's ABS module, put on while the car runs, eases the brakes as on a car built with it; a car on the two terrains.
