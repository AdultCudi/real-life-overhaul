# Getting started

From a fresh clone to a build you can try in the game. You need Big Ambitions and the .NET SDK; you do not need Unity or Blender unless you change a model, an animation or a piece of clothing.

## What you need

- **Big Ambitions**, installed through Steam and up to date on the public branch. The build compiles against the game's own DLLs, which are not in the repository and must never be added to it.
- **The [.NET SDK](https://dotnet.microsoft.com/download), version 10 or later.** The mod targets netstandard2.1; the checkers run on .NET 10.
- **Git**, and Git Bash or PowerShell for the commands below.
- **Python 3**, for the locale check (`locales.py`).
- **Windows.** The mod and the game run on Windows only.

## 1. Clone

```
git clone https://github.com/AdultCudi/real-life-overhaul.git
cd real-life-overhaul
```

Run every command below from the repository's root folder.

## 2. Build

Find the game's `Managed` folder. With Steam in its usual place it is:

```
C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed
```

Build the mod against it:

```
dotnet build RealLifeOverhaul.Build/RealLifeOverhaul.csproj -c Release "-p:GameManaged=C:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed"
```

- The build must end with `0 Warning(s)` and `0 Error(s)`.
- The built mod is in `RealLifeOverhaul.Build/out/RealLifeOverhaul`: the DLL, `Locales/`, `Bundles/` and `thumbnail.jpg`. That folder is the whole package, as it is uploaded.
- Without `-p:GameManaged` the project looks in `G:\Steam\steamapps\common\Big Ambitions`, where the maintainer's game is.
- If the build fails with a missing type or member, your game may be older or newer than the one the mod is made for; see [After a game update](after-a-game-update.md).

## 3. Run the checks

The four offline checkers need nothing but .NET. Build each one, then run its DLL:

```
dotnet build RealLifeOverhaul.Build/CatalogueCheck -c Release
dotnet RealLifeOverhaul.Build/CatalogueCheck/bin/Release/net10.0/CatalogueCheck.dll
```

Do the same for `NavCheck`, `EconomyCheck` and `PlannerCheck`. Each must end with `ALL CHECKS PASSED`. Then:

- **ContractCheck** checks the built mod against the game on your disk. Build the mod first, then pass the `Managed` folder: `dotnet RealLifeOverhaul.Build/ContractCheck/bin/Release/net10.0/ContractCheck.dll "<Managed folder>"`.
- **The locale check**: `python RealLifeOverhaul.Build/Workshop/locales.py` must end with `LOCALES OK`.

What each checker proves, and how to read a failure, is in [Checks](checks.md).

## 4. Try your build in the game

1. **Close the game.** The game watches its mods folder, and files changed under a running game lose their textures and texts until it is restarted.
2. Build with `-p:InstallToGame=true` added to the command in step 2. The build then puts the mod in `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal\RealLifeOverhaul` instead, and removes any file there that is not part of the package.
3. If you are subscribed on the Workshop, untick the Workshop item under **Mods** in the game's main menu. With both switched on the game loads neither: it refuses two mods with the same assembly name.
4. Start the game. The mod's page is under **Options**, on the **Mods** tab.
5. On that page, under **Troubleshooting**, **Diagnostics**, switch on **Developer diagnostics** and **Show test tools**. The test buttons start most situations at once, and the mod's lines go to `Player.log`; see [Logs and debugging](../reference/logs-and-debugging.md).

To build into another folder, add `"-p:ModsLocal=<folder>"` instead.

## Setting up Unity, for asset work

1. Install Big Ambitions through Steam, Unity 2022.3.62f2 (must match the game) and the .NET SDK.
2. Open this repository's root folder in Unity.
   In **Big Ambitions > Welcome**, import the game DLLs.
3. If the game is not at `G:\Steam\steamapps\common\Big Ambitions`, pass `-p:GameManaged="<game>\Big Ambitions_Data\Managed"` to the dotnet commands below.

## Building the code and the asset bundles

Two ways, same source:

- **Fast iteration (code only):** `dotnet build RealLifeOverhaul.Build/RealLifeOverhaul.csproj -c Release` compiles the mod and copies the DLL, the locales and `Bundles/` to `RealLifeOverhaul.Build/out/RealLifeOverhaul`.
  Add `-p:InstallToGame=true` to put them in the game's `ModsLocal/RealLifeOverhaul` instead (only with the game closed), or `"-p:ModsLocal=<folder>"` for a folder of your own.
- **Asset bundle (only after changing the looks):** `CharacterAssets/build.ps1` (or only the last step, `build_bundle.ps1`) rebuilds `Assets/Mods/RealLifeOverhaul/Bundles/reallife_character` from `CharacterAssets/build` with Unity in batch mode and checks that it loads; the bundle is committed, so a code-only build needs neither Unity nor Blender.
  See [`CharacterAssets/README.md`](../../CharacterAssets/README.md).
- **Prop bundles (only after changing a prop):** `PropAssets/build.ps1` rebuilds `Assets/Mods/RealLifeOverhaul/Bundles/reallife_props` and `reallife_props_2` the same way, and `PropAssets/contents/` with them; all are committed too. A new prop goes in the second (`PropAssets/README.md`, "The bundles").
  See [`PropAssets/README.md`](../../PropAssets/README.md), which also lists every prop with its size, pivot and how to use it.
- **Clip bundle (only after changing a clip):** `CityAssets/build.ps1` rebuilds `Assets/Mods/RealLifeOverhaul/Bundles/reallife_anim`, measures every clip on a stand-in with the game's skeleton and proves in play mode that clips cross-fade over an Animator Controller; it is committed too.
  See [`CityAssets/README.md`](../../CityAssets/README.md), which lists every clip, what it is for and how to use one from any feature.

  What the dotnet build installs is the whole package, exactly as it is uploaded: `RealLifeOverhaul.dll`, every `Locales/*.json`, every file in `Bundles/` and `thumbnail.jpg` (the Workshop picture).
  The Unity **Mod Builder** is not used for this mod: its manifest (`ModManifest.asset`) names no asset bundle, because the bundles are built by the three scripts above.

Restart the game to load a new build.
Publishing to the Workshop is done from inside the game (Mods > Mod Creator); the texts for its form, the thumbnail script, the gallery pictures and the steps are in `RealLifeOverhaul.Build/Workshop` (start with `UPLOAD.md`).

## Good first tasks

Small, self-contained kinds of work that teach you how the mod is put together. Talk to us first on the Discord's developer channel or in an issue, so two people do not do the same thing.

- **A text that reads badly.** Find a sentence in the game that is unclear, too long or breaks [the style sheet](../../RealLifeOverhaul.Build/Workshop/STYLE.md), and mend its key in `en.json`. See [Texts and translation](texts-and-translation.md).
- **A translation.** Check or improve a language you speak natively, key by key.
- **A living city activity.** One person working alone on the pavement is one catalogue entry; see [Adding an activity](adding-things.md#adding-an-activity-to-the-living-city).
- **A life event.** One more entry in the life events' catalogue (`Scripts/Life/LifeEventCatalogue*.cs`), with its texts. An event whose text speaks of the player's home, building, neighbours or landlord needs `HasHome`, so it never reaches a player with no home; EconomyCheck (`LifeFixChecks.cs`) holds every event to that.
- **An offline check.** A rule in a `*Rules.cs` file that no checker covers yet: add the check, and make it fail once to prove it runs.
- **A wish-list asset.** A prop, clip or sound a feature already asks for by id; the feature pages list them, and the asset pipelines' READMEs say how to make one.
- **A page of these docs** that is wrong or hard to follow.
