# Comments

A comment in RLO tells the next reader what the code cannot: why it is so, what unit a number is in, what it costs, and what the game does that the code depends on. These rules apply to new and changed comments. Existing comments are not rewritten for the standard alone.

## What the code has

| What | Measured on 17 October 2026 |
| --- | --- |
| Summaries (`/// <summary>`) | 13,734; 9,777 of them (71%) on one line |
| Summaries above a type | 2,600; median 5 lines, 95% at most 26 lines, 27 over 40 |
| Summaries above a member | 11,144; median 1 line, 57 over 8 lines |
| Public and internal types with a summary | 2,559 of 2,789 (92%) |
| Public methods with a summary | 8,063 of 11,604 (69%) |
| Constants with a trailing comment | 4,315 of 7,914 (55%) |
| `//` comments | 12,294 on a line of their own, 16,855 after code |
| Runs of `//` lines | 9,941; median 1 line, 31 longer than 5 lines |
| Comment line length | median 106 columns, wrapped at 117 to 123 |
| `TODO`, `FIXME`, `HACK` | 0 |
| Version numbers or "used to", "no longer" in comments | about 330 |

### RLO-DOC-001: A comment says why

**MUST.** Write a comment to give what the code cannot say: the reason, the unit, the cost, a limit of the game, or a case that looks wrong and is not. **MUST NOT** restate what the code does.

Why: a comment that repeats the code goes out of date with it and says nothing new.

Checked by: review.

```csharp
private bool _broken;   // a fault outside the parts: off for the session   (good)
private bool _broken;   // true when broken                                  (bad)
```

### RLO-DOC-002: Every public and internal type has a summary

**MUST.** Give every public and internal type a `/// <summary>` that says what it is for. For a partial type, one part carries it.

Why: the summary is what a reader and the editor's tooltip show first; 92% of the mod's types have one, and the 114 without are in the baseline.

Checked by: StandardCheck.

```csharp
/// <summary>Something the player owes that is not credit: a fine, a court debt.</summary>
public struct DebtLine
```

### RLO-DOC-003: Members whose name is not enough have a summary

**SHOULD.** Give a public method or property a one-line summary unless its name and type say all of it. Say what it returns when nothing is found, and what it costs if more than a field read.

Why: a summary is what the editor shows at every call; 69% of public methods have one.

Checked by: review.

```csharp
/// <summary>Where this machine stands now. A field read.</summary>
public static GoingPublicRole Role => _role;
```

### RLO-DOC-004: A member's summary is short

**MUST** keep a member's summary within 8 `///` lines: the tags and up to six lines of text. **SHOULD** fit it on one line.

Why: a member's summary is read in a tooltip; 75% of them are one line already.

Checked by: StandardCheck.

### RLO-DOC-005: A type's summary stays under 40 lines

**MUST** keep a type's summary within 40 `///` lines. Put a longer account of a feature in its page under `docs/`, and link it from the summary.

Why: 95% of type summaries fit in 26 lines; past 40 they become a manual nobody keeps up to date.

Checked by: StandardCheck.

### RLO-DOC-006: A constant with a unit says the unit

**MUST.** Give a constant or a tuning field whose number has a unit a trailing comment with the unit and, where it is not plain, the reason.

Why: `30f` means nothing until you know it is seconds of real time; 4,315 constants carry such a comment.

Checked by: review.

```csharp
private const float RecenterDelay = 1.2f;   // seconds without mouse input before swinging back behind the car   (good)
private const float RecenterDelay = 1.2f;                                                                         (bad)
```

### RLO-DOC-007: Comments wrap at 120 columns

**SHOULD** wrap comment lines at 120 columns.

Why: the mod's comments already wrap there, and longer lines run off a review screen.

Checked by: review, StandardCheck (no line over 200, [RLO-STY-014](code-style.md#rlo-sty-014-lines-of-120-columns-never-over-200)).

### RLO-DOC-008: No change history in comments

**MUST NOT** write in a comment what the code used to do, when it changed, which version fixed it or who asked. Write what is true now.

Why: Git and What's new keep the history; a comment about the past is wrong as soon as the code moves on.

Checked by: review.

```csharp
// One relay sits in the game's event and calls the handlers in the order they joined.             (good)
// Every feature used to re-hook itself once a second; now one relay does it (0.12.0).             (bad)
```

### RLO-DOC-009: No notes of work left to do

**MUST NOT** leave `TODO`, `FIXME`, `HACK` or `XXX` in a comment, or code commented out.

Why: a change is finished when it merges; work left to do goes in an issue.

Checked by: StandardCheck (the markers), review (code commented out).

### RLO-DOC-010: Plain British English, no names

**MUST** write comments in plain British English. **MUST NOT** name a real person, a player or another mod's author; say "a player" or "the maintainer".

Why: the repository is public, and nobody's name belongs in it without their asking.

Checked by: review.

### RLO-DOC-011: Name code with see cref

**SHOULD** name a member of the mod in a summary with `<see cref="..."/>`, and add `<param>` or `<returns>` only where the name does not say it.

Why: a `cref` is checked by the compiler and followed by the editor; the mod has 2,318 of them and 913 `<param>` tags.

Checked by: review.
