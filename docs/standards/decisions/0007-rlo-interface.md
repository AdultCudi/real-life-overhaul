# 0007: The RLO interface, the mod's own designed screens

**Status:** Accepted. **Date:** 1 November 2026. Amends [0002](0002-game-ui-only.md) for the screens drawn on the kit.

## Context

The owner, on 16 October and 1 November 2026, said RLO replaces the game's interface with a design of its own: "The
most important thing in the overhaul is the UI and UX. It must look absolutely beautiful at the same time, not
cluttered." Copying the game's panels (0002) cannot give one consistent, designed look across a large overhaul, and
the living economy's screens (the population, the studio, the desk sessions) have no game screen to copy. On 1 November
the owner chose the City Ledger from a study of three directions.

## Decision

Screens of the overhaul, and the game apps the mod redraws (Contacts first), are drawn on the mod's own interface kit:

- The kit is `Scripts/Ui`, with its own fonts (Source Serif 4, Libre Franklin; SIL Open Font License) and its tokens,
  components and templates.
- It is laid out by game-free code and checked offline in every language, look and frame (`docs/standards/ui.md`).
- It sits behind the switch "RLO interface", under Living population.
- A redrawn game app keeps the game's object alive underneath and presses its buttons, so the game's logic stays the
  game's. Any fault gives the game's own screen back.
- Notices and question boxes stay the game's own (`Core/GameConfirm`).

## Consequences

- One look across the overhaul, with rules a machine can hold (RLO-UI-001 to 015).
- The kit carries its own fonts (about 5 MB of bundle) and a renderer to maintain.
- A redrawn app depends on the game's members by name (ContractCheck reports them after an update), and falls back
  to the game's screen when they move.
- Screens the kit does not draw still follow 0002.

## Rules

[RLO-UI-001 to 015](../ui.md), [RLO-ARC-013](../architecture.md#rlo-arc-013-the-games-own-interface-only).
