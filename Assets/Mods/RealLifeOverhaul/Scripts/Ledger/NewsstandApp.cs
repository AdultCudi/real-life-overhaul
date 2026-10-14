using System;
using System.Collections;
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
    /// Newsstand, the newspapers app on the phone: it opens in the full-screen phone as the mod's other apps do, in the
    /// game's frame (GameUi.AppShell, copied from MyEmployees) on two tabs:
    ///
    ///   Titles    the four papers and the pass, with what each covers, its price a week, the subscription and today's
    ///             edition; the white box has the one picked: subscribe, cancel or take it up again, and read it. With
    ///             no working connection at home the box says what is missing and opens the Utilities app.
    ///   Reading   the picked title's edition laid over the table as one column of newsprint, scrolled with the mouse
    ///             wheel (smoothly, NewsstandWheel) or by dragging it; the box picks an earlier edition of the last few
    ///             mornings and, for the City Ledger, opens the printed page.
    ///
    /// An edition is written for the phone once (NewsstandPage) and kept while it is the edition of that morning and the
    /// language does not change; opening it again only sets the kept blocks. Nothing runs while the app is closed, and
    /// while it is open only the game's own scroll and, while the wheel moves the page, the glide.
    /// </summary>
    public sealed class NewsstandApp
    {
        public const string AppId = "RealLifeNewsstand";
        private const int TabTitles = 0, TabReading = 1;
        private const int WrittenMost = 12;
        private const string GuideTitle = "reallife_meta_title_ledger";   // the Guide's page the box's pages point to at their foot
        private static readonly string[] AppGlyphs = { "Icon-Edit", "Icon-Info", "Icon-Chat" };
        private static readonly string[] TabKeys = { "reallife_ledger_online_tab_titles", "reallife_ledger_online_tab_reading" };

        // The paper's inks, as the printed page has them.
        private static readonly Color Newsprint = new Color(0.957f, 0.945f, 0.914f, 1f);
        private static readonly Color Ink = new Color(0.10f, 0.10f, 0.11f, 1f);
        private static readonly Color RuleInk = new Color(0.12f, 0.12f, 0.13f, 1f);

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

        // One title's edition as written for the phone, kept.
        private sealed class Written
        {
            public int Title, Day, Language;
            public LedgerEdition Edition;
            public readonly List<NewsBlock> Blocks = new List<NewsBlock>(40);
        }

        public NewsstandApp(LedgerDirector director)
        {
            _director = director;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        // ---------------------------------------------------------------- on the phone

        /// <summary>Once a second while Newsstand runs: the app among the game's apps (the phone is built a little after the city).</summary>
        public void Install()
        {
            if (_app != null || _gaveUp)
            {
                return;
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
                    LedgerDirector.Logger?.Warn("Newsstand: the game's phone is not built as this version expects, so the app is left off. Subscriptions still renew, and the printed paper still works.");
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

        /// <summary>Something has changed (an edition came, a new one was written): the app is written again if it is open.</summary>
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

        // ---------------------------------------------------------------- building it from the game's parts

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

        // The reading page: a sheet of newsprint over the table (the game's own list background, tinted), the game's
        // ScrollRect on it with a masked viewport and the column of text inside, and a copy of the table's scroll bar.
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
            paper.color = Newsprint;
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

        // A copy of the table's own scroll bar for the page, its scripts that are not Unity's left off. Without one the page still scrolls.
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

        // ---------------------------------------------------------------- opening and the tabs

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

        // ---------------------------------------------------------------- the titles

        private void WriteTitles(InfoBox box, NewsstandState s)
        {
            Rows();
            int today = _director.Today, level = Prices.Level;
            bool outNow = Out(today);
            for (int i = 0; i < NewsstandCatalogue.Subscriptions; i++)
            {
                OnlineSub x = s.Subs[i];
                TableRow row = Row("sub|" + i);
                row.Pick = true;
                row.Index = i;
                bool title = i < NewsstandCatalogue.Titles;
                bool inPass = title && !x.Subscribed && s.Subs[NewsstandCatalogue.Pass].Subscribed;
                row.Set(0, LedgerText.T(NewsstandCatalogue.NameKeys[i]));
                row.Set(1, LedgerText.T(NewsstandCatalogue.AboutKeys[i]));
                row.Set(2, LedgerText.Money(NewsstandRules.WeekPrice(i, level)));
                row.Set(3, x.Subscribed ? (x.Cancelled ? LedgerText.Fill("reallife_ledger_online_state_ends", "day", x.PaidUntil.ToString()) : LedgerText.Fill("reallife_ledger_online_state_renews", "day", (x.PaidUntil + 1).ToString()))
                    : inPass ? LedgerText.T("reallife_ledger_online_state_in_pass") : "-", x.Subscribed ? (x.Cancelled ? TableRow.Yellow : TableRow.Green) : inPass ? TableRow.Green : TableRow.Plain);
                if (!title)
                {
                    row.Set(4, "-");
                }
                else if (NewsstandRules.Has(s, i, today))
                {
                    row.Set(4, LedgerText.T("reallife_ledger_online_today_in"), TableRow.Green);
                }
                else if (NewsstandRules.Covered(s, i))
                {
                    row.Set(4, outNow ? LedgerText.T("reallife_ledger_online_today_waiting") : LedgerText.Fill("reallife_ledger_online_today_at", "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour)),
                        outNow ? TableRow.Red : TableRow.Plain);
                }
                else
                {
                    row.Set(4, "-");
                }
            }
            UiClone.Words(_shell.Header, LedgerText.Fill("reallife_ledger_online_headline", "count", NewsstandRules.Running(s).ToString(), "price", LedgerText.Money(NewsstandRules.Weekly(s, level))));
            _table.Show(Titles, _rows, _pick >= 0 ? "sub|" + _pick : null);
            if (_pick >= 0 && _pick < NewsstandCatalogue.Subscriptions)
            {
                WriteSub(box, s, _pick, today, outNow);
            }
            else
            {
                WriteFront(box, s, today);
            }
        }

        // Nothing picked: what Newsstand is, the connection at home, and the way to read what came.
        private void WriteFront(InfoBox box, NewsstandState s, int today)
        {
            NetState net = _director.Net(out string home);
            box.Title(LedgerText.T("reallife_ledger_online_app"));
            if (!NewsstandRules.Works(net))
            {
                box.Line(LedgerText.T(net == NetState.NoHome ? "reallife_ledger_online_box_need_home" : "reallife_ledger_online_box_need_net"), CellTone.Yellow);
            }
            box.Row("reallife_ledger_online_box_net", LedgerPage.Plain(LedgerDirector.NetWords(net, home)));
            box.Note(LedgerText.T("reallife_ledger_online_box_pick"));
            Transform buttons = box.ButtonRow();
            int first = FirstReadable(s, today);
            if (first >= 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ledger_online_read", () => UiClone.Safe(() => Read(first)));
            }
            if (!NewsstandRules.Works(net) && net != NetState.NoHome)
            {
                UtilitiesButton(buttons);
            }
            box.Note(LedgerText.Fill("reallife_ledger_online_box_intro", "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour)));
            Meta.Guide.Point(box, GuideTitle);
        }

        // A title or the pass picked: its price, its subscription, today's edition, and what can be done.
        private void WriteSub(InfoBox box, NewsstandState s, int sub, int today, bool outNow)
        {
            OnlineSub x = s.Subs[sub];
            OnlineSub pass = s.Subs[NewsstandCatalogue.Pass];
            bool title = sub < NewsstandCatalogue.Titles;
            int price = _director.OnlinePrice(sub);
            box.Title(LedgerText.T(NewsstandCatalogue.NameKeys[sub]));
            string state = x.Subscribed
                ? (x.Cancelled ? LedgerText.Fill("reallife_ledger_online_box_cancelled", "day", x.PaidUntil.ToString())
                    : LedgerText.Fill("reallife_ledger_online_box_running", "paid", x.PaidUntil.ToString(), "next", (x.PaidUntil + 1).ToString(), "price", LedgerText.Money(price)))
                : title && pass.Subscribed ? LedgerText.T("reallife_ledger_online_box_by_pass") : LedgerText.T("reallife_ledger_online_box_none");
            box.Row("reallife_ledger_online_box_sub", LedgerPage.Plain(state));
            if (title)
            {
                string edition = NewsstandRules.Has(s, sub, today) ? LedgerText.T("reallife_ledger_online_today_in")
                    : !NewsstandRules.Covered(s, sub) ? "-"
                    : outNow ? LedgerText.T("reallife_ledger_online_today_waiting") : LedgerText.Fill("reallife_ledger_online_today_at", "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour));
                box.Row("reallife_ledger_online_box_today", LedgerPage.Plain(edition));
                Readable(s, sub, today);
                if (_days.Count > 0)
                {
                    box.Row("reallife_ledger_online_box_editions", LedgerPage.Plain(_days.Count.ToString()));
                }
            }
            NetState net = NetState.Online;
            bool canTake = !x.Subscribed && !(title && pass.Subscribed && !pass.Cancelled);
            if (canTake)
            {
                net = _director.Net(out string home);
                if (!NewsstandRules.Works(net))
                {
                    box.Line(LedgerDirector.NetWords(net, home));
                    box.Line(LedgerText.T(net == NetState.NoHome ? "reallife_ledger_online_box_need_home" : "reallife_ledger_online_box_need_net"), CellTone.Yellow);
                }
            }
            box.Row("reallife_ledger_online_box_price", LedgerPage.Plain(LedgerText.Money(price)));
            box.Row("reallife_ledger_online_box_covers", LedgerPage.Plain(LedgerText.T(NewsstandCatalogue.AboutKeys[sub])));
            Transform buttons = box.ButtonRow();
            if (title && _days.Count > 0)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ledger_online_read", () => UiClone.Safe(() => Read(sub)));
            }
            if (canTake && NewsstandRules.Works(net))
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ledger_online_subscribe", () => UiClone.Safe(() => Subscribe(sub)));
            }
            else if (canTake && net != NetState.NoHome)
            {
                UtilitiesButton(buttons);
            }
            if (x.Subscribed && x.Cancelled)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ledger_online_resume", () => UiClone.Safe(() => Subscribe(sub)));
            }
            if (x.Subscribed && !x.Cancelled)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_ledger_online_cancel", () => UiClone.Safe(() =>
                {
                    if (_director.CancelOnline(sub))
                    {
                        Say(NotificationType.Info, "reallife_ledger_online_note_cancelled", "title", LedgerText.T(NewsstandCatalogue.NameKeys[sub]), "day", x.PaidUntil.ToString());
                    }
                    Refresh();
                }));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ledger_online_back", Back);
            if (!title && !x.Subscribed)
            {
                int sum = 0;
                for (int t = 0; t < NewsstandCatalogue.Titles; t++)
                {
                    sum += _director.OnlinePrice(t);
                }
                box.Note(LedgerText.Fill("reallife_ledger_online_box_pass_note", "price", LedgerText.Money(price), "sum", LedgerText.Money(sum)));
            }
            box.Line("<i>" + LedgerPage.Plain(LedgerText.T(title ? NewsstandCatalogue.TaglineKeys[sub] : NewsstandCatalogue.AboutKeys[sub])) + "</i>");
            Meta.Guide.Point(box, GuideTitle);
        }

        private void Subscribe(int sub)
        {
            string name = LedgerText.T(NewsstandCatalogue.NameKeys[sub]);
            int price = _director.OnlinePrice(sub);
            OnlineRefusal refusal = _director.SubscribeOnline(sub, out bool resumed);
            switch (refusal)
            {
                case OnlineRefusal.None:
                    NewsstandState s = _director.Online;
                    bool came = s != null && sub < NewsstandCatalogue.Titles && NewsstandRules.Has(s, sub, _director.Today);
                    Say(NotificationType.Success, resumed ? "reallife_ledger_online_note_resumed" : sub == NewsstandCatalogue.Pass ? "reallife_ledger_online_note_pass"
                        : came ? "reallife_ledger_online_note_subscribed_in" : "reallife_ledger_online_note_subscribed", "title", name, "price", LedgerText.Money(price),
                        "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour));
                    break;
                case OnlineRefusal.NoMoney:
                    Say(NotificationType.Warning, "reallife_ledger_online_note_no_money", "title", name, "price", LedgerText.Money(price));
                    break;
                case OnlineRefusal.NoHome:
                    Say(NotificationType.Warning, "reallife_ledger_online_box_need_home");
                    break;
                case OnlineRefusal.NoNet:
                    Say(NotificationType.Warning, "reallife_ledger_online_box_need_net");
                    break;
                case OnlineRefusal.InPass:
                    Say(NotificationType.Info, "reallife_ledger_online_box_by_pass");
                    break;
            }
            Refresh();
        }

        private void UtilitiesButton(Transform buttons)
        {
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ledger_online_utilities", () => UiClone.Safe(() =>
            {
                if (!_director.OpenUtilities())
                {
                    Say(NotificationType.Info, "reallife_ledger_online_no_utilities");
                }
            }));
        }

        // ---------------------------------------------------------------- reading

        private void WriteReading(InfoBox box, NewsstandState s)
        {
            int today = _director.Today;
            if (_title < 0 || _title >= NewsstandCatalogue.Titles || !Readable(s, _title, today))
            {
                _title = FirstReadable(s, today);
                if (_title >= 0)
                {
                    Readable(s, _title, today);
                }
            }
            if (_title < 0)
            {
                UiClone.Words(_shell.Header, LedgerText.T("reallife_ledger_online_app"));
                box.Title(LedgerText.T("reallife_ledger_online_tab_reading"));
                box.Line(LedgerText.Fill("reallife_ledger_online_nothing", "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_ledger_online_to_titles", () => UiClone.Safe(() => ShowTab(TabTitles)));
                Meta.Guide.Point(box, GuideTitle);
                _writer.Empty(LedgerText.Fill("reallife_ledger_online_nothing", "hour", LedgerText.Hour(LedgerCatalogue.DeliveryHour)), _scratch);
                Sheet(true);
                Lay(_scratch, true);
                return;
            }
            if (!_days.Contains(_day))
            {
                _day = _days[0];
            }
            int title = _title;
            LedgerEdition e = _director.OnlineEdition(_day);
            string name = LedgerText.T(NewsstandCatalogue.NameKeys[title]);
            UiClone.Words(_shell.Header, LedgerText.Fill("reallife_ledger_online_headline_reading", "title", name, "day", DayWords(_day, today)));
            box.Title(name);
            if (!NewsstandRules.Covered(s, title))
            {
                box.Note(LedgerText.T("reallife_ledger_online_box_kept"));
            }
            box.Row("reallife_ledger_online_box_edition", LedgerPage.Plain(DayWords(_day, today)));
            if (_days.Count > 1)
            {
                int at = _days.IndexOf(_day);
                box.Selector(_days.Count, at, i => DayWords(_days[i], today), i => UiClone.Safe(() =>
                {
                    _day = _days[i];
                    Refresh();
                }));
            }
            Transform buttons = box.ButtonRow();
            if (title == NewsstandCatalogue.Ledger && e != null)
            {
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_ledger_online_printed", () => UiClone.Safe(() =>
                {
                    if (!_director.ReadPrinted(e))
                    {
                        Say(NotificationType.Info, "reallife_ledger_online_no_printed");
                    }
                }));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_ledger_online_to_titles", () => UiClone.Safe(() => ShowTab(TabTitles)));
            box.Line(LedgerText.T("reallife_ledger_online_box_scroll"));
            Meta.Guide.Point(box, GuideTitle);
            Sheet(true);
            List<NewsBlock> blocks = e != null ? Page(title, _day, e) : null;
            if (blocks == null)
            {
                _writer.Empty(LedgerText.T("reallife_ledger_online_gone"), _scratch);
                blocks = _scratch;
            }
            Lay(blocks, ReferenceEquals(blocks, _scratch));
        }

        // A title's edition as written for the phone: the one kept, or written now and kept.
        private List<NewsBlock> Page(int title, int day, LedgerEdition e)
        {
            for (int i = 0; i < _written.Count; i++)
            {
                Written w = _written[i];
                if (w.Title == title && w.Day == day && w.Language == _language && ReferenceEquals(w.Edition, e))
                {
                    return w.Blocks;
                }
            }
            Written made = null;
            for (int i = _written.Count - 1; i >= 0; i--)
            {
                Written w = _written[i];
                if ((w.Title == title && w.Day == day) || w.Day <= _director.Today - NewsstandCatalogue.KeepDays)
                {
                    made = made ?? w;
                    _written.RemoveAt(i);
                }
            }
            if (made == null && _written.Count >= WrittenMost)
            {
                made = _written[0];
                _written.RemoveAt(0);
            }
            made = made ?? new Written();
            if (ReferenceEquals(made.Blocks, _shown))
            {
                _shown = null;   // its blocks are written again: the page is set again too
            }
            made.Title = title;
            made.Day = day;
            made.Language = _language;
            made.Edition = e;
            float started = Time.realtimeSinceStartup;
            _writer.Write(e, title, made.Blocks);
            _written.Add(made);
            if (Meta.Dev.Verbose)
            {
                LedgerDirector.Logger?.Info("Newsstand: " + NewsstandCatalogue.NameKeys[title] + " of day " + day + " written for the phone: " + made.Blocks.Count + " blocks, "
                                            + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms.");
            }
            return made.Blocks;
        }

        // ---------------------------------------------------------------- setting the page

        private void Sheet(bool on)
        {
            if (_sheet != null && _sheet.gameObject.activeSelf != on)
            {
                _sheet.gameObject.SetActive(on);
                if (on)
                {
                    _sheet.SetAsLastSibling();   // over whatever the table has put in the list since
                }
            }
            if (!on && _glide != null)
            {
                _glide.Stop();
            }
        }

        // The blocks set one under the next in a column of readable width, in the middle of the sheet; the page goes to
        // its top when it is another page than the one shown.
        private void Lay(List<NewsBlock> blocks, bool fresh)
        {
            if (_viewport == null || _content == null)
            {
                return;
            }
            float width = _viewport.rect.width;
            if (width < 200f)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_shell.Root.transform);
                width = _viewport.rect.width;
            }
            if (width < 200f)
            {
                LaterOnce();   // the frame has not been laid out yet (the first opening): set once it has
                return;
            }
            bool other = fresh || !ReferenceEquals(blocks, _shown) || Mathf.Abs(width - _shownWidth) > 1f;
            if (!other)
            {
                return;   // the same page at the same width: it is set already, and stays where it was scrolled to
            }
            _shown = blocks;
            _shownWidth = width;
            if (_gameSize <= 0f)
            {
                TextAt(0);   // the first text made reads the game's own size, which the page's type is set against
            }
            float u = Mathf.Clamp(Mathf.Max(_gameSize, 26f), 24f, 40f);
            float column = Mathf.Min(width - 2.4f * u, 30f * u);
            float px = Hairline();
            float y = 1.2f * u;
            int texts = 0, rules = 0;
            for (int i = 0; i < blocks.Count; i++)
            {
                NewsBlock b = blocks[i];
                if (b.Kind == NewsstandPage.Rule || b.Kind == NewsstandPage.Double)
                {
                    y += 0.35f * u;
                    Image rule = RuleAt(rules++);
                    float thick = b.Kind == NewsstandPage.Double ? 3f * px : px;
                    Put(rule.rectTransform, 0f, y, column, thick);
                    if (b.Kind == NewsstandPage.Double)
                    {
                        Put(RuleAt(rules++).rectTransform, 0f, y + thick + 3f * px, column, px);
                        y += 4f * px;
                    }
                    y += thick + 0.45f * u;
                    continue;
                }
                TMP_Text t = TextAt(texts++);
                Style(t, b.Kind, u);
                float h = t.GetPreferredValues(b.Text, column, 0f).y;
                UiClone.Words(t, b.Text);
                Put(t.rectTransform, 0f, y, column, h + 2f);
                y += h + After(b.Kind) * u;
            }
            for (int i = texts; i < _texts.Count; i++)
            {
                if (_texts[i].gameObject.activeSelf)
                {
                    _texts[i].gameObject.SetActive(false);
                }
            }
            for (int i = rules; i < _rules.Count; i++)
            {
                if (_rules[i].gameObject.activeSelf)
                {
                    _rules[i].gameObject.SetActive(false);
                }
            }
            _content.sizeDelta = new Vector2(column, y + 1.5f * u);
            if (_wheel != null)
            {
                _wheel.Step = 4.2f * u;
            }
            if (_glide != null)
            {
                _glide.Top();
            }
        }

        // How each kind of block is set: its size against the body's, its alignment and spacing.
        private void Style(TMP_Text t, byte kind, float u)
        {
            float size = 1f;
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft;
            float paragraph = 0f;
            switch (kind)
            {
                case NewsstandPage.Masthead: size = 2.2f; align = TextAlignmentOptions.Top; break;
                case NewsstandPage.Centre: size = 0.86f; align = TextAlignmentOptions.Top; break;
                case NewsstandPage.Label: size = 0.9f; break;
                case NewsstandPage.Table: size = 0.9f; break;
                case NewsstandPage.Foot: size = 0.76f; align = TextAlignmentOptions.Top; break;
                default: paragraph = 45f; break;
            }
            t.fontSize = size * u;
            t.alignment = align;
            t.paragraphSpacing = paragraph;
            t.lineSpacing = kind == NewsstandPage.Masthead ? -18f : kind == NewsstandPage.Table ? -6f : 0f;
        }

        private static float After(byte kind)
        {
            switch (kind)
            {
                case NewsstandPage.Masthead: return 0.2f;
                case NewsstandPage.Centre: return 0.2f;
                case NewsstandPage.Label: return 0.35f;
                case NewsstandPage.Table: return 0.55f;
                case NewsstandPage.Foot: return 0.4f;
                default: return 0.75f;
            }
        }

        // A text of the page: the game's own line (the white box's), lifted out of its layout, in the paper's face and ink.
        private TMP_Text TextAt(int i)
        {
            while (_texts.Count <= i)
            {
                TMP_Text t = _shell.Box.Line(string.Empty, _content);
                t.gameObject.name = "Block" + _texts.Count;
                Loose(t.gameObject);
                if (_gameSize <= 0f)
                {
                    _gameSize = t.fontSize;
                }
                if (_face != null)
                {
                    t.font = _face;
                }
                t.color = Ink;
                t.fontStyle = FontStyles.Normal;
                t.richText = true;
                t.raycastTarget = false;
                t.enableAutoSizing = false;
                t.margin = Vector4.zero;
                t.textWrappingMode = TextWrappingModes.Normal;
                t.overflowMode = TextOverflowModes.Overflow;
                t.characterSpacing = 0f;
                t.wordSpacing = 0f;
                _texts.Add(t);
            }
            TMP_Text text = _texts[i];
            if (!text.gameObject.activeSelf)
            {
                text.gameObject.SetActive(true);
            }
            return text;
        }

        private Image RuleAt(int i)
        {
            while (_rules.Count <= i)
            {
                var go = new GameObject("Rule" + _rules.Count, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_content, false);
                var image = go.GetComponent<Image>();
                image.color = RuleInk;
                image.raycastTarget = false;
                _rules.Add(image);
            }
            Image rule = _rules[i];
            if (!rule.gameObject.activeSelf)
            {
                rule.gameObject.SetActive(true);
            }
            return rule;
        }

        // One pixel of the screen in canvas units, however the canvas is scaled.
        private float Hairline()
        {
            Canvas canvas = _sheet != null ? _sheet.GetComponentInParent<Canvas>() : null;
            float scale = canvas != null && canvas.rootCanvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            return Mathf.Max(1f, 1f / Mathf.Max(0.05f, scale));
        }

        // The page set again a frame later, once the frame has been laid out (at most one wait at a time).
        private void LaterOnce()
        {
            if (_later || _director == null)
            {
                return;
            }
            _later = true;
            _director.StartCoroutine(Guard.Routine(Later()));
        }

        private IEnumerator Later()
        {
            yield return null;
            _later = false;
            if (Showing && _tab == TabReading)
            {
                UiClone.Safe(Refresh);
            }
        }

        private static void Put(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
        }

        private static void Loose(GameObject part)
        {
            var layout = part.GetComponent<LayoutElement>() ?? part.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            var fitter = part.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }
        }

        // ---------------------------------------------------------------- the words

        // The days a title can be read on into _days, newest first, those whose edition is still kept. False if none.
        private bool Readable(NewsstandState s, int title, int today)
        {
            NewsstandRules.Days(s, title, today, _days);
            for (int i = _days.Count - 1; i >= 0; i--)
            {
                if (_director.OnlineEdition(_days[i]) == null)
                {
                    _days.RemoveAt(i);
                }
            }
            return _days.Count > 0;
        }

        // The first title with something to read: today's edition first, else the latest that came. -1 for none.
        private int FirstReadable(NewsstandState s, int today)
        {
            int best = -1, bestDay = 0;
            for (int t = 0; t < NewsstandCatalogue.Titles; t++)
            {
                if (Readable(s, t, today) && _days[0] > bestDay)
                {
                    best = t;
                    bestDay = _days[0];
                }
            }
            return best;
        }

        private bool Out(int today)
        {
            LedgerEdition latest = _director.Latest;
            return latest != null && latest.Day == today;
        }

        private static string DayWords(int day, int today)
        {
            return day == today
                ? LedgerText.Fill("reallife_ledger_online_day_today", "day", day.ToString())
                : LedgerText.Fill("reallife_ledger_online_day", "weekday", LedgerPage.Weekday(day), "day", day.ToString());
        }

        private static string Pair(string labelKey, string value)
        {
            return LedgerText.T(labelKey) + ": <b>" + LedgerPage.Plain(value) + "</b>";
        }

        private static void Say(NotificationType type, string key, params string[] pairs)
        {
            Notifications.Show(type, key, pairs.Length > 0 ? LedgerText.Data(pairs) : null, 6f);
        }

        // The table's rows go back to the pool before they are written again (only for the table: while the page is read
        // the table under it keeps the rows it shows).
        private void Rows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _pool.Add(_rows[i]);
            }
            _rows.Clear();
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
            _rows.Add(row.Reset(id));
            return row;
        }
    }
}
