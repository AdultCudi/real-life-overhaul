# 0004: Game-free rules classes, checked offline

**Status:** Accepted. **Date:** recorded 17 October 2026, from the code as it stands.

## Context

The mod cannot be tested in the game by a program: the game needs Steam, a city and a player, and many situations (a court date, a quarter's tax return, a strike) take weeks of game time to reach. Code that names Unity or game types cannot even be compiled without the game's assemblies.

Most of what can go wrong in a feature is arithmetic and decisions: a fee, a limit, an order, a day.

## Decision

Each feature keeps its arithmetic and its decisions in `*Rules.cs` files that name only `System` and the mod's own types, and take the day, the money and any random number as arguments. Its stores (`*Store.cs`) are game-free too. The offline checkers in `RealLifeOverhaul.Build` compile these files directly from `Scripts` and run them on made-up data, thousands of cases at a time.

## Consequences

- Every edge of a rule is checked in seconds, on every change, without the game. EconomyCheck alone runs about 10,000 checks.
- What ships is what was checked, because the checker compiles the same file.
- A rules file cannot reach the game, so the director passes it what it needs; that adds a little code.
- StandardCheck fails a rules file that names the game, and a rules or store file that no checker compiles.

## Rules

[RLO-ARC-002](../architecture.md#rlo-arc-002-rules-classes-are-game-free), [RLO-TST-001](../testing.md#rlo-tst-001-game-free-code-is-compiled-into-a-checker), [RLO-MP-004](../multiplayer.md#rlo-mp-004-same-inputs-same-answer).
