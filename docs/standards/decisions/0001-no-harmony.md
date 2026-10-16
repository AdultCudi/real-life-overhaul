# 0001: No Harmony patches

**Status:** Accepted. **Date:** recorded 17 October 2026, from the code as it stands.

## Context

Harmony rewrites a game method at run time so that a mod's code runs before, after or instead of it. Big Ambitions ships no Harmony, so a mod that patches has to bring a copy. A patch names a method by its signature; when a game update changes that method, the patch fails to apply or runs against code it was not written for, often without an error. Two mods that patch the same method decide each other's behaviour.

The game gives mods what RLO needs without patching: its events (`GlobalEvents`), its components, its interface objects to copy, and its save's `modData`. Where the game changes between builds, `Core/GameContract.cs` and ContractCheck find out before a player does.

## Decision

The mod patches no game method. It reaches the game through its events, its components and its public members, and copies the game's interface instead of changing it.

The one exception is the shared city in a Going Public session: Going Public has no way for another mod to receive its own messages, so `Core/GoingPublicLink.cs` puts a prefix on Going Public's two receive methods, using Going Public's own copy of Harmony, found by name at run time. Without Going Public it is never touched.

## Consequences

- A game update can switch a feature off (ContractCheck and the guard say which), but it cannot leave a broken patch in the game.
- RLO runs beside any other mod without fighting over a method.
- Some things the game does not expose cannot be done; the feature does without them.
- `RealLifeOverhaul.csproj` references only the game's own assemblies.

## Rules

[RLO-ARC-011](../architecture.md#rlo-arc-011-no-harmony-patches), [RLO-ARC-012](../architecture.md#rlo-arc-012-no-dependency-beyond-the-game).
