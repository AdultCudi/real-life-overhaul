using System;
using BAModAPI;
using BigAmbitions.ModsInternal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>What a row of the game's settings menu holds.</summary>
    public enum OptionRowKind
    {
        Switch,     // the game's on and off switch
        Slider,     // the game's slider, with its number at the right
        Choice,     // the game's drop-down, with its arrows
        Button      // the game's blue button
    }

    /// <summary>
    /// The rows of the game's own settings menu (UIs.options, its Graphics and Others views), copied asleep once and made
    /// from as often as a page needs: a name over a short description (the menu's Label and Tip, as its "Reset windows" row
    /// has them) and at the right the game's switch, slider with its number, drop-down or button. The menu's rows are
    /// already white on dark; the words take the dark look. Costs nothing a frame; a page makes the rows it needs.
    /// </summary>
    public sealed class OptionRows
    {
        public static IModLogger Logger;

        private static readonly string[] Harmless =
        {
            "ButtonEffects", "ToggleClickSound", "HoverEffect", "ScrollBarDraggingComponent", "ScrollBarHandleSizeLimiter"
        };

        private const float ControlWidth = 700f;   // the menu's own column of controls (its rows' LayoutElement)
        private const float ButtonWidth = 600f, ButtonHeight = 84f;   // the menu's button, with the room its own fitter gave one line
        private const float Gap = 20f;             // between the words and the control, as the menu's switch rows have it

        private readonly GameObject[] _templates = new GameObject[4];

        /// <summary>The four templates, asleep under <paramref name="dormant"/>. Null, with one line in Player.log, if the game's menu is not built as expected.</summary>
        public static OptionRows Take(Transform dormant)
        {
            try
            {
                var menu = InstanceBehavior<UI.UIs>.Instance != null ? InstanceBehavior<UI.UIs>.Instance.options : null;
                if (menu == null)
                {
                    UiClone.Fallback("the game's settings menu is not there to copy its rows from");
                    return null;
                }
                ModOptionsButtonControl button = Described(menu.GetComponentsInChildren<ModOptionsButtonControl>(true));
                var toggle = menu.GetComponentInChildren<ModOptionsToggleControl>(true);
                var slider = menu.GetComponentInChildren<ModOptionsSliderControl>(true);
                var dropdown = menu.GetComponentInChildren<ModOptionsDropdownControl>(true);
                if (button == null || toggle == null || slider == null || dropdown == null)
                {
                    string missing = button == null ? "button row with a description" : toggle == null ? "switch row" : slider == null ? "slider row" : "drop-down row";
                    UiClone.Fallback("the game's settings menu has no " + missing);
                    return null;
                }
                var rows = new OptionRows();
                GameObject words = rows.TakeButton(button.gameObject, dormant);
                if (words == null
                    || !rows.TakeControl(OptionRowKind.Switch, toggle.gameObject, words, dormant)
                    || !rows.TakeControl(OptionRowKind.Slider, slider.gameObject, words, dormant)
                    || !rows.TakeControl(OptionRowKind.Choice, dropdown.gameObject, words, dormant))
                {
                    return null;
                }
                UnityEngine.Object.DestroyImmediate(words);
                return rows;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("the rows of the game's settings menu could not be copied");
                return null;
            }
        }

        /// <summary>
        /// The menu's switch row alone, asleep under <paramref name="dormant"/>, for a page that is not the settings app's
        /// (InfoBox.Toggle). Null as Take is. The other three rows are made and let go with it, once.
        /// </summary>
        public static GameObject SwitchRow(Transform dormant)
        {
            OptionRows rows = Take(dormant);
            if (rows == null)
            {
                return null;
            }
            for (int i = 0; i < rows._templates.Length; i++)
            {
                if (i != (int)OptionRowKind.Switch && rows._templates[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(rows._templates[i]);
                }
            }
            return rows._templates[(int)OptionRowKind.Switch];
        }

        // The button row with a description under its name ("Reset windows"): its two lines become every row's words.
        private static ModOptionsButtonControl Described(ModOptionsButtonControl[] all)
        {
            for (int i = 0; i < all.Length; i++)
            {
                Transform labels = all[i].transform.childCount > 0 ? all[i].transform.GetChild(0) : null;
                if (labels != null && labels.GetComponent<VerticalLayoutGroup>() != null && labels.childCount >= 2 && labels.GetComponentsInChildren<TMP_Text>(true).Length >= 2)
                {
                    return all[i];
                }
            }
            return null;
        }

        // Its name and description kept apart as every row's words; its blue button at the right.
        private GameObject TakeButton(GameObject source, Transform dormant)
        {
            GameObject copy = UiClone.Asleep(source, dormant, "RLO Option Button");
            UiClone.StripGameScripts(copy, script => Array.IndexOf(Harmless, script.GetType().Name) >= 0);
            Tidy(copy);
            Transform labels = copy.transform.GetChild(0);
            Transform container = copy.transform.childCount > 1 ? copy.transform.GetChild(1) : null;
            Button button = container != null ? container.GetComponentInChildren<Button>(true) : null;
            if (button == null)
            {
                UiClone.Fallback("the game's settings menu's button row has no button");
                UnityEngine.Object.DestroyImmediate(copy);
                return null;
            }
            UiClone.Rewire(button, null);
            var column = container.GetComponent<VerticalLayoutGroup>();
            if (column != null)
            {
                column.childAlignment = TextAnchor.MiddleRight;
            }
            foreach (ContentSizeFitter fitter in button.GetComponents<ContentSizeFitter>())
            {
                UnityEngine.Object.DestroyImmediate(fitter);   // it keeps one size; its own fitter (the game's, taken off) sized it to its words
            }
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            TMP_Text caption = Caption(button);
            if (caption != null)
            {
                caption.textWrappingMode = TextWrappingModes.NoWrap;
                caption.enableAutoSizing = true;
                caption.fontSizeMin = 22f;
                caption.fontSizeMax = caption.fontSize;
            }
            Words(labels);
            GameObject words = UnityEngine.Object.Instantiate(labels.gameObject, dormant, false);
            words.name = "Labels";
            _templates[(int)OptionRowKind.Button] = copy;
            return words;
        }

        // A switch, slider or drop-down row: the menu's single label replaced by the two lines of words.
        private bool TakeControl(OptionRowKind kind, GameObject source, GameObject words, Transform dormant)
        {
            GameObject copy = UiClone.Asleep(source, dormant, "RLO Option " + kind);
            // The drop-down is the game's control, whole: its scripts stay (as FilterDrop keeps them).
            var dropdown = copy.GetComponentInChildren<global::UI.Elements.Dropdown>(true);
            Transform keep = kind == OptionRowKind.Choice && dropdown != null ? dropdown.transform : null;
            UiClone.StripGameScripts(copy, script => Array.IndexOf(Harmless, script.GetType().Name) >= 0 || (keep != null && script.transform.IsChildOf(keep)));
            Tidy(copy);
            Transform label = null;
            for (int i = 0; i < copy.transform.childCount && label == null; i++)
            {
                Transform child = copy.transform.GetChild(i);
                if (child.GetComponent<TMP_Text>() != null)
                {
                    label = child;
                }
            }
            bool ok = label != null && (kind != OptionRowKind.Switch || copy.GetComponentInChildren<Toggle>(true) != null)
                      && (kind != OptionRowKind.Slider || copy.GetComponentInChildren<Slider>(true) != null) && (kind != OptionRowKind.Choice || keep != null);
            if (!ok)
            {
                UiClone.Fallback("the game's settings menu's " + kind + " row is not built as expected");
                UnityEngine.Object.DestroyImmediate(copy);
                return false;
            }
            int at = label.GetSiblingIndex();
            UnityEngine.Object.DestroyImmediate(label.gameObject);
            GameObject labels = UnityEngine.Object.Instantiate(words, copy.transform, false);
            labels.name = "Labels";
            labels.transform.SetSiblingIndex(at);
            if (kind == OptionRowKind.Switch)
            {
                // At the right edge of the column, where every other control ends.
                Toggle toggle = copy.GetComponentInChildren<Toggle>(true);
                Transform track = toggle.transform.childCount > 0 ? toggle.transform.GetChild(0) : null;
                if (track is RectTransform r)
                {
                    r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0.5f);
                    r.anchoredPosition = Vector2.zero;
                }
            }
            _templates[(int)kind] = copy;
            return true;
        }

        // No fitter of its own (the page sets its height), and a gap before the control.
        private static void Tidy(GameObject row)
        {
            foreach (ContentSizeFitter fitter in row.GetComponents<ContentSizeFitter>())
            {
                UnityEngine.Object.DestroyImmediate(fitter);
            }
            var line = row.GetComponent<HorizontalLayoutGroup>();
            if (line != null)
            {
                line.spacing = Gap;
                line.childAlignment = TextAnchor.MiddleLeft;
                line.childForceExpandHeight = false;
            }
            for (int i = 1; i < row.transform.childCount; i++)
            {
                var element = row.transform.GetChild(i).GetComponent<LayoutElement>();
                if (element != null)
                {
                    element.minWidth = ControlWidth;
                    element.preferredWidth = ControlWidth;
                    element.flexibleWidth = 0f;
                }
            }
        }

        // The name in the dark look's white, the description smaller and softer, both wrapping.
        private static void Words(Transform labels)
        {
            var element = labels.GetComponent<LayoutElement>() ?? labels.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 9999f;   // as the menu's own label: all the room the control leaves
            element.flexibleWidth = 1f;
            element.minWidth = 0f;
            TMP_Text[] texts = labels.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text t = texts[i];
                t.textWrappingMode = TextWrappingModes.Normal;
                t.overflowMode = TextOverflowModes.Overflow;
                t.richText = true;
                t.raycastTarget = false;
                if (i == 0)
                {
                    t.color = DarkLook.Text;
                }
                else
                {
                    t.color = DarkLook.Soft;
                    t.fontStyle = FontStyles.Normal;
                }
            }
            var column = labels.GetComponent<VerticalLayoutGroup>();
            if (column != null)
            {
                column.spacing = 8f;
                column.childForceExpandHeight = false;
            }
        }

        private static TMP_Text Caption(Button button)
        {
            return button.GetComponentInChildren<TMP_Text>(true);
        }

        /// <summary>A row of the kind, made awake under <paramref name="parent"/> (so the game's drop-down starts), its parts found.</summary>
        public OptionRow Make(OptionRowKind kind, Transform parent)
        {
            GameObject copy = UnityEngine.Object.Instantiate(_templates[(int)kind], parent, false);
            copy.name = "Row " + kind;
            copy.SetActive(true);
            var row = new OptionRow { Root = copy, Kind = kind };
            TMP_Text[] words = copy.transform.Find("Labels").GetComponentsInChildren<TMP_Text>(true);
            row.Name = words[0];
            row.Note = words.Length > 1 ? words[1] : null;
            switch (kind)
            {
                case OptionRowKind.Switch:
                    row.Switch = copy.GetComponentInChildren<Toggle>(true);
                    row.Switch.onValueChanged = new Toggle.ToggleEvent();
                    break;
                case OptionRowKind.Slider:
                    row.Slider = copy.GetComponentInChildren<Slider>(true);
                    row.Slider.onValueChanged = new Slider.SliderEvent();
                    row.Slider.wholeNumbers = true;
                    TMP_Text[] values = row.Slider.transform.parent.GetComponentsInChildren<TMP_Text>(true);   // the number stands after the slider
                    row.Value = values.Length > 0 ? values[values.Length - 1] : null;
                    if (row.Value != null)
                    {
                        row.Value.color = DarkLook.Text;
                    }
                    break;
                case OptionRowKind.Choice:
                    row.Choice = FilterDrop.Of(copy);
                    break;
                default:
                    row.Button = copy.GetComponentInChildren<Button>(true);
                    row.Caption = row.Button != null ? Caption(row.Button) : null;
                    break;
            }
            return row;
        }
    }

    /// <summary>One row made from the game's settings menu. The caller wires the control once and writes it on each page.</summary>
    public sealed class OptionRow
    {
        public GameObject Root;
        public OptionRowKind Kind;
        public TMP_Text Name;
        public TMP_Text Note;          // the description under the name
        public Toggle Switch;
        public Slider Slider;
        public TMP_Text Value;         // the slider's number
        public FilterDrop Choice;
        public Button Button;
        public TMP_Text Caption;       // the word on the button

        /// <summary>Usable or not: the game's own look for a control that is not (Selectable.interactable), and its name dimmed.</summary>
        public void SetUsable(bool usable)
        {
            if (Switch != null)
            {
                Switch.interactable = usable;
            }
            if (Slider != null)
            {
                Slider.interactable = usable;
            }
            if (Choice != null)
            {
                Choice.SetInteractable(usable);
            }
            if (Button != null)
            {
                Button.interactable = usable;
            }
            if (Name != null)
            {
                Color c = DarkLook.Text;
                Name.color = usable ? c : new Color(c.r, c.g, c.b, 0.5f);
            }
        }

        public void Show(bool shown)
        {
            if (Root.activeSelf != shown)
            {
                Root.SetActive(shown);
            }
        }
    }
}
