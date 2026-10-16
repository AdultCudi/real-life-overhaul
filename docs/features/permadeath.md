# Permadeath

## In a sentence

An ending the player chooses: off unless switched on, kept per save; a death after a warning, the obituary, the save ended.

## Where the code is

- Folder: [`Scripts/Permadeath`](../../Assets/Mods/RealLifeOverhaul/Scripts/Permadeath/).
- Read first: `PermadeathRules.cs` (when a danger may end a life, game-free: EconomyCheck runs it), `PermadeathDirector.cs` and `Mortality.cs` (the one door the dangerous features call).

| File | What it is |
| --- | --- |
| `PermadeathRules` | when a danger may end a life, the odds, the near miss that comes first, the flu's three warnings, the switch (game-free: EconomyCheck runs it) |
| `PermadeathState` | the save's part: the switch, the near misses had, the flu's warnings, the businesses run, a death and its record (game-free) |
| `LifeStory` | a life as the records tell it, taken down at its end: the one line the obituary is written from (game-free) |
| `PermadeathStory` | which sentences the obituary has, in the order of the page, chosen from the story (game-free) |
| `PermadeathText` | the obituary's words, every {name} filled from the story; PermadeathBridges (ObituaryPage, what it asks of the others) |
| `PermadeathDirector` | the one component: a look a second, the switch, a death (fade, the game's save, the obituary, the main menu), an ended save; the test buttons |
| `PermadeathGame` | everything it touches of the game: the game's own "the player is dead", the fade, the save, the main menu, the tutorial and missions; the machine's memorial |
| `Mortality` | the one door the features where a life is in danger call (crash, hit on foot, mugging, the lender's men, the flu) |
| `PermadeathSettings` | its options block and its feature entry (Guide figures from the rules) |

## How it talks to other features

- The features where a life is in danger (a crash, a hit on foot, a mugging, the lender's men, the flu) call `Mortality`.
- Permadeath calls no other feature: `Wiring/PermadeathLinks.cs` reads the life an obituary tells, the businesses a save runs and the page it is printed on (the City Ledger's front page).
