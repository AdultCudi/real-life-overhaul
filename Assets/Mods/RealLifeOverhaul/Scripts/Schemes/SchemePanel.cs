using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;
using UnityEngine;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>One page of the schemes' panel: what it is about in plain words, and up to three answers, each with what it brings and what it risks.</summary>
    internal sealed class SchemePage
    {
        public string Title;
        public string Kicker;
        public string Body;
        public readonly string[] Choices = new string[SchemePanel.MostChoices];
        public readonly string[] Hints = new string[SchemePanel.MostChoices];
        public readonly Action[] Does = new Action[SchemePanel.MostChoices];
        public readonly List<UiConsequence>[] Lines = new List<UiConsequence>[SchemePanel.MostChoices];
        public int Count;

        /// <summary>An answer; a null <paramref name="what"/> only closes the page.</summary>
        public SchemePage Add(string choice, string hint, Action what)
        {
            if (Count < SchemePanel.MostChoices)
            {
                Choices[Count] = choice;
                Hints[Count] = hint;
                Does[Count] = what;
                Count++;
            }
            return this;
        }

        /// <summary>A line under the answer added last: what it brings (+1), what it risks (-1) or the money it costs (UiConsequence.Loss).</summary>
        public SchemePage Line(string text, int tone)
        {
            if (Count > 0 && !string.IsNullOrEmpty(text))
            {
                List<UiConsequence> lines = Lines[Count - 1] ?? (Lines[Count - 1] = new List<UiConsequence>(2));
                lines.Add(new UiConsequence { Text = text, Tone = tone, Change = tone == UiConsequence.Loss ? -1 : 0 });
            }
            return this;
        }
    }

    /// <summary>
    /// The schemes' one screen, a copy of the game's station list (on the RLO interface the decision card): what the
    /// scheme is, the gain and the risk said plainly, and the answers. The player may always walk away: the cancel key
    /// closes it and nothing is done. A page is written when shown; nothing runs per frame.
    /// </summary>
    internal sealed class SchemePanel
    {
        public const int MostChoices = 3;

        private const float InputDelay = 0.4f;   // seconds after a page appears before an answer counts: no double presses
        private const string ButtonChoose = "reallife_scheme_choose";

        private readonly SceneDesk _window = new SceneDesk("RealLifeOverhaul Schemes", 1250f, 150f);
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private readonly Action<Exception> _fault;
        private SchemePage _page;
        private float _shownAt;

        public SchemePanel(Action<Exception> fault)
        {
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
            Action what = page.Does[choice];
            if (what == null)
            {
                Close();
                return;
            }
            try
            {
                what();
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
