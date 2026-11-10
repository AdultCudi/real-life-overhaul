# Dynasty

## In a sentence

The player writes a will at a law firm, names an heir and leaves shares of the estate to family, people they know and the city's charities; estate tax falls on what passes at a death in old age or when the player retires; the player then plays on as the heir, who has their own age, name and learning; the rival tycoons age, retire and die by the same rules, and the estates of the city's households are counted.

## Where the code is

- Folder: [`Scripts/Dynasty`](../../Assets/Mods/RealLifeOverhaul/Scripts/Dynasty/) (namespace `RealLifeOverhaul.Dynasty`). The kit's page is `Scripts/Ui/Ventures/DynastyPages.cs` (game-free) and `DynastyLive.cs`; the wiring is `Scripts/Wiring/Wiring.DynastyLinks.cs`; the two parts other features give it are `Scripts/Family/FamilyDirector.Succession.cs` and `Scripts/Careers/CareersDirector.Dynasty.cs`.
- Option: "A will, estate tax, retiring and an heir who carries on" (`dynasty`, on by default). Off: nothing runs, the app leaves the phone, the game ends in old age as it always did, and the line in the save stays as it was (the will written, a handover under way at its step); a gift to a person is still dated in the line, so the option is no way round the gifts' window.
- Checks: EconomyCheck `--dynasty` (`DynastyChecks.cs`).

```
DynastyCatalogue           every figure, each with its unit and its source
DynastyState, DynastyStore the records and the save line reallife:dynasty (version 1)
DynastyRules               game-free: the tax and its bands, the gifts' window, a will's terms and fee, who carries on, an estate settled,
                           the debt for tax, the retired founder's fund, what the heir knows, what holds a handover back
DynastyWill                game-free: a draft changed a press at a time, and the signing
DynastyHand                game-free: a handover in its steps, kept whole across a save, a load and the option off
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
5. **The family side and the tax side.** The Family page and its things to do say who carries on today; the Company app's tax tab has the estate tax the estate would bear today.

## Who carries on

An heir is somebody the mod already holds: the partner (family life's), a grown child (family life's, eighteen or over), or one of the social life's people of family origin, between 18 and 85 years old (`DynastyRules.CanCarryOn`). The will's heir carries on if they are there and can; otherwise the law's fixed order decides: the partner, then the eldest grown child (`DynastyRules.Choose`). With neither, the state takes the estate: nothing is handed over and the game ends in old age as it always did. A relative carries on only when the will names them.

A grown child's age goes on after eighteen in the game's years (family life stops counting there; the dynasty keeps the day a child was first seen grown). A partner whose age nobody keeps is taken to be two years under the player's.

## The estate and its tax

- **The estate** is the cash over the investors' floor (`TechDirector.FloorNow`, rounded up) and everything else the Finance app counts (`Books.NetWorth`, with the shares on the market where it runs, `MarketDirector.PlayerWorth`), every debt taken off. Debts are therefore paid before anybody inherits, and nothing is written off: the heir takes the debts with the rest. An estate whose debts are more than it is ("under water") pays no bequest and passes whole, debts included.
- **Bands** (at the start's prices, following the city's): nothing on the first 1,000,000; 20% on the next 1,000,000; 30% on the next 4,000,000; 40% on the rest (`DynastyRules.Tax`).
- **Exempt:** what goes to a charity; a married partner's part, up to 50% of what passes (`SpouseShare`).
- **Gifts in life:** every gift of money to a person in the last three of the game's years (Giving's gifts, and a loan it wrote off) is counted back into what is taxed, however many and however small (`DynastyRules.GiftsInWindow`). Gifts to charities are not counted: they would be exempt in the estate too.
- **Paying it:** the tax is taken from the cash first. What the cash does not cover is never cut: it is owed at 6% a year, a part a day over five of the game's years (`DynastyRules.Borrow`, `LoanDay`), and shows among the Finance app's debts. A part the cash over the floor does not cover is a late day; after 30 late days in a row the part is taken whatever the cash.
- **Bequests** are each their share of what passes, paid in cash after the tax. If the cash left does not cover them all, every one is cut by the same thousandths (and the tax is worked again on what is really left to charities and the partner). Where a bequest lands: a charity's in its pot (Giving's, counted with the others' gifts and never as a deduction from the income tax); a person's in their household as the population holds it (its debt first, then its savings); a relative's with no such household on the family's own ledger; somebody with nowhere for it to land gets nothing, and it stays in the estate and is taxed with it. What somebody is left never comes back.
- **The identity:** the estate = what a retiring founder keeps + the tax + the bequests paid + what the heir is left (`DynastyRules.Balanced`). Every figure is whole money, so it holds to the cent; the cents of the wallet stay in the wallet.

## Retiring and the retired founder

Retiring is the same settlement, with the same tax, except that the founder keeps their part in cash as a fund of their own, which is not passed on and so not taxed yet. They stay in the family as its elder: once a month they draw their living from the fund (sized to last to 90), and at 90 what is left is an estate of its own that goes to the head of the family. If that is within three years of the handover it is taxed together with the estate they handed over (`DynastyRules.TaxOn`), so keeping a fund back never uses the threshold twice. One retired founder at a time.

Retiring is refused, before anything is valued or booked, while: no will is signed, the player is under the will's age, nobody can carry on, a Going Public session runs, an estate is already passing, the family already keeps a retired founder, or the law has anything open against the player (an open case, a warrant, prison served or pending, probation, community hours owed, a suspended licence, wanted by the police). It is no way out of a sentence, a case or a ban.

## What passes and what does not

| Thing | What happens |
| --- | --- |
| Businesses, staff, contracts, property, vehicles, boats | Stay in the save: the heir runs them. |
| Loans, accounts, fines on a plan, civil claims, the student loan | Stay owed. Nothing is written off. |
| Shares, a listed firm, investors and co-owners | Unchanged. The investors' floor is never the estate's cash. |
| Business licences | Stay with the businesses. |
| The driving licence, its points and a suspension | Left as they are: the mod keeps one licence a save, and a ban running is not lifted. |
| The game's skills and courses | The heir has their share of each skill and of the hours of each course; a course they have only part of is open again at the game's school. |
| The mod's education (Careers) | The heir's share of the skills; none of the founder's qualifications; their own degree if the family put them through university. The founder's job ends as a retirement; the pension pot stays. |
| Standing | Recognition and press are halved (the name keeps half). Integrity goes half the way back to 50. Customers, workers and neighbourhoods follow the businesses, which stay. |
| The criminal record | Sealed (Justice) and cleared (Law): it was the founder's own. Open matters are not touched. |
| The family | A partner who carries on keeps the children. A child or a relative who carries on starts a household of their own; the memories are kept. |
| The people the founder knew | Stay known. The founder's partner is no longer a partner. A person who becomes the played head leaves the cast by the social life's own way. |
| The look of the played character | Not touched in any way. |

**The share of the founder's learning** (`DynastyRules.Upbringing`): a child 30%, plus 10% for a public school or 25% for a private one, plus up to 25% for the college fund in proportion to what was paid in, plus up to 20% by their closeness; a partner 50% plus up to 30% by their affection; a relative 30% plus up to 30% by how close the two are.

## What holds the last day back

The estate does not pass in a Going Public session or while the player is held or in the middle of something staged; the age is then kept five days short of the end and the estate passes as soon as it can. With Permadeath on for the save a death is Permadeath's own and ends the save: the dynasty leaves it alone, and only retiring hands over. With the game's own ageing switched off nobody reaches their last days.

## The rivals and the city

Each tycoon who is forward has an age from the save's seed (45 to 64 at the first look), an age to retire at (65 to 74) and an age to die at (76 to 89); one head in four never retires and dies in office (68 to 87). At a handover the fortune bears the estate tax of the player's own bands; only the worth over the tycoon's floor leaves it (a tax from the floor itself would be made up the next morning), and what really left is what is said. The heir (28 to 42) takes the holding, the generation counts on, and the fortune grows at half the pace for 60 days (the rivals' own slow period). It shows on the rich list, the tycoon's panel and in the City Ledger the morning after. The rivals rest in a session.

The city's households, in the population's own terms: a household that loses somebody keeps its savings and debts (the ones left inherit, `LifeRules.AfterLoss`); one with nobody left is taken in by family beyond the city and its savings go with it. The dynasty counts those estates by the game's year: how many, what they left after their debts, the tax the same bands put on them, and the debts nothing was left to pay.

## The save

`reallife:dynasty`, version 1: the seed, the generation, the will and its bequests, the dated gifts, a handover under way (its step and its whole statement), the statements kept, the family's ledger, the retired founder, the debt for tax, the rivals' lines, the city's estates, the lineage, the paper's pieces and the days the children were seen grown. A save without it loads with no will. Every number is written and read in the invariant culture and every name is escaped.

**A handover** is frozen when it begins: the estate is valued once and its statement fixed. Its steps, a second apart, each do one thing and say so on the record (the tax, what is kept, each bequest, the heir), so a save, a load or the option off in the middle goes on at the step and nothing is paid twice.

## Left out, with the reason

- **Contesting a will.** The courts take a civil claim against the player (`DebtClaims.Lodge`), but a judgment is a debt with no claimant (`JusticeDirector.Court.cs`, `JudgeSuit`: `AddDebt(JusticeDebt.Civil, ...)`), so nothing a slighted relative won could reach them. The heir's reserved half stands in its place.
- **Selling assets to pay the tax.** What to sell is the heir's to decide: the tax the cash does not cover is owed with interest instead.
- **Any change to how the heir looks.** The heir is played in the body the save has.

## Tests

Options, Tests, Dynasty: the estate statement for today (changes nothing); reach the age the will sets for retiring; the last days now (hands over, cannot be put back); a rival tycoon hands over now. Each writes one `Dynasty check (...)` line.

Not verified in the game: the app in both frames, the notices, the handover itself (the name and the age taken by the game's own screens, the family and the people after it), the game's own ageing drawing the heir's age at the next midnight, the rich list and paper lines, and that a time skip of more than two game days inside one second of real time does not reach the game's own end first.
