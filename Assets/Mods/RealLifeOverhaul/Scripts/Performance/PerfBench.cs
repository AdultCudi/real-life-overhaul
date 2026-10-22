using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The benchmark: each item against itself in one-second windows "off ON off ON ... off", every ON window's median
    /// frame time against the off windows either side, which cancels the game's drift of a millisecond a minute. Everything
    /// is put back after each window and on every way out; a menu, pause, lost focus, building or vehicle stops it. Traffic,
    /// physics and navigation obstacles are never switched: they cannot be put back exactly.
    /// </summary>
    public sealed class PerfBench
    {
        private const float WindowSeconds = 1f;   // seconds
        private const float SettleSeconds = 0.35f;   // seconds after a switch, before the window opens
        private const float CameraSettleSeconds = 2f;   // seconds: the camera travels
        private const float BusySeconds = 40f;   // seconds: the longest a tweak may take to work through the scene
        private const float Shown = 0.10f;   // ms: a smaller saving is not called one, however consistent

        private enum Ref { None, NoUi, OneCanvas, NoMod, GameCamera }

        private sealed class Item
        {
            public string Label;
            public PerfTweak Tweak;
            public Ref Reference;
            public int Pairs;
            public string Meaning;
            public string Note = string.Empty;
            public float[] Median;   // one a window; even windows are off, odd ON
            public PerfLoopResult[] Loop;
            public int Cars, People;
            public string Canvas;   // for Ref.OneCanvas: the root canvas switched off, by name
            public bool CountsVisible;   // count the renderers drawn after the last ON and off windows
            public int VisibleOn = -1, VisibleOff = -1;
            public bool Unfinished;   // a sliced tweak was still working when its window had to open
        }

        private readonly PerfSampler _sampler;
        private readonly PerfLoop _loop;
        private readonly IModLogger _logger;
        private Item[] _items;
        private string[] _notMeasured;   // items with nothing to change in this game now, and why
        private int _item, _window;
        private bool _measuring, _wasBusy;
        private float _phaseEnds, _busyUntil;
        private string _where;

        // what a reference has changed, put back before the next window and on every way out
        private Ref _reference = Ref.None;
        private bool _cameraOption;
        private readonly List<Behaviour> _switchedOff = new List<Behaviour>(256);
        private readonly List<MonoBehaviour> _scratch = new List<MonoBehaviour>(32);
        private readonly Dictionary<Type, bool> _pausable = new Dictionary<Type, bool>();

        public bool Running { get; private set; }

        public PerfBench(PerfSampler sampler, PerfLoop loop, IModLogger logger)
        {
            _sampler = sampler;
            _loop = loop;
            _logger = logger;
        }

        public void Begin()
        {
            if (Running || _sampler.IsOpen || _loop.IsOpen)
            {
                return;
            }
            string obstacle = Obstacle();
            if (obstacle != null)
            {
                _logger?.Info("Performance test not started: " + obstacle + ".");
                PerfDirector.Notify("reallife_perf_bench_refused", 8f);
                return;
            }

            var items = new List<Item>(32);
            var notMeasured = new List<string>(8);
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                // nothing to measure: a feature the game has off already, or a picture setting at the game's own value
                string unused = tweak.NotInUse ?? (tweak.BySetting && !tweak.SettingWants ? "at the game's own value: its trial row measures a set value" : null);
                if (unused != null)
                {
                    notMeasured.Add(tweak.Id.PadRight(30) + unused);
                    continue;
                }
                // three pairs for an option, two for a trial
                items.Add(new Item { Label = tweak.Id, Tweak = tweak, Pairs = tweak.Trial || tweak.Id == "far_props" ? 2 : 3,
                                     CountsVisible = tweak is FarDistricts || tweak is LayerCull || tweak is ModCull });
            }
            _notMeasured = notMeasured.ToArray();
            items.Add(new Item { Label = "ref: no interface", Reference = Ref.NoUi, Pairs = 2, Meaning = "Every interface canvas disabled." });
            foreach (string canvas in new[] { "Smartphone", "Topbar", "PlayerHUD", "IngameDebugConsole" })
            {
                items.Add(new Item { Label = "ref: canvas " + canvas, Reference = Ref.OneCanvas, Canvas = canvas, Pairs = 2,
                                     Meaning = "Only this canvas disabled: its share of the interface's cost." });
            }
                items.Add(new Item { Label = "ref: this mod's components", Reference = Ref.NoMod, Pairs = 2,
                                 Meaning = "This mod's own per-frame code paused, except the two cameras and this test." });
            items.Add(new Item { Label = "ref: the game's own camera", Reference = Ref.GameCamera, Pairs = 1, Meaning = "The game's top-down view instead of the third-person camera." });
            _items = items.ToArray();
            float total = 0f;
            foreach (Item item in _items)
            {
                int windows = item.Pairs * 2 + 1;
                item.Median = new float[windows];
                item.Loop = new PerfLoopResult[windows];
                total += windows * (WindowSeconds + (item.Reference == Ref.GameCamera ? CameraSettleSeconds : SettleSeconds));
            }

            _sampler.Follow(null);   // the sampler follows the engine's frame split by itself
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                tweak.Forced = false;   // everything off but the item under test in its ON windows
            }
            PerfTweaks.Dirty = true;
            _where = PerfGame.Where();
            _item = 0;
            _window = 0;
            Running = true;
            Configure();
            _logger?.Info("Performance test: " + _items.Length + " items, about " + Mathf.CeilToInt(total + 30f) + " seconds. Stand still and do not touch the mouse.");
            PerfDirector.Notify("reallife_perf_bench_started4", 12f);
        }

        /// <summary>Called each frame by the director while the test runs.</summary>
        public void Step()
        {
            string obstacle = Obstacle();
            if (obstacle != null)
            {
                Stop();
                _logger?.Info("Performance test stopped (" + obstacle + "); everything is back as it was. Nothing was written.");
                PerfDirector.Notify("reallife_perf_bench_aborted", 8f);
                return;
            }
            float now = Time.unscaledTime;
            if (now < _phaseEnds)
            {
                return;
            }
            Item item = _items[_item];
            if (!_measuring)
            {
                // a tweak working through the scene in slices is measured only once it has finished
                if (Working(item))
                {
                    if (now < _busyUntil)
                    {
                        _wasBusy = true;
                        return;
                    }
                    item.Unfinished = true;
                }
                else if (_wasBusy)
                {
                    _wasBusy = false;
                    _phaseEnds = now + SettleSeconds;
                    return;
                }
                _measuring = true;
                _phaseEnds = now + WindowSeconds;
                _sampler.Open();
                _loop.Open();
                return;
            }

            _measuring = false;
            item.Loop[_window] = _loop.Close();
            item.Median[_window] = _sampler.Close().MedianMs;
            if (item.CountsVisible && _window >= item.Median.Length - 2)
            {
                int visible = CountVisible();   // outside the windows: it looks at every renderer
                if ((_window & 1) == 1)
                {
                    item.VisibleOn = visible;
                }
                else
                {
                    item.VisibleOff = visible;
                }
            }
            if ((_window & 1) == 1 && item.Tweak != null)
            {
                item.Note = (item.Tweak.Active ? string.Empty : "NOT ACTIVE  ") + (item.Tweak.Faulted ? "FAULTED (see Player.log)  " : string.Empty) + item.Tweak.Describe();
            }
            _window++;
            if (_window >= item.Median.Length)
            {
                _window = 0;
                _item++;
            }
            if (_item < _items.Length)
            {
                Configure();
                return;
            }

            string report = null;
            try
            {
                report = Describe();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            Stop();
            if (report == null)
            {
                return;
            }
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "RealLifeOverhaul_bench_" + DateTime.Now.ToString("HHmmss") + ".txt");
                File.WriteAllText(path, report);
                _logger?.Info("Performance test finished; everything is back as it was. Table written to " + path);
                PerfDirector.Notify("reallife_perf_bench_done", 10f);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>Stops without a report, everything restored: the scene is going.</summary>
        public void Cancel()
        {
            if (Running)
            {
                Stop();
            }
        }

        private Renderer[] _renderers;

        // how many renderers some camera or shadow pass drew in the frame just ended
        private int CountVisible()
        {
            _renderers ??= UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            int visible = 0;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null && renderer.isVisible)
                {
                    visible++;
                }
            }
            return visible;
        }

        // the item's tweak has not reached this window's state yet, or is still working through the scene
        private bool Working(Item item)
        {
            if (PerfTweaks.Dirty)
            {
                return true;
            }
            PerfTweak tweak = item.Tweak;
            if (tweak == null || tweak.Faulted)
            {
                return false;
            }
            bool on = (_window & 1) == 1;
            return tweak.Active != on || (on && tweak.Busy);
        }

        private void Configure()
        {
            Item item = _items[_item];
            bool on = (_window & 1) == 1;
            if (_window == 0)
            {
                item.Cars = PerfGame.CarsDriving();
                item.People = PerfGame.Pedestrians != null ? PerfGame.Pedestrians.Count : 0;
            }
            UndoReference();
            if (item.Tweak != null)
            {
                item.Tweak.Forced = on;
                PerfTweaks.Dirty = true;
            }
            else if (on)
            {
                try
                {
                    ApplyReference(item);
                }
                catch (Exception e)
                {
                    // measured anyway and said so; what was switched before the fault is in the list and goes back
                    item.Note = "COULD NOT BE SET UP: " + e.Message;
                    _logger?.Error(e);
                }
            }
            _measuring = false;
            _wasBusy = false;
            float now = Time.unscaledTime;
            _phaseEnds = now + (item.Reference == Ref.GameCamera ? CameraSettleSeconds : SettleSeconds);
            _busyUntil = now + BusySeconds;
        }

        private void ApplyReference(Item item)
        {
            _reference = item.Reference;
            switch (item.Reference)
            {
                case Ref.GameCamera:
                    _cameraOption = Settings.CameraEnabled;
                    Settings.CameraEnabled = false;   // in memory only: the saved option is not touched
                    item.Note = "the third-person camera option was " + (_cameraOption ? "on" : "off, so both states are the same view");
                    break;
                case Ref.NoUi:
                    foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    {
                        if (canvas.isRootCanvas)
                        {
                            SwitchOff(canvas);
                        }
                    }
                    item.Note = _switchedOff.Count + " root canvases";
                    break;
                case Ref.OneCanvas:
                    int graphics = 0;
                    foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    {
                        if (canvas.isRootCanvas && canvas.name == item.Canvas)
                        {
                            graphics += canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(false).Length;
                            SwitchOff(canvas);
                        }
                    }
                    item.Note = _switchedOff.Count + " canvas(es), " + graphics + " active graphics";
                    break;
                case Ref.NoMod:
                    PerfDirector director = PerfDirector.Instance;
                    if (director != null)
                    {
                        director.GetComponentsInChildren(false, _scratch);
                        foreach (MonoBehaviour script in _scratch)
                        {
                            // not the measuring itself, and not the cameras: the view must stay the same
                            if (script != null && script.GetType().Assembly == typeof(PerfBench).Assembly && !(script is PerfDirector) && !(script is PerfSampler)
                                && !(script is ThirdPersonCamera) && !(script is ChaseCamera) && Pausable(script.GetType()))
                            {
                                SwitchOff(script);
                            }
                        }
                        _scratch.Clear();
                    }
                    item.Note = _switchedOff.Count + " components";
                    break;
            }
        }

        // Paused for a window only if the engine calls it every frame or physics step and it does nothing on being switched
        // off or on: then switching it on again undoes the pause exactly.
        private bool Pausable(Type type)
        {
            if (_pausable.TryGetValue(type, out bool known))
            {
                return known;
            }
            const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            bool called = false, reacts = false;
            for (Type t = type; t != null && t != typeof(MonoBehaviour) && t != typeof(Behaviour); t = t.BaseType)
            {
                called |= t.GetMethod("Update", Declared, null, Type.EmptyTypes, null) != null || t.GetMethod("LateUpdate", Declared, null, Type.EmptyTypes, null) != null
                          || t.GetMethod("FixedUpdate", Declared, null, Type.EmptyTypes, null) != null;
                reacts |= t.GetMethod("OnDisable", Declared, null, Type.EmptyTypes, null) != null || t.GetMethod("OnEnable", Declared, null, Type.EmptyTypes, null) != null;
            }
            _pausable[type] = called && !reacts;
            return called && !reacts;
        }

        private void SwitchOff(Behaviour part)
        {
            if (part != null && part.enabled)
            {
                part.enabled = false;
                _switchedOff.Add(part);
            }
        }

        private void UndoReference()
        {
            switch (_reference)
            {
                case Ref.GameCamera:
                    Settings.CameraEnabled = _cameraOption;
                    break;
            }
            foreach (Behaviour part in _switchedOff)
            {
                if (part != null)
                {
                    part.enabled = true;
                }
            }
            _switchedOff.Clear();
            _reference = Ref.None;
        }

        private void Stop()
        {
            Running = false;
            _measuring = false;
            if (_loop.IsOpen)
            {
                _loop.Close();
            }
            if (_sampler.IsOpen)
            {
                _sampler.Close();
            }
            UndoReference();
            _renderers = null;
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                tweak.Forced = null;
            }
            PerfTweaks.Dirty = true;
        }

        // why the test cannot run now, or null
        private static string Obstacle()
        {
            if (!Application.isFocused)
            {
                return "the game is not the focused window";
            }
            if (Time.timeScale <= 0f)
            {
                return "the game is paused";
            }
            if (ThirdPersonCamera.IsUiOpen())
            {
                return "a menu is open";
            }
            if (BuildingManager.IsInsideBuilding)
            {
                return "the player is inside a building";
            }
            if (PerfGame.InVehicle())
            {
                return "the player is in a vehicle";
            }
            return null;
        }

        private delegate float Share(PerfLoopResult loop);

        // the mean over an item's pairs of (the two off windows either side) minus (the ON window between them)
        private static float Paired(Item item, Share share)
        {
            float sum = 0f;
            for (int k = 0; k < item.Pairs; k++)
            {
                PerfLoopResult a = item.Loop[k * 2], b = item.Loop[k * 2 + 1], c = item.Loop[k * 2 + 2];
                if (!a.Valid || !b.Valid || !c.Valid)
                {
                    return float.NaN;
                }
                sum += (share(a) + share(c)) * 0.5f - share(b);
            }
            return sum / item.Pairs;
        }

        private static float PairSaving(Item item, int k) => (item.Median[k * 2] + item.Median[k * 2 + 2]) * 0.5f - item.Median[k * 2 + 1];

        private static string Signed(float value) => float.IsNaN(value) ? "n/a" : (value >= 0f ? "+" : string.Empty) + PerfText.F2(value);

        private string Describe()
        {
            var text = new StringBuilder(24576);
            text.AppendLine("Real Life Overhaul performance test  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  mod " + typeof(PerfBench).Assembly.GetName().Version);
            text.AppendLine("where: " + _where + "  screen " + Screen.width + "x" + Screen.height + "  quality level " + QualitySettings.GetQualityLevel()
                            + "  targetFrameRate " + Application.targetFrameRate + "  vSyncCount " + QualitySettings.vSyncCount);
            text.AppendLine("method: each item is measured as  off ON off ON ... off  in windows of " + WindowSeconds
                            + " s. A window's value is the MEDIAN frame time. Each ON window is compared with the mean of the off windows");
            text.AppendLine("        either side of it (steady drift cancels); 'saved' is the mean of those paired differences and 'each pair' lists them:"
                            + " their spread is the uncertainty. Positive = the item makes the frame shorter.");
            text.AppendLine("        The d.* columns are the same paired difference for each share of the main thread's frame (positive = that share got smaller)."
                            + " All tweaks and trials other than the item are off throughout.");
            text.AppendLine();

            // how steady the run was: every off window there is
            float lowest = float.MaxValue, highest = 0f, first = _items[0].Median[0], last = 0f;
            double offSum = 0.0;
            int offCount = 0;
            var split = new double[9];
            int splits = 0;
            foreach (Item item in _items)
            {
                for (int w = 0; w < item.Median.Length; w += 2)
                {
                    float ms = item.Median[w];
                    lowest = Mathf.Min(lowest, ms);
                    highest = Mathf.Max(highest, ms);
                    last = ms;
                    offSum += ms;
                    offCount++;
                    PerfLoopResult l = item.Loop[w];
                    if (l.Valid)
                    {
                        split[0] += l.ScriptsMs; split[1] += l.PhysicsMs; split[2] += l.AnimationMs; split[3] += l.NavigationMs; split[4] += l.UiMs;
                        split[5] += l.RenderMs; split[6] += l.PipelineMs; split[7] += l.AudioMs; split[8] += l.OtherMs + l.WaitMs;
                        splits++;
                    }
                }
            }
            float typical = (float)(offSum / Mathf.Max(1, offCount));
            text.AppendLine("the run: " + offCount + " windows with everything off, median frame " + PerfText.F2(lowest) + " to " + PerfText.F2(highest) + " ms (mean "
                            + PerfText.F2(typical) + " ms = " + PerfText.F1(1000f / Mathf.Max(0.1f, typical)) + " fps); first " + PerfText.F2(first) + ", last " + PerfText.F2(last)
                            + ". That range is the drift the pairing is there to cancel.");
            text.AppendLine();

            string header = "item".PadRight(30) + Cell("off ms", 8) + Cell("ON ms", 8) + Cell("saved", 8) + "  " + "each pair".PadRight(22) + Cell("d.script", 9)
                            + Cell("d.phys", 8) + Cell("d.nav", 7) + Cell("d.draw", 8) + Cell("d.hdrp", 8) + Cell("d.other", 8) + Cell("cars", 6) + Cell("people", 7) + "   verdict";
            Section(text, header, "TWEAKS (options)", item => item.Tweak != null && !item.Tweak.Trial);
            Section(text, header, "TRIALS (one feature off, or one picture setting at a set value, for the row; several change the picture)",
                item => item.Tweak != null && item.Tweak.Trial);
            Section(text, header, "REFERENCES", item => item.Tweak == null);
            text.AppendLine("NOT MEASURED (nothing to change in this game now)");
            foreach (string line in _notMeasured)
            {
                text.AppendLine("  " + line);
            }
            text.AppendLine();
            text.AppendLine("verdict: 'faster' / 'SLOWER' = every pair agrees and the mean is at least " + PerfText.F2(Shown)
                            + " ms; otherwise 'not shown'. cars / people = traffic cars driving and pedestrians walking when the item began.");
            text.AppendLine("d.script = every script of the game and all mods; d.phys = physics; d.nav = navigation; d.draw = culling + render pipeline + hand-over to the render thread;"
                            + " d.hdrp = the part of d.draw inside the pipeline's own code; d.other = the rest.");

            text.AppendLine();
            text.AppendLine("where the main thread's frame goes with everything off (mean of " + splits
                            + " windows), and the frame rate if a share were removed entirely (no fix removes a share entirely):");
            if (splits == 0)
            {
                text.AppendLine("  the frame's steps could not be timed in this run");
            }
            else
            {
                string[] names =
                {
                    "all scripts (game and mods)", "physics", "animation", "navigation", "interface rebuilds", "all drawing work on the main thread",
                    "  of it: the render pipeline's own code", "audio", "everything else"
                };
                for (int i = 0; i < names.Length; i++)
                {
                    float share = (float)(split[i] / splits);
                    text.AppendLine("  " + names[i].PadRight(40) + PerfText.F2(share).PadLeft(6) + " ms = " + PerfText.F1(share / typical * 100f).PadLeft(5)
                                    + "% of the frame   without it: " + PerfText.F1(1000f / Mathf.Max(0.1f, typical - share)) + " fps");
                }
            }

            text.AppendLine();
            text.AppendLine("tweaks as the player has them set (back in force now):");
            foreach (PerfTweak tweak in PerfTweaks.All)
            {
                if (!tweak.Trial && !tweak.BySetting)
                {
                    text.AppendLine("  " + tweak.Id.PadRight(22) + (tweak.Wanted ? "on" : "off"));
                }
            }
            text.AppendLine("  " + "shadow_reach".PadRight(22) + Settings.ShadowReach + "% of the game's sun shadow distance");
            text.AppendLine("  " + "shadow_cascades".PadRight(22) + (Settings.ShadowCascades > 0 ? Settings.ShadowCascades.ToString() : "the game's own"));
            text.AppendLine("  " + "lod_reach".PadRight(22) + Settings.LodReach + "% of the game's level-of-detail distances");
            return text.ToString();
        }

        private void Section(StringBuilder text, string header, string title, Predicate<Item> belongs)
        {
            text.AppendLine(title);
            text.AppendLine(header);
            foreach (Item item in _items)
            {
                if (!belongs(item))
                {
                    continue;
                }
                float off = 0f, on = 0f, saved = 0f;
                bool allUp = true, allDown = true;
                var pairs = new StringBuilder(24);
                for (int w = 0; w < item.Median.Length; w++)
                {
                    if ((w & 1) == 0)
                    {
                        off += item.Median[w];
                    }
                    else
                    {
                        on += item.Median[w];
                    }
                }
                off /= item.Pairs + 1;
                on /= item.Pairs;
                for (int k = 0; k < item.Pairs; k++)
                {
                    float d = PairSaving(item, k);
                    saved += d;
                    allUp &= d > 0f;
                    allDown &= d < 0f;
                    pairs.Append(Signed(d)).Append(' ');
                }
                saved /= item.Pairs;
                string verdict = item.Pairs < 2 ? "one pair only: indicative" : allUp && saved >= Shown ? "faster" : allDown && saved <= -Shown ? "SLOWER" : "not shown";
                text.AppendLine(item.Label.PadRight(30) + Cell(PerfText.F2(off), 8) + Cell(PerfText.F2(on), 8) + Cell(Signed(saved), 8) + "  " + pairs.ToString().PadRight(22)
                                + Cell(Signed(Paired(item, l => l.ScriptsMs)), 9) + Cell(Signed(Paired(item, l => l.PhysicsMs)), 8) + Cell(Signed(Paired(item, l => l.NavigationMs)), 7)
                                + Cell(Signed(Paired(item, l => l.RenderMs)), 8) + Cell(Signed(Paired(item, l => l.PipelineMs)), 8)
                                + Cell(Signed(Paired(item, l => l.AnimationMs + l.UiMs + l.AudioMs + l.OtherMs)), 8) + Cell(item.Cars.ToString(), 6) + Cell(item.People.ToString(), 7)
                                + "   " + verdict + (item.Unfinished ? "  (STILL WORKING when measured)" : string.Empty));
                if (item.VisibleOn >= 0)
                {
                    item.Note += "  | renderers drawn: off " + item.VisibleOff + ", ON " + item.VisibleOn;
                }
                string note = (item.Meaning ?? string.Empty) + (item.Note.Length > 0 ? (item.Meaning != null ? "  [" + item.Note + "]" : item.Note) : string.Empty);
                if (note.Length > 0)
                {
                    text.AppendLine("      " + note);
                }
            }
            text.AppendLine();
        }

        private static string Cell(string value, int width) => value.PadLeft(width);
    }
}
