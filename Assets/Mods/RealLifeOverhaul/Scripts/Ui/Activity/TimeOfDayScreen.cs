using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's time of day tool (InGameTimeOfDayPanel, opened from the console to see a room in an hour's light) as a
    /// small panel at the left above the status bar: the hour on the kit's slider, its ends, ten minutes earlier and later,
    /// and Close. Every change goes through the tool's own slider and buttons, so the game lights the room as it would.
    /// Looks four times a second while it is open. Game side. Cost: a read a look; a layout when the hour moved.
    /// </summary>
    public sealed class TimeOfDayScreen : IUiScreen, IUiDragging
    {
        private const float Every = 0.25f;

        private readonly InGameTimeOfDayPanel _tool;
        private readonly Slider _slider;
        private readonly TMPro.TMP_Text _words;   // the tool's own words for the hour, which the kit shows as the tool writes them
        private readonly UiTimeOfDayModel _m = new UiTimeOfDayModel();
        private bool _dirty = true;
        private float _next;
        private int _seen;

        public TimeOfDayScreen(InGameTimeOfDayPanel tool)
        {
            _tool = tool;
            _slider = GameContract.DayToolSlider.Field != null ? GameContract.DayToolSlider.Field.GetValue(tool) as Slider : null;
            _words = GameContract.DayToolText.Field != null ? GameContract.DayToolText.Field.GetValue(tool) as TMPro.TMP_Text : null;
        }

        public string Id => "time_of_day";

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    int now = _slider != null ? Mathf.RoundToInt(_slider.value) : 0;
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _dirty = false;
            float min = _slider != null ? _slider.minValue : 300f, max = _slider != null ? _slider.maxValue : 1440f, value = _slider != null ? _slider.value : min;
            _m.Title = UiText.T("reallife_ui_time_of_day");
            string said = UiWidgets.Text(_words);
            _m.Clock = string.IsNullOrEmpty(said) ? Clock(value) : said;
            _m.Share = max > min ? (value - min) / (max - min) : 0f;
            _m.From = Clock(min);
            _m.To = Clock(max);
            _m.Earlier = UiText.T("reallife_ui_time_of_day_earlier");
            _m.Later = UiText.T("reallife_ui_time_of_day_later");
            UiTimeOfDay.Lay(s, _m, frame, dark, keys);
        }

        // Minutes of the day as the game writes a time (its 12 or 24 hour clock); the day's end is midnight.
        private static string Clock(float minutes)
        {
            int whole = Mathf.FloorToInt(minutes);
            return (whole / 60 % 24).GetFormattedTime(whole % 60);
        }

        public bool Press(int command, int arg)
        {
            if (_tool == null)
            {
                return false;
            }
            _dirty = true;
            if (command == UiDraw.CloseCommand)
            {
                InGameTimeOfDayPanel.Close();   // as the tool's own close: the game's light follows the clock again
                return true;
            }
            if (command == UiTimeOfDay.StepCommand)
            {
                if (arg < 0)
                {
                    _tool.DecreaseTimeButton();
                }
                else
                {
                    _tool.IncreaseTimeButton();
                }
                return true;
            }
            return command == UiTimeOfDay.SliderCommand;
        }

        private void Slide(float x)
        {
            if (_slider == null)
            {
                return;
            }
            float share = UiTimeOfDay.Share(_m, x);
            _slider.value = Mathf.Round(_slider.minValue + share * (_slider.maxValue - _slider.minValue));   // the tool's own listener lights the room
            _dirty = true;
        }

        public void PointerDown(int command, int arg, float x, float y)
        {
            if (command == UiTimeOfDay.SliderCommand)
            {
                Slide(x);
            }
        }

        public bool DragBegin(int command, int arg, float x, float y)
        {
            if (command != UiTimeOfDay.SliderCommand)
            {
                return false;
            }
            Slide(x);
            return true;
        }

        public void DragMove(float x, float y) => Slide(x);

        public void DragEnd(bool cancelled)
        {
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => false;

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => false;

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }
    }
}
