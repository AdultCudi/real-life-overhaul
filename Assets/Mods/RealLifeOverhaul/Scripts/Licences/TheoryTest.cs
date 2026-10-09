using System;
using System.Collections.Generic;
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
    /// A written test on a sheet in the middle of the screen: so many questions from a pool,
    /// three answers each, picked with 1, 2 or 3. No question comes twice in a paper, a paper
    /// takes at least one from every subject, and questions asked earlier in the session wait
    /// until the pool runs low. The player stands still for it. While one is on this reads
    /// three keys a frame; otherwise it does nothing at all.
    /// </summary>
    internal sealed class TheoryTest
    {
        private const float ShowAnswer = 1.6f;    // seconds the picked answer stays lit before the next question
        private const float StartGrace = 0.4f;    // the key that opened the test must not answer its first question

        private readonly List<int> _paper = new List<int>(16);
        private readonly string[] _options = new string[3];
        private readonly int[] _order = new int[3];

        private QuestionPool _pool;
        private string _titleKey;
        private Action<bool, int, int> _done;   // passed, score, of
        private int _passMark;
        private int _index;
        private int _score;
        private int _picked = -1;
        private float _nextAt;
        private float _readyAt;
        private bool _finished;
        private string _question;
        private string _title;

        public bool Active { get; private set; }

        /// <summary>Starts a paper. False (and nothing changed) if the pool is too small or the player cannot be held.</summary>
        public bool Begin(QuestionPool pool, int questions, int passMark, string titleKey, Action<bool, int, int> done)
        {
            PlayerController player = Helpers.PlayerHelper.PlayerController;
            if (Active || pool == null || pool.Keys.Length < questions || player == null)
            {
                return false;
            }
            _pool = pool;
            _titleKey = titleKey;
            _done = done;
            _passMark = passMark;
            _index = 0;
            _score = 0;
            _picked = -1;
            _finished = false;
            Draw(pool, questions);
            LicenceHold.Hold(player);
            Active = true;
            _readyAt = Time.unscaledTime + StartGrace;
            Show();
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

        // The question on the sheet, its answers in a new order.
        private void Show()
        {
            string[] parts = Licences.T(_pool.Keys[_paper[_index]]).Split('|');
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
            for (int i = 0; i < 3; i++)
            {
                int part = _order[i] + 1;
                _options[i] = part < parts.Length ? parts[part] : "-";
            }
            _title = Licences.T(_titleKey, "number", (_index + 1).ToString(), "of", _paper.Count.ToString());
            LicenceHud.Instance.ShowSheet(_title, _question, _options, Licences.T("reallife_lic_theory_foot"));
        }

        /// <summary>Once a frame while a paper is on: three key tests, and a timer after an answer.</summary>
        public void Frame()
        {
            if (!Active)
            {
                return;
            }
            float now = Time.unscaledTime;
            if (_finished)
            {
                if (now >= _readyAt && (JobKeys.InteractDown() || JobKeys.ChoiceDown() >= 0))
                {
                    Close();
                }
                return;
            }
            if (_picked >= 0)
            {
                if (now >= _nextAt)
                {
                    _picked = -1;
                    _index++;
                    if (_index >= _paper.Count)
                    {
                        Finish();
                    }
                    else
                    {
                        Show();
                    }
                }
                return;
            }
            if (now < _readyAt || ThirdPersonCamera.IsUiOpen())
            {
                return;
            }
            int choice = JobKeys.ChoiceDown();
            if (choice < 0)
            {
                return;
            }
            bool right = _order[choice] == 0;   // the right answer is always the first one in the line
            if (right)
            {
                _score++;
            }
            _picked = choice;
            _nextAt = now + ShowAnswer;
            LicenceHud.Instance.ShowSheet(_title, _question, _options,
                Licences.T(right ? "reallife_lic_theory_right" : "reallife_lic_theory_wrong"), choice, right);
        }

        private void Finish()
        {
            _finished = true;
            _readyAt = Time.unscaledTime + 1f;
            bool passed = _score >= _passMark;
            LicenceHud.Instance.ShowSheet(
                Licences.T(passed ? "reallife_lic_theory_passed" : "reallife_lic_theory_failed"),
                Licences.T("reallife_lic_theory_result", "score", _score.ToString(), "of", _paper.Count.ToString()).Replace("{pass}", _passMark.ToString()),
                null, Licences.T("reallife_lic_sheet_close", "key", Keys.Label(KeyAction.Interact)));
            Licences.Logger?.Info("Licences: written test finished, " + _score + " of " + _paper.Count + " (" + (passed ? "passed" : "failed") + ").");
            Action<bool, int, int> done = _done;
            _done = null;
            done?.Invoke(passed, _score, _paper.Count);
        }

        private void Close()
        {
            Active = false;
            LicenceHold.Free();
            if (LicenceHud.Exists)
            {
                LicenceHud.Instance.HideSheet();
            }
        }

        /// <summary>Stopped from outside (the street went away, a fault). Nothing is marked; the caller says what happens to the fee.</summary>
        /// <returns>True if a paper was on and had not yet been marked.</returns>
        public bool Abort()
        {
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
