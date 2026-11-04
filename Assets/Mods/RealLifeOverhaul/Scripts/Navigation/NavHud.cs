using System;
using System.Collections.Generic;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using TMPro;
using UI.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The minimap and the GPS's two lines in a copy of the game's task panel (docs/game-ui.md, section 10): under its
    /// mask the base and road pictures, squares as wide as the view's diagonal showing a window of their textures, and
    /// a turned layer, a unit a metre, with the route and the pooled markers. No Update: the director calls <see
    /// cref="Frame"/> once a frame while it shows, and it writes only what changed.
    /// </summary>
    public sealed partial class NavHud
    {
        public const string PanelName = "RealLife Minimap";
        public const string DormantName = "RealLife Dormant Minimap";
        public const int Pool = 32;

        // In the game HUD's units: it is laid out for 3840 x 2160 and scaled by its canvases.
        private const float Head = NavPlaceRules.Head;          // the headline's height
        private const float Side = 12f;                         // the box's own edge
        private const float ViewSide = NavPlaceRules.ViewSide;
        private const float Foot = NavPlaceRules.Foot;
        private const float LineHeight = NavPlaceRules.LineHeight;
        private const float LinesPad = NavPlaceRules.LinesPad;
        private const float MarkerSize = 46f;
        private const float GoalSize = 58f;
        private const float RouteWidth = 7f;      // metres

        public static IModLogger Logger;

        private readonly GameObject _dormant;
        private readonly GameObject _panel;
        private readonly RectTransform _rect;
        private readonly Canvas _canvas;           // the panel's own, which the kit's HUD order is set on (Sort)
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

        /// <summary>The route's points in metres: the director's arrays, given before <see cref="Route"/> is called.</summary>
        public float[] RouteX, RouteZ;

        public bool Alive => _panel != null && _rect != null;

        private static NavHud _live;   // the one built last; the game's HUD holds one

        /// <summary>
        /// The minimap's card on the screen while it shows (its map and its route lines), so a panel docked at the bottom left
        /// (the kit's item panel) can stand clear of it; null while it is hidden or gone.
        /// </summary>
        public static RectTransform Card => _live != null && _live._panel != null && _live._panel.activeInHierarchy ? _live._rect : null;

        /// <summary>The view's width and height in the HUD's own units, as last laid out.</summary>
        public float ViewWidth => _width - ViewSide * 2f;
        public float ViewHeight => _mapHeight;

        /// <summary>False, the panel hidden, when there was no room for it between the game's own.</summary>
        public bool HasRoom { get; private set; } = true;

        public void Destroy()
        {
            ReleaseScene();   // the 2.5D picture's texture is the mod's own: it goes with the panel
            if (_live == this)
            {
                _live = null;
            }
            if (_panel != null)
            {
                UnityEngine.Object.Destroy(_panel);
            }
            if (_dormant != null)
            {
                UnityEngine.Object.Destroy(_dormant);   // only if the panel never left it
            }
        }

        public void Show(bool show)
        {
            if (_shown != show && Alive)
            {
                _shown = show;
                _panel.SetActive(show && HasRoom);
            }
        }

        public void SetPicture(Texture picture)
        {
            _roadsImage.texture = picture;
            _roadsImage.enabled = picture != null && !_alone;
            _px = float.MaxValue;
        }

        /// <summary>The base picture and the piece of the world it covers; null takes it away.</summary>
        public void SetBase(Texture picture, float originX, float originZ, float worldWidth, float worldHeight)
        {
            if (_baseImage == null)
            {
                return;   // the panel's objects go with the scene before the director does
            }
            _baseX = originX;
            _baseZ = originZ;
            _baseWidth = worldWidth;
            _baseHeight = worldHeight;
            _baseImage.texture = picture;
            _baseImage.enabled = picture != null && worldWidth > 0f && worldHeight > 0f;
            _px = float.MaxValue;
        }

        /// <summary>For a test: the base picture with only the player over it, or everything again.</summary>
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
        /// Puts the panel where it covers nothing: clear of the game's panels, and of the kit's bottom bar and left column while
        /// the kit's HUD is up (NavPlaceRules), in the left column or beside it. On the slow tick, writing only changes; it also
        /// keeps the panel's canvas just over the kit HUD's (<see cref="Sort"/>).
        /// </summary>
        public void Place(int sizePercent, bool map, bool lines)
        {
            Func<NavKitSpace> kit = NavBridges.KitHud;
            NavKitSpace up = kit != null ? kit() : default;
            Sort(up);
            float width = NavPlaceRules.Width(sizePercent);
            float linesHeight = lines ? LineHeight * 2f + LinesPad : 0f;
            float wanted = NavPlaceRules.Wanted(width, map);
            float frame = NavPlaceRules.Frame(lines);

            NavSpace space = NavSpace.Free(_screen.rect.height);
            if (Showing(_hints))
            {
                space.HintsTop = Edge(_hints.childCount > 0 ? _hints.GetChild(0) as RectTransform : _hints, true);
            }
            if (_tasksUi != null && !_tasksUi.IsCollapsed && Showing(_tasks))
            {
                space.TasksBottom = Edge(_tasks, false);
            }
            if (Showing(_labels))
            {
                space.LabelsLeft = LeftOf(_labels);
                space.LabelsTop = Edge(_labels, true);
            }
            if (Showing(_item))
            {
                space.ItemLeft = LeftOf(_item);
                space.ItemTop = Edge(_item, true);
            }
            if (up.Up)
            {
                space.BarTop = up.BarTop;
                space.ColumnRight = up.ColumnRight;
                space.HasColumn = up.HasColumn;
                space.ColumnBottom = up.ColumnBottom;
                space.ChipsLeft = up.ChipsLeft;
                space.ChipsTop = up.ChipsTop;
            }
            bool fits = NavPlaceRules.Place(space, width, wanted, frame, _beside, out float x, out float bottom, out float mapHeight, out bool column);
            _beside = !column;
            float room = mapHeight;
            if (fits != HasRoom)
            {
                HasRoom = fits;
                _panel.SetActive(_shown && fits);
                if (!fits && !_noRoomSaid)
                {
                    _noRoomSaid = true;
                    Logger?.Info("Navigation: no room for the minimap beside the game's panels (" + Mathf.RoundToInt(room) + " of "
                                 + Mathf.RoundToInt(NavPlaceRules.LeastMap) + " units); it is hidden until there is.");
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
            _px = float.MaxValue;   // another size: everything in the view is written again
            _scale = 0f;
        }

        /// <summary>
        /// The panel's own canvas, over the kit HUD's and under every window: the kit draws its HUD (the bottom bar, the pieces,
        /// the prompts) on a canvas of its own over the game's, so the panel, a child of the game's HUD, was drawn under all of
        /// it. While the kit's HUD is up the panel overrides its sorting at the order the kit names for it (one over the HUD's);
        /// without it, the panel is sorted with the game's HUD as before. Two comparisons on the slow tick.
        /// </summary>
        private void Sort(NavKitSpace up)
        {
            if (_canvas.overrideSorting != up.Up)
            {
                _canvas.overrideSorting = up.Up;
            }
            if (up.Up && _canvas.sortingOrder != up.Order)
            {
                _canvas.sortingOrder = up.Order;
            }
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

        /// <summary>The two lines, each written only when its words change.</summary>
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

        /// <summary>The route has changed: <see cref="RouteX"/> and <see cref="RouteZ"/> hold this many points.</summary>
        public void Route(int count)
        {
            if (_route == null)
            {
                return;   // the panel's objects go with the scene before the director does, as the game quits
            }
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
                return;   // the panel's objects go with the scene before the director does, as the game quits
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

        /// <summary>Shows up to <see cref="Pool"/> marks in view, on the slow tick; a marker already showing its mark is not written again.</summary>
        public void Marks(List<NavMark> marks, float px, float pz, float metresAcross)
        {
            float reach = metresAcross * 0.75f;   // a little over the view's half diagonal: what a turned view can show
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
        /// <summary>Moves the view to the player once a frame, turned with the camera if <paramref name="turnWithView"/>; writes only changes.</summary>
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
                _playerIcon.localEulerAngles = new Vector3(0f, 0f, arrow + 45f);   // the game's player icon points up at 45 degrees
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
                // outside the view: on the edge, towards the place
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
