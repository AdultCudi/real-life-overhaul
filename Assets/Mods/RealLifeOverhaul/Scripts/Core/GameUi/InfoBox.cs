using System;
using BAModAPI;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The white box the game shows one employee in (MyEmployees, "Left/WhiteBox"), as a page that can be written
    /// again and again: a title, lines of text, headlines over a grey rule, switches, previous/next selectors and a
    /// row of up to three buttons. Each of those is one of the box's own parts, kept asleep as a template and copied
    /// when asked for:
    ///
    ///   title      EmployeePersonalData/EmployeeName        the employee's name
    ///   line       EmployeePersonalData/EmployeeInfoRow1/Age  "Age: 31" (label, then the value in bold)
    ///   headline   CurrentBusinessHeadline                  a word on a grey rule
    ///   switch     Demands/DemandTemplate                   a demand's name and its tick box
    ///   selector   AssignedAddress                          the arrows either side of the business an employee is assigned to
    ///   buttons    Buttons                                  the positive, negative and additional action buttons
    ///
    /// The box lays its children out itself (the game's vertical layout group), so a page is whatever was added, in order.
    /// A page is rebuilt only when the player clicks something.
    /// </summary>
    public sealed class InfoBox
    {
        public static IModLogger Logger;

        private Transform _box;
        private GameObject _title, _line, _headline, _switch, _selector, _buttons;

        public bool HasSelector => _selector != null;
        public bool HasSwitch => _switch != null;

        /// <summary>Takes the box's parts as templates and empties it. Null if the parts no page can do without are missing.</summary>
        public static InfoBox From(Transform whiteBox, Transform templates)
        {
            var box = new InfoBox { _box = whiteBox };
            box._title = Take(whiteBox, "EmployeePersonalData/EmployeeName", templates, "Title");
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
            UiClone.KeepOnly(whiteBox, "Background");
            Wrap(box._title.GetComponent<TMP_Text>());
            Wrap(box._line.GetComponent<TMP_Text>());
            box.PrepareSelector();
            box.PrepareSwitch();
            return box;
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

        // The selector is the game's drop-down with its script off: the two arrow buttons and the box between them.
        // The box is a text field in the game (it searches the list); here it only shows the choice, so the field is
        // taken off and its text kept, and the list that would drop down is destroyed.
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
                UnityEngine.Object.DestroyImmediate(arrow.gameObject);   // the arrow that says "this drops down"
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
            var group = _selector.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
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

        // ---------------------------------------------------------------- writing a page

        /// <summary>Empties the box for a new page.</summary>
        public void Clear()
        {
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

        private GameObject Add(GameObject template, Transform parent = null)
        {
            GameObject made = UnityEngine.Object.Instantiate(template, parent ?? _box, false);
            made.name = template.name;
            made.SetActive(true);
            return made;
        }

        public void Title(string words)
        {
            Title(words, null);
        }

        /// <summary>A title in the box, or (with a parent) anywhere else: the copy of the box's own.</summary>
        public TMP_Text Title(string words, Transform parent)
        {
            GameObject title = Add(_title, parent);
            UiClone.Words(title.transform, words);
            return title.GetComponent<TMP_Text>();
        }

        /// <summary>A line of text. The game's lines read "Label: value" with the value in bold; mark-up in 'words' does the same.</summary>
        public TMP_Text Line(string words)
        {
            return Line(words, null);
        }

        /// <summary>A line of text in the box, or (with a parent) anywhere else: the copy of the box's own, in its font and colour.</summary>
        public TMP_Text Line(string words, Transform parent)
        {
            GameObject line = Add(_line, parent);
            UiClone.Words(line.transform, words);
            return line.GetComponent<TMP_Text>();
        }

        public void Headline(string key)
        {
            Headline(key, null);
        }

        /// <summary>A headline over a grey rule, in the box or (with a parent) anywhere else. Returns what was made.</summary>
        public Transform Headline(string key, Transform parent)
        {
            if (_headline == null)
            {
                return Line("<b>" + key.GetLocalization() + "</b>", parent).transform;
            }
            GameObject headline = Add(_headline, parent);
            var text = headline.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                UiClone.Key(text, key);
                // the game's headline carries a small icon and a tooltip mark inside its text: not here
                for (int i = text.transform.childCount - 1; i >= 0; i--)
                {
                    text.transform.GetChild(i).gameObject.SetActive(false);
                }
            }
            return headline.transform;
        }

        /// <summary>A switch with its label. False if the game's switch row could not be had: the caller leaves the choice out.</summary>
        public bool Switch(string labelKey, bool on, Action<bool> changed)
        {
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
        /// A previous/next selector over a fixed number of choices; 'text' gives the words of a choice, 'changed' is
        /// told the new one. It does not wrap round: the arrow at either end goes grey.
        /// </summary>
        public bool Selector(int count, int index, Func<int, string> text, Action<int> changed)
        {
            return Selector(count, index, text, changed, null);
        }

        /// <summary>The same in the box, or (with a parent) anywhere else.</summary>
        public bool Selector(int count, int index, Func<int, string> text, Action<int> changed, Transform parent)
        {
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

        // Every button under an arrow gets the click: the game's arrow is a button inside a button.
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
        /// The row of action buttons, in the box or (with a parent) anywhere else. The row has the game's three kinds:
        /// index 0 the positive one, 1 the negative one, 2 the additional one; all three start hidden (Button below).
        /// </summary>
        public Transform ButtonRow(Transform parent = null)
        {
            GameObject row = Add(_buttons, parent);
            for (int i = 0; i < row.transform.childCount; i++)
            {
                row.transform.GetChild(i).gameObject.SetActive(false);
            }
            // The buttons that show share the row between them, as in the game: a button has no width of its own
            // (its picture stretches to it), so the row has to hand the width out.
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
            }
            return row.transform;
        }

        public const int Positive = 0, Negative = 1, Additional = 2;

        /// <summary>Shows one of a row's three buttons with its word and its click. A row with fewer kinds uses the last it has.</summary>
        public static Button Button(Transform row, int kind, string key, Action click)
        {
            if (row == null || row.childCount == 0)
            {
                return null;
            }
            Transform child = row.GetChild(Mathf.Clamp(kind, 0, row.childCount - 1));
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
        /// How the page fits the box, for the log: the height the page asks for against the height the box has. The
        /// box does not scroll, so a page taller than the box loses its foot. Works the layout out at once: call it
        /// only where a line is being written anyway (diagnostics on, a click).
        /// </summary>
        public string Fit()
        {
            var rect = (RectTransform)_box;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            float asks = LayoutUtility.GetPreferredHeight(rect), has = rect.rect.height;
            float room = rect.parent is RectTransform outer ? outer.rect.height : has;
            return "the page asks for " + Mathf.RoundToInt(asks) + " px, the box is " + Mathf.RoundToInt(has) + " px in " + Mathf.RoundToInt(room) + " px" + (asks > room + 1f ? ": ITS FOOT IS CUT OFF" : string.Empty);
        }

        /// <summary>The layout is worked out now, not next frame: a page is written in one go and shown at once.</summary>
        public void Done()
        {
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)_box);
        }
    }
}
