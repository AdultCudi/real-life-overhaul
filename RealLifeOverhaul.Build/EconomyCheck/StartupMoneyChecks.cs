using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;
using BizLine = RealLifeOverhaul.Economy.BizLine;
using BizLines = RealLifeOverhaul.Economy.BizLines;

// The startup money rules (Scripts/Tech/Venture/FirmAccount.cs): the firm account over the player's wallet, the investor floor, the lawful ways out, the board's
// suspicion and its ladder, the wind down with the waterfall, an old save's grace, the save line and the courts' charge. Murphy's exploit (raise, spend it on anything,
// close the firm, keep it) is run end to end. Game-free; the sources are read for what only the game can do.
internal static partial class Program
{
    private const long Raise = 100000000;   // a million at the start's prices, cents

    private static void StartupMoneyChecks()
    {
        MoneyExploit();
        MoneyHonestFailure();
        MoneyLawfulWays();
        MoneyRelatedParty();
        MoneySuspicion();
        MoneyLadder();
        MoneyWindDown();
        MoneyOldSaves();
        MoneySaveLine();
        MoneyGoingPublic();
        MoneyCost();
        MoneyWiring();
    }

    private static Cap FundedCap(int vc = 6, long amount = Raise, int day = 10)
    {
        Cap c = VentureRules.NewCap("Main~1");
        VentureRules.Accept(c, VentureRules.Sheet(vc, 400000000, amount, 700, 1000, day), 500000, day);
        return c;
    }

    private static void MoneyExploit()
    {
        Cap c = FundedCap();
        var caps = new List<Cap> { c };
        Check(StartupMoney.Has(c) && c.Acc.Raised == Raise && StartupMoney.Balance(c) == Raise && StartupMoney.Floor(c, 11) == Raise,
            "startup money: a term sheet taken opens the firm account at the round, and the wallet must keep all of it (" + StartupMoney.Floor(c, 11) + ")");
        long counted = StartupMoney.Shortfall(caps, 0, Raise, 12, false, "ba:transaction_boatpurchase");
        Check(counted == Raise && c.Acc.Taken == Raise && c.Acc.Misuse.Count == 1 && StartupMoney.Floor(c, 12) == 0,
            "startup money: the whole round spent on a boat is recorded with its day, amount and transaction (" + counted + ")");
        int suspicion = StartupMoney.Suspicion(c, 12, 0, 0);
        int thr = StartupMoney.Threshold(StartupMoney.Diligence(c));
        Check(suspicion >= 60 && StartupMoney.Next(c, 12, suspicion, thr, false) == StartupMoney.ActRemove,
            "startup money: taking the whole round alarms the board at once and the founder is removed (" + suspicion + ")");
        c.Acc.Step = StartupMoney.StepRemoved;
        c.Acc.StepDay = 12;
        Check(StartupMoney.Next(c, 19, suspicion, thr, false) == StartupMoney.ActClaim, "startup money: a week after the founder is removed the investors sue");
        c.Acc.Step = StartupMoney.StepClaim;
        Check(StartupMoney.Next(c, 20, suspicion, thr, false) == StartupMoney.ActCharge, "startup money: misuse over half the raise is reported as fraud once they have sued");
        StartupMoney.Wind w = StartupMoney.Close(c, 0, 30);
        Check(w.Owed >= Raise && w.Paid == 0 && w.Gap == w.Owed && w.Fraud && !w.Dry,
            "startup money: the exploit run to the end (raise, spend elsewhere, close) leaves the investors a claim of " + w.Gap + " and a fraud charge");
        Check(StartupMoney.Evidence(c) >= 0.5f && StartupMoney.Evidence(c) <= 0.95f, "startup money: the audit's evidence for the court is a strength between half and nearly all");
        Check(Regex.IsMatch(ModSource(@"Tech\TechDirector.Venture.cs"), @"WindDown\(") && ModSource(@"Tech\TechDirector.Venture.cs").Contains("_firmsFile.FirmAt(c.Place) == null"),
            "startup money: a funded firm whose business is gone is wound down, no cap is left dormant");
        string deals = ModSource(@"Tech\TechDirector.Deals.cs");
        Check(deals.Contains("RefreshFloor(Save.Day)") && deals.Contains("StartupMoney.Inflow(c, loan, Save.Day)") && ModSource(@"Tech\Venture\VentureRules.Caps.cs").Contains("StartupMoney.Inflow(c, t.Amount, day)"),
            "startup money: the term sheet and the bridge loan open the account and set the floor, they are no free money");
    }

    private static void MoneyHonestFailure()
    {
        Cap c = FundedCap();
        var caps = new List<Cap> { c };
        StartupMoney.Profit(c, -Raise, 40);   // every cent spent on the firm's wages, rent and stock
        Check(StartupMoney.Balance(c) == 0 && StartupMoney.Floor(c, 41) == 0, "startup money: a firm that spent all it raised on itself asks nothing of the wallet");
        Check(StartupMoney.Shortfall(caps, 0, Raise, 41, true, "ba:transaction_x") == 0 && c.Acc.Taken == 0, "startup money: an empty wallet is no shortfall when the money went into the firm");
        int susp = StartupMoney.Suspicion(c, 41, 0, 0);
        Check(susp < 15 && StartupMoney.Next(c, 41, susp, 34, true) == StartupMoney.ActNone, "startup money: honest failure never leaves the board's first look (" + susp + ")");
        StartupMoney.Wind w = StartupMoney.Close(c, 0, 45);
        Check(w.Dry && w.Owed == 0 && w.Gap == 0 && !w.Fraud, "startup money: an honest firm run dry owes nothing at its wind down");
        Cap half = FundedCap();
        StartupMoney.Profit(half, -Raise * 6 / 10, 40);
        StartupMoney.Wind part = StartupMoney.Close(half, Raise * 4 / 10, 45);
        Check(part.Owed > 0 && part.Owed <= Raise * 4 / 10 && part.Gap == 0 && part.Paid == part.Owed && !part.Fraud,
            "startup money: what an honest firm still holds goes to the investors from the wallet and nothing more is claimed (" + part.Owed + ")");
    }

    private static void MoneyLawfulWays()
    {
        Cap c = FundedCap();
        long floor = StartupMoney.Floor(c, 11);
        long band = StartupMoney.Band(c.Stage);
        Check(c.Acc.Band == band && band > 0 && StartupMoney.Band(0) < StartupMoney.Band(5), "startup money: the salary band is set at the round and rises with the stage");
        long drawn = StartupMoney.Draw(c, band * 3, 11);
        Check(drawn == band && StartupMoney.Floor(c, 11) == floor - band && StartupMoney.Draw(c, 1, 12) == 0, "startup money: the salary is held to the band, lowers the floor by what was drawn, and a second draw the same month gets nothing");
        Check(StartupMoney.Draw(c, band, 11 + MoneyCatalogue.PackDays) == band, "startup money: a new month brings a new band");
        long lent = StartupMoney.Lend(c, 20);
        Check(lent == StartupMoney.LoanMax(c) && lent == Raise / 10 && StartupMoney.Lend(c, 21) == 0, "startup money: a director's loan is allowed once, up to a tenth of the raise");
        long owed = StartupMoney.LoanOwed(c, 20 + 180);
        Check(owed > lent && owed == lent + lent * 1000 * 180 / 365 / 10000, "startup money: the loan owes interest over its 180 days (" + owed + ")");
        Check(!StartupMoney.RepayLoan(c, owed - 1, 200) && StartupMoney.RepayLoan(c, owed, 200) && c.Acc.Loan == 0 && c.Acc.Own == owed - lent, "startup money: the loan is repaid with its interest out of free cash only");
        Cap late = FundedCap();
        StartupMoney.Lend(late, 20);
        Check(!StartupMoney.LoanLapsed(late, 200) && StartupMoney.LoanLapsed(late, 201) && late.Acc.Taken > 0 && late.Acc.Loan == 0 && late.Acc.Misuse.Count == 1,
            "startup money: a loan left unpaid past its 180 days becomes misuse");
        Cap d = FundedCap();
        Check(StartupMoney.DividendRoom(d, 1) == 0, "startup money: no dividend before the firm has made a profit");
        StartupMoney.Profit(d, 20000000, 30);
        long room = StartupMoney.DividendRoom(d, 100);
        Check(room == 20000000 && StartupMoney.DividendRoom(d, 0) == 0, "startup money: a dividend is only from profit, and only while the firm makes one");
        long theirs = StartupMoney.Dividend(d, room);
        int share = VentureRules.PlayerShare(d);
        Check(theirs == room * (1000 - share) / 1000 && theirs > 0 && theirs < room && d.Acc.Dividends == room && StartupMoney.DividendRoom(d, 100) == 0,
            "startup money: a dividend is paid to every holder by their share, the investors' part leaving the wallet (" + theirs + ")");
        StartupMoney.Profit(d, 5000000, 31);
        StartupMoney.Misuse(d, 1000, 31, false, "x");
        Check(StartupMoney.DividendRoom(d, 100) == 0, "startup money: no dividend while money is counted as taken");
        Cap vote = FundedCap();
        vote.Misses = 1;
        Check(StartupMoney.DividendVote(vote) == (vote.VcSeats * 2 <= vote.BoardSeats), "startup money: the board votes a dividend down after a missed target when the investors hold it");
    }

    private static void MoneyRelatedParty()
    {
        Cap c = FundedCap();
        long fair = StartupMoney.Fair(c);
        Check(fair == VentureCatalogue.BurnByStage[c.Stage] * MoneyCatalogue.RelatedDays, "startup money: the fair price of a unit of work is days of the stage's burn");
        Check(StartupMoney.Related(c, fair, fair, "Shop", 12) == 0 && c.Acc.Taken == 0 && c.Acc.RelatedDeals == 1, "startup money: a deal at the fair price is shown in the pack and counts as nothing taken");
        long over = StartupMoney.Related(c, fair * 2, fair, "Shop", 13);
        Check(over == fair && c.Acc.Taken == fair && c.Acc.RelatedOver == fair && c.Acc.Related.Count == 2 && c.Acc.Misuse.Count == 1, "startup money: paying twice the fair price counts the excess as taken");
        for (int i = 0; i < 9; i++)
        {
            StartupMoney.Related(c, fair, fair, "Shop", 14 + i);
        }
        Check(c.Acc.Related.Count == MoneyCatalogue.RelatedKept && c.Acc.RelatedDeals == 11, "startup money: the pack keeps the latest deals and counts them all");
        Check(BizLines.Of("reallife_transaction_tech_related_cost") == BizLine.Cost && BizLines.Of("reallife_transaction_tech_related_takings") == BizLine.Takings
              && BizLines.Of("reallife_transaction_tech_venture_dividend") == BizLine.Capital && BizLines.Of("reallife_transaction_tech_venture_wind_down") == BizLine.Capital,
            "startup money: the new money lines are in BizMan's lists, a cost, takings and two capital moves");
    }

    private static void MoneySuspicion()
    {
        Cap strict = FundedCap(6), easy = FundedCap(0);
        int thr6 = StartupMoney.Threshold(StartupMoney.Diligence(strict)), thr0 = StartupMoney.Threshold(StartupMoney.Diligence(easy));
        Check(StartupMoney.Diligence(strict) == 850 && StartupMoney.Diligence(easy) == 250 && thr6 < thr0 && thr6 > 30 && thr0 < 60,
            "startup money: a stricter lead digs sooner (" + thr6 + " against " + thr0 + "), always between a question's 30 and removal's 60");
        StartupMoney.Misuse(strict, Raise / 4, 50, false, "a");
        StartupMoney.Misuse(easy, Raise / 4, 50, false, "a");
        int s6 = StartupMoney.Suspicion(strict, 50, 0, 0), s0 = StartupMoney.Suspicion(easy, 50, 0, 0);
        Check(s6 == s0 && s6 >= thr6 && s0 < thr0, "startup money: a quarter of the raise taken is dug into by the strict lead and let pass by the easy one (" + s6 + ")");
        Check(StartupMoney.Next(strict, 50, s6, thr6, true) == StartupMoney.ActAsk && StartupMoney.Next(easy, 50, s0, thr0, true) == StartupMoney.ActNone
              && StartupMoney.Next(strict, 50, s6, thr6, false) == StartupMoney.ActNone, "startup money: the lead asks on a pack day, not on any other");
        int later = StartupMoney.Suspicion(strict, 50 + 100, 0, 0);
        Check(later < s6 && later >= s6 - 30, "startup money: time brings suspicion down, by a month's worth at most (" + later + ")");
        Cap small = FundedCap(6);
        StartupMoney.Misuse(small, Raise / 20, 50, false, "a");
        int plain = StartupMoney.Suspicion(small, 50, 0, 0), skilled = StartupMoney.Suspicion(small, 50, 100, 0);
        Check(skilled < plain && StartupMoney.Suspicion(strict, 50, 100, 0) == s6, "startup money: Accounting softens a small slip and does nothing for a large sum");
        Cap lateOne = FundedCap(6);
        StartupMoney.Misuse(lateOne, Raise / 10, 50, false, "a");
        StartupMoney.Delay(lateOne, 50);
        int withDelay = StartupMoney.Suspicion(lateOne, 50, 0, 0), led = StartupMoney.Suspicion(lateOne, 50, 0, 100);
        Check(withDelay > StartupMoney.Suspicion(small, 50, 0, 0) && led < withDelay, "startup money: a delayed pack is a warning sign and Leadership calms the board over it");
        Check(StartupMoney.Delay(lateOne, 51) == false && lateOne.Acc.PackDay == 10 + MoneyCatalogue.PackDays + MoneyCatalogue.PackDelayDays, "startup money: the pack can be delayed once, by two weeks, never faked");
        long back = StartupMoney.Repay(strict, Raise, Raise, 60);
        Check(back == Raise / 4 && strict.Acc.Taken == 0 && StartupMoney.Suspicion(strict, 60, 0, 0) < 15 && StartupMoney.Floor(strict, 60) == Raise,
            "startup money: paying the money back clears what was taken, raises the floor again and calms the board");
        Check(StartupMoney.LookFinds(850) > StartupMoney.LookFinds(250) && StartupMoney.LookFinds(1000) <= 1000, "startup money: a stricter lead's look finds more, an audit finds all");
        Cap audit = FundedCap(6);
        StartupMoney.Misuse(audit, Raise / 4, 50, false, "a");
        Check(!StartupMoney.Audit(audit, StartupMoney.Suspicion(audit, 50, 0, 0), thr6) && StartupMoney.Audit(audit, thr6 + 15, thr6), "startup money: higher suspicion brings a forensic audit");
        Check(StartupMoney.ExplainChance(strict, 100, 100) > StartupMoney.ExplainChance(strict, 0, 0), "startup money: Accounting and Law help the player explain to the board");
        StartupMoney.Misuse(audit, Raise / 2, 51, false, "b");
        Check(StartupMoney.ExplainChance(audit, 100, 100) < StartupMoney.ExplainChance(strict, 100, 100), "startup money: a large sum is hard to explain whatever the skills");
    }

    private static void MoneyLadder()
    {
        Cap c = FundedCap(6);
        int thr = StartupMoney.Threshold(StartupMoney.Diligence(c));
        StartupMoney.Misuse(c, Raise / 4, 100, false, "ba:transaction_vehiclepurchase");
        int s = StartupMoney.Suspicion(c, 100, 0, 0);
        Check(StartupMoney.Next(c, 100, s, thr, true) == StartupMoney.ActAsk, "startup money: ladder step 1, the board asks");
        c.Acc.Step = StartupMoney.StepAsked;
        c.Acc.AskDay = 100;
        Check(StartupMoney.Next(c, 106, s, thr, true) == StartupMoney.ActNone && StartupMoney.Next(c, 107, s, thr, false) == StartupMoney.ActDemand, "startup money: step 2, a question left a week becomes a repay demand");
        c.Acc.Step = StartupMoney.StepDemand;
        c.Acc.StepDay = 107;
        Check(StartupMoney.Next(c, 120, s, thr, false) == StartupMoney.ActNone && StartupMoney.Next(c, 121, s, thr, false) == StartupMoney.ActRemove, "startup money: step 3, a demand ignored for 14 days removes the founder");
        long unvested = StartupMoney.BadLeaver(c, 121, 360);
        Holder me = c.Holders.Find(h => h.Kind == (int)HolderKind.Player);
        Check(unvested > 0 && me.Shares + unvested == VentureRules.FounderShares && unvested == VentureRules.FounderShares * (1000 - VentureRules.Vested(10, 121, 360)) / 1000,
            "startup money: the founder's unvested shares go back as a bad leaver (" + unvested + ")");
        Check(StartupMoney.BadLeaver(c, 121 + 4 * 360, 360) == 0, "startup money: shares fully vested cost the founder nothing");
        c.Acc.Step = StartupMoney.StepRemoved;
        c.Acc.StepDay = 121;
        Check(StartupMoney.Next(c, 127, s, thr, false) == StartupMoney.ActNone && StartupMoney.Next(c, 128, s, thr, false) == StartupMoney.ActClaim, "startup money: step 4, a week after removal the investors file a civil claim");
        c.Acc.Step = StartupMoney.StepClaim;
        Check(StartupMoney.Next(c, 130, s, thr, false) == StartupMoney.ActNone, "startup money: a quarter of the raise is a claim, not yet a crime");
        StartupMoney.Misuse(c, Raise / 4 + 1, 130, false, "b");
        Check(StartupMoney.Next(c, 130, s, thr, false) == StartupMoney.ActCharge, "startup money: step 5, over half the raise taken is reported");
        c.Acc.Case = 7;
        Check(StartupMoney.Next(c, 131, s, thr, false) == StartupMoney.ActNone, "startup money: the case is reported once");
        Cap honest = FundedCap(6);
        bool never = true;
        for (int step = 0; step <= StartupMoney.StepCharged; step++)
        {
            honest.Acc.Step = step;
            never &= StartupMoney.Next(honest, 500, 0, thr, true) == StartupMoney.ActNone;
        }
        Check(never, "startup money: with nothing taken the ladder never acts, at any step");
        Cap fix = FundedCap(6);
        StartupMoney.Misuse(fix, Raise / 4, 100, false, "a");
        fix.Acc.Step = StartupMoney.StepDemand;
        StartupMoney.Repay(fix, Raise, Raise, 105);
        Check(fix.Acc.Step == StartupMoney.StepNone && !fix.Acc.Stonewalled, "startup money: repaying in time brings the ladder back to the start");
        Cap big = FundedCap(6);
        StartupMoney.Misuse(big, Raise * 45 / 100, 100, false, "a");
        Check(StartupMoney.Next(big, 100, StartupMoney.Suspicion(big, 100, 0, 0), thr, false) == StartupMoney.ActRemove, "startup money: alarmed past 60, the board skips the questions");
        string money = ModSource(@"Tech\TechDirector.Money.cs");
        Check(money.Contains("ReplaceChief(c, firm, day)") && money.Contains("BadLeaver") && money.Contains("ShutDoors(day)") && money.Contains("TechBridges.Fame?.Invoke(3,")
              && money.Contains("TechBridges.InvestorClaim") && money.Contains("TechBridges.InvestorFraud") && money.Contains("reallife_tech_news_removed"),
            "startup money: from step 3 the founder is replaced through the board's path, every door shuts, Standing falls and the paper tells it");
    }

    private static void MoneyWindDown()
    {
        Cap c = FundedCap(2);
        c.Bridge = 10000000;
        StartupMoney.Inflow(c, 10000000, 20);
        StartupMoney.Profit(c, -30000000, 30);
        long balance = StartupMoney.Balance(c);
        StartupMoney.Wind full = StartupMoney.Close(c, balance, 31);
        Check(full.Bridge == 10000000 && full.Price == balance - 10000000 && full.Owed <= balance && full.Owed >= 10000000 && full.Gap == 0 && full.Paid == full.Owed,
            "startup money: the wind down pays the bridge first, then shares the rest by the waterfall (" + full.Owed + " of " + balance + ")");
        StartupMoney.Wind shy = StartupMoney.Close(c, balance / 2, 31);
        Check(shy.Paid == balance / 2 && shy.Gap == shy.Owed - shy.Paid && shy.Gap > 0 && !shy.Fraud, "startup money: what the wallet cannot cover becomes the claim, and a small gap with nothing taken is no fraud");
        Cap two = FundedCap(2);
        VentureRules.Accept(two, VentureRules.Sheet(4, 1000000000, 300000000, 700, 1000, 60), 500000, 60);
        long[] pay = VentureRules.Waterfall(two.Holders, StartupMoney.Balance(two));
        long sum = 0;
        for (int i = 0; i < pay.Length; i++)
        {
            sum += pay[i];
        }
        Check(sum <= StartupMoney.Balance(two) && sum >= StartupMoney.Balance(two) - 2 && StartupMoney.Close(two, StartupMoney.Balance(two), 70).Owed <= sum, "startup money: two rounds are shared by their preferences and shares, never more than the firm holds");
        Cap ran = FundedCap(2);
        StartupMoney.Misuse(ran, Raise / 20, 40, false, "a");
        StartupMoney.Wind small = StartupMoney.Close(ran, 0, 45);
        Check(small.Gap > 0 && !small.Fraud, "startup money: a small amount taken followed by a shutdown is a claim but not yet fraud");
        Cap bad = FundedCap(2);
        StartupMoney.Misuse(bad, Raise * 15 / 100, 40, false, "a");
        StartupMoney.Wind clear = StartupMoney.Close(bad, 0, 45);
        Check(clear.Fraud && clear.Taken == Raise * 15 / 100, "startup money: over a tenth of the raise taken followed by a shutdown is the clear pattern");
        Cap repaid = FundedCap(2);
        StartupMoney.Misuse(repaid, Raise * 15 / 100, 40, false, "a");
        StartupMoney.Wind cover = StartupMoney.Close(repaid, Raise * 2, 45);
        Check(cover.Gap == 0 && !cover.Fraud && cover.Paid == cover.Owed, "startup money: a founder who has the money to hand back at the close owes no claim and faces no charge");
        Check(StartupMoney.Close(FundedCap(2), 0, 5).Owed > 0, "startup money: the wind down is the same whoever closes the firm: only the books decide");
    }

    private static void MoneyOldSaves()
    {
        Cap c = VentureRules.NewCap("Old~1");
        c.VcSeats = 1;
        c.Holders.Add(new Holder { Kind = (int)HolderKind.Vc, Who = 6, Invested = Raise, Shares = 4000000, Preference = Raise });
        Check(!c.Acc.Open && StartupMoney.OpenOld(c, 500, 360) && c.Acc.Open && !StartupMoney.OpenOld(c, 501, 360), "startup money: a running firm in an old save opens its account once");
        Check(c.Acc.Raised == 0 && StartupMoney.Floor(c, 501) == 0 && c.Acc.GraceTo == 530, "startup money: it opens with a zero investor floor and 30 days of grace");
        VentureRules.Accept(c, VentureRules.Sheet(6, 400000000, Raise, 700, 1000, 510), 500000, 510);
        Check(StartupMoney.Floor(c, 515) == 0, "startup money: a round taken in the grace asks nothing of the wallet until it ends");
        StartupMoney.Misuse(c, Raise / 2, 515, false, "a");
        Check(StartupMoney.Suspicion(c, 515, 0, 0) == 0 && StartupMoney.Next(c, 515, 100, 34, true) == StartupMoney.ActNone, "startup money: nothing is asked or done in the grace");
        Check(StartupMoney.Floor(c, 531) == Math.Max(0, Raise - Raise / 2), "startup money: after the grace the rules apply to what was raised since (" + StartupMoney.Floor(c, 531) + ")");
        Cap plain = VentureRules.NewCap("Own~1");
        Check(!StartupMoney.OpenOld(plain, 5, 360) && !plain.Acc.Open && StartupMoney.Floor(plain, 6) == 0 && !StartupMoney.Has(plain), "startup money: a firm with no investors has no account");
        Cap oldFounder = VentureRules.NewCap("Old~2");
        oldFounder.VcSeats = 1;
        StartupMoney.OpenOld(oldFounder, 500, 360);
        Check(StartupMoney.BadLeaver(oldFounder, 500, 360) == 0, "startup money: nobody's founder shares are called unvested for the past");
    }

    private static void MoneySaveLine()
    {
        VentureFile f = SavedMoneyFile();
        string line = VentureStore.Write(f);
        VentureFile back = VentureStore.Read(line, out int version);
        Cap c = back.CapOf("Main~1"), was = f.CapOf("Main~1");
        FirmAccount a = c.Acc, b = was.Acc;
        Check(version == VentureStore.Version && VentureStore.Version == 2 && a.Open && a.Raised == b.Raised && a.Own == b.Own && a.Out == b.Out && a.Taken == b.Taken && a.Band == b.Band
              && a.Loan == b.Loan && a.LoanDay == b.LoanDay && a.LoanUsed && a.PackDay == b.PackDay && a.Step == b.Step && a.Suit == b.Suit && a.Case == b.Case && a.Name == b.Name && a.Gone == b.Gone
              && a.RelatedDeals == b.RelatedDeals && a.Misuse.Count == b.Misuse.Count && a.Misuse[0].Key == b.Misuse[0].Key && a.Related.Count == b.Related.Count && a.Related[0].Fair == b.Related[0].Fair
              && StartupMoney.Floor(c, 90) == StartupMoney.Floor(was, 90) && VentureStore.Write(back) == line,
            "startup money: the firm account, what was taken and the deals come back from the save line as they were");
        string old = Regex.Replace(line, "~[xmr]:[^~]*", string.Empty).Replace("v=2", "v=1");
        VentureFile older = VentureStore.Read(old, out int oldVersion);
        Cap oc = older.CapOf("Main~1");
        Check(oldVersion == 1 && oc != null && !oc.Acc.Open && oc.Holders.Count == was.Holders.Count && oc.Total == was.Total && oc.Bridge == was.Bridge && oc.Stage == was.Stage && StartupMoney.OpenOld(oc, 100, 360),
            "startup money: a line from before the account still reads, and the firm opens its account with a grace");
        var parts = new HashSet<char>();
        foreach (Match m in Regex.Matches(line, "~([a-z]):"))
        {
            parts.Add(m.Groups[1].Value[0]);
        }
        string cPart = Regex.Match(line, "~c:([^~]*)").Groups[1].Value;
        string first = cPart.Split(';')[0];
        Check(parts.SetEquals(new HashSet<char>("hfvsackt" + "xmr")) && first.Split('|').Length == 18 && Regex.Match(line, "~k:([^~]*)").Success,
            "startup money: the new state is in parts of its own (x, m, r) and the old parts keep their fields, so an older RLO reads the line and leaves them out");
        VentureFile empty = new VentureFile { Seed = 3 };
        Cap none = VentureRules.NewCap("Solo~1");
        empty.Caps.Add(none);
        Check(!Regex.IsMatch(VentureStore.Write(empty), "~x:[^~]*Solo"), "startup money: a firm with no investors writes no account");
    }

    private static VentureFile SavedMoneyFile()
    {
        var f = new VentureFile { Seed = 21, FirstDay = 3, LastDay = 80 };
        Cap c = FundedCap(6);
        c.Acc.Name = "Main Street Labs";
        StartupMoney.Profit(c, -12345678, 40);
        StartupMoney.Draw(c, 1, 41);
        StartupMoney.Lend(c, 42);
        StartupMoney.Misuse(c, 777777, 50, true, "ba:transaction_boatpurchase");
        StartupMoney.Related(c, 4000000, 2000000, "Corner Shop", 55);
        StartupMoney.Delay(c, 60);
        c.Acc.Step = StartupMoney.StepDemand;
        c.Acc.StepDay = 70;
        c.Acc.Suit = 4;
        c.Acc.Case = 9;
        c.Acc.Gone = 0;
        f.Caps.Add(c);
        return f;
    }

    private static string MoneyRun()
    {
        VentureFile f = SavedMoneyFile();
        Cap c = f.Caps[0];
        var caps = new List<Cap> { c };
        StartupMoney.Shortfall(caps, 1000, 5000000, 80, false, "ba:transaction_x");
        StartupMoney.Repay(c, 3000000, 3000000, 81);
        c.Acc.Step = StartupMoney.Next(c, 90, StartupMoney.Suspicion(c, 90, 20, 30), 34, true);
        return VentureStore.Write(f);
    }

    private static void MoneyGoingPublic()
    {
        Check(MoneyRun() == MoneyRun() && StartupMoney.Hash("abc") == 0x1A47E90B && StartupMoney.Hash(null) == (int)(2166136261u & 0x7FFFFFFF),
            "startup money: the same books give the same line and the same rolls on every machine (a fixed hash, no clock, no random)");
        string money = ModSource(@"Tech\TechDirector.Money.cs") + ModSource(@"Tech\Venture\FirmAccount.cs") + ModSource(@"Justice\InvestorClaims.cs") + ModSource(@"Wiring\Wiring.BoardLinks.cs");
        string[] wire = { "GoingPublic.Send", "GoingPublic.Share", "GoingPublic.Publish", "Broadcast(", "SendTo(", "Rpc", "System.Random", "DateTime.Now", "UnityEngine.Random" };
        string found = string.Empty;
        foreach (string w in wire)
        {
            found += money.Contains(w) ? w + " " : string.Empty;
        }
        Check(found.Length == 0, "startup money: nothing goes over the network and nothing is random (" + found.Trim() + ")");
        string claims = ModSource(@"Justice\InvestorClaims.cs");
        Check(claims.Contains("DebtClaims.Lodge(claim, \"investor\")") && claims.Contains("DebtClaims.Available"), "startup money: the investors' claim is one of this player's own civil suits, filed only while the courts run");
    }

    private static void MoneyCost()
    {
        string money = ModSource(@"Tech\TechDirector.Money.cs");
        string hook = MemberText(money, "private void OnMoneyEvent(string gameEvent)");
        int compare = hook.IndexOf("gameEvent != MoneyChange", StringComparison.Ordinal), wall = hook.IndexOf("wallet >= _floorNow", StringComparison.Ordinal), look = hook.IndexOf("LastTransaction(save)", StringComparison.Ordinal);
        Check(hook.Length > 0 && compare >= 0 && wall > compare && look > wall && hook.IndexOf("now >= was", StringComparison.Ordinal) < look && hook.Contains("MoneyCatalogue.ScansPerDay"),
            "startup money: the floor is checked only when money moves, the transactions are looked at only when the wallet falls under it, at most " + MoneyCatalogue.ScansPerDay + " times a day");
        Check(!Regex.IsMatch(money, @"\b(Update|LateUpdate|FixedUpdate|OnGUI|StartCoroutine)\(") && Occurs(ModSource(@"Tech\TechDirector.cs"), "KeepMoney()") == 1,
            "startup money: nothing per frame, the hook is kept attached once a second with a reference compare");
        var caps = new List<Cap> { FundedCap(6), FundedCap(2), FundedCap(1), FundedCap(0) };
        var clock = Stopwatch.StartNew();
        long sink = 0;
        for (int i = 0; i < 200000; i++)
        {
            sink += StartupMoney.FloorOf(caps, 20) > i ? 1 : 0;
        }
        clock.Stop();
        Check(sink >= 0 && clock.ElapsedMilliseconds < 1500, "startup money: the floor of four firms is added up 200000 times in " + clock.ElapsedMilliseconds + " ms, allocating nothing");
    }

    private static void MoneyWiring()
    {
        ChargeDef fraud = JusticeLaw.Find(JusticeLaw.InvestorFraud);
        Check(fraud != null && fraud.Id == "investor_fraud" && fraud.NameKey == "reallife_justice_charge_investor_fraud" && fraud.Class == ChargeClass.Felony && fraud.Fine > 0 && fraud.PrisonDays > 0,
            "startup money: investor fraud is a charge of its own, a felony");
        string cases = ModSource(@"Justice\TaxCases.cs");
        Check(cases.Contains("FileInvestorFraud(int amount, float evidence)") && cases.Contains("Evidence = new[]") && ModSource(@"Justice\CourtHearing.cs").Contains("reallife_justice_court_figure_investors"),
            "startup money: the charge is opened on a summons with the audit's evidence as its strength, and the court reads the sum");
        string links = ModSource(@"Wiring\TechLinks.cs");
        Check(links.Contains("TechBridges.InvestorClaim = InvestorClaims.File") && links.Contains("TechBridges.InvestorFraud = TaxCases.FileInvestorFraud") && ModSource(@"Tech\TechBridges.cs").Contains("InvestorFraud = null"),
            "startup money: the courts are reached through the tech bridges, and let go with them");
        string board = ModSource(@"Wiring\Wiring.BoardLinks.cs");
        Check(board.Contains("Delegable = false") && board.Contains("SessionRegistry.Add(Kind())") && board.Contains("SessionRegistry.Remove(Id)") && ModSource(@"Wiring\Wiring.SectorLinks.cs").Contains("BoardLinks.Connect()"),
            "startup money: the board's question is a desk session, held by the owner alone, put in and taken out with the venture part");
    }
}
