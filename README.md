# Real Life Overhaul

A mod for [Big Ambitions](https://store.steampowered.com/app/1331550/Big_Ambitions/) that adds everyday life to the city: a camera behind your character, accidents, crime, police and courts, buses and driving, a home, a family and friends, courses and careers, credit, a city economy and a stock market, and much more for your businesses, from staff and brands to a tech firm. Every part can be switched off by itself.

**To play it, subscribe on the Steam Workshop:**
https://steamcommunity.com/sharedfiles/filedetails/?id=3816536560

**Or install it without the Workshop:** run `setup.exe` from [the latest release](https://github.com/AdultCudi/real-life-overhaul/releases/latest). [INSTALL.md](INSTALL.md) has the steps, and how to install from the zip by hand.

Questions, help and bug reports: [the Real Life Overhaul Discord](https://discord.gg/tzCh2VXs9E).

This repository is the source. You only need it to read the code, report a problem in detail, or contribute.

## Status

Version 0.1.0 was the first public release; 0.60.0 is the current one. Much of it has had little play from start to finish. The in-game Guide has a "Known limits" page, and [the Workshop description](RealLifeOverhaul.Build/Workshop/description.txt) carries the short version.

Windows only. English, Brazilian Portuguese, Simplified Chinese, German, French, Spanish, Russian, Dutch, Turkish, Polish and Korean; it follows the language the game is set to, and a text not yet translated is in English. It is made for the current build of Big Ambitions on Steam's public branch: on an older build some of its apps and features do not work, and the mod says so in the main menu, with how to update the game.

## What is in the repository

| Path | What it is |
| --- | --- |
| `Assets/Mods/RealLifeOverhaul/Scripts` | The mod's code, one folder per feature |
| `Assets/Mods/RealLifeOverhaul/Locales/en.json` | Every text the player reads |
| `Assets/Mods/RealLifeOverhaul/Bundles` | The built models, animations and clothes the game loads |
| `PropAssets`, `CityAssets`, `CharacterAssets` | The scripts that build those bundles with Blender and Unity |
| `RealLifeOverhaul.Build` | The command-line build, the checkers, the installer and the developer guide's front door |
| `docs` | The developer documentation: contributing, reference and one page per feature |
| `RealLifeOverhaul.Build/Workshop` | The Workshop page texts, the writing rules and the upload steps |
| `docs/SDK.md` and the other folders under `Assets/Mods` | Hovgaard Games' mod SDK template and its example mods, which this repository started from |

The developer documentation starts at [the developer guide](RealLifeOverhaul.Build/README.md), which has the folder map and gets you building in fifteen minutes. [The documentation index](docs/README.md) lists every page: how to contribute, the rules the code follows, how each feature works, and what has and has not been checked in the game.

## Building

You need Big Ambitions installed through Steam and the [.NET SDK](https://dotnet.microsoft.com/download), version 10 or later. The build compiles against the game's own DLLs, which are not in this repository and must not be added to it. You do not need Unity or Blender unless you change a model, an animation or a piece of clothing: the built bundles are in the repository.

```
dotnet build RealLifeOverhaul.Build/RealLifeOverhaul.csproj -c Release "-p:GameManaged=<Steam>\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
```

The build must end with `0 Warning(s)` and `0 Error(s)`, and puts the built mod in `RealLifeOverhaul.Build/out/RealLifeOverhaul`. [Getting started](docs/contributing/getting-started.md) has the checks, how to try a build in the game, and what to do when a step fails.

## Reporting a problem

The quickest way is the **Report a bug** button under **Start here** on the mod's options page: it gathers your log, settings and a screenshot into one zip for you to post on the Discord or attach to [a bug report](https://github.com/AdultCudi/real-life-overhaul/issues/new?template=bug_report.md). It sends nothing itself. By hand:

1. On the mod's options page, under Troubleshooting, Diagnostics, switch on "Developer diagnostics" and make the problem happen again.
2. Attach `Player.log` from that session. It is in `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions` and is overwritten when the game starts again.
3. Add a screenshot, what you were doing, what you expected, the game's build, the mod version and which other mods are on.

[Logs and debugging](docs/reference/logs-and-debugging.md) says what the mod writes to the log.

### Stutter, or a frame rate that gets worse the longer you play

With "Developer diagnostics" on, the mod writes one line to `Player.log` for every minute of play, and one for every frame that took longer than 80 ms:

```
[Mod:Real Life Overhaul] Session 12 min: frames 5210, average 11.5 ms, worst 61 ms, over 33 ms: 4, over 50 ms: 1; managed heap 412 MB of 610 MB, collections in the minute 3; engine memory 2950 MB; mod objects 345 (118 shown) under 6 roots; people 2 out, 12 made of 12; city people 3 out, 8 made of 8, city props made 14; looks built in the minute 1 (0 while playing), since the city loaded 23 (2 while playing); on foot in the street, game hour 9.
[Mod:Real Life Overhaul] Session: a frame of 96 ms; the mod was at: city life: a character's look being built (a mesh merge) (the mod's named and timed work in that frame took 14.2 ms of it); where it went: scripts' Update 71 ms, rendering 12 ms, the rest 13 ms; the collector: no collection (41 so far), at work 0.0 ms, heap 731 MB before and 733 MB after; the game: its screen was clear, not saving, no scene loaded or unloaded; ...; on foot in the street, game hour 9.
```

Please send:

1. `Player.log` from a session of ten minutes or more in which it stuttered, with "Developer diagnostics" on from the start.
2. How long you had played when it began, and what you were doing (on foot, driving, indoors, the map).
3. Your processor, graphics card, memory, and whether the game is on a hard disk or an SSD.
4. Which other mods were on.
5. If you can: whether the same save stutters after the same time with this mod switched off.

How to read the lines is in [the session record](docs/reference/logs-and-debugging.md#the-session-record).

## Contributing

Pull requests are welcome. Talk to us first on the Discord's developer channel or in an issue, then read [CONTRIBUTING.md](CONTRIBUTING.md) and [the engineering standard](docs/standards/README.md). Nothing is merged into `main` without the maintainer's review and approval.

## Licence

MIT. See [LICENSE](LICENSE). The mod SDK template and its example mods are by Hovgaard Games, also under MIT.

Big Ambitions is made by Hovgaard Games. This mod is not made or endorsed by them.
