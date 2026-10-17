# Performance

The mod's first rule is that it costs nothing you can feel. This page holds the rules for per-frame work, what was measured, and the two folders that measure: `Scripts/Performance` and `Scripts/Diagnostics`. The short list of rules for a pull request is in [the rules](../contributing/rules.md).

## The rules in short

- While a feature is idle it costs one time comparison per frame.
- Nothing is allocated per frame. No `OnGUI`, no LINQ and no text built in `Update`.
- No search of the whole scene (`FindObjectsByType` and the like) while the game is played: only behind a loading screen, the game's black screen at a door, or a pause (`Core/SceneFinds`, `Core/SearchPace`).
- Every `Update`, `LateUpdate` and `FixedUpdate` opens `using var hotSpot = HotSpots.Time(slot);`, so the session record can name the mod's share of a long frame.

The rules as they were written for the code:

- A look over everything of a kind in the scene (every material, every lamp, every building) is never done in the frame that asked for it once the city is running: the list is taken once and gone through a few hundred at a time on a timer (`RefractionOff`, `StaleLampLights`).
  A performance tweak is told of a scene loaded or unloaded with `WorldChanged(bool loaded)` and only notes it.
- Anything large done in one frame is named with `SessionWatch.Mark` (see [The session record](logs-and-debugging.md#the-session-record)).
- Push values to the engine only when they change; poll game state at the slowest rate that works (per in-game hour, or a timer), not every frame.
- Visual options that cost frames are off by default and say so in their label.

- The game moves everyone on foot (the player included) on the navigation mesh; colliders stop nobody.
  Anything that must be solid for people is cut out of the mesh with a carving `NavMeshObstacle` on an object of the mod's own (`SceneObstacles` for vehicles, the cordon's walls), pooled, switched off on every exit path, never put on a pooled car, never where the game's pedestrians walk (pavements, crossings), and always with a way out left open.

  A cordon wall runs exactly along tape that went up (and takes the barriers of an end in with it), so what looks closed is closed; the cordon says in the log whether each wall really cut the mesh.

## When characters' looks are built (`People/Looks`)

Building a look is the one costly thing the mod's people need: `AppearanceSetter.UpdateVisuals` merges the character's meshes (several `Mesh.CombineMeshes` and new arrays of bones and bind poses each time).

The game's own pedestrians are dressed once and kept, so the game does none of this while the city is played; in 0.1.0 the mod did it whenever a living-city site was set up (a merge per person, in that frame), after every scene (every civilian re-dressed) and the first time a scene needed each of its twelve people.
The rule since 0.1.1:

- **Where it cannot be seen.** `RealLifeOverhaulCity.OnLoadAsync` runs behind the loading screen: there the living city's eight (`CityLifeDirector.Prewarm`) and six civilians of the street pool (`ActorPool.Prewarm`) are made and dressed.
  Four officers and two paramedics follow as soon as the prop bundle has loaded (their kit is made of it; 25 s at most), one a frame while the loading screen or a pause is still up (`Looks.Hidden`), else one every `Looks.Gap` (5 s).

  So all twenty exist before the first scene can start.

- **One at a time afterwards.** Whoever prepares a look ahead of need asks `Looks.Free`: one a frame while hidden, else one every 5 s over all pools together.
  Each pool has a coroutine for it that runs only while its shelf has work.
- **A look is kept.** A civilian's look is "worn" after three outings or ten minutes (`Looks.Worn`) and is then changed on the shelf through the slow path.
  Whoever came back longest ago goes out first, so with six (or eight) on the shelf the same face is not handed out twice running, and one character is never two people at once.
- **Dressed for a part, kept for it.** `ActorPool.Take` hands out somebody already dressed for the part; only when nobody is (more officers wanted than exist, say) is a look built on the spot, and then on one of the part there are most of on the shelf.

  The living city's pool goes further (`CityPeople.Thrifty`): when nobody on its shelf is dressed for a part, `Take` says no, as it does when nobody is free, and the look is made ready on the shelf (`Want`); the director tries that site again first on a later look (`_retry`).
  A site with two parts nobody has takes two or three tries, six seconds apart, out of view.

  People dressed for a job stay dressed for it, so the shelf comes to hold what the street's sites use.
  A test button builds on the spot (`AtNeed`).

- **Not changed:** the pools of fires and street events and of the civic crew (the same class, not thrifty: their scenes are few and dress their people as they are set up), and the casts whose people are somebody in particular (`FamilyCast`, `SocialCast`, `JobCast`, the hotel doorman): a relative's or a friend's look is that person, and is built when they are first put out.
  Every one of them is counted.
- **Counted.** Everything that builds a look calls `Looks.Count` first; the session record prints the totals.

Weighed and not taken: `AppearanceSetter.skipMeshMerging` (no merge at all; `ModLooks` uses it for the player's one high-detail double).

Every garment then stays a skinned renderer of its own, eight to eleven a character where there is one now, drawn, culled and skinned in every frame the character is in view, for up to twenty-odd people; and the pools' hiding of people out of view, the cuffs and the kit all work on the one merged renderer.
That would trade a hitch now and then for a cost in every frame.

## What cuts the navigation mesh, and when

A carving `NavMeshObstacle` makes the engine cut the piece of mesh it stands on again whenever it appears, goes or moves.
All of the mod's are `carveOnlyStationary`, placed while switched off and then switched on, so each is cut once.

- **Bus stops** (`BusStopDressing`, five boxes a stop, three stops dressed).
  In 0.1.0 the boxes cut the mesh whenever a stop was dressed, which driving through the city did every few seconds (dressed within 135 m, taken in beyond 175 m; 190 m and 230 m from 0.1.4).

  Now a dressed stop is a thing to look at until the player is on foot within 60 m of it (`Solid`, kept until 90 m, and never on the bus): only then are its boxes enabled.
  Nothing is cut while driving.
  What is lost: while a stop is not solid the game's pedestrians (who walk on this mesh) may pass through its pole and panels, which can be seen from a passing car.

  A stop's five boxes change in the same frame on purpose: the engine cuts the tile once for the lot, and one box a frame would have it cut five times.
  The self-check ("stop dressed", diagnostics only) is made when a stop becomes solid.

- **Carts** (`Core/SolidFootprint`: the player's own job cart, and the living city's coffee and hot-dog carts).
  One box on the cart object, its own length (1.5 m) and as deep as leaves the vendor's and the customer's places (0.68 m and 0.72 m from the cart's middle) on the mesh: half a depth of 0.68 less the walker's radius less 0.05 m, 0.40 m at most.

  While it does not cut it is the obstacle walkers steer round that the carts had before (0.56 m deep).
  The living city's carts cut only while the player is on foot within 60 m (kept to 90 m), as the bus stops; the job's cart cuts from the moment it is put down, because the player is on foot beside it for as long as it stands.

  1.6 s after a cut `Verify` asks the mesh: the cart's middle off it, both places on it, and a way on foot from one to the other.
  If a place was lost the cut is taken back for that placing and the cart is steered round only.
  One line: `Jobs check (cart solid): ...` or `City life check (cart solid): ...`.

  If the walker's radius is 0.58 m or more there is no room for a box and the carts are steered round, not solid; the line says so.

- **Scenes** (`SceneObstacles`, eight vehicle boxes; the cordon's walls; a parked patrol car's box; a precinct's parked cars; the wedding's): put out once when the scene is set and taken in once when it ends, each group in one frame for the same reason.
  The cordon switches a wall off and on once more if its cut did not take.
  None is toggled on a timer.

## Long frames: what was found, and the rules that follow

Three sessions of Player.log with the record on (83 frames over 80 ms) sort like this:

- **The game's own, behind its black screen: about three in four.** `BuildingManager.ExitFromBuildingCoroutine` fades to black, then calls `GlobalEvents.onExitBuilding`, on which every `CityMapHider` switches its part of the city back on in the one frame (`CityMapHider.OnExitBuilding`; on the way in the same work is spread over frames): 215 to 280 ms every time.
  `EnterBuildingCoroutine` loads the interior and its items under the same fade: 85 to 630 ms.
  Neither is the mod's and neither is seen.

  The record now counts them apart.

- **A search of the whole scene, in play: the mod's, about 110 ms each.** `Object.FindObjectsOfType` and its relations go through every object the engine has in memory.

  The traffic system's `CurrentSceneData.GetSceneInstance()` is such a search on every call, and five features called it: the line "Crime: road map built" was followed by a frame of 112 to 117 ms in all three sessions, and every driving lesson and test began with one of 126 to 146 ms.
  **Rule: no search of the scene while the game is played.** `Core/RoadData` finds the road data once per city.

  `Core/SceneFinds` is the only place that searches, names and times each search and says FAIL in the log for one made in play.
  `ScenePrewarm` runs each feature's one-off search behind the loading screen (the game loads its mods after the city's scenes and before its loading screen goes: `UI.Load.LoadingScreen.LoadAsyncScenes`).

- **The collector: not a cause of a frame that is seen.** Four of those frames had a collection finish in them, all four at a door (the game makes and destroys a whole interior there, and a collection that cannot keep up is finished in one go: it made those frames an estimated 40 to 100 ms longer, behind the black screen).

  No long frame outside a door had one: in ordinary play the collector works in slices of 3 ms, and the probe measured 3.09 ms as its longest share of a frame.

- **Cuts of the navigation mesh: not shown to be a cause.** Every long frame within 2.5 s of a cut asked for by the mod was a walk through a door, or the frame the cordon itself was set out in.
  The step timing now proves or refutes it per frame ("navigation").

  The rules stand: a carving obstacle is moved only while switched off, everything of one scene changes in one frame, and every change is noted in `Core/MeshCuts`.

- **Handlers on the game's events.** `EventHold.Keep` with several holders on one event had each put itself back every second for ever, because each one's putting back changed the event for the others (`Core/EventHold.cs` says how it is kept now).
  It was a few kilobytes a second, not a long frame.

A session of 0.9.4 (half an hour on foot in the street, 16 frames over 50 ms a minute):

- **A search of the whole scene every five seconds of play: the mod's.** About twelve frames a minute of 84 to 124 ms, all under "scripts' Update" with nothing of the mod's named, five seconds apart to a tenth of a second.

  The hair finish for other characters in the game's strand hair (`Character/CharacterQuality.FindOthers`, new in 0.9.4) called `FindObjectsByType<AppearanceSetter>` on a five second timer, not through `Core/SceneFinds`, so the record did not count it, and found nobody in a game played alone.

  Now the whole scene is searched only behind the black screen, a loading screen or a pause (`Core/SearchPace`, twice at most in one), and in play only Going Public's own list of the other players is read (`Core/GoingPublic.RemotePlayers`).

- **Searches the first time something was needed, in play: the mod's.** The marked crossings (a frame of 107 ms, "the city's crossings being listed", and the traffic's first look of 89,806 microseconds) are now listed behind the loading screen with the road map (`SolidStreets.Prewarm`).
  The game's navigation obstacles beside the bus stops likewise (`BusZones.Prewarm`).

  The game's panel of the building the player is in is taken from the game's own reference, not from a search of the scene with switched-off objects included (a frame of 104 ms under "coroutines and timers" the first time the player went into a building).

- **The traffic's look over its budget** (113 microseconds on average and 3311 at worst): the developer diagnostics' own lines, sixteen in that minute, were written, and their texts made, inside the measured look.
  They are now written with its clock stopped, and the minute's line says where the look's time went, phase by phase.
- **Frames with their time under "the rest"** (115 to 244 ms, four of them, one just after going into a building): not in any script's step, so not the mod's scripts.
  Which engine step they were in is named from now on.
- **The heap's growth** (72 MB in the busiest minute, 35 MB in the next): the mod's share of it is now said in the minute's line, and a frame in which the heap leaps by 8 MB or more (one of 25 MB in that session) gets a line of its own.

## What the merged whole costs per frame

Read through after the merge: 67 `Update` / `LateUpdate` / `FixedUpdate` methods, no `OnGUI`, no `InvokeRepeating`, and no `Find*` or allocation on any idle per-frame path.
"Idle" is in the city with no scene and no panel, walking or driving.

| component | idle, per frame | changed here |
|---|---|---|
| the thirteen feature directors (Jobs, Home, Transport x2, Leisure, Events, Social, Travel, Business, Justice, Licences, Civic, Garage) and `LifeEvents` | a timer comparison and two to eight flag tests; their work is on a 1 s (some 0.25 s) tick | **before: each took itself off the game's save event and put itself back every 1 to 2 s (a dozen features, two to four new delegates each, every second). After: one relay for the whole mod (`Core/SaveHook`), re-hooked only when the game's event is not the one last seen: nothing allocated while idle.** Home's game-event hook is made once |
| `ThirdPersonCamera.Update` | the heaviest always-on method: both of the game's cameras are written every frame | **before: a sphere cast every frame for the indoor camera even out of doors. After: only while that camera is live** (`CinemachineCore.IsLive`) |
| `ChaseCamera.LateUpdate` (driving) | pose of the chase camera | **before: the lens written every frame. After: only when the field-of-view option has changed** |
| `CityPeople.FixedUpdate` (people out, driving) | every other step: the car against eight people | **before: `GetComponent<Rigidbody>` twice a second. After: when the car changes** |
| `CrimeDirector`, `EventDirector`, `BusinessDirector`, `SocialDirector`, `CarOwnership`, `LeisureSound`, `PoliceAttention`, `PolicePursuit`, `BeatPatrol`, `PolicePresence`, `PedestrianIncidents`, `PlayerKnockdown`, `Law.LawDirector` | one to three comparisons | nothing to do per frame. **On their timers, found later: `Law.LawDirector` (the save and the two building events, every three seconds), `HomeDirector` (the game's event, every second), `Family.FamilyDirector` (the save, every two seconds) and `MapPlaces` (the map event, every three seconds) still took themselves off the game's events and put themselves back. Now `SaveHook.Keep` and `EventHold.Keep`: nothing allocated while the game's event has not changed** |
| `PerfDirector` and the tweaks that are on by default | one time comparison; the tweaks tick every 2 to 5 s | **before: every scene loaded or unloaded made `RefractionOff` look through every material in memory in that frame (`Resources.FindObjectsOfTypeAll<Material>`, seven engine calls a material) and, on the next tick, `StaleLampLights` through all 3,038 street lamps (three reflected fields each). After: unloads are ignored, a load asks for one look four seconds after the last scene arrived, and both looks go through their list 300 and 250 at a time, 20 times a second. The first look after the city loads is sliced the same way; refraction stays as the game has it until that look is finished** |
| `TransitDirector` (4 times a second) | the stops in reach, the bus | **before: every second the label of each dressed stop was put together for a self-check that had already been made; and the self-check itself (a dozen questions to the navigation mesh, once per dressing) ran with diagnostics off, when nobody reads its line. After: neither** |
| `MetaUi`, `CuffPose`, `HoseHold`, `CarMeasure`, `CarDrag`, `ActivityAnimators`, `RagdollSystem`, `Stretcher`, `EmergencyDriver`, `BikeMount`, `TipsyDriving`, `PerfSampler`, `PerfLoop` | disabled while idle | nothing to do |
| `TowService` (added after this read-through) | disabled while there is nothing to tow; with a truck out, a time comparison a frame and a 0.25 s tick (a few distances; driving away, `TransitSight.Seen`'s rays) | at work a handful of transform writes a frame (the bed, the cable, the wreck); `TowTruck.Update` and its `BusDriver` run only while the truck is on the street |
| `CityPeople.Update`, `ActorPool.Update` | off with nobody out; with people out, a distance and a dot product each to hide those out of view | left: it is what keeps unseen characters from being drawn |
| `PlayerDouble.LateUpdate` | off by default; on: every bone copied | left: an option that says what it costs |
| `CarStrikes.FixedUpdate` (driving above 5 km/h) | every other step: the car against walkers, standing figures and the mod's people | left: it is the hit test itself |
| `NearbyInteraction.Update` | the option, thirteen `TakesInteractKey` flags, two key tests | left for whoever next touches that line: test the two keys first and the ten flags only on a press |
| `TrafficAccidents.Update` -> `TrafficWatch` | 5 times a second: every pooled traffic car's body looked at | left: the bounce watchdog's whole purpose; it is the largest timed cost while idle |
| `Wiring` | nothing: no component, no tick | adapters run when asked; `Wiring.Scenes` is four flag reads on a director's own roll timer |
| `HomeDirector`, `CarOwnership`, `GarageDirector` (their interface moved to the game's own after this read-through) | a time comparison each; the Garage also compares what is ridden (`TwoWheelers.Frame`, one reference) | **before: a key test per frame while a prompt was up (Home, car ownership, Garage), the run's marker and the two-wheeler's marker drawn per frame, the custom shop's money text written four times a second. After: no key is read (the game runs its panels' buttons), the markers are the game's guider, set when they change, and a text is written only when what it says has changed.** The needs bars are three `fillAmount` writes a second at most, each only when its whole number changed; the bay panel's line and button are built when the game shows or refreshes its panel |
| `Economy.EconomyDirector` (added after this read-through) | no `Update`: a coroutine that waits one second on the real clock, then four flag tests, three handlers held (`SaveHook.Keep`, `EventHold.Keep`) and, only while nobody lends, one static field read | see [Economy](../features/README.md#economy): its work is on the game's hour and day events (a handful of loans and banks compared; the score worked out when something changed) and when the app is opened |
| `Nav.NavDirector` (added after this read-through) | a time comparison and two flag tests; while the minimap shows, the player's place and heading compared with what was last written, and written (one quad's texture window, one position, up to 34 rotations) only when they differ | see [Minimap and GPS](../features/navigation.md): its work is on a 0.25 s tick, and a route search takes 0.4 ms of a frame while one is under way |

Not shared, on purpose: every director reads the player, the hour and "in a vehicle" for itself, but on its own slow tick, so a common ticker would save a few property reads a second and couple thirteen features to one clock.

## The measuring folders

### `Scripts/Performance`

fixes for the base game's frame rate, and the measuring that decides them.

| File | What it is |
| --- | --- |
| `PerfDirector` | switches the tweaks as the options ask, gives each its timer tick; F7 probe, Shift+F7 benchmark |
| `PerfTweak` | one optimisation: Apply records what it changes, Restore puts it back; a fault switches it off |
| `PerfTweaks` | the list of tweaks (options page, saved choices, benchmark rows all follow from it) |
| `Tweaks/` | the tweaks themselves, one small class each |
| `PerfSampler` | frame times, main/render/GPU split, engine counters over a window; off unless measuring |
| `PerfLoop` | where the main thread's frame goes: a timestamp in front of each of the engine's frame steps, and per camera; off unless measuring |
| `PerfProbe` | F7: ten seconds measured, then RealLifeOverhaul_perf_HHmmss.txt with the step table and a census of the scene |
| `PerfCensus` | what is in the scene and how the engine is set up (report time only) |
| `PerfBench` | Shift+F7: every tweak, trial (one render-pipeline feature off) and reference measured off/ON/off/ON in one-second windows, paired so drift cancels; RealLifeOverhaul_bench_HHmmss.txt |
| `PerfGame` | what the above need from the game (pedestrians, traffic, lamps, trees) |

### `Scripts/Diagnostics`

Development aids: the session record and its frame timing, and the F9 character report (`CharacterLab.cs`, marked temporary in its own summary).

| File | What it is |
| --- | --- |
| `SessionWatch` | the session record: with "Developer diagnostics" on, one line a minute (frame times, memory, the mod's objects) and one for every frame over 80 ms, naming what the mod was at (see [The session record](logs-and-debugging.md#the-session-record)) |
| `FrameSteps` | where the frame before this one went, by engine step (scripts' Update, coroutines, physics, navigation, rendering...), for the long-frame line; in the engine's loop only while the record runs |
