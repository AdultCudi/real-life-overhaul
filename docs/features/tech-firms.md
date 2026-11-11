# The tech firms' markets

## In a sentence

Under the living economy's master option **Living population** and its own option **Tech firms rebuilt**, the player's software studios, AI labs, IT consultancies, cybersecurity firms, web agencies, design studios and data centres sell into markets sized from the city's people and firms, shared with rival companies and startups that play by the same rules, with no cap but the market, and each kind has its own desk sessions.

## Where the code is

- Folder: [`Scripts/Tech/Sector`](../../Assets/Mods/RealLifeOverhaul/Scripts/Tech/Sector/); the director's parts in `Scripts/Tech/TechDirector.Sector.cs`, `TechDirector.SectorDesk.cs` and `TechDirector.SectorTests.cs`; the fourteen desk sessions and the population's city in `Scripts/Wiring/Wiring.SectorLinks.cs`.
- Options: "Tech firms rebuilt" (`living_firms`), under "Living population" (`living_population`). Off, the firms run by their own rules as before: a market of their own for each product, the old offers and tenders, the old cloud. The products, customers, contracts and racks are the firms' own records either way, so switching it on and off loses nothing.

```
Tech/Sector/                    the first ten are game-free and compiled by EconomyCheck (SectorChecks.cs)
  SectorCatalogue                 every figure: the world the city stands for, paces, curves, the rivals, IT work, data centres, the sessions
  SectorFile                      the city last read, the markets' anchors, the rival products and companies, the power price, the firms' and products' records
  SectorRules                     the markets sized from the city, the curves in place of the clamps, a product's day in its market, reputation
  SectorRules.Rivals              the companies' and startups' products, their day by the player's rules, tenders, who comes for the player's people
  SectorRules.Work                the city's IT work shared among the firms, client sizes, security incidents
  SectorRules.Infra               racks, compute and AI compute from the city's firms and labs, the cloud's share, power price spikes
  SectorRules.Desk                the fourteen desk sessions: what each choice does, a manager's sound choice
  SectorRules.Old                 an older save taken in: the rivals as strong as before, markets held up for old customers
  SectorStore, SectorShare        the save's line, "reallife:tech_sector"; the shared city's part, letter R
  SectorBridges                   the population's city and Flare's talk bridge
  SectorViews                     every screen as data: a firm's page, a product's market, the data centres' markets
  SectorViews.Page                a firm's whole page as data for the mod's design system: figures, products or work, rivals, team, shares
  SectorGuide                     the feature's entry and the Guide page's figures
  TechApp.Sector                  the screens drawn in the Tech app until the mod's design system draws the same data
```

## The markets

Each kind of product and buyer is one market: software for people, businesses and companies, apps, licences, and AI for each buyer. Its buyers are the city's people (with their households' mood for digital spending), its small and mid-sized businesses or its large organisations, counted from the population's employers (a business of the game's is one firm of its size, an office building's other floors many firms in Manhattan's mix, a big public body and the mod's own companies large), times the world each stands for beyond the city (some 60 people for a resident, eight businesses for a business, some thirty organisations for a large one), growing each of the save's years and filling towards its ceiling (the one clock, `CalendarRules.MoneyYearDays`'s note: two fifths more people and a fifth more firms in ten years, a long game in a short year never running away). With the population not running, the documented city stands in: the living city's first day as its employers count it (55,600 people; 1,490 small, 156 mid-sized and 12 large firms), so a market is the same size either way. Each size of firm's world, projects, racks and compute are sized on those measured firms, so the living city's business, company and AI markets are the size the money scale's paybacks are set on.

A product's share is its appeal among the buyers who know of it against every other product's in the market and against buying nothing (`ScaleRules.Share`). Appeal is features against the market's usual number (2r/(1+r), no clamp), soundness (no floor of 300), price against the market's going price (the money scale's curve), the firm's name (no top of 800) and its designers (a curve). Awareness is a month of marketing over the buyers (`ScaleRules.Reach`) and what the customers tell others. Customers move towards the share at the market's pace (people fastest, companies slowest, sellers speeding businesses), churn by the firms' own rule, and a product past its share loses customers to the others. A licence's deals close at its pace while it is under its share: no cap of 400 deals. Reliability and security postures are curves with no top of 950.

## Rivals

The listed companies that make software (Codeharbour Software, Abacus Lane Software, Shopfront Online, Tallyrow Analytics, Signpost Search, Ledgerline Payments) have a product in each market they sell in, and every running startup of a software sector one in its market. Each builds with its team by the player's feature cost (a model also needs its compute), cuts its price when it has lost a good part of its customers and raises it back, and goes down now and then. Council tenders are decided by one rule for every bidder, each company with its own name. A company that just took a lead or won a tender comes for the player's people harder. Seeded from the sector's seed.

## IT work, data centres and the cloud

The city's firms put out projects by their size, their IT budgets spread over 365 days whatever the save's year; each of the player's firms that takes work earns its share against the city's rival IT firms (a web agency or a design studio against its fewer rival agencies) by its name and sellers, carried until a whole offer (never a daily draw); a large client's project is bigger, and a fresh portfolio draws more of them. A branding project is priced as designers are paid (a graphic designer's pay against a software engineer's, the build rate's 780 in a thousand: its fair price; at the build rate a design studio would pay its desks back in weeks and then earn more than a firm of engineers). The council tenders IT work, an integration or a managed service, only while one of the player's firms takes it: a web agency or design studio alone could never bid on them, and a bid would fail and cost it its name. A change order agrees a date for the work it adds. A security firm's managed clients are attacked now and then. The city's firms rent racks (by size, at the end game's scale the region too) taken by tier and the cloud's name; they buy compute and AI compute, with the rivals' software customers' hosting and labs' training, taken by the cloud's price and name against the rival clouds. Power price spikes come every month or two.

## At the desk

Fourteen sessions, each held at the owner's desk in the firm's office or by its manager: a software studio's sprint planning, outage response and big customer's demands; an AI lab's training run (with a benchmark against the rival labs) and model launch; an IT consultancy's pitch or tender and scope creep; a cybersecurity firm's incident response; the data centres' capacity planning, power price spike and failover (offered while the player has a data centre); a web agency's or design studio's client pitch, revision round and portfolio piece. A session waiting on an event (an outage, an offer, a spike) is due while it waits.

## A product's page

- **What the next feature waits for.** A feature is done when its work is in and, for an AI model, when its generation's training has come too (`FirmsRules.Build`): 400 AI compute unit-days for each of the first two features, twice as much for each of the next two, and so on, added a day at the level the page sets (none, 20, 50, 100 or 200 units; 50 by default); the rival labs wait for their training by the same rule (`SectorRules.RivalDay`). The work goes on building while a model waits, so a lab with more engineers than training has its work past a feature's cost. The page wrote the cost less the work, which showed a negative number of points to go ("-211 points", then "-882", on a lab making 529 points a day). `FirmsRules.Wait` now gives the pages three figures, none below zero: the work still to put in, the work past the cost (kept for the features after it, and spent as the training comes) and the training still to come with the days it takes at the level. `ProductViews` chooses the lines (a text's key and the names that fill it) for the old pages and the kit's alike, and `TechText` writes them: when only the training is short the next feature's line says the work is done and waits for it, and the training line says what training is and how many days it takes (or that the level is none, so none comes). A firm's page shows a product's progress as the lesser of its work and its training. A withdrawn product waits for nothing: the day does not build it (`TechDirector.Firms`) and the cloud does not train it (`TechDirector.Infra`), so `FirmsRules.Wait` gives it no work, no training and no days, and its page says none of it.
- **Why a product has no customers.** A product that is not on sale yet says so and what puts it on sale (`TechText.OffSale`: the Launch button, from the third feature). A licence closes a deal at a pace, never by chance (`FirmsRules.LicenceRate`, `SectorRules.LicenceRate`): with a feature lead over its rival, nobody selling it and no marketing, about one deal in under two weeks, sooner with sellers, so a product better than its rivals is not shut out of its market. The firms' own page said the days (`reallife_tech_product_deals`); the tech markets' section now does the same, given the firm's selling points (`SectorViews.Market`), or says that the licence already holds the companies its pull earns it, or that its pull earns it fewer than one, in place of a pace it cannot keep. A product not on sale yet has no line saying its customers are heading up, as it has none. The section says where the customers are heading and how the deals close only once the day has counted the market: the markets are worked out each day and not saved, so on a save just loaded they hold no buyers until the next day, and a pull earned against nothing says nothing of the product. A founded firm's product is weighed in its niche, as its sale weighs it (`NicheRules.Market`, worked out once for the sale in `TechDirector.NicheView` and for the page in `TechDirector.SectorMarketSection`), so the target, the share and the days to a deal on the page are the niche's, with the founder's selling points a sales channel adds; a niche with no businesses today has no heading and no deals line.

## Older saves

On the sector's first day the companies' products start as strong as the rivals the products faced, then each market is held up so every product sits at its share with the customers it has, fading to the city's own market over a year: nothing jumps.

## Going Public

Only the player's own firms, products and professionals are touched. Every roll is seeded. The city, the companies, the rivals, the power price and the markets' anchors are the host's: a versioned part of the shared city (`SectorShare`, letter R) carries them to the guests, who apply it and never step the rivals; a message of another version of the mod is never applied. Each player's products stay on their own machine.

## Cost

Nothing per frame. The markets run inside the firms' day: a pass over the eight markets and the rival products, and a product's day each (EconomyCheck: 48 products and the rivals under 0.5 ms a day; the save's line written and read in under 2 ms). The city is read once a game day. A page builds its screen when drawn.

## Tests

- **Hold a software studio's, AI lab's, IT consultancy's, cybersecurity firm's, web agency's or design studio's, or the data centres' desk sessions now**: each kind's sessions held at once with a manager's sound choices, the event a session answers set up first where the test can.
- **Show the tech markets now**: each market's buyers, the player's share and the leader in the log, the best share in a notice.
- **A year of the tech markets now**: the rivals' year and the player's products selling through it, the takings booked today.

With Developer diagnostics on: `Tech check (desk, ...)`, `Tech check (market, ...)` and `Tech check (markets, rivals)`, each with PASS.

## Checks

EconomyCheck (`--sector`, SectorChecks.cs and its parts): the markets sized from the city, the one clock (a year or ten of the save grow the world alike at 60 days a year and at 365, filling towards its ceiling), the curves, the rivals, the city's IT work, data centres and the cloud, the desk sessions, older saves, the save and the shared part, every screen, and each kind's ten years of money (3,650 game days) at years of 365 and 60 days on the documented city, each payback in its kind's band and every last year paying its way: software 120 to 360 days, an AI lab 180 to 540, the services 60 to 240 (a design studio's contracts alone among them, at their fair price, with the desk sessions a manager answers: a consultancy's scope creep and an agency's revision round), the cloud and the hyperscaler the end game's 120 to 360. The living years (LivingYearsChecks.cs, the population's page) run the same kinds on the living city beside the documented one.

EconomyCheck (`--tech-points`, TechPointsChecks.cs): the report's lab (529 points a day on 50 units of training a day) through forty days, its work far past a feature's cost for most of them, what it waits for never below zero and the page's words for each day, the rule's states (the work in, the training short, the level none, a damaged record, a withdrawn model), the page and the build agreeing on what a feature waits for in 480 states of four kinds of product, being built and on sale, the lines `ProductViews` chooses in a grid of 2,880, 1,440 and 60 states (the key, the names held to the English text, the tone, and none for a withdrawn product), a licence ahead of its rival closing its first deal when its page says (the firms' own rules and the tech markets, with no marketing and nobody selling; the premise that the demand rule is right), the markets' deals lines, a save just loaded saying nothing of a pull against markets not counted yet, a founded firm's licence told its niche's target and days, which are the days to the sale's first deal (a niche too small for the companies it has, one with room and a sales channel, one with no businesses, a founding that does not count, competition not begun), the sources (only the rules name a feature's cost, every page goes through the tech words, the day and the cloud still skip a withdrawn product) and the texts.
