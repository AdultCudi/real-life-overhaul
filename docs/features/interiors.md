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
