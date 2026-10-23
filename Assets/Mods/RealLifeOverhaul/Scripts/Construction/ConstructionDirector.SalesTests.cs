using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Floors;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The show flats' and renovations' test entries (Options > Tests). Each works from anywhere at any time: it brings forward
    /// what it needs (a block sold off plan of the player's, a show flat, a viewing; a refit at the player's nearest business) and
    /// writes one line, "Show flats check (...)" or "Renovation check (...)", ending PASS or FAIL. The state before the first test
    /// is kept for "put the construction game back", which puts back the businesses' interiors a test refitted too. Each test's
    /// work is a public static method, so a test runner can call it with no press of its own.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const string ShowCheck = "Show flats check (";   // each line goes on with its test's name and ")"
        private const string RefitCheck = "Renovation check (";

        // The director when a test may run; otherwise the refusal said and a FAIL line written.
        private static ConstructionDirector TestDirector(string check, string test, bool shows)
        {
            ConstructionDirector d = Instance;
            if (d == null || !ModState.Loaded || SaveGameManager.Current == null || !d._loaded)
            {
                TestRefusals.Tell(Logger, check + test + ")", TestRefusal.NotLoaded);
                Logger?.Warn(check + test + "): the construction game has not read this save yet: FAIL.");
                return null;
            }
            if (!d.Running || (shows ? !Settings.ConstructionShowFlats : !Settings.ConstructionRenovations))
            {
                TestRefusals.Tell(Logger, check + test + ")", TestRefusal.FeatureOff);
                Logger?.Warn(check + test + "): " + (shows ? "show flats are" : "renovations are") + " off in the options: FAIL.");
                return null;
            }
            return d;
        }

        private static bool Failed(string check, string test, string key, string why)
        {
            TestRefusals.TellKey(Logger, check + test + ")", key, why);
            Logger?.Warn(check + test + "): " + why + ": FAIL.");
            return false;
        }

        // ------------------------------------------------------------------ show flats

        /// <summary>
        /// Test: a show flat at the test project now: the player's block sold off plan (made for the test if they have none) gets a
        /// show flat in its first design, dressed for nothing.
        /// </summary>
        public static bool TestShowFlatNow()
        {
            const string test = "a show flat at the test project now";
            ConstructionDirector d = TestDirector(ShowCheck, test, true);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.ShowFlatNow(test);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool ShowFlatNow(string test)
        {
            ProjectFile p = _state.ShowFlat();
            if (p != null)
            {
                Logger?.Warn(ShowCheck + test + "): project " + p.Id + "'s show flat is open already, in " + p.ShowDesign + ": PASS.");
                TestDone("project " + p.Id + "'s show flat is open", TabProjects, p.Id);
                return true;
            }
            p = TestBlock(test, out bool made, out string why);
            if (p == null)
            {
                return Failed(ShowCheck, test, "reallife_bld_test_no_block", why);
            }
            if (!ShowChoices(p, _showChoices) || _showChoices.Count == 0)
            {
                return Failed(ShowCheck, test, "reallife_bld_show_no_designs", "the game's residential designs give no show flat design");
            }
            BeforeTest();
            ShowDesignView design = _showChoices[0];
            string answer = OpenShow(p, design.Id, true);
            if (!p.ShowOpen)
            {
                return Failed(ShowCheck, test, answer, "the show flat was not opened (" + answer + ")");
            }
            bool there = ShowWhere(out FloorWhere at);
            int quality = ShowQuality(p);
            Logger?.Warn(ShowCheck + test + "): project " + p.Id + (made ? " (made for the test)" : string.Empty) + " has its show flat in " + design.Id + ", " + design.Pieces
                         + " pieces, quality " + quality + " against the district's standard " + p.ShowStandard + ", " + ShowFlatRules.Viewings(quality, p.ShowStandard) + " viewing(s) a day, "
                         + (there ? "on floor " + at.Floor + " of " + ConstructionGame.AddressText(at.Street, at.Number) : "waiting for a spare floor") + ": PASS.");
            TestDone("project " + p.Id + "'s show flat opened", TabProjects, p.Id);
            return true;
        }

        /// <summary>
        /// The player's block of homes that may have a show flat; made for a test when there is none: the new district's nearest open
        /// lot (or ground ahead), a block of homes on it with its plans drawn and its permission granted, ready to sell off plan.
        /// </summary>
        private ProjectFile TestBlock(string test, out bool made, out string why)
        {
            made = false;
            why = null;
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (ShowFlatRules.Allowed(p, FlatsOf(p)))
                {
                    return p;
                }
            }
            if (GoingPublic.InSession)
            {
                why = "no land is sold in a Going Public session, so no block can be made for the test";
                return null;
            }
            BeforeTest();
            string lot = TestLotId(out why);
            if (lot == null)
            {
                return null;
            }
            int id = NewPlot(lot, out string refused);
            ProjectFile block = _state.Project(id);
            if (block == null)
            {
                why = "no project could be made on lot " + lot + " (" + refused + ")";
                return null;
            }
            LotFile owned = _state.Lot(lot);
            if (owned != null)
            {
                owned.Zones |= BuildCatalogue.Zone(BuildUse.Homes);   // a test's block of homes whatever the lot was zoned for
            }
            block.Use = BuildUse.Homes;
            DefaultDesign(block);
            if (block.Area <= 0 && Lot(lot, out LotView view))
            {
                block.Area = Math.Max(BuildCatalogue.FlatArea * BuildCatalogue.FlatsLeast, LandRules.UsualArea(view.Width, view.Depth, view.Class));
            }
            block.PlansDone = true;
            block.Quality = 70;
            block.Engineered = true;
            block.Planning = PlanningState.Granted;
            block.Stage = ProjectStage.Ready;
            block.After = AfterUse.SellFlats;
            block.Flats = FlatsOf(block);
            made = true;
            Logger?.Info("Construction: project " + block.Id + " made for the show flats' test on lot " + lot + ": " + block.Flats + " flats, ready to sell off plan.");
            return block;
        }

        // A lot of the player's for a test's block: one they hold with nothing on it, else the district's nearest open lot, else ground ahead.
        private string TestLotId(out string why)
        {
            why = null;
            for (int i = 0; i < _state.Lots.Count; i++)
            {
                if (_state.OnLot(_state.Lots[i].Id) == null && Lot(_state.Lots[i].Id, out _))
                {
                    return _state.Lots[i].Id;
                }
            }
            int today = ConstructionGame.Day;
            if (NearestOpenLot(ConstructionGame.Player, out LotView open, out _))
            {
                _state.Lots.Add(new LotFile { Id = open.Id, Bought = today, Zones = LandRules.ZonesOf(open.Class), TaxedTo = today, EmptySince = today });
                return open.Id;
            }
            if (!ConstructionGame.Outdoors)
            {
                why = "the new district has no open lot, and the ground ahead can only be looked at from outdoors";
                return null;
            }
            Vector3 ahead = ConstructionGame.Ahead;
            int yaw = Mathf.RoundToInt(Quaternion.LookRotation(ahead).eulerAngles.y);
            Vector3 middle = ConstructionGame.Player + ahead * (TestLotDepth * 0.5f + 2f);
            if (!GroundClear(middle, yaw, TestLotWidth, TestLotDepth, out float ground))
            {
                why = "the new district has no open lot and the ground ahead is not clear";
                return null;
            }
            string id = TestLotPrefix + _state.NewId().ToString(CultureInfo.InvariantCulture);
            _testLots.Add(new LotView
            {
                Id = id, Street = string.Empty, X = middle.x, Y = ground, Z = middle.z, Yaw = yaw, Width = TestLotWidth, Depth = TestLotDepth, Class = 0, Open = true, Traffic = 50
            });
            _testPrice = ConstructionGame.PriceNear(middle);
            _state.Lots.Add(new LotFile { Id = id, Bought = today, Zones = (1 << (int)BuildUse.Count) - 1, TaxedTo = today, EmptySince = today });
            return id;
        }

        /// <summary>Test: the player taken into the show flat now, by its building's own door from wherever they are (one is opened first if none is).</summary>
        public static bool TestShowVisitNow()
        {
            const string test = "into the show flat now";
            ConstructionDirector d = TestDirector(ShowCheck, test, true);
            if (d == null)
            {
                return false;
            }
            try
            {
                if (d._state.ShowFlat() == null && !d.ShowFlatNow("a show flat at the test project now"))
                {
                    return Failed(ShowCheck, test, "reallife_bld_test_no_block", "no show flat could be opened first");
                }
                if (d.InShowFlat)
                {
                    Logger?.Warn(ShowCheck + test + "): the player is in the show flat already: PASS.");
                    return true;
                }
                if (!d.ShowWhere(out FloorWhere at))
                {
                    return Failed(ShowCheck, test, "reallife_bld_answer_show_no_floor", "the show flat has no floor yet: every building the floors hold is full and none could be taken");
                }
                string refused = FloorsDirector.Enter(at.Host, at.Floor, "the show flats' test");
                if (refused != null)
                {
                    return Failed(ShowCheck, test, "reallife_bld_test_no_way_in", "the way in was refused: " + refused);
                }
                Logger?.Warn(ShowCheck + test + "): the player goes in to floor " + at.Floor + " of " + ConstructionGame.AddressText(at.Street, at.Number)
                             + " by the game's own door; the show flat's own lines follow as it is dressed: PASS.");
                return true;
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        /// <summary>Test: a viewing at the show flat now, decided at once by its own chance (the player showing the buyers round if they are there).</summary>
        public static bool TestViewingNow()
        {
            return ViewingTest("a viewing now", false);
        }

        /// <summary>Test: a buyer at the show flat now: a viewing that sells a flat at the show flat's price.</summary>
        public static bool TestShowBuyerNow()
        {
            return ViewingTest("a buyer at the show flat now", true);
        }

        private static bool ViewingTest(string test, bool buyer)
        {
            ConstructionDirector d = TestDirector(ShowCheck, test, true);
            if (d == null)
            {
                return false;
            }
            try
            {
                if (d._state.ShowFlat() == null && !d.ShowFlatNow("a show flat at the test project now"))
                {
                    return Failed(ShowCheck, test, "reallife_bld_test_no_block", "no show flat could be opened first");
                }
                ProjectFile p = d._state.ShowFlat();
                d.BeforeTest();
                int key = HourKey(ConstructionGame.Day, ConstructionGame.Hour);
                if (d.InShowFlat)
                {
                    p.ShowShown = key;   // the player is there: they show the buyers round
                }
                ViewingHost host = HostOf(p, key);
                int quality = ShowQuality(p);
                int chance = ShowFlatRules.BuyerChance(quality, p.ShowStandard, d._state.Firm.Reputation, host);
                int soldBefore = p.FlatsSold;
                long balanceBefore = p.OffPlanBalance + p.Deposits;
                int price = ShowFlatRules.Price(d.OffPlanPrice(p), quality, p.ShowStandard);
                bool sold = d.Viewing(p, key, buyer);
                bool right = sold == (p.FlatsSold == soldBefore + 1) && (!sold || p.OffPlanBalance + p.Deposits - balanceBefore == price) && (!buyer || sold);
                Logger?.Warn(ShowCheck + test + "): project " + p.Id + "'s show flat, shown round by " + host + ", quality " + quality + " against " + p.ShowStandard
                             + ", a buyer's chance " + chance + " in 1000: " + (sold ? "a flat sold at " + price + ", " + (FlatsOf(p) - p.FlatsSold) + " left" : "no buyer this time")
                             + (right ? ": PASS." : ": FAIL (the sale's figures do not add up)."));
                d.TestDone(sold ? "a flat sold at the show flat" : "a viewing with no buyer", TabProjects, p.Id);
                return right;
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        // ------------------------------------------------------------------ renovations

        // The player's business nearest to them, or false.
        private bool NearestBusiness(out BusinessView nearest, out float metres)
        {
            nearest = default(BusinessView);
            metres = 0f;
            Refittable(_businesses);
            Vector3 player = ConstructionGame.Player;
            float best = float.MaxValue;
            for (int i = 0; i < _businesses.Count; i++)
            {
                Vector3 d = _businesses[i].Door - player;
                d.y = 0f;
                if (d.sqrMagnitude < best)
                {
                    best = d.sqrMagnitude;
                    nearest = _businesses[i];
                }
            }
            metres = best < float.MaxValue ? Mathf.Sqrt(best) : 0f;
            return best < float.MaxValue;
        }

        /// <summary>Test: renovate the nearest shop of the player's now: a refit started there (the contractor's, for nothing) and handed over at once.</summary>
        public static bool TestRefitNow()
        {
            const string test = "renovate the nearest shop of yours now";
            ConstructionDirector d = TestDirector(RefitCheck, test, false);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.RefitNow(test, true);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        /// <summary>Test: a refit under way at the nearest business of the player's now, left to run: the hoarding up and the business closed.</summary>
        public static bool TestRefitStartNow()
        {
            const string test = "a refit under way at your nearest business now";
            ConstructionDirector d = TestDirector(RefitCheck, test, false);
            if (d == null)
            {
                return false;
            }
            try
            {
                return d.RefitNow(test, false);
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        private bool RefitNow(string test, bool finish)
        {
            if (!NearestBusiness(out BusinessView b, out float metres))
            {
                return Failed(RefitCheck, test, "reallife_bld_test_no_business", "the player runs no shop or office (rent one and open a business in it first)");
            }
            ProjectFile p = _state.AtBuilding(b.Street, b.Number);
            BeforeTest();
            KeepInterior(b);
            ReadInterior(b, out int decorBefore, out _, out int wearBefore);
            bool started = false;
            if (p == null)
            {
                int id = ContractorRefit(b.Street, b.Number, true, out string why);
                p = _state.Project(id);
                if (p == null)
                {
                    return Failed(RefitCheck, test, why ?? "reallife_bld_answer_site_refused", "no refit could start at " + b.Name + " (" + why + ")");
                }
                started = true;
            }
            else if (p.Kind == ProjectKind.Refit && p.Stage == ProjectStage.Ready)
            {
                int site = StartWorksSite(p.Street, p.Number, RefitRules.Works, true, 0, p.Hours, out string refused);   // the company's refit started for the test, crews or not
                if (site <= 0)
                {
                    return Failed(RefitCheck, test, "reallife_bld_answer_site_refused", "the company's refit at " + b.Name + " could not start: " + refused);
                }
                p.SiteId = site;
                p.Stage = ProjectStage.Building;
                p.StartDay = ConstructionGame.Day;
                p.DueDay = PlanningRules.AfterWorkingDays(p.StartDay, TenderRules.StandardDays(p.Hours));
                HoldShut(p);
                started = true;
            }
            if (!finish)
            {
                bool under = p.Stage == ProjectStage.Building && p.SiteId > 0;
                Logger?.Warn(RefitCheck + test + "): " + b.Name + ", " + metres.ToString("0", CultureInfo.InvariantCulture) + " m away: "
                             + (under ? (started ? "a refit started now, " : "its refit already under way, ") + "site " + p.SiteId + ", due on day " + p.DueDay
                                        + ", the business kept closed for it: PASS." : "its project is at " + p.Stage + ", not under way: FAIL."));
                TestDone("a refit under way at " + b.Name, TabProjects, p.Id);
                return under;
            }
            bool handed = p.Stage == ProjectStage.Building && p.SiteId > 0 && FinishSite(p.SiteId);
            if (!handed && p.Stage == ProjectStage.Building)
            {
                Handover(p, ConstructionGame.Day);   // the site was gone from the programme: handed over all the same
                handed = true;
            }
            ReadInterior(b, out int decorAfter, out int refit, out int wearAfter);
            bool interiors = Settings.Interiors;
            bool pass = handed && p.Stage == ProjectStage.Finished && (!interiors || (wearAfter == 0 && refit > 0 && decorAfter >= decorBefore));
            Logger?.Warn(RefitCheck + test + "): " + b.Name + ", " + metres.ToString("0", CultureInfo.InvariantCulture) + " m away, refitted now by "
                         + (p.Contractor >= 0 ? "contractor " + p.Contractor : "the company") + " (grade " + p.Grade + "): wear " + wearBefore / 10 + "% -> " + wearAfter / 10 + "%, decor "
                         + decorBefore + " -> " + decorAfter + " (the new fittings +" + refit + ")" + (interiors ? string.Empty : ", Interiors off") + "; the business open again"
                         + (pass ? ": PASS." : ": FAIL."));
            TestDone(b.Name + " refitted now", TabProjects, p.Id);
            return pass;
        }

        private static void KeepInterior(BusinessView b)
        {
            try
            {
                ConstructionBridges.KeepInterior?.Invoke(b.Street, b.Number);
            }
            catch (Exception e)
            {
                Faulty("KeepInterior", e);
            }
        }

        private static bool ReadInterior(BusinessView b, out int decor, out int refit, out int wear)
        {
            decor = refit = wear = 0;
            try
            {
                return !Down("Interior") && ConstructionBridges.Interior != null && ConstructionBridges.Interior(b.Street, b.Number, out decor, out refit, out wear);
            }
            catch (Exception e)
            {
                Faulty("Interior", e);
                return false;
            }
        }

        /// <summary>Test: a business of the city's puts its refit out to tender now (closing the next day), the renovation speciality's work.</summary>
        public static bool TestRefitTenderNow()
        {
            const string test = "a business's refit tender now";
            ConstructionDirector d = TestDirector(RefitCheck, test, false);
            if (d == null)
            {
                return false;
            }
            if (GoingPublic.IsGuest)
            {
                return Failed(RefitCheck, test, "reallife_bld_test_host", "a guest of a Going Public session puts out no tenders: the host's programme does");
            }
            if (!Settings.ConstructionDevelopers)
            {
                return Failed(RefitCheck, test, "reallife_bld_test_developers_off", "the city's developers and tenders are off in the options");
            }
            try
            {
                d.BeforeTest();
                int before = d._state.Tenders.Count, day = ConstructionGame.Day;
                d._state.NextRefitDay = day;
                d.RefitTenders(day);
                TenderFile t = d._state.Tenders.Count > before ? d._state.Tenders[d._state.Tenders.Count - 1] : null;
                if (t == null || t.Source != TenderSource.Refit)
                {
                    return Failed(RefitCheck, test, "reallife_bld_test_nothing", "no business of the city's could be found for a refit");
                }
                Logger?.Warn(RefitCheck + test + "): tender " + t.Id + ", " + d.TenderName(t) + ": " + t.Hours + " working hours for " + t.Value + ", the renovation speciality's work ("
                             + TenderRules.NeedOf(t.Source, t.CivicKind, t.Use) + "), closing at hour " + t.Closes % 24 + " of day " + t.Closes / 24 + ": PASS.");
                d.TestDone("a business's refit out to tender", TabTenders, t.Id);
                return true;
            }
            catch (Exception e)
            {
                d.Fault("a test (" + test + ")", e);
                return false;
            }
        }

        /// <summary>Test: the construction game put back as before the first test, the interiors a test refitted with it (the options page's "put back").</summary>
        public static bool TestPutBackNow()
        {
            ConstructionDirector d = Instance;
            if (d == null)
            {
                TestRefusals.Tell(Logger, "Construction back", TestRefusal.NotLoaded);
                return false;
            }
            d.TestBack();
            return d._backDone;
        }

        // What the show flats' and refits' tests changed beyond the line: the interiors refitted, the closures, the show flat's floor.
        private int SalesBack(BuildState was)
        {
            int interiors = 0;
            try
            {
                interiors = !Down("InteriorsBack") && ConstructionBridges.InteriorsBack != null ? ConstructionBridges.InteriorsBack() : 0;
            }
            catch (Exception e)
            {
                Faulty("InteriorsBack", e);
            }
            for (int i = 0; i < was.Projects.Count; i++)
            {
                ProjectFile p = was.Projects[i];
                if (ClosesShop(p) && _state.AtBuilding(p.Street, p.Number) == null)
                {
                    LetOpen(p);   // its refit is gone with the tests: the business opens
                }
            }
            SyncShowPlace();
            return interiors;
        }
    }
}
