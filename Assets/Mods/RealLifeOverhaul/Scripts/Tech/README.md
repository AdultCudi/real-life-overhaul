# `Scripts/Tech`

The tech sector and the professions: tech firms of the player's own, hiring, the infrastructure (data centres and the cloud), the player's professional staff, the city's tech sites and ventures. Each part has its own option and its own line in the save.

## Read first

- `TechDirector.cs`: keeps every part going; each part in partial files of its own (the apps' actions in `.Roles`, `.FirmsApp` and `.Deals`).
- `TechBridges.cs`: what it asks of the others.
- `Firms`: the firms, one folder a part (Hiring, Infra, People, Sites, Venture).
- `Sites/TowerFloors.cs`: the tech companies' towers on the floors core ([Floors](../../../../../docs/features/floors.md)): a building held a tower, a company a floor, the game's own panel at its door and its lift; the lobby and the rest of a company's floor are `TechSites`' own, laid again by the core's lift.

## Documentation

How it works, its rules and its tests: [Wiring (TechLinks.cs)](../../../../../docs/reference/wiring.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
