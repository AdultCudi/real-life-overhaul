# Economy: credit products, missed payments, the private lender and the ways out

## In a sentence

More kinds of credit (an overdraft, a credit card, a purchase on finance and more), what follows a missed payment, a private lender who lends when the banks will not, and the ways out: consolidation, debt advice, a debt arrangement and bankruptcy.

## Where the code is

- Folders: [`Scripts/Economy/Banking`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Banking/) (`Terms`, `Accounts`, `Debt`, `Loans` and `Desk`; its [README](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Banking/README.md) says what lives where). The files are listed in the code block below.
- Read first: `Banking/Terms/ProductCatalogue.cs`, `Banking/Desk/CreditDesk.cs` and `Banking/Debt/SharkRules.cs` (the private lender).

## How it talks to other features

- Every product asks the [credit rating](economy-credit.md) first (`CreditLedger.CanBorrow`).
- `Wiring/DebtLinks.cs` turns a lender's claim into one of Justice's civil suits, and gives the private lender what it needs from the street (whether the player is held, a free stretch of kerb, a point out of the camera's view).

The second part of the economy (`Scripts/Economy/Banking`; options "More kinds of credit", "The private lender", "Consequences of not paying" and "How hard not paying hits").
It stands on the credit rating above and changes none of its rules: every product asks `CreditLedger.CanBorrow`, so under the threshold (580) nothing is opened and nothing is drawn, on every preset; what is already owed stays owed.
No method of the game's is patched.

The game's own loans stay the game's own records.

```
Economy/
  Banking/          all but the Desk and DebtPhone are game-free and compiled by EconomyCheck
    Terms/
      ProductCatalogue  EVERY NUMBER of this part: limits, rates, fees, the steps' counts by level, the private lender, the ways out
      ProductTerms      the terms by rating (limits, rates, deposits, quotes), and consolidation's before and after
      GameLoanMath      the game's loan interest formula, for a business loan and the penalty rate
    Accounts/
      CreditAccount     ONE model for every account of the mod's (ProductKind)
      AccountRules      the accounts' arithmetic and daily run; IWallet and the transaction keys (Money)
      Wallet            IWallet over GameManager.ChangeMoneySafe (the game's transaction history has every movement)
    Loans/
      LoanHolds         what the mod writes on one of the game's loans (penalty rate, arrangement) and how it is put back exactly
    Desk/
      CreditDesk        THE GAME SIDE: starting, stopping, what is held on the game's loans, the hour and the save; one part a concern:
                        .Day (each day's payments, the steps, claims), .Offers (opening, drawing, paying), .Loans (the business
                        loan, consolidation), .Finance (car finance and mortgages, repossession), .Collections, .Shark, .Relief
                        (advice, arrangement, bankruptcy), .Banks (by bank, for the bank apps), .Tests (leaving a save, the test tools)
    Debt/
      LadderRules       LadderState and what a lender does as payments go on being missed, for accounts and the game's loans alike
      SharkRules        the private lender's one debt and its terms (SharkDebt, SharkRules)
      Relief            arrangement, bankruptcy, the rating's ceilings, an approval for finance (Relief, ReliefRules)
      DebtPhone         three contacts and their conversations: Collections, Private lender, Debt advice
  Finance/
    Books             the game's figures the economy reads: what leaves tomorrow, businesses and their statements, sales
    FinanceApp.Products  the product pages of Borrow
    FinanceApp.Accounts  the Accounts tab
Justice/DebtClaims  NEW FILE in Justice: file, ask about and withdraw a lender's civil claim (one of Justice's own suits)
Wiring/DebtLinks    the bridges: courts, holds, the injury of a mugging, what the camera sees, a kerb for the test car, contact pictures
```

## The products

All figures are constants of `ProductCatalogue`; "the bank's rate" is the house bank's own rate moved by the rating as `LendingTerms` moves it, times the game's interest multiplier.
The house bank (`BankTerms.House`) is the bank with the largest cap of its own.
Interest is whole money, worked out in hundredths of a per cent over the game's days in a year with the remainder carried (`CreditAccount.Carry`): a long run neither gains nor loses a unit.

| Product | What it is | How it is collected |
| --- | --- | --- |
| Overdraft (`ProductKind.Overdraft`) | A limit of 2% (at 580) to 8% (from 800) of what the city's banks lend at the rating, rounded to 50, at least 200. Free to hold. Rate: the bank's plus 8 points. | On the first hour event of a new day, before the game's run, if cash is short of what leaves tonight (`Books.Tomorrow`, the figure the Overview shows) it pays the shortfall into cash up to its free limit. Interest is added to the balance each day. Cash above 500 pays it back at each day's end. A balance over the limit is a missed payment. |
| Credit card (`CreditCard`) | A limit of 3% to 12% of the same, rounded to 50, at least 300. Cash is drawn in the app for a fee of 3%, at least 5. Rate: the bank's plus 12 points. | A statement every 7 days. 3 days later the minimum (5% of the statement, at least 25, or all of a smaller balance) is taken, and what is then still unpaid of the statement bears 7 days' interest. A statement paid in full bears none. A minimum paid by hand before its day counts as a payment on time on that day (`DayOutcome.PaidEarly`, `OnTime`). A minimum that could not be taken is tried again each day, and is one miss a statement. |
| Business loan (a game `Loan`, `LoanMark.Purpose = Business`) | Offered when the player runs a business with 7 daily statements on record (`GameInstance.financialSummaries`, what EconoView's business page reads): up to 30 times its average daily profit, at the bank's rate less 1 point, plus the phone's arrangement fee. The mark keeps the business as `Security`. The bank's cap in memory is raised by what is owed on it (`BankTerms.Secured`), so it does not use up the personal cap. | By the game, like any loan. |
| Car finance (`CarFinance`) | See  [A purchase on finance](#a-purchase-on-finance-where-the-game-forced-a-change). Deposit 10% to 50%, 60 to 240 days, the bank's rate plus 2 points, up to what the bank lends unsecured at the rating. The vehicle (`VehicleInstance.id`) is the security. | A daily instalment (the amount over the term, rounded up) and the day's interest, taken together on the game's day event, after the game's own loans. If the cash is not there nothing is taken, the interest is added to the balance, and it is a miss. |
| Mortgage (`Mortgage`) | The same for a building bought in BizMan: deposit 20% to 50%, 360 to 1,440 days, the bank's rate less 1 point, up to 5 times what the bank lends unsecured. The building (its address) is the security. | As car finance. |
| Late fees (`Arrears`) | Late fees on a game loan that were not in cash on their day. No interest. | Taken in full as soon as the cash is there. |

Early repayment of any of the mod's accounts is free, at any time, in Finance under Accounts.
A game loan is repaid in EconoView, as before.

**The mortgage: the game does sell property.** BizMan's presentation buys a building outright (`BizManPresentation`, transaction `ba:transaction_buildingbought`, a `RealEstate` entry in `GameInstance.realEstate` with its `purchasePrice`); `RealEstateHelper` collects its rents daily and sells it to a competitor when it is put up for sale.
So the mortgage exists.

A building is never taken for one: the game has no safe way to take a building back (`RealEstateHelper.SellBuildingForCompat` removes the entry without restoring the owner or the rent), and the player may live or trade in it.
At the fifth step the lender takes the building's rents instead.

## A purchase on finance (where the game forced a change)

The design was: the player arms finance, buys in the game's own way, and the lender pays the financed part back into cash after the game has charged the full price.
The game refuses a purchase unless the WHOLE price is in cash (`ShowcaseVehicleController.Purchase` and `VehicleDeliveryHelper` call `ChangeMoneySafe` without force; BizMan tests `Money < price`), so that would only have financed people who did not need it.

What is built instead keeps the game's purchase untouched and turns the order round:

1. **Approval** (`CreditDesk.Approve`): amount, deposit share and term are chosen in Finance.
   The buyer must hold the deposit the amount implies (`ProductTerms.DepositFor`).
   The approved amount is paid into cash at once (`reallife_transaction_finance_advance`), the application goes on the credit file, and the ids of the vehicles (or the addresses of the buildings) owned now are remembered.
2. **Purchase**: made in the game's own way.
   While an approval stands the desk compares one count a second (`VehicleInstances.Count` or `realEstate.Count`); when it changes it looks for what is new.
   A vehicle's price is the newest `ba:transaction_vehiclebought` for that type since the approval (else the type's list price); a building's is its `RealEstate.purchasePrice`.
   Lent vehicles (`zeroSellingPrice`) and hand vehicles are passed over.
3. **The account** opens for `ProductTerms.Financed`: the price less the buyer's deposit, never more than was approved.
   What was approved over that is taken back at once (`..._finance_returned`).
   The approval's interest (`ReliefRules.PurchaseCharge`, `..._finance_charge`) is on the part financed only, for the days before the purchase: the account charges its own interest from the day it opens, so no day is charged twice, and what goes back unused costs nothing.
   An approval already standing in an older save is charged so when it ends: nothing saved says how it is charged, and the rule can only lower the charge.
4. **No purchase** by the end of the day after the approval: all of it is taken back, by force if it has been spent (the game lets cash go under nothing, as its own loan interest does), with interest on all of it for every day held (`ReliefRules.CancelCharge`).
   The same on "Cancel the approval", after the game's question with that interest, in Finance and in the bank's app alike (`FinanceApp.WriteApproval`).
   The question before an approval, its page and the Guide name the interest: about so much a day on the whole sum while nothing is bought, and on the part financed once something is.

**Selling or losing the security** (`CreditDesk.Securities`, each game hour): when the vehicle or building is no longer the player's, the newest matching sale in the game's transactions since yesterday (`ba:transaction_itemsold` with the vehicle's type, `ba:transaction_buildingsold` with the address) is taken against the balance as far as it is still in cash.
The rest stays owed on the same payments, unsecured, and the app says so.

## When payments are missed

One state machine (`LadderRules`, `LadderState`) for every debt: each account of the mod's and each of the game's loans (its state is on the loan's `LoanMark`, fed by `LoanBook.Settle`'s findings).
It counts due payments missed in a row; a payment made in full puts the count back to nothing (the misses stay on the credit file).

| Step | Standard, Realistic | Hardcore | What happens |
| --- | --- | --- | --- |
| 1 Reminder | 1 | 1 | The notice that the payment was missed (it names the amount and the fall in the rating), and the Planner's row. |
| 2 Late fee | 2 | 2 | 2% of the payment, at least 15 (halved on Relaxed), added to the account. For a game loan: taken from cash, else put on the `Arrears` account. |
| 3 Penalty rate | 3 | 3 | 5 points a year more until 3 payments in a row are made. |
| 4 Collections | 5 | 4 | The call: "Pay what is overdue now", "Agree a payment plan" (the overdue money over 10 days on top of the normal payment; one per debt; broken, it goes straight to the next step), "I can't pay". |
| 5 Repossession | 8 | 6 | Secured debts only. Car finance: the vehicle is taken and sold for 60% of its price; the rest stays owed. Business loan: 25% of that business's daily takings go to the lender until the arrears are paid. Mortgage: the building's rents do. |
| 6 Claim | 12 | 9 | A civil claim for the balance and costs (10%, at least 50). Also when a balance left by a repossession still stands 10 days later. |

Relaxed stops after step 4, Standard after step 5, Realistic and Hardcore go to step 6 (`ProductCatalogue.LastStep`, option `economy_level`, set by the presets: Relaxed 0, Standard 1, Realistic 2, Hardcore 3).
With "Consequences of not paying" off no step is taken at all, any penalty rate is lifted, and only the rating is affected, as in the first part.

**The penalty rate on a game loan** (`LoanHolds`, `GameLoanMath`).
`Loan.dailyInterest` is a public number the game takes each night.
The loan's own interest and payment are kept on its mark (`OwnInterest`, `OwnPayment`) from the moment anything is held.

The penalty is the game's formula (`LoanHelper.CalculateDailyInterestPayment`) for 5 points on the amount borrowed, added to the loan's own interest (the game keeps no rate, so this is the formula at the loan's rate plus 5 to within one unit of rounding).
The own figures are written back exactly: when three payments in a row are made.
When "Consequences of not paying" is switched off.

When the economy stops (the option, a fault, the city unloading); by the save tools.
And **every time the game writes a save** (`CreditDesk.BeforeSave` on the game's save event, which runs before the game serialises): a save file therefore never holds anything but the game's own figures and loads in the plain game as it would without the mod, with or without "prepare this save".

What is held stays noted in the credit file and is written again within a second and on the next hour event, which is the first thing the game's new day does, before its run.
If the player changes the payment in EconoView, the new figure is taken as the loan's own.
When the game prices its loans again for a new interest multiplier, each loan's own figures are first put back at its own rate under the new multiplier (`LoanHolds.Repriced`, see [the credit rating](economy-credit.md)), so that recalculation is never taken for a loan's own.

A debt arrangement uses the same mechanism (interest and payment are nothing while it runs).

**Repossession of a vehicle** (`CreditDesk.TakeVehicles`): never while the player is in it (`GameInstance.ActiveVehicleId`, `controlledByPlayer`), within 30 m of it, or looking at it (`StreetSpots.IsInView` through `EconomyBridges.InView`); then with the game's own `VehicleInstance.Delete`.
Its cargo goes with it, and the notice says so beforehand.

**The claim** (`CreditDesk.Claim`): `EconomyBridges.FileClaim` (Wiring/DebtLinks) calls `DebtClaims.File` in `Scripts/Justice/DebtClaims.cs`, a new file that adds a `JusticeSuit` to `JusticeState.Suits` with Justice's own numbering and a text from the Legal contact.

From then on it is Justice's: listed and answered in the Legal file (settled for 60%, or contested with Justice's own chance), judged on the day after its date, and what is owed becomes one of Justice's civil debts, collected as Justice collects all its debts.
The account is closed here (a game loan is removed from `GameInstance.Loans`; nothing else in the game refers to a loan).

A judgment shows on the rating through `EconomyLinks`, which already counts Justice's civil debts and posts `CreditKind.Judgment`.
With Justice off there is no court: the debt stays as it is.
Justice's suit can be contested and won; then the lender gets nothing.

## Consolidation and early repayment

`CreditDesk.Consolidation` / `Consolidate`: every game loan, the card's balance and the overdraft's are paid off by ONE new game loan at the chosen bank at today's rate for the rating, for what is owed plus the fee (1%, at least 25: the fee is in the loan, so no cash is needed).

Offered at or over the threshold, if the new loan fits what that bank lends at the rating, and not while one of the game's quest loans is open at that bank, nor at any bank while the game's own bankruptcy quest runs (`CreditDesk.GameBankruptcy`: its emergency loan would be paid off; the page says to follow the quest).
Finance shows before and after: owed, daily cost, interest still to pay, days (`ProductTerms.Before` / `After`; a card or overdraft has no end, so its interest is counted over the new loan's days).
It posts one `Application` and one `Consolidated` line, not early pay-offs.

The card and the overdraft stay open with nothing owed.

## The private lender

`SharkDebt` / `SharkRules`, contact "Private lender" (`reallife-lender`).
It makes itself known by a text the first time a bank refuses the player (`EconomyDirector.TellRefused`) or the rating is under the threshold at a day's end.

It lends 500 to 5,000, or up to 10 days of average daily income if that is more; 3% of the sum is added each day; all of it is due in 10 days; one extension by 10 days costs 15% of what is owed, named with the new day in a question before it is agreed (`reallife_lender_extend_terms`, `SharkRules.ExtendedDay`); what is owed never passes 3 times the sum.
It is not an `ICreditAccount` and posts nothing: it is not in the rating and paying it builds nothing.

Past the due day: day 1 a text.
Day 3 the player is set upon in the street if on foot and out of doors, and the cash on them is taken against the debt.
Day 6 one of their businesses is damaged (a repair bill of 10% of the debt, 150 to 1,500).
Then one or the other every 3 days.
On Relaxed every act is a text.

An act that cannot happen yet waits and is tried each game hour; nothing happens while `EconomyBridges.Held` says the player is held (the game's dialogs, time skips, hospital and pause through `LifeEvents.IsSafeForEvent`, a panel or test of the mod's, a large street scene, the police, prison, a bus ride).

**Bridges to Crime and Business.** Neither has a public way in: `CrimeDirector` stages its scenes from its own deck and has only the test entry `StageNow`; the business-pressure feature deals its issues from its own deck and has only `Test`.

So nobody walks up to the player: the assault is the injury a mugging gives (`LifeEvents.Injure`, 12 hours hurt, 8 energy, never enough to make a tired player faint) with a notice, and the damage is a repair bill with a notice.
A staged scene would need an entry added to `Scripts/Crime`.

## Debt advice, a debt arrangement, bankruptcy

Debt advice (contact "Debt advice", free) is offered by a notice when something has been overdue 10 days running or a claim has been filed.
The call explains the three ways with the caller's own figures and changes nothing.
The two that change anything are in Finance, Accounts, "Ways out of debt", where the game's question box confirms them (the red one for bankruptcy).

- **Sell and settle**: the game's own figures (`PlayerHelper.GetPersonalWealth`).
  Nothing is sold by the mod.
- **A debt arrangement** (`CreditDesk.Arrange`): every game loan and every unsecured account of the mod's stands still (no interest, no fees, no steps; a game loan through `LoanHold.Frozen`) and is paid by one daily payment of 20% of the average daily income, at least 25, shared out in a fixed order.
  The rating has a ceiling of 560 while it runs and for 20 days after (`ReliefRules.Ceiling`, applied in `CreditScoring.Compute`).

  Three missed payments end it and everything runs again.
  The private lender and secured finance are not part of it.

- **Bankruptcy** (`CreditDesk.Bankrupt`): every game loan (removed from `GameInstance.Loans`), every account but a mortgage that still has its building, and the private lender's debt are written off; open claims are withdrawn (`DebtClaims.Withdraw`); a vehicle on finance goes back (taken out of sight as above, its proceeds the creditors'); cash above 500 goes to the creditors; fines and court debts stay.

  The rating's ceiling is 300 for 60 days, during which `CreditLedger.CanBorrow` answers `CreditRefusal.Bankrupt` whatever the score (a flag of its own: `ReliefRules.NoCredit`), then 450 rising by 2 a day.
  Once in 180 days.
  Not offered while the game's own bankruptcy quest (`SQBankruptcy`) is active (the page points to the game's) or while the tutorial's first-business loan is open.

## The Finance app

Five tabs (`AppShell.SetTabCount(5)`).
The rule that keeps the white box short: a list is rows of the table, which scrolls; the box shows only the row that is picked.

- **Overview**: "owed" and "leaves tomorrow" include every account; the table lists the mod's accounts and the private lender's debt under the loans.
- **Credit**: a line names the debt that hurts most now (the one furthest behind, else a limit over half used), and says so when the rating is being held under a ceiling.
- **Borrow**: under the banks, one row per kind of credit with "On offer", "Open" or "Not offered now"; picking one shows its page (what it is, the figures, the selectors, one confirm) or the sentence that says why not.
- **Accounts** (new): every account and loan with lender, owed, and how it stands (in order, payments behind, penalty rate, on a plan, in collections, to be repossessed, in an arrangement); the private lender's debt; an approval; "Ways out of debt".
  Picking a row shows its page and its buttons: pay an amount (the game's text box), pay it all off, draw cash, close it, pay what is overdue, agree a plan.

## The save: version 2

```
v=2~h:...~d:...~e:...~f:...~l:...~a:...~k:...~r:...
  h  as version 1, and at the end: the next account's number
  l  as version 1, and at the end: standing (12: misses|overdue|paid run|step|penalty|plan left|plan a day|plan used|
     call due|repossession day|repossession pending|claim number)|held (bits: 1 penalty, 2 frozen)|own interest|
     own payment|purpose (0 plain, 1 business, 2 consolidation)|security ("street#number")
  a  the mod's open accounts: id|kind|owed|agreed|rate (hundredths of a per cent)|carry|opened|term|instalment|next
     statement day|due day|statement left|minimum left|charged|secured|security value|bank's number|frozen|closed|test|
     standing (12)|security|security's name|bank's street
  k  the private lender: known|borrowed|owed|taken day|due day|grown day|extended|acted day|waiting|test
  r  the ways out and an approval: arranged|day|a day|total|paid|misses|ended day|bankrupt day|overdue since|advice
     told|advice known|collector known|approved kind|amount|deposit|term|rate|day|bank's number|bank's street|claims (n,n)
```

**Migration from version 1**: nothing is made up.
A version 1 line has the first five parts and seven fields to a loan; it is read as it is, the new parts are empty, and each loan starts in order with its lender.
It is written back as version 2.
Closed accounts are never written; at most 24 accounts are.

`EconomyCheck` round-trips a full version 2 file, loads a literal version 1 line, and damages the new parts.

**With the mod removed.** A save always holds the game's own loan figures (see the penalty rate).
The mod's own accounts are in `reallife:economy` and do not exist in the plain game.

"Prepare this save for removing the mod" (and "reset the mod's data") call `CreditDesk.Leave` through the feature's cleanup: an approval's money goes back, every hold on a game loan is ended for good, each account is paid from cash as far as the cash goes, and what is left is forgiven.
One `Economy: leaving this save. ...` line in Player.log lists each account with what was paid and what was forgiven.
A financed vehicle or building stays the player's.

Three contacts are registered with the feature so the save tools remove them.

## For features built on it

```csharp
ProductCatalogue.*                             // every figure; ProductKind, LadderStep, ProductAnswer
ProductTerms.RateBp(score, ownRate, points, multiplier) / OverdraftLimit / CardLimit / FinanceMost / Quote
CreditFile.Accounts / Account(kind) / AccountById / Shark / Relief          // the save's state (CreditLedger.File)
CreditAccount : ICreditAccount                 // Owed, Agreed, RateBp, RateNow, Ladder, Secured, Frozen, Test
AccountRules.RunDay / Pay / Draw / Cover / Finance / Repossess / AgreePlan / DueTomorrow / Interest
LadderRules.Missed / Paid / Takes / AgreePlan, LoanHolds.Begin / Apply / Restore / End
ReliefRules.Ceiling / NoCredit                 // a ceiling on the rating, and the hard flag CanBorrow asks
EconomyDirector.Instance.Desk                  // CreditDesk: Gate, Offer, OpenAccount, Pay, Draw, Approve, Business,
                                               // Consolidation, Arrange, Bankrupt, Owed, Level, ApplyHolds / RestoreHolds
BankTerms.House / At(street, number) / Secured // the bank the accounts are with; room a secured loan adds to a cap
Books.Tomorrow(file) / Businesses / AverageProfit / SalesYesterday
EconomyBridges.FileClaim / ClaimOpen / WithdrawClaim / Held / Assault / InView / KerbSpot / ContactIcon
DebtClaims.File / Open / Withdraw              // (Justice) a civil claim from outside the justice system
```

A new product is a value at the END of `ProductKind` (before `Count`), a name and a source in `ProductCatalogue`, its terms in `ProductTerms`, and a case in `AccountRules.RunDay`; the model, the save, the steps, the app's list and the ways out take it as it is.

The city's economy moves the rates through this hook: its central bank's points (`LendingTerms.BasePoints`) go in through `LendingTerms.For` and `ProductTerms.WithBase` (see [the city](economy-city.md)), and an account keeps the rate it was agreed at (`RateBp`).

## Cost

Still no `Update`.
Per second: the desk tests three flags (holds to write again after a save, an approval standing, a vehicle waiting to be taken) and, only while one is set, compares one count or looks at one vehicle.
Per game hour: what is held on the loans compared (a handful), each secured account's security looked up in the game's list, the private lender's waiting act.

Per game day: each account's arithmetic (integers), the steps, one or two lines on the credit file.
Transactions are searched only when a purchase or a sale has just been noticed.
The app builds a page when a tab or a row is picked.
No per-frame work and no per-frame allocation.

## Faults

Everything the director asks of the desk goes through `EconomyDirector.DeskDo`.
A fault there logs one line (`Economy: the credit desk ... is switched off for this session after an error in ...`), writes every loan's own interest and payment back, and leaves the rating, the banks' terms, the app and the game's daily run going.
The accounts stay in the save.
A fault in the first part still switches the whole economy off, and that stops the desk first.

## Test buttons

- "open a test overdraft, a test card and a test car on finance": accounts marked as tests, never part of the rating, never taking or paying money; the car is a lent `DeliveryVehicleInstance` at a free stretch of kerb.
- "the test accounts miss 1 / 3 payments": each step is taken and logged.
- "the collections call about a test account".
- "repossess the test car": taken once the player is 30 m away and not looking.
- "send the private lender's first text".
- "work out a debt arrangement and a bankruptcy in the log": on the test accounts and on the real figures; nothing is changed.
- "put everything back": test accounts and the test car removed; what is held on the game's loans checked.

Lines the tests put on the credit file are test entries: "clear test entries" takes them out.

## Log lines (with "Developer diagnostics" on)

- `Economy check (accounts at load, day 40): 3 account(s), 1 loan(s) with something held (0 without its own figures: cleared), 0 business loan(s) whose business has gone ..., 0 account(s) whose security has gone ...: PASS`
- `Economy check (account opened): CreditCard #2 with <bank>, 0 owed of 1200 at 26% a year: PASS`
- `Economy check (overdraft, day 41): 950 leaves tonight (loans 300, accounts 118, fines 0, rent 400, wages 132) against 610 in cash: the overdraft paid 340, 340 of 800 drawn: PASS`
- `Economy check (account, day 41): CarFinance #3: 9000 owed, interest 18, paid 118, now 8900 owed: PASS` (or `a payment of 100 missed (2 in a row)`)
- `Economy check (step, day 43): CarFinance #3: 3 payment(s) missed in a row on level 1 (secured), 300 overdue: PenaltyRate: PASS`
- `Economy check (rate, the penalty rate): the loan of 10000 at <street> 12: daily interest 13 to 21, daily payment 27 to 27; its own 13 and 27 are remembered, held at the penalty rate: PASS` (and `(rate, the game is saving)`, `(rate, after the save)`, `(rate, three payments in a row)`)
- `Economy check (approval)`, `(purchase on finance)`, `(approval ended)`, `(security gone, day n)`, `(repossession, day n)`, `(takings, day n)`, `(claim, day n)`
- `Economy check (collections): ...`, `(business loan)`, `(consolidation): 3 debt(s) of 7500 in all (...) replaced by one loan of 7575 ...: PASS`
- `Economy check (private lender, day n): 60 added, 2600 owed of at most 6000, due day 20, 3 day(s) past: Assault: PASS`
- `Economy check (debt advice, day n)`, `(debt arrangement, day n): 80 paid, 4920 left of 5000: PASS`, `(bankruptcy, day n): before ... after ...: PASS`
- `Economy: leaving this save. ...` (always written)

## Checks

`EconomyCheck` now compiles fourteen game-free files and runs 293 checks (`Checks2.cs` has the new ones):

- the terms of every product at every score (nothing under the threshold, no limit falling, no rate rising);
- interest over 600 days to the unit, a vehicle and a mortgage paid off on their last day to the unit, missed days, early repayment;
- the overdraft's cover and repayment, the card's statement cycle;
- the steps for each level from a scripted history, the payment plan kept and broken, repossession sums and the claim after 10 days;
- consolidation's totals, the private lender's growth, cap and acts, the arrangement, bankruptcy's totals, the 60-day flag and the rebuild;
- the save's version 2 round trip, a version 1 file, damage in the new parts;
- the penalty rate written and put back on a model of the game's loan formula.

## Not verified (the game could not be run)

Everything on screen: that five tabs fit the bar (it sizes each tab to its label; five short labels are expected to fit); that the product pages with three selectors and a row of buttons fit the white box, which does not scroll; that a second row of buttons draws under the first.
That the game's hour event reaches the desk before the run on the day's first hour (the overdraft's log line shows it).

That `GameEvent`-free detection of a purchase by count catches every way of buying a vehicle (showroom, contract, delivery).
The sale transactions' data keys (`itemSoldInfo`, `address`) are read from the decompiled game and not seen in a save.
That a vehicle deleted while its scene is not loaded leaves nothing behind.
The three contacts' pictures and their place in the phone's list.

That a conversation opened from a notification (`dialogUI.ShowDialog`) behaves as one opened from a call.
