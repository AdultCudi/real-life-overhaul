# Performance

The mod's first rule is that it costs the game nothing it can avoid. These rules say what a frame may cost and where heavier work goes. They follow Unity's guidance on scripting performance: as close to zero bytes allocated a frame as you can get, nothing looked up in `Update` that can be kept, and work run only when something changes.

A *frame method* is `Update`, `LateUpdate` or `FixedUpdate`, and the `OnUpdate`, `OnLateUpdate` or `OnFixedUpdate` it calls inside its guard ([RLO-ARC-007](architecture.md#rlo-arc-007-every-entry-point-is-behind-the-guard)). The mod has 122 frame methods today.

### RLO-PERF-001: An idle feature costs one comparison a frame

**MUST.** While a feature has nothing to do, its frame method costs at most one comparison with the clock. Run anything heavier once, on an event, or spread over frames.

Why: dozens of features run at once, and the sum of their idle costs is paid in every frame of every session.

Checked by: review.

```csharp
private void OnUpdate()   // good
{
    if (Time.unscaledTime < _next)
    {
        return;
    }
    _next = Time.unscaledTime + Gap;
    Look();
}
```

### RLO-PERF-002: Every frame method opens with HotSpots.Time

**MUST.** Open every `Update`, `LateUpdate` and `FixedUpdate` with `using var hotSpot = HotSpots.Time(slot);`, its slot made once in a static field with `HotSpots.Slot("<Feature>: <Class>.<Method>")`.

Why: with Developer diagnostics on, the session log then names the mod's share of a long frame. 120 of the 122 frame methods do; the other two are the guard's notices and the session record itself.

Checked by: StandardCheck.

```csharp
private static readonly int HotLateUpdate = HotSpots.Slot("Camera: ChaseCamera.LateUpdate");

private void LateUpdate()
{
    using var hotSpot = HotSpots.Time(HotLateUpdate);
```

### RLO-PERF-003: No allocation in a frame method

**MUST NOT** allocate in a frame method: no `new` collection or array, no string built by `+`, `$"..."`, `string.Format` or `ToString`, no lambda or closure. Keep collections in fields and `Clear` them; build a text only when its value changes ([RLO-PERF-007](#rlo-perf-007-text-is-rebuilt-only-when-it-changes)).

Why: 1 KB a frame at 60 frames a second is 3.6 MB a minute for Unity's collector, which is not generational. 24 lines in 14 files do this today, most in a log line written once.

Checked by: StandardCheck (in the frame method's own body; what it calls is review's).

```csharp
_near.Clear();                               // good: a field made once
var near = new List<CityPerson>();           // bad: every frame
```

### RLO-PERF-004: No search, lookup or physics query in a frame method

**MUST NOT** search the scene, look up a component (`GetComponent`), find a child by name or make a physics query in a frame method. Look it up once, when the feature starts or the object appears, and keep it in a field.

Why: a lookup costs on every call, and a search of the scene took 84 to 146 ms a time in the session records.

Checked by: StandardCheck.

```csharp
_body = car.GetComponent<Rigidbody>();       // good: once, when the car is taken
Rigidbody body = car.GetComponent<Rigidbody>();   // bad: in OnUpdate
```

### RLO-PERF-005: Whole-scene searches only through SceneFinds

**MUST.** Search the whole scene (`FindObjectsByType` and its relatives, `GameObject.Find`) only through `Core/SceneFinds`, and only behind a loading screen, the game's black screen at a door, or a pause (`Core/SearchPace`).

Why: `SceneFinds` names and times every search and writes FAIL in the log for one made in play. 40 calls in 13 files, mostly the diagnostics and the performance tools, are in the baseline.

Checked by: StandardCheck.

### RLO-PERF-006: Physics queries that do not allocate

**MUST** use the non-allocating physics queries (`Physics.RaycastNonAlloc`, `Physics.OverlapSphereNonAlloc` and their relatives) with a buffer kept in a field. **MUST NOT** use `RaycastAll`, `OverlapSphere` or the other calls that return a new array.

Why: each call allocates an array the collector must take back.

Checked by: StandardCheck.

### RLO-PERF-007: Text is rebuilt only when it changes

**MUST.** Rebuild a text shown on screen only when the value in it changes: keep the last value and compare.

Why: a label rebuilt every frame allocates a string every frame and makes the game lay out its text again.

Checked by: review.

```csharp
if (minutes != _shownMinutes)   // good
{
    _shownMinutes = minutes;
    _label.text = FormatMinutes(minutes);
}
```

### RLO-PERF-008: Delegates are made once and kept

**MUST.** Make a delegate handed to the game, to an event or to the guard once, keep it in a field, and use that field both to add it and to take it off. **MUST NOT** wrap a method at the moment of `-=` or `RemoveListener`.

Why: every method group and lambda is a new object, and a new one never matches the one added.

Checked by: review.

```csharp
SaveHook.Keep(_onSave ?? (_onSave = OnSaveGame));   // good
SaveHook.Keep(OnSaveGame);                          // bad: a new delegate on every call
```

### RLO-PERF-009: Coroutines start through the guard

**MUST** start a coroutine of the mod's through `Guard.Routine(...)`. **SHOULD** keep a yield instruction a loop waits on (`WaitForSeconds`) in a field.

Why: a coroutine that throws must stop its feature, not the game; a `new WaitForSeconds` in a loop allocates on every pass. 108 of the mod's 126 coroutine starts use the guard, and the other 18 run the game's own coroutines.

Checked by: ContractCheck (the guard), review (the yield).

### RLO-PERF-010: Timers by a next time, never by name

**MUST.** Time work with a next-time field read in the frame method, or with the director's own once-a-second step. **MUST NOT** use `InvokeRepeating`, `Invoke("...")`, `SendMessage("...")` or `BroadcastMessage("...")`.

Why: calls by name are found by reflection on every call and are outside the guard; none is in the mod today.

Checked by: StandardCheck.

### RLO-PERF-011: Heavy work is spread or hidden

**MUST.** Spread work that takes more than a millisecond over frames with `Core/WorkSlice`, or do it behind the loading screen (`ScenePrewarm`, `Core/LoadHold`) or the game's black screen at a door.

Why: the session record counts a frame over 80 ms as a long frame; behind a black screen nobody sees it.

Checked by: review, and the timing checks of [RLO-TST-008](testing.md#rlo-tst-008-anything-that-could-be-slow-is-timed).

### RLO-PERF-012: Every component says its cost a frame

**MUST.** Say in the summary of every `MonoBehaviour` class what it costs a frame, idle and busy.

Why: the cost is the first thing a reviewer weighs, and the summary is where they look. 112 of the 164 components say it today.

Checked by: StandardCheck (the summary of some part of the class mentions its frame or its cost).

```csharp
/// <summary>
/// ... The cost: nothing per frame but one comparison with the real clock; once a second, two property reads.
/// </summary>
```

### RLO-PERF-013: What code makes, code destroys

**MUST.** Destroy every engine asset the mod makes by code when it is dropped: a `Texture2D`, `Mesh`, `Material`, `RenderTexture` (a temporary one is released), `Sprite`, `AudioClip`, a `ScriptableObject` made with `CreateInstance`, and a copy of a mesh or material made with `Instantiate`. A sprite made with `Sprite.Create` goes with its texture (`CodeAssets.DropSprite`); a volume profile goes with the settings `Add` gave it (`CodeAssets.DropProfile`). An asset kept on purpose for the session (a glyph drawn once, a business type registered with the game) is cached once in a static field and listed in StandardCheck's kept list with its reason.

Why: destroying the GameObject that shows an asset does not free it, and an asset still referenced survives the game's own sweep of unused assets. A texture or a settings object made per photo, per load or per look and never destroyed is memory a long session never gets back.

Checked by: StandardCheck (every site's name destroyed in its type's files, or kept on purpose with the code that shows how; a kept entry nothing matches fails).

```csharp
CodeAssets.DropProfile(profile);   // good: the profile and its exposure setting
UnityEngine.Object.Destroy(profile);   // bad: the setting Add made stays
```

### RLO-PERF-014: What a frame method calls each tick does not allocate either

**MUST NOT** allocate in a method a frame method calls on every tick, in the features whose tick methods StandardCheck lists (the seasons: the director's step, the snow's look and its following of the camera, the leaves, the line under the date, the trees, the sky's test, the coats, the rain's hush; Flare's face painter and its moment; the living city's shelf; the session record and the timing of the mod's scripts; the performance director). Work that allocates on purpose and rarely (a log line when the season turns, the sky copied when its look changes, the walkers' paces narrowed) lives in a method of its own, left off the list.

Why: [RLO-PERF-003](#rlo-perf-003-no-allocation-in-a-frame-method) reads only the frame method's own body. The seasons' once-a-second step passed it while a player's session record showed the heap growing 888 KB a second under it.

Checked by: StandardCheck (each listed method's body, with RLO-PERF-003's patterns and any object of a class made; a listed method that is gone fails too).

### RLO-PERF-015: No character's look is built on a tick

**MUST NOT** build a character's look (the game's `UpdateElements`, `UpdateVisuals`, `SetAppearance`, `SetRandomAppearance`, `RandomizeElements`, or the mod's `Dress`, `Redress`, `PutBack`) in a method StandardCheck lists for RLO-PERF-014. A look that is only for variety waits for a screen that hides it: the loading screen, or the black screen at full strength at a door (`Core/ScenePrewarm`, `Looks.Hidden`); one a scene needs is built at the pace of `People/Looks`.

Why: the game merges a person's meshes for every look, some 1.5 MB of arrays and a few milliseconds each. The coats dressed one walker at every look in play and the living city changed worn looks in play (the owner's session record of 25 Oct 2026: 241 to 528 KB a second under the seasons' step, about 260 KB a second under the shelf).

Checked by: StandardCheck (each listed method's body).

### RLO-PERF-016: Props live in their bundles, within their limits

**MUST.** Build every modelled prop into one of the props bundles that `PropAssets/sets.json` lists, and only through `PropAssets/build.ps1`, which writes the bundle and its list (`PropAssets/contents/<bundle>.json`) together. The first bundle is closed: a new prop goes in the open one. A prop's id is in one bundle only. Each bundle keeps its set's triangle, atlas and disk limits. The mod reads the bundles in `Core/PropLibrary` and nowhere else, and a caller asks for a prop by id, never by bundle. Everything that ships or lists the mod's files carries every bundle. A prop the code asks for by name is in a bundle; a prop taken out of the bundles (`removed` in sets.json) is not named by code again unless it is modelled anew in the open one.

Why: one bundle filled its atlas and its triangle budget, and two prop branches merging once overflowed it ("the atlas is full: no room for plastic_grey"). A second bundle with its own limits lets new props in without changing any old one, and the lists keep a reader of the repository able to see what each shipped bundle holds without Unity.

Checked by: StandardCheck (each bundle's SHA-256 against its list, the limits, ids across the bundles, `PropLibrary.BundlePaths` against sets.json, the package, installer and zip taking the whole folder, INSTALL.md, UPLOAD.md, the Workshop credits and the folder map naming every bundle, the names asked of `PropLibrary`, `Shapes.Prop` and a clip's `tool=`, and the removed ids); the build itself (`sets.py`: a new id in the closed set, or one another bundle holds, stops it).

### RLO-PERF-017: The game's font is never found by a scene search in play

**MUST NOT** search the scene for the game's font (or for a text to take it from) while the game is played. `Core/GameUi/GameFont` finds it once by a scene search behind a black screen (`FromScene`, reached from `Refind` or from the getter only when `SceneFinds.Black`), keeps its name, and finds it again by that name among the assets in memory (`SceneFinds.InMemory`, under a millisecond) if the font held has gone. No other file of `Core/GameUi` searches the scene for a text or a font.

Why: a player's session record (26 Oct 2026) showed a 97 ms search for "a text of the game's interface (its font)" in a 139 ms frame in play, when a computer panel's buttons were first copied.

Checked by: StandardCheck (GameFont.cs: scene searches only inside `FromScene`, and the getter's `SceneFinds.Black` guard before it); EconomyCheck (the What's new line in all nine languages).

### RLO-PERF-018: The game's list of trees only holds trees that exist

**MUST** take the trees of a game object out of `TreeController.AllTrees` before the mod destroys the object, in every file that copies the game's scenery into the city (Uptown, new ground, copied buildings, car parks, building sites, landmarks, corners, schools, precinct frontage, hotels, floors): `SceneryTrees.Destroy` or `SceneryTrees.DestroyNow` in place of `Object.Destroy` and `DestroyImmediate`, or `SceneryTrees.Forget` on the line before. A line that holds no tree by construction says so with "RLO-PERF-018 no trees" in its comment. **MUST** keep `MapTreeGuard` started: its handler is first in the map's event (`EventHold.First`) and prunes the list (`SceneryTrees.Prune`) before the game's own map handler walks it.

Why: the game adds a tree to the list when it wakes and never takes it out, and its city map asks every tree in the list for its game object. One destroyed tree made `TreeController.ToggleMapLod` throw inside the map's open coroutine, so M only tinted the screen and the map stayed shut for the rest of the session (0.60.0: the garage doors' mouths on Uptown's pavements destroyed the trees standing in them). The game never destroys a tree in a session; the mod does.

Checked by: StandardCheck (no raw Destroy of a game object in those files; the guard's `EventHold.First`, its `SceneryTrees.Prune()` and the mod starting it); the Developer diagnostics line "Map check (trees)" and the test "Check the city map with a destroyed tree" in Options > Tests.

### RLO-PERF-019: What the mod adds to BizMan's open, switch and tab change stays small

**MUST** keep the mod's handlers on BizMan's business view small: under 2 ms added to opening a business and under 1 ms to switching business or changing tab, on a large save (100 businesses, 2,000 employees, 50,000 transactions), nearly nothing allocated. A step's handlers are the pieces on BizMan's pages (the Interior tab and its page, the presentation's refusal of the mod's buildings, the start screen's cards) and the listeners on the game's registration change event, which BizMan raises each time a business view or the Schedule tab is left. **MUST NOT**, in a handler of a step: subscribe to anything (a handler is put on once, by its feature's timer), search the scene, walk every registration, rebuild a layout, read the disk or use LINQ. A handler that runs on every step allocates nothing. Each opens `BizPaceWatch.Time(slot)` on its work, so that the session record, Options > Tests > Check BizMan's speed and the minute's line name it. A listener on the registration change event keys its work on the building's own facts (`BizPaceRules.Signature`) or its type, so a change that concerns another building asks for nothing.

Why: a player's report (27 Oct 2026) was that opening a business in BizMan and switching business or tab got slower with each release. The game raises the registration change event for every building each time a business view is left, and two of the mod's listeners then looked over the warehouses and the city's empty shops again a second later whatever had changed, the start screen read the city's businesses at every opening, and a log line made the Interior tab lay its columns out twice. Nothing in a step's path was timed, so nothing said so.

Checked by: StandardCheck (each listed handler's body: the banned patterns, `BizPaceWatch.Time`, no allocation in the cheap ones; a listener on the registration change event or a component put on BizMan's pages in a file the lists do not name fails); EconomyCheck (`BizPaceChecks.cs`: the accounting and the filters timed on a large save shape, under budget and allocating nothing; the texts); the test in Options > Tests (the walk through the player's businesses and every tab, PASS under the budgets).

### RLO-PERF-020: A job that is heavy the first time, or once a city, is spread over frames

**MUST** spread over frames, or build behind the loading screen, any job of the mod's that costs over 4 ms the first time it runs or once a city load: a walk over the city's buildings, the measuring of candidates with the engine's queries, a vehicle or a front built from the game's pieces, a search for places on the street. A job is cut into items (a building looked at, a candidate measured, a lane point tried, a piece of a front put up) and a `WorkSlice` of about a millisecond or two is handed down to the loop over them, so that a slice ends between two items and the job goes on from where it stopped (a cursor in a field, never a thread). The one-second tick may begin a job; a frame method carries it on. A job under a dark screen (`SceneFinds.Black`) may be done whole. **MUST NOT** call such a job whole from a frame method or a tick.

Why: a player's log (27 Oct 2026, ten minutes) put the mod's worst frames in work done whole. The law chose six precincts (a walk over some 880 buildings and 14 candidates measured in each district) and put up the first front in one frame of 83 ms plus 16 ms, with the garage's search for bike places (37 ms) in the same frame; a bus built from the prop bundle in a tick took some 20 ms; the bus stops' look along a window of lane points, each point some 2 ms of the engine's queries, made `TransitMap.Step` 16 ms at worst.

Checked by: StandardCheck (`SliceChecks.cs`: each listed job's method holds the words that spread it and none that does it whole; a job added to the list is checked the same way); NavCheck (`FrameCheck.cs`: a frame whose time went into a wait is told from one that went into work, so that a script is not blamed for a wait it was caught in).

### RLO-PERF-021: What the mod runs in the frame the game turns the day in is listed, and why

**MUST** list every handler the mod puts on the game's day event (`GlobalEvents.onNewDay`) or on the economy's `AfterDay`, with its verdict and the reason (StandardCheck/DayChecks.cs). A handler is **kept** in that frame only if the game needs its work there: it books money into yesterday's statement before the game's tax and daily summary read it, it sets what the game's own employee and rival runs of the same frame read, it writes the figures the day's customers are drawn by, or it is a few comparisons. Anything else is **spread**: the handler only queues its work (`DaySpread.Shared.Add`), and the pump (`Core/DayPump`) plays it a slice of about 2 ms a frame from the next frame on, in the order queued. A step is a function that says whether the work is finished, so a job cut into items goes on from where it stopped. The hour, the next day, the save (`SaveHook`) and the feature standing down play what is left at once (`DaySpread.Flush`); a city that goes drops its steps (`Drop`). The result must be the result of the same steps run at once, and the same on every machine (nothing a frame's length or a machine's speed can change). **MUST NOT**, in the body of a handler of the day: search the scene, rebuild a layout or read the disk. A new handler on the day that is not in the list fails the check.

Why: a player's log (27 Oct 2026) put the mod's scripts at 61 to 151 ms of the frame the game turns the day in, every one of the game's own steps of the day stopped behind them: Flare's round 32 to 46 ms, the economy 17 to 43, the weekly look of the investors 22, the tech sector 23, careers 13, the company 10 to 14 and Standing 10. Which could move was not known without reading each, so none did but Flare's.

Checked by: StandardCheck (`DayChecks.cs`: every subscription to the day is in the list, as many as listed, and holds nothing banned; `SliceChecks.cs`: each spread job only queues and each place that must finish it does); EconomyCheck (`DaySpreadChecks.cs`: a step queued in a frame is played in a later one, a job run a slice at a time gives the result of the same job at once, the flush before a save empties the queue, a fault costs only its step, and the weekly look made a business a frame with a flush halfway leaves the file the whole look leaves); the minute's midnight line in the session record (wanted: under 20 ms of the mod's in the frame, with the steps and the log lines left for later counted).

### RLO-PERF-022: What the mod changes for the city map is put back as it shuts, and the map check sees it

**MUST** put back everything the mod changed for the city map as it shuts, and at once. The map draws the whole city from above and pauses the game, so the mod's parts give things back for it: the far districts' squares, the mod's far things, the draw distances, a copied building's detail, the side walls' windows, the markers and lit buildings of the mod's places, the route and the district layer. A part gives a thing back in the frame the map's camera first needs it, before the camera culls, never the whole city at once when only part of it is seen (`FarDistricts` lends a square in the frame its bounds, 150 m wider and 300 m taller, enter the camera's view). It takes the thing out again as soon as the game says the map's camera is back down (`onCityMapClosed`), a slice of about 2 ms every frame, not after a pause of seconds; what it takes out is what was out before the map, the very renderers, but what the camera is near by then. A part that queues its work for the map catches up faster after it (`ModCulling`: 64 things a frame instead of 8, until none waits). **MUST** list every handler on the map's events (`GlobalEvents.onCityMapToggle`, `onCityMapClosed`) in StandardCheck/MapChecks.cs, with the name of its part's look, or with no part and the reason it changes nothing that must be put back. A part with a name adds its look once as it starts (`MapParts.Add`): a few counts and comparisons that say whether it still holds anything changed for the map, its words written only when it is given a line, no search of the scene and nothing allocated. **MUST NOT**, in the body of a handler of the map: search the scene, rebuild a layout or read the disk.

Why: the far districts gave back every renderer the city map draws as it opened (some 250,000) in that one frame, and took them out again only 5 seconds after it shut, a millisecond every 50 ms, so the street drew the whole city for 7 to 9 seconds after every map. Nothing saw it: the session record leaves paused frames out, so a report of a lower frame rate and stutters after the map (28 Oct 2026, 0.61.0 beta 4) could not be answered from the log.

Checked by: StandardCheck (`MapChecks.cs`: every subscription to the map's events is in the list, as many as listed; each part named adds its look and each look added is named; each listed handler holds nothing banned; the first handler on the map's event tells the session record, and the record asks the parts); the session record with Developer diagnostics on (`Diagnostics/SessionWatch.Map`): a line for each opening of the map (its frames, the frame it opened in and the one it shut in, with the mod's share of each), and `Map check (after closing): ...: PASS` once every part says it holds nothing within 2 s of play coming back, with the player's camera set against the one before the map, and the frames of play after it against the minute before.

### RLO-PERF-024: Work a moment sets off is spread a step a frame, or done once

**MUST** spread a step a frame the one-off work a moment of play sets off (getting into a car, a black screen at a door), or do it once for the session, so that no frame of play holds more than 2 ms of it. Getting into a car: the handling's design and the search for the car's power go a step at a time (`HandlingDesign`, `CarSolve`), a slice of 0.5 ms a frame (`CarTuning.SliceMs`), with the same result as the whole design and search, bit for bit. A black screen: the pieces waiting for it (`ScenePrewarm`) go one a frame, each tried once behind each black screen, and a piece of several steps does one a frame (`ScenePrewarm.NextFrame`: a coat or a worn look, each a mesh merge); a piece that needs only the game's traffic system is done in the game's late call, behind the loading screen. A piece that cannot be cut (one mesh merge, one search of the scene) is one frame's work by itself. A save: what does not change between saves is made once (the district save's serialization policy, which the game's serializer keys its formatters by). **MUST NOT** call the whole design or search from the game (`HandlingModel.Design`, `CarModel.Solve`: for the offline checks only), run every waiting piece in one frame, or make a new serialization policy at a save.

Why: the owner's 0.61.0 beta 6 log (28 Oct 2026) put the mod's own worst frames there: 15.2 ms as the player got into a car (the design and 24 drives of the model in one frame), 13.3 ms at a black screen (45.3 ms in beta 4: a dozen coats, the worn looks and the lane grid in its first frame), and 53 ms of a save frame in the district save's section, most of it the serializer building a formatter for every type of the records again (a new policy each save; one kept for the session gives the same bytes in some 8 ms instead of 19 on the owner's save, offline).

Checked by: StandardCheck (`SpreadChecks.cs`: each listed method holds the words that spread its work or keep it once, and none that does it whole); NavCheck (`CarSpreadCheck.cs`: the design and the search a step at a time against the whole, with a whole design of another car between every two steps, each step's time, the frames of the spread, and the self-check line's verdict); with Developer diagnostics on, `Car tuning check (spread over frames): ...` for each car got into and `Prewarm check (behind a black screen, a piece a frame): ...` for each black screen, each with its frames, its slowest frame and PASS or FAIL against 2 ms (a frame of one piece that cannot be cut is not held against it).
