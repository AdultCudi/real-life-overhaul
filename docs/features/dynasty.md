# Dynasty

## In a sentence

The player writes a will at a law firm, names an heir and leaves shares of the estate to family, people they know and the city's charities; estate tax falls on what passes at a death in old age or when the player retires; the player then plays on as the heir, who has their own age, name and learning; the rival tycoons age, retire and die by the same rules, and the estates of the city's households are counted.

## Where the code is

- Folder: [`Scripts/Dynasty`](../../Assets/Mods/RealLifeOverhaul/Scripts/Dynasty/) (namespace `RealLifeOverhaul.Dynasty`). The kit's page is `Scripts/Ui/Ventures/DynastyPages.cs` (game-free) and `DynastyLive.cs`; the wiring is `Scripts/Wiring/Wiring.DynastyLinks.cs`; the two parts other features give it are `Scripts/Family/FamilyDirector.Succession.cs` and `Scripts/Careers/CareersDirector.Dynasty.cs`.
- Option: "A will, estate tax, retiring and an heir who carries on" (`dynasty`, on by default). Off: nothing runs, the app leaves the phone, the game ends in old age as it always did, and the line in the save stays as it was (the will written, a handover under way at its step). Two things go on all the same: a gift to a person is still dated (in the line, or in a line of its own, `reallife:dynasty_gifts`, when the line cannot be read), so the option is no way round the gifts' window; and the debt for estate tax stays among the Finance app's debts and is charged for every day that went by when the option is switched on again.
- Checks: EconomyCheck `--dynasty` (`DynastyChecks.cs` and `DynastyFixChecks.cs`).

```
DynastyCatalogue           every figure, each with its unit and its source
DynastyState, DynastyStore the records and the save line reallife:dynasty (version 1)
DynastyRules               game-free: the tax and its bands, the gifts' window, a will's terms and fee, who carries on, an estate settled and settled again on
                           what really landed, the debt for tax (every day), the retired founder's fund and living, what the heir knows, the marriage that frees a partner's
                           part, what holds a handover back and what is done in the last days
DynastyBooking             game-free: what the estate is made of besides the cash (Holdings), how money is booked in chunks (DynastyBooking), what the law and the clock say
                           (DynastyGate), the estate kept for a day, a revision and the cash (DynastyEstateCache), a gift kept in a line of its own (DynastyGifts)
DynastyWill                game-free: a draft changed a press at a time, and the signing
DynastyHand                game-free: a handover in its steps, each with a flag of its own set with its effect, kept whole across a save, a load, an error and the option off
DynastyRivals              game-free: the tycoons' ages, retiring and dying, their tax (and DynastyCity: the city's estates)
DynastyStudy               game-free: the heir's study file in the mod's own education
DynastyFacts               what the pages read
DynastyDirector (+ parts)  the timer (a second), the day, the last days, the handover's steps, the will, the rivals, facts, tests, the app
DynastyGame                the one file that touches the game: the clock, the played character's name and age, skills and courses, cash, notices
DynastyApp                 the app in the game's own frame; the kit's page is Ui/Ventures/DynastyPages
```

## What the player does

1. **The will.** The **Legacy** app (Will tab) lists the partner, the children, up to ten of the people the player knows (the family first, then the closest) and Giving's three charities. For each: whether they carry on after the player (one heir), and the share of the estate they are left, in steps of 5%. All bequests together take at most 50% (`OthersMost`: the heir's reserved half). The tab also sets the age the player may retire from (55 to 85 in steps of 5) and what they keep when they do (0, 5, 10, 15 or 20% of the estate). Changes are a draft until **Sign the will**: the best law firm of the city draws it up for 1,200 the first time and 300 for a change, at the city's prices; the player's own law firm charges nothing. The fee is taken only from the cash over the investors' floor, and without it nothing is signed.
2. **The estate.** The Estate tab works out today's estate and its tax, line by line, as if it passed today, shows whether the player may retire and why not, the debt for tax if there is one, and keeps the last six estate statements.
3. **Retiring.** From the will's age on, **Retire and hand over** (asked once more in the game's question box) passes the estate while the player lives.
4. **The last days.** Two days before the game's own end in old age (90 years, `FuneralHelper.DeathAge`) the estate passes to the heir and the player plays on as them, so the game's end never comes while somebody can carry on.
5. **The family side and the tax side.** The Family page and its things to do say who carries on today; the Company app's tax tab has the estate tax the estate would bear today. The estate on a page, and that row, is worked out once for a day, the director's revision and the cash (`DynastyEstateCache`), not at every rebuild of a page; only the Estate tab asks the law what holds a handover or refuses retiring.

## Who carries on

An heir is somebody the mod already holds: the partner (family life's), a grown child (family life's, eighteen or over), or one of the social life's people of family origin, between 18 and 85 years old (`DynastyRules.CanCarryOn`). The will's heir carries on if they are there and can; otherwise the law's fixed order decides: the partner, then the eldest grown child (`DynastyRules.Choose`). With neither, the state takes the estate: nothing is handed over and the game ends in old age as it always did. A relative carries on only when the will names them.

A grown child's age goes on after eighteen in the game's years (family life stops counting there; the dynasty keeps the day a child was first seen grown). A partner whose age nobody keeps is taken to be two years under the player's.

## The estate and its tax

- **The estate** is the cash over the investors' floor (`TechDirector.FloorNow`, rounded up) and everything else, line by line (`Holdings`, filled in `Wiring/Wiring.DynastyLinks.cs` `HoldingsNow`, each line read from its own feature in a guard of its own; assets are rounded down and debts up):
  - **Counted in:** the Finance app's net worth (`Books.NetWorth`, with the shares on the market where it runs, `MarketDirector.PlayerWorth`): the cash, the investment funds, vehicles and boats at their price and property at the price paid, less the game's loans, the mod's accounts, fines, court debts and the estate's own debt. Also the money set aside in the company's tax reserve (`TaxFile.Reserve`; it left the cash, and is the founder's until the bill is paid); the own bank's capital as the regulator counts it, retained earnings with it, for a running bank that is not a test's (`OwnBankRules.CapitalNow`); Giving's loans to people that are still open (what is left to be paid back); and what friends owe the player from the social life's loans (`SocialPerson.Loan`). So cash moved into the reserve, the bank or a loan to a friend in the weeks before is counted all the same, and a loan to the heir is counted too (it ends when the heir takes over: nobody owes themselves).
  - **Taken off:** the student loan and the construction loans (credit accounts that `EconomyBridges.OtherAccounts` gives and the net worth does not take off); the game's tax bill waiting to be paid, with its late fee if that has been added (`currentUnpaidTaxes.totalToPay`), and the game's back taxes (`currentBackTaxes`); the company's back tax and fines found by an audit (`TaxFile.Owed`); and what the player owes friends (`SocialPerson.Debt`).
  - **Not valued:** the stock, equipment and goodwill of the businesses have no price in the game that is honest, so they are left out and stay with the businesses; property is counted at the price paid, as the game counts it for the Finance app.

  Debts are therefore paid before anybody inherits, and nothing is written off: the heir takes the debts with the rest. An estate whose debts are more than it is ("under water") pays no bequest and passes whole, debts included.
- **Bands** (at the start's prices, following the city's): nothing on the first 1,000,000; 20% on the next 1,000,000; 30% on the next 4,000,000; 40% on the rest (`DynastyRules.Tax`).
- **Exempt:** what goes to a charity; a married partner's part, up to 50% of what passes (`SpouseShare`), and only for a marriage at least as old as the gifts' window, three of the game's years (`DynastyRules.WedLongEnough`). The wedding day is the one family life keeps (`FamilySave.MarriedDay`); where none was kept (a save from before it was) the dynasty notes the first day it sees the couple married, and a marriage already there when the dynasty first met the save counts as long enough (`DynastyCatalogue.WedOld`). A partner married a few days has no relief, as an heir or as a bequest.
- **Gifts in life:** every gift of money to a person in the last three of the game's years (Giving's gifts, and a loan it wrote off) is counted back into what is taxed, however many and however small (`DynastyRules.GiftsInWindow`). Gifts to charities are not counted: they would be exempt in the estate too.
- **Paying it:** the tax is taken from the cash first. What the cash does not cover is never cut: it is owed at 6% a year, a part a day over five of the game's years (`DynastyRules.Borrow`, `LoanDay`), and shows among the Finance app's debts whatever the option. A part the cash over the floor does not cover is a late day; after 30 late days in a row the part is taken whatever the cash. Every day that went by since the debt was last gone through is charged one by one (`DynastyRules.LoanDays`), however many.
- **Money is booked in chunks:** the wallet is a float, so a booking is made in whole money of at most 8,000,000 at a time (`DynastyCatalogue.ChunkMost`, a number a float holds exactly) and the cents last, so a tax or a bequest of any size is booked to the cent and none is cut at what an int holds. A booking the game refuses stops the step with a `HandFault` (logged as an error, the feature switched off for the session); what was taken so far is kept in the save and the step goes on from there when it is loaded again, so nothing is paid twice and no money is made.
- **Bequests** are each their share of what passes, paid in cash after the tax. If the cash left does not cover them all, every one is cut by the same thousandths (and the tax is worked again on what is really left to charities and the partner). Where a bequest lands: a charity's in its pot (Giving's, counted with the others' gifts and never as a deduction from the income tax); a person's in their household as the population holds it (its debt first, then its savings); a relative's with no such household on the family's own ledger; somebody with nowhere for it to land gets nothing, and it stays in the estate and is taxed with it. A person is found by their place in the cast **and** their name, so a place that the cast gave to somebody else is not them. At most six bequests (`BequestsMost`) make a will. A bequest is booked out of the wallet first and then lands; if less lands than was booked (a session began part way, a household has no room after all) the rest comes back to the wallet and stays in the estate with the heir, and the tax is worked again on what really landed (`DynastyRules.Retax`): a charity's part that did not land is no longer exempt, a partner's relief covers only what landed, and the difference is added to the estate's debt. What somebody is left never comes back.
- **The identity:** the estate = what a retiring founder keeps + the tax + the bequests paid + what the heir is left (`DynastyRules.Balanced`). Every figure is whole money, so it holds to the cent; the cents of the wallet stay in the wallet.

## Retiring and the retired founder

Retiring is the same settlement, with the same tax, except that the founder keeps their part in cash as a fund of their own, which is not passed on and so not taxed yet. They stay in the family as its elder: once a month they draw their living from the fund (sized to last to 90) and it is paid to the head of the family through the same booking, visible among the transactions (`DynastyRules.ElderPaid`; what the game did not take goes back into the fund), and at 90 what is left is an estate of its own that goes to the head of the family. If that is within three years of the handover it is taxed together with the estate they handed over (`DynastyRules.TaxOn`), so keeping a fund back never uses the threshold twice. One retired founder at a time.

Retiring is refused, before anything is valued or booked, while: no will is signed, the player is under the will's age, nobody can carry on, a Going Public session runs, an estate is already passing, the family already keeps a retired founder, or the law has anything open against the player (an open case, a warrant, prison served or pending, probation, community hours owed, a suspended licence of either kind, wanted by the police; a state of the law that cannot be read counts as open: `DynastyGate.LawOpen`, filled by `Wiring.DynastyLinks.LawNow`). It is no way out of a sentence, a case or a ban.

## What passes and what does not

| Thing | What happens |
| --- | --- |
| Businesses, staff, contracts, property, vehicles, boats | Stay in the save: the heir runs them. |
| Loans, accounts, fines on a plan, civil claims, the student loan | Stay owed. Nothing is written off. |
| Shares, a listed firm, investors and co-owners | Unchanged. The investors' floor is never the estate's cash. |
| Business licences | Stay with the businesses. |
| Tax the founder had set aside, the own bank, loans to and from people | Stay in the save. The tax reserve, the bank's capital and the loans out are counted in the estate and the heir takes them; what is owed to friends is taken off and the heir owes it. |
| The driving licence, its points and a suspension | Left as they are: the mod keeps one licence a save, and a ban running is not lifted. |
| The game's skills and courses | The heir has their share of each skill. A course the founder **finished stays finished** with all its hours (`DynastyRules.CourseMinutes`): the family's businesses are run on them (the recruitment agency asks for the human resources course, headquarters and offices for theirs, the market for its own). A course only part done is the heir's share of the hours, open again at the game's school. |
| The mod's education (Careers) | The heir's share of the skills; none of the founder's qualifications; their own degree if the family put them through university. The founder's job ends as a retirement; the pension pot stays. |
| Standing | Recognition and press are halved (the name keeps half). Integrity goes half the way back to 50. Customers, workers and neighbourhoods follow the businesses, which stay. |
| The record | **Not touched.** Nothing is sealed, cleared or closed at a handover: convictions, the police's lines and everything open (a case, a fine, a plan to pay, probation, a suspension, a warrant) stay exactly as they are for whoever leads the family. Justice seals a record by its own clean days and the credit file reads the police's lines by their own days, so a handover neither wipes a past nor improves a rating, and a death is nobody's way out. |
| The family | A partner who carries on keeps the children. A child or a relative who carries on starts a household of their own and the memories are kept; the other children stay in the family, each with their school and college fund (they are the new head's to look after, `FamilyDirector.NewHead`); only the founder's partner record goes (they are a parent or a relative now). The founder's own parents are left as the mod holds them: family life keeps one Mom for the head of the family, and she stays that for the heir. |
| The people the founder knew | Stay known. The founder's partner is no longer a partner. A person who becomes the played head leaves the cast by the social life's own way. |
| The look of the played character | Not touched in any way. |

**The share of the founder's learning** (`DynastyRules.Upbringing`): a child 30%, plus 10% for a public school or 25% for a private one, plus up to 25% for the college fund in proportion to what was paid in, plus up to 20% by their closeness; a partner 50% plus up to 30% by their affection; a relative 30% plus up to 30% by how close the two are.

## What holds the last day back

The estate does not pass in a Going Public session or while the player is held (prison, before the court) or in the middle of something staged (`DynastyGate.Custody`); the age is then kept five days short of the end (`DynastyRules.Pinned`, at every hour, so the game's own midnight cannot take it past) and the estate passes the hour it can. An open case or a sentence does not hold a death back: they stay open for whoever leads the family. With Permadeath on for the save a death is Permadeath's own and ends the save: the dynasty leaves it alone, and only retiring hands over. With the game's own ageing switched off nobody reaches their last days.

## The rivals and the city

Each tycoon who is forward has an age from the save's seed (45 to 64 at the first look), an age to retire at (65 to 74) and an age to die at (76 to 89); one head in four never retires and dies in office (68 to 87). At a handover the fortune bears the estate tax of the player's own bands; only the worth over the tycoon's floor leaves it (a tax from the floor itself would be made up the next morning), and what really left is what is said. The heir (28 to 42) takes the holding, the generation counts on, and the fortune grows at half the pace for 60 days (the rivals' own slow period). It shows on the rich list, the tycoon's panel and in the City Ledger the morning after. The rivals rest in a session.

The city's households, in the population's own terms: a household that loses somebody keeps its savings and debts (the ones left inherit, `LifeRules.AfterLoss`); one with nobody left is taken in by family beyond the city and its savings go with it. The dynasty counts those estates by the game's year: how many, what they left after their debts, the tax the same bands put on them, and the debts nothing was left to pay.

## The save

`reallife:dynasty`, version 1: the seed, the generation, the day the present marriage began, the will and its bequests, the dated gifts, a handover under way (its step, its whole statement, what has been taken from the wallet so far and each bequest's progress), the statements kept, the family's ledger, the retired founder, the debt for tax, the rivals' lines, the city's estates, the lineage, the paper's pieces and the days the children were seen grown. A save without it loads with no will. Every number is written and read in the invariant culture and every name is escaped.

**A handover** is frozen when it begins: the estate is valued once and its statement fixed. Its steps each do one thing and say so on the record with a flag of its own that is set in the same step as its effect (the debt borrowed and the tax's cash part; what is kept; each bequest: out of the wallet, then landed, then what did not land back; the tax worked again; the other features told; the played person changed; the lineage), so a save, a load, an error or the option off in the middle goes on at the step and nothing is done twice. A retirement takes a step a second; a death takes them all at once.

## Left out, with the reason

- **Contesting a will.** The courts take a civil claim against the player (`DebtClaims.Lodge`), but a judgment is a debt with no claimant (`JusticeDirector.Court.cs`, `JudgeSuit`: `AddDebt(JusticeDebt.Civil, ...)`), so nothing a slighted relative won could reach them. The heir's reserved half stands in its place.
- **Selling assets to pay the tax.** What to sell is the heir's to decide: the tax the cash does not cover is owed with interest instead.
- **Any change to how the heir looks.** The heir is played in the body the save has.

## Tests

Options, Tests, Dynasty: the estate statement for today (changes nothing); reach the age the will sets for retiring; the last days now (hands over, cannot be put back); a rival tycoon hands over now. Each writes one `Dynasty check (...)` line.

Not verified in the game: the app in both frames, the notices, the handover itself (the name and the age taken by the game's own screens, the family and the people after it), the game's own ageing drawing the heir's age at the next midnight, the rich list and paper lines, that the game's time machine runs `GlobalEvents.onNewHour` for every hour of a skip before it reaches the midnight that ends the game (read in `GameManager.RunMainGameTick`: every hour of a tick is passed in order, `NewDay` at 24, and a skip held back by a cell is the one case that is only kept short of the end by that hourly look), and the monthly living appearing among the transactions.
