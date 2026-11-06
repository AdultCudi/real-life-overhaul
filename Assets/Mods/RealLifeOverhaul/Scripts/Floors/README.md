# `Scripts/Floors`

The mod's places on floors of the buildings it holds: the university's classrooms, the tech companies' towers, and every place to come, which takes a floor and not another of the city's buildings.

## Read first

- `FloorsDirector.cs`: the one component, and what holders and features call (`Hold`, `Let`, `Ask`, `Release`, `Where`, `Enter`, `Lift`).
- `FloorPlace.cs` and `FloorHost.cs`: a place on a floor, and a building held, as a feature declares them.
- `FloorsRules.cs` and `FloorLoan.cs`: which place goes on which floor, and what each of the game's steps gives back (game-free).
- `FloorsGame.cs`: everything it reads from or does to the game.

## Documentation

How it works, its rules and its tests: [Floors](../../../../../docs/features/floors.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
