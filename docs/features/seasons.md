# Seasons

## In a sentence

The year's four seasons, as the calendar has them, change how much of each good the player's customers buy, and on some winter days it snows: flakes fall, the ground turns white, and traffic and people in the street slow down.

## Where the code is

- Folder: [`Scripts/Seasons`](../../Assets/Mods/RealLifeOverhaul/Scripts/Seasons/).
- Read first: `SeasonRules.cs` (every figure and rule, game-free) and `SeasonDirector.cs` (once a second: the goods, the snow, the news).

```
SeasonRules          the goods' table, the season of a day, the snow of a day and how much lies     EconomyCheck/SeasonChecks.cs
SeasonGame           THE GAME SIDE: the clock, each good's sales ratio, the rain's place, the walkers' paces
SeasonGame.Snow      THE GAME SIDE: the game's soft round particle for the flakes, and its rain hidden while snow falls
SnowHush             the game's wet look held dry while snow falls over its rain (on only then)
SnowLook             the flakes (one particle system fitted to and following the camera) and the white ground (one decal projector on the snow's own decal layer)
SnowGround           the ground, roads, pavements and roofs given that decal layer behind the loading screen, and given back
SeasonLookRules      how each season looks, game-free: the leaves' colour, the season's rain and lightning, falling leaves, the sky   EconomyCheck/SeasonLookChecks.cs
SeasonGame.Sky       THE GAME SIDE: the sky's copy of the game's fair-weather settings, the season's rain through the game's own
SeasonGame.Trees     THE GAME SIDE: the trees' leaf materials tinted in memory and put back
SeasonGame.Coats     THE GAME SIDE: people in the street in the game's own warm clothes (off by default)
SeasonLook           autumn's falling leaves (one particle system) and a storm's lightning (one directional light)
SeasonBar            the season and its day under the top bar's date (a copy of the date's label, a picture drawn in code)
SeasonDirector.Look  every three seconds: every look above, and standing down for Alcware Seasons
SeasonDirector       keeps it going: the goods once a season, the snow once a second, the forecast and the news
SeasonText           the seasons' names, the goods that sell more and less, the snow's hours
SeasonGuide          the feature's entry, the options block, the Guide page's figures
SeasonBridges        the save's number (from the street events' calendar)
SeasonDirector.Tests the two test buttons
Wiring/Wiring.SeasonLinks.cs             the save's number, the snow on the roads for the weather, the Calendar's line
Wiring/Wiring.CalendarSeasons.cs  each season's first day and the forecast's snow on the Calendar
```

## How it talks to other features

- **The calendar** (`Core/CalendarRules`): the season of a day is `CalendarRules.Season`, the quarters of the year the save chose, the year beginning in midwinter. The Calendar app shows each season's first day with what sells more, the snow of the next seven days, and on a day's page the season's line (`CalendarDirector.SeasonWords`, wired in `SeasonLinks`).
- **The weather** (`Events/Weather`): `Weather.SnowNow` asks the seasons for the snow on the roads (0 clear, 1 in patches, 2 covered). The weather multiplies its factors by it (below), so the driven car's grip and traffic's speed follow through `WeatherEffects`, as they do in rain. A change of snow alone does not repeat the sky's warning (`Weather.SnowOnlyChanged`).
- **The street events' calendar** (`Events/EventCalendar.Salt`): the save's own number, kept in the events' save line and shared by the host in a Going Public session (`SharedCity` part S). The snow is rolled from it and the day.
- **The city's economy** is untouched: its supply side already moves wholesale prices by season (`MacroCatalogue.SeasonPush`); this is the demand side.
- **Road paint** (`Transport/RoadPaint.Decal`): the game's own decal material, found behind a loading screen, which the snow on the ground copies.

## What sells when

The game's retail customers buy each product in proportion to the item's `productSalesRatio` (`CustomerEntriesCalculatorRetail.AddProductsToCustomersEntriesList`: customers x ratio x the district's demand / 100 x satisfaction x the product's impact). The ratio is a field of the item asset in memory, never in a save, so it is written as the city's economy writes wholesale prices: the game's own ratio times the season's figure, for the 49 goods of `SeasonRules.Goods`, once when the season turns and once after a load, and put back exactly when the option goes off, on a fault and when the city unloads. A ratio another mod has changed since it was written is taken as the good's own from then on.

The game draws a day's customers at midnight (`BusinessHelper.RunDaily`, before `onNewDay`) and on load, so the season counted is the one of the day the next draw is for (`SeasonRules.DrawDay`: from 23:00 the next day's). The figure also reaches what the mod's staff rota reads (`RotaGame`, the same formula) and the game's own estimate of a week's income (`EstimatedWeeklyIncomeHelper`). The game sizes a rival's shop (and, in saves begun before build 2847, the player's) by the largest ratio among its primary products (`BusinessHelper.GetMaxHcpsqmForRegistration`), so a rival ice cream parlour has fewer customers in winter too.

Market Insider's demand (`NeighborhoodDemand.demand`) is not changed: it is saved, capped at 100 and worked out again by the game whenever a provider changes.

Each good's four figures average 970 to 1060 thousandths, so a year of seasons gives or costs a shop next to nothing; no figure is under 600 or over 1500.

| | winter | spring | summer | autumn |
|---|---|---|---|---|
| coffee, tea | +15%, +20% | | -10%, -15% | +5%, +10% |
| ice cream, slushies | -30% | +5% | +35%, +30% | -10%, -5% |
| soda, energy drinks, beer, margaritas | -10% to -5% | | +10% to +20% | |
| wine, whisky, martinis | +10% to +20% | | -15% to -5% | +5% |
| gifts, jewellery | +15% to +25% | -5% | -10% to -5% | +5% |
| flowers | +15%, +20% | +20%, +15% | -5% | -15% |
| salads, fruit, vegetables | down | up | +5% to +20% | apples and pears +15% |
| hot dogs, kebabs, cotton candy | | | +15% to +20% | |
| pizza, frozen food, popcorn | +5% to +10% | | | |
| umbrellas | -5% | +15% | -20% | +20% |
| clothes | +10% | | -15% | +10% |
| phones, watches, headphones, books | +10% to +15% | -5% | | |

The game has no swimwear or hot chocolate to sell, so they are not in the table.

## Snow

- **Which days.** A winter day snows if its roll (`SeasonRules.Roll(salt, day, 41)`) is under 300 thousandths. The fall starts at an hour from 0 to 20, lasts 2 to 9 hours, and is over by midnight; one in three (350 thousandths) is heavy. The same save's number and day give the same snow on every machine, and nothing about it is saved.
- **How hard.** `Falling` is 0 outside the fall's hours, 0.5 in a light fall and 1 in a heavy one, rising and dying away over 24 minutes.
- **How much lies.** `Cover` is what the day's fall has laid (a heavy fall 1, a light one 0.55, reached after two and a half hours of falling) on top of what lay at midnight, melting away over the next 16 hours. Snow lies until midnight once fallen, so only the day before matters.
- **The roads.** `Step`: under 0.2 clear, from 0.2 in patches, from 0.6 covered. With "Weather affects driving, crime and energy" on:

| | accidents | grip | traffic speed | street life | sight | walkers set off at |
|---|---|---|---|---|---|---|
| in patches | x1.2 | x0.88 | x0.93 | x0.9 | | x0.93 of their pace |
| covered | x1.45 | x0.72 | x0.85 | x0.8 | x0.9 | x0.85 of their pace |

  On top of the sky's and the spell's factors. Walkers: the game's pedestrian pool gives each person who sets off a pace from its animator overrides' ranges (`PedestrianPool.ActionOnGet`); those ranges are narrowed while snow lies (no walker under 0.6 m/s) and put back exactly. People already walking keep their pace. Traffic's pace is narrowed by the weather as in rain, and not on a Going Public guest, whose traffic is the host's.
- **The forecast.** At 18:00 the evening before, and between 7:00 and 10:00 on the day if the fall is still to come; a warning when the first flakes fall. All go through the morning summary while the player sleeps (`Core/MorningNews`). Only with the weather option on, as its own forecast is.
- **The season's first morning**, between 7:00 and 10:00: the season and the three goods that sell most more and most less.

## What snow looks like (`SnowLook`)

- **Flakes:** one particle system kept round what the camera sees: a box from 2 m under the player to over the camera, centred between the camera and the player, square to the world and moved after the camera four times a second while it snows (a few reads and one position). Simulated in the world, so the camera moves through the snow. The flakes are the game's own soft round particle, its wheel spray's material (`WheelParticlesDrops`, the `SmoothCircle` dot of its particle shader graph), found behind the loading screen and drawn unchanged as billboards; where it cannot be found, small lit pieces of the shared paint instead. Fitted to the camera (`SeasonLookRules.Field`): a flake is 0.0034 m for each metre the camera stands off (3.5 to 15 cm), about four pixels across where it looks on a 1080-line screen; heavy falls hang 1.1 to 3.2 m apart (closer for a near camera), light ones wider by the cube root of their strength; the box is 1.3 times the distance plus 16 m a side (24 to 72 m) and the camera's height plus 6 m high; at most 6,000 at once. For the game's street camera 35 m off: 12 cm flakes 2.9 m apart, about 4,900 in a heavy fall; for the mod's camera 6 m off: 3.5 cm flakes 1.2 m apart, about 2,900. Each grows in and shrinks away, so none pops. Nothing falls indoors.
- **The ground:** one decal projector, 150 m square round the player, a copy of the game's road paint (`RoadPaint.Decal`, an HDRP/Decal material) carrying a 128 pixel white made once (two layers of tileable noise, mostly opaque with thinner patches), its pattern pinned to the street. Its fade is the cover, in fiftieths. It follows the player when they are 25 m from its middle.
  - **Only on the ground.** A decal draws on every surface in its box unless HDRP's decal layers are in effect. The game's HDRP asset (`HDRP_High`) supports them (`supportDecalLayers`), but its camera's frame settings leave `DecalLayers` off (the mod's performance census: "frame settings OFF: ... DecalLayers"). In 0.51.0 the snow read the asset alone, so its 76 m box whitened everything in it, cars, people, faces and walls, wholly in heavy snow. Now:
    - behind the loading screen (`SnowGround.Mark`, one search of the scene's renderers), every renderer on the game's own `Ground`, `GroundUnplacable`, `Roads`, `Sidewalk` and `Buildings` physics layers, never a skinned one, is given one decal layer of the snow's own: the one of rendering layer bits 9 to 15 the fewest other renderers carry (`SeasonRules.SnowBit`; none do in this build, so bit 15). Any other renderer carrying it is taken off it while marked. Cars and people (`0x101`: light layer default and decal layer default) are never on it, so nothing spawned later is either. The game's own decals stay on the default layer and draw as before;
    - the projector is drawn on that layer alone (`decalLayerMask`), and while it lies outdoors the player's camera is given `DecalLayers` through the one owner of its frame settings (`PerfFrameSettings.Enable`, given back on going indoors, when the snow melts, with the option off and when the city unloads);
    - the cover is shown only once the camera draws with decal layers (`HDCamera.frameSettings`), so for no frame can it whiten everything in its box. The angle fade (30 to 60 degrees), which HDRP applies only with decal layers, keeps walls and fronts clear.
  - **Where that cannot be had** (decals or decal layers off in the asset, no ground layer by name, or a camera that will not draw with them after four seconds) no snow lies at all: it still falls, and the roads' grip and traffic still follow it, said once in the log. There is no thin slab at the player's feet as a fallback any more: it reached tyres, shoes and the foot of every wall.
- Nothing is drawn indoors. Off ("Show the snow falling and on the ground"), nothing is drawn and the snow is still forecast and felt.

## How each season looks (`SeasonLookRules`, `SeasonDirector.Look`)

Each part has its own option (all on but people's clothes) and comes of the day, the hour and the save's number, so every machine of a session sees the same.

- **Trees** (`season_trees`). The game's trees (`TreeController.AllTrees`, its own list, 7,578 in New York) draw their leaves with a handful of shared materials of its wind shader (`VegetationShaders/VegetationWindShader`, coloured by `_MainColor`; trunks are on other materials). They are found by walking 300 trees a second (the first half minute of a city), and each is coloured its own colour times the season's tint, in memory only, written when the tint changes (twentieths: a few times a game day) and put back exactly. The tint by the part of the year: midwinter dull brown (0.78, 0.72, 0.62), fresh light green in spring (0.88, 1, 0.78), the game's own through summer, amber (1, 0.80, 0.45), red and orange (1, 0.58, 0.32) and brown (0.82, 0.55, 0.34) through autumn, dull again by winter. The game's own leaves falling from trees keep their colour.
- **Snow in the place of rain.** While snow falls or lies on the roads, or the snow test holds, the game's own rain is not seen or heard on this machine: the renderers of its rain effect (drops, sheets, splashes) are switched off and its sound muted (`RainHelper.RainAudio`, contract part `snow_fall`, optional), and its two wet-look values (`_hov_Puddles`, `_Rain`) are written dry after the game each frame by `SnowHush`, on only while the game's rain falls under snow. Each is put back exactly when the snow stops. The game's weather is never written (raining or not, its strength, its hours), so its people, traffic, the mod's weather and Going Public's weather sync go on as the game has them. The seasons start no shower or storm on a snow day, and end their own if snow comes.
- **Rain** (`season_rain`). Spring showers on 300 of 1000 days (1 to 2 hours), autumn rain on 400 (2 to 6 hours), summer thunderstorms on 180 (1 to 3 hours), starting from 6:00 to 20:00 and over by midnight (`SeasonLookRules.RainOn`). It is the game's own rain (`RainHelper.Command_ToggleRain`, its build-up, drops, puddles and sound), at 0.5, 0.7 and 1 of its strength; the game draws its own length, so the season's end is written into its end and fade times (contract part `season_rain`) and it fades out by itself. Nothing starts while the game's own rain falls. On a Going Public guest nothing is started: the host's rain, the season's included, is brought by Going Public. The game then schedules its next rain from that end, as after any of its own.
- **Lightning.** While a summer storm's drops fall on the player outdoors: a flash every 9 to 40 seconds (`FlashGap`, the same every time), two strokes of a directional light of 16,000 lux over a quarter of a second, switched off between. The game has no thunder sound, and the mod adds none.
- **Sky** (`season_sky`). The game's light, exposure, fog and sky colours come every frame from its fair-weather settings (an `EnvironmentSettings` asset), blended into its rain's as a shower comes (`TimeOfDayController`). A snow day's overcast (`Overcast`: how hard it snows, or 0.4 of what lies) takes a copy of the fair-weather asset 0.8 of that of the way to the rain's (the sun, exposure, fog and the sky's three colours, sampled half-hourly), and summer's warmth lowers the sun's colour temperature by 650 K (not in a storm). The copy is put in the asset's place (contract part `season_sky`) and written only when either changes in twentieths; the game's own is put back when both are 0. It is light, not a tint: people and cars are lit as on any grey day.
- **Falling leaves** (`season_leaves`). Autumn only, most in its middle: up to 26 a second over a 36 m square round the player, small flat pieces of the shared paint in a warm brown, swaying down over 14 seconds; none while rain falls or indoors.
- **People in the cold** (`season_coats`, off by default). One person a second off screen, from the game's list of people walking (`PedestrianSpawner.activePedestrians`), is given a warm top in autumn, and long trousers too in winter: among their own model's variants (sweaters, hoodies, jackets, high-necked shirts; jeans and long trousers; the game has no coats), in their outfit's style (its tags), the same pick for the same outfit. Only the Torso and Legs elements change (`AppearanceSetter.UpdateElements`); body, face, hair and skin are never touched. Three a second are put back as they were once the cold is over or the option goes off, all at once when the seasons stop or the city unloads.
- **Under the date** (`season_bar`). "Winter, day 3 of 15": a copy of the game's own date label (its font and colour, 0.8 of its size), outside its layout, with the season's picture drawn once in code (a snowflake, a flower, a sun, a leaf). Written when the day changes.
- **Alcware Seasons.** Another author's mod with seasons of its own (`OtherMods.AlcwareSeasons`, its assembly `AlcwareSeasons` with `AlcwareSeasons.SeasonsMod`). Both would change the same things, so while it is switched on these seasons stand down whole (what sells, snow, sky, rain, trees, leaves, clothes and the line under the date), with one notice and one log line, unless `season_beside_alcware` (Keep these seasons with Alcware Seasons, off) is on.

## Cost

One timer comparison a frame. Once a second: the season (a few comparisons), the snow of the moment (two rolls of the day and the day before, no allocation: EconomyCheck times it), the weather's one call, and the look's comparisons. The goods: 49 writes once a season and once after a load. The walkers' ranges: a handful of writes when the snow on the roads changes. The particle system and the decal are Unity's to draw; the projector moves once every 25 m. The white is made once a session (16,384 pixels). The ground's marks: one search of the scene's renderers a city, behind the loading screen, and a bit set on each piece of ground; given back by a walk over the same list. While snow lies outdoors the camera draws with decal layers, a little more of the graphics card's time (HDRP writes a decal layer target with the depth prepass); never otherwise. Two reads a second follow the game's camera while it lies. The Calendar's source: a few comparisons a day of the month looked at, and the snow of at most eight days.

## Going Public

The season comes of the day and the year the save chose; the snow of the day and the save's number, which the host shares (part S of the shared city). So every machine has the same seasons and snow. The goods' ratios are each machine's own items, read only for its player's businesses' customers. Nothing is sent, and nothing of another player's is changed.

## The save

Nothing. Everything is worked out from the day and the save's number. A save loads as it was without the mod.

Each look, every three seconds: a few comparisons. The trees: a walk of 300 trees a second until all are known, then a few colour writes a game day. The sky: a copy of one asset and its curves rebuilt when the overcast or warmth moves a twentieth. The rain: the game's own command once a shower. Lightning: a short routine of a few frames for each flash. Leaves: Unity's particles. Clothes: one person a look at most, the game merging their meshes as it does when it dresses them (its own allocation, named in the session record); the mod keeps the worn pieces by reference and writes the change through two kept pieces, so it makes nothing itself. The line under the date: written once a day.

## Options and tests

- `seasons` (Seasons change what sells, and it snows in winter, on): off, every ratio is the game's and it never snows.
- `snow_look` (Show the snow falling and on the ground, on): off, nothing is drawn, for slower machines. The ground is marked behind every load while the seasons are on whatever this option says (the mark is a rendering layer bit no decal of the game's uses, harmless while no snow lies), so switching it on in play whitens the ground at once. Its tip says that snow lies only where the game's graphics settings allow it, never on people or cars.
- Test: "Show this season's goods and the coming snow in the log": each good's figure and its ratio as the game has it and now, the next eight snow days, and `Seasons test (goods): ...: PASS` when every ratio is the game's own times the season's and the snow falls only in winter and the same when asked again.
- Test: "Let it snow here now (press again to stop)": a heavy fall and the ground whitening over twenty seconds, held three minutes. Six seconds on, `Seasons test (snow): N flake(s) in the air, S cm across and D m apart for the camera C m off (about P px where it looks, at 1080 lines), ...; the ground marked: N piece(s) behind the loading screen T s ago; rain seen: no (...); ...: PASS` when flakes are falling and big enough to be seen from the camera, no rain of the game's is seen, the ground is covered, the roads are snowy to the weather and traffic is slower; then `Seasons test (snow look): the ground whitened on the snow's own decal layer (...) | not at all (why); N renderer(s) of M car(s) and person(s) within 40 m, the player's own among them, 0 on the snow's layer; ...: PASS` (one physics query of the cars and people near the player, once). Refused indoors.
- Options `season_trees`, `season_sky`, `season_rain`, `season_leaves`, `season_bar` (on), `season_coats` and `season_beside_alcware` (off).
- Tests: "Seasons: show me spring / summer / autumn / winter here now (press again to stop)": that season at its height where the player stands, held three minutes (summer with a storm and spring with a shower, at once, except on a Going Public guest; winter with a heavy fall). Fifteen seconds on, `Seasons test (autumn look): the trees x(1.00, 0.58, 0.32) on N leaf material(s), ...; the sky ...; rain ...; n flash(es) of lightning; n leaf(s) falling; under the date: '...'; n person(s) dressed for the cold: PASS`, and for winter the snow look's line too.
- With Developer diagnostics on, each time the goods are written: `Seasons check (demand, day n): 49 good(s) held for winter, 0 not as wanted, 0.05 ms: PASS`.

## Offline checks

`EconomyCheck/SeasonChecks.cs` (`--seasons` runs them alone): the table (bounds, every year near even, no good twice, the player's ask), the ratio, the goods that sell most more and less, the season of every day of every year length from 8 to 400 against the calendar's, a season's first day, the draw's day, the snow over five save numbers and three year lengths (only in winter, in its hours, over by midnight, about 30% of winter days and 35% of them heavy, the same when asked again, another save another winter), how hard it falls, how much lies at every quarter hour (nothing before it falls, thicker as it falls, as it was at midnight the next morning, gone in 16 hours), the roads' steps, no allocation and the time of a moment's snow, the sources' use of the rules, every figure of the Guide page from the code, the texts in every language and What's new.

## Not done

- Snow drifting in through doorways, footprints, or snow on cars and people: snow never lies on them.
- Lying snow on a graphics setting without HDRP's decal layers: a decal there whitens whatever stands in it, so none is drawn.
- Ground on the game's `Default` layer (some squares and plazas) is not marked: what it is cannot be told from its layer.
- Thunder: the game has no thunder sound, and the mod adds no sound files of its own.
- Coats, scarves and hats: the game's clothes have none; its sweaters, hoodies and jackets stand in.
- A season length of its own: a season is a quarter of the year the save chose, as the game's calendar has it.
- Market Insider's percentages: they are the game's saved demand, worked out again by the game.
