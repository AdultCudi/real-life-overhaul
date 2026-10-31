# The city's population

## In a sentence

Under the living economy's master option **Living population**, every flat in the city's residential buildings, and above many of its shops, is a home with a household of real people who age, study, work, pair up, have children, move and die; they earn, pay rent and tax and spend at the businesses within reach, and every business's customers (the player's, the city's and the rivals') follow them from the game's own count on the business's first day.

## Where the code is

- Folder: [`Scripts/Population`](../../Assets/Mods/RealLifeOverhaul/Scripts/Population/); the wiring in `Scripts/Wiring/Wiring.PopulationLinks.cs`; the mod's copies of the game's customer arithmetic read `Scripts/Core/CustomerScale.cs`; Uptown's demand reads `Scripts/Uptown/DemandBridges.cs`.
- Option: "Living population" (`living_population`, the overhaul's master switch, on in the beta; the feature "living" owns it). Off, the population stands down: the game's own customers, the city economy's own district incomes, the Economy app's tables by cycle and Uptown's fixed ratios stand, exactly as before.
- The design: `not-in-repo/plans/tech-overhaul/POPULATION.md` (with the owner's answers of 1 Nov 2026).

```
Population/                     the rules files are game-free and compiled by EconomyCheck (PopulationChecks.cs, PopulationYearsChecks.cs, PopulationSaveChecks.cs)
  PopulationCatalogue             every figure, each with its unit and its source (the game's own where it gives one)
  MeasureRules, CityTable         homes and jobs from each building; the city's table laid out, a saved population bound to the city at a load
  PopulationState                 households, people, employers and businesses as plain arrays with stable ids; a household's monthly ledger
  PopDraw                         the seeded draws (the same city from the same seed on every machine)
  GenerateRules                   the people generated from the buildings
  LifeRules, RegionRules          a day of life; the month's moves, the region's arrivals and departures
  LabourRules                     employers, jobs filled by residents and commuters, pay
  SpendRules, PoolRules           a household's day of money; the day's pools shared among businesses to the cent, and a household's part worked out after
  HousingRules                    rents by vacancy, choosing a flat, each district's pull for construction
  CustomerRules                   each business's factor on the game's own count
  PopulationSim                   the work in slices: the generation, the day's turn, the month's
  PopulationStore, PopulationShare  the save's line; the Going Public part
  PopulationViews                 the Economy app's screens as view models
  PopulationLinks                 other systems' people linked to residents
  CityPopulation                  what other features read
  PopulationDirector              the timer, the day's turn, the save, Going Public, the tests
  PopulationGame                  the only file that touches the game; .Calculators: the game's customer calculators with the population's count
  GameRules                       the people's games: the genres they like and the platforms they play on, the game studio's audience
  PopulationAttention, PopulationBridges, PopulationText, PopulationGuide
```

## The measure

The owner chose the bigger city (1 Nov 2026): 18,000 to 22,000 households and 50,000 to 65,000 people. A home is sized as a real flat, not as the game's unit: a residential building's first flat is the game's own unit (the one it lets, at its size and rent), and the rest of its floor times 0.82 net is flats of its district's size, 52 m2 in the Garment District, 54 in Industry City, 56 in Lower Manhattan, 66 in Hell's Kitchen, 78 in Murray Hill and 88 in Midtown (New York's average rented flat is some 65 to 70 m2 net); Uptown's and Cedar Heights' flats are the mix of their classes' (52, 68 and 90 m2). The residential buildings alone hold some 13,000 such flats, so a share of the floors above the shops is homes too, as on most of New York's streets of shops: 35% in Midtown's towers of offices, 50 to 80% elsewhere, none in the Hamptons. A Hamptons house is one household. Build 3682's city: 20,422 flats (7,259 above shops), 70 m2 net on average. Jobs are unchanged: an office a desk for every 16 m2 of its net floor, a shop by its sales floor and kind, a warehouse by its floor, a cinema 30 and a theatre 60, a special service by its kind's density, a residential building a member of staff for every 40 of the game's units: 72,663 jobs. Uptown's and Cedar Heights' buildings are measured the same way at each load; as Uptown builds, its homes add to the city.

## The people

Generated from the buildings with the city economy's seed (or a save's own), a flat at a time from a hash of the seed and its address: 5% of flats kept free, the household's class from its district's shares (the game's own unit leaned by its size), its size by its class (about 2.8 people a household: drawn at 3.2 for the working class, 2.8 for the middle and 2.5 for the upper, and no more people than the flat holds at 9 m2 each), its kind (couples with children, lone parents, three generations, flatmates, people alone), ages, schooling, work, tastes, savings, debt, cars, and whether it owns its flat. A generated tenant the market's rent would drive out holds a regulated tenancy, as about half of New York's renters do; they are still needed in the bigger city (without them some 600 households, 3%, leave in the first twelve days). EconomyCheck's figures for the base city: 19,418 households and 55,809 people (2.87 a household, 21% alone; 28% under 18 and 9% over 65), 24,496 residents in work and 46,608 commuters (64% of the jobs, against 86% before the bigger city).

Each day: birthdays move people through school, university, work and retirement at 65; couples form, have children and part; grown children leave home for a flat of their own; people die by their age. Each month: rents move with vacancy; households move when the rent is past what they bear or the flat gives a person less than 13 m2; households from the region take free flats while jobs are open; some leave after a long time out of work. Over ten years without the player the base city goes from 55,600 to 57,500 people and keeps 19,400 households, with about nine births and seven deaths a thousand a year, and unemployment of 6 to 7%.

## Jobs and money

Every job belongs to an employer: a business of the game's, the other firms of an office building, the public sector, a special service, a building's staff. Residents fill them by field and distance; commuters from the region take the rest, fewer while the city's own people are out of work (employers take those who live near first: at twice the start rate of unemployment the region fills a fifth of what it would). The player's businesses employ the game's own staff, each linked to a resident. Pay is the game's scale (12 to 18 an hour unskilled, more with skill and in the professions); a field's pay rises when its jobs stay open.

A household's day: pay, benefits and pensions in; income tax, rent, about twenty kinds of spending by its income against the city's middle and its tastes; the rest saved. Each household keeps a ledger of the running month and the last: income by source, taxes, rent and to whom, spending by kind, saving and debt. Spending goes into pools by district, class and kind (at home and near work, and the commuters' and visitors' own), routed to the districts in reach and shared among the businesses there by price against what the class bears, interior, satisfaction, attention and habit. Every cent is placed by whole-number shares, so a household's spending at a business can be worked out afterwards and, summed over households, is that business's takings from residents to the cent.

## The game's customers

At start the game's five customer calculators are given the mod's own subclasses (the game ships no Harmony, so no patch: the factory keeps its calculators in static fields). Each asks the game's own method for the hour with the initial customers scaled by the business's factor: its takings today over its takings on its first day, with the game's promotion since then taken back out (the population's attention carries it). The staffed furniture's cap, an office's workers on shift, the day and hour factors and the AI's draw stay the game's. On a business's first day nothing moves. AI businesses close by the game's own rule when people go elsewhere. The mod's copies of the arithmetic (the staff rota, the car dealership, the bank branches, a brand's site estimate, the Economy app's change in each business's customers) read the same scale; Meaningful Ambience's count reads the game's calculator, so it has it too. The business features' levers (issues, campaigns, brands, faults, Flare, the interior) no longer write a clamped promotion while the population runs: they are attention that draws people from rivals.

## Games

Each person from six has the platforms they play on (PC, console, phone, by age, class and sex: some three in five people play, most on phones) and the studio's twelve genres they like (by their main platform's share in the game studio's own table, leaned by age and sex). A child first drawn takes some of a parent's likes and plays more on what the home has; each birthday redraws a few bits for the new age. The day's figures count the residents of each genre on each platform, by district and class (`CityPopulation.Players`, `PlayersIn`, `PlayersOfClass`). The game studio's audience (`StudioBridges.Population`, wired in `Wiring.PopulationLinks`) is those residents times the world's people over the residents at the population's start, so its first day is the documented world's (`StudioBridges.World`) and it follows births, deaths, moves and the world's growth after; a guest's is the host's. In the base city 72% of people from six play: 28% on PCs, 30% on consoles, 51% on phones.

## What else reads it

The city economy's district incomes follow the households' mean incomes from where they stood; the Economy app's unemployment and vacancies are the measured market's; Uptown's and Cedar Heights' demand for homes, shops, offices and warehouses comes from the people (households wanting a flat, the region wanting in, takings growing, spending per shop floor, goods per warehouse floor). The council's taxes (income, sales at 8.9% of prices, property at 15% of rents) are an account the civic work will read (`CityPopulation.TaxesMonth`).

## The save and Going Public

One line, `reallife:population`: binary columns compressed in pieces on the save's workers by the district save's zip, version 2 telling each number against what the rest make it (ids against the one before, days against today, a rent against its flat's, pay against the going rate, the ledger in whole money against the household's day and shares), so the bigger city's line is 1.4 MB in a year of 60 days and 1.7 MB in one of 365, under the 2 MB budget; a line of version 1 is still read, its people's games drawn as it loads; a save without it generates the city; a load binds it to the city as it stands (a household whose building is not standing is kept, away, and looks for a flat). In a session the host owns the people and shares part P (the day, the books, each district's figures, each business's takings by address, the residents of each genre on each platform); a guest anchors each business on the first figures it hears and lives no population of its own.

## Tests

Options, Tests: Generate the city's people again; A month passes for the city's people (the population lives ahead of the clock and waits for the game's days); Show a household at random.

## Checks

EconomyCheck (`--population`): the measure (the owner's band of households and people, flats of about 70 m2, jobs unchanged), the generation (bands, the same seed the same city), a day with every cent placed and every household's part summing to each business's takings, ten years of life, the labour market, the costs a slice, the save's round trip, its size in a year of 365 days and a damaged line, the rebind, the Going Public part, the factor, the games (the studio's table, the first day the world's, the counts the people's as they change), every screen's view model.
