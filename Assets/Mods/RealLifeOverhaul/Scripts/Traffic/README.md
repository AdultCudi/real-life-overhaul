# `Scripts/Traffic`

Staged traffic accidents with an ambulance and police response, the tow truck, hit-and-run chases, the player's crash consequences, the reckless drivers and the solid streets.

## Read first

- `TrafficAccidents.cs`: the orchestrator: what happens and when. Its parts: `.Staging` (the collision and the wrecks), `.Motion` (a roll or a shunt), `.Units` (the ambulance and the patrol car), `.WayOut` and `.Leaving` (the units' departure), `.Chase` (the hit-and-run), `.Tests`.
- `EmergencyDriver.cs`: drives a unit along the road points (`.Manner`: how it is told to drive; `.Sense`: its look ahead).
- `SceneCordon.cs`: closes the road round a scene (`.Road`, `.Tape`, `.Walls`, `.CrewWay`, `.Props`).
- `TrafficTools.cs`: what all of these need from the traffic system.

## Documentation

How it works, its rules and its tests: [Street scenes](../../../../../docs/features/street-scenes.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
