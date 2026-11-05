using Helpers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// What the 2.5D minimap adds inside its view: a strip at the top left with the district and the street the player is on, a compass
    /// at the top right (a dot that goes round a ring to where north is), and a scale bar at the bottom left. All are children of the
    /// view, made when the 2.5D picture is first switched on and shown only with it; they never touch the card's place, size or order.
    /// Cost a frame: nothing (they are written by the picture's draw and the slow tick, and only when a figure changes).
    /// </summary>
    public sealed partial class NavHud
    {
        private const float StripPad = 14f;           // HUD units from the view's edge
        private const float CompassSize = 52f;
        private const float CompassOrbit = 17f;       // the dot's distance from the compass's middle

        private RectTransform _strip, _compass, _compassDot, _scaleBox;
        private TMP_Text _stripText, _scaleText;
        private RectTransform _scaleLine, _scaleA, _scaleB;
        private string _stripShown;
        private int _scaleMetres;
        private float _scaleUnits;
        private float _compassTurn = float.MaxValue;
        private static readonly int[] ScaleSteps = { 1000, 500, 200, 100, 50, 20, 10, 5 };

        // The strip, the compass and the scale bar, made once; false when the panel has no text to copy (the strip and the bar are then left out).
        private void StripMake()
        {
            if (_strip != null || _view == null)
            {
                return;
            }
            Color32 plate = new Color32(15, 25, 34, 205);
            Color32 ink = new Color32(233, 239, 244, 255);
            Color32 line = new Color32(147, 166, 183, 255);
            Color32 accent = new Color32(79, 184, 234, 255);

            _strip = Plate("Strip", _view, plate);
            _strip.anchorMin = _strip.anchorMax = _strip.pivot = new Vector2(0f, 1f);
            _strip.anchoredPosition = new Vector2(StripPad, -StripPad);
            _strip.sizeDelta = new Vector2(240f, 46f);
            _stripText = Label("Strip text", _strip, ink, TextAlignmentOptions.Left);
            if (_stripText != null)
            {
                RectTransform text = _stripText.rectTransform;
                text.anchorMin = Vector2.zero;
                text.anchorMax = Vector2.one;
                text.offsetMin = new Vector2(14f, 0f);
                text.offsetMax = new Vector2(-14f, 0f);
            }

            _compass = Plate("Compass", _view, plate);
            _compass.anchorMin = _compass.anchorMax = _compass.pivot = new Vector2(1f, 1f);
            _compass.anchoredPosition = new Vector2(-StripPad, -StripPad);
            _compass.sizeDelta = new Vector2(CompassSize, CompassSize);
            Image ring = _compass.GetComponent<Image>();
            ring.sprite = NavMarks.Blob;   // a round plate
            RectTransform dot = Plate("North", _compass, accent);
            dot.anchorMin = dot.anchorMax = dot.pivot = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(12f, 12f);
            dot.GetComponent<Image>().sprite = NavMarks.Blob;
            _compassDot = dot;

            SwitchMake(plate, accent);   // the 2D and 3D button under the compass

            _scaleBox = Child("Scale", _view);
            _scaleBox.anchorMin = _scaleBox.anchorMax = _scaleBox.pivot = Vector2.zero;
            _scaleBox.anchoredPosition = new Vector2(StripPad + 4f, StripPad + 4f);
            _scaleBox.sizeDelta = new Vector2(10f, 10f);
            _scaleLine = Plate("Scale line", _scaleBox, line);
            _scaleLine.anchorMin = _scaleLine.anchorMax = _scaleLine.pivot = Vector2.zero;
            _scaleLine.anchoredPosition = Vector2.zero;
            _scaleA = Plate("Scale end", _scaleBox, line);
            _scaleA.anchorMin = _scaleA.anchorMax = _scaleA.pivot = Vector2.zero;
            _scaleB = Plate("Scale end", _scaleBox, line);
            _scaleB.anchorMin = _scaleB.anchorMax = _scaleB.pivot = new Vector2(1f, 0f);
            _scaleText = Label("Scale text", _scaleBox, ink, TextAlignmentOptions.BottomLeft);
            if (_scaleText != null)
            {
                RectTransform text = _scaleText.rectTransform;
                text.anchorMin = text.anchorMax = text.pivot = Vector2.zero;
                text.anchoredPosition = new Vector2(0f, 12f);
                text.sizeDelta = new Vector2(200f, 40f);
            }
        }

        private void StripShow(bool on)
        {
            if (_strip == null)
            {
                return;
            }
            _strip.gameObject.SetActive(on);
            _compass.gameObject.SetActive(on);
            if (_switch != null)
            {
                _switch.gameObject.SetActive(on);
            }
            _scaleBox.gameObject.SetActive(on);
            _stripShown = null;
            _scaleMetres = 0;
            _compassTurn = float.MaxValue;
        }

        /// <summary>The district and the street, as the strip says them; written only when they change.</summary>
        public void SceneStrip(string district, string street)
        {
            if (_stripText == null)
            {
                return;
            }
            string words = string.IsNullOrEmpty(street) ? district : string.IsNullOrEmpty(district) ? street : district + "   " + street;
            if (words == _stripShown)
            {
                return;
            }
            _stripShown = words;
            bool both = !string.IsNullOrEmpty(street) && !string.IsNullOrEmpty(district);
            _stripText.text = both ? "<color=#4FB8EA>" + district + "</color>   " + street : words ?? string.Empty;
            _strip.sizeDelta = new Vector2(Mathf.Clamp(_stripText.preferredWidth + 36f, 120f, Mathf.Max(120f, ViewWidth - CompassSize - StripPad * 3f)), 46f);
        }

        // On each draw of the picture: the dot to where north is, and the scale bar's length for the metres across.
        private void StripFrame(float turn, float metresAcross)
        {
            if (_strip == null || !_strip.gameObject.activeSelf)
            {
                return;
            }
            if (Mathf.Abs(Mathf.DeltaAngle(turn, _compassTurn == float.MaxValue ? turn + 180f : _compassTurn)) > 0.5f)
            {
                _compassTurn = turn;
                float radians = turn * Mathf.Deg2Rad;
                _compassDot.anchoredPosition = new Vector2(-Mathf.Sin(radians) * CompassOrbit, Mathf.Cos(radians) * CompassOrbit);
            }
            // the longest round number of metres that takes up a third of the view's width or less
            float unitsPerMetre = ViewWidth / metresAcross;
            int metres = ScaleSteps[ScaleSteps.Length - 1];
            for (int i = 0; i < ScaleSteps.Length; i++)
            {
                if (ScaleSteps[i] * unitsPerMetre <= ViewWidth * 0.34f)
                {
                    metres = ScaleSteps[i];
                    break;
                }
            }
            float units = Mathf.Round(metres * unitsPerMetre);
            if (metres == _scaleMetres && Mathf.Abs(units - _scaleUnits) < 1f)
            {
                return;
            }
            if (metres != _scaleMetres && _scaleText != null)
            {
                _scaleText.text = metres.ToFormattedDistance();
            }
            _scaleMetres = metres;
            _scaleUnits = units;
            _scaleLine.sizeDelta = new Vector2(units, 4f);
            _scaleA.sizeDelta = new Vector2(4f, 16f);
            _scaleB.sizeDelta = new Vector2(4f, 16f);
            _scaleB.anchoredPosition = new Vector2(units, 0f);
        }

        // A solid plate (no sprite unless one is given): the card's own look, slate and flat.
        private static RectTransform Plate(string name, Transform parent, Color32 colour)
        {
            RectTransform rect = Child(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return rect;
        }

        // A copy of the minimap's own line of text (the game's lettering), for a label.
        private TMP_Text Label(string name, Transform parent, Color32 colour, TextAlignmentOptions alignment)
        {
            if (_line1 == null)
            {
                return null;
            }
            GameObject copy = UnityEngine.Object.Instantiate(_line1.gameObject, parent);
            copy.name = name;
            var text = copy.GetComponent<TMP_Text>();
            if (text == null)
            {
                UnityEngine.Object.Destroy(copy);
                return null;
            }
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            text.color = colour;
            text.alignment = alignment;
            text.enableAutoSizing = false;
            text.fontSize = 26f;
            text.richText = true;
            text.raycastTarget = false;
            text.text = string.Empty;
            copy.SetActive(true);
            return text;
        }
    }
}
