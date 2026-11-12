# `Scripts/Tech`

The tech sector and the professions: tech firms of the player's own, hiring, the infrastructure (data centres and the cloud), the player's professional staff, the city's tech sites and ventures. Each part has its own option and its own line in the save.

## Read first

- `TechDirector.cs`: keeps every part going; each part in partial files of its own (the apps' actions in `.Roles`, `.FirmsApp` and `.Deals`).
- `TechBridges.cs`: what it asks of the others.
- `TechDirector.Seating.cs`, `Firms/SeatRound.cs`, `Firms/SeatPlan.cs`, `Firms/SeatShifts.cs`: a professional at a tech firm without a shift given a free desk and a week of shifts, on the game's hour, newest first; the rounds and the week are game-free, the one file that touches the game's schedule is `SeatShifts` ([New hires](../../../../../docs/features/staffing.md#new-hires-a-role-and-a-seat)).
- `Firms`: the firms, one folder a part (Hiring, Infra, People, Sites, Venture).
- `Hiring`: the labour market, the player's ads, applicants and offers, a search firm's search and its rush, a hire's notice bought out ([Hiring professionals](../../../../../docs/features/tech-hiring.md)).
- `Studio`: the game studio under Living population, from the concept at the desk to its life after launch ([The game studio](../../../../../docs/features/game-studio.md)).
- `Titles`: the names of products and games: the rules a typed name is cleaned by and the made-up names of each kind and genre, the player's and the rivals', with the director's part in `TechDirector.Titles.cs` ([The game studio](../../../../../docs/features/game-studio.md), Names).
- `Sector`: the other tech firms under Living population: shared markets from the city, rivals by the same rules, the city's IT work, data centres' demand and fourteen desk sessions ([The tech firms' markets](../../../../../docs/features/tech-firms.md)).
- `Venture`: the startups and the player's firms raising; `FirmAccount.cs` is the money the investors put in, the floor the wallet must keep, the board and the wind down ([Startup money](../../../../../docs/features/startup-money.md)).
- `Sites/TowerFloors.cs`: the tech companies' towers on the floors core ([Floors](../../../../../docs/features/floors.md)): a building held a tower, a company a floor, the game's own panel at its door and its lift; the lobby and the rest of a company's floor are `TechSites`' own, laid again by the core's lift.

## Documentation

How it works, its rules and its tests: [Wiring (TechLinks.cs)](../../../../../docs/reference/wiring.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
