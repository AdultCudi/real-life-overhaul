using System;
using System.Collections.Generic;
using System.Globalization;
using Extensions;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Written-test questions, each a locale line "question|right|wrong|wrong", grouped by subject.</summary>
    internal sealed class QuestionPool
    {
        public readonly string[] Keys;
        public readonly int[] Groups;                               // index of the first question of each subject
        public readonly HashSet<int> Seen = new HashSet<int>();     // asked this session: not again until the pool runs low

        public QuestionPool(string[] keys, int[] groups)
        {
            Keys = keys;
            Groups = groups;
        }
    }

    /// <summary>Keeps the player on the spot with one of the game's navigation blockers.</summary>
    internal static class LicenceHold
    {
        private const NavigationBlocker Blocker = (NavigationBlocker)7301;   // our own value, so nobody else's is ever lifted

        private static PlayerController _held;

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
    /// A written test as question cards in the game's list window, locked against the cancel key while the player is
    /// held still. Giving up fails the paper. Nothing per frame: each click does its part.
    /// </summary>
    internal sealed class TheoryTest
    {
        private const int GiveUp = 3;
        private const string ButtonAnswer = "reallife_lic_list_answer";
        private const string ButtonDo = "reallife_lic_list_do";

        private readonly SchoolScreens _screens;
        private readonly List<int> _paper = new List<int>(16);
        private readonly int[] _order = new int[3];
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private readonly Action _onReportClosed;

        private QuestionPool _pool;
        private string _titleKey;
        private Action<bool> _done;
        private int _passMark;
        private int _index;
        private int _score;
        private bool _finished;
        private bool _leaving;        // closed by us: not a loss

        public TheoryTest(SchoolScreens screens)
        {
            _screens = screens;
            _onPick = Picked;
            _onClosed = Closed;
            _onReportClosed = ReportClosed;
        }

        public bool Active { get; private set; }

        /// <summary>True when the list was taken from under an unfinished paper: the director then aborts it.</summary>
        public bool Lost { get; private set; }

        /// <summary>Starts a paper. False, with nothing changed, if it cannot be shown.</summary>
        public bool Begin(QuestionPool pool, int questions, int passMark, string titleKey, Action<bool> done)
        {
            PlayerController player = PlayerHelper.PlayerController;
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

        // One from every subject, then any; unseen ones first. Begin checked the pool is big enough, so each Pick adds one.
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
            while (_paper.Count < questions)
            {
                Pick(pool, 0, pool.Keys.Length);
            }
            // Shuffled, so the subjects do not come in order.
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

        // The question with its answers shuffled. False if the game's window cannot be made.
        private bool Show()
        {
            TravelList list = _screens.Window.Take(this);
            if (list == null)
            {
                return false;
            }
            string[] parts = Figures(Licences.T(_pool.Keys[_paper[_index]])).Split('|');
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
            list.Begin(Licences.T(_titleKey), parts[0] + "\n<size=80%>" + progress + "</size>");
            for (int i = 0; i < 3; i++)
            {
                int part = _order[i] + 1;
                string answer = part < parts.Length ? parts[part] : "-";
                list.Add(i, Licences.T("reallife_lic_theory_answer", "number", (i + 1).ToString()), answer, ButtonAnswer, true);
            }
            list.Add(GiveUp, Licences.T("reallife_lic_theory_give_up"), Licences.T("reallife_lic_theory_give_up_detail"), ButtonDo, true);
            list.Open(_onPick, _onClosed);
            list.Locked = true;
            if (Meta.Dev.Verbose)
            {
                Licences.Logger?.Info("Licences check (theory): question " + (_index + 1) + " of " + _paper.Count + ", " + (opening ? "the game's list is open, " : string.Empty) + list.Made
                                      + " entries made so far; " + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture) + " ms: PASS.");
            }
            return true;
        }

        // Money questions quote today's fines: the fine as charged, and the wrong answers a tenth and ten times it.
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
                bool right = _order[id] == 0;   // the right answer is first in the line
                if (right)
                {
                    _score++;
                }
                Licences.Toast(right ? NotificationType.Success : NotificationType.Warning,
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

        private void GaveUp()
        {
            _finished = true;
            Licences.Logger?.Info("Licences check (theory): given up at question " + (_index + 1) + " of " + _paper.Count + " with " + _score + " right: counted as failed.");
            Licences.Toast(NotificationType.Warning, "reallife_lic_theory_gave_up", 9f);
            Action<bool> done = _done;
            _done = null;
            Close();
            done?.Invoke(false);
        }

        private void Finish()
        {
            _finished = true;
            bool passed = _score >= _passMark;
            string body = Licences.T("reallife_lic_theory_result", "score", _score.ToString(), "of", _paper.Count.ToString()).Replace("{pass}", _passMark.ToString());
            string title = Licences.T(passed ? "reallife_lic_theory_passed" : "reallife_lic_theory_failed");
            Licences.Toast(passed ? NotificationType.Success : NotificationType.Warning,
                passed ? "reallife_lic_theory_passed" : "reallife_lic_theory_failed", 9f);
            if (!_screens.Report(this, title, Licences.T("reallife_lic_theory_score", "score", _score.ToString(), "of", _paper.Count.ToString()), body, _onReportClosed))
            {
                Close();
            }
            Licences.Logger?.Info("Licences: written test finished, " + _score + " of " + _paper.Count + " (" + (passed ? "passed" : "failed") + ").");
            Action<bool> done = _done;
            _done = null;
            done?.Invoke(passed);
        }

        // The score page is the shared report page: its closing comes here, not to Closed.
        private void ReportClosed()
        {
            if (Active && _finished)
            {
                Close();
            }
        }

        // On the score page that ends the test; on a question the list was taken from under the paper.
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

        /// <summary>Stops the paper unmarked. True if one was on and not yet marked: the caller refunds the fee.</summary>
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
