using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// A switch in the box as the Mod settings app has them: the game's own settings menu row (GameUi.OptionRows: the
    /// menu's on and off switch, its sprites switch-off@2x and switch-on@2x, at the right), its name and the short line under
    /// it set in the box's own words and colours, the control's column as wide as the switch rather than the menu's 700.
    /// The template is made the first time a page asks for one (OptionRows.SwitchRow) and kept from page to page as the
    /// box's other parts are (InfoBox.Keep.cs). Where the game's menu could not be copied it returns false and the page
    /// uses the box's tick switch (Switch). Cost: nothing per frame; the first such page copies the menu's rows once.
    /// </summary>
    public sealed partial class InfoBox
    {
        private const float ToggleWidth = 80f, ToggleHeight = 40f;   // the menu's switch sprite (80 by 40) where its track cannot be measured

        private Transform _templatesAt;   // the box's templates, asleep: where the toggle's is made
        private GameObject _toggleT;
        private bool _toggleTried, _toggleOn;

        /// <summary>
        /// A switch row with its label and, under it, a short line (none when empty). False if the settings menu's switch
        /// row could not be had: the caller uses <see cref="Switch"/>.
        /// </summary>
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
            TMP_Text[] words = row.transform.Find("Labels").GetComponentsInChildren<TMP_Text>(true);   // the name, then the line under it (as OptionRows.Make finds them)
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

        // The settings menu's switch row, set for the box: its words in the box's font, size and colours, indented as a
        // section's rows are, the control's column the switch's own width.
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
