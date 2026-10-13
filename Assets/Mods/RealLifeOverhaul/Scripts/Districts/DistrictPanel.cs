using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.Districts
{
    /// <summary>
    /// The district view's window on the city map: a copy of the game's own filter window (Canvases/CityMap/MapFilters,
    /// class CityMapFilters), standing beside it on the map's canvas, the same size. Its headline names the layer, the
    /// box under the headline (where the game has its search field) says what the layer shows, and its list holds:
    ///
    ///   a category line     the game's own line (an orange word over a grey rule), for "Key", the district's name, "Districts"
    ///   a key row           the game's own filter row with its icon a disc in the band's colour: the band and its range
    ///   a card line         the same row with no icon and no switch: one figure of the district pointed at
    ///   a district row      the row with the disc in its band's colour, its figure, and the game's arrow button that flies
    ///                       the map there; pointing at it shows its card, clicking it keeps the card on it
    ///
    /// Its yellow button at the foot, which closes the map in the game's window, switches the view off here. The copy is
    /// made asleep the first time the view is shown in a city, the game's scripts are taken off (the ones that only draw
    /// or sound stay), and the rows the game's window had are destroyed: only its two templates are kept. Rows are made
    /// when first needed and used again on every filling. Nothing of it runs while it is hidden, and nothing runs per
    /// frame while it shows: it is written again only when the layer, the day or the district pointed at changes.
    /// </summary>
    internal sealed class DistrictPanel
    {
        public const string ObjectName = "RealLife District View";
        private const float Gap = 30f;                 // between it and the game's filter window
        private const float CardPerChar = 13.5f;       // units of width a character of the rows' lettering takes, about
        private static readonly string[] Harmless = { "ButtonEffects", "ScrollBarDraggingComponent", "ScrollBarHandleSizeLimiter", "ToggleClickSound" };

        public static IModLogger Logger;

        private sealed class Slot
        {
            public GameObject Object;
            public RectTransform Rect;
            public Component Label;
            public TMP_Text Text;
            public RectTransform LabelRect;
            public RectTransform Horizontal;
            public Image Icon;
            public GameObject Focus;
            public string District;
            public Vector3 Place;
        }

        private readonly List<Slot> _lines = new List<Slot>(4);
        private readonly List<Slot> _rows = new List<Slot>(32);
        private GameObject _root;
        private Component _title;
        private TMP_Text _about;
        private GameObject _lineTemplate, _rowTemplate;
        private float _rowWidth = 655f;
        private int _usedLines, _usedRows, _order;
        private Sprite _disc;

        /// <summary>Told with a district's id when the pointer comes onto its row, and with null when it leaves.</summary>
        public Action<string> Pointed;
        /// <summary>Told with a district's id when its row is clicked.</summary>
        public Action<string> Clicked;
        /// <summary>The yellow button: the view is switched off.</summary>
        public Action Closed;

        public bool Alive => _root != null;
        public bool Shown => _root != null && _root.activeSelf;

        /// <summary>The window, asleep. Null, with one line in Player.log, if the game's filter window is not built as expected.</summary>
        public static DistrictPanel Make()
        {
            Transform holder = null;
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                CityMapFilters original = uis != null ? uis.mapFilters : null;
                Transform canvas = original != null ? original.transform.parent : null;
                if (canvas == null)
                {
                    UiClone.Fallback("the city map's filter window is not there to copy, so the district view has no window");
                    return null;
                }
                holder = UiClone.Dormant(canvas, ObjectName + " Holder");
                GameObject root = UiClone.Asleep(original.gameObject, holder, ObjectName);
                UiClone.StripGameScripts(root, script => Array.IndexOf(Harmless, script.GetType().Name) >= 0);
                var panel = new DistrictPanel { _root = root };
                Transform header = UiClone.Need(root.transform, "Headline/Header", "the district view");
                Transform top = UiClone.Need(root.transform, "Top Controls", "the district view");
                Transform close = UiClone.Need(root.transform, "CloseCityMapButton", "the district view");
                ScrollRect scroll = root.GetComponentInChildren<ScrollRect>(true);
                Transform content = scroll != null ? scroll.content : null;
                Transform row = content != null ? content.Find("Entry") : null;
                Transform line = content != null ? content.Find("LineEntry") : null;
                Transform label = row != null ? row.Find("Horizontal/Label") : null;
                if (header == null || top == null || close == null || content == null || row == null || line == null || label == null)
                {
                    UiClone.Fallback("the city map's filter window is not built as expected, so the district view has no window");
                    UnityEngine.Object.DestroyImmediate(holder.gameObject);
                    return null;
                }
                // The game's rows go (the copy brought every live one with it); its two templates stay, as this window's.
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    Transform child = content.GetChild(i);
                    if (child != row && child != line)
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                }
                foreach (Transform template in new[] { row, line })
                {
                    if (!template.CompareTag("Untagged"))
                    {
                        template.tag = "Untagged";
                    }
                    template.gameObject.SetActive(false);
                }
                Hide(row, "Toggle");
                Hide(line, "Collapse");
                Hide(line, "Toggle");
                panel._rowTemplate = row.gameObject;
                panel._lineTemplate = line.gameObject;

                // Where the game has its search field, a few lines of the rows' own lettering.
                for (int i = top.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(top.GetChild(i).gameObject);
                }
                GameObject about = UnityEngine.Object.Instantiate(label.gameObject, top, false);
                about.name = "About";
                var aboutLayout = about.GetComponent<LayoutElement>();
                if (aboutLayout != null)
                {
                    aboutLayout.preferredWidth = -1f;
                }
                var aboutRect = (RectTransform)about.transform;
                aboutRect.sizeDelta = new Vector2(aboutRect.sizeDelta.x, ((RectTransform)top).sizeDelta.y);
                panel._about = about.GetComponent<TMP_Text>();
                if (panel._about != null)
                {
                    panel._about.textWrappingMode = TextWrappingModes.Normal;
                    panel._about.overflowMode = TextOverflowModes.Ellipsis;
                    panel._about.alignment = TextAlignmentOptions.TopLeft;
                    panel._about.fontSize = 24f;
                    panel._about.raycastTarget = false;
                }
                panel._title = header;
                UiClone.Rewire(close, () => panel.Closed?.Invoke());
                UiClone.Key(close.GetComponentInChildren<TMP_Text>(true), "reallife_dv_close");

                var group = root.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = 1f;
                    group.interactable = true;
                    group.blocksRaycasts = true;
                }
                // Beside the game's own window, to its left, at its height: the game's anchors, moved over by its width.
                root.SetActive(false);
                var rect = (RectTransform)root.transform;
                var own = (RectTransform)original.transform;
                rect.SetParent(canvas, false);
                rect.SetSiblingIndex(original.transform.GetSiblingIndex() + 1);
                rect.anchoredPosition = own.anchoredPosition - new Vector2(own.sizeDelta.x + Gap, 0f);
                var scrollRect = (RectTransform)scroll.transform;
                var contentRect = (RectTransform)content;
                float width = own.sizeDelta.x + scrollRect.sizeDelta.x + contentRect.sizeDelta.x;   // (both stretch with the window, inset by their negative sizes)
                if (width > 100f)
                {
                    panel._rowWidth = width;
                }
                panel._disc = GameSprites.Get("circle") ?? Disc();
                UnityEngine.Object.DestroyImmediate(holder.gameObject);
                return panel;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("the district view's window could not be built from the city map's filter window");
                if (holder != null)
                {
                    UnityEngine.Object.DestroyImmediate(holder.gameObject);
                }
                return null;
            }
        }

        private static void Hide(Transform under, string path)
        {
            Transform found = under.Find(path);
            if (found != null)
            {
                found.gameObject.SetActive(false);
            }
        }

        // A white disc for the colour swatches, should the game's own "circle" not be in memory: drawn once.
        private static Sprite Disc()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size / 2f, dy = y + 0.5f - size / 2f;
                    float edge = size / 2f - Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(edge) * 255f));
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "RealLifeOverhaul District Disc" };
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        public void Show(bool on)
        {
            if (_root != null && _root.activeSelf != on)
            {
                _root.SetActive(on);
                if (!on)
                {
                    Pointed?.Invoke(null);
                }
            }
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
            _root = null;
            _lines.Clear();
            _rows.Clear();
        }

        // ---------------------------------------------------------------- filling

        /// <summary>Starts a filling: the headline and the words under it. Lines and rows follow in order, then <see cref="End"/>.</summary>
        public void Begin(string title, string about)
        {
            _usedLines = _usedRows = _order = 0;
            UiClone.Words(_title, title);
            if (_about != null)
            {
                UiClone.Words(_about, about);
            }
        }

        /// <summary>A category line: an orange word over the game's grey rule.</summary>
        public void Heading(string words)
        {
            Slot slot = Take(_lines, ref _usedLines, _lineTemplate, false);
            UiClone.Words(slot.Label, words);
        }

        /// <summary>A row of the key: a disc in a band's colour and the band's words.</summary>
        public void Swatch(Tint tint, string words)
        {
            Slot slot = Take(_rows, ref _usedRows, _rowTemplate, true);
            Fill(slot, tint, true, words, null, Vector3.zero);
        }

        /// <summary>One of the card's lines: words with no disc and no button, as long as the window is wide (it wraps).</summary>
        public void Line(string words)
        {
            Slot slot = Take(_rows, ref _usedRows, _rowTemplate, true);
            Fill(slot, default, false, words, null, Vector3.zero);
        }

        /// <summary>A district's row: its band's disc, its words, the arrow that flies the map to it; pointing at it and clicking it are told.</summary>
        public void District(Tint tint, string district, string words, Vector3 place)
        {
            Slot slot = Take(_rows, ref _usedRows, _rowTemplate, true);
            Fill(slot, tint, true, words, district, place);
        }

        /// <summary>Ends a filling: what was not used this time is put away.</summary>
        public void End()
        {
            for (int i = _usedLines; i < _lines.Count; i++)
            {
                if (_lines[i].Object.activeSelf)
                {
                    _lines[i].Object.SetActive(false);
                }
            }
            for (int i = _usedRows; i < _rows.Count; i++)
            {
                if (_rows[i].Object.activeSelf)
                {
                    _rows[i].Object.SetActive(false);
                }
                _rows[i].District = null;
            }
        }

        private Slot Take(List<Slot> pool, ref int used, GameObject template, bool row)
        {
            Slot slot;
            if (used < pool.Count)
            {
                slot = pool[used];
            }
            else
            {
                GameObject made = UnityEngine.Object.Instantiate(template, template.transform.parent, false);
                made.name = (row ? "Row " : "Line ") + (pool.Count + 1);
                slot = new Slot { Object = made, Rect = (RectTransform)made.transform };
                slot.Label = made.transform.Find(row ? "Horizontal/Label" : "Label");
                slot.Text = slot.Label != null ? slot.Label.GetComponent<TMP_Text>() : null;
                slot.LabelRect = slot.Label != null ? (RectTransform)slot.Label.transform : null;
                if (row)
                {
                    slot.Horizontal = made.transform.Find("Horizontal") as RectTransform;
                    Transform icon = made.transform.Find("Icon");
                    slot.Icon = icon != null ? icon.GetComponent<Image>() : null;
                    Transform focus = made.transform.Find("Horizontal/FocusButton");
                    slot.Focus = focus != null ? focus.gameObject : null;
                    Slot owner = slot;
                    if (focus != null)
                    {
                        UiClone.Rewire(focus, () => DistrictGame.FlyTo(owner.Place));
                    }
                    UiClone.Rewire(made.transform, () =>
                    {
                        if (owner.District != null)
                        {
                            Clicked?.Invoke(owner.District);
                        }
                    });
                    var pointer = made.AddComponent<DistrictPanelRow>();
                    pointer.Enter = () =>
                    {
                        if (owner.District != null)
                        {
                            Pointed?.Invoke(owner.District);
                        }
                    };
                    pointer.Exit = () =>
                    {
                        if (owner.District != null)
                        {
                            Pointed?.Invoke(null);
                        }
                    };
                    if (slot.Text != null)
                    {
                        slot.Text.richText = true;
                        slot.Text.overflowMode = TextOverflowModes.Ellipsis;
                    }
                }
                pool.Add(slot);
            }
            used++;
            slot.Object.transform.SetSiblingIndex(_order++);
            if (!slot.Object.activeSelf)
            {
                slot.Object.SetActive(true);
            }
            return slot;
        }

        private void Fill(Slot slot, Tint tint, bool disc, string words, string district, Vector3 place)
        {
            slot.District = district;
            slot.Place = place;
            if (slot.Icon != null)
            {
                slot.Icon.enabled = disc;
                if (disc)
                {
                    slot.Icon.sprite = _disc;
                    slot.Icon.color = new Color32(tint.R, tint.G, tint.B, 255);
                    slot.Icon.preserveAspect = true;
                }
            }
            bool focus = district != null;
            if (slot.Focus != null && slot.Focus.activeSelf != focus)
            {
                slot.Focus.SetActive(focus);
            }
            // A card line takes the whole width; a row with a disc keeps the game's room for it on the left.
            float left = disc ? 88f : 20f;
            float right = 12f;
            if (slot.Horizontal != null)
            {
                slot.Horizontal.offsetMin = new Vector2(left, slot.Horizontal.offsetMin.y);
                slot.Horizontal.offsetMax = new Vector2(-right, slot.Horizontal.offsetMax.y);
            }
            UiClone.Words(slot.Label, words);
            float width = _rowWidth - left - right - (focus ? 63f : 0f);
            int lines = 1;
            if (slot.Text != null)
            {
                bool wrap = !disc;
                slot.Text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                if (wrap)
                {
                    lines = Mathf.Max(1, Mathf.CeilToInt(Plain(words).Length * CardPerChar / Mathf.Max(100f, width)));
                }
            }
            float high = disc ? 64f : 22f + 32f * lines;
            if (!Mathf.Approximately(slot.Rect.sizeDelta.y, high))
            {
                slot.Rect.sizeDelta = new Vector2(slot.Rect.sizeDelta.x, high);
            }
            if (slot.LabelRect != null && !Mathf.Approximately(slot.LabelRect.sizeDelta.y, high))
            {
                slot.LabelRect.sizeDelta = new Vector2(slot.LabelRect.sizeDelta.x, high);
            }
        }

        // The words without their rich-text tags, for counting the room they take.
        private static string Plain(string words)
        {
            if (string.IsNullOrEmpty(words) || words.IndexOf('<') < 0)
            {
                return words ?? string.Empty;
            }
            var plain = new System.Text.StringBuilder(words.Length);
            bool tag = false;
            for (int i = 0; i < words.Length; i++)
            {
                char c = words[i];
                if (c == '<')
                {
                    tag = true;
                }
                else if (c == '>')
                {
                    tag = false;
                }
                else if (!tag)
                {
                    plain.Append(c);
                }
            }
            return plain.ToString();
        }
    }

    /// <summary>A row of the district view's window: the pointer coming onto it and leaving it.</summary>
    public sealed class DistrictPanelRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Enter, Exit;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Enter != null)
            {
                UiClone.Safe(Enter);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Exit != null)
            {
                UiClone.Safe(Exit);
            }
        }
    }
}
