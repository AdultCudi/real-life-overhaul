# Architecture

How the mod starts, how a feature is laid out, how features meet, and what keeps the game and the save safe. Read this before you write your first feature; the numbered rules for a pull request are in [the rules](../contributing/rules.md).

## Start-up

The mod has two entry points, both in `Scripts/Core/RealLifeOverhaulMod.cs`:

1. **`RealLifeOverhaulInit`**, once when the game starts. It reads the settings, registers the options page and runs `Guard.Start`: the game contract is resolved, the game's build is compared with `GameBuild.MadeFor`, and every game member the mod's compiled code names is asked of the running game. See [After a game update](../contributing/after-a-game-update.md).
2. **`RealLifeOverhaulCity`**, each time the city loads. It makes the mod's root object (`RealLifeOverhaul`), adds each feature's director to it, finds what each feature needs from the scene behind the loading screen (`ScenePrewarm`), and last of all fills in the bridges between features (`Wiring.Connect`).

After a save loads, the directors' first loads run one a frame in a fixed order that keeps their dependencies (`Core/StartRules.cs`, `Core/StartLine.cs`). Every step that can fail is a `Guard.Step`: one that fails costs the mod only itself.

## The director pattern

Every feature is laid out the same way, in its own folder under `Scripts/`. `Scripts/Economy/Company` is a complete example to copy.

| Layer | File names | What it holds |
| --- | --- | --- |
| Rules | `*Rules.cs`, `*Catalogue.cs`, `*Model.cs` | Pure rules and figures with no Unity or game types, checked offline by a checker |
| Director | `*Director.cs` and its partial files | The one component that owns state and timing: a timer, the game's hour and day events, the save |
| Game | `*Game.cs` | The only place that touches the game's API |
| Interface | `*App*.cs`, `*Tab.cs`, `*Panel.cs` | Pages built from copies of the game's own interface |
| Texts | `*Text.cs` | Locale keys in, lines out |
| Guide | `*Guide.cs` | The feature's entry in the registry and its Guide page's facts |
| Save | `*Store.cs`, `*State.cs` | The feature's line in the save |
| Tests | `*Director.Tests.cs` | The test buttons on the options page |
| Bridges | `*Bridges.cs` | What the feature asks of others, each with a default |

## Wiring and bridges

Every feature works alone. It talks to the others only through small optional bridges, each with a default that works when nobody fills it in. `Scripts/Wiring` is the one folder that fills them in, one small file per pairing; nothing there has an `Update`. The full list, and who answers each bridge, is in [Wiring](wiring.md).

## The save

- A feature keeps its state in the save's `modData` under its own key, `reallife:<feature>`, with a version. The keys are listed in [Save keys and ids](keys.md).
- New fields are appended at the end of a record, so a save without them still loads.
- A line no store can read is kept in the save as it was read (`Core/SaveLines.cs`).
- One handler on the game's save event serves the whole mod (`Core/SaveHook.cs`).
- Phone contacts the mod adds have ids starting `reallife-`, notifications keys starting `reallife_`, and the mod's moods types starting `reallife_happinessmodifier_`. The save tools remove them all; see [Save tools](../features/meta.md#save-tools-what-is-removed-and-how-that-was-checked-against-the-game).
- Nothing goes into the save that the base game cannot load without the mod.

## The guard and feature guards

Every entry point the game or the engine calls (an `Update`, a coroutine, a handler of one of the game's events, a button) runs inside the mod's guard: `Core/Guard.cs`, `Core/GuardCalls.cs` and `Core/FeatureGuard.cs`.
A fault is logged and the feature stands down; it never breaks the game's own screen or frame.

Game members read by name are lines of the game contract (`Core/GameContract.cs`), and members an older game lacks are read only through `Core/GameMembers.cs` and `Core/GameConfirm.cs`.
ContractCheck fails the gate on anything left outside.

How each part works is in [After a game update](../contributing/after-a-game-update.md); how faults show in the log is in [Logs and debugging](logs-and-debugging.md#faults-and-features-that-switch-themselves-off).

## Per-frame work

Idle, a feature costs one time comparison a frame. The rules, what was measured and the per-frame cost of every component are in [Performance](performance.md).

## Going Public

Going Public is another author's mod that lets several players share one city. A change must stay safe in a Going Public session: deterministic across machines, nothing new sent over the network that an older or absent copy of this mod cannot read, and nothing changed that belongs to another player. `Core/GoingPublic.cs` says whether a session is on (`InSession`) and whose things are whose (`Mine`, `MineVehicle`); `Core/SharedCity.cs` shares the parts the host decides.

## The game's own interface

The mod draws no canvas, panel or prompt of its own: it copies the game's interface. How, and with which building blocks, is in [The game's own interface](game-interface.md).

## Rules the code follows

These are the rules as they were written for the code; the short list is in [the rules](../contributing/rules.md).

Performance is a requirement, not a nice-to-have.

- No `KeyCode` outside `Core/Keys.cs`: test a key with `Keys.Down(action)` or `Keys.Held(action)` (one engine call each), and build any text that names a key with `Keys.Label(action)` (cache it; `Keys.Version` goes up when a binding changes).
  A new key is one `KeyAction`, one row in `Keys.All` and its name in `en.json`.

- Nothing allocates per frame.
  No `OnGUI`, no LINQ or string building in `Update`, no `FindObjectsOfType` outside a one-off setup.
  Put anything on screen through the game's own interface (see [The game's own interface](game-interface.md)); there is no shared canvas of the mod's.
  The one font asset others read is `GameUi.GameFont.Asset`.

- A handler on one of the game's event fields is kept there with `SaveHook.Keep` (the save) or `EventHold.Keep` (any other field: entering and leaving a building, the map, the game's events), never by taking it off and putting it back on a timer: each of those builds a new list of the game's handlers.

- A feature is one component under `Scripts/<Feature>/`, registered in `Core/RealLifeOverhaulMod.cs`, with a toggle in `Core/Settings.cs` and its strings in the locales (`Locales/en.json`, `pt.json` and `zh-cn.json`).
- Anything added to a pooled game object (traffic cars) must be removed again.

- Anything changed on a game object (materials, renderer flags, lights, textures) is recorded and put back when the option is switched off and when the scene ends.
  A feature that meets something unexpected logs once, restores the game's own state and stays off; it never throws per frame.
- A question goes through `GameConfirm.Show` (the same parameters as the game's `HudConfirm.Show`), and the members an older build of the game lacked through `Core/GameMembers`; nothing else names them (ContractCheck fails if it does).
  A new member of the game that may not be in every build is read the same way: in a small method of its own, inside a try whose catch asks `GameMembers.Missing` (see [After a game update](../contributing/after-a-game-update.md)).

- Never leave data in the savegame that the base game cannot load without this mod (see the comment on `Core/ModState.cs`).
- Props the mod builds itself (cordon, stretcher) are made once, kept switched off and reused.
  Nothing is built or destroyed per scene.
  Their meshes and materials come from `Core/PropLibrary.TryGet` (modelled props from the bundle: shared meshes, two shared instanced materials on one atlas, no colliders) with `Core/Shapes` as the fallback when it says no (shared plain meshes, one material per colour).
  Never a material or a property block per instance.

- A test button that cannot do what was asked says why: `Core/TestRefusal` (one enum value, one sentence in `en.json`, one log line with what was measured).
  Never one sentence for several causes.

- New motion is a Humanoid clip from `CityAssets`, played through `ActivityAnimator`; never a pose set on bones by hand.
  Somebody who is walking keeps the game's own walk and carries with a masked clip (`PlayOver`); a whole-body walking clip is only for where the game's walk will not do, and then its speed is matched to the ground (`MatchSpeed`) from a timer, never per frame.

  A character that is about to become a ragdoll has its clip player disposed first.
