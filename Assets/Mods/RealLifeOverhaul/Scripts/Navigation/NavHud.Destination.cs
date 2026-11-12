using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// What the minimap's headline holds while a route leads somewhere (the owner's mockup, minimap): the place's name over its address at the left, the minutes left
    /// over the distance at the right; and the 2D and 3D button that turns the 2.5D picture flat or tilted. Made once with the panel; written only when a text changes,
    /// by the director's slow tick and the picture's draw. Cost a frame: nothing.
    /// </summary>
    public sealed partial class NavHud
    {
        private const float RightStep = 16f;            // HUD units: the minutes' column follows its text in steps of this, so a changing distance seldom lays it out again

        private TMP_Text _sub, _right;
        private RectTransform _titleRect;
        private bool _destinationOn;
        private string _subShown, _rightShown, _minutesShown, _distanceShown;
        private float _rightWanted;
        private float _headWidth = -1f, _headRight = -1f;
        private int _headParts = -1;
        private RectTransform _switch;
        private TMP_Text _switchText;
        private UnityEngine.Events.UnityAction _switchListener;

        /// <summary>The 2D and 3D button was pressed: the director turns the picture flat or tilted.</summary>
        public Action SwitchPressed;

        // The address under the title and the minutes at the right, made from copies of the minimap's own line of text; nothing if there is none to copy.
        private void DestinationMake()
        {
            _titleRect = _title is TMP_Text title ? title.rectTransform : null;
            if (_sub != null || _head == null || _titleRect == null || _line1 == null)
            {
                return;
            }
            _sub = HeadLabel("Address", NavHeadRules.Address, TextAlignmentOptions.MidlineLeft, 22f, 18f);
            _right = HeadLabel("Minutes", NavHeadRules.Minutes, TextAlignmentOptions.MidlineRight, NavHeadRules.RightSize, NavHeadRules.RightLeastSize);
            if (_right != null)
            {
                _right.fontStyle = FontStyles.Bold;
                _right.richText = true;
            }
        }

        private TMP_Text HeadLabel(string name, uint colour, TextAlignmentOptions alignment, float size, float least)
        {
            TMP_Text text = Label(name, _head, Colour(colour), alignment);
            if (text == null)
            {
                return null;
            }
            text.fontSize = size;
            text.enableAutoSizing = true;   // smaller lettering first; shortened only when that does not fit
            text.fontSizeMax = size;
            text.fontSizeMin = least;
            text.margin = Vector4.zero;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.gameObject.SetActive(false);
            return text;
        }

        /// <summary>
        /// The destination's address and its minutes and distance, or none (null address) when there is no route to speak of. The title keeps the upper part of the
        /// band and the minutes the right of it, as wide as they ask (NavHeadRules.Layout); written only when a text changes.
        /// </summary>
        public void Destination(string address, string minutes, string distance)
        {
            if (_sub == null || _right == null || _titleRect == null)
            {
                HeadLayout();
                return;
            }
            bool on = address != null || minutes != null;
            if (on != _destinationOn)
            {
                _destinationOn = on;
                _subShown = _rightShown = _minutesShown = _distanceShown = null;
                _rightWanted = 0f;
                if (!on)
                {
                    _sub.gameObject.SetActive(false);
                    _right.gameObject.SetActive(false);
                }
            }
            if (!on)
            {
                HeadLayout();
                return;
            }
            string right = _rightShown;
            if (minutes != _minutesShown || distance != _distanceShown)
            {
                // Joined only when a part changed: the tick runs four times a second and most of them find the same words.
                _minutesShown = minutes;
                _distanceShown = distance;
                right = minutes == null ? null : string.IsNullOrEmpty(distance) ? minutes : minutes + "\n<size=78%><color=" + NavHeadRules.DistanceHex + ">" + distance + "</color></size>";
            }
            if (address != _subShown)
            {
                _subShown = address;
                _sub.text = address ?? string.Empty;
                _sub.gameObject.SetActive(address != null);
            }
            if (right != _rightShown)
            {
                _rightShown = right;
                _right.text = right ?? string.Empty;
                _right.gameObject.SetActive(right != null);
                // The column as wide as the words ask at their full size, in steps: measured only when the words change.
                _rightWanted = right == null ? 0f : Mathf.Ceil((_right.preferredWidth + 2f) / RightStep) * RightStep;
            }
            HeadLayout();
        }

        // The band's texts where NavHeadRules puts them, for the card's width and what shows; written only when one of those changed.
        private void HeadLayout()
        {
            if (_titleRect == null || _width <= 0f)
            {
                return;
            }
            bool address = _sub != null && _sub.gameObject.activeSelf;
            bool right = _right != null && _right.gameObject.activeSelf;
            int parts = (address ? 1 : 0) | (right ? 2 : 0);
            float wanted = right ? _rightWanted : 0f;
            if (_width == _headWidth && parts == _headParts && wanted == _headRight)
            {
                return;
            }
            _headWidth = _width;
            _headParts = parts;
            _headRight = wanted;
            NavHeadRules.Layout(NavHeadRules.BandWidth(_width), NavHeadRules.BandHeight(Head), wanted, address, right, out NavHeadBox name, out NavHeadBox under, out NavHeadBox minutes);
            Put(_titleRect, name);
            if (_sub != null)
            {
                Put(_sub.rectTransform, under);
            }
            if (_right != null)
            {
                Put(_right.rectTransform, minutes);
            }
        }

        private static void Put(RectTransform rect, NavHeadBox box)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(box.X, box.Y);
            rect.sizeDelta = new Vector2(box.Width, box.Height);
        }

        // The 2D and 3D button under the compass: a round plate with its words, pressed with the mouse. Made with the strip, shown with it.
        private void SwitchMake(Color32 plate, Color32 accent)
        {
            if (_switch != null)
            {
                return;
            }
            _switch = Plate("Picture switch", _view, plate);
            _switch.anchorMin = _switch.anchorMax = _switch.pivot = new Vector2(1f, 1f);
            _switch.anchoredPosition = new Vector2(-StripPad, -StripPad - CompassSize - SwitchGap);
            _switch.sizeDelta = new Vector2(CompassSize, CompassSize);
            Image face = _switch.GetComponent<Image>();
            face.sprite = NavMarks.Blob;
            face.raycastTarget = true;   // the one part of the view that takes the pointer: the button
            _switchText = Label("Picture switch text", _switch, accent, TextAlignmentOptions.Center);
            if (_switchText != null)
            {
                _switchText.fontSize = 24f;
                _switchText.fontStyle = FontStyles.Bold;
            }
            var button = _switch.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            _switchListener = Guard.Listener(OnSwitch);
            button.onClick.AddListener(_switchListener);
        }

        private void OnSwitch()
        {
            SwitchPressed?.Invoke();
        }

        /// <summary>The words on the picture switch: the picture as it is now.</summary>
        public void SwitchShows(bool tilted)
        {
            if (_switchText != null)
            {
                string words = NavDestRules.Switch(tilted);
                if (_switchText.text != words)
                {
                    _switchText.text = words;
                }
            }
        }
    }
}
