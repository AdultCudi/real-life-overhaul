using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.InputSystem;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A list of places to travel to, in the window the game lists its subway stations in
    /// (Canvases/CityMap/SubwayStations, class CityMapSubwayStations): the headline bar, a scrolling list of entries
    /// that each have a name and a blue button, and the yellow Cancel button with its key. The window is copied
    /// asleep once per city, the scripts that make it the subway's are taken off, and it is put on the HUD's canvas
    /// (the game's own stands on the map's, which is only drawn while the map is open). Where the game has its search
    /// field this window has two lines of text, in a copy of an entry's own label.
    ///
    /// An entry is the game's entry: its label carries the name (in capitals, as the game writes a station's) and, on
    /// a second smaller line, the details; its button carries a word of the game's ("Travel"). Entries are made when
    /// first needed and used again on every opening, so opening the list a second time makes nothing.
    ///
    /// The cost: nothing while the window is closed (its object is switched off, and so is the one script of the
    /// mod's on it). While it is open that script tests the game's cancel key once a frame, which is how the game's
    /// own windows are closed.
    /// </summary>
    public sealed class TravelList
    {
        public static IModLogger Logger;

        private static readonly string[] Harmless = { "ButtonEffects", "ScrollBarDraggingComponent", "ScrollBarHandleSizeLimiter" };
        private static int _open;

        /// <summary>True while any such window is on screen: the mod's camera frees the mouse for it (Camera/ThirdPersonCamera.IsUiOpen).</summary>
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

        /// <summary>
        /// True while the window may not be left by the player: the Cancel button is out of sight and the game's cancel
        /// key is left to the game (its own menu still opens). The owner can still <see cref="Close"/> it. Put back to
        /// false by every closing, so a window handed to the next owner is an ordinary one.
        /// </summary>
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

        /// <summary>How many entries have been made so far (for the log).</summary>
        public int Made => _rows.Count;

        /// <summary>The window's own rectangle on the HUD's canvas, for an owner that stands it somewhere else than the middle or makes it wider. Null once the interface has gone.</summary>
        public RectTransform Window => _root != null ? (RectTransform)_root.transform : null;

        /// <summary>
        /// For a list whose entries say more than a name and one line: how high an entry is, and whether the lines
        /// under the name may run on to the next line. Call it before the first <see cref="Add"/>: entries already
        /// made keep the shape they were made with.
        /// </summary>
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
                    // The template by what it is made of, should the game have renamed it: the first entry with a label and a button.
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

                // The entries the game made for its stations go; its template stays, as this list's.
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    Transform child = content.GetChild(i);
                    if (child != entry)
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    }
                }
                entry.name = "Entry";
                if (!entry.CompareTag("Untagged"))
                {
                    entry.tag = "Untagged";
                }
                var entryRect = (RectTransform)entry;
                entryRect.sizeDelta = new Vector2(entryRect.sizeDelta.x, 104f);   // the game's 88.8 holds one line; this holds a name and a smaller line under it
                var text = label.GetComponent<TMP_Text>();
                if (text != null)
                {
                    text.richText = true;
                    text.fontStyle &= ~FontStyles.UpperCase;   // the name keeps the game's capitals by a tag of its own (Add); the line under it reads as a sentence
                    text.textWrappingMode = TextWrappingModes.NoWrap;
                    text.overflowMode = TextOverflowModes.Ellipsis;
                }
                entry.gameObject.SetActive(false);
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

                var group = root.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = 1f;
                    group.interactable = true;
                    group.blocksRaycasts = true;
                }
                // The middle of the screen, where the game puts what it asks the player: on the HUD nothing stands there.
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

        /// <summary>
        /// Stands the window on another canvas (the game's time skip hides the HUD but keeps a screen of its own), or back on the
        /// HUD's with null. It stays the last thing drawn there. Does nothing if it is already there.
        /// </summary>
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

        /// <summary>Back to the top of the entries: for an owner that fills the window again with another page while it is open.</summary>
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

        /// <summary>True while the window's objects are there (the game's interface is rebuilt with the city).</summary>
        public bool Alive => _root != null;

        /// <summary>Starts a filling: the headline and the lines under it, as final words. Entries follow with <see cref="Add"/>, then <see cref="Open"/>.</summary>
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

        /// <summary>One entry: a name, a smaller line under it, and the button's word (a locale key, written by the game's component).</summary>
        public void Add(int id, string name, string detail, string buttonKey, bool enabled)
        {
            Entry(id, name, detail, buttonKey, enabled, 0f, false);
        }

        /// <summary>An entry taller than the list's others, for a detail line that runs to several lines (see <see cref="HeightFor"/>).</summary>
        public void Add(int id, string name, string detail, string buttonKey, bool enabled, float height)
        {
            Entry(id, name, detail, buttonKey, enabled, height, false);
        }

        /// <summary>A line of text in the list's own lettering with no button: something to read before the choices.</summary>
        public void AddNote(string name, string detail, float height)
        {
            Entry(0, name, detail, null, false, height, true);
        }

        /// <summary>A height, in the list's units, for an entry whose detail line is this long: a name line, then the lines of the detail at about this window's width.</summary>
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
                    row.Button.onClick.AddListener(() => Picked(clicked));
                }
                _rows.Add(row);
            }
            _used++;
            row.Id = id;
            // (An entry with no name is the detail alone, in the list's own lettering: a page of plain text.)
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
                    row.Button.gameObject.SetActive(!note);   // (a note has none; an entry made from a note's has it back)
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

        /// <summary>Shows what was filled in. A click on an entry's button calls <paramref name="onPick"/> with its id; the window stays open until <see cref="Close"/>.</summary>
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
                _scroll.content.anchoredPosition = Vector2.zero;   // from the top: the content hangs from the top of the view
            }
        }

        /// <summary>Closes the window (the Cancel button, the cancel key, or the caller). The closed callback runs once.</summary>
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

        /// <summary>The window and everything made for it, gone.</summary>
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
                // The clicked button is not left selected: the entries are used again for other places, and the
                // interface's confirm key presses whatever is selected.
                EventSystem events = EventSystem.current;
                if (events != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(_root.transform))
                {
                    events.SetSelectedGameObject(null);
                }
                int id = row.Id;
                UiClone.Safe(() => pick(id));
            }
        }

        /// <summary>The window's object went with the game's interface while it was open: the count is put right.</summary>
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
    /// On the window's own object, so it runs only while the window is open: the game's cancel key closes the window,
    /// and the press is taken so that it does not open the game's menu as well (the game takes its presses the same
    /// way: PlayerAction.Reset). It runs before the game's own key handling.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class TravelListKeys : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "TravelListKeys", typeof(TravelListKeys));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public TravelList Owner;

        private static readonly int HotUpdate = HotSpots.Slot("Core: TravelListKeys.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
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
