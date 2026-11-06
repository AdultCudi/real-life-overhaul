using System;
using System.Collections.Generic;
using BAModAPI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The game's F1 help screen copied asleep as a window of the mod's, without the scripts that drove the help and painted
    /// in the dark colours (DarkLook); the game's own help is left alone. Costs nothing a frame of its own.
    /// </summary>
    public sealed class HelpFrame
    {
        public static IModLogger Logger;

        // What only draws or sounds stays on the copy.
        private static readonly string[] Harmless =
        {
            "ButtonEffects", "ToggleClickSound", "HoverEffect", "ScrollBarDraggingComponent", "ScrollBarHandleSizeLimiter", "TextInputTextChangedSound"
        };

        private const float RowGap = 34f;          // between two rows of a page, in canvas units of 3840 x 2160
        private const float PageFoot = 60f;        // below the last row, so it never sits on the window's edge
        private const float NoteSize = 28f;        // a paragraph: the help's own text size
        private const float SmallSize = 24f;       // small capitals over a group of rows

        public GameObject Root { get; private set; }
        public TMP_Text Headline { get; private set; }
        public TMP_Text Status { get; private set; }
        public Button Close { get; private set; }
        public Button Back { get; private set; }
        public Button Forward { get; private set; }
        public TMP_InputField Search { get; private set; }
        public Button Clear { get; private set; }
        public ScrollRect PageScroll { get; private set; }
        /// <summary>The list's content: the categories, in order.</summary>
        public RectTransform List { get; private set; }
        /// <summary>The page's content: the topic's header first, then the page.</summary>
        public RectTransform Page { get; private set; }
        public Image TopicGlyph { get; private set; }
        public TMP_Text TopicCategory { get; private set; }
        public TMP_Text TopicName { get; private set; }
        public TMP_Text TopicNote { get; private set; }

        private GameObject _category;       // a category of the list, asleep, with one page link in it
        private GameObject _paragraph;      // a text of the page, asleep
        private GameObject _small;          // small capitals over a group of rows, asleep

        /// <summary>The copy, asleep under <paramref name="dormant"/> and painted dark. Null, with one line in Player.log, if the game's help is not built as expected.</summary>
        public static HelpFrame Make(Transform dormant, string name)
        {
            try
            {
                var help = InstanceBehavior<HelpSystem>.Instance;
                if (help == null || help.container == null)
                {
                    UiClone.Fallback("the game's help screen is not there to copy, so '" + name + "' is not added");
                    return null;
                }
                GameObject root = UiClone.Asleep(help.container, dormant, name);
                var frame = new HelpFrame { Root = root };
                if (!frame.Take(root.transform, dormant, name))
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    return null;
                }
                root.SetActive(true);
                return frame;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("'" + name + "' could not be built from the game's help screen");
                return null;
            }
        }

        private bool Take(Transform root, Transform dormant, string name)
        {
            Transform background = UiClone.Need(root, "Background", name);
            Transform headline = UiClone.Need(root, "Topbar/Headline", name);
            Transform close = UiClone.Need(root, "Topbar/CloseButton", name);
            Transform back = UiClone.Need(root, "BackButton", name);
            Transform forward = UiClone.Need(root, "ForwardButton", name);
            Transform search = UiClone.Need(root, "InputField (Search)", name);
            Transform left = UiClone.Need(root, "Left (Categories)", name);
            Transform list = UiClone.Need(root, "Left (Categories)/Viewport/Content", name);
            Transform category = list != null ? UiClone.Need(list, "Category", name) : null;
            Transform page = UiClone.Need(root, "Help Page", name);
            Transform content = page != null ? UiClone.Need(page, "Viewport/Content", name) : null;
            Transform topic = content != null ? UiClone.Need(content, "Topic", name) : null;
            Transform paragraph = content != null ? UiClone.Need(content, "TMProTemplate", name) : null;
            if (background == null || headline == null || close == null || back == null || forward == null || search == null || left == null || list == null
                || category == null || page == null || content == null || topic == null || paragraph == null)
            {
                return false;
            }
            // The paragraph's own scripts (the help's mark-down, not the game's assembly) go first: nothing renders mark-down here.
            foreach (MonoBehaviour script in paragraph.GetComponents<MonoBehaviour>())
            {
                if (script != null && !(script is TMP_Text))
                {
                    UnityEngine.Object.DestroyImmediate(script);
                }
            }
            UiClone.StripGameScripts(Root, script => Array.IndexOf(Harmless, script.GetType().Name) >= 0);
            UiClone.KeepOnly(list, "Category");                  // the categories the help made from its pages
            UiClone.KeepOnly(content, "Topic", "TMProTemplate"); // and the page it last showed
            Transform pages = category.Find("Pages");
            if (pages == null || pages.childCount == 0 || category.Find("CategoryButton") == null)
            {
                UiClone.Fallback(name + ": the help's list of topics is not built as expected");
                return false;
            }
            UiClone.KeepOnly(pages, pages.GetChild(0).name);

            Headline = headline.GetComponent<TMP_Text>();
            Close = UiClone.Rewire(close, null);
            Back = UiClone.Rewire(back, null);
            Forward = UiClone.Rewire(forward, null);
            Search = search.GetComponent<TMP_InputField>();
            PageScroll = page.GetComponent<ScrollRect>();
            List = (RectTransform)list;
            Page = (RectTransform)content;
            if (Headline == null || Close == null || Back == null || Forward == null || Search == null || PageScroll == null)
            {
                UiClone.Fallback(name + ": the help's top bar, search box or page is not built as expected");
                return false;
            }
            TakeTopic(topic);
            TakeSearch(search);

            // The templates sleep apart from the window; a page's parts are made from them.
            _category = category.gameObject;
            category.SetParent(dormant, false);
            _paragraph = paragraph.gameObject;
            paragraph.SetParent(dormant, false);
            _small = TopicCategory != null ? UnityEngine.Object.Instantiate(TopicCategory.gameObject, dormant, false) : null;
            TidyText(_paragraph, NoteSize, DarkLook.Soft, FontStyles.Normal);
            if (_small != null)
            {
                TidyText(_small, SmallSize, DarkLook.Muted, FontStyles.UpperCase);
                UiClone.Words(_small.GetComponent<TMP_Text>(), string.Empty);
            }

            // A status at the right of the top bar, left of the close button: a copy of the headline.
            var status = UnityEngine.Object.Instantiate(headline.gameObject, headline.parent, false);
            status.name = "Status";
            var statusRect = (RectTransform)status.transform;
            statusRect.anchorMin = statusRect.anchorMax = statusRect.pivot = new Vector2(1f, 0.5f);
            statusRect.anchoredPosition = new Vector2(-136f, 0f);
            statusRect.sizeDelta = new Vector2(1300f, 60f);
            Status = status.GetComponent<TMP_Text>();
            Status.alignment = TextAlignmentOptions.MidlineRight;
            Status.fontSize = 30f;
            Status.textWrappingMode = TextWrappingModes.NoWrap;
            Status.overflowMode = TextOverflowModes.Ellipsis;
            var headlineRect = (RectTransform)headline;
            headlineRect.sizeDelta = new Vector2(-1500f, headlineRect.sizeDelta.y);   // leaves the right of the bar to the status

            // Rows a little apart, and room under the last one.
            var layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.spacing = RowGap;
                layout.padding = new RectOffset(0, 0, 0, (int)PageFoot);
            }
            Paint(background, headline, Status, back, forward, search, page);
            return true;
        }

        // The header over the page: the coloured square and its picture, the category, the page's name, and a note at the right.
        private void TakeTopic(Transform topic)
        {
            // As wide as the page, so a note can stand at its right.
            var element = topic.GetComponent<LayoutElement>() ?? topic.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            Transform square = topic.Find("Image");
            TopicGlyph = square != null && square.childCount > 0 ? square.GetChild(0).GetComponent<Image>() : null;
            Transform category = topic.Find("CategoryName");
            Transform name = topic.Find("PageName");
            TopicCategory = category != null ? category.GetComponent<TMP_Text>() : null;
            TopicName = name != null ? name.GetComponent<TMP_Text>() : null;
            if (TopicName != null)
            {
                var noteObject = UnityEngine.Object.Instantiate(name.gameObject, topic, false);
                noteObject.name = "Note";
                var rect = (RectTransform)noteObject.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, -12.8f);
                rect.sizeDelta = new Vector2(560f, 42f);
                TopicNote = noteObject.GetComponent<TMP_Text>();
                TopicNote.alignment = TextAlignmentOptions.MidlineRight;
                TopicNote.fontSize = NoteSize;
                TopicNote.fontStyle = FontStyles.Normal;
                TopicNote.color = DarkLook.Soft;
                ((RectTransform)name).sizeDelta = new Vector2(860f, ((RectTransform)name).sizeDelta.y);
                TopicName.textWrappingMode = TextWrappingModes.NoWrap;
                TopicName.overflowMode = TextOverflowModes.Ellipsis;
                TopicName.color = DarkLook.Text;
            }
            if (TopicCategory != null)
            {
                TopicCategory.color = DarkLook.Muted;
            }
        }

        // Its magnifier and clear button shown as the game's InputField script showed them (taken off with it).
        private void TakeSearch(Transform search)
        {
            Transform magnifier = search.Find("DefaultIcon");
            Transform number = search.Find("NumberIcon");
            Transform clear = search.Find("ClearIcon");
            Transform autocomplete = search.Find("Text Area/AutocompleteTextField");
            if (magnifier != null)
            {
                magnifier.gameObject.SetActive(true);
                Tint(magnifier, DarkLook.Soft);
            }
            if (number != null)
            {
                UnityEngine.Object.DestroyImmediate(number.gameObject);
            }
            if (autocomplete != null)
            {
                UnityEngine.Object.DestroyImmediate(autocomplete.gameObject);
            }
            if (clear != null)
            {
                Clear = UiClone.Rewire(clear, null);
                Tint(clear, DarkLook.Soft);
                clear.gameObject.SetActive(false);
            }
            if (!string.IsNullOrEmpty(Search.text))
            {
                Search.SetTextWithoutNotify(string.Empty);   // what was last looked for in the game's help
            }
            if (Search.textComponent != null)
            {
                Search.textComponent.color = DarkLook.Text;
            }
            if (Search.placeholder is TMP_Text hint)
            {
                hint.color = DarkLook.Muted;
            }
            Search.caretColor = DarkLook.Text;
            Search.customCaretColor = true;
            Search.selectionColor = new Color(DarkLook.Chosen.r, DarkLook.Chosen.g, DarkLook.Chosen.b, 0.35f);
        }

        // The help's white and greys in the dark colours: its sprites keep their shape and shading, tinted.
        private void Paint(Transform background, Transform headline, TMP_Text status, Transform back, Transform forward, Transform search, Transform page)
        {
            Tint(background, DarkLook.Back);
            SetColour(headline, DarkLook.Text);
            status.color = DarkLook.Soft;
            foreach (Transform round in new[] { back, forward, search })
            {
                Tint(round, DarkLook.Field);
                Transform icon = round.Find("Icon");
                if (icon != null)
                {
                    Tint(icon, DarkLook.Text);
                }
            }
            Transform bar = page.Find("Scrollbar Vertical");
            if (bar != null)
            {
                Tint(bar, DarkLook.Track);
                Transform handle = bar.Find("Sliding Area/Handle");
                if (handle != null)
                {
                    Tint(handle, DarkLook.Handle);
                }
                var scrollbar = bar.GetComponent<Scrollbar>();
                if (scrollbar != null)
                {
                    ColorBlock colours = scrollbar.colors;
                    colours.normalColor = Color.white;
                    colours.highlightedColor = Color.white;
                    colours.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                    colours.selectedColor = Color.white;
                    scrollbar.colors = colours;
                }
            }
        }

        /// <summary>A category at the end of the list; its pages are added with <see cref="HelpGroup.Add"/>.</summary>
        public HelpGroup AddGroup(string name)
        {
            GameObject copy = UnityEngine.Object.Instantiate(_category, List, false);
            copy.name = name;
            copy.SetActive(true);
            return new HelpGroup(copy.transform);
        }

        /// <summary>A paragraph made under the page, awake; the caller writes and places it.</summary>
        public TMP_Text NewParagraph()
        {
            GameObject copy = UnityEngine.Object.Instantiate(_paragraph, Page, false);
            copy.name = "Paragraph";
            copy.SetActive(true);
            return copy.GetComponent<TMP_Text>();
        }

        /// <summary>Small capitals over a group of rows, made under the page; null if the help had no category label to copy.</summary>
        public TMP_Text NewSmall()
        {
            if (_small == null)
            {
                return null;
            }
            GameObject copy = UnityEngine.Object.Instantiate(_small, Page, false);
            copy.name = "Heading";
            copy.SetActive(true);
            return copy.GetComponent<TMP_Text>();
        }

        // Sized by the page's layout: its width from the page, its height from its lines.
        private static void TidyText(GameObject text, float size, Color colour, FontStyles style)
        {
            var t = text.GetComponent<TMP_Text>();
            if (t == null)
            {
                return;
            }
            var rect = (RectTransform)text.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            t.fontSize = size;
            t.color = colour;
            t.fontStyle = style;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.raycastTarget = false;
            if (text.GetComponent<LayoutElement>() is LayoutElement element)
            {
                element.ignoreLayout = false;
            }
        }

        internal static void Tint(Transform part, Color colour)
        {
            var image = part != null ? part.GetComponent<Image>() : null;
            if (image != null)
            {
                image.color = colour;
            }
        }

        internal static void SetColour(Transform part, Color colour)
        {
            var text = part != null ? part.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                text.color = colour;
            }
        }
    }

    /// <summary>One category of the help's list: a button with its name and arrow, and the pages it opens to. Costs nothing a frame.</summary>
    public sealed class HelpGroup
    {
        public readonly Transform Root;
        public readonly Button Button;
        public readonly TMP_Text Name;
        private readonly Transform _arrow;
        private readonly Transform _pages;
        private readonly GameObject _pagesBack;
        private readonly GameObject _link;     // the help's own page link, the template of this one's
        private readonly List<HelpLink> _links = new List<HelpLink>(16);

        public bool IsOpen { get; private set; }

        internal HelpGroup(Transform root)
        {
            Root = root;
            Transform button = root.Find("CategoryButton");
            Button = UiClone.Rewire(button.Find("Button"), null);
            Transform name = button.Find("Text (TMP)");
            Name = name != null ? name.GetComponent<TMP_Text>() : null;
            _arrow = button.Find("Arrow");
            _pages = root.Find("Pages");
            Transform back = root.Find("PagesBackground");
            _pagesBack = back != null ? back.gameObject : null;
            _link = _pages.GetChild(0).gameObject;
            _link.SetActive(false);
            HelpFrame.Tint(button.Find("Background"), DarkLook.Raised);
            HelpFrame.Tint(_arrow, DarkLook.Soft);
            HelpFrame.Tint(back, DarkLook.Sunk);
            if (Name != null)
            {
                Name.color = DarkLook.Text;
            }
            SetOpen(false);
        }

        public HelpLink Add(string name)
        {
            GameObject copy = UnityEngine.Object.Instantiate(_link, _pages, false);
            copy.name = name;
            copy.SetActive(true);
            var link = new HelpLink(copy);
            _links.Add(link);
            return link;
        }

        /// <summary>Opens or closes it as HelpCategory.SetOpenState does: its pages and their ground shown, the arrow turned.</summary>
        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (_pages.gameObject.activeSelf != open)
            {
                _pages.gameObject.SetActive(open);
            }
            if (_pagesBack != null && _pagesBack.activeSelf != open)
            {
                _pagesBack.SetActive(open);
            }
            if (_arrow != null)
            {
                _arrow.localEulerAngles = new Vector3(0f, 0f, open ? 0f : 180f);
            }
        }

        public void Show(bool shown)
        {
            if (Root.gameObject.activeSelf != shown)
            {
                Root.gameObject.SetActive(shown);
            }
        }
    }

    /// <summary>One page link of a category: lighter under the pointer, in the game's blue while it is the page shown. Costs nothing a frame.</summary>
    public sealed class HelpLink
    {
        // The tint that makes words written in Text read Soft at rest, so the pointer can lighten them to Text.
        private static readonly Color Resting = new Color(DarkLook.Soft.r / DarkLook.Text.r, DarkLook.Soft.g / DarkLook.Text.g, DarkLook.Soft.b / DarkLook.Text.b, 1f);

        public readonly GameObject Root;
        public readonly Button Button;
        public readonly TMP_Text Name;
        private bool _chosen;

        internal HelpLink(GameObject root)
        {
            Root = root;
            Name = root.GetComponent<TMP_Text>();
            Button = UiClone.Rewire(root.transform, null);
            if (Button != null && Name != null)
            {
                Button.targetGraphic = Name;
                Button.transition = Selectable.Transition.ColorTint;
                var nav = Button.navigation;
                nav.mode = Navigation.Mode.None;
                Button.navigation = nav;
            }
            SetChosen(false);
        }

        /// <summary>The page shown: its name in the game's blue; otherwise soft white, full white under the pointer.</summary>
        public void SetChosen(bool chosen)
        {
            _chosen = chosen;
            if (Name != null)
            {
                Name.color = chosen ? DarkLook.Chosen : DarkLook.Text;
            }
            if (Button != null)
            {
                ColorBlock colours = Button.colors;
                colours.normalColor = chosen ? Color.white : Resting;
                colours.highlightedColor = Color.white;
                colours.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                colours.selectedColor = colours.normalColor;
                colours.colorMultiplier = 1f;
                colours.fadeDuration = 0.06f;
                Button.colors = colours;
            }
        }

        public bool Chosen => _chosen;

        public void Show(bool shown)
        {
            if (Root.activeSelf != shown)
            {
                Root.SetActive(shown);
            }
        }
    }
}
