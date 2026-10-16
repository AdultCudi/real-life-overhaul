# Architecture

These rules cover how a feature is put together and how it meets the game. `Scripts/Economy/Company` is a complete example of every one of them.

## The shape of a feature

A feature is one folder under `Assets/Mods/RealLifeOverhaul/Scripts` (50 today). Its files are named by the part they play:

| File | Part | Files today |
| --- | --- | --- |
| `*Rules.cs` | Game-free rules: arithmetic, decisions, limits. Checked offline. | 99 |
| `*Director.cs`, `*Director.*.cs` | Owns the feature's state and its timing. | 131 |
| `*Game.cs` | The one place that reads or changes the game for the feature. | 49 |
| `*Store.cs`, `*State.cs` | The feature's line in the save, and its state. | 35, 20 |
| `*App*.cs`, `*Tab.cs` | Its pages in the game's phone and computer. | 90 |
| `*Text.cs` | Its texts, built from locale keys. | 33 |
| `*Guide.cs` | Its page in the in-game Guide. | 21 |
| `*Bridges.cs` | What it asks of other features, as optional delegates. | 20 |
| `Scripts/Wiring/*Links.cs` | Where two features are joined. | 43 |
| `*Director.Tests.cs` | Its entries under Options > Tests. | 22 |

### RLO-ARC-001: One folder per feature, files named by part

**MUST.** Put a new feature in a folder of its own and name its files by the part they play, as in the table.

Why: you find the rules, the save or the screen of any feature by its file name.

Checked by: review.

### RLO-ARC-002: Rules classes are game-free

**MUST.** Keep a `*Rules.cs` file free of Unity and the game: it names only `System` and `RealLifeOverhaul` namespaces, and takes everything it needs, the day, the money, a random number, as arguments.

Why: a game-free file compiles into the offline checkers, so its every edge is tested without the game ([ADR-0004](decisions/0004-game-free-rules.md)). All 99 rules files are game-free today.

Checked by: StandardCheck.

```csharp
public static int LateFee(int owed, int daysLate)          // good: plain values in, a value out
public static int LateFee(BuildingRegistration building)   // bad: a game type in a rules file
```

### RLO-ARC-003: A director owns state and timing

**SHOULD.** Give a feature one director that holds its state, decides when its parts run and is the only writer of its store. Split a large director into part files by concern.

Why: one owner of the state makes every path through a save, a load and a switch-off findable.

Checked by: review.

### RLO-ARC-004: One Game file meets the game

**SHOULD.** Put every call into the game's code for a feature in its `*Game.cs` file, and keep everything else in plain types of the mod's own.

Why: when a game update changes a member, one file changes.

Checked by: review.

### RLO-ARC-005: Features meet only through bridges

**MUST.** Reach another feature only through a delegate in your feature's `*Bridges.cs` file, with a safe default, filled in by a file in `Scripts/Wiring`. **MUST NOT** name another feature's director, store or game file.

Why: every feature works alone, and switching one off cannot break another.

Checked by: review.

```csharp
// Economy/EconomyBridges.cs
/// <summary>The legal and public record. Default: spotless.</summary>
public static Func<LegalStanding> Legal;

// Wiring/EconomyLinks.cs
EconomyBridges.Legal = ReadLegal;

// Economy/EconomyDirector.cs: the default when nothing is wired
private static readonly Func<LegalStanding> Legal = () => EconomyBridges.Legal != null ? EconomyBridges.Legal() : default(LegalStanding);
```

### RLO-ARC-006: Every feature is registered and can be switched off

**MUST.** Register a new feature with one `Features.Add(...)` statement in `Scripts/Meta/FeatureList.cs`, with its option, presets, save keys (`.Data`), clean-up and Guide page. Its option is on by default unless the maintainer decides otherwise. With the option off, the game MUST behave exactly as without the feature.

Why: the options page, the presets, the Guide, the save tools and the removal of the mod all read this list.

Checked by: review.

### RLO-ARC-007: Every entry point is behind the guard

**MUST.** Run every method that the engine or the game calls inside the mod's guard (`Core/Guard.cs`, `Core/FeatureGuard.cs`):

| Entry point | How |
| --- | --- |
| `Update`, `LateUpdate`, `FixedUpdate` | A `FeatureGuard` field from `Guard.For`, the body in `OnUpdate` inside a `try` that calls `Guard.Fail` |
| A coroutine | Started through `Guard.Routine(...)` |
| A handler on one of the game's events | `Guard.Wrap(...)`, made once and kept in a field |
| A UnityEvent listener | `Guard.Listener(...)`, made once and kept in a field |

Why: a fault in the mod must cost one line in the log, never the game's own frame. 120 of the 122 frame methods follow this pattern, and the other two are the guard's own.

Checked by: ContractCheck.

```csharp
private static readonly FeatureGuard FrameGuard = Guard.For("camera", "ChaseCamera", typeof(ChaseCamera));

private void LateUpdate()
{
    using var hotSpot = HotSpots.Time(HotLateUpdate);
    if (FrameGuard.Off)
    {
        enabled = false;
        return;
    }
    try
    {
        OnLateUpdate();
    }
    catch (System.Exception e)
    {
        Guard.Fail(FrameGuard, e, this);
    }
}
```

### RLO-ARC-008: Game members that may change go through the contract

**MUST.** Read a game member by name only through `Core/GameContract.cs`, and a member that older game builds lack only through `Core/GameMembers.cs`. Open every question box through `Core/GameConfirm.Show`. Keep each call to a member that may change in a small method of its own.

Why: Mono compiles a whole method the first time it runs, so one missing member fails the whole method; a small method fails alone, and the contract says so once.

Checked by: ContractCheck.

### RLO-ARC-009: Fail safe

**MUST.** When a feature meets a fault it cannot handle, switch that feature off for the session with one log line and leave the game as it was. When a game member a feature needs is missing, switch the feature off before it runs.

Why: a player's game must survive a game update the mod was not made for.

Checked by: review, ContractCheck.

### RLO-ARC-010: Give back what you borrow

**MUST.** Give back everything taken from the game (a car, a pedestrian, an employee, a screen) as it was found, on every path: the timer's end, saving, loading, going indoors, sleeping, fast travel, unloading the scene and the option switched off midway.

Why: anything left changed is written into the player's save.

Checked by: review.

### RLO-ARC-011: No Harmony patches

**MUST NOT** add a Harmony patch or a reference to Harmony. The one exception is the shared city's receive prefix on Going Public's own copy of Harmony, found by name at run time in `Core/GoingPublicLink.cs`.

Why: patches break silently on a game update and clash with other mods ([ADR-0001](decisions/0001-no-harmony.md)).

Checked by: StandardCheck.

### RLO-ARC-012: No dependency beyond the game

**MUST NOT** add a package, a project or an assembly reference to `RealLifeOverhaul.csproj` other than the game's own assemblies under `$(GameManaged)`.

Why: the game loads one DLL per mod folder, and anything else would have to ship beside it.

Checked by: StandardCheck.

### RLO-ARC-013: The game's own interface only

**MUST.** Build every screen, panel, notice and question from the game's own interface through `Scripts/Core/GameUi` (`AppShell`, `PhoneApps`, `InfoBox`, `DeskList`, `DoorPanel`, `WorldPanel`, `TravelList`, `GuideMark`, `UiClone`, `GameFont`, `GameSprites`). Start what a player does at a place by clicking the thing at that place, in the game's own panel. If the game has no control for something, do without it.

Why: a player should not be able to tell the mod's screens from the game's ([ADR-0002](decisions/0002-game-ui-only.md)). Read `RealLifeOverhaul.Build/docs/game-ui.md` first.

Checked by: review.

### RLO-ARC-014: Per-save state under reallife: keys

**MUST.** Keep a feature's state in the save's `modData` under its own key, `reallife:<feature>`, written by its store:

1. Begin the line with a version.
2. Append new fields at the end of a record.
3. Load a save without the new fields.
4. Keep a line no store can read exactly as it was read (`Core/SaveLines`).

Name phone contacts `reallife-<name>`.

Why: a save must load with the mod removed, an older version or a newer one, and the removal tool finds the mod's data by these prefixes ([ADR-0003](decisions/0003-save-under-reallife-keys.md)). The mod writes 141 keys today, all under `reallife:`.

Checked by: StandardCheck.

```csharp
public const string Key = "reallife:economy_company";   // good
public const string Key = "company";                     // bad
```

### RLO-ARC-015: Derive rather than save

**SHOULD.** Work a value out from the game's state when you can, rather than saving it.

Why: a saved copy can disagree with the game after a load, a game update or another mod.

Checked by: review.
