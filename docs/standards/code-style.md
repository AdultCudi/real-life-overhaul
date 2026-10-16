# Code style

These rules cover how the mod's C# reads: names, files, layout and the language features it uses. They describe the code in `Assets/Mods/RealLifeOverhaul/Scripts` as it was measured on 17 October 2026: 1,315 files, 533,803 lines, 3,122 types.

The checkers in `RealLifeOverhaul.Build` follow the same rules, with one exception in [RLO-STY-013](#rlo-sty-013-braces-on-every-body).

## Names

| What | Style | Measured |
| --- | --- | --- |
| Types, methods, properties, events, enum members | `PascalCase` | All 3,122 types and 3,083 properties; all but 10 of 20,110 methods |
| Interfaces | `IPascalCase` | 42 of 42 |
| Public, internal and protected fields | `PascalCase` | All but 58 of 6,040; those 58 are JSON data read by name |
| Private fields, instance or static | `_camelCase` | 7,977 of 7,979 |
| Constants and static readonly fields, any access | `PascalCase` | 7,808 constants and 1,846 static readonly fields, none otherwise |
| Locals and parameters | `camelCase` | 30,978 of 30,994 locals |

### RLO-STY-001: PascalCase for types and members

**MUST.** Name types, methods, properties, events, enum members and every field that is not private in PascalCase.

Why: the game, Unity and .NET name them so, and all but 68 of the mod's names do.

Checked by: StandardCheck, `.editorconfig`.

```csharp
public float RentDue;            // good
public float rentDue;            // bad
private void Shared_Changed()    // bad: no underscores in a method name
```

A field that `JsonUtility` reads by its JSON name keeps that name (`Character/ModAssets.cs`).

### RLO-STY-002: Interfaces start with I

**MUST.** Begin an interface's name with `I` and a capital letter.

Why: every interface in the mod does, as in .NET.

Checked by: StandardCheck, `.editorconfig`.

```csharp
public interface IPlannerSource   // good
public interface PlannerSource    // bad
```

### RLO-STY-003: Private fields are _camelCase

**MUST.** Name every private field that is not a constant or static readonly with an underscore and camelCase. This includes private static fields.

Why: it tells a field from a local at a glance; 7,977 of 7,979 private fields follow it.

Checked by: StandardCheck, `.editorconfig`.

```csharp
private float _nextLook;            // good
private static bool _started;       // good
private static int[] Current;       // bad
private static float s_nextLook;    // bad: RLO does not use s_
```

### RLO-STY-004: Constants and static readonly fields are PascalCase

**MUST.** Name a constant or a static readonly field in PascalCase at every access level.

Why: they are fixed values, named like the members a reader looks up; every one in the mod is.

Checked by: StandardCheck, `.editorconfig`.

```csharp
private const float Every = 1f;                                       // good
private static readonly List<string> StoodDown = new List<string>(4);  // good
private const int MAX_PARTS = 5;                                       // bad
```

### RLO-STY-005: Locals and parameters are camelCase

**MUST.** Name local variables and parameters in camelCase, without prefixes.

Why: 30,978 of 30,994 locals do.

Checked by: `.editorconfig`.

```csharp
int freeActors = 0;   // good
int FreeActors = 0;   // bad
```

## Files and namespaces

### RLO-STY-006: A file is named after its main type

**MUST.** Name a new file after the main type it declares. A small type used only with the main type, such as a struct of figures or an enum of its states, MAY sit in the same file.

Why: you find a type by its file name; 1,196 of 1,315 files are named so, and 410 keep a companion type beside the main one.

Checked by: StandardCheck.

```
Economy/Company/CompanyStore.cs   declares CompanyStore              good
Core/LookupRules.cs               declares ListIndex, KeyedSums      bad
```

### RLO-STY-007: Part files hold a partial type

**MUST.** Split a large type into partial files named `Type.Part.cs`, one concern each, and declare the type `partial` in every part.

Why: a director's parts stay readable on their own; 197 part files do this today.

Checked by: StandardCheck.

```
CompanyDirector.cs, CompanyDirector.Staff.cs, CompanyDirector.Tax.cs   good
CompanyDirectorStaff.cs                                                bad
```

### RLO-STY-008: Files stay under 1,000 lines

**SHOULD.** Keep a file under 1,000 lines. Move a concern into a part file ([RLO-STY-007](#rlo-sty-007-part-files-hold-a-partial-type)) when it grows past that.

Why: the median file has 305 lines and 87 files are over 1,000; those are the ones reviews miss things in.

Checked by: review.

### RLO-STY-009: One namespace per folder

**MUST.** Give a file the namespace the other files of its folder have. Give a new feature folder the namespace `RealLifeOverhaul.<Folder>`. Use block-scoped namespaces.

Why: 94 of 96 folders hold one namespace. The older features (23 folders) are in the root namespace `RealLifeOverhaul` and the newer ones (23 folders, 800 of 1,315 files) in a namespace of their own; new folders follow the newer, majority style. C# 9 has no file-scoped namespaces.

Checked by: StandardCheck.

```csharp
namespace RealLifeOverhaul.Planner   // good: a file in Scripts/Planner
{
}

namespace RealLifeOverhaul.Planner;  // bad: not C# 9
```

### RLO-STY-010: Using directives: System first, then alphabetical

**MUST.** Put `using` directives at the top of the file, outside the namespace: the `System` namespaces first, then the others, each group in alphabetical order, with no blank lines between.

Why: 1,238 of 1,265 files order them so, and a sorted list shows a new dependency in review.

Checked by: StandardCheck, `.editorconfig`.

```csharp
using System;                      // good
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UnityEngine;
```

## Layout

### RLO-STY-011: Four spaces, no tabs

**MUST.** Indent with four spaces a level. Never use a tab.

Why: no line in the mod is indented with a tab.

Checked by: StandardCheck, `.editorconfig`.

### RLO-STY-012: Allman braces

**MUST.** Put every opening and closing brace of a type, a member or a statement block on its own line. A block that fits on one line, such as `{ get; private set; }` or a short enum, MAY stay on one line.

Why: 72,478 of the mod's block braces stand on their own line.

Checked by: StandardCheck, `.editorconfig`.

```csharp
if (FrameGuard.Off)   // good
{
    return;
}

if (FrameGuard.Off) {  // bad
```

### RLO-STY-013: Braces on every body

**MUST.** Put the body of every `if`, `else`, `for`, `foreach` and `while` in braces, even a single statement. In the checkers, a check of one line MAY stay on one line: `if (bad) Fail("...")`.

Why: a statement added under an unbraced `if` runs every time; 33,371 of the mod's `if` statements have braces and 238 bodies in 32 files do not.

Checked by: StandardCheck (the mod's code), `.editorconfig`.

```csharp
if (_broken)     // good
{
    return;
}

if (_broken) return;   // bad in the mod
```

### RLO-STY-014: Lines of 120 columns, never over 200

**SHOULD.** Keep a line within 120 columns, and wrap comments at 120. **MUST NOT** write a line over 200 columns.

Why: 94% of code lines fit in 120 and comments wrap at 117 to 123 columns; 1,716 lines in 459 files are over 200 and are listed in the baseline.

Checked by: StandardCheck (200).

### RLO-STY-015: Clean whitespace and encoding

**MUST.** End no line with spaces or tabs. End every file with a line break. Save C# files as UTF-8 without a byte order mark.

Why: whitespace changes hide real changes in a diff; 1,314 of 1,315 files end with a line break and 1,300 have no byte order mark.

Checked by: StandardCheck (trailing whitespace), `.editorconfig`.

Git stores text files with LF line endings, and Git for Windows checks them out with CRLF (`core.autocrlf true`). Keep that setting.

## Language

### RLO-STY-016: var only when the type is on the right

**MUST.** Write the type of a local variable. Use `var` only when the right-hand side names the type: `new`, a cast, a generic argument such as `InstanceBehavior<GameManager>.Instance`, or `using var hotSpot = HotSpots.Time(...)`. Write the type in `foreach`.

Why: a reader of a pull request has no tooltips; 30,994 declarations name their type and 2,556 use `var`, of which 1,901 are `new`.

Checked by: `.editorconfig`.

```csharp
var shops = new List<CompanyShop>(16);   // good
int day = Clock.Day;                     // good
var day = Clock.Day;                     // bad
foreach (CompanyShop shop in _shops)     // good
```

### RLO-STY-017: No LINQ in the mod

**MUST NOT** use `System.Linq` in `Scripts`. Write the loop. The checkers MAY use LINQ.

Why: LINQ allocates enumerators and delegates, and the mod allocates nothing in what runs often; no file in `Scripts` uses it today.

Checked by: StandardCheck.

```csharp
int open = 0;                                // good
for (int i = 0; i < _shops.Count; i++)
{
    open += _shops[i].Open ? 1 : 0;
}

int open = _shops.Count(s => s.Open);        // bad
```

### RLO-STY-018: Explicit access modifiers, no this.

**MUST.** Write the access modifier of every type and member, `private` too. **MUST NOT** qualify members with `this.`.

Why: 12,398 private fields say `private` and 291 do not; `this.` appears nowhere.

Checked by: `.editorconfig`.

```csharp
private bool _running;   // good
bool _running;           // bad
```

### RLO-STY-019: Classes are sealed or static

**SHOULD.** Make a class `sealed` or `static` unless it is made to be inherited.

Why: 2,114 of 2,205 classes are; a sealed class tells the reader nothing overrides it.

Checked by: review.

### RLO-STY-020: Expression bodies for one-line members

**MAY.** Write a property or method whose body is one expression with `=>` on one line.

Why: 2,092 properties do; longer bodies use braces.

Checked by: `.editorconfig` (silent).

```csharp
public static bool InSession => _role == GoingPublicRole.Host || _role == GoingPublicRole.Guest;
```

### RLO-STY-021: C# 9 on netstandard2.1

**MUST.** Use only what `RealLifeOverhaul.Build/RealLifeOverhaul.csproj` sets: C# 9 (`LangVersion` 9.0), `netstandard2.1`, nullable reference types off. **MUST NOT** add `record` types, `init` accessors or other features that need types `netstandard2.1` lacks.

Why: the game runs the mod on Unity's Mono; a feature the build accepts may still fail there.

Checked by: the build.
