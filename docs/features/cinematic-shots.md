# Cinematic shots

## In a sentence

A developer's tool that takes the mod's own pictures by itself: one button in Options, Tests runs a list of eighteen composed shots in the city as it stands, saves each at 3840 by 2160, and puts everything back.

It is not a feature for players: there is no option, no app and no Guide page. It was made so the Workshop page can have pictures of the game with the mod without somebody playing for them.

## How to use it

1. Load a save and stand outdoors where the pictures should be taken. Everything is looked for within 140 m of the player, so a busy avenue, with the player's car parked near, gives the most shots.
2. Choose a look under Realistic graphics first if the pictures should have it: the game's own night is as bright as a dull afternoon, and the look is what makes it night ([Realistic graphics](graphics.md)).
3. Open Options, Tests, the Camera and Gallery's tests, and press one of:
   - **Cinematic shots: take the set now**: all eighteen, each in its own light of the day (midday, the golden hour, the blue hour, night).
   - **Cinematic shots: at this hour only**: the sixteen that are not twins of another, in the light of the hour the game's clock shows.
4. Close the menu. The run starts then, and a line at the foot of the screen counts the shots. Esc stops it.
5. A notice at the end names the folder: `Pictures/Real Life Overhaul/Cinematic/<date and time>/` under the user's own profile. Each picture is a PNG with a JPG (quality 92) beside it, named by its number, the light the game was read to show and what it is of (`08_golden_avenue_long.png`), and `shots.txt` lists each picture's subject, place, hour, light, lens and check, and every shot left out with the reason.

A whole set takes a minute or two. To get the shots that need something to be there, stage it first with the other test buttons: an accident, a fire or a street event for the incident, a stall for the stall, and wait at a bus stop for the bus.

## Reading a run without looking at the pictures

`shots.txt`, and Player.log under `Cinematic shots`, say for each picture:

- **light**: what was read back from the game after the hour was shown: the sun on or off, its strength, colour temperature and height, the moon, the street lamps, the exposure, and how far a ray goes in the fog (the game's, and what the picture was drawn with).
- **check**: the picture's brightness, spread and saturation; the same three for the screen's own frame from the same place, and whether the two are one picture; the same three for the game's own view in that light; and `FLAGGED flat`, `grey` or `fogged`, or `not flagged`.

One line a picture in the log begins `Cinematic shots check (08 avenue_long:` and ends PASS or FAIL. It is FAIL when the target's picture was not the screen's, when the picture is flagged, or when the light read back is not the light its shot asks for.

## Where the code is

- Folder: [`Scripts/Photos/Cinematic`](../../Assets/Mods/RealLifeOverhaul/Scripts/Photos/Cinematic/), part of the Camera and Gallery's feature (`photos`).
- Checks: EconomyCheck `--cinematic` (`CinematicChecks.cs`).

```
CineRules             game-free: the list of shots, the lenses, the names, the picker, where the camera stands for each kind of shot, how far it is
                      pulled in when something is in the way, the busiest spot, the four lights' hours, the depth of field
CineJudge             game-free: what fills a frame (rays across it), the light read back and its word in a name, the mist lifted for a camera
                      that stands in it, the way most of the city lies, the middle of a carriageway, a picture's three figures against the screen's
CineHold              game-free: what the run borrows from the game, written once and given back once, in order
CineSubjects          what a shot is of, found in the city as it stands: the game's lists, the mod's map rows, the traffic system's lanes, rays
CineGame              the only file that touches the game and the engine: the pause, the time of day tool, the look, a place for the camera,
                      the picture's target and its reading, the files; and CineLine, the run's one line of words
CineGame.Judge        the measuring: the pipeline's volumes blended where the camera stands, the game's lights read back, the rays across a
                      frame, a picture's figures
CineDirector          the one component: the run (a coroutine), the hold, the end
CineDirector.Tests    Options, Tests
```

Hooks in other files, each a line or a few: `Photos/PhotosGuide.cs` (the two buttons, the cleanup), `Core/RealLifeOverhaulMod.cs` (the component), `Camera/ThirdPersonCamera.cs` and `Wiring/NavLinks.cs` (the mouse look and the minimap stand aside), `Ui/Sweep/SweepLedger.cs` (the line's row), `Graphics/GraphicsGame.cs` and `GraphicsDirector.cs` (the look follows the hour the game lights the city for, `LitHour`), and three small questions asked of other features on a press: `TransitDirector.BusOut`, `CityLifeDirector.SiteOut` and `EventDirector.Burning`.

## The shots

| No. | Name | Subject | Camera | Lens | Light asked |
| --- | --- | --- | --- | --- | --- |
| 01 | skyline | the city, the way most of its buildings lie, from a roof's edge behind the player | wide, a tripod's height over the edge, a little down | 62 | midday |
| 02 | avenue_symmetry | the street nearest the player (walls on both sides first), from the middle of its carriageway | down its middle at eye height | 42 | midday |
| 03 | pavement | the busiest stretch of pavement | a walker on a third of the frame | 32 | midday |
| 04 | park | the most trees together | from the ground, gone round until trees and open ground fill the frame | 58 | midday |
| 05 | landmark | a landmark or sight of the mod's map rows, by the building that stands at it | from the ground, far enough back for the whole of it with sky over it | 60 | midday |
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

The lens is the vertical field of view in degrees. 20 to 35 is a long lens, over 50 a wide one. A close shot (03, 06, 07, 09, 10, 11, 13, 17) asks for depth of field with the subject sharp, through a volume of the tool's own; every shot has a slight vignette (0.22). No lens dirt, no bloom.

A shot whose subject is not there, or for which no place gives a frame worth taking, is left out, and the log and `shots.txt` say why. The car shots say how many cars of the player's the game lists and how far off the nearest is.

## The game's picture, and what the tool does with it

What the game draws is set out in [Realistic graphics](graphics.md), read from the game's files. Four facts of it decide what a camera at street level sees:

- **The fog is a ground mist.** It reaches 5 to 25 m up and a ray goes 42 to 60 m in it, district by district and hour by hour, in local volumes. The game's own camera stands above it and looks down through a few metres of it. A camera at eye height looks along it: at 60 m two thirds of the picture is fog, and a street is a white-out. This, not the render target, is what washed out the first run's pictures (12 Nov 2026).
- **The exposure is one for noon and midnight** (EV 10, fixed), the moon is white, and the night sky is a grey gradient. From above, a night reads by its lamp pools and dark roads. From the street, with the sky and the mist in view, the game's own night is as bright as a dull afternoon.
- **The sun is white all day** and warm only in its last hour (3600 K from 20:00), out at 21:00.
- **The game hides what stands between its camera and the player**, and stops doing so while its interface is hidden.

So:

- **The camera.** The game's camera is driven by Cinemachine's brain. The tool switches the brain off, as the Camera app and photo mode do, places the camera each frame from `LateUpdate`, and sets the field of view and a near plane of 0.1 m. The far plane, the camera's volume layers and its volume anchor stay the game's.
- **A place for the camera.** `CineRules.Frame` gives the place for a kind of shot from the subject's position, facing and size. `CineGame.Place` casts a sphere from the subject along the line to the camera and pulls the camera in to stand 0.6 m before anything met; gives the place up if that is nearer than 55% of the distance wanted (75% for the close shots); moves the place along its line while it is not clear air; and keeps it over the ground. Then **45 rays are cast across the frame** (`CineGame.Fill`, `CineJudge.Frame`), each told as sky, ground, a tree, the subject or anything else, and the place is taken only if:
  - no one flat surface within 6 m is on more than 40% of the rays (a wall in the lens), nor things within 6 m on more than 60% (the ground and the subject itself are not counted);
  - a skyline has sky along more than half of its top, nothing of the roof within 12 m in the view, and 60% of it going 80 m or to the sky;
  - a street shot runs 88 m clear down the middle of the frame and has a side of the street within 90 m in it (a park side has no wall for a ray to meet, so one side is enough);
  - a tall thing has sky over a third of its top; a park has four trees in sight, 15% open ground and no more than 45% walls.

  Each kind has its places to try: both sides and both ends; eight ways round a thing seen from the ground; every roof found. After the frames that let the picture settle, the subject must still be inside the frame.
- **The subjects.** The street is the one nearest the player by the traffic system's own lanes, or at a corner the street that crosses it: at five cross sections of each the middle of the carriageway is half way between its outermost lanes, and the one taken has walls on both sides if any has, then the longest clear run. `shots.txt` says whether it had walls on both sides, on one, or on neither. The skyline looks the way with the most buildings within 30 degrees and 120 to 1300 m, so never off the map's edge, from the edge of a roof 12 to 130 m up with 60 m of open air before it. A place on the map is taken by the building that stands at it, measured by its renderers; a marker with no building is no picture.
- **The interface.** `ScreenshotController.ToggleUIVisibility`, the game's own screenshot switch, hides the game's whole interface and its markers, and with them the mod's HUD, which stands in the game's canvases. The mod's minimap and mouse look stand aside (`CineDirector.Active`), the pointer is hidden and the world's clicks are held, so nothing is outlined under it. The camera stops drawing the interface's layer, which markers and name tags that stand in the world are on, and the game's guiders to a destination. A picture is drawn into a render target of its own, which no screen interface is ever drawn into.
- **The game's speed.** The game is paused through its own speed control (`GameSpeedController.SetPause`, with the pause key locked), as photo mode does, so nothing moves while the camera is away from the player; every frame of a run the pause is put back if something of the game's ended it.
- **The light.** The game has a time of day tool of its own (`InGameTimeOfDayPanel`, `TimeOfDayController.usingSliderDebugTool`), which shows the sun, sky, fog, windows and lamps of any hour from 5:00 to 24:00 without moving the clock. The tool uses it exactly as that panel does, and sets `forcedHour` too, which is what the street lamps ask. **The clock is never written**, so no hour and no day turns. The four hours come from the game's own day: the golden hour is the warmest hour at which its sun still has a third of its strength (the first run took the sun's height alone, and got a sun that had all but faded). The mod's own look (Realistic graphics) follows the hour the game lights the city for (`GraphicsGame.LitHour`) and is told at once, since its own timer waits on the game's clock.
- **The light is read back, and names the picture.** After the camera is placed, the game's sun, moon and lamps are read (`CineGame.Seen`), and the picture's name carries the light they show (`CineJudge.Light`): day or midday, golden, blue, night. A shot that asks for night while the game shows the sun is named for the sun, and the list and the log say what was asked.
- **The air.** The pipeline's volumes are blended where the camera stands, as the pipeline blends them (`CineGame.Air`), and the fog's reach read. For a camera in the mist the reach alone is lifted, in the tool's own volume: to 450 m at the least, or two and a half times the shot's depth (a skyline 1000 m, the long lens 500 m, the street at eye height 300 m), 3000 m at the most. It is never thickened, so the clearer air of a look stays; the fog's colour, its height and its switch are not touched. The list says both figures.
- **Weather.** Not changed. The game's rain is drawn from the save's seed with its next start written in the save, and the mod's weather slows traffic and changes grip; neither can be set for a picture without side effects.
- **The picture, held against the screen.** For each shot the screen's own frame from the camera's place is read first. Then the camera draws into a 3840 by 2160 target for sixteen frames and the target is read at the end of a frame. Both are reduced to three figures (brightness, spread, saturation, from a grid of 96 by 54 pixels). If the target's picture is not the screen's, or is flagged where the screen's is not, the screen's own frame is saved in its place, drawn larger by a whole number where the screen has fewer than 2160 lines (if the engine gives that; else at the screen's size). The game's own view is read from the screen before the camera is moved, and again in each new light, and each picture is flagged against it: flat, grey or fogged. The PNG and the JPG are encoded and written on a worker.

## Giving back

Everything changed is first written in `CineHold` and given back once, in this order: the tool's line, the picture's target, the light, the look, the lens, the camera's layers, the camera's place, the brain, the game's two camera turns, the interface, the pointer, the world's clicks, the game's zoom, the game's speed, the player's walk. The run's `finally` does it when the run ends, is stopped with Esc or faults; the feature's cleanup, the object switched off and the city unloading do it themselves. One log line at the end says how many parts were held, that none is left, and that the clock did not move.

## Limits

- With no look chosen under Realistic graphics, a night picture is the game's own night: lamps, lit windows and a grey sky, at the day's exposure. The name says night because the sun is out; the picture is as bright as the game makes it.
- What has changed with the hour shown: the game counts its walkers and its traffic by the hour, so a night shot has fewer of both, and they take a moment to come back afterwards. Things of the mod's that read the clock itself (not the game's light, and not the look) keep the real hour's appearance.
- Pictures show what the game draws near the player: its people, cars and lamps are drawn to 200 m of the player, wherever the camera stands, and its far plane ends the view where the game's ends.
- People of the mod's own pools who were out of view when the game was paused may not be drawn.
- Refused in a Going Public session: the camera and the light are every player's.
- The rays tell a wall from open air, not a good picture from a dull one. A frame that passes them can still be an ugly one.
