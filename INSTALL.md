# Installing Real Life Overhaul without the Steam Workshop

There are two ways to get the mod. Use one of them, not both.

| | Steam Workshop | Download from GitHub |
| --- | --- | --- |
| How | Subscribe on the [Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3816536560) | Download a zip from [Releases](https://github.com/AdultCudi/real-life-overhaul/releases/latest) and put it in a folder yourself |
| Updates | Steam updates it for you | You download each new version yourself |
| Good for | Most players | Playing without the Workshop, keeping a version you like, or trying a version before it reaches the Workshop |

Both are the same mod, built from this repository. The game loads a mod from its own `ModsLocal` folder exactly as it loads one from the Workshop.

You still need Big Ambitions itself, on Windows.

## Install

1. **Close the game.**
2. **Download** `RealLifeOverhaul-<version>.zip` from the [latest release](https://github.com/AdultCudi/real-life-overhaul/releases/latest).
3. **Open the game's mod folder.** Paste this into the address bar of File Explorer and press Enter:

   ```
   %USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal
   ```

   If Windows says it cannot find it, start the game once and close it again: the game makes the folder when it starts. You can also make the `ModsLocal` folder yourself inside `Big Ambitions`.
4. **Unzip** the download into `ModsLocal`. You should end up with exactly this:

   ```
   ModsLocal
   └── RealLifeOverhaul
       ├── RealLifeOverhaul.dll
       ├── thumbnail.jpg
       ├── Bundles
       │   ├── reallife_anim
       │   ├── reallife_character
       │   └── reallife_props
       └── Locales
           ├── en.json
           └── pt.json
   ```

   The game only loads a folder that has the mod's DLL directly inside it. Check that you do not have `RealLifeOverhaul\RealLifeOverhaul\...`, one folder inside another, which some unzip tools make.
5. **Start the game.** The mod's page is in **Options**, on the **Mods** tab. A notice appears once for each new version: click it to open the Guide.

The first time you load a save that has never had mods, the game offers to keep a backup and plays a copy marked "(Modded)". That is the game's own safety step, not the mod's.

## If you are also subscribed on the Workshop

Unsubscribe from the Workshop item, or remove the `RealLifeOverhaul` folder from `ModsLocal`. With both, the game starts the mod twice. The mod notices and the second copy does nothing, but you cannot be sure which version is the one that runs.

## Update

1. Close the game.
2. Delete the `RealLifeOverhaul` folder from `ModsLocal`.
3. Unzip the new version as in "Install". Deleting the old folder first matters: a file left over from an older version can confuse the new one.

Your saves and the mod's options are not in that folder, so they are kept.

## Uninstall

1. Load each save you played with the mod. In the mod's options, under **Troubleshooting**, **Save tools**, press **Prepare this save for removing the mod**, then save.
2. Close the game and delete the `RealLifeOverhaul` folder from `ModsLocal`.

## Checking the download

Each release lists the SHA-256 of its zip. To check yours, in PowerShell:

```
Get-FileHash "$env:USERPROFILE\Downloads\RealLifeOverhaul-0.2.4.zip" -Algorithm SHA256
```

The value must match the one on the release page. Only download the mod from the Workshop page or this repository's Releases.

## If it does not load

- The mod is not on the Mods tab: check the folder layout in step 4, and that the game was closed while you copied it.
- Something goes wrong in play: report it on the [Discord](https://discord.gg/tzCh2VXs9E) in #bug-reports, or open an issue here. Attach `Player.log` from `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions`, taken right after the problem and before the game starts again.
