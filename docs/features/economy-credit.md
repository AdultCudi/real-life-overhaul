# Economy: credit rating, lending and the Finance app

## In a sentence

One credit rating worked out from how the player pays, the banks' terms moved by it (under the threshold nobody lends), loans from the phone and a **Finance** app: the core the rest of the economy stands on.

## Where the code is

- Folders: [`Scripts/Economy/Banking`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Banking/), [`Scripts/Economy`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/) (`EconomyBridges.cs`) and [`Finance`](../../Assets/Mods/RealLifeOverhaul/Scripts/Economy/Finance/). The files are listed in the code block below.
- Read first: `Banking/EconomyDirector.cs`, `Banking/Rating/CreditScoring.cs` and `Banking/Rating/CreditLedger.cs`.
- The other economy pages: [credit products and debt](economy-credit-products.md), [bank apps and your own bank](economy-banks.md), [the city](economy-city.md), [investors](economy-investors.md), [the market](economy-markets.md), [running a business](economy-company.md) and [the rota planner](economy-rota.md).

## How it talks to other features

- Economy calls no other feature and none calls it: `Wiring/EconomyLinks.cs` fills in `EconomyBridges` with the record the rating reads (`Wiring.Standing`, Justice's debts), the debts the Finance app lists, the price level and the refunds the city counts.
- The Planner's Finance rows come from `Wiring/EconomyLinks.cs` too.

The economy core (`Scripts/Economy/Banking`, option "Credit rating and phone loans", id `economy`): one credit rating worked out from how the player pays, the banks' terms moved by it (under the threshold nobody lends), loans from the phone, and a Finance app.

It is the foundation the later economy work stands on: more kinds of credit and what a missed payment leads to (built: see [Economy: credit products, missed payments, the private lender and the ways out](economy-credit-products.md)), a city economy with interest rates, investors, tax.
No method of the game's is patched; everything is done by reading and writing the game's public state, on its events, with copies of its interface.

```
Economy/
  Banking/
    Terms/            game-free: compiled as they are by RealLifeOverhaul.Build/EconomyCheck
      CreditCatalogue   EVERY NUMBER: the scale, the bands, the threshold, the weights, the fades, the terms, the fee
      LendingTerms      the terms table, the phone's fee, Decide, MostNow
    Rating/           game-free: compiled as they are by RealLifeOverhaul.Build/EconomyCheck
      CreditFile        CreditFile (what the save holds), CreditEvent, PayDay, LoanMark, LoanView, LegalStanding, CreditInputs
      CreditScoring     the arithmetic: Compute, Tips (what would raise it most), Open (the starting estimate)
      CreditLedger      THE PUBLIC FACE: the score and grade, CanBorrow, Post, GradeChanged, ICreditAccount and its register
      CreditStore       the save's one line, "reallife:economy"
    Loans/
      LoanBook          game-free: the game's loans told apart (Sync) and the daily run read (Settle)
      BankTerms         the city's banks; their own terms kept, the rating's written, the own put back
      GameLoans         the game's loans as an ICreditAccount; the game's own means test read from its public members
      PhoneLending      a loan made from the phone exactly as the bank's dialogue makes one, plus the fee
  Finance/
    FinanceApp        the tabs (five since the second part) and the white box's pages
    FinanceTable      the table (the game's BaTable) and its row
    FinanceText       the words; money as the game's loan pages write it
    CashFlow          one pass over the game's transaction history for a period
  EconomyBridges    what the economy asks of the rest of the mod (filled in by Wiring/EconomyLinks); Finance.Open
  Banking/EconomyDirector   keeps it going: the save's line, the game's hour and day events, the banks' terms, the app, the tests
```

## The rating

One score, 300 to 850, with a grade.
It is computed, never random: the same file, figures and day give the same score.

| | |
|---|---|
| bands (`GradeFrom`) | 300 very poor, 500 poor, 580 fair, 670 good, 740 very good, 800 excellent |
| `LendingThreshold` | 580: below it nobody lends, on every preset (no preset touches the economy) |
| `CleanStart` | 640: a save with no debts and no record |
| `Weights` | payments 40, owed 25, length 10, legal 15, applications 10 (per cent; they add up to 100) |

`score = 300 + 550 x (sum of weight x factor) / 100`, each factor from 0 to 1 (`CreditScoring.Compute`):

- **Payments.** Every instalment of the last `PaymentFadeDays` (60) weighs `1 - age / 60`.
  Factor = `(prior x neutral + good + lasting) / (prior + good + lasting + MissWeight x bad)` with `PaymentPriorWeight` 4, `PaymentNeutral` 13/44 (what makes an empty file come out at 640), `MissWeight` 8, an instalment paid in part counting as `PartPaidShare` 0.5 of a miss, and `lasting` = payments on time older than the fade, each worth `LastingPaymentShare` 0.05, at most `LastingPaymentsMost` 200 of them.

  So a miss costs a thin file more than a long one, and is gone from the factor after 60 days (it stays in the file's totals).

- **Owed.** `OwedAgainstLimitShare` 0.8 of it is `1 - owed / limit` (the limit is what every bank would lend on its **own** terms, so the rating does not feed on itself); the rest is `1 - (daily payments / daily income) / 0.5` (`ServiceShareWorst`), with no income counting as the worst.
  Income is the game's `PlayerHelper.CalculateDailyIncome`.
- **Length.** Days since credit was first held, in full after `HistoryFullDays` 120.
- **Legal.** 1 less `ConvictionCost` 0.35 a conviction, `JudgmentCost` 0.25 a civil judgment being paid off, `LateDayCost` 0.08 for each day a fine or court debt could not be taken (at most 0.40), `OutstandingCost` 0.05 while any is owed, `OffenceCost` 0.05 a line on the police record in the last `OffenceDays` 30 (at most 0.20).
  Read through `EconomyBridges.Legal`, never from Justice or Law directly.
- **Applications.** Each takes `ApplicationDent` 0.15 off, fading over `ApplicationFadeDays` 10.
  One application is about 8 points for a day.
  A loan taken at a bank is an application (seen when the loan appears), and so is one by phone.

**The starting estimate** (`CreditScoring.Open`, logged as `Economy: a credit file is opened ...`): each loan in the save is taken as paid on time for `paid-off part / daily payment` days (what the game's daily run takes each day), which gives the length of the history and the lasting payments.
Debts and the record need no estimate: the scoring reads them live.

**The file** (`CreditFile`) keeps the instalments of the last 60 days counted per day, the last `LedgerMost` 120 events with what each did to the score, folded totals per kind for older ones, and the loans as last seen.
An event is posted with `CreditLedger.Post(kind, amount, day, sourceKey, count)`; the result is the effect in points.

## How the daily payment is read

`GameManager.NewDay` raises `GlobalEvents.onNewHour`, then runs the day (`BusinessHelper.RunDaily` calls `LoanHelper.RunDaily`: an instalment is taken, or only the interest when the money is short and the balance is left as it was), then raises `GlobalEvents.onNewDay`.
So:

- on the hour event `LoanBook.Sync` brings the file's marks up to date: after it, each mark is exactly its loan as it stood **before** the run.
  What the player did since the last hour is seen here (a new loan, one paid off early, part paid off, a changed daily payment) and posted;
- on the day event `LoanBook.Settle` compares after the run: a balance that fell was paid, a loan that is gone was paid off, a balance that did not move while a payment was due was missed.

Nothing the player does can fall between the two (they are in one call of the game's), so the reading is exact.

If the day event ever came without the hour event before it, the comparison is against the last hour's reading and the day's line says so and ends FAIL. A loan is known by the object itself while the save is loaded and, after a load, by its fingerprint (the bank's address and the amount borrowed).
Two loans of one print are told apart by their balance (`LoanBook`, top comment).

`LoanEntryUi` clamps a daily payment between the bank's minimum and the balance, so a payment under the minimum cannot be set; a bank without settings has no minimum, and a payment of 0 is then neither paid nor missed.

## The banks' terms

A bank's terms are one `BankSettings` object in memory.
`BankDialog.LoanRequest` reads `maxTotalLoanAmount` when an amount is confirmed and `BankLoanSettings` reads `annualInterestRate` and `yearsToPayLoan` for the figures shown while it is typed.
`BankTerms.Apply(score)` writes `LendingTerms.For(score, own cap, own rate)` on each:

| rating | cap | annual rate |
|---|---|---|
| under 580 | 0: the game itself refuses every amount | (not offered) |
| 580 | `CapShareAtThreshold` 40% of the bank's own | its own plus `RatePointsAtThreshold` 6 |
| 800 and over (`BestTermsFrom`) | `CapShareAtBest` 130% | its own less 2 (`RatePointsAtBest` -2), never under `RateFloor` 1 |
| between | a straight line | a straight line, rounded to a whole point |

- **When.** At the start, each game hour, each day, after any event that moves the score, when the app opens and before a quote.
  Only a value that differs is written.
- **The game's quest loans are left alone** (`BankTerms.QuestHold`): while the tutorial's first-business loan is open (`tutorialEnabled` and `tutorial_quest_establish_first_business_objective_1` not completed: the game's own test in `BankDialog.GetEconomyMaxLoanAmount`) every bank keeps its own terms; while the side quest `SQBankruptcy` is active a bank with `allowSideQuestEmergencyLoan` keeps its own (that loan ignores the cap anyway).
- **Put back.** The first values seen on an object are its own and are kept for as long as the game runs.
  `BankTerms.Restore` writes them back exactly: when the option goes off, on a fault, when the city unloads or the mod is switched off (`Wiring.EconomyLinks.Disconnect`, and again in the director's `OnDestroy`), and from the test button.
  Nothing of this is in a save: the objects are the game's assets.
- **The refusal.** With a cap of 0 the game says "the most we can lend you is $0".
  So while nobody lends, the one-second timer looks whether a bank's dialogue is open (`DialogController.current.dialog is BankDialog`, at the counter or on a call) and says why once, through the game's notification, with what would raise the rating most.
- **Known.** The game recalculates every loan's daily interest and payment from the bank's settings when `bankInterestMultiplier` is changed in the game's options (`LoanHelper.RecalculateLoanPayments`): with the economy on that uses the rate for the rating at that moment, not the rate each loan was taken at.

## Loans from the phone

`PhoneLending.Take` makes one of the game's own loans as `BankDialog.LoanRequest` does: `new Loan(amount, interest, payment, the bank's address)` with the figures from `LoanHelper.CalculateDailyInterestPayment` and `CalculateDailyPayment`, added to `SaveGameManager.Current.Loans`, paid out with the game's `ba:transaction_loanpayout`, and `ba:gameevent_newloan` raised.
EconoView lists it, the game's daily run collects it, and it is there with the mod removed.

The most a lender lends now is `LendingTerms.MostNow`: its cap for the rating less what is owed to it, and no more than the game's own means test (`BankDialog.GetEconomyMaxLoanAmount` is private; `GameLoans.MeansLimit` reads the same public members).
The fee is `PhoneFeeShare` 1.5% of the amount, rounded up, at least `PhoneFeeLeast` 25 (`reallife_transaction_loan_fee`, in the game's loan-expenses category).
The smallest loan is the game's 500.
The bankruptcy quest's emergency loan is offered in person only.

## The Finance app

Built as the Planner is (`AppShell`, with `SetTabCount(4)`), id `RealLifeFinance`.
Nothing runs while it is closed; rows and figures are made when it opens, when a tab is picked and when the player changes something in it.

| tab | what it shows | from |
|---|---|---|
| Overview | cash; owed in all; loan payments, fines and rent tomorrow; wages paid yesterday; net worth; the rating; every debt as a row | `GameInstance.Money`, `Loans`; `EconomyBridges.OtherDebts`; rent by the rule of `BusinessHelper.RunDaily` over `BuildingRegistrations`; yesterday's transactions in the category `ba:transactioncategory_salaryexpenses`; `PlayerHelper.GetPersonalWealth().CurrentWealth` |
| Credit | score, grade, the threshold, the one or two things that would raise it most, each factor in a plain word; the last 30 events | `CreditLedger.Score`, `CreditScoring.Tips`, `CreditFile.Events` |
| Borrow | each bank: rate offered, most it lends now, what is owed to it; an amount (steps, or typed into the game's text box); payment, interest, days, fee, cost; the game's question box | `BankTerms.Lenders`, `PhoneLending.Quote` (the game's `LoanHelper`), `HudConfirm` |
| Cash flow | money in and out by the game's kinds of transaction for yesterday, 7 days or 30, and the five largest | `GameInstance.Transactions` (`CashFlow`: no second history is kept) |

- The game has no forecast of wages (`EmployeeInstance.PayWage` works them out as it pays), so the app shows what was paid yesterday and says so.
- The game gives a category to eleven kinds of transaction only, and a type to every one, so the cash flow is grouped by type, named by the game's own `<type>_label` (the grouping EconoView's filter offers).
- The game keeps at least seven days of transactions and drops older ones past 1,000; when the period reaches further back than the history, the app says from which day the history runs.
- Repaying and changing a daily payment stay in EconoView; "Repay in EconoView" opens it (`FullMenu.ShowApp`).
- The badge on the HUD button counts payments missed and a change of grade since the app was last opened.
- **EconoView's loans page has no button of the mod's.** Its layout is a prefab the UI study has not read, and a button placed blind in a page of the game's is not clean.
  The Planner's row, the notifications and the app are the ways in.

## The save

One line of the save's modData, `reallife:economy` (`CreditStore`; version 1 as below, version 2 since the second part, which adds to it: see [The save: version 2](economy-credit-products.md#the-save-version-2)), registered with the feature, so the self-check and "prepare this save for removing the mod" know it:

```
v=1~h:...~d:...~e:...~f:...~l:...
  h  header: start day|first credit day|old on time|old missed|old part|test offset|last score|last grade|settled day|
     unseen missed|unseen grade|last missed day|count|amount|last grade day|grade it was|record as last seen (5)
  d  per day of the last sixty: day|on time|missed|part|test on time|test missed
  e  the last 120 events: kind|day|amount|count|effect|test|source
  f  folded totals, one per kind: count|amount
  l  the loans as last seen: street|number|total|first day|remaining|payment|interest
```

A later version adds fields at the end of a record or a part with a new letter; this reader takes what it knows.
A full file is under 8,000 characters (`EconomyCheck` measures one).
A damaged line opens a fresh file from what the save shows.
The loans are the game's own records: a save loads in the plain game with its loans as they are.

The line is written when the game saves (`SaveHook`), and when the option is switched off or the save tools stop the feature in the middle of play.
It is **not** written when the city unloads: by then the game's current save may be another one.

The director's first look is a second after the city's objects are made, and one that is destroyed after a newer one has started leaves the file, the ledger and the banks to that one.

## For features built on it

```csharp
CreditLedger.Value / Grade / Score          // the rating; Score has every factor and the counts behind it
CreditLedger.CanBorrow(out CreditRefusal)    // THE GATE: every lender asks it first
CreditLedger.Post(kind, amount, day, sourceKey, count)   // one line on the file; returns the effect in points
CreditLedger.GradeChanged                    // (was, is)
CreditLedger.Posted                          // (event, score before, score after)
ICreditAccount + CreditLedger.Register       // Key, NameKey, Balance, Limit, PaymentDue: counted in "owed" and in the app
LendingTerms.For / MostNow / PhoneFee / Decide, BankTerms.Lenders / Wanted / QuestHold
EconomyBridges.Legal / LegalReady / OtherDebts, Finance.Open(tab)
```

A new kind of credit is a class implementing `ICreditAccount`, registered while the economy runs, that posts `PaidOnTime`, `Missed` or `PartPaid` for its instalments (they are counted on their day) and `NewCredit` when it lends.
A new kind of event is a value at the end of `CreditKind` and a text in `FinanceText.EventKeys`.
Every figure is a constant of `CreditCatalogue`; the Guide's page prints them through `{facts}`.

## Cost

No `Update`.
A coroutine waits one second on the real clock (the game is paused while the phone or a call is open) and then: tests four flags, holds three handlers (`SaveHook.Keep`, `EventHold.Keep`), and reads one static field while nobody lends.
Per game hour: the loans compared with their marks (a handful), the banks' terms compared (a handful).

Per game day: the same, the score worked out once or twice (one pass over at most 60 days and 120 events), the record read.
The score is cached and worked out again only after something changed.
The app allocates only when a page is written.

## Faults

Every handler is wrapped.
A fault logs one line (`Economy: switched off for this session after an error in ...`), puts the banks' own terms back, takes the app off the phone and leaves the game, its loans and its daily run alone.

## Test buttons

"Test: show my credit report in the log" (written whether or not diagnostics are on), "record a missed payment", "record 30 days of payments on time", "set the rating to 560 / 600 / 760" (each a test entry), "clear test entries" (the score, the days, the events and the totals are exactly as before; `EconomyCheck` proves it), "restore the banks' own terms" (until the next game hour).

## Log lines (with "Developer diagnostics" on)

- `Economy: a credit file is opened for this save on day 12 at 640 (Fair). Factors: ...` (once per save)
- `Economy check (start): 3 bank(s) found, 1 loan(s), file version 1, rating 655 (Fair), 3 bank(s) given the terms for it. <bank>: own 100000 at 8%, in memory 70682 at 11%; ...: PASS`
- `Economy check (day 13): 2 instalment(s) read against the loans as they stood before the run. <street> <number>, 10000, left 9500 before the run: paid 112; ...: missed 100. Rating 655 to 611: PASS`
- `Economy: event Missed (day 12, amount 100, count 1, from 'loans'): rating 655 to 611. Factors: payments 0.31 (4 on time, 1 missed), owed ..., length ..., record ..., applications ...`
- `Economy check (terms, the day): rating 611, 3 bank(s) changed. ...: PASS`
- `Economy check (application): 5000 from <bank> by phone at rating 655, the most it lends now 61000: approved at 11% a year, 41 a day and 4 interest for 122 days, fee 75: PASS`
- `Economy: a bank's dialogue opened with the rating at 560, under 580: the reason was said ...`
- `Economy check (terms restored, the option is off): 3 bank(s) put back. <bank>: own 100000 at 8%, in memory 100000 at 8%; ...: PASS`
- `Finance: tab 1 written, 14 row(s); 2.3 ms.`

## Checks

`RealLifeOverhaul.Build/EconomyCheck` compiles the game-free files as they are (seven for this part) and checks:

- the bands and the weights, the start at 640, a clean history rising, misses and their recovery over 60 days, applications, owed and the record;
- the score always inside 300 to 850 (400 random files), the gate at 580, the terms table at every score, the fee, the cap and the folding;
- the test entries, the save's line with a later version and with damage, loan identity across list changes and a load, and the daily run.

`dotnet build RealLifeOverhaul.Build/EconomyCheck -c Release`, then `dotnet RealLifeOverhaul.Build/EconomyCheck/bin/Release/net10.0/EconomyCheck.dll` ends with `ALL CHECKS PASSED`.

## Not verified (the game could not be run)

Everything on screen: the four tabs (the two in the middle are copies with no picture, expected to draw as plain white tabs), whether the white box holds the longest pages (Credit with two tips, an offer), the app's place on the phone's second page and in the full menu's bar with ten buttons, the icon's glyph.
That `Money` in the game's loan format reads well in the table.

That the bank's dialogue, opened under the threshold, answers every amount with its "maximum exceeded" text and that the notification is seen beside it.
The text the game's localiser gives a transaction type that has no `_label`.
That the hour event of `NewDay` reaches the mod's handler before the run on every path (it is the game's own order; the day's check line says so each day).
