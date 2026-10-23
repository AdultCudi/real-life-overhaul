using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// The traffic mend (Traffic/TrafficMendRules.cs, Traffic/TrafficMend.cs, Traffic/TrafficMendGame.cs). A player's city
// emptied a few minutes into a session: the game's traffic (GleyTrafficSystem) threw a NullReferenceException in
// DensityManager.UpdateVehicleDensity on every frame for the rest of it, 147,632 times, because the density manager it
// held had lost its link to the road grid (the game's own Mono gives that very trace, [0x0000e] and no frame under it,
// for a null DensityManager.gridManager and for nothing else), and later was not a whole object any more ([0x00000]).
// TrafficManager.Update then stops before it takes far cars off, turns the lights or moves the grid: no car is added
// again, and nothing in the game puts it right. Checked here: the game's log lines of that fault are told from every
// other (both traces of the player's log, and the engine's own form of a line handed to a listener), a model of the
// game's traffic loop frame by frame that empties for good without the mend and fills again within three seconds with
// it, never a mend for one bad frame or on a guest's mirrored traffic, at most one in 30 s and five a city, the same
// answers for the same frames, the cost of weighing a line, and over the sources: the mend ticked and listening, the
// held density manager never read through or destroyed, no car taken off the road, and the texts and What's new.
internal static partial class Program
{
    private const string MendText = "NullReferenceException: Object reference not set to an instance of an object";

    // The player's Player.log, its first 707 lines and the 146,925 after them.
    private const string MendLogged = "  at GleyTrafficSystem.DensityManager.UpdateVehicleDensity (UnityEngine.Vector3 playerPosition, UnityEngine.Vector3 playerDirection, System.Int32 activeCameraIndex) [0x0000e] in <e752cdd3257e48ecb30b90cf9611592a>:0 \n"
                                    + "  at GleyTrafficSystem.TrafficManager.Update () [0x00207] in <e752cdd3257e48ecb30b90cf9611592a>:0 \n";
    private const string MendLoggedLater = "  at GleyTrafficSystem.DensityManager.UpdateVehicleDensity (UnityEngine.Vector3 playerPosition, UnityEngine.Vector3 playerDirection, System.Int32 activeCameraIndex) [0x00000] in <e752cdd3257e48ecb30b90cf9611592a>:0 \n"
                                         + "  at GleyTrafficSystem.TrafficManager.Update () [0x00207] in <e752cdd3257e48ecb30b90cf9611592a>:0 \n";

    // The engine's own form of the same stack, as Application.logMessageReceivedThreaded hands it over.
    private const string MendHeard = "GleyTrafficSystem.DensityManager.UpdateVehicleDensity (UnityEngine.Vector3 playerPosition, UnityEngine.Vector3 playerDirection, System.Int32 activeCameraIndex) (at <e752cdd3257e48ecb30b90cf9611592a>:0)\n"
                                   + "GleyTrafficSystem.TrafficManager.Update () (at <e752cdd3257e48ecb30b90cf9611592a>:0)\n";

    // A player standing off the road grid: the game's lookup is out of range, which passes when they are back on it.
    private const string MendOffGrid = "GleyUrbanAssets.CurrentSceneData.GetCell (System.Single xPoz, System.Single zPoz) (at <e752cdd3257e48ecb30b90cf9611592a>:0)\n"
                                     + "GleyUrbanAssets.GridManager.GetCell (System.Int32 activeCameraIndex) (at <e752cdd3257e48ecb30b90cf9611592a>:0)\n" + MendHeard;

    private static void TrafficMends()
    {
        MendLines();
        MendModel();
        MendDecisions();
        MendCost();
        MendSources();
    }

    private static void MendLines()
    {
        const int Exception = TrafficMendRules.TypeException;
        Check(TrafficMendRules.IsDensityFault(Exception, MendText, MendLogged), "traffic mend: the player's log line of the density pass at [0x0000e] is the fault");
        Check(TrafficMendRules.IsDensityFault(Exception, MendText, MendLoggedLater), "traffic mend: the player's later line at [0x00000] is the fault");
        Check(TrafficMendRules.IsDensityFault(Exception, MendText, MendHeard), "traffic mend: the engine's own form of the line, as a listener gets it, is the fault");
        Check(!TrafficMendRules.IsDensityFault(Exception, "IndexOutOfRangeException: Index was outside the bounds of the array.", MendOffGrid),
            "traffic mend: a player off the road grid (an index out of range, which passes) is not the fault");
        Check(!TrafficMendRules.IsDensityFault(Exception, MendText,
                "Helpers.HappinessHelper.ConvertTemporalBoostsToRegularBoosts () (at <fdedeac4e1ad42668c1e57f28d2390d6>:0)\nPlayerController.Awake () (at <fdedeac4e1ad42668c1e57f28d2390d6>:0)\n"),
            "traffic mend: a null reference anywhere else (the same log's happiness boost) is not the fault");
        Check(!TrafficMendRules.IsDensityFault(Exception, MendText, "GleyTrafficSystem.TrafficManager.Update () (at <e752>:0)\n"),
            "traffic mend: a null reference in the traffic's own Update but not in its density pass is not the fault");
        Check(!TrafficMendRules.IsDensityFault(Exception, MendText, "GleyTrafficSystem.TrafficManager.Update () (at <e752>:0)\nGleyTrafficSystem.DensityManager.UpdateVehicleDensity\n"),
            "traffic mend: the density pass only counts when the traffic's Update called it");
        Check(!TrafficMendRules.IsDensityFault(0, MendText, MendHeard) && !TrafficMendRules.IsDensityFault(2, MendText, MendHeard) && !TrafficMendRules.IsDensityFault(3, MendText, MendHeard),
            "traffic mend: only an exception line counts, never an error, a warning or a log line with the same words");
        Check(!TrafficMendRules.IsDensityFault(Exception, null, MendHeard) && !TrafficMendRules.IsDensityFault(Exception, MendText, null)
              && !TrafficMendRules.IsDensityFault(Exception, string.Empty, string.Empty), "traffic mend: a line with no text or no stack is not the fault");
        Check(!TrafficMendRules.IsDensityFault(Exception, "[Mod:Real Life Overhaul] " + MendText, MendHeard),
            "traffic mend: the mod's own line that quotes the fault is not counted again");
    }

    // A model of the game's traffic loop as GleyTrafficSystem has it: each frame TrafficManager.Update makes its density
    // pass, which adds one car when there are fewer than wanted, and then takes off a car that has gone too far. Once the
    // density manager's link to the grid is lost, the pass throws on every frame it runs and nothing after it runs. The
    // mod's mend is driven as Traffic/TrafficMend drives it: every fault counted as it is logged, a look once a second.
    private sealed class TrafficModel
    {
        public int Wanted = 20, OnRoad = 20;
        public bool Broken, Broke;
        public int Faults, Mends, GaveUp, AddedAfterBreak;
        public MendState State;
        public float Next;

        public void Frame(int frame, bool mend, bool running = true, float breakAt = 60f)
        {
            float t = frame / 60f;
            if (t >= breakAt && !Broke)
            {
                Broke = Broken = true;
            }
            if (running)
            {
                Pass(frame, t);
            }
            if (!mend || t < Next)
            {
                return;
            }
            Next = t + TrafficMendRules.Interval;
            int faults = Faults;
            Faults = 0;
            switch (TrafficMendRules.Decide(ref State, faults, running, t))
            {
                case MendStep.Mend:
                    Mends++;
                    Broken = false;   // a new density manager, counting the cars on the road
                    break;
                case MendStep.GiveUp:
                    GaveUp++;
                    break;
            }
        }

        // TrafficManager.Update: the density pass, which throws while broken and has fewer cars than it wants, and after
        // it the far car taken off (one every second and a half), which a throw skips.
        private void Pass(int frame, float t)
        {
            if (OnRoad < Wanted)
            {
                if (Broken)
                {
                    Faults++;
                    return;
                }
                OnRoad++;
                AddedAfterBreak += Broke ? 1 : 0;
            }
            if (frame % 90 == 0 && OnRoad > 0)
            {
                OnRoad--;
            }
        }
    }

    private static void MendModel()
    {
        var without = new TrafficModel();
        var with = new TrafficModel();
        float mendedAt = -1f;
        for (int frame = 0; frame < 60 * 180; frame++)
        {
            without.Frame(frame, mend: false);
            int before = with.Mends;
            with.Frame(frame, mend: true);
            if (with.Mends > before && mendedAt < 0f)
            {
                mendedAt = frame / 60f;
            }
        }
        Check(without.AddedAfterBreak == 0 && without.OnRoad < without.Wanted,
            "traffic mend: without it, the modelled traffic adds no car in the two minutes after the break (" + without.AddedAfterBreak + " added, "
            + without.OnRoad + " of " + without.Wanted + " on the road), as the player's city emptied");
        Check(mendedAt >= 60f && mendedAt <= 63f, "traffic mend: with it, the break is mended within three seconds (at " + mendedAt.ToString("F2") + " s, the break at 60)");
        Check(with.Mends == 1 && with.GaveUp == 0, "traffic mend: one mend for one break (" + with.Mends + ")");
        Check(with.AddedAfterBreak > 0 && with.OnRoad >= with.Wanted - 1, "traffic mend: after it the cars keep coming (" + with.AddedAfterBreak + " added, " + with.OnRoad + " on the road)");

        // Indoors or paused, the game runs no pass and nothing is mended; out again, it is.
        var indoors = new TrafficModel();
        for (int frame = 0; frame < 60 * 120; frame++)
        {
            indoors.Frame(frame, mend: true, running: frame < 60 * 60 || frame >= 60 * 90);
        }
        Check(indoors.Mends == 1 && indoors.State.LastMend >= 90f && indoors.State.LastMend <= 93f,
            "traffic mend: not mended while the traffic is stopped, mended within three seconds of it running again (" + indoors.Mends + " at "
            + indoors.State.LastMend.ToString("F2") + " s)");
    }

    private static void MendDecisions()
    {
        // One bad frame, or two a second, is never a break.
        var state = new MendState();
        MendStep step = MendStep.None;
        for (int look = 1; look <= 120; look++)
        {
            step = TrafficMendRules.Decide(ref state, look % 10 == 0 ? 1 : 0, true, look);
            if (step != MendStep.None)
            {
                break;
            }
        }
        Check(step == MendStep.None && state.Mends == 0, "traffic mend: a single failed pass every ten seconds is never mended");
        state = new MendState();
        for (int look = 1; look <= 120 && step == MendStep.None; look++)
        {
            step = TrafficMendRules.Decide(ref state, TrafficMendRules.FaultsInALook - 1, true, look);
        }
        Check(step == MendStep.None, "traffic mend: fewer than " + TrafficMendRules.FaultsInALook + " failed passes a second are never mended");

        // A look of failing alone is not enough; a second in a row is, at a low frame rate too.
        state = new MendState();
        Check(TrafficMendRules.Decide(ref state, 5, true, 1f) == MendStep.None && TrafficMendRules.Decide(ref state, 5, true, 2f) == MendStep.Mend,
            "traffic mend: two seconds of failing at five frames a second are mended on the second look");
        state = new MendState();
        Check(TrafficMendRules.Decide(ref state, 60, true, 1f) == MendStep.None && TrafficMendRules.Decide(ref state, 0, true, 2f) == MendStep.None
              && TrafficMendRules.Decide(ref state, 60, true, 3f) == MendStep.None, "traffic mend: a look without failing between two that fail starts the count again");

        // Not on a guest's mirrored traffic, indoors or paused: Running is false there (Traffic/TrafficMendGame), and no look then counts.
        state = new MendState();
        bool any = false;
        for (int look = 1; look <= 120; look++)
        {
            any |= TrafficMendRules.Decide(ref state, 60, false, look) != MendStep.None;
        }
        Check(!any && state.Streak == 0, "traffic mend: nothing is done while the traffic does not run as this machine's own (a Going Public guest, indoors, paused)");

        // Still failing after a mend: the next waits 30 s, five a city, then it gives up once and says so.
        state = new MendState();
        int mends = 0, giveUps = 0;
        float last = -100f, closest = float.MaxValue;
        for (int look = 1; look <= 1000; look++)
        {
            MendStep s = TrafficMendRules.Decide(ref state, 60, true, look);
            if (s == MendStep.Mend)
            {
                mends++;
                closest = Math.Min(closest, look - last);
                last = look;
            }
            giveUps += s == MendStep.GiveUp ? 1 : 0;
        }
        Check(mends == TrafficMendRules.MostMends && giveUps == 1, "traffic mend: a city's traffic that keeps failing is mended " + TrafficMendRules.MostMends
              + " times and given up on once (" + mends + ", " + giveUps + ")");
        Check(closest >= TrafficMendRules.Settle, "traffic mend: never two mends within " + TrafficMendRules.Settle.ToString("F0") + " s (closest " + closest.ToString("F0") + " s)");
        state = new MendState();
        Check(TrafficMendRules.Decide(ref state, 60, true, 1f) == MendStep.None && TrafficMendRules.Decide(ref state, 60, true, 2f) == MendStep.Mend,
            "traffic mend: the next city's traffic, a new state, is mended again");

        // The same looks give the same answers.
        string a = MendRun(), b = MendRun();
        Check(a == b && a.Length > 0, "traffic mend: the same looks give the same steps (" + a + ")");

        // The self-check line.
        Check(TrafficMendRules.CheckLine(58, 0, 17).EndsWith(": PASS", StringComparison.Ordinal) && TrafficMendRules.CheckLine(58, 0, 17).StartsWith("Traffic check (density pass): ", StringComparison.Ordinal),
            "traffic mend: the self-check passes with no failed pass after the mend");
        Check(TrafficMendRules.CheckLine(58, 60, 17).Contains(": FAIL, "), "traffic mend: the self-check fails, with why, when the pass still fails");
        Check(TrafficMendRules.CheckLine(1234, 0, 5).Contains("1234 failed"), "traffic mend: the self-check's numbers are written without a culture's separators");
    }

    private static string MendRun()
    {
        var state = new MendState();
        var steps = new System.Text.StringBuilder();
        int[] faults = { 0, 4, 60, 60, 0, 60, 60, 60, 2, 60, 60 };
        for (int look = 0; look < 400; look++)
        {
            MendStep s = TrafficMendRules.Decide(ref state, faults[look % faults.Length], look % 37 != 0, look);
            steps.Append(s == MendStep.Mend ? 'M' : s == MendStep.GiveUp ? 'G' : '.');
        }
        return steps.ToString().Replace(".", string.Empty);
    }

    private static void MendCost()
    {
        long bytes = MendAsks(out long ms, out int faults);
        Check(bytes == 0, "traffic mend: weighing 1,000,000 log lines allocates nothing (" + bytes + " bytes)");
        Check(ms < 300, "traffic mend: weighing 1,000,000 log lines takes " + ms + " ms (300 at the most; the game logs a few a second)");
        Check(faults == 250000, "traffic mend: of the timed lines exactly the density faults were counted (" + faults + ")");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long MendAsks(out long ms, out int faults)
    {
        string[] texts = { MendText, "IndexOutOfRangeException: Index was outside the bounds of the array.", "[Mod:Real Life Overhaul] Car handling tuning", MendText };
        string[] stacks = { MendHeard, MendOffGrid, string.Empty, "PlayerController.Awake () (at <fdedeac4e1ad42668c1e57f28d2390d6>:0)\n" };
        int[] types = { TrafficMendRules.TypeException, TrafficMendRules.TypeException, 3, TrafficMendRules.TypeException };
        faults = 0;
        Stopwatch watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int n = 0; n < 1000000; n++)
        {
            int k = n & 3;
            faults += TrafficMendRules.IsDensityFault(types[k], texts[k], stacks[k]) ? 1 : 0;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        watch.Stop();
        ms = watch.ElapsedMilliseconds;
        return allocated;
    }

    // Over the sources as they ship.
    private static void MendSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string mendPath = Path.Combine(scripts, @"Traffic\TrafficMend.cs");
        string gamePath = Path.Combine(scripts, @"Traffic\TrafficMendGame.cs");
        if (!File.Exists(mendPath) || !File.Exists(gamePath))
        {
            Check(false, "traffic mend sources: Traffic/TrafficMend.cs and Traffic/TrafficMendGame.cs are at " + scripts);
            return;
        }
        string mend = Code(File.ReadAllText(mendPath));
        string game = Code(File.ReadAllText(gamePath));
        string accidents = Code(File.ReadAllText(Path.Combine(scripts, @"Traffic\TrafficAccidents.cs")));
        string start = Code(File.ReadAllText(Path.Combine(scripts, @"Core\RealLifeOverhaulMod.cs")));
        string features = Code(File.ReadAllText(Path.Combine(scripts, @"Meta\FeatureList.cs")));
        string contract = Code(File.ReadAllText(Path.Combine(scripts, @"Core\GameContract.cs")));

        Match tick = Regex.Match(accidents, @"private void OnUpdate\(\)\s*\{(?<b>[^}]*)");
        Check(tick.Success && tick.Groups["b"].Value.Contains("TrafficMend.Tick(Time.unscaledTime);")
              && tick.Groups["b"].Value.IndexOf("TrafficMend.Tick", StringComparison.Ordinal) < tick.Groups["b"].Value.IndexOf("if (_lacking)", StringComparison.Ordinal),
            "traffic mend sources: TrafficAccidents' frame ticks the mend before anything of its own can return, whatever its option");
        Check(start.Contains("Guard.Step(\"the traffic mend's count of the game's failed density passes\", TrafficMend.Listen);"),
            "traffic mend sources: the count of the game's failed passes starts with the mod, before any city");
        Check(mend.Contains("Application.logMessageReceivedThreaded += Heard;") && Regex.IsMatch(mend, @"private static readonly Application\.LogCallback Heard = OnLog;")
              && Regex.Matches(mend, @"logMessageReceived").Count == 1, "traffic mend sources: one listener, made once, added once");
        Match onLog = Regex.Match(mend, @"private static void OnLog\([^)]*\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
        Check(onLog.Success && !onLog.Groups["b"].Value.Contains("Logger") && onLog.Groups["b"].Value.Contains("Interlocked.Increment(ref _faults)")
              && onLog.Groups["b"].Value.Contains("catch (Exception)"), "traffic mend sources: the listener only counts, never logs (it would hear itself), and drops a line it cannot weigh");
        Match tickBody = Regex.Match(mend, @"public static void Tick\(float now\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
        Check(tickBody.Success && Regex.IsMatch(tickBody.Groups["b"].Value, @"^\s*if \(now < _next\)\s*\{\s*return;\s*\}"),
            "traffic mend sources: idle, the tick costs one comparison a frame");

        // The held density manager: only its reference is compared, then written over; never read through, called or destroyed.
        Check(Regex.Matches(game, @"GameContract\.TrafficDensity\.Field\.GetValue\(").Count == 2
              && game.Contains("heldMissing = ReferenceEquals(GameContract.TrafficDensity.Field.GetValue(traffic), null);")
              && game.Contains("GameContract.TrafficDensity.Field.SetValue(traffic, fresh);"),
            "traffic mend sources: the mend compares the held density manager's reference with null and writes it over, nothing else (the test's break the other read)");
        Match mendBody = Regex.Match(game, @"public static bool Mend\([^)]*\)\s*\{(?<b>(?:[^{}]|\{(?<d>)|\}(?<-d>))*)\}");
        Check(mendBody.Success && !Regex.IsMatch(mendBody.Groups["b"].Value, @"\bheld\b|DensityGrid|Destroy\((?!fresh)"),
            "traffic mend sources: the mend never touches the old density manager and destroys only the new one if the game's start of it fails");
        Check(!Regex.IsMatch(game, @"ClearTraffic|RemoveVehicle|ClearTrafficOnArea|SetPause|\.Initialize\(|Manager\.Initialize"),
            "traffic mend sources: no car is taken off the road and the traffic is not started again: the cars on it drive on");
        Check(game.Contains("GameContract.DensityCount.Field.SetValue(fresh, carsKept);") && game.Contains("fresh.UpdateMaxCars(wanted);"),
            "traffic mend sources: the new density manager counts the cars on the road and wants what the game last asked for");
        Check(game.Contains("public static bool Running => !GameContract.TrafficMend.Off && TrafficTools.Ready;")
              && Code(File.ReadAllText(Path.Combine(scripts, @"Traffic\TrafficTools.cs"))).Contains("public static bool Ready => Street && !GoingPublic.TrafficMirrored;"),
            "traffic mend sources: nothing is mended on a Going Public guest, whose traffic is the host's, nor indoors or with the contract broken");
        Check(Regex.Matches(contract, @"Field\(TrafficMend, Plugins, ""GleyTrafficSystem\.DensityManager"", ""gridManager""").Count == 1
              && Regex.Matches(game, @"GameContract\.DensityGrid").Count == 1 && game.Contains("GameContract.DensityGrid.Field.SetValue(held, null);"),
            "traffic mend sources: the density manager's grid link is written only by the test's break");

        // The test, the texts and What's new.
        Check(start.Contains(".AddButton(\"reallife_test_traffic_mend\", TrafficMend.Test)") && features.Contains("\"reallife_test_traffic_mend\")"),
            "traffic mend sources: the test is on the options page and in the traffic feature's list");
        string locales = Path.GetFullPath(Path.Combine(scripts, @"..\Locales"));
        string[] keys = { "reallife_contract_part_traffic_mend", "reallife_test_traffic_mend", "reallife_test_traffic_mend_done" };
        string[] marks = { "Fixed: ", "Corrigido: ", "已修复：", "Behoben: ", "Corrigé : ", "Corregido: ", "Исправлено: ", "Opgelost: ", "Düzeltildi: " };
        string[] codes = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        string[] words = { "traffic", "trânsito", "车流", "Stadtverkehr", "circulation", "tráfico", "трафик", "verkeer", "trafiği" };
        string[] gone = { "empties", "esvazia", "彻底消失", "leert sich", "disparaît plus", "desaparece", "исчезает", "verdwijnt", "kaybolmuyor" };
        for (int c = 0; c < codes.Length; c++)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(locales, codes[c] + ".json")));
            if (c < 3)
            {
                foreach (string key in keys)
                {
                    Check(doc.RootElement.TryGetProperty(key, out JsonElement value) && value.GetString().Length > 0 && value.GetString().Length <= 240,
                        "traffic mend texts: " + key + " is in " + codes[c] + ".json, at most 240 characters");
                }
            }
            // Any What's new row: the line is in the next row until a release numbers it into its own.
            bool line = false;
            foreach (JsonProperty row in doc.RootElement.EnumerateObject())
            {
                if (row.Name.StartsWith("reallife_changes_", StringComparison.Ordinal) && !row.Name.EndsWith("_title", StringComparison.Ordinal)
                    && row.Value.ValueKind == JsonValueKind.String)
                {
                    string text = row.Value.GetString();
                    int at = text.IndexOf(marks[c] + (c == 2 ? "城市车流" : string.Empty), StringComparison.Ordinal);
                    while (at >= 0 && !line)
                    {
                        int end = text.IndexOf(marks[c], at + marks[c].Length, StringComparison.Ordinal);
                        string piece = end < 0 ? text.Substring(at) : text.Substring(at, end - at);
                        line = piece.Contains(words[c]) && piece.Contains(gone[c]);
                        at = end;
                    }
                }
            }
            Check(line, "traffic mend texts: a What's new row has the Fixed line for the emptied traffic in " + codes[c]);
        }
    }
}
