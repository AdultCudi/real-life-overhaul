using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The needs strip: three short bars (hygiene, stress, home) and a line for an illness, at
    /// the left edge of the screen. Its own retained canvas, built the first time it is wanted.
    /// Shown only when it has something to say: at home, while a need is in its warning zone,
    /// or while ill. The director calls <see cref="Show"/> once a second; a bar or a text is
    /// touched only when the whole number it shows has changed.
    /// </summary>
    public sealed class HomeHud : MonoBehaviour
    {
        private const float BarWidth = 96f;
        private static readonly Color Plate = new Color(0.05f, 0.07f, 0.10f, 0.80f);
        private static readonly Color Track = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color Good = new Color(0.36f, 0.78f, 0.42f);
        private static readonly Color Fair = new Color(0.95f, 0.76f, 0.22f);
        private static readonly Color Bad = new Color(0.94f, 0.36f, 0.30f);
        private static readonly Color Soft = new Color(0.80f, 0.85f, 0.92f);

        private static HomeHud _instance;

        private GameObject _panel;
        private RectTransform _panelRect;
        private readonly RectTransform[] _fill = new RectTransform[3];
        private readonly Image[] _fillImage = new Image[3];
        private readonly int[] _shown = { -1, -1, -1 };
        private readonly int[] _state = { -1, -1, -1 };
        private TextMeshProUGUI _note;
        private string _noteShown;
        private bool _on;
        private bool _noteOn;

        public static HomeHud Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Home HUD").AddComponent<HomeHud>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -11;   // under the game's own menus and the mod's other panels
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // Left edge, a little above the middle: clear of the portrait (top left) and the game's bottom bar.
            _panelRect = Box("Needs", transform, new Vector2(0f, 0.5f), new Vector2(110f, 150f), new Vector2(204f, 78f), Plate);
            string[] labels = { "reallife_home_strip_hygiene", "reallife_home_strip_stress", "reallife_home_strip_home" };
            for (int i = 0; i < 3; i++)
            {
                float y = 24f - i * 22f;
                TextMeshProUGUI label = Text("Label", _panelRect, 15f, Soft, TextAlignmentOptions.Left);
                Place(label.rectTransform, new Vector2(-92f, y), new Vector2(84f, 20f));
                label.SetText(JobText.T(labels[i]));
                RectTransform track = Box("Track", _panelRect, new Vector2(0.5f, 0.5f), new Vector2(44f, y), new Vector2(BarWidth, 8f), Track);
                RectTransform fill = Box("Fill", track, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(BarWidth, 8f), Good);
                fill.pivot = new Vector2(0f, 0.5f);
                _fill[i] = fill;
                _fillImage[i] = fill.GetComponent<Image>();
            }
            _note = Text("Note", _panelRect, 15f, Fair, TextAlignmentOptions.Left);
            Place(_note.rectTransform, new Vector2(-92f, -44f), new Vector2(190f, 20f));
            _note.gameObject.SetActive(false);
            _panel = _panelRect.gameObject;
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

        /// <param name="note">A line under the bars (an illness and how long it has to run), or null. Pass the same string object while nothing changes.</param>
        public void Show(float hygiene, float stress, float home, bool hasHome, string note)
        {
            if (!_on)
            {
                _on = true;
                _panel.SetActive(true);
            }
            Bar(0, hygiene, hygiene < HomeBalance.HygieneLow ? 2 : hygiene < 50f ? 1 : 0);
            Bar(1, stress, stress > HomeBalance.StressHigh ? 2 : stress > 50f ? 1 : 0);
            Bar(2, hasHome ? home : 0f, !hasHome || home < HomeBalance.MessLow ? 2 : home < 50f ? 1 : 0);
            bool noteOn = note != null;
            if (noteOn != _noteOn)
            {
                _noteOn = noteOn;
                _note.gameObject.SetActive(noteOn);
                _panelRect.sizeDelta = new Vector2(204f, noteOn ? 106f : 78f);
                _panelRect.anchoredPosition = new Vector2(110f, noteOn ? 138f : 150f);
            }
            if (noteOn && !ReferenceEquals(note, _noteShown))
            {
                _noteShown = note;
                _note.SetText(note);
            }
        }

        private void Bar(int index, float value, int state)
        {
            int whole = Mathf.Clamp(Mathf.RoundToInt(value), 0, 100);
            if (whole != _shown[index])
            {
                _shown[index] = whole;
                _fill[index].sizeDelta = new Vector2(BarWidth * whole / 100f, 8f);
            }
            if (state != _state[index])
            {
                _state[index] = state;
                _fillImage[index].color = state == 2 ? Bad : state == 1 ? Fair : Good;
            }
        }

        private static RectTransform Box(string objectName, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color colour)
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
            image.color = colour;
            image.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI Text(string objectName, Transform parent, float size, Color colour, TextAlignmentOptions alignment)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = colour;
#pragma warning disable CS0618
            text.enableWordWrapping = false;
#pragma warning restore CS0618
            if (Hud.Font != null)
            {
                text.font = Hud.Font;
            }
            return text;
        }

        // From the middle of the parent, the text's left edge at x.
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
