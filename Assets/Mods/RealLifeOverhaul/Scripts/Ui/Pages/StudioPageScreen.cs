using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A tech firm's page as a screen of the kit: the game studio's (StudioViews) or another firm's (SectorViews), its
    /// choices stepped with Enter, its actions pressed as the Tech app presses them (the director's own acts), worked
    /// out again after every press. Cost: a screen and a layout a press; nothing per frame.
    /// </summary>
    public sealed class StudioPageScreen : IUiScreen
    {
        private readonly List<ViewChoice> _choices = new List<ViewChoice>(16);
        private readonly List<ViewAction> _actions = new List<ViewAction>(4);
        private readonly UiStudioWords _words = new UiStudioWords();
        private readonly string _place;
        private bool _dirty = true;

        public Action Close;

        public StudioPageScreen(string place)
        {
            _place = place;
        }

        public string Id => "studio";

        public bool Dirty => _dirty;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            StudioScreen v = Screen();
            UiFormModel m = v != null ? StudioPages.Model(v, UiText.Words, _choices, _actions) : new UiFormModel { Title = UiText.T("reallife_ui_page_empty") };
            UiForm.Lay(s, m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == UiForm.FieldCommand && arg >= 0 && arg < _choices.Count && _choices[arg] != null)
            {
                return Do(_choices[arg].Act, StudioPages.NextValue(_choices[arg]));
            }
            if (command == UiForm.ActionCommand && arg >= 0 && arg < _actions.Count && _actions[arg].Enabled)
            {
                return Do(_actions[arg].Act, _actions[arg].Arg);
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            Close?.Invoke();
            return Close != null;
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action)
        {
            int primary = _actions.Count - 1;
            return action == UiAction.Alt && primary >= 0 && Press(UiForm.ActionCommand, primary);
        }

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        private StudioScreen Screen()
        {
            TechDirector d = TechDirector.Instance;
            FirmPlace firm = d != null ? d.FirmOf(_place) : null;
            if (firm == null)
            {
                return null;
            }
            return d.StudioRuns(firm.File.K) ? d.StudioFirmScreen(firm, _words) : d.SectorFirmScreen(firm, _words);
        }

        private bool Do(int act, int value)
        {
            TechDirector d = TechDirector.Instance;
            FirmPlace firm = d != null ? d.FirmOf(_place) : null;
            if (firm == null)
            {
                return false;
            }
            if (d.StudioRuns(firm.File.K))
            {
                d.StudioDo(act, _place, 0, value);
            }
            else if (!d.SectorDo(act, _place, value))
            {
                return false;
            }
            _dirty = true;
            return true;
        }

        // The figures as the Tech app writes them: money at today's prices, numbers, shares, days and the mod's texts.
        private sealed class UiStudioWords : IStudioFormat
        {
            public string Money(long cents) => TechText.Cents(cents * Prices.Level / 1000);
            public string Whole(long n) => TechText.Whole(n);
            public string Mille(int thousandths) => TechText.Mille(thousandths);
            public string Day(int day) => TechText.Day(day);
            public string Text(string key) => TechText.T(key);
        }
    }
}
