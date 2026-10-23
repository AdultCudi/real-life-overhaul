using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.TestRun;

// The city expansion's test run (Scripts/TestRun): its order, its registry and every check in it, the lines it reads,
// the airport lots it gives, what it keeps in PlayerPrefs over the load, the summary, the texts; the game's side read as
// text: nothing at all in normal play, the one file that touches the game, every test it presses existing.
internal static partial class Program
{
    private static void TestRunChecks()
    {
        TestRunOrder();
        TestRunLines();
        TestRunLots();
        TestRunCarry();
        TestRunSummary();
        TestRunRegistry();
        TestRunSources();
        TestRunTexts();
    }

    // ---------------------------------------------------------------- the order

    private static void TestRunOrder()
    {
        List<RunCheck> table = RunPlan.All();
        List<RunCheck> order = RunRules.Ordered(table);
        Check(order.Count == table.Count && order.Count >= 60, "test run: every check of the table is in the order (" + order.Count + ")");
        bool phases = true, places = true, kept = true;
        for (int i = 1; i < order.Count; i++)
        {
            RunCheck a = order[i - 1], b = order[i];
            phases &= a.Phase <= b.Phase;
            places &= a.Phase != b.Phase || a.Place <= b.Place;
            kept &= a.Phase != b.Phase || a.Place != b.Place || a.Order < b.Order;
        }
        Check(phases, "test run: the checks before the load, then the last before it, then after it, then the put backs");
        Check(places, "test run: within a phase the player is moved place by place (here, out of doors, Uptown, a building's door, the grass, its pavement, the test building)");
        Check(kept, "test run: within a place the checks keep the order they were registered in");

        int At(string id) => order.FindIndex(c => c.Id == id);
        Check(At("reallife_construction_test_rebuild") < At("reallife_construction_test_stage"), "test run: the rebuild site is set out before the stage test moves the nearest site on");
        Check(At("reallife_newground_test_patch") < At("reallife_newground_test_link") && At("reallife_newground_test_link") < At("reallife_newground_test_grow"),
            "test run: the walking patch is laid before it is linked, and the grow (which takes the patch away) comes last");
        Check(At("reallife_bld_test_lot") < At("reallife_bld_test_company") && At("reallife_bld_test_buyer") < At(RunPlan.UptownOff) && At("reallife_bld_test_back") > At(RunPlan.NextHour),
            "test run: the construction game's tests run in its own order while Uptown stands, and its put back runs at the very end");
        Check(At("reallife_uptown_test_save") < At(RunPlan.UptownOff) && At(RunPlan.UptownOff) < At("reallife_test_city_away") && At("reallife_test_city_away") < At("reallife_test_city_back"),
            "test run: Uptown's own tests run before it is stood down in play, then another city and back, last before the load");
        Check(At(RunPlan.BuildingOn) < At(RunPlan.BuildingMade) && At(RunPlan.RoadOn) < At(RunPlan.RoadAppended),
            "test run: the test building and the test road are asked for before the load whose lines they are read in");
        Check(At("reallife_newbuildings_test_walk") < At("reallife_newbuildings_test_off") && At("reallife_newbuildings_test_save") < At("reallife_newbuildings_test_off")
              && At("reallife_newbuildings_test_off") < At(RunPlan.NextHour),
            "test run: the test building is walked into and saved inside before the switch-off hides it, and its next hour after");
        Check(order[order.Count - 1].Id == RunPlan.LookBuilding, "test run: it ends with the player at the test building's door, for the eyes");
        Check(At("reallife_floors_test_place") < At("reallife_newground_test_census"), "test run: the test office's floor comes before the checks out of doors take the player out");

        // A feature's check added later joins its place's group, after the table's own there.
        var later = new List<RunCheck>(table);
        later.Add(new RunCheck("reallife_landmarks_test_tower", "landmarks", "Landmarks check (tower)", RunPlace.OpenGround, RunPhase.BeforeLoad, 60f) { Order = table.Count });
        List<RunCheck> joined = RunRules.Ordered(later);
        int tower = joined.FindIndex(c => c.Id == "reallife_landmarks_test_tower");
        Check(tower > 0 && joined[tower - 1].Place == RunPlace.OpenGround && joined[tower + 1].Place == RunPlace.PavementEdge,
            "test run: a feature's check registered later runs with its place's group, after the table's checks there");
        Check(RunRules.Twice(table).Count == 0, "test run: no id is in the table twice");
        later.Add(new RunCheck("reallife_bld_test_lot", "construction", "Construction check (test lot)", RunPlace.Here, RunPhase.BeforeLoad, 60f) { Order = later.Count });
        Check(RunRules.Twice(later).Count == 1 && RunRules.Twice(later)[0] == "reallife_bld_test_lot", "test run: an id registered twice is caught");

        // The same order whatever order the list came in.
        var shuffled = new List<RunCheck>(table);
        shuffled.Reverse();
        List<RunCheck> again = RunRules.Ordered(shuffled);
        bool same = again.Count == order.Count;
        for (int i = 0; same && i < order.Count; i++)
        {
            same = ReferenceEquals(again[i], order[i]);
        }
        Check(same, "test run: the order is the same whatever order the checks are handed in");
    }

    // ---------------------------------------------------------------- the lines

    private static void TestRunLines()
    {
        Check(RunRules.Begins("Uptown check (census): the first block stands: PASS", "Uptown check (census): ")
              && RunRules.Begins("[Mod:RealLifeOverhaul] Uptown check (census): the first block stands: PASS", "Uptown check (census): "),
            "test run: a check's line is known written straight or after the logger's bracketed name");
        Check(!RunRules.Begins("[Mod:RealLifeOverhaul] Uptown check (streets): Roads check (append): traffic started: PASS", "Roads check (append)"),
            "test run: a line that only names another check inside it is not that check's");
        Check(!RunRules.Begins("Construction check (wrap down): every piece taken down: PASS.", "Construction check (wrap):")
              && !RunRules.Begins("Uptown check (census, the test): PASS", "Uptown check (census): "),
            "test run: a check's line does not take a longer check's line of the same words");
        Check(!RunRules.Begins(null, "x") && !RunRules.Begins("[broken", "broken") && RunRules.Bare("[Mod:X] a b") == "a b" && RunRules.Bare("plain") == "plain",
            "test run: nothing, a broken bracket and a bare line are read safely");

        Check(RunRules.Judge("Held buildings test: 3 address(es) held ... none of them held: PASS", "PASS") == RunResult.Pass, "test run: a PASS line passes");
        Check(RunRules.Judge("Walk check (link): path to the patch Player COMPLETE, NPC PARTIAL: FAIL", "PASS") == RunResult.Fail, "test run: a FAIL anywhere fails it");
        Check(RunRules.Judge("Construction check (copy): no free ground 40 m ahead: refused.", "PASS") == RunResult.Fail, "test run: a line without what it must say fails it");
        Check(RunRules.Judge("City check: New York (7 of 7 districts)", null) == RunResult.Pass && RunRules.Judge(null, null) == RunResult.Waiting,
            "test run: a line with no verdict passes by being written; no line is still waited for");
        Check(RunRules.Judge("Walk check (runtime build): Player 4 tiles, sample PASS, path from the player PARTIAL", "COMPLETE") == RunResult.Fail
              && RunRules.Judge("Walk check (runtime build): Player 4 tiles, sample PASS, path from the player COMPLETE", "COMPLETE") == RunResult.Pass,
            "test run: a check that must say COMPLETE is not passed by another word's PASS");

        Check(RunRules.Refusal("[Mod:RealLifeOverhaul] Construction lot test: not started (reallife_bld_test_nothing).", out string why) && why == "reallife_bld_test_nothing"
              && RunRules.OfRefusal(why, out bool retry) == RunResult.NothingHere && !retry,
            "test run: a refusal that finds nothing in the save to bring forward is told apart, not a FAIL");
        Check(RunRules.Refusal("Construction developers test: not started (reallife_bld_designs_wait).", out why) && RunRules.OfRefusal(why, out retry) == RunResult.Refused && retry,
            "test run: a refusal that passes by itself presses the test again");
        Check(RunRules.Refusal("Uptown test: not started (NotLoaded, the game is paused).", out why) && why == "NotLoaded, the game is paused"
              && RunRules.OfRefusal(why, out retry) == RunResult.Refused && !retry && !RunRules.Refusal("Uptown check (census): PASS", out _),
            "test run: any other refusal is a refusal, with its reason read whole");
        Check(RunRules.Failed(RunResult.Fail) && RunRules.Failed(RunResult.Refused) && RunRules.Failed(RunResult.TimedOut) && RunRules.Failed(RunResult.Fault)
              && !RunRules.Failed(RunResult.Pass) && !RunRules.Failed(RunResult.NothingHere) && !RunRules.Failed(RunResult.Stopped),
            "test run: a FAIL, a refusal, no line and a fault count against the mod; nothing to try and a stop do not");
        Check(RunRules.Worse(RunResult.Pass, RunResult.Fail) == RunResult.Fail && RunRules.Worse(RunResult.Pass, RunResult.Refused) == RunResult.Refused
              && RunRules.Worse(RunResult.Fail, RunResult.Pass) == RunResult.Fail && RunRules.Worse(RunResult.Waiting, RunResult.Pass) == RunResult.Pass,
            "test run: a FAIL or a refusal heard with a check's PASS outweighs it");
        Check(new RunCheck("x", "f", "Made buildings check (way out)", RunPlace.Here, RunPhase.AfterLoad, 1f).Family == "Made buildings check ("
              && new RunCheck("x", "f", "District save test: ", RunPlace.Here, RunPhase.AfterLoad, 1f).Family == "District save test: ",
            "test run: a check's family is its words before ' check ('");
    }

    // ---------------------------------------------------------------- the places

    private static void TestRunLots()
    {
        const int lots = 6;
        Check(RunRules.OpenLot(0, 0, lots) == 1 && RunRules.OpenLot(1, 0, lots) == 2 && RunRules.OpenLot(3, 0, lots) == 4 && RunRules.OpenLot(4, 0, lots) == 1,
            "test run: open-ground checks take the lots in turn past the test building's, round again when they run out");
        Check(RunRules.PavementLot(0, lots) == 5 && RunRules.PavementLot(5, lots) == 4, "test run: the walking checks keep the last lot the test building does not stand on");
        bool apart = true;
        for (int taken = -1; taken < lots; taken++)
        {
            for (int use = 0; use < 12; use++)
            {
                int lot = RunRules.OpenLot(use, taken, lots);
                apart &= lot >= 0 && lot < lots && lot != taken && lot != RunRules.PavementLot(taken, lots);
            }
        }
        Check(apart, "test run: an open-ground check never gets the test building's lot or the walking checks' one, wherever the building stands");
        Check(RunRules.OpenLot(0, 0, 2) == -1 && RunRules.OpenLot(-1, 0, lots) == -1, "test run: too few lots, or no use, give none");
        RunRules.Front(-2159f, -957f, 0f, 34f, out float x, out float z, out float yaw);
        Check(Math.Abs(x + 2159f) < 0.01f && Math.Abs(z - (-957f + 17f + RunRules.FrontStep)) < 0.01f && Math.Abs(yaw - 180f) < 0.01f,
            "test run: the spot at a lot's front is a step out on its street's side, facing into it");
        RunRules.Front(0f, 0f, 90f, 20f, out x, out z, out yaw);
        Check(Math.Abs(x - (10f + RunRules.FrontStep)) < 0.01f && Math.Abs(z) < 0.01f && Math.Abs(yaw - 270f) < 0.01f, "test run: the same for a lot whose street lies east");
        Check(Math.Abs(RunRules.Toward(0f, 0f, 0f, 5f)) < 0.01f && Math.Abs(RunRules.Toward(0f, 0f, 5f, 0f) - 90f) < 0.01f && Math.Abs(RunRules.Toward(0f, 0f, -5f, 0f) - 270f) < 0.01f,
            "test run: the way from one point to another is the game's turn (0 = +Z, 90 = +X)");
        Check(!RunRules.Storeys(5f) && RunRules.Storeys(3f * RunRules.StoreyMetres) && RunRules.Storeys(6f * RunRules.StoreyMetres) && !RunRules.Storeys(9f * RunRules.StoreyMetres),
            "test run: the scaffolding's building is one of three to six storeys");
    }

    // ---------------------------------------------------------------- across the load

    private static void TestRunCarry()
    {
        var duties = new Dictionary<string, string> { { "road", "1" }, { "sites", "12:15" } };
        var outcomes = new List<RunOutcome>
        {
            new RunOutcome { Id = "reallife_held_test", Result = RunResult.Pass, Line = "Held buildings test: none held: PASS" },
            new RunOutcome { Id = "reallife_bld_test_plans", Result = RunResult.NothingHere, Line = "Construction plans test: not started (reallife_bld_test_nothing).\twith\na break" },
            new RunOutcome { Id = "reallife_newground_test_grow", Result = RunResult.TimedOut, Line = new string('x', 900) },
        };
        long began = 638650000000000000L;
        string kept = RunRules.Carry(began, duties, outcomes);
        var backDuties = new Dictionary<string, string>();
        var back = new List<RunOutcome>();
        bool read = RunRules.ReadCarry(kept, out long when, backDuties, back);
        Check(read && when == began && backDuties.Count == 2 && backDuties["sites"] == "12:15" && back.Count == 3 && back[0].Result == RunResult.Pass
              && back[1].Result == RunResult.NothingHere && back[1].Line.IndexOf('\t') < 0 && back[1].Line.IndexOf('\n') < 0 && back[2].Line.Length == RunRules.MostLine,
            "test run: what it keeps over the load reads back the same, line breaks and tabs made spaces, a long line cut");
        Check(RunRules.Carry(began, duties, outcomes) == kept, "test run: the same run is kept as the same text");
        Check(!RunRules.ReadCarry(string.Empty, out _, null, null) && !RunRules.ReadCarry(kept.Replace(RunRules.Version, "testrun0"), out _, null, null)
              && !RunRules.ReadCarry(kept.Replace("\t1\t", "\tseven\t"), out _, null, null) && !RunRules.ReadCarry(kept + "half a line\n", out _, null, null),
            "test run: nothing kept, another version and a damaged line are never read as a run");
        Check(RunRules.ReadCarry(RunRules.Carry(began, null, null), out _, backDuties, back) && backDuties.Count == 0 && back.Count == 0, "test run: a run with nothing decided yet reads back empty");
        long hour = TimeSpan.TicksPerHour;
        Check(RunRules.Fresh(began, began + 11 * hour) && !RunRules.Fresh(began, began + 13 * hour) && !RunRules.Fresh(began, began - hour) && !RunRules.Fresh(0, began),
            "test run: a run waits for its load half a day at most, and one from the future or of no time is forgotten");
    }

    // ---------------------------------------------------------------- the summary

    private static void TestRunSummary()
    {
        var order = new List<RunCheck>
        {
            new RunCheck("a", "f", "Uptown check (census): ", RunPlace.Here, RunPhase.BeforeLoad, 1f) { Eyes = "one" },
            new RunCheck("b", "f", "Walk check (link)", RunPlace.Here, RunPhase.BeforeLoad, 1f) { Eyes = "two" },
            new RunCheck("c", "f", "Construction check (plans)", RunPlace.Here, RunPhase.BeforeLoad, 1f) { Eyes = "three" },
            new RunCheck("d", "f", "Copy check (frame cost)", RunPlace.Here, RunPhase.BeforeLoad, 1f),
        };
        var outcomes = new List<RunOutcome>
        {
            new RunOutcome { Id = "a", Result = RunResult.Pass, Line = "Uptown check (census): PASS" },
            new RunOutcome { Id = "b", Result = RunResult.Fail, Line = "Walk check (link): FAIL" },
            new RunOutcome { Id = "c", Result = RunResult.NothingHere, Line = "nothing" },
        };
        List<string> eyes = RunRules.Eyes(order, outcomes);
        string block = RunRules.Summary(order, outcomes, eyes, false, out int passed, out int ran, out List<string> failed);
        Check(passed == 1 && ran == 2 && failed.Count == 1 && failed[0] == "Walk check (link)", "test run: the summary counts the checks that ran, and names those that failed");
        Check(block.StartsWith("City expansion test run: 1 of 2 PASS; failed: Walk check (link); nothing to try in this save: Construction check (plans).", StringComparison.Ordinal),
            "test run: the summary's first line says N of M PASS, what failed and what had nothing to try");
        Check(block.Contains("\n  PASS    Uptown check (census): Uptown check (census): PASS") && block.Contains("\n  NOT RUN Copy check (frame cost): not run"),
            "test run: one line per check with its result and its line");
        Check(eyes.Count == 2 && eyes[0] == "one" && eyes[1] == "two" && block.Contains("\n  1. one") && block.Contains("\n  2. two") && !block.Contains("three"),
            "test run: the checklist for the eyes names only what stood, numbered, after the lines");
        string all = RunRules.Summary(order.GetRange(0, 1), outcomes, null, true, out passed, out ran, out failed);
        Check(all.StartsWith("City expansion test run: 1 of 1 PASS; none failed; stopped before the end.", StringComparison.Ordinal), "test run: a run with nothing failed, and a stopped one, say so");
        var many = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            many.Add("Construction check (inspection test)");
        }
        string shortList = RunRules.Short(many, 100);
        Check(shortList.Length <= 100 && shortList.EndsWith(" more", StringComparison.Ordinal) && RunRules.Short(new List<string> { "a", "b" }, 100) == "a, b" && RunRules.Short(null, 10) == string.Empty,
            "test run: the notice's list of failures fits its room, the rest counted");
        var table = RunPlan.All();
        int looks = 0;
        foreach (RunCheck c in table)
        {
            looks += c.Look != null ? 1 : 0;
        }
        Check(looks <= RunRules.MostEyes, "test run: at most five things for the eyes (" + looks + ")");
    }

    // ---------------------------------------------------------------- the registry

    private static void TestRunRegistry()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file) => File.Exists(System.IO.Path.Combine(scripts, file)) ? File.ReadAllText(System.IO.Path.Combine(scripts, file)) : string.Empty;
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        string list = Read(@"TestRun\RunList.cs"), plan = Read(@"TestRun\RunPlan.cs");
        var sources = new List<KeyValuePair<string, string>>();
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            if (System.IO.Path.GetFileName(path) != "RunPlan.cs")
            {
                sources.Add(new KeyValuePair<string, string>(path, File.ReadAllText(path)));
            }
        }

        List<RunCheck> table = RunPlan.All();
        string unbound = null, unwritten = null, untexted = null, badLoad = null, badLook = null;
        foreach (RunCheck c in table)
        {
            bool own = RunPlan.Own(c.Id);
            string constant = own ? ConstantOf(plan, c.Id) : null;
            bool bound = list.Contains("case \"" + c.Id + "\":") || (constant != null && list.Contains("case RunPlan." + constant + ":"));
            if (c.AtLoad ? bound : !bound)
            {
                unbound = unbound ?? c.Id;
            }
            if (!own && (en == null || !en.ContainsKey(c.Id)))
            {
                untexted = untexted ?? c.Id;
            }
            if (c.AtLoad && (c.Phase != RunPhase.AfterLoad || c.Run != null))
            {
                badLoad = badLoad ?? c.Id;
            }
            if ((c.Look != null) != (c.Eyes != null) || (c.Look != null && (en == null || !en.ContainsKey(c.Look))))
            {
                badLook = badLook ?? c.Id;
            }
            bool found = c.Line.StartsWith("Test run (", StringComparison.Ordinal) && list.Contains("RunPlan." + LineConstant(plan, c.Line));
            foreach (KeyValuePair<string, string> source in sources)
            {
                found |= WritesLine(source.Value, c.Line);
            }
            if (!found)
            {
                unwritten = unwritten ?? c.Line;
            }
        }
        Check(unbound == null, "test run: every check of the table is given its work in RunList by its id, and a line the load writes none (" + (unbound ?? "all") + ")");
        Check(untexted == null, "test run: every test button the run presses has its label in en.json (" + (untexted ?? "all") + ")");
        Check(unwritten == null, "test run: every line the run waits for is written by a source of the mod (" + (unwritten ?? "all") + ")");
        Check(badLoad == null, "test run: a line the load writes is read after the load, with nothing pressed (" + (badLoad ?? "all") + ")");
        Check(badLook == null, "test run: every look for the eyes has its notice in en.json and its line in the checklist (" + (badLook ?? "all") + ")");
        bool ends = true;
        foreach (RunCheck c in table)
        {
            ends &= c.Phase != RunPhase.End || c.Place == RunPlace.Here || c.Id == RunPlan.LookBuilding;
        }
        Check(ends, "test run: every put back at the end runs where the player stands, so a stopped run runs them too");

        // Every test of the city expansion's parts is in the run, pressed by a step of its own, or listed with why not.
        var expansion = new List<string>();
        foreach (string file in new[] { @"NewBuildings\NewBuildingsGuide.cs", @"NewGround\NewGroundGuide.cs", @"BuildTrades\TradesGuide.cs", @"Uptown\GroundGuide.cs", @"Uptown\LifeGuide.cs",
                     @"Construction\ConstructionGuide.cs", @"Core\CityCheck.cs" })
        {
            foreach (Match m in Regex.Matches(Read(file), @"""(reallife_[a-z_]+_test_[a-z_]+|reallife_test_city_[a-z]+)"""))
            {
                if (!expansion.Contains(m.Groups[1].Value) && m.Groups[1].Value != "reallife_bld_test_host" && m.Groups[1].Value != "reallife_bld_test_done")
                {
                    expansion.Add(m.Groups[1].Value);
                }
            }
        }
        foreach (Match m in Regex.Matches(Read(@"Core\RealLifeOverhaulMod.cs"), @"AddButton\(""(reallife_construction_test_[a-z]+|reallife_held_test)"""))
        {
            expansion.Add(m.Groups[1].Value);
        }
        expansion.Add("reallife_floors_test_place");
        expansion.Add("reallife_test_district_save");
        // A feature's own .Check(...) puts its test in the run as the table does; the run keeps only the first of an id met twice.
        var registered = new List<string>();
        bool once = true;
        foreach (KeyValuePair<string, string> source in sources)
        {
            foreach (Match m in Regex.Matches(Regex.Replace(source.Value, @"^\s*//.*$", string.Empty, RegexOptions.Multiline), @"\.Check\(""(reallife_[a-z_]+)"""))
            {
                once &= !registered.Contains(m.Groups[1].Value) && !table.Exists(c => c.Id == m.Groups[1].Value);
                registered.Add(m.Groups[1].Value);
            }
        }
        Check(once, "test run: no test is both in the run's table and a feature's .Check(...), or in two .Check(...) lines (the run would drop the second)");
        string missing = null;
        int listed = 0;
        foreach (string key in expansion)
        {
            bool inRun = table.Exists(c => c.Id == key) || registered.Contains(key);
            bool byStep = false, notRun = false;
            for (int i = 0; i < RunPlan.ByStep.GetLength(0); i++)
            {
                byStep |= RunPlan.ByStep[i, 0] == key && table.Exists(c => c.Id == RunPlan.ByStep[i, 1]);
            }
            for (int i = 0; i < RunPlan.NotRun.GetLength(0); i++)
            {
                notRun |= RunPlan.NotRun[i, 0] == key && RunPlan.NotRun[i, 1].Length > 20;
            }
            listed += inRun ? 1 : 0;
            if (!(inRun || byStep || notRun) || (inRun && notRun))
            {
                missing = missing ?? key;
            }
        }
        Check(expansion.Count >= 60 && missing == null,
            "test run: each of the expansion's " + expansion.Count + " test buttons is in the run (" + listed + "), pressed by a step of its own, or listed with why not (" + (missing ?? "all") + ")");

        // A feature's own .Check(...) in its guide: its line written by a source, its label a text.
        string features = Read(@"Meta\Features.cs");
        Check(features.Contains("public Feature Check(string labelKey, Action run, string line, RunPlace place = RunPlace.Here, RunPhase phase = RunPhase.BeforeLoad")
              && features.Contains("Checks.Add(new RunCheck(labelKey, Id, line, place, phase, seconds) { Run = run });") && features.Contains("AddTest(labelKey, run);")
              && list.Contains("foreach (Feature feature in Features.All)") && list.Contains("all.Add(feature.Checks[k]);"),
            "test run: a feature joins the run with one .Check(...) in its guide, which is also its test button, and the run gathers every feature's");
        string guideless = null;
        foreach (KeyValuePair<string, string> source in sources)
        {
            string code = Regex.Replace(source.Value, @"^\s*//.*$", string.Empty, RegexOptions.Multiline);   // a comment's example is not a check
            foreach (Match m in Regex.Matches(code, @"\.Check\(""(reallife_[a-z_]+)"",\s*[^,]+,\s*""([^""]+)"""))
            {
                bool written = false;
                foreach (KeyValuePair<string, string> other in sources)
                {
                    written |= WritesLine(other.Value.Replace(m.Value, string.Empty), m.Groups[2].Value);
                }
                if (!written || en == null || !en.ContainsKey(m.Groups[1].Value))
                {
                    guideless = guideless ?? m.Groups[1].Value;
                }
            }
        }
        Check(guideless == null, "test run: every feature's own check has its label in en.json and its line written by a source (" + (guideless ?? "all") + ")");

        // The test buttons it presses exist as the run names them (the build would fail otherwise; this names the method).
        foreach (string method in new[] { "TestAskOnOpenGround", "TestNoLongerAsked", "TestShowAgain" })
        {
            Check(Read(@"NewBuildings\NewBuildingsDirector.TestRun.cs").Contains(" " + method + "(") && list.Contains(method), "test run: new buildings' " + method + " is there and the run calls it");
        }
        Check(Read(@"Civic\CivicDirector.TestRun.cs").Contains("public int TestTakeAway(int from, int to)") && list.Contains("TestTakeAway(from, to)") && list.Contains("TestNextSite"),
            "test run: the sites the civic tests set out are taken back between the ids kept before and after them");
        string off = MemberText(Read(@"NewBuildings\NewBuildingsDirector.Tests.cs"), "public void TestOff()");
        Check(off.Contains("if (_made[i].Spec.Life)") && off.Contains("hidden == hides && kept == hides && after"),
            "test run: the switch-off test counts only the buildings the switch hides (a district's are left to their district)");
    }

    // The constant of RunPlan whose value is the id, or null.
    private static string ConstantOf(string plan, string id)
    {
        Match m = Regex.Match(plan, @"public const string (\w+) = """ + Regex.Escape(id) + @""";");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string LineConstant(string plan, string line)
    {
        Match m = Regex.Match(plan, @"public const string (\w+) = """ + Regex.Escape(line) + @""";");
        return m.Success ? m.Groups[1].Value : "(none)";
    }

    // True when a source writes the line: its words, or their start before a part filled in at run time (a name, an
    // address), begin a string of the source. Cut at a space or a comma, never short of the first word in a check's
    // brackets; a check whose bracket begins with a name filled in has its family and that name as strings of its own.
    private static bool WritesLine(string source, string line)
    {
        int open = line.IndexOf(" check (", StringComparison.Ordinal);
        int least = 12;
        string first = null;
        if (open > 0)
        {
            int from = open + " check (".Length;
            int to = line.IndexOfAny(new[] { ' ', ',', ')' }, from);
            first = line.Substring(from, (to > from ? to : line.Length) - from);
            least = from + first.Length;
            if (source.Contains("\"" + line.Substring(0, from) + "\"") && first.Length > 0 && source.Contains("\"" + first))
            {
                return true;   // "Made buildings check (" + name, its name "test building" a string of the same source
            }
        }
        string words = line;
        while (words.Length >= least)
        {
            if (source.Contains("\"" + words))
            {
                return true;
            }
            int cut = Math.Max(words.LastIndexOf(' ', words.Length - 2), words.LastIndexOf(',', words.Length - 2));
            if (cut <= 0)
            {
                return false;
            }
            words = words.Substring(0, cut + 1);
        }
        return false;
    }

    // ---------------------------------------------------------------- the game's side, read as text

    private static void TestRunSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file) => File.Exists(System.IO.Path.Combine(scripts, file)) ? File.ReadAllText(System.IO.Path.Combine(scripts, file)) : string.Empty;
        string director = Read(@"TestRun\RunDirector.cs"), game = Read(@"TestRun\RunGame.cs"), list = Read(@"TestRun\RunList.cs"), rules = Read(@"TestRun\RunRules.cs"),
            plan = Read(@"TestRun\RunPlan.cs"), guide = Read(@"TestRun\RunGuide.cs"), mod = Read(@"Core\RealLifeOverhaulMod.cs");
        Check(director.Length > 0 && game.Length > 0 && list.Length > 0 && rules.Length > 0 && plan.Length > 0 && guide.Length > 0, "test run: its files are there");

        // Nothing in normal play: no frame method, one PlayerPrefs read at start-up, the log heard only while a run is under way.
        bool frames = false;
        foreach (string text in new[] { director, game, list, rules, plan, guide })
        {
            frames |= Regex.IsMatch(text, @"void (Update|LateUpdate|FixedUpdate)\(") || text.Contains(": MonoBehaviour");
        }
        Check(!frames, "test run: no frame method and no component of its own: it is one coroutine on the mod's own object while a run is under way");
        string begin = MemberText(director, "public static void Begin(IModLogger logger)");
        Check(begin.Contains("if (kept.Length == 0)") && begin.IndexOf("return;", StringComparison.Ordinal) > begin.IndexOf("if (kept.Length == 0)", StringComparison.Ordinal)
              && begin.IndexOf("Listen();", StringComparison.Ordinal) > begin.IndexOf("if (kept.Length == 0)", StringComparison.Ordinal),
            "test run: at start-up it reads one PlayerPrefs text and does nothing more unless a run waits for its load");
        Check(Occurs(director, "Application.logMessageReceivedThreaded += Hear;") == 1 && Occurs(director, "Application.logMessageReceivedThreaded -= Hear;") == 1
              && MemberText(director, "private static void Finish()").Contains("Unlisten();"),
            "test run: the log is heard from the run's start and let go at its end");
        string onLog = MemberText(director, "private static void OnLog(string text, string stack, LogType type)");
        Check(onLog.Contains("Thread.CurrentThread.ManagedThreadId != _mainThread") && !onLog.Contains("Logger") && onLog.Contains("catch (Exception)"),
            "test run: a logged line is read on the main thread only, never logs, and a fault drops it");
        Check(mod.Contains("Guard.Step(\"the city expansion's test run, if one waits for its load\", () => TestRun.RunDirector.Begin(context.Logger));"),
            "test run: its start-up step is one guarded step of the mod's start, before any save is read");
        Check(director.Contains("Meta.MetaUi.Instance.Run(Guard.Routine(Run(false)))") && director.Contains("Meta.MetaUi.Instance.Run(Guard.Routine(Run(true)))")
              && list.Contains("MetaUi.Instance.Run(Guard.Routine(PlayerPutBack()))") && list.Contains("MetaUi.Instance.Run(Guard.Routine(CensusLater()))"),
            "test run: every coroutine runs behind the guard on the mod's own persistent object, which outlives the load");
        Check(director.Contains("private const string Pref = \"reallife.testrun\";"), "test run: its PlayerPrefs key has a dot, so the locale checks never take it for a text");

        // Safety: the tools must be on, never in a session or another city; a test's fault is caught; the button stops a run.
        string press = MemberText(director, "public static void Press()");
        int stop = press.IndexOf("_stopping = true;", StringComparison.Ordinal), tools = press.IndexOf("!Meta.Dev.Verbose || !Settings.ShowTestTools", StringComparison.Ordinal),
            session = press.IndexOf("RunGame.Session", StringComparison.Ordinal), city = press.IndexOf("RunGame.OtherCity", StringComparison.Ordinal),
            start = press.IndexOf("Run(false)", StringComparison.Ordinal);
        Check(stop > 0 && tools > stop && session > tools && city > session && start > city, "test run: pressed again it stops; else it needs the tools on, refuses in a session and another city, then begins");
        Check(game.Contains("public static bool Session => GoingPublic.InSession;") && !Regex.IsMatch(director + game + list, @"\b(Send|Broadcast|Rpc|Network)\w*\("),
            "test run: a Going Public session is asked, and nothing is sent");
        string pressOne = MemberText(director, "private static bool Press(RunCheck c, ref RunResult result, ref string line)");
        string one = MemberText(director, "private static IEnumerator One(RunCheck c)");
        Check(pressOne.Contains("catch (Exception e)") && pressOne.Contains("result = RunResult.Fault;") && one.Contains("c.Undo();") && one.Contains("catch (Exception e)"),
            "test run: a test or a put back that throws is logged and the run goes on");
        Check(one.Contains("if (RunGame.Running || !RunGame.Loaded)") && one.Contains("waited += now - last;") && one.Contains("waited > c.Seconds"),
            "test run: a check's time counts only while the game runs (a menu or the pause stops it)");
        string run = MemberText(director, "private static IEnumerator Run(bool carried)");
        int save = run.IndexOf("RunGame.SaveStart(RunText.StartSave", StringComparison.Ordinal), first = run.IndexOf("yield return One(order[i]);", StringComparison.Ordinal),
            keep = run.IndexOf("Keep();", StringComparison.Ordinal), load = run.IndexOf("RunText.Load", StringComparison.Ordinal), after = run.IndexOf("RunPhase.AfterLoad", StringComparison.Ordinal),
            back = run.IndexOf("order[i].Phase == RunPhase.End && order[i].Place == RunPlace.Here", StringComparison.Ordinal), sum = run.IndexOf("Summary(order, stopped);", StringComparison.Ordinal);
        Check(save > 0 && first > save && keep > first && load > keep && after > load && back > after && sum > back,
            "test run: the save that undoes it first, the checks, the run kept and the load asked for, the checks after it, the put backs on a stop, the summary");
        Check(RunText.StartSave == "RealLifeOverhaul test run start" && game.Contains("SaveGameManager.Save(SaveGameManager.SaveType.OldAgeBackUp, name, folder)")
              && game.Contains("save.SaveGameName = kept;") && game.Contains("SaveGameManager.MarkChange();"),
            "test run: its first save is the game's own, under its name, the save played kept under its own name and still unsaved where it was");

        // The one file that touches the game; the move is the taxi's.
        foreach (string word in new[] { "SaveGameManager", "BuildingManager", "UiFader", "PedestrianCam", "NavMesh", "InstanceBehavior" })
        {
            Check(!director.Contains(word) && !list.Contains(word) && !rules.Contains(word) && !plan.Contains(word), "test run: only RunGame names the game's " + word);
        }
        string move = MemberText(game, "public static IEnumerator MoveTo(Vector3 at, float yaw)");
        int hide = move.IndexOf("ToggleVisibility(show: false)", StringComparison.Ordinal), fade = move.IndexOf("UiFader.Fade(", StringComparison.Ordinal),
            warp = move.IndexOf("WarpSafely(at)", StringComparison.Ordinal), show = move.IndexOf("ToggleVisibility(show: true)", StringComparison.Ordinal),
            unfade = move.IndexOf("UiFader.UnFade(", StringComparison.Ordinal);
        Check(hide > 0 && fade > hide && warp > fade && show > warp && unfade > show && move.Contains("SetNavigationBlocker(Blocker)") && move.Contains("UnsetNavigationBlocker(Blocker)"),
            "test run: the player is moved as the game's taxi moves them: hidden and held, the black screen, the warp onto the walking mesh, shown, the black screen off");
        Check(game.Contains("manager.ExitFromBuilding(0);") && game.Contains("game.selectedVehicle.ExitVehicle();") && !Regex.IsMatch(game, @"FindObjects?OfType|FindObjectsByType|GameObject\.Find\("),
            "test run: out of a building and a vehicle the game's own way, and no search of the scene");
        Check(game.Contains("GameDlc.Open(InstanceBehavior<CityManager>.Instance)") && game.Contains("timeMachine.StartTimeMachine(goal, disableCancel: true)"),
            "test run: the scaffolding's building is picked through the DLC rule, and time is run on by the game's own time machine");
        Check(guide.Contains("[DescribesFeature]") && guide.Contains(".TestFirst(RunText.Button, RunDirector.Press)"), "test run: its button is the first of the Diagnostics tests");
    }

    // ---------------------------------------------------------------- the texts

    private static void TestRunTexts()
    {
        string folder = LocalesFolder();
        string keysText = File.ReadAllText(System.IO.Path.Combine(folder, "..", "Scripts", "TestRun", "RunText.cs"));
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")), zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        var keys = new List<string>();
        foreach (Match m in Regex.Matches(keysText, @"public const string \w+ = ""(reallife_[a-z_]+)"";"))
        {
            keys.Add(m.Groups[1].Value);
        }
        string missing = null;
        bool short240 = true;
        foreach (string key in keys)
        {
            if (en == null || pt == null || zh == null || !en.ContainsKey(key) || !pt.ContainsKey(key) || !zh.ContainsKey(key) || !SameHoles(en[key], pt[key], zh[key]))
            {
                missing = missing ?? key;
                continue;
            }
            int longest = en[key].Replace("{failed}", new string('x', 100)).Replace("{passed}", "100").Replace("{ran}", "100").Replace("{save}", RunText.StartSave).Length;
            short240 &= longest <= 240;
        }
        Check(keys.Count == 16 && missing == null, "test run: every text is in en, pt and zh-cn with the same {holes} (" + (missing ?? keys.Count + " texts") + ")");
        Check(short240, "test run: every notice is at most 240 characters in English, with its longest list of failures");
        Check(en != null && en.TryGetValue(RunText.Load, out string load) && load == "Save and load now; the run carries on by itself."
              && en.TryGetValue(RunText.Button, out string button) && button == "City expansion: run every check",
            "test run: the button and the load's notice say what the owner was promised");
    }
}
