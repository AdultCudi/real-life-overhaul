# Crime

## In a sentence

Crime near the player, by the player and against the player: street crimes played out with a cast and an arrest, crimes rolled off screen, and the police noticing the player's own offences.

## Where the code is

- Folder: [`Scripts/Crime`](../../Assets/Mods/RealLifeOverhaul/Scripts/Crime/), with the scenes in [`Scenes`](../../Assets/Mods/RealLifeOverhaul/Scripts/Crime/Scenes/).
- Read first: `CrimeCatalogue.cs` (every crime, its odds and lines; fines and odds in `CrimeLaw`), `CrimeDirector.cs` (one scene at a time), `CrimeScene.cs` (the base every scene shares) and `PoliceAttention.cs` (the player's own offences).

| File | What it is |
| --- | --- |
| `CrimeCatalogue` | every crime: where and when it can happen, weight, endings with odds, its lines; fines and odds (CrimeLaw) |
| `CrimeDeck` | deals crimes, endings and lines without repeats (game-free, like LifeEventDeck) |
| `CrimeDirector` | one scene at a time: when, which, ticking it, the marker, 911, the test buttons |
| `CrimeScene` | ICrimeScene and the base every scene shares: cast of four, steps, arrests, clearing up |
| `CrimeScenes` | which scene plays which catalogue entry: one line per crime |
| `Scenes/` | the scenes: FootChase, Questioning, Disturbance, Snatch, Mugging, Assault, DrugDeal, LoneOffender (graffiti, car break-in, drunk, vendor, panhandler, fare dodger), CarPursuit (traffic stop, reckless driver, car theft), ParkingTicket, OwnShopTheft, ShopIncident (shoplifter, hold-up), PlayerPickpocket, PlayerMugging |
| `ArrestSequence` | the one arrest every scene uses: hands up, cuffed, to the kerb, into a patrol car, away |
| `CuffPose` | hands behind the back, re-aimed after the animator each frame while (and only while) someone is cuffed |
| `Crime` | money, toasts and small helpers shared by all of the above |
| `CrimeCars` | finds a parked car to stand beside or an ordinary traffic car to take over |
| `Knockdown` | one of a scene's people knocked off their feet (RagdollSystem) and up again |
| `ShopFloor` | what indoor scenes read from the shop: doors, shelves, camera cover, the guard |
| `OffscreenCrimes` | overnight break-in, parked car broken into, scam call: rolled once a day, all capped |
| `PoliceAttention` | the player's own offences: seen or not, pulled over, ticket, pursuit, escape, custody |
| `PlayerOffences` | notices red lights, speeding, pavement and wrong-side driving, jaywalking (timed, player only) |
| `RoadMap` | stop lines and lanes from the traffic system in a grid, built once per city load |

## How it talks to other features

- A crime is one of the large street scenes ([`Wiring.Scenes`](../reference/wiring.md#one-street-scene-at-a-time-wiringscenes)) and casts its people from the shared pool ([Street scenes](street-scenes.md)).
- `PoliceAttention` and `Traffic/PolicePursuit` catch the player and charge the roadside fines once; Law and Justice take it from there. See [Who does what at an arrest](../reference/wiring.md#who-does-what-at-an-arrest).
- Every staged crime is reported to the paper with `News.Report`.

## Adding a crime

See [Adding a crime](../contributing/adding-things.md#adding-a-crime).
