using System;
using System.Collections;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The step table's assert part: its steps, each a nested class (see SelfTestSteps.cs).</summary>
    internal static partial class SelfTestSteps
    {
        /// <summary>A picture of the screen into the output folder, named by the step (or by the name given).</summary>
        internal sealed class ScreenshotStep : SelfTestStep
        {
            public override string Name => "screenshot";

            public override IEnumerator Run(SelfTestContext c)
            {
                yield return new WaitForEndOfFrame();   // what is drawn this frame, UI included
                string file = c.Capture(c.Arg("name"));
                if (file == null)
                {
                    c.Fail("the picture could not be asked for");
                    yield break;
                }
                c.Waiting = "the engine to write " + file;
                yield return c.PictureWritten();
                if (c.Runner.PictureMissing)
                {
                    c.Fail("the engine did not write " + file + " in 10 s");
                }
                else
                {
                    c.Pass(file + "; " + SelfTestGame.ShowingWords());
                }
            }
        }

        /// <summary>No fault in the mod's guards, no feature switched off and no kit piece fallen back to the game's own screen since the last step that is not an assert.</summary>
        internal sealed class AssertNoFaultsStep : SelfTestStep
        {
            public override string Name => "assert_no_faults";

            public override IEnumerator Run(SelfTestContext c)
            {
                string words = c.Runner.Watch.FaultWords();
                if (words.Length == 0)
                {
                    c.Pass("the guards recorded no fault, no feature went off, no kit piece fell back");
                }
                else
                {
                    c.Fail(words);
                }
                yield break;
            }
        }

        /// <summary>No error or exception line in the game's log since the last step that is not an assert: by default RLO's own (a line names whose it is), or every one.</summary>
        internal sealed class AssertNoLogErrorsStep : SelfTestStep
        {
            public override string Name => "assert_no_log_errors";

            public override IEnumerator Run(SelfTestContext c)
            {
                bool rlo = c.Arg("which", "rlo") != "any";
                var lines = new List<string>(8);
                int n = c.Runner.Watch.WindowLines(rlo, lines);
                if (n == 0)
                {
                    int others = rlo ? c.Runner.Watch.OtherWindowLines() : 0;
                    string more = others > 0 ? " (" + others + " from the game or another mod are in the step's log_errors)" : string.Empty;
                    c.Pass("no " + (rlo ? "RLO " : string.Empty) + "error line in the log" + more);
                }
                else
                {
                    c.Fail(n + " error line(s): " + string.Join(" | ", lines.GetRange(0, Math.Min(lines.Count, 4))));
                }
                yield break;
            }
        }

        /// <summary>The "assert_kit_showing" step (SelfTestKinds has its words).</summary>
        internal sealed class AssertKitShowingStep : SelfTestStep
        {
            public override string Name => "assert_kit_showing";

            public override IEnumerator Run(SelfTestContext c)
            {
                string id = c.Arg("id");
                float until = Time.realtimeSinceStartup + c.Number("wait", 3f);
                while (true)
                {
                    bool none = id == "none";
                    UiHost host = none ? SelfTestGame.Standing(null) : SelfTestGame.Showing(id);
                    if (none ? host == null : host != null)
                    {
                        c.Pass(none ? "no kit screen shows" : "the kit screen '" + id + "' shows (" + host.SelfTestShape() + ")");
                        yield break;
                    }
                    c.Waiting = none ? "the kit screens to go" : "the kit screen '" + id + "' to show";
                    if (Time.realtimeSinceStartup > until)
                    {
                        c.Fail(none ? "a kit screen shows: " + SelfTestGame.ShowingWords() : "the kit screen '" + id + "' does not show: " + SelfTestGame.ShowingWords()
                            + (UiDirector.On ? string.Empty : " (the RLO interface is off or stopped)"));
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.25f);
                }
            }
        }

        /// <summary>Words on the showing kit screen: a text key's words in the game's language (the way the kit reads it), or plain words.</summary>
        internal sealed class AssertTextStep : SelfTestStep
        {
            public override string Name => "assert_text";

            public override IEnumerator Run(SelfTestContext c)
            {
                string key = c.Arg("key"), words = c.Arg("words");
                if (key == null && words == null)
                {
                    c.Fail("name a key or the words");
                    yield break;
                }
                if (key != null)
                {
                    words = UiText.T(key);
                    int brace = words.IndexOf('{');
                    if (words == key)
                    {
                        c.Fail("the key '" + key + "' has no text");
                        yield break;
                    }
                    if (brace > 0)
                    {
                        words = words.Substring(0, brace).TrimEnd();   // a filled-in figure is not asked for
                    }
                }
                UiHost host = SelfTestGame.Target(c.Arg("screen"));
                if (host == null)
                {
                    c.Fail("no kit screen shows: " + SelfTestGame.ShowingWords());
                    yield break;
                }
                if (host.SelfTestHasWords(words))
                {
                    c.Pass("'" + words + "' is on '" + host.Screen.Id + "'");
                }
                else
                {
                    c.Fail("'" + words + "' is not on '" + host.Screen.Id + "' (" + host.SelfTestShape() + ")");
                }
                yield break;
            }
        }

        /// <summary>A mod is loaded, by a part of its name as the game reports it; when it is not, the step says so plainly (or is skipped, when the plan says an absent mod is fine).</summary>
        internal sealed class AssertModLoadedStep : SelfTestStep
        {
            public override string Name => "assert_mod_loaded";

            public override IEnumerator Run(SelfTestContext c)
            {
                string want = c.Arg("name");
                List<string> mods = SelfTestRunner.LoadedMods();
                for (int i = 0; i < mods.Count; i++)
                {
                    if (mods[i].IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        c.Pass("loaded: " + mods[i]);
                        yield break;
                    }
                }
                string words = "no loaded mod is named like '" + want + "'. Loaded: " + string.Join("; ", mods);
                if (c.Flag("skip_if_absent", false))
                {
                    c.Skip("MOD ABSENT, so what needs it was not tested. " + words);
                }
                else
                {
                    c.Fail("MOD ABSENT: " + words);
                }
                yield break;
            }
        }
    }
}
