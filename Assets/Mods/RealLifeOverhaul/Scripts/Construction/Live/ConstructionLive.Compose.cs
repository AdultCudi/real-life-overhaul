using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;

namespace RealLifeOverhaul.Construction
{
    /// <summary>The page composed from what the old pages' writers put in the box and the rows: a tab's table with its card, or the page of the thing picked.</summary>
    public sealed partial class ConstructionLive
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private void Compose(ConstructionModel m, BuildState state, string selected)
        {
            if (selected != null && _box.HasContent)
            {
                m.Shape = ConstructionShape.Detail;
                m.DetailIcon = TabIcons[_tab];
                m.Back = ConstructionText.T("reallife_bld_back");
                _box.Finish(m);
                m.DetailTable = _detailTable;
                m.RowKeys = _detailKeys;
                Project(m, state, selected);
                return;
            }
            if (_tab == ConstructionDirector.TabOverview)
            {
                ComposeOverview(m, state);
                return;
            }
            m.Shape = ConstructionShape.Table;
            m.HeadIcon = TabIcons[_tab];
            m.HeadTitle = ConstructionText.T(TabKeys[_tab]);
            m.HeadSub = Headline(state);
            Figures(m, state);
            m.Table = TableOf(m);
            m.Card = _box.Card(null);
            if (_tab == ConstructionDirector.TabLand)
            {
                m.Foot = AsksCard(state);
            }
        }

        private string Headline(BuildState state)
        {
            string name = Director.Office(out PlaceView office) && state.Firm.Founded > 0 ? office.Name : ConstructionText.T("reallife_bld_app");
            return ConstructionText.T("reallife_bld_headline", "name", name, "reputation", state.Firm.Reputation.ToString(Inv), "sites", state.Building().ToString(Inv));
        }

        // A project's page opens with its stage track over the cards: where the works stand is the first thing its page says.
        private void Project(ConstructionModel m, BuildState state, string selected)
        {
            if (!selected.StartsWith("prj:", StringComparison.Ordinal) || !int.TryParse(selected.Substring(4), NumberStyles.Integer, Inv, out int id))
            {
                return;
            }
            ProjectFile p = state.Project(id);
            if (p == null)
            {
                return;
            }
            m.Track = StageTrack(p);
        }

        private static readonly string[] StepKeys =
        {
            "reallife_ui_cons_step_design", "reallife_ui_cons_step_planning", "reallife_ui_cons_step_ready", "reallife_ui_cons_step_building", "reallife_ui_cons_step_done"
        };

        private static readonly UiIcon[] StepIcons = { UiIcon.Pencil, UiIcon.Scales, UiIcon.Key, UiIcon.Hammer, UiIcon.Flag };

        private UiTrackModel StageTrack(ProjectFile p)
        {
            int at = Math.Min((int)p.Stage, StepKeys.Length - 1);
            var steps = new UiTrackStep[StepKeys.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                steps[i] = new UiTrackStep
                {
                    Icon = StepIcons[i], Label = ConstructionText.T(StepKeys[i]), Note = i == at ? StepNote(p) : string.Empty,
                    State = p.Stage == ProjectStage.Abandoned ? UiStepState.Later : i < at || p.Stage == ProjectStage.Finished
                        ? UiStepState.Done : i == at ? UiStepState.Now : i == at + 1 ? UiStepState.Next : UiStepState.Later
                };
            }
            return new UiTrackModel
            {
                Title = ConstructionText.T("reallife_ui_cons_track"), Line = ConstructionText.StageWords(p), Steps = steps,
                StateWords = new[] { ConstructionText.T("reallife_ui_cons_state_done"), ConstructionText.T("reallife_ui_cons_state_now"),
                    ConstructionText.T("reallife_ui_cons_state_next"),
                    ConstructionText.T("reallife_ui_cons_state_later"), ConstructionText.T("reallife_ui_cons_state_later") }
            };
        }

        // What the step in hand is waiting on, in a few words: the due day of the works, the day a decision is given.
        private string StepNote(ProjectFile p)
        {
            switch (p.Stage)
            {
                case ProjectStage.Planning when p.DecideDay > 0:
                    return ConstructionText.Day(p.DecideDay);
                case ProjectStage.Building when p.DueDay > 0:
                    return ConstructionText.T("reallife_ui_cons_due", "day", ConstructionText.Day(p.DueDay));
                case ProjectStage.Finished when p.Handed > 0:
                    return ConstructionText.Day(p.Handed);
                default:
                    return string.Empty;
            }
        }

        // ------------------------------------------------------------------ the figures of a tab's head

        private void Figures(ConstructionModel m, BuildState state)
        {
            FirmFile f = state.Firm;
            var labels = new List<string>(4);
            var values = new List<string>(4);
            switch (_tab)
            {
                case ConstructionDirector.TabLand:
                {
                    int sale = 0, mine = 0;
                    for (int i = 0; i < _rows.Count; i++)
                    {
                        sale += _rows[i].Kind == ConstructionTables.LandForSale ? 1 : 0;
                        mine += _rows[i].Kind == ConstructionTables.LandOwned || _rows[i].Kind == ConstructionTables.LandBuilding ? 1 : 0;
                    }
                    Add(labels, values, "reallife_ui_cons_fig_for_sale", sale.ToString(Inv));
                    Add(labels, values, "reallife_ui_cons_fig_yours", mine.ToString(Inv));
                    break;
                }
                case ConstructionDirector.TabProjects:
                {
                    long spent = 0;
                    for (int i = 0; i < state.Projects.Count; i++)
                    {
                        spent += state.Projects[i].Live ? state.Projects[i].Spent : 0L;
                    }
                    Add(labels, values, "reallife_ui_cons_fig_under_way", state.Building().ToString(Inv));
                    Add(labels, values, "reallife_ui_cons_fig_done", f.Finished.ToString(Inv));
                    Add(labels, values, "reallife_ui_cons_fig_spent", ConstructionText.Money(spent));
                    break;
                }
                case ConstructionDirector.TabTenders:
                {
                    int open = 0;
                    for (int i = 0; i < state.Tenders.Count; i++)
                    {
                        open += state.Tenders[i].State == TenderState.Open ? 1 : 0;
                    }
                    Add(labels, values, "reallife_ui_cons_fig_open", open.ToString(Inv));
                    Add(labels, values, "reallife_ui_cons_fig_won", f.TendersWon.ToString(Inv));
                    Add(labels, values, "reallife_ui_cons_fig_lost", f.TendersLost.ToString(Inv));
                    break;
                }
                case ConstructionDirector.TabCrews:
                {
                    int on = 0;
                    long perDay = 0;
                    for (int i = 0; i < state.Crews.Count; i++)
                    {
                        on += state.Crews[i].Project > 0 ? 1 : 0;
                        perDay += state.Crews[i].Rate;
                    }
                    Add(labels, values, "reallife_ui_cons_fig_crews", state.Crews.Count.ToString(Inv));
                    Add(labels, values, "reallife_ui_cons_fig_on_site", on.ToString(Inv));
                    Add(labels, values, "reallife_ui_cons_fig_a_day", ConstructionText.Money(perDay));
                    break;
                }
                case ConstructionDirector.TabCompany:
                    Add(labels, values, "reallife_ui_cons_fig_standing", f.Reputation.ToString(Inv));
                    Add(labels, values, "reallife_ui_cons_fig_done", f.Finished.ToString(Inv));
                    Add(labels, values, "reallife_ui_cons_fig_books", ConstructionText.Money(f.Earned - f.Spent));
                    break;
                case ConstructionDirector.TabRivals:
                {
                    int better = 0;
                    for (int r = 0; r < BuildCatalogue.RivalCount; r++)
                    {
                        better += state.Rivals[r].Reputation > f.Reputation ? 1 : 0;
                    }
                    Add(labels, values, "reallife_ui_cons_fig_builders", BuildCatalogue.RivalCount.ToString(Inv));
                    Add(labels, values, "reallife_ui_cons_fig_rank", ConstructionText.T("reallife_bld_value_rank", "rank", (better
                        + 1).ToString(Inv), "count", (BuildCatalogue.RivalCount + 1).ToString(Inv)));
                    break;
                }
                default:
                {
                    long owed = 0;
                    int bills = 0;
                    for (int i = 0; i < _rows.Count; i++)
                    {
                        bills += _rows[i].Kind == ConstructionTables.CityBill ? 1 : 0;
                    }
                    for (int i = 0; i < state.Projects.Count; i++)
                    {
                        owed += state.Projects[i].LoanOwed + state.Projects[i].InvestorOwed;
                    }
                    Add(labels, values, "reallife_ui_cons_fig_bills", bills.ToString(Inv));
                    Add(labels, values, "reallife_ui_cons_fig_owed", ConstructionText.Money(owed));
                    break;
                }
            }
            m.FigLabels = labels.ToArray();
            m.FigValues = values.ToArray();
        }

        private static void Add(List<string> labels, List<string> values, string key, string value)
        {
            labels.Add(ConstructionText.T(key));
            values.Add(value);
        }

        // What land asks a square metre in each district, the mean of the lots for sale, with the change on the month before once there is a month before it.
        private UiFactCardModel AsksCard(BuildState state)
        {
            long most = 0L;
            for (int i = 0; i < state.Asks.Count; i++)
            {
                most = Math.Max(most, state.Asks[i].Latest);
            }
            if (most <= 0L)
            {
                return null;
            }
            var lines = new List<UiFactLine>(state.Asks.Count);
            for (int i = 0; i < state.Asks.Count; i++)
            {
                AskRing ring = state.Asks[i];
                if (ring.Latest <= 0L)
                {
                    continue;
                }
                int change = ring.Change;
                string figure = ConstructionText.Money(ring.Latest) + (ring.Count < 2 ? "  " + ConstructionText.T("reallife_ui_cons_ask_wait")
                    : "  " + (change > 0 ? "+" : string.Empty) + (change / 10f).ToString("0.0", Inv) + "%");
                lines.Add(UiFactLine.Bar(ConstructionText.T(ring.District), (int)(ring.Latest * 100L / most), figure));
            }
            return lines.Count == 0 ? null : new UiFactCardModel { Title = ConstructionText.T("reallife_ui_cons_asks_title"), Heading = true, Lines = lines.ToArray() };
        }

        // ------------------------------------------------------------------ the table

        // The kind of each column of a tab's table: the first is the thing's name on a tile, the last of a state a chip.
        private static UiCellKind[] KindsOf(string tab)
        {
            switch (tab)
            {
                case "Land": return new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Text, UiCellKind.Money, UiCellKind.Chip, UiCellKind.Money };
                case "Projects": return new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Meter, UiCellKind.Money, UiCellKind.Chip };
                case "Tenders": return new[] { UiCellKind.Tile, UiCellKind.Money, UiCellKind.Text, UiCellKind.Text, UiCellKind.Chip };
                case "Crews": return new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Meter, UiCellKind.Meter, UiCellKind.Money, UiCellKind.Chip };
                case "Rivals": return new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Meter, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Text, UiCellKind.Figure };
                case "City": return new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Money, UiCellKind.Text };
                default: return new[] { UiCellKind.Text, UiCellKind.Text };
            }
        }

        // The columns in the order they are shown, by the column of the row that fills them: a column the kit adds (the price a square metre, the floors a builder is
        // licensed to, the jobs it has done for the player) stands past the old table's own and is put where it reads best.
        private static int[] OrderOf(string tab, int own)
        {
            switch (tab)
            {
                case "Land": return new[] { 0, 1, 2, 3, 5, 4 };
                case "Rivals": return new[] { 0, 1, 2, 5, 3, 4, 6 };
                default:
                    var order = new int[own];
                    for (int i = 0; i < own; i++)
                    {
                        order[i] = i;
                    }
                    return order;
            }
        }

        private static string ExtraTitle(string tab, int column)
        {
            string key = tab == "Land" ? "reallife_ui_cons_col_sqm" : column == 5 ? "reallife_ui_cons_col_floors" : "reallife_ui_cons_col_jobs";
            return ConstructionText.T(key);
        }

        private static UiIcon RowIcon(string tab, int kind)
        {
            switch (tab)
            {
                case "Land":
                    return kind == ConstructionTables.LandOwned ? UiIcon.Key : kind == ConstructionTables.LandBuilding
                        ? UiIcon.Building : kind == ConstructionTables.LandLandmark ? UiIcon.Flag
                        : kind == ConstructionTables.LandRival ? UiIcon.Handshake : kind == ConstructionTables.LandOfferIn ? UiIcon.Envelope : UiIcon.MapTrifold;
                case "Projects": return UiIcon.Hammer;
                case "Tenders": return UiIcon.ClipboardText;
                case "Crews":
                    return kind == ConstructionTables.CrewsPlant ? UiIcon.Truck : kind == ConstructionTables.CrewsStaff
                        ? UiIcon.IdBadge : kind == ConstructionTables.CrewsOffer ? UiIcon.Tag : UiIcon.People;
                case "Rivals": return UiIcon.Flag;
                default:
                    return kind == ConstructionTables.CityBill ? UiIcon.Receipt : kind == ConstructionTables.CityCouncil
                        ? UiIcon.Scales : kind == ConstructionTables.CityAward ? UiIcon.Trophy : UiIcon.ListChecks;
            }
        }

        private static UiRole Tone(byte tone)
        {
            switch ((CellTone)tone)
            {
                case CellTone.Green: return UiRole.Ok;
                case CellTone.Yellow: return UiRole.Warn;
                case CellTone.Red: return UiRole.Danger;
                default: return UiRole.Muted;
            }
        }

        // The rows the old pages wrote, ordered and filtered as the table's head and filter ask, in the kit's columns.
        private UiDataTableModel TableOf(ConstructionModel m)
        {
            TableSpec spec = _spec;
            UiCellKind[] kinds = KindsOf(spec.Tab);
            int[] shownAs = OrderOf(spec.Tab, Math.Min(spec.Count, kinds.Length));
            int cols = shownAs.Length;
            var columns = new UiDataColumn[cols];
            for (int d = 0; d < cols; d++)
            {
                int c = shownAs[d];
                bool own = c < spec.Count;
                TableColumn col = own ? spec.Columns[c] : null;
                columns[d] = new UiDataColumn
                {
                    Title = own ? ConstructionText.T(col.TitleKey) : ExtraTitle(spec.Tab, c), Kind = kinds[c], Width = own && col.Flexible ? 0f : Width(kinds[c]),
                    Sortable = !own || col.Sort != TableSort.None
                };
            }
            int filter = spec.FilterCount > 0 ? _view.Filter : -1;
            m.FilterKinds = spec.FilterCount > 0 && spec.Filters[0].KindKeys != null ? spec.Filters[0].KindKeys.Length : 0;
            var shown = new List<TableRow>(_rows.Count);
            for (int i = 0; i < _rows.Count; i++)
            {
                TableRow r = _rows[i];
                if (r.Info || (filter >= 0 && r.Kind != filter))
                {
                    continue;
                }
                shown.Add(r);
            }
            int sort = _view.Sort >= 0 && _view.Sort < cols ? _view.Sort : spec.FirstColumn >= 0 ? Array.IndexOf(shownAs, spec.FirstColumn) : -1;
            bool down = _view.Sort >= 0 ? _view.SortDown : !spec.FirstAscending;
            if (sort >= 0 && sort < cols)
            {
                int by = shownAs[sort];
                shown.Sort((a, b) =>
                {
                    double x = a.KeyAt(by), y = b.KeyAt(by);
                    int order = !double.IsNaN(x) && !double.IsNaN(y) ? x.CompareTo(y) : string.Compare(a.TextAt(by), b.TextAt(by), StringComparison.CurrentCultureIgnoreCase);
                    return down ? -order : order;
                });
            }
            var rows = new UiDataRow[shown.Count];
            var keys = new string[shown.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                TableRow r = shown[i];
                var cells = new string[cols];
                var values = new int[cols];
                var tones = new UiRole[cols];
                for (int d = 0; d < cols; d++)
                {
                    int c = shownAs[d];
                    string words = r.TextAt(c);
                    cells[d] = words == "-" ? string.Empty : words ?? string.Empty;
                    tones[d] = c == 0 ? UiRole.None : Tone(r.ToneAt(c));
                    double key = r.KeyAt(c);
                    values[d] = kinds[c] == UiCellKind.Tile ? (int)RowIcon(spec.Tab, r.Kind) : kinds[c] == UiCellKind.Meter ? MeterValue(spec.Tab, c, key) : 0;
                    if (kinds[c] == UiCellKind.Meter && double.IsNaN(key))
                    {
                        cells[d] = string.Empty;
                    }
                }
                rows[i] = new UiDataRow { Cells = cells, Values = values, Tones = tones };
                keys[i] = r.Pick ? r.Id : null;
            }
            m.RowKeys = keys;
            var table = new UiDataTableModel { Columns = columns, Rows = rows, RowCount = rows.Length, SortColumn = sort, SortDown = down, Selected = -1 };
            if (spec.FilterCount > 0)
            {
                TableFilter f = spec.Filters[0];
                table.Filters = new[] { filter >= 0 && f.KindKeys != null && filter < f.KindKeys.Length ? ConstructionText.T(f.KindKeys[filter]) : ConstructionText.T(f.AllKey) };
            }
            if (rows.Length == 0 && !string.IsNullOrEmpty(spec.EmptyKey))
            {
                table.EmptyTitle = ConstructionText.T(TabKeys[_tab]);
                table.EmptyBody = ConstructionText.T(spec.EmptyKey);
            }
            return table;
        }

        private static float Width(UiCellKind kind)
        {
            switch (kind)
            {
                case UiCellKind.Money: return 144f;
                case UiCellKind.Chip: return 232f;
                case UiCellKind.Meter: return 144f;
                case UiCellKind.Figure: return 96f;
                default: return 168f;
            }
        }

        // A meter's bar: progress is a share of one, skill and morale are already 0 to 100.
        private static int MeterValue(string tab, int column, double key)
        {
            if (double.IsNaN(key))
            {
                return 0;
            }
            return (int)Math.Max(0, Math.Min(100, tab == "Projects" ? Math.Round(key * 100.0) : Math.Round(key)));
        }
    }
}
