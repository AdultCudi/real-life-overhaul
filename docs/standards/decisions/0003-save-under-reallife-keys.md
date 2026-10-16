# 0003: Per-save state under reallife: keys

**Status:** Accepted. **Date:** recorded 17 October 2026, from the code as it stands.

## Context

A player's save must keep working whatever happens to the mod: removed, updated, rolled back, or loaded on a machine where one of its features is switched off. The game's save has a dictionary for mods, `modData`, of string keys and string values, which the game writes and reads without looking inside. Other mods use the same dictionary.

## Decision

Each feature keeps its state in its own `modData` entry, `reallife:<feature>`, written by its store (`*Store.cs`) in a compact line that begins with a version. New fields are appended at the end of a record; a line without them still loads; a line no store can read is kept exactly as it was read (`Core/SaveLines`). Phone contacts the mod adds have `reallife-` ids. The mod writes all of its entries through one relay on the game's save event (`Core/SaveHook`).

## Consequences

- A save loads with the mod removed: the game ignores keys it does not know, and the removal tool finds the mod's by their prefix.
- An older version of the mod reads a newer save's lines as far as it knows them, and keeps the rest.
- Each store needs a round-trip check in a checker, and an older line without the new fields ([RLO-TST-003](../testing.md#rlo-tst-003-what-a-check-covers)).
- State that can be worked out from the game is not saved at all ([RLO-ARC-015](../architecture.md#rlo-arc-015-derive-rather-than-save)).

## Rules

[RLO-ARC-014](../architecture.md#rlo-arc-014-per-save-state-under-reallife-keys), [RLO-ARC-015](../architecture.md#rlo-arc-015-derive-rather-than-save).
