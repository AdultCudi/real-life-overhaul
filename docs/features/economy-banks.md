# Economy: bank apps and your own bank

## In a sentence

One phone app for each of the city's three largest banks, each showing that bank's slice of the player's money, and late in a game a bank of the player's own, run from the headquarters.

## Where the code is

- Folders: [`Scripts/Economy/Banking/BankApps`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Banking/BankApps/) and [`Scripts/Economy/OwnBank`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/OwnBank/).
- Read first: `Banking/BankApps/BanksDirector.cs`, `Banking/Terms/BankRules.cs`, `OwnBank/OwnBankDirector.cs` and `OwnBank/OwnBankRules.cs`.

## How it talks to other features

- The bank apps read the game's own banks (`Banking/Loans/BankTerms.Lenders`) and keep no save data.
- `Wiring/OwnBankLinks.cs` gives your own bank Standing's integrity, the council's levers, the courts for a lender's claims and the Stocks app's quotes.

`Scripts/Economy/Banking/BankApps/`.
One app on the game's phone for each of the city's three largest banks, under the game's own name for the bank, each that bank's slice of the player's money.
The Finance app stays the one page for every lender at once.
Option: Money > Bank apps (`economy_banks`, on by default).
No save data.

## Which banks, and how they differ

The banks are the game's own (`BankTerms.Lenders`: every special-service building whose settings are a `BankSettings` and that has a registration).
Branches with the same name are one bank.
`BankRules.Rank` orders them by the most each lends on its own terms (`BankSettings.maxTotalLoanAmount` as first read, before the rating writes it), the largest first, by name among equals; `BankTerms.Banks` holds one branch of each in that order.

The first is `BankTerms.House`, the bank the mod's accounts have always been held with.
The game's New York has two banks (Vantander Bank, own cap 2,000,000 at 12%; Jensen Capital, 40,000 at 20%), so it has two bank apps; the log says so once per city ("Banks: 2 bank(s) in the city, 2 bank app(s) on the phone (the city has fewer than 3 banks)").

| Rank | Rates | Card, overdraft, car finance, mortgage, business loan | Fee of a loan by phone |
|---|---|---|---|
| 0 (the largest) | as before | as before | as before (1.5%, at least 25) |
| 1 | one point more | 125% | half the usual share |
| 2 | one point less | 80% | three quarters of the usual share |
| 3 and on | as rank 0 | as rank 0 | as rank 0 |

The rule moves only the mod's own terms, and only inside the one desk that decides them: `CreditDesk.Offer(kind, bank)`, `Business(shop, bank, ...)`, `BusinessQuote`, `AgreedAt`, `LimitAt` and `PhoneLending.Quote` (the fee).
The Finance app asks the same desk, so its offers from a bank are that bank's in its app too.
For a new overdraft or card, the bank's share applies to the rating-based ceiling first, then `ProductTerms.IncomeLimit` caps it at 30 or 60 days of the game's average daily income (`PlayerHelper.CalculateDailyIncome`), rounded down to 50, with starter minimums of 200 or 300.
A bank that gives a quarter more still stops at the income cap. The Rating tab and next-rating preview use the same capped terms.
An open account retains its agreed limit, so changes in income or rating do not create an over-limit balance or fees; see [credit products](economy-credit-products.md).

The game's own loan at the bank counter keeps the game's terms by the rating alone (`BankTerms.Apply`): the banks already differ there by their own settings.
Under the lending threshold (580) every limit is 0 at every bank whatever its share, and the rating's gate (`CreditDesk.Gate`, `CreditLedger.CanBorrow`) is asked again at the moment anything is done: every app refuses in the bank's own words and shows no button that could lend.

One overdraft and one card are held in all, as before, with the bank that opened them (the address on the account, `CreditAccount.Street` and `Number`, which every account has always carried: an older save's accounts are at the house bank already, so nothing is converted).
A car or home approval records its bank (`Relief.ApprovedStreet`), and the account it becomes is held there.
A game loan belongs to the bank at its own `Loan.bankAddress`.

## The tabs

- **Accounts**: the current account (the cash, held with the house bank; another bank's app says where it is), an overdraft held here with pay, pay off and close, and the statement: every transaction of the game's history (`CashFlow`) that is this bank's, over a day, a week or a month.

  The house bank's statement is every movement of cash; another bank's is what names it (`businessName` on the game's loan transactions and the fees) or names an account it holds (`name` on the accounts' transactions).

- **Loans**: every loan owed to the bank, the game's own and the mod's (business, consolidation, car finance, mortgage, late fees).
  Pick one: its figures, how it stands, the next step if payments are missed, and pay an amount or pay it off.
  A game loan's row is known by the loan (`FinanceApp.LoanRowId`: its bank, first day and sum), so a pick stays on it when a loan before it is paid off.

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
Tables are `GameUi.DataTable` (a cell's tone is a type of its own, `GameUi.CellTone`, so that a figure passed to `TableRow.Set` is always what the column is ordered and summed by, never taken for a colour) with one set of `TableSpec`s per app (`BankTables`), so an order or a filter is that app's.

The app's name on its button and in the menu's top bar is one text key, `reallife_bank_app` ("{bank}"), filled through the game's own `TextLocalizationComponent.Arguments` (`PhoneAppDef.TitleArguments`, [game-ui.md](../../RealLifeOverhaul.Build/docs/game-ui.md) section 33): it still follows a change of language.
The icon is the game's app icon painted in one of three plain colours by rank; no bank's real name, logo or colours are used.

## Cost

No Update.
Once a second (the credit side's timer) `BanksDirector` compares the banks wanted with the apps on the phone: a few comparisons while nothing changes, and at most one app put on a second (each copies a few hundred of the game's objects, so they never land in one frame together).

The badges (debts behind or with a call waiting) are counted after the credit side's hour and day: one pass over the file's accounts and loans per bank.
A page costs one pass over the accounts and loans; the statement one pass over the game's transaction history (at most about a thousand).
Nothing runs while an app is closed.

## Checks

EconomyCheck (`BankChecks.cs`): the three profiles differ pairwise in rate, limit and fee.
Rank 0 gives every limit, rate and fee exactly as before at every rating.
Under the threshold every bank's every limit is 0 and at or over it every bank lends.
Limits never fall and rates never rise as the rating rises, at every bank; no rate under the floor; the fees.
The ranking of branches and banks; its cost.

In the game: Options > Tests > "Check the bank apps against Finance" writes `Bank check (figures)`: what every bank's Loans tab adds up with what no bank holds, against the Finance app's total, and that every bank refuses under the threshold.
"Open the first bank's app" opens it on Apply.
With diagnostics on, every page writes `Banks: <bank>, tab N written` with its height against the white box (`InfoBox.Fit`), and a game loan paid from an app writes `Bank check (loan payment)`.

## Not verified (the game could not be run)

That five tabs fit the bar and the eight columns of the Loans table fit (the Finance app has seven).
That the white box holds the Card page with two rows of buttons.
That the top bar shows the bank's name when an app opens from the full menu and from the HUD phone, and the game's own app's name again after it.

That `Icon-Coin` is among the phone's sprites (the next glyph is used if not).
The statement's matching by name relies on the data keys of the game's loan transactions (`businessName`, read from the decompiled `LoanHelper` and `LoanEntryUi`).

## Your own bank

`Scripts/Economy/OwnBank/`.
A bank of the player's own, late in a game, run from the headquarters, in its own app (Your bank; the Company app's first page has a button to it).
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
