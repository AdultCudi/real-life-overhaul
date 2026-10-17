using System;
using System.Text;
using RealLifeOverhaul.GameUi;
using UI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>Where a page of the justice panel is set.</summary>
    public enum JusticeScene
    {
        Court,
        Cell,
        Office      // the Legal file, letters, summaries
    }

    /// <summary>Who stands on a page of the justice panel.</summary>
    public enum JusticeFace
    {
        None,
        Judge,
        Prosecutor,
        Counsel,
        Officer,
        Inmate,
        Visitor,
        Clerk
    }

    /// <summary>One page of the justice system's screen: who is there, who speaks, the text and up to three answers.</summary>
    public sealed class JusticePage
    {
        public JusticeScene Scene;
        public string Title;
        public string Subtitle;
        public JusticeFace Left, Middle, Right;
        public string LeftName, MiddleName, RightName;
        public int Speaker = -1;        // 0 left, 1 middle, 2 right; -1 nobody
        public string SpeakerName;
        public string Body;
        public readonly string[] Choices = new string[JusticePanel.MostChoices];
        public readonly string[] Hints = new string[JusticePanel.MostChoices];
        public int Count;

        public JusticePage Add(string choice, string hint = null)
        {
            if (Count < JusticePanel.MostChoices)
            {
                Choices[Count] = choice;
                Hints[Count] = hint;
                Count++;
            }
            return this;
        }
    }

    /// <summary>The justice system's one screen, a copy of the game's station list. A page is written when shown; nothing runs per frame.</summary>
    public sealed class JusticePanel
    {
        public const int MostChoices = 3;

        private const float InputDelay = 0.4f;    // seconds after a page appears before an answer counts: no double presses
        private const string ButtonChoose = "reallife_justice_list_choose";

        private static JusticePanel _instance;

        private readonly DeskList _window = new DeskList("RealLifeOverhaul Justice", 1250f, 150f);
        private readonly StringBuilder _info = new StringBuilder(160);
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private Action<int> _answer;
        private int _count;
        private float _shownAt;
        private bool _open;
        private bool _onCover;

        private JusticePanel()
        {
            _onPick = Pick;
            _onClosed = Closed;
        }

        public static JusticePanel Instance => _instance ?? (_instance = new JusticePanel());

        public static bool Exists => _instance != null;

        /// <summary>True from the first page until <see cref="Close"/>.</summary>
        public static bool IsOpen => _instance != null && _instance._open;

        /// <summary>True while a page is on screen.</summary>
        public static bool IsShowing => _instance != null && _instance._open && _instance._window.IsOpen;

        public void Show(JusticePage page, Action<int> answer)
        {
            TravelList list = _window.Take(this);
            if (list == null)
            {
                _open = false;
                _answer = null;
                JusticeDirector director = JusticeDirector.Instance;
                if (director != null)
                {
                    director.UiLost();
                }
                return;
            }
            _answer = answer;
            _count = Mathf.Clamp(page.Count, 1, MostChoices);
            _shownAt = Time.unscaledTime;

            _info.Length = 0;
            _info.Append(page.Subtitle ?? string.Empty);
            AddName(page.LeftName);
            AddName(page.MiddleName);
            AddName(page.RightName);
            list.Begin(page.Title ?? string.Empty, _info.ToString());
            string body = page.Body ?? string.Empty;
            string speaker = page.SpeakerName ?? string.Empty;
            int lines = 1;
            for (int i = 0; i < body.Length; i++)
            {
                lines += body[i] == '\n' ? 1 : 0;
            }
            list.AddNote(speaker, body, list.HeightFor((int)((body.Length + 70 * lines) * 1.3f), false));
            for (int i = 0; i < _count; i++)
            {
                string hint = page.Hints[i];
                list.Add(i, page.Choices[i] ?? string.Empty, hint, ButtonChoose, true,
                    string.IsNullOrEmpty(hint) ? 0f : list.HeightFor(hint.Length, true));
            }
            Stand(list);
            list.Open(_onPick, _onClosed);
            list.ScrollToTop();
            JusticeDirector owner = JusticeDirector.Instance;
            list.Locked = !(page.Scene == JusticeScene.Office && owner != null && owner.FileIsOpen);
            if (!_open)
            {
                _open = true;
                if (Meta.Dev.Verbose)
                {
                    JusticeDirector.Logger?.Info("Justice check (page list): the game's list is open (" + page.Scene + "), " + list.Made + " entries made so far: PASS.");
                }
            }
        }

        private void AddName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                _info.Append(_info.Length > 0 ? "\n" : string.Empty).Append(name);
            }
        }

        // While the screen is faded the game's black cover, on a canvas of its own, would hide the list: it stands on that canvas.
        private void Stand(TravelList list)
        {
            bool cover = UiFader.isFading;
            if (cover == _onCover && !cover)
            {
                return;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            Canvas canvas = cover && uis != null && uis.blackOverlay != null ? uis.blackOverlay.canvas : null;
            list.MoveTo(canvas != null ? canvas.rootCanvas.transform : null);
            _onCover = canvas != null;
        }

        public void Close()
        {
            _answer = null;
            if (!_open)
            {
                return;
            }
            _open = false;
            _window.Close();
        }

        public void Dispose()
        {
            Close();
            _window.Destroy(e => JusticeDirector.Logger?.Error(e));
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Pick(int choice)
        {
            if (!_open || choice < 0 || choice >= _count || Time.unscaledTime - _shownAt < InputDelay)
            {
                return;
            }
            Action<int> answer = _answer;
            if (answer == null)
            {
                Close();
                return;
            }
            _shownAt = Time.unscaledTime;   // whatever the answer shows next gets its own delay
            try
            {
                answer(choice);
            }
            catch (Exception e)
            {
                // A failed answer must not leave its page up with the player held behind it.
                Close();
                if (JusticeDirector.Instance != null)
                {
                    JusticeDirector.Instance.PanelFault(e);
                }
            }
        }

        // The list was closed: by Close, nothing more to do; by the cancel key (the Legal file only), the file is closed too.
        private void Closed()
        {
            if (_onCover)
            {
                _onCover = false;
                _window.Take(this)?.MoveTo(null);
            }
            JusticeDirector director = JusticeDirector.Instance;
            if (_open && _answer != null && director != null && director.FileIsOpen)
            {
                _open = false;
                _answer = null;
                director.CloseFile();
            }
        }
    }
}
