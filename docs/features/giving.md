# Giving

## In a sentence

The player can give to seven of the city's causes (a food bank, a housing trust, a training fund, a night shelter, a scholarship fund, a care fund and a district's upkeep), once or every month, in sums as large as their cash allows, with big gifts taking the giver's name in the City Ledger and the gifts to charities deducted from their tax within a cap and carried forward over years; they can help the people they know (give or lend money, pay the rent, clear a debt, back a small business, pay for a course, buy a car or a home) and some people refuse; people in real trouble ask for help now and then and somebody helped again and again leans on the player and asks for more; the rival tycoons give to the same causes, and the city's people help one another.

## Where the code is

- Folder: [`Scripts/Giving`](../../Assets/Mods/RealLifeOverhaul/Scripts/Giving/) (namespace `RealLifeOverhaul.Giving`). The kit's page is `Scripts/Ui/Ventures/GivePages.cs` (game-free) and `GivingLive.cs`; the call's money is `Scripts/Giving/GivingTalk.cs`, held by `Scripts/Social/PersonDialog.Giving.cs` (a call) and `Scripts/Social/PeopleDialog.cs` (a person's page); the wiring is `Scripts/Wiring/Wiring.GivingLinks.cs`, with the care fund in `Wiring.HealthLinks.cs` and the partner's car in `FamilyLinks.cs`.
- Option: "Donations to charities and help for people" (`giving`, on by default). Off: nothing runs, no standing gift is paid, no loan is asked for, no ask is sent, the app leaves the phone (the way the other mod apps leave it, `PhoneApp.Remove`), and the line in the save stays as it was, a month's pass half done included, and goes on when the option is back on and the app is put back.
- Checks: EconomyCheck `--giving` (`GivingChecks.cs`, and `GivingHelpChecks.cs` for the newer causes and the help for people).

```
GivingCatalogue            every figure, each with its unit and its source
GivingState, GivingStore   the records and the save line reallife:giving (version 1)
GivingRules                game-free: steps, months, the deduction's cap, standing, a person's warmth, loan parts, asks, leaning, rivals
GivingYear                 game-free: the game's tax year (when it turns, what is claimed, the carry forward, the game's list read back)
GivingMonth                game-free: the month's pass in its steps, kept whole across a save, a load and the option off
GivingLoans                game-free: who may be lent to, the loan made (an ordinary one or a business loan), a loan's month
GivingPeople               game-free: money that reaches households and people through the population's own fields; the causes' monthly passes
GivingHelp                 game-free: how a person answers an offer, what each kind of help costs and what it changes in the household and the person
GiveFacts, GiveResult      what the pages read; how a gift ended
GivingDirector (+ parts)   the timer (a second), the day, the month in steps, gifts, help, loans and asks, facts, tests, the app
GivingTalk                 the ways to help, one at a time, in the game's own dialogue panel (a call and a person's page alike)
GivingGame                 the one file that touches the game: cash, the game's list of deductions, the population, the districts, the cars on sale, notices
GivingApp                  the app in the game's own frame; the kit's page is Ui/Ventures/GivePages
```

## What the player does

1. **Causes.** The Giving app (Charities tab) lists seven. Choose one, an amount from the steps, then **Give**, or **Give every month**. A standing gift is paid on the first day of each month and stopped from the same page. One a cause. For a district's upkeep choose the district first.
2. **Bigger gifts.** The steps run from 50 to 500 million at the start's prices (following the city's prices). Everyone is shown the first eleven, to 100,000; a richer player is shown every step up to the cash they can give, so the most a gift can be follows their wealth. A single gift of 250,000 or more takes the giver's name (see below).
3. **People.** On a call, **Something else**, then **Help with money** (or on the person's own page in People: **History**, then **Money**) offers the ways to help, one at a time: **Give money**, **Lend money**, **Pay the rent**, **Clear a debt**, **Back a business**, **Pay for a course**, **Buy a car** and **Buy them a home**, each with its price under the button. They are offered only where the city's people are there to receive the money: not in a Going Public session, not with the living population off.
4. **Asks.** Someone close who is out of work, owes more than a month's income or pays a rent over the line sends a text asking for an amount. The call offers **Give** what they ask, **Another answer** (half of it, then a loan of it, then **Say no**). A gift or a loan to the person who is asking, by this answer or by plain **Give money**, closes the ask. Somebody the player has helped again and again may ask for more with no real need (see "Leaning").
5. **Loans.** The Loans tab lists every loan, open and closed.
6. **The year.** The This year tab shows the gifts to charities this tax year, the income so far, the cap on the deduction, what is deducted (what the game's tax will deduct), what is carried forward, the neighbours' giving, the rivals' and the halls and wings that carry the player's name.
7. **People.** The People tab lists everyone the player has given to, lent to, helped or said no to: what each is short of, how they lean on the player, what the rent paid for them still covers, what they owe.

## The causes

| Cause | Whom it helps | What it changes |
| --- | --- | --- |
| Harbour Food Bank | households owing more than a month's income whose savings do not cover a quarter of it, the worst off first | the household's debt (`HouseholdTable.Debt`), its content a little |
| Stoop Housing Trust | tenants whose rent took over 45% of last month's income (`PopulationCatalogue.RentBurdenMove`, where the population moves a household) | the part of the rent over the line goes to the household's debt, then its savings |
| Second Chance Training Fund | people out of work with the least skill, not another feature's | skill +3 a course (`PersonTable.Skill`), and the population pays a job by skill (`LabourRules.PayFor`) |
| Lantern Night Shelter (the homeless) | tenants of the city's landlords whose rent takes 70% of their income or more (`PopulationCatalogue.RentBurdenLeave`), the point past which the population drives a household out of the city when it finds nothing cheaper | the shelter pays the gap between their rent and what they can bear for up to six months and holds the tenancy at that rent as a regulated tenancy (`HouseholdFlags.Regulated`, the population's own kept-rent mark), clearing the wish to move; its content +6 |
| Open Doors Scholarship Fund (schools) | households in debt with a pupil or a student, a student's first, the worst off first | the debt (at most 2,500 a household), and +2 skill for each student in it, so the pay they leave university at is better |
| Riverside Care Fund (health) | the city's households whose own share of a care bill is met (Health's people's pass asks as each case falls on a household) | 70% of a household's own share (what its plan leaves) is paid before it reaches the household's savings and debt, up to the pot and the month's cap |
| Neighbourhood Upkeep Fund (a district's upkeep) | the district the player names (the seven of New York the mod knows that this city has) | at once: neighbourhood standing in that district (`FamePart.Neighbourhood`, up to 6 points by the gift) and more custom in its shops for 5, 10 or 20 days by the gift (`FameCity.DistrictBoost`, 3 per cent, the same as the city's sponsored event); it holds no pot |

A cause with a pot holds what it is given and pays out once a month under a cap: a sum for each household of the city (14, 10, 4, 6, 5 and 8 at the start's prices for the first six; Giving USA 2023 puts human services at some 12% of American giving, and Americans give some 2% of an income of some 6,000 a month), at most 48 households or people a month, a household at most 1,500 (the shelter 6,000, the school fund 2,500). The rivals' gifts go in the same pots, so the cap is shared (and never to a district's upkeep, which holds none). What a cause did is on its page: held now, last month's pay-out, the year's. The player's own household and the tenants the player lets a flat to are never helped, so no charity money returns as rent.

Left out, with the reason: **an animal charity** (the mod has no pets) and **an arts fund and a clean streets fund** (their only effects would be moods of the player's own, `ModState.CivicClean` and `CivicFestive`, and the council's sweepers belong to Civic's lever). A **health** cause is built, but not as a clinic of the player's own: health stays on hold for that, and the fund works only through Health's bills for the city's households.

## Money

- Gifts, help and loans leave the player's cash through `BizBooks.Book` only, and every kind is a bank line of its own with a label (`reallife_transaction_giving_*` and `_label`): a gift to a charity, a monthly gift, a naming gift, a gift to a district's upkeep, money given to a person, rent paid, a debt cleared, a business backed, a loan to back a business, a course, a car, a home, a loan, a loan paid back and the deduction. The Finance pages count them as they count every line, and the tax page counts the deduction.
- Only the **spare** cash can be given: the wallet less what the investors of the player's firms are owed (`TechDirector.FloorNow`), so a gift never takes the wallet under the firm account's floor and is never counted as misuse ([startup money](startup-money.md)).
- A gift to a person lands in their household: its debt first, then its savings. A person the population does not hold gets a plain gift as warmth alone, and the call says so; every other kind of help needs a household the player may help and is refused otherwise. Loans are made only to residents, who are the ones who can repay.
- The investors' floor is held in cents and a gift is whole money, so the reserve is rounded up (`GivingRules.Reserve`): a gift of exactly the spare cash never leaves the wallet a cent under the floor.
- Standing gifts that cannot be paid (no spare cash) are skipped and said once; they are never forced.
- Going Public, and the living population off: the city's people are the host's in a session, and absent with the option off. Causes, help, loans and gifts to people then stand down (a gift would change another player's people, or land nowhere), and **no money is taken** for a gift or a loan to a person (the call says why); the rivals rest in a session. Everything is seeded by the save's own seed through a fixed hash; nothing is sent.

## The tax return

Gifts to charities (every cause, a district's upkeep included, and only those) are deducted from the game's own tax, as the game deducts a business's costs (`TaxHelper.TrackTransaction`), under the name **Charitable giving**, so it is its own line on the tax statement. The game keeps its list by the name it is given and writes the statement from it when the tax falls due, translating the name if it is a key it knows (`TaxHelper.GetDeductibleExpenseLabel`), as it does for its own deductions; giving passes its key (`reallife_giving_deduct`) whenever the game's text tables hold it, so a change of language reaches the statement, and the words of the language in use if they do not. A save loaded without the mod shows the key as it is.

The game's year (`GameManager.NewDay`: the day goes up, then `TaxHelper.RunDaily`) works the tax out and clears the list on the day that divides by the year's length, `D`. So giving's year is the game's: it runs from such a day to the day before the next, a gift on any of its days, the last and the first included, is in the list the game reads, and the sums of the page start again on `D` itself (`GivingYear.Turn`, before anything is counted). The cap is 6% of the year's income (`GivingCatalogue.DeductionShare`; the US allows cash gifts up to 60% of adjusted gross income, IRS Publication 526, and a business keeps about a tenth of its sales), taken on the game's business sales of the days after `D` up to yesterday (the game writes a day's summary the day after, and its own sum is over those days), which only grow within a year: the game keeps a year's summaries until a year and a day after, so none of this year's can be dropped. A gift is claimed **at once** at the end of the gift, and again each day as the income grows: `GivingYear.ClaimToday` claims `min(gifts + carry, cap) - what is claimed already`, where what is claimed already is the greater of what the game's list holds under giving's name (the key or the words in use) and giving's own saved record of what it claimed (`TaxClaimed`). The record is the floor, so a change of language (which can leave an old line in the list under words that are no longer read), a save, a load or a cap that falls can never make the same gift be claimed twice, and a claim is never taken back. Nothing is claimed for a year after its day has passed (the option off across the turn): the list is cleared by then. The cap is worked on the income known the day before the gift, so the last day's sales are not in the cap of the year's last claims. If the player leaves the tax unpaid across a clearing day of a very short year, the game does not clear its list (`TaxHelper.RunDaily` skips the boundary while taxes are unpaid) and the new year's claim is held back by what the list still holds, which is the safe way. Gifts to people, loans and a loan written off are never counted.

**The carry forward.** A big gift is mostly over the cap. As real relief carries an excess forward for five years (IRS Publication 526), what the cap held back of a year's gifts is carried to the next years (`GivingYear.CarryOn`, as the year turns): five places, the latest year first, the oldest lapsing. The claim uses this year's gifts before it touches the carry and the oldest carry first (`DrawCarry`, worked afresh from `CarryTaken`, so it never takes the same carry twice), and what is claimed in a year never passes the cap or the gifts there were. The This year tab shows what is carried.

## Naming, standing, the press and warmth

- A month's gifts earn integrity (`FameCause.Charity`) by `3 x gift / (gift + 5,000)` points and recognition from the press by `8 x gift / (gift + 20,000)` tenths. Only the newly earned amount is asked each time, so ten gifts of 1,000 earn what one of 10,000 earns. Fame holds a part to 8 points a day and recognition to its top; what Fame really applied is what is counted as earned, and a part it held back is asked again with the next gift of the month.
- **Naming.** A single gift of 250,000 or more (at the start's prices; never a standing gift) takes the giver's name: a kitchen, a housing wing, a training centre, a shelter, a scholarship, a wing or a square in the district (`GivingCatalogue.NamingKeys`, with the player's own name). It is kept on the page (the last twelve), it leads the City Ledger the morning after (inserted after the masthead), and Flare's account of the City Ledger takes the paper's first headlines, so the naming is Flare's first post of that morning. It earns once 2 points of integrity and 2 points of recognition (`GivingCatalogue.NamingIntegrity`, `NamingPress`) through Standing's own figures, on top of the month's curve.
- A gift of 25,000 or more is printed in the City Ledger the morning after; 100,000 or more leads the front page.
- A person's gifts and help in a 30-day window earn up to 6 points of affection and 4 of trust by the same curve (half at 1,000), counted half when the person was in no need. A loan counts for half of a gift of the same size (it comes back) in the same window, so a tiny loan again and again earns what one loan of their sum would; a repayment earns no warmth, and a default costs some. A home worth 400,000 earns no more in a window than a gift of 10,000 does.

## Helping a person

The way: `GivingTalk` (a call and a person's page alike), `GivingDirector.Help`, and the rules in `GivingHelp`. Each answer shows its price under the button; the person answers first, and only then is money taken.

| Help | Costs | What it changes |
| --- | --- | --- |
| Give money | 50 up to the most (10,000 at the start, doubling for each wealth step the player has passed, to 320,000) | the household's debt first, then its savings, its content a little (`GivingPeople.Help`) |
| Lend money | the same | the same, and a loan of six parts with no interest (below) |
| Pay the rent | one, three or six months of their rent | the money in the household's savings, its content a little; their rent is theirs to pay again after those months (`PersonGiving.CoveredUntil`), and until then they do not ask for the rent |
| Clear a debt | all of their household's debt, or half of it, to the most | the debt falls by the sum |
| Back a business | a step from 1,000 up to the most; a gift, or a loan of 12 parts with 4% on top | the money in their savings, +4 skill (so their pay follows), and for somebody **out of work** the business is their work: they are in work with no employer (as the player's staff with no place yet are) at 70% of what their skill fetches |
| Pay for a course | 1,200 | +5 skill, and for a worker or somebody out of work the pay their skill fetches follows (`LabourRules.PayFor`) |
| Buy a car | one of three, by the game's own cars on sale (a quarter, the middle and nine tenths of the list by price) | the household holds one more car (`HouseholdTable.Cars`, to two, which the population reads for what it spends on a car), its content +6; for the **partner** who lives with the player the car is the family's own conversation (`PartnerCar`: one of the player's cars, or a new one at the game's own price), offered whether or not they asked, and never in a Going Public session |
| Buy them a home | 20 years of the flat's market rent | the household owns its flat as the population's owners do (`Landlords.Own`: an owner's upkeep and tax, no mortgage, no wish to move, no regulated tenancy), its content +10; only for a tenant of the city's landlords |

**Where a person can hold property.** The mod keeps no property for a person of the social cast; the population keeps it for their household. So a car and a home are given to the household the person is a resident of, in the fields the population already reads (`Cars`, `Landlord`), and the lasting effect is in that household's spending and rent. The player's own household and a partner who lives with the player have no such household of their own: the partner's car is the family's, and a home for them is the player's own homes, so none is offered.

**Who refuses, and why** (`GivingHelp.Answer`; nothing is taken from the player when they do, and the call says why in the person's words):

- Not close enough: affection and trust together under a line for the kind (rent and debt 70, a course 60, a business 90, a car 115, a home 130; a friend is 50, a close friend 115). A car or a home only from a close friend, a partner or family.
- Generous and in no need: would rather give than take (not for a car, a home or a business).
- Proud (about one person in five, by the save's seed): refuses the first gift over a small kindness (500 at the start's prices), takes it ten days later, takes a loan or a small kindness at once, and has no pride left once they have taken any help.

**What money cannot buy.** The warmth a gift earns goes through the same window and the same curve whatever its kind and size, so there is a most (6 points of affection, 4 of trust in 30 days); help never touches familiarity or the stage of a friendship or a romance; an envious person feels half the trust.

**Leaning.** Each help counts on the person's dependence as it stands (a point lighter for each 30 days since the last), to 5. Nobody asks for 21 days after being helped; after that the chance of an ask rises 30% for each point of dependence, and from two points somebody who is not the reliable sort may ask for **more** with no real need, 80% of their last help. The call says so (`reallife_giving_plea_more`). The player can say no: the **first no costs nothing**, every later one costs a tenth of a point of affection.

## Loans

No interest; paid back in six monthly parts, each the least of the part, what is owed and what the household can spare (savings less a month's rent). A **business loan** is paid back over twelve parts and owes 4% of the sum on top, once. A part that cannot be paid is a miss; three months in a row with nothing paid and the loan is written off, with a loss of warmth and no court case (the mod's justice system has no small claims path for the player as claimant). At most eight open, one a person, up to the most (10,000, doubling for each wealth step). The rules are `GivingLoans` (who may be lent to, the loan made, a month's turn); the director books the player's money and tells the person.

## Who asks

A friend or family member whose household is in trouble as the population holds it (out of work, debt past a month's income, a rent over the line, the rent not paid for them) may text for help when the social life's hourly round has picked them to write to and no plan or offer is waiting: at most one ask every 14 days, none of one person within 60 days, none within 21 days of being helped, drawn from a hash of the save's seed, the person and the hour. The chance is **1 in 1,000 an hour** for each such person (`GivingCatalogue.AskChance`, 10 in ten thousand), up to twice that for a rich and well known player, and more for somebody who leans on the player's help. That is rare and never spamming: the draw is made only when the social life's hour has picked the person to write to, and never more than one ask comes in a fortnight, whoever is asking.

## The rivals and the people

Each active tycoon gives once a month about 0.5% of their worth a year, moved by a rate of their own within 60% either way (seeded), to their favourite cause and now and then another. It comes out of their worth and goes in a pot, but only out of the worth over their floor, the start's worth at today's prices that the rivals raise a worth to every day and when a tycoon comes forward (`RivalRules.Grow`, `Activate`): a gift from the floor itself would be made up by the next morning, so a tycoon at the floor gives nothing and one just over it gives only the part over. What leaves them is exactly what goes in the pot. It shows as a line on the rich list and the tycoon's panel and, from 25,000, in the paper. The people of the city: each month a household with plenty (savings past a year of income) gives 2% of them to a household in hardship, one gift for each 400 households, every dollar leaving one household and reaching another.

## The save

`reallife:giving`, version 1: the seed, the tax year's sums (the year's start is the game's clearing day; a start saved by an earlier version, one past it, is read as the clearing day), the step the month's pass has reached and its month, the seven pots with their year, a standing gift per cause with the month its turn last came (and its district), the loans (with their parts and what a business loan adds), what was given to each person (and how they lean, when they refused, how long their rent is paid, the nos), the rivals' giving, the paper's notes, the names the big gifts carry, the carry of the deduction and the gifts to each district. A save without it loads with nothing given, and a line from before the newer parts loads as it was (no carry, no names, loans of six parts, nothing leaning). Every number is written and read in the invariant culture, so a language with a decimal comma saves and loads the same line.

**The month's pass** is a step for each cause and three more, a second apart (the counters start again, the standing gifts, the rivals' gifts, one pay-out for each cause, the neighbours). The month is marked done when the last step is, and the step it has reached is in the save, so a save, a load or the option off in the middle goes on at that step. A step that could run twice says what it has done on its own record (a standing gift its month, a rival its month, a cause the month it paid out in, the neighbours theirs), so a standing gift is never paid twice in a month, a rival never gives twice and a pot never pays out twice. A standing gift set during the month's pass is first paid the month after. A line from before the pass went by steps loads with none under way. The care fund pays as bills come, and a district's upkeep when it is given; both close their month with the counters.

## Every constant, and why

Money is whole money at the start's prices unless it says otherwise. The older constants are in the sections above; these are the ones the newer causes and the help for people added (`GivingCatalogue`).

| Constant | Value | Why |
| --- | --- | --- |
| `Steps`, `StepsBase` | 50 to 500,000,000; 11 shown to everyone | a billionaire gives in sums that mean something to them; a poorer player is not shown sums that mean nothing |
| `AmountMost` | half of int | no sum overflows; the game's cash is a float, far under it |
| `CapPerHousehold` | 14, 10, 4, 6, 5, 8, 0 | the first three from Giving USA 2023's human services share; the shelter is dearer a head, the school fund and care fund by their share of giving; a district's upkeep holds no pot |
| `ShelterMonths` | 6 | the months of the gap the shelter pays before a tenancy is held at a rent the household can bear (a regulated tenancy the population already has) |
| `ShelterMost` | 6,000 | a year of a fair rent to a family; one household's holding costs at most this, the months fewer when the gap is wide |
| `ShelterContent` | 6 | three times a charity's lift, for keeping a home is the worse fear |
| `ScholarMost` | 2,500 | a term's fees and books against one household's debt |
| `ScholarSkill` | 2 | a student's pay when they leave follows their skill |
| `CareShare` | 700 (70%) | the share of a household's own bill (what its plan leaves) the fund pays; the standard plan pays half to 85% of the rest |
| `UpkeepPointsMost`, `UpkeepHalf` | 6, 10,000 | the city's own sponsored event gives 4 points; a gift to upkeep is spent at once, by a curve |
| `UpkeepFrom`, `UpkeepDays` | 1,000, 10,000, 100,000; 5, 10, 20 days | the city's sponsored event runs 5 days; a bigger gift lasts longer |
| `UpkeepBoost` | 3 per cent | the same as the city's sponsored event; Standing holds all boosts to 4 |
| `NamingFrom` | 250,000 | a few years of a rich household's giving: a hall is named for a gift of that size |
| `NamingPress`, `NamingIntegrity` | 20 tenths, 2 points | the city sees the name every day; once, through Standing's own figures |
| `NamingsKept` | 12 | the page keeps the last twelve |
| `CarryYears` | 5 | the US carries an excess of the deduction forward five years (IRS Publication 526) |
| `CourseCost` | 1,200 | a term at a city college |
| `CourseSkill` | 5 | more than the fund's 3: this is the whole course, not a share of it |
| `BusinessSkill` | 4 | they learn by running it |
| `BusinessPayShare` | 700 (70%) | in a business of their own somebody out of work earns less than their skill fetches at first |
| `BusinessLeast` | 1,000 | below it is no business |
| `BusinessParts`, `BusinessInterest` | 12 months, 40 (4%) | a business needs longer to pay back than a rent does; a bank's small business rate is about 7% a year and a friend asks less |
| `CarsMost` | 2 | the population's own most (`GenerateRules`) |
| `CarContent`, `HomeContent` | 6, 10 | a car of their own, and a home with no rent and nobody to put them out |
| `HomePriceYears` | 20 | the price to rent ratio of New York is about 20 to 25 |
| `HelpLeast` | 0, 70, 70, 90, 60, 115, 130 | closeness (affection and trust together, 0 to 200) wanted before taking money, rent, a debt, a business, a course, a car, a home; a friend is 50, a close friend 115 |
| `ProudShare` | 200 (20%) | about one person in five is proud, by the save's seed |
| `ProudAbove` | 500 | a proud person takes a small kindness |
| `ProudGapDays` | 10 | their pride eases after a short while |
| `EnviousTrustShare` | 500 (50%) | money from the rich is not warmly taken by the envious |
| `DependDecayDays`, `DependMost` | 30 days, 5 | somebody helped long ago has found their feet |
| `HelpCooldownDays` | 21 | nobody asks again straight after being helped |
| `MoreFrom` | 2 | from here somebody asks for more with no real need |
| `AskPerDependence` | 300 (30%) | the likelier an ask the more they lean |
| `MoreShare` | 800 (80%) | they ask for a share of their last help |
| `FreeNos`, `NoCost` | 1, 10 tenths | the first no costs nothing; later ones a tenth of a point of affection |

## Tests

Options, Tests, Giving: a small gift to a cause (and the deduction claimed with it, a district's upkeep in turn); the month's pass now (no new one while one is under way); a small gift to the first person you know; a small loan to them; someone in trouble asks; help for the first person who needs it (a course, a month's rent or half a debt; the person may refuse); a hall named after you with nothing paid; put giving back as before. Each writes one `Giving check (...)` line.

Not verified in the game: the call's dialogue pages and the person's money page, the app in both frames and leaving the phone with the option, the tax statement's line (that the game's text tables hold giving's key, so the statement is translated; if they do not, the words are written, which the checks cover), the rich list and paper lines, Flare taking a naming as its first headline, Health's bills falling on the care fund, the shelter holding a tenancy through the population's month, and the partner's car from Help with money.
