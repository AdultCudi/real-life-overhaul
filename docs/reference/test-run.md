# The test run

## In a sentence

One button runs every automatic check of the city expansion, puts the player where each check needs them, asks for one load in the middle, puts back what each check changed, and ends with a summary notice and a block in `Player.log`.

## Where the code is

- Folder: [`Scripts/TestRun`](../../Assets/Mods/RealLifeOverhaul/Scripts/TestRun/) (namespace `RealLifeOverhaul.TestRun`); its README lists every file.
- The registry is `Feature.Check(...)` in `Meta/Features.cs`; the expansion's own checks are the table in `RunPlan.cs`, given their work in `RunList.cs`.
- The few test entry points it needs besides the buttons: `NewBuildings/NewBuildingsDirector.TestRun.cs` (the test building asked for on the airport's grass alone, no longer asked for, the hidden buildings shown again) and `Civic/CivicDirector.TestRun.cs` (the sites the construction tests set out taken back).

## Pressing it

Options, Troubleshooting, Diagnostics: switch on **Developer diagnostics** and **Show test tools**. Then Test tools, Troubleshooting, Diagnostics: **City expansion: run every check**, the first button there.

1. A save of the game's own is written first, named `RealLifeOverhaul test run start`, in this character's saves. Loading it undoes everything the run did.
2. Close the options. The run begins once the game runs (the player's own pause is let go, and given back at the end).
3. The checks run one by one. Each is pressed without any input, its line is waited for, and what it changed is put back.
4. A notice says **Save and load now; the run carries on by itself.** Save, then load that save. The run is kept in PlayerPrefs over the load (also over a restart of the game, for twelve hours) and carries on by itself after it.
5. The summary notice: `City expansion test run: N of M PASS; failed: ...`. The block in `Player.log` has one line per check with its result and its line, and at its end a checklist of at most five things only eyes can judge, each saying where the run left the player for it.

Pressed again while it runs, it stops, puts back what it can and says so; the start save undoes the rest.

## The order

By phase, then by place (so the player is moved as few times as can be), then in the order registered (the table first, then the features' own):

- **Before the load**, where the player stands: the held buildings, Uptown's census and path, its district census, a business, its save read back, the district save, the trades' commission, bribe and apprenticeship, and the construction game's tests in its own order. Out of doors: the test office on a floor, the architects' practice, the road census, what is under the player, Uptown's traffic. At a building's door: the scaffolding, a rebuild site, a stage on, the parking lane. On the airport's grass: a frame storey, the crane, a copy, the frame cost. On the grass's pavement: the walking patch, its link, the grown tile.
- **Last before the load:** Uptown stood down in play (its clean-up) and its census of the stand-down, another city and back, the test road and the test building asked for at the next load.
- **After the load:** the test building made, Uptown back with its streets laid, the test road appended, the test building walked into and out of and saved inside, the switch-off and its next hour (the game's own time machine runs the clock on), the cars on the test road.
- **At the end, and when stopped:** the construction game put back, the civic sites taken back, the test road and the test building no longer asked for, the hidden buildings shown again, the player out of doors; then the player at the test building's door for the last look.

## The places

Nobody looks for a spot. Each place is found by the run, and the player is moved there the way the game's taxi moves them (hidden and held, the black screen, the warp onto the walking mesh, turned with the camera behind them, shown again).

| `RunPlace` | Where |
|---|---|
| `Here` | as the player stands |
| `Outdoors` | out of any vehicle and building, by the game's own exits |
| `UptownLot` | the front of the first open lot of Uptown's first block that nothing stands on, facing into it |
| `BesideBuilding` | a step out from the street door of the nearest building of three to six storeys (its shell read from its own colliders, as the scaffolding reads it), not the player's, not one of the mod's |
| `OpenGround` | the front of an open lot on the airport's grass (the test building's six, found free offline), each check its own, never the test building's |
| `PavementEdge` | the pavement at the last of those lots, the same for every walking check |
| `TestBuilding` | the test building's door |

## A feature joins the run

One line in its guide, in its `Features.Add(...)` chain. It is the test's button too:

```csharp
.Check("reallife_landmarks_test_tower", LandmarksDirector.TestTower, "Landmarks check (tower)", RunPlace.UptownLot)
```

- The work is a public static method that needs no input, and writes one line beginning with the line given: `<Feature> check (<test>): ... PASS` or `FAIL`.
- A refusal through `Core/TestRefusal` ends the check at once as REFUSED, with its reason.
- Optional: `RunPhase.ForTheLoad` for what asks for something at the next load and `RunPhase.AfterLoad` for what is read after it; the seconds the line is waited for (60 by default, counted only while the game runs).

## How a line is read

- A check is decided by the first line that begins with its line (after the logger's `[Mod:...]` name): FAIL anywhere fails it; otherwise it passes if it says PASS (or what the check must say, such as COMPLETE; a line with no verdict passes by being written).
- A FAIL of the same family (the words before ` check (`) or a refusal heard in the same moment outweighs a PASS.
- A refusal that finds nothing in the save to bring forward (no project, no company, no tender) is NOTHING: listed apart in the summary, not counted against the mod.
- No line within the check's time is NO LINE; a test that throws is FAULT, and the run goes on.

## What it does not press, and why

`RunPlan.NotRun` lists them: the trades' shifts (mason, electrician, crane operator, inspection, survey, night watch, a design and a tender at the practice's desk) are games of the player's hands; the trades' work at a site opens pages for the player to choose from; Uptown's rent is the player's, in BizMan. The test building and the test road are asked for by steps of the run's own, which ask for exactly what the run needs and take it back at the end.

## Going Public, safety and cost

- Refused in a Going Public session, with Developer diagnostics or the test tools off, and in a city the mod does not know. Nothing is sent.
- Nothing runs in normal play: one PlayerPrefs text read at start-up. During a run, one coroutine on the mod's own persistent object (one time comparison a frame), and a prefix test of each log line on the main thread.

## Offline checks

EconomyCheck's `TestRunChecks.cs` (`--test-run` alone): the order (phases, places, registration, every dependency between checks, a later feature's check in its place's group, the same order from any input), every line read (a line inside another, a longer check's, the logger's name, FAIL, COMPLETE, refusals, nothing to try, a refusal pressed again), the airport lots given (never the test building's or the pavement's), the spot at a lot's front, the carry-over (read back the same, cut, a damaged or older one refused, twelve hours), the summary and the checklist, the registry (every id bound, every label a text, every line written by a source, every test button of the expansion in the run or listed with why), and the sources (no frame method, nothing at start-up but one read, the log heard only during a run, the guard, the safety, the taxi's move, the one game file), and the texts in en, pt and zh-cn.

## Not verified without the game

Every place the run finds (the building of three to six storeys near where the run reaches it, the lots' fronts on the walking mesh, the pavement's empty tile for the grow), the move under the black screen, the save written while the options are open, the load carried over in play and over a restart, and each test's own line in a run. The run itself is the test of these: its summary says which place or line did not come.
