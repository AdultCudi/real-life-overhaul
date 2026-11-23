# Economy: the Banks app and your own bank

## In a sentence

One Banks app on the phone for the city's three largest banks, each bank's page showing that bank's slice of the player's money, and late in a game a bank of the player's own, run from the headquarters, with branches of its own in shop units.

## Where the code is

- Folders: [`Scripts/Economy/Banking/BankApps`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Banking/BankApps/) and [`Scripts/Economy/OwnBank`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/OwnBank/).
- Read first: `Banking/BankApps/BanksDirector.cs`, `Banking/Terms/BankRules.cs`, `OwnBank/OwnBankDirector.cs` and `OwnBank/OwnBankRules.cs`.

## How it talks to other features

- The Banks app reads the game's own banks (`Banking/Loans/BankTerms.Lenders`) and keep no save data.
- `Wiring/OwnBankLinks.cs` gives your own bank Standing's integrity, the council's levers, the courts for a lender's claims and the Stocks app's quotes.

`Scripts/Economy/Banking/BankApps/`.
One app on the game's phone for the city's three largest banks, the bank picked under each page's title with the game's own selector, under the game's own name for the bank; each bank's page is that bank's slice of the player's money. (Until the phone had folders each bank had an app of its own: three buttons that were one app in all but the bank. They are one app now, in the Money folder, as a phone's banking app holds every account; see [the phone](phone.md).)
The Finance app stays the one page for every lender at once.
Option: Money > Banks app (`economy_banks`, on by default).
No save data.

## Which banks, and how they differ

The banks are the game's own (`BankTerms.Lenders`: every special-service building whose settings are a `BankSettings` and that has a registration).
Branches with the same name are one bank.
`BankRules.Rank` orders them by the most each lends on its own terms (`BankSettings.maxTotalLoanAmount` as first read, before the rating writes it), the largest first, by name among equals; `BankTerms.Banks` holds one branch of each in that order.

The first is `BankTerms.House`, the bank the mod's accounts have always been held with.
The game's New York has two banks (Vantander Bank, own cap 2,000,000 at 12%; Jensen Capital, 40,000 at 20%), so its Banks app holds two; the log says so once per city ("Banks: 2 bank(s) in the city, 2 in the Banks app (the city has fewer than 3 banks)").

| Rank | Rates | Card, overdraft, car finance, mortgage, business loan | Fee of a loan by phone |
|---|---|---|---|
| 0 (the largest) | as before | as before | as before (1.5%, at least 25) |
| 1 | one point more | 125% | half the usual share |
| 2 | one point less | 80% | three quarters of the usual share |
| 3 and on | as rank 0 | as rank 0 | as rank 0 |

The rule moves only the mod's own terms, and only inside the one desk that decides them: `CreditDesk.Offer(kind, bank)`, `Business(shop, bank, ...)`, `BusinessQuote`, `AgreedAt`, `LimitAt` and `PhoneLending.Quote` (the fee).
The Finance app asks the same desk, so its offers from a bank are that bank's on its page of the Banks app too.
For a new overdraft or card, the bank's share applies to the rating-based ceiling first, then `ProductTerms.IncomeLimit` caps it at 30 or 60 days of the game's average daily income (`PlayerHelper.CalculateDailyIncome`), rounded down to 50, with starter minimums of 200 or 300.
A bank that gives a quarter more still stops at the income cap. The Rating tab and next-rating preview use the same capped terms.
An open account retains its agreed limit, so changes in income or rating do not create an over-limit balance or fees; see [credit products](economy-credit-products.md).

The game's own loan at the bank counter keeps the game's terms by the rating alone (`BankTerms.Apply`): the banks already differ there by their own settings.
Under the lending threshold (580) every limit is 0 at every bank whatever its share, and the rating's gate (`CreditDesk.Gate`, `CreditLedger.CanBorrow`) is asked again at the moment anything is done: every page refuses in the bank's own words and shows no button that could lend.

One overdraft and one card are held in all, as before, with the bank that opened them (the address on the account, `CreditAccount.Street` and `Number`, which every account has always carried: an older save's accounts are at the house bank already, so nothing is converted).
A car or home approval records its bank (`Relief.ApprovedStreet`), and the account it becomes is held there.
A game loan belongs to the bank at its own `Loan.bankAddress`.

## The tabs

- **Accounts**: the current account (the cash, held with the house bank; another bank's page says where it is), an overdraft held here with pay, pay off and close, and the statement: every transaction of the game's history (`CashFlow`) that is this bank's, over a day, a week or a month.

  The house bank's statement is every movement of cash; another bank's is what names it (`businessName` on the game's loan transactions and the fees) or names an account it holds (`name` on the accounts' transactions).

- **Loans**: every loan owed to the bank, the game's own and the mod's (business, consolidation, car finance, mortgage, late fees).
  Pick one: its figures, how it stands, the next step if payments are missed, and pay an amount or pay it off.
  A game loan's row is known by the loan (`FinanceApp.LoanRowId`: its bank, first day and sum), so a pick stays on it when a loan before it is paid off.

  A game loan's kind is its mark's purpose: a bank loan, a business loan, a consolidation loan, or the student loan a new game may start with ([credit products](economy-credit-products.md#a-new-game-that-starts-in-debt)).
  A game loan is paid as EconoView pays it (`LoanEntryUi.PayOffLoan`: the game's `ba:transaction_loanpayoff` under the bank's name, the amount off `remainingAmount`, the loan out of the list with `ba:gameevent_loanpaid` when nothing is left); the credit file then reads it as it reads a payment made in EconoView (`EconomyDirector.BringUpToDate`).
  A collections call that waits shows its three answers here as well.
  Changing a game loan's daily payment stays in EconoView (a button opens it).

- **Card**: a card held here: limit, balance, the share used, the rate, the statement, the minimum and its due day; pay the minimum, an amount or all of it; draw cash; close it at nothing owed.
  The table is the card's own movements over 30 days.
- **Apply**: the bank's loan, overdraft, card, business loan, car finance, mortgage and consolidation, each with its figures today; a page for each with the same question boxes as the Finance app.
  An approval that stands shows Finance's own page (`FinanceApp.WriteApproval`): what holding it costs so far, and a cancel asked first in the game's question box with that charge.
- **Rating**: the bank's terms at five ratings from just under the threshold to the best terms, and the player's own: a loan up to (its cap before income and wealth), its rate, the card, the overdraft and car finance; what the next 50 points would bring at this bank; what would raise the rating.

## The game's parts

The panel is `GameUi.AppShell` (copied from MyEmployees) like the Finance app's.
Tables are `GameUi.DataTable` (a cell's tone is a type of its own, `GameUi.CellTone`, so that a figure passed to `TableRow.Set` is always what the column is ordered and summed by, never taken for a colour) with one set of `TableSpec`s for the app (`BankTables`), so an order or a filter is the app's.

The app's name on its button and in the menu's top bar is one text key, `reallife_banks_app` ("Banks").
The bank is picked under each page's title with the game's own previous and next selector (`InfoBox.Selector`, `BankApp.Which`), its words the game's own name for each bank; with one bank in the city its name alone stands there. The bank picked stays picked by its name when the city's banks are read again (`BankApp.Offer`), and another bank picked starts that bank's page of the tab from its start.
The icon is the game's app icon painted in a plain banking blue; no bank's real name, logo or colours are used.

## Cost

No Update.
Once a second (the credit side's timer) `BanksDirector` compares the banks wanted with the banks the app shows: a few comparisons while nothing changes. The app is put on once (it copies a few hundred of the game's objects when it is first opened).

The badge (debts behind or with a call waiting, at every bank the app shows, added up) is counted after the credit side's hour and day: one pass over the file's accounts and loans per bank.
A page costs one pass over the accounts and loans; the statement one pass over the game's transaction history (at most about a thousand).
Nothing runs while the app is closed.

## Checks

EconomyCheck (`BankChecks.cs`): the three profiles differ pairwise in rate, limit and fee.
Rank 0 gives every limit, rate and fee exactly as before at every rating.
Under the threshold every bank's every limit is 0 and at or over it every bank lends.
Limits never fall and rates never rise as the rating rises, at every bank; no rate under the floor; the fees.
The ranking of branches and banks; its cost.

In the game: Options > Tests > "Check the Banks app against Finance" writes `Bank check (figures)`: what every bank's Loans tab adds up with what no bank holds, against the Finance app's total, and that every bank refuses under the threshold.
"Open the Banks app on the first bank" opens it on the largest bank's Apply tab.
With diagnostics on, every page writes `Banks: <bank>, tab N written` with its height against the white box (`InfoBox.Fit`), and a game loan paid from the app writes `Bank check (loan payment)`.

## Not verified (the game could not be run)

That five tabs fit the bar and the eight columns of the Loans table fit (the Finance app has seven).
That the white box holds the Card page with two rows of buttons.
That the top bar shows the bank's name when an app opens from the full menu and from the HUD phone, and the game's own app's name again after it.

That `Icon-Coin` is among the phone's sprites (the next glyph is used if not).
The statement's matching by name relies on the data keys of the game's loan transactions (`businessName`, read from the decompiled `LoanHelper` and `LoanEntryUi`).

## Your own bank

`Scripts/Economy/OwnBank/`.
A bank of the player's own, founded in the middle of a game as a community bank and grown a level at a time to a national one (see [Bank levels and the owner's pay](#bank-levels-and-the-owners-pay)), run from the headquarters, in its own app (Your bank; the Company app's first page has a button to it).
Option: Business > Your own bank (`economy_bank`, on; it needs the credit rating).
Save line `reallife:economy_ownbank` (`OwnBankStore`, v1), and `reallife:economy_ownbank_test` while a test has run.

What the game already has, and what this is not: the game's banks lend to the player (`BankSettings`: a cap, an annual rate, years to pay; `LoanHelper.RunDaily` takes the payments) and take no savings; its investment funds (`InvestmentFund`, EconoView's Investments page) are the player's own money in a fund; its rivals (`RivalsHelper`) own businesses and buildings.
None of them is touched.

The bank's rivals are the game's banks by their own rate (`BankTerms.House`), priced as they price the player (`LendingTerms`); its fund trades the mod's Stocks app; its borrowers are the city's people by credit rating and the city's businesses, as pools, not one by one.

- **The rules** (`OwnBankCatalogue`, `OwnBankRules`, `OwnBankFund`, game-free).
  The licence and the least capital.

  The rivals' rates (savers: the bank rate less 1.5 points; borrowers: the house bank's own rate for a typical rating of each kind, a business loan a point under it, with the central bank's points and the game's interest multiplier; the poorly rated, whom no bank lends to, at the fair rate and 12 points), lowered or raised by up to a point as the bank takes their customers.

  The city's savings (people, businesses) and borrowing (five kinds) are shared between the rivals' hold of 1000 and the bank's weight: its hold, by its rate against theirs (e to a pull for each point), and for savers by the city's trust (its confidence: the player's integrity, the capital ratio against the lines, the scare of what the paper printed, the cycle; and its age, full after nine months).

  Balances move a share of a month's way to it each day; a run (confidence under 450 until it is back over 550) takes 3% of people's and 6% of businesses' savings a day.
  Defaults a year by kind, moved by the cycle (people) or `CityModel.Trouble` (business pressure, firms), inflation against the target, the council's business tax, grants, wage floor and rents, the adverse selection of a dear rate and each month's luck (seeded).

  Arrears settle over a month: a part put right, the rest written off, a part of that coming back evenly over two months (more, over four, through the courts, for 5% in costs).
  The capital ratio is the capital (with the fund at market prices) against the assets weighted by risk.

  Lending and buying stop at 13%, the regulator watches under 12.5%, under 10% is a breach with a month's deadline, a deadline missed is fined and the second closes the bank, as do no capital or a ratio under 5%, or savers it cannot pay after the fund is sold under the market.
  A closed bank is wound up: savers first, the regulator's tenth, the rest the player's.
  The next bank pays in half as much capital again for each closure, and a dividend never takes the capital under what it was founded with (`FoundedLeast`).

  The balance sheet balances after every day (`OwnBankRules.Balanced`).

- **The day** (`OwnBankDirector`, after the credit side's day, `EconomyDirector.AfterDay`).
  Each day ended since the last (a week at most) is run with the city now (`OwnBankGame.City`) and the market's quotes (`OwnBankBridges.Quotes`) and booked at the headquarters, dated on it (`OwnBankGame.BookDay`): takings (`TakingsKey`, in the statement's takings through `EconomyBridges.Takings`), savers' interest and losses as rows among its goods (`CostRow`), staff as salaries, all deductible; a fine is not.
  Every line the bank books names the bank by the name the player gave it (`OwnBankGame.Place`, each booking passed the bank's name; the name typed at founding is on the licence and the capital paid in). Until 1 Nov 2026 the lines named the headquarters, so Finance showed the headquarters' name for the bank. The game files them at the headquarters' address all the same, so EconoView's statement for the headquarters holds them.
  Days the bank stood still (its option or the credit side off) are not run: the line written on standing down says so (`OwnBank.Still`), and the first start after it runs from that day on (`OwnBankRules.Resume`).

  Then the day's profit is kept in the bank's capital (`KeptKey`) or its loss met from it (`CoveredKey`), so the player's cash nets to nothing; neither is in any statement category.
  The licence (licensing fees) and capital in come from the player's cash; a dividend, a sale and what a winding up leaves go into it.
  Founding and a sale go ahead only at the sums the game's question showed (a day may pass while it is open); if the game refuses the capital, the licence is given back.
  The day the regulator's watch was last told is saved with the bank, so a ratio on the line is told once a month across loads.
  `GameBooks.Refresh(day - 1)` puts the day into the statement the game made for it.

- **The rest of the mod** (`Wiring/OwnBankLinks`): integrity (Standing), the council's levers (Civic), the courts (`DebtClaims.Available`), the market's quotes (Markets), integrity lost for a fine (3) or a closure (8) and a little for a breach in the papers; the paper's pieces (`News.Extra`, chained after the company's; `LedgerCatalogue` kinds `_ob_news_*`, Business section).
  The headquarters is Going Public's `Mine`.
- **Tests**: a bank founded for a test (paid by nobody, pays nothing out), a month now, a hard month (a recession, inflation at 8%, business pressure at its worst), a run, the regulator's deadline today, the fund's trades, and putting the bank back as it was before the session's first test.
  A test that runs days runs them now and sets the bank's clock back by as many (`OwnBankRules.Rewind`).

Cost: nothing per frame; once a second a few comparisons; once a game day a few dozen sums per day ended, a pass over the market's 150 or so quotes and a handful of transactions; the fund's weekly rebalance sorts the eligible quotes once.
With "Developer diagnostics" on each day writes `Own bank check (day N)`: what was booked where, the market, the time it took and whether the books balance.

### Checks

EconomyCheck (`OwnBankChecks.cs`, 91 checks): every table's length.
The rivals' rates, their answer and the floor.
The city's savers and borrowers (none with no bank, the hold's share at parity, rates, trust, firms faster than people, a fifth less borrowing in a recession), the market settling within 2% of its weights in two and a half years and never by a hundredth of the city's savings in a day.

Defaults by the cycle, business pressure, inflation, the council and a dear rate, bounded.
Three years of a quiet city balanced every day, lending kept at its line, profitable within bounds.
A day's money item by item; the arrears and the courts; capital in and out.
A breach, a fine, a closure, a breach put right, insolvency and collapse.
A run that starts and ends, and one that closes the bank.

The fund's rules, even parts, a failure, distress, gains, no market, no buying in a breach, a fire sale.
A sale and a winding up.
The save's line (round trip, damage, bounds, a later version, mending).
Determinism and ten years in under a second and a half.

### Not verified (the game could not be run)

That four tabs and seven columns fit the frame and the Bank tab's white box holds its nine lines and three buttons (`InfoBox.Fit` with diagnostics on).
That the City Ledger prints the pieces on the morning after (they are queued for the next paper after six in the morning).
That the headquarters' statement shows the bank's rows (the same path as the broadband company's network row).

And the game's own text box for a name and an amount (as the bank apps use it).

## Bank levels and the owner's pay

`Scripts/Economy/OwnBank/Tiers/`.
The bank's level (its tier in the code: `BankTiers`, `OwnBankRules.Tiers`) and the owner's pay are part of the bank: no option of their own, and their fields appended to the end of the bank's head record (`OwnBankStore`, still v1), so an older RLO reads its own fields and leaves them.

- **Three levels.** A Community Bank, a Regional Bank and a National Bank; the National Bank is the bank as it was before levels, every figure of `OwnBankCatalogue` and no limit but the ratio. Each has a licence and a least capital, before the end game's scale and at it (mixed by the share as the bank's other figures are), a count of branches, a most of loans (performing and in arrears) and of savings, the record of months a move up to it asks, and its running costs (the banking hall before the scale; its systems and its least compliance crew at it).

  | Level | Licence | Least capital | Branches | Loans | Savings | Record to reach it |
  |---|---|---|---|---|---|---|
  | Community Bank | $150,000 ($500,000 at scale) | $300,000 ($1,000,000) | 2 | $2.5m ($15m) | $4m ($25m) | founded |
  | Regional Bank | $800,000 ($5,000,000) | $700,000 ($4,000,000) | 6 | $12m ($70m) | $20m ($100m) | 6 months |
  | National Bank | $3,000,000 ($60,000,000) | $5,000,000 ($150,000,000) | 27 | no limit | no limit | 12 months |

- **Branches.** The Community and Regional counts (2 and 6) are plain figures; the National Bank's is the city's fit: `BranchCatalogue.PerDistrict` (3) for each of the `BranchCatalogue.Districts` (9: the game's seven, and Uptown and Cedar Heights), 27 in all, where it was a flat 16 until a player asked for more once Uptown and Cedar Heights stood. It is `BranchCatalogue.BranchesMost`, which is also the most branches the save's line keeps. EconomyCheck holds the district count to `CityDistricts.NewYork` and `DistrictSplit`, so a district added later fails it until the limit is looked at again. The count is the same in any city the bank runs in; nothing about it is read from the city.
- **Founding** is always a Community Bank (`OwnBankDirector.FoundLicence`, `FoundCapital`): $450,000 in all without the scale, $1,500,000 with it, where a bank cost $8,000,000 and $210,000,000 before levels. A closure before still asks half as much capital again for each.
- **Where the limits bind** (`OwnBankRules.Caps`, at today's prices and the scale the bank runs at): the day shares the savers' new money within the room its level's savings leave (`OwnBankRules.Day`, `DepositRoom`) and lends within the room its book leaves (`Lend`, `LoanRoom`); a counter turns away a deposit past the room (`VisitResult.Full`) and makes no loan past it (`CounterLends`); the director takes a branch in only while the level's count has room (`OwnBankRules.BranchRoom`, `ReadBranches`), and a branch past it takes no customers and has no counter until the bank moves up. Interest is still credited to savers, and savers and borrowers still leave and repay: a bank at or over a limit keeps all it holds and takes no more.
- **Moving up** (`OwnBankRules.MoveUpWhy`, `MoveUp`; `OwnBankDirector.MoveUp`): the bank must be clear of the regulator (not watched, not in breach) with no run, have held its level for the next level's record without a breach (a breach day begins the record again: `TierFrom`), and hold the next level's least capital at the price of what is bought now. It costs the next level's licence less the one it holds, so a bank climbed to the top has paid exactly a national licence. Moved up, its record begins again and it keeps the new level's least capital (`FoundedLeast`). The player is told once a level when every condition but the licence is met (`TierTold`), and at a month's turn when a level's savings or loans are full.
- **An older save's bank** (`OwnBankRules.FitTier`, as the director reads the line): a line with no level is fitted on the terms the bank was founded on (its `FoundedLeast` gives the end game's share and its closures). A bank whose capital still meets that least is a National Bank, as it paid for; one whose capital has fallen is put at the highest level its capital meets and keeps that level's least from then. Either way it keeps every saver, loan and branch, even past that level's limits (they only stop it taking more), and its record counts from its founding.
- **The owner's pay** (`OwnBankRules.OwnerPay`, at the end of the day after the regulator): a share of the day's profit in steps of 5% (the game's own slider, copied from its settings menu as the Mod settings app's rows are: `GameUi/InfoBox.Slider`), nothing on a day without profit; or a fixed sum a day, paid whatever the day made, as a salary is. Nothing in a breach or a run, or for a test's bank, and never past what the bank can spare (its cash over the reserve, its capital over its least and its lending line: `Keep`, as for a dividend). A fixed sum is a commitment, so the bank holds it back through the day (`PayAhead`, `FreeCash`, `FreeCapital`): its lending, the fund's buying and the counters' loans never spend it, and the day's lending also keeps back the running costs still to come out of the cash that evening (`Running`). Before 1 Nov 2026 the fixed sum was capped at the day's profit and the lending spent the cash first, so a bank that lent all it could paid about the day's takings and the pay moved like a share (a player's report). The pay leaves the capital and the reserves together, so the books balance; the rest of the profit stays in the capital. It is booked into the player's cash as a dividend is (`PayKey`, among the Finance app's investments); `PaidMonth`, `PaidLast` and `PaidAll` keep what was paid, and `PayWanted`, `PayGiven` and `PayHeld` the last day's pay asked for, paid and why less (`BankTiers.PayHeld*`), which the pay's page names on a line of its own. With no pay chosen (the default, and every older bank) the bank keeps all of its profit and lends as before.
- **Automatic dividends** (`BankDividends`, `OwnBankRules.Dividends`, at the end of the day after the owner's pay; a player's suggestion): off, every 7 days, every game month or every game year, a share of the profit the bank kept over that time after the owner's pay (`DivKept`), in steps of 5%. Paid out of the capital and the reserves together and booked with the day as a dividend taken by hand (`CapitalOutKey`), never past the cash over the reserve or the capital over its least, its lending line and the fixed pay held back (`FreeCash`, `Keep`, `PayAhead`); nothing in a breach or a run, for a test's bank, or for a time that kept no profit. Each time falls due on its last day, paid, paid short or skipped, with a notice naming why (`DivWhy`) and the next time begun; a change of how often begins it again that day. The fields (`DivEvery` to `DivAll`) follow the pay's at the end of the head record; an older line reads as off. The Bank tab's third row opens its page; a test entry brings its day to today.
- **The app**: the Bank tab's list has two rows more, its level (what it may hold) and the owner's pay (this month's); each opens a page in the box: the level's meters against its limits, the next level's conditions ticked as met, the licence and a button to move up (with the game's question); the pay's way of paying, its slider or its sum, and what was paid.
- **Tests**: "make it ready to move up a level" (the record made long enough; a test's bank also gets the next level's least capital, paid by nobody, and moves up for nothing).

What the levels come to, in the offline check (a quiet city of the game's year, a bank founded a Community Bank, a year at each level, its capital put up to the next level's least where its profit has not brought it there, nothing paid out): before the scale a Community Bank makes about $3,500 a day and pays its licence back in ten months, and its kept profit brings it a Regional Bank's capital in two years; a Regional Bank makes about $13,000 a day six months on and pays its move up back in fourteen months; a National Bank about $45,000, its move up back in sixteen. At the scale: about $16,000 to $22,000 a day, the licence back in nine months, a Regional Bank's capital in thirty-one; a Regional Bank about $63,000 to $90,000, its move up back in eighteen months; a National Bank about $276,000 six months on, its $55,000,000 move up back in fifty months, about what a bank's licence took before levels.

Going Public: the bank is the player's own (the headquarters and the branches are Going Public's `Mine`); a level and a pay are the bank's own figures in its own line; nothing new is sent.
Cost: nothing per frame; a few sums more in the bank's day and in a branch's hour.

### Checks

EconomyCheck (`BankTierChecks.cs`): the table (every figure, rising, the top the bank of before, a climb paying exactly a national licence, founding at most a tenth of before); each limit binding in the day before the scale and at it, with the city wanting far more, and a bank over its limits keeping what it holds; the counter's deposits and loans; the branch count, with a National Bank's room past the sixteen it once stopped at, and the director's reading; moving up and each condition alone, the record begun again by a breach, the save's own months; an older save's bank fitted at each level, on the scale's terms and after closures, its loans, savers and ten branches kept and nothing more taken; the save's new fields after every field an older version reads; the pay at 0%, 5%, 35% and 100% split to the cent every day of two years, a fixed sum, nothing in a breach, a run or for a test's bank, never past the bank's room, the same on every run, a month's pay rolled; the balance figures above against their aims; the texts and the slider; a day of a bank at its limits timed and allocation-free.
EconomyCheck (`BankDividendChecks.cs`): each time falling due on its last day, the share of the kept profit to the cent with a fixed pay, the same on every run, a time without profit, a breach, a run and a test's bank paying nothing, the reserve and the capital holding one back and named, never into the watch or the fixed pay, off as before, the save and an older line, the rewind, the booking, the notices, the page and the texts, a day timed.
EconomyCheck (`BankPayFixChecks.cs`): a fixed pay under the profit paid in full on every clear day at each level and scale, the report's national bank lending all it can included; a fixed sum on a day of no profit or a loss; what is held back for it; one over the profit held back at the capital kept, never into the watch; the reserve named when it binds; the last day's pay saved and an older line read; every booking passing the bank's name; the pay's page and the texts.

### Not verified (the game could not be run)

That the game's slider, copied into the Bank tab's white box, shows its track, fill and knob in the box's colours and drags in steps.
That the level's and the pay's pages fit the box with their rows, ticks and buttons (`InfoBox.Fit` with diagnostics on).
That a branch opened past the level's count waits as told, and is taken in when the bank moves up.

## Bank branches

`Scripts/Economy/OwnBank/Branch/`.
Branches of the player's own bank: a business type of the mod's own (`BranchType`, `reallife_bankbranch`, "Bank Branch") opened in a shop unit with BizMan while the bank runs, its customers booked in the bank's own books.
Option: Business > Bank branches (`economy_bank_branch`, on; it needs Your own bank).
Its figures are part of the bank's save line (`OwnBankStore` parts `k` and `b`); there is no line of its own.

- **The business** is the game's own: rent, the fit-out (ready-made designs, see [Ready-made designs](designs.md)), opening hours, the rota and staff hired the game's way. Tellers are staff with customer service as their main skill, each on a shift at a till. The type sends no game shoppers (`spawnCustomers` off, no simulator); it is dressed as the city's banks (their icon and uniforms) once the game's types are there, and "prepare this save for removing the mod" makes each branch a gift shop (`BranchType.Strip`).
- **An hour** (`OwnBankDirector.Branches`, `EconomyDirector.AfterHour`, at most three hours caught up): each branch's customers are the game's own count for a shop of its building (`CustomerEntriesCalculator.GetInitialCustomers`, by promotion as the game counts it) times the branch's share (180 thousandths), the hour's weight and its satisfaction; the counter sees to six an hour a teller on shift (70% to 130% by skill) and six for the player while they work it; the rest leave.
- **A visit** (`BranchRules.Hour`, `OwnBankRules.Visit`) is drawn from the bank's seed, the hour, the branch and the visit: an account opened (about $400 paid in), a deposit ($350), a withdrawal ($300, never more than the people hold or the bank's cash), a loan of about $6,000 to a poorly, fairly or well rated person (only to those the bank lends to, outside a run or a breach, and within its cash over the reserve and its lending line), or a question. Each moves the bank's people's deposits, its loans and its reserves as its day does, so `OwnBankRules.Balanced` holds after every visit. In a run those who came to pay in take out. Fees: $25 an account, $1.50 cash at the counter, 1.5% of a loan (at least $50), at today's prices; they wait outside the balance sheet (`OwnBank.CounterFees`) and are the next day's takings (`BankDay.Counter`), booked at the headquarters with the rest.
- **The reach** (`BranchRules.Reach`): the visits all branches saw to in the last seven days raise the bank's hold of the city's people (`BankCity.Reach`, read by `SaversWanted` and `LoansWanted`) towards 300 thousandths more, half of it at 2,000 visits a week; businesses get 400 thousandths of it. Each branch more adds less.
- **The counter**: inside one of the player's branches a free till is clickable (`Careers/DeskGame`'s second target), the player walks to it and the game's own work panel (`Jobs/ShiftSkip`) offers a stint of up to four hours; while it runs the player stands at the till as the game's own WorkActivity puts them there and counts as a teller.
- **The floor** (`BranchFloor`): while the player is in a branch, up to eight of the hour's customers come in at the door, queue across a staffed till from its teller, are seen to and leave, or wait and leave unseen; at most three at once, from the mod's people (`People/ActorPool`). It shows what the hour booked and changes nothing.
- **The app**: a Branches tab in Your bank, a row a branch (this month's customers, seen to, left, accounts, loans, fees, rent and wages a day from the game's `RentPerDay` and the staff's shifts), and the reach with what it brings (`BranchRules.Worth`: the deposits and loans wanted with and without it, and their margin a day before staff).
- **Off**: no hour is worked and none is owed later, the reach is nothing, the counter cannot be clicked and the floor is empty; the bank holds what it would without branches. The type stays registered, so a save that holds a branch still loads.
- **The bank's level** sets how many branches it runs (two, six, twenty-seven: see [Bank levels and the owner's pay](#bank-levels-and-the-owners-pay)); a branch past that count is not taken in, sees nobody and has no counter until the bank moves up, and the player is told once a day.

What a branch comes to, in the offline check (a quiet city, a bank a year old, a mid-sized shop unit with two tellers, open 9 to 18): 553 customers seen to a week, a reach of 64 thousandths, $376 a day in fees and $2,921 a day of margin on $1.48 million more deposits and $1.84 million more loans, against $802 a day of rent and wages: $2,496 a day net, a $90,000 fit-out paid back in eight months of five days. A tenth such branch brings less than its rent and wages.

Going Public: branches are the player's own registrations (`GoingPublic.Mine`); visits are drawn from the bank's own seed, so the same everywhere; the floor and the counter are local; nothing new is sent.
Cost: nothing per frame; once a game hour one pass over the save's buildings and staff and a few dozen sums a branch (twenty-seven branches' hour in well under a millisecond offline, nothing allocated); once a second, inside a branch, a look at its tills; the floor four times a second over at most three people, only inside a branch.
With "Developer diagnostics" on each hour writes `Bank branch check (hour N of day D)`: the branches, customers, seen to, loans, fees, the reach, the time and whether the books balance; walking in writes `Bank branch check (floor, ...)`.

Tests: "An hour at my branch now" (a lunchtime hour of the branch the player is in, or the first, with a teller at least, booked now; "Put my bank back" undoes it) and "Customers at my branch now" (the floor of a busy hour, nothing booked).

### Checks

EconomyCheck (`BranchChecks.cs`): the catalogue, with a National Bank's limit held to the city's districts; an hour's customers by hour, promotion and satisfaction, carried parts, a closed hour; the counter by tellers, skill and the player; each kind of visit in the books, balanced, with its fee, a withdrawal capped, no loan in a run, a breach, to those not lent to or past the line; the fees as the next day's takings and nothing else different; an hour's visits against the bank's moves; determinism; the month and the week; the reach bounded and diminishing, more of people than of businesses; a year with none, one, three and eight branches balanced every day; a branch that pays (the figures above) and a tenth that does not; the save with branches, without, short, capped and full to the limit; the option off; the floor's plan; the texts; an hour of a National Bank's full count of branches timed and allocation-free.

### Not verified (the game could not be run)

That the game lists Bank Branch in BizMan's start screen for a shop unit and dresses it with the bank's icon and uniforms.
That the till is clickable with the business's own call to action asked after the mod's, and that the player stands at it as at a job's till.
That a teller hired for the branch stands at the till on their shift, and that customers queue across it on the floor.
That five tabs and eight columns fit the bank app.

## Bank risk, credit cards and the trading desk

`Scripts/Economy/OwnBank/Risk/`, `Cards/` and `Desk/` (players' suggestions of 1 Nov 2026: automatic dividends, a bank that can fail, a credit card business with a Black card for the owner, a trading desk).
The bank risk is one option, Business > Bank risk (`economy_bank_risk`, a choice of Off, Standard and Hard; Off by default, and Off is the bank exactly as it was: every table's Off row is the neutral figure, and `BankRisk` is the one place for them).
The card business and the trading desk are switched on by the owner in the bank itself (the Cards and Desk tabs), per bank, and are off in every older save.
Save: new parts of the bank's line `reallife:economy_ownbank`, still v1 (`OwnBankStore`): `r` (risk), `c`, `q` and `w` (cards), `t` and `g` (desk). A line from before them reads as Off, closed and balanced. The owner's card is one more field (the last) of an account's record in the credit file.
Cost: nothing per frame. A bank's day gains a few dozen sums (the card kinds, the desk, the money borrowed) and the own book's weekly trade sorts the quotes once. EconomyCheck times a day of everything running over 150 quotes under 1.5 ms (it takes well under one).

### Automatic dividends (what changed)

- **Quarterly** joins weekly, monthly and yearly (`BankDividends.Quarterly`, saved as 4, so the numbers of the others are not renumbered; the page shows them in time order, `BankDividends.Order`). A quarter is three of the save's calendar months, never longer than its year.
- **The next sum** is shown with the next date (`OwnBankRules.DividendExpected`): the profit kept so far plus the days still to run at the month's rate, after the owner's pay, the share of it, as far as the bank may pay now. It is an estimate. The day's books decide.
- **Only from profit, never below the required capital** was already so (`OwnBankRules.Dividends`: the share of the profit kept over the time, capped at the cash over the reserve and the capital over its least, its lending line and the fixed pay held back). With the bank risk on, a bank the regulator is watching pays none, and says why.
- **A bank line each** (`reallife_transaction_ownbank_investment_dividend_auto`) and **the tax** as a line of its own (`..._dividend_tax`): `ListingVotes.DividendTax`, the stock market's own rate for dividends moved by the council's business tax, through `OwnBankBridges.DividendTax` (`Wiring/OwnBankLinks`). A dividend the owner takes by hand is taxed the same way (`OwnBankGame.WithholdOnTaken`): one rule for dividends.

### The risk

`BankRisk` (tables by level), `OwnBankRules.Risk` (the rules), `OwnBankDirector.Risk` (the tools and the notices). Everything is a function of the bank, the city and the bank's seed.

- **Credit risk by the economy.** `DefaultPerMille` multiplies a kind's defaults by `StressFactor`: the cycle's state (a firm's loans 170% of the usual in a recession on Standard, 230% on Hard, a person's 125% and 160%), the kind drawn to be in trouble this month and a lopsided book. What is written off comes back less in a slump (`RecoveryFactor`). A month's trouble is drawn from the seed (`ShockSegment`: 8% of months on Standard, 15% on Hard) and told in the last two fifths of the month before it.
- **Concentration.** Once the book (loans, arrears and cards) is larger than the capital, a kind over `ConcLimit` of it is charged: the part over weighs more for the regulator and makes that kind's defaults rise.
- **Liquidity.** The reserve kept against deposits rises (8% off, 10%, 13%). Savers leave faster than they come (`FlightSpeed`), and of their own accord under a confidence of 65% (`Flight`, businesses first). The city's confidence also falls when the cash would last under two days of a run, and after a month's loss of a twentieth of the capital (`RiskCushion`).
- **A run** begins under a higher confidence on Standard and Hard (`RunStart`), with warning signs (`Signs`: confidence, cash cover, the ratio near its line, a coming shock, flight, concentration) told once a month. The tools: pay savers well (`RunHold`); **sell loans** to other banks at a discount that grows in a slump and in a run (`SellLoans`, a quarter of a kind at once, the discount out of the capital at once); **borrow from other banks** (`Borrow`: a share of the capital for a month at the bank rate and a margin, more in a run and a slump, with a fee; a liability beside the deposits, repaid on its day out of the cash over the reserve or rolled over at a penalty rate); and **put in the owner's capital** (the page says how much clears the warning line, `CapitalToClear`). Each keeps `Balanced` true: reserves, loans, arrears, the fund, the own book and the cards together equal the deposits, the money borrowed and the capital.
- **The regulator's ladder.** A warning line over the watch line (`LendRatio` 130 off, 140, 160: the bank lends and buys no further under it), a watch (new lending cut to half or a quarter, no dividend and no pay), a breach with its deadline and fines, and the resolution as before: closed at the second missed deadline, with no capital left or under half the law's ratio.
- **The waterfall** (`OwnBankRules.Resolve`): a bank taken is sold for what its assets fetch (less in a slump), the savers are paid first, the banks it borrowed from next, the regulator's charge comes off what is left and the rest is the owner's. `WindUp` pays that to the cent, and with no money borrowed, no cards and no own book it is the old formula exactly.

### Credit cards

`CardTerms` (the table), `OwnBankRules.Cards`, `OwnBankDirector.Cards`. A card goes to the bank's own clients (its savers, by what a saver holds), a kind of client at a time (the five kinds of borrower; the tiers Starter, Classic, Gold, Business and Corporate). The owner sets, per kind, issuing and the limit (four steps), and for all the rate over the rivals' card rate, a yearly fee and two credit-score lines: approved at once from the first, declined under the second, and between them queued for the owner, who approves or declines the queue together. Looser lines bring more clients and worse ones (`CardShares`: a normal spread of scores, where a lower score makes the cohort's defaults higher, kept as `CardRisk`).
A card earns interest on the part of its balance that is carried, the shop's interchange on what is spent and the fee. It costs rewards, processing, fraud and defaults by the loans' own model (`DefaultPerMille`, so the economy, the shock and the concentration act on cards too) times a card's lack of security. The regulator weighs a card balance at 150%, and cards count in the level's loan limit. Cash for balances comes from the bank's reserves and its lending line, so cards compete with loans for them.
**The owner's own card** is an ordinary card of the credit file with the flag `CreditAccount.Own` (its statement, minimum, interest and ladder are the mod's own), which the file's one-card rule skips (`CreditFile.Account`, `OwnCard`) and the bank's books follow. `AccountRules` tells `OwnCardSink` what is drawn, paid, charged and written off (the claim and the bankruptcy), the bank applies it with its next day, and a draw the bank has not the cash for over its reserve is refused (`CreditDesk.Draw`). Its limit is at most a tenth of the capital and its rate three points under the best card's. The owner is weighed like any client. If the bank is sold or closed the card stays the owner's debt.

### The trading desk

`BankDesk`, `OwnBankRules.Desk`, `OwnBankDirector.Desk`. **Brokerage:** the clients hold a share of their deposits for trading (people 25%, firms 6%), trade it about three times a year and pay the bank's commission on the volume. The volume follows the market's activity (`MarketDirector.ActivityMilli`: the market's own variance today against the ordinary), and is nothing while the market is not running. The market has options and futures (`Markets/Derivatives`: futures on raw materials, options on materials and on shares), so while the exchange is on (`MarketDirector.DerivativesOpen`) the clients also deal in them at a higher commission, and a very wild market costs the desk some of their failed margin calls. With the exchange off the desk offers neither. **The own book:** up to a quarter of the capital held by a mandate (index: the largest companies, income: the best dividends, growth: mid and small companies), traded to an even part each weekly, marked to the market's real prices each day (the same quotes as the fund), counted by the regulator at the fund's weight and in the capital at market. Its **risk limit** (a loss from its peak of 5% to 25%) cuts it: the book is sold down to half and nothing is bought for a month. A fire sale meeting savers sells the book before the fund.

### Both interfaces and the books

The old app and the kit show the same facts (`OwnBankReader`, read by both): a risk row on the Bank tab opening its page, a sale of loans on a kind of borrower's page, the Cards and Desk tabs, and the dividends' page with the order, the expected sum and the tax. Every act is a call of the director that both make.
The day's books go to the headquarters' statement as one line each (`BankLines`): the loans' and fund's takings, the cards' and the desk's; the savers', the losses' (with what a sale of loans cost), the cards' (losses, fraud, rewards, processing), the funding's (interest on money borrowed) and the desk's costs. Every key is in `BizLines`, so the Company app, the game's tax and a listed bank's quarters (`BankDay.Income` and `Profit`, told once to `OwnBankBridges.DayBooked`) count each line once.

### Checks

EconomyCheck `--bank-risk` (`BankRiskChecks.cs`): the tables' lengths, Off the neutral row, Hard harder; defaults rising from a boom to a recession on both levels, a firm's loans over half as many again, recoveries; the seed's shocks (their rate, determinism, the warning window, a swollen kind); concentration (charged, spared, the floor, cards counted); the reserve, savers leaving faster than they come, flight, cover and the city's loss of heart, the run's hold; a run meeting the tools (sale, borrowing, repaying, rolling over, the owner's capital clearing the warning line) with the books balanced each time, and a run that ends or closes the bank; the waterfall adding up in 300 banks, the old formula to the cent, a closing's waterfall; three years Off booking nothing of the new parts; the watch's limits; the save (every field, an older line, garbage, mending); the card book's sums, the shops' fee against fraud, losses by the economy, the owner's choices, the lines and the queue; the owner's card through the sink, the account's own arithmetic and the credit file; the desk's commission by activity and by derivatives, margin calls, each mandate, the cut, no buying for a month, a fire sale; quarterly dividends, the expected sum, never below the required capital and from profit alone on every level in a boom and a recession; the statement's lines adding up to the day; the sources of both interfaces; the texts; the timing.
`--bank-risk-report` prints the profit a month of a small, a middling and a large bank before and after, in good times and in a recession.

### Not verified (the game could not be run)

That the Cards and Desk tabs and the risk page fit the app's frame in the old interface (`InfoBox.Fit`); the kit's are checked by `--ui-only own_bank`.
That the owner's card appears in the Finance app's accounts and that its draws, statement and payments move the bank's books in a real game (the offline checks drive the same arithmetic).
That the market's variance is a good measure of its activity in play, and that the exchange's options and futures are on when its option is.
