using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace RealLifeOverhaul.Gfx
{
    /// <summary>
    /// Options, Tests: what only the game can show. The cost of each cost setting measured on this computer, where the
    /// player stands (the game's own picture and the look in turn, each for some seconds, the engine's own frame timing
    /// read, one PASS or FAIL line for each setting against its budget); every hour of the day shown in turn, by the
    /// game's own clock tool, with the save's clock left alone; and one line saying what is on the picture now.
    /// COST: nothing unless a button is pressed; a measurement's windows are the performance sampler's, which allocates
    /// nothing while one is open.
    /// </summary>
    public sealed partial class GraphicsDirector
    {
        private const float ArmSeconds = 2f;        // seconds with no menu up before the measurement begins
        private const float ArmLook = 0.25f;        // seconds between looks at whether the menus are shut
        private const float ArmMost = 90f;          // seconds the measurement waits for the menus before it gives up

        private PerfSampler _sampler;
        private Coroutine _measuring, _hours;
        private readonly float[] _card = new float[GraphicsRules.CostPairs * 2 + 1];    // each window's time on the graphics card, ms
        private readonly float[] _frame = new float[GraphicsRules.CostPairs * 2 + 1];   // each window's median frame, ms

        /// <summary>Options, Tests: measures what each cost setting adds to a frame here, and writes a line for each to Player.log. A second press stops it.</summary>
        public void TestCost()
        {
            try
            {
                StartCost();
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void StartCost()
        {
            if (_measuring != null)
            {
                EndMeasure("stopped by a second press");
                GraphicsGame.Tell("reallife_gfx_test_cost_stopped", 8f);
                return;
            }
            if (_broken || !GraphicsGame.Ready || _hours != null)
            {
                GraphicsGame.Tell("reallife_gfx_test_refused", 8f, true);
                return;
            }
            GraphicsGame.Tell("reallife_gfx_test_cost_armed", 8f);
            _measuring = StartCoroutine(Guard.Routine(Measure()));
        }

        /// <summary>Options, Tests: shows every hour of the day in turn, the game's light and the look together, and puts the hour back. A second press stops it.</summary>
        public void TestHours()
        {
            try
            {
                StartHours();
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void StartHours()
        {
            if (_hours != null)
            {
                EndHours();
                GraphicsGame.Tell("reallife_gfx_test_hours_done", 8f);
                return;
            }
            // The game's clock tool also sets how many cars and people the hour has: in a shared city those are the session's.
            if (_broken || !GraphicsGame.Ready || _measuring != null || GraphicsGame.InSession || GraphicsGame.MapOpen || _designer)
            {
                GraphicsGame.Tell("reallife_gfx_test_refused", 8f, true);
                return;
            }
            if (!GraphicsGame.HoursBegin())
            {
                GraphicsGame.Tell("reallife_gfx_test_refused", 8f, true);
                return;
            }
            GraphicsGame.Tell("reallife_gfx_test_hours_started", 10f);
            _hours = StartCoroutine(Guard.Routine(Hours()));
        }

        /// <summary>Options, Tests: one line in Player.log saying what is on the picture now, and how a grey card comes out in the game's picture and in the look's.</summary>
        public void TestSay()
        {
            try
            {
                SayNow();
            }
            catch (Exception e)
            {
                Fault(e);
            }
        }

        private void SayNow()
        {
            if (_broken || !GraphicsGame.Ready || _game == null)
            {
                GraphicsGame.Tell("reallife_gfx_test_refused", 8f, true);
                return;
            }
            Refresh(true);
            GraphicsMoment now = Now(Look);
            float grey = GraphicsModel.Grey(now.Rain / (float)GraphicsRules.WeatherSteps, now.Snow / (float)GraphicsRules.WeatherSteps, now.Cover / (float)GraphicsRules.WeatherSteps);
            GraphicsRules.Evaluate(in now, _values, _set);
            CardPair games = GraphicsModel.Game(now.Hour, grey, 0f), looks = GraphicsModel.Look(now.Hour, grey, 0f, _values, _set);
            float change = games.Middle > 0f ? (looks.Middle / games.Middle - 1f) * 100f : 0f;
            Meta.Dev.Always(Logger, "Graphics check (now): look " + GraphicsRules.LookName(now.Look) + ", cost " + GraphicsRules.CostName(now.Cost) + ", "
                                    + (now.Place == GraphicsPlace.Indoors ? "indoors" : "the city") + ", " + Clock(now.Hour) + ", rain " + now.Rain + "/20, snow " + now.Snow + "/20 falling and "
                                    + now.Cover + "/20 lying, " + (_shown ? "the look is on the picture" : "the picture is the game's own") + "; " + _game.Describe()
                                    + "; a grey card on open ground by the rules: the game's " + (games.Middle * 255f).ToString("0", Inv) + " of 255, the look's "
                                    + (looks.Middle * 255f).ToString("0", Inv) + " (" + change.ToString("+0.0;-0.0", Inv) + "%, wanted within " + GraphicsRules.GreyTolerance.ToString("0", Inv)
                                    + "%): " + (Math.Abs(change) <= GraphicsRules.GreyTolerance ? "PASS" : "FAIL") + ".");
            GraphicsGame.Tell("reallife_gfx_test_said", 8f);
        }

        // Every way out of a test: the measurement's look let go, the game's own hour back. Safe when none runs.
        private void StopTests()
        {
            if (_measuring != null)
            {
                EndMeasure("the city is going or the options were cleaned up");
            }
            if (_hours != null)
            {
                EndHours();
            }
        }

        // ---------------------------------------------------------------- the cost

        private IEnumerator Measure()
        {
            var look = new WaitForSecondsRealtime(ArmLook);
            var settle = new WaitForSecondsRealtime(GraphicsRules.CostSettleSeconds);
            var window = new WaitForSecondsRealtime(GraphicsRules.CostWindowSeconds);
            try
            {
                foreach (object step in MeasureSteps(look, settle, window))
                {
                    yield return step;
                }
            }
            finally
            {
                if (_measuring != null)
                {
                    // Ended by a fault, not by its own end or a stop: the look it held is let go, and the next second's look puts the picture right.
                    _measuring = null;
                    _forcedLook = _forcedCost = -1;
                    if (_sampler != null && _sampler.IsOpen)
                    {
                        _sampler.Close();
                    }
                }
            }
        }

        private IEnumerable MeasureSteps(WaitForSecondsRealtime look, WaitForSecondsRealtime settle, WaitForSecondsRealtime window)
        {
            // The button is on the options page: the measurement begins once the game has run with no menu up for two seconds.
            float clear = 0f, waited = 0f;
            while (clear < ArmSeconds)
            {
                yield return look;
                waited += ArmLook;
                clear = Obstacle() == null ? clear + ArmLook : 0f;
                if (waited > ArmMost)
                {
                    EndMeasure("the menus stayed open");
                    GraphicsGame.Tell("reallife_gfx_test_cost_stopped", 8f);
                    yield break;
                }
            }
            if (_sampler == null)
            {
                _sampler = gameObject.AddComponent<PerfSampler>();   // off outside a window: no cost a frame
            }
            int shown = Look != GraphicsLook.Off ? Look : GraphicsLook.Natural;   // with the option off the Natural look is the one measured
            string cap = GraphicsGame.FrameCap();
            GraphicsGame.Tell("reallife_gfx_test_cost_started", 10f);
            Meta.Dev.Always(Logger, "Graphics check (cost): measuring the " + GraphicsRules.LookName(shown) + " look " + (GraphicsGame.Place == GraphicsPlace.Indoors ? "indoors" : "in the city")
                                    + " at " + GraphicsGame.ScreenWidth + "x" + GraphicsGame.ScreenHeight + ": each cost setting in " + GraphicsRules.CostPairs
                                    + " pairs of windows of " + GraphicsRules.CostWindowSeconds.ToString("0.#", Inv) + " s, the game's own picture either side of each"
                                    + (cap != null ? "; " + cap + ", so only the graphics card's own time is judged" : string.Empty) + ". Stand still.");
            int passed = 0, judged = 0;
            for (int cost = GraphicsCost.Free; cost < GraphicsCost.Count; cost++)
            {
                bool timed = true;
                for (int w = 0; w < _card.Length; w++)
                {
                    _forcedLook = (w & 1) == 1 ? shown : GraphicsLook.Off;
                    _forcedCost = cost;
                    Refresh(true);
                    yield return settle;
                    string stopped = Obstacle();
                    if (stopped == null)
                    {
                        _sampler.Open();
                        yield return window;
                        PerfWindow result = _sampler.Close();
                        _frame[w] = result.MedianMs;
                        _card[w] = result.GpuMs;
                        timed &= result.TimedFrames > 0 && result.GpuMs > 0f;
                        stopped = Obstacle();
                    }
                    if (stopped != null)
                    {
                        EndMeasure(stopped);
                        GraphicsGame.Tell("reallife_gfx_test_cost_stopped", 8f);
                        yield break;
                    }
                }
                int verdict = Verdict(cost, timed, cap);
                judged += verdict != GraphicsRules.NotJudged ? 1 : 0;
                passed += verdict == GraphicsRules.Pass ? 1 : 0;
            }
            _forcedLook = _forcedCost = -1;
            _measuring = null;
            Refresh(true);
            Meta.Dev.Always(Logger, "Graphics check (cost): finished, " + passed + " of " + judged + " cost settings judged are within their budget; the picture is back as the options have it.");
            GraphicsGame.Tell("reallife_gfx_test_cost_done", 10f);
        }

        // One cost setting's line: what it added to the graphics card's frame and to the whole frame, each pair, and the verdict.
        private int Verdict(int cost, bool timed, string cap)
        {
            float budget = GraphicsRules.Budget(cost);
            float card = GraphicsRules.Added(_card, GraphicsRules.CostPairs), frame = GraphicsRules.Added(_frame, GraphicsRules.CostPairs);
            bool byCard = timed;
            int verdict = !byCard && cap != null ? GraphicsRules.NotJudged
                : GraphicsRules.CostVerdict(byCard ? card : frame, GraphicsRules.Spread(byCard ? _card : _frame, GraphicsRules.CostPairs), budget);
            var line = new StringBuilder(320);
            line.Append("Graphics check (cost, ").Append(GraphicsRules.CostName(cost)).Append("): ");
            if (timed)
            {
                line.Append("the graphics card's frame ").Append(card.ToString("+0.00;-0.00", Inv)).Append(" ms (the game's own ").Append(_card[0].ToString("0.00", Inv)).Append(" ms; pairs");
                Pairs(line, _card);
                line.Append("), ");
            }
            else
            {
                line.Append("the engine gave no time for the graphics card, ");
            }
            line.Append("the whole frame ").Append(frame.ToString("+0.00;-0.00", Inv)).Append(" ms (the game's own ").Append(_frame[0].ToString("0.00", Inv)).Append(" ms; pairs");
            Pairs(line, _frame);
            line.Append(cap != null ? "; " + cap : string.Empty).Append("); wanted at most ").Append(budget.ToString("0.00", Inv)).Append(" ms on ")
                .Append(byCard ? "the graphics card" : "the frame").Append(": ");
            line.Append(verdict == GraphicsRules.Pass ? "PASS" : verdict == GraphicsRules.Fail ? "FAIL" : verdict == GraphicsRules.Unsure
                ? "NOT SHOWN (the pairs disagree by more than the difference: stand still and run it again)" : "NOT JUDGED (switch v-sync and the frame cap off and run it again)");
            line.Append('.');
            Meta.Dev.Always(Logger, line.ToString());
            return verdict;
        }

        private static void Pairs(StringBuilder line, float[] windows)
        {
            for (int k = 0; k < GraphicsRules.CostPairs; k++)
            {
                line.Append(' ').Append((windows[k * 2 + 1] - (windows[k * 2] + windows[k * 2 + 2]) * 0.5f).ToString("+0.00;-0.00", Inv));
            }
        }

        // Why the measurement cannot run now, or null. A menu, a pause or another window in front would be measured instead of the picture.
        private string Obstacle()
        {
            return !GraphicsGame.Focused ? "the game is not the window in front" : GraphicsGame.Paused ? "the game is paused" : GraphicsGame.MapOpen ? "the city map is open"
                : _designer ? "the interior designer is open" : ThirdPersonCamera.IsUiOpen() ? "a menu is open" : null;
        }

        private void EndMeasure(string why)
        {
            if (_measuring != null)
            {
                StopCoroutine(_measuring);
                _measuring = null;
            }
            if (_sampler != null && _sampler.IsOpen)
            {
                _sampler.Close();
            }
            _forcedLook = _forcedCost = -1;
            Meta.Dev.Always(Logger, "Graphics check (cost): stopped (" + why + "); the picture is back as the options have it. Lines above this one stand; nothing else was judged.");
            if (!_broken && _game != null && GraphicsGame.Ready)
            {
                Refresh(true);
            }
        }

        // ---------------------------------------------------------------- the hours

        private IEnumerator Hours()
        {
            var wait = new WaitForSecondsRealtime(GraphicsRules.HourShownSeconds);
            int first = Mathf.FloorToInt(GraphicsGame.Hour);
            try
            {
                for (int i = 1; i <= 24; i++)
                {
                    _shownHour = (first + i) % 24;
                    GraphicsGame.HoursShow(_shownHour);
                    Refresh(true);
                    yield return wait;
                    if (GraphicsGame.MapOpen || _designer || !GraphicsGame.Ready)
                    {
                        break;   // the game holds its own light still on the map and in the designer: the hours would not show
                    }
                }
            }
            finally
            {
                if (_hours != null)
                {
                    // Its own end, or a fault: the save's own hour back either way. A stop from outside ends it in EndHours.
                    _hours = null;
                    _shownHour = -1f;
                    GraphicsGame.HoursEnd();
                }
            }
            if (!_broken && _game != null && GraphicsGame.Ready)
            {
                Refresh(true);
            }
            GraphicsGame.Tell("reallife_gfx_test_hours_done", 8f);
        }

        private void EndHours()
        {
            if (_hours != null)
            {
                StopCoroutine(_hours);
                _hours = null;
            }
            _shownHour = -1f;
            GraphicsGame.HoursEnd();
            if (!_broken && _game != null && GraphicsGame.Ready)
            {
                Refresh(true);
            }
        }
    }
}
