using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using BigAmbitions.InputSystem;
using Localizor.LanguageChangeEvent;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A choice of places on the open city map, in a copy of the game's subway station window, with one of the game's
    /// markers over each place; pointing glides the map there, a click picks. The map pauses the game, so it runs on the
    /// interface's clock. Costs nothing while closed; while open, one look a frame whether the map is still open.
    /// </summary>
    public sealed partial class MapPicker
    {
        public static IModLogger Logger;

        private const float MarkerUp = 4.5f;   // metres over the place, as the mod's other markers stand
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

        /// <summary>The map and the markers only, the window itself kept hidden: for an owner with a window of its own (build mode's).</summary>
        public bool Windowless { get; set; }

        /// <summary>True while the window's objects are there: the game's interface is rebuilt with the city.</summary>
        public bool Alive => _root != null;

        /// <summary>Entries made so far, for the log.</summary>
        public int Made => _rows.Count;

        /// <summary>Markers made so far, for the log.</summary>
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
                // Read before the entry's script goes: where the parts it points at sit.
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

                UiClone.EntryTemplate(entry, label);
                picker._template = entry.gameObject;

                // The game's search field, cleared of what it did there.
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

                UiClone.Visible(root.GetComponent<CanvasGroup>());
                // On the map's canvas just after the game's window, with the game's anchors.
                root.SetActive(false);
                var rect = (RectTransform)root.transform;
                rect.SetParent(canvas, false);
                rect.SetSiblingIndex(original.transform.GetSiblingIndex() + 1);
                root.AddComponent<MapPickerWatch>().Owner = picker;

                // The markers' anchors: plain objects in the city's own scene, so that they go with it.
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

        // "Scroll View/Viewport/Content/Entry"; "" for the parent itself, null if it is not under it.
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

        /// <summary>Starts a filling: the headline and the markers' icon and colour. Entries follow, then <see cref="Open"/>.</summary>
        public void Begin(string title, Sprite icon, Color colour)
        {
            _used = 0;
            _marksUsed = 0;
            _icon = icon;
            _colour = colour;
            UiClone.Words(_title, title);
        }

        /// <summary>A line of text at the top in the smaller lettering, with no button; the search leaves it.</summary>
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

        /// <summary>One entry: a name, a smaller line, the button's locale key and its place on the map (none: no marker).</summary>
        public void Add(int id, string name, string detail, string buttonKey, bool enabled, bool hasPlace, Vector3 place)
        {
            Row row = Entry(id, name, detail, buttonKey, enabled, hasPlace, place, false);
            Height(row, ((RectTransform)_template.transform).sizeDelta.y);
            row.Mark = hasPlace ? Marker(place, name, _colour, _used - 1) : -1;   // the entry just filled is the last one used
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
                    row.Button.onClick.AddListener(Guard.Listener(() => Picked(index)));
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
                    row.Button.gameObject.SetActive(!note);   // a note has none; an entry reused from a note's has it back
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

        // Made when first needed and reused; -1 if the map would not take one.
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

        // The plate shows the name without the dismiss button of the game's directions; the blob takes the pointer.
        private void Clickable(Mark mark, int index)
        {
            try
            {
                if (_blobField == null)
                {
                    _blobField = GameContract.PoiBlob.Field;   // the game's private fields, resolved once
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

        /// <summary>Opens the map over <paramref name="centre"/> with what was filled in; a click calls <paramref name="onPick"/>. False if the game would not open it.</summary>
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
                    return false;   // the game's own choice of a station is up
                }
                MapPlaces.Quiet = true;   // before the map opens, so they never show for a moment
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
                _root.SetActive(true);   // its watch runs on it, so the map shut by any means closes it
                CanvasGroup group = _root.GetComponent<CanvasGroup>() ?? _root.AddComponent<CanvasGroup>();
                group.alpha = Windowless ? 0f : 1f;   // windowless: the map and the markers, nothing of the window seen or clicked
                group.blocksRaycasts = !Windowless;
                group.interactable = !Windowless;
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
                _scroll.content.anchoredPosition = Vector2.zero;   // the content hangs from the top of the view
            }
            return true;
        }

        /// <summary>Closes the choice and the map, the game's own way out. The closed callback runs once.</summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (CityMap.IsOpen && city != null && city.cityMap != null)
            {
                city.cityMap.Toggle(Vector3.zero, force: true);   // also stops the map's opening if it is still under way
            }
            Closed();
        }

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
                if (!ReferenceEquals(_marks[i].Poi, null) && !GameManager.isCitySceneBeingUnloaded)
                {
                    MapPlaces.Drop(_marks[i].Poi);   // out of the game's list of markers first, then destroyed
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

        // However it closed: this window, the cancel key, or anything else that closed the map.
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

        // The entries whose name holds what is typed, as the game's list narrows.
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
            UiClone.Deselect(_root.transform);
            int id = row.Id;
            UiClone.Safe(() => pick(id));
        }
    }
}
