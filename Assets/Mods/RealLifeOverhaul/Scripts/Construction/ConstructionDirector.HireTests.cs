using System;
using System.Globalization;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The tests of a builder hired by the player (Options > Tests, and the city expansion's test run). Each works from anywhere
    /// at any time, with or without a company: it takes the player's building ready to start (made for the test on a lot of theirs
    /// when there is none), and writes one "Construction check (...)" line ending PASS or FAIL. A test's contract moves no money.
    /// The construction game's put back undoes them. Each is a public static method, so a runner can call it with no press.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const string HireCheck = "Construction check (";   // each line goes on with its test's name and ")"
        private const int HireTestLate = 4;                         // days late the late builder's test hands over

        /// <summary>Test: a building of the player's commissioned to the city's cheapest builder now, every stage moved on, handed over.</summary>
        public static bool TestHireNow()
        {
            return HireTest("a client project built by a rival now", d => d.HireThrough("a client project built by a rival now", 0));
        }

        /// <summary>Test: a late builder: the player's building handed over some days past its day, the builder paying its penalty.</summary>
        public static bool TestHireLateNow()
        {
            return HireTest("a late builder pays its penalty", d => d.HireThrough("a late builder pays its penalty", HireTestLate));
        }

        /// <summary>Test: the city's builders' quotes for the player's building, written out and shown in the app.</summary>
        public static bool TestHireQuotesNow()
        {
            return HireTest("a builder's quotes shown", d => d.HireQuotes("a builder's quotes shown"));
        }

        /// <summary>
        /// Test: a builder hired for the player's building now and its site worked hour by hour to the keys through the director's
        /// own working hour, the programme's hours run on ahead of the clock as a sleep's catching up runs them: every stage begun,
        /// each inspection booked by the builder, visited at the hour's own time and paid, nothing standing still, handed over.
        /// </summary>
        public static bool TestHireWorkedNow()
        {
            return HireTest("a hired builder's site worked to its keys", d => d.HireWorked("a hired builder's site worked to its keys"));
        }

        private static bool HireTest(string test, Func<ConstructionDirector, bool> work)
        {
            ConstructionDirector d = Instance;
            if (d == null || !ModState.Loaded || SaveGameManager.Current == null || !d._loaded)
            {
                TestRefusals.Tell(Logger, HireCheck + test + ")", TestRefusal.NotLoaded);
                Logger?.Warn(HireCheck + test + "): the construction game has not read this save yet: FAIL.");
                return false;
            }
            if (!d.Running)
            {
                TestRefusals.Tell(Logger, HireCheck + test + ")", TestRefusal.FeatureOff);
                Logger?.Warn(HireCheck + test + "): the construction game is off in the options: FAIL.");
                return false;
            }
            try
            {
                return work(d);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        // The player's building a builder can be hired for: one of theirs ready to start, else a block of offices made for the test.
        private ProjectFile HireTestProject(out bool made, out string why)
        {
            made = false;
            why = null;
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (HireKind(p) && HireBlock(p) == null)
                {
                    return p;
                }
            }
            ProjectFile plot = TestPlot(BuildUse.Offices, "building", out why);
            made = plot != null;
            if (made)
            {
                plot.After = AfterUse.Keep;   // the player's own from the next load, as a company's build is
                Logger?.Info("Construction: project " + plot.Id + " made for the hired builder's test on lot " + plot.Lot + ", estimated at " + plot.Estimate + ".");
            }
            return plot;
        }

        // Hired, every stage moved on and handed over now, so many days past its day.
        private bool HireThrough(string test, int late)
        {
            if (GoingPublic.IsGuest)
            {
                return Failed(HireCheck, test, "reallife_bld_test_host", "a Going Public guest starts no site of the city's programme");
            }
            ProjectFile p = HireTestProject(out bool made, out string why);
            if (p == null)
            {
                return Failed(HireCheck, test, "reallife_bld_test_no_building", why ?? "no building of the player's is ready to start");
            }
            BeforeTest();
            int count = Quotes(p, _quotes);
            int cheapest = CommissionRules.Cheapest(_quotes, count);
            string answer = HireBuilder(p, cheapest, true);
            if (!p.Hired || p.Stage != ProjectStage.Building)
            {
                return Failed(HireCheck, test, answer, "no builder could be hired for project " + p.Id + " (" + answer + ")");
            }
            int day = ConstructionGame.Day;
            int site = p.SiteId;
            long[] paid = new long[(int)Inspection.Count];
            for (int k = 0; k < (int)Inspection.Count; k++)
            {
                AdvanceSiteNow(site);   // the street's site moves on with it
                HiredStage(p, (WorkStage)Math.Min(k + 1, (int)WorkStage.Envelope));
                long before = p.HirePaid;
                Passed(p, k, true, day);
                paid[k] = p.HirePaid - before;
            }
            if (late > 0)
            {
                p.DueDay = day - late;   // its day gone by: the builder is late by so many days
            }
            bool done = FinishSite(site);
            if (!done && p.Stage == ProjectStage.Building)
            {
                Handover(p, day);   // the site was gone from the programme: handed over all the same
            }
            long price = p.ContractorPrice;
            long penalty = CommissionRules.Penalty(price, late);
            bool stages = paid[0] == CommissionRules.StagePay(price, 0) && paid[1] == CommissionRules.StagePay(price, 1) && paid[2] == CommissionRules.StagePay(price, 2);
            bool settled = p.HirePaid == price - penalty && p.HirePenalty == penalty && (late == 0 ? penalty == 0 : penalty > 0);
            bool handed = p.Stage == ProjectStage.Finished && p.Opening == day + 1 && (p.InDistrict || IsTestLot(p.Lot) || p.Kind == ProjectKind.Rebuild);
            bool pass = stages && settled && handed;
            Logger?.Warn(HireCheck + test + "): project " + p.Id + (made ? " (made for the test)" : string.Empty) + " built by "
                         + ConstructionText.T(BuildCatalogue.RivalKeys[p.Contractor]) + ", " + HireWords(p) + "; the deposit " + CommissionRules.Deposit(price) + ", the stages "
                         + paid[0].ToString(CultureInfo.InvariantCulture) + ", " + paid[1].ToString(CultureInfo.InvariantCulture) + " and " + paid[2].ToString(CultureInfo.InvariantCulture)
                         + ", the retention " + CommissionRules.Retention(price) + (late > 0 ? ", " + late + " day(s) late" : ", on time")
                         + "; handed over on day " + p.Handed + ", its opening on day "
                         + p.Opening + ", " + (p.InDistrict ? "the player's from the next load" : IsTestLot(p.Lot) ? "on a test's lot" : "not the district's yet") + (pass ? ": PASS." : ": FAIL."));
            TestDone(late > 0 ? "a late builder paid its penalty" : "a building of yours put up by a builder of the city's", TabProjects, p.Id);
            return pass;
        }

        // Hired, then its site worked from the next working hour as the programme works it (WorkOn, the hour's own day and hour),
        // the site's hours moved on by what each hour did, and handed over by the programme once its works are done.
        private bool HireWorked(string test)
        {
            if (GoingPublic.IsGuest)
            {
                return Failed(HireCheck, test, "reallife_bld_test_host", "a Going Public guest starts no site of the city's programme");
            }
            ProjectFile p = HireTestProject(out bool made, out string why);
            if (p == null)
            {
                return Failed(HireCheck, test, "reallife_bld_test_no_building", why ?? "no building of the player's is ready to start");
            }
            BeforeTest();
            int count = Quotes(p, _quotes);
            string answer = HireBuilder(p, CommissionRules.Cheapest(_quotes, count), true);
            if (!p.Hired || p.Stage != ProjectStage.Building)
            {
                return Failed(HireCheck, test, answer, "no builder could be hired for project " + p.Id + " (" + answer + ")");
            }
            int site = p.SiteId, permits = Permits(), worked = 0, seen = p.StageSeen;
            float speed = CivicCatalogue.SiteSpeed(permits);
            var stages = new System.Text.StringBuilder(64);
            long at = (long)ConstructionGame.Day * 24 + ConstructionGame.Hour;
            for (int guard = 0; guard < 24 * BuildCatalogue.ScheduleMostDays && p.Stage == ProjectStage.Building; guard++)
            {
                at++;
                int day = (int)(at / 24), hour = (int)(at % 24);
                SiteFacts facts = SiteOf(site);
                if (!facts.Known || !SiteRules.WorkingHour(day, hour, permits))
                {
                    continue;
                }
                float done = facts.Done + WorkOn(site, facts.Done, facts.Hours, speed, day, hour);
                worked++;
                if (p.StageSeen != seen)
                {
                    seen = p.StageSeen;
                    stages.Append(stages.Length > 0 ? ", " : string.Empty).Append((WorkStage)seen);
                }
                if (done >= facts.Hours)
                {
                    FinishSite(site);   // the programme hands a finished site over: the handover is the director's own
                    break;
                }
                SetSiteDone(site, done);
            }
            bool every = p.Insp[0] == InspectionState.Passed && p.Insp[1] == InspectionState.Passed && p.Insp[2] == InspectionState.Passed && p.HireStages == 7;
            bool pass = p.Stage == ProjectStage.Finished && every && seen == (int)WorkStage.Envelope && p.HirePaid == p.ContractorPrice - p.HirePenalty && p.WaitHours == 0;
            Logger?.Warn(HireCheck + test + "): project " + p.Id + (made ? " (made for the test)" : string.Empty) + " built by " + ConstructionText.T(BuildCatalogue.RivalKeys[p.Contractor])
                         + ", " + HireWords(p) + ": " + worked + " working hour(s) of its site's " + p.Hours + ", the stages " + stages + ", " + p.Fails + " inspection(s) failed and put right, "
                         + (every ? "every inspection passed and paid" : "not every inspection passed and paid") + ", "
                         + (p.Stage == ProjectStage.Finished ? "handed over on day " + p.Handed : "not handed over")
                         + " (due " + p.DueDay + ")" + (pass ? ": PASS." : ": FAIL."));
            TestDone("a building of yours worked to its keys by a builder of the city's", TabProjects, p.Id);
            return pass;
        }

        // Every builder's quote, twice: the same both times, eight of them, the cheapest, the fastest and the best named.
        private bool HireQuotes(string test)
        {
            ProjectFile p = HireTestProject(out bool made, out string why);
            if (p == null)
            {
                return Failed(HireCheck, test, "reallife_bld_test_no_building", why ?? "no building of the player's is ready to start");
            }
            int count = Quotes(p, _quotes);
            var again = new TenderBid[BuildCatalogue.RivalCount];   // a test's press: the second look is its own
            bool same = Quotes(p, again) == count;
            var text = new System.Text.StringBuilder(512);
            for (int i = 0; i < count; i++)
            {
                same &= again[i].Price == _quotes[i].Price && again[i].Days == _quotes[i].Days && again[i].Score == _quotes[i].Score;
                text.Append(i > 0 ? "; " : string.Empty).Append(ConstructionText.T(BuildCatalogue.RivalKeys[i])).Append(' ')
                    .Append(CommissionRules.Price(_quotes[i]).ToString(CultureInfo.InvariantCulture))
                    .Append(" in ").Append(_quotes[i].Days).Append(" day(s)");
            }
            int cheapest = CommissionRules.Cheapest(_quotes, count), fastest = CommissionRules.Fastest(_quotes, count), best = TenderRules.Winner(_quotes, count);
            bool pass = count == BuildCatalogue.RivalCount && same && cheapest >= 0 && fastest >= 0 && best >= 0 && _quotes[cheapest].Price > 0;
            Logger?.Warn(HireCheck + test + "): project " + p.Id + (made ? " (made for the test)" : string.Empty) + ", estimated at " + p.Estimate + ", " + count + " quote(s) standing until day "
                         + CommissionRules.StandUntil(ConstructionGame.Day) + ": " + text + "; the cheapest " + cheapest + ", the fastest " + fastest + ", the best " + best
                         + (same ? ", the same at a second look" : ", different at a second look") + (pass ? ": PASS." : ": FAIL."));
            _view?.WantQuotes(p.Id);   // the next opening of the app shows them
            TestDone("the city's builders' quotes for project " + p.Id, TabProjects, p.Id);
            return pass;
        }
    }
}
