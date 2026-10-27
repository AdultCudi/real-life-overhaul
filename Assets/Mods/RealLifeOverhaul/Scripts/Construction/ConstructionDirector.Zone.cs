using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// Zoning per square (plans/growables.md section 1): a use painted on squares of the player's own land, each site's squares
    /// kept as digits (LotFile.Cells, ZoneRules) and its own paint the use most of them carry; a use the site's zones do not
    /// allow goes to City Hall through AskZone as before, its squares shown waiting until the council decides. The council's
    /// own zoning of free land square by square (BuildState.CouncilCells; the plan's use for a column it never changed), which
    /// land bought takes with it, and which the council changes at its weekly sitting where one use is wanted and another is not
    /// (ZoneRules.Rezone, its odds the player's own rule's lift by demand). On a press and the day's pass; nothing per frame.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private readonly List<int> _paintColumns = new List<int>(64);
        private readonly List<int> _paintRows = new List<int>(64);
        private readonly List<LotFile> _paintSites = new List<LotFile>(4);
        private int[] _siteRows = new int[BuildCatalogue.SiteSpacesMost];

        /// <summary>A column's place within a site of the player's (0 its first space), or -1 when the site does not hold it.</summary>
        public int SiteColumn(int column, LotFile site)
        {
            CellGrid grid = Cells;
            if (site == null || column < 0 || column >= grid.Columns.Count || !Space(site.Id, out LotView first) || first.Strip != grid.Columns[column].Strip)
            {
                return -1;
            }
            int at = grid.Columns[column].Space - first.Index;
            return at >= 0 && at < site.Spaces ? at : -1;
        }

        // Each of a site's columns' rows held, into the site buffer: its digits' shape for ZoneRules.
        private int[] SiteRows(LotFile site)
        {
            if (_siteRows.Length < site.Spaces)
            {
                _siteRows = new int[site.Spaces];
            }
            int stripRows = Space(site.Id, out LotView first) ? CellRules.RowsFor(first.Depth) : 5;
            for (int i = 0; i < site.Spaces; i++)
            {
                _siteRows[i] = CellRules.RowsAt(site.Rows, i, stripRows);
            }
            return _siteRows;
        }

        /// <summary>The council's paint of a free square now (0 unzoned, else BuildUse + 1): its record, else the plan's use for its lot.</summary>
        public int CouncilAt(int column, int row)
        {
            CellGrid grid = Cells;
            if (column < 0 || column >= grid.Columns.Count || !LotOfColumn(column, out LotView lot))
            {
                return 0;
            }
            CellColumn c = grid.Columns[column];
            _state.CouncilCells.TryGetValue(CellRules.Key(c.Strip, c.Space), out string record);
            return ZoneRules.CouncilAt(record, row, lot.Id, lot.Class);
        }

        // The council's use on every row of a free column (BuildUse), -1 when its rows differ or it is unzoned.
        private int CouncilColumnUse(int column)
        {
            int rows = Cells.Columns[column].Rows, first = CouncilAt(column, 0);
            for (int r = 1; r < rows; r++)
            {
                if (CouncilAt(column, r) != first)
                {
                    return -1;
                }
            }
            return first - 1;
        }

        /// <summary>
        /// A use painted on squares of the player's own land (pairs of column and row; <paramref name="use"/> -1 takes the zoning
        /// away): on each site they fall on, a use its zones allow is in force at once, one they do not goes to City Hall (AskZone,
        /// its fee) and the squares wait on the council. Squares not the player's are left alone. Returns a text key.
        /// </summary>
        public string PaintCells(IList<CellAt> cells, int use)
        {
            if (GoingPublic.InSession)
            {
                return "reallife_bld_answer_session_land";
            }
            if (cells == null || cells.Count == 0)
            {
                return "reallife_grow_zone_nothing";
            }
            _paintSites.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                RowsHeld(cells[i].Column, out LotFile site);
                if (site != null && !_paintSites.Contains(site))
                {
                    _paintSites.Add(site);
                }
            }
            if (_paintSites.Count == 0)
            {
                return "reallife_bld_hint_zone_not_yours";
            }
            string answer = use < 0 ? "reallife_bld_mode_painted_none" : "reallife_bld_mode_painted";
            int painted = 0;
            for (int s = 0; s < _paintSites.Count; s++)
            {
                LotFile site = _paintSites[s];
                bool waits = use >= 0 && !LandRules.Zoned(ZonesNow(site), (BuildUse)use);
                if (waits && (site.ZoneAsk & BuildCatalogue.Zone((BuildUse)use)) == 0)
                {
                    string asked = AskZone(site.Id, (BuildUse)use);
                    if (asked != "reallife_bld_answer_zone_asked")
                    {
                        answer = asked;   // another request waits on the council, or no money for the fee: these squares stay as they were
                        continue;
                    }
                    answer = "reallife_bld_mode_paint_asked";
                }
                else if (waits)
                {
                    answer = "reallife_bld_mode_paint_asked";   // asked already: these squares wait with the rest
                }
                _paintColumns.Clear();
                _paintRows.Clear();
                for (int i = 0; i < cells.Count; i++)
                {
                    int held = RowsHeld(cells[i].Column, out LotFile on);
                    int at = on == site ? SiteColumn(cells[i].Column, site) : -1;
                    if (at >= 0 && cells[i].Row < held)
                    {
                        _paintColumns.Add(at);
                        _paintRows.Add(cells[i].Row);
                    }
                }
                int before = site.Paint;
                site.Cells = ZoneRules.SetMany(site.Cells, site.Spaces, SiteRows(site), site.Paint, _paintColumns, _paintRows, BuildModeRules.PaintOf(use));
                site.Paint = ZoneRules.Main(site.Cells, site.Paint);
                painted += _paintColumns.Count;
                ProjectFile p = _state.OnLot(site.Id);
                if (site.Paint > 0 && site.Paint != before && p != null && p.Stage == ProjectStage.Design && p.Kind == ProjectKind.Plot
                    && LandRules.Zoned(ZonesNow(site), (BuildUse)(site.Paint - 1)) && p.Use != (BuildUse)(site.Paint - 1))
                {
                    SetUse(p, (BuildUse)(site.Paint - 1));   // the plans in hand take the use most of the site now carries, as the app's use row does
                }
            }
            Logger?.Info("Construction: build mode painted " + painted + " square(s) of " + _paintSites.Count + " site(s) " + (use < 0 ? "with no zone" : ((BuildUse)use).ToString())
                         + ": " + answer + ".");
            Changed();
            return answer;
        }

        /// <summary>The paint of one square of the player's land (0 none, else BuildUse + 1), and whether it waits on the council; 0 for a square not theirs.</summary>
        public int PaintOfSquare(int column, int row, out bool waits)
        {
            waits = false;
            int held = RowsHeld(column, out LotFile site);
            int at = site != null ? SiteColumn(column, site) : -1;
            if (at < 0 || row >= held)
            {
                return 0;
            }
            int paint = ZoneRules.At(site.Cells, at, row, site.Paint);
            waits = paint > 0 && !LandRules.Zoned(ZonesNow(site), (BuildUse)(paint - 1));
            return paint;
        }

        // A site's squares bought: each takes the council's zoning of it, the site's zones the uses among them (what the council
        // zoned free land for is the land's own once bought). Called as the site is made.
        private void TakeCouncilZoning(LotFile site)
        {
            CellGrid grid = Cells;
            if (!Space(site.Id, out LotView first))
            {
                return;
            }
            int column = grid.ColumnOf(first.Strip, first.Index);
            int[] rows = SiteRows(site);
            string cells = string.Empty;
            for (int i = 0; i < site.Spaces && column >= 0; i++, column = grid.Next(column))
            {
                for (int r = 0; r < rows[i]; r++)
                {
                    int council = CouncilAt(column, r);
                    cells = ZoneRules.Set(cells, site.Spaces, rows, 0, i, r, council);
                    if (council > 0)
                    {
                        site.Zones |= BuildCatalogue.Zone((BuildUse)(council - 1));
                    }
                }
            }
            site.Cells = cells;
            site.Paint = ZoneRules.Main(cells, 0);
        }

        // ------------------------------------------------------------------ the council's own zoning of free land

        private readonly int[] _demandNow = new int[(int)BuildUse.Count];
        private readonly int[] _freeColumns = new int[(int)BuildUse.Count];

        // The council's weekly sitting: where one use is wanted and another's free land stands idle, a few free columns side by side
        // rezoned for the wanted use, at the odds the player's own requests are heard at (the demand's lift). Outside a session alone.
        private void CouncilZoningDay(int day)
        {
            if (GoingPublic.InSession || LandRules.Weekday(day) != BuildCatalogue.CouncilWeekday || !DistrictLives())
            {
                return;
            }
            for (int d = 0; d < BuildCatalogue.Districts; d++)
            {
                CouncilSitting(day, d);   // the same sitting for each of the band's districts, by its own demand and its own free land
            }
        }

        private void CouncilSitting(int day, int district)
        {
            CellGrid grid = Cells;
            for (int u = 0; u < _demandNow.Length; u++)
            {
                _demandNow[u] = DemandFor((BuildUse)u, district);
                _freeColumns[u] = 0;
            }
            for (int c = 0; c < grid.Columns.Count; c++)
            {
                if (IsFreeColumn(c) && DistrictOfColumn(c) == district)
                {
                    int use = CouncilColumnUse(c);
                    _freeColumns[Math.Max(0, use)] += use >= 0 ? 1 : 0;
                }
            }
            if (!ZoneRules.Rezone(_demandNow, _freeColumns, out int to, out int from))
            {
                return;
            }
            BuildRandom random = BuildRandom.Of(_state.Seed, day, 0x7A0E + district);   // a draw of its own for each district
            if (!random.Chance(ZoneRules.RezoneOdds(_demandNow[to])))
            {
                Logger?.Info("Construction: the council's sitting on day " + day + " kept the zoning (" + (BuildUse)to + " wanted at " + _demandNow[to] + ", " + (BuildUse)from + " at "
                             + _demandNow[from] + ").");
                return;
            }
            // The first run of free columns of the idle use, from a point along the district drawn from the seed and the day.
            int start = random.Range(0, Math.Max(1, grid.Columns.Count)), done = 0;
            string street = null;
            for (int k = 0; k < grid.Columns.Count && done == 0; k++)
            {
                int c = (start + k) % grid.Columns.Count;
                if (!IsFreeColumn(c) || DistrictOfColumn(c) != district || CouncilColumnUse(c) != from || (grid.Prev(c) >= 0 && IsFreeColumn(grid.Prev(c)) && CouncilColumnUse(grid.Prev(c)) == from))
                {
                    continue;
                }
                for (int at = c; at >= 0 && done < ZoneRules.RezoneMost && IsFreeColumn(at) && DistrictOfColumn(at) == district && CouncilColumnUse(at) == from; at = grid.Next(at))
                {
                    CellColumn col = grid.Columns[at];
                    _state.CouncilCells[CellRules.Key(col.Strip, col.Space)] = ZoneRules.CouncilColumn(col.Rows, to + 1);
                    done++;
                    if (street == null && LotOfColumn(at, out LotView lot))
                    {
                        string words = ConstructionGame.StreetWords(lot.Street);   // the street in words, as the Ledger paper prints it (never its id)
                        street = words.Length > 0 ? words : null;
                    }
                }
            }
            if (done == 0)
            {
                return;
            }
            _cellsFactsStamp++;
            Paper("reallife_grow_paper_rezoned", ConstructionText.Fill("reallife_grow_rezoned_name", "street", street ?? ConstructionText.T(DistrictKeyOf(district)), "use",
                ConstructionText.T(BuildCatalogue.UseKeys[to])), done);
            Logger?.Info("Construction: the council rezoned " + done + " free column(s) on " + (street ?? "?") + " in district " + district + " from " + (BuildUse)from + " to " + (BuildUse)to
                         + " on day " + day + " (demand "
                         + _demandNow[to] + " against " + _demandNow[from] + ").");
            Changed();
        }

        // True for a column of free land: for sale, nobody's, no building, no developer, no grown site.
        private bool IsFreeColumn(int column)
        {
            return LotOfColumn(column, out LotView lot) && FreeSpace(lot);
        }

        private int _cellsFactsStamp;

        /// <summary>Moves whenever the council's zoning of free land changes, so build mode reads its columns again.</summary>
        public int ZoningStamp => _cellsFactsStamp;
    }
}
