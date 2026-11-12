# Cinematic shots

## In a sentence

A developer's tool that takes the mod's own pictures by itself: one button in Options, Tests runs a list of eighteen composed shots in the city as it stands, saves each at 3840 by 2160, and puts everything back.

It is not a feature for players: there is no option, no app and no Guide page. It was made so the Workshop page can have pictures of the game with the mod without somebody playing for them.

## How to use it

1. Load a save and stand outdoors where the pictures should be taken. Everything is looked for within 140 m of the player, so a busy avenue, with the player's car parked near, gives the most shots.
2. Open Options, Tests, the Camera and Gallery's tests, and press one of:
   - **Cinematic shots: take the set now**: all eighteen, each in its own light of the day (midday, the golden hour, the blue hour, night).
   - **Cinematic shots: at this hour only**: the sixteen that are not twins of another, in the light of the hour the game's clock shows.
3. Close the menu. The run starts then, and a line at the foot of the screen counts the shots. Esc stops it.
4. A notice at the end names the folder: `Pictures/Real Life Overhaul/Cinematic/<date and time>/` under the user's own profile. Each picture is a PNG with a JPG (quality 92) beside it, named by its number, its light and what it is of (`08_golden_avenue_long.png`), and `shots.txt` lists each picture's subject, place, hour and lens, and every shot left out with the reason.

A whole set takes a minute or two. To get the shots that need something to be there, stage it first with the other test buttons: an accident, a fire or a street event for the incident, a stall for the stall, and wait at a bus stop for the bus.

## Where the code is

- Folder: [`Scripts/Photos/Cinematic`](../../Assets/Mods/RealLifeOverhaul/Scripts/Photos/Cinematic/), part of the Camera and Gallery's feature (`photos`).
- Checks: EconomyCheck `--cinematic` (`CinematicChecks.cs`).

```
CineRules             game-free: the list of shots, the lenses, the names, the picker, where the camera stands for each kind of shot, how far it is
                      pulled in when something is in the way, the busiest spot, the street's run, the four lights' hours, the depth of field
CineHold              game-free: what the run borrows from the game, written once and given back once, in order
CineSubjects          what a shot is of, found in the city as it stands: the game's lists, the mod's map rows, a few rays
CineGame              the only file that touches the game and the engine: the pause, the time of day tool, the look, a place for the camera,
                      the picture's target and its reading, the files; and CineLine, the run's one line of words
CineDirector          the one component: the run (a coroutine), the hold, the end
CineDirector.Tests    Options, Tests
```

Hooks in other files, each a line or a few: `Photos/PhotosGuide.cs` (the two buttons, the cleanup), `Core/RealLifeOverhaulMod.cs` (the component), `Camera/ThirdPersonCamera.cs` and `Wiring/NavLinks.cs` (the mouse look and the minimap stand aside), and three small questions asked of other features on a press: `TransitDirector.BusOut`, `CityLifeDirector.SiteOut` and `EventDirector.Burning`.

## The shots

| No. | Name | Subject | Camera | Lens | Light |
| --- | --- | --- | --- | --- | --- |
| 01 | skyline | the city from a roof behind the player | wide, from the roof, a little down | 62 | midday |
| 02 | avenue_symmetry | the longest straight street from where the player stands | down its middle at eye height | 42 | midday |
| 03 | pavement | the busiest stretch of pavement | a walker on a third of the frame | 32 | midday |
| 04 | park | the most trees together | from the ground, looking up | 58 | midday |
| 05 | landmark | a landmark or sight of the mod's map rows | from the ground, looking up | 60 | midday |
| 06 | car_front | the player's car | three quarters from the front, low | 35 | golden |
| 07 | car_wheel | the player's car | beside its nose at wheel height | 56 | golden |
| 08 | avenue_long | the street | a long lens down it | 24 | golden |
| 09 | over_shoulder | the player | over their shoulder, looking where they look | 40 | golden |
| 10 | player_thirds | the player | on a third of the frame | 30 | golden |
| 11 | stall | a stall, a cart or a street cafe of the living city | a person there on a third | 35 | golden |
| 12 | skyline_lit | the city from the roof | as 01 | 62 | blue |
| 13 | bus | the mod's bus on the street | three quarters from the front | 35 | blue |
| 14 | incident | an accident, a fire, a street event or a crime that is on | three quarters, from well back | 30 | blue |
| 15 | avenue_lights | the street | as 08, a little longer | 22 | night |
| 16 | frontage | the nearest shopfront | square on | 40 | night |
| 17 | car_tail | the player's car | three quarters from behind, low | 35 | night |
| 18 | station | a police station or a hotel of the mod's | three quarters from the front | 38 | night |

The lens is the vertical field of view in degrees. 20 to 35 is a long lens, over 50 a wide one. A close shot (03, 06, 07, 09, 10, 11, 13, 17) asks for depth of field with the subject sharp, through a volume of the tool's own; every shot has a slight vignette (0.22). Nothing else is added to the game's own post processing: no lens dirt, no bloom.

A shot whose subject is not there is left out, and the log and `shots.txt` say why.

## What the game does, and what the tool does with it

- **The camera.** The game's camera is driven by Cinemachine's brain. The tool switches the brain off, as the Camera app and photo mode do, places the camera each frame from `LateUpdate`, and sets the field of view and a near plane of 0.1 m.
- **A place for the camera.** `CineRules.Frame` gives the place for a kind of shot from the subject's position, facing and size. `CineGame.Place` then casts a sphere from the subject along the line to the camera and pulls the camera in to stand 0.6 m before anything met (a wall crossed is how a camera comes to be inside a building); gives the place up if that is nearer than 55% of the distance wanted (75% for the close shots); moves the place along its line while it is not clear air; and keeps it over the ground. Both sides are tried, then both from the other end. After the frames that let the picture settle, the subject must be inside the frame or the shot is left out. The roof for the skyline is the highest of eight places behind the player, between 12 and 130 m over the street, with 60 m of open air toward the city.
- **The interface.** `ScreenshotController.ToggleUIVisibility`, the game's own screenshot switch, hides the game's whole interface and its markers, and with them the mod's HUD, which stands in the game's canvases. While it is off the game also stops hiding buildings and trees between its camera and the player (`ViewBlockingObjectManager`), so what it had hidden fades back in before the first shot. The mod's minimap and mouse look stand aside (`CineDirector.Active`), the pointer is hidden and the world's clicks are held, so nothing is outlined under it. The camera stops drawing the interface's layer, which markers and name tags that stand in the world are on, and the game's guiders to a destination. A picture is drawn into a render target of its own, which no screen interface is ever drawn into.
- **The game's speed.** The game is paused through its own speed control (`GameSpeedController.SetPause`, with the pause key locked), as photo mode does, so nothing moves while the camera is away from the player; every frame of a run the pause is put back if something of the game's ended it. Pictures are still drawn frame by frame.
- **The light.** The game has a time of day tool of its own (`InGameTimeOfDayPanel`, `TimeOfDayController.usingSliderDebugTool`), which shows the sun, sky, fog, windows and lamps of any hour from 5:00 to 24:00 without moving the clock. The tool uses it exactly as that panel does, and sets `forcedHour` too, which is what the street lamps ask. The four hours come from the game's own day: when it puts its sun out, when its lamps come on, and the last hour its sun still stands 8 degrees up. **The clock is never written**, so no hour and no day turns: no midnight, no wages, no bills. At the end the tool is left as it was found and the light put back to the clock's own hour.
- **Weather.** Not changed. The game's rain is drawn from the save's seed with its next start written in the save, and the mod's weather slows traffic and changes grip; neither can be set for a picture without side effects. `shots.txt` says what the weather was.
- **The picture.** The camera draws into a 3840 by 2160 target for sixteen frames, so the game's smoothing of edges builds up at that size, and the target is read at the end of a frame. A frame of one flat colour means nothing was drawn into it: then the screen itself is read, at the screen's size, and `shots.txt` says so. The PNG and the JPG are encoded and written on a worker.

## Giving back

Everything changed is first written in `CineHold` and given back once, in this order: the tool's line, the picture's target, the light, the look, the lens, the camera's layers, the camera's place, the brain, the game's two camera turns, the interface, the pointer, the world's clicks, the game's zoom, the game's speed, the player's walk. The run's `finally` does it when the run ends, is stopped with Esc or faults; the feature's cleanup, the object switched off and the city unloading do it themselves. One log line at the end says how many parts were held, that none is left, and that the clock did not move.

## Limits

- What has changed with the hour shown: the game counts its walkers and its traffic by the hour, so a night shot has fewer of both, and they take a moment to come back afterwards. Things of the mod's that read the clock itself (not the game's light) keep the real hour's look.
- Pictures show what the game draws near the player: its people, cars and lamps are drawn to 200 m of the player, wherever the camera stands.
- People of the mod's own pools who were out of view when the game was paused may not be drawn.
- Refused in a Going Public session: the camera and the light are every player's.
- It cannot be checked without the game that HDRP draws into the target with every effect, or that the camera's settings allow depth of field. The flat frame test and the screen's reading are there for the first; the second shows in the pictures.
