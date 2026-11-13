using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A tech firm's whole page as a screen of the kit: the game studio's (StudioViews.Page) or another firm's
    /// (SectorViews.Page) on the app window as the Tech app's own firm pages stand, its actions pressed as the Tech app presses
    /// them (the director's own acts), and worked out again after every press. Cost: a page and a layout when it opens and
    /// after a press; nothing per frame.
    /// </summary>
    public sealed class StudioPageScreen : IUiScreen
    {
        private const int Tables = 4;

        private readonly List<ViewAction> _acts = new List<ViewAction>(StudioPages.MostOthers + 1);
        private readonly TechApp.StudioWords _words = new TechApp.StudioWords();
        private readonly float[] _offset = new float[Tables];
        private readonly int[] _reveal = { -1, -1, -1, -1 };
        private readonly string _place;   // the firm's place, or null for the labelled sample studio
        private StudioFaceModel _m = new StudioFaceModel();
        private int _tab;
        private bool _dirty = true;

        public Action Close;

        public StudioPageScreen(string place)
        {
            _place = place;
        }

        public string Id => "studio";

        public bool Dirty => _dirty;

        /// <summary>The model as last laid, for the checks and the tests.</summary>
        public StudioFaceModel Model => _m;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            FirmPage page = _place != null ? Page() : null;
            _acts.Clear();
            if (_place == null)
            {
                _m = StudioPages.Sample(UiText.Words);
            }
            else if (page != null)
            {
                _m = StudioPages.Page(page, UiText.Words, _acts, TechLive.Extras(_place));
                _tab = _m.Tabs.Length > 0 ? Math.Max(0, Math.Min(_tab, _m.Tabs.Length - 1)) : 0;
                _m.Tab = _tab;
            }
            else
            {
                _m = StudioPages.Empty(UiText.Words);
            }
            for (int i = 0; i < Tables; i++)
            {
                UiDataTableModel t = Table(i);
                if (t != null)
                {
                    t.Offset = _offset[i];
                    t.Reveal = _reveal[i];
                }
                _reveal[i] = -1;
            }
            StudioFace.Lay(s, _m, frame, dark, keys);
            for (int i = 0; i < Tables; i++)
            {
                UiDataTableModel t = Table(i);
                _offset[i] = t != null ? t.Offset : 0f;
            }
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            if (command == StudioFace.ActionCommand)
            {
                ViewAction a = arg >= 0 && arg < _acts.Count ? _acts[arg] : null;
                return a != null && a.Enabled && Do(a.Act, a.Arg);
            }
            if (command == UiAppWindow.NavCommand && arg >= 0 && arg < _m.Tabs.Length)
            {
                _tab = arg;
                _dirty = true;
                return true;
            }
            if (command == StudioFace.NoteCommand)
            {
                TechDirector d = TechDirector.Instance;
                if (d != null)
                {
                    d.OpenRoles(TechDirector.TabTeam, null);
                }
                return d != null;
            }
            // A row is read where it stands: it is a target so the keys and the pad can scroll to it, and it opens nothing.
            return command == StudioFace.MainBase + UiDataTable.Row || command == StudioFace.MoreBase + UiDataTable.Row || command == StudioFace.TeamBase + UiDataTable.Row
                   || command == StudioFace.PeopleBase + UiDataTable.Row;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            Close?.Invoke();
            return Close != null;
        }

        // The overview and the team change places with Tab.
        public void Tab(int step)
        {
            if (_m.Tabs.Length > 1)
            {
                _tab = ((_tab + step) % _m.Tabs.Length + _m.Tabs.Length) % _m.Tabs.Length;
                _dirty = true;
            }
        }

        // F runs the main action and P the first of the others, as their buttons' caps say.
        public bool Act(UiAction action)
        {
            for (int i = 0; i < _m.Actions.Length; i++)
            {
                if (_m.Actions[i].Key == action && action != UiAction.Count)
                {
                    return Press(StudioFace.ActionCommand, i);
                }
            }
            return false;
        }

        public void Reveal(int list, int index)
        {
            for (int i = 0; i < Tables; i++)
            {
                if (list >= 0 && list == _m.Lists[i])
                {
                    _reveal[i] = index;
                    _dirty = true;
                }
            }
        }

        public void Wheel(int clip, float notches)
        {
            for (int i = 0; i < Tables; i++)
            {
                if (clip >= 0 && clip == _m.Clips[i])
                {
                    _offset[i] = Math.Max(0f, _offset[i] - notches * UiTokens.TableRow);
                    _dirty = true;
                }
            }
        }

        private UiDataTableModel Table(int i) => i == 0 ? _m.Main : i == 1 ? _m.More : i == 2 ? _m.Team : _m.People;

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
            if (d == null || !d.PageAct(_place, act, value))
            {
                return false;
            }
            _dirty = true;
            return true;
        }
    }
}
