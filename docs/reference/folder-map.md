# Folder map

Where things are in the repository. The code has one folder per feature under `Assets/Mods/RealLifeOverhaul/Scripts`: the [front door](../../RealLifeOverhaul.Build/README.md#where-the-code-is) has one line for each, and each folder has a `README.md` naming the files to read first. This page has the rest: the mod's own folder, `Scripts/Core` in more detail, and the folders outside the mod.

## The mod's folder (`Assets/Mods/RealLifeOverhaul`)

This is the mod, and the single source of truth: the Unity project and the command-line build compile the same files.

| Path | What it is |
| --- | --- |
| `ModManifest.asset` | id, version, what the Mod Builder packages (it names no asset bundle: the bundles are built by the three asset pipelines) |
| `RealLifeOverhaul.asmdef` | assembly definition and references |
| `Locales/en.json` | every player-facing string, flat keys prefixed reallife_; beside it `pt.json`, `zh-cn.json`, `de.json`, `fr.json`, `es.json` and `ru.json`, see [Texts and translation](../contributing/texts-and-translation.md) |
| `Bundles/reallife_character` | meshes and textures for the player's new looks (built by `CharacterAssets`) |
| `Bundles/reallife_props` | the modelled street props and their texture atlas (built by `PropAssets`) |
| `Bundles/reallife_anim` | the mod's own Humanoid animation clips and how each tool sits in the hand (built by `CityAssets`) |
| `Scripts/` | the code, one folder per feature |
| `thumbnail.jpg` | the Workshop picture, installed with the mod |

## `Scripts/Core`

Entry points, settings, per-save state, shared HUD, the modelled props (PropLibrary) and the plain shapes they fall back to (Shapes), and the parts every feature shares: the guard, the game contract, the save hook, the options and keys, map places, the calendar and prices, and the game's own interface (`GameUi`).

| File | What it is |
| --- | --- |
| `RealLifeOverhaulMod.cs` | the two entry points: `RealLifeOverhaulInit` at game start, `RealLifeOverhaulCity` at each city load ([Architecture](architecture.md#start-up)) |
| `Settings.cs`, `Keys.cs` | every option, and every key the mod acts on ([Key bindings](keys.md#key-bindings)) |
| `Guard.cs`, `GuardCalls.cs`, `FeatureGuard.cs` | the guard round every entry point ([After a game update](../contributing/after-a-game-update.md)) |
| `GameContract.cs`, `GameMembers.cs`, `GameConfirm.cs`, `GameBuild.cs` | the game members read by name, the ones older builds lack, the question box, the build the mod is made for |
| `ModImage.cs`, `ModScan.cs` | the mod's own compiled references, asked of the running game at start |
| `SaveHook.cs`, `SaveLines.cs`, `ModState.cs` | the one handler on the game's save event, unreadable lines kept, per-save state and moods |
| `EventHold.cs` | handlers kept on the game's event fields without taking them off and on |
| `HotSpots.cs`, `SceneFinds.cs`, `SearchPace.cs`, `MeshCuts.cs` | the timing of the mod's scripts, whole-scene searches, navigation mesh cuts ([Performance](performance.md)) |
| `StartRules.cs`, `StartLine.cs`, `LoadHold.cs` | the directors' first loads, one a frame |
| `MapPlaces.cs` | the mod's places on the city map ([Map places](map-places.md)) |
| `GameCalendar.cs`, `CalendarRules.cs`, `Prices.cs` | the calendar and the price level ([The calendar and the price level](calendar-and-prices.md)) |
| `GoingPublic.cs`, `GoingPublicLink.cs`, `SharedCity.cs`, `SharedCityWire.cs` | a Going Public session, whose things are whose, and what the host shares |
| `SpecialBuildings.cs` | the buildings the mod claims as its own |
| `PropLibrary.cs`, `Shapes.cs` | the modelled props by id, and the plain shapes they fall back to |
| `RoadData.cs`, `RoadGraph.cs`, `RouteSearch.cs` | the road data found once per city, and the shared road search |
| `TestRefusal.cs` | why a test button could not do what was asked |
| `GameUi/` | the game's own interface, copied ([The game's own interface](game-interface.md)) |

## The other folders under `Scripts`

The front door's list links every folder's feature page. These folders have no feature page; their `README.md` is where to start:

- `Camera`: on-foot third-person camera, driving chase camera; the game's place markers put right when their target is behind the camera (PoiDirection).
- `World`: enter buildings and vehicles with a key, visual grading; every feature's places on the city map (MapLinks).
- `Life`: random life events (catalogue, no-repeat deck, runner), sickness, hospital.
- `Phone`: callable contacts and their conversations.
- `Diagnostics` and `Performance`: on [Performance](performance.md#the-measuring-folders).
- `Tech`, `Ledger`, `Fame`, `Photos`, `Rivals`, `Street`, `Texts`, `Faults`, `FitOut`, `Offices`, `EndScale`, and in `Economy` the folders `Banking`, `Brands`, `Dealer`, `Estate`, `Online` and `Services`.

## Outside the mod's folder

Unity ignores every folder below: none of them is under `Assets`.

### The asset pipelines

The built bundles are committed, so a code-only build needs neither Unity nor Blender.

| Path | What it is |
| --- | --- |
| `CharacterAssets/` | asset pipeline for the new looks |
| `CharacterAssets/scripts/` | Blender scripts: make the meshes and textures, write them to build/ |
| `CharacterAssets/PreviewProject/`, `preview.ps1` | the game's character rebuilt under HDRP: a look is judged here before it ships |
| `CharacterAssets/UnityProject/` | small Unity project that only turns build/ into the asset bundle |
| `CharacterAssets/build_bundle.ps1` | runs it in batch mode: build/ to Assets/Mods/RealLifeOverhaul/Bundles, then a load check |
| `PropAssets/` | asset pipeline for the street props |
| `PropAssets/scripts/` | Blender scripts: paint the atlas, model every prop, render previews |
| `PropAssets/UnityProject/` | small Unity project that only turns build/ into the asset bundle |
| `PropAssets/build.ps1` | everything: atlas, props, previews, Bundles/reallife_props, load check |
| `CityAssets/` | asset pipeline for the animation clips |
| `CityAssets/scripts/` | Blender scripts: the authoring rig, THE CLIPS, contact sheets, FBX |
| `CityAssets/UnityProject/` | small Unity project: Humanoid import, the bundle, and the tests that prove the clips play |
| `CityAssets/build.ps1` | everything: clips, Bundles/reallife_anim, every clip measured on the game's skeleton, play-mode proof |
| `GarageAssets/` | read-only extraction of the game's vehicle data |
| `GarageAssets/scripts/extract_vehicles.py` | every vehicle prefab: hierarchy, meshes, materials, shader properties (how cars are painted) |
| `GarageAssets/scripts/extract_fit.py` | every car body measured from its triangles: the rows of Scripts/Garage/GarageFit.cs |
| `GarageAssets/scripts/check_locale.py` | the by-hand locale check, for when the CatalogueCheck executable is blocked |
| `JobAssets/scripts/extract_wages.py` | what the game pays and charges, read from its data files: what the gigs were set against ([Jobs](../features/jobs.md#pay-against-the-games)) |

Each pipeline's own `README.md` says how to build it: [CharacterAssets](../../CharacterAssets/README.md), [PropAssets](../../PropAssets/README.md) (every prop with its size and pivot) and [CityAssets](../../CityAssets/README.md) (every clip and how to use one).

### `RealLifeOverhaul.Build`

The command-line build and checks.

| Path | What it is |
| --- | --- |
| `RealLifeOverhaul.csproj` | compiles the same scripts with dotnet and installs the package ([Getting started](../contributing/getting-started.md)) |
| `CatalogueCheck/` | verifies the event and crime catalogues, their dealing, and locale keys |
| `PlannerCheck/` | checks the Planner's game-free parts (see [The Planner](../features/planner.md)) |
| `NavCheck/` | checks the game-free parts of the minimap and the GPS, the shared route search (see [Minimap and GPS](../features/navigation.md)), the bus bays (see [Buses](../features/transport.md#buses)) and the taxi called from the phone (see [Taxi by phone](../features/taxi.md)) |
| `EconomyCheck/` | checks the economy's game-free parts: the rating, the terms, the fee, the credit file and its line in the save, the loans (see [the economy pages](../features/README.md#economy)), and every other feature's game-free rules |
| `ContractCheck/` | checks the mod against a version of the game on disk: the game contract, the compiled references, the game's keys (see [After a game update](../contributing/after-a-game-update.md)) |
| `StandardCheck/` | the rules of the engineering standard (`docs/standards`) that a program can check, against its `baseline.txt` (see [Checks](../contributing/checks.md)) |
| `Shared/ModLanguages.cs` | the mod's seven languages by the game's codes (en, pt, zh-cn, de, fr, es, ru), linked into the checkers; de, fr, es and ru may lack keys, which the game reads in English (its fallback language) |
| `Workshop/locales.py` | the locale check of every language that is there (the gate's "LOCALES OK") |
| `Workshop/` | the Workshop page's texts, [the style sheet](../../RealLifeOverhaul.Build/Workshop/STYLE.md), `check.pl` and the upload steps (`UPLOAD.md`) |
| `Installer/` | the installer for players without the Workshop: `build-installer.ps1` and `RealLifeOverhaul.iss` |
| `TextPack/` | the small mod each RLO Texts pack ships as: it tells Real Life Overhaul where the pack's folder is |
| `docs/` | notes kept beside the build: [[game-ui.md](../../RealLifeOverhaul.Build/docs/game-ui.md)](../../RealLifeOverhaul.Build/docs/game-ui.md) (the game's interface, read before interface work), [other-mods.md](../../RealLifeOverhaul.Build/docs/other-mods.md), and the options page before and after its rework (`options-before.md`, `options-after.md`) |

### The rest

| Path | What it is |
| --- | --- |
| `Assets/Mods/` (other folders) and `docs/SDK.md` | Hovgaard Games' mod SDK template and its example mods, which this repository started from |
| `Assets/Editor`, `Assets/_BaDependencies` | the SDK's editor tools and the game's DLLs imported by its Welcome window (not committed) |
| `docs/` | this documentation |
| `.github/` | the issue and pull request templates |
