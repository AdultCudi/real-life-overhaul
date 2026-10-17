# `Scripts/Economy/Banking`

The player's money and borrowing: the credit rating, the banks' terms by it, loans from the phone, the mod's own accounts (overdraft, credit card, car finance, mortgage), what follows a missed payment, the private lender, the ways out of debt, and one app for each of the city's largest banks.

## What lives where

| Folder | What it holds |
| --- | --- |
| `.` | `EconomyDirector`: keeps it all going. Its timer, hour and day are every economic feature's too (`Follow`, `AfterHour`, `AfterDay`). Parts: `.Day` (the game's loans read and settled each hour and day), `.App` (the Finance app, a loan from the phone), `.Tests`. |
| `Terms/` | Every rate, fee, limit and term, and the formulas that set them. Game-free. |
| `Rating/` | The credit file, the score (`CreditScoring`), its public face (`CreditLedger`: the gate every lender asks, the one way to write on the file) and its line in the save (`CreditStore`). Game-free. |
| `Loans/` | The game's own loans: the city's banks and the terms written on them (`BankTerms`), the loans read (`GameLoans`) and told apart (`LoanBook`, `LoanMark`), a loan made from the phone (`PhoneLending`), the penalty rate and an arrangement written on a loan and put back (`LoanHolds`). |
| `Accounts/` | The mod's own accounts: `CreditAccount`, their arithmetic and daily run (`AccountRules`), the cash they move (`Wallet`). |
| `Debt/` | What a lender does as payments are missed (`LadderRules`), the private lender (`SharkRules`), the ways out of debt (`Relief`), and the three phone contacts and their calls (`DebtPhone`). |
| `Desk/` | `CreditDesk`: the game side of everything above, one part per concern: `.Day` (each day's payments and steps), `.Offers` (opening, drawing, paying), `.Loans` (business loan, consolidation), `.Finance` (car finance and mortgages), `.Collections`, `.Shark`, `.Relief`, `.Banks` (by bank, for the bank apps), `.Tests`. |
| `BankApps/` | `BanksDirector` and `BankApp`: one bank's slice of the same record. They keep nothing of their own. |

Everything game-free (`Terms`, `Rating`, the rules in `Loans`, `Accounts` and `Debt`) is compiled as it is by `RealLifeOverhaul.Build/EconomyCheck`.

## Interest and terms: the one place

Every number is a constant of `Terms/CreditCatalogue.cs` (the rating and a bank's loan terms) or `Terms/ProductCatalogue.cs` (the mod's products, missed payments, the private lender, the ways out). Each rate and fee is worked out once:

| What | Where |
| --- | --- |
| A bank's loan cap and rate at a rating | `Terms/LendingTerms.For` (with the central bank's `BasePoints`); written on the game's banks by `Loans/BankTerms.Apply` |
| A product's limit and rate at a rating | `Terms/ProductTerms` (`OverdraftLimit`, `CardLimit`, `FinanceMost`, `RateBp`, `WithBase`, `FixedRateBp`, `BusinessRate`) |
| Each bank's difference | `Terms/BankRules` (points on rates, share of limits and of the fee), applied by `Desk/CreditDesk.Banks` (`LimitAt`, `AgreedAt`) |
| A loan's daily interest | the game's own `LoanHelper` at the rate written on its bank; where the mod sets the rate (a business loan, the penalty rate), `Terms/GameLoanMath` |
| The game's interest multiplier, and its keys for loans | `Loans/GameLoans` (`Multiplier`, `ExpenseCategory`, `LoanPaidEvent`); the quest ids in `Loans/BankTerms`. When the multiplier changes, each loan keeps its own rate: `Terms/GameLoanMath.Repriced`, `Loans/LoanHolds.Repriced`, called by `EconomyDirector.KeepLoanRates` |
| An account's daily interest | `Accounts/AccountRules.Interest` (whole money, the rest carried), at `CreditAccount.RateNow` |
| Approval money held, the private lender | `Terms/ProductTerms.AdvanceCharge` (nothing bought: all of it, every day held, `Debt/ReliefRules.CancelCharge`) and `PurchaseCharge` (bought: the financed part only, to the day before, `ReliefRules.PurchaseCharge`); `Debt/SharkRules.ADay` |
| Fees | phone fee `LendingTerms.PhoneFee` / `BankRules.Fee`; card cash `AccountRules.CashFee`; late fee `AccountRules.LateFee`; claim costs `LadderRules.ClaimCosts`; consolidation `ProductTerms.ConsolidationFee`; extension `SharkRules.ExtendFee` |

## Documentation

How it works, its rules and its tests: [the credit rating](../../../../../../docs/features/economy-credit.md), [credit products and debt](../../../../../../docs/features/economy-credit-products.md) and [the bank apps](../../../../../../docs/features/economy-banks.md).
How the code is laid out, and how features meet: [Architecture](../../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
