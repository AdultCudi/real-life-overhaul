using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Construction
{
    /// <summary>A place build mode marks on the map: a site under way, a development, a redevelopment.</summary>
    public struct BuildMark
    {
        public string Name;     // what it is, in words
        public string Detail;   // its stage and share done
        public float X, Z;      // where its marker stands
        public bool Mine;       // the player's
    }

    /// <summary>
    /// Build mode's side of the construction game (plans/build-mode.md): the district's grid of zoning cells over its lots, what
    /// each column is (for sale, the player's and how deep, painted, a building's, a site's, the developers'), cells bought and
    /// bought deeper through the Land tab's own purchase, a use painted (applied, or the City Hall request through AskZone), the
    /// designs that fit a footprint of cells, a placed design's warnings and estimate, the project made through the very calls the
    /// app's pages make (NewPlot, SetUse, SetDesign, SetParking), and the tools' calls (demolition, join, split, refit). The
    /// city's developers start only where the district wants their use, and the council weighs that demand on a rezoning.
    /// COST: nothing per frame; the grid is made once a load (and when the lots change), every other call is a press or a pick.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private CellGrid _cells;
        private int _cellsStamp = int.MinValue;
        private readonly Dictionary<string, int> _lotAt = new Dictionary<string, int>(1024, StringComparer.Ordinal);
        private readonly List<LotView> _footprint = new List<LotView>(8);
        private BuildMode _mode;

        /// <summary>Build mode's window and pointer, made on its first opening.</summary>
        public BuildMode Mode => _mode;

        /// <summary>The district's cells over its lots at this load (made again when the lots change); empty with no district.</summary>
        public CellGrid Cells
        {
            get
            {
                IReadOnlyList<LotView> lots = Lots;
                if (_cells == null || _cellsStamp != _lotsStamp)
                {
                    _cellsStamp = _lotsStamp;
                    _cells = CellGrid.Of(lots);
                    ReadHeld(_cells);
                    _lotAt.Clear();
                    for (int i = 0; i < lots.Count; i++)
                    {
                        _lotAt[lots[i].Id] = i;
                    }
                }
                return _cells;
            }
        }

        // The ground the plan keeps out of the squares, into the grid: read with the lots, so build mode draws and names it.
        private static void ReadHeld(CellGrid grid)
        {
            try
            {
                Action<List<HeldView>> held = BuildModeBridges.Held;
                if (held != null)
                {
                    held(grid.Held);
                }
            }
            catch (Exception e)
            {
                grid.Held.Clear();
                Faulty("Held", e);
            }
        }

        /// <summary>The plan's lot a column of cells is part of; false when it is gone.</summary>
        public bool LotOfColumn(int column, out LotView lot)
        {
            CellGrid grid = Cells;
            lot = default(LotView);
            if (column < 0 || column >= grid.Columns.Count || !_lotAt.TryGetValue(grid.Columns[column].LotId, out int at))
            {
                return false;
            }
            IReadOnlyList<LotView> lots = Lots;
            if (at >= lots.Count)
            {
                return false;
            }
            lot = lots[at];
            return true;
        }

        // A site of the player's bought short of its strip's depth: its ground cut to its shallowest column's rows.
        private LotView WithRows(string id, LotView lot)
        {
            LotFile own = _state != null ? _state.Lot(id) : null;
            if (own == null || string.IsNullOrEmpty(own.Rows))
            {
                return lot;
            }
            int stripRows = CellRules.RowsFor(lot.Depth);
            return CellRules.Trim(lot, CellRules.MinRows(own.Rows, own.Spaces, stripRows), stripRows);
        }

        /// <summary>The rows of a column the player has (0 for a column not theirs) and the site that holds it.</summary>
        public int RowsHeld(int column, out LotFile site)
        {
            site = null;
            if (!LotOfColumn(column, out LotView lot))
            {
                return 0;
            }
            site = Holding(lot.Id);
            if (site == null)
            {
                return 0;
            }
            CellColumn c = Cells.Columns[column];
            if (string.IsNullOrEmpty(site.Rows) || !Space(site.Id, out LotView first) || first.Strip != c.Strip)
            {
                return c.Rows;
            }
            return CellRules.RowsAt(site.Rows, c.Space - first.Index, c.Rows);
        }

        /// <summary>What a column is, for its cells' colours: who holds it, its zoning, a building's use, occupancy and fall, a site's share built.</summary>
        public BuildModeRules.ColumnFacts FactsOf(int column)
        {
            var f = new BuildModeRules.ColumnFacts { Use = -1, MainUse = -1, CouncilUse = -1 };
            if (!LotOfColumn(column, out LotView lot))
            {
                return f;
            }
            int lotClass = BuildCatalogue.Clamp(lot.Class, 0, BuildCatalogue.ClassCount - 1);
            f.MainUse = (int)BuildCatalogue.ClassUse[lotClass];
            f.Zones = LandRules.ZonesOf(lotClass);
            f.MineRows = RowsHeld(column, out LotFile site);
            f.Mine = site != null;
            f.Developer = DeveloperOn(lot.Id);
            f.Demolishing = Demolishing(lot);
            CellColumn col = Cells.Columns[column];
            f.Council = _state.CouncilCells.TryGetValue(CellRules.Key(col.Strip, col.Space), out string council) ? council : null;   // the council's zoning of the square
            f.CouncilUse = ZoneRules.CouncilUse(lot.Id, lot.Class);
            GrowFile grown = GrowOf(lot.Id);
            f.RunDown = grown != null && grown.Standing && grown.RunDown;
            if (f.Mine)
            {
                f.Zones = ZonesNow(site);
                f.Paint = site.Paint;
                f.PaintWaits = site.Paint > 0 && !LandRules.Zoned(f.Zones, (BuildUse)(site.Paint - 1));
                int at = SiteColumn(column, site);
                f.Squares = at >= 0 ? ZoneRules.Column(site.Cells, at) : null;   // each square's own zone (none of its own: the site's paint)
                f.Cleared = ClearedGround(site.Id);
                ProjectFile p = _state.OnLot(site.Id);
                f.HasProject = p != null;
                f.UnderWay = p != null && p.Stage == ProjectStage.Building;
                f.Progress = f.UnderWay ? Built(p) : 0;
                if (!f.UnderWay && ParkOf(lot.Id) != null)
                {
                    f.Mine = false;   // a car park of the player's stands on their land: their building, run in build mode and at its pay machine
                    f.Building = f.MyBuilding = true;
                    f.Use = f.Paint > 0 ? f.Paint - 1 : f.MainUse;
                    return f;
                }
                if (!f.UnderWay && !f.Cleared && LotTaken(lot.Id))
                {
                    f.Mine = false;   // a building of the player's stands on their land: their building, not bare land
                    f.Building = f.MyBuilding = true;
                    f.Use = f.Paint > 0 ? f.Paint - 1 : f.MainUse;
                    f.Vacant = BuildModeGame.Occupant(lot.Street, lot.Number, out bool empty, out _) && empty;
                }
                return f;
            }
            f.Free = FreeSpace(lot);
            f.Building = !f.Free && LotTaken(lot.Id);
            bool landmark = LandmarkHeld(lot.Id);
            if (landmark || lot.Kept)
            {
                // A landmark's lot (its site, whatever its works) or ground kept clear: held, whoever asks. The player's Land tab, the developers
                // and the car-park builders all ask LotTaken, which says so for both, so none of them can take it.
                f.Held = true;
                f.Kept = !landmark;
                f.Building = f.Free = false;
                return f;
            }
            CarParkFile builders = ParkOf(lot.Id);
            if (builders != null)
            {
                // A car park of the city's builders: planned, going up by its share built, or open.
                f.Building = true;
                f.Stands = true;
                f.MyBuilding = false;
                f.Use = f.MainUse;
                f.UnderWay = builders.Stage == CarParkStage.Building;
                SiteFacts yard = builders.SiteId > 0 ? SiteOf(builders.SiteId) : default(SiteFacts);
                f.Progress = yard.Known && yard.Hours > 0f ? (int)Math.Max(0f, Math.Min(100f, yard.Done * 100f / yard.Hours)) : 0;
                return f;
            }
            if (grown != null && grown.UnderWay && grown.Owner != GrowOwner.Player)
            {
                // A grown site: the developers' while it is planned, under construction by its share built once it goes up.
                f.UnderWay = grown.Stage == GrowStage.Building;
                SiteFacts works = grown.SiteId > 0 ? SiteOf(grown.SiteId) : default(SiteFacts);
                f.Progress = works.Known && works.Hours > 0f ? (int)Math.Max(0f, Math.Min(100f, works.Done * 100f / works.Hours)) : 0;
            }
            if (f.Building)
            {
                f.Stands = LotBuilt(lot.Id) || (grown != null && grown.Standing) || f.UnderWay;   // a building of the district stands there at this load, or is going up
                f.MyBuilding = ConstructionGame.Owns(lot.Street, lot.Number);
                f.Use = f.MainUse;
                DevFile dev = _state.Development(lot.Id);
                f.UnderWay = dev != null && dev.SiteId > 0 && !dev.Handed;
                f.Vacant = BuildModeGame.Occupant(lot.Street, lot.Number, out bool empty, out _) && empty;
            }
            return f;
        }

        // A building of the lot coming down: a demolition the player or the city has under way there.
        private bool Demolishing(LotView lot)
        {
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (p.Live && p.Kind == ProjectKind.Demolition && (p.Lot == lot.Id || (p.Number == lot.Number && p.Street == lot.Street)))
                {
                    return true;
                }
            }
            return false;
        }

        private int Built(ProjectFile p)
        {
            _trackFacts = _trackFacts ?? new TrackFacts(this);
            return _trackFacts.Percent(p);
        }

        /// <summary>Each column's land value step among the district's prices (BuildModeRules.ValueStepOf), into <paramref name="into"/>; worked once a view needs it.</summary>
        public void ValueSteps(int[] into)
        {
            CellGrid grid = Cells;
            long least = long.MaxValue, most = 0L;
            for (int c = 0; c < grid.Columns.Count && c < into.Length; c++)
            {
                long price = LotOfColumn(c, out LotView lot) ? LotPrice(lot) : 0L;
                into[c] = price > long.MaxValue / 8 ? -1 : (int)Math.Min(int.MaxValue, price / Math.Max(1, grid.Columns[c].Rows));
                if (price > 0L)
                {
                    least = Math.Min(least, into[c]);
                    most = Math.Max(most, into[c]);
                }
            }
            for (int c = 0; c < grid.Columns.Count && c < into.Length; c++)
            {
                into[c] = BuildModeRules.ValueStepOf(into[c], least == long.MaxValue ? 0L : least, most);
            }
        }

        /// <summary>One cell in words for the pointer: its address and row, who holds it, its zoning, the height limit and a cell's price.</summary>
        public string CellWords(int column, int row)
        {
            CellGrid grid = Cells;
            if (column < 0 || column >= grid.Columns.Count || !LotOfColumn(column, out LotView lot))
            {
                return null;
            }
            BuildModeRules.ColumnFacts f = FactsOf(column);
            if (f.Held)
            {
                HeldColumnCard(lot, f, out string heldHead, out string heldWhy, out _);
                return heldHead + ". " + heldWhy;
            }
            string holder;
            if (f.Mine && row < f.MineRows)
            {
                holder = ConstructionText.T("reallife_bld_mode_cell_yours");
            }
            else if (f.Building)
            {
                holder = f.MyBuilding ? ConstructionText.T("reallife_bld_mode_cell_your_building")
                    : BuildModeGame.Occupant(lot.Street, lot.Number, out _, out string who) && !string.IsNullOrEmpty(who) ? who : ConstructionText.T("reallife_bld_mode_key_taken");
            }
            else if (f.Free || f.Mine)
            {
                holder = ConstructionText.T("reallife_bld_mode_key_forsale");
            }
            else
            {
                holder = ConstructionText.T("reallife_bld_mode_cell_kept");
            }
            if (f.Developer)
            {
                holder += ", " + ConstructionText.T("reallife_bld_mode_mark_developer");
            }
            long price = LotPrice(lot);
            string cell = price > 0L ? ConstructionText.Money(CellRules.ColumnPrice(price, 1, grid.Columns[column].Rows)) : "-";
            int zones = BuildModeRules.ZonesAt(f, row);
            return ConstructionText.Fill("reallife_bld_mode_cell", "address", LotWords(lot.Id), "row", ConstructionText.N(row + 1), "holder", holder, "uses", UsesWords(zones),
                "storeys", ConstructionText.N(UptownStoreys()), "price", cell);
        }

        /// <summary>
        /// One cell as the tooltip beside the pointer says it: who holds it and where, the council's zoning with the height limit, and
        /// a cell's price or the use painted on the player's own land (and whether City Hall is asked for it). False off the grid.
        /// </summary>
        public bool HoverCard(int column, int row, out string head, out string zoning, out string more)
        {
            head = zoning = more = null;
            CellGrid grid = Cells;
            if (column < 0 || column >= grid.Columns.Count || !LotOfColumn(column, out LotView lot))
            {
                return false;
            }
            BuildModeRules.ColumnFacts f = FactsOf(column);
            if (f.Held)
            {
                HeldColumnCard(lot, f, out head, out zoning, out more);
                return true;
            }
            string holder;
            if (f.Mine && row < f.MineRows)
            {
                holder = ConstructionText.T("reallife_bld_mode_cell_yours");
            }
            else if (f.Building)
            {
                holder = f.MyBuilding ? ConstructionText.T("reallife_bld_mode_cell_your_building")
                    : BuildModeGame.Occupant(lot.Street, lot.Number, out _, out string who) && !string.IsNullOrEmpty(who) ? who : ConstructionText.T("reallife_bld_mode_key_taken");
            }
            else if (f.Free || f.Mine)
            {
                holder = ConstructionText.T("reallife_bld_mode_key_forsale");
            }
            else
            {
                holder = ConstructionText.T("reallife_bld_mode_cell_kept");
            }
            head = ConstructionText.Fill("reallife_bld_hover_head", "holder", holder, "address", LotWords(lot.Id), "district", ConstructionText.T(DistrictKeyOf(lot.District)));
            int council = BuildModeRules.ZonesAt(f, row);   // the square's own: a paint in force, the council's use of free land, else the site's
            zoning = ConstructionText.Fill("reallife_bld_hover_zoning", "uses", UsesWords(council), "storeys", ConstructionText.N(UptownStoreys()));
            int squarePaint = f.Mine && row < f.MineRows ? BuildModeRules.PaintAt(f, row) : 0;
            if (squarePaint > 0)
            {
                string use = ConstructionText.T(BuildCatalogue.UseKeys[Math.Min((int)BuildUse.Count - 1, squarePaint - 1)]);
                bool waits = !LandRules.Zoned(f.Zones, (BuildUse)(squarePaint - 1));
                more = ConstructionText.Fill(waits ? "reallife_bld_hover_painted_waits" : "reallife_bld_hover_painted", "use", use);
            }
            else if (f.Free || (f.Mine && row >= f.MineRows))
            {
                long price = LotPrice(lot);
                more = price > 0L ? ConstructionText.Fill("reallife_bld_hover_price", "price", ConstructionText.Money(CellRules.ColumnPrice(price, 1, grid.Columns[column].Rows))) : null;
            }
            if (f.Developer)
            {
                more = (more != null ? more + ". " : string.Empty) + ConstructionText.T("reallife_bld_mode_mark_developer");
            }
            if (f.RunDown)
            {
                more = (more != null ? more + ". " : string.Empty) + ConstructionText.T("reallife_grow_hover_rundown");
            }
            string park = ParkHover(lot.Id);
            if (park != null)
            {
                more = (more != null ? more + ". " : string.Empty) + park;
            }
            return true;
        }

        // The card of a square held for a landmark or kept clear: whose it is and what stands (the landmark's own words, "Planned: its lots are held
        // for it"), and that it is not for sale.
        private void HeldColumnCard(LotView lot, BuildModeRules.ColumnFacts f, out string head, out string zoning, out string more)
        {
            string site = f.Kept ? null : LandmarkSiteOf(lot.Id);
            string name = site != null ? LandmarkName(site) : null;
            string holder = !string.IsNullOrEmpty(name) ? name : ConstructionText.T(f.Kept ? "reallife_bld_held_kept_name" : "reallife_bld_held_landmark_name");
            head = ConstructionText.Fill("reallife_bld_hover_head", "holder", holder, "address", LotWords(lot.Id), "district", ConstructionText.T(DistrictKeyOf(lot.District)));
            string words = site != null ? LandmarkWords(site) : null;
            zoning = f.Kept ? ConstructionText.T("reallife_bld_held_kept") : !string.IsNullOrEmpty(words) ? words : ConstructionText.T("reallife_bld_held_landmark");
            more = ConstructionText.T("reallife_bld_mode_cell_kept");   // "Not for sale"
        }

        internal static string LandmarkSiteOf(string lot)
        {
            try
            {
                Func<string, string> of = BuildModeBridges.LandmarkOf;
                return of != null && lot != null ? of(lot) : null;
            }
            catch (Exception e)
            {
                Faulty("LandmarkOf", e);
                return null;
            }
        }

        /// <summary>
        /// The card of a held ground no square covers (a special place's, a park's, the campus's, the district's edge): what holds it and
        /// that it is not for sale. False for an index out of the grid's held grounds.
        /// </summary>
        public bool HeldCard(int index, out string head, out string zoning, out string more)
        {
            head = zoning = more = null;
            CellGrid grid = Cells;
            if (index < 0 || index >= grid.Held.Count)
            {
                return false;
            }
            HeldView h = grid.Held[index];
            string holder = !string.IsNullOrEmpty(h.Name) ? h.Name : ConstructionText.T(h.NameKey ?? "reallife_bld_held_place_name");
            string district = ConstructionText.T(DistrictKeyOf(h.District));
            head = h.Street != null
                ? ConstructionText.Fill("reallife_bld_hover_head", "holder", holder, "address", ConstructionGame.AddressText(h.Street, h.Number), "district", district)
                : ConstructionText.Fill("reallife_bld_hover_held_head", "holder", holder, "district", district);
            zoning = ConstructionText.T(HeldKeys[(int)h.Kind] + (h.Kind == HeldWhat.Place && !h.Stands ? "_down" : string.Empty));
            more = ConstructionText.T("reallife_bld_mode_cell_kept");
            return true;
        }

        // What each held ground says it is: a place's ("a special place of the district", with "_down" when its building did not stand), a park's, the campus's, the edge's.
        private static readonly string[] HeldKeys = { "reallife_bld_held_place", "reallife_bld_held_park", "reallife_bld_held_campus", "reallife_bld_held_edge" };

        /// <summary>The working days a design's building takes at the city's own pace under the code in force (what the builders bid around).</summary>
        public int PlaceDays(DesignChoice design, BuildUse use)
        {
            int storeys = PlansRules.Storeys(design.Height);
            int hours = PlansRules.Hours(use, Math.Max(1, design.TotalSqm), storeys, ProjectKind.Plot);
            return CommissionRules.UsualDays(hours, Permits());
        }

        /// <summary>A zoning's uses in words, joined by commas ("-" for none).</summary>
        public static string UsesWords(int zones)
        {
            var words = new List<string>(4);
            for (int u = 0; u < (int)BuildUse.Count; u++)
            {
                if (LandRules.Zoned(zones, (BuildUse)u))
                {
                    words.Add(ConstructionText.T(BuildCatalogue.UseKeys[u]));
                }
            }
            return words.Count == 0 ? "-" : string.Join(", ", words.ToArray());
        }

        // ------------------------------------------------------------------ the districts' demand

        /// <summary>A district's name as a locale key.</summary>
        public static string DistrictKeyOf(int district) => BuildCatalogue.DistrictKeys[Math.Max(0, Math.Min(BuildCatalogue.Districts - 1, district))];

        /// <summary>The district a lot is in (0 Uptown, 1 Cedar Heights), Uptown for one not there.</summary>
        public int DistrictOfLot(string lotId)
        {
            return Lot(lotId, out LotView lot) ? lot.District : 0;
        }

        /// <summary>The district the lot a column of cells is part of is in.</summary>
        public int DistrictOfColumn(int column)
        {
            return LotOfColumn(column, out LotView lot) ? lot.District : 0;
        }

        /// <summary>A district's demand for a use, 0 to 100, or -1 while it is not known (a Going Public session, the district not counted).</summary>
        public int DemandFor(BuildUse use, int district)
        {
            Func<int, int, int> ask = BuildModeBridges.Demand;
            if (ask == null || Down("Demand"))
            {
                return -1;
            }
            try
            {
                return ask((int)use, district);
            }
            catch (Exception e)
            {
                Faulty("Demand", e);
                return -1;
            }
        }

        /// <summary>The demand for a use in the district a lot is in.</summary>
        public int DemandFor(BuildUse use, string lotId) => DemandFor(use, DistrictOfLot(lotId));

        // The developers start a building only where its use is wanted in the lot's district (as before where demand is not known).
        private bool DemandWants(BuildUse use, int district) => BuildModeRules.DeveloperMay(DemandFor(use, district));

        // A rezoning's odds lifted by the demand for the use in the lot's district.
        private int DemandLift(BuildUse use, int district) => BuildModeRules.CouncilLift(DemandFor(use, district));

        // ------------------------------------------------------------------ buying cells

        /// <summary>
        /// The price of the cells of a site picked (each free column's rows at its space's price, each of the player's columns its rows
        /// more), and why it cannot be bought now (a text key) or null.
        /// </summary>
        public string CellsBlock(CellSite site, out long price, out int freeFirst, out int freeCount, out LotFile mine)
        {
            price = 0L;
            freeFirst = -1;
            freeCount = 0;
            mine = null;
            if (GoingPublic.InSession)
            {
                return "reallife_bld_answer_session_land";
            }
            CellGrid grid = Cells;
            bool deeper = false;
            int at = site.FirstColumn;
            for (int i = 0; i < site.Count; i++, at = grid.Next(at))
            {
                if (at < 0 || !LotOfColumn(at, out LotView lot))
                {
                    return "reallife_bld_answer_no_lot";
                }
                CellColumn c = grid.Columns[at];
                int had = RowsHeld(at, out LotFile own);
                long spacePrice = LotPrice(lot);
                if (spacePrice <= 0L)
                {
                    return "reallife_bld_answer_no_price";
                }
                if (own != null)
                {
                    if (mine != null && mine != own)
                    {
                        return "reallife_bld_mode_refuse_two_sites";
                    }
                    if (freeCount > 0 && freeFirst + freeCount != i)
                    {
                        return "reallife_bld_mode_refuse_mixed";
                    }
                    mine = own;
                    if (site.Rows[i] > had)
                    {
                        if (_state.OnLot(own.Id) != null)
                        {
                            return "reallife_bld_answer_lot_busy";
                        }
                        deeper = true;
                        price += CellRules.DeeperPrice(spacePrice, had, site.Rows[i], c.Rows);
                    }
                    continue;
                }
                if (!FreeSpace(lot))
                {
                    return "reallife_bld_answer_lot_taken";
                }
                if (freeCount > 0 && freeFirst + freeCount != i)
                {
                    return "reallife_bld_mode_refuse_mixed";   // free cells on both sides of the player's: two buys
                }
                if (freeCount == 0)
                {
                    freeFirst = i;
                }
                freeCount++;
                price += CellRules.ColumnPrice(spacePrice, site.Rows[i], c.Rows);
            }
            if (freeCount == 0 && !deeper)
            {
                return "reallife_bld_mode_refuse_owned";
            }
            if (freeCount > 0 && _state.Lots.Count >= BuildCatalogue.LotsMost)
            {
                return "reallife_bld_answer_too_many_lots";
            }
            if (mine != null && mine.Spaces + freeCount > BuildCatalogue.SiteSpacesMost)
            {
                return "reallife_bld_answer_join_too_wide";
            }
            return price <= 0L ? "reallife_bld_answer_no_price" : null;
        }

        /// <summary>
        /// The cells of a site picked bought, at their price, by the Land tab's own transaction: free columns as a site of their own
        /// (joined to the player's site beside them), the player's columns deeper. Returns a text key.
        /// </summary>
        public string BuyCells(CellSite site, bool free = false)
        {
            string block = CellsBlock(site, out long price, out int freeFirst, out int freeCount, out LotFile mine);
            if (block != null)
            {
                return block;
            }
            CellGrid grid = Cells;
            if (!free && !ConstructionGame.Take(price, "reallife_transaction_bld_land", LotWords(grid.Columns[site.FirstColumn].LotId), false))
            {
                return "reallife_bld_answer_no_money";
            }
            int day = ConstructionGame.Day;
            string newId = null;
            int at = site.FirstColumn;
            var rows = new int[Math.Max(1, freeCount)];
            long freePaid = 0L;
            for (int i = 0; i < site.Count; i++, at = grid.Next(at))
            {
                LotOfColumn(at, out LotView lot);
                CellColumn c = grid.Columns[at];
                int had = RowsHeld(at, out LotFile own);
                long spacePrice = LotPrice(lot);
                if (own != null)
                {
                    if (site.Rows[i] > had)
                    {
                        long more = CellRules.DeeperPrice(spacePrice, had, site.Rows[i], c.Rows);
                        own.Paid += more;
                        own.Rows = Deepened(own, c, site.Rows[i]);
                    }
                    continue;
                }
                if (newId == null)
                {
                    newId = lot.Id;
                }
                rows[i - freeFirst] = site.Rows[i];
                freePaid += CellRules.ColumnPrice(spacePrice, site.Rows[i], c.Rows);
            }
            if (newId != null)
            {
                SiteSpaces(newId, freeCount, _strip);
                LotView joined = Joined(_strip);
                var bought = new LotFile
                {
                    Id = newId, Bought = day, Paid = freePaid, Zones = LandRules.ZonesOf(joined.Class), TaxedTo = day, EmptySince = day, Spaces = freeCount,
                    Rows = CellRules.RowsText(rows, freeCount, grid.Columns[site.FirstColumn].Rows)
                };
                _state.Lots.Add(bought);
                TakeCouncilZoning(bought);   // the council's zoning of the squares comes with them
                Logger?.Info("Construction: build mode bought " + site.Cells + " cell(s) in " + site.Count + " space(s) from " + newId + " for " + price + ".");
            }
            if (mine != null && newId != null)
            {
                string first = freeFirst == 0 ? newId : mine.Id;   // the lower site first: it joins the one after it
                string joinedKey = Join(first);
                if (joinedKey != "reallife_bld_answer_joined")
                {
                    Logger?.Info("Construction: build mode's cells bought beside the site at " + mine.Id + " stay a site of their own (" + joinedKey + ").");
                }
            }
            Changed();
            return "reallife_bld_mode_bought";
        }

        // A site's digits with one of its columns bought deeper.
        private string Deepened(LotFile own, CellColumn c, int rows)
        {
            if (!Space(own.Id, out LotView first))
            {
                return own.Rows;
            }
            var all = new int[own.Spaces];
            for (int i = 0; i < own.Spaces; i++)
            {
                all[i] = CellRules.RowsAt(own.Rows, i, c.Rows);
            }
            int at = c.Space - first.Index;
            if (at >= 0 && at < all.Length)
            {
                all[at] = Math.Max(all[at], rows);
            }
            return CellRules.RowsText(all, all.Length, c.Rows);
        }

        // ------------------------------------------------------------------ zones

        /// <summary>A use painted on a site of the player's (-1 for none): applied when its zones allow it, else put to the council at City Hall. Returns a text key.</summary>
        public string PaintSite(string siteId, int use)
        {
            if (GoingPublic.InSession)
            {
                return "reallife_bld_answer_session_land";
            }
            LotFile own = _state.Lot(siteId);
            if (own == null)
            {
                return "reallife_bld_answer_no_lot";
            }
            if (use < 0 || use >= (int)BuildUse.Count)
            {
                own.Paint = 0;
                own.Cells = string.Empty;   // every square takes the site's paint again
                Changed();
                return "reallife_bld_mode_painted_none";
            }
            if (LandRules.Zoned(ZonesNow(own), (BuildUse)use))
            {
                own.Paint = BuildModeRules.PaintOf(use);
                own.Cells = string.Empty;   // the whole site painted: every square takes it
                ProjectFile p = _state.OnLot(siteId);
                if (p != null && p.Stage == ProjectStage.Design && p.Kind == ProjectKind.Plot && p.Use != (BuildUse)use)
                {
                    SetUse(p, (BuildUse)use);   // the plans in hand take the use painted, as the app's use row does
                }
                Changed();
                return "reallife_bld_mode_painted";
            }
            string asked = AskZone(siteId, (BuildUse)use);
            if (asked == "reallife_bld_answer_zone_asked")
            {
                own.Paint = BuildModeRules.PaintOf(use);   // shown as waiting until the council decides
                own.Cells = string.Empty;
                Changed();
                return "reallife_bld_mode_paint_asked";
            }
            return asked;
        }

        // ------------------------------------------------------------------ placing a building

        // A footprint's spaces as one lot, cut to the rows given.
        private bool FootprintSite(int firstColumn, int count, int rows, out LotView site)
        {
            site = default(LotView);
            _footprint.Clear();
            CellGrid grid = Cells;
            int at = firstColumn;
            for (int i = 0; i < count; i++, at = grid.Next(at))
            {
                if (at < 0 || !LotOfColumn(at, out LotView lot))
                {
                    return false;
                }
                if (_footprint.Count == 0 || _footprint[_footprint.Count - 1].Id != lot.Id)
                {
                    _footprint.Add(lot);
                }
            }
            site = Joined(_footprint);
            site = CellRules.Trim(site, rows, grid.Columns[firstColumn].Rows);
            return true;
        }

        /// <summary>
        /// The designs that fit a footprint of cells for a use: at a block's corner the corner buildings that stand on it first (the
        /// owner's seven, Scripts/Corners), then the designs of the city's own that fill it, the best covering first; false while they
        /// are being looked through.
        /// </summary>
        public bool FootprintDesigns(int firstColumn, int count, int rows, BuildUse use, List<DesignChoice> into)
        {
            into.Clear();
            if (!FootprintSite(firstColumn, count, rows, out LotView site))
            {
                return true;
            }
            bool ready = true;
            if (CellRules.Corner(Cells, firstColumn, count, out bool low, out float edge))
            {
                ready = CornerDesigns(site, low, edge, use, into);   // a corner site over two streets: the corner buildings that fit it
            }
            int corners = into.Count;
            SiteGround(site, out float width, out float depth);
            ready &= ReadDesigns(width, depth, BuildCatalogue.UseTypes[(int)use], DesignsAsked, _cityDesigns);
            KeepFitting(site, _cityDesigns);
            into.AddRange(_cityDesigns);
            _cityDesigns.Clear();
            Logger?.Info("Construction: build mode's designs for " + count + " space(s) by " + rows + " row(s) from " + site.Id + ": " + corners + " corner building(s), "
                         + (into.Count - corners) + " of the city's.");
            return ready;
        }

        private readonly List<DesignChoice> _cityDesigns = new List<DesignChoice>(32);

        /// <summary>A placed design's warnings (BuildModeRules' bits), its estimate and the storeys the code allows.</summary>
        public int PlaceWarnings(int firstColumn, int count, DesignChoice design, bool hasDesign, BuildUse use, bool parkingUnder, out long estimate, out int limit)
        {
            estimate = 0L;
            CellGrid grid = Cells;
            var ask = new PlaceAsk { Session = GoingPublic.InSession, HasDesign = hasDesign, Columns = count, ParkingUnder = parkingUnder, Mine = true, RowsHad = int.MaxValue };
            LotFile site = null;
            int at = firstColumn;
            for (int i = 0; i < count; i++, at = at >= 0 ? grid.Next(at) : -1)
            {
                LotFile holder = null;
                int had = at >= 0 ? RowsHeld(at, out holder) : 0;
                if (holder == null || (site != null && holder != site))
                {
                    ask.Mine = false;
                    ask.RowsHad = 0;
                    continue;
                }
                site = holder;
                ask.SiteColumns++;
                ask.RowsHad = Math.Min(ask.RowsHad, had);
            }
            if (ask.RowsHad == int.MaxValue)
            {
                ask.RowsHad = 0;
            }
            CellColumn first = firstColumn >= 0 && firstColumn < grid.Columns.Count ? grid.Columns[firstColumn] : null;
            ask.RowsNeeded = first != null ? CellRules.RowsNeeded(design.Depth, first.Depth / first.Rows) : int.MaxValue;
            ask.Zoned = site != null && LandRules.Zoned(ZonesNow(site), use);
            int code = Permits();
            limit = PlansRules.HeightLimit(string.Empty, true, code);
            if (hasDesign)
            {
                ask.Storeys = PlansRules.Storeys(design.Height);
                ask.Limit = limit;
                int area = Math.Max(1, design.TotalSqm);
                ask.ParkingNeeded = PlansRules.ParkingNeeded(use, area, code);
                var probe = new ProjectFile
                {
                    Kind = ProjectKind.Plot, Use = use, Area = area, Storeys = ask.Storeys, ParkingUnder = parkingUnder,
                    District = site != null ? DistrictKeyOf(DistrictOfLot(site.Id)) : string.Empty, Lot = site != null ? site.Id : string.Empty
                };
                estimate = Preview(probe, out _, out _, out _, out _).Total;
                ask.ParkingUnder |= ask.ParkingNeeded > 0 && ParkSpare(probe) >= ask.ParkingNeeded;   // a car park of the player's near it counts as the parking asked
            }
            return BuildModeRules.Warnings(ask);
        }

        /// <summary>
        /// A building of a design placed on a footprint of the player's cells: the footprint made its own site by the Land tab's split
        /// and join where it is not the whole site, then the project made, its use, design and parking chosen, through the app's own
        /// calls. Returns the project's id, or 0 with why.
        /// </summary>
        public int PlaceProject(int firstColumn, int count, DesignChoice design, BuildUse use, bool parkingUnder, out string why)
        {
            why = null;
            if (GoingPublic.InSession)
            {
                why = "reallife_bld_answer_session_land";
                return 0;
            }
            int warnings = PlaceWarnings(firstColumn, count, design, true, use, parkingUnder, out _, out _);
            if (!BuildModeRules.MayBuild(warnings))
            {
                why = BuildModeRules.WarnKeys[FirstBit(warnings & BuildModeRules.Stops)];
                return 0;
            }
            RowsHeld(firstColumn, out LotFile site);
            if (!LotOfColumn(firstColumn, out LotView firstLot) || site == null)
            {
                why = "reallife_bld_answer_no_lot";
                return 0;
            }
            if (_state.OnLot(site.Id) != null)
            {
                why = "reallife_bld_answer_lot_busy";
                return 0;
            }
            string siteId = site.Id;
            if (site.Spaces != count || siteId != firstLot.Id)
            {
                string split = Split(siteId);   // the footprint made a site of its own: its columns joined back after the split
                if (split != "reallife_bld_answer_split")
                {
                    why = split;
                    return 0;
                }
                siteId = firstLot.Id;
                for (int i = 1; i < count; i++)
                {
                    string joined = Join(siteId);
                    if (joined != "reallife_bld_answer_joined")
                    {
                        why = joined;
                        return 0;
                    }
                }
            }
            int id = NewPlot(siteId, out why);
            ProjectFile p = _state.Project(id);
            if (p == null)
            {
                return 0;
            }
            string used = p.Use != use ? SetUse(p, use) : null;
            if (used != null)
            {
                why = used;
                return id;
            }
            if (!string.IsNullOrEmpty(design.Look))
            {
                // A corner building: its model over the design of the city's that fits the ground it leaves (offered on this footprint
                // alone, so never among those that fill the whole site), worn as the design list's pick is.
                Wear(p, design);
            }
            else if (DesignChoices(p, _choices))
            {
                for (int i = 0; i < _choices.Count; i++)
                {
                    if (_choices[i].Name == design.Name && _choices[i].Size == design.Size)
                    {
                        SetDesign(p, i);
                        break;
                    }
                }
            }
            SetParking(p, parkingUnder);
            Logger?.Info("Construction: build mode placed " + (string.IsNullOrEmpty(design.Look) ? design.Name : design.Look + " over " + design.Name) + " (" + use + ", "
                         + PlansRules.Storeys(design.Height) + " storeys) on " + count + " space(s) from " + siteId + ": project " + id + ".");
            Changed();
            return id;
        }

        private static int FirstBit(int bits)
        {
            for (int i = 0; i < BuildModeRules.WarnCount; i++)
            {
                if ((bits & (1 << i)) != 0)
                {
                    return i;
                }
            }
            return 0;
        }

        // ------------------------------------------------------------------ the marks on the map

        /// <summary>The sites under way (the player's projects with a place, the developers' and the city's redevelopments) as marks, into <paramref name="into"/> (cleared).</summary>
        public void Marks(List<BuildMark> into)
        {
            into.Clear();
            _trackFacts = _trackFacts ?? new TrackFacts(this);
            for (int i = 0; i < _state.Projects.Count; i++)
            {
                ProjectFile p = _state.Projects[i];
                if (!p.Live || !PlaceOf(p, out MapPlace place))
                {
                    continue;
                }
                UnityEngine.Vector3 at = Where(place);
                string detail = ConstructionText.StageWords(p) + (p.Stage == ProjectStage.Building ? ", " + _trackFacts.Percent(p) + "%" : string.Empty);
                into.Add(new BuildMark { Name = ProjectName(p), Detail = detail, X = at.x, Z = at.z, Mine = true });
            }
            for (int i = 0; i < _state.Developments.Count; i++)
            {
                DevFile dev = _state.Developments[i];
                if (dev.SiteId > 0 && !dev.Handed && PlaceOfLot(dev.Lot, out MapPlace place))
                {
                    UnityEngine.Vector3 at = Where(place);
                    into.Add(new BuildMark { Name = LotWords(dev.Lot), Detail = ConstructionText.T("reallife_bld_mode_mark_developer"), X = at.x, Z = at.z });
                }
            }
            for (int i = 0; i < _state.CityRedevs.Count; i++)
            {
                RedevFile r = _state.CityRedevs[i];
                if (r.Live && PlaceOfLot(r.Lot, out MapPlace place))
                {
                    UnityEngine.Vector3 at = Where(place);
                    into.Add(new BuildMark { Name = ConstructionGame.AddressText(r.Street, r.Number), Detail = ConstructionText.T("reallife_bld_mode_mark_redevelop"), X = at.x, Z = at.z });
                }
            }
            for (int i = 0; i < _state.Grows.Count; i++)
            {
                GrowFile g = _state.Grows[i];
                if (g.UnderWay && g.Owner != GrowOwner.Player && PlaceOfLot(g.Lot, out MapPlace place))
                {
                    UnityEngine.Vector3 at = Where(place);
                    into.Add(new BuildMark { Name = GrowName(g), Detail = GrowStageWords(g), X = at.x, Z = at.z });   // the city's growth, its stage
                }
            }
        }

        /// <summary>What a contractor's refit of a unit of the player's costs today (the city's best renovation contractor's quote), or 0.</summary>
        public long RefitPrice(string street, int number)
        {
            return ConstructionGame.Unit(street, number, out UnitView u) ? QuoteFor(u).Price : 0L;
        }

        // ------------------------------------------------------------------ the city's own districts (build mode's plots)

        private readonly List<SiteFacts> _works = new List<SiteFacts>(64);

        /// <summary>Uptown's lots' addresses (street + '#' + number) into the caller's set (cleared): left to its squares, never plots.</summary>
        public void UptownAddresses(HashSet<string> into)
        {
            into.Clear();
            IReadOnlyList<LotView> lots = Lots;
            for (int i = 0; i < lots.Count; i++)
            {
                if (!string.IsNullOrEmpty(lots[i].Street))
                {
                    into.Add(lots[i].Street + "#" + lots[i].Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        /// <summary>
        /// The works under way at the city's buildings (street + '#' + number to the share done, 0 to 100, and whether the player's)
        /// into the caller's maps (cleared): every site of the city's programme at a building (the player's rebuilds and refits, the
        /// works a rival builder won, the city's own), so the plots show the NPCs' works as the player's. Once at an opening.
        /// </summary>
        public void CityWorks(Dictionary<string, int> share, HashSet<string> mine)
        {
            share.Clear();
            mine.Clear();
            Action<List<SiteFacts>> all = ConstructionBridges.AllSites;
            if (all == null)
            {
                return;
            }
            all(_works);
            for (int i = 0; i < _works.Count; i++)
            {
                SiteFacts f = _works[i];
                if (!f.Known || string.IsNullOrEmpty(f.Street) || f.Number <= 0)
                {
                    continue;
                }
                string key = f.Street + "#" + f.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                share[key] = f.Hours <= 0f ? 100 : (int)Math.Max(0f, Math.Min(100f, f.Done * 100f / f.Hours));
                if (f.Mine || (_state != null && _state.OnSite(f.Id) != null))
                {
                    mine.Add(key);
                }
            }
        }

        // ------------------------------------------------------------------ opening

        /// <summary>Build mode opened over Uptown from the Construction app (the city map, time paused). Returns why not as a text key, or null.</summary>
        public string OpenBuildMode()
        {
            if (!Settings.ConstructionBuildMode)
            {
                return "reallife_bld_mode_off";
            }
            if (!Running)
            {
                return "reallife_bld_mode_not_running";
            }
            if (Cells.Columns.Count == 0)
            {
                return "reallife_bld_mode_no_district";
            }
            if (_mode == null)
            {
                BuildMode.Logger = Logger;
                BuildModeGame.Logger = Logger;
                _mode = gameObject.AddComponent<BuildMode>();
            }
            return _mode.Open(this) ? null : "reallife_bld_mode_no_map";
        }
    }
}
