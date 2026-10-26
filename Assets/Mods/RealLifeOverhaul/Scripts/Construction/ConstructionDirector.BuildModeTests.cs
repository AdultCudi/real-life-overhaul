using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Build mode's tests (Options > Tests, with the test tools; plans/build-mode.md section 8). Each writes one
    /// "Construction check (build mode: ...)" line ending PASS or FAIL and a notice, and is a public static method a runner can
    /// call. The state from before the first test is kept for the construction game's put back; money a test moves on purpose
    /// (a zoning fee) stays moved, as the other tests'.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const string ModeCheck = "Construction check (build mode: ";

        private static ConstructionDirector ModeReady(string test)
        {
            ConstructionDirector d = Instance;
            if (d == null || !ModState.Loaded || SaveGameManager.Current == null || !d._loaded)
            {
                TestRefusals.Tell(Logger, ModeCheck + test + ")", TestRefusal.NotLoaded);
                Logger?.Warn(ModeCheck + test + "): the construction game has not read this save yet: FAIL.");
                return null;
            }
            if (!d.Running || !Settings.ConstructionBuildMode || d.Cells.Columns.Count == 0)
            {
                TestRefusals.Tell(Logger, ModeCheck + test + ")", TestRefusal.FeatureOff);
                Logger?.Warn(ModeCheck + test + "): the construction game or build mode is off, or Uptown is not a district at this load: FAIL.");
                return null;
            }
            return d;
        }

        private static bool ModeLine(string test, string what, bool pass, string noticeKey)
        {
            Logger?.Warn(ModeCheck + test + "): " + what + ": " + (pass ? "PASS" : "FAIL") + ".");
            UI.Notification.Notifications.Show(pass ? UI.Notification.NotificationType.Info : UI.Notification.NotificationType.Warning, pass ? "reallife_bld_mode_test_pass" : noticeKey,
                null, 7f, null, null, true, false);
            return pass;
        }

        // The nearest run of free spaces to the player with at least so many, its first column; -1 for none.
        private int FreeColumn(int spaces)
        {
            CellGrid grid = Cells;
            Vector3 near = ConstructionGame.Player;
            int best = -1;
            float far = float.MaxValue;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                bool all = true;
                int at = c;
                for (int k = 0; k < spaces && all; k++, at = at >= 0 ? grid.Next(at) : -1)
                {
                    all = at >= 0 && LotOfColumn(at, out LotView lot) && FreeSpace(lot);
                }
                if (!all)
                {
                    continue;
                }
                grid.Rect(c, 0, 0f, out float x1, out float z1, out float x2, out float z2);
                float d = ((x1 + x2) * 0.5f - near.x) * ((x1 + x2) * 0.5f - near.x) + ((z1 + z2) * 0.5f - near.z) * ((z1 + z2) * 0.5f - near.z);
                if (d < far)
                {
                    far = d;
                    best = c;
                }
            }
            return best;
        }

        // The cells of so many columns from a first, each so many rows deep (0: every row).
        private CellSite PickColumns(int first, int count, int rows, out string why)
        {
            var picked = new List<CellAt>(count);
            CellGrid grid = Cells;
            int at = first;
            for (int k = 0; k < count && at >= 0; k++, at = grid.Next(at))
            {
                int r = rows <= 0 ? grid.Columns[at].Rows : Math.Min(rows, grid.Columns[at].Rows);
                picked.Add(new CellAt(at, r - 1));
            }
            why = CellRules.Site(grid, picked, BuildCatalogue.SiteSpacesMost, out CellSite site);
            return site;
        }

        /// <summary>Test: build mode opened and left again; the map, the time, the camera and the player as they were. A button only (it opens the map).</summary>
        public static void TestBuildModeOpenNow()
        {
            ConstructionDirector d = ModeReady("open and leave");
            if (d == null)
            {
                return;
            }
            d.StartCoroutine(Guard.Routine(d.OpenAndLeave()));
        }

        private IEnumerator OpenAndLeave()
        {
            const string test = "open and leave";
            Vector3 before = ConstructionGame.Player;
            bool pausedBefore = BuildModeGame.Paused;
            string why = OpenBuildMode();
            if (why != null)
            {
                ModeLine(test, "build mode would not open (" + why + ")", false, why);
                yield break;
            }
            yield return new WaitForSecondsRealtime(1.5f);
            bool open = _mode != null && _mode.IsOpen && BuildModeGame.MapOpen;
            bool paused = BuildModeGame.Paused;
            bool layer = _mode != null && _mode.LayerShown;
            _mode.Close();
            yield return new WaitForSecondsRealtime(1.5f);
            float moved = Vector3.Distance(before, ConstructionGame.Player);
            bool shut = !BuildModeGame.MapOpen && _mode != null && !_mode.IsOpen && BuildModeGame.CameraFree;
            bool time = BuildModeGame.Paused == pausedBefore;
            bool pass = open && paused && layer && shut && time && moved < 0.05f;
            ModeLine(test, "open over Uptown " + Yes(open) + ", the time paused " + Yes(paused) + ", the cells drawn " + Yes(layer) + "; left: the map shut and its camera free " + Yes(shut)
                           + ", the time as before " + Yes(time) + ", the player " + moved.ToString("0.00", CultureInfo.InvariantCulture) + " m from where they were",
                               pass, "reallife_bld_mode_test_fail");
        }

        private static string Yes(bool b) => b ? "YES" : "NO";

        /// <summary>Test: free cells picked give the Land tab's own price, every row and a short column. Nothing bought.</summary>
        public static bool TestBuildModePriceNow()
        {
            ConstructionDirector d = ModeReady("price");
            return d != null && d.PriceTest();
        }

        private bool PriceTest()
        {
            const string test = "price";
            int first = FreeColumn(2);
            if (first < 0)
            {
                return ModeLine(test, "no two free spaces side by side at this load", false, "reallife_bld_test_nothing");
            }
            CellSite whole = PickColumns(first, 2, 0, out string why);
            if (why != null)
            {
                return ModeLine(test, "the two columns made no site (" + why + ")", false, why);
            }
            string block = CellsBlock(whole, out long price, out _, out _, out _);
            LotOfColumn(first, out LotView lot);
            long tab = SitePrice(lot.Id, 2);
            CellSite shortSite = PickColumns(first, 1, 2, out _);
            CellsBlock(shortSite, out long shortPrice, out _, out _, out _);
            long wanted = CellRules.ColumnPrice(LotPrice(lot), 2, Cells.Columns[first].Rows);
            bool pass = block == null && price == tab && price > 0 && shortPrice == wanted && shortPrice < LotPrice(lot);
            return ModeLine(test, whole.Cells + " cells of 2 spaces from " + lot.Id + " at " + price + " against the Land tab's " + tab + "; 2 rows of one space at "
                + shortPrice + " (wanted " + wanted + ")",
                pass, "reallife_bld_mode_test_fail");
        }

        /// <summary>Test: three rows of the nearest free space bought for nothing; the site keeps its rows and its ground is cut to them.</summary>
        public static bool TestBuildModeBuyNow()
        {
            ConstructionDirector d = ModeReady("buy");
            return d != null && !GoingPublic.InSession && d.BuyTest();
        }

        private bool BuyTest()
        {
            const string test = "buy";
            int first = FreeColumn(1);
            if (first < 0 || _state.Lots.Count >= BuildCatalogue.LotsMost)
            {
                return ModeLine(test, "no free space at this load, or no room for land", false, "reallife_bld_test_nothing");
            }
            BeforeTest();
            CellSite site = PickColumns(first, 1, 3, out _);
            string answer = BuyCells(site, true);
            LotOfColumn(first, out LotView plan);
            LotFile own = _state.Lot(plan.Id);
            bool cut = own != null && Lot(plan.Id, out LotView ground) && ground.Depth < plan.Depth && ground.Depth > 0;
            bool rows = own != null && own.Rows == "3" && RowsHeld(first, out _) == 3;
            bool pass = answer == "reallife_bld_mode_bought" && rows && cut;
            TestDone("three rows of a free space the player's, for nothing", TabLand, 0);
            return ModeLine(test, "3 rows of " + plan.Id + ": " + answer + ", its rows " + (own != null ? "'" + own.Rows + "'" : "none") + ", its ground cut " + Yes(cut),
                pass, "reallife_bld_mode_test_fail");
        }

        /// <summary>Test: a use painted on a site of the player's: one its zones allow applies; one they do not is asked of the council (its fee paid).</summary>
        public static bool TestBuildModeZoneNow()
        {
            ConstructionDirector d = ModeReady("zone");
            return d != null && !GoingPublic.InSession && d.ZoneTest();
        }

        private bool ZoneTest()
        {
            const string test = "zone";
            LotFile site = null;
            for (int i = 0; i < _state.Lots.Count && site == null; i++)
            {
                site = _state.Lots[i].ZoneAsk == 0 && _state.OnLot(_state.Lots[i].Id) == null && !IsTestLot(_state.Lots[i].Id) ? _state.Lots[i] : null;
            }
            if (site == null)
            {
                int first = FreeColumn(1);
                if (first < 0)
                {
                    return ModeLine(test, "no site of the player's and no free space", false, "reallife_bld_test_nothing");
                }
                BeforeTest();
                BuyCells(PickColumns(first, 1, 0, out _), true);
                LotOfColumn(first, out LotView lot);
                site = _state.Lot(lot.Id);
            }
            if (site == null)
            {
                return ModeLine(test, "no site could be had", false, "reallife_bld_mode_test_fail");
            }
            BeforeTest();
            int zones = ZonesNow(site), allowed = -1, refused = -1;
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if (LandRules.Zoned(zones, (BuildUse)u))
                {
                    allowed = allowed < 0 ? u : allowed;
                }
                else
                {
                    refused = refused < 0 ? u : refused;
                }
            }
            string a = allowed >= 0 ? PaintSite(site.Id, allowed) : null;
            bool applied = allowed < 0 || (a == "reallife_bld_mode_painted" && site.Paint == allowed + 1);
            string b = refused >= 0 ? PaintSite(site.Id, refused) : null;
            bool asked = refused < 0 || (b == "reallife_bld_mode_paint_asked" && site.ZoneAsk == BuildCatalogue.Zone((BuildUse)refused) && site.Paint == refused + 1)
                         || b == "reallife_bld_answer_no_money";
            TestDone("a use painted on a site of the player's, and one asked of the council", TabLand, 0);
            return ModeLine(test, "site " + site.Id + ": an allowed use " + (a ?? "none to paint") + ", a use the council must allow " + (b ?? "none to ask"), applied
                && asked, "reallife_bld_mode_test_fail");
        }

        /// <summary>Test: a design placed in build mode makes the very project the app's own calls make on the same site (its use, design, size, floor area, ground).</summary>
        public static bool TestBuildModePlaceNow()
        {
            ConstructionDirector d = ModeReady("place");
            return d != null && !GoingPublic.InSession && d.PlaceTest();
        }

        private bool PlaceTest()
        {
            const string test = "place";
            int first = FreeColumn(2);
            if (first < 0)
            {
                return ModeLine(test, "no two free spaces side by side at this load", false, "reallife_bld_test_nothing");
            }
            BeforeTest();
            CellSite site = PickColumns(first, 2, 0, out _);
            BuyCells(site, true);
            LotOfColumn(first, out LotView lot);
            LotFile own = _state.Lot(lot.Id);
            if (own == null)
            {
                return ModeLine(test, "the test's site could not be had", false, "reallife_bld_mode_test_fail");
            }
            own.Zones = (1 << (int)BuildUse.Count) - 1;   // every use, so the design is the test's question, not the zoning's
            var designs = new List<DesignChoice>(DesignsAsked);
            bool ready = FootprintDesigns(site.FirstColumn, site.Count, site.MinRows, BuildUse.Offices, designs);
            if (!ready || designs.Count == 0)
            {
                return ModeLine(test, ready ? "no design of the city's fills two spaces of offices"
                    : "the city's designs are still being looked through: press again in a moment", false, "reallife_bld_mode_test_fail");
            }
            DesignChoice design = designs[0];
            // The app's own path first: the project, its use, the design among its choices.
            int oldId = NewPlot(lot.Id, out string refused);
            ProjectFile old = _state.Project(oldId);
            if (old == null)
            {
                return ModeLine(test, "the app's path made no project (" + refused + ")", false, "reallife_bld_mode_test_fail");
            }
            SetUse(old, BuildUse.Offices);
            DesignChoices(old, _choices);
            int index = -1;
            for (int i = 0; i < _choices.Count && index < 0; i++)
            {
                index = _choices[i].Name == design.Name && _choices[i].Size == design.Size ? i : -1;
            }
            if (index >= 0)
            {
                SetDesign(old, index);
            }
            string wanted = old.Use + "|" + old.Design + "|" + old.Size + "|" + old.Version + "|" + old.Area + "|" + old.Width + "|" + old.Depth + "|" + old.Lot;
            _state.Projects.Remove(old);
            own.EmptySince = ConstructionGame.Day;
            int id = PlaceProject(site.FirstColumn, site.Count, design, BuildUse.Offices, false, out string why);
            ProjectFile p = _state.Project(id);
            string got = p != null ? p.Use + "|" + p.Design + "|" + p.Size + "|" + p.Version + "|" + p.Area + "|" + p.Width + "|" + p.Depth + "|" + p.Lot : "none (" + why + ")";
            bool pass = p != null && index >= 0 && got == wanted;
            TestDone("a design placed in build mode on two spaces of the player's", TabProjects, id);
            return ModeLine(test, "the app's path " + wanted + "; build mode's " + got, pass, "reallife_bld_mode_test_fail");
        }

        /// <summary>Test: the bulldoze tool asks the demolition permit for the player's nearest Uptown building (its project made, then given up again).</summary>
        public static bool TestBuildModeBulldozeNow()
        {
            ConstructionDirector d = ModeReady("bulldoze");
            return d != null && d.BulldozeTest();
        }

        private bool BulldozeTest()
        {
            const string test = "bulldoze";
            CellGrid grid = Cells;
            Vector3 near = ConstructionGame.Player;
            LotView best = default(LotView);
            float far = float.MaxValue;
            bool found = false;
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                if (grid.Columns[c].LotColumn != 0 || !LotOfColumn(c, out LotView lot) || !FactsOf(c).MyBuilding || DemolishBlock(lot.Street, lot.Number) != null)
                {
                    continue;
                }
                float d = (lot.X - near.x) * (lot.X - near.x) + (lot.Z - near.z) * (lot.Z - near.z);
                if (d < far)
                {
                    far = d;
                    best = lot;
                    found = true;
                }
            }
            if (!found)
            {
                return ModeLine(test, "no Uptown building of the player's may be knocked down now (buy one in BizMan and empty it first)", false, "reallife_bld_mode_test_no_building");
            }
            BeforeTest();
            int id = NewDemolition(best.Street, best.Number, out string why);
            ProjectFile p = _state.Project(id);
            bool pass = p != null && p.Kind == ProjectKind.Demolition && p.Live;
            if (p != null)
            {
                Abandon(p);   // the test asks; it never knocks anything down
            }
            return ModeLine(test, "the demolition permit's project for " + ConstructionGame.AddressText(best.Street, best.Number) + ": " + (p != null ? "project " + id
                + ", given up again" : "none (" + why + ")"),
                pass, "reallife_bld_mode_test_fail");
        }

        /// <summary>Test: every building finished on the district's ground checked now as a whole building of the game's (one line each in Player.log).</summary>
        public static bool TestBuildModeCompleteNow()
        {
            ConstructionDirector d = ModeReady("whole buildings");
            if (d == null)
            {
                return false;
            }
            Func<int> all = BuildModeBridges.CompleteAll;
            int failed = all != null ? all() : -1;
            return ModeLine("whole buildings", failed < 0 ? "the district cannot be asked at this load" : failed
                + " finished building(s) with an item failed (each named in its own line)", failed == 0,
                "reallife_bld_mode_test_incomplete");
        }

        /// <summary>
        /// Test: a building stood through build mode and checked: two free spaces the player's for nothing, a design placed, its plans
        /// and permission granted at once, a builder of the city's hired for nothing and every stage done, handed over; the district
        /// stands it (at once, or at the next black screen) and its whole-building check line follows.
        /// </summary>
        public static bool TestBuildModeStandNow()
        {
            ConstructionDirector d = ModeReady("stand and check");
            return d != null && !GoingPublic.InSession && !GoingPublic.IsGuest && d.StandTest();
        }

        private bool StandTest()
        {
            const string test = "stand and check";
            if (!PlaceTest())
            {
                return ModeLine(test, "no project could be placed", false, "reallife_bld_mode_test_fail");
            }
            ProjectFile p = null;
            for (int i = _state.Projects.Count - 1; i >= 0 && p == null; i--)
            {
                p = _state.Projects[i].Live && _state.Projects[i].Kind == ProjectKind.Plot && _state.Projects[i].Stage == ProjectStage.Design ? _state.Projects[i] : null;
            }
            if (p == null)
            {
                return ModeLine(test, "the placed project is gone", false, "reallife_bld_mode_test_fail");
            }
            p.After = AfterUse.Keep;
            p.Storeys = Math.Max(1, p.Storeys);
            p.PlansDone = true;
            p.Quality = 70;
            p.Engineered = true;
            p.Planning = PlanningState.Granted;
            p.Stage = ProjectStage.Ready;
            p.Estimate = Math.Max(1L, Preview(p, out _, out _, out _, out _).Total);
            int count = Quotes(p, _quotes);
            string answer = HireBuilder(p, CommissionRules.Cheapest(_quotes, count), true);
            if (!p.Hired || p.Stage != ProjectStage.Building)
            {
                return ModeLine(test, "no builder could be hired (" + answer + ")", false, "reallife_bld_mode_test_fail");
            }
            int day = ConstructionGame.Day;
            int siteId = p.SiteId;
            for (int k = 0; k < (int)Inspection.Count; k++)
            {
                AdvanceSiteNow(siteId);
                HiredStage(p, (WorkStage)Math.Min(k + 1, (int)WorkStage.Envelope));
                Passed(p, k, true, day);
            }
            if (!FinishSite(siteId) && p.Stage == ProjectStage.Building)
            {
                Handover(p, day);
            }
            Func<string, bool> check = BuildModeBridges.CompleteNow;
            bool handed = p.Stage == ProjectStage.Finished && p.InDistrict;
            bool whole = handed && check != null && check(p.Lot);
            TestDone("a building stood through build mode and handed over", TabProjects, p.Id);
            return ModeLine(test, "project " + p.Id + " on " + p.Lot + " handed over " + Yes(handed) + "; the whole-building check " + (whole ? "passed now"
                : "follows when it stands (its line in Player.log)"),
                handed, "reallife_bld_mode_test_fail");
        }
    }
}
