using System;
using System.Collections.Generic;
using System.Globalization;
using AwesomeCharts;
using BigAmbitions.Rivals;
using EnhancedUI.EnhancedScroller;
using Extensions;
using Localizor;
using Streets;
using UI.Components;
using UI.Smartphone.Apps.MarketInsider;
using UI.Smartphone.Apps.Rivals;
using UI.Smartphone.Apps.Rivals.Tables;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Rivals as a screen of the kit, mirrored from the game's own app (a league you climb): the game's leaderboard in its
    /// order (by weekly income) with each line's week read from the game's own history of it, the chosen rival's panel
    /// (the chart as the game drew it under its filter, the rivalry's marks with the game's own words, Open in Contacts
    /// where the game shows it), and the chosen rival's businesses and buildings as the game's tables hold and sort them.
    /// Every press goes to the game: a rival is chosen through its leaderboard button (so the game's event fires), a filter
    /// through the game's chart, a sort through the table's header, and a row's Show on map, Set destination and opening
    /// through that row's own buttons in the game's table, brought into its (unseen) view first. Cost: a look at a few
    /// counts four times a second while it shows (nothing allocated); a layout only when they changed.
    /// </summary>
    public sealed class RivalsScreen : IUiScreen, IUiPointing
    {
        private const float Every = 0.25f;                  // seconds between looks at the game's app
        private const int Week = 7;                          // days back a line's week is read from
        private const string MoneyFilter = "weeklyincome";   // the game's filter of its chart that is in money
        private static readonly string[] BusinessHeads = { "BusinessName", "BusinessType", "Address", "WeeklyIncome" };   // the game's headers, in the kit's columns' order
        private static readonly string[] BuildingHeads = { "Address", "Neighborhood", "BuildingType", "TotalSize", "EstimatedValue", "Price" };

        private readonly RivalsApp _app;
        private readonly RivalsView _v = new RivalsView();
        private readonly UiRivalsModel _m = new UiRivalsModel();
        private readonly List<RivalEntry> _league = new List<RivalEntry>(24);
        private readonly List<RivalLeaderboardButton> _lines = new List<RivalLeaderboardButton>(24);   // each league row's game button
        private readonly List<string> _filterValues = new List<string>(4);
        private bool _dirty = true;
        private float _next;
        private long _seen;
        private int _tab, _selected, _reveal = -1, _focus = -1;
        private float _offset;

        public RivalsScreen(RivalsApp app)
        {
            _app = app;
        }

        public string Id => RivalsKit.Id;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    long now = Signature();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            Build();
            RivalsViews.Rivals(_m, UiText.Words, _v);
            UiRivals.Lay(s, _m, frame, dark, keys);
            _offset = _m.Table.Offset;
            _reveal = -1;
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            _dirty = true;
            switch (command)
            {
                case UiAppWindow.NavCommand:
                    return Show(arg);
                case UiAppWindow.ActionCommand:
                    return Done(UiWidgets.Press(Contacts()), "open the chosen rival in Contacts: handed to the game");
                case UiRivals.ChartTabCommand:
                    return Filter(arg);
                case UiDataTable.First + UiDataTable.Row:
                    return _tab == UiRivals.League ? Choose(arg) : Select(arg);
                case UiDataTable.First + UiDataTable.Head:
                    return Sort(arg);
                case UiRivals.RowLinkCommand:
                    return RowButton(0);
                case UiRivals.RowActionCommand:
                    return RowButton(_tab == UiRivals.Businesses ? arg + 1 : 1);
                case UiChart.First + UiChart.Point:
                    _focus = _focus == arg ? -1 : arg;
                    return true;
                default:
                    return false;
            }
        }

        public void Focused(int command, int arg)
        {
            if (command == UiDataTable.First + UiDataTable.Row && _tab != UiRivals.League && arg != _selected)
            {
                _selected = arg;   // the cursor chooses the row its actions are for, as a management game's tables follow it
                _dirty = true;
            }
            else if (command == UiChart.First + UiChart.Point && arg != _focus)
            {
                _focus = arg;
                _dirty = true;
            }
        }

        public void Pointed(int command, int arg)
        {
            int point = command == UiChart.First + UiChart.Point ? arg : -1;
            if (point != _focus)
            {
                _focus = point;
                _dirty = true;
            }
        }

        public bool Back() => false;   // nothing of the kit's own is open over the page: the game closes its app

        public void Tab(int step) => Show((_tab + step + 3) % 3);

        public bool Act(UiAction action) => false;   // F and P are the buttons keyed on them

        public void Reveal(int list, int index)
        {
            _reveal = index;
            _dirty = true;
        }

        public void Wheel(int clip, float notches)
        {
            _offset = Math.Max(0f, _offset - notches * UiTokens.S16);
            _dirty = true;
        }

        // ------------------------------------------------------------------------------------------------- presses

        // The kit's tab, and the game's own tab of the chosen rival's tables with it, so the rows pressed are the game's in view.
        private bool Show(int tab)
        {
            if (tab < 0 || tab > UiRivals.Buildings)
            {
                return false;
            }
            if (tab != _tab)
            {
                _tab = tab;
                _selected = 0;
                _offset = 0f;
                _focus = -1;
                if (tab != UiRivals.League && !GameTab(tab == UiRivals.Businesses ? (Component)Businesses() : Buildings()))
                {
                    UiDirector.Logger?.Info("Rivals (kit): the game's TabMenu above the " + (tab == UiRivals.Businesses ? "businesses" : "buildings")
                                            + " table is MISSING, so the game's own Rivals view shows until the app closes");
                    RivalsKit.Classic();
                }
            }
            return true;
        }

        /// <summary>
        /// The game's own tab of a table: its TabMenu is found above the table (no hierarchy path), the container the table
        /// stands in is the ancestor under the menu's containers parent, and the menu's public SelectTab takes its name.
        /// False when there is no TabMenu or the table is in none of its containers.
        /// </summary>
        internal static bool GameTab(Component table, bool select = true)
        {
            TabMenu menu = table != null ? table.GetComponentInParent<TabMenu>(true) : null;
            var containers = menu != null ? GameContract.RivalsTabContainers.Field?.GetValue(menu) as Transform : null;
            for (Transform at = table != null ? table.transform : null; containers != null && at != null; at = at.parent)
            {
                if (at.parent == containers)
                {
                    if (select)
                    {
                        menu.SelectTab(at.name);
                    }
                    return true;
                }
            }
            return false;
        }

        // A rival chosen through the game's own leaderboard button: the game shows them, loads their tables and fires its event.
        private bool Choose(int row)
        {
            if (row < 0 || row >= _lines.Count || _lines[row] == null)
            {
                return false;
            }
            return Done(UiWidgets.Press(_lines[row].button), "choose a rival from the leaderboard: handed to the game");
        }

        private bool Select(int row)
        {
            _selected = row;
            return true;
        }

        // A filter of the chart through the game's own: it draws the chart again, which the kit reads back.
        private bool Filter(int index)
        {
            ChartController chart = Chart();
            if (chart == null || index < 0 || index >= _filterValues.Count)
            {
                return false;
            }
            chart.SetFilter(_filterValues[index]);
            _focus = -1;
            return Done(true, "filter the rival's chart: handed to the game");
        }

        // A sort through the table's own header: it orders its rows and shows its arrow, which the kit reads back.
        private bool Sort(int column)
        {
            if (_tab == UiRivals.Businesses && column >= 0 && column < BusinessHeads.Length)
            {
                Businesses()?.ChangeSortOrder(BusinessHeads[column]);
                return true;
            }
            if (_tab == UiRivals.Buildings && column >= 0 && column < BuildingHeads.Length)
            {
                Buildings()?.ChangeSortOrder(BuildingHeads[column]);
                return true;
            }
            return false;
        }

        // The chosen row's own button in the game's table (brought into its unseen view): a business's Show on map (0), its
        // opening in BizMan (1) or Set destination (2); a building's opening (1).
        private bool RowButton(int which)
        {
            if (_tab == UiRivals.Businesses)
            {
                RivalBusinessesTable table = Businesses();
                var cell = CellAt(table != null ? table.scroller : null, _selected, table != null ? table.data.Count : 0) as RivalBusinessesCellView;
                Button b = cell == null ? null : which == 0 ? cell.showOnMapButton : which == 2 ? cell.setDestinationButton
                    : GameContract.RivalsOpenBusiness.Field?.GetValue(cell) as Button;
                return Done(UiWidgets.Press(b), (which == 0 ? "show a rival's business on the map" : which == 2 ? "set a rival's business as the destination" : "open a rival's business")
                                                 + ": handed to the game");
            }
            if (_tab == UiRivals.Buildings && which == 1)
            {
                RivalRealEstateTable table = Buildings();
                var cell = CellAt(table != null ? table.scroller : null, _selected, table != null ? table.data.Count : 0) as RealEstateCellView;
                var b = cell != null ? GameContract.RivalsOpenBuilding.Field?.GetValue(cell) as Button : null;
                return Done(UiWidgets.Press(b), "open a rival's building: handed to the game");
            }
            return false;
        }

        private static EnhancedScrollerCellView CellAt(EnhancedScroller scroller, int index, int count)
        {
            if (scroller == null || index < 0 || index >= count)
            {
                return null;
            }
            scroller.JumpToDataIndex(index);
            return scroller.GetCellViewAtDataIndex(index);
        }

        // ------------------------------------------------------------------------------------------------- the game's state

        private RivalLeaderboard Board() => GameContract.RivalsBoard.Field?.GetValue(_app) as RivalLeaderboard;

        private SelectedRivalUI Chosen() => GameContract.RivalsChosen.Field?.GetValue(_app) as SelectedRivalUI;

        private ChartController Chart() => GameContract.RivalsChart.Field?.GetValue(Chosen()) as ChartController;

        private RivalBusinessesTable Businesses() => GameContract.RivalsBusinessTable.Field?.GetValue(_app) as RivalBusinessesTable;

        private RivalRealEstateTable Buildings() => GameContract.RivalsBuildingTable.Field?.GetValue(_app) as RivalRealEstateTable;

        private Button Contacts() => GameContract.RivalsContacts.Field?.GetValue(Chosen()) as Button;

        private List<RivalLeaderboardButton> Lines() => GameContract.RivalsButtons.Field?.GetValue(Board()) as List<RivalLeaderboardButton>;

        // What would change the screen: the leaderboard (built anew each time the app opens), the rival chosen, the chart's
        // points and filter, the tables' rows and sort, the rivalry's marks, and the kit's own tab, row and reading.
        private long Signature()
        {
            long sig = _tab * 31L + _selected;
            sig = sig * 31 + _focus;
            List<RivalLeaderboardButton> lines = Lines();
            sig = sig * 31 + (lines != null ? lines.Count : -1);
            RivalLeaderboardData chosen = _app.selectedRival;
            sig = sig * 31 + (chosen != null ? chosen.GetHashCode() : 0);
            ChartController chart = Chart();
            List<LineEntry> points = chart != null && chart.chart != null && chart.chart.GetChartData() != null && chart.chart.GetChartData().DataSets.Count > 0
                ? chart.chart.GetChartData().DataSets[0].Entries : null;
            sig = sig * 31 + (points != null ? points.Count + (points.Count > 0 ? Mathf.RoundToInt(points[points.Count - 1].Value) : 0) : -1);
            string filter = GameContract.RivalsFilter.Field?.GetValue(Chosen()) as string;
            sig = sig * 31 + (filter != null ? filter.Length : -1);
            RivalBusinessesTable businesses = Businesses();
            RivalRealEstateTable buildings = Buildings();
            sig = sig * 31 + (businesses != null ? businesses.data.Count * 7 + SortOf(businesses.transform, BusinessHeads, out bool _) : -1);
            sig = sig * 31 + (buildings != null ? buildings.data.Count * 7 + SortOf(buildings.transform, BuildingHeads, out bool _) : -1);
            var marks = GameContract.RivalsMarks.Field?.GetValue(Chosen()) as Transform;
            sig = sig * 31 + (marks != null ? marks.childCount : -1);
            return sig;
        }

        private void Build()
        {
            _v.Tab = _tab;
            _v.Money = f => f.ToShortCurrencyFormat();
            League();
            ChosenPanel();
            Tables();
            _v.Selected = _selected;
            _v.Offset = _offset;
            _v.Reveal = _reveal;
            _v.ChartFocus = _focus;
        }

        // The game's leaderboard in its order: each button's line, its war mark as the game shows it, and its week from the
        // game's own history of the weekly income (the player's, or the rival's).
        private void League()
        {
            _league.Clear();
            _lines.Clear();
            List<RivalLeaderboardButton> lines = Lines();
            GameInstance save = SaveGameManager.Current;
            int today = save != null ? save.Day : 0;
            int chosen = 0;
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                RivalLeaderboardButton line = lines[i];
                RivalLeaderboardData d = line != null ? line.data : null;
                if (d == null)
                {
                    continue;
                }
                FaceSources.Rival(d);   // their face as the game has it: its own sprite or its portrait camera, never a painted one
                bool you = string.IsNullOrEmpty(d.rivalId);
                var war = GameContract.RivalsWarMark.Field?.GetValue(line) as Image;
                List<Tuple<int, float>> history = you ? (save != null ? save.playerWeeklyIncomeHistory : null) : RivalsHelper.GetRivalState(d.rivalId)?.weeklyIncomeHistory;
                if (d == _app.selectedRival)
                {
                    chosen = _league.Count;
                }
                _league.Add(new RivalEntry
                {
                    Name = d.entryName, You = you, Defeated = d.isDefeated, Rivalry = war != null && war.gameObject.activeSelf, WeeklyIncome = d.weeklyIncome,
                    Businesses = d.ownedBusinesses != null ? d.ownedBusinesses.Count : 0, WeekAgo = WeekAgo(history, today), Age = d.ageInYears,
                    District = string.IsNullOrEmpty(d.mostActiveNeighborhood) ? null : UiText.T(d.mostActiveNeighborhood)
                });
                _lines.Add(line);
            }
            _v.League = _league.ToArray();
            _v.Chosen = chosen;
        }

        /// <summary>A weekly income's figure from about a week before today in the game's history of it, or NaN with none.</summary>
        private static float WeekAgo(List<Tuple<int, float>> history, int today)
        {
            float found = float.NaN;
            int day = int.MaxValue;
            for (int i = 0; history != null && i < history.Count; i++)
            {
                Tuple<int, float> e = history[i];
                if (e != null && e.Item1 >= today - Week && e.Item1 < today && e.Item1 < day)
                {
                    day = e.Item1;
                    found = e.Item2;
                }
            }
            return found;
        }

        // The chosen rival's panel: Open in Contacts where the game shows it, the chart as the game drew it, its filters, and
        // the rivalry's marks with the game's own words.
        private void ChosenPanel()
        {
            Button contacts = Contacts();
            _v.Contacts = contacts != null && contacts.gameObject.activeInHierarchy;
            ChartController chart = Chart();
            _filterValues.Clear();
            var labels = new List<string>(4);
            string filter = GameContract.RivalsFilter.Field?.GetValue(Chosen()) as string;
            int chosenFilter = 0;
            for (int i = 0; chart != null && chart.filterOptions != null && i < chart.filterOptions.Count; i++)
            {
                FilterOption o = chart.filterOptions[i];
                if (o == null)
                {
                    continue;
                }
                if (string.Equals(o.value, filter, StringComparison.OrdinalIgnoreCase))
                {
                    chosenFilter = _filterValues.Count;
                }
                _filterValues.Add(o.value);
                labels.Add(UiText.T(o.label));
            }
            _v.Filters = labels.ToArray();
            _v.Filter = chosenFilter;
            _v.ChartMoney = string.Equals(filter, MoneyFilter, StringComparison.OrdinalIgnoreCase);
            List<LineEntry> points = chart != null && chart.chart != null && chart.chart.GetChartData() != null && chart.chart.GetChartData().DataSets.Count > 0
                ? chart.chart.GetChartData().DataSets[0].Entries : null;
            int n = points != null ? points.Count : 0;
            _v.Chart = new float[n];
            _v.ChartDays = new int[n];
            for (int i = 0; i < n; i++)
            {
                _v.Chart[i] = points[i] != null ? points[i].Value : 0f;
                _v.ChartDays[i] = points[i] != null ? Mathf.RoundToInt(points[i].Position) : 0;
            }
            Threats();
        }

        // The rivalry's marks the game put on the chosen rival (a special rival at war with the player): each mark's title and
        // first line in the game's own words, its glyph by what it is.
        private void Threats()
        {
            var shown = GameContract.RivalsMarksShown.Field?.GetValue(Chosen()) as GameObject;
            var marks = GameContract.RivalsMarks.Field?.GetValue(Chosen()) as Transform;
            var rows = new List<UiRivalThreat>(6);
            for (int i = 0; shown != null && shown.activeInHierarchy && marks != null && i < marks.childCount; i++)
            {
                Transform mark = marks.GetChild(i);
                BasicTooltip tip = mark.gameObject.activeSelf ? mark.GetComponent<BasicTooltip>() : null;
                if (tip == null || string.IsNullOrEmpty(tip.titleKey))
                {
                    continue;
                }
                string body = null;
                if (!string.IsNullOrEmpty(tip.descriptionKey))
                {
                    string first = tip.descriptionKey.Split(',')[0];
                    body = Words(first, tip.localizationArguments);
                }
                rows.Add(new UiRivalThreat { Icon = ThreatIcon(tip.titleKey), Title = Words(tip.titleKey, tip.localizationArguments), Body = body });
            }
            _v.Threats = rows.ToArray();
        }

        // A mark's words as its tooltip writes them, with the game's spaced dash between a title and its days made a comma.
        private static string Words(string key, object args)
        {
            string words = key.Localize(args).ToString();
            return words != null ? MarketViews.Plain(words).Replace(" - ", ", ") : null;
        }

        private static UiIcon ThreatIcon(string key) =>
            key.Contains("price_reduction") ? UiIcon.Tag : key.Contains("hire_best") ? UiIcon.People : key.Contains("low_demand") ? UiIcon.Store
            : key.Contains("shortage") || key.Contains("backorder") ? UiIcon.Package : UiIcon.Flag;

        // The chosen rival's two tables as the game holds and sorts them, and the sort the shown one's header says.
        private void Tables()
        {
            RivalBusinessesTable businesses = Businesses();
            List<RivalBusinessesCellView.RivalBusinessModel> rows = businesses != null ? businesses.data : null;
            int n = rows != null ? rows.Count : 0;
            _v.Businesses = new RivalBusiness[n];
            for (int i = 0; i < n; i++)
            {
                RivalBusinessesCellView.RivalBusinessModel b = rows[i];
                _v.Businesses[i] = b == null ? default : new RivalBusiness
                {
                    Name = b.BusinessName, TypeKey = b.BusinessType, Type = UiText.T(b.BusinessType), WeeklyIncome = b.WeeklyIncome, Address = b.Address.ToFormattedString()
                };
            }
            RivalRealEstateTable buildings = Buildings();
            List<RealEstateCellView.RealEstateModel> places = buildings != null ? buildings.data : null;
            int k = places != null ? places.Count : 0;
            _v.Buildings = new RivalBuilding[k];
            for (int i = 0; i < k; i++)
            {
                _v.Buildings[i] = MarketScreen.Building(places[i]);
            }
            _v.SortColumn = -1;
            _v.SortDown = false;
            if (_tab == UiRivals.Businesses && businesses != null)
            {
                _v.SortColumn = SortOf(businesses.transform, BusinessHeads, out bool down) - 1;
                _v.SortDown = down;
            }
            else if (_tab == UiRivals.Buildings && buildings != null)
            {
                _v.SortColumn = SortOf(buildings.transform, BuildingHeads, out bool down) - 1;
                _v.SortDown = down;
            }
        }

        /// <summary>The column a table's header shows its arrow on, plus one (0 for none), and whether it points down.</summary>
        internal static int SortOf(Transform table, string[] heads, out bool down)
        {
            down = false;
            Transform headers = table != null ? table.Find("Headers") : null;
            for (int i = 0; headers != null && i < heads.Length; i++)
            {
                Transform head = headers.Find(heads[i]);
                Transform up = head != null ? head.Find("ArrowUp") : null, dn = head != null ? head.Find("ArrowDown") : null;
                if ((up != null && up.gameObject.activeSelf) || (dn != null && dn.gameObject.activeSelf))
                {
                    down = dn != null && dn.gameObject.activeSelf;
                    return i + 1;
                }
            }
            return 0;
        }

        // One line a press in Player.log, so a report shows each button reaching the game.
        private static bool Done(bool done, string what)
        {
            UiDirector.Logger?.Info("Rivals (kit): " + what + (done ? string.Empty : " was not shown by the game, so nothing was pressed"));
            return done;
        }
    }
}
