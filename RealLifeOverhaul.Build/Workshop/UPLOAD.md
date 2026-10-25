# Uploading Real Life Overhaul to the Steam Workshop

The upload is done from inside the game. Everything else is prepared here. What the game does at each step was read from its code (`UploadModPanel`, `SteamModUploader`, `ModDiscoveryRegistry`).

## What is in this folder

| File | Use |
| --- | --- |
| `title.txt` | paste into the form's title field |
| `description.txt` | paste into the form's description field (Steam BBCode) |
| `summary.txt` | a two-line version, if a short text is wanted anywhere |
| `changenote.txt` | the change note; the form only asks for one on an update |
| `tags.txt` | why there is nothing to choose |
| `gallery/` | seven screenshots for the Steam page |
| `make_thumbnail.ps1` | rebuilds `Assets/Mods/RealLifeOverhaul/thumbnail.jpg` |
| `CREDITS.md` | what the mod contains and where it came from; has a licence line to fill in |
| `STYLE.md`, `check.pl` | the writing rules for the mod's texts and the script that checks them |

## Before you upload

1. Decide the licence line in `CREDITS.md` (it is marked TODO). The in-game Credits page says that no licence has been stated yet; change `reallife_guide_credits` in `en.json` if you state one.
2. Build, which installs the package into the game's `ModsLocal` folder:
   `dotnet build RealLifeOverhaul.Build/RealLifeOverhaul.csproj -c Release`
3. Open `%USERPROFILE%\AppData\LocalLow\Hovgaard Games\Big Ambitions\ModsLocal\RealLifeOverhaul` and check that it holds exactly this and nothing else:

   | File | Size |
   | --- | --- |
   | `RealLifeOverhaul.dll` | about 3.1 MB |
   | `thumbnail.jpg` | about 216 KB |
   | `Locales\en.json` | about 0.7 MB |
   | `Bundles\reallife_anim` | 6,294,571 bytes |
   | `Bundles\reallife_character` | 44,520,019 bytes |
   | `Bundles\reallife_props` | 10,650,842 bytes |
   | `Bundles\reallife_props_2` | 1,298 bytes (the second props bundle: no props in it yet, so no atlas) |

   The whole folder is uploaded as it stands (about 66 MB), so delete anything else you find in it. There must be exactly one `.dll` in the folder's root, and `thumbnail.jpg` must be the only picture there: the game takes the first `.jpg`, `.jpg` or `.jpeg` in the root that is within its size limit (1 MB) as the thumbnail, whatever it is called.
4. Play once from this folder with a save you care about and one new game. The welcome panel should appear once, "Show test tools" and "Developer diagnostics" should be off, and `Player.log` should have one line "Real Life Overhaul 0.1.0 initialised." and otherwise only warnings, if any.

## The upload

1. Start Big Ambitions from Steam (the upload needs Steam running and you signed in).
2. Main menu: **Mods**, then **Mod Creator**, then **Create new mod**.
3. In the folder dropdown choose **RealLifeOverhaul**. The thumbnail appears in the preview. If it does not, the picture is missing from the folder's root or is over 1 MB.
4. Title: paste `title.txt`.
5. Description: paste `description.txt`. If the field cuts it short or will not take line breaks, paste `summary.txt` instead and put the full text on the Steam page afterwards (Workshop item, "Edit title & description").
6. Target build: leave the number the form shows. It is the build of the game you are running, and the form refuses a higher one.
7. Press **Upload**. The item is created with the tag "mod" and **public visibility**; the form has no field for either. A progress bar runs while about 66 MB goes up.
8. When it finishes, the game subscribes you to your own item, switches it on, and opens its Steam page.

If it fails, the game shows which of these it was: not connected to Steam, the folder or the thumbnail not found, or Steam's own error code. Details are in `Player.log`.

## Straight afterwards: remove the local copy

The game loads every folder in `ModsLocal` as well as every Workshop item that is switched on. After the upload you have both. They are the same DLL, so the game loads the assembly once, but it registers each folder as a mod of its own and starts the mod's classes twice (it only looks for two mods with one assembly name among the Workshop items and among the `ModsLocal` folders separately). Up to 0.1.0 that meant two copies of every feature running at once. From 0.1.1 the second copy to start writes one warning to `Player.log` ("switched on twice", "already running in this city") and does nothing; which copy is the one running, and so whose options and key choices are in use, is then whichever the game started first. Keep one:

- Quit the game and move `ModsLocal\RealLifeOverhaul` somewhere outside `ModsLocal` (or delete it: the build puts it back).
- While you work on the mod again, do the opposite: keep the `ModsLocal` copy and untick the Workshop item under **Mods** in the main menu.

Two more things follow from the mod now having a different id (its Workshop number, where the local copy had its folder path):

- Options and key choices are stored per mod id, so the Workshop copy starts with the shipped defaults. To carry yours over: press "Export my settings to a text file" on the options page before you remove the local copy, and "Import settings from the text file" afterwards.
- Saves are not affected. What the mod keeps in a save does not depend on the mod id.

## On the Steam page

1. Add the pictures from `gallery/` ("Add/edit images & videos"):

   | File | Shows |
   | --- | --- |
   | `01_accident_ambulance.jpg` | an accident: the ambulance, a casualty on the stretcher, paramedics, an officer, the cordon (the thumbnail's source) |
   | `02_pedestrian_down_cordon.jpg` | a pedestrian hit by a van: cones, police tape, the barrier, paramedics at work |
   | `03_bus_stop.jpg` | a bus stop with its shelter, route sign and the timetable panel |
   | `04_coffee_cart.jpg` | buying a coffee at the cart, with the notices for it |
   | `05_street_chess.jpg` | the chess player at a table on the pavement |
   | `06_police_incident_night.jpg` | a police incident at night: a patrol car at the kerb and officers on foot |
   | `07_city_bicycle.jpg` | a bicycle taken from a rack |

   They are your own screenshots, uncropped, with the game's interface on them.
2. Check the description's formatting (headings and lists).
3. Visibility: the game uploaded it as Public. Change it on the Steam page if you want to look it over first; note that an update from the game sets it to Public again.
4. Leave the tag "mod" as it is. The game finds subscribed mods by it.

## Checking it as a subscriber

1. With `ModsLocal\RealLifeOverhaul` moved away, start the game. Under **Mods** the item is listed and ticked.
2. Start a new game. The welcome panel appears; the options page (Options, Mods tab) starts with the search box, the Guide and the presets; the preset in use is Standard.
3. `Player.log` has "[Mod:Real Life Overhaul] Real Life Overhaul 0.1.1 initialised." and no error from the mod.
4. Best of all, ask someone else to subscribe on a computer that never had the mod.

## Updating later

1. Set the new version in the three places the build checks (see `Scripts/Meta/Changelog.cs`), add its changelog row and texts, and build.
2. Main menu, **Mods**, **Mod Creator**, **Edit mod** on your item. Choose the folder again, write the change note (it is required), and press **Update**.
3. Remove the local copy again.
