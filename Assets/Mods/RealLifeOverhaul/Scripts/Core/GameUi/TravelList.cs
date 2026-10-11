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
        }

        private readonly List<Row> _rows = new List<Row>(48);
        private GameObject _root;
        private GameObject _template;
        private Component _title;
        private Component _info;
        private TextLocalizationComponent _cancel;
        private ScrollRect _scroll;
        private Action<int> _onPick;
        private Action _onClosed;
        private int _used;

        public bool IsOpen { get; private set; }

        /// <summary>How many entries have been made so far (for the log).</summary>
        public int Made => _rows.Count;

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
                SessionWatch.Mark("game UI: the travel list being copied from the game's list of subway stations");
                holder = UiClone.Dormant(canvas, name + " Holder");
                root = UiClone.Asleep(uis.cityMapSubwayStations.gameObject, holder, name);
                UiClone.StripGameScripts(root, script => Array.IndexOf(Harmless, script.GetType().Name) >= 0);

                var list = new TravelList { _root = root };
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
            UiClone.Words(row.Label, string.IsNullOrEmpty(detail) ? "<uppercase>" + name + "</uppercase>"
                : "<uppercase>" + name + "</uppercase>\n<size=80%><color=#C9CED3>" + detail + "</color></size>");
            if (!ReferenceEquals(row.ButtonKey, buttonKey))
            {
                row.ButtonKey = buttonKey;
                UiClone.Key(row.ButtonLabel, buttonKey);
            }
            if (row.Button != null)
            {
                row.Button.interactable = enabled;
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
        public TravelList Owner;

        private void Update()
        {
            if (Owner == null || !Owner.IsOpen)
            {
                return;
            }
            try
            {
                if (PlayerAction.Cancel.Pressed())
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
