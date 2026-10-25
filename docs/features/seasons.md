# Seasons

## In a sentence

The year's four seasons, as the calendar has them, change how much of each good the player's customers buy, and on some winter days it snows: flakes fall, the ground turns white, and traffic and people in the street slow down.

## Where the code is

- Folder: [`Scripts/Seasons`](../../Assets/Mods/RealLifeOverhaul/Scripts/Seasons/).
- Read first: `SeasonRules.cs` (every figure and rule, game-free) and `SeasonDirector.cs` (once a second: the goods, the snow, the news).

```
SeasonRules          the goods' table, the season of a day, the snow of a day and how much lies     EconomyCheck/SeasonChecks.cs
SeasonGame           THE GAME SIDE: the clock, each good's sales ratio, the rain's place, the walkers' paces
SnowLook             the flakes (one particle system) and the white ground (one decal projector)
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

- **Flakes:** one particle system, hung 10 m over the place by the player where the game's rain falls (`RainHelper.RainEffect`, contract part `snow_fall`), simulated in the world so the player walks through it. Up to 230 small white cubes a second over a 44 m square, falling about a metre a second for 11 seconds, drifting with low-quality noise. The paint is the shared lit material (`Shapes.Paint`) with a little glow, so flakes show at night. At most 2,600 at once.
- **The ground:** one decal projector, 150 m square round the player, a copy of the game's road paint (`RoadPaint.Decal`, an HDRP/Decal material) carrying a 128 pixel white made once (two layers of tileable noise, mostly opaque with thinner patches), its pattern pinned to the street. Its fade is the cover, in fiftieths. It follows the player when they are 25 m from its middle.
  - With HDRP's decal layers on (`supportDecalLayers`), it reaches from 70 m over the player to 6 m under their feet with an angle fade from 30 to 60 degrees, so the ground and the roofs turn white and walls do not.
  - Without them HDRP ignores the angle fade, so the box is a 70 cm slab at the player's feet: the ground and pavements only, and the bottom of what stands on them.
  - With decals off in the game's graphics there is no white ground, said once in the log.
- Nothing is drawn indoors. Off ("Show the snow falling and on the ground"), nothing is drawn and the snow is still forecast and felt.

## Cost

One timer comparison a frame. Once a second: the season (a few comparisons), the snow of the moment (two rolls of the day and the day before, no allocation: EconomyCheck times it), the weather's one call, and the look's comparisons. The goods: 49 writes once a season and once after a load. The walkers' ranges: a handful of writes when the snow on the roads changes. The particle system and the decal are Unity's to draw; the projector moves once every 25 m. The white is made once a session (16,384 pixels). The Calendar's source: a few comparisons a day of the month looked at, and the snow of at most eight days.

## Going Public

The season comes of the day and the year the save chose; the snow of the day and the save's number, which the host shares (part S of the shared city). So every machine has the same seasons and snow. The goods' ratios are each machine's own items, read only for its player's businesses' customers. Nothing is sent, and nothing of another player's is changed.

## The save

Nothing. Everything is worked out from the day and the save's number. A save loads as it was without the mod.

## Options and tests

- `seasons` (Seasons change what sells, and it snows in winter, on): off, every ratio is the game's and it never snows.
- `snow_look` (Show the snow falling and on the ground, on): off, nothing is drawn, for slower machines.
- Test: "Show this season's goods and the coming snow in the log": each good's figure and its ratio as the game has it and now, the next eight snow days, and `Seasons test (goods): ...: PASS` when every ratio is the game's own times the season's and the snow falls only in winter and the same when asked again.
- Test: "Let it snow here now (press again to stop)": a heavy fall and the ground whitening over twenty seconds, held three minutes. Six seconds on, `Seasons test (snow): ...: PASS` when flakes are falling, the ground is covered, the roads are snowy to the weather and traffic is slower. Refused indoors.
- With Developer diagnostics on, each time the goods are written: `Seasons check (demand, day n): 49 good(s) held for winter, 0 not as wanted, 0.05 ms: PASS`.

## Offline checks

`EconomyCheck/SeasonChecks.cs` (`--seasons` runs them alone): the table (bounds, every year near even, no good twice, the player's ask), the ratio, the goods that sell most more and less, the season of every day of every year length from 8 to 400 against the calendar's, a season's first day, the draw's day, the snow over five save numbers and three year lengths (only in winter, in its hours, over by midnight, about 30% of winter days and 35% of them heavy, the same when asked again, another save another winter), how hard it falls, how much lies at every quarter hour (nothing before it falls, thicker as it falls, as it was at midnight the next morning, gone in 16 hours), the roads' steps, no allocation and the time of a moment's snow, the sources' use of the rules, every figure of the Guide page from the code, the texts in every language and What's new.

## Not done

- Snow drifting in through doorways, footprints, or snow on cars and people beyond what the decal gives the tops of them.
- Ground snow that keeps off walls without HDRP's decal layers: that needs a mask of where the ground is, which is either a search of the scene or physics queries over many frames.
- A snowy sky: the game's sky is darkened only by its own rain's transition, which is worked out every frame from its own clock.
- Market Insider's percentages: they are the game's saved demand, worked out again by the game.
