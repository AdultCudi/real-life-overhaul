# Checks

The mod has six checker programs and a locale check. Together with a build that has no warnings they are the gate: a change is merged only when all of them pass. None of them starts the game. Four need nothing but .NET; ContractCheck reads the game's files on disk.

## Run them

From the repository's root, build a checker and run its DLL:

```
dotnet build RealLifeOverhaul.Build/<Checker> -c Release
dotnet RealLifeOverhaul.Build/<Checker>/bin/Release/net10.0/<Checker>.dll
```

`<Checker>` is `CatalogueCheck`, `NavCheck`, `EconomyCheck`, `PlannerCheck`, `ContractCheck` or `StandardCheck`. Build the mod first for ContractCheck (see [Getting started](getting-started.md#2-build)), and give it the game's `Managed` folder as its first argument or in the `BA_MANAGED` environment variable. The locale check is `python RealLifeOverhaul.Build/Workshop/locales.py`.

## What each one proves

| Checker | What it proves | Passes with |
| --- | --- | --- |
| `CatalogueCheck` | The life events' and crimes' catalogues and their no-repeat dealing; every locale key the catalogues and the sources name exists in `en.json`; the gig offers (`GigCheck.cs`) and a shift saved mid-way (`SavedShiftCheck.cs`); the notifications' kinds and their length, 240 characters at most (`NoticeCheck.cs`); the options page and the **Settings** app (`OptionsCheck.cs`, `SettingsAppCheck.cs`); the employers' offices; the names of business types | `ALL CHECKS PASSED` |
| `NavCheck` | The game-free geometry: the minimap and the GPS, the road search, bus bays, the taxi, the ambulance crew's way, the cordon, the tow truck, the police station's walkways, the fit-out of the mod's buildings, the first minute after a load, the timing of the mod's own scripts and when the scene may be searched | `ALL CHECKS PASSED (n)` |
| `EconomyCheck` | Every other feature's game-free rules, some thousands of checks: the economy, the company and the rota, careers and study, the tech sector, interiors, Flare, Standing, saves and their round trips, and timings | `ALL CHECKS PASSED (n)` |
| `PlannerCheck` | The Planner's game-free parts: the calendar, order, clashes, reminders across a save and a time skip, repeats and its texts in every language | `ALL CHECKS PASSED (n)` |
| `ContractCheck` | The built mod against the game on disk: the game's build against `GameBuild.MadeFor`, every piece of the game contract, every game type and member the compiled mod names, that every entry point, coroutine and delegate handed to the game is behind the guard, and the game's text keys the code names | `ALL CHECKS PASSED (n)` |
| `StandardCheck` | The rules of [the engineering standard](../standards/README.md) that a program can check, read from the repository's files as text. Each finding counts against its rule and its subject (a file, a key, a commit), up to what `RealLifeOverhaul.Build/StandardCheck/baseline.txt` allows, so the code as it stood when the standard was set passes and anything new fails. `-write-baseline` writes the baseline anew; [the testing standard](../standards/testing.md) says when that may be done | `ALL CHECKS PASSED (n)` |
| `locales.py` | Every language beside `en.json`: the same keys in the same order (en, pt and zh-cn in full), the BOM, placeholders, tags and line breaks kept, no dashes, notifications at most 240 characters, and each text in its own language | `LOCALES OK` |

Each checker's `Program.cs` says what it calls, and its `.csproj` which of the mod's game-free files it compiles. `RealLifeOverhaul.Build/Workshop/check.pl` checks `en.json` against [the style sheet](../../RealLifeOverhaul.Build/Workshop/STYLE.md): dashes, stock phrases, jargon, American spellings and the rest. `perl RealLifeOverhaul.Build/Workshop/check.pl -v Assets/Mods/RealLifeOverhaul/Locales/en.json` lists each hit; every count must be 0. A hit a reviewer has accepted goes in `check-allow.txt` beside the script, and a line there that no longer matches a hit counts as stale.

What each check must cover, and when a check is owed, is in [the testing standard](../standards/testing.md).

## How to read a failure

- **A failed check prints one line**, starting `FAIL` (`  FAIL:` in CatalogueCheck), with what was checked. The last line is then `n of m CHECKS FAILED` (or `n CHECK(S) FAILED`).
- **Search the checker's folder for the line's words** to find the check; its name and the words around it say which rule it holds the code to.
- **A failed build of a checker** is a compile error in the mod's game-free files it links. Fix that first: a checker that does not compile would otherwise run its last good build.
- **ContractCheck after a game update** fails on purpose until the mod is brought up to the new game; see [After a game update](after-a-game-update.md), which says how to read each of its sections.
- **A timing check that fails while the machine is busy** (EconomyCheck's and NavCheck's micro-timing checks, such as "a budget of 1 ms") can be load. Run the checker again on an idle machine before you believe it; a pass on a busy machine never excuses a failure.
- **"An Application Control policy has blocked this file"** is Windows refusing a freshly built DLL. Build it again with `--no-incremental -p:Deterministic=false` and run it again.
- **`locales.py`** prints one line per fault, naming the language and the key.

## Writing a new check

Put a feature's pure rules in files with no Unity or game types, and link them into the checker most like them: EconomyCheck for money, business, careers, tech, interiors, Flare and saves; NavCheck for geometry and roads.
Cover the normal cases, every edge (zero, one, none, the largest), determinism, and a save round trip for new fields.

Check every new text's key in all three required locales, and add a timing check for anything that could be slow.
Prove a new check runs by making it fail once.

The companion contract can be checked on its own with `EconomyCheck.dll --companion`.
It uses the production serializer and owner helpers, and holds the synthetic
fixtures in `contracts/companion/v1/` to their generated bytes. See the
[contract README](../../contracts/companion/v1/README.md) for regeneration and the
cross-repository consumer checks.

## Notes on the checkers

`dotnet run --project RealLifeOverhaul.Build/CatalogueCheck -c Release` fails if an event id is duplicated, a locale key is missing, or the no-repeat deck repeats early.
If Windows blocks the unsigned check executable ("An Application Control policy has blocked this file"), build it and run the DLL through the dotnet host instead: `dotnet build RealLifeOverhaul.Build/CatalogueCheck -c Release`, then `dotnet RealLifeOverhaul.Build/CatalogueCheck/bin/Release/net10.0/CatalogueCheck.dll`.
Run it after any change to the event catalogue or `en.json`.

Three more check programs compile the mod's own game-free files and run them on made-up data: `PlannerCheck` (see [The Planner](../features/planner.md)), `NavCheck` (see [Minimap and GPS](../features/navigation.md)) and `EconomyCheck` (see [Economy](../features/README.md#economy)).
NavCheck also covers `Core/RouteSearch.cs`, which the bus network uses.
In `HitchCheck.cs` it covers the keeper of handlers on the game's events, `Core/EventHold.cs`, and the session record's rings, `Core/WorkRings.cs`.

In `HotSpotCheck.cs` it covers the timing of the mod's own scripts, `Core/HotSpots.cs`, and when the whole scene may be searched, `Core/SearchPace.cs`, with the before and after of the hair finish's search on a made-up session.

Each ends with `ALL CHECKS PASSED`.
