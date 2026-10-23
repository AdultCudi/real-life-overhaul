using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A switch in the box as the Mod settings app has them: the settings menu's own row (OptionRows.SwitchRow), made when a
    /// page first asks; without it the page uses <see cref="Switch"/>.
    /// </summary>
    public sealed partial class InfoBox
    {
        private const float ToggleWidth = 80f, ToggleHeight = 40f;   // the menu's switch sprite, where its track cannot be measured

        private Transform _templatesAt;   // the box's templates, asleep: where the toggle's is made
        private GameObject _toggleT;
        private bool _toggleTried, _toggleOn;

        /// <summary>A switch row with its label and a short line under it (none when empty). False without the game's row.</summary>
        public bool Toggle(string labelKey, string note, bool on, Action<bool> changed)
        {
            using var own = Timed();
            if (!_toggleTried)
            {
                MakeToggle();
            }
            if (_toggleT == null)
            {
                return false;
            }
            GameObject row = Add(_toggleT);
            TMP_Text[] words = row.transform.Find("Labels").GetComponentsInChildren<TMP_Text>(true);   // the name, then the line under it, as OptionRows.Make finds them
            TMP_Text line = words.Length > 1 ? words[1] : null;
            UiClone.Key(words[0], labelKey);
            bool noted = !string.IsNullOrEmpty(note);
            if (line != null)
            {
                if (line.gameObject.activeSelf != noted)
                {
                    line.gameObject.SetActive(noted);
                }
                UiClone.Words(line, noted ? note : string.Empty);
            }
            var toggle = row.GetComponentInChildren<Toggle>(true);
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.SetIsOnWithoutNotify(on);
            toggle.interactable = true;
            toggle.onValueChanged.AddListener(value => UiClone.Safe(() => changed(value)));
            return true;
        }

        // Words in the box's font, size and colours, indented as a section's rows, the control's column the switch's width.
        private void MakeToggle()
        {
            _toggleTried = true;
            GameObject row = _templatesAt != null ? OptionRows.SwitchRow(_templatesAt) : null;
            if (row == null)
            {
                return;
            }
            Transform labels = row.transform.Find("Labels");
            TMP_Text[] words = labels != null ? labels.GetComponentsInChildren<TMP_Text>(true) : null;
            Toggle toggle = row.GetComponentInChildren<Toggle>(true);
            if (words == null || words.Length < 2 || toggle == null)
            {
                UiClone.Fallback("the game's settings menu's switch row is not built as expected, the box's own switch is used");
                UnityEngine.Object.DestroyImmediate(row);
                return;
            }
            row.name = "Toggle";
            TMP_FontAsset font = _bodyT.GetComponent<TMP_Text>().font;
            Paint(words[0], font, LookRules.BodySize, _ink);
            Paint(words[1], font, LookRules.NoteSize, _muted);
            var track = (toggle.transform.childCount > 0 ? toggle.transform.GetChild(0) : toggle.transform) as RectTransform;
            float width = track != null && track.sizeDelta.x > 1f ? track.sizeDelta.x : ToggleWidth;
            float height = track != null && track.sizeDelta.y > 1f ? track.sizeDelta.y : ToggleHeight;
            for (int i = 0; i < row.transform.childCount; i++)
            {
                Transform child = row.transform.GetChild(i);
                var element = child != labels ? child.GetComponent<LayoutElement>() : null;
                if (element != null)
                {
                    element.minWidth = element.preferredWidth = width;
                    element.minHeight = Mathf.Max(element.minHeight, height);
                }
            }
            var line = row.GetComponent<HorizontalLayoutGroup>();
            if (line != null)
            {
                line.padding = new RectOffset((int)LookRules.Indent, 0, 4, 4);
            }
            _toggleT = row;
            _toggleOn = toggle.isOn;
        }

        private static void Paint(TMP_Text text, TMP_FontAsset font, float size, Color colour)
        {
            if (font != null)
            {
                text.font = font;
            }
            text.fontSize = size;
            text.color = colour;
        }
    }
}
