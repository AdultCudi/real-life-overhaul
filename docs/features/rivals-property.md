# The rivals' buildings and their fortune

## In a sentence

The game's own rivals (the nineteen of its Rivals app) own buildings by their place in the app's ranking: the top rivals own the most, the last owns a handful, a new game is dealt that way at once and a game under way is moved towards it a few buildings a day, through the game's own sales to and purchases from the market; and wherever the mod shows one of those rivals it counts what the rival owns in its fortune, beside the weekly income the game ranks it by.

A player started a new game, saw the rival in 19th place of 20 own about 130 buildings, and asked that a rival's buildings be more gradual by its place and count in its fortune. Another player said the cheapest building for sale was about 200,000.

## What the game does (Build 3682, read from the decompiled game)

- **Who the rivals are.** `RivalsHelper.GenerateRivals`: 19 at a new game, four special rivals, seven wholesalers and eight importers (`NumberOfInitialRivals`). The three tycoons of [the rich list](../reference/wiring.md) are the mod's own and are not these.
- **How many buildings each is given.** `CityGenerator.DistributeBuildingsToRivals`, at a new game and again for each district the mod adds (Uptown, Cedar Heights: `LifeGame`): of the buildings the player does not own, that are no special service and no Hamptons house, 24 per cent go to the four special rivals, half to the first five of the others, and the rest to the other ten, each group in equal chunks in the order of the rivals' ids. A rival's place in the ranking never enters. A building the player sells goes to a random rival (`RealEstateHelper.SimulateCompetitorBuyingPlayerBuildings`).
- **How the ranking is made.** `RivalLeaderboard.Load` sorts the rivals and the player by weekly takings (`RivalData.WeeklyIncome`: the last seven days of the rival's shops and offices; `GetPlayerLeaderboardData`: the player's weekly profit). Buildings are in no figure. The Rivals app shows their number and a table of them (value, price) beside the rival.
- **The market.** `RealEstateHelper.RunDaily`: a day in ten one building for sale goes to a random rival; each district keeps three buildings for sale, picked at random from any building the player does not own, a rival's too (`SetBuildingForSale` blanks the owner and lists it at its market value). So the game already lets rival buildings go to the market and come back to rivals; nothing in it follows a place.

## What the mod does

### The curve (`Economy/Estate/RivalHoldingsRules.cs`, game-free)

The rivals hold some number of buildings together. The place `p` (0 the top) of `R` rivals has the weight `(R - p)^2`: the last one, the first 361 for nineteen. Each gets `Least` = 4 buildings, and the rest of the buildings is shared by weight, the odd buildings to the largest fractions and then the better place (`Targets`). Why this shape: the weight falls evenly with the place and steeply enough that the last is "a handful" and the top many times that, but the second is not far under the first, so no rival owns a quarter of the city; it never rises towards the bottom, adds up to the whole and keeps four buildings for even the last (`RivalHoldingsChecks.cs`). Of 1,200 buildings among nineteen rivals the places hold, from the last, 5, 6, 8, 11, 15, 20 and so on up to 50 (tenth) and 168 (first); of 5,000 the last holds 6 and the first 724. With fewer buildings than four for each rival, each has what the total gives it and the rest follows the same curve.

The worth follows the place with the number: a rival in the top half that has more than its share lets go its cheapest buildings and a rival there that has less buys the dearest it may; the bottom half does the opposite, so the leaders keep the big buildings and the last rivals the small ones. In the checks the top six hold more than ten times the worth of the bottom six.

### The pace (`RivalHoldingsRules.Pace`)

The option "Rivals' buildings by their place" (`rivals_holdings`, under Rivals; the Guide's page "Rivals") has three steps:

- **As the game deals them** (0): the mod does nothing.
- **Fit their place in a new game** (1, the default): in the first three days of a game (`NewGameDays`) every rival's buildings are dealt at once (`AtOnce`: nobody has seen the city yet, and no building is the player's); the save then carries a line (`reallife:rival_holdings`, `RivalHoldingsStore`) that says it was dealt, and from then on that game moves gently as the ranking changes. A save that was not dealt that way is left as it is.
- **Fit their place in every game** (2): as above, and a save under way too, gently from the day the option is switched on.

Gently (`Day`) is each game day: the rivals furthest over their shares put up for sale up to 3 buildings (`SalesPerDay`: the game's own `SetBuildingForSale`, the listing at the market value the mod already writes), and the rivals furthest under buy up to 3 (`BuysPerDay`) of the buildings that have been for sale at least 3 days (`ListedDays`: the player has them first, at the game's own price and offers) and never the last 2 for sale in a district (`ListingsLeast`). So at most 21 buildings a week go to the market and 21 come back to rivals; no building vanishes, and a building a player has an offer on is the player's to buy until a rival has it. The rivals' lists are built again once after the moves (`RivalsHelper.RefreshRivals`) and the map's for sale marks follow.

It never reads into its book, so never touches: a building the player owns or rents, a special service, a Hamptons house, a building of a DLC the player does not have (`GameDlc.Gated`), an address the game's ignore list holds (the mod's own held buildings among them), and in a Going Public session another player's; in a Going Public session the whole part rests (the rivals there are the host's). Every pick is by value and then by index, so the same city gives the same moves on every machine. The game keeps doing its own part meanwhile (each district's three random listings, a rival buying one a day in ten), which pulls the holdings off their shares a little each day and which the next days put right: the offline year of gentle days leaves out that noise, the game's own listing is what the player will see.

The ranking it reads is the game's own: `RivalRanking` sorts the rivals by weekly takings as the app does (a tie by id) and leaves out the defeated. It rides the property business's day (`EstateDirector.HoldingsDay`, `EstateGame.Holdings`), so it needs the property part on; a fault stops only this part for the session.

### Their fortune (`Rivals/RivalFortuneRules.cs`, `RivalFortuneGame.cs`)

One rule for every page: a rival's **fortune** is the market value of the buildings it owns plus half a year (26 weeks, the same half year a rival puts up when it buys a business, `ServicesCatalogue.RivalPurseWeeks`) of its weekly takings and the rent its buildings would bring at the market's rate (7 days of `Building.GetBuildingDailyMarketRent`). So a rival with 130 buildings cannot read as poorer than its property, whatever its takings. The game's own number, the weekly takings the ranking is made of, is not written or changed anywhere; the mod's figures stand beside it.

- **The RLO interface** (the Rivals app on the kit, `Ui/Rivals`, `Ui/RivalsViews.cs`): a Fortune column in the league, and in the chosen rival's head the buildings (their number and worth), the rent a week, the fortune, and where the order by fortune differs from the game's, "By fortune: 1 of 19, the game's 19". The player's own row and a defeated rival's have none.
- **The game's interface** (the old one, which cannot be added to without drawing over the game's own Rivals app): the Finance app's Rivals page (`RivalPage`, `WriteProperty`) has a kind of row "Property", one for each rival in the game's order, with the fortune in the figure column and the buildings, their worth, the rent and the takings in the note.

Both read the game's registrations through one reading kept for the game day and the rival's number of buildings (`RivalFortuneGame.Of`): nothing is searched or allocated on a redraw.

## Cheaper homes

What the game and the mod offer at the low end, read from the code:

- The market lists whole buildings only, three a district picked at random (`UpdateBuildingsForSale`), priced by the building's market value, which the mod writes as the game's value times the market, prices, the district and the building's condition (`EstateRules.Value`; a run-down building, a quarter of the city, keeps 79 to 88 per cent of it: `ConditionValue`). There are no flats or studios to buy as units in the game, in Uptown or in Cedar Heights: the district's smallest building is an A1 of 75 square metres, a whole building.
- The mod's mortgage (`Economy/Banking`) asks at least 20 per cent down (`HomeDepositLeast`). The cheapest building a player reported was about 200,000, a deposit of 40,000, which is 26 to 36 weeks of a gig a day (four game hours a shift, 40 to 55 an hour: `JobCatalogue`), before the mortgage's own approval.
- What this changes: the leading rivals now let go their cheapest buildings first (up to 21 a week), so the cheapest buildings of the rivals reach the market in every district instead of three random ones. The prices are the mod's value of the building as before; the listing rule, not a special case, brings the small homes. It does not bring a price down to a deposit of a few weeks: that would need a flat or a studio to be a unit one buys on its own, and a building's units are only the mod's tenants' homes today (`EstateRules.UnitCount`); the player never owns a unit of a building. That is a feature of its own.

## What this does not do

- The real estate agency as the player's own business and a place to visit a rival are other features.
- It does not change which rival is in which place: the game ranks them by takings, and only the buildings follow.

## Where the code is

```
Economy/Estate/RivalHoldingsRules.cs     game-free: the curve, the pace, the new game's deal, a day's moves, the book
Economy/Estate/RivalHoldingsStore.cs     game-free: the save's one line (the game was dealt)
Economy/Estate/RivalRanking.cs           the game's order of its rivals
Economy/Estate/EstateGame.Holdings.cs    the Game file: the book read from the registrations, the moves made
Economy/Estate/EstateDirector.Holdings.cs  the day, a start, a fault, the test
Rivals/RivalFortuneRules.cs              game-free: the fortune by one rule, the place by fortune
Rivals/RivalFortuneGame.cs               the buildings' value and rent read, kept for a day
Ui/Rivals/RivalsScreen.cs, Ui/RivalsViews.cs   the kit's Rivals app: the column, the facts
Rivals/RivalPage.cs                      the old interface's Finance page: the "Property" rows
```

Checks: `EconomyCheck --rivalholdings` (`RivalHoldingsChecks.cs`, in the full run too). Tests: Options > Tests, "Test: the rivals' buildings" (one gentle day now, a notice of what moved and what the first and the last rival hold).
