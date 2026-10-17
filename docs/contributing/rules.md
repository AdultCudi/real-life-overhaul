# The rules for new code

The short list. Every pull request is held to it. Each rule says why in one line and links to the full standard in [`docs/standards`](../standards/README.md).

1. **Cost nothing when idle.** A feature not in use does no more than one time comparison a frame: nothing allocated, searched or built as text per frame, and no search of the whole scene while the game is played.
   - Why: the mod's first promise is no performance hit, and twenty features each costing a little add up to a stutter.
   - More: [the performance standard](../standards/performance.md) (RLO-PERF-001, 003, 004, 005); what was measured is in [Performance](../reference/performance.md).
2. **Fail safe.** Every entry point the game calls is behind the guard; a feature that meets something unexpected logs once, puts back what it changed and switches itself off.
   - Why: a fault in the mod must never break the game's own screen or frame.
   - More: [RLO-ARC-007, every entry point behind the guard](../standards/architecture.md#rlo-arc-007-every-entry-point-is-behind-the-guard), [RLO-ARC-009, fail safe](../standards/architecture.md#rlo-arc-009-fail-safe) and [RLO-ARC-010, give back what you borrow](../standards/architecture.md#rlo-arc-010-give-back-what-you-borrow); how the guard works is in [After a game update](after-a-game-update.md).
3. **The save stays loadable without the mod.** State goes under the feature's own `reallife:` key in `modData`, new fields at the end of a record, and nothing the base game cannot read.
   - Why: a player must be able to remove the mod and keep playing their save.
   - More: [RLO-ARC-014, per-save state under reallife: keys](../standards/architecture.md#rlo-arc-014-per-save-state-under-reallife-keys); the keys are in [Save keys and ids](../reference/keys.md).
4. **Keep Going Public safe.** Deterministic across machines, nothing new sent over the network that an older or absent copy of the mod cannot read, and never change another player's things (`GoingPublic.InSession`, `Mine`).
   - Why: people play the mod in multiplayer sessions, and a difference between machines breaks the session for everyone.
   - More: [the multiplayer standard](../standards/multiplayer.md) (RLO-MP-003, 004, 006, 007).
5. **Use the game's own interface.** No canvas, panel or prompt of the mod's own: copy or drive the game's through `Scripts/Core/GameUi`. Something done at a place is started by clicking the thing at that place.
   - Why: the mod should look as if the game's makers had added it.
   - More: [RLO-ARC-013, the game's own interface only](../standards/architecture.md#rlo-arc-013-the-games-own-interface-only); the building blocks are in [The game's own interface](../reference/game-interface.md).
6. **Texts in all three locales, in the house style.** Every player text is a key, appended at the end of `en.json`, `pt.json` and `zh-cn.json` in the same commit; British English, short sentences, no dashes, an exclamation mark only in speech; a notification of at most 240 characters.
   - Why: the mod ships in seven languages, and a missing key shows the player a raw key.
   - More: [the texts standard](../standards/texts.md) (RLO-TXT-002, 005 to 008); how to add one is in [Texts and translation](texts-and-translation.md).
7. **No game files.** Never commit the game's DLLs, models, textures, sounds or decompiled code, or assets you have no right to share.
   - Why: they are not ours to publish.
   - More: [RLO-GIT-011, what never goes in](../standards/git-and-review.md#rlo-git-011-what-never-goes-in).
8. **Every feature has an option, and off means off.** With its option off the game behaves exactly as without the feature.
   - Why: players switch parts off when they clash with another mod or with how they play.
   - More: [RLO-ARC-006, every feature registered and switchable](../standards/architecture.md#rlo-arc-006-every-feature-is-registered-and-can-be-switched-off).
9. **Fix the cause, once.** Find the root cause, cover every path that leads there, and add the check that would catch it coming back. No TODOs and no stubs.
   - Why: a fix made twice is a fix that was not made.
   - More: [RLO-GIT-008, the definition of done](../standards/git-and-review.md#rlo-git-008-the-definition-of-done) and [RLO-DOC-009, no notes of work left to do](../standards/comments.md#rlo-doc-009-no-notes-of-work-left-to-do).
10. **Prove it offline, and say what you could not.** Put pure rules in game-free files and check them in a checker; give every situation you would otherwise wait for a test button. In the pull request, say what you tried in the game, or that you did not.
    - Why: most of the mod cannot be tested without hours of play.
    - More: [the testing standard](../standards/testing.md) (RLO-TST-001, 007, 010); the checkers are in [Checks](checks.md).
11. **Match the code around you.** Its naming, its `<summary>` comments that say why and what it costs per frame, and its layout of small files.
    - Why: one style across 1,300 files is what keeps the code readable.
    - More: [the code style standard](../standards/code-style.md), [RLO-DOC-002, every type has a summary](../standards/comments.md#rlo-doc-002-every-public-and-internal-type-has-a-summary) and [RLO-PERF-012, every component says its cost a frame](../standards/performance.md#rlo-perf-012-every-component-says-its-cost-a-frame).
12. **Clean commits.** One change per pull request, plain commit messages, no attribution lines for tools, no personal names or emails, and never `ProjectSettings`.
    - Why: the repository is public and the history is part of the documentation.
    - More: [the git and review standard](../standards/git-and-review.md) (RLO-GIT-002, 004, 005).
