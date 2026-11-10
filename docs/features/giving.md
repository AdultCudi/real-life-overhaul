# Giving

## In a sentence

The player can give to three of the city's charities, once or every month, with the gifts to charities deducted from their tax within a cap, and can give or lend money to the people they know, who repay a loan from what their household can spare; people in real trouble ask for help now and then, the rival tycoons give to the same charities, and the city's people help one another.

## Where the code is

- Folder: [`Scripts/Giving`](../../Assets/Mods/RealLifeOverhaul/Scripts/Giving/) (namespace `RealLifeOverhaul.Giving`). The kit's page is `Scripts/Ui/Ventures/GivePages.cs` (game-free) and `GivingLive.cs`; the call's money is `Scripts/Social/PersonDialog.Giving.cs`; the wiring is `Scripts/Wiring/Wiring.GivingLinks.cs`.
- Option: "Donations to charities and help for people" (`giving`, on by default). Off: nothing runs, no standing gift is paid, no loan is asked for, the app is not on the phone, and the line in the save stays as it was and goes on when the option is back on.
- Checks: EconomyCheck `--giving` (`GivingChecks.cs`).

```
GivingCatalogue            every figure, each with its unit and its source
GivingState, GivingStore   the records and the save line reallife:giving (version 1)
GivingRules                game-free: steps, months, the deduction, standing, a person's warmth, loans, asks, rivals
GivingPeople               game-free: money that reaches households and people through the population's own fields
GiveFacts, GiveResult      what the pages read; how a gift ended
GivingDirector (+ parts)   the timer (a second), the day, the month in steps, gifts, loans and asks, facts, tests, the app
GivingGame                 the one file that touches the game: cash, the game's list of deductions, the population, notices
GivingApp                  the app in the game's own frame; the kit's page is Ui/Ventures/GivePages
```

## What the player does

1. **Charities.** The Giving app (Charities tab) lists three charities. Choose one, an amount from the steps (50 to 100,000 at the start's prices, following the city's prices), then **Give**, or **Give every month**. A standing gift is paid on the first day of each month and stopped from the same page. One a charity.
2. **People.** On a call with someone the player knows, **Something else** offers **Give money** and **Lend money**, each with the amounts the cash and the rules allow, one at a time (**A different amount**).
3. **Asks.** Someone close who is out of work, owes more than a month's income or pays a rent over the line sends a text asking for an amount. The call offers **Give** what they ask, **Another answer** (half of it, then a loan of it, then **Say no**).
4. **Loans.** The Loans tab lists every loan, open and closed.
5. **The year.** The This year tab shows the gifts to charities, the income so far, the cap on the deduction, what is deducted, the neighbours' giving and the rivals'.

## The charities

| Charity | Whom it helps | What it changes |
| --- | --- | --- |
| Harbour Food Bank | households owing more than a month's income whose savings do not cover a quarter of it, the worst off first | the household's debt (`HouseholdTable.Debt`), its content a little |
| Stoop Housing Trust | tenants whose rent took over 45% of last month's income (`PopulationCatalogue.RentBurdenMove`, where the population moves a household) | the part of the rent over the line goes to the household's debt, then its savings |
| Second Chance Training Fund | people out of work with the least skill, not another feature's | skill +3 a course (`PersonTable.Skill`), and the population pays a job by skill (`LabourRules.PayFor`) |

A charity holds what it is given in a pot and pays out once a month under a cap: a sum for each household of the city (14, 10 and 4 at the start's prices; Giving USA 2023 puts human services at some 12% of American giving, and Americans give some 2% of an income of some 6,000 a month), at most 48 households or people a month, a household at most 1,500. The rivals' gifts go in the same pots, so the cap is shared. What it did is on its page: held now, last month's pay-out, the year's. The player's own household and the tenants the player lets a flat to are never helped, so no charity money returns as rent.

Left out, with the reason: **an animal charity** (the mod has no pets), **a health charity** (health is on hold), **a school fund** (a council school has no lever money moves: its teachers' skill follows the council's budget lever, `SchoolRules.CouncilSkill`, and the campus is behind its own option), **an arts fund and a clean streets fund** (their only effects would be moods of the player's own, `ModState.CivicClean` and `CivicFestive`, and the council's sweepers belong to Civic's lever).

## Money

- Gifts and loans leave the player's cash through `BizBooks.Book` only. Only the **spare** cash can be given: the wallet less what the investors of the player's firms are owed (`TechDirector.FloorNow`), so a gift never takes the wallet under the firm account's floor and is never counted as misuse ([startup money](startup-money.md)).
- A gift to a person lands in their household: its debt first, then its savings. A person the population does not hold gets the gift as warmth alone, and the call says so. Loans are made only to residents, who are the ones who can repay.
- Standing gifts that cannot be paid (no spare cash) are skipped and said once; they are never forced.
- Going Public: in a session the city's people are the host's, so charities, loans and the people's help stand down (the gift would change another player's people); the rivals rest. Everything is seeded by the save's own seed through a fixed hash; nothing is sent.

## The tax return

Gifts to charities (and only those) are deducted from the game's own tax, as the game deducts a business's costs (`TaxHelper.TrackTransaction`), under the name **Charitable giving**, so it is its own line on the tax statement. The cap is 6% of the year's income (`GivingCatalogue.DeductionShare`; the US allows cash gifts up to 60% of adjusted gross income, IRS Publication 526, and a business keeps about a tenth of its sales), taken on the game's business sales of the days gone, which only grow, so a claim made early is never over the cap of the whole year. Each day the director claims `min(gifts, cap) - what the game's list already holds under that name`; reading the game's own list means a year is never claimed twice, and on the day the game clears its list (`day % daysPerYear == 0`) nothing is claimed. Gifts to people, loans and a loan written off are never counted.

## Standing, the press and warmth

- A month's gifts earn integrity (`FameCause.Charity`) by `3 x gift / (gift + 5,000)` points and recognition from the press by `8 x gift / (gift + 20,000)` tenths. Only the newly earned amount is asked each time, so ten gifts of 1,000 earn what one of 10,000 earns.
- A gift of 25,000 or more is printed in the City Ledger the morning after; 100,000 or more leads the front page.
- A person's gifts in a 30-day window earn up to 6 points of affection and 4 of trust by the same curve (half at 1,000), counted half when the person was in no need.

## Loans

No interest; paid back in six monthly parts, each the least of the part, what is owed and what the household can spare (savings less a month's rent). A part that cannot be paid is a miss; three months in a row with nothing paid and the loan is written off, with a loss of warmth and no court case (the mod's justice system has no small claims path for the player as claimant). At most eight open, one a person, 10,000 the most.

## Who asks

A friend or family member whose household is in trouble as the population holds it (out of work, debt past a month's income, a rent over the line) may text for help in the social life's hourly round, when no plan or offer is waiting: at most one ask every 14 days, none of one person within 60 days, drawn from a hash of the save's seed, the person and the hour; 1 in 10,000 an hour, up to twice that for a rich and well known player.

## The rivals and the people

Each active tycoon gives once a month about 0.5% of their worth a year, moved by a rate of their own within 60% either way (seeded), to their favourite charity and now and then another. It comes out of their worth and goes in a pot; it shows as a line on the rich list and the tycoon's panel and, from 25,000, in the paper. The people of the city: each month a household with plenty (savings past a year of income) gives 2% of them to a household in hardship, one gift for each 400 households, every dollar leaving one household and reaching another.

## The save

`reallife:giving`, version 1: the seed, the tax year's sums, the three pots with their year, a standing gift per charity, the loans, what was given to each person, the rivals' giving and the paper's notes. A save without it loads with nothing given.

## Tests

Options, Tests, Giving: a small gift to a charity (and the deduction asked for); the month's pass now; a small gift to the first person you know; a small loan to them; someone in trouble asks; put giving back as before. Each writes one `Giving check (...)` line.

Not verified in the game: the call's dialogue pages, the app in both frames, the tax statement's line.
