# Realistic graphics

## In a sentence

A look for the whole picture, chosen on the options page, that follows the clock, the weather and the season by setting sliders of work the game's renderer already does in every frame, so the default choice adds no pass for the graphics card, runs nothing of the mod's own per frame and allocates nothing.

## Where the code is

- Folder: [`Scripts/Graphics`](../../Assets/Mods/RealLifeOverhaul/Scripts/Graphics/).
- Read first: `GraphicsRules.cs` (every figure of every look, game-free) and `GraphicsDirector.cs` (when a look is worked out and written).

```
GraphicsRules           the looks, the table of every setting a look may write, the day's phases, the budgets, the verdicts   EconomyCheck/GraphicsChecks.cs
GraphicsPicture         the arithmetic: the pipeline's custom tone curve, the game's own curve, a light's colour, white balance, contrast
GraphicsModel           the game's own day as its files have it, and a grey card followed through the game's picture and a look's
GraphicsGame            THE GAME SIDE: the mod's one volume, the camera's layer, the sun and the moon, the game's events, other mods' volumes
GraphicsDirector        keeps it going: once a second of game time, a door, the map, the designer, the option
GraphicsDirector.Tests  the three test buttons: the cost measured here, every hour shown, what is on the picture now
GraphicsGuide           the feature's entry, the options block, the Guide page's figures
GraphicsBridges         what it asks of the seasons: the season, snow falling, snow lying
Wiring/Wiring.GraphicsLinks.cs   those three answers, each behind a try with a wiring fault line
```

## How it talks to other features

- **Seasons** (`Scripts/Seasons`): the look asks for the season the city shows and for the snow that falls and lies (`GraphicsBridges`, filled in by `Wiring.GraphicsLinks`). It only reads. The seasons go on swapping the game's sky settings and showing the game's rain as snow; while it snows the look lays none of rain's own darkness, gloss or mist on top of the snow's.
- **The Picture sliders** (`World/VisualEnhancer`, the options under 'Picture'): a volume of priority 1000 with the pipeline's contrast and saturation. The look's volume is at 800, under it, so the sliders are added on top of the look as they are on top of the game's picture.
- **The Performance options** (`Performance/Tweaks`): shadow reach and cascades are theirs (they edit the game's own shadow settings), and so are the camera's frame settings. The look never writes either.
- **The game contract** (`Core/GameContract`, part `graphics_shade`): the game's corner shading is another maker's plug-in (HBAO), reached by name. If a game update moves it, Balanced and High leave it as the game has it and say so once.
- **The map check** (`Core/MapParts`, "the picture's look"): the session record asks, after the city map has shut, whether the look is on the picture again.
- **Going Public**: nothing is sent and nothing is read from the session. A look comes of the clock and the weather alone, so every machine works out the same one. The hours' test is refused in a shared session, because the game's clock tool also sets the session's traffic.
- **Notifications**: the tests' answers carry `_test_` in their keys, the mod's mark for a notification that pops up whatever the player has switched off.

## What the game does now

Read from the game's files (build 3682) and its code.

| | |
|---|---|
| Renderer | Unity's High Definition pipeline, one pipeline asset for each of the game's three quality levels |
| Built without | screen-space reflections, screen-space global illumination, the pipeline's own ambient occlusion, volumetric fog, volumetric clouds, ray tracing, dynamic resolution (no upscaler): every one of these is switched off in all three pipeline assets, which is decided when the game is built |
| Shadows | the sun's shadow map is 2048, 1024 or 512 pixels by quality level, already the largest its asset allows; the filtering is set with the asset too; contact shadows are allowed in the frame but no light asks for them |
| Anti-aliasing | none, FXAA or SMAA, by the game's option. There is no temporal pass, so nothing to sharpen with |
| Corner shading | a plug-in (HBAO) drawn after the opaque things, strength 1 and a radius of 1 m in the street, up to strength 2 in a room, off by the game's option |
| Exposure | fixed, EV 10, day and night. The game writes an exposure for the hour too, but onto a setting that is not switched on, so nothing reads it |
| Tone curve | the pipeline's fitted ACES curve, which tops out at 0.949: nothing on the screen is ever quite white |
| Colour table | 32 to the side, built again in every frame from whatever the grading settings are |
| Sun | on from 6:00 to 21:00, up to 13,500 lux, 6500 K through the day, colder at dawn (7800 K at 6:00, 7100 K at 7:00) and warm only in the last hour (3600 K from 20:00). The game writes its strength, its temperature and its turn in every frame, and never its colour |
| Moon | the rest of the night, 1,000 lux, white |
| Sky | a gradient of three colours by the hour, with a second set for rain |
| Fog | a ground mist: it reaches 5 to 25 m up and a ray goes 42 to 60 m in it, by district and hour, in the sky's colour |
| Glow | bloom from a brightness of 1, strength 0.1 to 0.7 by district |
| Street lamps | some 4,400 in the city, their colour set in the prefab and never in code; they cast no shadows unless the game's shadows option is at its highest |
| Not in the city's picture | chromatic aberration, vignette, film grain, motion blur, depth of field: none of the city's profiles has any, so there is nothing cheap to take out |
| Volumes | fourteen in the city's scene, district by district, priorities 0 and 1; the city map's own (priority 1, on only while the map is open); the menu's (100); a room's (1) |

## Why it looks the way it does

- **One exposure for noon and midnight** and a curve that never reaches white leave the day with grey whites and the night as bright as a dull afternoon.
- **A white sun all day and a white moon.** The only warm light is the last hour before the sun goes out, and dawn is colder than noon.
- **No aerial perspective.** The mist hugs the ground, so a street six blocks off is as crisp and as dark as the one underfoot, and nothing fades into the sky.
- **Glow from everything brighter than white** puts a haze over sunlit walls by day.
- **Flat shade.** The sky lights the shade with the same strength at every hour, and the only corner shading is a narrow one.

None of that is a missing pass. It is the settings of passes the game already runs, which is why a look can change it at no cost.

## What a look changes

A look is one global volume of the mod's own, over every volume of the game's, on a layer of its own (31) that only the player's camera is given. The game's profiles are never edited: with the volume off the picture is the game's exactly. The staff uniform preview's camera reads every layer, so it is kept off the look's.

The Free choice writes these and nothing else (`GraphicsRules.Knobs`, 39 rows):

| The pipeline's setting | What the look sets | Why it costs nothing |
|---|---|---|
| Tonemapping | a custom curve in place of the fitted one: a toe, a shoulder that reaches white | the colour table is built in every frame whatever the curve |
| Exposure | compensation in stops, by the hour and the weather | a number in the same exposure the game applies |
| White balance | temperature, by the hour, the weather and the season | part of the same colour table |
| Channel mixer | stronger or paler colour that never moves a grey (each row adds up to 100) | part of the same colour table |
| Split toning | a tint for the dark parts and one for the bright parts, each as bright as the grey it replaces | part of the same colour table |
| Bloom | the brightness a thing must have to glow, and how wide the glow spreads | the game's bloom is on in every district |
| Fog | how far a ray goes, how high the haze reaches, its tint | the game's fog is on in every district |
| Indirect lighting | the sky's light in the shade, the sky's reflection in glass and paint | two multipliers in every lit pixel |
| The sun and the moon | a colour filter over the game's own light, its largest part 1 | the lights are lit; a colour is a colour |

### The three looks

| | Natural | Filmic | Vivid |
|---|---|---|---|
| Curve (toe strength, toe length, shoulder strength, length, angle) | 0.32, 0.61, 0.945, 4.75, 0.10 | 0.33, 0.59, 0.94, 3.55, 0.20 | 0.33, 0.59, 0.92, 3.75, 0.09 |
| Exposure in stops (day, golden hours, blue hours, night) | +0.05, +0.17, +0.06, +0.10 | +0.14, +0.32, +0.18, +0.22 | 0, +0.08, +0.04, +0.08 |
| Colour's strength, 1 the game's | 1.06, 1.10, 0.98, 0.93 | 0.97, 1.04, 0.92, 0.86 | 1.16, 1.22, 1.06, 1.02 |
| White balance | 0, +8, -6, -4 | -1, +10, -8, -6 | +2, +11, -5, -3 |
| The sun as it comes up, at noon, as it goes down | 3200 K, 5850 K, 3000 K | 3100 K, 5600 K, 2800 K | 3400 K, 5900 K, 3100 K |
| The moon's filter (red, green, blue) | 0.74, 0.86, 1 | 0.64, 0.84, 1 | 0.70, 0.84, 1 |
| The sky's light in the shade (day, night) | 0.94, 0.92 | 0.90, 0.88 | 0.97, 0.95 |
| The haze: metres a ray goes | 450, 340, 380, 620 | 380, 280, 330, 520 | 560, 420, 460, 720 |
| The haze: metres it reaches up | 170 | 190 | 150 |
| Glow from a brightness of (day, night) | 1.65, 1 | 1.70, 1 | 1.55, 1 |

The day has four phases, each a share from 0 to 1 that turns smoothly round the hours the game itself lights its sun and puts it out: day, the golden hours (after sunrise and before sunset), the blue hours (either side of the night) and night. A look's setting is its four figures mixed by those shares.

- **Weather.** Rain is 0.18 stops darker by day, cooler (5 on the white balance), a little paler, with the haze at 42% of its reach, the sky's reflection 0.2 stronger, and lamps that glow a little more at night. Lying snow is 0.10 stops brighter by day with blue shade, and must be brighter before it glows. Falling snow halves the haze's reach. Only the thickest of the three hazes is taken, never one on top of another.
- **Seasons**, while they are on: winter 2 cooler and 3% paler, spring 2% fuller, summer 2 warmer and 3% fuller, autumn 3 warmer.
- **A room** has figures of its own and no fog, sky light or corner shading of the mod's. The sun's and the moon's filters hold indoors too, since the same lights shine through the windows.
- **Where the picture is the game's own**: the city map (the mod's build mode is the map), the interior designer, the blueprint maker. The volume is switched off there and the two lights are given back.

### The game's brightness is kept

`GraphicsModel` follows a grey card lying on open ground through the game's picture (its sun, moon and sky by the hour, its fixed exposure, its curve) and through a look's, and the checks hold every minute of the day to it:

- in fair weather the card is within 10% of the game's, in the sun and in the shade, in every season, at the game's contrast and at the Picture slider's own +10;
- in rain and snow, within 15%;
- through the night the card in the shade never falls under 0.92 of the game's;
- in a room, within 10%.

What changes is the ends: what the game shows at 245 of 255 a look shows as white, and a deep shadow is a little deeper.

It is a model of flat open ground. It knows no building's shadow, no lamp and no corner shading, so it says that a look is no darker picture, not what a street looks like.

### Nothing jumps

The picture moves with the clock's minute (a second of play) and the weather's twentieth. While the game's own time of day tool shows another hour (its console panel, or the [cinematic shots](cinematic-shots.md), which show other hours through it), the look follows the hour the game lights the city for (`GraphicsGame.LitHour`), so its exposure, colours and haze are never another hour's. Each row of the table has a step, the most it may move from one minute or one twentieth to the next, and the checks hold every look to it: 0.012 stops, 0.7 on the white balance, 1 point of the mixer, 28 m of haze, 0.025 of the sun's filter. While the game has its sun out the filter stands still, so nothing is written for a light that does not shine.

## What Balanced and High add

Off until the player chooses one. They add work for the graphics card, each within a budget.

| | Balanced, budget 0.5 ms | High, budget 1.2 ms |
|---|---|---|
| Contact shadows | from the sun, rays of 0.2 m, 10 samples, out to 150 m | from the sun and the moon, 0.3 m, 16 samples, out to 200 m |
| Micro shadowing | finer shading in creases, 0.7 | 0.9 |
| Corner shading (the city only) | the game's own made 1.5 times as wide and 1.15 times as strong | 1.8 times as wide, 1.3 times as strong |

The corner shading is written only while the game's own is on in the game's options: its strength is what starts the pass, so a player who has switched it off keeps it off. A room keeps the game's, which is up to twice the street's already.

The budgets are budgets. No cost has been measured on a real computer yet: the measuring test (below) is how each is confirmed, and the look is off by default until it has been.

## What this renderer cannot do

Said plainly, and not faked:

- **Volumetric fog, screen-space reflections, screen-space global illumination, the pipeline's own ambient occlusion, volumetric clouds and ray tracing** are switched off in all three of the game's pipeline assets. That is fixed when the game is built: the shaders and buffers are not there to switch on.
- **A larger shadow map** for the sun: it already has the largest its asset allows, and the atlas is sized when the pipeline starts.
- **Softer shadow filtering**: also set when the pipeline starts.
- **Street lamps that cast shadows**: the game's lamp script sets each lamp's shadows itself, by the game's own option, and again whenever a lamp's detail level changes.
- **Sharpening**: there is no temporal anti-aliasing and no upscaler to sharpen with.
- **Taking out cheap effects**: the city's picture has none to take out.
- **Street lamp and window colours** are the game's own. A lamp's colour is in its prefab and a lit window is a material shared by its building: neither is a slider of a pass. Changing them means finding and writing thousands of lights and materials behind a loading screen, which waits until it can be seen in the game.
- **Wet roads**: the puddles and the wet look are the game's own and the seasons'. The look only makes the sky's reflection stronger in rain.
- **Another mod's phone map** that copies the player's camera with every layer sees the look too (below, Other mods).

## The performance contract

- **No frame method.** The director has no Update. Its one timer is a wait of a second of game time, made once, so in a pause it stands still.
- **Nothing unless something moved.** A second in which the minute, the weather, the place and the options are the same ends in a comparison. Otherwise a look is worked out (under a microsecond in the checker, nothing allocated) and only the sliders that moved by more than 0.0005 are written, twenty at most.
- **Nothing searched.** The camera and the two lights come from the game's own fields. Other mods' volumes are looked for only on a door, the map, the designer or an option, never on the timer.
- **Nothing started.** The Free rows are held against a list of the switches and sliders that start a pass, a buffer, a texture or more samples (`GraphicsChecks.cs`), and the costly settings are not even in the volume until a costly choice first asks.
- **Everything given back.** Off, a fault, the options' clean-up and the mod unloading each put the two lights and the cameras' layers back as found; the volume's profile is destroyed with its settings.

What the offline checks prove is in `EconomyCheck/GraphicsChecks.cs` (`dotnet EconomyCheck.dll --graphics`). What only the game can show is the cost on a real graphics card, which is the first test below.

## Other mods

- A global volume another mod made at run time that sets one of the pipeline's settings the look sets too (its tone curve, say) is left that setting: the look's own is switched off, setting by setting, and Player.log says so once for each.
- The look's volume has its profile as its own already, so a mod that asks every volume for its profile gets no copy that would leave the look unread.
- **Immersion - RPG Camera** does ask every volume for its profile, and with its 'no city fog' option on it switches the fog off in each, the look's own among them: the look's haze goes with the game's mist, as that player asked, and the rest of the look stays. The look never writes the fog's switches, so the two do not fight.
- **Voogle Route**'s phone map is drawn by a copy of the player's camera that reads every layer, so it shows the look too. It is a camera another mod makes at run time, and finding it would be a search of the scene.

## Tests

Options, Test tools, with 'Show test tools' on:

- **Measure what each graphics choice costs here (about a minute).** Close the menu and stand still. It shows the game's own picture and the look in turn, three pairs of two seconds for each of the three costs, reads the engine's own frame timing, and writes one line for each cost to Player.log, beginning `Graphics check (cost,` and ending PASS or FAIL against the budget (or NOT SHOWN where the pairs disagree by more than the difference, or NOT JUDGED where v-sync or a frame cap is on and the engine gives no time for the graphics card).
- **Show every hour of the day in the picture.** Each hour for four seconds by the game's own clock tool, the save's clock left alone. Refused in a shared session.
- **Write what the graphics have changed now to Player.log.** One line beginning `Graphics check (now)`: the look, the cost, the place, the hour, the weather, what is written, and the grey card by the rules.

With Developer diagnostics on, a line beginning `Graphics check (` is also written whenever the look, the cost or the place changes.

## Not yet seen in the game

Everything on this page about the game's files was read from them, and everything about the looks' arithmetic is checked offline. What no check can show, and nobody has yet looked at in the game:

- how the three looks read on the screen, hour by hour, and whether the haze suits the game's high camera;
- the cost of each choice on a real graphics card (the measuring test);
- that the engine's frame timing gives a time for the graphics card in this build;
- that layer 31 is read by no other camera of the game's, and that the game's contact shadow shader is in the build;
- that the game's corner shading takes the wider radius through the mod's volume;
- that another mod's volume is told from the game's own by how it was made.
