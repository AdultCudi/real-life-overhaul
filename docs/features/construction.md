# Construction

## In a sentence

Buildings going up in the city as the game itself would show them: a whole building wrapped to its roof in the game's own scaffolding and fabric while it is rebuilt, and on ground given to it a new building's hoarding, dig, slab, tower crane, frame storeys, cladding and wrap, stage by stage over game days.

## Where the code is

- Folder: [`Scripts/Civic`](../../Assets/Mods/RealLifeOverhaul/Scripts/Civic/), as two kinds of site of the construction programme ([Civic life](civic.md#construction-sites)): `SiteKind.Rebuild` and `SiteKind.Plot`.
- Read first: `WrapRules.cs` and `PlotRules.cs` (game-free), then `SiteWrap.cs`.

```
WrapRules            the game's scaffold round a building: its shell's sides, runs clear of doors and of what stands in the way,
                     the 5.17 m pitch to the roof, fabric from the second lift up, the stages, the pace (game-free; NavCheck WrapCheck)
BuildingShell        a building's solid boxes from its own colliders, in its own axes (no physics query; a faded building reads the same)
SiteKit              the game's own scaffold modules and fabric copied from a building that wears them (unbatched, scripts taken
                     off), and its concrete, metal and fence wire by name: once a city, behind the loading screen
SiteWrap             one wrap: laid out, set out and taken down a few pieces a frame out of view, every renderer in the building's
                     fade, hide and city map lists, a holder whose game extension names the building
PlotRules            a new building on given ground: its record ("plot:24x20:090:5" in the site's street field), the frame, the
                     slab, the cladding, the yard's crane, cabin, toilet and stacks, the fence and the gate (game-free)
PlotMeshes           a stage's boxes as one mesh in two materials, and the fence; CraneSlew: the jib's legacy Animation clip
SiteDresser.Rebuild  the rebuild's hoarding, wrap stage and crew; SiteDresser.Plot: a plot's stages and the camera hiding its frame
SiteDresser.Wraps    what is in view for a site that fills a building, the wraps stepped each frame while they change, the tests' wrap and crane
CivicDirector.Construction   the five test buttons
```

## What players see

**A rebuild** (one at a time in the city, about 160 working hours, drawn by the programme like any site; never the player's own building, never one of the mod's own places, none a DLC keeps):

1. Hoarding along the door's wall, the notice board, two people at it.
2. The game's scaffold going up every street side, half its lifts, no fabric; one of the crew on the first deck.
3. Wrapped to the roof, the fabric hung from the second lift up; people on the deck at work and a painter; the skip at the kerb.
4. The inspection: an inspector with a clipboard at the notice board, one person on the deck.
5. The scaffold coming down to half, no fabric; then the site is handed over and everything goes once nobody is looking.

At the handover the building wears a new look of the same size, a design of the city's own stood over it by the copy engine (`BuildingCopies.PlaceOver`, its own look hidden), out of view; the works state keeps the look (`SiteLook`, at most 96) and it stands again behind the loading screen at every load (`SiteLooks`).

**A new plot** (on ground given to it; about 320 working hours):

1. Hoarding along the front with a 6 m gate at the yard, the fence wire round the rest, the site cabin and a toilet in the yard.
2. The dig: the frame's footprint broken up, spoil heaped round it; two people digging.
3. The ground slab with its starter bars, the tower crane on its pad in the yard with its jib slewing, material stacks.
4. The frame rising a storey at a time (3.6 m a storey, columns on a grid of spans up to 6 m, a slab each, edge protection round the top).
5. Every storey but the top clad in metal panels, and the frame wrapped in the game's scaffolding, fabric on.

At the handover the frame comes down out of view and the finished building stands on the plot, a design of the city's own fitted to the frame's footprint (`BuildingCopies.PlaceOnPlot`), kept and stood again at every load as a rebuild's is, until the lot holds a building of the district's own. Whatever of a plot stands between the camera and the player is hidden the way the game hides its own buildings (their shadows kept).

**A narrow plot** (10 to 20 m of land along the street, 20 m deep at least: a small shop's or home's lot) keeps a metre each side and its yard behind the frame, goes up to six storeys without a tower crane, and has its gate at the front's end. Plots on the lots of a district may stand side by side: a plot refuses only a site on its own ground and 2 m round it.

## The game's scaffolding

- **What it is:** the Buildings scene's `SM_Scaffolding_8m` (6.0 m), `_4m` and `_Corner_4m` (3.0 m) modules, 3.04 m deep, and `SM_Scaffolding_LargeFabric` and `_SmallFabric`, stacked at 5.17 m on 26 of the game's buildings.
- **Layout:** one run along each street side (pavement under the whole depth, no other building in it), a corner module at each end (the far one turned a quarter, as the game's), 6 m modules between and one 3 m where the count is odd, slid along the slack until no post stands within 0.95 m of a door's middle, else split round the door. A tree, a sign or a lamp over a stretch splits the run; a bin or hydrant where a post would stand keeps the posts off it. Lifts: the top deck within half a lift of 0.6 m under the roof, at most ten, at most 360 pieces.
- **Fade:** the pieces' renderers join the building's `renderersToFade` and `renderersToHide` and its private set the city map swaps for its box (`_lodRenderers`), and leave them before a piece is destroyed; the pieces hang under a holder whose `ViewBlockingEntityExtension` names the building (set before the holder wakes), so the camera's ray meeting the scaffold fades the building with it, as the game's own scaffold does. Two lines of `Core/GameContract.cs` (part `site_wrap_fade`).
- **Pace:** nothing is set out or taken down while the wrap's box is in the camera's view (the test buttons excepted), never while the city map shows, six pieces a frame (twelve to take down, sixty behind the game's black screen).
- **Pieces:** copies of a building's own scaffold made once a city; where every building's is static-batched they are made from the game's meshes and materials found by name instead.

## Props and clips

The building sites' props are in a second props bundle, `Bundles/reallife_props_site` (PropAssets/README.md, "The second bundle"): `tower_crane_mast`, `tower_crane_jib` (turning about its pivot, the slewing ring, by Unity's Animation), `site_cabin`, `site_toilet`, `site_stack_blocks`, `site_stack_timber`, `site_stack_rebar`, `site_earthworks` and the `hammer`. Each is drawn as a plain stand-in of the same size (`CivicProps.CivicStandIns`) while the bundle is not installed. The clips `hammer_nail` and `rake_asphalt` (CityAssets, `clips_build.py`) are played when the anim bundle has them, `kneel_work` and `shovel_dig` otherwise.

## Going Public

The new kinds are values of the site's existing kind field, and a plot's ground is text in its existing street field: the save's line and the host's line of sites keep their shape and version, and nothing new is sent. The host draws the programme as before (seeded, the same on every machine); a guest follows it. The wrap and the plot's meshes are drawn by each machine from the same site records. The test buttons that change sites are the host's.

## Self-check lines (Developer diagnostics)

- `Construction check (wrap): N modules, N lifts, linked to the building's fade and its city map look: PASS` when a wrap has every piece it should.
- `Construction check (wrap down): every piece taken down, and the building's fade lists hold none of them: PASS` when it is gone.
- `Construction check (crane): mast ... slewing by the engine's own animation (100 s a round): PASS` for the test crane.
- `Construction: the game's scaffold pieces copied from the game's own scaffolding; concrete ..., metal ..., fence wire ...` once a city.

## Tests (Options, the civic section's tests)

- **Construction: wrap the nearest building in the game's scaffolding**: the nearest building with a door within 70 m, to its roof, fabric on, a few pieces a frame in view.
- **Construction: a rebuild site at the nearest building**: a rebuild site set out now.
- **Construction: advance the nearest site one stage**: the nearest site within 80 m moves to its next stage (a plot in its frame stage: a storey); at the last, it is handed over.
- **Construction: raise a frame storey here**: a storey more on the nearest plot within 60 m; with none, a plot on 24 by 20 m of ground ahead of the player (no building on it), at its first storey.
- **Construction: stand the tower crane here**: the crane alone, 12 m ahead, slewing.

## The construction game

### In a sentence

A construction company of the player's own: an office opened as a Construction Company and run from the Construction app; land bought in the new district or a building of the player's rebuilt; plans, the requirements and an engineer's sign off; planning permission at City Hall with the neighbours' objections and an appeal; a site that goes up in the city's construction programme at the crews' pace, through four stages and three inspections; materials, plant, crews, money; a handover, an opening and what the building is for afterwards; and the city's builders, who bid against the player and put up the district's buildings on the lots nobody bought.

### Where the code is

- Folder: [`Scripts/Construction`](../../Assets/Mods/RealLifeOverhaul/Scripts/Construction/) (namespace `RealLifeOverhaul.Construction`); its README lists every file.
- Read first: `BuildCatalogue.cs` (every number and name), then the game-free rules (`LandRules`, `PlansRules`, `PlanningRules`, `SiteRules`, `MoneyRules`, `TenderRules`, `FirmRules`, `DevelopmentRules`), `BuildState.cs` and `BuildStore.cs`, then `ConstructionDirector.cs`.
- Where it meets the rest of the mod: `ConstructionBridges.cs`, filled in by `Wiring/Wiring.ConstructionLinks.cs`; the city's programme asks the builders through Civic's `SiteBuilders.cs`, and keeps the finished looks with `SiteLooks.cs`.

```
BuildCatalogue        the uses, stages, inspections, materials, plant, trades, roles, specialities, cover and every figure (game-free)
LandRules             a lot's price from the game's own value of a building, zoning, the council's day, land tax, flats, the frame (game-free)
PlansRules            storeys, hours, stages, loads, crews and plant, fire exits, parking, the height limit, the engineer, the estimate
PlanningRules         working days, objections, the council's decision, conditions, the appeal, the licence (game-free)
SiteRules             a crew's pace, materials, the work's quality, an inspection, deliveries, a bribe, theft, morale and strikes
MoneyRules            the construction loan's limit, rate, draws, interest and instalments, investors, a sale's share (game-free)
TenderRules           the works' value, the rivals' bids and the player's, the score and the winner (game-free)
FirmRules             reputation, a speciality, cover, a week's crews to hire, a rival's offer, plant's resale (game-free)
DevelopmentRules      the city's developers: whose lots, in what order, what use and builder, which design, on which days (game-free)
ShowFlatRules         a show flat's viewings, a buyer's chance, the price against the plain off plan price, its costs, which blocks (game-free)
RefitRules            a refit's hours, the fittings' grade, a contractor's quote from the eight builders' bids (game-free)
CommissionRules       a builder hired by the player: the week's eight quotes, the contract's deposit, stages, retention and
                      penalty, the settling up, the builder's pace and the quality the inspector sees (game-free)
BuildState, BuildStore   the company, its lots, crews, plant, staff, projects, tenders, the rivals and the developers' lots, in one line
ConstructionDirector  the state and its timing: a second's look, the hour's and the day's passes (partial files below)
  .Asks               every bridge asked, its fault said once and its default answered
  .Projects           a project from its plans to the handover and the district
  .Sites              what the programme asks of a builder's site, the inspections, the materials and plant, a site manager
  .Day                the hour and the day: crews, plant, money, papers, risks, the opening
  .Market             the developers, the tenders and the award, the rivals
  .Actions            what the player does: found, the licence, land, crews, plant, staff, money, a visit, the ribbon
  .App                the app on the phone, its notices, a site's notice board, the ribbon's panel at the place
  .Tests              the options page's test entries
  .ShowFlats          a block's show flat: its design, floor, viewings, sales and days
  .Refits             a business's refit by the company or a contractor, its closure, its handover, businesses' refit tenders
  .Hire               a builder of the city's hired for the player's building: the quotes, the contract, its stages and
                      inspections, the stage payments and the settling up at the handover
  .HireTests          the hired builder's tests, each a public static method
  .Desk               the front desks: the building the player is in, its desk kept up, a click, what the desk talks about
  .DeskTests          the front desks' tests, each a public static method
  .Screens            the viewing panel on the show flat's floor, the way there, a renovation from the Interior tab
  .SalesTests         the show flats' and renovations' tests, each a public static method
ShowFlatPlace         the show flat's place on a floor (the floors core), on its design's apartment structure
ConstructionGame      the one file that reads or changes the game: clock, prices, money, the player's offices, buildings and staff
ConstructionType      the Construction Company business type (ModdingAPI.RegisterModBusinessType), furnished as the game's design studio
ConstructionApp (.*)  the app in the game's own frame (GameUi.AppShell): Overview, Land, Projects, Tenders, Crews, Company, Rivals;
                      .ShowFlat a block's show flat on its money page, .Refit the businesses and their refits
PlanningCall          City Hall's planning and building control pages, its call's sixth topic and the same pages from the app
FrontDeskRules        what a front desk talks about first, the front desk by the door, the office manager's counts (game-free)
BuilderDesk           the front desk's conversation in the game's dialogue panel: the quote, the hire, the builder, the app
BuilderDeskGame       the front desk on the game's side: the office, its staff by the door, the click, the walk up, the time held
BuilderDeskPointer    the person at the front desk as a thing to point at, with the game's own card
ConstructionGuide     the feature list's entry, its facts, the options and the test entries
```

### What players see: the loop

1. **The company.** An office opened as a <b>Construction Company</b> in BizMan (an office business type of the mod's, its staff the game's own designers and client staff at the office's desks). In the **Construction** app: Found the company.
2. **The licence.** The contractor's licence at City Hall: a fee, a decision two days on, refused while a conviction counts; a year, renewed in its last days.
3. **Land.** The Land tab lists the new district's open lots for sale, priced at 68% of the value of the building a lot of its class usually holds, at the district's price a square metre, its type's multiplier and the lot's own traffic, as the game values a building (`Building.GetMarketPricePerSqm`). Each lot's class is zoned for some uses; the city's council can add one (asked at City Hall, heard at its Thursday planning sitting or at the player's own meeting), or allow a use on every lot of a class. Land tax each week, more on land kept empty. Or a building of the player's own: **Plan a rebuild**.
4. **Plans.** The use (one the lot is zoned for), a design of the city's own that fits the frame's footprint (the copy engine's measured designs, of a size the lot's class takes), the architect (a practice for a fee, an architect of the staff, or the player once qualified), parking under the building or none. The plans write in the fire exits; the parking asked, the district's height limit (12 storeys in the new district, 8 under strict permits: the elected government's building code) and an engineer's sign off (from three storeys, and for every rebuild) are requirements. An estimator of the staff narrows the estimate's error.
5. **Planning permission** at City Hall (see below for why City Hall's call): the fee, the neighbours' objections in the City Ledger, the council's decision in six to fourteen working days, conditions with many objections, a refusal with its reasons; an appeal to the court (a lawyer of the player's own helps) or the plans drawn again.
6. **The site.** With the licence, permission and a crew: the site starts in the city's programme, a plot on the lot's ground or the game's scaffolding round the building. It works at the crews' pace (their trades against the stage's need, skill, morale, the plant, the speciality), as far as the stage's materials allow.
7. **Materials, plant, crews.** Each stage's loads ordered from one of three builders' merchants, priced off the raw materials exchange's building materials (their move against the city's price level, `Markets.MarketDirector.MaterialMove`), delivered at 07:00 on a working day, sometimes short; what a project will still buy can be hedged with futures (**Materials and hedge**). Plant hired a day, or bought and kept at a yard (a warehouse of the player's); a tower crane from three storeys. Crews hired from the week's labour market, paid each working day, their morale up and down, strikes, walking off, a rival's offer to match.
8. **Inspections.** Three visits (after the foundations, after the frame, at the end), booked, the inspector at 10:00 on the next working day; a failure means rework; a bribe waves it through and may come out for 21 days (a case of bribery, the company's reputation and Standing). A site manager of the staff books, orders, gets the plant and puts a free crew on.
9. **Money.** A construction loan at the business loan's rate and 1.5 points, drawn in stages and on the credit file; an investor's money for a share more; a block's flats sold before they are built (a deposit now, the rest at completion). Theft, storms and faults, and insurance's share.
10. **The handover.** The site is handed over: the building's new look stands out of view (below), a plot's building is handed to the district (from the next load one of its buildings, the player's own or its residents'), the City Ledger has it, and the next day is its opening.
11. **The opening.** Between 10:00 and 18:00 of the opening day, at the building's front, the game's own panel offers **Cut the ribbon** (`GameUi.DoorPanel`): a moment on Flare, a story, a lift in the company's reputation and Standing. Unattended, it opens by itself at 18:00.
12. **Afterwards.** A plot's building is the player's from the next load (used, let or sold through BizMan: the district writes the game's own deed), or a block's flats are sold one by one at the asking price chosen, the building its residents'. A rebuild's building carries on with its new look.

**Without a company, or instead of the company's crews: hire a builder** ([below](#hiring-a-builder)). Land, plans and planning permission (steps 3 to 5) need no company; only the company's own crews need it and its licence.

Alongside: **Tenders** (the city's own works as its sites start, a business's rebuild every six to ten days, a public building on an office lot every fifteen to twenty-five), bid at 85% to 130% of the value, usual or fast, the best mix of price, time and reputation winning; the **Rivals** tab (the city's eight contractors, Civic's names, each known for a kind of work, with its office in Uptown); the **Company** tab (office, licence, reputation, speciality, insurance, yard, record, books).

**The rivals' offices.** Each of the eight rival builders has an office in Uptown, in one of the district's eight leased office lots (Uptown's `BuildingsRules.Leased`, lease `i` rival `i`'s; [Uptown](uptown.md), "The leased offices"). The wiring leases them (`Wiring.ConstructionLinks.RivalLease`, setting `DistrictBuildings.Lessee` whatever any option says, so every machine leases the same): a business of the construction company's type (`ConstructionType.TypeName`), set up as the game's graphic design studios are (`ConstructionType.Template`: their layouts, hours and sign), under the rival's name, with BizMan's description (`BuildCatalogue.OfficeAboutKey`) and valued by its crews (`BuildCatalogue.OfficeDayPerCrew`, $1,200 a crew a day). So the map's row for the Construction Company type outlines the eight, BizMan presents each as a business of the game's (held off the market: no rent, sale or takeover), and a player walking in finds a studio's furniture and staff. The **Rivals** tab gives each one's address (`ConstructionBridges.RivalOffice`) and its button makes it the game's destination (`ConstructionBridges.RivalOfficeGo`); with Uptown not standing at a load, a rival has no office then and the tab says so. Nothing of it is in the construction game's line: the offices' registrations are Uptown's, kept by the district save.

**Why City Hall's call for planning:** City Hall is already a contact with topics in the game's own conversation panel (Civic's `CityHallDialog`), the licence and zoning belong with it, and a call needs nothing in the street; a desk on a floor would need a building held for City Hall, which the floors core has not got. The app opens the same pages (`PlanningCall.Open`), on the project's application when it can be made.

### With the city's construction programme

Every site the company and the developers start is one of the programme's (`CivicWorks.StartPlot` on given ground, `StartAt` for a rebuild), so the street shows it as it shows the city's own; the programme asks the builders through `SiteBuilders`:

```
Hours     a working hour's work: the crews' pace, or none while the materials or an inspection hold it (a tender's site waits)
Owns      a builder's site is not one of the city's six; Shown: put away while the construction game is off
Name      the notice board names the player's company; Notice: the company's own page at its board (walk, book)
Started   a city site may go to tender; Handed: the handover, every site; Design, Tag: the finished look and the lot
Taken     a plot's lot holds a building of the district now: its look is given up
CopyOver, CopyOnPlot, CopyRemove, CopyGone   the copy engine for the finished looks
PlanningTopic   City Hall's sixth topic
```

The programme holds 16 sites at once (`CivicCatalogue.SitesMost`, the save's and the host's line): the city's own six, the company's four at most and the developers' five at most.

### With the copy engine

- A rebuild's new look: `BuildingCopies.PlaceOver(building, design, null, "construction", hideOwn: true)`, a design of the building's own size that fits its footprint and 1.5 m round it (`BuildingCopies.Footprint`).
- A plot's building: `BuildingCopies.PlaceOnPlot(design, null, middle, yaw, frameWidth, frameDepth, "construction")`, the design picked for the frame's footprint (`LandRules.Frame`, which the checks hold to `PlotRules.Frame`).
- The designs that fit some ground: `BuildingCopies.Fitting(width, depth, type, most, into)`, the best covering first, never one drawn in a static batch or of a DLC; asking starts the engine's measuring in play.
- Each look stands out of view in play, and behind the loading screen at a load (`ScenePrewarm.AddLate`, before the copy engine's own late step).

### With the new district

The district's lots and buildings are Uptown's ([Uptown](uptown.md)), wired in `Wiring/Wiring.ConstructionLinks.cs`:

```
Lots           every lot of UptownLots.All as a LotView: its id, street and number, middle (at UptownLots.GroundY), turn, size and
               class (LotClass's number: small, office, big, warehouse), Open while UptownLots.Open(lot), and its traffic
               (DistrictBuildings.TrafficOf)
LotsStamp      UptownLots.Stamp and DistrictBuildings.Stamp together: the lots are read again only when either changes
DistrictLives  DistrictBuildings.Lives
LotTaken       DistrictBuildings.Taken: a building stands there, or one finished waits for the next load (not for sale)
LotBuilt       DistrictBuildings.Finished: a building of the district stands there now (no look of the programme's there)
SizeTaken      the sizes the lot's class takes (Lot.Sizes): a design of another size is never offered for the lot
LandPrice      DistrictBuildings.LandPricePerSqm (the district's base price times its multiplier)
Finish         DistrictBuildings.Finish(lot, design, size, version, type, 0, FinishOwner.Player or City, price, out why)
```

The company passes 0 storeys (the design's own floor area) and, for a building of the player's own, the land's price and the build's cost as its price; `FinishOwner.Player` only outside a Going Public session (the district refuses it in one), and the residents' (the city's) when a block's flats are sold. A lot the developers or the player have built on is the district's from the next load, and the finished building's look of the programme's is given up there.

### With the building trades

Agreed with the trades ([Building trades](trades.md), "With the construction company"); wired in `Wiring/Wiring.ConstructionLinks.cs`:

- `ConstructionBridges.Qualified = TradesBridges.Qualified`: "architect" before the player may design a project, "engineer" for a sign off of their own.
- `ConstructionBridges.Commission`: **Draw the plans** with the player as the architect puts the project's brief at the architects' practice, `TradesBridges.Commission("build:<n>", storeys, flats, budget)` (the flats a block's floor area holds, the estimate as the budget); the plans then wait on the practice.
- `ConstructionBridges.DesignOf`: each day the company asks `TradesBridges.DesignOf("build:<n>", out DesignScore)`. Once the player has finished the design there, the plans are drawn: their quality is 30 and 70% of the design's `Total`; its `CostPermille` (bounded 600 to 1600) scales the estimate's materials, crews and contingency and every stage's loads (a dearer design costs more to build); for a block of homes its `Units` are the block's flats; its `Storeys` are written to Player.log beside the project's (the building's look stays the city's design it wears). `Construction check (own design)` says what was read.
- The roles the company gives its office's employees (architect, engineer, estimator, site manager) and its crews' trades (labourer, mason, crane operator, electrician, plumber) are `TradeCatalogue.Roles`' ids; the offline checks hold each to it.

### With the stock market

Wired in `Wiring/Wiring.ConstructionLinks.cs`, the bodies in `Wiring/Wiring.MarketsBuildLinks.cs` ([The city's builders and hedging a project](economy-markets.md#the-citys-builders-and-hedging-a-project-buildermarket-hedgerules)):

```
MaterialFactor   a material's move on the exchange against its own price at the price level (MarketDirector.MaterialMove):
                 cement, steel, bricks and timber the exchange's own building materials, glass the game's
RivalNews        a rival's tender won (or the player's), its works handed over on time or late against the days it bid, a
                 development finished, a tender ring found out (each member), the building of the year's builder: the market's
                 builder news list (Markets/BuilderMarket), by its kinds' numbers
HedgePlan, Hedge   a project's purchases to come (ExposureRules) as futures, planned or bought (MarketDirector.HedgePlan, Hedge)
Contract         a hedge's future on the exchange's book; SettleTest, UndoTests: the test tools'
```

A project's **Materials and hedge** page (`ConstructionApp.Hedge.cs`) shows what it will still buy of each material, from which day, and the futures that would hedge it; **Hedge** buys them; its futures are looked at each day (`HedgesDay`) and at the handover a notice says what the hedge saved or cost (`HedgeEnd`). The company's takings and costs are kept by day (`BuildState.TakeIn`, `PayOut`, `Books`) for the player's listed company's books.

### Hiring a builder

The owner's ask (24 Oct 2026): build anything without a company, and hire the rivals. The player is the client (the developer); one of the eight rival builders builds.

1. **Without a company or a licence:** buy a lot (Land) or pick a building of the player's (**Plan a rebuild**), have the plans drawn (a practice, or the player as architect where the trades allow), and get planning permission at City Hall, all as before. A project at **Ready to build** that is a plot or a rebuild (`ConstructionDirector.HireKind`) shows **Hire a builder** whether or not there is a company; with a company, **Start building** (the company's crews) stands beside it. The player's own company never quotes: it builds with its crews or not at all.
2. **The quotes** (`ConstructionApp.Hire.cs`, the project's third page): each of the eight builders bids as on a tender (`TenderRules.RivalBid` through `CommissionRules.Bids`, the loop the refit's contractor quotes through too), the works valued at the plans' estimate with a client's margin (`TenderRules.BuildingValue`), the usual days from the site's hours, homes or offices the speciality for a plot and renovation for a rebuild, and a builder busy with the city's works asking more. Drawn from the save, the project and the week (`CommissionRules.Salt`), they stand to the week's last day. Each row gives the builder, its price and its working days; the one picked shows its standing, what it is known for, the deposit, the three stage payments, the retention and the penalty. Pick one, **The cheapest** or **The fastest**, then **Hire this builder** (the game's question box).
3. **The contract** (`BuildCatalogue.Hire*`): a deposit of 10% as it is signed (`ConstructionGame.Take`: refused without the cash), 25%, 30% and 30% as the foundations, frame and final inspections pass (`Force`, as a contract's payments run on), and the last 5% kept back to the handover. Late, the builder gives back 0.5% of the price a day, 10% at most, out of the retention and then in cash (`CommissionRules.Settle`). Every payment is the game's own transaction (`reallife_transaction_bld_builder`), counted in the project's spending and so in the building's price for the district.
4. **The works:** the site is set out at once in the city's programme by the company's own starter (`StartSite`: a plot's frame or a rebuild's scaffolding, the builder named on its notice board). The builder's site goes through the company's own working hour (`Work`): the same four stages and three inspections, each stage held until the inspector passes it, at the builder's pace (`CommissionRules.Pace`: as fast as its quote needs, then its own luck, less of it for a builder of standing), with its own crews, materials and plant. The builder books the inspector for the next working day itself (`HiredBooks`); the verdict weighs its standing (`CommissionRules.Quality`); a failure is its rework, at its own cost. A storm can still set it back. Its due day is its quoted days and two for the inspections' waits, as the company's own.
5. **The handover** is the company's own (`Handover`): the retention settled, the builder's record (built, its standing up on time and down late, the stock market's builder news outside a Going Public session), the critic's opinion and the building of the year with the builder named, the opening day and the ribbon, a plot's building handed to the district (the player's from the next load, used, let or sold, or its flats sold one by one), a rebuild's new look and refit. None of it is the company's: not its record, reputation, insurance or a fault's claim. A construction loan, an investor and flats sold off plan work as for the company's own builds.
6. **The app without a company:** the Overview says how to build without one and opens Land and Rivals; Land, Projects, the quotes and Rivals all work; Tenders and Crews say how to found the company. The Rivals tab counts what each builder has built for the player.

Going Public: the quotes are drawn from the save, the project and the week alone; the site is the player's own (a rebuild only of a building they own, under the programme's own-id rule; a guest starts no plot, as before); nothing is sent; the builder's news reaches the stock market only outside a session. A save with a contract read by an older RLO passes its new fields over; without the mod the line is kept as it was.

### The front desk

The owner's ask (24 Oct 2026): "make the construction buildings interactive, similar to how interior designers or basically every other native building is. You enter, you will be able to interact with the person."

**How the game does it** (decompiled `BigAmbitions`): a special place's person is an item of its layout, a `SpecialEmployeeController` (`SpecialEmployeeController.cs`; a `BusinessEmployeeController`, `BusinessEmployeeController.cs`), whose `customValue` names its kind (`SpecialEmployeeType`: Bank, RecruitmentAgency, MarketingAgency, InstallationFirm and the rest, lines 12 to 26). Its `Start` makes the person (`Characters/HumanDefinitionLow`), seats them on the desk's chair and plays the at the computer animation every 5 to 15 s (lines 40 to 81, `BusinessEmployeeController.cs` 64 to 92). Pointing at it, the game's call to action is `click_to_talk` (`ManageCtaBehavior.GetCta`, lines 159 to 170: the player walks up with `SetGoal`, then `Interact`), with the person's name over it (`EmployeeNameOverlay`). `Interact` seats the player on the customer's chair, pauses the game a moment later and opens the game's dialogue panel facing the person (`dialogUI.ShowDialog(GetDialogType(), NavigationBlocker.SpecialEmployeeDialog, contact, UnpauseGame, employeeTpc)`, line 91), its pages `DialogEntry`s of three buttons (confirm, a second option, cancel: `Entities/DialogEntry.cs`). The dialogue is the building's `SpecialService.dialogType` (`BuildingRegistration.GetCallDialogType`, line 326), so the game's own desk opens the furniture store's dialogue anywhere without a special service: it cannot be put into a builder's office as it is. A click goes through the game's list of calls to action (`CtaManager.CtaBehaviors`, asked in order; `EntityController` runs the first one's action on a click, lines 237 to 247).

**The mod's way, the same in play:**

1. **Where:** a rival builder's office in Uptown while the district and that office stand at this load (`DistrictBuildings.LeaseHere`: the player inside that lease's building, `LifeGame.Visitable`: still the builder's business, never a unit the player rents or owns nor another player's, `BuildingsRules.Visitable`), and an office of the player's own construction company (`GoingPublic.Mine`). Nowhere else.
2. **Who:** the office is laid out as the game's design studio and staffed by the game (`BuildingManager.SetupAiEmployeeStations`); its front desk is the workstation somebody of the game's sits at nearest the door the game lets the player in by (`FrontDeskRules.Front`, the primary `ExitZone`'s spawn point, as `GetFirstExitZone` picks it on foot), never one the player works or whose person is away. The person is the game's own, seated and at work as the game sets them. In the player's own office that is one of their own staff, so the office manager is there while somebody works the front desk.
3. **The prompt:** a trigger box on the game's interactive layer round the person, from the desk's top to their head (so a click on the desk is still the game's own, and nothing of the office's business changes), the desk and the person outlined as the game outlines its items, the role over the game's own `click_to_talk` (`BuilderDeskPointer`; an entry at the front of the game's list of calls to action that answers only for it, `BuilderDeskGame`). It stands aside while furniture is placed or the interior designer is open.
4. **The talk:** the player walks up in front of the person (`SetGoal`, a place on the game's walking mesh on the player's side), and the game's dialogue panel opens facing them, the game paused as the game's desk staff pause it (never in a Going Public session, whose clock is everyone's) and running again as it closes (`BuilderDesk`).
   - **Get a quote for my project**: the builder's quote this week for the player's building at Ready to build, the very one the app's quotes page shows (`ConstructionDirector.DeskQuote` through `Quotes`, never priced a second way): its price, working days, deposit and the day it stands to, or why it cannot be hired yet (`HireBlock`).
   - **Hire you for my project**: the dialogue closes and the game's question box asks with the app's own words; yes signs through the app's own `HireBuilder`, deposit and all.
   - **What do you build?**: its speciality, standing, crews, tenders won, buildings finished and how many were the player's.
   - **Open the Construction app**: on the project's quotes, the builder's row of Rivals, or the project.
   - **Goodbye** on every page.
   - With nothing ready (`FrontDeskRules.Step`), the desk says what to do first: buy land (and **Open the Land tab**), have the plans drawn, apply for planning permission, wait for the council's decision, or that the builder is already at work on the player's building.
5. **The player's own office:** the office manager gives the projects under way, those being built and the crews, and opens the company's pages: **The overview**, then **Projects or crews** (**The projects**, **The crews**).
6. **Cost:** once a second, one look at the building the player is in; the building's items looked through only when they come in (a second apart for eight seconds while the game seats its staff) and every fifteen seconds while nobody sits at the front desk. Nothing per frame, no scene search, nothing saved.

**Why not the game's own desk item:** inserting a `SpecialEmployeeController` into the studio's layouts would open the furniture store's dialogue (the office has no special service), the layouts are shared with the player's own company (whose office would get the desk as a piece of furniture), and giving the office a special service of its own would change what the game makes of it (its lists, its contact). The game's own staff at the front workstation, its own call to talk and its own panel give the same thing without touching the business.

Going Public: everything is on the player's machine for their own project; the quote and the contract are the app's (above); the time is never paused in a session; nothing is sent.

### Show flats

A block of homes sold off plan (a plot of homes at its Ready or Building stage, not handed to the district, with flats left) can open a **show flat** on its money page. One at a time.

- **Its design.** The show flats' designs are the Designs feature's (`Designs/ShowFlatDesigns.cs`, `DesignsDirector.ShowFlats.cs`): made from the game's own residential designs (`StreamingAssets/InteriorDesigns/Residential/<structure>`, read by the firm's own `InteriorInstallationFirmHelper`), each **fully dressed** (the game's design whole) or **plain** (cut to `PlainKeeps`: beds, seats, tables, the kitchen, storage and the ceiling lamps, none of the art, plants, rugs, lamps or screens). Only the apartment structures (never a Hamptons house or the car park), and of those the one whose storey is nearest the block's flat (`ShowFlatRules.FlatArea`, `ShowFlatDesigns.Nearest`). Priced at the game's own prices: its pieces at `ItemHelper.GetDefaultMarketPrice` and the firm's installation fee for the structure. They are never written to the firms' folders: a show flat is no home the firm installs in.
- **Its quality.** Interiors scores it as it scores a business: the game's interior score of the design's walls and floors (`InteriorScoreCalculator`) and the decor score of its pieces (`InteriorDirector.DecorOf`, the same arithmetic as a business's), overall six tenths and four tenths. Against the standard of the block's district (`InteriorDirector.StandardOf`; a plot's is the new district's, `DistrictValues.Id`).
- **Where it is.** A place of the floors core (`ShowFlatPlace`), on the design's apartment structure, laid by the core with the design's pieces (`FloorLayout.Design`, a cut's keep, nothing left standing on a piece taken out). The player walks in by the building's door as to any floor; **Show me the way** puts the building on the game's destination guider (`GameUi.GuideMark`). Its id, `~construction_showflat`, sorts after every place every machine shares, so it never moves their floors.
- **Viewings.** One a day at the district's standard, two from 10 points over it, three from 25 over (11:00, 14:00, 17:00). A viewing is decided as its hour ends: the chance (`ShowFlatRules.BuyerChance`) is 170 in 1000 at the standard, 6 more a point over it and less under, 2 more a point of the company's reputation over 50; the player showing the buyers round (the game's own panel on the floor, **Show them round**) adds 150, the sales agent 90, and with nobody there the chance is 45% of itself. A buyer takes a flat off plan at the show flat's price: the plain off plan price, 3 thousandths more for each point over the standard and less under, 900 to 1120 in 1000 (`ShowFlatRules.PricePermille`). Deposits now, the rest at completion: the one off plan sale (`OffPlanSale`), as the plain **Sell off plan** button.
- **Costs.** The dressing once (the design's furniture and the installation), then 240 a day open and 210 a day for the agent, at today's prices. It closes at the handover, when the block is sold out or given up, or when the player closes it.

### Renovations

- **A refit** of a business the player runs (a shop or an office they rent or own, theirs in a session, with a door on the street): from the business's **Interior** tab in BizMan (**Renovate**) or the app's Land tab. With a licensed company it is a project of its own (`ProjectKind.Refit`): ready at once (an interior needs no permission), worked by the company's crews as the programme's refit works (`RefitRules.Works`, Civic's `SiteKind.Refit`, a hoarding at its front): its loads, trades and plant, one final inspection, the renovation speciality's. Without one, the city's best renovation contractor (the eight rivals bid as on a tender, `RefitRules.Quote`) does it at its quote, paid at once, at the city's pace.
- **While the works go on** the business is closed: business pressure's own closure (`ClosureSource.Refit`), held three hours at a time each hour, so it opens by itself if the construction game stops; let go at the handover or when the project is given up.
- **At the handover** Interiors refits it (`InteriorDirector.Refit`): wear to nothing and new fittings that add 6, 10 or 14 points to the decor score by the work's grade (a contractor's good; the company's by the work's quality, a grade better as a renovation company), fading as the finishes wear (`InteriorRules.RefitLift`). Kept in the save's line `reallife:interiors_refits`.
- **An exterior refresh** is the construction game's rebuild of a building the player owns (Land, **Plan a rebuild**): the new look through the rebuild's site, where the building allows it. A rebuild can carry the interior refit too (**Refit the business inside too**): the business closes for its last stage and is refitted at the handover. The City Hall shopfront refit (Civic) stays the shopfront's: no interior, no closure.
- **Tenders.** Businesses of the city's put their refits out to tender every 5 to 9 days (`TenderSource.Refit`), valued at the programme's refit rate; the renovation speciality's work. A rival's win puts the hoarding up at the business.

### The city around it

What the city does around the company (the approved list's "the city around it"), with the option **Rigged tenders, unpaid bills in court and the building of the year** (`construction_city`, on) for the last three:

- **The building code is the elected government's.** The permits policy of the city's government (Civic) sets the new district's storeys (12, the most its buildings take, or 8 under strict permits), the other districts' limits (a third more or less), the parking asked (a third fewer or half as many again), the planning fee (40 % less or half as much again) and the surcharge on land kept empty (doubled or halved): `CivicCatalogue`'s figures, read through the `Permits` bridge by `PlansRules.HeightLimit`, `ParkingNeeded`, `PlanningFee`, `Estimate` and `LandRules.LandTax`. The plans, the estimate and the decision use the code in force on their day. With the city's elections off, the code is as the city came.
- **One council for zoning.** An application for a lot is heard by the city's council (`ConstructionBridges.CouncilDay`, `Council`, Civic's `CivicGovernment.Hear`): at its Thursday planning sitting, or at the player's own meeting if they sit on it, where they have a vote and an interest to declare. The council can also allow a use on every lot of a class (a motion of the player's in office): `BuildState.ClassAdded`, so a lot's uses are its own, those granted for it and its class's (`ZonesNow`). With no council answering, the planners' odds decide as before.
- **The City tab.** An eighth tab: the code in force and what is to change when (the permits change on its way and the next election, `CodeAhead`), the council's next sitting, the zoning before it and the classes rezoned, the bills, and the building of the year. City Hall's planning pages and policy pages show the same code.
- **Rigged tenders** (`RingRules`, `.Rings`): a ring of three builders forms on 9 % of the city's works' tenders and 14 % of others (the save's seed and the tender); the leader marks its price up a tenth, the others cover at 118 to 126 % of the value. A licensed company of standing is asked to cover (65 %) at 125 % for a cut of 2.5 % of the winning price, on the tender's page: cover, turn it down, or report it. Reported, the ring's bids are struck out at the award and it is found out at once (the company's standing +3, Standing +2). Otherwise the city's inquiry may find it out on any of the 45 days after the award (2.5 % a day): every member's standing -10, the City Ledger names them (the paper's story path, which the builders' share prices may listen to), and a company that covered faces a case of bid rigging (`TaxCases.FileBidRigging`, standing -15, Standing -6). Not on a Going Public guest, which has no tenders.
- **Unpaid bills** (`BillRules`, `.Bills`): at a contract's handover the client pays its final part on the day, late (3 to 20 days, 26 %) or not at all (9 %, never the city); a block's off plan buyers keep their balance back now and then (17 % each, a third of them for good). The company sends up to two letters three days apart and then sues (after a letter, or a week overdue): a claim of its own (`OwnClaims.File` through `Sue`, the court's fee paid, the result read back through `SuitResult`). The other way: a crew's day the company cannot pay is owed and paid as soon as the cash is there, or after five days the crew sues (`BuildClaims.FileUnpaid` through `Unpaid`); an order the cash cannot cover goes on the merchant's account for 14 days (a company of standing 30, nothing of a merchant's overdue), paid on its day if the cash is there, sued for a week after.
- **The City Ledger's opinions and the building of the year** (`AwardRules`, `.Award`): each building finished in the city (the company's handovers, the developers' buildings, a public building a rival put up) is given the critic's score (its quality, on time or late, a few points of taste) and the paper writes of the player's and of the city's best and worst (one to five stars). In the year's last days (an eighth of the year, three to seven days) the four best are shortlisted and the city votes on Flare each day by their scores, the player's following adding to theirs (`Followers`); the next year's first day names the winner: the builder's standing, and for the player's company +6 reputation and Standing +4.

The save: four parts after the line's own, "^z:" the classes' zoning, "^g:" the rings, "^b:" the bills, "^y:" the year's buildings and the last award (`BuildStore.City`); an older reader passes them over, a line without them reads as a company the city never ruled on. Tests: "the city's council hears a zoning application now", "a rigged tender covered for and found out, and one reported", "a client taken to court, and a crew's claim for wages", "the building of the year awarded now" (`TestCouncilZoning`, `TestRing`, `TestBills`, `TestYearAward`, each a public method a runner calls through `Wiring.CityTests`, each writing `Construction check (...)` with PASS or FAIL); "put everything back as before the tests" takes back the cases and claims they filed (`CityBack`). Offline: `EconomyCheck/CivicBuildChecks.cs`.

### With the landmarks

Uptown's landmarks ([Uptown's landmarks](landmarks.md)) are tendered and built through the construction game; `ConstructionBridges`' ten landmark bridges are wired to `Scripts/Landmarks` in `Wiring/Wiring.ConstructionLinks.cs`:

- **A landmark's tender** (`TenderSource.Landmark`, `ConstructionDirector.LandmarkTender`): put out by the landmarks' timetable, never in a Going Public session; priced as a public building of the landmark's use, floor area and storeys with 40% more (`BuildCatalogue.LandmarkPremiumPermille`), open four days (`LandmarkTenderHours`), awarded with the others. The player's win is a project on the landmark's site (`LandmarkSites`: each site as a lot of the construction game's, its ground the works' plot), wearing the landmark's design (`LandmarkDesign`); a rival's win is told to the landmarks (`LandmarkAwarded`), whose timetable runs the rival's works.
- **The lots a landmark holds** are taken (`LandmarkHeld`): not for sale, never a development's, never a public building's. The Land tab shows each landmark as a row of its own, its state (`LandmarkWords`) and a way to its tender.
- **The player's handover** of a landmark goes to the landmarks (`LandmarkHanded`), which hand it to the district.
- **The works a rival or the city runs** for a landmark (`LandmarkWorks`) are the city's megaproject: not one of its six, shown with the game off, at the timetable's pace (`LandmarkPace`), wearing the landmark's design; the programme keeps no look of its own where a landmark stands in its own right (`LandmarkBuilt`; the stadium, and a building of the district's once it stands).
- **The height limit:** a landmark is the city's own project, granted at its award; the district's height limit (`PlansRules.HeightLimit`, the elected government's building code: 12 storeys under Fast-track, 8 under Strict) is for the plans a company draws, so the tower is the council's own exception and may stand taller.
- A public building a rival won now goes at the city's pace and is handed over when its works are done (before, its site stopped just short of the end).

### Going Public

- **Deterministic.** The city's developers are drawn from the lot plan and the day alone (`DevelopmentRules`: whose lots, in what order, their uses, builders, designs and days, never a save's seed): every machine finishes the same lots on the same days (`DistrictBuildings.Finish` with `FinishOwner.City`), and only the host sets their sites out. Rival builders are the same eight with the same figures everywhere, their offices on the same lots, set up by the game's own start of a business with the engine's chance seeded from the save, never in a unit the player or another player rents or owns.
- **The player's company is theirs alone.** Its line is in their save; its offices are theirs (`GoingPublic.Mine`); its money moves through the game's own call. No land is sold in a session; a public building's tender is never put out in one, a business's only by the host; the city's works go to tender on the host. A guest's rebuild is a site of the guest's own (the programme's own-id rule); a guest starts no plot.
- **Show flats and refits.** A show flat is the player's own: its floor is on their machine only (its place asks after every shared place, so no shared floor moves), and its viewings draw from the save's seed. A refit is only of a business that is the player's (`GoingPublic.Mine`); its closure is business pressure's own, and its site is the programme's own-id rule's, as a rebuild's. A guest puts out no refit tender.
- **Nothing new sent.** The company sends nothing. Its sites are the programme's own records in the host's existing line (the finished looks appended after the sites, which an older reader ignores).
- **Known limit:** a lot a player bought alone stays theirs in their save; in a session the developers pass over it on that player's machine only.

### The save

- One line under `reallife:construction` ("v=1^h:...^f:...^l:...^c:...^q:...^s:...^p:...^t:...^r:...^d:...^o:...^k:...", the company's books by day in `k`, a project's hedge at the end of its record), and `reallife:construction_before_tests` while a test has changed something. Fields are appended at the end of a record, parts by a new letter; a newer line is read as far as this version knows it, a line this version cannot read is kept as it was read (`SaveLines`).
- It names the district's lots by their ids and the game's buildings by street and number as words of its own; nothing of the game's part of the save. A finished building on a lot becomes the district's record (Uptown's own line, kept by the district save). A company is a `BuildingRegistration` with the type `reallife:businesstype_construction`, made a design studio again by "prepare this save for removing the mod".
- A save without the line starts afresh; the sites of a line whose game is switched off wait, put away.
- A hired builder's contract is six fields at the end of its project's record, after the hedge (its quoted days, pace, the stages paid, a test's mark, what is paid and the penalty); the builder and the price are the contractor's two fields a refit already had. An older reader passes them over, and a line without them reads as no contract. A show flat and a refit are fields at the end of their project's record (the design's id, its scores, its agent and sales; the contractor, the price, the grade, a rebuild's refit), and the next refit tender's day at the end of the header. The show flat's floor is no record of the game's: it is asked again at each load. A refit's closure is the game's own switch, as business pressure's closures are.

### Options

- **Your own construction company** (on): off, the company, its land, projects and tenders are not there, its sites wait put away, and the city's programme is as without it. It needs the city's construction sites.
- **The city's developers build on the lots, and works go out to tender** (on): off, no development, no tender, no rival's offer.
- **Show flats for blocks sold off plan** (on): off, no show flat (an open one waits, put away, its floor let go).
- **Refits of your businesses, and businesses' refits to tender** (on): off, no refit, no closure for one, no refit tender.
- **Land tax on the lots you own** (on); **Theft, storms and faults on your sites** (on).
- **Rigged tenders, unpaid bills in court and the building of the year** (on): off, no ring forms, clients and buyers pay on the day, an unpaid crew only loses heart, no merchant gives an account, no award.

### Self-check lines (Developer diagnostics)

- `Construction check (day N): ... project(s), ... crew(s), ... tender(s), ... development(s) known; N ms: PASS` each day.
- `Construction check (planning): ...: granted|refused (...)`, `Construction check (appeal): ...`, `Construction check (inspection): ... odds ... passed|failed`, `Construction check (tender N): N bids ...` as each is decided.
- `Construction check (own design): project N's design from the practice: score ..., cost ... in 1000 of its budget ...` when the player's design is in.
- `Construction check (trades): the player's own qualification, the brief at the practice and the design from it are wired: PASS` at the start; with the district's lots wired or not.
- `Construction check (ribbon panel)` from the game's own panel at the opening.
- Always: `Construction: read (line version 1): ...`, `Construction: running.`, each project's step, `Construction: the city's developers (N) began on lot ...`, `finished lot ...`.

### Tests (Options > Tests, with "Show test tools" on)

Each brings forward one thing a player would wait for, or says in a notice why it cannot, and writes its `Construction check (...)` line; the state from before the first test is kept for the last.

- **Construction: a lot of yours, in the new district or ahead of you**: the new district's open lot nearest to the player, theirs for nothing, from anywhere and indoors too (no spot to look for, and its building goes to the district when it is done); with no district, 24 by 30 m of open ground ahead, zoned for every use and priced at the nearest building's district.
- **Construction: found the company, with its licence**; **City Hall decides the licence**; **every set of plans drawn** (a design of the player's own is read if the practice has it, else drawn at 70); **every planning decision and appeal**; **the council votes on zoning**.
- **every site on to its next stage**, **every inspection held**, **every delivery arrives**, **every site handed over**, **an opening day today** (the ribbon at any hour of the day).
- **a business's tender opens** (closing in two hours), **every open tender awarded**, **the developers start or finish a building** (not in a session).
- **a rival's offer to your best crew**, **a crew on strike**, **a fault comes to light**, **a bribe comes out**, **a buyer for a flat**.
- **put everything back as before the tests**: the state from before the first test, and the sites the tests began taken away; money moved since stays moved.

### Show flats' and renovations' tests

Each works from anywhere, writes one `Show flats check (...)` or `Renovation check (...)` line ending PASS or FAIL, and is a public static method of `ConstructionDirector` a runner can call: `TestShowFlatNow`, `TestShowVisitNow`, `TestViewingNow`, `TestShowBuyerNow`, `TestRefitNow`, `TestRefitStartNow`, `TestRefitTenderNow`, `TestPutBackNow`.

- **Show flats: a show flat at the test project now**: the player's block sold off plan, or one made for the test (the district's nearest open lot, a block of homes with its plans and permission, ready to sell off plan); its show flat in the first design, dressed for nothing.
- **take me into the show flat now** (opens one first if none is), **a viewing now** (decided at once, shown round by the player if they are on its floor), **a buyer at the show flat now** (a sale at the show flat's price).
- **Renovation: renovate the nearest shop of yours now**: the player's nearest business refitted (a contractor's, for nothing) and handed over at once, the wear and decor before and after in the line; **a refit under way at your nearest business now** leaves it running, the hoarding up and the business closed; **a business's refit tender now**.
- Each joins the city expansion's test run with one `.Check(...)` in `ConstructionGuide` (its button too): all where the player stands before the load, but **into the show flat** out of doors; the construction game's own put back at the run's end undoes them.
- The construction game's **put everything back** also puts back the interiors a test refitted (`InteriorDirector.TestKeep`, `TestPutBack`), lets a closed business open and lets the show flat's floor go.

### Hiring a builder's tests

Each writes one `Construction check (...)` line ending PASS or FAIL, works from anywhere with or without a company, moves no money (a test's contract), joins the city expansion's test run with one `.Check(...)` in `ConstructionGuide` (its button too), and is undone by the construction game's put back:

- **Construction: the city's builders' quotes for a building of yours** (`TestHireQuotesNow`): the player's building ready to start, or a block of offices made for the test on a lot of theirs; the eight quotes read twice and the same, the cheapest, the fastest and the best named; the app opens on them next.
- **Construction: a building of yours put up by a builder of the city's, handed over now** (`TestHireNow`): the cheapest hired, every stage moved on and paid, handed over, its opening tomorrow, the building the district's.
- **Construction: a late builder pays its penalty** (`TestHireLateNow`): the same, handed over four days late, the penalty off the price.

### The front desk's tests

Each writes one `Construction check (...)` line ending PASS or FAIL and is a public static method of `ConstructionDirector`; nothing is changed:

- **Construction: take me to a builder's desk** (`TestDeskGoNow`): the nearest rival builder's office (by its door) made the game's destination, as the Rivals tab does; inside one already, its front desk found and the player told to click the person there. In the city expansion's test run.
- **Construction: talk to the builder at the desk now** (`TestDeskTalkNow`): the conversation opened from anywhere, at the office the player is in (facing its receptionist) or the nearest's; its line names the page it opens on and, for a ready building, that its quote is the app's own. A button only: it waits on the player's choice.
- **Construction: every builder's desk pages written out** (`TestDeskPagesNow`): every builder's welcome, what to do first and its record, each quote the app's own. In the city expansion's test run.

### Offline checks

`RealLifeOverhaul.Build/EconomyCheck/BuildersChecks.cs` (`dotnet EconomyCheck.dll --builders` alone): the catalogue's tables; the game's own value of a building and a lot's price; zoning, the council's day, land tax, flats; the plans' hours, stages, inspections, exits, parking, height and engineer; the estimate and the player's design's cost; planning, objections, the appeal and the licence; a crew's pace, deliveries, rework, bribes, morale; the loan's draws, a year's interest and the instalments to the last unit; tenders and the winner; the developers' share, order, uses, builders and days, and that no seed is in them; the frame against `PlotRules.Frame` for every plot size; the store's line read back to the same line, a newer line's fields passed over; then the sources (no LINQ, no Update but the one, the layering, every bridge wired and put back, the trades' three lines, the shared files' lines, every test's check line) and every key in en, pt and zh-cn, What's new in all nine.

`RealLifeOverhaul.Build/EconomyCheck/HireChecks.cs`: the eight quotes, their week and a busy builder, the refit's quote unchanged through the one loop, the contract's money to the last unit, the penalty and its cap, the settling up, the builder's pace and quality, the save's fields read back and passed over, the sources (the client's path through the company's own site, inspections and handover; no company or licence asked; the app without one; the tests and their lines) and the texts in every language.

`RealLifeOverhaul.Build/EconomyCheck/BuilderDeskChecks.cs` (`dotnet EconomyCheck.dll --desk`): what the desk talks about first for every way the player's building can stand, the front desk by the door, the office manager's counts, an office receiving nobody in a unit anybody rents or owns; the sources (the one quote and hire the app has, the game's question box with the app's words, the game's own call to talk first in its list, the panel facing the person, the time held never in a session, the pointer standing aside while furniture is placed, the director's second and stand-down, no frame method or scene search, the tests and their lines) and the texts in every language.

`RealLifeOverhaul.Build/EconomyCheck/ShowFlatsChecks.cs` (`dotnet EconomyCheck.dll --show-flats`): the viewings, a buyer's chance, the price and the costs; which blocks may have a show flat; a refit's hours, grade and quote (the same for the same seed and day); the fittings' lift and its fade through Interiors' own working, and a room's decor by a business's arithmetic; the construction line with a show flat and refits read back, and one from before them; the interiors' refits line; a refit's closure in business pressure's line; the tenders' spacing and speciality; the designs' ids, cuts and structures, and against the installed game's residential designs; then the sources (the layering, nothing per frame, the floors core's design layout, the place's id, the wiring, the options, every test) and the texts in every language.

### Not verified without the game

Everything above is built and checked offline. Not seen in play: the front desk's person picked and outlined in a builder's office and in the player's own, the box round them taking the click while the desk keeps its own, the walk up and the panel facing them, the game paused and running again, the question box after the dialogue closes, and the app opening on the right page; the quotes page and a hired builder's site in the app, its notice board naming the builder, the contract's payments in the game's statements; the app's pages in the game's frame, the City Hall pages, a site's pace and its notice board, the ribbon's panel at a building's front, the finished looks standing, the business type in BizMan with its staff and desks, the transactions in the game's statements, the credit file's account, and the new district's lots for sale and its buildings handed over. Of the show flats and refits: the game's residential design laid on a borrowed apartment structure on a floor (its pieces, walls and floors, and the game's own interactions with a bed or a kitchen there), the viewing panel on that floor, the guider to its building, the Interior tab's Renovate button and the question box, a business kept closed for its refit and opened at the end, the hoarding at its front, and the decor and wear in the Interior tab after a refit.
