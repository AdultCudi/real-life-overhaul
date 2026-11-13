using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A tech firm's whole page on the game's own widgets (GameUi.AppShell), for a player with the RLO interface off: the page the kit draws
    /// (StudioPageScreen), made from the same page (StudioViews.Page, SectorViews.Page), the same figures, acts and extras (StudioPages,
    /// TechLive.Extras) and pressed through the same director call (TechDirector.PageAct). The white box has the next step, the actions, the
    /// figures with the takings' line, the team's skill as bars, the facts and the note; the table's tabs are the games or products, the
    /// market's rivals and the firm's people. Opened from the firm's own page in the Tech app. Written when opened, after a press and when the
    /// firms change; nothing runs while it is closed.
    /// </summary>
    public sealed class StudioPageApp
    {
        public const string PageId = "RealLifeFirmPage";   // not an AppId: it has no button and no place among the phone's apps, as the kit's pages have none

        private const int Games = 0, Rivals = 1, People = 2;
        private const float SparkWidth = 760f, SparkHeight = 120f;
        private const int SparkPixelsWide = 380, SparkPixelsHigh = 60;

        private static readonly TableSpec Off = new TableSpec("FirmPage", "Off").Column("reallife_ui_page_empty", 600f, TableSort.None, flexible: true);

        private static readonly string[] PeopleKeys = { "reallife_ui_tech_col_person", "reallife_roles_col_role", "reallife_ui_tech_skill", "reallife_ui_tech_morale" };

        private readonly TechDirector _director;
        private readonly TechApp.StudioWords _words = new TechApp.StudioWords();
        private readonly List<ViewAction> _acts = new List<ViewAction>(StudioPages.MostOthers + 1);
        private readonly List<TableRow> _rows = new List<TableRow>(32);
        private readonly List<TableRow> _pool = new List<TableRow>(32);
        private readonly Dictionary<string, TableSpec> _specs = new Dictionary<string, TableSpec>(8);
        private PhoneApp _app;
        private AppShell _shell;
        private DataTable _table;
        private Texture2D _spark;
        private string _place;
        private int _tab;
        private bool _writing, _gaveUp;

        public StudioPageApp(TechDirector director)
        {
            _director = director;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>The page among the game's apps, with no button of its own (the Tech app's firm page opens it). Once the Tech app is on the phone.</summary>
        public void Install()
        {
            if (_app != null || _gaveUp || !PhoneApp.PhoneUp)
            {
                return;
            }
            var def = new PhoneAppDef
            {
                Id = PageId,
                TitleKey = "reallife_ui_page_studio",
                NoButtons = true,
                Build = Build,
                Opened = () => UiClone.Safe(Refresh),
                Closed = Closed,
                Kit = () => new StudioPageScreen(_place ?? FirstPlace()) { Close = ShellPages.CloseMenu },   // the kit's page for the firm, when it and its switch are on
                KitScreen = "studio",
                KitIcon = UiIcon.GameController
            };
            _app = PhoneApp.Install(def, out bool tryAgain);
            _gaveUp = _app == null && !tryAgain;
            UiShell.Face(_app, def);
            if (_gaveUp)
            {
                TechDirector.Logger?.Warn("Tech: the game's menu is not built as this version expects, so a firm's whole page is not shown on the game's widgets.");
            }
        }

        public void Remove()
        {
            if (_app != null)
            {
                _app.Remove();
                _app = null;
            }
            _shell = null;
            _table = null;
            DropSpark();
        }

        // The first firm's place, for the kit's face of a page opened before any firm was asked for; null with no firm.
        private string FirstPlace() => _director.FirmPlaces.Count > 0 ? _director.FirmPlaces[0].File.Place : null;

        /// <summary>Opens the page on a firm; false while the page is not among the apps.</summary>
        public bool Open(string place)
        {
            if (_app == null || string.IsNullOrEmpty(place))
            {
                return false;
            }
            if (place != _place)
            {
                _tab = Games;
            }
            _place = place;
            if (Showing)
            {
                Refresh();
            }
            else
            {
                _app.Open();
            }
            return true;
        }

        /// <summary>The firms changed: the page open is written again.</summary>
        public void Changed()
        {
            if (Showing)
            {
                Refresh();
            }
        }

        private GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("firm page: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, PageId);
            _table = DataTable.On(_shell, "FirmPage");
            if (_table == null || !_shell.SetTabCount(3) || !_shell.SetColumns(Off))
            {
                return null;
            }
            _table.Money = amount => TechText.Cents((long)amount);
            for (int i = 0; i < 3; i++)
            {
                int tab = i;
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        private void Closed()
        {
            DropSpark();
        }

        private void ShowTab(int tab)
        {
            _tab = tab;
            Refresh();
        }

        private TableRow Row(string id)
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
            row.Reset(id);
            row.Pick = false;
            row.Index = _rows.Count;
            _rows.Add(row);
            return row;
        }

        // The columns of a table by their keys, made once and kept (the player's order and filters live in the spec).
        private TableSpec SpecOf(string[] keys, bool[] figures, string emptyKey)
        {
            string id = string.Join(",", keys) + "|" + emptyKey;
            if (!_specs.TryGetValue(id, out TableSpec spec))
            {
                spec = new TableSpec("FirmPage", id);
                for (int i = 0; i < keys.Length && i < TableSpec.MaxColumns; i++)
                {
                    bool figure = figures != null && i < figures.Length && figures[i];
                    spec.Column(keys[i], i == 0 ? 420f : 200f, TableSort.None, i == 0, false, figure);
                }
                if (!string.IsNullOrEmpty(emptyKey))
                {
                    spec.Empty(emptyKey);
                }
                _specs[id] = spec;
            }
            return spec;
        }

        // ------------------------------------------------------------------ the write

        private void Refresh()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                InfoBox box = _shell.Box;
                box.Clear();
                TableSpec spec = Off;
                FirmPlace firm = _director.FirmOf(_place);
                bool studio = firm != null && _director.StudioRuns(firm.File.K);
                FirmPage page = firm == null ? null : studio ? _director.StudioFirmPage(firm, _words) : _director.SectorFirmPage(firm, _words);
                UiClone.Key(_shell.TabLabels[Games], studio ? "reallife_firmpage_tab_games" : "reallife_tech_tab_products");
                UiClone.Key(_shell.TabLabels[Rivals], "reallife_tech_button_rivals");
                UiClone.Key(_shell.TabLabels[People], "reallife_news_nav_people");
                _shell.ShowTab(_tab);
                if (page == null)
                {
                    UiClone.Words(_shell.Header, TechText.T("reallife_ui_page_studio"));
                    box.Title(TechText.T("reallife_ui_page_empty"));
                    box.Note(TechText.T("reallife_firmpage_gone"));
                }
                else
                {
                    StudioExtras extras = TechLive.Extras(_place);
                    StudioFaceModel m = StudioPages.Page(page, UiText.Words, _acts, extras);
                    UiClone.Words(_shell.Header, m.Title);
                    spec = WriteBox(box, m, page, extras);
                }
                box.Done();
                _table.Show(spec, _rows, null);
                if (_tab >= _shell.Tabs.Length)
                {
                    _tab = Games;
                }
            }
            finally
            {
                _writing = false;
            }
        }

        // The white box: the head, what to do next, the figures, the team, the facts and the buttons. Returns the table of the tab showing, its rows written.
        private TableSpec WriteBox(InfoBox box, StudioFaceModel m, FirmPage page, StudioExtras extras)
        {
            box.Title(m.Title);
            if (!string.IsNullOrEmpty(m.Next))
            {
                box.Line(m.Next, Tone(m.NextInk));
            }
            UiHeroModel hero = extras != null ? extras.Hero : null;
            if (hero != null)
            {
                if (string.IsNullOrEmpty(hero.Kicker))
                {
                    box.Section("reallife_ui_page_details");
                }
                else
                {
                    box.SectionWords(hero.Kicker);
                }
                box.Line(hero.Title);
                if (hero.Progress >= 0)
                {
                    box.MeterWords(hero.Line ?? string.Empty, hero.Progress / 100f, hero.Progress + "%", CellTone.Plain);
                }
                else if (!string.IsNullOrEmpty(hero.Line))
                {
                    box.Line(hero.Line);
                }
                if (!string.IsNullOrEmpty(hero.Note))
                {
                    box.Note(hero.Note);
                }
            }
            DropSpark();
            for (int i = 0; i < m.Figures.Length; i++)
            {
                UiFigureModel f = m.Figures[i];
                string value = string.IsNullOrEmpty(f.Change) ? f.Value : f.Value + "  " + f.Change;
                CellTone tone = string.IsNullOrEmpty(f.Change) ? CellTone.Plain : f.Good ? CellTone.Green : CellTone.Red;
                box.RowWords(f.Label, value, tone);
                if (f.Trend != null && f.Trend.Length > 1)
                {
                    MakeSpark(f.Trend);
                    box.Picture(_spark, SparkWidth, SparkHeight);
                }
            }
            if (page.Team.Count > 0)
            {
                box.Section(page.TeamKey);
                for (int i = 0; i < page.Team.Count; i++)
                {
                    ViewRole r = page.Team[i];
                    string group = TechText.Fill(StudioViews.GroupCountKey, "group", TechText.T(StudioViews.GroupKey(r.Group)), "count", r.Count.ToString());
                    box.MeterWords(group, r.Skill / 100f, r.Needed ? TechText.T(StudioViews.NeededKey) : r.Skill.ToString(), r.Needed ? CellTone.Yellow : CellTone.Plain);
                }
            }
            if (page.Facts.Count > 0)
            {
                box.Section(page.FactsKey);
                for (int i = 0; i < page.Facts.Count; i++)
                {
                    box.Row(page.Facts[i].LabelKey, StudioPages.Capital(page.Facts[i].Value));
                }
            }
            bool needed = false;
            for (int i = 0; i < page.Team.Count; i++)
            {
                needed |= page.Team[i].Needed;
            }
            if (extras != null && !string.IsNullOrEmpty(extras.Tickets))
            {
                box.Line(extras.Tickets, CellTone.Yellow);
            }
            if (needed)
            {
                box.Note(TechText.T("reallife_studio_page_needed_how"));
            }
            if (needed || (extras != null && !string.IsNullOrEmpty(extras.Tickets)))
            {
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ui_tech_open_roles", () => _director.OpenRoles(TechDirector.TabTeam, null));
            }
            for (int i = 0; i < _acts.Count; i++)
            {
                WriteAction(box, _acts[i], m.Actions[i].Kind == UiButtonKind.Primary);
            }
            Meta.Guide.Point(box, "reallife_meta_title_firms");
            return WriteTable(page, extras);
        }

        private void WriteAction(InfoBox box, ViewAction a, bool primary)
        {
            if (!a.Enabled && !string.IsNullOrEmpty(a.WhyKey))
            {
                box.Line(TechText.T(a.WhyKey), CellTone.Yellow);
            }
            UnityEngine.UI.Button button = InfoBox.Button(box.ButtonRow(), primary ? InfoBox.Positive : a.Danger ? InfoBox.Negative : InfoBox.Additional, a.Key, () => Press(a));
            if (button != null)
            {
                button.interactable = a.Enabled;
            }
        }

        // A press runs the director's act as the Tech app's own button does; a withdrawal asks first.
        private void Press(ViewAction a)
        {
            if (a.Danger)
            {
                GameConfirm.Show("reallife_tech_confirm_withdraw_title", "reallife_tech_confirm_withdraw_body", () => UiClone.Safe(() => Run(a)), null, null, null, true, true);
                return;
            }
            Run(a);
        }

        private void Run(ViewAction a)
        {
            _director.PageAct(_place, a.Act, a.Arg);
            Refresh();
        }

        private static CellTone Tone(UiRole ink)
        {
            return ink == UiRole.OkText ? CellTone.Green : ink == UiRole.WarnText ? CellTone.Yellow : ink == UiRole.DangerText ? CellTone.Red : CellTone.Plain;
        }

        // ------------------------------------------------------------------ the table of the tab showing

        private TableSpec WriteTable(FirmPage page, StudioExtras extras)
        {
            if (_tab == People)
            {
                UiPersonRow[] people = extras != null ? extras.People : new UiPersonRow[0];
                for (int i = 0; i < people.Length; i++)
                {
                    TableRow row = Row("p" + i);
                    row.Set(0, people[i].Name);
                    row.Set(1, people[i].Role ?? string.Empty);
                    row.Set(2, people[i].First.ToString(), people[i].First);
                    row.Set(3, people[i].Second.ToString(), people[i].Second);
                }
                return SpecOf(PeopleKeys, new[] { false, false, true, true }, "reallife_studio_page_none");
            }
            ViewTable table = _tab == Rivals ? page.More : page.Main;
            for (int r = 0; r < table.Rows.Count; r++)
            {
                TableRow row = Row("t" + r);
                string[] cells = table.Rows[r];
                for (int c = 0; c < cells.Length && c < TableSpec.MaxColumns; c++)
                {
                    row.Set(c, cells[c] ?? string.Empty);
                }
            }
            return SpecOf(table.ColumnKeys, table.Figures, table.EmptyKey);
        }

        // ------------------------------------------------------------------ the takings' line

        // The line of a week's takings, day by day, as a picture the white box takes: the line in the accent over a soft fill under it.
        private void MakeSpark(float[] trend)
        {
            DropSpark();
            int w = SparkPixelsWide, h = SparkPixelsHigh;
            _spark = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "FirmPageLine" };
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < trend.Length; i++)
            {
                low = Mathf.Min(low, trend[i]);
                high = Mathf.Max(high, trend[i]);
            }
            float span = Mathf.Max(1f, high - low);
            var pixels = new Color32[w * h];
            var line = new Color32(46, 160, 120, 255);
            var fill = new Color32(46, 160, 120, 70);
            for (int x = 0; x < w; x++)
            {
                float at = x / (float)(w - 1) * (trend.Length - 1);
                int i0 = Mathf.Min(trend.Length - 2, (int)at);
                float value = Mathf.Lerp(trend[i0], trend[i0 + 1], at - i0);
                int top = Mathf.Clamp(Mathf.RoundToInt((value - low) / span * (h - 8)) + 3, 2, h - 3);
                for (int y = 0; y <= top; y++)
                {
                    pixels[y * w + x] = y >= top - 1 ? line : fill;
                }
            }
            _spark.SetPixels32(pixels);
            _spark.Apply(false, true);
        }

        private void DropSpark()
        {
            if (_spark != null)
            {
                UnityEngine.Object.Destroy(_spark);
                _spark = null;
            }
        }
    }
}
