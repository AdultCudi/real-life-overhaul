using System.Collections;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The step table's log part: what the game's log holds, how long a stretch took, and the game's own monologue.</summary>
    internal static partial class SelfTestSteps
    {
        /// <summary>The "assert_log_has" step (SelfTestKinds has its words).</summary>
        internal sealed class AssertLogHasStep : SelfTestStep
        {
            public override string Name => "assert_log_has";

            public override IEnumerator Run(SelfTestContext c)
            {
                string text = c.Arg("text"), from = c.Arg("from");
                bool absent = c.Flag("absent", false);
                if (from != null && !c.Runner.Watch.Knows(from))
                {
                    c.Fail("no step labelled '" + from + "' has run (name a step's label in from)");
                    yield break;
                }
                float until = Time.realtimeSinceStartup + c.Number("wait", absent ? 0f : 3f);
                while (true)
                {
                    bool has = c.Runner.Watch.LogHas(text, from, out string line);
                    if (has && !absent)
                    {
                        c.Pass("the log has: " + line);
                        yield break;
                    }
                    if (has)
                    {
                        c.Fail("the log has a line that should not be there: " + line);
                        yield break;
                    }
                    c.Waiting = "a log line holding '" + text + "'";
                    if (Time.realtimeSinceStartup > until)
                    {
                        if (absent)
                        {
                            c.Pass("no log line holds '" + text + "'");
                        }
                        else
                        {
                            c.Fail("no log line since " + (from != null ? "step '" + from + "'" : "the last action") + " holds '" + text + "' (is Developer diagnostics on for an Info line?)");
                        }
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }
        }

        /// <summary>The "measure" step (SelfTestKinds has its words).</summary>
        internal sealed class MeasureStep : SelfTestStep
        {
            public override string Name => "measure";

            public override IEnumerator Run(SelfTestContext c)
            {
                string mark = c.Arg("mark"), since = c.Arg("since");
                if (mark == null && since == null)
                {
                    c.Fail("name a mark to note, a moment to measure since, or both");
                    yield break;
                }
                string words = string.Empty;
                if (since != null)
                {
                    float seconds = c.Runner.Since(since);
                    if (seconds < 0f)
                    {
                        c.Fail("no moment named '" + since + "' was marked");
                        yield break;
                    }
                    string name = c.Arg("as", since);
                    c.Runner.Measured(name, seconds);
                    words = name + " " + seconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " s since '" + since + "'";
                }
                if (mark != null)
                {
                    c.Runner.Mark(mark);
                    words += (words.Length > 0 ? "; " : string.Empty) + "marked '" + mark + "'";
                }
                c.Pass(words);
            }
        }

        /// <summary>The "game_open" step (SelfTestKinds has its words).</summary>
        internal sealed class GameOpenStep : SelfTestStep
        {
            public override string Name => "game_open";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: these windows are a city's");
                    yield break;
                }
                if (!SelfTestWorld.GameOpen(c.Arg("what"), out string why))
                {
                    c.Fail(why);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(c.Number("settle", 1.5f));
                c.Pass("opened " + c.Arg("what") + "; " + SelfTestGame.ShowingWords());
            }
        }

        /// <summary>The "monologue" step (SelfTestKinds has its words).</summary>
        internal sealed class MonologueStep : SelfTestStep
        {
            public override string Name => "monologue";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: a monologue is a city's");
                    yield break;
                }
                if (!SelfTestWorld.Monologue(c.Arg("key", "common_close")))
                {
                    c.Fail("the game has no monologue screen to show it on");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(c.Number("settle", 2f));
                c.Pass("the game's monologue card was asked for; " + SelfTestGame.ShowingWords());
            }
        }
    }
}
