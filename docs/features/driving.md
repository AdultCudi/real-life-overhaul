# Driving

## In a sentence

The car the player drives gets the performance of its kind (power, gearing and drag worked out from a target per vehicle type), an eased throttle and a speed reading in the game's own vehicle panel.

## Where the code is

- Folder: [`Scripts/Driving`](../../Assets/Mods/RealLifeOverhaul/Scripts/Driving/).
- Read first: `CarCatalogue.cs` (the targets for every vehicle type, game-free), `CarModel.cs` (the physics model that finds power, gearing and drag) and `CarTuning.cs` (applies it to the driven car and puts every value back).

| File | What it is |
| --- | --- |
| `CarCatalogue` | every vehicle type's targets (0-100 km/h time, top speed) and kind, and the rule for unknown types (game-free) |
| `CarModel` | a car in a straight line as the game's physics moves it; finds the power, gearing and drag that meet a target (game-free) |
| `CarTuning` | reads the driven car, applies what CarModel found, puts every value back; the eased pedal |
| `CarDrag` | air drag on the driven car, one force per physics step, only while a tuned car is driven |
| `CarMeasure` | the "Measure this car" button: times a full-throttle run, off otherwise |

## How it talks to other features

- The Garage's performance parts tune a car through `CarModel` and a hook in `CarTuning` (`Garage/GarageTuning`); see [Garage](garage.md).
- Traffic is never tuned: only the car the player drives.

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
  The brakes come on over 0.25 s through the physics' own `brakes.actuationTime`.

  In reverse the game swaps the pedals, and the one that drives is the one eased.

- **Brakes** are left alone: with the game's tyres they stop a car from 100 km/h in 19 to 56 m and from its new top speed in 66 to 122 m.
- A car the sums cannot be done for (no combustion engine, no automatic gearbox, missing wheels or curve) is left exactly as the game made it, with one line in the log saying why.
