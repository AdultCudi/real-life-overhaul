# DLC and update readiness

Hovgaard keeps adding to Big Ambitions: free updates that add districts and areas (the Hamptons came this way in 1.0; the roadmap of 16 September 2026 plans Liberty Island, a Midtown Mall behind StockCo and a JFK Airport area) and paid DLCs, the first of them a second city, Shanghai, reached by air from JFK. The game already carries what a DLC needs: `SteamAPI.DLC` (HGPlugins), `Building.requiredDLC` and `BoatType.requiredDLC`, the refusal "common_requires_dlc" at a door and in BizMan, and `ToggleObjectDLC`, which shows or hides a scene object by ownership. These rules keep the mod working when that content arrives. The steps to take when it does are in [after-a-game-update.md](../contributing/after-a-game-update.md#new-content-and-dlcs).

| Piece | Where |
| --- | --- |
| Is the loaded city the New York the mod knows? | `Core/CityCheck` (game), `Core/CityRules` (its decision table, game-free) |
| New York's districts, and their order | `Core/CityDistricts` |
| Which features stand on New York's data | `Meta/FeatureCities` (the city table) |
| May the mod pick this building or boat? | `Core/GameDlc` (`Gated`, `Open`) |
| What the installed game holds | `RealLifeOverhaul.Build/Census/game_census.py`, and ContractCheck's section "the game's districts, DLCs and scenes" |

### RLO-DLC-001: Nothing a DLC keeps is picked, and no pick reads ownership

**MUST.** Pick from the game's buildings only through `Core/GameDlc`: CityManager's list through `GameDlc.Open(city)`, any other building, registration or boat type through `GameDlc.Gated(...)`. **MUST NOT** read `requiredDLC`, `DlcIsOwned`, `SteamAPI` or `SteamApps` anywhere else.

Why: players of one Going Public session may own different DLCs, and the game hides objects by ownership, so CityManager's list can differ between their machines; a pick made without what a DLC keeps is the same on every one, and the mod never sends a player to a door the game refuses them.

Checked by: ContractCheck (the DLC rule in the sources; every pick of the census names the helper).

### RLO-DLC-002: Every feature says whether it stands on New York's data

**MUST.** Give every feature a line in `Meta/FeatureCities`: `NewYork` when it claims or keeps a building of New York's, keeps a table by New York's districts, runs between the game's subway stations or keeps something measured from New York; `AnyCity` otherwise.

Why: in another city the New York features stand down (`Core/CityCheck`: their components are not started and their guards are away) and the rest run; a feature with no line would run on data that is not there.

Checked by: ContractCheck (every `Features.Add` and every guard's feature has a line; every line is a feature).

### RLO-DLC-003: A district the mod does not know never throws

**MUST.** Give a district id the mod does not know a default or leave it out, in every table by district: no array index from a fixed list without a bounds test, no dictionary read without `TryGetValue`, a `switch` with a default. Put New York's seven first with `CityDistricts.KnownFirst` or `CityDistricts.Ordered` wherever a place or a saved index follows the order, and read the loaded city's districts with `CityCheck.Here` or `CityCheck.NewYorkDistricts`, never the game's whole list alone.

Why: a free update adds districts, and the game's data may one day hold another city's; a new district must not take a precinct from one of New York's, shift a saved ward, or leave a business without internet.

Checked by: EconomyCheck (`CityReadyChecks.cs`: precincts, wards, internet, broadband, hotels, City Utilities with a new district).

### RLO-DLC-004: Picks are made again at every load from the game's data

**MUST.** Make a place's building choice from the city as it is at the load, not from a list kept in the code. Leave out a building the game uses for itself (`SpecialService`), one behind a private fence and one a DLC keeps. A saved place whose building is gone or no longer fits moves, or ends, with one log line, and a notice when the player could tell.

Why: the Midtown Mall and the JFK Airport area will take buildings and ground the mod may hold today; a place that is simply picked again moves out of their way.

Checked by: review, EconomyCheck (the places' own rules).

### RLO-DLC-005: The city check runs once per city load and costs nothing per frame

**MUST.** Decide the city in `Core/CityCheck` only: once in the city part's start, again in the game's late "game loaded" call, and once after a scene changed under the running city part. **MUST NOT** ask what city this is per frame; read `CityCheck.NewYork`, `CityCheck.Kind` or `CityCheck.Here`.

Why: the verdict changes only when a city loads; every guard of a New York feature carries it already.

Checked by: review.

### RLO-DLC-006: An update's new content is read before the mod is made for its build

**MUST.** After a game update, run the census and ContractCheck against the new build before raising `GameBuild.MadeFor`, and read every new district, street, scene, DLC and gated building they name.

Why: ContractCheck fails on a district, scene or DLC the mod does not know, so the gate stops until somebody has decided what each means for the mod.

Checked by: ContractCheck (the game's districts, DLCs and scenes).
