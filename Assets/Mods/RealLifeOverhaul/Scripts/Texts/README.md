# `Scripts/Texts`

RLO Texts: optional packs that word the city's messages on the player's own PC. The game decides everything as before (who, when, what happens); a pack is asked only for the words, every word is checked before it is shown, and whenever no pack answers in time the written lines are shown as before.

## Read first

- `TextsDirector.cs`: keeps it going, and the reasons for how it runs.
- `TextIntents.cs`: the cards built from the game's state.
- `TextCheck.cs`: every word checked before it is shown.
- `TextPacks.cs`: where a pack is found.

## Data v6

A pack of data 6 (its pack.txt says `data=6`) gets every card again with what the contract adds; a pack of data 5 or older keeps its exact cards and ways.

- `TextIntents.Six.cs`: the cards of data v6, the old ones carried on and the city's new talk (the staff, life events, gossip, a review of the player's business).
- `TextLanguages.cs`, `TextSlotKinds.cs`, `TextCheck.Six.cs`: the game's language on the card when the pack writes it (`languages=` in pack.txt), each slot's kind, and the checks in the card's language.
- `TextDecoding.cs`: the request's grammar, its tokens and the player's name kept out (logit bias from the server's own `/tokenize`).
- `TextMemoryRules.cs`, `TextMemoryStore.cs`, `TextMemoryTopics.cs`, `TextsDirector.Memory.cs`: what people remember of the player, recorded by the game and kept in the save (`reallife:texts_memory`).
- `TextsCityRules.cs`, `TextsCityStore.cs`, `TextCityViews.cs`, `TextsBridges.cs`, `TextsDirector.City.cs`, `TextsGame.City.cs`: the city's new talk, once a game hour (`reallife:texts_city`).
- `TextShown.cs`: what was shown, the model's words against the written lines and why, counted by pack and intent.

The contract with the training data is written from these tables: `dotnet EconomyCheck.dll --texts-spec <folder>` writes `SPEC_v6.json` and `fixtures_v6.jsonl` into the folder (the data side's `data/gen/v6/spec`) and the fixtures into `RealLifeOverhaul.Build/EconomyCheck/TextsCardsV6.jsonl`. EconomyCheck fails when a card changes until it is written again (`TextsSixChecks.cs`).

## Documentation

How it works, its rules and its tests: [Wiring (TextsLinks.cs, TextsPhone.cs)](../../../../../docs/reference/wiring.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
