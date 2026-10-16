# Street scenes: accidents, the emergency services and the street's people

## In a sentence

Staged traffic accidents with an ambulance and police response, the tow truck, hit-and-run chases, the player's crash consequences, people knocked down by cars, and the pool of characters every street scene shares.

## Where the code is

- [`Scripts/Traffic`](../../Assets/Mods/RealLifeOverhaul/Scripts/Traffic/): staged traffic accidents with ambulance and police response, hit-and-run chases, player crash consequences. Read `TrafficAccidents.cs` first.
- [`Scripts/People`](../../Assets/Mods/RealLifeOverhaul/Scripts/People/): the mod's street characters and the scenes they play. Read `ActorPool.cs` and `ActorRole.cs` first.
- [`Scripts/Ragdoll`](../../Assets/Mods/RealLifeOverhaul/Scripts/Ragdoll/): people hit by cars: ragdoll physics, pedestrian collisions, the player knocked down. Read `RagdollSystem.cs` first.

### `Scripts/Traffic`

| File | What it is |
| --- | --- |
| `TrafficAccidents` | the orchestrator: what happens and when; owns the scene and clears it on every exit |
| `ResponseUnit` | one ambulance or patrol car taken from traffic (always the real ambulance, always the police body) |
| `EmergencyDriver` | drives a unit along the road points: closes up and stops short of vehicles and people, never leaves the road or forces through; says what holds it up |
| `RouteGates` | red lights and closed lane points for a vehicle the mod drives like anybody else (the bus's rules, for the called taxi) |
| `SceneParking` | the free-space tests (what a pose touches; a unit's whole body against every solid layer; a strip of road), and where a unit pulls up when a scene has no lane for it to stand in |
| `SceneCordon` | closes the road round a scene (the traffic system's own switch) with an apron for the response vehicles, says where they stand; tape and barriers with a wall along each, an opening wherever the ambulance crew's way crosses it and nothing standing on that way (SetCorridor), the cone taper, the gate (its hold line 24 m before the scene; held shut for a leaving unit, and for the crew), debris and evidence markers at the point of impact |
| `TrafficWatch` | self-check: stops a vehicle that bounces on the spot, and reports and removes a response vehicle left standing with its crew aboard or after its scene |
| `SceneObstacles` | cuts the vehicles standing at a scene out of the navigation mesh, so people on foot go round them |
| `TrafficEffects` | lights, sirens, the police body, fire, smoke, debris |
| `PursuitSight` | whether a patrol car still has the player in sight: shared by both pursuits |
| `PursuitPace` | how fast a patrol car after the player may go (0.95 of their car's top speed, 130 to 200 km/h) and how long it keeps at it |
| `PursuitBackup` | the second patrol car that joins a pursuit of the player after 45 s, if traffic has a car to give |
| `TrafficTools` | what all of these need from the traffic system; a vehicle's real footprint from its colliders |
| `Tow/` | the tow truck that takes a cleared scene's wrecks away (TrafficAccidents hands them over in Clear) |
| `TowService` | one truck at a time: held out of sight, sent, the operator's work, the wreck winched up and carried, taken off the road unseen; the wreck goes as before wherever this cannot be done; the test button |
| `TowTruck` | the mod's own rollback (props tow_truck, tow_truck_tilt, tow_truck_bed, the bus's wheels): solid, driven by BusDriver, its bed posed, its light bar and lamps, the winch's cable |
| `TowRoads` | where it stands (ahead of the wreck in its lane, come by the lane beside) and where it starts (out of sight, 70 to 280 m back): once per truck sent |
| `TowRules` | game-free (NavCheck/TowCheck): the bed's motion, a car drawn along the road and up the bed, the stretch it takes, the time it is said to take, when it may come and go |

### `Scripts/People`

| File | What it is |
| --- | --- |
| `ActorPool` | the twelve characters every scene shares: created once, reused in the same outfit, hidden when out of view; four may be out only to be seen and give way to any scene |
| `Uniforms` | the one outfit every officer wears and the one every paramedic wears: named garments, fixed colours; the hi-vis shirt |
| `ServiceKit` | what is worn with a uniform, on the bones: badge, belt, radio, cap, POLICE / PARAMEDIC panels, the medic bag; fitted to the measured body, the cap to the measured head (band above the brow, peak above the eyes) |
| `Stature` | how tall a character stands, from its joints and measured crown; the scale that makes officers 1.86 m (women 1.76 m) and no paramedic short |
| `BonePose` | aim a bone, reach a hand to a place: shared by KneelPose, SupinePose and Crime/CuffPose |
| `StreetActor` | one character: walk, stand, pose, kneel, gesture |
| `KneelPose` | down on one knee, set on the bones by hand (the game has no such animation); costs nothing to hold |
| `SupinePose` | flat on the back along a bed, for an animated character or a ragdoll at rest: what the stretcher lays its passenger in |
| `ActorRole` | one person's part in a scene; safe to use when nobody could be had |
| `StreetSpots` | where people can stand: beside car doors, on stretches of pavement |
| `StreetPeople` | who counts as a person on the street besides the walking pedestrians: the game's standing and sitting people, from its pools' parents (once a second, only while asked) |
| `AccidentCast` | drivers, paramedics and officers at a traffic accident (run by Traffic/TrafficAccidents) |
| `CrewWay` | the ambulance crew's one way from the rear doors to the casualty and back, planned and tested pose by pose before anyone sets off, and the fixed layout beside the casualty (stretcher, who kneels where) |
| `CrewPath` | that way as plain geometry (no engine types; checked outside the game): a line with rounded corners laid out by distance, the crew's pose at any distance along it, what it sweeps over, how near that comes to a point, a box or a length of tape |
| `SceneWitness` | an unhurt driver or bystander at a scene: checks the casualty, phones, looks for help; out to the kerb the moment the tape is up or the paramedics are out, and gives their account there |
| `SceneOfficer` | one officer's jobs at a scene: one on traffic at the barrier (stepping out of the open lane to wave the ambulance through when it leaves that way); the other at the casualty, setting out evidence markers, taking the witness's account, at the cordon's opening |
| `Stretcher` | the ambulance crew's wheeled stretcher: one, built once; wheeled along a CrewPath, moving its crew with it frame by frame (no navigation agent finds a way), swung where a corner will not do; its passenger laid straight (SupinePose) under a blanket, or a covered form |
| `PeopleAhead` | the people a mod-driven vehicle must brake for whom physics cannot see |
| `RoadsideStop` | the crew of a patrol car at a stopped car: a ticket at the window, or an arrest (Crime/ArrestSequence) |
| `StreetCrime` | the old name for street incidents; forwards to Crime/CrimeDirector |
| `BeatPatrol` | a pair of officers walking a beat near the player, most of the time |
| `PolicePresence` | a patrol car parked at the kerb with its officers by it, and an officer at a junction |
| `ICasualty` | someone lying at a scene who is not one of its cast (a person hit by a car) |

### `Scripts/Ragdoll`

| File | What it is |
| --- | --- |
| `RagdollRig` | builds a ragdoll on a character's bones (bodies, colliders, joints) and strips it again |
| `Ragdoll` | one character as a ragdoll: animated to physical, frozen at rest, restored completely |
| `RagdollSystem` | the six ragdolls there are, for full characters and the game's plain standing figures alike, and up to eight bodies that lie on without physics when a seventh person goes down (removed out of view); the only per-frame work, and only while someone is down |
| `PedestrianTakeover` | borrows one of the game's pooled pedestrians and gives it back (walking, or through the pool) |
| `Casualty` | one person who was hit: lies, gets up angry and walks off, or stays until taken away |
| `PedestrianIncidents` | rolls what a hit does by speed, looks after the casualties, stages traffic hitting someone |
| `CarStrikes` | notices the player's car running into people (positions, not collisions): walkers, standing figures (StreetPeople) and the mod's own, everyone in the sweep, one log line per batch |
| `PlayerKnockdown` | the player on foot hit by traffic: brief ragdoll, then shaken, injured or hospital |

## How it talks to other features

- One large street scene at a time: `TrafficAccidents.IsActive` is one of the flags [`Wiring.Scenes`](../reference/wiring.md#one-street-scene-at-a-time-wiringscenes) puts side by side.
- Roads are closed only through `SceneCordon` (and Civic's roadworks by the same rule); the bus, the taxi and the tow truck share its driver, `EmergencyDriver`.
- Crime scenes cast their people through `People/ActorPool` and `ActorRole`, as every scene does.
- `PolicePursuit` arrests and summons through Justice; see [Who does what at an arrest](../reference/wiring.md#who-does-what-at-an-arrest).

## Rules the street scenes keep

- People come from `People/ActorPool` only, through an `ActorRole`, and never more than twelve at once, of whom at most four are only there to be seen (`ActorRole.EnterPresence`: officers on the beat, by a parked car, at a junction).
  Those are taken back without warning the moment a scene needs a character, so whoever owns one must cope with finding nobody behind the role.

  A scene must work when the pool gives it nobody, tick its people on a timer (not per frame), and release every one of them in every way it can end.

- The stretcher and its crew never go anywhere by a navigation agent.

  It is wheeled along a line worked out beforehand (`People/CrewPath`), and it is the stretcher that moves its crew (`Stretcher.Wheel`): every frame it puts whoever pushes on the line, itself an arm's length ahead pointing from them to its own place on the line, the second of a pair beside its leading end (the near hand on that rail), all on the ground height the line was given.

  Agents are only told the speed, for the walk to play to (`StreetActor.Convey`).
  So nothing on the navigation mesh (a wall's cut, a vehicle's, an edge) can turn the crew aside, hold it up or stop it short, and there is no "own route" to fall back on.
  The few steps between the places they work at beside it are walked the same way (`Stretcher.Walk`: round the bed's corner, round the casualty's head or feet).

  With `stretcher_push_bar` in the bundle the hands are on its grips (0.225 m either side, 0.95 m up, 1.11 m from the middle: where `push_cart` holds a rail for someone 1.55 m from the middle), and the crew kneel to lift 0.10 m further out than that, clear of the bar.

  Nobody at the stretcher has anything else in a hand: the medic bag rides on the bed (`ServiceKit.Stow`) and is in a hand only while its owner walks without the stretcher.

- The ambulance crew has one way out and back (`People/CrewWay`), planned once when the ambulance has stopped, and everything else agrees with it.
  Corners are rounded to 1.2 m and the last stretch is straight for the stretcher's own reach, so it ends exactly on its place pointing exactly along it.

  Every pose on the way (the bed, whoever pushes, whoever walks beside it) is tested before anyone sets off against vehicles and fixtures (0.3 m to spare), the tape (never run along, crossed in one opening), and the casualty's own body (0.2 m).

  It tries both sides of the ambulance and of the casualty, straight, square through an end of the tape, or by a place to one side; on the carriageway and out of any lane traffic is let past in first, and only then over a pavement or through that lane (held shut meanwhile: `SceneCordon.HoldForCrew`).

  Two last moves: a run-in round a corner onto the body's own line; or, where the casualty lies across a road too narrow for that, a swing-in (straight at their side to 2.1 m off their line, then a quarter turn into place with the crew at the bed's ends).

  Back by the same places, round behind the ambulance and up to the rear doors square, leading end 0.25 m from them (swung square on the spot where there is no room to turn).
  The layout at the casualty is fixed from the body's axis (`CrewWay.Lay`): the stretcher parallel, head to head, 0.55 m away; whoever treats at the chest on the other side; the second at the head or hips.

- The cordon stands clear of that way (`SceneCordon.SetCorridor`): no post, cone or barrier within 0.3 m of anything of the crew on it, no tape across it, an opening at least 2.6 m wide wherever it crosses a side, the near end's own opening where it crosses that end.
  Tape that already stands clear is not touched.

  When tape is run again, a place is never judged by a navigation mesh that the tape's own walls are still cut out of (that moved every post in off its line, onto the crew).
  Traffic let past a scene waits at a hold line 24 m before it, behind where the ambulance stands, never beside its rear doors.

- A crew's step ends when it has been done, never on a clock: the stretcher at the end of its line, both at their places, the lift played.
  The ambulance does not drive off from under its crew (only the scene's hard limit ends that).
  A step that is well overdue may be finished at once only when both of the crew are out of the player's sight.

  Each step writes one `People: crew check (...)` line with what was measured, what is allowed, and PASS or FAIL: set off, through the opening, parked beside, lifted, set off back, back through the opening, loaded; the cordon adds its own `Cordon: run round the ambulance crew's way ... PASS/FAIL`.

- Nobody but the crews and the casualty is inside the tape: a witness or passer-by goes out to the kerb the moment the tape is up or the paramedics are out (`SceneWitness`), and gives their account there; the place they wait is never on the crew's way.
- A fine for people struck is taken at the roadside only up to the share of the cash in hand a court's fines are bounded to (`PolicePursuit.Roadside`: 35 %, never bounded below 250, `JusticeLaw.Bound`); the rest is left to the court case, which credits what was paid, and the toast says so.
- Ragdolls come from `Ragdoll/RagdollSystem` only (six at once: 11 bodies each while falling, nothing simulated at rest; when all are in use the one who has lain longest gives up their place) and are ended through it on every path.
  A character must never be moved, shown, hidden or animated while it is one (`StreetActor.Limp`, `RagdollSystem.Holds`), and a pedestrian borrowed from the game goes back through `PedestrianTakeover` with nothing of ours left on it.

- A vehicle driven by the mod (`EmergencyDriver`) never touches anything: it brakes for vehicles and for people (`PeopleAhead` covers those without a collider), moves over only into another lane of the road graph, never steers off its road points and never forces through.

  The one exception is a car being stopped at the roadside, which pulls in to the kerb side of its own lane first (`EmergencyDriver.PullOver`), and only where `SceneParking.KerbRoom` found that strip to be carriageway, nobody's lane and free of everything.
  Held up near its goal it stops in lane and its crew walks.

  A response vehicle parks on its own route, where `SceneCordon.Stand` puts it: in a lane the wrecks block, on the scene's own carriageway, the ambulance nearest the casualty and outside the tape, nose to the scene.

  Never at a kerb, on a parking strip or across a crossing: the place is tested before it sets off with its whole body and 0.3 m all round against every solid layer (`SceneParking.Standing`: vehicles, sign posts, hydrants, poles, a sign overhanging the road), and one that fails is moved back along the unit's own lane only (`TrafficAccidents.SettleStand`).
  Distances to a place are measured along the route, never as the crow flies.

  Traffic standing in a unit's way is what gets moved (`TrafficAccidents.ClearTheWay`), not the place.

- A unit with its crew aboard leaves, forwards, within seconds.

  The loaded ambulance goes at once, siren on: the tape is taken in (`SceneCordon.Lower`), bystanders are sent to the kerb and the officers are called back.
  It drives on along its own lane: straight through where the scene is if that lane is clear, or round the wrecks by a lane beside them if that is (the gate held shut for it: `SceneCordon.HoldGateShut`, given back on every path).

  Otherwise the wrecks standing in its lane are taken off the road first (out of view at once, in view after five seconds).

  The patrol car is the last to go: when the ambulance is past the scene and the patrol car's own lane through it is clear, it drives off while the road is still closed, and the road is given back when both are away.
  With a wreck still in its lane it waits for the scene to clear and drives off through it.

  No unit is ever handed back to the traffic system standing: it is driven until it is clear of the scene and moving along a lane (which may be after the scene has ended: `TrafficAccidents.TickDepartures`), or taken off the road out of view.

- A departure is a plan, not a hope, and the two units never wait on each other.
  Where the patrol car stands is settled against the ambulance's way out when the units are scheduled (`TrafficAccidents.PlanDeparture`): at the far end only if it stands clear of that line by both half widths and 0.4 m, else behind the ambulance in its own lane.

  A unit leaving through the scene by a lane checked for it does not pull out to pass (`EmergencyDriver.MayPass`): the lane beside is the other unit's.
  A driver that is out of its lane and held up goes back to its own lane when that is clear.

  A leaving ambulance held up by the patrol car has it moved at once (`GiveWay`): officers called aboard, driven off ahead of it, or backed along its own lane when the two are nose to nose (the one case in which a unit reverses).
  "Could not be moved" does not exist for the mod's own vehicles.

- No response vehicle is taken off the road where somebody may see it (in the camera's view, or within 45 m of it): one that cannot get away stays, says so once (`departure check (... held up): FAIL`), keeps trying to drive off, and is removed only out of sight.
  `TrafficWatch` (`Guard`) still reports any response vehicle that does not move for 25 s.

  An ordinary ambulance from the traffic pool that comes within 90 m of a scene is taken off the road out of view, so that only the scene's own stands there.
  Each step of a departure writes one `Accident: departure check (...)` line with what was measured and PASS or FAIL: planned, units parked, ambulance set off, ambulance past the scene, patrol car set off, patrol car past the scene, road reopened.

- The traffic system's springs have no damper: what steadies a car is the traffic system replacing its velocity every physics step, which stops the moment the car is ignored (`SetVehicleIgnored`).
  A vehicle the mod holds is therefore always either driven (`EmergencyDriver` bleeds off its vertical speed) or kinematic (`Pin`, `ResponseUnit.Park`), never left standing unparked; it goes back only through `TrafficTools.ReturnToTraffic` / `Remove`, which put its body right and log its state.

  Nothing solid and immovable is left where traffic can end up on top of it (a body at rest gives way to a vehicle over it: `Ragdoll.YieldToVehicles`).
  `TrafficWatch` reports and stops whatever bounces anyway.

- Tape, posts, barriers and cones stand on the carriageway only: never on a pavement or a kerb, never through anything solid (vehicles, street furniture, buildings, steps).
  Cones are a taper upstream of an end, beyond the response vehicles, laid when those are in and taken in when one leaves.
- A vehicle that shows its driver in the cab (the delivery truck) has that figure hidden while the driver is on the road as one of the scene's people, and shown again when the car is let go (`TrafficAccidents.EmptySeat` / `Unpin`).
  So has any further vehicle of a crash the road was closed for (the third car, whose driver nobody plays): nobody sits at the wheel of a wreck through the scene.

  Only cars and SUVs are rolled; a rolled car is frozen flat on its side, roof or wheels, never leaning.

- Nobody is laid down where the whole length of a body is not clear of every vehicle (`AccidentCast.BodyClear`); a casualty on the stretcher lies along it on their back (`SupinePose`).
- A scene says in the log, once per transition and never per frame, what each unit and crew is doing (dispatched, held up, parked at N m, crew out, stretcher up, loaded, left), so that a run can be diagnosed from `Player.log` alone.
  A scene is planned to clear inside three minutes; only a casualty still being seen to can take it past that, and never past five.

- A scene leaves the road and the traffic as it found them on every way out, traffic shut down included: a car the mod holds is handed back or taken off the road (the game's own clearing skips cars with a scripted route), and `TrafficAccidents.Recheck` looks again on the next tick out of doors and says so if anything was left.
- Nobody gestures at nobody: conversational gestures play only while the person spoken to is standing with them.
  A scene is worded as what it is (one car is never "two drivers").
- Roads are closed only through `SceneCordon`, which remembers exactly which road points it switched off and opens those again on every exit path (`TrafficAccidents.Clear`).

  Nothing else may touch `temporaryDisabled`, with one exception: `Civic/RoadClosure` closes one lane beside roadworks by the same switch and the same discipline (only points that were open, never one somebody else has shut, only a stretch with no junction and with every way in shuttable where traffic has another way to go; exactly those points opened again on every path out).

- Officers and paramedics wear `People/Uniforms` and the equipment of `People/ServiceKit` (pieces on bones: no component, collider or shadow, hidden with the body, removed when the character is dressed as anyone else) and nothing else; civilians stay random.
  An ambulance is only ever a response vehicle (`TrafficTools.IsAmbulance`): never a wreck, a runner or a suspect, and no other vehicle stands in for one.

- People stand and walk at street level: positions come from `StreetSpots` (which refuses any walkable point that is not at the height of the ground beneath it) and they travel only by their navigation agent.
  Poses use animations the characters have (`StreetActor.Knows`) or `KneelPose`; a pose the animator lacks is skipped, never substituted blindly.
