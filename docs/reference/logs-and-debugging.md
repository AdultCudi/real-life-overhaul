# Logs and debugging

The mod writes to the game's own log, `Player.log`. This page says where it is, what the mod writes there, and how to send one with a bug report.

## Where the log is

On Windows:

```
%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions\Player.log
```

- `Player.log` is the session that is running or just ended. The game overwrites it when it starts again, and moves the one before to `Player-prev.log` in the same folder.
- Near the top the game writes `Loaded Big Ambitions (Build N)`: the game's build.
- The mod's own files (settings exports, photo mode's pictures, bug reports, backups) are in `RealLifeOverhaul` in that same folder, which is the game's `persistentDataPath`.

## What the mod writes

Every line of the mod's starts with `[Mod:Real Life Overhaul]` (the game's logger puts it there). The few lines the mod writes to the engine itself start with `[RealLifeOverhaul]`.

| Kind | When it is written | Example |
| --- | --- | --- |
| Warnings and errors | Always | `Game build: ...`, `Game contract: ...`, an exception with its stack |
| A feature's own lines | Only with **Developer diagnostics** on | `Jobs: offer for 'cleaner' (day 14, hour 9, seed ...)` |
| Self-checks | With **Developer diagnostics** on, once per event | `Jobs check (cart solid): ... PASS` |
| The session record | With **Developer diagnostics** on, once a minute and for each long frame | `Session 12 min: frames 5210, average 11.5 ms, ...` |

A line begins with the feature or part that wrote it (`Jobs:`, `Transit:`, `Meta:`, `Game UI:`, `Map links:`), so you can search the log for one feature.

### Self-checks: PASS and FAIL

A self-check line says what was measured against what was wanted, then `PASS`, or `FAIL` with the reason. For example, `Transit check (arrival)` gives how far the bus stopped from its pole, and `Law check (court hold): taken to <precinct>: in the holding cell: PASS` says where a court's hours were served.

- A self-check is written once per event (a scene starts, a bus arrives, a panel opens), never per frame.
- A `FAIL` line names what did not hold. It is the first thing to read in a report.
- A few lines are written whatever the switch says, because a player's log must show them: the settling of a shift saved mid-way (`Jobs check (saved shift): settled ...`) is one.
- Each feature page lists its own lines under a heading such as "Log lines" or "Checks"; see [log lines by feature](#log-lines-by-feature).

### Faults, and features that switch themselves off

Every entry point the game calls is behind the mod's guard (`Core/Guard.cs`, `Core/FeatureGuard.cs`):

- A fault that says the game lacks a member stops that entry point's class at once, with one line, instead of failing every frame.
- Any other fault is written in full, the first three of one entry point in full and then a short line each.
- Five faults within thirty seconds switch the feature off for the rest of the session.
- After a game update, the options page says under **Troubleshooting**, **Diagnostics**, "What a game update switched off". How the guard works is in [After a game update](../contributing/after-a-game-update.md).

If the mod is switched on twice (the Workshop item and a copy in `ModsLocal`), the second copy writes `Real Life Overhaul is switched on twice ...` and does nothing.

## The Developer diagnostics switch

On the mod's options page, under **Troubleshooting**, **Diagnostics**, switch on **Developer diagnostics**. Switch on **Show test tools** too if you want the test buttons. In code the switch is `Meta.Dev`, and `Meta.Dev.Verbose` is true while it is on.

"Show test tools" and "Developer diagnostics" are off as shipped.
With diagnostics off the mod writes only warnings and errors to `Player.log` (every logger in the mod goes through the one switch, `Meta.Dev.Gated`), and the three diagnostic keys (F7, Shift+F7, F9) do nothing.
To work on the mod, switch both on under Diagnostics on the options page.
With both on, the first test there, **City expansion: run every check**, runs every automatic check of the city expansion by itself and sums it up ([The test run](test-run.md)).

The switch drops the line, not the work of making it: the text handed to `Info` is put together before the logger sees it.
That is fine for a line written when something happens (a scene starts, a bus is sent).
A line on a timer, or one that needs a question asked of the game first (a self-check on the navigation mesh), tests `Meta.Dev.Verbose` and does neither while diagnostics are off.

## Reporting a bug with a log

The quickest way is the **Report a bug** button, under **Start here** on the options page (the **Settings** app on the phone has it too). It sends nothing. It gathers what a report needs into one folder, zips it and opens the folder and the mod's Discord for you:

- `report.txt`: the versions, the language, Going Public, the preset and every option changed from it, the day, the money and the mod's last 50 warnings and errors;
- `Player.log` and `Player-prev.log` (the last 6 MB and 3 MB), `settings.txt` and a screenshot.

The folder is `RealLifeOverhaul/Reports/<date and time>` under the game's folder above, and the last five reports are kept. Your Windows user name and Steam ids are masked; look through the zip before you post it, since names can still be in the logs.

To report by hand instead:

1. Switch on **Developer diagnostics** and make the problem happen again.
2. Close the game, or copy `Player.log` before you start it again.
3. Open a [bug report](../../.github/ISSUE_TEMPLATE/bug_report.md) with the game's build, the mod's version, the steps and the log.

For stutter, play ten minutes or more with the switch on from the start; [the session record](#the-session-record) below says how to read what it writes.

## The session record

For reports of the kind "it stutters after a few minutes, or hours".
`Diagnostics/SessionWatch` is a component on the mod's root object that is disabled unless "Developer diagnostics" is on (`Meta.Dev.Apply` switches it), so it costs nothing as shipped.

On, it costs a dozen reads of numbers the engine and the game already keep and as many comparisons a frame, some two hundred and fifty reads of the clock for the step timing (`Diagnostics/FrameSteps`, a few microseconds), the timing of the mod's own scripts (`Core/HotSpots`, some 55 ns a scope, under 8 microseconds a frame), and, once a minute, a walk over the mod's own objects.

Nothing is allocated except for a line that is written.
As shipped, each of the mod's scopes is one flag test.

One line for every minute of play (loading screens, pauses and time with the window in the background are left out):

```
Session 12 min: frames 5210, average 11.5 ms, worst 61 ms, over 33 ms: 4, over 50 ms: 1; managed heap 412 MB of 610 MB,
collections in the minute 3, grown by 31.0 MB in the minute (517 KB a second), the collector at work 212 ms in the minute
and 3.1 ms at most in a frame; engine memory 2950 MB; mod objects 345 (118 shown) under 6 roots; people 2 out, 12 made of
12; city people 3 out, 8 made of 8, city props made 14; looks built in the minute 1 (0 while playing), since the city
loaded 23 (2 while playing); frames over 80 ms: 2, of them 2 behind the game's black screen (into or out of a building)
and 0 seen; the mod's navigation obstacles changed in the minute 0; handlers put back on the game's events 0 (changed
events looked into 3); searches of the whole scene 0 (0 while playing); the mod's own scripts: 0.41 ms a frame on
average and 3.2 ms at most in a frame, the most in the minute: Transport: TransitDirector.Update (0.12 ms a frame, 2.0 ms
at worst), Traffic: SolidStreets.Update (0.06 ms a frame, 0.9 ms at worst), People: ActorPool.Update (0.04 ms a frame,
0.3 ms at worst); the heap grew under them by 14 KB a second (every frame counted) of the 590 KB a second it grew in
all, the most under Transport: TransitDirector.Update (6 KB a second), ...; on foot in the street, game hour 9.
```

| part | what it is | what to look for over a session |
|---|---|---|
| frames, average, worst | the minute's frame times | the average is the frame rate; the worst is the minute's longest stall |
| over 33 ms, over 50 ms | frames longer than that in the minute | these are the stutters: how many a minute, and whether the number grows |
| managed heap A of B | `GC.GetTotalMemory(false)` and the size the heap has grown to (`Profiler.GetMonoHeapSizeLong`) | B only ever grows; if it grows minute after minute something allocates on a timer (the game's code and every mod share this heap) |
| collections in the minute | `GC.CollectionCount(0)` since the last line | more collections a minute at the same place and speed means more garbage a second |
| grown by N MB in the minute | the allocation budget: `GC.GetTotalMemory(false)` read every frame, and every rise added up (what a collection gives back in the same frame is not seen, so it is a little under what was allocated). The game's share and every mod's together | about 35 MB a minute in a busy city with the mod as it is now. A version of the mod that raises it by megabytes allocates on a timer somewhere |
| the collector at work, and at most in a frame | the engine's own `GC.Collect` time (a profiler counter this build offers), summed over the minute and its largest frame | the game's collector works in slices of 3 ms (`gc-max-time-slice=3` in boot.config): "at most" near 3 ms is the collector as intended; tens of milliseconds is a collection that could not be sliced |
| engine memory | `Profiler.GetTotalAllocatedMemoryLong`: meshes, textures, materials, audio | a steady climb between two midnights is normal for the game (it frees unused assets once a game day, at midnight); a climb across days is a leak |
| mod objects (shown) under N roots | every object under a root object named `RealLifeOverhaul...`, and how many of them are switched on | everything of the mod's is pooled: the count rises while each kind of scene is met for the first time and then stands still. If it climbs all session, something is made per use and never put away |
| people, city people, city props | the two pools of characters (12 and 8) and the living city's prop shelf | "made" stops at 12 and 8; "out" goes up and down |
| looks built (while playing) | characters' looks built, a mesh merge each (`People/Looks`): in the minute and since the city loaded; in brackets those built while the game was neither loading nor paused | about twenty at the load, none of them while playing. After that a few an hour is the pools changing worn looks, one at a time. Several "while playing" in one minute, minute after minute, means scenes are asking for looks nobody on the shelf has |
| frames over 80 ms: N, behind the game's black screen or its loading screen, seen | the long frames of the minute; "behind the black screen" are those with the game's fade up (`UI.UiFader.isFading`), which is its walk into or out of a building, or its loading screen still there (`UI.Load.LoadingScreen`, which fades out for 0.8 s after the save has loaded) | every walk out of a building is a frame of about 225 ms and every walk in one of 85 to 630 ms, all the game's own and all behind its black screen (see below). "Seen" is the number that matters, and it should be 0 |
| the mod's navigation obstacles changed | carving obstacles of the mod's made, moved or switched in the minute (`Core/MeshCuts`) | a handful when a bus stop, a cart or a cordon comes or goes while the player is on foot near it; never a steady number minute after minute |
| handlers put back on the game's events | `Core/EventHold`: times a handler had to be put on one of the game's event fields, and times a changed field was looked into | a few after a load, then 0. Sixty a minute is a holder putting itself back every second |
| searches of the whole scene (while playing) | `Core/SceneFinds`: every `FindObjectsByType` and `Resources.FindObjectsOfTypeAll` the mod makes | each is a frame of about 110 ms in this game. "While playing" must be 0: what a search finds is found behind the loading screen (`ScenePrewarm`) |
| the mod's own scripts | `Core/HotSpots`: every Update, LateUpdate and FixedUpdate of the mod's and its handlers of the game's events (a new hour or day, a door, the city map, a character's change of look), each named "Feature: Class.Method"; their time a frame on average and in the worst frame, and the three that took the most | well under a millisecond a frame on average. A name whose worst is tens of milliseconds is a stutter of the mod's, and the name says where |
| BizMan: opens, switches and tab changes | `BizPace/BizPaceWatch`: BizMan's business view switched on (an open), another business picked in it (a switch) or another tab (a tab change), each counted with what the mod's handlers added, the worst, the owner's budget (2 ms to an open, 1 ms to the others) and the slowest handler; absent when BizMan was not used in the minute. Each episode is a line of its own, "BizMan check (open at ...)" | the worst under its budget. A name here is the handler to look at; Options > Tests > Check BizMan's speed walks every business and tab and says PASS or FAIL |
| the heap grew under them | what the mod's own scripts allocated: counted exactly in every frame where the runtime counts each thread's allocations (`GC.GetAllocatedBytesForCurrentThread`, found out when the record starts), else read from the heap's size one frame in eight; beside it what the heap grew by in all | the mod's share of "grown by". The three names under which it grew the most are where to look first |

A second line a minute says what grew since the first minute of the record, so that a leak names itself (`Diagnostics/ObjectKinds`):

```
Session 12 min, what grew since minute 2: textures 1834 (+40), 612 MB (+35); the mod's holders: Civic Props +120 (160
now), Transit +40 (310 now), Fire Effects +6 (12 now); the kinds below them: Civic Props > cone +80 (100 now), ...; in the
scene kept across loads 412 of the mod's objects (+3) (58 holders and 214 kinds counted); the managed heap after its last
collection 690 MB (+40 since the first collection seen); the mod's log lines in the minute 52, 610 this run (the game
keeps every log line in memory for the whole run).
```

| part | what it is | what to look for over a session |
|---|---|---|
| textures N (+n), M MB (+m) | the engine's own count and size of the textures it does not stream (`Texture.nonStreamingTextureCount`, `nonStreamingTextureMemory`): every texture a script makes is among them | a few dozen more while new places are seen; a rise of the same size minute after minute is textures made and never destroyed |
| the mod's holders | the objects right under a root of the mod's (`Civic Props`, `Transit`, `Fire Effects`), each with everything below it, against the first minute: the three that grew the most | everything is pooled, so a holder grows while new kinds of scene are met and then stands still |
| the kinds below them | the same one level down, by name without its number (`Civic Props > cone`, `Transit > Bus Stop`) | the kind that keeps growing is the leak, by name |
| the scene kept across loads | the mod's objects in the engine's scene for objects kept across loads (`DontDestroyOnLoad`), counted for this line alone so the first line's "mod objects" means what it always has | stands still after the first minutes |
| the managed heap after its last collection | `GC.GetTotalMemory(false)` in the first frame after a collection finished: what the heap keeps, its garbage gone, against the first collection the record saw | the first line's heap goes up and down with garbage; this one rises only with what is held. Tens of megabytes an hour, hour after hour, is memory kept somewhere: the game's or the mod's |
| the mod's log lines | lines the mod wrote to the log in the minute and this run (`Meta.Dev.Lines`) | the game keeps every log line in memory for the whole run (`DebugLogCollector`), and goes through all of them for each new one: with Developer diagnostics on, a few thousand an hour |

One line for a frame in which the managed heap grew by 8 MB or more at once (6 a minute at most): how much, how much of it under the mod's own scripts (where that frame was counted) and under which, and the mod's last pieces of work.

One line for a frame longer than 80 ms (12 a minute at most; the rest are in the minute's count):

```
Session: a frame of 231 ms; none of the mod's large pieces of work was under way; where it went: coroutines and timers
204 ms, rendering 14 ms, the rest 13 ms; the collector: no collection (41 so far), at work 0.0 ms, heap 731 MB before and
733 MB after; the game: its screen was black (its own walk into or out of a building: not seen as a stutter),
the player came out of a building in it, not saving, no scene loaded or unloaded; the mod's own scripts in it: 0.6 ms
(Transport: TransitDirector.Update 0.3 ms, People: ActorPool.Update 0.1 ms, Core: GoingPublicWatch.Update 0.1 ms); the
mod's navigation obstacles changed in the second before it and in it: 0; the mod's last pieces of work: "people: a
character being made" (3.1 ms) 41.0 s before, "transit: a bus stop being put out" (not timed) 80.2 s before; on foot in
the street, game hour 7.
```

How to read it:

- **where it went** is the engine's own steps of that frame, timed by `Diagnostics/FrameSteps`. "navigation" is the step in which the engine cuts the walkable mesh: a long frame with its time there is a cut, asked for half a second earlier by a carving obstacle. "coroutines and timers" with the screen black is the game taking the player through a door. "scripts' Update" is some script's Update, the game's or a mod's. "rendering" and "the rest" are the engine's.

  Where the rest is 5 ms or more, the one engine step it was mostly in is named beside it (every step outside the named groups is timed by itself: "PostLateUpdate.UpdateAllSkinnedMeshes", or another mod's step by its type).

- **the mod's own scripts in it** is the time all of the mod's Updates, LateUpdates, FixedUpdates and event handlers took in that frame (`Core/HotSpots`), with the three that took the most.
  Time under "scripts' Update" that is not here is the game's or another mod's.
- **the collector** says whether a collection finished between the record's two looks, how long the engine's collector worked in that frame, and the heap on both sides of it.
- **the game** says whether its black screen was up ("black": drawn at full strength as the frame began, not seen; "fading, the picture showing through": its fade out or back in, 0.2 s each way at a door, seen, and counted as seen in the minute's line), whether the player had just gone through a door, whether it was saving (`SaveGameManager.SavingGameInProgress`) and whether a scene had just been loaded or unloaded.
- **the mod's last pieces of work** are the last three named with `SessionWatch.Mark`, with how long each took where the place that named it also called `SessionWatch.Done`, and when it was, seen from the long frame.

The test "Test: write the session record's lines now" (Options, Tests, under Performance) writes both lines at once, so every part can be read without waiting for a stutter.

"The mod was at" is the last piece of work named with `SessionWatch.Mark("...")` in the three frames before.
`Mark` is a handful of writes into a fixed ring with a constant text and one read of the clock (the same cost with diagnostics on or off).

It is called where the mod does something large in one go: a character made or given a look (`ActorPool.Dress`, `CityPeople.Dress`, the casts), a living-city site set up, a bus stop, a precinct front or a two-wheeler stand put out, an accident, a crime, a fire or a street event starting, the game saving (the mod's own share of the save is timed and written as its own `Session:` line), the performance tweaks listing the loaded materials.

Nearly all of them name it with `using var piece = SessionWatch.Piece("...")`, which is `Mark` and, at the end of the block, `Done`, so the line has its time beside it.
New code that builds something in one frame names it the same way; `Mark` is never called per frame and never with a text put together for the call.

With the step timing on, the frame is cut where the engine itself cuts it (just after it updates the time, `Diagnostics/FrameSteps`), so the parts add up to the frame: the named groups, "waiting for the last frame to be shown", "the rest" of the engine's steps, and "outside the engine's steps".
The line then says what the system saw of the same frame (`Diagnostics/MachineClock`) and, where the build offers them, the engine's own figures for the graphics card, and ends with "so:" and the cause the figures point to (`Diagnostics/FrameRules`):

```
...; where it went: scripts' Update 1 ms, rendering 5 ms, waiting for the last frame to be shown 362 ms, the rest 2 ms,
outside the engine's steps 1 ms; the processor: the main thread ran 16 ms of it, the game 47 ms over all its threads,
the machine's 24 processors 97% busy (other programs 91%); page faults in it: 310; the engine's own figures: not offered
by this build; so: the game waited for the last frame to be shown while other programs held the processor (...).
```

How to tell the causes apart:

- **The graphics card or the display:** the time is under "waiting for the last frame to be shown", the main thread ran little of it and the other programs' share is small. Where the engine gives the graphics card's time, a large one says the card itself was slow; a small one says the render thread or the display.
- **Another program:** the main thread ran little of the frame while other programs held most of the machine's processors (a build, a game, a scan). The time can be under the wait (the render thread kept waiting) or outside the steps.
- **The engine's own work, or a script's:** the main thread ran most of the frame, and the steps above say where.
- **Outside the engine's steps with the processor free:** the window's messages, the system or the driver.
- **Tens of thousands of page faults:** memory was being brought in, paged back from the disk or touched for the first time (a large allocation, a texture coming in).

The thread's and the system's times are counted at the system's clock tick (about 15.6 ms): good for a long frame and the minute's sums, not for one short frame.

The line a minute says the same of the minute: the minute's waits for the last frame to be shown and its time outside the engine's steps (the sum and the worst frame), the share of the minute the main thread ran, the other programs' share of the processor, the page faults, and the causes of its frames over 33 ms counted by the same rules as the long-frame line. Where the build offers the engine's own frame timing (`FrameTimingManager`), it ends with the graphics card's longest frame of the minute in milliseconds; where it does not, it says "the engine's own frame timing: not offered by this build".

What the graphics card's figures are (`Diagnostics/SessionWatch.Timing`): on the long-frame line, the engine's own time for the card and for the render thread, the worst of the last few frames it has figures for (they come a few frames late); on the minute's line, the card's longest frame. They say how long the card took, not what it spent the time on.

### The game's midnight

The game runs every feature's day in the one frame it turns the day in (`GameManager.NewDay` calls the game's hour and day events), and then asks the engine to unload unused assets, which runs as "loading" in the next frame or two. With the record on, a line follows every midnight, whether or not the frame was long, naming every part of the mod's in that frame (a part inside another, such as `Event: EconomyDirector.OnDay` and `Economy: EconomyDirector.OnDay`, counts under both names and once in the total):

```
Session check (the game's midnight, day 10): the mod's scripts took 4.2 ms of the frame the game turned the day in (61 ms
in all); by part (a part inside another counts under both): Event: EconomyDirector.OnDay 2.1 ms, ...; the day's code
left for the frames after: 2 step(s) (Core/DaySpread), 96 log line(s) (Core/DayLines); the day's code
compiled ahead: yes (wanted: under 20 ms once it is): PASS
```

The steps are the work of the midnight that the game does not read in that frame, played in the frames after it (`Core/DaySpread`, `Core/DayPump`); the log lines are the Info lines the day's handlers wrote, held and written a few a frame (`Core/DayLines`), in order. The list of what stays in the frame, and why, is [RLO-PERF-021](../standards/performance.md).

The day's code is compiled ahead a slice a frame, some twenty seconds after each load (`Core/DayWarm`, `Core/CodeWarm`): Mono compiles a method the first time it is called, and before this the first midnight of a session compiled every feature's day in that one frame (148.7 ms of the mod's at the first midnight of a session on 25 Oct 2026, ten to twenty times less at every midnight after). Its line, once it is done:

```
Day warm check (in play, a slice a frame): the mod's code for the game's midnight compiled ahead: 4618 of the mod's
method(s) read, 7888 compiled, 85 refused (...), 282 with no code of their own; 610 ms over 505 frame(s), the most in a
frame 13.1 ms (wanted: a slice of 1 ms, a long method alone in its frame): PASS.
```

Options > Tests, Performance, "Prepare the code for the game's midnight now" does the rest at once and writes the same line. A frame of several hundred milliseconds of "loading" right after midnight, with tens of thousands of page faults and a few milliseconds of the mod's, is the game's own `Resources.UnloadUnusedAssets()` at the end of `GameManager.NewDay`: Player.log has the engine's line for it just before ("Unloading N unused Assets ... Total: 934.9 ms (FindLiveObjects ... MarkObjects ...)"). Its time grows with the number of loaded objects and the managed heap; the mod does not start it.

### The city map

The city map pauses the game, so the minute's line leaves its frames out. With the record on, the map's frames are counted on their own, a frame of the map over 80 ms has a line of its own ("on the city map" in its place words), and once the map's camera is back down one line says what the map cost:

```
Session: the city map was open 12.4 s: 731 frames, average 16.9 ms, worst 41 ms, over 33 ms: 2, over 50 ms: 0; the mod's
own scripts 0.21 ms a frame on average and 1.9 ms at most (mostly Core: Driver.LateUpdate 1.2 ms); the frame it opened
in took 96 ms, the mod's own scripts 7.4 ms of it (Core: MapPlaces.OnMap 3.1 ms, ...); the frame it shut in took 58 ms,
...; its camera's way back down 61 frames, the longest 22 ms; frames of the map over 80 ms: 1 (a line each, up to 12).
```

Then the map check (RLO-PERF-022): every part of the mod's that changed something for the map is asked once a frame until none holds anything, which must be within 2 s of play coming back. Its line follows some 120 frames of play later:

```
Map check (after closing): the map was open 12.4 s; far squares: 36 of 58 out (231804 renderers, 231804 as the map
opened), 36 of the 36 out before it out again, in 14 frame(s) after its camera was back; the mod's far things: as in the
street, ...; draw distances: the options' again on the player's camera; ...; the player's camera as before the map (its
layers, far plane and draw distances); every part back 0.21 s (14 frames) after play came back; frames of play 10.4 ms on
average in the 120 after it, 10.6 ms in the minute before; the mod's own scripts 0.32 ms a frame after it, 0.30 before
(wanted: every part back within 2 s, nothing left, the player's camera as before): restored, 0 lingering: PASS
```

A FAIL names the part still holding something and what it holds, or says the player's camera is not as before the map (its layers, far plane or draw distances). A map opened again before the last one's camera was back down is set against the camera before the first opening (the game stops its shutting coroutine, so the draw distances are still the ones set for the way down). The parts and their looks are listed in StandardCheck/MapChecks.cs; the looks are in `Core/MapParts`.

As a city's part of the mod unloads, one more check is written once the menu's scene comes in (RLO-PERF-023): `Culling check (unload): 0 dead cullables in the game's culling lists (N listed) ...: PASS`. A dead entry there is a lamp, tree, bench, sign or people's zone the mod destroyed and left in the game's culling lists, which makes the game throw every frame the moment it builds its culling group again.

What the lines cannot say: which of the game's scripts, or another mod's, a long frame was spent in; how long the mod's own coroutines took (they run under "coroutines and timers" and are not timed one by one); what the graphics card spent its time on (which pass, which shader); and anything about the card at all on a build without the engine's frame timing.

## Clicks and moving furniture

`Diagnostics/ClickWatch` writes a line for a click, with Developer diagnostics on, at most two a second. It reads the game and changes nothing.

- `Click check:` for a left click: the interface under the pointer, what the ray met, the game's target, and whether walking was blocked, the player was indoors, in a vehicle, in a menu, on the map or paused.
- `Move check (right click):` for a right click on a piece of furniture. It says whether the game lifts it to move it. If not, it gives the first reason the game's own right click finds, in the game's order (`Diagnostics/FurnitureRules`). Only "in use", "part of the building" and a piece fixed in a flat come with a notice of the game's. The rest do nothing at all, an activity under way or a panel open among them. For "in use" it says what makes it so: somebody seated on it, a piece on it in use, something under each of its places to sit, a seat at the table kept, or its own in-use mark. Each line also says whether the building lists the piece, and whether the piece's record is the building's own.
- `Move check (place click):` for a left click while a piece is being placed. It says whether the game puts it down there. If not, it says why: a piece of the interface selected, the pointer over the interface, no floor under the pointer, outside a house's plot, blocked floor, or it does not fit. When it does not fit, it names what stands in the way and whose it is: the game's, this mod's, or another mod's by its assembly.
- `Move check (ended):` when a move ends: where the piece is, and whether the building's own record of it, which the save keeps, says the same. `FAIL` there means the move would not be kept, or the piece would be gone after a load. Moves in the interior designer are left out: it keeps a moved piece's place only once its own animation has played.

## Log lines by feature

Each feature describes its own lines where it describes the work that writes them. Search the page for the prefix.

| Prefix | Feature | Where the lines are described |
| --- | --- | --- |
| `Game build:`, `Game contract:` | the guard | [After a game update](../contributing/after-a-game-update.md) |
| `Session` | the session record | [The session record](#the-session-record), above |
| `Click check:`, `Move check (...)` | clicks and moving furniture | [Clicks and moving furniture](#clicks-and-moving-furniture), above |
| `Jobs:`, `Jobs check (...)`, `Careers:` | jobs and employers | [Jobs](../features/jobs.md): gig offers, the cleaner, the drinks cart, a shift across a save |
| `Transit check (...)`, `Transit:` | buses and the subway | [Transport](../features/transport.md#buses): "Lines to judge a run by" |
| `Taxi check (...)` | the taxi by phone | [Taxi by phone](../features/taxi.md) |
| `Accident: departure check (...)`, `People: crew check (...)`, `Cordon:` | accidents and the emergency services | [Street scenes](../features/street-scenes.md#rules-the-street-scenes-keep) |
| `Law check (...)`, `Law:` | precincts and custody | [Law](../features/law.md) |
| `Justice check (...)` | the courts | [Justice](../features/justice.md) |
| `Licences check (...)` | driving licences | [Driving licences](../features/licences.md) |
| `Social check (...)` | the social life | [Social life](../features/social.md) |
| `Family check (...)`, `People check (seat)` | family life | [Family](../features/family.md) |
| `Leisure check (...)` | leisure | [Leisure](../features/leisure.md) |
| `Travel check (...)`, `Travel:` | hotels and holidays | [Travel and lodging](../features/travel.md) |
| `Civic check (...)` | civic life | [Civic life](../features/civic.md) |
| `City life:`, `City life check (...)`, `Clips:` | the living city | [The living city](../features/city-life.md#clips-nothing-played) |
| `Meta check (...)`, `Meta:` | the Guide, the options page, the save tools | [Meta](../features/meta.md) |
| `Navigation:` | the minimap and the GPS | [Minimap and GPS](../features/navigation.md#log-lines-with-developer-diagnostics-on) |
| `Economy:`, `Economy check (...)` | credit and lending | [Credit rating](../features/economy-credit.md#log-lines-with-developer-diagnostics-on), [credit products](../features/economy-credit-products.md#log-lines-with-developer-diagnostics-on) |
| `Ventures:`, `Ventures check (...)` | investors | [Investors](../features/economy-investors.md#log-lines-with-developer-diagnostics-on) |
| `Listing:`, `Listing check (...)` | the market and the listed company | [The stock market](../features/economy-markets.md#log-lines) |
| `Company:`, `Company check (...)` | running a business | [Running a business](../features/economy-company.md#diagnostics) |
| `City economy:` | the city's economy | [The city](../features/economy-city.md#options-tests-diagnostics) |
| `Game UI:`, `Notice check (attach)` | the game's own interface | [The game's own interface](game-interface.md) |
| `Map links:` | places on the city map | [Map places](map-places.md) |
