using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BAModAPI;
using BigAmbitions.ModsInternal;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>
    /// Runs a plan: its steps one at a time by one coroutine (a step waits by yielding, never by looping a frame), each with
    /// its own time limit, writing results.json after every step so a crash still leaves what ran, then "SELFTEST DONE" to
    /// Player.log and, if the plan says so, the quit. It exists only when the trigger file was read (SelfTestEntry); it has
    /// no Update, so it costs nothing per frame; the game's log event is hooked only while the plan runs.
    /// </summary>
    public sealed class SelfTestRunner : MonoBehaviour
    {
        private const float PictureWait = 10f;   // seconds the engine is given to write a picture

        internal static IModLogger Logger;
        internal static SelfTestRunner Instance { get; private set; }

        private SelfTestPlan _plan;
        private string _folder;
        private readonly SelfTestResults _results = new SelfTestResults();
        internal readonly SelfTestWatch Watch = new SelfTestWatch();
        private string _lastPicture;
        private bool _pictureMissing;
        private int _modsSeen = -1;
        private readonly Dictionary<string, string> _vars = new Dictionary<string, string>(StringComparer.Ordinal);   // what find_place, call and read put by name, read by later steps as {name}
        private readonly Dictionary<string, float> _marks = new Dictionary<string, float>(StringComparer.Ordinal);   // named moments of the run, for measure

        /// <summary>Reads the trigger file, removes it, and starts the plan; a plan that cannot be read is said in Player.log.</summary>
        internal static void Begin(string path, IModLogger logger)
        {
            Logger = logger;
            string json = File.ReadAllText(path);
            File.Delete(path);   // consumed: a run that dies leaves no trigger for the next start
            var errors = new List<string>();
            SelfTestPlan plan = SelfTestPlanReader.Read(json, errors);
            if (plan == null)
            {
                Debug.LogWarning("SELFTEST plan refused: " + string.Join("; ", errors));
                return;
            }
            var host = new GameObject("RealLifeOverhaul Self-test");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<SelfTestRunner>();
            runner._plan = plan;
            runner._folder = SelfTestRules.OutputFolder(Application.persistentDataPath, plan.Output);
            Instance = runner;
            runner.StartCoroutine(Guard.Routine(runner.Main()));
        }

        private IEnumerator Main()
        {
            _results.Plan = _plan.Name;
            _results.Started = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            _results.Total = _plan.Steps.Count;
            _results.ModVersion = Meta.Changelog.Latest;
            _results.GameBuild = GameBuildText();
            bool background = Application.runInBackground;
            Application.runInBackground = true;   // a run is not watched: the game must not sleep when its window loses focus
            Watch.Hook();
            Debug.Log("SELFTEST START plan " + _plan.Name + ", " + _plan.Steps.Count + " steps, results in " + _folder);
            Write();
            for (int i = 0; i < _plan.Steps.Count; i++)
            {
                yield return RunStep(_plan.Steps[i]);
                yield return null;
            }
            int restored = _plan.KeepOptions ? 0 : SelfTestOptions.RestoreAll();
            if (restored > 0)
            {
                _results.Notes.Add(restored + " option(s) the plan changed were put back as they were");
            }
            Watch.Unhook();
            Application.runInBackground = background;
            _results.Done = true;
            Write();
            Debug.Log(_results.DoneLine());
            if (_plan.Quit)
            {
                yield return new WaitForSecondsRealtime(1f);   // the log and the file are on disk before the engine goes
                Application.Quit();
            }
        }

        private IEnumerator RunStep(SelfTestStepSpec spec)
        {
            var result = new SelfTestStepResult { Name = spec.Label, Kind = spec.Kind };
            var ctx = new SelfTestContext(spec, result, this);
            SelfTestStep step = SelfTestSteps.Make(spec.Kind);
            Watch.Begin(spec.Label, spec.Kind.StartsWith("assert_", StringComparison.Ordinal));
            float began = Time.realtimeSinceStartup;
            var stack = new Stack<IEnumerator>(4);
            float wrote = began;
            _results.Current = (_results.Steps.Count + 1) + "/" + _plan.Steps.Count + " " + spec.Label + " (limit " + spec.Limit + " s): starting";
            Write();
            if (!Substitute(spec, out string varWhy))
            {
                ctx.Fail(varWhy);
            }
            else if (step == null)
            {
                ctx.Fail("no step class for '" + spec.Kind + "'");
            }
            else
            {
                stack.Push(step.Run(ctx));
            }
            while (stack.Count > 0 && !ctx.Decided)
            {
                if (Time.realtimeSinceStartup - began > spec.Limit)
                {
                    ctx.Fail("timed out after " + spec.Limit + " s while " + ctx.Waiting);
                    break;
                }
                if (Time.realtimeSinceStartup - wrote > 5f)
                {
                    wrote = Time.realtimeSinceStartup;   // a heartbeat: a results file that stops changing means the game itself is stuck
                    _results.Current = (_results.Steps.Count + 1) + "/" + _plan.Steps.Count + " " + spec.Label + " (limit " + spec.Limit + " s, "
                                       + (int)(wrote - began) + " s in): " + ctx.Waiting;
                    Write();
                }
                bool more;
                try
                {
                    more = stack.Peek().MoveNext();
                }
                catch (Exception e)
                {
                    ctx.Fail("the step threw " + e.GetType().Name + ": " + e.Message);
                    Logger?.Warn("SelfTest: step " + spec.Label + " threw " + e);
                    break;
                }
                if (!more)
                {
                    stack.Pop();
                    continue;
                }
                object yielded = stack.Peek().Current;
                if (yielded is IEnumerator inner)
                {
                    stack.Push(inner);
                    continue;
                }
                yield return yielded;
            }
            if (!ctx.Decided)
            {
                ctx.Pass("done");
            }
            if (result.Outcome == SelfTestOutcome.Fail)
            {
                // A failure leaves what is needed to read it from the results alone: a picture, and where the game stood.
                yield return new WaitForEndOfFrame();
                Capture(spec.Label + "-fail", result);
                yield return PictureWritten();
                try
                {
                    result.Diagnostics = SelfTestDiag.Dump();
                }
                catch (Exception e)
                {
                    result.Diagnostics = "the diagnostics could not be read: " + e.GetType().Name + " " + e.Message;
                }
            }
            result.Seconds = Time.realtimeSinceStartup - began;
            Watch.StepLines(result.LogErrors);
            _results.Steps.Add(result);
            _results.Current = string.Empty;
            Debug.Log("SELFTEST step " + _results.Steps.Count + "/" + _plan.Steps.Count + " " + result.Name + ": " + result.Outcome + " - " + result.Words);
            Write();
        }

        private void Write()
        {
            try
            {
                RefreshMods();
                _results.Write(_folder);
            }
            catch (Exception e)
            {
                Logger?.Warn("SelfTest: the results could not be written (" + e.GetType().Name + ": " + e.Message + ")");
            }
        }

        /// <summary>The picture asked for: written beside the results; the file's name is added to the step's pictures.</summary>
        internal string Capture(string name, SelfTestStepResult result)
        {
            try
            {
                Directory.CreateDirectory(_folder);
                string file = SelfTestRules.SafeName(name, "shot") + ".png";
                _lastPicture = Path.Combine(_folder, file);
                _pictureMissing = false;
                if (File.Exists(_lastPicture))
                {
                    File.Delete(_lastPicture);
                }
                ScreenCapture.CaptureScreenshot(_lastPicture);
                result.Shots.Add(file);
                return file;
            }
            catch (Exception e)
            {
                Logger?.Warn("SelfTest: a picture could not be asked for (" + e.Message + ")");
                _lastPicture = null;
                return null;
            }
        }

        internal IEnumerator PictureWritten()
        {
            float until = Time.realtimeSinceStartup + PictureWait;
            while (_lastPicture != null && !SelfTestPictures.Written(_lastPicture))
            {
                if (Time.realtimeSinceStartup > until)
                {
                    _pictureMissing = true;
                    yield break;
                }
                yield return null;
            }
        }

        internal void SetVar(string name, string value) => _vars[name] = value ?? string.Empty;

        /// <summary>The step's arguments with every {name} replaced by what an earlier step put there; false, with why, for one nothing set.</summary>
        private bool Substitute(SelfTestStepSpec spec, out string why)
        {
            why = null;
            foreach (string key in new List<string>(spec.Args.Keys))
            {
                string value = spec.Args[key];
                int open = value.IndexOf('{');
                while (open >= 0)
                {
                    int close = value.IndexOf('}', open);
                    if (close < 0)
                    {
                        break;
                    }
                    string name = value.Substring(open + 1, close - open - 1);
                    if (!_vars.TryGetValue(name, out string got))
                    {
                        why = "the variable {" + name + "} was set by no earlier step that ran";
                        return false;
                    }
                    value = value.Substring(0, open) + got + value.Substring(close + 1);
                    open = value.IndexOf('{', open + got.Length);
                }
                spec.Args[key] = value;
            }
            return true;
        }

        /// <summary>Notes a named moment now.</summary>
        internal void Mark(string name) => _marks[name] = Time.realtimeSinceStartup;

        /// <summary>Seconds since a named moment, or -1 when it was never noted.</summary>
        internal float Since(string name) => _marks.TryGetValue(name, out float at) ? Time.realtimeSinceStartup - at : -1f;

        /// <summary>Records a measured figure in the results.</summary>
        internal void Measured(string name, double seconds) => _results.Measures[name] = Math.Round(seconds, 2);

        /// <summary>True when the last picture asked for never reached the disk.</summary>
        internal bool PictureMissing => _pictureMissing;

        /// <summary>The loaded mods as the game reports them, for the results and for assert_mod_loaded.</summary>
        internal static List<string> LoadedMods()
        {
            var list = new List<string>(8);
            try
            {
                foreach ((string id, string name) in ModLifecycleLoader.GetActiveMods())
                {
                    list.Add(name + " (" + id + ") " + VersionOf(id));
                }
            }
            catch (Exception e)
            {
                list.Add("the list of mods could not be read: " + e.GetType().Name);
            }
            return list;
        }

        private static string VersionOf(string id)
        {
            foreach (List<DiscoveredModEntry> entries in ModDiscoveryRegistry.Entries.Values)
            {
                foreach (DiscoveredModEntry entry in entries)
                {
                    if (entry != null && entry.ModId == id && entry.EntryType != null)
                    {
                        return "v" + entry.EntryType.Assembly.GetName().Version;
                    }
                }
            }
            return "v?";
        }

        private void RefreshMods()
        {
            List<string> mods = LoadedMods();
            if (mods.Count == _modsSeen)
            {
                return;
            }
            _modsSeen = mods.Count;
            _results.Mods.Clear();
            _results.Mods.AddRange(mods);
        }

        private static string GameBuildText()
        {
            try
            {
                return "build " + GameVersion.GetCurrent().buildNumber;
            }
            catch (Exception)
            {
                return Application.version;
            }
        }
    }
}
