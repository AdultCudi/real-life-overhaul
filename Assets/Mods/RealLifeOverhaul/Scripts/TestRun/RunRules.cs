using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.TestRun
{
    /// <summary>Where the test run puts the player before a check runs. The run finds the place itself: nobody looks for one.</summary>
    public enum RunPlace
    {
        Here,            // as the player stands, indoors or out
        Outdoors,        // out of any building and vehicle, where they stand
        UptownLot,       // at the front of an open lot of Uptown's first block that nothing stands on, facing into it
        BesideBuilding,  // at the street door of a building of three to six storeys, facing it
        OpenGround,      // at the front of an open lot on the grass round the airport, facing into it; each check its own lot
        PavementEdge,    // on the pavement at the edge of the airport's grass, the same spot for every walking check
        TestBuilding     // at the door of the test building, 1 Proving Lane
    }

    /// <summary>When in the run a check runs. The run asks for one load, between <see cref="ForTheLoad"/> and <see cref="AfterLoad"/>.</summary>
    public enum RunPhase
    {
        BeforeLoad,      // in the session the run was started in
        ForTheLoad,      // last before the load: what stands a part down in play, and what asks for something at the next load
        AfterLoad,       // after the one load
        End              // the put backs, last; also run when the run is stopped
    }

    /// <summary>What became of one check.</summary>
    public enum RunResult
    {
        Waiting,
        Pass,
        Fail,            // its line said FAIL, or something other than what it must say
        Refused,         // the test said why it did not start
        TimedOut,        // no line within its time
        Fault,           // it threw: logged, and the run went on
        NothingHere,     // the save holds nothing of what the test brings forward (no project, no company): not the mod's fault
        Stopped          // the run was stopped while it waited
    }

    /// <summary>
    /// One check of the run: the test it presses, the start of the one log line that decides it, where the player must be
    /// and when. A feature joins the run with one <c>.Check(...)</c> in its guide (Meta/Features.cs); the city expansion's
    /// own checks are the table in <see cref="RunPlan"/>. Game-free.
    /// </summary>
    public sealed class RunCheck
    {
        public readonly string Id;          // unique in the run: a test's label key, or "testrun_" and a step of the run's own
        public readonly string Feature;     // the feature's id (Meta/FeatureList)
        public readonly string Line;        // the start of the line that decides it, after the logger's "[Mod:...] "
        public RunPlace Place;
        public RunPhase Phase;
        public float Seconds;               // s of the game running that the line is waited for
        public string Pass = RunRules.PassWord;   // what the line must say; null: written is enough (a line with no verdict)
        public string Fails;                // a line beginning so is a FAIL at once (a stand-down said instead of the line), or null
        public bool AtLoad;                 // written by the load itself: looked for among the lines heard since the load began
        public string Look;                 // a locale key: something for the eyes here, told as the check begins
        public string Eyes;                 // the checklist's line for it in Player.log: what to look at, and where the run left the player
        public Action Run;                  // the test's work, called without any input; null for a line the load writes
        public Action Undo;                 // what puts back what it changed, called once it is decided; or null
        public bool UndoOnPass;             // its put back only after a PASS (a refused test gave back what it took itself)
        public int Order;                   // the order it was registered in

        public RunCheck(string id, string feature, string line, RunPlace place, RunPhase phase, float seconds)
        {
            Id = id;
            Feature = feature;
            Line = line;
            Place = place;
            Phase = phase;
            Seconds = seconds;
        }

        /// <summary>The words before " check (" (or the whole start): lines of the same family that say FAIL fail it at once.</summary>
        public string Family
        {
            get
            {
                if (Line == null)
                {
                    return string.Empty;
                }
                int at = Line.IndexOf(" check (", StringComparison.Ordinal);
                return at > 0 ? Line.Substring(0, at + " check (".Length) : Line;
            }
        }
    }

    /// <summary>One check's outcome, kept across the load.</summary>
    public sealed class RunOutcome
    {
        public string Id;
        public RunResult Result;
        public string Line;                 // the line that decided it, or why there was none
    }

    /// <summary>
    /// The arithmetic of the test run: whether a log line is a check's, what it says, the order the checks run in, which
    /// lot of the airport's grass a check is given, what is carried over the load in PlayerPrefs, and the summary. No
    /// Unity or game types, so EconomyCheck runs it as it ships. Nothing here runs in play unless a run is under way.
    /// </summary>
    public static class RunRules
    {
        public const string PassWord = "PASS";
        public const string FailWord = "FAIL";
        public const string RefusedMark = " test: not started (";   // Core/TestRefusal's line
        public const string Version = "testrun1";                  // the carry-over's first line: another is never read
        public const int MostLine = 400;                           // characters of a deciding line kept across the load
        public const double MostHoursCarried = 12.0;               // hours a run may wait for its load before it is forgotten
        public const float DefaultSeconds = 60f;                   // s a check's line is waited for unless it says otherwise

        /// <summary>
        /// Refusals that mean the save holds nothing for the test to bring forward (no project, no company, no tender): the
        /// mod is not at fault, so the summary lists them apart, and no FAIL is counted.
        /// </summary>
        public static readonly string[] NothingKeys =
        {
            "reallife_bld_test_nothing_back", "reallife_bld_test_nothing", "reallife_bld_test_no_office", "reallife_bld_answer_no_company",
            "reallife_bld_test_no_client", "reallife_bld_test_no_bribe"
        };

        /// <summary>Refusals that pass by themselves (the copy engine still measuring the city's designs): the test is pressed again.</summary>
        public static readonly string[] RetryKeys = { "reallife_bld_designs_wait" };

        public const int MostTries = 4;          // presses of a test whose refusal passes by itself
        public const float RetrySeconds = 10f;   // s between them

        /// <summary>
        /// True when <paramref name="text"/> is the line <paramref name="prefix"/> begins, written straight or after the
        /// logger's bracketed name ("[Mod:RealLifeOverhaul] Uptown check (census): ...").
        /// </summary>
        public static bool Begins(string text, string prefix)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(prefix))
            {
                return false;
            }
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
            int close = text.Length > 0 && text[0] == '[' ? text.IndexOf("] ", StringComparison.Ordinal) : -1;
            return close > 0 && text.Length >= close + 2 + prefix.Length && string.CompareOrdinal(text, close + 2, prefix, 0, prefix.Length) == 0;
        }

        /// <summary>The line without the logger's bracketed name.</summary>
        public static string Bare(string text)
        {
            if (string.IsNullOrEmpty(text) || text[0] != '[')
            {
                return text ?? string.Empty;
            }
            int close = text.IndexOf("] ", StringComparison.Ordinal);
            return close > 0 ? text.Substring(close + 2) : text;
        }

        /// <summary>What a deciding line says: FAIL anywhere fails it; else it passes if it says what it must (or need say nothing).</summary>
        public static RunResult Judge(string text, string pass)
        {
            if (text == null)
            {
                return RunResult.Waiting;
            }
            if (text.IndexOf(FailWord, StringComparison.Ordinal) >= 0)
            {
                return RunResult.Fail;
            }
            if (pass == null)
            {
                return RunResult.Pass;
            }
            return text.IndexOf(pass, StringComparison.Ordinal) >= 0 ? RunResult.Pass : RunResult.Fail;
        }

        /// <summary>True for a test's refusal line (Core/TestRefusal), with the reason inside its brackets.</summary>
        public static bool Refusal(string text, out string reason)
        {
            reason = null;
            int at = text != null ? text.IndexOf(RefusedMark, StringComparison.Ordinal) : -1;
            if (at < 0)
            {
                return false;
            }
            int from = at + RefusedMark.Length;
            int to = text.LastIndexOf(')');
            reason = to > from ? text.Substring(from, to - from) : text.Substring(from);
            return true;
        }

        /// <summary>A refusal's outcome: nothing in the save to bring forward, one that passes by itself, or a refusal.</summary>
        public static RunResult OfRefusal(string reason, out bool retry)
        {
            retry = StartsWithAny(reason, RetryKeys);
            return StartsWithAny(reason, NothingKeys) ? RunResult.NothingHere : RunResult.Refused;
        }

        private static bool StartsWithAny(string text, string[] keys)
        {
            if (text == null)
            {
                return false;
            }
            for (int i = 0; i < keys.Length; i++)
            {
                if (text.StartsWith(keys[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when the result counts against the mod.</summary>
        public static bool Failed(RunResult result)
        {
            return result == RunResult.Fail || result == RunResult.Refused || result == RunResult.TimedOut || result == RunResult.Fault;
        }

        /// <summary>The worse of two outcomes of one check: a FAIL or a refusal heard with its line outweighs its PASS.</summary>
        public static RunResult Worse(RunResult a, RunResult b)
        {
            return Rank(b) > Rank(a) ? b : a;
        }

        private static int Rank(RunResult r)
        {
            switch (r)
            {
                case RunResult.Waiting: return 0;
                case RunResult.Pass: return 1;
                case RunResult.NothingHere: return 2;
                case RunResult.Refused: return 3;
                case RunResult.Fail: return 4;
                case RunResult.Fault: return 5;
                default: return 3;
            }
        }

        // ---------------------------------------------------------------- the order

        /// <summary>
        /// The order the checks run in: by phase; within a phase by place, so the player is moved as few times as can be
        /// (where they stand first, then out of doors, Uptown, a building's door, the airport's grass, its pavement, the
        /// test building); within a place in the order they were registered, the run's own table first. Stable.
        /// </summary>
        public static List<RunCheck> Ordered(IList<RunCheck> checks)
        {
            var list = new List<RunCheck>(checks.Count);
            for (int i = 0; i < checks.Count; i++)
            {
                if (checks[i] != null)
                {
                    list.Add(checks[i]);
                }
            }
            list.Sort((a, b) =>
            {
                int c = ((int)a.Phase).CompareTo((int)b.Phase);
                if (c == 0)
                {
                    c = ((int)a.Place).CompareTo((int)b.Place);
                }
                return c != 0 ? c : a.Order.CompareTo(b.Order);
            });
            return list;
        }

        /// <summary>Ids registered twice, for the offline check and the run's own line: the second is left out.</summary>
        public static List<string> Twice(IList<RunCheck> checks)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var twice = new List<string>();
            for (int i = 0; i < checks.Count; i++)
            {
                if (checks[i] != null && !seen.Add(checks[i].Id))
                {
                    twice.Add(checks[i].Id);
                }
            }
            return twice;
        }

        // ---------------------------------------------------------------- the airport's grass

        /// <summary>
        /// The airport lot an open-ground check is given: the <paramref name="use"/>-th of the lots the test building does
        /// not stand on (<paramref name="taken"/>, or -1), round again when they run out, and never the one kept for the
        /// walking checks (the last free one). -1 with fewer than two lots free.
        /// </summary>
        public static int OpenLot(int use, int taken, int lots)
        {
            int free = lots - (taken >= 0 && taken < lots ? 1 : 0) - 1;   // one kept for the pavement
            if (free <= 0 || use < 0)
            {
                return -1;
            }
            int want = use % free;
            for (int i = 0; i < lots; i++)
            {
                if (i == taken)
                {
                    continue;
                }
                if (want-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The airport lot kept for the walking checks: the last one the test building does not stand on; -1 with none.</summary>
        public static int PavementLot(int taken, int lots)
        {
            for (int i = lots - 1; i >= 0; i--)
            {
                if (i != taken)
                {
                    return i;
                }
            }
            return -1;
        }

        public const float FrontStep = 1.5f;   // m in front of a lot's front edge: on the pavement its door gives onto

        /// <summary>
        /// The spot before a lot's front and the way into it: the lot's middle (<paramref name="x"/>, <paramref name="z"/>),
        /// the way its street lies (<paramref name="facing"/>, degrees, 0 = +Z, as NewBuildings' MadeLot) and its depth.
        /// </summary>
        public static void Front(float x, float z, float facing, float depth, out float atX, out float atZ, out float yaw)
        {
            double turn = facing * Math.PI / 180.0;
            float reach = depth * 0.5f + FrontStep;
            atX = x + (float)Math.Sin(turn) * reach;
            atZ = z + (float)Math.Cos(turn) * reach;
            yaw = facing + 180f;
            yaw -= 360f * (float)Math.Floor(yaw / 360f);
        }

        /// <summary>The way from a point to another, in degrees round the vertical as the game turns things (0 = +Z).</summary>
        public static float Toward(float fromX, float fromZ, float toX, float toZ)
        {
            float yaw = (float)(Math.Atan2(toX - fromX, toZ - fromZ) * 180.0 / Math.PI);
            return yaw < 0f ? yaw + 360f : yaw;
        }

        public const float StoreyMetres = 3.5f;   // m a storey of the game's buildings stands, near enough to count them
        public const int FewestStoreys = 3;       // the scaffolding test's building: three to six storeys
        public const int MostStoreys = 6;

        /// <summary>True when a building's top, metres above the street at its door, is that of three to six storeys.</summary>
        public static bool Storeys(float top)
        {
            return top >= (FewestStoreys - 0.5f) * StoreyMetres && top <= (MostStoreys + 0.5f) * StoreyMetres;
        }

        // ---------------------------------------------------------------- across the load

        /// <summary>
        /// What the run keeps in PlayerPrefs over its load, as text: the version, when it began (UTC ticks), what it has to
        /// put back (key=value), then one line per check decided. Tabs and line breaks in a line become spaces.
        /// </summary>
        public static string Carry(long beganTicks, IDictionary<string, string> duties, IList<RunOutcome> outcomes)
        {
            var text = new StringBuilder(4096);
            text.Append(Version).Append('\n').Append(beganTicks.ToString(CultureInfo.InvariantCulture)).Append('\n');
            bool first = true;
            if (duties != null)
            {
                var keys = new List<string>(duties.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (string key in keys)
                {
                    text.Append(first ? string.Empty : "\t").Append(Plain(key)).Append('=').Append(Plain(duties[key]));
                    first = false;
                }
            }
            text.Append('\n');
            for (int i = 0; outcomes != null && i < outcomes.Count; i++)
            {
                RunOutcome o = outcomes[i];
                string line = Plain(o.Line);
                if (line.Length > MostLine)
                {
                    line = line.Substring(0, MostLine);
                }
                text.Append(Plain(o.Id)).Append('\t').Append(((int)o.Result).ToString(CultureInfo.InvariantCulture)).Append('\t').Append(line).Append('\n');
            }
            return text.ToString();
        }

        /// <summary>The carry-over read back; false for anything else (nothing kept, another version, a damaged line).</summary>
        public static bool ReadCarry(string text, out long beganTicks, Dictionary<string, string> duties, List<RunOutcome> outcomes)
        {
            beganTicks = 0;
            duties?.Clear();
            outcomes?.Clear();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string[] lines = text.Split('\n');
            if (lines.Length < 3 || lines[0] != Version || !long.TryParse(lines[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out beganTicks))
            {
                return false;
            }
            if (lines[2].Length > 0)
            {
                foreach (string pair in lines[2].Split('\t'))
                {
                    int eq = pair.IndexOf('=');
                    if (eq <= 0)
                    {
                        return false;
                    }
                    if (duties != null)
                    {
                        duties[pair.Substring(0, eq)] = pair.Substring(eq + 1);
                    }
                }
            }
            for (int i = 3; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                {
                    continue;
                }
                string[] f = lines[i].Split('\t');
                if (f.Length != 3 || f[0].Length == 0 || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int code)
                    || code < (int)RunResult.Waiting || code > (int)RunResult.Stopped)
                {
                    return false;
                }
                outcomes?.Add(new RunOutcome { Id = f[0], Result = (RunResult)code, Line = f[2] });
            }
            return true;
        }

        /// <summary>True while a carried run is young enough to carry on: begun under <see cref="MostHoursCarried"/> hours ago, and not in the future.</summary>
        public static bool Fresh(long beganTicks, long nowTicks)
        {
            return beganTicks > 0 && nowTicks >= beganTicks && (nowTicks - beganTicks) < (long)(MostHoursCarried * TimeSpan.TicksPerHour);
        }

        private static string Plain(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        }

        // ---------------------------------------------------------------- the summary

        /// <summary>A check's name in the summary: the line it is decided by, without its colon and what may follow.</summary>
        public static string NameOf(RunCheck check)
        {
            string line = check.Line ?? check.Id;
            return line.TrimEnd(' ', ':');
        }

        public const string Header = "City expansion test run: ";

        /// <summary>
        /// The Player.log block: "City expansion test run: N of M PASS; failed: ...", one line per check with its result and
        /// line, then the checklist for the eyes (at most five lines, each saying where the run left the player for it).
        /// M counts the checks that ran; those with nothing to try in this save are named apart.
        /// </summary>
        public static string Summary(IList<RunCheck> order, IList<RunOutcome> outcomes, IList<string> eyes, bool stopped, out int passed, out int ran, out List<string> failed)
        {
            passed = 0;
            ran = 0;
            failed = new List<string>();
            var nothing = new List<string>();
            var rows = new StringBuilder(8192);
            for (int i = 0; i < order.Count; i++)
            {
                RunOutcome o = Find(outcomes, order[i].Id);
                RunResult r = o != null ? o.Result : RunResult.Waiting;
                string name = NameOf(order[i]);
                if (r == RunResult.Pass)
                {
                    passed++;
                    ran++;
                }
                else if (Failed(r))
                {
                    ran++;
                    failed.Add(name);
                }
                else if (r == RunResult.NothingHere)
                {
                    nothing.Add(name);
                }
                rows.Append("\n  ").Append(Word(r).PadRight(8)).Append(name).Append(": ").Append(o != null && !string.IsNullOrEmpty(o.Line) ? o.Line : "not run");
            }
            var text = new StringBuilder(rows.Length + 1024);
            text.Append(Header).Append(passed.ToString(CultureInfo.InvariantCulture)).Append(" of ").Append(ran.ToString(CultureInfo.InvariantCulture)).Append(" PASS");
            text.Append(failed.Count > 0 ? "; failed: " + string.Join(", ", failed) : "; none failed");
            if (nothing.Count > 0)
            {
                text.Append("; nothing to try in this save: ").Append(string.Join(", ", nothing));
            }
            if (stopped)
            {
                text.Append("; stopped before the end");
            }
            text.Append('.').Append(rows);
            if (eyes != null && eyes.Count > 0)
            {
                text.Append("\n  For your eyes (where the run left you for each):");
                for (int i = 0; i < eyes.Count && i < MostEyes; i++)
                {
                    text.Append("\n  ").Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(". ").Append(eyes[i]);
                }
            }
            return text.ToString();
        }

        public const int MostEyes = 5;   // lines of the checklist for the eyes

        /// <summary>The checklist for the eyes: each check with something to look at that stood (passed or failed, not refused), in the run's order, five at most.</summary>
        public static List<string> Eyes(IList<RunCheck> order, IList<RunOutcome> outcomes)
        {
            var eyes = new List<string>(MostEyes);
            for (int i = 0; i < order.Count && eyes.Count < MostEyes; i++)
            {
                RunOutcome o = order[i].Eyes != null ? Find(outcomes, order[i].Id) : null;
                if (o != null && (o.Result == RunResult.Pass || o.Result == RunResult.Fail))
                {
                    eyes.Add(order[i].Eyes);
                }
            }
            return eyes;
        }

        /// <summary>The word a check's row begins with.</summary>
        public static string Word(RunResult r)
        {
            switch (r)
            {
                case RunResult.Pass: return "PASS";
                case RunResult.Fail: return "FAIL";
                case RunResult.Refused: return "REFUSED";
                case RunResult.TimedOut: return "NO LINE";
                case RunResult.Fault: return "FAULT";
                case RunResult.NothingHere: return "NOTHING";
                case RunResult.Stopped: return "STOPPED";
                default: return "NOT RUN";
            }
        }

        /// <summary>A list of names that fits <paramref name="most"/> characters: as many as fit, then "and N more".</summary>
        public static string Short(IList<string> names, int most)
        {
            if (names == null || names.Count == 0)
            {
                return string.Empty;
            }
            var text = new StringBuilder(most + 32);
            text.Append(names[0]);
            for (int i = 1; i < names.Count; i++)
            {
                string next = ", " + names[i];
                string more = " and " + (names.Count - i - 1).ToString(CultureInfo.InvariantCulture) + " more";   // what would follow it, were it the last to fit
                bool last = i == names.Count - 1;
                if (text.Length + next.Length + (last ? 0 : more.Length) > most)
                {
                    return text.Append(" and ").Append((names.Count - i).ToString(CultureInfo.InvariantCulture)).Append(" more").ToString();
                }
                text.Append(next);
            }
            return text.ToString();
        }

        /// <summary>The outcome of a check by its id, or null.</summary>
        public static RunOutcome Find(IList<RunOutcome> outcomes, string id)
        {
            for (int i = 0; outcomes != null && i < outcomes.Count; i++)
            {
                if (outcomes[i].Id == id)
                {
                    return outcomes[i];
                }
            }
            return null;
        }
    }
}
