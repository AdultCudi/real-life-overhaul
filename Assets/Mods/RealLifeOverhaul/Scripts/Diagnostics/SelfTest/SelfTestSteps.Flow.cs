using System.Collections;
using BigAmbitions.DayNightCycle;
using Timemachine;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The step table's flow part: its steps, each a nested class (see SelfTestSteps.cs).</summary>
    internal static partial class SelfTestSteps
    {
        /// <summary>Waiting for the menu, the city and the clock.</summary>
        internal sealed class WaitMenuStep : SelfTestStep
        {
            public override string Name => "wait_menu";

            public override IEnumerator Run(SelfTestContext c)
            {
                while (!SelfTestGame.MenuUp())
                {
                    c.Waiting = "waiting for the main menu to stand";
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                c.Pass("the main menu stands");
            }
        }

        /// <summary>The "wait_city" step (SelfTestKinds has its words).</summary>
        internal sealed class WaitCityStep : SelfTestStep
        {
            public override string Name => "wait_city";

            public override IEnumerator Run(SelfTestContext c)
            {
                while (!SelfTestGame.CityUp())
                {
                    string chain = "the mod's load chain has not finished (state read " + ModState.Loaded + ", first slices played " + DayWarm.Ready + ")";
                c.Waiting = "waiting for a city: " + (SaveGameManager.Current == null ? "no game is loaded" : UI.Load.LoadScene.isLoading ? "the loading screen is up" : chain);
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                c.Waiting = "the settle seconds after the city stood";
                yield return new WaitForSecondsRealtime(c.Number("settle", 3f));
                c.Pass("the city stands and the mod's load chain has finished: " + SelfTestGame.CityWords());
            }
        }

        /// <summary>The "wait_seconds" step (SelfTestKinds has its words).</summary>
        internal sealed class WaitSecondsStep : SelfTestStep
        {
            public override string Name => "wait_seconds";

            public override IEnumerator Run(SelfTestContext c)
            {
                c.Waiting = "waiting " + c.Number("seconds", 0f) + " s";
                yield return new WaitForSecondsRealtime(c.Number("seconds", 0f));
                c.Pass("waited " + c.Number("seconds", 0f) + " s");
            }
        }

        /// <summary>The "wait_minutes" step (SelfTestKinds has its words).</summary>
        internal sealed class WaitMinutesStep : SelfTestStep
        {
            public override string Name => "wait_minutes";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands, so the game's clock is not running");
                    yield break;
                }
                float start = SelfTestGame.Minutes(), want = c.Number("minutes", 0f);
                float lastMoved = Time.realtimeSinceStartup, last = start;
                while (SelfTestGame.Minutes() - start < want)
                {
                    float now = SelfTestGame.Minutes();
                    if (now > last)
                    {
                        last = now;
                        lastMoved = Time.realtimeSinceStartup;
                    }
                    else if (Time.realtimeSinceStartup - lastMoved > 20f)
                    {
                        c.Fail("the game's clock did not move for 20 s (paused, or a screen holds the game) at " + SelfTestGame.CityWords());
                        yield break;
                    }
                    c.Waiting = "waiting for game minutes: " + (int)(SelfTestGame.Minutes() - start) + " of " + want;
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                c.Pass("waited " + want + " game minutes: " + SelfTestGame.CityWords());
            }
        }

        /// <summary>The "skip_time" step (SelfTestKinds has its words).</summary>
        internal sealed class SkipTimeStep : SelfTestStep
        {
            public override string Name => "skip_time";

            public override IEnumerator Run(SelfTestContext c)
            {
                string span = c.Arg("span");
                if (!SelfTestRules.SpanOk(span))
                {
                    c.Fail("'" + span + "' is not a span (use d, h and m: 1d, 3h, 2d5h30m)");
                    yield break;
                }
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: the time machine needs a running game");
                    yield break;
                }
                string before = SelfTestGame.CityWords();
                if (!SelfTestTime.Skip(span))
                {
                    c.Fail("the game's time machine would not start for '" + span + "'");
                    yield break;
                }
                while (SelfTestTime.Running())
                {
                    c.Waiting = "the game's time machine to finish skipping " + span;
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                yield return new WaitForSecondsRealtime(1.5f);   // its end: the screens it hid come back, the midnight's work runs
                c.Pass("skipped " + span + ": " + before + " to " + SelfTestGame.CityWords());
            }
        }

        /// <summary>The "set_clock" step (SelfTestKinds has its words).</summary>
        internal sealed class SetClockStep : SelfTestStep
        {
            public override string Name => "set_clock";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: the time machine needs a running game");
                    yield break;
                }
                GameInstance save = SaveGameManager.Current;
                int hour = (int)c.Number("hour", 8f);
                float minute = c.Number("minute", 0f);
                int day = (int)c.Number("day", save.Day);
                if (!c.Spec.Args.ContainsKey("day") && (hour * 60f + minute) <= (save.Hour * 60f + save.Minute))
                {
                    day++;   // the time of day has passed: the next day's
                }
                float goal = (day * 24 + hour) * 60f + minute;
                if (goal <= SelfTestGame.Minutes())
                {
                    c.Fail("the game's clock only goes forward: day " + day + " " + hour + ":" + ((int)minute).ToString("00") + " is not after now");
                    yield break;
                }
                string before = SelfTestGame.CityWords();
                if (!SelfTestTime.SkipTo(day, hour, minute))
                {
                    c.Fail("the game's time machine would not start");
                    yield break;
                }
                while (SelfTestTime.Running())
                {
                    c.Waiting = "the game's time machine to reach day " + day + " " + hour + ":" + ((int)minute).ToString("00");
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                yield return new WaitForSecondsRealtime(1.5f);
                c.Pass("the clock set forward: " + before + " to " + SelfTestGame.CityWords());
            }
        }

        /// <summary>The "note" step (SelfTestKinds has its words).</summary>
        internal sealed class NoteStep : SelfTestStep
        {
            public override string Name => "note";

            public override IEnumerator Run(SelfTestContext c)
            {
                c.Pass(c.Arg("text"));
                yield break;
            }
        }
    }
}
