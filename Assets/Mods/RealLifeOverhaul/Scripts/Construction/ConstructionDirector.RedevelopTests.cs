using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Redevelopment's test entries (Options > Tests), one for each part of it, the player's side and the city's. Each works from
    /// anywhere at any time: it brings forward what it needs, writes one line, "Redevelopment check (...)", ending PASS or FAIL,
    /// and says so in a notice (a failure's notice says why). The state before the first test is kept for the construction
    /// game's own "put back". Each test's work is a public static method, so a test runner can call it with no press of its own.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const string RedoCheck = "Redevelopment check (";   // each line goes on with its test's name and ")"

        private readonly List<UnitView> _testUnits = new List<UnitView>(8);

        // The director when a redevelopment test may run; otherwise the refusal said and a FAIL line written.
        private static ConstructionDirector RedoDirector(string test, bool refits)
        {
            ConstructionDirector d = Instance;
            if (d == null || !ModState.Loaded || SaveGameManager.Current == null || !d._loaded)
            {
                TestRefusals.Tell(Logger, RedoCheck + test + ")", TestRefusal.NotLoaded);
                Logger?.Warn(RedoCheck + test + "): the construction game has not read this save yet: FAIL.");
                return null;
            }
            if (!d.Running || (refits && !Settings.ConstructionRenovations))
            {
                TestRefusals.Tell(Logger, RedoCheck + test + ")", TestRefusal.FeatureOff);
                Logger?.Warn(RedoCheck + test + "): " + (refits ? "renovations are" : "the construction game is") + " off in the options: FAIL.");
                return null;
            }
            return d;
        }

        // A test passed: its line, and a notice saying so that opens the app on what it made.
        private void Passed(string test, string labelKey, string line, int tab, int pick)
        {
            Logger?.Warn(RedoCheck + test + "): " + line + ": PASS.");
            Changed();
            Notifications.Show(NotificationType.Success, "reallife_bld_test_passed", new Dictionary<string, string>(1) { { "test", ConstructionText.T(labelKey) } }, 8f, null,
                Guard.Wrap(() => Open(tab, pick)), true, false);
        }

        // ------------------------------------------------------------------ refurnishing

        /// <summary>
        /// Test: the player's nearest unit (a business, a home or an empty unit) refitted now by the city's best contractor for
        /// nothing, furnished with the first of the game's designs for it through the game's own installation contract (its price
        /// given back first, so the test costs nothing), and handed over at once.
        /// </summary>
        public static bool TestRefurnishNow()
        {
            const string test = "refurnish your nearest unit with one of the game's designs now";
            ConstructionDirector d = RedoDirector(test, true);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.RefurnishNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool RefurnishNow(string test)
        {
            if (!NearestUnit(out UnitView u, out float metres))
            {
                return Failed(RedoCheck, test, "reallife_bld_test_no_unit", "the player rents or owns no unit with a door on the street");
            }
            ProjectFile live = _state.AtBuilding(u.Street, u.Number);
            if (live != null)
            {
                return Failed(RedoCheck, test, "reallife_bld_answer_building_busy", u.Name + " has works under way already (project " + live.Id + ")");
            }
            DesignsFor(u, _designs);
            string design = _designs.Count > 0 ? _designs[0] : null;
            long price = 0L;
            if (design != null)
            {
                FurnishCost(u, design, out price, out _, out _, out _);
            }
            BeforeTest();
            int id = ContractorRefit(u.Street, u.Number, true, design, out string why);
            ProjectFile p = _state.Project(id);
            if (p == null)
            {
                return Failed(RedoCheck, test, why ?? "reallife_bld_answer_site_refused", "no refit could start at " + u.Name + " (" + why + ")");
            }
            if (price > 0L)
            {
                ConstructionGame.Give(price, "reallife_transaction_bld_test_furnish", u.Name);   // the game's installers take it at the handover: the test costs nothing
            }
            bool handed = p.SiteId > 0 && FinishSite(p.SiteId);
            if (!handed && p.Stage == ProjectStage.Building)
            {
                Handover(p, ConstructionGame.Day);   // the site was gone from the programme: handed over all the same
                handed = true;
            }
            bool waiting = design != null && ConstructionGame.InstallationWaiting(u.Street, u.Number);
            ConstructionGame.PiecesOf(u, design, out int inUnit, out int inDesign);
            bool laid = design == null || waiting || (inDesign >= 0 && inUnit == inDesign);
            string where = u.Name + ", " + metres.ToString("0", CultureInfo.InvariantCulture) + " m away (" + (u.Trading ? "a business" : "a home or an empty unit") + ")";
            string what = design == null ? "the installers have no design for its kind and size: its finishes renewed, its furniture kept"
                : waiting ? "the game's own installers lay the design " + design + " once the player is out of it (their contract waits for the next day)"
                : "furnished as the game's design " + design + ": " + inUnit + " piece(s) in it, the design's " + inDesign;
            if (!(handed && p.Stage == ProjectStage.Finished && laid))
            {
                Logger?.Warn(RedoCheck + test + "): " + where + ": project " + p.Id + " at " + p.Stage + "; " + what + ": FAIL.");
                return Failed(RedoCheck, test, "reallife_bld_test_failed_log", "the refit or its furnishing did not finish");
            }
            Passed(test, "reallife_bld_test_refurnish", where + ", refitted now by contractor " + p.Contractor + " (grade " + p.Grade + "); " + what, TabProjects, p.Id);
            return true;
        }

        // The player's unit nearest to them, or false.
        private bool NearestUnit(out UnitView nearest, out float metres)
        {
            nearest = default(UnitView);
            metres = 0f;
            RefitUnits(_testUnits);
            Vector3 player = ConstructionGame.Player;
            float best = float.MaxValue;
            for (int i = 0; i < _testUnits.Count; i++)
            {
                Vector3 d = _testUnits[i].Door - player;
                d.y = 0f;
                if (d.sqrMagnitude < best)
                {
                    best = d.sqrMagnitude;
                    nearest = _testUnits[i];
                }
            }
            _testUnits.Clear();
            metres = best < float.MaxValue ? Mathf.Sqrt(best) : 0f;
            return best < float.MaxValue;
        }

        /// <summary>
        /// Test: a business of the city's (one the game has another layout of its kind for at its size) puts its refit out to tender
        /// now, a rival wins it with no bid of the player's, the hoarding goes up and its works are handed over at once: the
        /// business wears another of the game's layouts. The city's side of refurnishing; never in a Going Public session.
        /// </summary>
        public static bool TestCityRefurnishNow()
        {
            const string test = "a business of the city's refitted by a rival and furnished anew now";
            ConstructionDirector d = RedoDirector(test, true);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.CityRefurnishNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool CityRefurnishNow(string test)
        {
            if (GoingPublic.InSession)
            {
                return Failed(RedoCheck, test, "reallife_bld_test_session_city", "in a Going Public session the city's businesses keep their interiors on every machine");
            }
            int day = ConstructionGame.Day;
            BuildRandom random = BuildRandom.Of(_state.Seed, day, 919);
            BuildingView b = default(BuildingView);
            bool found = false;
            for (int k = 0; k < 32 && !found; k++)
            {
                if (ConstructionGame.Client(random.Range(0, 100000), out BuildingView c) && ConstructionGame.AiLayouts(c.Street, c.Number, _layouts, out int current)
                    && RefitRules.AiLayout(0, 0, _layouts.Count, current) >= 0)
                {
                    b = c;
                    found = true;
                }
            }
            _layouts.Clear();
            if (!found)
            {
                return Failed(RedoCheck, test, "reallife_bld_test_no_city_business", "no business of the city's that the game has another layout of its kind for was found");
            }
            string before = ConstructionGame.LayoutOf(b.Street, b.Number);
            BeforeTest();
            int now = day * 24 + ConstructionGame.Hour;
            var t = new TenderFile
            {
                Id = _state.NewId(), Source = TenderSource.Refit, CivicKind = RefitRules.Works, Street = b.Street, Number = b.Number,
                Use = BuildCatalogue.UseOf(b.Type) >= 0 ? (BuildUse)BuildCatalogue.UseOf(b.Type) : BuildUse.Shops, Area = Math.Max(1, b.TotalSqm), Opened = now, Closes = now
            };
            t.Hours = RefitRules.Hours(t.Area);
            t.Value = RefitRules.Value(t.Hours, ConstructionGame.Prices);
            AddTender(t);
            Award(t, day);   // the player's company has no bid in it: one of the city's builders wins
            if (t.Winner < 0 || t.SiteId <= 0 || !FinishSite(t.SiteId))
            {
                return Failed(RedoCheck, test, "reallife_bld_test_failed_log", "tender " + t.Id + " went to " + t.Winner + " and its site " + t.SiteId + " could not be handed over");
            }
            string after = ConstructionGame.LayoutOf(b.Street, b.Number);
            if (string.IsNullOrEmpty(after) || after == before)
            {
                Logger?.Warn(RedoCheck + test + "): " + b.Name + " kept its layout " + before + " after tender " + t.Id + ": FAIL.");
                return Failed(RedoCheck, test, "reallife_bld_test_failed_log", "the business kept its layout");
            }
            Passed(test, "reallife_bld_test_city_refurnish", b.Name + ", tender " + t.Id + " won by " + ConstructionText.T(BuildCatalogue.RivalKeys[t.Winner]) + " and handed over now: its layout "
                   + before + " -> " + after, TabTenders, t.Id);
            return true;
        }

        // ------------------------------------------------------------------ demolition

        private readonly List<BuildingView> _testOwned = new List<BuildingView>(8);

        /// <summary>
        /// Test: the player's nearest building in Uptown knocked down now, for good: a demolition made, its permit granted and its
        /// notice run at once (its tenants and business gone, nothing paid), the city's demolition contractor's works for nothing,
        /// handed over at once; its lots cleared and its ground the player's land.
        /// </summary>
        public static bool TestDemolitionNow()
        {
            const string test = "knock down your nearest Uptown building now, for good";
            ConstructionDirector d = RedoDirector(test, false);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.DemolitionNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool DemolitionNow(string test)
        {
            if (GoingPublic.InSession)
            {
                return Failed(RedoCheck, test, "reallife_bld_test_session_city", "in a Going Public session the district's buildings stay as they are on every machine");
            }
            if (!Settings.ConstructionRedevelop)
            {
                return Failed(RedoCheck, test, "reallife_bld_answer_redevelop_off", "redevelopment is off in the options");
            }
            if (!NearestUptownBuilding(out BuildingView b, out float metres))
            {
                return Failed(RedoCheck, test, "reallife_bld_test_no_uptown", "the player owns no building of Uptown's that redevelopment may take (buy one in BizMan first)");
            }
            string block = DemolishBlock(b.Street, b.Number);
            if (block != null)
            {
                return Failed(RedoCheck, test, block, b.Name + " cannot be knocked down now (" + block + ")");
            }
            BeforeTest();
            int id = NewDemolition(b.Street, b.Number, out string why);
            ProjectFile p = _state.Project(id);
            if (p == null)
            {
                return Failed(RedoCheck, test, why ?? "reallife_bld_test_failed_log", "no demolition could be made at " + b.Name + " (" + why + ")");
            }
            StandingOnLot(p.Lot, out StandingView before);
            int lots = 1 + (before.Over != null ? before.Over.Length : 0);
            p.Planning = PlanningState.Granted;   // the permit at once, as City Hall would grant it
            p.Stage = ProjectStage.Ready;
            p.NoticeUntil = ConstructionGame.Day;
            ConstructionGame.Evict(p.Street, p.Number);   // the notice run at once: the business closed and the tenants gone, nothing paid for a test
            p.BuyOut = -1L;
            string ordered = ContractorVacant(p, true);
            bool handed = p.SiteId > 0 && FinishSite(p.SiteId);
            if (!handed && p.Stage == ProjectStage.Building)
            {
                Handover(p, ConstructionGame.Day);   // the site was gone from the programme: handed over all the same
                handed = true;
            }
            bool cleared = AskCleared(p.Lot);
            LotFile land = _state.Lot(p.Lot);
            string where = b.Name + ", " + metres.ToString("0", CultureInfo.InvariantCulture) + " m away, " + before.Sqm + " m2 on " + lots + " lot(s)";
            if (!(handed && p.InDistrict && cleared && land != null && land.Spaces == Math.Min(lots, BuildCatalogue.SiteSpacesMost)))
            {
                Logger?.Warn(RedoCheck + test + "): " + where + ": project " + p.Id + " at " + p.Stage + " (" + ordered + "), down in the district " + p.InDistrict + ", lot cleared " + cleared
                             + ", the player's land " + (land != null ? land.Spaces + " lot(s)" : "none") + ": FAIL.");
                return Failed(RedoCheck, test, "reallife_bld_test_failed_log", "the building did not come down");
            }
            Passed(test, "reallife_bld_test_demolish", where + ": permit granted, notice run, knocked down now by contractor " + p.Contractor + "; its lot(s) cleared and the player's land, a site of "
                   + land.Spaces + " lot(s) worth " + land.Paid + " as land", TabLand, 0);
            return true;
        }

        // The player's building of Uptown's nearest to them that redevelopment may take, or false.
        private bool NearestUptownBuilding(out BuildingView nearest, out float metres)
        {
            nearest = default(BuildingView);
            metres = 0f;
            ConstructionGame.Owned(_testOwned);
            Vector3 player = ConstructionGame.Player;
            float best = float.MaxValue;
            for (int i = 0; i < _testOwned.Count; i++)
            {
                BuildingView b = _testOwned[i];
                if (!Standing(b.Street, b.Number, out StandingView s) || s.Kept || !ConstructionGame.Door(b.Street, b.Number, out Vector3 door, out _))
                {
                    continue;
                }
                Vector3 d = door - player;
                d.y = 0f;
                if (d.sqrMagnitude < best)
                {
                    best = d.sqrMagnitude;
                    nearest = b;
                }
            }
            _testOwned.Clear();
            metres = best < float.MaxValue ? Mathf.Sqrt(best) : 0f;
            return best < float.MaxValue;
        }

        // Whether the district has a lot cleared, as the bridge says.
        private static bool AskCleared(string lot)
        {
            try
            {
                return !Down("Cleared") && ConstructionBridges.Cleared != null && ConstructionBridges.Cleared(lot);
            }
            catch (Exception e)
            {
                Faulty("Cleared", e);
                return false;
            }
        }

        // ------------------------------------------------------------------ building new on cleared ground

        /// <summary>
        /// Test: a new building on ground the player cleared by a demolition, put up now: a plot planned on their cleared site (the
        /// first of the city's designs that fills its spaces, any size that does), its plans and permission at once, one of the
        /// city's builders hired for nothing, every stage moved on and handed over; the building the district's from the next load.
        /// </summary>
        public static bool TestClearedBuildNow()
        {
            const string test = "a new building on your cleared ground now";
            ConstructionDirector d = RedoDirector(test, false);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.ClearedBuildNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool ClearedBuildNow(string test)
        {
            if (GoingPublic.InSession)
            {
                return Failed(RedoCheck, test, "reallife_bld_test_session_city", "in a Going Public session the district's buildings stay as they are on every machine");
            }
            string lot = null;
            for (int i = 0; i < _state.Lots.Count && lot == null; i++)
            {
                lot = AskCleared(_state.Lots[i].Id) && _state.OnLot(_state.Lots[i].Id) == null ? _state.Lots[i].Id : null;
            }
            if (lot == null)
            {
                return Failed(RedoCheck, test, "reallife_bld_test_no_cleared", "the player holds no cleared ground with nothing planned on it (knock a building of theirs down first)");
            }
            BeforeTest();
            int id = NewPlot(lot, out string why);
            ProjectFile p = _state.Project(id);
            if (p == null || string.IsNullOrEmpty(p.Design))
            {
                return Failed(RedoCheck, test, why ?? "reallife_bld_answer_no_design",
                    "no building of the city's designs could be planned on cleared lot " + lot + " (" + (why ?? "no design fills it") + ")");
            }
            p.PlansDone = true;   // the plans and the permission at once, as the practice and City Hall would give them
            p.Quality = 70;
            p.Engineered = true;
            p.Planning = PlanningState.Granted;
            p.Stage = ProjectStage.Ready;
            p.After = AfterUse.Keep;
            p.Estimate = Math.Max(1L, Preview(p, out _, out _, out _, out _).Total);
            int count = Quotes(p, _quotes);
            string answer = HireBuilder(p, CommissionRules.Cheapest(_quotes, count), true);
            if (!p.Hired || p.Stage != ProjectStage.Building)
            {
                return Failed(RedoCheck, test, answer, "no builder could be hired for project " + p.Id + " (" + answer + ")");
            }
            int day = ConstructionGame.Day, site = p.SiteId;
            for (int k = 0; k < (int)Inspection.Count; k++)
            {
                AdvanceSiteNow(site);   // the street's site moves on with it
                HiredStage(p, (WorkStage)Math.Min(k + 1, (int)WorkStage.Envelope));
                Passed(p, k, true, day);
            }
            if (!FinishSite(site) && p.Stage == ProjectStage.Building)
            {
                Handover(p, day);   // the site was gone from the programme: handed over all the same
            }
            Lot(lot, out LotView ground);
            int spaces = SpacesOf(p.Size, p.Version);
            string where = "cleared lot " + lot + " (" + ground.Spaces + " space(s), " + ground.Width + " m)";
            if (!(p.Stage == ProjectStage.Finished && p.InDistrict && spaces >= 1 && spaces <= ground.Spaces))
            {
                Logger?.Warn(RedoCheck + test + "): " + where + ": project " + p.Id + " at " + p.Stage + ", " + p.Size + " v" + p.Version + " (" + spaces + " space(s)), the district's "
                             + p.InDistrict + ": FAIL.");
                return Failed(RedoCheck, test, "reallife_bld_test_failed_log", "the new building was not handed to the district");
            }
            Passed(test, "reallife_bld_test_cleared_build", where + ": " + p.Size + " v" + p.Version + " (" + spaces + " space(s)) in " + p.Design + ", built by "
                   + ConstructionText.T(BuildCatalogue.RivalKeys[p.Contractor]) + " for nothing and handed over now; the player's from the next load", TabProjects, p.Id);
            return true;
        }

        // ------------------------------------------------------------------ joining and splitting land

        private readonly List<LandRun> _testRuns = new List<LandRun>(16);
        private readonly List<string> _testOver = new List<string>(8);

        /// <summary>
        /// Test: two neighbouring free spaces of Uptown's the player's for nothing as two sites, joined into one (one site, its
        /// building over both), then split back into two; the price shared to the last unit. Nothing is knocked down.
        /// </summary>
        public static bool TestJoinSplitNow()
        {
            const string test = "two sites of yours joined into one and split back now";
            ConstructionDirector d = RedoDirector(test, false);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.JoinSplitNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool JoinSplitNow(string test)
        {
            if (GoingPublic.InSession || !DistrictLives())
            {
                return Failed(RedoCheck, test, "reallife_bld_test_session_city", "a Going Public session is on, or Uptown is not a district at this load");
            }
            Runs(_testRuns);
            Vector3 near = ConstructionGame.Player;
            string first = null;
            float best = float.MaxValue;
            for (int i = 0; i < _testRuns.Count; i++)
            {
                float far = Far(_testRuns[i].First, near);
                if (_testRuns[i].Spaces >= 2 && far < best)
                {
                    best = far;
                    first = _testRuns[i].First.Id;
                }
            }
            _testRuns.Clear();
            if (first == null || !SiteSpaces(first, 2, _strip) || _state.Lots.Count + 2 > BuildCatalogue.LotsMost)
            {
                return Failed(RedoCheck, test, "reallife_bld_test_nothing", "no run of two free spaces or more, or no room for two more sites");
            }
            string second = _strip[1].Id;
            long priceA = Math.Max(1L, LotPrice(_strip[0])), priceB = Math.Max(1L, LotPrice(_strip[1]));
            BeforeTest();
            int day = ConstructionGame.Day;
            _state.Lots.Add(new LotFile { Id = first, Bought = day, Paid = priceA, Zones = LandRules.ZonesOf(_strip[0].Class), TaxedTo = day, EmptySince = day, Spaces = 1 });
            _state.Lots.Add(new LotFile { Id = second, Bought = day, Paid = priceB, Zones = LandRules.ZonesOf(_strip[1].Class), TaxedTo = day, EmptySince = day, Spaces = 1 });
            string joined = Join(first);
            LotFile site = _state.Lot(first);
            bool one = joined == "reallife_bld_answer_joined" && site != null && site.Spaces == 2 && _state.Lot(second) == null && site.Paid == priceA + priceB
                       && Lot(first, out LotView ground) && ground.Spaces == 2;
            OverOf(first, _testOver);
            bool over = _testOver.Count == 1 && _testOver[0] == second;
            string split = Split(first);
            LotFile a = _state.Lot(first), b = _state.Lot(second);
            bool two = split == "reallife_bld_answer_split" && a != null && b != null && a.Spaces == 1 && b.Spaces == 1 && a.Paid + b.Paid == priceA + priceB;
            _testOver.Clear();
            string line = "spaces " + first + " and " + second + ": joined (" + joined + "), one site of 2 lots over " + (over ? "the second" : "nothing") + ", split back (" + split + ") into "
                          + (a != null ? a.Paid.ToString(CultureInfo.InvariantCulture) : "?") + " and " + (b != null ? b.Paid.ToString(CultureInfo.InvariantCulture) : "?")
                          + " of " + (priceA + priceB);
            if (!(one && over && two))
            {
                Logger?.Warn(RedoCheck + test + "): " + line + ": FAIL.");
                return Failed(RedoCheck, test, "reallife_bld_test_failed_log", "the sites were not joined and split as they should be");
            }
            Passed(test, "reallife_bld_test_join_split", line, TabLand, 0);
            return true;
        }

        // ------------------------------------------------------------------ extensions and change of use

        /// <summary>
        /// Test: the player's nearest building in Uptown given a storey more now, for good: an extension made, its plans and
        /// permission at once, the cheapest of the city's builders hired for nothing, its inspections passed and handed over at
        /// once; its floor area (the district's record and the deed) a storey more, its front its own.
        /// </summary>
        public static bool TestExtensionNow()
        {
            const string test = "a storey more on your nearest Uptown building now, for good";
            ConstructionDirector d = RedoDirector(test, false);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.ExtensionNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool ExtensionNow(string test)
        {
            if (GoingPublic.InSession || !Settings.ConstructionRedevelop)
            {
                return Failed(RedoCheck, test, GoingPublic.InSession ? "reallife_bld_test_session_city" : "reallife_bld_answer_redevelop_off",
                    "a Going Public session is on, or redevelopment is off in the options");
            }
            if (!NearestUptownBuilding(out BuildingView b, out float metres))
            {
                return Failed(RedoCheck, test, "reallife_bld_test_no_uptown", "the player owns no building of Uptown's that redevelopment may take (buy one in BizMan first)");
            }
            string block = ExtendBlock(b.Street, b.Number);
            if (block != null)
            {
                return Failed(RedoCheck, test, block, b.Name + " cannot be given a storey more now (" + block + ")");
            }
            BeforeTest();
            Standing(b.Street, b.Number, out StandingView before);
            int id = NewExtension(b.Street, b.Number, out string why);
            ProjectFile p = _state.Project(id);
            if (p == null)
            {
                return Failed(RedoCheck, test, why ?? "reallife_bld_test_failed_log", "no extension could be made at " + b.Name + " (" + why + ")");
            }
            p.PlansDone = true;   // the plans, the engineer and the permission at once, as the practice and City Hall would give them
            p.Quality = 70;
            p.Engineered = true;
            p.Planning = PlanningState.Granted;
            p.Stage = ProjectStage.Ready;
            p.Estimate = Math.Max(1L, Preview(p, out _, out _, out _, out _).Total);
            int count = Quotes(p, _quotes);
            string answer = HireBuilder(p, CommissionRules.Cheapest(_quotes, count), true);
            if (!p.Hired || p.Stage != ProjectStage.Building)
            {
                return Failed(RedoCheck, test, answer, "no builder could be hired for project " + p.Id + " (" + answer + ")");
            }
            bool keeps = KeepsLook(p.SiteId);
            int day = ConstructionGame.Day, site = p.SiteId;
            for (int k = 0; k < (int)Inspection.Count; k++)
            {
                AdvanceSiteNow(site);   // the street's site moves on with it
                HiredStage(p, (WorkStage)Math.Min(k + 1, (int)WorkStage.Envelope));
                Passed(p, k, true, day);
            }
            if (!FinishSite(site) && p.Stage == ProjectStage.Building)
            {
                Handover(p, day);   // the site was gone from the programme: handed over all the same
            }
            Standing(b.Street, b.Number, out StandingView after);
            string where = b.Name + ", " + metres.ToString("0", CultureInfo.InvariantCulture) + " m away, " + before.Storeys + " storey(s) and " + before.Sqm + " m2";
            if (!(p.Stage == ProjectStage.Finished && p.InDistrict && keeps && after.Storeys == before.Storeys + 1 && after.Sqm > before.Sqm && after.Design == before.Design))
            {
                Logger?.Warn(RedoCheck + test + "): " + where + ": project " + p.Id + " at " + p.Stage + ", the district's " + p.InDistrict + ", its front kept " + keeps + "; now "
                             + after.Storeys + " storey(s), " + after.Sqm + " m2: FAIL.");
                return Failed(RedoCheck, test, "reallife_bld_test_failed_log", "the building was not given its storey");
            }
            Passed(test, "reallife_bld_test_extend", where + ": a storey more by " + ConstructionText.T(BuildCatalogue.RivalKeys[p.Contractor]) + " for nothing, now " + after.Storeys
                   + " storey(s) and " + after.Sqm + " m2, the deed's with it, its front its own", TabProjects, p.Id);
            return true;
        }

        /// <summary>
        /// Test: the player's nearest building in Uptown made another use now, for good: a change of use made to the first use
        /// its structure takes, its permission granted and its notice run at once (its tenants and business gone, nothing paid),
        /// the city's renovation contractor's works for nothing, handed over at once; its building of the new type now.
        /// </summary>
        public static bool TestConversionNow()
        {
            const string test = "your nearest Uptown building made another use now, for good";
            ConstructionDirector d = RedoDirector(test, false);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.ConversionNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool ConversionNow(string test)
        {
            if (GoingPublic.InSession || !Settings.ConstructionRedevelop)
            {
                return Failed(RedoCheck, test, GoingPublic.InSession ? "reallife_bld_test_session_city" : "reallife_bld_answer_redevelop_off",
                    "a Going Public session is on, or redevelopment is off in the options");
            }
            if (!NearestUptownBuilding(out BuildingView b, out float metres))
            {
                return Failed(RedoCheck, test, "reallife_bld_test_no_uptown", "the player owns no building of Uptown's that redevelopment may take (buy one in BizMan first)");
            }
            string block = ConvertBlock(b.Street, b.Number);
            if (block != null)
            {
                return Failed(RedoCheck, test, block, b.Name + " cannot be made another use now (" + block + ")");
            }
            BeforeTest();
            Standing(b.Street, b.Number, out StandingView before);
            int id = NewConversion(b.Street, b.Number, out string why);
            ProjectFile p = _state.Project(id);
            if (p == null)
            {
                return Failed(RedoCheck, test, why ?? "reallife_bld_test_failed_log", "no change of use could be made at " + b.Name + " (" + why + ")");
            }
            p.Planning = PlanningState.Granted;   // the permission at once, as City Hall would grant it
            p.Stage = ProjectStage.Ready;
            p.NoticeUntil = ConstructionGame.Day;
            ConstructionGame.Evict(p.Street, p.Number);   // the notice run at once: the business closed and the tenants gone, nothing paid for a test
            p.BuyOut = -1L;
            string ordered = ContractorVacant(p, true);
            bool handed = p.SiteId > 0 && FinishSite(p.SiteId);
            if (!handed && p.Stage == ProjectStage.Building)
            {
                Handover(p, ConstructionGame.Day);   // the site was gone from the programme: handed over all the same
                handed = true;
            }
            Standing(b.Street, b.Number, out StandingView after);
            string type = BuildCatalogue.UseTypes[(int)p.Use];
            string where = b.Name + ", " + metres.ToString("0", CultureInfo.InvariantCulture) + " m away, of " + before.Type;
            if (!(handed && p.InDistrict && string.Equals(after.Type, type, StringComparison.Ordinal) && after.Sqm == before.Sqm))
            {
                Logger?.Warn(RedoCheck + test + "): " + where + ": project " + p.Id + " at " + p.Stage + " (" + ordered + "), the district's " + p.InDistrict + ", now of " + after.Type
                             + " (" + type + " asked): FAIL.");
                return Failed(RedoCheck, test, "reallife_bld_test_failed_log", "the building was not made its new use");
            }
            Passed(test, "reallife_bld_test_convert", where + ": permission granted, notice run, made " + type + " by contractor " + p.Contractor + " for nothing; an empty unit of the player's",
                TabProjects, p.Id);
            return true;
        }
    }
}
