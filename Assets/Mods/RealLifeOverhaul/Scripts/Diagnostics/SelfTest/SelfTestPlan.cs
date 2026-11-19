using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>One step of a plan as read: its kind, its label, its time limit and its arguments, all checked against the table.</summary>
    public sealed class SelfTestStepSpec
    {
        public string Kind;
        public string Label;       // the name in the results: the plan's own, else the kind with its place in the plan
        public int Limit;          // seconds of real time
        public readonly Dictionary<string, string> Args = new Dictionary<string, string>(StringComparer.Ordinal);

        public string Arg(string name, string fallback = null) => Args.TryGetValue(name, out string value) ? value : fallback;

        public double Number(string name, double fallback)
        {
            return Args.TryGetValue(name, out string value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) ? n : fallback;
        }

        public bool Flag(string name, bool fallback)
        {
            return Args.TryGetValue(name, out string value) ? value == "true" : fallback;
        }
    }

    /// <summary>A plan as read: the steps in order, the output folder and what to do at the end.</summary>
    public sealed class SelfTestPlan
    {
        public string Name = "plan";
        public string Output;
        public bool Quit;
        public bool KeepOptions;
        public readonly List<SelfTestStepSpec> Steps = new List<SelfTestStepSpec>();
    }

    /// <summary>
    /// Reads and checks a plan file, game-free so the offline checks can try every fault of one. A plan is a JSON object:
    /// "kind": "rlo-selftest" (so no other file of the data folder is ever taken for one), "name", "output" (a folder), "quit",
    /// "keep_options" and "steps": a list of objects with "do" (a kind of SelfTestKinds), the kind's arguments, and optionally
    /// "label" (the step's name in the results) and "limit". A step {"do":"repeat","times":N,"steps":[...]} stands for its steps N times. Every fault is one line
    /// in <c>errors</c>; a plan with any is not run. COST: one parse when a plan file exists; nothing otherwise.
    /// </summary>
    public static class SelfTestPlanReader
    {
        public const string Magic = "rlo-selftest";
        public const int MostSteps = 3000;     // after repeats are written out
        public const int MostRepeat = 50;
        public const int MostDepth = 3;
        public const int MostLimit = 7200;     // seconds a step may be given

        public static SelfTestPlan Read(string json, List<string> errors)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json ?? string.Empty);
            }
            catch (Exception e)
            {
                errors.Add("the plan is not JSON: " + FirstLine(e.Message));
                return null;
            }
            if (!(root["kind"] is JValue kind) || (kind.Value as string) != Magic)
            {
                errors.Add("the plan's \"kind\" must be \"" + Magic + "\"");
                return null;
            }
            var plan = new SelfTestPlan
            {
                Name = SelfTestRules.SafeName(root.Value<string>("name"), "plan"),
                Output = root.Value<string>("output"),
                Quit = root["quit"] == null || root.Value<bool>("quit"),
                KeepOptions = root["keep_options"] != null && root.Value<bool>("keep_options")
            };
            if (string.IsNullOrWhiteSpace(plan.Output))
            {
                errors.Add("the plan has no \"output\" folder");
            }
            if (!(root["steps"] is JArray steps) || steps.Count == 0)
            {
                errors.Add("the plan has no \"steps\"");
                return errors.Count == 0 ? plan : null;
            }
            int attempted = 0;
            Expand(steps, plan, errors, 0, string.Empty, ref attempted);
            if (plan.Steps.Count > MostSteps)
            {
                errors.Add("the plan has " + plan.Steps.Count + " steps after its repeats; the most is " + MostSteps);
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                if (!seen.Add(plan.Steps[i].Label))
                {
                    plan.Steps[i].Label += "_" + (i + 1);   // two steps of one name keep their files apart
                }
            }
            CheckNames(plan, errors);
            return errors.Count == 0 ? plan : null;
        }

        // A "from" names the label of an earlier step of the plan (the label as the results spell it: letters, digits, dash and underscore),
        // and a "since" names a moment an earlier measure step marked: a name that nothing carries would only be found when the plan ran.
        private static void CheckNames(SelfTestPlan plan, List<string> errors)
        {
            var labels = new HashSet<string>(StringComparer.Ordinal);
            var marks = new HashSet<string>(StringComparer.Ordinal);
            var variables = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                SelfTestStepSpec step = plan.Steps[i];
                string from = step.Arg("from");
                if (from != null && !labels.Contains(from))
                {
                    // inside a repeat every label of a round carries the round's suffix: a "from" written without it means the same round's step
                    int cut = step.Label.Length;
                    while (cut > 2 && char.IsDigit(step.Label[cut - 1]))
                    {
                        cut--;
                    }
                    string suffix = cut > 2 && step.Label[cut - 1] == 'r' && step.Label[cut - 2] == '_' ? step.Label.Substring(cut - 2) : string.Empty;
                    if (suffix.Length > 0 && labels.Contains(from + suffix))
                    {
                        step.Args["from"] = from + suffix;
                        from = null;
                    }
                }
                if (from != null && !labels.Contains(from))
                {
                    errors.Add("step " + (i + 1) + " (" + step.Kind + "): from '" + from + "' is the label of no earlier step (labels are written with letters, digits, dash and underscore: "
                               + SelfTestRules.SafeName(from, "?") + "?)");
                }
                string since = step.Arg("since");
                if (since != null && !marks.Contains(since))
                {
                    errors.Add("step " + (i + 1) + " (" + step.Kind + "): since '" + since + "' was marked by no earlier measure step");
                }
                foreach (KeyValuePair<string, string> pair in step.Args)
                {
                    int open = pair.Value.IndexOf('{');
                    while (open >= 0)
                    {
                        int close = pair.Value.IndexOf('}', open);
                        string name = close > open ? pair.Value.Substring(open + 1, close - open - 1) : null;
                        if (name == null)
                        {
                            break;
                        }
                        if (!variables.Contains(name))
                        {
                            errors.Add("step " + (i + 1) + " (" + step.Kind + "): {" + name + "} is set by no earlier find_place, call or read step (its as)");
                        }
                        open = pair.Value.IndexOf('{', close);
                    }
                }
                if ((step.Kind == "find_place" || step.Kind == "call" || step.Kind == "read") && step.Arg("as") != null)
                {
                    variables.Add(step.Arg("as"));
                }
                labels.Add(step.Label);
                if (step.Arg("mark") != null)
                {
                    marks.Add(step.Arg("mark"));
                }
            }
        }

        private static void Expand(JArray steps, SelfTestPlan plan, List<string> errors, int depth, string suffix, ref int attempted)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                attempted++;
                if (!(steps[i] is JObject step))
                {
                    errors.Add("step " + attempted + " is not an object");
                    continue;
                }
                string kind = step.Value<string>("do");
                if (kind == "repeat")
                {
                    int times = step["times"] != null && step["times"].Type == JTokenType.Integer ? step.Value<int>("times") : 0;
                    if (times < 1 || times > MostRepeat || !(step["steps"] is JArray inner) || inner.Count == 0 || depth >= MostDepth)
                    {
                        errors.Add("a repeat needs \"times\" from 1 to " + MostRepeat + ", its own \"steps\", and sits at most " + MostDepth + " deep");
                        continue;
                    }
                    for (int t = 1; t <= times; t++)
                    {
                        Expand(inner, plan, errors, depth + 1, suffix + "_r" + t, ref attempted);
                    }
                    continue;
                }
                SelfTestStepSpec spec = One(step, kind, attempted, errors, suffix);
                if (spec != null)
                {
                    plan.Steps.Add(spec);
                }
            }
        }

        private static SelfTestStepSpec One(JObject step, string kindName, int number, List<string> errors, string suffix)
        {
            string where = "step " + number + (kindName != null ? " (" + kindName + ")" : string.Empty);
            SelfTestKind kind = SelfTestKinds.Find(kindName);
            if (kind == null)
            {
                errors.Add(where + ": no such kind of step");
                return null;
            }
            var spec = new SelfTestStepSpec { Kind = kind.Name, Limit = kind.Limit };
            string label = step.Value<string>("label");
            spec.Label = SelfTestRules.SafeName(label, kind.Name) + suffix;
            bool ok = true;
            foreach (KeyValuePair<string, JToken> pair in step)
            {
                string key = pair.Key;
                if (key == "do" || key == "label")
                {
                    continue;
                }
                if (key == "limit")
                {
                    if (pair.Value.Type != JTokenType.Integer || pair.Value.Value<int>() < 1 || pair.Value.Value<int>() > MostLimit)
                    {
                        errors.Add(where + ": limit is whole seconds from 1 to " + MostLimit);
                        ok = false;
                    }
                    else
                    {
                        spec.Limit = pair.Value.Value<int>();
                    }
                    continue;
                }
                string type = kind.TypeOf(key);
                if (type == null)
                {
                    errors.Add(where + ": takes no \"" + key + "\"");
                    ok = false;
                    continue;
                }
                string text = Value(pair.Value, type);
                if (text == null)
                {
                    errors.Add(where + ": \"" + key + "\" must be " + TypeWords(type));
                    ok = false;
                    continue;
                }
                spec.Args[key] = text;
            }
            for (int i = 0; i < kind.Required.Length; i++)
            {
                string name = kind.Required[i].Substring(0, kind.Required[i].IndexOf(':'));
                if (!spec.Args.ContainsKey(name))
                {
                    errors.Add(where + ": needs \"" + name + "\"");
                    ok = false;
                }
            }
            if (ok && kind.Name == "wait_seconds" && !step.ContainsKey("limit"))
            {
                spec.Limit = (int)Math.Min(MostLimit, spec.Number("seconds", 0) + 30);
            }
            if (ok && kind.Name == "measure" && !spec.Args.ContainsKey("mark") && !spec.Args.ContainsKey("since"))
            {
                errors.Add(where + ": needs a \"mark\" to note or a \"since\" to measure from");
                ok = false;
            }
            return ok ? spec : null;
        }

        // An argument's value as text in the invariant culture, or null when it is not the type asked for.
        private static string Value(JToken token, string type)
        {
            switch (type)
            {
                case "s":
                    return token.Type == JTokenType.String || token.Type == JTokenType.Integer || token.Type == JTokenType.Float || token.Type == JTokenType.Boolean
                        ? Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture)?.Replace("True", "true").Replace("False", "false")
                        : null;
                case "i":
                    return token.Type == JTokenType.Integer ? token.Value<long>().ToString(CultureInfo.InvariantCulture) : null;
                case "n":
                    return token.Type == JTokenType.Integer || token.Type == JTokenType.Float ? token.Value<double>().ToString("R", CultureInfo.InvariantCulture) : null;
                case "b":
                    return token.Type == JTokenType.Boolean ? (token.Value<bool>() ? "true" : "false") : null;
            }
            return null;
        }

        private static string TypeWords(string type)
        {
            switch (type)
            {
                case "i": return "a whole number";
                case "n": return "a number";
                case "b": return "true or false";
                default: return "words";
            }
        }

        private static string FirstLine(string text)
        {
            int cut = text != null ? text.IndexOf('\n') : -1;
            return cut > 0 ? text.Substring(0, cut).TrimEnd() : text;
        }

        /// <summary>The plan's steps one to a line, for the log and the README's examples.</summary>
        public static string Outline(SelfTestPlan plan)
        {
            var sb = new StringBuilder(plan.Steps.Count * 24);
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                sb.Append(i + 1).Append(". ").Append(plan.Steps[i].Label).Append('\n');
            }
            return sb.ToString();
        }
    }
}
