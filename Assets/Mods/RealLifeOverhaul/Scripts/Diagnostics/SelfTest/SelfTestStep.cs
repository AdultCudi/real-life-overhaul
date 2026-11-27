using System.Collections;
using System.IO;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>
    /// What a step is given: its plan entry, a place to say what it came to, what it is waiting for (said when its time limit
    /// ends), and the runner's tools (the screenshot, the log and fault windows, the options changed). Developer diagnostic;
    /// it lives for one step.
    /// </summary>
    internal sealed class SelfTestContext
    {
        public readonly SelfTestStepSpec Spec;
        public readonly SelfTestStepResult Result;
        public readonly SelfTestRunner Runner;
        public bool Decided { get; private set; }
        public string Waiting = "starting";   // what the step waits for now, in words, for the timeout line

        public SelfTestContext(SelfTestStepSpec spec, SelfTestStepResult result, SelfTestRunner runner)
        {
            Spec = spec;
            Result = result;
            Runner = runner;
        }

        public void Pass(string words) => Decide(SelfTestOutcome.Pass, words);

        public void Fail(string words) => Decide(SelfTestOutcome.Fail, words);

        public void Skip(string words) => Decide(SelfTestOutcome.Skipped, words);

        /// <summary>The first word wins: a step that has said what it came to is not changed by a later line.</summary>
        private void Decide(SelfTestOutcome outcome, string words)
        {
            if (Decided)
            {
                return;
            }
            Decided = true;
            Result.Outcome = outcome;
            Result.Words = words;
        }

        public string Arg(string name, string fallback = null) => Spec.Arg(name, fallback);

        public float Number(string name, float fallback) => (float)Spec.Number(name, fallback);

        public bool Flag(string name, bool fallback) => Spec.Flag(name, fallback);

        /// <summary>The picture asked for now: written beside the results as "&lt;step&gt;[-extra].png"; its file name, or null when it could not be asked for.</summary>
        public string Capture(string extra = null)
        {
            return Runner.Capture(Spec.Label + (string.IsNullOrEmpty(extra) ? string.Empty : "-" + SelfTestRules.SafeName(extra, "x")), Result);
        }

        /// <summary>Waits until the engine has written the last picture asked for (it is written after the frame is drawn).</summary>
        public IEnumerator PictureWritten()
        {
            return Runner.PictureWritten();
        }
    }

    /// <summary>
    /// One kind of step of a plan: a small class with a name, a time limit (the plan's, else the kind's) and a result it
    /// puts in the context. A step is a coroutine run by the runner one at a time, never busy waiting; a step that comes
    /// to its end without saying what it came to has passed. The runner catches what a step throws and writes it as a fault.
    /// </summary>
    internal abstract class SelfTestStep
    {
        public abstract string Name { get; }

        public abstract IEnumerator Run(SelfTestContext c);
    }

    /// <summary>The picture file of a step, in the output folder.</summary>
    internal static class SelfTestPictures
    {
        public static string Path(string folder, string name) => System.IO.Path.Combine(folder, name + ".png");

        public static bool Written(string path)
        {
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }
    }
}
