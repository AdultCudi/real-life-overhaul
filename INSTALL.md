# Installing Real Life Overhaul without the Steam Workshop

There are two places to get the mod. Use one of them, not both.

| | Steam Workshop | Download from GitHub |
| --- | --- | --- |
| How | Subscribe on the [Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3816536560) | Run `setup.exe` from [Releases](https://github.com/AdultCudi/real-life-overhaul/releases/latest), or unzip the zip from there yourself |
| Updates | Steam updates it for you | You run each new version's setup yourself |
| Good for | Most players | Playing without the Workshop, keeping a version you like, or trying a version before it reaches the Workshop |

Both are the same mod, built from this repository. The game loads a mod from its own `ModsLocal` folder exactly as it loads one from the Workshop.

You still need Big Ambitions itself, on Windows, kept up to date on Steam's public branch: the mod is made for the game's current build.

## Install with setup.exe (the easy way)

1. **Download** `RealLifeOverhaul-<version>-setup.exe` from the [latest release](https://github.com/AdultCudi/real-life-overhaul/releases/latest).
2. **Run it.** Windows may say "Windows protected your PC", because the installer is not signed with a paid certificate. Click **More info**, then **Run anyway**. Check the download first if you like (see "Checking the download" below).
3. **Click through.** The installer:
   - puts the mod in the game's mod folder, `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal\RealLifeOverhaul`;
   - replaces an older copy there, and keeps your saves and the mod's options;
   - waits if the game is open, and asks you to close it;
   - warns you if you are also subscribed on the Workshop;
   - needs no administrator rights.
4. **Start the game.** The mod's page is in **Options**, on the **Mods** tab.

To update, run the new version's setup the same way. To uninstall, see "Uninstall" below.

## Install by hand (zip)

1. **Close the game.**
2. **Download** `RealLifeOverhaul-<version>.zip` from the [latest release](https://github.com/AdultCudi/real-life-overhaul/releases/latest).
3. **Open the game's mod folder.** Paste this into the address bar of File Explorer and press Enter:

   ```
   %USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal
   ```

   If Windows says it cannot find it, start the game once and close it again: the game makes the folder when it starts. You can also make the `ModsLocal` folder yourself inside `Big Ambitions`.
4. **Delete the old copy first.** If there is a `RealLifeOverhaul` folder in `ModsLocal` already, from an earlier version, delete the whole folder before you unzip. Unzipping over it keeps every file the new version no longer has, and the mod can pick those up: an old file once dressed the city bus in the wrong parts of its textures.
5. **Unzip** the download into `ModsLocal`. You should end up with exactly this:

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
           ├── pt.json
           └── zh-cn.json
   ```

   The game only loads a folder that has the mod's DLL directly inside it. Check that you do not have `RealLifeOverhaul\RealLifeOverhaul\...`, one folder inside another, which some unzip tools make.
6. **Start the game.** The mod's page is in **Options**, on the **Mods** tab. A notice appears once for each new version: click it to open the Guide.

The first time you load a save that has never had mods, the game offers to keep a backup and plays a copy marked "(Modded)". That is the game's own safety step, not the mod's.

## If you are also subscribed on the Workshop

Unsubscribe from the Workshop item (or untick it under **Mods** in the game's main menu), or uninstall the copy in `ModsLocal`. With both, the game starts the mod twice. The mod notices and the second copy does nothing, but you cannot be sure which version is the one that runs.

## Update

- **With setup.exe:** run the new version's setup. It clears the old copy first.
- **By hand:** close the game, delete the `RealLifeOverhaul` folder from `ModsLocal`, and unzip the new version as in "Install by hand". Never unzip over the old folder: a file left over from an older version can confuse the new one.

Your saves and the mod's options are not in that folder, so they are kept.

## Uninstall

1. Load each save you played with the mod. In the mod's options, under **Troubleshooting**, **Save tools**, press **Prepare this save for removing the mod**, then save.
2. Close the game.
3. **Installed with setup.exe:** open Windows **Settings**, **Apps**, find **Real Life Overhaul (Big Ambitions mod)** and choose **Uninstall**. **Installed by hand:** delete the `RealLifeOverhaul` folder from `ModsLocal`.

## Checking the download

Each release lists the SHA-256 of its setup and its zip. To check yours, in PowerShell (with the name of the file you downloaded):

```
Get-FileHash "$env:USERPROFILE\Downloads\RealLifeOverhaul-0.12.3-setup.exe" -Algorithm SHA256
```

The value must match the one on the release page. Only download the mod from the Workshop page or this repository's Releases.

## If it does not load

- The mod is not on the Mods tab: run the setup again, or, if you installed by hand, check the folder layout in step 5 and that the game was closed while you copied it.
- The mod's apps do nothing, or the mod says the game needs updating: your game is an older build. In Steam, right-click Big Ambitions in the Library and choose **Properties**. Under **Betas**, set Beta Participation to **None**. Then, under **Installed Files**, choose **Verify integrity of game files**.
- Something goes wrong in play: report it on the [Discord](https://discord.gg/tzCh2VXs9E) in #bug-reports, or open an issue here. Attach `Player.log` from `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions`, taken right after the problem and before the game starts again.
