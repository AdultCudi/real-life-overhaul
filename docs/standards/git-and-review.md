# Git and review

These rules cover how a change gets from a branch into `main`: its branch, its commits, its pull request, the review and what done means. They describe the history as it is: 856 commits and 407 merges, 108 version tags.

### RLO-GIT-001: Branch names

**MUST.** Name a branch `fix/<name>` for a fix or `feature/<name>` for a feature, with lower-case words and hyphens. The maintainer also uses `perf/`, `release/`, `integrate/`, `test/` and `build/`.

Why: the merge commit names the branch, so the history says what each change was. 231 branches are `feature/`, 74 `fix/`.

Checked by: StandardCheck.

```
feature/bot-topup     good
fix/wage-creep        good
Feature/BotTopUp      bad
```

### RLO-GIT-002: Commit subjects

**MUST.** Write a commit's subject as one plain sentence that says what is true after the change, led by the area it touches when that helps. Begin with a capital letter, a version number or the name of a file or member. **MUST NOT** end it with a full stop or begin it with a type prefix such as `feat:` or `fix:`.

Why: `git log --oneline` then reads as a list of what changed. 855 of 856 subjects have no full stop, 389 begin with their area, and none has a type prefix.

Checked by: StandardCheck (the commits on your branch).

```
Credit card: paid off by hand inside a debt arrangement, which ends cleared the moment all of it is paid     good
EconomyCheck: What's new checks read any row, so a release that numbers the next row passes                   good
fix: credit card arrangement.                                                                                 bad
```

### RLO-GIT-003: Commit bodies

**SHOULD.** When the subject cannot carry it, add a body after a blank line: what happened before, what happens now, and why. Write it in plain sentences.

Why: the reason for a change is the first thing lost; 338 of the 856 commits have a body.

Checked by: review.

### RLO-GIT-004: No trailers, names or addresses

**MUST NOT** add trailer lines (`Co-authored-by:`, `Signed-off-by:`) or any personal name or email address to a commit message.

Why: the repository is public, and the history has none.

Checked by: review, the maintainer's scan before every push.

### RLO-GIT-005: One change per pull request

**MUST.** Make one fix or one feature per pull request. Open an issue first for anything large.

Why: a pull request that does one thing is reviewed in one sitting and shipped as one release.

Checked by: review.

### RLO-GIT-006: Everything in the same change

**MUST.** Put the code, its texts in the three required locales, its What's new line, its offline checks and the documentation it changes into the same pull request.

Why: a feature merged without its texts or its checks is a feature half shipped.

Checked by: review.

### RLO-GIT-007: The review checklist

**MUST.** The maintainer reviews every pull request against this list, and the author checks it first.

- [ ] The mod builds with `0 Warning(s)` and `0 Error(s)`, and every checker and `locales.py` pass ([RLO-TST-009](testing.md#rlo-tst-009-the-gate-decides)).
- [ ] StandardCheck passes and its baseline was not raised ([RLO-TST-011](testing.md#rlo-tst-011-the-baseline-only-shrinks)).
- [ ] The cause is fixed at its root, on every path that leads there.
- [ ] Idle, the change costs at most one comparison a frame, and no frame method allocates or searches ([performance.md](performance.md)).
- [ ] Every new entry point is behind the guard ([RLO-ARC-007](architecture.md#rlo-arc-007-every-entry-point-is-behind-the-guard)).
- [ ] Features meet only through bridges and `Scripts/Wiring` ([RLO-ARC-005](architecture.md#rlo-arc-005-features-meet-only-through-bridges)).
- [ ] Every screen is the game's own interface ([RLO-ARC-013](architecture.md#rlo-arc-013-the-games-own-interface-only)).
- [ ] Saved state is under `reallife:`, versioned, and an older save loads ([RLO-ARC-014](architecture.md#rlo-arc-014-per-save-state-under-reallife-keys)).
- [ ] It is safe in a Going Public session ([multiplayer.md](multiplayer.md)).
- [ ] New texts are in `en`, `pt` and `zh-cn`, at the end, in British English, without dashes or exclamation marks ([texts.md](texts.md)).
- [ ] New rules have offline checks, and each failed once ([RLO-TST-005](testing.md#rlo-tst-005-a-new-check-fails-once)).
- [ ] Comments say why, and none tells history ([comments.md](comments.md)).
- [ ] The pull request says how it was tested ([RLO-TST-010](testing.md#rlo-tst-010-say-how-it-was-tested)).
- [ ] The version number is unchanged ([RLO-GIT-009](#rlo-git-009-versions)).

Checked by: review.

### RLO-GIT-008: The definition of done

**MUST.** A change is done when all of these are true:

1. It builds with `0 Warning(s)` and `0 Error(s)`.
2. Every check passes ([RLO-TST-009](testing.md#rlo-tst-009-the-gate-decides)).
3. Its texts are in the locales, its feature has its option, its Guide page and its in-game tests.
4. It has its line in What's new ([RLO-TXT-011](texts.md#rlo-txt-011-a-line-in-whats-new)).
5. The documentation under `docs/` says what the code now does, in the same change.
6. It was tested in the game, or the pull request says that it was not.

Why: a change that is merged is shipped as a release soon after, with nothing left to add.

Checked by: review.

### RLO-GIT-009: Versions

**MUST NOT** change the version in a pull request. The maintainer sets it at release:

| Part | Raised by | Example |
| --- | --- | --- |
| MAJOR | The maintainer's decision; 0 until then | `0` |
| MINOR | A release with a new feature; PATCH goes back to 0 | `0.29.0` |
| PATCH | A release of fixes only, up to 99 | `0.29.3` |

Why: the version is written in seven places (`RealLifeOverhaul.csproj`, `ModManifest.asset`, `Meta/Changelog.cs`, `README.md`, `INSTALL.md`, the installer script and the Workshop description) by one script, and the build stops if the first three disagree. Each release is tagged `v<version>`.

Checked by: the build, review.

### RLO-GIT-010: The maintainer merges

**MUST.** Only the maintainer merges into `main`, as a merge commit (`git merge --no-ff`) named `Merge <branch>: <what it brings>`. Nobody pushes to `main` directly.

Why: every change on `main` passes one review and one gate, and each merge commit marks one change.

Checked by: GitHub's protection of `main`, `.github/CODEOWNERS`.

### RLO-GIT-011: What never goes in

**MUST NOT** commit:

- build output: `bin`, `obj`, `RealLifeOverhaul.Build/out`;
- `ProjectSettings`;
- the game's files, or code decompiled from it or from another mod;
- logs, `gate.log` among them;
- personal details, tokens or keys.

Why: the repository is public, and the game's files are not the mod's to share.

Checked by: `.gitignore`, review, the maintainer's scan before every push.
