# Living with other authors' mods

The game loads every mod that is switched on into one program. Two mods that do the same thing to the same object
of the game (steer the camera, draw on the city map) do not fail: they both run, and the player sees a picture that
shakes or two route lines. This page is the rule this mod follows when that happens, and the table of the mods it
knows by name. The next clash is one more line in that table and, if it is a new kind, one more place that asks.

## The rule

1. **This mod is the one that gives way.** It never switches another mod off, never changes another mod's objects and
   never patches its code. What it can do is stop doing its own share.
2. **Stand down for what is seen, not for what is installed.** A mod that is switched on may be idle (the camera mod
   below starts switched off, behind a key). So where the game lets it be seen, this mod watches what happens to the
   game's object and stands down only while something else is driving it, and comes back when that stops. A name
   only supplies the sentence the player reads, and a shorter count.
3. **Never take turns.** Standing down takes a few frames in a row; coming back takes many more in a row
   (`CameraWatch`: 12 frames, 3 with a known mod installed, against 90). Two mods that alternate frame by frame are
   the fault this exists to prevent.
4. **Where it cannot be seen, divide the screen.** Nothing shows from outside whether another mod is drawing a route
   at this moment. There the split is fixed: the other mod keeps the game object it draws on, this mod keeps what is
   its own. The player gets one option to hand over more.
5. **Say so, in the game's own interface, once.** A notice the first time in a session (not on every load), one
   "Other mods:" warning in Player.log per mod found, a self-check line with Developer diagnostics on, and the options
   that do nothing drawn as the game draws a control that cannot be used, with a sentence that names the mod.
6. **Cost nothing.** Looking for mods is one walk over the loaded assemblies when a city loads and when the options
   page is drawn. Watching is a handful of loads a frame, with nothing allocated. State the cost in the class comment.

## How a mod is found (`Core/OtherMods`)

A mod is known by a type of its own: an assembly whose name begins as the table says (`OtherModName.Is`: the name
itself, or the name and then something that is not a letter, because some authors put the version in the assembly's
name) and which holds the named type. `OtherMods.Look(logger)` does the walk and logs one line per mod newly found;
`OtherMods.<mod>.Loaded` is a field read from then on. `OtherMods.Of(kind)` gives the first mod of a kind that is on.

## The table

| Mod (as its author names it) | Assembly begins | Known type | What it does to the game | What this mod does |
| --- | --- | --- | --- | --- |
| Immersion - RPG Camera | `Immersion` (ships as `Immersion-1.2.0-test8`) | `Immersion.ImmersionEntry` | Adds its own Cinemachine virtual camera and gives it priority 1000 while its camera is on, -1000 while it is off (its key, default off) or a screen is open; locks the pointer while on. | `Camera/CameraYield`: while that camera is live, `ThirdPersonCamera` gives the game's on-foot cameras their own figures back and frees the pointer, and `ChaseCamera` lets go. Back 90 clean frames after it stops. The Camera section's options are drawn unusable with "unavailable: the camera is with Immersion - RPG Camera". |
| Voogle Route | `VoogleRoute` | `VoogleRoute.VoogleRouteCityMod` | Draws its route as lines in the world: on the ground on foot and driving, and on the city map on the map's own layer (`CityMap.cityMapMask`); has its own click, bookmark panels and destination popup on the city map, and a phone app. No minimap on the HUD. | `Navigation/NavDirector`: nothing of this mod's is put on the city map (no `NavCityMap` object, no double click). This mod's route stays on its own minimap with its two lines, but the arrival notice and the end of the destination are left to that mod (one notice, one rule). With "Use the other mod's route line" on (the row is on the page only while that mod is on; default off) there is no route of this mod's at all. |

## How a foreign camera is seen without a name (`Camera/CameraYield`)

Two findings, each read once a frame, neither needing to know who:

- **A live virtual camera above the game's priorities.** The game gives its own virtual cameras priority 1 or 0 and
  no other (`Helpers.CameraHelper.CameraTransition`). This mod's own (the chase camera at 100, the custom shop's at
  200) are named `RealLifeOverhaul ...`. Any other live camera with a priority over 1 is another mod's.
- **A camera that is not where the engine put it.** The engine's camera brain writes the camera's place at the end of
  a frame and nothing of the game's writes it afterwards (no script of the game sets the main camera's transform). If
  at the start of the next frame the camera is more than 5 cm or half a degree from there, something wrote it in
  between. Not looked at while the brain is blending between two cameras or is switched off (photo mode).

Nothing is counted while a screen is open: camera mods stand aside for the city map and the menus themselves. Nor
during the player's funeral, the one scene the game films with cameras that are not on its list (a timeline's).

## Adding the next one

1. Decompile it into `evidence/othermods/` and read what it does to which object of the game. Never copy from it.
2. Add its line to `OtherMods` (assembly prefix, a type every version has, its name, its kind) and to the table here.
3. If its kind exists, that is all: whoever asks `OtherMods.Of(kind)` or `CameraYield.StoodDown` already gives way.
4. If it is a new kind, decide by rule 2 or rule 4, put the question in the one "Game" file of the feature it touches,
   make the affected options say so (`Feature.StandsAside` or `Feature.Present` in `Meta/FeatureList.cs`), add a
   self-check line, and a test entry that pretends the mod is there if it cannot be staged otherwise.
5. Add offline checks for whatever counting or arithmetic the stand-down has (`NavCheck/PolishCheck.cs`).

## Diagnostics

- `Other mods: 'Voogle Route' is also switched on (assembly VoogleRoute). ...` (always logged, once per mod)
- `Camera: another mod drives the camera (Immersion - RPG Camera, through its own camera '...'). ...` (always logged)
- `Camera check: another mod drives the camera (Immersion - RPG Camera): stood down: PASS.`
- `Camera: nothing else has driven the camera for 90 frames. This mod's camera is back.`
- `Navigation check (other mod): Voogle Route is switched on: ...: PASS.`
- Options > Test tools > Camera > "Test: pretend another mod has the camera" runs the whole stand-down with nothing
  installed.

Proven (read in the decompiled code): the game's two priorities, that the game never writes the main camera's
transform, the camera mod's priorities and its key, where the route mod draws. Inferred (not seen in the game): that
the stand-down leaves no shake with the camera mod on, and how the unusable options look.
