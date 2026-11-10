using System;
using UI.Notification;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the market director: the extras' entries on the options page's Tests. Each does what it says when pressed, or
    /// says why not in a notice, and writes one "Stock extras check (...)" line. On a press only.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
        private const string TestSplitName = "Stock extras check (a split at the next open)";
        private const string TestReverseName = "Stock extras check (shares put together at the next open)";
        private const string TestFundName = "Stock extras check (the fund against its parts)";
        private const string TestBlockName = "Stock extras check (a block quoted)";
        private const string TestRivalName = "Stock extras check (a rival looks at the market)";
        private const string TestBankName = "Stock extras check (the bank's own books)";

        // The extras' tests: the market running and settled, the option on, and never in another player's city.
        private bool ExtrasTestReady(string test)
        {
            if (!TestReady(test, true))
            {
                return false;
            }
            if (!Settings.MarketsExtras)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (GoingPublic.InSession || _following)
            {
                TestRefusals.TellKey(Logger, test, "reallife_stx_test_session");
                return false;
            }
            return true;
        }

        // The company a test is about: one the player holds, else one they watch, else the first of the city's own; a rival's holding when asked for.
        private Stock ExtrasTestStock(bool holding)
        {
            MarketState s = _engine.State;
            Stock first = null;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < s.Stocks.Count; i++)
                {
                    Stock k = s.Stocks[i];
                    if (!k.Listed || k.Own != null || k.BidCents > 0 || k.SplitDay > 0 || (holding && (k.Def == null || !k.Def.Holding)))
                    {
                        continue;
                    }
                    first = first ?? k;
                    if (pass == 0 ? s.Held(k.Id) > 0 : pass == 1 ? s.Watching(k.Id) : true)
                    {
                        return k;
                    }
                }
            }
            return first;
        }

        /// <summary>Test: a company the player holds (else watches, else the first) splits two for one at the next open.</summary>
        public static void TestExtrasSplit()
        {
            Instance?.ExtrasSplitTest(TestSplitName, 2);
        }

        /// <summary>Test: the same company's shares are put together, ten into one, at the next open.</summary>
        public static void TestExtrasReverse()
        {
            Instance?.ExtrasSplitTest(TestReverseName, -10);
        }

        private void ExtrasSplitTest(string test, int ratio)
        {
            FinishMoment();
            if (!ExtrasTestReady(test))
            {
                return;
            }
            Stock k = ExtrasTestStock(false);
            if (k == null || !TestSplit(k.Id, ratio))
            {
                TestRefusals.TellKey(Logger, test, "reallife_stx_test_none");
                return;
            }
            Queue(NotificationType.Info, ratio > 0 ? "reallife_mkt_note_split" : "reallife_stx_note_reverse", "name", k.Name, "ratio", Math.Abs(ratio).ToString(), "day", k.SplitDay.ToString());
            Logger?.Info(test + ": " + k.Name + " (" + k.Ticker + ") " + (ratio > 0 ? ratio + " for 1" : "1 for " + -ratio) + " on day " + k.SplitDay + ", at " + k.Last + " cents with "
                         + k.Shares + " shares; the player holds " + _engine.State.Held(k.Id) + ": PASS");
        }

        /// <summary>Test: what a hundred units of the fund cost and fetch now, against what they hold; nothing is bought.</summary>
        public static void TestExtrasFund()
        {
            Instance?.ExtrasFundTest();
        }

        private void ExtrasFundTest()
        {
            FinishMoment();
            if (!ExtrasTestReady(TestFundName))
            {
                return;
            }
            MarketState s = _engine.State;
            int phase = Trading ? s.Phase : MarketCatalogue.PhaseOpen + 1;
            FundDeal buy = FundRules.Quote(s, 100, true, phase, CommissionLeast, long.MaxValue, 0, true, true, false);
            FundDeal sell = FundRules.Quote(s, 100, false, phase, CommissionLeast, long.MaxValue, 100, true, true, false);
            if (buy.NavCents <= 0)
            {
                TestRefusals.TellKey(Logger, TestFundName, "reallife_stx_test_none");
                return;
            }
            bool fair = buy.PriceCents >= buy.NavCents && sell.PriceCents <= sell.NavCents;
            Queue(NotificationType.Info, "reallife_stx_test_fund_note", "nav", MarketText.Price(buy.NavCents), "buy", MarketText.Price(buy.PriceCents), "sell", MarketText.Price(sell.PriceCents));
            Logger?.Info(TestFundName + ": a unit holds " + buy.NavCents + " cents (the index " + s.IndexLast + " hundredths, the fund " + s.Extras.Track.ToString("0.000000", Inv)
                         + " of it); 100 units cost " + buy.PriceCents + " and fetch " + sell.PriceCents + " cents each; the player holds " + s.Extras.Units + ": "
                         + (fair ? "PASS" : "FAIL (a unit dealt better than its parts)"));
        }

        /// <summary>Test: the largest block of a rival's holding (else of a city company) the player could buy now, quoted; nothing is bought.</summary>
        public static void TestExtrasBlock()
        {
            Instance?.ExtrasBlockTest();
        }

        private void ExtrasBlockTest()
        {
            FinishMoment();
            if (!ExtrasTestReady(TestBlockName))
            {
                return;
            }
            MarketState s = _engine.State;
            Stock k = ExtrasTestStock(true) ?? ExtrasTestStock(false);
            if (k == null)
            {
                TestRefusals.TellKey(Logger, TestBlockName, "reallife_stx_test_none");
                return;
            }
            int phase = Trading ? s.Phase : MarketCatalogue.PhaseOpen + 1;
            long qty = Math.Max(BlockRules.Least(k), BlockRules.Most(s, k, true));
            BlockQuote q = BlockRules.Quote(s, k, true, qty, long.MaxValue, CommissionLeast, phase, true, true, false);
            Quote now = MarketTrading.QuoteOf(s, k, phase);
            bool fair = q.Cents == 0 || q.Cents >= now.Ask;
            Queue(NotificationType.Info, "reallife_stx_test_block_note", "name", k.Name, "shares", qty.ToString("N0"), "price", MarketText.Price(Math.Max(q.Cents, now.Ask)),
                "stake", MarketText.Percent(BlockRules.StakeBp(s.Held(k.Id) + qty, k.Shares)));
            Logger?.Info(TestBlockName + ": " + qty + " of " + k.Name + " (" + k.Ticker + ") at " + q.Cents + " cents against an offer of " + now.Ask + " (" + q.ImpactBp + " bp), "
                         + q.Total + " in all, " + q.StakeBp + " bp of the company (" + q.Why + "): " + (fair ? "PASS" : "FAIL (a block under the offer)"));
        }

        /// <summary>Test: the first rival tycoon who is forward looks at the market now, whatever the day.</summary>
        public static void TestExtrasRival()
        {
            Instance?.ExtrasRivalTest();
        }

        private void ExtrasRivalTest()
        {
            FinishMoment();
            if (!ExtrasTestReady(TestRivalName))
            {
                return;
            }
            RivalLook look = ExtrasBridges.Rival;
            MarketState s = _engine.State;
            for (int t = 0; look != null && t < s.Extras.Rivals.Length; t++)
            {
                if (!look(t, out long worth, out int holderStock, out string _, out string name, out bool _))
                {
                    continue;
                }
                RivalBook book = s.Extras.Rivals[t];
                long units = RivalMarket.FundUnits(s, book, worth);
                book.Units += units;
                book.UnitsCost += (long)(units * FundRules.Nav(s) / 100.0);
                string dealt = RivalBuys(s, t, book, holderStock, RivalMarket.Pick(s, t, holderStock, Save.Day, book), worth * ExtrasCatalogue.RivalBlockBp / 10000L);
                Queue(NotificationType.Info, "reallife_stx_test_rival_note", "rival", name, "units", units.ToString("N0"), "blocks", book.Stakes.Count.ToString());
                Logger?.Info(TestRivalName + ": " + name + " (worth " + worth + ") bought " + units + " unit(s) of the fund" + dealt + "; holds " + book.Units + " unit(s) and "
                             + book.Stakes.Count + " block(s) worth " + RivalMarket.Worth(s, book) + ": PASS");
                Changes();
                return;
            }
            TestRefusals.TellKey(Logger, TestRivalName, "reallife_stx_test_no_rival");
        }

        /// <summary>Test: the player's bank as a listing would read it now: its quarters, its worth on its own books, and what stands in the way.</summary>
        public static void TestExtrasBank()
        {
            Instance?.ExtrasBankTest();
        }

        private void ExtrasBankTest()
        {
            FinishMoment();
            if (!ExtrasTestReady(TestBankName))
            {
                return;
            }
            BankFacts f = BankNow();
            if (!f.Running)
            {
                TestRefusals.TellKey(Logger, TestBankName, "reallife_stx_test_no_bank");
                return;
            }
            MarketState s = _engine.State;
            BankWhy why = BankWhyNow(out double worth);
            int quarters = BankFloatRules.Quarters(s.Extras.Bank, Save.Day, Year, out int profitable);
            double capital = f.CapitalCents / 100.0;
            bool own = worth <= capital * ExtrasCatalogue.BankBookMost + 1.0;
            Queue(NotificationType.Info, "reallife_stx_test_bank_note", "bank", f.Name, "worth", MarketText.Money((long)worth), "why", ExtrasText.Why(why));
            Logger?.Info(TestBankName + ": " + f.Name + ", " + quarters + " complete quarter(s) of its own books (" + profitable + " in profit), capital " + (long)capital + ", worth "
                         + (long)worth + " (never over " + ExtrasCatalogue.BankBookMost.ToString("0.#", Inv) + " times its capital), stage " + s.Extras.Bank.Stage + ", " + why + ": "
                         + (own ? "PASS" : "FAIL (worth over its own capital's most)"));
        }
    }
}
