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
