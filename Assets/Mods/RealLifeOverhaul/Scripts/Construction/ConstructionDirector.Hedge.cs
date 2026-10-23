using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for the stock market: a project's materials exposure (ExposureRules) and its hedge on the raw
    /// materials exchange, bought through ConstructionBridges.Hedge as ordinary futures; each day's look at what those futures
    /// have made, and the project's end saying what the hedge saved or cost; the rival builders' news for the market. The
    /// test entries hedge a test project, bring its end forward and put both back. Asked on a page, a press or the day's
    /// pass; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<MaterialNeed> _needs = new List<MaterialNeed>(24);
        private readonly List<int> _opened = new List<int>(8);
        private Func<int, HedgeLook> _look;

        // ------------------------------------------------------------------ the asks

        /// <summary>A rival builder's news for the stock market: its number (-1 the player), the kind's number, a weight.</summary>
        internal static void RivalNews(int rival, int kind, int weight)
        {
            try
            {
                if (!Down("RivalNews"))
                {
                    ConstructionBridges.RivalNews?.Invoke(rival, kind, weight);
                }
            }
            catch (Exception e)
            {
                Faulty("RivalNews", e);
            }
        }

        private static bool PlanHedge(List<MaterialNeed> needs, List<HedgeLine> lines, out HedgeQuote quote)
        {
            quote = new HedgeQuote { WhyKey = "reallife_bld_hedge_no_exchange" };
            lines.Clear();
            try
            {
                return !Down("HedgePlan") && ConstructionBridges.HedgePlan != null && ConstructionBridges.HedgePlan(needs, lines, out quote);
            }
            catch (Exception e)
            {
                Faulty("HedgePlan", e);
                return false;
            }
        }

        private static int BuyHedge(List<MaterialNeed> needs, bool test, List<int> opened, out string whyKey)
        {
            whyKey = "reallife_bld_hedge_no_exchange";
            try
            {
                return !Down("Hedge") && ConstructionBridges.Hedge != null ? ConstructionBridges.Hedge(needs, test, opened, out whyKey) : 0;
            }
            catch (Exception e)
            {
                Faulty("Hedge", e);
                return 0;
            }
        }

        // A future as the exchange's book has it; false while the exchange is not running (nothing is folded then).
        private static bool ContractNow(int id, out HedgeLook look)
        {
            look = default(HedgeLook);
            try
            {
                return !Down("Contract") && ConstructionBridges.Contract != null && ConstructionBridges.Contract(id, out look);
            }
            catch (Exception e)
            {
                Faulty("Contract", e);
                return false;
            }
        }

        // ------------------------------------------------------------------ a project's exposure and hedge

        /// <summary>The materials a project will still buy, at today's prices; the list is the director's own, read before the next call.</summary>
        public List<MaterialNeed> Exposure(ProjectFile p)
        {
            Exchange(_exchange);
            ExposureRules.Of(p, ConstructionGame.Day, ConstructionGame.Prices, _exchange, p != null ? MaterialsFactor(p) : 1000, _needs);
            return _needs;
        }

        /// <summary>True while a project's hedge has futures still open: no new hedge until they have ended.</summary>
        public static bool HedgeOpen(ProjectFile p)
        {
            return p != null && p.Hedges.Count > 0;
        }

        /// <summary>What a hedge of a project's purchases to come would be now, its futures into <paramref name="lines"/>.</summary>
        public bool HedgeQuoteOf(ProjectFile p, List<HedgeLine> lines, out HedgeQuote quote)
        {
            lines.Clear();
            if (!Settings.MarketsBuilders)
            {
                quote = new HedgeQuote { WhyKey = "reallife_bld_hedge_off" };
                return false;
            }
            List<MaterialNeed> needs = Exposure(p);
            if (needs.Count == 0)
            {
                quote = new HedgeQuote { WhyKey = "reallife_bld_hedge_none" };
                return false;
            }
            return PlanHedge(needs, lines, out quote);
        }

        /// <summary>The Hedge press: the project's purchases to come hedged with futures bought now. Returns a text key.</summary>
        public string Hedge(ProjectFile p)
        {
            return HedgeNow(p, false, out _);
        }

        private string HedgeNow(ProjectFile p, bool test, out int bought)
        {
            bought = 0;
            if (p == null || (p.Stage != ProjectStage.Ready && p.Stage != ProjectStage.Building))
            {
                return "reallife_bld_hedge_none";
            }
            if (!Settings.MarketsBuilders)
            {
                return "reallife_bld_hedge_off";
            }
            if (HedgeOpen(p))
            {
                return "reallife_bld_hedge_already";
            }
            List<MaterialNeed> needs = Exposure(p);
            if (needs.Count == 0)
            {
                return "reallife_bld_hedge_none";
            }
            _opened.Clear();
            bought = BuyHedge(needs, test, _opened, out string why);
            for (int i = 0; i < _opened.Count && p.Hedges.Count < ExposureRules.HedgesMost; i++)
            {
                p.Hedges.Add(new HedgeHold { Contract = _opened[i] });
            }
            if (bought > 0)
            {
                p.HedgeDay = ConstructionGame.Day;
                Logger?.Info("Construction: project " + p.Id + " (" + ProjectName(p) + ") hedged with " + bought + " future(s) on the exchange" + (why != null ? "; the rest not: " + why : string.Empty) + ".");
                Changed();
            }
            return bought > 0 ? (why != null ? "reallife_bld_hedge_bought_part" : "reallife_bld_hedge_bought") : why ?? "reallife_bld_hedge_none";
        }

        /// <summary>What each of a project's futures has made, looked at now; an ended one folded into what the hedge made.</summary>
        public void LookAtHedge(ProjectFile p)
        {
            if (p == null || p.Hedges.Count == 0)
            {
                return;
            }
            if (_look == null)
            {
                _look = id =>
                {
                    ContractNow(id, out HedgeLook look);
                    return look;
                };
            }
            if (!ExchangeAnswers())
            {
                return;   // the market is off or not running: what it has made is kept as last seen
            }
            ExposureRules.Fold(p, _look);
        }

        // Whether the exchange answers at all: a look at a contract that cannot exist says.
        private static bool ExchangeAnswers()
        {
            return ContractNow(0, out _);
        }

        // The day's pass: every project's hedge looked at, so a future that ended keeps what it made in the save.
        private void HedgesDay()
        {
            if (!ExchangeAnswers())
            {
                return;
            }
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                LookAtHedge(_state.Projects[i]);
            }
        }

        // The project's end: what its hedge saved or cost, said in the log and, for one with a hedge, in a notice.
        private void HedgeEnd(ProjectFile p)
        {
            if (p.HedgeDay <= 0)
            {
                return;
            }
            LookAtHedge(p);
            long saved = HedgeSaved(p);
            Logger?.Info("Construction: project " + p.Id + "'s hedge " + (saved >= 0 ? "saved " : "cost ") + Math.Abs(saved) + (p.Hedges.Count > 0 ? " so far, " + p.Hedges.Count
                         + " future(s) still to settle" : string.Empty) + ".");
            int id = p.Id;
            var data = new Dictionary<string, string>(2) { { "name", ProjectName(p) }, { "amount", ConstructionText.Money(Math.Abs(saved)) } };
            ConstructionGame.Tell(saved >= 0 ? NotificationType.Success : NotificationType.Info, saved >= 0 ? "reallife_bld_notice_hedge_saved" : "reallife_bld_notice_hedge_cost", data,
                () => OpenHedge(id));
        }

        /// <summary>What a project's hedge saved (over nothing) or cost, in money: its futures' results with their commission.</summary>
        public static long HedgeSaved(ProjectFile p)
        {
            long cents = p != null ? p.HedgeCents : 0L;
            return cents >= 0 ? cents / 100 : -((-cents + 99) / 100);
        }

        /// <summary>Opens the app on a project's materials and hedge page.</summary>
        public void OpenHedge(int project)
        {
            if (_app != null && _view != null && Running)
            {
                _view.WantHedge(project);
                _app.Open();
            }
        }

        // ------------------------------------------------------------------ the test entries

        /// <summary>Test: the test project (made now if there is none) hedged on the exchange at once, whatever the hour.</summary>
        public static void TestHedge()
        {
            Instance?.HedgeTest();
        }

        /// <summary>Test: the test project's hedge settles now at today's prices and the project ends, its page saying what the hedge saved or cost.</summary>
        public static void TestHedgeEnd()
        {
            Instance?.HedgeTestEnd();
        }

        /// <summary>Test: the hedge tests put back: their futures undone with their money, the test project gone.</summary>
        public static void TestHedgeBack()
        {
            Instance?.HedgeTestBack();
        }

        private bool HedgeTestReady(string test)
        {
            if (!TestReady(test, false))
            {
                return false;
            }
            if (!Settings.MarketsBuilders)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (GoingPublic.InSession)
            {
                TestRefusals.TellKey(Logger, test, "reallife_mkt_drv_test_session");
                return false;
            }
            if (!ExchangeAnswers())
            {
                TestRefusals.TellKey(Logger, test, "reallife_bld_hedge_no_exchange");
                return false;
            }
            return true;
        }

        // The test project: a block of homes ready to build, on a test's lot, the test tools' own.
        private ProjectFile TestProject(bool make)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                if (_state.Projects[i].HedgeTest)
                {
                    return _state.Projects[i];
                }
            }
            if (!make || _state.Projects.Count >= BuildCatalogue.ProjectsMost)
            {
                return null;
            }
            int day = ConstructionGame.Day;
            int id = _state.NewId();
            var p = new ProjectFile
            {
                Id = id, Kind = ProjectKind.Plot, Stage = ProjectStage.Ready, Created = day, Lot = TestLotPrefix + "hedge" + id.ToString(CultureInfo.InvariantCulture),
                Use = BuildUse.Homes, Area = TestArea, Storeys = TestStoreys, Width = 30, Depth = 40, PlansDone = true, Quality = 70, Engineered = true,
                Planning = PlanningState.Granted, HedgeTest = true
            };
            p.Hours = PlansRules.Hours(p.Use, p.Area, p.Storeys, p.Kind);
            Exchange(_exchange);
            p.Estimate = PlansRules.Estimate(p.Kind, p.Use, p.Area, p.Storeys, false, 2, ConstructionGame.Prices, _exchange).Total;
            _state.Projects.Add(p);
            return p;
        }

        private const int TestArea = 2400, TestStoreys = 6;   // m2 and storeys: a block whose frame and walls fall within the four weeks on offer

        private void HedgeTest()
        {
            const string test = "Hedging check (hedge the test project)";
            if (!HedgeTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ProjectFile p = TestProject(false);
                if (p != null && !HedgeOpen(p) && (p.Stage != ProjectStage.Ready || p.HedgeDay > 0))
                {
                    _state.Projects.Remove(p);   // the last test's project, ended: a new one to hedge (its futures stay the test tools' until put back)
                    p = null;
                }
                p = p ?? TestProject(true);
                if (p == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_bld_hedge_test_full");
                    return;
                }
                if (HedgeOpen(p))
                {
                    TestRefusals.TellKey(Logger, test, "reallife_bld_hedge_already");
                    return;
                }
                List<MaterialNeed> needs = Exposure(p);
                long money = 0;
                for (int i = 0; i < needs.Count; i++)
                {
                    money += needs[i].Money;
                }
                int count = needs.Count;
                string key = HedgeNow(p, true, out int bought);
                Logger?.Info(test + ": project " + p.Id + " (" + TestArea + " m2, " + TestStoreys + " storeys) will buy " + count + " lot(s) of materials for " + money + "; " + bought
                             + " future(s) bought by the test tools (" + key + "): " + (bought > 0 && p.Hedges.Count == bought ? "PASS" : "FAIL"));
                OpenHedge(p.Id);
            });
        }

        private void HedgeTestEnd()
        {
            const string test = "Hedging check (the test project's end)";
            if (!HedgeTestReady(test))
            {
                return;
            }
            TestGuard(test, () =>
            {
                ProjectFile p = TestProject(false);
                if (p == null || p.HedgeDay <= 0)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_bld_hedge_test_first");
                    return;
                }
                var ids = new List<int>(p.Hedges.Count);
                for (int i = 0; i < p.Hedges.Count; i++)
                {
                    ids.Add(p.Hedges[i].Contract);
                }
                int settled = 0;
                try
                {
                    settled = !Down("SettleTest") && ConstructionBridges.SettleTest != null ? ConstructionBridges.SettleTest(ids) : 0;
                }
                catch (Exception e)
                {
                    Faulty("SettleTest", e);
                }
                LookAtHedge(p);
                int day = ConstructionGame.Day;
                p.Stage = ProjectStage.Finished;
                p.Handed = day;
                p.Opening = day;
                p.Opened = true;   // a test's project has no opening of its own
                HedgeEnd(p);
                long saved = HedgeSaved(p);
                Logger?.Info(test + ": " + settled + " of " + ids.Count + " future(s) settled at today's prices; the hedge " + (saved >= 0 ? "saved " : "cost ") + Math.Abs(saved)
                             + ", " + p.Hedges.Count + " still open: " + (settled == ids.Count && p.Hedges.Count == 0 ? "PASS" : "FAIL"));
                Changed();
                OpenHedge(p.Id);
            });
        }

        private void HedgeTestBack()
        {
            const string test = "Hedging check (put the hedge tests back)";
            if (!TestReady(test, false))
            {
                return;
            }
            TestGuard(test, () =>
            {
                int undone = 0;
                try
                {
                    undone = !Down("UndoTests") && ConstructionBridges.UndoTests != null ? ConstructionBridges.UndoTests() : 0;
                }
                catch (Exception e)
                {
                    Faulty("UndoTests", e);
                }
                int gone = 0;
                for (int i = _state.Projects.Count - 1; i >= 0; i--)
                {
                    if (_state.Projects[i].HedgeTest)
                    {
                        _state.Projects.RemoveAt(i);
                        gone++;
                    }
                }
                if (undone == 0 && gone == 0)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_bld_hedge_test_nothing");
                    return;
                }
                Logger?.Info(test + ": " + undone + " test future(s) undone with their money, " + gone + " test project(s) gone: PASS");
                TestDone("the hedge tests put back", TabProjects, 0);
            });
        }
    }
}
