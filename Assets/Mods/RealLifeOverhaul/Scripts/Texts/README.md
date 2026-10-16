# `Scripts/Texts`

RLO Texts: optional packs that word the city's messages on the player's own PC. The game decides everything as before (who, when, what happens); a pack is asked only for the words, every word is checked before it is shown, and whenever no pack answers in time the written lines are shown as before.

## Read first

- `TextsDirector.cs`: keeps it going, and the reasons for how it runs.
- `TextIntents.cs`: the cards built from the game's state.
- `TextCheck.cs`: every word checked before it is shown.
- `TextPacks.cs`: where a pack is found.

## Documentation

How it works, its rules and its tests: [Wiring (TextsLinks.cs, TextsPhone.cs)](../../../../../docs/reference/wiring.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
