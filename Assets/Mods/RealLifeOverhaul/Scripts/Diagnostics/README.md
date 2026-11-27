# `Scripts/Diagnostics`

Development aids: the session record (one line a minute and one for each long frame with Developer diagnostics on), the frame step timing, and the F9 character report.

## Read first

- `SessionWatch.cs`: the session record.
- `ObjectKinds.cs`: the record's count of the mod's objects by holder and by kind, which names what grows in a long session.
- `FrameSteps.cs`: where the frame before this one went, by engine step.
- `SelfTest/`: the in-game self-test, a developer diagnostic that runs a plan of steps (load a save, open every screen of the kit, press, type, assert) with nobody at the keyboard. It does nothing at all unless the file `RealLifeOverhaul/selftest.json` is in the game's data folder when the game starts; `SelfTestEntry.cs` is its one way in, `SelfTestKinds.cs` the table of its steps and `SelfTestRunner.cs` what runs a plan. Offline checks: `EconomyCheck --selftest`.
- `ClickWatch.cs` and `ClickWatch.Moves.cs`: a line for a click, and for moving furniture why the game will not lift a piece or put it down, and whether its move is kept; `FurnitureRules.cs` is their game-free part.

## Documentation

How it works, its rules and its tests: [Logs and debugging](../../../../../docs/reference/logs-and-debugging.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
