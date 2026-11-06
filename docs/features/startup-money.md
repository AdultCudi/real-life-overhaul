# Startup money

## In a sentence

Money a firm raises from investors belongs to the firm, not to the player's pocket: each firm with investors keeps a **firm account** over the player's one wallet, the wallet must keep the investors' share while the firm runs, the board sends a monthly pack and asks questions when money goes missing, and a firm that closes is wound down with the investors paid first. Honest failure costs the player their shares and nothing else. Taking the money and closing the firm ends in a claim, and for the clear cases in a charge.

## Where the code is

- Rules, game-free: [`Scripts/Tech/Venture/FirmAccount.cs`](../../Assets/Mods/RealLifeOverhaul/Scripts/Tech/Venture/FirmAccount.cs) (`FirmAccount`, `MoneyCatalogue`, `StartupMoney`); the account hangs on the cap table as `Cap.Acc` (`VentureModel.cs`), is opened by `VentureRules.Accept` and the bridge loan, and is saved in `VentureStore` (version 2).
- The game's side: `Scripts/Tech/TechDirector.Money.cs` (the money hook, the day, the ladder, the lawful actions, the wind down) and `TechDirector.MoneyTests.cs`; the firm's page in `Scripts/Tech/Venture/TechApp.Money.cs`.
- The board's question: `Scripts/Wiring/Wiring.BoardLinks.cs`, one desk session. The courts: `Scripts/Justice/InvestorClaims.cs` and `TaxCases.FileInvestorFraud`, reached through `TechBridges.InvestorClaim` and `TechBridges.InvestorFraud` ([Justice](justice.md)).
- Option: none of its own. It runs while "Startups" (the venture part of the tech sector) runs; with that off nothing is raised and nothing is watched.

## The firm account

A ledger kept in cents at the start's prices, made again from the books the game already writes by address, never a second wallet:

> balance = money raised + bridge loans + the firm's own takings less its costs since the account opened (the game's daily statement), less what the player lawfully took out

The investors' part is the balance, never more than they put in. **The floor** is the sum over the player's firms of the investors' part less what has already been counted as taken. The firm page says it: "Your wallet must keep $X while this firm runs." A firm with no investors has no account.

The floor is checked when money moves: the mod listens to the game's own money change event, and only when the wallet has fallen under the floor does it read the game's last transaction. What the wallet fell by, up to what is under the floor, is **counted as taken**, with the day, the amount and the transaction's name ("Vehicle purchase, $2.4M"), and the player is told once a day. Not counted: money spent at the firm's own address (that is the firm's spending, and lowers the account, its own rent and wages included) and anything under a small noise sum. The measure is the gap itself, so rent, wages, loan payments, taxes and running costs anywhere else that push the wallet under the floor are counted too, labelled by what they were ("Running costs of Speedy Bites", "Your rent"). They have to be repaid like any other, but they build suspicion slowly: only what passes 3 percent of the raise counts towards it, and then at a third.

Honest losses are never misuse: costs of the firm lower the account, so a firm that spent all it raised on itself asks nothing of the wallet.

## Lawful ways out

| Way out | Rule |
|---|---|
| Founder salary | Up to a band a month set at each round by its stage (400 thousand to 2.8 million cents a month at the start's prices, per stage). Drawing it lowers the floor. |
| The firm's spending | Anything booked at its address: rent, wages, stock, servers, marketing, fit out. |
| Work bought from the player's other businesses | A related party deal, from the firm's page: the firm pays another business of the player's, both sides booked at their own businesses and the wallet does not move. The fair price is **five days of what the stage burns** (`VentureCatalogue.BurnByStage`); paid at that, it is shown in the board pack and counts as nothing; paid half again or double, the part over is counted as taken. The deals are bounded: one a business every 30 days, and all together over 90 days at most 15 percent of what the stage burns in that time. Past that the board votes: it agrees to one such deal in 90 days, and only for a firm with nothing taken, no question open and a lead whose diligence is under 500; otherwise it refuses and nothing is booked. Every deal is in the pack, and many of them add suspicion even at the fair price. |
| Director's loan | Once, up to a tenth of the raise, 180 days, 10 % a year. Repaid out of free cash only. Left unpaid, it becomes misuse. |
| Dividend | Only from profit not yet paid out, while the firm makes a profit and nothing is owed, and the board agrees (it refuses after a missed target when the investors hold the board). Paid to every holder by their share; the investors' part leaves the wallet as a payment. |

Everything else that takes the wallet under the floor is misuse. **Putting cash back** (the page's button, or the board's question) is spare cash, the part of the wallet over the floor, counted against what was taken, oldest first; nothing moves, the floor rises by it.

## The board

- **Pack.** Every 30 days: burn, runway, revenue against the target, deals with the player's other businesses, salary against the band, the firm account and the wallet. Honest by construction: read from the books. It can be delayed once, by 14 days; that is a warning sign, never a faked pack.
- **Suspicion**, 0 to 100 for each firm, worked out when asked and not kept. The main part is what was taken against the raise (half the raise is 70). Deals over the fair price, spending while the firm burns, a late pack, a missed target and a stonewalled question add. Time (a month's worth at most) and putting cash back bring it down. The player's **Accounting** softens a small shortfall (under a tenth of the raise) and **Leadership** a late pack or a missed target. The page says it in words: calm, a little uneasy, asking, alarmed. With Developer diagnostics on it shows the number and the lead's threshold; with **Law** at 40 it says how early the lead looks.
- **The lead digs** at `55 minus diligence times 25 over 1000` (Ironbark Growth at 850 digs at 34, Lantern Row Angels at 200 at 50), on a pack day. A plain look finds a share of what was taken (40 to 100 % by diligence); an audit, past the threshold by 15 or after the founder is removed, finds all of it, with the transactions.

## The ladder

Each step is taken only if money is counted as taken; an honest firm never leaves the first look.

1. **A question** (pack day, suspicion over the lead's threshold): a desk session, "The board's question", held at the owner's own desk, no manager takes it. Repay what you can, explain (a seeded roll: 450 thousandths plus Accounting times 3 plus Law times 2 minus the size taken times 2, from 5 % to 90 %), or say nothing. The same three buttons are on the firm's page. Unanswered for 7 days it counts as saying nothing.
2. **A repay demand** for what the look found, 14 days. Repaying clears it.
3. **The founder is removed** (suspicion over 60, or the demand ignored): the board replaces the chief executive through the existing path (`ReplaceChief`), the unvested part of the founder's shares is cancelled as a bad leaver, every venture firm shuts its door for 180 days (`VentureFile.ClosedUntil`), Standing (integrity) falls and the City Ledger tells it.
4. **A civil claim** a week on, if money is still counted as taken: the investors sue for it plus a tenth in costs (`InvestorClaims`, an ordinary civil suit in the Legal file).
5. **A criminal case** once sued, if over half the raise is counted as taken: the charge `investor_fraud`, its strength the audit's evidence (`StartupMoney.Evidence`).

## Winding down

A funded firm whose business is gone for any reason (the player closed or sold it, eviction, bankruptcy) is found gone for two daily looks running and then wound down. The firm's cash is worked out from the account; the **bridge loan** is paid first; the rest goes through the cap table's waterfall (`VentureRules.Waterfall`: preferences first, then everyone shares); the investors' lines are paid out of the wallet. What the wallet cannot cover is the investors' claim, with a tenth in costs. If that gap comes with money still counted as taken over a tenth of the raise (misuse followed by a shutdown) the charge is filed too. A firm that honestly ran dry has nothing to share, owes nothing, and is told so. Every funded firm's cap is wound down: none is ever left dormant.

## Older saves

A firm that already had investors when the save was made opens its account the first day it is seen with **nothing raised on the books**, so a zero floor, and 30 days of grace in which nothing is counted, asked or done. Its founder shares count as fully vested. A line from before the account reads as it did. Nobody is punished for the past.

## Going Public

Each player's firms, accounts and claims stay on their own machine. Nothing here goes over the network; the rolls are seeded by the firm's place through a fixed hash, so the same books give the same answers, and the claim and the case are opened in this player's own courts.

## Cost

Nothing per frame. The money hook does a string compare and a float compare for each money change and nothing more while the wallet is over the floor; the game's transactions are read only when it falls under, at most 200 times a day. The rest runs once a game day or on a click.

## The save line

`reallife:tech_ventures`, version 2, with three new parts that an older RLO does not read: `x` (the account of each firm with investors), `m` (what was counted as taken) and `r` (the deals with the player's other businesses). The parts it had keep their fields.

## Terms

Every round now comes with terms (preference multiple, participation, anti-dilution, founder vesting, information rights, a spending limit the board must agree), and the player's skills do real work: see [Startup terms](startup-terms.md).

## Tests

On the options page, under the Tech sector's startups: "My firm's board looks at its books" (the pack is sent now and the board looks at the books), "Wind down my funded firm" (for real) and "Spend investor money on something else" (a quarter of the investors' money goes into startups and comes straight back, so only the books remember it).

## Checks

`EconomyCheck --startup-money`: the exploit run end to end, honest failure, each lawful way out, a deal over the fair price, suspicion and digging by diligence, the ladder's steps, the waterfall at wind down, an old save's grace, the save line read by an old and a new reader, determinism, and the cost of the floor check.
