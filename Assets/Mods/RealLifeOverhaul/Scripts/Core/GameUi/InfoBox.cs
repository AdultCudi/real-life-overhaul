using System;
using BAModAPI;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The white box the game shows one employee in (MyEmployees, "Left/WhiteBox"), as a page written again and again.
    /// Its own parts are kept asleep as templates and copied when asked for:
    ///
    ///   title      EmployeePersonalData/EmployeeName          the employee's name
    ///   line       EmployeePersonalData/EmployeeInfoRow1/Age  "Age: 31"
    ///   headline   CurrentBusinessHeadline                    a word on a grey rule
    ///   switch     Demands/DemandTemplate                     a demand's name and its tick box
    ///   selector   AssignedAddress                            the arrows either side of an assigned business
    ///   buttons    Buttons                                    the action buttons, laid out by ButtonFlow
    ///
    /// The box's own layout group stacks what is added. A slider comes from the settings menu (InfoBox.Slider). On top
    /// sits the mod's look (InfoBox.Look, InfoBox.Parts, LookRules): a header in the app's colour, sections, rows,
    /// meters, ticks, notes and the Guide's foot, all copies of templates made once in From.
    ///
    /// Parts are reused: Clear lays the parts of the box's own kinds aside among the templates and the next page takes them
    /// back in order, each first set back as a new copy would be (<see cref="Restore"/>). Pictures, chips and anything
    /// written into another parent are made and destroyed each page. A page is written only on a click; nothing runs a frame.
    /// </summary>
    public sealed partial class InfoBox
    {
        public static IModLogger Logger;

        private Transform _box;
        private GameObject _title, _line, _headline, _switch, _selector, _buttons;

        public bool HasSelector => _selector != null;
        public bool HasSwitch => _switch != null;

        /// <summary>Takes the box's parts as templates and empties it. Null if the parts no page can do without are missing.</summary>
        public static InfoBox From(Transform whiteBox, Transform templates)
        {
            var box = new InfoBox { _box = whiteBox, _templatesAt = templates };
            box._title =Take(whiteBox, "EmployeePersonalData/EmployeeName", templates, "Title");
            box._line = Take(whiteBox, "EmployeePersonalData/EmployeeInfoRow1/Age", templates, "Line");
            box._headline = Take(whiteBox, "CurrentBusinessHeadline", templates, "Headline");
            box._switch = Take(whiteBox, "Demands/DemandTemplate", templates, "Switch");
            box._selector = Take(whiteBox, "AssignedAddress", templates, "Selector");
            box._buttons = Take(whiteBox, "Buttons", templates, "Buttons");
            if (box._title == null || box._line == null || box._buttons == null)
            {
                UiClone.Fallback("the white box of MyEmployees has no name, no line of text or no buttons");
                return null;
            }
            box.ReadLook(whiteBox);
            UiClone.KeepOnly(whiteBox, "Background");
            Wrap(box._title.GetComponent<TMP_Text>());
            Wrap(box._line.GetComponent<TMP_Text>());
            box.PrepareSelector();
            box.PrepareSwitch();
            PrepareButtons(box._buttons);
            box.MakeLook(templates);
            box.Space(whiteBox);
            box.Keep(templates);
            return box;
        }

        /// <summary>
        /// The same parts, written into another object that lays out its children (a scroll view's content, so a long page
        /// scrolls). The white box copy is left with its background only. Null if the parts no page can do without are missing.
        /// </summary>
        public static InfoBox Into(Transform whiteBox, Transform templates, Transform box)
        {
            InfoBox made = box != null ? From(whiteBox, templates) : null;
            if (made != null)
            {
                made._box = box;
                made.Space(box);
            }
            return made;
        }

        private static GameObject Take(Transform whiteBox, string path, Transform templates, string name)
        {
            Transform part = whiteBox.Find(path);
            if (part == null)
            {
                UiClone.Fallback("the white box of MyEmployees has no '" + path + "'");
                return null;
            }
            part.SetParent(templates, false);
            part.name = name;
            part.gameObject.SetActive(true);
            if (!part.CompareTag("Untagged"))
            {
                part.gameObject.tag = "Untagged";
            }
            return part.gameObject;
        }

        private static void Wrap(TMP_Text text)
        {
            if (text != null)
            {
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Overflow;
            }
        }

        // The selector is the game's drop-down with its script off: two arrow buttons and the box between. Here the box only
        // shows the choice, so its text field (the game's search) goes, its text kept, and the list that would drop is destroyed.
        private void PrepareSelector()
        {
            if (_selector == null)
            {
                return;
            }
            Transform header = _selector.transform.Find("Header");
            Transform middle = header != null ? header.Find("MiddleBox") : null;
            var field = middle != null ? middle.GetComponentInChildren<TMP_InputField>(true) : null;
            if (header == null || header.Find("LeftButton") == null || header.Find("RightButton") == null || field == null || field.textComponent == null)
            {
                UiClone.Fallback("the game's selector is not built as expected, choices are shown as buttons instead");
                UnityEngine.Object.DestroyImmediate(_selector);
                _selector = null;
                return;
            }
            TMP_Text text = field.textComponent;
            text.gameObject.name = "Choice";
            if (field.placeholder != null)
            {
                field.placeholder.gameObject.SetActive(false);
            }
            UnityEngine.Object.DestroyImmediate(field);
            text.raycastTarget = false;
            Transform options = _selector.transform.Find("OptionsPanel");
            if (options != null)
            {
                UnityEngine.Object.DestroyImmediate(options.gameObject);
            }
            Transform arrow = middle.Find("Arrow");
            if (arrow != null)
            {
                UnityEngine.Object.DestroyImmediate(arrow.gameObject);   // "this drops down"
            }
            foreach (MonoBehaviour script in middle.GetComponents<MonoBehaviour>())
            {
                if (script != null && script.GetType().Name == "HoverEffect")
                {
                    UnityEngine.Object.DestroyImmediate(script);
                }
            }
            var open = middle.GetComponent<Button>();
            if (open != null)
            {
                UnityEngine.Object.DestroyImmediate(open);
            }
            header.Find("LeftButton").gameObject.SetActive(true);
            header.Find("RightButton").gameObject.SetActive(true);
            UiClone.Visible(_selector.GetComponent<CanvasGroup>());
        }

        private void PrepareSwitch()
        {
            if (_switch == null)
            {
                return;
            }
            Transform main = _switch.transform.Find("MainInfo");
            if (main == null || main.Find("Name") == null || main.GetComponentInChildren<Toggle>(true) == null)
            {
                UiClone.Fallback("the game's switch row is not built as expected, switches are left out");
                UnityEngine.Object.DestroyImmediate(_switch);
                _switch = null;
                return;
            }
            UiClone.KeepOnly(_switch.transform, "MainInfo");
            for (int i = main.childCount - 1; i >= 0; i--)
            {
                Transform child = main.GetChild(i);
                if (child.name != "Name" && child.GetComponent<Toggle>() == null)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
            AppShell.Width(main.Find("Name"), 520f, true);
        }

        // The row lays its buttons out with ButtonFlow in place of the game's one-line layout, and asks for the height its
        // lines take (the game's row keeps 120). Every row is a copy of this one.
        private static void PrepareButtons(GameObject buttons)
        {
            var line = buttons.GetComponent<HorizontalLayoutGroup>();
            if (line != null)
            {
                UnityEngine.Object.DestroyImmediate(line);
            }
            if (buttons.GetComponent<LayoutGroup>() != null || buttons.AddComponent<ButtonFlow>() == null)
            {
                UiClone.Fallback("the game's row of buttons kept a layout of its own, so a row shows one button of each kind");
                return;
            }
            var size = buttons.GetComponent<LayoutElement>();
            if (size != null)
            {
                size.minHeight = size.preferredHeight = -1f;   // as tall as its lines (ButtonRules.RowHeight)
            }
        }

        /// <summary>Empties the box for a new page: parts of its own kinds are laid aside for the next page, the rest destroyed.</summary>
        public void Clear()
        {
            Begin();
            using var own = Timed();
            for (int i = 0; i < _live.Count; i++)
            {
                GameObject part = _live[i];
                if (part != null && part.transform.parent == _box)
                {
                    Park(part, _liveFrom[i]);
                }
            }
            _live.Clear();
            _liveFrom.Clear();
            for (int i = _box.childCount - 1; i >= 0; i--)
            {
                Transform child = _box.GetChild(i);
                if (child.name != "Background")
                {
                    child.gameObject.SetActive(false);   // out of the layout now; gone at the end of the frame
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }

        // A copy of a template, in the box unless a parent is given. In the box, a kind the box keeps comes from what Clear
        // laid aside if it can (Restore sets it as a new copy), else it is a new copy.
        private GameObject Add(GameObject template, Transform parent = null)
        {
            bool kept = parent == null && Kept(template);
            GameObject made = kept ? Reuse(template) : null;
            if (made == null)
            {
                made = UnityEngine.Object.Instantiate(template, parent ?? _box, false);
                made.name = template.name;
                made.SetActive(true);
            }
            if (kept)
            {
                _live.Add(made);
                _liveFrom.Add(template);
            }
            _parts++;
            return made;
        }

        /// <summary>The page's title: its header, under the app's colour.</summary>
        public void Title(string words)
        {
            Header(words);
        }

        /// <summary>A title: in the box the page's header, anywhere else a plain copy of the box's title.</summary>
        public TMP_Text Title(string words, Transform parent)
        {
            using var own = Timed();
            if (parent == null)
            {
                return Header(words);
            }
            GameObject title = Add(_title, parent);
            UiClone.Words(title.transform, words);
            return title.GetComponent<TMP_Text>();
        }

        /// <summary>
        /// A line of text. An old "Label <b>value</b>" line (FinanceText.Pair, the game's form) is written as a row, so a page
        /// not yet written with rows reads as one that is.
        /// </summary>
        public TMP_Text Line(string words)
        {
            using var own = Timed();
            string label, value;
            if (LookRules.SplitPair(words, out label, out value))
            {
                return RowWords(label, value, CellTone.Plain);
            }
            return Line(words, CellTone.Plain);
        }

        /// <summary>A line of text in the box, or anywhere else as a copy of the box's own.</summary>
        public TMP_Text Line(string words, Transform parent)
        {
            using var own = Timed();
            if (parent == null)
            {
                return Line(words);
            }
            GameObject line = Add(_line, parent);
            UiClone.Words(line.transform, words);
            return line.GetComponent<TMP_Text>();
        }

        /// <summary>
        /// A picture (a post's thumbnail) at a width and its texture's shape, never taller than <paramref name="most"/>. The
        /// texture stays the caller's; the row goes with the page.
        /// </summary>
        public RawImage Picture(Texture texture, float width, float most)
        {
            using var own = Timed();
            if (texture == null || texture.width <= 0)
            {
                return null;
            }
            var row = new GameObject("Picture", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            row.transform.SetParent(_box, false);
            _parts++;
            var image = row.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            float height = Mathf.Min(most, width * texture.height / texture.width);
            var layout = row.GetComponent<LayoutElement>();
            layout.preferredWidth = height * texture.width / texture.height;
            layout.preferredHeight = height;
            layout.flexibleWidth = 0f;
            return image;
        }

        /// <summary>A headline: the start of a section.</summary>
        public void Headline(string key)
        {
            Section(key);
        }

        /// <summary>A headline of words already made (a name and a figure): the start of a section.</summary>
        public Transform HeadlineWords(string words)
        {
            using var own = Timed();
            return SectionWords(words);
        }

        /// <summary>In the box the start of a section; anywhere else the game's headline over a grey rule.</summary>
        public Transform Headline(string key, Transform parent)
        {
            using var own = Timed();
            if (parent == null)
            {
                return Section(key);
            }
            if (_headline == null)
            {
                return Line("<b>" + key.GetLocalization() + "</b>", parent).transform;
            }
            GameObject headline = Add(_headline, parent);
            var text = headline.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                UiClone.Key(text, key);
                // The game's headline carries an icon and a tooltip mark inside its text: not here.
                for (int i = text.transform.childCount - 1; i >= 0; i--)
                {
                    text.transform.GetChild(i).gameObject.SetActive(false);
                }
            }
            return headline.transform;
        }

        /// <summary>A switch with its label. False without the game's switch row: the caller leaves the choice out.</summary>
        public bool Switch(string labelKey, bool on, Action<bool> changed)
        {
            using var own = Timed();
            if (_switch == null)
            {
                return false;
            }
            GameObject row = Add(_switch);
            UiClone.Key(row.transform.Find("MainInfo/Name"), labelKey);
            var toggle = row.GetComponentInChildren<Toggle>(true);
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.SetIsOnWithoutNotify(on);
            toggle.interactable = true;
            toggle.onValueChanged.AddListener(value => UiClone.Safe(() => changed(value)));
            return true;
        }

        /// <summary>
        /// A previous/next selector over a fixed number of choices; <paramref name="text"/> gives a choice's words. It does
        /// not wrap round: the arrow at either end goes grey.
        /// </summary>
        public bool Selector(int count, int index, Func<int, string> text, Action<int> changed)
        {
            return Selector(count, index, text, changed, null);
        }

        /// <summary>The same in the box, or anywhere else.</summary>
        public bool Selector(int count, int index, Func<int, string> text, Action<int> changed, Transform parent)
        {
            using var own = Timed();
            if (_selector == null || count < 1)
            {
                return false;
            }
            GameObject row = Add(_selector, parent);
            Transform header = row.transform.Find("Header");
            var left = header.Find("LeftButton").GetComponent<Button>();
            var right = header.Find("RightButton").GetComponent<Button>();
            Transform choice = Find(header, "Choice");
            int at = Mathf.Clamp(index, 0, count - 1);
            Action show = () =>
            {
                UiClone.Words(choice, text(at));
                if (left != null)
                {
                    left.interactable = at > 0;
                }
                if (right != null)
                {
                    right.interactable = at < count - 1;
                }
            };
            Action<int> step = by =>
            {
                int to = Mathf.Clamp(at + by, 0, count - 1);
                if (to != at)
                {
                    at = to;
                    show();
                    changed(at);
                }
            };
            Hook(header.Find("LeftButton"), () => step(-1));
            Hook(header.Find("RightButton"), () => step(1));
            show();
            return true;
        }

        // The game's arrow is a button inside a button: each gets the click.
        private static void Hook(Transform arrow, Action click)
        {
            foreach (Button button in arrow.GetComponentsInChildren<Button>(true))
            {
                UiClone.Rewire(button, click);
            }
        }

        private static Transform Find(Transform under, string name)
        {
            foreach (Transform child in under.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        /// <summary>
        /// A row of action buttons, in the box or anywhere else. Kinds: 0 positive, 1 negative, 2 additional; each button a
        /// page writes shows, as many of a kind as written, laid out by ButtonFlow in order on as many lines as they need.
        /// </summary>
        public Transform ButtonRow(Transform parent = null)
        {
            using var own = Timed();
            if (parent == null)
            {
                EndSection();   // it closes the section above: its buttons act on the page
            }
            GameObject row = Add(_buttons, parent);
            var flow = row.GetComponent<ButtonFlow>();
            if (flow != null)
            {
                flow.Begin();
                return row.transform;
            }
            for (int i = 0; i < row.transform.childCount; i++)
            {
                row.transform.GetChild(i).gameObject.SetActive(false);
            }
            // The game's own layout, if the row could not be given ButtonFlow: one line, a button of each kind.
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
            }
            return row.transform;
        }

        public const int Positive = 0, Negative = 1, Additional = 2;

        /// <summary>
        /// Shows a button of a row with its word and click: the kind's own button, or a copy beside it for a further one of the
        /// kind. Written again with the same kind and word, it is the same button. A row with fewer kinds uses its last.
        /// </summary>
        public static Button Button(Transform row, int kind, string key, Action click)
        {
            using var own = TimedWriting();
            if (row == null || row.childCount == 0)
            {
                return null;
            }
            var flow = row.GetComponent<ButtonFlow>();
            Transform child = flow != null ? flow.Take(kind, key) : row.GetChild(Mathf.Clamp(kind, 0, row.childCount - 1));
            if (child == null)
            {
                return null;
            }
            child.gameObject.SetActive(true);
            var text = child.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                UiClone.Key(text, key);
            }
            Button button = UiClone.Rewire(child, click);
            if (button != null)
            {
                button.interactable = true;
            }
            return button;
        }

        /// <summary>
        /// How the page fits, for the log: the height it asks for against the box's (the box does not scroll). Works the
        /// layout out at once: only where a line is being written anyway.
        /// </summary>
        public string Fit()
        {
            var rect = (RectTransform)_box;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            float asks = LayoutUtility.GetPreferredHeight(rect), has = rect.rect.height;
            float room = rect.parent is RectTransform outer ? outer.rect.height : has;
            return "the page asks for " + Mathf.RoundToInt(asks) + " px, the box is " + Mathf.RoundToInt(has) + " px in " + Mathf.RoundToInt(room) + " px"
                   + (asks > room + 1f ? ": ITS FOOT IS CUT OFF" : string.Empty);
        }

        /// <summary>Marks the layout for rebuilding: a page is written in one go and shown at once.</summary>
        public void Done()
        {
            using (Timed())
            {
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)_box);
            }
            Finish();
        }
    }
}
