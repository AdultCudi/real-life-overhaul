# Testing

Nobody can run every situation in the game for every change. The mod is proved three ways: offline checks of its game-free code, self-check lines it writes in the game, and in-game tests a player or the maintainer can press. These rules say what each must cover.

## The checkers

Each checker is a console program in `RealLifeOverhaul.Build` that compiles the mod's own game-free files, so it checks exactly what ships. Its last line is `ALL CHECKS PASSED (n)` or `m of n CHECKS FAILED`.

| Checker | What it checks |
| --- | --- |
| `EconomyCheck` | Every game-free rule not listed below: the economy, the company, careers, tech, interiors, Flare, Standing, saves and timings |
| `NavCheck` | Geometry, roads, routes, the bus, the tow truck, handlers on the game's events, hot-spot timing |
| `PlannerCheck` | The Planner's model, store, rules and texts |
| `CatalogueCheck` | The life event and crime catalogues, options, presets, tips, notifications and their length |
| `ContractCheck` | The mod against the installed game: every game type and member it names, the guard on every entry point, the game's keys, the game's build |
| `StandardCheck` | The rules of this standard that a program can check |
| `Workshop/locales.py` | Every translation against `en.json`: keys, order, byte order mark, placeholders, dashes; ends `LOCALES OK` |
| `Workshop/check.pl` | Text style faults in the files you give it |

358 of the mod's 1,315 source files are compiled into at least one checker today, and all 99 rules files and all 35 stores are among them.

### RLO-TST-001: Game-free code is compiled into a checker

**MUST.** Put a feature's arithmetic and decisions in `*Rules.cs` files ([RLO-ARC-002](architecture.md#rlo-arc-002-rules-classes-are-game-free)) and compile every `*Rules.cs` and `*Store.cs` file into at least one checker, with a `<Compile Include=... Link=...>` item in its project file.

Why: a rule that is not compiled into a checker is checked by nobody.

Checked by: StandardCheck.

```xml
<Compile Include="..\..\Assets\Mods\RealLifeOverhaul\Scripts\Planner\PlannerRules.cs" Link="Mod\PlannerRules.cs" />
```

### RLO-TST-002: The checker most like the code

**SHOULD.** Add new checks to the checker whose subject is closest, from the table above. Make a new checker only for a new kind of subject, and add it to the gate in the same change ([RLO-TST-009](#rlo-tst-009-the-gate-decides)).

Why: each checker builds in seconds because it compiles only what it checks.

Checked by: review.

### RLO-TST-003: What a check covers

**MUST.** Cover, for every new rule:

1. The normal case.
2. Every edge: zero, one, none and the largest.
3. Determinism: the same inputs give the same answer.
4. A save round trip for every new saved field, and an older save without it.
5. Every new text's key in `en.json`, `pt.json` and `zh-cn.json`.

Why: these are the cases play reaches least often.

Checked by: review.

### RLO-TST-004: Check names and messages

**MUST.** Put a checker's checks for one subject in a file named `<Subject>Checks.cs`, as a part of `internal static partial class Program`, called from `Main`. Word each check's message as what was measured against what was wanted, so that a `FAIL` line alone says what broke.

Why: 125 files in `EconomyCheck` follow this, and a failing gate is read by its lines alone.

Checked by: review.

```csharp
Check(TabCount(items, TabToday, 5, -1) == 3, "tab counts: Today (3)");   // good
Check(TabCount(items, TabToday, 5, -1) == 3, "test 14 failed");          // bad
```

### RLO-TST-005: A new check fails once

**MUST.** Make every new check fail once, by breaking the code or the check's expectation on purpose, before you rely on it. Say in the pull request that you did.

Why: a check that cannot fail proves nothing.

Checked by: review.

### RLO-TST-006: Self-check lines in the game

**MUST.** Where the game's behaviour cannot be checked offline, write one log line per event, never per frame, and only while Developer diagnostics are on (`Meta.Dev.Verbose`):

```
<Feature> check (<what>): <what was measured against what was wanted>: PASS
<Feature> check (<what>): <what was measured against what was wanted>: FAIL, <why>
```

Why: a player's `Player.log` then shows what happened in their game; 507 such lines exist today.

Checked by: review.

```csharp
Logger?.Info("Tow check (one car): the model and the map's marker stand " + apart.ToString("0.00", CultureInfo.InvariantCulture) + " m from the car's body: " + (apart < 0.5f ? "PASS" : "FAIL"));
```

### RLO-TST-007: An in-game test for every wait

**MUST.** Add an entry under Options > Tests for every situation the maintainer would otherwise have to wait for in play: a day's end, a court date, a breakdown. The entry works when pressed, or says in a notice why it cannot now.

Why: a feature that takes three game weeks to show itself is otherwise never seen before it ships.

Checked by: review.

### RLO-TST-008: Anything that could be slow is timed

**MUST.** Add a timing check to the checker for any rule that runs often or over many items, with its budget in the check's message.

Why: a timing budget in the checker catches a slow change before a player's frame does.

Checked by: review.

### RLO-TST-009: The gate decides

**MUST.** Before you open a pull request, run every check and read every last line:

1. Build the mod: `dotnet build RealLifeOverhaul.Build/RealLifeOverhaul.csproj -c Release "-p:ModsLocal=<a folder of your own>"`. It ends with `0 Warning(s)` and `0 Error(s)`.
2. Build and run each checker: `dotnet build RealLifeOverhaul.Build/<Checker> -c Release`, then `dotnet RealLifeOverhaul.Build/<Checker>/bin/Release/net10.0/<Checker>.dll`. Each ends with `ALL CHECKS PASSED`. ContractCheck needs the game installed.
3. Run `python RealLifeOverhaul.Build/Workshop/locales.py`. It ends with `LOCALES OK`.

The maintainer runs the same set before every merge and every release. A change merges only when every line passes.

Why: a change that fails a check fails for every player who installs it.

Checked by: the maintainer's gate.

Decide by the text of a result, never by the exit status of a pipe: in `checker | tail -1 && next`, the status is `tail`'s, and 0.8.2 shipped past a failed check that way.

### RLO-TST-010: Say how it was tested

**MUST.** Say in the pull request how the change was proved: "built and checked offline", "seen in the game: <what you did and saw>", or both. **MUST NOT** say a change works in the game when it was not run there.

Why: the maintainer plans the in-game tests of a release from what was not seen.

Checked by: review.

### RLO-TST-011: The baseline only shrinks

**MUST NOT** raise a count in `RealLifeOverhaul.Build/StandardCheck/baseline.txt` or add a line to it in a change that adds code. When you fix a finding the baseline lists, lower its count in the same change; `dotnet StandardCheck.dll -write-baseline` writes the file from what it finds. Only the maintainer MAY accept a new entry, in a pull request labelled `standard`.

Why: the baseline holds the code written before the standard (2,672 findings under 1,037 entries), so that StandardCheck passes today and fails anything new.

Checked by: review.
