# Performance

The mod's first rule is that it costs nothing you can feel. This page holds the rules for per-frame work, what was measured, and the two folders that measure: `Scripts/Performance` and `Scripts/Diagnostics`. The short list of rules for a pull request is in [the rules](../contributing/rules.md).

## The rules in short

- While a feature is idle it costs one time comparison per frame.
- Nothing is allocated per frame. No `OnGUI`, no LINQ and no text built in `Update`.
- No search of the whole scene (`FindObjectsByType` and the like) while the game is played: only behind a loading screen, the game's black screen at a door, or a pause (`Core/SceneFinds`, `Core/SearchPace`). Work that comes back at every door is made only on a frame the black screen covers at full strength (`SceneFinds.Black`), never in its fades: they take 0.2 s each way at a door, and the picture shows through them.
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

## The mod's own things far away: drawn by distance (`Performance/ModCulling`)

The game measures its own street things (lamps, trees, benches, signs, stands) against two bands from the player, 60 m and 200 m (`Culling.CullingManager`, `Culling.CullingGroupController`, `ICullable`), and puts out their lights, particles and people beyond the first.
The mod's own things are measured the same way, in a culling group of its own with the same bands (`ModCulling`; the rules in `CullRules`, the slots in `CullBook`): a thing registered there has its renderers held back from drawing (`Renderer.forceRenderingOff`, only on renderers that were drawn, given back exactly) beyond 200 m, and drawn again inside it.
Nothing else of it changes: its colliders, navigation boxes, place and state stay as they are, so the culling is visual and this machine's alone, and nothing about it goes over a Going Public session.
While the city map is open everything is drawn.
It is a performance option, "The mod's bus stops, signs and boards drawn within 200 m" (`perf_mod_cull`), on by default: the camera's far plane is 300 m, so what goes is small and far.

A group of its own rather than the game's: the game's list re-reads every sphere of its whole list in the frame after any one leaves it (`UnregisterCullable` sets `_recreateCullingGroup`), and bus stops and station fronts come and go.
The engine measures the spheres as it culls; a change of band is noted in its callback and applied in a later frame (at most 8 things a frame), so a frame with no change costs `PerfDirector` one comparison.

Every kind of thing the mod puts in the world, and how each is kept from costing frames far away (`CullRules.Inventory`, printed by the F7 probe's census):

| Things | How | Why |
| --- | --- | --- |
| bus stops (pole, shelter, bench, lettering, strips) | culled here | every place of the city (26) is dressed while the city is loaded; the bay's paint already ends at 120 m |
| police station fronts and their parked patrol cars | culled here | dressed within 260 m and taken in beyond 330 m: drawn only to 200 m in between |
| the university's, the courthouse's and the driving school's signs | culled here | they stand all session |
| help-wanted boards, newspaper kiosks | culled here | they stand all session |
| hotel fronts, data centre fronts, bike docks and stands | their own distance | dressed within 150 m, 160 m and 90 m |
| the living city's stalls, carts, brooms and cones; civic stands and dressed sites | their own distance | taken away beyond 150 m (out of view beyond 95 m), 160 m and 175 m |
| a parked patrol car with officers (`PolicePresence`) | its own distance | put out 45 to 95 m away out of view, taken away beyond 175 m |
| the mod's street people (crowds, staff, officers, crews) | out of view | `CityPeople` and `ActorPool` stop drawing whoever is out of view |
| interiors (stations, offices, server rooms, the showroom, the garage, campus rooms, home life) | indoors | drawn only while the player is in the building |
| signs on the mod's buildings (`FrontSigns`) | the game's | the game's own sign holders, culled by the game |
| fires, accident scenes, emergency vehicles and their beacons | never | an incident in progress |
| police pursuits and reckless drivers | never | a car the player chases or is chased by |
| the city bus on the road, the tow truck, taxis | never | the player waits for it, rides it, or works with it |
| street events, jobs' props, leisure, the driving test, runs, community service | never | the player's own activity, in progress |
| friends, family and roommates met in the street | never | a person in a conversation |
| the player's own bikes | never | drawn as the game draws the player's own cars |

What it saves: the bus stops (a dozen or so renderers each, all 26 standing at once), a station front's pieces with up to three parked cars, and the signs, boards and kiosks are no longer drawn while they are between 200 m and the far plane in view; that band is where most of the camera's draws are (2,779 of the 4,487 renderers drawn were 200 to 400 m away in the probe of 9 Oct).
Estimated: a few dozen to about a hundred draws fewer in a busy view, under 0.1 to 0.3 ms of the render thread; the F7 probe's census now counts the mod's renderers in the street by distance and how many the culling holds back, and Shift+F7 measures `mod_cull` as a row of its own (it counts the renderers drawn with it off and on).

## The picture settings and the pipeline's costly features (`Performance/Tweaks/ShadowReach.cs`, `PipelineTrials`)

Three settings in the Performance section's "Shadows and detail", each the game's own until the player moves it (`RenderLevelRules`):

- **Sun shadow distance** (`render_shadow_reach`, 25% to 100% of the game's): the shadow distance of the game's own global volumes ('Default Volume' and 'OutdoorShadowVolume'; 100 m in the probe of 9 Oct) as a share; fewer things drawn into the shadow maps, shadows end nearer. Interiors' local volumes are never touched.
- **Sun shadow cascades** (`render_shadow_cascades`, the game's own or one to four; the game has two): fewer passes over the shadow casters, softer shadows near the camera. For three and four the pipeline's own splits are set, as the game's profile holds splits for two only.
- **Detailed models distance** (`render_lod_reach`, 25% to 100%): the player's camera's level-of-detail bias as a share of the game's quality level's, through the camera's own frame settings (`PerfFrameSettings`).

Only values the game's own global volumes set are changed, kept as found and put back exactly when a setting returns to the game's own, when the city goes or the option faults.

The benchmark (Shift+F7) has a trial row for each, at a set value (half the distance, one cascade, half the detail), and for each of the pipeline's often costly features: screen-space reflections, the pipeline's ambient occlusion, volumetric fog, volumetric clouds and contact shadows.
A trial of a feature the game has off already is not measured but listed under NOT MEASURED with the reason: on High the pipeline asset supports none of reflections, ambient occlusion, volumetric fog or volumetric clouds (the game's own ambient occlusion is the HBAO plug-in, on its own option in the game's settings), and the volumes leave contact shadows off (the census of 9 Oct).
A picture setting left at the game's own value is listed there too.

Found while reading: the Visuals section's "Detail distance" (`visuals_detail`, `World/VisualEnhancer`) writes `QualitySettings.lodBias`, which the render pipeline replaces with its frame settings' bias for every camera's culling (`HDRenderPipeline.TryCull` calls `QualitySettings.SetLODSettings` with `GetResolvedLODBias`), so it has not changed what is drawn. Left as it is for the owner to decide; "Detailed models distance" goes through the frame settings.

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

Two sessions of 0.45.0 (the owner's Player.log and Player-prev.log, 21 Oct), every frame over 80 ms:

- **Flare's hour: the mod's, on the hour.** A frame of 84 and of 99 ms, 53 to 63 ms of it `FlareDirector.OnHour` (the city's posts, the City Office's cases, invitations, the unread count, the words asked ahead and the player's milestones, all in the game's hour event, with the other features' hour work in the same frame).
  The hour's work now runs a step a frame from the frame after the hour (`Social/Flare/FlareHourSteps`), in the same order; the save, a new day and standing down finish what is left first.
  Each step is named in the session record ("flare: the hour's ...").
- **The first ticks after a load** (a frame of 91 ms, 38 ms of it the mod's: `EconomyDirector.Tick` and its `StartUp`, nested): once a load, with the game's own physics and scripts at 50 ms in the same frame; left.
- **A police station's front put out (434 ms) and `MapLinks`:** behind the loading screen, not seen.
- **The game's own, left as they are:** two frames waiting for the last frame to be shown (159 and 212 ms: the render thread, the graphics card or the display; the mod's scripts 0.2 ms), and two frames outside the engine's steps (102 and 119 ms, the mod's scripts under 3.2 ms).
- **The transit director's worst moment of the first minutes** (13.4 ms, under 80 ms so unnamed): the bus zones made the first time out of doors had every parking lane beside a zone laid out again by the game in that frame (`ParkingLaneGenerator.RegenerateAutoParkSpotsNear`: a sync of every moved transform with physics, and the lane's spots destroyed and made again, two to four lanes a zone).
  Now one lane a tick (`BusZones.RelayOne`), and the transit director's steps have slots of their own in the session record (`BusZones.Step`, `TransitMap.Step`, `DressNear`).

A session of 0.46.0 (the owner's Player.log, 21 Oct, 22:45 onwards; 54 frames over 80 ms, three loads):

- **The hair finish's search of the whole scene, in the fade back in: the mod's, on every walk into a building.** 27 searches of 76.7 to 107.9 ms (86 on average), 21 of them named in frames of 90 to 180 ms, each 0.2 to 0.3 s after the game's "went into a building"; none found anyone new in strand hair.
  `SearchPace` made it half a second into any fade, and `SceneFinds.Dark` counted the whole fade (`UiFader.isFading`) as black. At a door the game fades out for 0.2 s, puts the building in, waits five frames and fades back in for 0.2 s (`BuildingManager.EnterBuildingCoroutine`, `DelayedEnterBuildingActions`, `WarmUpBehaviourManager`), so half a second in is the middle of the fade back in: the room, half shown, stopped for some 90 ms.
  The session record said "black or fading: not seen" and the search check said PASS, for the same reason.
  Now the search is made only on a frame the overlay is at full strength (`SceneFinds.Black`, the overlay's own alpha, read only while the game fades): at a door in the frame after the game's own "entered, and settled" call (`GlobalEvents.onEnterBuildingDelayed`), four frames before its fade back in; in a long black screen (sleep, a taxi) at 0.5 s and 2.5 s as before. The record names a frame in a fade "fading, the picture showing through" and counts it as seen, and the search check says FAIL for a search in a fade. `ScenePrewarm`'s pieces left for the street wait for the same full black screen. NavCheck's `DoorFadeCheck` runs the game's own fades frame by frame.
- **The game's own, behind its black screen at full strength:** every walk out, a frame of 213 to 257 ms (one of 382 ms with a collection finished in it) under "coroutines and timers" with the heap up 12 to 24 MB (`ExitFromBuildingCoroutine`, then every `CityMapHider` switched back on in the one frame, as found before); every walk in, a frame of 82 to 165 ms (the building's load), one of 570 ms with a collection finished in it. The mod's scripts in those frames: 0.2 to 2.7 ms. Left.
- **Flare's faces behind the black screen:** 100 faces in the session, 6.5 to 12.6 ms of the main thread each, a quarter of a second apart (`FlareFacePainter`, the game's `PortraitGenerator`: one render and a read-back, no coroutine of its own). A face is no hitch; the 141 to 159 waiting are Flare's people drawn ahead, not work left in a frame. Left.
- **The ambulance crew sent out: the mod's, seen.** One frame of 97 ms, 67.6 ms of it the crew's way planned in one go (`CrewWay.Plan`: 754 ways, 5444 poses, 180 questions to the engine), with the player driving past. Once an accident with someone lying in the road. Not changed in this round: spreading the plan over frames is a job of its own.
- **The first frames after a load: the mod's, once a load.** 592 ms as the loading screen fades out (the mod's scripts 399 ms of it: the law director's first frame with a precinct front, 74.8 ms, the garage's, 43.3 ms, life events', 39.3 ms), then 140 ms (the economy's first ticks, 19 ms). Three loads in the session. Left, as in the round before.
- **The game's own, seen:** a frame of 535 ms under "coroutines and timers" at a desk of the game's (the mod's scripts 0.4 ms), 189 ms of rendering after a change of clothes, 144 ms waiting for the last frame to be shown after a night's sleep, 143 ms in the game's own scripts at one of its moments, and three of 89 to 110 ms outside the engine's steps.

A session of 0.52.1 with the city expansion (the owner's Player.log, 25 Oct; 61 frames over 80 ms in eighteen minutes of play):

- **The game's own, at every door, behind its black screen: the stutters felt.** 24 frames of 102 to 825 ms, 81 to 772 ms of each under "coroutines and timers", the heap up by as much as 108 MB in the one frame, every one in the frame the player went into or came out of a building (`EnterBuildingCoroutine`, `ExitFromBuildingCoroutine`, as found before). The mod's scripts in them: 0.3 to 4.1 ms.
  Each came 0.2 s after "flare: a face drawn by the game's portrait camera", because the face painter drew on the first frame of the fade out (`UI.UiFader.isFading`) and the game's fade takes 0.2 s; a walk in with no face before it was the same (228 ms, 42 MB; 224 ms, 12 MB in Player-prev.log). The face itself is 7 to 18 ms of one frame (61 in the session), seen through the fade.
  Now a face is drawn only on a frame the overlay covers at full strength (`Social/Flare/FlareFaceMoment`, `SceneFinds.Covered`), and its self-check says FAIL if the picture showed through. NavCheck's face pace runs it on the model of the game's fades. The game's door frames are left.
- **Looks built in play: the steady allocation.** The game's mesh merge allocates some 1.5 MB a person; one in a sampled frame (one in eight) reads as 250 to 530 KB a second.
  The coats (`Seasons: SeasonDirector.Update`, 241 to 528 KB a second) dressed one walker at every look, 20 a minute; they are now dressed (or given their clothes back) behind the black screen of the next walk out, twelve at most a door.
  The living city's worn looks (`CityPeople.Tending`, 254 to 268 KB a second, one to three a minute in play) wait for the black screen too (`Looks.Hidden` counts the overlay at full strength; four a door).
  A career's boss was given a random look on the shelf as they left and their own look built again when the player came back (`JobCast.Refresh`, 267 KB a second): a seeded person now keeps their look. The career cast and the bus stops' people are made behind the loading screen (`JobDirector.Update`, 312 KB a second in the first minute, a character made for the first boss).
  The first minute's `PerfDirector.Update` (923 KB a second) was the far districts' survey, once a city: 397,234 renderers sorted into 58 squares. The squares are kept with their room for the next city. The session record itself allocates nothing a frame (StandardCheck's RLO-PERF-014 reads its frame path).
- **The first frames after a load, behind the loading screen: the mod's.** 567 ms, 344 ms of it the mod's: some 35 directors' first loads in one frame, the law director's first (57.9 ms: the record, the precincts chosen and the nearest front put out), the Garage's (39.5 ms: the two-wheelers' places, with 170 log lines) and the seasons' (25.3 ms: 300 trees walked). The loading screen still covered it.
  The law director and the Garage now have seats of the start line (`Core/StartLine`), a frame each; the trees are walked behind the loading screen (`ScenePrewarm`), and a millisecond at a look after it; the two-wheelers say three refused places and count the rest.
- **Not changed, for the owner:** the character work. The hair finish's search of the whole scene (`CharacterQuality.Update`) was in 21 frames of 107 to 419 ms, 80 to 219 ms of each, all of them 0.1 to 1.2 s after a walk into a building and all with the overlay at full strength by the record (none in a fade); its 22 searches each said PASS and found 38 to 54 people. Three of them took 206 to 219 ms (frames of 300 and 415 ms).
- **Then the hair finish's search at doors:** it now goes from the drawn hair to its wearer (`CharacterQuality.FindOthers`, `Character/HairWearers`). Skinned meshes are a kind of the engine's own, listed by the engine (a few hundred drawn in the city, a fraction of a millisecond), where the game's characters are a kind of its scripts, found only by going through every object in memory. A drawn renderer of strand hair is its nearest character's if that one wears it, else the wearer a search of every character found; a renderer neither can place sends that search once, and its answer (a wearer or nobody) is kept for the scene. Options > Tests, "Compare the hair search with the old one", makes both searches and says PASS or FAIL in Player.log.
- **The mod's save, seen:** 262, 126 and 98 ms when the game saves (50 features). Left for a round of its own.

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
| `TransitDirector` (4 times a second) | the stops in reach, the bus | **before: every second the label of each dressed stop was put together for a self-check that had already been made; and the self-check itself (a dozen questions to the navigation mesh, once per dressing) ran with diagnostics off, when nobody reads its line. After: neither.** The first time out of doors, the bus zones' parking lanes are laid out again by the game one a tick, not two to four in one frame |
| `MetaUi`, `CuffPose`, `HoseHold`, `CarMeasure`, `CarDrag`, `ActivityAnimators`, `RagdollSystem`, `Stretcher`, `EmergencyDriver`, `BikeMount`, `TipsyDriving`, `PerfSampler`, `PerfLoop` | disabled while idle | nothing to do |
| `TowService` (added after this read-through) | disabled while there is nothing to tow; with a truck out, a time comparison a frame and a 0.25 s tick (a few distances; driving away, `TransitSight.Seen`'s rays) | at work a handful of transform writes a frame (the bed, the cable, the wreck); `TowTruck.Update` and its `BusDriver` run only while the truck is on the street |
| `CityPeople.Update`, `ActorPool.Update` | off with nobody out; with people out, a distance and a dot product each to hide those out of view | left: it is what keeps unseen characters from being drawn |
| `PlayerDouble.LateUpdate` | off by default; on: every bone copied | left: an option that says what it costs |
| `CarStrikes.FixedUpdate` (driving above 5 km/h) | every other step: the car against walkers, standing figures and the mod's people | left: it is the hit test itself |
| `NearbyInteraction.Update` | the option and two key tests | **before: eighteen `TakesInteractKey` flags read every frame, then the keys. After: the option, the two keys, and the flags only on a press (the same answer; NavCheck's SlowSpotCheck keeps the order and every flag)** |
| `TrafficAccidents.Update` -> `TrafficWatch` | 5 times a second: a fifth of the pool's bodies, and each standing car's | **before: every pooled car's body (active, kinematic, position) five times a second. After: every car once a second, and five times a second only those that moved under 2 m in that second (`TrafficWatchRules`): moving traffic is read a fifth as often, standing traffic at most a fifth more. A bounce is still caught, within 5.4 s of it starting, from a stand or after a stop (NavCheck's BounceCheck)** |
| `PerfDirector` -> `ModCulling.Frame` | one comparison | new: the mod's things beyond 200 m are not drawn; a change of band costs a walk over that thing's renderers, at most 8 things a frame |
| `FlareDirector` on the game's hour | nothing between hours | **before: the hour's work in the hour's frame (53 to 63 ms the first hour after a load). After: a step a frame from the next frame, six steps an hour** |
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
| `PerfBench` | Shift+F7: every tweak, trial (one render-pipeline feature off, or a picture setting at a set value) and reference measured off/ON/off/ON in one-second windows, paired so drift cancels; a feature the game has off already, or a setting at the game's value, is listed as not measured; RealLifeOverhaul_bench_HHmmss.txt |
| `ModCulling`, `CullRules`, `CullBook` | the mod's own things drawn by distance, with the game's bands; the inventory of every kind of thing the mod puts in the world; the culling group's slots |
| `RenderLevelRules`, `Tweaks/ShadowReach.cs` | the picture settings: the sun's shadow distance and cascades, and the distance of detailed models, each the game's own until moved |
| `PerfGame` | what the above need from the game (pedestrians, traffic, lamps, trees) |

### `Scripts/Diagnostics`

Development aids: the session record and its frame timing, and the F9 character report (`CharacterLab.cs`, marked temporary in its own summary).

| File | What it is |
| --- | --- |
| `SessionWatch` | the session record: with "Developer diagnostics" on, one line a minute (frame times, memory, the mod's objects) and one for every frame over 80 ms, naming what the mod was at (see [The session record](logs-and-debugging.md#the-session-record)) |
| `FrameSteps` | where the frame before this one went, by engine step (scripts' Update, coroutines, physics, navigation, rendering...), for the long-frame line; in the engine's loop only while the record runs |
