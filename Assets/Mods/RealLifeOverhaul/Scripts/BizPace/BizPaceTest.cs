using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Streets;
using UI.Notification;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.BizPace
{
    /// <summary>
    /// Options > Tests > Check BizMan's speed: the game's own BizMan opened on the player's businesses in turn (the view switched on afresh
    /// for one, straight on from the last for the next, so there are opens and switches) and every tab of each clicked, twice. The first
    /// walk lets the mod's code compile (Mono compiles a method when it is first called) and is only logged; the second is judged. What
    /// the mod's handlers added to each step is counted by <see cref="BizPaceWatch"/> and put against the owner's budgets
    /// (<see cref="BizPaceRules"/>); the work they leave for the next ticks is counted after a pause. One line says it all, with the slowest
    /// handler named, and a notice says PASS or FAIL. The test changes nothing in the save. Costs nothing unless it is run; the walk is
    /// spread over frames, one move at a time.
    /// </summary>
    public static class BizPaceTest
    {
        private const int Most = 12;               // businesses walked at most
        private const int Settle = BizPaceRules.Window + 1;   // frames after each move: past the window of its episode
        private const float Pause = 1.5f;          // real seconds the next ticks are given after the walk
        private const double AfterBudgetMs = 2.0;  // what the walk may leave the mod's timers to do after it, in all
        private static bool _running;

        /// <summary>Starts the test; a second press while it runs does nothing.</summary>
        public static void Run()
        {
            try
            {
                if (_running)
                {
                    return;
                }
                if (!ModState.Loaded || SaveGameManager.Current == null || BizPaceGame.App() == null)
                {
                    TestRefusals.TellKey(BizPaceWatch.Logger, "BizMan speed", "reallife_perf_test_bizman_none");
                    return;
                }
                if (BizPaceGame.Unsaved())
                {
                    TestRefusals.TellKey(BizPaceWatch.Logger, "BizMan speed", "reallife_perf_test_bizman_unsaved");
                    return;
                }
                _running = true;
                Meta.MetaUi.Instance.Run(Routine());
            }
            catch (Exception e)
            {
                _running = false;
                BizPaceWatch.Logger?.Error(e);
            }
        }

        private static IEnumerator Routine()
        {
            var places = new List<Address>(Most);
            var buttons = new List<Button>(24);
            var first = new BizPaceBook();
            var judged = new BizPaceBook();
            string stopped = null;
            bool armed = false;
            double after = 0.0, afterHandlerMs = 0.0;
            string afterHandler = null;
            try
            {
                BizPaceGame.Businesses(places, Most);
                if (places.Count == 0)
                {
                    TestRefusals.TellKey(BizPaceWatch.Logger, "BizMan speed", "reallife_perf_test_bizman_none");
                    yield break;
                }
                BizPaceWatch.Arm(true);
                armed = true;
                for (int pass = 0; pass < 2 && stopped == null; pass++)
                {
                    BizPaceWatch.BeginWalk(pass == 0 ? first : judged);
                    for (int i = 0; i < places.Count && stopped == null; i++)
                    {
                        Address place = places[i];
                        if (!Do(() => BizPaceGame.OpenOn(place, i % 2 == 0)))
                        {
                            stopped = "BizMan would not open";
                            break;
                        }
                        for (int f = 0; f < Settle; f++)
                        {
                            yield return null;
                            BizPaceWatch.Frame(Time.frameCount);
                        }
                        BizPaceGame.Tabs(buttons);
                        for (int t = 0; t < buttons.Count && stopped == null; t++)
                        {
                            if (BizPaceGame.Unsaved())
                            {
                                stopped = "the Settings tab holds changes not saved";
                                break;
                            }
                            Button tab = buttons[t];
                            if (tab == null || !Do(() => tab.onClick.Invoke()))
                            {
                                continue;
                            }
                            for (int f = 0; f < Settle; f++)
                            {
                                yield return null;
                                BizPaceWatch.Frame(Time.frameCount);
                            }
                        }
                    }
                }
                float until = Time.realtimeSinceStartup + Pause;
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    BizPaceWatch.Frame(Time.frameCount);
                }
                after = BizPaceWatch.AfterMs(out afterHandler, out afterHandlerMs);
            }
            finally
            {
                _running = false;
                if (armed)
                {
                    BizPaceWatch.Walk = null;
                    BizPaceWatch.Arm(false);
                }
                Do(BizPaceGame.CloseMenu);
            }
            Report(places.Count, first, judged, after, afterHandler, afterHandlerMs, stopped);
        }

        private static bool Do(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                BizPaceWatch.Logger?.Error(e);
                return false;
            }
        }

        // One line for the walk, and the notice.
        private static void Report(int businesses, BizPaceBook first, BizPaceBook judged, double after, string afterHandler, double afterHandlerMs, string stopped)
        {
            var line = new StringBuilder(480);
            line.Append("BizMan check (the walk through ").Append(businesses).Append(" business(es)): first walk, the code compiling").Append(Said(first)).Append("; judged walk").Append(Said(judged))
                .Append("; the work left for the next ticks ").Append(BizPaceRules.Ms(after)).Append(" ms")
                .Append(afterHandler != null ? " (" + afterHandler + " " + BizPaceRules.Ms(afterHandlerMs) + " ms)" : string.Empty)
                .Append(", wanted under ").Append(BizPaceRules.Ms(AfterBudgetMs)).Append(" ms");
            string reason = null;
            bool ok = stopped == null && BizPaceRules.Passes(judged.WorstMs, judged.Count, businesses >= 2, out reason);
            if (ok && after > AfterBudgetMs)
            {
                ok = false;
                reason = "the next ticks were left " + BizPaceRules.Ms(after) + " ms of the mod's work";
            }
            if (stopped != null)
            {
                reason = stopped;
            }
            line.Append(": ").Append(ok ? "PASS." : "FAIL (" + reason + ").");
            BizPaceWatch.Logger?.Info(line.ToString());
            if (stopped != null)
            {
                TestRefusals.TellKey(BizPaceWatch.Logger, "BizMan speed", "reallife_perf_test_bizman_unsaved", stopped);
                return;
            }
            var data = new Dictionary<string, string>(5)
            {
                { "open", BizPaceRules.Ms(judged.WorstMs[(int)BizStep.Open]) },
                { "switch", BizPaceRules.Ms(judged.WorstMs[(int)BizStep.Switch]) },
                { "tab", BizPaceRules.Ms(judged.WorstMs[(int)BizStep.Tab]) },
                { "count", businesses.ToString(CultureInfo.InvariantCulture) },
                { "handler", judged.Slowest ?? "-" }
            };
            Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_perf_test_bizman_pass" : "reallife_perf_test_bizman_fail", data, 12f, null, null, true, false);
        }

        // ": 3 opens (the mod added 0.40 ms at most, wanted under 2.00), ..." for a book; its slowest handler named.
        private static string Said(BizPaceBook book)
        {
            var text = new StringBuilder(160);
            book.Append(text);
            return text.Length > 0 ? text.ToString().Replace("; BizMan:", ":") : ": no step was made";
        }
    }
}
