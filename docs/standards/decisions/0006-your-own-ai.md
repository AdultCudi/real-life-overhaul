# 0006: Your own AI: RLO Texts' cards may go to an AI service the player chooses

**Status:** Accepted. **Date:** 20 October 2026.

## Context

RLO Texts' packs ([0005](0005-rlo-texts-local-model.md)) are small models: a player with a ChatGPT plan, an API key of a service, or a larger model in Ollama or LM Studio can have better words than a pack gives, and a player whose PC is too slow for a pack can have words at all. A service on the internet is the player's own account and the player's own data, and it can be slow, refuse, run out or go away.

## Decision

The mod can send the card RLO Texts builds for its pack, with the same system turn, to an AI service the player chooses (`Texts/TextProviders`), and reads the reply through the same checks (`Texts/TextCheck`). It is off unless the player chooses a service. Every service but ChatGPT is reached with the player's key by one client: the OpenAI-compatible chat API, or Anthropic's Messages API (`Texts/TextServiceRules`). ChatGPT is reached through OpenAI's Sign in with ChatGPT for open-source apps (`Texts/TextChatGpt`, `Texts/TextSignIn`). GitHub Copilot is not offered: GitHub documents no way for an app of its own to call it, and signing in as another app would pass the mod off as that app. A Claude subscription is never offered: Anthropic's terms forbid its sign-in in other apps.

## Consequences

- With a service chosen, each card leaves the PC: who speaks, the place, the time, what happened, the last lines of the thread and what the player typed. The options and the Guide say so; Ollama and LM Studio stay on the PC (only 127.0.0.1 is accepted for them).
- Keys and tokens are kept in one file per Windows user, sealed with Windows' protection of the user's data (`Texts/TextKeys`), never in a save, the options or a log; every log line of the runner goes through `TextServiceRules.Clean`.
- The runner (`Texts/TextService`) has its own queue and at most two requests in flight, each within the time left to its deadline and at most six seconds, on worker threads; the game's thread only hands words over. After a fault it rests and the hub falls back to the pack, then to the written line (`Texts/TextsHub`).
- In a Going Public session nothing shared (the shared feed and paper, which must agree on every machine) is ever sent to a service; only the player's own lines are, and nothing new goes over the session's network.
- The checks drive the whole path offline against pretend services and a pretend sign-in server (`EconomyCheck/TextsServiceChecks.cs`).

## Rules

[RLO-PERF-001](../performance.md#rlo-perf-001-an-idle-feature-costs-one-comparison-a-frame), [RLO-MP-004](../multiplayer.md#rlo-mp-004-same-inputs-same-answer), [RLO-MP-007](../multiplayer.md#rlo-mp-007-nothing-new-that-an-older-or-absent-rlo-cannot-read), [RLO-ARC-009](../architecture.md#rlo-arc-009-fail-safe), [RLO-ARC-012](../architecture.md#rlo-arc-012-no-dependency-beyond-the-game).
