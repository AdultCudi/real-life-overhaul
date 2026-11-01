using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A tech firm's whole page as a screen of the kit: the game studio's (StudioViews.Page) or another firm's
    /// (SectorViews.Page) on the dashboard, its actions pressed as the Tech app presses them (the director's own acts), and
    /// worked out again after every press. Cost: a page and a layout when it opens and after a press; nothing per frame.
    /// </summary>
    public sealed class StudioPageScreen : IUiScreen
    {
        private readonly List<ViewAction> _others = new List<ViewAction>(StudioPages.MostOthers);
        private readonly UiStudioWords _words = new UiStudioWords();
        private readonly string _place;
        private ViewAction _primary;
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
            FirmPage page = Page();
            UiDashboardModel m;
            if (page != null)
            {
                m = StudioPages.Page(page, UiText.Words, _others, out _primary);
            }
            else
            {
                _primary = null;
                _others.Clear();
                string empty = UiText.T("reallife_ui_page_empty");
                m = new UiDashboardModel { Title = empty, Table = new UiTableModel { EmptyTitle = empty } };
                m.Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") } };
                m.PromptCount = 1;
            }
            UiDashboard.Lay(s, m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            ViewAction a = command == UiDashboard.ActionCommand ? _primary : command == UiDashboard.OtherCommand && arg >= 0 && arg < _others.Count ? _others[arg] : null;
            return a != null && a.Enabled && Do(a.Act, a.Arg);
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

        public bool Act(UiAction action) => action == UiAction.Alt && Press(UiDashboard.ActionCommand, 0);

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }

        private FirmPage Page()
        {
            TechDirector d = TechDirector.Instance;
            FirmPlace firm = d != null ? d.FirmOf(_place) : null;
            if (firm == null)
            {
                return null;
            }
            return d.StudioRuns(firm.File.K) ? d.StudioFirmPage(firm, _words) : d.SectorFirmPage(firm, _words);
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
