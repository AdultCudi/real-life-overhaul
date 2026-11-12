# Loyalty cards

## In a sentence

A loyalty scheme for each of the player's shops, switched on from a Loyalty tab in BizMan: customers served well become members, come back more often and spend a little more, the player gives back a share of what members spend, and other owners' shops in the district run cards by the same rules.

The owner asked for it: "loyalty cards for the player's shops".

## Where the code is

- Folder: [`Scripts/Loyalty`](../../Assets/Mods/RealLifeOverhaul/Scripts/Loyalty/).
- Read first: `LoyaltyRules.cs` (every number and every rule, game-free, run by EconomyCheck's `LoyaltyChecks.cs`), then `LoyaltyDirector.cs` and `LoyaltyDirector.Day.cs` (the state, the day and the hour) and `LoyaltyGame.cs` (the only file that touches the game).

| File | What it is |
| --- | --- |
| `LoyaltyRules` | game-free: the tables of the reward rate and the sign-up push, a day of a scheme (`Step`, `Day`), the day's turn (`Turn`), the lift and the basket, the costs, the break even, the other owners' shops (`RivalRuns`, `RivalStrength`, `Rivalry`) |
| `LoyaltyState` | a scheme as the save keeps it (`LoyaltyFile`), a day of its week, a rival shop as the rules see it, what a day reads and comes to |
| `LoyaltyStore` | the save's one line, `reallife:loyalty` |
| `LoyaltyGame` | the player's businesses, yesterday from the game's order history and satisfaction, an hour that could serve nobody, the other owners' shops of a kind in a district, a product more in a basket, money through `BizBooks.Book`; `LoyaltyBridges` |
| `LoyaltyDirector` | the one component: a second's look, the save, the presses (`Start`, `End`, `NextRate`, `NextPush`, `NextOffer`), `FootfallOf` |
| `LoyaltyDirector.Day` | the game's day (`Pass`: yesterday lived and booked, the turn, today's lift written) and the game's hour (the hours counted, the baskets) |
| `LoyaltyDirector.Tests` | Options, Tests |
| `LoyaltyTab`, `LoyaltyTab.Pages` | the Loyalty tab in BizMan: a copy of the game's own tab button and two of BizMan's scroll views, as the Interior tab's; its lines are also what the RLO interface draws |
| `LoyaltyText`, `LoyaltyGuide` | its words, its options block and its feature entry (Guide figures from the rules) |

Hooks in other files, each a line or a few: `Wiring/Wiring.LoyaltyLinks.cs` and `Wiring/Wiring.cs`, `Core/Settings.cs`, `Core/RealLifeOverhaulMod.cs`, `Core/FeatureOwners.cs`, `Core/GameContract.cs` (the tab's part of BizMan's members), `Meta/FeatureList.cs`, `Meta/FeatureCities.cs`, `Meta/OptionsTree.cs`, `BizPace/BizPaceGame.cs` (the tab's handlers warmed), and on the RLO interface `Ui/BizManRules.cs`, `Ui/BizMan/BizManScreen.Interior.cs`, `Ui/BizMan/BizManScreen.Page.cs`, `Ui/UiScreens.BizMan.cs`, `Ui/UiScreens.BizOps.cs` and `Ui/Sweep/SweepLedger.cs`.

## What was there before

- **The customers of a business.** The game draws a day's customers from one figure, a business's promotion (`BuildingRegistration.promotion.total`: the street's traffic and the game's own marketing). The mod has one writer of it, `BusinessGame.Footfall`, which recomputes the game's figure and applies three parts, each inside its own bounds (`BusinessCatalogue.PromotionFor`): a trouble's own change, the shared part (`BusinessGame.Ambient`: the city's economy, the company's staff and campaigns, the brands, Flare, the city's faults, the rival tycoons and Standing, each wrapped on the one before in `Scripts/Wiring`) and the interior's part. `Core/CustomerDraw` is the one way the day's customers are drawn again after a write.
- **Fuller baskets.** Interiors and decor adds products to customers' orders over the district's standard (`InteriorGame.Basket`).
- **Satisfaction.** The game's own, by business: service, prices accepted, cleanliness, facilities (`BuildingRegistration.satisfaction`), and a day's customers, takings and goods' cost in its order history.
- **BizMan.** The Interior tab is the mod's one tab there, a page written as lines.
- **Brands.** `Economy/Brands` groups the player's shops under a brand.
- **Nothing of loyalty.** No members, cards or repeat customers were modelled anywhere in the mod or the game.

The scheme adds no second way to customers: its lift is one more addend of the shared part, wrapped in `Wiring/Wiring.LoyaltyLinks.cs` after the rivals' pairing and before Standing's, so the bounds business pressure keeps on that part (15% more at most, with all the others) hold with it, and the interior's part, Meaningful Interiors' and Meaningful Ambience's own work and the living population's reading of the levers are untouched.

## The rules

Every figure is in `LoyaltyRules`; the Guide prints its figures from there. Members and a shop's average day are kept in hundredths, so a small shop's scheme still grows; everything is whole numbers and nothing is random but a hash of a place, a day and an hour, so every machine works out the same schemes.

- **The customer base.** A shop's regular customers are five days of its customers (the average day is taken over seven). At most 60% of them ever carry the card (`PoolMost`), a little more where the brand's other shops take it (6% more each, 24% at most).
- **Joining** (`Join`). Each day 4% of the customers served without a card take one at the second reward rate, with pleased customers. Satisfaction counts from 40 (nobody) to 80 (in full). The rate's appeal, the sign-up push, a members' day and rival cards scale it.
- **Falling away** (`Lapse`). 1.5% of members a day at the second rate; up to 9% more by the share of the day's opening hours that could serve nobody (closed by its switch, nothing on the shelves, nobody at a station that needs somebody: counted each game hour), up to 5% more under a cleanliness of 60, up to 7% more when customers accept fewer than 70% of the prices; 25% a day at the very worst. A day of rest by the shop's own hours costs nothing. A scheme that has ended loses 15% of its members a day.
- **What members do.** Members come back more often (5.5%, 9%, 11.2% or 13.3% more by the rate), so with a share `p` of the base carrying the card the shop draws `p` times that more customers: 8% at most on an ordinary day at the top rate with a full base, 12% on a members' day (`LiftMost`). The game's figure takes whole per cents, so the tenths are given as whole per cents on that many days in ten. Some of members' visits take one product more (2% to 5.5% of them), added to the coming hour's orders as the game adds a product.
- **The reward rate:** 1%, 2%, 3% or 5% of what members spend, booked each morning for the day before. Each step brings fewer visits and sign-ups a point than the one before and costs as much again.
- **The sign-up push:** cards by the till (nothing), staff asking every customer ($0.50 a new member, half as many sign-ups again), a welcome bonus ($1.50, a little over twice the sign-ups).
- **The members' day:** one day a week with double rewards. Members move their visits to it (half as many extra visits again on the day, 8% fewer on the others), 15% more customers join and a fifth fewer fall away. It needs seven days' notice, and the old day ends when a new one is announced.
- **The costs.** Set-up $1,500 once ($600 for a shop joining its brand's running scheme), $12 a day while it runs, $300 to print a new rate, all at the city's prices; the rewards, never over 10% of a day's takings. All are marketing costs at the business (`BizBooks.Book` with the game's marketing category and a deduction for the game's tax), dated the day they belong to, and that day's statement is brought up to date (`GameBooks.Refresh`).
- **No setting is best.** `BreakEven` is the margin on its goods from which a rate's extra visits alone pay for its rewards with a full base: about 19%, 24%, 30% and 43% by the rate, more with a members' day or rival cards. The page shows it beside the shop's own margin (from the game's order history) and says in red when the choice gives back more than the cards bring. At or over it a settled scheme never gives back more than the margin it brings (the fuller baskets come on top); under it the loss is the player's own choice.
- **Nothing to play against the clock.** The switch, a new rate and a members' day come into force at the day's turn (`Turn`), so a day's lift and the rewards paid for it are always of one setting. A cut in the rate loses 8% of the members a step at once. A scheme with no cash behind it (the cash under nothing in the morning) stops, as the game stops a campaign it cannot pay.
- **A brand's shops** share one card's terms: a press at one applies to all of the brand's shops that run it, and a shop joining takes the brand's terms.

## The other owners' shops

Whatever the player can do, other owners do by the same rules (`LoyaltyRules.RivalRuns`, `RivalStrength`). For each of the player's schemes, the shops of the same kind in the same district that are not a player's are read once a day (`LoyaltyGame.Rivals`): the city's own shops and the game's rivals' businesses, by the skill of the crew the game holds for each (the game keeps a satisfaction only for the player's shops, so the average main skill of a shop's people, less anyone the player poached, stands for how well it serves) and the days it has traded, and the rival tycoons' outlets that stand on no business of the game's (`LoyaltyBridges.Outlets`, from `Rivals/RivalFirms`; in a Going Public session the tycoons rest). A shop runs a card once it has traded 14 days with a crew whose skill is 55 or more, and then only six in ten of the rivals' and a third of the city's, by a seed from its address; its rate and push come from the seed, and its members are the days since, lived by the very `Step` the player's scheme lives by. The strength of every such card in the district (the share of its own base carrying it) lowers the player's lift and sign-ups: with a strength of 100% of a base in all the lift is halved, and never less than 40% of it is left. The other owners' books are not changed: a card's cost and gain are theirs.

## The option

**Loyalty cards for your shops** (`loyalty`, on). Off: no scheme runs, costs or brings anything, every figure is written again without its part, the tab is gone and the schemes wait in the save.

## Going Public

Only the player's own businesses (`GoingPublic.Mine`, through `BusinessGame.IsBusiness`) run a scheme, on the player's own money; another player's shop is never read as a rival (`GoingPublic.Foreign`). Nothing is sent. Each machine draws only its own businesses' customers, and every figure of a scheme is worked out from the game's own books in whole numbers.

## The save

One line, `reallife:loyalty`, version 1: each scheme (the place, the switch as it is and as chosen, the terms in force and chosen, the members and the average day, the day's counts) and the days of its week. A scheme of a business that is no longer the player's, or one long over, is not kept.

## Cost

Nothing per frame and no Update: a few comparisons a second. At the game's day, one walk of the save's businesses and, for each running scheme, one more for the other owners' shops of its kind; at the game's hour, a walk of the schemes and of the coming hour's customers of each. The tab costs one flag test a frame while BizMan shows a business.

## Tests

- Options, Test tools, each writing `Loyalty check (...)` lines: **write every scheme to the log**, **live seven days at once at the first scheme** (no money moves), **read the rival shops' cards now**.
- With Developer diagnostics on, each day's turn writes `Loyalty check (day N): ...: PASS`.
- `EconomyCheck --loyalty` (`LoyaltyChecks.cs`): the tables and their diminishing returns, growth, decay, the bounded lift and its place in the one figure, the costs, the break even at every rate, push and members' day over settled runs, the presses against the turn, the other owners' shops, the save, the sources and the texts.

## Not seen in the game

Everything above is built and checked offline. Not seen in play: the tab's button and its two columns in BizMan and on the RLO interface, the question box before the set-up, the lift in a day's customers, the products added to baskets, and the lines in the business's statement.
