using System.Collections.Generic;
using UI.Notification;

namespace RealLifeOverhaul.Loyalty
{
    /// <summary>
    /// Options, Tests: what the player would otherwise wait days for. No test moves money. Each says what happened in a notice
    /// and writes "Loyalty check" lines.
    /// </summary>
    public sealed partial class LoyaltyDirector
    {
        private bool TestReady()
        {
            if (!Running)
            {
                LoyaltyGame.Tell(NotificationType.Warning, "reallife_loyalty_test_off", null);
                return false;
            }
            Map();
            return true;
        }

        // The first scheme that runs or starts tomorrow, with its business.
        private bool TestFirst(out BuildingRegistration reg, out LoyaltyFile f)
        {
            foreach (KeyValuePair<BuildingRegistration, LoyaltyFile> pair in _byReg)
            {
                if (pair.Value.On || pair.Value.Want)
                {
                    reg = pair.Key;
                    f = pair.Value;
                    return true;
                }
            }
            reg = null;
            f = null;
            LoyaltyGame.Tell(NotificationType.Warning, "reallife_loyalty_test_none", null);
            Logger?.Info("Loyalty check (test): no scheme runs at a business of the player's; switch one on from the Loyalty tab in BizMan first.");
            return false;
        }

        /// <summary>Options, Tests: every scheme's figures written to the log, each checked against its bounds.</summary>
        public void TestReport()
        {
            if (!TestReady())
            {
                return;
            }
            int today = LoyaltyGame.Day, good = 0;
            foreach (KeyValuePair<BuildingRegistration, LoyaltyFile> pair in _byReg)
            {
                LoyaltyFile f = pair.Value;
                LoyaltyDay week = LoyaltyRules.Week(f, today);
                bool right = f.Lift >= 0 && f.Lift <= LoyaltyRules.LiftMost && f.Members <= LoyaltyRules.Cap(f.Base, f.Sisters) && FootfallOf(pair.Key) == (f.On ? f.Lift : 0);
                good += right ? 1 : 0;
                Logger?.Info("Loyalty check (test, report): " + LoyaltyGame.Name(pair.Key) + (f.On ? " runs" : f.Want ? " starts tomorrow" : " is off") + ", rate step " + f.Rate
                             + ", push " + f.Push + ", members' day " + f.OfferDay + "; " + f.Members / 100 + " member(s) of a base of " + LoyaltyRules.BaseCustomers(f.Base) / 100
                             + ", lift " + f.Lift + "% (carried " + f.Carried + "%), basket " + f.Basket + " per mille; the week: " + week.Visits + " visit(s), rewards "
                             + week.Rewards / 100 + ", costs " + week.Costs / 100 + ", extra takings " + week.Extra / 100 + ", margin on them " + week.Margin / 100 + "; rivals "
                             + f.RivalsRunning + " of " + f.Rivals + " (strength " + f.Rivalry + "); margin " + f.MarginMille + " per mille against a break even of "
                             + LoyaltyRules.BreakEven(f.NextRate, f.NextOffer >= 0, f.Rivalry) + ": " + (right ? "PASS" : "FAIL"));
            }
            LoyaltyGame.Tell(NotificationType.Info, "reallife_loyalty_test_report", LoyaltyGame.Data("count", LoyaltyText.N(_byReg.Count), "good", LoyaltyText.N(good)));
        }

        /// <summary>
        /// Options, Tests: the first running scheme lives seven more days of yesterday's customers at once (no money moves and
        /// nothing is booked), and today's lift is written from the members it then has.
        /// </summary>
        public void TestWeek()
        {
            if (!TestReady() || !TestFirst(out BuildingRegistration reg, out LoyaltyFile f))
            {
                return;
            }
            int today = LoyaltyGame.Day, before = f.Members;
            LoyaltyInput d = LoyaltyGame.Yesterday(reg, today - 1);
            if (f.Want && !f.On)
            {
                f.On = true;   // the cards are out a night early, for the test
                f.Since = today;
            }
            ReadRivals(reg, f, today);
            for (int i = 0; i < LoyaltyRules.WeekDays; i++)
            {
                LoyaltyRules.Step(ref f.Members, ref f.Base, d.Served, f.Rate, f.Push, f.OfferDay, (d.Weekday + i) % LoyaltyRules.WeekDays, d.Satisfaction, d.Pricing, d.Clean,
                    0, 0, f.Rivalry, f.Sisters, out int _, out int _, out int _);
            }
            LoyaltyRules.Today(f, today);
            WriteFigures(false);
            _revision++;
            bool right = f.Members >= 0 && f.Members <= LoyaltyRules.Cap(f.Base, f.Sisters) && f.Lift >= 0 && f.Lift <= LoyaltyRules.LiftMost;
            LoyaltyGame.Tell(NotificationType.Info, "reallife_loyalty_test_week",
                LoyaltyGame.Data("business", LoyaltyGame.Name(reg), "before", LoyaltyText.Whole(before), "after", LoyaltyText.Whole(f.Members), "percent", LoyaltyText.N(f.Lift)));
            Logger?.Info("Loyalty check (test, seven days at once at " + LoyaltyGame.Name(reg) + "): " + d.Served + " customer(s) a day at satisfaction " + d.Satisfaction + ", members "
                         + before / 100 + " -> " + f.Members / 100 + " of at most " + LoyaltyRules.Cap(f.Base, f.Sisters) / 100 + ", today's lift " + f.Lift + "%: " + (right ? "PASS" : "FAIL"));
        }

        /// <summary>Options, Tests: the other owners' shops of each scheme's kind in its district read now, and which of them run a card.</summary>
        public void TestRivals()
        {
            if (!TestReady())
            {
                return;
            }
            int today = LoyaltyGame.Day, shops = 0, running = 0;
            foreach (KeyValuePair<BuildingRegistration, LoyaltyFile> pair in _byReg)
            {
                LoyaltyFile f = pair.Value;
                ReadRivals(pair.Key, f, today);
                shops += f.Rivals;
                running += f.RivalsRunning;
                bool right = f.RivalsRunning <= f.Rivals && f.Rivalry >= 0 && f.Rivalry <= LoyaltyRules.RivalryMost && LoyaltyRules.RivalryKept(f.Rivalry) >= LoyaltyRules.RivalFloor;
                Logger?.Info("Loyalty check (test, rivals of " + LoyaltyGame.Name(pair.Key) + "): " + f.Rivals + " shop(s) of its kind in its district are another owner's, "
                             + f.RivalsRunning + " run a card (strength " + f.Rivalry + " per mille), leaving " + LoyaltyRules.RivalryKept(f.Rivalry) / 10 + "% of this scheme's lift: "
                             + (right ? "PASS" : "FAIL"));
            }
            _revision++;
            if (_byReg.Count == 0)
            {
                LoyaltyGame.Tell(NotificationType.Warning, "reallife_loyalty_test_none", null);
                return;
            }
            LoyaltyGame.Tell(NotificationType.Info, "reallife_loyalty_test_rivals", LoyaltyGame.Data("count", LoyaltyText.N(shops), "running", LoyaltyText.N(running)));
        }
    }
}
