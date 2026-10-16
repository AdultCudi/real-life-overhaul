# Glossary

The mod's own words, as the code and these pages use them.

| Word | What it means here |
| --- | --- |
| black screen | The game's fade at a door or a time skip. Work hidden behind it is not seen as a stutter. |
| bridge | A small optional door one feature offers or asks of another, with a default that works alone. Bridges are in each feature's `*Bridges.cs` and are filled in by [Wiring](wiring.md). |
| bundle | One of the three asset bundles in `Assets/Mods/RealLifeOverhaul/Bundles`: the characters' looks, the props and the animation clips. Each is built by its own pipeline. |
| clip | One of the mod's own Humanoid animations, from `Bundles/reallife_anim`, played through `ActivityAnimator`. The list is in [`CityAssets/README.md`](../../CityAssets/README.md). |
| contract | The game contract (`Core/GameContract.cs`): every piece of the game the mod reaches by name, checked as the game starts and by ContractCheck. See [After a game update](../contributing/after-a-game-update.md). |
| cordon | The tape, barriers and closed road round an accident (`Traffic/SceneCordon`). |
| Developer diagnostics | The option that lets a feature's own lines, the self-checks and the session record into `Player.log`. In code, `Meta.Dev`. See [Logs and debugging](logs-and-debugging.md). |
| director | The one component of a feature that owns its state and timing: a timer, the game's hour and day, the save. `*Director.cs`. |
| fact | A figure in curly brackets in a Guide page's text, such as `{tech_net_days}`, filled from the code so that a page can never say what the rules do not do. |
| feature guard | The part of the guard that counts one feature's faults and switches it off for the session after five in thirty seconds (`Core/FeatureGuard.cs`). |
| feature registry | `Meta/Features` and `Meta/FeatureList.cs`: every feature, one statement each, read by the Guide, the presets, the options page and the save tools. |
| game-free | Code with no Unity or game types, so a checker can compile and run it offline: the `*Rules.cs`, `*Catalogue.cs` and `*Model.cs` files. |
| gate | The full set of checks a change must pass before it is merged: the build with no warnings, the five checkers and the locale check. See [Checks](../contributing/checks.md). |
| Going Public | Another author's mod that lets several players share one city. Every change must stay safe in one of its sessions. |
| guard | `Core/Guard.cs`: every entry point the game calls runs inside it, so a fault stops one part of the mod and never the game's frame. |
| host, guest | In a Going Public session, the host runs the shared city (`Core/SharedCity`); a guest takes the host's parts whole. |
| hot spot | One of the mod's timed scopes (`Core/HotSpots`), opened by every `Update`, so the session record can name the mod's share of a frame. |
| ladder | Several things are called a ladder: the career ladder of ranks (`Jobs/CareerCatalogue`), what a lender does as payments are missed (`Economy/Products/Ladder.cs`), the rota planner's ladder of plans (`RotaLadder.cs`), a deal's ladder in Ventures, and the Treasury ladder of bonds in the **Stocks** app. |
| line | A feature's state in the save: one string in `modData` under its key. |
| `MadeFor` | The game build this version of the mod is made for (`Core/GameBuild.cs`). ContractCheck fails until it is raised after a game update. |
| map place | A place a feature puts on the city map through `Core/MapPlaces` ([Map places](map-places.md)). |
| `modData` | The game's dictionary of strings in a save, where the mod keeps every line, under keys that start `reallife:` ([Save keys](keys.md)). |
| pool | A shared set of characters made once and reused: the street scenes' twelve (`People/ActorPool`), the living city's eight (`CityLife/CityPeople`), and the pools of events and civic life. |
| presence | A character out only to be seen (an officer on the beat), whom any scene may take back at once. |
| preset | Relaxed, Standard, Realistic or Hardcore: a set of option values. Standard is always an option's default. |
| prop | One of the mod's modelled objects, from `Bundles/reallife_props`, asked for by id from `Core/PropLibrary`. The list is in [`PropAssets/README.md`](../../PropAssets/README.md). |
| role | One person's part in a street scene (`People/ActorRole`), safe to use when the pool gave nobody. |
| scene | One of the large things staged round the player: an accident, a crime, a fire or street event, a rally. One at a time ([`Wiring.Scenes`](wiring.md#one-street-scene-at-a-time-wiringscenes)). |
| self-check | A log line written once per event that says what was measured against what was wanted, then `PASS` or `FAIL`. |
| session record | The once-a-minute and long-frame lines of `Diagnostics/SessionWatch` ([Logs and debugging](logs-and-debugging.md#the-session-record)). |
| Standing | The city's reaction to the player's success: wealth, recognition and reputation (`Scripts/Fame`). |
| stand down | What an entry point does when the game lacks a member it needs: it stops, with one line, and the rest of the mod goes on. |
| stand-in | What the code uses until an asset exists: plain shapes for a prop, one of the game's gestures for a clip. |
| test tools | The **Tests** buttons on the options page, shown with **Show test tools** on. Each starts a situation you would otherwise have to wait for, or says why it cannot. |
| time machine | The game's own time skip (`TimeMachine`), used for rides, custody and a shift left to the clock. |
| What's new | The in-game list of changes. The next release's lines are the locale key `reallife_changes_next`. |
| wish-list | The props, clips and sounds a feature asks for by id and does without until they exist. Each feature page has its own. |
| Wiring | `Scripts/Wiring`, the one folder that fills in the bridges between features ([Wiring](wiring.md)). |
