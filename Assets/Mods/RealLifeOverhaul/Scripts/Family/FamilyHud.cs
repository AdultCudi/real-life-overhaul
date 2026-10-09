using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// What family life puts on screen besides the game's own toasts and conversations: a key
    /// prompt low in the middle, and a two-line objective under the top bar while there is
    /// somewhere to be. A retained canvas built on first use; nothing here runs per frame, and
    /// a text is rebuilt only when it changes (compared by reference: callers keep the string).
    /// </summary>
    public sealed class FamilyHud : MonoBehaviour
    {
        private static readonly Color Plate = new Color(0.05f, 0.06f, 0.09f, 0.86f);
        private static readonly Color Accent = new Color(0.93f, 0.42f, 0.55f, 1f);
        private static readonly Color Soft = new Color(0.80f, 0.84f, 0.90f, 1f);

        private static FamilyHud _instance;

        private GameObject _prompt;
        private TextMeshProUGUI _promptText;
        private string _promptShown;
        private bool _promptOn;

        private GameObject _panel;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _line;
        private string _titleShown;
        private string _lineShown;
        private bool _panelOn;

        public static FamilyHud Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Family HUD").AddComponent<FamilyHud>();
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

            RectTransform panel = Box("Objective", transform, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(640f, 84f), Plate);
            Box("Stripe", panel, new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(6f, 84f), Accent);
            _title = Text("Title", panel, 17f, Soft, TextAlignmentOptions.Left);
            Place(_title.rectTransform, new Vector2(22f, -8f), new Vector2(600f, 24f));
            _line = Text("Line", panel, 22f, Color.white, TextAlignmentOptions.Left);
            Place(_line.rectTransform, new Vector2(22f, -36f), new Vector2(606f, 40f));
            _panel = panel.gameObject;
            _panel.SetActive(false);

            RectTransform prompt = Box("Prompt", transform, new Vector2(0.5f, 0f), new Vector2(0f, 250f), new Vector2(640f, 48f), Plate);
            _promptText = Text("Text", prompt, 23f, Color.white, TextAlignmentOptions.Center);
            RectTransform fill = _promptText.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            _prompt = prompt.gameObject;
            _prompt.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void ShowPrompt(string text)
        {
            if (!_promptOn)
            {
                _promptOn = true;
                _prompt.SetActive(true);
            }
            if (!ReferenceEquals(text, _promptShown))
            {
                _promptShown = text;
                _promptText.SetText(text ?? string.Empty);
            }
        }

        public void HidePrompt()
        {
            if (_promptOn)
            {
                _promptOn = false;
                _prompt.SetActive(false);
            }
        }

        public void ShowObjective(string title, string line)
        {
            if (!_panelOn)
            {
                _panelOn = true;
                _panel.SetActive(true);
            }
            if (!ReferenceEquals(title, _titleShown))
            {
                _titleShown = title;
                _title.SetText(title ?? string.Empty);
            }
            if (!ReferenceEquals(line, _lineShown))
            {
                _lineShown = line;
                _line.SetText(line ?? string.Empty);
            }
        }

        public void HideObjective()
        {
            if (_panelOn)
            {
                _panelOn = false;
                _panel.SetActive(false);
            }
        }

        /// <summary>Everything off for a moment (a photograph) and back.</summary>
        public void Show(bool show)
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = show;
            }
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

        private static TextMeshProUGUI Text(string objectName, Transform parent, float size, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            // The SDK's TextMeshPro is older than the game's: this property is the one both versions have.
#pragma warning disable CS0618
            text.enableWordWrapping = false;
#pragma warning restore CS0618
            text.overflowMode = TextOverflowModes.Ellipsis;
            TMP_FontAsset font = Hud.Font;
            if (font != null)
            {
                text.font = font;
            }
            return text;
        }

        // Anchored to the top left of the parent.
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
