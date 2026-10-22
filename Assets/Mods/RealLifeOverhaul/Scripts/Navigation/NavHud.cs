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
    /// The minimap and the GPS's two lines on the game's HUD, in a copy of the game's own task panel (docs/game-ui.md,
    /// section 10). Inside its mask: a ground tint, the base picture and the road picture, each a square as wide as the
    /// view's diagonal showing a window of its texture, and a turned and scaled layer, one unit a metre, holding the route
    /// and the pooled markers; over them the destination's marker and the player's icon.
    ///
    /// No Update of its own: the director calls <see cref="Frame"/> once a frame while the panel shows, and it writes only
    /// what changed, so standing still writes nothing. The panel has a canvas of its own, so moving the map re-batches
    /// only these few pictures.
    /// </summary>
    public sealed partial class NavHud
    {
        public const string PanelName = "RealLife Minimap";
        public const string DormantName = "RealLife Dormant Minimap";
        public const int Pool = 32;

        // In the game HUD's units: it is laid out for 3840 x 2160 and scaled by its canvases.
        private const float Column = 740f;        // the width of the game's left column (tasks above, control hints below)
        private const float Head = 100f;          // the headline's height
        private const float Side = 12f;           // the box's own edge
        private const float ViewSide = 32f;       // the map's inset: the box is drawn 14 inside its rectangle, and 6 more frame the map
        private const float Foot = 46f;           // the map's inset from the lower edge
        private const float LineHeight = 58f;
        private const float LinesPad = 14f;
        private const float Gap = 16f;            // between this panel and the game's panels above and below it
        private const float LeastMap = 240f;      // a map lower than this is not worth showing
        private const float TasksTop = 320f;      // the task panel hangs this far below the screen's top edge
        private const float ExpandButton = 100f;  // its button stays there when it is folded away
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

        /// <summary>The route's points in metres: the director's arrays, given before <see cref="Route"/> is called.</summary>
        public float[] RouteX, RouteZ;

        public bool Alive => _panel != null && _rect != null;

        /// <summary>The view's width and height in the HUD's own units, as last laid out.</summary>
        public float ViewWidth => _width - ViewSide * 2f;
        public float ViewHeight => _mapHeight;

        /// <summary>False, the panel hidden, when there was no room for it between the game's own.</summary>
        public bool HasRoom { get; private set; } = true;

        public void Destroy()
        {
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
        /// Puts the panel where it covers nothing of the game's: in the left column between the control hints and the task
        /// panel, or, with no room there, at the bottom beside it, above the address and item panels. Each panel is measured
        /// where it is, so any interface scale and screen shape work. On the slow tick; writes only what changed.
        /// </summary>
        public void Place(int sizePercent, bool map, bool lines)
        {
            float width = Mathf.Round(Column * Mathf.Clamp(sizePercent, 50, 100) / 100f);
            float linesHeight = lines ? LineHeight * 2f + LinesPad : 0f;
            float wanted = map ? width - ViewSide * 2f : 0f;
            float frame = Head + Foot + linesHeight;
            float screenHeight = _screen.rect.height;

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
            _px = float.MaxValue;   // another size: everything in the view is written again
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

        // A panel's left edge, from the left of the screen.
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

        /// <summary>
        /// Shows up to <see cref="Pool"/> marks in view in the pooled markers, on the slow tick; a marker already showing its
        /// mark is not written again.
        /// </summary>
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
        /// <summary>
        /// Moves the view to the player, once a frame. <paramref name="heading"/> is the way the player faces and
        /// <paramref name="view"/> the way the camera looks, degrees clockwise from north; with <paramref name="turnWithView"/>
        /// the map turns so what is ahead on the screen is up, else north is up. Writes nothing when nothing changed.
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
