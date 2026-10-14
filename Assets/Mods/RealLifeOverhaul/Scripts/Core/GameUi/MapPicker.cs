using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using BigAmbitions.InputSystem;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A choice of places made on the open city map, the way the game offers its subway stations there
    /// (SubwayStation.Interact: CityMap.Toggle, then CityMap.ToggleSubwayMode). The game's window for its stations
    /// (Canvases/CityMap/SubwayStations, class CityMapSubwayStations) is copied asleep once per city, the scripts that
    /// make it the subway's are taken off, and the copy stands where the game's stands, on the map's canvas: the
    /// headline, the search field (it narrows the entries by name, as the game's does), the scrolling entries with a
    /// name, a smaller line under it and the blue button, and Cancel. While it is up the map's filters are put away, as
    /// the game puts them away for its stations, and so are the mod's own places (<see cref="MapPlaces.Quiet"/>).
    ///
    /// An entry with a place has one of the game's own markers over it (CityMap.AddPoi), in the owner's icon and colour;
    /// the place the player is at has its marker in the game's red, as the game marks the station the player is at.
    /// Pointing at a marker shows its name; clicking it picks it. Pointing at an entry glides the map to its place after
    /// the game's own delay and leaving it stops the glide, as the game's entries do (CityMapSubwayStationEntry); a
    /// click anywhere on an entry picks it.
    ///
    /// The map pauses the game while it is open (Time.timeScale 0), so everything here runs on the interface's clock,
    /// and an owner that ticks on the game's clock takes up a pick once the map has closed. The cost: nothing while it
    /// is closed. While it is open one script of the mod's on the window tests once a frame whether the map is still
    /// open. The entries and the markers are made when first needed and used again on every opening.
    /// </summary>
    public sealed class MapPicker
    {
        public static IModLogger Logger;

        private const float MarkerUp = 4.5f;   // metres over the place, as the mod's other markers stand (MapPlaces)
        private static readonly string[] Harmless = { "ButtonEffects", "ScrollBarDraggingComponent", "ScrollBarHandleSizeLimiter" };
        private static FieldInfo _blobField, _plateButtonField;
        private static bool _markersSaid;
        private static MapPicker _shown;

        /// <summary>True while a choice is up on the map.</summary>
        public static bool AnyOpen => _shown != null && _shown.IsOpen;

        private sealed class Row
        {
            public GameObject Object;
            public Component Label;
            public Component ButtonLabel;
            public Button Button;
            public Image Background;
            public TMP_Text Text;
            public string ButtonKey;
            public string Name;       // what the search field looks for
            public int Id;
            public bool Note;
            public bool Enabled;
            public bool HasPlace;
            public Vector3 Place;
            public int Mark = -1;     // its marker, or -1
        }

        private sealed class Mark
        {
            public PointOfInterest Poi;
            public Transform Anchor;
            public int Row = -1;      // the entry it is the marker of; -1 for the place the player is at
        }

        private readonly List<Row> _rows = new List<Row>(64);
        private readonly List<Mark> _marks = new List<Mark>(64);
        private GameObject _root;
        private GameObject _template;
        private Component _title;
        private TMP_InputField _search;
        private TextLocalizationComponent _cancel;
        private ScrollRect _scroll;
        private Transform _places;
        private string _labelPath, _buttonPath, _backgroundPath;
        private float _previewDelay = 0.2f;
        private Sprite _icon;
        private Color _colour = Color.white;
        private Action<int> _onPick;
        private Action _onClosed;
        private int _used;
        private int _marksUsed;
        private int _pointedRow = -1;   // the entry the pointer is on
        private float _glideAt;         // ...and when the map glides to its place
        private bool _glided;
        private int _plate = -1;        // the marker whose name is showing

        public bool IsOpen { get; private set; }

        /// <summary>True while the window's objects are there (the game's interface is rebuilt with the city).</summary>
        public bool Alive => _root != null;

        /// <summary>How many entries have been made so far (for the log).</summary>
        public int Made => _rows.Count;

        /// <summary>How many markers have been made so far (for the log).</summary>
        public int Marked => _marks.Count;

        /// <summary>The window, asleep. Null, with one line in Player.log, if the game's window is not built as expected.</summary>
        public static MapPicker Make(string name)
        {
            GameObject root = null;
            Transform holder = null;
            try
            {
                UIs uis = InstanceBehavior<UIs>.Instance;
                CityMapSubwayStations original = uis != null ? uis.cityMapSubwayStations : null;
                Transform canvas = original != null ? original.transform.parent : null;
                CityMapSubwayStationEntry entryOriginal = original != null ? original.entryTemplate : null;
                if (canvas == null || entryOriginal == null || uis.mapFilters == null)
                {
                    UiClone.Fallback("the game's list of subway stations on the map is not there to copy, so '" + name + "' is not made");
                    return null;
                }
                // Where the parts the game's entry script points at sit in its entry, read before the script goes.
                string entryPath = PathUnder(original.transform, entryOriginal.transform);
                string labelPath = entryOriginal.stationNameLabel != null ? PathUnder(entryOriginal.transform, entryOriginal.stationNameLabel.transform) : "Label";
                string buttonPath = entryOriginal.travelButton != null ? PathUnder(entryOriginal.transform, entryOriginal.travelButton.transform) : null;
                string backgroundPath = entryOriginal.background != null ? PathUnder(entryOriginal.transform, entryOriginal.background.transform) : null;
                using var piece = SessionWatch.Piece("game UI: the map's list of places being copied from the game's list of subway stations");
                holder = UiClone.Dormant(canvas, name + " Holder");
                root = UiClone.Asleep(original.gameObject, holder, name);
                UiClone.StripGameScripts(root, script => Array.IndexOf(Harmless, script.GetType().Name) >= 0);

                var picker = new MapPicker
                {
                    _root = root,
                    _previewDelay = Mathf.Clamp(original.cameraPreviewDelay, 0f, 1f),
                    _labelPath = labelPath,
                    _buttonPath = buttonPath,
                    _backgroundPath = backgroundPath
                };
                Transform header = UiClone.Need(root.transform, "Headline/Header", name);
                Transform top = UiClone.Need(root.transform, "Top Controls", name);
                Transform cancel = UiClone.Need(root.transform, "Cancel", name);
                Transform entry = !string.IsNullOrEmpty(entryPath) ? root.transform.Find(entryPath) : null;
                Transform label = entry != null && labelPath != null ? (labelPath.Length == 0 ? entry : entry.Find(labelPath)) : null;
                Button button = entry != null ? FindButton(entry, buttonPath) : null;
                picker._search = top != null ? top.GetComponentInChildren<TMP_InputField>(true) : null;
                picker._scroll = root.GetComponentInChildren<ScrollRect>(true);
                if (header == null || entry == null || label == null || button == null || cancel == null || picker._search == null || picker._scroll == null)
                {
                    UiClone.Fallback("'" + name + "': the game's list of subway stations is not built as expected");
                    UnityEngine.Object.DestroyImmediate(holder.gameObject);
                    return null;
                }

                // The entries the game made for its stations go; its template stays, as this window's.
                Transform content = entry.parent;
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
                    text.fontStyle &= ~FontStyles.UpperCase;   // the name keeps the game's capitals by a tag of its own (Entry); the line under it reads as a sentence
                    text.textWrappingMode = TextWrappingModes.NoWrap;
                    text.overflowMode = TextOverflowModes.Ellipsis;
                }
                entry.gameObject.SetActive(false);
                picker._template = entry.gameObject;

                // The game's search field, with nothing left on it of what it did there.
                TMP_InputField search = picker._search;
                search.onValueChanged = new TMP_InputField.OnChangeEvent();
                search.onEndEdit = new TMP_InputField.SubmitEvent();
                search.onSubmit = new TMP_InputField.SubmitEvent();
                search.onSelect = new TMP_InputField.SelectionEvent();
                search.onDeselect = new TMP_InputField.SelectionEvent();
                search.SetTextWithoutNotify(string.Empty);
                MapPicker owner = picker;
                search.onValueChanged.AddListener(query => UiClone.Safe(() => owner.Filter(query)));

                picker._title = header;
                UiClone.Rewire(cancel, picker.Close);
                picker._cancel = cancel.GetComponentInChildren<TextLocalizationComponent>(true);

                var group = root.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = 1f;
                    group.interactable = true;
                    group.blocksRaycasts = true;
                }
                // Beside the game's own window, on the map's canvas, where the game's stands (it keeps the game's anchors).
                root.SetActive(false);
                var rect = (RectTransform)root.transform;
                rect.SetParent(canvas, false);
                rect.SetSiblingIndex(original.transform.GetSiblingIndex() + 1);
                root.AddComponent<MapPickerWatch>().Owner = picker;

                // What the markers stand over: plain objects in the city's own scene, so that they go with it.
                var places = new GameObject(name + " Places");
                CityManager city = InstanceBehavior<CityManager>.Instance;
                Scene scene = city != null ? city.gameObject.scene : canvas.gameObject.scene;
                if (scene.IsValid())
                {
                    SceneManager.MoveGameObjectToScene(places, scene);
                }
                picker._places = places.transform;

                UnityEngine.Object.DestroyImmediate(holder.gameObject);
                return picker;
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

        // The path of a child under a parent ("Scroll View/Viewport/Content/Entry"), "" for the parent itself, null if it is not under it.
        private static string PathUnder(Transform parent, Transform child)
        {
            string path = null;
            for (Transform t = child; t != null; t = t.parent)
            {
                if (t == parent)
                {
                    return path ?? string.Empty;
                }
                path = path == null ? t.name : t.name + "/" + path;
            }
            return null;
        }

        private static Button FindButton(Transform entry, string path)
        {
            Transform at = path == null ? null : path.Length == 0 ? entry : entry.Find(path);
            Button button = at != null ? at.GetComponent<Button>() : null;
            return button != null ? button : entry.GetComponentInChildren<Button>(true);
        }

        /// <summary>Starts a filling: the headline, as final words, and the icon and colour of the markers. Entries follow, then <see cref="Open"/>.</summary>
        public void Begin(string title, Sprite icon, Color colour)
        {
            _used = 0;
            _marksUsed = 0;
            _icon = icon;
            _colour = colour;
            UiClone.Words(_title, title);
        }

        /// <summary>A line of text in the list's smaller lettering, with no button, at the top: something to read before the choices. The search field leaves it.</summary>
        public void Note(string text)
        {
            Row row = Entry(0, null, text, null, false, false, Vector3.zero, true);
            int lines = 0;
            string[] parts = (text ?? string.Empty).Split('\n');
            RectTransform window = _root != null ? (RectTransform)_root.transform : null;
            float width = window != null && window.rect.width > 1f ? window.rect.width : 775f;
            float perLine = Mathf.Max(24f, (width - 150f) / 13f);   // the smaller line is about 13 units a character
            for (int i = 0; i < parts.Length; i++)
            {
                lines += Mathf.Max(1, Mathf.CeilToInt(parts[i].Length / perLine));
            }
            Height(row, Mathf.Max(((RectTransform)_template.transform).sizeDelta.y, 34f + 30f * lines));
        }

        /// <summary>
        /// One entry: a name, a smaller line under it, the button's word (a locale key, written by the game's component),
        /// and the place it stands for on the map (none: no marker, and pointing at it moves nothing).
        /// </summary>
        public void Add(int id, string name, string detail, string buttonKey, bool enabled, bool hasPlace, Vector3 place)
        {
            Row row = Entry(id, name, detail, buttonKey, enabled, hasPlace, place, false);
            Height(row, ((RectTransform)_template.transform).sizeDelta.y);
            row.Mark = hasPlace ? Marker(place, name, _colour, _used - 1) : -1;   // (the entry just filled is the last one used)
        }

        /// <summary>The place the player is at: a marker in the game's red, named, that picks nothing.</summary>
        public void Here(Vector3 place, string name)
        {
            Color red = Color.red;
            try
            {
                red = Colors.LightRed;
            }
            catch (Exception)
            {
                red = new Color(0.93f, 0.36f, 0.36f);
            }
            Marker(place, name, red, -1);
        }

        private Row Entry(int id, string name, string detail, string buttonKey, bool enabled, bool hasPlace, Vector3 place, bool note)
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
                row = new Row { Object = made };
                row.Label = _labelPath.Length == 0 ? made.transform : made.transform.Find(_labelPath);
                row.Text = row.Label != null ? row.Label.GetComponent<TMP_Text>() : null;
                row.Button = FindButton(made.transform, _buttonPath);
                row.ButtonLabel = row.Button != null ? row.Button.GetComponentInChildren<TMP_Text>(true) : null;
                Transform background = _backgroundPath == null ? null : _backgroundPath.Length == 0 ? made.transform : made.transform.Find(_backgroundPath);
                row.Background = background != null ? background.GetComponent<Image>() : null;
                int index = _rows.Count;
                if (row.Button != null)
                {
                    row.Button.onClick = new Button.ButtonClickedEvent();
                    row.Button.onClick.AddListener(() => Picked(index));
                }
                var pointer = made.AddComponent<MapPickerEntry>();
                pointer.Owner = this;
                pointer.Index = index;
                _rows.Add(row);
            }
            _used++;
            row.Id = id;
            row.Note = note;
            row.Enabled = enabled && !note;
            row.Name = name ?? string.Empty;
            row.HasPlace = hasPlace && !note;
            row.Place = place;
            row.Mark = -1;
            UiClone.Words(row.Label, string.IsNullOrEmpty(name) ? "<size=80%><color=#C9CED3>" + (detail ?? string.Empty) + "</color></size>"
                : string.IsNullOrEmpty(detail) ? "<uppercase>" + name + "</uppercase>"
                : "<uppercase>" + name + "</uppercase>\n<size=80%><color=#C9CED3>" + detail + "</color></size>");
            if (row.Text != null)
            {
                row.Text.textWrappingMode = note ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            }
            if (!note && !ReferenceEquals(row.ButtonKey, buttonKey))
            {
                row.ButtonKey = buttonKey;
                UiClone.Key(row.ButtonLabel, buttonKey);
            }
            if (row.Button != null)
            {
                row.Button.interactable = row.Enabled;
                if (row.Button.gameObject.activeSelf == note)
                {
                    row.Button.gameObject.SetActive(!note);   // (a note has none; an entry made from a note's has it back)
                }
            }
            if (row.Background != null)
            {
                row.Background.enabled = false;   // as the game's entries: on while pointed at
            }
            if (!row.Object.activeSelf)
            {
                row.Object.SetActive(true);
            }
            return row;
        }

        private static void Height(Row row, float high)
        {
            var rect = (RectTransform)row.Object.transform;
            if (!Mathf.Approximately(rect.sizeDelta.y, high))
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, high);
            }
        }

        // One of the game's markers over a place, made when first needed and used again. -1 if the map would not take one.
        private int Marker(Vector3 place, string name, Color colour, int row)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.cityMap == null || _places == null)
            {
                return -1;
            }
            Mark mark;
            if (_marksUsed < _marks.Count)
            {
                mark = _marks[_marksUsed];
            }
            else
            {
                if (city.cityMap.poiTemplate == null)
                {
                    return -1;
                }
                var anchor = new GameObject("Place " + (_marks.Count + 1));
                anchor.transform.SetParent(_places, false);
                PointOfInterest poi = city.cityMap.AddPoi(anchor.transform, _icon, colour, string.Empty);
                if (poi == null)
                {
                    UnityEngine.Object.Destroy(anchor);
                    return -1;
                }
                mark = new Mark { Poi = poi, Anchor = anchor.transform };
                Clickable(mark, _marks.Count);
                _marks.Add(mark);
            }
            int index = _marksUsed++;
            mark.Row = row;
            mark.Anchor.position = place + Vector3.up * MarkerUp;
            mark.Poi.SetIcon(_icon, colour);
            mark.Poi.SetText(name ?? string.Empty);
            return index;
        }

        // The marker's plate shows its name without the dismiss button the game's directions have; its blob takes the pointer.
        private void Clickable(Mark mark, int index)
        {
            try
            {
                if (_blobField == null)
                {
                    _blobField = GameContract.PoiBlob.Field;   // (the game's private fields, resolved once: Core/GameContract)
                    _plateButtonField = GameContract.PoiButton.Field;
                }
                var dismiss = _plateButtonField != null ? _plateButtonField.GetValue(mark.Poi) as Button : null;
                if (dismiss != null)
                {
                    dismiss.gameObject.SetActive(false);
                }
                var blob = _blobField != null ? _blobField.GetValue(mark.Poi) as Image : null;
                if (blob == null)
                {
                    if (!_markersSaid)
                    {
                        _markersSaid = true;
                        UiClone.Fallback("the game's map marker has no shape to point at, so the markers of a choice on the map are shown but not clicked (its entries still pick)");
                    }
                    return;
                }
                blob.raycastTarget = true;
                var pointer = blob.gameObject.GetComponent<MapPickerMark>() ?? blob.gameObject.AddComponent<MapPickerMark>();
                pointer.Owner = this;
                pointer.Index = index;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// Opens the city map over <paramref name="centre"/> and shows what was filled in, or shows it again while it is
        /// open (after a filling that changed it). A click on an entry or a marker calls <paramref name="onPick"/> with its
        /// id; it stays open until <see cref="Close"/>, the game's cancel key or anything else closes the map. False if the
        /// game would not open its map now (it says why where it does).
        /// </summary>
        public bool Open(Vector3 centre, Action<int> onPick, Action onClosed)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (_root == null || city == null || city.cityMap == null || uis == null || uis.mapFilters == null)
            {
                return false;
            }
            for (int i = _used; i < _rows.Count; i++)
            {
                if (_rows[i].Object.activeSelf)
                {
                    _rows[i].Object.SetActive(false);
                }
            }
            _onPick = onPick;
            _onClosed = onClosed;
            if (!IsOpen)
            {
                if (CityMap.IsOpen && city.cityMap.isSubwayMode)
                {
                    return false;   // (the game's own choice of a station is up)
                }
                MapPlaces.Quiet = true;   // before the map opens: the mod's places are not shown for a moment
                if (!CityMap.IsOpen)
                {
                    city.cityMap.Toggle(centre);
                }
                if (!CityMap.IsOpen)
                {
                    MapPlaces.Quiet = false;
                    _onPick = null;
                    _onClosed = null;
                    return false;
                }
                IsOpen = true;
                _shown = this;
                uis.mapFilters.Toggle(false);   // as the game's choice of a station puts them away
                if (_cancel != null)
                {
                    _cancel.Key = "common_cancel";
                    _cancel.Suffix = PlayerAction.Cancel.AsSuffix();   // the player's own binding, as the game's windows write it
                }
                _search.SetTextWithoutNotify(string.Empty);
                _root.SetActive(true);
            }
            Unpoint();
            Filter(_search.text);
            for (int i = 0; i < _marks.Count; i++)
            {
                GameObject marker = _marks[i].Poi != null ? _marks[i].Poi.gameObject : null;
                if (marker != null && marker.activeSelf != i < _marksUsed)
                {
                    marker.SetActive(i < _marksUsed);
                }
                if (i < _marksUsed && _marks[i].Poi != null)
                {
                    _marks[i].Poi.SetContainerActive(false);   // the name shows while pointed at
                }
            }
            _scroll.StopMovement();
            if (_scroll.content != null)
            {
                _scroll.content.anchoredPosition = Vector2.zero;   // from the top: the content hangs from the top of the view
            }
            return true;
        }

        /// <summary>Closes the choice and the map with it, the game's own way out (as the game's Cancel does for its stations). The closed callback runs once.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (CityMap.IsOpen && city != null && city.cityMap != null)
            {
                city.cityMap.Toggle(Vector3.zero, force: true);   // (stops the map's own opening if it is still under way)
            }
            Closed();
        }

        /// <summary>The window and everything made for it, gone.</summary>
        public void Destroy()
        {
            if (IsOpen)
            {
                Closed();
            }
            _onPick = null;
            _onClosed = null;
            _rows.Clear();
            for (int i = 0; i < _marks.Count; i++)
            {
                if (_marks[i].Poi != null && !GameManager.isCitySceneBeingUnloaded)
                {
                    UnityEngine.Object.Destroy(_marks[i].Poi.gameObject);   // (the game takes it out of its list of markers)
                }
            }
            _marks.Clear();
            if (_places != null)
            {
                UnityEngine.Object.Destroy(_places.gameObject);
            }
            _places = null;
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
            }
            _root = null;
        }

        // Whoever closed it (this window, the game's cancel key, or anything else that closed the map).
        private void Closed()
        {
            if (!IsOpen)
            {
                return;
            }
            IsOpen = false;
            if (_shown == this)
            {
                _shown = null;
            }
            Unpoint();
            for (int i = 0; i < _marks.Count; i++)
            {
                if (_marks[i].Poi != null && _marks[i].Poi.gameObject.activeSelf)
                {
                    _marks[i].Poi.gameObject.SetActive(false);
                }
            }
            if (_root != null)
            {
                _root.SetActive(false);
            }
            MapPlaces.Quiet = false;
            Action closed = _onClosed;
            _onPick = null;
            _onClosed = null;
            if (closed != null)
            {
                UiClone.Safe(closed);
            }
        }

        // The search field: the entries whose name holds what is typed, as the game's list of stations narrows.
        private void Filter(string query)
        {
            string value = (query ?? string.Empty).Trim();
            bool all = value.Length == 0;
            for (int i = 0; i < _used; i++)
            {
                Row row = _rows[i];
                bool show = all || row.Note || row.Name.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0;
                if (row.Object.activeSelf != show)
                {
                    row.Object.SetActive(show);
                }
            }
        }

        private void Picked(int index)
        {
            Action<int> pick = _onPick;
            if (!IsOpen || pick == null || index < 0 || index >= _used)
            {
                return;
            }
            Row row = _rows[index];
            if (row.Note || !row.Enabled)
            {
                return;
            }
            // The clicked button is not left selected: the entries are used again, and the interface's confirm key presses whatever is selected.
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(_root.transform))
            {
                events.SetSelectedGameObject(null);
            }
            int id = row.Id;
            UiClone.Safe(() => pick(id));
        }

        // ---------------------------------------------------------------- the pointer

        internal void EntryEnter(int index)
        {
            if (!IsOpen || index < 0 || index >= _used || _rows[index].Note)
            {
                return;
            }
            Unpoint();
            Row row = _rows[index];
            if (row.Background != null)
            {
                row.Background.enabled = true;
            }
            _pointedRow = index;
            _glideAt = Time.unscaledTime + _previewDelay;
            _glided = false;
        }

        internal void EntryExit(int index)
        {
            if (index == _pointedRow)
            {
                Unpoint();
            }
        }

        internal void EntryClick(int index)
        {
            Picked(index);
        }

        internal void MarkEnter(int index)
        {
            if (IsOpen && index >= 0 && index < _marksUsed)
            {
                Plate(index);
            }
        }

        internal void MarkExit(int index)
        {
            if (index == _plate && _pointedRow < 0)
            {
                Plate(-1);
            }
        }

        internal void MarkClick(int index)
        {
            if (IsOpen && index >= 0 && index < _marksUsed && _marks[index].Row >= 0)
            {
                Picked(_marks[index].Row);
            }
        }

        private void Unpoint()
        {
            if (_pointedRow >= 0 && _pointedRow < _rows.Count && _rows[_pointedRow].Background != null)
            {
                _rows[_pointedRow].Background.enabled = false;
            }
            if (_glided)
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (city != null && city.cityMap != null && city.cityMap.cityMapCam != null)
                {
                    city.cityMap.cityMapCam.MoveCameraToTarget(Vector3.zero);   // stops the glide where it is, as the game's entries do
                }
            }
            _pointedRow = -1;
            _glided = false;
            Plate(-1);
        }

        private void Plate(int index)
        {
            if (_plate == index)
            {
                return;
            }
            if (_plate >= 0 && _plate < _marks.Count && _marks[_plate].Poi != null)
            {
                _marks[_plate].Poi.SetContainerActive(false);
            }
            _plate = index;
            if (index >= 0 && _marks[index].Poi != null)
            {
                _marks[index].Poi.SetContainerActive(true);
            }
        }

        /// <summary>Once a frame while it is open (the window's own script): the map, the glide and the name plate.</summary>
        internal void Frame()
        {
            if (!IsOpen)
            {
                return;
            }
            if (!CityMap.IsOpen)
            {
                Closed();   // the game's cancel key, or anything else that closed the map
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.mapFilters != null && uis.mapFilters.gameObject.activeSelf)
            {
                uis.mapFilters.gameObject.SetActive(false);   // (the game's key that hides and shows the interface puts them back)
            }
            if (_pointedRow >= 0 && !_glided && Time.unscaledTime >= _glideAt)
            {
                _glided = true;
                Row row = _rows[_pointedRow];
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (row.HasPlace && city != null && city.cityMap != null && city.cityMap.cityMapCam != null)
                {
                    city.cityMap.cityMapCam.MoveCameraToTarget(row.Place);
                    Plate(row.Mark);
                }
            }
            if (_plate >= 0 && _plate < _marks.Count && _marks[_plate].Poi != null)
            {
                _marks[_plate].Poi.SetContainerActive(true);   // (the game's marker puts its plate away the first time it is shown)
            }
        }

        /// <summary>The window's object went with the game's interface: what is open is put right.</summary>
        internal void Lost()
        {
            if (IsOpen)
            {
                IsOpen = false;
                if (_shown == this)
                {
                    _shown = null;
                }
                MapPlaces.Quiet = false;
            }
            _root = null;
            _onPick = null;
            _onClosed = null;
        }
    }

    /// <summary>On the window's own object, so it runs only while the window is open: <see cref="MapPicker"/> once a frame.</summary>
    public sealed class MapPickerWatch : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "MapPickerWatch", typeof(MapPickerWatch));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public MapPicker Owner;

        private static readonly int HotUpdate = HotSpots.Slot("Core: MapPickerWatch.Update");
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
            if (Owner == null)
            {
                return;
            }
            try
            {
                Owner.Frame();
            }
            catch (Exception e)
            {
                MapPicker.Logger?.Error(e);
                enabled = false;   // the entries and Cancel still work
            }
        }

        private void OnDestroy()
        {
            Owner?.Lost();
        }
    }

    /// <summary>An entry of the window: pointed at, it glides the map to its place; clicked anywhere, it picks it (CityMapSubwayStationEntry).</summary>
    public sealed class MapPickerEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public MapPicker Owner;
        public int Index;

        public void OnPointerEnter(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.EntryEnter(Index));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.EntryExit(Index));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.EntryClick(Index));
        }
    }

    /// <summary>A marker of the window's on the map: pointed at, it shows its name; clicked, it picks its entry.</summary>
    public sealed class MapPickerMark : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public MapPicker Owner;
        public int Index;

        public void OnPointerEnter(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.MarkEnter(Index));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.MarkExit(Index));
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            UiClone.Safe(() => Owner?.MarkClick(Index));
        }
    }
}
