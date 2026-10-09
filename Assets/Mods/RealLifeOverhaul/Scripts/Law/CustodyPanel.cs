using System;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The holding cell, as the player sees it: one panel over a dark screen with the precinct's
    /// name, the charge sheet, a clock counting down game time to release, and three things to
    /// do (sit it out, pay bail, call a lawyer), each a button and a number key. Built the first
    /// time somebody is held and kept switched off between; nothing here runs by itself, and a
    /// text is only set when what it says has changed.
    /// </summary>
    internal sealed class CustodyPanel : MonoBehaviour
    {
        public const int Options = 3;

        private static readonly Color Backdrop = new Color(0.035f, 0.04f, 0.055f, 1f);
        private static readonly Color Plate = new Color(0.085f, 0.10f, 0.135f, 1f);
        private static readonly Color Bar = new Color(0.14f, 0.16f, 0.20f, 1f);
        private static readonly Color Accent = new Color(0.20f, 0.42f, 0.95f, 1f);
        private static readonly Color Soft = new Color(0.66f, 0.72f, 0.82f);
        private static readonly Color ButtonOn = new Color(0.16f, 0.20f, 0.28f, 1f);
        private static readonly Color ButtonOff = new Color(0.10f, 0.11f, 0.13f, 1f);

        /// <summary>True while the cell is on screen: the mouse cursor is needed (Camera/ThirdPersonCamera asks).</summary>
        public static bool IsOpen => _cellOpen || DeskPanel.IsOpen;   // (the front desk has buttons too: one answer for whoever frees the cursor)

        private static bool _cellOpen;

        private GameObject _root;
        private GameObject _bars;
        private Image _back;
        private RectTransform _plate;
        private TMP_FontAsset _font;
        private TextMeshProUGUI _precinct;
        private TextMeshProUGUI _sheet;
        private TextMeshProUGUI _clock;
        private TextMeshProUGUI _sentence;
        private TextMeshProUGUI _status;
        private readonly TextMeshProUGUI[] _optionText = new TextMeshProUGUI[Options];
        private readonly Image[] _optionPlate = new Image[Options];
        private readonly Button[] _optionButton = new Button[Options];
        private readonly bool[] _optionOn = new bool[Options];
        private Action<int> _chosen;
        private int _hours = -1;
        private int _minutes = -1;
        private string _clockFormat;

        public static CustodyPanel Create(Transform parent)
        {
            var go = new GameObject("RealLifeOverhaul Holding Cell");
            go.transform.SetParent(parent, false);
            return go.AddComponent<CustodyPanel>();
        }

        private void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;   // over everything, the time skip's own clock included: this is the screen while held
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            _font = Hud.Font;

            _root = new GameObject("Cell", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var all = (RectTransform)_root.transform;
            Stretch(all);
            _back = _root.AddComponent<Image>();
            _back.color = Backdrop;
            _back.raycastTarget = true;   // nothing under the cell can be clicked

            // The bars of the cell door, behind everything else.
            _bars = new GameObject("Bars", typeof(RectTransform));
            _bars.transform.SetParent(all, false);
            Stretch((RectTransform)_bars.transform);
            for (int i = 0; i < 13; i++)
            {
                Box("Bar", _bars.transform, new Vector2(0.5f, 0.5f), new Vector2((i - 6) * 150f, 0f), new Vector2(16f, 1200f), Bar);
            }

            RectTransform plate = Box("Sheet", all, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(980f, 660f), Plate);
            _plate = plate;
            Box("Stripe", plate, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(980f, 8f), Accent);

            TextMeshProUGUI title = Text("Title", plate, 22f, Soft, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0f, -30f), new Vector2(900f, 30f));
            title.SetText("reallife_law_cell_title".Tr());
            _precinct = Text("Precinct", plate, 40f, Color.white, TextAlignmentOptions.Center);
            Place(_precinct.rectTransform, new Vector2(0f, -66f), new Vector2(900f, 52f));

            TextMeshProUGUI sheetTitle = Text("Sheet Title", plate, 20f, Soft, TextAlignmentOptions.Left);
            Place(sheetTitle.rectTransform, new Vector2(0f, -140f), new Vector2(860f, 26f));
            sheetTitle.SetText("reallife_law_cell_sheet".Tr());
            _sheet = Text("Charges", plate, 25f, Color.white, TextAlignmentOptions.TopLeft);
            Place(_sheet.rectTransform, new Vector2(0f, -172f), new Vector2(860f, 200f));
#pragma warning disable CS0618
            _sheet.enableWordWrapping = true;
#pragma warning restore CS0618
            _sheet.fontStyle = FontStyles.Normal;

            _clock = Text("Clock", plate, 56f, Color.white, TextAlignmentOptions.Center);
            Place(_clock.rectTransform, new Vector2(0f, -392f), new Vector2(900f, 70f));
            _sentence = Text("Sentence", plate, 21f, Soft, TextAlignmentOptions.Center);
            Place(_sentence.rectTransform, new Vector2(0f, -462f), new Vector2(900f, 28f));
            _status = Text("Status", plate, 22f, new Color(1f, 0.80f, 0.35f), TextAlignmentOptions.Center);
            Place(_status.rectTransform, new Vector2(0f, -500f), new Vector2(900f, 30f));

            for (int i = 0; i < Options; i++)
            {
                int index = i;
                RectTransform button = Box("Option", plate, new Vector2(0.5f, 0f), new Vector2((i - 1) * 316f, 60f), new Vector2(300f, 76f), ButtonOn);
                _optionPlate[i] = button.GetComponent<Image>();
                _optionPlate[i].raycastTarget = true;
                _optionButton[i] = button.gameObject.AddComponent<Button>();
                _optionButton[i].targetGraphic = _optionPlate[i];
                _optionButton[i].onClick.AddListener(() => Click(index));
                RectTransform tag = Box("Key", button, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(34f, 34f), Accent);
                TextMeshProUGUI tagText = Text("Text", tag, 22f, Color.white, TextAlignmentOptions.Center);
                Stretch(tagText.rectTransform);
                tagText.SetText(JobKeys.ChoiceLabel(i));
                _optionText[i] = Text("Label", button, 21f, Color.white, TextAlignmentOptions.Left);
                _optionText[i].rectTransform.anchorMin = new Vector2(0f, 0f);
                _optionText[i].rectTransform.anchorMax = new Vector2(1f, 1f);
                _optionText[i].rectTransform.offsetMin = new Vector2(54f, 4f);
                _optionText[i].rectTransform.offsetMax = new Vector2(-8f, -4f);
#pragma warning disable CS0618
                _optionText[i].enableWordWrapping = true;
#pragma warning restore CS0618
            }
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_root != null && _root.activeSelf)
            {
                _cellOpen = false;
            }
        }

        private void Click(int index)
        {
            if (index >= 0 && index < Options && _optionOn[index])
            {
                _chosen?.Invoke(index);
            }
        }

        /// <summary>Puts the cell on screen. <paramref name="chosen"/> is told which button was clicked (0, 1 or 2).</summary>
        /// <param name="docked">
        /// True when the player is standing in a real cell: the sheet is then a smaller plate at the right of the screen
        /// with the room in view beside it, instead of the whole screen.
        /// </param>
        public void Open(string precinct, string sheet, string sentence, Action<int> chosen, bool docked = false)
        {
            _back.color = docked ? new Color(0f, 0f, 0f, 0f) : Backdrop;
            _back.raycastTarget = !docked;
            _bars.SetActive(!docked);
            _plate.localScale = docked ? new Vector3(0.68f, 0.68f, 1f) : Vector3.one;
            _plate.anchoredPosition = docked ? new Vector2(596f, 60f) : new Vector2(0f, 20f);
            _plate.GetComponent<Image>().raycastTarget = docked;   // (a click on the plate does not reach the room behind it)
            _chosen = chosen;
            _precinct.SetText(precinct);
            _sheet.SetText(sheet);
            _sentence.SetText(sentence);
            _status.SetText(string.Empty);
            _clockFormat = "reallife_law_cell_clock".Tr().Replace("{hours}", "{0}").Replace("{minutes}", "{1}{2}");
            _hours = -1;
            _minutes = -1;
            _root.SetActive(true);
            _cellOpen = true;
        }

        public void Close()
        {
            _chosen = null;
            if (_root != null && _root.activeSelf)
            {
                _root.SetActive(false);
            }
            _cellOpen = false;
        }

        /// <summary>The time still to serve. Rebuilt only when the minute shown changes.</summary>
        public void SetClock(int hours, int minutes)
        {
            if (hours == _hours && minutes == _minutes)
            {
                return;
            }
            _hours = hours;
            _minutes = minutes;
            _clock.SetText(_clockFormat, hours, minutes / 10, minutes % 10);   // (the text mesh has no zero padding of its own)
        }

        public void SetStatus(string text)
        {
            _status.SetText(text ?? string.Empty);
        }

        public void SetSentence(string text)
        {
            _sentence.SetText(text ?? string.Empty);
        }

        /// <summary>What a button says, and whether it can be pressed.</summary>
        public void SetOption(int index, string label, bool available)
        {
            if (index < 0 || index >= Options)
            {
                return;
            }
            _optionOn[index] = available;
            _optionText[index].SetText(label ?? string.Empty);
            _optionText[index].color = available ? Color.white : Soft;
            _optionPlate[index].color = available ? ButtonOn : ButtonOff;
            _optionButton[index].interactable = available;
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

        private TextMeshProUGUI Text(string objectName, Transform parent, float size, Color colour, TextAlignmentOptions alignment)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.color = colour;
            // The SDK's TextMeshPro is older than the game's: this property is the one both versions have.
#pragma warning disable CS0618
            text.enableWordWrapping = false;
#pragma warning restore CS0618
            if (_font != null)
            {
                text.font = _font;
            }
            return text;
        }

        // Anchored to the top centre of the parent, hanging down from the given offset.
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Texts for the law's own screens and toasts: the locale line with the key names filled in.</summary>
    internal static class LawText
    {
        /// <summary>The line for a key, with {key}, {key1}..{key3} filled in (Jobs/JobKeys, Core/Keys).</summary>
        public static string Tr(this string key)
        {
            return JobKeys.Fill(key.GetLocalization());
        }
    }
}
