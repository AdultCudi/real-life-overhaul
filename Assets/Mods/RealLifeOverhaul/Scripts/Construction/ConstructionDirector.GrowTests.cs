using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The growth's tests (Options > Tests, Construction, with the test tools; plans/growables.md): squares zoned with each of the
    /// Zone tool's ways and read back from the save; a developer's pass forced and its site begun; a grown building finished and
    /// its tenant moving in on its day; Let it grow at each tier on land of the player's; a building's condition falling while it
    /// stands empty. Each writes one "Construction check (growth: ...)" line ending PASS or FAIL and a notice, and is a public
    /// static method a runner can call. The state from before the first test is kept for the construction game's put back.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const string GrowCheck = "Construction check (growth: ";

        // What the tools may pick: the rows of each column the player holds.
        private sealed class HeldBy : ZoneRules.IHeld
        {
            private readonly ConstructionDirector _d;

            public HeldBy(ConstructionDirector d)
            {
                _d = d;
            }

            public int Held(int column) => _d.RowsHeld(column, out _);
        }

        private static ConstructionDirector GrowReady(string test)
        {
            ConstructionDirector d = Instance;
            if (d == null || !ModState.Loaded || SaveGameManager.Current == null || !d._loaded)
            {
                TestRefusals.Tell(Logger, GrowCheck + test + ")", TestRefusal.NotLoaded);
                Logger?.Warn(GrowCheck + test + "): the construction game has not read this save yet: FAIL.");
                return null;
            }
            if (!d.Running || !Settings.ConstructionGrowth || d.Cells.Columns.Count == 0 || GoingPublic.InSession)
            {
                TestRefusals.Tell(Logger, GrowCheck + test + ")", TestRefusal.FeatureOff);
                Logger?.Warn(GrowCheck + test + "): the construction game or the growth is off, Uptown is not a district at this load, or a Going Public session: FAIL.");
                return null;
            }
            return d;
        }

        private static bool GrowLine(string test, string what, bool pass)
        {
            Logger?.Warn(GrowCheck + test + "): " + what + ": " + (pass ? "PASS" : "FAIL") + ".");
            UI.Notification.Notifications.Show(pass ? UI.Notification.NotificationType.Info : UI.Notification.NotificationType.Warning,
                pass ? "reallife_bld_mode_test_pass" : "reallife_grow_test_fail", null, 7f, null, null, true, false);
            return pass;
        }

        // Two free columns side by side bought for nothing as a site of the player's: its first column, or -1.
        private int GrowTestLand(out LotFile site)
        {
            site = null;
            int first = FreeColumn(2);
            if (first < 0 || _state.Lots.Count >= BuildCatalogue.LotsMost)
            {
                return -1;
            }
            BeforeTest();
            BuyCells(PickColumns(first, 2, 0, out _), true);
            RowsHeld(first, out site);
            return site != null ? first : -1;
        }

        /// <summary>Test: squares of a site of the player's zoned with each way of the Zone tool (fill a side, a rectangle, the brush, unzone), then the save written and read back.</summary>
        public static bool TestGrowZoneNow()
        {
            ConstructionDirector d = GrowReady("zone squares");
            return d != null && d.GrowZoneTest();
        }

        private bool GrowZoneTest()
        {
            const string test = "zone squares";
            int first = GrowTestLand(out LotFile site);
            if (first < 0)
            {
                return GrowLine(test, "no two free spaces side by side at this load, or no room for land", false);
            }
            int zones = ZonesNow(site), a = -1, b = -1;
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if (LandRules.Zoned(zones, (BuildUse)u))
                {
                    a = a < 0 ? u : a;
                    b = a >= 0 && u != a ? u : b;
                }
            }
            b = b < 0 ? a : b;
            if (a < 0)
            {
                return GrowLine(test, "the site " + site.Id + " is zoned for nothing", false);
            }
            CellGrid grid = Cells;
            int second = grid.Next(first);
            var held = new HeldBy(this);
            var cells = new List<CellAt>(16);
            ZoneRules.FillSide(grid, first, held, cells);
            string fill = PaintCells(cells, a);
            bool filled = PaintOfSquare(first, 0, out _) == a + 1 && PaintOfSquare(second, grid.Columns[second].Rows - 1, out _) == a + 1;
            ZoneRules.Rect(grid, new CellAt(first, 1), new CellAt(second, 2), held, cells);
            int rectCount = cells.Count;
            string rect = PaintCells(cells, b);
            bool rected = rectCount == 4 && PaintOfSquare(first, 1, out _) == b + 1 && PaintOfSquare(second, 2, out _) == b + 1 && PaintOfSquare(first, 0, out _) == a + 1;
            cells.Clear();
            bool brushed = ZoneRules.Brush(grid, new CellAt(first, 3), held);
            cells.Add(new CellAt(first, 3));
            string brush = PaintCells(cells, b);
            brushed &= PaintOfSquare(first, 3, out _) == b + 1 && PaintOfSquare(second, 3, out _) == a + 1;
            ZoneRules.Rect(grid, new CellAt(second, 0), new CellAt(second, 0), held, cells);
            string unzone = PaintCells(cells, -1);
            bool unzoned = PaintOfSquare(second, 0, out _) == 0 && PaintOfSquare(first, 0, out _) == a + 1;
            string before = site.Cells;
            BuildState back = BuildStore.Read(BuildStore.Write(_state), out _);
            LotFile read = back != null ? back.Lot(site.Id) : null;
            bool saved = read != null && read.Cells == before && read.Paint == site.Paint;
            bool pass = filled && rected && brushed && unzoned && saved;
            TestDone("two free spaces the player's, their squares zoned with each way of the Zone tool", TabLand, 0);
            return GrowLine(test, "site " + site.Id + ": fill " + fill + " " + Yes(filled) + ", rectangle of " + rectCount + " " + rect + " " + Yes(rected) + ", brush " + brush + " " + Yes(brushed)
                                  + ", unzone " + unzone + " " + Yes(unzoned) + "; its squares '" + before + "' read back from the save " + Yes(saved), pass);
        }

        /// <summary>Test: the city's developers' pass forced now for the use with the most free land zoned for it; a site begins, its land the developer's.</summary>
        public static bool TestGrowPassNow()
        {
            ConstructionDirector d = GrowReady("a developer's pass");
            return d != null && d.GrowPassTest();
        }

        private bool GrowPassTest()
        {
            const string test = "a developer's pass";
            CellGrid grid = Cells;
            var free = new int[(int)BuildUse.Count];
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                int use = IsFreeColumn(c) ? CouncilColumnUse(c) : -1;
                free[Math.Max(0, use)] += use >= 0 ? 1 : 0;
            }
            int most = 0;
            for (int u = 1; u < free.Length; u++)
            {
                most = free[u] > free[most] ? u : most;
            }
            if (free[most] == 0)
            {
                return GrowLine(test, "no free land the council zones at this load", false);
            }
            BeforeTest();
            long land = _state.CityLand;
            int day = ConstructionGame.Day;
            GrowFile g = GrowBegin(day, most);
            bool begun = g != null && g.Stage == GrowStage.Plans && g.Until > day && _state.CityLand > land;
            bool taken = g != null && Lot(g.Lot, out LotView lot) && !FreeSpace(lot) && DeveloperOn(g.Lot);
            TestDone("the city's developers' pass forced for " + (BuildUse)most, TabCity, 0);
            return GrowLine(test, (BuildUse)most + " (" + free[most] + " free column(s) zoned): " + (g == null ? "nothing begun (no design fits, or the designs are still being read)"
                : GrowName(g) + ", " + g.Spaces + " space(s), " + GrowStageWords(g) + ", the land " + g.Price + " to the city " + Yes(begun) + ", no longer for sale " + Yes(taken)), begun && taken);
        }

        /// <summary>
        /// Test: a grown building finished now (each stage rushed) and its tenant's day worked: the day before it, its unit for rent;
        /// on it, let. A button only: the building stands in the district for good.
        /// </summary>
        public static void TestGrowTenantsNow()
        {
            ConstructionDirector d = GrowReady("tenants move in");
            d?.GrowTenantsTest();
        }

        private void GrowTenantsTest()
        {
            const string test = "tenants move in";
            GrowFile g = null;
            for (int i = 0; i < _state.Grows.Count && g == null; i++)
            {
                g = _state.Grows[i].UnderWay && _state.Grows[i].Owner != GrowOwner.Player ? _state.Grows[i] : null;
            }
            int day = ConstructionGame.Day;
            if (g == null)
            {
                for (int u = 0; u < (int)BuildUse.Count && g == null; u++)
                {
                    g = GrowBegin(day, u);
                }
            }
            if (g == null)
            {
                GrowLine(test, "no grown site under way and none could begin", false);
                return;
            }
            for (int step = 0; step < 4 && g.UnderWay; step++)
            {
                GrowStep(g, day, true);   // its plans, its permission, its works: each now
            }
            if (g.Stage != GrowStage.Letting)
            {
                GrowLine(test, GrowName(g) + " did not stand (" + GrowStageWords(g) + "; Player.log has why)", false);
                return;
            }
            int letOn = g.Until, demand = DemandFor(g.Use, g.Lot);
            bool before = StandingOnLot(g.Lot, out StandingView b) && BuildModeGame.Occupant(b.Street, b.Number, out bool vacantBefore, out _) && vacantBefore;
            GrowStep(g, letOn - 1, false);   // the day before its tenant's: still for rent
            bool waited = g.Stage == GrowStage.Letting;
            GrowStep(g, letOn, false);       // its day: let
            bool let = g.Stage == GrowStage.Let;
            bool occupied = StandingOnLot(g.Lot, out b) && BuildModeGame.Occupant(b.Street, b.Number, out bool vacantAfter, out string who) && !vacantAfter;
            bool pass = before && waited && let && (occupied || g.Use == BuildUse.Warehouses);
            TestDone("a grown building finished and let on its tenant's day", TabCity, 0);
            GrowLine(test, GrowName(g) + " stood on day " + g.Handed + "; with demand " + demand + " its tenant's day is " + letOn + " (" + (letOn - g.Handed) + " day(s) on): for rent before "
                           + Yes(before) + ", still for rent the day before " + Yes(waited) + ", let on the day " + Yes(let) + ", someone in " + Yes(occupied)
                           + (g.Use == BuildUse.Warehouses ? " (a warehouse waits for whoever rents it)" : string.Empty), pass);
        }

        /// <summary>
        /// Test: Let it grow on two spaces of the player's at each tier: the estimates rise with the tier, each project wears its tier's
        /// cost, quality and practice. Each given up after.
        /// </summary>
        public static bool TestGrowTiersNow()
        {
            ConstructionDirector d = GrowReady("Let it grow's tiers");
            return d != null && d.GrowTiersTest();
        }

        private bool GrowTiersTest()
        {
            const string test = "Let it grow's tiers";
            int first = GrowTestLand(out LotFile site);
            if (first < 0)
            {
                return GrowLine(test, "no two free spaces side by side at this load, or no room for land", false);
            }
            int rows = Cells.Columns[first].Rows;
            int use = GrowUseOf(first, 2, rows);
            if (use < 0)
            {
                int zones = ZonesNow(site);
                for (int u = 0; u < (int)BuildUse.Count && use < 0; u++)
                {
                    use = LandRules.Zoned(zones, (BuildUse)u) ? u : -1;
                }
                if (use >= 0)
                {
                    PaintSite(site.Id, use);   // a use the site allows on every square: Let it grow needs zoned squares
                }
            }
            var cost = new long[(int)GrowTier.Count];
            var said = new System.Text.StringBuilder(256);
            bool pass = use >= 0;
            for (int t = 0; t < (int)GrowTier.Count && pass; t++)
            {
                cost[t] = GrowEstimate(first, 2, rows, (GrowTier)t, out string design);
                int id = LetItGrowTest(first, 2, rows, (GrowTier)t, out string why);
                ProjectFile p = _state.Project(id);
                bool right = p != null && p.Grow == t + 1 && p.CostPermille == GrowRules.TierCostPermille[t] && p.Firm == GrowRules.TierFirm[t] && p.Architect == ArchitectKind.Firm;
                said.Append(t > 0 ? "; " : string.Empty).Append((GrowTier)t).Append(' ').Append(cost[t]).Append(" in ").Append(design ?? "none").Append(", project ").Append(id)
                    .Append(right ? " as its tier" : " NOT as its tier (" + (why ?? "?") + ")");
                pass &= cost[t] > 0 && right;
                if (p != null)
                {
                    Abandon(p);   // given up: the next tier on the same land
                }
            }
            pass &= cost[0] <= cost[1] && cost[1] <= cost[2] && cost[0] < cost[2];
            TestDone("Let it grow at each tier on two spaces the player's, each given up after", TabProjects, 0);
            string rising = Yes(cost[0] <= cost[1] && cost[1] <= cost[2]);
            return GrowLine(test, "site " + site.Id + " (" + (use >= 0 ? ((BuildUse)use).ToString() : "no use") + "): " + said + "; the estimates rise with the tier " + rising, pass);
        }

        // Let it grow as the button makes it, its plans not asked (no fee moved by a test): the project at its tier.
        private int LetItGrowTest(int firstColumn, int count, int rows, GrowTier tier, out string why)
        {
            why = null;
            int use = GrowUseOf(firstColumn, count, rows);
            if (use < 0 || !GrowPick(firstColumn, count, rows, (BuildUse)use, tier, out DesignChoice design))
            {
                why = use < 0 ? "reallife_grow_answer_no_zone" : "reallife_grow_answer_no_design";
                return 0;
            }
            int id = PlaceProject(firstColumn, count, design, (BuildUse)use, true, out why);
            ProjectFile p = _state.Project(id);
            if (p != null)
            {
                int t = (int)tier;
                p.Grow = t + 1;
                p.CostPermille = GrowRules.TierCostPermille[t];
                SetArchitect(p, ArchitectKind.Firm, GrowRules.TierFirm[t], null);
            }
            return id;
        }

        /// <summary>
        /// Test: a building of the city's standing empty, watched as a grown one for ten weeks: its condition falls past the grace, it is
        /// marked run down and its traffic drops; then refurbished, as new. Its record and traffic put back after.
        /// </summary>
        public static bool TestGrowConditionNow()
        {
            ConstructionDirector d = GrowReady("condition");
            return d != null && d.GrowConditionTest();
        }

        private bool GrowConditionTest()
        {
            const string test = "condition";
            ReadStanding();
            StandingView empty = default(StandingView);
            bool found = false;
            for (int i = 0; i < _standing.Count && !found; i++)
            {
                StandingView s = _standing[i];
                found = !s.Kept && string.IsNullOrEmpty(s.Lot) == false && GrowOf(s.Lot) == null && ConstructionGame.CityHeld(s.Street, s.Number)
                        && BuildModeGame.Occupant(s.Street, s.Number, out bool vacant, out _) && vacant;
                empty = found ? s : empty;
            }
            if (!found)
            {
                return GrowLine(test, "no empty building of the city's stands at this load", false);
            }
            BeforeTest();
            int day = ConstructionGame.Day;
            var g = new GrowFile { Id = _state.NewId(), Stage = GrowStage.Let, Owner = GrowOwner.Builder, Lot = empty.Lot, Spaces = 1, Handed = day - 70, Day = day - 100,
                EmptyDays = GrowRules.EmptyGraceDays };   // empty since its keys: past its grace from the first week
            _state.Grows.Add(g);
            GrowsChanged();
            int own = ConstructionGame.Building(empty.Street, empty.Number, out BuildingView before) ? before.Traffic : -1;
            var path = new System.Text.StringBuilder(64);
            for (int w = 0; w < 10; w++)
            {
                GrowLook(g, day + w * 7);
                path.Append(w > 0 ? ", " : string.Empty).Append(g.Condition);
            }
            int dropped = ConstructionGame.Building(empty.Street, empty.Number, out BuildingView after) ? after.Traffic : -1;
            bool fell = g.Condition < GrowRules.RunDownBelow && g.RunDown && dropped >= 0 && own >= 0 && dropped < own;
            GrowRefurbished(empty.Street, empty.Number);
            int back = ConstructionGame.Building(empty.Street, empty.Number, out BuildingView again) ? again.Traffic : -1;
            bool refurbished = g.Condition == GrowRules.ConditionFull && !g.RunDown && back >= dropped;
            _state.Grows.Remove(g);
            GrowsChanged();
            RetrafficNow(empty.Lot, 0);
            TestDone("an empty building of the city's watched as a grown one for ten weeks, then put back", TabCity, 0);
            return GrowLine(test, empty.Street + " " + empty.Number + " empty: condition by week " + path + ", run down " + Yes(g.RunDown || fell) + ", traffic " + own + " to " + dropped
                                  + "; refurbished: condition " + g.Condition + ", traffic " + back, fell && refurbished);
        }
    }
}
