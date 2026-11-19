# Flats and studios

## In a sentence

A player buys one flat or studio of a residential building as a home, on cash or on the home mortgage, without buying the building: the unit is a long lease from the building's owner, it is the player's home the way a rented apartment is, with a monthly service charge and its share of the building's property tax in place of rent, and it can be let, sold, or folded into the building if the player later buys it whole.

A player asked for it: "having some more affordable housing to buy would be cool". The game sells whole buildings only, and the cheapest whole residential building (Build 3682's own figures) is $3.9 million at the start's prices.

## Where the code is

- Folder: [`Scripts/Economy/Estate`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Estate/), a part of the property business (`EstatePart.Flats`) with its own option, over the property part.
- Read first: `FlatCatalogue.cs` (every number), `FlatRules.cs` (every rule, game-free, run by EconomyCheck's `FlatChecks.cs`), then `EstateDirector.Flats.cs` (the listing, buying, the day, letting, selling and folding) and `EstateGame.Flats.cs` (the only file that touches the game for flats).

```
FlatCatalogue        the numbers
FlatRules            game-free: the kinds, a building split over its units by floor, the lease's price, the listing's draw, the bills, the fold's credit, the refusals
FlatState            a building as the rule sees it, a unit listed, a flat owned (with the one-unit holding its letting and sale run on), the book of them
FlatStore            the save's line, "reallife:estate_flats"
EstateGame.Flats     the residential buildings the player does not own or rent, the building's tax, the building's registration rented at no rent, the market's rent at a sale
EstateDirector.Flats the listing, BuyFlat, the day (value, the month's two lines, letting, a sale), FoldFlats, the player's calls, the test tools
CreditDesk.Flats     the mortgage on a flat: what the bank would lend, the account, a sale settled out of the proceeds, a fold moving it to the building
Economy/Estate/EstateApp.Flats   the Property app in the game's frame: the Market tab's rows and a unit's page, the Mine tab's rows and a flat's page
Ui/Estate/EstateLive.Flats        the same on the kit
```

Hooks in other files, each a line or a few: `EstateDirector.cs` (the part, the line read and written), `EstateState.cs` (the book), `EstateGame.cs` (a building's floor and tax read once), `EstateApp.cs`, `EstateApp.Property.cs`, `EstateLive.Market.cs`, `EstateLive.Mine.cs` (the rows and pages), `EstateTables.cs` (a kind for the two filters), `CreditDesk.cs` (a flat as a mortgage's security), `Finance/DayRules.cs` (the capital lines), `Wiring/EstateLinks.cs`, `Meta/FeatureList.Economy.cs`, `Meta/OptionsTree.cs`, `Meta/FeatureCities.cs`.

## What the game does

- A residential building has a floor (`Building.totalSqm`) and a unit size (`BuildingSizeData.squareMeters`: 54, 96, 204 or 589 m2); its value is the floor times the district's square metre price (`Building.GetMarketValue`). The property business already counts its units (`EstateRules.UnitCount`: floor over unit size, 1 to 12) and lets them one by one.
- The player's home is a building registration the player rents (`RentedByPlayer`, `RentPerDay`): the game's own `BuildingHelper.RentBuilding(building, 0, 0)` is how its console rents one at no rent, and every part of the mod that looks for a home (the household, the utilities, sleep, the home's needs) reads such a registration. A building the player owns or rents is not a flat's.
- The game charges a building it counts as owned a property tax for the year (`RealEstate.TaxesAmount`: floor times the type's and the district's multipliers times 3.5), in the tax bill. A flat is not in the game's list of owned buildings, so its share of that tax is the mod's line.

## What the feature does

### The market

- **Which buildings.** Every residential building of the city that is not a Hamptons house, is not the player's (owned or rented) and has at least 3 units (`MinUnits`), in every district the game has, and in a district the mod adds (Uptown, Cedar Heights) as soon as its buildings are among the city's. In a Going Public session none is listed.
- **How a building is split.** Its units are four kinds, by a draw of the building's own (never the save's): a studio, a one, a two and a three bedroom flat, weighing 3, 5, 7 and 10 of its floor. A middling district's units are 35% studios; each 5 points of income under (over) the middle adds (takes) a tenth of a per cent, between 15% and 55%. The building's value is shared out by those weights, to the dollar (`FlatRules.Freehold`, largest remainder): the units' shares add up to the building's value, and a studio is always the smallest share.
- **What a unit costs.** A flat is a long lease, not the freehold: its price is **15%** (`LeaseShare`) of the unit's share, named in tens. The owner keeps the building, which is why a service charge goes to them. This is the one figure that makes a unit's price a small part of a building's: by floor alone the cheapest studio of the game's cheapest building would be $153,000.
- **Which units are for sale.** The draw is made every 20 days (`ListWindow`): each unit is for sale with a chance of 13% in a middling district at a good condition, 12 points more in a building run down (condition 600 or under), half a point more for each 5 points of income under the middle (less over it), between 4% and 40%; at most 3 units of one building at once. The draw is the building's, the unit's, the window's and the save's seed alone, so it is the same all through a window and in every look at it, and a unit the player holds is never listed.
- **Where it shows.** The Market tab beside the buildings and hotels, in the districts filter and a kind of its own ("Flats and studios"), on the game's frame and on the kit; its page gives the price, the floor, the share of the building, what it costs to keep, and what the mortgage would be. A flat's row is in no total of the tables' buildings.

### The prices the rule gives (Build 3682's own buildings, the start's prices, the market at its long-run level)

The lease's price of a unit, by the district's own residential buildings with 3 units or more. "Studio" is the cheapest kind; the range is between a building kept as new and one run down to a condition of 300 (a building's value keeps 79% of itself there). The deposit is 20% (`HomeDepositLeast`).

| District | Buildings | Cheapest studio | Median studio | Dearest unit | Deposit on the cheapest |
| --- | --- | --- | --- | --- | --- |
| Garment District | 79 | $25,520 to $32,300 | $70,500 to $89,200 | $2.0 to $2.6 million | $5,104 to $6,460 |
| Industry City | 71 | $28,110 to $35,590 | $48,500 to $61,400 | $2.0 to $2.5 million | $5,622 to $7,118 |
| Lower Manhattan | 45 | $35,010 to $44,310 | $144,600 to $183,100 | $3.8 to $4.8 million | $7,002 to $8,862 |
| Hell's Kitchen | 84 | $59,800 to $75,690 | $172,100 to $217,800 | $12.3 to $15.5 million | $11,960 to $15,138 |
| Murray Hill | 33 | $91,890 to $116,310 | $443,500 to $561,400 | $8.0 to $10.2 million | $18,378 to $23,262 |
| Midtown | 23 | $454,380 to $575,170 | $2.1 to $2.7 million | $52.7 to $66.7 million | $90,876 to $115,034 |

The Hamptons' houses are one household each and are not split. A new district's buildings are priced by the same rule from their own floor and value.

**The first deposit.** The cheapest studio is $25,520 (a run-down building in the Garment District), so a first deposit is $5,104: 4.6 weeks of an early gig of $1,100 a week (the least a player reports), 3.4 weeks at $1,500. Nothing special-cases a first purchase: it is the smallest share of the cheapest district's smallest building, at the lease's share, at the mortgage's 20%. A mortgage on it is over the bank's least ($5,000 at the start's prices).

### Buying

- **Refused before any money** (`FlatRules.MayBuy`, in this order): the part off, a Going Public session, a unit no longer listed, six flats held already (`HeldMost`), a price that is not the price now (the question box states the price, and a price that moved is refused with nothing taken), cash under the deposit, a mortgage the bank will not give. Then the player is asked in the game's question box.
- **Cash** takes the whole price. **A mortgage** asks a deposit of 20% to 50% and a term of 360 to 1,440 days, stepped on the page, the same terms, rate and limits as a building's (`CreditDesk.QuoteFlat`: the house bank's offer, the least and the most it lends); the deposit is the price less what the bank puts in, the account is secured by the flat's key, and it shows in the Finance app with the home loans.
- **The home.** The building's registration is rented by the player at no rent through the game's own `RentBuilding`; with nothing to rent, the deposit goes back and the player is told.
- **Bank lines**, each its own key with its label: the purchase (`reallife_transaction_flat_buy`), the sale (`..._sold`), the fold (`..._fold`), the service charge (`..._service`), the property tax (`..._tax`) and a tenant's rent (`..._rent`). The purchase, the sale and the fold are capital in the day's result; the others count.

### Owning

- **Its value** is the building's value now by the existing market (`EstateGame.Value`: the market, prices, the district, the condition) shared by the unit's weight at the lease's share, written each morning. With the property part off it stands still at the last value written.
- **A month's two lines**, taken once a month on the calendar's month turning (`BillDue`: a month after the one last billed; a month of a long gap or of time away is one bill, never back-bills; the month of the purchase is not billed): the **service charge**, 20% of the rent the flat would let for in the month (`ServiceShare`), to the building's owner (a rival or the city's landlord, named on the line: the game keeps no purse for a landlord, so the money goes to nobody, as a parking space's price does), and the unit's **share of the building's property tax**, the game's own yearly figure for the whole building over the unit's floor share over the year's twelve months, at least $1.
- **Where it shows.** The Mine tab, a row of its own (worth, paid, rent coming in, the year's rent and costs) and a page with its figures, its bills, its letting and its sale, on both interfaces.
- **Letting.** **Let it to a tenant** puts the flat's one unit through the same letting rules as a building's units (`EstateRules.Apply`, `Accept`, `EstateDay.Unit`: applicants, a lease, rent a day, arrears, notice, the court), on the flat's own value, so its rent is the market's rent of that value and a small part of the building's. The rent comes in as `flat_rent` each day it is paid. Nothing of a landlord's duties (repair calls, inspections, managers) is applied to a flat. The player should not use a flat as a home while it is let.
- **Selling.** The same rule as a building's listing: an asking price at a share of the value (70% to 150%), an offer by `EstateRules.SaleOffer`, accepted to complete in 5 days, a sale that may fall through, the agent's fee (`AgentFee`, 1.5%, $500 at least) and the tax on a gain (`GainsTax`) as the lines a building's sale has, the estate agency's own commission hook left where the listing code has it. A mortgage on the flat is paid out of the proceeds first (`SettleFlatSale`). A player still living in it pays the market's rent for it from then on (`BuildingHelper`'s market rent, as the game leaves a unit the player still uses when it sells a building); nothing inside is taken.
- **If the player buys the whole building** (seen at a start, at the day, and when the app opens): each flat of it folds in. The unit's share of the price paid, at the lease's share (`FoldCredit`), is credited as `flat_fold` so the unit is not paid for twice; what the flat cost over that is added to the building's works against a gain (`FoldBasis`, never under nothing); a mortgage on the flat is secured by the building from then on; a tenant's lease ends. The registration is the player's home in the building the game lets the rest of.
- **If the building's owner changes** (a rival sells or buys it), nothing happens to the flat: it is keyed by the building and the unit, the service charge goes by the owner at the month's turn, the home is the player's registration.

### The save

One line, `reallife:estate_flats`, version 1: the flats, each with its building, unit, kind, the units it was drawn from, what was paid and when, the last value, the month last billed, the one-unit holding's condition, base, whether the player lets it, its sale (asking, offer, day, price, whether it falls through), its unit's lease (asking, tenant, record, rent, deposit, until, arrears, notice, court) and its applicants. A save from before has no line, and no flats. A line from a newer version is read as far as it goes; one this version cannot read is kept as it was read and written back unchanged while no flat is held. With the property part off the line is read and written as it stands.

### With the option off

**Flats and studios to buy** (`economy_estate_flats`, on), under Property, needs the property option. Off: nothing is listed, bought, billed, let or sold; a flat the player holds stays their home (the game's registration is the game's own), keeps its last value, and is shown on the Mine tab; its mortgage is left as it is. On again, everything resumes from the month.

### Going Public

A unit is the buying player's only, and nothing is sent over the network. In a session no building is read for sale, none is made a home, no sale completes and no mortgage on a flat is judged: the player's own flats stay theirs and are settled on the first day played alone. The draw never reads another player's building: a building another player rents is a registration `RentedByPlayer` on this machine too, and is left out by the same test as the player's own.

## Constants and decisions

| Figure | Value | Why |
| --- | --- | --- |
| Kinds and weights | studio 3, one 5, two 7, three 10 | a studio the smallest share; areas rise by a half and a half again |
| Studios in a middling district | 35%, 15% to 55% | poorer districts' units are smaller |
| Lease's share (`LeaseShare`) | 15% of the unit's share | the owner keeps the building; the only figure between a unit's share and its price, and what puts a first deposit within five weeks (the owner's word: no special case) |
| Minimum units | 3 | two units are a landlord's pair |
| Window, chance, most listed | 20 days, 13%, 3 | enough choice, not a flood; turnover every window |
| Held most | 6 | the page and the save stay small |
| Service charge | 20% of the flat's market rent | well under the rent a let flat brings |
| Property tax | the game's yearly figure by floor share, a twelfth a month | the game's own tax, which a flat is not in |
| Mortgage | the home mortgage's: 20% to 50% down, 360 to 1,440 days | the same rules as a building's |
| Sale | the listing rule's | the owner's word |
| Rounding | tens | prices are named |

Decisions where the player's words left it open: a flat is sold as a lease, so that a studio can cost a small part of a building by the rule and not by a special case; the buildings the player already rents (a home with rent) are not listed, so buying the flat one lives in is not offered; the service charge and the tax go to nobody, as a parking space's price does; a flat sold while lived in leaves the player renting it at the market's rent, taking nothing from inside; a landlord's duties do not apply to a flat; with the property part off the bills stop with the value.

## Cost

Nothing per frame and no Update. The listing is drawn once a window, when the app opens or after a purchase: one walk of the city's buildings and one of the save's registrations (about 400 residential buildings, 12 rolls each). A flat's day is a few comparisons.

## Tests

- Options, Test tools, under Property: **Test: buy the cheapest flat for sale** (cash, a real purchase through `BuyFlat`) and **Test: take this month's flat charges** (each flat once for the month).
- `EconomyCheck --flats` (`FlatChecks.cs`): the shares adding up to the building to the dollar, a studio the smallest, the lease's price, the cheapest studio and its deposit in the game's own figures, the deposit and the mortgage's figures, a value that moves with the building's, the listing (never over the most, the same in a window, none under the least, none held, more where run down), the bills once a month and after a gap, a fold not paying twice, a sale and no profit from buying and selling, a let flat, every refusal, the save (a round trip, an old save, a damaged line kept, a newer line, a cut record, a figure out of range), the sources and the texts.

## Not seen in the game

Everything above is built and checked offline. Not seen: the rows and pages on both interfaces, the game's question box before the money, the registration rented at no rent being a home in the game's own screens, the mortgage in the Finance app, the month's two lines in the bank, a sale completing, and a fold when a building is bought.
