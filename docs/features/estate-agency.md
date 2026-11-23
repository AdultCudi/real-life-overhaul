# The estate agency's book of blocks

## In a sentence

An estate agency the player opens at an office he holds, with customer service staff on its rota, can now manage other owners' blocks of flats for a share of the rent: landlords of the city and the rivals who own flats call for agencies to pitch, rival agencies pitch against the player by a rule fixed by the save, and the player's own buildings can be put under the agency so a property manager's work is done without a fee.

A player asked for it ("start a real estate agency to start competing against the rivals that own tons of real estate"). The agency itself, with its instructions and commissions, was there before; this is the part that makes it a business to run and to compete in.

## Where the code is

Folder [`Scripts/Economy/Estate`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Estate/), namespace `RealLifeOverhaul.Economy`.

| File | What it is |
| --- | --- |
| `AgencyBook` | the model: `ManagedBlock` (a call for pitches, a block held, a block lost), `AgencyBid` (a rival agency's pitch), `AgencyBook` (the blocks and the month's figures), `AgencyLandlord`, `BlockEvent`: game-free |
| `AgencyBookCatalogue` | every figure of it: game-free |
| `AgencyBookRules` | the calls for pitches, the rival agencies, the owner's choice, the hours, a block's day, the month: game-free and seeded |
| `AgencyBookStore` | the save's line `reallife:estate_agency_book`: game-free |
| `AgencyText` | the words for an owner, a rival agency, a block and why one was lost |
| `EstateDirector.Book` | the book in the director: the day, the events paid and told, the player's pitches; `.BookTests` the two test tools |
| `EstateApp.Agency` | the Agency tab and the block's page on the old interface |
| `Ui/Estate/EstateLive.Market` | the same on the kit |

Changed beside them: `EstateGame` (the negotiators' skill is read by their hours; the two fee keys), `AgencyRules.Day` (the hours the blocks and the buildings under the agency take are not the instructions'), `EstateRules.Duties` (`Runs` and `Handles`, so the player's own agency is run as a property manager is), `BizLines` (the fee keys are takings of the business), `EstateGuide` (the facts the Guide's page prints).

## Who the negotiators are

The agency's staff are the employees on the rota of its office with the customer service skill, as for the instructions. The game has no estate agent, so no new profession was made: the staff are negotiators, as the instructions' pages have always said (the mod's style rules keep the word agent out of its texts). Their hours a day are `AgencyRules.Capacity` of their weekly hours and their skill (the skill weighted by mood), and the skill the owners judge a pitch by is the mean of theirs weighted by their hours (`AgencyFile.Skill`).

## Calls for pitches

A running agency (an office the player holds) is called on by an owner every `OfferEveryDays` (5) days at most while fewer than `OffersMost` (3) calls stand and fewer than `BlocksMost` (6) blocks are held. One owner never asks twice for the same district at once. A call has 4 to 24 flats of 40 to 110 square metres in a district the agency knows, the rent the district's price and the market's level give (a year's yield, `EstateCatalogue.AgencyYield`, over the game's year), the fee the owner asks (7% to 11% of the rent collected, either side of a property manager's 10%) and the satisfaction they expect of the tenants (fair 45%, good 52%, strict 58%). It stands `OfferOpenDays` (6) days.

The owner is a rival of the save who is a landlord (the day's read of the city's rents, `RentMapData.Landlords`) on 500 in a thousand of the calls when there is one, otherwise one of six landlords of the city with names of the mod's (`reallife_estate_owner_0` to `_5`). Draws are `EstateRules.Roll` of the save's seed.

## Rival agencies

A rival of the save who is a landlord runs an agency on a draw of the save's seed and the rival's id (500 in a thousand). Its negotiators' skill is 40 plus a point for every 8 units the rival holds, to 90. For a call, each rival agency that is not the owner pitches on a draw (600 in a thousand), at 85% to 105% of the asking fee, at most three of them. They are taken in the order of their ids, so the game's order of listing them changes nothing, and the pitches are made when the call is drawn and kept in the save.

A pitch is weighed as `skill x 2 + reputation / 20 - fee%`, the reputation being the player's in the call's district. The player pitches at the asking fee or undercuts it at 80% (twenty points). On the last day the best pitch takes the block; a tie is the player's. A call nobody pitched for goes to the best rival pitch, or to nobody: that is no loss of the agency's. A pitch lost lowers the district's reputation by 5, a block won raises it by 15.

A pitch needs negotiators at the office and enough spare hours (the hours of the pitches already made are not spare again), and is refused with a notice that says which.

## A block held

A block is a few figures of the mod's, not a building of the city (the game keeps no tenants for a building that is not the player's, as it keeps no dirt for another business's floor in the cleaning contracts): flats let, the tenants' satisfaction, the building's condition, repair calls, strikes and the month's rent collected. The agency's work moves them by the property business's own measures.

- **Hours.** A flat takes a tenth of a negotiator's hour a day (`HoursPerUnit`), and so does a unit of the player's own buildings under the agency. Every morning the hours are shared out: what the negotiators' hours cover of what is needed, in thousandths, is how well each block is served. With under 50% covered, the player's own buildings wait and the blocks have the hours.
- **Tenants.** Satisfaction drifts to a target by `EstateRules.Drift`: the property business's base, the building's condition as `HappyTarget` reads it, less the repair calls waiting (`HappyCall`, more for each day past the grace), less up to 200 points when the negotiators cover none of the hours.
- **Calls.** A let flat reports a fault on `EstateRules.CallChance` of the building's condition. An open call is answered each day on 700 in a thousand times what the negotiators cover; one left past `CallLapseDays` does damage (`CallLapseLoss`).
- **Leases.** A let flat's lease ends on 8 in a thousand of the days, and the tenant renews on `EstateRules.RenewChance` of their satisfaction, or goes. An empty flat is let again on `ReletChance` of a day (the market's cycle, the hours covered and the lift of a managed building, `ManagedApplicants`), and the agency is paid `AgentLetDays` (7) days of rent for each.
- **Condition.** Up a point a day with the hours covered and no call left past the inspector's days, down a point with under half of them covered.
- **The city's inspection.** Every `InspectEvery` days: the condition less 60 for each call older than five days, against `InspectPass`. A failure is a strike, a fine the agency pays (`EstateRules.InspectFine`, booked at the office under the city fine's key) and a new look within `ReinspectDays`; three in a row end the block.

## The month

When the market's month turns (`EstateState.Months`) each held block pays its fee: the owner's share of the rent collected in the month, at the fee asked and the pitch made. A month in which the tenants' satisfaction, averaged over its days, was under the owner's standard costs half of the fee, which is held back (and tells); three such months in a row end the block, and a month kept starts the count again. A kept month raises the district's reputation by 10, a missed one lowers it by 20, a block lost by 40, a block handed back (`Hand it back`, free) by 10.

`AgencyBookRules.MonthEnd` does a month once: it refuses a month the book has already done (`AgencyBook.Month`, saved), so no fee is paid twice, in a long play or across a save and a load. EconomyCheck runs twelve seeds through four hundred days with and without a save and a load every day and wants the same sum of fees both ways, equal to what the figures call for.

## Money

All of it is through the mod's own transaction keys, each with a `_label` text:

| Key | What | Booked |
| --- | --- | --- |
| `reallife_transaction_estate_agency_fee` | a block's month's fee | in, at the agency's office, the takings of the business |
| `reallife_transaction_estate_agency_letting` | the fee for flats let again | in, at the agency's office, the takings of the business |
| `reallife_transaction_estate_fine` | the city's fine after a block failed its inspection | out, taken whatever the cash |

Both fee keys are in `BizLines`' takings (the key prefix `estate_agency_` is one of its domain), so the game's daily statement, BizMan and the Company app's books count them once with the commission the agency always had. Nothing is paid for a call lost, a pitch, a block handed back or a month's fee withheld.

## The player's own buildings

On a building's page (the `Mine` tab) who runs it has a fourth choice once the agency is open: `Your own agency` (`EstateManager.Agency`). While its negotiators cover the hours (`EstateHolding.Covered`, worked out every morning, never saved) it is run as a property manager runs it (`EstateRules.Runs`: a contractor to every call, a reminder, a plan, notice, the court, the builders before the city's order), and applicants come as for a manager. It charges no fee and moves no money, because it is the player's own either way; the saving against a manager's 10% is what the negotiators' hours buy. When the negotiators are short, the building is the player's to run for the day. Shutting the agency, or losing its office, sets every such building back to the player.

## The pages

The Agency tab lists the blocks beside the instructions on both interfaces: a row for each call, block held and block lost, filtered by kind (`Managing`) and by stage, with the rent roll a month, the fee to come, the hours a day and the day. A call has its page (the fee asked and the month's at today's rents, the fee if undercut, the hours it needs against those spare, how many rival agencies pitch, what the pitch is weighed on, `Pitch at their fee`, `Undercut their fee`, `Take the pitch back`); a block held shows its flats let, the tenants' satisfaction against the owner's standard, the condition, the calls, the next inspection and its strikes, the hours covered, the month's rent and fee so far, the fees earned and `Hand it back`; a block lost says why. The summary says what the book holds, the hours taken against the negotiators', the negotiators' skill, the player's own units under the agency, the month's fees to come, last month's, the letting fees and the pitches won and lost. The kit adds two figures to the strip and shows the first call unanswered over the table.

## The save

`reallife:estate_agency_book`, version 1, beside the property business's other lines: `v=1^h:<book>^b:<block>;<block>`. The head is the next number, the last call's day, the month's letting fees, last month's fees, withheld fees and letting fees, the pitches won and lost and the month last settled; a block is its number, owner and district (escaped), then its stage, why, flats, area, rent, fee, pitch, standard, day, pitched, let, satisfaction, condition, strikes, next inspection, misses, rent collected, satisfaction summed and its days, fees, earned, months, withheld, lettings, then its open calls' days and the rival pitches (rival, per cent, skill, divided by '.'), then the rival the block went to. A save from before has no line; a line that cannot be read is read past; every figure is put inside its bounds on reading; a later version adds fields at a record's end. A building under the agency is `Manager` 3 in the property line (an older RLO reads it as a property manager).

## Going Public

The property business's own gating holds: nothing of the book touches another player's building, the draws are of the save's seed and the host's rivals, and no money is paid but to the player.

## Tests

Options, Tests: `An owner's call for the agency to pitch` and `The agency's month: management fees` (the month's turn now: each held block pays its fee, once; the month is counted as done beyond the last). Each opens the Agency tab on what it made.

Offline: EconomyCheck `--estate-agency` (and in the full run), `EstateAgencyBookChecks.cs`: the fee and letting fee rules, the calls (bounds, determinism, no repeats, the owners), the rival agencies (the same whatever order the rivals are listed in, bounds, about half), a pitch refused for want of negotiators, hours or room, the owner's choice and every outcome told once, the hours, a block's day, the inspection, the month paid once, a year of the book with and without a save and a load every day, the line in the save, the player's own buildings and who charges, the books' lists and the texts.
