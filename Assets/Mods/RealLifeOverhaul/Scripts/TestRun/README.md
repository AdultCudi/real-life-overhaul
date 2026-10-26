# `Scripts/TestRun`

The city expansion's test run: one button under Options, Troubleshooting, Test tools, Diagnostics ("City expansion: run every check") that runs every automatic check of the expansion by itself, puts the player where each needs them, asks for one load in the middle, puts back what each changed and ends with a summary. A developer tool: players never see it.

## Read first

- `RunRules.cs`: what a check is, how a log line is read, the order, the airport lots, the carry-over and the summary (game-free).
- `RunPlan.cs`: the expansion's own checks as data, and the tests it does not press with the reason (game-free).
- `RunList.cs`: each check's work, and the run's own steps (Uptown stood down in play, what is asked for at the load, the put backs).
- `RunDirector.cs`: the run's state and timing; `RunGame.cs`: the only file that touches the game.

## A feature joins the run

One line in its guide, in its `Features.Add(...)` chain: the test's work is a public static method that needs no input and writes one line `<Feature> check (<test>): ... PASS` or `FAIL`.

```csharp
.Check("reallife_landmarks_test_tower", LandmarksDirector.TestTower, "Landmarks check (tower)", RunPlace.UptownLot)
```

## Documentation

How it works, its places, its rules and its checks: [The test run](../../../../../docs/reference/test-run.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
