# `Scripts/NewGround`

New ground: ground the mod lays itself for a district of its own, laid at every load and never saved. The city's two walking meshes grown over it, streets appended to the game's traffic before it starts, the game's parking lanes copied along its kerbs, pedestrian zones and crossings, and bus termini. Uptown is laid with it; on its own it has developer tests.

## Read first

- `RoadRules.cs` and `RoadPlan.cs`: the road generator and the checks every build passes (game-free).
- `GroundRules.cs` and `GroundShapes.cs`: the walking side's tiles, boxes, samples and self-check lines; the tiles as rectangles, the agent's margin at the ground's edge, what stands on it cut out, the old mesh's islands (game-free).
- `GroundWork.cs`: the front door a district lays its streets and ground through.
- `WalkGame.cs` (with `WalkGame.Build.cs`) and `RoadsGame.cs`: the only files that call Unity's navigation and write the traffic's road data.

## Documentation

How it works, how a district uses it, its rules and its tests: [New ground](../../../../../docs/features/new-ground.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
