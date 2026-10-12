using System;
using System.Collections.Generic;
using Extensions;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A pool of written-test questions. Each question is one line in en.json: the question,
    /// the right answer and two wrong ones, separated by '|'. The answers are shuffled when
    /// shown. <see cref="Groups"/> says where each subject starts in <see cref="Keys"/>, so a
    /// paper can take something from every subject.
    /// </summary>
    internal sealed class QuestionPool
    {
        public readonly string[] Keys;
        public readonly int[] Groups;                               // index of the first question of each subject
        public readonly HashSet<int> Seen = new HashSet<int>();     // asked this session: not asked again until the pool runs low

        public QuestionPool(string[] keys, int[] groups)
        {
            Keys = keys;
            Groups = groups;
        }
    }

    /// <summary>Keeps the player on the spot with one of the game's own navigation blockers (a value of our own, so nobody else's is ever lifted).</summary>
    internal static class LicenceHold
    {
        private const NavigationBlocker Blocker = (NavigationBlocker)7301;

        private static PlayerController _held;

        public static bool Holding => _held != null;

        public static void Hold(PlayerController player)
        {
            if (player == null || ReferenceEquals(_held, player))
            {
                return;
            }
            Free();
            _held = player;
            player.ResetNavigation();
            player.SetNavigationBlocker(Blocker);
        }

        public static void Free()
        {
            PlayerController player = _held;
            _held = null;
            try
            {
                if (player != null)
                {
                    player.UnsetNavigationBlocker(Blocker);
                }
            }
            catch (Exception e)
            {
                Licences.Logger?.Error(e);
            }
        }
    }


    /// <summary>
    /// A written test as a question card in the game's own list window (<see cref="TravelList"/>): the question and
    /// "Question 4 of 12" under the headline, an entry for each of the three answers, and "Give up the test". So many
    /// questions from a pool; no question comes twice in a paper, a paper takes at least one from every subject, and
    /// questions asked earlier in the session wait until the pool runs low. The list cannot be closed with the cancel key
    /// until the paper is answered or given up (<see cref="TravelList.Locked"/>), and the player stands still for it.
    /// An answer is a notification ("Correct." or "Wrong.") and the next question follows at once; the last one ends in a
    /// page with the score and "Close". Giving up counts as a failed paper.
    ///
    /// Nothing runs by itself: a click on an entry does it all. If the game's window cannot be copied a paper does not
    /// start (the director returns the fee).
    /// </summary>
    internal sealed class TheoryTest
    {
        // The questions about money name today's fines (Core/Prices): the fine as charged, and the wrong answers a tenth and ten times it.
        private static string Figures(string text)
        {
            if (text.IndexOf('{') < 0)
            {
                return text;
            }
            return Fine(Fine(Fine(text, "redlight", CrimeLaw.RedLightFine), "speeding", CrimeLaw.SpeedingFine), "injury", PolicePursuit.InjuryFine);
        }

        private static string Fine(string text, string name, int fine)
        {
            return text.Replace("{" + name + "}", ((float)fine).ToCurrencyFormat())
                .Replace("{" + name + "_low}", PriceRules.Round(fine / 10f).ToCurrencyFormat())
                .Replace("{" + name + "_high}", PriceRules.Round(fine * 10f).ToCurrencyFormat());
        }

        private const int GiveUp = 3;
        private const string ButtonAnswer = "reallife_lic_list_answer";
        private const string ButtonDo = "reallife_lic_list_do";

        private readonly SchoolScreens _screens;
        private readonly List<int> _paper = new List<int>(16);
        private readonly string[] _options = new string[3];
        private readonly int[] _order = new int[3];
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private readonly Action _onReportClosed;

        private QuestionPool _pool;
        private string _titleKey;
        private Action<bool, int, int> _done;   // passed, score, of
        private int _passMark;
        private int _index;
        private int _score;
        private bool _finished;
        private bool _leaving;                  // closed by us: not a loss
        private string _question;

        public TheoryTest(SchoolScreens screens)
        {
            _screens = screens;
            _onPick = Picked;
            _onClosed = Closed;
            _onReportClosed = ReportClosed;
        }

        public bool Active { get; private set; }

        /// <summary>True when the list was taken from under an unfinished paper (the street went away, a fault): the director aborts it and returns the fee.</summary>
        public bool Lost { get; private set; }

        /// <summary>Starts a paper. False (and nothing changed) if the pool is too small, the player cannot be held or the game's list cannot be made.</summary>
        public bool Begin(QuestionPool pool, int questions, int passMark, string titleKey, Action<bool, int, int> done)
        {
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            if (Active || pool == null || pool.Keys.Length < questions || player == null || _screens.Failed)
            {
                return false;
            }
            _pool = pool;
            _titleKey = titleKey;
            _done = done;
            _passMark = passMark;
            _index = 0;
            _score = 0;
            _finished = false;
            _leaving = false;
            Lost = false;
            Draw(pool, questions);
            LicenceHold.Hold(player);
            Active = true;
            if (!Show())
            {
                Active = false;
                LicenceHold.Free();
                _done = null;
                return false;
            }
            Licences.Logger?.Info("Licences: a written test of " + questions + " questions started (" + titleKey + ").");
            return true;
        }

        // One from every subject first, then whatever is left, never one twice and unseen ones before seen ones.
        private void Draw(QuestionPool pool, int questions)
        {
            _paper.Clear();
            if (pool.Keys.Length - pool.Seen.Count < questions)
            {
                pool.Seen.Clear();
            }
            for (int g = 0; g < pool.Groups.Length && _paper.Count < questions; g++)
            {
                int from = pool.Groups[g];
                int to = g + 1 < pool.Groups.Length ? pool.Groups[g + 1] : pool.Keys.Length;
                Pick(pool, from, to);
            }
            int guard = 0;
            while (_paper.Count < questions && guard++ < 400)
            {
                Pick(pool, 0, pool.Keys.Length);
            }
            // The order of the paper is not the order of the subjects.
            for (int i = _paper.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int swap = _paper[i];
                _paper[i] = _paper[j];
                _paper[j] = swap;
            }
        }

        private void Pick(QuestionPool pool, int from, int to)
        {
            int span = to - from;
            int start = Random.Range(0, span);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < span; i++)
                {
                    int index = from + (start + i) % span;
                    if (_paper.Contains(index) || (pass == 0 && pool.Seen.Contains(index)))
                    {
                        continue;
                    }
                    _paper.Add(index);
                    pool.Seen.Add(index);
                    return;
                }
            }
        }

        // The question on the card, its answers in a new order. False if the game's window cannot be made.
        private bool Show()
        {
            TravelList list = _screens.Window.Take(this);
            if (list == null)
            {
                return false;
            }
            string[] parts = Figures(Licences.T(_pool.Keys[_paper[_index]])).Split('|');
            _question = parts[0];
            for (int i = 0; i < 3; i++)
            {
                _order[i] = i;
            }
            for (int i = 2; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int swap = _order[i];
                _order[i] = _order[j];
                _order[j] = swap;
            }
            float began = Time.realtimeSinceStartup;
            bool opening = !list.IsOpen;
            string progress = Licences.T("reallife_lic_theory_progress", "number", (_index + 1).ToString(), "of", _paper.Count.ToString());
            list.Begin(Licences.T(_titleKey), _question + "\n<size=80%>" + progress + "</size>");
            for (int i = 0; i < 3; i++)
            {
                int part = _order[i] + 1;
                _options[i] = part < parts.Length ? parts[part] : "-";
                list.Add(i, Licences.T("reallife_lic_theory_answer", "number", (i + 1).ToString()), _options[i], ButtonAnswer, true);
            }
            list.Add(GiveUp, Licences.T("reallife_lic_theory_give_up"), Licences.T("reallife_lic_theory_give_up_detail"), ButtonDo, true);
            list.Open(_onPick, _onClosed);
            list.Locked = true;
            if (Meta.Dev.Verbose)
            {
                Licences.Logger?.Info("Licences check (theory): question " + (_index + 1) + " of " + _paper.Count + ", " + (opening ? "the game's list is open, " : string.Empty) + list.Made
                                      + " entries made so far; " + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms: PASS.");
            }
            return true;
        }

        private void Picked(int id)
        {
            try
            {
                if (!Active || _finished)
                {
                    return;
                }
                if (id == GiveUp)
                {
                    GaveUp();
                    return;
                }
                if (id < 0 || id > 2)
                {
                    return;
                }
                bool right = _order[id] == 0;   // the right answer is always the first one in the line
                if (right)
                {
                    _score++;
                }
                Licences.Toast(right ? UI.Notification.NotificationType.Success : UI.Notification.NotificationType.Warning,
                    right ? "reallife_lic_theory_right" : "reallife_lic_theory_wrong", 2.5f);
                _index++;
                if (_index >= _paper.Count)
                {
                    Finish();
                }
                else if (!Show())
                {
                    Lost = true;
                }
            }
            catch (Exception e)
            {
                Licences.Logger?.Error(e);
                Lost = true;
            }
        }

        // The paper is marked as it stands: failed, and it can be sat again from tomorrow like any failed paper.
        private void GaveUp()
        {
            _finished = true;
            Licences.Logger?.Info("Licences check (theory): given up at question " + (_index + 1) + " of " + _paper.Count + " with " + _score + " right: counted as failed.");
            Licences.Toast(UI.Notification.NotificationType.Warning, "reallife_lic_theory_gave_up", 9f);
            Action<bool, int, int> done = _done;
            _done = null;
            Close();
            done?.Invoke(false, _score, _paper.Count);
        }

        private void Finish()
        {
            _finished = true;
            bool passed = _score >= _passMark;
            string body = Licences.T("reallife_lic_theory_result", "score", _score.ToString(), "of", _paper.Count.ToString()).Replace("{pass}", _passMark.ToString());
            string title = Licences.T(passed ? "reallife_lic_theory_passed" : "reallife_lic_theory_failed");
            Licences.Toast(passed ? UI.Notification.NotificationType.Success : UI.Notification.NotificationType.Warning,
                passed ? "reallife_lic_theory_passed" : "reallife_lic_theory_failed", 9f);
            if (!_screens.Report(this, title, Licences.T("reallife_lic_theory_score", "score", _score.ToString(), "of", _paper.Count.ToString()), body, _onReportClosed))
            {
                Close();
            }
            Licences.Logger?.Info("Licences: written test finished, " + _score + " of " + _paper.Count + " (" + (passed ? "passed" : "failed") + ").");
            Action<bool, int, int> done = _done;
            _done = null;
            done?.Invoke(passed, _score, _paper.Count);
        }

        private void ReportClosed()
        {
            if (Active && _finished)
            {
                Close();
            }
        }

        // The list was closed: on the score page that is the end of the test; on a question it was taken from under the paper.
        private void Closed()
        {
            if (!Active || _leaving)
            {
                return;
            }
            if (_finished)
            {
                Close();
            }
            else
            {
                Lost = true;
            }
        }

        // The score page's Close button comes through the shared report page: its own closing is not ours, so the page is watched here.
        private void Close()
        {
            _leaving = true;
            Active = false;
            LicenceHold.Free();
            if (_screens.Window.OpenFor(this))
            {
                _screens.Window.Close();
            }
            _leaving = false;
        }

        /// <summary>Stopped from outside (the street went away, a fault). Nothing is marked; the caller says what happens to the fee.</summary>
        /// <returns>True if a paper was on and had not yet been marked.</returns>
        public bool Abort()
        {
            Lost = false;
            if (!Active)
            {
                return false;
            }
            bool unmarked = !_finished;
            _done = null;
            Close();
            return unmarked;
        }
    }
}
