# The Real Life Overhaul engineering standard

This standard is the set of numbered rules that every change to Real Life Overhaul (RLO) follows: the code, the texts, the checks, the commits and the review. It describes the house style the code already has, in 107 rules on nine pages. Each rule was measured against the code before it was written down, and the counts are on its page.

## The pages

| Page | What it covers | Rules |
| --- | --- | --- |
| [code-style.md](code-style.md) | Names, files, namespaces, layout, language features | RLO-STY-001 to 021 |
| [architecture.md](architecture.md) | Features, directors, bridges, rules classes, the guard, the save | RLO-ARC-001 to 015 |
| [performance.md](performance.md) | What a frame may cost, searches, allocation, timers, assets made by code | RLO-PERF-001 to 018 |
| [multiplayer.md](multiplayer.md) | Staying safe in a Going Public session | RLO-MP-001 to 008 |
| [testing.md](testing.md) | Offline checks, self-check lines, in-game tests, the gate | RLO-TST-001 to 011 |
| [texts.md](texts.md) | Locales, keys, British English, What's new, the Guide | RLO-TXT-001 to 012 |
| [comments.md](comments.md) | What a comment is for, summaries, lengths | RLO-DOC-001 to 011 |
| [git-and-review.md](git-and-review.md) | Branches, commits, pull requests, review, done, versions | RLO-GIT-001 to 011 |
| [dlc-and-updates.md](dlc-and-updates.md) | New districts, DLC content and another city: the city check, the DLC rule, tables by district | RLO-DLC-001 to 006 |
| [decisions/](decisions/README.md) | The architecture decision records behind the rules | ADR-0001 to 0006 |

## How to read a rule

Each rule has an ID, a level, one sentence of reason, and where a machine checks it.

```
### RLO-STY-003: Private fields are _camelCase
MUST. Name every private field that is not a constant or static readonly with an underscore and camelCase.
Why: ...
Checked by: StandardCheck.
```

Quote the ID in a review comment or a commit when a rule decides something: "RLO-PERF-003: this allocates a list every frame."

**Checked by** names the tool that fails a change that breaks the rule:

| Tool | What it is |
| --- | --- |
| StandardCheck | `RealLifeOverhaul.Build/StandardCheck`, which reads the repository as text. See [testing.md](testing.md). |
| ContractCheck, EconomyCheck, NavCheck, PlannerCheck, CatalogueCheck | The offline checkers in `RealLifeOverhaul.Build`. |
| `locales.py`, `check.pl` | The text checks in `RealLifeOverhaul.Build/Workshop`. |
| `.editorconfig` | Your editor shows a suggestion. Nothing fails. |
| Review | The maintainer checks it by reading the change. |

## Levels

The key words "MUST", "MUST NOT", "REQUIRED", "SHALL", "SHALL NOT", "SHOULD", "SHOULD NOT", "RECOMMENDED", "MAY", and "OPTIONAL" in these pages are to be interpreted as described in [RFC 2119](https://www.rfc-editor.org/rfc/rfc2119).

| Word | What it means here |
| --- | --- |
| MUST, MUST NOT | Without exception. A change that breaks it is not merged. |
| SHOULD, SHOULD NOT | Follow it unless you have a reason. Give the reason in the pull request. |
| MAY | Your choice. |

## Existing code

The rules apply to new and changed code. Code written before 17 October 2026 that breaks a rule is listed in `RealLifeOverhaul.Build/StandardCheck/baseline.txt` and is not rewritten for the standard alone. When you change such code for another reason, you MAY bring it into line, and you then lower its count in the baseline ([RLO-TST-011](testing.md#rlo-tst-011-the-baseline-only-shrinks)).

## Where RLO differs from Microsoft and Google

The standard starts from Microsoft's [C# coding conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions) and [identifier names](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names), Google's [C# style guide](https://google.github.io/styleguide/csharpguide.html) and Unity's guidance on [garbage collection](https://docs.unity3d.com/2022.3/Documentation/Manual/performance-garbage-collection-best-practices.html) and scripting performance. These are the places where RLO deliberately does otherwise.

| Topic | Microsoft | Google | RLO | Why |
| --- | --- | --- | --- | --- |
| Private static fields | `s_name` | `_name` | `_name` ([RLO-STY-003](code-style.md#rlo-sty-003-private-fields-are-_camelcase)) | 859 private static fields use `_name` and none `s_name`. |
| Private constants and static readonly fields | PascalCase for constants | `_camelCase` | PascalCase ([RLO-STY-004](code-style.md#rlo-sty-004-constants-and-static-readonly-fields-are-pascalcase)) | 7,808 constants and 1,846 static readonly fields are PascalCase. |
| Braces | Allman | K&R, no line break before `{` | Allman ([RLO-STY-012](code-style.md#rlo-sty-012-allman-braces)) | Every multi-line block in the mod's code. |
| Indentation | 4 spaces | 2 spaces | 4 spaces ([RLO-STY-011](code-style.md#rlo-sty-011-four-spaces-no-tabs)) | No line of the mod's 1,315 files is indented with a tab. |
| Line length | 65 in samples | 100 | 120, never over 200 ([RLO-STY-014](code-style.md#rlo-sty-014-lines-of-120-columns-never-over-200)) | 94% of code lines fit 120; comments wrap at 120. |
| Namespaces | File-scoped | Block | Block ([RLO-STY-009](code-style.md#rlo-sty-009-one-namespace-per-folder)) | The build is C# 9, which has no file-scoped namespaces. |
| `var` | When the type is apparent | Encouraged | Only when the type is written on the right ([RLO-STY-016](code-style.md#rlo-sty-016-var-only-when-the-type-is-on-the-right)) | 92% of declarations name their type. |
| LINQ | Use it for collections | Judge per case | Never in the mod ([RLO-STY-017](code-style.md#rlo-sty-017-no-linq-in-the-mod)) | No allocation in anything that runs often; the mod has none today. |
| Catching `Exception` | Avoid | Not covered | Required at every entry point ([RLO-ARC-007](architecture.md#rlo-arc-007-every-entry-point-is-behind-the-guard)) | A fault in the mod must never break the game's own frame. |
| Comments | On their own line | Generous | Trailing comments for a field's unit and reason ([RLO-DOC-006](comments.md#rlo-doc-006-a-constant-with-a-unit-says-the-unit)) | 16,855 trailing comments carry units and reasons. |
| One class per file | Implied | Prefer one core class | One main type, small companions allowed ([RLO-STY-006](code-style.md#rlo-sty-006-a-file-is-named-after-its-main-type)) | 410 files keep a small struct or enum beside the type that uses it. |
| User interface | Not covered | Not covered | The game's own interface only ([RLO-ARC-013](architecture.md#rlo-arc-013-the-games-own-interface-only)) | Players should not be able to tell the mod's screens from the game's. |
| Patching the host | Not covered | Not covered | No Harmony ([RLO-ARC-011](architecture.md#rlo-arc-011-no-harmony-patches)) | See [ADR-0001](decisions/0001-no-harmony.md). |
| Player texts | Not covered | Not covered | British English, no dashes, an exclamation mark only in speech ([texts.md](texts.md)) | The mod's style sheet, `RealLifeOverhaul.Build/Workshop/STYLE.md`. |
| Determinism | Not covered | Not covered | Shared results identical on every machine ([multiplayer.md](multiplayer.md)) | Going Public runs one city on several machines. |

## How the standard changes

1. Open a pull request that changes the page, with the label `standard`.
2. Say what the code does today, with counts, and why the rule should change.
3. Change `.editorconfig` and StandardCheck in the same pull request when the rule is checked there.
4. The maintainer approves it or closes it. Nothing in `docs/standards` changes without the maintainer's approval.

A rule keeps its ID for good. A rule that is withdrawn stays on its page, marked "Withdrawn", with the date and the reason, so that old references still lead somewhere.
