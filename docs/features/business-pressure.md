# Business pressure

## In a sentence

Trouble and chances for the businesses the player runs: rivals who undercut, a protection racket, inspections, staff disputes, suppliers, complaints and praise, a tax audit, bulk orders and awards, each with choices on the **Business desk** contact.

## Where the code is

- Folder: [`Scripts/Business`](../../Assets/Mods/RealLifeOverhaul/Scripts/Business/).
- Read first: `BusinessCatalogue.cs` (every kind of issue and every number, game-free), `BusinessDirector.cs` (the clock, the morning roll, the off switch) and `BusinessGame.cs` (the only code that touches the game's business fields).

| File | What it is |
| --- | --- |
| `BusinessCatalogue` | every kind of issue and every number that bounds it (game-free) |
| `BusinessState` | open issues, running footfall changes and closures with their end, the racket: one line in modData (game-free) |
| `BusinessGame` | the only code that reads or changes the game's business fields; each change bounded and logged |
| `BusinessIssues` | what each kind is: when it can start, what it says, the choices, what each does, the default at the deadline |
| `BusinessDirector` | the hourly clock, the one roll a morning, the off switch that puts everything back, the test buttons |
| `BusinessDesk` | the phone contact that lists open issues with deadlines and choices; its messages are the record |
| `BusinessVisit` | a collector, an inspector or a police sting played in the player's own shop while they stand in it |

## How it talks to other features

- `BusinessDirector.Footfall` is the mod's one writer of a business's promotion figure: the company, the brands, the city's economy and interiors all go through it (`Wiring/CompanyLinks.cs`, `BrandLinks.cs`, `CityLinks.cs`, `InteriorLinks.cs`).
- The company starts an audit or an inspection through this feature's own rules, and the utilities close a business through its closures (`Wiring/UtilityLinks.cs`).
- The racket's arrest is played with `Crime/ArrestSequence`.

**What the game's rivals already do.** `BigAmbitions.Rivals` is a real simulation: 19 rivals own businesses and buildings.

A "special rival" per neighbourhood sends a message once the player has one revenue business there and becomes active at three, after which its timeline fires three attacks (`RivalDefenseHelper`): price cuts of 45 to 65 % on the player's best sellers for 5 to 8 days, 3 to 7 competitor businesses opened to push demand down, and 10 to 24 of the player's best employees poached at once (`EmployeeInstance.PoachByRival`: the employee texts the offer with accept and decline buttons).

`CompetitionHelper` opens and shuts AI businesses daily, and `RealEstateHelper` has competitors buying buildings.
Rent never changes after signing and there is no landlord behaviour, so "a rival buys your building" is not added.
The mod's rival moves therefore fill the early game: they are not started in a neighbourhood whose special rival is active, and the mod's one poaching *is* the game's own offer, made to a single employee at +12 %.

The game takes an offer's buttons away only when one is pressed, and draws them on the newest message of the employee's chat whatever has happened since; most ways an employee leaves send nothing there.
`Core/RivalOfferGuard` empties the buttons of any rival's offer (and of the rival's giving up) once it is no longer the player's to answer, by one rule (`Core/RivalOfferRules`): the employee off the list, replaced by the headhunter under another name, or resigned, or the offer matched, run out or given up.
It looks after every game hour, after a firing, once a save loads, right after this mod lets an employee go (`BusinessGame.Leave`, the tech sector's resignation and dismissal, a sale with its staff), and as the Contacts app picks a chat (`Core/RivalOfferWatch`).
The poaching issue's watch reads the same rule: it says the employee has left for the rival only when the buttons would go, and that they stay once the offer was matched.

Everything is in `Scripts/Business`: `BusinessCatalogue` (every kind, every number; game-free), `BusinessState` (one line, `reallife:business`, in modData; game-free), `BusinessGame` (the only code that touches the game's business fields), `BusinessIssues` (what each kind does), `BusinessDirector` (the clock, the morning roll, the off switch, the tests), `BusinessDesk` (the phone contact), `BusinessVisit` (somebody who comes into the shop).

**Rules for all of it** (`BusinessCatalogue`):

- nothing in the first 7 days after the mod first sees a save; never on a business known for less than 7 days;
- one open issue per business, two overall; a business is left alone for 3 days after one ends; each kind has its own rest (8 to 30 days); one roll a morning at 09:00: 8 % at frequency 1 to 30.5 % at 10;
- a charge is a share of what that business itself took a day (the game's order history, last seven days) between a floor and a ceiling.
  One the player did not choose (a fine, a repair, an adjustment) is also never more than 6 % of the cash in hand; nothing is ever forced, and a charge that cannot be paid falls back to the issue's other outcome;
- footfall changes are -12 % to +10 % of customers, one per business, with an end day, and only on businesses whose promotion figure the game itself recomputes and can carry the change in full (not within a few points of 0 or 100);
- a closure is 12 game hours at most;
- an employee's satisfaction is never pushed below 20 (the game makes them resign under 10); a wage only goes up;
- **the off switch** closes every open issue without its outcome, puts every promotion figure back and reopens every business the mod closed; each group has its own switch; "easy business" leaves the opportunities only.

**How the effects reach the game** (all in `BusinessGame`, each logged with before and after):

- *Money*: `GameManager.ChangeMoneySafe` at the business's address, never forced.
  Marketing answers are booked under the game's marketing category, fines and licences under licensing fees, bonuses and the lunch under salary expenses, so they appear in that business's own income statement.
- *Footfall*: the game computes customers from `0.5 + 0.75 x promotion / 100`.
  The mod recomputes the game's own figure (`BusinessHelper.UpdatePromotion`), sets `promotion.total` to the value that gives the wanted percent (`BusinessCatalogue.PromotionFor`), and has the game redraw the customers still to come today (`CustomerEntriesHelper.UpdateCustomerEntriesForPlayerBusiness`, which the game itself calls when a business opens or closes).
  It is put back on top each game hour and redrawn after midnight.

  The game recomputes the figure daily, on entering the building and when marketing changes, so a save loaded without the mod heals by itself the next day.
  The player sees it: the promotion figure on the business's page moves.

- *Closure*: `BuildingRegistration.TemporarilyClose`, the switch on the business's own page; the reopening hour is remembered and done by the mod.
  Only a building of the player's is closed. A closure on a building the player has let go since is dropped as the save is read and each hour (`BusinessState.DropWhere`), and the switch it set is cleared while nobody rents the building, so it never passes to the next tenant or is opened as the player's.
  Removing the mod mid-closure leaves the business closed until the player unticks it.
- *Theft by an employee*: added to `stolenItemsCost`, which the game books as Theft in the statement and clears each night.
  No cash moves.
- *Wages*: `EmployeeInstance.hourlyWage`, as the game's own poach counter-offer writes it.
  *Leaving*: the game's own resignation path (what an ignored complaint does).
  *Deliveries*: `DeliveryContract.nextDeliveryDay`.

| kind | when | what it does | choices | caps |
|---|---|---|---|---|
| Competitor undercuts | has a promotion figure; no active game rival | -10 % customers, 5 days | marketing push (0.6 of a day, 80 to 1200: +4 % for 3 days) / match prices (0.3, 40 to 600: dip halved) / quality (cleanliness 80 and satisfaction 60: over in 2 days) / ride it out | -10 % |
| A rival wants your staff | a poachable employee, a rival with businesses | the game's own offer, +12 %, 3 days | answered in the employee's messages: match or let go | one employee |
| Smear in the paper | has a promotion figure | -6 %, 4 days | right of reply (0.4, 60 to 800: ends) / lawyer (0.25, 50 to 500: 55 % ends it and +3 % for 2 days) / ignore | -6 % |
| Protection racket | takes 150 a day or more | see below | pay / refuse / police / security | weekly 60 to 600; repair 80 to 900; closed 4 h |
| Health inspection | food business | tomorrow 10:00, on the game's cleanliness figure | book a contract clean (0.2, 40 to 400: dirt to a third) or clean it yourself | 75 passes (+4 % for 3 days); 50 to 74 a warning and a second visit in 2 days; under 50 a fine (0.5, 100 to 1500); under 30 or unpaid: closed 6 h |
| Fire-safety inspection | any | tomorrow 10:00: cleanliness 55 and the facility score 40 | book a contractor's check (0.25, 50 to 500: certain pass) | fine 0.4, 80 to 1000; both failed or unpaid: closed 4 h; pass +4 % for 3 days |
| Licence renewal | any | a fee (0.5, 60 to 900) due in 3 days | pay now | late: x1.5; unpayable: closed 6 h |
| Strike threat | 2+ staff, wages 12 % under the game's formula on average | 3 days to answer | agree (each wage to 95 % of the formula, +15 % at most) / bonus of 8 hours' wages each / refuse (half the time a walkout) | closed 8 h; satisfaction -8 |
| Theft by an employee | stock to steal, security level 10+, staff | a loss (0.15 of a day, 20 to 300) booked as theft | dismiss / final warning (30 % repaid) / let it go | 300 |
| Argument | 2+ staff | two fall out | side with either (+5 / -12 satisfaction) / lunch (0.05, 20 to 120: +6 both) | unanswered: -6 both |
| Late delivery | a wholesale delivery within 2 days | a day late unless | express (15 % of the delivery, 20 to 400) / take the delay | one day |
| Supplier's surcharge | the same | 8 % of the delivery, 15 to 500 | pay / haggle (30 % + half the negotiation skill, to 80 %; trains it) / switch haulier (a third, a day late) | 500 |
| Complaint that spreads | satisfaction under 50 or cleanliness under 55 | -6 %, 3 days, naming the weakest of the game's four figures | apologise and voucher (0.15, 20 to 300: ends) / ignore | -6 % |
| Praise that spreads | satisfaction 70+ | +6 %, 3 days | share it (5 days) | +6 % |
| Tax audit | a week of books | 3 days to answer | accountant (0.07 of a day, 100 to 800: 70 % clean, else a third of the adjustment) / alone (40 % clean, 45 % an adjustment of 0.14 of a day, 100 to 1500, 15 % a refund to 500) | adjustment also at most 6 % of cash |
| Bulk order | takes 100 a day | 0.4 of a day (120 to 2000) if the shop is open tomorrow at 12:00 | take it / decline | one every 8 days |
| Feature in the paper | has a promotion figure | | give the interview: +8 % for 3 days | +8 % |
| Award | satisfaction 75+, cleanliness 70+ | a prize (0.3, 100 to 1000), +5 % for 4 days | accept | every 30 days |

**The racket.** A collector asks for a weekly sum; if the player is standing in that shop he walks in and says so himself, and the desk opens on it when he reaches them.
*Pay*: collected every seven days, creeping 10 % a week to 1.5 times the first demand; the desk can stop it or go to the police at any time.

*Refuse* (also the default after 3 days): in 2 to 4 days, with odds of 0.7 x (1 - security / 150), a brick through the window (a repair, and the shop shut 4 hours); then nothing for 14 days.

*Police*: three days, then odds of 0.35 + security / 200 (0.85 at most) of a case; with one, the collector is caught at the shop next day at 14:00 (played in the shop through `Crime/ArrestSequence` if the player is in it, otherwise told) and **the racket is over for good in that save**; without one it counts as a refusal.

*Security*: three days to get the game's security level to 50 (the game's own guards and cameras); then he gives up for 28 days, otherwise it counts as a refusal.

The crime system has no public entry for staging a scene by name (`CrimeDirector.StageNow` takes a family, for its test buttons), so the vandalism is told, not played; the arrest uses `ArrestSequence.BeginIndoors` directly.

**In the shop** (`BusinessVisit`): the collector, an inspector (walks in, looks the place over with a clipboard at two spots, comes to the player, leaves; the verdict follows an hour later) and the sting are played by the street scenes' own pool through `ActorRole`: in at the real door, on the shop's navigation mesh, out by the door, 110 s at most, ended with everybody back in the pool the moment the player leaves or a crime scene starts.

Nothing of the shop is touched.
Nobody free in the pool: no visit, and the issue goes on by phone.

**The Business desk** is a phone contact (the game gives a mod no way to add a phone page): its call goes through the open issues with their deadlines, reads each choice out with its cost before it is taken, and its messages are the record of everything that happened.

Test buttons (options, Business): one per group (each press starts the next kind of that group on the business the player is standing in, or their first; the grace periods, rests and odds are waived, and a kind that cannot exist here says exactly why), open the Business desk here, and resolve all (also forgets the racket, ended or not).

Wish-list for the asset pipelines (the two clips are already asked for by id before today's stand-in; the props need placement code):

| id | what | used by |
|---|---|---|
| clip `collector_lean` | leaning in, one hand flat on a counter, the other gesturing low, 5 s loop | **delivered, deliberately not played by `BusinessVisit`**: it is made for a counter 0.975 m up with its near edge 0.32 to 0.36 m ahead and reads as a man bent over nothing anywhere else. A visit is played 1.7 m from wherever the player stands, and the game's shop furniture says nothing of a counter top's height or edge, so no such place can be found. The collector (and the man of a sting) talks with `talk_a` and, once the desk is open on his issue, waits with `arms_crossed`; the game's `IDontThinkSo` gesture only without the clip bundle. One line in `BusinessVisit` (`TalkClip`) if a measured counter ever exists |
| clip `inspect_look` | clipboard in the left hand, looking up at a ceiling and down at a floor, 6 s loop | the inspector between spots (today `clipboard_write`) |

| prop `window_boarded` | a plywood sheet 1.2 x 1.5 m to stand against a shopfront | (needs placement code) after the vandalism, for the hours the shop is shut |
| prop `closed_notice` | an A3 notice on a stand, "CLOSED BY ORDER" | (needs placement code) a business closed by an inspector |
