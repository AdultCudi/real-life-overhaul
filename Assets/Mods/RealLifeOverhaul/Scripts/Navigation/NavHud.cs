using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor.LanguageChangeEvent;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI;
using UI.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The minimap and the GPS's two lines on the game's HUD. The panel is a copy of the game's own task panel (the
    /// dark box with the light headline in the left column), made asleep while the game runs: its box, its headline,
    /// its rounded mask, and the text of a task entry for the two lines. See docs/game-ui.md, section 10.
    ///
    ///   Panel            the task panel's box; a canvas of its own, so that moving the map re-batches these few
    ///                    pictures and not the whole HUD
    ///     Headline       the task panel's headline: "Map", or where the route leads
    ///     Viewport       the task panel's mask
    ///       Ground       a tint under the roads
    ///       Base         the city under the roads (NavBase: buildings and water): a square like the roads', turned
    ///                    with it, with a window of its own on its own picture; off unless the option is on
    ///       Roads        the map picture: one square as wide as the view's diagonal, turned with the player; which
    ///                    part of the picture it shows is its texture window, set when the player has moved
    ///       Spin         turned and scaled like the roads; one unit inside it is one metre
    ///         World      moved so that the player is at Spin's middle
    ///           Route    the route (NavRouteLine), in metres
    ///           Marker   the pooled markers, each at its place in metres, kept upright and the same size
    ///       Goal         the destination's marker: held at the view's edge while the place is outside it
    ///       Player       the map's own player icon on the marker's round base
    ///     Line 1, 2      the next turn, and what is left
    ///
    /// Nothing here has an Update. <see cref="Frame"/> is called by the director once a frame while the panel shows,
    /// and writes only what has changed: standing still writes nothing.
    /// </summary>
    public sealed class NavHud
    {
        public const string PanelName = "RealLife Minimap";
        public const string DormantName = "RealLife Dormant Minimap";
        public const int Pool = 32;

        // The game's HUD is laid out for 3840 x 2160 and scaled by its canvases; so is this.
        private const float Column = 740f;        // the width of the game's left column (tasks above, control hints below)
        private const float Head = 100f;          // the headline's height
        private const float Side = 12f;           // the box's own edge
        private const float ViewSide = 32f;       // the map inside it: the box and its headline are drawn 14 inside their own rectangles (a soft edge), as seen in the game; 6 more for a frame of the box round the map
        private const float Foot = 46f;           // ...and its lower edge
        private const float LineHeight = 58f;
        private const float LinesPad = 14f;
        private const float Gap = 16f;            // between this panel and the game's panels above and below it
        private const float LeastMap = 240f;      // a map lower than this is not worth showing
        private const float TasksTop = 320f;      // the task panel hangs this far below the screen's top edge
        private const float ExpandButton = 100f;  // ...and its button stays there when it is folded away
        private const float MarkerSize = 46f;
        private const float GoalSize = 58f;
        private const float RouteWidth = 7f;      // metres

        public static IModLogger Logger;

        private readonly GameObject _dormant;
        private readonly GameObject _panel;
        private readonly RectTransform _rect;
        private readonly RectTransform _head;
        private readonly RectTransform _view;
        private readonly RectTransform _roads;
        private readonly RawImage _roadsImage;
        private readonly RectTransform _base;
        private readonly RawImage _baseImage;
        private float _baseX, _baseZ, _baseWidth, _baseHeight;   // the piece of the world the base picture covers
        private bool _alone;
        private readonly RectTransform _spin;
        private readonly RectTransform _world;
        private readonly NavRouteLine _route;
        private readonly RectTransform _goal;
        private readonly Image _goalBlob, _goalIcon;
        private readonly RectTransform _playerIcon;
        private readonly Component _title;
        private readonly TMP_Text _line1, _line2;
        private readonly RectTransform _hints;        // the game's control hints (below this panel)
        private readonly RectTransform _tasks;        // the game's task panel (above it)
        private readonly TasksUI _tasksUi;
        private readonly RectTransform _screen;       // the HUD canvas
        private readonly RectTransform _labels;       // the address under the player (bottom middle)
        private readonly RectTransform _item;         // the panel of what the player holds or drives (bottom middle)
        private readonly NavGraph _graph;
        private static readonly Vector3[] Corners = new Vector3[4];

        private readonly RectTransform[] _marker = new RectTransform[Pool];
        private readonly Image[] _markerBlob = new Image[Pool];
        private readonly Image[] _markerIcon = new Image[Pool];
        private readonly int[] _markerShows = new int[Pool];
        private int _markersOn;

        private bool _shown;
        private bool _mapOn = true;
        private bool _linesOn;
        private bool _goalOn;
        private float _width, _mapHeight, _bottom = -1f, _left;
        private bool _beside;
        private float _px = float.MaxValue, _pz, _turn = float.MaxValue, _scale, _arrow = float.MaxValue;
        private float _goalX, _goalZ;
        private string _titleShown, _line1Shown, _line2Shown;
        private bool _noRoomSaid;

        /// <summary>The route's points in the map's space (x and z in metres): the director's arrays, given before <see cref="Route"/> is called.</summary>
        public float[] RouteX, RouteZ;

        public bool Alive => _panel != null && _rect != null;

        /// <summary>The view's width and height in the HUD's own units, as last laid out.</summary>
        public float ViewWidth => _width - ViewSide * 2f;
        public float ViewHeight => _mapHeight;

        /// <summary>False when there was no room for the panel between the game's own (it is then hidden).</summary>
        public bool HasRoom { get; private set; } = true;

        private NavHud(NavGraph graph, GameObject dormant, GameObject panel, UIs uis, Transform hints, Transform tasks, Transform lineLike, Color32 ground, Color32 road, Color32 routeColour)
        {
            _graph = graph;
            _dormant = dormant;
            _panel = panel;
            _rect = (RectTransform)panel.transform;
            _screen = (RectTransform)uis.playerHUD.transform;
            _tasksUi = uis.tasksUI;
            _tasks = tasks as RectTransform;
            _hints = hints as RectTransform;
            _labels = uis.playerHUD.nearestBuildingText != null ? uis.playerHUD.nearestBuildingText.transform as RectTransform : null;
            _item = uis.playerHUD.itemPanelUI != null ? uis.playerHUD.itemPanelUI.transform.Find("Panel") as RectTransform : null;

            // The copy is a picture of the game's panel and nothing more: nothing on it is clicked, scrolled or laid out by the game.
            Take<ScrollRect>(panel);
            Take<ContentSizeFitter>(panel);
            var graphics = new List<Graphic>(16);
            panel.GetComponentsInChildren(true, graphics);
            for (int i = 0; i < graphics.Count; i++)
            {
                graphics[i].raycastTarget = false;
            }
            panel.AddComponent<Canvas>();   // re-batched by itself (it inherits the HUD canvas's order and scale)

            _head = (RectTransform)panel.transform.Find("Headline");
            Take<Button>(_head.gameObject);
            Take<LayoutElement>(_head.gameObject);
            _head.anchorMin = new Vector2(0f, 1f);
            _head.anchorMax = new Vector2(1f, 1f);
            _head.pivot = new Vector2(0.5f, 1f);
            _head.offsetMin = new Vector2(Side, -Head);
            _head.offsetMax = new Vector2(-Side, 0f);
            Transform label = _head.Find("Label");
            _title = label != null ? label.GetComponent<TMP_Text>() : null;
            Transform fold = _head.Find("Icon");
            if (fold != null)
            {
                fold.gameObject.SetActive(false);   // the arrows that fold the task panel away: this panel does not fold
            }
            if (_title is TMP_Text titleText)
            {
                titleText.textWrappingMode = TextWrappingModes.NoWrap;
                titleText.overflowMode = TextOverflowModes.Ellipsis;
            }

            _view = (RectTransform)panel.transform.Find("Viewport");
            _view.anchorMin = new Vector2(0f, 1f);
            _view.anchorMax = new Vector2(1f, 1f);
            _view.pivot = new Vector2(0.5f, 1f);

            RectTransform groundRect = Child("Ground", _view);
            Stretch(groundRect);
            Image groundImage = groundRect.gameObject.AddComponent<Image>();
            groundImage.color = ground;
            groundImage.raycastTarget = false;

            _base = Child("Base", _view);
            _baseImage = _base.gameObject.AddComponent<RawImage>();
            _baseImage.raycastTarget = false;
            _baseImage.enabled = false;   // until the picture is there (and never, with its option off)

            _roads = Child("Roads", _view);
            _roadsImage = _roads.gameObject.AddComponent<RawImage>();
            _roadsImage.color = road;
            _roadsImage.raycastTarget = false;
            _roadsImage.enabled = false;   // until the picture is there

            _spin = Child("Spin", _view);
            _spin.sizeDelta = Vector2.zero;
            _world = Child("World", _spin);
            _world.sizeDelta = Vector2.zero;
            RectTransform routeRect = Child("Route", _world);
            routeRect.sizeDelta = Vector2.zero;
            _route = routeRect.gameObject.AddComponent<NavRouteLine>();
            _route.color = routeColour;
            _route.raycastTarget = false;

            Sprite blob = NavMarks.Blob;
            for (int i = 0; i < Pool; i++)
            {
                _marker[i] = Marker("Marker", _world, blob, MarkerSize, out _markerBlob[i], out _markerIcon[i]);
                _marker[i].gameObject.SetActive(false);
                _markerShows[i] = -1;
            }
            _goal = Marker("Goal", _view, blob, GoalSize, out _goalBlob, out _goalIcon);
            _goal.gameObject.SetActive(false);

            RectTransform player = Marker("Player", _view, blob, GoalSize, out Image playerBlob, out Image playerIcon);
            _playerIcon = playerIcon.rectTransform;
            if (uis.mapFilters != null)
            {
                playerBlob.color = uis.mapFilters.playerFilterColor;
                playerIcon.sprite = uis.mapFilters.playerIcon;
                playerIcon.enabled = playerIcon.sprite != null;
            }
            player.anchoredPosition = Vector2.zero;

            _line1 = Line("Line 1", lineLike, new Color32(255, 255, 255, 255));
            _line2 = Line("Line 2", lineLike, new Color32(202, 205, 206, 255));   // the grey of the task panel's group labels
            UiClone.Key(_title, "reallife_nav_title");
            _titleShown = null;
        }

        // ---------------------------------------------------------------- making it

        /// <summary>
        /// Copies the game's task panel and puts the minimap in it. Null if it cannot be done: <paramref name="later"/>
        /// then says whether to ask again (the game's HUD is not there yet) or not (it is not built as expected; one
        /// line has said what was missing).
        /// </summary>
        public static NavHud Build(NavGraph graph, out bool later)
        {
            later = true;
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null || uis.playerHUD == null || uis.tasksUI == null || uis.mapFilters == null || InstanceBehavior<GlobalReferences>.Instance == null)
            {
                return null;
            }
            later = false;
            Transform tasks = UiClone.Need(uis.tasksUI.transform, "Container/Panel", "the task panel (the minimap is a copy of it)");
            if (tasks == null || tasks.Find("Headline") == null || tasks.Find("Viewport") == null)
            {
                if (tasks != null)
                {
                    UiClone.Fallback("the task panel has no 'Headline' or no 'Viewport': the minimap is left off");
                }
                return null;
            }
            using var piece = SessionWatch.Piece("the minimap's panel");
            NavMarks.Prepare();   // the round base of the game's map markers, for the markers made here
            Transform dormant = UiClone.Dormant(uis.playerHUD.transform, DormantName);
            GameObject panel = UiClone.Asleep(tasks.gameObject, dormant, PanelName);
            if (panel == null)
            {
                UnityEngine.Object.Destroy(dormant.gameObject);
                return null;
            }
            // What is not wanted goes first (the scroll bar, and every task entry the copy brought along), then the
            // game's scripts come off what is left.
            UiClone.KeepOnly(panel.transform, "Headline", "Viewport");
            UiClone.KeepOnly(panel.transform.Find("Viewport"));
            UiClone.StripGameScripts(panel, null);
            Transform entry = uis.tasksUI.taskEntryTemplate != null ? uis.tasksUI.taskEntryTemplate.Find("Label") : null;
            Transform lineLike = entry != null ? entry : panel.transform.Find("Headline/Label");
            if (entry == null)
            {
                UiClone.Fallback("the task entry's 'Label' was not found: the GPS's lines use the headline's lettering");
            }
            Colors colours = InstanceBehavior<GlobalReferences>.Instance.colors;
            Color32 ground = colours.midnight;
            ground.a = 150;
            Color32 road = colours.lightGrey;
            road.a = 235;
            Color32 route = colours.blue;
            route.a = 255;
            Transform hints = uis.transform.Find("ControlsHintUIController/Container");
            if (hints == null)
            {
                UiClone.Fallback("the control hints were not found: the minimap stands at the bottom of the left column whether they show or not");
            }
            NavHud hud;
            try
            {
                hud = new NavHud(graph, dormant.gameObject, panel, uis, hints, tasks, lineLike, ground, road, route);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("the minimap could not be put together from the task panel and is left off");
                UnityEngine.Object.Destroy(dormant.gameObject);
                return null;
            }
            // Switched off, then out of the sleeping holder and onto the HUD, under everything else there (a dialog of
            // the game's covers it). What is left in it wakes when the panel is first shown.
            panel.SetActive(false);
            panel.transform.SetParent(uis.playerHUD.transform, false);
            panel.transform.SetAsFirstSibling();
            hud._rect.anchorMin = Vector2.zero;
            hud._rect.anchorMax = Vector2.zero;
            hud._rect.pivot = Vector2.zero;
            hud._rect.localScale = Vector3.one;
            hud._rect.localRotation = Quaternion.identity;
            UnityEngine.Object.Destroy(dormant.gameObject);
            return hud;
        }

        private static void Take<T>(GameObject from) where T : Component
        {
            T found = from.GetComponent<T>();
            if (found != null)
            {
                UnityEngine.Object.DestroyImmediate(found);
            }
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var made = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)made.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // A marker as the game's map draws one: the round base in the place's colour, the place's picture on it.
        private static RectTransform Marker(string name, Transform parent, Sprite blob, float size, out Image blobImage, out Image iconImage)
        {
            RectTransform rect = Child(name, parent);
            rect.sizeDelta = new Vector2(size, size);
            blobImage = rect.gameObject.AddComponent<Image>();
            blobImage.sprite = blob;
            blobImage.enabled = blob != null;
            blobImage.raycastTarget = false;
            RectTransform icon = Child("Icon", rect);
            icon.sizeDelta = new Vector2(size * 0.56f, size * 0.56f);
            iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            return rect;
        }

        // One of the two lines: a copy of a task entry's text, with nothing left on it but the lettering.
        private TMP_Text Line(string name, Transform like, Color32 colour)
        {
            GameObject copy = like != null ? UiClone.Asleep(like.gameObject, _dormant.transform, name) : null;
            if (copy == null)
            {
                return null;
            }
            UiClone.StripGameScripts(copy, null);
            for (int i = copy.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(copy.transform.GetChild(i).gameObject);
            }
            Take<Button>(copy);
            Take<CanvasGroup>(copy);
            Take<LayoutElement>(copy);
            Take<TextLocalizationComponent>(copy);   // the words are final when they are written; the game's key must not write over them
            var text = copy.GetComponent<TMP_Text>();
            if (text == null)
            {
                UnityEngine.Object.DestroyImmediate(copy);
                return null;
            }
            copy.transform.SetParent(_panel.transform, false);
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.localScale = Vector3.one;
            text.raycastTarget = false;
            text.color = colour;
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.Left;
            text.margin = Vector4.zero;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = true;
            text.fontSizeMax = 30f;
            text.fontSizeMin = 21f;
            text.text = string.Empty;
            copy.SetActive(false);
            return text;
        }

        public void Destroy()
        {
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
            }
            if (_dormant != null)
            {
                UnityEngine.Object.Destroy(_dormant);   // (only if the panel never left it)
            }
        }

        // ---------------------------------------------------------------- showing, placing

        public void Show(bool show)
        {
            if (_shown != show && Alive)
            {
                _shown = show;
                _panel.SetActive(show && HasRoom);
            }
        }

        /// <summary>The map picture, when it has been made.</summary>
        public void SetPicture(Texture picture)
        {
            _roadsImage.texture = picture;
            _roadsImage.enabled = picture != null && !_alone;
            _px = float.MaxValue;
        }

        /// <summary>
        /// The city under the roads (buildings and water) and the piece of the world it covers, when it has been made;
        /// null takes it away again, and the minimap is as it is without it.
        /// </summary>
        public void SetBase(Texture picture, float originX, float originZ, float worldWidth, float worldHeight)
        {
            if (_baseImage == null)
            {
                return;   // the panel's objects are destroyed with the scene before the director is
            }
            _baseX = originX;
            _baseZ = originZ;
            _baseWidth = worldWidth;
            _baseHeight = worldHeight;
            _baseImage.texture = picture;
            _baseImage.enabled = picture != null && worldWidth > 0f && worldHeight > 0f;
            _px = float.MaxValue;
        }

        /// <summary>For a test: the base picture with nothing over it but the player (no roads, route or markers), or everything again.</summary>
        public void Alone(bool alone)
        {
            if (_alone == alone || _roadsImage == null || _spin == null)
            {
                return;
            }
            _alone = alone;
            _roadsImage.enabled = !alone && _roadsImage.texture != null;
            _spin.gameObject.SetActive(!alone);
        }

        /// <summary>
        /// Puts the panel where it covers nothing of the game's, as wide as the size option says. Called on the slow
        /// tick; writes only when something has changed.
        ///
        /// First choice: the game's left column, between its control hints (bottom left) and its task panel (top
        /// left), the map as high as the room between them allows. When a long task list leaves no room there: at the
        /// bottom, beside the column, lifted over the address labels or the item panel if it would reach them (a
        /// narrow screen). Each of the game's panels is measured where it really is, so the game's interface scale
        /// and any screen shape are taken as they come.
        /// </summary>
        public void Place(int sizePercent, bool map, bool lines)
        {
            float width = Mathf.Round(Column * Mathf.Clamp(sizePercent, 50, 100) / 100f);
            float linesHeight = lines ? LineHeight * 2f + LinesPad : 0f;
            float wanted = map ? width - ViewSide * 2f : 0f;
            float frame = Head + Foot + linesHeight;
            float screenHeight = _screen.rect.height;

            // The left column.
            float bottom = Showing(_hints) ? Edge(_hints.childCount > 0 ? _hints.GetChild(0) as RectTransform : _hints, true) + Gap : 0f;
            float top = screenHeight - TasksTop - ExpandButton;
            if (_tasksUi != null && !_tasksUi.IsCollapsed && Showing(_tasks))
            {
                top = Mathf.Min(top, Edge(_tasks, false));
            }
            float room = top - Gap - bottom - frame;
            float x = 0f;
            bool column = room >= (map ? LeastMap + (_beside ? 60f : 0f) : 0f);
            if (!column)
            {
                // Beside the column, at the bottom: as high as the screen under the top bar allows.
                x = Column + Gap;
                bottom = 0f;
                if (Showing(_labels) && LeftOf(_labels) < x + width + Gap)
                {
                    bottom = Mathf.Max(bottom, Edge(_labels, true) + Gap);
                }
                if (Showing(_item) && LeftOf(_item) < x + width + Gap)
                {
                    bottom = Mathf.Max(bottom, Edge(_item, true) + Gap);
                }
                room = screenHeight - TasksTop - Gap - bottom - frame;
            }
            _beside = !column;
            float mapHeight = map ? Mathf.Min(wanted, Mathf.Floor(room)) : 0f;
            bool fits = map ? mapHeight >= LeastMap : room >= 0f;
            if (fits != HasRoom)
            {
                HasRoom = fits;
                _panel.SetActive(_shown && fits);
                if (!fits && !_noRoomSaid)
                {
                    _noRoomSaid = true;
                    Logger?.Info("Navigation: no room for the minimap beside the game's panels (" + Mathf.RoundToInt(room) + " of "
                                 + Mathf.RoundToInt(LeastMap) + " units); it is hidden until there is.");
                }
            }
            if (!fits)
            {
                return;
            }
            if (width == _width && mapHeight == _mapHeight && bottom == _bottom && x == _left && map == _mapOn && lines == _linesOn)
            {
                return;
            }
            _width = width;
            _mapHeight = mapHeight;
            _bottom = bottom;
            _left = x;
            _mapOn = map;
            _linesOn = lines;
            _rect.anchoredPosition = new Vector2(x, bottom);
            _rect.sizeDelta = new Vector2(width, Head + mapHeight + linesHeight + Foot);
            _view.gameObject.SetActive(map);
            _view.offsetMin = new Vector2(ViewSide, -Head - mapHeight);
            _view.offsetMax = new Vector2(-ViewSide, -Head);
            float diagonal = Mathf.Ceil(Mathf.Sqrt(ViewWidth * ViewWidth + mapHeight * mapHeight)) + 2f;
            _roads.sizeDelta = new Vector2(diagonal, diagonal);
            _base.sizeDelta = _roads.sizeDelta;
            PlaceLine(_line1, Foot + LineHeight + 4f, lines);
            PlaceLine(_line2, Foot + 4f, lines);
            _px = float.MaxValue;   // the view is another size: everything in it is written again
            _scale = 0f;
        }

        /// <summary>Where the panel stands, for the log.</summary>
        public string Where => _beside ? "beside the left column" : "in the left column";

        private static bool Showing(RectTransform rect)
        {
            return rect != null && rect.gameObject.activeInHierarchy;
        }

        // The upper or lower edge of one of the game's panels, in the HUD's units from the bottom of the screen.
        private float Edge(RectTransform rect, bool upper)
        {
            rect.GetWorldCorners(Corners);   // bottom left, top left, top right, bottom right
            return _screen.InverseTransformPoint(Corners[upper ? 1 : 0]).y - _screen.rect.yMin;
        }

        // Its left edge, from the left of the screen.
        private float LeftOf(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            return _screen.InverseTransformPoint(Corners[0]).x - _screen.rect.xMin;
        }

        private static void PlaceLine(TMP_Text line, float fromBottom, bool show)
        {
            if (line == null)
            {
                return;
            }
            RectTransform rect = line.rectTransform;
            rect.offsetMin = new Vector2(Side + 28f, fromBottom);
            rect.offsetMax = new Vector2(-Side - 28f, fromBottom + LineHeight);
            if (line.gameObject.activeSelf != show)
            {
                line.gameObject.SetActive(show);
            }
        }

        // ---------------------------------------------------------------- words

        /// <summary>The headline: where the route leads, or null for the panel's own name.</summary>
        public void Title(string place)
        {
            if (_title == null || place == _titleShown)
            {
                return;
            }
            _titleShown = place;
            if (place == null)
            {
                UiClone.Key(_title, "reallife_nav_title");
            }
            else
            {
                UiClone.Words(_title, place);
            }
        }

        /// <summary>The two lines. A line is written only when its words have changed.</summary>
        public void Lines(string first, string second)
        {
            if (_line1 != null && first != _line1Shown)
            {
                _line1Shown = first;
                _line1.text = first ?? string.Empty;
            }
            if (_line2 != null && second != _line2Shown)
            {
                _line2Shown = second;
                _line2.text = second ?? string.Empty;
            }
        }

        // ---------------------------------------------------------------- the route, the goal, the markers

        /// <summary>The route has changed: <see cref="RouteX"/> and <see cref="RouteZ"/> hold this many points.</summary>
        public void Route(int count)
        {
            if (count < 2)
            {
                _route.Clear();
                return;
            }
            _route.Set(RouteX, RouteZ, count, RouteWidth, 0.7f);
        }

        /// <summary>The destination's marker (held at the view's edge while the place is outside it), or none.</summary>
        public void Goal(bool on, float x, float z, Sprite icon, Color32 colour)
        {
            if (_goal == null || _goalBlob == null || _goalIcon == null)
            {
                return;   // the panel's objects are destroyed with the scene before the director is (the game quitting)
            }
            _goalX = x;
            _goalZ = z;
            if (on)
            {
                colour.a = 255;
                _goalBlob.color = colour;
                _goalIcon.sprite = icon;
                _goalIcon.enabled = icon != null;
            }
            if (_goalOn != on)
            {
                _goalOn = on;
                _goal.gameObject.SetActive(on);
            }
            _px = float.MaxValue;
        }

        /// <summary>
        /// Shows the marks that are in view, at most <see cref="Pool"/> of them, in the pooled markers. Called on the
        /// slow tick. A marker that already shows its mark is not written again.
        /// </summary>
        public void Marks(List<NavMark> marks, float px, float pz, float metresAcross)
        {
            float reach = metresAcross * 0.75f;   // the view's half diagonal and a little: what a turned view can show
            int used = 0;
            for (int i = 0; i < marks.Count && used < Pool; i++)
            {
                NavMark mark = marks[i];
                float dx = mark.X - px;
                float dz = mark.Z - pz;
                if (dx > reach || dx < -reach || dz > reach || dz < -reach)
                {
                    continue;
                }
                RectTransform marker = _marker[used];
                if (_markerShows[used] != i || marker.anchoredPosition.x != mark.X || marker.anchoredPosition.y != mark.Z)
                {
                    _markerShows[used] = i;
                    marker.anchoredPosition = new Vector2(mark.X, mark.Z);
                    _markerBlob[used].color = mark.Colour;
                    Image icon = _markerIcon[used];
                    icon.sprite = mark.Icon;
                    icon.enabled = mark.Icon != null;
                    // Without the round base the picture carries the colour itself.
                    icon.color = _markerBlob[used].enabled ? new Color32(255, 255, 255, 255) : mark.Colour;
                }
                if (used >= _markersOn)
                {
                    marker.gameObject.SetActive(true);
                    Upright(marker);
                }
                used++;
            }
            for (int i = used; i < _markersOn; i++)
            {
                _marker[i].gameObject.SetActive(false);
                _markerShows[i] = -1;
            }
            _markersOn = used;
        }

        /// <summary>The marks were read again: what each marker shows is no longer known.</summary>
        public void MarksChanged()
        {
            for (int i = 0; i < Pool; i++)
            {
                _markerShows[i] = -1;
            }
        }

        private void Upright(RectTransform marker)
        {
            float size = _scale > 0f ? 1f / _scale : 1f;
            marker.localScale = new Vector3(size, size, 1f);
            marker.localEulerAngles = new Vector3(0f, 0f, _turn == float.MaxValue ? 0f : -_turn);
        }

        // ---------------------------------------------------------------- every frame

        /// <summary>
        /// Moves the view to the player. <paramref name="heading"/> is the way the player (or the car) faces and
        /// <paramref name="view"/> the way the camera looks, both degrees clockwise from north. With
        /// <paramref name="turnWithView"/> the map turns with the camera, so that what is ahead on the screen is up on the
        /// map, and the arrow shows the player's own way against it; otherwise north is up and the arrow turns.
        /// <paramref name="metresAcross"/> is how much of the city the view's width shows. Writes nothing when the
        /// player has not moved or turned and the view is as wide as it was.
        /// </summary>
        public void Frame(float px, float pz, float heading, float view, float metresAcross, bool turnWithView)
        {
            if (!_mapOn || _mapHeight <= 0f)
            {
                return;
            }
            float scale = ViewWidth / metresAcross;
            float turn = turnWithView ? view : 0f;
            bool scaled = Mathf.Abs(scale - _scale) > _scale * 0.0005f;
            bool turned = Mathf.Abs(Mathf.DeltaAngle(turn, _turn == float.MaxValue ? turn + 180f : _turn)) > 0.1f;
            bool moved = Mathf.Abs(px - _px) > 0.02f || Mathf.Abs(pz - _pz) > 0.02f;
            float arrow = -Mathf.DeltaAngle(turn, heading);   // the player's way on the map as it is turned
            if (Mathf.Abs(Mathf.DeltaAngle(arrow, _arrow == float.MaxValue ? arrow + 180f : _arrow)) > 0.5f)
            {
                _arrow = arrow;
                _playerIcon.localEulerAngles = new Vector3(0f, 0f, arrow + 45f);   // the game's player icon points up at 45 degrees (PlayerController.UpdateCityMapPlayerIconRotation)
            }
            if (!scaled && !turned && !moved)
            {
                return;
            }
            _px = px;
            _pz = pz;
            if (turned)
            {
                _turn = turn;
                var angles = new Vector3(0f, 0f, turn);
                _roads.localEulerAngles = angles;
                _base.localEulerAngles = angles;
                _spin.localEulerAngles = angles;
            }
            if (scaled)
            {
                _scale = scale;
                _spin.localScale = new Vector3(scale, scale, 1f);
            }
            if (scaled || turned)
            {
                for (int i = 0; i < _markersOn; i++)
                {
                    Upright(_marker[i]);
                }
            }
            _world.anchoredPosition = new Vector2(-px, -pz);
            if (_roadsImage.enabled)
            {
                NavView.Window(px, pz, _graph.OriginX, _graph.OriginZ, _graph.WorldWidth, _graph.WorldHeight, _roads.sizeDelta.x / _scale,
                               out float u, out float v, out float w, out float h);
                _roadsImage.uvRect = new Rect(u, v, w, h);
            }
            if (_baseImage.enabled)
            {
                NavView.Window(px, pz, _baseX, _baseZ, _baseWidth, _baseHeight, _base.sizeDelta.x / _scale, out float u, out float v, out float w, out float h);
                _baseImage.uvRect = new Rect(u, v, w, h);
            }
            if (_goalOn)
            {
                NavView.ToView(_goalX, _goalZ, px, pz, _turn, _scale, out float gx, out float gy);
                float halfWidth = ViewWidth * 0.5f - GoalSize * 0.45f;
                float halfHeight = _mapHeight * 0.5f - GoalSize * 0.45f;
                // Outside the view: on the edge, in the direction of the place.
                float over = Mathf.Max(Mathf.Abs(gx) / halfWidth, Mathf.Abs(gy) / halfHeight);
                if (over > 1f)
                {
                    gx /= over;
                    gy /= over;
                }
                _goal.anchoredPosition = new Vector2(gx, gy);
            }
        }
    }
}
