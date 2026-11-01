# 0008: The RLO interface's look, Cities: Skylines II in place of the City Ledger

**Status:** Accepted. **Date:** 1 November 2026. Amends [0007](0007-rlo-interface.md)'s look.

## Context

On 1 November 2026 the kit was drawn as the City Ledger, a newspaper's look ([0007](0007-rlo-interface.md)): serif heads,
a salmon accent, a dark and a light paper, a masthead across the top and full-screen pages. The owner turned it down
the same day: "it looks like AI slop, especially the brown red color theme... like I'm looking at a dashboard instead
of a game UI. Something is missing and something feels incomplete". He picked Cities: Skylines II as the base: "Cs2 is
good ... mix and match".

The direction (`not-in-repo/design/DIRECTION_CS2.md`, with its reference screenshots) names what the City Ledger
lacked: the world on screen round every panel, colourful icons that carry meaning, cool slate panels, a thin title
strip, compact rows, real charts, links that look like links, a cyan fill for what is chosen and a status bar along the
bottom. A prototype of six screens in that look (a person's info panel, the city's people as an info view, the studio
and Contacts as app windows, a desk session as a decision card, the phone's home) was rendered over the game's own
streets at 1080 and 720. The owner approved it on 1 November 2026: "Yes this works. This looks very good."

## Decision

The mod draws its interface in the language of Cities: Skylines II, with a few pieces from other games where each does
a job better. It copies the language, never a game's art:

- Cities: Skylines II is the base: panels over the world, title strips, rows and links, donuts and legends, the icon
  tabs of its info panels, and the status bar.
- Football Manager 24: the profile (a portrait, key facts, stars, attributes graded red to green) and the sidebar of an
  app with many sections.
- Frostpunk 2: the decision card, with numbered choices and their consequences in coloured lines.
- inZOI: round portraits with a status ring, and soft status chips.
- Big Ambitions: the game's own icons where it has one (`UiIcons.Own`), so the mod sits beside the game as one product.

The kit sets its type in Overpass (SIL OFL 1.1), the face Cities: Skylines II uses, and has one dark look. Its icons
are Phosphor's (MIT), and its portraits are painted in code. A screen is drawn on its templates: the info panel, the
info view, the app window, the decision card and the phone.

## Consequences

- The serif faces, the salmon and claret, the light paper, the masthead and the full-screen pages are gone. Overpass and
  Phosphor's licences ship in the font bundle, and `UiAssets/CREDITS.md` names both.
- Light mode is dropped from the kit: `UiTokens.Colour` ignores the look it is passed. Flare's own light option stays
  inside Flare.
- Icons (`UiIconPaths.Data.cs`, written by `UiAssets/scripts/icons.py`, filled by `UiIconRaster`) and portraits
  (`UiPortraits`) are made in code, so no picture files ship for them.
- The world is the ground, so contrast is measured over a bright street (`UiCheckRules.World`).
- Rules RLO-UI-002, 004, 006, 007, 008, 010, 015 and 016 changed, and RLO-UI-017 and 018 are new.
- The waves that convert every game and mod screen build on the templates (`UiInfoPanel`, `UiInfoView`, `UiAppWindow`,
  `UiDecision`, `UiPhone`). The four older shapes draw inside the window until their screens are converted.
- As of this record the look is checked offline and in previews over the game's streets, not yet in the game.

## Rules

[RLO-UI-002, 004, 006, 007, 008, 010 and 015 to 018](../ui.md).
