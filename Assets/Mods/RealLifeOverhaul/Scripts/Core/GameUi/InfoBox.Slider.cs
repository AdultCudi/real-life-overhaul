using System;
using BigAmbitions.ModsInternal;
using Localizor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// The box's slider: the game's own, the one its settings menu's slider rows have (UIs.options, ModOptionsSliderControl:
    /// the control the Mod settings app's rows are made of, GameUi.OptionRows), copied once the first time a page asks for
    /// one, with the row's own words taken off. On a page it is a row of its label and the words of the step it stands at,
    /// then the slider the width of the box, its track, fill and knob in the box's colours as its meters have them. Dragged,
    /// it writes its row again and tells the page each new step; the page itself is not written again for it, so a drag is
    /// never cut short. A page that has one copies it afresh, and Clear takes it away with the page, as a picture is. Cost:
    /// nothing per frame; a step dragged writes one row.
    /// </summary>
    public sealed partial class InfoBox
    {
        private const float SliderHeightLeast = 36f;   // pixels: the room the slider is given at the least, as the settings menu's own

        private static readonly string[] SliderHarmless = { "ButtonEffects", "ToggleClickSound", "HoverEffect" };   // the game's scripts that only sound or show a hover

        private GameObject _sliderT;   // the game's slider, made the first time a page asks for one; null until then, or if it could not be had
        private bool _sliderTried;

        /// <summary>
        /// A slider over whole steps from 0 to <paramref name="steps"/>, standing at <paramref name="index"/>: a row of its label
        /// (a locale key) and <paramref name="text"/>'s words for the step, then the game's slider; <paramref name="changed"/>
        /// is told each new step as it is dragged. False if the game's slider could not be had: the caller offers the choice
        /// another way (a selector).
        /// </summary>
        public bool Slider(string labelKey, int steps, int index, Func<int, string> text, Action<int> changed)
        {
            using var own = Timed();
            if (steps < 1 || text == null || changed == null || !SliderMade())
            {
                return false;
            }
            string label = LookRules.Label(labelKey.GetLocalization());
            int at = Mathf.Clamp(index, 0, steps);
            TMP_Text row = RowWords(label, text(at), CellTone.Plain);
            GameObject part = Add(_sliderT);
            var slider = part.GetComponent<Slider>();
            PaintSlider(slider);
            slider.onValueChanged = new Slider.SliderEvent();
            slider.wholeNumbers = true;
            slider.minValue = 0f;
            slider.maxValue = steps;
            slider.SetValueWithoutNotify(at);
            slider.interactable = true;
            int shown = at;
            slider.onValueChanged.AddListener(value => UiClone.Safe(() =>
            {
                int to = Mathf.Clamp(Mathf.RoundToInt(value), 0, steps);
                if (to == shown)
                {
                    return;
                }
                shown = to;
                row.text = RowMarkup(label, text(to), CellTone.Plain, false);
                changed(to);
            }));
            _headerText = null;
            return true;
        }

        // The game's slider, copied asleep among the templates once: the settings menu's slider row's own Slider with its
        // track, fill and knob, and nothing of the row's words; laid out by the box at the width it has.
        private bool SliderMade()
        {
            if (_sliderT != null)
            {
                return true;
            }
            if (_sliderTried || _parked == null)
            {
                return false;
            }
            _sliderTried = true;
            try
            {
                var uis = InstanceBehavior<UI.UIs>.Instance;
                var row = uis != null && uis.options != null ? uis.options.GetComponentInChildren<ModOptionsSliderControl>(true) : null;
                var game = row != null ? row.GetComponentInChildren<Slider>(true) : null;
                if (game == null)
                {
                    UiClone.Fallback("the game's settings menu has no slider for the apps' boxes to copy; their choices are shown with arrows instead");
                    return false;
                }
                GameObject copy = UiClone.Asleep(game.gameObject, _parked.parent, "Slider");
                UiClone.StripGameScripts(copy, script => Array.IndexOf(SliderHarmless, script.GetType().Name) >= 0);
                foreach (TMP_Text words in copy.GetComponentsInChildren<TMP_Text>(true))
                {
                    words.gameObject.SetActive(false);   // a number of the row's under the slider: the box writes the step in its own row
                }
                var rect = (RectTransform)copy.transform;
                var element = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
                element.ignoreLayout = false;
                element.minHeight = element.preferredHeight = Mathf.Max(SliderHeightLeast, rect.rect.height);
                element.flexibleWidth = 1f;
                element.minWidth = 0f;
                _sliderT = copy;
                return true;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                UiClone.Fallback("the game's slider could not be copied for the apps' boxes; their choices are shown with arrows instead");
                return false;
            }
        }

        // The slider in the box's colours: its track as a meter's, its fill in the app's colour, its knob in the box's ink.
        private void PaintSlider(Slider slider)
        {
            Transform back = slider.transform.Find("Background");
            var track = back != null ? back.GetComponent<Image>() : null;
            if (track != null)
            {
                track.color = Color.Lerp(_rule, Color.white, 0.35f);
            }
            var fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            if (fill != null)
            {
                fill.color = Paint(CellTone.Plain);
            }
            var knob = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
            if (knob != null)
            {
                knob.color = _ink;
            }
        }
    }
}
