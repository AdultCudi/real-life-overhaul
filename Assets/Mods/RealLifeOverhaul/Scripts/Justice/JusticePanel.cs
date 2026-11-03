using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
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

    /// <summary>A line under an answer: what it costs or brings, good for the player (+1) or a cost (-1).</summary>
    public struct JusticeLine
    {
        public string Text;
        public int Tone;
    }

    /// <summary>One page of the justice system's screen: who is there, who speaks, the text and up to three answers.</summary>
    public sealed class JusticePage
    {
        public JusticeScene Scene;
        public string Title;
        public string Subtitle;
        public JusticeFace Left, Middle, Right;
        public string LeftName, MiddleName, RightName;
        public string LeftRole, MiddleRole, RightRole;   // who each is where their place's word is not it; null for that word
        public bool Defendant;                           // the player in the dock, among those present
        public int Speaker = -1;        // 0 left, 1 middle, 2 right; -1 nobody
        public string SpeakerName;
        public string Body;
        public UiLedgerLine[] Statement;   // on the RLO interface: the body's figures as a statement, its words over it and its foot under it; null for none
        public string StatementWords, StatementFoot;
        public readonly string[] Choices = new string[JusticePanel.MostChoices];
        public readonly string[] Hints = new string[JusticePanel.MostChoices];
        public readonly string[] Times = new string[JusticePanel.MostChoices];   // the time an answer costs ("3 days"); null for none
        public readonly List<JusticeLine>[] Lines = new List<JusticeLine>[JusticePanel.MostChoices];
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

        /// <summary>The time the answer added last costs, beside its name.</summary>
        public JusticePage Takes(string time)
        {
            if (Count > 0 && !string.IsNullOrEmpty(time))
            {
                Times[Count - 1] = time;
            }
            return this;
        }

        /// <summary>A line under the answer added last: what it costs or brings, in its colour (UiConsequence.Loss for money paid out).</summary>
        public JusticePage Line(string text, int tone)
        {
            if (Count > 0 && !string.IsNullOrEmpty(text))
            {
                List<JusticeLine> lines = Lines[Count - 1] ?? (Lines[Count - 1] = new List<JusticeLine>(3));
                lines.Add(new JusticeLine { Text = text, Tone = tone });
            }
            return this;
        }
    }

    /// <summary>
    /// The justice system's one screen, a copy of the game's station list (on the RLO interface the decision card: those
    /// present, who speaks, the answers numbered with what each means). A page is written when shown; nothing runs per frame.
    /// </summary>
    public sealed class JusticePanel
    {
        public const int MostChoices = 3;

        private const float InputDelay = 0.4f;    // seconds after a page appears before an answer counts: no double presses
        private const string ButtonChoose = "reallife_justice_list_choose";

        private static JusticePanel _instance;

        private readonly SceneDesk _window = new SceneDesk("RealLifeOverhaul Justice", 1250f, 150f);
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
            SceneList list = _window.Take(this);
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
            string you = page.Defendant ? JusticeText.T("reallife_justice_court_you") : null;
            list.Moment(page.Subtitle, Emblem(page.Scene), Present(page.Left, page.LeftName), Present(page.Middle, page.MiddleName), Present(page.Right, page.RightName), you);
            list.Roles(new[]
                {
                    Role(page.Left, page.LeftRole), Role(page.Middle, page.MiddleRole), Role(page.Right, page.RightRole),
                    page.Defendant ? JusticeText.T("reallife_justice_role_defendant") : null
                },
                new[] { Ring(page.Left), Ring(page.Middle), Ring(page.Right), page.Defendant ? UiRole.Accent : UiRole.None });
            string body = page.Body ?? string.Empty;
            string speaker = page.SpeakerName ?? string.Empty;
            int lines = 1;
            for (int i = 0; i < body.Length; i++)
            {
                lines += body[i] == '\n' ? 1 : 0;
            }
            list.AddNote(speaker, body, list.HeightFor((int)((body.Length + 70 * lines) * 1.3f), false));
            if (page.Statement != null)
            {
                list.Statement(page.StatementWords, page.Statement, page.StatementFoot);
            }
            for (int i = 0; i < _count; i++)
            {
                string hint = page.Hints[i];
                UiConsequence[] costs = Lines(page.Lines[i]);
                string said = SceneViews.Joined(hint, costs, page.Times[i]);   // as the game's own list says it, with what it costs
                list.Add(i, page.Choices[i] ?? string.Empty, hint, ButtonChoose, true,
                    string.IsNullOrEmpty(said) ? 0f : list.HeightFor(said.Length, true), costs, page.Times[i]);
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

        // The page's place as the kit's card shows it: the court's scales, the cell's key, the Legal file's case.
        private static UiIcon Emblem(JusticeScene scene) => scene == JusticeScene.Court ? UiIcon.Scales : scene == JusticeScene.Cell ? UiIcon.Key : UiIcon.Briefcase;

        // One of those present on the page, by name; nobody where the place stands empty.
        private static string Present(JusticeFace face, string name) => face == JusticeFace.None ? null : name;

        // Who one of those present is, under their name on the RLO interface's card: the page's word for them, or their place's.
        private static string Role(JusticeFace face, string own)
        {
            if (face == JusticeFace.None)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(own))
            {
                return own;
            }
            switch (face)
            {
                case JusticeFace.Judge: return JusticeText.T("reallife_justice_role_judge");
                case JusticeFace.Prosecutor: return JusticeText.T("reallife_justice_role_prosecutor");
                case JusticeFace.Counsel: return JusticeText.T("reallife_justice_role_counsel");
                case JusticeFace.Officer: return JusticeText.T("reallife_justice_role_officer");
                case JusticeFace.Inmate: return JusticeText.T("reallife_justice_role_inmate");
                case JusticeFace.Visitor: return JusticeText.T("reallife_justice_role_visitor");
                default: return JusticeText.T("reallife_justice_role_clerk");
            }
        }

        // Whose side one of those present is on, as their ring: the case against the player, the court, the player's own lawyer.
        private static UiRole Ring(JusticeFace face) =>
            face == JusticeFace.Prosecutor ? UiRole.Danger : face == JusticeFace.Judge ? UiRole.Warn : face == JusticeFace.Counsel ? UiRole.Ok : UiRole.None;

        private static UiConsequence[] Lines(List<JusticeLine> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return null;
            }
            var made = new UiConsequence[lines.Count];
            for (int i = 0; i < made.Length; i++)
            {
                int tone = lines[i].Tone;
                made[i] = new UiConsequence
                {
                    Text = lines[i].Text,
                    Tone = tone <= UiConsequence.Loss ? UiConsequence.Loss : Math.Sign(tone),   // money out stays red; the rest good, against or neither
                    Change = tone <= UiConsequence.Loss ? -1 : 0
                };
            }
            return made;
        }

        private void AddName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                _info.Append(_info.Length > 0 ? "\n" : string.Empty).Append(name);
            }
        }

        // While the screen is faded the game's black cover, on a canvas of its own, would hide the list: it stands on that canvas.
        private void Stand(SceneList list)
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
