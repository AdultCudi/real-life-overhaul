# 0005: RLO Texts runs a small text model on the player's own PC, offline

**Status:** Accepted. **Date:** recorded 17 October 2026, from the code as it stands (0.29.0).

## Context

The city's messages (Flare posts, texts, street talk) were written lines, and the same line came back for every inbox. A text model can word each message from the game's real state, but it needs memory and processor time, can fail, and can say something wrong.

A model loaded inside the game's process shares its memory, its collector and its graphics device, and a native crash there takes the game down. A service on the internet would need an account, a connection and the players' data.

## Decision

The game decides everything as before: who, when, what happens and every effect. RLO Texts only words the result.

A pack, from RLO's own Workshop items, holds a small text model and llama.cpp's own server, which the mod starts as a child process of the game listening on `127.0.0.1` only (`Texts/TextServer`).

Every word is checked before it is shown (`Texts/TextCheck`), and whenever the model is absent, off, loading, busy, slow or wrong, the written line is shown as before. The pack's runtime files are pinned by SHA-256 (`Texts/TextRuntime`). In a Going Public session the host writes shared text once and the guests check it again (`Texts/TextsShare`).

## Consequences

- Nothing leaves the player's machine, and the mod works the same without a pack.
- A crash of the model ends only its process; the game carries on with the written lines.
- A pack's model takes 0.4 to 1.3 GB of memory; a slow machine is offered the smaller Lite pack and words only what can be asked ahead.
- The queue, the budget and the speed test are extra code (`Texts/TextQueue`, `Texts/TextSpeed`), with their own offline checks.
- The feature costs one time comparison a frame while idle.

## Rules

[RLO-PERF-001](../performance.md#rlo-perf-001-an-idle-feature-costs-one-comparison-a-frame), [RLO-PERF-011](../performance.md#rlo-perf-011-heavy-work-is-spread-or-hidden), [RLO-MP-004](../multiplayer.md#rlo-mp-004-same-inputs-same-answer), [RLO-ARC-009](../architecture.md#rlo-arc-009-fail-safe).
