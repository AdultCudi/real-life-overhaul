# Interiors and decor

## In a sentence

Interiors and decor that matter: the game's interior design score, a decor score from the placed items, wear, each district's standard, and what they do to customers, prices, complaints, brands, Standing, staff, hiring and bids.

## Where the code is

- Folder: [`Scripts/Interiors`](../../Assets/Mods/RealLifeOverhaul/Scripts/Interiors/).
- Read first: `InteriorRules.cs` (the decor score, the target and every effect with its cap, game-free), `InteriorDirector.cs` and `InteriorTab.cs` (the **Interior** tab in BizMan).

| File | What it is |
| --- | --- |
| `InteriorRules` | the decor score (diminishing returns by item kind, each category's share, variety), the target by floor area, wear and dirt, the district's standard, every effect and its cap, the parts the other authors' mods take over (game-free) |
| `InteriorStore` | each business's wear and news in the save's one line (game-free) |
| `InteriorGame` | everything it touches of the game: the interior score, the placed items sorted by the game's own tags and types, the floor area, the district's minimum, the basket, a refresh's charge; InteriorBridges, what it asks of the others |
| `InteriorDirector` | the one component: the morning's round, the hourly basket, footfall written, complaints and news, the test buttons |
| `InteriorTab` | the Interior tab in BizMan: a copy of the game's own tab button and two of BizMan's scroll views with MyEmployees' lines: the scores, and four pages (what it does, the items that count, today's customers by where they came from, the game's own rules for the business) |
| `InteriorText, InteriorGuide` | its words, its options block and its three feature entries (Guide figures from the rules) |

## How it talks to other features

- The feature calls no other feature and none calls it: `Wiring/InteriorLinks.cs` writes its share of a business's customers through business pressure's footfall, reads the cleaning service and the district's income, and lets the brands, Standing, the company, the professionals, the inspector and the paper read what it measures.

## The rules

Every figure is in `InteriorRules` (game-free, checked by EconomyCheck's InteriorChecks); the Guide prints its figures from there.

**The scores.**

- **Interior:** the game's own interior design score (walls and floors, InteriorScoreCalculator), 0 to 100.
- **Decor:** the placed items, sorted by the game's own tags and types into seven categories. An item kind's first copy counts its category's points, each further copy of the same kind 60% of the one before, five copies at most. Each category counts up to its share of the target: art, greenery and lighting 30% each, floor decor 20%, audio 15%, furniture 15%, equipment 10%; so no single kind of thing fills it, and variety is needed for full marks. The target grows with the floor area: 12 points for a small shop, 1 more for each 12 m2 over 24, 90 at most. Each different kind of decor item over three adds a point, 8 at most.
- **Wear:** the finishes age: 3 thousandths a day, up to 4 more on a dirty floor, half as much while the cleaning service looks after the place. Refreshing them sets it back to nothing; redecorating (the game's interior score rising by 10 or more) halves it.
- Both scores lose up to 20% at full wear and up to 15% for dirt (cleanliness under 70).
- **Overall:** six tenths interior, four tenths decor.

**The district's standard:** 15 in the poorest district (income 60) up to 70 in the richest (140), rising with its income level, and never under the game's own minimum for it (Midtown's). A business gets its first 14 days to fit out: falling short of the standard costs it nothing then (customers, complaints), being over it counts.

**The strengths:** four sliders of the options, each from 0% (off) to 200% (double) of the figures below, 100% by default: the district standard (its customers either way and its complaints), the decor's customers (and an office's clients), what a nicer interior lets a shop charge (the price the district bears and the basket), and wear.

**The bound,** so that nothing of the feature's can cut an established business's takings sharply: at the defaults the interior can cost it at most 6% of its customers, and nothing of its prices or its baskets, which only ever gain. A complaint it brings (business pressure's own, 6% for 3 days) counts inside the same 6%: while any dip of business pressure's costs the shop customers, the shortfall gives way so that the two together never take more than the bound (`InteriorRules.GiveWay`); and under the default strength, where the bound is smaller than a complaint, the interior brings no complaints at all. 6% because it is what one complaint already costs, under a normal day's swing in the game's own hourly figures, and well under what good decor brings back (10%). At double strength the bound is 12%.

**Phased in:** every effect on customers and takings comes in over `RampDays` days (a seventh more each day) from the day these rules first read a business: for a save that meets them, from that day; for a business opened afterwards, from its own first day (`InteriorRules.Ramp`). Nothing changes a business's takings overnight.

**The effects,** each inside its cap, at the default strengths:

| Effect | What it does |
| --- | --- |
| standard | short of it: 1% fewer customers for each 4 points short (rounded down), 6% at most, and a complaint more likely (4 per mille a day for each point short from 10 short, 120 at most); over it: 1% more for each 8 points, 3% at most. Never no customers. |
| decor | 10% more customers at a decor score of 100 (20% at double). |
| footfall | the two together, -12% to +24% at the strongest: a part of business pressure's footfall of its own (`BusinessCatalogue.ClampInterior`), beside what the rest of the mod adds. While the business sells nothing (closed, nothing on its shelves, nobody on shift, its last 8 customers all turning its prices down) it brings nobody extra and adds nothing to a basket; a shortfall still costs. |
| prices | over the standard the district bears 1 thousandth more of the price for each point, 3% at most; under it nothing less. |
| basket | over the standard, each customer takes more of what the shop sells and has in stock: their order's products (never the door's entrance fee) times a share that rises from nothing at the standard to the district's most at an overall figure of 85: 10% where the standard is lowest, 6% where it is highest. The whole part for certain, the rest as a chance; an office's client one at most. As Meaningful Interiors does; the prices' strength sizes it. |
| brand | a shop of a brand counts up to 3 points more or less in its brand's standards. |
| standing | a shop's figure in the customers' reputation moves up to 2 points either way. |
| staff | comfort 0 to 5: plants (1, or 2 with three or more), good light (1), a rest corner (2): the calm level contented staff settle at goes up by the comfort, and a professional's morale heads 8 thousandths higher for each point. |
| offices | the best office of the player's at 60 or over adds 1 to the esteem candidates on the jobs board feel, 2 at 80; a firm's office over 50 weighs its bids up to 2% better. An office that takes clients is booked more of them an hour ahead: the decor's strength at its overall score (10% at 100 by default, 20% at double), never more than its staffed workstations left free. |
