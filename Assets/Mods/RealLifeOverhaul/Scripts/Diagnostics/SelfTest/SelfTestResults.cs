using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The result of one step: what it came to, in words, with its time, its pictures and the log errors that came while it ran.</summary>
    public sealed class SelfTestStepResult
    {
        public string Name;
        public string Kind;
        public SelfTestOutcome Outcome;
        public string Words;
        public double Seconds;
        public readonly List<string> Shots = new List<string>();     // file names in the output folder
        public readonly List<string> LogErrors = new List<string>(); // new error lines of the game's log, each with whose it is
    }

    /// <summary>
    /// The results file: <c>results.json</c> in the output folder, written whole after every step so a crash still leaves
    /// what ran. Game-free (checked offline). COST: one small file written a step.
    /// </summary>
    public sealed class SelfTestResults
    {
        public string Plan;
        public string Started;       // as text, in UTC
        public bool Done;
        public string GameBuild;
        public string ModVersion;
        public readonly List<string> Mods = new List<string>();   // the loaded mods as the game reports them: "name (id) version"
        public readonly List<string> Notes = new List<string>();  // what ran outside a step: options put back, a plan refused
        public readonly List<SelfTestStepResult> Steps = new List<SelfTestStepResult>();
        public int Total;            // the plan's steps, so a half-written file says how far it got

        public int Count(SelfTestOutcome outcome)
        {
            int n = 0;
            for (int i = 0; i < Steps.Count; i++)
            {
                n += Steps[i].Outcome == outcome ? 1 : 0;
            }
            return n;
        }

        public string DoneLine() => SelfTestRules.DoneLine(Count(SelfTestOutcome.Pass), Count(SelfTestOutcome.Fail));

        public string ToJson()
        {
            var sb = new StringBuilder(2048 + Steps.Count * 300);
            using (var sw = new StringWriter(sb, CultureInfo.InvariantCulture))
            using (var w = new JsonTextWriter(sw) { Formatting = Formatting.Indented })
            {
                w.WriteStartObject();
                w.WritePropertyName("plan"); w.WriteValue(Plan);
                w.WritePropertyName("started_utc"); w.WriteValue(Started);
                w.WritePropertyName("done"); w.WriteValue(Done);
                w.WritePropertyName("game_build"); w.WriteValue(GameBuild);
                w.WritePropertyName("mod_version"); w.WriteValue(ModVersion);
                w.WritePropertyName("steps_planned"); w.WriteValue(Total);
                w.WritePropertyName("passed"); w.WriteValue(Count(SelfTestOutcome.Pass));
                w.WritePropertyName("failed"); w.WriteValue(Count(SelfTestOutcome.Fail));
                w.WritePropertyName("skipped"); w.WriteValue(Count(SelfTestOutcome.Skipped));
                Strings(w, "mods", Mods);
                Strings(w, "notes", Notes);
                w.WritePropertyName("steps");
                w.WriteStartArray();
                for (int i = 0; i < Steps.Count; i++)
                {
                    SelfTestStepResult s = Steps[i];
                    w.WriteStartObject();
                    w.WritePropertyName("name"); w.WriteValue(s.Name);
                    w.WritePropertyName("kind"); w.WriteValue(s.Kind);
                    w.WritePropertyName("outcome"); w.WriteValue(s.Outcome == SelfTestOutcome.Pass ? "pass" : s.Outcome == SelfTestOutcome.Fail ? "fail" : "skipped");
                    w.WritePropertyName("words"); w.WriteValue(s.Words);
                    w.WritePropertyName("seconds"); w.WriteValue(Math.Round(s.Seconds, 2));
                    Strings(w, "screenshots", s.Shots);
                    Strings(w, "log_errors", s.LogErrors);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            return sb.ToString();
        }

        private static void Strings(JsonWriter w, string name, List<string> list)
        {
            w.WritePropertyName(name);
            w.WriteStartArray();
            for (int i = 0; i < list.Count; i++)
            {
                w.WriteValue(list[i]);
            }
            w.WriteEndArray();
        }

        /// <summary>Writes the file through a temporary name and a move, so a reader never meets half of it.</summary>
        public void Write(string folder)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "results.json");
            string temp = path + ".tmp";
            File.WriteAllText(temp, ToJson(), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temp, path);
        }
    }
}
