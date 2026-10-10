# Real Life Overhaul

A mod for [Big Ambitions](https://store.steampowered.com/app/1331550/Big_Ambitions/) that adds everyday life to the city: a camera behind your character, traffic accidents with police and ambulance, crime, courts, driving licences, jobs, health, friends, a family and public transport. Every part can be switched off by itself.

**To play it, subscribe on the Steam Workshop:**
https://steamcommunity.com/sharedfiles/filedetails/?id=3816536560

This repository is the source. You only need it if you want to read the code, report a problem in detail, or contribute.

## Status

Version 0.1.0 is the first public release. Much of it has had little play from start to finish. The in-game Guide has a "Known limits" page, and [the Workshop description](RealLifeOverhaul.Build/Workshop/description.txt) carries the short version.

Windows and English only.

## What is in the repository

| Path | What it is |
| --- | --- |
| `Assets/Mods/RealLifeOverhaul/Scripts` | The mod's code, one folder per feature |
| `Assets/Mods/RealLifeOverhaul/Locales/en.json` | Every text the player reads |
| `Assets/Mods/RealLifeOverhaul/Bundles` | The built models, animations and clothes the game loads |
| `PropAssets`, `CityAssets`, `CharacterAssets` | The scripts that build those bundles with Blender and Unity |
| `RealLifeOverhaul.Build` | The command-line build, the text checker and the developer documentation |
| `RealLifeOverhaul.Build/Workshop` | The Workshop page texts, the writing rules and the upload steps |
| `docs/SDK.md` and the other folders under `Assets/Mods` | Hovgaard Games' mod SDK template and its example mods, which this repository started from |

The developer documentation is [RealLifeOverhaul.Build/README.md](RealLifeOverhaul.Build/README.md). It is long. It describes how each feature works, the rules the code follows, and what has and has not been checked in the game.

## Building

You need:

- Big Ambitions installed through Steam. The build compiles against the game's own DLLs, which are not in this repository and must not be added to it.
- The [.NET SDK](https://dotnet.microsoft.com/download), version 10 or later.

Build and install into the game's local mods folder:

```
dotnet build RealLifeOverhaul.Build/RealLifeOverhaul.csproj -c Release "-p:GameManaged=<Steam>\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
```

The build must end with `0 Warning(s)` and `0 Error(s)`. It copies the mod to `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal\RealLifeOverhaul`. To build without installing, add `"-p:ModsLocal=<some other folder>"`.

If you are also subscribed on the Workshop, untick the Workshop item under Mods in the game's main menu while you test a local build. The game refuses two mods with the same assembly name, so with both switched on neither copy loads. A copy built under another assembly name would load next to the first; the second one to start in a city then logs a warning and does nothing.

Check the texts:

```
dotnet build RealLifeOverhaul.Build/CatalogueCheck -c Release
dotnet RealLifeOverhaul.Build/CatalogueCheck/bin/Release/net10.0/CatalogueCheck.dll
```

It must end with `ALL CHECKS PASSED`.

You do not need Unity or Blender unless you change a model, an animation or a piece of clothing. The built bundles are in the repository.

## Reporting a problem

Open an issue. What helps most:

1. On the mod's options page, under Diagnostics, switch on "Developer diagnostics" and make the problem happen again.
2. Attach `Player.log` from that session. It is in `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions` and is overwritten when the game starts again.
3. Add a screenshot, what you were doing, what you expected, the mod version and which other mods are on.

### Stutter, or a frame rate that gets worse the longer you play

With "Developer diagnostics" on, the mod writes one line to `Player.log` for every minute of play, and one for every frame that took longer than 80 ms:

```
[Mod:Real Life Overhaul] Session 12 min: frames 5210, average 11.5 ms, worst 61 ms, over 33 ms: 4, over 50 ms: 1; managed heap 412 MB of 610 MB, collections in the minute 3; engine memory 2950 MB; mod objects 345 (118 shown) under 6 roots; people 2 out, 5 made of 12; city people 3 out, 8 made of 8, city props made 14; on foot in the street, game hour 9.
[Mod:Real Life Overhaul] Session: a frame of 96 ms; the mod was at: city life: a character's look being built (a mesh merge); a garbage collection ran in it; on foot in the street, game hour 9.
```

Please send:

1. `Player.log` from a session of ten minutes or more in which it stuttered, with "Developer diagnostics" on from the start.
2. How long you had played when it began, and what you were doing (on foot, driving, indoors, the map).
3. Your processor, graphics card, memory, and whether the game is on a hard disk or an SSD.
4. Which other mods were on.
5. If you can: whether the same save stutters after the same time with this mod switched off.

How to read the lines is in [the developer documentation](RealLifeOverhaul.Build/README.md), under "The session record".

## Contributing

Pull requests are welcome. Read [CONTRIBUTING.md](CONTRIBUTING.md) first. Nothing is merged into `main` without the maintainer's review and approval.

## Licence

MIT. See [LICENSE](LICENSE). The mod SDK template and its example mods are by Hovgaard Games, also under MIT.

Big Ambitions is made by Hovgaard Games. This mod is not made or endorsed by them.
