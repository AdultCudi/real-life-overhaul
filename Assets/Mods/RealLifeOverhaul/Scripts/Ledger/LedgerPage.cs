using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The City Ledger as a newspaper, in the game's full menu (the game stands still while it is open; the cancel key puts
    /// it down). The frame is the game's own (GameUi.AppShell, copied from MyEmployees, with no button on the phone: it is
    /// opened by picking up the paper), its tabs are the paper's pages, and over the frame lies one sheet of newsprint, the
    /// game's own white panel tinted, with the page on it made of the game's own texts, headlines and rules:
    ///
    ///   Front page    the masthead with its two ears (the weather, the city index) and the dateline; the lead story under
    ///                 its headline in two columns; the city, the economy and business in three columns of stories and
    ///                 briefs; down the side the markets (the index, the risers and fallers, the bank rate), the weather, and
    ///                 around town (people, what is on, letters and the rest)
    ///   Markets       the market's stories, the city index with its last weeks drawn, every sector, the risers, the
    ///                 fallers and the most traded
    ///   Share prices  every listed company's close and its move, by sector, in four columns, as a paper prints them
    ///
    /// Positions are fractions of the sheet, so the page fits whatever room the menu gives it, and the type is sized to the
    /// sheet when it is opened. Everything is written when the page is opened or turned (in the language of the moment),
    /// never per frame; the edition itself is put together once a morning (LedgerDirector).
    /// </summary>
    public sealed partial class LedgerPage
    {
        public const string AppId = "RealLifeLedger";
        public const int FromHome = 0, FromKiosk = 1, FromTest = 2;
        private const int Front = 0, Markets = 1, Prices = 2, Pages = 3;

        private static readonly string[] TabKeys = { "reallife_ledger_tab_front", "reallife_ledger_tab_markets", "reallife_ledger_tab_prices" };
        private static readonly string[] FromKeys = { "reallife_ledger_header_home", "reallife_ledger_header_kiosk", "reallife_ledger_header_test" };

        // The paper's inks: newsprint, black ink, a grey for the small print, the kicker's red, rising green and falling red.
        private static readonly Color Newsprint = new Color(0.957f, 0.945f, 0.914f, 1f);
        private static readonly Color Ink = new Color(0.10f, 0.10f, 0.11f, 1f);
        private static readonly Color Grey = new Color(0.36f, 0.36f, 0.37f, 1f);
        private static readonly Color RuleInk = new Color(0.14f, 0.14f, 0.15f, 1f);
        private static readonly Color KickerInk = new Color(0.60f, 0.14f, 0.09f, 1f);
        private const string UpInk = "#1E6E37", DownInk = "#A3271D", SoftInk = "#5B5B60";

        private static readonly Vector3[] Corners = new Vector3[4];

        private readonly LedgerDirector _director;
        private readonly StringBuilder _b = new StringBuilder(4096);
        private readonly List<LedgerPiece> _pieces = new List<LedgerPiece>(24);
        private readonly List<LedgerQuote> _quotes = new List<LedgerQuote>(160);
        private readonly List<TMP_Text> _sized = new List<TMP_Text>(64);
        private readonly List<float> _factors = new List<float>(64);
        private readonly RectTransform[] _pages = new RectTransform[Pages];

        private PhoneApp _app;
        private bool _gaveUp;
        private AppShell _shell;
        private RectTransform _layout, _left, _right, _tabs, _list, _sheet;
        private LedgerEdition _edition;
        private int _from;
        private int _page;
        private float _height;    // the sheet's height the type was last sized for

        // the front page
        private TMP_Text _fTitle, _fEarLeft, _fEarRight, _fDateLeft, _fDateMiddle, _fDateRight, _fKicker, _fHead, _fLead1, _fLead2, _fMarkets, _fWeather, _fAround;
        private readonly TMP_Text[] _fColumns = new TMP_Text[3];
        // the markets page
        private TMP_Text _mTitle, _mName, _mDate, _mHead, _mLead1, _mLead2, _mNews1, _mNews2, _mIndex, _mCaption, _mSectors, _mMovers;
        private LedgerSpark _mChart;
        // the share prices
        private TMP_Text _pTitle, _pName, _pDate, _pLegend;
        private readonly TMP_Text[] _pColumns = new TMP_Text[4];

        public LedgerPage(LedgerDirector director)
        {
            _director = director;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        // ---------------------------------------------------------------- on the phone

        /// <summary>Once a second while the paper runs: the page among the game's apps (the phone is built a little after the city).</summary>
        public void Install()
        {
            if (_app != null || _gaveUp)
            {
                return;
            }
            _app = PhoneApp.Install(new PhoneAppDef
            {
                Id = AppId,
                TitleKey = "reallife_ledger_title",
                NoButtons = true,
                Build = Build,
                Opened = () => UiClone.Safe(Opened),
                Closed = Closed
            }, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    LedgerDirector.Logger?.Warn("City Ledger: the game's menu is not built as this version expects, so the paper's page cannot be shown. Subscriptions and kiosks still work.");
                }
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
            _gaveUp = false;
        }

        /// <summary>Opens the page on an edition. False if the page could not be put in the menu.</summary>
        public bool Show(LedgerEdition edition, int from)
        {
            if (_app == null || !_app.Installed || edition == null)
            {
                return false;
            }
            if (!ReferenceEquals(edition, _edition))
            {
                _page = Front;   // a new paper opens at its front page
            }
            _edition = edition;
            _from = Mathf.Clamp(from, 0, FromKeys.Length - 1);
            if (Showing)
            {
                UiClone.Safe(Opened);
            }
            else
            {
                _app.Open();
            }
            return true;
        }

        /// <summary>A new edition was put together: written again if the page is open on the old one.</summary>
        public void Changed()
        {
            LedgerState state = _director.State;
            if (Showing && state != null && state.Edition != null)
            {
                _edition = state.Edition;
                UiClone.Safe(Opened);
            }
        }

        private void Opened()
        {
            if (_shell == null || _sheet == null)
            {
                return;
            }
            if (_edition == null)
            {
                LedgerState state = _director.State;
                _edition = state != null ? state.Edition : null;
            }
            Measure();
            UiClone.Words(_shell.Header, LedgerText.T(FromKeys[_from]));
            Turn(_page);
        }

        private void Closed()
        {
            // nothing runs while the page is down
        }

        // The page shown: its tab marked, its words written now.
        private void Turn(int page)
        {
            _page = Mathf.Clamp(page, 0, Pages - 1);
            _shell.ShowTab(_page);
            for (int i = 0; i < Pages; i++)
            {
                if (_pages[i] != null && _pages[i].gameObject.activeSelf != (i == _page))
                {
                    _pages[i].gameObject.SetActive(i == _page);
                }
            }
            if (_edition == null)
            {
                return;
            }
            switch (_page)
            {
                case Front: WriteFront(_edition); break;
                case Markets: WriteMarkets(_edition); break;
                default: WritePrices(_edition); break;
            }
        }

        // ---------------------------------------------------------------- building it from the game's parts

        private GameObject Build(FullMenu menu, Transform dormant)
        {
            SessionWatch.Mark("ledger: the paper's page being built from the game's MyEmployees");   // once per city
            _shell = AppShell.Make(menu, dormant, AppId);
            if (_shell == null || !_shell.SetTabCount(Pages))
            {
                return null;
            }
            for (int i = 0; i < Pages; i++)
            {
                int page = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => UiClone.Safe(() => Turn(page)));
            }
            _layout = _shell.Root.transform.Find("Layout 30-70") as RectTransform;
            _left = _layout != null ? _layout.Find("Left") as RectTransform : null;
            _right = _layout != null ? _layout.Find("Right") as RectTransform : null;
            _tabs = _right != null ? _right.Find("TableTabs") as RectTransform : null;
            _list = _shell.List as RectTransform;
            if (_layout == null || _left == null || _right == null || _tabs == null || _list == null)
            {
                UiClone.Fallback("the paper's page: MyEmployees is not laid out as expected");
                return null;
            }
            // The white box and the table stay where they are (the frame keeps its shape) but are not seen and take no clicks.
            Unseen(_left);
            Unseen(_list);
            if (_shell.TopButtons != null)
            {
                _shell.TopButtons.gameObject.SetActive(false);
            }

            Transform background = _list.Find("ListBackground");
            GameObject sheet = background != null ? UnityEngine.Object.Instantiate(background.gameObject, _layout, false) : new GameObject("Sheet", typeof(RectTransform), typeof(Image));
            sheet.name = "Sheet";
            for (int i = sheet.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(sheet.transform.GetChild(i).gameObject);
            }
            var paper = sheet.GetComponent<Image>() ?? sheet.AddComponent<Image>();
            paper.color = Newsprint;
            paper.raycastTarget = true;   // the page takes the clicks that land on it
            var unlaid = sheet.GetComponent<LayoutElement>() ?? sheet.AddComponent<LayoutElement>();
            unlaid.ignoreLayout = true;
            _sheet = (RectTransform)sheet.transform;
            _sheet.anchorMin = _sheet.anchorMax = _sheet.pivot = Vector2.zero;
            sheet.SetActive(true);

            for (int i = 0; i < Pages; i++)
            {
                var go = new GameObject("Page" + i, typeof(RectTransform));
                go.transform.SetParent(_sheet, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(30f, 22f);
                rect.offsetMax = new Vector2(-30f, -22f);
                _pages[i] = rect;
                go.SetActive(i == Front);
            }
            BuildFront(_pages[Front]);
            BuildMarkets(_pages[Markets]);
            BuildPrices(_pages[Prices]);
            return _shell.Root;
        }

        private static void Unseen(Transform part)
        {
            var group = part.GetComponent<CanvasGroup>() ?? part.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private void BuildFront(RectTransform p)
        {
            _fEarLeft = Words(p, false, 0f, 0.865f, 0.17f, 1f, TextAlignmentOptions.TopLeft, 0.0172f, FontStyles.Normal);
            VRule(p, 0.178f, 0.872f, 0.995f, 1f);
            _fTitle = Words(p, true, 0.19f, 0.872f, 0.81f, 1f, TextAlignmentOptions.Center, 0.082f, FontStyles.Bold);
            _fTitle.characterSpacing = 6f;
            VRule(p, 0.822f, 0.872f, 0.995f, 1f);
            _fEarRight = Words(p, false, 0.83f, 0.865f, 1f, 1f, TextAlignmentOptions.TopRight, 0.0172f, FontStyles.Normal);
            HRule(p, 0f, 1f, 0.862f, 3f);
            _fDateLeft = Words(p, false, 0f, 0.828f, 0.34f, 0.858f, TextAlignmentOptions.MidlineLeft, 0.0185f, FontStyles.Normal);
            _fDateMiddle = Words(p, false, 0.33f, 0.828f, 0.67f, 0.858f, TextAlignmentOptions.Midline, 0.0185f, FontStyles.Normal);
            _fDateRight = Words(p, false, 0.66f, 0.828f, 1f, 0.858f, TextAlignmentOptions.MidlineRight, 0.0185f, FontStyles.Normal);
            HRule(p, 0f, 1f, 0.824f, 1f);

            _fKicker = Words(p, false, 0f, 0.788f, 0.72f, 0.818f, TextAlignmentOptions.BottomLeft, 0.0168f, FontStyles.Bold | FontStyles.UpperCase);
            _fKicker.color = KickerInk;
            _fHead = Words(p, true, 0f, 0.672f, 0.72f, 0.788f, TextAlignmentOptions.TopLeft, 0.041f, FontStyles.Bold | FontStyles.UpperCase);
            _fLead1 = Words(p, false, 0f, 0.468f, 0.352f, 0.668f, TextAlignmentOptions.TopJustified, 0.0198f, FontStyles.Normal);
            VRule(p, 0.36f, 0.474f, 0.664f, 1f);
            _fLead2 = Words(p, false, 0.368f, 0.468f, 0.72f, 0.668f, TextAlignmentOptions.TopJustified, 0.0198f, FontStyles.Normal);
            Link(_fLead1, _fLead2);
            HRule(p, 0f, 0.72f, 0.46f, 1f);

            string[] sections = { LedgerCatalogue.SectionKeys[(int)LedgerSection.City], LedgerCatalogue.SectionKeys[(int)LedgerSection.Economy], LedgerCatalogue.SectionKeys[(int)LedgerSection.Business] };
            for (int i = 0; i < 3; i++)
            {
                float x0 = i * 0.244f, x1 = x0 + 0.232f;
                Label(p, sections[i], x0, 0.414f, x1, 0.452f);
                _fColumns[i] = Words(p, false, x0, 0f, x1, 0.408f, TextAlignmentOptions.TopJustified, 0.0188f, FontStyles.Normal);
                if (i < 2)
                {
                    VRule(p, x1 + 0.006f, 0f, 0.452f, 1f);
                }
            }

            VRule(p, 0.735f, 0f, 0.82f, 1f);
            Label(p, LedgerCatalogue.SectionKeys[(int)LedgerSection.Markets], 0.75f, 0.784f, 1f, 0.82f);
            _fMarkets = Words(p, false, 0.75f, 0.5f, 1f, 0.778f, TextAlignmentOptions.TopLeft, 0.0185f, FontStyles.Normal);
            Label(p, LedgerCatalogue.SectionKeys[(int)LedgerSection.Weather], 0.75f, 0.454f, 1f, 0.49f);
            _fWeather = Words(p, false, 0.75f, 0.33f, 1f, 0.448f, TextAlignmentOptions.TopLeft, 0.0185f, FontStyles.Normal);
            Label(p, LedgerCatalogue.SectionKeys[(int)LedgerSection.AroundTown], 0.75f, 0.284f, 1f, 0.32f);
            _fAround = Words(p, false, 0.75f, 0f, 1f, 0.278f, TextAlignmentOptions.TopLeft, 0.0178f, FontStyles.Normal);
        }

        private void BuildMarkets(RectTransform p)
        {
            _mTitle = Words(p, true, 0f, 0.905f, 0.6f, 1f, TextAlignmentOptions.MidlineLeft, 0.05f, FontStyles.Bold);
            _mTitle.characterSpacing = 4f;
            _mName = Words(p, true, 0.5f, 0.905f, 1f, 1f, TextAlignmentOptions.MidlineRight, 0.042f, FontStyles.Bold | FontStyles.UpperCase);
            HRule(p, 0f, 1f, 0.9f, 3f);
            _mDate = Words(p, false, 0f, 0.865f, 1f, 0.895f, TextAlignmentOptions.Midline, 0.0185f, FontStyles.Normal);
            HRule(p, 0f, 1f, 0.86f, 1f);

            _mHead = Words(p, true, 0f, 0.752f, 0.56f, 0.852f, TextAlignmentOptions.TopLeft, 0.036f, FontStyles.Bold | FontStyles.UpperCase);
            _mLead1 = Words(p, false, 0f, 0.528f, 0.274f, 0.748f, TextAlignmentOptions.TopJustified, 0.0195f, FontStyles.Normal);
            VRule(p, 0.28f, 0.534f, 0.744f, 1f);
            _mLead2 = Words(p, false, 0.286f, 0.528f, 0.56f, 0.748f, TextAlignmentOptions.TopJustified, 0.0195f, FontStyles.Normal);
            Link(_mLead1, _mLead2);
            HRule(p, 0f, 0.56f, 0.52f, 1f);
            _mNews1 = Words(p, false, 0f, 0f, 0.274f, 0.512f, TextAlignmentOptions.TopJustified, 0.0185f, FontStyles.Normal);
            VRule(p, 0.28f, 0f, 0.51f, 1f);
            _mNews2 = Words(p, false, 0.286f, 0f, 0.56f, 0.512f, TextAlignmentOptions.TopJustified, 0.0185f, FontStyles.Normal);
            Link(_mNews1, _mNews2);

            VRule(p, 0.575f, 0f, 0.852f, 1f);
            Label(p, "reallife_ledger_index", 0.59f, 0.815f, 1f, 0.852f);
            _mIndex = Words(p, false, 0.59f, 0.742f, 1f, 0.81f, TextAlignmentOptions.TopLeft, 0.0195f, FontStyles.Normal);
            _mChart = LedgerSpark.Make(p, "IndexChart");
            Place(_mChart.rectTransform, 0.59f, 0.618f, 1f, 0.738f);
            _mCaption = Words(p, false, 0.59f, 0.586f, 1f, 0.614f, TextAlignmentOptions.TopLeft, 0.0155f, FontStyles.Italic);
            _mCaption.color = Grey;
            HRule(p, 0.59f, 1f, 0.58f, 1f);
            Label(p, "reallife_ledger_sectors", 0.59f, 0.54f, 0.788f, 0.575f);
            _mSectors = Words(p, false, 0.59f, 0f, 0.788f, 0.534f, TextAlignmentOptions.TopLeft, 0.0168f, FontStyles.Normal);
            VRule(p, 0.797f, 0f, 0.575f, 1f);
            Label(p, "reallife_ledger_movers", 0.806f, 0.54f, 1f, 0.575f);
            _mMovers = Words(p, false, 0.806f, 0f, 1f, 0.534f, TextAlignmentOptions.TopLeft, 0.0168f, FontStyles.Normal);
        }

        private void BuildPrices(RectTransform p)
        {
            _pTitle = Words(p, true, 0f, 0.905f, 0.6f, 1f, TextAlignmentOptions.MidlineLeft, 0.05f, FontStyles.Bold);
            _pTitle.characterSpacing = 4f;
            _pName = Words(p, true, 0.5f, 0.905f, 1f, 1f, TextAlignmentOptions.MidlineRight, 0.042f, FontStyles.Bold | FontStyles.UpperCase);
            HRule(p, 0f, 1f, 0.9f, 3f);
            _pDate = Words(p, false, 0f, 0.865f, 1f, 0.895f, TextAlignmentOptions.Midline, 0.0185f, FontStyles.Normal);
            HRule(p, 0f, 1f, 0.86f, 1f);
            _pLegend = Words(p, false, 0f, 0.826f, 1f, 0.856f, TextAlignmentOptions.Midline, 0.0155f, FontStyles.Italic);
            _pLegend.color = Grey;
            HRule(p, 0f, 1f, 0.822f, 1f);
            for (int i = 0; i < _pColumns.Length; i++)
            {
                float x0 = i * 0.2525f, x1 = x0 + 0.2425f;
                _pColumns[i] = Words(p, false, x0, 0f, x1, 0.814f, TextAlignmentOptions.TopLeft, 0.016f, FontStyles.Normal);
                _pColumns[i].overflowMode = TextOverflowModes.Truncate;
                if (i < _pColumns.Length - 1)
                {
                    VRule(p, x1 + 0.005f, 0f, 0.814f, 1f);
                }
            }
        }

        // ---------------------------------------------------------------- the parts

        // A text of the game's (the white box's title or line) lifted out of the box's layout and placed on the page.
        private TMP_Text Words(RectTransform parent, bool title, float x0, float y0, float x1, float y1, TextAlignmentOptions align, float size, FontStyles style)
        {
            TMP_Text t = title ? _shell.Box.Title(string.Empty, parent) : _shell.Box.Line(string.Empty, parent);
            Loose(t.gameObject);
            Place(t.rectTransform, x0, y0, x1, y1);
            t.alignment = align;
            t.fontStyle = style;
            t.color = Ink;
            t.richText = true;
            t.raycastTarget = false;
            t.enableAutoSizing = false;
            t.margin = Vector4.zero;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.paragraphSpacing = 0f;
            _sized.Add(t);
            _factors.Add(size);
            return t;
        }

        // A section's name over a rule: the game's own headline, in the paper's ink.
        private void Label(RectTransform parent, string key, float x0, float y0, float x1, float y1)
        {
            Transform made = _shell.Box.Headline(key, parent);
            if (made == null)
            {
                return;
            }
            Loose(made.gameObject);
            Place((RectTransform)made, x0, y0, x1, y1);
            foreach (Image image in made.GetComponentsInChildren<Image>(true))
            {
                image.color = RuleInk;
                image.raycastTarget = false;
            }
            var text = made.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.color = Ink;
                text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
                text.raycastTarget = false;
                _sized.Add(text);
                _factors.Add(0.0175f);
            }
        }

        private static void Link(TMP_Text first, TMP_Text second)
        {
            first.overflowMode = TextOverflowModes.Linked;
            first.linkedTextComponent = second;
            second.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void HRule(RectTransform parent, float x0, float x1, float y, float px)
        {
            RectTransform r = Rule(parent);
            r.anchorMin = new Vector2(x0, y);
            r.anchorMax = new Vector2(x1, y);
            r.sizeDelta = new Vector2(0f, px);
            r.anchoredPosition = Vector2.zero;
        }

        private static void VRule(RectTransform parent, float x, float y0, float y1, float px)
        {
            RectTransform r = Rule(parent);
            r.anchorMin = new Vector2(x, y0);
            r.anchorMax = new Vector2(x, y1);
            r.sizeDelta = new Vector2(px, 0f);
            r.anchoredPosition = Vector2.zero;
        }

        private static RectTransform Rule(RectTransform parent)
        {
            var go = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = RuleInk;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform r, float x0, float y0, float x1, float y1)
        {
            r.anchorMin = new Vector2(x0, y0);
            r.anchorMax = new Vector2(x1, y1);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        // Out of whatever layout the template was made for: placed by its anchors alone.
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

        // ---------------------------------------------------------------- fitting the sheet to the frame

        // The sheet laid over its room in the frame, and the type sized to the sheet.
        private void Measure()
        {
            if (!Bounds(out float x0, out float y0, out float x1, out float y1))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_layout);   // the frame's own layout, worked out now
                if (!Bounds(out x0, out y0, out x1, out y1))
                {
                    LedgerDirector.Logger?.Warn("City Ledger: the menu's frame has no room for the paper yet; it is measured again when the paper is next opened.");
                    return;
                }
            }
            Rect space = _layout.rect;
            _sheet.anchoredPosition = new Vector2(x0 - space.xMin, y0 - space.yMin);
            _sheet.sizeDelta = new Vector2(x1 - x0, y1 - y0);
            float height = y1 - y0;
            if (Mathf.Abs(height - _height) < 1f)
            {
                return;
            }
            _height = height;
            for (int i = 0; i < _sized.Count; i++)
            {
                if (_sized[i] != null)
                {
                    _sized[i].fontSize = Mathf.Max(10f, _factors[i] * height);
                }
            }
        }

        // The sheet's room: over the white box and the table together, from under the tabs to the foot of the table. False while there is no room at all.
        private bool Bounds(out float x0, out float y0, out float x1, out float y1)
        {
            Rect left = Local(_left), right = Local(_right), tabs = Local(_tabs), list = Local(_list);
            x0 = Mathf.Min(left.xMin, right.xMin);
            x1 = Mathf.Max(left.xMax, right.xMax);
            y0 = Mathf.Min(list.yMin, left.yMin);
            y1 = tabs.yMin - 8f;
            return x1 - x0 >= 200f && y1 - y0 >= 200f;
        }

        private Rect Local(RectTransform part)
        {
            part.GetWorldCorners(Corners);
            Vector3 a = _layout.InverseTransformPoint(Corners[0]), c = _layout.InverseTransformPoint(Corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
        }
    }
}
