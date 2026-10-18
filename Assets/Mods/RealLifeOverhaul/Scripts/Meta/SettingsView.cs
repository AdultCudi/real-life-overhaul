using System;
using System.Collections.Generic;
using System.Diagnostics;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The Settings app's page in the game's frame (GameUi.AppShell, copied from MyEmployees) with the kit's table: a tab per
    /// group of the options page; in the table the group's sections in the page's order, each a heading ("4 of 6 on") over its
    /// rows (an option's name, its value, what keeps it from working), a drop-down to show one section, and the test tools at
    /// the end of Troubleshooting; the box for what is picked (an option's sentence, value, control, needs and Guide page);
    /// and a search over every group. Every value goes through OptionsLayout (OptionsLayout.Book.cs), the rows of the Mods tab
    /// itself. Written when the app opens and on a click; nothing per frame.
    /// </summary>
    public sealed partial class SettingsView
    {
        private const int ModeTab = 0, ModeFound = 1, ModeDiffer = 2;
        private const int SearchLength = 40;   // characters the game's text entry takes for a search
        private const string TabPrefix = "reallife" + "_set_tab_";   // and the group's id (apart, so that the locale check does not take the prefix for a key)
        private const string GuideTitle = "reallife_guide_start_title";   // the Guide's page the app's own pages point to: "Getting started" has its section

        private static readonly TableSpec Found = new TableSpec("Settings", "Found")
            .Column("reallife_set_col_option", 760f, TableSort.None, flexible: true)
            .Column("reallife_set_col_value", 300f)
            .Column("reallife_set_col_where", 620f, right: true)
            .Empty("reallife_set_empty_found");

        private static readonly TableSpec Differ = new TableSpec("Settings", "Differ")
            .Column("reallife_set_col_option", 760f, TableSort.None, flexible: true)
            .Column("reallife_set_col_value", 300f)
            .Column("reallife_set_col_preset", 300f)
            .Column("reallife_set_col_where", 460f, right: true)
            .Empty("reallife_set_empty_differ");

        private readonly List<TableRow> _rows = new List<TableRow>(128);
        private readonly List<TableRow> _pool = new List<TableRow>(128);
        private readonly List<SettingsLine> _all = new List<SettingsLine>(800);   // every line of every tab: a table row's Index is its place here
        private readonly List<string> _ids = new List<string>(800);              // the table's id of each line
        private readonly List<string> _words = new List<string>(4);
        private readonly List<int> _found = new List<int>(64);
        private TableSpec[] _specs;             // one per tab, so that each keeps the section it shows
        private int[] _starts;                  // where each tab's lines begin in _all
        private AppShell _shell;
        private DataTable _table;
        private SettingsLine _pick;
        private int _tab;
        private int _mode;
        private string _query = string.Empty;
        private string _want;
        private bool _ready, _writing, _wrote;

        /// <summary>The app's panel, asleep; null if the game's parts are not as expected.</summary>
        public GameObject Build(FullMenu menu, Transform dormant)
        {
            SettingsTab[] tabs = OptionsLayout.AppTabs;
            if (tabs == null)
            {
                return null;
            }
            _shell = AppShell.Make(menu, dormant, SettingsApp.AppId);
            _table = DataTable.On(_shell, "Settings");
            _specs = new TableSpec[tabs.Length];
            for (int t = 0; t < tabs.Length; t++)
            {
                _specs[t] = new TableSpec("Settings", tabs[t].Group.Id)
                    .Column("reallife_set_col_option", 900f, TableSort.None, flexible: true)
                    .Column("reallife_set_col_value", 320f)
                    .Column("reallife_set_col_note", 560f, right: true)
                    .Filter("reallife_set_all_sections")
                    .Empty("reallife_set_empty");
            }
            if (_table == null || !_shell.SetTabCount(tabs.Length) || !_shell.SetColumns(_specs[0]))
            {
                return null;
            }
            _table.SelectionLost = Back;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKey(tabs[i].Group));
                UiClone.Rewire(_shell.Tabs[i], () => UiClone.Safe(() => ShowTab(tab)));
            }
            _all.Clear();
            _ids.Clear();
            _starts = new int[tabs.Length];
            for (int t = 0; t < tabs.Length; t++)
            {
                _starts[t] = _all.Count;
                foreach (SettingsLine line in tabs[t].Lines)
                {
                    _ids.Add("l" + _all.Count);
                    _all.Add(line);
                }
            }
            return _shell.Root;
        }

        /// <summary>A tab's label: a short word for its group (reallife_set_tab_you).</summary>
        public static string TabKey(TreeGroup group)
        {
            return TabPrefix + group.Id;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        public void Opened()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            _ready = true;
            if (_want != null)
            {
                GoTo(_want);
                _want = null;
            }
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
            if (_wrote)
            {
                _wrote = false;
                UiClone.Safe(UnityEngine.PlayerPrefs.Save);   // on disk now, not only when the game quits: a crash later keeps the change
            }
        }

        /// <summary>The section ("s:id") or option (its id) shown when the app next opens; null keeps what was shown.</summary>
        public void Want(string target)
        {
            _want = target;
            if (Showing && _ready && target != null)
            {
                GoTo(target);
                _want = null;
                Refresh();
            }
        }

        /// <summary>The options were read again (a preset, an import, a reset): the page is written again if it is showing.</summary>
        public void Changed()
        {
            if (Showing && _ready && !_writing)
            {
                Refresh();
            }
        }

        // A section or an option asked for from elsewhere: its tab, the line picked, every section shown, and the table scrolled to it.
        private void GoTo(string target)
        {
            SettingsLine line = target.StartsWith("s:", StringComparison.Ordinal) ? OptionsLayout.AppHeading(target.Substring(2)) : OptionsLayout.AppFind(target);
            if (line == null)
            {
                MetaLog.Warn("the Settings app was asked to open on '" + target + "', which the options page does not have.");
                return;
            }
            Open(line);
        }

        private void Open(SettingsLine line)
        {
            _mode = ModeTab;
            _tab = line.Tab;
            _pick = line;
            TableFilter filter = _specs[_tab].Filters[0];
            if (filter.Chosen >= 0 && filter.Chosen != line.Filter)
            {
                filter.Chosen = -1;
                filter.ChosenName = null;
            }
            _table.RevealNext();
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab || _pick != null || _mode != ModeTab)
            {
                _tab = tab;
                _pick = null;
                _mode = ModeTab;
                Refresh();
            }
        }

        private void RowClicked(TableRow row)
        {
            SettingsLine line = row != null && row.Pick && row.Index >= 0 && row.Index < _all.Count ? _all[row.Index] : null;
            if (line == null)
            {
                _table.ClearSelection();
                return;
            }
            if (_mode != ModeTab)
            {
                Open(line);   // a match or a difference: its own tab, on its row
                Refresh();
                return;
            }
            if (line == _pick)
            {
                return;
            }
            _pick = line;
            Refresh();
        }

        private void Back()
        {
            if (_pick == null && _mode == ModeTab)
            {
                return;
            }
            _pick = null;
            _mode = ModeTab;
            _table.ClearSelection();
            Refresh();
        }

        private void Refresh()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            long started = Stopwatch.GetTimestamp();
            try
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _shell.ShowTab(_tab);
                InfoBox box = _shell.Box;
                box.Clear();
                Compare();
                string selected = null;
                TableSpec spec;
                if (_mode == ModeFound)
                {
                    spec = Found;
                    WriteFound(box);
                }
                else if (_mode == ModeDiffer)
                {
                    spec = Differ;
                    WriteDiffer(box);
                }
                else
                {
                    spec = _specs[_tab];
                    WriteTab(box, ref selected);
                }
                box.Done();
                _table.Show(spec, _rows, selected);
                if (Dev.Verbose)
                {
                    double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                    MetaLog.Info("Meta check (settings app): " + (_mode == ModeFound ? "search '" + _query + "'" : _mode == ModeDiffer ? "differences" : "tab " + OptionsLayout.AppTabs[_tab].Group.Id)
                                 + ", " + _rows.Count + " rows, " + (_pick != null ? "'" + OptionsLayout.AppLabel(_pick) + "' picked" : "nothing picked") + ", written in "
                                 + ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms (wanted under 20): " + (ms < 20.0 ? "PASS" : "SLOW") + ".");
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // The tab's lines: a heading over each section's rows, the value now, and what keeps a row from doing anything.
        private void WriteTab(InfoBox box, ref string selected)
        {
            SettingsTab tab = OptionsLayout.AppTabs[_tab];
            TableFilter filter = _specs[_tab].Filters[0];
            filter.Names.Clear();
            foreach (SettingsKind kind in tab.Kinds)
            {
                filter.Names.Add(KindName(kind));
            }
            int headingRow = -1, headingOn = 0, headingAll = 0, tests = 0;
            bool found = false;
            int first = _starts[_tab];
            for (int i = 0; i < tab.Lines.Count; i++)
            {
                SettingsLine line = tab.Lines[i];
                if (!OptionsLayout.AppShown(line) || OptionsLayout.AppLabelKey(line) == OptionsLayout.SettingsButtonKey)
                {
                    continue;   // a row that is off the page for now; and the button that opens this very app
                }
                if (line.IsHeading)
                {
                    CloseHeading(headingRow, headingOn, headingAll, tests);
                    headingOn = headingAll = tests = 0;
                }
                TableRow row = Row(first + i);
                row.Kind = line.Filter;
                if (line == _pick)
                {
                    selected = row.Id;
                    found = true;
                }
                switch (line.Kind)
                {
                    case LineKind.Heading:
                    case LineKind.TestHeading:
                        headingRow = _rows.Count - 1;
                        row.Set(0, "<b>" + OptionsLayout.AppLabel(line) + "</b>");
                        break;
                    case LineKind.Option:
                        WriteOptionRow(row, line, ref headingOn, ref headingAll);
                        break;
                    case LineKind.Button:
                        row.Set(0, OptionsLayout.AppLabel(line));
                        row.Set(1, OptionsLayout.AppCaptionKey(line).GetLocalization());
                        int preset = PresetOf(line);
                        if (preset >= 0 && _covers[preset] > 0)
                        {
                            row.Set(2, _counts[preset] == 0 ? T("reallife_set_in_use") : T("reallife_set_differ_count", "count", _counts[preset].ToString()), _counts[preset] == 0 ? TableRow.Green : TableRow.Plain);
                        }
                        break;
                    case LineKind.Key:
                        row.Set(0, OptionsLayout.AppLabel(line));
                        int action = OptionsLayout.AppKeyAction(line);
                        row.Set(1, action >= 0 ? Keys.Label((KeyAction)action) : string.Empty);
                        break;
                    case LineKind.Test:
                        tests++;
                        row.Set(0, OptionsLayout.AppLabel(line));
                        row.Set(1, T("reallife_opt_caption_run"));
                        break;
                }
            }
            CloseHeading(headingRow, headingOn, headingAll, tests);
            CountTab(tab, out int on, out int all);
            string group = tab.Group.TitleKey.GetLocalization();
            if (tab.Group.Id == OptionsTree.StartId)
            {
                UiClone.Words(_shell.Header, _closest < 0 ? T("reallife_set_app_title")
                    : _exact ? T("reallife_set_headline_in_use", "preset", PresetName(_closest))
                    : T("reallife_set_headline_differ", "preset", PresetName(_closest), "count", _counts[_closest].ToString()));
            }
            else
            {
                UiClone.Words(_shell.Header, all > 0 ? T("reallife_set_headline", "group", group, "on", on.ToString(), "total", all.ToString()) : group);
            }
            if (!found)
            {
                _pick = null;
            }
            if (_pick == null)
            {
                WriteOverview(box, tab, on, all);
                return;
            }
            WritePicked(box, _pick);
        }

        private void WriteOptionRow(TableRow row, SettingsLine line, ref int on, ref int all)
        {
            int value = OptionsLayout.AppValue(line);
            OptionType type = OptionsLayout.AppType(line);
            row.Set(0, OptionsLayout.AppLabel(line));
            row.Set(1, OptionsLayout.AppValueText(line, value), type == OptionType.Toggle && value != 0 ? TableRow.Green : TableRow.Plain);
            string aside = OptionsLayout.AppAside(line);
            string blocker = aside == null ? OptionsLayout.AppBlocker(line) : null;
            if (aside != null)
            {
                row.Set(2, T("reallife_set_aside"), TableRow.Yellow);
            }
            else if (blocker != null)
            {
                row.Set(2, T("reallife_set_needs", "option", OptionsLayout.AppLabelOf(blocker)), TableRow.Yellow);
            }
            if (type == OptionType.Toggle)
            {
                all++;
                on += value != 0 ? 1 : 0;
            }
        }

        // A heading's count, once its rows are written: "4 of 6 on", or the number of test tools under it.
        private void CloseHeading(int row, int on, int all, int tests)
        {
            if (row < 0 || row >= _rows.Count)
            {
                return;
            }
            if (tests > 0)
            {
                _rows[row].Set(1, T("reallife_set_tools", "count", tests.ToString()));
            }
            else if (all > 0)
            {
                _rows[row].Set(1, T("reallife_set_count", "on", on.ToString(), "total", all.ToString()));
            }
        }

        // The switches of a tab and how many are on, whatever the drop-down shows.
        private static void CountTab(SettingsTab tab, out int on, out int all)
        {
            on = all = 0;
            foreach (SettingsLine line in tab.Lines)
            {
                if (line.Kind == LineKind.Option && OptionsLayout.AppType(line) == OptionType.Toggle && OptionsLayout.AppShown(line))
                {
                    all++;
                    on += OptionsLayout.AppValue(line) != 0 ? 1 : 0;
                }
            }
        }

        // The section's own switches: "4 of 6 on" in its box.
        private static void CountSection(SettingsLine heading, out int on, out int all)
        {
            on = all = 0;
            foreach (SettingsLine line in OptionsLayout.AppTabs[heading.Tab].Lines)
            {
                if (line.Kind == LineKind.Option && line.Section == heading.Section && OptionsLayout.AppType(line) == OptionType.Toggle && OptionsLayout.AppShown(line))
                {
                    all++;
                    on += OptionsLayout.AppValue(line) != 0 ? 1 : 0;
                }
            }
        }

        private static string KindName(SettingsKind kind)
        {
            if (kind.IsTests)
            {
                string group = kind.TestGroup >= 0 ? OptionsTree.Groups[kind.TestGroup].TitleKey.GetLocalization() : "reallife_meta_title_other".GetLocalization();
                return T("reallife_set_tests_of", "group", group);
            }
            return (kind.Section != null ? kind.Section.TitleKey : "reallife_meta_title_other").GetLocalization();
        }

        // Where a line stands on the Mods tab: "Your character and home, Needs and wellbeing".
        private static string Where(SettingsLine line)
        {
            string group = OptionsTree.Groups[line.Tab].TitleKey.GetLocalization();
            if (line.Kind == LineKind.Test || line.Kind == LineKind.TestHeading)
            {
                return group + ", " + "reallife_meta_title_tests".GetLocalization();
            }
            return line.Section != null ? group + ", " + line.Section.TitleKey.GetLocalization() : group;
        }

        private void AskSearch()
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return;
            }
            uis.textInputUi.Show("reallife_set_search_hint", "reallife_set_search", text => UiClone.Safe(() => Searched(text)), null, _query, false, SearchLength);
        }

        private void Searched(string typed)
        {
            _query = (typed ?? string.Empty).Trim();
            OptionsFind.Split(_query, _words);
            _mode = ModeFound;
            _pick = null;
            Refresh();
        }

        // The matches of every tab, in the order of the page; a heading is found through its rows.
        private void WriteFound(InfoBox box)
        {
            var texts = new List<string>(_all.Count);
            var sections = new List<string>(_all.Count);
            var groups = new List<string>(_all.Count);
            string testWords = "reallife_meta_title_tests".GetLocalization().ToLowerInvariant();
            for (int i = 0; i < _all.Count; i++)
            {
                SettingsLine line = _all[i];
                bool shown = OptionsLayout.AppShown(line) && OptionsLayout.AppLabelKey(line) != OptionsLayout.SettingsButtonKey;
                texts.Add(shown ? OptionsLayout.AppSearchText(line) : string.Empty);
                string section = line.Section != null ? line.Section.TitleKey.GetLocalization().ToLowerInvariant() + " " + line.Section.Words : string.Empty;
                sections.Add(shown ? (line.Kind == LineKind.Test ? section + " " + testWords : section) : string.Empty);
                groups.Add(shown ? OptionsTree.Groups[line.Tab].TitleKey.GetLocalization().ToLowerInvariant() : string.Empty);
            }
            int count = SettingsRules.Search(_words, _all, texts, sections, groups, _found);
            for (int f = 0; f < _found.Count; f++)
            {
                SettingsLine line = _all[_found[f]];
                TableRow row = Row(_found[f]);
                row.Set(0, OptionsLayout.AppLabel(line));
                if (line.Kind == LineKind.Option)
                {
                    row.Set(1, OptionsLayout.AppValueText(line, OptionsLayout.AppValue(line)));
                }
                row.Set(2, Where(line));
            }
            UiClone.Words(_shell.Header, _words.Count == 0 ? T("reallife_set_search_short") : T("reallife_set_headline_found", "count", count.ToString(), "query", _query));
            box.Title(T("reallife_set_search"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_set_box_query", _query.Length > 0 ? _query : "-");
            box.Row("reallife_set_box_found", count.ToString());
            box.Line(_words.Count == 0 ? T("reallife_set_search_short") : count == 0 ? T("reallife_set_box_found_none") : T("reallife_set_box_found_pick"));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_set_search_again", () => UiClone.Safe(AskSearch));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_set_back", Back);
            Guide.Point(box, GuideTitle);
        }

        private TableRow Row(int index)
        {
            TableRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new TableRow();
            }
            _rows.Add(row.Reset(_ids[index]));
            row.Index = index;
            row.Pick = true;
            return row;
        }

        /// <summary>A text with its names filled: T("reallife_set_count", "on", "4", "total", "6").</summary>
        private static string T(string key, params string[] pairs)
        {
            string text = key.GetLocalization();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
            }
            return text;
        }
    }
}
