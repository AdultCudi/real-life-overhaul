using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What car ownership puts on screen: an information panel and a one-line key prompt (the
    /// repair bay's list of what is due), and the fuel gauge beside the speedometer. A retained
    /// canvas built on first use; every text is set only when it changes, and nothing here runs
    /// per frame. The buses and the subway journey no longer draw here: they use the game's own
    /// panel and list (Core/GameUi/WorldPanel, TravelList).
    /// </summary>
    public sealed class TransportHud : MonoBehaviour
    {
        private static TransportHud _instance;

        private GameObject _panel;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private string _titleShown;
        private string _bodyShown;
        private bool _panelOn;

        private GameObject _prompt;
        private TextMeshProUGUI _promptText;
        private string _promptShown;
        private bool _promptOn;

        private GameObject _gauge;
        private RectTransform _gaugeFill;
        private Image _gaugeFillImage;
        private TextMeshProUGUI _gaugeText;
        private TextMeshProUGUI _gaugeNote;
        private int _gaugeShown = int.MinValue;
        private string _noteShown;
        private bool _gaugeOn;

        public static TransportHud Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("RealLifeOverhaul Transport HUD").AddComponent<TransportHud>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10;   // under the game's own menus, like the mod's other HUD
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var dark = new Color(0.04f, 0.05f, 0.07f, 0.80f);
            var blue = new Color(0.10f, 0.32f, 0.62f, 0.95f);

            // Left of the screen, below the portrait and clear of the phone.
            RectTransform panel = Box("Panel", transform, new Vector2(0f, 0.5f), new Vector2(250f, 40f), new Vector2(440f, 262f), dark);
            RectTransform head = Box("Head", panel, new Vector2(0.5f, 1f), new Vector2(0f, -19f), new Vector2(440f, 38f), blue);
            _title = Text("Title", head, 21f, Color.white, TextAlignmentOptions.Left);
            Fill(_title.rectTransform, 12f, 0f);
            _body = Text("Body", panel, 19f, new Color(0.92f, 0.94f, 0.97f), TextAlignmentOptions.TopLeft);
            _body.fontStyle = FontStyles.Normal;
            _body.rectTransform.anchorMin = Vector2.zero;
            _body.rectTransform.anchorMax = Vector2.one;
            _body.rectTransform.offsetMin = new Vector2(12f, 8f);
            _body.rectTransform.offsetMax = new Vector2(-12f, -44f);
            _panel = panel.gameObject;
            _panel.SetActive(false);

            RectTransform prompt = Box("Prompt", transform, new Vector2(0.5f, 0f), new Vector2(0f, 262f), new Vector2(760f, 40f), dark);
            _promptText = Text("Text", prompt, 21f, Color.white, TextAlignmentOptions.Center);
            Fill(_promptText.rectTransform, 8f, 0f);
            _prompt = prompt.gameObject;
            _prompt.SetActive(false);

            // To the right of the speedometer (which is 150 wide at 400, 150).
            RectTransform gauge = Box("Fuel", transform, new Vector2(0.5f, 0f), new Vector2(534f, 150f), new Vector2(106f, 84f), new Color(0.04f, 0.05f, 0.07f, 0.72f));
            _gaugeText = Text("Value", gauge, 22f, Color.white, TextAlignmentOptions.Center);
            Place(_gaugeText.rectTransform, new Vector2(0f, 20f), new Vector2(106f, 28f));
            Box("Track", gauge, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(86f, 12f), new Color(1f, 1f, 1f, 0.16f));
            _gaugeFill = Box("Level", gauge, new Vector2(0.5f, 0f), new Vector2(-43f, 60f), new Vector2(86f, 12f), Color.white);
            _gaugeFill.pivot = new Vector2(0f, 0.5f);
            _gaugeFillImage = _gaugeFill.GetComponent<Image>();
            _gaugeNote = Text("Note", gauge, 13f, new Color(1f, 0.72f, 0.25f), TextAlignmentOptions.Center);
            Place(_gaugeNote.rectTransform, new Vector2(0f, 3f), new Vector2(106f, 18f));
            _gauge = gauge.gameObject;
            _gauge.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ---------------------------------------------------------------- the panel

        /// <summary>Shows the panel with a heading and up to eight lines. A no-op when both are what is already there.</summary>
        public void ShowPanel(string title, string body)
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
            if (!ReferenceEquals(body, _bodyShown))
            {
                _bodyShown = body;
                _body.SetText(body ?? string.Empty);
            }
        }

        public void HidePanel()
        {
            if (_panelOn)
            {
                _panelOn = false;
                _panel.SetActive(false);
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

        // ---------------------------------------------------------------- the fuel gauge

        /// <param name="percent">0 to 100.</param>
        /// <param name="note">A word under the bar (already localised: "SERVICE", "TYRES"), or null.</param>
        public void ShowFuel(int percent, string note)
        {
            if (!_gaugeOn)
            {
                _gaugeOn = true;
                _gauge.SetActive(true);
            }
            if (percent != _gaugeShown)
            {
                _gaugeShown = percent;
                _gaugeText.SetText("{0}%", percent);
                _gaugeFill.sizeDelta = new Vector2(86f * Mathf.Clamp01(percent / 100f), 12f);
                Color colour = percent <= 10 ? new Color(0.95f, 0.22f, 0.18f) : percent <= 20 ? new Color(1f, 0.72f, 0.25f) : new Color(0.55f, 0.85f, 0.45f);
                _gaugeFillImage.color = colour;
                _gaugeText.color = percent <= 20 ? colour : Color.white;
            }
            if (!ReferenceEquals(note, _noteShown))
            {
                _noteShown = note;
                _gaugeNote.SetText(note ?? string.Empty);
            }
        }

        public void HideFuel()
        {
            if (_gaugeOn)
            {
                _gaugeOn = false;
                _gauge.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- building

        private static RectTransform Box(string objectName, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color colour)
        {
            var made = new GameObject(objectName, typeof(RectTransform));
            made.transform.SetParent(parent, false);
            var rect = (RectTransform)made.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = made.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return rect;
        }

        private static TextMeshProUGUI Text(string objectName, Transform parent, float size, Color colour, TextAlignmentOptions alignment)
        {
            var made = new GameObject(objectName, typeof(RectTransform));
            made.transform.SetParent(parent, false);
            var text = made.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = colour;
            text.richText = true;
            if (Hud.Font != null)
            {
                text.font = Hud.Font;
            }
            return text;
        }

        private static void Fill(RectTransform rect, float sides, float ends)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(sides, ends);
            rect.offsetMax = new Vector2(-sides, -ends);
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
