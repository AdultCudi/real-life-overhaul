using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The dispatcher's planning board: a small map with the depot in the middle, the parcels of
    /// the batch as numbered squares, and each van's round drawn as lines in its colour. Its own
    /// retained canvas, built the first time a dispatcher shift starts and switched off when none
    /// is on. Nothing here runs by itself: <see cref="DispatchJob"/> calls <see cref="Draw"/> when
    /// an assignment changes, and that is the only time anything is laid out or lettered.
    /// </summary>
    public sealed class DispatchBoard : MonoBehaviour
    {
        public const int Parcels = 6;
        public const int Vans = 3;
        private const int Segments = Parcels + Vans;      // every parcel is one leg out, and each van has one leg home
        private const float MapHalf = 180f;               // pixels from the depot to the edge of the plan (map units run -1 to 1)

        private static readonly Color Plate = new Color(0.05f, 0.07f, 0.10f, 0.90f);
        private static readonly Color Paper = new Color(0.10f, 0.13f, 0.17f, 0.95f);
        private static readonly Color Idle = new Color(0.62f, 0.66f, 0.72f, 1f);
        private static readonly Color Ink = new Color(0.03f, 0.05f, 0.09f, 1f);
        public static readonly Color[] VanColours =
        {
            new Color(0.25f, 0.62f, 0.95f, 1f), new Color(0.98f, 0.62f, 0.15f, 1f), new Color(0.35f, 0.80f, 0.45f, 1f)
        };

        private static DispatchBoard _instance;

        private GameObject _panel;
        private RectTransform _map;
        private RectTransform _ring;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _foot;
        private readonly RectTransform[] _dots = new RectTransform[Parcels];
        private readonly Image[] _dotImages = new Image[Parcels];
        private readonly RectTransform[] _lines = new RectTransform[Segments];
        private readonly Image[] _lineImages = new Image[Segments];
        private TMP_FontAsset _font;
        private bool _on;

        public static DispatchBoard Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Dispatch Board").AddComponent<DispatchBoard>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -9;   // under the game's own menus
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _font = Hud.Font;

            // Centre left: clear of the objective at the top, the choices at the bottom and the phone on the right.
            RectTransform panel = Box("Board", transform, new Vector2(0f, 0.5f), new Vector2(300f, 30f), new Vector2(440f, 500f), Plate);
            _title = Label("Title", panel, 22f, Color.white);
            Place(_title.rectTransform, new Vector2(0f, 222f), new Vector2(420f, 30f));
            _map = Box("Map", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(MapHalf * 2f + 40f, MapHalf * 2f + 40f), Paper);
            for (int i = 0; i < Segments; i++)
            {
                _lines[i] = Box("Leg", _map, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 4f), Idle);
                _lineImages[i] = _lines[i].GetComponent<Image>();
                _lines[i].gameObject.SetActive(false);
            }
            RectTransform depot = Box("Depot", _map, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f), Color.white);
            depot.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _ring = Box("Current", _map, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f), Color.white);
            for (int i = 0; i < Parcels; i++)
            {
                _dots[i] = Box("Parcel", _map, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f), Idle);
                _dotImages[i] = _dots[i].GetComponent<Image>();
                TextMeshProUGUI number = Label("Number", _dots[i], 22f, Ink);
                Fill(number.rectTransform);
                number.SetText("{0}", i + 1);
            }
            _foot = Label("Foot", panel, 19f, new Color(0.80f, 0.85f, 0.92f));
            Place(_foot.rectTransform, new Vector2(0f, -222f), new Vector2(424f, 28f));
            _panel = panel.gameObject;
            _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void Hide()
        {
            if (_on)
            {
                _on = false;
                _panel.SetActive(false);
            }
        }

        /// <summary>
        /// Lays the plan out. Called when a parcel is assigned or taken back, never on a timer.
        /// </summary>
        /// <param name="where">The parcels, in map units from the depot (-1 to 1).</param>
        /// <param name="van">The van each parcel is on, or -1.</param>
        /// <param name="rounds">For each van, its parcels in the order it drives them; -1 ends the list.</param>
        /// <param name="current">The parcel being decided, or -1.</param>
        public void Draw(string title, string foot, Vector2[] where, int[] van, int[,] rounds, int current)
        {
            if (!_on)
            {
                _on = true;
                _panel.SetActive(true);
            }
            _title.SetText(title ?? string.Empty);
            _foot.SetText(foot ?? string.Empty);
            for (int i = 0; i < Parcels; i++)
            {
                _dots[i].anchoredPosition = where[i] * MapHalf;
                _dotImages[i].color = van[i] >= 0 ? VanColours[van[i]] : Idle;
            }
            _ring.gameObject.SetActive(current >= 0);
            if (current >= 0)
            {
                _ring.anchoredPosition = where[current] * MapHalf;
            }
            int used = 0;
            for (int v = 0; v < Vans; v++)
            {
                Vector2 from = Vector2.zero;
                bool any = false;
                for (int s = 0; s < Parcels && rounds[v, s] >= 0; s++)
                {
                    Vector2 to = where[rounds[v, s]] * MapHalf;
                    Leg(used++, from, to, VanColours[v]);
                    from = to;
                    any = true;
                }
                if (any)
                {
                    Leg(used++, from, Vector2.zero, VanColours[v]);
                }
            }
            for (int i = used; i < Segments; i++)
            {
                _lines[i].gameObject.SetActive(false);
            }
        }

        private void Leg(int index, Vector2 from, Vector2 to, Color colour)
        {
            if (index >= Segments)
            {
                return;
            }
            RectTransform line = _lines[index];
            Vector2 along = to - from;
            line.gameObject.SetActive(true);
            line.anchoredPosition = (from + to) * 0.5f;
            line.sizeDelta = new Vector2(along.magnitude, 4f);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            colour.a = 0.85f;
            _lineImages[index].color = colour;
        }

        private static RectTransform Box(string objectName, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private TextMeshProUGUI Label(string objectName, Transform parent, float size, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.overflowMode = TextOverflowModes.Ellipsis;
#pragma warning disable CS0618   // the SDK's TextMeshPro is older than the game's: this property is the one both have
            text.enableWordWrapping = false;
#pragma warning restore CS0618
            if (_font != null)
            {
                text.font = _font;
            }
            return text;
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
