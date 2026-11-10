using System;
using RealLifeOverhaul.Ui;
using UnityEngine;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>
    /// The schemes' one screen, a copy of the game's station list (on the RLO interface the decision card): what the
    /// scheme is, the gain and the risk said plainly, and the answers. The player may always walk away: the cancel key
    /// closes it and nothing is done. A page (<see cref="SchemePage"/>) is written when shown, and an answer hands its
    /// press to the director; nothing runs per frame.
    /// </summary>
    internal sealed class SchemePanel
    {
        private const float InputDelay = 0.4f;   // seconds after a page appears before an answer counts: no double presses
        private const string ButtonChoose = "reallife_scheme_choose";

        private readonly SceneDesk _window = new SceneDesk("RealLifeOverhaul Schemes", 1250f, 150f);
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private readonly Action<SchemePress> _press;
        private readonly Action<Exception> _fault;
        private SchemePage _page;
        private float _shownAt;

        public SchemePanel(Action<SchemePress> press, Action<Exception> fault)
        {
            _press = press;
            _fault = fault;
            _onPick = Pick;
            _onClosed = Closed;
        }

        public bool IsOpen => _page != null && _window.IsOpen;

        /// <summary>Shows a page. False, and nothing shown, if the game's window cannot be made.</summary>
        public bool Show(SchemePage page)
        {
            SceneList list = _window.Take(this);
            if (list == null || page == null)
            {
                _page = null;
                return false;
            }
            _page = page;
            _shownAt = Time.unscaledTime;
            string body = page.Body ?? string.Empty;
            int lines = 1;
            for (int i = 0; i < body.Length; i++)
            {
                lines += body[i] == '\n' ? 1 : 0;
            }
            list.Begin(page.Title ?? string.Empty, page.Kicker ?? string.Empty);
            list.Moment(page.Kicker, UiIcon.Briefcase);
            list.AddNote(string.Empty, body, list.HeightFor((int)((body.Length + 70 * lines) * 1.3f), false));
            for (int i = 0; i < page.Count; i++)
            {
                UiConsequence[] costs = page.Lines[i] != null ? page.Lines[i].ToArray() : null;
                string said = SceneViews.Joined(page.Hints[i], costs, null);
                list.Add(i, page.Choices[i] ?? string.Empty, page.Hints[i], ButtonChoose, true, string.IsNullOrEmpty(said) ? 0f : list.HeightFor(said.Length, true), costs, null);
            }
            list.Open(_onPick, _onClosed);
            list.ScrollToTop();
            list.Locked = false;
            return true;
        }

        public void Close()
        {
            _page = null;
            _window.Close();
        }

        public void Dispose()
        {
            Close();
            _window.Destroy(_fault);
        }

        private void Pick(int choice)
        {
            SchemePage page = _page;
            if (page == null || choice < 0 || choice >= page.Count || Time.unscaledTime - _shownAt < InputDelay)
            {
                return;
            }
            SchemePress press = page.Presses[choice];
            try
            {
                _press?.Invoke(press);
            }
            catch (Exception e)
            {
                Close();
                _fault?.Invoke(e);
                return;
            }
            if (ReferenceEquals(_page, page))
            {
                Close();   // the answer showed no page of its own: this one is done
            }
        }

        private void Closed()
        {
            _page = null;
        }
    }
}
