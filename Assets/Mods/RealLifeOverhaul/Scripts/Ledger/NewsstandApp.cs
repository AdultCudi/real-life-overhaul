using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// Newsstand, the newspapers app on the phone (GameUi.AppShell): its titles with their subscriptions, and a reading
    /// page. An edition is written for the phone once (NewsstandPage) and kept; nothing runs while the app is closed.
    /// </summary>
    public sealed partial class NewsstandApp
    {
        public const string AppId = "RealLifeNewsstand";
        private const int TabTitles = 0, TabReading = 1;
        private const int WrittenMost = 12;                               // editions written for the phone kept at most
        private const string GuideTitle = "reallife_meta_title_ledger";   // the Guide's page the box points to
        private static readonly string[] AppGlyphs = { "Icon-Edit", "Icon-Info", "Icon-Chat" };
        private static readonly string[] TabKeys = { "reallife_ledger_online_tab_titles", "reallife_ledger_online_tab_reading" };

        /// <summary>0 title, 1 what it covers, 2 a week, 3 the subscription, 4 today's edition. Rows as written.</summary>
        internal static readonly TableSpec Titles = new TableSpec("Newsstand", "Titles")
            .Column("reallife_ledger_online_col_title", 440f, flexible: true)
            .Column("reallife_ledger_online_col_covers", 560f)
            .Column("reallife_ledger_online_col_week", 170f)
            .Column("reallife_ledger_online_col_sub", 340f)
            .Column("reallife_ledger_online_col_today", 300f, right: true)
            .Empty("reallife_ledger_online_empty");

        private static int _language;   // counted up when the game's language changes: what was written in the old one is written again

        private readonly LedgerDirector _director;
        private readonly NewsstandPage _writer = new NewsstandPage();
        private readonly List<TableRow> _rows = new List<TableRow>(NewsstandCatalogue.Subscriptions);
        private readonly List<TableRow> _pool = new List<TableRow>(NewsstandCatalogue.Subscriptions);
        private readonly List<int> _days = new List<int>(NewsstandCatalogue.KeepDays);
        private readonly List<Written> _written = new List<Written>(WrittenMost);
        private readonly List<NewsBlock> _scratch = new List<NewsBlock>(8);
        private readonly List<TMP_Text> _texts = new List<TMP_Text>(32);
        private readonly List<Image> _rules = new List<Image>(24);
        private readonly Action _languageChanged = () => _language++;

        private PhoneApp _app;
        private bool _gaveUp, _listening;
        private AppShell _shell;
        private DataTable _table;
        private RectTransform _sheet, _viewport, _content;
        private ScrollRect _scroll;
        private NewsstandWheel _wheel;
        private NewsstandGlide _glide;
        private TMP_FontAsset _face;
        private float _gameSize;
        private bool _writing, _later;
        private int _tab;
        private int _pick = -1;        // the subscription picked in the table
        private int _title = -1;       // the title being read
        private int _day;              // the morning of the edition being read
        private int _wanted = -1;      // a title asked for before the app opened
        private List<NewsBlock> _shown;    // the blocks set on the page now
        private float _shownWidth;

        public NewsstandApp(LedgerDirector director)
        {
            _director = director;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>Once a second while Newsstand runs: the app among the game's apps (the phone is built a little after the city).</summary>
        public void Install()
        {
            if (_app != null || _gaveUp)
            {
                return;
            }
            if (!PhoneApp.PhoneUp)
            {
                return;   // the game's phone is not built yet: asked again next time, nothing made for it
            }
            GameSprites.Want(AppGlyphs);
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_ledger_online_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(128, 150, 186, 255), new Color32(44, 58, 96, 255), 0.42f, "AppIcon-Newsstand"),
                Build = Build,
                Opened = () => UiClone.Safe(Opened),
                Closed = Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    LedgerDirector.Logger?.Warn("Newsstand: the game's phone is not built as this version expects, so the app is left off. "
                                                + "Subscriptions still renew, and the printed paper still works.");
                }
                return;
            }
            if (!_listening)
            {
                LocalizorManager.OnLanguageChanged += _languageChanged;
                _listening = true;
            }
        }

        public void Remove()
        {
            if (_listening)
            {
                LocalizorManager.OnLanguageChanged -= _languageChanged;
                _listening = false;
            }
            if (_app != null)
            {
                _app.Remove();
                _app = null;
            }
            _shell = null;
            _table = null;
            _sheet = _viewport = _content = null;
            _scroll = null;
            _glide = null;
            _wheel = null;
            _texts.Clear();
            _rules.Clear();
            _written.Clear();
            _shown = null;
            _gaveUp = false;
        }

        /// <summary>Opens the app on a title's latest edition (a test, or another page asking).</summary>
        public void Open(int title)
        {
            if (_app == null || !_app.Installed)
            {
                return;
            }
            _wanted = title;
            if (Showing)
            {
                UiClone.Safe(Opened);
            }
            else
            {
                _app.Open();
            }
        }

        public void Changed()
        {
            if (Showing && !_writing)
            {
                UiClone.Safe(Refresh);
            }
        }

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        private GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("ledger: Newsstand being built from the game's MyEmployees");   // once per city
            _shell = AppShell.Make(menu, dormant, AppId);
            _table = DataTable.On(_shell, "Newsstand");
            if (_table == null || !_shell.SetColumns(Titles))
            {
                return null;
            }
            _table.Money = amount => LedgerText.Money((int)amount);
            _table.SelectionLost = Back;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => UiClone.Safe(() => ShowTab(tab)));
            }
            if (!BuildSheet())
            {
                return null;
            }
            return _shell.Root;
        }

        // The reading page: a tinted copy of the table's background with a ScrollRect, a masked viewport, a scroll bar.
        private bool BuildSheet()
        {
            var list = _shell.List as RectTransform;
            if (list == null)
            {
                UiClone.Fallback("Newsstand: the table is not laid out as expected, so there is no page to read on");
                return false;
            }
            Transform background = list.Find("ListBackground");
            GameObject sheet = background != null ? UnityEngine.Object.Instantiate(background.gameObject, list, false) : new GameObject("Sheet", typeof(RectTransform), typeof(Image));
            sheet.name = "Reading";
            for (int i = sheet.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(sheet.transform.GetChild(i).gameObject);
            }
            var paper = sheet.GetComponent<Image>() ?? sheet.AddComponent<Image>();
            paper.color = LedgerPage.Newsprint;
            paper.raycastTarget = true;   // the page takes the wheel and the drag
            var unlaid = sheet.GetComponent<LayoutElement>() ?? sheet.AddComponent<LayoutElement>();
            unlaid.ignoreLayout = true;
            _sheet = (RectTransform)sheet.transform;
            Stretch(_sheet, 0f);
            _sheet.SetAsLastSibling();

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(_sheet, false);
            _viewport = (RectTransform)viewport.transform;
            Stretch(_viewport, 10f);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(_viewport, false);
            _content = (RectTransform)content.transform;
            _content.anchorMin = _content.anchorMax = _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(100f, 100f);

            _scroll = sheet.AddComponent<ScrollRect>();
            _scroll.viewport = _viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.elasticity = 0.1f;
            _scroll.inertia = true;
            _scroll.decelerationRate = 0.135f;
            _scroll.scrollSensitivity = 0f;   // the wheel is the glide's: the ScrollRect's own jumps a whole notch at once
            Bar(list);
            _glide = sheet.AddComponent<NewsstandGlide>();
            _glide.Scroll = _scroll;
            _glide.enabled = false;
            _wheel = sheet.AddComponent<NewsstandWheel>();
            _wheel.Glide = _glide;
            _face = LedgerType.Find();
            sheet.SetActive(false);
            return true;
        }

        // A copy of the table's scroll bar without its scripts that are not Unity's; without it the page still scrolls.
        private void Bar(RectTransform list)
        {
            try
            {
                ScrollRect theirs = _shell.Scroller != null ? _shell.Scroller.GetComponent<ScrollRect>() : null;
                Scrollbar bar = theirs != null ? theirs.verticalScrollbar : null;
                if (bar == null)
                {
                    return;
                }
                GameObject copy = UnityEngine.Object.Instantiate(bar.gameObject, _sheet, false);
                copy.name = "ScrollBar";
                MonoBehaviour[] scripts = copy.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < scripts.Length; i++)
                {
                    string space = scripts[i] != null ? scripts[i].GetType().Namespace : null;
                    if (scripts[i] != null && (space == null || !space.StartsWith("UnityEngine", StringComparison.Ordinal)))
                    {
                        UnityEngine.Object.DestroyImmediate(scripts[i]);
                    }
                }
                var layout = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
                var rect = (RectTransform)copy.transform;
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(-6f, 0f);
                rect.sizeDelta = new Vector2(Mathf.Max(12f, ((RectTransform)bar.transform).rect.width), -24f);
                Scrollbar mine = copy.GetComponent<Scrollbar>();
                if (mine != null)
                {
                    mine.onValueChanged = new Scrollbar.ScrollEvent();
                    _scroll.verticalScrollbar = mine;
                    _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                }
                copy.SetActive(true);
            }
            catch (Exception e)
            {
                LedgerDirector.Logger?.Error(e);
            }
        }

        private static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        private void Opened()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            if (_wanted >= 0)
            {
                _title = _wanted;
                _day = 0;
                _tab = TabReading;
                _wanted = -1;
            }
            Refresh();
        }

        private void Closed()
        {
            if (_glide != null)
            {
                _glide.Stop();
            }
            if (_table != null)
            {
                _table.ClearSelection();
            }
            _pick = -1;
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab)
            {
                _tab = tab;
                Refresh();
            }
        }

        private void RowClicked(TableRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            if (row.Index == _pick)
            {
                return;
            }
            _pick = row.Index;
            Refresh();
        }

        private void Back()
        {
            if (_pick < 0)
            {
                return;
            }
            _pick = -1;
            _table.ClearSelection();
            Refresh();
        }

        private void Read(int title)
        {
            _title = title;
            _day = 0;
            _tab = TabReading;
            Refresh();
        }

        private void Refresh()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                _shell.ShowTab(_tab);
                InfoBox box = _shell.Box;
                box.Clear();
                NewsstandState s = _director.Online;
                if (s == null)
                {
                    UiClone.Words(_shell.Header, LedgerText.T("reallife_ledger_online_app"));
                    box.Title(LedgerText.T("reallife_ledger_online_off_title"));
                    box.Note(LedgerText.T("reallife_ledger_online_off_body"));
                    Sheet(false);
                    Rows();
                    _table.Show(Titles, _rows, null);
                }
                else if (_tab == TabReading)
                {
                    WriteReading(box, s);
                }
                else
                {
                    Sheet(false);
                    WriteTitles(box, s);
                }
                box.Done();
            }
            finally
            {
                _writing = false;
            }
        }

        private static void Say(NotificationType type, string key, params string[] pairs)
        {
            Notifications.Show(type, key, pairs.Length > 0 ? LedgerText.Data(pairs) : null, 6f);
        }
    }
}
