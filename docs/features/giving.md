# Giving

## In a sentence

The player can give to three of the city's charities, once or every month, with the gifts to charities deducted from their tax within a cap, and can give or lend money to the people they know, who repay a loan from what their household can spare; people in real trouble ask for help now and then, the rival tycoons give to the same charities, and the city's people help one another.

## Where the code is

- Folder: [`Scripts/Giving`](../../Assets/Mods/RealLifeOverhaul/Scripts/Giving/) (namespace `RealLifeOverhaul.Giving`). The kit's page is `Scripts/Ui/Ventures/GivePages.cs` (game-free) and `GivingLive.cs`; the call's money is `Scripts/Social/PersonDialog.Giving.cs`; the wiring is `Scripts/Wiring/Wiring.GivingLinks.cs`.
- Option: "Donations to charities and help for people" (`giving`, on by default). Off: nothing runs, no standing gift is paid, no loan is asked for, the app leaves the phone (the way the other mod apps leave it, `PhoneApp.Remove`), and the line in the save stays as it was, a month's pass half done included, and goes on when the option is back on and the app is put back.
- Checks: EconomyCheck `--giving` (`GivingChecks.cs`).

```
GivingCatalogue            every figure, each with its unit and its source
GivingState, GivingStore   the records and the save line reallife:giving (version 1)
GivingRules                game-free: steps, months, the deduction's cap, standing, a person's warmth, loan parts, asks, rivals
GivingYear                 game-free: the game's tax year (when it turns, what is claimed, the game's list read back)
GivingMonth                game-free: the month's pass in its steps, kept whole across a save, a load and the option off
GivingLoans                game-free: who may be lent to, the loan made, a loan's month
GivingPeople               game-free: money that reaches households and people through the population's own fields
GiveFacts, GiveResult      what the pages read; how a gift ended
GivingDirector (+ parts)   the timer (a second), the day, the month in steps, gifts, loans and asks, facts, tests, the app
GivingGame                 the one file that touches the game: cash, the game's list of deductions, the population, notices
GivingApp                  the app in the game's own frame; the kit's page is Ui/Ventures/GivePages
```

## What the player does

1. **Charities.** The Giving app (Charities tab) lists three charities. Choose one, an amount from the steps (50 to 100,000 at the start's prices, following the city's prices), then **Give**, or **Give every month**. A standing gift is paid on the first day of each month and stopped from the same page. One a charity.
2. **People.** On a call with someone the player knows, **Something else** offers **Give money** and **Lend money**, each with the amounts the cash and the rules allow, one at a time (**A different amount**). They are offered only where the city's people are there to receive the money: not in a Going Public session, not with the living population off.
3. **Asks.** Someone close who is out of work, owes more than a month's income or pays a rent over the line sends a text asking for an amount. The call offers **Give** what they ask, **Another answer** (half of it, then a loan of it, then **Say no**). A gift or a loan to the person who is asking, by this answer or by plain **Give money** or **Lend money**, closes the ask.
4. **Loans.** The Loans tab lists every loan, open and closed.
5. **The year.** The This year tab shows the gifts to charities this tax year, the income so far, the cap on the deduction, what is deducted (what the game's tax will deduct), the neighbours' giving and the rivals'. The year is the game's: it begins on the day the game clears its list of deductions.

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
- The investors' floor is held in cents and a gift is whole money, so the reserve is rounded up (`GivingRules.Reserve`): a gift of exactly the spare cash never leaves the wallet a cent under the floor.
- Standing gifts that cannot be paid (no spare cash) are skipped and said once; they are never forced.
- Going Public, and the living population off: the city's people are the host's in a session, and absent with the option off. Charities, loans and gifts to people then stand down (a gift would change another player's people, or land nowhere), and **no money is taken** for a gift or a loan to a person (the call says why); the rivals rest in a session. A person the population does run but does not hold as a household that can be helped (not yet linked to a resident, the player's own household, a tenant) is the one case that gets a gift as the relationship alone, and the call says so. Everything is seeded by the save's own seed through a fixed hash; nothing is sent.

## The tax return

Gifts to charities (and only those) are deducted from the game's own tax, as the game deducts a business's costs (`TaxHelper.TrackTransaction`), under the name **Charitable giving**, so it is its own line on the tax statement. The game keeps its list by the name it is given and writes the statement from it when the tax falls due, translating the name if it is a key it knows (`TaxHelper.GetDeductibleExpenseLabel`), as it does for its own deductions; giving passes its key (`reallife_giving_deduct`) whenever the game's text tables hold it, so a change of language reaches the statement, and the words of the language in use if they do not. A save loaded without the mod shows the key as it is.

The game's year (`GameManager.NewDay`: the day goes up, then `TaxHelper.RunDaily`) works the tax out and clears the list on the day that divides by the year's length, `D`. So giving's year is the game's: it runs from such a day to the day before the next, a gift on any of its days, the last and the first included, is in the list the game reads, and the sums of the page start again on `D` itself (`GivingYear.Turn`, before anything is counted). The cap is 6% of the year's income (`GivingCatalogue.DeductionShare`; the US allows cash gifts up to 60% of adjusted gross income, IRS Publication 526, and a business keeps about a tenth of its sales), taken on the game's business sales of the days after `D` up to yesterday (the game writes a day's summary the day after, and its own sum is over those days), which only grow within a year: the game keeps a year's summaries until a year and a day after, so none of this year's can be dropped. A gift is claimed **at once** at the end of the gift, and again each day as the income grows: `GivingYear.ClaimToday` claims `min(gifts, cap) - what is claimed already`, where what is claimed already is the greater of what the game's list holds under giving's name (the key or the words in use) and giving's own saved record of what it claimed (`TaxClaimed`). The record is the floor, so a change of language (which can leave an old line in the list under words that are no longer read), a save, a load or a cap that falls can never make the same gift be claimed twice, and a claim is never taken back. Nothing is claimed for a year after its day has passed (the option off across the turn): the list is cleared by then. The cap is worked on the income known the day before the gift, so the last day's sales are not in the cap of the year's last claims. If the player leaves the tax unpaid across a clearing day of a very short year, the game does not clear its list (`TaxHelper.RunDaily` skips the boundary while taxes are unpaid) and the new year's claim is held back by what the list still holds, which is the safe way. Gifts to people, loans and a loan written off are never counted.

## Standing, the press and warmth

- A month's gifts earn integrity (`FameCause.Charity`) by `3 x gift / (gift + 5,000)` points and recognition from the press by `8 x gift / (gift + 20,000)` tenths. Only the newly earned amount is asked each time, so ten gifts of 1,000 earn what one of 10,000 earns. Fame holds a part to 8 points a day and recognition to its top; what Fame really applied is what is counted as earned, and a part it held back is asked again with the next gift of the month.
- A gift of 25,000 or more is printed in the City Ledger the morning after; 100,000 or more leads the front page.
- A person's gifts and loans in a 30-day window earn up to 6 points of affection and 4 of trust by the same curve (half at 1,000), counted half when the person was in no need. A loan counts for half of a gift of the same size (it comes back) in the same window, so a tiny loan again and again earns what one loan of their sum would; a repayment earns no warmth, and a default costs some.

## Loans

No interest; paid back in six monthly parts, each the least of the part, what is owed and what the household can spare (savings less a month's rent). A part that cannot be paid is a miss; three months in a row with nothing paid and the loan is written off, with a loss of warmth and no court case (the mod's justice system has no small claims path for the player as claimant). At most eight open, one a person, 10,000 the most. The rules are `GivingLoans` (who may be lent to, the loan made, a month's turn); the director books the player's money and tells the person.

## Who asks

A friend or family member whose household is in trouble as the population holds it (out of work, debt past a month's income, a rent over the line) may text for help when the social life's hourly round has picked them to write to and no plan or offer is waiting: at most one ask every 14 days, none of one person within 60 days, drawn from a hash of the save's seed, the person and the hour. The chance is **1 in 1,000 an hour** for each such person (`GivingCatalogue.AskChance`, 10 in ten thousand), up to twice that for a rich and well known player. That is rare and never spamming: the draw is made only when the social life's hour has picked the person to write to, and never more than one ask comes in a fortnight, whoever is asking.

## The rivals and the people

Each active tycoon gives once a month about 0.5% of their worth a year, moved by a rate of their own within 60% either way (seeded), to their favourite charity and now and then another. It comes out of their worth and goes in a pot, but only out of the worth over their floor, the start's worth at today's prices that the rivals raise a worth to every day and when a tycoon comes forward (`RivalRules.Grow`, `Activate`): a gift from the floor itself would be made up by the next morning, so a tycoon at the floor gives nothing and one just over it gives only the part over. What leaves them is exactly what goes in the pot. It shows as a line on the rich list and the tycoon's panel and, from 25,000, in the paper. The people of the city: each month a household with plenty (savings past a year of income) gives 2% of them to a household in hardship, one gift for each 400 households, every dollar leaving one household and reaching another.

## The save

`reallife:giving`, version 1: the seed, the tax year's sums (the year's start is the game's clearing day; a start saved by an earlier version, one past it, is read as the clearing day), the step the month's pass has reached and its month, the three pots with their year, a standing gift per charity with the month its turn last came, the loans, what was given to each person, the rivals' giving and the paper's notes. A save without it loads with nothing given. Every number is written and read in the invariant culture, so a language with a decimal comma saves and loads the same line.

**The month's pass** is seven steps, a second apart (the counters start again, the standing gifts, the rivals' gifts, one pay-out for each charity, the neighbours). The month is marked done when the last step is, and the step it has reached is in the save, so a save, a load or the option off in the middle goes on at that step. A step that could run twice says what it has done on its own record (a standing gift its month, a rival its month, a charity the month it paid out in, the neighbours theirs), so a standing gift is never paid twice in a month, a rival never gives twice and a pot never pays out twice. A standing gift set during the month's pass is first paid the month after. A line from before the pass went by steps loads with none under way.

## Tests

Options, Tests, Giving: a small gift to a charity (and the deduction claimed with it); the month's pass now (no new one while one is under way); a small gift to the first person you know; a small loan to them; someone in trouble asks; put giving back as before. Each writes one `Giving check (...)` line.

Not verified in the game: the call's dialogue pages, the app in both frames and leaving the phone with the option, the tax statement's line (that the game's text tables hold giving's key, so the statement is translated; if they do not, the words are written, which the checks cover), the rich list and paper lines.
