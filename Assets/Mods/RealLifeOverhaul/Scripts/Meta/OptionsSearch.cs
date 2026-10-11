using System;
using System.Collections.Generic;
using BigAmbitions.Mods;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.Meta
{
    /// <summary>
    /// The search box of the options page: a text field under the page's first header. While two letters or more
    /// stand in it, a row is shown only if every word typed is in its label, or in its section's name, what the
    /// section's feature is (its "about" sentence) or the other words for it in <see cref="SearchWords"/>; a
    /// section's button is shown only if one of its rows is; and the test tools are searched whether or not they
    /// are shown otherwise. Emptying the box puts every section back as it was left: nothing here writes what is
    /// open and closed.
    ///
    /// Nothing runs per frame. The work is done when the field's text changes; the lower-case texts are made on
    /// the first letters typed after the page was drawn; rows are the game's own objects, switched on and off.
    /// If anything fails, the box is hidden, one line goes to the log, and the page is as it was without it.
    /// </summary>
    public static partial class OptionsLayout
    {
        // Other words for a feature. First who: the feature's id, or the header key of its group for one that is
        // not registered (either may be listed, with spaces between). Then the words, in lower case.
        // A word here finds every row of the feature, so only words that mean the whole of it.
        private static readonly string[][] SearchWords =
        {
            new[] { "camera", "view third person chase cam zoom" },
            new[] { "character reallife_looks_header", "hair clothes outfit wardrobe appearance looks" },
            new[] { "citylife reallife_city_header", "pedestrians street workers passers-by" },
            new[] { "crime", "robbery mugging thief theft burglar scam pickpocket criminal" },
            new[] { "driving", "car steering throttle pedal" },
            new[] { "jobs", "work employment shift" },
            new[] { "life", "random events" },
            new[] { "people", "police officers paramedics pedestrians cop cops" },
            new[] { "traffic", "accident crash collision ambulance pursuit chase" },
            new[] { "visuals", "graphics colour color grading" },
            new[] { "keys", "keys key bindings shortcuts hotkeys rebind" },
            new[] { "performance", "fps frame rate lag stutter slow optimisation optimization" },
            new[] { "law reallife_law_header", "police cop cops officer arrest precinct station custody wanted" },
            new[] { "justice reallife_justice_header", "court trial judge lawyer prison jail sentence bail" },
            new[] { "licences reallife_licences_header", "driving licence license test theory examiner points" },
            new[] { "garage reallife_garage_header", "car tuning paint parts bike motorbike motorcycle scooter two-wheeler" },
            new[] { "transport reallife_transport_header", "bus buses subway metro train car ownership" },
            new[] { "events reallife_events_header", "fire weather rain storm street" },
            new[] { "family reallife_family_header", "partner spouse wife husband wedding marriage children kids baby" },
            new[] { "social reallife_social_header", "friends dating date relationship career" },
            new[] { "home reallife_home_header", "health sick illness sleep" },
            new[] { "travel reallife_travel_header", "hotel holiday vacation lodging" },
            new[] { "civic reallife_civic_header", "election vote mayor politics construction roadworks" },
            new[] { "leisure reallife_leisure_header", "fun night life photo clothes" },
            new[] { "business reallife_business_header", "company shop competition" },
            new[] { "navigation reallife_nav_header", "gps minimap map route satnav directions destination" },
            new[] { "savetools", "uninstall remove backup" },
            new[] { "diagnostics", "debug log developer" },
        };

        private const int ShortestQuery = 2;   // one letter is in nearly every row: switching all of them on would be a hitch for nothing

        /// <summary>A row of the page that is in no section (the buttons at the top, the headers between the parts).</summary>
        private sealed class LooseRow
        {
            public string LabelKey;   // null: never found (a header, a splitter)
            public GameObject Object;
            public string Text;
        }

        private static readonly List<LooseRow> Loose = new List<LooseRow>();
        private static readonly List<string> Words = new List<string>();
        private static string _query = string.Empty;
        private static bool _indexed;
        private static bool _searchFailed;
        private static SearchBox _box;
        private static GameObject _pageTop;

        /// <summary>True while the search box decides which rows are shown.</summary>
        private static bool Searching => Words.Count > 0;

        /// <summary>The row that makes the box. The game has no text field on this page, so it is built here.</summary>
        private sealed class SearchRow : ModOption
        {
            public SearchRow() : base(null, string.Empty)
            {
            }

            public override void SpawnUi(Transform parent, string modId)
            {
                if (_searchFailed || parent == null)
                {
                    return;
                }
                try
                {
                    _box = SearchBox.Make(parent, Last(parent));
                }
                catch (Exception e)
                {
                    SearchFailed(e);
                }
            }
        }

        // ---------------------------------------------------------------- the page being drawn

        private static void AddLoose(ModOptions page, ModOption option, bool findable)
        {
            var loose = new LooseRow { LabelKey = findable ? option.Label : null };
            Loose.Add(loose);
            page.AddCustom(option);
            page.AddCustom(new RowTap(parent => loose.Object = Last(parent)));
        }

        // The first thing on every drawing of the page: the box is new and empty, so nothing is being searched for.
        private static void SearchDrawn(Transform parent)
        {
            Words.Clear();
            _query = string.Empty;
            _indexed = false;
            _box = null;
            _pageTop = Last(parent);
            foreach (LooseRow loose in Loose)
            {
                loose.Object = null;
            }
        }

        // One of the page's own buttons has been made below the box: the box takes its height, font and shape from it.
        private static void SearchStyle(GameObject button)
        {
            if (_box == null || button == null)
            {
                return;
            }
            try
            {
                _box.Style(button);
            }
            catch (Exception e)
            {
                SearchFailed(e);
            }
        }

        private static void SearchDisposed()
        {
            Words.Clear();
            _query = string.Empty;
            _box = null;
            Keys.Suspended = MetaUi.AnyOpen;
        }

        // Hides the box for the rest of the session and puts the page back as the sections have it.
        private static void SearchFailed(Exception e)
        {
            if (!_searchFailed)
            {
                _searchFailed = true;
                MetaLog.Error(e);
                MetaLog.Warn("the search box on the options page failed and is hidden; the page works as it did without it.");
            }
            Words.Clear();
            _query = string.Empty;
            try
            {
                Keys.Suspended = MetaUi.AnyOpen;
                if (_box != null)
                {
                    _box.gameObject.SetActive(false);
                }
                _box = null;
                ShowAll();
            }
            catch (Exception again)
            {
                MetaLog.Error(again);
            }
        }

        // ---------------------------------------------------------------- what was typed

        // Takes the box's text. True if what is searched for is now something else.
        private static bool SetQuery(string typed)
        {
            string query = typed.Length < ShortestQuery ? string.Empty : typed.ToLowerInvariant();
            if (query == _query)
            {
                return false;
            }
            _query = query;
            Words.Clear();
            int start = -1;
            for (int i = 0; i <= query.Length; i++)
            {
                bool gap = i == query.Length || char.IsWhiteSpace(query[i]);
                if (gap && start >= 0)
                {
                    Words.Add(start == 0 && i == query.Length ? query : query.Substring(start, i - start));
                    start = -1;
                }
                else if (!gap && start < 0)
                {
                    start = i;
                }
            }
            return true;
        }

        // Every word typed is in the row's own text or in its section's.
        private static bool Matches(string rowText, string sectionText)
        {
            for (int i = 0; i < Words.Count; i++)
            {
                string word = Words[i];
                if ((rowText == null || rowText.IndexOf(word, StringComparison.Ordinal) < 0)
                    && (sectionText == null || sectionText.IndexOf(word, StringComparison.Ordinal) < 0))
                {
                    return false;
                }
            }
            return true;
        }

        private static string Lower(string key)
        {
            return string.IsNullOrEmpty(key) ? string.Empty : key.GetLocalization().ToLowerInvariant();
        }

        // The texts the words are looked for in, once per drawing of the page (the language may have changed since the last).
        private static void EnsureIndex()
        {
            if (_indexed)
            {
                return;
            }
            _indexed = true;
            foreach (LooseRow loose in Loose)
            {
                loose.Text = loose.LabelKey != null ? Lower(loose.LabelKey) : null;
            }
            string tests = Lower("reallife_meta_title_tests") + " " + Lower("reallife_meta_tests_group") + " test";
            foreach (Section section in Sections)
            {
                Feature feature = section.Feature;
                string text = Title(feature).ToLowerInvariant() + " " + Lower(feature.AboutKey) + " " + OtherWords(feature);
                section.Text = section.IsTests || section.Parent != null ? text + " " + tests : text;
                section.Texts.Clear();
                // One object per row is what the game makes. Should a row have made none or several, the section is found as a whole.
                if (section.Objects.Count == section.Rows.Count)
                {
                    foreach (ModOption row in section.Rows)
                    {
                        section.Texts.Add(Lower(row.Label));
                    }
                }
            }
        }

        private static string OtherWords(Feature feature)
        {
            string words = string.Empty;
            foreach (string[] entry in SearchWords)
            {
                foreach (string who in entry[0].Split(' '))
                {
                    if (who == feature.Id || feature.Groups.Contains(who))
                    {
                        words += " " + entry[1];
                        break;
                    }
                }
            }
            return words;
        }

        // Shows what matches and hides the rest. Returns how many rows are shown.
        private static int Filter()
        {
            int found = 0;
            foreach (LooseRow loose in Loose)
            {
                bool match = loose.Text != null && Matches(loose.Text, null);
                Show(loose.Object, match);
                if (match)
                {
                    found++;
                }
            }
            foreach (Section section in Sections)
            {
                if (section.Parent == null)
                {
                    found += Filter(section);
                }
            }
            return found;
        }

        private static int Filter(Section section)
        {
            int found = 0;
            bool each = section.Texts.Count == section.Objects.Count;
            bool whole = !each && Matches(null, section.Text);
            for (int i = 0; i < section.Objects.Count; i++)
            {
                bool match = each ? Matches(section.Texts[i], section.Text) : whole;
                Show(section.Objects[i], match);
                if (match)
                {
                    found++;
                }
            }
            foreach (Section group in section.Children)
            {
                found += Filter(group);
            }
            Show(section.Header, found > 0);
            CaptionState(section, CaptionFound);
            Label(section, section.Found);
            return found;
        }

        // The page as the sections have it: after the box was emptied.
        private static void ShowAll()
        {
            foreach (LooseRow loose in Loose)
            {
                Show(loose.Object, true);
            }
            foreach (Section section in Sections)
            {
                if (section.Parent == null)
                {
                    Apply(section);
                }
            }
        }

        // Something changed what may be shown while a search is on (the "Show test tools" switch among the results).
        private static void SearchAgain()
        {
            try
            {
                int found = Filter();
                if (_box != null)
                {
                    _box.Report(found);
                }
            }
            catch (Exception e)
            {
                SearchFailed(e);
            }
        }

        // ---------------------------------------------------------------- the box

        /// <summary>
        /// The search row: a plate with the text field on it and a button that empties it, and a line for the
        /// count below. It has no Update: it is called by the field's own events.
        /// </summary>
        private sealed class SearchBox : MonoBehaviour
        {
            private static readonly Color PlateColor = new Color(0.07f, 0.085f, 0.115f, 0.96f);
            private static readonly Color ClearColor = new Color(0.30f, 0.36f, 0.46f, 1f);

            private RectTransform _row;
            private RectTransform _plateRect;
            private RectTransform _area;
            private RectTransform _clearRect;
            private LayoutElement _layout;
            private Image _plate;
            private Image _clearPlate;
            private GameObject _clear;
            private TMP_InputField _field;
            private TextMeshProUGUI _text;
            private TextMeshProUGUI _hint;
            private TextMeshProUGUI _cross;
            private TextMeshProUGUI _status;
            private ScrollToTop _top;
            private float _height;
            private float _size;
            private string _typed = string.Empty;
            private string _many;
            private string _one;
            private string _none;
            private string _short;

            public static SearchBox Make(Transform parent, GameObject above)
            {
                int layer = LayerMask.NameToLayer("UI");
                var root = new GameObject("RealLifeOverhaul Search", typeof(RectTransform));
                root.layer = layer < 0 ? 5 : layer;
                root.transform.SetParent(parent, false);
                try
                {
                    var box = root.AddComponent<SearchBox>();
                    box.Build(above);
                    return box;
                }
                catch (Exception)
                {
                    root.SetActive(false);
                    Destroy(root);
                    throw;
                }
            }

            private GameObject Child(string objectName, Transform parent)
            {
                var go = new GameObject(objectName, typeof(RectTransform));
                go.layer = gameObject.layer;
                go.transform.SetParent(parent, false);
                return go;
            }

            private TextMeshProUGUI Line(string objectName, Transform parent, Color color, TMP_FontAsset font)
            {
                var text = Child(objectName, parent).AddComponent<TextMeshProUGUI>();
                text.raycastTarget = false;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.color = color;
                text.richText = false;
                // The SDK's TextMeshPro is older than the game's: this property is the one both versions have.
#pragma warning disable CS0618
                text.enableWordWrapping = false;
#pragma warning restore CS0618
                text.overflowMode = TextOverflowModes.Overflow;
                if (font != null)
                {
                    text.font = font;
                }
                return text;
            }

            private void Build(GameObject above)
            {
                _row = (RectTransform)transform;
                _layout = gameObject.AddComponent<LayoutElement>();

                // As wide as the row above (the mod's header), whichever way the page lays its rows out.
                TMP_Text sample = above != null ? above.GetComponentInChildren<TMP_Text>(true) : null;
                TMP_FontAsset font = sample != null ? sample.font : null;
                _size = sample != null ? Mathf.Clamp(sample.fontSize * 0.8f, 14f, 96f) : 30f;
                if (above != null && above.transform is RectTransform like)
                {
                    _row.anchorMin = like.anchorMin;
                    _row.anchorMax = like.anchorMax;
                    _row.pivot = like.pivot;
                    _row.sizeDelta = like.sizeDelta;
                    var likeLayout = above.GetComponent<LayoutElement>();
                    if (likeLayout != null)
                    {
                        _layout.minWidth = likeLayout.minWidth;
                        _layout.preferredWidth = likeLayout.preferredWidth;
                        _layout.flexibleWidth = likeLayout.flexibleWidth;
                    }
                }

                GameObject plate = Child("Box", _row);
                _plateRect = (RectTransform)plate.transform;
                _plateRect.anchorMin = new Vector2(0f, 1f);
                _plateRect.anchorMax = new Vector2(1f, 1f);
                _plateRect.pivot = new Vector2(0.5f, 1f);
                _plate = plate.AddComponent<Image>();
                _plate.color = PlateColor;

                GameObject area = Child("Text Area", _plateRect);
                _area = (RectTransform)area.transform;
                area.AddComponent<RectMask2D>();
                _hint = Line("Placeholder", _area, MetaUi.Soft, font);
                _hint.fontStyle = FontStyles.Italic;
                _hint.overflowMode = TextOverflowModes.Ellipsis;
                _hint.text = "reallife_meta_search_hint".GetLocalization();
                MetaUi.Stretch(_hint.rectTransform, 0f, 0f, 0f, 0f);
                _text = Line("Text", _area, MetaUi.Ink, font);
                MetaUi.Stretch(_text.rectTransform, 0f, 0f, 0f, 0f);

                _clear = Child("Clear", _plateRect);
                _clearRect = (RectTransform)_clear.transform;
                _clearRect.anchorMin = _clearRect.anchorMax = _clearRect.pivot = new Vector2(1f, 0.5f);
                _clearPlate = _clear.AddComponent<Image>();
                _clearPlate.color = ClearColor;
                var clearButton = _clear.AddComponent<Button>();
                clearButton.targetGraphic = _clearPlate;
                Navigation none = clearButton.navigation;
                none.mode = Navigation.Mode.None;
                clearButton.navigation = none;
                clearButton.onClick.AddListener(Empty);
                _cross = Line("Label", _clearRect, MetaUi.Ink, font);
                _cross.alignment = TextAlignmentOptions.Center;
                _cross.text = "x";
                MetaUi.Stretch(_cross.rectTransform, 0f, 0f, 0f, 0f);
                _clear.SetActive(false);

                GameObject status = Child("Status", _row);
                _status = Line("Text", status.transform, MetaUi.Soft, font);
                _status.overflowMode = TextOverflowModes.Ellipsis;
                MetaUi.Stretch(_status.rectTransform, 0f, 0f, 0f, 0f);
                var statusRect = (RectTransform)status.transform;
                statusRect.anchorMin = new Vector2(0f, 0f);
                statusRect.anchorMax = new Vector2(1f, 0f);
                statusRect.pivot = new Vector2(0.5f, 0f);
                status.SetActive(false);

                _many = "reallife_meta_search_results".GetLocalization();
                _one = "reallife_meta_search_result".GetLocalization();
                _none = "reallife_meta_search_none".GetLocalization();
                _short = "reallife_meta_search_short".GetLocalization();

                _field = plate.AddComponent<TMP_InputField>();
                _field.targetGraphic = _plate;
                _field.textViewport = _area;
                _field.textComponent = _text;
                _field.placeholder = _hint;
                _field.lineType = TMP_InputField.LineType.SingleLine;
                _field.characterLimit = 48;
                _field.richText = false;
                _field.onFocusSelectAll = false;
                _field.restoreOriginalTextOnEscape = false;   // Esc empties it instead (Ended)
                _field.customCaretColor = true;
                _field.caretColor = MetaUi.Accent;
                _field.selectionColor = new Color(MetaUi.Accent.r, MetaUi.Accent.g, MetaUi.Accent.b, 0.35f);
                if (font != null)
                {
                    _field.fontAsset = font;
                }
                Navigation fieldNavigation = _field.navigation;
                fieldNavigation.mode = Navigation.Mode.None;
                _field.navigation = fieldNavigation;
                ColorBlock colors = _field.colors;
                colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 1f);
                colors.highlightedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.pressedColor = Color.white;
                colors.fadeDuration = 0.05f;
                _field.colors = colors;
                // The field was switched on before it had its text: off and on again makes its caret.
                _field.enabled = false;
                _field.enabled = true;
                _field.onValueChanged.AddListener(Changed);
                _field.onSelect.AddListener(Selected);
                _field.onDeselect.AddListener(Deselected);
                _field.onEndEdit.AddListener(Ended);

                _top = gameObject.AddComponent<ScrollToTop>();
                _top.enabled = false;
                _top.Scroll = GetComponentInParent<ScrollRect>();

                Size(_size * 2.4f, _size);
            }

            /// <summary>Takes the height, the font and the shape of one of the page's own buttons.</summary>
            public void Style(GameObject button)
            {
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                float size = _size;
                if (label != null)
                {
                    size = Mathf.Clamp(label.fontSize, 14f, 96f);
                    if (label.font != null)
                    {
                        _text.font = label.font;
                        _hint.font = label.font;
                        _cross.font = label.font;
                        _status.font = label.font;
                        _field.fontAsset = label.font;
                    }
                }
                Button pressed = button.GetComponentInChildren<Button>(true);
                Image shape = pressed != null ? pressed.targetGraphic as Image : null;
                if (shape != null && shape.sprite != null)
                {
                    _plate.sprite = shape.sprite;
                    _plate.type = shape.type;
                    _plate.pixelsPerUnitMultiplier = shape.pixelsPerUnitMultiplier;
                    _clearPlate.sprite = shape.sprite;
                    _clearPlate.type = shape.type;
                    _clearPlate.pixelsPerUnitMultiplier = shape.pixelsPerUnitMultiplier;
                }
                float height = 0f;
                var buttonLayout = button.GetComponent<LayoutElement>();
                var buttonRect = button.transform as RectTransform;
                if (buttonLayout != null && buttonLayout.preferredHeight > 1f)
                {
                    height = buttonLayout.preferredHeight;
                }
                else if (buttonRect != null && buttonRect.rect.height > 1f)
                {
                    height = buttonRect.rect.height;
                }
                Size(Mathf.Clamp(height, size * 1.6f, size * 4f), size);
            }

            // Everything in the row is placed from the height of the plate and the size of the letters.
            private void Size(float height, float size)
            {
                _height = height;
                _size = size;
                float pad = height * 0.3f;
                _plateRect.anchoredPosition = Vector2.zero;
                _plateRect.sizeDelta = new Vector2(0f, height);
                MetaUi.Stretch(_area, pad, 0f, height + pad * 0.5f, 0f);
                _clearRect.sizeDelta = new Vector2(height * 0.68f, height * 0.68f);
                _clearRect.anchoredPosition = new Vector2(-height * 0.16f, 0f);
                var statusRect = (RectTransform)_status.transform.parent;
                statusRect.offsetMin = new Vector2(pad, 0f);
                statusRect.offsetMax = new Vector2(-pad, size * 1.6f);
                _field.pointSize = size;
                _text.fontSize = size;
                _hint.fontSize = size;
                _cross.fontSize = size;
                _status.fontSize = size * 0.85f;
                Fit();
            }

            // The row is as high as the plate, and the line below it while that is there.
            private void Fit()
            {
                bool line = _status.transform.parent.gameObject.activeSelf;
                float height = _height * 1.15f + (line ? _size * 1.6f : 0f);
                if (!Mathf.Approximately(_layout.preferredHeight, height))
                {
                    _layout.minHeight = height;
                    _layout.preferredHeight = height;
                    _row.sizeDelta = new Vector2(_row.sizeDelta.x, height);
                }
            }

            // ------------------------------------------------------------ the field's events

            private void Changed(string typed)
            {
                try
                {
                    _typed = typed == null ? string.Empty : typed.Trim();
                    bool any = !string.IsNullOrEmpty(typed);
                    if (_clear.activeSelf != any)
                    {
                        _clear.SetActive(any);
                    }
                    int found = -1;
                    if (SetQuery(_typed))
                    {
                        if (Searching)
                        {
                            EnsureIndex();
                            found = Filter();
                            _top.Go(_pageTop);
                        }
                        else
                        {
                            ShowAll();
                        }
                    }
                    else if (Searching)
                    {
                        return;   // a space more or less: the same search
                    }
                    Report(found);
                }
                catch (Exception e)
                {
                    SearchFailed(e);
                }
            }

            /// <summary>The line under the box: how many rows were found, or that nothing was.</summary>
            public void Report(int found)
            {
                GameObject line = _status.transform.parent.gameObject;
                bool show = _typed.Length > 0;
                if (show)
                {
                    _status.text = !Searching ? _short
                        : found == 0 ? _none.Replace("{query}", _typed)
                        : found == 1 ? _one
                        : _many.Replace("{count}", found.ToString());
                }
                if (line.activeSelf != show)
                {
                    line.SetActive(show);
                    Fit();
                }
            }

            // The mod's own keys are off while the field has the keyboard. The game leaves its shortcuts alone by
            // itself: the field is the selected object, on the UI layer (GameManager.HasInputSelected).
            private void Selected(string typed)
            {
                Keys.Suspended = true;
            }

            private void Deselected(string typed)
            {
                Keys.Suspended = MetaUi.AnyOpen;
                Ended(typed);
            }

            // Esc empties the box. Either the field saw the key itself, or the game took the selection away for it
            // first (CancelButtonHandler, in the city), in which case the key is still down in this frame.
            private void Ended(string typed)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_field.text) && (_field.wasCanceled || Keys.PanelClose()))
                    {
                        _field.text = string.Empty;
                    }
                }
                catch (Exception e)
                {
                    SearchFailed(e);
                }
            }

            private void Empty()
            {
                try
                {
                    _field.text = string.Empty;
                    EventSystem events = EventSystem.current;
                    if (events != null)
                    {
                        events.SetSelectedGameObject(_field.gameObject);   // and go on typing
                    }
                }
                catch (Exception e)
                {
                    SearchFailed(e);
                }
            }

            // The page was closed, or is being drawn again, with the field still selected.
            private void OnDisable()
            {
                Keys.Suspended = MetaUi.AnyOpen;
            }
        }

        /// <summary>
        /// Puts the page's scroll view where the mod's first header is at the top. Switched on by a new search
        /// for two frames (the page lays its rows out again in between), and off otherwise.
        /// </summary>
        private sealed class ScrollToTop : MonoBehaviour
        {
            public ScrollRect Scroll;

            private GameObject _anchor;
            private int _frames;

            public void Go(GameObject anchor)
            {
                if (Scroll == null)
                {
                    return;
                }
                _anchor = anchor;
                _frames = 2;
                enabled = true;
            }

            private void LateUpdate()
            {
                try
                {
                    Place();
                }
                catch (Exception e)
                {
                    _frames = 0;
                    Scroll = null;
                    MetaLog.Error(e);
                }
                if (--_frames <= 0)
                {
                    enabled = false;
                }
            }

            private void Place()
            {
                if (Scroll == null || Scroll.content == null)
                {
                    return;
                }
                RectTransform content = Scroll.content;
                RectTransform view = Scroll.viewport != null ? Scroll.viewport : (RectTransform)Scroll.transform;
                float hidden = content.rect.height - view.rect.height;
                Scroll.velocity = Vector2.zero;
                if (hidden <= 1f)
                {
                    return;
                }
                float above = 0f;   // how far below the top of everything the mod's part begins (another mod's rows may come first)
                if (_anchor != null && _anchor.transform is RectTransform anchor)
                {
                    Vector3 top = content.InverseTransformPoint(anchor.TransformPoint(new Vector3(0f, anchor.rect.yMax, 0f)));
                    above = content.rect.yMax - top.y;
                }
                Scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(above / hidden);
            }
        }
    }
}
