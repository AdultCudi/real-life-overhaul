# Competition in niches

Phase 5 of the startups rework. It builds on [Founding a startup](startup-founding.md) (the niches) and [Startup money](startup-money.md) (the firm account): the city's startups sell into the niche their founding gave them, from the same buyers as the player's products, and they react to the player on the day. It runs with Startups and the tech firms' markets (the rivals live there); with either off nothing here runs and the markets behave as before.

## What the player sees

- **Niche page** (Tech app, Startups tab, Niches): a card "Who sells here" with each rival that sells into the niche: its price a month, its share of the niche's buyers and its money (a startup's cash, "Listed company" for a listed company that came in). A rival a listed company bought says who is behind it.
- **A founded firm's page** (go to market): "Coming at you", one short line a reaction, newest first, and "Bake offs": the open ones with three buttons to enter, and how the last few ended.
- **Desk session** "Bake off", at the owner's own desk, while a bake off is open for the firm. It is the same entry as the page's buttons.

## One pool of buyers for a niche

A niche has the buyers `NicheRules.Buyers` gives it. Everyone who sells there pulls in the same buyers: the player's products (`NicheRules.Market`, worked out for the sale in `TechDirector.NicheView` and for the product's page in `TechDirector.SectorMarketSection`, so the page says what the sale does), the city's startups bound to the niche, and listed companies that came in. A seller's customers move towards its share of the niche, its pull over everyone's (`ScaleRules.Share`), so together they never pass the niche's buyers. The listed companies sell in the whole market and keep their customers; the part of the market's pull that no niche owns is counted in a niche by the niche's share of the market's rivals, as before (`NichePool.Rest`).

A startup's niche is `NicheRules.OfStartup` (seeded; none for a game or a consumer product). Its appeal is weighed among the niche's buyers at the price the niche bears, its reach over those buyers (`RivalProduct.Frame`), and its customers move towards its view of the pool (`RivalProduct.View`). These are worked out each day in `SectorPulls` and never saved.

## Older saves

The pool fades in over `RivalryCatalogue.RampDays` (30) days from the day competition first ran on the save (`RivalryFile.Since`): on that day a startup is weighed among the market's buyers and pull exactly as it was. A firm is only noted on its first look: a raise or a lead already there starts nothing. Nothing a running firm has is taken away.

## The reactions

All of them run once a day, on the host, seeded with `TechRandom`.

| Reaction | Trigger | Effect |
|---|---|---|
| Price cut | A rival bound to a niche holds a lot less of the niche than it held lately (30 percent under its peak share, at most once in 30 days). The sector's own cut on lost customers no longer applies to it. | Its price tier drops a step; it goes back up after 90 days at 95 percent of its peak, never during a war chest. |
| Copying | The player's firm ships a feature (its best live product's features rose since the last look) that leaves a rival in the same niche and market behind. | The rival has the same features after a delay set by its team: the cost of the missing features (`FirmsRules.FeatureCost`) over 60 percent of its work points (`RivalryRules.CopyDelay`), between 7 and 90 days. A bought team's extra points shorten it. Not again within 14 days. |
| War chest | The player's firm raises at least 30 percent of the stage's usual round (`VentureRules.RoundSize`). | Each rival in the niche that has not raised for 90 days, runs, is below Growth and meets the round rules' bar may (60 percent a day) bring its next round forward. It starts raising so that `VentureRules.Day` closes it in 3 days: the lead, the value and the terms are the round rules' own (`VentureRules.Round` stamps the lead's terms), so phase 3's terms apply. When the round closes its money is spent over 60 days on reach (a quarter of it, never more than the average listed company spends) and its price drops to the lowest. |
| Hiring | A rival in the niche is funded: a startup past its seed with 120 days of money, one a listed company stands behind, or a listed company that came in. | `TechDirector.SectorPull` and `SectorPoacher` hand its pull (1500 for a funded startup, 1800 for a backed or listed one) and its name to `TalentRules.Approached`, so an approach on the firm's senior people is likelier and comes from it. A line every 30 days. |
| Sold to a listed company | A city startup with a rival product is sold (the sale in `VentureRules.Day`, or a deliberate buy below). | The buyer is a listed company that sells software in its market, seeded. The product goes on after the startup is gone, with more spend: half the buyer's marketing and a quarter of its team's points, and its price at the lowest for 90 days. The sale itself is the venture part's: the waterfall pays everyone. City startups only; the player's own firm being bought belongs to the exits part. |
| A listed company comes in | A niche has got big (at least 1.5 times the average niche of its kind of buyer) and has a startup with customers or a firm of the player's. | Daily 5 per mille, at most two per niche: a company that does not sell in the market starts a product there with the market's best features, no customers, and its own team, marketing and name. It is not settled at a market share. |
| A listed company buys | The same big niche, a rival startup at seed or later. | Daily 4 per mille: `VentureRules.Sell` at the greater of 3 to 9 years of its recurring revenue and half its value. It then goes on as above. |

## Bake offs

A big customer in a niche where one of the player's firms sells, one of the city's own businesses of the niche's kinds (from the game's list, never the player's), holds a bake off about once in 40 days, none while one is open. It signs for a number of sites (24 to 48 businesses, 3 to 6 enterprises) for six months at the niche's going price, and closes after ten days.

The score of a product is its appeal in the niche (features, soundness, price and the firm's name, `SectorRules.Appeal`). The player's gets Selling (up to 30 percent more), fit (plus or minus 25 percent around 500) and the tactic: a demo with Product sense (up to 20 percent), the relationships with Relationships (up to 20 percent), or a sharper price (15 percent, and the contract is a tenth smaller). A rival's is its appeal and a day's seeded roll of 10 percent either way. The city's startups, the listed companies that came in and the best listed company in the market take part by that rule, and a tie goes to the rival. A rival that wins signs the customer's sites. Entering grows Selling, and the demo or relationships grow their skills, through the study's own route.

A win books the contract as takings at the winning firm's address through the mod's booking path (`BakeGame.Takings`, the transaction `reallife_transaction_tech_bakeoff`, counted as takings in the game's statement). Nothing else in competition moves money.

## The exploit stays closed

A bake off's contract is the firm's own takings at its address, so the firm account counts it like any sale: its balance grows by it, and the investors' floor stays what they put in. Competition takes no money out of any firm, pays nothing into the wallet and reads no wallet; a war chest or a sale is the city startup's own round or sale. A bake off is entered once per firm and decided once.

## Going Public

The rivals are the host's. Competition runs on the host or a lone player only (a guest takes the host's rival products from the shared city, unchanged in shape); its state is a line of the player's own save, `reallife:tech_rivals`, which an older or absent RLO never reads. A guest's firms see the market as before and hold no bake offs.

## The save line

`reallife:tech_rivals`, version 1: parts h (the day first run, the next bake off number), n (the day each niche last held a bake off), r (what is kept of each rival product: entrant, niche, buyer, peak share, war chest, copy), s (where the player's firms stood at the last look), l (the reactions for the pages, 30 at most), b (bake offs) and e (who entered them). `reallife:tech_ventures` and `reallife:tech_sector` keep their fields.

## Not built

- A head to head in the landlords' niche: it has no business type to pick a customer from.
- A guest's own bake offs under Going Public (the rivals are the host's).
- A rival startup's own revenue is not tied to its product's customers (it was not before).

## Where the code is

`Scripts/Tech/Rivals/` (`RivalryCatalogue`, `RivalryModel`, `RivalryStore`, `NichePool`, `RivalryRules` with `.Reactions` and `.Bake`, `RivalryBridges`, `RivalryView`, `BakeGame`), `TechDirector.Rivalry.cs`, `TechDirector.BakeOff.cs`, `Wiring/Wiring.BakeLinks.cs`, `Ui/Tech/TechLive.Rivals.cs`, and small hooks in `TechDirector.Sector.cs`, `TechDirector.Founding.cs`, `TechDirector.Venture.cs` and `SectorRules.Rivals.cs`. Checks: `EconomyCheck --startup-rivals`.
