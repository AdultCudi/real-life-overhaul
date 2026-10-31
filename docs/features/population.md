# The city's population

## In a sentence

Under the living economy's master option **Living population**, every flat in the city's residential buildings is a home with a household of real people who age, study, work, pair up, have children, move and die; they earn, pay rent and tax and spend at the businesses within reach, and every business's customers (the player's, the city's and the rivals') follow them from the game's own count on the business's first day.

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
  PopulationAttention, PopulationBridges, PopulationText, PopulationGuide
```

## The measure

Each residential building has its floor times 0.8 over the game's own unit for its size in flats (a Hamptons villa is one). Build 3682's city has 8,658 flats; the design guessed 11,000 to 14,000 before the measure, and the game's units are large (132 m2 gross on average). Jobs: an office a desk for every 16 m2 of its net floor, a shop by its sales floor and kind, a warehouse by its floor, a cinema 30 and a theatre 60, a special service by its kind's density, a residential building a member of staff for every 40 flats: 72,663 jobs. A retail building's floors above its shop have no use (the game lets the shop alone). Uptown's and Cedar Heights' buildings are measured the same way at each load.

## The people

Generated from the buildings with the city economy's seed (or a save's own), a flat at a time from a hash of the seed and its address: 5% of flats kept free, the household's class from its district's shares leaned by the flat's size, its size and kind from Manhattan's (two people a household, nearly half alone), ages, schooling, work, tastes, savings, debt, cars, and whether it owns its flat. A generated tenant the market's rent would drive out holds a regulated tenancy, as about half of New York's renters do. EconomyCheck's figures for the base city: 8,228 households, 16,038 people.

Each day: birthdays move people through school, university, work and retirement at 65; couples form, have children and part; grown children leave home for a flat of their own; people die by their age. Each month: rents move with vacancy; households move when the rent is past what they bear or the flat too small; households from the region take free flats while jobs are open; some leave after a long time out of work. Over ten years without the player the base city stays at 16,000 to 17,500 people, with about nine births and ten deaths a thousand a year.

## Jobs and money

Every job belongs to an employer: a business of the game's, the other firms of an office building, the public sector, a special service, a building's staff. Residents fill them by field and distance; commuters from the region take the rest. The player's businesses employ the game's own staff, each linked to a resident. Pay is the game's scale (12 to 18 an hour unskilled, more with skill and in the professions); a field's pay rises when its jobs stay open.

A household's day: pay, benefits and pensions in; income tax, rent, about twenty kinds of spending by its income against the city's middle and its tastes; the rest saved. Each household keeps a ledger of the running month and the last: income by source, taxes, rent and to whom, spending by kind, saving and debt. Spending goes into pools by district, class and kind (at home and near work, and the commuters' and visitors' own), routed to the districts in reach and shared among the businesses there by price against what the class bears, interior, satisfaction, attention and habit. Every cent is placed by whole-number shares, so a household's spending at a business can be worked out afterwards and, summed over households, is that business's takings from residents to the cent.

## The game's customers

At start the game's five customer calculators are given the mod's own subclasses (the game ships no Harmony, so no patch: the factory keeps its calculators in static fields). Each asks the game's own method for the hour with the initial customers scaled by the business's factor: its takings today over its takings on its first day, with the game's promotion since then taken back out (the population's attention carries it). The staffed furniture's cap, an office's workers on shift, the day and hour factors and the AI's draw stay the game's. On a business's first day nothing moves. AI businesses close by the game's own rule when people go elsewhere. The mod's copies of the arithmetic (the staff rota, the car dealership, the bank branches, a brand's site estimate, the Economy app's change in each business's customers) read the same scale; Meaningful Ambience's count reads the game's calculator, so it has it too. The business features' levers (issues, campaigns, brands, faults, Flare, the interior) no longer write a clamped promotion while the population runs: they are attention that draws people from rivals.

## What else reads it

The city economy's district incomes follow the households' mean incomes from where they stood; the Economy app's unemployment and vacancies are the measured market's; Uptown's and Cedar Heights' demand for homes, shops, offices and warehouses comes from the people (households wanting a flat, the region wanting in, takings growing, spending per shop floor, goods per warehouse floor). The council's taxes (income, sales at 8.9% of prices, property at 15% of rents) are an account the civic work will read (`CityPopulation.TaxesMonth`).

## The save and Going Public

One line, `reallife:population`: binary columns compressed in pieces on the save's workers by the district save's zip; a save without it generates the city; a load binds it to the city as it stands (a household whose building is not standing is kept, away, and looks for a flat). In a session the host owns the people and shares part P (the day, the books, each district's figures, each business's takings by address); a guest anchors each business on the first figures it hears and lives no population of its own.

## Tests

Options, Tests: Generate the city's people again; A month passes for the city's people (the population lives ahead of the clock and waits for the game's days); Show a household at random.

## Checks

EconomyCheck (`--population`): the measure, the generation (bands, the same seed the same city), a day with every cent placed and every household's part summing to each business's takings, ten years of life, the labour market, the costs a slice, the save's round trip and a damaged line, the rebind, the Going Public part, the factor, every screen's view model.
