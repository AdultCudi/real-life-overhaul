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
  SectorGuide                     the feature's entry and the Guide page's figures
  TechApp.Sector                  the screens drawn in the Tech app until the mod's design system draws the same data
```

## The markets

Each kind of product and buyer is one market: software for people, businesses and companies, apps, licences, and AI for each buyer. Its buyers are the city's people (with their households' mood for digital spending), its small and mid-sized businesses or its large organisations, counted from the population's employers (a business of the game's is one firm of its size, an office building's other floors many firms in Manhattan's mix, a big public body and the mod's own companies large), times the world each stands for beyond the city (some 60 people for a resident, eight businesses for a business, some thirty organisations for a large one), growing each of the save's years and filling towards its ceiling (the one clock, `CalendarRules.MoneyYearDays`'s note: two fifths more people and a fifth more firms in ten years, a long game in a short year never running away). With the population not running, the documented city stands in: the living city's first day as its employers count it (55,600 people; 1,490 small, 156 mid-sized and 12 large firms), so a market is the same size either way. The documented city's guess before the population was measured (2,500, 160 and 30 firms) left the living city's business, company and AI markets at three fifths, two fifths and two fifths of the size the money scale was set on, so their paybacks ran to years; each size of firm's markets, projects, racks and compute were moved onto the measured firms whole.

A product's share is its appeal among the buyers who know of it against every other product's in the market and against buying nothing (`ScaleRules.Share`). Appeal is features against the market's usual number (2r/(1+r), no clamp), soundness (no floor of 300), price against the market's going price (the money scale's curve), the firm's name (no top of 800) and its designers (a curve). Awareness is a month of marketing over the buyers (`ScaleRules.Reach`) and what the customers tell others. Customers move towards the share at the market's pace (people fastest, companies slowest, sellers speeding businesses), churn by the firms' own rule, and a product past its share loses customers to the others. A licence's deals close at its pace while it is under its share: no cap of 400 deals. Reliability and security postures are curves with no top of 950.

## Rivals

The listed companies that make software (Codeharbour Software, Abacus Lane Software, Shopfront Online, Tallyrow Analytics, Signpost Search, Ledgerline Payments) have a product in each market they sell in, and every running startup of a software sector one in its market. Each builds with its team by the player's feature cost (a model also needs its compute), cuts its price when it has lost a good part of its customers and raises it back, and goes down now and then. Council tenders are decided by one rule for every bidder, each company with its own name. A company that just took a lead or won a tender comes for the player's people harder. Seeded from the sector's seed.

## IT work, data centres and the cloud

The city's firms put out projects by their size, their IT budgets spread over 365 days whatever the save's year; each of the player's firms that takes work earns its share against the city's rival IT firms (a web agency or a design studio against its fewer rival agencies) by its name and sellers, carried until a whole offer (never a daily draw); a large client's project is bigger, and a fresh portfolio draws more of them. A branding project is priced as designers are paid (a graphic designer's pay against a software engineer's, the build rate's 780 in a thousand: its fair price; at the build rate a design studio paid its desks back in weeks and then earned more than a firm of engineers). The council tenders IT work, an integration or a managed service, only while one of the player's firms takes it: a web agency or design studio alone was offered tenders it could never bid on, and a sim that bid on them failed every one and lost its name. A change order agrees a date for the work it adds. A security firm's managed clients are attacked now and then. The city's firms rent racks (by size, at the end game's scale the region too) taken by tier and the cloud's name; they buy compute and AI compute, with the rivals' software customers' hosting and labs' training, taken by the cloud's price and name against the rival clouds. Power price spikes come every month or two.

## At the desk

Fourteen sessions, each held at the owner's desk in the firm's office or by its manager: a software studio's sprint planning, outage response and big customer's demands; an AI lab's training run (with a benchmark against the rival labs) and model launch; an IT consultancy's pitch or tender and scope creep; a cybersecurity firm's incident response; the data centres' capacity planning, power price spike and failover (offered while the player has a data centre); a web agency's or design studio's client pitch, revision round and portfolio piece. A session waiting on an event (an outage, an offer, a spike) is due while it waits.

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
