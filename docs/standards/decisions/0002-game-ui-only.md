# 0002: The game's own interface only

**Status:** Accepted. **Date:** recorded 17 October 2026, from the code as it stands.

## Context

The maintainer's aim is that the mod "must look like Hovgaard gave an update". A mod that draws its own canvases, panels and buttons looks like a mod, takes its own fonts and scales, and does not follow the game's settings for language, resolution or controller. Players then learn two interfaces.

The game's phone apps, info boxes, desk lists, door panels, notifications and question boxes can be copied and filled from code.

## Decision

Every screen of the mod is the game's own interface, copied or driven through `Scripts/Core/GameUi`. Notices go through the game's notifications and confirmations through the game's question box (`Core/GameConfirm`). What a player does at a place starts from the game's own panel at that place. If the game has no control for something, the mod does without it.

## Consequences

- The mod's screens follow the game's look, language and scaling, and change when the game's do.
- A game update that changes an interface object can break a screen; `GameUi` keeps those reads in one place and ContractCheck reports them.
- Some ideas cannot be built, because the game has no control for them.
- `RealLifeOverhaul.Build/docs/game-ui.md` describes each piece and how to use it.

## Rules

[RLO-ARC-013](../architecture.md#rlo-arc-013-the-games-own-interface-only).
