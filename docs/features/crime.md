# Crime

## In a sentence

Crime near the player, by the player and against the player: street crimes played out with a cast and an arrest, crimes rolled off screen, and the police noticing the player's own offences.

## Where the code is

- Folder: [`Scripts/Crime`](../../Assets/Mods/RealLifeOverhaul/Scripts/Crime/), with the scenes in [`Scenes`](../../Assets/Mods/RealLifeOverhaul/Scripts/Crime/Scenes/).
- Read first: `CrimeCatalogue.cs` (every crime, its odds and lines; fines and odds in `CrimeLaw`), `CrimeDirector.cs` (one scene at a time), `CrimeScene.cs` (the base every scene shares) and `PoliceAttention.cs` (the player's own offences).

| File | What it is |
| --- | --- |
| `CrimeCatalogue` | every crime: where and when it can happen, weight, endings with odds, its lines; fines and odds (CrimeLaw) |
| `CrimeDeck` | deals crimes, endings and lines without repeats (game-free, like LifeEventDeck) |
| `CrimeDirector` | one scene at a time: when, which, ticking it, the marker, 911, the test buttons |
| `CrimeScene` | ICrimeScene and the base every scene shares: cast of four, steps, arrests, clearing up |
| `CrimeScenes` | which scene plays which catalogue entry: one line per crime |
| `Scenes/` | the scenes: FootChase, Questioning, Disturbance, Snatch, Mugging, Assault, DrugDeal, LoneOffender (graffiti, car break-in, drunk, vendor, panhandler, fare dodger), CarPursuit (traffic stop, reckless driver, car theft), ParkingTicket, OwnShopTheft, ShopIncident (shoplifter, hold-up), PlayerPickpocket, PlayerMugging |
| `ArrestSequence` | the one arrest every scene uses: hands up, cuffed, to the kerb, into a patrol car, away |
| `CuffPose` | hands behind the back, re-aimed after the animator each frame while (and only while) someone is cuffed |
| `Crime` | money, toasts and small helpers shared by all of the above |
| `CrimeCars` | finds a parked car to stand beside or an ordinary traffic car to take over |
| `Knockdown` | one of a scene's people knocked off their feet (RagdollSystem) and up again |
| `ShopFloor` | what indoor scenes read from the shop: doors, shelves, camera cover, the guard |
| `OffscreenCrimes` | overnight break-in, parked car broken into, scam call: rolled once a day, all capped |
| `PoliceAttention` | the player's own offences: seen or not, pulled over, ticket, pursuit, escape, custody |
| `PlayerOffences` | notices red lights, speeding, pavement and wrong-side driving, jaywalking and crossing on red (timed, player only), each only if its own switch is on, and on foot only while nothing carries the player |
| `OffenceRules` | game-free: the `Offence` kinds, which of them the police act on (`OffenceSwitches`), what becomes of the older single switch (`TakeOlderSwitch`), and when the player counts as carried (`RideFacts`) |
| `PlayerRide` | reads the game's signs that something carries the player, for the foot look |
| `RoadMap` | stop lines and lanes from the traffic system in a grid, built once per city load |

## Traffic offences: one switch each

The police act on eight kinds of traffic offence, each with its own option under **Traffic offences** on the options page (the section needs **Police**).
Each is on by default and in the presets (Relaxed off, Standard, Realistic and Hardcore on).

| Option id | Offence | Where it is looked for |
| --- | --- | --- |
| `offence_red_light` | running a red light | `PlayerOffences.Driving`, the stop line |
| `offence_speeding` | speeding | `PlayerOffences.Slow` |
| `offence_pavement` | driving fast on the pavement (`Offence.Reckless`) | `PlayerOffences.Slow` |
| `offence_wrong_way` | driving fast against the traffic (`Offence.WrongSide`) | `PlayerOffences.Slow` |
| `offence_drink` | driving after drinks | `PoliceAttention.Report` (Leisure) and `Breathalyse` at any stop |
| `offence_jaywalk` | walking in the road in front of an officer | `PlayerOffences.OnFoot` |
| `offence_cross_red` | crossing against the walk light in front of an officer | `PlayerOffences.OnFoot` |
| `offence_horn` | the horn at people with the right of way | `HornReactions.Police` |

- Every look takes the switches as a value (`Settings.Offences`, an `OffenceSwitches`), so a kind that is off is not looked for, in the first sighting and in the extra offences booked during a stop alike. With all of the looked-for kinds off nothing is read, and the road map is not built.
- `player_offences`, the one switch an older version had for all of them, is no longer an option. At the first read after the update its saved value is written as the saved value of each of the eight that has none of its own, and the key is deleted (`OffenceRules.TakeOlderSwitch`, called by `Settings.MoveOffences` before the eight are read), so a player who had traffic offences off still has them all off. It has to be written, not only read as a fallback: the game's toggle on the options page (`ModOptionsToggleControl.Initialize`) shows an option with no saved value as the shipped "on" and sets it so, and the game's reset (`ModOptionsViewController.ClearOptionPrefs`) deletes the keys of the registered options and never a key that is no option. A value the player saved is never changed; a new player, and every start after the first, writes nothing. The Relaxed preset sets all eight off.
- An older version's `settings.txt` or `settings_before_preset.txt` holds one `player_offences=<0|1>` line: `Presets.Import` reads it as that value for each of the eight, at its place in the file (a later line for an option wins), so undoing a preset made before the update works.
- Drink driving has a switch of its own like the rest, `offence_drink`; Leisure reports it through `PoliceAttention.Report`, which asks the switch first.
- The switches change what the police do about the player's own offences only. Nobody else gains or loses anything, nothing is sent over the network, and a Going Public guest's traffic is still never looked at (`TrafficTools.Ready`).

### On foot, never while carried

The foot look runs twice a second and first asks whether something carries the player (`PlayerRide.Read`, `OffenceRules.Carried`), before it looks for an officer.
Any one of seven signs is enough, because each ride sets a different one:

| Sign | Whose ride it shows |
| --- | --- |
| the save's active vehicle (`PlayerHelper.IsUsingVehicle`) | the game's own cars, bikes and scooters |
| `TaxiSystem.IsTraveling` | the game's taxi and private driver |
| the player sits on a seat of the game's (`Character.isSittingOn`) | any seat |
| `PlayerController.NavigationDisabled` (any of the game's walking blockers is set, or its debug log window is open) | any ride that blocks walking, as the game's taxi and the mod's own bus and taxi rides do, and any other ride must or the player could walk off it; a panel or an activity sets one too, and nobody walks then either |
| the player's NavMeshAgent is off (`Character.navmeshAgent.enabled`) | the game's seats (`SitOnChair`), `AttachToObject`, and a hidden player (`ToggleVisibility(false)`, as the taxi and `EnterVehicle` do): each switches it off, and with it off nobody can walk, so a ride of another mod that borrows any of them is told without a blocker |
| a vehicle among the player's parents (`VehicleComponent` or `VehicleController`) | a ride that seats the player in a car the way the game does |
| a move faster than anyone on foot (`OffenceRules.FootMost`, 8 m/s) since the last look | a ride that sets none of the above |

While carried, the road timer is dropped, no officer is looked for and no offence on foot can be found.
The save's active vehicle cannot tell alone: a taxi or private driver from another mod, and the mod's own bus, never set it, and without the other signs every crossing the car passed against the walk light and every second in the road would be a shout and now and then a fine.
With Developer diagnostics on, one log line says when a ride begins and what showed it, and one when it ends; a foot offence always writes the signs it found (none, if a ride went unseen), so a player's Player.log shows which sign a fined ride did not give.
The signs are a heuristic: a ride of another mod that moves the player by a route that sets none of the seven, below 8 m/s, is still taken for a walk (the Guide's Known limits says so). EconomyCheck's `TrafficOffenceChecks` read each step from the game to the decision in its own text (`PlayerRide.Read` and its helpers, `PlayerOffences.Carried`, `OnFoot`, `Rest`, `Reset`) and show each to fail when one piece of it is changed.

The Guide's Crime page and the What's new line say what this covers in a player's words, one phrase for each sign (the game's walking block and its walking switched off are one phrase to a player), and say that a ride of another mod counts only if it shows one of them.
The speed in the Guide is the `ride_speed` fact (`OffenceRules.FootMost` in km/h, from `Meta/FeatureList.cs`).
`TrafficOffenceChecks` read both texts for every sign `RideFacts` holds and fail on a sign that has no phrase, so a sign added later is not left out of what a player is told.

## How it talks to other features

- A crime is one of the large street scenes ([`Wiring.Scenes`](../reference/wiring.md#one-street-scene-at-a-time-wiringscenes)) and casts its people from the shared pool ([Street scenes](street-scenes.md)).
- `PoliceAttention` and `Traffic/PolicePursuit` catch the player and charge the roadside fines once; Law and Justice take it from there. See [Who does what at an arrest](../reference/wiring.md#who-does-what-at-an-arrest).
- Every staged crime is reported to the paper with `News.Report`.

## Adding a crime

See [Adding a crime](../contributing/adding-things.md#adding-a-crime).
