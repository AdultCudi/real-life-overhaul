# A landlord's duties

## In a sentence

A building the player lets himself through the Property app now has tenants with a satisfaction of their own, repair calls the player answers with a contractor or by doing the job at the building's door, complaints and tenants who leave, the city's inspections with fines and an order to put things right, a reminder and a payment plan for rent owed, and a lettings agency or a property manager who runs it for a fee.

## Where the code is

- Folder: [`Scripts/Economy/Estate`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Estate/), a part of the property business (`EstatePart.Duties`) with its own option.
- Read first: `EstateRules.Duties.cs` (every rule, game-free, checked by EconomyCheck's `EstateDutyChecks.cs`), the figures at the end of `EstateCatalogue.cs`, `EstateDirector.Duties.cs` (each building's morning, the money and the player's answers), `EstateDirector.Visit.cs` and `EstateVisitGame.cs` (the repair the player does himself), `EstateApp.Duties.cs` (the pages).

## How it talks to other features

- It stands on the property business's letting: the units, applicants, leases, arrears, notice and the court of `EstateRules.Letting.cs`, the condition and works of `EstateRules.cs`, and the save line `reallife:economy_estate` (parts `l` and `m`, version 4).
- The repair the player does himself uses the game's own destination marker (`GameUi.GuideMark`), the game's panel at the door (`GameUi.DoorPanel`) and the game's own work panel with its clock (`Jobs/ShiftSkip`), as a stint at a bank branch's counter does.
- Its rows in the Planner go out with the property business's (`Wiring/EstateLinks`): repairs waiting, the city's inspection or order, a tenant leaving.
- Nothing new is asked of another feature, and none of it is sent over the network.

## What happens

Only a building the player lets himself (**Let it yourself** on the **Mine** tab) has any of this; one the game lets is as before.

- **Satisfaction.** Each let unit's tenant has one, 0 to 1000 (`EstateUnit.Happy`; -1 until its first morning, so a save from before is judged afresh). Each morning it closes 15% of the gap to a target (`HappyTarget`): 520, plus half a point for each point of condition over 600 (less under), 40 a level of refurbishment, 0.6 for each thousandth of the market's rent the lease is under (or over) it, within -250 and +150, less an open repair (100, and 20 a day past its two days' grace, 300 at most), notice served (150) and the city's order on the building (60).
  - A lease's end renews by it (`RenewChance`: 10% at nothing to 90%, about as before for a tenant as content as most), in place of the rule of condition and rent the letting had.
  - Under 500 a tenant misses rent more often (`UnhappyMiss`, up to 30% more).
  - Under 450 they complain, 8% of mornings (`Duty`); a complaint says what they mind most (`Grievance`: the rent, the building, a repair, notice or the order).
  - Under 250 they may say they go in 14 days (`QuitDay`, 4% a morning); over 400 before then and they stay. They go on the day as a tenant who leaves does, the deposit set against what is owed.
- **Repair calls.** A let unit reports a fault on 1.2% of mornings in a building as new, one more in a thousand for each 40 of condition lost, never while builders are in. One call at a time, of six kinds (`CallKinds`), with a contractor's quote of 0.4% of the unit's share of the game's value, by kind and prices, $60 at least.
  - **Send a contractor**: paid now, as "Repairs" in the game's books, put right the next morning.
  - **Do it yourself**: 35% of the quote in materials, paid when it is done. The game's marker goes on the building's door; standing there on foot the game's panel offers **Do the repair**, which opens the game's work panel for 45 to 150 minutes by kind. Stopped early, the job waits for another go; not done in three days, it is open again. One such job at a time.
  - Done within the grace it pleases the tenant by 60, later by 20. Left ten days it does 25 of condition's damage once, and the tenant complains.
  - Repair works to new on the Works tab put every open call right.
  - Three complaints in a lease with a call a week old and the tenant goes to the city: the inspector comes in two days.
- **The city's inspections.** Every let building has one about every 45 days, the first drawn 10 to 45 days off (`Schedule`). The score is the condition less 60 for each call older than five days; it passes at 450, the mark under which applicants stay away. A fail is a fine ($400 at the start's prices, $1,200 for a second in a row) and an order: the inspector comes back in 14 days, then every 7. Two fails in a row bar new tenants until a pass (`Barred`): applicants stop. With repair works under way the inspector comes the day after them, with no fine.
- **Rent owed.** On a lease in arrears:
  - **Send a reminder**: once a spell, 20 points more a morning of the chance it is all paid, the tenant 25 less satisfied.
  - **Offer a payment plan**: once a spell, taken by 30% plus 0.7% a point of record. Taken, the arrears are paid in instalments over twice the days owed (4 to 12) with the rent, which the game pays again from that night (`EstateUnit.Paying`). A missed morning (80% of the tenant's own miss chance) ends it, a day's rent added to the arrears. No notice is served while it runs. Paid off, the tenant is 40 more satisfied.
  - Notice and the court are the letting's own.
- **Who runs it** (`EstateManager`, on the building's page):
  - **You**: everything is the player's, for nothing.
  - **A lettings agency**: 25% more applicants, and the best one for each empty unit taken (none with a record under 40; at once at 95% of the asking rent or more, else on the application's last day). Its fee is seven days of the agreed rent at each letting.
  - **A property manager**: the same, and each morning a contractor to every call, a reminder, then a plan to a tenant with a record of 40 or more, then notice, then the court; it has the builders in a week before the city's order runs out with the building below the mark. Its cut is 10% of the rent that comes in, arrears included. One notice a morning says what it did.
- **The building's page** (Mine tab): the rent roll, what is owed, the tenants' satisfaction, open calls, the next inspection or the order, the year's repairs, fees and fines (in its costs too, and started again with the game's tax year), and who runs it. Every building's page also adds up what it cost, bought and improved, against what a sale at its worth today would leave after the agent's fee and the tax on a gain: the sum a flip is judged by.
- **The Tenants tab** has a satisfaction column; the lease page its satisfaction, where it heads and why, complaints, a tenant's word to go, an open call, a plan, and the two new buttons. **The Works tab** lists the open calls before the buildings (the filter "Repair calls"), each with its page and the two ways to answer it.
- **The Planner** has a row for calls waiting at a building (not one a manager runs), the inspection or the order's day, and a tenant leaving.

## The option

**A landlord's duties** (`economy_estate_duties`, on), under Property. Off, a building the player lets himself is let exactly as before (EconomyCheck runs the same leases with and without the new fields): when it goes off, plans end with their arrears owed, a contractor paid for finishes the job, every other call, inspection and order is let go, and a job of the player's is called off (`EstateRules.DutiesOff`). Satisfaction and the manager chosen are kept for when it comes back.

## Tests (Options, Test tools, Property)

Each writes an `Estate test:` line and opens the app on what it made; **Put property back** restores the books as they were before the first test, not the cash.

- **A tenant reports a repair**: a call at the first let unit (a tenant put in first if none), the Works tab open on it.
- **A tenant complains**: a tenant made unhappy, a complaint told, the lease open.
- **A failed city inspection**: a let building brought under the mark and inspected now: the fine and the order.
- **A property manager's morning**: a manager takes a building on and its morning runs now: a contractor goes to the call.
- The existing **Rent day with a tenant in arrears** gives a lease with the reminder and the plan to press.

With Developer diagnostics on: `Estate check (duties, ...)`, `Estate check (inspection, ...)`, `Estate check (payment plan, ...)`, `Estate check (a repair done by the player, ...)`, `Estate check (duties off)` and `Estate check (repair panel)`, each ending PASS.

## Going Public

Everything is the player's own buildings in the player's own save line, drawn from the save's seed. Nothing is sent over the network and nobody else's building is touched. The repair at the door is on the player's machine only.

## With the city expansion

A finished building of the construction game (`feature/city-expansion`) becomes the player's from the next load through the game's own deed, so it is among `GameInstance.realEstate`, read by `EstateGame.ReadHoldings` as any other: let it yourself and all of this applies, its units counted from the game's figures. Joining needs a line in `Meta/FeatureCities.cs` for `economy_estate_duties` (it stands on no data of New York's of its own, as the property business). A block whose flats are kept rather than sold would want `ConstructionBridges` to tell the property business the design's units, so its flats are the units let.

## Not seen in the game

Everything above is built and checked offline. Not seen: the marker on a building's door, the game's panel there and the work panel with its clock, the selector of who runs a building, the new column and rows, and the notices opening the app on the right page.
