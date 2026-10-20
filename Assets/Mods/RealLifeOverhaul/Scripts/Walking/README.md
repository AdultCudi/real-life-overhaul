# `Scripts/Walking`

Walking on a key: on foot, one press of the walk key (F3 unless it is changed) puts the player on the game's own walk, and a press again gives back the game's own pace.

## Read first

- `WalkRules.cs`: the toggle, what stops it, the speed put on and given back, the walk cycle (game-free).
- `WalkDirector.cs`: the one component.
- `WalkGame.cs`: the only file that touches the game.

## Documentation

How it works, its rules and its tests: [Walking](../../../../../docs/features/walking.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
