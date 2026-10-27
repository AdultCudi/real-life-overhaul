# The Economy app

## In a sentence

An app on the phone that draws the city's economy as dashboards in a look of its own, dark to begin with: a verdict on the economy, headline tiles with their changes over a week and a month, charts of prices, inflation and the bank rate, the jobs the cycle moves, every district ranked by its pull on customers, the stock index and its sectors, and the council's books, with a small daily history in the save and plain advice worked out from the figures.

## Where the code is

- Folder: [`Scripts/Outlook`](../../Assets/Mods/RealLifeOverhaul/Scripts/Outlook/). The app's name in the game is Economy (the phone id is `RealLifeEconomy`); "Outlook" is the folder's.
- Read first: `OutlookCatalogue.cs` (every number), `OutlookRules.cs` and `OutlookInsights.cs` (game-free), then `OutlookDirector.cs`.
- Wiring: `Scripts/Wiring/Wiring.OutlookLinks.cs`. The Finance app's line and button: `Scripts/Economy/Finance/FinanceApp.City.cs`.

```
Outlook/
  OutlookCatalogue    EVERY NUMBER: the history's length, the series' names, the tiles, the verdict's and the advice's thresholds (game-free)
  OutlookBook         the daily history: a ring of 120 days for each of at most 72 figures (game-free)
  OutlookStore        its one line in the save, "reallife:economy_app" (game-free)
  OutlookReadings     one look at the game as plain data: the city, jobs, districts, market, council, tax, the player (game-free)
  OutlookRules        a change over days and its tone, the verdict, the districts' ranking (game-free)
  OutlookInsights     the advice, by rule from the readings, ranked (game-free)
  OutlookWords        the sentences from the figures, and a figure written one way everywhere (game-free)
  OutlookScale        the charts' axes and smooth lines for any series at all (game-free)
  OutlookTheme        the two palettes and the contrast arithmetic that holds them to WCAG AA (game-free)
  OutlookGame         the only file that reads the game: the city, the districts' data, the player, the tax
  OutlookBridges      what it asks of the other features (jobs, market, council, the districts the mod adds to)
  OutlookStudy        the trends and the verdict of one look, in one place for the app, Finance and the tests
  OutlookDirector     owns the history and the timing: the app on the phone, the day, the hour, the save; its parts .App and .Tests
  OutlookApp          the app: the frame, header and tabs; its parts .Overview .Prices .Jobs .Districts .Places .Markets .Finances .Parts
  OutlookPage         a drawing surface: cards, pills, lettering, pictures and places to click, made once and used again
  OutlookMesh         one UI mesh for a picture: lines, fades, bars rounded at the top, discs, rounded boxes
  OutlookDraw         the charts on it: the small line, the large chart with its axes, the cycle's bands, meters
  OutlookKit          the shapes drawn once into textures (rounded box, circle, soft shadow) and the parts' makers
  OutlookIcon         the app's own glyph for its icon
  OutlookText         the words in the game's language; OutlookGuide the feature entry and the options block
```

## How it talks to other features

- It reads and writes nothing of the game's. `OutlookGame` reads the city's economy (`CityEconomyDirector.State`), each district's data (`NeighborhoodData`), the player's debts, rent and wages tomorrow (`Books`), the buildings and businesses they have, and the game's tax. Everything else comes through `OutlookBridges`, which `Wiring.OutlookLinks` fills: the jobs (the tech sector's unemployment table, the careers' vacancies and layoffs), the stock market (index, sectors, their histories), the council's books, and Uptown's and Cedar Heights' demand, market level and buildings going up.
- The Finance app reaches it through `EconomyBridges` (`OpenEconomyApp`, `EconomyAppOn`, `EconomyVerdict`), and so do the city's notices (`EconomyBridges.OpenCity` opens the Prices and rates tab).
- No Harmony patch. The app is a panel among the game's apps (`GameUi.PhoneApp`), put on the phone like the others, with the Money folder as its place (`PagerRules.ModOrder`, `PhoneLayoutRules.Defaults`).

## The figures it draws, and which have a history

| Figure | Kept by | History before the app | In the app |
| --- | --- | --- | --- |
| Price index, inflation, bank rate, the cycle | the city's economy (`CityState`) | the current value; the last month's rise; the rate a month before | daily history (`prices`, `infl`, `rate`, `cycle`) |
| Rents and wages, takings, what customers spend | the city's economy (`CityModel.Costs`, `Takings`, `Spend`) | the current factor | follows `prices`; `spend` |
| Unemployment among professionals, vacancies, jobseekers, the chance a job is lost | the tech sector's and the careers' tables, by the cycle | the current value | daily history (`unemp`, `vac`) |
| A district's income level | the city's economy | the current value and a month ago | daily history for each district (`di:<id>`) and their mean (`income`) |
| Footfall by district and cycle, what each district bears | the city's economy | the current value | footfall `foot` |
| Property worth, land by the square metre | the city's economy and the game's district data | the current value | `prop` (the mean); each district's worked out from the stored cycle, prices, income and level |
| Uptown's and Cedar Heights' demand by use, market level, buildings going up | `Uptown` and `Construction` | the current value | daily history (`dd:`, `dl:`, `grow`) |
| The stock index and its sectors | `Markets` | 100 trading days of candles; each sector's closes | read from the market, not copied |
| The council's reserve, week's takings and spending, departments | `Civic` | the current value | daily history (`res`, `rev`, `spd`) |
| The income tax rate and the bill owing | the game | the current value | none |

The mod keeps no figure for consumer confidence and none for a city-wide unemployment rate. The app draws neither and invents neither: what customers spend and the footfall (the cycle's, and each district's) are the nearest figures, and unemployment is the tech sector's own table for professionals.

## The history

One line of the save's modData, `reallife:economy_app` (`OutlookStore`, version 1): `v=1~l:{last day}~s:{figure}|{first day}|{readings};...`, the readings split by commas. A reading is `=n` (whole), a bare number (its change from the day before), nothing (the same) or `n` (no reading that day). The city's model run for 130 days, with 23 figures (the national ones, nine districts' incomes and Uptown's level and demand), is a line of about 5,000 characters; the most the book can hold, 72 figures that change every day, is about 55,000.

- Bounded: `OutlookBook` is a ring of the last 120 game days (two of the game's 60-day years) for each figure, at most 72 figures, so nothing grows. A day older than the window cannot be written.
- Old saves: a save without the line starts an empty book and fills it a day at a time; the first look of a save writes that day. Until a change over 7 or 30 days has a reading to compare with, its pill shows a dash. The first month stands on what the city's model remembers of itself (the last month's rise in prices, a district's income a month ago).
- A line this version cannot read (damaged, or a later version's that does not parse) is kept as it was read (`SaveLines`); a later version's line is read as far as it goes.
- Written at the game's day event (after the city's economy has done its day), at the first second of a save, and over the day's reading each time the open app is written. Saved with the game's own save (`SaveHook`).
- Going Public: the history is this machine's own and nothing is sent; a guest reads the host's economy as the city's economy shows it.

## The tabs

The app fills the whole of the game's app area (3840 by about 1750 units on a 16:9 screen; every position is worked out from the width it is given, so a narrower screen has fewer columns). A header, a row of tabs, and a page that scrolls.

- **Header**: the title "Economy" large and bold; beside it the verdict as a chip (a dot and the word, in green for Growing, blue for Steady, amber for Slowing or Overheating, red In recession); at the right the game's date as a quiet pill and a round button that swaps the dark and light looks (a sun while it is dark, a moon while it is light). A soft glow of the accent colour fades into the ground behind it. Beneath it the tabs.
- **Tabs**: a rounded track of seven pills, Overview, Prices and rates, Jobs and people, Districts, Markets, City finances, Insights; the tab showing is a filled pill in the accent colour, the others quiet.
- **Overview**: the verdict card (the word at 100 units, the sentence under it, three fact pills: the month of the state, days to the rate decision, the busiest district; at the right the cycle through the last 120 days as a row of coloured bands, boom green, steady blue, slowdown amber, recession red, with its legend); eight headline tiles in a grid of up to four columns (Prices, Inflation, Bank rate, Unemployment, District income, Property worth, Stock index, Council reserve), each with its figure in large type, a pill for its change over 7 days and one over 30 (an arrow and its size, green where the change is good news, red where it is bad), and a small line with a fade under it and a dot at the last reading; a click leads to its tab; then the top three pieces of advice and a pill to all of them.
- **Prices and rates**: three large chart cards (the price index, inflation a year, the central bank's rate as a stepped line), each with the value, the two change pills, gridlines with the figures at the left, a fade under the line, the last reading marked with a value label, and the days at the foot; a card of rents and wages, takings and what customers spend, each on the game's own figure with a meter, what you pay tomorrow and any adjustments carried over to the next day; the central bank's rate, last decision and next; and the six kinds of goods' wholesale prices as bars up or down from the usual line, a shortage in red and a glut in blue, and "you stock it".
- **Jobs and people**: unemployment among professionals as a chart card; the vacancies open as a stepped chart; a card for someone who hires (jobseekers, the chance a job is lost, whether hiring is hard, easy or usual); and a card of every district's income level as horizontal bars, longest first, each in the colour of the district on the city map.
- **Districts**: each district as a card in a grid of up to three columns, ranked by its pull on customers, with its map colour down the edge, a rank badge in that colour, the pull, its income level large with its change over 30 days and its line, four figures (property worth, land by the square metre, footfall, the prices its customers bear), its people by class as a stacked bar; Uptown and Cedar Heights add four rounded bars for the demand for homes, shops, offices and stores (green from 70, red up to 25), their market level and the buildings going up. Under the districts, "Your businesses and buildings" (when the player has any): a card of up to twelve businesses, each with a dot in its district's map colour, the district's name, the customers its district, the economy and its prices add or take (green, red or grey) and a pill saying its prices suit the district or are over what it bears; and a card of up to twelve buildings with the game's value, what each is worth today and the difference as a pill. Beyond twelve, a line says how many more. These are what the Finance app's page for the city listed.
- **Markets**: a large chart card of the city's stock index with the market's state and the day's move; below it a card for each sector (up to twelve, the biggest movers first) with its level, the day's move and its line.
- **City finances**: the council's reserve as a chart card; its week (takings, spending, what is left, with meters); its four departments, each as five steps from two under to two over with the level marked; and the game's income tax rate with the bill owing and the day it is due.
- **Insights**: every piece of advice as a card, in order.

A piece of advice is a card with a stripe down its left edge and a round badge in the colour of its kind (green with an arrow for a chance, amber with a mark for a risk, blue with an "i" for news), what has happened in bold, what it means under it in quiet type, and two pills: the tab it points to and its kind. A click leads to that tab.

## The look

The owner, 27 Oct 2026: "Keep the app theme dark as default. Also make it beautiful not the same old UI." For this app the rule that everything is the game's own interface is set aside, as it is for Flare: the panel, the opening, the phone and the font (the game's own, found once) are the game's; the screens are drawn in `OutlookPage` and `OutlookMesh`. Built once and used again; nothing runs per frame.

- **Dark** (to begin with): ground `#0F1420`, cards `#171E2E` with a one-pixel outline `#2A3550` and a soft shadow, raised parts `#1F2940`, text `#F2F5FA`, quiet text `#8A94A7`, the accent teal `#2DD4BF`, green `#2ECC71`, red `#FF5C5C`, amber `#FFB020`, blue `#5AA9FF`. **Light**: ground `#F3F5F9`, cards white, text `#111827`, quiet `#586275`, accent `#0A7468`, with darker news colours. The option "Look of the Economy app" sets it, and so does the round button in the header.
- **Type** (canvas units of the game's 3840 by 2160): the title 66, the verdict 100, a tile's figure 76, a card's heading 38, body 30, small 26, axis and captions 24 in capitals.
- **Shapes**: cards 40 units round, pills fully round, a card's shadow a soft fall-off nine-sliced from one texture; bars rounded at the top; a chart's line 5 units wide with a soft edge, joined at the points, smoothed through them by a monotone curve (never past a point), the area under it fading from the accent at a third of its strength to nothing.
- **Contrast**: every lettering of both looks is held to WCAG AA (4.5 to 1) on the surface it is set on, by `OutlookTheme.Pairs` (EconomyCheck's OutlookChecks); a label on a line's or a district's colour takes near-black or white, whichever holds, darkening the fill if neither does (`OutlookTheme.Legible`); the district colours of the city map are lifted to 3 to 1 against a card (`OutlookTheme.Visible`).

## The verdict

A recession and a slowdown are the city's own states. A boom or a steady economy with prices rising 1.5 points over the 2% target (3.5% a year) is **Overheating**. A boom is **Growing**. A steady economy is **Slowing** if over 30 days the districts' mean income lost 3.0 points or the stock index lost 8%, **Growing** if it gained 2.0 points or the index gained 5%, and **Steady** if neither or both. The sentence under the word says what the state does: the footfall and spending of the cycle, the inflation against the target.

## The advice

Twenty-three rules, each reading figures the mod keeps and saying nothing when they are not kept; none invents a number. Each is a card of two sentences: what has happened, with its figures, and what it means for the player. Ranked by a score: the rule's weight, 24 points for touching something the player has (debts, shops, staff, a district they have a business or a building in, a kind of goods they stock), and up to 20 for the size of the move; ties by rule then district id, so the same readings give the same advice on every machine. At most 24 are kept, three on the Overview.

| Rule | Reads | Says |
| --- | --- | --- |
| Rate raised or cut | the last decision of the central bank | new loans cost more and overdrafts and cards follow (pay down debt), or cost less (borrow for what pays) |
| Rate due | an announced decision within 3 days | borrow before a rise; a loan can wait before a cut |
| Prices high or low | inflation at 3.5% or more, or 0.5% or less (the target is 2%) | check your own prices; costs are steady |
| Prices up, and you pay rent or wages | the price index over 30 days, 3% or more | check your margins |
| Jobs tight or slack | unemployment at 4% or less, or 7% or more | staff cost more, or are easy to hire and easy to lose |
| Boom, slowdown or recession | the cycle's footfall and spending | stock up and hire, or keep stock and costs lean |
| Property up or down in a district | its worth over 30 days, 3% or more | any building of yours there is worth more or less today |
| Income up or down in a district | its income level over 30 days, 2 points or more | customers there bear higher or lower prices |
| A shortage or a glut | a kind of goods moved 15% or more | buy before the rise, or buy more; worse for the kinds you stock |
| The stock index moved | 5% or more over 30 days | investors confident or nervous |
| Demand high or low | a use in Uptown or Cedar Heights at 70 or more, or 25 or less | developers build and values may rise; or units stand empty |
| Buildings going up | 3 or more in one of them | more people and foot traffic |
| The council runs short | its week's spending over its takings, or a reserve below nothing | the reserve drains and approval of the budget suffers |

## Rivals and the city read the same signals

Parity with the rivals and the NPCs: the app shows nothing that only the player has. The rival tycoons' worth grows with the cycle the verdict reads (`RivalCatalogue.CycleGrowthBp`); the city's developers start a building where a use's demand is high, the demand shown in the Districts tab; the careers' vacancies and layoffs and the tech firms' hiring follow the tables the Jobs tab draws; the managers of the player's shops and the rivals' businesses read the district income levels the app ranks. Where a rival decides on expansion or hiring it already uses those figures, so nothing was added to them.

## The Finance app

The Overview keeps three lines (the bank rate, prices, and the economy, now with the verdict) and a button, "Open the Economy app", where it had "The city". The page "The city" is gone from the Finance app; the Economy app has all of it: the rate and the next decision, prices, inflation and the economy on the Prices and rates tab (with rents, wages and takings against the game's own figures and what is carried over), the districts' income, the kinds of goods and the shortages and gluts, and, under the Districts tab, each of your businesses against what its district bears and each of your buildings at today's value. The notices about the bank rate and the supply open the Economy app on Prices and rates.

## Options, tests, diagnostics

- "Economy app on the phone" (`economy_app`, on) and "Look of the Economy app" (`economy_app_look`: Dark, Light), in the Money section. Off, the app is not on the phone and the history stops; what was kept stays in the save.
- Options > Tests: "Open the Economy app and check every tab" (each tab must draw; where the figures are kept it must draw a line of them; the advice is checked against the game's own figures, read afresh; PASS or FAIL to Player.log), "Show a sample year in the Economy app" (the city's model run for 130 days over the game's districts, never saved; again to put the save's own back), "Write the Economy app's advice in the log".
- With Developer diagnostics on, each day's write is one line, `Economy check (app history, day n): ... PASS`, and each page written one line with its timing.

## Cost

No Update and nothing per frame. Once a second a few comparisons (the director's timer). Once a game day a pass over the dozen or so districts and a few lists, writing a few dozen numbers into the history. Once a game hour while the app is open, and on each tab picked, the page is written again from parts made before: once the pools have grown to the largest page there is nothing new made or destroyed, only parts set to new places, lettering and meshes. Opening the app for the first time in a session makes its parts and three small textures once. None of this has been timed in the game.

## Checks

`EconomyCheck` (`OutlookChecks.cs` and `OutlookTextChecks.cs`, run alone with `--outlook`, 127 checks, and with the rest in the gate):

- The history: the book bounded in days and in figures, its ring, the line in the save round trip for sixty books with steps, gaps and the extreme numbers, an old save, a later version's line, damaged lines refused.
- The rules: a change over days and how it reads; each of the five words of the verdict at its boundary; the districts' ranking; each of the twenty-three rules speaking at its threshold and silent a step short of it, and its sentence carrying the figures it read.
- The drawing: the charts' axes and smooth lines for a flat series, one point, none, negatives, huge numbers, NaN and infinity; the contrast of both palettes (every pair of text and ground at 4.5 to 1 or more).
- The texts: every key the code names in English, Portuguese and Chinese with the same names in curly brackets and in the other six languages as the game reads them, no dashes or exclamation marks, the notices at most 240 characters, every rule's sentences whole in three languages, the Guide's figures from the code, What's new in all eleven languages.
- The places: the readings of the player's businesses and buildings used again and trimmed.
- The sources: no Update, no scene search, no Harmony, the guard on the mesh, one file that reads the game, every registration (the phone's order, the accent, the start seat, the feature owner, the options, the wiring, the Finance app's button).

Each was made to fail once, in a scratch copy of the worktree with one line of the code or of a text changed: the figures bound, the days bound, the verdict's boundary, an advice rule's threshold, the chart axis for a flat series, the quiet colour of the dark look, a record of a place not cleared, an `Update` method added, a placeholder dropped from a Portuguese text, a dash in an English text, and the Korean What's new line left empty. Each run failed in the check named for it and the copy was put back.

## Not verified (the game could not be run)

How the screens look: every size and position is worked out from the width the app is given, and none has been seen on a screen. That the game's font carries the lettering of every language in the sizes used; that the nine-sliced shadow reads well on the dark ground; the first opening's cost; the figures of a real save over a long game; the behaviour of the scroll wheel over the page in the game's full menu.
