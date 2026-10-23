using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.InputSystem;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A list of places to travel to, in a copy of the game's subway station window put on the HUD's canvas, with two lines of
    /// text where the game has its search field; entries are made once and reused. Costs nothing while closed; while open,
    /// one look at the cancel key a frame.
    /// </summary>
    public sealed class TravelList
    {
        public static IModLogger Logger;

        private static readonly string[] Harmless = { "ButtonEffects", "ScrollBarDraggingComponent", "ScrollBarHandleSizeLimiter" };
        private static int _open;

        /// <summary>True while any such window is on screen: the mod's camera frees the mouse for it.</summary>
        public static bool AnyOpen => _open > 0;

        private sealed class Row
        {
            public GameObject Object;
            public Component Label;
            public Component ButtonLabel;
            public Button Button;
            public string ButtonKey;
            public int Id;
            public bool Note;
        }

        private readonly List<Row> _rows = new List<Row>(48);
        private GameObject _root;
        private GameObject _template;
        private Component _title;
        private Component _info;
        private TextLocalizationComponent _cancel;
        private GameObject _cancelButton;
        private Transform _home;
        private bool _locked;
        private ScrollRect _scroll;
        private Action<int> _onPick;
        private Action _onClosed;
        private int _used;

        public bool IsOpen { get; private set; }

        /// <summary>The player may not leave: Cancel hidden, the cancel key left to the game. The owner can still close it; every closing clears it.</summary>
        public bool Locked
        {
            get { return _locked; }
            set
            {
                _locked = value;
                if (_cancelButton != null && _cancelButton.activeSelf == value)
                {
                    _cancelButton.SetActive(!value);
                }
            }
        }

        /// <summary>Entries made so far, for the log.</summary>
        public int Made => _rows.Count;

        /// <summary>The window's rectangle on the HUD's canvas, for an owner that moves or widens it; null once the interface has gone.</summary>
        public RectTransform Window => _root != null ? (RectTransform)_root.transform : null;

        /// <summary>An entry's height, and whether the lines under the name wrap: before the first Add.</summary>
        public void EntryShape(float height, bool wrap)
        {
            if (_template == null)
            {
                return;
            }
            var rect = (RectTransform)_template.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            Transform label = _template.transform.Find("Label");
            var text = label != null ? label.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            }
        }

        /// <summary>The window, asleep. Null, with one line in Player.log, if the game's window is not built as expected.</summary>
        public static TravelList Make(string name)
        {
            GameObject root = null;
            Transform holder = null;
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                Transform canvas = uis != null && uis.playerHUD != null ? uis.playerHUD.transform : null;
                if (uis == null || uis.cityMapSubwayStations == null || canvas == null)
                {
                    UiClone.Fallback("the game's list of subway stations is not there to copy, so '" + name + "' is not made");
                    return null;
                }
                using var piece = SessionWatch.Piece("game UI: the travel list being copied from the game's list of subway stations");
                holder = UiClone.Dormant(canvas, name + " Holder");
                root = UiClone.Asleep(uis.cityMapSubwayStations.gameObject, holder, name);
                UiClone.StripGameScripts(root, script => Array.IndexOf(Harmless, script.GetType().Name) >= 0);

                var list = new TravelList { _root = root, _home = canvas };
                Transform header = UiClone.Need(root.transform, "Headline/Header", name);
                Transform top = UiClone.Need(root.transform, "Top Controls", name);
                Transform content = UiClone.Need(root.transform, "Scroll View/Viewport/Content", name);
                Transform cancel = UiClone.Need(root.transform, "Cancel", name);
                Transform entry = content != null ? content.Find("Entry") : null;
                if (entry == null && content != null)
                {
                    // Should the game rename it: the first entry with a label and a button.
                    for (int i = 0; i < content.childCount && entry == null; i++)
                    {
                        Transform child = content.GetChild(i);
                        entry = child.Find("Label") != null && child.GetComponentInChildren<Button>(true) != null ? child : null;
                    }
                }
                Transform label = entry != null ? entry.Find("Label") : null;
                Button button = entry != null ? entry.GetComponentInChildren<Button>(true) : null;
                list._scroll = root.GetComponentInChildren<ScrollRect>(true);
                if (header == null || top == null || content == null || cancel == null || label == null || button == null || list._scroll == null)
                {
                    UiClone.Fallback("'" + name + "': the game's list of subway stations is not built as expected");
                    UnityEngine.Object.DestroyImmediate(holder.gameObject);
                    return null;
                }

                UiClone.EntryTemplate(entry, label);
                list._template = entry.gameObject;

                // Where the game has its search field: two lines of text in a copy of an entry's label.
                for (int i = top.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(top.GetChild(i).gameObject);
                }
                GameObject info = UnityEngine.Object.Instantiate(label.gameObject, top, false);
                info.name = "Info";
                var infoRect = (RectTransform)info.transform;
                infoRect.anchorMin = infoRect.anchorMax = new Vector2(0f, 1f);
                infoRect.pivot = new Vector2(0.5f, 0.5f);
                infoRect.sizeDelta = new Vector2(0f, 150f);
                var infoText = info.GetComponent<TMP_Text>();
                if (infoText != null)
                {
                    infoText.richText = true;
                    infoText.textWrappingMode = TextWrappingModes.Normal;
                    infoText.overflowMode = TextOverflowModes.Ellipsis;
                    infoText.alignment = TextAlignmentOptions.TopLeft;
                }
                info.SetActive(true);
                list._info = info.transform;
                list._title = header;

                UiClone.Rewire(cancel, list.Close);
                list._cancel = cancel.GetComponentInChildren<TextLocalizationComponent>(true);
                list._cancelButton = cancel.gameObject;

                UiClone.Visible(root.GetComponent<CanvasGroup>());
                // The middle of the screen, where the game asks the player things: nothing of the HUD stands there.
                root.SetActive(false);
                var rect = (RectTransform)root.transform;
                rect.SetParent(canvas, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(775f, 1360f);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.SetAsLastSibling();
                root.AddComponent<TravelListKeys>().Owner = list;
                UnityEngine.Object.DestroyImmediate(holder.gameObject);
                return list;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("'" + name + "' could not be built from the game's list of subway stations");
                if (holder != null)
                {
                    UnityEngine.Object.DestroyImmediate(holder.gameObject);
                }
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
                return null;
            }
        }

        /// <summary>Stands the window on another canvas (the time skip's own screen), or back on the HUD's with null.</summary>
        public void MoveTo(Transform canvas)
        {
            Transform target = canvas != null ? canvas : _home;
            if (_root == null || target == null || _root.transform.parent == target)
            {
                return;
            }
            _root.transform.SetParent(target, false);
            _root.transform.SetAsLastSibling();
        }

        /// <summary>Back to the top of the entries, for an owner that fills the open window with another page.</summary>
        public void ScrollToTop()
        {
            if (_root != null && _scroll != null)
            {
                _scroll.StopMovement();
                if (_scroll.content != null)
                {
                    _scroll.content.anchoredPosition = Vector2.zero;
                }
            }
        }

        /// <summary>True while the window's objects are there: the game's interface is rebuilt with the city.</summary>
        public bool Alive => _root != null;

        /// <summary>Starts a filling: the headline and the lines under it. Entries follow with Add, then <see cref="Open"/>.</summary>
        public void Begin(string title, string info)
        {
            _used = 0;
            UiClone.Words(_title, title);
            Info(info);
        }

        /// <summary>The lines under the headline, as final words.</summary>
        public void Info(string words)
        {
            UiClone.Words(_info, words);
        }

        /// <summary>One entry: a name, a smaller line under it, and the button's locale key.</summary>
        public void Add(int id, string name, string detail, string buttonKey, bool enabled)
        {
            Entry(id, name, detail, buttonKey, enabled, 0f, false);
        }

        /// <summary>An entry taller than the others, for a detail of several lines (see <see cref="HeightFor"/>).</summary>
        public void Add(int id, string name, string detail, string buttonKey, bool enabled, float height)
        {
            Entry(id, name, detail, buttonKey, enabled, height, false);
        }

        /// <summary>A line of text with no button: something to read before the choices.</summary>
        public void AddNote(string name, string detail, float height)
        {
            Entry(0, name, detail, null, false, height, true);
        }

        /// <summary>An entry's height for a detail line this long: a name line, then the detail's lines at this window's width.</summary>
        public float HeightFor(int characters, bool button)
        {
            RectTransform window = Window;
            float width = window != null ? window.sizeDelta.x : 775f;
            float perLine = Mathf.Max(24f, (width - (button ? 420f : 150f)) / 13f);   // the smaller line is about 13 units a character
            int lines = Mathf.Max(1, Mathf.CeilToInt(characters / perLine));
            return Mathf.Max(_template != null ? ((RectTransform)_template.transform).sizeDelta.y : 104f, 58f + 28f * lines);
        }

        private void Entry(int id, string name, string detail, string buttonKey, bool enabled, float height, bool note)
        {
            Row row;
            if (_used < _rows.Count)
            {
                row = _rows[_used];
            }
            else
            {
                GameObject made = UnityEngine.Object.Instantiate(_template, _template.transform.parent, false);
                made.name = "Entry " + (_rows.Count + 1);
                row = new Row { Object = made, Label = made.transform.Find("Label") };
                row.Button = made.GetComponentInChildren<Button>(true);
                row.ButtonLabel = row.Button != null ? row.Button.GetComponentInChildren<TMP_Text>(true) : null;
                if (row.Button != null)
                {
                    Row clicked = row;
                    row.Button.onClick = new Button.ButtonClickedEvent();
                    row.Button.onClick.AddListener(Guard.Listener(() => Picked(clicked)));
                }
                _rows.Add(row);
            }
            _used++;
            row.Id = id;
            // An entry with no name is the detail alone, in the list's own lettering.
            UiClone.Words(row.Label, string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(detail) ? detail
                : string.IsNullOrEmpty(detail) ? "<uppercase>" + name + "</uppercase>"
                : "<uppercase>" + name + "</uppercase>\n<size=80%><color=#C9CED3>" + detail + "</color></size>");
            if (!note && !ReferenceEquals(row.ButtonKey, buttonKey))
            {
                row.ButtonKey = buttonKey;
                UiClone.Key(row.ButtonLabel, buttonKey);
            }
            if (row.Button != null)
            {
                row.Button.interactable = enabled;
                if (row.Button.gameObject.activeSelf == note)
                {
                    row.Button.gameObject.SetActive(!note);   // a note has none; an entry reused from a note's has it back
                }
            }
            row.Note = note;
            float high = height > 0f ? height : ((RectTransform)_template.transform).sizeDelta.y;
            var rect = (RectTransform)row.Object.transform;
            if (!Mathf.Approximately(rect.sizeDelta.y, high))
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, high);
            }
            if (!row.Object.activeSelf)
            {
                row.Object.SetActive(true);
            }
        }

        /// <summary>Shows what was filled in. A click calls <paramref name="onPick"/> with the entry's id; it stays open until <see cref="Close"/>.</summary>
        public void Open(Action<int> onPick, Action onClosed)
        {
            for (int i = _used; i < _rows.Count; i++)
            {
                if (_rows[i].Object.activeSelf)
                {
                    _rows[i].Object.SetActive(false);
                }
            }
            _onPick = onPick;
            _onClosed = onClosed;
            if (IsOpen || _root == null)
            {
                return;
            }
            if (_cancel != null)
            {
                _cancel.Key = "common_cancel";
                _cancel.Suffix = PlayerAction.Cancel.AsSuffix();   // the player's own binding, as the game's windows write it
            }
            IsOpen = true;
            _open++;
            _root.transform.SetAsLastSibling();
            _root.SetActive(true);
            _scroll.StopMovement();
            if (_scroll.content != null)
            {
                _scroll.content.anchoredPosition = Vector2.zero;   // the content hangs from the top of the view
            }
        }

        /// <summary>Closes the window (Cancel, the cancel key, or the owner). The closed callback runs once.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            _open = Mathf.Max(0, _open - 1);
            Locked = false;
            if (_root != null)
            {
                _root.SetActive(false);
            }
            Action closed = _onClosed;
            _onPick = null;
            _onClosed = null;
            if (closed != null)
            {
                UiClone.Safe(closed);
            }
        }

        public void Destroy()
        {
            if (IsOpen)
            {
                IsOpen = false;
                _open = Mathf.Max(0, _open - 1);
            }
            _onPick = null;
            _onClosed = null;
            _rows.Clear();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
            _root = null;
        }

        private void Picked(Row row)
        {
            Action<int> pick = _onPick;
            if (IsOpen && pick != null)
            {
                UiClone.Deselect(_root.transform);
                int id = row.Id;
                UiClone.Safe(() => pick(id));
            }
        }

        /// <summary>The window's object went with the game's interface while open: the count is put right.</summary>
        internal void Lost()
        {
            if (IsOpen)
            {
                IsOpen = false;
                _open = Mathf.Max(0, _open - 1);
            }
            _root = null;
        }
    }

    /// <summary>
    /// On the window's own object, so it runs a frame only while the window is open: the cancel key closes it, the press
    /// taken so it does not open the game's menu too (PlayerAction.Reset). Runs before the game's own key handling.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class TravelListKeys : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "TravelListKeys", typeof(TravelListKeys));   // its Update

        public TravelList Owner;

        private static readonly int HotUpdate = HotSpots.Slot("Core: TravelListKeys.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (Owner == null || !Owner.IsOpen)
            {
                return;
            }
            try
            {
                if (!Owner.Locked && PlayerAction.Cancel.Pressed())
                {
                    PlayerAction.Cancel.Reset();
                    Owner.Close();
                }
            }
            catch (Exception e)
            {
                TravelList.Logger?.Error(e);
                enabled = false;   // the Cancel button still closes it
            }
        }

        private void OnDestroy()
        {
            Owner?.Lost();
        }
    }
}
