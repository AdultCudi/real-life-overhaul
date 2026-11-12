# Windows and macOS

Big Ambitions is sold for Windows and for macOS, and the mod is built on Windows alone: there is no Mac to run it on and the editor here builds for Windows. A player on a Mac met version 1.0.0 with every word of the RLO interface drawn as a pink block, because the interface's font bundle brought a text shader compiled for Direct3D. The rules here keep the mod the same on both systems by construction, and say what a Mac does without.

| Piece | Where |
| --- | --- |
| Which system this is, game-free | `Core/HostSystem` (`Windows`, `Name`) |
| What a bundle may bring, and the check's line | `Core/BundleShaderRules` (game-free), `Core/BundleShaders` (the check as a bundle is read) |
| The interface's fonts on the game's own text shader | `Ui/UiShaderRules` (game-free), `Ui/UiFonts.Mend` |
| Where the game's log is | `Core/LogPlaceRules` |
| The game's own shaders by name | `RealLifeOverhaul.Build/StandardCheck/game-shaders.txt`, written by `RealLifeOverhaul.Build/Census/game_shaders.py --write` from the installed game |
| A bundle's file read without the engine | `RealLifeOverhaul.Build/StandardCheck/BundleFile.cs` |

### RLO-PLT-001: What only Windows has is reached only where the system was asked

**MUST.** Make a P/Invoke call (kernel32, crypt32), or start a program, only in a member that asks `HostSystem.Windows` first, and give that member another way for a Mac: a figure that says it is not available, a rule that answers instead, or a plain line that the feature stands aside. **MUST NOT** call one from a field's initialiser or a static constructor.

Why: a P/Invoke fails only when it is called, so a guarded call costs a Mac nothing, while one made as a class is first touched takes the whole class with it. RLO Texts' runner is llama.cpp's Windows program and DPAPI is Windows' own, so on a Mac the packs stand aside with one notice and a key for Your own AI is held in memory until the game closes.

Checked by: StandardCheck (every call of an `extern` of its file, and every `Process.Start`, stands in a member that names `HostSystem.Windows` or `RuntimePlatform.Windows`).

### RLO-PLT-002: No path is written with Windows' separator or folders

**MUST.** Build every path with `Path.Combine` from a folder the engine gives (`Application.persistentDataPath`, `Application.consoleLogPath`, the mod's own root). **MUST NOT** write a path with a backslash, a drive letter, `AppData`, `LocalLow` or a `%NAME%` variable in the mod's code.

Why: a Mac keeps the game's data in `~/Library/Application Support/com.Hovgaard-Games.Big-Ambitions` and its log apart from it, under `~/Library/Logs`; a path spelt for Windows names nothing there.

Checked by: StandardCheck (string literals of the mod's sources; the two words written on purpose are listed with their reason in `PlatformChecks.cs` and fail when they are gone).

### RLO-PLT-003: Every bundle is one of the list, named as its file is, and checked as it is read

**MUST.** Name every asset bundle the mod ships in `BundleShaderRules.Bundles`, read it under that very name, letter for letter, and hand what was read to `BundleShaders.Check` (the fonts: `UiFonts.Mend` and its line) before any of it is used. **MUST** name locale and sound files in small letters.

Why: a Mac's disk may tell `reallife_ui` from `Reallife_UI`, which Windows never does. The check gives a material that came in a bundle the game's own shader of its name, once, behind the loading screen, and writes one line a bundle to Player.log, "Shaders check (bundle): N materials, M shaders, K not supported on the graphics device, R repointed to the game's own, L left: PASS", with the graphics API, the system and the game's build. It is a FAIL that names the materials when any is left on a shader that does not draw, and a FAIL when the game's shader a bundle's pieces are drawn with is not there. That line is how a player's log proves or disproves the mod on a system nobody here can run.

Checked by: StandardCheck (the files against the list; every file that opens a bundle has it checked; each name read by a loader; each sound asked for under its file's very name).

### RLO-PLT-004: A bundle brings no shader the game has not got

**MUST NOT** ship a bundle that holds a shader whose name is not in `game-shaders.txt`, unless `BundleShaderRules` writes down the game's shader that gives the same look. **SHOULD** ship no shader and no material at all: draw a bundle's meshes with a material made from the game's own shader (`Shader.Find`, or a material of the game's), as the props, the looks and the map do.

Why: a shader in a bundle is compiled for the graphics API of the system the bundle was built for, Direct3D 11 here, and draws pink anywhere else (Metal on a Mac, Vulkan on Windows with `-force-vulkan`). Meshes, textures, clips, fonts and text are the same file on every system: the textures are BC7 and BC5, the formats the engine gives a Mac's build as well.

Checked by: StandardCheck (each shipped bundle's file is read, its shaders and materials listed, and each shader's name looked up).

### RLO-PLT-005: The words that say where a file is are true on a Mac

**MUST.** Say where the game's log is from `LogPlaceRules.Folder(Application.consoleLogPath, ...)`, write what a notice says is "next to Player.log" to that folder, and keep the Guide's "Where the files are" and INSTALL.md true for a Mac as well as for Windows.

Why: the owner asks a player for Player.log to answer a report, and a Mac's log is not where the game's data is.

Checked by: StandardCheck (the Guide's fact and its page, the three report writers, INSTALL.md); EconomyCheck (the rules: `--platform`).

## What cannot be checked here

Nothing is run on a Mac. The rules make the same path run on both systems, so that what the owner sees on Windows is what a Mac runs, and the line of RLO-PLT-003 says the rest in a Mac player's log. Still unseen: that the Mac build of the game holds the same shaders by name (it is made from the same project, and its own text draws), the look of the interface's text there, and the folders a Mac opens from a bug report.

Left as it is on a Mac: Steam's own filter of what other players type on Flare is looked for in the Windows game's Steam library, which a Mac's game has under another name, so there the mod's own word list filters instead (the contract says so in the log). The game's logo maker writes "Async GPU readback failed" to a Mac's log for each logo and then reads it another way: the lines are the game's and the logos are made.
