using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>
    /// The City Ledger as a newspaper, in the game's full menu (the game stands still while it is open; the cancel key puts
    /// it down). The frame is the game's own (GameUi.AppShell, copied from MyEmployees, with no button on the phone: it is
    /// opened by picking up the paper), its tabs are the paper's pages, and over the frame lies one sheet of newsprint, the
    /// game's own white panel tinted. The page on it is set as a paper is: in a serif (LedgerType), in black ink, with thin
    /// black rules, justified columns linked one into the next, and labels in small capitals over a rule:
    ///
    ///   Front page    the masthead between its two ears (the weather, the city index) over a double rule, and the
    ///                 dateline; the lead's headline across the page, its standfirst and byline, then four columns: the
    ///                 lead's text under a drop cap, the rest of its section, the city, the economy, business and around
    ///                 town, each story under its headline; down the side a box with the markets, the weather and what
    ///                 is inside; on a short day the index's last sessions drawn under the columns
    ///   Markets       the market's stories and the movers in three columns, the index with its chart, every sector
    ///   Share prices  every listed company's close and its move, by sector, in five columns, as a paper prints them
    ///
    /// Everything is placed when the page is written (opened or turned, in the language of the moment), never per frame:
    /// the type is sized to the sheet, the columns are laid out as shallow as holds their words (balanced), and what a
    /// short paper leaves goes to a larger type, the index's chart and the headline. The edition itself is put together
    /// once a morning (LedgerDirector). A page is laid out once for each paper, language, sheet size and face
    /// (LedgerPageStamp): turned to again, or the paper opened again, for the same, it is the page as it was laid out, and
    /// costs nothing but the tab. The page itself is built the first time the paper is opened (GameUi.PhoneApp.Ready).
    /// </summary>
    public sealed partial class LedgerPage
    {
        public const string AppId = "RealLifeLedger";
        public const int FromHome = 0, FromKiosk = 1, FromTest = 2, FromPhone = 3;
        private const int Front = 0, Markets = 1, Prices = 2, Pages = 3;

        private static readonly string[] TabKeys = { "reallife_ledger_tab_front", "reallife_ledger_tab_markets", "reallife_ledger_tab_prices" };
        private static readonly string[] FromKeys = { "reallife_ledger_header_home", "reallife_ledger_header_kiosk", "reallife_ledger_header_test", "reallife_ledger_header_phone" };

        // The paper's inks: newsprint, black ink, the rules' black and the chart's faint rules. Moves are printed in ink
        // with a mark (up or down), as a paper prints them.
        private static readonly Color Newsprint = new Color(0.957f, 0.945f, 0.914f, 1f);
        private static readonly Color Ink = new Color(0.10f, 0.10f, 0.11f, 1f);
        private static readonly Color RuleInk = new Color(0.12f, 0.12f, 0.13f, 1f);
        private static readonly Color FaintInk = new Color(0.10f, 0.10f, 0.11f, 0.16f);

        private static readonly Vector3[] Corners = new Vector3[4];

        private readonly LedgerDirector _director;
        private readonly StringBuilder _b = new StringBuilder(4096);
        private readonly List<LedgerPiece> _pieces = new List<LedgerPiece>(24);
        private readonly List<LedgerPiece> _also = new List<LedgerPiece>(24);
        private readonly List<LedgerQuote> _quotes = new List<LedgerQuote>(160);
        private readonly RectTransform[] _pages = new RectTransform[Pages];
        private readonly Rules[] _rules = new Rules[Pages];
        private readonly LedgerPageStamp[] _laid = new LedgerPageStamp[Pages];   // what each page was last laid out for (Turn)

        private PhoneApp _app;
        private bool _gaveUp;
        private AppShell _shell;
        private RectTransform _layout, _left, _right, _tabs, _list, _sheet;
        private LedgerEdition _edition;
        private int _from;
        private int _page;
        private float _height;    // the sheet's height: the type is sized to it

        // The face the paper is set in (null: the game's own) and its measures, as fractions of its size: ascender,
        // descender and line gap (TextMeshPro's line is the three), the capitals' height over the ascender, the width of a
        // figure, of the up mark and of the per cent sign; and one screen pixel in canvas units (a hairline rule).
        private TMP_FontAsset _face;
        private float _asc = 1.07f, _desc = 0.29f, _gap, _cap = 0.72f, _capRatio = 0.67f, _figure = 0.56f, _mark = 0.6f, _percent = 0.9f;
        private bool _marks = true;     // the face (or a fallback) has the up and down marks
        private bool _measured;
        private float _px = 2f;

        // the front page
        private TMP_Text _fTitle, _fEarLeft, _fEarRight, _fDateLeft, _fDateMiddle, _fDateRight, _fKicker, _fHead, _fStand, _fByline, _fBand, _fCaption, _fAd, _fBox;
        private Columns _fColumns;
        private LedgerSpark _fChart;
        // the markets page
        private TMP_Text _mTitle, _mName, _mDate, _mKicker, _mHead, _mByline, _mIndex, _mCaption, _mSectorsLabel;
        private readonly TMP_Text[] _mSectors = new TMP_Text[2];
        private Columns _mColumns;
        private LedgerSpark _mChart;
        // the share prices
        private TMP_Text _pTitle, _pName, _pDate, _pLegend;
        private readonly TMP_Text[] _pColumns = new TMP_Text[5];

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
            if (_app == null || !_app.Installed || edition == null || _special != null)
            {
                return false;   // (a special front page is up: it is not turned into the morning's paper under its reader)
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
            if (_edition == null && _special == null)
            {
                LedgerState state = _director.State;
                _edition = state != null ? state.Edition : null;
            }
            Measure();
            UiClone.Words(_shell.Header, _special != null ? _special.Header : LedgerText.T(FromKeys[_from]));
            Turn(_page);
        }

        private void Closed()
        {
            // nothing runs while the page is down; a special front page tells whoever opened it
            if (_special != null)
            {
                SpecialClosed();
            }
        }

        // The page shown: its tab marked, its words written and placed now.
        private void Turn(int page)
        {
            _page = _special != null ? Front : Mathf.Clamp(page, 0, Pages - 1);
            _shell.ShowTab(_page);
            for (int i = 0; i < Pages; i++)
            {
                if (_pages[i] != null && _pages[i].gameObject.activeSelf != (i == _page))
                {
                    _pages[i].gameObject.SetActive(i == _page);
                }
            }
            if ((_edition == null && _special == null) || _height < 100f)
            {
                return;
            }
            LedgerPageStamp now = Stamp();
            if (now.Same(_laid[_page]))
            {
                return;   // laid out for this paper, language, sheet and face already: its texts, rules and chart are as they were written
            }
            _laid[_page] = default(LedgerPageStamp);   // (until this one is written whole)
            MeasureFace(_page == Front ? _fBox : _page == Markets ? _mIndex : _pLegend);
            Rules rules = _rules[_page];
            rules.Begin();
            switch (_page)
            {
                case Front:
                    if (_special != null)
                    {
                        WriteSpecial(_special, rules);
                    }
                    else
                    {
                        WriteFront(_edition, rules);
                    }
                    break;
                case Markets: WriteMarkets(_edition, rules); break;
                default: WritePrices(_edition, rules); break;
            }
            rules.End();
            _laid[_page] = now;
        }

        // What the page that shows would be laid out for now (LedgerPageStamp).
        private LedgerPageStamp Stamp()
        {
            return new LedgerPageStamp
            {
                Paper = _special != null ? (object)_special : _edition,
                Language = Localizor.LocalizorManager.LoadedLocale,
                Width = _sheet.sizeDelta.x,
                Height = _height,
                Pixel = _px,
                Face = _face,
                Copy = _director.CopyPrice,
                Week = _director.WeekPrice,
                Laid = true
            };
        }

        // ---------------------------------------------------------------- building it from the game's parts

        private GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("ledger: the paper's page being built from the game's MyEmployees");   // once per city
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

            _face = LedgerType.Find();
            _measured = false;
            System.Array.Clear(_laid, 0, _laid.Length);   // new pages: none is laid out yet
            for (int i = 0; i < Pages; i++)
            {
                var go = new GameObject("Page" + i, typeof(RectTransform));
                go.transform.SetParent(_sheet, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                _pages[i] = rect;
                _rules[i] = new Rules(rect);
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
            _fTitle = Text(p, "Masthead", TextAlignmentOptions.Top, FontStyles.Bold);
            _fTitle.textWrappingMode = TextWrappingModes.NoWrap;
            _fEarLeft = Text(p, "EarLeft", TextAlignmentOptions.Top);
            _fEarRight = Text(p, "EarRight", TextAlignmentOptions.Top);
            _fDateLeft = Text(p, "DateLeft", TextAlignmentOptions.TopLeft);
            _fDateMiddle = Text(p, "DateMiddle", TextAlignmentOptions.Top);
            _fDateRight = Text(p, "DateRight", TextAlignmentOptions.TopRight);
            _fKicker = Text(p, "Kicker", TextAlignmentOptions.TopLeft);
            _fHead = Text(p, "Headline", TextAlignmentOptions.TopLeft, FontStyles.Bold | FontStyles.UpperCase);
            _fHead.lineSpacing = HeadSpacing;
            _fStand = Text(p, "Standfirst", TextAlignmentOptions.TopLeft);
            _fStand.lineSpacing = -10f;
            _fByline = Text(p, "Byline", TextAlignmentOptions.TopLeft);
            _fColumns = new Columns(this, p, FrontColumns, "Column", BodySpacing, ItemSpacing);
            _fBand = Text(p, "IndexLine", TextAlignmentOptions.TopLeft);
            _fBand.textWrappingMode = TextWrappingModes.NoWrap;
            _fChart = LedgerSpark.Make(p, "IndexChart");
            _fCaption = Text(p, "IndexCaption", TextAlignmentOptions.TopLeft);
            _fAd = Text(p, "HouseAdvert", TextAlignmentOptions.Center);
            _fAd.lineSpacing = -8f;
            _fBox = Text(p, "AtAGlance", TextAlignmentOptions.TopJustified);
            _fBox.lineSpacing = -12f;
            _fBox.paragraphSpacing = 45f;
        }

        private void BuildMarkets(RectTransform p)
        {
            Folio(p, out _mTitle, out _mName, out _mDate);
            _mKicker = Text(p, "Kicker", TextAlignmentOptions.TopLeft);
            _mHead = Text(p, "Headline", TextAlignmentOptions.TopLeft, FontStyles.Bold | FontStyles.UpperCase);
            _mHead.lineSpacing = HeadSpacing;
            _mByline = Text(p, "Byline", TextAlignmentOptions.TopLeft);
            _mColumns = new Columns(this, p, MarketColumns, "Column", BodySpacing, ItemSpacing);
            _mIndex = Text(p, "Index", TextAlignmentOptions.TopLeft);
            _mIndex.lineSpacing = -12f;
            _mIndex.paragraphSpacing = 30f;
            _mChart = LedgerSpark.Make(p, "IndexChart");
            _mCaption = Text(p, "IndexCaption", TextAlignmentOptions.TopLeft);
            _mSectorsLabel = Text(p, "SectorsLabel", TextAlignmentOptions.TopLeft);
            for (int i = 0; i < _mSectors.Length; i++)
            {
                _mSectors[i] = Text(p, "Sectors" + i, TextAlignmentOptions.TopLeft);
                _mSectors[i].textWrappingMode = TextWrappingModes.NoWrap;
                _mSectors[i].overflowMode = TextOverflowModes.Truncate;
                _mSectors[i].lineSpacing = TableSpacing;
            }
        }

        private void BuildPrices(RectTransform p)
        {
            Folio(p, out _pTitle, out _pName, out _pDate);
            _pLegend = Text(p, "Legend", TextAlignmentOptions.Top);
            for (int i = 0; i < _pColumns.Length; i++)
            {
                _pColumns[i] = Text(p, "Prices" + i, TextAlignmentOptions.TopLeft);
                _pColumns[i].textWrappingMode = TextWrappingModes.NoWrap;
                _pColumns[i].overflowMode = TextOverflowModes.Truncate;
                _pColumns[i].lineSpacing = TableSpacing;
                _pColumns[i].paragraphSpacing = 30f;
            }
        }

        // The head of an inside page: the paper's name, the page's, the dateline.
        private void Folio(RectTransform p, out TMP_Text title, out TMP_Text name, out TMP_Text date)
        {
            title = Text(p, "Title", TextAlignmentOptions.TopLeft, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            name = Text(p, "PageName", TextAlignmentOptions.TopRight);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            date = Text(p, "Dateline", TextAlignmentOptions.Top);
        }

        // ---------------------------------------------------------------- the parts

        // A text of the game's (the white box's line) lifted out of the box's layout, set in the paper's face and ink;
        // placed and sized when the page is written.
        private TMP_Text Text(RectTransform parent, string name, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            TMP_Text t = _shell.Box.Line(string.Empty, parent);
            t.gameObject.name = name;
            Loose(t.gameObject);
            if (_face != null)
            {
                t.font = _face;
            }
            t.alignment = align;
            t.fontStyle = style;
            t.color = Ink;
            t.richText = true;
            t.raycastTarget = false;
            t.enableAutoSizing = false;
            t.margin = Vector4.zero;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.characterSpacing = 0f;
            t.wordSpacing = 0f;
            t.lineSpacing = 0f;
            t.paragraphSpacing = 0f;
            t.wordWrappingRatios = Spread;
            Put(t.rectTransform, 0f, 0f, 10f, 10f);
            return t;
        }

        // A part placed by its top left corner, x across and y down from the page's top left, in canvas units.
        private static void Put(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
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

        // ---------------------------------------------------------------- measuring

        // The height of a line of type: from one baseline to the next.
        private float Advance(float size, float lineSpacing)
        {
            return size * (_asc + _desc + _gap + lineSpacing * 0.01f);
        }

        // How many lines a paragraph takes at a width, in a text's settings at a size.
        private int Lines(TMP_Text t, string words, float width, float size, float lineSpacing)
        {
            t.fontSize = size;
            float h = t.GetPreferredValues(words, Mathf.Max(1f, width), 0f).y;
            return 1 + Mathf.Max(0, Mathf.RoundToInt((h - (_asc + _desc) * size) / Mathf.Max(0.01f, Advance(size, lineSpacing))));
        }

        // How tall a text is at a width (its font size set first).
        private static float Tall(TMP_Text t, string words, float width)
        {
            return t.GetPreferredValues(words, Mathf.Max(1f, width), 0f).y;
        }

        // The face's measures, from its own figures and the widths of a few signs: once per page built, when a page is first
        // shown (with a text of that page, which is awake by then).
        private void MeasureFace(TMP_Text t)
        {
            if (_measured || t == null)
            {
                return;
            }
            _measured = true;
            TMP_FontAsset face = t.font;
            if (face != null)
            {
                FaceInfo f = face.faceInfo;
                float size = Mathf.Max(1f, f.pointSize);
                _asc = f.ascentLine / size;
                _desc = Mathf.Abs(f.descentLine) / size;
                _gap = Mathf.Max(0f, (f.lineHeight - (f.ascentLine - f.descentLine)) / size);
                _cap = f.capLine > 0f ? f.capLine / size : 0.7f * _asc;
                _capRatio = Mathf.Clamp(_cap / Mathf.Max(0.01f, _asc), 0.4f, 1f);
                _marks = face.HasCharacter('\u25B2', true, true) && face.HasCharacter('\u25BC', true, true);
            }
            t.fontSize = 100f;
            _figure = Mathf.Max(0.3f, t.GetPreferredValues("0000000000").x / 1000f);
            _percent = Mathf.Max(0.3f, t.GetPreferredValues("%").x / 100f);
            _mark = _marks ? Mathf.Max(0.2f, t.GetPreferredValues("\u25B2").x / 100f) * MarkSize : 0f;
        }

        // ---------------------------------------------------------------- fitting the sheet to the frame

        // The sheet laid over its room in the frame; the type is sized to the sheet when a page is written.
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
            _height = y1 - y0;
            // a hairline is one pixel of the screen, however the canvas is scaled
            Canvas canvas = _sheet.GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.rootCanvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            _px = Mathf.Max(1f, 1f / Mathf.Max(0.05f, scale));
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
