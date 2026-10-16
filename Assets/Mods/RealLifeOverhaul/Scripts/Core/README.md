# `Scripts/Core`

Entry points, settings, per-save state, the guard and the game contract, the save hook, the options and keys, map places, the calendar and prices, the modelled props and the plain shapes they fall back to, and the game's own interface (GameUi).

## Read first

- `RealLifeOverhaulMod.cs`: the two entry points: game start and each city load.
- `Guard.cs`: the guard round every entry point.
- `GameContract.cs`: the game members read by name.
- `Settings.cs`: every option.
- `SaveHook.cs`: the one handler on the game's save event.

## Documentation

How it works, its rules and its tests: [Architecture](../../../../../docs/reference/architecture.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
