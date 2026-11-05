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
        private const float RightWidth = 190f;         // HUD units: the minutes and the distance, right aligned in the headline
        private const float SwitchSize = 52f;

        private TMP_Text _sub, _right;
        private RectTransform _titleRect;
        private Vector2 _titleMin, _titleMax, _titleOffMin, _titleOffMax;
        private bool _destinationOn;
        private string _subShown, _rightShown;
        private RectTransform _switch;
        private TMP_Text _switchText;
        private UnityEngine.Events.UnityAction _switchListener;

        /// <summary>The 2D and 3D button was pressed: the director turns the picture flat or tilted.</summary>
        public Action SwitchPressed;

        // The address under the title and the minutes at the right, made from copies of the minimap's own line of text; nothing if there is none to copy.
        private void DestinationMake()
        {
            if (_sub != null || _head == null || _title == null || _line1 == null)
            {
                return;
            }
            _titleRect = ((TMP_Text)_title).rectTransform;
            _titleMin = _titleRect.anchorMin;
            _titleMax = _titleRect.anchorMax;
            _titleOffMin = _titleRect.offsetMin;
            _titleOffMax = _titleRect.offsetMax;
            _sub = HeadLabel("Address", new Color32(150, 170, 187, 255), TextAlignmentOptions.MidlineLeft, 26f);
            _right = HeadLabel("Minutes", new Color32(233, 239, 244, 255), TextAlignmentOptions.MidlineRight, 32f);
            if (_right != null)
            {
                _right.fontStyle = FontStyles.Bold;
                RectTransform rect = _right.rectTransform;
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.sizeDelta = new Vector2(RightWidth, 0f);
                rect.anchoredPosition = Vector2.zero;
                _right.richText = true;
            }
        }

        private TMP_Text HeadLabel(string name, Color32 colour, TextAlignmentOptions alignment, float size)
        {
            TMP_Text text = Label(name, _head, colour, alignment);
            if (text == null)
            {
                return null;
            }
            text.fontSize = size;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.gameObject.SetActive(false);
            return text;
        }

        /// <summary>
        /// The destination's address and its minutes and distance, or none (null address) when there is no route to speak of. The title keeps the upper part of the
        /// headline and gives the right edge to the minutes; written only when a text changes.
        /// </summary>
        public void Destination(string address, string minutes, string distance)
        {
            if (_sub == null || _right == null || _titleRect == null)
            {
                return;
            }
            bool on = address != null || minutes != null;
            if (on != _destinationOn)
            {
                _destinationOn = on;
                _sub.gameObject.SetActive(on && address != null);
                _right.gameObject.SetActive(on && minutes != null);
                if (on)
                {
                    _titleRect.anchorMin = new Vector2(_titleMin.x, 0.42f);
                    _titleRect.anchorMax = _titleMax;
                    _titleRect.offsetMin = new Vector2(_titleOffMin.x, 0f);
                    _titleRect.offsetMax = new Vector2(_titleOffMax.x - RightWidth - 8f, _titleOffMax.y);
                    RectTransform sub = _sub.rectTransform;
                    sub.anchorMin = new Vector2(0f, 0f);
                    sub.anchorMax = new Vector2(1f, 0.44f);
                    sub.offsetMin = new Vector2(_titleOffMin.x, 0f);
                    sub.offsetMax = new Vector2(_titleOffMax.x - RightWidth - 8f, 0f);
                }
                else
                {
                    _titleRect.anchorMin = _titleMin;
                    _titleRect.anchorMax = _titleMax;
                    _titleRect.offsetMin = _titleOffMin;
                    _titleRect.offsetMax = _titleOffMax;
                }
                _subShown = _rightShown = null;
            }
            if (!on)
            {
                return;
            }
            string right = minutes == null ? null : string.IsNullOrEmpty(distance) ? minutes : minutes + "\n<size=75%><color=#93A6B7>" + distance + "</color></size>";
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
            }
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
            _switch.anchoredPosition = new Vector2(-StripPad, -StripPad - CompassSize - 10f);
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
