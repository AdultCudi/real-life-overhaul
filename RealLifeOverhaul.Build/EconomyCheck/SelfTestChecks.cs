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
        string ok = "{\"kind\":\"rlo-selftest\",\"name\":\"t\",\"output\":\"D:/o\",\"steps\":[{\"do\":\"wait_menu\"},{\"do\":\"load_save\",\"save\":\"newest\",\"limit\":90},{\"do\":\"open_screen\",\"id\":\"contacts\",\"shot\":false,\"settle\":2.5}]}";
        SelfTestPlan plan = SelfTestRead(ok, out List<string> errors);
        Check(plan != null && errors.Count == 0 && plan.Steps.Count == 3 && plan.Name == "t" && plan.Output == "D:/o" && plan.Quit && !plan.KeepOptions, "selftest plan: a plan is read: its steps, name, output, quit on by default");
        Check(plan != null && plan.Steps[0].Limit == SelfTestKinds.Find("wait_menu").Limit && plan.Steps[1].Limit == 90, "selftest plan: a step has its kind's time limit unless the plan names one");
        Check(plan != null && plan.Steps[2].Flag("shot", true) == false && Math.Abs(plan.Steps[2].Number("settle", 0) - 2.5) < 1e-9 && plan.Steps[1].Arg("save") == "newest", "selftest plan: arguments are read as the kind types them");
        Check(plan != null && plan.Steps[0].Label == "wait_menu" && plan.Steps[2].Label == "open_screen", "selftest plan: a step with no label is called by its kind");
        plan = SelfTestRead("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"save_game\",\"name\":\"my save\",\"label\":\"first save\"},{\"do\":\"screenshot\",\"name\":\"city\"}]}", out errors);
        Check(plan != null && plan.Steps[0].Label == "first_save" && plan.Steps[0].Arg("name") == "my save" && plan.Steps[1].Arg("name") == "city" && plan.Steps[1].Label == "screenshot",
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
        Check(SelfTestRefused("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"measure\"}]}", "mark"), "selftest plan: a measure that names no mark and no moment to measure from is refused");
        plan = SelfTestRead("{\"kind\":\"rlo-selftest\",\"output\":\"o\",\"steps\":[{\"do\":\"measure\",\"mark\":\"a\"},{\"do\":\"measure\",\"since\":\"a\",\"as\":\"load\"},"
            + "{\"do\":\"assert_log_has\",\"text\":\"Uptown check\",\"from\":\"x\",\"absent\":false,\"wait\":4},{\"do\":\"game_open\",\"what\":\"help\"},{\"do\":\"monologue\"}]}", out errors);
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
            if (!ok && File.ReadAllText(file).IndexOf("SelfTest", StringComparison.OrdinalIgnoreCase) >= 0)
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
        foreach (string file in files)
        {
            SelfTestPlan plan = SelfTestRead(File.ReadAllText(file), out List<string> errors);
            Check(plan != null && errors.Count == 0 && plan.Steps.Count > 0, "selftest plans: " + IoPath.GetFileName(file) + " reads against the table" + (errors.Count > 0 ? ": " + string.Join("; ", errors) : string.Empty));
        }
    }
}
