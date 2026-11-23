using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IoPath = System.IO.Path;
using Newtonsoft.Json.Linq;
using RealLifeOverhaul.SelfTest;

// The in-game self-test (Scripts/Diagnostics/SelfTest), a developer diagnostic that runs a plan of steps with nobody at the keyboard:
// the plan's reading and checking, the step table, the results file, and the rule that nothing runs, is read or is made without the
// trigger file. Offline: --selftest. What needs the game: that a step does what its words say (the owner's first real run).
internal static partial class Program
{
    private static void SelfTestAll()
    {
        SelfTestGate();
        SelfTestPlans();
        SelfTestTable();
        SelfTestResultsFile();
        SelfTestSources();
        SelfTestShippedPlans();
    }

    private static string SelfTestMod(string relative)
    {
        string path = IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts", relative));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static List<string> SelfTestFiles()
    {
        string folder = IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Diagnostics\SelfTest"));
        var list = new List<string>();
        if (Directory.Exists(folder))
        {
            list.AddRange(Directory.GetFiles(folder, "*.cs"));
        }
        return list;
    }

    // The trigger: no file, nothing; a stale file, taken away unread; a fresh one, read.
    private static void SelfTestGate()
    {
        int exists = 0, written = 0;
        var now = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        SelfTestRules.Verdict none = SelfTestRules.Look(@"C:\data", p => { exists++; return false; }, p => { written++; return now; }, now, out string path);
        Check(none == SelfTestRules.Verdict.Absent && path == null, "selftest: with no trigger file nothing runs and no path is given");
        Check(exists == 1 && written == 0, "selftest: with no trigger file the game's start costs one existence test and reads nothing else (" + exists + " tests, " + written + " ages)");
        SelfTestRules.Verdict fresh = SelfTestRules.Look(@"C:\data", p => true, p => now.AddMinutes(-5), now, out path);
        Check(fresh == SelfTestRules.Verdict.Run && path != null && path.Replace('\\', '/').EndsWith("RealLifeOverhaul/selftest.json", StringComparison.Ordinal), "selftest: a fresh trigger file in the mod's folder of the data folder is run");
        Check(SelfTestRules.Look(@"C:\data", p => true, p => now.AddMinutes(-(SelfTestRules.StaleMinutes + 1)), now, out path) == SelfTestRules.Verdict.Stale, "selftest: a trigger file left more than " + SelfTestRules.StaleMinutes + " minutes ago is stale, so a player's session never meets one");
        Check(SelfTestRules.Look(null, p => true, p => now, now, out path) == SelfTestRules.Verdict.Absent && SelfTestRules.TriggerPath("") == null, "selftest: no data folder, no trigger");
        Check(SelfTestRules.OutputFolder(@"C:\data", @"D:\out\a") == @"D:\out\a" && SelfTestRules.OutputFolder(@"C:\data", "x").Replace('\\', '/').EndsWith("RealLifeOverhaul/selftest/x", StringComparison.Ordinal) && SelfTestRules.OutputFolder(@"C:\data", " ") == null,
            "selftest: an absolute output folder is kept, a relative one goes under the self-test's own folder, none is none");
        Check(SelfTestRules.SafeName("a b/../c", "z") == "a_b____c", "selftest: a name is made safe for a file");
        Check(SelfTestRules.SafeName("", "z") == "z" && SelfTestRules.SafeName(new string('x', 99), "z").Length == 40, "selftest: an empty name is the fallback and a long one is cut at 40");
        Check(SelfTestRules.Owner("NullReferenceException at RealLifeOverhaul.Ui.X", null) == SelfTestRules.OwnerRlo && SelfTestRules.Owner("x", "at Real Life Overhaul.Y") == SelfTestRules.OwnerRlo
              && SelfTestRules.Owner("a game line", "at BigAmbitions.Z") == SelfTestRules.OwnerOther, "selftest: a log line is RLO's by its words or its stack, else the game's or another mod's");
        Check(SelfTestRules.SpanOk("1d") && SelfTestRules.SpanOk("2d5h30m") && SelfTestRules.SpanOk("45m") && !SelfTestRules.SpanOk("d") && !SelfTestRules.SpanOk("5") && !SelfTestRules.SpanOk("1x") && !SelfTestRules.SpanOk(""),
            "selftest: a span is whole numbers each followed by d, h or m");
        Check(SelfTestRules.DoneLine(7, 2) == "SELFTEST DONE passed 7 failed 2", "selftest: the last line of a run reads 'SELFTEST DONE passed N failed M'");
    }

    private static SelfTestPlan SelfTestRead(string json, out List<string> errors)
    {
        errors = new List<string>();
        return SelfTestPlanReader.Read(json, errors);
    }

    private static bool SelfTestRefused(string json, string word)
    {
        SelfTestPlan plan = SelfTestRead(json, out List<string> errors);
        return plan == null && errors.Count > 0 && errors.Exists(e => e.IndexOf(word, StringComparison.Ordinal) >= 0);
    }

    // The plan: read, and every fault of one is a line that refuses it.
    private static void SelfTestPlans()
    {
        string ok = "{\"kind\":\"rlo-selftest\",\"name\":\"t\",\"output\":\"D:/o\",\"steps\":[{\"do\":\"wait_menu\"},{\"do\":\"load_save\",\"save\":\"selftest-a\",\"limit\":90},{\"do\":\"open_screen\",\"id\":\"contacts\",\"shot\":false,\"settle\":2.5}]}";
        SelfTestPlan plan = SelfTestRead(ok, out List<string> errors);
        Check(plan != null && errors.Count == 0 && plan.Steps.Count == 3 && plan.Name == "t" && plan.Output == "D:/o" && plan.Quit && !plan.KeepOptions, "selftest plan: a plan is read: its steps, name, output, quit on by default");
        Check(plan != null && plan.Steps[0].Limit == SelfTestKinds.Find("wait_menu").Limit && plan.Steps[1].Limit == 90, "selftest plan: a step has its kind's time limit unless the plan names one");
        Check(plan != null && plan.Steps[2].Flag("shot", true) == false && Math.Abs(plan.Steps[2].Number("settle", 0) - 2.5) < 1e-9 && plan.Steps[1].Arg("save") == "selftest-a", "selftest plan: arguments are read as the kind types them");
        Check(plan != null && plan.Steps[0].Label == "wait_menu" && plan.Steps[2].Label == "open_screen", "selftest plan: a step with no label is called by its kind");
        plan = SelfTestRead("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"save_game\",\"name\":\"selftest-my\",\"label\":\"first save\"},{\"do\":\"screenshot\",\"name\":\"city\"}]}", out errors);
        Check(plan != null && plan.Steps[0].Label == "first_save" && plan.Steps[0].Arg("name") == "selftest-my" && plan.Steps[1].Arg("name") == "city" && plan.Steps[1].Label == "screenshot",
            "selftest plan: a step's label names it in the results and its name argument (a save, a picture, a business) is its own");

        Check(SelfTestRefused("not json at all", "not JSON"), "selftest plan: text that is not JSON is refused");
        Check(SelfTestRefused("{\"kind\":\"other\",\"output\":\"o\",\"steps\":[{\"do\":\"wait_menu\"}]}", "kind"), "selftest plan: a file whose kind is not rlo-selftest is refused, so no other file is taken for a plan");
        Check(SelfTestRefused("{\"output\":\"o\",\"steps\":[{\"do\":\"wait_menu\"}]}", "kind"), "selftest plan: a file with no kind is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"steps\":[{\"do\":\"wait_menu\"}]}", "output"), "selftest plan: a plan with no output folder is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[]}", "steps"), "selftest plan: a plan with no steps is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"fly\"}]}", "no such kind"), "selftest plan: an unknown kind of step is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"wait_menu\",\"colour\":\"red\"}]}", "takes no"), "selftest plan: an argument the kind does not take is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"open_screen\"}]}", "needs"), "selftest plan: a missing required argument is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"wait_seconds\",\"seconds\":\"ten\"}]}", "a number"), "selftest plan: an argument of the wrong type is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"wait_menu\",\"limit\":0}]}", "limit"), "selftest plan: a time limit of nothing is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"wait_menu\",\"limit\":99999}]}", "limit"), "selftest plan: a time limit past two hours is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[\"wait_menu\"]}", "not an object"), "selftest plan: a step that is not an object is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"wait_menu\"},{\"do\":\"nope\"},{\"do\":\"open_screen\"}]}", "step 2") && SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"wait_menu\"},{\"do\":\"nope\"},{\"do\":\"open_screen\"}]}", "step 3"),
            "selftest plan: every fault of a plan is told, each with its step's number");

        // Repeats.
        plan = SelfTestRead("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"wait_menu\"},{\"do\":\"repeat\",\"times\":3,\"steps\":[{\"do\":\"wait_city\"},{\"do\":\"screenshot\"}]}]}", out errors);
        Check(plan != null && plan.Steps.Count == 7, "selftest plan: a repeat of three writes its steps out three times (" + (plan != null ? plan.Steps.Count : -1) + " steps)");
        var labels = new HashSet<string>();
        bool distinct = plan != null;
        for (int i = 0; plan != null && i < plan.Steps.Count; i++)
        {
            distinct &= labels.Add(plan.Steps[i].Label);
        }
        Check(distinct, "selftest plan: no two steps of a plan share a name, so their pictures never overwrite each other");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"repeat\",\"times\":0,\"steps\":[{\"do\":\"wait_menu\"}]}]}", "repeat"), "selftest plan: a repeat of none is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"repeat\",\"times\":2}]}", "repeat"), "selftest plan: a repeat with no steps is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"repeat\",\"times\":2,\"steps\":[{\"do\":\"repeat\",\"times\":2,\"steps\":[{\"do\":\"repeat\",\"times\":2,\"steps\":[{\"do\":\"repeat\",\"times\":2,\"steps\":[{\"do\":\"wait_menu\"}]}]}]}]}]}", "repeat"),
            "selftest plan: repeats nested more than three deep are refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"repeat\",\"times\":50,\"steps\":[{\"do\":\"repeat\",\"times\":50,\"steps\":[{\"do\":\"repeat\",\"times\":50,\"steps\":[{\"do\":\"wait_menu\"}]}]}]}]}", "steps after its repeats"),
            "selftest plan: a plan that repeats itself past " + SelfTestPlanReader.MostSteps + " steps is refused");

        plan = SelfTestRead("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"quit\":false,\"keep_options\":true,\"steps\":[{\"do\":\"wait_seconds\",\"seconds\":120}]}", out errors);
        Check(plan != null && !plan.Quit && plan.KeepOptions && plan.Steps[0].Limit == 150, "selftest plan: quit and keep_options are read, and a wait's limit grows with its seconds");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"wait_menu\"},{\"do\":\"assert_log_has\",\"text\":\"x\",\"from\":\"load 1\"}]}", "label of no earlier step"),
            "selftest plan: a from that names the label of no earlier step is refused when the plan is read, not when it runs");
        plan = SelfTestRead("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"wait_menu\",\"label\":\"load_1\"},{\"do\":\"assert_log_has\",\"text\":\"x\",\"from\":\"load_1\"},"
            + "{\"do\":\"repeat\",\"times\":2,\"steps\":[{\"do\":\"wait_city\",\"label\":\"c\"},{\"do\":\"assert_money\",\"atleast\":1,\"from\":\"c\"},{\"do\":\"assert_bank_line\",\"key\":\"k\",\"from\":\"c\",\"count\":1},"
            + "{\"do\":\"assert_notice\",\"key\":\"n\",\"absent\":true},{\"do\":\"assert_owns\",\"address\":\"1 Main Street\"},{\"do\":\"set_year\",\"days\":365},{\"do\":\"write_wav\",\"path\":\"Busking/a.wav\",\"hz\":440}]}]}", out errors);
        Check(plan != null && errors.Count == 0 && plan.Steps.Count == 16 && plan.Steps[3].Arg("from") == "c_r1" && plan.Steps[10].Arg("from") == "c_r2",
            "selftest plan: inside a repeat a from names the same round's label, and the money, bank line, notice, owns, year and wav steps read with their arguments (" + (plan != null ? plan.Steps.Count : -1) + " steps)");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"teleport\",\"address\":\"{precinct}\"}]}", "set by no earlier"), "selftest plan: a {variable} that no earlier find_place, call or read set is refused when the plan is read");
        plan = SelfTestRead("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"find_place\",\"kind\":\"precinct\",\"as\":\"p\"},{\"do\":\"enter_building\",\"address\":\"{p}\"},{\"do\":\"leave_building\"},"
            + "{\"do\":\"kit_press\",\"command\":\"Row\",\"arg\":2,\"screen\":\"estate\"},{\"do\":\"kit_press\",\"key\":\"reallife_flat_btn_buy_cash\"},{\"do\":\"kit_commands\"},{\"do\":\"confirm\",\"answer\":\"no\"},{\"do\":\"set_day\",\"weekday\":\"Monday\",\"hour\":9},"
            + "{\"do\":\"assert_home\"},{\"do\":\"call\",\"type\":\"X\",\"method\":\"Y\",\"args\":\"1|a\",\"as\":\"r\"},{\"do\":\"read\",\"type\":\"X\",\"member\":\"Book.Count\",\"atleast\":1,\"expect\":\"count\"},"
            + "{\"do\":\"press_button\",\"button\":\"b\",\"expect_text\":\"x\",\"expect_log\":\"y\",\"refuse\":\"z\"}]}", out errors);
        Check(plan != null && errors.Count == 0 && plan.Steps.Count == 12 && plan.Steps[1].Arg("address") == "{p}", "selftest plan: the new play steps (find_place, enter and leave, kit_press by name, text or key, kit_commands, confirm, set_day, assert_home, call, read, press_button with expectations) read with their arguments");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"measure\",\"since\":\"never\"}]}", "marked by no earlier"), "selftest plan: a since that no earlier measure marked is refused");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"load_save\",\"save\":\"My Game\"}]}", "self-test's own") && SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"load_save\"}]}", "newest")
              && SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"save_game\",\"name\":\"Owner Save\"}]}", "self-test's own"),
            "selftest plan: a plan may load only saves whose names begin selftest-, never 'newest', and may write only such saves, unless it sets owner_saves");
        plan = SelfTestRead("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"owner_saves\":true,\"steps\":[{\"do\":\"load_save\"},{\"do\":\"load_save\",\"save\":\"selftest-x\"}]}", out errors);
        Check(plan != null && plan.OwnerSaves && plan.Steps.Count == 2, "selftest plan: owner_saves lets a plan load the newest save of the owner's");
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"measure\"}]}", "mark"), "selftest plan: a measure that names no mark and no moment to measure from is refused");
        plan = SelfTestRead("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"measure\",\"mark\":\"a\"},{\"do\":\"measure\",\"since\":\"a\",\"as\":\"load\"},"
            + "{\"do\":\"assert_log_has\",\"text\":\"Uptown check\",\"absent\":false,\"wait\":4},{\"do\":\"game_open\",\"what\":\"help\"},{\"do\":\"monologue\"}]}", out errors);
        Check(plan != null && plan.Steps.Count == 5 && plan.Steps[1].Arg("as") == "load" && plan.Steps[2].Arg("text") == "Uptown check" && !plan.Steps[2].Flag("absent", true) && plan.Steps[2].Number("wait", 0) == 4,
            "selftest plan: measure, assert_log_has, game_open and monologue read with their arguments");
    }

    // The step table: names unique, limits sane, every kind has its class on the game side and the other way round.
    private static void SelfTestTable()
    {
        var names = new HashSet<string>();
        bool unique = true, limits = true, specs = true;
        foreach (SelfTestKind kind in SelfTestKinds.All)
        {
            unique &= names.Add(kind.Name);
            limits &= kind.Limit > 0 && kind.Limit <= SelfTestPlanReader.MostLimit;
            foreach (string spec in kind.Required)
            {
                specs &= spec.IndexOf(':') > 0 && "sinb".IndexOf(spec[spec.Length - 1]) >= 0 && spec.Length - spec.IndexOf(':') == 2;
            }
            foreach (string spec in kind.Optional)
            {
                specs &= spec.IndexOf(':') > 0 && "sinb".IndexOf(spec[spec.Length - 1]) >= 0 && spec.Length - spec.IndexOf(':') == 2;
            }
            specs &= !string.IsNullOrEmpty(kind.Words) && kind.Words.IndexOf('\u2014') < 0 && kind.Words.IndexOf('!') < 0;
        }
        Check(unique && limits && specs, "selftest table: every kind of step has its own name, a limit of seconds, typed arguments and words");
        Check(SelfTestKinds.All.Length >= 30 && SelfTestKinds.Find("load_save") != null && SelfTestKinds.Find("nope") == null && SelfTestKinds.Find(null) == null, "selftest table: the table has the steps the plan needs and finds none by a wrong name");

        string made = SelfTestMod(@"Diagnostics\SelfTest\SelfTestSteps.cs");
        bool every = made.Length > 0;
        var cases = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(made, "case \"([a-z_]+)\": return new ([A-Za-z]+)\\(\\);"))
        {
            cases.Add(m.Groups[1].Value);
            string cls = m.Groups[2].Value;
            string all = string.Join("\n", SelfTestFiles().ConvertAll(File.ReadAllText));
            every &= SelfTestKinds.Find(m.Groups[1].Value) != null && all.Contains("sealed class " + cls + " : SelfTestStep") && all.Contains("public override string Name => \"" + m.Groups[1].Value + "\"");
        }
        foreach (SelfTestKind kind in SelfTestKinds.All)
        {
            every &= cases.Contains(kind.Name);
        }
        Check(every && cases.Count == SelfTestKinds.All.Length, "selftest table: the game side has a step class for every kind in the table and none the table lacks (" + cases.Count + " of " + SelfTestKinds.All.Length + ")");
    }

    private static void SelfTestResultsFile()
    {
        var results = new SelfTestResults { Plan = "p", Started = "2026-11-19 12:00:00", GameBuild = "build 3682", ModVersion = "1.0.17", Total = 3 };
        results.Mods.Add("Real Life Overhaul (id) v1.0.17");
        var a = new SelfTestStepResult { Name = "wait_menu", Kind = "wait_menu", Outcome = SelfTestOutcome.Pass, Words = "the main menu stands", Seconds = 4.2 };
        var b = new SelfTestStepResult { Name = "open_screen", Kind = "open_screen", Outcome = SelfTestOutcome.Fail, Words = "does not show", Seconds = 2.0 };
        b.Shots.Add("open_screen.png");
        b.LogErrors.Add("exception (RLO): boom \"quoted\"");
        var c = new SelfTestStepResult { Name = "x", Kind = "note", Outcome = SelfTestOutcome.Skipped, Words = "MOD ABSENT" };
        results.Measures["load_seconds_1"] = 12.34;
        results.Steps.Add(a);
        Check(!JObject.Parse(results.ToJson()).Value<bool>("done") && results.Count(SelfTestOutcome.Pass) == 1, "selftest results: a run under way says it is not done");
        results.Steps.Add(b);
        results.Steps.Add(c);
        results.Done = true;
        JObject json = JObject.Parse(results.ToJson());
        Check(json.Value<bool>("done") && json.Value<int>("passed") == 1 && json.Value<int>("failed") == 1 && json.Value<int>("skipped") == 1 && json.Value<int>("steps_planned") == 3, "selftest results: the file counts passed, failed and skipped and says it is done");
        JToken second = json["steps"][1];
        Check(second.Value<string>("outcome") == "fail" && second.Value<string>("words") == "does not show" && second["screenshots"][0].Value<string>() == "open_screen.png" && second["log_errors"][0].Value<string>().Contains("\"quoted\""),
            "selftest results: a step has its outcome, words, pictures and new log errors (quotes kept)");
        Check(json["mods"][0].Value<string>().Contains("Real Life Overhaul") && json.Value<string>("game_build") == "build 3682", "selftest results: the loaded mods and the game's build are in the file");
        Check(json["measures"].Value<double>("load_seconds_1") == 12.34, "selftest results: a figure a measure step recorded is in the file by its name, in seconds, so two builds can be compared");
                b.Diagnostics = "scenes: Intro | isLoading True";
        results.Current = "3/19 new_game (limit 600 s, 40 s in): the game to leave the character screen";
        JObject withDiag = JObject.Parse(results.ToJson());
        Check(withDiag["steps"][1].Value<string>("diagnostics").Contains("scenes: Intro") && withDiag.Value<string>("current").Contains("new_game") && withDiag["steps"][0]["diagnostics"] == null,
            "selftest results: a failed step carries the diagnostics, and the file says which step is under way and what it waits for");
                Check(results.DoneLine() == "SELFTEST DONE passed 1 failed 1", "selftest results: the last line counts what passed and failed");

        string folder = IoPath.Combine(IoPath.GetTempPath(), "rlo-selftest-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            results.Write(folder);
            results.Steps.RemoveAt(2);
            results.Write(folder);
            string file = IoPath.Combine(folder, "results.json");
            Check(File.Exists(file) && !File.Exists(file + ".tmp") && ((JArray)JObject.Parse(File.ReadAllText(file))["steps"]).Count == 2, "selftest results: the file is written whole after a step, again over the old one, and no temporary file is left");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
    }

    // The sources: inert without the file, nothing per frame, nothing a player can see, nothing outside the folder but the few hooks.
    private static void SelfTestSources()
    {
        string entry = SelfTestMod(@"Diagnostics\SelfTest\SelfTestEntry.cs");
        string runner = SelfTestMod(@"Diagnostics\SelfTest\SelfTestRunner.cs");
        string watch = SelfTestMod(@"Diagnostics\SelfTest\SelfTestWatch.cs");
        Check(entry.Contains("ModEntryOnInitializationLoad") && entry.Contains("SelfTestRules.Look(Application.persistentDataPath, File.Exists") && entry.Contains("Verdict.Absent")
              && entry.IndexOf("SelfTestRunner.Begin", StringComparison.Ordinal) > entry.IndexOf("Verdict.Absent", StringComparison.Ordinal), "selftest source: the entry asks the trigger once at the game's start and returns before anything is made when there is no file");
        string beforeRun = entry.Substring(0, Math.Max(0, entry.IndexOf("SelfTestRunner.Begin", StringComparison.Ordinal)));
        Check(beforeRun.Contains("File.Delete(path)") && runner.Contains("File.Delete(path);   // consumed"), "selftest source: a stale trigger file is deleted unread and a fresh one is consumed as it is read");
        bool frame = false, hook = false;
        foreach (string file in SelfTestFiles())
        {
            string text = File.ReadAllText(file);
            frame |= text.Contains("void Update(") || text.Contains("void LateUpdate(") || text.Contains("void FixedUpdate(") || text.Contains("OnGUI(");
            hook |= !file.EndsWith("SelfTestWatch.cs", StringComparison.Ordinal) && text.Contains("logMessageReceived");
        }
        Check(!frame, "selftest source: nothing of the self-test has an Update, LateUpdate, FixedUpdate or OnGUI: it costs nothing per frame");
        Check(!hook && watch.Contains("Application.logMessageReceived += _onLog") && watch.Contains("Application.logMessageReceived -= _onLog") && runner.Contains("Watch.Hook()") && runner.Contains("Watch.Unhook()"),
            "selftest source: the game's log is hooked only while a plan runs, in one place, and let go at its end");
        string joined = string.Join("\n", SelfTestFiles().ConvertAll(File.ReadAllText));
        Check(!joined.Contains("GetLocalization(") && !joined.Contains("UiText.T(\"reallife_") && !joined.Contains("Notifications.Show"), "selftest source: it uses no locale text and shows the player nothing (its words go to the results file in English)");
        Check(!joined.Contains("AddToggle(") && !joined.Contains("AddButton(") && !joined.Contains("AddSlider(") && !joined.Contains("OptionsService.Register("), "selftest source: it adds no option to the options page");
        Check(runner.Contains("runner.StartCoroutine(Guard.Routine(runner.Main()))"), "selftest source: the runner's coroutine is started through the guard");

        // Nothing else of the mod names the self-test but the accessors it was given.
        string[] allowed = { "Diagnostics\\SelfTest\\", "Ui\\UiHost.cs", "Ui\\UiHost.SelfTest.cs", "Ui\\Host\\UiGameHost.SelfTest.cs", "Core\\GameUi\\PhoneApps.cs" };
        string scripts = IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var outside = new List<string>();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string rel = file.Substring(scripts.Length + 1);
            bool ok = false;
            foreach (string a in allowed)
            {
                ok |= rel.StartsWith(a, StringComparison.Ordinal) || rel == a;
            }
            if (!ok && File.ReadAllText(file).IndexOf("SelfTest", StringComparison.Ordinal) >= 0)   // the type name; a test business called "Selftest Studio" is not it
            {
                outside.Add(rel);
            }
        }
        Check(outside.Count == 0, "selftest source: no file outside the self-test's folder names it but those that carry its accessors (" + string.Join(", ", outside) + ")");

        // Nothing a player reads names it.
        string en = File.Exists(IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales\en.json")))
            ? File.ReadAllText(IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales\en.json"))) : "x";
        Check(en.IndexOf("selftest", StringComparison.OrdinalIgnoreCase) < 0 && en.IndexOf("self-test", StringComparison.OrdinalIgnoreCase) < 0, "selftest source: no text of the mod, the Guide included, names the self-test");
        string settings = SelfTestMod(@"Core\Settings.cs") + SelfTestMod(@"Meta\OptionsTree.cs") + SelfTestMod(@"Meta\FeatureList.cs");
        Check(settings.IndexOf("selftest", StringComparison.OrdinalIgnoreCase) < 0, "selftest source: no setting, option or feature list names the self-test");

        // A step passes on evidence: the new game only when the loading began and the scenes changed, through the screen's own button.
        string saves = SelfTestMod(@"Diagnostics\SelfTest\SelfTestSteps.Saves.cs");
        int ng = saves.IndexOf("class NewGameStep", StringComparison.Ordinal);
        string newGame = ng >= 0 ? saves.Substring(ng, saves.IndexOf("class SaveGameStep", ng, StringComparison.Ordinal) - ng) : string.Empty;
        Check(newGame.Contains("SelfTestSaves.StartButton(screen)") && newGame.Contains("start.onClick.Invoke()") && newGame.Contains("start.interactable") && newGame.Contains("HasANameSet") && newGame.Contains("AnswerQuestion()"),
            "selftest source: the new game waits for the character screen's Start button to be interactive, types the name, presses the button's own handler and answers the game's questions");
        int pass = newGame.IndexOf("c.Pass(", StringComparison.Ordinal);
        Check(pass > 0 && newGame.LastIndexOf("if (loadingSeen && SelfTestDiag.Scenes() != before)", pass, StringComparison.Ordinal) > 0 && newGame.Contains("since > 90f") && newGame.Contains("stuck at the character creation screen"),
            "selftest source: the new game passes only after the loading flag was seen and the loaded scenes changed, and fails within 90 s naming the screen it stuck at");
        string kit = SelfTestMod(@"Diagnostics\SelfTest\SelfTestSteps.Kit.cs");
        string world = SelfTestMod(@"Diagnostics\SelfTest\SelfTestSteps.World.cs");
        Check(saves.Contains("SelfTestDiag.Scenes() == before") && kit.Contains("neither the phone's menu nor any kit screen shows") && SelfTestMod(@"Diagnostics\SelfTest\SelfTestSteps.Play.cs").Contains("nothing was asserted") && world.Contains("did not move from")
              && SelfTestMod(@"Diagnostics\SelfTest\SelfTestWorld.cs").Contains("reads back as") && world.Contains("left \" + (long)") && world.Contains("the console refused"),
            "selftest source: load_save, open_app, press_button, teleport, found_business, set_money and console each wait for what they caused and say what they waited for");
        Check(runner.Contains("-fail") && runner.Contains("SelfTestDiag.Dump()") && runner.Contains("Time.realtimeSinceStartup - wrote > 5f") && SelfTestMod(@"Diagnostics\SelfTest\SelfTestDiag.cs").Contains("showing canvases"),
            "selftest source: a failed step takes a picture and writes the scenes, loading flag and showing canvases and kit screens into the results; a running step rewrites the file every 5 s");
        string runPath = @"D:\BigAmbitionsMod\not-in-repo\tools\selftest\run.sh";
        string runSh = File.Exists(runPath) ? File.ReadAllText(runPath) : "STALLED at step live.py -gt 180";
        Check(runSh.Contains("STALLED at step") && runSh.Contains("live.py") && runSh.Contains("-gt 180"), "selftest tools: run.sh prints each step as it lands and stops the game as STALLED when results.json does not change for 3 minutes");

        string game = SelfTestMod(@"Diagnostics\SelfTest\SelfTestGame.cs");
        string ui = SelfTestMod(@"Diagnostics\SelfTest\SelfTestUi.cs");
        Check(game.Contains("HudHost.Instance") && game.Contains("ids.Add(\"hud\")") && game.Contains("public static bool IsShowing") && kit.Contains("SelfTestGame.IsShowing(kit)") && SelfTestMod(@"Diagnostics\SelfTest\SelfTestSteps.Assert.cs").Contains("SelfTestGame.IsShowing(id)"),
            "selftest source: the HUD (a host of its own, not a UiHost) is in the list of kit screens showing, and assert_kit_showing and the opening steps count it");
        Check(game.Contains("CityMap.IsOpen") && game.Contains("ClearNotices()") && game.Contains("left.Hide()") && game.Contains("Residue") && kit.Contains("NOT SHOWN ALONE") && kit.Contains("LEFT OPEN"),
            "selftest source: before and after each screen the walk leaves the map, answers the question box, closes each kit screen (taking away one that will not close), clears the notices, and reports a screen not shown alone or left open");
        Check(SelfTestScreenTable.Rank("contacts", true) == 0 && SelfTestScreenTable.Rank("company", false) == 1 && SelfTestScreenTable.Rank("world_items_list", false) == 2 && SelfTestScreenTable.Rank("hud_side", false) == 2
              && SelfTestScreenTable.Rank("question_box", false) == 2 && SelfTestScreenTable.Rank("build_mode", false) == 2 && ui.Contains("public static List<string> Ordered"),
            "selftest source: the walk of the registry puts the plain apps first, then the screens a test button opens, and the situations (pickers, world boxes, build mode) last");
        Check(SelfTestKinds.Find("open_every_screen").Limit >= 3600 && world.Contains("set again") && world.Contains("the first set read"), "selftest source: a long walk has a generous limit, and set_money sets, reads after a frame, sets again once and says how far off the first read was");
        byte[] wav = SelfTestWav.Make(0.5, 440);
        int dataBytes = BitConverter.ToInt32(wav, 40), rate = BitConverter.ToInt32(wav, 24);
        Check(wav.Length == 44 + dataBytes && dataBytes == SelfTestWav.Rate && rate == SelfTestWav.Rate && wav[0] == (byte)'R' && wav[8] == (byte)'W' && BitConverter.ToInt16(wav, 22) == 1 && BitConverter.ToInt16(wav, 34) == 16,
            "selftest source: a generated tone is a mono 16 bit PCM wav of the length asked (" + wav.Length + " bytes)");
        string state = SelfTestMod(@"Diagnostics\SelfTest\SelfTestSteps.State.cs");
        Check(state.Contains("Path.GetFullPath(Path.Combine(Application.persistentDataPath, \"RealLifeOverhaul\", \"GigMusic\"))") && state.Contains("rel.Contains(\"..\")") && state.Contains("StartsWith(root"),
            "selftest source: the wav step writes only inside RealLifeOverhaul/GigMusic of the game's data folder, never up a path");
        string play = SelfTestMod(@"Diagnostics\SelfTest\SelfTestSteps.Play.cs");
        Check(play.Contains("SelfTestPressRow(") && play.Contains("until = Time.realtimeSinceStartup + c.Number(\"wait\", 3f)") && SelfTestKinds.Find("kit_press").TypeOf("row") == "i" && SelfTestKinds.Find("kit_press").TypeOf("wait") == "n"
              && SelfTestMod(@"Ui\UiHost.SelfTest.cs").Contains("SelfTestWordsOf") && SelfTestMod(@"Diagnostics\SelfTest\SelfTestGame.cs").Contains("UiTemplate.Phone"),
            "selftest source: kit_press presses a table row by index or by the words of any of its cells, waits for the target to appear, and the HUD and the folded phone are the resting state");
        Check(play.Contains("refused ('") && play.Contains("expect_text") && play.Contains("expect_log") && play.Contains("c.Skip(\"pressed \" + c.Arg(\"button\") + \", said: \"") && play.Contains("c.Pass(\"pressed \" + c.Arg(\"button\") + \", said: \""),
            "selftest source: a test button's press fails on a refusal, passes only on what the plan expects of the notices and log lines it caused, and is a skip quoting what was said when nothing is expected");
        Check(play.Contains("SelfTestReflect.Command(") && play.Contains("SelfTestPressText(") && play.Contains("class KitCommandsStep") && play.Contains("HudConfirm.isOpen") && SelfTestMod(@"Diagnostics\SelfTest\SelfTestRunner.cs").Contains("private bool Substitute("),
            "selftest source: kit_press by a command constant's name or the words on a target, kit_commands, confirm and the {name} variables are there");
        string watchText = SelfTestMod(@"Diagnostics\SelfTest\SelfTestWatch.cs");
        Check(watchText.Contains("Notifications.onShow") && watchText.Contains("EventHold.Keep(ref Notifications.onShow") && watchText.Contains("public List<string> NoticeLines"),
            "selftest source: the notices a press caused are kept from the game's own notification event, joined and kept attached as the mod's other hooks are, only while a plan runs");
        // The screens table against the registry and the options page.
        string mod = SelfTestMod(@"Core\RealLifeOverhaulMod.cs");
        bool ids = true, labels = true;
        foreach (string[] row in SelfTestScreenTable.Apps)
        {
            ids &= RealLifeOverhaul.Ui.UiScreens.Find(row[0]) != null;
        }
        foreach (string[] row in SelfTestScreenTable.Tests)
        {
            ids &= RealLifeOverhaul.Ui.UiScreens.Find(row[0]) != null;
            labels &= mod.Contains("AddButton(\"" + row[1] + "\"");
        }
        Check(ids, "selftest source: every screen in the opening table is in the kit's registry");
        Check(labels, "selftest source: every options button the opening table presses is on the mod's options page");
        bool reg = true, labelsOk = true;
        foreach (string id in SelfTestScreenTable.Unopenable)
        {
            reg &= RealLifeOverhaul.Ui.UiScreens.Find(id) != null && SelfTestScreenTable.Why(id) != null;
        }
        foreach (string[] row in SelfTestScreenTable.GameOpens)
        {
            reg &= RealLifeOverhaul.Ui.UiScreens.Find(row[0]) != null && new[] { "help", "blueprints", "phone" }.Contains(row[1]);
        }
        foreach (string[] row in SelfTestScreenTable.Tests)
        {
            labelsOk &= row.Length == 2 || (row.Length == 3 && row[2] == "toggle");
        }
        Check(reg && labelsOk, "selftest source: the screens no step can open and the game's own windows it opens by a call are all in the registry, each unopenable one with its reason");
        bool overlap = false;
        foreach (string id in SelfTestScreenTable.Unopenable)
        {
            overlap |= SelfTestScreenTable.TestOf(id) != null || SelfTestScreenTable.AppOf(id) != null;
        }
        Check(!overlap, "selftest source: no screen is both opened by a button and listed as one no step can open");
        Check(SelfTestScreenTable.Toggles("world_prompt") && SelfTestScreenTable.Toggles("hud_side") && !SelfTestScreenTable.Toggles("contacts") && !SelfTestScreenTable.Toggles("gallery"),
            "selftest source: the tests that toggle (a door's prompt, a desk's list, the HUD's sample pieces) are pressed again to take their screen away");
        Check(SelfTestScreenTable.Tests.Length >= 70, "selftest source: the opening table covers the situations (a door, a desk, a custody list, the bus, the HUD's pieces, tooltips, build mode, the designer) as well as the apps (" + SelfTestScreenTable.Tests.Length + " rows)");
                Check(SelfTestScreenTable.AppOf("contacts") == "Contacts" && SelfTestScreenTable.TestOf("gallery") == "reallife_ui_test_gallery" && SelfTestScreenTable.AppOf("gallery") == null && SelfTestScreenTable.TestOf("nope") == null,
            "selftest source: the opening table answers by a screen's id");
    }

    // The plans that ship beside the tools (outside the repository): where they are, each is read against the table.
    private static void SelfTestShippedPlans()
    {
        string folder = @"D:\BigAmbitionsMod\not-in-repo\tools\selftest";
        if (!Directory.Exists(folder))
        {
            return;
        }
        var files = new List<string>(Directory.GetFiles(folder, "*.json"));
        if (Directory.Exists(IoPath.Combine(folder, "plans")))
        {
            files.AddRange(Directory.GetFiles(IoPath.Combine(folder, "plans"), "*.json"));
        }
        string betaTexts = @"D:\BigAmbitionsMod\wt-living\Assets\Mods\RealLifeOverhaul\Locales\en.json";
        JObject texts = File.Exists(betaTexts) ? JObject.Parse(File.ReadAllText(betaTexts).TrimStart('\uFEFF')) : null;
        string gameFile = @"G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\StreamingAssets\locale\en.json";
        JObject gameTexts = File.Exists(gameFile) ? JObject.Parse(File.ReadAllText(gameFile).TrimStart('\uFEFF')) : new JObject();
        string beta = @"D:\BigAmbitionsMod\wt-living\Assets\Mods\RealLifeOverhaul\Scripts\Core\RealLifeOverhaulMod.cs";
        string buttons = File.Exists(beta) ? File.ReadAllText(beta) : string.Empty;   // the beta's page, where the new features' buttons are
        string buttonsHere = SelfTestMod(@"Core\RealLifeOverhaulMod.cs");
        foreach (string file in files)
        {
            SelfTestPlan plan = SelfTestRead(File.ReadAllText(file), out List<string> errors);
            Check(plan != null && errors.Count == 0 && plan.Steps.Count > 0, "selftest plans: " + IoPath.GetFileName(file) + " reads against the table" + (errors.Count > 0 ? ": " + string.Join("; ", errors) : string.Empty));
            Check(plan == null || plan.OwnerSaves == (IoPath.GetFileName(file) == "smoke.json"), "selftest plans: " + IoPath.GetFileName(file) + " loads only the self-test's own saves (only smoke.json may take the owner's)");
            var unknown = new List<string>();
            for (int i = 0; plan != null && texts != null && i < plan.Steps.Count; i++)
            {
                string key = plan.Steps[i].Arg("key");
                if (key == null || (plan.Steps[i].Kind != "kit_press" && plan.Steps[i].Kind != "assert_notice" && plan.Steps[i].Kind != "assert_text"))
                {
                    continue;
                }
                string bare = key.TrimEnd(':');
                bool exact = texts.ContainsKey(bare) || gameTexts.ContainsKey(bare);
                bool prefix = false;
                foreach (KeyValuePair<string, JToken> pair in texts)
                {
                    prefix |= pair.Key.StartsWith(bare, StringComparison.Ordinal);
                }
                if (!(plan.Steps[i].Kind == "assert_notice" ? exact || prefix || bare.Length < 6 : exact))
                {
                    unknown.Add(key);
                }
            }
            Check(unknown.Count == 0, "selftest plans: " + IoPath.GetFileName(file) + " presses and asserts only by text keys that exist in the texts (" + string.Join(", ", unknown) + ")");
            var missing = new List<string>();
            for (int i = 0; plan != null && i < plan.Steps.Count; i++)
            {
                string button = plan.Steps[i].Arg("button");
                if (button != null && !buttons.Contains("AddButton(\"" + button + "\"") && !buttonsHere.Contains("AddButton(\"" + button + "\""))
                {
                    missing.Add(button);
                }
            }
            Check(missing.Count == 0, "selftest plans: " + IoPath.GetFileName(file) + " presses only buttons the options page has (" + string.Join(", ", missing) + ")");
        }
    }
}
